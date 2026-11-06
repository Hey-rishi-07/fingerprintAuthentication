-- =====================================================================
--  MGP  -  Describe legacy schema (STRUCTURE ONLY, no pass/item data)
--  Oracle 11g, run in SQL*Plus.
--
--  HOW TO RUN
--    1. Connect as the schema user the old application used
--       (the user behind the old "admin" DSN), e.g.
--          sqlplus admin/<password>@<tns>
--    2. Run:   @00_describe_legacy_schema.sql
--    3. Output file:  mgp_schema_desc.txt  (in the current folder)
--
--  WHAT IT PRINTS
--    1. Database version
--    2. List of all MGP objects + the HR tables/views the app reads
--    3. For every table/view: columns (like DESC), PK/UK/FK/CHECK,
--       indexes, triggers, and the SQL text of MGP views
--    4. Sequences
--    5. Stored PL/SQL code named MGP* (packages/procedures/functions)
--    6. Lookup/master values (types, statuses, units, roles) and
--       COUNTS ONLY grouped by type/status - no names, no items.
--
--  Nothing is changed in the database; the script only reads.
--  You may delete section 6 if even counts cannot be shared.
-- =====================================================================

SET SERVEROUTPUT ON SIZE UNLIMITED FORMAT WRAPPED
SET LINESIZE 32767
SET PAGESIZE 50000
SET LONG 100000
SET TRIMSPOOL ON
SET TRIMOUT ON
SET FEEDBACK OFF
SET VERIFY OFF
SET DEFINE OFF
SET SQLBLANKLINES ON

SPOOL mgp_schema_desc.txt

