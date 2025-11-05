namespace Elig.Data.Models;

public class Payer
{
    public long PayerId { get; set; }
    public string PayerName { get; set; } = null!;
    public string PayerIdentifier { get; set; } = null!;
    public string PayerIdentifierType { get; set; } = null!; // 'NPI', 'TIN', 'PAYERID'
    public string? PayerAddressLine1 { get; set; }
    public string? PayerAddressLine2 { get; set; }
    public string? PayerCity { get; set; }
    public string? PayerState { get; set; }
    public string? PayerZip { get; set; }
    public string? PayerPhone { get; set; }
    public string? PayerFax { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<EligibilityRequest> EligibilityRequests { get; set; } = new List<EligibilityRequest>();
}
