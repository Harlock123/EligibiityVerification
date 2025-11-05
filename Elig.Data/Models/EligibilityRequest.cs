namespace Elig.Data.Models;

/// <summary>
/// Represents a 270 EDI transaction - Eligibility verification request sent to payer
/// </summary>
public class EligibilityRequest
{
    public long RequestId { get; set; }

    // Reference Information
    public string TraceNumber { get; set; } = null!; // Used to correlate 270/271
    public string? TransactionControlNumber { get; set; }
    public long? RequestBatchId { get; set; }

    // Related Entities
    public long MemberId { get; set; }
    public long ProviderId { get; set; }
    public long PayerId { get; set; }

    // Request Details
    public string? ServiceTypeCode { get; set; } // '30' (Health Benefit Plan Coverage), etc.
    public DateTime? ServiceDate { get; set; }
    public DateTime InquiryDate { get; set; } = DateTime.Now;

    // Status Tracking
    public string RequestStatusCode { get; set; } = "DRAFT";
    public int SubmissionAttemptCount { get; set; } = 0;
    public DateTime? LastSubmissionDate { get; set; }
    public DateTime? AcknowledgementDate { get; set; }

    // Raw Data (optional - store original EDI)
    public string? Raw270Data { get; set; }

    // Error Tracking
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ErrorDate { get; set; }

    // Timeout Settings
    public int ExpectedResponseTime { get; set; } = 30; // seconds
    public DateTime? TimeoutDate { get; set; }

    // Audit Fields
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public virtual Member Member { get; set; } = null!;
    public virtual Provider Provider { get; set; } = null!;
    public virtual Payer Payer { get; set; } = null!;
    public virtual RequestStatus RequestStatus { get; set; } = null!;
    public virtual ICollection<EligibilityResponse> Responses { get; set; } = new List<EligibilityResponse>();
    public virtual ICollection<RequestStatusHistory> StatusHistory { get; set; } = new List<RequestStatusHistory>();
    public virtual ICollection<TransactionErrorLog> ErrorLogs { get; set; } = new List<TransactionErrorLog>();
}
