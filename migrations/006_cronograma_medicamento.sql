-- ============================================================
-- 006_cronograma_medicamento.sql
-- Tablas para el módulo Cronograma de Administración de Medicamentos
-- Ejecutar en: DEV, QA, PROD
-- ============================================================

-- Precios / configuración de medicamentos por tenant
CREATE TABLE IF NOT EXISTS configuracion_medicamento (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenant(id) ON DELETE CASCADE,
    medicamento     VARCHAR(100) NOT NULL,   -- 'EPO', 'HIERRO'
    precio_unitario DECIMAL(12,2) NOT NULL DEFAULT 0,
    unidad          VARCHAR(50)  NOT NULL DEFAULT 'UI',  -- 'UI', 'mg'
    observacion     VARCHAR(500),
    activo          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ,
    UNIQUE (tenant_id, medicamento)
);

-- Cabecera del cronograma mensual por paciente
CREATE TABLE IF NOT EXISTS cronograma_medicamento (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenant(id) ON DELETE CASCADE,
    paciente_id     UUID NOT NULL REFERENCES paciente(id) ON DELETE CASCADE,
    periodo_anio    INTEGER NOT NULL,
    periodo_mes     INTEGER NOT NULL CHECK (periodo_mes BETWEEN 1 AND 12),
    plan_salud      VARCHAR(200),            -- copiado del paciente al generar
    epo_ui_semana   DECIMAL(10,2),           -- fuente: prescripcion_sugerida
    hierro_mg_mes   DECIMAL(10,2),           -- fuente: prescripcion_sugerida
    observaciones   TEXT,
    estado          VARCHAR(50) NOT NULL DEFAULT 'BORRADOR',  -- BORRADOR / CONFIRMADO
    activo          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ,
    created_by      UUID,
    updated_by      UUID,
    UNIQUE (tenant_id, paciente_id, periodo_anio, periodo_mes)
);

-- Detalle: una fila por día de sesión del mes
CREATE TABLE IF NOT EXISTS cronograma_dia (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    cronograma_id           UUID NOT NULL REFERENCES cronograma_medicamento(id) ON DELETE CASCADE,
    tenant_id               UUID NOT NULL,
    fecha_sesion            DATE NOT NULL,
    epo_dosis_ui            DECIMAL(10,2),   -- dosis EPO calculada o editada manualmente
    hierro_dosis_mg         DECIMAL(10,2),   -- dosis Hierro calculada o editada manualmente
    epo_editado_manual      BOOLEAN NOT NULL DEFAULT FALSE,
    hierro_editado_manual   BOOLEAN NOT NULL DEFAULT FALSE,
    observacion             VARCHAR(500),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ,
    updated_by              UUID,
    UNIQUE (cronograma_id, fecha_sesion)
);

-- Log de auditoría de ediciones manuales
CREATE TABLE IF NOT EXISTS cronograma_auditoria (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL,
    cronograma_id   UUID NOT NULL REFERENCES cronograma_medicamento(id) ON DELETE CASCADE,
    dia_id          UUID REFERENCES cronograma_dia(id) ON DELETE SET NULL,
    usuario_id      UUID,
    accion          VARCHAR(100) NOT NULL,
    campo           VARCHAR(100),
    valor_anterior  VARCHAR(200),
    valor_nuevo     VARCHAR(200),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Índices
CREATE INDEX IF NOT EXISTS idx_cronograma_tenant_periodo
    ON cronograma_medicamento (tenant_id, periodo_anio, periodo_mes);

CREATE INDEX IF NOT EXISTS idx_cronograma_paciente
    ON cronograma_medicamento (tenant_id, paciente_id);

CREATE INDEX IF NOT EXISTS idx_cronograma_dia_cronograma
    ON cronograma_dia (cronograma_id);

CREATE INDEX IF NOT EXISTS idx_cronograma_dia_fecha
    ON cronograma_dia (tenant_id, fecha_sesion);

-- Datos iniciales: precios por defecto para tenant demo
INSERT INTO configuracion_medicamento (tenant_id, medicamento, precio_unitario, unidad)
SELECT id, 'EPO', 0.00, 'UI'
FROM tenant
WHERE NOT EXISTS (
    SELECT 1 FROM configuracion_medicamento cm
    WHERE cm.tenant_id = tenant.id AND cm.medicamento = 'EPO'
);

INSERT INTO configuracion_medicamento (tenant_id, medicamento, precio_unitario, unidad)
SELECT id, 'HIERRO', 0.00, 'mg'
FROM tenant
WHERE NOT EXISTS (
    SELECT 1 FROM configuracion_medicamento cm
    WHERE cm.tenant_id = tenant.id AND cm.medicamento = 'HIERRO'
);
