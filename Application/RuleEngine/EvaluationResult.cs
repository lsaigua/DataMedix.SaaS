namespace DataMedix.Application.RuleEngine
{
    /// <summary>
    /// Resultado de evaluar todas las reglas para un paciente en un período.
    /// </summary>
    public class EvaluationResult
    {
        // ── EPO ───────────────────────────────────────────────────────────────
        /// <summary>Código de la regla EPO que disparó (EPO-01..EPO-07). Null si ninguna aplicó.</summary>
        public string? ReglaEpoCodigo { get; set; }
        public string? ReglaEpoNombre { get; set; }
        public decimal? EpoUiSemana { get; set; }
        public bool EpoRecomendada { get; set; }
        public string? EpoMensaje { get; set; }

        // ── Hierro IV ────────────────────────────────────────────────────────
        public string? ReglaHierroCodigo { get; set; }
        public string? ReglaHierroNombre { get; set; }
        public decimal? HierroMgMes { get; set; }
        public decimal? HierroDosisIndividualMg { get; set; }
        public bool HierroRecomendado { get; set; }
        public string? HierroMensaje { get; set; }

        // ── Alertas ───────────────────────────────────────────────────────────
        public List<AlertaClinica> Alertas { get; set; } = new();

        // ── Modificadores aplicados ───────────────────────────────────────────
        public List<string> ModificadoresAplicados { get; set; } = new();

        // ── Cálculo alternativo Ganzoni ───────────────────────────────────────
        /// <summary>Dosis de hierro según fórmula de Ganzoni: peso×(15−Hb)×2.4+500</summary>
        public decimal? HierroGanzoniMg { get; set; }

        // ── Helpers ───────────────────────────────────────────────────────────
        public bool TieneAlertaCritica =>
            Alertas.Any(a => a.Severidad == "CRITICA");

        public bool TieneAlertaResistenciaEpo =>
            Alertas.Any(a => a.Flag == "RESISTENCIA_EPO");

        public bool RequierePruebaSensibilidad =>
            Alertas.Any(a => a.Flag == "REQUIERE_PRUEBA_SENSIBILIDAD");

        public bool TieneSobrecargaHierro =>
            Alertas.Any(a => a.Flag == "SOBRECARGA_HIERRO");
    }
}
