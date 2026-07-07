using DataMedix.Application.Services;
using FluentAssertions;
using Xunit;

namespace DataMedix.Tests.Services
{
    /// <summary>
    /// Tests del algoritmo de distribución clínica de Hierro IV.
    /// Casos verificados: turno LMV/MJS, 100 mg, 200 mg, 600 mg, 1000 mg,
    /// bordes (0 mg, null, más apps que sesiones) y separación mínima 48 h.
    /// </summary>
    public class HierroSchedulerServiceTests
    {
        private readonly HierroSchedulerService _sut = new();

        // ── Sesiones LMV de un mes típico (13 sesiones) ────────────────────
        private static readonly List<DateTime> LmvMarzo2025 = new()
        {
            new DateTime(2025, 3, 3),  new DateTime(2025, 3, 5),  new DateTime(2025, 3, 7),
            new DateTime(2025, 3, 10), new DateTime(2025, 3, 12), new DateTime(2025, 3, 14),
            new DateTime(2025, 3, 17), new DateTime(2025, 3, 19), new DateTime(2025, 3, 21),
            new DateTime(2025, 3, 24), new DateTime(2025, 3, 26), new DateTime(2025, 3, 28),
            new DateTime(2025, 3, 31)
        };

        // ── Sesiones MJS de un mes típico (13 sesiones) ────────────────────
        private static readonly List<DateTime> MjsMarzo2025 = new()
        {
            new DateTime(2025, 3, 4),  new DateTime(2025, 3, 6),  new DateTime(2025, 3, 8),
            new DateTime(2025, 3, 11), new DateTime(2025, 3, 13), new DateTime(2025, 3, 15),
            new DateTime(2025, 3, 18), new DateTime(2025, 3, 20), new DateTime(2025, 3, 22),
            new DateTime(2025, 3, 25), new DateTime(2025, 3, 27), new DateTime(2025, 3, 29),
        };

        [Fact]
        public void Hierro0_RetornaListaVacia()
        {
            _sut.GenerarFechasAplicacion(0, LmvMarzo2025).Should().BeEmpty();
        }

        [Fact]
        public void HierroNull_RetornaListaVacia()
        {
            _sut.GenerarFechasAplicacion(null, LmvMarzo2025).Should().BeEmpty();
        }

        [Fact]
        public void SinSesiones_RetornaListaVacia()
        {
            _sut.GenerarFechasAplicacion(600, new List<DateTime>()).Should().BeEmpty();
        }

        [Fact]
        public void ConstanteDosisAplicacionEs100mg()
        {
            HierroSchedulerService.DosisAplicacion.Should().Be(100m);
        }

        [Theory]
        [InlineData(100, 1)]
        [InlineData(200, 2)]
        [InlineData(300, 3)]
        [InlineData(600, 6)]
        [InlineData(1000, 10)]
        public void NumeroDeAplicacionesCorrecto_LMV(decimal hierroMgMes, int esperado)
        {
            var result = _sut.GenerarFechasAplicacion(hierroMgMes, LmvMarzo2025);
            result.Should().HaveCount(esperado);
        }

        [Theory]
        [InlineData(100, 1)]
        [InlineData(200, 2)]
        [InlineData(600, 6)]
        public void NumeroDeAplicacionesCorrecto_MJS(decimal hierroMgMes, int esperado)
        {
            var result = _sut.GenerarFechasAplicacion(hierroMgMes, MjsMarzo2025);
            result.Should().HaveCount(esperado);
        }

        [Fact]
        public void TodasLasFechasEstanEnLaListaDeSesiones()
        {
            var result = _sut.GenerarFechasAplicacion(600, LmvMarzo2025);
            result.Should().OnlyContain(f => LmvMarzo2025.Contains(f));
        }

        [Fact]
        public void SeparacionMinima48hEntreFechas()
        {
            var result = _sut.GenerarFechasAplicacion(600, LmvMarzo2025).OrderBy(d => d).ToList();
            for (int i = 1; i < result.Count; i++)
                (result[i] - result[i - 1]).TotalHours.Should().BeGreaterThanOrEqualTo(48);
        }

        [Fact]
        public void Hierro600_DistribucionUniforme_13Sesiones()
        {
            // 6 apps sobre 13 sesiones → deben cubrir el mes entero (1ra y última semana incluidas)
            var result = _sut.GenerarFechasAplicacion(600, LmvMarzo2025).OrderBy(d => d).ToList();
            result.First().Should().Be(LmvMarzo2025.First(), "debe comenzar desde el inicio del mes");
            result.Last().Should().Be(LmvMarzo2025.Last(), "debe llegar hasta el final del mes");
        }

        [Fact]
        public void Hierro100_AplicacionCentral()
        {
            // 1 app → sesión central del mes (índice m/2)
            var result = _sut.GenerarFechasAplicacion(100, LmvMarzo2025);
            result.Should().HaveCount(1);
            result[0].Should().Be(LmvMarzo2025[LmvMarzo2025.Count / 2]);
        }

        [Fact]
        public void Hierro1300_CapaAlMaximoDeSesiones_13()
        {
            // 13 apps = igual a sesiones disponibles → debe devolver las 13
            var result = _sut.GenerarFechasAplicacion(1300, LmvMarzo2025);
            result.Should().HaveCount(13);
            result.Should().BeEquivalentTo(LmvMarzo2025);
        }

        [Fact]
        public void HierroMayorQueSesionesDisponibles_CapaAlMax()
        {
            // 20 apps pero solo 13 sesiones → se capan en 13
            var result = _sut.GenerarFechasAplicacion(2000, LmvMarzo2025);
            result.Should().HaveCount(13);
        }

        [Fact]
        public void FechasOrdenadas()
        {
            var result = _sut.GenerarFechasAplicacion(600, LmvMarzo2025);
            result.Should().BeInAscendingOrder();
        }

        [Fact]
        public void SinDuplicados()
        {
            var result = _sut.GenerarFechasAplicacion(600, LmvMarzo2025);
            result.Distinct().Should().HaveCount(result.Count);
        }
    }
}
