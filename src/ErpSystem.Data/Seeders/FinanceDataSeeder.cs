using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds initial finance data including currencies, exchange rates, fiscal years,
/// fiscal periods, finance settings, and a standard chart of accounts.
/// </summary>
public class FinanceDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FinanceDataSeeder> _logger;

    public FinanceDataSeeder(ApplicationDbContext context, ILogger<FinanceDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting finance data seeding...");

            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                _logger.LogWarning("Default tenant not found. Skipping finance data seeding.");
                return;
            }

            var tenantId = defaultTenant.Id;
            var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // 1. Seed Currencies
            await SeedCurrenciesAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 2. Seed Exchange Rates
            await SeedExchangeRatesAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 2.5 Seed Payment Terms
            await SeedPaymentTermsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 3. Seed Fiscal Years
            await SeedFiscalYearsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 4. Seed Fiscal Periods
            await SeedFiscalPeriodsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 5. Seed Account Segments
            await SeedAccountSegmentsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 6. Seed Standard Chart of Accounts
            await SeedAccountsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 6.1 Deliberately no-op: analytical dimensions must not be expanded into GL identities.
            await SeedFinanceDemoAccountCombinationsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 6.25 Seed unit-accounting demo drivers used by statistical ledger screens
            await SeedUnitAccountingDemoDataAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 6.5 Seed Payroll GL accounts used by HR payroll posting
            await SeedPayrollAccountsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // Stable codes, not environment-specific GUIDs, bind books, classifications and the
            // reviewed Finance demo chart. Unreviewed external accounts remain readiness blockers.
            await new FinanceClassificationManifestSeeder(_context, _logger).SeedAsync(tenantId, baseDate);
            await new FinanceBaselineProvisioningSeeder(_context, _logger).SeedAsync(tenantId, baseDate);
            // Protected statement standards require the canonical books to be active. Provision
            // them after baseline activation so a fresh seed receives the same layouts as a later
            // idempotent seed-finance-baseline pass.
            await new FinanceFinancialStatementStandardSeeder(_context, _logger).SeedAsync(tenantId, baseDate);

            // 7. Seed Fixed Asset Categories
            await SeedFixedAssetCategoriesAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // Fixed-asset masters and their monetary values are deliberately not baseline seed data.
            // They must enter through an explicit import/capitalization/opening workflow so every
            // accounting-book value has source, journal, and posting-event lineage.

            // 8. Seed Tax Configuration
            await SeedTaxConfigurationAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 9. Seed Finance Settings (must be after accounts for FK references)
            await SeedFinanceSettingsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 9.5 Seed operational liquidity masters only for this known standard/demo COA.
            // Custom tenant COAs are deliberately handled by the Banking setup wizard.
            await SeedFinanceDemoBankingMastersAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            await SeedBankingSettlementDefaultsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 9.75 Seed counterparties needed to execute Finance-owned AP and AR scenarios.
            // No invoices, receipts or payments are seeded: testers must create those records
            // through the controlled workflows so the resulting evidence is meaningful.
            await SeedFinanceDemoCounterpartiesAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 10. Seed Module Definitions
            await SeedModuleDefinitionsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 11. Seed Document Mappings
            await SeedTransactionDocumentMappingsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 12. Seed collection follow-up examples only when posted overdue AR
            // evidence already exists. A fresh database normally has no posted AR
            // exposure yet, so this remains a safe no-op until representative demo
            // transactions have been loaded and the settlement projection rebuilt.
            await SeedArCollectionFollowUpDemoAsync(tenantId);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Finance data seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding finance data");
            throw;
        }
    }

    private async Task SeedArCollectionFollowUpDemoAsync(Guid tenantId)
    {
        var assignee = await _context.Users.AsNoTracking()
            .Where(user => user.IsActive && user.TenantId == tenantId)
            .OrderBy(user => user.CreatedAt)
            .Select(user => new { user.Id, user.UserName })
            .FirstOrDefaultAsync();
        if (assignee == null)
            return;

        var candidates = await _context.SubledgerSettlementBalances.AsNoTracking()
            .Where(balance =>
                balance.TenantId == tenantId &&
                !balance.IsDeleted &&
                balance.SourceModule == SubledgerSettlementModules.AccountsReceivable &&
                balance.OutstandingAmount > 0 &&
                balance.DueDate.HasValue &&
                balance.DueDate.Value.Date < DateTime.UtcNow.Date)
            .OrderBy(balance => balance.DueDate)
            .Take(2)
            .ToListAsync();

        foreach (var exposure in candidates)
        {
            var alreadyExists = await _context.CollectionActivities.IgnoreQueryFilters().AnyAsync(activity =>
                activity.TenantId == tenantId &&
                !activity.IsDeleted &&
                activity.CollectionContext == CollectionActivityValues.FinanceArContext &&
                activity.IsPrimaryTask &&
                activity.InvoiceId == exposure.SourceDocumentId);
            if (alreadyExists)
                continue;

            var now = DateTime.UtcNow;
            var demoReference = $"ARCOL-DEMO-{exposure.SourceDocumentNumber}";
            _context.CollectionActivities.Add(new CollectionActivity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ReferenceNumber = demoReference[..Math.Min(50, demoReference.Length)],
                CollectionContext = CollectionActivityValues.FinanceArContext,
                IsPrimaryTask = true,
                BusinessPartnerId = exposure.CounterpartyId,
                InvoiceId = exposure.SourceDocumentId,
                Subject = $"Follow up overdue invoice {exposure.SourceDocumentNumber}",
                ActivityType = CollectionActivityValues.FollowUpTaskType,
                Description = "Seeded demonstration task backed by posted AR settlement evidence.",
                ActivityDate = now,
                FollowUpDate = now.Date.AddDays(1),
                CollectionStatus = CollectionActivityValues.PendingStatus,
                OutstandingAmount = exposure.OutstandingAmount,
                AssignedToId = assignee.Id,
                Priority = 5,
                Notes = "Use this task to demonstrate assignment, reminders, promises to pay, and collection history.",
                CreatedAt = now,
                CreatedBy = assignee.UserName ?? "System",
                CreatedById = assignee.Id
            });
        }
    }

    #region Currency Seeding

    private async Task SeedCurrenciesAsync(Guid tenantId, DateTime baseDate)
    {
        // GHS - Ghana Cedi (Base Currency)
        var ghsId = Guid.Parse("00000001-0001-0001-0001-000000000001");
        if (!await _context.Currencies.AnyAsync(c => c.CurrencyCode == "GHS" && c.TenantId == tenantId))
        {
            await _context.Currencies.AddAsync(new Currency
            {
                Id = ghsId,
                TenantId = tenantId,
                CurrencyCode = "GHS",
                NumericCode = "936",
                CurrencyName = "Ghana Cedi",
                CurrencySymbol = "₵",
                DecimalPlaces = 2,
                RoundingMethod = "Standard",
                RoundingPrecision = 0.01m,
                SymbolPosition = "Before",
                DecimalSeparator = ".",
                ThousandsSeparator = ",",
                CountryCode = "GH",
                CountryName = "Ghana",
                IsActive = true,
                IsBaseCurrency = true,
                HasTransactionHistory = true,
                CurrencyClassification = "Regional",
                GeographicRegion = "West Africa",
                MinorUnitName = "Pesewa",
                MinorUnitPluralName = "Pesewas",
                MinorUnitRatio = 100,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        // USD - US Dollar
        var usdId = Guid.Parse("00000001-0001-0001-0001-000000000002");
        if (!await _context.Currencies.AnyAsync(c => c.CurrencyCode == "USD" && c.TenantId == tenantId))
        {
            await _context.Currencies.AddAsync(new Currency
            {
                Id = usdId,
                TenantId = tenantId,
                CurrencyCode = "USD",
                NumericCode = "840",
                CurrencyName = "US Dollar",
                CurrencySymbol = "$",
                DecimalPlaces = 2,
                RoundingMethod = "Standard",
                RoundingPrecision = 0.01m,
                SymbolPosition = "Before",
                DecimalSeparator = ".",
                ThousandsSeparator = ",",
                CountryCode = "US",
                CountryName = "United States",
                IsActive = true,
                IsBaseCurrency = false,
                HasTransactionHistory = true,
                CurrencyClassification = "Major",
                GeographicRegion = "North America",
                MinorUnitName = "Cent",
                MinorUnitPluralName = "Cents",
                MinorUnitRatio = 100,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        // EUR - Euro
        var eurId = Guid.Parse("00000001-0001-0001-0001-000000000003");
        if (!await _context.Currencies.AnyAsync(c => c.CurrencyCode == "EUR" && c.TenantId == tenantId))
        {
            await _context.Currencies.AddAsync(new Currency
            {
                Id = eurId,
                TenantId = tenantId,
                CurrencyCode = "EUR",
                NumericCode = "978",
                CurrencyName = "Euro",
                CurrencySymbol = "€",
                DecimalPlaces = 2,
                RoundingMethod = "Standard",
                RoundingPrecision = 0.01m,
                SymbolPosition = "Before",
                DecimalSeparator = ".",
                ThousandsSeparator = ",",
                CountryCode = "EU",
                CountryName = "European Union",
                IsActive = true,
                IsBaseCurrency = false,
                HasTransactionHistory = false,
                CurrencyClassification = "Major",
                GeographicRegion = "Europe",
                MinorUnitName = "Cent",
                MinorUnitPluralName = "Cents",
                MinorUnitRatio = 100,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        // GBP - British Pound
        var gbpId = Guid.Parse("00000001-0001-0001-0001-000000000004");
        if (!await _context.Currencies.AnyAsync(c => c.CurrencyCode == "GBP" && c.TenantId == tenantId))
        {
            await _context.Currencies.AddAsync(new Currency
            {
                Id = gbpId,
                TenantId = tenantId,
                CurrencyCode = "GBP",
                NumericCode = "826",
                CurrencyName = "British Pound",
                CurrencySymbol = "£",
                DecimalPlaces = 2,
                RoundingMethod = "Standard",
                RoundingPrecision = 0.01m,
                SymbolPosition = "Before",
                DecimalSeparator = ".",
                ThousandsSeparator = ",",
                CountryCode = "GB",
                CountryName = "United Kingdom",
                IsActive = true,
                IsBaseCurrency = false,
                HasTransactionHistory = true,
                CurrencyClassification = "Major",
                GeographicRegion = "Europe",
                MinorUnitName = "Penny",
                MinorUnitPluralName = "Pence",
                MinorUnitRatio = 100,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Currencies seeded");
    }

    #endregion

    #region Payment Term Seeding

    private async Task SeedPaymentTermsAsync(Guid tenantId, DateTime baseDate)
    {
        await PaymentTermBaselineSeeder.SeedTenantBaselineAsync(_context, tenantId);
        // Development Finance fixtures should exercise the same approved close templates as a
        // tenant created through the API; this avoids demo-only checklist behavior.
        await FinanceCloseTemplateBaselineSeeder.SeedTenantBaselineAsync(_context, tenantId);

        _logger.LogInformation("Payment terms seeded");
    }

    #endregion

    #region Exchange Rate Seeding

    private async Task SeedExchangeRatesAsync(Guid tenantId, DateTime baseDate)
    {
        var effectiveDate = new DateTime(2024, 12, 15, 0, 0, 0, DateTimeKind.Utc);
        var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // System user

        // GHS to USD
        if (!await _context.ExchangeRates.AnyAsync(r => 
            r.BaseCurrencyCode == "GHS" && r.TargetCurrencyCode == "USD" && r.TenantId == tenantId))
        {
            await _context.ExchangeRates.AddAsync(new ExchangeRate
            {
                Id = Guid.Parse("00000002-0001-0001-0001-000000000001"),
                TenantId = tenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                // Canonical direction: 1 source/base currency = Rate target currency.
                Rate = 0.08m,
                InverseRate = 12.5m,
                RateType = ExchangeRateType.Daily,
                EffectiveDate = effectiveDate,
                RateSource = "Bank of Ghana",
                IsActive = true,
                IsManualEntry = false,
                CreatedByUserId = systemUserId,
                CreatedDate = effectiveDate,
                CreatedAt = effectiveDate,
                CreatedBy = "System"
            });
        }

        // GHS to EUR
        if (!await _context.ExchangeRates.AnyAsync(r => 
            r.BaseCurrencyCode == "GHS" && r.TargetCurrencyCode == "EUR" && r.TenantId == tenantId))
        {
            await _context.ExchangeRates.AddAsync(new ExchangeRate
            {
                Id = Guid.Parse("00000002-0001-0001-0001-000000000002"),
                TenantId = tenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "EUR",
                Rate = 0.076m,
                InverseRate = 13.157895m,
                RateType = ExchangeRateType.Daily,
                EffectiveDate = effectiveDate,
                RateSource = "Bank of Ghana",
                IsActive = true,
                IsManualEntry = false,
                CreatedByUserId = systemUserId,
                CreatedDate = effectiveDate,
                CreatedAt = effectiveDate,
                CreatedBy = "System"
            });
        }

        // GHS to GBP
        if (!await _context.ExchangeRates.AnyAsync(r => 
            r.BaseCurrencyCode == "GHS" && r.TargetCurrencyCode == "GBP" && r.TenantId == tenantId))
        {
            await _context.ExchangeRates.AddAsync(new ExchangeRate
            {
                Id = Guid.Parse("00000002-0001-0001-0001-000000000003"),
                TenantId = tenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "GBP",
                Rate = 0.063m,
                InverseRate = 15.873016m,
                RateType = ExchangeRateType.Daily,
                EffectiveDate = effectiveDate,
                RateSource = "Bank of Ghana",
                IsActive = true,
                IsManualEntry = false,
                CreatedByUserId = systemUserId,
                CreatedDate = effectiveDate,
                CreatedAt = effectiveDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Exchange rates seeded");
    }

    #endregion

    #region Fiscal Year Seeding

    private async Task SeedFiscalYearsAsync(Guid tenantId, DateTime baseDate)
    {
        await EnsureFiscalYearAsync(
            tenantId,
            Guid.Parse("00000003-0001-0001-0001-000000000003"),
            2025,
            status: "Closed",
            isClosed: true,
            isLocked: true,
            baseDate);

        await EnsureFiscalYearAsync(
            tenantId,
            Guid.Parse("00000003-0001-0001-0001-000000000004"),
            2026,
            status: "Open",
            isClosed: false,
            isLocked: false,
            baseDate);

        _logger.LogInformation("Fiscal years seeded");
    }

    private async Task EnsureFiscalYearAsync(
        Guid tenantId,
        Guid preferredId,
        int year,
        string status,
        bool isClosed,
        bool isLocked,
        DateTime createdAt)
    {
        var code = $"FY{year}";
        var startDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var existing = await _context.FiscalYears
            .FirstOrDefaultAsync(fy => fy.FiscalYearCode == code && fy.TenantId == tenantId);

        if (existing != null)
        {
            // Fiscal-year status is operational accounting data, not reference data. Development
            // seeding runs repeatedly, so updating an existing row here could silently reopen a
            // year that Finance deliberately closed or locked. Seed only missing demo years.
            return;
        }

        var idInUse = await _context.FiscalYears.AnyAsync(fy => fy.Id == preferredId);
        await _context.FiscalYears.AddAsync(new FiscalYear
        {
            Id = idInUse ? Guid.NewGuid() : preferredId,
            TenantId = tenantId,
            FiscalYearCode = code,
            FiscalYearName = $"Fiscal Year {year}",
            Year = year,
            StartDate = startDate,
            EndDate = endDate,
            TotalDays = DateTime.IsLeapYear(year) ? 366 : 365,
            FiscalYearType = "Calendar",
            Status = status,
            IsActive = !isClosed,
            NumberOfPeriods = 12,
            BaseCurrency = "GHS",
            IsClosed = isClosed,
            IsLocked = isLocked,
            CreatedAt = createdAt,
            CreatedBy = "System"
        });
    }

    #endregion

    #region Fiscal Period Seeding

    private async Task SeedFiscalPeriodsAsync(Guid tenantId, DateTime baseDate)
    {
        var fy2025Id = Guid.Parse("00000003-0001-0001-0001-000000000003");
        var fy2026Id = Guid.Parse("00000003-0001-0001-0001-000000000004");

        for (var month = 1; month <= 12; month++)
        {
            await EnsureCalendarMonthPeriodAsync(tenantId, fy2025Id, 2025, month, "Closed", true, true, baseDate);

            // The fresh FY2026 demo baseline keeps the July-August operational window open.
            // Earlier periods are closed and later periods remain Future so developers exercise
            // the real open transition instead of bypassing period controls with an all-year-open
            // fixture.
            var status = month <= 6 ? "Closed" : month <= 8 ? "Open" : "Future";
            await EnsureCalendarMonthPeriodAsync(
                tenantId,
                fy2026Id,
                2026,
                month,
                status,
                isClosed: month <= 6,
                isLocked: false,
                createdAt: baseDate);
        }

        _logger.LogInformation("Fiscal periods seeded");
    }

    private Task EnsureCalendarMonthPeriodAsync(
        Guid tenantId,
        Guid fiscalYearId,
        int year,
        int month,
        string status,
        bool isClosed,
        bool isLocked,
        DateTime createdAt)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1).AddTicks(-1);
        var preferredId = Guid.Parse($"00000004-0001-0001-{year}-0000000000{month:D2}");

        return EnsureFiscalPeriodAsync(
            tenantId,
            fiscalYearId,
            preferredId,
            month,
            startDate.ToString("MMMM yyyy"),
            startDate,
            endDate,
            status,
            isClosed,
            isLocked,
            createdAt);
    }

    private async Task EnsureFiscalPeriodAsync(
        Guid tenantId,
        Guid fiscalYearId,
        Guid preferredId,
        int periodNumber,
        string periodName,
        DateTime startDate,
        DateTime endDate,
        string status,
        bool isClosed,
        bool isLocked,
        DateTime createdAt)
    {
        var existing = await _context.FiscalPeriods
            .FirstOrDefaultAsync(fp =>
                fp.TenantId == tenantId
                && fp.FiscalYearId == fiscalYearId
                && fp.PeriodNumber == periodNumber);

        if (existing != null)
        {
            // Never rewrite an existing accounting period. Its status, dates and lock state may
            // represent posted transactions, a signed close, or an audit decision. Re-running
            // demo seeding must therefore be missing-only and operationally idempotent.
            return;
        }

        var idInUse = await _context.FiscalPeriods.AnyAsync(fp => fp.Id == preferredId);
        var insertId = idInUse ? Guid.NewGuid() : preferredId;

        await _context.FiscalPeriods.AddAsync(new FiscalPeriod
        {
            Id = insertId,
            TenantId = tenantId,
            FiscalYearId = fiscalYearId,
            PeriodNumber = periodNumber,
            PeriodName = periodName,
            PeriodCode = $"{startDate:yyyy-MM}",
            StartDate = startDate,
            EndDate = endDate,
            Status = status,
            PeriodStatus = status,
            IsOpen = string.Equals(status, "Open", StringComparison.OrdinalIgnoreCase),
            IsClosed = isClosed,
            IsLocked = isLocked,
            PeriodDays = (endDate.Date - startDate.Date).Days + 1,
            CreatedAt = createdAt,
            CreatedBy = "System"
        });
    }

    #endregion

    #region Account Segment Seeding

    private async Task SeedAccountSegmentsAsync(Guid tenantId, DateTime baseDate)
    {
        await new FinanceSegmentDimensionManifestSeeder(_context, _logger).SeedAsync(tenantId, baseDate);
    }
    #endregion

    #region Account Seeding

    private Task SeedAccountsAsync(Guid tenantId, DateTime baseDate) =>
        SeedPhase5AccountsAsync(tenantId, baseDate);
    private async Task SeedPhase5AccountsAsync(Guid tenantId, DateTime baseDate)
    {
        var company = await _context.AccountSegmentStructures.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.SegmentCode == FinanceSegmentDimensionManifestSeeder.CompanyCode && item.IsActive && !item.IsDeleted);
        var natural = await _context.AccountSegmentStructures.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.SegmentCode == FinanceSegmentDimensionManifestSeeder.NaturalAccountCode && item.IsActive && !item.IsDeleted);
        if (company == null || natural == null)
        {
            _logger.LogError("Phase 5 COMPANY/NATURAL_ACCOUNT structure is unavailable for tenant {TenantId}.", tenantId);
            return;
        }

        var companyValue = await _context.SegmentLookupValues.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.SegmentStructureId == company.Id && item.IsActive && !item.IsDeleted);
        if (companyValue == null)
        {
            _logger.LogError("Phase 5 COMPANY lookup value is unavailable for tenant {TenantId}.", tenantId);
            return;
        }

        // Payroll accounts are part of the same canonical Finance chart. Include them before
        // the payroll-specific enrichment pass so every system-created GL account receives the
        // mandatory COMPANY/NATURAL_ACCOUNT identity on its first seed.
        var definitions = GetStandardChartOfAccounts(tenantId, baseDate)
            .Concat(GetPayrollChartOfAccounts(tenantId, baseDate))
            .ToList();
        var definitionIds = definitions.Select(item => item.Id).ToArray();
        var existingAccounts = await _context.Accounts.Include(item => item.SegmentValues.Where(value => !value.IsDeleted))
            .Where(item => item.TenantId == tenantId && definitionIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id);
        var newAccounts = new List<Account>();
        var upgradedAccounts = 0;
        foreach (var definition in definitions)
        {
            var isNew = !existingAccounts.TryGetValue(definition.Id, out var account);
            account ??= definition;
            if (isNew) newAccounts.Add(account);

            var systemManaged = isNew || (!string.IsNullOrWhiteSpace(account.CreatedBy)
                && account.CreatedBy.StartsWith("System", StringComparison.OrdinalIgnoreCase));
            if (!systemManaged || account.SegmentValues.Any())
                continue;

            var naturalValue = account.AccountCode.Trim().ToUpperInvariant();
            if (naturalValue.Length != natural.SegmentLength || !naturalValue.All(char.IsDigit))
            {
                _logger.LogWarning(
                    "System GL account {AccountId} was not upgraded because natural code {AccountCode} does not match the governed segment definition.",
                    account.Id, account.AccountCode);
                continue;
            }
            account.IsSegmented = true;
            account.AccountNumber = $"{companyValue.SegmentValue}-{naturalValue}";
            var segmentValues = new List<AccountSegmentValue>
            {
                NewValue(company, companyValue.SegmentValue, companyValue.Description, companyValue.Id),
                NewValue(natural, naturalValue, account.AccountName, null)
            };
            account.SegmentValues = segmentValues;
            if (!isNew)
            {
                // These values are discovered through an already tracked account. Their GUID keys
                // are assigned client-side, so relationship discovery can otherwise classify them
                // as existing rows and issue UPDATEs that affect zero rows on SQL Server.
                _context.AccountSegmentValues.AddRange(segmentValues);
            }
            if (!isNew) upgradedAccounts++;
        }

        if (newAccounts.Count > 0) await _context.Accounts.AddRangeAsync(newAccounts);
        await _context.SaveChangesAsync();
        _logger.LogInformation(
            "Seeded {CreatedCount} and upgraded {UpgradedCount} system-managed Phase 5 COMPANY/NATURAL_ACCOUNT GL identities.",
            newAccounts.Count, upgradedAccounts);

        AccountSegmentValue NewValue(AccountSegmentStructure structure, string value, string? description, Guid? lookupId) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SegmentStructureId = structure.Id,
            SegmentPosition = structure.SegmentPosition, SegmentValue = value, SegmentLookupValueId = lookupId,
            SegmentValueDescription = description, EffectiveDate = baseDate, IsLocked = false,
            CreatedAt = baseDate, CreatedBy = "System (Finance Segment Manifest 1.0)"
        };
    }

    /// <summary>Preserves the old call boundary while deliberately creating no dimension-expanded accounts.</summary>
    private Task SeedFinanceDemoAccountCombinationsAsync(Guid tenantId, DateTime baseDate)
    {
        // Department and project now belong to transaction coding. Creating GL identities for
        // their Cartesian combinations would duplicate the dimension architecture.
        return Task.CompletedTask;
    }
    private async Task SeedUnitAccountingDemoDataAsync(Guid tenantId, DateTime baseDate)
    {
        var unitTypeDefinitions = new (Guid Id, string Code, string Name, string Description, int DecimalPlaces)[]
        {
            (Guid.Parse("10000000-0000-0000-0000-000000000101"), "EMP", "Employees", "Headcount used for workforce ratios and cost allocations.", 0),
            (Guid.Parse("10000000-0000-0000-0000-000000000102"), "SQM", "Square Meters", "Area measurements used for occupancy and facilities allocations.", 2),
            (Guid.Parse("10000000-0000-0000-0000-000000000103"), "HRS", "Hours", "Hours used for labour, machine-time, and utilization metrics.", 2),
            (Guid.Parse("10000000-0000-0000-0000-000000000104"), "UNIT", "Production Units", "Operational output count used for production KPIs.", 0)
        };

        var unitTypesByCode = await _context.UnitTypes
            .Where(ut => ut.TenantId == tenantId && !ut.IsDeleted)
            .ToDictionaryAsync(ut => ut.Code);

        foreach (var definition in unitTypeDefinitions)
        {
            if (unitTypesByCode.ContainsKey(definition.Code))
            {
                continue;
            }

            var unitType = new UnitType
            {
                Id = definition.Id,
                TenantId = tenantId,
                Code = definition.Code,
                Name = definition.Name,
                Description = definition.Description,
                DecimalPlaces = definition.DecimalPlaces,
                IsActive = true,
                CreatedAt = baseDate,
                CreatedBy = "System"
            };

            _context.UnitTypes.Add(unitType);
            unitTypesByCode[unitType.Code] = unitType;
        }

        var accountDefinitions = new (Guid Id, string AccountNumber, string Name, string Description, string UnitTypeCode, string? ParentAccountNumber, int AccountLevel, bool IsPostingAccount, decimal CurrentBalance)[]
        {
            (Guid.Parse("10000000-0000-0000-0000-000000001000"), "U-1000", "Total Employees", "Summary headcount across all departments.", "EMP", null, 1, false, 45m),
            (Guid.Parse("10000000-0000-0000-0000-000000001100"), "U-1100", "Operations Employees", "Operations department headcount.", "EMP", "U-1000", 2, true, 18m),
            (Guid.Parse("10000000-0000-0000-0000-000000001200"), "U-1200", "Sales Employees", "Sales department headcount.", "EMP", "U-1000", 2, true, 12m),
            (Guid.Parse("10000000-0000-0000-0000-000000001300"), "U-1300", "Administration Employees", "Administration department headcount.", "EMP", "U-1000", 2, true, 15m),
            (Guid.Parse("10000000-0000-0000-0000-000000002000"), "U-2000", "Total Office Space", "Summary office space occupied by the business.", "SQM", null, 1, false, 2500m),
            (Guid.Parse("10000000-0000-0000-0000-000000002100"), "U-2100", "Head Office Space", "Head office floor area.", "SQM", "U-2000", 2, true, 1600m),
            (Guid.Parse("10000000-0000-0000-0000-000000002200"), "U-2200", "Branch Office Space", "Branch office floor area.", "SQM", "U-2000", 2, true, 900m),
            (Guid.Parse("10000000-0000-0000-0000-000000003000"), "U-3000", "Machine Hours", "Machine hours available for production analysis.", "HRS", null, 1, true, 1240m),
            (Guid.Parse("10000000-0000-0000-0000-000000004000"), "U-4000", "Production Units", "Completed production units for operational KPIs.", "UNIT", null, 1, true, 8600m)
        };

        var unitAccountsByNumber = await _context.UnitAccounts
            .Where(ua => ua.TenantId == tenantId && !ua.IsDeleted)
            .ToDictionaryAsync(ua => ua.AccountNumber);

        foreach (var definition in accountDefinitions)
        {
            if (unitAccountsByNumber.ContainsKey(definition.AccountNumber))
            {
                continue;
            }

            if (!unitTypesByCode.TryGetValue(definition.UnitTypeCode, out var unitType))
            {
                _logger.LogWarning("Skipping unit account {AccountNumber}; unit type {UnitTypeCode} is missing.", definition.AccountNumber, definition.UnitTypeCode);
                continue;
            }

            Guid? parentAccountId = null;
            if (!string.IsNullOrWhiteSpace(definition.ParentAccountNumber))
            {
                if (!unitAccountsByNumber.TryGetValue(definition.ParentAccountNumber, out var parentAccount))
                {
                    _logger.LogWarning("Skipping unit account {AccountNumber}; parent account {ParentAccountNumber} is missing.", definition.AccountNumber, definition.ParentAccountNumber);
                    continue;
                }

                parentAccountId = parentAccount.Id;
            }

            var unitAccount = new UnitAccount
            {
                Id = definition.Id,
                TenantId = tenantId,
                AccountNumber = definition.AccountNumber,
                Name = definition.Name,
                Description = definition.Description,
                UnitTypeId = unitType.Id,
                ParentAccountId = parentAccountId,
                AccountLevel = definition.AccountLevel,
                IsPostingAccount = definition.IsPostingAccount,
                IsActive = true,
                CurrentBalance = definition.CurrentBalance,
                CreatedAt = baseDate,
                CreatedBy = "System"
            };

            _context.UnitAccounts.Add(unitAccount);
            unitAccountsByNumber[unitAccount.AccountNumber] = unitAccount;
        }

        var openPeriod = await _context.FiscalPeriods
            .Where(fp => fp.TenantId == tenantId && fp.IsOpen && !fp.IsDeleted)
            .OrderBy(fp => fp.StartDate)
            .FirstOrDefaultAsync();

        if (openPeriod == null)
        {
            _logger.LogInformation("No open fiscal period found for unit-account demo balances. Seeded unit types/accounts only.");
            return;
        }

        var postingAccountIds = accountDefinitions
            .Where(a => a.IsPostingAccount && unitAccountsByNumber.ContainsKey(a.AccountNumber))
            .Select(a => unitAccountsByNumber[a.AccountNumber].Id)
            .ToList();

        var existingBalanceAccountIds = await _context.UnitAccountBalances
            .Where(b => b.TenantId == tenantId && b.FiscalPeriodId == openPeriod.Id && postingAccountIds.Contains(b.UnitAccountId))
            .Select(b => b.UnitAccountId)
            .ToListAsync();

        foreach (var definition in accountDefinitions.Where(a => a.IsPostingAccount))
        {
            if (!unitAccountsByNumber.TryGetValue(definition.AccountNumber, out var unitAccount) ||
                existingBalanceAccountIds.Contains(unitAccount.Id))
            {
                continue;
            }

            // Demo statistical balances only; production movements should come from posted UnitJournalEntry lines.
            _context.UnitAccountBalances.Add(new UnitAccountBalance
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UnitAccountId = unitAccount.Id,
                FiscalYearId = openPeriod.FiscalYearId,
                FiscalPeriodId = openPeriod.Id,
                OpeningBalance = definition.CurrentBalance,
                PeriodActivity = 0,
                ClosingBalance = definition.CurrentBalance,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
    }

    private async Task SeedPayrollAccountsAsync(Guid tenantId, DateTime baseDate)
    {
        var payrollAccounts = GetPayrollChartOfAccounts(tenantId, baseDate);
        var payrollCodes = payrollAccounts.Select(a => a.AccountCode).ToList();
        List<Account> existingAccounts;
        try
        {
            existingAccounts = await _context.Accounts
                .Where(a => a.TenantId == tenantId && payrollCodes.Contains(a.AccountCode))
                .ToListAsync();
        }
        catch (SqlException ex) when (ex.Number == 207)
        {
            _logger.LogWarning(
                "Skipping payroll GL account seed because account schema appears behind code (missing columns). Apply latest migrations and rerun seeding.");
            return;
        }

        foreach (var account in payrollAccounts)
        {
            var existing = existingAccounts.FirstOrDefault(a => a.AccountCode == account.AccountCode);
            if (existing == null)
            {
                _context.Accounts.Add(account);
                continue;
            }

            existing.AccountName = account.AccountName;
            existing.AccountType = account.AccountType;
            existing.AccountCategory = account.AccountCategory;
            existing.AccountSubCategory = account.AccountSubCategory;
            existing.Description = account.Description;
            existing.CurrencyCode = account.CurrencyCode;
            existing.IsMultiCurrency = account.IsMultiCurrency;
            existing.IsIFRSClassified = account.IsIFRSClassified;
            existing.IsBaseClassified = account.IsBaseClassified;
            existing.IsLocalClassified = account.IsLocalClassified;
            existing.AllowDirectPosting = account.AllowDirectPosting;
            existing.IsControlAccount = account.IsControlAccount;
            existing.BudgetTrackingEnabled = account.BudgetTrackingEnabled;
            existing.Status = AccountStatus.Active;
            existing.IsSystemAccount = true;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = "System";
        }

        _logger.LogInformation("Ensured {Count} payroll GL accounts in Finance chart of accounts.", payrollAccounts.Count);
    }

    private static List<Account> GetPayrollChartOfAccounts(Guid tenantId, DateTime baseDate)
        =>
        [
            new Account
            {
                Id = Guid.Parse("00000005-1010-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1010",
                AccountNumber = "000-1010-0000",
                AccountName = "Cash and Bank - Payroll Clearing",
                AccountType = AccountType.Asset,
                AccountCategory = "Current Assets",
                AccountSubCategory = "Cash and Bank",
                Description = "Default bank and cash clearing account used by payroll net pay journals.",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = true,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                IsSystemAccount = true,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1120-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1120",
                AccountNumber = "000-1120-0000",
                AccountName = "Staff Loans and Salary Advances",
                AccountType = AccountType.Asset,
                AccountCategory = "Current Assets",
                AccountSubCategory = "Employee Receivables",
                Description = "Receivable account for staff loan repayments, salary advances, and related payroll recoveries.",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = true,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                IsSystemAccount = true,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-2120-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "2120",
                AccountNumber = "000-2120-0000",
                AccountName = "Accrued Payroll Payables",
                AccountType = AccountType.Liability,
                AccountCategory = "Current Liabilities",
                AccountSubCategory = "Payroll Payables",
                Description = "Default liability account for accrued payroll deductions, taxes, pensions, and contribution payables.",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = true,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                IsSystemAccount = true,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-4920-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "4920",
                AccountNumber = "000-4920-0000",
                AccountName = "Payroll Recoveries and Interest Income",
                AccountType = AccountType.Revenue,
                AccountCategory = "Other Income",
                AccountSubCategory = "Payroll Recoveries",
                Description = "Income account for payroll loan interest and recoveries credited from payroll runs.",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = true,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                IsSystemAccount = true,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-6020-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "6020",
                AccountNumber = "000-6020-0000",
                AccountName = "Salaries, Wages and Payroll Costs",
                AccountType = AccountType.Expense,
                AccountCategory = "Operating Expenses",
                AccountSubCategory = "Payroll Costs",
                Description = "Default payroll expense account for basic salary, allowances, overtime, employer contributions, and arrears.",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = true,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                IsSystemAccount = true,
                CreatedAt = baseDate,
                CreatedBy = "System"
            }
        ];

    private List<Account> GetStandardChartOfAccounts(Guid tenantId, DateTime baseDate)
    {
        // Reference-data seeding must never invent a ledger balance. Earlier development fixtures
        // placed presentation values on accounts without journals, which made the
        // trial balance, detailed ledger and account card disagree on a fresh database. All GL
        // accounts now start at zero; opening positions must enter through the controlled opening-
        // balance workspace so debit/credit evidence and subledger reconciliation are retained.
        return new List<Account>
        {
            // ===== ASSETS =====
            new Account
            {
                Id = Guid.Parse("00000005-1000-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1000",
                AccountNumber = "1000",
                AccountName = "Cash and Cash Equivalents",
                AccountType = AccountType.Asset,
                AccountCategory = "Current Assets",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1100-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1100",
                AccountNumber = "1100",
                AccountName = "Accounts Receivable",
                AccountType = AccountType.Asset,
                AccountCategory = "Current Assets",
                CurrencyCode = "GHS",
                IsMultiCurrency = true,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = true,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1130-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1130",
                AccountNumber = "1130",
                AccountName = "Withholding Tax Receivable",
                AccountType = AccountType.Asset,
                AccountCategory = "Current Assets",
                AccountSubCategory = "Tax Receivables",
                Description = "Control account for WHT and withholding VAT suffered on TDC customer receipts pending statutory credit/offset.",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = true,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1200-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1200",
                AccountNumber = "1200",
                AccountName = "Inventory",
                AccountType = AccountType.Asset,
                AccountCategory = "Current Assets",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1500-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1500",
                AccountNumber = "1500",
                AccountName = "Property, Plant & Equipment",
                AccountType = AccountType.Asset,
                AccountCategory = "Fixed Assets",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = true,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1510-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1510",
                AccountNumber = "1510",
                AccountName = "Buildings",
                AccountType = AccountType.Asset,
                AccountCategory = "Fixed Assets",
                ParentAccountId = Guid.Parse("00000005-1500-0000-0000-000000000001"),
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1520-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1520",
                AccountNumber = "1520",
                AccountName = "Equipment",
                AccountType = AccountType.Asset,
                AccountCategory = "Fixed Assets",
                ParentAccountId = Guid.Parse("00000005-1500-0000-0000-000000000001"),
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1530-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1530",
                AccountNumber = "1530",
                AccountName = "Vehicles",
                AccountType = AccountType.Asset,
                AccountCategory = "Fixed Assets",
                ParentAccountId = Guid.Parse("00000005-1500-0000-0000-000000000001"),
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1590-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1590",
                AccountNumber = "1590",
                AccountName = "Accumulated Depreciation",
                AccountType = AccountType.Asset,
                AccountCategory = "Fixed Assets",
                ParentAccountId = Guid.Parse("00000005-1500-0000-0000-000000000001"),
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1990-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1990",
                AccountNumber = "1990",
                AccountName = "Migration Clearing Account",
                AccountType = AccountType.Asset,
                AccountCategory = "Current Assets",
                Description = "Temporary clearing account for opening-balance migration offsets; expected to net to zero after migration.",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1020-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1020",
                AccountNumber = "1020",
                AccountName = "Undeposited Cash",
                AccountType = AccountType.Asset,
                AccountCategory = "Cash and Cash Equivalents",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = true,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1021-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1021",
                AccountNumber = "1021",
                AccountName = "Cheques Awaiting Deposit",
                AccountType = AccountType.Asset,
                AccountCategory = "Cash and Cash Equivalents",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = true,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1022-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1022",
                AccountNumber = "1022",
                AccountName = "Mobile Money Clearing",
                AccountType = AccountType.Asset,
                AccountCategory = "Cash and Cash Equivalents",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = true,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-1023-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "1023",
                AccountNumber = "1023",
                AccountName = "Card Settlement Clearing",
                AccountType = AccountType.Asset,
                AccountCategory = "Cash and Cash Equivalents",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = true,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-9999-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "9999",
                AccountNumber = "9999",
                AccountName = "Suspense Account",
                AccountType = AccountType.Asset,
                AccountCategory = "Current Assets",
                Description = "Temporary holding account for unallocated transactions",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },

            // ===== LIABILITIES =====
            new Account
            {
                Id = Guid.Parse("00000005-2000-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "2000",
                AccountNumber = "2000",
                AccountName = "Accounts Payable",
                AccountType = AccountType.Liability,
                AccountCategory = "Current Liabilities",
                CurrencyCode = "GHS",
                IsMultiCurrency = true,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = true,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-2100-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "2100",
                AccountNumber = "2100",
                AccountName = "Accrued Expenses",
                AccountType = AccountType.Liability,
                AccountCategory = "Current Liabilities",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-2110-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "2110",
                AccountNumber = "2110",
                AccountName = "GRV Accrual Control",
                AccountType = AccountType.Liability,
                AccountCategory = "Current Liabilities",
                AccountSubCategory = "Goods Received Not Invoiced",
                Description = "Dedicated control account credited when goods are received before supplier invoicing, then cleared when the AP invoice is posted.",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = true,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-2200-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "2200",
                AccountNumber = "2200",
                AccountName = "Tax/VAT Control",
                AccountType = AccountType.Liability,
                AccountCategory = "Current Liabilities",
                AccountSubCategory = "Tax Payables",
                Description = "Control account for VAT, levies, withholding tax, and other statutory tax clearing balances.",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = true,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-2500-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "2500",
                AccountNumber = "2500",
                AccountName = "Long-term Debt",
                AccountType = AccountType.Liability,
                AccountCategory = "Non-Current Liabilities",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },

            // ===== EQUITY =====
            new Account
            {
                Id = Guid.Parse("00000005-3000-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "3000",
                AccountNumber = "3000",
                AccountName = "Share Capital",
                AccountType = AccountType.Equity,
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-3100-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "3100",
                AccountNumber = "3100",
                AccountName = "Retained Earnings",
                AccountType = AccountType.Equity,
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },

            // ===== REVENUE =====
            new Account
            {
                Id = Guid.Parse("00000005-4000-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "4000",
                AccountNumber = "4000",
                AccountName = "Sales Revenue",
                AccountType = AccountType.Revenue,
                CurrencyCode = "GHS",
                IsMultiCurrency = true,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-4100-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "4100",
                AccountNumber = "4100",
                AccountName = "Service Revenue",
                AccountType = AccountType.Revenue,
                CurrencyCode = "GHS",
                IsMultiCurrency = true,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-4110-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "4110",
                AccountNumber = "4110",
                AccountName = "Rental Income",
                AccountType = AccountType.Revenue,
                AccountCategory = "Operating Revenue",
                AccountSubCategory = "Property Rental",
                Description = "Rental income from Estate and Property Management lease billing.",
                CurrencyCode = "GHS",
                IsMultiCurrency = true,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-4210-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "4210",
                AccountNumber = "4210",
                AccountName = "Sales Discounts Allowed",
                AccountType = AccountType.Revenue,
                AccountCategory = "Revenue Deductions",
                AccountSubCategory = "Contra Revenue",
                Description = "Contra-revenue account debited for customer trade and settlement discounts allowed.",
                CurrencyCode = "GHS",
                IsMultiCurrency = true,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-4900-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "4900",
                AccountNumber = "4900",
                AccountName = "Other Income",
                AccountType = AccountType.Revenue,
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-4910-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "4910",
                AccountNumber = "4910",
                AccountName = "Purchase Discounts Received",
                AccountType = AccountType.Revenue,
                AccountCategory = "Other Income",
                AccountSubCategory = "Supplier Discounts",
                Description = "Income account credited for supplier trade and settlement discounts received.",
                CurrencyCode = "GHS",
                IsMultiCurrency = true,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-7100-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "7100",
                AccountNumber = "7100",
                AccountName = "Unrealized Exchange Gain",
                AccountType = AccountType.Revenue,
                AccountCategory = "Other Income",
                Description = "Unrealized foreign exchange gains from multi-currency revaluation",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-7110-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "7110",
                AccountNumber = "7110",
                AccountName = "Unrealized Exchange Loss",
                AccountType = AccountType.Expense,
                AccountCategory = "Other Expenses",
                Description = "Unrealized foreign exchange losses from multi-currency revaluation",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-7200-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "7200",
                AccountNumber = "7200",
                AccountName = "Realized Exchange Gain",
                AccountType = AccountType.Revenue,
                AccountCategory = "Other Income",
                Description = "Realized foreign exchange gains from settled transactions",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-7210-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "7210",
                AccountNumber = "7210",
                AccountName = "Realized Exchange Loss",
                AccountType = AccountType.Expense,
                AccountCategory = "Other Expenses",
                Description = "Realized foreign exchange losses from settled transactions",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },

            // ===== EXPENSES =====
            new Account
            {
                Id = Guid.Parse("00000005-5000-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "5000",
                AccountNumber = "5000",
                AccountName = "Cost of Goods Sold",
                AccountType = AccountType.Expense,
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-6000-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "6000",
                AccountNumber = "6000",
                AccountName = "Salaries and Wages",
                AccountType = AccountType.Expense,
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-6100-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "6100",
                AccountNumber = "6100",
                AccountName = "Rent Expense",
                AccountType = AccountType.Expense,
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-6200-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "6200",
                AccountNumber = "6200",
                AccountName = "Utilities Expense",
                AccountType = AccountType.Expense,
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-6300-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "6300",
                AccountNumber = "6300",
                AccountName = "Depreciation Expense",
                AccountType = AccountType.Expense,
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-6400-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "6400",
                AccountNumber = "6400",
                AccountName = "Marketing and Advertising",
                AccountType = AccountType.Expense,
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-6500-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "6500",
                AccountNumber = "6500",
                AccountName = "Professional Fees",
                AccountType = AccountType.Expense,
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = true,
                IsControlAccount = false,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new Account
            {
                Id = Guid.Parse("00000005-6600-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "6600",
                AccountNumber = "6600",
                AccountName = "Bank Charges",
                AccountType = AccountType.Expense,
                AccountCategory = "Finance Costs",
                CurrencyCode = "GHS",
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = true,
                IsBaseClassified = true,
                IsLocalClassified = true,
                AllowDirectPosting = false,
                IsControlAccount = true,
                BudgetTrackingEnabled = true,
                Status = AccountStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = "System"
            }
        };
    }

    #endregion

    #region Fixed Asset Categories Seeding

    private async Task SeedFixedAssetCategoriesAsync(Guid tenantId, DateTime baseDate)
    {
        if (await _context.FixedAssetCategories.AnyAsync(c => c.TenantId == tenantId))
        {
            _logger.LogInformation("Fixed asset categories already exist. Skipping.");
            return;
        }

        var categories = new List<FixedAssetCategory>
        {
            new FixedAssetCategory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Buildings",
                Code = "FA-BLDG",
                Description = "Buildings and structural assets",
                DefaultMethod = DepreciationMethod.StraightLine,
                DefaultUsefulLifeMonths = 240,
                DefaultResidualValuePercent = 5,
                AssetAccountId = Guid.Parse("00000005-1510-0000-0000-000000000001"),
                AccumulatedDepreciationAccountId = Guid.Parse("00000005-1590-0000-0000-000000000001"),
                DepreciationExpenseAccountId = Guid.Parse("00000005-6300-0000-0000-000000000001"),
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new FixedAssetCategory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Equipment",
                Code = "FA-EQP",
                Description = "Production and office equipment",
                DefaultMethod = DepreciationMethod.StraightLine,
                DefaultUsefulLifeMonths = 60,
                DefaultResidualValuePercent = 10,
                AssetAccountId = Guid.Parse("00000005-1520-0000-0000-000000000001"),
                AccumulatedDepreciationAccountId = Guid.Parse("00000005-1590-0000-0000-000000000001"),
                DepreciationExpenseAccountId = Guid.Parse("00000005-6300-0000-0000-000000000001"),
                CreatedAt = baseDate,
                CreatedBy = "System"
            },
            new FixedAssetCategory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Vehicles",
                Code = "FA-VEH",
                Description = "Company vehicles and transport assets",
                DefaultMethod = DepreciationMethod.StraightLine,
                DefaultUsefulLifeMonths = 48,
                DefaultResidualValuePercent = 10,
                AssetAccountId = Guid.Parse("00000005-1530-0000-0000-000000000001"),
                AccumulatedDepreciationAccountId = Guid.Parse("00000005-1590-0000-0000-000000000001"),
                DepreciationExpenseAccountId = Guid.Parse("00000005-6300-0000-0000-000000000001"),
                CreatedAt = baseDate,
                CreatedBy = "System"
            }
        };

        await _context.FixedAssetCategories.AddRangeAsync(categories);
        _logger.LogInformation($"Seeded {categories.Count} fixed asset categories");
    }

    #endregion

    #region Banking Settlement Seeding

    /// <summary>
    /// Seeds the cash-management masters used by the manual Finance demonstration script.
    /// </summary>
    /// <remarks>
    /// Balances intentionally start at zero. The opening-balance and transaction scenarios must
    /// establish monetary balances through controlled postings; placing a value directly on the
    /// bank master would make the bank card disagree with the GL and weaken the demonstration.
    /// Existing masters are preserved so rerunning Development seeding cannot overwrite local
    /// bank setup or operational balances.
    /// </remarks>
    private async Task SeedFinanceDemoBankingMastersAsync(Guid tenantId, DateTime baseDate)
    {
        var accountNumbers = new[] { "100-1001-0000", "100-1002-0000", "100-1003-0000" };
        var glAccounts = await _context.Accounts
            .Where(account =>
                account.TenantId == tenantId &&
                !account.IsDeleted &&
                accountNumbers.Contains(account.AccountNumber))
            .ToDictionaryAsync(account => account.AccountNumber);

        var bankDefinitions = new[]
        {
            new { Id = Guid.Parse("00000008-1001-0000-0000-000000000001"), Number = "TDC-DEMO-GHS-001", Name = "TDC Main Operating Account", Bank = "Ghana Commercial Bank", Branch = "Tema Main", Currency = "GHS", GL = "100-1001-0000" },
            new { Id = Guid.Parse("00000008-1002-0000-0000-000000000001"), Number = "TDC-DEMO-USD-001", Name = "TDC Foreign Currency Account", Bank = "Ghana Commercial Bank", Branch = "Tema Main", Currency = "USD", GL = "100-1002-0000" },
            new { Id = Guid.Parse("00000008-1003-0000-0000-000000000001"), Number = "TDC-DEMO-PETTY-001", Name = "TDC Finance Petty Cash Account", Bank = "Internal Cash Office", Branch = "TDC Head Office", Currency = "GHS", GL = "100-1003-0000" }
        };

        foreach (var definition in bankDefinitions)
        {
            if (await _context.BankAccounts.IgnoreQueryFilters().AnyAsync(bank =>
                    bank.TenantId == tenantId && bank.AccountNumber == definition.Number))
            {
                continue;
            }

            if (!glAccounts.TryGetValue(definition.GL, out var glAccount))
            {
                _logger.LogWarning(
                    "Skipping demo bank account {AccountNumber}; GL account {GLAccountNumber} is missing.",
                    definition.Number,
                    definition.GL);
                continue;
            }

            _context.BankAccounts.Add(new BankAccount
            {
                Id = definition.Id,
                TenantId = tenantId,
                AccountNumber = definition.Number,
                AccountName = definition.Name,
                BankName = definition.Bank,
                BankBranch = definition.Branch,
                Currency = definition.Currency,
                AccountType = BankAccountType.Checking,
                GLAccountId = glAccount.Id,
                CurrentBalance = 0m,
                AvailableBalance = 0m,
                OpeningBalance = 0m,
                IsActive = true,
                OpeningDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Notes = "Development-only TDC Finance demonstration master. Establish balances through controlled postings.",
                CreatedAt = baseDate,
                CreatedBy = "System (Finance Demo)"
            });
        }

        var paymentMethodDefinitions = new[]
        {
            new { Id = Guid.Parse("00000009-0001-0000-0000-000000000001"), Code = "CASH", Name = "Cash", Type = PaymentMethodType.Cash, RequiresBank = false, RequiresReference = false, DefaultGL = "000-1020-0000" },
            new { Id = Guid.Parse("00000009-0002-0000-0000-000000000001"), Code = "CHQ", Name = "Cheque", Type = PaymentMethodType.Cheque, RequiresBank = false, RequiresReference = true, DefaultGL = "000-1021-0000" },
            new { Id = Guid.Parse("00000009-0003-0000-0000-000000000001"), Code = "EFT", Name = "Electronic Funds Transfer", Type = PaymentMethodType.EFT, RequiresBank = true, RequiresReference = true, DefaultGL = "100-1001-0000" },
            new { Id = Guid.Parse("00000009-0004-0000-0000-000000000001"), Code = "MOMO", Name = "Mobile Money", Type = PaymentMethodType.MobileMoney, RequiresBank = false, RequiresReference = true, DefaultGL = "000-1022-0000" },
            new { Id = Guid.Parse("00000009-0005-0000-0000-000000000001"), Code = "CARD", Name = "Card", Type = PaymentMethodType.Card, RequiresBank = false, RequiresReference = true, DefaultGL = "000-1023-0000" },
            new { Id = Guid.Parse("00000009-0006-0000-0000-000000000001"), Code = "BANK", Name = "Bank Transfer", Type = PaymentMethodType.BankTransfer, RequiresBank = true, RequiresReference = true, DefaultGL = "100-1001-0000" }
        };
        var allGlAccounts = await _context.Accounts
            .Where(account => account.TenantId == tenantId && !account.IsDeleted)
            .ToDictionaryAsync(account => account.AccountNumber);

        foreach (var definition in paymentMethodDefinitions)
        {
            if (await _context.PaymentMethods.IgnoreQueryFilters().AnyAsync(method =>
                    method.TenantId == tenantId && method.Code == definition.Code))
            {
                continue;
            }

            _context.PaymentMethods.Add(new ErpSystem.Core.Entities.Finance.PaymentMethod
            {
                Id = definition.Id,
                TenantId = tenantId,
                Code = definition.Code,
                Name = definition.Name,
                Type = definition.Type,
                Description = "Development-only TDC Finance demonstration payment method.",
                IsActive = true,
                RequiresBankAccount = definition.RequiresBank,
                RequiresReference = definition.RequiresReference,
                DefaultGLAccountId = allGlAccounts.TryGetValue(definition.DefaultGL, out var account)
                    ? account.Id
                    : null,
                CreatedAt = baseDate,
                CreatedBy = "System (Finance Demo)"
            });
        }
    }

    /// <summary>
    /// Seeds realistic but fictional counterparties for Finance-owned AP and AR UAT.
    /// </summary>
    private async Task SeedFinanceDemoCounterpartiesAsync(Guid tenantId, DateTime baseDate)
    {
        var net30 = await _context.PaymentTerms
            .FirstOrDefaultAsync(term => tenantId == term.TenantId && term.Code == "NET30" && !term.IsDeleted);
        var accounts = await _context.Accounts
            .Where(account => account.TenantId == tenantId && !account.IsDeleted)
            .ToDictionaryAsync(account => account.AccountNumber);

        var supplierDefinitions = new[]
        {
            new { Code = "TDC-DEMO-SUP-001", Name = "Tema Engineering Services Ltd", Currency = "GHS", TaxId = "C0000000010", Withholding = true, Expense = "300-6500-P101", Notes = "Professional and engineering services; use WHT Services in the demonstration." },
            new { Code = "TDC-DEMO-SUP-002", Name = "Volta Office Solutions Ltd", Currency = "GHS", TaxId = "C0000000029", Withholding = true, Expense = "100-6200-0000", Notes = "Local goods and office services; use WHT Goods where the threshold is met." },
            new { Code = "TDC-DEMO-SUP-003", Name = "Global Infrastructure Systems Inc", Currency = "USD", TaxId = "FOREIGN-DEMO-003", Withholding = false, Expense = "300-6500-P101", Notes = "Foreign-currency supplier for cross-currency settlement and FX evidence." }
        };

        foreach (var definition in supplierDefinitions)
        {
            var partner = await _context.BusinessPartners.IgnoreQueryFilters().FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.PartnerCode == definition.Code);
            if (partner == null)
            {
                partner = new BusinessPartner
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PartnerCode = definition.Code,
                    PartnerName = definition.Name,
                    LegalName = definition.Name,
                    PartnerType = "Supplier",
                    RegistrationStatus = "Approved",
                    ApprovalStatus = "Approved",
                    IsActive = true,
                    TaxIdentificationNumber = definition.TaxId,
                    PrimaryContactName = "Finance Contact",
                    PrimaryContactTitle = "Accounts Officer",
                    PrimaryEmail = $"accounts.{definition.Code.ToLowerInvariant()}@example.test",
                    PrimaryPhone = "+233 30 000 0000",
                    PhysicalAddress = "Tema Development Area",
                    PhysicalCity = "Tema",
                    PhysicalCountry = definition.Currency == "GHS" ? "Ghana" : "United States",
                    Currency = definition.Currency,
                    PaymentTermId = net30?.Id,
                    PaymentTerms = "Net 30",
                    SubjectToWithholdingDeduction = definition.Withholding,
                    Notes = definition.Notes,
                    CreatedAt = baseDate,
                    CreatedBy = "System (Finance Demo)"
                };
                _context.BusinessPartners.Add(partner);
            }

            var role = await _context.BusinessPartnerRoles.IgnoreQueryFilters().FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.BusinessPartnerId == partner.Id &&
                item.RoleType == BusinessPartnerRoleType.Supplier);
            if (role == null)
            {
                role = new BusinessPartnerRole
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = partner.Id,
                    RoleType = BusinessPartnerRoleType.Supplier, Status = BusinessPartnerRoleStatus.Active,
                    ActiveFromUtc = baseDate, CreatedAt = baseDate, CreatedBy = "System (Finance Demo)"
                };
                _context.BusinessPartnerRoles.Add(role);
            }

            var profile = await _context.BusinessPartnerApProfileVersions.IgnoreQueryFilters().FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.BusinessPartnerRoleId == role.Id && item.VersionNumber == 1);
            if (profile == null)
            {
                profile = new BusinessPartnerApProfileVersion
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerRoleId = role.Id,
                    VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
                    EffectiveFrom = baseDate, ApReferenceNumber = definition.Code,
                    PaymentTermId = net30?.Id,
                    DefaultExpenseAccountId = accounts.TryGetValue(definition.Expense, out var expenseAccount)
                        ? expenseAccount.Id : null,
                    SubjectToWithholding = definition.Withholding,
                    ApprovedAtUtc = baseDate, DecisionReason = "Approved Finance demonstration profile.",
                    CreatedAt = baseDate, CreatedBy = "System (Finance Demo)"
                };
                _context.BusinessPartnerApProfileVersions.Add(profile);
                // Persist the profile before its default WHT line. The profile points back to the
                // selected line, so inserting both ends in one EF batch forms a dependency cycle.
                await _context.SaveChangesAsync();
            }

            if (definition.Withholding)
            {
                var taxCode = definition.Code == "TDC-DEMO-SUP-002" ? "WHT-GOODS" : "WHT-SERV";
                var tax = await _context.Taxes.FirstAsync(item =>
                    item.TenantId == tenantId && item.Code == taxCode && item.IsActive && !item.IsDeleted);
                var whtDefault = await _context.BusinessPartnerApWhtDefaults.IgnoreQueryFilters().FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.ApProfileVersionId == profile.Id && item.WithholdingTaxId == tax.Id);
                if (whtDefault == null)
                {
                    whtDefault = new BusinessPartnerApWhtDefault
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, ApProfileVersionId = profile.Id,
                        CategoryCode = taxCode == "WHT-GOODS" ? "GOODS" : "SERVICES",
                        CategoryName = taxCode == "WHT-GOODS" ? "Goods" : "Services",
                        WithholdingTaxId = tax.Id, IsDefaultForAp = true, IsActive = true,
                        CreatedAt = baseDate, CreatedBy = "System (Finance Demo)"
                    };
                    _context.BusinessPartnerApWhtDefaults.Add(whtDefault);
                    await _context.SaveChangesAsync();
                }
                profile.DefaultWithholdingLineId = whtDefault.Id;
                partner.DefaultWithholdingTaxId = tax.Id;
            }
        }

        var customerDefinitions = new[]
        {
            new { Code = "TDC-DEMO-CUS-001", Name = "Tema Industrial Estate Residents Association", Currency = "GHS", CreditLimit = 500000m, VatWithholdingAgent = false, TaxId = "C1000000011" },
            new { Code = "TDC-DEMO-CUS-002", Name = "Meridian Property Holdings Ltd", Currency = "GHS", CreditLimit = 750000m, VatWithholdingAgent = true, TaxId = "C1000000020" },
            new { Code = "TDC-DEMO-CUS-003", Name = "Atlantic Development Partners Ltd", Currency = "USD", CreditLimit = 1000000m, VatWithholdingAgent = false, TaxId = "FOREIGN-DEMO-C03" }
        };

        foreach (var definition in customerDefinitions)
        {
            var partner = await _context.BusinessPartners.IgnoreQueryFilters().FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.PartnerCode == definition.Code);
            if (partner == null)
            {
                partner = new BusinessPartner
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PartnerCode = definition.Code,
                    CustomerAccountNumber = definition.Code,
                    PartnerName = definition.Name,
                    LegalName = definition.Name,
                    PartnerType = "Customer",
                    CustomerType = "Corporate",
                    RegistrationStatus = "Approved",
                    ApprovalStatus = "Approved",
                    IsActive = true,
                    IsVatWithholdingAgent = definition.VatWithholdingAgent,
                    TaxTreatment = TaxTreatment.Standard,
                    TaxIdentificationNumber = definition.TaxId,
                    PrimaryContactName = "Finance Contact",
                    PrimaryContactTitle = "Accounts Officer",
                    PrimaryEmail = $"accounts.{definition.Code.ToLowerInvariant()}@example.test",
                    PrimaryPhone = "+233 30 000 0000",
                    PhysicalAddress = "Tema Development Area",
                    PhysicalCity = "Tema",
                    PhysicalCountry = definition.Currency == "GHS" ? "Ghana" : "United Kingdom",
                    Currency = definition.Currency,
                    CreditLimit = definition.CreditLimit,
                    OutstandingBalance = 0m,
                    PaymentTermId = net30?.Id,
                    PaymentTerms = "Net 30",
                    Notes = "Fictional TDC Finance UAT customer. No opening exposure is seeded.",
                    CreatedAt = baseDate,
                    CreatedBy = "System (Finance Demo)"
                };
                _context.BusinessPartners.Add(partner);
            }

            var role = await _context.BusinessPartnerRoles.IgnoreQueryFilters().FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.BusinessPartnerId == partner.Id &&
                item.RoleType == BusinessPartnerRoleType.Customer);
            if (role == null)
            {
                role = new BusinessPartnerRole
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = partner.Id,
                    RoleType = BusinessPartnerRoleType.Customer, Status = BusinessPartnerRoleStatus.Active,
                    ActiveFromUtc = baseDate, CreatedAt = baseDate, CreatedBy = "System (Finance Demo)"
                };
                _context.BusinessPartnerRoles.Add(role);
            }

            if (!await _context.BusinessPartnerArProfileVersions.IgnoreQueryFilters().AnyAsync(item =>
                    item.TenantId == tenantId && item.BusinessPartnerRoleId == role.Id && item.VersionNumber == 1))
            {
                _context.BusinessPartnerArProfileVersions.Add(new BusinessPartnerArProfileVersion
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerRoleId = role.Id,
                    VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
                    EffectiveFrom = baseDate, ArReferenceNumber = definition.Code,
                    PaymentTermId = net30?.Id, CreditLimit = definition.CreditLimit,
                    IsWithholdingAgent = definition.VatWithholdingAgent,
                    ApprovedAtUtc = baseDate, DecisionReason = "Approved Finance demonstration profile.",
                    CreatedAt = baseDate, CreatedBy = "System (Finance Demo)"
                });
            }
        }
    }

    private async Task SeedBankingSettlementDefaultsAsync(Guid tenantId, DateTime baseDate)
    {
        var definitions = new[]
        {
            new
            {
                Id = Guid.Parse("00000007-1020-0000-0000-000000000001"),
                Code = "UNDEP-CASH-GHS",
                Name = "Undeposited Cash (GHS)",
                Type = LiquidityAccountType.UndepositedCash,
                GLAccountId = Guid.Parse("00000005-1020-0000-0000-000000000001")
            },
            new
            {
                Id = Guid.Parse("00000007-1021-0000-0000-000000000001"),
                Code = "CHQ-CLEAR-GHS",
                Name = "Cheques Awaiting Deposit (GHS)",
                Type = LiquidityAccountType.ChequesAwaitingDeposit,
                GLAccountId = Guid.Parse("00000005-1021-0000-0000-000000000001")
            },
            new
            {
                Id = Guid.Parse("00000007-1022-0000-0000-000000000001"),
                Code = "MOMO-CLEAR-GHS",
                Name = "Mobile Money Clearing (GHS)",
                Type = LiquidityAccountType.MobileMoneyClearing,
                GLAccountId = Guid.Parse("00000005-1022-0000-0000-000000000001")
            },
            new
            {
                Id = Guid.Parse("00000007-1023-0000-0000-000000000001"),
                Code = "CARD-CLEAR-GHS",
                Name = "Card Settlement Clearing (GHS)",
                Type = LiquidityAccountType.CardSettlementClearing,
                GLAccountId = Guid.Parse("00000005-1023-0000-0000-000000000001")
            },
            new
            {
                // TDC begins with one controlled main cashier till. It shares the existing cash
                // control GL account with undeposited cash while retaining separate physical
                // custody sessions and liquidity entries; this avoids inventing a second ledger.
                Id = Guid.Parse("00000007-1024-0000-0000-000000000001"),
                Code = "TILL-MAIN-GHS",
                Name = "TDC Main Cashier Till (GHS)",
                Type = LiquidityAccountType.CashTill,
                GLAccountId = Guid.Parse("00000005-1020-0000-0000-000000000001")
            }
        };

        var validAccountIds = await _context.Accounts
            .Where(account =>
                account.TenantId == tenantId &&
                definitions.Select(definition => definition.GLAccountId).Contains(account.Id))
            .Select(account => account.Id)
            .ToListAsync();
        foreach (var definition in definitions.Where(definition => validAccountIds.Contains(definition.GLAccountId)))
        {
            var existing = await _context.LiquidityAccounts.FirstOrDefaultAsync(
                account => account.TenantId == tenantId && account.Code == definition.Code);
            if (existing != null)
            {
                existing.Name = definition.Name;
                existing.AccountType = definition.Type;
                existing.Currency = "GHS";
                existing.GLAccountId = definition.GLAccountId;
                existing.IsActive = true;
                existing.IsSystemAccount = true;
                existing.AllowsManualAllocations = true;
                continue;
            }

            _context.LiquidityAccounts.Add(new LiquidityAccount
            {
                Id = definition.Id,
                TenantId = tenantId,
                Code = definition.Code,
                Name = definition.Name,
                AccountType = definition.Type,
                Currency = "GHS",
                GLAccountId = definition.GLAccountId,
                IsActive = true,
                IsSystemAccount = true,
                AllowsManualAllocations = true,
                CreatedAt = baseDate,
                CreatedBy = "System",
                Notes = "Standard Banking & Settlement control account."
            });
        }

        var mappedBankIds = await _context.LiquidityAccounts
            .Where(account => account.TenantId == tenantId && account.BankAccountId.HasValue)
            .Select(account => account.BankAccountId!.Value)
            .ToListAsync();
        var banks = await _context.BankAccounts
            .Where(bank =>
                bank.TenantId == tenantId &&
                bank.IsActive &&
                bank.GLAccountId.HasValue &&
                !mappedBankIds.Contains(bank.Id))
            .ToListAsync();
        foreach (var bank in banks)
        {
            _context.LiquidityAccounts.Add(new LiquidityAccount
            {
                TenantId = tenantId,
                // The historical eight-character prefix is not unique for deterministic seed
                // GUIDs (several begin with 00000008). Sixteen hexadecimal characters keep the
                // code comfortably inside LiquidityAccount's 30-character limit while retaining
                // enough of the stable bank identifier to avoid collisions on repeatable builds.
                Code = BuildBankLiquidityAccountCode(bank.Id),
                Name = bank.AccountName,
                AccountType = LiquidityAccountType.Bank,
                Currency = bank.Currency,
                GLAccountId = bank.GLAccountId!.Value,
                BankAccountId = bank.Id,
                IsActive = true,
                IsSystemAccount = true,
                AllowsManualAllocations = false,
                CreatedAt = baseDate,
                CreatedBy = "System",
                Notes = "Bank subtype created from the existing bank account master."
            });
        }

        var returnedChequeBankChargeAccountId =
            Guid.Parse("00000005-6600-0000-0000-000000000001");
        var hasReturnedChequeBankChargeAccount = await _context.Accounts.AnyAsync(account =>
            account.TenantId == tenantId &&
            account.Id == returnedChequeBankChargeAccountId &&
            !account.IsDeleted);
        var settings = await _context.FinanceSettings.FirstOrDefaultAsync(item => item.TenantId == tenantId);
        if (settings != null &&
            !settings.ReturnedChequeBankChargeAccountId.HasValue &&
            hasReturnedChequeBankChargeAccount)
        {
            settings.ReturnedChequeBankChargeAccountId = returnedChequeBankChargeAccountId;
        }
    }

    private static string BuildBankLiquidityAccountCode(Guid bankAccountId) =>
        $"BANK-{bankAccountId.ToString("N")[..16]}".ToUpperInvariant();

    #endregion

    #region Finance Settings Seeding

    private async Task SeedFinanceSettingsAsync(Guid tenantId, DateTime baseDate)
    {
        FinanceSettings? existingSettings;
        try
        {
            existingSettings = await _context.FinanceSettings.FirstOrDefaultAsync(fs => fs.TenantId == tenantId);
        }
        catch (SqlException ex) when (ex.Number == 207)
        {
            _logger.LogWarning(
                ex,
                "Skipping finance settings seed because finance settings schema appears behind code (missing columns). Apply latest migrations and rerun seeding.");
            return;
        }
        
        if (existingSettings != null)
        {
            if (existingSettings.CoaType != "Segmented")
            {
                _logger.LogInformation($"Updating existing Finance Settings COA Type from {existingSettings.CoaType} to Segmented");
                existingSettings.CoaType = "Segmented";
            }

            var settingsUpdated = ApplyDefaultFinanceSettingsControlAccounts(existingSettings);
            if (settingsUpdated)
            {
                existingSettings.UpdatedAt = DateTime.UtcNow;
                existingSettings.UpdatedBy = "System";
                _logger.LogInformation("Updated missing finance settings control account defaults.");
            }

            return;
        }

        var settings = new FinanceSettings
        {
            Id = Guid.Parse("00000006-0001-0001-0001-000000000001"),
            TenantId = tenantId,
            CoaType = "Segmented",
            CoaConfigurationLocked = false,
            BaseCurrency = "GHS",
            AccountSeparator = "-",
            RetainedEarningsAccountId = Guid.Parse("00000005-3100-0000-0000-000000000001"),
            UnrealizedGainLossAccountId = Guid.Parse("00000005-7100-0000-0000-000000000001"),
            UnrealizedFxGainAccountId = Guid.Parse("00000005-7100-0000-0000-000000000001"),
            UnrealizedFxLossAccountId = Guid.Parse("00000005-7110-0000-0000-000000000001"),
            RealizedGainLossAccountId = Guid.Parse("00000005-7200-0000-0000-000000000001"),
            RealizedFxGainAccountId = Guid.Parse("00000005-7200-0000-0000-000000000001"),
            RealizedFxLossAccountId = Guid.Parse("00000005-7210-0000-0000-000000000001"),
            SuspenseAccountId = Guid.Parse("00000005-9999-0000-0000-000000000001"),
            ControlAccountArId = Guid.Parse("00000005-1100-0000-0000-000000000001"),
            ControlAccountApId = Guid.Parse("00000005-2000-0000-0000-000000000001"),
            ControlAccountInventoryId = Guid.Parse("00000005-1200-0000-0000-000000000001"),
            ControlAccountPayrollId = Guid.Parse("00000005-2120-0000-0000-000000000001"),
            ControlAccountTaxId = Guid.Parse("00000005-2200-0000-0000-000000000001"),
            ControlAccountGRVAccrualId = Guid.Parse("00000005-2110-0000-0000-000000000001"),
            DiscountAllowedAccountId = Guid.Parse("00000005-4210-0000-0000-000000000001"),
            DiscountReceivedAccountId = Guid.Parse("00000005-4910-0000-0000-000000000001"),
            MigrationClearingAccountId = Guid.Parse("00000005-1990-0000-0000-000000000001"),
            CreatedAt = baseDate,
            CreatedBy = "System"
        };

        await _context.FinanceSettings.AddAsync(settings);

        _logger.LogInformation("Finance settings seeded");

        static bool ApplyDefaultFinanceSettingsControlAccounts(FinanceSettings settings)
        {
            var updated = false;

            if (!settings.ControlAccountArId.HasValue)
            {
                settings.ControlAccountArId = Guid.Parse("00000005-1100-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.ControlAccountApId.HasValue)
            {
                settings.ControlAccountApId = Guid.Parse("00000005-2000-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.ControlAccountInventoryId.HasValue)
            {
                settings.ControlAccountInventoryId = Guid.Parse("00000005-1200-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.ControlAccountPayrollId.HasValue)
            {
                settings.ControlAccountPayrollId = Guid.Parse("00000005-2120-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.ControlAccountTaxId.HasValue)
            {
                settings.ControlAccountTaxId = Guid.Parse("00000005-2200-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.ControlAccountGRVAccrualId.HasValue)
            {
                settings.ControlAccountGRVAccrualId = Guid.Parse("00000005-2110-0000-0000-000000000001");
                updated = true;
            }

            // Move tenants that still have the old generic default to the dedicated GRV control account.
            if (settings.ControlAccountGRVAccrualId == Guid.Parse("00000005-2100-0000-0000-000000000001"))
            {
                settings.ControlAccountGRVAccrualId = Guid.Parse("00000005-2110-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.DiscountAllowedAccountId.HasValue)
            {
                settings.DiscountAllowedAccountId = Guid.Parse("00000005-4210-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.DiscountReceivedAccountId.HasValue)
            {
                settings.DiscountReceivedAccountId = Guid.Parse("00000005-4910-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.MigrationClearingAccountId.HasValue)
            {
                settings.MigrationClearingAccountId = Guid.Parse("00000005-1990-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.UnrealizedFxGainAccountId.HasValue)
            {
                settings.UnrealizedFxGainAccountId = Guid.Parse("00000005-7100-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.UnrealizedFxLossAccountId.HasValue)
            {
                settings.UnrealizedFxLossAccountId = Guid.Parse("00000005-7110-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.RealizedFxGainAccountId.HasValue)
            {
                settings.RealizedFxGainAccountId = Guid.Parse("00000005-7200-0000-0000-000000000001");
                updated = true;
            }

            if (!settings.RealizedFxLossAccountId.HasValue)
            {
                settings.RealizedFxLossAccountId = Guid.Parse("00000005-7210-0000-0000-000000000001");
                updated = true;
            }

            return updated;
        }
    }

    #endregion
    #region Tax Configuration Seeding

    private async Task SeedTaxConfigurationAsync(Guid tenantId, DateTime baseDate)
    {
        // 1. Create Individual Taxes
        var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001"); 

        // 1.1 NHIL (2.5%)
        var nhil = await GetOrCreateTaxAsync(tenantId, "NHIL", "National Health Insurance Levy", 2.5m, TaxApplicability.Sales, TaxCategory.Standard, false, baseDate, systemUserId);
        
        // 1.2 GETFund (2.5%)
        var getfund = await GetOrCreateTaxAsync(tenantId, "GETFUND", "GETFund Levy", 2.5m, TaxApplicability.Sales, TaxCategory.Standard, false, baseDate, systemUserId);
        
        // 1.3 COVID-19 (1%) - retained inactive for historical transactions only.
        var covid = await GetOrCreateTaxAsync(tenantId, "COVID19", "COVID-19 Health Recovery Levy", 1.0m, TaxApplicability.Sales, TaxCategory.Standard, false, baseDate, systemUserId);
        if (covid.IsActive)
        {
            covid.IsActive = false;
            covid.UpdatedAt = DateTime.UtcNow;
            covid.UpdatedBy = "System";
        }
        
        // 1.4 VAT Standard (15%)
        var vatStd = await GetOrCreateTaxAsync(tenantId, "VAT-STD", "Value Added Tax (Standard)", 15.0m, TaxApplicability.Sales, TaxCategory.Standard, true, baseDate, systemUserId);
        
        // 1.5 WHT 7.5% (Services)
        var whtServices = await GetOrCreateTaxAsync(tenantId, "WHT-SERV", "Withholding Tax (Services)", 7.5m, TaxApplicability.Purchases, TaxCategory.Withholding, false, baseDate, systemUserId, 2000m);
        
        // 1.6 WHT 3% (Goods)
        var whtGoods = await GetOrCreateTaxAsync(tenantId, "WHT-GOODS", "Withholding Tax (Goods)", 3.0m, TaxApplicability.Purchases, TaxCategory.Withholding, false, baseDate, systemUserId, 2000m);

        // 1.7 WHT 5% (Works). GRA currently applies the same GH¢2,000 annual
        // supplier threshold to resident goods, works and entity-service payments.
        var whtWorks = await GetOrCreateTaxAsync(tenantId, "WHT-WORKS", "Withholding Tax (Works)", 5.0m, TaxApplicability.Purchases, TaxCategory.Withholding, false, baseDate, systemUserId, 2000m);

        // AR receipts record tax suffered by TDC rather than calculating a payable deduction.
        // Separate sales-side configurations keep account direction explicit and avoid exposing
        // an AP payable tax accidentally on the customer receipt screen.
        var whtReceivable = await GetOrCreateTaxAsync(tenantId, "WHT-REC-SERV", "WHT Receivable (Services)", 7.5m, TaxApplicability.Sales, TaxCategory.Withholding, false, baseDate, systemUserId);
        var vatWithholdingReceivable = await GetOrCreateTaxAsync(tenantId, "VAT-WHT-REC", "VAT Withholding Receivable", 7.0m, TaxApplicability.Sales, TaxCategory.VatWithholding, false, baseDate, systemUserId);

        var taxPayableAccountId = Guid.Parse("00000005-2200-0000-0000-000000000001");
        var taxReceivableAccountId = Guid.Parse("00000005-1130-0000-0000-000000000001");
        foreach (var purchaseWht in new[] { whtServices, whtGoods, whtWorks })
        {
            // Preserve tenant overrides. These are baseline defaults only for installations that
            // have not yet mapped a statutory control account.
            purchaseWht.TaxPayableAccountId ??= taxPayableAccountId;
        }
        whtReceivable.TaxReceivableAccountId ??= taxReceivableAccountId;
        vatWithholdingReceivable.TaxReceivableAccountId ??= taxReceivableAccountId;

        await _context.SaveChangesAsync();

        // 2. Create Tax Groups
        
        // 2.1 VAT Standard Scheme (Sales)
        // Ghana's active standard scheme excludes the abolished COVID-19 Health Recovery Levy,
        // and VAT is calculated on the base taxable amount rather than compounded on levies.
        var vatGroup = await _context.TaxGroups
            .FirstOrDefaultAsync(g => g.Code == "VAT-STD-SCHEME" && g.TenantId == tenantId);

        if (vatGroup == null)
        {
            vatGroup = new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "VAT-STD-SCHEME",
                Name = "VAT Standard Scheme (15% + Levies)",
                Description = "Standard VAT Scheme including NHIL, GETFund, and VAT on the base taxable amount. COVID-19 Health Recovery Levy is inactive.",
                Applicability = TaxApplicability.Sales,
                IsDefault = true,
                IsActive = true,
                CreatedAt = baseDate,
                CreatedBy = "System"
            };
            
            await _context.TaxGroups.AddAsync(vatGroup);
        }
        else
        {
            vatGroup.Name = "VAT Standard Scheme (15% + Levies)";
            vatGroup.Description = "Standard VAT Scheme including NHIL, GETFund, and VAT on the base taxable amount. COVID-19 Health Recovery Levy is inactive.";
            vatGroup.Applicability = TaxApplicability.Sales;
            vatGroup.IsDefault = true;
            vatGroup.IsActive = true;
            vatGroup.UpdatedAt = DateTime.UtcNow;
            vatGroup.UpdatedBy = "System";
        }

        await EnsureTaxGroupComponentAsync(tenantId, vatGroup, nhil, 1, CompoundBasis.BaseOnly);
        await EnsureTaxGroupComponentAsync(tenantId, vatGroup, getfund, 2, CompoundBasis.BaseOnly);
        await EnsureTaxGroupComponentAsync(tenantId, vatGroup, vatStd, 3, CompoundBasis.BaseOnly);

        var covidComponents = await _context.TaxGroupComponents
            .Where(c => c.TenantId == tenantId && c.TaxGroupId == vatGroup.Id && c.TaxId == covid.Id && !c.IsDeleted)
            .ToListAsync();

        foreach (var component in covidComponents)
        {
            component.IsDeleted = true;
            component.DeletedAt = DateTime.UtcNow;
            component.DeletedBy = "System";
            component.UpdatedAt = DateTime.UtcNow;
            component.UpdatedBy = "System";
        }

        // 2.2 WHT Services (Purchases)
        if (!await _context.TaxGroups.AnyAsync(g => g.Code == "WHT-SERVICES" && g.TenantId == tenantId))
        {
             var whtGroup = new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "WHT-SERVICES",
                Name = "Withholding Tax - Services (7.5%)",
                Applicability = TaxApplicability.Purchases,
                IsDefault = true,
                IsActive = true,
                CreatedAt = baseDate,
                CreatedBy = "System"
            };
            await _context.TaxGroups.AddAsync(whtGroup);
            await _context.TaxGroupComponents.AddAsync(new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = tenantId, TaxId = whtServices.Id, TaxGroupId = whtGroup.Id, CalculationOrder = 1, CompoundBasis = CompoundBasis.BaseOnly });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Tax configuration seeded");
    }

    private async Task<Tax> GetOrCreateTaxAsync(Guid tenantId, string code, string name, decimal rate, TaxApplicability applicability, TaxCategory category, bool isInputDeductible, DateTime baseDate, Guid userId, decimal? threshold = null)
    {
        var tax = await _context.Taxes.FirstOrDefaultAsync(t => t.Code == code && t.TenantId == tenantId);
        if (tax == null)
        {
            tax = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                Rate = rate,
                Applicability = applicability,
                Category = category,
                IsInputTaxDeductible = isInputDeductible,
                ThresholdAmount = threshold,
                IsActive = true,
                EffectiveFrom = baseDate,
                CreatedAt = baseDate,
                CreatedBy = "System"
            };
            await _context.Taxes.AddAsync(tax);
        }
        return tax;
    }

    private async Task EnsureTaxGroupComponentAsync(
        Guid tenantId,
        TaxGroup taxGroup,
        Tax tax,
        int calculationOrder,
        CompoundBasis compoundBasis)
    {
        var component = await _context.TaxGroupComponents
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.TaxGroupId == taxGroup.Id && c.TaxId == tax.Id);

        if (component == null)
        {
            await _context.TaxGroupComponents.AddAsync(new TaxGroupComponent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TaxId = tax.Id,
                TaxGroupId = taxGroup.Id,
                CalculationOrder = calculationOrder,
                CompoundBasis = compoundBasis,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            });
            return;
        }

        component.CalculationOrder = calculationOrder;
        component.CompoundBasis = compoundBasis;
        component.AppliesOnTaxCodes = null;
        component.IsDeleted = false;
        component.DeletedAt = null;
        component.DeletedBy = null;
        component.UpdatedAt = DateTime.UtcNow;
        component.UpdatedBy = "System";
    }

    #endregion

    #region Module Definition Seeding

    private async Task SeedModuleDefinitionsAsync(Guid tenantId, DateTime baseDate)
    {
        var now = DateTime.UtcNow;
        var existing = await _context.ModuleDefinitions
            .IgnoreQueryFilters()
            .Where(module => module.TenantId == tenantId)
            .ToListAsync();
        var currentCodes = FinanceModuleLockCatalog.Definitions
            .Select(module => module.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in FinanceModuleLockCatalog.Definitions)
        {
            var module = existing.FirstOrDefault(item =>
                item.ModuleCode.Equals(definition.Code, StringComparison.OrdinalIgnoreCase));

            if (module == null)
            {
                await _context.ModuleDefinitions.AddAsync(new ModuleDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ModuleCode = definition.Code,
                    ModuleName = definition.Name,
                    Description = definition.Description,
                    IconClass = definition.IconClass,
                    SortOrder = definition.SortOrder,
                    IsActive = true,
                    IsSystem = true,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
                continue;
            }

            module.ModuleCode = definition.Code;
            module.ModuleName = definition.Name;
            module.Description = definition.Description;
            module.IconClass = definition.IconClass;
            module.SortOrder = definition.SortOrder;
            module.IsActive = true;
            module.IsSystem = true;
            module.IsDeleted = false;
            module.DeletedAt = null;
            module.DeletedBy = null;
            module.UpdatedAt = now;
            module.UpdatedBy = "System";
        }

        foreach (var stale in existing.Where(module => !currentCodes.Contains(module.ModuleCode) && module.IsActive))
        {
            stale.IsActive = false;
            stale.UpdatedAt = now;
            stale.UpdatedBy = "System";
        }

        _logger.LogInformation("Finance-integrated top-level module definitions reconciled");
    }

    private async Task SeedTransactionDocumentMappingsAsync(Guid tenantId, DateTime baseDate)
    {
        // Document type alone cannot determine the originating top-level module. For
        // example, an AR invoice may be entered in Finance or generated by Sales.
        // OriginModuleCode on the posting request is now authoritative, so retire the
        // early-stage mappings without deleting historical configuration rows.
        var staleMappings = await _context.TransactionDocumentModuleMappings
            .Where(mapping => mapping.TenantId == tenantId && mapping.IsActive)
            .ToListAsync();

        foreach (var mapping in staleMappings)
        {
            mapping.IsActive = false;
            mapping.UpdatedAt = DateTime.UtcNow;
            mapping.UpdatedBy = "System";
        }

        _logger.LogInformation(
            "Retired {Count} legacy document-type module mappings; posting origin is now explicit",
            staleMappings.Count);
    }

    #endregion
}
