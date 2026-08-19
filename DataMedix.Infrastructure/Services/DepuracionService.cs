using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataMedix.Infrastructure.Services
{
    /// <summary>
    /// Depuración de datos clínicos de un período. El borrado es FÍSICO.
    ///
    /// Antes de purgar se cierra la facturación del período: el libro de
    /// facturación conserva su propia copia de cédula y nombre de cada paciente
    /// facturado, de modo que el cobro sigue siendo justificable aunque los datos
    /// clínicos desaparezcan.
    ///
    /// Los pacientes NO se borran físicamente: se dan de baja (Activo = false)
    /// solo si quedaron sin ningún dato en ningún período. El padrón es la
    /// referencia del historial multi-mes y del cobro.
    ///
    /// Todas las consultas filtran por tenant explícitamente y además pasan por
    /// los global query filters del DbContext, que ExecuteDelete también respeta.
    /// </summary>
    public class DepuracionService : IDepuracionService
    {
        private readonly DataMedixDbContext _db;
        private readonly IAuditoriaRepository _auditoria;
        private readonly IFacturacionService _facturacion;

        public DepuracionService(
            DataMedixDbContext db,
            IAuditoriaRepository auditoria,
            IFacturacionService facturacion)
        {
            _db = db;
            _auditoria = auditoria;
            _facturacion = facturacion;
        }

        public async Task<DepuracionConteoDto> ContarDatosPeriodoAsync(Guid tenantId, int año, int mes)
        {
            var period = new DateTime(año, mes, 1, 0, 0, 0, DateTimeKind.Utc);

            // Ids de lotes del período (para filtrar FK)
            var loteIds = await _db.LotesImportacion
                .Where(l => l.TenantId == tenantId && l.PeriodoAnio == año && l.PeriodoMes == mes)
                .Select(l => l.Id)
                .ToListAsync();

            // Ids de snapshots del período (para filtrar SnapshotDetalle)
            var snapIds = await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && s.PeriodoAnio == año && s.PeriodoMes == mes)
                .Select(s => s.Id)
                .ToListAsync();

            // Ids de cronogramas del período (para filtrar días, hierro y eventos)
            var cronoIds = await _db.CronogramasMedicamento
                .Where(c => c.TenantId == tenantId && c.PeriodoAnio == año && c.PeriodoMes == mes)
                .Select(c => c.Id)
                .ToListAsync();

            var facturacion = await _facturacion.ObtenerPeriodoAsync(tenantId, año, mes);

            return new DepuracionConteoDto
            {
                Año = año,
                Mes = mes,
                ResultadosLaboratorio = await _db.ResultadosLaboratorio
                    .CountAsync(r => r.TenantId == tenantId && r.PeriodoAnio == año && r.PeriodoMes == mes),
                Snapshots = snapIds.Count,
                SnapshotDetalles = snapIds.Count == 0 ? 0 :
                    await _db.SnapshotsMensualesDetalle
                        .CountAsync(d => snapIds.Contains(d.SnapshotId)),
                PrescripcionesSugeridas = await _db.PrescripcionesSugeridas
                    .CountAsync(p => p.TenantId == tenantId && p.PeriodDate == period),
                PrescripcionesFinales = await _db.PrescripcionesFinales
                    .CountAsync(p => p.TenantId == tenantId && p.PeriodDate == period),
                LotesImportacion = loteIds.Count,
                DetallesImportacion = loteIds.Count == 0 ? 0 :
                    await _db.ImportacionDetalles
                        .CountAsync(d => loteIds.Contains(d.LoteId)),
                ErroresImportacion = loteIds.Count == 0 ? 0 :
                    await _db.ImportacionErrores
                        .CountAsync(e => loteIds.Contains(e.LoteId)),

                Cronogramas = cronoIds.Count,
                CronogramaDias = cronoIds.Count == 0 ? 0 :
                    await _db.CronogramasDia.CountAsync(d => cronoIds.Contains(d.CronogramaId)),
                AplicacionesHierro = cronoIds.Count == 0 ? 0 :
                    await _db.AplicacionesHierro.CountAsync(a => cronoIds.Contains(a.CronogramaId)),
                EventosDosisPendiente = cronoIds.Count == 0 ? 0 :
                    await _db.EventosDosisPendiente.CountAsync(e => cronoIds.Contains(e.CronogramaId)),
                CronogramaAuditorias = cronoIds.Count == 0 ? 0 :
                    await _db.CronogramasAuditoria.CountAsync(a => cronoIds.Contains(a.CronogramaId)),

                PacientesADarDeBaja = await ContarPacientesQueQuedanSinDatosAsync(tenantId, año, mes),

                FacturacionCerrada   = facturacion.EstaCerrado,
                PacientesFacturables = facturacion.PacientesFacturados
            };
        }

        public async Task<int> EliminarPeriodoAsync(Guid tenantId, int año, int mes, Guid usuarioId)
        {
            var period = new DateTime(año, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            int total = 0;

            // ── 0. Cerrar facturación ANTES de borrar ─────────────────────────
            // Es lo único que preserva la base de cobro del período. Idempotente:
            // si ya estaba cerrado, no se altera la factura emitida.
            var factura = await _facturacion.CerrarPeriodoAsync(tenantId, año, mes, usuarioId);

            // Pacientes con actividad ANTES del borrado, para saber después
            // cuáles quedaron sin ningún dato.
            var pacientesTocados = await PacientesConActividadEnPeriodoAsync(tenantId, año, mes);

            // ── 1. PrescripcionFinal ──────────────────────────────────────────
            total += await _db.PrescripcionesFinales
                .Where(p => p.TenantId == tenantId && p.PeriodDate == period)
                .ExecuteDeleteAsync();

            // ── 2. PrescripcionSugerida ───────────────────────────────────────
            total += await _db.PrescripcionesSugeridas
                .Where(p => p.TenantId == tenantId && p.PeriodDate == period)
                .ExecuteDeleteAsync();

            // ── 3. SnapshotMensualDetalle (FK → SnapshotMensual) ──────────────
            var snapIds = await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && s.PeriodoAnio == año && s.PeriodoMes == mes)
                .Select(s => s.Id)
                .ToListAsync();

            if (snapIds.Count > 0)
            {
                total += await _db.SnapshotsMensualesDetalle
                    .Where(d => snapIds.Contains(d.SnapshotId))
                    .ExecuteDeleteAsync();
            }

            // ── 4. SnapshotMensual ────────────────────────────────────────────
            total += await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && s.PeriodoAnio == año && s.PeriodoMes == mes)
                .ExecuteDeleteAsync();

            // ── 5. ResultadoLaboratorio ───────────────────────────────────────
            total += await _db.ResultadosLaboratorio
                .Where(r => r.TenantId == tenantId && r.PeriodoAnio == año && r.PeriodoMes == mes)
                .ExecuteDeleteAsync();

            // ── 6. ImportacionError + ImportacionDetalle + LoteImportacion ────
            var loteIds = await _db.LotesImportacion
                .Where(l => l.TenantId == tenantId && l.PeriodoAnio == año && l.PeriodoMes == mes)
                .Select(l => l.Id)
                .ToListAsync();

            if (loteIds.Count > 0)
            {
                total += await _db.ImportacionErrores
                    .Where(e => loteIds.Contains(e.LoteId))
                    .ExecuteDeleteAsync();

                total += await _db.ImportacionDetalles
                    .Where(d => loteIds.Contains(d.LoteId))
                    .ExecuteDeleteAsync();
            }

            total += await _db.LotesImportacion
                .Where(l => l.TenantId == tenantId && l.PeriodoAnio == año && l.PeriodoMes == mes)
                .ExecuteDeleteAsync();

            // ── 7. Cronograma de medicación del período ───────────────────────
            var cronoIds = await _db.CronogramasMedicamento
                .Where(c => c.TenantId == tenantId && c.PeriodoAnio == año && c.PeriodoMes == mes)
                .Select(c => c.Id)
                .ToListAsync();

            if (cronoIds.Count > 0)
            {
                total += await _db.CronogramasAuditoria
                    .Where(a => cronoIds.Contains(a.CronogramaId))
                    .ExecuteDeleteAsync();

                total += await _db.EventosDosisPendiente
                    .Where(e => cronoIds.Contains(e.CronogramaId))
                    .ExecuteDeleteAsync();

                total += await _db.AplicacionesHierro
                    .Where(a => cronoIds.Contains(a.CronogramaId))
                    .ExecuteDeleteAsync();

                total += await _db.CronogramasDia
                    .Where(d => cronoIds.Contains(d.CronogramaId))
                    .ExecuteDeleteAsync();

                total += await _db.CronogramasMedicamento
                    .Where(c => c.TenantId == tenantId && c.PeriodoAnio == año && c.PeriodoMes == mes)
                    .ExecuteDeleteAsync();
            }

            // ── 8. Pacientes que quedaron sin datos: baja lógica ──────────────
            // Nunca borrado físico. La fila sigue respaldando el historial de
            // cobro y el detalle de facturación de períodos anteriores.
            var pacientesDeBaja = await DarDeBajaPacientesSinDatosAsync(tenantId, pacientesTocados);

            // ── Auditoría ─────────────────────────────────────────────────────
            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenantId,
                UsuarioId   = usuarioId,
                Accion      = "DELETE",
                Entidad     = "Periodo",
                Descripcion = $"Depuración {año}-{mes:D2}: {total} registros eliminados, " +
                              $"{pacientesDeBaja} paciente(s) dados de baja. " +
                              $"Facturación cerrada con {factura.PacientesFacturados} paciente(s), " +
                              $"total {factura.Total:N2}",
                CreatedAt   = DateTime.UtcNow
            });

            return total;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Pacientes
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Pacientes con cualquier dato clínico en el período indicado.</summary>
        private async Task<List<Guid>> PacientesConActividadEnPeriodoAsync(Guid tenantId, int año, int mes)
        {
            var period = new DateTime(año, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            var ids = new HashSet<Guid>();

            ids.UnionWith(await _db.ResultadosLaboratorio
                .Where(r => r.TenantId == tenantId && r.PeriodoAnio == año && r.PeriodoMes == mes)
                .Select(r => r.PacienteId).Distinct().ToListAsync());

            ids.UnionWith(await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && s.PeriodoAnio == año && s.PeriodoMes == mes)
                .Select(s => s.PacienteId).Distinct().ToListAsync());

            ids.UnionWith(await _db.PrescripcionesSugeridas
                .Where(p => p.TenantId == tenantId && p.PeriodDate == period)
                .Select(p => p.PacienteId).Distinct().ToListAsync());

            ids.UnionWith(await _db.PrescripcionesFinales
                .Where(p => p.TenantId == tenantId && p.PeriodDate == period)
                .Select(p => p.PacienteId).Distinct().ToListAsync());

            ids.UnionWith(await _db.CronogramasMedicamento
                .Where(c => c.TenantId == tenantId && c.PeriodoAnio == año && c.PeriodoMes == mes)
                .Select(c => c.PacienteId).Distinct().ToListAsync());

            return ids.ToList();
        }

        /// <summary>
        /// De los pacientes indicados, cuáles no tienen ningún dato clínico en
        /// ningún período. Se evalúa contra el estado actual de la base.
        /// </summary>
        private async Task<List<Guid>> PacientesSinDatosAsync(Guid tenantId, List<Guid> candidatos)
        {
            if (candidatos.Count == 0) return new List<Guid>();

            var conDatos = new HashSet<Guid>();

            conDatos.UnionWith(await _db.ResultadosLaboratorio
                .Where(r => r.TenantId == tenantId && candidatos.Contains(r.PacienteId))
                .Select(r => r.PacienteId).Distinct().ToListAsync());

            conDatos.UnionWith(await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && candidatos.Contains(s.PacienteId))
                .Select(s => s.PacienteId).Distinct().ToListAsync());

            conDatos.UnionWith(await _db.PrescripcionesSugeridas
                .Where(p => p.TenantId == tenantId && candidatos.Contains(p.PacienteId))
                .Select(p => p.PacienteId).Distinct().ToListAsync());

            conDatos.UnionWith(await _db.PrescripcionesFinales
                .Where(p => p.TenantId == tenantId && candidatos.Contains(p.PacienteId))
                .Select(p => p.PacienteId).Distinct().ToListAsync());

            conDatos.UnionWith(await _db.CronogramasMedicamento
                .Where(c => c.TenantId == tenantId && candidatos.Contains(c.PacienteId))
                .Select(c => c.PacienteId).Distinct().ToListAsync());

            return candidatos.Where(id => !conDatos.Contains(id)).ToList();
        }

        /// <summary>
        /// Vista previa: cuántos pacientes quedarían sin datos si se purgara el
        /// período. Simula el estado posterior sin tocar nada.
        /// </summary>
        private async Task<int> ContarPacientesQueQuedanSinDatosAsync(Guid tenantId, int año, int mes)
        {
            var period = new DateTime(año, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            var candidatos = await PacientesConActividadEnPeriodoAsync(tenantId, año, mes);
            if (candidatos.Count == 0) return 0;

            // Igual que PacientesSinDatosAsync pero excluyendo el período que se
            // va a borrar, porque esos datos ya no existirán.
            var conDatosEnOtroPeriodo = new HashSet<Guid>();

            conDatosEnOtroPeriodo.UnionWith(await _db.ResultadosLaboratorio
                .Where(r => r.TenantId == tenantId && candidatos.Contains(r.PacienteId) &&
                            !(r.PeriodoAnio == año && r.PeriodoMes == mes))
                .Select(r => r.PacienteId).Distinct().ToListAsync());

            conDatosEnOtroPeriodo.UnionWith(await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && candidatos.Contains(s.PacienteId) &&
                            !(s.PeriodoAnio == año && s.PeriodoMes == mes))
                .Select(s => s.PacienteId).Distinct().ToListAsync());

            conDatosEnOtroPeriodo.UnionWith(await _db.PrescripcionesSugeridas
                .Where(p => p.TenantId == tenantId && candidatos.Contains(p.PacienteId) &&
                            p.PeriodDate != period)
                .Select(p => p.PacienteId).Distinct().ToListAsync());

            conDatosEnOtroPeriodo.UnionWith(await _db.PrescripcionesFinales
                .Where(p => p.TenantId == tenantId && candidatos.Contains(p.PacienteId) &&
                            p.PeriodDate != period)
                .Select(p => p.PacienteId).Distinct().ToListAsync());

            conDatosEnOtroPeriodo.UnionWith(await _db.CronogramasMedicamento
                .Where(c => c.TenantId == tenantId && candidatos.Contains(c.PacienteId) &&
                            !(c.PeriodoAnio == año && c.PeriodoMes == mes))
                .Select(c => c.PacienteId).Distinct().ToListAsync());

            // Solo cuentan los que hoy están activos: los ya dados de baja no cambian
            var yaInactivos = await _db.Pacientes
                .Where(p => p.TenantId == tenantId && candidatos.Contains(p.Id) && !p.Activo)
                .Select(p => p.Id)
                .ToListAsync();

            var inactivos = yaInactivos.ToHashSet();

            return candidatos.Count(id => !conDatosEnOtroPeriodo.Contains(id) && !inactivos.Contains(id));
        }

        /// <summary>Baja lógica de los pacientes que quedaron sin ningún dato.</summary>
        private async Task<int> DarDeBajaPacientesSinDatosAsync(Guid tenantId, List<Guid> candidatos)
        {
            var huerfanos = await PacientesSinDatosAsync(tenantId, candidatos);
            if (huerfanos.Count == 0) return 0;

            return await _db.Pacientes
                .Where(p => p.TenantId == tenantId && huerfanos.Contains(p.Id) && p.Activo)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Activo, false)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));
        }
    }
}
