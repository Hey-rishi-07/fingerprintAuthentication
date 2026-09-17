-- ====================================================================
-- CentralMFA: Oracle 11g Database Schema
-- Multi-Factor Authentication Gateway & Policy Management
-- ====================================================================

-- 1. Table: APP_USER_FINGERPRINTS (Enrolled Biometric Templates)
CREATE TABLE APP_USER_FINGERPRINTS (
    PCNO            VARCHAR2(50)   NOT NULL,
    FINGER_TEMPLATE BLOB           NOT NULL,
    QUALITY         NUMBER(3)      DEFAULT 0,
    CREATED_ON      DATE           DEFAULT SYSDATE,
    UPDATED_ON      DATE           DEFAULT SYSDATE,
    CONSTRAINT PK_APP_USER_FINGERPRINTS PRIMARY KEY (PCNO)
);

-- 2. Table: MFA_REGISTERED_APPS (Intranet Applications Registered with MFA)
CREATE TABLE MFA_REGISTERED_APPS (
    APP_ID          VARCHAR2(50)   NOT NULL,
    APP_NAME        VARCHAR2(100)  NOT NULL,
    BASE_URL        VARCHAR2(255),
    CALLBACK_URL    VARCHAR2(255)  NOT NULL,
    SECRET_KEY      VARCHAR2(64)   NOT NULL,
    IS_ACTIVE       NUMBER(1)      DEFAULT 1,
    CREATED_ON      DATE           DEFAULT SYSDATE,
    CONSTRAINT PK_MFA_REGISTERED_APPS PRIMARY KEY (APP_ID)
);

-- 3. Table: MFA_APP_POLICIES (Enforcement Rule per Application)
-- Values for POLICY_TYPE:
--   'ALL_USERS'      : Everyone logging in or submitting must scan fingerprint.
--   'SPECIFIC_USERS' : Only PCNOs listed in MFA_APP_USER_WHITELIST must scan; others bypass.
--   'ENROLLED_ONLY'  : Only users who have a fingerprint registered in Oracle must scan.
--   'DISABLED'       : MFA is bypassed for all users of this app.
CREATE TABLE MFA_APP_POLICIES (
    APP_ID          VARCHAR2(50)   NOT NULL,
    POLICY_TYPE     VARCHAR2(30)   DEFAULT 'ALL_USERS',
    UPDATED_ON      DATE           DEFAULT SYSDATE,
    CONSTRAINT PK_MFA_APP_POLICIES PRIMARY KEY (APP_ID),
    CONSTRAINT FK_POLICY_APP FOREIGN KEY (APP_ID) REFERENCES MFA_REGISTERED_APPS(APP_ID) ON DELETE CASCADE
);

-- 4. Table: MFA_APP_USER_WHITELIST (Specific PCNOs assigned to an application)
CREATE TABLE MFA_APP_USER_WHITELIST (
    APP_ID          VARCHAR2(50)   NOT NULL,
    PCNO            VARCHAR2(50)   NOT NULL,
    ADDED_ON        DATE           DEFAULT SYSDATE,
    CONSTRAINT PK_MFA_APP_USER_WHITELIST PRIMARY KEY (APP_ID, PCNO),
    CONSTRAINT FK_WHITELIST_APP FOREIGN KEY (APP_ID) REFERENCES MFA_REGISTERED_APPS(APP_ID) ON DELETE CASCADE
);

-- 5. Table: MFA_AUDIT_LOGS (Audit Trail of all MFA Verifications)
CREATE TABLE MFA_AUDIT_LOGS (
    LOG_ID          NUMBER         NOT NULL,
    APP_ID          VARCHAR2(50),
    PCNO            VARCHAR2(50),
    ACTION_NAME     VARCHAR2(100),
    TXN_ID          VARCHAR2(100),
    MATCH_SCORE     NUMBER(6),
    STATUS          VARCHAR2(20),  -- 'APPROVED', 'DENIED', 'BYPASSED', 'ERROR'
    CLIENT_IP       VARCHAR2(50),
    DETAILS         VARCHAR2(255),
    CREATED_ON      DATE           DEFAULT SYSDATE,
    CONSTRAINT PK_MFA_AUDIT_LOGS PRIMARY KEY (LOG_ID)
);

-- Sequence for Audit Logs
CREATE SEQUENCE SEQ_MFA_AUDIT_LOGS START WITH 1 INCREMENT BY 1 NOCACHE;

-- 6. Seed Sample Applications for Instant Testing
INSERT INTO MFA_REGISTERED_APPS (APP_ID, APP_NAME, BASE_URL, CALLBACK_URL, SECRET_KEY, IS_ACTIVE)
VALUES ('HRMS', 'HR Management System', 'http://127.0.0.1:5100/hrms/', 'http://127.0.0.1:5100/hrms/Callback.aspx', 'HRMS_SECRET_KEY_8F9A2B1C', 1);

INSERT INTO MFA_REGISTERED_APPS (APP_ID, APP_NAME, BASE_URL, CALLBACK_URL, SECRET_KEY, IS_ACTIVE)
VALUES ('ERP', 'Enterprise Resource Planning', 'http://127.0.0.1:5200/erp/', 'http://127.0.0.1:5200/erp/Callback.aspx', 'ERP_SECRET_KEY_77B1C9D2', 1);

INSERT INTO MFA_REGISTERED_APPS (APP_ID, APP_NAME, BASE_URL, CALLBACK_URL, SECRET_KEY, IS_ACTIVE)
VALUES ('PAYROLL', 'Payroll Processing System', 'http://127.0.0.1:5300/payroll/', 'http://127.0.0.1:5300/payroll/Callback.aspx', 'PAYROLL_SECRET_KEY_55E3F8A1', 1);

-- Seed Policies for Sample Apps
INSERT INTO MFA_APP_POLICIES (APP_ID, POLICY_TYPE) VALUES ('HRMS', 'ALL_USERS');
INSERT INTO MFA_APP_POLICIES (APP_ID, POLICY_TYPE) VALUES ('ERP', 'SPECIFIC_USERS');
INSERT INTO MFA_APP_POLICIES (APP_ID, POLICY_TYPE) VALUES ('PAYROLL', 'ALL_USERS');

-- Seed Specific Users for ERP
INSERT INTO MFA_APP_USER_WHITELIST (APP_ID, PCNO) VALUES ('ERP', '1001');
INSERT INTO MFA_APP_USER_WHITELIST (APP_ID, PCNO) VALUES ('ERP', '1002');

COMMIT;
