using System.Text.Json.Serialization;

namespace DataMedix.Domain.RuleEngine
{
    /// <summary>
    /// Datos de la acción que ejecuta una regla clínica.
    /// </summary>
    public class RuleActionData
    {
        [JsonPropertyName("tipo")]
        public string Tipo { get; set; } = "";

        // ── EPO ──────────────────────────────────────────────────────────────
        [JsonPropertyName("epo_ui_semana")]
        public decimal? EpoUiSemana { get; set; }

        // ── Hierro IV ────────────────────────────────────────────────────────
        [JsonPropertyName("hierro_mg_mes")]
        public decimal? HierroMgMes { get; set; }

        [JsonPropertyName("dosis_individual_mg")]
        public decimal? DosisIndividualMg { get; set; }

        [JsonPropertyName("frecuencia")]
        public string? Frecuencia { get; set; }

        // ── Común ────────────────────────────────────────────────────────────
        [JsonPropertyName("recomendar")]
        public bool Recomendar { get; set; } = true;

        [JsonPropertyName("mensaje")]
        public string Mensaje { get; set; } = "";

        // ── Alerta ───────────────────────────────────────────────────────────
        [JsonPropertyName("flag")]
        public string? Flag { get; set; }

        [JsonPropertyName("requiere_revision_medica")]
        public bool RequiereRevisionMedica { get; set; }

        [JsonPropertyName("sugerencia_dosis_max")]
        public decimal? SugerenciaDosisMax { get; set; }

        // ── Modificador ──────────────────────────────────────────────────────
        [JsonPropertyName("aplicar")]
        public string? Aplicar { get; set; }

        /// <summary>Mapa de dosis original → dosis reducida (mes impar): "1000"→600, "600"→400, etc.</summary>
        [JsonPropertyName("mapeo")]
        public Dictionary<string, decimal>? Mapeo { get; set; }
    }
}