DECLARE
    -- HR / other-schema objects referenced by the old ASP pages
    v_ext SYS.ODCIVARCHAR2LIST := SYS.ODCIVARCHAR2LIST(
        'HRDATA.EMPDETAILS',
        'HRDATA.VIEW_OFFICERS',
        'HRDATA.HR_DIVISIONS',
        'HRDATA.HR_EMP_DIVISION',
        'HRDATA.HR_DESIGNATIONS',
        'HRDATA.HR_EMP_MAIN',
        'HRDATA.HR_EMP_DESIGNATION',
        'HRDATA.HR_EMP_CONTACTDETAILS',
        'HRDATA.HR_V_DESIGHIERARCHY',
        'HRIMS.HR_EMP_ROLES',
        'HRIMS.HR_DIVISIONS',
        'HRIMS.HR_SPECIALGROUPMEMBERS',
        'HRIMS.HR_EMP_SPECIALREPORTINGGROUP',
        'INTRANETSERVICES.INTRA_APPL_DO_REP_NOMINATE');

    v_line  VARCHAR2(32767);
    v_flag  BOOLEAN;
    v_last  VARCHAR2(400);

    PROCEDURE p(s IN VARCHAR2 DEFAULT NULL) IS
    BEGIN
        DBMS_OUTPUT.PUT_LINE(s);
    END;

    -- pad without truncating
    FUNCTION pad(s IN VARCHAR2, n IN PLS_INTEGER) RETURN VARCHAR2 IS
    BEGIN
        RETURN s || RPAD(' ', GREATEST(1, n - NVL(LENGTH(s), 0)));
    END;

    -- print multi-line text (view SQL, trigger body, conditions)
    PROCEDURE p_text(s IN VARCHAR2, ind IN VARCHAR2 DEFAULT '    ') IS
        v_rest VARCHAR2(32767) := REPLACE(s, CHR(13));
        v_pos  PLS_INTEGER;
    BEGIN
        WHILE v_rest IS NOT NULL LOOP
            v_pos := INSTR(v_rest, CHR(10));
            IF v_pos = 0 THEN
                p(ind || v_rest);
                v_rest := NULL;
            ELSE
                p(ind || SUBSTR(v_rest, 1, v_pos - 1));
                v_rest := SUBSTR(v_rest, v_pos + 1);
            END IF;
        END LOOP;
    END;

    FUNCTION type_of(dt VARCHAR2, dl NUMBER, cl NUMBER, pr NUMBER, sc NUMBER)
        RETURN VARCHAR2 IS
    BEGIN
        IF dt IN ('VARCHAR2', 'NVARCHAR2', 'CHAR', 'NCHAR') THEN
            RETURN dt || '(' || NVL(NULLIF(cl, 0), dl) || ')';
        ELSIF dt = 'RAW' THEN
            RETURN dt || '(' || dl || ')';
        ELSIF dt = 'NUMBER' AND pr IS NOT NULL THEN
            RETURN 'NUMBER(' || pr || CASE WHEN NVL(sc, 0) <> 0 THEN ',' || sc END || ')';
        ELSIF dt = 'NUMBER' AND sc = 0 THEN
            RETURN 'NUMBER(*,0)';
        ELSE
            RETURN dt;
        END IF;
    END;

    FUNCTION cons_cols(o VARCHAR2, c VARCHAR2) RETURN VARCHAR2 IS
        r VARCHAR2(4000);
    BEGIN
        FOR x IN (SELECT column_name FROM all_cons_columns
                   WHERE owner = o AND constraint_name = c
                   ORDER BY position) LOOP
            r := r || CASE WHEN r IS NOT NULL THEN ', ' END || x.column_name;
        END LOOP;
        RETURN r;
    END;

    FUNCTION ind_cols(o VARCHAR2, i VARCHAR2) RETURN VARCHAR2 IS
        r VARCHAR2(4000);
    BEGIN
        FOR x IN (SELECT column_name, descend FROM all_ind_columns
                   WHERE index_owner = o AND index_name = i
                   ORDER BY column_position) LOOP
            r := r || CASE WHEN r IS NOT NULL THEN ', ' END || x.column_name
                   || CASE WHEN x.descend = 'DESC' THEN ' DESC' END;
        END LOOP;
        RETURN r;
    END;

    FUNCTION ref_table(o VARCHAR2, c VARCHAR2) RETURN VARCHAR2 IS
        r VARCHAR2(200);
    BEGIN
        SELECT owner || '.' || table_name INTO r
          FROM all_constraints WHERE owner = o AND constraint_name = c;
        RETURN r;
    EXCEPTION WHEN NO_DATA_FOUND THEN
        RETURN o || '.?(' || c || ')';
    END;

