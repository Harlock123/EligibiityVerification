namespace Elig.Data.Models;

public class BenefitType
{
    public string BenefitCode { get; set; } = null!;
    public string BenefitDescription { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ICollection<BenefitDetail> BenefitDetails { get; set; } = new List<BenefitDetail>();
}
