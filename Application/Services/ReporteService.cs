using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Application.RuleEngine;
using DataMedix.Domain.Entities;
using System.Text.Json;

namespace DataMedix.Application.Services
{
    public class ReporteService : IReporteService
    {
        private readonly IPrescripcionRepository _prescRepo;
        private readonly ISnapshotMensualRepository _snapshotRepo;
        private readonly IReglaClinicaRepository _reglaRepo;

        private static readonly JsonSerializerOptions _jsonOpts =
            new() { PropertyNameCaseInsensitive = true };

        public ReporteService(
            IPrescripcionRepository prescRepo,
            ISnapshotMensualRepository snapshotRepo,
            IReglaClinicaRepository reglaRepo)
        {
            _prescRepo   = prescRepo;
            _snapshotRepo = snapshotRepo;
            _reglaRepo   = reglaRepo;
        }

        public async Task<ResumenClinicoDto> GetResumenPeriodoAsync(
            Guid tenantId, DateTime periodDate, string? planSalud = null)
        {
            // ── Carga de datos ─────────────────────────────────────────────────
            var prescripciones = await _prescRepo.GetByPeriodoAsync(tenantId, periodDate);

            // Todos los snapshots del período (sin paginación)
            var snapshots = (await _snapshotRepo.GetByPeriodoConDetallesAsync(tenantId, periodDate))
                .ToList();

            // Filtrar por plan de salud en memoria
            if (!string.IsNullOrEmpty(planSalud))
            {
                var idsEnPlan = snapshots
                    .Where(s => s.PlanSalud == planSalud)
                    .Select(s => s.PacienteId)
                    .ToHashSet();

                snapshots = snapshots.Where(s => idsEnPlan.Contains(s.PacienteId)).ToList();
                prescripciones = prescripciones
                    .Where(p => idsEnPlan.Contains(p.PacienteId))
                    .ToList();
            }

            // Nombres de reglas para etiquetas legibles
            var reglas = await _reglaRepo.GetAllAsync();
            var reglaNombres = reglas.ToDictionary(r => r.Codigo, r => r.Nombre);

            // Alertas parseadas para cada prescripción
            var prescConAlertas = prescripciones
                .Select(p => (presc: p, alertas: ParseAlertas(p.AlertasJson)))
                .ToList();

            var dto = new ResumenClinicoDto { Periodo = periodDate };
            int total = prescripciones.Count;

            // ── KPIs ───────────────────────────────────────────────────────────
            dto.TotalPrescripciones      = total;
            dto.TotalPacientesEvaluados  = snapshots.Count;
            dto.PacientesConEpo          = prescripciones.Count(p => p.EpoUiSemana > 0);
            dto.PacientesConHierro       = prescripciones.Count(p => p.HierroMgMes > 0);
            dto.AlertasCriticas          = prescConAlertas.Count(x => x.alertas.Any(a => a.Severidad == "CRITICA"));
            dto.AlertasAltas             = prescConAlertas.Count(x =>
                x.alertas.Any(a => a.Severidad == "ALTA") &&
                !x.alertas.Any(a => a.Severidad == "CRITICA"));
            dto.PrescripcionesPendientes = prescripciones.Count(p => p.Estado == "PENDIENTE");
            dto.PrescripcionesAprobadas  = prescripciones.Count(p => p.Estado == "APROBADO");
            dto.PorcentajeAprobacion     = total > 0
                ? Math.Round((decimal)dto.PrescripcionesAprobadas / total * 100, 1)
                : 0;

            // ── Zonas Hb ──────────────────────────────────────────────────────
            dto.HbCritico  = snapshots.Count(s => s.HbValor < 8);
            dto.HbBajo     = snapshots.Count(s => s.HbValor >= 8 && s.HbValor < 10);
            dto.HbObjetivo = snapshots.Count(s => s.HbValor >= 10 && s.HbValor <= 13);
            dto.HbAlto     = snapshots.Count(s => s.HbValor > 13);
            dto.HbSinDato  = snapshots.Count(s => !s.HbValor.HasValue);

            // ── Distribución EPO ──────────────────────────────────────────────
            dto.DistribucionEpo = prescripciones
                .Where(p => !string.IsNullOrEmpty(p.ReglaEpoCodigo))
                .GroupBy(p => p.ReglaEpoCodigo!)
                .Select(g => new ReglaDistribucionDto
                {
                    Codigo       = g.Key,
                    Descripcion  = reglaNombres.GetValueOrDefault(g.Key, g.Key),
                    Pacientes    = g.Count(),
                    DosisPromedio = g.Any(p => p.EpoUiSemana > 0)
                        ? Math.Round(g.Average(p => p.EpoUiSemana ?? 0), 0) : null,
                    Porcentaje   = total > 0
                        ? Math.Round((decimal)g.Count() / total * 100, 1) : 0,
                    Zona         = ZonaEpo(g.Key)
                })
                .OrderBy(r => r.Codigo)
                .ToList();

            // ── Distribución Hierro ───────────────────────────────────────────
            dto.DistribucionHierro = prescripciones
                .Where(p => !string.IsNullOrEmpty(p.ReglaHierroCodigo))
                .GroupBy(p => p.ReglaHierroCodigo!)
                .Select(g => new ReglaDistribucionDto
                {
                    Codigo       = g.Key,
                    Descripcion  = reglaNombres.GetValueOrDefault(g.Key, g.Key),
                    Pacientes    = g.Count(),
                    DosisPromedio = g.Any(p => p.HierroMgMes > 0)
                        ? Math.Round(g.Average(p => p.HierroMgMes ?? 0), 0) : null,
                    Porcentaje   = total > 0
                        ? Math.Round((decimal)g.Count() / total * 100, 1) : 0,
                    Zona         = "objetivo"
                })
                .OrderByDescending(r => r.Pacientes)
                .ToList();

            // ── Resumen de alertas por tipo ────────────────────────────────────
            dto.ResumenAlertas = prescConAlertas
                .SelectMany(x => x.alertas)
                .GroupBy(a => a.Codigo)
                .Select(g => new AlertaTipoDto
                {
                    Codigo      = g.Key,
                    Descripcion = DescripcionAlerta(g.Key),
                    Severidad   = g.First().Severidad,
                    Total       = g.Count()
                })
                .OrderBy(a => OrdenSeveridad(a.Severidad))
                .ThenByDescending(a => a.Total)
                .ToList();

            // ── Pacientes que requieren atención ──────────────────────────────
            var snapMap = snapshots.ToDictionary(s => s.PacienteId, s => s);
            dto.PacientesAlerta = prescConAlertas
                .Where(x => x.alertas.Any())
                .Select(x =>
                {
                    snapMap.TryGetValue(x.presc.PacienteId, out var snap);
                    var sevMax = x.alertas.Any(a => a.Severidad == "CRITICA") ? "CRITICA"
                        : x.alertas.Any(a => a.Severidad == "ALTA") ? "ALTA" : "MEDIA";
                    return new PacienteAlertaDto
                    {
                        PacienteId         = x.presc.PacienteId,
                        NombreCompleto     = x.presc.Paciente?.NombreCompleto ?? "—",
                        Identificacion     = x.presc.Paciente?.Identificacion ?? "—",
                        Hb                 = snap?.HbValor,
                        SeveridadMaxima    = sevMax,
                        EstadoPrescripcion = x.presc.Estado,
                        Alertas = x.alertas.Select(a => new AlertaItemDto
                        {
                            Codigo      = a.Codigo,
                            Descripcion = DescripcionAlerta(a.Codigo),
                            Severidad   = a.Severidad,
                            Mensaje     = a.Mensaje
                        }).ToList()
                    };
                })
                .OrderBy(p => OrdenSeveridad(p.SeveridadMaxima))
                .ThenBy(p => p.Hb)
                .ToList();

            // ── Tendencia Hb del grupo ─────────────────────────────────────────
            dto.TendenciaHb = await BuildTendenciaAsync(tenantId, periodDate, snapshots);

            return dto;
        }

