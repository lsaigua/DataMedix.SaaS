-- =============================================================================
-- DATAMEDIX SAAS - DATOS SEMILLA (SEED)
-- Roles, Parámetros Clínicos, Alias, Rangos de Prescripción
-- =============================================================================

-- ========================
-- ROLES
-- ========================
INSERT INTO rol (nombre, descripcion, es_global) VALUES
    ('SUPERADMIN',   'Administrador global del sistema. Ve todo.',             TRUE),
    ('ADMIN',        'Administrador de empresa/laboratorio.',                  FALSE),
    ('MEDICO',       'Médico. Acceso a prescripción y aprobación clínica.',   FALSE),
    ('OPERADOR',     'Operador de carga, validación y digitación.',            FALSE),
    ('VISUALIZADOR', 'Solo lectura de datos clínicos.',                       FALSE)
ON CONFLICT (nombre) DO NOTHING;

-- ========================
-- PARÁMETROS CLÍNICOS NORMALIZADOS
-- ========================
INSERT INTO parametro_clinico (codigo, nombre, descripcion, unidad_medida_default, tipo_dato,
    valor_min_referencia, valor_max_referencia, es_parametro_clave, orden_visualizacion) VALUES
    ('HB',   'Hemoglobina',               'Hemoglobina en sangre',                   'g/dL',   'numerico', 10.0,  12.0,  TRUE,  1),
    ('FE',   'Hierro',                    'Hierro sérico',                            'ug/dL',  'numerico', 60.0,  170.0, TRUE,  2),
    ('FERR', 'Ferritina',                 'Ferritina sérica',                         'ng/mL',  'numerico', 200.0, 500.0, TRUE,  3),
    ('ISAT', 'Saturación de Transferrina','Índice de saturación de transferrina',     '%',      'numerico', 20.0,  50.0,  TRUE,  4),
    ('CREAT','Creatinina',                'Creatinina sérica',                        'mg/dL',  'numerico', 0.7,   1.3,   FALSE, 5),
    ('UREA', 'Urea',                      'Urea sérica',                              'mg/dL',  'numerico', 15.0,  45.0,  FALSE, 6),
    ('BUN',  'BUN',                       'Nitrógeno ureico en sangre',               'mg/dL',  'numerico', 7.0,   20.0,  FALSE, 7),
    ('PCR',  'Proteína C Reactiva',       'PCR cuantitativa',                         'mg/L',   'numerico', 0.0,   5.0,   FALSE, 8),
    ('ALB',  'Albúmina',                  'Albúmina sérica',                          'g/dL',   'numerico', 3.5,   5.0,   FALSE, 9),
    ('KT_V', 'Kt/V',                      'Eficiencia de diálisis',                   '',       'numerico', 1.2,   2.0,   FALSE, 10),
    ('PTH',  'PTH',                       'Parathormona intacta',                     'pg/mL',  'numerico', 150.0, 600.0, FALSE, 11),
    ('CALC', 'Calcio',                    'Calcio sérico',                            'mg/dL',  'numerico', 8.4,   10.2,  FALSE, 12),
    ('FOSF', 'Fósforo',                   'Fósforo sérico',                           'mg/dL',  'numerico', 2.5,   5.5,   FALSE, 13),
    ('TRANSF','Transferrina',             'Transferrina sérica',                      'mg/dL',  'numerico', 200.0, 360.0, FALSE, 14),
    ('VCM',  'VCM',                       'Volumen corpuscular medio',                'fL',     'numerico', 80.0,  100.0, FALSE, 15),
    ('HCM',  'HCM',                       'Hemoglobina corpuscular media',            'pg',     'numerico', 27.0,  33.0,  FALSE, 16)
ON CONFLICT (codigo) DO NOTHING;

-- ========================
-- ALIAS DE PARÁMETROS (normalización)
-- Nota: ON CONFLICT no funciona con tenant_id nullable (NULL != NULL en SQL).
-- Se usa WHERE NOT EXISTS para idempotencia segura.
-- ========================

