-- =============================================================================
-- DataMedix.SaaS — Seed inicial (datos de configuración)
-- Ejecutar DESPUÉS de 000_schema_completo.sql
-- Idempotente: usa ON CONFLICT DO NOTHING
-- =============================================================================

-- pgcrypto para generar hash BCrypt de contraseñas
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =============================================================================
-- 1. ROLES
-- =============================================================================
INSERT INTO public.rol (id, nombre, descripcion, es_global, activo)
VALUES
    ('00000000-0000-0000-0000-000000000001', 'SUPERADMIN',   'Super administrador global — acceso a todos los tenants', true,  true),
    ('00000000-0000-0000-0000-000000000002', 'ADMIN',        'Administrador del laboratorio',                           false, true),
    ('00000000-0000-0000-0000-000000000003', 'MEDICO',       'Médico tratante — revisa y aprueba prescripciones',       false, true),
    ('00000000-0000-0000-0000-000000000004', 'OPERADOR',     'Operador de importación y entrada de datos',              false, true),
    ('00000000-0000-0000-0000-000000000005', 'VISUALIZADOR', 'Solo lectura — historial y reportes',                     false, true)
ON CONFLICT (nombre) DO NOTHING;

-- =============================================================================
-- 2. PARÁMETROS CLÍNICOS (catálogo global, sin tenant)
-- =============================================================================
INSERT INTO public.parametro_clinico
    (id, codigo, nombre, descripcion, unidad_medida_default, tipo_dato,
     valor_min_referencia, valor_max_referencia, es_parametro_clave, orden_visualizacion)
VALUES
    -- Parámetros clave del motor de reglas (anemia renal)
    ('10000000-0000-0000-0000-000000000001', 'HB',          'Hemoglobina',
     'Concentración de hemoglobina en sangre',
     'g/dL', 'numerico', 11.5, 13.0, true, 1),

    ('10000000-0000-0000-0000-000000000002', 'FE',          'Hierro sérico',
     'Concentración sérica de hierro',
     'µg/dL', 'numerico', 60, 170, true, 2),

    ('10000000-0000-0000-0000-000000000003', 'FERR',        'Ferritina sérica',
     'Marcador de reservas de hierro',
     'ng/mL', 'numerico', 200, 850, true, 3),

    ('10000000-0000-0000-0000-000000000004', 'ISAT',        'Saturación de transferrina (TSAT)',
     'Porcentaje de saturación de transferrina (%)',
     '%', 'numerico', 20, 50, true, 4),

    -- Parámetros adicionales monitoreados en HD
    ('10000000-0000-0000-0000-000000000005', 'POTASIO',     'Potasio sérico',
     'Electrolito — riesgo de hiperkalemia en HD',
     'mEq/L', 'numerico', 3.5, 5.5, true, 5),

    ('10000000-0000-0000-0000-000000000006', 'PTH',         'Paratohormona intacta (PTH)',
     'Control de hiperparatiroidismo en ERC',
     'pg/mL', 'numerico', 150, 600, false, 6),

    ('10000000-0000-0000-0000-000000000007', 'ALBUMINA',    'Albúmina sérica',
     'Marcador de estado nutricional',
     'g/dL', 'numerico', 3.5, 5.0, false, 7),

    ('10000000-0000-0000-0000-000000000008', 'CREATININA',  'Creatinina sérica',
     'Marcador de función renal residual',
     'mg/dL', 'numerico', null, null, false, 8),

    ('10000000-0000-0000-0000-000000000009', 'BUN',         'Nitrógeno ureico (BUN)',
     'Producto del metabolismo proteico',
     'mg/dL', 'numerico', null, null, false, 9),

    ('10000000-0000-0000-0000-000000000010', 'SODIO',       'Sodio sérico',
     'Electrolito — equilibrio hídrico',
     'mEq/L', 'numerico', 136, 145, false, 10),

    ('10000000-0000-0000-0000-000000000011', 'CALCIO',      'Calcio sérico',
     'Control del metabolismo óseo mineral',
     'mg/dL', 'numerico', 8.5, 10.5, false, 11),

    ('10000000-0000-0000-0000-000000000012', 'FOSFORO',     'Fósforo sérico',
     'Control del metabolismo óseo mineral',
     'mg/dL', 'numerico', 3.5, 5.5, false, 12),

    ('10000000-0000-0000-0000-000000000013', 'PESO',        'Peso corporal',
     'Peso del paciente en kilogramos',
     'kg', 'numerico', null, null, false, 13)
