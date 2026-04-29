using DataMedix.Application.DTOs;
using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface ILoteImportacionRepository
    {
        Task<LoteImportacion?> GetByIdAsync(Guid tenantId, Guid loteId);
        Task<List<LoteImportacion>> GetByTenantAsync(Guid tenantId, int pagina = 1, int tamano = 20);
        Task AddAsync(LoteImportacion lote);
        Task UpdateAsync(LoteImportacion lote);
        Task AddDetallesAsync(List<ImportacionDetalle> detalles);
        Task AddErroresAsync(List<ImportacionError> errores);
        Task<List<ImportacionError>> GetErroresByLoteAsync(Guid loteId);
        Task IgnorarErrorAsync(Guid errorId);
        Task CancelarLoteAsync(LoteImportacion lote);
    }

    public interface IParametroClinicoRepository
    {
        Task<List<ParametroClinico>> GetAllAsync(bool soloActivos = true);
        Task<ParametroClinico?> ResolverPorAliasAsync(string alias, Guid? tenantId = null);
        Task<Dictionary<string, ParametroClinico>> GetMapaAliasesAsync(Guid? tenantId = null);
    }

    public interface IResultadoLaboratorioRepository
    {
        Task BulkInsertAsync(List<ResultadoLaboratorio> resultados);
        Task<List<ResultadoLaboratorio>> GetByPacienteYPeriodoAsync(Guid tenantId, Guid pacienteId, DateTime periodDate);
        Task<List<ResultadoLaboratorio>> GetByLoteAsync(Guid loteId);
        Task<(List<ResultadoLaboratorio> Items, int Total)> GetPagedAsync(Guid tenantId, ResultadoFiltro filtro);
        Task<List<ResultadoLaboratorio>> GetForExportAsync(Guid tenantId, ResultadoFiltro filtro);
    }

    public interface ISnapshotMensualRepository
    {
        Task<SnapshotMensual?> GetByPacienteYPeriodoAsync(Guid tenantId, Guid pacienteId, DateTime periodDate);
        Task<SnapshotMensual?> GetUltimoVigenteAsync(Guid tenantId, Guid pacienteId, DateTime hasta);
        Task<List<SnapshotMensual>> GetHistorialAsync(Guid tenantId, Guid pacienteId, int meses = 12);
        Task<List<SnapshotMensual>> GetByPeriodoAsync(Guid tenantId, DateTime periodDate,
            string? busqueda = null, int pagina = 1, int tamano = 50, string? planSalud = null);
        Task<List<string>> GetPlanesSaludAsync(Guid tenantId, DateTime periodDate);
        Task UpsertAsync(SnapshotMensual snapshot);
        Task AddDetallesAsync(List<SnapshotMensualDetalle> detalles);
    }

    public interface IRangoPreescribaRepository
    {
        Task<List<RangoPrescriba>> GetByParametroAsync(Guid parametroId, Guid? tenantId = null);
        Task<RangoPrescriba?> BuscarRangoAplicableAsync(Guid parametroId, decimal valor, Guid? tenantId = null);
        Task<List<RangoPrescriba>> GetAllAsync(Guid? tenantId = null);
        Task UpsertAsync(RangoPrescriba rango);
    }

    public interface IPrescripcionRepository
    {
        Task<PrescripcionSugerida?> GetSugeridaByPacienteYPeriodoAsync(Guid tenantId, Guid pacienteId, DateTime periodDate);
        Task UpsertSugeridaAsync(PrescripcionSugerida prescripcion);
        Task<PrescripcionFinal?> GetFinalByPacienteYPeriodoAsync(Guid tenantId, Guid pacienteId, DateTime periodDate);
        Task AddFinalAsync(PrescripcionFinal prescripcion);
        Task UpdateFinalAsync(PrescripcionFinal prescripcion);
        Task<List<PrescripcionSugerida>> GetPendientesAsync(Guid tenantId, DateTime periodDate);
        Task<List<PrescripcionSugerida>> GetByPeriodoAsync(Guid tenantId, DateTime periodDate, string? busqueda = null);
    }

    public interface IAuditoriaRepository
    {
        Task RegistrarAsync(AuditoriaLog log);
    }
}