-- Hemoglobina
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'HB')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('HB'),('Hb'),('hb'),('HEMOGLOBINA'),('Hemoglobina'),('hemoglobina'),
            ('HGB'),('Hgb'),('hgb'),('HEMOGLOBINA g/dL'),('Hemoglobina g/dL'),
            ('HEMATOGLOBINA'),('Hematoglobina')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- Hierro
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'FE')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('HIERRO'),('Hierro'),('hierro'),('Fe'),('FE'),('fe'),
            ('FIERRO'),('Fierro'),('HIERRO SERICO'),('Hierro sérico'),
            ('IRON'),('Iron'),('SERIC IRON'),('Hierro Suero')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- Ferritina
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'FERR')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('FERRITINA'),('Ferritina'),('ferritina'),('FERR'),('Ferr'),
            ('FERRITINA SERICA'),('Ferritina sérica'),('FERRITINA SERO')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- Saturación de Transferrina
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'ISAT')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('ISAT'),('IST'),('% SATURACION'),('% Saturación'),('SATURACION'),('Saturación'),
            ('SATURACION TRANSFERRINA'),('Saturación de Transferrina'),
            ('INDICE SATURACION'),('Índice de Saturación'),
            ('INDICE DE SATURACION'),('SAT. TRANSFERRINA'),
            ('TRANSFERRINA SAT'),('TSAT'),('T-SAT')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- Creatinina
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'CREAT')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('CREATININA'),('Creatinina'),('creatinina'),('CREAT'),('Creat'),
            ('CREATININA SERICA'),('Cr'),('CR')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- Urea
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'UREA')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('UREA'),('Urea'),('urea'),('UREA SERICA')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- BUN
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'BUN')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('BUN'),('Bun'),('bun'),('NITROGENO UREICO'),('Nitrógeno Ureico'),
            ('NITROGEN UREICO'),('NUS'),('UREA NITROGENO')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- PCR
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'PCR')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('PCR'),('Pcr'),('pcr'),('PROTEINA C REACTIVA'),('Proteína C Reactiva'),
            ('CRP'),('C REACTIVE PROTEIN'),('PROTEINA C')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- Albúmina
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'ALB')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('ALBUMINA'),('Albúmina'),('albumina'),('ALB'),('Alb'),
            ('ALBUMIN'),('Albumin'),('ALBUMINA SERICA')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- Kt/V
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'KT_V')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('KT/V'),('Kt/V'),('ktv'),('KTV'),('kt/v'),
            ('EFICIENCIA DIALISIS'),('Eficiencia diálisis'),
            ('ADEC DIALISIS'),('KT')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- PTH
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'PTH')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('PTH'),('Pth'),('pth'),('PARATHORMONA'),('Parathormona'),
            ('iPTH'),('IPTH'),('PARATOHORMONA'),('PTH INTACTA'),
            ('HORMONA PARATIROIDEA')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- Calcio
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'CALC')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('CALCIO'),('Calcio'),('calcio'),('Ca'),('CA'),('ca'),
            ('CALCIUM'),('Calcium'),('CALCIO TOTAL'),('CALCIO IONICO')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- Fósforo
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'FOSF')
INSERT INTO alias_parametro (parametro_clinico_id, alias)
SELECT p.id, a.alias FROM p,
    (VALUES ('FOSFORO'),('Fósforo'),('fosforo'),('P'),('PHOSPHORUS'),
            ('Phosphorus'),('FOSF'),('FOSFATO'),('Fosfato')) AS a(alias)
WHERE NOT EXISTS (
    SELECT 1 FROM alias_parametro ap
    WHERE LOWER(ap.alias) = LOWER(a.alias) AND ap.tenant_id IS NULL);

-- ========================
-- RANGOS A PRESCRIBIR
-- ========================

-- Rangos EPO basados en Hemoglobina
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'HB')
INSERT INTO rango_prescriba (parametro_clinico_id, nombre, descripcion,
    valor_minimo, valor_maximo, accion, medicamento, dosis_sugerida,
    ajuste_porcentaje, observacion, orden, vigente_desde)
