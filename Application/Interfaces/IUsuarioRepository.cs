using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface IUsuarioRepository
    {
        Task<Usuario?> GetByEmailAsync(string email);
        Task<Usuario?> GetByIdAsync(Guid id);
        Task<List<UsuarioRol>> GetRolesAsync(Guid usuarioId);
        Task UpdateUltimoAccesoAsync(Guid usuarioId);
    }
}
