using DataMedix.Application.RuleEngine;

namespace DataMedix.Tests.RuleEngine
{
    /// <summary>
    /// Builder fluido para construir EvaluationContext en tests.
    /// Los valores por defecto corresponden a un paciente estable en hemodiálisis:
    /// Hb=11.5, TSAT=25%, Ferritina=350, 6 meses en diálisis, mes par, sin primera vez hierro.
    /// </summary>
    public class TestContextBuilder
    {
        private readonly EvaluationContext _ctx = new()
        {
            TenantId   = Guid.NewGuid(),
            PacienteId = Guid.NewGuid(),
            PeriodDate = new DateTime(2025, 4, 1),
            Hb         = 11.5m,
            TSAT       = 25m,
            Ferritina  = 350m,
            MesesEnDialisis   = 6,
            MesActualEsImpar  = false, // mes 4 = par
            PrimeraVezHierro  = false,
            PerfilHierroActual = true,
            MesesSinMejoraHb  = 0,
        };

        public static TestContextBuilder Create() => new();

        public TestContextBuilder ConHb(decimal hb)                   { _ctx.Hb = hb; return this; }
        public TestContextBuilder ConTSAT(decimal tsat)               { _ctx.TSAT = tsat; return this; }
        public TestContextBuilder ConFerritina(decimal ferritina)     { _ctx.Ferritina = ferritina; return this; }
        public TestContextBuilder ConHierroSerico(decimal fe)         { _ctx.HierroSerico = fe; return this; }
        public TestContextBuilder ConPotasio(decimal k)               { _ctx.Potasio = k; return this; }
        public TestContextBuilder ConPTH(decimal pth)                 { _ctx.PTH = pth; return this; }
        public TestContextBuilder ConPesoKg(decimal peso)             { _ctx.PesoKg = peso; return this; }
        public TestContextBuilder ConMesesEnDialisis(int meses)       { _ctx.MesesEnDialisis = meses; return this; }
        public TestContextBuilder ConEpoActual(decimal uiSemana)      { _ctx.EpoUiSemanaActual = uiSemana; return this; }
        public TestContextBuilder ConMesesSinMejoraHb(int meses)      { _ctx.MesesSinMejoraHb = meses; return this; }
        public TestContextBuilder PrimeraVezHierro(bool val = true)   { _ctx.PrimeraVezHierro = val; return this; }
        public TestContextBuilder MesImpar(bool val = true)           { _ctx.MesActualEsImpar = val; return this; }
        public TestContextBuilder SinPerfilHierro()                   { _ctx.PerfilHierroActual = false; _ctx.TSAT = null; _ctx.Ferritina = null; return this; }
        public TestContextBuilder SinHb()                             { _ctx.Hb = null; return this; }
        public TestContextBuilder SinTSAT()                           { _ctx.TSAT = null; return this; }
        public TestContextBuilder SinFerritina()                      { _ctx.Ferritina = null; return this; }
        /// <summary>Fuerza perfil_hierro_actual=false sin anular TSAT/Ferritina (para testear el modificador).</summary>
        public TestContextBuilder SinPerfilHierroActual()             { _ctx.PerfilHierroActual = false; return this; }

        public EvaluationContext Build() => _ctx;
    }
}
