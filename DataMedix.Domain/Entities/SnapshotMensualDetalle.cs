namespace DataMedix.Domain.Entities
{
    public class SnapshotMensualDetalle
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SnapshotId { get; set; }
        public Guid? ParametroClinicoId { get; set; }
        public string? ParametroNombre { get; set; }
        public string? ValorTexto { get; set; }
        public decimal? ValorNumerico { get; set; }
        public string? UnidadMedida { get; set; }
        public bool EsPatologico { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public SnapshotMensual Snapshot { get; set; } = null!;
        public ParametroClinico? ParametroClinico { get; set; }
    }
}
