using DataMedix.Application.DTOs;

namespace DataMedix.Application.Interfaces
{
    public interface IDepuracionService
    {
        /// <summary>Cuenta los registros que se eliminarán para el período dado.</summary>
        Task<DepuracionConteoDto> ContarDatosPeriodoAsync(Guid tenantId, int año, int mes);

        /// <summary>
        /// Elimina físicamente todos los datos clínicos, de importación y de
        /// cronograma del período. Retorna el total de registros eliminados.
        ///
        /// Antes de borrar cierra la facturación del período, que conserva su
        /// propia copia de los pacientes facturados. Los pacientes NO se borran:
        /// los que quedan sin ningún dato se dan de baja (Activo = false).
        ///
        /// Orden: cierre de facturación → PrescripcionFinal → PrescripcionSugerida
        ///        → SnapshotDetalle → Snapshot → ResultadoLaboratorio
        ///        → ImportacionError → ImportacionDetalle → Lote
        ///        → CronogramaAuditoria → EventoDosisPendiente → AplicacionHierro
        ///        → CronogramaDia → CronogramaMedicamento → baja de pacientes
        /// </summary>
        Task<int> EliminarPeriodoAsync(Guid tenantId, int año, int mes, Guid usuarioId);
    }
}
