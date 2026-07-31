-- ============================================================
-- 008_permisos_y_tarifas.sql
-- 1) Permisos configurables por rol y por tenant
-- 2) Modelos de cobro por tenant (suscripción / por paciente / mixto)
-- Ejecutar en: DEV, QA, PROD — DESPUÉS de 007_facturacion_periodo.sql
-- ============================================================

-- ============================================================
-- PARTE 1 — PERMISOS
--
-- `permiso` es un catálogo GLOBAL de acciones y opciones de menú; no lleva
-- tenant porque el conjunto de funciones de la aplicación es el mismo para
-- todos. Lo que sí es por tenant es la ASIGNACIÓN: cada clínica decide qué
-- rol accede a qué.
--
-- `rol_permiso.tenant_id` NULL = valor por defecto de fábrica, visible para
-- todos los tenants que no hayan definido el suyo. Un registro con tenant_id
-- concreto pisa al default para ese tenant y solo para él.
-- ============================================================

CREATE TABLE IF NOT EXISTS permiso (
    codigo          VARCHAR(60) PRIMARY KEY,
    nombre          VARCHAR(120) NOT NULL,
    descripcion     VARCHAR(300),
    grupo           VARCHAR(60)  NOT NULL,           -- agrupador en el menú y en la matriz
    ruta            VARCHAR(200),                    -- NULL = permiso de acción, no de menú
    icono           VARCHAR(40),
    orden           INTEGER NOT NULL DEFAULT 0,
    -- Permisos de plataforma: solo el dueño del SaaS, nunca delegables al tenant
    solo_superadmin BOOLEAN NOT NULL DEFAULT FALSE,
    activo          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS rol_permiso (
    id             UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id      UUID REFERENCES tenant(id) ON DELETE CASCADE,   -- NULL = default global
    rol_id         UUID NOT NULL REFERENCES rol(id) ON DELETE CASCADE,
    permiso_codigo VARCHAR(60) NOT NULL REFERENCES permiso(codigo) ON DELETE CASCADE,
    permitido      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ,
    updated_by     UUID
);

-- Un solo registro por (tenant, rol, permiso). Índices parciales porque en
-- PostgreSQL un UNIQUE normal no distingue filas con tenant_id NULL.
CREATE UNIQUE INDEX IF NOT EXISTS ux_rol_permiso_tenant
    ON rol_permiso (tenant_id, rol_id, permiso_codigo)
    WHERE tenant_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_rol_permiso_default
    ON rol_permiso (rol_id, permiso_codigo)
    WHERE tenant_id IS NULL;

CREATE INDEX IF NOT EXISTS idx_rol_permiso_lookup
    ON rol_permiso (rol_id, tenant_id);

-- ── Catálogo de permisos ─────────────────────────────────────────────────────
INSERT INTO permiso (codigo, nombre, descripcion, grupo, ruta, orden, solo_superadmin) VALUES
    ('dashboard.ver',         'Dashboard',                  'Panel principal',                              'General',        '/dashboard',                 10, false),
    ('pacientes.ver',         'Pacientes',                  'Consultar el padrón de pacientes',             'Clínico',        '/pacientes',                 20, false),
    ('pacientes.editar',      'Crear y editar pacientes',   'Alta, edición y baja de pacientes',            'Clínico',        NULL,                         21, false),
    ('laboratorio.importar',  'Importar resultados',        'Carga masiva de resultados de laboratorio',    'Laboratorio',    '/laboratorio/importar',      30, false),
    ('laboratorio.manual',    'Ingreso manual',             'Registro manual de resultados',                'Laboratorio',    '/laboratorio/manual',        31, false),
    ('laboratorio.historial', 'Historial de laboratorio',   'Consulta de resultados por período',           'Laboratorio',    '/laboratorio/historial',     32, false),
    ('historial.clinico',     'Historial clínico',          'Historial clínico mensual del paciente',       'Laboratorio',    '/historial-clinico',         33, false),
    ('prescripciones.ver',    'Prescripciones',             'Consultar prescripciones sugeridas',           'Clínico',        '/prescripciones',            40, false),
    ('prescripciones.aprobar','Aprobar prescripciones',     'Aprobar o rechazar una prescripción',          'Clínico',        NULL,                         41, false),
    ('cronograma.ver',        'Cronograma de medicación',   'Consultar el cronograma mensual',              'Clínico',        '/cronograma-medicacion',     50, false),
    ('cronograma.editar',     'Editar cronograma',          'Modificar dosis, salas y ausencias',           'Clínico',        NULL,                         51, false),
    ('cronograma.precios',    'Precios de medicación',      'Configurar precios de EPO y hierro',           'Clínico',        NULL,                         52, false),
    ('hojaepo.ver',           'Hoja EPO',                   'Hoja de dosificación de EPO',                  'Clínico',        '/hoja-epo',                  60, false),
    ('calidad.ver',           'Dashboard de calidad',       'Indicadores KDOQI y alertas',                  'Clínico',        '/clinico/dashboard-calidad', 70, false),
    ('reportes.ver',          'Reportes',                   'Reportes clínicos del grupo de pacientes',     'Análisis',       '/reportes',                  80, false),
    ('reglas.gestionar',      'Reglas clínicas',            'Motor de reglas de dosificación',              'Administración', '/clinico/reglas',            90, false),
    ('rangos.gestionar',      'Rangos Prescriba',           'Rangos de referencia para prescripción',       'Administración', '/rangos-prescriba',          91, false),
    ('cargas.ver',            'Cargas',                     'Lotes de importación y sus errores',           'Administración', '/cargas',                   100, false),
    ('usuarios.gestionar',    'Usuarios',                   'Alta y edición de usuarios del tenant',        'Administración', '/usuarios',                 110, false),
    ('roles.gestionar',       'Roles y permisos',           'Configurar qué accede cada rol',               'Administración', '/admin/roles-permisos',     111, false),
    ('configuracion.ver',     'Configuración',              'Datos del centro',                             'Administración', '/configuracion',            120, false),
    ('auditoria.ver',         'Auditoría',                  'Bitácora de acciones',                         'Administración', '/auditoria',                130, false),
    ('depuracion.ejecutar',   'Depuración de datos',        'Eliminar datos de un período',                 'Administración', '/admin/depuracion',         140, false),
    ('facturacion.ver',       'Facturación',                'Consultar el consumo y costo mensual',         'Administración', '/facturacion',              150, false),
    ('facturacion.cerrar',    'Cerrar período',             'Congelar la facturación de un mes',            'Administración', NULL,                        151, false),
    ('tarifas.configurar',    'Tarifas y planes',           'Definir el modelo de cobro de cada cliente',   'Plataforma',     '/admin/tarifas',            200, true),
    ('facturacion.global',    'Facturación consolidada',    'Ver la facturación de todos los clientes',     'Plataforma',     '/admin/facturacion-global', 201, true)
ON CONFLICT (codigo) DO UPDATE SET
    nombre          = EXCLUDED.nombre,
    descripcion     = EXCLUDED.descripcion,
    grupo           = EXCLUDED.grupo,
    ruta            = EXCLUDED.ruta,
    orden           = EXCLUDED.orden,
    solo_superadmin = EXCLUDED.solo_superadmin;

-- ── Asignación por defecto (tenant_id NULL) ──────────────────────────────────
-- Reproduce el comportamiento actual del sistema para que nada cambie al
-- desplegar. Cada tenant puede ajustarlo después desde la aplicación.

-- SUPERADMIN: todo, incluidos los permisos de plataforma
INSERT INTO rol_permiso (tenant_id, rol_id, permiso_codigo, permitido)
SELECT NULL, '00000000-0000-0000-0000-000000000001', p.codigo, true
FROM permiso p
ON CONFLICT DO NOTHING;

-- ADMIN: todo lo del tenant, nunca lo de plataforma
INSERT INTO rol_permiso (tenant_id, rol_id, permiso_codigo, permitido)
SELECT NULL, '00000000-0000-0000-0000-000000000002', p.codigo, true
FROM permiso p
WHERE p.solo_superadmin = false
ON CONFLICT DO NOTHING;

-- MEDICO: clínico completo + lectura de laboratorio y reportes
INSERT INTO rol_permiso (tenant_id, rol_id, permiso_codigo, permitido)
SELECT NULL, '00000000-0000-0000-0000-000000000003', codigo, true
FROM (VALUES
    ('dashboard.ver'), ('pacientes.ver'), ('pacientes.editar'),
    ('laboratorio.historial'), ('historial.clinico'),
    ('prescripciones.ver'), ('prescripciones.aprobar'),
    ('cronograma.ver'), ('cronograma.editar'),
    ('hojaepo.ver'), ('calidad.ver'), ('reportes.ver'),
    ('reglas.gestionar'), ('rangos.gestionar')
) AS t(codigo)
ON CONFLICT DO NOTHING;

-- OPERADOR: carga de datos, sin decisiones clínicas
INSERT INTO rol_permiso (tenant_id, rol_id, permiso_codigo, permitido)
SELECT NULL, '00000000-0000-0000-0000-000000000004', codigo, true
FROM (VALUES
    ('dashboard.ver'), ('pacientes.ver'), ('pacientes.editar'),
    ('laboratorio.importar'), ('laboratorio.manual'), ('laboratorio.historial'),
    ('historial.clinico'), ('cargas.ver')
) AS t(codigo)
ON CONFLICT DO NOTHING;

-- VISUALIZADOR: solo lectura
INSERT INTO rol_permiso (tenant_id, rol_id, permiso_codigo, permitido)
SELECT NULL, '00000000-0000-0000-0000-000000000005', codigo, true
FROM (VALUES
    ('dashboard.ver'), ('pacientes.ver'),
    ('laboratorio.historial'), ('historial.clinico'),
    ('prescripciones.ver'), ('cronograma.ver'),
    ('hojaepo.ver'), ('calidad.ver'), ('reportes.ver')
) AS t(codigo)
ON CONFLICT DO NOTHING;

-- ============================================================
-- PARTE 2 — MODELOS DE COBRO POR TENANT
--
-- Tres modelos comerciales:
--   SUSCRIPCION  = tarifa fija mensual según plan
--   POR_PACIENTE = solo consumo, con precio escalonado por volumen
--   MIXTO        = tarifa base + consumo por paciente
-- A cualquiera se le suman el soporte premium mensual y los cargos únicos
-- (implementación, migración) que apliquen al período.
-- ============================================================

ALTER TABLE tenant ADD COLUMN IF NOT EXISTS modelo_cobro VARCHAR(20) NOT NULL DEFAULT 'MIXTO';
ALTER TABLE tenant ADD COLUMN IF NOT EXISTS tarifa_soporte_mensual DECIMAL(12,2) NOT NULL DEFAULT 0;
ALTER TABLE tenant ADD COLUMN IF NOT EXISTS moneda VARCHAR(10) NOT NULL DEFAULT 'USD';
ALTER TABLE tenant ADD COLUMN IF NOT EXISTS facturacion_notas VARCHAR(500);

-- Tramos de precio por volumen de pacientes.
-- Tarifa plana por tramo: si el volumen del mes cae en 101-300, TODOS los
-- pacientes se cobran a ese precio. Es lo que comunica la tabla comercial.
CREATE TABLE IF NOT EXISTS tenant_tarifa_tramo (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenant(id) ON DELETE CASCADE,
    desde_pacientes INTEGER NOT NULL DEFAULT 1,
    hasta_pacientes INTEGER,                        -- NULL = sin límite superior
    precio_paciente DECIMAL(12,2) NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ,
    UNIQUE (tenant_id, desde_pacientes)
);

CREATE INDEX IF NOT EXISTS idx_tramo_tenant ON tenant_tarifa_tramo (tenant_id, desde_pacientes);

-- Cargos únicos o puntuales que se suman a la factura de un período concreto
CREATE TABLE IF NOT EXISTS tenant_cargo_unico (
    id            UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id     UUID NOT NULL REFERENCES tenant(id) ON DELETE CASCADE,
    concepto      VARCHAR(200) NOT NULL,
    monto         DECIMAL(12,2) NOT NULL DEFAULT 0,
    periodo_anio  INTEGER NOT NULL,
    periodo_mes   INTEGER NOT NULL CHECK (periodo_mes BETWEEN 1 AND 12),
    aplicado      BOOLEAN NOT NULL DEFAULT TRUE,
    observaciones VARCHAR(500),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by    UUID
);

CREATE INDEX IF NOT EXISTS idx_cargo_tenant_periodo
    ON tenant_cargo_unico (tenant_id, periodo_anio, periodo_mes);

-- La cabecera de facturación guarda el desglose del cobro para que la factura
-- cerrada se pueda reconstruir aunque después cambien las tarifas del tenant.
ALTER TABLE facturacion_periodo ADD COLUMN IF NOT EXISTS modelo_cobro VARCHAR(20) NOT NULL DEFAULT 'MIXTO';
ALTER TABLE facturacion_periodo ADD COLUMN IF NOT EXISTS costo_soporte    DECIMAL(12,2) NOT NULL DEFAULT 0;
ALTER TABLE facturacion_periodo ADD COLUMN IF NOT EXISTS costo_cargos     DECIMAL(12,2) NOT NULL DEFAULT 0;
ALTER TABLE facturacion_periodo ADD COLUMN IF NOT EXISTS moneda           VARCHAR(10)   NOT NULL DEFAULT 'USD';

-- Tramos por defecto según la tabla comercial publicada, solo para tenants
-- que aún no tengan tramos definidos.
INSERT INTO tenant_tarifa_tramo (tenant_id, desde_pacientes, hasta_pacientes, precio_paciente)
SELECT t.id, 1, 100, 3.50 FROM tenant t
WHERE NOT EXISTS (SELECT 1 FROM tenant_tarifa_tramo x WHERE x.tenant_id = t.id);

INSERT INTO tenant_tarifa_tramo (tenant_id, desde_pacientes, hasta_pacientes, precio_paciente)
SELECT t.id, 101, 300, 2.80 FROM tenant t
WHERE NOT EXISTS (
    SELECT 1 FROM tenant_tarifa_tramo x WHERE x.tenant_id = t.id AND x.desde_pacientes = 101);

INSERT INTO tenant_tarifa_tramo (tenant_id, desde_pacientes, hasta_pacientes, precio_paciente)
SELECT t.id, 301, NULL, 2.20 FROM tenant t
WHERE NOT EXISTS (
    SELECT 1 FROM tenant_tarifa_tramo x WHERE x.tenant_id = t.id AND x.desde_pacientes = 301);

-- ============================================================
-- VISTA — consolidado de facturación para el dueño del SaaS
-- ============================================================
DROP VIEW IF EXISTS public.vw_facturacion_mensual;

CREATE OR REPLACE VIEW public.vw_facturacion_mensual AS
SELECT
    t.id                        AS tenant_id,
    t.nombre                    AS tenant_nombre,
    t.subdomain,
    t.plan_nombre,
    fp.modelo_cobro,
    fp.periodo_anio,
    fp.periodo_mes,
    make_date(fp.periodo_anio, fp.periodo_mes, 1) AS mes,
    fp.estado,
    fp.tarifa_base,
    fp.tarifa_paciente,
    fp.pacientes_facturados,
    fp.costo_soporte,
    fp.costo_cargos,
    fp.total,
    fp.moneda,
    fp.cerrado_at
FROM public.facturacion_periodo fp
JOIN public.tenant t ON t.id = fp.tenant_id
ORDER BY fp.periodo_anio DESC, fp.periodo_mes DESC, t.nombre;
