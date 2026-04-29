namespace DataMedix.Domain.Entities
{
    public class PrescripcionSugerida
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid PacienteId { get; set; }
        public Guid? SnapshotId { get; set; }
        public DateTime PeriodDate { get; set; }

        // EPO
        public string? EpoAccion { get; set; }
        public string? EpoDosisSugerida { get; set; }
        public string? EpoObservacion { get; set; }
        public Guid? EpoRangoId { get; set; }

        // Hierro IV
        public string? HierroAccion { get; set; }
        public string? HierroDosisSugerida { get; set; }
        public string? HierroObservacion { get; set; }
        public Guid? HierroRangoId { get; set; }

        public string? ObservacionesGenerales { get; set; }
        public string Estado { get; set; } = EstadoPrescripcion.Pendiente;
        public Guid? RevisadoPor { get; set; }
        public DateTime? RevisadoAt { get; set; }

        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Paciente Paciente { get; set; } = null!;
        public SnapshotMensual? Snapshot { get; set; }
        public RangoPrescriba? EpoRango { get; set; }
        public RangoPrescriba? HierroRango { get; set; }
        public PrescripcionFinal? PrescripcionFinal { get; set; }
    }

    public class PrescripcionFinal
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid PacienteId { get; set; }
        public Guid? PrescripcionSugeridaId { get; set; }
        public Guid MedicoId { get; set; }
        public DateTime PeriodDate { get; set; }

        // EPO
        public bool EpoPrescrito { get; set; } = false;
        public string? EpoDosis { get; set; }
        public string? EpoFrecuencia { get; set; }
        public string? EpoObservacion { get; set; }

        // Hierro IV
        public bool HierroPrescrito { get; set; } = false;
        public string? HierroDosis { get; set; }
        public string? HierroFrecuencia { get; set; }
        public string? HierroObservacion { get; set; }

        public string? Observaciones { get; set; }
        public string? Diagnostico { get; set; }
        public string Estado { get; set; } = "ACTIVA";
        public DateTime AprobadoAt { get; set; } = DateTime.UtcNow;

        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Paciente Paciente { get; set; } = null!;
        public Usuario Medico { get; set; } = null!;
        public PrescripcionSugerida? PrescripcionSugerida { get; set; }
    }

    public static class EstadoPrescripcion
    {
        public const string Pendiente = "PENDIENTE";
        public const string Revisado = "REVISADO";
        public const string Aprobado = "APROBADO";
        public const string Rechazado = "RECHAZADO";
    }
}
