using DataMedix.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace DataMedix.Portal.Middleware
{
    /// <summary>
    /// Resuelve el tenant activo por request y popula ITenantContext (scoped).
    ///
    /// Estrategia doble:
    ///   1. Subdominio → lookup en DB (cacheado 30 min) para obtener el tenant_id del host.
    ///   2. Cookie claim "tenant_id" → fuente de verdad del usuario autenticado.
    ///
    /// Validación de seguridad: si el subdominio apunta a un tenant diferente al de la cookie,
    /// rechaza con 403. Esto impide que un usuario de lab02 acceda a lab01.
    ///
    /// Para rutas de static files y SignalR hubs, el middleware opera sin bloquear
    /// aunque no haya tenant resuelto.
    ///
    /// Registro en Program.cs: app.UseMiddleware&lt;TenantContextMiddleware&gt;()
    /// DESPUÉS de app.UseAuthentication() y ANTES de app.UseAuthorization().
    /// </summary>
    public sealed class TenantContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _connectionString;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TenantContextMiddleware> _logger;

        // Paths que no requieren tenant (static assets, SignalR, health checks)
        private static readonly string[] _bypassPrefixes =
            ["/_blazor", "/_framework", "/favicon", "/.well-known"];

        public TenantContextMiddleware(
            RequestDelegate next,
            IConfiguration config,
            IMemoryCache cache,
            ILogger<TenantContextMiddleware> logger)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
            _connectionString = config.GetConnectionString("DatabasePostgres")
                ?? throw new InvalidOperationException("ConnectionStrings:DatabasePostgres no configurado.");
        }

        public async Task InvokeAsync(HttpContext context, TenantContext tenantCtx)
        {
            var path = context.Request.Path.Value ?? "";

            // Bypass para assets y SignalR — no necesitan contexto de tenant
            if (_bypassPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            // ── 1. Resolver tenant desde subdominio (cacheado) ────────────────
            var host = context.Request.Host.Host;
            var subdomain = host.Split('.')[0];
            var subdomainTenantId = await GetTenantIdBySubdomainAsync(subdomain);

            // ── 2. Si el usuario está autenticado, leer claim de la cookie ────
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var claimStr = context.User.FindFirst("tenant_id")?.Value;
                if (Guid.TryParse(claimStr, out var cookieTenantId) && cookieTenantId != Guid.Empty)
                {
                    // ── 3. Validación de seguridad: subdomain ↔ cookie ────────
                    if (subdomainTenantId.HasValue && subdomainTenantId.Value != cookieTenantId)
                    {
                        _logger.LogWarning(
                            "Mismatch tenant: subdomain={Subdomain} → {SubTenant}, cookie → {CookieTenant}. IP={IP}",
                            subdomain, subdomainTenantId, cookieTenantId,
                            context.Connection.RemoteIpAddress);

                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("Acceso denegado: laboratorio incorrecto.");
                        return;
                    }

                    // ── 4. Contexto resuelto ──────────────────────────────────
                    tenantCtx.Set(cookieTenantId, subdomain);
                }
            }
            else if (subdomainTenantId.HasValue)
            {
                // Usuario no autenticado: popula el tenant del subdominio para que
                // la página de login muestre el branding correcto del laboratorio.
                tenantCtx.Set(subdomainTenantId.Value, subdomain);
            }

            await _next(context);
        }

        /// <summary>
        /// Busca el tenant_id para un subdominio dado.
        /// Cacheado 30 minutos — los tenants cambian muy raramente.
        /// Usa Npgsql raw (no EF) para evitar dependencia circular con DataMedixDbContext
        /// y para leer solo la columna necesaria sin cargar el objeto completo.
        /// </summary>
        private async Task<Guid?> GetTenantIdBySubdomainAsync(string subdomain)
        {
            var cacheKey = $"tenant:sub:{subdomain}";
            if (_cache.TryGetValue<Guid>(cacheKey, out var cached))
                return cached;

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT id FROM tenant WHERE subdomain = $1 AND activo = true LIMIT 1";
                cmd.Parameters.AddWithValue(subdomain);
                var result = await cmd.ExecuteScalarAsync();

                if (result is Guid tid)
                {
                    _cache.Set(cacheKey, tid, TimeSpan.FromMinutes(30));
                    return tid;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al resolver tenant por subdominio '{Subdomain}'", subdomain);
            }

            return null;
        }
    }
}
