using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DataMedix.Infrastructure.Services
{
    /// <summary>
    /// Alta y mantenimiento de los clientes del SaaS.
    ///
    /// Como TarifaService, atraviesa tenants a propósito: usa
    /// IgnoreQueryFilters() de forma acotada y está protegido por el permiso de
    /// plataforma "tenants.gestionar".
    /// </summary>
    public class TenantAdminService : ITenantAdminService
    {
        private readonly DataMedixDbContext _db;
        private readonly IUsuarioRepository _usuarios;
        private readonly IAuditoriaRepository _auditoria;
        private readonly IMemoryCache _cache;

        /// <summary>Tramos por defecto según la tabla comercial publicada.</summary>
        private static readonly (int Desde, int? Hasta, decimal Precio)[] TramosPorDefecto =
        [
            (1, 100, 3.50m),
            (101, 300, 2.80m),
            (301, null, 2.20m)
        ];

        public TenantAdminService(
            DataMedixDbContext db,
            IUsuarioRepository usuarios,
            IAuditoriaRepository auditoria,
            IMemoryCache cache)
        {
            _db = db;
            _usuarios = usuarios;
            _auditoria = auditoria;
            _cache = cache;
        }

        public async Task<List<TenantAdminDto>> ListarAsync(bool incluirInactivos = true)
        {
            var q = _db.Tenants.Where(t => t.DeletedAt == null);
            if (!incluirInactivos) q = q.Where(t => t.Activo);

            var tenants = await q.OrderBy(t => t.Nombre).AsNoTracking().ToListAsync();
            var ids = tenants.Select(t => t.Id).ToList();

            // Métricas de uso: distinguen un cliente operando de uno recién creado
            var usuarios = await _db.Usuarios
                .Where(u => u.TenantId != null && ids.Contains(u.TenantId.Value) && u.Activo)
                .GroupBy(u => u.TenantId!.Value)
                .Select(g => new { TenantId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.TenantId, x => x.Total);

            var pacientes = await _db.Pacientes
                .IgnoreQueryFilters()
                .Where(p => ids.Contains(p.TenantId) && p.Activo)
                .GroupBy(p => p.TenantId)
                .Select(g => new { TenantId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.TenantId, x => x.Total);

            var actividad = await _db.ResultadosLaboratorio
                .IgnoreQueryFilters()
                .Where(r => ids.Contains(r.TenantId))
                .GroupBy(r => r.TenantId)
                .Select(g => new { TenantId = g.Key, Ultima = g.Max(x => x.CreatedAt) })
                .ToDictionaryAsync(x => x.TenantId, x => x.Ultima);

            return tenants.Select(t => Map(
                t,
                usuarios.GetValueOrDefault(t.Id),
                pacientes.GetValueOrDefault(t.Id),
                actividad.GetValueOrDefault(t.Id))).ToList();
        }

        public async Task<TenantAdminDto?> ObtenerAsync(Guid tenantId)
        {
            var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId);
            return t is null ? null : Map(t, 0, 0, null);
        }

        public async Task<bool> SubdominioDisponibleAsync(string subdomain, Guid? excluirTenantId = null)
        {
            var sub = Normalizar(subdomain);
            if (string.IsNullOrWhiteSpace(sub)) return false;

            return !await _db.Tenants
                .AnyAsync(t => t.Subdomain == sub &&
                               (excluirTenantId == null || t.Id != excluirTenantId.Value));
        }

        public async Task<Guid> CrearAsync(TenantAdminDto dto, AdminInicialDto admin, Guid usuarioId)
        {
            var subdomain = Normalizar(dto.Subdomain);

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new InvalidOperationException("El nombre del cliente es obligatorio.");
            if (string.IsNullOrWhiteSpace(subdomain))
                throw new InvalidOperationException("El subdominio es obligatorio.");
            if (!await SubdominioDisponibleAsync(subdomain))
                throw new InvalidOperationException($"El subdominio «{subdomain}» ya está en uso.");
            if (string.IsNullOrWhiteSpace(admin.Email))
                throw new InvalidOperationException("El correo del administrador inicial es obligatorio.");
            if (string.IsNullOrWhiteSpace(admin.Password) || admin.Password.Length < 6)
                throw new InvalidOperationException("La contraseña del administrador debe tener al menos 6 caracteres.");

            var rolAdmin = await _db.Roles.FirstOrDefaultAsync(r => r.Nombre == "ADMIN")
                ?? throw new InvalidOperationException("No existe el rol ADMIN en el catálogo.");

            var tenant = new Tenant
            {
                Id            = Guid.NewGuid(),
                Codigo        = string.IsNullOrWhiteSpace(dto.Codigo)
                                    ? subdomain.ToUpperInvariant()
                                    : dto.Codigo.Trim(),
                Nombre        = dto.Nombre.Trim(),
                Subdomain     = subdomain,
                Ruc           = dto.Ruc?.Trim(),
                EmailContacto = dto.EmailContacto?.Trim(),
                Telefono      = dto.Telefono?.Trim(),
                Direccion     = dto.Direccion?.Trim(),
                Ciudad        = dto.Ciudad?.Trim(),
                Pais          = dto.Pais?.Trim(),
                PlanNombre    = dto.PlanNombre?.Trim(),
                Activo        = dto.Activo,
                IsolationMode = "shared",
                ModeloCobro   = ModeloCobro.Mixto,
                Moneda        = "USD",
                CreatedAt     = DateTime.UtcNow
            };

            _db.Tenants.Add(tenant);

            // Tarifas por defecto: sin ellas el cliente aparecería con total cero
            foreach (var (desde, hasta, precio) in TramosPorDefecto)
            {
                _db.TenantTarifaTramos.Add(new TenantTarifaTramo
                {
                    TenantId       = tenant.Id,
                    DesdePacientes = desde,
                    HastaPacientes = hasta,
                    PrecioPaciente = precio
                });
            }

            await _db.SaveChangesAsync();

            // Usuario administrador inicial. Va después del SaveChanges del
            // tenant porque la FK del usuario lo necesita ya persistido.
            var usuarioAdmin = new Usuario
            {
                Id             = Guid.NewGuid(),
                TenantId       = tenant.Id,
                Codigo         = $"ADM-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Identificacion = admin.Identificacion.Trim(),
                PrimerNombre   = admin.PrimerNombre.Trim(),
                PrimerApellido = admin.PrimerApellido.Trim(),
                Email          = admin.Email.Trim().ToLowerInvariant(),
                Activo         = true,
                CreatedBy      = usuarioId,
                PasswordHash   = ""
            };

            await _usuarios.AddAsync(usuarioAdmin, admin.Password, rolAdmin.Id);

            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenant.Id,
                UsuarioId   = usuarioId,
                Accion      = "CREATE",
                Entidad     = "Tenant",
                EntidadId   = tenant.Id,
                Descripcion = $"Cliente creado: {tenant.Nombre} ({tenant.Subdomain}). " +
                              $"Administrador inicial: {usuarioAdmin.Email}",
                CreatedAt   = DateTime.UtcNow
            });

            InvalidarCacheSubdominio(subdomain);
            return tenant.Id;
        }

        public async Task ActualizarAsync(TenantAdminDto dto, Guid usuarioId)
        {
            if (dto.Id is null) throw new InvalidOperationException("Falta el identificador del cliente.");

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == dto.Id.Value)
                ?? throw new InvalidOperationException("El cliente no existe.");

            var subdomain = Normalizar(dto.Subdomain);
            if (string.IsNullOrWhiteSpace(subdomain))
                throw new InvalidOperationException("El subdominio es obligatorio.");
            if (!await SubdominioDisponibleAsync(subdomain, tenant.Id))
                throw new InvalidOperationException($"El subdominio «{subdomain}» ya está en uso.");

            var subdomainAnterior = tenant.Subdomain;

            tenant.Codigo        = dto.Codigo?.Trim();
            tenant.Nombre        = dto.Nombre.Trim();
            tenant.Subdomain     = subdomain;
            tenant.Ruc           = dto.Ruc?.Trim();
            tenant.EmailContacto = dto.EmailContacto?.Trim();
            tenant.Telefono      = dto.Telefono?.Trim();
            tenant.Direccion     = dto.Direccion?.Trim();
            tenant.Ciudad        = dto.Ciudad?.Trim();
            tenant.Pais          = dto.Pais?.Trim();
            tenant.PlanNombre    = dto.PlanNombre?.Trim();
            tenant.UpdatedAt     = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenant.Id,
                UsuarioId   = usuarioId,
                Accion      = "UPDATE",
                Entidad     = "Tenant",
                EntidadId   = tenant.Id,
                Descripcion = $"Cliente actualizado: {tenant.Nombre} ({tenant.Subdomain})",
                CreatedAt   = DateTime.UtcNow
            });

            InvalidarCacheSubdominio(subdomainAnterior);
            InvalidarCacheSubdominio(subdomain);
        }

        public async Task CambiarEstadoAsync(Guid tenantId, bool activo, Guid usuarioId)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId)
                ?? throw new InvalidOperationException("El cliente no existe.");

            tenant.Activo    = activo;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenantId,
                UsuarioId   = usuarioId,
                Accion      = "UPDATE",
                Entidad     = "Tenant",
                EntidadId   = tenantId,
                Descripcion = activo
                    ? $"Cliente reactivado: {tenant.Nombre}"
                    : $"Cliente desactivado: {tenant.Nombre}. Los datos se conservan.",
                CreatedAt   = DateTime.UtcNow
            });

            InvalidarCacheSubdominio(tenant.Subdomain);
        }

        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// El middleware cachea la resolución subdominio → tenant 30 minutos.
        /// Sin invalidar, un cliente nuevo o renombrado tardaría media hora en
        /// poder entrar.
        /// </summary>
        private void InvalidarCacheSubdominio(string? subdomain)
        {
            if (!string.IsNullOrWhiteSpace(subdomain))
                _cache.Remove($"tenant:sub:{subdomain}");
        }

        /// <summary>Subdominio en minúsculas y sin espacios, como llega en el host.</summary>
        private static string Normalizar(string? subdomain) =>
            (subdomain ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", "");

        private static TenantAdminDto Map(Tenant t, int usuarios, int pacientes, DateTime? actividad) => new()
        {
            Id              = t.Id,
            Codigo          = t.Codigo,
            Nombre          = t.Nombre ?? "",
            Subdomain       = t.Subdomain,
            Ruc             = t.Ruc,
            EmailContacto   = t.EmailContacto,
            Telefono        = t.Telefono,
            Direccion       = t.Direccion,
            Ciudad          = t.Ciudad,
            Pais            = t.Pais,
            PlanNombre      = t.PlanNombre,
            Activo          = t.Activo,
            CreatedAt       = t.CreatedAt,
            Usuarios        = usuarios,
            Pacientes       = pacientes,
            UltimaActividad = actividad
        };
    }
}
