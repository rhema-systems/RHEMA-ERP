using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class DecideCivilEngineeringPermittingReviewRequest
{
    public Guid ClientRequestId { get; set; }
    public CivilEngineeringPermittingHodDecisionOutcome Outcome { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
}

public sealed class CivilEngineeringPermittingHodDecisionQueueItemDto
{
    public Guid EngineeringReviewId { get; init; }
    public Guid DevelopmentApprovalFileId { get; init; }
    public string FileNumber { get; init; } = string.Empty;
    public string ApplicationReference { get; init; } = string.Empty;
    public string ApplicantName { get; init; } = string.Empty;
    public string ProjectLabel { get; init; } = string.Empty;
    public string ReviewerName { get; init; } = string.Empty;
    public string CommentCategoryLabel { get; init; } = string.Empty;
    public string ReviewComment { get; init; } = string.Empty;
    public CivilEngineeringPermittingOutcome RecommendedOutcome { get; init; }
    public string? DocumentReference { get; init; }
    public DateTime ReviewedAt { get; init; }
    public DateTime HandoffDueDate { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}
