namespace DataMedix.Domain.Entities
{
    public class AliasParametro
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ParametroClinicoId { get; set; }
        public Guid? TenantId { get; set; }     // NULL = alias global
        public string Alias { get; set; } = null!;
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ParametroClinico ParametroClinico { get; set; } = null!;
    }
}
