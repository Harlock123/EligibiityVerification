-- =====================================================
-- Sample Queries for 270/271 Eligibility Verification Database
-- =====================================================

-- =====================================================
-- INSERTING DATA
-- =====================================================

-- Example: Insert a new payer
INSERT INTO payer (
    payer_name,
    payer_identifier,
    payer_identifier_type,
    payer_city,
    payer_state,
    created_by
) VALUES (
    'Blue Cross Blue Shield',
    '12345',
    'PAYERID',
    'Chicago',
    'IL',
    'SYSTEM'
);

-- Example: Insert a new provider
INSERT INTO provider (
    provider_npi,
    provider_organization_name,
    provider_type,
    provider_city,
    provider_state,
    created_by
) VALUES (
    '1234567890',
    'ABC Medical Center',
    'ORGANIZATION',
    'New York',
    'NY',
    'SYSTEM'
);

-- Example: Insert a new member
INSERT INTO member (
    member_first_name,
    member_last_name,
    member_date_of_birth,
    member_gender,
    member_subscriber_id,
    created_by
) VALUES (
    'John',
    'Doe',
    '1980-01-15',
    'M',
    'SUB123456789',
    'SYSTEM'
);

-- Example: Insert a new eligibility request (270)
INSERT INTO eligibility_request (
    trace_number,
    member_id,
    provider_id,
    payer_id,
    service_type_code,
    service_date,
    request_status_code,
    created_by
) VALUES (
    'TRC' || TO_CHAR(CURRENT_TIMESTAMP, 'YYYYMMDDHH24MISS') || LPAD(nextval('eligibility_request_request_id_seq')::TEXT, 6, '0'),
    1, -- member_id from previous insert
    1, -- provider_id from previous insert
    1, -- payer_id from previous insert
    '30', -- Health Benefit Plan Coverage
    CURRENT_DATE,
    'DRAFT',
    'SYSTEM'
);

-- =====================================================
-- WORKFLOW QUERIES
-- =====================================================

-- 1. Submit a request (change status from DRAFT to QUEUED)
UPDATE eligibility_request
SET
    request_status_code = 'QUEUED',
    updated_by = 'BATCH_PROCESSOR',
    updated_at = CURRENT_TIMESTAMP
WHERE request_id = 1
  AND request_status_code = 'DRAFT';

-- 2. Mark request as submitted
UPDATE eligibility_request
SET
    request_status_code = 'SUBMITTED',
    last_submission_date = CURRENT_TIMESTAMP,
    submission_attempt_count = submission_attempt_count + 1,
    updated_by = 'EDI_SENDER',
    updated_at = CURRENT_TIMESTAMP
WHERE request_id = 1
  AND request_status_code = 'QUEUED';

-- 3. Insert a response when received from payer
INSERT INTO eligibility_response (
    request_id,
    trace_number,
    response_status_code,
    eligibility_disposition_code,
    coverage_status,
    plan_name,
    group_number,
    coverage_effective_date,
    coverage_termination_date,
    created_by
) VALUES (
    1, -- request_id
    'TRC20250105120000000001', -- Same trace number as request
    'RECEIVED',
    'ACTIVE',
    'ACTIVE',
    'Gold Plan PPO',
    'GRP987654',
    '2025-01-01',
    '2025-12-31',
    'EDI_RECEIVER'
);

-- 4. Mark response as processed
UPDATE eligibility_response
SET
    response_status_code = 'PROCESSED',
    processed_date = CURRENT_TIMESTAMP,
    updated_by = 'PROCESSOR',
    updated_at = CURRENT_TIMESTAMP
WHERE response_id = 1
  AND response_status_code = 'RECEIVED';

-- =====================================================
-- MONITORING QUERIES
-- =====================================================

-- Find all pending requests (submitted but no response)
SELECT
    req.request_id,
    req.trace_number,
    req.last_submission_date,
    m.member_first_name || ' ' || m.member_last_name AS member_name,
    pay.payer_name,
    ROUND(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - req.last_submission_date))/60, 2) AS minutes_waiting
FROM eligibility_request req
INNER JOIN member m ON req.member_id = m.member_id
INNER JOIN payer pay ON req.payer_id = pay.payer_id
LEFT JOIN eligibility_response resp ON req.request_id = resp.request_id
WHERE resp.response_id IS NULL
  AND req.request_status_code = 'SUBMITTED'
ORDER BY req.last_submission_date;

-- Find requests that may have timed out (waiting more than 5 minutes)
SELECT
    req.request_id,
    req.trace_number,
    req.last_submission_date,
    pay.payer_name,
    ROUND(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - req.last_submission_date))/60, 2) AS minutes_waiting
