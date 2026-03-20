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

            var rows = worksheet.RangeUsed().RowsUsed().Skip(1);

            foreach (var row in rows)
            {
                var dto = new LabRowDto
                {
                    LineNumber = row.RowNumber(),
                    FechaOrden = row.Cell(1).GetDateTime(),
                    PlanSalud = row.Cell(2).GetString().Trim(),
                    TipoAtencion = row.Cell(3).GetString().Trim(),
                    Identificacion = row.Cell(4).GetString().Trim(),
                    PrimerNombre = row.Cell(5).GetString().Trim(),
                    Examen = row.Cell(6).GetString().Trim(),
                    Parametro = row.Cell(7).GetString().Trim(),
                    ResultadoTexto = row.Cell(8).GetString().Trim(),
                    UnidadMedidad = row.Cell(9).GetString().Trim(),
                    FechaExamen = row.Cell(1).GetDateTime()
                  
                   
                };

                resultados.Add(dto);
            }

            return await Task.FromResult(resultados);
        }
    }
}
