namespace DataMedix.Domain.Entities
{
    public class Tenant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? Codigo { get; set; }
        public string? Nombre { get; set; }
        public string? Ruc { get; set; }
        public string Subdomain { get; set; } = null!;
        public string? LogoUrl { get; set; }
        public string? EmailContacto { get; set; }
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public string? Ciudad { get; set; }
        public string? Pais { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        // ── Routing multi-tenant (agregado en migración 005) ──────────────────
        /// <summary>"shared" = base compartida (default). "dedicated" = base propia.</summary>
        public string IsolationMode { get; set; } = "shared";
        /// <summary>Nombre del secreto en IConfiguration, NO el valor de la connection string.</summary>
        public string? ConnectionStringRef { get; set; }
        /// <summary>Nombre del plan de servicio para facturación.</summary>
        public string? PlanNombre { get; set; }

        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
        public ICollection<Paciente> Pacientes { get; set; } = new List<Paciente>();
    }
}
