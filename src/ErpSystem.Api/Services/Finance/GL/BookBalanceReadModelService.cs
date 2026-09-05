using System.Globalization;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Maintains rebuildable exact-book balance/exposure projections. Posted AccountTransaction rows are
/// always authoritative; neither projection may be used to manufacture journal evidence.
/// </summary>
public sealed class BookBalanceReadModelService : IBookBalanceReadModelService
{
    private const string Posted = "Posted";
    private readonly ApplicationDbContext _context;

    public BookBalanceReadModelService(ApplicationDbContext context) => _context = context;

    public async Task ApplyPostingAsync(Guid tenantId, Guid accountingBookId, string accountingBookCode,
        Guid fiscalPeriodId, string functionalCurrencyCode, IReadOnlyCollection<AccountTransaction> lines,
        bool updatePrimaryCompatibilityBalance, DateTime postedAt, Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        var active = lines.Where(line => !line.IsDeleted).ToArray();
        if (active.Length == 0) throw new InvalidOperationException("Posting contains no balance lines.");
        // Exact reversal may lawfully target the now-inactive historical default book. In that one
        // case the caller's already-validated primary flag preserves the original compatibility
        // coordinate; ordinary and non-primary posting still require today's active authority.
        var primaryBook = updatePrimaryCompatibilityBalance
            ? await ResolveUniqueDefaultBookAsync(tenantId, requireActivePosting: false, cancellationToken)
            : await ResolvePrimaryCompatibilityBookAsync(tenantId, cancellationToken);
        if (updatePrimaryCompatibilityBalance != (primaryBook.Id == accountingBookId))
            throw new InvalidOperationException(
                "Primary compatibility selection does not match the authoritative active default posting book.");
        var functionalAuthority = await ResolveFunctionalCurrencyAsync(tenantId, cancellationToken);
        if (!string.Equals(functionalCurrencyCode, functionalAuthority, StringComparison.Ordinal))
            throw new InvalidOperationException("Posting functional currency does not exactly match tenant authority.");
        if (active.Any(line => line.TenantId != tenantId || line.AccountingBookId != accountingBookId
                || line.FiscalPeriodId != fiscalPeriodId
                || !string.Equals(line.BookClassification, accountingBookCode, StringComparison.Ordinal)
                || !string.Equals(line.FunctionalCurrencyCode, functionalCurrencyCode, StringComparison.Ordinal)))
            throw new InvalidOperationException("Posting line evidence does not match the exact balance scope.");

        var period = await _context.FiscalPeriods.AsNoTracking()
            .SingleAsync(item => item.TenantId == tenantId && item.Id == fiscalPeriodId && !item.IsDeleted, cancellationToken);
        var accountIds = active.Select(line => line.AccountId).Distinct().ToArray();
        var rows = await _context.AccountBalances
            .Where(item => item.TenantId == tenantId && item.AccountingBookId == accountingBookId
                && item.Currency == functionalCurrencyCode
                && accountIds.Contains(item.AccountId) && !item.IsDeleted)
            .Include(item => item.FiscalPeriod)
            .ToListAsync(cancellationToken);

        foreach (var group in active.GroupBy(line => line.AccountId))
        {
            var debits = Round(group.Sum(line => line.DebitAmount));
            var credits = Round(group.Sum(line => line.CreditAmount));
            var signed = Round(debits - credits);
            var row = rows.SingleOrDefault(item => item.AccountId == group.Key && item.FiscalPeriodId == fiscalPeriodId);
            if (row == null)
            {
                var prior = rows.Where(item => item.AccountId == group.Key
                        && item.FiscalPeriod.FiscalYearId == period.FiscalYearId
                        && item.FiscalPeriod.PeriodNumber < period.PeriodNumber)
                    .OrderByDescending(item => item.FiscalPeriod.PeriodNumber)
                    .FirstOrDefault();
                var priorClosing = prior?.ClosingBalance ?? 0m;
                row = new AccountBalance
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountId = group.Key,
                    AccountingBookId = accountingBookId, BookClassification = accountingBookCode,
                    FiscalPeriodId = fiscalPeriodId,
                    Currency = functionalCurrencyCode, OpeningBalance = priorClosing,
                    OpeningBalanceType = Side(priorClosing), ClosingBalance = priorClosing,
                    ClosingBalanceType = Side(priorClosing),
                    YearToDateDebits = prior?.YearToDateDebits ?? 0m,
                    YearToDateCredits = prior?.YearToDateCredits ?? 0m,
                    YearToDateNetMovement = prior?.YearToDateNetMovement ?? 0m,
                    CreatedAt = postedAt
                };
                _context.AccountBalances.Add(row);
            }

            row.PeriodDebits = Round(row.PeriodDebits + debits);
            row.PeriodCredits = Round(row.PeriodCredits + credits);
            row.PeriodNetMovement = Round(row.PeriodDebits - row.PeriodCredits);
            row.ClosingBalance = Round(row.OpeningBalance + row.PeriodNetMovement);
            row.ClosingBalanceType = Side(row.ClosingBalance);
            row.YearToDateDebits = Round(row.YearToDateDebits + debits);
            row.YearToDateCredits = Round(row.YearToDateCredits + credits);
            row.YearToDateNetMovement = Round(row.YearToDateDebits - row.YearToDateCredits);
            row.TransactionCount += group.Count();
            row.LastTransactionDate = Max(row.LastTransactionDate, group.Max(line => line.TransactionDate));
            row.LastTransactionUserId = actorId;
            row.LastUpdated = postedAt;
            row.HasActivity = row.TransactionCount > 0;
            row.IsZeroBalance = row.ClosingBalance == 0m;
            row.IsNegativeBalance = row.ClosingBalance < 0m;
            row.IsReconciled = true;

            foreach (var later in rows.Where(item => item.AccountId == group.Key
                         && item.FiscalPeriod.FiscalYearId == period.FiscalYearId
                         && item.FiscalPeriod.PeriodNumber > period.PeriodNumber))
            {
                later.OpeningBalance = Round(later.OpeningBalance + signed);
                later.ClosingBalance = Round(later.ClosingBalance + signed);
                later.OpeningBalanceType = Side(later.OpeningBalance);
                later.ClosingBalanceType = Side(later.ClosingBalance);
                later.LastUpdated = postedAt;
                later.IsZeroBalance = later.ClosingBalance == 0m;
                later.IsNegativeBalance = later.ClosingBalance < 0m;
                later.IsReconciled = true;
            }

            foreach (var ytd in rows.Where(item => item.AccountId == group.Key
                         && item.FiscalPeriod.FiscalYearId == period.FiscalYearId
                         && item.FiscalPeriod.PeriodNumber > period.PeriodNumber))
            {
                ytd.YearToDateDebits = Round(ytd.YearToDateDebits + debits);
                ytd.YearToDateCredits = Round(ytd.YearToDateCredits + credits);
                ytd.YearToDateNetMovement = Round(ytd.YearToDateDebits - ytd.YearToDateCredits);
            }
        }

