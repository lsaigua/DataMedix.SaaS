using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataMedix.Infrastructure.Repositories
{
    public class CronogramaRepository : ICronogramaRepository
    {
        private readonly DataMedixDbContext _db;

        public CronogramaRepository(DataMedixDbContext db) => _db = db;

        public async Task<CronogramaMedicamento?> GetByPacienteYPeriodoAsync(
            Guid tenantId, Guid pacienteId, int anio, int mes)
        {
            return await _db.CronogramasMedicamento
                .Include(c => c.Dias)
                .FirstOrDefaultAsync(c =>
                    c.TenantId == tenantId &&
                    c.PacienteId == pacienteId &&
                    c.PeriodoAnio == anio &&
                    c.PeriodoMes == mes &&
                    c.Activo);
        }

        public async Task<List<CronogramaMedicamento>> GetByPeriodoAsync(
            Guid tenantId, int anio, int mes)
        {
            return await _db.CronogramasMedicamento
                .Include(c => c.Dias)
                .Include(c => c.Paciente)
                .Where(c =>
                    c.TenantId == tenantId &&
                    c.PeriodoAnio == anio &&
                    c.PeriodoMes == mes &&
                    c.Activo)
                .OrderBy(c => c.Paciente.PrimerApellido)
                .ThenBy(c => c.Paciente.PrimerNombre)
                .ToListAsync();
        }

        public async Task<CronogramaMedicamento?> GetConDiasAsync(Guid tenantId, Guid cronogramaId)
        {
            return await _db.CronogramasMedicamento
                .Include(c => c.Dias.OrderBy(d => d.FechaSesion))
                .Include(c => c.Paciente)
                .FirstOrDefaultAsync(c => c.Id == cronogramaId && c.TenantId == tenantId);
        }

        public async Task<CronogramaMedicamento> UpsertAsync(CronogramaMedicamento cronograma)
        {
            var existing = await _db.CronogramasMedicamento
                .FirstOrDefaultAsync(c =>
                    c.TenantId == cronograma.TenantId &&
                    c.PacienteId == cronograma.PacienteId &&
                    c.PeriodoAnio == cronograma.PeriodoAnio &&
                    c.PeriodoMes == cronograma.PeriodoMes);

            if (existing == null)
            {
                _db.CronogramasMedicamento.Add(cronograma);
                await _db.SaveChangesAsync();
                return cronograma;
            }

            existing.PlanSalud = cronograma.PlanSalud;
            existing.EpoUiSemana = cronograma.EpoUiSemana;
            existing.HierroMgMes = cronograma.HierroMgMes;
            existing.Observaciones = cronograma.Observaciones;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = cronograma.UpdatedBy;

            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task<CronogramaDia> UpsertDiaAsync(CronogramaDia dia)
        {
            var existing = await _db.CronogramasDia
                .FirstOrDefaultAsync(d => d.Id == dia.Id);

            if (existing == null)
            {
                existing = await _db.CronogramasDia
                    .FirstOrDefaultAsync(d =>
                        d.CronogramaId == dia.CronogramaId &&
                        d.FechaSesion == dia.FechaSesion);
            }

            if (existing == null)
            {
                _db.CronogramasDia.Add(dia);
                await _db.SaveChangesAsync();
                return dia;
            }

            // Solo actualizar si fue editado manualmente o no tiene dato
            if (dia.EpoEditadoManual || !existing.EpoEditadoManual)
            {
                existing.EpoDosisuI = dia.EpoDosisuI;
                existing.EpoEditadoManual = dia.EpoEditadoManual;
            }
            if (dia.HierroEditadoManual || !existing.HierroEditadoManual)
            {
                existing.HierroDosismg = dia.HierroDosismg;
                existing.HierroEditadoManual = dia.HierroEditadoManual;
            }
            existing.Observacion = dia.Observacion ?? existing.Observacion;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = dia.UpdatedBy;

            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task<List<CronogramaDia>> UpsertDiasAsync(List<CronogramaDia> dias)
        {
            var resultado = new List<CronogramaDia>();
            foreach (var dia in dias)
                resultado.Add(await UpsertDiaAsync(dia));
            return resultado;
        }

        public async Task RegistrarAuditoriaAsync(CronogramaAuditoria entrada)
        {
            _db.CronogramasAuditoria.Add(entrada);
            await _db.SaveChangesAsync();
        }

        public async Task UpsertBatchAsync(List<CronogramaMedicamento> nuevos)
        {
            if (nuevos.Count > 0)
                _db.CronogramasMedicamento.AddRange(nuevos);
            // Las entidades ya tracked (modificadas en memoria) son detectadas automáticamente por EF
            await _db.SaveChangesAsync();
        }

        public async Task BatchReplaceDiasAsync(List<Guid> cronogramaIds, List<CronogramaDia> nuevasDias)
        {
            if (cronogramaIds.Count == 0) return;

            // Eliminar TODAS las dias existentes de estos cronogramas
            var existentes = await _db.CronogramasDia
                .Where(d => cronogramaIds.Contains(d.CronogramaId))
                .ToListAsync();

            if (existentes.Count > 0)
                _db.CronogramasDia.RemoveRange(existentes);

            // Insertar todas las nuevas (los valores manuales ya vienen resueltos desde el servicio)
            if (nuevasDias.Count > 0)
                await _db.CronogramasDia.AddRangeAsync(nuevasDias);

            await _db.SaveChangesAsync();
        }
    }

    public class ConfiguracionMedicamentoRepository : IConfiguracionMedicamentoRepository
    {
        private readonly DataMedixDbContext _db;

        public ConfiguracionMedicamentoRepository(DataMedixDbContext db) => _db = db;

        public async Task<List<ConfiguracionMedicamento>> GetByTenantAsync(Guid tenantId)
        {
            return await _db.ConfiguracionesMedicamento
                .Where(c => c.TenantId == tenantId && c.Activo)
                .OrderBy(c => c.Medicamento)
                .ToListAsync();
        }

        public async Task<ConfiguracionMedicamento?> GetByMedicamentoAsync(Guid tenantId, string medicamento)
        {
            return await _db.ConfiguracionesMedicamento
                .FirstOrDefaultAsync(c =>
                    c.TenantId == tenantId &&
                    c.Medicamento == medicamento &&
                    c.Activo);
        }

        public async Task UpsertAsync(ConfiguracionMedicamento config)
        {
            var existing = await _db.ConfiguracionesMedicamento
                .FirstOrDefaultAsync(c =>
                    c.TenantId == config.TenantId &&
                    c.Medicamento == config.Medicamento);

            if (existing == null)
            {
                _db.ConfiguracionesMedicamento.Add(config);
            }
            else
            {
                existing.PrecioUnitario = config.PrecioUnitario;
                existing.Unidad = config.Unidad;
                existing.Observacion = config.Observacion;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
        }
    }
}
