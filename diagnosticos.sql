-- ============================================================
-- SCRIPTS DE DIAGNÓSTICO DATAMEDIX
-- Conectar a la base PostgreSQL y ejecutar por secciones
-- ============================================================


-- ── 1. ALIASES CONFIGURADOS ──────────────────────────────────────────────────
-- Muestra todos los parámetros clínicos y los alias que los mapean.
-- Si ISAT/Saturación no tiene alias que coincida con la columna del Excel,
-- los registros aparecerán como PARAMETRO_DESCONOCIDO.

SELECT
    p.nombre          AS parametro,
    p.codigo          AS codigo,
    a.alias           AS alias_excel,
    a.activo          AS alias_activo,
    CASE WHEN a.tenant_id IS NULL THEN 'GLOBAL' ELSE 'TENANT' END AS ambito
FROM parametros_clinicos p
LEFT JOIN alias_parametros a ON a.parametro_clinico_id = p.id AND a.activo = true
ORDER BY p.codigo, a.alias;


-- ── 2. PARÁMETROS NO RECONOCIDOS (PARAMETRO_DESCONOCIDO) ─────────────────────
-- Muestra los nombres de columnas del Excel que NO tienen alias configurado.
-- Estos son los candidatos a agregar en alias_parametros.

SELECT
    r.parametro_raw                       AS nombre_columna_excel,
    COUNT(*)                              AS veces_importado,
    MAX(r.created_at)::date               AS ultima_carga,
    COUNT(DISTINCT r.paciente_id)         AS pacientes_afectados
FROM resultados_laboratorio r
WHERE r.parametro_clinico_id IS NULL
  AND r.activo = true
GROUP BY r.parametro_raw
ORDER BY veces_importado DESC;


-- ── 3. RESUMEN DE SNAPSHOTS POR PERÍODO ──────────────────────────────────────
-- Muestra cuántos snapshots existen por mes y cuántos tienen cada parámetro.
-- Si 'con_isat' es 0, el alias de ISAT/Saturación no está configurado.

SELECT
    s.periodo_anio                        AS año,
    s.periodo_mes                         AS mes,
    COUNT(*)                              AS total_snapshots,
    COUNT(s.hb_valor)                     AS con_hb,
    COUNT(s.hierro_valor)                 AS con_hierro,
    COUNT(s.ferritina_valor)              AS con_ferritina,
    COUNT(s.saturacion_valor)             AS con_isat,
    COUNT(*) FILTER (WHERE s.tiene_datos_completos) AS datos_completos
FROM snapshots_mensuales s
WHERE s.activo = true
GROUP BY s.periodo_anio, s.periodo_mes
ORDER BY s.periodo_anio, s.periodo_mes;


-- ── 4. DETALLE DE PACIENTES SIN ISAT EN UN PERÍODO ESPECÍFICO ────────────────
-- Cambia los valores de año y mes según el período que quieras revisar.

SELECT
    p.primer_apellido || ' ' || p.segundo_apellido || ', ' ||
    p.primer_nombre  || ' ' || COALESCE(p.segundo_nombre, '') AS paciente,
    p.identificacion,
    s.periodo_anio   AS año,
    s.periodo_mes    AS mes,
    s.hb_valor,
    s.hierro_valor,
    s.ferritina_valor,
    s.saturacion_valor   AS isat,
    s.tiene_datos_completos
FROM snapshots_mensuales s
JOIN pacientes p ON p.id = s.paciente_id
WHERE s.activo = true
  AND s.periodo_anio = 2025      -- <-- CAMBIAR
  AND s.periodo_mes  = 1         -- <-- CAMBIAR (1=enero, 2=febrero, ...)
ORDER BY p.primer_apellido, p.primer_nombre;


-- ── 5. COMPARAR RESULTADOS CRUDOS VS SNAPSHOT PARA UN PACIENTE ───────────────
-- Reemplaza <CEDULA> con la cédula del paciente (10 dígitos con ceros a la izq.)

SELECT
    r.periodo_anio    AS año,
    r.periodo_mes     AS mes,
    pc.codigo         AS codigo_param,
    pc.nombre         AS nombre_param,
    r.parametro_raw   AS columna_excel,
    r.valor_numerico,
    r.unidad_medida,
    r.activo
FROM resultados_laboratorio r
JOIN pacientes p ON p.id = r.paciente_id
LEFT JOIN parametros_clinicos pc ON pc.id = r.parametro_clinico_id
WHERE p.identificacion = '0000000000'   -- <-- CAMBIAR a la cédula
  AND r.activo = true
