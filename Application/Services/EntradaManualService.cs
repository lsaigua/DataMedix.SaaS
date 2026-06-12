using DataMedix.Application.DTOs.EntradaManual;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;

namespace DataMedix.Application.Services
{
    /// <summary>
    /// Permite ingresar o editar resultados de laboratorio manualmente para pacientes
    /// que no envían archivo Excel. El flujo es idéntico al de importación:
    /// SnapshotMensual → PrescripcionSugerida → disponible en HojaEPO y Prescripciones.
    ///
    /// Los snapshots de origen manual se distinguen por LoteId = null.
    /// </summary>
    public class EntradaManualService : IEntradaManualService
    {
        private readonly IPacienteRepository         _pacienteRepo;
        private readonly ISnapshotMensualRepository  _snapshotRepo;
        private readonly IPrescripcionRepository     _prescripcionRepo;
        private readonly PrescripcionService         _prescripcionService;
        private readonly IUnitOfWork                 _uow;

        public EntradaManualService(
            IPacienteRepository        pacienteRepo,
            ISnapshotMensualRepository snapshotRepo,
            IPrescripcionRepository    prescripcionRepo,
            PrescripcionService        prescripcionService,
            IUnitOfWork                uow)
        {
            _pacienteRepo        = pacienteRepo;
            _snapshotRepo        = snapshotRepo;
            _prescripcionRepo    = prescripcionRepo;
            _prescripcionService = prescripcionService;
            _uow                 = uow;
        }

        public async Task<ResultadoEntradaManualDto> ProcesarAsync(
            EntradaManualDto dto, Guid tenantId, Guid usuarioId)
        {
            if (!dto.TieneAlgunValor)
                return ResultadoEntradaManualDto.Error("Debe ingresar al menos un resultado de laboratorio.");

            var paciente = await _pacienteRepo.GetByIdAsync(tenantId, dto.PacienteId);
            if (paciente is null)
                return ResultadoEntradaManualDto.Error("Paciente no encontrado.");

            var periodDate = new DateTime(dto.PeriodoAnio, dto.PeriodoMes, 1);

            // Obtener o crear snapshot para el período
            var snapshot = await _snapshotRepo.GetByPacienteYPeriodoAsync(
                               tenantId, dto.PacienteId, periodDate)
                           ?? new SnapshotMensual
                           {
                               TenantId    = tenantId,
                               PacienteId  = dto.PacienteId,
                               PeriodDate  = periodDate,
                               PeriodoAnio = dto.PeriodoAnio,
                               PeriodoMes  = dto.PeriodoMes,
                           };

            // Actualizar solo los campos que se ingresaron explícitamente
            if (dto.HbValor.HasValue)
            {
                snapshot.HbValor  = dto.HbValor;
                snapshot.HbUnidad = dto.HbUnidad ?? "g/dL";
            }
            if (dto.HierroValor.HasValue)
            {
                snapshot.HierroValor  = dto.HierroValor;
                snapshot.HierroUnidad = dto.HierroUnidad ?? "µg/dL";
            }
            if (dto.FerritinaValor.HasValue)
            {
                snapshot.FerritinaValor  = dto.FerritinaValor;
                snapshot.FerritinaUnidad = dto.FerritinaUnidad ?? "ng/mL";
            }
            if (dto.SaturacionValor.HasValue)
            {
                snapshot.SaturacionValor  = dto.SaturacionValor;
                snapshot.SaturacionUnidad = dto.SaturacionUnidad ?? "%";
            }

            // Carry-forward del panel de hierro si no se proporcionó este mes
            // (Ferritina, TSAT y Hierro sérico se piden cada 2 meses en HD)
            if (!snapshot.HierroValor.HasValue || !snapshot.FerritinaValor.HasValue || !snapshot.SaturacionValor.HasValue)
            {
                var historial = await _snapshotRepo.GetHistorialAsync(tenantId, dto.PacienteId, 6);
                var anterior  = historial
                    .Where(h => h.PeriodDate < periodDate &&
                                (h.HierroValor.HasValue || h.FerritinaValor.HasValue || h.SaturacionValor.HasValue))
                    .OrderByDescending(h => h.PeriodDate)
                    .FirstOrDefault();

                if (anterior != null)
                {
                    snapshot.HierroValor     ??= anterior.HierroValor;
                    snapshot.HierroUnidad    ??= anterior.HierroUnidad;
                    snapshot.FerritinaValor  ??= anterior.FerritinaValor;
                    snapshot.FerritinaUnidad ??= anterior.FerritinaUnidad;
                    snapshot.SaturacionValor  ??= anterior.SaturacionValor;
                    snapshot.SaturacionUnidad ??= anterior.SaturacionUnidad;
                    snapshot.EsDatosPeriodoAnterior = !dto.FerritinaValor.HasValue && !dto.SaturacionValor.HasValue;
                }
            }
            else
            {
                snapshot.EsDatosPeriodoAnterior = false;
            }

            // Parámetros adicionales: se guardan en SnapshotMensualDetalle
            snapshot.TieneDatosCompletos =
                snapshot.HbValor.HasValue &&
                snapshot.HierroValor.HasValue &&
                snapshot.FerritinaValor.HasValue &&
                snapshot.SaturacionValor.HasValue;

            // LoteId = null → origen manual (distingue de importación Excel)
            snapshot.LoteId    = null;
            snapshot.UpdatedAt = DateTime.UtcNow;

            // Guardar detalles adicionales si se proporcionaron
            var detalles = BuildDetalles(dto, snapshot.Id);

            await _snapshotRepo.UpsertAsync(snapshot);

            if (detalles.Count > 0)
                await _snapshotRepo.AddDetallesAsync(detalles);

            await _uow.SaveChangesAsync();

            // Regenerar prescripción (solo si está PENDIENTE o no existe)
            snapshot.Paciente = paciente; // Nav prop para MesesEnDialisis en el motor de reglas
            var prescExistente = await _prescripcionRepo.GetSugeridaByPacienteYPeriodoAsync(
                tenantId, dto.PacienteId, periodDate);

            bool bloqueada = prescExistente is { Estado: not null }
                             && prescExistente.Estado != EstadoPrescripcion.Pendiente;

            if (!bloqueada)
                await _prescripcionService.GenerarParaSnapshotAsync(snapshot, tenantId);

            return ResultadoEntradaManualDto.Ok(paciente.NombreCompleto, dto.PeriodoAnio, dto.PeriodoMes, bloqueada);
        }

        public async Task<SnapshotMensual?> GetSnapshotAsync(
            Guid tenantId, Guid pacienteId, int anio, int mes)
        {
            var periodDate = new DateTime(anio, mes, 1);
            return await _snapshotRepo.GetByPacienteYPeriodoAsync(tenantId, pacienteId, periodDate);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<SnapshotMensualDetalle> BuildDetalles(EntradaManualDto dto, Guid snapshotId)
        {
            var lista = new List<SnapshotMensualDetalle>();

            void Agregar(string nombre, decimal? valor)
            {
                if (!valor.HasValue) return;
                lista.Add(new SnapshotMensualDetalle
                {
                    SnapshotId     = snapshotId,
                    ParametroNombre = nombre,
                    ValorNumerico  = valor,
                    ValorTexto     = valor.Value.ToString("G"),
                });
            }

            Agregar("POTASIO",  dto.PotasioValor);
            Agregar("ALBUMINA", dto.AlbuminaValor);
            Agregar("PESO",     dto.PesoKgValor);

            return lista;
        }
    }
}
