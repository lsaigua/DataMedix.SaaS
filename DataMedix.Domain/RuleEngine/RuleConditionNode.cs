using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataMedix.Domain.RuleEngine
{
    /// <summary>
    /// Nodo del árbol de condiciones. Puede ser compuesto (AND/OR/NOT)
    /// o hoja (param + operador + valor).
    /// </summary>
    public class RuleConditionNode
    {
        // ── Nodo compuesto ───────────────────────────────────────────────────
        [JsonPropertyName("operator")]
        public string? Operator { get; set; }           // "AND" | "OR" | "NOT"

        [JsonPropertyName("conditions")]
        public List<RuleConditionNode>? Conditions { get; set; }

        // ── Nodo hoja ────────────────────────────────────────────────────────
        [JsonPropertyName("param")]
        public string? Param { get; set; }              // "Hb" | "TSAT" | "Ferritina" | ...

        [JsonPropertyName("op")]
        public string? Op { get; set; }                 // "<" | "<=" | ">" | ">=" | "=" | "!=" | "between"

        [JsonPropertyName("value")]
        public JsonElement? Value { get; set; }         // número | bool | [min, max]

        /// <summary>
        /// Para operador "between": "left"=[min,max) | "right"=(min,max] | "both"=[min,max] | "none"=(min,max)
        /// </summary>
        [JsonPropertyName("inclusive")]
        public string? Inclusive { get; set; }

        [JsonIgnore]
        public bool IsLeaf => Param != null;
    }
}
