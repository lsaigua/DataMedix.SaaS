using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface ITenantRepository
    {
        Task<Tenant?> GetBySubdomainAsync(string subdomain);
        Task<Tenant?> GetByIdAsync(Guid id);
        Task UpdateNombreAsync(Guid id, string nombre);
    }
}
