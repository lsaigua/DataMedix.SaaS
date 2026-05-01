using DataMedix.Domain.Entities;

namespace DataMedix.Infrastructure.Seed
{
    /// <summary>
    /// Reglas clínicas por defecto: 7 EPO + 12 Hierro EV (R1-R12) + 6 Alertas + 1 Modificador.
    /// Se usan como seed inicial y como fixture en tests.
    /// </summary>
    public static class ReglasSeed
    {
        public static List<ReglaClinica> GetReglas() =>
        [
            // ================================================================
            // BLOQUE 1 — EPO (Eritropoyetina) UI/semana según Hb
            // Prioridad 100-106 — primera regla que aplica gana
            // ================================================================

            // EPO-01: Hb > 13 — No se recomienda EPO
            new() {
                Codigo = "EPO-01", Nombre = "Hb > 13 — EPO no recomendada",
                Tipo = TipoRegla.Epo, Prioridad = 100,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":">","value":13}]}""",
                AccionJson = """{"tipo":"EPO","epo_ui_semana":0,"recomendar":false,"mensaje":"Hb > 13 g/dL — No se recomienda EPO. Riesgo cardiovascular aumentado."}"""
            },

            // EPO-02: Hb 12.5-13 — 2000 UI/sem
            new() {
                Codigo = "EPO-02", Nombre = "Hb 12.5–13 — EPO 2000 UI/sem",
                Tipo = TipoRegla.Epo, Prioridad = 101,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":12.5},{"param":"Hb","op":"<","value":13}]}""",
                AccionJson = """{"tipo":"EPO","epo_ui_semana":2000,"recomendar":true,"mensaje":"Hb en rango alto — dosis de mantenimiento mínima."}"""
            },

            // EPO-03: Hb 11.7-12.5 — 4000 UI/sem
            new() {
                Codigo = "EPO-03", Nombre = "Hb 11.7–12.5 — EPO 4000 UI/sem",
                Tipo = TipoRegla.Epo, Prioridad = 102,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":11.7},{"param":"Hb","op":"<","value":12.5}]}""",
                AccionJson = """{"tipo":"EPO","epo_ui_semana":4000,"recomendar":true,"mensaje":"Hb en zona objetivo alta."}"""
            },

            // EPO-04: Hb 11-11.7 — 6000 UI/sem
            new() {
                Codigo = "EPO-04", Nombre = "Hb 11–11.7 — EPO 6000 UI/sem",
                Tipo = TipoRegla.Epo, Prioridad = 103,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":11},{"param":"Hb","op":"<","value":11.7}]}""",
                AccionJson = """{"tipo":"EPO","epo_ui_semana":6000,"recomendar":true,"mensaje":"Hb en zona objetivo. Mantener."}"""
            },

            // EPO-05: Hb 10-11 — 8000 UI/sem
            new() {
                Codigo = "EPO-05", Nombre = "Hb 10–11 — EPO 8000 UI/sem",
                Tipo = TipoRegla.Epo, Prioridad = 104,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":10},{"param":"Hb","op":"<","value":11}]}""",
                AccionJson = """{"tipo":"EPO","epo_ui_semana":8000,"recomendar":true,"mensaje":"Hb por debajo de objetivo — incrementar dosis."}"""
            },

            // EPO-06: Hb 8-10 — 12000 UI/sem
            new() {
                Codigo = "EPO-06", Nombre = "Hb 8–10 — EPO 12000 UI/sem",
                Tipo = TipoRegla.Epo, Prioridad = 105,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":8},{"param":"Hb","op":"<","value":10}]}""",
                AccionJson = """{"tipo":"EPO","epo_ui_semana":12000,"recomendar":true,"mensaje":"Anemia moderada — dosis terapéutica."}"""
            },

            // EPO-07: Hb < 8 — 18000 UI/sem (anemia severa)
            new() {
                Codigo = "EPO-07", Nombre = "Hb < 8 — EPO 18000 UI/sem (anemia severa)",
                Tipo = TipoRegla.Epo, Prioridad = 106,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":8}]}""",
                AccionJson = """{"tipo":"EPO","epo_ui_semana":18000,"recomendar":true,"mensaje":"Anemia severa — dosis máxima. Evaluar transfusión si sintomática."}"""
            },

            // ================================================================
            // BLOQUE 2 — HIERRO EV (mg/mes) Matriz R1-R12
            // Prioridad 200-211 — del caso más extremo al más general
            // Primera regla que aplica gana
            // ================================================================

            // FE-R12: Hb >= 13 — Hierro no recomendado
            new() {
                Codigo = "FE-R12", Nombre = "Hb >= 13 — Hierro no recomendado",
                Tipo = TipoRegla.Hierro, Prioridad = 200,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":13}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":0,"recomendar":false,"mensaje":"Hb >= 13 — Hierro EV no recomendado de rutina."}"""
            },

            // FE-R01: Ferropenia absoluta severa — 1000 mg/mes
            new() {
                Codigo = "FE-R01", Nombre = "Ferropenia absoluta severa — 1000 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 201,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"<","value":20},{"param":"Ferritina","op":"<","value":200}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":1000,"dosis_individual_mg":200,"frecuencia":"diálisis seguidas hasta completar dosis mensual","recomendar":true,"mensaje":"Ferropenia absoluta severa — reposición máxima."}"""
            },

