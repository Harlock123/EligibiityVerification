namespace Elig.Data.Models;

/// <summary>
/// Represents detailed benefit information from 271 response
/// </summary>
public class BenefitDetail
{
    public long BenefitDetailId { get; set; }

    // Link to Response
    public long ResponseId { get; set; }

    // Benefit Information
    public string BenefitTypeCode { get; set; } = null!; // '30' (Health Benefit Plan), '1' (Medical Care), etc.
    public string? ServiceTypeCode { get; set; }
    public string? CoverageLevelCode { get; set; } // 'IND' (Individual), 'FAM' (Family), etc.

    // Benefit Description
    public string? BenefitDescription { get; set; }

    // Network Information
    public bool? InNetworkIndicator { get; set; }
    public string? NetworkName { get; set; }

    // Financial Details
    public decimal? BenefitAmount { get; set; }
    public decimal? BenefitPercentage { get; set; }
    public int? BenefitQuantity { get; set; }
    public string? BenefitQuantityQualifier { get; set; } // 'VISITS', 'DAYS', etc.

    // Time Period
    public string? TimePeriodQualifier { get; set; } // 'YEAR', 'MONTH', 'LIFETIME', etc.

    // Authorization/Certification
    public bool? AuthorizationRequired { get; set; }
    public bool? CertificationRequired { get; set; }
    public string? PriorAuthorizationNumber { get; set; }

    // Dates
    public DateTime? BenefitBeginDate { get; set; }
    public DateTime? BenefitEndDate { get; set; }

    // Additional Information
    public string? BenefitNotes { get; set; }

    // Audit Fields
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual EligibilityResponse Response { get; set; } = null!;
    public virtual BenefitType BenefitType { get; set; } = null!;
}
