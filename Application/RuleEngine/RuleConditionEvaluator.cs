using System.Text.Json;
using DataMedix.Domain.RuleEngine;

namespace DataMedix.Application.RuleEngine
{
    /// <summary>
    /// Evalúa un árbol de condiciones (RuleConditionNode) contra un EvaluationContext.
    /// Stateless y thread-safe — puede registrarse como Singleton.
    /// </summary>
    public sealed class RuleConditionEvaluator
    {
        // Params numéricos: mapean nombre del JSON → getter en EvaluationContext
        private static readonly Dictionary<string, Func<EvaluationContext, decimal?>> NumericParams =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Hb"]                    = ctx => ctx.Hb,
                ["TSAT"]                  = ctx => ctx.TSAT,
                ["Ferritina"]             = ctx => ctx.Ferritina,
                ["HierroSerico"]          = ctx => ctx.HierroSerico,
                ["Albumina"]              = ctx => ctx.Albumina,
                ["Creatinina"]            = ctx => ctx.Creatinina,
                ["BUN"]                   = ctx => ctx.BUN,
                ["Potasio"]               = ctx => ctx.Potasio,
                ["Sodio"]                 = ctx => ctx.Sodio,
                ["Calcio"]                = ctx => ctx.Calcio,
                ["Fosforo"]               = ctx => ctx.Fosforo,
                ["PTH"]                   = ctx => ctx.PTH,
                ["PesoKg"]                = ctx => ctx.PesoKg,
                ["meses_en_dialisis"]     = ctx => (decimal?)ctx.MesesEnDialisis,
                ["epo_ui_semana_actual"]  = ctx => ctx.EpoUiSemanaActual,
                ["meses_sin_mejora_hb"]   = ctx => (decimal?)ctx.MesesSinMejoraHb,
            };

        // Params booleanos
        private static readonly Dictionary<string, Func<EvaluationContext, bool>> BoolParams =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["primera_vez_hierro"]   = ctx => ctx.PrimeraVezHierro,
                ["mes_actual_es_impar"]  = ctx => ctx.MesActualEsImpar,
                ["perfil_hierro_actual"] = ctx => ctx.PerfilHierroActual,
            };

        /// <summary>Evalúa el nodo raíz contra el contexto. false si nodo es null.</summary>
        public bool Evaluate(RuleConditionNode? node, EvaluationContext ctx)
        {
            if (node is null) return false;
            return node.IsLeaf ? EvaluateLeaf(node, ctx) : EvaluateComposite(node, ctx);
        }

        private bool EvaluateComposite(RuleConditionNode node, EvaluationContext ctx)
        {
            var children = node.Conditions ?? [];
            return (node.Operator ?? "AND").ToUpperInvariant() switch
            {
                "AND" => children.All(c => Evaluate(c, ctx)),
                "OR"  => children.Any(c => Evaluate(c, ctx)),
                "NOT" => children.Count == 1 && !Evaluate(children[0], ctx),
                _     => false
            };
        }

        private static bool EvaluateLeaf(RuleConditionNode node, EvaluationContext ctx)
        {
            var param = node.Param!;
            var op    = node.Op!;
            var value = node.Value;

            if (value is null) return false;

            // ── Boolean params ───────────────────────────────────────────────
            if (BoolParams.TryGetValue(param, out var boolGetter))
            {
                if (value.Value.ValueKind != JsonValueKind.True &&
                    value.Value.ValueKind != JsonValueKind.False)
                    return false;

                var ctxVal    = boolGetter(ctx);
                var targetVal = value.Value.GetBoolean();
                return op switch
                {
                    "="  => ctxVal == targetVal,
                    "!=" => ctxVal != targetVal,
                    _    => false
                };
            }

            // ── Numeric params ───────────────────────────────────────────────
            if (NumericParams.TryGetValue(param, out var numGetter))
            {
                var ctxVal = numGetter(ctx);
                if (!ctxVal.HasValue) return false;  // dato ausente → condición no cumplida

                return op switch
                {
                    "="       => ctxVal.Value == GetDecimal(value.Value),
                    "!="      => ctxVal.Value != GetDecimal(value.Value),
                    "<"       => ctxVal.Value < GetDecimal(value.Value),
                    "<="      => ctxVal.Value <= GetDecimal(value.Value),
                    ">"       => ctxVal.Value > GetDecimal(value.Value),
                    ">="      => ctxVal.Value >= GetDecimal(value.Value),
                    "between" => EvaluateBetween(ctxVal.Value, value.Value, node.Inclusive),
                    _         => false
                };
            }

            return false;
        }

        private static bool EvaluateBetween(decimal val, JsonElement rangeEl, string? inclusive)
        {
            if (rangeEl.ValueKind != JsonValueKind.Array || rangeEl.GetArrayLength() != 2)
                return false;

            var min = rangeEl[0].GetDecimal();
            var max = rangeEl[1].GetDecimal();

            // "left"=[min,max) | "right"=(min,max] | "none"=(min,max) | default=[min,max]
            return (inclusive ?? "both").ToLowerInvariant() switch
            {
                "left"  => val >= min && val < max,
                "right" => val > min && val <= max,
                "none"  => val > min && val < max,
                _       => val >= min && val <= max  // "both"
            };
        }

        private static decimal GetDecimal(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDecimal(),
            _ => throw new InvalidOperationException($"Expected number, got {el.ValueKind}")
        };
    }
}
