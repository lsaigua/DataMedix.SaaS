namespace DataMedix.Domain.Entities
{
    public class Paciente
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string? Codigo { get; set; }
        public string Identificacion { get; set; } = null!;
        public string PrimerNombre { get; set; } = null!;
        public string? SegundoNombre { get; set; }
        public string PrimerApellido { get; set; } = null!;
        public string? SegundoApellido { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public string? Genero { get; set; }
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? PlanSalud { get; set; }
        public string? TipoAtencion { get; set; }
        public DateTime? FechaIngreso { get; set; }         // Fecha ingreso a diálisis
        public string? MedicoResponsable { get; set; }
        public string? Observaciones { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }

        public string NombreCompleto =>
            $"{PrimerNombre} {SegundoNombre} {PrimerApellido} {SegundoApellido}".Trim()
                                                                                   .Replace("  ", " ");

        // Tiempo en diálisis calculado dinámicamente
        public int? MesesEnDialisis => FechaIngreso.HasValue
            ? (int)((DateTime.Today - FechaIngreso.Value).TotalDays / 30.44)
            : null;

        public Tenant Tenant { get; set; } = null!;
        public ICollection<ResultadoLaboratorio> Resultados { get; set; } = new List<ResultadoLaboratorio>();
        public ICollection<SnapshotMensual> Snapshots { get; set; } = new List<SnapshotMensual>();
    }
}
