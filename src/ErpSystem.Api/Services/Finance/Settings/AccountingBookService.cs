using System.Data;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Settings;

public sealed class AccountingBookService : IAccountingBookService
{
    public const string PrimaryCode = "BASE";
    public const string IfrsAdjustmentsCode = "IFRS_ADJUSTMENTS";
    public const string UsdParallelCode = "USD_PARALLEL";
    public const string WorkflowEntityType = "AccountingBookLifecycle";
    private static readonly HashSet<string> PseudoCodes = new(StringComparer.Ordinal)
    {
        "ALL", "ALL_ACTIVE_BOOKS", "ALL_CLASSIFIED_BOOKS", "ALLCLASSIFIEDBOOKS"
    };
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowService? _workflow;
    private readonly IFinanceAuditService? _audit;
    private readonly IAccountingBookInitializationService? _initialization;

    public AccountingBookService(ApplicationDbContext db, ICurrentUserService currentUser, IWorkflowService workflow, IFinanceAuditService audit,
        IAccountingBookInitializationService? initialization = null)
    { _db = db; _currentUser = currentUser; _workflow = workflow; _audit = audit; _initialization = initialization; }
    // Kept for mapping-only legacy callers while DI and governed lifecycle operations use the complete constructor.
    public AccountingBookService(ApplicationDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }
    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<IReadOnlyList<AccountingBookDto>> GetBooksAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        // Reads must never create configuration. Tenant provisioning owns deterministic seeds.
        var query = BookQuery().AsNoTracking();
        if (!includeInactive) query = query.Where(book => book.IsActive);
        var books = await query.OrderBy(book => book.SortOrder).ThenBy(book => book.Code).ToListAsync(cancellationToken);
        var used = await GetUsedBookIdsAsync(books.Select(item => item.Id), cancellationToken);
        var protectedAccountLabels = await GetProtectedAccountLabelsAsync(books, cancellationToken);
        var reversible = await GetReversibleDesignationAsync(cancellationToken);
        var result = new List<AccountingBookDto>();
        foreach (var item in books)
        {
            var readiness = await GetActivationReadinessAsync(item, cancellationToken);
            result.Add(Map(item, used.Contains(item.Id), readiness,
                reversible?.NewPrimaryBookId == item.Id ? reversible : null, protectedAccountLabels));
        }
        return result;
    }

    public async Task<AccountingBookDto> GetBookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var book = await BookQuery().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Accounting book was not found.");
        var reversible = await GetReversibleDesignationAsync(cancellationToken);
        var protectedAccountLabels = await GetProtectedAccountLabelsAsync(new[] { book }, cancellationToken);
        return Map(book, await HasUseAsync(id, cancellationToken), await GetActivationReadinessAsync(book, cancellationToken),
            reversible?.NewPrimaryBookId == id ? reversible : null, protectedAccountLabels);
    }

    public async Task EnsureTenantDefaultsAsync(CancellationToken cancellationToken = default)
    {
        // Compatibility callers validate provisioned authority; they must not manufacture setup as a side effect.
        var books = await _db.AccountingBooks.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted).ToListAsync(cancellationToken);
        if (books.Count == 0) throw new InvalidOperationException("ACCOUNTING_BOOK_SETUP_REQUIRED: No accounting books are configured for this tenant.");
        if (books.Count(item => item.BookType == AccountingBookType.PrimaryFull && item.IsDefault) != 1)
            throw new InvalidOperationException("PRIMARY_BOOK_AUTHORITY_AMBIGUOUS: Exactly one primary/default full book is required.");
    }

    public Task<AccountingBookDto> CreateAsync(CreateAccountingBookDto request, CancellationToken cancellationToken = default) =>
        AtomicAsync(() => CreateCoreAsync(request, cancellationToken), cancellationToken);
    public Task<AccountingBookDto> UpdateAsync(Guid id, UpdateAccountingBookDto request, CancellationToken cancellationToken = default) =>
        AtomicAsync(() => UpdateCoreAsync(id, request, cancellationToken), cancellationToken);
    public Task<AccountingBookDto> RequestTransitionAsync(Guid id, RequestAccountingBookTransitionDto request, CancellationToken cancellationToken = default) =>
        AtomicAsync(() => RequestTransitionCoreAsync(id, request, cancellationToken), cancellationToken);
    public Task<AccountingBookDto> ApproveTransitionAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default) =>
        DecideAsync(id, request, "Approve", cancellationToken);
    public Task<AccountingBookDto> RejectTransitionAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default) =>
        DecideAsync(id, request, "Reject", cancellationToken);
    public Task<AccountingBookDto> RequestPrimaryReplacementAsync(Guid id, RequestPrimaryAccountingBookReplacementDto request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The tenant Primary book is perpetual and cannot be replaced.");
    public Task<AccountingBookDto> ApprovePrimaryReplacementAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The tenant Primary book is perpetual and cannot be replaced.");
    public Task<AccountingBookDto> RejectPrimaryReplacementAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The tenant Primary book is perpetual and cannot be replaced.");
    public Task<AccountingBookDto> RequestPrimaryReplacementReversalAsync(Guid id, RequestPrimaryAccountingBookReversalDto request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The tenant Primary book is perpetual and has no replacement designation to reverse.");
    public Task<AccountingBookDto> ApprovePrimaryReplacementReversalAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The tenant Primary book is perpetual and has no replacement designation to reverse.");
    public Task<AccountingBookDto> RejectPrimaryReplacementReversalAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The tenant Primary book is perpetual and has no replacement designation to reverse.");

    private async Task<AccountingBookDto> RequestPrimaryReplacementCoreAsync(Guid id, RequestPrimaryAccountingBookReplacementDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A primary-book replacement reason is required.");
        var effectiveDate = request.EffectiveDate.Date;
        if (effectiveDate < DateTime.UtcNow.Date) throw new InvalidOperationException("A primary-book replacement cannot be backdated.");
        var target = await BookQuery().SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new KeyNotFoundException("Accounting book was not found.");
        ApplyRowVersion(target, request.RowVersion);
        if (target.PendingLifecycleStatus.HasValue || target.PrimaryReplacementRequestedAtUtc.HasValue)
            throw new InvalidOperationException("The proposed primary book already has a pending governed change.");
        if (await _db.AccountingBooks.AnyAsync(item => item.TenantId == TenantId && !item.IsDeleted
            && item.PrimaryReplacementRequestedAtUtc != null, ct))
            throw new InvalidOperationException("The tenant already has a pending primary-book replacement.");
        if (await HasPendingPrimaryReversalAsync(ct))
            throw new InvalidOperationException("The tenant already has a pending primary-book replacement reversal.");
        var current = await LockAndValidatePrimaryReplacementAsync(target, ct);
        if (await _db.AccountingBookPrimaryDesignations.AnyAsync(item => item.TenantId == TenantId
            && item.EffectiveFrom == effectiveDate && !item.IsDeleted, ct))
            throw new InvalidOperationException("A primary-book designation already exists for this effective date.");
        var workflow = Workflow();
        if (!await workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType))
            throw new InvalidOperationException("A published AccountingBookLifecycle approval workflow is required.");
        var before = Snapshot(target);
        target.PrimaryReplacementFromBookId = current.Id;
        target.PrimaryReplacementEffectiveDate = effectiveDate;
        target.PrimaryReplacementReason = request.Reason.Trim();
        target.PrimaryReplacementRequestedByUserId = RequiredActor();
        target.PrimaryReplacementRequestedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        var result = await workflow.StartApprovalWorkflowAsync(WorkflowEntityType, target.Id);
        if (!result.Success) throw new InvalidOperationException(result.Message ?? "The primary-book replacement workflow could not be started.");
        target.PrimaryReplacementWorkflowInstanceId = result.WorkflowInstanceId;
        target.UpdatedAt = DateTime.UtcNow; target.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(ct);
        await AuditAsync(FinanceAuditEvents.AccountingBookPrimaryReplacementRequested, target, before, Snapshot(target), request.Reason, ct);
        return await LoadDtoAsync(target.Id, ct);
    }

    private Task<AccountingBookDto> DecidePrimaryReplacementAsync(Guid id, DecideAccountingBookTransitionDto request, bool approve, CancellationToken ct) => AtomicAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A primary-book replacement decision reason is required.");
        var target = await BookQuery().SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new KeyNotFoundException("Accounting book was not found.");
        ApplyRowVersion(target, request.RowVersion);
        if (!target.PrimaryReplacementRequestedAtUtc.HasValue || !target.PrimaryReplacementRequestedByUserId.HasValue
            || !target.PrimaryReplacementFromBookId.HasValue || !target.PrimaryReplacementEffectiveDate.HasValue)
            throw new InvalidOperationException("The accounting book has no pending primary-book replacement.");
        var actor = RequiredActor();
        if (target.PrimaryReplacementRequestedByUserId == actor)
            throw new InvalidOperationException("Maker-checker control prohibits the requester from deciding this replacement.");
        var workflow = Workflow();
        if (!await workflow.CanUserApproveAsync(WorkflowEntityType, target.Id, actor))
            throw new UnauthorizedAccessException("The current user is not an assigned approver for this replacement.");
        AccountingBook? current = null;
        if (approve)
        {
            if (target.PrimaryReplacementEffectiveDate.Value.Date > DateTime.UtcNow.Date)
                throw new InvalidOperationException("The replacement cannot be approved before its effective date.");
            current = await LockAndValidatePrimaryReplacementAsync(target, ct);
            if (current.Id != target.PrimaryReplacementFromBookId)
                throw new InvalidOperationException("The current primary book changed while this request awaited approval. Reject it and submit a fresh request.");
        }
        var before = Snapshot(target);
        var result = await workflow.ProcessApprovalStepAsync(WorkflowEntityType, target.Id, actor, approve ? "Approve" : "Reject", request.Reason.Trim());
        if (!result.Success) throw new InvalidOperationException(result.Message ?? "The primary-book replacement decision failed.");
        if (!approve)
        {
            ClearPrimaryReplacement(target);
        }
        else if (result.Status == WorkflowInstanceStatus.Completed)
        {
            var oldPrimary = current!;
            var designation = new AccountingBookPrimaryDesignation
            {
                TenantId = TenantId, PreviousPrimaryBookId = oldPrimary.Id, NewPrimaryBookId = target.Id,
                EffectiveFrom = target.PrimaryReplacementEffectiveDate!.Value.Date,
                RequestReason = target.PrimaryReplacementReason!, RequestedByUserId = target.PrimaryReplacementRequestedByUserId!.Value,
                RequestedAtUtc = target.PrimaryReplacementRequestedAtUtc!.Value, ApprovedByUserId = actor, ApprovedAtUtc = DateTime.UtcNow,
                DecisionReason = request.Reason.Trim(), WorkflowInstanceId = target.PrimaryReplacementWorkflowInstanceId,
                CreatedAt = DateTime.UtcNow, CreatedBy = ActorName()
            };
            _db.AccountingBookPrimaryDesignations.Add(designation);
            // Two saves are intentional: SQL Server's filtered unique index permits only one default.
            // The serializable transaction prevents observers from seeing the temporary no-primary state.
            oldPrimary.BookType = AccountingBookType.ParallelFull; oldPrimary.IsDefault = false;
            oldPrimary.UpdatedAt = DateTime.UtcNow; oldPrimary.UpdatedBy = ActorName();
            await _db.SaveChangesAsync(ct);
            target.BookType = AccountingBookType.PrimaryFull; target.IsDefault = true;
            ClearPrimaryReplacement(target);
        }
        target.UpdatedAt = DateTime.UtcNow; target.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(ct);
        await AuditAsync(approve ? FinanceAuditEvents.AccountingBookPrimaryReplacementApproved : FinanceAuditEvents.AccountingBookPrimaryReplacementRejected,
            target, before, Snapshot(target), request.Reason, ct);
        return await LoadDtoAsync(target.Id, ct);
    }, ct);

    private async Task<AccountingBook> LockAndValidatePrimaryReplacementAsync(AccountingBook target, CancellationToken ct)
    {
        if (target.BookType != AccountingBookType.ParallelFull || target.IsDefault)
            throw new InvalidOperationException("Only a non-primary full accounting book can replace the current primary book.");
        if (target.LifecycleStatus != AccountingBookLifecycleStatus.Active || !target.IsActive || !target.AllowsPosting)
            throw new InvalidOperationException("The proposed primary book must be Active and posting-enabled.");
        var readiness = await GetActivationReadinessAsync(target, ct);
        if (!readiness.IsReady) throw new InvalidOperationException($"PRIMARY_REPLACEMENT_NOT_READY: {string.Join(" ", readiness.Blockers)}");
        var primaries = await _db.AccountingBooks.Where(item => item.TenantId == TenantId && !item.IsDeleted
            && item.BookType == AccountingBookType.PrimaryFull && item.IsDefault).ToListAsync(ct);
        if (primaries.Count != 1) throw new InvalidOperationException("PRIMARY_BOOK_AUTHORITY_AMBIGUOUS: Exactly one current primary/default full book is required.");
        var current = primaries[0];
        if (current.PendingLifecycleStatus.HasValue || current.PrimaryReplacementRequestedAtUtc.HasValue)
            throw new InvalidOperationException("The current primary book has a pending governed change.");
        if (current.LifecycleStatus != AccountingBookLifecycleStatus.Active || !current.IsActive || !current.AllowsPosting)
            throw new InvalidOperationException("The current primary book must remain Active until replacement is approved.");
        if (!string.Equals(current.FunctionalCurrencyCode, target.FunctionalCurrencyCode, StringComparison.Ordinal))
            throw new InvalidOperationException("The replacement and current primary books must have the same functional currency.");
        return current;
    }

    private static void ClearPrimaryReplacement(AccountingBook book)
    {
        book.PrimaryReplacementFromBookId = null; book.PrimaryReplacementEffectiveDate = null;
        book.PrimaryReplacementReason = null; book.PrimaryReplacementRequestedByUserId = null;
        book.PrimaryReplacementRequestedAtUtc = null;
    }

    private async Task<AccountingBookDto> RequestPrimaryReplacementReversalCoreAsync(Guid id, RequestPrimaryAccountingBookReversalDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("A primary-book replacement reversal reason is required.");
        var current = await BookQuery().SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new KeyNotFoundException("Accounting book was not found.");
        ApplyRowVersion(current, request.RowVersion);
        var (designation, _) = await LockAndValidatePrimaryReversalAsync(current, ct);
        if (designation.ReversalRequestedAtUtc.HasValue)
            throw new InvalidOperationException("This primary-book replacement already has a pending reversal.");
        if (await _db.AccountingBookPrimaryDesignations.AnyAsync(item => item.TenantId == TenantId && !item.IsDeleted
            && item.ReversalRequestedAtUtc != null && item.ReversedAtUtc == null, ct))
            throw new InvalidOperationException("The tenant already has a pending primary-book replacement reversal.");
        var workflow = Workflow();
        if (!await workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType))
            throw new InvalidOperationException("A published AccountingBookLifecycle approval workflow is required.");
        var before = Snapshot(current);
        designation.ReversalReason = request.Reason.Trim();
        designation.ReversalRequestedByUserId = RequiredActor();
        designation.ReversalRequestedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        var result = await workflow.StartApprovalWorkflowAsync(WorkflowEntityType, current.Id);
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? "The primary-book replacement reversal workflow could not be started.");
        designation.ReversalWorkflowInstanceId = result.WorkflowInstanceId;
        await _db.SaveChangesAsync(ct);
        await AuditAsync(FinanceAuditEvents.AccountingBookPrimaryReplacementReversalRequested, current, before,
            new { Book = Snapshot(current), DesignationId = designation.Id, designation.ReversalReason }, request.Reason, ct,
            designation.ReversalWorkflowInstanceId);
        return await LoadDtoAsync(current.Id, ct);
    }

    private Task<AccountingBookDto> DecidePrimaryReplacementReversalAsync(Guid id, DecideAccountingBookTransitionDto request, bool approve, CancellationToken ct) => AtomicAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("A primary-book replacement reversal decision reason is required.");
        var current = await BookQuery().SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new KeyNotFoundException("Accounting book was not found.");
        ApplyRowVersion(current, request.RowVersion);
        var designation = await _db.AccountingBookPrimaryDesignations.SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && !item.IsDeleted && item.NewPrimaryBookId == current.Id
            && item.ReversalRequestedAtUtc != null && item.ReversedAtUtc == null, ct)
            ?? throw new InvalidOperationException("The accounting book has no pending primary-book replacement reversal.");
        var actor = RequiredActor();
        if (designation.ReversalRequestedByUserId == actor)
            throw new InvalidOperationException("Maker-checker control prohibits the requester from deciding this reversal.");
        var workflow = Workflow();
        if (!await workflow.CanUserApproveAsync(WorkflowEntityType, current.Id, actor))
            throw new UnauthorizedAccessException("The current user is not an assigned approver for this reversal.");
        AccountingBook? restored = null;
        if (approve)
            (_, restored) = await LockAndValidatePrimaryReversalAsync(current, ct, designation.Id);
        var before = new { Book = Snapshot(current), DesignationId = designation.Id, designation.ReversalReason };
        var result = await workflow.ProcessApprovalStepAsync(WorkflowEntityType, current.Id, actor,
            approve ? "Approve" : "Reject", request.Reason.Trim());
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? "The primary-book replacement reversal decision failed.");
        if (!approve)
        {
            ClearPrimaryReversalRequest(designation);
        }
        else if (result.Status == WorkflowInstanceStatus.Completed)
        {
            current.BookType = AccountingBookType.ParallelFull;
            current.IsDefault = false;
            current.UpdatedAt = DateTime.UtcNow;
            current.UpdatedBy = ActorName();
            await _db.SaveChangesAsync(ct);
            restored!.BookType = AccountingBookType.PrimaryFull;
            restored.IsDefault = true;
            restored.UpdatedAt = DateTime.UtcNow;
            restored.UpdatedBy = ActorName();
            designation.ReversedByUserId = actor;
            designation.ReversedAtUtc = DateTime.UtcNow;
            designation.ReversalDecisionReason = request.Reason.Trim();
        }
        await _db.SaveChangesAsync(ct);
        await AuditAsync(approve ? FinanceAuditEvents.AccountingBookPrimaryReplacementReversalApproved
                : FinanceAuditEvents.AccountingBookPrimaryReplacementReversalRejected,
            current, before, new { Book = Snapshot(current), DesignationId = designation.Id, designation.ReversedAtUtc },
            request.Reason, ct, designation.ReversalWorkflowInstanceId);
        return await LoadDtoAsync(current.Id, ct);
    }, ct);

    private async Task<(AccountingBookPrimaryDesignation Designation, AccountingBook Restored)> LockAndValidatePrimaryReversalAsync(
        AccountingBook current, CancellationToken ct, Guid? expectedDesignationId = null)
    {
        if (current.BookType != AccountingBookType.PrimaryFull || !current.IsDefault
            || current.LifecycleStatus != AccountingBookLifecycleStatus.Active || !current.IsActive || !current.AllowsPosting)
            throw new InvalidOperationException("Only the active current primary/default book can have its latest replacement reversed.");
        if (current.PendingLifecycleStatus.HasValue || current.PrimaryReplacementRequestedAtUtc.HasValue)
            throw new InvalidOperationException("The current primary book has another pending governed change.");
        var latest = await _db.AccountingBookPrimaryDesignations
            .Where(item => item.TenantId == TenantId && !item.IsDeleted && item.ReversedAtUtc == null)
            .OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.ApprovedAtUtc)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("There is no approved primary-book replacement to reverse.");
        if (latest.NewPrimaryBookId != current.Id || (expectedDesignationId.HasValue && latest.Id != expectedDesignationId.Value))
            throw new InvalidOperationException("Only the latest effective primary-book replacement can be reversed.");
        if (latest.EffectiveFrom.Date != DateTime.UtcNow.Date)
            throw new InvalidOperationException("Only a replacement effective today can use same-day reversal. Submit a new effective-dated replacement instead.");
        var restored = await BookQuery().SingleOrDefaultAsync(item => item.Id == latest.PreviousPrimaryBookId, ct)
            ?? throw new InvalidOperationException("The prior primary accounting book is unavailable.");
        if (restored.BookType != AccountingBookType.ParallelFull || restored.IsDefault
            || restored.LifecycleStatus != AccountingBookLifecycleStatus.Active || !restored.IsActive || !restored.AllowsPosting)
            throw new InvalidOperationException("The prior primary book must remain Active, posting-enabled, and non-primary.");
        if (restored.PendingLifecycleStatus.HasValue || restored.PrimaryReplacementRequestedAtUtc.HasValue)
            throw new InvalidOperationException("The prior primary book has a pending governed change.");
        if (!string.Equals(current.FunctionalCurrencyCode, restored.FunctionalCurrencyCode, StringComparison.Ordinal))
            throw new InvalidOperationException("The current and prior primary books must have the same functional currency.");
        var readiness = await GetActivationReadinessAsync(restored, ct);
        if (!readiness.IsReady)
            throw new InvalidOperationException($"PRIMARY_REVERSAL_NOT_READY: {string.Join(" ", readiness.Blockers)}");
        return (latest, restored);
    }

    private static void ClearPrimaryReversalRequest(AccountingBookPrimaryDesignation designation)
    {
        designation.ReversalReason = null;
        designation.ReversalRequestedByUserId = null;
        designation.ReversalRequestedAtUtc = null;
        designation.ReversalWorkflowInstanceId = null;
    }

    private async Task<AccountingBookDto> CreateCoreAsync(CreateAccountingBookDto request, CancellationToken ct)
    {
        if (Enum.TryParse<AccountingBookType>(request.BookType, true, out var requestedType)
            && requestedType == AccountingBookType.PrimaryFull)
            throw new InvalidOperationException("The tenant Primary book is system-provisioned and cannot be created manually.");
        var value = await ValidateStructureAsync(request, null, ct);
        var now = DateTime.UtcNow;
        var book = new AccountingBook
        {
            TenantId = TenantId, Code = value.Code, Name = value.Name, Description = value.Description,
            Purpose = value.Purpose, BookType = value.Type, LifecycleStatus = AccountingBookLifecycleStatus.Draft,
            FunctionalCurrencyCode = value.FunctionalCurrency, EffectiveFromUtc = value.EffectiveFromUtc,
            EffectiveToUtc = value.EffectiveToUtc, BaseAccountingBookId = value.BaseBook?.Id,
            ReplicationStartDate = value.ReplicationStartDate,
            ParallelOpeningMode = value.ParallelOpeningMode,
            ParallelTranslationMethod = value.ParallelTranslationMethod,
            CurrencyTranslationReserveAccountId = value.CurrencyTranslationReserveAccountId,
            CurrencyRoundingAccountId = value.CurrencyRoundingAccountId,
            IsDefault = value.Type == AccountingBookType.PrimaryFull, IsActive = false, AllowsPosting = false,
            IsSystemDefined = false, SortOrder = request.SortOrder, CreatedAt = now, CreatedBy = ActorName()
        };
        _db.AccountingBooks.Add(book);
        await _db.SaveChangesAsync(ct);
        await AuditAsync(FinanceAuditEvents.AccountingBookCreated, book, null, Snapshot(book), "Accounting book created in Draft.", ct);
        return await LoadDtoAsync(book.Id, ct);
    }

    private async Task<AccountingBookDto> UpdateCoreAsync(Guid id, UpdateAccountingBookDto request, CancellationToken ct)
    {
        var book = await BookQuery().SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new KeyNotFoundException("Accounting book was not found.");
        ApplyRowVersion(book, request.RowVersion);
        if (book.PendingLifecycleStatus.HasValue || book.PrimaryReplacementRequestedAtUtc.HasValue || await HasPendingPrimaryReversalAsync(ct)) throw new InvalidOperationException("The accounting book has a pending governed change.");
        if (book.LifecycleStatus == AccountingBookLifecycleStatus.Retired) throw new InvalidOperationException("A retired accounting book cannot be edited.");
        var value = await ValidateStructureAsync(request, book, ct);
        var structuralChange = book.Code != value.Code || book.Purpose != value.Purpose || book.BookType != value.Type
            || book.FunctionalCurrencyCode != value.FunctionalCurrency || book.BaseAccountingBookId != value.BaseBook?.Id
            || book.EffectiveFromUtc != value.EffectiveFromUtc || book.EffectiveToUtc != value.EffectiveToUtc
            || book.ReplicationStartDate != value.ReplicationStartDate
            || book.ParallelOpeningMode != value.ParallelOpeningMode
            || book.ParallelTranslationMethod != value.ParallelTranslationMethod
            || book.CurrencyTranslationReserveAccountId != value.CurrencyTranslationReserveAccountId
            || book.CurrencyRoundingAccountId != value.CurrencyRoundingAccountId;
        if (structuralChange && (await HasUseAsync(id, ct) || book.InitializationStartedAtUtc.HasValue
            || book.LifecycleStatus is AccountingBookLifecycleStatus.Initializing or AccountingBookLifecycleStatus.Active
                or AccountingBookLifecycleStatus.Suspended or AccountingBookLifecycleStatus.Retired))
            throw new InvalidOperationException("Accounting-book structural identity is immutable after accounting use or initialization begins.");
        var before = Snapshot(book);
        book.Code = value.Code; book.Name = value.Name; book.Description = value.Description; book.Purpose = value.Purpose;
        book.BookType = value.Type; book.FunctionalCurrencyCode = value.FunctionalCurrency;
        book.EffectiveFromUtc = value.EffectiveFromUtc; book.EffectiveToUtc = value.EffectiveToUtc;
        book.BaseAccountingBookId = value.BaseBook?.Id; book.IsDefault = value.Type == AccountingBookType.PrimaryFull;
        book.ReplicationStartDate = value.ReplicationStartDate;
        book.ParallelOpeningMode = value.ParallelOpeningMode;
        book.ParallelTranslationMethod = value.ParallelTranslationMethod;
        book.CurrencyTranslationReserveAccountId = value.CurrencyTranslationReserveAccountId;
        book.CurrencyRoundingAccountId = value.CurrencyRoundingAccountId;
        book.SortOrder = request.SortOrder; book.UpdatedAt = DateTime.UtcNow; book.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(ct);
        await AuditAsync(FinanceAuditEvents.AccountingBookUpdated, book, before, Snapshot(book), "Accounting book configuration updated.", ct);
        return await LoadDtoAsync(book.Id, ct);
    }

    private async Task<AccountingBookDto> RequestTransitionCoreAsync(Guid id, RequestAccountingBookTransitionDto request, CancellationToken ct)
    {
        if (!Enum.TryParse<AccountingBookLifecycleStatus>(request.TargetStatus, true, out var target)) throw new InvalidOperationException("Accounting-book target lifecycle status is invalid.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A lifecycle-transition reason is required.");
        var book = await BookQuery().SingleOrDefaultAsync(item => item.Id == id, ct) ?? throw new KeyNotFoundException("Accounting book was not found.");
        if (book.BookType == AccountingBookType.PrimaryFull)
            throw new InvalidOperationException("The tenant Primary book is perpetual and does not support lifecycle transitions.");
        ApplyRowVersion(book, request.RowVersion);
        if (book.PendingLifecycleStatus.HasValue || book.PrimaryReplacementRequestedAtUtc.HasValue || await HasPendingPrimaryReversalAsync(ct)) throw new InvalidOperationException("The accounting book already has a pending governed change.");
        await ValidateGovernedTransitionAsync(book, target, ct);
        var workflow = Workflow();
        if (!await workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType)) throw new InvalidOperationException("A published AccountingBookLifecycle approval workflow is required.");
        var before = Snapshot(book);
        book.PendingLifecycleStatus = target; book.PendingTransitionReason = request.Reason.Trim();
        book.TransitionRequestedByUserId = RequiredActor(); book.TransitionRequestedAtUtc = DateTime.UtcNow;
        book.TransitionDecidedByUserId = null; book.TransitionDecidedAtUtc = null; book.TransitionDecisionReason = null;
        await _db.SaveChangesAsync(ct);
        var result = await workflow.StartApprovalWorkflowAsync(WorkflowEntityType, book.Id);
        if (!result.Success) throw new InvalidOperationException(result.Message ?? "The accounting-book transition workflow could not be started.");
        book.TransitionWorkflowInstanceId = result.WorkflowInstanceId; book.UpdatedAt = DateTime.UtcNow; book.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(ct);
        await AuditAsync(FinanceAuditEvents.AccountingBookTransitionRequested, book, before, Snapshot(book), request.Reason, ct);
        return await LoadDtoAsync(book.Id, ct);
    }

    private Task<AccountingBookDto> DecideAsync(Guid id, DecideAccountingBookTransitionDto request, string action, CancellationToken ct) => AtomicAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("A transition decision reason is required.");
        var book = await BookQuery().SingleOrDefaultAsync(item => item.Id == id, ct) ?? throw new KeyNotFoundException("Accounting book was not found.");
        ApplyRowVersion(book, request.RowVersion);
        if (!book.PendingLifecycleStatus.HasValue || !book.TransitionRequestedByUserId.HasValue) throw new InvalidOperationException("The accounting book has no pending lifecycle transition.");
        var actor = RequiredActor();
        if (book.TransitionRequestedByUserId == actor) throw new InvalidOperationException("Maker-checker control prohibits the requester from deciding this transition.");
        var workflow = Workflow();
        if (!await workflow.CanUserApproveAsync(WorkflowEntityType, book.Id, actor)) throw new UnauthorizedAccessException("The current user is not an assigned approver for this transition.");
        // The base graph is mutable while a request waits for approval. Revalidate it under the
        // serializable writer boundary before advancing the workflow or recording approval evidence.
        if (action == "Approve") await ValidateGovernedTransitionAsync(book, book.PendingLifecycleStatus.Value, ct);
        var before = Snapshot(book);
        var result = await workflow.ProcessApprovalStepAsync(WorkflowEntityType, book.Id, actor, action, request.Reason.Trim());
        if (!result.Success) throw new InvalidOperationException(result.Message ?? $"The transition {action.ToLowerInvariant()} action failed.");
        book.TransitionDecidedByUserId = actor; book.TransitionDecidedAtUtc = DateTime.UtcNow; book.TransitionDecisionReason = request.Reason.Trim();
        var completed = result.Status == WorkflowInstanceStatus.Completed;
        if (action == "Reject") ClearPending(book);
        else if (completed)
        {
            var target = book.PendingLifecycleStatus!.Value;
            if (target == AccountingBookLifecycleStatus.Active
                && book.BookType == AccountingBookType.ParallelFull)
            {
                if (_initialization == null)
                    throw new InvalidOperationException("Parallel opening initialization is unavailable.");
                if (book.ParallelOpeningMode == ParallelBookOpeningMode.GovernedOpeningConversion)
                    await _initialization.ApplyGovernedParallelOpeningAsync(book.Id, ct);
                else if (book.ParallelOpeningMode == ParallelBookOpeningMode.HistoricalReplay)
                    await _initialization.ReplayHistoricalParallelTransactionsAsync(book.Id, ct);
            }
            book.LifecycleStatus = target;
            if (target == AccountingBookLifecycleStatus.Initializing) book.InitializationStartedAtUtc ??= DateTime.UtcNow;
            if ((book.BookType is AccountingBookType.Delta or AccountingBookType.ParallelFull)
                && target == AccountingBookLifecycleStatus.Initializing)
            {
                if (_initialization == null)
                    throw new InvalidOperationException("Derived-book structure provisioning is unavailable.");
                await _initialization.EnsureDeltaStructureAsync(book.Id, ct);
            }
            ApplyPostingFlags(book); ClearPending(book, true);
            if (target is AccountingBookLifecycleStatus.Active or AccountingBookLifecycleStatus.Suspended)
            {
                // Manifest mappings are prepared while inactive, executable only while the
                // governed book is active, and retained for a later governed resumption.
                var preparedMappings = await _db.AccountAccountingBooks.Where(item => item.TenantId == TenantId
                    && item.AccountingBookId == book.Id && !item.IsDeleted).ToListAsync(ct);
                foreach (var mapping in preparedMappings.Where(FinanceClassificationManifestSeeder.IsUntouchedManifestOwnedMapping))
                    mapping.IsEnabled = target == AccountingBookLifecycleStatus.Active;
            }
        }
        book.UpdatedAt = DateTime.UtcNow; book.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(ct);
        var eventType = action == "Reject" ? FinanceAuditEvents.AccountingBookTransitionRejected
            : completed ? FinanceAuditEvents.AccountingBookTransitionApproved : FinanceAuditEvents.AccountingBookTransitionApprovalStepCompleted;
        await AuditAsync(eventType, book, before, Snapshot(book), request.Reason, ct);
        return await LoadDtoAsync(book.Id, ct);
    }, ct);

    private async Task<ValidatedBookStructure> ValidateStructureAsync(CreateAccountingBookDto request, AccountingBook? currentBook, CancellationToken ct)
    {
        var currentId = currentBook?.Id;
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Purpose))
            throw new InvalidOperationException("Accounting-book code, name and accounting purpose are required.");
        if (!Enum.TryParse<AccountingBookType>(request.BookType, true, out var type)) throw new InvalidOperationException("Accounting-book type is invalid.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (code.Length > 20 || PseudoCodes.Contains(code) || !char.IsAsciiLetter(code[0])
            || code.Any(character => !(char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character == '_')))
            throw new InvalidOperationException("Accounting-book code must be a concrete canonical uppercase code using letters, digits and underscores.");
        if (request.Name.Trim().Length > 100 || request.Purpose.Trim().Length > 50 || request.Description?.Trim().Length > 500)
            throw new InvalidOperationException("Accounting-book name, purpose or description exceeds its supported length.");
        if (request.EffectiveFromUtc.HasValue && request.EffectiveToUtc.HasValue && request.EffectiveToUtc <= request.EffectiveFromUtc)
            throw new InvalidOperationException("Accounting-book effective end must be after its effective start.");
        if (await _db.AccountingBooks.AnyAsync(item => item.TenantId == TenantId && item.Code == code && item.Id != currentId && !item.IsDeleted, ct))
            throw new InvalidOperationException("Accounting-book code already exists for this tenant.");

        AccountingBook? baseBook = null;
        string? currency;
        DateTime? effectiveFrom = null;
        DateTime? effectiveTo = null;
        DateTime? replicationStart = null;
        ParallelBookOpeningMode? openingMode = null;
        ParallelBookTranslationMethod? translationMethod = null;
        Guid? translationReserveAccountId = null;
        Guid? roundingAccountId = null;
        if (type == AccountingBookType.Delta)
        {
            if (!request.BaseAccountingBookId.HasValue || request.BaseAccountingBookId == currentId)
                throw new InvalidOperationException("A Delta book requires a different same-tenant base book.");
            baseBook = await _db.AccountingBooks.SingleOrDefaultAsync(item => item.Id == request.BaseAccountingBookId && item.TenantId == TenantId
                && !item.IsDeleted, ct)
                ?? throw new InvalidOperationException("Delta base book is invalid for this tenant.");
            if (baseBook.BookType == AccountingBookType.Delta)
                throw new InvalidOperationException("A Delta book must be based on a Primary or Parallel full book, never another Delta.");
            if (request.ReplicationStartDate.HasValue || !string.IsNullOrWhiteSpace(request.ParallelOpeningMode)
                || !string.IsNullOrWhiteSpace(request.ParallelTranslationMethod)
                || request.CurrencyTranslationReserveAccountId.HasValue || request.CurrencyRoundingAccountId.HasValue)
                throw new InvalidOperationException("Parallel replication and translation settings are not valid for a Delta book.");
            var candidate = new AccountingBook
            {
                Id = currentId ?? Guid.NewGuid(),
                TenantId = TenantId,
                BookType = AccountingBookType.Delta,
                LifecycleStatus = currentBook?.LifecycleStatus ?? AccountingBookLifecycleStatus.Draft,
                BaseAccountingBookId = baseBook.Id,
                Code = code
            };
            await ValidateFullDeltaLineageAsync(candidate, candidate.LifecycleStatus, ct);
            currency = null;
            effectiveFrom = request.EffectiveFromUtc?.Date;
            effectiveTo = request.EffectiveToUtc?.Date;
        }
        else if (type == AccountingBookType.ParallelFull)
        {
            if (request.EffectiveFromUtc.HasValue || request.EffectiveToUtc.HasValue)
                throw new InvalidOperationException("Parallel books use a replication cutoff, not lifecycle effective dates.");
            if (!request.BaseAccountingBookId.HasValue || request.BaseAccountingBookId == currentId)
                throw new InvalidOperationException("A Parallel book requires the tenant Primary book as its base.");
            baseBook = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(item =>
                item.Id == request.BaseAccountingBookId && item.TenantId == TenantId
                && item.BookType == AccountingBookType.PrimaryFull && item.IsDefault && !item.IsDeleted, ct)
                ?? throw new InvalidOperationException("A Parallel book must be based on the tenant Primary book.");
            var setting = await _db.FinanceSettings.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted)
                .Select(item => item.BaseCurrency).SingleOrDefaultAsync(ct);
            var tenantCurrency = await _db.Tenants.AsNoTracking().Where(item => item.Id == TenantId && !item.IsDeleted)
                .Select(item => item.BaseCurrency).SingleOrDefaultAsync(ct);
            currency = request.FunctionalCurrencyCode;
            // Match the SQL BIN2 constraints exactly. Unicode uppercasing and trimming would let
            // non-ASCII or non-canonical evidence pass in non-SQL providers and fail only later.
            if (!IsCanonicalCurrency(tenantCurrency)
                || !IsCanonicalCurrency(setting)
                || !IsCanonicalCurrency(currency))
                throw new InvalidOperationException(
                    "FUNCTIONAL_CURRENCY_INVALID: Tenant, Finance Settings, and requested full-book currencies must each be exactly three uppercase ASCII letters.");
            if (!string.Equals(tenantCurrency, setting, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "FUNCTIONAL_CURRENCY_MISMATCH: Tenant and Finance Settings base currencies must match exactly.");
            if (string.Equals(tenantCurrency, currency, StringComparison.Ordinal))
                throw new InvalidOperationException("A Parallel book must use a foreign currency different from the tenant Primary currency.");
            if (!request.ReplicationStartDate.HasValue)
                throw new InvalidOperationException("A Parallel replication start date is required.");
            if (!Enum.TryParse<ParallelBookOpeningMode>(request.ParallelOpeningMode, true, out var parsedOpeningMode))
                throw new InvalidOperationException("A valid Parallel opening mode is required.");
            openingMode = parsedOpeningMode;
            if (openingMode == ParallelBookOpeningMode.GovernedOpeningConversion)
            {
                if (!Enum.TryParse<ParallelBookTranslationMethod>(request.ParallelTranslationMethod, true, out var parsedTranslationMethod))
                    throw new InvalidOperationException("A governed Parallel opening conversion requires a translation method.");
                translationMethod = parsedTranslationMethod;
            }
            else if (!string.IsNullOrWhiteSpace(request.ParallelTranslationMethod))
                throw new InvalidOperationException("A translation method applies only to governed Parallel opening conversion.");
            replicationStart = request.ReplicationStartDate.Value.Date;
            translationReserveAccountId = request.CurrencyTranslationReserveAccountId;
            roundingAccountId = request.CurrencyRoundingAccountId;
        }
        else
        {
            if (request.BaseAccountingBookId.HasValue || request.EffectiveFromUtc.HasValue || request.EffectiveToUtc.HasValue
                || request.ReplicationStartDate.HasValue || !string.IsNullOrWhiteSpace(request.ParallelOpeningMode)
                || !string.IsNullOrWhiteSpace(request.ParallelTranslationMethod))
                throw new InvalidOperationException("The tenant Primary book is perpetual and cannot have base, effective-date, or replication settings.");
            var setting = await _db.FinanceSettings.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted)
                .Select(item => item.BaseCurrency).SingleOrDefaultAsync(ct);
            var tenantCurrency = await _db.Tenants.AsNoTracking().Where(item => item.Id == TenantId && !item.IsDeleted)
                .Select(item => item.BaseCurrency).SingleOrDefaultAsync(ct);
            currency = request.FunctionalCurrencyCode;
            if (!IsCanonicalCurrency(tenantCurrency) || !IsCanonicalCurrency(setting) || !IsCanonicalCurrency(currency)
                || !string.Equals(tenantCurrency, setting, StringComparison.Ordinal)
                || !string.Equals(tenantCurrency, currency, StringComparison.Ordinal))
                throw new InvalidOperationException("The Primary book currency must exactly match tenant and Finance Settings base currency authority.");
        }
        var otherPrimaryCount = await _db.AccountingBooks.CountAsync(item => item.TenantId == TenantId
            && item.BookType == AccountingBookType.PrimaryFull && item.IsDefault && item.Id != currentId && !item.IsDeleted, ct);
        if (type == AccountingBookType.PrimaryFull && otherPrimaryCount != 0)
            throw new InvalidOperationException("Exactly one primary/default full book is permitted per tenant.");
        if (type != AccountingBookType.PrimaryFull && otherPrimaryCount == 0)
            throw new InvalidOperationException("A non-primary book cannot be configured until the tenant has exactly one primary/default full book.");
        if (currentId.HasValue)
        {
            var currentIsPrimary = await _db.AccountingBooks.AsNoTracking().AnyAsync(item => item.Id == currentId && item.TenantId == TenantId
                && item.BookType == AccountingBookType.PrimaryFull && item.IsDefault && !item.IsDeleted, ct);
            if (currentIsPrimary && type != AccountingBookType.PrimaryFull)
                throw new InvalidOperationException("The tenant's primary/default book cannot be converted without a governed replacement workflow.");
        }
        return new ValidatedBookStructure(code, request.Name.Trim(), Optional(request.Description), request.Purpose.Trim(),
            type, currency, baseBook, effectiveFrom, effectiveTo, replicationStart, openingMode,
            translationMethod, translationReserveAccountId, roundingAccountId);
    }

    private sealed record ValidatedBookStructure(
        string Code,
        string Name,
        string? Description,
        string Purpose,
        AccountingBookType Type,
        string? FunctionalCurrency,
        AccountingBook? BaseBook,
        DateTime? EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        DateTime? ReplicationStartDate,
        ParallelBookOpeningMode? ParallelOpeningMode,
        ParallelBookTranslationMethod? ParallelTranslationMethod,
        Guid? CurrencyTranslationReserveAccountId,
        Guid? CurrencyRoundingAccountId);

    private static void ValidateTransition(AccountingBookLifecycleStatus current, AccountingBookLifecycleStatus target)
    {
        var valid = (current, target) switch
        {
            (AccountingBookLifecycleStatus.Draft, AccountingBookLifecycleStatus.Configuring) => true,
            (AccountingBookLifecycleStatus.Configuring, AccountingBookLifecycleStatus.Initializing) => true,
            (AccountingBookLifecycleStatus.Initializing, AccountingBookLifecycleStatus.Active) => true,
            (AccountingBookLifecycleStatus.Active, AccountingBookLifecycleStatus.Suspended) => true,
            (AccountingBookLifecycleStatus.Suspended, AccountingBookLifecycleStatus.Active) => true,
            (AccountingBookLifecycleStatus.Draft or AccountingBookLifecycleStatus.Configuring or AccountingBookLifecycleStatus.Suspended, AccountingBookLifecycleStatus.Retired) => true,
            _ => false
        };
        if (!valid) throw new InvalidOperationException($"Lifecycle transition from {current} to {target} is not permitted.");
    }

    private async Task ValidateGovernedTransitionAsync(
        AccountingBook book,
        AccountingBookLifecycleStatus target,
        CancellationToken ct)
    {
        ValidateTransition(book.LifecycleStatus, target);
        if (book.BookType == AccountingBookType.PrimaryFull)
            throw new InvalidOperationException("The tenant Primary book is perpetual and cannot be suspended, retired, or replaced.");
        if (target == AccountingBookLifecycleStatus.Active)
        {
            var readiness = await GetActivationReadinessAsync(book, ct);
            if (!readiness.IsReady)
                throw new InvalidOperationException($"ACCOUNTING_BOOK_ACTIVATION_NOT_READY: {string.Join(" ", readiness.Blockers)}");
        }

        // One tenant-scoped graph is used for both child advancement and ancestor invalidation.
        var tenantBooks = await ValidateFullDeltaLineageAsync(book, target, ct);
        ValidateDependentDeltaLineage(book, target, tenantBooks);
    }

    private async Task<IReadOnlyList<AccountingBook>> ValidateFullDeltaLineageAsync(
        AccountingBook candidate,
        AccountingBookLifecycleStatus target,
        CancellationToken ct)
    {
        // Every structural and lifecycle writer scans the same tenant graph inside AtomicAsync's
        // serializable transaction. Those range locks make child attachment and ancestor
        // invalidation mutually visible; weakening this query can reintroduce invalid Delta trees.
        List<AccountingBook> tenantBooks;
        if (_db.Database.IsSqlServer())
        {
            // Serializable shared range reads can deadlock during symmetric lock conversion and
            // do not guarantee that the first authority reader wins. UPDLOCK makes this tenant
            // graph the actual writer boundary; HOLDLOCK retains it through commit.
            tenantBooks = await _db.AccountingBooks
                .FromSqlInterpolated($"SELECT * FROM [AccountingBooks] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {TenantId} AND [IsDeleted] = CAST(0 AS bit)")
                .AsNoTracking()
                .ToListAsync(ct);
        }
        else
        {
            tenantBooks = await _db.AccountingBooks.AsNoTracking()
                .Where(item => item.TenantId == TenantId && !item.IsDeleted)
                .ToListAsync(ct);
        }
        var byId = tenantBooks.ToDictionary(item => item.Id);
        byId[candidate.Id] = candidate;
        ValidateOwnDeltaLineage(candidate, target, byId);
        return tenantBooks;
    }

    private static void ValidateOwnDeltaLineage(
        AccountingBook book,
        AccountingBookLifecycleStatus target,
        IReadOnlyDictionary<Guid, AccountingBook> byId)
    {
        if (book.BookType == AccountingBookType.PrimaryFull)
        {
            if (book.BaseAccountingBookId.HasValue)
                throw new InvalidOperationException("The tenant Primary book cannot have a base book.");
            return;
        }
        if (!book.BaseAccountingBookId.HasValue
            || !byId.TryGetValue(book.BaseAccountingBookId.Value, out var baseBook))
            throw new InvalidOperationException("Derived-book base lineage is missing, deleted, or belongs to another tenant.");
        if (baseBook.Id == book.Id)
            throw new InvalidOperationException("Accounting-book base cycles are prohibited.");
        if (book.BookType == AccountingBookType.ParallelFull
            && (baseBook.BookType != AccountingBookType.PrimaryFull || !baseBook.IsDefault))
            throw new InvalidOperationException("A Parallel book must be based directly on the tenant Primary book.");
        if (book.BookType == AccountingBookType.Delta
            && baseBook.BookType is not (AccountingBookType.PrimaryFull or AccountingBookType.ParallelFull))
            throw new InvalidOperationException("A Delta book must be based directly on a Primary or Parallel full book.");
        if (baseBook.LifecycleStatus is AccountingBookLifecycleStatus.Suspended or AccountingBookLifecycleStatus.Retired
            || baseBook.PendingLifecycleStatus is AccountingBookLifecycleStatus.Suspended or AccountingBookLifecycleStatus.Retired)
            throw new InvalidOperationException($"Base book {baseBook.Code} is not available to support this derived book.");
        if (!BaseStatusSupports(target, baseBook.LifecycleStatus))
            throw new InvalidOperationException($"Base book {baseBook.Code} in {baseBook.LifecycleStatus} cannot support a transition to {target}.");
    }

    private static bool BaseStatusSupports(AccountingBookLifecycleStatus childTarget, AccountingBookLifecycleStatus baseStatus) =>
        childTarget switch
        {
            AccountingBookLifecycleStatus.Configuring => baseStatus is AccountingBookLifecycleStatus.Configuring
                or AccountingBookLifecycleStatus.Initializing or AccountingBookLifecycleStatus.Active,
            AccountingBookLifecycleStatus.Initializing => baseStatus is AccountingBookLifecycleStatus.Initializing
                or AccountingBookLifecycleStatus.Active,
            AccountingBookLifecycleStatus.Active => baseStatus == AccountingBookLifecycleStatus.Active,
            _ => baseStatus != AccountingBookLifecycleStatus.Retired
        };

    private static void ValidateDependentDeltaLineage(
        AccountingBook book,
        AccountingBookLifecycleStatus target,
        IReadOnlyCollection<AccountingBook> tenantBooks)
    {
        if (target is not (AccountingBookLifecycleStatus.Suspended or AccountingBookLifecycleStatus.Retired)) return;
        var descendants = new HashSet<Guid>();
        var frontier = new Queue<Guid>();
        frontier.Enqueue(book.Id);
        while (frontier.Count > 0)
        {
            var baseId = frontier.Dequeue();
            foreach (var child in tenantBooks.Where(item => item.BookType == AccountingBookType.Delta
                         && item.BaseAccountingBookId == baseId && descendants.Add(item.Id)))
                frontier.Enqueue(child.Id);
        }

        // Suspension also makes a Draft/Configuring descendant's governed advancement impossible;
        // require every live descendant to retire or detach through its own controlled workflow first.
        var invalid = tenantBooks.Where(item => descendants.Contains(item.Id)
                && item.LifecycleStatus != AccountingBookLifecycleStatus.Retired)
            .OrderBy(item => item.Code)
            .Select(item => item.Code)
            .ToArray();
        if (invalid.Length > 0)
            throw new InvalidOperationException(
                $"Transition would invalidate dependent Delta book lineage: {string.Join(", ", invalid)}.");
    }

    private static void ApplyPostingFlags(AccountingBook book)
    { book.IsActive = book.LifecycleStatus == AccountingBookLifecycleStatus.Active; book.AllowsPosting = book.IsActive; }
    private static void ClearPending(AccountingBook book, bool preserveDecision = false)
    {
        book.PendingLifecycleStatus = null;
        book.PendingTransitionReason = null;
    }

    private IQueryable<AccountingBook> BookQuery() => _db.AccountingBooks.Include(item => item.BaseAccountingBook)
        .Where(item => item.TenantId == TenantId && !item.IsDeleted);
    private async Task<HashSet<Guid>> GetUsedBookIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var values = ids.ToArray();
        var journals = await _db.JournalEntries.AsNoTracking().Where(item => item.TenantId == TenantId && values.Contains(item.AccountingBookId) && !item.IsDeleted)
            .Select(item => item.AccountingBookId).Distinct().ToListAsync(ct);
        var events = await _db.FinancePostingEvents.AsNoTracking().Where(item => item.TenantId == TenantId && values.Contains(item.AccountingBookId) && !item.IsDeleted)
            .Select(item => item.AccountingBookId).Distinct().ToListAsync(ct);
        return journals.Concat(events).ToHashSet();
    }
    private async Task<bool> HasUseAsync(Guid id, CancellationToken ct) =>
        await _db.JournalEntries.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.AccountingBookId == id && !item.IsDeleted, ct)
        || await _db.FinancePostingEvents.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.AccountingBookId == id && !item.IsDeleted, ct);
    private async Task<AccountingBookDto> LoadDtoAsync(Guid id, CancellationToken ct)
    {
        var item = await BookQuery().AsNoTracking().SingleAsync(book => book.Id == id, ct);
        var reversible = await GetReversibleDesignationAsync(ct);
        var protectedAccountLabels = await GetProtectedAccountLabelsAsync(new[] { item }, ct);
        return Map(item, await HasUseAsync(id, ct), await GetActivationReadinessAsync(item, ct),
            reversible?.NewPrimaryBookId == id ? reversible : null, protectedAccountLabels);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetProtectedAccountLabelsAsync(
        IEnumerable<AccountingBook> books, CancellationToken ct)
    {
        var accountIds = books.SelectMany(item => new Guid?[]
            { item.CurrencyTranslationReserveAccountId, item.CurrencyRoundingAccountId })
            .Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        if (accountIds.Length == 0) return new Dictionary<Guid, string>();
        return await _db.Accounts.AsNoTracking().Where(item => item.TenantId == TenantId
                && accountIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, item => $"{item.AccountNumber} — {item.AccountName}", ct);
    }

    public async Task<DeltaBookLedgerInquiryDto> GetDeltaLedgerAsync(Guid id, DateTime? fromDate = null,
        DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var delta = await BookQuery().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Accounting book was not found.");
        if (delta.BookType != AccountingBookType.Delta
            || !delta.BaseAccountingBookId.HasValue || delta.BaseAccountingBook == null)
            throw new InvalidOperationException("A combined Delta ledger inquiry requires a Delta book with governed base-book authority.");
        if (delta.BaseAccountingBook.BookType == AccountingBookType.Delta)
            throw new InvalidOperationException("Nested Delta ledger inheritance is not supported.");
        if (fromDate.HasValue && toDate.HasValue && toDate.Value.Date < fromDate.Value.Date)
            throw new InvalidOperationException("Ledger inquiry end date cannot be before its start date.");

        var baseBook = delta.BaseAccountingBook;
        var bookIds = new[] { baseBook.Id, delta.Id };
        var query = _db.JournalEntries.AsNoTracking().Where(item => item.TenantId == TenantId
            && !item.IsDeleted && item.PostingStatus == "Posted" && bookIds.Contains(item.AccountingBookId));
        if (fromDate.HasValue)
            query = query.Where(item => item.EntryDate >= fromDate.Value.Date);
        if (toDate.HasValue)
        {
            var exclusiveEnd = toDate.Value.Date.AddDays(1);
            query = query.Where(item => item.EntryDate < exclusiveEnd);
        }

        var rows = await query.OrderByDescending(item => item.EntryDate)
            .ThenByDescending(item => item.PostingDate)
            .ThenByDescending(item => item.JournalEntryNumber)
            .Select(item => new DeltaBookLedgerEntryDto
            {
                JournalEntryId = item.Id,
                JournalEntryNumber = item.JournalEntryNumber,
                AccountingDate = item.EntryDate,
                PostedAtUtc = item.PostingDate,
                Description = item.Description,
                ReferenceNumber = item.ReferenceNumber,
                SourceBookCode = item.AccountingBookId == baseBook.Id ? baseBook.Code : delta.Code,
                Layer = item.AccountingBookId == baseBook.Id ? "Inherited" : "Adjustment",
                TotalDebit = item.TotalDebitAmount,
                TotalCredit = item.TotalCreditAmount
            })
            .ToListAsync(cancellationToken);

        return new DeltaBookLedgerInquiryDto
        {
            DeltaAccountingBookId = delta.Id,
            DeltaAccountingBookCode = delta.Code,
            BaseAccountingBookId = baseBook.Id,
            BaseAccountingBookCode = baseBook.Code,
            FunctionalCurrencyCode = baseBook.FunctionalCurrencyCode
                ?? await _db.Tenants.AsNoTracking().Where(item => item.Id == TenantId)
                    .Select(item => item.BaseCurrency).SingleAsync(cancellationToken),
            FromDate = fromDate?.Date,
            ToDate = toDate?.Date,
            Entries = rows
        };
    }

    public Task<DeltaBookCombinedReportDto> GetDeltaCombinedReportAsync(Guid id, DateTime asOfDate, CancellationToken cancellationToken = default) =>
        GetDeltaCombinedReportAsync(new[] { id }, asOfDate, cancellationToken);

    public async Task<DeltaBookCombinedReportDto> GetDeltaCombinedReportAsync(IReadOnlyCollection<Guid> ids, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        if (asOfDate == default) throw new InvalidOperationException("A report as-of date is required.");
        var selectedIds = ids.Where(item => item != Guid.Empty).Distinct().ToArray();
        if (selectedIds.Length == 0)
            throw new InvalidOperationException("Select at least one Delta adjustment layer.");
        var loadedDeltas = await BookQuery().AsNoTracking().Where(item => selectedIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var deltaById = loadedDeltas.ToDictionary(item => item.Id);
        var deltas = selectedIds.Where(deltaById.ContainsKey).Select(id => deltaById[id]).ToList();
        if (deltas.Count != selectedIds.Length)
            throw new KeyNotFoundException("One or more selected accounting books were not found.");
        if (deltas.Any(delta => delta.BookType != AccountingBookType.Delta
            || !delta.BaseAccountingBookId.HasValue || delta.BaseAccountingBook == null))
            throw new InvalidOperationException("A combined report accepts only Delta books with governed base-book authority.");
        var baseIds = deltas.Select(item => item.BaseAccountingBookId!.Value).Distinct().ToArray();
        if (baseIds.Length != 1)
            throw new InvalidOperationException("Selected Delta layers must share the same full base book and currency.");
        var baseBook = deltas[0].BaseAccountingBook!;
        if (baseBook.BookType == AccountingBookType.Delta)
            throw new InvalidOperationException("Nested Delta combined reporting is not yet supported; select a Delta book whose base is a full book.");
        var deltaIds = deltas.Select(item => item.Id).ToArray();
        var reportBookIds = deltaIds.Append(baseBook.Id).ToArray();

        var mappedAccountIds = await _db.AccountAccountingBooks.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted && item.IsEnabled
                && reportBookIds.Contains(item.AccountingBookId))
            .Select(item => item.AccountId).Distinct().ToListAsync(cancellationToken);
        var exclusiveEnd = asOfDate.Date.AddDays(1);
        var balanceRows = await _db.AccountTransactions.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted && item.PostingStatus == "Posted"
                && item.TransactionDate < exclusiveEnd
                && reportBookIds.Contains(item.AccountingBookId))
            .GroupBy(item => new { item.AccountingBookId, item.AccountId })
            .Select(group => new
            {
                group.Key.AccountingBookId,
                group.Key.AccountId,
                SignedBalance = group.Sum(item => item.DebitAmount - item.CreditAmount)
            })
            .ToListAsync(cancellationToken);
        var accountIds = mappedAccountIds.Concat(balanceRows.Select(item => item.AccountId)).Distinct().ToList();
        if (accountIds.Count == 0)
            throw new InvalidOperationException("The base and Delta books have no enabled account mappings or posted balances to report.");

        var accounts = await _db.Accounts.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted && accountIds.Contains(item.Id))
            .Select(item => new { item.Id, item.AccountNumber, item.AccountName, item.AccountType })
            .OrderBy(item => item.AccountNumber).ToListAsync(cancellationToken);
        var balances = balanceRows.ToDictionary(
            item => (item.AccountingBookId, item.AccountId), item => item.SignedBalance);
        var lines = accounts.Select(account =>
        {
            var baseBalance = balances.GetValueOrDefault((baseBook.Id, account.Id));
            var deltaBalance = deltaIds.Sum(deltaId => balances.GetValueOrDefault((deltaId, account.Id)));
            return new DeltaBookCombinedReportLineDto
            {
                AccountId = account.Id, AccountNumber = account.AccountNumber, AccountName = account.AccountName,
                AccountType = account.AccountType.ToString(), BaseSignedBalance = baseBalance,
                DeltaSignedBalance = deltaBalance, CombinedSignedBalance = baseBalance + deltaBalance
            };
        }).ToList();
        var currency = baseBook.FunctionalCurrencyCode
            ?? await _db.Tenants.AsNoTracking().Where(item => item.Id == TenantId).Select(item => item.BaseCurrency).SingleAsync(cancellationToken);
        return new DeltaBookCombinedReportDto
        {
            DeltaAccountingBookId = deltas.Count == 1 ? deltas[0].Id : Guid.Empty,
            DeltaAccountingBookCode = string.Join(" + ", deltas.Select(item => item.Code)),
            DeltaAccountingBookIds = deltaIds,
            DeltaAccountingBookCodes = deltas.Select(item => item.Code).ToArray(),
            BaseAccountingBookId = baseBook.Id, BaseAccountingBookCode = baseBook.Code,
            FunctionalCurrencyCode = currency, AsOfDate = asOfDate.Date,
            BaseTotal = lines.Sum(item => item.BaseSignedBalance),
            DeltaTotal = lines.Sum(item => item.DeltaSignedBalance),
            CombinedTotal = lines.Sum(item => item.CombinedSignedBalance), Lines = lines
        };
    }
    private static AccountingBookDto Map(AccountingBook book, bool used, AccountingBookActivationReadinessDto readiness,
        AccountingBookPrimaryDesignation? reversible = null,
        IReadOnlyDictionary<Guid, string>? protectedAccountLabels = null) => new()
    {
        Id = book.Id, TenantId = book.TenantId, Code = book.Code, Name = book.Name, Description = book.Description,
        Purpose = book.Purpose, BookType = book.BookType.ToString(), LifecycleStatus = book.LifecycleStatus.ToString(),
        FunctionalCurrencyCode = book.FunctionalCurrencyCode, EffectiveFromUtc = book.EffectiveFromUtc, EffectiveToUtc = book.EffectiveToUtc,
        BaseAccountingBookId = book.BaseAccountingBookId, BaseAccountingBookCode = book.BaseAccountingBook?.Code,
        ReplicationStartDate = book.ReplicationStartDate,
        ParallelOpeningMode = book.ParallelOpeningMode?.ToString(),
        ParallelTranslationMethod = book.ParallelTranslationMethod?.ToString(),
        CurrencyTranslationReserveAccountId = book.CurrencyTranslationReserveAccountId,
        CurrencyTranslationReserveAccountLabel = book.CurrencyTranslationReserveAccountId.HasValue
            ? protectedAccountLabels?.GetValueOrDefault(book.CurrencyTranslationReserveAccountId.Value) : null,
        CurrencyRoundingAccountId = book.CurrencyRoundingAccountId,
        CurrencyRoundingAccountLabel = book.CurrencyRoundingAccountId.HasValue
            ? protectedAccountLabels?.GetValueOrDefault(book.CurrencyRoundingAccountId.Value) : null,
        InitializationStartedAtUtc = book.InitializationStartedAtUtc, IsActive = book.IsActive, IsDefault = book.IsDefault,
        AllowsPosting = book.AllowsPosting, IsSystemDefined = book.IsSystemDefined, SortOrder = book.SortOrder,
        PendingLifecycleStatus = book.PendingLifecycleStatus?.ToString(), PendingTransitionReason = book.PendingTransitionReason,
        TransitionRequestedByUserId = book.TransitionRequestedByUserId, TransitionRequestedAtUtc = book.TransitionRequestedAtUtc,
        TransitionWorkflowInstanceId = book.TransitionWorkflowInstanceId, HasAccountingUse = used, ActivationReady = readiness.IsReady,
        PrimaryReplacementFromBookId = book.PrimaryReplacementFromBookId,
        PrimaryReplacementEffectiveDate = book.PrimaryReplacementEffectiveDate,
        PrimaryReplacementReason = book.PrimaryReplacementReason,
        PrimaryReplacementRequestedByUserId = book.PrimaryReplacementRequestedByUserId,
        PrimaryReplacementRequestedAtUtc = book.PrimaryReplacementRequestedAtUtc,
        PrimaryReplacementWorkflowInstanceId = book.PrimaryReplacementWorkflowInstanceId,
        ReversiblePrimaryDesignationId = reversible?.Id,
        ReversiblePrimaryDesignationPreviousBookId = reversible?.PreviousPrimaryBookId,
        ReversiblePrimaryDesignationEffectiveDate = reversible?.EffectiveFrom,
        PrimaryReversalReason = reversible?.ReversalReason,
        PrimaryReversalRequestedByUserId = reversible?.ReversalRequestedByUserId,
        PrimaryReversalRequestedAtUtc = reversible?.ReversalRequestedAtUtc,
        PrimaryReversalWorkflowInstanceId = reversible?.ReversalWorkflowInstanceId,
        ReadinessMessage = readiness.IsReady ? null : string.Join(" ", readiness.Blockers),
        RowVersion = book.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(book.RowVersion)
    };
    private Task<AccountingBookPrimaryDesignation?> GetReversibleDesignationAsync(CancellationToken ct) =>
        _db.AccountingBookPrimaryDesignations.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted && item.ReversedAtUtc == null
                && item.EffectiveFrom == DateTime.UtcNow.Date)
            .OrderByDescending(item => item.ApprovedAtUtc).FirstOrDefaultAsync(ct);
    private Task<bool> HasPendingPrimaryReversalAsync(CancellationToken ct) =>
        _db.AccountingBookPrimaryDesignations.AnyAsync(item => item.TenantId == TenantId && !item.IsDeleted
            && item.ReversalRequestedAtUtc != null && item.ReversedAtUtc == null, ct);
    private async Task<AccountingBookActivationReadinessDto> GetActivationReadinessAsync(AccountingBook book, CancellationToken ct)
    {
        // Lifecycle request and approval both call this method inside their serializable boundary.
        // The initialization service re-derives financial, source-book, fiscal/module and exact-book
        // readiness rather than trusting a previously displayed readiness flag.
        var authority = _initialization ?? new AccountingBookInitializationService(_db, _currentUser, _workflow!, _audit!);
        return await authority.GetReadinessAsync(book.Id, ct);
    }
    private static object Snapshot(AccountingBook item) => new
    {
        item.Code, item.Name, item.Description, item.Purpose, BookType = item.BookType.ToString(), LifecycleStatus = item.LifecycleStatus.ToString(),
        item.FunctionalCurrencyCode, item.EffectiveFromUtc, item.EffectiveToUtc, item.BaseAccountingBookId,
        item.ReplicationStartDate, ParallelOpeningMode = item.ParallelOpeningMode?.ToString(),
        ParallelTranslationMethod = item.ParallelTranslationMethod?.ToString(),
        item.CurrencyTranslationReserveAccountId, item.CurrencyRoundingAccountId, item.IsDefault,
        item.IsActive, item.AllowsPosting, item.SortOrder, item.InitializationStartedAtUtc,
        PendingLifecycleStatus = item.PendingLifecycleStatus?.ToString(), item.PendingTransitionReason,
        item.TransitionRequestedByUserId, item.TransitionRequestedAtUtc, item.TransitionWorkflowInstanceId,
        item.PrimaryReplacementFromBookId, item.PrimaryReplacementEffectiveDate, item.PrimaryReplacementReason,
        item.PrimaryReplacementRequestedByUserId, item.PrimaryReplacementRequestedAtUtc, item.PrimaryReplacementWorkflowInstanceId,
        item.TransitionDecidedByUserId, item.TransitionDecidedAtUtc, item.TransitionDecisionReason
    };
    private async Task AuditAsync(string type, AccountingBook book, object? before, object after, string reason, CancellationToken ct,
        Guid? workflowInstanceId = null) =>
        await (_audit ?? throw new InvalidOperationException("Finance audit service is required for accounting-book mutation.")).RecordAsync(new FinanceAuditEventDto { TenantId = TenantId, EventType = type, SourceModule = "GL",
            SourceDocumentType = WorkflowEntityType, SourceDocumentId = book.Id,
            WorkflowInstanceId = workflowInstanceId ?? book.TransitionWorkflowInstanceId ?? book.PrimaryReplacementWorkflowInstanceId,
            Resource = "Finance.AccountingBook", ResourceId = book.Id.ToString(), BeforeValues = before, AfterValues = after, Reason = reason.Trim() }, ct);
    private void ApplyRowVersion(AccountingBook book, string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("Row version is required.");
        byte[] value; try { value = Convert.FromBase64String(encoded); } catch (FormatException) { throw new InvalidOperationException("Row version is invalid."); }
        if (value.Length == 0 || (book.RowVersion.Length > 0 && !book.RowVersion.SequenceEqual(value))) throw new DbUpdateConcurrencyException("The accounting book changed after it was loaded. Reload and retry.");
        _db.Entry(book).Property(item => item.RowVersion).OriginalValue = value;
    }
    private Task<T> AtomicAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return TrackedAsync(action);
        return _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try { var result = await action(); await transaction.CommitAsync(ct); return result; }
            catch { await transaction.RollbackAsync(ct); _db.ChangeTracker.Clear(); throw; }
        });
    }
    private async Task<T> TrackedAsync<T>(Func<Task<T>> action) { try { return await action(); } catch { _db.ChangeTracker.Clear(); throw; } }
    private Guid RequiredActor() => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated Finance user is required.");
    private IWorkflowService Workflow() => _workflow ?? throw new InvalidOperationException("Finance workflow service is required for accounting-book lifecycle transitions.");
    private string ActorName() => _currentUser.UserName ?? "system";
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsCanonicalCurrency(string? value) => value is { Length: 3 }
        && value.All(character => character is >= 'A' and <= 'Z');

    public async Task SyncAccountMappingsAsync(Account account, IReadOnlyCollection<AccountAccountingBookUpdateDto> requestedMappings, CancellationToken cancellationToken = default)
    {
        if (!_db.Database.IsRelational()) { await SyncMappingsCoreAsync(account, requestedMappings, cancellationToken); return; }
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try { await SyncMappingsCoreAsync(account, requestedMappings, cancellationToken); await transaction.CommitAsync(cancellationToken); }
            catch { await transaction.RollbackAsync(cancellationToken); throw; }
        });
    }

    private async Task SyncMappingsCoreAsync(Account account, IReadOnlyCollection<AccountAccountingBookUpdateDto> requests, CancellationToken ct)
    {
        if (requests == null || requests.Count == 0 || !requests.Any(item => item.IsEnabled))
            throw new InvalidOperationException("At least one enabled accounting-book assignment is required.");
        var tenantId = account.TenantId;
        var books = await _db.AccountingBooks.Where(item => item.TenantId == tenantId && !item.IsDeleted).ToListAsync(ct);
        var existing = await _db.AccountAccountingBooks.Where(item => item.TenantId == tenantId && item.AccountId == account.Id && !item.IsDeleted).ToListAsync(ct);
        var before = existing.Select(MappingSnapshot).ToList();
        var classifications = await _db.AccountClassifications.Where(item => item.TenantId == tenantId && !item.IsDeleted).ToListAsync(ct);
        var parentIds = classifications.Where(item => item.ParentClassificationId.HasValue).Select(item => item.ParentClassificationId!.Value).ToHashSet();
        var resolved = new List<(AccountAccountingBookUpdateDto Request, AccountingBook Book, AccountClassification? Classification)>();
        foreach (var request in requests)
        {
            var book = request.AccountingBookId.HasValue ? books.SingleOrDefault(item => item.Id == request.AccountingBookId)
                : books.SingleOrDefault(item => string.Equals(item.Code, request.AccountingBookCode?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (book == null || request.IsEnabled && (!book.IsActive || !book.AllowsPosting))
                throw new InvalidOperationException("An accounting-book assignment is invalid or inactive for this tenant.");
            var classification = request.AccountClassificationId.HasValue ? classifications.SingleOrDefault(item => item.Id == request.AccountClassificationId) : null;
            if (request.IsEnabled && (classification == null || classification.AccountingBookId != book.Id
                || classification.Status != AccountClassificationStatus.Active || !classification.IsPostingClassification
                || parentIds.Contains(classification.Id) || classification.CoreAccountType != account.AccountType))
                throw new InvalidOperationException("Each enabled accounting-book assignment requires a compatible active posting classification.");
            resolved.Add((request, book, classification));
        }
        if (resolved.Select(item => item.Book.Id).Distinct().Count() != resolved.Count) throw new InvalidOperationException("Accounting-book assignments must be unique.");
        var requestedIds = resolved.Select(item => item.Book.Id).ToHashSet();
        if (existing.Any(item => !requestedIds.Contains(item.AccountingBookId))) throw new InvalidOperationException("Every existing account-book assignment must be included with its row version.");
        var now = DateTime.UtcNow;
        foreach (var item in resolved)
        {
            var mapping = existing.SingleOrDefault(value => value.AccountingBookId == item.Book.Id);
            if (mapping == null)
            {
                mapping = new AccountAccountingBook { TenantId = tenantId, AccountId = account.Id, AccountingBookId = item.Book.Id, CreatedAt = now, CreatedBy = ActorName() };
                _db.AccountAccountingBooks.Add(mapping);
            }
            else ApplyMappingRowVersion(mapping, item.Request.RowVersion);
            mapping.IsEnabled = item.Request.IsEnabled; mapping.AccountClassificationId = item.Classification?.Id;
            mapping.FinancialStatementLineItem = item.Request.FinancialStatementLineItem; mapping.UpdatedAt = now; mapping.UpdatedBy = ActorName();
        }
        await _db.SaveChangesAsync(ct);
        if (_audit != null)
        {
            var after = await _db.AccountAccountingBooks.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.AccountId == account.Id && !item.IsDeleted)
                .OrderBy(item => item.AccountingBookId)
                .Select(item => new
                {
                    item.Id,
                    item.AccountingBookId,
                    item.AccountClassificationId,
                    item.IsEnabled,
                    item.FinancialStatementLineItem
                })
                .ToListAsync(ct);
            await _audit.RecordAsync(new FinanceAuditEventDto
            {
                TenantId = tenantId,
                EventType = FinanceAuditEvents.AccountBookMappingsChanged,
                SourceModule = "GL",
                SourceDocumentType = "Account",
                SourceDocumentId = account.Id,
                Resource = "Finance.Account.BookMappings",
                ResourceId = account.Id.ToString(),
                BeforeValues = before,
                AfterValues = after
            }, ct);
        }
    }

    private static object MappingSnapshot(AccountAccountingBook item) => new
    {
        item.Id,
        item.AccountingBookId,
        item.AccountClassificationId,
        item.IsEnabled,
        item.FinancialStatementLineItem
    };

    private void ApplyMappingRowVersion(AccountAccountingBook mapping, string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("Row version is required for an existing account-book assignment.");
        byte[] value; try { value = Convert.FromBase64String(encoded); } catch (FormatException) { throw new InvalidOperationException("Account-book assignment row version is invalid."); }
        if (value.Length == 0 || mapping.RowVersion.Length > 0 && !mapping.RowVersion.SequenceEqual(value)) throw new DbUpdateConcurrencyException("The account-book assignment changed after it was loaded. Reload the account and retry.");
        _db.Entry(mapping).Property(item => item.RowVersion).OriginalValue = value;
    }
}
