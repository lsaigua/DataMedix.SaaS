using BCrypt.Net;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataMedix.Infrastructure.Repositories
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly DataMedixDbContext _db;
        public UsuarioRepository(DataMedixDbContext db) => _db = db;

        public async Task<Usuario?> GetByEmailAsync(string email)
        {
            var user = await _db.Usuarios
                .Include(u => u.Roles).ThenInclude(ur => ur.Rol)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email && u.Activo);

            if (user is null) return null;

            // TenantId no existe en la columna usuario.tenant_id hasta correr el SQL migration.
            // Lo derivamos del primer registro activo en usuarioempresa (mapeado como Roles).
            if (!user.TenantId.HasValue)
            {
                var tenantId = user.Roles
                    .Where(r => r.Activo && r.TenantId.HasValue)
                    .Select(r => r.TenantId)
                    .FirstOrDefault();
                user.TenantId = tenantId;
            }

            return user;
        }

        public async Task<Usuario?> GetByIdAsync(Guid id)
        {
            var user = await _db.Usuarios
                .Include(u => u.Roles).ThenInclude(ur => ur.Rol)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.Activo);

            if (user is null) return null;

            if (!user.TenantId.HasValue)
            {
                user.TenantId = user.Roles
                    .Where(r => r.Activo && r.TenantId.HasValue)
                    .Select(r => r.TenantId)
                    .FirstOrDefault();
            }

            return user;
        }

        public async Task<List<UsuarioRol>> GetRolesAsync(Guid usuarioId) =>
            await _db.UsuariosRoles
                .Include(ur => ur.Rol)
                .Where(ur => ur.UsuarioId == usuarioId && ur.Activo)
                .ToListAsync();

        public async Task UpdateUltimoAccesoAsync(Guid usuarioId)
        {
            // La columna ultimo_acceso se agrega via SQL migration.
            // Si aún no existe en la DB esta llamada lanzaría error; se protege con try/catch.
            try
            {
                await _db.Usuarios
                    .Where(u => u.Id == usuarioId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.UltimoAcceso, DateTime.UtcNow));
            }
            catch
            {
                // columna aún no migrada; ignorar silenciosamente
            }
        }

        public async Task<List<Usuario>> GetByTenantAsync(
            Guid tenantId, string? busqueda, int pagina, int tamano)
        {
            var idsEnTenant = await _db.UsuariosRoles
                .Where(r => r.TenantId == tenantId && r.Activo)
                .Select(r => r.UsuarioId)
                .Distinct()
                .ToListAsync();

            var q = _db.Usuarios
                .Include(u => u.Roles).ThenInclude(ur => ur.Rol)
                .Where(u => idsEnTenant.Contains(u.Id));

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var b = busqueda.Trim().ToUpper();
                q = q.Where(u =>
                    u.Email.ToUpper().Contains(b) ||
                    u.PrimerNombre.ToUpper().Contains(b) ||
                    u.PrimerApellido.ToUpper().Contains(b) ||
                    u.Identificacion.Contains(b));
            }

            return await q
                .OrderBy(u => u.PrimerApellido)
                .ThenBy(u => u.PrimerNombre)
                .Skip((pagina - 1) * tamano)
                .Take(tamano)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> CountAsync(Guid tenantId, string? busqueda)
        {
            var idsEnTenant = await _db.UsuariosRoles
                .Where(r => r.TenantId == tenantId && r.Activo)
                .Select(r => r.UsuarioId)
                .Distinct()
                .ToListAsync();

            var q = _db.Usuarios.Where(u => idsEnTenant.Contains(u.Id));

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var b = busqueda.Trim().ToUpper();
                q = q.Where(u =>
                    u.Email.ToUpper().Contains(b) ||
                    u.PrimerNombre.ToUpper().Contains(b) ||
                    u.PrimerApellido.ToUpper().Contains(b) ||
                    u.Identificacion.Contains(b));
            }

            return await q.CountAsync();
        }

        public async Task AddAsync(Usuario usuario, string passwordPlano, Guid rolId)
        {
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordPlano);
            usuario.CreatedAt = DateTime.UtcNow;
            await _db.Usuarios.AddAsync(usuario);
            await _db.SaveChangesAsync();

            await _db.UsuariosRoles.AddAsync(new UsuarioRol
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuario.Id,
                RolId = rolId,
                TenantId = usuario.TenantId,
                Activo = true,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Usuario usuario)
        {
            usuario.UpdatedAt = DateTime.UtcNow;
            _db.Usuarios.Update(usuario);
            await _db.SaveChangesAsync();
        }

        public async Task<List<Rol>> GetRolesDisponiblesAsync() =>
            await _db.Roles
                .Where(r => r.Activo && r.Nombre != "SUPERADMIN")
                .OrderBy(r => r.Nombre)
                .AsNoTracking()
                .ToListAsync();
    }
}
