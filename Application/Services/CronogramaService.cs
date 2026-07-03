using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;

namespace DataMedix.Application.Services
{
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
    /// Distribución Hierro: 200 mg por sesión, primera sesión de cada semana,
    /// hasta agotar el total mensual (HierroMgMes).
    /// </summary>
    public class CronogramaService
    {
        private readonly ICronogramaRepository _repo;
        private readonly IConfiguracionMedicamentoRepository _configRepo;
        private readonly IPacienteRepository _pacienteRepo;
        private readonly IPrescripcionRepository _prescripcionRepo;

        public CronogramaService(
            ICronogramaRepository repo,
            IConfiguracionMedicamentoRepository configRepo,
            IPacienteRepository pacienteRepo,
            IPrescripcionRepository prescripcionRepo)
        {
            _repo = repo;
            _configRepo = configRepo;
            _pacienteRepo = pacienteRepo;
            _prescripcionRepo = prescripcionRepo;
        }

        // ──────────────────────────────────────────────────────────────────────
        // LEER cronogramas del mes (sin regenerar)
        // ──────────────────────────────────────────────────────────────────────
        public Task<List<CronogramaMedicamento>> GetCronogramasMesAsync(Guid tenantId, int anio, int mes)
            => _repo.GetByPeriodoAsync(tenantId, anio, mes);

        // ──────────────────────────────────────────────────────────────────────
        // GENERAR / ACTUALIZAR cronograma para todos los pacientes del tenant
        // ──────────────────────────────────────────────────────────────────────
        public async Task<List<CronogramaMedicamento>> GenerarOActualizarMesAsync(
            Guid tenantId, int anio, int mes, Guid? usuarioId = null)
        {
            var pacientes = await _pacienteRepo.GetActivosAsync(tenantId);
            var resultado = new List<CronogramaMedicamento>();

            foreach (var paciente in pacientes)
            {
                var cronograma = await GenerarPorPacienteAsync(tenantId, paciente.Id, anio, mes, usuarioId);
                resultado.Add(cronograma);
            }

            return resultado;
        }

        // ──────────────────────────────────────────────────────────────────────
        // GENERAR cronograma de un paciente para un mes dado
        // Solo rellena días vacíos (no sobreescribe ediciones manuales)
        // ──────────────────────────────────────────────────────────────────────
        public async Task<CronogramaMedicamento> GenerarPorPacienteAsync(
            Guid tenantId, Guid pacienteId, int anio, int mes, Guid? usuarioId = null)
        {
            var paciente = await _pacienteRepo.GetByIdAsync(tenantId, pacienteId)
                ?? throw new InvalidOperationException($"Paciente {pacienteId} no encontrado");

            // Buscar prescripción del mes anterior para obtener dosis
            // (Cronograma del mes X usa prescripción del mes X-1 procesada)
            var prescripcion = await ObtenerPrescripcionVigenteAsync(tenantId, pacienteId, anio, mes);

            var cronograma = await _repo.GetByPacienteYPeriodoAsync(tenantId, pacienteId, anio, mes)
                ?? new CronogramaMedicamento
                {
                    TenantId = tenantId,
                    PacienteId = pacienteId,
                    PeriodoAnio = anio,
                    PeriodoMes = mes,
                    CreatedBy = usuarioId
                };

            cronograma.PlanSalud = paciente.PlanSalud ?? cronograma.PlanSalud;
            cronograma.EpoUiSemana = prescripcion?.EpoUiSemana ?? cronograma.EpoUiSemana;
            cronograma.HierroMgMes = prescripcion?.HierroMgMes ?? cronograma.HierroMgMes;
            cronograma.UpdatedBy = usuarioId;
            cronograma.UpdatedAt = DateTime.UtcNow;

            cronograma = await _repo.UpsertAsync(cronograma);

            // Calcular y persistir días de sesión del mes
            var dias = GenerarDias(cronograma);
            await _repo.UpsertDiasAsync(dias);

            // Recargar con días
            return await _repo.GetConDiasAsync(tenantId, cronograma.Id) ?? cronograma;
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

            var fechasSesion = ObtenerFechasSesion(cronograma.PeriodoAnio, cronograma.PeriodoMes, turno.Value);
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

        private static TipoTurno? ParsearTurno(string? planSalud)
        {
            if (string.IsNullOrWhiteSpace(planSalud)) return null;
            var upper = planSalud.ToUpperInvariant();
            if (upper.Contains("LMV")) return TipoTurno.LMV;
            if (upper.Contains("MJS")) return TipoTurno.MJS;
            return null;
        }

        private static List<DateTime> ObtenerFechasSesion(int anio, int mes, TipoTurno turno)
        {
            var diasSemana = turno == TipoTurno.LMV
                ? new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }
                : new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday };

            var inicio = new DateTime(anio, mes, 1);
            var fin = inicio.AddMonths(1);
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
                if (semana.Count == 0) continue;

                // Índice del día central según turno (LMV=Mié=índice 1, MJS=Jue=índice 1)
                var (dosis1, dosis2, dosis3) = CalcularDosisEpoSemanal(ui);

                if (dosis3 > 0 && semana.Count >= 3)
                {
                    resultado[semana[0]] = dosis1;
                    resultado[semana[1]] = dosis2;
                    resultado[semana[2]] = dosis3;
                }
                else if (dosis1 > 0 && dosis3 == 0 && semana.Count >= 1)
                {
                    // Solo día central
                    var centro = semana.Count >= 2 ? semana[1] : semana[0];
                    resultado[centro] = dosis2 > 0 ? dosis2 : dosis1;
                }
                else if (semana.Count >= 2)
                {
                    resultado[semana[0]] = dosis1;
                    resultado[semana[^1]] = dosis3 > 0 ? dosis3 : dosis2;
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
            if (!hierroMgMes.HasValue || hierroMgMes <= 0) return resultado;

            const decimal dosisPorSesion = 200m;
            var semanas = AgruparPorSemana(fechasSesion);
            decimal restante = hierroMgMes.Value;

            foreach (var semana in semanas)
            {
                if (restante <= 0) break;
                if (semana.Count == 0) continue;

                var dosis = Math.Min(dosisPorSesion, restante);
                resultado[semana[0]] = dosis;
                restante -= dosis;
            }

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
            // Busca la prescripción del mes actual; si no hay, intenta el mes anterior.
            // El cronograma del mes X se alimenta de la prescripción aprobada más reciente.
            var periodoActual = new DateTime(anio, mes, 1);
            var prescripcion = await _prescripcionRepo.GetSugeridaByPacienteYPeriodoAsync(
                tenantId, pacienteId, periodoActual);

            if (prescripcion != null) return prescripcion;

            // Fallback: mes anterior (prescripción ya procesada del corte anterior)
            var periodoAnterior = periodoActual.AddMonths(-1);
            return await _prescripcionRepo.GetSugeridaByPacienteYPeriodoAsync(
                tenantId, pacienteId, periodoAnterior);
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
    }
}
