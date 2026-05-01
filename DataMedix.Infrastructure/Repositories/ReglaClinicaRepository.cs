using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataMedix.Infrastructure.Repositories
{
    public class ReglaClinicaRepository : IReglaClinicaRepository
    {
        private readonly DataMedixDbContext _db;
        public ReglaClinicaRepository(DataMedixDbContext db) => _db = db;

        public async Task<IReadOnlyList<ReglaClinica>> GetActiveAsync()
        {
            var reglas = await _db.ReglasClinicas
                .Where(r => r.Activo)
                .OrderBy(r => r.Prioridad)
                .AsNoTracking()
                .ToListAsync();

            foreach (var r in reglas) r.Parse();
            return reglas;
        }

        public async Task<IReadOnlyList<ReglaClinica>> GetAllAsync(Guid? tenantId = null) =>
            await _db.ReglasClinicas
                .Where(r => tenantId == null || r.TenantId == null || r.TenantId == tenantId)
                .OrderBy(r => r.Prioridad)
                .AsNoTracking()
                .ToListAsync();

        public async Task<ReglaClinica?> GetByCodigoAsync(string codigo) =>
            await _db.ReglasClinicas.FirstOrDefaultAsync(r => r.Codigo == codigo);

        public async Task<bool> ExisteCodigoAsync(string codigo) =>
            await _db.ReglasClinicas.AnyAsync(r => r.Codigo == codigo);

        public async Task AddAsync(ReglaClinica regla) =>
            await _db.ReglasClinicas.AddAsync(regla);

        public async Task UpdateAsync(ReglaClinica regla)
        {
            regla.UpdatedAt = DateTime.UtcNow;
            _db.ReglasClinicas.Update(regla);
            await _db.SaveChangesAsync();
        }
    }
}
