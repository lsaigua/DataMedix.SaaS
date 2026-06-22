-- =============================================================================
-- DataMedix.SaaS — Schema completo (fresh install)
-- Ejecutar en Supabase → SQL Editor en orden, en un solo bloque.
-- Idempotente: usa CREATE TABLE IF NOT EXISTS y ADD COLUMN IF NOT EXISTS.
-- Incluye columnas de migración 005 (tenant routing + usage_events).
-- =============================================================================

-- Extensión requerida para uuid_generate_v4()
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- =============================================================================
-- TIER 0 — Tablas sin dependencias externas
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.tenant (
    id                   UUID          NOT NULL DEFAULT uuid_generate_v4(),
    codigo               VARCHAR(20)   NOT NULL,
    nombre               VARCHAR(200)  NOT NULL,
    ruc                  VARCHAR(20),
    subdomain            VARCHAR(50)   NOT NULL,
    logo_url             TEXT,
    email_contacto       VARCHAR(200),
    telefono             VARCHAR(20),
    direccion            TEXT,
    ciudad               VARCHAR(100),
    pais                 VARCHAR(100)  DEFAULT 'Ecuador',
    activo               BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at           TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ,
    deleted_at           TIMESTAMPTZ,
    -- columnas migración 005 (tenant routing & billing)
    isolation_mode       VARCHAR(20)   NOT NULL DEFAULT 'shared',
    connection_string_ref VARCHAR(200),
    plan_nombre          VARCHAR(100),
    CONSTRAINT tenant_pkey          PRIMARY KEY (id),
    CONSTRAINT tenant_codigo_key    UNIQUE (codigo),
    CONSTRAINT tenant_subdomain_key UNIQUE (subdomain),
    CONSTRAINT ck_tenant_isolation_mode CHECK (isolation_mode IN ('shared', 'dedicated'))
);

CREATE TABLE IF NOT EXISTS public.rol (
    id          UUID          NOT NULL DEFAULT uuid_generate_v4(),
    nombre      VARCHAR(50)   NOT NULL,
    descripcion TEXT,
    es_global   BOOLEAN       NOT NULL DEFAULT FALSE,
    activo      BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT rol_pkey        PRIMARY KEY (id),
    CONSTRAINT rol_nombre_key  UNIQUE (nombre)
);

CREATE TABLE IF NOT EXISTS public.parametro_clinico (
    id                      UUID          NOT NULL DEFAULT uuid_generate_v4(),
    codigo                  VARCHAR(50)   NOT NULL,
    nombre                  VARCHAR(300)  NOT NULL,
    descripcion             TEXT,
    unidad_medida_default   VARCHAR(50),
    tipo_dato               VARCHAR(20)   NOT NULL DEFAULT 'numerico',
    valor_min_referencia    NUMERIC,
    valor_max_referencia    NUMERIC,
    es_parametro_clave      BOOLEAN       NOT NULL DEFAULT FALSE,
    orden_visualizacion     INTEGER       DEFAULT 99,
    activo                  BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT parametro_clinico_pkey        PRIMARY KEY (id),
    CONSTRAINT parametro_clinico_codigo_key  UNIQUE (codigo)
);

-- DataProtection keys — usa SERIAL para evitar la referencia al sequence externo
CREATE TABLE IF NOT EXISTS public.data_protection_keys (
    id            SERIAL  PRIMARY KEY,
    friendly_name TEXT,
    xml           TEXT
);

-- =============================================================================
-- TIER 1 — Dependen de tenant
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.usuario (
    id              UUID          NOT NULL DEFAULT uuid_generate_v4(),
    tenant_id       UUID,
    codigo          VARCHAR(50)   NOT NULL,
    identificacion  VARCHAR(20)   NOT NULL,
    primer_nombre   VARCHAR(150)  NOT NULL,
    segundo_nombre  VARCHAR(150),
    primer_apellido VARCHAR(150)  NOT NULL,
    segundo_apellido VARCHAR(150),
    email           VARCHAR(200)  NOT NULL,
    telefono        VARCHAR(20),
    password_hash   VARCHAR(255)  NOT NULL,
    activo          BOOLEAN       NOT NULL DEFAULT TRUE,
    ultimo_acceso   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    created_by      UUID,
    CONSTRAINT usuario_pkey        PRIMARY KEY (id),
    CONSTRAINT usuario_email_key   UNIQUE (email),
    CONSTRAINT usuario_tenant_fkey FOREIGN KEY (tenant_id) REFERENCES public.tenant(id)
);

