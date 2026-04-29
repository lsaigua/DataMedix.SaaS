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
