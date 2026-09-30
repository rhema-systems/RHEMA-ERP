namespace ErpSystem.Core.Interfaces.Finance;

public sealed class FinanceSourceBookAuthorityFreezeRequest
{
    public string OriginModuleCode { get; init; } = string.Empty;
    public string SourceDocumentType { get; init; } = string.Empty;
    public Guid SourceDocumentId { get; init; }
    public string PostingAction { get; init; } = string.Empty;
    public DateTime EffectiveDate { get; init; }
    public string TransactionCurrencyCode { get; init; } = string.Empty;
    public string FreezeStage { get; init; } = string.Empty;
    public Guid? SourceWorkflowInstanceId { get; init; }
}

public sealed class FinanceSourceBookAuthorityOriginRequest
{
    public Guid OriginAuthorityId { get; init; }
    public string Role { get; init; } = string.Empty;
}

public sealed record FinanceSourceBookAuthorityResult(
    Guid AuthorityId,
    int AuthorityVersion,
    string OriginModuleCode,
    string SourceDocumentType,
    Guid SourceDocumentId,
    string PostingAction,
    DateTime EffectiveDate,
    string FreezeStage,
    Guid? SourceWorkflowInstanceId,
    Guid AccountingBookId,
    string AccountingBookCode,
    string FunctionalCurrencyCode,
    string TransactionCurrencyCode,
    string SelectionBasis,
    string AuthorityFingerprint,
    Guid? OriginalFinancePostingEventId,
    Guid? OriginalJournalEntryId);

/// <summary>
/// Freezes exact primary-book authority before approval/first posting, inherits already-frozen
/// source authority, and retains only exact posted legacy evidence. Callers own the serializable
/// transaction and source-document lifecycle checks. The helper verifies exact workflow ownership,
/// type, stage, and completed outcome when posting; it additionally takes a transaction application
/// lock for the normalized source/action coordinate on SQL Server.
/// </summary>
public interface IFinanceSourceBookAuthorityService
{
    Task<FinanceSourceBookAuthorityResult> FreezeInitialPrimaryAsync(
        FinanceSourceBookAuthorityFreezeRequest request,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceBookAuthorityResult> FreezeResubmissionAsync(
        FinanceSourceBookAuthorityFreezeRequest request,
        Guid supersedesAuthorityId,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceBookAuthorityResult> FreezeInheritedAsync(
        FinanceSourceBookAuthorityFreezeRequest request,
        IReadOnlyCollection<FinanceSourceBookAuthorityOriginRequest> origins,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceBookAuthorityResult> RetainExistingPostedOriginalAsync(
        FinanceSourceBookAuthorityFreezeRequest request,
        Guid retainedJournalEntryId,
        Guid? retainedFinancePostingEventId = null,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceBookAuthorityResult> RequireForPostingAsync(
        FinanceSourceBookAuthorityFreezeRequest request,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceBookAuthorityResult> BindOriginalPostingAsync(
        Guid authorityId,
        Guid financePostingEventId,
        Guid journalEntryId,
        CancellationToken cancellationToken = default);

    Task<FinanceSourceBookAuthorityResult> RequireBoundOriginalAsync(
        Guid authorityId,
        CancellationToken cancellationToken = default);
}
