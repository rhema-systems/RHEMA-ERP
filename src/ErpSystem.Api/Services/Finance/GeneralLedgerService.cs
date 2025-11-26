using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using ErpSystem.Core.Enums;

namespace ErpSystem.Api.Services
{
    public class GeneralLedgerService : IGeneralLedgerService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantSettingsService _tenantSettings;

        public GeneralLedgerService(
            ApplicationDbContext context, 
            ICurrentUserService currentUserService,
            ITenantSettingsService tenantSettings)
        {
            _context = context;
            _currentUserService = currentUserService;
            _tenantSettings = tenantSettings;
        }

        #region Account Management

        public async Task<Account> CreateSegmentedAccountAsync(AccountCreateDto accountDto)
        {
            // 1. Validate Account Structure
            await ValidateAccountStructureAsync(accountDto.AccountNumber);

            // 2. Map DTO to Entity
            var account = new Account
            {
                Id = Guid.NewGuid(),
                AccountCode = accountDto.AccountCode ?? accountDto.AccountNumber, // Fallback to AccountNumber if Code not provided
                AccountNumber = accountDto.AccountNumber,
                AccountName = accountDto.AccountName,
                AccountType = Enum.Parse<AccountType>(accountDto.AccountType),
                CurrencyCode = accountDto.CurrencyCode,
                IsMultiCurrency = accountDto.IsMultiCurrency,
                IsSegmented = true,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = false,
                Status = AccountStatus.Active,
                TenantId = _currentUserService.TenantId ?? Guid.Empty // Ensure TenantId is set
            };

            // 3. Save to Database
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            return account;
        }

        public async Task<bool> ValidateAccountStructureAsync(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new ArgumentException("Account number cannot be empty.");

            var tenantId = _currentUserService.TenantId;
            if (tenantId == null) throw new InvalidOperationException("Tenant context is required for validation.");

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

        public async Task<Account?> GetAccountByIdAsync(Guid accountId)
        {
            return await _context.Accounts
                .Include(a => a.SegmentValues)
                .Include(a => a.CurrencyLinks)
                .FirstOrDefaultAsync(a => a.Id == accountId);
        }

        public async Task<Account?> GetAccountByCodeAsync(string accountCode)
        {
            return await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountCode == accountCode);
        }

        public async Task<IEnumerable<Account>> GetAllAccountsAsync()
        {
            return await _context.Accounts.ToListAsync();
        }

        #endregion

        #region Transaction Processing

        public async Task<JournalEntry> PostJournalEntryAsync(CreateJournalEntryDto entryDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Validate Debits = Credits
                var totalDebit = entryDto.Transactions.Where(t => t.TransactionType == "Debit").Sum(t => t.Amount);
                var totalCredit = entryDto.Transactions.Where(t => t.TransactionType == "Credit").Sum(t => t.Amount);

                if (totalDebit != totalCredit)
                {
                    throw new InvalidOperationException("Journal Entry must balance (Debits != Credits).");
                }

                // 2. Get Open Fiscal Period
                var fiscalPeriod = await _context.FiscalPeriods
                    .FirstOrDefaultAsync(p => p.StartDate <= entryDto.TransactionDate && p.EndDate >= entryDto.TransactionDate);
                
                if (fiscalPeriod == null)
                {
                    // For MVP/Testing, create a default period if none exists
                    // In production, this should throw an error
                    var fiscalYear = await _context.FiscalYears.FirstOrDefaultAsync() ?? new FiscalYear 
                    { 
                        Id = Guid.NewGuid(), 
                        FiscalYearCode = entryDto.TransactionDate.Year.ToString(),
                        StartDate = new DateTime(entryDto.TransactionDate.Year, 1, 1),
                        EndDate = new DateTime(entryDto.TransactionDate.Year, 12, 31),
                        IsActive = true
                    };
                    
                    if (_context.Entry(fiscalYear).State == EntityState.Detached) _context.FiscalYears.Add(fiscalYear);

                    fiscalPeriod = new FiscalPeriod
                    {
                        Id = Guid.NewGuid(),
                        FiscalYearId = fiscalYear.Id,
                        PeriodName = $"{entryDto.TransactionDate:MMMM yyyy}",
                        PeriodCode = $"{entryDto.TransactionDate:yyyy-MM}",
                        PeriodNumber = entryDto.TransactionDate.Month,
                        StartDate = new DateTime(entryDto.TransactionDate.Year, entryDto.TransactionDate.Month, 1),
                        EndDate = new DateTime(entryDto.TransactionDate.Year, entryDto.TransactionDate.Month, DateTime.DaysInMonth(entryDto.TransactionDate.Year, entryDto.TransactionDate.Month)),
                        PeriodStatus = "Open",
                        IsOpen = true
                    };
                    _context.FiscalPeriods.Add(fiscalPeriod);
                    await _context.SaveChangesAsync();
                }

                // 3. Create Journal Entry Header
                var journalEntry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    JournalEntryNumber = entryDto.JournalNumber,
                    EntryDate = entryDto.TransactionDate,
                    Description = entryDto.Description ?? string.Empty,
                    ReferenceNumber = entryDto.Reference,
                    TotalDebitAmount = totalDebit,
                    TotalCreditAmount = totalCredit,
                    PostingStatus = "Posted",
                    PostingDate = DateTime.UtcNow,
                    FiscalPeriodId = fiscalPeriod.Id,
                    IsBalanced = true,
                    BookClassification = "IFRS"
                };

