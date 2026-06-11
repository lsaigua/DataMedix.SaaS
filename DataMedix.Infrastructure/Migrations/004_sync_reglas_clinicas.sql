-- =============================================================================
-- Migration 004: Sincronizar reglas clínicas — nuevas reglas + fix MOD-MES-IMPAR
-- Ejecutar una sola vez contra la base de datos de producción.
-- Es idempotente: usa ON CONFLICT DO NOTHING para inserts y verifica antes de update.
-- =============================================================================

-- ── 1. NUEVAS REGLAS (insertar si no existen) ─────────────────────────────────

-- FE-R13: Hb<11.5 + TSAT [20,30) + Ferritina [500,850) → 200 mg/mes
-- Cubría un gap en la tabla de 13 condiciones (condición #4 de la especificación)
INSERT INTO reglas_clinicas (id, codigo, nombre, tipo, prioridad, condiciones_json, accion_json)
SELECT
    uuid_generate_v4(),
    'FE-R13',
    'Hb<11.5 + TSAT 20-30 + Ferritina 500-850 — 200 mg/mes',
    'HIERRO',
    212,
    '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"between","value":[20,30],"inclusive":"left"},{"param":"Ferritina","op":"between","value":[500,850],"inclusive":"left"}]}',
    '{"tipo":"HIERRO","hierro_mg_mes":200,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Ferropenia funcional con reservas intermedias — mantenimiento."}'
WHERE NOT EXISTS (SELECT 1 FROM reglas_clinicas WHERE codigo = 'FE-R13');

-- FE-R14: Hb [10,11.5) + TSAT [20,30) + Ferritina<500 → 400 mg/mes
-- Cubría un gap en la tabla de 13 condiciones (condición #5 de la especificación)
INSERT INTO reglas_clinicas (id, codigo, nombre, tipo, prioridad, condiciones_json, accion_json)
SELECT
    uuid_generate_v4(),
    'FE-R14',
    'Hb 10-11.5 + TSAT 20-30 + Ferritina<500 — 400 mg/mes',
    'HIERRO',
    213,
    '{"operator":"AND","conditions":[{"param":"Hb","op":"between","value":[10,11.5],"inclusive":"left"},{"param":"TSAT","op":"between","value":[20,30],"inclusive":"left"},{"param":"Ferritina","op":"<","value":500}]}',
    '{"tipo":"HIERRO","hierro_mg_mes":400,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Anemia moderada con ferropenia — dosis intermedia."}'
WHERE NOT EXISTS (SELECT 1 FROM reglas_clinicas WHERE codigo = 'FE-R14');

-- MOD-EPO-ESCALAR: Escalamiento EPO +6000 por no-respuesta
-- Condiciones: no primer mes AND HB_previa<10 AND HB_actual<=HB_previa
-- Cantidad (6000) y máximo (18000) son parámetros en el JSON — no hardcodeados
INSERT INTO reglas_clinicas (id, codigo, nombre, tipo, prioridad, condiciones_json, accion_json)
SELECT
    uuid_generate_v4(),
    'MOD-EPO-ESCALAR',
    'Modificador escalamiento EPO — +6000 por no respuesta',
    'MODIFICADOR',
    401,
    '{"operator":"AND","conditions":[{"param":"es_primer_mes","op":"=","value":false},{"param":"hb_previa","op":"<","value":10},{"param":"hb_no_crecio_desde_previa","op":"=","value":true}]}',
    '{"tipo":"MODIFICADOR","aplicar":"escalar_epo","cantidad":6000,"maximo":18000,"mensaje":"Escalamiento EPO +6000 UI/sem por no respuesta (HB previa <10 sin mejora)."}'
WHERE NOT EXISTS (SELECT 1 FROM reglas_clinicas WHERE codigo = 'MOD-EPO-ESCALAR');


-- ── 2. FIX MOD-MES-IMPAR (condición incorrecta) ──────────────────────────────
-- Antes: condición "perfil_hierro_actual=false" → nunca disparaba con carry-forward
-- Ahora:  condición "Ferritina<850"              → dispara correctamente en meses impares
--         (excepto FE-R11 donde ferritina alta, y BAJA=ALTA=400, no necesita reducción)

UPDATE reglas_clinicas
SET
    condiciones_json = '{"operator":"AND","conditions":[{"param":"mes_actual_es_impar","op":"=","value":true},{"param":"Ferritina","op":"<","value":850}]}',
    updated_at       = NOW()
WHERE codigo = 'MOD-MES-IMPAR'
  AND condiciones_json LIKE '%perfil_hierro_actual%';
-- La condición LIKE asegura que solo actualiza si tiene la versión vieja.
-- Si ya fue corregida manualmente, esta sentencia no hace nada.


-- ── 3. VERIFICACIÓN ──────────────────────────────────────────────────────────
-- Ejecuta esto para confirmar que todo quedó bien:

SELECT codigo, nombre, tipo, prioridad, activo
FROM reglas_clinicas
WHERE codigo IN ('FE-R13', 'FE-R14', 'MOD-EPO-ESCALAR', 'MOD-MES-IMPAR')
ORDER BY prioridad;

-- Resultado esperado:
-- MOD-MES-IMPAR   | ... | MODIFICADOR | 400 | true
-- MOD-EPO-ESCALAR | ... | MODIFICADOR | 401 | true
-- FE-R13          | ... | HIERRO      | 212 | true
-- FE-R14          | ... | HIERRO      | 213 | true
