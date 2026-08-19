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
    }
}
