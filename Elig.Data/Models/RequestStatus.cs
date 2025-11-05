namespace Elig.Data.Models;

public class RequestStatus
{
    public string StatusCode { get; set; } = null!;
    public string StatusDescription { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ICollection<EligibilityRequest> EligibilityRequests { get; set; } = new List<EligibilityRequest>();
}
