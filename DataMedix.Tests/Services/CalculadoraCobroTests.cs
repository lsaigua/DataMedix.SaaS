using DataMedix.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DataMedix.Tests.Services
{
    /// <summary>
    /// Tests de la regla comercial de cobro mensual.
    ///
    /// Es el cálculo que ve el cliente en su pantalla de Facturación y el que el
    /// dueño del SaaS usa para consolidar lo que debe cobrar: ambos deben dar
    /// exactamente el mismo número, por eso vive en un único lugar sin
    /// dependencias de base de datos.
    ///
    /// Tramos publicados: 1–100 → 3.50 | 101–300 → 2.80 | 301+ → 2.20
    /// La tarifa es PLANA por tramo, no escalonada acumulativa.
    /// </summary>
    public class CalculadoraCobroTests
    {
        private static List<TenantTarifaTramo> TramosComerciales() =>
        [
            new() { DesdePacientes = 1,   HastaPacientes = 100,  PrecioPaciente = 3.50m },
            new() { DesdePacientes = 101, HastaPacientes = 300,  PrecioPaciente = 2.80m },
            new() { DesdePacientes = 301, HastaPacientes = null, PrecioPaciente = 2.20m },
        ];

        // ── Selección de tramo ────────────────────────────────────────────────

        [Theory]
        [InlineData(1,   3.50)]
        [InlineData(100, 3.50)]
        [InlineData(101, 2.80)]
        [InlineData(300, 2.80)]
        [InlineData(301, 2.20)]
        [InlineData(999, 2.20)]
        public void PrecioPorPaciente_aplica_el_tramo_del_volumen(int pacientes, decimal esperado)
        {
            var precio = CalculadoraCobro.PrecioPorPaciente(TramosComerciales(), 0m, pacientes);

            precio.Should().Be(esperado);
        }

        [Fact]
        public void PrecioPorPaciente_sin_tramos_usa_la_tarifa_del_tenant()
        {
            var precio = CalculadoraCobro.PrecioPorPaciente(new List<TenantTarifaTramo>(), 4.25m, 150);

            precio.Should().Be(4.25m);
        }

        [Fact]
        public void PrecioPorPaciente_sin_pacientes_usa_el_precio_de_entrada()
        {
            // Con cero pacientes no hay tramo que "contenga" el volumen; se toma
            // el primero, que es el precio publicado de entrada.
            var precio = CalculadoraCobro.PrecioPorPaciente(TramosComerciales(), 0m, 0);

            precio.Should().Be(3.50m);
        }

        // ── Modelos de cobro ──────────────────────────────────────────────────

        [Fact]
        public void Mixto_suma_base_mas_consumo()
        {
            var r = CalculadoraCobro.Calcular(
                ModeloCobro.Mixto, tarifaBase: 349m, tarifaPacienteFallback: 0m,
                tarifaSoporteMensual: 0m, TramosComerciales(), pacientes: 150, cargosDelPeriodo: 0m);

            r.CostoPacientes.Should().Be(420m);   // 150 × 2.80
            r.Total.Should().Be(769m);            // 349 + 420
        }

        [Fact]
        public void Suscripcion_ignora_el_consumo()
        {
            var r = CalculadoraCobro.Calcular(
                ModeloCobro.Suscripcion, tarifaBase: 699m, tarifaPacienteFallback: 0m,
                tarifaSoporteMensual: 0m, TramosComerciales(), pacientes: 500, cargosDelPeriodo: 0m);

            r.CostoPacientes.Should().Be(0m);
            r.PrecioPaciente.Should().Be(0m);
            r.Total.Should().Be(699m);
        }

        [Fact]
        public void PorPaciente_no_cobra_cargo_base()
        {
            var r = CalculadoraCobro.Calcular(
                ModeloCobro.PorPaciente, tarifaBase: 349m, tarifaPacienteFallback: 0m,
                tarifaSoporteMensual: 0m, TramosComerciales(), pacientes: 400, cargosDelPeriodo: 0m);

            r.TarifaBase.Should().Be(0m);
            r.CostoPacientes.Should().Be(880m);   // 400 × 2.20
            r.Total.Should().Be(880m);
        }

        // ── Recurrentes y cargos puntuales ────────────────────────────────────

        [Fact]
        public void Soporte_y_cargos_se_suman_en_cualquier_modelo()
        {
            var r = CalculadoraCobro.Calcular(
                ModeloCobro.Mixto, tarifaBase: 349m, tarifaPacienteFallback: 0m,
                tarifaSoporteMensual: 200m, TramosComerciales(),
                pacientes: 50, cargosDelPeriodo: 1500m);

            r.CostoPacientes.Should().Be(175m);   // 50 × 3.50
            r.CostoSoporte.Should().Be(200m);
            r.CostoCargos.Should().Be(1500m);
            r.Total.Should().Be(2224m);           // 349 + 175 + 200 + 1500
        }

        [Fact]
        public void Sin_tarifas_configuradas_el_total_es_cero()
        {
            var r = CalculadoraCobro.Calcular(
                ModeloCobro.Mixto, 0m, 0m, 0m,
                new List<TenantTarifaTramo>(), pacientes: 163, cargosDelPeriodo: 0m);

            r.Total.Should().Be(0m);
        }

        [Fact]
        public void Un_paciente_de_mas_puede_bajar_el_total_al_cruzar_de_tramo()
        {
            // Consecuencia conocida de la tarifa plana: al cruzar a un tramo más
            // barato el total baja. Se documenta con un test para que el día que
            // alguien lo reporte como bug quede claro que es la regla comercial.
            var con100 = CalculadoraCobro.Calcular(
                ModeloCobro.PorPaciente, 0m, 0m, 0m, TramosComerciales(), 100, 0m);
            var con101 = CalculadoraCobro.Calcular(
                ModeloCobro.PorPaciente, 0m, 0m, 0m, TramosComerciales(), 101, 0m);

            con100.Total.Should().Be(350m);       // 100 × 3.50
            con101.Total.Should().Be(282.80m);    // 101 × 2.80
            con101.Total.Should().BeLessThan(con100.Total);
        }
    }
}
