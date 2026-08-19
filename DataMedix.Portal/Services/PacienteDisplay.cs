namespace DataMedix.Portal.Services
{
    /// <summary>
    /// Enmascaramiento visual de nombres de pacientes.
    /// SOLO afecta la presentación en pantalla: el dato real nunca se modifica,
    /// por lo que búsquedas, filtros, exportaciones a Excel y persistencia
    /// siguen operando sobre el nombre completo.
    /// </summary>
    public static class PacienteDisplay
    {
        /// <summary>Bloque fijo de enmascaramiento (no revela la longitud real del nombre).</summary>
        private const string Mascara = "XXXXXXXXXXX";

        /// <summary>
        /// Deja visible únicamente el primer nombre.
        /// Ej: "LUIS MANUEL PEREZ OÑA" → "LUIS XXXXXXXXXXX".
        /// </summary>
        public static string Mask(string? nombreCompleto)
        {
            if (string.IsNullOrWhiteSpace(nombreCompleto))
                return nombreCompleto ?? string.Empty;

            var texto = nombreCompleto.Trim();
            var corte = texto.IndexOf(' ');

            // Un solo token: no hay apellidos que ocultar.
            return corte < 0 ? texto : $"{texto[..corte]} {Mascara}";
        }

        /// <summary>
        /// Nombre enmascarado y truncado para celdas o etiquetas con ancho limitado.
        /// </summary>
        public static string Mask(string? nombreCompleto, int maxLength)
        {
            var enmascarado = Mask(nombreCompleto);
            return enmascarado.Length <= maxLength
                ? enmascarado
                : enmascarado[..maxLength] + "…";
        }

        /// <summary>
        /// Inicial para avatares. Proviene del primer nombre, que permanece visible.
        /// </summary>
        public static string Inicial(string? nombreCompleto) =>
            string.IsNullOrWhiteSpace(nombreCompleto)
                ? "?"
                : char.ToUpperInvariant(nombreCompleto.Trim()[0]).ToString();
    }
}
