using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using System.Globalization;

namespace DataMedix.Application.Services
{
    /// <summary>
    /// Motor de importación completo:
    /// Excel → Staging → Validación → Normalización → Resultados → Snapshot → Prescripción
    /// </summary>
    public class ImportacionService
    {
        private readonly IExcelReader _excelReader;
        private readonly ILoteImportacionRepository _loteRepo;
        private readonly IPacienteRepository _pacienteRepo;
        private readonly IParametroClinicoRepository _parametroRepo;
        private readonly IResultadoLaboratorioRepository _resultadoRepo;
        private readonly ISnapshotMensualRepository _snapshotRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly PrescripcionService _prescripcionService;

        public ImportacionService(
            IExcelReader excelReader,
            ILoteImportacionRepository loteRepo,
            IPacienteRepository pacienteRepo,
            IParametroClinicoRepository parametroRepo,
            IResultadoLaboratorioRepository resultadoRepo,
            ISnapshotMensualRepository snapshotRepo,
            IUnitOfWork unitOfWork,
            PrescripcionService prescripcionService)
        {
            _excelReader = excelReader;
            _loteRepo = loteRepo;
            _pacienteRepo = pacienteRepo;
            _parametroRepo = parametroRepo;
            _resultadoRepo = resultadoRepo;
            _snapshotRepo = snapshotRepo;
            _unitOfWork = unitOfWork;
            _prescripcionService = prescripcionService;
        }

