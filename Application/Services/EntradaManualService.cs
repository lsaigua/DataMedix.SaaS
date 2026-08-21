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

            // Turno de sesión. El cronograma lo lee de snapshot.PlanSalud para
            // saber en qué días hay diálisis; si no se guarda, el paciente sale
            // en la grilla pero sin días ni totales, que es lo que pasaba antes.
            var turno = TurnoDialisis.Normalizar(dto.Turno)
                        ?? TurnoDialisis.Normalizar(paciente.PlanSalud);

            if (!string.IsNullOrWhiteSpace(turno))
                snapshot.PlanSalud = turno;

            var tipoAtencion = dto.TipoAtencion ?? paciente.TipoAtencion;
            if (!string.IsNullOrWhiteSpace(tipoAtencion))
                snapshot.TipoAtencion = tipoAtencion;

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

            // Cargar historial una sola vez — se usa para carry-forward del panel de hierro
            // Y también para carry-forward de detalles (Potasio, Albúmina, etc.)
            var historial = await _snapshotRepo.GetHistorialAsync(tenantId, dto.PacienteId, 6);

            // Carry-forward del panel de hierro si no se proporcionó este mes
            // (Ferritina, TSAT y Hierro sérico se piden cada 2 meses en HD)
            if (!snapshot.HierroValor.HasValue || !snapshot.FerritinaValor.HasValue || !snapshot.SaturacionValor.HasValue)
            {
                var anterior = historial
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

            // Detalles del mes actual (valores explícitamente ingresados en el formulario)
            var detalles = BuildDetalles(dto, snapshot.Id);

            // Carry-forward de detalles del mes anterior para parámetros no ingresados
            // (p.ej. Potasio, Albúmina que no se ingresaron este mes — alimentan alertas clínicas)
            var anteriorParaDetalles = historial
                .Where(h => h.PeriodDate < periodDate)
                .OrderByDescending(h => h.PeriodDate)
                .FirstOrDefault();

            if (anteriorParaDetalles != null)
            {
                var mapaDetallesAnt = await _snapshotRepo.GetDetallesBySnapshotIdsAsync(
                    new[] { anteriorParaDetalles.Id });

                if (mapaDetallesAnt.TryGetValue(anteriorParaDetalles.Id, out var detAnt))
                {
                    var nombresActuales = new HashSet<string>(
                        detalles.Select(d => d.ParametroNombre?.Trim().ToUpperInvariant() ?? "\x00"),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var det in detAnt.Where(d => !string.IsNullOrEmpty(d.ParametroNombre)))
                    {
                        var clave = det.ParametroNombre!.Trim().ToUpperInvariant();
                        if (!nombresActuales.Contains(clave))
                        {
                            detalles.Add(new SnapshotMensualDetalle
                            {
                                SnapshotId         = snapshot.Id,
                                ParametroClinicoId = det.ParametroClinicoId,
                                ParametroNombre    = det.ParametroNombre,
                                ValorTexto         = det.ValorTexto,
                                ValorNumerico      = det.ValorNumerico,
                                UnidadMedida       = det.UnidadMedida,
                                EsPatologico       = det.EsPatologico
                            });
                        }
                    }
                }
            }

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

        public async Task<EntradaManualDto?> GetDatosPeriodoAsync(
            Guid tenantId, Guid pacienteId, int anio, int mes)
        {
            var snapshot = await GetSnapshotAsync(tenantId, pacienteId, anio, mes);
            if (snapshot is null) return null;

            var dto = new EntradaManualDto
            {
                PacienteId      = pacienteId,
                PeriodoAnio     = anio,
                PeriodoMes      = mes,
                Turno           = TurnoDialisis.Normalizar(snapshot.PlanSalud),
                TipoAtencion    = snapshot.TipoAtencion,
                HbValor         = snapshot.HbValor,
                HbUnidad        = snapshot.HbUnidad         ?? "g/dL",
                HierroValor     = snapshot.HierroValor,
                HierroUnidad    = snapshot.HierroUnidad     ?? "µg/dL",
                FerritinaValor  = snapshot.FerritinaValor,
                FerritinaUnidad = snapshot.FerritinaUnidad  ?? "ng/mL",
                SaturacionValor = snapshot.SaturacionValor,
                SaturacionUnidad= snapshot.SaturacionUnidad ?? "%",
                EsEdicion       = true
            };

            // Potasio, albúmina y peso no están en el snapshot sino en su detalle:
            // sin esto el formulario los mostraba vacíos aunque estuvieran guardados.
            var mapa = await _snapshotRepo.GetDetallesBySnapshotIdsAsync(new[] { snapshot.Id });

            if (mapa.TryGetValue(snapshot.Id, out var detalles))
            {
                decimal? Buscar(string nombre) => detalles
                    .FirstOrDefault(d => string.Equals(d.ParametroNombre?.Trim(), nombre,
                                                       StringComparison.OrdinalIgnoreCase))
                    ?.ValorNumerico;

                dto.PotasioValor  = Buscar("POTASIO");
                dto.AlbuminaValor = Buscar("ALBUMINA");
                dto.PesoKgValor   = Buscar("PESO");
            }

            return dto;
        }

        public async Task<string?> GetTurnoSugeridoAsync(
            Guid tenantId, Guid pacienteId, int anio, int mes)
        {
            var periodDate = new DateTime(anio, mes, 1);

            var actual = await _snapshotRepo.GetByPacienteYPeriodoAsync(tenantId, pacienteId, periodDate);
            var turno  = TurnoDialisis.Normalizar(actual?.PlanSalud);
            if (turno is not null) return turno;

            // Sin turno este mes, se hereda del más reciente que sí lo tenga
            var historial = await _snapshotRepo.GetHistorialAsync(tenantId, pacienteId, 12);

            turno = historial
                .Where(h => h.PeriodDate <= periodDate)
                .OrderByDescending(h => h.PeriodDate)
                .Select(h => TurnoDialisis.Normalizar(h.PlanSalud))
                .FirstOrDefault(t => t is not null);

            if (turno is not null) return turno;

            var paciente = await _pacienteRepo.GetByIdAsync(tenantId, pacienteId);
            return TurnoDialisis.Normalizar(paciente?.PlanSalud);
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
