namespace DataMedix.Application.DTOs.HojaEpo
{
    public class HojaEpoCeldaDto
    {
        public DateTime PeriodDate { get; set; }

        // Valores del SnapshotMensual
        public decimal? HbValor { get; set; }
        public string? HbUnidad { get; set; }
        public decimal? HierroValor { get; set; }
        public decimal? FerritinaValor { get; set; }
        public decimal? SaturacionValor { get; set; }

        // Calculados por el motor de reglas (PrescripcionSugerida)
        public decimal? EpoUiSemana { get; set; }
        public decimal? HierroMgMes { get; set; }
        public string? EpoAccion { get; set; }
        public string? HierroAccion { get; set; }

        // Ajuste médico (PrescripcionFinal.EpoDosis / HierroDosis)
        public string? AjusteEpo { get; set; }
        public string? AjusteHierro { get; set; }

        // Valor numérico del ajuste (null si no está establecido o no es parseable)
        public decimal? AjusteEpoDecimal    => decimal.TryParse(AjusteEpo,    out var v) ? v : null;
        public decimal? AjusteHierroDecimal => decimal.TryParse(AjusteHierro, out var v) ? v : null;

        // Dosis efectiva: el ajuste médico tiene precedencia sobre el calculado.
        // Si el médico puso 1000 en Aj.Hierro, el paciente recibe 1000 (no calculado+1000).
        public decimal? EpoEfectivo    => AjusteEpoDecimal    ?? EpoUiSemana;
        public decimal? HierroEfectivo => AjusteHierroDecimal ?? HierroMgMes;

        // IDs para operaciones de guardado
        public Guid? PrescripcionSugeridaId { get; set; }
        public Guid? PrescripcionFinalId { get; set; }
        public string EstadoPrescripcion { get; set; } = "SIN_DATOS";

        public bool TieneDatos => HbValor.HasValue || EpoUiSemana.HasValue || HierroMgMes.HasValue || AjusteEpoDecimal.HasValue || AjusteHierroDecimal.HasValue;
    }
}
