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
        /// <summary>Inserta en lote los cronogramas nuevos y persiste cambios tracked (1 SaveChanges total).</summary>
        Task UpsertBatchAsync(List<CronogramaMedicamento> nuevos);
        /// <summary>Elimina todas las dias de los cronogramas indicados y las reemplaza con las nuevas (2 queries totales).</summary>
        Task BatchReplaceDiasAsync(List<Guid> cronogramaIds, List<CronogramaDia> nuevasDias);
    }

    public interface IConfiguracionMedicamentoRepository
    {
        Task<List<ConfiguracionMedicamento>> GetByTenantAsync(Guid tenantId);
        Task<ConfiguracionMedicamento?> GetByMedicamentoAsync(Guid tenantId, string medicamento);
        Task UpsertAsync(ConfiguracionMedicamento config);
    }

    /// <summary>Precios de EPO por dosis administrada; extensible sin cambio de código.</summary>
    public interface IPrecioEpoDosisRepository
    {
        Task<List<PrecioEpoDosis>> GetByTenantAsync(Guid tenantId);
        Task UpsertManyAsync(List<PrecioEpoDosis> precios);
    }

    /// <summary>
    /// Eventos formales de dosis pendiente (Fase 2). Registra fecha sugerida,
    /// dosis UI y estado (Programada / Aplicada / Suspendida).
    /// </summary>
    public interface IEventoDosisPendienteRepository
    {
        Task<EventoDosisPendiente?> GetByCronogramaAsync(Guid tenantId, Guid cronogramaId);
        Task<List<EventoDosisPendiente>> GetByPeriodoAsync(Guid tenantId, int anio, int mes);
        Task UpsertAsync(EventoDosisPendiente evento);
        Task DeleteByCronogramaIdsAsync(IEnumerable<Guid> cronogramaIds);
        Task ActualizarEstadoAsync(Guid id, string estado, Guid? usuarioId);
    }
}
