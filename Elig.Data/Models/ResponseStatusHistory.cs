namespace Elig.Data.Models;

/// <summary>
/// Tracks all status changes for eligibility responses
/// </summary>
public class ResponseStatusHistory
{
    public long HistoryId { get; set; }
    public long ResponseId { get; set; }
    public string? OldStatusCode { get; set; }
    public string NewStatusCode { get; set; } = null!;
    public DateTime StatusChangeDate { get; set; } = DateTime.Now;
    public string? StatusChangeReason { get; set; }
    public string? ChangedBy { get; set; }

    // Navigation properties
    public virtual EligibilityResponse Response { get; set; } = null!;
    public virtual ResponseStatus? OldStatus { get; set; }
    public virtual ResponseStatus NewStatus { get; set; } = null!;
}