BEGIN
    p('MGP LEGACY SCHEMA DESCRIPTION');
    p('Generated : ' || TO_CHAR(SYSDATE, 'DD-MON-YYYY HH24:MI') || '   Connected as: ' || USER);
    BEGIN
        EXECUTE IMMEDIATE 'SELECT banner FROM v$version WHERE ROWNUM = 1' INTO v_line;
        p('Database  : ' || v_line);
    EXCEPTION WHEN OTHERS THEN
        p('Database  : (no access to v$version)');
    END;

    ------------------------------------------------------------------
    p; p('==================== 1. OBJECT LIST ====================');
    FOR o IN (SELECT owner, object_name, object_type, status
                FROM all_objects
               WHERE (object_name LIKE 'MGP%'
                      OR owner || '.' || object_name IN (SELECT column_value FROM TABLE(v_ext)))
                 AND object_type NOT IN ('INDEX', 'LOB', 'INDEX PARTITION', 'TABLE PARTITION')
                 AND owner NOT IN ('SYS', 'SYSTEM')
               ORDER BY object_type, owner, object_name) LOOP
        p(pad(o.object_type, 18) || pad(o.owner || '.' || o.object_name, 60) || o.status);
    END LOOP;

    p; p('-- Synonyms pointing to MGP objects');
    FOR s IN (SELECT owner, synonym_name, table_owner, table_name, db_link
                FROM all_synonyms
               WHERE (synonym_name LIKE 'MGP%' OR table_name LIKE 'MGP%')
                 AND owner NOT IN ('SYS', 'SYSTEM')
               ORDER BY owner, synonym_name) LOOP
        p(pad(s.owner || '.' || s.synonym_name, 50) || '-> ' || s.table_owner || '.' || s.table_name
          || CASE WHEN s.db_link IS NOT NULL THEN '@' || s.db_link END);
    END LOOP;

    ------------------------------------------------------------------
    p; p('==================== 2. TABLES AND VIEWS ====================');
    FOR t IN (SELECT owner, object_name, object_type
                FROM all_objects
               WHERE object_type IN ('TABLE', 'VIEW')
                 AND (object_name LIKE 'MGP%'
                      OR owner || '.' || object_name IN (SELECT column_value FROM TABLE(v_ext)))
                 AND owner NOT IN ('SYS', 'SYSTEM')
               ORDER BY CASE WHEN object_name LIKE 'MGP%' THEN 0 ELSE 1 END,
                        object_type, owner, object_name) LOOP
        p;
        p(RPAD('=', 110, '='));
        p(t.object_type || '  ' || t.owner || '.' || t.object_name);

        FOR s IN (SELECT num_rows, last_analyzed FROM all_tables
                   WHERE owner = t.owner AND table_name = t.object_name) LOOP
            p('Approx rows (statistics): ' || NVL(TO_CHAR(s.num_rows), 'not analysed')
              || CASE WHEN s.last_analyzed IS NOT NULL
                      THEN '   analysed ' || TO_CHAR(s.last_analyzed, 'DD-MON-YYYY') END);
        END LOOP;
        FOR c IN (SELECT comments FROM all_tab_comments
                   WHERE owner = t.owner AND table_name = t.object_name
                     AND comments IS NOT NULL) LOOP
            p('Comment: ' || c.comments);
        END LOOP;

        p(RPAD('-', 110, '-'));
        p(pad('#', 4) || pad('COLUMN', 32) || pad('TYPE', 18) || pad('NULL?', 10) || 'DEFAULT / COMMENT');
        FOR c IN (SELECT tc.column_id, tc.column_name, tc.data_type, tc.data_length,
                         tc.char_length, tc.data_precision, tc.data_scale, tc.nullable,
                         tc.data_default,
                         (SELECT cc.comments FROM all_col_comments cc
                           WHERE cc.owner = tc.owner AND cc.table_name = tc.table_name
                             AND cc.column_name = tc.column_name) AS col_comment
                    FROM all_tab_columns tc
                   WHERE tc.owner = t.owner AND tc.table_name = t.object_name
                   ORDER BY tc.column_id) LOOP
            v_line := pad(TO_CHAR(c.column_id), 4) || pad(c.column_name, 32)
                   || pad(type_of(c.data_type, c.data_length, c.char_length,
                                  c.data_precision, c.data_scale), 18)
                   || pad(CASE c.nullable WHEN 'N' THEN 'NOT NULL' END, 10);
            IF c.data_default IS NOT NULL THEN
                v_line := v_line || 'DEFAULT '
                       || TRIM(REPLACE(REPLACE(c.data_default, CHR(10), ' '), CHR(13), ' '));
            END IF;
            IF c.col_comment IS NOT NULL THEN
                v_line := v_line || '  -- ' || c.col_comment;
            END IF;
            p(v_line);
        END LOOP;

        -- constraints
        v_flag := FALSE;
        FOR k IN (SELECT constraint_name, constraint_type, search_condition,
                         r_owner, r_constraint_name, delete_rule, status
                    FROM all_constraints
                   WHERE owner = t.owner AND table_name = t.object_name
                     AND constraint_type IN ('P', 'U', 'R', 'C')
                   ORDER BY DECODE(constraint_type, 'P', 1, 'U', 2, 'R', 3, 4),
                            constraint_name) LOOP
            -- skip the automatic "COL IS NOT NULL" checks (already shown above)
            IF NOT (k.constraint_type = 'C'
                    AND k.constraint_name LIKE 'SYS\_C%' ESCAPE '\'
                    AND k.search_condition LIKE '%IS NOT NULL') THEN
                IF NOT v_flag THEN p('  Constraints:'); v_flag := TRUE; END IF;
                v_line := '    ' || pad(k.constraint_name, 32);
                IF k.constraint_type = 'P' THEN
                    v_line := v_line || 'PRIMARY KEY (' || cons_cols(t.owner, k.constraint_name) || ')';
                ELSIF k.constraint_type = 'U' THEN
                    v_line := v_line || 'UNIQUE (' || cons_cols(t.owner, k.constraint_name) || ')';
                ELSIF k.constraint_type = 'R' THEN
                    v_line := v_line || 'FOREIGN KEY (' || cons_cols(t.owner, k.constraint_name)
                           || ') REFERENCES ' || ref_table(k.r_owner, k.r_constraint_name)
                           || ' (' || cons_cols(k.r_owner, k.r_constraint_name) || ')'
                           || CASE WHEN k.delete_rule <> 'NO ACTION' THEN ' ON DELETE ' || k.delete_rule END;
                ELSE
                    v_line := v_line || 'CHECK (' || REPLACE(k.search_condition, CHR(10), ' ') || ')';
                END IF;
                IF k.status <> 'ENABLED' THEN v_line := v_line || '  [' || k.status || ']'; END IF;
                p(v_line);
            END IF;
        END LOOP;

        -- indexes
        v_flag := FALSE;
        FOR i IN (SELECT owner, index_name, uniqueness FROM all_indexes
                   WHERE table_owner = t.owner AND table_name = t.object_name
                   ORDER BY index_name) LOOP
            IF NOT v_flag THEN p('  Indexes:'); v_flag := TRUE; END IF;
            p('    ' || pad(i.index_name, 32) || pad(i.uniqueness, 10)
              || '(' || ind_cols(i.owner, i.index_name) || ')');
        END LOOP;

        -- triggers (only for MGP tables)
        IF t.object_name LIKE 'MGP%' THEN
            FOR g IN (SELECT owner, trigger_name, trigger_type, triggering_event,
                             status, trigger_body
                        FROM all_triggers
                       WHERE table_owner = t.owner AND table_name = t.object_name
                       ORDER BY trigger_name) LOOP
                p('  Trigger: ' || g.owner || '.' || g.trigger_name || '  ' || g.trigger_type
                  || ' ' || g.triggering_event || '  [' || g.status || ']');
                p_text(g.trigger_body, '      ');
            END LOOP;
        END IF;

        -- view SQL (only for MGP views; HR views just show columns)
        IF t.object_type = 'VIEW' AND t.object_name LIKE 'MGP%' THEN
            FOR v IN (SELECT text FROM all_views
                       WHERE owner = t.owner AND view_name = t.object_name) LOOP
                p('  View SQL:');
                p_text(v.text, '    ');
            END LOOP;
        END IF;
    END LOOP;

    ------------------------------------------------------------------
    p; p('==================== 3. SEQUENCES ====================');
    FOR s IN (SELECT sequence_owner, sequence_name, min_value, max_value,
                     increment_by, cache_size, last_number
                FROM all_sequences
               WHERE sequence_name LIKE '%MGP%'
                 AND sequence_owner NOT IN ('SYS', 'SYSTEM')
               ORDER BY sequence_owner, sequence_name) LOOP
        p(pad(s.sequence_owner || '.' || s.sequence_name, 50)
          || 'start>=' || s.min_value || '  inc=' || s.increment_by
          || '  cache=' || s.cache_size || '  next~' || s.last_number);
    END LOOP;

    ------------------------------------------------------------------
    p; p('==================== 4. STORED CODE (MGP*) ====================');
    v_last := NULL;
    FOR s IN (SELECT owner, name, type, line, text
                FROM all_source
               WHERE name LIKE '%MGP%'
                 AND type <> 'TRIGGER'
                 AND owner NOT IN ('SYS', 'SYSTEM')
               ORDER BY owner, name, type, line) LOOP
        IF v_last IS NULL OR v_last <> s.owner || '.' || s.name || ' ' || s.type THEN
            v_last := s.owner || '.' || s.name || ' ' || s.type;
            p; p('---- ' || v_last || ' ----');
        END IF;
        p(RTRIM(RTRIM(s.text, CHR(10)), CHR(13)));
    END LOOP;
