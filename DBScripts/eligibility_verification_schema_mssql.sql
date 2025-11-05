-- =====================================================
-- 270/271 Eligibility Verification Database Schema
-- Microsoft SQL Server Version
-- =====================================================

-- =====================================================
-- LOOKUP/REFERENCE TABLES
-- =====================================================

-- Request Status Lookup
CREATE TABLE request_status (
    status_code NVARCHAR(20) PRIMARY KEY,
    status_description NVARCHAR(100) NOT NULL,
    is_active BIT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE()
);

-- Response Status Lookup
CREATE TABLE response_status (
    status_code NVARCHAR(20) PRIMARY KEY,
    status_description NVARCHAR(100) NOT NULL,
    is_active BIT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE()
);

-- Eligibility Disposition Lookup
CREATE TABLE eligibility_disposition (
    disposition_code NVARCHAR(20) PRIMARY KEY,
    disposition_description NVARCHAR(100) NOT NULL,
    is_active BIT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE()
);

-- Benefit Type Lookup
CREATE TABLE benefit_type (
    benefit_code NVARCHAR(10) PRIMARY KEY,
    benefit_description NVARCHAR(200) NOT NULL,
    is_active BIT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE()
);

-- Insert common status values
INSERT INTO request_status (status_code, status_description) VALUES
('DRAFT', 'Draft - Not yet submitted'),
('QUEUED', 'Queued for submission'),
('SUBMITTED', 'Submitted to payer'),
('ACKNOWLEDGED', 'Acknowledged by payer'),
('RESPONSE_RECEIVED', 'Response received'),
('ERROR', 'Error in submission'),
('TIMEOUT', 'Request timed out'),
('CANCELLED', 'Request cancelled');

INSERT INTO response_status (status_code, status_description) VALUES
('RECEIVED', 'Response received'),
('PROCESSED', 'Response processed successfully'),
('PARTIAL', 'Partial response received'),
('ERROR', 'Error in response'),
('REJECTED', 'Request rejected by payer');

INSERT INTO eligibility_disposition (disposition_code, disposition_description) VALUES
('ACTIVE', 'Active coverage'),
('INACTIVE', 'Inactive coverage'),
('ELIGIBLE', 'Eligible for benefits'),
('INELIGIBLE', 'Ineligible for benefits'),
('PENDING', 'Eligibility pending'),
('TERMINATED', 'Coverage terminated'),
('UNKNOWN', 'Unknown status');

-- =====================================================
-- CORE ENTITY TABLES
-- =====================================================

-- Payer/Insurance Company Information
CREATE TABLE payer (
    payer_id BIGINT IDENTITY(1,1) PRIMARY KEY,
    payer_name NVARCHAR(200) NOT NULL,
    payer_identifier NVARCHAR(50) NOT NULL,
    payer_identifier_type NVARCHAR(10) NOT NULL, -- e.g., 'NPI', 'TIN', 'PAYERID'
    payer_address_line1 NVARCHAR(100),
    payer_address_line2 NVARCHAR(100),
    payer_city NVARCHAR(50),
    payer_state NVARCHAR(2),
    payer_zip NVARCHAR(10),
    payer_phone NVARCHAR(20),
    payer_fax NVARCHAR(20),
    is_active BIT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE(),
    updated_at DATETIME2 DEFAULT GETDATE(),
    created_by NVARCHAR(50),
    updated_by NVARCHAR(50),
    CONSTRAINT UQ_payer_identifier UNIQUE(payer_identifier, payer_identifier_type)
);

CREATE INDEX idx_payer_identifier ON payer(payer_identifier);
CREATE INDEX idx_payer_active ON payer(is_active);