        await ApplyExposureDeltasAsync(tenantId, accountingBookId, accountingBookCode,
            functionalCurrencyCode, period, active, postedAt, cancellationToken);

        if (updatePrimaryCompatibilityBalance)
            await ApplyPrimaryCompatibilityAsync(tenantId, active, cancellationToken);
    }

    public async Task<BookBalanceInquiryDto> GetAsync(Guid tenantId, Guid accountId, string accountingBookCode,
        Guid? fiscalPeriodId = null, CancellationToken cancellationToken = default)
    {
        var code = RequireCode(accountingBookCode);
        var book = await ResolveBookAsync(tenantId, code, cancellationToken);
        var mapping = await _context.AccountAccountingBooks.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && item.AccountId == accountId && item.AccountingBookId == book.Id
            && item.IsEnabled && !item.IsDeleted, cancellationToken);
        if (!mapping) throw new KeyNotFoundException("Account is not enabled for the selected accounting book.");

        var balancesQuery = _context.AccountBalances.AsNoTracking().Where(item => item.TenantId == tenantId
            && item.AccountId == accountId && item.AccountingBookId == book.Id && !item.IsDeleted);
        if (fiscalPeriodId.HasValue) balancesQuery = balancesQuery.Where(item => item.FiscalPeriodId == fiscalPeriodId.Value);
        var balances = await balancesQuery.OrderBy(item => item.FiscalPeriodId)
            .Select(item => new AccountBookBalanceDto(item.AccountId, item.AccountingBookId,
                item.BookClassification, item.FiscalPeriodId, item.Currency!, item.OpeningBalance,
                item.PeriodDebits, item.PeriodCredits, item.PeriodNetMovement, item.ClosingBalance,
                item.YearToDateDebits, item.YearToDateCredits, item.YearToDateNetMovement,
                item.TransactionCount, item.LastTransactionDate)).ToListAsync(cancellationToken);
        var exposures = await _context.AccountCurrencyExposures.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.AccountId == accountId
                && item.AccountingBookId == book.Id && !item.IsDeleted)
            .OrderBy(item => item.TransactionCurrencyCode)
            .Select(item => new AccountCurrencyExposureDto(item.AccountId, item.AccountingBookId,
                item.AccountingBookCode, item.FunctionalCurrencyCode, item.TransactionCurrencyCode,
                item.SignedForeignBalance, item.SignedFunctionalBalance, item.TransactionCount,
                item.FirstTransactionDate, item.LastTransactionDate)).ToListAsync(cancellationToken);
        var functionalAuthority = await ResolveFunctionalCurrencyAsync(tenantId, cancellationToken);
        if (balances.Any(item => !string.Equals(item.AccountingBookCode, book.Code, StringComparison.Ordinal)
                || !string.Equals(item.FunctionalCurrencyCode, functionalAuthority, StringComparison.Ordinal))
            || exposures.Any(item => !string.Equals(item.AccountingBookCode, book.Code, StringComparison.Ordinal)
                || !string.Equals(item.FunctionalCurrencyCode, functionalAuthority, StringComparison.Ordinal)
                || item.TransactionCurrencyCode.Length != 3
                || !string.Equals(item.TransactionCurrencyCode, item.TransactionCurrencyCode.ToUpperInvariant(), StringComparison.Ordinal)))
            throw new InvalidOperationException("Book balance evidence is noncanonical or has invalid tenant/book/currency lineage.");
        return new BookBalanceInquiryDto(book.Id, book.Code, balances, exposures);
    }

    public async Task<BookBalanceReconciliationDto> ReconcileAsync(Guid tenantId,
        BookBalanceReconciliationRequestDto request, Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var book = await ResolveBookAsync(tenantId, RequireCode(request.AccountingBookCode), cancellationToken);
        var primaryBook = await ResolvePrimaryCompatibilityBookAsync(tenantId, cancellationToken);
        var functionalAuthority = await ResolveFunctionalCurrencyAsync(tenantId, cancellationToken);
        if (request.Apply && (string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || !request.ApprovedByUserId.HasValue || request.ApprovedByUserId == Guid.Empty
            || request.ApprovedByUserId == requestedByUserId))
            throw new InvalidOperationException("An approved rebuild requires a reason, idempotency key, and a different maker/checker user.");

        // Preview and apply share the same serialized source snapshot. A dry run must not report a
        // torn projection while a posting commits between its source and target reads.
        var ownsRebuildTransaction = _context.Database.IsRelational()
            && _context.Database.CurrentTransaction == null;
        await using var rebuildTransaction = ownsRebuildTransaction
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        await AcquireTenantProjectionLockAsync(tenantId, cancellationToken);

        // Ignore filters only while loading the exact source graph: an active posted line whose
        // header was soft-deleted is corrupt ledger evidence and must be observed and rejected,
        // never disappear from reconciliation through the JournalEntry query filter.
        var transactions = await _context.AccountTransactions.IgnoreQueryFilters().AsNoTracking()
            .Include(item => item.FiscalPeriod)
            .Include(item => item.JournalEntry)
            .Where(item => item.TenantId == tenantId && item.AccountingBookId == book.Id
                && item.BookClassification == book.Code && item.PostingStatus == Posted && !item.IsDeleted)
            .OrderBy(item => item.FiscalPeriod.FiscalYearId).ThenBy(item => item.FiscalPeriod.PeriodNumber)
            .ThenBy(item => item.AccountId).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        if (transactions.Any(item => !string.Equals(item.BookClassification, book.Code, StringComparison.Ordinal)
                || item.JournalEntry.TenantId != tenantId
                || item.JournalEntry.AccountingBookId != book.Id
                || !string.Equals(item.JournalEntry.BookClassification, book.Code, StringComparison.Ordinal)
                || !string.Equals(item.JournalEntry.PostingStatus, Posted, StringComparison.Ordinal)
                || item.JournalEntry.IsDeleted))
            throw new InvalidOperationException("Posted journal/header book evidence does not exactly match the selected accounting book.");
        if (transactions.Any(item => !string.Equals(item.FunctionalCurrencyCode, functionalAuthority, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(item.TransactionCurrency)
                    && (item.TransactionCurrency.Length != 3
                        || !string.Equals(item.TransactionCurrency, item.TransactionCurrency.ToUpperInvariant(), StringComparison.Ordinal)))))
            throw new InvalidOperationException("Transaction currency evidence does not exactly match tenant/canonical currency authority.");
        var expectedBalances = BuildBalances(tenantId, book, transactions);
        var expectedExposures = BuildExposures(tenantId, book, transactions);
        var transactionFingerprint = Fingerprint(transactions);
        var fingerprint = transactionFingerprint;
        var existingBalances = await _context.AccountBalances.AsNoTracking().Where(item =>
            item.TenantId == tenantId && item.AccountingBookId == book.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        var existingExposures = await _context.AccountCurrencyExposures.AsNoTracking().Where(item =>
            item.TenantId == tenantId && item.AccountingBookId == book.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        var balanceDrift = CountBalanceDrift(expectedBalances, existingBalances);
        var exposureDrift = CountExposureDrift(expectedExposures, existingExposures);
        var absoluteDrift = AbsoluteBalanceDrift(expectedBalances, existingBalances);
        var compatibilityDrift = 0;
        Dictionary<Guid, decimal>? primaryCompatibility = null;
        if (book.Id == primaryBook.Id)
        {
            var authority = await LoadPrimaryCompatibilityAuthorityAsync(
                tenantId, book, transactions, cancellationToken);
            var accounts = authority.Accounts;
            fingerprint = ReconciliationFingerprint(transactionFingerprint, authority.Fingerprint);
            primaryCompatibility = BuildPrimaryCompatibility(transactions, accounts);
            compatibilityDrift = accounts.Values.Count(account =>
                account.Balance != primaryCompatibility.GetValueOrDefault(account.Id));
        }

        if (!request.Apply)
        {
            if (rebuildTransaction != null)
                await rebuildTransaction.CommitAsync(cancellationToken);
            return new(book.Id, book.Code, false, balanceDrift, exposureDrift, compatibilityDrift,
                absoluteDrift, fingerprint, null);
        }

        var result = await ExecuteRebuildAsync(tenantId, book, request, requestedByUserId, expectedBalances,
            expectedExposures, primaryCompatibility, fingerprint, balanceDrift, exposureDrift,
            compatibilityDrift, absoluteDrift, cancellationToken);
        if (rebuildTransaction != null)
            await rebuildTransaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<BookBalanceReconciliationDto> ExecuteRebuildAsync(Guid tenantId, AccountingBook book,
        BookBalanceReconciliationRequestDto request, Guid requestedByUserId, List<AccountBalance> balances,
        List<AccountCurrencyExposure> exposures, Dictionary<Guid, decimal>? primaryCompatibility,
        string fingerprint, int balanceDrift, int exposureDrift, int compatibilityDrift,
        decimal absoluteDrift, CancellationToken cancellationToken)
    {
        // No executable API is exposed in C2. Any future command surface must enforce tenant/user
        // authorization before calling this service; this fingerprint preserves maker/checker intent.
        var commandFingerprint = RebuildCommandFingerprint(tenantId, book, request, requestedByUserId, fingerprint);
        var existingRun = await _context.FinanceBalanceRebuildRuns.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.AccountingBookId == book.Id
            && item.IdempotencyKey == request.IdempotencyKey!.Trim() && !item.IsDeleted, cancellationToken);
        if (existingRun != null)
        {
            if (!string.Equals(existingRun.SourceFingerprint, fingerprint, StringComparison.Ordinal)
                || !string.Equals(existingRun.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The rebuild idempotency key was already used for different source or governance evidence.");
            return new(book.Id, book.Code, true, 0, 0, 0, 0m, fingerprint, existingRun.Id);
        }

        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            _context.AccountBalances.RemoveRange(await _context.AccountBalances.Where(item =>
                item.TenantId == tenantId && item.AccountingBookId == book.Id).ToListAsync(cancellationToken));
            _context.AccountCurrencyExposures.RemoveRange(await _context.AccountCurrencyExposures.Where(item =>
                item.TenantId == tenantId && item.AccountingBookId == book.Id).ToListAsync(cancellationToken));
            _context.AccountBalances.AddRange(balances);
            _context.AccountCurrencyExposures.AddRange(exposures);
            if (primaryCompatibility != null)
            {
                var accounts = await _context.Accounts.Where(item => item.TenantId == tenantId && !item.IsDeleted)
                    .ToListAsync(cancellationToken);
                foreach (var account in accounts)
                    account.Balance = primaryCompatibility.GetValueOrDefault(account.Id);
            }
            var run = new FinanceBalanceRebuildRun
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id,
                AccountingBookCode = book.Code, IdempotencyKey = request.IdempotencyKey!.Trim(),
                Reason = request.Reason!.Trim(), SourceFingerprint = fingerprint,
                CommandFingerprint = commandFingerprint,
                BalanceRows = balances.Count, ExposureRows = exposures.Count, AbsoluteDrift = absoluteDrift,
                RequestedByUserId = requestedByUserId, ApprovedByUserId = request.ApprovedByUserId!.Value,
                CompletedAt = DateTime.UtcNow, Status = "Completed", CreatedAt = DateTime.UtcNow
            };
            _context.FinanceBalanceRebuildRuns.Add(run);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return new(book.Id, book.Code, true, balanceDrift, exposureDrift, compatibilityDrift,
                absoluteDrift, fingerprint, run.Id);
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ApplyExposureDeltasAsync(Guid tenantId, Guid bookId, string bookCode,
        string functionalCurrency, FiscalPeriod currentPeriod,
        IReadOnlyCollection<AccountTransaction> lines, DateTime now,
        CancellationToken cancellationToken)
    {
        var foreign = lines.Where(line => !string.IsNullOrWhiteSpace(line.TransactionCurrency)
                && !string.Equals(line.TransactionCurrency,
                functionalCurrency, StringComparison.OrdinalIgnoreCase))
            .GroupBy(line => new { line.AccountId, Currency = line.TransactionCurrency!.ToUpperInvariant() });
        foreach (var group in foreign)
        {
            var row = await _context.AccountCurrencyExposures.SingleOrDefaultAsync(item =>
                item.TenantId == tenantId && item.AccountingBookId == bookId && item.AccountId == group.Key.AccountId
                && item.TransactionCurrencyCode == group.Key.Currency && !item.IsDeleted, cancellationToken);
            if (row == null)
            {
                row = new AccountCurrencyExposure { Id = Guid.NewGuid(), TenantId = tenantId,
                    AccountId = group.Key.AccountId, AccountingBookId = bookId, AccountingBookCode = bookCode,
                    FunctionalCurrencyCode = functionalCurrency, TransactionCurrencyCode = group.Key.Currency,
                    CreatedAt = now, LastRebuiltAt = now, SourceFingerprint = new string('0', 64) };
                _context.AccountCurrencyExposures.Add(row);
            }
            row.SignedForeignBalance = Round(row.SignedForeignBalance
                + group.Sum(line => line.TransactionDebitAmount.GetValueOrDefault() - line.TransactionCreditAmount.GetValueOrDefault()));
            row.SignedFunctionalBalance = Round(row.SignedFunctionalBalance
                + group.Sum(line => line.DebitAmount - line.CreditAmount));
            row.TransactionCount += group.Count();
            row.FirstTransactionDate = Min(row.FirstTransactionDate, group.Min(line => line.TransactionDate));
            row.LastTransactionDate = Max(row.LastTransactionDate, group.Max(line => line.TransactionDate));
            row.LastRebuiltAt = now;
            // Fingerprint the complete authoritative exposure source, including the currently tracked
            // posting lines. This keeps incremental and rebuilt projections comparable.
            var persisted = await _context.AccountTransactions.AsNoTracking()
                .Include(item => item.FiscalPeriod)
                .Where(item => item.TenantId == tenantId && item.AccountingBookId == bookId
                    && item.AccountId == group.Key.AccountId && item.PostingStatus == Posted
                    && !item.IsDeleted && item.TransactionCurrency == group.Key.Currency)
                .ToListAsync(cancellationToken);
            row.SourceFingerprint = Fingerprint(persisted.Concat(group)
                .GroupBy(item => item.Id).Select(items => items.First()), currentPeriod);
        }
    }

    private async Task ApplyPrimaryCompatibilityAsync(Guid tenantId, IEnumerable<AccountTransaction> lines,
        CancellationToken cancellationToken)
    {
        foreach (var group in lines.GroupBy(line => line.AccountId))
        {
            var account = await _context.Accounts.SingleAsync(item => item.TenantId == tenantId
                && item.Id == group.Key && !item.IsDeleted, cancellationToken);
            var signed = Round(group.Sum(line => line.DebitAmount - line.CreditAmount));
            var legacyNormalBalance = account.AccountType is AccountType.Liability or AccountType.Equity or AccountType.Revenue
                ? -signed : signed;
            account.Balance = Round(account.Balance + legacyNormalBalance);
        }
    }

    private async Task AcquireTenantProjectionLockAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsSqlServer()) return;
        if (_context.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Balance rebuild locking requires an active transaction.");
        // Use the same coarse C1 representation resource. Until C3 orchestration exists, posting and
        // rebuilding for a tenant must serialize so no line can be omitted or applied twice.
        var resource = $"FIN:POSTING-REPRESENTATION:{tenantId:N}";
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @result int;
EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
IF @result < 0 THROW 51000, 'Could not acquire Finance balance projection lock.', 1;", cancellationToken);
    }

    private async Task<AccountingBook> ResolveBookAsync(Guid tenantId, string code, CancellationToken token)
    {
        var matches = await _context.AccountingBooks.AsNoTracking().Where(item => item.TenantId == tenantId
            && item.Code == code && item.IsActive && item.AllowsPosting && !item.IsDeleted).Take(2).ToListAsync(token);
        if (matches.Count != 1 || !string.Equals(matches[0].Code, code, StringComparison.Ordinal))
            throw new KeyNotFoundException("The selected active accounting book is unavailable, noncanonical, or ambiguous.");
        return matches[0];
    }

    private async Task<AccountingBook> ResolvePrimaryCompatibilityBookAsync(Guid tenantId, CancellationToken token)
        => await ResolveUniqueDefaultBookAsync(tenantId, requireActivePosting: true, token);

    private async Task<AccountingBook> ResolveUniqueDefaultBookAsync(
        Guid tenantId,
        bool requireActivePosting,
        CancellationToken token)
    {
        var books = await _context.AccountingBooks.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsDefault && !item.IsDeleted
                && (!requireActivePosting || (item.IsActive && item.AllowsPosting)))
            .Take(2).ToListAsync(token);
        if (books.Count != 1)
            throw new InvalidOperationException(
                "PRIMARY_BOOK_AUTHORITY_AMBIGUOUS: Exactly one active default posting book is required.");
        return books[0];
    }

    private async Task<string> ResolveFunctionalCurrencyAsync(Guid tenantId, CancellationToken token)
    {
        var configured = await _context.FinanceSettings.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Take(2).Select(item => item.BaseCurrency).ToListAsync(token);
        if (configured.Count > 1)
            throw new InvalidOperationException("Tenant functional-currency authority is ambiguous.");
        var value = configured.Count == 1 ? configured[0] : await _context.Tenants.AsNoTracking()
            .Where(item => item.Id == tenantId && !item.IsDeleted)
            .Select(item => item.BaseCurrency).SingleOrDefaultAsync(token);
        if (string.IsNullOrWhiteSpace(value) || value.Length != 3
            || !string.Equals(value, value.ToUpperInvariant(), StringComparison.Ordinal))
            throw new InvalidOperationException("Tenant functional-currency authority is unavailable or noncanonical.");
        return value;
    }

    private static List<AccountBalance> BuildBalances(Guid tenantId, AccountingBook book, List<AccountTransaction> transactions)
    {
        var result = new List<AccountBalance>();
        foreach (var accountYear in transactions.GroupBy(item => new
                 { item.AccountId, item.FiscalPeriod.FiscalYearId, item.FunctionalCurrencyCode }))
        {
            decimal opening = 0m, ytdDr = 0m, ytdCr = 0m;
            foreach (var period in accountYear.GroupBy(item => new { item.FiscalPeriodId, item.FiscalPeriod.PeriodNumber })
                         .OrderBy(item => item.Key.PeriodNumber))
            {
                var dr = Round(period.Sum(item => item.DebitAmount)); var cr = Round(period.Sum(item => item.CreditAmount));
                ytdDr = Round(ytdDr + dr); ytdCr = Round(ytdCr + cr); var closing = Round(opening + dr - cr);
                result.Add(new AccountBalance { Id = Guid.NewGuid(), TenantId = tenantId, AccountId = accountYear.Key.AccountId,
                    AccountingBookId = book.Id, BookClassification = book.Code, FiscalPeriodId = period.Key.FiscalPeriodId,
                    Currency = period.First().FunctionalCurrencyCode, OpeningBalance = opening, OpeningBalanceType = Side(opening),
                    PeriodDebits = dr, PeriodCredits = cr, PeriodNetMovement = Round(dr - cr), ClosingBalance = closing,
                    ClosingBalanceType = Side(closing), YearToDateDebits = ytdDr, YearToDateCredits = ytdCr,
                    YearToDateNetMovement = Round(ytdDr - ytdCr), TransactionCount = period.Count(),
                    LastTransactionDate = period.Max(item => item.TransactionDate), LastUpdated = DateTime.UtcNow,
                    LastReconciledDate = DateTime.UtcNow, IsReconciled = true, HasActivity = true,
                    IsZeroBalance = closing == 0m, IsNegativeBalance = closing < 0m });
                opening = closing;
            }
        }
        return result;
    }

    private static List<AccountCurrencyExposure> BuildExposures(Guid tenantId, AccountingBook book, List<AccountTransaction> transactions) =>
        transactions.Where(item => !string.IsNullOrWhiteSpace(item.TransactionCurrency)
                && !string.Equals(item.TransactionCurrency, item.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => new { item.AccountId, Functional = item.FunctionalCurrencyCode, Currency = item.TransactionCurrency! })
            .Select(group => new AccountCurrencyExposure { Id = Guid.NewGuid(), TenantId = tenantId,
                AccountId = group.Key.AccountId, AccountingBookId = book.Id, AccountingBookCode = book.Code,
                FunctionalCurrencyCode = group.Key.Functional, TransactionCurrencyCode = group.Key.Currency,
                SignedForeignBalance = Round(group.Sum(item => item.TransactionDebitAmount.GetValueOrDefault() - item.TransactionCreditAmount.GetValueOrDefault())),
                SignedFunctionalBalance = Round(group.Sum(item => item.DebitAmount - item.CreditAmount)),
                TransactionCount = group.Count(), FirstTransactionDate = group.Min(item => item.TransactionDate),
                LastTransactionDate = group.Max(item => item.TransactionDate), LastRebuiltAt = DateTime.UtcNow,
                SourceFingerprint = Fingerprint(group) }).ToList();

    private static int CountBalanceDrift(List<AccountBalance> expected, List<AccountBalance> actual) =>
        expected.Count(item => !actual.Any(row => BalanceEquivalent(item, row)))
        + actual.Count(item => !expected.Any(row => BalanceEquivalent(row, item)));

    private static int CountExposureDrift(List<AccountCurrencyExposure> expected, List<AccountCurrencyExposure> actual) =>
        expected.Count(item => !actual.Any(row => ExposureEquivalent(item, row)))
        + actual.Count(item => !expected.Any(row => ExposureEquivalent(row, item)));

    private static bool BalanceEquivalent(AccountBalance expected, AccountBalance actual) =>
        expected.TenantId == actual.TenantId && expected.AccountId == actual.AccountId
        && expected.AccountingBookId == actual.AccountingBookId
        && string.Equals(expected.BookClassification, actual.BookClassification, StringComparison.Ordinal)
        && expected.FiscalPeriodId == actual.FiscalPeriodId
        && string.Equals(expected.Currency, actual.Currency, StringComparison.Ordinal)
        && expected.OpeningBalance == actual.OpeningBalance
        && expected.OpeningBalanceType == actual.OpeningBalanceType
        && expected.PeriodDebits == actual.PeriodDebits && expected.PeriodCredits == actual.PeriodCredits
        && expected.PeriodNetMovement == actual.PeriodNetMovement
        && expected.ClosingBalance == actual.ClosingBalance
        && expected.ClosingBalanceType == actual.ClosingBalanceType
        && expected.YearToDateDebits == actual.YearToDateDebits
        && expected.YearToDateCredits == actual.YearToDateCredits
        && expected.YearToDateNetMovement == actual.YearToDateNetMovement
        && expected.TransactionCount == actual.TransactionCount
        && expected.LastTransactionDate == actual.LastTransactionDate
        && expected.HasActivity == actual.HasActivity
        && expected.IsZeroBalance == actual.IsZeroBalance
        && expected.IsNegativeBalance == actual.IsNegativeBalance
        && expected.IsReconciled == actual.IsReconciled;

    private static bool ExposureEquivalent(AccountCurrencyExposure expected, AccountCurrencyExposure actual) =>
        expected.TenantId == actual.TenantId && expected.AccountId == actual.AccountId
        && expected.AccountingBookId == actual.AccountingBookId
        && string.Equals(expected.AccountingBookCode, actual.AccountingBookCode, StringComparison.Ordinal)
        && string.Equals(expected.FunctionalCurrencyCode, actual.FunctionalCurrencyCode, StringComparison.Ordinal)
        && string.Equals(expected.TransactionCurrencyCode, actual.TransactionCurrencyCode, StringComparison.Ordinal)
        && expected.SignedForeignBalance == actual.SignedForeignBalance
        && expected.SignedFunctionalBalance == actual.SignedFunctionalBalance
        && expected.TransactionCount == actual.TransactionCount
        && expected.FirstTransactionDate == actual.FirstTransactionDate
        && expected.LastTransactionDate == actual.LastTransactionDate
        && string.Equals(expected.SourceFingerprint, actual.SourceFingerprint, StringComparison.Ordinal);

    private static decimal AbsoluteBalanceDrift(List<AccountBalance> expected, List<AccountBalance> actual)
    {
        var keys = expected.Select(item => (item.AccountId, item.FiscalPeriodId, item.Currency))
            .Concat(actual.Select(item => (item.AccountId, item.FiscalPeriodId, item.Currency))).Distinct();
        return Round(keys.Sum(key =>
        {
            var expectedBalance = expected.SingleOrDefault(item =>
                (item.AccountId, item.FiscalPeriodId, item.Currency) == key)?.ClosingBalance ?? 0m;
            var actualBalance = actual.SingleOrDefault(item =>
                (item.AccountId, item.FiscalPeriodId, item.Currency) == key)?.ClosingBalance ?? 0m;
            return Math.Abs(expectedBalance - actualBalance);
        }));
    }

    private static string Fingerprint(IEnumerable<AccountTransaction> rows, FiscalPeriod? fallbackPeriod = null)
    {
        var text = new StringBuilder("RHEMA-FINANCE-BOOK-BALANCE-SOURCE|V2\n");
        foreach (var row in rows.OrderBy(item => item.Id))
        {
            var period = row.FiscalPeriod
                ?? (fallbackPeriod?.Id == row.FiscalPeriodId ? fallbackPeriod : null)
                ?? throw new InvalidOperationException("Fiscal-period evidence is required for balance fingerprinting.");
            text.Append(row.Id.ToString("N")).Append('|').Append(row.TenantId.ToString("N")).Append('|')
                .Append(row.AccountingBookId.ToString("N")).Append('|').Append(row.BookClassification).Append('|')
                .Append(row.AccountId.ToString("N")).Append('|').Append(row.FiscalPeriodId.ToString("N")).Append('|')
                .Append(period.FiscalYearId.ToString("N")).Append('|')
                .Append(period.PeriodNumber.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(period.StartDate.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(period.EndDate.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.TransactionDate.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.FunctionalCurrencyCode).Append('|').Append(row.TransactionCurrency).Append('|')
                .Append(row.DebitAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.CreditAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.TransactionDebitAmount.GetValueOrDefault().ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.TransactionCreditAmount.GetValueOrDefault().ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.PostingStatus).Append('|').Append(row.IsDeleted ? '1' : '0').Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static string RebuildCommandFingerprint(Guid tenantId, AccountingBook book,
        BookBalanceReconciliationRequestDto request, Guid requestedByUserId, string sourceFingerprint)
    {
        var evidence = string.Join("|", "RHEMA-FINANCE-BOOK-BALANCE-REBUILD-COMMAND", "V1",
            tenantId.ToString("N"), book.Id.ToString("N"), book.Code,
            request.IdempotencyKey!.Trim(), request.Reason!.Trim(),
            requestedByUserId.ToString("N"), request.ApprovedByUserId!.Value.ToString("N"), sourceFingerprint);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
    }

    private async Task<(Dictionary<Guid, Account> Accounts, string Fingerprint)>
        LoadPrimaryCompatibilityAuthorityAsync(Guid tenantId, AccountingBook book,
            IReadOnlyCollection<AccountTransaction> transactions, CancellationToken cancellationToken)
    {
        var accountIds = transactions.Select(item => item.AccountId).Distinct().OrderBy(item => item).ToArray();
        var accounts = await _context.Accounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && accountIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (accounts.Count != accountIds.Length)
            throw new InvalidOperationException("Primary compatibility evidence references an unavailable account.");

        var mappings = await _context.AccountAccountingBooks.AsNoTracking()
            .Include(item => item.AccountClassification)
            .Where(item => item.TenantId == tenantId && item.AccountingBookId == book.Id
                && accountIds.Contains(item.AccountId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var accountId in accountIds)
        {
            var eligible = mappings.Where(item => item.AccountId == accountId && item.IsEnabled).ToArray();
            var account = accounts[accountId];
            if (eligible.Length != 1 || eligible[0].AccountClassification is not { } classification
                || classification.TenantId != tenantId || classification.AccountingBookId != book.Id
                || classification.Status != AccountClassificationStatus.Active
                || !classification.IsPostingClassification || classification.IsDeleted
                || classification.CoreAccountType != account.AccountType)
                throw new InvalidOperationException(
                    "Primary compatibility rebuild requires one enabled compatible account/book classification mapping.");
        }

        return (accounts, CompatibilityAuthorityFingerprint(accounts.Values, mappings.Where(item => item.IsEnabled)));
    }

    private static string CompatibilityAuthorityFingerprint(
        IEnumerable<Account> accounts, IEnumerable<AccountAccountingBook> mappings)
    {
        var text = new StringBuilder("RHEMA-FINANCE-PRIMARY-COMPATIBILITY-AUTHORITY|V1\n");
        foreach (var account in accounts.OrderBy(item => item.Id))
            text.Append("ACCOUNT|").Append(account.Id.ToString("N")).Append('|')
                .Append(account.TenantId.ToString("N")).Append('|').Append(account.AccountCode).Append('|')
                .Append(account.AccountNumber).Append('|').Append((int)account.AccountType).Append('\n');
        foreach (var mapping in mappings.OrderBy(item => item.AccountId).ThenBy(item => item.Id))
        {
            var classification = mapping.AccountClassification
                ?? throw new InvalidOperationException("Primary compatibility classification evidence is unavailable.");
            text.Append("MAPPING|").Append(mapping.Id.ToString("N")).Append('|')
                .Append(mapping.TenantId.ToString("N")).Append('|').Append(mapping.AccountId.ToString("N")).Append('|')
                .Append(mapping.AccountingBookId.ToString("N")).Append('|')
                .Append(mapping.AccountClassificationId?.ToString("N")).Append('|').Append(mapping.IsEnabled ? '1' : '0').Append('|')
                .Append(classification.Id.ToString("N")).Append('|').Append(classification.TenantId.ToString("N")).Append('|')
                .Append(classification.AccountingBookId.ToString("N")).Append('|').Append(classification.Code).Append('|')
                .Append((int)classification.CoreAccountType).Append('|').Append((int)classification.Status).Append('|')
                .Append(classification.IsPostingClassification ? '1' : '0').Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static string ReconciliationFingerprint(string transactionFingerprint, string authorityFingerprint)
    {
        var evidence = $"RHEMA-FINANCE-BOOK-BALANCE-RECONCILIATION|V2|{transactionFingerprint}|{authorityFingerprint}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
    }

    private static string RequireCode(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("Accounting book code is required.", nameof(value)) : value.Trim().ToUpperInvariant();
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string Side(decimal value) => value < 0m ? "CR" : "DR";
    private static DateTime Min(DateTime? current, DateTime candidate) => current.HasValue && current.Value < candidate ? current.Value : candidate;
    private static DateTime Max(DateTime? current, DateTime candidate) => current.HasValue && current.Value > candidate ? current.Value : candidate;

    private static Dictionary<Guid, decimal> BuildPrimaryCompatibility(
        IEnumerable<AccountTransaction> transactions, IReadOnlyDictionary<Guid, Account> accounts)
    {
        var result = accounts.Keys.ToDictionary(id => id, _ => 0m);
        foreach (var group in transactions.GroupBy(item => item.AccountId))
        {
            if (!accounts.TryGetValue(group.Key, out var account))
                throw new InvalidOperationException("Primary-book transaction references an unavailable account.");
            var signed = Round(group.Sum(item => item.DebitAmount - item.CreditAmount));
            result[group.Key] = account.AccountType is AccountType.Liability or AccountType.Equity or AccountType.Revenue
                ? -signed : signed;
        }
        return result;
    }
}
