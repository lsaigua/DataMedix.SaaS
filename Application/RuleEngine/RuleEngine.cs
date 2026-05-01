using DataMedix.Domain.Entities;

namespace DataMedix.Application.RuleEngine
{
    /// <summary>
    /// Motor de reglas clínicas.
    /// Evalúa todas las reglas activas en orden de prioridad y aplica las que se cumplen.
    /// Stateless y thread-safe — registrar como Singleton.
    /// </summary>
    public sealed class RuleEngine : IRuleEngine
    {
        private readonly IRuleCache _cache;
        private readonly RuleConditionEvaluator _evaluator;

        public RuleEngine(IRuleCache cache, RuleConditionEvaluator evaluator)
        {
            _cache     = cache;
            _evaluator = evaluator;
        }

        public async Task<EvaluationResult> EvaluateAsync(EvaluationContext ctx)
        {
            var rules  = await _cache.GetActiveRulesAsync();
            var result = new EvaluationResult();

            bool epoApplied    = false;
            bool hierroApplied = false;

            // Reglas ya vienen ordenadas por prioridad (ASC) desde el cache
            foreach (var rule in rules)
            {
                if (!_evaluator.Evaluate(rule.ParsedCondition, ctx))
                    continue;

                switch (rule.Tipo)
                {
                    case TipoRegla.Epo when !epoApplied:
                        ApplyEpo(rule, result);
                        epoApplied = true;
                        break;

                    case TipoRegla.Hierro when !hierroApplied:
                        ApplyHierro(rule, result);
                        hierroApplied = true;
                        break;

                    case TipoRegla.Alerta:
                        ApplyAlerta(rule, result);
                        break;

                    case TipoRegla.Modificador:
                        // Los modificadores se evalúan DESPUÉS de EPO/FE (prioridad >= 400)
                        ApplyModificador(rule, result);
                        break;
                }
            }

            // Cálculo alternativo de Ganzoni (referencia, no reemplaza regla principal)
            if (ctx.PesoKg.HasValue && ctx.Hb.HasValue)
                result.HierroGanzoniMg = CalcularGanzoni(ctx.PesoKg.Value, ctx.Hb.Value);

            return result;
        }

        // ── Aplicadores ──────────────────────────────────────────────────────

        private static void ApplyEpo(ReglaClinica rule, EvaluationResult result)
        {
            var action = rule.ParsedAction!;
            result.ReglaEpoCodigo  = rule.Codigo;
            result.ReglaEpoNombre  = rule.Nombre;
            result.EpoUiSemana     = action.EpoUiSemana ?? 0;
            result.EpoRecomendada  = action.Recomendar;
            result.EpoMensaje      = action.Mensaje;
        }

        private static void ApplyHierro(ReglaClinica rule, EvaluationResult result)
        {
            var action = rule.ParsedAction!;
            result.ReglaHierroCodigo    = rule.Codigo;
            result.ReglaHierroNombre    = rule.Nombre;
            result.HierroMgMes          = action.HierroMgMes ?? 0;
            result.HierroDosisIndividualMg = action.DosisIndividualMg;
            result.HierroRecomendado    = action.Recomendar;
            result.HierroMensaje        = action.Mensaje;
        }

        private static void ApplyAlerta(ReglaClinica rule, EvaluationResult result)
        {
            var action = rule.ParsedAction!;
            result.Alertas.Add(new AlertaClinica
            {
                Codigo                = rule.Codigo,
                Flag                  = action.Flag ?? rule.Codigo,
                Severidad             = rule.Severidad ?? "MEDIA",
                Mensaje               = action.Mensaje,
                RequiereRevisionMedica = action.RequiereRevisionMedica,
                SugerenciaDosisMax    = action.SugerenciaDosisMax,
            });
        }

        private static void ApplyModificador(ReglaClinica rule, EvaluationResult result)
        {
            var action = rule.ParsedAction!;

            // R: Reducir dosis de hierro en mes impar
            if (action.Aplicar == "reducir_dosis_hierro" &&
                result.HierroMgMes.HasValue &&
                action.Mapeo is not null)
            {
                var key = ((int)result.HierroMgMes.Value).ToString();
                if (action.Mapeo.TryGetValue(key, out var nuevaDosis))
                    result.HierroMgMes = nuevaDosis;

                result.ModificadoresAplicados.Add(rule.Codigo);
            }
        }

        // ── Fórmula de Ganzoni ────────────────────────────────────────────────
        // Déficit de hierro (mg) = Peso(kg) × (15 − Hb actual g/dL) × 2.4 + 500
        private static decimal CalcularGanzoni(decimal pesoKg, decimal hbActual)
            => pesoKg * (15m - hbActual) * 2.4m + 500m;
    }
}
