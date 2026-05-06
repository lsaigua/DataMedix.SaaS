using DataMedix.Application.DTOs.HojaEpo;

namespace DataMedix.Application.Interfaces
{
    public interface IHojaEpoService
    {
        Task<List<HojaEpoRowDto>> GetMatrizAsync(Guid tenantId, HojaEpoFiltroDto filtro);
        Task GuardarAjusteAsync(Guid tenantId, Guid pacienteId, DateTime periodDate,
            string? ajusteEpo, string? ajusteHierro, Guid medicoId, Guid? prescSugeridaId);
    }
}
