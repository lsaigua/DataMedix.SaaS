-- =============================================================================
-- DATAMEDIX SAAS - SCHEMA COMPLETO POSTGRESQL / SUPABASE
-- Version: 2.0 | Producción
-- Arquitectura: Multi-tenant (shared DB + tenant_id)
-- =============================================================================

-- Extensiones requeridas
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm"; -- búsqueda de texto fuzzy

-- =============================================================================
-- TABLAS MAESTRAS / SEGURIDAD
-- =============================================================================

CREATE TABLE IF NOT EXISTS tenant (
    id              UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    codigo          VARCHAR(20) UNIQUE NOT NULL,
    nombre          VARCHAR(200) NOT NULL,
    ruc             VARCHAR(20),
    subdomain       VARCHAR(50) UNIQUE NOT NULL,
    logo_url        TEXT,
    email_contacto  VARCHAR(200),
    telefono        VARCHAR(20),
    direccion       TEXT,
    ciudad          VARCHAR(100),
    pais            VARCHAR(100) DEFAULT 'Ecuador',
    activo          BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    deleted_at      TIMESTAMPTZ
);
COMMENT ON TABLE tenant IS 'Empresas/Laboratorios multi-tenant';

CREATE TABLE IF NOT EXISTS rol (
    id          UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    nombre      VARCHAR(50) UNIQUE NOT NULL,
    descripcion TEXT,
    es_global   BOOLEAN     NOT NULL DEFAULT FALSE,
    activo      BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE rol IS 'Roles del sistema: SUPERADMIN, ADMIN, MEDICO, OPERADOR, VISUALIZADOR';

CREATE TABLE IF NOT EXISTS usuario (
    id              UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID        REFERENCES tenant(id),
    codigo          VARCHAR(50) NOT NULL,
    identificacion  VARCHAR(20) NOT NULL,
    primer_nombre   VARCHAR(150) NOT NULL,
    segundo_nombre  VARCHAR(150),
    primer_apellido VARCHAR(150) NOT NULL,
    segundo_apellido VARCHAR(150),
    email           VARCHAR(200) UNIQUE NOT NULL,
    telefono        VARCHAR(20),
    password_hash   VARCHAR(255) NOT NULL,
    activo          BOOLEAN     NOT NULL DEFAULT TRUE,
    ultimo_acceso   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    created_by      UUID        REFERENCES usuario(id)
);
COMMENT ON TABLE usuario IS 'Usuarios del sistema. tenant_id NULL = SuperAdmin global';

CREATE TABLE IF NOT EXISTS usuario_rol (
    id          UUID    PRIMARY KEY DEFAULT uuid_generate_v4(),
    usuario_id  UUID    NOT NULL REFERENCES usuario(id) ON DELETE CASCADE,
    rol_id      UUID    NOT NULL REFERENCES rol(id),
    tenant_id   UUID    REFERENCES tenant(id),
    activo      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(usuario_id, rol_id)
);

-- =============================================================================
-- PACIENTES
-- =============================================================================

CREATE TABLE IF NOT EXISTS paciente (
    id                  UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id           UUID        NOT NULL REFERENCES tenant(id),
    codigo              VARCHAR(50),
    identificacion      VARCHAR(20) NOT NULL,
    primer_nombre       VARCHAR(150) NOT NULL,
    segundo_nombre      VARCHAR(150),
    primer_apellido     VARCHAR(150) NOT NULL,
    segundo_apellido    VARCHAR(150),
    nombre_completo     VARCHAR(600) GENERATED ALWAYS AS (
                            TRIM(primer_nombre || ' ' || COALESCE(segundo_nombre,'') || ' ' ||
                                 primer_apellido || ' ' || COALESCE(segundo_apellido,''))
                        ) STORED,
    fecha_nacimiento    DATE,
    genero              CHAR(1),        -- M / F
    email               VARCHAR(200),
    telefono            VARCHAR(20),
    plan_salud          VARCHAR(200),
    tipo_atencion       VARCHAR(200),
    fecha_ingreso       DATE,           -- fecha de ingreso a diálisis
    medico_responsable  VARCHAR(300),
    observaciones       TEXT,
    activo              BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ,
    created_by          UUID        REFERENCES usuario(id),
    UNIQUE(tenant_id, identificacion)
);
COMMENT ON TABLE paciente IS 'Pacientes por tenant. identificacion = cédula/documento único por empresa';
COMMENT ON COLUMN paciente.fecha_ingreso IS 'Fecha ingreso a diálisis. Tiempo en diálisis = CURRENT_DATE - fecha_ingreso';

-- =============================================================================
-- PARÁMETROS CLÍNICOS NORMALIZADOS
-- =============================================================================

CREATE TABLE IF NOT EXISTS parametro_clinico (
    id                      UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    codigo                  VARCHAR(50) UNIQUE NOT NULL,    -- HB, FE, FERR, ISAT
    nombre                  VARCHAR(200) NOT NULL,
    descripcion             TEXT,
    unidad_medida_default   VARCHAR(50),
    tipo_dato               VARCHAR(20) NOT NULL DEFAULT 'numerico',   -- numerico / texto
    valor_min_referencia    DECIMAL(12,4),
    valor_max_referencia    DECIMAL(12,4),
    es_parametro_clave      BOOLEAN     NOT NULL DEFAULT FALSE,  -- HB, Fe, Ferr, ISAT
    orden_visualizacion     INT         DEFAULT 99,
    activo                  BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE parametro_clinico IS 'Catálogo normalizado de parámetros clínicos. Base del motor de prescripción';

CREATE TABLE IF NOT EXISTS alias_parametro (
    id                  UUID    PRIMARY KEY DEFAULT uuid_generate_v4(),
    parametro_clinico_id UUID   NOT NULL REFERENCES parametro_clinico(id),
    tenant_id           UUID    REFERENCES tenant(id),  -- NULL = global
    alias               TEXT    NOT NULL,
    activo              BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(LOWER(alias), tenant_id)
);
COMMENT ON TABLE alias_parametro IS 'Alias de parámetros clínicos. Permite normalizar nombres variables (HB, Hemoglobina, HEMOGLOBINA)';

-- =============================================================================
-- LOTES DE IMPORTACIÓN
-- =============================================================================

CREATE TABLE IF NOT EXISTS lote_importacion (
    id                      UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id               UUID        NOT NULL REFERENCES tenant(id),
    nombre_archivo          VARCHAR(500) NOT NULL,
    nombre_archivo_original VARCHAR(500) NOT NULL,
    storage_path            TEXT,               -- Supabase Storage path
    periodo_anio            INT         NOT NULL,
    periodo_mes             INT         NOT NULL,
    period_date             DATE        NOT NULL,   -- SIEMPRE primer día del mes
    total_filas             INT         NOT NULL DEFAULT 0,
    filas_validas           INT         NOT NULL DEFAULT 0,
    filas_error             INT         NOT NULL DEFAULT 0,
    filas_duplicadas        INT         NOT NULL DEFAULT 0,
    estado                  VARCHAR(50) NOT NULL DEFAULT 'PENDIENTE',
    -- PENDIENTE | PROCESANDO | COMPLETADO | ERROR | CANCELADO
    mensaje_error           TEXT,
    fecha_inicio_proceso    TIMESTAMPTZ,
    fecha_fin_proceso       TIMESTAMPTZ,
    activo                  BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              UUID        REFERENCES usuario(id)
);
COMMENT ON TABLE lote_importacion IS 'Batch de importación por archivo Excel. period_date siempre es el día 1 del mes';

-- =============================================================================
-- IMPORTACIÓN DETALLE (STAGING - fila cruda del Excel)
-- =============================================================================

CREATE TABLE IF NOT EXISTS importacion_detalle (
    id                  UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    lote_id             UUID        NOT NULL REFERENCES lote_importacion(id) ON DELETE CASCADE,
    tenant_id           UUID        NOT NULL REFERENCES tenant(id),
    numero_fila         INT         NOT NULL,
    -- Columnas crudas del Excel
    fecha_orden_raw     VARCHAR(50),
    plan_salud_raw      VARCHAR(300),
    tipo_atencion_raw   VARCHAR(300),
    identificacion_raw  VARCHAR(50),
    paciente_raw        VARCHAR(500),
    examen_raw          VARCHAR(500),
    parametro_raw       VARCHAR(500),
    resultado_raw       VARCHAR(300),
    unidad_medida_raw   VARCHAR(100),
    -- Procesado
    period_date         DATE,
    paciente_id         UUID        REFERENCES paciente(id),
    parametro_clinico_id UUID       REFERENCES parametro_clinico(id),
    estado              VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    -- PENDIENTE | VALIDO | ERROR | DUPLICADO | PROCESADO
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE importacion_detalle IS 'Staging: cada fila cruda del Excel antes de normalización';

CREATE TABLE IF NOT EXISTS importacion_error (
    id                      UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    lote_id                 UUID        NOT NULL REFERENCES lote_importacion(id) ON DELETE CASCADE,
    importacion_detalle_id  UUID        REFERENCES importacion_detalle(id),
    numero_fila             INT,
    campo                   VARCHAR(100),
    tipo_error              VARCHAR(50),
    -- REQUERIDO | FORMATO | DUPLICADO | PARAMETRO_DESCONOCIDO | PACIENTE_INVALIDO
    mensaje                 TEXT        NOT NULL,
    valor_recibido          TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE importacion_error IS 'Errores detallados por fila/campo durante importación';

-- =============================================================================
-- RESULTADOS DE LABORATORIO (datos normalizados y procesados)
-- =============================================================================

CREATE TABLE IF NOT EXISTS resultado_laboratorio (
    id                      UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id               UUID        NOT NULL REFERENCES tenant(id),
    paciente_id             UUID        NOT NULL REFERENCES paciente(id),
    lote_id                 UUID        NOT NULL REFERENCES lote_importacion(id),
    parametro_clinico_id    UUID        REFERENCES parametro_clinico(id),
    period_date             DATE        NOT NULL,   -- siempre primer día del mes
    periodo_anio            INT         NOT NULL,
    periodo_mes             INT         NOT NULL,
    plan_salud              VARCHAR(200),
    tipo_atencion           VARCHAR(200),
    fecha_orden             DATE,
    examen_raw              VARCHAR(300),
    parametro_raw           VARCHAR(300),
    resultado_texto         VARCHAR(300),
    valor_numerico          DECIMAL(14,4),
    unidad_medida           VARCHAR(100),
    valor_min_referencia    DECIMAL(10,4),
    valor_max_referencia    DECIMAL(10,4),
    es_patologico           BOOLEAN     DEFAULT FALSE,
    activo                  BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              UUID        REFERENCES usuario(id)
);
COMMENT ON TABLE resultado_laboratorio IS 'Resultados normalizados. period_date siempre día 1 del mes';
COMMENT ON COLUMN resultado_laboratorio.period_date IS 'Normalizado al primer día del mes. Ej: 25/03/2026 → 01/03/2026';

-- =============================================================================
-- SNAPSHOT MENSUAL (consolidado por paciente + período)
-- =============================================================================

CREATE TABLE IF NOT EXISTS snapshot_mensual (
    id                      UUID    PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id               UUID    NOT NULL REFERENCES tenant(id),
    paciente_id             UUID    NOT NULL REFERENCES paciente(id),
    period_date             DATE    NOT NULL,   -- siempre 1ro del mes
    periodo_anio            INT     NOT NULL,
    periodo_mes             INT     NOT NULL,
    lote_id                 UUID    REFERENCES lote_importacion(id),
    plan_salud              VARCHAR(200),
    tipo_atencion           VARCHAR(200),
    -- Parámetros clave desnormalizados para rendimiento de UI
    hb_valor                DECIMAL(8,4),
    hb_unidad               VARCHAR(20),
    hierro_valor            DECIMAL(8,4),
    hierro_unidad           VARCHAR(20),
    ferritina_valor         DECIMAL(8,4),
    ferritina_unidad        VARCHAR(20),
    saturacion_valor        DECIMAL(8,4),
    saturacion_unidad       VARCHAR(20),
    -- Metadatos de completitud
    tiene_datos_completos   BOOLEAN NOT NULL DEFAULT FALSE,
    es_datos_periodo_anterior BOOLEAN NOT NULL DEFAULT FALSE,
    period_date_real        DATE,   -- de dónde vienen los datos si son del mes anterior
    activo                  BOOLEAN NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ,
    UNIQUE(tenant_id, paciente_id, period_date)
);
COMMENT ON TABLE snapshot_mensual IS 'Vista consolidada mensual por paciente. Si no hay datos del mes actual, se usa el último mes vigente';

CREATE TABLE IF NOT EXISTS snapshot_mensual_detalle (
    id                      UUID    PRIMARY KEY DEFAULT uuid_generate_v4(),
    snapshot_id             UUID    NOT NULL REFERENCES snapshot_mensual(id) ON DELETE CASCADE,
    parametro_clinico_id    UUID    REFERENCES parametro_clinico(id),
    parametro_nombre        VARCHAR(200),
    valor_texto             VARCHAR(300),
    valor_numerico          DECIMAL(14,4),
    unidad_medida           VARCHAR(100),
    es_patologico           BOOLEAN DEFAULT FALSE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE snapshot_mensual_detalle IS 'Todos los parámetros del snapshot mensual. Flexible para nuevos parámetros';

-- =============================================================================
-- RANGOS A PRESCRIBIR
-- =============================================================================

CREATE TABLE IF NOT EXISTS rango_prescriba (
    id                      UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id               UUID        REFERENCES tenant(id),  -- NULL = global
    parametro_clinico_id    UUID        NOT NULL REFERENCES parametro_clinico(id),
    nombre                  VARCHAR(300) NOT NULL,
    descripcion             TEXT,
    valor_minimo            DECIMAL(12,4),
    valor_maximo            DECIMAL(12,4),
    accion                  VARCHAR(50) NOT NULL,  -- AUMENTAR | MANTENER | REDUCIR | SUSPENDER
    medicamento             VARCHAR(100),           -- EPO | HIERRO_IV | AMBOS
    dosis_sugerida          TEXT,
    ajuste_porcentaje       DECIMAL(7,2),
    observacion             TEXT,
    orden                   INT         DEFAULT 99,
    vigente_desde           DATE,
    vigente_hasta           DATE,
    activo                  BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ,
    created_by              UUID        REFERENCES usuario(id)
);
COMMENT ON TABLE rango_prescriba IS 'Rangos clínicos configurables para motor de prescripción EPO/Hierro';

-- =============================================================================
-- PRESCRIPCIÓN SUGERIDA (motor automático)
-- =============================================================================

CREATE TABLE IF NOT EXISTS prescripcion_sugerida (
    id                      UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id               UUID        NOT NULL REFERENCES tenant(id),
    paciente_id             UUID        NOT NULL REFERENCES paciente(id),
    snapshot_id             UUID        REFERENCES snapshot_mensual(id),
    period_date             DATE        NOT NULL,
    -- EPO
    epo_accion              VARCHAR(50),    -- AUMENTAR | MANTENER | REDUCIR | SUSPENDER
    epo_dosis_actual        TEXT,
    epo_dosis_sugerida      TEXT,
    epo_observacion         TEXT,
    epo_rango_id            UUID        REFERENCES rango_prescriba(id),
    -- Hierro IV
    hierro_accion           VARCHAR(50),
    hierro_dosis_actual     TEXT,
    hierro_dosis_sugerida   TEXT,
    hierro_observacion      TEXT,
    hierro_rango_id         UUID        REFERENCES rango_prescriba(id),
    -- General
    observaciones_generales TEXT,
    estado                  VARCHAR(50) NOT NULL DEFAULT 'PENDIENTE',
    -- PENDIENTE | REVISADO | APROBADO | RECHAZADO
    revisado_por            UUID        REFERENCES usuario(id),
    revisado_at             TIMESTAMPTZ,
    activo                  BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, paciente_id, period_date)
);
COMMENT ON TABLE prescripcion_sugerida IS 'Prescripción generada automáticamente por el motor clínico';

-- =============================================================================
-- PRESCRIPCIÓN FINAL (aprobada por médico)
-- =============================================================================

CREATE TABLE IF NOT EXISTS prescripcion_final (
    id                          UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id                   UUID        NOT NULL REFERENCES tenant(id),
    paciente_id                 UUID        NOT NULL REFERENCES paciente(id),
    prescripcion_sugerida_id    UUID        REFERENCES prescripcion_sugerida(id),
    medico_id                   UUID        NOT NULL REFERENCES usuario(id),
    period_date                 DATE        NOT NULL,
    -- EPO
    epo_prescrito               BOOLEAN     NOT NULL DEFAULT FALSE,
    epo_dosis                   TEXT,
    epo_frecuencia              TEXT,
    epo_observacion             TEXT,
    -- Hierro IV
    hierro_prescrito            BOOLEAN     NOT NULL DEFAULT FALSE,
    hierro_dosis                TEXT,
    hierro_frecuencia           TEXT,
    hierro_observacion          TEXT,
    -- General
    observaciones               TEXT,
    diagnostico                 TEXT,
    estado                      VARCHAR(50) NOT NULL DEFAULT 'ACTIVA',
    -- ACTIVA | SUSPENDIDA | MODIFICADA | CANCELADA
    aprobado_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    activo                      BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ
);
COMMENT ON TABLE prescripcion_final IS 'Prescripción médica final aprobada por el médico';

-- =============================================================================
-- AUDITORÍA
-- =============================================================================

CREATE TABLE IF NOT EXISTS auditoria_log (
    id              UUID        PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID        REFERENCES tenant(id),
    usuario_id      UUID        REFERENCES usuario(id),
    accion          VARCHAR(100) NOT NULL,
    -- LOGIN | LOGOUT | IMPORT | CREATE | UPDATE | DELETE | APPROVE | EXPORT
    entidad         VARCHAR(100),
    entidad_id      UUID,
    descripcion     TEXT,
    datos_anteriores JSONB,
    datos_nuevos    JSONB,
    ip_address      VARCHAR(50),
    user_agent      TEXT,
    resultado       VARCHAR(20) NOT NULL DEFAULT 'OK',  -- OK | ERROR
    mensaje_error   TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMENT ON TABLE auditoria_log IS 'Log de auditoría de todas las acciones del sistema';

-- =============================================================================
-- ÍNDICES DE RENDIMIENTO
-- =============================================================================

-- Pacientes
CREATE INDEX IF NOT EXISTS idx_paciente_tenant ON paciente(tenant_id);
CREATE INDEX IF NOT EXISTS idx_paciente_tenant_ident ON paciente(tenant_id, identificacion);
CREATE INDEX IF NOT EXISTS idx_paciente_nombre ON paciente USING GIN(nombre_completo gin_trgm_ops);

-- Resultados
CREATE INDEX IF NOT EXISTS idx_resultado_tenant_pac_period ON resultado_laboratorio(tenant_id, paciente_id, period_date);
CREATE INDEX IF NOT EXISTS idx_resultado_tenant_period ON resultado_laboratorio(tenant_id, period_date);
CREATE INDEX IF NOT EXISTS idx_resultado_param ON resultado_laboratorio(parametro_clinico_id);
CREATE INDEX IF NOT EXISTS idx_resultado_lote ON resultado_laboratorio(lote_id);

-- Snapshots
CREATE INDEX IF NOT EXISTS idx_snapshot_tenant_pac_period ON snapshot_mensual(tenant_id, paciente_id, period_date);
CREATE INDEX IF NOT EXISTS idx_snapshot_tenant_period ON snapshot_mensual(tenant_id, period_date);

-- Lotes
CREATE INDEX IF NOT EXISTS idx_lote_tenant_period ON lote_importacion(tenant_id, period_date);
CREATE INDEX IF NOT EXISTS idx_lote_estado ON lote_importacion(estado);

-- Importación detalle
CREATE INDEX IF NOT EXISTS idx_detalle_lote ON importacion_detalle(lote_id);
CREATE INDEX IF NOT EXISTS idx_detalle_estado ON importacion_detalle(estado);

-- Prescripciones
CREATE INDEX IF NOT EXISTS idx_presc_sug_tenant_pac_period ON prescripcion_sugerida(tenant_id, paciente_id, period_date);
CREATE INDEX IF NOT EXISTS idx_presc_final_tenant_pac_period ON prescripcion_final(tenant_id, paciente_id, period_date);
CREATE INDEX IF NOT EXISTS idx_presc_final_medico ON prescripcion_final(medico_id);

-- Alias de parámetros
CREATE INDEX IF NOT EXISTS idx_alias_lower ON alias_parametro(LOWER(alias));
CREATE INDEX IF NOT EXISTS idx_alias_param ON alias_parametro(parametro_clinico_id);

-- Usuarios
CREATE INDEX IF NOT EXISTS idx_usuario_email ON usuario(email);
CREATE INDEX IF NOT EXISTS idx_usuario_tenant ON usuario(tenant_id);

-- Auditoría
CREATE INDEX IF NOT EXISTS idx_auditoria_tenant ON auditoria_log(tenant_id);
CREATE INDEX IF NOT EXISTS idx_auditoria_usuario ON auditoria_log(usuario_id);
CREATE INDEX IF NOT EXISTS idx_auditoria_created ON auditoria_log(created_at DESC);

-- =============================================================================
-- VISTAS ÚTILES
-- =============================================================================

-- Vista: Último snapshot vigente por paciente
CREATE OR REPLACE VIEW v_ultimo_snapshot_vigente AS
SELECT DISTINCT ON (tenant_id, paciente_id)
    s.*,
    p.identificacion,
    p.nombre_completo,
    p.plan_salud,
    p.tipo_atencion,
    p.fecha_ingreso,
    EXTRACT(YEAR FROM AGE(CURRENT_DATE, p.fecha_ingreso))::INT * 12 +
    EXTRACT(MONTH FROM AGE(CURRENT_DATE, p.fecha_ingreso))::INT AS meses_en_dialisis
FROM snapshot_mensual s
JOIN paciente p ON p.id = s.paciente_id
WHERE s.activo = TRUE
ORDER BY s.tenant_id, s.paciente_id, s.period_date DESC;

COMMENT ON VIEW v_ultimo_snapshot_vigente IS
'Último snapshot clínico disponible por paciente (puede ser del mes anterior si el actual no tiene datos)';

-- Vista: Historial mensual de parámetros clave por paciente
CREATE OR REPLACE VIEW v_historial_mensual AS
SELECT
    s.tenant_id,
    s.paciente_id,
    p.identificacion,
    p.nombre_completo,
    s.period_date,
    s.periodo_anio,
    s.periodo_mes,
    s.hb_valor,
    s.hierro_valor,
    s.ferritina_valor,
    s.saturacion_valor,
    s.tiene_datos_completos,
    s.es_datos_periodo_anterior,
    ps.estado AS estado_prescripcion,
    ps.epo_accion,
    ps.epo_dosis_sugerida,
    ps.hierro_accion,
    ps.hierro_dosis_sugerida,
    pf.epo_dosis AS epo_dosis_final,
    pf.hierro_dosis AS hierro_dosis_final,
    pf.medico_id AS aprobado_por_medico
FROM snapshot_mensual s
JOIN paciente p ON p.id = s.paciente_id
LEFT JOIN prescripcion_sugerida ps ON ps.paciente_id = s.paciente_id
    AND ps.period_date = s.period_date AND ps.tenant_id = s.tenant_id
LEFT JOIN prescripcion_final pf ON pf.paciente_id = s.paciente_id
    AND pf.period_date = s.period_date AND pf.tenant_id = s.tenant_id
WHERE s.activo = TRUE
ORDER BY s.paciente_id, s.period_date DESC;

COMMENT ON VIEW v_historial_mensual IS 'Historial completo mensual por paciente con prescripciones';

-- Vista: Resumen de lotes de importación
CREATE OR REPLACE VIEW v_resumen_lotes AS
SELECT
    l.*,
    t.nombre AS nombre_empresa,
    u.primer_nombre || ' ' || u.primer_apellido AS cargado_por,
    ROUND(CASE WHEN l.total_filas > 0
               THEN l.filas_validas::DECIMAL / l.total_filas * 100
               ELSE 0 END, 1) AS pct_exito
FROM lote_importacion l
JOIN tenant t ON t.id = l.tenant_id
LEFT JOIN usuario u ON u.id = l.created_by
ORDER BY l.created_at DESC;
