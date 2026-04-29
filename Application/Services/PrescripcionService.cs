using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;

namespace DataMedix.Application.Services
{
    /// <summary>
    /// Motor clínico de prescripción sugerida para EPO y Hierro IV.
    /// Basado en los rangos configurados en rango_prescriba por parámetro clínico.
    /// Regla KDIGO: HB objetivo 10-12 g/dL. Ferritina objetivo 200-500 ng/mL.
    /// </summary>
    public class PrescripcionService
    {
        private readonly ISnapshotMensualRepository _snapshotRepo;
        private readonly IRangoPreescribaRepository _rangoRepo;
        private readonly IPrescripcionRepository _prescripcionRepo;
        private readonly IParametroClinicoRepository _parametroRepo;

        public PrescripcionService(
            ISnapshotMensualRepository snapshotRepo,
            IRangoPreescribaRepository rangoRepo,
            IPrescripcionRepository prescripcionRepo,
            IParametroClinicoRepository parametroRepo)
        {
            _snapshotRepo = snapshotRepo;
            _rangoRepo = rangoRepo;
            _prescripcionRepo = prescripcionRepo;
            _parametroRepo = parametroRepo;
        }

        /// <summary>
        /// Genera prescripción sugerida para todos los pacientes de un período.
        /// Se llama automáticamente después de procesar un lote de importación.
        /// </summary>
        public async Task GenerarParaPeriodoAsync(Guid tenantId, DateTime periodDate)
        {
            var snapshots = await _snapshotRepo.GetByPeriodoAsync(tenantId, periodDate);
            var parametros = await _parametroRepo.GetAllAsync();

            var hbParam = parametros.FirstOrDefault(p => p.Codigo == "HB");
            var ferrParam = parametros.FirstOrDefault(p => p.Codigo == "FERR");
            var isatParam = parametros.FirstOrDefault(p => p.Codigo == "ISAT");

            List<RangoPrescriba> rangosEpo = hbParam != null
                ? await _rangoRepo.GetByParametroAsync(hbParam.Id, tenantId)
                : new List<RangoPrescriba>();

            List<RangoPrescriba> rangosFerr = ferrParam != null
                ? await _rangoRepo.GetByParametroAsync(ferrParam.Id, tenantId)
                : new List<RangoPrescriba>();

            List<RangoPrescriba> rangosIsat = isatParam != null
                ? await _rangoRepo.GetByParametroAsync(isatParam.Id, tenantId)
                : new List<RangoPrescriba>();

            foreach (var snapshot in snapshots)
            {
                await GenerarParaSnapshotAsync(snapshot, rangosEpo, rangosFerr, rangosIsat, tenantId);
            }
        }

        /// <summary>
        /// Genera prescripción sugerida para un snapshot específico.
        /// </summary>
        public async Task GenerarParaSnapshotAsync(
            SnapshotMensual snapshot,
            List<RangoPrescriba> rangosEpo,
            List<RangoPrescriba> rangosFerr,
            List<RangoPrescriba> rangosIsat,
            Guid tenantId)
        {
            var prescripcion = await _prescripcionRepo.GetSugeridaByPacienteYPeriodoAsync(
                tenantId, snapshot.PacienteId, snapshot.PeriodDate)
                ?? new PrescripcionSugerida
                {
                    TenantId = tenantId,
                    PacienteId = snapshot.PacienteId,
                    SnapshotId = snapshot.Id,
                    PeriodDate = snapshot.PeriodDate,
                    Estado = EstadoPrescripcion.Pendiente
                };

            // Solo regenerar si está en estado PENDIENTE
            if (prescripcion.Estado != EstadoPrescripcion.Pendiente) return;

            // ── EPO: basado en HB ──────────────────────────────────────────
            if (snapshot.HbValor.HasValue)
            {
                var rangoEpo = rangosEpo
                    .OrderBy(r => r.Orden)
                    .FirstOrDefault(r => r.AplicaParaValor(snapshot.HbValor.Value));

                if (rangoEpo != null)
                {
                    prescripcion.EpoAccion = rangoEpo.Accion;
                    prescripcion.EpoDosisSugerida = rangoEpo.DosisSugerida;
                    prescripcion.EpoObservacion = rangoEpo.Observacion;
                    prescripcion.EpoRangoId = rangoEpo.Id;
                }
            }
            else
            {
                prescripcion.EpoObservacion = "No hay datos de Hemoglobina para este período.";
            }

            // ── HIERRO IV: basado en Ferritina + ISAT ─────────────────────
            var accionHierro = DeterminarAccionHierro(
                snapshot.FerritinaValor,
                snapshot.SaturacionValor,
                rangosFerr,
                rangosIsat,
                out var rangoHierroAplicado,
                out var obsHierro);

            prescripcion.HierroAccion = accionHierro;
            prescripcion.HierroRangoId = rangoHierroAplicado?.Id;
            prescripcion.HierroDosisSugerida = rangoHierroAplicado?.DosisSugerida;
            prescripcion.HierroObservacion = obsHierro;

            // ── Observaciones generales ────────────────────────────────────
            prescripcion.ObservacionesGenerales = GenerarObservacionesGenerales(snapshot);

            await _prescripcionRepo.UpsertSugeridaAsync(prescripcion);
        }

