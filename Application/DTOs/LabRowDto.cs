namespace DataMedix.Application.DTOs
{
    public class LabRowDto
    {
        public int LineNumber { get; set; }

        // Columnas crudas del Excel
        public DateTime? FechaOrden { get; set; }
        public string? PlanSalud { get; set; }
        public string? TipoAtencion { get; set; }
        public string? Identificacion { get; set; }
        public string? NombrePaciente { get; set; }
        public string? Examen { get; set; }
        public string? Parametro { get; set; }
        public string? ResultadoTexto { get; set; }
        public string? UnidadMedida { get; set; }

        // Período normalizado al primer día del mes
        public DateTime PeriodDate => FechaOrden.HasValue
            ? new DateTime(FechaOrden.Value.Year, FechaOrden.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            : new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        public bool TieneIdentificacion => !string.IsNullOrWhiteSpace(Identificacion);
        public bool TieneParametro => !string.IsNullOrWhiteSpace(Parametro);
        public bool TieneResultado => !string.IsNullOrWhiteSpace(ResultadoTexto);
    }
}
