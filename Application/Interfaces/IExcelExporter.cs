using DataMedix.Application.DTOs;
using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface IExcelExporter
    {
        byte[] GenerarErroresExcel(IEnumerable<ErrorImportacionDto> errores, string nombreArchivo);
        byte[] ExportarPrescripcionesMes(
            IEnumerable<PrescripcionSugerida> prescripciones,
            IEnumerable<SnapshotMensual> snapshots,
            string periodo);
    }
}