CREATE TABLE IF NOT EXISTS public.reglas_clinicas (
    id              UUID          NOT NULL DEFAULT uuid_generate_v4(),
    codigo          VARCHAR(50)   NOT NULL,
    nombre          VARCHAR(300)  NOT NULL,
    tipo            VARCHAR(20)   NOT NULL,
    prioridad       INTEGER       NOT NULL,
    severidad       VARCHAR(20),
    condiciones_json TEXT         NOT NULL,
    accion_json     TEXT          NOT NULL,
    version         INTEGER       NOT NULL DEFAULT 1,
    activo          BOOLEAN       NOT NULL DEFAULT TRUE,
    tenant_id       UUID,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    created_by      UUID,
    CONSTRAINT reglas_clinicas_pkey       PRIMARY KEY (id),
    CONSTRAINT reglas_clinicas_codigo_key UNIQUE (codigo)
);

-- usage_events — migración 005
CREATE TABLE IF NOT EXISTS public.usage_events (
    id          UUID          NOT NULL DEFAULT uuid_generate_v4(),
    tenant_id   UUID          NOT NULL,
    event_type  VARCHAR(100)  NOT NULL,
    occurred_at TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    metadata    JSONB,
    CONSTRAINT usage_events_pkey       PRIMARY KEY (id),
    CONSTRAINT usage_events_tenant_fkey FOREIGN KEY (tenant_id) REFERENCES public.tenant(id)
);

-- =============================================================================
-- TIER 2 — Dependen de tenant + usuario o parametro_clinico
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.usuario_rol (
    id         UUID        NOT NULL DEFAULT uuid_generate_v4(),
    usuario_id UUID        NOT NULL,
    rol_id     UUID        NOT NULL,
    tenant_id  UUID,
    activo     BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT usuario_rol_pkey        PRIMARY KEY (id),
    CONSTRAINT usuario_rol_usuario_fkey FOREIGN KEY (usuario_id) REFERENCES public.usuario(id),
    CONSTRAINT usuario_rol_rol_fkey    FOREIGN KEY (rol_id)     REFERENCES public.rol(id),
    CONSTRAINT usuario_rol_tenant_fkey FOREIGN KEY (tenant_id)  REFERENCES public.tenant(id)
);

CREATE TABLE IF NOT EXISTS public.paciente (
    id               UUID          NOT NULL DEFAULT uuid_generate_v4(),
    tenant_id        UUID          NOT NULL,
    codigo           VARCHAR(50),
    identificacion   VARCHAR(20)   NOT NULL,
    primer_nombre    VARCHAR(150)  NOT NULL,
    segundo_nombre   VARCHAR(150),
    primer_apellido  VARCHAR(150)  NOT NULL,
    segundo_apellido VARCHAR(150),
    nombre_completo  TEXT          GENERATED ALWAYS AS (
        TRIM(primer_nombre
            || COALESCE(' ' || NULLIF(segundo_nombre, ''), '')
            || ' ' || primer_apellido
            || COALESCE(' ' || NULLIF(segundo_apellido, ''), ''))
    ) STORED,
    fecha_nacimiento DATE,
    genero           CHAR(1),
    email            VARCHAR(200),
    telefono         VARCHAR(20),
    plan_salud       VARCHAR(200),
    tipo_atencion    VARCHAR(200),
    fecha_ingreso    DATE,
    medico_responsable VARCHAR(300),
    observaciones    TEXT,
    activo           BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ,
    created_by       UUID,
    CONSTRAINT paciente_pkey        PRIMARY KEY (id),
    CONSTRAINT paciente_tenant_fkey  FOREIGN KEY (tenant_id) REFERENCES public.tenant(id),
    CONSTRAINT paciente_created_fkey FOREIGN KEY (created_by) REFERENCES public.usuario(id)
);