        // ─────────────────────────────────────────────────────────────────────
        // FASE 1: Previsualización (sin guardar nada)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<PreVisualizacionDto> PrevisualizarAsync(Stream fileStream, Guid tenantId)
        {
            var filas = await _excelReader.ReadAsync(fileStream);
            var errores = new List<ErrorImportacionDto>();
            var filasValidas = new List<LabRowDto>();

            foreach (var fila in filas)
            {
                var erroresFila = ValidarFila(fila);
                if (erroresFila.Any())
                    errores.AddRange(erroresFila);
                else
                    filasValidas.Add(fila);
            }

            return new PreVisualizacionDto
            {
                Filas = filas.Take(100).ToList(),
                Errores = errores.Take(200).ToList(),
                TotalFilas = filas.Count,
                FilasValidas = filasValidas.Count,
                FilasConError = errores.GroupBy(e => e.NumeroFila).Count()
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // FASE 2: Procesamiento completo
        // ─────────────────────────────────────────────────────────────────────
        public async Task<ImportacionResultadoDto> ProcesarAsync(
            Stream fileStream,
            string nombreArchivo,
            Guid tenantId,
            Guid usuarioId)
        {
            // 1. Leer Excel
            var filas = await _excelReader.ReadAsync(fileStream);
            if (!filas.Any())
                return Error(Guid.Empty, "El archivo no contiene datos.");

            // 2. Determinar período del lote (usar la fecha más frecuente)
            var periodDate = DeterminarPeriodo(filas);

            // 3. Crear lote y PERSISTIRLO antes de bulk insert (FK constraint)
            var lote = new LoteImportacion
            {
                TenantId = tenantId,
                NombreArchivo = nombreArchivo,
                NombreArchivoOriginal = nombreArchivo,
                PeriodoAnio = periodDate.Year,
                PeriodoMes = periodDate.Month,
                PeriodDate = periodDate,
                TotalFilas = filas.Count,
                CreatedBy = usuarioId,
                Estado = EstadoLote.Procesando,
                FechaInicioProceso = DateTime.UtcNow
            };
            await _loteRepo.AddAsync(lote);
            // CRÍTICO: guardar lote en BD antes de insertar detalles (FK lote_id)
            await _unitOfWork.CommitAsync();

            try
            {
                // 4. Cargar a staging
                var detalles = MapearDetalles(filas, lote.Id, tenantId, periodDate);
                await _loteRepo.AddDetallesAsync(detalles);

                // 5. Cargar mapa de alias de parámetros (una sola consulta O(1) lookup)
                var mapaAliases = await _parametroRepo.GetMapaAliasesAsync(tenantId);

                // 6. Procesar filas válidas: resolver pacientes y construir resultados
                var errores = new List<ImportacionError>();
                var resultados = new List<ResultadoLaboratorio>();
                var pacientesCache = new Dictionary<string, Paciente>(StringComparer.OrdinalIgnoreCase);

                foreach (var fila in filas)
                {
                    var erroresFila = ValidarFila(fila);
                    if (erroresFila.Any())
                    {
                        foreach (var e in erroresFila)
                            errores.Add(new ImportacionError
                            {
                                LoteId = lote.Id,
                                NumeroFila = fila.LineNumber,
                                Campo = e.Campo,
                                TipoError = e.TipoError,
                                Mensaje = e.Mensaje,
                                ValorRecibido = e.ValorRecibido
                            });
                        continue;
                    }

                    // Resolver parámetro clínico por alias (primero, para filtrar filas irrelevantes)
                    var aliasKey = fila.Parametro!.Trim().ToUpperInvariant();
                    mapaAliases.TryGetValue(aliasKey, out var parametro);

                    if (parametro == null)
                    {
                        // Búsqueda parcial como fallback
                        var aliasEntry = mapaAliases.Keys.FirstOrDefault(k =>
                            k.Contains(aliasKey) || aliasKey.Contains(k));
                        if (aliasEntry != null) mapaAliases.TryGetValue(aliasEntry, out parametro);
                    }

                    if (parametro == null)
                    {
                        errores.Add(new ImportacionError
                        {
                            LoteId = lote.Id,
                            NumeroFila = fila.LineNumber,
                            Campo = "Parametro",
                            TipoError = TipoErrorImportacion.ParametroDesconocido,
                            Mensaje = $"Parámetro no reconocido: '{fila.Parametro}'",
                            ValorRecibido = fila.Parametro
                        });
                        continue;
                    }

                    // Resolver paciente (cache en memoria durante el lote)
                    var ident = fila.Identificacion!.Trim();
                    if (!pacientesCache.TryGetValue(ident, out var paciente))
                    {
                        paciente = await _pacienteRepo.GetByIdentificacionAsync(tenantId, ident);
                        if (paciente == null)
                        {
                            paciente = CrearPaciente(fila, tenantId, usuarioId);
                            await _pacienteRepo.AddAsync(paciente);
                        }
                        else
                        {
                            ActualizarPacienteSiNecesario(paciente, fila);
                        }
                        pacientesCache[ident] = paciente;
                    }

                    // Parsear valor numérico
                    decimal? valorNumerico = null;
                    if (decimal.TryParse(fila.ResultadoTexto,
                        NumberStyles.Any, CultureInfo.InvariantCulture, out var vn))
                        valorNumerico = vn;

                    var resultado = new ResultadoLaboratorio
                    {
                        TenantId = tenantId,
                        PacienteId = paciente.Id,
                        LoteId = lote.Id,
                        ParametroClinicoId = parametro.Id,
                        // CRÍTICO: asignar nav prop para que GenerarSnapshotsAsync pueda leer el Codigo
                        ParametroClinico = parametro,
                        PeriodDate = fila.PeriodDate,
                        PeriodoAnio = fila.PeriodDate.Year,
                        PeriodoMes = fila.PeriodDate.Month,
                        PlanSalud = fila.PlanSalud,
                        TipoAtencion = fila.TipoAtencion,
                        FechaOrden = fila.FechaOrden,
                        ExamenRaw = fila.Examen,
                        ParametroRaw = fila.Parametro,
                        ResultadoTexto = fila.ResultadoTexto!,
                        ValorNumerico = valorNumerico,
                        UnidadMedida = fila.UnidadMedida ?? parametro.UnidadMedidaDefault,
                        ValorMinReferencia = parametro.ValorMinReferencia,
                        ValorMaxReferencia = parametro.ValorMaxReferencia,
                        CreatedBy = usuarioId
                    };
                    resultado.CalcularPatologia();
                    resultados.Add(resultado);
                }

                // 7. CRÍTICO: persistir pacientes nuevos antes del BulkInsert de resultados (FK paciente_id)
                await _unitOfWork.CommitAsync();

                // 8. BulkInsert resultados (ahora los pacientes ya existen en BD)
                if (resultados.Any())
                    await _resultadoRepo.BulkInsertAsync(resultados);

                // 9. BulkInsert errores
                if (errores.Any())
                    await _loteRepo.AddErroresAsync(errores);

                // 10. Generar snapshots mensuales por paciente+período
                await GenerarSnapshotsAsync(resultados, tenantId);

                // 11. Generar prescripciones sugeridas automáticamente (motor clínico KDIGO)
                await _prescripcionService.GenerarParaPeriodoAsync(tenantId, periodDate);

                // 12. Actualizar estadísticas del lote
                lote.FilasValidas = resultados.Count;
                lote.FilasError = errores.Select(e => e.NumeroFila).Distinct().Count();
                lote.FilasDuplicadas = 0;
                lote.CompletarProcesamiento();
                await _loteRepo.UpdateAsync(lote);
                await _unitOfWork.CommitAsync();

                return new ImportacionResultadoDto
                {
                    LoteId = lote.Id,
                    Estado = lote.Estado,
                    TotalFilas = lote.TotalFilas,
                    FilasValidas = lote.FilasValidas,
                    FilasError = lote.FilasError,
                    FilasDuplicadas = lote.FilasDuplicadas,
                    Errores = errores.Select(e => new ErrorImportacionDto
                    {
                        NumeroFila = e.NumeroFila ?? 0,
                        Campo = e.Campo,
                        TipoError = e.TipoError,
                        Mensaje = e.Mensaje,
                        ValorRecibido = e.ValorRecibido
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                lote.MarcarError(ex.Message);
                await _loteRepo.UpdateAsync(lote);
                try { await _unitOfWork.CommitAsync(); } catch { /* absorb secondary failure */ }

                return Error(lote.Id, $"Error durante el procesamiento: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // SNAPSHOT MENSUAL por paciente+período
        // ─────────────────────────────────────────────────────────────────────
        private async Task GenerarSnapshotsAsync(
            List<ResultadoLaboratorio> resultados,
            Guid tenantId)
        {
            // Agrupar por paciente + período
            var grupos = resultados
                .GroupBy(r => new { r.PacienteId, r.PeriodDate })
                .ToList();

            foreach (var grupo in grupos)
            {
                var snapshot = await _snapshotRepo.GetByPacienteYPeriodoAsync(
                    tenantId, grupo.Key.PacienteId, grupo.Key.PeriodDate)
                    ?? new SnapshotMensual
                    {
                        TenantId = tenantId,
                        PacienteId = grupo.Key.PacienteId,
                        PeriodDate = grupo.Key.PeriodDate,
                        PeriodoAnio = grupo.Key.PeriodDate.Year,
                        PeriodoMes = grupo.Key.PeriodDate.Month,
                        LoteId = grupo.First().LoteId
                    };

                var primerRes = grupo.First();
                snapshot.PlanSalud ??= primerRes.PlanSalud;
                snapshot.TipoAtencion ??= primerRes.TipoAtencion;
                snapshot.UpdatedAt = DateTime.UtcNow;

                // Mapear parámetros clave por código (nav prop ParametroClinico asignado al crear)
                foreach (var res in grupo.Where(r => r.ParametroClinico != null))
                {
                    switch (res.ParametroClinico!.Codigo)
                    {
                        case "HB":
                            snapshot.HbValor = res.ValorNumerico;
                            snapshot.HbUnidad = res.UnidadMedida;
                            break;
                        case "FE":
                            snapshot.HierroValor = res.ValorNumerico;
                            snapshot.HierroUnidad = res.UnidadMedida;
                            break;
                        case "FERR":
                            snapshot.FerritinaValor = res.ValorNumerico;
                            snapshot.FerritinaUnidad = res.UnidadMedida;
                            break;
                        case "ISAT":
                            snapshot.SaturacionValor = res.ValorNumerico;
                            snapshot.SaturacionUnidad = res.UnidadMedida;
                            break;
                    }
                }

                snapshot.TieneDatosCompletos =
                    snapshot.HbValor.HasValue &&
                    snapshot.HierroValor.HasValue &&
                    snapshot.FerritinaValor.HasValue &&
                    snapshot.SaturacionValor.HasValue;

                await _snapshotRepo.UpsertAsync(snapshot);

                // Detalles: un registro por cada parámetro del grupo
                var detalles = grupo.Select(r => new SnapshotMensualDetalle
                {
                    SnapshotId = snapshot.Id,
                    ParametroClinicoId = r.ParametroClinicoId,
                    ParametroNombre = r.ParametroRaw ?? r.ParametroClinico?.Nombre,
                    ValorTexto = r.ResultadoTexto,
                    ValorNumerico = r.ValorNumerico,
                    UnidadMedida = r.UnidadMedida,
                    EsPatologico = r.EsPatologico
                }).ToList();

                await _snapshotRepo.AddDetallesAsync(detalles);
            }

            await _unitOfWork.CommitAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private static DateTime DeterminarPeriodo(List<LabRowDto> filas)
        {
            var fechas = filas
                .Where(f => f.FechaOrden.HasValue)
                .Select(f => f.PeriodDate)
                .ToList();

            if (!fechas.Any())
                return new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return fechas.GroupBy(f => f).OrderByDescending(g => g.Count()).First().Key;
        }

        private static List<ImportacionDetalle> MapearDetalles(
            List<LabRowDto> filas, Guid loteId, Guid tenantId, DateTime periodDate)
        {
            return filas.Select(f => new ImportacionDetalle
            {
                LoteId = loteId,
                TenantId = tenantId,
                NumeroFila = f.LineNumber,
                FechaOrdenRaw = f.FechaOrden?.ToString("dd/MM/yyyy"),
                PlanSaludRaw = f.PlanSalud,
                TipoAtencionRaw = f.TipoAtencion,
                IdentificacionRaw = f.Identificacion,
                PacienteRaw = f.NombrePaciente,
                ExamenRaw = f.Examen,
                ParametroRaw = f.Parametro,
                ResultadoRaw = f.ResultadoTexto,
                UnidadMedidaRaw = f.UnidadMedida,
                PeriodDate = f.PeriodDate
            }).ToList();
        }

        private static List<ErrorImportacionDto> ValidarFila(LabRowDto fila)
        {
            var errores = new List<ErrorImportacionDto>();

            if (!fila.TieneIdentificacion)
                errores.Add(new ErrorImportacionDto
                {
                    NumeroFila = fila.LineNumber,
                    Campo = "Identificacion",
                    TipoError = TipoErrorImportacion.Requerido,
                    Mensaje = "La identificación del paciente es requerida."
                });

            if (!fila.FechaOrden.HasValue)
                errores.Add(new ErrorImportacionDto
                {
                    NumeroFila = fila.LineNumber,
                    Campo = "FechaOrden",
                    TipoError = TipoErrorImportacion.Requerido,
                    Mensaje = "La fecha de orden es requerida."
                });

            if (!fila.TieneParametro)
                errores.Add(new ErrorImportacionDto
                {
                    NumeroFila = fila.LineNumber,
                    Campo = "Parametro",
                    TipoError = TipoErrorImportacion.Requerido,
                    Mensaje = "El nombre del parámetro es requerido."
                });

            if (!fila.TieneResultado)
                errores.Add(new ErrorImportacionDto
                {
                    NumeroFila = fila.LineNumber,
                    Campo = "Resultado",
                    TipoError = TipoErrorImportacion.Requerido,
                    Mensaje = "El resultado es requerido."
                });

            return errores;
        }

        private static Paciente CrearPaciente(LabRowDto fila, Guid tenantId, Guid usuarioId)
        {
            var nombre = fila.NombrePaciente?.Trim() ?? "";
            var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return new Paciente
            {
                TenantId = tenantId,
                Identificacion = fila.Identificacion!.Trim(),
                PrimerNombre = partes.Length > 0 ? partes[0] : "SIN NOMBRE",
                PrimerApellido = partes.Length > 1 ? partes[^1] : "SIN APELLIDO",
                PlanSalud = fila.PlanSalud,
                TipoAtencion = fila.TipoAtencion,
                CreatedBy = usuarioId,
                Activo = true
            };
        }

        private static void ActualizarPacienteSiNecesario(Paciente paciente, LabRowDto fila)
        {
            if (string.IsNullOrEmpty(paciente.PlanSalud) && !string.IsNullOrEmpty(fila.PlanSalud))
                paciente.PlanSalud = fila.PlanSalud;
            if (string.IsNullOrEmpty(paciente.TipoAtencion) && !string.IsNullOrEmpty(fila.TipoAtencion))
                paciente.TipoAtencion = fila.TipoAtencion;
            paciente.UpdatedAt = DateTime.UtcNow;
        }

        private static ImportacionResultadoDto Error(Guid loteId, string mensaje) =>
            new()
            {
                LoteId = loteId,
                Estado = "ERROR",
                MensajeError = mensaje
            };
    }
}
