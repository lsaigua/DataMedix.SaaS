using ClosedXML.Excel;
using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;

namespace DataMedix.Infrastructure.Excel
{
    public class ExcelExporter : IExcelExporter
    {
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
