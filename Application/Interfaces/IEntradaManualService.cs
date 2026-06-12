using DataMedix.Application.DTOs.EntradaManual;
using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface IEntradaManualService
    {
        /// <summary>
        /// Crea o actualiza el snapshot mensual con los valores ingresados manualmente
        /// y regenera la prescripción sugerida (si no está aprobada).
        /// </summary>
        Task<ResultadoEntradaManualDto> ProcesarAsync(
            EntradaManualDto dto, Guid tenantId, Guid usuarioId);

        /// <summary>
        /// Devuelve el snapshot existente para un paciente/período, si existe.
        /// Usado para pre-llenar el formulario de edición.
        /// </summary>
        Task<SnapshotMensual?> GetSnapshotAsync(
            Guid tenantId, Guid pacienteId, int anio, int mes);
    }
}