ON CONFLICT (id) DO UPDATE
    SET codigo                = EXCLUDED.codigo,
        nombre                = EXCLUDED.nombre,
        descripcion           = EXCLUDED.descripcion,
        unidad_medida_default = EXCLUDED.unidad_medida_default,
        es_parametro_clave    = EXCLUDED.es_parametro_clave;

-- Alias de parámetros (nombres alternativos que pueden llegar en archivos Excel)
-- alias_parametro no tiene UNIQUE constraint → usamos WHERE NOT EXISTS para idempotencia
INSERT INTO public.alias_parametro (parametro_clinico_id, tenant_id, alias)
SELECT v.pid::uuid, v.tid::uuid, v.als
FROM (VALUES
    ('10000000-0000-0000-0000-000000000001', null, 'HB'),
    ('10000000-0000-0000-0000-000000000001', null, 'HEMOGLOBINA'),
    ('10000000-0000-0000-0000-000000000001', null, 'HGB'),
    ('10000000-0000-0000-0000-000000000002', null, 'HIERRO'),
    ('10000000-0000-0000-0000-000000000002', null, 'HIERRO SERICO'),
    ('10000000-0000-0000-0000-000000000002', null, 'FE'),
    ('10000000-0000-0000-0000-000000000003', null, 'FERRITINA'),
    ('10000000-0000-0000-0000-000000000004', null, 'SATURACION'),
    ('10000000-0000-0000-0000-000000000004', null, 'SATURACION DE TRANSFERRINA'),
    ('10000000-0000-0000-0000-000000000004', null, 'TSAT'),
    ('10000000-0000-0000-0000-000000000004', null, 'ISAT'),
    ('10000000-0000-0000-0000-000000000005', null, 'POTASIO'),
    ('10000000-0000-0000-0000-000000000005', null, 'K'),
    ('10000000-0000-0000-0000-000000000005', null, 'K+'),
    ('10000000-0000-0000-0000-000000000006', null, 'PTH'),
    ('10000000-0000-0000-0000-000000000006', null, 'PARATOHORMONA'),
    ('10000000-0000-0000-0000-000000000006', null, 'PARATHORMONA'),
    ('10000000-0000-0000-0000-000000000007', null, 'ALBUMINA'),
    ('10000000-0000-0000-0000-000000000007', null, 'ALB'),
    ('10000000-0000-0000-0000-000000000008', null, 'CREATININA'),
    ('10000000-0000-0000-0000-000000000008', null, 'CREAT'),
    ('10000000-0000-0000-0000-000000000009', null, 'BUN'),
    ('10000000-0000-0000-0000-000000000010', null, 'SODIO'),
    ('10000000-0000-0000-0000-000000000010', null, 'NA'),
    ('10000000-0000-0000-0000-000000000010', null, 'NA+'),
    ('10000000-0000-0000-0000-000000000011', null, 'CALCIO'),
    ('10000000-0000-0000-0000-000000000011', null, 'CA'),
    ('10000000-0000-0000-0000-000000000011', null, 'CA2+'),
    ('10000000-0000-0000-0000-000000000012', null, 'FOSFORO'),
    ('10000000-0000-0000-0000-000000000012', null, 'FÓSFORO'),
    ('10000000-0000-0000-0000-000000000012', null, 'P'),
    ('10000000-0000-0000-0000-000000000013', null, 'PESO'),
    ('10000000-0000-0000-0000-000000000013', null, 'PESO KG'),
    ('10000000-0000-0000-0000-000000000013', null, 'PESOKG')
) AS v(pid, tid, als)
WHERE NOT EXISTS (
    SELECT 1 FROM public.alias_parametro a
    WHERE a.parametro_clinico_id = v.pid::uuid
      AND a.tenant_id IS NOT DISTINCT FROM v.tid::uuid
      AND a.alias = v.als
);

-- =============================================================================
-- 3. TENANT INICIAL (demo / desarrollo)
-- =============================================================================
INSERT INTO public.tenant
    (id, codigo, nombre, subdomain, email_contacto, pais, activo, isolation_mode, plan_nombre)
VALUES
    ('20000000-0000-0000-0000-000000000001',
     'DEMO', 'DataMedix Demo Lab', 'demo',
     'admin@datamedix.com', 'Ecuador', true, 'shared', 'Starter')
ON CONFLICT (codigo) DO NOTHING;

-- =============================================================================
-- 4. USUARIOS INICIALES
-- Contraseña por defecto: Admin@2026!
-- IMPORTANTE: cambiar en primer inicio de sesión.
-- =============================================================================

