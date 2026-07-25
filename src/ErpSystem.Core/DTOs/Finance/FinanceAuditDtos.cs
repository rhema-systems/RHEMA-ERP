namespace ErpSystem.Core.DTOs.Finance;

public sealed class FinanceAuditEventDto
{
    public string EventType { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? WorkflowApprovalId { get; set; }
    public object? BeforeValues { get; set; }
    public object? AfterValues { get; set; }
    public object? Context { get; set; }
    public string? Reason { get; set; }
    public string? Comment { get; set; }
    public string? CorrelationId { get; set; }
    public string? Resource { get; set; }
    public string? ResourceId { get; set; }
}
