using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
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

            // 6.25 Seed unit-accounting demo drivers used by statistical ledger screens
            await SeedUnitAccountingDemoDataAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 6.5 Seed Payroll GL accounts used by HR payroll posting
            await SeedPayrollAccountsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 7. Seed Fixed Asset Categories
            await SeedFixedAssetCategoriesAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 7.5 Seed Fixed Assets
            await SeedFixedAssetsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 8. Seed Tax Configuration
            await SeedTaxConfigurationAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 9. Seed Finance Settings (must be after accounts for FK references)
            await SeedFinanceSettingsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 10. Seed Module Definitions
            await SeedModuleDefinitionsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            // 11. Seed Document Mappings
            await SeedTransactionDocumentMappingsAsync(tenantId, baseDate);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Finance data seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding finance data");
            throw;
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
                Rate = 0.08m,
                InverseRate = 12.5m, // 1 / 0.08
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
                InverseRate = 13.1579m, // 1 / 0.076
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
                InverseRate = 15.873m, // 1 / 0.063
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
            existing.FiscalYearName = $"Fiscal Year {year}";
            existing.Year = year;
            existing.StartDate = startDate;
            existing.EndDate = endDate;
            existing.TotalDays = DateTime.IsLeapYear(year) ? 366 : 365;
            existing.FiscalYearType = "Calendar";
            existing.Status = status;
            existing.IsActive = !isClosed;
            existing.NumberOfPeriods = 12;
            existing.BaseCurrency = "GHS";
            existing.IsClosed = isClosed;
            existing.IsLocked = isLocked;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = "System";
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

            var status = month < 6 ? "Closed" : month == 6 ? "Open" : "Future";
            await EnsureCalendarMonthPeriodAsync(
                tenantId,
                fy2026Id,
                2026,
                month,
                status,
                isClosed: month < 6,
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
            existing.PeriodName = periodName;
            existing.PeriodCode = $"{startDate:yyyy-MM}";
            existing.StartDate = startDate;
            existing.EndDate = endDate;
            existing.Status = status;
            existing.PeriodStatus = status;
            existing.IsOpen = string.Equals(status, "Open", StringComparison.OrdinalIgnoreCase);
            existing.IsClosed = isClosed;
            existing.IsLocked = isLocked;
            existing.PeriodDays = (endDate.Date - startDate.Date).Days + 1;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = "System";
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
        // Target client COA shape restored from historical seed: DEPT-ACCT-PROJ.
        var deptSegment = await EnsureSegmentStructureAsync(
            tenantId,
            preferredId: Guid.Parse("00000004-0001-0001-0001-000000000002"),
            segmentName: "Department",
            segmentCode: "DEPT",
            segmentPosition: 1,
            segmentLength: 3,
            dataType: "Alphanumeric",
            separatorCharacter: "-",
            lookupTableRequired: true,
            isMandatory: true,
            isReportingDimension: true,
            isNaturalAccount: false,
            description: "Functional Department",
            baseDate: baseDate);

        var depts = new[]
        {
            new { Val = "000", Desc = "General / No Department" },
            new { Val = "100", Desc = "Finance & Administration" },
            new { Val = "200", Desc = "Estates Management" },
            new { Val = "300", Desc = "Development & Engineering" },
            new { Val = "400", Desc = "Legal" },
            new { Val = "500", Desc = "Corporate Planning & Communication" },
            new { Val = "600", Desc = "Internal Audit" }
        };

        var order = 1;
        foreach (var dept in depts)
        {
            await EnsureSegmentLookupValueAsync(tenantId, deptSegment.Id, dept.Val, dept.Desc, order++, baseDate);
        }

        var accountSegment = await EnsureSegmentStructureAsync(
            tenantId,
            preferredId: Guid.Parse("00000004-0001-0001-0001-000000000003"),
            segmentName: "Natural Account",
            segmentCode: "ACCT",
            segmentPosition: 2,
            segmentLength: 4,
            dataType: "Numeric",
            separatorCharacter: "-",
            lookupTableRequired: false,
            isMandatory: true,
            isReportingDimension: true,
            isNaturalAccount: true,
            description: "Natural GL Account",
            baseDate: baseDate);

        var projectSegment = await EnsureSegmentStructureAsync(
            tenantId,
            preferredId: Guid.Parse("00000004-0001-0001-0001-000000000004"),
            segmentName: "Project",
            segmentCode: "PROJ",
            segmentPosition: 3,
            segmentLength: 4,
            dataType: "Alphanumeric",
            separatorCharacter: null,
            lookupTableRequired: true,
            isMandatory: true,
            isReportingDimension: true,
            isNaturalAccount: false,
            description: "Capital/Construction Project",
            baseDate: baseDate);

        var projects = new[]
        {
            new { Val = "0000", Desc = "No Project" },
            new { Val = "P101", Desc = "Community 26 Kpone Affordable Housing" },
            new { Val = "P102", Desc = "Oxygen City Housing Project (Ho)" },
            new { Val = "P103", Desc = "Kaiser Flats Redevelopment" },
            new { Val = "P104", Desc = "Tema 5,000-Capacity Event Center" }
        };

        order = 1;
        foreach (var project in projects)
        {
            await EnsureSegmentLookupValueAsync(tenantId, projectSegment.Id, project.Val, project.Desc, order++, baseDate);
        }

        await DeactivateLegacySegmentAsync(tenantId, "FUND", "FUND");
        await DeactivateLegacySegmentAsync(tenantId, "TEST", "TEST SEGMENT");

        await _context.SaveChangesAsync();
        _logger.LogInformation("Account segments seeded");

        async Task EnsureSegmentLookupValueAsync(
            Guid lookupTenantId,
            Guid segmentStructureId,
            string segmentValue,
            string description,
            int displayOrder,
            DateTime effectiveDate)
        {
            var existing = await _context.SegmentLookupValues
                .FirstOrDefaultAsync(v =>
                    v.TenantId == lookupTenantId &&
                    v.SegmentStructureId == segmentStructureId &&
                    v.SegmentValue == segmentValue);

            if (existing == null)
            {
                await _context.SegmentLookupValues.AddAsync(new SegmentLookupValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = lookupTenantId,
                    SegmentStructureId = segmentStructureId,
                    SegmentValue = segmentValue,
                    Description = description,
                    DisplayOrder = displayOrder,
                    EffectiveDate = effectiveDate,
                    IsActive = true,
                    CreatedAt = effectiveDate,
                    CreatedBy = "System"
                });
                return;
            }

            existing.Description = description;
            existing.DisplayOrder = displayOrder;
            existing.EffectiveDate = existing.EffectiveDate == default ? effectiveDate : existing.EffectiveDate;
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = "System";
        }

        async Task DeactivateLegacySegmentAsync(Guid legacyTenantId, string segmentCode, string segmentName)
        {
            var code = segmentCode.ToUpper();
            var name = segmentName.ToUpper();
            var legacySegment = await _context.AccountSegmentStructures
                .FirstOrDefaultAsync(s =>
                    s.TenantId == legacyTenantId &&
                    s.IsActive &&
                    ((s.SegmentCode != null && s.SegmentCode.ToUpper() == code)
                     || (s.SegmentName != null && s.SegmentName.ToUpper() == name)));

            if (legacySegment == null)
            {
                return;
            }

            legacySegment.IsActive = false;
            legacySegment.UpdatedAt = DateTime.UtcNow;
            legacySegment.UpdatedBy = "System";
            _logger.LogInformation("Deactivated legacy segment structure {SegmentCode} for tenant {TenantId}", legacySegment.SegmentCode, legacyTenantId);
        }
    }

    private async Task<AccountSegmentStructure> EnsureSegmentStructureAsync(
        Guid tenantId,
        Guid preferredId,
        string segmentName,
        string segmentCode,
        int segmentPosition,
        int segmentLength,
        string dataType,
        string? separatorCharacter,
        bool lookupTableRequired,
        bool isMandatory,
        bool isReportingDimension,
        bool isNaturalAccount,
        string description,
        DateTime baseDate)
    {
        var normalizedSegmentCode = segmentCode.ToUpperInvariant();
        var normalizedSegmentName = segmentName.ToUpperInvariant();

        var existing = await _context.AccountSegmentStructures
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId &&
                (
                    (isNaturalAccount && s.IsNaturalAccount)
                    || (s.SegmentCode != null && s.SegmentCode.ToUpper() == normalizedSegmentCode)
                    || (s.SegmentName != null && s.SegmentName.ToUpper() == normalizedSegmentName)
                ));

        if (existing != null)
        {
            existing.SegmentName = segmentName;
            existing.SegmentCode = segmentCode;
            existing.SegmentPosition = segmentPosition;
            existing.SegmentLength = segmentLength;
            existing.DataType = dataType;
            existing.SeparatorCharacter = separatorCharacter;
            existing.LookupTableRequired = lookupTableRequired;
            existing.IsMandatory = isMandatory;
            existing.IsReportingDimension = isReportingDimension;
            existing.IsNaturalAccount = isNaturalAccount;
            existing.IsActive = true;
            existing.Description = description;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = "System";
            return existing;
        }

        var idInUse = await _context.AccountSegmentStructures.AnyAsync(s => s.Id == preferredId);
        var newId = idInUse ? Guid.NewGuid() : preferredId;
        var created = new AccountSegmentStructure
        {
            Id = newId,
            TenantId = tenantId,
            SegmentName = segmentName,
            SegmentCode = segmentCode,
            SegmentPosition = segmentPosition,
            SegmentLength = segmentLength,
            DataType = dataType,
            SeparatorCharacter = separatorCharacter,
            LookupTableRequired = lookupTableRequired,
            IsMandatory = isMandatory,
            IsReportingDimension = isReportingDimension,
            IsNaturalAccount = isNaturalAccount,
            IsActive = true,
            Description = description,
            CreatedAt = baseDate,
            CreatedBy = "System"
        };

        await _context.AccountSegmentStructures.AddAsync(created);
        return created;
    }

    #endregion

    #region Account Seeding

    private async Task SeedAccountsAsync(Guid tenantId, DateTime baseDate)
    {
        // Get Segments
        var deptSegment = await _context.AccountSegmentStructures.FirstOrDefaultAsync(s => s.SegmentCode == "DEPT" && s.TenantId == tenantId && s.IsActive);
        var acctSegment = await _context.AccountSegmentStructures.FirstOrDefaultAsync(s => s.IsNaturalAccount && s.TenantId == tenantId);
        var projectSegment = await _context.AccountSegmentStructures.FirstOrDefaultAsync(s => s.SegmentCode == "PROJ" && s.TenantId == tenantId && s.IsActive);

        if (deptSegment == null || acctSegment == null || projectSegment == null)
        {
            _logger.LogError("Segments not found during account seeding. Ensure SeedAccountSegmentsAsync runs first.");
            return;
        }

        // Get Default Values
        var deptValue = await _context.SegmentLookupValues.FirstOrDefaultAsync(v => v.SegmentStructureId == deptSegment.Id && v.SegmentValue == "000");
        var projectValue = await _context.SegmentLookupValues.FirstOrDefaultAsync(v => v.SegmentStructureId == projectSegment.Id && v.SegmentValue == "0000");

        var standardAccounts = GetStandardChartOfAccounts(tenantId, baseDate);
        
        // Filter out accounts that already exist
        var existingAccountIds = await _context.Accounts
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Id)
            .ToListAsync();
            
        var newAccounts = standardAccounts.Where(a => !existingAccountIds.Contains(a.Id)).ToList();

        if (!newAccounts.Any())
        {
            _logger.LogInformation("All standard accounts already exist. Skipping.");
            return;
        }

        foreach (var account in newAccounts)
        {
            // Apply Segmentation
            account.IsSegmented = true;
            account.AccountNumber = $"000-{account.AccountCode}-0000"; // Dept-Account-Project
            
            // Create Segment Values
            account.SegmentValues = new List<AccountSegmentValue>
            {
                // Segment 1: Department (000)
                new AccountSegmentValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SegmentStructureId = deptSegment.Id,
                    SegmentPosition = 1,
                    SegmentValue = "000",
                    SegmentLookupValueId = deptValue?.Id,
                    SegmentValueDescription = deptValue?.Description ?? "General / No Department",
                    EffectiveDate = baseDate,
                    IsLocked = false,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                },

                // Segment 2: Natural Account (Code)
                new AccountSegmentValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SegmentStructureId = acctSegment.Id,
                    SegmentPosition = 2,
                    SegmentValue = account.AccountCode,
                    SegmentLookupValueId = null, // Natural account usually doesn't have lookup, or lookup IS the account list
                    SegmentValueDescription = account.AccountName,
                    EffectiveDate = baseDate,
                    IsLocked = false,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                },

                // Segment 3: Project (0000)
                new AccountSegmentValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SegmentStructureId = projectSegment.Id,
                    SegmentPosition = 3,
                    SegmentValue = "0000",
                    SegmentLookupValueId = projectValue?.Id,
                    SegmentValueDescription = projectValue?.Description ?? "No Project",
                    EffectiveDate = baseDate,
                    IsLocked = false,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                }
            };
        }

        await _context.Accounts.AddRangeAsync(newAccounts);
        await _context.SaveChangesAsync(); // Explicitly save changes here
        
        _logger.LogInformation($"Seeded {newAccounts.Count} new accounts with segmentation");
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

            existing.AccountNumber = account.AccountNumber;
            existing.AccountName = account.AccountName;
            existing.AccountType = account.AccountType;
            existing.AccountCategory = account.AccountCategory;
            existing.AccountSubCategory = account.AccountSubCategory;
            existing.Description = account.Description;
            existing.CurrencyCode = account.CurrencyCode;
            existing.IsMultiCurrency = account.IsMultiCurrency;
            existing.IsSegmented = account.IsSegmented;
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
                Balance = 250000m,
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
                Balance = 185000m,
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
                Balance = 320000m,
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
                Balance = 1500000m,
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
                Balance = 800000m,
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
                Balance = 450000m,
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
                Balance = 250000m,
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
                Balance = 300000m,
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
                Balance = 0m,
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
                Balance = 0m,
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
                Balance = 125000m,
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
                Balance = 45000m,
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
                Balance = 0m,
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
                Balance = 0m,
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
                Balance = 500000m,
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
                Balance = 1000000m,
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
                Balance = 435000m,
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
                Balance = 850000m,
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
                Balance = 320000m,
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
                Balance = 0m,
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
                Balance = 25000m,
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
                Balance = 0m,
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
                Balance = 12500m,
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
                Balance = 0m,
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
                Balance = 8200m,
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
                Balance = 0m,
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
                Balance = 420000m,
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
                Balance = 280000m,
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
                Balance = 60000m,
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
                Balance = 18000m,
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
                Balance = 75000m,
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
                Balance = 45000m,
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
                Balance = 32000m,
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

    #region Fixed Assets Seeding

    private async Task SeedFixedAssetsAsync(Guid tenantId, DateTime baseDate)
    {
        if (await _context.FixedAssets.AnyAsync(a => a.TenantId == tenantId))
        {
            _logger.LogInformation("Fixed assets already exist. Skipping.");
            return;
        }

        // Get categories to link
        var categories = await _context.FixedAssetCategories
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(c => c.Code, c => c.Id);

        if (!categories.Any())
        {
            _logger.LogWarning("No fixed asset categories found. Skipping fixed asset seeding.");
            return;
        }

        var assets = new List<FixedAsset>();
        var systemUser = "System";

        // 1. Building Asset
        if (categories.TryGetValue("FA-BLDG", out var buildingCategoryId))
        {
            assets.Add(new FixedAsset
            {
                TenantId = tenantId,
                FixedAssetCategoryId = buildingCategoryId,
                AssetCode = "FA-2024-BLDG-001",
                Name = "Headquarters Building",
                Description = "Main office building in Accra",
                PurchaseDate = baseDate.AddYears(-2),
                PlacedInServiceDate = baseDate.AddYears(-2).AddDays(30),
                PurchasePrice = 1200000m,
                InstallationCost = 50000m,
                TaxAmount = 0m,
                AcquisitionCost = 1250000m,
                NetBookValue = 1125000m, // Roughly 2 years depreciation
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationConvention = DepreciationConvention.FullMonth,
                UsefulLifeMonths = 240, // 20 years
                ResidualValue = 50000m,
                Status = FixedAssetStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = systemUser
            });
        }

        // 2. Equipment Asset
        if (categories.TryGetValue("FA-EQP", out var equipmentCategoryId))
        {
            assets.Add(new FixedAsset
            {
                TenantId = tenantId,
                FixedAssetCategoryId = equipmentCategoryId,
                AssetCode = "FA-2024-EQP-001",
                Name = "Industrial Generator",
                Description = "Backup power generator 500kVA",
                PurchaseDate = baseDate.AddMonths(-6),
                PlacedInServiceDate = baseDate.AddMonths(-6).AddDays(5),
                PurchasePrice = 150000m,
                InstallationCost = 10000m,
                TaxAmount = 0m,
                AcquisitionCost = 160000m,
                NetBookValue = 144000m,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationConvention = DepreciationConvention.FullMonth,
                UsefulLifeMonths = 60, // 5 years
                ResidualValue = 10000m,
                SerialNumber = "GEN-500K-9988",
                Status = FixedAssetStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = systemUser
            });

            assets.Add(new FixedAsset
            {
                TenantId = tenantId,
                FixedAssetCategoryId = equipmentCategoryId,
                AssetCode = "FA-2024-EQP-002",
                Name = "Server Rack System",
                Description = "Main datacenter server rack",
                PurchaseDate = baseDate.AddMonths(-1),
                PlacedInServiceDate = baseDate.AddMonths(-1).AddDays(2),
                PurchasePrice = 45000m,
                InstallationCost = 5000m,
                TaxAmount = 0m,
                AcquisitionCost = 50000m,
                NetBookValue = 49166.67m,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationConvention = DepreciationConvention.FullMonth,
                UsefulLifeMonths = 60,
                ResidualValue = 0m,
                SerialNumber = "SRV-RCK-1122",
                Status = FixedAssetStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = systemUser
            });
        }

        // 3. Vehicle Asset
        if (categories.TryGetValue("FA-VEH", out var vehicleCategoryId))
        {
            assets.Add(new FixedAsset
            {
                TenantId = tenantId,
                FixedAssetCategoryId = vehicleCategoryId,
                AssetCode = "FA-2024-VEH-001",
                Name = "Delivery Truck - Toyota Hilux",
                Description = "Main operations delivery vehicle",
                PurchaseDate = baseDate.AddYears(-1),
                PlacedInServiceDate = baseDate.AddYears(-1).AddDays(14),
                PurchasePrice = 250000m,
                InstallationCost = 0m,
                TaxAmount = 0m,
                AcquisitionCost = 250000m,
                NetBookValue = 187500m,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationConvention = DepreciationConvention.FullMonth,
                UsefulLifeMonths = 48, // 4 years
                ResidualValue = 25000m,
                SerialNumber = "VIN-TOY-HLX-4455",
                Status = FixedAssetStatus.Active,
                CreatedAt = baseDate,
                CreatedBy = systemUser
            });
        }

        if (assets.Any())
        {
            await _context.FixedAssets.AddRangeAsync(assets);
            _logger.LogInformation($"Seeded {assets.Count} fixed assets");
        }
    }

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
            OpeningBalanceAutoRoutingEnabled = true,
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