                _context.JournalEntries.Add(journalEntry);

                // 4. Process Lines and Update Balances
                int lineNum = 1;
                foreach (var lineDto in entryDto.Transactions)
                {
                    var account = await _context.Accounts.FindAsync(lineDto.AccountId);
                    if (account == null) throw new InvalidOperationException($"Account not found: {lineDto.AccountId}");

                    decimal debitAmount = 0;
                    decimal creditAmount = 0;

                    // Update Account Balance
                    if (lineDto.TransactionType == "Debit")
                    {
                        debitAmount = lineDto.Amount;
                        if (account.AccountType == AccountType.Asset || account.AccountType == AccountType.Expense)
                            account.Balance += lineDto.Amount;
                        else
                            account.Balance -= lineDto.Amount;
                    }
                    else // Credit
                    {
                        creditAmount = lineDto.Amount;
                        if (account.AccountType == AccountType.Liability || account.AccountType == AccountType.Equity || account.AccountType == AccountType.Revenue)
                            account.Balance += lineDto.Amount;
                        else
                            account.Balance -= lineDto.Amount;
                    }

                    // Create Transaction Record
                    var transactionRecord = new AccountTransaction
                    {
                        Id = Guid.NewGuid(),
                        AccountId = account.Id,
                        JournalEntryId = journalEntry.Id,
                        DebitAmount = debitAmount,
                        CreditAmount = creditAmount,
                        TransactionDate = entryDto.TransactionDate,
                        Description = lineDto.Description,
                        SourceReferenceNumber = lineDto.Reference,
                        FiscalPeriodId = fiscalPeriod.Id,
                        LineNumber = lineNum++,
                        BookClassification = "IFRS"
                    };

                    _context.AccountTransactions.Add(transactionRecord);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return journalEntry;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<decimal> GetAccountBalanceAsync(Guid accountId, string currencyCode = "GHS")
        {
            var account = await _context.Accounts.FindAsync(accountId);
            if (account == null) throw new ArgumentException("Account not found");

            // TODO: Implement multi-currency balance calculation using AccountCurrencyLink and ExchangeRates
            return account.Balance;
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
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null) throw new InvalidOperationException("Tenant context is required.");

            // 1. Get Accounts to Revalue
            var query = _context.Accounts
                .Where(a => a.TenantId == tenantId && a.IsActive && !a.IsDeleted);

            if (!string.IsNullOrEmpty(request.CurrencyCode))
            {
                // Revalue specific currency accounts OR multi-currency accounts OR accounts with currency links
                query = query.Where(a => 
                    a.CurrencyCode == request.CurrencyCode 
                    || a.IsMultiCurrency
                    || a.CurrencyLinks.Any(c => c.LinkedCurrencyCode == request.CurrencyCode && c.IsActive));
            }
            else
            {
                // Revalue all foreign currency accounts + multi-currency accounts + accounts with active currency links
                // Note: Ideally we filter out base currency accounts if they are not multi-currency
                query = query.Where(a => 
                    a.IsMultiCurrency 
                    || a.CurrencyCode != "GHS" 
                    || a.CurrencyLinks.Any(c => c.IsActive)); // Include accounts with any active currency links
            }

            var accounts = await query.ToListAsync();
            var revaluationDate = request.RevaluationDate.Date;

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
                IsBalanced = true, // Will be balanced by Gain/Loss account
                BookClassification = "IFRS",
                TenantId = tenantId.Value,
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
                    .Where(t => t.TransactionCurrency != null && t.TransactionCurrency != "GHS") // Exclude base currency txns
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
                        .Where(r => r.TargetCurrencyCode == currency && r.BaseCurrencyCode == "GHS" && r.EffectiveDate <= revaluationDate)
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
                        TenantId = tenantId.Value
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
                TenantId = tenantId.Value
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
            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date && p.TenantId == _currentUserService.TenantId);
            
