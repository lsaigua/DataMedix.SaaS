using DataMedix.Application.Services;
using FluentAssertions;
using Xunit;

namespace DataMedix.Tests.Services
{
    /// <summary>
    /// Tests del algoritmo de distribución clínica de Hierro IV.
    ///
    /// Algoritmo: n = ceil(HierroMgMes / 200), dosis/app = HierroMgMes/n,
    /// distribución centrada por segmentos: idx = round((2i+1)*m/(2n)).
    ///
    /// LMV Marzo 2025: 13 sesiones (índices 0–12, días 3,5,7,10,12,14,17,19,21,24,26,28,31)
    /// MJS Marzo 2025: 12 sesiones (días 4,6,8,11,13,15,18,20,22,25,27,29)
    /// </summary>
    public class HierroSchedulerServiceTests
    {
        private readonly HierroSchedulerService _sut = new();

        private static readonly List<DateTime> LmvMarzo2025 = new()
        {
            new DateTime(2025, 3, 3),  new DateTime(2025, 3, 5),  new DateTime(2025, 3, 7),
            new DateTime(2025, 3, 10), new DateTime(2025, 3, 12), new DateTime(2025, 3, 14),
            new DateTime(2025, 3, 17), new DateTime(2025, 3, 19), new DateTime(2025, 3, 21),
            new DateTime(2025, 3, 24), new DateTime(2025, 3, 26), new DateTime(2025, 3, 28),
            new DateTime(2025, 3, 31)
        };

        private static readonly List<DateTime> MjsMarzo2025 = new()
        {
            new DateTime(2025, 3, 4),  new DateTime(2025, 3, 6),  new DateTime(2025, 3, 8),
            new DateTime(2025, 3, 11), new DateTime(2025, 3, 13), new DateTime(2025, 3, 15),
            new DateTime(2025, 3, 18), new DateTime(2025, 3, 20), new DateTime(2025, 3, 22),
            new DateTime(2025, 3, 25), new DateTime(2025, 3, 27), new DateTime(2025, 3, 29),
        };

        // ── Bordes ──────────────────────────────────────────────────────────
        [Fact]
        public void Hierro0_RetornaListaVacia()
            => _sut.GenerarFechasAplicacion(0, LmvMarzo2025).Should().BeEmpty();

        [Fact]
        public void HierroNull_RetornaListaVacia()
            => _sut.GenerarFechasAplicacion(null, LmvMarzo2025).Should().BeEmpty();

        [Fact]
        public void SinSesiones_RetornaListaVacia()
            => _sut.GenerarFechasAplicacion(600, new List<DateTime>()).Should().BeEmpty();

        [Fact]
        public void ConstanteMaxDosisAplicacionEs200mg()
            => HierroSchedulerService.MaxDosisAplicacion.Should().Be(200m);

        // ── Número de aplicaciones: n = ceil(HierroMgMes / 200) ─────────────
        [Theory]
        [InlineData(100,  1)]   // ceil(100/200) = 1
        [InlineData(200,  1)]   // ceil(200/200) = 1
        [InlineData(300,  2)]   // ceil(300/200) = 2
        [InlineData(400,  2)]   // ceil(400/200) = 2
        [InlineData(600,  3)]   // ceil(600/200) = 3
        [InlineData(1000, 5)]   // ceil(1000/200) = 5
        public void NumeroDeAplicacionesCorrecto_LMV(decimal hierroMgMes, int esperado)
            => _sut.GenerarFechasAplicacion(hierroMgMes, LmvMarzo2025).Should().HaveCount(esperado);

        [Theory]
        [InlineData(100,  1)]
        [InlineData(200,  1)]
        [InlineData(600,  3)]
        public void NumeroDeAplicacionesCorrecto_MJS(decimal hierroMgMes, int esperado)
            => _sut.GenerarFechasAplicacion(hierroMgMes, MjsMarzo2025).Should().HaveCount(esperado);

