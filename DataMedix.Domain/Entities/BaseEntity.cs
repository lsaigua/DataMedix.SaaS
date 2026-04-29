namespace DataMedix.Domain.Entities
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public Guid? CreatedBy { get; set; }
    }
}
