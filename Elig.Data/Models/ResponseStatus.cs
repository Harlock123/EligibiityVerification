namespace Elig.Data.Models;

public class ResponseStatus
{
    public string StatusCode { get; set; } = null!;
    public string StatusDescription { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ICollection<EligibilityResponse> EligibilityResponses { get; set; } = new List<EligibilityResponse>();
}