CREATE TABLE IF NOT EXISTS public.alias_parametro (
    id                  UUID        NOT NULL DEFAULT uuid_generate_v4(),
    parametro_clinico_id UUID       NOT NULL,
    tenant_id           UUID,
    alias               TEXT        NOT NULL,
    activo              BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT alias_parametro_pkey          PRIMARY KEY (id),
    CONSTRAINT alias_parametro_param_fkey    FOREIGN KEY (parametro_clinico_id) REFERENCES public.parametro_clinico(id),
    CONSTRAINT alias_parametro_tenant_fkey   FOREIGN KEY (tenant_id)            REFERENCES public.tenant(id)
);

-- =============================================================================
-- TIER 3 — Dependen de tenant + usuario + paciente
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.lote_importacion (
    id                      UUID          NOT NULL DEFAULT uuid_generate_v4(),
    tenant_id               UUID          NOT NULL,
    nombre_archivo          VARCHAR(500)  NOT NULL,
    nombre_archivo_original VARCHAR(500)  NOT NULL,
    storage_path            TEXT,
    periodo_anio            INTEGER       NOT NULL,
    periodo_mes             INTEGER       NOT NULL,
    period_date             DATE          NOT NULL,
    total_filas             INTEGER       NOT NULL DEFAULT 0,
    filas_validas           INTEGER       NOT NULL DEFAULT 0,
    filas_error             INTEGER       NOT NULL DEFAULT 0,
    filas_duplicadas        INTEGER       NOT NULL DEFAULT 0,
    estado                  VARCHAR(20)   NOT NULL DEFAULT 'PENDIENTE',
    mensaje_error           TEXT,
    fecha_inicio_proceso    TIMESTAMPTZ,
    fecha_fin_proceso       TIMESTAMPTZ,
    activo                  BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    created_by              UUID,
    CONSTRAINT lote_importacion_pkey        PRIMARY KEY (id),
    CONSTRAINT lote_importacion_tenant_fkey  FOREIGN KEY (tenant_id) REFERENCES public.tenant(id),
    CONSTRAINT lote_importacion_created_fkey FOREIGN KEY (created_by) REFERENCES public.usuario(id)
);

CREATE TABLE IF NOT EXISTS public.rango_prescriba (
    id                   UUID          NOT NULL DEFAULT uuid_generate_v4(),
    tenant_id            UUID,
    parametro_clinico_id UUID          NOT NULL,
    nombre               VARCHAR(300)  NOT NULL,
    descripcion          TEXT,
    valor_minimo         NUMERIC(12,4),
    valor_maximo         NUMERIC(12,4),
    accion               VARCHAR(50)   NOT NULL,
    medicamento          VARCHAR(100),
    dosis_sugerida       TEXT,
    ajuste_porcentaje    NUMERIC(7,2),
    observacion          TEXT,
    orden                INTEGER       DEFAULT 99,
    vigente_desde        DATE,
    vigente_hasta        DATE,
    activo               BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at           TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ,
    created_by           UUID,
    CONSTRAINT rango_prescriba_pkey        PRIMARY KEY (id),
    CONSTRAINT rango_prescriba_param_fkey   FOREIGN KEY (parametro_clinico_id) REFERENCES public.parametro_clinico(id),
    CONSTRAINT rango_prescriba_tenant_fkey  FOREIGN KEY (tenant_id)            REFERENCES public.tenant(id),
    CONSTRAINT rango_prescriba_created_fkey FOREIGN KEY (created_by)           REFERENCES public.usuario(id)
);

