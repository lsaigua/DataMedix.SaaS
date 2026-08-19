namespace DataMedix.Application.DTOs
{
    /// <summary>
    /// Vista de facturación de un período. Si el período está cerrado los datos
    /// vienen del libro inmutable; si está abierto se calculan en vivo.
    /// </summary>
    public class FacturacionPeriodoDto
    {
        public Guid? Id { get; set; }
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string? PlanNombre { get; set; }
        public string ModeloCobro { get; set; } = "MIXTO";
        public decimal TarifaBase { get; set; }
        public decimal TarifaPaciente { get; set; }
        public decimal CostoSoporte { get; set; }
        public decimal CostoCargos { get; set; }
        public string Moneda { get; set; } = "USD";
        public string Estado { get; set; } = "ABIERTO";
        public DateTime? CerradoAt { get; set; }

        /// <summary>Detalle de los cargos puntuales incluidos en el período.</summary>
        public List<CargoUnicoDto> Cargos { get; set; } = new();

        public List<FacturacionPacienteDto> Pacientes { get; set; } = new();

        public int PacientesFacturados => Pacientes.Count;
        public decimal CostoPacientes => PacientesFacturados * TarifaPaciente;
        public decimal Total => TarifaBase + CostoPacientes + CostoSoporte + CostoCargos;

        /// <summary>True si el tenant no tiene ninguna tarifa configurada.</summary>
        public bool SinTarifas =>
            TarifaBase == 0 && TarifaPaciente == 0 && CostoSoporte == 0 && CostoCargos == 0;

        public bool EstaCerrado => Estado == "CERRADO";
        public string NombrePeriodo =>
            new DateTime(Anio, Mes, 1).ToString("MMMM yyyy");

        public int Activos   => Pacientes.Count(p => p.EstadoPaciente == "ACTIVO");
        public int Bajas     => Pacientes.Count(p => p.EstadoPaciente == "BAJA");
        public int Eliminados => Pacientes.Count(p => p.EstadoPaciente == "ELIMINADO");
    }

    public class FacturacionPacienteDto
    {
        public Guid PacienteId { get; set; }
        public string? Identificacion { get; set; }
        public string? NombreCompleto { get; set; }
        public bool TuvoLaboratorio { get; set; }
        public bool TuvoSnapshot { get; set; }
        public bool TuvoPrescripcion { get; set; }
        public bool TuvoCronograma { get; set; }
        public string EstadoPaciente { get; set; } = "ACTIVO";
        public decimal TarifaAplicada { get; set; }

        /// <summary>Resumen legible del origen de la actividad facturable.</summary>
        public string Origenes
        {
            get
            {
                var partes = new List<string>(4);
                if (TuvoLaboratorio)  partes.Add("Laboratorio");
                if (TuvoSnapshot)     partes.Add("Snapshot");
                if (TuvoPrescripcion) partes.Add("Prescripción");
                if (TuvoCronograma)   partes.Add("Cronograma");
                return partes.Count == 0 ? "—" : string.Join(" · ", partes);
            }
        }
    }

    /// <summary>Fila del histórico de períodos facturados.</summary>
    public class FacturacionResumenDto
    {
        public Guid Id { get; set; }
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string Estado { get; set; } = "ABIERTO";
        public int PacientesFacturados { get; set; }
        public decimal Total { get; set; }
        public DateTime? CerradoAt { get; set; }

        public string NombrePeriodo => new DateTime(Anio, Mes, 1).ToString("MMMM yyyy");
    }
}
