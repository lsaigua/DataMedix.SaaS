-- ============================================================
-- 007_facturacion_periodo.sql
-- Módulo de Facturación por período (base + tarifa por paciente)
-- Ejecutar en: DEV, QA, PROD
--
-- REGLA DE NEGOCIO
--   Un paciente cuenta para la factura de un mes si tuvo ACTIVIDAD en ese
--   período: resultado de laboratorio, snapshot, prescripción o cronograma.
--   No se cuenta por estado actual del padrón — de lo contrario una baja
--   posterior reescribiría facturas ya emitidas.
--
-- POR QUÉ EL DETALLE COPIA CÉDULA Y NOMBRE
--   La depuración de datos borra físicamente los datos clínicos y puede dar de
--   baja pacientes. El detalle de facturación es un libro inmutable: conserva
--   su propia copia para poder justificar el cobro aunque el paciente ya no
--   exista en el padrón. Estas tablas NUNCA se tocan desde la depuración.
-- ============================================================

-- Cabecera: un registro por tenant y período
CREATE TABLE IF NOT EXISTS facturacion_periodo (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id               UUID NOT NULL REFERENCES tenant(id) ON DELETE CASCADE,
    periodo_anio            INTEGER NOT NULL,
    periodo_mes             INTEGER NOT NULL CHECK (periodo_mes BETWEEN 1 AND 12),
    plan_nombre             VARCHAR(100),               -- copiado del tenant al cerrar
    tarifa_base             DECIMAL(12,2) NOT NULL DEFAULT 0,   -- cargo fijo mensual
    tarifa_paciente         DECIMAL(12,2) NOT NULL DEFAULT 0,   -- cargo por paciente procesado
    pacientes_facturados    INTEGER NOT NULL DEFAULT 0,
    total                   DECIMAL(14,2) NOT NULL DEFAULT 0,
    -- ABIERTO = se recalcula en vivo | CERRADO = congelado, no cambia más
    estado                  VARCHAR(20) NOT NULL DEFAULT 'ABIERTO',
    cerrado_at              TIMESTAMPTZ,
    cerrado_por             UUID,
    observaciones           VARCHAR(500),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ,
    UNIQUE (tenant_id, periodo_anio, periodo_mes)
);

-- Detalle: una fila por paciente facturado en el período
CREATE TABLE IF NOT EXISTS facturacion_periodo_detalle (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    facturacion_periodo_id  UUID NOT NULL REFERENCES facturacion_periodo(id) ON DELETE CASCADE,
    tenant_id               UUID NOT NULL,
    -- Sin FK a paciente: el detalle debe sobrevivir a la depuración del padrón
    paciente_id             UUID NOT NULL,
    identificacion          VARCHAR(50),
    nombre_completo         VARCHAR(300),
    -- Origen de la actividad que hizo facturable al paciente
    tuvo_laboratorio        BOOLEAN NOT NULL DEFAULT FALSE,
    tuvo_snapshot           BOOLEAN NOT NULL DEFAULT FALSE,
    tuvo_prescripcion       BOOLEAN NOT NULL DEFAULT FALSE,
    tuvo_cronograma         BOOLEAN NOT NULL DEFAULT FALSE,
    -- Estado del paciente en el padrón al momento del cierre
    estado_paciente         VARCHAR(20) NOT NULL DEFAULT 'ACTIVO',  -- ACTIVO / BAJA / ELIMINADO
    tarifa_aplicada         DECIMAL(12,2) NOT NULL DEFAULT 0,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (facturacion_periodo_id, paciente_id)
);

-- Índices
CREATE INDEX IF NOT EXISTS idx_fact_periodo_tenant
    ON facturacion_periodo (tenant_id, periodo_anio DESC, periodo_mes DESC);

CREATE INDEX IF NOT EXISTS idx_fact_periodo_estado
    ON facturacion_periodo (tenant_id, estado);

CREATE INDEX IF NOT EXISTS idx_fact_detalle_periodo
    ON facturacion_periodo_detalle (facturacion_periodo_id);

CREATE INDEX IF NOT EXISTS idx_fact_detalle_paciente
    ON facturacion_periodo_detalle (tenant_id, paciente_id);

-- ============================================================
-- Tarifas del plan por tenant
-- Se guardan en el tenant para que el cierre copie el valor vigente.
-- ============================================================
ALTER TABLE tenant ADD COLUMN IF NOT EXISTS tarifa_base     DECIMAL(12,2) NOT NULL DEFAULT 0;
ALTER TABLE tenant ADD COLUMN IF NOT EXISTS tarifa_paciente DECIMAL(12,2) NOT NULL DEFAULT 0;

-- Valores por defecto según los planes comerciales publicados.
-- Solo aplica a tenants que aún no tienen tarifa configurada.
UPDATE tenant SET tarifa_base = 149.00, tarifa_paciente = 3.50
WHERE tarifa_base = 0 AND tarifa_paciente = 0
  AND UPPER(COALESCE(plan_nombre, '')) LIKE '%ESENCIAL%';

UPDATE tenant SET tarifa_base = 349.00, tarifa_paciente = 2.80
WHERE tarifa_base = 0 AND tarifa_paciente = 0
  AND UPPER(COALESCE(plan_nombre, '')) LIKE '%PROFESIONAL%';

UPDATE tenant SET tarifa_base = 349.00, tarifa_paciente = 2.80
WHERE tarifa_base = 0 AND tarifa_paciente = 0
  AND UPPER(COALESCE(plan_nombre, '')) LIKE '%ALIADO%';

-- ============================================================
-- VISTA — resumen de facturación mensual (super admin)
--
-- Reemplaza la versión anterior, que hacía COUNT(*) sobre usage_events y por
-- tanto contaba EVENTOS, no pacientes: un lote de 161 pacientes generaba un
-- solo evento y se facturaba como 1. Ahora lee el libro de facturación.
-- ============================================================
DROP VIEW IF EXISTS public.vw_facturacion_mensual;

CREATE OR REPLACE VIEW public.vw_facturacion_mensual AS
SELECT
    t.id                        AS tenant_id,
    t.nombre                    AS tenant_nombre,
    t.subdomain,
    t.plan_nombre,
    fp.periodo_anio,
    fp.periodo_mes,
    make_date(fp.periodo_anio, fp.periodo_mes, 1) AS mes,
    fp.estado,
    fp.tarifa_base,
    fp.tarifa_paciente,
    fp.pacientes_facturados,
    fp.total,
    fp.cerrado_at
FROM public.facturacion_periodo fp
JOIN public.tenant t ON t.id = fp.tenant_id
ORDER BY fp.periodo_anio DESC, fp.periodo_mes DESC, t.nombre;
