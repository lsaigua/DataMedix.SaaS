namespace DataMedix.Domain.Entities
{
    public class ResultadoLaboratorio
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid PacienteId { get; set; }
        public Guid LoteId { get; set; }
        public Guid? ParametroClinicoId { get; set; }

        // Periodo normalizado (siempre primer día del mes)
        public DateTime PeriodDate { get; set; }
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }

        // Datos clínicos del Excel
        public string? PlanSalud { get; set; }
        public string? TipoAtencion { get; set; }
        public DateTime? FechaOrden { get; set; }
        public string? ExamenRaw { get; set; }
        public string? ParametroRaw { get; set; }
        public string ResultadoTexto { get; set; } = null!;
        public decimal? ValorNumerico { get; set; }
        public string? UnidadMedida { get; set; }
        public decimal? ValorMinReferencia { get; set; }
        public decimal? ValorMaxReferencia { get; set; }
        public bool EsPatologico { get; set; } = false;

        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedBy { get; set; }

        public Paciente Paciente { get; set; } = null!;
        public LoteImportacion Lote { get; set; } = null!;
        public ParametroClinico? ParametroClinico { get; set; }

        public static DateTime NormalizarPeriod(DateTime fecha) =>
            new DateTime(fecha.Year, fecha.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        public void CalcularPatologia()
        {
            if (!ValorNumerico.HasValue) { EsPatologico = false; return; }
            if (!ValorMinReferencia.HasValue || !ValorMaxReferencia.HasValue) { EsPatologico = false; return; }

            EsPatologico = ValorNumerico.Value < ValorMinReferencia.Value ||
                           ValorNumerico.Value > ValorMaxReferencia.Value;
        }
    }
}