END;
/

-- ====================================================================
--  5. LOOKUP / MASTER VALUES + COUNTS ONLY  (delete if not allowed)
--     If a table below does not exist you will just see an ORA- error;
--     the script continues.
-- ====================================================================
SET LINESIZE 300
SET FEEDBACK ON
COLUMN ABVN FORMAT A20
COLUMN TYPE FORMAT A50
COLUMN STATUS FORMAT A40
COLUMN ROLE FORMAT A40
COLUMN ACUNIT FORMAT A30

PROMPT
PROMPT ==================== 5. LOOKUP VALUES ====================
PROMPT -- MGP_TYPES
SELECT * FROM MGP_TYPES ORDER BY 1;
PROMPT -- MGP_STATUS
SELECT * FROM MGP_STATUS ORDER BY 1;
PROMPT -- MGP_ACCOUNTINGUNITS
SELECT * FROM MGP_ACCOUNTINGUNITS ORDER BY 1;
PROMPT -- MGP_ROLES
SELECT * FROM MGP_ROLES ORDER BY 1;
PROMPT -- MGP_REPORTS
SELECT * FROM MGP_REPORTS ORDER BY 1;

PROMPT
PROMPT ==================== 6. COUNTS ONLY (no data) ====================
PROMPT -- Passes per TYPEID / STATUSID (confirms the workflow)
SELECT TYPEID, STATUSID, COUNT(*) AS PASSES,
       TO_CHAR(MIN(MGPDATE), 'YYYY') AS FIRST_YEAR,
       TO_CHAR(MAX(MGPDATE), 'YYYY') AS LAST_YEAR
  FROM MGP_MAIN GROUP BY TYPEID, STATUSID ORDER BY 1, 2;

