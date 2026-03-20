using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Infrastructure.Persistence
{
    public class TenantRepository : ITenantRepository
    {
        private readonly DataMedixDbContext _context;

        public TenantRepository(DataMedixDbContext context)
        {
            _context = context;
        }

        public async Task<Tenant?> GetBySubdomainAsync(string subdomain)
        {
            return await _context.Tenants
                .FirstOrDefaultAsync(t => t.subdomain == subdomain && t.isactive);
        }
    }
}
