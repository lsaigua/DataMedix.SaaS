-- =============================================================================
-- MIGRACIÓN 001: Alinear esquema existente con la aplicación DataMedix
-- Ejecutar en Supabase → SQL Editor
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. TABLA usuario: renombrar columna PasswordHash y agregar columnas faltantes
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE public.usuario
  RENAME COLUMN "PasswordHash" TO password_hash;

ALTER TABLE public.usuario
  ADD COLUMN IF NOT EXISTS tenant_id      uuid,
  ADD COLUMN IF NOT EXISTS ultimo_acceso  timestamp without time zone,
  ADD COLUMN IF NOT EXISTS updated_at     timestamp without time zone,
  ADD COLUMN IF NOT EXISTS created_by     uuid;

-- Poblar tenant_id en usuario desde usuarioempresa (empresa por defecto)
UPDATE public.usuario u
SET tenant_id = ue.idempresa
FROM public.usuarioempresa ue
WHERE ue.idusuario = u.id
  AND ue.isdefault = true
  AND ue.activo = true;

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. TABLA paciente: agregar columnas faltantes para la aplicación
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE public.paciente
  ADD COLUMN IF NOT EXISTS tenant_id          uuid,
  ADD COLUMN IF NOT EXISTS plan_salud         character varying(200),
  ADD COLUMN IF NOT EXISTS tipo_atencion      character varying(200),
  ADD COLUMN IF NOT EXISTS fecha_ingreso      date,
  ADD COLUMN IF NOT EXISTS medico_responsable character varying(300),
  ADD COLUMN IF NOT EXISTS observaciones      text,
  ADD COLUMN IF NOT EXISTS updated_at         timestamp without time zone,
  ADD COLUMN IF NOT EXISTS created_by         uuid;

