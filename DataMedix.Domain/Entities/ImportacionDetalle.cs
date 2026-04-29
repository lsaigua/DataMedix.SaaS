namespace DataMedix.Domain.Entities
{
    public class ImportacionDetalle
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid LoteId { get; set; }
        public Guid TenantId { get; set; }
        public int NumeroFila { get; set; }

        // Columnas crudas del Excel
        public string? FechaOrdenRaw { get; set; }
        public string? PlanSaludRaw { get; set; }
        public string? TipoAtencionRaw { get; set; }
        public string? IdentificacionRaw { get; set; }
        public string? PacienteRaw { get; set; }
        public string? ExamenRaw { get; set; }
        public string? ParametroRaw { get; set; }
        public string? ResultadoRaw { get; set; }
        public string? UnidadMedidaRaw { get; set; }

        // Datos procesados
        public DateTime? PeriodDate { get; set; }
        public Guid? PacienteId { get; set; }
        public Guid? ParametroClinicoId { get; set; }
        public string Estado { get; set; } = EstadoDetalle.Pendiente;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public LoteImportacion Lote { get; set; } = null!;
        public Paciente? Paciente { get; set; }
        public ParametroClinico? ParametroClinico { get; set; }
    }

    public static class EstadoDetalle
    {
        public const string Pendiente = "PENDIENTE";
        public const string Valido = "VALIDO";
        public const string Error = "ERROR";
        public const string Duplicado = "DUPLICADO";
        public const string Procesado = "PROCESADO";
    }
}
