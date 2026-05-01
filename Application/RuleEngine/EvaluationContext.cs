namespace DataMedix.Application.RuleEngine
{
    /// <summary>
    /// Todos los valores de entrada para evaluar las reglas clínicas de un paciente en un período.
    /// Los valores nullable indican que no fueron medidos en ese período.
    /// </summary>
    public class EvaluationContext
    {
        public Guid TenantId { get; set; }
        public Guid PacienteId { get; set; }
        public DateTime PeriodDate { get; set; }

        // ── Parámetros de laboratorio ─────────────────────────────────────────
        public decimal? Hb { get; set; }              // g/dL  — hemoglobina
        public decimal? TSAT { get; set; }             // %     — índice saturación transferrina (ISAT)
        public decimal? Ferritina { get; set; }        // ng/mL — ferritina sérica
        public decimal? HierroSerico { get; set; }     // µg/dL — hierro sérico (FE)
        public decimal? Albumina { get; set; }         // g/dL
        public decimal? Creatinina { get; set; }       // mg/dL
        public decimal? BUN { get; set; }              // mg/dL
        public decimal? Potasio { get; set; }          // mEq/L
        public decimal? Sodio { get; set; }            // mEq/L
        public decimal? Calcio { get; set; }           // mg/dL
        public decimal? Fosforo { get; set; }          // mg/dL
        public decimal? PTH { get; set; }              // pg/mL

        // ── Contexto clínico ──────────────────────────────────────────────────
        public decimal? PesoKg { get; set; }

        /// <summary>Meses transcurridos desde ingreso a hemodiálisis.</summary>
        public int MesesEnDialisis { get; set; }

        /// <summary>Dosis de EPO actual (semana anterior) en UI/semana. Null si no tiene.</summary>
        public decimal? EpoUiSemanaActual { get; set; }

        /// <summary>
        /// Número consecutivo de meses donde Hb no mejoró con EPO ≥ 12000 UI/sem.
        /// 3+ activa la alerta de resistencia.
        /// </summary>
        public int MesesSinMejoraHb { get; set; }

        /// <summary>True si esta es la primera vez que el paciente recibirá hierro EV.</summary>
        public bool PrimeraVezHierro { get; set; }

        /// <summary>True si el mes del período es impar (sin perfil de hierro nuevo).</summary>
        public bool MesActualEsImpar { get; set; }

        /// <summary>True si hay perfil de hierro (TSAT + Ferritina) en el período actual.</summary>
        public bool PerfilHierroActual { get; set; }
    }
}
