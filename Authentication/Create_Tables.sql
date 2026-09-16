-- ====================================================================
-- Oracle 11g DDL Script: MFA Fingerprint Storage Table
-- Table: APP_USER_FINGERPRINTS
-- ====================================================================

-- 1. Create table for storing fingerprint templates as BLOB
CREATE TABLE APP_USER_FINGERPRINTS (
    PCNO            VARCHAR2(50)   NOT NULL,
    FINGER_TEMPLATE BLOB           NOT NULL,
    QUALITY         NUMBER(3)      DEFAULT 0,
    CREATED_ON      DATE           DEFAULT SYSDATE,
    UPDATED_ON      DATE           DEFAULT SYSDATE,
    CONSTRAINT PK_APP_USER_FINGERPRINTS PRIMARY KEY (PCNO)
);

-- 2. Add comments on table and columns
COMMENT ON TABLE APP_USER_FINGERPRINTS IS 'Stores ISO 19794-2 biometric fingerprint templates for multi-factor authentication';
COMMENT ON COLUMN APP_USER_FINGERPRINTS.PCNO IS 'Employee ID / PCNO matching Active Directory SAMAccountName or EmployeeID attribute';
COMMENT ON COLUMN APP_USER_FINGERPRINTS.FINGER_TEMPLATE IS 'Binary fingerprint template bytes captured from Mantra MFS100 scanner';
COMMENT ON COLUMN APP_USER_FINGERPRINTS.QUALITY IS 'Fingerprint capture quality score (0 to 100)';
COMMENT ON COLUMN APP_USER_FINGERPRINTS.CREATED_ON IS 'Initial enrollment timestamp';
COMMENT ON COLUMN APP_USER_FINGERPRINTS.UPDATED_ON IS 'Last fingerprint update timestamp';

-- 3. Optional sample query to verify table and BLOB lengths
-- SELECT PCNO, DBMS_LOB.GETLENGTH(FINGER_TEMPLATE) AS TEMPLATE_BYTES, QUALITY, UPDATED_ON FROM APP_USER_FINGERPRINTS;

COMMIT;
