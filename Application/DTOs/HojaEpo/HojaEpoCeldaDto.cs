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

        // Dosis efectiva: ADITIVA — calculado + ajuste médico.
        // EPO: base 4000 + ajuste médico 2000 = 6000 UI/sem que recibe el paciente.
        // Si solo existe uno de los dos, se usa ese valor directamente.
        public decimal? EpoEfectivo =>
            (EpoUiSemana.HasValue || AjusteEpoDecimal.HasValue)
            ? (EpoUiSemana ?? 0) + (AjusteEpoDecimal ?? 0)
            : null;

        public decimal? HierroEfectivo =>
            (HierroMgMes.HasValue || AjusteHierroDecimal.HasValue)
            ? (HierroMgMes ?? 0) + (AjusteHierroDecimal ?? 0)
            : null;

        // IDs para operaciones de guardado
        public Guid? PrescripcionSugeridaId { get; set; }
        public Guid? PrescripcionFinalId { get; set; }
        public string EstadoPrescripcion { get; set; } = "SIN_DATOS";

        public bool TieneDatos => HbValor.HasValue || EpoUiSemana.HasValue || HierroMgMes.HasValue || AjusteEpoDecimal.HasValue || AjusteHierroDecimal.HasValue;
    }
}
