-- =============================================================================
-- Migration 003: Reglas clínicas configurables (Motor de Reglas Fase 1)
-- =============================================================================

-- ─── 1. Tabla reglas_clinicas ─────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS reglas_clinicas (
    id               UUID        NOT NULL DEFAULT uuid_generate_v4() PRIMARY KEY,
    codigo           VARCHAR(50) NOT NULL UNIQUE,
    nombre           VARCHAR(300) NOT NULL,
    tipo             VARCHAR(20)  NOT NULL,        -- EPO | HIERRO | ALERTA | MODIFICADOR
    prioridad        INT          NOT NULL,
    severidad        VARCHAR(20),                  -- CRITICA | ALTA | MEDIA (solo ALERTA)
    condiciones_json TEXT         NOT NULL,
    accion_json      TEXT         NOT NULL,
    version          INT          NOT NULL DEFAULT 1,
    activo           BOOLEAN      NOT NULL DEFAULT TRUE,
    tenant_id        UUID,                         -- NULL = regla global
    created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ,
    created_by       UUID
);

CREATE INDEX IF NOT EXISTS ix_reglas_clinicas_activo_prioridad
    ON reglas_clinicas (activo, prioridad)
    WHERE activo = TRUE;

CREATE INDEX IF NOT EXISTS ix_reglas_clinicas_tenant
    ON reglas_clinicas (tenant_id)
    WHERE tenant_id IS NOT NULL;

-- ─── 2. Nuevas columnas en prescripcion_sugerida ──────────────────────────────

ALTER TABLE prescripcion_sugerida
    ADD COLUMN IF NOT EXISTS regla_epo_codigo    VARCHAR(50),
    ADD COLUMN IF NOT EXISTS regla_hierro_codigo VARCHAR(50),
    ADD COLUMN IF NOT EXISTS epo_ui_semana       NUMERIC(10,2),
    ADD COLUMN IF NOT EXISTS hierro_mg_mes       NUMERIC(10,2),
    ADD COLUMN IF NOT EXISTS hierro_ganzoni_mg   NUMERIC(10,2),
    ADD COLUMN IF NOT EXISTS alertas_json        TEXT,
    ADD COLUMN IF NOT EXISTS contexto_json       TEXT;

-- ─── 3. Índice para FK blanda hacia reglas_clinicas ──────────────────────────

CREATE INDEX IF NOT EXISTS ix_prescripcion_sugerida_regla_epo
    ON prescripcion_sugerida (regla_epo_codigo)
    WHERE regla_epo_codigo IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_prescripcion_sugerida_regla_hierro
    ON prescripcion_sugerida (regla_hierro_codigo)
    WHERE regla_hierro_codigo IS NOT NULL;
