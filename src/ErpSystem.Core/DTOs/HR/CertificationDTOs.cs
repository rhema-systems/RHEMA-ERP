using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ═════════════════════════════════════════════════════════════════════════════
//  Demo feedback round 2, lane C2 — the certification model (plan § 6.3).
//
//  Catalogue rows, the skill and position links that consume them, what an
//  employee holds, the compliance read, and the expiry sweep's shapes.
// ═════════════════════════════════════════════════════════════════════════════

#region Catalogue

public class CertificationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid CertifyingBodyId { get; set; }
    public string CertifyingBodyName { get; set; } = string.Empty;
    public string? CertifyingBodyAbbreviation { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public CertificationKind Kind { get; set; }
    public string? Description { get; set; }
    public int? ValidityMonths { get; set; }
    public bool RenewalRequired { get; set; }
    public int? ExpiryNotificationLeadDays { get; set; }
    public bool IsActive { get; set; }

    /// <summary>How many skills, positions and people cite it — what a delete would sever.</summary>
    public int SkillCount { get; set; }
    public int PositionCount { get; set; }
    public int HolderCount { get; set; }
}

public class CreateCertificationDto
{
    [Required]
    public Guid CertifyingBodyId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    public CertificationKind Kind { get; set; } = CertificationKind.Certification;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(1, 600)]
    public int? ValidityMonths { get; set; }

    public bool RenewalRequired { get; set; }

    [Range(1, 730)]
    public int? ExpiryNotificationLeadDays { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateCertificationDto : CreateCertificationDto
{
    [Required]
    public Guid Id { get; set; }
}

#endregion

#region Skill links

public class SkillCertificationDto
{
    public Guid Id { get; set; }
    public Guid CertificationId { get; set; }
    public string CertificationName { get; set; } = string.Empty;
    public CertificationKind Kind { get; set; }
    public Guid CertifyingBodyId { get; set; }
    public string CertifyingBodyName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string? Notes { get; set; }
}

/// <summary>One row of a skill's accepted credentials. The skill save sends the whole set.</summary>
public class SkillCertificationInputDto
{
    [Required]
    public Guid CertificationId { get; set; }

    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Position requirements

public class PositionCertificationRequirementDto
{
    public Guid Id { get; set; }
    public Guid CertificationId { get; set; }
    public string CertificationName { get; set; } = string.Empty;
    public CertificationKind Kind { get; set; }
    public Guid CertifyingBodyId { get; set; }
    public string CertifyingBodyName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string? Notes { get; set; }
}

/// <summary>One row of a position's required credentials. The position save sends the whole set.</summary>
public class CreatePositionCertificationRequirementDto
{
    [Required]
    public Guid CertificationId { get; set; }

    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Employee credentials

public class EmployeeCertificationDto : BaseDto
{
    public Guid EmployeeId { get; set; }
    public Guid CertificationId { get; set; }
    public string CertificationName { get; set; } = string.Empty;
    public CertificationKind Kind { get; set; }
    public Guid CertifyingBodyId { get; set; }
    public string CertifyingBodyName { get; set; } = string.Empty;
    public string? CertificateNumber { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>Computed at read time from the dates and the lead days; only Revoked is stored.</summary>
    public EmployeeCertificationStatus Status { get; set; }

    /// <summary>Negative once expired; null when the credential does not expire.</summary>
    public int? DaysUntilExpiry { get; set; }

    public bool IsRevoked { get; set; }
    public DateOnly? RevokedOn { get; set; }
    public string? RevocationReason { get; set; }

    public bool HasEvidence { get; set; }
    public string? EvidenceFileName { get; set; }
    public long? EvidenceFileSizeBytes { get; set; }

    public bool IsVerified { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedOn { get; set; }

    public string? Notes { get; set; }
}

public class CreateEmployeeCertificationDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid CertificationId { get; set; }

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    public DateOnly? IssuedOn { get; set; }

    /// <summary>Left blank, it is computed from IssuedOn and the catalogue's ValidityMonths.</summary>
    public DateOnly? ExpiresOn { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateEmployeeCertificationDto
{
    [Required]
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    public DateOnly? IssuedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class RevokeEmployeeCertificationDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateOnly? RevokedOn { get; set; }
}

#endregion

#region Compliance

/// <summary>
/// What the employee's position requires against what the employee holds — the same shape as
/// the document compliance strip.
/// </summary>
public class EmployeeCertificationComplianceDto
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }

    public bool RequiresCertification { get; set; }
    public bool RequiresLicense { get; set; }

    /// <summary>Every mandatory requirement is held, valid and not revoked.</summary>
    public bool IsCompliant { get; set; }

    public int MandatoryCount { get; set; }
    public int MandatorySatisfiedCount { get; set; }

    /// <summary>Credentials the employee holds that expire inside their lead window.</summary>
    public int ExpiringSoonCount { get; set; }

    /// <summary>Credentials the employee holds that have expired.</summary>
    public int ExpiredCount { get; set; }

    public List<EmployeeCertificationComplianceLineDto> Lines { get; set; } = new();
}

public class EmployeeCertificationComplianceLineDto
{
    public Guid CertificationId { get; set; }
    public string CertificationName { get; set; } = string.Empty;
    public string CertifyingBodyName { get; set; } = string.Empty;
    public CertificationKind Kind { get; set; }
    public bool IsMandatory { get; set; }

    /// <summary><c>Held</c>, <c>ExpiringSoon</c>, <c>Expired</c>, <c>Revoked</c> or <c>Missing</c>.</summary>
    public string Status { get; set; } = string.Empty;

    public bool IsSatisfied { get; set; }
    public Guid? EmployeeCertificationId { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public int? DaysUntilExpiry { get; set; }
}

#endregion

#region The expiry sweep

public class CertificationExpiryReminderItemDto
{
    public string Kind { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }
    public Guid EmployeeCertificationId { get; set; }
    public Guid CertificationId { get; set; }
    public string CertificationName { get; set; } = string.Empty;
    public string? CertificateNumber { get; set; }
    public string? Reference { get; set; }
    public DateOnly? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int LeadDays { get; set; }
    public int EscalationTier { get; set; }
    public Guid? RoutedToEmployeeId { get; set; }
    public bool AlreadyRaised { get; set; }
}

public class CertificationExpiryRunResultDto
{
    public Guid RunId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int CredentialsConsidered { get; set; }
    public int RemindersQueued { get; set; }
    public int AlreadyRaised { get; set; }
}

public class CertificationExpiryRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public Guid? TriggeredByUserId { get; set; }
    public int RemindersQueued { get; set; }
}

public class CertificationExpiryLogEntryDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }
    public Guid EmployeeCertificationId { get; set; }
    public Guid CertificationId { get; set; }
    public string? CertificationName { get; set; }
    public string? Reference { get; set; }
    public DateOnly? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public Guid? RoutedToEmployeeId { get; set; }
    public DateTime RaisedAt { get; set; }
}

#endregion
