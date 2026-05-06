using DataMedix.Application.DTOs.HojaEpo;
using DataMedix.Application.Interfaces;

namespace DataMedix.Application.Services
{
    public class HojaEpoService : IHojaEpoService
    {
        private readonly ISnapshotMensualRepository _snapRepo;
        private readonly IPrescripcionRepository _prescRepo;

        public HojaEpoService(ISnapshotMensualRepository snapRepo, IPrescripcionRepository prescRepo)
        {
            _snapRepo = snapRepo;
            _prescRepo = prescRepo;
        }

        public async Task<List<HojaEpoRowDto>> GetMatrizAsync(Guid tenantId, HojaEpoFiltroDto filtro)
        {
            var meses = filtro.Meses;
            if (!meses.Any()) return [];

            var desde = meses.First();
            var hasta = meses.Last();

            // Carga snapshots (incluye navegación Paciente)
            var snapshots = await _snapRepo.GetByRangoAsync(
                tenantId, desde, hasta, filtro.BusquedaPaciente, filtro.ParametroClinicoId);

            var pacienteIds = snapshots.Select(s => s.PacienteId).Distinct().ToList();
            if (!pacienteIds.Any()) return [];

            // Carga prescripciones en lote para todos los pacientes del rango
            var prescSugeridas = await _prescRepo.GetSugeridaByRangoAsync(tenantId, desde, hasta, pacienteIds);
            var prescFinales   = await _prescRepo.GetFinalByRangoAsync(tenantId, desde, hasta, pacienteIds);

            // Índices para búsqueda O(1)
            var snapIdx  = snapshots.ToDictionary(s => (s.PacienteId, s.PeriodDate));
            var prescIdx = prescSugeridas.ToDictionary(p => (p.PacienteId, p.PeriodDate));
            var finalIdx = prescFinales.ToDictionary(f => (f.PacienteId, f.PeriodDate));

            // Pacientes únicos (la navegación ya viene en los snapshots)
            var pacientes = snapshots
                .Where(s => s.Paciente != null)
                .Select(s => s.Paciente)
                .DistinctBy(p => p.Id)
                .ToList();

            var rows = new List<HojaEpoRowDto>(pacientes.Count);

            foreach (var paciente in pacientes)
            {
                var row = new HojaEpoRowDto
                {
                    PacienteId           = paciente.Id,
                    Identificacion       = paciente.Identificacion ?? "",
                    NombrePaciente       = paciente.NombreCompleto,
                    TiempoDialisisMeses  = paciente.MesesEnDialisis ?? 0,
                };

                foreach (var mes in meses)
                {
                    var celda = new HojaEpoCeldaDto { PeriodDate = mes };

                    if (snapIdx.TryGetValue((paciente.Id, mes), out var snap))
                    {
                        celda.HbValor         = snap.HbValor;
                        celda.HbUnidad        = snap.HbUnidad;
                        celda.HierroValor     = snap.HierroValor;
                        celda.FerritinaValor  = snap.FerritinaValor;
                        celda.SaturacionValor = snap.SaturacionValor;
                    }

                    if (prescIdx.TryGetValue((paciente.Id, mes), out var presc))
                    {
                        celda.EpoUiSemana           = presc.EpoUiSemana;
                        celda.HierroMgMes           = presc.HierroMgMes;
                        celda.EpoAccion             = presc.EpoAccion;
                        celda.HierroAccion          = presc.HierroAccion;
                        celda.PrescripcionSugeridaId = presc.Id;
                        celda.EstadoPrescripcion    = presc.Estado;
                    }

                    if (finalIdx.TryGetValue((paciente.Id, mes), out var final))
                    {
                        celda.AjusteEpo          = final.EpoDosis;
                        celda.AjusteHierro       = final.HierroDosis;
                        celda.PrescripcionFinalId = final.Id;
                    }

                    row.Meses[mes] = celda;
                }

                CalcularResumen(row);
                rows.Add(row);
            }

            return rows.OrderBy(r => r.NombrePaciente).ToList();
        }

        public async Task GuardarAjusteAsync(
            Guid tenantId, Guid pacienteId, DateTime periodDate,
            string? ajusteEpo, string? ajusteHierro,
            Guid medicoId, Guid? prescSugeridaId) =>
            await _prescRepo.GuardarAjusteHojaEpoAsync(
                tenantId, pacienteId, periodDate,
                ajusteEpo, ajusteHierro, medicoId, prescSugeridaId);

        private static void CalcularResumen(HojaEpoRowDto row)
        {
            var hbVals = row.Meses.Values.Where(c => c.HbValor.HasValue).Select(c => c.HbValor!.Value).ToList();
            row.PromedioHb = hbVals.Count > 0 ? Math.Round(hbVals.Average(), 1) : null;

            var epoVals = row.Meses.Values.Where(c => c.EpoUiSemana.HasValue).Select(c => c.EpoUiSemana!.Value).ToList();
            row.PromedioEpo = epoVals.Count > 0 ? Math.Round(epoVals.Average(), 0) : null;

            var hierroVals = row.Meses.Values.Where(c => c.HierroMgMes.HasValue).Select(c => c.HierroMgMes!.Value).ToList();
            row.PromedioHierro = hierroVals.Count > 0 ? Math.Round(hierroVals.Average(), 0) : null;
            row.SumaHierro     = hierroVals.Count > 0 ? hierroVals.Sum() : null;
        }
    }
}
