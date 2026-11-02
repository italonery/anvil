-- =============================================================================
-- Anvil.Persistence.Postgres - Database-Managed Governance Conventions
-- Responsible for managing 'created' and 'updated' columns directly in PostgreSQL
-- =============================================================================

-- 1. Trigger Function: Preserves 'created' immutability and sets 'updated' to real-time clock_timestamp() on BEFORE UPDATE
CREATE OR REPLACE FUNCTION public.fn_set_updated_timestamp()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.created := OLD.created;
    NEW.updated := clock_timestamp();
    RETURN NEW;
END;
$$;

-- 2. Helper Procedure: Attaches the 'updated' trigger to any target table
-- Usage: CALL public.sp_attach_updated_trigger('public', 'your_table_name');
CREATE OR REPLACE PROCEDURE public.sp_attach_updated_trigger(
    p_schema_name TEXT,
    p_table_name TEXT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_trigger_name TEXT := format('trg_%s_set_updated', p_table_name);
BEGIN
    EXECUTE format(
        'DROP TRIGGER IF EXISTS %I ON %I.%I;',
        v_trigger_name,
        p_schema_name,
        p_table_name
    );

    EXECUTE format(
        'CREATE TRIGGER %I BEFORE UPDATE ON %I.%I FOR EACH ROW WHEN (OLD.* IS DISTINCT FROM NEW.*) EXECUTE FUNCTION public.fn_set_updated_timestamp();',
        v_trigger_name,
        p_schema_name,
        p_table_name
    );
END;
$$;

-- =============================================================================
-- Reference Table Template:
--
-- CREATE TABLE public.example_entities (
--     id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
--     created TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
--     updated TIMESTAMPTZ NULL
-- );
--
-- CALL public.sp_attach_updated_trigger('public', 'example_entities');
-- =============================================================================
