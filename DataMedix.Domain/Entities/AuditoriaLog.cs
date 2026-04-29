namespace DataMedix.Domain.Entities
{
    public class AuditoriaLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? TenantId { get; set; }
        public Guid? UsuarioId { get; set; }
        public string Accion { get; set; } = null!;
        // LOGIN | LOGOUT | IMPORT | CREATE | UPDATE | DELETE | APPROVE | EXPORT
        public string? Entidad { get; set; }
        public Guid? EntidadId { get; set; }
        public string? Descripcion { get; set; }
        public string? DatosAnteriores { get; set; }    // JSON
        public string? DatosNuevos { get; set; }        // JSON
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string Resultado { get; set; } = "OK";   // OK | ERROR
        public string? MensajeError { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
