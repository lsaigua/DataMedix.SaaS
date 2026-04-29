namespace DataMedix.Domain.Entities
{
    public class Rol
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nombre { get; set; } = null!;     // SUPERADMIN | ADMIN | MEDICO | OPERADOR | VISUALIZADOR
        public string? Descripcion { get; set; }
        public bool EsGlobal { get; set; } = false;
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UsuarioRol> UsuariosRoles { get; set; } = new List<UsuarioRol>();
    }
}
