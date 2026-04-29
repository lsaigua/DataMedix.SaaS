-- F1-02: añade columna es_ignorado a importacion_error
-- Permite que el usuario marque errores de importación como revisados/ignorados.
ALTER TABLE importacion_error
    ADD COLUMN IF NOT EXISTS es_ignorado BOOLEAN NOT NULL DEFAULT FALSE;