        // ─────────────────────────────────────────────────────────────────────
        // LÓGICA CLÍNICA DE HIERRO
        // ─────────────────────────────────────────────────────────────────────
        private static string? DeterminarAccionHierro(
            decimal? ferritina,
            decimal? isat,
            List<RangoPrescriba> rangosFerr,
            List<RangoPrescriba> rangosIsat,
            out RangoPrescriba? rangoAplicado,
            out string? observacion)
        {
            rangoAplicado = null;
            observacion = null;

            if (!ferritina.HasValue && !isat.HasValue)
            {
                observacion = "No hay datos de Ferritina ni ISAT para este período.";
                return null;
            }

            // Prioridad: si ISAT < 20% → déficit funcional (iniciar hierro incluso con Ferritina normal)
            if (isat.HasValue && isat.Value < 20)
            {
                observacion = $"ISAT {isat:F1}% < 20%. Déficit funcional de hierro. " +
                              "Iniciar/aumentar hierro IV independientemente de Ferritina.";
                return AccionPrescripcion.Aumentar;
            }

            // Si hay Ferritina, usar rangos de ferritina
            if (ferritina.HasValue)
            {
                rangoAplicado = rangosFerr
                    .OrderBy(r => r.Orden)
                    .FirstOrDefault(r => r.AplicaParaValor(ferritina.Value));

                if (rangoAplicado != null)
                {
                    observacion = rangoAplicado.Observacion;
                    return rangoAplicado.Accion;
                }
            }

            observacion = "Evaluar manualmente con Ferritina e ISAT disponibles.";
            return null;
        }

        private static string GenerarObservacionesGenerales(SnapshotMensual s)
        {
            var obs = new List<string>();

            if (!s.HbValor.HasValue)
                obs.Add("Hemoglobina sin datos este período.");
            else if (s.HbValor < 10)
                obs.Add($"HB={s.HbValor:F1} g/dL. Anemia severa.");
            else if (s.HbValor > 13)
                obs.Add($"HB={s.HbValor:F1} g/dL. Por encima del rango objetivo. Riesgo CV.");

            if (!s.FerritinaValor.HasValue)
                obs.Add("Ferritina sin datos este período.");
            else if (s.FerritinaValor < 200)
                obs.Add($"Ferritina={s.FerritinaValor:F0} ng/mL. Déficit de hierro.");
            else if (s.FerritinaValor > 500)
                obs.Add($"Ferritina={s.FerritinaValor:F0} ng/mL. Sobrecarga de hierro.");

            if (!s.SaturacionValor.HasValue)
                obs.Add("ISAT sin datos este período.");
            else if (s.SaturacionValor < 20)
                obs.Add($"ISAT={s.SaturacionValor:F1}%. Déficit funcional de hierro.");

            if (s.EsDatosPeriodoAnterior)
                obs.Add("⚠️ Usando datos del período anterior. No hay resultados del mes actual.");

            return string.Join(" | ", obs);
        }
    }
}