-- Super Admin global (sin tenant — accede a todos los datos)
INSERT INTO public.usuario
    (id, tenant_id, codigo, identificacion,
     primer_nombre, primer_apellido,
     email, password_hash, activo)
VALUES
    ('30000000-0000-0000-0000-000000000001',
     null, 'SA001', '0000000001',
     'Super', 'Admin',
     'superadmin@datamedix.com',
     crypt('Admin@2026!', gen_salt('bf', 12)),
     true)
ON CONFLICT (email) DO NOTHING;

-- Admin del tenant demo
INSERT INTO public.usuario
    (id, tenant_id, codigo, identificacion,
     primer_nombre, primer_apellido,
     email, password_hash, activo)
VALUES
    ('30000000-0000-0000-0000-000000000002',
     '20000000-0000-0000-0000-000000000001',
     'ADM001', '0000000002',
     'Admin', 'Demo',
     'admin@demo.datamedix.com',
     crypt('Admin@2026!', gen_salt('bf', 12)),
     true)
ON CONFLICT (email) DO NOTHING;

-- Asignar roles
INSERT INTO public.usuario_rol (usuario_id, rol_id, tenant_id, activo)
VALUES
    -- SuperAdmin → rol SUPERADMIN (global, sin tenant)
    ('30000000-0000-0000-0000-000000000001',
     '00000000-0000-0000-0000-000000000001',
     null, true),

    -- Admin demo → rol ADMIN del tenant demo
    ('30000000-0000-0000-0000-000000000002',
     '00000000-0000-0000-0000-000000000002',
     '20000000-0000-0000-0000-000000000001', true)
ON CONFLICT DO NOTHING;

-- =============================================================================
-- 5. REGLAS CLÍNICAS (motor de prescripción)
-- Espejo exacto de ReglasSeed.cs — idempotente por UNIQUE en codigo.
-- La aplicación también las inserta automáticamente en startup,
-- pero tenerlas aquí permite arrancar/verificar sin levantar la app.
-- =============================================================================

INSERT INTO public.reglas_clinicas
    (id, codigo, nombre, tipo, prioridad, severidad, condiciones_json, accion_json, version, activo)
VALUES

-- ── BLOQUE 1: EPO (prioridad 100-106) ────────────────────────────────────────
('RC000001-0000-0000-0000-000000000001',
 'EPO-01', 'Hb > 13 — EPO no recomendada', 'EPO', 100, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":">","value":13}]}',
 '{"tipo":"EPO","epo_ui_semana":0,"recomendar":false,"mensaje":"Hb > 13 g/dL — No se recomienda EPO. Riesgo cardiovascular aumentado."}',
 1, true),

('RC000001-0000-0000-0000-000000000002',
 'EPO-02', 'Hb 12.5–13 — EPO 2000 UI/sem', 'EPO', 101, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":12.5},{"param":"Hb","op":"<","value":13}]}',
 '{"tipo":"EPO","epo_ui_semana":2000,"recomendar":true,"mensaje":"Hb en rango alto — dosis de mantenimiento mínima."}',
 1, true),

('RC000001-0000-0000-0000-000000000003',
 'EPO-03', 'Hb 11.7–12.5 — EPO 4000 UI/sem', 'EPO', 102, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":11.7},{"param":"Hb","op":"<","value":12.5}]}',
 '{"tipo":"EPO","epo_ui_semana":4000,"recomendar":true,"mensaje":"Hb en zona objetivo alta."}',
 1, true),

('RC000001-0000-0000-0000-000000000004',
 'EPO-04', 'Hb 11–11.7 — EPO 6000 UI/sem', 'EPO', 103, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":11},{"param":"Hb","op":"<","value":11.7}]}',
 '{"tipo":"EPO","epo_ui_semana":6000,"recomendar":true,"mensaje":"Hb en zona objetivo. Mantener."}',
 1, true),

('RC000001-0000-0000-0000-000000000005',
 'EPO-05', 'Hb 10–11 — EPO 8000 UI/sem', 'EPO', 104, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":10},{"param":"Hb","op":"<","value":11}]}',
 '{"tipo":"EPO","epo_ui_semana":8000,"recomendar":true,"mensaje":"Hb por debajo de objetivo — incrementar dosis."}',
 1, true),

