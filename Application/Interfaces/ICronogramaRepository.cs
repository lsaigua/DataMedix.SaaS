using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface ICronogramaRepository
    {
        Task<CronogramaMedicamento?> GetByPacienteYPeriodoAsync(Guid tenantId, Guid pacienteId, int anio, int mes);
        Task<List<CronogramaMedicamento>> GetByPeriodoAsync(Guid tenantId, int anio, int mes);
        Task<CronogramaMedicamento?> GetConDiasAsync(Guid tenantId, Guid cronogramaId);
        Task<CronogramaMedicamento> UpsertAsync(CronogramaMedicamento cronograma);
        Task<CronogramaDia> UpsertDiaAsync(CronogramaDia dia);
        Task<List<CronogramaDia>> UpsertDiasAsync(List<CronogramaDia> dias);
        Task RegistrarAuditoriaAsync(CronogramaAuditoria entrada);
    }

    public interface IConfiguracionMedicamentoRepository
    {
        Task<List<ConfiguracionMedicamento>> GetByTenantAsync(Guid tenantId);
        Task<ConfiguracionMedicamento?> GetByMedicamentoAsync(Guid tenantId, string medicamento);
        Task UpsertAsync(ConfiguracionMedicamento config);
    }
}
