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

        /// <summary>
        /// Formulario prellenado con lo que se guardó para ese paciente y período,
        /// incluidos los parámetros adicionales (potasio, albúmina, peso) que viven
        /// en el detalle del snapshot. Devuelve null si no hay nada cargado.
        /// </summary>
        Task<EntradaManualDto?> GetDatosPeriodoAsync(
            Guid tenantId, Guid pacienteId, int anio, int mes);

        /// <summary>
        /// Último turno conocido del paciente (del período pedido o del más
        /// reciente anterior), para no obligar a reescribirlo cada mes.
        /// </summary>
        Task<string?> GetTurnoSugeridoAsync(
            Guid tenantId, Guid pacienteId, int anio, int mes);
    }
}