ORDER BY r.periodo_anio, r.periodo_mes, pc.codigo;


-- ── 6. LOTES DE IMPORTACIÓN Y SUS ESTADÍSTICAS ───────────────────────────────

SELECT
    l.id::text        AS lote_id,
    l.nombre_archivo_original,
    l.periodo_anio    AS año,
    l.periodo_mes     AS mes,
    l.estado,
    l.total_filas,
    l.filas_validas,
    l.filas_error,
    l.created_at::date AS fecha_carga,
    COUNT(DISTINCT d.id) AS filas_staging
FROM lotes_importacion l
LEFT JOIN importacion_detalles d ON d.lote_id = l.id
WHERE l.activo = true
GROUP BY l.id, l.nombre_archivo_original, l.periodo_anio, l.periodo_mes,
         l.estado, l.total_filas, l.filas_validas, l.filas_error, l.created_at
ORDER BY l.created_at DESC;


-- ── 7. AGREGAR ALIAS PARA ISAT (EJECUTAR DESPUÉS DE CONOCER EL NOMBRE EXACTO) ─
-- Primero ejecuta la consulta 2 para ver el nombre exacto de la columna ISAT.
-- Luego reemplaza 'NOMBRE_COLUMNA_EXCEL' con ese valor exacto.

/*
INSERT INTO alias_parametros (id, alias, parametro_clinico_id, activo, created_at)
SELECT
    gen_random_uuid(),
    'NOMBRE_COLUMNA_EXCEL',      -- <-- poner el valor de parametro_raw de la consulta 2
    p.id,
    true,
    NOW()
FROM parametros_clinicos p
WHERE p.codigo = 'ISAT'
ON CONFLICT DO NOTHING;

-- Si también necesitas agregar alias para Ferritina o Hierro:
-- WHERE p.codigo = 'FERR'  (ferritina)
-- WHERE p.codigo = 'FE'    (hierro sérico)
-- WHERE p.codigo = 'HB'    (hemoglobina)
*/


-- ── 8. VERIFICAR PRESCRIPCIONES GENERADAS POR PERÍODO ────────────────────────

SELECT
    ps.periodo_anio   AS año,
    ps.periodo_mes    AS mes,
    COUNT(*)          AS total_prescripciones,
    COUNT(ps.epo_ui_semana)  AS con_epo,
    COUNT(ps.hierro_mg_mes)  AS con_hierro,
    COUNT(*) FILTER (WHERE ps.estado = 'PENDIENTE')  AS pendientes,
    COUNT(*) FILTER (WHERE ps.estado = 'APROBADO')   AS aprobadas
FROM prescripciones_sugeridas ps
WHERE ps.activo = true
GROUP BY ps.periodo_anio, ps.periodo_mes
ORDER BY ps.periodo_anio, ps.periodo_mes;


-- ── 9. DIAGNÓSTICO RÁPIDO: ¿POR QUÉ NO CALCULA HIERRO? ─────────────────────
-- Si ISAT o Ferritina son NULL en snapshot, el motor no puede calcular hierro IV.
-- Esta consulta muestra exactamente qué está faltando para cada paciente.

SELECT
    p.identificacion,
    p.primer_apellido || ' ' || p.primer_nombre AS paciente,
    s.periodo_anio   AS año,
    s.periodo_mes    AS mes,
    CASE WHEN s.hb_valor IS NULL         THEN 'FALTA HB '           ELSE '' END ||
    CASE WHEN s.saturacion_valor IS NULL THEN 'FALTA ISAT '         ELSE '' END ||
    CASE WHEN s.ferritina_valor IS NULL  THEN 'FALTA FERRITINA '    ELSE '' END ||
    CASE WHEN s.hierro_valor IS NULL     THEN 'FALTA HIERRO_SERICO' ELSE '' END
        AS datos_faltantes,
    ps.epo_ui_semana,
    ps.hierro_mg_mes
FROM snapshots_mensuales s
JOIN pacientes p ON p.id = s.paciente_id
LEFT JOIN prescripciones_sugeridas ps ON ps.paciente_id = s.paciente_id
    AND ps.period_date = s.period_date AND ps.activo = true
WHERE s.activo = true
  AND (s.saturacion_valor IS NULL OR s.ferritina_valor IS NULL)
ORDER BY s.periodo_anio DESC, s.periodo_mes DESC, p.primer_apellido;
