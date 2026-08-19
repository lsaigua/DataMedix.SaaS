namespace DataMedix.Domain.Entities
{
    /// <summary>Desglose del cobro mensual de un cliente.</summary>
    public readonly record struct ResultadoCobro(
        decimal TarifaBase,
        decimal PrecioPaciente,
        int Pacientes,
        decimal CostoPacientes,
        decimal CostoSoporte,
        decimal CostoCargos)
    {
        public decimal Total => TarifaBase + CostoPacientes + CostoSoporte + CostoCargos;
    }

    /// <summary>
    /// Cálculo puro del cobro mensual. Sin dependencias de base de datos para
    /// que la regla comercial sea única y testeable: la usan tanto la vista del
    /// cliente como el consolidado del dueño del SaaS, y ambas deben dar
    /// exactamente el mismo número.
    /// </summary>
    public static class CalculadoraCobro
    {
        /// <summary>
        /// Precio unitario aplicable al volumen del mes.
        ///
        /// Tarifa PLANA por tramo: con 150 pacientes y el tramo 101–300 a 2.80,
        /// los 150 se cobran a 2.80 (no es escalonado acumulativo). Es lo que
        /// comunica la tabla comercial al cliente.
        /// Sin tramos definidos, se usa la tarifa por paciente del tenant.
        /// </summary>
        public static decimal PrecioPorPaciente(
            IEnumerable<TenantTarifaTramo>? tramos, decimal tarifaPacienteFallback, int pacientes)
        {
            if (tramos is null) return tarifaPacienteFallback;

            var lista = tramos.OrderBy(t => t.DesdePacientes).ToList();
            if (lista.Count == 0) return tarifaPacienteFallback;

            var tramo = lista.FirstOrDefault(t => t.Contiene(pacientes));

            // Volumen por debajo del primer tramo (o cero pacientes): se aplica
            // el primero, que es el precio de entrada publicado.
            tramo ??= pacientes <= 0 ? lista[0] : lista[^1];

            return tramo.PrecioPaciente;
        }

        public static ResultadoCobro Calcular(
            string modeloCobro,
            decimal tarifaBase,
            decimal tarifaPacienteFallback,
            decimal tarifaSoporteMensual,
            IEnumerable<TenantTarifaTramo>? tramos,
            int pacientes,
            decimal cargosDelPeriodo)
        {
            var precio = PrecioPorPaciente(tramos, tarifaPacienteFallback, pacientes);

            var (baseAplicada, costoPacientes) = modeloCobro switch
            {
                // Tarifa plana: el consumo no se cobra aparte
                ModeloCobro.Suscripcion => (tarifaBase, 0m),
                // Solo consumo: no hay cargo fijo
                ModeloCobro.PorPaciente => (0m, pacientes * precio),
                // Mixto (por defecto): ambos
                _                       => (tarifaBase, pacientes * precio)
            };

            return new ResultadoCobro(
                TarifaBase:     baseAplicada,
                PrecioPaciente: modeloCobro == ModeloCobro.Suscripcion ? 0m : precio,
                Pacientes:      pacientes,
                CostoPacientes: costoPacientes,
                CostoSoporte:   tarifaSoporteMensual,
                CostoCargos:    cargosDelPeriodo);
        }
    }
}
