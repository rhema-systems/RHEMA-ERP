using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ═════════════════════════════════════════════════════════════════════════════
//  Demo feedback round 2, lane C3 — named sets (plan § 1.5, § 6.4).
//
//  Three masters of one shape (benefit group, skill set, certification set),
//  their members, and the EFFECTIVE reads that union a position's attached sets
//  with its individual rows and say where each line came from.
// ═════════════════════════════════════════════════════════════════════════════

#region Benefit groups

public class BenefitGroupDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    /// <summary>What a delete would sever — the server refuses while <c>PositionCount</c> is non-zero.</summary>
    public int MemberCount { get; set; }
    public int PositionCount { get; set; }

    public List<BenefitGroupMemberDto> Members { get; set; } = new();
}

public class CreateBenefitGroupDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateBenefitGroupDto : CreateBenefitGroupDto
{
    [Required]
    public Guid Id { get; set; }
}

public class BenefitGroupMemberDto : BaseDto
{
    public Guid BenefitGroupId { get; set; }
    public Guid PolicyId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public string? PolicyCode { get; set; }
    public bool PolicyIsActive { get; set; }
}

/// <summary>
/// ⚠ Carries the policy and nothing else. A position needing its own amount or expiry for a
/// benefit takes that benefit individually, not from a group (plan Q-5).
/// </summary>
public class BenefitGroupMemberInputDto
{
    [Required]
    public Guid PolicyId { get; set; }
}

#endregion

#region Skill sets

public class SkillSetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    public int MemberCount { get; set; }
    public int PositionCount { get; set; }

    public List<SkillSetMemberDto> Members { get; set; } = new();
}

public class CreateSkillSetDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSkillSetDto : CreateSkillSetDto
{
    [Required]
    public Guid Id { get; set; }
}

public class SkillSetMemberDto : BaseDto
{
    public Guid SkillSetId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string? SkillCategory { get; set; }
    public bool SkillIsActive { get; set; }
    public SkillLevel RequiredLevel { get; set; }
    public bool IsRequired { get; set; }
    public int Priority { get; set; }
}

/// <summary>Carries the same three attributes the individual position row carries.</summary>
public class SkillSetMemberInputDto
{
    [Required]
    public Guid SkillId { get; set; }

    public SkillLevel RequiredLevel { get; set; } = SkillLevel.Beginner;

    public bool IsRequired { get; set; } = true;

    [Range(1, 100)]
    public int Priority { get; set; } = 1;
}

#endregion

#region Certification sets

public class CertificationSetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    public int MemberCount { get; set; }
    public int PositionCount { get; set; }

    public List<CertificationSetMemberDto> Members { get; set; } = new();
}

public class CreateCertificationSetDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateCertificationSetDto : CreateCertificationSetDto
{
    [Required]
    public Guid Id { get; set; }
}

public class CertificationSetMemberDto : BaseDto
{
    public Guid CertificationSetId { get; set; }
    public Guid CertificationId { get; set; }
    public string CertificationName { get; set; } = string.Empty;
    public string? CertificationCode { get; set; }
    public string CertifyingBodyName { get; set; } = string.Empty;
    public bool CertificationIsActive { get; set; }
    public bool IsMandatory { get; set; }
}

public class CertificationSetMemberInputDto
{
    [Required]
    public Guid CertificationId { get; set; }

    public bool IsMandatory { get; set; } = true;
}

#endregion

#region The effective reads

/// <summary>
/// Where an effective line came from. <c>Individual</c> is a row on the position itself; a set
/// source names the set, and a line provided by two attached sets carries both (rule 4 of § 6.4.2 —
/// overlap between sets is allowed, and the read says so rather than hiding it).
/// </summary>
public class EffectiveSourceDto
{
    /// <summary>"Individual" or "Set".</summary>
    public string Kind { get; set; } = "Individual";

    /// <summary>Null for an individual row; the set's id otherwise.</summary>
    public Guid? SetId { get; set; }

    public string? SetName { get; set; }
    public string? SetCode { get; set; }

    /// <summary>What a screen prints: "Individual" or "Set: SAFETY-CORE".</summary>
    public string Label { get; set; } = "Individual";
}

public class EffectiveBenefitDto
{
    public Guid PolicyId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public string? PolicyCode { get; set; }
    public bool PolicyIsActive { get; set; }

    /// <summary>Only ever set on an individual line — group members carry no amount (Q-5).</summary>
    public decimal? PositionAmount { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>The individual row's id, where this line is one. Null for a group-provided line.</summary>
    public Guid? PositionBenefitId { get; set; }

    public List<EffectiveSourceDto> Sources { get; set; } = new();
}

public class EffectiveSkillDto
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string? SkillCategory { get; set; }
    public bool RequiresCertification { get; set; }

    /// <summary>
    /// Where two sources disagree the STRONGEST wins: the highest required level, and required
    /// beats preferred. A post cannot need a skill less because a second set asked for less.
    /// </summary>
    public SkillLevel RequiredLevel { get; set; }
    public bool IsRequired { get; set; }
    public int Priority { get; set; }

    public Guid? PositionSkillRequirementId { get; set; }

    public List<EffectiveSourceDto> Sources { get; set; } = new();
}

public class EffectiveCertificationDto
{
    public Guid CertificationId { get; set; }
    public string CertificationName { get; set; } = string.Empty;
    public string? CertificationCode { get; set; }
    public string CertifyingBodyName { get; set; } = string.Empty;
    public bool CertificationIsActive { get; set; }

    /// <summary>Mandatory anywhere is mandatory — the same strongest-wins rule as the skill level.</summary>
    public bool IsMandatory { get; set; }

    public Guid? PositionCertificationRequirementId { get; set; }

    public List<EffectiveSourceDto> Sources { get; set; } = new();
}

#endregion
