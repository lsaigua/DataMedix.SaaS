namespace DataMedix.Domain.Entities
{
    public class Usuario
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? TenantId { get; set; }     // NULL = SuperAdmin global
        public string Codigo { get; set; } = null!;
        public string Identificacion { get; set; } = null!;
        public string PrimerNombre { get; set; } = null!;
        public string? SegundoNombre { get; set; }
        public string PrimerApellido { get; set; } = null!;
        public string? SegundoApellido { get; set; }
        public string Email { get; set; } = null!;
        public string? Telefono { get; set; }
        public string PasswordHash { get; set; } = null!;
        public bool Activo { get; set; } = true;
        public DateTime? UltimoAcceso { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }

        public string NombreCompleto =>
            $"{PrimerNombre} {SegundoNombre} {PrimerApellido} {SegundoApellido}".Trim()
                                                                                   .Replace("  ", " ");

        public Tenant? Tenant { get; set; }
        public ICollection<UsuarioRol> Roles { get; set; } = new List<UsuarioRol>();
    }
}