-- =============================================================================
-- TIER 4 — Dependen de lote_importacion y paciente
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.importacion_detalle (
    id                   UUID        NOT NULL DEFAULT uuid_generate_v4(),
    lote_id              UUID        NOT NULL,
    tenant_id            UUID        NOT NULL,
    numero_fila          INTEGER     NOT NULL,
    fecha_orden_raw      VARCHAR(100),
    plan_salud_raw       VARCHAR(200),
    tipo_atencion_raw    VARCHAR(200),
    identificacion_raw   VARCHAR(100),
    paciente_raw         VARCHAR(300),
    examen_raw           VARCHAR(300),
    parametro_raw        VARCHAR(200),
    resultado_raw        VARCHAR(300),
    unidad_medida_raw    VARCHAR(100),
    period_date          DATE,
    paciente_id          UUID,
    parametro_clinico_id UUID,
    estado               VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT importacion_detalle_pkey        PRIMARY KEY (id),
    CONSTRAINT importacion_detalle_lote_fkey   FOREIGN KEY (lote_id)              REFERENCES public.lote_importacion(id),
    CONSTRAINT importacion_detalle_tenant_fkey FOREIGN KEY (tenant_id)            REFERENCES public.tenant(id),
    CONSTRAINT importacion_detalle_pac_fkey    FOREIGN KEY (paciente_id)          REFERENCES public.paciente(id),
    CONSTRAINT importacion_detalle_param_fkey  FOREIGN KEY (parametro_clinico_id) REFERENCES public.parametro_clinico(id)
);

CREATE TABLE IF NOT EXISTS public.resultado_laboratorio (
    id                   UUID          NOT NULL DEFAULT uuid_generate_v4(),
    tenant_id            UUID          NOT NULL,
    paciente_id          UUID          NOT NULL,
    lote_id              UUID          NOT NULL,
    parametro_clinico_id UUID,
    period_date          DATE          NOT NULL,
    periodo_anio         INTEGER       NOT NULL,
    periodo_mes          INTEGER       NOT NULL,
    plan_salud           VARCHAR(200),
    tipo_atencion        VARCHAR(200),
    fecha_orden          DATE,
    examen_raw           VARCHAR(300),
    parametro_raw        VARCHAR(200),
    resultado_texto      VARCHAR(300),
    valor_numerico       NUMERIC(14,4),
    unidad_medida        VARCHAR(100),
    valor_min_referencia NUMERIC(12,4),
    valor_max_referencia NUMERIC(12,4),
    es_patologico        BOOLEAN       DEFAULT FALSE,
    activo               BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at           TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    created_by           UUID,
    CONSTRAINT resultado_laboratorio_pkey        PRIMARY KEY (id),
    CONSTRAINT resultado_laboratorio_tenant_fkey  FOREIGN KEY (tenant_id)            REFERENCES public.tenant(id),
    CONSTRAINT resultado_laboratorio_pac_fkey     FOREIGN KEY (paciente_id)          REFERENCES public.paciente(id),
    CONSTRAINT resultado_laboratorio_lote_fkey    FOREIGN KEY (lote_id)              REFERENCES public.lote_importacion(id),
    CONSTRAINT resultado_laboratorio_param_fkey   FOREIGN KEY (parametro_clinico_id) REFERENCES public.parametro_clinico(id),
    CONSTRAINT resultado_laboratorio_created_fkey FOREIGN KEY (created_by)           REFERENCES public.usuario(id)
);

CREATE TABLE IF NOT EXISTS public.snapshot_mensual (
    id                       UUID          NOT NULL DEFAULT uuid_generate_v4(),
    tenant_id                UUID          NOT NULL,
    paciente_id              UUID          NOT NULL,
    period_date              DATE          NOT NULL,
    periodo_anio             INTEGER       NOT NULL,
    periodo_mes              INTEGER       NOT NULL,
    lote_id                  UUID,
    plan_salud               VARCHAR(200),
    tipo_atencion            VARCHAR(200),
    hb_valor                 NUMERIC(8,4),
    hb_unidad                VARCHAR(20),
    hierro_valor             NUMERIC(8,4),
    hierro_unidad            VARCHAR(20),
    ferritina_valor          NUMERIC(8,4),
    ferritina_unidad         VARCHAR(20),
    saturacion_valor         NUMERIC(8,4),
    saturacion_unidad        VARCHAR(20),
    tiene_datos_completos    BOOLEAN       NOT NULL DEFAULT FALSE,
    es_datos_periodo_anterior BOOLEAN      NOT NULL DEFAULT FALSE,
    period_date_real         DATE,
    activo                   BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at               TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ,
    CONSTRAINT snapshot_mensual_pkey        PRIMARY KEY (id),
    CONSTRAINT snapshot_mensual_uq          UNIQUE (tenant_id, paciente_id, period_date),
    CONSTRAINT snapshot_mensual_tenant_fkey  FOREIGN KEY (tenant_id)  REFERENCES public.tenant(id),
    CONSTRAINT snapshot_mensual_pac_fkey     FOREIGN KEY (paciente_id) REFERENCES public.paciente(id),
    CONSTRAINT snapshot_mensual_lote_fkey    FOREIGN KEY (lote_id)     REFERENCES public.lote_importacion(id)
);

