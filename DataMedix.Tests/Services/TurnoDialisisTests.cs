using DataMedix.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DataMedix.Tests.Services
{
    /// <summary>
    /// Interpretación del turno de diálisis.
    ///
    /// Códigos de día: L, M (martes), X (miércoles), J, V, SA ó S (sábado), D.
    /// Un turno se escribe concatenándolos. LMV y MJS son códigos históricos
    /// que NO siguen esa regla y se resuelven aparte.
    ///
    /// Poner una dosis en el día equivocado es un error clínico, así que cada
    /// caso ambiguo tiene su test.
    /// </summary>
    public class TurnoDialisisTests
    {
        private static DayOfWeek[] Dias(string codigo) =>
            TurnoDialisis.Detectar(codigo)!.ToArray();

        // ── Días sueltos ──────────────────────────────────────────────────────

        [Theory]
        [InlineData("L",  DayOfWeek.Monday)]
        [InlineData("M",  DayOfWeek.Tuesday)]
        [InlineData("X",  DayOfWeek.Wednesday)]
        [InlineData("J",  DayOfWeek.Thursday)]
        [InlineData("V",  DayOfWeek.Friday)]
        [InlineData("SA", DayOfWeek.Saturday)]
        [InlineData("S",  DayOfWeek.Saturday)]
        [InlineData("D",  DayOfWeek.Sunday)]
        public void Detectar_reconoce_cada_dia_suelto(string codigo, DayOfWeek esperado)
        {
            Dias(codigo).Should().Equal(esperado);
        }

        [Fact]
        public void Sabado_se_acepta_como_S_y_como_SA()
        {
            Dias("S").Should().Equal(Dias("SA"));
        }

        // ── Códigos históricos ────────────────────────────────────────────────

        [Fact]
        public void LMV_es_lunes_miercoles_y_viernes_no_martes()
        {
            // La M de LMV significa MIÉRCOLES. Leerla como martes movería las
            // dosis un día para los pacientes que ya usan este turno.
            Dias("LMV").Should().Equal(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);
            Dias("LMV").Should().NotContain(DayOfWeek.Tuesday);
        }

        [Fact]
        public void MJS_es_martes_jueves_y_sabado()
        {
            Dias("MJS").Should().Equal(DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday);
        }

        [Theory]
        [InlineData("1er LMV")]
        [InlineData("2do LMV")]
        [InlineData("3er LMV")]
        public void Prefijos_historicos_siguen_funcionando(string codigo)
        {
            Dias(codigo).Should().Equal(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);
        }

        // ── Turno diario ──────────────────────────────────────────────────────

        [Fact]
        public void LMXJVSAD_son_los_siete_dias()
        {
            Dias("LMXJVSAD").Should().Equal(
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday);
        }

        [Fact]
        public void LMXJVSAD_no_se_confunde_con_LMV()
        {
            // Comparten las dos primeras letras. Si el orden de resolución
            // buscara LMV por subcadena antes de tokenizar, este turno diario
            // se habría reducido a tres días.
            Dias("LMXJVSAD").Should().HaveCount(7);
        }

        [Fact]
        public void Un_codigo_que_contiene_LMV_no_pierde_sus_otros_dias()
        {
            // "LMVSA" tokeniza como lunes, martes, viernes y sábado. Resolver
            // primero el compuesto LMV habría descartado el sábado.
            Dias("LMVSA").Should().Equal(
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Friday, DayOfWeek.Saturday);
        }

        // ── Orden y limpieza ──────────────────────────────────────────────────

        [Fact]
        public void Los_dias_salen_ordenados_de_lunes_a_domingo()
        {
            // El domingo va al final aunque en DayOfWeek valga 0
            Dias("DL").Should().Equal(DayOfWeek.Monday, DayOfWeek.Sunday);
        }

        [Fact]
        public void Un_dia_repetido_no_se_duplica()
        {
            Dias("LL").Should().Equal(DayOfWeek.Monday);
        }

        // ── Rechazos ──────────────────────────────────────────────────────────

        [Theory]
        [InlineData("IESS")]
        [InlineData("privado")]
        [InlineData("#N/A")]
        [InlineData("")]
        [InlineData(null)]
        public void Detectar_rechaza_lo_que_no_es_turno(string? entrada)
        {
            TurnoDialisis.Detectar(entrada).Should().BeNull();
            TurnoDialisis.EsValido(entrada).Should().BeFalse();
        }

        [Fact]
        public void Una_letra_desconocida_invalida_el_turno_completo()
        {
            // Preferible no programar a programar en días inventados
            TurnoDialisis.Detectar("LZV").Should().BeNull();
        }

        // ── Normalización ─────────────────────────────────────────────────────

        [Theory]
        [InlineData("1er LMV",  "LMV")]
        [InlineData("lmxjvsad", "LMXJVSAD")]
        [InlineData(" l ",      "L")]
        [InlineData("IESS",     null)]
        public void Normalizar_devuelve_el_codigo_canonico(string entrada, string? esperado)
        {
            TurnoDialisis.Normalizar(entrada).Should().Be(esperado);
        }

        [Fact]
        public void SesionesPorSemana_cuenta_los_dias()
        {
            TurnoDialisis.SesionesPorSemana("L").Should().Be(1);
            TurnoDialisis.SesionesPorSemana("LMV").Should().Be(3);
            TurnoDialisis.SesionesPorSemana("LMXJVSAD").Should().Be(7);
            TurnoDialisis.SesionesPorSemana("IESS").Should().Be(0);
        }

        [Fact]
        public void EsPatronClasico_solo_para_LMV_y_MJS()
        {
            TurnoDialisis.EsPatronClasico(TurnoDialisis.Detectar("LMV")).Should().BeTrue();
            TurnoDialisis.EsPatronClasico(TurnoDialisis.Detectar("MJS")).Should().BeTrue();
            TurnoDialisis.EsPatronClasico(TurnoDialisis.Detectar("LXV")).Should().BeTrue();  // mismos días
            TurnoDialisis.EsPatronClasico(TurnoDialisis.Detectar("LMJ")).Should().BeFalse();
            TurnoDialisis.EsPatronClasico(TurnoDialisis.Detectar("L")).Should().BeFalse();
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

    /// <summary>
    /// Reglas de selección del turno, compartidas por el cronograma, la
    /// prescripción sugerida y el alta de pacientes. Viven en el dominio para
    /// que un mismo paciente no acabe con turnos distintos según dónde se edite.
    /// </summary>
    public class TurnoSeleccionTests
    {
        [Fact]
        public void Un_combinado_no_convive_con_otro_combinado()
        {
            var sel = new[] { "LMV" };

            TurnoDialisis.PuedeSeleccionar(sel, "MJS").Should().BeFalse();
            TurnoDialisis.PuedeSeleccionar(sel, "LMXJVSAD").Should().BeFalse();
        }

        [Fact]
        public void Un_combinado_bloquea_los_dias_sueltos()
        {
            var sel = new[] { "LMV" };

            TurnoDialisis.PuedeSeleccionar(sel, "M").Should().BeFalse();
            TurnoDialisis.PuedeSeleccionar(sel, "D").Should().BeFalse();
        }

        [Fact]
        public void Los_dias_sueltos_se_combinan_entre_ellos()
        {
            var sel = new[] { "L" };

            TurnoDialisis.PuedeSeleccionar(sel, "J").Should().BeTrue();
        }

        [Fact]
        public void Un_dia_suelto_bloquea_los_combinados()
        {
            var sel = new[] { "L" };

            TurnoDialisis.PuedeSeleccionar(sel, "LMV").Should().BeFalse();
        }

        [Fact]
        public void Lo_ya_elegido_siempre_se_puede_quitar()
        {
            TurnoDialisis.PuedeSeleccionar(new[] { "LMV" }, "LMV").Should().BeTrue();
        }

        [Fact]
        public void Con_dias_sueltos_elegidos_el_combinado_queda_bloqueado()
        {
            // La regla del diagrama es explicita: elegir dias sueltos impide
            // seleccionar LMV o MJS. Para cambiar a un combinado hay que pasar
            // antes por «Sin turno», que es el boton de escape del selector.
            var sel = TurnoDialisis.Alternar(new[] { "L", "J" }, "LMV");

            sel.Should().Equal("L", "J");
        }

        [Fact]
        public void Desde_cero_un_combinado_se_puede_elegir()
        {
            TurnoDialisis.Alternar(Array.Empty<string>(), "LMV").Should().Equal("LMV");
        }

        [Fact]
        public void Alternar_quita_lo_que_ya_estaba()
        {
            var sel = TurnoDialisis.Alternar(new[] { "L", "J" }, "J");

            sel.Should().Equal("L");
        }

        [Fact]
        public void Alternar_ignora_lo_que_la_regla_no_permite()
        {
            var sel = TurnoDialisis.Alternar(new[] { "LMV" }, "M");

            sel.Should().Equal("LMV");
        }

        [Fact]
        public void Componer_ordena_los_dias_por_semana_no_por_orden_de_clic()
        {
            // El médico pulsó J y luego L: debe guardarse "LJ"
            TurnoDialisis.Componer(new[] { "J", "L" }).Should().Be("LJ");
        }

        [Fact]
        public void Componer_de_un_combinado_devuelve_el_combinado()
        {
            TurnoDialisis.Componer(new[] { "LMXJVSAD" }).Should().Be("LMXJVSAD");
        }

        [Fact]
        public void Componer_sin_seleccion_es_vacio()
        {
            TurnoDialisis.Componer(Array.Empty<string>()).Should().BeEmpty();
        }

        [Theory]
        [InlineData("LMV",      new[] { "LMV" })]
        [InlineData("LMXJVSAD", new[] { "LMXJVSAD" })]
        [InlineData("LJ",       new[] { "L", "J" })]
        [InlineData("1er MJS",  new[] { "MJS" })]
        [InlineData("IESS",     new string[0])]
        public void Descomponer_reabre_el_selector_con_lo_guardado(string codigo, string[] esperado)
        {
            TurnoDialisis.Descomponer(codigo).Should().Equal(esperado);
        }

        [Fact]
        public void Componer_y_descomponer_son_inversas()
        {
            foreach (var codigo in new[] { "L", "LJ", "LMXJVSAD", "LMV", "MJS", "MXV" })
                TurnoDialisis.Componer(TurnoDialisis.Descomponer(codigo)).Should().Be(codigo);
        }
    }
}
