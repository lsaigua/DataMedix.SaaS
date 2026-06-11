using ClosedXML.Excel;
using DataMedix.Application.DTOs;
using DataMedix.Application.DTOs.HojaEpo;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;

namespace DataMedix.Infrastructure.Excel
{
    public class ExcelExporter : IExcelExporter
    {
        public byte[] ExportarHojaEpo(
            IEnumerable<HojaEpoRowDto> filas,
            IEnumerable<DateTime> meses,
            string periodo)
        {
            var filasList  = filas.ToList();
            var mesesList  = meses.OrderBy(m => m).ToList();
            var headerColor = XLColor.FromHtml("#4f46e5");
            var mesColor    = XLColor.FromHtml("#7c3aed");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Prescripción Sugerida");

            // Columnas fijas + 11 por mes
            string[] colsFijas = ["N°", "PACIENTE", "CÉDULA"];
            string[] colsMes   = ["HB (g/dL)", "HIERRO (µg/dL)", "FERRIT. (ng/mL)",
                                   "ISAT (%)", "EPO (UI/sem)", "AJ.EPO MÉDICO",
                                   "HIERRO IV (mg/mes)", "AJ.HIERRO MÉDICO",
                                   "ESTADO", "PATOLÓGICO", "ACCIÓN"];
            int totalCols = colsFijas.Length + colsMes.Length * mesesList.Count;

            // ── Fila 1: cabecera fija + grupos de mes ──────────────────────────
            for (int c = 0; c < colsFijas.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = colsFijas[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = headerColor;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            }
            // Merge rowspan para las 3 columnas fijas
            for (int c = 1; c <= colsFijas.Length; c++)
                ws.Range(1, c, 2, c).Merge();

            for (int mi = 0; mi < mesesList.Count; mi++)
            {
                int startCol = colsFijas.Length + mi * colsMes.Length + 1;
                int endCol   = startCol + colsMes.Length - 1;
                var mesCell  = ws.Cell(1, startCol);
                mesCell.Value = mesesList[mi].ToString("MMM yyyy").ToUpper();
                mesCell.Style.Font.Bold = true;
                mesCell.Style.Font.FontColor = XLColor.White;
                mesCell.Style.Fill.BackgroundColor = mesColor;
                mesCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(1, startCol, 1, endCol).Merge();
            }

            // ── Fila 2: sub-cabeceras por mes ─────────────────────────────────
            for (int mi = 0; mi < mesesList.Count; mi++)
            {
                int startCol = colsFijas.Length + mi * colsMes.Length + 1;
                for (int ci = 0; ci < colsMes.Length; ci++)
                {
                    var cell = ws.Cell(2, startCol + ci);
                    cell.Value = colsMes[ci];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = headerColor;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Font.FontSize = 8.5;
                }
            }

            ws.Row(1).Height = 18;
            ws.Row(2).Height = 28;
            ws.SheetView.FreezeRows(2);
            ws.SheetView.FreezeColumns(colsFijas.Length);

            // ── Filas de datos: UNA FILA por paciente (igual que la grilla) ────
            int row  = 3;
            int pnum = 1;
            foreach (var fila in filasList)
            {
                ws.Cell(row, 1).Value = pnum++;
                ws.Cell(row, 2).Value = fila.NombrePaciente;
                ws.Cell(row, 3).Value = fila.Identificacion;

                for (int mi = 0; mi < mesesList.Count; mi++)
                {
                    var mes = mesesList[mi];
                    fila.Meses.TryGetValue(mes, out var celda);
                    int bc = colsFijas.Length + mi * colsMes.Length + 1;

                    if (celda != null)
                    {
                        SetNum(ws, row, bc,     celda.HbValor,         "0.0");
                        SetNum(ws, row, bc + 1, celda.HierroValor,     "0.00");
                        SetNum(ws, row, bc + 2, celda.FerritinaValor,  "0.0");
                        SetNum(ws, row, bc + 3, celda.SaturacionValor, "0.0");
                        SetNum(ws, row, bc + 4, celda.EpoEfectivo,    "#,##0");
                        ws.Cell(row, bc + 5).Value  = celda.AjusteEpo   ?? "";
                        SetNum(ws, row, bc + 6, celda.HierroEfectivo, "#,##0");
                        ws.Cell(row, bc + 7).Value  = celda.AjusteHierro ?? "";
                        ws.Cell(row, bc + 8).Value  = celda.EstadoPrescripcion ?? "";
                        ws.Cell(row, bc + 9).Value  = celda.HbValor.HasValue
                            ? (celda.HbValor < 10 || celda.HbValor > 13 ? "Sí ⚠" : "No ✓") : "";
                        ws.Cell(row, bc + 10).Value = celda.EpoAccion ?? "";

                        if (celda.HbValor.HasValue)
                        {
                            ws.Cell(row, bc).Style.Font.Bold = true;
                            ws.Cell(row, bc).Style.Font.FontColor =
                                celda.HbValor < 10 ? XLColor.FromHtml("#dc2626") :
                                celda.HbValor > 13 ? XLColor.FromHtml("#d97706") :
                                                     XLColor.FromHtml("#059669");
                        }
                    }
                }

                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
                row++;
            }

            // ── Ajustar anchos ─────────────────────────────────────────────────
            ws.Column(1).Width = 5;
            ws.Column(2).Width = 35;
            ws.Column(3).Width = 14;
            for (int mi = 0; mi < mesesList.Count; mi++)
            {
                int startCol = colsFijas.Length + mi * colsMes.Length + 1;
                for (int ci = 0; ci < colsMes.Length; ci++)
                    ws.Column(startCol + ci).Width = 12;
            }

            // Borde
            if (row > 3)
            {
                var tableRange = ws.Range(1, 1, row - 1, totalCols);
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.InsideBorder  = XLBorderStyleValues.Hair;
            }

            wb.Properties.Title  = $"Prescripción Sugerida — {periodo}";
            wb.Properties.Author = "DataMedix";

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static void SetNum(IXLWorksheet ws, int row, int col, decimal? val, string fmt)
        {
            if (val.HasValue)
            {
                ws.Cell(row, col).Value = (double)val.Value;
                ws.Cell(row, col).Style.NumberFormat.Format = fmt;
            }
        }


        public byte[] ReconstruirArchivoLote(IEnumerable<ImportacionDetalle> detalles, string nombreArchivo)
        {
            var rows = detalles.ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Resultados");

            string[] headers = ["FECHA ORDEN", "PLAN SALUD", "TIPO ATENCIÓN",
                                 "IDENTIFICACIÓN", "PACIENTE", "EXAMEN",
                                 "PARÁMETRO", "RESULTADO", "UNIDAD"];

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            ws.Row(1).Height = 18;
            ws.SheetView.FreezeRows(1);

            for (int i = 0; i < rows.Count; i++)
            {
                var d   = rows[i];
                int row = i + 2;
                ws.Cell(row, 1).Value = d.FechaOrdenRaw    ?? "";
                ws.Cell(row, 2).Value = d.PlanSaludRaw     ?? "";
                ws.Cell(row, 3).Value = d.TipoAtencionRaw  ?? "";
                ws.Cell(row, 4).Value = d.IdentificacionRaw ?? "";
                ws.Cell(row, 5).Value = d.PacienteRaw      ?? "";
                ws.Cell(row, 6).Value = d.ExamenRaw        ?? "";
                ws.Cell(row, 7).Value = d.ParametroRaw     ?? "";
                ws.Cell(row, 8).Value = d.ResultadoRaw     ?? "";
                ws.Cell(row, 9).Value = d.UnidadMedidaRaw  ?? "";
                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
            }

            ws.Columns(1, headers.Length).AdjustToContents();
            ws.Column(5).Width = Math.Min(ws.Column(5).Width, 40);

            if (rows.Count > 0)
            {
                var rng = ws.Range(1, 1, rows.Count + 1, headers.Length);
                rng.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rng.Style.Border.InsideBorder  = XLBorderStyleValues.Hair;
            }

            wb.Properties.Title  = nombreArchivo;
            wb.Properties.Author = "DataMedix";

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public byte[] ExportarPrescripcionesMes(
            IEnumerable<PrescripcionSugerida> prescripciones,
            IEnumerable<SnapshotMensual> snapshots,
            string periodo)
        {
            var snapMap = snapshots.ToDictionary(s => s.PacienteId, s => s);
            var rows = prescripciones
                .OrderBy(p => p.Paciente?.PrimerApellido)
                .ThenBy(p => p.Paciente?.PrimerNombre)
                .ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Prescripción Sugerida");

            // ── Cabeceras ──────────────────────────────────────────────────────
            string[] headers =
            [
                "N°", "PACIENTE", "CÉDULA", "PLAN SALUD",
                "HB (g/dL)", "FERRITINA (ng/mL)", "ISAT (%)", "HIERRO SÉRICO (µg/dL)",
                "EPO (UI/sem)", "DOSIS EPO SUGERIDA",
                "HIERRO IV (mg/mes)", "DOSIS HIERRO SUGERIDA",
                "ESTADO", "OBSERVACIONES"
            ];

            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4f46e5");
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(1).Height = 20;
            ws.SheetView.FreezeRows(1);

            // ── Filas de datos ─────────────────────────────────────────────────
            for (int i = 0; i < rows.Count; i++)
            {
                var p    = rows[i];
                var snap = snapMap.GetValueOrDefault(p.PacienteId);
                int r    = i + 2;

                ws.Cell(r, 1).Value  = i + 1;
                ws.Cell(r, 2).Value  = p.Paciente?.NombreCompleto ?? "—";
                ws.Cell(r, 3).Value  = p.Paciente?.Identificacion ?? "—";
                ws.Cell(r, 4).Value  = snap?.PlanSalud ?? "—";
                ws.Cell(r, 5).Value  = snap?.HbValor.HasValue == true ? (double)snap.HbValor!.Value : 0;
                ws.Cell(r, 6).Value  = snap?.FerritinaValor.HasValue == true ? (double)snap.FerritinaValor!.Value : 0;
                ws.Cell(r, 7).Value  = snap?.SaturacionValor.HasValue == true ? (double)snap.SaturacionValor!.Value : 0;
                ws.Cell(r, 8).Value  = snap?.HierroValor.HasValue == true ? (double)snap.HierroValor!.Value : 0;
                ws.Cell(r, 9).Value  = p.EpoUiSemana.HasValue ? (double)p.EpoUiSemana!.Value : 0;
                ws.Cell(r, 10).Value = p.EpoDosisSugerida ?? "—";
                ws.Cell(r, 11).Value = p.HierroMgMes.HasValue ? (double)p.HierroMgMes!.Value : 0;
                ws.Cell(r, 12).Value = p.HierroDosisSugerida ?? "—";
                ws.Cell(r, 13).Value = p.Estado ?? "—";
                ws.Cell(r, 14).Value = p.ObservacionesGenerales ?? "";

                // Formato numérico
                ws.Cell(r, 5).Style.NumberFormat.Format  = "0.0";
                ws.Cell(r, 6).Style.NumberFormat.Format  = "0.0";
                ws.Cell(r, 7).Style.NumberFormat.Format  = "0.0";
                ws.Cell(r, 8).Style.NumberFormat.Format  = "0.00";
                ws.Cell(r, 9).Style.NumberFormat.Format  = "#,##0";
                ws.Cell(r, 11).Style.NumberFormat.Format = "#,##0";

                // Color fila alternada
                if (r % 2 == 0)
                    ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");

                // Color HB según rango clínico
                if (snap?.HbValor.HasValue == true)
                {
                    var hbCell = ws.Cell(r, 5);
                    hbCell.Style.Font.Bold = true;
                    hbCell.Style.Font.FontColor = snap.HbValor < 10 ? XLColor.FromHtml("#dc2626")
                        : snap.HbValor > 13 ? XLColor.FromHtml("#d97706")
                        : XLColor.FromHtml("#059669");
                }

                // Color estado
                var estadoCell = ws.Cell(r, 13);
                estadoCell.Style.Font.Bold = true;
                estadoCell.Style.Font.FontColor = p.Estado switch
                {
                    "APROBADO"  => XLColor.FromHtml("#059669"),
                    "RECHAZADO" => XLColor.FromHtml("#dc2626"),
                    "REVISADO"  => XLColor.FromHtml("#1d4ed8"),
                    _           => XLColor.FromHtml("#92400e")
                };
            }

            // ── Ajustar anchos ─────────────────────────────────────────────────
            ws.Columns(1, headers.Length).AdjustToContents();
            ws.Column(2).Width  = Math.Min(ws.Column(2).Width,  40);  // PACIENTE máx 40
            ws.Column(14).Width = Math.Min(ws.Column(14).Width, 60);  // OBSERVACIONES máx 60
            ws.Column(14).Style.Alignment.WrapText = true;

            // Borde tabla
            var dataRange = ws.Range(1, 1, rows.Count + 1, headers.Length);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder  = XLBorderStyleValues.Hair;

            wb.Properties.Title  = $"Prescripción Sugerida — {periodo}";
            wb.Properties.Author = "DataMedix";

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public byte[] GenerarErroresExcel(IEnumerable<ErrorImportacionDto> errores, string nombreArchivo)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Errores");

            // Cabeceras
            ws.Cell(1, 1).Value = "# Fila";
            ws.Cell(1, 2).Value = "Campo";
            ws.Cell(1, 3).Value = "Tipo Error";
            ws.Cell(1, 4).Value = "Descripción";
            ws.Cell(1, 5).Value = "Valor Recibido";

            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
            headerRow.Style.Font.FontColor = XLColor.White;
            headerRow.Height = 18;

            int row = 2;
            foreach (var e in errores)
            {
                ws.Cell(row, 1).Value = e.NumeroFila;
                ws.Cell(row, 2).Value = e.Campo ?? "";
                ws.Cell(row, 3).Value = e.TipoError;
                ws.Cell(row, 4).Value = e.Mensaje;
                ws.Cell(row, 5).Value = e.ValorRecibido ?? "";

                // Color alternado
                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#fffbeb");

                row++;
            }

            // Ajustar anchos
            ws.Columns(1, 5).AdjustToContents();
            if (ws.Column(4).Width > 80) ws.Column(4).Width = 80;

            // Freeze headers
            ws.SheetView.FreezeRows(1);

            wb.Properties.Title = $"Errores - {nombreArchivo}";
            wb.Properties.Author = "DataMedix";

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