        public Task<List<string>> GetPlanesSaludAsync(Guid tenantId, DateTime periodDate) =>
            _snapshotRepo.GetPlanesSaludAsync(tenantId, periodDate);

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task<List<TendenciaHbDto>> BuildTendenciaAsync(
            Guid tenantId, DateTime hasta, List<SnapshotMensual> actual)
        {
            var pacienteIds = actual.Select(s => s.PacienteId).ToList();
            if (!pacienteIds.Any()) return [];

            var historial = await _snapshotRepo.GetHistorialByPacientesAsync(
                tenantId, pacienteIds, hasta, meses: 6);

            return historial.Values
                .SelectMany(snaps => snaps)
                .Where(s => s.HbValor.HasValue)
                .GroupBy(s => s.PeriodDate)
                .OrderBy(g => g.Key)
                .Select(g => new TendenciaHbDto
                {
                    Periodo      = g.Key,
                    PeriodoLabel = g.Key.ToString("MMM yy"),
                    HbPromedio   = Math.Round(g.Average(s => s.HbValor!.Value), 1),
                    Pacientes    = g.Count()
                })
                .ToList();
        }

        private static string ZonaEpo(string codigo) => codigo switch
        {
            "EPO-07" => "critica",
            "EPO-06" => "baja",
            "EPO-05" or "EPO-04" => "atencion",
            "EPO-03" or "EPO-02" => "objetivo",
            "EPO-01" => "alto",
            _ => "objetivo"
        };

        internal static string DescripcionAlerta(string codigo) => codigo switch
        {
            "ALERT-HB-CRIT"           => "Hemoglobina crítica",
            "ALERT-HB-BAJA"           => "Hemoglobina baja",
            "ALERT-HB-ALTA"           => "Hemoglobina elevada",
            "ALERT-K-CRIT"            => "Potasio crítico",
            "ALERT-RESIST-EPO"        => "Resistencia a EPO",
            "ALERT-PRUEBA-SENS"       => "Prueba de sensibilidad hierro",
            "ALERT-FERRITINA-EXTREMA" => "Ferritina extrema",
            _ => codigo
        };

        private static int OrdenSeveridad(string sev) => sev switch
        {
            "CRITICA" => 0, "ALTA" => 1, _ => 2
        };

        private static List<AlertaClinica> ParseAlertas(string? json)
        {
            if (string.IsNullOrEmpty(json)) return [];
            try { return JsonSerializer.Deserialize<List<AlertaClinica>>(json, _jsonOpts) ?? []; }
            catch { return []; }
        }
    }
}