('RC000001-0000-0000-0000-000000000006',
 'EPO-06', 'Hb 8–10 — EPO 12000 UI/sem', 'EPO', 105, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":8},{"param":"Hb","op":"<","value":10}]}',
 '{"tipo":"EPO","epo_ui_semana":12000,"recomendar":true,"mensaje":"Anemia moderada — dosis terapéutica."}',
 1, true),

('RC000001-0000-0000-0000-000000000007',
 'EPO-07', 'Hb < 8 — EPO 18000 UI/sem (anemia severa)', 'EPO', 106, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":8}]}',
 '{"tipo":"EPO","epo_ui_semana":18000,"recomendar":true,"mensaje":"Anemia severa — dosis máxima. Evaluar transfusión si sintomática."}',
 1, true),

-- ── BLOQUE 2: HIERRO EV (prioridad 200-213) ──────────────────────────────────
('RC000002-0000-0000-0000-000000000001',
 'FE-R12', 'Hb >= 13 — Hierro no recomendado', 'HIERRO', 200, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":13}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":0,"recomendar":false,"mensaje":"Hb >= 13 — Hierro EV no recomendado de rutina."}',
 1, true),

('RC000002-0000-0000-0000-000000000002',
 'FE-R01', 'Ferropenia absoluta severa — 1000 mg/mes', 'HIERRO', 201, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"<","value":20},{"param":"Ferritina","op":"<","value":200}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":1000,"dosis_individual_mg":200,"frecuencia":"diálisis seguidas hasta completar dosis mensual","recomendar":true,"mensaje":"Ferropenia absoluta severa — reposición máxima."}',
 1, true),

('RC000002-0000-0000-0000-000000000003',
 'FE-R11', 'Hb<10 + TSAT<20 + Ferritina alta — 400 mg/mes', 'HIERRO', 202, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":10},{"param":"TSAT","op":"<","value":20},{"param":"Ferritina","op":"between","value":[850,2001]}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":400,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Anemia con saturación baja pese a ferritina alta — ferropenia funcional."}',
 1, true),

('RC000002-0000-0000-0000-000000000004',
 'FE-R10', 'Hb<10 + TSAT 20-30 + Ferritina muy alta — 200 mg/mes', 'HIERRO', 203, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":10},{"param":"TSAT","op":"between","value":[20,30],"inclusive":"left"},{"param":"Ferritina","op":"between","value":[850,2000],"inclusive":"left"}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":200,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Anemia con reservas elevadas — dosis conservadora."}',
 1, true),

('RC000002-0000-0000-0000-000000000005',
 'FE-R07', 'Hb<10 + TSAT 20-30 + Ferritina<500 — 600 mg/mes', 'HIERRO', 204, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":10},{"param":"TSAT","op":"between","value":[20,30],"inclusive":"left"},{"param":"Ferritina","op":"<","value":500}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":600,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Anemia con saturación baja — dosis intermedia."}',
 1, true),

('RC000002-0000-0000-0000-000000000006',
 'FE-R03', 'Hb<11.5 + TSAT<20 + Ferritina<850 — 400 mg/mes', 'HIERRO', 205, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"<","value":20},{"param":"Ferritina","op":"<","value":850}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":400,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Ferropenia funcional/absoluta moderada."}',
 1, true),

('RC000002-0000-0000-0000-000000000007',
 'FE-R02', 'Hb<11.5 + TSAT<=20 + Ferritina<=500 — 600 mg/mes', 'HIERRO', 206, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"<=","value":20},{"param":"Ferritina","op":"<=","value":500}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":600,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Ferropenia con reservas bajas — dosis intermedia-alta."}',
 1, true),

('RC000002-0000-0000-0000-000000000008',
 'FE-R05', 'Hb<11.5 + TSAT 30-40 + Ferritina<200 — 400 mg/mes', 'HIERRO', 207, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"between","value":[30,40],"inclusive":"left"},{"param":"Ferritina","op":"<","value":200}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":400,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Reservas bajas pese a saturación adecuada."}',
 1, true),

('RC000002-0000-0000-0000-000000000009',
 'FE-R06', 'Hb<11.5 + TSAT 30-40 + Ferritina 200-500 — 200 mg/mes', 'HIERRO', 208, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"between","value":[30,40],"inclusive":"left"},{"param":"Ferritina","op":"between","value":[200,500],"inclusive":"left"}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":200,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Mantenimiento mínimo con reservas adecuadas."}',
 1, true),

