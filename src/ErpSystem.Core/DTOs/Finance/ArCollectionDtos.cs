using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// One posted AR exposure together with its optional controlled follow-up task.
/// Monetary values come from the settlement projection, not mutable invoice totals.
/// </summary>
public sealed class ArCollectionWorkItemDto
{
    public Guid SettlementBalanceId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerCity { get; set; }
    public string? CustomerState { get; set; }
    public string? CustomerCountry { get; set; }
    public string? CustomerPostalCode { get; set; }
    public bool IsPartnerResolved { get; set; }
    public string? PartnerResolutionMessage { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public DateTime DueDate { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal OutstandingAmount { get; set; }
    public int DaysOverdue { get; set; }
    public string AgingBucket { get; set; } = string.Empty;

    public Guid? TaskId { get; set; }
    public string TaskReference { get; set; } = string.Empty;
    public string TaskStatus { get; set; } = "Unassigned";
    public int Priority { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public decimal PromisedAmount { get; set; }
    public DateTime? PromisedPayDate { get; set; }
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool IsFollowUpOverdue { get; set; }
    public bool IsPromiseBreached { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class ArCollectionWorkQueueDto
{
    public DateTime AsOfDate { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public IReadOnlyList<ArCollectionWorkItemDto> Items { get; set; } = Array.Empty<ArCollectionWorkItemDto>();
}

public sealed class ArCollectionSummaryDto
{
    public DateTime AsOfDate { get; set; }
    public int OverdueExposureCount { get; set; }
    public int UnassignedExposureCount { get; set; }
    public int OverdueFollowUpCount { get; set; }
    public int PromiseToPayCount { get; set; }
    public int BreachedPromiseCount { get; set; }
    public IReadOnlyList<ArCollectionCurrencyTotalDto> NativeCurrencyTotals { get; set; } =
        Array.Empty<ArCollectionCurrencyTotalDto>();
    public string? FunctionalCurrencyCode { get; set; }
    public decimal? FunctionalOutstandingTotal { get; set; }
    public decimal? FunctionalPromisedTotal { get; set; }
    public string FunctionalTotalBasis { get; set; } =
        "Historical posting and settlement carrying basis";
    public string? FunctionalTotalUnavailableReason { get; set; }
}

public sealed class ArCollectionCurrencyTotalDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal OutstandingAmount { get; set; }
    public decimal PromisedAmount { get; set; }
}

public sealed class ArCollectionAssigneeDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class GenerateArCollectionTasksDto
{
    public DateTime? AsOfDate { get; set; }

    [Range(1, 3650)]
    public int MinimumDaysOverdue { get; set; } = 1;

    public Guid? AssignedToId { get; set; }

    [Range(0, 365)]
    public int FollowUpInDays { get; set; } = 1;

    [Range(1, 10)]
    public int Priority { get; set; } = 5;
}

public sealed class GenerateArCollectionTasksResultDto
{
    public int EligibleCount { get; set; }
    public int CreatedCount { get; set; }
    public int ExistingCount { get; set; }
    public int RefreshedCount { get; set; }
    public int AutoResolvedCount { get; set; }
    public int ReactivatedCount { get; set; }
    public int SkippedUnresolvedPartnerCount { get; set; }
}

public sealed class CreateArCollectionTaskDto
{
    [Required]
    public Guid SettlementBalanceId { get; set; }

    public Guid? AssignedToId { get; set; }

    public DateTime? FollowUpDate { get; set; }

    [Range(1, 10)]
    public int Priority { get; set; } = 5;

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public sealed class UpdateArCollectionTaskDto
{
    [Required]
    [StringLength(50)]
    public string CollectionStatus { get; set; } = string.Empty;

    public Guid? AssignedToId { get; set; }
    public DateTime? FollowUpDate { get; set; }

    [Range(1, 10)]
    public int Priority { get; set; } = 5;

    [Range(0, 999999999999.99)]
    public decimal PromisedAmount { get; set; }

    public DateTime? PromisedPayDate { get; set; }

    [StringLength(50)]
    public string? Outcome { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class RecordArCollectionReminderDto
{
    [Required]
    [StringLength(20)]
    public string Channel { get; set; } = "Internal";

    [StringLength(250)]
    public string? Recipient { get; set; }

    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = string.Empty;

    public DateTime? NextFollowUpDate { get; set; }

    /// <summary>
    /// External delivery is intentionally recorded as officer-confirmed evidence.
    /// The Finance module does not assume ownership of email/SMS provider setup.
    /// </summary>
    public bool ConfirmedDispatched { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ArCollectionHistoryItemDto
{
    public Guid Id { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string CollectionStatus { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public string? ReminderChannel { get; set; }
    public string? ReminderRecipient { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public DateTime ActivityDate { get; set; }
    public string? ActorName { get; set; }
}