-- =============================================================================
-- TIER 5 — Dependen de snapshot_mensual / importacion_detalle
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.importacion_error (
    id                     UUID        NOT NULL DEFAULT uuid_generate_v4(),
    lote_id                UUID        NOT NULL,
    importacion_detalle_id UUID,
    numero_fila            INTEGER,
    campo                  VARCHAR(100),
    tipo_error             VARCHAR(50) NOT NULL,
    mensaje                TEXT        NOT NULL,
    valor_recibido         TEXT,
    es_ignorado            BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT importacion_error_pkey        PRIMARY KEY (id),
    CONSTRAINT importacion_error_lote_fkey   FOREIGN KEY (lote_id)                REFERENCES public.lote_importacion(id),
    CONSTRAINT importacion_error_detalle_fkey FOREIGN KEY (importacion_detalle_id) REFERENCES public.importacion_detalle(id)
);

CREATE TABLE IF NOT EXISTS public.snapshot_mensual_detalle (
    id                   UUID          NOT NULL DEFAULT uuid_generate_v4(),
    snapshot_id          UUID          NOT NULL,
    parametro_clinico_id UUID,
    parametro_nombre     VARCHAR(200),
    valor_texto          VARCHAR(300),
    valor_numerico       NUMERIC(14,4),
    unidad_medida        VARCHAR(100),
    es_patologico        BOOLEAN       DEFAULT FALSE,
    created_at           TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT snapshot_mensual_detalle_pkey        PRIMARY KEY (id),
    CONSTRAINT snapshot_mensual_detalle_snap_fkey   FOREIGN KEY (snapshot_id)          REFERENCES public.snapshot_mensual(id) ON DELETE CASCADE,
    CONSTRAINT snapshot_mensual_detalle_param_fkey  FOREIGN KEY (parametro_clinico_id) REFERENCES public.parametro_clinico(id)
);

CREATE TABLE IF NOT EXISTS public.prescripcion_sugerida (
    id                    UUID          NOT NULL DEFAULT uuid_generate_v4(),
    tenant_id             UUID          NOT NULL,
    paciente_id           UUID          NOT NULL,
    snapshot_id           UUID,
    period_date           DATE          NOT NULL,
    epo_accion            VARCHAR(50),
    epo_dosis_sugerida    TEXT,
    epo_observacion       TEXT,
    epo_rango_id          UUID,
    hierro_accion         VARCHAR(50),
    hierro_dosis_sugerida TEXT,
    hierro_observacion    TEXT,
    hierro_rango_id       UUID,
    regla_epo_codigo      VARCHAR(50),
    regla_hierro_codigo   VARCHAR(50),
    epo_ui_semana         NUMERIC(10,2),
    hierro_mg_mes         NUMERIC(10,2),
    hierro_ganzoni_mg     NUMERIC(10,2),
    alertas_json          TEXT,
    contexto_json         TEXT,
    observaciones_generales TEXT,
    estado                VARCHAR(50)   NOT NULL DEFAULT 'PENDIENTE',
    revisado_por          UUID,
    revisado_at           TIMESTAMPTZ,
    activo                BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT prescripcion_sugerida_pkey           PRIMARY KEY (id),
    CONSTRAINT prescripcion_sugerida_uq             UNIQUE (tenant_id, paciente_id, period_date),
    CONSTRAINT prescripcion_sugerida_tenant_fkey     FOREIGN KEY (tenant_id)     REFERENCES public.tenant(id),
    CONSTRAINT prescripcion_sugerida_pac_fkey        FOREIGN KEY (paciente_id)   REFERENCES public.paciente(id),
    CONSTRAINT prescripcion_sugerida_snap_fkey       FOREIGN KEY (snapshot_id)   REFERENCES public.snapshot_mensual(id),
    CONSTRAINT prescripcion_sugerida_epo_rango_fkey  FOREIGN KEY (epo_rango_id)  REFERENCES public.rango_prescriba(id),
    CONSTRAINT prescripcion_sugerida_hie_rango_fkey  FOREIGN KEY (hierro_rango_id) REFERENCES public.rango_prescriba(id),
    CONSTRAINT prescripcion_sugerida_revisado_fkey   FOREIGN KEY (revisado_por)  REFERENCES public.usuario(id)
);