            // FE-R11: Hb<10 + TSAT<20 + Ferritina alta — 400 mg/mes
            new() {
                Codigo = "FE-R11", Nombre = "Hb<10 + TSAT<20 + Ferritina alta — 400 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 202,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":10},{"param":"TSAT","op":"<","value":20},{"param":"Ferritina","op":"between","value":[850,2001]}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":400,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Anemia con saturación baja pese a ferritina alta — ferropenia funcional."}"""
            },

            // FE-R10: Hb<10 + TSAT 20-30 + Ferritina muy alta — 200 mg/mes
            new() {
                Codigo = "FE-R10", Nombre = "Hb<10 + TSAT 20-30 + Ferritina muy alta — 200 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 203,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":10},{"param":"TSAT","op":"between","value":[20,30],"inclusive":"left"},{"param":"Ferritina","op":"between","value":[850,2000],"inclusive":"left"}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":200,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Anemia con reservas elevadas — dosis conservadora."}"""
            },

            // FE-R07: Hb<10 + TSAT 20-30 + Ferritina<500 — 600 mg/mes
            new() {
                Codigo = "FE-R07", Nombre = "Hb<10 + TSAT 20-30 + Ferritina<500 — 600 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 204,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":10},{"param":"TSAT","op":"between","value":[20,30],"inclusive":"left"},{"param":"Ferritina","op":"<","value":500}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":600,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Anemia con saturación baja — dosis intermedia."}"""
            },

            // FE-R03: Hb<11.5 + TSAT<20 + Ferritina<850 — 400 mg/mes
            new() {
                Codigo = "FE-R03", Nombre = "Hb<11.5 + TSAT<20 + Ferritina<850 — 400 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 205,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"<","value":20},{"param":"Ferritina","op":"<","value":850}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":400,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Ferropenia funcional/absoluta moderada."}"""
            },

            // FE-R02: Hb<11.5 + TSAT<=20 + Ferritina<=500 — 600 mg/mes
            new() {
                Codigo = "FE-R02", Nombre = "Hb<11.5 + TSAT<=20 + Ferritina<=500 — 600 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 206,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"<=","value":20},{"param":"Ferritina","op":"<=","value":500}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":600,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Ferropenia con reservas bajas — dosis intermedia-alta."}"""
            },

            // FE-R05: Hb<11.5 + TSAT 30-40 + Ferritina<200 — 400 mg/mes
            new() {
                Codigo = "FE-R05", Nombre = "Hb<11.5 + TSAT 30-40 + Ferritina<200 — 400 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 207,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"between","value":[30,40],"inclusive":"left"},{"param":"Ferritina","op":"<","value":200}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":400,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Reservas bajas pese a saturación adecuada."}"""
            },

            // FE-R06: Hb<11.5 + TSAT 30-40 + Ferritina 200-500 — 200 mg/mes
            new() {
                Codigo = "FE-R06", Nombre = "Hb<11.5 + TSAT 30-40 + Ferritina 200-500 — 200 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 208,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"between","value":[30,40],"inclusive":"left"},{"param":"Ferritina","op":"between","value":[200,500],"inclusive":"left"}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":200,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Mantenimiento mínimo con reservas adecuadas."}"""
            },

            // FE-R08: Hb 11.5-13 + TSAT<20 + Ferritina<200 — 400 mg/mes
            new() {
                Codigo = "FE-R08", Nombre = "Hb 11.5-13 + TSAT<20 + Ferritina<200 — 400 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 209,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"between","value":[11.5,13],"inclusive":"left"},{"param":"TSAT","op":"<","value":20},{"param":"Ferritina","op":"<","value":200}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":400,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Hb en objetivo pero reservas pobres — reponer."}"""
            },

            // FE-R04: Hb 11.5-13 + TSAT<30 + Ferritina 200-500 — 200 mg/mes
            new() {
                Codigo = "FE-R04", Nombre = "Hb 11.5-13 + TSAT<30 + Ferritina 200-500 — 200 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 210,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"between","value":[11.5,13],"inclusive":"left"},{"param":"TSAT","op":"<","value":30},{"param":"Ferritina","op":"between","value":[200,500],"inclusive":"left"}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":200,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Mantenimiento conservador."}"""
            },

            // FE-R09: Hb 11.5-13 + TSAT 20-30 + Ferritina<200 — 200 mg/mes
            new() {
                Codigo = "FE-R09", Nombre = "Hb 11.5-13 + TSAT 20-30 + Ferritina<200 — 200 mg/mes",
                Tipo = TipoRegla.Hierro, Prioridad = 211,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"between","value":[11.5,13],"inclusive":"left"},{"param":"TSAT","op":"between","value":[20,30],"inclusive":"left"},{"param":"Ferritina","op":"<","value":200}]}""",
                AccionJson = """{"tipo":"HIERRO","hierro_mg_mes":200,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Mantenimiento por reservas bajas."}"""
            },

            // ================================================================
            // BLOQUE 3 — ALERTAS CLÍNICAS
            // Todas las alertas que apliquen se acumulan (no hay "primera gana")
            // ================================================================

            // ALERT-HB-CRIT: Hb < 8 — Anemia severa
            new() {
                Codigo = "ALERT-HB-CRIT", Nombre = "Hb < 8 — Anemia severa",
                Tipo = TipoRegla.Alerta, Prioridad = 300, Severidad = "CRITICA",
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":8}]}""",
                AccionJson = """{"tipo":"ALERTA","flag":"HB_CRITICA","requiere_revision_medica":true,"mensaje":"Hb < 8 g/dL — Anemia severa. Evaluar transfusión urgente y causa secundaria."}"""
            },

            // ALERT-HB-BAJA: Hb 8-9 — Bajo umbral mínimo
            new() {
                Codigo = "ALERT-HB-BAJA", Nombre = "Hb < 9 — Bajo umbral mínimo recomendado",
                Tipo = TipoRegla.Alerta, Prioridad = 301, Severidad = "ALTA",
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":8},{"param":"Hb","op":"<","value":9}]}""",
                AccionJson = """{"tipo":"ALERTA","flag":"HB_BAJO_UMBRAL","mensaje":"Hb < 9 g/dL — KDIGO recomienda evitar este nivel. Iniciar/intensificar AEE."}"""
            },

            // ALERT-HB-ALTA: Hb 11.5-13 — Sobre límite superior
            new() {
                Codigo = "ALERT-HB-ALTA", Nombre = "Hb > 11.5 — Sobre límite superior deseado",
                Tipo = TipoRegla.Alerta, Prioridad = 302, Severidad = "MEDIA",
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":">","value":11.5},{"param":"Hb","op":"<=","value":13}]}""",
                AccionJson = """{"tipo":"ALERTA","flag":"HB_SOBRE_OBJETIVO","mensaje":"Hb > 11.5 g/dL — Sobre límite superior. Justificar si paciente de alto riesgo de sangrado."}"""
            },

            // ALERT-K-CRIT: K+ > 6 — Hiperkalemia crítica
            new() {
                Codigo = "ALERT-K-CRIT", Nombre = "Potasio > 6 mEq/L — Hiperkalemia crítica",
                Tipo = TipoRegla.Alerta, Prioridad = 310, Severidad = "CRITICA",
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Potasio","op":">","value":6}]}""",
                AccionJson = """{"tipo":"ALERTA","flag":"HIPERKALEMIA","requiere_revision_medica":true,"mensaje":"K+ > 6 mEq/L — Riesgo arrítmico. Confirmar con muestra fresca y evaluar diálisis urgente."}"""
            },

            // ALERT-RESIST-EPO: Sospecha resistencia EPO
            new() {
                Codigo = "ALERT-RESIST-EPO", Nombre = "Sospecha de resistencia a EPO",
                Tipo = TipoRegla.Alerta, Prioridad = 320, Severidad = "ALTA",
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":10},{"param":"epo_ui_semana_actual","op":">=","value":12000},{"param":"meses_sin_mejora_hb","op":">=","value":3}]}""",
                AccionJson = """{"tipo":"ALERTA","flag":"RESISTENCIA_EPO","sugerencia_dosis_max":18000,"mensaje":"Sospecha resistencia EPO. Descartar: osteítis fibrosa, toxicidad por aluminio, hemoglobinopatías, mieloma, desnutrición, hemólisis, subdiálisis, hipotiroidismo, IECAs, alcoholismo."}"""
            },

            // ALERT-PRUEBA-SENS: Paciente nuevo — prueba de sensibilidad
            new() {
                Codigo = "ALERT-PRUEBA-SENS", Nombre = "Paciente nuevo — Prueba de sensibilidad a hierro requerida",
                Tipo = TipoRegla.Alerta, Prioridad = 330, Severidad = "MEDIA",
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"meses_en_dialisis","op":"<","value":24},{"param":"primera_vez_hierro","op":"=","value":true}]}""",
                AccionJson = """{"tipo":"ALERTA","flag":"REQUIERE_PRUEBA_SENSIBILIDAD","requiere_revision_medica":true,"mensaje":"Paciente con <24 meses en diálisis y primera prescripción de hierro EV. Realizar prueba de sensibilidad previa."}"""
            },

            // ALERT-FERRITINA-EXTREMA: Ferritina > 2000 — Sobrecarga
            new() {
                Codigo = "ALERT-FERRITINA-EXTREMA", Nombre = "Ferritina > 2000 — Sobrecarga",
                Tipo = TipoRegla.Alerta, Prioridad = 340, Severidad = "ALTA",
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"Ferritina","op":">","value":2000}]}""",
                AccionJson = """{"tipo":"ALERTA","flag":"SOBRECARGA_HIERRO","requiere_revision_medica":true,"mensaje":"Ferritina > 2000 ng/mL — Sospecha sobrecarga férrica. Suspender hierro EV y evaluar."}"""
            },

            // ================================================================
            // BLOQUE 4 — MODIFICADORES
            // Se aplican DESPUÉS de determinar las dosis base (prioridad 400+)
            // ================================================================

            // MOD-MES-IMPAR: Reducir dosis hierro en mes impar sin perfil nuevo
            new() {
                Codigo = "MOD-MES-IMPAR", Nombre = "Modificador mes impar — Reducir dosis hierro",
                Tipo = TipoRegla.Modificador, Prioridad = 400,
                CondicionesJson = """{"operator":"AND","conditions":[{"param":"mes_actual_es_impar","op":"=","value":true},{"param":"perfil_hierro_actual","op":"=","value":false}]}""",
                AccionJson = """{"tipo":"MODIFICADOR","aplicar":"reducir_dosis_hierro","mapeo":{"1000":600,"600":400,"400":200,"200":200},"mensaje":"Mes impar sin nuevo perfil de hierro — mantener esquema reducido si Hb y perfil estables."}"""
            },
        ];
    }
}
