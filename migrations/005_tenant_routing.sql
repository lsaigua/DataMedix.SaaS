-- =============================================================================
-- MIGRACIÓN 005: Tenant routing + tabla usage_events para facturación
-- Ejecutar en Supabase → SQL Editor
-- Idempotente: usa IF NOT EXISTS y IF NOT EXISTS en columnas
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. Ampliar tabla tenant con columnas de routing y metadata
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE public.tenant
  ADD COLUMN IF NOT EXISTS isolation_mode        VARCHAR(20)   NOT NULL DEFAULT 'shared',
  ADD COLUMN IF NOT EXISTS connection_string_ref VARCHAR(200),
  ADD COLUMN IF NOT EXISTS plan_nombre           VARCHAR(100),
  ADD COLUMN IF NOT EXISTS ruc                   VARCHAR(20),
  ADD COLUMN IF NOT EXISTS email_contacto        VARCHAR(200),
  ADD COLUMN IF NOT EXISTS telefono              VARCHAR(20),
  ADD COLUMN IF NOT EXISTS direccion             TEXT,
  ADD COLUMN IF NOT EXISTS ciudad                VARCHAR(100),
  ADD COLUMN IF NOT EXISTS pais                  VARCHAR(100),
  ADD COLUMN IF NOT EXISTS logo_url              TEXT,
  ADD COLUMN IF NOT EXISTS codigo                VARCHAR(20),
  ADD COLUMN IF NOT EXISTS created_at            TIMESTAMPTZ   DEFAULT NOW(),
  ADD COLUMN IF NOT EXISTS updated_at            TIMESTAMPTZ,
  ADD COLUMN IF NOT EXISTS deleted_at            TIMESTAMPTZ;

-- Constraint check para isolation_mode
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'ck_tenant_isolation_mode'
  ) THEN
    ALTER TABLE public.tenant
      ADD CONSTRAINT ck_tenant_isolation_mode
        CHECK (isolation_mode IN ('shared', 'dedicated'));
  END IF;
END$$;

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. Tabla usage_events — facturación por evento (prescripción generada, etc.)
--    Siempre en la base central; tenants dedicated también escriben aquí.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.usage_events (
  id           UUID         NOT NULL DEFAULT uuid_generate_v4() PRIMARY KEY,
  tenant_id    UUID         NOT NULL REFERENCES public.tenant(id),
  event_type   VARCHAR(100) NOT NULL,
  occurred_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  metadata     JSONB
);

CREATE INDEX IF NOT EXISTS ix_ue_tenant_id    ON public.usage_events (tenant_id);
CREATE INDEX IF NOT EXISTS ix_ue_occurred_at  ON public.usage_events (occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_ue_type_tenant  ON public.usage_events (tenant_id, event_type, occurred_at DESC);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. Vista de resumen para facturación mensual (visible para super admin)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE VIEW public.vw_facturacion_mensual AS
SELECT
  t.id              AS tenant_id,
  t.nombre          AS tenant_nombre,
  t.subdomain,
  t.plan_nombre,
  DATE_TRUNC('month', ue.occurred_at) AS mes,
  ue.event_type,
  COUNT(*)          AS cantidad
FROM public.usage_events ue
JOIN public.tenant t ON t.id = ue.tenant_id
GROUP BY t.id, t.nombre, t.subdomain, t.plan_nombre,
         DATE_TRUNC('month', ue.occurred_at), ue.event_type
ORDER BY mes DESC, tenant_nombre, event_type;

-- =============================================================================
-- DOWN (comentado — ejecutar manualmente si hay rollback)
-- =============================================================================
-- DROP VIEW  IF EXISTS public.vw_facturacion_mensual;
-- DROP TABLE IF EXISTS public.usage_events;
-- ALTER TABLE public.tenant
--   DROP COLUMN IF EXISTS isolation_mode,
--   DROP COLUMN IF EXISTS connection_string_ref,
--   DROP COLUMN IF EXISTS plan_nombre,
--   DROP COLUMN IF EXISTS ruc,
--   DROP COLUMN IF EXISTS email_contacto,
--   DROP COLUMN IF EXISTS telefono,
--   DROP COLUMN IF EXISTS direccion,
--   DROP COLUMN IF EXISTS ciudad,
--   DROP COLUMN IF EXISTS pais,
--   DROP COLUMN IF EXISTS logo_url,
--   DROP COLUMN IF EXISTS codigo,
--   DROP COLUMN IF EXISTS created_at,
--   DROP COLUMN IF EXISTS updated_at,
--   DROP COLUMN IF EXISTS deleted_at;
