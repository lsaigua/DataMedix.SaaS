using DataMedix.Application.DTOs;

namespace DataMedix.Application.Interfaces
{
    public interface IExcelExporter
    {
        byte[] GenerarErroresExcel(IEnumerable<ErrorImportacionDto> errores, string nombreArchivo);
    }
}