-- =============================================================================
-- TIER 6 — Dependen de prescripcion_sugerida
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.prescripcion_final (
    id                      UUID          NOT NULL DEFAULT uuid_generate_v4(),
    tenant_id               UUID          NOT NULL,
    paciente_id             UUID          NOT NULL,
    prescripcion_sugerida_id UUID,
    medico_id               UUID          NOT NULL,
    period_date             DATE          NOT NULL,
    epo_prescrito           BOOLEAN       NOT NULL DEFAULT FALSE,
    epo_dosis               TEXT,
    epo_frecuencia          TEXT,
    epo_observacion         TEXT,
    hierro_prescrito        BOOLEAN       NOT NULL DEFAULT FALSE,
    hierro_dosis            TEXT,
    hierro_frecuencia       TEXT,
    hierro_observacion      TEXT,
    observaciones           TEXT,
    diagnostico             TEXT,
    estado                  VARCHAR(50)   NOT NULL DEFAULT 'ACTIVA',
    aprobado_at             TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    activo                  BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ,
    CONSTRAINT prescripcion_final_pkey          PRIMARY KEY (id),
    CONSTRAINT prescripcion_final_tenant_fkey    FOREIGN KEY (tenant_id)               REFERENCES public.tenant(id),
    CONSTRAINT prescripcion_final_pac_fkey       FOREIGN KEY (paciente_id)             REFERENCES public.paciente(id),
    CONSTRAINT prescripcion_final_sug_fkey       FOREIGN KEY (prescripcion_sugerida_id) REFERENCES public.prescripcion_sugerida(id),
    CONSTRAINT prescripcion_final_medico_fkey    FOREIGN KEY (medico_id)               REFERENCES public.usuario(id)
);

CREATE TABLE IF NOT EXISTS public.auditoria_log (
    id              UUID          NOT NULL DEFAULT uuid_generate_v4(),
    tenant_id       UUID,
    usuario_id      UUID,
    accion          VARCHAR(100)  NOT NULL,
    entidad         VARCHAR(100),
    entidad_id      UUID,
    descripcion     TEXT,
    datos_anteriores JSONB,
    datos_nuevos    JSONB,
    ip_address      VARCHAR(50),
    user_agent      TEXT,
    resultado       VARCHAR(20)   NOT NULL DEFAULT 'OK',
    mensaje_error   TEXT,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT auditoria_log_pkey        PRIMARY KEY (id),
    CONSTRAINT auditoria_log_tenant_fkey  FOREIGN KEY (tenant_id)  REFERENCES public.tenant(id),
    CONSTRAINT auditoria_log_usuario_fkey FOREIGN KEY (usuario_id) REFERENCES public.usuario(id)
);

-- =============================================================================
-- ÍNDICES — rendimiento en queries frecuentes
-- =============================================================================

-- usuario
CREATE INDEX IF NOT EXISTS ix_usuario_tenant_id   ON public.usuario (tenant_id);
CREATE INDEX IF NOT EXISTS ix_usuario_email        ON public.usuario (email);

-- usuario_rol
CREATE INDEX IF NOT EXISTS ix_usuario_rol_usuario  ON public.usuario_rol (usuario_id);
CREATE INDEX IF NOT EXISTS ix_usuario_rol_tenant   ON public.usuario_rol (tenant_id);

-- paciente
CREATE INDEX IF NOT EXISTS ix_paciente_tenant_id   ON public.paciente (tenant_id);
CREATE INDEX IF NOT EXISTS ix_paciente_identificacion ON public.paciente (tenant_id, identificacion);

-- alias_parametro
CREATE INDEX IF NOT EXISTS ix_alias_param_tenant   ON public.alias_parametro (tenant_id);

