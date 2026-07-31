using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DataMedix.Infrastructure.Services
{
    /// <summary>
    /// Permisos efectivos por tenant y rol, con caché en memoria.
    ///
    /// Se consulta en cada navegación y en cada render del menú, así que la
    /// caché no es un lujo: sin ella cada clic golpearía la base varias veces.
    /// Se invalida al guardar la matriz del tenant.
    /// </summary>
    public class PermisoService : IPermisoService
    {
        private readonly DataMedixDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IAuditoriaRepository _auditoria;

        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(20);
        private const string CacheKeyCatalogo = "permisos:catalogo";

        public PermisoService(DataMedixDbContext db, IMemoryCache cache, IAuditoriaRepository auditoria)
        {
            _db = db;
            _cache = cache;
            _auditoria = auditoria;
        }

        private static string CacheKeyMatriz(Guid tenantId) => $"permisos:matriz:{tenantId}";

        public async Task<List<Permiso>> GetCatalogoAsync()
        {
            if (_cache.TryGetValue(CacheKeyCatalogo, out List<Permiso>? cached) && cached is not null)
                return cached;

            var catalogo = await _db.Permisos
                .Where(p => p.Activo)
                .OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(CacheKeyCatalogo, catalogo, Ttl);
            return catalogo;
        }

        public async Task<Dictionary<Guid, HashSet<string>>> GetMatrizAsync(Guid tenantId)
        {
            var key = CacheKeyMatriz(tenantId);
            if (_cache.TryGetValue(key, out Dictionary<Guid, HashSet<string>>? cached) && cached is not null)
                return cached;

            // Se traen defaults y overrides juntos; el override gana por rol+permiso
            var filas = await _db.RolesPermisos
                .Where(rp => rp.TenantId == null || rp.TenantId == tenantId)
                .Select(rp => new { rp.RolId, rp.PermisoCodigo, rp.Permitido, rp.TenantId })
                .AsNoTracking()
                .ToListAsync();

            var matriz = new Dictionary<Guid, HashSet<string>>();

            foreach (var grupo in filas.GroupBy(f => new { f.RolId, f.PermisoCodigo }))
            {
                // Si el tenant definió su propio valor, ese manda sobre el default
                var efectiva = grupo.FirstOrDefault(f => f.TenantId == tenantId)
                            ?? grupo.First();

                if (!efectiva.Permitido) continue;

                if (!matriz.TryGetValue(efectiva.RolId, out var set))
                    matriz[efectiva.RolId] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                set.Add(efectiva.PermisoCodigo);
            }

            _cache.Set(key, matriz, Ttl);
            return matriz;
        }

        public async Task<HashSet<string>> GetPermisosEfectivosAsync(Guid tenantId, IEnumerable<string> roles)
        {
            var nombres = roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim().ToUpperInvariant())
                .ToHashSet();

            if (nombres.Count == 0)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rolesDb = await GetRolesAsync(incluirGlobales: true);
            var rolIds = rolesDb
                .Where(r => nombres.Contains(r.Nombre.ToUpperInvariant()))
                .Select(r => r.Id)
                .ToList();

            var matriz = await GetMatrizAsync(tenantId);

            var efectivos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rolId in rolIds)
                if (matriz.TryGetValue(rolId, out var set))
                    efectivos.UnionWith(set);

            return efectivos;
        }

        public async Task<bool> TienePermisoAsync(Guid tenantId, IEnumerable<string> roles, string permisoCodigo)
        {
            var efectivos = await GetPermisosEfectivosAsync(tenantId, roles);
            return efectivos.Contains(permisoCodigo);
        }

        public async Task<List<Rol>> GetRolesAsync(bool incluirGlobales = false)
        {
            const string key = "permisos:roles";
            if (!_cache.TryGetValue(key, out List<Rol>? roles) || roles is null)
            {
                roles = await _db.Roles
                    .Where(r => r.Activo)
                    .OrderBy(r => r.Nombre)
                    .AsNoTracking()
                    .ToListAsync();
                _cache.Set(key, roles, Ttl);
            }

            // SUPERADMIN es un rol de plataforma: no se ofrece en la matriz del tenant
            return incluirGlobales ? roles : roles.Where(r => !r.EsGlobal).ToList();
        }

        public async Task GuardarMatrizAsync(
            Guid tenantId, Dictionary<Guid, HashSet<string>> matriz, Guid usuarioId)
        {
            var catalogo = await GetCatalogoAsync();
            // Los permisos de plataforma no son delegables: se ignoran aunque
            // lleguen en el payload.
            var asignables = catalogo.Where(p => !p.SoloSuperadmin).Select(p => p.Codigo).ToList();
            var rolesTenant = (await GetRolesAsync()).Select(r => r.Id).ToHashSet();

            var existentes = await _db.RolesPermisos
                .Where(rp => rp.TenantId == tenantId)
                .ToListAsync();

            var indice = existentes.ToDictionary(rp => (rp.RolId, rp.PermisoCodigo));
            var ahora = DateTime.UtcNow;

            foreach (var rolId in rolesTenant)
            {
                matriz.TryGetValue(rolId, out var concedidos);
                concedidos ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var codigo in asignables)
                {
                    var permitido = concedidos.Contains(codigo);

                    if (indice.TryGetValue((rolId, codigo), out var fila))
                    {
                        if (fila.Permitido == permitido) continue;
                        fila.Permitido = permitido;
                        fila.UpdatedAt = ahora;
                        fila.UpdatedBy = usuarioId;
                    }
                    else
                    {
                        _db.RolesPermisos.Add(new RolPermiso
                        {
                            TenantId      = tenantId,
                            RolId         = rolId,
                            PermisoCodigo = codigo,
                            Permitido     = permitido,
                            UpdatedAt     = ahora,
                            UpdatedBy     = usuarioId
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
            InvalidarCache(tenantId);

            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenantId,
                UsuarioId   = usuarioId,
                Accion      = "UPDATE",
                Entidad     = "RolPermiso",
                Descripcion = "Matriz de permisos actualizada",
                CreatedAt   = ahora
            });
        }

        public async Task RestaurarPorDefectoAsync(Guid tenantId, Guid usuarioId)
        {
            await _db.RolesPermisos
                .Where(rp => rp.TenantId == tenantId)
                .ExecuteDeleteAsync();

            InvalidarCache(tenantId);

            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenantId,
                UsuarioId   = usuarioId,
                Accion      = "UPDATE",
                Entidad     = "RolPermiso",
                Descripcion = "Permisos restaurados a los valores por defecto",
                CreatedAt   = DateTime.UtcNow
            });
        }

        public void InvalidarCache(Guid tenantId) => _cache.Remove(CacheKeyMatriz(tenantId));
    }
}
