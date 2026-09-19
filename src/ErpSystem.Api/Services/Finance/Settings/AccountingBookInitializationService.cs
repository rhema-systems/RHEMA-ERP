using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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

public sealed class AccountingBookInitializationService : IAccountingBookInitializationService
{
    public const string WorkflowEntityType = "AccountingBookInitialization";
    private static readonly HashSet<string> PseudoBookCodes = new(StringComparer.Ordinal)
        { "ALL", "ALL_ACTIVE_BOOKS", "ALL_CLASSIFIED_BOOKS", "ALLCLASSIFIEDBOOKS" };
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowService _workflow;
    private readonly IFinanceAuditService _audit;
    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public AccountingBookInitializationService(ApplicationDbContext db, ICurrentUserService currentUser, IWorkflowService workflow, IFinanceAuditService audit)
        => (_db, _currentUser, _workflow, _audit) = (db, currentUser, workflow, audit);

    public async Task<AccountingBookInitializationDto?> GetAsync(Guid accountingBookId, CancellationToken cancellationToken = default)
    {
        await RequireBookAsync(accountingBookId, cancellationToken);
        var entity = await Query().AsNoTracking().Where(item => item.AccountingBookId == accountingBookId)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
        return entity == null ? null : await MapAsync(entity, cancellationToken);
    }