        // ── Distribución centrada ────────────────────────────────────────────
        [Fact]
        public void Hierro200_AplicacionEnMitadDelMes_LMV()
        {
            // n=1, m=13: idx = round(1*13/2) = round(6.5) = 6 (banker's) → sessions[6] = Mar 17
            var result = _sut.GenerarFechasAplicacion(200, LmvMarzo2025);
            result.Should().HaveCount(1);
            result[0].Should().Be(new DateTime(2025, 3, 17), "200mg → 1 aplicación en sesión central del mes");
        }

        [Fact]
        public void Hierro100_AplicacionEnMitadDelMes()
        {
            // n=1 (mismo que 200mg), dosis = 100mg en la sesión central
            var result = _sut.GenerarFechasAplicacion(100, LmvMarzo2025);
            result.Should().HaveCount(1);
            result[0].Should().Be(LmvMarzo2025[LmvMarzo2025.Count / 2]);
        }

        [Fact]
        public void Hierro400_DosAplicaciones_Centradas_15DiasApart_LMV()
        {
            // n=2, m=13: idx[0]=round(1*13/4)=round(3.25)=3 → Mar10
            //            idx[1]=round(3*13/4)=round(9.75)=10 → Mar26
            var result = _sut.GenerarFechasAplicacion(400, LmvMarzo2025).OrderBy(d => d).ToList();
            result.Should().HaveCount(2);
            result[0].Should().Be(new DateTime(2025, 3, 10));
            result[1].Should().Be(new DateTime(2025, 3, 26));
            (result[1] - result[0]).Days.Should().BeGreaterThan(14,
                "las dos aplicaciones deben estar al menos 15 días separadas");
        }

        [Fact]
        public void Hierro600_TresAplicaciones_Centradas_LMV()
        {
            // n=3, m=13: idx[0]=round(1*13/6)=round(2.17)=2 → Mar7
            //            idx[1]=round(3*13/6)=round(6.5)=6   → Mar17
            //            idx[2]=round(5*13/6)=round(10.83)=11 → Mar28
            var result = _sut.GenerarFechasAplicacion(600, LmvMarzo2025).OrderBy(d => d).ToList();
            result.Should().HaveCount(3);
            result[0].Should().Be(new DateTime(2025, 3, 7));
            result[1].Should().Be(new DateTime(2025, 3, 17));
            result[2].Should().Be(new DateTime(2025, 3, 28));
        }

        [Fact]
        public void Hierro600_NoComienzaEnPrimeraSesion_NiTerminaEnUltima()
        {
            // La distribución centrada NO va del borde al borde
            var result = _sut.GenerarFechasAplicacion(600, LmvMarzo2025).OrderBy(d => d).ToList();
            result.First().Should().NotBe(LmvMarzo2025.First(), "distribución centrada, no inicia en primera sesión");
            result.Last().Should().NotBe(LmvMarzo2025.Last(), "distribución centrada, no termina en última sesión");
        }

        // ── Invariantes generales ────────────────────────────────────────────
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

        // ── Cap al número de sesiones disponibles ────────────────────────────
        [Fact]
        public void HierroExactoIgualSesiones_RetornaTodas()
        {
            // ceil(13*200/200) = 13 = m → shortcut, retorna todas las sesiones
            var result = _sut.GenerarFechasAplicacion(13 * 200, LmvMarzo2025);
            result.Should().HaveCount(13);
            result.Should().BeEquivalentTo(LmvMarzo2025);
        }

        [Fact]
        public void HierroMayorQueSesionesDisponibles_CapaAlMax()
        {
            // ceil(3000/200) = 15 > 13 sesiones → cap en 13
            var result = _sut.GenerarFechasAplicacion(3000, LmvMarzo2025);
            result.Should().HaveCount(13);
        }

        [Fact]
        public void Hierro1000_CincoAplicaciones_EnLmv()
        {
            // ceil(1000/200) = 5 < 13 → 5 aplicaciones
            _sut.GenerarFechasAplicacion(1000, LmvMarzo2025).Should().HaveCount(5);
        }
    }
}
