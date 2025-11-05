namespace Elig.Data.Models;

public class Member
{
    public long MemberId { get; set; }
    public string MemberFirstName { get; set; } = null!;
    public string MemberLastName { get; set; } = null!;
    public string? MemberMiddleName { get; set; }
    public DateTime MemberDateOfBirth { get; set; }
    public string? MemberGender { get; set; } // 'M', 'F', 'U'
    public string? MemberSsn { get; set; } // Should be encrypted in production
    public string? MemberSubscriberId { get; set; }
    public string? MemberGroupNumber { get; set; }
    public string? MemberAddressLine1 { get; set; }
    public string? MemberAddressLine2 { get; set; }
    public string? MemberCity { get; set; }
    public string? MemberState { get; set; }
    public string? MemberZip { get; set; }
    public string? MemberPhone { get; set; }
    public string? MemberEmail { get; set; }
    public bool IsSubscriber { get; set; } = true;
    public long? SubscriberMemberId { get; set; }
    public string? RelationshipToSubscriber { get; set; } // 'SELF', 'SPOUSE', 'CHILD', etc.
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public virtual Member? SubscriberMember { get; set; }
    public virtual ICollection<Member> Dependents { get; set; } = new List<Member>();
    public virtual ICollection<EligibilityRequest> EligibilityRequests { get; set; } = new List<EligibilityRequest>();
}
