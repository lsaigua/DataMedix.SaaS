-- =============================================================================
-- 004_sync_reglas_clinicas.sql
-- Sincronización manual de reglas clínicas en ambientes existentes.
--
-- CUÁNDO EJECUTAR:
--   Solo cuando necesites modificar una regla clínica ya existente en la BD.
--   Las reglas NUEVAS se insertan automáticamente en startup vía ReglasSeed.
--   Las reglas existentes NO se sobrescriben en startup para preservar ediciones
--   manuales realizadas por el equipo médico en producción.
--
-- CÓMO EJECUTAR:
--   Copiar el bloque UPDATE/INSERT para la regla específica que deseas cambiar
--   y ejecutarlo directamente en el Supabase SQL Editor del ambiente correspondiente.
--
-- NOTA: Esta migración no modifica tablas — es una plantilla de referencia.
--       El schema de reglas_clinicas fue creado en startup (EnsureStartupAsync).
-- =============================================================================

-- Ejemplo: actualizar la acción de una regla existente
-- UPDATE reglas_clinicas
-- SET accion_json = '{"medicamento":"EPO","dosis":"4000 UI","frecuencia":"semanal"}',
--     version     = version + 1,
--     updated_at  = now()
-- WHERE codigo = 'EPO_HB_CRITICA';

-- Ejemplo: forzar reactivación de una regla desactivada
-- UPDATE reglas_clinicas SET activo = true WHERE codigo = 'HIERRO_GANZONI';

-- Ejemplo: cambiar prioridad de evaluación
-- UPDATE reglas_clinicas SET prioridad = 5 WHERE codigo = 'EPO_ESCALACION';
