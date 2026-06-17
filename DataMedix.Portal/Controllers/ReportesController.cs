using ClosedXML.Excel;
using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataMedix.Portal.Controllers
{
    [Authorize]
    [Route("api/reportes")]
    public class ReportesController : Controller
    {
        private readonly IResultadoLaboratorioRepository _resultadoRepo;

        public ReportesController(IResultadoLaboratorioRepository resultadoRepo)
        {
            _resultadoRepo = resultadoRepo;
        }

        private Guid GetTenantId()
        {
            var claim = User.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        [HttpGet("pivot-excel")]
        public async Task<IActionResult> PivotExcel(
            [FromQuery] int mes,
            [FromQuery] int anio,
            [FromQuery] string? tiposAtencion,
            [FromQuery] string? columnaKeys,
            [FromQuery] string? busquedaPaciente)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Forbid();

            var filtro = new ResultadoFiltro
            {
                MesDe = mes, AnioDe = anio,
                MesHasta = mes, AnioHasta = anio,
                Tamano = 10_000
            };
            var all = await _resultadoRepo.GetForExportAsync(tenantId, filtro);

            // filtro por tipos de atención
            var tipos = tiposAtencion?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
            if (tipos.Length > 0)
                all = all.Where(r => r.TipoAtencion != null && tipos.Contains(r.TipoAtencion)).ToList();

            // filtro por nombre/cédula
            if (!string.IsNullOrWhiteSpace(busquedaPaciente))
            {
                var b = busquedaPaciente.Trim().ToUpperInvariant();
                all = all.Where(r =>
                    r.Paciente.Identificacion.ToUpperInvariant().Contains(b) ||
                    r.Paciente.NombreCompleto.ToUpperInvariant().Contains(b)).ToList();
            }

            // todas las columnas en orden
            var allCols = all
                .GroupBy(r => r.ParametroClinicoId.HasValue
                    ? r.ParametroClinicoId.Value.ToString()
                    : "__" + (r.ParametroRaw?.Trim().ToUpperInvariant() ?? "?"))
                .Select(g => new
                {
                    Key    = g.Key,
                    Nombre = g.First().ParametroClinico?.Nombre ?? g.First().ParametroRaw ?? "?",
                    Orden  = g.First().ParametroClinico?.OrdenVisualizacion ?? 9999
                })
                .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
                .ToList();

            // columnas visibles (filtro de parámetros)
            var keys = columnaKeys?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
            var visCols = keys.Length > 0
                ? allCols.Where(c => keys.Contains(c.Key)).ToList()
                : allCols;

            // filas pivot (un paciente por fila)
            var filas = all
                .GroupBy(r => r.PacienteId)
                .Select(g =>
                {
                    var first = g.First();
                    var celdas = g.ToDictionary(
                        r => r.ParametroClinicoId.HasValue
                            ? r.ParametroClinicoId.Value.ToString()
                            : "__" + (r.ParametroRaw?.Trim().ToUpperInvariant() ?? "?"),
                        r => (r.ValorNumerico, r.ResultadoTexto, r.EsPatologico));
                    return new
                    {
                        first.Paciente.Identificacion,
                        Nombre       = first.Paciente.NombreCompleto,
                        TipoAtencion = first.TipoAtencion,
                        Celdas       = celdas
                    };
                })
                .OrderBy(f => f.TipoAtencion).ThenBy(f => f.Nombre)
                .ToList();

            // construir Excel
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Resultados Crudos");

            int totalCols = 3 + visCols.Count;
            ws.Cell(1, 1).Value = "TIPO AT.";
            ws.Cell(1, 2).Value = "CÉDULA";
            ws.Cell(1, 3).Value = "PACIENTE";
            for (int c = 0; c < visCols.Count; c++)
                ws.Cell(1, c + 4).Value = visCols[c].Nombre;

            var hdrRange = ws.Range(1, 1, 1, totalCols);
            hdrRange.Style.Font.Bold = true;
            hdrRange.Style.Font.FontColor = XLColor.White;
            hdrRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0ea5e9");
            hdrRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(1).Height = 20;
            ws.SheetView.FreezeRows(1);
            ws.SheetView.FreezeColumns(3);

            int row = 2;
            foreach (var fila in filas)
            {
                ws.Cell(row, 1).Value = fila.TipoAtencion ?? "—";
                ws.Cell(row, 2).Value = fila.Identificacion;
                ws.Cell(row, 3).Value = fila.Nombre;

                bool tienePatologico = false;
                for (int c = 0; c < visCols.Count; c++)
                {
                    if (!fila.Celdas.TryGetValue(visCols[c].Key, out var celda)) continue;
                    var cell = ws.Cell(row, c + 4);
                    if (celda.ValorNumerico.HasValue)
                    {
                        cell.Value = (double)celda.ValorNumerico.Value;
                        cell.Style.NumberFormat.Format = "0.####";
                    }
                    else if (celda.ResultadoTexto != null)
                        cell.Value = celda.ResultadoTexto;

                    if (celda.EsPatologico)
                    {
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontColor = XLColor.FromHtml("#dc2626");
                        tienePatologico = true;
                    }
                }

                if (tienePatologico)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#fff5f5");
                else if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");

                row++;
            }

            ws.Column(1).Width = 14;
            ws.Column(2).Width = 14;
            ws.Column(3).Width = 35;
            for (int c = 0; c < visCols.Count; c++)
                ws.Column(c + 4).Width = Math.Max(12, Math.Min(visCols[c].Nombre.Length + 2, 28));

            if (row > 2)
            {
                var tblRange = ws.Range(1, 1, row - 1, totalCols);
                tblRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tblRange.Style.Border.InsideBorder  = XLBorderStyleValues.Hair;
            }

            var periodo = new DateTime(anio, mes, 1).ToString("MMMM yyyy");
            wb.Properties.Title  = $"Resultados Crudos — {periodo}";
            wb.Properties.Author = "DataMedix";

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"resultados_crudos_{anio}_{mes:D2}_{DateTime.Now:HHmm}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpGet("resultados-excel")]
        public async Task<IActionResult> ResultadosExcel(
            [FromQuery] string? busquedaPaciente,
            [FromQuery] Guid? parametroClinicoId,
            [FromQuery] int? anioDe,
            [FromQuery] int? mesDe,
            [FromQuery] int? anioHasta,
            [FromQuery] int? mesHasta,
            [FromQuery] Guid? loteId)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Forbid();

            var filtro = new ResultadoFiltro
            {
                BusquedaPaciente = busquedaPaciente,
                ParametroClinicoId = parametroClinicoId,
                AnioDe = anioDe,
                MesDe = mesDe,
                AnioHasta = anioHasta,
                MesHasta = mesHasta,
                LoteId = loteId
            };

            var resultados = await _resultadoRepo.GetForExportAsync(tenantId, filtro);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Resultados de Laboratorio");

            string[] headers =
            {
                "Identificación", "Paciente", "Parámetro", "Valor", "Unidad",
                "Ref. Mín", "Ref. Máx", "Patológico", "Período", "Lote"
            };

            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0ea5e9");
            headerRange.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var r in resultados)
            {
                ws.Cell(row, 1).Value = r.Paciente.Identificacion;
                ws.Cell(row, 2).Value = r.Paciente.NombreCompleto;
                ws.Cell(row, 3).Value = r.ParametroClinico?.Nombre ?? r.ParametroRaw ?? "—";
                if (r.ValorNumerico.HasValue)
                    ws.Cell(row, 4).Value = (double)r.ValorNumerico.Value;
                else
                    ws.Cell(row, 4).Value = r.ResultadoTexto ?? "—";
                ws.Cell(row, 5).Value = r.UnidadMedida ?? "—";
                if (r.ValorMinReferencia.HasValue)
                    ws.Cell(row, 6).Value = (double)r.ValorMinReferencia.Value;
                else
                    ws.Cell(row, 6).Value = "—";
                if (r.ValorMaxReferencia.HasValue)
                    ws.Cell(row, 7).Value = (double)r.ValorMaxReferencia.Value;
                else
                    ws.Cell(row, 7).Value = "—";
                ws.Cell(row, 8).Value = r.EsPatologico ? "Sí" : "No";
                ws.Cell(row, 9).Value = r.PeriodDate.ToString("yyyy-MM");
                ws.Cell(row, 10).Value = r.Lote?.NombreArchivoOriginal ?? "—";

                if (r.EsPatologico)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#fee2e2");

                row++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            var fileName = $"resultados_laboratorio_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}
