namespace DataMedix.Application.DTOs.EntradaManual
{
    public class EntradaManualDto
    {
        public Guid   PacienteId  { get; set; }
        public int    PeriodoAnio { get; set; }
        public int    PeriodoMes  { get; set; }

        /// <summary>
        /// Turno de diálisis: LMV o MJS. Es lo que usa el cronograma para saber
        /// en qué días hay sesión; sin turno el paciente aparece sin días ni
        /// totales. En la importación llega en la columna Plan Salud del Excel.
        /// </summary>
        public string?  Turno        { get; set; }
        public string?  TipoAtencion { get; set; }

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

        /// <summary>True si el formulario se prellenó con datos ya guardados.</summary>
        public bool EsEdicion { get; set; }
    }

    /// <summary>Turnos de sesión reconocidos por el cronograma.</summary>
    public static class TurnoDialisis
    {
        public const string Lmv = "LMV";
        public const string Mjs = "MJS";

        public static readonly (string Valor, string Etiqueta)[] Opciones =
        [
            (Lmv, "LMV — Lunes, Miércoles y Viernes"),
            (Mjs, "MJS — Martes, Jueves y Sábado")
        ];

        /// <summary>
        /// Extrae el turno de un texto libre (el Excel trae cosas como
        /// "LMV MAÑANA" o "Turno MJS"). Devuelve null si no reconoce ninguno.
        /// </summary>
        public static string? Detectar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var upper = texto.ToUpperInvariant();
            if (upper.Contains(Lmv)) return Lmv;
            if (upper.Contains(Mjs)) return Mjs;
            return null;
        }
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
