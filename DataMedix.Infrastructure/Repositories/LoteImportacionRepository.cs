using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace DataMedix.Infrastructure.Repositories
{
    public class LoteImportacionRepository : ILoteImportacionRepository
    {
        private readonly DataMedixDbContext _db;
        public LoteImportacionRepository(DataMedixDbContext db) => _db = db;

        public async Task<LoteImportacion?> GetByIdAsync(Guid tenantId, Guid loteId) =>
            await _db.LotesImportacion
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == loteId);

        public async Task<List<LoteImportacion>> GetByTenantAsync(Guid tenantId, int pagina = 1, int tamano = 20) =>
            await _db.LotesImportacion
                .Where(l => l.TenantId == tenantId && l.Activo)
                .OrderByDescending(l => l.CreatedAt)
                .Skip((pagina - 1) * tamano).Take(tamano)
                .ToListAsync();

        public async Task AddAsync(LoteImportacion lote) =>
            await _db.LotesImportacion.AddAsync(lote);

        public async Task UpdateAsync(LoteImportacion lote)
        {
            _db.LotesImportacion.Update(lote);
            await Task.CompletedTask;
        }

        public async Task AddDetallesAsync(List<ImportacionDetalle> detalles)
        {
            if (!detalles.Any()) return;
            await _db.BulkInsertAsync(detalles, new BulkConfig { SetOutputIdentity = false });
        }

        public async Task AddErroresAsync(List<ImportacionError> errores)
        {
            if (!errores.Any()) return;
            await _db.BulkInsertAsync(errores, new BulkConfig { SetOutputIdentity = false });
        }

        public async Task<List<ImportacionError>> GetErroresByLoteAsync(Guid loteId) =>
            await _db.ImportacionErrores
                .Where(e => e.LoteId == loteId)
                .OrderBy(e => e.NumeroFila)
                .ToListAsync();

        public async Task IgnorarErrorAsync(Guid errorId)
        {
            var err = await _db.ImportacionErrores.FindAsync(errorId);
            if (err == null) return;
            err.EsIgnorado = true;
            await _db.SaveChangesAsync();
        }

        public async Task CancelarLoteAsync(LoteImportacion lote)
        {
            lote.Cancelar();
            _db.LotesImportacion.Update(lote);
            await _db.SaveChangesAsync();
        }
    }

    public class ParametroClinicoRepository : IParametroClinicoRepository
    {
        private readonly DataMedixDbContext _db;
        public ParametroClinicoRepository(DataMedixDbContext db) => _db = db;

        public async Task<List<ParametroClinico>> GetAllAsync(bool soloActivos = true) =>
            await _db.ParametrosClinicos
                .Where(p => !soloActivos || p.Activo)
                .OrderBy(p => p.OrdenVisualizacion)
                .ToListAsync();

        public async Task<ParametroClinico?> ResolverPorAliasAsync(string alias, Guid? tenantId = null)
        {
            var aliasNorm = alias.Trim().ToUpperInvariant();
            var match = await _db.AliasParametros
                .Include(a => a.ParametroClinico)
                .Where(a => a.Activo && a.Alias.ToUpper() == aliasNorm &&
                            (a.TenantId == null || a.TenantId == tenantId))
                .OrderBy(a => a.TenantId == null ? 1 : 0) // Preferir alias específico del tenant
                .FirstOrDefaultAsync();

            return match?.ParametroClinico;
        }

        public async Task<Dictionary<string, ParametroClinico>> GetMapaAliasesAsync(Guid? tenantId = null)
        {
            var aliases = await _db.AliasParametros
                .Include(a => a.ParametroClinico)
                .Where(a => a.Activo && (a.TenantId == null || a.TenantId == tenantId))
                .ToListAsync();

            var mapa = new Dictionary<string, ParametroClinico>(StringComparer.OrdinalIgnoreCase);
            // Primero añadir globales, luego los del tenant (sobrescriben)
            foreach (var a in aliases.OrderBy(x => x.TenantId == null ? 0 : 1))
                mapa[a.Alias.ToUpperInvariant()] = a.ParametroClinico;

            return mapa;
        }
    }

    public class ResultadoLaboratorioRepository : IResultadoLaboratorioRepository
    {
        private readonly DataMedixDbContext _db;
        public ResultadoLaboratorioRepository(DataMedixDbContext db) => _db = db;

        public async Task BulkInsertAsync(List<ResultadoLaboratorio> resultados)
        {
            if (!resultados.Any()) return;
            await _db.BulkInsertAsync(resultados, new BulkConfig { SetOutputIdentity = false });
        }

        public async Task<List<ResultadoLaboratorio>> GetByPacienteYPeriodoAsync(
            Guid tenantId, Guid pacienteId, DateTime periodDate) =>
            await _db.ResultadosLaboratorio
                .Include(r => r.ParametroClinico)
                .Where(r => r.TenantId == tenantId &&
                            r.PacienteId == pacienteId &&
                            r.PeriodDate == periodDate &&
                            r.Activo)
                .ToListAsync();

        public async Task<List<ResultadoLaboratorio>> GetByLoteAsync(Guid loteId) =>
            await _db.ResultadosLaboratorio
                .Include(r => r.ParametroClinico)
                .Where(r => r.LoteId == loteId && r.Activo)
                .ToListAsync();

        public async Task<(List<ResultadoLaboratorio> Items, int Total)> GetPagedAsync(
            Guid tenantId, ResultadoFiltro filtro)
        {
            var q = BuildResultadoQuery(tenantId, filtro);
            var total = await q.CountAsync();
            var items = await q
                .OrderByDescending(r => r.PeriodDate)
                .ThenBy(r => r.Paciente.PrimerApellido)
                .ThenBy(r => r.ParametroClinico != null ? r.ParametroClinico.OrdenVisualizacion : 99)
                .Skip((filtro.Pagina - 1) * filtro.Tamano)
                .Take(filtro.Tamano)
                .ToListAsync();
            return (items, total);
        }

        public async Task<List<ResultadoLaboratorio>> GetForExportAsync(
            Guid tenantId, ResultadoFiltro filtro) =>
            await BuildResultadoQuery(tenantId, filtro)
                .OrderByDescending(r => r.PeriodDate)
                .ThenBy(r => r.Paciente.PrimerApellido)
                .ThenBy(r => r.ParametroClinico != null ? r.ParametroClinico.OrdenVisualizacion : 99)
                .Take(10_000)
                .ToListAsync();

        private IQueryable<ResultadoLaboratorio> BuildResultadoQuery(Guid tenantId, ResultadoFiltro filtro)
        {
            var q = _db.ResultadosLaboratorio
                .Include(r => r.Paciente)
                .Include(r => r.ParametroClinico)
                .Include(r => r.Lote)
                .Where(r => r.TenantId == tenantId && r.Activo);

            if (!string.IsNullOrWhiteSpace(filtro.BusquedaPaciente))
            {
                var b = filtro.BusquedaPaciente.Trim().ToUpper();
                q = q.Where(r => r.Paciente.Identificacion.Contains(b) ||
                                 r.Paciente.PrimerNombre.ToUpper().Contains(b) ||
                                 r.Paciente.PrimerApellido.ToUpper().Contains(b));
            }

            if (filtro.ParametroClinicoId.HasValue)
                q = q.Where(r => r.ParametroClinicoId == filtro.ParametroClinicoId);

            if (filtro.FechaDesde.HasValue)
                q = q.Where(r => r.PeriodDate >= filtro.FechaDesde.Value);

            if (filtro.FechaHasta.HasValue)
                q = q.Where(r => r.PeriodDate <= filtro.FechaHasta.Value);

            if (filtro.LoteId.HasValue)
                q = q.Where(r => r.LoteId == filtro.LoteId.Value);

            return q;
        }
    }

    public class SnapshotMensualRepository : ISnapshotMensualRepository
    {
        private readonly DataMedixDbContext _db;
        public SnapshotMensualRepository(DataMedixDbContext db) => _db = db;

        public async Task<SnapshotMensual?> GetByPacienteYPeriodoAsync(
            Guid tenantId, Guid pacienteId, DateTime periodDate) =>
            await _db.SnapshotsMensuales
                .Include(s => s.Detalles)
                .FirstOrDefaultAsync(s =>
                    s.TenantId == tenantId &&
                    s.PacienteId == pacienteId &&
                    s.PeriodDate == periodDate &&
                    s.Activo);

        public async Task<SnapshotMensual?> GetUltimoVigenteAsync(
            Guid tenantId, Guid pacienteId, DateTime hasta) =>
            await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId &&
                            s.PacienteId == pacienteId &&
                            s.PeriodDate <= hasta &&
                            s.Activo)
                .OrderByDescending(s => s.PeriodDate)
                .FirstOrDefaultAsync();

        public async Task<List<SnapshotMensual>> GetHistorialAsync(
            Guid tenantId, Guid pacienteId, int meses = 12) =>
            await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && s.PacienteId == pacienteId && s.Activo)
                .OrderByDescending(s => s.PeriodDate)
                .Take(meses)
                .ToListAsync();

        public async Task<List<SnapshotMensual>> GetByPeriodoAsync(Guid tenantId, DateTime periodDate,
            string? busqueda = null, int pagina = 1, int tamano = 50, string? planSalud = null)
        {
            var q = _db.SnapshotsMensuales
                .Include(s => s.Paciente)
                .Where(s => s.TenantId == tenantId && s.PeriodDate == periodDate && s.Activo);

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var b = busqueda.Trim().ToUpper();
                q = q.Where(s =>
                    s.Paciente.Identificacion.Contains(b) ||
                    s.Paciente.PrimerNombre.ToUpper().Contains(b) ||
                    s.Paciente.PrimerApellido.ToUpper().Contains(b));
            }

            if (!string.IsNullOrWhiteSpace(planSalud))
                q = q.Where(s => s.PlanSalud == planSalud);

            return await q
                .OrderBy(s => s.Paciente.PrimerApellido)
                .Skip((pagina - 1) * tamano).Take(tamano)
                .ToListAsync();
        }

        public async Task<List<string>> GetPlanesSaludAsync(Guid tenantId, DateTime periodDate) =>
            await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && s.PeriodDate == periodDate &&
                            s.Activo && s.PlanSalud != null)
                .Select(s => s.PlanSalud!)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

        public async Task UpsertAsync(SnapshotMensual snapshot)
        {
            var existente = await _db.SnapshotsMensuales.FindAsync(snapshot.Id);
            if (existente == null)
                await _db.SnapshotsMensuales.AddAsync(snapshot);
            else
                _db.SnapshotsMensuales.Update(snapshot);
        }

        public async Task AddDetallesAsync(List<SnapshotMensualDetalle> detalles)
        {
            if (!detalles.Any()) return;
            // Eliminar detalles previos del snapshot
            var snapshotIds = detalles.Select(d => d.SnapshotId).Distinct().ToList();
            var previos = _db.SnapshotsMensualesDetalle
                .Where(d => snapshotIds.Contains(d.SnapshotId));
            _db.SnapshotsMensualesDetalle.RemoveRange(previos);
            await _db.SnapshotsMensualesDetalle.AddRangeAsync(detalles);
        }
    }

    public class RangoPreescribaRepository : IRangoPreescribaRepository
    {
        private readonly DataMedixDbContext _db;
        public RangoPreescribaRepository(DataMedixDbContext db) => _db = db;

        public async Task<List<RangoPrescriba>> GetByParametroAsync(Guid parametroId, Guid? tenantId = null) =>
            await _db.RangosPrescriba
                .Where(r => r.Activo &&
                            r.ParametroClinicoId == parametroId &&
                            (r.TenantId == null || r.TenantId == tenantId))
                .OrderBy(r => r.Orden)
                .ToListAsync();

        public async Task<RangoPrescriba?> BuscarRangoAplicableAsync(
            Guid parametroId, decimal valor, Guid? tenantId = null)
        {
            var rangos = await GetByParametroAsync(parametroId, tenantId);
            return rangos.OrderBy(r => r.Orden).FirstOrDefault(r => r.AplicaParaValor(valor));
        }

        public async Task<List<RangoPrescriba>> GetAllAsync(Guid? tenantId = null) =>
            await _db.RangosPrescriba
                .Include(r => r.ParametroClinico)
                .Where(r => r.Activo && (r.TenantId == null || r.TenantId == tenantId))
                .OrderBy(r => r.ParametroClinico.OrdenVisualizacion).ThenBy(r => r.Orden)
                .ToListAsync();

        public async Task UpsertAsync(RangoPrescriba rango)
        {
            if (rango.Id == Guid.Empty)
            {
                rango.Id = Guid.NewGuid();
                rango.CreatedAt = DateTime.UtcNow;
                await _db.RangosPrescriba.AddAsync(rango);
            }
            else
            {
                rango.UpdatedAt = DateTime.UtcNow;
                _db.RangosPrescriba.Update(rango);
            }
        }
    }

    public class PrescripcionRepository : IPrescripcionRepository
    {
        private readonly DataMedixDbContext _db;
        public PrescripcionRepository(DataMedixDbContext db) => _db = db;

        public async Task<PrescripcionSugerida?> GetSugeridaByPacienteYPeriodoAsync(
            Guid tenantId, Guid pacienteId, DateTime periodDate) =>
            await _db.PrescripcionesSugeridas
                .FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId &&
                    p.PacienteId == pacienteId &&
                    p.PeriodDate == periodDate &&
                    p.Activo);

        public async Task UpsertSugeridaAsync(PrescripcionSugerida prescripcion)
        {
            var existente = await _db.PrescripcionesSugeridas.FindAsync(prescripcion.Id);
            if (existente == null)
                await _db.PrescripcionesSugeridas.AddAsync(prescripcion);
            else
                _db.PrescripcionesSugeridas.Update(prescripcion);
            await _db.SaveChangesAsync();
        }

        public async Task<PrescripcionFinal?> GetFinalByPacienteYPeriodoAsync(
            Guid tenantId, Guid pacienteId, DateTime periodDate) =>
            await _db.PrescripcionesFinales
                .Include(p => p.Medico)
                .FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId &&
                    p.PacienteId == pacienteId &&
                    p.PeriodDate == periodDate &&
                    p.Activo);

        public async Task AddFinalAsync(PrescripcionFinal prescripcion) =>
            await _db.PrescripcionesFinales.AddAsync(prescripcion);

        public async Task UpdateFinalAsync(PrescripcionFinal prescripcion)
        {
            _db.PrescripcionesFinales.Update(prescripcion);
            await Task.CompletedTask;
        }

        public async Task<List<PrescripcionSugerida>> GetPendientesAsync(Guid tenantId, DateTime periodDate) =>
            await _db.PrescripcionesSugeridas
                .Include(p => p.Paciente)
                .Where(p => p.TenantId == tenantId &&
                            p.PeriodDate == periodDate &&
                            p.Estado == EstadoPrescripcion.Pendiente &&
                            p.Activo)
                .OrderBy(p => p.Paciente.PrimerApellido)
                .ToListAsync();

        public async Task<List<PrescripcionSugerida>> GetByPeriodoAsync(
            Guid tenantId, DateTime periodDate, string? busqueda = null)
        {
            var q = _db.PrescripcionesSugeridas
                .Include(p => p.Paciente)
                .Where(p => p.TenantId == tenantId && p.PeriodDate == periodDate && p.Activo);

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var b = busqueda.Trim().ToUpper();
                q = q.Where(p =>
                    p.Paciente.Identificacion.Contains(b) ||
                    p.Paciente.PrimerNombre.ToUpper().Contains(b) ||
                    p.Paciente.PrimerApellido.ToUpper().Contains(b));
            }

            return await q
                .OrderBy(p => p.Paciente.PrimerApellido)
                .ThenBy(p => p.Paciente.PrimerNombre)
                .ToListAsync();
        }
    }

    public class AuditoriaRepository : IAuditoriaRepository
    {
        private readonly DataMedixDbContext _db;
        public AuditoriaRepository(DataMedixDbContext db) => _db = db;

        public async Task RegistrarAsync(AuditoriaLog log) =>
            await _db.AuditoriaLogs.AddAsync(log);
    }
}
