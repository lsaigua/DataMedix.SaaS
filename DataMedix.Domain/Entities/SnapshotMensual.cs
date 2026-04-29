namespace DataMedix.Domain.Entities
{
    public class SnapshotMensual
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid PacienteId { get; set; }
        public DateTime PeriodDate { get; set; }            // Siempre 1ro del mes
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }
        public Guid? LoteId { get; set; }
        public string? PlanSalud { get; set; }
        public string? TipoAtencion { get; set; }

        // Parámetros clave desnormalizados para rendimiento
        public decimal? HbValor { get; set; }
        public string? HbUnidad { get; set; }
        public decimal? HierroValor { get; set; }
        public string? HierroUnidad { get; set; }
        public decimal? FerritinaValor { get; set; }
        public string? FerritinaUnidad { get; set; }
        public decimal? SaturacionValor { get; set; }
        public string? SaturacionUnidad { get; set; }

        // Metadatos de completitud
        public bool TieneDatosCompletos { get; set; } = false;
        // true cuando se usa el mes anterior porque el actual no tiene datos
        public bool EsDatosPeriodoAnterior { get; set; } = false;
        public DateTime? PeriodDateReal { get; set; }       // período de donde vienen los datos

        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Paciente Paciente { get; set; } = null!;
        public LoteImportacion? Lote { get; set; }
        public ICollection<SnapshotMensualDetalle> Detalles { get; set; } = new List<SnapshotMensualDetalle>();
        public PrescripcionSugerida? PrescripcionSugerida { get; set; }
    }
}
