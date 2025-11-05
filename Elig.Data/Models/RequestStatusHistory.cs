namespace Elig.Data.Models;

/// <summary>
/// Tracks all status changes for eligibility requests
/// </summary>
public class RequestStatusHistory
{
    public long HistoryId { get; set; }
    public long RequestId { get; set; }
    public string? OldStatusCode { get; set; }
    public string NewStatusCode { get; set; } = null!;
    public DateTime StatusChangeDate { get; set; } = DateTime.Now;
    public string? StatusChangeReason { get; set; }
    public string? ChangedBy { get; set; }

    // Navigation properties
    public virtual EligibilityRequest Request { get; set; } = null!;
    public virtual RequestStatus? OldStatus { get; set; }
    public virtual RequestStatus NewStatus { get; set; } = null!;
}
