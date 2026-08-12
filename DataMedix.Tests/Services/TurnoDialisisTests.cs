using DataMedix.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DataMedix.Tests.Services
{
    /// <summary>
    /// El cronograma solo sabe repartir LMV y MJS. Cualquier otro texto deja al
    /// paciente en la grilla sin días de sesión ni totales, así que la detección
    /// se comparte entre alta de pacientes, ingreso manual e importación.
    /// </summary>
    public class TurnoDialisisTests
    {
        [Theory]
        [InlineData("LMV", "LMV")]
        [InlineData("MJS", "MJS")]
        [InlineData("lmv", "LMV")]
        [InlineData("Turno MJS", "MJS")]
        [InlineData("LMV MAÑANA", "LMV")]
        public void Detectar_reconoce_los_dos_turnos_validos(string entrada, string esperado)
        {
            TurnoDialisis.Detectar(entrada).Should().Be(esperado);
        }

        [Theory]
        [InlineData("LMXJVSAD")]   // el valor que traían los archivos importados
        [InlineData("IESS")]
        [InlineData("privado")]
        [InlineData("")]
        [InlineData(null)]
        public void Detectar_descarta_lo_que_no_es_un_turno(string? entrada)
        {
            TurnoDialisis.Detectar(entrada).Should().BeNull();
            TurnoDialisis.EsValido(entrada).Should().BeFalse();
        }

        [Fact]
        public void LMXJVSAD_no_se_confunde_con_LMV()
        {
            // Comparten las dos primeras letras; si la detección fuera por
            // prefijo, este valor pasaría como turno LMV y generaría sesiones
            // en días equivocados.
            TurnoDialisis.Detectar("LMXJVSAD").Should().NotBe(TurnoDialisis.Lmv);
        }
    }

    public class TipoAtencionPacienteTests
    {
        [Theory]
        [InlineData("Hemodiálisis")]
        [InlineData("hemodialisis")]
        [InlineData("HEMODIALISIS CRONICA")]
        [InlineData("HD")]
        public void Detectar_normaliza_a_hemodialisis(string entrada)
        {
            TipoAtencionPaciente.Detectar(entrada).Should().Be(TipoAtencionPaciente.Hemodialisis);
        }

        [Theory]
        [InlineData("Peritoneal")]
        [InlineData("DIALISIS PERITONEAL")]
        [InlineData("DP")]
        public void Detectar_normaliza_a_peritoneal(string entrada)
        {
            TipoAtencionPaciente.Detectar(entrada).Should().Be(TipoAtencionPaciente.Peritoneal);
        }

        [Theory]
        [InlineData("Ambulatorio")]
        [InlineData("")]
        [InlineData(null)]
        public void Detectar_devuelve_null_si_no_es_ninguna_modalidad(string? entrada)
        {
            TipoAtencionPaciente.Detectar(entrada).Should().BeNull();
        }
    }
}