-- Provider Information
CREATE TABLE provider (
    provider_id BIGINT IDENTITY(1,1) PRIMARY KEY,
    provider_npi NVARCHAR(10) NOT NULL,
    provider_tax_id NVARCHAR(20),
    provider_first_name NVARCHAR(50),
    provider_last_name NVARCHAR(50),
    provider_organization_name NVARCHAR(200),
    provider_type NVARCHAR(20), -- 'INDIVIDUAL', 'ORGANIZATION'
    provider_specialty NVARCHAR(100),
    provider_address_line1 NVARCHAR(100),
    provider_address_line2 NVARCHAR(100),
    provider_city NVARCHAR(50),
    provider_state NVARCHAR(2),
    provider_zip NVARCHAR(10),
    provider_phone NVARCHAR(20),
    is_active BIT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE(),
    updated_at DATETIME2 DEFAULT GETDATE(),
    created_by NVARCHAR(50),
    updated_by NVARCHAR(50),
    CONSTRAINT UQ_provider_npi UNIQUE(provider_npi)
);

CREATE INDEX idx_provider_npi ON provider(provider_npi);
CREATE INDEX idx_provider_tax_id ON provider(provider_tax_id);

-- Member/Patient Information
CREATE TABLE member (
    member_id BIGINT IDENTITY(1,1) PRIMARY KEY,
    member_first_name NVARCHAR(50) NOT NULL,
    member_last_name NVARCHAR(50) NOT NULL,
    member_middle_name NVARCHAR(50),
    member_date_of_birth DATE NOT NULL,
    member_gender NVARCHAR(1), -- 'M', 'F', 'U'
    member_ssn NVARCHAR(11), -- Encrypted in production
    member_subscriber_id NVARCHAR(50),
    member_group_number NVARCHAR(50),
    member_address_line1 NVARCHAR(100),
    member_address_line2 NVARCHAR(100),
    member_city NVARCHAR(50),
    member_state NVARCHAR(2),
    member_zip NVARCHAR(10),
    member_phone NVARCHAR(20),
    member_email NVARCHAR(100),
    is_subscriber BIT DEFAULT 1,
    subscriber_member_id BIGINT, -- Reference to subscriber if this is a dependent
    relationship_to_subscriber NVARCHAR(20), -- 'SELF', 'SPOUSE', 'CHILD', etc.
    is_active BIT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE(),
    updated_at DATETIME2 DEFAULT GETDATE(),
    created_by NVARCHAR(50),
    updated_by NVARCHAR(50),
    CONSTRAINT FK_member_subscriber FOREIGN KEY (subscriber_member_id) REFERENCES member(member_id)
);

CREATE INDEX idx_member_dob ON member(member_date_of_birth);
CREATE INDEX idx_member_subscriber_id ON member(member_subscriber_id);
CREATE INDEX idx_member_name ON member(member_last_name, member_first_name);

-- =====================================================
-- TRANSACTION TABLES
-- =====================================================

-- Eligibility Request (270 Transaction)
CREATE TABLE eligibility_request (
    request_id BIGINT IDENTITY(1,1) PRIMARY KEY,

    -- Reference Information
    trace_number NVARCHAR(50) NOT NULL UNIQUE, -- Used to correlate 270/271
    transaction_control_number NVARCHAR(9),
    request_batch_id BIGINT, -- If requests are batched

    -- Related Entities
    member_id BIGINT NOT NULL,
    provider_id BIGINT NOT NULL,
    payer_id BIGINT NOT NULL,

    -- Request Details
    service_type_code NVARCHAR(10), -- '30' (Health Benefit Plan Coverage), etc.
    service_date DATE, -- Date of service being inquired about
    inquiry_date DATETIME2 NOT NULL DEFAULT GETDATE(),

    -- Status Tracking
    request_status_code NVARCHAR(20) NOT NULL DEFAULT 'DRAFT',
    submission_attempt_count INT DEFAULT 0,
    last_submission_date DATETIME2,
    acknowledgement_date DATETIME2,

    -- Raw Data (optional - store original EDI)
    raw_270_data NVARCHAR(MAX),

    -- Error Tracking
    error_code NVARCHAR(20),
    error_message NVARCHAR(MAX),
    error_date DATETIME2,

    -- Timeout Settings
    expected_response_time INT DEFAULT 30, -- seconds
    timeout_date DATETIME2,

    -- Audit Fields
    created_at DATETIME2 DEFAULT GETDATE(),
    updated_at DATETIME2 DEFAULT GETDATE(),
    created_by NVARCHAR(50),
    updated_by NVARCHAR(50),

    CONSTRAINT FK_eligreq_member FOREIGN KEY (member_id) REFERENCES member(member_id),
    CONSTRAINT FK_eligreq_provider FOREIGN KEY (provider_id) REFERENCES provider(provider_id),
    CONSTRAINT FK_eligreq_payer FOREIGN KEY (payer_id) REFERENCES payer(payer_id),
    CONSTRAINT FK_eligreq_status FOREIGN KEY (request_status_code) REFERENCES request_status(status_code)
);

