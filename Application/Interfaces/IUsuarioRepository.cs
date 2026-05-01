using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface IUsuarioRepository
    {
        Task<Usuario?> GetByEmailAsync(string email);
        Task<Usuario?> GetByIdAsync(Guid id);
        Task<List<UsuarioRol>> GetRolesAsync(Guid usuarioId);
        Task UpdateUltimoAccesoAsync(Guid usuarioId);

        // Admin CRUD
        Task<List<Usuario>> GetByTenantAsync(Guid tenantId, string? busqueda, int pagina, int tamano);
        Task<int> CountAsync(Guid tenantId, string? busqueda);
        Task AddAsync(Usuario usuario, string passwordPlano, Guid rolId);
        Task UpdateAsync(Usuario usuario);
        Task<List<Rol>> GetRolesDisponiblesAsync();
    }
}
