namespace DataMedix.Application.DTOs.EntradaManual
{
    public class EntradaManualDto
    {
        public Guid   PacienteId  { get; set; }
        public int    PeriodoAnio { get; set; }
        public int    PeriodoMes  { get; set; }

        // Panel principal — todos opcionales (al menos uno debe tener valor)
        public decimal? HbValor          { get; set; }
        public string?  HbUnidad         { get; set; } = "g/dL";

        public decimal? HierroValor      { get; set; }
        public string?  HierroUnidad     { get; set; } = "µg/dL";

        public decimal? FerritinaValor   { get; set; }
        public string?  FerritinaUnidad  { get; set; } = "ng/mL";

        public decimal? SaturacionValor  { get; set; }
        public string?  SaturacionUnidad { get; set; } = "%";

        // Parámetros adicionales (alimentan alertas clínicas)
        public decimal? PotasioValor     { get; set; }
        public decimal? AlbuminaValor    { get; set; }
        public decimal? PesoKgValor      { get; set; }

        public bool TieneAlgunValor =>
            HbValor.HasValue || HierroValor.HasValue ||
            FerritinaValor.HasValue || SaturacionValor.HasValue ||
            PotasioValor.HasValue || AlbuminaValor.HasValue || PesoKgValor.HasValue;
    }

    public class ResultadoEntradaManualDto
    {
        public bool    Exitoso           { get; init; }
        public string? MensajeError      { get; init; }
        public string? NombrePaciente    { get; init; }
        public int     PeriodoAnio       { get; init; }
        public int     PeriodoMes        { get; init; }
        public bool    PrescripcionGenerada { get; init; }
        public bool    PrescripcionBloqueada { get; init; }   // Ya aprobada — no se regeneró

        public static ResultadoEntradaManualDto Ok(string nombre, int anio, int mes, bool bloqueada = false) =>
            new() { Exitoso = true, NombrePaciente = nombre, PeriodoAnio = anio, PeriodoMes = mes,
                    PrescripcionGenerada = !bloqueada, PrescripcionBloqueada = bloqueada };

        public static ResultadoEntradaManualDto Error(string msg) =>
            new() { Exitoso = false, MensajeError = msg };
    }
}
