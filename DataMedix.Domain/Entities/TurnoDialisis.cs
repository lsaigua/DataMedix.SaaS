namespace DataMedix.Domain.Entities
{
    /// <summary>
    /// Turnos de sesión de diálisis. Son los ÚNICOS valores que el cronograma
    /// sabe repartir en días, así que cualquier otro texto deja al paciente sin
    /// sesiones y sin totales.
    ///
    /// La regla vive en el dominio porque la aplican tres caminos distintos: el
    /// alta de pacientes, el ingreso manual de resultados y la importación de
    /// archivos, que suele traer cadenas como "LMXJVSAD" que no son un turno.
    /// </summary>
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
        /// Extrae el turno de un texto libre ("LMV MAÑANA", "Turno MJS").
        /// Devuelve null si no reconoce ninguno de los dos.
        /// </summary>
        public static string? Detectar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var upper = texto.ToUpperInvariant();
            if (upper.Contains(Lmv)) return Lmv;
            if (upper.Contains(Mjs)) return Mjs;
            return null;
        }

        public static bool EsValido(string? texto) => Detectar(texto) is not null;
    }

    /// <summary>Modalidad de tratamiento del paciente.</summary>
    public static class TipoAtencionPaciente
    {
        public const string Hemodialisis = "Hemodiálisis";
        public const string Peritoneal   = "Peritoneal";

        public static readonly string[] Opciones = [Hemodialisis, Peritoneal];

        /// <summary>
        /// Normaliza el texto del archivo o del formulario a una de las dos
        /// modalidades. Tolera acentos y abreviaturas ("HD", "DP", "hemodialisis").
        /// </summary>
        public static string? Detectar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            var t = texto.Trim().ToUpperInvariant()
                .Replace("Á", "A").Replace("É", "E").Replace("Í", "I")
                .Replace("Ó", "O").Replace("Ú", "U");

            if (t.Contains("HEMO") || t == "HD") return Hemodialisis;
            if (t.Contains("PERITON") || t == "DP" || t == "DPCA") return Peritoneal;
            return null;
        }
    }
}
