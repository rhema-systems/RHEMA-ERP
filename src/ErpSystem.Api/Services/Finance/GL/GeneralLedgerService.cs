using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;

using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Api.Services.Finance.GL
{
    public class GeneralLedgerService : IGeneralLedgerService
    {
        private readonly ApplicationDbContext _context;
        private readonly ReportingDbContext _reportingContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantSettingsService _tenantSettings;
        private readonly IFiscalPeriodService _fiscalPeriodService;
        private readonly IDocumentNumberingService _documentNumberingService;
        private readonly IAccountingBookService _accountingBookService;
        private readonly IFinancePostingEngine _financePostingEngine;
        private readonly IFinancialStatementLayoutExecutionService? _statementLayoutExecutionService;
        private readonly FinanceDimensionReportingFilterService? _dimensionReportingFilters;
        private readonly IAccountSegmentIdentityService _segmentIdentityService;

        public GeneralLedgerService(
            ApplicationDbContext context,
            ReportingDbContext reportingContext,
            ICurrentUserService currentUserService,
            ITenantSettingsService tenantSettings,
            IFiscalPeriodService fiscalPeriodService,
            IDocumentNumberingService documentNumberingService,
            IAccountingBookService accountingBookService,
            IFinancePostingEngine financePostingEngine,
            IFinancialStatementLayoutExecutionService? statementLayoutExecutionService = null,
            FinanceDimensionReportingFilterService? dimensionReportingFilters = null,
            IAccountSegmentIdentityService? segmentIdentityService = null)
        {
            _context = context;
            _reportingContext = reportingContext;
            _currentUserService = currentUserService;
            _tenantSettings = tenantSettings;
            _fiscalPeriodService = fiscalPeriodService;
            _documentNumberingService = documentNumberingService;
            _accountingBookService = accountingBookService;
            _financePostingEngine = financePostingEngine;
            _statementLayoutExecutionService = statementLayoutExecutionService;
            _dimensionReportingFilters = dimensionReportingFilters;
            _segmentIdentityService = segmentIdentityService ?? new ErpSystem.Api.Services.Finance.Segments.AccountSegmentIdentityService(context);
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

        #region Account Management

        public async Task<Account> CreateSegmentedAccountAsync(AccountCreateDto accountDto)
        {
            try
            {
                // Validate input DTO
                if (accountDto == null)
                    throw new ArgumentNullException(nameof(accountDto), "Account DTO cannot be null");

                if (string.IsNullOrWhiteSpace(accountDto.AccountNumber))
                    throw new ArgumentException("Account number is required", nameof(accountDto.AccountNumber));

                if (string.IsNullOrWhiteSpace(accountDto.AccountName))
                    throw new ArgumentException("Account name is required", nameof(accountDto.AccountName));

                if (string.IsNullOrWhiteSpace(accountDto.AccountType))
                    throw new ArgumentException("Account type is required", nameof(accountDto.AccountType));

                // Get tenant ID - throw if not available
                var tenantId = TenantId;
                Console.WriteLine($"DEBUG: TenantId from CurrentUserService: {tenantId}");
                Console.WriteLine($"DEBUG: TenantId is Guid.Empty: {tenantId == Guid.Empty}");
                
                if (tenantId == Guid.Empty)
                    throw new InvalidOperationException("Tenant context is required for account creation. User must be authenticated with a valid tenant.");

                // 1. Validate the exact active identity and compose the account number on the server.
                var identity = await _segmentIdentityService.ValidateAndComposeAsync(
                    tenantId, accountDto.SegmentValues, accountDto.AccountNumber);
                if (!string.IsNullOrWhiteSpace(accountDto.AccountCode)
                    && !string.Equals(accountDto.AccountCode.Trim(), identity.NaturalAccountCode, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Account code must match the Natural Account segment value '{identity.NaturalAccountCode}'.");
                await _accountingBookService.EnsureTenantDefaultsAsync();

                // 2. Parse AccountType safely
                if (!Enum.TryParse<AccountType>(accountDto.AccountType, true, out var accountType))
                    throw new ArgumentException($"Invalid account type: {accountDto.AccountType}. Valid values are: {string.Join(", ", Enum.GetNames(typeof(AccountType)))}", nameof(accountDto.AccountType));

                // 3. Map DTO to Entity
                var now = DateTime.UtcNow;
                var userName = _currentUserService.UserName ?? "system";
                var baseCurrencyCode = await _tenantSettings.GetBaseCurrencyAsync();

                var account = new Account
                {
                    Id = Guid.NewGuid(),
                    AccountCode = identity.NaturalAccountCode,
                    AccountNumber = identity.AccountNumber,
                    AccountName = accountDto.AccountName,
                    AccountType = accountType,
                    AccountCategory = accountDto.AccountCategory,
                    AccountSubCategory = accountDto.AccountSubCategory,
                    CurrencyCode = string.IsNullOrWhiteSpace(accountDto.CurrencyCode) ? baseCurrencyCode : accountDto.CurrencyCode.Trim().ToUpperInvariant(),
                    IsMultiCurrency = accountDto.IsMultiCurrency,
                    IsSegmented = true,
                    IsControlAccount = accountDto.IsControlAccount,
                    AllowDirectPosting = accountDto.IsPostingAllowed,
                    Status = AccountStatus.Active,
                    TenantId = tenantId,
                    CreatedAt = now,
                    CreatedBy = userName
                };

                // 4. Create Segment Values from DTO
                if (identity.Values.Count > 0)
                {
                    foreach (var segmentValue in identity.Values.OrderBy(s => s.SegmentPosition))
                    {
                        var segmentEntity = new AccountSegmentValue
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            AccountId = account.Id,
                            SegmentStructureId = segmentValue.SegmentStructureId,
                            SegmentValue = segmentValue.SegmentValue,
                            SegmentLookupValueId = segmentValue.SegmentLookupValueId,
                            SegmentPosition = segmentValue.SegmentPosition,
                            IsLocked = false,
                            EffectiveDate = now,
                            CreatedAt = now,
                            CreatedBy = userName
                        };
                        account.SegmentValues.Add(segmentEntity);
                    }

                    Console.WriteLine($"DEBUG: Created {identity.Values.Count} segment values for account {account.AccountNumber}");
                }
                else
                {
                    Console.WriteLine($"WARNING: No segment values provided for account {account.AccountNumber}");
                }

                // 5. Save to Database
                _context.Accounts.Add(account);
                await _accountingBookService.SyncAccountMappingsAsync(account, accountDto.AccountingBooks);

                return account;
            }
            catch
            {
                // The request-level exception middleware owns diagnostic logging and the
                // tenant administrator audit record. Do not duplicate it on the console.
                throw;
            }
        }

        public async Task<bool> ValidateAccountStructureAsync(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new ArgumentException("Account number cannot be empty.");

            var tenantId = TenantId;

            // 1. Fetch Active Segment Structure
            var segments = await _context.AccountSegmentStructures
                .Where(s => s.TenantId == tenantId && s.IsActive)
                .OrderBy(s => s.SegmentPosition)
                .ToListAsync();

            if (!segments.Any())
            {
                // If no structure defined, assume simple validation or throw error depending on policy.
                // For now, we'll allow it but log a warning if we had a logger.
                return true; 
            }

            // 2. Determine Separator (Assume consistent separator from first segment)
            var separator = segments.First().SeparatorCharacter ?? "-";
            
            // 3. Split Account Number
            // Handle "None" separator or empty string
            string[] parts;
            if (string.IsNullOrEmpty(separator))
            {
                // Fixed length parsing would be needed here, but for MVP assuming separator exists
                // If no separator, we treat the whole string as one part or need logic to slice by SegmentLength
                // For this implementation, we assume a separator is used if defined.
                parts = new[] { accountNumber }; 
            }
            else
            {
                parts = accountNumber.Split(new[] { separator }, StringSplitOptions.None);
            }

            // 4. Validate Segment Count
            if (parts.Length != segments.Count)
            {
                throw new ArgumentException($"Account number format invalid. Expected {segments.Count} segments, found {parts.Length}.");
            }

            // 5. Validate Each Segment
            for (int i = 0; i < segments.Count; i++)
            {
                var segmentDef = segments[i];
                var segmentValue = parts[i];

                // A. Length Check
                if (segmentValue.Length != segmentDef.SegmentLength)
                {
                    throw new ArgumentException($"Segment {i + 1} ({segmentDef.SegmentName}) length invalid. Expected {segmentDef.SegmentLength}, found {segmentValue.Length} ('{segmentValue}').");
                }

                // B. Data Type Check (Alphanumeric is standard, could add Numeric check)
                if (segmentDef.DataType == "Numeric" && !long.TryParse(segmentValue, out _))
                {
                     throw new ArgumentException($"Segment {i + 1} ({segmentDef.SegmentName}) must be numeric.");
                }

                // C. Lookup Validation
                if (segmentDef.LookupTableRequired)
                {
                    var exists = await _context.SegmentLookupValues
                        .AnyAsync(v => v.SegmentStructureId == segmentDef.Id 
                                    && v.SegmentValue == segmentValue 
                                    && v.IsActive 
                                    && v.TenantId == tenantId);
                    
                    if (!exists)
                    {
                        throw new ArgumentException($"Invalid value '{segmentValue}' for segment {segmentDef.SegmentName}. Value not found in lookup table.");
                    }
                }
            }

            return true;
        }

        private static bool IsAllZeros(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (var ch in value)
            {
                if (ch != '0')
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<Account?> GetAccountByIdAsync(Guid accountId)
        {
            var tenantId = TenantId;
            return await _context.Accounts
                .Include(a => a.SegmentValues)
                .Include(a => a.CurrencyLinks)
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId && !a.IsDeleted);
        }

        public async Task<Account?> GetAccountByCodeAsync(string accountCode)
        {
            var tenantId = TenantId;
            return await _context.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AccountCode == accountCode && !a.IsDeleted);
        }

        public async Task<IEnumerable<Account>> GetAllAccountsAsync()
        {
            var tenantId = TenantId;
            return await _context.Accounts
                .Where(a => a.TenantId == tenantId && !a.IsDeleted)
                .ToListAsync();
        }

        #endregion

        #region Transaction Processing

        [Obsolete("Legacy direct GL posting is disabled. Use IJournalEntryService for manual journals or IFinancePostingEngine through the owning Finance module.")]
        public Task<JournalEntry> PostJournalEntryAsync(CreateJournalEntryDto entryDto)
            => throw new InvalidOperationException(
                "Legacy direct GL posting is disabled. Use the journal entry lifecycle for manual journals or the owning Finance module service through IFinancePostingEngine.");

        [Obsolete("Use the exact-book /api/finance/book-balances inquiry. This compatibility API resolves only the single active default posting book.")]
        public async Task<decimal> GetAccountBalanceAsync(Guid accountId, string? currencyCode = null)
        {
            var tenantId = TenantId;
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId && !a.IsDeleted);
            if (account == null) throw new ArgumentException("Account not found");
            var primaryBooks = await _context.AccountingBooks.AsNoTracking().Where(item =>
                    item.TenantId == tenantId && item.IsDefault && item.IsActive && item.AllowsPosting && !item.IsDeleted)
                .Take(2).ToListAsync();
            if (primaryBooks.Count != 1)
                throw new InvalidOperationException("PRIMARY_BOOK_AUTHORITY_AMBIGUOUS: Legacy balance inquiry requires exactly one active default posting book.");
            var requestedCurrency = string.IsNullOrWhiteSpace(currencyCode)
                ? await _tenantSettings.GetBaseCurrencyAsync()
                : currencyCode.Trim().ToUpperInvariant();

            if (!string.Equals(requestedCurrency, await _tenantSettings.GetBaseCurrencyAsync(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Legacy balance inquiry supports only the functional currency. Use exact-book exposure inquiry for foreign currency.");
            return account.Balance;
        }

        public async Task<string> GenerateJournalEntryNumberAsync(CancellationToken cancellationToken = default)
        {
            return await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.JournalEntry,
                TenantId,
                DateTime.UtcNow,
                nameof(JournalEntry),
                cancellationToken: cancellationToken);
        }

        #endregion

        #region Currency Management

        public async Task<IEnumerable<ExchangeRate>> GetExchangeRatesAsync(DateTime date)
        {
            return await _context.ExchangeRates
                .Where(r => r.EffectiveDate.Date == date.Date)
                .ToListAsync();
        }

        public async Task<JournalEntry> RunCurrencyRevaluationAsync(RevaluationRequestDto request)
        {
            _ = request;
            await Task.CompletedTask;
            throw new InvalidOperationException(
                "Legacy GeneralLedgerService currency revaluation is disabled. Use ICurrencyRevaluationService/IFxAccountingService so FX journals post through IFinancePostingEngine.");
        }

        private async Task<JournalEntry> RunCurrencyRevaluationCoreAsync(RevaluationRequestDto request)
        {
            var tenantIdValue = TenantId;
            var revaluationDate = request.RevaluationDate.Date;

            // 1. Get Accounts to Revalue
            // Use ReportingDbContext to bypass global filters (TenantId, SoftDelete) which cause EF Core translation issues.
            // We manually apply the filters here.
            var allAccounts = await _reportingContext.Accounts
                .Where(a => a.TenantId == tenantIdValue 
                         && !a.IsDeleted 
                         && a.Status == ErpSystem.Core.Enums.AccountStatus.Active)
                .ToListAsync();

            // Load currency links separately for the accounts we need
            var accountIds = allAccounts.Select(a => a.Id).ToList();
            var currencyLinks = await _context.AccountCurrencyLinks
                .IgnoreQueryFilters()
                .Where(cl => cl.TenantId == tenantIdValue && accountIds.Contains(cl.AccountId) && cl.IsActive && !cl.IsDeleted)
                .ToListAsync();

            // Manually attach currency links to accounts (in-memory)
            foreach (var account in allAccounts)
            {
                account.CurrencyLinks = currencyLinks.Where(cl => cl.AccountId == account.Id).ToList();
            }

            var baseCurrencyCode = await _tenantSettings.GetBaseCurrencyAsync();
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
                JournalEntryNumber = await _documentNumberingService.GenerateAsync(
                    DocumentNumberingModules.Finance,
                    FinanceDocumentTypes.CurrencyRevaluation,
                    tenantIdValue,
                    revaluationDate,
                    nameof(JournalEntry)),
                EntryDate = revaluationDate,
                Description = $"Currency Revaluation - {request.RevaluationType} - {revaluationDate:d}",
                ReferenceNumber = request.RevaluationType,
                PostingStatus = request.PreviewOnly ? "Draft" : "Posted",
                PostingDate = DateTime.UtcNow,
                IsBalanced = true, // Will be balanced by Gain/Loss account
                BookClassification = "IFRS",
                TenantId = tenantIdValue,
                FiscalPeriodId = await GetOpenFiscalPeriodIdAsync(revaluationDate)
            };

            var transactions = new List<AccountTransaction>();
            decimal totalAdjustment = 0;
            int lineNum = 1;

            // 3. Process Each Account
            foreach (var account in accounts)
            {
                // Get foreign currency balance
                // For simplicity in this MVP, we sum all transactions. 
                // In production, this should be optimized (e.g., using pre-calculated balances).
                
                var accountTransactions = await _context.AccountTransactions
                    .Where(t => t.AccountId == account.Id && t.TransactionDate <= revaluationDate && !t.IsDeleted)
                    .ToListAsync();

                // Group by currency to handle multi-currency accounts correctly
                var currencyGroups = accountTransactions
                    .Where(t => t.TransactionCurrency != null
                        && !string.Equals(t.TransactionCurrency, baseCurrencyCode, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(t => t.TransactionCurrency);

                foreach (var group in currencyGroups)
                {
                    string currency = group.Key!;
                    if (!string.IsNullOrEmpty(request.CurrencyCode) && currency != request.CurrencyCode) continue;

                    // Try to get balance from AccountCurrencyLink (more efficient)
                    var currencyLink = account.CurrencyLinks
                        .FirstOrDefault(c => c.LinkedCurrencyCode == currency && c.IsActive);
                    
                    decimal foreignBalance = currencyLink?.ForeignCurrencyBalance 
                        ?? group.Sum(t => t.ForeignCurrencyAmount ?? 0); // Fallback to transaction sum
                    decimal currentBaseBalance = group.Sum(t => t.DebitAmount - t.CreditAmount); // Net Base Balance for this currency

                    if (foreignBalance == 0 && currentBaseBalance == 0) continue;

                    // Get Exchange Rate
                    var exchangeRateEntity = await _context.ExchangeRates
                        .Where(r => r.TargetCurrencyCode == currency
                            && r.BaseCurrencyCode == baseCurrencyCode
                            && r.EffectiveDate <= revaluationDate)
                        .OrderByDescending(r => r.EffectiveDate)
                        .FirstOrDefaultAsync();

                    if (exchangeRateEntity == null) 
                    {
                        // Log warning or skip? For now skip.
                        continue; 
                    }

                    decimal rate = exchangeRateEntity.Rate;
                    decimal newBaseBalance = foreignBalance * rate;
                    decimal adjustment = newBaseBalance - currentBaseBalance;

                    if (adjustment == 0) continue;

                    // Update AccountCurrencyLink metadata if it exists
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
                        ForeignCurrencyAmount = 0, // No change in foreign balance
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
                        // Gain for Asset (Debit increases), Loss for Liability (Credit increases)?
                        // Actually, if Base Balance needs to increase:
                        // Asset: Debit
                        // Liability: Credit
                        // We need to know Account Type to know if 'Balance' is Debit or Credit normal.
                        // But here we calculated Net Base Balance as (Debit - Credit).
                        // So if Adjustment is positive, we need to add to (Debit - Credit).
                        // i.e. Debit the account.
                        adjustmentTxn.DebitAmount = adjustment;
                        adjustmentTxn.CreditAmount = 0;
                    }
                    else
                    {
                        // Adjustment is negative, we need to reduce (Debit - Credit).
                        // i.e. Credit the account.
                        adjustmentTxn.DebitAmount = 0;
                        adjustmentTxn.CreditAmount = Math.Abs(adjustment);
                    }

                    transactions.Add(adjustmentTxn);
                    totalAdjustment += adjustment;
                }
            }

            if (transactions.Count == 0) return journalEntry; // Nothing to revalue

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

            // TotalAdjustment is Net Debit change.
            // If Positive (Net Debit), we need a Credit to balance.
            // If Negative (Net Credit), we need a Debit to balance.
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
                await _context.SaveChangesAsync();
            }
            else
            {
                // Attach transactions for preview return
                journalEntry.Transactions = transactions;
            }

            return journalEntry;
        }

        private async Task<Guid> GetOpenFiscalPeriodIdAsync(DateTime date)
        {
            var tenantId = TenantId;
            
            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date && p.TenantId == tenantId);
            
            if (period == null)
            {
                throw new InvalidOperationException(
                    $"No fiscal period is configured for date {date:yyyy-MM-dd}. " +
                    $"Please create the appropriate fiscal year and period in Finance > Setup > Fiscal Periods.");
            }

            return period.Id;
        }

        #endregion

        #region Financial Statements

        public async Task<BalanceSheetDto> GenerateBalanceSheetAsync(BalanceSheetRequestDto request)
        {
            var tenantId = TenantId;
            var bookClassification = NormalizeBookClassification(request.BookClassification);
            var dimensionFilters = await ResolveDimensionFiltersAsync(request.DimensionFilters);

            var accounts = await GetReportingAccountsAsync(
                bookClassification,
                request.AccountIds,
                request.SegmentFilters,
                new[] { AccountType.Asset, AccountType.Liability, AccountType.Equity });
            var classificationPresentation = await GetClassificationPresentationAsync(
                bookClassification, accounts.Select(account => account.Id));

            var rawBalances = await CalculatePostedRawBalancesAsOfAsync(
                tenantId,
                accounts.Select(a => a.Id).ToArray(),
                request.AsAtDate,
                bookClassification,
                dimensionFilters);
            var accountBalances = new Dictionary<Guid, decimal>();
            foreach (var account in accounts)
            {
                var rawBalance = rawBalances.GetValueOrDefault(account.Id);
                var balance = ToStatementNormalBalance(account.AccountType, rawBalance);
                if (balance != 0)
                {
                    accountBalances[account.Id] = balance;
                }
            }

            // 3. Build Balance Sheet structure
            var balanceSheet = new BalanceSheetDto
            {
                CompanyName = await _tenantSettings.GetCompanyNameAsync(),
                AsAtDate = request.AsAtDate,
                BookClassification = bookClassification,
                CurrencyCode = await _tenantSettings.GetBaseCurrencyAsync()
            };

            // 4. Group accounts by section and line item
            var assetAccounts = accounts.Where(a => a.AccountType == AccountType.Asset && accountBalances.ContainsKey(a.Id)).ToList();
            var liabilityAccounts = accounts.Where(a => a.AccountType == AccountType.Liability && accountBalances.ContainsKey(a.Id)).ToList();
            var equityAccounts = accounts.Where(a => a.AccountType == AccountType.Equity && accountBalances.ContainsKey(a.Id)).ToList();

            // 5. Build Assets section
            var assetsSection = BuildBalanceSheetSection(
                "Assets", 
                1, 
                assetAccounts, 
                accountBalances, 
                classificationPresentation,
                request.IncludeAccountDetails);

            // 6. Build Liabilities section
            var liabilitiesSection = BuildBalanceSheetSection(
                "Liabilities", 
                2, 
                liabilityAccounts, 
                accountBalances, 
                classificationPresentation,
                request.IncludeAccountDetails);

            // 7. Build Equity section
            var equitySection = BuildBalanceSheetSection(
                "Equity", 
                3, 
                equityAccounts, 
                accountBalances, 
                classificationPresentation,
                request.IncludeAccountDetails);

            balanceSheet.Sections = new List<BalanceSheetSectionDto> 
            { 
                assetsSection, 
                liabilitiesSection, 
                equitySection 
            };

            // 8. Calculate totals
            balanceSheet.TotalAssets = assetsSection.SectionTotal;
            balanceSheet.TotalLiabilities = liabilitiesSection.SectionTotal;
            balanceSheet.TotalEquity = equitySection.SectionTotal;
            balanceSheet.LayoutExecution = await ExecuteStatementLayoutAsync(
                FinancialStatementType.BalanceSheet,
                bookClassification,
                null,
                request.AsAtDate,
                request.LayoutId,
                request.UseDefaultLayout,
                request.IncludeAccountDetails,
                request.AccountIds,
                request.SegmentFilters,
                request.DimensionFilters);

            return balanceSheet;
        }

        private BalanceSheetSectionDto BuildBalanceSheetSection(
            string sectionName,
            int sectionOrder,
            List<Account> accounts,
            Dictionary<Guid, decimal> accountBalances,
            IReadOnlyDictionary<Guid, ClassificationPresentation> classificationPresentation,
            bool includeAccountDetails)
        {
            var section = new BalanceSheetSectionDto
            {
                SectionName = sectionName,
                SectionOrder = sectionOrder
            };

            var categories = accounts
                .GroupBy(a => classificationPresentation.TryGetValue(a.Id, out var presentation)
                    ? presentation.ParentName ?? presentation.Name
                    : "Unclassified")
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var categoryGroup in categories)
            {
                var category = new BalanceSheetCategoryDto
                {
                    CategoryName = categoryGroup.Key,
                    CategoryOrder = categories.IndexOf(categoryGroup) + 1
                };

                // Group by line item
                var lineItemGroups = categoryGroup
                    .GroupBy(a => classificationPresentation.TryGetValue(a.Id, out var presentation)
                        ? presentation.Name
                        : "Unclassified")
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var lineItemGroup in lineItemGroups)
                {
                    var lineItem = new BalanceSheetLineItemDto
                    {
                        LineItemName = lineItemGroup.Key,
                        Amount = lineItemGroup.Sum(a => accountBalances[a.Id]),
                        LineOrder = lineItemGroups.IndexOf(lineItemGroup) + 1
                    };

                    if (includeAccountDetails)
                    {
                        lineItem.AccountNumbers = lineItemGroup.Select(a => a.AccountNumber).ToList();
                    }

                    category.LineItems.Add(lineItem);
                }

                category.CategoryTotal = category.LineItems.Sum(li => li.Amount);
                section.Categories.Add(category);
            }

            section.SectionTotal = section.Categories.Sum(c => c.CategoryTotal);
            return section;
        }

        private string? GetLineItem(Account account, string bookClassification)
        {
            return NormalizeBookClassification(bookClassification) switch
            {
                "IFRS" => account.IFRSLineItem,
                "LOCAL_STATUTORY" => account.BaseLineItem,
                "MANAGEMENT" => account.LocalLineItem,
                _ => account.IFRSLineItem
            };
        }

        private sealed record NormalizedSegmentFilter(Guid SegmentStructureId, string SegmentValue);

        private static string NormalizeBookClassification(string? bookClassification)
        {
            var normalized = (bookClassification ?? "IFRS").Trim().ToUpperInvariant();
            return normalized switch
            {
                "BASE" or "LOCAL" => "LOCAL_STATUTORY",
                "MANAGEMENT" => "MANAGEMENT",
                _ => normalized
            };
        }

        private sealed record ClassificationPresentation(string Code, string Name, string? ParentName);

        private async Task<IReadOnlyDictionary<Guid, ClassificationPresentation>> GetClassificationPresentationAsync(
            string accountingBookCode,
            IEnumerable<Guid> accountIds)
        {
            var tenantId = _currentUserService.GetRequiredFinanceTenantId();
            var ids = accountIds.Distinct().ToArray();
            return await _context.AccountAccountingBooks
                .AsNoTracking()
                .Where(mapping => mapping.TenantId == tenantId
                    && !mapping.IsDeleted
                    && mapping.IsEnabled
                    && ids.Contains(mapping.AccountId)
                    && mapping.AccountingBook.Code == accountingBookCode
                    && mapping.AccountClassification != null)
                .Select(mapping => new
                {
                    mapping.AccountId,
                    mapping.AccountClassification!.Code,
                    mapping.AccountClassification.Name,
                    ParentName = mapping.AccountClassification.ParentClassification != null
                        ? mapping.AccountClassification.ParentClassification.Name
                        : null
                })
                .ToDictionaryAsync(
                    item => item.AccountId,
                    item => new ClassificationPresentation(item.Code, item.Name, item.ParentName));
        }

        private async Task<List<Account>> GetReportingAccountsAsync(
            string? bookClassification,
            IEnumerable<Guid>? accountIds,
            IEnumerable<FinanceSegmentFilterDto>? segmentFilters,
            IEnumerable<AccountType>? accountTypes = null)
        {
            var tenantId = TenantId;
            var selectedAccountIds = accountIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray() ?? Array.Empty<Guid>();
            var resolvedSegmentFilters = await ResolveSegmentFiltersAsync(tenantId, segmentFilters);

            IQueryable<Account> accountQuery = _context.Accounts
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted);

            if (accountTypes != null)
            {
                var typeArray = accountTypes.Distinct().ToArray();
                if (typeArray.Length > 0)
                {
                    accountQuery = accountQuery.Where(a => typeArray.Contains(a.AccountType));
                }
            }

            if (selectedAccountIds.Length > 0)
            {
                var ownedCount = await accountQuery.CountAsync(a => selectedAccountIds.Contains(a.Id));
                if (ownedCount != selectedAccountIds.Length)
                {
                    throw new InvalidOperationException("One or more report account filters do not belong to the current tenant.");
                }

                accountQuery = accountQuery.Where(a => selectedAccountIds.Contains(a.Id));
            }

            foreach (var filter in resolvedSegmentFilters)
            {
                accountQuery = accountQuery.Where(a => a.SegmentValues.Any(sv =>
                    sv.TenantId == tenantId &&
                    sv.SegmentStructureId == filter.SegmentStructureId &&
                    sv.SegmentValue == filter.SegmentValue));
            }

            return await accountQuery
                .OrderBy(a => a.AccountNumber)
                .ToListAsync();
        }

        private async Task<List<NormalizedSegmentFilter>> ResolveSegmentFiltersAsync(
            Guid tenantId,
            IEnumerable<FinanceSegmentFilterDto>? segmentFilters)
        {
            var requestedFilters = segmentFilters?
                .Where(filter => filter != null && !string.IsNullOrWhiteSpace(filter.SegmentValue))
                .ToList() ?? new List<FinanceSegmentFilterDto>();

            var resolved = new List<NormalizedSegmentFilter>();
            foreach (var filter in requestedFilters)
            {
                var value = filter.SegmentValue.Trim();
                IQueryable<AccountSegmentStructure> structureQuery = _context.AccountSegmentStructures
                    .AsNoTracking()
                    .Where(s => s.TenantId == tenantId && s.IsActive && s.IsReportingDimension);

                if (filter.SegmentStructureId.HasValue && filter.SegmentStructureId.Value != Guid.Empty)
                {
                    var segmentId = filter.SegmentStructureId.Value;
                    structureQuery = structureQuery.Where(s => s.Id == segmentId);
                }
                else if (!string.IsNullOrWhiteSpace(filter.SegmentCode))
                {
                    var segmentCode = filter.SegmentCode.Trim();
                    structureQuery = structureQuery.Where(s => s.SegmentCode == segmentCode);
                }
                else if (filter.SegmentPosition.HasValue)
                {
                    var position = filter.SegmentPosition.Value;
                    structureQuery = structureQuery.Where(s => s.SegmentPosition == position);
                }
                else
                {
                    throw new InvalidOperationException("Segment report filters require a tenant-owned segment id, code, or position.");
                }

                var structure = await structureQuery.SingleOrDefaultAsync();
                if (structure == null)
                {
                    throw new InvalidOperationException("One or more report segment filters do not belong to the current tenant or are not reporting dimensions.");
                }

                if (structure.LookupTableRequired)
                {
                    var valueExists = await _context.SegmentLookupValues
                        .AsNoTracking()
                        .AnyAsync(v =>
                            v.TenantId == tenantId &&
                            v.SegmentStructureId == structure.Id &&
                            v.SegmentValue == value &&
                            v.IsActive);

                    if (!valueExists)
                    {
                        throw new InvalidOperationException($"Segment value '{value}' is not valid for reporting dimension '{structure.SegmentName}'.");
                    }
                }
                else
                {
                    if (value.Length != structure.SegmentLength)
                    {
                        throw new InvalidOperationException(
                            $"Segment value '{value}' must be exactly {structure.SegmentLength} characters for reporting dimension '{structure.SegmentName}'.");
                    }

                    if (structure.DataType.Equals("Numeric", StringComparison.OrdinalIgnoreCase) &&
                        value.Any(character => !char.IsDigit(character)))
                    {
                        throw new InvalidOperationException(
                            $"Segment value '{value}' must contain only numbers for reporting dimension '{structure.SegmentName}'.");
                    }

                    if (structure.DataType.Equals("Alphanumeric", StringComparison.OrdinalIgnoreCase) &&
                        value.Any(character => !char.IsLetterOrDigit(character)))
                    {
                        throw new InvalidOperationException(
                            $"Segment value '{value}' must contain only letters and numbers for reporting dimension '{structure.SegmentName}'.");
                    }

                    var now = DateTime.UtcNow;
                    var valueExistsOnActiveAccount = await _context.AccountSegmentValues
                        .AsNoTracking()
                        .AnyAsync(segmentValue =>
                            segmentValue.TenantId == tenantId &&
                            segmentValue.SegmentStructureId == structure.Id &&
                            segmentValue.SegmentValue == value &&
                            !segmentValue.IsDeleted &&
                            segmentValue.EffectiveDate <= now &&
                            (segmentValue.EndDate == null || segmentValue.EndDate > now) &&
                            segmentValue.Account.TenantId == tenantId &&
                            !segmentValue.Account.IsDeleted &&
                            segmentValue.Account.Status == AccountStatus.Active &&
                            (segmentValue.Account.EffectiveDate == null || segmentValue.Account.EffectiveDate <= now) &&
                            (segmentValue.Account.ExpirationDate == null || segmentValue.Account.ExpirationDate > now));

                    if (!valueExistsOnActiveAccount)
                    {
                        throw new InvalidOperationException(
                            $"Segment value '{value}' is not used by an active GL account for reporting dimension '{structure.SegmentName}'.");
                    }
                }

                resolved.Add(new NormalizedSegmentFilter(structure.Id, value));
            }

            return resolved;
        }

        private IQueryable<AccountTransaction> BuildPostedLedgerQuery(
            Guid tenantId,
            string? bookClassification,
            bool includeReversed = true)
        {
            var normalizedBook = NormalizeBookClassification(bookClassification);
            var statuses = includeReversed
                ? new[] { "Posted", "Reversed" }
                : new[] { "Posted" };

            return _context.AccountTransactions
                .AsNoTracking()
                .Include(t => t.JournalEntry)
                .Include(t => t.FinanceDimensionSet)
                .ThenInclude(set => set!.Items)
                .Include(t => t.FinanceDimensionSnapshot)
                .ThenInclude(snapshot => snapshot!.Items)
                .Where(t =>
                    t.TenantId == tenantId &&
                    !t.IsDeleted &&
                    t.BookClassification == normalizedBook &&
                    t.JournalEntry.TenantId == tenantId &&
                    !t.JournalEntry.IsDeleted &&
                    statuses.Contains(t.PostingStatus) &&
                    statuses.Contains(t.JournalEntry.PostingStatus));
        }

        private async Task<IReadOnlyCollection<ResolvedFinanceDimensionFilter>> ResolveDimensionFiltersAsync(
            IEnumerable<FinanceDimensionFilterDto>? filters,
            CancellationToken cancellationToken = default)
        {
            var requested = filters?.ToList() ?? new List<FinanceDimensionFilterDto>();
            if (requested.Count == 0)
            {
                return Array.Empty<ResolvedFinanceDimensionFilter>();
            }

            if (_dimensionReportingFilters == null)
            {
                throw new InvalidOperationException(
                    "Transaction-dimension filtering is not available for Finance reports.");
            }

            return await _dimensionReportingFilters.ResolveAsync(requested, cancellationToken);
        }

        private IQueryable<AccountTransaction> ApplyDimensionFilters(
            IQueryable<AccountTransaction> query,
            IReadOnlyCollection<ResolvedFinanceDimensionFilter>? filters)
            => filters == null || filters.Count == 0
                ? query
                : _dimensionReportingFilters!.Apply(query, filters);

        private async Task<Dictionary<Guid, decimal>> CalculatePostedRawBalancesAsOfAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> accountIds,
            DateTime asAtDate,
            string? bookClassification,
            IReadOnlyCollection<ResolvedFinanceDimensionFilter>? dimensionFilters = null)
        {
            if (accountIds.Count == 0)
            {
                return new Dictionary<Guid, decimal>();
            }

            var endExclusive = asAtDate.Date.AddDays(1);
            return await ApplyDimensionFilters(
                    BuildPostedLedgerQuery(tenantId, bookClassification),
                    dimensionFilters)
                .Where(t => accountIds.Contains(t.AccountId) && t.TransactionDate < endExclusive)
                .GroupBy(t => t.AccountId)
                .Select(g => new { AccountId = g.Key, Balance = g.Sum(t => t.DebitAmount - t.CreditAmount) })
                .ToDictionaryAsync(x => x.AccountId, x => x.Balance);
        }

        private async Task<Dictionary<Guid, AccountPeriodMovement>> CalculatePostedPeriodMovementAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> accountIds,
            DateTime startDate,
            DateTime endDate,
            string? bookClassification,
            IReadOnlyCollection<ResolvedFinanceDimensionFilter>? dimensionFilters = null)
        {
            if (accountIds.Count == 0)
            {
                return new Dictionary<Guid, AccountPeriodMovement>();
            }

            var start = startDate.Date;
            var endExclusive = endDate.Date.AddDays(1);
            return await ApplyDimensionFilters(
                    BuildPostedLedgerQuery(tenantId, bookClassification),
                    dimensionFilters)
                .Where(t =>
                    accountIds.Contains(t.AccountId) &&
                    t.TransactionDate >= start &&
                    t.TransactionDate < endExclusive)
                .GroupBy(t => t.AccountId)
                .Select(g => new AccountPeriodMovement
                {
                    AccountId = g.Key,
                    Debits = g.Sum(t => t.DebitAmount),
                    Credits = g.Sum(t => t.CreditAmount)
                })
                .ToDictionaryAsync(x => x.AccountId, x => x);
        }

        private sealed class AccountPeriodMovement
        {
            public Guid AccountId { get; set; }
            public decimal Debits { get; set; }
            public decimal Credits { get; set; }
            public decimal RawMovement => Debits - Credits;
        }

        private static decimal ToStatementNormalBalance(AccountType accountType, decimal rawDebitMinusCredit)
        {
            return accountType == AccountType.Asset || accountType == AccountType.Expense
                ? rawDebitMinusCredit
                : -rawDebitMinusCredit;
        }

        private static void ApplyRawBalanceToTrialBalanceLine(TrialBalanceLineDto line, decimal rawDebitMinusCredit)
        {
            if (rawDebitMinusCredit > 0)
            {
                line.DebitBalance = rawDebitMinusCredit;
                line.CreditBalance = 0;
            }
            else if (rawDebitMinusCredit < 0)
            {
                line.DebitBalance = 0;
                line.CreditBalance = Math.Abs(rawDebitMinusCredit);
            }
            else
            {
                line.DebitBalance = 0;
                line.CreditBalance = 0;
            }
        }

        private static void ApplyRawOpeningBalanceToTrialBalanceLine(TrialBalanceLineDto line, decimal rawDebitMinusCredit)
        {
            if (rawDebitMinusCredit > 0)
            {
                line.OpeningDebitBalance = rawDebitMinusCredit;
                line.OpeningCreditBalance = 0;
            }
            else if (rawDebitMinusCredit < 0)
            {
                line.OpeningDebitBalance = 0;
                line.OpeningCreditBalance = Math.Abs(rawDebitMinusCredit);
            }
            else
            {
                line.OpeningDebitBalance = 0;
                line.OpeningCreditBalance = 0;
            }
        }

        private async Task<decimal> CalculateAccountBalanceAsOf(
            Guid tenantId,
            Guid accountId,
            DateTime asAtDate,
            string bookClassification)
        {
            var balances = await CalculatePostedRawBalancesAsOfAsync(
                tenantId,
                new[] { accountId },
                asAtDate,
                bookClassification);

            return balances.GetValueOrDefault(accountId);
        }

        private async Task<decimal> CalculatePostedAccountNetBalanceAsOf(Guid tenantId, Guid accountId, DateTime asAtDate, string bookClassification)
        {
            var balances = await CalculatePostedRawBalancesAsOfAsync(
                tenantId,
                new[] { accountId },
                asAtDate,
                bookClassification);

            return balances.GetValueOrDefault(accountId);
        }

        public async Task<IncomeStatementDto> GenerateIncomeStatementAsync(IncomeStatementRequestDto request)
        {
            var tenantId = TenantId;
            if (request.PeriodEnd.Date < request.PeriodStart.Date)
            {
                throw new ArgumentException("Period end must be on or after period start.");
            }

            var bookClassification = NormalizeBookClassification(request.BookClassification);
            var dimensionFilters = await ResolveDimensionFiltersAsync(request.DimensionFilters);

            var accounts = await GetReportingAccountsAsync(
                bookClassification,
                request.AccountIds,
                request.SegmentFilters,
                new[] { AccountType.Revenue, AccountType.Expense });
            var classificationPresentation = await GetClassificationPresentationAsync(
                bookClassification, accounts.Select(account => account.Id));

            var rawMovements = await CalculatePostedPeriodMovementAsync(
                tenantId,
                accounts.Select(a => a.Id).ToArray(),
                request.PeriodStart,
                request.PeriodEnd,
                bookClassification,
                dimensionFilters);
            var disposalGainAccountIds = await GetTenantDisposalGainAccountIdsAsync(tenantId);
            var accountActivity = new Dictionary<Guid, decimal>();
            foreach (var account in accounts)
            {
                var movement = rawMovements.GetValueOrDefault(account.Id);
                var activity = movement == null
                    ? 0
                    : ToStatementNormalBalance(account.AccountType, movement.RawMovement);
                if (disposalGainAccountIds.Contains(account.Id) && account.AccountType == AccountType.Expense && activity < 0)
                {
                    activity = Math.Abs(activity);
                }

                if (activity != 0)
                {
                    accountActivity[account.Id] = activity;
                }
            }

            // 3. Build Income Statement structure
            var incomeStatement = new IncomeStatementDto
            {
                CompanyName = await _tenantSettings.GetCompanyNameAsync(),
                PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd,
                BookClassification = bookClassification,
                CurrencyCode = await _tenantSettings.GetBaseCurrencyAsync()
            };

            // 4. Separate Revenue and Expense accounts
            var revenueAccounts = accounts.Where(a => a.AccountType == AccountType.Revenue && accountActivity.ContainsKey(a.Id)).ToList();
            var expenseAccounts = accounts.Where(a => a.AccountType == AccountType.Expense && accountActivity.ContainsKey(a.Id)).ToList();
            var disposalGainPresentationAccounts = accounts
                .Where(a => disposalGainAccountIds.Contains(a.Id) && accountActivity.ContainsKey(a.Id))
                .ToList();

            var otherIncomeAccounts = revenueAccounts
                .Where(a => classificationPresentation.GetValueOrDefault(a.Id)?.Code == "OTHER_INCOME")
                .ToList();
            otherIncomeAccounts = otherIncomeAccounts
                .Concat(disposalGainPresentationAccounts)
                .DistinctBy(a => a.Id)
                .ToList();
            revenueAccounts = revenueAccounts.Except(otherIncomeAccounts).ToList();

            // 5. Build Revenue section
            var revenueSection = BuildIncomeStatementSection(
                "Revenue",
                1,
                revenueAccounts,
                accountActivity,
                bookClassification,
                request.IncludeAccountDetails);

            // 6. Build Expense sections (categorized)
            var costOfSalesAccounts = expenseAccounts.Where(a => classificationPresentation.GetValueOrDefault(a.Id)?.Code == "COST_OF_SALES").ToList();
            var disposalGainExpenseAccounts = disposalGainPresentationAccounts
                .Where(a => a.AccountType == AccountType.Expense)
                .ToList();
            expenseAccounts = expenseAccounts.Except(disposalGainExpenseAccounts).ToList();
            var otherExpenseAccounts = expenseAccounts
                .Where(a => classificationPresentation.GetValueOrDefault(a.Id)?.Code == "OTHER_EXPENSE")
                .ToList();
            var operatingExpenseAccounts = expenseAccounts.Where(a => classificationPresentation.GetValueOrDefault(a.Id)?.Code != "COST_OF_SALES"
                && classificationPresentation.GetValueOrDefault(a.Id)?.Code != "TAX_EXPENSE"
                && !otherExpenseAccounts.Contains(a)).ToList();
            var taxExpenseAccounts = expenseAccounts.Where(a => classificationPresentation.GetValueOrDefault(a.Id)?.Code == "TAX_EXPENSE").ToList();

            var costOfSalesSection = BuildIncomeStatementSection(
                "Cost of Sales",
                2,
                costOfSalesAccounts,
                accountActivity,
                bookClassification,
                request.IncludeAccountDetails);

            var operatingExpensesSection = BuildIncomeStatementSection(
                "Operating Expenses",
                3,
                operatingExpenseAccounts,
                accountActivity,
                bookClassification,
                request.IncludeAccountDetails);

            var otherIncomeSection = BuildIncomeStatementSection(
                "Other Income",
                4,
                otherIncomeAccounts,
                accountActivity,
                bookClassification,
                request.IncludeAccountDetails);

            var otherExpensesSection = BuildIncomeStatementSection(
                "Other Expenses",
                5,
                otherExpenseAccounts,
                accountActivity,
                bookClassification,
                request.IncludeAccountDetails);

            var taxExpenseSection = BuildIncomeStatementSection(
                "Tax Expense",
                6,
                taxExpenseAccounts,
                accountActivity,
                bookClassification,
                request.IncludeAccountDetails);

            incomeStatement.Sections = new List<IncomeStatementSectionDto>
            {
                revenueSection,
                costOfSalesSection,
                operatingExpensesSection,
                otherIncomeSection,
                otherExpensesSection,
                taxExpenseSection
            };

            // 7. Calculate totals and profit metrics
            incomeStatement.TotalRevenue = revenueSection.SectionTotal;
            incomeStatement.TotalCostOfSales = costOfSalesSection.SectionTotal;
            incomeStatement.GrossProfit = incomeStatement.TotalRevenue - incomeStatement.TotalCostOfSales;
            incomeStatement.TotalOperatingExpenses = operatingExpensesSection.SectionTotal;
            incomeStatement.OperatingProfit = incomeStatement.GrossProfit - incomeStatement.TotalOperatingExpenses;
            incomeStatement.TotalOtherIncome = otherIncomeSection.SectionTotal;
            incomeStatement.TotalOtherExpenses = otherExpensesSection.SectionTotal;
            incomeStatement.TaxExpense = taxExpenseSection.SectionTotal;
            incomeStatement.ProfitBeforeTax = incomeStatement.OperatingProfit
                + incomeStatement.TotalOtherIncome
                - incomeStatement.TotalOtherExpenses;
            incomeStatement.NetProfit = incomeStatement.ProfitBeforeTax - incomeStatement.TaxExpense;
            incomeStatement.PresentationWarnings = BuildDisposalGainPresentationWarnings(disposalGainPresentationAccounts);
            incomeStatement.LayoutExecution = await ExecuteStatementLayoutAsync(
                FinancialStatementType.IncomeStatement,
                bookClassification,
                request.PeriodStart,
                request.PeriodEnd,
                request.LayoutId,
                request.UseDefaultLayout,
                request.IncludeAccountDetails,
                request.AccountIds,
                request.SegmentFilters,
                request.DimensionFilters);

            return incomeStatement;
        }

        private async Task<FinancialStatementLayoutExecutionDto?> ExecuteStatementLayoutAsync(
            FinancialStatementType statementType,
            string bookClassification,
            DateTime? periodStart,
            DateTime periodEnd,
            Guid? layoutId,
            bool useDefaultLayout,
            bool includeAccountDetails,
            IEnumerable<Guid>? accountIds,
            IEnumerable<FinanceSegmentFilterDto>? segmentFilters,
            IEnumerable<FinanceDimensionFilterDto>? dimensionFilters)
        {
            var requestedLayoutId = layoutId.HasValue && layoutId.Value != Guid.Empty
                ? layoutId
                : null;
            if (!requestedLayoutId.HasValue && !useDefaultLayout)
            {
                return null;
            }

            if (_statementLayoutExecutionService == null)
            {
                if (requestedLayoutId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Financial statement layout execution is not available.");
                }

                return null;
            }

            var books = await _accountingBookService.GetBooksAsync();
            var book = books?.FirstOrDefault(candidate =>
                NormalizeBookClassification(candidate.Code).Equals(
                    bookClassification,
                    StringComparison.OrdinalIgnoreCase));
            if (book == null)
            {
                if (requestedLayoutId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Accounting book '{bookClassification}' is not available for the selected layout.");
                }

                return null;
            }

            try
            {
                return await _statementLayoutExecutionService.ExecutePublishedAsync(
                    new FinancialStatementLayoutExecutionRequestDto
                    {
                        LayoutId = requestedLayoutId,
                        StatementType = statementType,
                        AccountingBookId = book.Id,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd,
                        IncludeAccountDetails = includeAccountDetails,
                        IncludeHiddenRows = false,
                        AccountIds = accountIds?
                            .Where(accountId => accountId != Guid.Empty)
                            .Distinct()
                            .ToList() ?? new List<Guid>(),
                        SegmentFilters = segmentFilters?.ToList()
                            ?? new List<FinanceSegmentFilterDto>(),
                        DimensionFilters = dimensionFilters?.ToList()
                            ?? new List<FinanceDimensionFilterDto>()
                    });
            }
            catch (KeyNotFoundException) when (!requestedLayoutId.HasValue)
            {
                // A default layout is optional during rollout. The established
                // account-classification report remains the safe fallback.
                return null;
            }
        }

        private async Task<HashSet<Guid>> GetTenantDisposalGainAccountIdsAsync(Guid tenantId)
        {
            var accountIds = await _context.FixedAssetCategories
                .AsNoTracking()
                .Where(category =>
                    category.TenantId == tenantId &&
                    !category.IsDeleted &&
                    category.GainOnDisposalAccountId.HasValue)
                .Select(category => category.GainOnDisposalAccountId!.Value)
                .Distinct()
                .ToListAsync();

            return accountIds.ToHashSet();
        }

        private static List<string> BuildDisposalGainPresentationWarnings(IEnumerable<Account> disposalGainAccounts)
        {
            return disposalGainAccounts
                .Where(account => account.AccountType is AccountType.Expense or AccountType.Revenue)
                .Select(account =>
                {
                    var accountLabel = string.IsNullOrWhiteSpace(account.AccountNumber)
                        ? account.AccountName
                        : $"{account.AccountNumber} {account.AccountName}";

                    return account.AccountType == AccountType.Expense
                        ? $"Fixed asset disposal gain account '{accountLabel}' is expense-class; it is presented as non-operating disposal gain, not ordinary operating expense."
                        : $"Fixed asset disposal gain account '{accountLabel}' is revenue-class; it is presented as non-operating disposal gain unless statutory mapping intentionally presents it as revenue.";
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<MultiCurrencyDetailReportDto> GenerateMultiCurrencyDetailReportAsync(MultiCurrencyDetailRequestDto request)
        {
            var tenantId = TenantId;

            var baseCurrency = await _tenantSettings.GetBaseCurrencyAsync();
            var companyName = await _tenantSettings.GetCompanyNameAsync();

            // 1. Identify Accounts to Include
            var query = _context.Accounts
                .Include(a => a.CurrencyLinks)
                .Where(a => a.TenantId == tenantId && !a.IsDeleted);

            if (request.AccountId.HasValue)
            {
                query = query.Where(a => a.Id == request.AccountId.Value);
            }

            if (!string.IsNullOrEmpty(request.CurrencyCode))
            {
                // Include accounts that are defined in this currency OR have a link to this currency
                query = query.Where(a => a.CurrencyCode == request.CurrencyCode 
                                      || a.CurrencyLinks.Any(cl => cl.LinkedCurrencyCode == request.CurrencyCode && cl.IsActive));
            }
            else
            {
                // If no specific currency requested, include all multi-currency accounts or accounts with links
                query = query.Where(a => a.IsMultiCurrency || a.CurrencyLinks.Any());
            }

            var accounts = await query.ToListAsync();
            var report = new MultiCurrencyDetailReportDto
            {
                CompanyName = companyName,
                ReportDate = DateTime.UtcNow,
                PeriodStart = request.StartDate,
                PeriodEnd = request.EndDate
            };

            // 2. Process Each Account
            foreach (var account in accounts)
            {
                // Determine which currency we are reporting on for this account
                // If request has currency, use it.
                // If not, and account is multi-currency, we might need to report on all its currencies?
                // For this implementation, if no currency specified, we iterate through all foreign currencies linked or native.
                
                var currenciesToReport = new List<string>();
                if (!string.IsNullOrEmpty(request.CurrencyCode))
                {
                    currenciesToReport.Add(request.CurrencyCode);
                }
                else
                {
                    if (account.CurrencyCode != baseCurrency) currenciesToReport.Add(account.CurrencyCode);
                    currenciesToReport.AddRange(account.CurrencyLinks.Select(cl => cl.LinkedCurrencyCode));
                }
                currenciesToReport = currenciesToReport.Distinct().ToList();

                foreach (var currency in currenciesToReport)
                {
                    // Skip if account doesn't actually support this currency (unless it's the native one)
                    if (account.CurrencyCode != currency && !account.CurrencyLinks.Any(cl => cl.LinkedCurrencyCode == currency)) continue;

                    var accountDetail = new MultiCurrencyAccountDetailDto
                    {
                        AccountId = account.Id,
                        AccountNumber = account.AccountNumber,
                        AccountName = account.AccountName,
                        CurrencyCode = currency
                    };

                    // A. Calculate Opening Balances
                    // Get all transactions before StartDate for this currency
                    var openingTransactions = await _context.AccountTransactions
                        .Where(t => t.AccountId == account.Id 
                                 && t.TransactionDate < request.StartDate 
                                 && t.TransactionCurrency == currency
                                 && !t.IsDeleted)
                        .ToListAsync();

                    if (!request.IncludeRevaluation)
                    {
                        openingTransactions = openingTransactions.Where(t => !t.IsRevaluationEntry).ToList();
                    }

                    accountDetail.OpeningBalanceForeign = openingTransactions.Sum(t => t.ForeignCurrencyAmount ?? 0);
                    
                    // For Base Opening Balance, we sum the Base equivalents (Debit - Credit)
                    // Note: This logic assumes Asset/Expense are Debit normal, others Credit normal?
                    // Or we just report Net Debit? Let's report Net Debit (Debit - Credit) for simplicity in generic report.
                    // Or better, stick to the Account Type normalization used in PostJournalEntry.
                    // But AccountTransaction stores Debit/Credit absolute values.
                    // Let's use (Debit - Credit) as the "Net Movement"
                    
                    accountDetail.OpeningBalanceBase = openingTransactions.Sum(t => t.DebitAmount - t.CreditAmount);

                    // B. Fetch Period Transactions
                    var periodTransactions = await _context.AccountTransactions
                        .Where(t => t.AccountId == account.Id 
                                 && t.TransactionDate >= request.StartDate 
                                 && t.TransactionDate <= request.EndDate
                                 && t.TransactionCurrency == currency
                                 && !t.IsDeleted)
                        .OrderBy(t => t.TransactionDate)
                        .ThenBy(t => t.CreatedAt)
                        .ToListAsync();

                    if (!request.IncludeRevaluation)
                    {
                        periodTransactions = periodTransactions.Where(t => !t.IsRevaluationEntry).ToList();
                    }

                    // C. Process Transactions
                    decimal runningForeign = accountDetail.OpeningBalanceForeign;
                    decimal runningBase = accountDetail.OpeningBalanceBase;

                    foreach (var txn in periodTransactions)
                    {
                        var txnDetail = new MultiCurrencyTransactionDetailDto
                        {
                            TransactionDate = txn.TransactionDate,
                            Description = txn.Description ?? string.Empty,
                            Reference = txn.SourceReferenceNumber ?? string.Empty,
                            TransactionType = txn.DebitAmount > 0 ? "Debit" : "Credit",
                            ForeignAmount = txn.ForeignCurrencyAmount ?? 0,
                            ExchangeRate = txn.ExchangeRate ?? 0,
                            BaseAmount = txn.DebitAmount > 0 ? txn.DebitAmount : txn.CreditAmount,
                            IsRevaluation = txn.IsRevaluationEntry
                        };

                        // Update Running Balances
                        // Foreign Amount is usually signed or we need to know direction.
                        // In our system, ForeignCurrencyAmount seems to be absolute? 
                        // Let's check PostJournalEntry... it doesn't explicitly set ForeignCurrencyAmount sign.
                        // But usually ForeignAmount follows the Debit/Credit logic.
                        // If Debit > 0, it's a "Debit" in foreign currency too.
                        
                        decimal netBase = txn.DebitAmount - txn.CreditAmount;
                        decimal netForeign = 0;

                        // We need to infer direction of Foreign Amount if it's stored absolute.
                        // Assuming ForeignCurrencyAmount is absolute, we apply same sign as Base.
                        if (txn.DebitAmount > 0) netForeign = txn.ForeignCurrencyAmount ?? 0;
                        else netForeign = -(txn.ForeignCurrencyAmount ?? 0);

                        runningForeign += netForeign;
                        runningBase += netBase;

                        txnDetail.RunningBalanceForeign = runningForeign;
                        txnDetail.RunningBalanceBase = runningBase;

                        accountDetail.Transactions.Add(txnDetail);

                        // Accumulate Totals
                        if (txn.DebitAmount > 0)
                        {
                            accountDetail.TotalDebitsBase += txn.DebitAmount;
                            accountDetail.TotalDebitsForeign += (txn.ForeignCurrencyAmount ?? 0);
                        }
                        else
                        {
                            accountDetail.TotalCreditsBase += txn.CreditAmount;
                            accountDetail.TotalCreditsForeign += (txn.ForeignCurrencyAmount ?? 0);
                        }
                    }

                    accountDetail.ClosingBalanceForeign = runningForeign;
                    accountDetail.ClosingBalanceBase = runningBase;
                    
                    // Calculate Unrealized Gain/Loss embedded in this period?
                    // Or just the sum of revaluation entries?
                    accountDetail.UnrealizedGainLoss = periodTransactions
                        .Where(t => t.IsRevaluationEntry)
                        .Sum(t => t.DebitAmount - t.CreditAmount);

                    report.Accounts.Add(accountDetail);
                }
            }

            return report;
        }

        private IncomeStatementSectionDto BuildIncomeStatementSection(
            string sectionName,
            int sectionOrder,
            List<Account> accounts,
            Dictionary<Guid, decimal> accountActivity,
            string bookClassification,
            bool includeAccountDetails)
        {
            var section = new IncomeStatementSectionDto
            {
                SectionName = sectionName,
                SectionOrder = sectionOrder
            };

            // Group by line item
            var lineItemGroups = accounts
                .GroupBy(a => GetLineItem(a, bookClassification) ?? "Unclassified")
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var lineItemGroup in lineItemGroups)
            {
                var lineItem = new IncomeStatementLineItemDto
                {
                    LineItemName = lineItemGroup.Key,
                    Amount = lineItemGroup.Sum(a => accountActivity[a.Id]),
                    LineOrder = lineItemGroups.IndexOf(lineItemGroup) + 1
                };

                if (includeAccountDetails)
                {
                    lineItem.AccountNumbers = lineItemGroup.Select(a => a.AccountNumber).ToList();
                }

                section.LineItems.Add(lineItem);
            }

            section.SectionTotal = section.LineItems.Sum(li => li.Amount);
            return section;
        }

        private async Task<decimal> CalculateAccountActivityForPeriod(
            Guid tenantId,
            Guid accountId,
            DateTime periodStart,
            DateTime periodEnd,
            string bookClassification)
        {
            var endExclusive = periodEnd.Date.AddDays(1);
            var transactions = await BuildPostedLedgerQuery(tenantId, bookClassification)
                .Where(t => t.AccountId == accountId
                    && t.TransactionDate >= periodStart.Date
                    && t.TransactionDate < endExclusive)
                .ToListAsync();

            // For Revenue (credit-normal): Credit - Debit gives positive revenue
            // For Expense (debit-normal): Debit - Credit gives positive expense
            // We return Credit - Debit, so Revenue is positive, Expense is negative
            return transactions.Sum(t => t.CreditAmount - t.DebitAmount);
        }

        public async Task<DetailedLedgerReportDto> GenerateDetailedLedgerAsync(DetailedLedgerRequestDto request)
        {
            var tenantId = TenantId;

            if (request.EndDate.Date < request.StartDate.Date)
            {
                throw new ArgumentException("End date must be on or after start date.");
            }

            var startDate = request.StartDate.Date;
            var endExclusive = request.EndDate.Date.AddDays(1);
            var selectedAccountIds = request.AccountIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
            var bookClassification = NormalizeBookClassification(request.BookClassification);
            var dimensionFilters = await ResolveDimensionFiltersAsync(request.DimensionFilters);

            var accounts = await GetReportingAccountsAsync(
                bookClassification,
                selectedAccountIds,
                request.SegmentFilters);

            var report = new DetailedLedgerReportDto
            {
                CompanyName = await _tenantSettings.GetCompanyNameAsync(),
                ReportDate = DateTime.UtcNow,
                StartDate = startDate,
                EndDate = request.EndDate.Date,
                BookClassification = bookClassification,
                CurrencyCode = await _tenantSettings.GetBaseCurrencyAsync()
            };

            foreach (var account in accounts)
            {
                var transactionBaseQuery = ApplyDimensionFilters(
                        BuildPostedLedgerQuery(tenantId, bookClassification, request.IncludeReversed),
                        dimensionFilters)
                    .Where(t => t.AccountId == account.Id);

                if (!request.IncludeReversed)
                {
                    transactionBaseQuery = transactionBaseQuery.Where(t => !t.IsReversed);
                }

                var openingBalance = 0m;
                if (request.IncludeOpeningBalances)
                {
                    var openingMovements = await transactionBaseQuery
                        .Where(t => t.TransactionDate < startDate)
                        .Select(t => new { t.DebitAmount, t.CreditAmount })
                        .ToListAsync();

                    openingBalance = openingMovements.Sum(t => GetNormalBalanceMovement(account.AccountType, t.DebitAmount, t.CreditAmount));
                }

                var ledgerAccount = new DetailedLedgerAccountDto
                {
                    AccountId = account.Id,
                    AccountCode = account.AccountCode,
                    AccountNumber = account.AccountNumber,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType.ToString(),
                    OpeningBalance = Math.Abs(openingBalance),
                    OpeningBalanceType = GetBalanceType(account.AccountType, openingBalance)
                };

                var runningBalance = openingBalance;
                var periodTransactions = await transactionBaseQuery
                    .Where(t => t.TransactionDate >= startDate && t.TransactionDate < endExclusive)
                    .OrderBy(t => t.TransactionDate)
                    .ThenBy(t => t.JournalEntry.JournalEntryNumber)
                    .ThenBy(t => t.LineNumber)
                    .ToListAsync();

                foreach (var transaction in periodTransactions)
                {
                    runningBalance += GetNormalBalanceMovement(account.AccountType, transaction.DebitAmount, transaction.CreditAmount);

                    ledgerAccount.Lines.Add(new DetailedLedgerLineDto
                    {
                        TransactionId = transaction.Id,
                        JournalEntryId = transaction.JournalEntryId,
                        JournalEntryNumber = transaction.JournalEntry?.JournalEntryNumber ?? string.Empty,
                        TransactionDate = transaction.TransactionDate,
                        LineNumber = transaction.LineNumber,
                        Description = transaction.Description ?? transaction.JournalEntry?.Description ?? string.Empty,
                        Reference = transaction.SourceReferenceNumber
                            ?? transaction.JournalEntry?.ReferenceNumber
                            ?? transaction.ReferenceNumber
                            ?? string.Empty,
                        SourceModule = transaction.SourceModule ?? transaction.JournalEntry?.SourceModule ?? "GL",
                        PostingStatus = transaction.JournalEntry?.PostingStatus ?? transaction.PostingStatus,
                        DebitAmount = transaction.DebitAmount,
                        CreditAmount = transaction.CreditAmount,
                        RunningBalance = Math.Abs(runningBalance),
                        RunningBalanceType = GetBalanceType(account.AccountType, runningBalance),
                        CurrencyCode = transaction.TransactionCurrency,
                        ForeignAmount = transaction.ForeignCurrencyAmount,
                        ExchangeRate = transaction.ExchangeRate,
                        IsReversed = transaction.IsReversed,
                        SegmentString = transaction.SegmentString,
                        FinanceDimensionSetId = transaction.FinanceDimensionSetId,
                        FinanceDimensionDisplay = transaction.FinanceDimensionSnapshot?.DisplayValueSnapshot
                            ?? transaction.FinanceDimensionSet?.DisplayValue,
                        Dimensions = transaction.FinanceDimensionSnapshot?.Items
                            .OrderBy(item => item.DimensionCodeSnapshot)
                            .Select(item => new FinanceDimensionAssignmentDto
                            {
                                DefinitionId = item.FinanceDimensionDefinitionId,
                                ValueId = item.FinanceDimensionValueId,
                                DimensionCode = item.DimensionCodeSnapshot,
                                DimensionName = item.DimensionNameSnapshot,
                                ValueCode = item.DimensionValueCodeSnapshot,
                                ValueName = item.DimensionValueNameSnapshot
                            }).ToList()
                            ?? transaction.FinanceDimensionSet?.Items
                            .OrderBy(item => item.DimensionCodeSnapshot)
                            .Select(item => new FinanceDimensionAssignmentDto
                            {
                                DefinitionId = item.FinanceDimensionDefinitionId,
                                ValueId = item.FinanceDimensionValueId,
                                DimensionCode = item.DimensionCodeSnapshot,
                                DimensionName = item.DimensionNameSnapshot,
                                ValueCode = item.DimensionValueCodeSnapshot,
                                ValueName = item.DimensionValueNameSnapshot
                            })
                            .ToList() ?? new List<FinanceDimensionAssignmentDto>()
                    });
                }

                ledgerAccount.TotalDebits = ledgerAccount.Lines.Sum(l => l.DebitAmount);
                ledgerAccount.TotalCredits = ledgerAccount.Lines.Sum(l => l.CreditAmount);
                ledgerAccount.ClosingBalance = Math.Abs(runningBalance);
                ledgerAccount.ClosingBalanceType = GetBalanceType(account.AccountType, runningBalance);

                report.TotalDebits += ledgerAccount.TotalDebits;
                report.TotalCredits += ledgerAccount.TotalCredits;
                report.Accounts.Add(ledgerAccount);
            }

            return report;
        }

        private static decimal GetNormalBalanceMovement(AccountType accountType, decimal debitAmount, decimal creditAmount)
        {
            var isDebitNormal = accountType == AccountType.Asset || accountType == AccountType.Expense;
            return isDebitNormal ? debitAmount - creditAmount : creditAmount - debitAmount;
        }

        private static string GetBalanceType(AccountType accountType, decimal normalBalance)
        {
            if (normalBalance == 0)
            {
                return string.Empty;
            }

            var isDebitNormal = accountType == AccountType.Asset || accountType == AccountType.Expense;
            var isNormalBalance = normalBalance > 0;
            return isDebitNormal == isNormalBalance ? "Debit" : "Credit";
        }

        public async Task<CashBankLedgerReportDto> GenerateCashBankLedgerAsync(CashBankLedgerRequestDto request)
        {
            var tenantId = TenantId;
            if (request.EndDate.Date < request.StartDate.Date)
            {
                throw new ArgumentException("End date must be on or after start date.");
            }

            var startDate = request.StartDate.Date;
            var endDate = request.EndDate.Date;
            var endExclusive = endDate.AddDays(1);
            var bookClassification = NormalizeBookClassification(request.BookClassification);
            var requestedBankAccountIds = request.BankAccountIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray() ?? Array.Empty<Guid>();
            var requestedGlAccountIds = request.GlAccountIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray() ?? Array.Empty<Guid>();

            IQueryable<BankAccount> bankQuery = _context.BankAccounts
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId && !b.IsDeleted && b.GLAccountId.HasValue);

            if (requestedBankAccountIds.Length > 0)
            {
                var ownedBankCount = await bankQuery.CountAsync(b => requestedBankAccountIds.Contains(b.Id));
                if (ownedBankCount != requestedBankAccountIds.Length)
                {
                    throw new InvalidOperationException("One or more cash/bank report bank-account filters do not belong to the current tenant.");
                }

                bankQuery = bankQuery.Where(b => requestedBankAccountIds.Contains(b.Id));
            }

            if (requestedGlAccountIds.Length > 0)
            {
                bankQuery = bankQuery.Where(b => requestedGlAccountIds.Contains(b.GLAccountId!.Value));
            }

            var bankAccounts = await bankQuery
                .OrderBy(b => b.AccountNumber)
                .ToListAsync();
            var glAccountIds = bankAccounts
                .Select(b => b.GLAccountId!.Value)
                .Distinct()
                .ToArray();

            if (requestedGlAccountIds.Length > 0)
            {
                var requestedWithoutBankMapping = requestedGlAccountIds.Except(glAccountIds).ToArray();
                if (requestedWithoutBankMapping.Length > 0)
                {
                    await GetReportingAccountsAsync(bookClassification, requestedGlAccountIds, request.SegmentFilters, new[] { AccountType.Asset });
                    throw new InvalidOperationException("One or more GL account filters are not mapped to a tenant-owned cash/bank account.");
                }
            }

            var glAccounts = await GetReportingAccountsAsync(
                bookClassification,
                glAccountIds,
                request.SegmentFilters,
                new[] { AccountType.Asset });
            var glAccountsById = glAccounts.ToDictionary(a => a.Id);
            bankAccounts = bankAccounts
                .Where(b => b.GLAccountId.HasValue && glAccountsById.ContainsKey(b.GLAccountId.Value))
                .ToList();
            glAccountIds = bankAccounts.Select(b => b.GLAccountId!.Value).Distinct().ToArray();

            var openingBalances = request.IncludeOpeningBalances
                ? await CalculatePostedRawBalancesAsOfAsync(
                    tenantId,
                    glAccountIds,
                    startDate.AddDays(-1),
                    bookClassification)
                : new Dictionary<Guid, decimal>();

            var report = new CashBankLedgerReportDto
            {
                CompanyName = await _tenantSettings.GetCompanyNameAsync(),
                StartDate = startDate,
                EndDate = endDate,
                BookClassification = bookClassification,
                CurrencyCode = await _tenantSettings.GetBaseCurrencyAsync()
            };

            foreach (var bankAccount in bankAccounts)
            {
                var glAccountId = bankAccount.GLAccountId!.Value;
                var glAccount = glAccountsById[glAccountId];
                var rawOpening = openingBalances.GetValueOrDefault(glAccountId);
                var opening = ToStatementNormalBalance(glAccount.AccountType, rawOpening);
                var runningBalance = opening;
                var ledgerAccount = new CashBankLedgerAccountDto
                {
                    BankAccountId = bankAccount.Id,
                    BankAccountNumber = bankAccount.AccountNumber,
                    BankAccountName = bankAccount.AccountName,
                    BankCurrencyCode = bankAccount.Currency,
                    GlAccountId = glAccount.Id,
                    GlAccountNumber = glAccount.AccountNumber,
                    GlAccountName = glAccount.AccountName,
                    OpeningBalance = opening,
                    StoredSnapshotBalance = bankAccount.CurrentBalance
                };

                var periodTransactions = await BuildPostedLedgerQuery(tenantId, bookClassification)
                    .Where(t =>
                        t.AccountId == glAccountId &&
                        t.TransactionDate >= startDate &&
                        t.TransactionDate < endExclusive)
                    .OrderBy(t => t.TransactionDate)
                    .ThenBy(t => t.JournalEntry.JournalEntryNumber)
                    .ThenBy(t => t.LineNumber)
                    .ToListAsync();

                foreach (var transaction in periodTransactions)
                {
                    runningBalance += ToStatementNormalBalance(
                        glAccount.AccountType,
                        transaction.DebitAmount - transaction.CreditAmount);

                    ledgerAccount.Lines.Add(new CashBankLedgerLineDto
                    {
                        TransactionId = transaction.Id,
                        JournalEntryId = transaction.JournalEntryId,
                        TransactionDate = transaction.TransactionDate,
                        JournalEntryNumber = transaction.JournalEntry?.JournalEntryNumber ?? string.Empty,
                        Description = transaction.Description ?? transaction.JournalEntry?.Description ?? string.Empty,
                        Reference = transaction.SourceReferenceNumber
                            ?? transaction.JournalEntry?.ReferenceNumber
                            ?? string.Empty,
                        SourceModule = transaction.SourceModule ?? transaction.JournalEntry?.SourceModule ?? "GL",
                        SourceDocumentType = transaction.SourceDocumentType
                            ?? transaction.JournalEntry?.SourceDocumentType
                            ?? string.Empty,
                        SourceDocumentId = transaction.SourceDocumentId ?? transaction.JournalEntry?.SourceDocumentId,
                        DebitAmount = transaction.DebitAmount,
                        CreditAmount = transaction.CreditAmount,
                        RunningBalance = runningBalance,
                        SegmentString = transaction.SegmentString
                    });
                }

                ledgerAccount.Receipts = ledgerAccount.Lines.Sum(l => l.DebitAmount);
                ledgerAccount.Payments = ledgerAccount.Lines.Sum(l => l.CreditAmount);
                ledgerAccount.ClosingBalance = runningBalance;
                ledgerAccount.SnapshotComparisonAvailable = string.Equals(
                    bankAccount.Currency,
                    report.CurrencyCode,
                    StringComparison.OrdinalIgnoreCase);
                report.Accounts.Add(ledgerAccount);
            }

            report.TotalOpeningBalance = report.Accounts.Sum(a => a.OpeningBalance);
            report.TotalReceipts = report.Accounts.Sum(a => a.Receipts);
            report.TotalPayments = report.Accounts.Sum(a => a.Payments);
            report.TotalClosingBalance = report.Accounts.Sum(a => a.ClosingBalance);

            return report;
        }

        public async Task<TrialBalanceDto> GenerateTrialBalanceAsync(TrialBalanceRequestDto request)
        {
            var tenantId = TenantId;
            var bookClassification = NormalizeBookClassification(request.BookClassification);
            var dimensionFilters = await ResolveDimensionFiltersAsync(request.DimensionFilters);

            var accounts = await GetReportingAccountsAsync(
                bookClassification,
                request.AccountIds,
                request.SegmentFilters);

            var accountIds = accounts.Select(a => a.Id).ToArray();
            var asAtDate = request.AsAtDate.Date;
            var periodStart = request.PeriodStart?.Date
                ?? await _context.FiscalPeriods
                    .AsNoTracking()
                    .Where(p => p.TenantId == tenantId
                        && p.StartDate <= asAtDate
                        && p.EndDate >= asAtDate)
                    .OrderByDescending(p => p.StartDate)
                    .Select(p => (DateTime?)p.StartDate.Date)
                    .FirstOrDefaultAsync();

            var openingBalances = periodStart.HasValue
                ? await CalculatePostedRawBalancesAsOfAsync(
                    tenantId,
                    accountIds,
                    periodStart.Value.AddDays(-1),
                    bookClassification,
                    dimensionFilters)
                : new Dictionary<Guid, decimal>();

            var periodMovements = periodStart.HasValue
                ? await CalculatePostedPeriodMovementAsync(
                    tenantId,
                    accountIds,
                    periodStart.Value,
                    asAtDate,
                    bookClassification,
                    dimensionFilters)
                : await CalculatePostedPeriodMovementAsync(
                    tenantId,
                    accountIds,
                    DateTime.MinValue,
                    asAtDate,
                    bookClassification,
                    dimensionFilters);

            var trialBalance = new TrialBalanceDto
            {
                CompanyName = await _tenantSettings.GetCompanyNameAsync(),
                AsAtDate = asAtDate,
                PeriodStart = periodStart,
                BookClassification = bookClassification,
                CurrencyCode = await _tenantSettings.GetBaseCurrencyAsync()
            };

            var lines = new List<TrialBalanceLineDto>();

            foreach (var account in accounts)
            {
                var openingBalance = openingBalances.GetValueOrDefault(account.Id);
                var movement = periodMovements.GetValueOrDefault(account.Id);
                var periodDebits = movement?.Debits ?? 0m;
                var periodCredits = movement?.Credits ?? 0m;
                var closingBalance = openingBalance + periodDebits - periodCredits;

                if (!request.IncludeZeroBalances
                    && openingBalance == 0
                    && periodDebits == 0
                    && periodCredits == 0
                    && closingBalance == 0)
                {
                    continue;
                }

                var line = new TrialBalanceLineDto
                {
                    AccountId = account.Id,
                    AccountCode = account.AccountCode,
                    AccountNumber = account.AccountNumber,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType.ToString(),
                    PeriodDebits = periodDebits,
                    PeriodCredits = periodCredits
                };

                ApplyRawOpeningBalanceToTrialBalanceLine(line, openingBalance);
                ApplyRawBalanceToTrialBalanceLine(line, closingBalance);

                lines.Add(line);
            }

            trialBalance.Lines = lines;

            trialBalance.TotalDebits = lines.Sum(l => l.DebitBalance);
            trialBalance.TotalCredits = lines.Sum(l => l.CreditBalance);

            return trialBalance;
        }

        public async Task<CashFlowStatementDto> GenerateCashFlowStatementAsync(CashFlowStatementRequestDto request)
        {
            var tenantId = TenantId;
            if (request.PeriodEnd.Date < request.PeriodStart.Date)
            {
                throw new ArgumentException("Period end must be on or after period start.");
            }

            var periodStart = request.PeriodStart.Date;
            var periodEnd = request.PeriodEnd.Date;
            var bookClassification = NormalizeBookClassification(request.BookClassification);
            var cashFlowMethod = NormalizeCashFlowMethod(request.Method);

            var cashFlowStatement = new CashFlowStatementDto
            {
                CompanyName = await _tenantSettings.GetCompanyNameAsync(),
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                BookClassification = bookClassification,
                CurrencyCode = await _tenantSettings.GetBaseCurrencyAsync(),
                Method = cashFlowMethod
            };

            // Bank-master mappings and configured classification roles are the only
            // authoritative ways an account enters cash-flow processing.
            var mappedBankGlAccountIds = await _context.BankAccounts
                .AsNoTracking()
                .Where(bank =>
                    bank.TenantId == tenantId &&
                    !bank.IsDeleted &&
                    bank.GLAccountId.HasValue)
                .Select(bank => bank.GLAccountId!.Value)
                .Distinct()
                .ToListAsync();

            var roleBasedCashAccountIds = await _context.AccountAccountingBooks
                .AsNoTracking()
                .Where(mapping =>
                    mapping.TenantId == tenantId && !mapping.IsDeleted && mapping.IsEnabled
                    && mapping.AccountingBook.Code == bookClassification
                    && mapping.AccountingBook.IsActive && mapping.AccountingBook.AllowsPosting
                    && mapping.AccountClassification != null
                    && mapping.AccountClassification.Status == AccountClassificationStatus.Active
                    && (mapping.AccountClassification.SystemRole == AccountClassificationSystemRole.Cash
                        || mapping.AccountClassification.SystemRole == AccountClassificationSystemRole.Bank))
                .Select(mapping => mapping.AccountId)
                .Distinct()
                .ToListAsync();
            var cashAccountIds = mappedBankGlAccountIds.Concat(roleBasedCashAccountIds).ToHashSet();

            var activityEndExclusive = periodEnd.AddDays(1);
            var cashActivityJournalIds = cashAccountIds.Count == 0
                ? new List<Guid>()
                : await BuildPostedLedgerQuery(tenantId, bookClassification)
                    .Where(transaction =>
                        cashAccountIds.Contains(transaction.AccountId) &&
                        transaction.TransactionDate >= periodStart &&
                        transaction.TransactionDate < activityEndExclusive &&
                        !(transaction.SourceModule == "MIGRATION" &&
                          transaction.SourceDocumentType == "OpeningBalanceBatch"))
                    .Select(transaction => transaction.JournalEntryId)
                    .Distinct()
                    .ToListAsync();

            // Every non-cash counterpart in a cash-touching journal must identify its
            // statement section. Silently dropping an unclassified account produces a
            // balanced GL but a false cash-flow statement, so reporting fails closed.
            if (cashActivityJournalIds.Count > 0)
            {
                var cashCounterpartAccountIds = await BuildPostedLedgerQuery(tenantId, bookClassification)
                    .Where(transaction =>
                        cashActivityJournalIds.Contains(transaction.JournalEntryId) &&
                        !cashAccountIds.Contains(transaction.AccountId) &&
                        transaction.TransactionDate >= periodStart &&
                        transaction.TransactionDate < activityEndExclusive &&
                        !(transaction.SourceModule == "MIGRATION" &&
                          transaction.SourceDocumentType == "OpeningBalanceBatch"))
                    .Select(transaction => transaction.AccountId)
                    .Distinct()
                    .ToListAsync();

                var cashCounterpartAccounts = await _context.Accounts
                    .AsNoTracking()
                    .Where(account =>
                        account.TenantId == tenantId &&
                        !account.IsDeleted &&
                        cashCounterpartAccountIds.Contains(account.Id))
                    .ToListAsync();
                var foundCounterpartIds = cashCounterpartAccounts.Select(account => account.Id).ToHashSet();
                var unavailableCounterparts = cashCounterpartAccountIds
                    .Where(accountId => !foundCounterpartIds.Contains(accountId))
                    .OrderBy(accountId => accountId)
                    .ToList();
                if (unavailableCounterparts.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Cash-flow reporting cannot resolve active tenant account(s) referenced by cash journals: " +
                        string.Join(", ", unavailableCounterparts) +
                        ". Restore the account lineage before generating the statement.");
                }

                var unclassifiedCounterparts = cashCounterpartAccounts
                    .Where(account => NormalizeCashFlowSection(account.CashFlowClassification) == null)
                    .OrderBy(account => account.AccountNumber)
                    .Select(account => $"{account.AccountNumber} - {account.AccountName}")
                    .ToList();

                if (unclassifiedCounterparts.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Cash-flow classification is required for cash-journal counterpart account(s): " +
                        string.Join(", ", unclassifiedCounterparts) +
                        ". Assign Operating, Investing, or Financing in Chart of Accounts.");
                }
            }

            // Get all accounts with cash flow classifications
            var accounts = await _context.Accounts
                .Where(account =>
                    account.TenantId == tenantId &&
                    !account.IsDeleted &&
                    account.CashFlowClassification != null &&
                    !cashAccountIds.Contains(account.Id))
                .ToListAsync();

            // Calculate activity for each account during the period
            var accountActivity = new Dictionary<Guid, decimal>();
            foreach (var account in accounts)
            {
                var activity = await CalculateCashFlowAccountActivityForPeriod(
                    tenantId,
                    account.Id,
                    periodStart,
                    periodEnd,
                    bookClassification,
                    cashActivityJournalIds);
                if (Math.Abs(activity) > 0.01m)
                {
                    accountActivity[account.Id] = activity;
                }
            }

            // Separate accounts by cash flow classification
            var operatingAccounts = accounts.Where(a => NormalizeCashFlowSection(a.CashFlowClassification) == "Operating" && accountActivity.ContainsKey(a.Id)).ToList();
            var investingAccounts = accounts.Where(a => NormalizeCashFlowSection(a.CashFlowClassification) == "Investing" && accountActivity.ContainsKey(a.Id)).ToList();
            var financingAccounts = accounts.Where(a => NormalizeCashFlowSection(a.CashFlowClassification) == "Financing" && accountActivity.ContainsKey(a.Id)).ToList();

            // Build Operating Activities section
            cashFlowStatement.OperatingActivities = BuildCashFlowSection(
                "Cash Flows from Operating Activities",
                1,
                operatingAccounts,
                accountActivity,
                bookClassification,
                request.IncludeAccountDetails);

            if (cashFlowMethod == "Indirect")
            {
                cashFlowStatement.OperatingActivities = await BuildIndirectOperatingCashFlowSectionAsync(
                    tenantId,
                    periodStart,
                    periodEnd,
                    bookClassification,
                    accounts,
                    cashFlowStatement.OperatingActivities.SectionTotal,
                    request.IncludeAccountDetails,
                    cashFlowStatement.PresentationWarnings);
            }

            // Build Investing Activities section
            cashFlowStatement.InvestingActivities = BuildCashFlowSection(
                "Cash Flows from Investing Activities",
                2,
                investingAccounts,
                accountActivity,
                bookClassification,
                request.IncludeAccountDetails);

            // Build Financing Activities section
            cashFlowStatement.FinancingActivities = BuildCashFlowSection(
                "Cash Flows from Financing Activities",
                3,
                financingAccounts,
                accountActivity,
                bookClassification,
                request.IncludeAccountDetails);

            // Calculate totals
            cashFlowStatement.NetCashFromOperating = cashFlowStatement.OperatingActivities.SectionTotal;
            cashFlowStatement.NetCashFromInvesting = cashFlowStatement.InvestingActivities.SectionTotal;
            cashFlowStatement.NetCashFromFinancing = cashFlowStatement.FinancingActivities.SectionTotal;
            cashFlowStatement.NetIncreaseInCash = cashFlowStatement.NetCashFromOperating + 
                                                   cashFlowStatement.NetCashFromInvesting + 
                                                   cashFlowStatement.NetCashFromFinancing;

            decimal cashAtBeginning = 0;
            decimal cashAtEnd = 0;

            foreach (var cashAccountId in cashAccountIds)
            {
                cashAtBeginning += await CalculateAccountBalanceAsOf(
                    tenantId,
                    cashAccountId,
                    periodStart.AddDays(-1),
                    bookClassification);
                cashAtEnd += await CalculateAccountBalanceAsOf(
                    tenantId,
                    cashAccountId,
                    periodEnd,
                    bookClassification);
            }

            // A governed opening posted on the first report date represents the position at
            // the opening boundary. It is not a receipt generated during that reporting day.
            // Add only the cash legs to beginning cash; the helper above excludes the entire
            // opening batch from operating, investing and financing activity.
            if (cashAccountIds.Count > 0)
            {
                var firstDayEndExclusive = periodStart.AddDays(1);
                var cutoverCashAtBoundary = await BuildPostedLedgerQuery(tenantId, bookClassification)
                    .Where(transaction =>
                        cashAccountIds.Contains(transaction.AccountId) &&
                        transaction.TransactionDate >= periodStart &&
                        transaction.TransactionDate < firstDayEndExclusive &&
                        transaction.SourceModule == "MIGRATION" &&
                        transaction.SourceDocumentType == "OpeningBalanceBatch")
                    .SumAsync(transaction => (decimal?)(transaction.DebitAmount - transaction.CreditAmount)) ?? 0m;
                cashAtBeginning += cutoverCashAtBoundary;
            }

            cashFlowStatement.CashAtBeginning = cashAtBeginning;
            cashFlowStatement.CashAtEnd = cashAtEnd;

            return cashFlowStatement;
        }

        private async Task<CashFlowSectionDto> BuildIndirectOperatingCashFlowSectionAsync(
            Guid tenantId,
            DateTime periodStart,
            DateTime periodEnd,
            string bookClassification,
            IReadOnlyCollection<Account> classifiedAccounts,
            decimal directOperatingCash,
            bool includeAccountDetails,
            ICollection<string> presentationWarnings)
        {
            var incomeStatement = await GenerateIncomeStatementAsync(new IncomeStatementRequestDto
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                BookClassification = bookClassification,
                IncludeAccountDetails = false
            });

            var section = new CashFlowSectionDto
            {
                SectionName = "Cash Flows from Operating Activities (Indirect Method)",
                SectionOrder = 1,
                LineItems =
                {
                    new CashFlowLineItemDto
                    {
                        LineItemName = "Profit for the period",
                        Amount = incomeStatement.NetProfit,
                        LineOrder = 1
                    }
                }
            };

            var workingCapitalAccounts = classifiedAccounts
                .Where(account =>
                    NormalizeCashFlowSection(account.CashFlowClassification) == "Operating" &&
                    account.AccountType is AccountType.Asset or AccountType.Liability)
                .ToList();
            var workingCapitalMovements = await CalculateCashFlowPeriodRawMovementsAsync(
                tenantId,
                workingCapitalAccounts.Select(account => account.Id).ToArray(),
                periodStart,
                periodEnd,
                bookClassification);

            var workingCapitalLines = workingCapitalAccounts
                .Where(account => workingCapitalMovements.ContainsKey(account.Id))
                .Select(account =>
                {
                    var normalMovement = ToStatementNormalBalance(
                        account.AccountType,
                        workingCapitalMovements[account.Id]);
                    var cashAdjustment = account.AccountType == AccountType.Asset
                        ? -normalMovement
                        : normalMovement;
                    return new
                    {
                        Name = $"Change in {GetLineItem(account, bookClassification) ?? account.AccountName}",
                        Account = account,
                        Amount = cashAdjustment
                    };
                })
                .Where(item => Math.Abs(item.Amount) > 0.01m)
                .GroupBy(item => item.Name)
                .OrderBy(group => group.Key)
                .Select(group => new CashFlowLineItemDto
                {
                    LineItemName = group.Key,
                    Amount = group.Sum(item => item.Amount),
                    AccountNumbers = includeAccountDetails
                        ? group.Select(item => item.Account.AccountNumber).OrderBy(number => number).ToList()
                        : null
                })
                .ToList();

            foreach (var line in workingCapitalLines)
            {
                line.LineOrder = section.LineItems.Count + 1;
                section.LineItems.Add(line);
            }

            var explainedOperatingCash = section.LineItems.Sum(line => line.Amount);
            var otherNonCashAdjustments = directOperatingCash - explainedOperatingCash;
            if (Math.Abs(otherNonCashAdjustments) > 0.01m)
            {
                section.LineItems.Add(new CashFlowLineItemDto
                {
                    LineItemName = "Other non-cash and classification adjustments",
                    Amount = otherNonCashAdjustments,
                    LineOrder = section.LineItems.Count + 1
                });
                presentationWarnings.Add(
                    "Indirect operating cash flow contains a ledger-derived residual adjustment. " +
                    "Review operating working-capital classifications and non-cash journals before final sign-off.");
            }

            section.SectionTotal = section.LineItems.Sum(line => line.Amount);
            return section;
        }

        private async Task<Dictionary<Guid, decimal>> CalculateCashFlowPeriodRawMovementsAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> accountIds,
            DateTime periodStart,
            DateTime periodEnd,
            string bookClassification)
        {
            if (accountIds.Count == 0)
                return new Dictionary<Guid, decimal>();

            var endExclusive = periodEnd.Date.AddDays(1);
            return await BuildPostedLedgerQuery(tenantId, bookClassification)
                .Where(transaction =>
                    accountIds.Contains(transaction.AccountId) &&
                    transaction.TransactionDate >= periodStart.Date &&
                    transaction.TransactionDate < endExclusive &&
                    !(transaction.SourceModule == "MIGRATION" &&
                      transaction.SourceDocumentType == "OpeningBalanceBatch"))
                .GroupBy(transaction => transaction.AccountId)
                .Select(group => new
                {
                    AccountId = group.Key,
                    RawMovement = group.Sum(transaction => transaction.DebitAmount - transaction.CreditAmount)
                })
                .ToDictionaryAsync(item => item.AccountId, item => item.RawMovement);
        }

        private async Task<decimal> CalculateCashFlowAccountActivityForPeriod(
            Guid tenantId,
            Guid accountId,
            DateTime periodStart,
            DateTime periodEnd,
            string bookClassification,
            IReadOnlyCollection<Guid> cashActivityJournalIds)
        {
            if (cashActivityJournalIds.Count == 0)
            {
                return 0m;
            }

            var endExclusive = periodEnd.Date.AddDays(1);
            var transactions = await BuildPostedLedgerQuery(tenantId, bookClassification)
                .Where(transaction =>
                    transaction.AccountId == accountId &&
                    cashActivityJournalIds.Contains(transaction.JournalEntryId) &&
                    transaction.TransactionDate >= periodStart.Date &&
                    transaction.TransactionDate < endExclusive &&
                    !(transaction.SourceModule == "MIGRATION" &&
                      transaction.SourceDocumentType == "OpeningBalanceBatch"))
                .ToListAsync();

            return transactions.Sum(transaction => transaction.CreditAmount - transaction.DebitAmount);
        }

        private CashFlowSectionDto BuildCashFlowSection(
            string sectionName,
            int sectionOrder,
            List<Account> accounts,
            Dictionary<Guid, decimal> accountActivity,
            string bookClassification,
            bool includeAccountDetails)
        {
            var section = new CashFlowSectionDto
            {
                SectionName = sectionName,
                SectionOrder = sectionOrder
            };

            // Group by line item
            var lineItemGroups = accounts
                .GroupBy(a => GetLineItem(a, bookClassification) ?? "Other")
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var lineItemGroup in lineItemGroups)
            {
                // For cash flow, we want actual cash movements
                // Positive = cash inflow, Negative = cash outflow
                var amount = lineItemGroup.Sum(a => accountActivity[a.Id]);

                var lineItem = new CashFlowLineItemDto
                {
                    LineItemName = lineItemGroup.Key,
                    Amount = amount,
                    LineOrder = lineItemGroups.IndexOf(lineItemGroup) + 1
                };

                if (includeAccountDetails)
                {
                    lineItem.AccountNumbers = lineItemGroup.Select(a => a.AccountNumber).ToList();
                }

                section.LineItems.Add(lineItem);
            }

            section.SectionTotal = section.LineItems.Sum(li => li.Amount);
            return section;
        }

        private static string NormalizeCashFlowMethod(string? method)
        {
            return (method ?? "Indirect").Trim().ToUpperInvariant() switch
            {
                "DIRECT" => "Direct",
                "INDIRECT" => "Indirect",
                _ => throw new ArgumentException("Cash-flow method must be Direct or Indirect.", nameof(method))
            };
        }

        private static string? NormalizeCashFlowSection(string? classification)
        {
            if (string.IsNullOrWhiteSpace(classification))
                return null;

            return classification.Trim().ToUpperInvariant() switch
            {
                "OPERATING" => "Operating",
                "INVESTING" => "Investing",
                "FINANCING" => "Financing",
                _ => null
            };
        }

        #endregion

        #region Period-End Close

        public async Task<PeriodCloseValidationDto> ValidatePeriodCloseAsync(Guid fiscalPeriodId)
        {
            // GeneralLedgerService remains the reporting/year-close facade, but fiscal-period
            // readiness has one owner. Delegation prevents this older API contract from drifting
            // away from the persisted checks enforced by FiscalPeriodService.
            return await _fiscalPeriodService.ValidatePeriodCloseAsync(fiscalPeriodId);
        }

        public async Task<PeriodCloseResultDto> CloseFiscalPeriodAsync(PeriodCloseRequestDto request)
        {
            // Do not maintain a second mutation path. The primary service owns close-cycle
            // evidence, maker-checker certification, audit and period status as one operation.
            return await _fiscalPeriodService.ClosePeriodAsync(request);
        }

        public async Task<PeriodCloseResultDto> ReopenFiscalPeriodAsync(PeriodReopenRequestDto request)
        {
            // This legacy GL facade now submits the same controlled maker request as the Finance
            // period screen. It must never bypass higher-tier approval by editing FiscalPeriod.
            var reopenRequest = await _fiscalPeriodService.RequestPeriodReopenAsync(request);
            return new PeriodCloseResultDto
            {
                Success = true,
                Message = "Period reopen request submitted; the period remains closed pending independent higher-tier approval.",
                FiscalPeriodId = reopenRequest.FiscalPeriodId
            };
        }

        public async Task LockFiscalPeriodAsync(Guid fiscalPeriodId, string lockReason)
        {
            // Lock audit and status normalization are owned by FiscalPeriodService.
            await _fiscalPeriodService.LockPeriodAsync(new PeriodLockRequestDto
            {
                FiscalPeriodId = fiscalPeriodId,
                LockReason = lockReason
            });
        }

        public async Task UnlockFiscalPeriodAsync(Guid fiscalPeriodId, string unlockReason)
        {
            // Unlocks are exceptional control events; delegate so the mandatory reason and
            // Finance audit trail cannot be skipped through this compatibility facade.
            await _fiscalPeriodService.UnlockPeriodAsync(fiscalPeriodId, unlockReason);
        }

        public async Task<PeriodCloseResultDto> CloseFiscalYearAsync(YearEndCloseRequestDto request)
        {
            var tenantId = TenantId;
            var userId = _currentUserService.UserId;
            if (userId == null)
                throw new InvalidOperationException("User context is required.");

            var fiscalYear = await _context.FiscalYears
                .Include(fy => fy.FiscalPeriods)
                .FirstOrDefaultAsync(fy => fy.Id == request.FiscalYearId && fy.TenantId == tenantId);

            if (fiscalYear == null)
                throw new ArgumentException($"Fiscal year {request.FiscalYearId} not found");

            if (fiscalYear.IsClosed)
            {
                return new PeriodCloseResultDto
                {
                    Success = false,
                    Message = $"Fiscal year '{fiscalYear.FiscalYearName}' is already closed.",
                    Errors = new List<string> { "Reopen the fiscal year before closing it again." }
                };
            }

            // Validate all periods are closed
            var openPeriods = fiscalYear.FiscalPeriods.Where(p => !p.IsClosed).ToList();
            if (openPeriods.Any())
            {
                return new PeriodCloseResultDto
                {
                    Success = false,
                    Message = "Cannot close fiscal year - some periods are still open",
                    Errors = openPeriods.Select(p => $"Period '{p.PeriodName}' is still open").ToList()
                };
            }

            var periodIds = fiscalYear.FiscalPeriods.Select(p => p.Id).ToList();
            var unpostedJournalEntries = await _context.JournalEntries
                .CountAsync(je => je.TenantId == tenantId
                    && periodIds.Contains(je.FiscalPeriodId)
                    && !je.IsDeleted
                    && je.PostingStatus != "Posted"
                    && je.PostingStatus != "Reversed");

            if (unpostedJournalEntries > 0)
            {
                return new PeriodCloseResultDto
                {
                    Success = false,
                    Message = "Cannot close fiscal year - unposted journal entries remain",
                    Errors = new List<string>
                    {
                        $"{unpostedJournalEntries} journal entr{(unpostedJournalEntries == 1 ? "y is" : "ies are")} not posted. Post or remove all draft, submitted, and approved entries before closing the year."
                    }
                };
            }

            var unpostedTransactionLines = await _context.AccountTransactions
                .CountAsync(t => t.TenantId == tenantId
                    && periodIds.Contains(t.FiscalPeriodId)
                    && !t.IsDeleted
                    && t.PostingStatus != "Posted"
                    && t.PostingStatus != "Reversed");

            if (unpostedTransactionLines > 0)
            {
                return new PeriodCloseResultDto
                {
                    Success = false,
                    Message = "Cannot close fiscal year - unposted transaction lines remain",
                    Errors = new List<string>
                    {
                        $"{unpostedTransactionLines} account transaction line{(unpostedTransactionLines == 1 ? " is" : "s are")} not posted. Resolve the ledger data before closing the year."
                    }
                };
            }

            // Keep the closing journal posting and the fiscal-year state flip in one commit so a
            // failure between them cannot leave a posted closing entry on an open year.
            await using var dbTransaction = await _context.Database.BeginTransactionAsync();

            var (closingJournalEntryId, netIncome) = await TransferRetainedEarningsAsync(fiscalYear, request.RetainedEarningsAccountId);

            // Update fiscal year
            fiscalYear.IsClosed = true;
            fiscalYear.IsActive = false;
            fiscalYear.Status = "Closed";
            fiscalYear.ClosedDate = DateTime.UtcNow;
            fiscalYear.ClosedByUserId = Guid.Parse(userId);
            fiscalYear.RetainedEarningsTransferComplete = true;
            fiscalYear.RetainedEarningsTransferDate = DateTime.UtcNow;
            fiscalYear.ClosingJournalEntryId = closingJournalEntryId;
            fiscalYear.NetIncomeTransferred = netIncome;
            fiscalYear.YearEndClosingNotes = request.ClosingNotes;

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return new PeriodCloseResultDto
            {
                Success = true,
                Message = $"Fiscal year '{fiscalYear.FiscalYearName}' closed successfully. Net income transferred: {fiscalYear.NetIncomeTransferred:N2}",
                FiscalPeriodId = fiscalYear.Id,
                PeriodName = fiscalYear.FiscalYearName,
                ClosedDate = fiscalYear.ClosedDate
            };
        }

        public async Task<PeriodCloseResultDto> ReopenFiscalYearAsync(Guid fiscalYearId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reason is required to reopen a fiscal year.");

            var tenantId = TenantId;
            var userId = _currentUserService.UserId;
            if (userId == null)
                throw new InvalidOperationException("User context is required.");

            var fiscalYear = await _context.FiscalYears
                .Include(fy => fy.FiscalPeriods)
                .FirstOrDefaultAsync(fy => fy.Id == fiscalYearId && fy.TenantId == tenantId);

            if (fiscalYear == null)
                throw new ArgumentException($"Fiscal year {fiscalYearId} not found");

            if (!fiscalYear.IsClosed)
                throw new InvalidOperationException($"Fiscal year '{fiscalYear.FiscalYearName}' is not closed.");

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();

            // Reverse the closing entry through the posting engine so account balance
            // snapshots and posting-event back-references stay correct.
            if (fiscalYear.ClosingJournalEntryId.HasValue)
            {
                var closingEntry = await _context.JournalEntries
                    .Include(je => je.Transactions)
                    .FirstOrDefaultAsync(je => je.Id == fiscalYear.ClosingJournalEntryId.Value && je.TenantId == tenantId);

                if (closingEntry == null)
                    throw new InvalidOperationException("The fiscal year's closing journal entry could not be found.");

                var reversalRequest = new FinancePostingRequestV2Dto
                {
                    SourceModule = "GL",
                    SourceDocumentType = "YearEndCloseReversal",
                    SourceDocumentId = fiscalYear.Id,
                    SourceDocumentTenantId = tenantId,
                    ReversalOfJournalEntryId = closingEntry.Id,
                    ReversalReason = reason.Trim(),
                    ReversalType = "Manual",
                    PostingAction = "Reverse",
                    SourceDocumentReference = fiscalYear.FiscalYearCode,
                    Description = $"Reopen fiscal year - reversal of year-end close for {fiscalYear.FiscalYearName}",
                    PostingDate = closingEntry.EntryDate,
                    FiscalPeriodId = closingEntry.FiscalPeriodId,
                    JournalType = "Year-End Close Reversal",
                    FunctionalCurrencyCode = await _tenantSettings.GetBaseCurrencyAsync(),
                    IdempotencyKey = $"GL:YearEndCloseReversal:{tenantId:N}:{fiscalYear.Id:N}:{closingEntry.Id:N}",
                    AllowPostingToClosedPeriod = true,
                    Lines = closingEntry.Transactions
                        .Where(t => !t.IsDeleted)
                        .Select(t => new FinancePostingLineDto
                        {
                            AccountId = t.AccountId,
                            Description = $"Reversal: {t.Description}",
                            DebitAmount = t.CreditAmount,
                            CreditAmount = t.DebitAmount,
                            Notes = reason.Trim(),
                            TransactionTag = "YearEndCloseReversal"
                        })
                        .ToList()
                };

                await _financePostingEngine.PostAsync(reversalRequest);
            }

            fiscalYear.IsClosed = false;
            fiscalYear.IsActive = true;
            fiscalYear.Status = "Open";
            fiscalYear.ClosedDate = null;
            fiscalYear.ClosedByUserId = null;
            fiscalYear.RetainedEarningsTransferComplete = false;
            fiscalYear.RetainedEarningsTransferDate = null;
            fiscalYear.ClosingJournalEntryId = null;
            fiscalYear.NetIncomeTransferred = 0m;
            fiscalYear.YearEndClosingNotes = string.IsNullOrWhiteSpace(fiscalYear.YearEndClosingNotes)
                ? $"Reopened {DateTime.UtcNow:u}: {reason.Trim()}"
                : $"{fiscalYear.YearEndClosingNotes}\nReopened {DateTime.UtcNow:u}: {reason.Trim()}";

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return new PeriodCloseResultDto
            {
                Success = true,
                Message = $"Fiscal year '{fiscalYear.FiscalYearName}' reopened. The year-end closing entry was reversed.",
                FiscalPeriodId = fiscalYear.Id,
                PeriodName = fiscalYear.FiscalYearName
            };
        }

        public async Task<FinanceDashboardDto> GetFinanceDashboardAsync()
        {
            var tenantId = TenantId;
            var today = DateTime.UtcNow.Date;

            var fiscalYear = await _context.FiscalYears
                .Where(fy => fy.TenantId == tenantId && !fy.IsDeleted && fy.StartDate <= today && fy.EndDate >= today)
                .OrderByDescending(fy => fy.StartDate)
                .FirstOrDefaultAsync();

            var startDate = fiscalYear?.StartDate.Date ?? new DateTime(today.Year, 1, 1);
            var endDate = fiscalYear?.EndDate.Date ?? new DateTime(today.Year, 12, 31);

            var activity = await _context.AccountTransactions
                .Where(t => t.TenantId == tenantId
                    && !t.IsDeleted
                    && t.TransactionDate >= startDate
                    && t.TransactionDate <= endDate
                    && (t.Account.AccountType == AccountType.Revenue || t.Account.AccountType == AccountType.Expense))
                .Select(t => new
                {
                    t.Account.AccountType,
                    t.Account.AccountName,
                    t.TransactionDate,
                    t.DebitAmount,
                    t.CreditAmount
                })
                .ToListAsync();

            var revenue = activity
                .Where(t => t.AccountType == AccountType.Revenue)
                .Sum(t => t.CreditAmount - t.DebitAmount);
            var expenses = activity
                .Where(t => t.AccountType == AccountType.Expense)
                .Sum(t => t.DebitAmount - t.CreditAmount);

            var monthly = new List<FinanceDashboardMonthlyPointDto>();
            var monthCursor = new DateTime(startDate.Year, startDate.Month, 1);
            var lastMonth = new DateTime(endDate.Year, endDate.Month, 1);
            while (monthCursor <= lastMonth && monthly.Count < 12)
            {
                var monthEnd = monthCursor.AddMonths(1);
                var monthRows = activity.Where(t => t.TransactionDate >= monthCursor && t.TransactionDate < monthEnd).ToList();
                monthly.Add(new FinanceDashboardMonthlyPointDto
                {
                    Name = monthCursor.ToString("MMM"),
                    Revenue = monthRows.Where(t => t.AccountType == AccountType.Revenue).Sum(t => t.CreditAmount - t.DebitAmount),
                    Expenses = monthRows.Where(t => t.AccountType == AccountType.Expense).Sum(t => t.DebitAmount - t.CreditAmount)
                });
                monthCursor = monthEnd;
            }

            var expenseChart = activity
                .Where(t => t.AccountType == AccountType.Expense)
                .GroupBy(t => t.AccountName)
                .Select(g => new FinanceDashboardBreakdownPointDto
                {
                    Name = g.Key,
                    Value = g.Sum(t => t.DebitAmount - t.CreditAmount)
                })
                .Where(p => p.Value > 0)
                .OrderByDescending(p => p.Value)
                .Take(8)
                .ToList();

            // Cash on hand comes from the posted cash/bank ledger (source of truth), matching
            // the cash position report rather than stored snapshots.
            var cashLedger = await GenerateCashBankLedgerAsync(new CashBankLedgerRequestDto
            {
                StartDate = today,
                EndDate = today
            });

            return new FinanceDashboardDto
            {
                Kpis = new FinanceDashboardKpisDto
                {
                    Revenue = revenue,
                    Expenses = expenses,
                    NetProfit = revenue - expenses,
                    CashOnHand = cashLedger.TotalClosingBalance
                },
                Monthly = monthly,
                ExpenseChart = expenseChart
            };
        }

        private async Task<(Guid? ClosingJournalEntryId, decimal NetIncome)> TransferRetainedEarningsAsync(
            FiscalYear fiscalYear,
            Guid retainedEarningsAccountId)
        {
            var tenantId = TenantId;

            var retainedEarningsAccountExists = await _context.Accounts
                .AnyAsync(a => a.TenantId == tenantId && a.Id == retainedEarningsAccountId && !a.IsDeleted);
            if (!retainedEarningsAccountExists)
                throw new ArgumentException($"Retained earnings account {retainedEarningsAccountId} not found");

            // Get all revenue and expense accounts with balances for the year
            var periodIds = fiscalYear.FiscalPeriods.Select(p => p.Id).ToList();

            var revenueExpenseTransactions = await _context.AccountTransactions
                .Include(t => t.Account)
                .Where(t => t.TenantId == tenantId
                    && periodIds.Contains(t.FiscalPeriodId)
                    && !t.IsDeleted
                    && (t.PostingStatus == "Posted" || t.PostingStatus == "Reversed")
                    && (t.Account.AccountType == AccountType.Revenue || t.Account.AccountType == AccountType.Expense))
                .ToListAsync();

            // Calculate balances by account
            var accountBalances = revenueExpenseTransactions
                .GroupBy(t => t.AccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Account = g.First().Account,
                    Balance = g.Sum(t => t.CreditAmount - t.DebitAmount) // Revenue positive, Expense negative
                })
                .Where(b => Math.Abs(b.Balance) > 0.01m)
                .ToList();

            decimal netIncome = accountBalances.Sum(b => b.Balance);

            // A year with no revenue/expense activity closes without a closing journal.
            if (accountBalances.Count == 0)
            {
                return (null, 0m);
            }

            var lastPeriod = fiscalYear.FiscalPeriods.OrderByDescending(p => p.EndDate).First();

            // Zero each account against its actual net balance rather than by account type so
            // contra balances (e.g. negative revenue) never produce negative posting amounts.
            var lines = new List<FinancePostingLineDto>();
            foreach (var acctBalance in accountBalances)
            {
                lines.Add(new FinancePostingLineDto
                {
                    AccountId = acctBalance.AccountId,
                    Description = acctBalance.Account.AccountType == AccountType.Revenue
                        ? "Year-end close - Revenue account"
                        : "Year-end close - Expense account",
                    DebitAmount = acctBalance.Balance > 0 ? acctBalance.Balance : 0m,
                    CreditAmount = acctBalance.Balance < 0 ? Math.Abs(acctBalance.Balance) : 0m,
                    TransactionTag = "YearEndClose"
                });
            }

            lines.Add(new FinancePostingLineDto
            {
                AccountId = retainedEarningsAccountId,
                Description = $"Year-end close - Net Income transfer: {netIncome:N2}",
                DebitAmount = netIncome < 0 ? Math.Abs(netIncome) : 0m, // Net loss = debit
                CreditAmount = netIncome > 0 ? netIncome : 0m, // Net income = credit
                TransactionTag = "YearEndClose"
            });

            // Post through the finance posting engine so the closing entry gets a posting
            // event, idempotency protection, and correct Account.Balance snapshot movements.
            var postingRequest = new FinancePostingRequestV2Dto
            {
                SourceModule = "GL",
                SourceDocumentType = "YearEndClose",
                SourceDocumentId = fiscalYear.Id,
                SourceDocumentTenantId = tenantId,
                SourceDocumentReference = fiscalYear.FiscalYearCode,
                Description = $"Year-end close - Transfer to Retained Earnings for {fiscalYear.FiscalYearName}",
                PostingDate = fiscalYear.EndDate.Date,
                FiscalPeriodId = lastPeriod.Id,
                JournalType = "Year-End Close",
                FunctionalCurrencyCode = await _tenantSettings.GetBaseCurrencyAsync(),
                IdempotencyKey = $"GL:YearEndClose:{tenantId:N}:{fiscalYear.Id:N}",
                AllowPostingToClosedPeriod = true,
                Lines = lines
            };

            var postingResult = await _financePostingEngine.PostAsync(postingRequest);

            return (postingResult.JournalEntryId, netIncome);
        }

        #endregion
    }


}
