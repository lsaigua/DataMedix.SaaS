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

        public async Task<Tenant?> GetByIdAsync(Guid id) =>
            await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

        public async Task UpdateNombreAsync(Guid id, string nombre) =>
            await _context.Tenants
                .Where(t => t.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Nombre, nombre)
                    .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));
    }
}
