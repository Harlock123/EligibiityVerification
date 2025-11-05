namespace Elig.Data.Models;

public class EligibilityDisposition
{
    public string DispositionCode { get; set; } = null!;
    public string DispositionDescription { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ICollection<EligibilityResponse> EligibilityResponses { get; set; } = new List<EligibilityResponse>();
}
