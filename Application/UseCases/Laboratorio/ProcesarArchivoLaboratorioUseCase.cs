using DataMedix.Application.DTOs;
using DataMedix.Application.Services;

namespace DataMedix.Application.UseCases.Laboratorio
{
    /// <summary>
    /// Use case facade para mantener compatibilidad con el portal.
    /// Delega toda la lógica al ImportacionService.
    /// </summary>
    public class ProcesarArchivoLaboratorioUseCase
    {
        private readonly ImportacionService _importacionService;

        public ProcesarArchivoLaboratorioUseCase(ImportacionService importacionService)
        {
            _importacionService = importacionService;
        }

        public Task<ImportacionResultadoDto> ProcesarAsync(
            Stream fileStream,
            string nombreArchivo,
            Guid tenantId,
            Guid usuarioId) =>
            _importacionService.ProcesarAsync(fileStream, nombreArchivo, tenantId, usuarioId);

        public Task<PreVisualizacionDto> PrevisualizarAsync(Stream fileStream, Guid tenantId) =>
            _importacionService.PrevisualizarAsync(fileStream, tenantId);
    }
}
