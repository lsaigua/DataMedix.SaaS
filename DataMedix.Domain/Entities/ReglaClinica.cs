using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using DataMedix.Domain.RuleEngine;

namespace DataMedix.Domain.Entities
{
    /// <summary>
    /// Regla clínica configurable: condición JSON → acción JSON.
    /// Global (TenantId=null) o tenant-específica.
    /// </summary>
    public class ReglaClinica
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Código único de negocio: EPO-01, FE-R01, ALERT-HB-CRIT</summary>
        public string Codigo { get; set; } = "";

        public string Nombre { get; set; } = "";

        /// <summary>EPO | HIERRO | ALERTA | MODIFICADOR</summary>
        public string Tipo { get; set; } = "";

        /// <summary>Menor número = mayor prioridad. EPO: 100-106, FE: 200-211, ALERTA: 300+, MOD: 400+</summary>
        public int Prioridad { get; set; }

        /// <summary>Solo para ALERTA: CRITICA | ALTA | MEDIA</summary>
        public string? Severidad { get; set; }

        /// <summary>Árbol de condiciones serializado como JSON</summary>
        public string CondicionesJson { get; set; } = "{}";

        /// <summary>Acción serializada como JSON</summary>
        public string AccionJson { get; set; } = "{}";

        public int Version { get; set; } = 1;
        public bool Activo { get; set; } = true;

        /// <summary>Null = regla global; valor = regla del tenant</summary>
        public Guid? TenantId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }

        // ── Parsed in-memory (never persisted) ──────────────────────────────
        [NotMapped] public RuleConditionNode? ParsedCondition { get; private set; }
        [NotMapped] public RuleActionData? ParsedAction { get; private set; }

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Deserializa CondicionesJson y AccionJson.
        /// Llamar después de cargar desde DB (en el repositorio).
        /// </summary>
        public void Parse()
        {
            ParsedCondition = JsonSerializer.Deserialize<RuleConditionNode>(CondicionesJson, _jsonOpts);
            ParsedAction = JsonSerializer.Deserialize<RuleActionData>(AccionJson, _jsonOpts);
        }
    }

    public static class TipoRegla
    {
        public const string Epo = "EPO";
        public const string Hierro = "HIERRO";
        public const string Alerta = "ALERTA";
        public const string Modificador = "MODIFICADOR";
    }
}
