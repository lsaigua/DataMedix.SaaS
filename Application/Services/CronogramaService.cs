using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;

namespace DataMedix.Application.Services
{
    /// <summary>
    /// DTO interno para el paso de datos prescripción → cronograma.
    /// </summary>
    internal record DatosPacienteParaCronograma(
        Guid PacienteId,
        string NombreCompleto,
        string? PlanSalud,
        decimal? EpoUiSemana,
        decimal? HierroMgMes);

    /// <summary>
    /// Genera y gestiona el cronograma mensual de medicamentos (EPO e Hierro IV).
    ///
    /// Turnos:
    ///   LMV (Lunes/Miércoles/Viernes) → 1er, 2do, 3er LMV
    ///   MJS (Martes/Jueves/Sábado)    → 1er, 2do, 3er MJS
    ///
    /// Distribución EPO (UI semanales → UI por sesión):
    ///   2000  → solo día central (Mié / Jue)
    ///   4000  → primer + último día (2000 c/u)
    ///   6000  → 3 días (2000 c/u)
    ///   8000  → primer + último (4000 c/u)
    ///   12000 → 3 días (4000 c/u)
    ///   18000 → 3 días (6000 c/u)
    ///
    /// Distribución Hierro: HierroMgMes ÷ núm_sesiones_mes (redondeado a entero),
    /// aplicado equitativamente a CADA sesión del turno en el mes.
    /// </summary>
    public class CronogramaService
    {
        private readonly ICronogramaRepository _repo;
        private readonly IConfiguracionMedicamentoRepository _configRepo;
        private readonly IPrescripcionRepository _prescripcionRepo;
        private readonly ISnapshotMensualRepository _snapshotRepo;

        public CronogramaService(
            ICronogramaRepository repo,
            IConfiguracionMedicamentoRepository configRepo,
            IPrescripcionRepository prescripcionRepo,
            ISnapshotMensualRepository snapshotRepo)
        {
            _repo = repo;
            _configRepo = configRepo;
            _prescripcionRepo = prescripcionRepo;
            _snapshotRepo = snapshotRepo;
        }

        // ──────────────────────────────────────────────────────────────────────
        // LEER cronogramas del mes (sin regenerar)
        // ──────────────────────────────────────────────────────────────────────
        public Task<List<CronogramaMedicamento>> GetCronogramasMesAsync(Guid tenantId, int anio, int mes)
            => _repo.GetByPeriodoAsync(tenantId, anio, mes);

