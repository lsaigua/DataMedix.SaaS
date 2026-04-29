using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMedix.Infrastructure.Persistence
{
    public class TenantRepository : ITenantRepository
    {
        private readonly DataMedixDbContext _context;

        public TenantRepository(DataMedixDbContext context)
        {
            _context = context;
        }

        public async Task<Tenant?> GetBySubdomainAsync(string subdomain) =>
            await _context.Tenants
                .FirstOrDefaultAsync(t => t.Subdomain == subdomain && t.Activo);
    }
}
