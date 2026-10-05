namespace DataMedix.Domain.Entities
{
    /// <summary>
    /// Reglas de costo de la medicación, en un solo lugar.
    ///
    /// Los precios configurados son por UNIDAD ADMINISTRABLE, nunca por unidad
    /// de medida: el de EPO es por aplicación (no por UI) y el de hierro es por
    /// ampolla de 100 mg (no por mg). Confundirlos infla el costo en varios
    /// órdenes de magnitud, así que la conversión vive aquí y no repartida por
    /// las pantallas.
    /// </summary>
    public static class CostoMedicacion
    {
        /// <summary>Miligramos de hierro IV que contiene cada ampolla.</summary>
        public const decimal MgPorAmpollaHierro = 100m;

        /// <summary>Ampollas que representan los miligramos indicados.</summary>
        public static decimal AmpollasHierro(decimal mg) => mg / MgPorAmpollaHierro;

        /// <summary>
        /// Costo del hierro IV: 400 mg con la ampolla a $1.00 son $4.00.
        /// Los miligramos que no completan una ampolla se cobran proporcionales.
        /// </summary>
        public static decimal CostoHierro(decimal mg, decimal precioAmpolla) =>
            AmpollasHierro(mg) * precioAmpolla;

        // ──────────────────────────────────────────────────────────────────────
        // EPO
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Descompone la dosis de un día en las presentaciones que SÍ tienen
        /// precio, porque el precio se cobra por vial administrado.
        ///
        /// Al concentrar la dosis semanal en pocos días aparecen valores que no
        /// están en la tabla: 12.000 UI en un solo día no tiene precio propio,
        /// pero son 3 viales de 4.000. Sin descomponer, esa dosis se cobraba en
        /// cero porque la búsqueda era por coincidencia exacta.
        ///
        /// El orden de resolución es:
        ///   1. La dosis existe en la tabla → un solo vial.
        ///   2. Es múltiplo exacto de la presentación de referencia del paciente
        ///      (la que implica su prescripción semanal) → tantos viales de esa.
        ///   3. Descomposición voraz de mayor a menor con lo disponible.
        ///
        /// Devuelve la lista de viales. Si queda un resto que ninguna
        /// presentación cubre, se informa aparte para no cobrarlo en silencio.
        /// </summary>
        public static (List<decimal> Viales, decimal Resto) DescomponerEpo(
            decimal dosisDia,
            IEnumerable<decimal> presentacionesConPrecio,
            decimal presentacionReferencia = 0)
        {
            var viales = new List<decimal>();
            if (dosisDia <= 0) return (viales, 0m);

            var disponibles = presentacionesConPrecio
                .Where(p => p > 0)
                .Distinct()
                .OrderByDescending(p => p)
                .ToList();

            if (disponibles.Count == 0) return (viales, dosisDia);

            // 1. La dosis del día es una presentación con precio propio
            if (disponibles.Contains(dosisDia))
            {
                viales.Add(dosisDia);
                return (viales, 0m);
            }

            // 2. Múltiplo exacto de la presentación que usa habitualmente el
            //    paciente: 12.000 con referencia 4.000 son 3 viales de 4.000,
            //    no 2 de 6.000. Se respeta la presentación que ya se le aplica.
            if (presentacionReferencia > 0 &&
                disponibles.Contains(presentacionReferencia) &&
                dosisDia % presentacionReferencia == 0)
            {
                var n = (int)(dosisDia / presentacionReferencia);
                for (int i = 0; i < n; i++) viales.Add(presentacionReferencia);
                return (viales, 0m);
            }

            // 3. Voraz de mayor a menor
            var restante = dosisDia;
            foreach (var p in disponibles)
            {
                while (restante >= p)
                {
                    viales.Add(p);
                    restante -= p;
                }
            }

            return (viales, restante);
        }

        /// <summary>
        /// Costo de la dosis de un día, cobrando cada vial a su precio.
        /// </summary>
        public static decimal CostoEpoDia(
            decimal dosisDia,
            IReadOnlyDictionary<decimal, decimal> precios,
            decimal presentacionReferencia = 0)
        {
            var (viales, _) = DescomponerEpo(dosisDia, precios.Keys, presentacionReferencia);
            return viales.Sum(v => precios.TryGetValue(v, out var precio) ? precio : 0m);
        }
    }
}