CREATE INDEX idx_eligreq_trace ON eligibility_request(trace_number);
CREATE INDEX idx_eligreq_status ON eligibility_request(request_status_code);
CREATE INDEX idx_eligreq_member ON eligibility_request(member_id);
CREATE INDEX idx_eligreq_provider ON eligibility_request(provider_id);
CREATE INDEX idx_eligreq_payer ON eligibility_request(payer_id);
CREATE INDEX idx_eligreq_inquiry_date ON eligibility_request(inquiry_date);
CREATE INDEX idx_eligreq_submission_date ON eligibility_request(last_submission_date);

-- Eligibility Response (271 Transaction)
CREATE TABLE eligibility_response (
    response_id BIGINT IDENTITY(1,1) PRIMARY KEY,

    -- Link to Request
    request_id BIGINT NOT NULL,
    trace_number NVARCHAR(50) NOT NULL, -- Matches request trace number

    -- Response Identification
    transaction_control_number NVARCHAR(9),
    response_batch_id BIGINT,

    -- Response Details
    response_received_date DATETIME2 NOT NULL DEFAULT GETDATE(),
    response_status_code NVARCHAR(20) NOT NULL DEFAULT 'RECEIVED',

    -- Eligibility Disposition
    eligibility_disposition_code NVARCHAR(20),
    coverage_status NVARCHAR(20), -- 'ACTIVE', 'INACTIVE', etc.

    -- Coverage Dates
    coverage_effective_date DATE,
    coverage_termination_date DATE,
    plan_begin_date DATE,
    plan_end_date DATE,

    -- Plan Information
    plan_name NVARCHAR(200),
    plan_number NVARCHAR(50),
    group_name NVARCHAR(200),
    group_number NVARCHAR(50),

    -- Financial Information
    copay_amount DECIMAL(10,2),
    coinsurance_percentage DECIMAL(5,2),
    deductible_amount DECIMAL(10,2),
    deductible_remaining DECIMAL(10,2),
    out_of_pocket_max DECIMAL(10,2),
    out_of_pocket_remaining DECIMAL(10,2),

    -- Response Codes
    reject_reason_code NVARCHAR(10),
    reject_reason_description NVARCHAR(MAX),
    follow_up_action_code NVARCHAR(10),

    -- Raw Data (optional - store original EDI)
    raw_271_data NVARCHAR(MAX),

    -- Processing Information
    processed_date DATETIME2,
    processing_notes NVARCHAR(MAX),

    -- Error Tracking
    error_code NVARCHAR(20),
    error_message NVARCHAR(MAX),

    -- Audit Fields
    created_at DATETIME2 DEFAULT GETDATE(),
    updated_at DATETIME2 DEFAULT GETDATE(),
    created_by NVARCHAR(50),
    updated_by NVARCHAR(50),

    CONSTRAINT FK_eligresp_request FOREIGN KEY (request_id) REFERENCES eligibility_request(request_id),
    CONSTRAINT FK_eligresp_status FOREIGN KEY (response_status_code) REFERENCES response_status(status_code),
    CONSTRAINT FK_eligresp_disposition FOREIGN KEY (eligibility_disposition_code) REFERENCES eligibility_disposition(disposition_code)
);

CREATE INDEX idx_eligresp_request ON eligibility_response(request_id);
CREATE INDEX idx_eligresp_trace ON eligibility_response(trace_number);
CREATE INDEX idx_eligresp_status ON eligibility_response(response_status_code);
CREATE INDEX idx_eligresp_disposition ON eligibility_response(eligibility_disposition_code);
CREATE INDEX idx_eligresp_received_date ON eligibility_response(response_received_date);