-- Poblar tenant_id en paciente desde pacienteempresa
UPDATE public.paciente p
SET tenant_id = pe.idempresa
FROM public.pacienteempresa pe
WHERE pe.idpaciente = p.id;

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. TABLA rol: agregar columna es_global si se necesita
-- ─────────────────────────────────────────────────────────────────────────────
-- (Actualmente ignorada en C#; descomentar si se activa en el futuro)
-- ALTER TABLE public.rol ADD COLUMN IF NOT EXISTS es_global boolean DEFAULT false;

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. TABLA parametro_clinico (nueva)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.parametro_clinico (
  id                    uuid NOT NULL DEFAULT uuid_generate_v4(),
  codigo                character varying(50) NOT NULL,
  nombre                character varying(200) NOT NULL,
  descripcion           text,
  unidad_medida_default character varying(50),
  tipo_dato             character varying(20) DEFAULT 'numerico',
  valor_min_referencia  decimal(12,4),
  valor_max_referencia  decimal(12,4),
  es_parametro_clave    boolean DEFAULT false,
  orden_visualizacion   integer DEFAULT 99,
  activo                boolean DEFAULT true,
  created_at            timestamp with time zone DEFAULT now(),
  CONSTRAINT parametro_clinico_pkey PRIMARY KEY (id),
  CONSTRAINT parametro_clinico_codigo_key UNIQUE (codigo)
);

-- Parámetros clínicos base para diálisis
INSERT INTO public.parametro_clinico
  (codigo, nombre, unidad_medida_default, es_parametro_clave, orden_visualizacion)
VALUES
  ('HB',   'Hemoglobina',                        'g/dL',    true,  1),
  ('HCT',  'Hematocrito',                        '%',       false, 2),
  ('FE',   'Hierro Sérico',                      'μg/dL',  true,  3),
  ('FERR', 'Ferritina',                          'ng/mL',  true,  4),
  ('ISAT', 'Saturación de Transferrina',         '%',       true,  5),
  ('TIBC', 'Cap. Total Fijación Hierro (TIBC)', 'μg/dL',  false, 6),
  ('CREAT','Creatinina',                         'mg/dL',  false, 7),
  ('BUN',  'Nitrógeno Ureico (BUN)',             'mg/dL',  false, 8),
  ('ALB',  'Albúmina',                          'g/dL',   false, 9),
  ('PTH',  'Hormona Paratiroidea (PTH)',         'pg/mL',  false, 10),
  ('PCR',  'Proteína C Reactiva (PCR)',          'mg/L',   false, 11),
  ('PLAQ', 'Plaquetas',                          'x10³/μL',false, 12)
ON CONFLICT (codigo) DO NOTHING;

-- ─────────────────────────────────────────────────────────────────────────────
-- 5. TABLA alias_parametro (nueva)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.alias_parametro (
  id                  uuid NOT NULL DEFAULT uuid_generate_v4(),
  parametro_clinico_id uuid NOT NULL,
  tenant_id           uuid,
  alias               character varying NOT NULL,
  activo              boolean DEFAULT true,
  created_at          timestamp with time zone DEFAULT now(),
  CONSTRAINT alias_parametro_pkey PRIMARY KEY (id),
  CONSTRAINT fk_alias_parametro FOREIGN KEY (parametro_clinico_id)
    REFERENCES public.parametro_clinico(id)
);

-- Aliases comunes de laboratorio
INSERT INTO public.alias_parametro (parametro_clinico_id, alias)
SELECT pc.id, a.alias
FROM public.parametro_clinico pc
JOIN (VALUES
  ('HB',   'HEMOGLOBINA'),
  ('HB',   'HGB'),
  ('HB',   'Hb'),
  ('HB',   'hemoglobina'),
  ('FE',   'HIERRO'),
  ('FE',   'Hierro'),
  ('FE',   'Fe sérico'),
  ('FERR', 'FERRITINA'),
  ('FERR', 'Ferritina'),
  ('ISAT', 'SATURACION'),
  ('ISAT', 'Saturación'),
  ('ISAT', 'ISat'),
  ('CREAT','CREATININA'),
  ('CREAT','Creatinina'),
  ('BUN',  'UREA'),
  ('BUN',  'Urea'),
  ('ALB',  'ALBUMINA'),
  ('ALB',  'Albúmina'),
  ('PTH',  'PARATOHORMONA'),
  ('PCR',  'PROTEINA C REACTIVA')
) AS a(codigo, alias) ON pc.codigo = a.codigo
WHERE NOT EXISTS (
  SELECT 1 FROM public.alias_parametro ap2
  WHERE ap2.parametro_clinico_id = pc.id AND ap2.alias = a.alias
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 6. TABLA lote_importacion (nueva)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.lote_importacion (
  id                      uuid NOT NULL DEFAULT uuid_generate_v4(),
  tenant_id               uuid NOT NULL,
  nombre_archivo          character varying(500) NOT NULL,
  nombre_archivo_original character varying(500) NOT NULL,
  storage_path            text,
  periodo_anio            integer NOT NULL,
  periodo_mes             integer NOT NULL,
  period_date             date NOT NULL,
  total_filas             integer DEFAULT 0,
  filas_validas           integer DEFAULT 0,
  filas_error             integer DEFAULT 0,
  filas_duplicadas        integer DEFAULT 0,
  estado                  character varying(50) DEFAULT 'PENDIENTE',
  mensaje_error           text,
  fecha_inicio_proceso    timestamp with time zone,
  fecha_fin_proceso       timestamp with time zone,
  activo                  boolean DEFAULT true,
  created_at              timestamp with time zone DEFAULT now(),
  created_by              uuid,
  CONSTRAINT lote_importacion_pkey PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS idx_lote_importacion_tenant_period
  ON public.lote_importacion (tenant_id, period_date);

-- ─────────────────────────────────────────────────────────────────────────────
-- 7. TABLA importacion_detalle (staging / nueva)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.importacion_detalle (
  id                   uuid NOT NULL DEFAULT uuid_generate_v4(),
  lote_id              uuid NOT NULL,
  tenant_id            uuid NOT NULL,
  numero_fila          integer NOT NULL,
  fecha_orden_raw      character varying(50),
  plan_salud_raw       character varying(300),
  tipo_atencion_raw    character varying(300),
  identificacion_raw   character varying(50),
  paciente_raw         character varying(500),
  examen_raw           character varying(500),
  parametro_raw        character varying(500),
  resultado_raw        character varying(300),
  unidad_medida_raw    character varying(100),
  period_date          date,
  paciente_id          uuid,
  parametro_clinico_id uuid,
  estado               character varying(20) DEFAULT 'PENDIENTE',
  created_at           timestamp with time zone DEFAULT now(),
  CONSTRAINT importacion_detalle_pkey PRIMARY KEY (id),
  CONSTRAINT fk_impdet_lote FOREIGN KEY (lote_id)
    REFERENCES public.lote_importacion(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_importacion_detalle_lote
  ON public.importacion_detalle (lote_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 8. TABLA importacion_error (nueva)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.importacion_error (
  id                    uuid NOT NULL DEFAULT uuid_generate_v4(),
  lote_id               uuid NOT NULL,
  importacion_detalle_id uuid,
  numero_fila           integer,
  campo                 character varying(100),
  tipo_error            character varying(50),
  mensaje               text NOT NULL,
  valor_recibido        text,
  created_at            timestamp with time zone DEFAULT now(),
  CONSTRAINT importacion_error_pkey PRIMARY KEY (id),
  CONSTRAINT fk_imperr_lote FOREIGN KEY (lote_id)
    REFERENCES public.lote_importacion(id) ON DELETE CASCADE
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 9. TABLA resultado_laboratorio (nueva — esquema limpio)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.resultado_laboratorio (
  id                   uuid NOT NULL DEFAULT uuid_generate_v4(),
  tenant_id            uuid NOT NULL,
  paciente_id          uuid NOT NULL,
  lote_id              uuid NOT NULL,
  parametro_clinico_id uuid,
  period_date          date NOT NULL,
  periodo_anio         integer NOT NULL,
  periodo_mes          integer NOT NULL,
  plan_salud           character varying(200),
  tipo_atencion        character varying(200),
  fecha_orden          date,
  examen_raw           character varying(300),
  parametro_raw        character varying(300),
  resultado_texto      character varying(300) NOT NULL,
  valor_numerico       decimal(14,4),
  unidad_medida        character varying(100),
  valor_min_referencia decimal(10,4),
  valor_max_referencia decimal(10,4),
  es_patologico        boolean DEFAULT false,
  activo               boolean DEFAULT true,
  created_at           timestamp with time zone DEFAULT now(),
  created_by           uuid,
  CONSTRAINT resultado_laboratorio_pkey PRIMARY KEY (id),
  CONSTRAINT fk_resultado_paciente FOREIGN KEY (paciente_id)
    REFERENCES public.paciente(id),
  CONSTRAINT fk_resultado_lote FOREIGN KEY (lote_id)
    REFERENCES public.lote_importacion(id)
);
CREATE INDEX IF NOT EXISTS idx_resultado_tenant_paciente_period
  ON public.resultado_laboratorio (tenant_id, paciente_id, period_date);
CREATE INDEX IF NOT EXISTS idx_resultado_tenant_period
  ON public.resultado_laboratorio (tenant_id, period_date);

-- ─────────────────────────────────────────────────────────────────────────────
-- 10. TABLA snapshot_mensual (nueva)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.snapshot_mensual (
  id                      uuid NOT NULL DEFAULT uuid_generate_v4(),
  tenant_id               uuid NOT NULL,
  paciente_id             uuid NOT NULL,
  period_date             date NOT NULL,
  periodo_anio            integer NOT NULL,
  periodo_mes             integer NOT NULL,
  lote_id                 uuid,
  plan_salud              character varying(200),
  tipo_atencion           character varying(200),
  hb_valor                decimal(8,4),
  hb_unidad               character varying(20),
  hierro_valor            decimal(8,4),
  hierro_unidad           character varying(20),
  ferritina_valor         decimal(8,4),
  ferritina_unidad        character varying(20),
  saturacion_valor        decimal(8,4),
  saturacion_unidad       character varying(20),
  tiene_datos_completos   boolean DEFAULT false,
  es_datos_periodo_anterior boolean DEFAULT false,
  period_date_real        date,
  activo                  boolean DEFAULT true,
  created_at              timestamp with time zone DEFAULT now(),
  updated_at              timestamp with time zone,
  CONSTRAINT snapshot_mensual_pkey PRIMARY KEY (id),
  CONSTRAINT snapshot_mensual_unique UNIQUE (tenant_id, paciente_id, period_date)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 11. TABLA snapshot_mensual_detalle (nueva)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.snapshot_mensual_detalle (
  id                   uuid NOT NULL DEFAULT uuid_generate_v4(),
  snapshot_id          uuid NOT NULL,
  parametro_clinico_id uuid,
  parametro_nombre     character varying(200),
  valor_texto          character varying(300),
  valor_numerico       decimal(14,4),
  unidad_medida        character varying(100),
  es_patologico        boolean DEFAULT false,
  created_at           timestamp with time zone DEFAULT now(),
  CONSTRAINT snapshot_mensual_detalle_pkey PRIMARY KEY (id),
  CONSTRAINT fk_snapdet_snapshot FOREIGN KEY (snapshot_id)
    REFERENCES public.snapshot_mensual(id) ON DELETE CASCADE
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 12. TABLA rango_prescriba (nueva)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.rango_prescriba (
  id                   uuid NOT NULL DEFAULT uuid_generate_v4(),
  tenant_id            uuid,
  parametro_clinico_id uuid NOT NULL,
  nombre               character varying(300) NOT NULL,
  descripcion          text,
  valor_minimo         decimal(12,4),
  valor_maximo         decimal(12,4),
  accion               character varying(50) NOT NULL,
  medicamento          character varying(100),
  dosis_sugerida       text,
  ajuste_porcentaje    decimal(7,2),
  observacion          text,
  orden                integer DEFAULT 99,
  vigente_desde        date,
  vigente_hasta        date,
  activo               boolean DEFAULT true,
  created_at           timestamp with time zone DEFAULT now(),
  updated_at           timestamp with time zone,
  created_by           uuid,
  CONSTRAINT rango_prescriba_pkey PRIMARY KEY (id),
  CONSTRAINT fk_rango_parametro FOREIGN KEY (parametro_clinico_id)
    REFERENCES public.parametro_clinico(id)
);

-- Rangos base (ajustar según protocolo clínico de la empresa)
INSERT INTO public.rango_prescriba
  (parametro_clinico_id, nombre, valor_minimo, valor_maximo, accion, medicamento, dosis_sugerida, orden)
SELECT pc.id, r.nombre, r.vmin, r.vmax, r.accion, r.med, r.dosis, r.orden
FROM public.parametro_clinico pc
JOIN (VALUES
  ('HB', 'HB < 10 g/dL — Iniciar/Aumentar EPO', NULL, 10.0,   'INICIAR',   'Eritropoyetina', 'Según protocolo', 1),
  ('HB', 'HB 10-11 g/dL — Mantener EPO',         10.0, 11.0,  'MANTENER',  'Eritropoyetina', 'Dosis actual',    2),
  ('HB', 'HB 11-12 g/dL — Objetivo alcanzado',   11.0, 12.0,  'MANTENER',  'Eritropoyetina', 'Dosis actual',    3),
  ('HB', 'HB > 12 g/dL — Reducir/Suspender EPO', 12.0, NULL,  'REDUCIR',   'Eritropoyetina', 'Reducir 25%',     4),
  ('FE', 'Hierro < 40 μg/dL — Déficit severo',   NULL, 40.0,  'INICIAR',   'Hierro IV',      'Según protocolo', 1),
  ('FE', 'Hierro 40-60 μg/dL — Déficit leve',    40.0, 60.0,  'INICIAR',   'Hierro IV',      'Dosis baja',      2),
  ('FE', 'Hierro > 60 μg/dL — Normal',           60.0, NULL,  'MANTENER',  NULL,             NULL,              3),
  ('FERR','Ferritina < 200 ng/mL — Iniciar hierro',NULL,200.0,'INICIAR',   'Hierro IV',      'Según protocolo', 1),
  ('FERR','Ferritina 200-500 ng/mL — Mantener',  200.0,500.0, 'MANTENER',  NULL,             NULL,              2),
  ('FERR','Ferritina > 500 ng/mL — Suspender',   500.0,NULL,  'SUSPENDER', 'Hierro IV',      'Suspender',       3),
  ('ISAT','ISAT < 20% — Iniciar hierro IV',       NULL, 20.0,  'INICIAR',   'Hierro IV',      'Según protocolo', 1),
  ('ISAT','ISAT >= 20% — Saturación adecuada',    20.0, NULL,  'MANTENER',  NULL,             NULL,              2)
) AS r(codigo, nombre, vmin, vmax, accion, med, dosis, orden) ON pc.codigo = r.codigo
WHERE NOT EXISTS (
  SELECT 1 FROM public.rango_prescriba rp2
  WHERE rp2.parametro_clinico_id = pc.id AND rp2.nombre = r.nombre
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 13. TABLA prescripcion_sugerida (nueva — diferente de prescripcionsugerida antigua)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.prescripcion_sugerida (
  id                    uuid NOT NULL DEFAULT uuid_generate_v4(),
  tenant_id             uuid NOT NULL,
  paciente_id           uuid NOT NULL,
  snapshot_id           uuid,
  period_date           date NOT NULL,
  epo_accion            character varying(50),
  epo_dosis_sugerida    text,
  epo_observacion       text,
  epo_rango_id          uuid,
  hierro_accion         character varying(50),
  hierro_dosis_sugerida text,
  hierro_observacion    text,
  hierro_rango_id       uuid,
  observaciones_generales text,
  estado                character varying(50) DEFAULT 'PENDIENTE',
  revisado_por          uuid,
  revisado_at           timestamp with time zone,
  activo                boolean DEFAULT true,
  created_at            timestamp with time zone DEFAULT now(),
  CONSTRAINT prescripcion_sugerida_pkey PRIMARY KEY (id),
  CONSTRAINT prescripcion_sugerida_unique UNIQUE (tenant_id, paciente_id, period_date)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 14. TABLA prescripcion_final (nueva)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.prescripcion_final (
  id                     uuid NOT NULL DEFAULT uuid_generate_v4(),
  tenant_id              uuid NOT NULL,
  paciente_id            uuid NOT NULL,
  prescripcion_sugerida_id uuid,
  medico_id              uuid NOT NULL,
  period_date            date NOT NULL,
  epo_prescrito          boolean DEFAULT false,
  epo_dosis              text,
  epo_frecuencia         character varying,
  epo_observacion        text,
  hierro_prescrito       boolean DEFAULT false,
  hierro_dosis           text,
  hierro_frecuencia      character varying,
  hierro_observacion     text,
  observaciones          text,
  diagnostico            text,
  estado                 character varying(50) DEFAULT 'ACTIVA',
  aprobado_at            timestamp with time zone DEFAULT now(),
  activo                 boolean DEFAULT true,
  created_at             timestamp with time zone DEFAULT now(),
  updated_at             timestamp with time zone,
  CONSTRAINT prescripcion_final_pkey PRIMARY KEY (id),
  CONSTRAINT fk_prescfinal_paciente FOREIGN KEY (paciente_id)
    REFERENCES public.paciente(id),
  CONSTRAINT fk_prescfinal_medico FOREIGN KEY (medico_id)
    REFERENCES public.usuario(id)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 15. TABLA auditoria_log (nueva — diferente de auditlog antigua)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.auditoria_log (
  id               uuid NOT NULL DEFAULT uuid_generate_v4(),
  tenant_id        uuid,
  usuario_id       uuid,
  accion           character varying(100) NOT NULL,
  entidad          character varying(100),
  entidad_id       uuid,
  descripcion      text,
  datos_anteriores jsonb,
  datos_nuevos     jsonb,
  ip_address       character varying(50),
  user_agent       text,
  resultado        character varying(20) DEFAULT 'OK',
  mensaje_error    text,
  created_at       timestamp with time zone DEFAULT now(),
  CONSTRAINT auditoria_log_pkey PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS idx_auditoria_tenant   ON public.auditoria_log (tenant_id);
CREATE INDEX IF NOT EXISTS idx_auditoria_createdat ON public.auditoria_log (created_at);
