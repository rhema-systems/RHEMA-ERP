using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Finance.MultiCurrency
{
    /// <summary>
    /// Service implementation for foreign currency revaluation operations.
    /// Handles unrealized gain/loss calculations and journal entry generation.
    /// </summary>
    public class CurrencyRevaluationService : ICurrencyRevaluationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ReportingDbContext _reportingContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<CurrencyRevaluationService> _logger;

        public CurrencyRevaluationService(
            ApplicationDbContext context,
            ReportingDbContext reportingContext,
            ICurrentUserService currentUserService,
            ITenantSettingsService tenantSettingsService,
            ILogger<CurrencyRevaluationService> logger)
        {
            _context = context;
            _reportingContext = reportingContext;
            _currentUserService = currentUserService;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant context is required.");

        public async Task<JournalEntry> RunCurrencyRevaluationAsync(
            RevaluationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                return await RunCurrencyRevaluationCoreAsync(request, cancellationToken);
            });
        }

        private async Task<JournalEntry> RunCurrencyRevaluationCoreAsync(
            RevaluationRequestDto request,
            CancellationToken cancellationToken)
        {
            var tenantIdValue = TenantId;
            var revaluationDate = request.RevaluationDate.Date;
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            // 1. Get Accounts to Revalue
            var allAccounts = await _reportingContext.Accounts
                .Where(a => a.TenantId == tenantIdValue
                         && !a.IsDeleted
                         && a.Status == AccountStatus.Active)
                .ToListAsync(cancellationToken);

            // Load currency links separately
            var accountIds = allAccounts.Select(a => a.Id).ToList();
            var currencyLinks = await _context.AccountCurrencyLinks
                .IgnoreQueryFilters()
                .Where(cl => accountIds.Contains(cl.AccountId) && cl.IsActive && !cl.IsDeleted)
                .ToListAsync(cancellationToken);

            // Attach currency links to accounts (in-memory)
            foreach (var account in allAccounts)
            {
                account.CurrencyLinks = currencyLinks.Where(cl => cl.AccountId == account.Id).ToList();
            }

            // Filter accounts based on currency
            var accounts = new List<Account>();
            if (!string.IsNullOrEmpty(request.CurrencyCode))
            {
                accounts = allAccounts.Where(a =>
                    a.CurrencyCode == request.CurrencyCode
                    || a.IsMultiCurrency
                    || a.CurrencyLinks.Any(c => c.LinkedCurrencyCode == request.CurrencyCode))
                    .ToList();
            }
            else
            {
                accounts = allAccounts.Where(a =>
                    a.IsMultiCurrency
                    || !string.Equals(a.CurrencyCode, baseCurrencyCode, StringComparison.OrdinalIgnoreCase)
                    || a.CurrencyLinks.Any())
                    .ToList();
            }

            // 2. Prepare Journal Entry
            var journalEntry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                JournalEntryNumber = $"REV-{revaluationDate:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8)}",
                EntryDate = revaluationDate,
                Description = $"Currency Revaluation - {request.RevaluationType} - {revaluationDate:d}",
                ReferenceNumber = request.RevaluationType,
                PostingStatus = request.PreviewOnly ? "Draft" : "Posted",
                PostingDate = DateTime.UtcNow,
                IsBalanced = true,
                BookClassification = "IFRS",
                TenantId = tenantIdValue,
                FiscalPeriodId = await GetOpenFiscalPeriodIdAsync(revaluationDate, cancellationToken)
            };

            var transactions = new List<AccountTransaction>();
            decimal totalAdjustment = 0;
            int lineNum = 1;

            // 3. Process Each Account
            foreach (var account in accounts)
            {
                var accountTransactions = await _context.AccountTransactions
                    .Where(t => t.AccountId == account.Id && t.TransactionDate <= revaluationDate && !t.IsDeleted)
                    .ToListAsync(cancellationToken);

                // Group by currency
                var currencyGroups = accountTransactions
                    .Where(t => t.TransactionCurrency != null
                        && !string.Equals(t.TransactionCurrency, baseCurrencyCode, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(t => t.TransactionCurrency);

                foreach (var group in currencyGroups)
                {
                    string currency = group.Key!;
                    if (!string.IsNullOrEmpty(request.CurrencyCode) && currency != request.CurrencyCode) continue;

                    // Get balance from AccountCurrencyLink
                    var currencyLink = account.CurrencyLinks
                        .FirstOrDefault(c => c.LinkedCurrencyCode == currency && c.IsActive);

                    decimal foreignBalance = currencyLink?.ForeignCurrencyBalance
                        ?? group.Sum(t => t.ForeignCurrencyAmount ?? 0);
                    decimal currentBaseBalance = group.Sum(t => t.DebitAmount - t.CreditAmount);

                    if (foreignBalance == 0 && currentBaseBalance == 0) continue;

                    // Get Exchange Rate
                    var exchangeRateEntity = await _context.ExchangeRates
                        .Where(r => r.TargetCurrencyCode == currency
                            && r.BaseCurrencyCode == baseCurrencyCode
                            && r.EffectiveDate <= revaluationDate)
                        .OrderByDescending(r => r.EffectiveDate)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (exchangeRateEntity == null)
                    {
                        _logger.LogWarning("No exchange rate found for {Currency} as of {Date}", currency, revaluationDate);
                        continue;
                    }

                    decimal rate = exchangeRateEntity.Rate;
                    decimal newBaseBalance = foreignBalance * rate;
                    decimal adjustment = newBaseBalance - currentBaseBalance;

                    if (adjustment == 0) continue;

                    // Update AccountCurrencyLink metadata
                    if (currencyLink != null)
                    {
                        currencyLink.LastRevaluationDate = revaluationDate;
                        currencyLink.CurrentExchangeRate = rate;
                        currencyLink.RateEffectiveDate = revaluationDate;
                        currencyLink.LastRevaluationAdjustment = adjustment;
                        currencyLink.CumulativeRevaluationAdjustment += adjustment;
                    }

                    // Create Adjustment Transaction
                    var adjustmentTxn = new AccountTransaction
                    {
                        Id = Guid.NewGuid(),
                        AccountId = account.Id,
                        JournalEntryId = journalEntry.Id,
                        TransactionDate = revaluationDate,
                        Description = $"Revaluation {currency} @ {rate}",
                        TransactionCurrency = currency,
                        ForeignCurrencyAmount = 0,
                        ExchangeRate = rate,
                        IsRevaluationEntry = true,
                        RevaluationType = "Unrealized",
                        FiscalPeriodId = journalEntry.FiscalPeriodId,
                        LineNumber = lineNum++,
                        BookClassification = "IFRS",
                        TenantId = tenantIdValue
                    };

                    if (adjustment > 0)
                    {
                        adjustmentTxn.DebitAmount = adjustment;
                        adjustmentTxn.CreditAmount = 0;
                    }
                    else
                    {
                        adjustmentTxn.DebitAmount = 0;
                        adjustmentTxn.CreditAmount = Math.Abs(adjustment);
                    }

                    transactions.Add(adjustmentTxn);
                    totalAdjustment += adjustment;
                }
            }

            if (transactions.Count == 0)
            {
                _logger.LogInformation("No revaluation adjustments needed for {Date}", revaluationDate);
                return journalEntry;
            }

            // 4. Post Balancing Entry to Unrealized Gain/Loss Account
            var balancingTxn = new AccountTransaction
            {
                Id = Guid.NewGuid(),
                AccountId = request.UnrealizedGainLossAccountId,
                JournalEntryId = journalEntry.Id,
                TransactionDate = revaluationDate,
                Description = "Unrealized Gain/Loss - Revaluation",
                IsRevaluationEntry = true,
                RevaluationType = "Unrealized",
                FiscalPeriodId = journalEntry.FiscalPeriodId,
                LineNumber = lineNum++,
                BookClassification = "IFRS",
                TenantId = tenantIdValue
            };

            if (totalAdjustment > 0)
            {
                balancingTxn.CreditAmount = totalAdjustment;
                balancingTxn.DebitAmount = 0;
            }
            else
            {
                balancingTxn.DebitAmount = Math.Abs(totalAdjustment);
                balancingTxn.CreditAmount = 0;
            }
            transactions.Add(balancingTxn);

            // Update Header Totals
            journalEntry.TotalDebitAmount = transactions.Sum(t => t.DebitAmount);
            journalEntry.TotalCreditAmount = transactions.Sum(t => t.CreditAmount);

            if (!request.PreviewOnly)
            {
                _context.JournalEntries.Add(journalEntry);
                _context.AccountTransactions.AddRange(transactions);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Currency revaluation completed: {JournalNumber}, {Count} adjustments, Total: {Total}",
                    journalEntry.JournalEntryNumber, transactions.Count - 1, totalAdjustment);
            }
            else
            {
                journalEntry.Transactions = transactions;
                _logger.LogInformation("Currency revaluation preview generated: {Count} adjustments, Total: {Total}",
                    transactions.Count - 1, totalAdjustment);
            }

            return journalEntry;
        }

        public async Task<IReadOnlyList<JournalEntry>> GetRevaluationHistoryAsync(
            DateTime startDate,
            DateTime endDate,
            string? currencyCode = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.JournalEntries
                .Where(je => je.TenantId == TenantId
                    && je.EntryDate >= startDate
                    && je.EntryDate <= endDate
                    && je.ReferenceNumber != null
                    && je.ReferenceNumber.Contains("Revaluation")
                    && !je.IsDeleted)
                .Include(je => je.Transactions)
                .OrderByDescending(je => je.EntryDate);

            var entries = await query.ToListAsync(cancellationToken);

            if (!string.IsNullOrEmpty(currencyCode))
            {
                entries = entries.Where(je => je.Transactions.Any(t => t.TransactionCurrency == currencyCode)).ToList();
            }

            return entries;
        }

        private async Task<Guid> GetOpenFiscalPeriodIdAsync(DateTime date, CancellationToken cancellationToken)
        {
            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date && p.TenantId == TenantId, cancellationToken);

            if (period != null) return period.Id;

            // Auto-create if missing
            var fiscalYear = await _context.FiscalYears
                .FirstOrDefaultAsync(fy => fy.TenantId == TenantId && fy.StartDate <= date && fy.EndDate >= date, cancellationToken)
                ?? new FiscalYear
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    FiscalYearCode = date.Year.ToString(),
                    StartDate = new DateTime(date.Year, 1, 1),
                    EndDate = new DateTime(date.Year, 12, 31),
                    IsActive = true
                };

            if (_context.Entry(fiscalYear).State == EntityState.Detached)
            {
                _context.FiscalYears.Add(fiscalYear);
                await _context.SaveChangesAsync(cancellationToken);
            }

            period = new FiscalPeriod
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                FiscalYearId = fiscalYear.Id,
                PeriodName = $"{date:MMMM yyyy}",
                PeriodCode = $"{date:yyyy-MM}",
                PeriodNumber = date.Month,
                StartDate = new DateTime(date.Year, date.Month, 1),
                EndDate = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)),
                PeriodStatus = "Open",
                IsOpen = true
            };
            _context.FiscalPeriods.Add(period);
            await _context.SaveChangesAsync(cancellationToken);

            return period.Id;
        }
    }
}
