namespace DataMedix.Application.DTOs
{
    /// <summary>Configuración comercial de un cliente, editable por el dueño del SaaS.</summary>
    public class TarifaTenantDto
    {
        public Guid TenantId { get; set; }
        public string? TenantNombre { get; set; }
        public string? Subdomain { get; set; }
        public bool Activo { get; set; } = true;

        public string? PlanNombre { get; set; }
        public string ModeloCobro { get; set; } = "MIXTO";
        public decimal TarifaBase { get; set; }
        public decimal TarifaPaciente { get; set; }
        public decimal TarifaSoporteMensual { get; set; }
        public string Moneda { get; set; } = "USD";
        public string? Notas { get; set; }

        public List<TramoTarifaDto> Tramos { get; set; } = new();

        /// <summary>Pacientes procesados en el último período calculado, para vista previa.</summary>
        public int PacientesUltimoPeriodo { get; set; }
        public decimal TotalUltimoPeriodo { get; set; }
    }

    public class TramoTarifaDto
    {
        public Guid? Id { get; set; }
        public int DesdePacientes { get; set; } = 1;
        public int? HastaPacientes { get; set; }
        public decimal PrecioPaciente { get; set; }

        public string Rango => HastaPacientes is null
            ? $"{DesdePacientes}+ pacientes"
            : $"{DesdePacientes} – {HastaPacientes} pacientes";
    }

    public class CargoUnicoDto
    {
        public Guid? Id { get; set; }
        public string Concepto { get; set; } = "";
        public decimal Monto { get; set; }
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }
        public bool Aplicado { get; set; } = true;
        public string? Observaciones { get; set; }
    }

    /// <summary>Fila del consolidado de facturación de todos los clientes.</summary>
    public class FacturacionGlobalDto
    {
        public Guid TenantId { get; set; }
        public string? TenantNombre { get; set; }
        public string? Subdomain { get; set; }
        public string? PlanNombre { get; set; }
        public string ModeloCobro { get; set; } = "MIXTO";
        public string Estado { get; set; } = "ABIERTO";
        public int PacientesFacturados { get; set; }
        public decimal TarifaBase { get; set; }
        public decimal CostoPacientes { get; set; }
        public decimal CostoSoporte { get; set; }
        public decimal CostoCargos { get; set; }
        public decimal Total { get; set; }
        public string Moneda { get; set; } = "USD";
        public DateTime? CerradoAt { get; set; }

        public bool EstaCerrado => Estado == "CERRADO";
    }
}
