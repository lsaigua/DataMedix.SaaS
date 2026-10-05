using DataMedix.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DataMedix.Tests.Services
{
    /// <summary>
    /// Reparto de la dosis semanal de EPO entre los días del turno.
    ///
    /// Dos invariantes que no pueden romperse nunca:
    ///   1. El total repartido en la semana es EXACTAMENTE la dosis prescrita.
    ///   2. Toda dosis es múltiplo de 2.000 UI, la presentación mínima.
    ///
    /// Violar la primera sub o sobredosifica; violar la segunda produce una
    /// dosis que no existe en ningún vial y no se puede administrar.
    /// </summary>
    public class DistribucionEpoTests
    {
        private static IReadOnlyList<DayOfWeek> Turno(string codigo) =>
            TurnoDialisis.Detectar(codigo)!;

        // ── Invariantes ───────────────────────────────────────────────────────

        [Theory]
        [InlineData("L",        2000)]
        [InlineData("L",        8000)]
        [InlineData("M",        6000)]
        [InlineData("LMJ",     12000)]
        [InlineData("LMXJV",   10000)]
        [InlineData("LMXJVSAD", 6000)]
        [InlineData("LMXJVSAD",18000)]
        [InlineData("LMXJVSAD", 2000)]
        public void El_total_semanal_se_conserva_exacto(string codigo, int uiSemana)
        {
            DistribucionEpo.TotalSemanal(uiSemana, Turno(codigo)).Should().Be(uiSemana);
        }

        [Theory]
        [InlineData("L",        8000)]
        [InlineData("LMXJVSAD", 8000)]
        [InlineData("LMXJVSAD",18000)]
        [InlineData("LMXJV",   10000)]
        public void Toda_dosis_es_multiplo_de_la_presentacion_minima(string codigo, int uiSemana)
        {
            var dosis = DistribucionEpo.PorDia(uiSemana, Turno(codigo)).Values;

            dosis.Should().OnlyContain(d => d % DistribucionEpo.PresentacionMinima == 0);
        }

        [Fact]
        public void Nunca_produce_fracciones_inadministrables()
        {
            // 8.000 UI entre 7 días serían 1.142,86 UI por sesión, que no existe.
            // El reparto por unidades lo resuelve con 4 días de 2.000 y 3 sin EPO.
            var porDia = DistribucionEpo.PorDia(8000, Turno("LMXJVSAD"));

            porDia.Values.Sum().Should().Be(8000);
            porDia.Values.Should().OnlyContain(d => d == 2000);
            porDia.Should().HaveCount(4);
        }

        // ── Casos concretos ───────────────────────────────────────────────────

        [Fact]
        public void Turno_de_un_solo_dia_lleva_toda_la_dosis_semanal()
        {
            // Decisión clínica del médico: no se topa ni se reparte solo.
            var porDia = DistribucionEpo.PorDia(8000, Turno("L"));

            porDia.Should().HaveCount(1);
            porDia[DayOfWeek.Monday].Should().Be(8000);
        }

        [Fact]
        public void Turno_diario_con_dosis_baja_deja_dias_sin_EPO()
        {
            // Se dializa los 7 días pero solo recibe EPO en 3: es lo correcto,
            // no un error de reparto.
            var porDia = DistribucionEpo.PorDia(6000, Turno("LMXJVSAD"));

            porDia.Should().HaveCount(3);
            porDia.Values.Should().OnlyContain(d => d == 2000);
            porDia.Values.Sum().Should().Be(6000);
        }

        [Fact]
        public void Turno_diario_con_dosis_alta_reparte_las_unidades_extra()
        {
            // 18.000 = 9 unidades entre 7 días → 5 días de 2.000 y 2 de 4.000
            var porDia = DistribucionEpo.PorDia(18000, Turno("LMXJVSAD"));

            porDia.Should().HaveCount(7);
            porDia.Values.Sum().Should().Be(18000);
            porDia.Values.Count(d => d == 4000).Should().Be(2);
            porDia.Values.Count(d => d == 2000).Should().Be(5);
        }

        [Fact]
        public void Las_unidades_extra_no_se_amontonan_al_inicio()
        {
            // 5 unidades entre 4 días: la extra debe caer repartida, no siempre
            // en el lunes, para no concentrar la dosis al principio de semana.
            var porDia = DistribucionEpo.PorDia(10000, Turno("LMJV"));

            porDia.Values.Sum().Should().Be(10000);
            porDia.Values.Count(d => d == 4000).Should().Be(1);
        }

        // ── Bordes ────────────────────────────────────────────────────────────

        [Fact]
        public void Sin_dosis_no_hay_reparto()
        {
            DistribucionEpo.PorDia(0, Turno("LMV")).Should().BeEmpty();
        }

        [Fact]
        public void Sin_dias_no_hay_reparto()
        {
            DistribucionEpo.PorDia(6000, Array.Empty<DayOfWeek>()).Should().BeEmpty();
        }

        [Fact]
        public void DosisTipicaPorSesion_es_la_menor_dosis_efectiva()
        {
            // Se usa para convertir UI pendientes en sesiones equivalentes
            DistribucionEpo.DosisTipicaPorSesion(18000, Turno("LMXJVSAD")).Should().Be(2000);
            DistribucionEpo.DosisTipicaPorSesion(8000,  Turno("L")).Should().Be(8000);
            DistribucionEpo.DosisTipicaPorSesion(0,     Turno("L")).Should().Be(0);
        }

        [Theory]
        [InlineData(2000,  2000)]
        [InlineData(4000,  2000)]
        [InlineData(6000,  2000)]
        [InlineData(8000,  4000)]
        [InlineData(12000, 4000)]
        [InlineData(18000, 6000)]
        public void PresentacionReferencia_es_el_vial_que_implica_la_prescripcion(int semanal, int esperado)
        {
            DistribucionEpo.PresentacionReferencia(semanal).Should().Be(esperado);
        }

        [Fact]
        public void La_referencia_no_depende_del_turno()
        {
            // 8.000 UI/sem se administran en viales de 4.000 tanto si se
            // reparten en dos días como si se concentran en uno solo.
            DistribucionEpo.PresentacionReferencia(8000).Should().Be(4000m);
        }

        [Fact]
        public void Una_dosis_semanal_fuera_de_la_tabla_no_impone_referencia()
        {
            DistribucionEpo.PresentacionReferencia(10000).Should().Be(0m);
        }
    }
}
