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
    }
}
