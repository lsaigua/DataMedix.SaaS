namespace DataMedix.Domain.Entities
{
    public class AplicacionHierro
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid PacienteId { get; set; }
        public Guid CronogramaId { get; set; }
        public DateTime FechaProgramada { get; set; }
        public decimal DosisMg { get; set; } = 100m;
        public string Estado { get; set; } = EstadoAplicacionHierro.Pendiente;
        public string? Observaciones { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    public static class EstadoAplicacionHierro
    {
        public const string Pendiente    = "PENDIENTE";
        public const string Aplicado     = "APLICADO";
        public const string Suspendido   = "SUSPENDIDO";
        public const string Reprogramado = "REPROGRAMADO";
        public const string Cancelado    = "CANCELADO";
    }
}
