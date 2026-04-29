using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface IPacienteRepository
    {
        Task<Paciente?> GetByIdentificacionAsync(Guid tenantId, string identificacion);
        Task<Paciente?> GetByIdAsync(Guid tenantId, Guid pacienteId);
        Task<List<Paciente>> GetAllAsync(Guid tenantId, string? busqueda = null, int pagina = 1, int tamano = 50);
        Task<int> CountAsync(Guid tenantId, string? busqueda = null);
        Task AddAsync(Paciente paciente);
        Task UpdateAsync(Paciente paciente);
    }
}