('RC000002-0000-0000-0000-000000000010',
 'FE-R08', 'Hb 11.5-13 + TSAT<20 + Ferritina<200 — 400 mg/mes', 'HIERRO', 209, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"between","value":[11.5,13],"inclusive":"left"},{"param":"TSAT","op":"<","value":20},{"param":"Ferritina","op":"<","value":200}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":400,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Hb en objetivo pero reservas pobres — reponer."}',
 1, true),

('RC000002-0000-0000-0000-000000000011',
 'FE-R04', 'Hb 11.5-13 + TSAT<30 + Ferritina 200-500 — 200 mg/mes', 'HIERRO', 210, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"between","value":[11.5,13],"inclusive":"left"},{"param":"TSAT","op":"<","value":30},{"param":"Ferritina","op":"between","value":[200,500],"inclusive":"left"}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":200,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Mantenimiento conservador."}',
 1, true),

('RC000002-0000-0000-0000-000000000012',
 'FE-R09', 'Hb 11.5-13 + TSAT 20-30 + Ferritina<200 — 200 mg/mes', 'HIERRO', 211, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"between","value":[11.5,13],"inclusive":"left"},{"param":"TSAT","op":"between","value":[20,30],"inclusive":"left"},{"param":"Ferritina","op":"<","value":200}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":200,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Mantenimiento por reservas bajas."}',
 1, true),

('RC000002-0000-0000-0000-000000000013',
 'FE-R13', 'Hb<11.5 + TSAT 20-30 + Ferritina 500-850 — 200 mg/mes', 'HIERRO', 212, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":11.5},{"param":"TSAT","op":"between","value":[20,30],"inclusive":"left"},{"param":"Ferritina","op":"between","value":[500,850],"inclusive":"left"}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":200,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Ferropenia funcional con reservas intermedias — mantenimiento."}',
 1, true),

('RC000002-0000-0000-0000-000000000014',
 'FE-R14', 'Hb 10-11.5 + TSAT 20-30 + Ferritina<500 — 400 mg/mes', 'HIERRO', 213, null,
 '{"operator":"AND","conditions":[{"param":"Hb","op":"between","value":[10,11.5],"inclusive":"left"},{"param":"TSAT","op":"between","value":[20,30],"inclusive":"left"},{"param":"Ferritina","op":"<","value":500}]}',
 '{"tipo":"HIERRO","hierro_mg_mes":400,"dosis_individual_mg":200,"recomendar":true,"mensaje":"Anemia moderada con ferropenia — dosis intermedia."}',
 1, true),

-- ── BLOQUE 3: ALERTAS CLÍNICAS (prioridad 300-340) ───────────────────────────
('RC000003-0000-0000-0000-000000000001',
 'ALERT-HB-CRIT', 'Hb < 8 — Anemia severa', 'ALERTA', 300, 'CRITICA',
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":8}]}',
 '{"tipo":"ALERTA","flag":"HB_CRITICA","requiere_revision_medica":true,"mensaje":"Hb < 8 g/dL — Anemia severa. Evaluar transfusión urgente y causa secundaria."}',
 1, true),

('RC000003-0000-0000-0000-000000000002',
 'ALERT-HB-BAJA', 'Hb < 9 — Bajo umbral mínimo recomendado', 'ALERTA', 301, 'ALTA',
 '{"operator":"AND","conditions":[{"param":"Hb","op":">=","value":8},{"param":"Hb","op":"<","value":9}]}',
 '{"tipo":"ALERTA","flag":"HB_BAJO_UMBRAL","mensaje":"Hb < 9 g/dL — KDIGO recomienda evitar este nivel. Iniciar/intensificar AEE."}',
 1, true),

('RC000003-0000-0000-0000-000000000003',
 'ALERT-HB-ALTA', 'Hb > 11.5 — Sobre límite superior deseado', 'ALERTA', 302, 'MEDIA',
 '{"operator":"AND","conditions":[{"param":"Hb","op":">","value":11.5},{"param":"Hb","op":"<=","value":13}]}',
 '{"tipo":"ALERTA","flag":"HB_SOBRE_OBJETIVO","mensaje":"Hb > 11.5 g/dL — Sobre límite superior. Justificar si paciente de alto riesgo de sangrado."}',
 1, true),

('RC000003-0000-0000-0000-000000000004',
 'ALERT-K-CRIT', 'Potasio > 6 mEq/L — Hiperkalemia crítica', 'ALERTA', 310, 'CRITICA',
 '{"operator":"AND","conditions":[{"param":"Potasio","op":">","value":6}]}',
 '{"tipo":"ALERTA","flag":"HIPERKALEMIA","requiere_revision_medica":true,"mensaje":"K+ > 6 mEq/L — Riesgo arrítmico. Confirmar con muestra fresca y evaluar diálisis urgente."}',
 1, true),

