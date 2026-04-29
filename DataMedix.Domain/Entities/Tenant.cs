namespace DataMedix.Domain.Entities
{
    public class Tenant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? Codigo { get; set; }          // no existe en DB; ignorado
        public string? Nombre { get; set; }          // columna: name
        public string? Ruc { get; set; }             // ignorado
        public string Subdomain { get; set; } = null!;
        public string? LogoUrl { get; set; }         // ignorado
        public string? EmailContacto { get; set; }   // ignorado
        public string? Telefono { get; set; }        // ignorado
        public string? Direccion { get; set; }       // ignorado
        public string? Ciudad { get; set; }          // ignorado
        public string? Pais { get; set; }            // ignorado
        public bool Activo { get; set; } = true;     // columna: isactive
        public DateTime? CreatedAt { get; set; }     // ignorado
        public DateTime? UpdatedAt { get; set; }     // ignorado
        public DateTime? DeletedAt { get; set; }     // ignorado

        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
        public ICollection<Paciente> Pacientes { get; set; } = new List<Paciente>();
    }
}
