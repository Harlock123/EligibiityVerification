namespace Elig.Data.Models;

/// <summary>
/// Logs errors that occur during transaction processing
/// </summary>
public class TransactionErrorLog
{
    public long ErrorLogId { get; set; }
    public long? RequestId { get; set; }
    public long? ResponseId { get; set; }
    public string ErrorType { get; set; } = null!; // 'SUBMISSION', 'PROCESSING', 'VALIDATION', etc.
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorDetails { get; set; } // JSON or detailed error information
    public DateTime ErrorDate { get; set; } = DateTime.Now;
    public bool Resolved { get; set; } = false;
    public DateTime? ResolvedDate { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }

    // Navigation properties
    public virtual EligibilityRequest? Request { get; set; }
    public virtual EligibilityResponse? Response { get; set; }
}