('RC000003-0000-0000-0000-000000000005',
 'ALERT-RESIST-EPO', 'Sospecha de resistencia a EPO', 'ALERTA', 320, 'ALTA',
 '{"operator":"AND","conditions":[{"param":"Hb","op":"<","value":10},{"param":"epo_ui_semana_actual","op":">=","value":12000},{"param":"meses_sin_mejora_hb","op":">=","value":3}]}',
 '{"tipo":"ALERTA","flag":"RESISTENCIA_EPO","sugerencia_dosis_max":18000,"mensaje":"Sospecha resistencia EPO. Descartar: osteítis fibrosa, toxicidad por aluminio, hemoglobinopatías, mieloma, desnutrición, hemólisis, subdiálisis, hipotiroidismo, IECAs, alcoholismo."}',
 1, true),

('RC000003-0000-0000-0000-000000000006',
 'ALERT-PRUEBA-SENS', 'Paciente nuevo — Prueba de sensibilidad a hierro requerida', 'ALERTA', 330, 'MEDIA',
 '{"operator":"AND","conditions":[{"param":"meses_en_dialisis","op":"<","value":24},{"param":"primera_vez_hierro","op":"=","value":true}]}',
 '{"tipo":"ALERTA","flag":"REQUIERE_PRUEBA_SENSIBILIDAD","requiere_revision_medica":true,"mensaje":"Paciente con <24 meses en diálisis y primera prescripción de hierro EV. Realizar prueba de sensibilidad previa."}',
 1, true),

('RC000003-0000-0000-0000-000000000007',
 'ALERT-FERRITINA-EXTREMA', 'Ferritina > 2000 — Sobrecarga', 'ALERTA', 340, 'ALTA',
 '{"operator":"AND","conditions":[{"param":"Ferritina","op":">","value":2000}]}',
 '{"tipo":"ALERTA","flag":"SOBRECARGA_HIERRO","requiere_revision_medica":true,"mensaje":"Ferritina > 2000 ng/mL — Sospecha sobrecarga férrica. Suspender hierro EV y evaluar."}',
 1, true),

-- ── BLOQUE 4: MODIFICADORES (prioridad 400-401) ───────────────────────────────
('RC000004-0000-0000-0000-000000000001',
 'MOD-MES-IMPAR', 'Modificador mes impar — Reducir dosis hierro', 'MODIFICADOR', 400, null,
 '{"operator":"AND","conditions":[{"param":"mes_actual_es_impar","op":"=","value":true},{"param":"Ferritina","op":"<","value":850}]}',
 '{"tipo":"MODIFICADOR","aplicar":"reducir_dosis_hierro","mapeo":{"1000":600,"600":400,"400":200,"200":200},"mensaje":"Mes impar — esquema BAJA según paridad mensual."}',
 1, true),

('RC000004-0000-0000-0000-000000000002',
 'MOD-EPO-ESCALAR', 'Modificador escalamiento EPO — +6000 por no respuesta', 'MODIFICADOR', 401, null,
 '{"operator":"AND","conditions":[{"param":"es_primer_mes","op":"=","value":false},{"param":"hb_previa","op":"<","value":10},{"param":"hb_no_crecio_desde_previa","op":"=","value":true}]}',
 '{"tipo":"MODIFICADOR","aplicar":"escalar_epo","cantidad":6000,"maximo":18000,"mensaje":"Escalamiento EPO +6000 UI/sem por no respuesta (HB previa <10 sin mejora)."}',
 1, true)

ON CONFLICT (codigo) DO NOTHING;

-- =============================================================================
-- VERIFICACIÓN FINAL
-- =============================================================================
SELECT 'roles'              AS tabla, COUNT(*) FROM public.rol
UNION ALL
SELECT 'parametros_clinicos',          COUNT(*) FROM public.parametro_clinico
UNION ALL
SELECT 'alias_parametros',             COUNT(*) FROM public.alias_parametro
UNION ALL
SELECT 'tenants',                      COUNT(*) FROM public.tenant
UNION ALL
SELECT 'usuarios',                     COUNT(*) FROM public.usuario
UNION ALL
SELECT 'usuario_roles',                COUNT(*) FROM public.usuario_rol
UNION ALL
SELECT 'reglas_clinicas',              COUNT(*) FROM public.reglas_clinicas
ORDER BY tabla;
