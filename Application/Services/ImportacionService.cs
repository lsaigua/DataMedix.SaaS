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

                    // Resolver parámetro clínico por alias exacto (sin fallback parcial para evitar
                    // que parámetros con nombre similar, como HCM, sobreescriban el valor de HB)
                    var aliasKey = fila.Parametro!.Trim().ToUpperInvariant();
                    mapaAliases.TryGetValue(aliasKey, out var parametro);
                    // Si no se reconoce, se guarda como dato crudo (ParametroClinicoId = null)
                    // Todos los registros se almacenan — solo los reconocidos contribuyen al snapshot

                    // Resolver paciente (cache en memoria durante el lote)
                    var ident = NormalizarIdentificacion(fila.Identificacion!);
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
                        ParametroClinicoId = parametro?.Id,
                        // Nav prop asignado para que GenerarSnapshotsAsync pueda leer el Codigo sin re-consultar
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
                        UnidadMedida = fila.UnidadMedida ?? parametro?.UnidadMedidaDefault,
                        ValorMinReferencia = parametro?.ValorMinReferencia,
                        ValorMaxReferencia = parametro?.ValorMaxReferencia,
                        CreatedBy = usuarioId
                    };
                    resultado.CalcularPatologia();
                    resultados.Add(resultado);
                }

                // 7. CRÍTICO: persistir pacientes nuevos antes del BulkInsert de resultados (FK paciente_id)
                await _unitOfWork.CommitAsync();

                // 8. Desactivar resultados anteriores del mismo paciente+período antes de insertar
                // (garantiza que solo la última carga es visible — sin duplicados históricos)
                if (resultados.Any())
                {
                    var pacientesEnLote = resultados.Select(r => r.PacienteId).Distinct().ToList();
                    var periodosEnLote  = resultados.Select(r => r.PeriodDate).Distinct().ToList();
                    await _resultadoRepo.DesactivarByPacientesYPeriodosAsync(
                        tenantId, pacientesEnLote, periodosEnLote);
                    await _resultadoRepo.BulkInsertAsync(resultados);
                }

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

            if (!grupos.Any()) return;

            // Pre-cargar historial previo en lote para carry-forward del panel de hierro
            // (Ferritina, ISAT y Hierro sérico se solicitan cada 2 meses — no HB)
            var todosLosIds  = grupos.Select(g => g.Key.PacienteId).Distinct().ToList();
            var periodoMinimo = grupos.Select(g => g.Key.PeriodDate).Min();
            var historialMap = await _snapshotRepo.GetHistorialByPacientesAsync(
                tenantId, todosLosIds, periodoMinimo, meses: 6);

            // Identificar el snapshot anterior más reciente por paciente y cargar sus
            // detalles en batch para el carry-forward (Potasio, Albúmina, Fósforo, etc.)
            var anteriorIdPorPaciente = new Dictionary<Guid, Guid>();
            foreach (var g in grupos)
            {
                historialMap.TryGetValue(g.Key.PacienteId, out var hist);
                var anterior = hist?
                    .Where(h => h.PeriodDate < g.Key.PeriodDate)
                    .OrderByDescending(h => h.PeriodDate)
                    .FirstOrDefault();
                if (anterior != null)
                    anteriorIdPorPaciente[g.Key.PacienteId] = anterior.Id;
            }
            var anteriorDetallesMap = await _snapshotRepo.GetDetallesBySnapshotIdsAsync(
                anteriorIdPorPaciente.Values);

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
                snapshot.PlanSalud    = primerRes.PlanSalud    ?? snapshot.PlanSalud;
                snapshot.TipoAtencion = primerRes.TipoAtencion ?? snapshot.TipoAtencion;
                snapshot.UpdatedAt    = DateTime.UtcNow;

                // Reset SOLO los parámetros presentes en esta carga para que una reimportación
                // parcial (p.ej. solo HB) no borre los valores previos (p.ej. ISAT/Ferritina
                // medidos en el mes anterior y ya guardados por carry-forward).
                var codigosPresentes = grupo
                    .Where(r => r.ParametroClinico != null)
                    .Select(r => r.ParametroClinico!.Codigo)
                    .ToHashSet();

                if (codigosPresentes.Contains("HB"))                                                           { snapshot.HbValor        = null; snapshot.HbUnidad        = null; }
                if (codigosPresentes.Contains("FE")   || codigosPresentes.Contains("HIERRO"))             { snapshot.HierroValor     = null; snapshot.HierroUnidad    = null; }
                if (codigosPresentes.Contains("FERR") || codigosPresentes.Contains("FERRITINA"))          { snapshot.FerritinaValor  = null; snapshot.FerritinaUnidad = null; }
                if (codigosPresentes.Contains("ISAT") || codigosPresentes.Contains("SATURACION"))         { snapshot.SaturacionValor = null; snapshot.SaturacionUnidad = null; }

                // Mapear parámetros clave por código (nav prop ParametroClinico asignado al crear)
                foreach (var res in grupo.Where(r => r.ParametroClinico != null))
                {
                    switch (res.ParametroClinico!.Codigo)
                    {
                        case "HB":
                            snapshot.HbValor         = res.ValorNumerico; snapshot.HbUnidad         = res.UnidadMedida; break;
                        case "FE" or "HIERRO":
                            snapshot.HierroValor     = res.ValorNumerico; snapshot.HierroUnidad     = res.UnidadMedida; break;
                        case "FERR" or "FERRITINA":
                            snapshot.FerritinaValor  = res.ValorNumerico; snapshot.FerritinaUnidad  = res.UnidadMedida; break;
                        case "ISAT" or "SATURACION":
                            snapshot.SaturacionValor = res.ValorNumerico; snapshot.SaturacionUnidad = res.UnidadMedida; break;
                    }
                }

                // ── Carry-forward del panel de hierro ──────────────────────────────────────
                // Si este mes no tiene FE/FERR/ISAT, tomar del snapshot anterior más reciente.
                // Clínico: Ferritina e ISAT se piden cada 2 meses en HD — es comportamiento normal.
                bool carryForwardIronUsado = false;
                if (!snapshot.HierroValor.HasValue || !snapshot.FerritinaValor.HasValue || !snapshot.SaturacionValor.HasValue)
                {
                    historialMap.TryGetValue(grupo.Key.PacienteId, out var hist);
                    var anteriorIron = hist?
                        .Where(h => h.PeriodDate < grupo.Key.PeriodDate &&
                                    (h.HierroValor.HasValue || h.FerritinaValor.HasValue || h.SaturacionValor.HasValue))
                        .OrderByDescending(h => h.PeriodDate)
                        .FirstOrDefault();
                    if (anteriorIron != null)
                    {
                        var antesHierro    = snapshot.HierroValor;
                        var antesFerritina = snapshot.FerritinaValor;
                        var antesSat       = snapshot.SaturacionValor;

                        snapshot.HierroValor    ??= anteriorIron.HierroValor;
                        snapshot.HierroUnidad   ??= anteriorIron.HierroUnidad;
                        snapshot.FerritinaValor  ??= anteriorIron.FerritinaValor;
                        snapshot.FerritinaUnidad ??= anteriorIron.FerritinaUnidad;
                        snapshot.SaturacionValor ??= anteriorIron.SaturacionValor;
                        snapshot.SaturacionUnidad ??= anteriorIron.SaturacionUnidad;

                        carryForwardIronUsado = antesHierro != snapshot.HierroValor
                            || antesFerritina != snapshot.FerritinaValor
                            || antesSat       != snapshot.SaturacionValor;
                    }
                }

                // Marcar si los datos del panel de hierro provienen del período anterior
                snapshot.EsDatosPeriodoAnterior = carryForwardIronUsado;

                snapshot.TieneDatosCompletos =
                    snapshot.HbValor.HasValue &&
                    snapshot.HierroValor.HasValue &&
                    snapshot.FerritinaValor.HasValue &&
                    snapshot.SaturacionValor.HasValue;

                await _snapshotRepo.UpsertAsync(snapshot);

                // ── Detalles del snapshot ──────────────────────────────────────────────────
                // 1. Detalles del mes actual (todos los parámetros de este lote)
                var idsPresentes   = new HashSet<Guid?>(grupo.Where(r => r.ParametroClinicoId.HasValue)
                                                              .Select(r => r.ParametroClinicoId));
                var rawsPresentes  = new HashSet<string>(
                    grupo.Where(r => !r.ParametroClinicoId.HasValue && r.ParametroRaw != null)
                         .Select(r => r.ParametroRaw!.Trim().ToUpperInvariant()),
                    StringComparer.OrdinalIgnoreCase);

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

                // 2. Carry-forward de detalles del mes anterior para parámetros faltantes
                //    (p.ej. Potasio, Albúmina, Fósforo que no vinieron en este lote)
                if (anteriorIdPorPaciente.TryGetValue(grupo.Key.PacienteId, out var anteriorId) &&
                    anteriorDetallesMap.TryGetValue(anteriorId, out var detAnt))
                {
                    foreach (var det in detAnt)
                    {
                        bool yaTiene = det.ParametroClinicoId.HasValue
                            ? idsPresentes.Contains(det.ParametroClinicoId)
                            : rawsPresentes.Contains(det.ParametroNombre?.Trim().ToUpperInvariant() ?? "\x00");

                        if (!yaTiene)
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
            var (primer, segundo, primerAp, segundoAp) = ParsearNombre(fila.NombrePaciente?.Trim() ?? "");
            return new Paciente
            {
                TenantId = tenantId,
                Identificacion = NormalizarIdentificacion(fila.Identificacion!),
                PrimerNombre = primer,
                SegundoNombre = segundo,
                PrimerApellido = primerAp,
                SegundoApellido = segundoAp,
                PlanSalud = fila.PlanSalud,
                TipoAtencion = fila.TipoAtencion,
                CreatedBy = usuarioId,
                Activo = true
            };
        }

        private static void ActualizarPacienteSiNecesario(Paciente paciente, LabRowDto fila)
        {
            var nombreNuevo = fila.NombrePaciente?.Trim() ?? "";
            if (!string.IsNullOrEmpty(nombreNuevo))
            {
                var (primer, segundo, primerAp, segundoAp) = ParsearNombre(nombreNuevo);
                paciente.PrimerNombre    = primer;
                paciente.SegundoNombre   = segundo;
                paciente.PrimerApellido  = primerAp;
                paciente.SegundoApellido = segundoAp;
            }
            if (!string.IsNullOrEmpty(fila.PlanSalud))    paciente.PlanSalud    = fila.PlanSalud;
            if (!string.IsNullOrEmpty(fila.TipoAtencion)) paciente.TipoAtencion = fila.TipoAtencion;
            var identNorm = NormalizarIdentificacion(fila.Identificacion!);
            if (paciente.Identificacion != identNorm) paciente.Identificacion = identNorm;
            paciente.UpdatedAt = DateTime.UtcNow;
        }

        private static string NormalizarIdentificacion(string ident)
        {
            var clean = ident.Trim();
            if (clean.Length > 0 && clean.All(char.IsDigit) && clean.Length < 10)
                clean = clean.PadLeft(10, '0');
            return clean;
        }

        private static (string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido)
            ParsearNombre(string nombre)
        {
            var p = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return p.Length switch
            {
                0 => ("SIN NOMBRE", null, "SIN APELLIDO", null),
                1 => (p[0], null, "SIN APELLIDO", null),
                2 => (p[0], null, p[1], null),
                3 => (p[0], p[1], p[2], null),
                4 => (p[0], p[1], p[2], p[3]),
                _ => (p[0], string.Join(" ", p[1..^2]), p[^2], p[^1])
            };
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
