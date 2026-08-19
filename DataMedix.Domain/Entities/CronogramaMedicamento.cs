namespace DataMedix.Domain.Entities
{
    public class CronogramaMedicamento
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid PacienteId { get; set; }
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }
        public string? PlanSalud { get; set; }
        public decimal? EpoUiSemana { get; set; }
        public decimal? HierroMgMes { get; set; }
        // UI de EPO que no pudo distribuirse en la primera semana parcial del cronograma
        public decimal? EpoDosisPendienteUI { get; set; }
        public string? Observaciones { get; set; }
        public string Estado { get; set; } = EstadoCronograma.Borrador;
        public bool Activo { get; set; } = true;
        public bool Ausente { get; set; } = false;
        public string? MotivoAusencia { get; set; }
        public string? Sala { get; set; }
        // Modo 0=CalendarioMensual (del 1 al fin de mes), 1=Flexible (fechaInicio → +1 mes)
        public short Modo { get; set; } = ModoCronograma.CalendarioMensual;
        public DateTime? FechaInicioFlex { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }

        public Paciente Paciente { get; set; } = null!;
        public ICollection<CronogramaDia> Dias { get; set; } = new List<CronogramaDia>();
    }

    public class CronogramaDia
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CronogramaId { get; set; }
        public Guid TenantId { get; set; }
        public DateTime FechaSesion { get; set; }
        public decimal? EpoDosisuI { get; set; }
        public decimal? HierroDosismg { get; set; }
        public bool EpoEditadoManual { get; set; } = false;
        public bool HierroEditadoManual { get; set; } = false;
        public string? Observacion { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }

        public CronogramaMedicamento Cronograma { get; set; } = null!;
    }

    public class ConfiguracionMedicamento
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string Medicamento { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; } = 0;
        public string Unidad { get; set; } = "UI";
        public string? Observacion { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Tenant Tenant { get; set; } = null!;
    }

    public class CronogramaAuditoria
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid CronogramaId { get; set; }
        public Guid? DiaId { get; set; }
        public Guid? UsuarioId { get; set; }
        public string Accion { get; set; } = string.Empty;
        public string? Campo { get; set; }
        public string? ValorAnterior { get; set; }
        public string? ValorNuevo { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Precio por dosis unitaria de EPO administrada en sesión.</summary>
    public class PrecioEpoDosis
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public decimal DosisUI { get; set; }
        public decimal Precio { get; set; } = 0;
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Evento formal de dosis compensatoria generado cuando el cronograma inicia
    /// en semana parcial. Registra la fecha sugerida y permite trazabilidad de aplicación.
    /// </summary>
    public class EventoDosisPendiente
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid CronogramaId { get; set; }
        public Guid PacienteId { get; set; }
        /// <summary>Primera sesión de la semana 2 del cronograma (día sugerido de aplicación).</summary>
        public DateTime FechaProgramada { get; set; }
        public decimal DosisUI { get; set; }
        public string Estado { get; set; } = EstadoDosisPendiente.Programada;
        public string? Observaciones { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }

        public CronogramaMedicamento Cronograma { get; set; } = null!;
    }

    public static class EstadoDosisPendiente
    {
        public const string Programada = "PROGRAMADA";
        public const string Aplicada   = "APLICADA";
        public const string Suspendida = "SUSPENDIDA";
    }

    public static class EstadoCronograma
    {
        public const string Borrador = "BORRADOR";
        public const string Confirmado = "CONFIRMADO";
    }

    public static class ModoCronograma
    {
        public const short CalendarioMensual = 0;
        public const short Flexible = 1;
    }
}
