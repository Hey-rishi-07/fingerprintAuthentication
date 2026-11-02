-- ====================================================================
-- Fingerprint POC - the POC's OWN enrolment table (Oracle 11g R2+)
-- Independent of the main fingerprint table: enrol / delete freely for testing.
-- (The program also creates this table automatically on start if the DB user has CREATE TABLE.)
-- ====================================================================

CREATE TABLE FP_POC_ENROLL (
    PCNO             VARCHAR2(30)  NOT NULL,
    FINGER_NO        NUMBER(2)     NOT NULL,          -- 1-5 right thumb..little, 6-10 left thumb..little
    FINGER_TEMPLATE  BLOB          NOT NULL,          -- ISO 19794-2 (or ANSI 378) template bytes
    TEMPLATE_FORMAT  VARCHAR2(10)  DEFAULT 'ISO',     -- ISO / ANSI / AUTO(imported)
    QUALITY          NUMBER(3),                       -- capture quality 0-100 (not the match score)
    SOURCE           VARCHAR2(20)  DEFAULT 'POC_ENROLL',  -- POC_ENROLL / IMPORTED / SQL_COPY
    ENROLLED_ON      DATE          DEFAULT SYSDATE,
    ENROLLED_BY      VARCHAR2(100),
    CONSTRAINT FP_POC_ENROLL_PK PRIMARY KEY (PCNO, FINGER_NO)
);

COMMENT ON TABLE FP_POC_ENROLL IS 'Fingerprint POC enrolment table (test data, independent of the main biometric table)';

-- If the POC connects with a separate user:
-- GRANT SELECT, INSERT, UPDATE, DELETE ON FP_POC_ENROLL TO FP_POC_RO;
--   (then use OWNER.FP_POC_ENROLL in App.config: EnrollTable and/or FpSourceTable)


-- ====================================================================
-- PORTING EXISTING TEST DATA into FP_POC_ENROLL
-- ====================================================================
-- Option A (recommended): let the program do it - handles BLOB, Base64 and HEX automatically,
--   skips rows that are not ISO/ANSI templates:
--       FingerprintVerifyPOC.exe --import ALL          (everything in ImportSourceTable, default FP_AUTH_V)
--       FingerprintVerifyPOC.exe --import 123456       (one PC No)
--
-- Option B: plain SQL - only when the main table stores RAW template bytes in a BLOB.
--   Copies a few test PC Nos; finger numbers come from the source, or 1,2,3.. per PC No if absent.
/*
INSERT INTO FP_POC_ENROLL (PCNO, FINGER_NO, FINGER_TEMPLATE, TEMPLATE_FORMAT, SOURCE, ENROLLED_ON, ENROLLED_BY)
SELECT  v.PCNO,
        NVL(v.FINGER_NO, ROW_NUMBER() OVER (PARTITION BY v.PCNO ORDER BY ROWNUM)),
        v.FINGER_TEMPLATE, 'AUTO', 'SQL_COPY', SYSDATE, USER
FROM    FP_AUTH_V v
WHERE   v.PCNO IN ('100001', '100002', '100003')        -- remove this line to copy everyone
AND     NOT EXISTS (SELECT 1 FROM FP_POC_ENROLL e
                    WHERE e.PCNO = v.PCNO AND e.FINGER_NO = NVL(v.FINGER_NO, -1));
COMMIT;
*/

-- Useful queries
-- SELECT PCNO, FINGER_NO, QUALITY, SOURCE, DBMS_LOB.GETLENGTH(FINGER_TEMPLATE) BYTES, ENROLLED_ON
-- FROM FP_POC_ENROLL ORDER BY PCNO, FINGER_NO;
-- DELETE FROM FP_POC_ENROLL WHERE SOURCE = 'POC_ENROLL'; COMMIT;   -- clear your test enrolments
