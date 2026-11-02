-- ====================================================================
-- Fingerprint verification POC - read-only view over the MAIN fingerprint table
-- Run as a DBA / owner of the main biometric schema. Adjust names in < >.
-- ====================================================================

-- 1. View exposing only what the POC needs. One row per enrolled finger
--    (several rows per PCNO are fine - the program matches against all of them).
CREATE OR REPLACE VIEW FP_AUTH_V AS
SELECT  TRIM(f.<PCNO_COLUMN>)        AS PCNO,
        f.<FINGER_INDEX_COLUMN>      AS FINGER_NO,        -- optional, remove if not available
        f.<TEMPLATE_COLUMN>          AS FINGER_TEMPLATE   -- BLOB/RAW, or Base64/HEX text
FROM    <BIOMETRIC_SCHEMA>.<MAIN_FINGERPRINT_TABLE> f
WHERE   f.<TEMPLATE_COLUMN> IS NOT NULL
--  AND f.<STATUS_COLUMN> = 'ACTIVE'                   -- e.g. skip deleted/inactive enrolments
--  AND f.<PCNO_COLUMN> IN ('100001','100002')         -- e.g. restrict the POC to a few employees
;

-- Optional: if the POC must not touch the main table at all at run time,
-- take a snapshot table instead of a view (refresh when needed):
-- CREATE TABLE FP_AUTH_POC AS SELECT * FROM FP_AUTH_V;
-- CREATE INDEX FP_AUTH_POC_IX1 ON FP_AUTH_POC (PCNO);

-- 2. Read-only account used by the POC program (App.config -> OracleDB)
-- CREATE USER FP_POC_RO IDENTIFIED BY "<strong_password>";
-- GRANT CREATE SESSION TO FP_POC_RO;
-- GRANT SELECT ON <OWNER>.FP_AUTH_V TO FP_POC_RO;
--   then in App.config:  FpSourceTable = <OWNER>.FP_AUTH_V

-- 3. Sanity checks
-- How many fingers per employee, and template sizes (ISO 19794-2 templates are typically ~300-1500 bytes):
-- SELECT PCNO, COUNT(*) FINGERS, MIN(DBMS_LOB.GETLENGTH(FINGER_TEMPLATE)) MIN_LEN,
--        MAX(DBMS_LOB.GETLENGTH(FINGER_TEMPLATE)) MAX_LEN
-- FROM FP_AUTH_V GROUP BY PCNO ORDER BY FINGERS DESC;
--
-- Template format check: an ISO 19794-2 template starts with bytes 46 4D 52 00 ("FMR\0").
-- SELECT PCNO, RAWTOHEX(DBMS_LOB.SUBSTR(FINGER_TEMPLATE, 8, 1)) HEADER FROM FP_AUTH_V WHERE ROWNUM <= 10;
--   464D5200 2030....  -> ISO 19794-2 (TemplateFormat=ISO)
--   464D5200 0000....  -> ANSI 378   (TemplateFormat=ANSI)
--   anything else      -> vendor-specific / image data: the MFS100 SDK cannot match it (see README)
