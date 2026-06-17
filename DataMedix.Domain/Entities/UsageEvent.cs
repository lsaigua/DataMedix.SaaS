namespace DataMedix.Domain.Entities
{
    /// <summary>
    /// Evento de uso para facturación por consumo.
    /// Siempre persiste en la base central, independientemente del modo de aislamiento del tenant.
    /// </summary>
    public class UsageEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string EventType { get; set; } = null!;
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public string? MetadataJson { get; set; }

        public Tenant Tenant { get; set; } = null!;
    }
}
