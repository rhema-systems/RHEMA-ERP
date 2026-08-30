using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringPermittingRoleLookupDto
{
    public Guid RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public IReadOnlyList<CivilEngineeringDevelopmentApprovalLookupOptionDto> Recipients { get; init; } = [];
}

public sealed class CivilEngineeringDevelopmentApprovalHandoffLookupsDto
{
    public IReadOnlyList<CivilEngineeringPermittingSection> Sections { get; init; } = [];
    public IReadOnlyList<CivilEngineeringPermittingRoleLookupDto> RecipientRoles { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDevelopmentApprovalDocumentLookupDto> Documents { get; init; } = [];
}

public sealed class CreateCivilEngineeringDevelopmentApprovalHandoffRequest
{
    public Guid ClientRequestId { get; set; }
    public CivilEngineeringPermittingSection ToSection { get; set; }
    public Guid RecipientRoleId { get; set; }
    public Guid RecipientUserId { get; set; }
    [StringLength(2000)] public string? CoverNote { get; set; }
    public DateTime DueDate { get; set; }
    public List<CivilEngineeringDevelopmentApprovalEvidenceRequest> Evidence { get; set; } = [];
}

public sealed class CivilEngineeringDevelopmentApprovalHandoffDto
{
    public Guid Id { get; init; }
    public int SequenceNumber { get; init; }
    public CivilEngineeringPermittingSection FromSection { get; init; }
    public CivilEngineeringPermittingSection ToSection { get; init; }
    public string FromUserName { get; init; } = string.Empty;
    public Guid RecipientRoleId { get; init; }
    public string RecipientRoleName { get; init; } = string.Empty;
    public Guid RecipientUserId { get; init; }
    public string RecipientUserName { get; init; } = string.Empty;
    public string? CoverNote { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<CivilEngineeringDevelopmentApprovalEvidenceDto> Evidence { get; init; } = [];
}
