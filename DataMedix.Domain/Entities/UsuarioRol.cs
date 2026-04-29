namespace DataMedix.Domain.Entities
{
    public class UsuarioRol
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UsuarioId { get; set; }
        public Guid? RolId { get; set; }       // nullable: idrol en usuarioempresa puede ser null
        public Guid? TenantId { get; set; }    // mapea a idempresa en usuarioempresa
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Usuario Usuario { get; set; } = null!;
        public Rol Rol { get; set; } = null!;
    }
}
