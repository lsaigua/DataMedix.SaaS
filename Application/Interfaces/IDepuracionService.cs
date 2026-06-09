using DataMedix.Application.DTOs;

namespace DataMedix.Application.Interfaces
{
    public interface IDepuracionService
    {
        /// <summary>Cuenta los registros que se eliminarán para el período dado.</summary>
        Task<DepuracionConteoDto> ContarDatosPeriodoAsync(Guid tenantId, int año, int mes);

        /// <summary>
        /// Elimina físicamente todos los datos clínicos y de importación del período.
        /// Retorna el total de registros eliminados.
        /// Orden: PrescripcionFinal → PrescripcionSugerida → SnapshotDetalle → Snapshot
        ///        → ResultadoLaboratorio → ImportacionError → ImportacionDetalle → Lote
        /// </summary>
        Task<int> EliminarPeriodoAsync(Guid tenantId, int año, int mes, Guid usuarioId);
    }
}
