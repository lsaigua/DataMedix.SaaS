using DataMedix.Application.DTOs;

namespace DataMedix.Application.Interfaces
{
    public interface IReporteService
    {
        Task<ResumenClinicoDto> GetResumenPeriodoAsync(
            Guid tenantId, DateTime periodDate, string? planSalud = null);

        Task<List<string>> GetPlanesSaludAsync(Guid tenantId, DateTime periodDate);
    }
}
