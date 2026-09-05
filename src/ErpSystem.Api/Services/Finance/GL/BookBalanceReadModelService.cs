using System.Globalization;
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
            row.LastTransactionDate = group.Max(line => line.TransactionDate);
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
            functionalCurrencyCode, active, postedAt, cancellationToken);

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
        return new BookBalanceInquiryDto(book.Id, book.Code, balances, exposures);
    }

    public async Task<BookBalanceReconciliationDto> ReconcileAsync(Guid tenantId,
        BookBalanceReconciliationRequestDto request, Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var book = await ResolveBookAsync(tenantId, RequireCode(request.AccountingBookCode), cancellationToken);
        if (request.Apply && (string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || !request.ApprovedByUserId.HasValue || request.ApprovedByUserId == Guid.Empty
            || request.ApprovedByUserId == requestedByUserId))
            throw new InvalidOperationException("An approved rebuild requires a reason, idempotency key, and a different maker/checker user.");

        var ownsRebuildTransaction = request.Apply && _context.Database.CurrentTransaction == null;
        await using var rebuildTransaction = ownsRebuildTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        if (request.Apply)
            await AcquireTenantProjectionLockAsync(tenantId, cancellationToken);

        var transactions = await _context.AccountTransactions.AsNoTracking()
            .Include(item => item.FiscalPeriod)
            .Where(item => item.TenantId == tenantId && item.AccountingBookId == book.Id
                && item.BookClassification == book.Code && item.PostingStatus == Posted && !item.IsDeleted)
            .OrderBy(item => item.FiscalPeriod.FiscalYearId).ThenBy(item => item.FiscalPeriod.PeriodNumber)
            .ThenBy(item => item.AccountId).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        if (transactions.Any(item => !string.Equals(item.BookClassification, book.Code, StringComparison.Ordinal)))
            throw new InvalidOperationException("Transaction book snapshots do not exactly match the selected accounting book.");
        var expectedBalances = BuildBalances(tenantId, book, transactions);
        var expectedExposures = BuildExposures(tenantId, book, transactions);
        var fingerprint = Fingerprint(transactions);
        var existingBalances = await _context.AccountBalances.AsNoTracking().Where(item =>
            item.TenantId == tenantId && item.AccountingBookId == book.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        var existingExposures = await _context.AccountCurrencyExposures.AsNoTracking().Where(item =>
            item.TenantId == tenantId && item.AccountingBookId == book.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        var balanceDrift = CountBalanceDrift(expectedBalances, existingBalances);
        var exposureDrift = CountExposureDrift(expectedExposures, existingExposures);
        var absoluteDrift = AbsoluteBalanceDrift(expectedBalances, existingBalances);

        if (!request.Apply)
            return new(book.Id, book.Code, false, balanceDrift, exposureDrift, absoluteDrift, fingerprint, null);

        var result = await ExecuteRebuildAsync(tenantId, book, request, requestedByUserId, expectedBalances,
            expectedExposures, fingerprint, balanceDrift, exposureDrift, absoluteDrift, cancellationToken);
        if (rebuildTransaction != null)
            await rebuildTransaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<BookBalanceReconciliationDto> ExecuteRebuildAsync(Guid tenantId, AccountingBook book,
        BookBalanceReconciliationRequestDto request, Guid requestedByUserId, List<AccountBalance> balances,
        List<AccountCurrencyExposure> exposures, string fingerprint, int balanceDrift, int exposureDrift,
        decimal absoluteDrift, CancellationToken cancellationToken)
    {
        var existingRun = await _context.FinanceBalanceRebuildRuns.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.AccountingBookId == book.Id
            && item.IdempotencyKey == request.IdempotencyKey!.Trim() && !item.IsDeleted, cancellationToken);
        if (existingRun != null)
        {
            if (!string.Equals(existingRun.SourceFingerprint, fingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException("The rebuild idempotency key was already used for different source evidence.");
            return new(book.Id, book.Code, true, 0, 0, 0m, fingerprint, existingRun.Id);
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
            var run = new FinanceBalanceRebuildRun
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id,
                AccountingBookCode = book.Code, IdempotencyKey = request.IdempotencyKey!.Trim(),
                Reason = request.Reason!.Trim(), SourceFingerprint = fingerprint,
                BalanceRows = balances.Count, ExposureRows = exposures.Count, AbsoluteDrift = absoluteDrift,
                RequestedByUserId = requestedByUserId, ApprovedByUserId = request.ApprovedByUserId!.Value,
                CompletedAt = DateTime.UtcNow, Status = "Completed", CreatedAt = DateTime.UtcNow
            };
            _context.FinanceBalanceRebuildRuns.Add(run);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return new(book.Id, book.Code, true, balanceDrift, exposureDrift, absoluteDrift, fingerprint, run.Id);
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ApplyExposureDeltasAsync(Guid tenantId, Guid bookId, string bookCode,
        string functionalCurrency, IReadOnlyCollection<AccountTransaction> lines, DateTime now,
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
            // This rolling evidence is not the journal authority; it makes incremental projection changes
            // inspectable until a governed rebuild replaces it with the canonical full-source fingerprint.
            row.SourceFingerprint = RollingFingerprint(row.SourceFingerprint, group);
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
        expected.Count(item => !actual.Any(row => row.AccountId == item.AccountId && row.FiscalPeriodId == item.FiscalPeriodId
            && row.Currency == item.Currency && row.PeriodDebits == item.PeriodDebits && row.PeriodCredits == item.PeriodCredits
            && row.OpeningBalance == item.OpeningBalance && row.ClosingBalance == item.ClosingBalance))
        + actual.Count(item => !expected.Any(row => row.AccountId == item.AccountId && row.FiscalPeriodId == item.FiscalPeriodId && row.Currency == item.Currency));

    private static int CountExposureDrift(List<AccountCurrencyExposure> expected, List<AccountCurrencyExposure> actual) =>
        expected.Count(item => !actual.Any(row => row.AccountId == item.AccountId && row.TransactionCurrencyCode == item.TransactionCurrencyCode
            && row.SignedForeignBalance == item.SignedForeignBalance && row.SignedFunctionalBalance == item.SignedFunctionalBalance))
        + actual.Count(item => !expected.Any(row => row.AccountId == item.AccountId && row.TransactionCurrencyCode == item.TransactionCurrencyCode));

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

    private static string Fingerprint(IEnumerable<AccountTransaction> rows)
    {
        var text = new StringBuilder("RHEMA-FINANCE-BOOK-BALANCE-SOURCE|V1\n");
        foreach (var row in rows.OrderBy(item => item.Id))
            text.Append(row.Id.ToString("N")).Append('|').Append(row.TenantId.ToString("N")).Append('|')
                .Append(row.AccountingBookId.ToString("N")).Append('|').Append(row.BookClassification).Append('|')
                .Append(row.AccountId.ToString("N")).Append('|').Append(row.FiscalPeriodId.ToString("N")).Append('|')
                .Append(row.FunctionalCurrencyCode).Append('|').Append(row.TransactionCurrency).Append('|')
                .Append(row.DebitAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.CreditAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.TransactionDebitAmount.GetValueOrDefault().ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
                .Append(row.TransactionCreditAmount.GetValueOrDefault().ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static string RollingFingerprint(string prior, IEnumerable<AccountTransaction> rows)
    {
        var evidence = $"RHEMA-FINANCE-BOOK-EXPOSURE-DELTA|V1|{prior}|{Fingerprint(rows)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
    }

    private static string RequireCode(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("Accounting book code is required.", nameof(value)) : value.Trim().ToUpperInvariant();
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string Side(decimal value) => value < 0m ? "CR" : "DR";
    private static DateTime Min(DateTime? current, DateTime candidate) => current.HasValue && current.Value < candidate ? current.Value : candidate;
    private static DateTime Max(DateTime? current, DateTime candidate) => current.HasValue && current.Value > candidate ? current.Value : candidate;
}