-- Benefit Details (from 271 Response)
CREATE TABLE benefit_detail (
    benefit_detail_id BIGINT IDENTITY(1,1) PRIMARY KEY,

    -- Link to Response
    response_id BIGINT NOT NULL,

    -- Benefit Information
    benefit_type_code NVARCHAR(10) NOT NULL, -- '30' (Health Benefit Plan), '1' (Medical Care), etc.
    service_type_code NVARCHAR(10), -- Specific service type
    coverage_level_code NVARCHAR(10), -- 'IND' (Individual), 'FAM' (Family), etc.

    -- Benefit Description
    benefit_description NVARCHAR(MAX),

    -- Network Information
    in_network_indicator BIT,
    network_name NVARCHAR(100),

    -- Financial Details
    benefit_amount DECIMAL(10,2),
    benefit_percentage DECIMAL(5,2),
    benefit_quantity INT,
    benefit_quantity_qualifier NVARCHAR(10), -- 'VISITS', 'DAYS', etc.

    -- Time Period
    time_period_qualifier NVARCHAR(10), -- 'YEAR', 'MONTH', 'LIFETIME', etc.

    -- Authorization/Certification
    authorization_required BIT,
    certification_required BIT,
    prior_authorization_number NVARCHAR(50),

    -- Dates
    benefit_begin_date DATE,
    benefit_end_date DATE,

    -- Additional Information
    benefit_notes NVARCHAR(MAX),

    -- Audit Fields
    created_at DATETIME2 DEFAULT GETDATE(),
    updated_at DATETIME2 DEFAULT GETDATE(),

    CONSTRAINT FK_benefit_response FOREIGN KEY (response_id) REFERENCES eligibility_response(response_id),
    CONSTRAINT FK_benefit_type FOREIGN KEY (benefit_type_code) REFERENCES benefit_type(benefit_code)
);

CREATE INDEX idx_benefit_response ON benefit_detail(response_id);
CREATE INDEX idx_benefit_type ON benefit_detail(benefit_type_code);
CREATE INDEX idx_benefit_service ON benefit_detail(service_type_code);

-- =====================================================
-- AUDIT/TRACKING TABLES
-- =====================================================

-- Request Status History (Track all status changes)
CREATE TABLE request_status_history (
    history_id BIGINT IDENTITY(1,1) PRIMARY KEY,
    request_id BIGINT NOT NULL,
    old_status_code NVARCHAR(20),
    new_status_code NVARCHAR(20) NOT NULL,
    status_change_date DATETIME2 DEFAULT GETDATE(),
    status_change_reason NVARCHAR(500),
    changed_by NVARCHAR(50),

    CONSTRAINT FK_reqhist_request FOREIGN KEY (request_id) REFERENCES eligibility_request(request_id),
    CONSTRAINT FK_reqhist_oldstatus FOREIGN KEY (old_status_code) REFERENCES request_status(status_code),
    CONSTRAINT FK_reqhist_newstatus FOREIGN KEY (new_status_code) REFERENCES request_status(status_code)
);

CREATE INDEX idx_reqhist_request ON request_status_history(request_id);
CREATE INDEX idx_reqhist_date ON request_status_history(status_change_date);

-- Response Status History
CREATE TABLE response_status_history (
    history_id BIGINT IDENTITY(1,1) PRIMARY KEY,
    response_id BIGINT NOT NULL,
    old_status_code NVARCHAR(20),
    new_status_code NVARCHAR(20) NOT NULL,
    status_change_date DATETIME2 DEFAULT GETDATE(),
    status_change_reason NVARCHAR(500),
    changed_by NVARCHAR(50),

    CONSTRAINT FK_resphist_response FOREIGN KEY (response_id) REFERENCES eligibility_response(response_id),
    CONSTRAINT FK_resphist_oldstatus FOREIGN KEY (old_status_code) REFERENCES response_status(status_code),
    CONSTRAINT FK_resphist_newstatus FOREIGN KEY (new_status_code) REFERENCES response_status(status_code)
);

