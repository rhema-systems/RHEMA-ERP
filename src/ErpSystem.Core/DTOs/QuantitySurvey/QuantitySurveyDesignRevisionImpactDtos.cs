using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyDesignImpactLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Group { get; init; }
    public string? Description { get; init; }
    public Guid? SupersedesDrawingId { get; init; }
}

public sealed class QuantitySurveyDesignImpactLookupsDto
{
    public IReadOnlyList<QuantitySurveyDesignImpactLookupDto> Drawings { get; init; } = [];
    public IReadOnlyList<QuantitySurveyDesignImpactLookupDto> ApprovedBoqLines { get; init; } = [];
}

public sealed class QuantitySurveyDesignImpactLineInputDto
{
    public Guid ProjectBoqVersionLineId { get; init; }
    public QuantitySurveyDesignImpactType ImpactType { get; init; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal? IndicativeQuantity { get; init; }
    [Required, StringLength(1000, MinimumLength = 5)] public string ImpactReason { get; init; } = string.Empty;
}

public sealed class CreateQuantitySurveyDesignImpactRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid ProjectId { get; init; }
    public Guid PreviousDrawingId { get; init; }
    public Guid RevisedDrawingId { get; init; }
    public QuantitySurveyDesignImpactRoute Route { get; init; }
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 10)] public string ChangeSummary { get; init; } = string.Empty;
    [MinLength(1)] public IReadOnlyList<QuantitySurveyDesignImpactLineInputDto> Lines { get; init; } = [];
}

public sealed class QuantitySurveyDesignImpactLifecycleRequest
{
    public Guid ClientRequestId { get; init; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
    [Required] public string RowVersion { get; init; } = string.Empty;
}

public sealed class QuantitySurveyDesignImpactLineDto
{
    public Guid Id { get; init; }
    public Guid ProjectBoqVersionLineId { get; init; }
    public Guid BoqLineKey { get; init; }
    public QuantitySurveyDesignImpactType ImpactType { get; init; }
    public string LineReference { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public decimal PreviousQuantity { get; init; }
    public decimal? IndicativeQuantity { get; init; }
    public string ImpactReason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyDesignImpactDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid PreviousDrawingId { get; init; }
    public Guid RevisedDrawingId { get; init; }
    public string PreviousDrawingLabel { get; init; } = string.Empty;
    public string RevisedDrawingLabel { get; init; } = string.Empty;
    public string ImpactNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ChangeSummary { get; init; } = string.Empty;
    public QuantitySurveyDesignImpactRoute Route { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid? WorkflowInstanceId { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyDesignImpactLineDto> Lines { get; init; } = [];
}

public sealed class QuantitySurveyDesignImpactRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? BeforeJson { get; init; }
    public string AfterJson { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