FROM eligibility_request req
INNER JOIN payer pay ON req.payer_id = pay.payer_id
LEFT JOIN eligibility_response resp ON req.request_id = resp.request_id
WHERE resp.response_id IS NULL
  AND req.request_status_code = 'SUBMITTED'
  AND req.last_submission_date < CURRENT_TIMESTAMP - INTERVAL '5 minutes'
ORDER BY req.last_submission_date;

-- Get request/response pairs with complete information
SELECT
    req.request_id,
    req.trace_number,
    req.inquiry_date,
    req.request_status_code,
    m.member_first_name || ' ' || m.member_last_name AS member_name,
    m.member_subscriber_id,
    pay.payer_name,
    resp.response_id,
    resp.eligibility_disposition_code,
    resp.coverage_status,
    resp.plan_name,
    resp.response_received_date,
    ROUND(EXTRACT(EPOCH FROM (resp.response_received_date - req.last_submission_date)), 2) AS response_time_seconds
FROM eligibility_request req
INNER JOIN member m ON req.member_id = m.member_id
INNER JOIN payer pay ON req.payer_id = pay.payer_id
LEFT JOIN eligibility_response resp ON req.request_id = resp.request_id
WHERE req.last_submission_date >= CURRENT_DATE - INTERVAL '7 days'
ORDER BY req.inquiry_date DESC;

-- =====================================================
-- REPORTING QUERIES
-- =====================================================

-- Daily submission summary
SELECT
    DATE(last_submission_date) AS submission_date,
    request_status_code,
    COUNT(*) AS request_count
FROM eligibility_request
WHERE last_submission_date >= CURRENT_DATE - INTERVAL '30 days'
GROUP BY DATE(last_submission_date), request_status_code
ORDER BY submission_date DESC, request_status_code;

-- Response time statistics by payer
SELECT
    pay.payer_name,
    COUNT(resp.response_id) AS total_responses,
    ROUND(AVG(EXTRACT(EPOCH FROM (resp.response_received_date - req.last_submission_date))), 2) AS avg_response_seconds,
    ROUND(MIN(EXTRACT(EPOCH FROM (resp.response_received_date - req.last_submission_date))), 2) AS min_response_seconds,
    ROUND(MAX(EXTRACT(EPOCH FROM (resp.response_received_date - req.last_submission_date))), 2) AS max_response_seconds
FROM eligibility_response resp
INNER JOIN eligibility_request req ON resp.request_id = req.request_id
INNER JOIN payer pay ON req.payer_id = pay.payer_id
WHERE resp.response_received_date >= CURRENT_DATE - INTERVAL '30 days'
GROUP BY pay.payer_name
ORDER BY avg_response_seconds;

-- Eligibility disposition summary
SELECT
    ed.disposition_description,
    pay.payer_name,
    COUNT(*) AS count,
    ROUND(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER (PARTITION BY pay.payer_name), 2) AS percentage_by_payer
FROM eligibility_response resp
INNER JOIN eligibility_disposition ed ON resp.eligibility_disposition_code = ed.disposition_code
INNER JOIN eligibility_request req ON resp.request_id = req.request_id
INNER JOIN payer pay ON req.payer_id = pay.payer_id
WHERE resp.response_received_date >= CURRENT_DATE - INTERVAL '30 days'
GROUP BY ed.disposition_description, pay.payer_name
ORDER BY pay.payer_name, count DESC;