CREATE INDEX idx_resphist_response ON response_status_history(response_id);
CREATE INDEX idx_resphist_date ON response_status_history(status_change_date);

-- Transaction Error Log
CREATE TABLE transaction_error_log (
    error_log_id BIGINT IDENTITY(1,1) PRIMARY KEY,
    request_id BIGINT,
    response_id BIGINT,
    error_type NVARCHAR(50) NOT NULL, -- 'SUBMISSION', 'PROCESSING', 'VALIDATION', etc.
    error_code NVARCHAR(20),
    error_message NVARCHAR(MAX),
    error_details NVARCHAR(MAX), -- JSON or detailed error information
    error_date DATETIME2 DEFAULT GETDATE(),
    resolved BIT DEFAULT 0,
    resolved_date DATETIME2,
    resolved_by NVARCHAR(50),
    resolution_notes NVARCHAR(MAX),

    CONSTRAINT FK_errorlog_request FOREIGN KEY (request_id) REFERENCES eligibility_request(request_id),
    CONSTRAINT FK_errorlog_response FOREIGN KEY (response_id) REFERENCES eligibility_response(response_id)
);

CREATE INDEX idx_errorlog_request ON transaction_error_log(request_id);
CREATE INDEX idx_errorlog_response ON transaction_error_log(response_id);
CREATE INDEX idx_errorlog_type ON transaction_error_log(error_type);
CREATE INDEX idx_errorlog_resolved ON transaction_error_log(resolved);

-- =====================================================
-- VIEWS FOR COMMON QUERIES
-- =====================================================

-- Complete Request/Response View
GO
CREATE VIEW vw_eligibility_transactions AS
SELECT
    req.request_id,
    req.trace_number,
    req.inquiry_date,
    req.request_status_code,
    req.last_submission_date,

    m.member_first_name,
    m.member_last_name,
    m.member_date_of_birth,
    m.member_subscriber_id,

    prov.provider_npi,
    prov.provider_organization_name,

    pay.payer_name,
    pay.payer_identifier,

    resp.response_id,
    resp.response_received_date,
    resp.response_status_code,
    resp.eligibility_disposition_code,
    resp.coverage_status,
    resp.plan_name,
    resp.group_number,

    CASE
        WHEN resp.response_id IS NULL THEN 'NO_RESPONSE'
        ELSE 'RESPONSE_RECEIVED'
    END AS transaction_status,

    CASE
        WHEN resp.response_received_date IS NOT NULL
        THEN DATEDIFF(SECOND, req.last_submission_date, resp.response_received_date)
        ELSE NULL
    END AS response_time_seconds

FROM eligibility_request req
LEFT JOIN eligibility_response resp ON req.request_id = resp.request_id
INNER JOIN member m ON req.member_id = m.member_id
INNER JOIN provider prov ON req.provider_id = prov.provider_id
INNER JOIN payer pay ON req.payer_id = pay.payer_id;
GO

-- Pending Requests (No Response Yet)
CREATE VIEW vw_pending_requests AS
SELECT
    req.*,
    m.member_first_name + ' ' + m.member_last_name AS member_name,
    pay.payer_name,
    DATEDIFF(MINUTE, req.last_submission_date, GETDATE()) AS minutes_since_submission
FROM eligibility_request req
INNER JOIN member m ON req.member_id = m.member_id
INNER JOIN payer pay ON req.payer_id = pay.payer_id
LEFT JOIN eligibility_response resp ON req.request_id = resp.request_id
WHERE resp.response_id IS NULL
  AND req.request_status_code IN ('SUBMITTED', 'ACKNOWLEDGED')
  AND req.last_submission_date IS NOT NULL;
GO

-- =====================================================
-- TRIGGERS FOR AUTOMATED TRACKING
-- =====================================================

