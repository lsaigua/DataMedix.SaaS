using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataMedix.Infrastructure.Repositories
{
    public class PacienteRepository : IPacienteRepository
    {
        private readonly DataMedixDbContext _db;
        public PacienteRepository(DataMedixDbContext db) => _db = db;

        public async Task<Paciente?> GetByIdentificacionAsync(Guid tenantId, string identificacion) =>
            await _db.Pacientes.FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.Identificacion == identificacion &&
                p.Activo);

        public async Task<Paciente?> GetByIdAsync(Guid tenantId, Guid pacienteId) =>
            await _db.Pacientes
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == pacienteId && p.Activo);

        public async Task<List<Paciente>> GetAllAsync(Guid tenantId, string? busqueda = null,
            int pagina = 1, int tamano = 50)
        {
            var q = _db.Pacientes.Where(p => p.TenantId == tenantId && p.Activo);

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var b = busqueda.Trim().ToUpper();
                q = q.Where(p =>
                    p.Identificacion.Contains(b) ||
                    p.PrimerNombre.ToUpper().Contains(b) ||
                    p.PrimerApellido.ToUpper().Contains(b));
            }

            return await q
                .OrderBy(p => p.PrimerApellido).ThenBy(p => p.PrimerNombre)
                .Skip((pagina - 1) * tamano).Take(tamano)
                .ToListAsync();
        }

        public async Task<int> CountAsync(Guid tenantId, string? busqueda = null)
        {
            var q = _db.Pacientes.Where(p => p.TenantId == tenantId && p.Activo);
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var b = busqueda.Trim().ToUpper();
                q = q.Where(p =>
                    p.Identificacion.Contains(b) ||
                    p.PrimerNombre.ToUpper().Contains(b) ||
                    p.PrimerApellido.ToUpper().Contains(b));
            }
            return await q.CountAsync();
        }

        public async Task AddAsync(Paciente paciente)
        {
            await _db.Pacientes.AddAsync(paciente);
        }

        public async Task UpdateAsync(Paciente paciente)
        {
            _db.Pacientes.Update(paciente);
            await Task.CompletedTask;
        }
    }
}