-- Error rate by payer
SELECT
    pay.payer_name,
    COUNT(*) AS total_requests,
    SUM(CASE WHEN req.request_status_code = 'ERROR' THEN 1 ELSE 0 END) AS error_count,
    ROUND(SUM(CASE WHEN req.request_status_code = 'ERROR' THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) AS error_percentage
FROM eligibility_request req
INNER JOIN payer pay ON req.payer_id = pay.payer_id
WHERE req.last_submission_date >= CURRENT_DATE - INTERVAL '30 days'
GROUP BY pay.payer_name
ORDER BY error_percentage DESC;

-- =====================================================
-- SEARCHING QUERIES
-- =====================================================

-- Search for member's eligibility history
SELECT
    req.inquiry_date,
    req.request_status_code,
    pay.payer_name,
    resp.coverage_status,
    resp.eligibility_disposition_code,
    resp.plan_name,
    resp.coverage_effective_date,
    resp.coverage_termination_date
FROM eligibility_request req
INNER JOIN member m ON req.member_id = m.member_id
INNER JOIN payer pay ON req.payer_id = pay.payer_id
LEFT JOIN eligibility_response resp ON req.request_id = resp.request_id
WHERE m.member_subscriber_id = 'SUB123456789'
ORDER BY req.inquiry_date DESC;

-- Search by trace number
SELECT
    req.request_id,
    req.trace_number,
    req.inquiry_date,
    req.request_status_code,
    m.member_first_name || ' ' || m.member_last_name AS member_name,
    pay.payer_name,
    resp.response_id,
    resp.eligibility_disposition_code,
    resp.coverage_status
FROM eligibility_request req
INNER JOIN member m ON req.member_id = m.member_id
INNER JOIN payer pay ON req.payer_id = pay.payer_id
LEFT JOIN eligibility_response resp ON req.request_id = resp.request_id
WHERE req.trace_number = 'TRC20250105120000000001';

-- Get benefit details for a specific response
SELECT
    bd.benefit_type_code,
    bt.benefit_description AS benefit_type,
    bd.in_network_indicator,
    bd.benefit_amount,
    bd.benefit_percentage,
    bd.authorization_required,
    bd.benefit_begin_date,
    bd.benefit_end_date
FROM benefit_detail bd
INNER JOIN benefit_type bt ON bd.benefit_type_code = bt.benefit_code
WHERE bd.response_id = 1
ORDER BY bd.benefit_type_code;

-- =====================================================
-- STATUS HISTORY QUERIES
-- =====================================================

-- View request status change history
SELECT
    rsh.request_id,
    req.trace_number,
    rsh.old_status_code,
    rsh.new_status_code,
    rsh.status_change_date,
    rsh.changed_by,
    rsh.status_change_reason
FROM request_status_history rsh
INNER JOIN eligibility_request req ON rsh.request_id = req.request_id
WHERE rsh.request_id = 1
ORDER BY rsh.status_change_date;

-- View response status change history
SELECT
    rsh.response_id,
    resp.trace_number,
    rsh.old_status_code,
    rsh.new_status_code,
    rsh.status_change_date,
    rsh.changed_by
FROM response_status_history rsh
INNER JOIN eligibility_response resp ON rsh.response_id = resp.response_id
WHERE rsh.response_id = 1
ORDER BY rsh.status_change_date;

-- =====================================================
-- ERROR TRACKING QUERIES
-- =====================================================

-- View all unresolved errors
SELECT
    tel.error_log_id,
    tel.error_type,
    tel.error_code,
    tel.error_message,
    tel.error_date,
    req.trace_number,
    m.member_first_name || ' ' || m.member_last_name AS member_name
FROM transaction_error_log tel
LEFT JOIN eligibility_request req ON tel.request_id = req.request_id
LEFT JOIN member m ON req.member_id = m.member_id
WHERE tel.resolved = FALSE
ORDER BY tel.error_date DESC;

-- Mark an error as resolved
UPDATE transaction_error_log
SET
    resolved = TRUE,
    resolved_date = CURRENT_TIMESTAMP,
    resolved_by = 'ADMIN_USER',
    resolution_notes = 'Issue was related to payer downtime. Resubmitted successfully.'
WHERE error_log_id = 1;

-- =====================================================
-- MAINTENANCE QUERIES
-- =====================================================

-- Archive old completed transactions (example - move to archive table)
-- Note: Create archive tables with same structure before running
/*
INSERT INTO eligibility_request_archive
SELECT * FROM eligibility_request
WHERE request_status_code = 'RESPONSE_RECEIVED'
  AND inquiry_date < CURRENT_DATE - INTERVAL '1 year';

DELETE FROM eligibility_request
WHERE request_status_code = 'RESPONSE_RECEIVED'
  AND inquiry_date < CURRENT_DATE - INTERVAL '1 year';
*/

-- Clean up orphaned draft requests older than 30 days
UPDATE eligibility_request
SET
    request_status_code = 'CANCELLED',
    updated_by = 'SYSTEM_CLEANUP',
    updated_at = CURRENT_TIMESTAMP
WHERE request_status_code = 'DRAFT'
  AND created_at < CURRENT_DATE - INTERVAL '30 days';

-- =====================================================
-- USEFUL VIEWS USAGE
-- =====================================================

-- Use the complete transaction view
SELECT * FROM vw_eligibility_transactions
WHERE inquiry_date >= CURRENT_DATE - INTERVAL '7 days'
ORDER BY inquiry_date DESC;

-- Use the pending requests view
SELECT * FROM vw_pending_requests
WHERE minutes_since_submission > 5
ORDER BY minutes_since_submission DESC;