SELECT p.id, r.nombre, r.descripcion, r.vmin, r.vmax, r.accion,
       r.medicamento, r.dosis, r.ajuste, r.obs, r.orden, CURRENT_DATE
FROM p,
(VALUES
    ('HB < 9 g/dL - Aumentar EPO 25%',
     'Anemia severa. Aumentar dosis EPO significativamente',
     NULL, 9, 'AUMENTAR', 'EPO', 'Aumentar 25% dosis actual', 25,
     'Descartar causas secundarias. Verificar acceso vascular y cumplimiento de diálisis.', 1),

    ('HB 9-10 g/dL - Aumentar EPO 10%',
     'Anemia moderada. Aumentar dosis EPO',
     9, 10, 'AUMENTAR', 'EPO', 'Aumentar 10% dosis actual', 10,
     'Monitorear respuesta en próximo mes. Verificar déficit de hierro.', 2),

    ('HB 10-12 g/dL - Mantener EPO',
     'HB en rango objetivo KDIGO. Mantener dosis actual',
     10, 12, 'MANTENER', 'EPO', 'Mantener dosis actual', 0,
     'Rango objetivo de hemoglobina según guías KDIGO. Continuar evaluación mensual.', 3),

    ('HB 12-13 g/dL - Reducir EPO 10%',
     'HB ligeramente elevada. Reducir dosis EPO',
     12, 13, 'REDUCIR', 'EPO', 'Reducir 10% dosis actual', -10,
     'Vigilar descenso gradual. Riesgo moderado de eventos cardiovasculares.', 4),

    ('HB > 13 g/dL - Suspender EPO',
     'HB muy elevada. Suspender EPO hasta normalización',
     13, 999, 'SUSPENDER', 'EPO', 'Suspender EPO hasta HB < 12 g/dL', 0,
     'Riesgo cardiovascular aumentado con HB > 13. Reevaluar en 4 semanas.', 5)
) AS r(nombre, descripcion, vmin, vmax, accion, medicamento, dosis, ajuste, obs, orden);

-- Rangos Hierro IV basados en Ferritina
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'FERR')
INSERT INTO rango_prescriba (parametro_clinico_id, nombre, descripcion,
    valor_minimo, valor_maximo, accion, medicamento, dosis_sugerida,
    ajuste_porcentaje, observacion, orden, vigente_desde)
SELECT p.id, r.nombre, r.descripcion, r.vmin, r.vmax, r.accion,
       r.medicamento, r.dosis, r.ajuste, r.obs, r.orden, CURRENT_DATE
FROM p,
(VALUES
    ('Ferritina < 100 - Carga IV urgente',
     'Déficit severo de hierro. Iniciar carga IV inmediata',
     NULL, 100, 'AUMENTAR', 'HIERRO_IV', '200mg IV 2x/semana x 5 semanas', 0,
     'Verificar que no haya infección activa. Puede haber resistencia a EPO por déficit.', 1),

    ('Ferritina 100-200 - Iniciar Hierro IV',
     'Déficit de hierro. Iniciar reposición',
     100, 200, 'AUMENTAR', 'HIERRO_IV', '100mg IV 1x/semana x 4 semanas', 0,
     'Evaluar ISAT complementariamente. Si ISAT < 20% confirmar déficit funcional.', 2),

    ('Ferritina 200-500 - Mantener Hierro',
     'Ferritina en rango objetivo. Mantener esquema actual',
     200, 500, 'MANTENER', 'HIERRO_IV', 'Mantener dosis actual de mantenimiento', 0,
     'Verificar ISAT. Si ISAT < 20% con Ferritina normal considerar déficit funcional.', 3),

    ('Ferritina > 500 - Suspender Hierro',
     'Ferritina elevada. Suspender hierro IV',
     500, 99999, 'SUSPENDER', 'HIERRO_IV', 'Suspender hasta Ferritina < 500 ng/mL', 0,
     'Riesgo de sobrecarga de hierro. Descartar inflamación activa (PCR elevada puede elevar Ferritina).', 4)
) AS r(nombre, descripcion, vmin, vmax, accion, medicamento, dosis, ajuste, obs, orden);

