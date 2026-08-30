using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringPermittingEngineeringReviewLookupsDto
{
    public IReadOnlyList<CivilEngineeringDevelopmentApprovalLookupOptionDto> CommentCategories { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDevelopmentApprovalDocumentLookupDto> Documents { get; init; } = [];
    public IReadOnlyList<CivilEngineeringPermittingOutcome> AllowedOutcomes { get; init; } = [];
}

public sealed class SubmitCivilEngineeringPermittingEngineeringReviewRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid CommentCategoryId { get; set; }
    [Required, StringLength(4000)] public string ReviewComment { get; set; } = string.Empty;
    public CivilEngineeringPermittingOutcome RecommendedOutcome { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
}

public sealed class CivilEngineeringPermittingEngineeringReviewDto
{
    public Guid Id { get; init; }
    public Guid DevelopmentApprovalFileId { get; init; }
    public Guid SourceHandoffId { get; init; }
    public string ReviewerName { get; init; } = string.Empty;
    public string CommentCategoryLabel { get; init; } = string.Empty;
    public string ReviewComment { get; init; } = string.Empty;
    public CivilEngineeringPermittingOutcome RecommendedOutcome { get; init; }
    public CivilEngineeringPermittingEngineeringReviewStage Stage { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string? DocumentReference { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public DateTime ReviewedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}