-- lote_importacion
CREATE INDEX IF NOT EXISTS ix_lote_tenant_id       ON public.lote_importacion (tenant_id);
CREATE INDEX IF NOT EXISTS ix_lote_period_date     ON public.lote_importacion (tenant_id, period_date);

-- importacion_detalle
CREATE INDEX IF NOT EXISTS ix_imp_det_lote         ON public.importacion_detalle (lote_id);
CREATE INDEX IF NOT EXISTS ix_imp_det_tenant       ON public.importacion_detalle (tenant_id);

-- importacion_error
CREATE INDEX IF NOT EXISTS ix_imp_err_lote         ON public.importacion_error (lote_id);

-- resultado_laboratorio
CREATE INDEX IF NOT EXISTS ix_rl_tenant_id         ON public.resultado_laboratorio (tenant_id);
CREATE INDEX IF NOT EXISTS ix_rl_paciente          ON public.resultado_laboratorio (tenant_id, paciente_id);
CREATE INDEX IF NOT EXISTS ix_rl_period            ON public.resultado_laboratorio (tenant_id, period_date);

-- snapshot_mensual
CREATE INDEX IF NOT EXISTS ix_snap_tenant_pac      ON public.snapshot_mensual (tenant_id, paciente_id);
CREATE INDEX IF NOT EXISTS ix_snap_period          ON public.snapshot_mensual (tenant_id, period_date);

-- snapshot_mensual_detalle
CREATE INDEX IF NOT EXISTS ix_snap_det_snapshot    ON public.snapshot_mensual_detalle (snapshot_id);

-- rango_prescriba
CREATE INDEX IF NOT EXISTS ix_rango_tenant         ON public.rango_prescriba (tenant_id);

-- prescripcion_sugerida
CREATE INDEX IF NOT EXISTS ix_presc_sug_tenant     ON public.prescripcion_sugerida (tenant_id);
CREATE INDEX IF NOT EXISTS ix_presc_sug_paciente   ON public.prescripcion_sugerida (tenant_id, paciente_id);

-- prescripcion_final
CREATE INDEX IF NOT EXISTS ix_presc_fin_tenant     ON public.prescripcion_final (tenant_id);

-- reglas_clinicas
CREATE INDEX IF NOT EXISTS ix_reglas_activo_prior  ON public.reglas_clinicas (activo, prioridad);

-- auditoria_log
CREATE INDEX IF NOT EXISTS ix_audit_tenant         ON public.auditoria_log (tenant_id);
CREATE INDEX IF NOT EXISTS ix_audit_created        ON public.auditoria_log (created_at);

-- usage_events
CREATE INDEX IF NOT EXISTS ix_ue_tenant_id         ON public.usage_events (tenant_id);
CREATE INDEX IF NOT EXISTS ix_ue_occurred_at       ON public.usage_events (occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_ue_type_tenant       ON public.usage_events (tenant_id, event_type, occurred_at DESC);

-- =============================================================================
-- VISTA — resumen facturación mensual (visible para super admin)
-- =============================================================================

CREATE OR REPLACE VIEW public.vw_facturacion_mensual AS
SELECT
    t.id                                          AS tenant_id,
    t.nombre                                      AS tenant_nombre,
    t.subdomain,
    t.plan_nombre,
    DATE_TRUNC('month', ue.occurred_at)           AS mes,
    ue.event_type,
    COUNT(*)                                      AS cantidad
FROM public.usage_events ue
JOIN public.tenant t ON t.id = ue.tenant_id
GROUP BY t.id, t.nombre, t.subdomain, t.plan_nombre,
         DATE_TRUNC('month', ue.occurred_at), ue.event_type
ORDER BY mes DESC, tenant_nombre, event_type;

-- =============================================================================
-- VERIFICACIÓN FINAL
-- =============================================================================
SELECT
    table_name,
    (SELECT COUNT(*) FROM information_schema.columns c
     WHERE c.table_name = t.table_name AND c.table_schema = 'public') AS columnas
FROM information_schema.tables t
WHERE table_schema = 'public'
  AND table_type = 'BASE TABLE'
ORDER BY table_name;
