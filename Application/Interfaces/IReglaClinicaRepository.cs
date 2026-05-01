using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface IReglaClinicaRepository
    {
        /// <summary>Reglas activas ordenadas por prioridad ASC, con JSON ya parseado.</summary>
        Task<IReadOnlyList<ReglaClinica>> GetActiveAsync();

        Task<IReadOnlyList<ReglaClinica>> GetAllAsync(Guid? tenantId = null);
        Task<ReglaClinica?> GetByCodigoAsync(string codigo);
        Task<bool> ExisteCodigoAsync(string codigo);
        Task AddAsync(ReglaClinica regla);
        Task UpdateAsync(ReglaClinica regla);
    }
}
