using DataMedix.Domain.Entities;


namespace DataMedix.Application.Interfaces
{
    public interface ITenantRepository
    {
        Task<Tenant?> GetBySubdomainAsync(string subdomain);
    }
}
