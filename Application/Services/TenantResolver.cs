using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;

namespace DataMedix.Application.Services
{
    public class TenantResolver : ITenantResolver
    {
        private readonly ITenantRepository _repository;

        public TenantResolver(ITenantRepository repository)
        {
            _repository = repository;
        }

        public async Task<Tenant?> ResolveAsync(string host)
        {
            var subdomain = host.Split('.')[0];
            return await _repository.GetBySubdomainAsync(subdomain);
        }
    }
}
