using DataMedix.Application.RuleEngine;
using DataMedix.Infrastructure.Seed;
using FluentAssertions;
using Xunit;

namespace DataMedix.Tests.RuleEngine
{
    /// <summary>
    /// Tests del motor de reglas clínicas con las 26 reglas del seed.
    /// Condiciones verificadas contra ReglasSeed.cs (fuente de verdad).
    /// </summary>
    public class RuleEngineTests
    {
        private readonly Application.RuleEngine.RuleEngine _engine;

        public RuleEngineTests()
        {
            var reglas = ReglasSeed.GetReglas();
            foreach (var r in reglas) r.Parse();
            _engine = new Application.RuleEngine.RuleEngine(new StubRuleCache(reglas), new RuleConditionEvaluator());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BLOQUE EPO — 7 reglas
        // EPO-01: Hb>13 → 0, EPO-02: [12.5,13)→2000, EPO-03: [11.7,12.5)→4000,
        // EPO-04: [11,11.7)→6000, EPO-05: [10,11)→8000, EPO-06: [8,10)→12000,
        // EPO-07: <8→18000
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task EPO01_HbMayor13_NoRecomiendaEpo()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(13.5m).Build());
            r.ReglaEpoCodigo.Should().Be("EPO-01");
            r.EpoRecomendada.Should().BeFalse();
            r.EpoUiSemana.Should().Be(0);
        }