            if (period != null) return period.Id;

            // Fallback: Create or throw? For MVP, let's throw if not found, or use the logic from PostJournalEntryAsync
            // Reusing logic from PostJournalEntryAsync would be better but it's embedded there.
            // For now, throw to enforce period setup.
            throw new InvalidOperationException($"No open fiscal period found for date {date:d}");
        }

        #endregion

        #region Financial Statements

        public async Task<BalanceSheetDto> GenerateBalanceSheetAsync(BalanceSheetRequestDto request)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null) throw new InvalidOperationException("Tenant context is required.");

            // 1. Get all accounts with balances as of the specified date
            var accounts = await _context.Accounts
                .Where(a => a.TenantId == tenantId && !a.IsDeleted)
                .ToListAsync();

            // Filter by book classification
            var lineItemProperty = request.BookClassification switch
            {
                "IFRS" => "IFRSLineItem",
                "Base" => "BaseLineItem",
                "Local" => "LocalLineItem",
                _ => "IFRSLineItem"
            };

            // 2. Calculate balances as of the specified date
            var accountBalances = new Dictionary<Guid, decimal>();
            foreach (var account in accounts)
            {
                var balance = await CalculateAccountBalanceAsOf(account.Id, request.AsAtDate);
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
                BookClassification = request.BookClassification,
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
                request.BookClassification,
                request.IncludeAccountDetails);

            // 6. Build Liabilities section
            var liabilitiesSection = BuildBalanceSheetSection(
                "Liabilities", 
                2, 
                liabilityAccounts, 
                accountBalances, 
                request.BookClassification,
                request.IncludeAccountDetails);

            // 7. Build Equity section
            var equitySection = BuildBalanceSheetSection(
                "Equity", 
                3, 
                equityAccounts, 
                accountBalances, 
                request.BookClassification,
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

            return balanceSheet;
        }

        private BalanceSheetSectionDto BuildBalanceSheetSection(
            string sectionName,
            int sectionOrder,
            List<Account> accounts,
            Dictionary<Guid, decimal> accountBalances,
            string bookClassification,
            bool includeAccountDetails)
        {
            var section = new BalanceSheetSectionDto
            {
                SectionName = sectionName,
                SectionOrder = sectionOrder
            };

            // Group by category
            var categories = accounts
                .GroupBy(a => a.AccountCategory ?? "Other")
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
                    .GroupBy(a => GetLineItem(a, bookClassification) ?? "Unclassified")
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
            return bookClassification switch
            {
                "IFRS" => account.IFRSLineItem,
                "Base" => account.BaseLineItem,
                "Local" => account.LocalLineItem,
                _ => account.IFRSLineItem
            };
        }

        private async Task<decimal> CalculateAccountBalanceAsOf(Guid accountId, DateTime asAtDate)
        {
            var transactions = await _context.AccountTransactions
                .Where(t => t.AccountId == accountId && t.TransactionDate <= asAtDate && !t.IsDeleted)
                .ToListAsync();

            return transactions.Sum(t => t.DebitAmount - t.CreditAmount);
        }

        public async Task<IncomeStatementDto> GenerateIncomeStatementAsync(IncomeStatementRequestDto request)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null) throw new InvalidOperationException("Tenant context is required.");

            // 1. Get all Revenue and Expense accounts
            var accounts = await _context.Accounts
                .Where(a => a.TenantId == tenantId && !a.IsDeleted 
                    && (a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense))
                .ToListAsync();

            // 2. Calculate activity for each account during the period
            var accountActivity = new Dictionary<Guid, decimal>();
            foreach (var account in accounts)
            {
                var activity = await CalculateAccountActivityForPeriod(account.Id, request.PeriodStart, request.PeriodEnd);
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
                BookClassification = request.BookClassification,
                CurrencyCode = await _tenantSettings.GetBaseCurrencyAsync()
            };

            // 4. Separate Revenue and Expense accounts
            var revenueAccounts = accounts.Where(a => a.AccountType == AccountType.Revenue && accountActivity.ContainsKey(a.Id)).ToList();
            var expenseAccounts = accounts.Where(a => a.AccountType == AccountType.Expense && accountActivity.ContainsKey(a.Id)).ToList();

            // 5. Build Revenue section
            var revenueSection = BuildIncomeStatementSection(
                "Revenue",
                1,
                revenueAccounts,
                accountActivity,
                request.BookClassification,
                request.IncludeAccountDetails);

            // 6. Build Expense sections (categorized)
            var costOfSalesAccounts = expenseAccounts.Where(a => (a.AccountCategory ?? "").Contains("Cost of Sales", StringComparison.OrdinalIgnoreCase)).ToList();
            var operatingExpenseAccounts = expenseAccounts.Where(a => !(a.AccountCategory ?? "").Contains("Cost of Sales", StringComparison.OrdinalIgnoreCase) 
                && !(a.AccountCategory ?? "").Contains("Tax", StringComparison.OrdinalIgnoreCase)).ToList();
            var taxExpenseAccounts = expenseAccounts.Where(a => (a.AccountCategory ?? "").Contains("Tax", StringComparison.OrdinalIgnoreCase)).ToList();

            var costOfSalesSection = BuildIncomeStatementSection(
                "Cost of Sales",
                2,
                costOfSalesAccounts,
                accountActivity,
                request.BookClassification,
                request.IncludeAccountDetails);

            var operatingExpensesSection = BuildIncomeStatementSection(
                "Operating Expenses",
                3,
                operatingExpenseAccounts,
                accountActivity,
                request.BookClassification,
                request.IncludeAccountDetails);

            var taxExpenseSection = BuildIncomeStatementSection(
                "Tax Expense",
                4,
                taxExpenseAccounts,
                accountActivity,
                request.BookClassification,
                request.IncludeAccountDetails);

            incomeStatement.Sections = new List<IncomeStatementSectionDto>
            {
                revenueSection,
                costOfSalesSection,
                operatingExpensesSection,
                taxExpenseSection
            };

            // 7. Calculate totals and profit metrics
            // Revenue is credit-normal, so positive activity means credit > debit
            incomeStatement.TotalRevenue = revenueSection.SectionTotal;
            incomeStatement.TotalCostOfSales = Math.Abs(costOfSalesSection.SectionTotal);
            incomeStatement.GrossProfit = incomeStatement.TotalRevenue - incomeStatement.TotalCostOfSales;
            incomeStatement.TotalOperatingExpenses = Math.Abs(operatingExpensesSection.SectionTotal);
            incomeStatement.OperatingProfit = incomeStatement.GrossProfit - incomeStatement.TotalOperatingExpenses;
            incomeStatement.TaxExpense = Math.Abs(taxExpenseSection.SectionTotal);
            incomeStatement.ProfitBeforeTax = incomeStatement.OperatingProfit;
            incomeStatement.NetProfit = incomeStatement.ProfitBeforeTax - incomeStatement.TaxExpense;

            return incomeStatement;
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

        private async Task<decimal> CalculateAccountActivityForPeriod(Guid accountId, DateTime periodStart, DateTime periodEnd)
        {
            var transactions = await _context.AccountTransactions
                .Where(t => t.AccountId == accountId 
                    && t.TransactionDate >= periodStart 
                    && t.TransactionDate <= periodEnd 
                    && !t.IsDeleted)
                .ToListAsync();

            // For Revenue (credit-normal): Credit - Debit gives positive revenue
            // For Expense (debit-normal): Debit - Credit gives positive expense
            // We return Credit - Debit, so Revenue is positive, Expense is negative
            return transactions.Sum(t => t.CreditAmount - t.DebitAmount);
        }

        public async Task<TrialBalanceDto> GenerateTrialBalanceAsync(TrialBalanceRequestDto request)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null) throw new InvalidOperationException("Tenant context is required.");

            // 1. Get all accounts for the tenant
            var accounts = await _context.Accounts
                .Where(a => a.TenantId == tenantId && !a.IsDeleted)
                .OrderBy(a => a.AccountNumber)
                .ToListAsync();

            // 2. Build Trial Balance
            var trialBalance = new TrialBalanceDto
            {
                CompanyName = await _tenantSettings.GetCompanyNameAsync(),
                AsAtDate = request.AsAtDate,
                CurrencyCode = await _tenantSettings.GetBaseCurrencyAsync()
            };

            var lines = new List<TrialBalanceLineDto>();

            // 3. Calculate balance for each account
            foreach (var account in accounts)
            {
                var balance = await CalculateAccountBalanceAsOf(account.Id, request.AsAtDate);

                // Skip zero balances if requested
                if (!request.IncludeZeroBalances && balance == 0)
                    continue;

                var line = new TrialBalanceLineDto
                {
                    AccountCode = account.AccountCode,
                    AccountNumber = account.AccountNumber,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType.ToString()
                };

                // Determine debit or credit balance based on account type and balance
                // Assets, Expenses: Debit-normal (positive balance = debit)
                // Liabilities, Equity, Revenue: Credit-normal (positive balance = credit)
                bool isDebitNormal = account.AccountType == AccountType.Asset || account.AccountType == AccountType.Expense;

                if (balance > 0)
                {
                    if (isDebitNormal)
                    {
                        line.DebitBalance = balance;
                        line.CreditBalance = 0;
                    }
                    else
                    {
                        line.DebitBalance = 0;
                        line.CreditBalance = balance;
                    }
                }
                else if (balance < 0)
                {
                    // Negative balance - reverse the normal side
                    if (isDebitNormal)
                    {
                        line.DebitBalance = 0;
                        line.CreditBalance = Math.Abs(balance);
                    }
                    else
                    {
                        line.DebitBalance = Math.Abs(balance);
                        line.CreditBalance = 0;
                    }
                }
                else
                {
                    line.DebitBalance = 0;
                    line.CreditBalance = 0;
                }

                lines.Add(line);
            }

            trialBalance.Lines = lines;

            // 4. Calculate totals
            trialBalance.TotalDebits = lines.Sum(l => l.DebitBalance);
            trialBalance.TotalCredits = lines.Sum(l => l.CreditBalance);

            return trialBalance;
        }

        public async Task<CashFlowStatementDto> GenerateCashFlowStatementAsync(CashFlowStatementRequestDto request)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null) throw new InvalidOperationException("Tenant context is required.");

            var cashFlowStatement = new CashFlowStatementDto
            {
                CompanyName = await _tenantSettings.GetCompanyNameAsync(),
                PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd,
                BookClassification = request.BookClassification,
                CurrencyCode = await _tenantSettings.GetBaseCurrencyAsync()
            };

            // Get all accounts with cash flow classifications
            var accounts = await _context.Accounts
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.CashFlowClassification != null)
                .ToListAsync();

            // Calculate activity for each account during the period
            var accountActivity = new Dictionary<Guid, decimal>();
            foreach (var account in accounts)
            {
                var activity = await CalculateAccountActivityForPeriod(account.Id, request.PeriodStart, request.PeriodEnd);
                if (Math.Abs(activity) > 0.01m)
                {
                    accountActivity[account.Id] = activity;
                }
            }

            // Separate accounts by cash flow classification
            var operatingAccounts = accounts.Where(a => a.CashFlowClassification == "Operating" && accountActivity.ContainsKey(a.Id)).ToList();
            var investingAccounts = accounts.Where(a => a.CashFlowClassification == "Investing" && accountActivity.ContainsKey(a.Id)).ToList();
            var financingAccounts = accounts.Where(a => a.CashFlowClassification == "Financing" && accountActivity.ContainsKey(a.Id)).ToList();

            // Build Operating Activities section
            cashFlowStatement.OperatingActivities = BuildCashFlowSection(
                "Cash Flows from Operating Activities",
                1,
                operatingAccounts,
                accountActivity,
                request.BookClassification,
                request.IncludeAccountDetails);

            // Build Investing Activities section
            cashFlowStatement.InvestingActivities = BuildCashFlowSection(
                "Cash Flows from Investing Activities",
                2,
                investingAccounts,
                accountActivity,
                request.BookClassification,
                request.IncludeAccountDetails);

            // Build Financing Activities section
            cashFlowStatement.FinancingActivities = BuildCashFlowSection(
                "Cash Flows from Financing Activities",
                3,
                financingAccounts,
                accountActivity,
                request.BookClassification,
                request.IncludeAccountDetails);

            // Calculate totals
            cashFlowStatement.NetCashFromOperating = cashFlowStatement.OperatingActivities.SectionTotal;
            cashFlowStatement.NetCashFromInvesting = cashFlowStatement.InvestingActivities.SectionTotal;
            cashFlowStatement.NetCashFromFinancing = cashFlowStatement.FinancingActivities.SectionTotal;
            cashFlowStatement.NetIncreaseInCash = cashFlowStatement.NetCashFromOperating + 
                                                   cashFlowStatement.NetCashFromInvesting + 
                                                   cashFlowStatement.NetCashFromFinancing;

            // Get cash balances
            var cashAccounts = await _context.Accounts
                .Where(a => a.TenantId == tenantId 
                    && !a.IsDeleted 
                    && (a.AccountCategory == "Cash" || a.AccountName.Contains("Cash", StringComparison.OrdinalIgnoreCase)))
                .ToListAsync();

            decimal cashAtBeginning = 0;
            decimal cashAtEnd = 0;

            foreach (var cashAccount in cashAccounts)
            {
                cashAtBeginning += await CalculateAccountBalanceAsOf(cashAccount.Id, request.PeriodStart.AddDays(-1));
                cashAtEnd += await CalculateAccountBalanceAsOf(cashAccount.Id, request.PeriodEnd);
            }

            cashFlowStatement.CashAtBeginning = cashAtBeginning;
            cashFlowStatement.CashAtEnd = cashAtEnd;

            return cashFlowStatement;
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

        #endregion

        #region Period-End Close

        public async Task<PeriodCloseValidationDto> ValidatePeriodCloseAsync(Guid fiscalPeriodId)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null) throw new InvalidOperationException("Tenant context is required.");

            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p => p.Id == fiscalPeriodId && p.TenantId == tenantId);

            if (period == null)
                throw new ArgumentException($"Fiscal period {fiscalPeriodId} not found");

            var validation = new PeriodCloseValidationDto
            {
                PeriodName = period.PeriodName,
                StartDate = period.StartDate,
                EndDate = period.EndDate
            };

            // Check if already closed
            if (period.IsClosed)
            {
                validation.ValidationErrors.Add("Period is already closed");
                validation.CanClose = false;
                return validation;
            }

            // Check if locked
            if (period.IsLocked)
            {
                validation.ValidationErrors.Add("Period is locked and cannot be closed again");
                validation.CanClose = false;
                return validation;
            }

            // Validate Trial Balance
            var transactions = await _context.AccountTransactions
                .Where(t => t.FiscalPeriodId == fiscalPeriodId && !t.IsDeleted)
                .ToListAsync();

            validation.TotalDebits = transactions.Sum(t => t.DebitAmount);
            validation.TotalCredits = transactions.Sum(t => t.CreditAmount);
            validation.Difference = validation.TotalDebits - validation.TotalCredits;
            validation.TotalJournalEntries = await _context.JournalEntries
                .CountAsync(j => j.FiscalPeriodId == fiscalPeriodId && !j.IsDeleted);
            validation.TotalTransactionLines = transactions.Count;

            if (!validation.IsBalanced)
            {
                validation.ValidationErrors.Add($"Trial Balance is out of balance by {validation.Difference:N2}. Debits must equal Credits.");
                validation.CanClose = false;
                return validation;
            }

            // Warnings (not blockers)
            if (!period.CurrencyRevaluationComplete)
            {
                validation.ValidationWarnings.Add("Currency revaluation has not been marked as complete");
            }

            if (!period.BankReconciliationComplete)
            {
                validation.ValidationWarnings.Add("Bank reconciliation has not been marked as complete");
            }

            if (!period.DepreciationComplete)
            {
                validation.ValidationWarnings.Add("Depreciation has not been marked as complete");
            }

            validation.CanClose = validation.ValidationErrors.Count == 0;
            return validation;
        }

        public async Task<PeriodCloseResultDto> CloseFiscalPeriodAsync(PeriodCloseRequestDto request)
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            if (tenantId == null || userId == null) 
                throw new InvalidOperationException("User context is required.");

            // Run validation unless skipped
            if (!request.SkipValidation)
            {
                var validation = await ValidatePeriodCloseAsync(request.FiscalPeriodId);
                if (!validation.CanClose)
                {
                    return new PeriodCloseResultDto
                    {
                        Success = false,
                        Message = "Period close validation failed",
                        FiscalPeriodId = request.FiscalPeriodId,
                        Errors = validation.ValidationErrors
                    };
                }
            }

            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p => p.Id == request.FiscalPeriodId && p.TenantId == tenantId);

            if (period == null)
                throw new ArgumentException($"Fiscal period {request.FiscalPeriodId} not found");

            // Calculate and cache statistics
            var transactions = await _context.AccountTransactions
                .Where(t => t.FiscalPeriodId == request.FiscalPeriodId && !t.IsDeleted)
                .ToListAsync();

            period.TotalDebits = transactions.Sum(t => t.DebitAmount);
            period.TotalCredits = transactions.Sum(t => t.CreditAmount);
            period.BalanceDifference = period.TotalDebits - period.TotalCredits;
            period.TotalTransactionLines = transactions.Count;
            period.TotalJournalEntries = await _context.JournalEntries
                .CountAsync(j => j.FiscalPeriodId == request.FiscalPeriodId && !j.IsDeleted);

            // Update period status
            period.IsClosed = true;
            period.IsOpen = false;
            period.PeriodStatus = "Closed";
            period.ClosedDate = DateTime.UtcNow;
            period.ClosedByUserId = Guid.Parse(userId);
            period.TrialBalanceValidated = true;
            period.TrialBalanceValidatedDate = DateTime.UtcNow;
            period.ClosingNotes = request.ClosingNotes;

            await _context.SaveChangesAsync();

            return new PeriodCloseResultDto
            {
                Success = true,
                Message = $"Period '{period.PeriodName}' closed successfully",
                FiscalPeriodId = period.Id,
                PeriodName = period.PeriodName,
                ClosedDate = period.ClosedDate
            };
        }

        public async Task<PeriodCloseResultDto> ReopenFiscalPeriodAsync(PeriodReopenRequestDto request)
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            if (tenantId == null || userId == null) 
                throw new InvalidOperationException("User context is required.");

            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p => p.Id == request.FiscalPeriodId && p.TenantId == tenantId);

            if (period == null)
                throw new ArgumentException($"Fiscal period {request.FiscalPeriodId} not found");

            // Check if locked
            if (period.IsLocked)
            {
                return new PeriodCloseResultDto
                {
                    Success = false,
                    Message = "Cannot reopen a locked period. Please unlock first.",
                    FiscalPeriodId = period.Id,
                    PeriodName = period.PeriodName,
                    Errors = new List<string> { "Period is locked" }
                };
            }

            // Check if already open
            if (!period.IsClosed)
            {
                return new PeriodCloseResultDto
                {
                    Success = false,
                    Message = "Period is already open",
                    FiscalPeriodId = period.Id,
                    PeriodName = period.PeriodName
                };
            }

            // Reopen the period
            period.IsClosed = false;
            period.IsOpen = true;
            period.PeriodStatus = "Open";
            period.HasBeenReopened = true;
            period.ReopenCount += 1;
            period.LastReopenedDate = DateTime.UtcNow;
            period.LastReopenedByUserId = Guid.Parse(userId);
            period.ReopenReason = request.Reason;

            await _context.SaveChangesAsync();

            return new PeriodCloseResultDto
            {
                Success = true,
                Message = $"Period '{period.PeriodName}' reopened successfully. Reopen count: {period.ReopenCount}",
                FiscalPeriodId = period.Id,
                PeriodName = period.PeriodName
            };
        }

        public async Task LockFiscalPeriodAsync(Guid fiscalPeriodId, string lockReason)
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            if (tenantId == null || userId == null) 
                throw new InvalidOperationException("User context is required.");

            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p => p.Id == fiscalPeriodId && p.TenantId == tenantId);

            if (period == null)
                throw new ArgumentException($"Fiscal period {fiscalPeriodId} not found");

            if (!period.IsClosed)
                throw new InvalidOperationException("Period must be closed before it can be locked");

            period.IsLocked = true;
            period.LockedDate = DateTime.UtcNow;
            period.LockedByUserId = Guid.Parse(userId);
            period.LockReason = lockReason;

            await _context.SaveChangesAsync();
        }

        public async Task UnlockFiscalPeriodAsync(Guid fiscalPeriodId, string unlockReason)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null) throw new InvalidOperationException("Tenant context is required.");

            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p => p.Id == fiscalPeriodId && p.TenantId == tenantId);

            if (period == null)
                throw new ArgumentException($"Fiscal period {fiscalPeriodId} not found");

            period.IsLocked = false;
            // Note: Keep audit trail of who locked and when, just unlock

            await _context.SaveChangesAsync();
        }

        public async Task<PeriodCloseResultDto> CloseFiscalYearAsync(YearEndCloseRequestDto request)
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            if (tenantId == null || userId == null) 
                throw new InvalidOperationException("User context is required.");

            var fiscalYear = await _context.FiscalYears
                .Include(fy => fy.FiscalPeriods)
                .FirstOrDefaultAsync(fy => fy.Id == request.FiscalYearId && fy.TenantId == tenantId);

            if (fiscalYear == null)
                throw new ArgumentException($"Fiscal year {request.FiscalYearId} not found");

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

            // Transfer retained earnings
            var closingEntry = await TransferRetainedEarningsAsync(request.FiscalYearId, request.RetainedEarningsAccountId);

            // Update fiscal year
            fiscalYear.IsClosed = true;
            fiscalYear.IsActive = false;
            fiscalYear.Status = "Closed";
            fiscalYear.ClosedDate = DateTime.UtcNow;
            fiscalYear.ClosedByUserId = Guid.Parse(userId);
            fiscalYear.RetainedEarningsTransferComplete = true;
            fiscalYear.RetainedEarningsTransferDate = DateTime.UtcNow;
            fiscalYear.ClosingJournalEntryId = closingEntry.Id;
            fiscalYear.NetIncomeTransferred = closingEntry.Transactions.Sum(t => t.CreditAmount - t.DebitAmount);
            fiscalYear.YearEndClosingNotes = request.ClosingNotes;

            await _context.SaveChangesAsync();

            return new PeriodCloseResultDto
            {
                Success = true,
                Message = $"Fiscal year '{fiscalYear.FiscalYearName}' closed successfully. Net income transferred: {fiscalYear.NetIncomeTransferred:N2}",
                FiscalPeriodId = fiscalYear.Id,
                PeriodName = fiscalYear.FiscalYearName,
                ClosedDate = fiscalYear.ClosedDate
            };
        }

        private async Task<JournalEntry> TransferRetainedEarningsAsync(Guid fiscalYearId, Guid retainedEarningsAccountId)
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            if (tenantId == null || userId == null) 
                throw new InvalidOperationException("User context is required.");

            var fiscalYear = await _context.FiscalYears
                .Include(fy => fy.FiscalPeriods)
                .FirstOrDefaultAsync(fy => fy.Id == fiscalYearId);

            if (fiscalYear == null)
                throw new ArgumentException($"Fiscal year {fiscalYearId} not found");

            // Get all revenue and expense accounts with balances for the year
            var periodIds = fiscalYear.FiscalPeriods.Select(p => p.Id).ToList();
            
            var revenueExpenseTransactions = await _context.AccountTransactions
                .Include(t => t.Account)
                .Where(t => periodIds.Contains(t.FiscalPeriodId) 
                    && !t.IsDeleted
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

            // Get last period of fiscal year for posting
            var lastPeriod = fiscalYear.FiscalPeriods.OrderByDescending(p => p.EndDate).First();

            // Create closing journal entry
            var journalEntry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                JournalEntryNumber = $"YE-CLOSE-{fiscalYear.FiscalYearCode}",
                Description = $"Year-end close - Transfer to Retained Earnings for {fiscalYear.FiscalYearName}",
                EntryDate = fiscalYear.EndDate,
                PostingDate = DateTime.UtcNow,
                FiscalPeriodId = lastPeriod.Id,
                PostingStatus = "Posted"
            };

            var transactions = new List<AccountTransaction>();

            // Close revenue accounts (debit to zero them out)
            foreach (var acctBalance in accountBalances.Where(b => b.Account.AccountType == AccountType.Revenue))
            {
                transactions.Add(new AccountTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    JournalEntryId = journalEntry.Id,
                    AccountId = acctBalance.AccountId,
                    FiscalPeriodId = lastPeriod.Id,
                    TransactionDate = fiscalYear.EndDate,
                    Description = "Year-end close - Revenue account",
                    DebitAmount = acctBalance.Balance, // Debit to close
                    CreditAmount = 0
                });
            }

            // Close expense accounts (credit to zero them out)
            foreach (var acctBalance in accountBalances.Where(b => b.Account.AccountType == AccountType.Expense))
            {
                transactions.Add(new AccountTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    JournalEntryId = journalEntry.Id,
                    AccountId = acctBalance.AccountId,
                    FiscalPeriodId = lastPeriod.Id,
                    TransactionDate = fiscalYear.EndDate,
                    Description = "Year-end close - Expense account",
                    DebitAmount = 0,
                    CreditAmount = Math.Abs(acctBalance.Balance) // Credit to close (balance is negative)
                });
            }

            // Transfer to retained earnings
            transactions.Add(new AccountTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                JournalEntryId = journalEntry.Id,
                AccountId = retainedEarningsAccountId,
                FiscalPeriodId = lastPeriod.Id,
                TransactionDate = fiscalYear.EndDate,
                Description = $"Year-end close - Net Income transfer: {netIncome:N2}",
                DebitAmount = netIncome < 0 ? Math.Abs(netIncome) : 0, // Net loss = debit
                CreditAmount = netIncome > 0 ? netIncome : 0 // Net income = credit
            });

            journalEntry.Transactions = transactions;

            _context.JournalEntries.Add(journalEntry);
            _context.AccountTransactions.AddRange(transactions);
            await _context.SaveChangesAsync();

            return journalEntry;
        }

        #endregion
    }
}
