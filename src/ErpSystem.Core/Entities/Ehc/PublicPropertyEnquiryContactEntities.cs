using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Ehc;

/// <summary>
/// A tenant-scoped, verified public contact identity. Tickets link to this durable identity so
/// Sales can see the complete enquiry history without creating a Business Partner prematurely.
/// </summary>
[Table("EhcPublicPropertyEnquiryContacts")]
public sealed class EhcPublicPropertyEnquiryContact : TenantEntity
{
    [Required, MaxLength(10)]
    public string Channel { get; set; } = string.Empty;

    [Required, MaxLength(320)]
    public string NormalizedContact { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;

    public DateTime LastVerifiedAtUtc { get; set; }
    public DateTime? LastEnquiryAtUtc { get; set; }

    public Guid? BusinessPartnerId { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }

    public ICollection<EhcTicket> Enquiries { get; set; } = new List<EhcTicket>();
}

/// <summary>
/// Audits anonymous OTP challenges and stores only hashes for the contact and the one-time
/// verification grant. The raw OTP is held by the existing OTP service and is never persisted.
/// </summary>
[Table("EhcPublicPropertyEnquiryVerifications")]
public sealed class EhcPublicPropertyEnquiryVerification : TenantEntity
{
    public Guid ListingId { get; set; }

    [Required, MaxLength(10)]
    public string Channel { get; set; } = string.Empty;

    [Required, StringLength(64, MinimumLength = 64)]
    public string ContactHash { get; set; } = string.Empty;

    public DateTime RequestedAtUtc { get; set; }
    public int VerificationAttemptCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    [StringLength(64, MinimumLength = 64)]
    public string? VerificationTokenHash { get; set; }

    public Guid? ContactId { get; set; }
    public EhcPublicPropertyEnquiryContact? Contact { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedSubmissionId { get; set; }
}