        [Fact]
        public async Task EPO01_HbExacto13_NoDispara()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(13.0m).Build());
            r.ReglaEpoCodigo.Should().NotBe("EPO-01");
        }

        [Fact]
        public async Task EPO02_Hb12_5a13_Dosis2000()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(12.7m).Build());
            r.ReglaEpoCodigo.Should().Be("EPO-02");
            r.EpoUiSemana.Should().Be(2000);
        }

        [Fact]
        public async Task EPO03_Hb11_7a12_5_Dosis4000()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(12.0m).Build());
            r.ReglaEpoCodigo.Should().Be("EPO-03");
            r.EpoUiSemana.Should().Be(4000);
        }

        [Fact]
        public async Task EPO04_Hb11a11_7_Dosis6000()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(11.5m).Build());
            r.ReglaEpoCodigo.Should().Be("EPO-04");
            r.EpoUiSemana.Should().Be(6000);
        }

        [Fact]
        public async Task EPO05_Hb10a11_Dosis8000()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(10.5m).Build());
            r.ReglaEpoCodigo.Should().Be("EPO-05");
            r.EpoUiSemana.Should().Be(8000);
        }

        [Fact]
        public async Task EPO06_Hb8a10_Dosis12000()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(9.5m).Build());
            r.ReglaEpoCodigo.Should().Be("EPO-06");
            r.EpoUiSemana.Should().Be(12000);
        }

        [Fact]
        public async Task EPO07_HbMenor8_Dosis18000()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(7.5m).Build());
            r.ReglaEpoCodigo.Should().Be("EPO-07");
            r.EpoUiSemana.Should().Be(18000);
        }

        [Fact]
        public async Task EPO_SinHb_NingunaCargaEpo()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().SinHb().Build());
            r.ReglaEpoCodigo.Should().BeNull();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BLOQUE HIERRO EV — 12 reglas (FE-R01..R12)
        // Condiciones basadas en Hb, TSAT y Ferritina; primera regla gana.
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task FE_R12_HbMayorA13_NoHierro()
        {
            // FE-R12 (p=200): Hb >= 13 → no hierro
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(13.5m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R12");
            r.HierroRecomendado.Should().BeFalse();
        }

        [Fact]
        public async Task FE_R01_FerritinaAbsolutaSevera_1000mg()
        {
            // FE-R01 (p=201): Hb<11.5 AND TSAT<20 AND Ferritina<200 → 1000 mg
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(10.0m).ConTSAT(15m).ConFerritina(150m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R01");
            r.HierroMgMes.Should().Be(1000);
        }

        [Fact]
        public async Task FE_R01_BordeTSAT20_NoDispara()
        {
            // TSAT=20 → no cumple TSAT<20 → FE-R01 no aplica
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(10.0m).ConTSAT(20m).ConFerritina(150m).Build());
            r.ReglaHierroCodigo.Should().NotBe("FE-R01");
        }

        [Fact]
        public async Task FE_R11_HbBaja_TSATBaja_FerrAlta_400mg()
        {
            // FE-R11 (p=202): Hb<10 AND TSAT<20 AND Ferritina [850,2001] → 400 mg
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(9.0m).ConTSAT(15m).ConFerritina(900m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R11");
            r.HierroMgMes.Should().Be(400);
        }

        [Fact]
        public async Task FE_R10_HbBaja_TSATMedia_FerrAlta_200mg()
        {
            // FE-R10 (p=203): Hb<10 AND TSAT [20,30) AND Ferritina [850,2000) → 200 mg
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(9.0m).ConTSAT(25m).ConFerritina(900m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R10");
            r.HierroMgMes.Should().Be(200);
        }

        [Fact]
        public async Task FE_R07_HbBaja_TSATMedia_FerrBaja_600mg()
        {
            // FE-R07 (p=204): Hb<10 AND TSAT [20,30) AND Ferritina<500 → 600 mg
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(9.0m).ConTSAT(25m).ConFerritina(350m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R07");
            r.HierroMgMes.Should().Be(600);
        }

        [Fact]
        public async Task FE_R03_HbBajaMedia_TSATBaja_FerrMedia_400mg()
        {
            // FE-R03 (p=205): Hb<11.5 AND TSAT<20 AND Ferritina<850 → 400 mg
            // Ferritina=200 (>= 200 → FE-R01 no aplica porque requiere Ferritina<200)
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(11.0m).ConTSAT(15m).ConFerritina(200m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R03");
            r.HierroMgMes.Should().Be(400);
        }

        [Fact]
        public async Task FE_R02_HbBajaMedia_TSAT20_FerrMedia_600mg()
        {
            // FE-R02 (p=206): Hb<11.5 AND TSAT<=20 AND Ferritina<=500 → 600 mg
            // TSAT=20 exacto: FE-R03 falla (TSAT<20 = false), FE-R02 aplica (TSAT<=20 = true)
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(11.0m).ConTSAT(20m).ConFerritina(300m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R02");
            r.HierroMgMes.Should().Be(600);
        }

        [Fact]
        public async Task FE_R05_HbBajaMedia_TSATAlta_FerrBaja_400mg()
        {
            // FE-R05 (p=207): Hb<11.5 AND TSAT [30,40) AND Ferritina<200 → 400 mg
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(11.0m).ConTSAT(35m).ConFerritina(150m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R05");
            r.HierroMgMes.Should().Be(400);
        }

        [Fact]
        public async Task FE_R06_HbBajaMedia_TSATAlta_FerrMedia_200mg()
        {
            // FE-R06 (p=208): Hb<11.5 AND TSAT [30,40) AND Ferritina [200,500) → 200 mg
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(11.0m).ConTSAT(35m).ConFerritina(350m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R06");
            r.HierroMgMes.Should().Be(200);
        }

        [Fact]
        public async Task FE_R08_HbObjetivo_TSATBaja_FerrBaja_400mg()
        {
            // FE-R08 (p=209): Hb [11.5,13) AND TSAT<20 AND Ferritina<200 → 400 mg
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(12.0m).ConTSAT(15m).ConFerritina(150m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R08");
            r.HierroMgMes.Should().Be(400);
        }

        [Fact]
        public async Task FE_R04_HbObjetivo_TSATMedia_FerrMedia_200mg()
        {
            // FE-R04 (p=210): Hb [11.5,13) AND TSAT<30 AND Ferritina [200,500) → 200 mg
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(12.0m).ConTSAT(25m).ConFerritina(350m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R04");
            r.HierroMgMes.Should().Be(200);
        }

        [Fact]
        public async Task FE_R09_HbObjetivo_TSATMedia_FerrBaja_200mg()
        {
            // FE-R09 (p=211): Hb [11.5,13) AND TSAT [20,30) AND Ferritina<200 → 200 mg
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(12.0m).ConTSAT(25m).ConFerritina(150m).Build());
            r.ReglaHierroCodigo.Should().Be("FE-R09");
            r.HierroMgMes.Should().Be(200);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ALERTAS — 7 reglas (todas acumulan)
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Alerta_HbCritica_MenorA8()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(7.5m).Build());
            r.Alertas.Should().Contain(a => a.Codigo == "ALERT-HB-CRIT");
            r.TieneAlertaCritica.Should().BeTrue();
        }

        [Fact]
        public async Task Alerta_HbCritica_ExactoA8_NoDispara()
        {
            // Hb = 8.0 está en EPO-06 [8,10), no en Hb<8
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(8.0m).Build());
            r.Alertas.Should().NotContain(a => a.Codigo == "ALERT-HB-CRIT");
        }

        [Fact]
        public async Task Alerta_HbBaja_8_a_9()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(8.5m).Build());
            r.Alertas.Should().Contain(a => a.Codigo == "ALERT-HB-BAJA");
        }

        [Fact]
        public async Task Alerta_HbAlta_11_5_a_13()
        {
            // ALERT-HB-ALTA: Hb > 11.5 AND Hb <= 13
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(12.0m).Build());
            r.Alertas.Should().Contain(a => a.Codigo == "ALERT-HB-ALTA");
        }

        [Fact]
        public async Task Alerta_HbAlta_MayorA13_NoDispara()
        {
            // Hb > 13 → condición Hb <= 13 falla → ALERT-HB-ALTA no aplica
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(13.5m).Build());
            r.Alertas.Should().NotContain(a => a.Codigo == "ALERT-HB-ALTA");
        }

        [Fact]
        public async Task Alerta_PotasioCritico_MayorA6()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConPotasio(6.5m).Build());
            r.Alertas.Should().Contain(a => a.Codigo == "ALERT-K-CRIT");
        }

        [Fact]
        public async Task Alerta_PotasioCritico_Exacto6_NoDispara()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConPotasio(6.0m).Build());
            r.Alertas.Should().NotContain(a => a.Codigo == "ALERT-K-CRIT");
        }

        [Fact]
        public async Task Alerta_ResistenciaEpo_Epo12000_3MesesSinMejora_HbMenor10()
        {
            // Condition: Hb<10 AND epo_ui_semana_actual>=12000 AND meses_sin_mejora_hb>=3
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create()
                    .ConHb(9.5m).ConEpoActual(12000m).ConMesesSinMejoraHb(3)
                    .Build());
            r.TieneAlertaResistenciaEpo.Should().BeTrue();
        }

        [Fact]
        public async Task Alerta_ResistenciaEpo_EpoBaja_NoDispara()
        {
            // epo_ui_semana_actual = 8000 < 12000 → no alerta
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create()
                    .ConHb(9.5m).ConEpoActual(8000m).ConMesesSinMejoraHb(3)
                    .Build());
            r.TieneAlertaResistenciaEpo.Should().BeFalse();
        }

        [Fact]
        public async Task Alerta_PruebaSensibilidad_PrimeraVezHierroMenos24Meses()
        {
            // Condition: meses_en_dialisis < 24 AND primera_vez_hierro = true
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create()
                    .ConMesesEnDialisis(6).PrimeraVezHierro(true)
                    .ConFerritina(150m).ConTSAT(15m)
                    .Build());
            r.RequierePruebaSensibilidad.Should().BeTrue();
        }

        [Fact]
        public async Task Alerta_PruebaSensibilidad_Mas24Meses_NoDispara()
        {
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create()
                    .ConMesesEnDialisis(30).PrimeraVezHierro(true)
                    .Build());
            r.RequierePruebaSensibilidad.Should().BeFalse();
        }

        [Fact]
        public async Task Alerta_FerritinaExtrema_Mayor2000()
        {
            // ALERT-FERRITINA-EXTREMA: Ferritina > 2000
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConFerritina(2100m).Build());
            r.TieneSobrecargaHierro.Should().BeTrue();
        }

        [Fact]
        public async Task Alerta_FerritinaExtrema_Exacto2000_NoDispara()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConFerritina(2000m).Build());
            r.TieneSobrecargaHierro.Should().BeFalse();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // MODIFICADOR MES IMPAR
        // Condition: mes_actual_es_impar=true AND perfil_hierro_actual=false
        // Mapeo: 1000→600, 600→400, 400→200, 200→200
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Modificador_MesImpar_Reduce1000a600()
        {
            // FE-R01 da 1000 mg; modifier (mes impar + sin perfil) → 600 mg
            // PerfilHierroActual se fuerza a false en el contexto aunque TSAT/Ferritina están presentes
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create()
                    .ConHb(10.0m).ConTSAT(15m).ConFerritina(150m)
                    .MesImpar(true).SinPerfilHierroActual()
                    .Build());
            r.ModificadoresAplicados.Should().Contain("MOD-MES-IMPAR");
            r.HierroMgMes.Should().Be(600);
        }

        [Fact]
        public async Task Modificador_MesPar_NoReduce()
        {
            // Mes par: modificador no aplica — dosis original de FE-R01 = 1000 mg
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create()
                    .ConHb(10.0m).ConTSAT(15m).ConFerritina(150m)
                    .MesImpar(false).SinPerfilHierroActual()
                    .Build());
            r.ModificadoresAplicados.Should().NotContain("MOD-MES-IMPAR");
            r.HierroMgMes.Should().Be(1000);
        }

        [Fact]
        public async Task Modificador_MesImpar_ConPerfilHierro_NoReduce()
        {
            // Aunque es mes impar, perfil_hierro_actual=true → modificador no aplica
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create()
                    .ConHb(10.0m).ConTSAT(15m).ConFerritina(150m)
                    .MesImpar(true) // PerfilHierroActual sigue siendo true (default)
                    .Build());
            r.ModificadoresAplicados.Should().NotContain("MOD-MES-IMPAR");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GANZONI — fórmula: peso × (15 − Hb) × 2.4 + 500
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Ganzoni_CalculaCuandoHayPesoYHb()
        {
            // Ganzoni = 70 × (15 - 10) × 2.4 + 500 = 840 + 500 = 1340
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(10.0m).ConPesoKg(70.0m).Build());
            r.HierroGanzoniMg.Should().Be(1340m);
        }

        [Fact]
        public async Task Ganzoni_NullSinPeso()
        {
            var r = await _engine.EvaluateAsync(TestContextBuilder.Create().ConHb(10.0m).Build());
            r.HierroGanzoniMg.Should().BeNull();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // MÚLTIPLES ALERTAS — acumulación (no hay primera-gana para alertas)
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task MultipleAlertas_HbCriticaYPotasio()
        {
            var r = await _engine.EvaluateAsync(
                TestContextBuilder.Create().ConHb(7.0m).ConPotasio(6.5m).Build());
            r.Alertas.Should().Contain(a => a.Codigo == "ALERT-HB-CRIT");
            r.Alertas.Should().Contain(a => a.Codigo == "ALERT-K-CRIT");
            r.Alertas.Count.Should().BeGreaterThanOrEqualTo(2);
        }
    }

    // ── Stub para IRuleCache — carga las reglas del seed directamente ──────────
    file sealed class StubRuleCache : IRuleCache
    {
        private readonly IReadOnlyList<DataMedix.Domain.Entities.ReglaClinica> _rules;
        public StubRuleCache(IEnumerable<DataMedix.Domain.Entities.ReglaClinica> rules)
            => _rules = rules.ToList().AsReadOnly();
        public Task<IReadOnlyList<DataMedix.Domain.Entities.ReglaClinica>> GetActiveRulesAsync()
            => Task.FromResult(_rules);
        public void Invalidate() { }
    }
}
