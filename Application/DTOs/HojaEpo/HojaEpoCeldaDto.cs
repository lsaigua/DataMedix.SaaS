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

        // IDs para operaciones de guardado
        public Guid? PrescripcionSugeridaId { get; set; }
        public Guid? PrescripcionFinalId { get; set; }
        public string EstadoPrescripcion { get; set; } = "SIN_DATOS";

        public bool TieneDatos => HbValor.HasValue || EpoUiSemana.HasValue || HierroMgMes.HasValue;
    }
}
