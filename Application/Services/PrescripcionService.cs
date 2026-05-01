using DataMedix.Application.Interfaces;
using DataMedix.Application.RuleEngine;
using DataMedix.Domain.Entities;
using System.Text.Json;

namespace DataMedix.Application.Services
{
    /// <summary>
    /// Genera prescripciones sugeridas de EPO y Hierro EV usando el motor de reglas clínicas.
    /// El motor evalúa las 26 reglas configurables en orden de prioridad.
    /// Optimizado para carga masiva: pre-carga toda la data en batch antes de iterar.
    /// </summary>
    public class PrescripcionService
    {
        private readonly IRuleEngine _ruleEngine;
        private readonly ISnapshotMensualRepository _snapshotRepo;
        private readonly IPrescripcionRepository _prescripcionRepo;

        public PrescripcionService(
            IRuleEngine ruleEngine,
            ISnapshotMensualRepository snapshotRepo,
            IPrescripcionRepository prescripcionRepo)
        {
            _ruleEngine = ruleEngine;
            _snapshotRepo = snapshotRepo;
            _prescripcionRepo = prescripcionRepo;
        }

        /// <summary>
        /// Genera prescripciones para todos los pacientes de un período.
        /// Llamado automáticamente tras procesar un lote de importación.
        /// Usa carga batch para eficiencia con 50+ pacientes concurrentes.
        /// </summary>
        public async Task GenerarParaPeriodoAsync(Guid tenantId, DateTime periodDate)
        {
            var snapshots = await _snapshotRepo.GetByPeriodoConDetallesAsync(tenantId, periodDate);
            if (snapshots.Count == 0) return;

            var pacienteIds = snapshots.Select(s => s.PacienteId).Distinct().ToList();

            // Pre-cargar toda la data necesaria en una sola ronda de consultas
            var historialMap = await _snapshotRepo.GetHistorialByPacientesAsync(
                tenantId, pacienteIds, periodDate, meses: 6);

            var conHierroPrevio = await _prescripcionRepo.GetPacientesConHierroPrevioAsync(
                tenantId, periodDate);

            var epoActualMap = await _prescripcionRepo.GetEpoActualByPacientesAsync(
                tenantId, pacienteIds, periodDate);

            var prescExistentes = await _prescripcionRepo.GetByPeriodoBatchAsync(tenantId, periodDate);
            var prescMap = prescExistentes.ToDictionary(p => p.PacienteId);

            var resultado = new List<PrescripcionSugerida>(snapshots.Count);

            foreach (var snapshot in snapshots)
            {
                prescMap.TryGetValue(snapshot.PacienteId, out var existente);

                // Solo regenerar si no existe o está en estado PENDIENTE
                if (existente is { Estado: not null } && existente.Estado != EstadoPrescripcion.Pendiente)
                    continue;

                historialMap.TryGetValue(snapshot.PacienteId, out var historial);
                epoActualMap.TryGetValue(snapshot.PacienteId, out var epoSemana);
                bool tieneHierroPrevio = conHierroPrevio.Contains(snapshot.PacienteId);

                var ctx = BuildContext(tenantId, snapshot, historial ?? [], epoSemana, tieneHierroPrevio, periodDate);
                var evalResult = await _ruleEngine.EvaluateAsync(ctx);

                var prescripcion = existente ?? new PrescripcionSugerida
                {
                    TenantId = tenantId,
                    PacienteId = snapshot.PacienteId,
                    SnapshotId = snapshot.Id,
                    PeriodDate = periodDate,
                    Estado = EstadoPrescripcion.Pendiente
                };

                MapResult(evalResult, prescripcion, ctx);
                resultado.Add(prescripcion);
            }

            await _prescripcionRepo.BulkUpsertSugeridaAsync(resultado);
        }

        /// <summary>
        /// Genera prescripción para un único snapshot. Usado en reprocessing individual.
        /// </summary>
        public async Task GenerarParaSnapshotAsync(SnapshotMensual snapshot, Guid tenantId)
        {
            var existente = await _prescripcionRepo.GetSugeridaByPacienteYPeriodoAsync(
                tenantId, snapshot.PacienteId, snapshot.PeriodDate);

            if (existente is { Estado: not null } && existente.Estado != EstadoPrescripcion.Pendiente)
                return;

            var historial = await _snapshotRepo.GetHistorialAsync(tenantId, snapshot.PacienteId, 6);
            var conHierro = await _prescripcionRepo.GetPacientesConHierroPrevioAsync(tenantId, snapshot.PeriodDate);
            var epoMap = await _prescripcionRepo.GetEpoActualByPacientesAsync(
                tenantId, [snapshot.PacienteId], snapshot.PeriodDate);
            epoMap.TryGetValue(snapshot.PacienteId, out var epoSemana);

            var ctx = BuildContext(
                tenantId, snapshot, historial, epoSemana,
                conHierro.Contains(snapshot.PacienteId), snapshot.PeriodDate);

            var evalResult = await _ruleEngine.EvaluateAsync(ctx);

            existente ??= new PrescripcionSugerida
            {
                TenantId = tenantId,
                PacienteId = snapshot.PacienteId,
                SnapshotId = snapshot.Id,
                PeriodDate = snapshot.PeriodDate,
                Estado = EstadoPrescripcion.Pendiente
            };

            MapResult(evalResult, existente, ctx);
            await _prescripcionRepo.UpsertSugeridaAsync(existente);
        }

        // ── Context builder ────────────────────────────────────────────────────

