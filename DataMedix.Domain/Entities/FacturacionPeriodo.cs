namespace DataMedix.Domain.Entities
{
    /// <summary>
    /// Cierre de facturación de un período (tenant + año/mes).
    /// Mientras está ABIERTO se recalcula en vivo; al CERRARSE queda congelado
    /// y ya no cambia, aunque después se depuren datos o se den de baja pacientes.
    /// </summary>
    public class FacturacionPeriodo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }

        /// <summary>Plan comercial copiado del tenant al momento del cierre.</summary>
        public string? PlanNombre { get; set; }
        public decimal TarifaBase { get; set; }
        public decimal TarifaPaciente { get; set; }
        public int PacientesFacturados { get; set; }
        public decimal Total { get; set; }

        public string Estado { get; set; } = EstadoFacturacion.Abierto;
        public DateTime? CerradoAt { get; set; }
        public Guid? CerradoPor { get; set; }
        public string? Observaciones { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<FacturacionPeriodoDetalle> Detalles { get; set; } =
            new List<FacturacionPeriodoDetalle>();

        public DateTime PeriodDate => new(PeriodoAnio, PeriodoMes, 1);
        public bool EstaCerrado => Estado == EstadoFacturacion.Cerrado;
    }

    /// <summary>
    /// Un paciente facturado en el período. Guarda su propia copia de cédula y
    /// nombre: no hay FK a paciente porque el detalle debe seguir justificando el
    /// cobro aunque la depuración elimine al paciente del padrón.
    /// </summary>
    public class FacturacionPeriodoDetalle
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid FacturacionPeriodoId { get; set; }
        public Guid TenantId { get; set; }
        public Guid PacienteId { get; set; }
        public string? Identificacion { get; set; }
        public string? NombreCompleto { get; set; }

        // Qué actividad hizo facturable al paciente en ese mes
        public bool TuvoLaboratorio { get; set; }
        public bool TuvoSnapshot { get; set; }
        public bool TuvoPrescripcion { get; set; }
        public bool TuvoCronograma { get; set; }

        /// <summary>Estado del paciente en el padrón al momento del cierre.</summary>
        public string EstadoPaciente { get; set; } = EstadoPacienteFacturado.Activo;
        public decimal TarifaAplicada { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public FacturacionPeriodo Periodo { get; set; } = null!;
    }

    public static class EstadoFacturacion
    {
        public const string Abierto = "ABIERTO";
        public const string Cerrado = "CERRADO";
    }

    public static class EstadoPacienteFacturado
    {
        /// <summary>Sigue en el padrón y activo.</summary>
        public const string Activo = "ACTIVO";
        /// <summary>Sigue en el padrón pero fue dado de baja (Activo = false).</summary>
        public const string Baja = "BAJA";
        /// <summary>Ya no existe la fila en el padrón: se eliminó físicamente.</summary>
        public const string Eliminado = "ELIMINADO";
    }
}