        // ──────────────────────────────────────────────────────────────────────
        // GENERAR / ACTUALIZAR — versión batch (8 queries vs. ~7.000 anterior).
        //
        // Flujo optimizado:
        //   1. 4 lecturas paralelas: prescripciones + snapshots del mes y anterior
        //   2. 1 lectura: cronogramas existentes con sus dias (para detectar ediciones manuales)
        //   3. Memoria: crear/actualizar entidades (sin queries)
        //   4. 1 SaveChanges: insertar nuevos cronogramas + actualizar existentes
        //   5. Memoria: generar dias, aplicando valores manuales donde corresponda
        //   6. 2 queries: DELETE dias viejos + INSERT nuevas en lote
        //   7. 1 lectura final: recargar cronogramas con Dias para mostrar en pantalla
        // ──────────────────────────────────────────────────────────────────────
        public async Task<List<CronogramaMedicamento>> GenerarOActualizarMesAsync(
            Guid tenantId, int anio, int mes, Guid? usuarioId = null, DateTime? fechaInicioFlex = null)
        {
            var periodoActual  = new DateTime(anio, mes, 1);
            var periodoAnterior = periodoActual.AddMonths(-1);

            // ── 1. Leer datos fuente ───────────────────────────────────────
            var prescripciones    = await _prescripcionRepo.GetByPeriodoBatchAsync(tenantId, periodoActual);
            var prescripcionesAnt = await _prescripcionRepo.GetByPeriodoBatchAsync(tenantId, periodoAnterior);
            var finalesAct        = await _prescripcionRepo.GetFinalByPeriodoBatchAsync(tenantId, periodoActual);
            var finalesAnt        = await _prescripcionRepo.GetFinalByPeriodoBatchAsync(tenantId, periodoAnterior);
            var snapshots         = await _snapshotRepo.GetByPeriodoAsync(tenantId, periodoActual,  tamano: 1000);
            var snapshotsAnt      = await _snapshotRepo.GetByPeriodoAsync(tenantId, periodoAnterior, tamano: 1000);

            var mapaPresc    = prescripciones.GroupBy(p => p.PacienteId).ToDictionary(g => g.Key, g => g.First());
            var mapaPresAnt  = prescripcionesAnt.GroupBy(p => p.PacienteId).ToDictionary(g => g.Key, g => g.First());
            var mapaFinalAct = finalesAct.GroupBy(f => f.PacienteId).ToDictionary(g => g.Key, g => g.First());
            var mapaFinalAnt = finalesAnt.GroupBy(f => f.PacienteId).ToDictionary(g => g.Key, g => g.First());
            var mapaSnap    = snapshots.GroupBy(s => s.PacienteId).ToDictionary(g => g.Key, g => g.First());
            var mapaSnapAnt = snapshotsAnt.GroupBy(s => s.PacienteId).ToDictionary(g => g.Key, g => g.First());

            var pacienteIds = mapaPresc.Keys
                .Union(mapaPresAnt.Keys).Union(mapaSnap.Keys).Union(mapaSnapAnt.Keys)
                .Distinct().ToList();

            if (pacienteIds.Count == 0) return new List<CronogramaMedicamento>();

            // ── 2. Cargar cronogramas existentes con sus Dias (1 query) ───
            var existentes    = await _repo.GetByPeriodoAsync(tenantId, anio, mes);
            var mapaExistentes = existentes.ToDictionary(c => c.PacienteId);

            // ── 3. Crear/actualizar entidades en memoria ───────────────────
            var paraInsertar    = new List<CronogramaMedicamento>();
            var todosCronogramas = new List<CronogramaMedicamento>();
            var ahora = DateTime.UtcNow;

            foreach (var pid in pacienteIds)
            {
                mapaPresc.TryGetValue(pid, out var presc);
                mapaPresAnt.TryGetValue(pid, out var prescAnt);
                mapaFinalAct.TryGetValue(pid, out var finalAct);
                mapaFinalAnt.TryGetValue(pid, out var finalAnt);
                mapaSnap.TryGetValue(pid, out var snap);
                mapaSnapAnt.TryGetValue(pid, out var snapAnt);

                var prescVigente = presc ?? prescAnt;
                var finalVigente = finalAct ?? finalAnt;
                var planSalud    = snap?.PlanSalud ?? snapAnt?.PlanSalud;

                // Dosis efectiva = calculada por reglas + ajuste médico (HojaEPO)
                var (epoEfectivo, hierroEfectivo) = ComputarEfectivos(prescVigente, finalVigente);

                var modoNuevo = fechaInicioFlex.HasValue ? ModoCronograma.Flexible : ModoCronograma.CalendarioMensual;

                if (mapaExistentes.TryGetValue(pid, out var existente))
                {
                    if (!string.IsNullOrWhiteSpace(planSalud))  existente.PlanSalud      = planSalud;
                    if (epoEfectivo.HasValue)                   existente.EpoUiSemana    = epoEfectivo;
                    if (hierroEfectivo.HasValue)                existente.HierroMgMes    = hierroEfectivo;
                    existente.Modo             = modoNuevo;
                    existente.FechaInicioFlex  = fechaInicioFlex.HasValue ? fechaInicioFlex.Value.Date : null;
                    existente.UpdatedAt        = ahora;
                    existente.UpdatedBy        = usuarioId;
                    todosCronogramas.Add(existente);
                }
                else
                {
                    var nuevo = new CronogramaMedicamento
                    {
                        TenantId         = tenantId,
                        PacienteId       = pid,
                        PeriodoAnio      = anio,
                        PeriodoMes       = mes,
                        PlanSalud        = planSalud,
                        EpoUiSemana      = epoEfectivo,
                        HierroMgMes      = hierroEfectivo,
                        Modo             = modoNuevo,
                        FechaInicioFlex  = fechaInicioFlex.HasValue ? fechaInicioFlex.Value.Date : null,
                        CreatedBy        = usuarioId,
                        UpdatedAt        = ahora,
                        UpdatedBy        = usuarioId
                    };
                    paraInsertar.Add(nuevo);
                    todosCronogramas.Add(nuevo);
                }
            }

            // ── 4. Persistir todo en 1 SaveChanges ────────────────────────
            await _repo.UpsertBatchAsync(paraInsertar);

            // ── 5. Generar dias en memoria para pacientes con turno ────────
            var cronogramaIdsConTurno = new List<Guid>();
            var todasNuevasDias       = new List<CronogramaDia>();

            foreach (var crono in todosCronogramas)
            {
                // Paciente ausente: borrar sus días existentes, no generar nuevos
                if (crono.Ausente)
                {
                    if (crono.Dias.Any())
                        cronogramaIdsConTurno.Add(crono.Id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(crono.PlanSalud) ||
                    (!(crono.EpoUiSemana > 0) && !(crono.HierroMgMes > 0)))
                    continue;

                cronogramaIdsConTurno.Add(crono.Id);
                var nuevasDias = GenerarDias(crono);

                // Preservar valores editados manualmente (crono.Dias viene del GetByPeriodoAsync anterior)
                if (crono.Dias.Any())
                {
                    var manuales = crono.Dias
                        .Where(d => d.EpoEditadoManual || d.HierroEditadoManual)
                        .ToDictionary(d => d.FechaSesion.Date);

                    foreach (var dia in nuevasDias)
                    {
                        if (!manuales.TryGetValue(dia.FechaSesion.Date, out var manual)) continue;
                        if (manual.EpoEditadoManual)
                        {
                            dia.EpoDosisuI = manual.EpoDosisuI;
                            dia.EpoEditadoManual = true;
                        }
                        if (manual.HierroEditadoManual)
                        {
                            dia.HierroDosismg = manual.HierroDosismg;
                            dia.HierroEditadoManual = true;
                        }
                        dia.Observacion = manual.Observacion;
                    }
                }

                todasNuevasDias.AddRange(nuevasDias);
            }

            // ── 6. Reemplazar dias en lote (DELETE + INSERT, 2 queries) ───
            await _repo.BatchReplaceDiasAsync(cronogramaIdsConTurno, todasNuevasDias);

            // ── 7. Recargar con Dias y Paciente para la UI (1 query) ──────
            return await _repo.GetByPeriodoAsync(tenantId, anio, mes);
        }

        // ──────────────────────────────────────────────────────────────────────
        // GENERAR cronograma de un paciente usando datos ya resueltos
        // ──────────────────────────────────────────────────────────────────────
        private async Task<CronogramaMedicamento> GenerarPorPacienteConDatosAsync(
            Guid tenantId, DatosPacienteParaCronograma datos, int anio, int mes, Guid? usuarioId)
        {
            var cronograma = await _repo.GetByPacienteYPeriodoAsync(tenantId, datos.PacienteId, anio, mes)
                ?? new CronogramaMedicamento
                {
                    TenantId = tenantId,
                    PacienteId = datos.PacienteId,
                    PeriodoAnio = anio,
                    PeriodoMes = mes,
                    CreatedBy = usuarioId
                };

            // Actualizar con datos frescos; preservar valores manuales si no hay nueva data
            if (!string.IsNullOrWhiteSpace(datos.PlanSalud))
                cronograma.PlanSalud = datos.PlanSalud;
            if (datos.EpoUiSemana.HasValue)
                cronograma.EpoUiSemana = datos.EpoUiSemana;
            if (datos.HierroMgMes.HasValue)
                cronograma.HierroMgMes = datos.HierroMgMes;

            cronograma.UpdatedBy = usuarioId;
            cronograma.UpdatedAt = DateTime.UtcNow;

            cronograma = await _repo.UpsertAsync(cronograma);

            // Solo generar días si hay turno y al menos una dosis configurada
            if (!string.IsNullOrWhiteSpace(cronograma.PlanSalud) &&
                (cronograma.EpoUiSemana > 0 || cronograma.HierroMgMes > 0))
            {
                var dias = GenerarDias(cronograma);
                await _repo.UpsertDiasAsync(dias);
            }

            return await _repo.GetConDiasAsync(tenantId, cronograma.Id) ?? cronograma;
        }

        // ──────────────────────────────────────────────────────────────────────
        // GENERAR cronograma de un paciente para un mes dado (API pública)
        // ──────────────────────────────────────────────────────────────────────
        public async Task<CronogramaMedicamento> GenerarPorPacienteAsync(
            Guid tenantId, Guid pacienteId, int anio, int mes, Guid? usuarioId = null)
        {
            var prescripcion = await ObtenerPrescripcionVigenteAsync(tenantId, pacienteId, anio, mes);
            var periodo = new DateTime(anio, mes, 1);
            var snapshot = await _snapshotRepo.GetByPacienteYPeriodoAsync(tenantId, pacienteId, periodo)
                ?? await _snapshotRepo.GetByPacienteYPeriodoAsync(tenantId, pacienteId, periodo.AddMonths(-1));

            var final = await _prescripcionRepo.GetFinalByPacienteYPeriodoAsync(tenantId, pacienteId, periodo)
                ?? await _prescripcionRepo.GetFinalByPacienteYPeriodoAsync(tenantId, pacienteId, periodo.AddMonths(-1));

            var (epoEfectivo, hierroEfectivo) = ComputarEfectivos(prescripcion, final);

            var datos = new DatosPacienteParaCronograma(
                PacienteId: pacienteId,
                NombreCompleto: snapshot?.Paciente?.NombreCompleto ?? "",
                PlanSalud: snapshot?.PlanSalud,
                EpoUiSemana: epoEfectivo,
                HierroMgMes: hierroEfectivo);

            return await GenerarPorPacienteConDatosAsync(tenantId, datos, anio, mes, usuarioId);
        }

        // ──────────────────────────────────────────────────────────────────────
        // EDITAR dosis de un día específico (con auditoría)
        // Los valores anteriores los pasa la UI (ya los tiene en memoria).
        // ──────────────────────────────────────────────────────────────────────
        public async Task EditarDiaAsync(
            Guid tenantId, Guid cronogramaId, Guid diaId,
            decimal? epoUi, decimal? hierroMg,
            decimal? anteriorEpo, decimal? anteriorHierro,
            string? observacion, Guid usuarioId)
        {
            var dia = new CronogramaDia
            {
                Id = diaId,
                CronogramaId = cronogramaId,
                TenantId = tenantId,
                EpoDosisuI = epoUi,
                HierroDosismg = hierroMg,
                EpoEditadoManual = epoUi.HasValue,
                HierroEditadoManual = hierroMg.HasValue,
                Observacion = observacion,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = usuarioId
            };

            await _repo.UpsertDiaAsync(dia);

            if (epoUi.HasValue)
                await _repo.RegistrarAuditoriaAsync(new CronogramaAuditoria
                {
                    TenantId = tenantId, CronogramaId = cronogramaId, DiaId = diaId,
                    UsuarioId = usuarioId, Accion = "EDICION_MANUAL", Campo = "EPO",
                    ValorAnterior = anteriorEpo?.ToString(), ValorNuevo = epoUi.ToString()
                });

            if (hierroMg.HasValue)
                await _repo.RegistrarAuditoriaAsync(new CronogramaAuditoria
                {
                    TenantId = tenantId, CronogramaId = cronogramaId, DiaId = diaId,
                    UsuarioId = usuarioId, Accion = "EDICION_MANUAL", Campo = "HIERRO",
                    ValorAnterior = anteriorHierro?.ToString(), ValorNuevo = hierroMg.ToString()
                });
        }

        // ──────────────────────────────────────────────────────────────────────
        // ACTUALIZAR TURNO Y REGENERAR días de un cronograma individual
        // ──────────────────────────────────────────────────────────────────────
        public async Task<CronogramaMedicamento> ActualizarTurnoYRegenerarAsync(
            Guid tenantId, Guid cronogramaId, string nuevoTurno, Guid? usuarioId = null)
        {
            var crono = await _repo.GetConDiasAsync(tenantId, cronogramaId);
            if (crono == null) throw new InvalidOperationException("Cronograma no encontrado");

            crono.PlanSalud = nuevoTurno.Trim();
            crono.UpdatedAt = DateTime.UtcNow;
            crono.UpdatedBy = usuarioId;

            // Persiste la actualización del PlanSalud (entidad tracked, 1 SaveChanges)
            await _repo.UpsertBatchAsync(new List<CronogramaMedicamento>());

            if (!crono.Ausente &&
                !string.IsNullOrWhiteSpace(crono.PlanSalud) &&
                (crono.EpoUiSemana > 0 || crono.HierroMgMes > 0))
            {
                var nuevasDias = GenerarDias(crono);

                // Preservar valores editados manualmente
                var manuales = crono.Dias
                    .Where(d => d.EpoEditadoManual || d.HierroEditadoManual)
                    .ToDictionary(d => d.FechaSesion.Date);

                foreach (var dia in nuevasDias)
                {
                    if (!manuales.TryGetValue(dia.FechaSesion.Date, out var manual)) continue;
                    if (manual.EpoEditadoManual) { dia.EpoDosisuI = manual.EpoDosisuI; dia.EpoEditadoManual = true; }
                    if (manual.HierroEditadoManual) { dia.HierroDosismg = manual.HierroDosismg; dia.HierroEditadoManual = true; }
                    dia.Observacion = manual.Observacion;
                }

                await _repo.BatchReplaceDiasAsync(new List<Guid> { cronogramaId }, nuevasDias);
            }

            return await _repo.GetConDiasAsync(tenantId, cronogramaId) ?? crono;
        }

        // ──────────────────────────────────────────────────────────────────────
        // MARCAR / DESMARCAR AUSENTE
        // ──────────────────────────────────────────────────────────────────────
        public async Task<CronogramaMedicamento> MarcarAusenteAsync(
            Guid tenantId, Guid cronogramaId, bool ausente, Guid? usuarioId = null)
        {
            var crono = await _repo.GetConDiasAsync(tenantId, cronogramaId);
            if (crono == null) throw new InvalidOperationException("Cronograma no encontrado");

            crono.Ausente = ausente;
            crono.UpdatedAt = DateTime.UtcNow;
            crono.UpdatedBy = usuarioId;

            await _repo.UpsertBatchAsync(new List<CronogramaMedicamento>());

            if (ausente)
            {
                // Eliminar todos los días programados del paciente ausente
                await _repo.BatchReplaceDiasAsync(new List<Guid> { cronogramaId }, new List<CronogramaDia>());
            }
            else if (!string.IsNullOrWhiteSpace(crono.PlanSalud) &&
                     (crono.EpoUiSemana > 0 || crono.HierroMgMes > 0))
            {
                // Reactivar: regenerar días respetando turno y dosis actuales
                var nuevasDias = GenerarDias(crono);
                await _repo.BatchReplaceDiasAsync(new List<Guid> { cronogramaId }, nuevasDias);
            }

            return await _repo.GetConDiasAsync(tenantId, cronogramaId) ?? crono;
        }

        // ──────────────────────────────────────────────────────────────────────
        // ACTUALIZAR SALA
        // ──────────────────────────────────────────────────────────────────────
        public async Task ActualizarSalaAsync(
            Guid tenantId, Guid cronogramaId, string? sala, Guid? usuarioId = null)
        {
            var crono = await _repo.GetConDiasAsync(tenantId, cronogramaId);
            if (crono == null) throw new InvalidOperationException("Cronograma no encontrado");

            crono.Sala      = string.IsNullOrWhiteSpace(sala) ? null : sala.Trim();
            crono.UpdatedAt = DateTime.UtcNow;
            crono.UpdatedBy = usuarioId;
            await _repo.UpsertBatchAsync(new List<CronogramaMedicamento>());
        }

        // ──────────────────────────────────────────────────────────────────────
        // CÁLCULO DE COSTOS
        // ──────────────────────────────────────────────────────────────────────
        public async Task<ResumenCostos> CalcularCostosAsync(
            Guid tenantId, int anio, int mes)
        {
            var cronogramas = await _repo.GetByPeriodoAsync(tenantId, anio, mes);
            var confEpo = await _configRepo.GetByMedicamentoAsync(tenantId, "EPO");
            var confHierro = await _configRepo.GetByMedicamentoAsync(tenantId, "HIERRO");

            decimal totalEpoUi = 0, totalHierroMg = 0;

            foreach (var c in cronogramas)
                foreach (var d in c.Dias)
                {
                    totalEpoUi += d.EpoDosisuI ?? 0;
                    totalHierroMg += d.HierroDosismg ?? 0;
                }

            return new ResumenCostos
            {
                TotalEpoUI = totalEpoUi,
                TotalHierroMg = totalHierroMg,
                CostoEpo = totalEpoUi * (confEpo?.PrecioUnitario ?? 0),
                CostoHierro = totalHierroMg * (confHierro?.PrecioUnitario ?? 0),
                PrecioEpoPorUI = confEpo?.PrecioUnitario ?? 0,
                PrecioHierroPorMg = confHierro?.PrecioUnitario ?? 0
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // ALGORITMO PRINCIPAL: genera la lista de días con sus dosis calculadas
        // ──────────────────────────────────────────────────────────────────────
        internal List<CronogramaDia> GenerarDias(CronogramaMedicamento cronograma)
        {
            var turno = ParsearTurno(cronograma.PlanSalud);
            if (turno == null)
                return new List<CronogramaDia>();

            var fechasSesion = ObtenerFechasSesion(
                cronograma.PeriodoAnio, cronograma.PeriodoMes, turno.Value, cronograma.FechaInicioFlex);
            var dosisEpoPorDia = DistribuirEpo(cronograma.EpoUiSemana, turno.Value, fechasSesion);
            var dosisHierroPorDia = DistribuirHierro(cronograma.HierroMgMes, fechasSesion);

            return fechasSesion.Select(fecha => new CronogramaDia
            {
                CronogramaId = cronograma.Id,
                TenantId = cronograma.TenantId,
                FechaSesion = fecha,
                EpoDosisuI = dosisEpoPorDia.TryGetValue(fecha, out var epo) ? epo : null,
                HierroDosismg = dosisHierroPorDia.TryGetValue(fecha, out var hierro) ? hierro : null
            }).ToList();
        }

        // ──────────────────────────────────────────────────────────────────────
        // HELPERS INTERNOS
        // ──────────────────────────────────────────────────────────────────────

        // Dosis efectiva = calculada por reglas (PrescripcionSugerida) +
        // ajuste médico (PrescripcionFinal), replicando HojaEpoCeldaDto.EpoEfectivo/HierroEfectivo.
        private static (decimal? epo, decimal? hierro) ComputarEfectivos(
            PrescripcionSugerida? sugerida, PrescripcionFinal? final)
        {
            var epoCalc    = sugerida?.EpoUiSemana;
            var hierroCalc = sugerida?.HierroMgMes;

            decimal? epoAj = null, hierroAj = null;
            if (final != null)
            {
                if (decimal.TryParse(final.EpoDosis,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var ev))
                    epoAj = ev;
                if (decimal.TryParse(final.HierroDosis,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var hv))
                    hierroAj = hv;
            }

            return (
                (epoCalc.HasValue || epoAj.HasValue) ? (epoCalc ?? 0) + (epoAj ?? 0) : null,
                (hierroCalc.HasValue || hierroAj.HasValue) ? (hierroCalc ?? 0) + (hierroAj ?? 0) : null
            );
        }

        private static TipoTurno? ParsearTurno(string? planSalud)
        {
            if (string.IsNullOrWhiteSpace(planSalud)) return null;
            var upper = planSalud.ToUpperInvariant();
            if (upper.Contains("LMV")) return TipoTurno.LMV;
            if (upper.Contains("MJS")) return TipoTurno.MJS;
            return null;
        }

        // fechaInicioFlex: si se provee, define el rango [fechaInicioFlex, fechaInicioFlex + 1 mes).
        // Si es null se usa el rango clásico [1ro del mes, fin del mes].
        private static List<DateTime> ObtenerFechasSesion(int anio, int mes, TipoTurno turno,
            DateTime? fechaInicioFlex = null)
        {
            var diasSemana = turno == TipoTurno.LMV
                ? new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }
                : new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday };

            var inicio = fechaInicioFlex.HasValue ? fechaInicioFlex.Value.Date : new DateTime(anio, mes, 1);
            var fin    = inicio.AddMonths(1);   // exclusivo: genera [inicio, fin)
            var fechas = new List<DateTime>();

            for (var d = inicio; d < fin; d = d.AddDays(1))
                if (diasSemana.Contains(d.DayOfWeek))
                    fechas.Add(d);

            return fechas;
        }

        private static Dictionary<DateTime, decimal> DistribuirEpo(
            decimal? epoUiSemana, TipoTurno turno, List<DateTime> fechasSesion)
        {
            var resultado = new Dictionary<DateTime, decimal>();
            if (!epoUiSemana.HasValue || epoUiSemana <= 0) return resultado;

            var semanas = AgruparPorSemana(fechasSesion);
            var ui = (int)Math.Round(epoUiSemana.Value);

            foreach (var semana in semanas)
            {
                var n = semana.Count;
                if (n == 0) continue;

                var (d1, d2, d3) = CalcularDosisEpoSemanal(ui);

                if (d1 == 0 && d2 > 0 && d3 == 0)
                {
                    // Solo día central (2000 UI/sem): usar el día del medio si hay 3, sino el disponible
                    resultado[n >= 2 ? semana[1] : semana[0]] = d2;
                }
                else if (n >= 3 && d3 > 0)
                {
                    // Semana completa con plan de 3 días
                    resultado[semana[0]] = d1;
                    if (d2 > 0) resultado[semana[1]] = d2;
                    resultado[semana[2]] = d3;
                }
                else if (n >= 2)
                {
                    // Semana parcial con 2 días: primer y último slot del plan
                    resultado[semana[0]] = d1;
                    resultado[semana[^1]] = d3 > 0 ? d3 : d2;
                }
                else
                {
                    // Semana parcial con 1 día (inicio o fin de mes): dosis del primer slot
                    resultado[semana[0]] = d1 > 0 ? d1 : d2;
                }
            }

            return resultado;
        }

        /// <summary>
        /// Retorna (dosis primer día, dosis día central, dosis último día).
        /// Si día central = 0, significa que no aplica ese slot.
        /// </summary>
        private static (decimal primero, decimal centro, decimal ultimo) CalcularDosisEpoSemanal(int ui)
        {
            return ui switch
            {
                2000  => (0,    2000, 0),     // solo centro
                4000  => (2000, 0,    2000),  // primero + último
                6000  => (2000, 2000, 2000),  // 3 días
                8000  => (4000, 0,    4000),  // primero + último
                12000 => (4000, 4000, 4000),  // 3 días
                18000 => (6000, 6000, 6000),  // 3 días
                _     => DistribuirEpoGenerico(ui)
            };
        }

        private static (decimal, decimal, decimal) DistribuirEpoGenerico(int ui)
        {
            // Fallback: si es múltiplo de 3 → 3 días; si no → 2 días (1er + último)
            if (ui % 3 == 0)
            {
                var parte = (decimal)ui / 3;
                return (parte, parte, parte);
            }
            else
            {
                var parte = (decimal)ui / 2;
                return (parte, 0, parte);
            }
        }

        private static Dictionary<DateTime, decimal> DistribuirHierro(
            decimal? hierroMgMes, List<DateTime> fechasSesion)
        {
            var resultado = new Dictionary<DateTime, decimal>();
            if (!hierroMgMes.HasValue || hierroMgMes <= 0 || fechasSesion.Count == 0)
                return resultado;

            // Distribuir igualmente entre todas las sesiones del turno en el mes.
            // dosisPorSesion = hierroMgMes / núm_sesiones, redondeado a entero.
            var sesiones = fechasSesion.OrderBy(f => f).ToList();
            decimal dosisPorSesion = Math.Round(hierroMgMes.Value / sesiones.Count, 0);
            if (dosisPorSesion <= 0) dosisPorSesion = 1m;

            foreach (var sesion in sesiones)
                resultado[sesion] = dosisPorSesion;

            return resultado;
        }

        private static List<List<DateTime>> AgruparPorSemana(List<DateTime> fechas)
        {
            if (fechas.Count == 0) return new List<List<DateTime>>();

            var grupos = new List<List<DateTime>>();
            var semanaActual = new List<DateTime>();
            int? semanaIso = null;

            foreach (var f in fechas.OrderBy(x => x))
            {
                var s = ISOWeek(f);
                if (semanaIso == null || s != semanaIso)
                {
                    if (semanaActual.Count > 0) grupos.Add(semanaActual);
                    semanaActual = new List<DateTime>();
                    semanaIso = s;
                }
                semanaActual.Add(f);
            }
            if (semanaActual.Count > 0) grupos.Add(semanaActual);
            return grupos;
        }

        private static int ISOWeek(DateTime d) =>
            System.Globalization.ISOWeek.GetWeekOfYear(d);

        private async Task<PrescripcionSugerida?> ObtenerPrescripcionVigenteAsync(
            Guid tenantId, Guid pacienteId, int anio, int mes)
        {
            var periodoActual = new DateTime(anio, mes, 1);
            var prescripcion = await _prescripcionRepo.GetSugeridaByPacienteYPeriodoAsync(
                tenantId, pacienteId, periodoActual);
            if (prescripcion != null) return prescripcion;
            return await _prescripcionRepo.GetSugeridaByPacienteYPeriodoAsync(
                tenantId, pacienteId, periodoActual.AddMonths(-1));
        }

        private enum TipoTurno { LMV, MJS }
    }

    public class ResumenCostos
    {
        public decimal TotalEpoUI { get; set; }
        public decimal TotalHierroMg { get; set; }
        public decimal CostoEpo { get; set; }
        public decimal CostoHierro { get; set; }
        public decimal CostoTotal => CostoEpo + CostoHierro;
        public decimal PrecioEpoPorUI { get; set; }
        public decimal PrecioHierroPorMg { get; set; }
        public int TotalPacientes { get; set; }
        public int TotalSesiones { get; set; }
    }
}
