using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
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
        // FY2023 (Closed)
        var fy2023Id = Guid.Parse("00000003-0001-0001-0001-000000000001");
        if (!await _context.FiscalYears.AnyAsync(fy => fy.FiscalYearCode == "FY2023" && fy.TenantId == tenantId))
        {
            await _context.FiscalYears.AddAsync(new FiscalYear
            {
                Id = fy2023Id,
                TenantId = tenantId,
                FiscalYearCode = "FY2023",
                FiscalYearName = "Fiscal Year 2023",
                Year = 2023,
                StartDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2023, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                FiscalYearType = "Calendar",
                Status = "Closed",
                NumberOfPeriods = 12,
                BaseCurrency = "GHS",
                IsClosed = true,
                IsLocked = true,
                CreatedAt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "System"
            });
        }

        // FY2024 (Open)
        var fy2024Id = Guid.Parse("00000003-0001-0001-0001-000000000002");
        if (!await _context.FiscalYears.AnyAsync(fy => fy.FiscalYearCode == "FY2024" && fy.TenantId == tenantId))
        {
            await _context.FiscalYears.AddAsync(new FiscalYear
            {
                Id = fy2024Id,
                TenantId = tenantId,
                FiscalYearCode = "FY2024",
                FiscalYearName = "Fiscal Year 2024",
                Year = 2024,
                StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                FiscalYearType = "Calendar",
                Status = "Open",
                NumberOfPeriods = 12,
                BaseCurrency = "GHS",
                IsClosed = false,
                IsLocked = false,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Fiscal years seeded");
    }

    #endregion

    #region Fiscal Period Seeding

    private async Task SeedFiscalPeriodsAsync(Guid tenantId, DateTime baseDate)
    {
        var fy2024Id = Guid.Parse("00000003-0001-0001-0001-000000000002");

        // January 2024 (Closed)
        if (!await _context.FiscalPeriods.AnyAsync(fp => fp.PeriodNumber == 1 && fp.FiscalYearId == fy2024Id))
        {
            await _context.FiscalPeriods.AddAsync(new FiscalPeriod
            {
                Id = Guid.Parse("00000004-0001-0001-0001-000000000001"),
                TenantId = tenantId,
                FiscalYearId = fy2024Id,
                PeriodNumber = 1,
                PeriodName = "January 2024",
                StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc),
                Status = "Closed",
                IsClosed = true,
                IsLocked = false,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        // November 2024 (Closed)
        if (!await _context.FiscalPeriods.AnyAsync(fp => fp.PeriodNumber == 11 && fp.FiscalYearId == fy2024Id))
        {
            await _context.FiscalPeriods.AddAsync(new FiscalPeriod
            {
                Id = Guid.Parse("00000004-0001-0001-0001-000000000011"),
                TenantId = tenantId,
                FiscalYearId = fy2024Id,
                PeriodNumber = 11,
                PeriodName = "November 2024",
                StartDate = new DateTime(2024, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2024, 11, 30, 23, 59, 59, DateTimeKind.Utc),
                Status = "Closed",
                IsClosed = true,
                IsLocked = true,
                CreatedAt = new DateTime(2024, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "System"
            });
        }

        // December 2024 (Open)
        if (!await _context.FiscalPeriods.AnyAsync(fp => fp.PeriodNumber == 12 && fp.FiscalYearId == fy2024Id))
        {
            await _context.FiscalPeriods.AddAsync(new FiscalPeriod
            {
                Id = Guid.Parse("00000004-0001-0001-0001-000000000012"),
                TenantId = tenantId,
                FiscalYearId = fy2024Id,
                PeriodNumber = 12,
                PeriodName = "December 2024",
                StartDate = new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                Status = "Open",
                IsClosed = false,
                IsLocked = false,
                CreatedAt = new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Fiscal periods seeded");
    }

    #endregion

    #region Account Segment Seeding

    private async Task SeedAccountSegmentsAsync(Guid tenantId, DateTime baseDate)
    {
        // 1. Fund Segment (Pos 1)
        var fundSegmentId = Guid.Parse("00000004-0001-0001-0001-000000000001");
        if (!await _context.AccountSegmentStructures.AnyAsync(s => s.SegmentName == "Fund" && s.TenantId == tenantId))
        {
            await _context.AccountSegmentStructures.AddAsync(new AccountSegmentStructure
            {
                Id = fundSegmentId,
                TenantId = tenantId,
                SegmentName = "Fund",
                SegmentCode = "FUND",
                SegmentPosition = 1,
                SegmentLength = 3,
                DataType = "Alphanumeric",
                SeparatorCharacter = "-",
                LookupTableRequired = true,
                IsMandatory = true,
                IsReportingDimension = true,
                IsNaturalAccount = false,
                IsActive = true,
                Description = "Fund/Entity Identifier",
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
            
            // Seed Fund Lookup Values
            await _context.SegmentLookupValues.AddAsync(new SegmentLookupValue
            {
                Id = Guid.Parse("00000004-0002-0001-0001-000000000001"),
                TenantId = tenantId,
                SegmentStructureId = fundSegmentId,
                SegmentValue = "001",
                Description = "General Fund",
                DisplayOrder = 1,
                EffectiveDate = baseDate,
                IsActive = true,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        // 2. Department Segment (Pos 2)
        var deptSegmentId = Guid.Parse("00000004-0001-0001-0001-000000000002");
        if (!await _context.AccountSegmentStructures.AnyAsync(s => s.SegmentName == "Department" && s.TenantId == tenantId))
        {
            await _context.AccountSegmentStructures.AddAsync(new AccountSegmentStructure
            {
                Id = deptSegmentId,
                TenantId = tenantId,
                SegmentName = "Department",
                SegmentCode = "DEPT",
                SegmentPosition = 2,
                SegmentLength = 3,
                DataType = "Alphanumeric",
                SeparatorCharacter = "-",
                LookupTableRequired = true,
                IsMandatory = true,
                IsReportingDimension = true,
                IsNaturalAccount = false,
                IsActive = true,
                Description = "Cost Center/Department",
                CreatedAt = baseDate,
                CreatedBy = "System"
            });

            // Seed Department Lookup Values
            var depts = new[]
            {
                new { Val = "000", Desc = "No Department" },
                new { Val = "100", Desc = "Administration" },
                new { Val = "200", Desc = "Sales & Marketing" },
                new { Val = "300", Desc = "Operations" },
                new { Val = "400", Desc = "Human Resources" },
                new { Val = "500", Desc = "Finance" }
            };

            int order = 1;
            foreach (var dept in depts)
            {
                await _context.SegmentLookupValues.AddAsync(new SegmentLookupValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SegmentStructureId = deptSegmentId,
                    SegmentValue = dept.Val,
                    Description = dept.Desc,
                    DisplayOrder = order++,
                    EffectiveDate = baseDate,
                    IsActive = true,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        // 3. Natural Account Segment (Pos 3)
        var accountSegmentId = Guid.Parse("00000004-0001-0001-0001-000000000003");
        if (!await _context.AccountSegmentStructures.AnyAsync(s => s.IsNaturalAccount && s.TenantId == tenantId))
        {
            await _context.AccountSegmentStructures.AddAsync(new AccountSegmentStructure
            {
                Id = accountSegmentId,
                TenantId = tenantId,
                SegmentName = "Natural Account",
                SegmentCode = "ACCT",
                SegmentPosition = 3,
                SegmentLength = 4,
                DataType = "Numeric",
                SeparatorCharacter = null, // Last segment
                LookupTableRequired = false, // Derived from AccountCode
                IsMandatory = true,
                IsReportingDimension = true,
                IsNaturalAccount = true,
                IsActive = true,
                Description = "Natural GL Account",
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
            
            // Seed Natural Account Lookup Values (Standard COA subset)
            var naturalAccounts = new[]
            {
                // Assets
                new { Val = "1000", Desc = "Cash and Cash Equivalents" },
                new { Val = "1100", Desc = "Accounts Receivable" },
                new { Val = "1200", Desc = "Inventory" },
                new { Val = "1300", Desc = "Prepaid Expenses" },
                new { Val = "1500", Desc = "Property, Plant and Equipment" },
                
                // Liabilities
                new { Val = "2000", Desc = "Accounts Payable" },
                new { Val = "2100", Desc = "Accrued Liabilities" },
                new { Val = "2200", Desc = "Short-Term Loans" },
                
                // Equity
                new { Val = "3000", Desc = "Share Capital" },
                new { Val = "3100", Desc = "Retained Earnings" },
                
                // Revenue
                new { Val = "4000", Desc = "Sales Revenue" },
                new { Val = "4100", Desc = "Service Revenue" },
                new { Val = "4900", Desc = "Other Income" },
                
                // Expenses
                new { Val = "5000", Desc = "Cost of Goods Sold" },
                new { Val = "6000", Desc = "Salaries and Wages" },
                new { Val = "6100", Desc = "Rent Expense" },
                new { Val = "6200", Desc = "Utilities Expense" },
                new { Val = "6300", Desc = "Depreciation Expense" },
                new { Val = "6400", Desc = "Marketing and Advertising" },
                new { Val = "8000", Desc = "Income Tax Expense" }
            };

            int acctOrder = 1;
            foreach (var acct in naturalAccounts)
            {
                await _context.SegmentLookupValues.AddAsync(new SegmentLookupValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SegmentStructureId = accountSegmentId,
                    SegmentValue = acct.Val,
                    Description = acct.Desc,
                    DisplayOrder = acctOrder++,
                    EffectiveDate = baseDate,
                    IsActive = true,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }
        else
        {
            // If segment already exists, check if values need seeding (for existing deployments)
            var existingSegment = await _context.AccountSegmentStructures
                .FirstOrDefaultAsync(s => s.IsNaturalAccount && s.TenantId == tenantId);
            
        if (existingSegment != null)
            {
                // Ensure LookupTableRequired is true if we are managing values distinct from accounts
                // existingSegment.LookupTableRequired = true; 

                // 1. Cleanup Duplicates
                var existingValues = await _context.SegmentLookupValues
                    .Where(v => v.SegmentStructureId == existingSegment.Id)
                    .ToListAsync();

                var duplicates = existingValues
                    .GroupBy(v => v.SegmentValue)
                    .Where(g => g.Count() > 1);

                if (duplicates.Any())
                {
                    _logger.LogWarning("Found duplicate natural account values. Cleaning up...");
                    foreach (var group in duplicates)
                    {
                        // Keep the one with the earliest creation date, or just the first one
                        var toRemove = group.OrderBy(v => v.CreatedAt).Skip(1);
                        _context.SegmentLookupValues.RemoveRange(toRemove);
                    }
                    await _context.SaveChangesAsync();
                    existingValues = await _context.SegmentLookupValues
                        .Where(v => v.SegmentStructureId == existingSegment.Id)
                        .ToListAsync(); // Refresh list
                }

                // 2. Seed Missing Values (Idempotent)
                var naturalAccounts = new[]
                {
                    // Assets
                    new { Val = "1000", Desc = "Cash and Cash Equivalents" },
                    new { Val = "1100", Desc = "Accounts Receivable" },
                    new { Val = "1200", Desc = "Inventory" },
                    new { Val = "1300", Desc = "Prepaid Expenses" },
                    new { Val = "1500", Desc = "Property, Plant and Equipment" },
                    
                    // Liabilities
                    new { Val = "2000", Desc = "Accounts Payable" },
                    new { Val = "2100", Desc = "Accrued Liabilities" },
                    new { Val = "2200", Desc = "Short-Term Loans" },
                    
                    // Equity
                    new { Val = "3000", Desc = "Share Capital" },
                    new { Val = "3100", Desc = "Retained Earnings" },
                    
                    // Revenue
                    new { Val = "4000", Desc = "Sales Revenue" },
                    new { Val = "4100", Desc = "Service Revenue" },
                    new { Val = "4900", Desc = "Other Income" },
                    
                    // Expenses
                    new { Val = "5000", Desc = "Cost of Goods Sold" },
                    new { Val = "6000", Desc = "Salaries and Wages" },
                    new { Val = "6100", Desc = "Rent Expense" },
                    new { Val = "6200", Desc = "Utilities Expense" },
                    new { Val = "6300", Desc = "Depreciation Expense" },
                    new { Val = "6400", Desc = "Marketing and Advertising" },
                    new { Val = "8000", Desc = "Income Tax Expense" }
                };

                int acctOrder = 1;
                foreach (var acct in naturalAccounts)
                {
                    if (!existingValues.Any(v => v.SegmentValue == acct.Val))
                    {
                        await _context.SegmentLookupValues.AddAsync(new SegmentLookupValue
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            SegmentStructureId = existingSegment.Id,
                            SegmentValue = acct.Val,
                            Description = acct.Desc,
                            DisplayOrder = acctOrder, // Note: Order might be non-sequential if mixed, but acceptable
                            EffectiveDate = baseDate,
                            IsActive = true,
                            CreatedAt = baseDate,
                            CreatedBy = "System"
                        });
                    }
                    acctOrder++;
                }
            }
        }
        
        await _context.SaveChangesAsync();
        _logger.LogInformation("Account segments seeded");
    }

    #endregion

    #region Account Seeding

    private async Task SeedAccountsAsync(Guid tenantId, DateTime baseDate)
    {
        // Get Segments
        var fundSegment = await _context.AccountSegmentStructures.FirstOrDefaultAsync(s => s.SegmentName == "Fund" && s.TenantId == tenantId);
        var deptSegment = await _context.AccountSegmentStructures.FirstOrDefaultAsync(s => s.SegmentName == "Department" && s.TenantId == tenantId);
        var acctSegment = await _context.AccountSegmentStructures.FirstOrDefaultAsync(s => s.IsNaturalAccount && s.TenantId == tenantId);

        if (fundSegment == null || deptSegment == null || acctSegment == null)
        {
            _logger.LogError("Segments not found during account seeding. Ensure SeedAccountSegmentsAsync runs first.");
            return;
        }

        // Get Default Values
        var fundValue = await _context.SegmentLookupValues.FirstOrDefaultAsync(v => v.SegmentStructureId == fundSegment.Id && v.SegmentValue == "001");
        var deptValue = await _context.SegmentLookupValues.FirstOrDefaultAsync(v => v.SegmentStructureId == deptSegment.Id && v.SegmentValue == "000");

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
            account.AccountNumber = $"001-000-{account.AccountCode}"; // Fund-Dept-Account
            
            // Create Segment Values
            account.SegmentValues = new List<AccountSegmentValue>
            {
                // Segment 1: Fund (001)
                new AccountSegmentValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SegmentStructureId = fundSegment.Id,
                    SegmentPosition = 1,
                    SegmentValue = "001",
                    SegmentLookupValueId = fundValue?.Id,
                    SegmentValueDescription = fundValue?.Description ?? "General Fund",
                    EffectiveDate = baseDate,
                    IsLocked = false,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                },
                
                // Segment 2: Department (000)
                new AccountSegmentValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SegmentStructureId = deptSegment.Id,
                    SegmentPosition = 2,
                    SegmentValue = "000",
                    SegmentLookupValueId = deptValue?.Id,
                    SegmentValueDescription = deptValue?.Description ?? "No Department",
                    EffectiveDate = baseDate,
                    IsLocked = false,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                },

                // Segment 3: Natural Account (Code)
                new AccountSegmentValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SegmentStructureId = acctSegment.Id,
                    SegmentPosition = 3,
                    SegmentValue = account.AccountCode,
                    SegmentLookupValueId = null, // Natural account usually doesn't have lookup, or lookup IS the account list
                    SegmentValueDescription = account.AccountName,
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
                Id = Guid.Parse("00000005-7100-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "7100",
                AccountNumber = "7100",
                AccountName = "Unrealized Exchange Gain/Loss",
                AccountType = AccountType.Revenue,
                AccountCategory = "Other Income",
                Description = "Unrealized foreign exchange gains and losses from multi-currency revaluation",
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
                Id = Guid.Parse("00000005-7200-0000-0000-000000000001"),
                TenantId = tenantId,
                AccountCode = "7200",
                AccountNumber = "7200",
                AccountName = "Realized Exchange Gain/Loss",
                AccountType = AccountType.Revenue,
                AccountCategory = "Other Income",
                Description = "Realized foreign exchange gains and losses from settled transactions",
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
        var existingSettings = await _context.FinanceSettings.FirstOrDefaultAsync(fs => fs.TenantId == tenantId);
        
        if (existingSettings != null)
        {
            if (existingSettings.CoaType != "Segmented")
            {
                _logger.LogInformation($"Updating existing Finance Settings COA Type from {existingSettings.CoaType} to Segmented");
                existingSettings.CoaType = "Segmented";
                // Ensure other critical fields match expected default if needed, or leave them.
                // For now, just fixing the COA Type is key.
            }
            else
            {
                _logger.LogInformation("Finance settings already exist and correct. Skipping settings seeding.");
            }
            return;
        }

        await _context.FinanceSettings.AddAsync(new FinanceSettings
        {
            Id = Guid.Parse("00000006-0001-0001-0001-000000000001"),
            TenantId = tenantId,
            CoaType = "Segmented",
            CoaConfigurationLocked = false,
            BaseCurrency = "GHS",
            AccountSeparator = "-",
            RetainedEarningsAccountId = Guid.Parse("00000005-3100-0000-0000-000000000001"),
            UnrealizedGainLossAccountId = Guid.Parse("00000005-7100-0000-0000-000000000001"),
            RealizedGainLossAccountId = Guid.Parse("00000005-7200-0000-0000-000000000001"),
            SuspenseAccountId = Guid.Parse("00000005-9999-0000-0000-000000000001"),
            CreatedAt = baseDate,
            CreatedBy = "System"
        });

        _logger.LogInformation("Finance settings seeded");
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
        
        // 1.3 COVID-19 (1%)
        var covid = await GetOrCreateTaxAsync(tenantId, "COVID19", "COVID-19 Health Recovery Levy", 1.0m, TaxApplicability.Sales, TaxCategory.Standard, false, baseDate, systemUserId);
        
        // 1.4 VAT Standard (15%)
        var vatStd = await GetOrCreateTaxAsync(tenantId, "VAT-STD", "Value Added Tax (Standard)", 15.0m, TaxApplicability.Sales, TaxCategory.Standard, true, baseDate, systemUserId);
        
        // 1.5 WHT 7.5% (Services)
        var whtServices = await GetOrCreateTaxAsync(tenantId, "WHT-SERV", "Withholding Tax (Services)", 7.5m, TaxApplicability.Purchases, TaxCategory.Withholding, false, baseDate, systemUserId, 2000m);
        
        // 1.6 WHT 3% (Goods)
        var whtGoods = await GetOrCreateTaxAsync(tenantId, "WHT-GOODS", "Withholding Tax (Goods)", 3.0m, TaxApplicability.Purchases, TaxCategory.Withholding, false, baseDate, systemUserId, 2000m);

        await _context.SaveChangesAsync();

        // 2. Create Tax Groups
        
        // 2.1 VAT Standard Scheme (Sales)
        if (!await _context.TaxGroups.AnyAsync(g => g.Code == "VAT-STD-SCHEME" && g.TenantId == tenantId))
        {
            var vatGroup = new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "VAT-STD-SCHEME",
                Name = "VAT Standard Scheme (15% + Levies)",
                Description = "Standard VAT Scheme including NHIL, GETFund, and COVID-19 Levy",
                Applicability = TaxApplicability.Sales,
                IsDefault = true,
                IsActive = true,
                CreatedAt = baseDate,
                CreatedBy = "System"
            };
            
            await _context.TaxGroups.AddAsync(vatGroup);
            
            // Add Components
            // NHIL - Base
            await _context.TaxGroupComponents.AddAsync(new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = tenantId, TaxId = nhil.Id, TaxGroupId = vatGroup.Id, CalculationOrder = 1, CompoundBasis = CompoundBasis.BaseOnly });
            // GETFund - Base
            await _context.TaxGroupComponents.AddAsync(new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = tenantId, TaxId = getfund.Id, TaxGroupId = vatGroup.Id, CalculationOrder = 2, CompoundBasis = CompoundBasis.BaseOnly });
            // COVID - Base
            await _context.TaxGroupComponents.AddAsync(new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = tenantId, TaxId = covid.Id, TaxGroupId = vatGroup.Id, CalculationOrder = 3, CompoundBasis = CompoundBasis.BaseOnly });
            // VAT - Compound (Cumulative)
            await _context.TaxGroupComponents.AddAsync(new TaxGroupComponent { Id = Guid.NewGuid(), TenantId = tenantId, TaxId = vatStd.Id, TaxGroupId = vatGroup.Id, CalculationOrder = 4, CompoundBasis = CompoundBasis.Cumulative });
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

    #endregion

    #region Module Definition Seeding

    private async Task SeedModuleDefinitionsAsync(Guid tenantId, DateTime baseDate)
    {
        var modules = new[]
        {
            new { Code = "FIN", Name = "Finance", Desc = "General Ledger, Accounts Payable, Accounts Receivable, Cash Management", Icon = "fa-calculator", Sort = 1 },
            new { Code = "INV", Name = "Inventory", Desc = "Inventory Management, Stock Control, Warehousing", Icon = "fa-boxes", Sort = 2 },
            new { Code = "PROC", Name = "Procurement", Desc = "Purchase Orders, Requisitions, Supplier Management", Icon = "fa-shopping-cart", Sort = 3 },
            new { Code = "SALES", Name = "Sales", Desc = "Sales Orders, Invoicing, Customer Management", Icon = "fa-chart-line", Sort = 4 },
            new { Code = "FA", Name = "Fixed Assets", Desc = "Asset Registry, Depreciation, Asset Lifecycle", Icon = "fa-building", Sort = 5 },
            new { Code = "MNT", Name = "Maintenance", Desc = "Work Orders, Preventive Maintenance, Equipment Management", Icon = "fa-wrench", Sort = 6 },
            new { Code = "HR", Name = "Human Resources", Desc = "Employee Management, Payroll, Leave Management", Icon = "fa-users", Sort = 7 },
            new { Code = "PROJ", Name = "Projects", Desc = "Project Management, Costing, Billing", Icon = "fa-project-diagram", Sort = 8 },
            new { Code = "MFG", Name = "Manufacturing", Desc = "Production Planning, BOM, Manufacturing Execution", Icon = "fa-industry", Sort = 9 }
        };

        foreach (var mod in modules)
        {
            if (!await _context.ModuleDefinitions.AnyAsync(m => m.ModuleCode == mod.Code && m.TenantId == tenantId))
            {
                await _context.ModuleDefinitions.AddAsync(new ModuleDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ModuleCode = mod.Code,
                    ModuleName = mod.Name,
                    Description = mod.Desc,
                    IconClass = mod.Icon,
                    SortOrder = mod.Sort,
                    IsActive = true,
                    IsSystem = true,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }
        
        _logger.LogInformation("Module definitions seeded");
    }

    private async Task SeedTransactionDocumentMappingsAsync(Guid tenantId, DateTime baseDate)
    {
        // Get Modules
        var finMod = await _context.ModuleDefinitions.FirstOrDefaultAsync(m => m.ModuleCode == "FIN" && m.TenantId == tenantId);
        var invMod = await _context.ModuleDefinitions.FirstOrDefaultAsync(m => m.ModuleCode == "INV" && m.TenantId == tenantId);
        var procMod = await _context.ModuleDefinitions.FirstOrDefaultAsync(m => m.ModuleCode == "PROC" && m.TenantId == tenantId);
        var salesMod = await _context.ModuleDefinitions.FirstOrDefaultAsync(m => m.ModuleCode == "SALES" && m.TenantId == tenantId);
        var faMod = await _context.ModuleDefinitions.FirstOrDefaultAsync(m => m.ModuleCode == "FA" && m.TenantId == tenantId);

        if (finMod == null) return; // Should not happen if previous method ran

        var mappings = new List<(string DocType, Guid ModId)>
        {
            // Finance
            ("JournalEntry", finMod.Id),
            ("Payment", finMod.Id),
            ("Receipt", finMod.Id),
            ("BankTransfer", finMod.Id),
            ("BankReconciliation", finMod.Id),
            ("TaxAdjustment", finMod.Id),
            ("BudgetEntry", finMod.Id),

            // Inventory
            ("InventoryAdjustment", invMod?.Id ?? finMod.Id),
            ("StockTransfer", invMod?.Id ?? finMod.Id),
            ("StockCount", invMod?.Id ?? finMod.Id),
            ("GoodsReceipt", invMod?.Id ?? finMod.Id),
            ("GoodsIssue", invMod?.Id ?? finMod.Id),

            // Procurement
            ("PurchaseOrder", procMod?.Id ?? finMod.Id),
            ("PurchaseRequisition", procMod?.Id ?? finMod.Id),
            ("VendorInvoice", procMod?.Id ?? finMod.Id), // AP Invoice usually mapped to Procurement or Finance

            // Sales
            ("SalesOrder", salesMod?.Id ?? finMod.Id),
            ("SalesInvoice", salesMod?.Id ?? finMod.Id), // AR Invoice
            ("DeliveryNote", salesMod?.Id ?? finMod.Id),
            ("Quotation", salesMod?.Id ?? finMod.Id),

            // Fixed Assets
            ("AssetAcquisition", faMod?.Id ?? finMod.Id),
            ("AssetDepreciation", faMod?.Id ?? finMod.Id),
            ("AssetDisposal", faMod?.Id ?? finMod.Id),
            ("AssetTransfer", faMod?.Id ?? finMod.Id)
        };

        foreach (var mapping in mappings)
        {
            if (!await _context.TransactionDocumentModuleMappings.AnyAsync(m => m.DocumentType == mapping.DocType && m.TenantId == tenantId))
            {
                await _context.TransactionDocumentModuleMappings.AddAsync(new TransactionDocumentModuleMapping
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DocumentType = mapping.DocType,
                    ModuleDefinitionId = mapping.ModId,
                    IsActive = true,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation("Transaction document mappings seeded");
    }

    #endregion
}
