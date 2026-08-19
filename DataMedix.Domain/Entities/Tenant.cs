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

        // ── Tarifas del plan (agregado en migración 007) ──────────────────────
        /// <summary>Cargo fijo mensual del plan.</summary>
        public decimal TarifaBase { get; set; }
        /// <summary>
        /// Cargo por cada paciente procesado. Se usa cuando el tenant no tiene
        /// tramos por volumen definidos; con tramos, manda el tramo.
        /// </summary>
        public decimal TarifaPaciente { get; set; }

        // ── Modelo de cobro (agregado en migración 008) ───────────────────────
        /// <summary>SUSCRIPCION | POR_PACIENTE | MIXTO — ver <see cref="ModeloCobro"/>.</summary>
        public string ModeloCobro { get; set; } = Entities.ModeloCobro.Mixto;
        /// <summary>Soporte premium u otros recurrentes que se suman cada mes.</summary>
        public decimal TarifaSoporteMensual { get; set; }
        public string Moneda { get; set; } = "USD";
        public string? FacturacionNotas { get; set; }

        public ICollection<TenantTarifaTramo> Tramos { get; set; } = new List<TenantTarifaTramo>();

        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
        public ICollection<Paciente> Pacientes { get; set; } = new List<Paciente>();
    }
}
