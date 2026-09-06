using System.Data;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Settings;

public sealed class AccountingBookService : IAccountingBookService
{
    public const string IfrsCode = "IFRS";
    public const string LocalStatutoryCode = "LOCAL_STATUTORY";
    public const string ManagementCode = "MANAGEMENT";
    public const string WorkflowEntityType = "AccountingBookLifecycle";
    private static readonly HashSet<string> PseudoCodes = new(StringComparer.Ordinal)
    {
        "ALL", "ALL_ACTIVE_BOOKS", "ALL_CLASSIFIED_BOOKS", "ALLCLASSIFIEDBOOKS"
    };
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowService? _workflow;
    private readonly IFinanceAuditService? _audit;

    public AccountingBookService(ApplicationDbContext db, ICurrentUserService currentUser, IWorkflowService workflow, IFinanceAuditService audit)
    { _db = db; _currentUser = currentUser; _workflow = workflow; _audit = audit; }
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
        return books.Select(item => Map(item, used.Contains(item.Id))).ToList();
    }

    public async Task<AccountingBookDto> GetBookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var book = await BookQuery().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Accounting book was not found.");
        return Map(book, await HasUseAsync(id, cancellationToken));
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

    private async Task<AccountingBookDto> CreateCoreAsync(CreateAccountingBookDto request, CancellationToken ct)
    {
        var value = await ValidateStructureAsync(request, null, ct);
        var now = DateTime.UtcNow;
        var book = new AccountingBook
        {
            TenantId = TenantId, Code = value.Code, Name = value.Name, Description = value.Description,
            Purpose = value.Purpose, BookType = value.Type, LifecycleStatus = AccountingBookLifecycleStatus.Draft,
            FunctionalCurrencyCode = value.FunctionalCurrency, EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc, BaseAccountingBookId = value.BaseBook?.Id,
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
        if (book.PendingLifecycleStatus.HasValue) throw new InvalidOperationException("The accounting book has a pending lifecycle transition.");
        if (book.LifecycleStatus == AccountingBookLifecycleStatus.Retired) throw new InvalidOperationException("A retired accounting book cannot be edited.");
        var value = await ValidateStructureAsync(request, id, ct);
        var structuralChange = book.Code != value.Code || book.Purpose != value.Purpose || book.BookType != value.Type
            || book.FunctionalCurrencyCode != value.FunctionalCurrency || book.BaseAccountingBookId != value.BaseBook?.Id
            || book.EffectiveFromUtc != request.EffectiveFromUtc || book.EffectiveToUtc != request.EffectiveToUtc;
        if (structuralChange && (await HasUseAsync(id, ct) || book.InitializationStartedAtUtc.HasValue
            || book.LifecycleStatus is AccountingBookLifecycleStatus.Initializing or AccountingBookLifecycleStatus.Active
                or AccountingBookLifecycleStatus.Suspended or AccountingBookLifecycleStatus.Retired))
            throw new InvalidOperationException("Accounting-book structural identity is immutable after accounting use or initialization begins.");
        var before = Snapshot(book);
        book.Code = value.Code; book.Name = value.Name; book.Description = value.Description; book.Purpose = value.Purpose;
        book.BookType = value.Type; book.FunctionalCurrencyCode = value.FunctionalCurrency;
        book.EffectiveFromUtc = request.EffectiveFromUtc; book.EffectiveToUtc = request.EffectiveToUtc;
        book.BaseAccountingBookId = value.BaseBook?.Id; book.IsDefault = value.Type == AccountingBookType.PrimaryFull;
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
        ApplyRowVersion(book, request.RowVersion);
        if (book.PendingLifecycleStatus.HasValue) throw new InvalidOperationException("The accounting book already has a pending lifecycle transition.");
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
            if (target == AccountingBookLifecycleStatus.Active) throw new InvalidOperationException("ACCOUNTING_BOOK_ACTIVATION_NOT_READY: C4 initialization and book-period readiness evidence is required before activation.");
            book.LifecycleStatus = target;
            if (target == AccountingBookLifecycleStatus.Initializing) book.InitializationStartedAtUtc ??= DateTime.UtcNow;
            ApplyPostingFlags(book); ClearPending(book, true);
        }
        book.UpdatedAt = DateTime.UtcNow; book.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(ct);
        var eventType = action == "Reject" ? FinanceAuditEvents.AccountingBookTransitionRejected
            : completed ? FinanceAuditEvents.AccountingBookTransitionApproved : FinanceAuditEvents.AccountingBookTransitionApprovalStepCompleted;
        await AuditAsync(eventType, book, before, Snapshot(book), request.Reason, ct);
        return await LoadDtoAsync(book.Id, ct);
    }, ct);

    private async Task<(string Code, string Name, string? Description, string Purpose, AccountingBookType Type, string? FunctionalCurrency, AccountingBook? BaseBook)> ValidateStructureAsync(CreateAccountingBookDto request, Guid? currentId, CancellationToken ct)
    {
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
        if (type == AccountingBookType.Delta)
        {
            if (!request.BaseAccountingBookId.HasValue || request.BaseAccountingBookId == currentId)
                throw new InvalidOperationException("A Delta book requires a different same-tenant base book.");
            baseBook = await _db.AccountingBooks.SingleOrDefaultAsync(item => item.Id == request.BaseAccountingBookId && item.TenantId == TenantId
                && !item.IsDeleted && item.LifecycleStatus != AccountingBookLifecycleStatus.Retired, ct)
                ?? throw new InvalidOperationException("Delta base book is invalid for this tenant.");
            await EnsureAcyclicAsync(baseBook, currentId, ct);
            currency = null;
        }
        else
        {
            if (request.BaseAccountingBookId.HasValue) throw new InvalidOperationException("A full accounting book cannot have a base book.");
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
            if (!string.Equals(tenantCurrency, setting, StringComparison.Ordinal)
                || !string.Equals(tenantCurrency, currency, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "FUNCTIONAL_CURRENCY_MISMATCH: Tenant, Finance Settings, and requested full-book currencies must match exactly.");
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
        return (code, request.Name.Trim(), Optional(request.Description), request.Purpose.Trim(), type, currency, baseBook);
    }

    private async Task EnsureAcyclicAsync(AccountingBook baseBook, Guid? currentId, CancellationToken ct)
    {
        var seen = new HashSet<Guid>();
        AccountingBook? cursor = baseBook;
        while (cursor != null)
        {
            if (!seen.Add(cursor.Id) || currentId == cursor.Id) throw new InvalidOperationException("Accounting-book base cycles are prohibited.");
            if (!cursor.BaseAccountingBookId.HasValue) return;
            cursor = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(item => item.Id == cursor.BaseAccountingBookId
                && item.TenantId == TenantId && !item.IsDeleted, ct)
                ?? throw new InvalidOperationException("Accounting-book base lineage is invalid for this tenant.");
        }
    }

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
        if (target == AccountingBookLifecycleStatus.Retired
            && book.BookType == AccountingBookType.PrimaryFull
            && book.IsDefault)
            throw new InvalidOperationException("The tenant's primary/default book cannot be retired without a governed replacement workflow.");
        if (target == AccountingBookLifecycleStatus.Active)
            throw new InvalidOperationException("ACCOUNTING_BOOK_ACTIVATION_NOT_READY: C4 initialization and book-period readiness evidence is required before activation.");

        // One tenant-scoped graph is used for both child advancement and ancestor invalidation.
        // A direct-only query is insufficient because Delta books may legitimately base on another
        // Delta, and a stale intermediate lifecycle would otherwise invalidate descendants silently.
        var tenantBooks = await _db.AccountingBooks.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted)
            .ToListAsync(ct);
        var byId = tenantBooks.ToDictionary(item => item.Id);
        ValidateOwnDeltaLineage(book, target, byId);
        ValidateDependentDeltaLineage(book, target, tenantBooks);
    }

    private static void ValidateOwnDeltaLineage(
        AccountingBook book,
        AccountingBookLifecycleStatus target,
        IReadOnlyDictionary<Guid, AccountingBook> byId)
    {
        if (book.BookType != AccountingBookType.Delta)
        {
            if (book.BaseAccountingBookId.HasValue)
                throw new InvalidOperationException("A full accounting book cannot retain Delta base-book lineage.");
            return;
        }

        var seen = new HashSet<Guid> { book.Id };
        var current = book;
        while (current.BookType == AccountingBookType.Delta)
        {
            if (!current.BaseAccountingBookId.HasValue
                || !byId.TryGetValue(current.BaseAccountingBookId.Value, out var baseBook))
                throw new InvalidOperationException("Delta base-book lineage is missing, retired, deleted or belongs to another tenant.");
            if (!seen.Add(baseBook.Id))
                throw new InvalidOperationException("Accounting-book base cycles are prohibited.");
            if (baseBook.LifecycleStatus == AccountingBookLifecycleStatus.Retired)
                throw new InvalidOperationException("A Delta book cannot transition while any base-book ancestor is retired.");
            if (baseBook.PendingLifecycleStatus is AccountingBookLifecycleStatus.Suspended or AccountingBookLifecycleStatus.Retired)
                throw new InvalidOperationException("A Delta book cannot advance while a base-book ancestor has a pending suspension or retirement.");
            if (!BaseStatusSupports(target, baseBook.LifecycleStatus))
                throw new InvalidOperationException($"Base book {baseBook.Code} in {baseBook.LifecycleStatus} cannot support a Delta transition to {target}.");
            current = baseBook;
        }
        if (current.BaseAccountingBookId.HasValue)
            throw new InvalidOperationException("A full accounting book cannot retain Delta base-book lineage.");
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
        return Map(item, await HasUseAsync(id, ct));
    }
    private static AccountingBookDto Map(AccountingBook book, bool used) => new()
    {
        Id = book.Id, TenantId = book.TenantId, Code = book.Code, Name = book.Name, Description = book.Description,
        Purpose = book.Purpose, BookType = book.BookType.ToString(), LifecycleStatus = book.LifecycleStatus.ToString(),
        FunctionalCurrencyCode = book.FunctionalCurrencyCode, EffectiveFromUtc = book.EffectiveFromUtc, EffectiveToUtc = book.EffectiveToUtc,
        BaseAccountingBookId = book.BaseAccountingBookId, BaseAccountingBookCode = book.BaseAccountingBook?.Code,
        InitializationStartedAtUtc = book.InitializationStartedAtUtc, IsActive = book.IsActive, IsDefault = book.IsDefault,
        AllowsPosting = book.AllowsPosting, IsSystemDefined = book.IsSystemDefined, SortOrder = book.SortOrder,
        PendingLifecycleStatus = book.PendingLifecycleStatus?.ToString(), PendingTransitionReason = book.PendingTransitionReason,
        TransitionRequestedByUserId = book.TransitionRequestedByUserId, TransitionRequestedAtUtc = book.TransitionRequestedAtUtc,
        TransitionWorkflowInstanceId = book.TransitionWorkflowInstanceId, HasAccountingUse = used, ActivationReady = false,
        ReadinessMessage = "C4 initialization and book-period readiness are not yet available; activation is disabled.",
        RowVersion = book.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(book.RowVersion)
    };
    private static object Snapshot(AccountingBook item) => new
    {
        item.Code, item.Name, item.Description, item.Purpose, BookType = item.BookType.ToString(), LifecycleStatus = item.LifecycleStatus.ToString(),
        item.FunctionalCurrencyCode, item.EffectiveFromUtc, item.EffectiveToUtc, item.BaseAccountingBookId, item.IsDefault,
        item.IsActive, item.AllowsPosting, item.SortOrder, item.InitializationStartedAtUtc,
        PendingLifecycleStatus = item.PendingLifecycleStatus?.ToString(), item.PendingTransitionReason,
        item.TransitionRequestedByUserId, item.TransitionRequestedAtUtc, item.TransitionWorkflowInstanceId,
        item.TransitionDecidedByUserId, item.TransitionDecidedAtUtc, item.TransitionDecisionReason
    };
    private async Task AuditAsync(string type, AccountingBook book, object? before, object after, string reason, CancellationToken ct) =>
        await (_audit ?? throw new InvalidOperationException("Finance audit service is required for accounting-book mutation.")).RecordAsync(new FinanceAuditEventDto { TenantId = TenantId, EventType = type, SourceModule = "GL",
            SourceDocumentType = WorkflowEntityType, SourceDocumentId = book.Id, WorkflowInstanceId = book.TransitionWorkflowInstanceId,
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
        var classifications = await _db.AccountClassifications.Where(item => item.TenantId == tenantId && !item.IsDeleted).ToListAsync(ct);
        var parentIds = classifications.Where(item => item.ParentClassificationId.HasValue).Select(item => item.ParentClassificationId!.Value).ToHashSet();
        var resolved = new List<(AccountAccountingBookUpdateDto Request, AccountingBook Book, AccountClassification? Classification)>();
        foreach (var request in requests)
        {
            var book = request.AccountingBookId.HasValue ? books.SingleOrDefault(item => item.Id == request.AccountingBookId)
                : books.SingleOrDefault(item => string.Equals(item.Code, request.AccountingBookCode?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (book == null || !book.IsActive || request.IsEnabled && !book.AllowsPosting)
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
    }

    private void ApplyMappingRowVersion(AccountAccountingBook mapping, string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("Row version is required for an existing account-book assignment.");
        byte[] value; try { value = Convert.FromBase64String(encoded); } catch (FormatException) { throw new InvalidOperationException("Account-book assignment row version is invalid."); }
        if (value.Length == 0 || mapping.RowVersion.Length > 0 && !mapping.RowVersion.SequenceEqual(value)) throw new DbUpdateConcurrencyException("The account-book assignment changed after it was loaded. Reload the account and retry.");
        _db.Entry(mapping).Property(item => item.RowVersion).OriginalValue = value;
    }
}