        private static EvaluationContext BuildContext(
            Guid tenantId,
            SnapshotMensual snapshot,
            List<SnapshotMensual> historial,
            decimal? epoUiSemana,
            bool tieneHierroPrevio,
            DateTime periodDate)
        {
            var ctx = new EvaluationContext
            {
                TenantId     = tenantId,
                PacienteId   = snapshot.PacienteId,
                PeriodDate   = periodDate,
                Hb           = snapshot.HbValor,
                TSAT         = snapshot.SaturacionValor,
                Ferritina    = snapshot.FerritinaValor,
                HierroSerico = snapshot.HierroValor,
                EpoUiSemanaActual = epoUiSemana,
                PrimeraVezHierro  = !tieneHierroPrevio,
                MesActualEsImpar  = periodDate.Month % 2 != 0,
                PerfilHierroActual = snapshot.FerritinaValor.HasValue && snapshot.SaturacionValor.HasValue,
                MesesSinMejoraHb   = CalcularMesesSinMejoraHb(snapshot.HbValor, historial)
            };

            if (snapshot.Paciente?.FechaIngreso.HasValue == true)
            {
                var diff = periodDate - snapshot.Paciente.FechaIngreso!.Value;
                ctx.MesesEnDialisis = Math.Max(0, (int)(diff.TotalDays / 30.44));
            }

            // Valores adicionales desde SnapshotMensualDetalle (Potasio, PTH, Peso, etc.)
            if (snapshot.Detalles is { Count: > 0 })
            {
                foreach (var d in snapshot.Detalles.Where(d => d.ValorNumerico.HasValue))
                {
                    var key = (d.ParametroNombre
                        ?? d.ParametroClinico?.Codigo
                        ?? "").Trim().ToUpperInvariant();

                    switch (key)
                    {
                        case "POTASIO" or "K" or "K+":           ctx.Potasio    ??= d.ValorNumerico; break;
                        case "PTH" or "PARATOHORMONA":            ctx.PTH        ??= d.ValorNumerico; break;
                        case "ALBUMINA" or "ALB":                 ctx.Albumina   ??= d.ValorNumerico; break;
                        case "CREATININA" or "CREAT":             ctx.Creatinina ??= d.ValorNumerico; break;
                        case "BUN":                               ctx.BUN        ??= d.ValorNumerico; break;
                        case "SODIO" or "NA" or "NA+":            ctx.Sodio      ??= d.ValorNumerico; break;
                        case "CALCIO" or "CA" or "CA2+":          ctx.Calcio     ??= d.ValorNumerico; break;
                        case "FOSFORO" or "P" or "FÓSFORO":       ctx.Fosforo    ??= d.ValorNumerico; break;
                        case "PESO" or "PESO KG" or "PESOKG":     ctx.PesoKg     ??= d.ValorNumerico; break;
                    }
                }
            }

            return ctx;
        }

        // Cuenta meses consecutivos (hacia atrás desde el actual) sin mejora de Hb >= 0.5 g/dL.
        private static int CalcularMesesSinMejoraHb(decimal? hbActual, List<SnapshotMensual> historial)
        {
            if (!hbActual.HasValue || historial.Count == 0) return 0;

            int count = 0;
            decimal current = hbActual.Value;

            foreach (var s in historial.OrderByDescending(s => s.PeriodDate))
            {
                if (!s.HbValor.HasValue) break;
                if (current - s.HbValor.Value >= 0.5m) break; // mejora significativa encontrada
                count++;
                current = s.HbValor.Value;
            }

            return count;
        }

        // ── Result mapper ──────────────────────────────────────────────────────

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static void MapResult(EvaluationResult r, PrescripcionSugerida p, EvaluationContext ctx)
        {
            // EPO
            p.ReglaEpoCodigo  = r.ReglaEpoCodigo;
            p.EpoAccion       = r.EpoRecomendada ? "RECOMENDAR" : "NO_RECOMENDAR";
            p.EpoUiSemana     = r.EpoUiSemana > 0 ? r.EpoUiSemana : null;
            p.EpoDosisSugerida = r.EpoUiSemana > 0 ? $"{r.EpoUiSemana:F0} UI/semana" : null;
            p.EpoObservacion  = r.EpoMensaje;

            // Hierro IV
            p.ReglaHierroCodigo  = r.ReglaHierroCodigo;
            p.HierroAccion       = r.HierroRecomendado ? "RECOMENDAR" : null;
            p.HierroMgMes        = r.HierroMgMes;
            p.HierroDosisSugerida = r.HierroMgMes.HasValue ? $"{r.HierroMgMes:F0} mg/mes" : null;
            p.HierroObservacion  = r.HierroMensaje;
            p.HierroGanzoniMg    = r.HierroGanzoniMg;

            // Alertas serializadas para UI
            p.AlertasJson = r.Alertas.Count > 0
                ? JsonSerializer.Serialize(r.Alertas, _jsonOpts)
                : null;

            // Observaciones generales: concatena mensajes de alertas
            if (r.Alertas.Count > 0)
                p.ObservacionesGenerales = string.Join(" | ",
                    r.Alertas.Select(a => $"[{a.Severidad}] {a.Mensaje}"));

            // Contexto clínico serializado para trazabilidad / auditoría
            p.ContextoJson = JsonSerializer.Serialize(new
            {
                ctx.Hb, ctx.TSAT, ctx.Ferritina, ctx.HierroSerico,
                ctx.Potasio, ctx.PTH, ctx.PesoKg,
                ctx.MesesEnDialisis, ctx.MesesSinMejoraHb,
                ctx.PrimeraVezHierro, ctx.MesActualEsImpar, ctx.PerfilHierroActual,
                ctx.EpoUiSemanaActual,
                r.ModificadoresAplicados
            }, _jsonOpts);
        }
    }
}