PROMPT -- Users per role
SELECT ROLEID, COUNT(*) AS USERS FROM MGP_EMP_ROLES GROUP BY ROLEID ORDER BY 1;

PROMPT -- Log activity codes used (ACTID meaning is needed for migration)
SELECT ACTID, COUNT(*) AS ENTRIES FROM MGP_LOG GROUP BY ACTID ORDER BY 1;

PROMPT -- Material transaction types
SELECT TRNTYPE, COUNT(*) AS ENTRIES FROM MGP_MTLTRANSACTIONS GROUP BY TRNTYPE ORDER BY 1;

PROMPT -- Issue types (T/P/L ...) used on passes
SELECT ISSUETYPE, COUNT(*) AS PASSES FROM MGP_ISSUETYPE GROUP BY ISSUETYPE ORDER BY 1;

PROMPT -- Is PASSNO unique per type and year?
SELECT TYPEID, TO_CHAR(MGPDATE, 'YYYY') AS YR,
       COUNT(*) AS PASSES, COUNT(DISTINCT PASSNO) AS DISTINCT_PASSNO,
       MIN(PASSNO) AS MIN_NO, MAX(PASSNO) AS MAX_NO
  FROM MGP_MAIN
 WHERE MGPDATE >= ADD_MONTHS(TRUNC(SYSDATE, 'YYYY'), -24)
 GROUP BY TYPEID, TO_CHAR(MGPDATE, 'YYYY') ORDER BY 1, 2;

SPOOL OFF
SET FEEDBACK ON
PROMPT Done. Output written to mgp_schema_desc.txt