    public async Task<AccountingBookInitializationPreparationDto> PrepareAsync(Guid accountingBookId, string mode, DateTime cutoffDate,
        Guid? sourceAccountingBookId, CancellationToken cancellationToken = default)
    {
        var book = await RequireBookAsync(accountingBookId, cancellationToken);
        if (!Enum.TryParse<AccountingBookInitializationMode>(mode, true, out var parsedMode)) throw new InvalidOperationException("Initialization mode is invalid.");
        if (cutoffDate == default) throw new InvalidOperationException("An initialization cutoff date is required.");
        var cutoffPeriods = (await _db.FiscalPeriods.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted).ToListAsync(cancellationToken))
            .Where(item => item.EndDate.Date == cutoffDate.Date).ToList();
        if (cutoffPeriods.Count != 1) throw new InvalidOperationException("INITIALIZATION_CUTOFF_PERIOD_INVALID: Cutoff must be the end date of exactly one live same-tenant fiscal period.");
        var cutoffPeriod = cutoffPeriods[0];
        var source = await ValidateSourceAsync(book, parsedMode, sourceAccountingBookId, cancellationToken);
        var authorityBook = source ?? book;
        var tenantCurrency = await _db.Tenants.AsNoTracking().Where(item => item.Id == TenantId && !item.IsDeleted).Select(item => item.BaseCurrency).SingleOrDefaultAsync(cancellationToken);
        if (tenantCurrency is not { Length: 3 } || tenantCurrency.Any(ch => ch is < 'A' or > 'Z')) throw new InvalidOperationException("Canonical tenant functional-currency authority is required.");
        var mappings = await _db.AccountAccountingBooks.AsNoTracking().Include(item => item.Account).Include(item => item.AccountClassification)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        mappings = mappings.Where(item => item.IsEnabled || !book.IsActive && FinanceClassificationManifestSeeder.IsUntouchedManifestOwnedMapping(item))
            .OrderBy(item => item.Account.AccountNumber).ToList();
        if (mappings.Any(item => item.Account == null || item.Account.IsDeleted || item.Account.TenantId != TenantId
            || item.AccountClassification == null || item.AccountClassification.IsDeleted || item.AccountClassification.Status != AccountClassificationStatus.Active
            || !item.AccountClassification.IsPostingClassification || item.AccountClassification.TenantId != TenantId
            || item.AccountClassification.AccountingBookId != book.Id || item.AccountClassification.CoreAccountType != item.Account.AccountType))
            throw new InvalidOperationException("Initialization contains an invalid mapped account or classification.");
        var balances = await _db.AccountBalances.AsNoTracking().Include(item => item.FiscalPeriod)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == authorityBook.Id && !item.IsDeleted && item.FiscalPeriod.EndDate <= cutoffDate.Date)
            .ToListAsync(cancellationToken);
        var latest = balances.GroupBy(item => item.AccountId).ToDictionary(group => group.Key,
            group => group.OrderByDescending(item => item.FiscalPeriod.EndDate).ThenByDescending(item => item.FiscalPeriod.PeriodNumber).First().ClosingBalance);
        return new AccountingBookInitializationPreparationDto { AccountingBookId = book.Id, AccountingBookCode = book.Code, Mode = parsedMode.ToString(),
            CutoffDate = cutoffDate.Date, CutoffFiscalPeriodId = cutoffPeriod.Id, CutoffFiscalPeriodCode = cutoffPeriod.PeriodCode,
            SourceAccountingBookId = source?.Id, SourceAccountingBookCode = source?.Code, FunctionalCurrencyCode = tenantCurrency,
            Accounts = mappings.Select(item => new AccountingBookInitializationPreparationLineDto { AccountId = item.AccountId,
                AccountNumber = item.Account.AccountNumber, AccountName = item.Account.AccountName, AccountClassificationId = item.AccountClassificationId!.Value,
                AccountClassificationCode = item.AccountClassification!.Code, AuthoritativeSignedBalance = latest.GetValueOrDefault(item.AccountId) }).ToList() };
    }

    public Task<AccountingBookInitializationDto> ConfigureAsync(Guid accountingBookId, ConfigureAccountingBookInitializationDto request, CancellationToken cancellationToken = default) =>
        AtomicAsync(async () =>
        {
            var book = await RequireBookAsync(accountingBookId, cancellationToken);
            if (book.LifecycleStatus is not (AccountingBookLifecycleStatus.Configuring or AccountingBookLifecycleStatus.Initializing))
                throw new InvalidOperationException("Book initialization may be configured only while the book is Configuring or Initializing.");
            if (!Enum.TryParse<AccountingBookInitializationMode>(request.Mode, true, out var mode)) throw new InvalidOperationException("Initialization mode is invalid.");
            if (request.CutoffDate == default) throw new InvalidOperationException("An initialization cutoff date is required.");
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new InvalidOperationException("An initialization idempotency key is required.");
            if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("An initialization reason is required.");
            var source = await ValidateSourceAsync(book, mode, request.SourceAccountingBookId, cancellationToken);
            if (mode == AccountingBookInitializationMode.IndependentOpeningBalances && !string.IsNullOrWhiteSpace(request.SourceAccountingBookCode)
                || source != null && !string.Equals(request.SourceAccountingBookCode, source.Code, StringComparison.Ordinal))
                throw new InvalidOperationException("Initialization source book ID and canonical code evidence must agree exactly.");
            var prepared = await PrepareEvidenceAsync(book, source, mode, request.CutoffDate.Date, request.IdempotencyKey.Trim(), request.Reason.Trim(), request.Lines, cancellationToken);
            var cutoffCode = await _db.FiscalPeriods.AsNoTracking().Where(item => item.Id == prepared.CutoffFiscalPeriodId && item.TenantId == TenantId && !item.IsDeleted)
                .Select(item => item.PeriodCode).SingleAsync(cancellationToken);
            if (request.CutoffFiscalPeriodId != prepared.CutoffFiscalPeriodId
                || !string.Equals(request.CutoffFiscalPeriodCode, cutoffCode, StringComparison.Ordinal))
                throw new InvalidOperationException("Initialization cutoff fiscal-period ID, code, and date must agree exactly.");
            var byKey = await Query().SingleOrDefaultAsync(item => item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
            if (byKey != null)
            {
                if (byKey.AccountingBookId != book.Id || byKey.EvidenceFingerprint != prepared.EvidenceFingerprint || byKey.ReconciliationFingerprint != prepared.ReconciliationFingerprint)
                    throw new InvalidOperationException("INITIALIZATION_IDEMPOTENCY_CONFLICT: The key was already used with different opening or authority evidence.");
                return await MapAsync(byKey, cancellationToken);
            }
            var latest = await Query().Where(item => item.AccountingBookId == book.Id).OrderByDescending(item => item.Version).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
            var existing = latest?.InitializationStatus == AccountingBookInitializationStatus.Draft ? latest : null;
            if (existing != null)
            {
                ApplyRowVersion(existing, request.RowVersion);
            }
            else if (latest?.InitializationStatus is AccountingBookInitializationStatus.PendingApproval or AccountingBookInitializationStatus.Approved)
                throw new InvalidOperationException("Submitted or approved initialization evidence is immutable.");
            var entity = existing ?? new AccountingBookInitialization { TenantId = TenantId, AccountingBookId = book.Id,
                Version = (latest?.Version ?? 0) + 1, SupersedesInitializationId = latest?.Id,
                PreparedByUserId = Actor(), PreparedAtUtc = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = ActorName() };
            if (existing == null) _db.AccountingBookInitializations.Add(entity);
            entity.Mode = mode; entity.CutoffDate = request.CutoffDate.Date; entity.CutoffFiscalPeriodId = prepared.CutoffFiscalPeriodId;
            entity.SourceAccountingBookId = source?.Id;
            entity.IdempotencyKey = request.IdempotencyKey.Trim(); entity.Reason = request.Reason.Trim(); entity.InitializationStatus = AccountingBookInitializationStatus.Draft;
            entity.TotalDebits = prepared.TotalDebits; entity.TotalCredits = prepared.TotalCredits; entity.RequiredAccountCount = prepared.RequiredCount;
            entity.CoveredAccountCount = prepared.Lines.Count; entity.EvidenceFingerprint = prepared.EvidenceFingerprint; entity.ReconciliationFingerprint = prepared.ReconciliationFingerprint;
            if (existing == null)
                entity.Lines = prepared.Lines.Select(line => NewLine(line)).ToList();
            else
            {
                // The exact account set was revalidated above. Update draft evidence in place so a
                // substantive edit preserves line identity and cannot create delete/reinsert races.
                foreach (var line in prepared.Lines)
                {
                    var persisted = entity.Lines.Single(item => item.AccountId == line.AccountId);
                    persisted.CurrencyCode = line.CurrencyCode; persisted.OpeningDebit = line.OpeningDebit;
                    persisted.OpeningCredit = line.OpeningCredit; persisted.BaseBookSignedBalance = line.BaseBookSignedBalance;
                    persisted.OpeningAdjustment = line.OpeningAdjustment; persisted.UpdatedAt = DateTime.UtcNow; persisted.UpdatedBy = ActorName();
                }
            }
            // The last substantive draft editor is the maker of the evidence eventually submitted.
            // Re-editing a rejected/draft version must not retain a prior checker decision.
            entity.PreparedByUserId = Actor(); entity.PreparedAtUtc = DateTime.UtcNow;
            entity.WorkflowInstanceId = null; entity.ApprovedByUserId = null; entity.ApprovedAtUtc = null;
            entity.RejectedByUserId = null; entity.RejectedAtUtc = null; entity.DecidedByUserId = null;
            entity.DecidedAtUtc = null; entity.DecisionReason = null;
            book.InitializationStartedAtUtc ??= DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await AuditAsync(FinanceAuditEvents.AccountingBookInitializationConfigured, entity, request.Reason, cancellationToken);
            return await MapAsync(await Query().AsNoTracking().SingleAsync(item => item.Id == entity.Id, cancellationToken), cancellationToken);
        }, cancellationToken);

    public Task<AccountingBookInitializationDto> SubmitAsync(Guid accountingBookId, CancellationToken cancellationToken = default) => AtomicAsync(async () =>
    {
        var entity = await Query().Where(item => item.AccountingBookId == accountingBookId).OrderByDescending(item => item.Version).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Book initialization has not been configured.");
        if (entity.InitializationStatus == AccountingBookInitializationStatus.PendingApproval) return await MapAsync(entity, cancellationToken);
        if (entity.InitializationStatus != AccountingBookInitializationStatus.Draft) throw new InvalidOperationException("Only Draft initialization evidence can be submitted.");
        await EnsureEvidenceUnchangedAsync(entity, cancellationToken);
        if (!await _workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType)) throw new InvalidOperationException("A published AccountingBookInitialization approval workflow is required.");
        var result = await _workflow.StartApprovalWorkflowAsync(WorkflowEntityType, entity.Id);
        if (!result.Success) throw new InvalidOperationException(result.Message ?? "Initialization approval workflow could not be started.");
        entity.WorkflowInstanceId = result.WorkflowInstanceId; entity.InitializationStatus = AccountingBookInitializationStatus.PendingApproval;
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.AccountingBookInitializationSubmitted, entity, entity.Reason, cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }, cancellationToken);

    public Task<AccountingBookInitializationDto> ApproveAsync(Guid accountingBookId, DecideAccountingBookInitializationDto request, CancellationToken cancellationToken = default) =>
        DecideAsync(accountingBookId, request, true, cancellationToken);
    public Task<AccountingBookInitializationDto> RejectAsync(Guid accountingBookId, DecideAccountingBookInitializationDto request, CancellationToken cancellationToken = default) =>
        DecideAsync(accountingBookId, request, false, cancellationToken);

    private Task<AccountingBookInitializationDto> DecideAsync(Guid accountingBookId, DecideAccountingBookInitializationDto request, bool approve, CancellationToken ct) => AtomicAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("An initialization decision reason is required.");
        var entity = await Query().Where(item => item.AccountingBookId == accountingBookId).OrderByDescending(item => item.Version).ThenByDescending(item => item.Id).FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Book initialization was not found.");
        ApplyRowVersion(entity, request.RowVersion);
        if (entity.InitializationStatus != AccountingBookInitializationStatus.PendingApproval || !entity.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Book initialization is not pending approval.");
        var checker = Actor();
        if (checker == entity.PreparedByUserId) throw new InvalidOperationException("The initialization checker must differ from the maker.");
        if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, entity.Id, checker)) throw new UnauthorizedAccessException("The current user cannot decide this initialization.");
        if (approve) await EnsureEvidenceUnchangedAsync(entity, ct);
        var outcome = await _workflow.ProcessApprovalStepAsync(WorkflowEntityType, entity.Id, checker, approve ? "Approve" : "Reject", request.Reason.Trim());
        if (!outcome.Success) throw new InvalidOperationException(outcome.Message ?? "Initialization workflow decision failed.");
        var completed = outcome.Status == WorkflowInstanceStatus.Completed;
        if (completed) entity.InitializationStatus = approve ? AccountingBookInitializationStatus.Approved : AccountingBookInitializationStatus.Rejected;
        entity.DecidedByUserId = checker; entity.DecidedAtUtc = DateTime.UtcNow;
        entity.ApprovedByUserId = completed && approve ? checker : null; entity.ApprovedAtUtc = completed && approve ? DateTime.UtcNow : null;
        entity.RejectedByUserId = completed && !approve ? checker : null; entity.RejectedAtUtc = completed && !approve ? DateTime.UtcNow : null;
        entity.DecisionReason = request.Reason.Trim(); entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(ct);
        var auditType = !completed ? FinanceAuditEvents.AccountingBookInitializationApprovalStepCompleted
            : approve ? FinanceAuditEvents.AccountingBookInitializationApproved : FinanceAuditEvents.AccountingBookInitializationRejected;
        await AuditAsync(auditType, entity, request.Reason, ct);
        return await MapAsync(entity, ct);
    }, ct);

    public async Task<AccountingBookActivationReadinessDto> GetReadinessAsync(Guid accountingBookId, CancellationToken cancellationToken = default)
    {
        var book = await RequireBookAsync(accountingBookId, cancellationToken);
        var blockers = new List<string>();
        var initialization = await Query().AsNoTracking().Where(item => item.AccountingBookId == book.Id)
            .OrderByDescending(item => item.Version).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (initialization?.InitializationStatus != AccountingBookInitializationStatus.Approved)
            blockers.Add("The latest initialization version must be approved.");
        else
        {
            try { await EnsureEvidenceUnchangedAsync(initialization, cancellationToken); }
            catch (InvalidOperationException ex) { blockers.Add(ex.Message); }
        }
        var periods = await _db.AccountingBookPeriods.AsNoTracking().Where(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        // Activation requires the single first posting period containing the day after the approved
        // cutoff (or the later effective date). Requiring every future period open would defeat close control.
        var firstPostingDate = initialization == null ? (DateTime?)null : initialization.CutoffDate.Date.AddDays(1);
        if (firstPostingDate.HasValue && book.EffectiveFromUtc.HasValue && book.EffectiveFromUtc.Value.Date > firstPostingDate.Value)
            firstPostingDate = book.EffectiveFromUtc.Value.Date;
        var effectivePeriods = firstPostingDate.HasValue ? await _db.FiscalPeriods.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted
            && item.StartDate <= firstPostingDate.Value && item.EndDate >= firstPostingDate.Value).Select(item => item.Id).ToListAsync(cancellationToken) : new List<Guid>();
        if (effectivePeriods.Count != 1) blockers.Add("Exactly one tenant fiscal period must contain the book's first posting date.");
        var ready = periods.Count(item => effectivePeriods.Contains(item.FiscalPeriodId) && item.PeriodStatus == AccountingBookPeriodStatus.Open
            && item.PendingStatus is not (AccountingBookPeriodStatus.Closed or AccountingBookPeriodStatus.Locked));
        if (effectivePeriods.Count == 1)
        {
            var fiscal = await _db.FiscalPeriods.AsNoTracking().SingleAsync(item => item.Id == effectivePeriods[0], cancellationToken);
            if (!fiscal.IsOpen || fiscal.IsClosed || fiscal.IsLocked) blockers.Add("The tenant fiscal period must remain globally open and unlocked.");
            var fiscalYear = await _db.FiscalYears.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fiscal.FiscalYearId
                && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
            if (fiscalYear == null || fiscalYear.IsClosed || fiscalYear.IsLocked)
                blockers.Add("The same-tenant parent fiscal year must remain open and unlocked.");
            var financeModuleId = await _db.ModuleDefinitions.AsNoTracking().Where(item => item.TenantId == TenantId && item.ModuleCode == ErpSystem.Core.Finance.FinanceModuleLockCatalog.Finance
                && item.IsActive && !item.IsDeleted).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
            if (!financeModuleId.HasValue && fiscal.IsGlobalLockSuspended)
                blockers.Add("The Finance module must be registered and explicitly open during a partial global period lock.");
            else if (financeModuleId.HasValue)
            {
                var moduleLock = await _db.PeriodModuleLocks.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId
                    && item.FiscalPeriodId == fiscal.Id && item.ModuleDefinitionId == financeModuleId.Value && !item.IsDeleted, cancellationToken);
                var expired = moduleLock is { IsLocked: false, ReopenExpiresAtUtc: not null }
                    && moduleLock.ReopenExpiresAtUtc <= DateTime.UtcNow;
                if (moduleLock?.IsLocked == true || expired || fiscal.IsGlobalLockSuspended && moduleLock == null)
                    blockers.Add("The Finance module must remain open for the book's first posting period.");
            }
        }
        if (effectivePeriods.Count == 1 && ready != 1) blockers.Add("The book's first posting period must be governed and open.");
        return new AccountingBookActivationReadinessDto { IsReady = blockers.Count == 0, Blockers = blockers,
            InitializationFingerprint = initialization?.EvidenceFingerprint, RequiredPeriodCount = effectivePeriods.Count, ReadyPeriodCount = ready };
    }

    public async Task<AccountingBookInitializationEvidenceValidationDto> ValidateCurrentApprovedEvidenceAsync(Guid accountingBookId, CancellationToken cancellationToken = default)
    {
        await RequireBookAsync(accountingBookId, cancellationToken);
        var latest = await Query().AsNoTracking().Where(item => item.AccountingBookId == accountingBookId)
            .OrderByDescending(item => item.Version).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (latest?.InitializationStatus != AccountingBookInitializationStatus.Approved)
            return new() { IsValid = false, InitializationId = latest?.Id, Version = latest?.Version, Blocker = "The latest initialization version is not approved." };
        try
        {
            // Re-derivation is the C4 authority: retained hashes alone are not proof that mappings and balances stayed reconciled.
            await EnsureEvidenceUnchangedAsync(latest, cancellationToken);
            return new() { IsValid = true, InitializationId = latest.Id, Version = latest.Version,
                EvidenceFingerprint = latest.EvidenceFingerprint, ReconciliationFingerprint = latest.ReconciliationFingerprint };
        }
        catch (InvalidOperationException ex)
        {
            return new() { IsValid = false, InitializationId = latest.Id, Version = latest.Version,
                EvidenceFingerprint = latest.EvidenceFingerprint, ReconciliationFingerprint = latest.ReconciliationFingerprint, Blocker = ex.Message };
        }
    }

    private async Task EnsureEvidenceUnchangedAsync(AccountingBookInitialization entity, CancellationToken ct)
    {
        var source = await ValidateSourceAsync(entity.AccountingBook, entity.Mode, entity.SourceAccountingBookId, ct);
        var prepared = await PrepareEvidenceAsync(entity.AccountingBook, source, entity.Mode, entity.CutoffDate,
            entity.IdempotencyKey, entity.Reason, entity.Lines.Select(MapLine).ToList(), ct);
        if (prepared.CutoffFiscalPeriodId != entity.CutoffFiscalPeriodId || prepared.EvidenceFingerprint != entity.EvidenceFingerprint || prepared.ReconciliationFingerprint != entity.ReconciliationFingerprint)
            throw new InvalidOperationException("INITIALIZATION_EVIDENCE_STALE: Account mappings, classifications, base balances or opening evidence changed.");
    }

    private async Task<Prepared> PrepareEvidenceAsync(AccountingBook book, AccountingBook? source, AccountingBookInitializationMode mode, DateTime cutoff,
        string idempotencyKey, string reason, IReadOnlyCollection<AccountingBookInitializationLineDto> requested, CancellationToken ct)
    {
        var cutoffPeriods = (await _db.FiscalPeriods.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted).ToListAsync(ct))
            .Where(item => item.EndDate.Date == cutoff.Date).ToList();
        if (cutoffPeriods.Count != 1)
            throw new InvalidOperationException("INITIALIZATION_CUTOFF_PERIOD_INVALID: Cutoff must be the end date of exactly one live same-tenant fiscal period.");
        var cutoffPeriod = cutoffPeriods[0];
        var mappings = await _db.AccountAccountingBooks.AsNoTracking().Include(item => item.Account).Include(item => item.AccountClassification)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && !item.IsDeleted).ToListAsync(ct);
        mappings = mappings.Where(item => item.IsEnabled || !book.IsActive && FinanceClassificationManifestSeeder.IsUntouchedManifestOwnedMapping(item))
            .OrderBy(item => item.AccountId).ToList();
        if (mappings.Count == 0 || mappings.Any(item => item.Account == null || item.Account.IsDeleted || item.Account.TenantId != TenantId
            || item.AccountClassification == null || item.AccountClassification.IsDeleted || item.AccountClassification.Status != AccountClassificationStatus.Active
            || !item.AccountClassification.IsPostingClassification || item.AccountClassification.TenantId != TenantId
            || item.AccountClassification.AccountingBookId != book.Id || item.AccountClassification.CoreAccountType != item.Account.AccountType))
            throw new InvalidOperationException("Initialization requires complete enabled account mappings with compatible active posting classifications.");
        var lines = requested.OrderBy(item => item.AccountId).ToList();
        if (lines.Count != mappings.Count || lines.Select(item => item.AccountId).Distinct().Count() != lines.Count
            || !lines.Select(item => item.AccountId).SequenceEqual(mappings.Select(item => item.AccountId)))
            throw new InvalidOperationException("Initialization must contain exactly one opening line for every enabled mapped account.");
        var tenantCurrency = await _db.Tenants.AsNoTracking().Where(item => item.Id == TenantId).Select(item => item.BaseCurrency).SingleOrDefaultAsync(ct);
        if (tenantCurrency is not { Length: 3 } || tenantCurrency.Any(ch => ch is < 'A' or > 'Z')) throw new InvalidOperationException("Canonical tenant functional-currency authority is required.");
        foreach (var line in lines)
        {
            line.CurrencyCode = line.CurrencyCode?.Trim() ?? string.Empty;
            if (!string.Equals(line.CurrencyCode, tenantCurrency, StringComparison.Ordinal)) throw new InvalidOperationException("Every opening line must use the exact tenant functional currency.");
            if (line.OpeningDebit < 0 || line.OpeningCredit < 0 || line.OpeningDebit > 0 && line.OpeningCredit > 0) throw new InvalidOperationException("Opening debit/credit evidence is invalid.");
        }
        var authorityBook = source ?? book;
        var sourceTransactions = await _db.AccountTransactions.AsNoTracking().Include(item => item.JournalEntry).Include(item => item.FiscalPeriod)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == authorityBook.Id && item.TransactionDate <= cutoff && !item.IsDeleted
                && item.PostingStatus == "Posted").ToListAsync(ct);
        // Initialization and C2 rebuild must interpret identical retained ledger evidence. A posted
        // active line under inconsistent header/book/period evidence is corruption, never an omission.
        if (sourceTransactions.Any(item => item.JournalEntry == null || item.JournalEntry.IsDeleted || item.JournalEntry.PostingStatus != "Posted"
            || item.JournalEntry.TenantId != TenantId || item.JournalEntry.AccountingBookId != authorityBook.Id
            || !string.Equals(item.BookClassification, authorityBook.Code, StringComparison.Ordinal)
            || !string.Equals(item.JournalEntry.BookClassification, authorityBook.Code, StringComparison.Ordinal)
            || item.FiscalPeriod == null || item.FiscalPeriod.TenantId != TenantId || item.FiscalPeriod.IsDeleted
            || item.TransactionDate.Date < item.FiscalPeriod.StartDate.Date || item.TransactionDate.Date > item.FiscalPeriod.EndDate.Date))
            throw new InvalidOperationException("INITIALIZATION_SOURCE_EVIDENCE_CORRUPT: Posted line, journal, exact book, code snapshot or fiscal-period lineage disagrees.");
        var balances = await _db.AccountBalances.AsNoTracking().Include(item => item.FiscalPeriod)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == authorityBook.Id && !item.IsDeleted && item.FiscalPeriod.EndDate <= cutoff)
            .ToListAsync(ct);
        var latest = balances.GroupBy(item => item.AccountId).ToDictionary(group => group.Key,
            group => group.OrderByDescending(item => item.FiscalPeriod.EndDate).ThenByDescending(item => item.FiscalPeriod.PeriodNumber).First());
        if (mode != AccountingBookInitializationMode.IndependentOpeningBalances)
        {
            foreach (var line in lines)
            {
                var expected = latest.GetValueOrDefault(line.AccountId)?.ClosingBalance ?? 0m;
                if (line.BaseBookSignedBalance != expected) throw new InvalidOperationException("Base-book opening evidence no longer agrees with the authoritative exact-book balance.");
                if (mode == AccountingBookInitializationMode.BaseBookCopyAtCutoff && line.OpeningAdjustment != 0)
                    throw new InvalidOperationException("Base-book copy initialization cannot contain opening adjustments.");
                var signedOpening = line.OpeningDebit - line.OpeningCredit;
                var expectedOpening = mode == AccountingBookInitializationMode.BaseBookCopyAtCutoff ? expected : expected + line.OpeningAdjustment;
                if (signedOpening != expectedOpening) throw new InvalidOperationException("Opening evidence does not reconcile to the selected initialization mode.");
            }
        }
        else
        {
            if (lines.Any(item => item.BaseBookSignedBalance != 0 || item.OpeningAdjustment != 0))
                throw new InvalidOperationException("Independent opening balances cannot carry base-book or adjustment evidence.");
            foreach (var line in lines)
                if (line.OpeningDebit - line.OpeningCredit != (latest.GetValueOrDefault(line.AccountId)?.ClosingBalance ?? 0m))
                    throw new InvalidOperationException("Independent opening evidence must reconcile to posted exact-book balances at cutoff; requested lines are not financial authority.");
        }
        var debit = lines.Sum(item => item.OpeningDebit); var credit = lines.Sum(item => item.OpeningCredit);
        if (debit != credit) throw new InvalidOperationException("Initialization opening evidence must be balanced.");
        var authority = string.Join('|', mappings.Select(item => $"{item.AccountId:N}:{item.Account.AccountNumber}:{item.Account.AccountType}:{item.Id:N}:{item.AccountClassificationId:N}:{item.AccountClassification!.Code}:{item.AccountClassification.Status}:{item.AccountClassification.IsPostingClassification}"));
        var balanceEvidence = string.Join('|', latest.OrderBy(item => item.Key).Select(item => $"{item.Key:N}:{item.Value.Id:N}:{item.Value.FiscalPeriodId:N}:{item.Value.FiscalPeriod.PeriodCode}:{item.Value.FiscalPeriod.StartDate:O}:{item.Value.FiscalPeriod.EndDate:O}:{D(item.Value.OpeningBalance)}:{D(item.Value.PeriodDebits)}:{D(item.Value.PeriodCredits)}:{D(item.Value.ClosingBalance)}"));
        var transactionEvidence = string.Join('|', sourceTransactions.OrderBy(item => item.TransactionDate).ThenBy(item => item.JournalEntryId).ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.JournalEntryId, item.FiscalPeriodId, item.AccountId, item.TransactionDate, item.DebitAmount, item.CreditAmount, item.TransactionCurrency, item.ForeignCurrencyAmount, item.ExchangeRate })
            .Select(item => $"{item.Id:N}:{item.JournalEntryId:N}:{item.FiscalPeriodId:N}:{item.AccountId:N}:{item.TransactionDate:O}:{D(item.DebitAmount)}:{D(item.CreditAmount)}:{item.TransactionCurrency}:{D(item.ForeignCurrencyAmount ?? 0)}:{D(item.ExchangeRate ?? 0)}"));
        var lineEvidence = string.Join('|', lines.Select(item => $"{item.AccountId:N}:{item.CurrencyCode}:{D(item.OpeningDebit)}:{D(item.OpeningCredit)}:{D(item.BaseBookSignedBalance)}:{D(item.OpeningAdjustment)}"));
        // Lifecycle activation changes the book's state but not its approved opening authority.
        // The source's active/postable state is revalidated separately on every evidence read.
        var evidence = Hash($"BOOK-INITIALIZATION-EVIDENCE-V2|{TenantId:N}|{book.Id:N}|{book.Code}|{book.BookType}|{book.FunctionalCurrencyCode}|{mode}|{cutoff:yyyy-MM-dd}|{cutoffPeriod.Id:N}|{cutoffPeriod.PeriodCode}|{cutoffPeriod.StartDate:O}|{cutoffPeriod.EndDate:O}|{source?.Id:N}|{source?.Code}|{source?.BookType}|{source?.FunctionalCurrencyCode}|{idempotencyKey}|{reason}|{lineEvidence}");
        var reconciliation = Hash($"BOOK-INITIALIZATION-RECONCILIATION-V1|{evidence}|{authority}|{balanceEvidence}|{transactionEvidence}|{D(debit)}|{D(credit)}");
        return new Prepared(lines, mappings.Count, debit, credit, evidence, reconciliation, cutoffPeriod.Id);
    }

    private async Task<AccountingBook?> ValidateSourceAsync(AccountingBook book, AccountingBookInitializationMode mode, Guid? sourceId, CancellationToken ct)
    {
        if (mode == AccountingBookInitializationMode.IndependentOpeningBalances)
        { if (sourceId.HasValue) throw new InvalidOperationException("Independent initialization cannot specify a source book."); return null; }
        if (!sourceId.HasValue || sourceId == book.Id) throw new InvalidOperationException("A distinct same-tenant source book is required for this initialization mode.");
        var source = await _db.AccountingBooks.SingleOrDefaultAsync(item => item.Id == sourceId && item.TenantId == TenantId && !item.IsDeleted, ct)
            ?? throw new InvalidOperationException("Initialization source book was not found for this tenant.");
        if (source.BookType == AccountingBookType.Delta || source.LifecycleStatus != AccountingBookLifecycleStatus.Active
            || !source.IsActive || !source.AllowsPosting || !IsCanonicalBookCode(source.Code)
            || source.FunctionalCurrencyCode is not { Length: 3 } currency || currency.Any(character => character is < 'A' or > 'Z')
            || !string.Equals(source.FunctionalCurrencyCode, book.FunctionalCurrencyCode, StringComparison.Ordinal))
            throw new InvalidOperationException("Initialization source must remain a canonical active and postable full accounting book.");
        return source;
    }
    private static bool IsCanonicalBookCode(string? value) => value is { Length: > 0 and <= 20 }
        && !PseudoBookCodes.Contains(value) && char.IsAsciiLetter(value[0])
        && value.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character == '_');
    private IQueryable<AccountingBookInitialization> Query() => _db.AccountingBookInitializations.Include(item => item.AccountingBook)
        .Include(item => item.SourceAccountingBook).Include(item => item.CutoffFiscalPeriod)
        .Include(item => item.Lines).ThenInclude(line => line.Account)
        .Where(item => item.TenantId == TenantId && !item.IsDeleted);
    private async Task<AccountingBook> RequireBookAsync(Guid id, CancellationToken ct) => await _db.AccountingBooks.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, ct)
        ?? throw new KeyNotFoundException("Accounting book was not found.");
    private static AccountingBookInitializationLineDto MapLine(AccountingBookInitializationLine line) => new() { AccountId = line.AccountId,
        AccountNumber = line.Account?.AccountNumber ?? line.AccountId.ToString(), AccountName = line.Account?.AccountName ?? "Account name unavailable",
        AccountType = line.Account?.AccountType.ToString() ?? string.Empty, CurrencyCode = line.CurrencyCode,
        OpeningDebit = line.OpeningDebit, OpeningCredit = line.OpeningCredit, BaseBookSignedBalance = line.BaseBookSignedBalance, OpeningAdjustment = line.OpeningAdjustment };
    private AccountingBookInitializationLine NewLine(AccountingBookInitializationLineDto line) => new() { TenantId = TenantId, AccountId = line.AccountId,
        CurrencyCode = line.CurrencyCode, OpeningDebit = line.OpeningDebit, OpeningCredit = line.OpeningCredit,
        BaseBookSignedBalance = line.BaseBookSignedBalance, OpeningAdjustment = line.OpeningAdjustment,
        CreatedAt = DateTime.UtcNow, CreatedBy = ActorName() };
    private static AccountingBookInitializationDto Map(AccountingBookInitialization item) => new() { Id = item.Id, AccountingBookId = item.AccountingBookId,
        AccountingBookCode = item.AccountingBook.Code, Version = item.Version, SupersedesInitializationId = item.SupersedesInitializationId,
        Mode = item.Mode.ToString(), Status = item.InitializationStatus.ToString(), CutoffDate = item.CutoffDate,
        CutoffFiscalPeriodId = item.CutoffFiscalPeriodId, CutoffFiscalPeriodCode = item.CutoffFiscalPeriod?.PeriodCode ?? string.Empty,
        SourceAccountingBookId = item.SourceAccountingBookId, SourceAccountingBookCode = item.SourceAccountingBook?.Code, IdempotencyKey = item.IdempotencyKey,
        Reason = item.Reason, TotalDebits = item.TotalDebits, TotalCredits = item.TotalCredits, RequiredAccountCount = item.RequiredAccountCount,
        CoveredAccountCount = item.CoveredAccountCount, IsBalanced = item.TotalDebits == item.TotalCredits,
        IsCoverageComplete = item.RequiredAccountCount == item.CoveredAccountCount, EvidenceFingerprint = item.EvidenceFingerprint,
        ReconciliationFingerprint = item.ReconciliationFingerprint, PreparedByUserId = item.PreparedByUserId, PreparedAtUtc = item.PreparedAtUtc,
        ApprovedByUserId = item.ApprovedByUserId, ApprovedAtUtc = item.ApprovedAtUtc,
        RejectedByUserId = item.RejectedByUserId, RejectedAtUtc = item.RejectedAtUtc,
        DecidedByUserId = item.DecidedByUserId, DecidedAtUtc = item.DecidedAtUtc, DecisionReason = item.DecisionReason,
        RowVersion = item.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(item.RowVersion), Lines = item.Lines.OrderBy(line => line.AccountId).Select(MapLine).ToList() };
    private async Task<AccountingBookInitializationDto> MapAsync(AccountingBookInitialization item, CancellationToken ct)
    {
        var dto = Map(item);
        var actorIds = new[] { item.PreparedByUserId, item.ApprovedByUserId, item.RejectedByUserId, item.DecidedByUserId }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (actorIds.Length == 0) return dto;

        var actors = await _db.Users.AsNoTracking()
            .Where(user => user.TenantId == TenantId && actorIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.UserName })
            .ToListAsync(ct);
        var names = actors.ToDictionary(user => user.Id, user =>
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrWhiteSpace(fullName) ? user.UserName ?? "Unknown user" : fullName;
        });
        dto.PreparedByName = names.GetValueOrDefault(item.PreparedByUserId);
        dto.ApprovedByName = item.ApprovedByUserId.HasValue ? names.GetValueOrDefault(item.ApprovedByUserId.Value) : null;
        dto.RejectedByName = item.RejectedByUserId.HasValue ? names.GetValueOrDefault(item.RejectedByUserId.Value) : null;
        dto.DecidedByName = item.DecidedByUserId.HasValue ? names.GetValueOrDefault(item.DecidedByUserId.Value) : null;
        return dto;
    }
    private async Task AuditAsync(string type, AccountingBookInitialization entity, string reason, CancellationToken ct) => await _audit.RecordAsync(new FinanceAuditEventDto
    { TenantId = TenantId, EventType = type, SourceModule = "GL", SourceDocumentType = WorkflowEntityType, SourceDocumentId = entity.Id,
        WorkflowInstanceId = entity.WorkflowInstanceId, Resource = "Finance.AccountingBookInitialization", ResourceId = entity.Id.ToString(), AfterValues = new
        { entity.AccountingBookId, Mode = entity.Mode.ToString(), Status = entity.InitializationStatus.ToString(), entity.CutoffDate, entity.SourceAccountingBookId,
          entity.IdempotencyKey, entity.EvidenceFingerprint, entity.ReconciliationFingerprint, entity.TotalDebits, entity.TotalCredits,
          entity.RequiredAccountCount, entity.CoveredAccountCount, entity.PreparedByUserId, entity.ApprovedByUserId,
          entity.RejectedByUserId, entity.DecidedByUserId }, Reason = reason.Trim() }, ct);
    private void ApplyRowVersion(AccountingBookInitialization entity, string? encoded)
    { if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("Row version is required."); byte[] value;
      try { value = Convert.FromBase64String(encoded); } catch (FormatException) { throw new InvalidOperationException("Row version is invalid."); }
      if (value.Length == 0 || entity.RowVersion.Length > 0 && !entity.RowVersion.SequenceEqual(value)) throw new DbUpdateConcurrencyException("Book initialization changed after it was loaded.");
      _db.Entry(entity).Property(item => item.RowVersion).OriginalValue = value; }
    private Guid Actor() => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated Finance user is required.");
    private string ActorName() => _currentUser.UserName ?? "system";
    private Task<T> AtomicAsync<T>(Func<Task<T>> action, CancellationToken ct) => !_db.Database.IsRelational() ? action() : _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    { await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); try { var result = await action(); await tx.CommitAsync(ct); return result; }
      catch { await tx.RollbackAsync(CancellationToken.None); _db.ChangeTracker.Clear(); throw; } });
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string D(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private sealed record Prepared(IReadOnlyList<AccountingBookInitializationLineDto> Lines, int RequiredCount, decimal TotalDebits,
        decimal TotalCredits, string EvidenceFingerprint, string ReconciliationFingerprint, Guid CutoffFiscalPeriodId);
}
