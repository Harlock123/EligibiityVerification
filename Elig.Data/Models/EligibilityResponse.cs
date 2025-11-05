namespace Elig.Data.Models;

/// <summary>
/// Represents a 271 EDI transaction - Eligibility verification response received from payer
/// </summary>
public class EligibilityResponse
{
    public long ResponseId { get; set; }

    // Link to Request
    public long RequestId { get; set; }
    public string TraceNumber { get; set; } = null!; // Matches request trace number

    // Response Identification
    public string? TransactionControlNumber { get; set; }
    public long? ResponseBatchId { get; set; }

    // Response Details
    public DateTime ResponseReceivedDate { get; set; } = DateTime.Now;
    public string ResponseStatusCode { get; set; } = "RECEIVED";

    // Eligibility Disposition
    public string? EligibilityDispositionCode { get; set; }
    public string? CoverageStatus { get; set; } // 'ACTIVE', 'INACTIVE', etc.

    // Coverage Dates
    public DateTime? CoverageEffectiveDate { get; set; }
    public DateTime? CoverageTerminationDate { get; set; }
    public DateTime? PlanBeginDate { get; set; }
    public DateTime? PlanEndDate { get; set; }

    // Plan Information
    public string? PlanName { get; set; }
    public string? PlanNumber { get; set; }
    public string? GroupName { get; set; }
    public string? GroupNumber { get; set; }

    // Financial Information
    public decimal? CopayAmount { get; set; }
    public decimal? CoinsurancePercentage { get; set; }
    public decimal? DeductibleAmount { get; set; }
    public decimal? DeductibleRemaining { get; set; }
    public decimal? OutOfPocketMax { get; set; }
    public decimal? OutOfPocketRemaining { get; set; }

    // Response Codes
    public string? RejectReasonCode { get; set; }
    public string? RejectReasonDescription { get; set; }
    public string? FollowUpActionCode { get; set; }

    // Raw Data (optional - store original EDI)
    public string? Raw271Data { get; set; }

    // Processing Information
    public DateTime? ProcessedDate { get; set; }
    public string? ProcessingNotes { get; set; }

    // Error Tracking
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    // Audit Fields
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public virtual EligibilityRequest Request { get; set; } = null!;
    public virtual ResponseStatus ResponseStatus { get; set; } = null!;
    public virtual EligibilityDisposition? EligibilityDisposition { get; set; }
    public virtual ICollection<BenefitDetail> BenefitDetails { get; set; } = new List<BenefitDetail>();
    public virtual ICollection<ResponseStatusHistory> StatusHistory { get; set; } = new List<ResponseStatusHistory>();
    public virtual ICollection<TransactionErrorLog> ErrorLogs { get; set; } = new List<TransactionErrorLog>();
}
