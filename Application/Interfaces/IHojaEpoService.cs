using DataMedix.Application.DTOs.HojaEpo;

namespace DataMedix.Application.Interfaces
{
    public interface IHojaEpoService
    {
        Task<List<HojaEpoRowDto>> GetMatrizAsync(Guid tenantId, HojaEpoFiltroDto filtro);
        Task GuardarAjusteAsync(Guid tenantId, Guid pacienteId, DateTime periodDate,
            string? ajusteEpo, string? ajusteHierro, Guid medicoId, Guid? prescSugeridaId);

        /// <summary>
        /// Asigna el turno al paciente y lo propaga a los snapshots del rango
        /// visible.
        ///
        /// Se escriben ambos a propósito: el paciente es el dato maestro que
        /// heredan los períodos nuevos, pero el cronograma de un mes ya cargado
        /// lee el turno del snapshot de ese mes. Actualizar solo uno dejaría la
        /// prescripción y el cronograma mostrando turnos distintos.
        /// </summary>
        Task ActualizarTurnoAsync(Guid tenantId, Guid pacienteId, string? codigoTurno,
            IReadOnlyList<DateTime> periodos);
    }
}