-- Rangos complementarios ISAT
WITH p AS (SELECT id FROM parametro_clinico WHERE codigo = 'ISAT')
INSERT INTO rango_prescriba (parametro_clinico_id, nombre, descripcion,
    valor_minimo, valor_maximo, accion, medicamento, dosis_sugerida,
    ajuste_porcentaje, observacion, orden, vigente_desde)
SELECT p.id, r.nombre, r.descripcion, r.vmin, r.vmax, r.accion,
       r.medicamento, r.dosis, r.ajuste, r.obs, r.orden, CURRENT_DATE
FROM p,
(VALUES
    ('ISAT < 20% - Déficit funcional hierro',
     'Déficit funcional de hierro independiente de Ferritina',
     NULL, 20, 'AUMENTAR', 'HIERRO_IV', 'Iniciar/aumentar hierro IV', 0,
     'Déficit funcional aún con Ferritina normal-alta. Priorizar hierro antes de aumentar EPO.', 1),

    ('ISAT 20-50% - Rango adecuado',
     'Saturación de transferrina en rango objetivo',
     20, 50, 'MANTENER', 'HIERRO_IV', 'Mantener esquema actual', 0,
     'Rango adecuado según guías KDIGO.', 2),

    ('ISAT > 50% - Sobresaturación',
     'Saturación excesiva. Suspender hierro IV',
     50, 100, 'SUSPENDER', 'HIERRO_IV', 'Suspender hierro IV', 0,
     'Riesgo de estrés oxidativo por exceso de hierro libre.', 3)
) AS r(nombre, descripcion, vmin, vmax, accion, medicamento, dosis, ajuste, obs, orden);

-- ========================
-- TENANT Y USUARIO DE EJEMPLO
-- ========================
DO $$
DECLARE
    v_tenant_id UUID;
    v_admin_rol UUID;
    v_medico_rol UUID;
    v_operador_rol UUID;
    v_admin_id UUID;
BEGIN
    -- Tenant de ejemplo
    INSERT INTO tenant (codigo, nombre, ruc, subdomain, email_contacto, ciudad, pais)
    VALUES ('LAB001', 'Laboratorio Ejemplo S.A.', '1790000001001', 'lab001', 'admin@lab001.com', 'Quito', 'Ecuador')
    ON CONFLICT (subdomain) DO NOTHING
    RETURNING id INTO v_tenant_id;

    IF v_tenant_id IS NULL THEN
        SELECT id INTO v_tenant_id FROM tenant WHERE subdomain = 'lab001';
    END IF;

    -- Obtener roles
    SELECT id INTO v_admin_rol FROM rol WHERE nombre = 'ADMIN';
    SELECT id INTO v_medico_rol FROM rol WHERE nombre = 'MEDICO';
    SELECT id INTO v_operador_rol FROM rol WHERE nombre = 'OPERADOR';

    -- Usuario admin de ejemplo (password: Admin@2026)
    -- Hash BCrypt de 'Admin@2026'
    INSERT INTO usuario (tenant_id, codigo, identificacion, primer_nombre, primer_apellido, email, password_hash)
    VALUES (v_tenant_id, 'ADM001', '0000000001', 'Administrador', 'Sistema', 'admin@datamedix.com',
            '$2a$11$rBnFCjQJuLLYQ7vWWqJfQeIPF0Y9KCGsAVzMR.SBkgXxXmpOKeDXi')
    ON CONFLICT (email) DO NOTHING
    RETURNING id INTO v_admin_id;

    IF v_admin_id IS NULL THEN
        SELECT id INTO v_admin_id FROM usuario WHERE email = 'admin@datamedix.com';
    END IF;

    -- Asignar rol ADMIN
    INSERT INTO usuario_rol (usuario_id, rol_id, tenant_id)
    VALUES (v_admin_id, v_admin_rol, v_tenant_id)
    ON CONFLICT (usuario_id, rol_id) DO NOTHING;

END $$;
