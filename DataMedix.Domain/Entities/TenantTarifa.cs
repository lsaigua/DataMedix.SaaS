namespace DataMedix.Domain.Entities
{
    /// <summary>
    /// Tramo de precio por volumen de pacientes.
    /// Tarifa PLANA por tramo: si el volumen del mes cae en 101-300, todos los
    /// pacientes se cobran a ese precio (no es escalonado acumulativo). Es lo
    /// que comunica la tabla comercial al cliente.
    /// </summary>
    public class TenantTarifaTramo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public int DesdePacientes { get; set; } = 1;
        /// <summary>NULL = sin límite superior.</summary>
        public int? HastaPacientes { get; set; }
        public decimal PrecioPaciente { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public bool Contiene(int pacientes) =>
            pacientes >= DesdePacientes &&
            (HastaPacientes is null || pacientes <= HastaPacientes.Value);

        public string Rango => HastaPacientes is null
            ? $"{DesdePacientes}+ pacientes"
            : $"{DesdePacientes} – {HastaPacientes} pacientes";
    }

    /// <summary>
    /// Cargo puntual que se suma a la factura de un período concreto:
    /// implementación inicial, migración de datos, personalizaciones.
    /// </summary>
    public class TenantCargoUnico
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string Concepto { get; set; } = null!;
        public decimal Monto { get; set; }
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }
        public bool Aplicado { get; set; } = true;
        public string? Observaciones { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedBy { get; set; }
    }

    /// <summary>Modelos comerciales de cobro.</summary>
    public static class ModeloCobro
    {
        /// <summary>Tarifa fija mensual según el plan contratado.</summary>
        public const string Suscripcion = "SUSCRIPCION";
        /// <summary>Solo consumo: pacientes procesados × precio del tramo.</summary>
        public const string PorPaciente = "POR_PACIENTE";
        /// <summary>Tarifa base + consumo por paciente.</summary>
        public const string Mixto = "MIXTO";

        public static readonly string[] Todos = [Suscripcion, PorPaciente, Mixto];

        public static string Etiqueta(string modelo) => modelo switch
        {
            Suscripcion => "Suscripción fija",
            PorPaciente => "Solo por paciente",
            Mixto       => "Base + por paciente",
            _           => modelo
        };

        public static string Descripcion(string modelo) => modelo switch
        {
            Suscripcion => "El cliente paga una tarifa mensual fija, sin importar cuántos pacientes procese.",
            PorPaciente => "El cliente paga solo por los pacientes procesados en el mes, al precio del tramo de volumen.",
            Mixto       => "Cargo fijo mensual más un valor por cada paciente procesado.",
            _           => ""
        };
    }
}
