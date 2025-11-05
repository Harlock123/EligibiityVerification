namespace Elig.Data.Models;

public class Provider
{
    public long ProviderId { get; set; }
    public string ProviderNpi { get; set; } = null!;
    public string? ProviderTaxId { get; set; }
    public string? ProviderFirstName { get; set; }
    public string? ProviderLastName { get; set; }
    public string? ProviderOrganizationName { get; set; }
    public string? ProviderType { get; set; } // 'INDIVIDUAL', 'ORGANIZATION'
    public string? ProviderSpecialty { get; set; }
    public string? ProviderAddressLine1 { get; set; }
    public string? ProviderAddressLine2 { get; set; }
    public string? ProviderCity { get; set; }
    public string? ProviderState { get; set; }
    public string? ProviderZip { get; set; }
    public string? ProviderPhone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<EligibilityRequest> EligibilityRequests { get; set; } = new List<EligibilityRequest>();
}