-- Trigger to update request updated_at timestamp
CREATE TRIGGER trg_update_request_timestamp
ON eligibility_request
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE eligibility_request
    SET updated_at = GETDATE()
    FROM eligibility_request er
    INNER JOIN inserted i ON er.request_id = i.request_id;
END;
GO

-- Trigger to log request status changes
CREATE TRIGGER trg_log_request_status
ON eligibility_request
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO request_status_history (
        request_id,
        old_status_code,
        new_status_code,
        changed_by
    )
    SELECT
        i.request_id,
        d.request_status_code,
        i.request_status_code,
        i.updated_by
    FROM inserted i
    INNER JOIN deleted d ON i.request_id = d.request_id
    WHERE d.request_status_code <> i.request_status_code
       OR (d.request_status_code IS NULL AND i.request_status_code IS NOT NULL)
       OR (d.request_status_code IS NOT NULL AND i.request_status_code IS NULL);
END;
GO

-- Trigger to update response updated_at timestamp
CREATE TRIGGER trg_update_response_timestamp
ON eligibility_response
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE eligibility_response
    SET updated_at = GETDATE()
    FROM eligibility_response er
    INNER JOIN inserted i ON er.response_id = i.response_id;
END;
GO

-- Trigger to log response status changes
CREATE TRIGGER trg_log_response_status
ON eligibility_response
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO response_status_history (
        response_id,
        old_status_code,
        new_status_code,
        changed_by
    )
    SELECT
        i.response_id,
        d.response_status_code,
        i.response_status_code,
        i.updated_by
    FROM inserted i
    INNER JOIN deleted d ON i.response_id = d.response_id
    WHERE d.response_status_code <> i.response_status_code
       OR (d.response_status_code IS NULL AND i.response_status_code IS NOT NULL)
       OR (d.response_status_code IS NOT NULL AND i.response_status_code IS NULL);
END;
GO

-- Trigger to auto-update request status when response is received
CREATE TRIGGER trg_update_request_on_response
ON eligibility_response
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE eligibility_request
    SET
        request_status_code = 'RESPONSE_RECEIVED',
        updated_at = GETDATE()
    FROM eligibility_request req
    INNER JOIN inserted i ON req.request_id = i.request_id
    WHERE req.request_status_code <> 'RESPONSE_RECEIVED';
END;
GO

-- =====================================================
-- COMMENTS/DOCUMENTATION
-- =====================================================

EXEC sp_addextendedproperty
    @name = N'MS_Description', @value = N'270 EDI transaction - Eligibility verification requests sent to payers',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'eligibility_request';

EXEC sp_addextendedproperty
    @name = N'MS_Description', @value = N'271 EDI transaction - Eligibility verification responses received from payers',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'eligibility_response';

EXEC sp_addextendedproperty
    @name = N'MS_Description', @value = N'Patient/member information for eligibility inquiries',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'member';

EXEC sp_addextendedproperty
    @name = N'MS_Description', @value = N'Healthcare provider information (requesting party)',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'provider';

EXEC sp_addextendedproperty
    @name = N'MS_Description', @value = N'Insurance payer/carrier information',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'payer';

EXEC sp_addextendedproperty
    @name = N'MS_Description', @value = N'Detailed benefit information from 271 responses',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'benefit_detail';

EXEC sp_addextendedproperty
    @name = N'MS_Description', @value = N'Unique identifier to correlate 270 request with 271 response',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'eligibility_request',
    @level2type = N'COLUMN', @level2name = 'trace_number';

EXEC sp_addextendedproperty
    @name = N'MS_Description', @value = N'Current status: DRAFT, QUEUED, SUBMITTED, ACKNOWLEDGED, RESPONSE_RECEIVED, ERROR, TIMEOUT, CANCELLED',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'eligibility_request',
    @level2type = N'COLUMN', @level2name = 'request_status_code';

EXEC sp_addextendedproperty
    @name = N'MS_Description', @value = N'Final disposition: ACTIVE, INACTIVE, ELIGIBLE, INELIGIBLE, PENDING, TERMINATED, UNKNOWN',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'eligibility_response',
    @level2type = N'COLUMN', @level2name = 'eligibility_disposition_code';
GO
