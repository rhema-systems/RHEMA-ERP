using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Creates the executable Finance baseline for a newly provisioned tenant. It is deliberately
/// narrower than lifecycle administration: only untouched manifest-owned standard books with no
/// economic activity are activated. Existing tenant decisions are never repaired or overwritten.
/// </summary>
public sealed class FinanceBaselineProvisioningSeeder
{
    public const string ProvisioningVersion = "FIN-BASELINE-2.0";
    public static readonly Guid ProvisioningMakerId = Guid.Parse("00000000-0000-0000-0000-00000000F101");
    public static readonly Guid ProvisioningAuthorityId = Guid.Parse("00000000-0000-0000-0000-00000000F102");
    private static readonly string[] StandardBookCodes = ["BASE", "IFRS_ADJUSTMENTS", "USD_PARALLEL"];
    private const string InitializationReason = "System-provisioned zero-balance Finance baseline.";

    private readonly ApplicationDbContext _db;
    private readonly ILogger _logger;

    public FinanceBaselineProvisioningSeeder(ApplicationDbContext db, ILogger logger) => (_db, _logger) = (db, logger);

    public async Task SeedAsync(Guid tenantId, DateTime provisionedAtUtc, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking().SingleAsync(item => item.Id == tenantId && !item.IsDeleted, cancellationToken);
        var books = await _db.AccountingBooks
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && StandardBookCodes.Contains(item.Code))
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Code)
            .ToListAsync(cancellationToken);
        if (books.Count != StandardBookCodes.Length)
            throw new InvalidOperationException("FINANCE_BASELINE_BOOK_SET_INCOMPLETE: BASE, IFRS_ADJUSTMENTS and USD_PARALLEL must exist before baseline provisioning.");

        var cutoffPeriod = await FindBaselineCutoffAsync(tenantId, cancellationToken);
        var firstPostingDate = cutoffPeriod.EndDate.Date.AddDays(1);
        var firstPostingPeriod = await _db.FiscalPeriods.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted && item.StartDate <= firstPostingDate && item.EndDate >= firstPostingDate,
            cancellationToken) ?? throw new InvalidOperationException("FINANCE_BASELINE_FIRST_PERIOD_MISSING: no fiscal period contains the first baseline posting date.");
        if (!firstPostingPeriod.IsOpen || firstPostingPeriod.IsClosed || firstPostingPeriod.IsLocked)
            throw new InvalidOperationException("FINANCE_BASELINE_FIRST_PERIOD_NOT_OPEN: the first baseline posting period must be globally open and unlocked.");

        foreach (var book in books)
            await ProvisionUntouchedBookAsync(tenantId,
                book.BookType == AccountingBookType.ParallelFull ? book.FunctionalCurrencyCode! : tenant.BaseCurrency,
                book, cutoffPeriod, firstPostingDate, provisionedAtUtc, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Applied Finance executable baseline {Version} for tenant {TenantId}", ProvisioningVersion, tenantId);
    }

    public static string? AuthorityName(Guid id) => id == ProvisioningMakerId
        ? "Finance baseline provisioning"
        : id == ProvisioningAuthorityId ? "Finance system authority" : null;

    private async Task ProvisionUntouchedBookAsync(Guid tenantId, string currency, AccountingBook book, FiscalPeriod cutoff,
        DateTime firstPostingDate, DateTime now, CancellationToken ct)
    {
        var existingInitializations = await _db.AccountingBookInitializations
            .Include(item => item.Lines)
            .Where(item => item.TenantId == tenantId && item.AccountingBookId == book.Id && !item.IsDeleted)
            .ToListAsync(ct);
        var hasActivity = await _db.AccountTransactions.AnyAsync(item =>
            item.TenantId == tenantId && item.AccountingBookId == book.Id && !item.IsDeleted, ct)
            || await _db.JournalEntries.AnyAsync(item =>
                item.TenantId == tenantId && item.AccountingBookId == book.Id && !item.IsDeleted, ct);

        var expectedProvisioningLifecycle = book.BookType == AccountingBookType.PrimaryFull
            ? book.LifecycleStatus == AccountingBookLifecycleStatus.Active && book.IsActive && book.AllowsPosting
            : book.LifecycleStatus == AccountingBookLifecycleStatus.Configuring && !book.IsActive && !book.AllowsPosting;
        var expectedRepairLifecycle = book.LifecycleStatus == AccountingBookLifecycleStatus.Active
            && book.IsActive && book.AllowsPosting;
        var systemOwnedBook = book.UpdatedBy == null
            && string.Equals(book.CreatedBy, $"System ({FinanceClassificationManifestSeeder.ManifestVersion})", StringComparison.Ordinal);
        var repairableInitialization = existingInitializations.Count == 1
            ? existingInitializations[0]
            : null;
        var repairSystemEvidence = repairableInitialization != null
            && expectedRepairLifecycle
            && repairableInitialization.InitializationStatus == AccountingBookInitializationStatus.Approved
            && string.Equals(repairableInitialization.IdempotencyKey, $"{ProvisioningVersion}-{book.Code}", StringComparison.Ordinal)
            && string.Equals(repairableInitialization.CreatedBy, $"System ({ProvisioningVersion})", StringComparison.Ordinal);
        if (hasActivity || !systemOwnedBook
            || (existingInitializations.Count == 0 && !expectedProvisioningLifecycle)
            || (existingInitializations.Count > 0 && !repairSystemEvidence))
            return;

        var mappings = await _db.AccountAccountingBooks.Include(item => item.Account).Include(item => item.AccountClassification)
            .Where(item => item.TenantId == tenantId && item.AccountingBookId == book.Id && !item.IsDeleted)
            .ToListAsync(ct);
        mappings = mappings.Where(item => item.Account != null && !item.Account.IsDeleted
            && (item.Account.EffectiveDate == null || item.Account.EffectiveDate.Value.Date <= cutoff.EndDate.Date))
            .OrderBy(item => item.AccountId).ToList();
        if (mappings.Count == 0 || mappings.Any(item => item.AccountClassification == null
            || item.AccountClassification.IsDeleted || item.AccountClassification.Status != AccountClassificationStatus.Active
            || !item.AccountClassification.IsPostingClassification
            || item.AccountClassification.AccountingBookId != book.Id
            || item.AccountClassification.CoreAccountType != item.Account.AccountType
            || !FinanceClassificationManifestSeeder.IsUntouchedManifestOwnedMapping(item)))
            throw new InvalidOperationException($"FINANCE_BASELINE_MAPPING_INVALID: {book.Code} does not have complete untouched manifest-owned account authority.");

        var idempotencyKey = $"{ProvisioningVersion}-{book.Code}";
        var lineEvidence = string.Join('|', mappings.Select(item => $"{item.AccountId:N}:{currency}:0:0:0:0::0:::"));
        var evidence = AccountingBookInitializationFingerprint.Evidence(
            tenantId, book.Id, book.Code, book.BookType, book.FunctionalCurrencyCode,
            AccountingBookInitializationMode.IndependentOpeningBalances, cutoff.EndDate.Date,
            cutoff.Id, cutoff.PeriodCode, cutoff.StartDate, cutoff.EndDate,
            null, null, null, null, null, idempotencyKey, InitializationReason, lineEvidence);
        var authority = string.Join('|', mappings.Select(item => $"{item.AccountId:N}:{item.Account.AccountNumber}:{item.Account.AccountType}:{item.Id:N}:{item.AccountClassificationId:N}:{item.AccountClassification!.Code}:{item.AccountClassification.Status}:{item.AccountClassification.IsPostingClassification}"));
        var reconciliation = AccountingBookInitializationFingerprint.Reconciliation(evidence, authority, string.Empty, string.Empty, 0, 0);
        if (repairSystemEvidence)
        {
            var retainedInitialization = repairableInitialization!;
            var expectedAccounts = mappings.Select(item => item.AccountId).ToHashSet();
            var retainedAccounts = retainedInitialization.Lines.Select(item => item.AccountId).ToHashSet();
            if (!expectedAccounts.SetEquals(retainedAccounts)
                || retainedInitialization.Lines.Any(item => item.OpeningDebit != 0 || item.OpeningCredit != 0
                    || item.BaseBookSignedBalance != 0 || item.OpeningAdjustment != 0
                    || !string.Equals(item.CurrencyCode, currency, StringComparison.Ordinal)))
                throw new InvalidOperationException($"FINANCE_BASELINE_EVIDENCE_REPAIR_REFUSED: {book.Code} is not untouched zero-opening system evidence.");

            if (retainedInitialization.RequiredAccountCount == mappings.Count
                && retainedInitialization.CoveredAccountCount == mappings.Count
                && string.Equals(retainedInitialization.EvidenceFingerprint, evidence, StringComparison.Ordinal)
                && string.Equals(retainedInitialization.ReconciliationFingerprint, reconciliation, StringComparison.Ordinal))
                return;

            retainedInitialization.RequiredAccountCount = mappings.Count;
            retainedInitialization.CoveredAccountCount = mappings.Count;
            retainedInitialization.EvidenceFingerprint = evidence;
            retainedInitialization.ReconciliationFingerprint = reconciliation;
            retainedInitialization.UpdatedAt = now;
            retainedInitialization.UpdatedBy = $"System ({ProvisioningVersion})";
            return;
        }

        var initialization = new AccountingBookInitialization
        {
            TenantId = tenantId, AccountingBookId = book.Id, Version = 1,
            Mode = AccountingBookInitializationMode.IndependentOpeningBalances,
            InitializationStatus = AccountingBookInitializationStatus.Approved,
            CutoffDate = cutoff.EndDate.Date, CutoffFiscalPeriodId = cutoff.Id,
            IdempotencyKey = idempotencyKey, Reason = InitializationReason,
            RequiredAccountCount = mappings.Count, CoveredAccountCount = mappings.Count,
            TotalDebits = 0, TotalCredits = 0, EvidenceFingerprint = evidence,
            ReconciliationFingerprint = reconciliation, PreparedByUserId = ProvisioningMakerId,
            PreparedAtUtc = now, ApprovedByUserId = ProvisioningAuthorityId, ApprovedAtUtc = now,
            DecidedByUserId = ProvisioningAuthorityId, DecidedAtUtc = now,
            DecisionReason = $"Provisioned by {ProvisioningVersion}; no prior economic activity existed.",
            CreatedAt = now, CreatedBy = $"System ({ProvisioningVersion})",
            Lines = mappings.Select(item => new AccountingBookInitializationLine
            {
                TenantId = tenantId, AccountId = item.AccountId, CurrencyCode = currency,
                OpeningDebit = 0, OpeningCredit = 0, BaseBookSignedBalance = 0, OpeningAdjustment = 0,
                CreatedAt = now, CreatedBy = $"System ({ProvisioningVersion})"
            }).ToList()
        };
        _db.AccountingBookInitializations.Add(initialization);

        foreach (var mapping in mappings) mapping.IsEnabled = true;
        book.InitializationStartedAtUtc = now;
        book.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        book.IsActive = true;
        book.AllowsPosting = true;
    }

    private async Task<FiscalPeriod> FindBaselineCutoffAsync(Guid tenantId, CancellationToken ct)
    {
        var open = await _db.FiscalPeriods.AsNoTracking().Where(item => item.TenantId == tenantId && !item.IsDeleted
            && item.IsOpen && !item.IsClosed && !item.IsLocked).OrderBy(item => item.StartDate).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("FINANCE_BASELINE_OPEN_PERIOD_MISSING: at least one globally open fiscal period is required.");
        return await _db.FiscalPeriods.AsNoTracking().Where(item => item.TenantId == tenantId && !item.IsDeleted
                && item.IsClosed && item.EndDate < open.StartDate)
            .OrderByDescending(item => item.EndDate).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("FINANCE_BASELINE_CUTOFF_MISSING: a closed fiscal period immediately preceding the open operating window is required.");
    }

}
