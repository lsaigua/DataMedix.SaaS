using DataMedix.Application.Interfaces;

namespace DataMedix.Application.Services
{
    /// <summary>
    /// Distribuye aplicaciones de Hierro IV siguiendo estándares clínicos de hemodiálisis
    /// (Fresenius EuCliD, Baxter Sharesource): dosis fija 100 mg por aplicación,
    /// distribución uniforme a lo largo del mes, separación mínima de 48 h.
    ///
    /// Algoritmo de distribución uniforme:
    ///   n = hierroMgMes / 100  (número de aplicaciones)
    ///   m = total sesiones del turno en el período
    ///   Si n == 1 → sesión central (m/2)
    ///   Si n  > 1 → índices = round(i * (m-1) / (n-1)) para i in [0, n-1]
    ///
    /// Para LMV y MJS las sesiones siempre tienen ≥ 48 h de separación entre sí
    /// (lunes→miércoles = 2 días, etc.), por lo que el filtro de 48 h es una
    /// salvaguarda para el modo Flexible.
    /// </summary>
    public class HierroSchedulerService : IHierroSchedulerService
    {
        public const decimal DosisAplicacion = 100m;
        private static readonly TimeSpan SeparacionMinima = TimeSpan.FromHours(48);

        public List<DateTime> GenerarFechasAplicacion(decimal? hierroMgMes, IReadOnlyList<DateTime> sessionDates)
        {
            if (!hierroMgMes.HasValue || hierroMgMes <= 0 || sessionDates.Count == 0)
                return new List<DateTime>();

            int n = (int)Math.Floor(hierroMgMes.Value / DosisAplicacion);
            if (n <= 0) return new List<DateTime>();

            var sessions = sessionDates.OrderBy(d => d).ToList();
            int m = sessions.Count;
            n = Math.Min(n, m);

            if (n == m) return sessions;

            var selected = new List<DateTime>(n);

            if (n == 1)
            {
                selected.Add(sessions[m / 2]);
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    int idx = (int)Math.Round((double)i * (m - 1) / (n - 1));
                    selected.Add(sessions[idx]);
                }
            }

            return EnforceMinimumSeparation(selected);
        }

        private static List<DateTime> EnforceMinimumSeparation(List<DateTime> dates)
        {
            var result = new List<DateTime>();
            DateTime? last = null;

            foreach (var d in dates.OrderBy(x => x))
            {
                if (last == null || (d - last.Value) >= SeparacionMinima)
                {
                    result.Add(d);
                    last = d;
                }
            }

            return result;
        }
    }
}
