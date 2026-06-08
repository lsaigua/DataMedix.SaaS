using DataMedix.Application.DTOs;
using DataMedix.Application.DTOs.HojaEpo;
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
        byte[] ExportarHojaEpo(
            IEnumerable<HojaEpoRowDto> filas,
            IEnumerable<DateTime> meses,
            string periodo);
    }
}
