using ClosedXML.Excel;
using DataMedix.Application.Interfaces;
using DataMedix.Application.DTOs;
using System.Globalization;

namespace DataMedix.Infrastructure.Excel
{
    public class ExcelReader : IExcelReader
    {
        public async Task<List<LabRowDto>> ReadAsync(Stream fileStream)
        {
            var resultados = new List<LabRowDto>();

            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet(1);
            var usedRange = worksheet.RangeUsed();

            if (usedRange == null) return resultados;

            var rows = usedRange.RowsUsed().Skip(1); // saltar cabecera

            foreach (var row in rows)
            {
                try
                {
                    var dto = new LabRowDto
                    {
                        LineNumber = row.RowNumber(),
                        FechaOrden = ParseDate(row.Cell(1)),
                        PlanSalud = row.Cell(2).GetString().Trim(),
                        TipoAtencion = row.Cell(3).GetString().Trim(),
                        Identificacion = row.Cell(4).GetString().Trim(),
                        NombrePaciente = row.Cell(5).GetString().Trim(),
                        Examen = row.Cell(6).GetString().Trim(),
                        Parametro = row.Cell(7).GetString().Trim(),
                        ResultadoTexto = row.Cell(8).GetString().Trim(),
                        UnidadMedida = row.Cell(9).GetString().Trim(),
                    };

                    // Normalizar campos vacíos a null
                    if (string.IsNullOrWhiteSpace(dto.PlanSalud)) dto.PlanSalud = null;
                    if (string.IsNullOrWhiteSpace(dto.TipoAtencion)) dto.TipoAtencion = null;
                    if (string.IsNullOrWhiteSpace(dto.Identificacion)) dto.Identificacion = null;
                    if (string.IsNullOrWhiteSpace(dto.NombrePaciente)) dto.NombrePaciente = null;
                    if (string.IsNullOrWhiteSpace(dto.Examen)) dto.Examen = null;
                    if (string.IsNullOrWhiteSpace(dto.Parametro)) dto.Parametro = null;
                    if (string.IsNullOrWhiteSpace(dto.ResultadoTexto)) dto.ResultadoTexto = null;
                    if (string.IsNullOrWhiteSpace(dto.UnidadMedida)) dto.UnidadMedida = null;

                    resultados.Add(dto);
                }
                catch
                {
                    // Fila con error de lectura - registrar como detalle con error
                    resultados.Add(new LabRowDto
                    {
                        LineNumber = row.RowNumber(),
                        Identificacion = null
                    });
                }
            }

            return await Task.FromResult(resultados);
        }

        private static DateTime? ParseDate(IXLCell cell)
        {
            try
            {
                if (cell.DataType == XLDataType.DateTime)
                    return DateTime.SpecifyKind(cell.GetDateTime(), DateTimeKind.Utc);

                var raw = cell.GetString().Trim();
                if (string.IsNullOrEmpty(raw)) return null;

                var formatos = new[] { "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" };
                if (DateTime.TryParseExact(raw, formatos, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var result))
                    return DateTime.SpecifyKind(result, DateTimeKind.Utc);

                if (DateTime.TryParse(raw, out result))
                    return DateTime.SpecifyKind(result, DateTimeKind.Utc);

                return null;
            }
            catch { return null; }
        }
    }
}
