namespace DataMedix.Domain.Entities
{
    /// <summary>
    /// Reparto de la dosis SEMANAL de EPO entre los días de sesión del turno.
    ///
    /// Se razona en UNIDADES de la presentación mínima, no en fracciones. Una
    /// división directa produce dosis inadministrables: 8.000 UI entre 7 días
    /// darían 1.142,86 UI, que no existe en ningún vial. Trabajando en múltiplos
    /// de la presentación mínima, toda dosis resultante es administrable y el
    /// total semanal se conserva exacto.
    ///
    /// Los turnos clásicos de 3 sesiones (LMV / MJS) NO usan este reparto:
    /// conservan su tabla validada en CronogramaService, que es asimétrica a
    /// propósito. Esta clase cubre el resto de turnos, de 1 a 7 días.
    /// </summary>
    public static class DistribucionEpo
    {
        /// <summary>Presentación más pequeña de EPO: toda dosis es múltiplo de este valor.</summary>
        public const decimal PresentacionMinima = 2000m;

        /// <summary>
        /// Dosis que corresponde a cada día del turno en una semana completa.
        ///
        /// Los días ausentes del resultado son sesión de diálisis SIN EPO, algo
        /// esperable en turnos diarios: el paciente se dializa todos los días
        /// pero recibe EPO solo algunos.
        /// </summary>
        public static Dictionary<DayOfWeek, decimal> PorDia(
            decimal epoUiSemana, IReadOnlyList<DayOfWeek> dias)
        {
            var mapa = new Dictionary<DayOfWeek, decimal>();
            if (dias is null || dias.Count == 0 || epoUiSemana <= 0) return mapa;

            var n = dias.Count;
            var k = (int)Math.Round(epoUiSemana / PresentacionMinima);
            if (k <= 0) return mapa;

            var unidadesBase = k / n;
            var resto        = k % n;

            // Los días que llevan la unidad extra se eligen con la misma fórmula
            // de índice centrado que usa el programador de hierro, para que
            // queden repartidos en la semana y no amontonados al inicio.
            var extra = new HashSet<int>();
            for (int i = 0; i < resto; i++)
                extra.Add(Math.Clamp((int)Math.Round((2.0 * i + 1) * n / (2.0 * resto)), 0, n - 1));

            // Si el redondeo colapsó dos índices en el mismo día, se completan
            // con los primeros libres: perder una unidad alteraría el total semanal.
            for (int i = 0; extra.Count < resto && i < n; i++)
                extra.Add(i);

            for (int i = 0; i < n; i++)
            {
                var unidades = unidadesBase + (extra.Contains(i) ? 1 : 0);
                if (unidades > 0) mapa[dias[i]] = unidades * PresentacionMinima;
            }

            return mapa;
        }

        /// <summary>Total efectivamente repartido en una semana completa.</summary>
        public static decimal TotalSemanal(decimal epoUiSemana, IReadOnlyList<DayOfWeek> dias) =>
            PorDia(epoUiSemana, dias).Values.Sum();

        /// <summary>
        /// Dosis representativa de una sesión, para convertir UI pendientes en
        /// sesiones equivalentes. Se toma la menor dosis distinta de cero, que
        /// es la unidad con la que se compensa.
        /// </summary>
        public static decimal DosisTipicaPorSesion(decimal epoUiSemana, IReadOnlyList<DayOfWeek> dias)
        {
            var valores = PorDia(epoUiSemana, dias).Values.Where(v => v > 0).ToList();
            return valores.Count == 0 ? 0m : valores.Min();
        }
    }
}
