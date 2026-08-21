namespace DataMedix.Domain.Entities
{
    /// <summary>
    /// Turno de sesión: el conjunto de días de la semana en que el paciente se
    /// dializa. Se interpreta a partir del texto de PlanSalud.
    ///
    /// Los códigos de día son L, M (martes), X (miércoles), J, V, SA ó S
    /// (sábado) y D. Un turno se escribe concatenándolos: "L", "MJS",
    /// "LMXJVSAD" (los siete días).
    ///
    /// La regla vive en el dominio porque la aplican el alta de pacientes, el
    /// ingreso manual, el cronograma y la prescripción sugerida.
    /// </summary>
    public static class TurnoDialisis
    {
        /// <summary>
        /// Tokens de día, ordenados para lectura VORAZ: "SA" debe probarse
        /// antes que "S", o "SAD" se leería como S + A(desconocido) + D.
        /// </summary>
        private static readonly (string Codigo, DayOfWeek Dia)[] Tokens =
        [
            ("SA", DayOfWeek.Saturday),
            ("L",  DayOfWeek.Monday),
            ("M",  DayOfWeek.Tuesday),
            ("X",  DayOfWeek.Wednesday),
            ("J",  DayOfWeek.Thursday),
            ("V",  DayOfWeek.Friday),
            ("S",  DayOfWeek.Saturday),
            ("D",  DayOfWeek.Sunday),
        ];

        /// <summary>
        /// Códigos históricos que NO se pueden leer token a token.
        ///
        /// En "LMV" la M significa MIÉRCOLES, no martes: es la abreviatura
        /// clásica de lunes-miércoles-viernes. Tokenizarla daría lunes, martes
        /// y viernes, y pondría las dosis en días equivocados para los 172
        /// pacientes que hoy usan ese código. Por eso se resuelven primero,
        /// como caso exacto, antes de intentar la lectura por tokens.
        /// </summary>
        private static readonly Dictionary<string, DayOfWeek[]> Compuestos =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["LMV"] = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
                ["MJS"] = [DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday],
            };

        public const string Lmv = "LMV";
        public const string Mjs = "MJS";
        public const string TodosLosDias = "LMXJVSAD";

        /// <summary>Opciones que se ofrecen en los formularios.</summary>
        public static readonly (string Valor, string Etiqueta)[] Opciones =
        [
            ("L",           "L — Lunes"),
            ("M",           "M — Martes"),
            ("X",           "X — Miércoles"),
            ("J",           "J — Jueves"),
            ("V",           "V — Viernes"),
            ("SA",          "SA — Sábado"),
            ("D",           "D — Domingo"),
            (Lmv,           "LMV — Lunes, Miércoles y Viernes"),
            (Mjs,           "MJS — Martes, Jueves y Sábado"),
            (TodosLosDias,  "LMXJVSAD — Todos los días"),
        ];

        /// <summary>
        /// Días de sesión del turno, en orden de la semana. Devuelve null si el
        /// texto no se puede interpretar como turno.
        ///
        /// Tolera prefijos históricos: "1er LMV", "3er MJS" y similares.
        /// </summary>
        public static IReadOnlyList<DayOfWeek>? Detectar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            var limpio = new string(texto.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            if (limpio.Length == 0) return null;

            // 1. Códigos compuestos históricos, por coincidencia EXACTA
            if (Compuestos.TryGetValue(limpio, out var exacto))
                return exacto;

            // 2. Lectura token a token. Va antes que la búsqueda por subcadena
            //    porque un código legítimo puede contener a uno compuesto:
            //    "LMVSA" es lunes-martes-viernes-sábado, y buscar "LMV" primero
            //    se habría quedado con tres días y perdido el sábado.
            var tokenizado = Tokenizar(limpio);
            if (tokenizado is not null) return tokenizado;

            // 3. Respaldo para prefijos históricos ("1er LMV", "3er MJS"), que
            //    no tokenizan porque arrastran letras que no son días. Solo
            //    estos dos códigos se buscan por subcadena: hacerlo con los de
            //    una letra daría falsos positivos en cualquier palabra.
            foreach (var (codigo, dias) in Compuestos)
                if (limpio.Contains(codigo, StringComparison.Ordinal))
                    return dias;

            return null;
        }

        private static IReadOnlyList<DayOfWeek>? Tokenizar(string limpio)
        {
            var dias = new List<DayOfWeek>();
            var i = 0;

            while (i < limpio.Length)
            {
                var avanzo = false;

                foreach (var (codigo, dia) in Tokens)
                {
                    if (i + codigo.Length > limpio.Length) continue;
                    if (string.CompareOrdinal(limpio, i, codigo, 0, codigo.Length) != 0) continue;

                    if (!dias.Contains(dia)) dias.Add(dia);
                    i += codigo.Length;
                    avanzo = true;
                    break;
                }

                // Un carácter que no es día invalida el turno completo: es
                // preferible no programar a programar en días inventados.
                if (!avanzo) return null;
            }

            return dias.Count == 0 ? null : OrdenarSemana(dias);
        }

        /// <summary>Ordena de lunes a domingo, no por el valor del enum (que empieza en domingo).</summary>
        private static List<DayOfWeek> OrdenarSemana(IEnumerable<DayOfWeek> dias) =>
            dias.OrderBy(d => d == DayOfWeek.Sunday ? 7 : (int)d).ToList();

        public static bool EsValido(string? texto) => Detectar(texto) is not null;

        /// <summary>
        /// Código canónico del turno: en mayúsculas, sin prefijos ni separadores.
        /// "1er LMV" → "LMV", "lmxjvsad" → "LMXJVSAD". Null si no es un turno.
        ///
        /// Los compuestos conservan su escritura clásica: LMV no se reescribe
        /// como "LXV" aunque esos sean sus días, porque LMV es lo que el
        /// personal clínico escribe y reconoce.
        /// </summary>
        public static string? Normalizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            var limpio = new string(texto.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            if (limpio.Length == 0) return null;

            if (Compuestos.ContainsKey(limpio)) return limpio;
            if (Tokenizar(limpio) is not null)  return limpio;

            foreach (var codigo in Compuestos.Keys)
                if (limpio.Contains(codigo, StringComparison.Ordinal))
                    return codigo;

            return null;
        }

        /// <summary>Cantidad de sesiones semanales del turno. 0 si no es válido.</summary>
        public static int SesionesPorSemana(string? texto) => Detectar(texto)?.Count ?? 0;

        // ──────────────────────────────────────────────────────────────────────
        // REGLAS DE SELECCIÓN
        //
        // Viven aquí y no en cada pantalla: las aplican el cronograma, la
        // prescripción sugerida y el alta de pacientes, y si divergieran un
        // mismo paciente podría acabar con turnos distintos según dónde se
        // edite.
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Códigos que describen una semana completa por sí solos.</summary>
        public static readonly string[] Combinados = [TodosLosDias, Lmv, Mjs];

        /// <summary>Códigos de un solo día, en orden de semana.</summary>
        public static readonly string[] DiasSueltos = ["L", "M", "X", "J", "V", "SA", "D"];

        public static bool EsCombinado(string codigo)  => Combinados.Contains(codigo, StringComparer.OrdinalIgnoreCase);
        public static bool EsDiaSuelto(string codigo)  => DiasSueltos.Contains(codigo, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// ¿Se puede añadir este código a la selección actual?
        ///
        /// • Un combinado es exclusivo: no convive con nada más.
        /// • Los días sueltos se combinan entre ellos, pero no con un combinado.
        ///   Un turno no puede ser "LMV y además el martes": o se describe por
        ///   combinado o se describe día a día.
        ///
        /// Lo ya seleccionado siempre se puede quitar.
        /// </summary>
        public static bool PuedeSeleccionar(IReadOnlyCollection<string> seleccion, string codigo)
        {
            if (seleccion.Contains(codigo, StringComparer.OrdinalIgnoreCase)) return true;

            var hayCombinado = seleccion.Any(EsCombinado);
            var hayDiaSuelto = seleccion.Any(EsDiaSuelto);

            return EsCombinado(codigo)
                ? !hayCombinado && !hayDiaSuelto
                : !hayCombinado;
        }

        /// <summary>
        /// Añade o quita un código respetando las reglas. Devuelve la selección
        /// resultante; elegir un combinado descarta lo anterior.
        /// </summary>
        public static List<string> Alternar(IReadOnlyCollection<string> seleccion, string codigo)
        {
            var resultado = seleccion.ToList();

            if (resultado.RemoveAll(c => string.Equals(c, codigo, StringComparison.OrdinalIgnoreCase)) > 0)
                return resultado;

            if (!PuedeSeleccionar(seleccion, codigo)) return resultado;

            if (EsCombinado(codigo)) resultado.Clear();
            resultado.Add(codigo);
            return resultado;
        }

        /// <summary>
        /// Código único a partir de la selección. Los días sueltos se concatenan
        /// en orden de semana, no en el orden en que se fueron pulsando: elegir
        /// primero J y luego L debe producir "LJ", no "JL".
        /// </summary>
        public static string Componer(IReadOnlyCollection<string> seleccion)
        {
            if (seleccion.Count == 0) return "";

            var combinado = seleccion.FirstOrDefault(EsCombinado);
            if (combinado is not null) return combinado.ToUpperInvariant();

            return string.Concat(
                DiasSueltos.Where(d => seleccion.Contains(d, StringComparer.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Selección equivalente a un código guardado, para reabrir el selector
        /// con lo que el paciente ya tiene.
        /// </summary>
        public static List<string> Descomponer(string? codigo)
        {
            var canonico = Normalizar(codigo);
            if (canonico is null) return [];

            if (EsCombinado(canonico)) return [canonico];

            var dias = Detectar(canonico);
            if (dias is null) return [];

            return DiasSueltos
                .Where(d => Detectar(d) is { Count: 1 } uno && dias.Contains(uno[0]))
                .ToList();
        }

        /// <summary>Etiqueta legible del turno, para pantallas y reportes.</summary>
        public static string Describir(string? texto)
        {
            var dias = Detectar(texto);
            if (dias is null) return "Sin turno";

            var nombres = dias.Select(d => d switch
            {
                DayOfWeek.Monday    => "Lun",
                DayOfWeek.Tuesday   => "Mar",
                DayOfWeek.Wednesday => "Mié",
                DayOfWeek.Thursday  => "Jue",
                DayOfWeek.Friday    => "Vie",
                DayOfWeek.Saturday  => "Sáb",
                _                   => "Dom",
            });

            return string.Join(", ", nombres);
        }

        /// <summary>
        /// Patrones clásicos de 3 sesiones. Se distinguen porque conservan su
        /// tabla de dosificación validada, distinta del reparto general.
        /// </summary>
        public static bool EsPatronClasico(IReadOnlyList<DayOfWeek>? dias)
        {
            if (dias is null || dias.Count != 3) return false;

            var set = dias.ToHashSet();
            return set.SetEquals(Compuestos[Lmv]) || set.SetEquals(Compuestos[Mjs]);
        }
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
