using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataMedix.Infrastructure.Repositories
{
    public class AplicacionHierroRepository : IAplicacionHierroRepository
    {
        private readonly DataMedixDbContext _db;

        public AplicacionHierroRepository(DataMedixDbContext db) => _db = db;

        public async Task BatchReplaceAsync(IEnumerable<Guid> cronogramaIds, List<AplicacionHierro> aplicaciones)
        {
            var ids = cronogramaIds.ToList();

            if (ids.Count > 0)
            {
                var existentes = await _db.AplicacionesHierro
                    .Where(a => ids.Contains(a.CronogramaId))
                    .ToListAsync();

                if (existentes.Count > 0)
                    _db.AplicacionesHierro.RemoveRange(existentes);
            }

            if (aplicaciones.Count > 0)
                await _db.AplicacionesHierro.AddRangeAsync(aplicaciones);

            await _db.SaveChangesAsync();
        }

        public async Task DeleteByCronogramaIdsAsync(IEnumerable<Guid> cronogramaIds)
        {
            var ids = cronogramaIds.ToList();
            if (ids.Count == 0) return;

            var existentes = await _db.AplicacionesHierro
                .Where(a => ids.Contains(a.CronogramaId))
                .ToListAsync();

            if (existentes.Count > 0)
            {
                _db.AplicacionesHierro.RemoveRange(existentes);
                await _db.SaveChangesAsync();
            }
        }
    }
}
