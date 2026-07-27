using DataMedix.Application.Interfaces;

namespace DataMedix.Application.Services
{
    /// <summary>
    /// Distribuye aplicaciones de Hierro IV con dosis máxima de 200 mg por aplicación.
    ///
    ///   n   = ceil(HierroMgMes / 200)    — número de aplicaciones
    ///   m   = total sesiones del turno en el período
    ///   idx = round((2i + 1) * m / (2n))  para i ∈ [0, n-1]  — centrado de segmentos
    ///
    /// La distribución "centrada" coloca cada aplicación en el CENTRO de su segmento,
    /// no en los bordes. Esto produce fechas hacia la semana 2 del mes para n=1 (200 mg),
    /// y ~15 días de separación para n=2 (400 mg), en lugar de primera/última sesión.
    ///
    /// Para LMV y MJS las sesiones siempre tienen ≥ 48 h entre sí,
    /// por lo que EnforceMinimumSeparation es solo una salvaguarda para modo Flexible.
    /// </summary>
    public class HierroSchedulerService : IHierroSchedulerService
    {
        /// <summary>Máximo de mg por aplicación. Determina el número de aplicaciones.</summary>
        public const decimal MaxDosisAplicacion = 200m;
        private static readonly TimeSpan SeparacionMinima = TimeSpan.FromHours(48);

        public List<DateTime> GenerarFechasAplicacion(decimal? hierroMgMes, IReadOnlyList<DateTime> sessionDates)
        {
            if (!hierroMgMes.HasValue || hierroMgMes <= 0 || sessionDates.Count == 0)
                return new List<DateTime>();

            int n = (int)Math.Ceiling(hierroMgMes.Value / MaxDosisAplicacion);
            if (n <= 0) return new List<DateTime>();

            var sessions = sessionDates.OrderBy(d => d).ToList();
            int m = sessions.Count;
            n = Math.Min(n, m);

            if (n == m) return sessions;

            var selected = new List<DateTime>(n);

            // Centrado de segmentos: idx = round((2i+1) * m / (2n))
            // n=1,m=13 → idx=round(6.5)=6 (mitad del mes)
            // n=2,m=13 → idx=3 y 10 (~semana 1 y semana 3, ~15 días entre ellas)
            // n=3,m=13 → idx=2,6,11 (~semana 1, 2, 4, ~10 días entre ellas)
            for (int i = 0; i < n; i++)
            {
                int idx = (int)Math.Round((double)(2 * i + 1) * m / (2 * n));
                idx = Math.Clamp(idx, 0, m - 1);
                selected.Add(sessions[idx]);
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
