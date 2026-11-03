-- ====================================================================
-- Fingerprint MFA for web applications - Oracle 11g R2+
-- Run once as the schema owner used by the web application.
-- ====================================================================

-- 1. Who must give a fingerprint after LDAP login (managed from FingerprintAdmin.aspx)
CREATE TABLE FP_MFA_USERS (
    PCNO         VARCHAR2(30)  NOT NULL,
    MFA_ENABLED  CHAR(1)       DEFAULT 'Y' NOT NULL,
    REMARKS      VARCHAR2(200),
    UPDATED_BY   VARCHAR2(30),
    UPDATED_ON   DATE          DEFAULT SYSDATE,
    CONSTRAINT FP_MFA_USERS_PK PRIMARY KEY (PCNO),
    CONSTRAINT FP_MFA_USERS_CK CHECK (MFA_ENABLED IN ('Y', 'N'))
);

-- 2. Audit of every verification / admin change
CREATE TABLE FP_MFA_AUDIT (
    AUDIT_ID     NUMBER        NOT NULL,
    PCNO         VARCHAR2(30),
    EVENT        VARCHAR2(30),      -- VERIFY, LOGIN, ADMIN_ENABLE, ADMIN_DISABLE
    RESULT       VARCHAR2(20),      -- VERIFIED, NOT_MATCHED, NOT_ENROLLED, BLOCKED, SKIPPED, ERROR, OK
    SCORE        NUMBER(10),
    QUALITY      NUMBER(3),
    CLIENT_IP    VARCHAR2(50),
    APP_NAME     VARCHAR2(50),
    MESSAGE      VARCHAR2(400),
    CREATED_ON   DATE          DEFAULT SYSDATE,
    CONSTRAINT FP_MFA_AUDIT_PK PRIMARY KEY (AUDIT_ID)
);
CREATE SEQUENCE FP_MFA_AUDIT_SEQ START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE INDEX FP_MFA_AUDIT_IX1 ON FP_MFA_AUDIT (PCNO, CREATED_ON);

-- 3. Enrolled fingerprints are READ from your view (Web.config FpMfa.SourceTable),
--    e.g. FP_AUTH_V from FingerprintVerifyPOC/Sql/Create_POC_View.sql, or the POC table FP_POC_ENROLL.

-- 4. If the web application connects with its own user (recommended):
-- GRANT SELECT, INSERT, UPDATE ON FP_MFA_USERS TO <WEBAPP_USER>;
-- GRANT SELECT, INSERT ON FP_MFA_AUDIT TO <WEBAPP_USER>;
-- GRANT SELECT ON FP_MFA_AUDIT_SEQ TO <WEBAPP_USER>;
-- GRANT SELECT ON FP_AUTH_V TO <WEBAPP_USER>;
--   then use OWNER.FP_MFA_USERS etc. in Web.config (FpMfa.UsersTable / AuditTable / AuditSequence / SourceTable)

-- Useful queries
-- SELECT * FROM FP_MFA_USERS ORDER BY UPDATED_ON DESC;
-- SELECT * FROM (SELECT * FROM FP_MFA_AUDIT ORDER BY AUDIT_ID DESC) WHERE ROWNUM <= 50;
