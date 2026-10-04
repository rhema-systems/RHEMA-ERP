using System.Reflection;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceDemoPrerequisiteSeederTests
{
    [Fact]
    public async Task Tax_seed_separates_purchase_vat_from_supplier_withholding()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var seeder = new FinanceDataSeeder(context, NullLogger<FinanceDataSeeder>.Instance);
        var seedMethod = typeof(FinanceDataSeeder).GetMethod(
            "SeedTaxConfigurationAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        seedMethod.Should().NotBeNull();
        await (Task)seedMethod!.Invoke(seeder, new object[] { tenantId, DateTime.UtcNow })!;

        var purchaseGroup = await context.TaxGroups.SingleAsync(group =>
            group.TenantId == tenantId && group.Code == "VAT-STD-PURCHASES");
        purchaseGroup.Applicability.Should().Be(TaxApplicability.Purchases);
        purchaseGroup.IsDefault.Should().BeTrue();
        var componentTaxIds = await context.TaxGroupComponents
            .Where(component => component.TenantId == tenantId && component.TaxGroupId == purchaseGroup.Id && !component.IsDeleted)
            .Select(component => component.TaxId)
            .ToListAsync();
        var componentTaxes = await context.Taxes.Where(tax => componentTaxIds.Contains(tax.Id)).ToListAsync();
        componentTaxes.Should().HaveCount(3);
        componentTaxes.Should().OnlyContain(tax => tax.Applicability == TaxApplicability.Purchases);
        componentTaxes.Single(tax => tax.Code == "VAT-STD-PUR").Category.Should().Be(TaxCategory.Standard);
        componentTaxes.Where(tax => tax.Code is "NHIL-PUR" or "GETFUND-PUR")
            .Should().OnlyContain(tax => tax.Category == TaxCategory.Levy);
        componentTaxes.Should().OnlyContain(tax => tax.TaxReceivableAccountId.HasValue);

        var salesTaxes = await context.Taxes
            .Where(tax => tax.TenantId == tenantId &&
                new[] { "NHIL", "GETFUND", "VAT-STD" }.Contains(tax.Code))
            .ToListAsync();
        salesTaxes.Should().HaveCount(3);
        salesTaxes.Should().OnlyContain(tax => tax.TaxPayableAccountId.HasValue,
            "every sales tax component must have an output-tax control account before AR approval can post it");

        var withholdingGroup = await context.TaxGroups.SingleAsync(group =>
            group.TenantId == tenantId && group.Code == "WHT-SERVICES");
        withholdingGroup.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task ExchangeRateSeed_ShouldStoreFunctionalCurrencyPerTargetCurrencyUnit()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var seeder = new FinanceDataSeeder(context, NullLogger<FinanceDataSeeder>.Instance);
        var seedMethod = typeof(FinanceDataSeeder).GetMethod(
            "SeedExchangeRatesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        seedMethod.Should().NotBeNull();
        await (Task)seedMethod!.Invoke(seeder, new object[] { tenantId, DateTime.UtcNow })!;
        await context.SaveChangesAsync();

        var usd = await context.ExchangeRates.SingleAsync(rate =>
            rate.TenantId == tenantId && rate.BaseCurrencyCode == "GHS" && rate.TargetCurrencyCode == "USD");
        // ExchangeRate.Rate follows the canonical source/base -> target convention:
        // 1 GHS = Rate USD. InverseRate is therefore the GHS value of one USD.
        usd.Rate.Should().Be(0.085397m);
        usd.InverseRate.Should().Be(11.7100m);

        var eur = await context.ExchangeRates.SingleAsync(rate =>
            rate.TenantId == tenantId && rate.BaseCurrencyCode == "GHS" && rate.TargetCurrencyCode == "EUR");
        eur.Rate.Should().Be(0.075310m);
        eur.InverseRate.Should().Be(13.2785m);

        var gbp = await context.ExchangeRates.SingleAsync(rate =>
            rate.TenantId == tenantId && rate.BaseCurrencyCode == "GHS" && rate.TargetCurrencyCode == "GBP");
        gbp.Rate.Should().Be(0.064392m);
        gbp.InverseRate.Should().Be(15.5298m);
    }

    [Fact]
    public void StandardFinanceSeed_ShouldProvisionFixedAssetConfigurationWithoutAssetMasters()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Seeders", "FinanceDataSeeder.cs"));

        source.Should().Contain("SeedFixedAssetCategoriesAsync");
        source.Should().NotContain("SeedFixedAssetsAsync");
        source.Should().NotContain("FA-2024-BLDG-001");
        source.Should().NotContain("FA-2024-EQP-001");
        source.Should().NotContain("FA-2024-EQP-002");
        source.Should().NotContain("FA-2024-VEH-001");
    }

    [Fact]
    public async Task StandardFinanceSeed_ShouldProvisionEveryGovernedFixedAssetAndFinanceSettingsGlDefault()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1);
        var seeder = new FinanceDataSeeder(context, NullLogger<FinanceDataSeeder>.Instance);
        var chartMethod = typeof(FinanceDataSeeder).GetMethod(
            "GetStandardChartOfAccounts",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var categoryMethod = typeof(FinanceDataSeeder).GetMethod(
            "SeedFixedAssetCategoriesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var settingsMethod = typeof(FinanceDataSeeder).GetMethod(
            "SeedFinanceSettingsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        chartMethod.Should().NotBeNull();
        categoryMethod.Should().NotBeNull();
        settingsMethod.Should().NotBeNull();
        var accounts = (List<Account>)chartMethod!.Invoke(seeder, new object[] { tenantId, baseDate })!;
        context.Accounts.AddRange(accounts);
        await context.SaveChangesAsync();

        accounts.Select(account => account.AccountCode).Should().Contain(new[]
        {
            "1030", "1090", "1210", "1540", "1545", "1580", "1595",
            "2050", "2510", "3200", "4930", "4935", "4940", "4950", "5010",
            "6310", "6320", "6330", "6610", "6700", "6710"
        });
        var roundingGain = accounts.Single(account => account.AccountCode == "4950");
        roundingGain.AccountType.Should().Be(AccountType.Revenue);
        roundingGain.AllowDirectPosting.Should().BeTrue();
        roundingGain.IsControlAccount.Should().BeFalse();
        var roundingLoss = accounts.Single(account => account.AccountCode == "6710");
        roundingLoss.AccountType.Should().Be(AccountType.Expense);
        roundingLoss.AllowDirectPosting.Should().BeTrue();
        roundingLoss.IsControlAccount.Should().BeFalse();

        await (Task)categoryMethod!.Invoke(seeder, new object[] { tenantId, baseDate })!;
        await (Task)settingsMethod!.Invoke(seeder, new object[] { tenantId, baseDate })!;
        await context.SaveChangesAsync();

        var categories = await context.FixedAssetCategories
            .Where(category => category.TenantId == tenantId && !category.IsDeleted)
            .ToListAsync();
        categories.Should().NotBeEmpty();
        categories.Should().OnlyContain(category =>
            category.GainOnDisposalAccountId.HasValue
            && category.LossOnDisposalAccountId.HasValue
            && category.DisposalProceedsClearingAccountId.HasValue
            && category.RevaluationSurplusAccountId.HasValue
            && category.RevaluationLossAccountId.HasValue
            && category.ImpairmentLossAccountId.HasValue
            && category.AccumulatedImpairmentAccountId.HasValue
            && category.ImpairmentReversalAccountId.HasValue
            && category.AucAccountId.HasValue);

        var settings = await context.FinanceSettings.SingleAsync(item => item.TenantId == tenantId);
        settings.ReturnToVendorClearingAccountId.Should().NotBeNull();
        settings.PurchaseReturnVarianceAccountId.Should().NotBeNull();
        settings.RetainedEarningsAccountId.Should().NotBeNull();
        settings.UnrealizedGainLossAccountId.Should().NotBeNull();
        settings.UnrealizedFxGainAccountId.Should().NotBeNull();
        settings.UnrealizedFxLossAccountId.Should().NotBeNull();
        settings.RealizedGainLossAccountId.Should().NotBeNull();
        settings.RealizedFxGainAccountId.Should().NotBeNull();
        settings.RealizedFxLossAccountId.Should().NotBeNull();
        settings.SuspenseAccountId.Should().NotBeNull();
        settings.SegmentClearingAccountId.Should().NotBeNull();
        settings.ControlAccountArId.Should().NotBeNull();
        settings.ControlAccountApId.Should().NotBeNull();
        settings.SupplierAdvanceAccountId.Should().NotBeNull();
        settings.CustomerAdvanceAccountId.Should().NotBeNull();
        settings.ControlAccountInventoryId.Should().NotBeNull();
        settings.ControlAccountPayrollId.Should().NotBeNull();
        settings.ControlAccountTaxId.Should().NotBeNull();
        settings.ControlAccountCOGSId.Should().NotBeNull();
        settings.ControlAccountGRVAccrualId.Should().NotBeNull();
        settings.DiscountAllowedAccountId.Should().NotBeNull();
        settings.DiscountReceivedAccountId.Should().NotBeNull();
        settings.MigrationClearingAccountId.Should().NotBeNull();
        settings.LeaseRouAssetAccountId.Should().NotBeNull();
        settings.LeaseLiabilityAccountId.Should().NotBeNull();
        settings.LeaseInterestExpenseAccountId.Should().NotBeNull();
        settings.WriteOffExpenseAccountId.Should().NotBeNull();
        settings.WriteOffRecoveryAccountId.Should().NotBeNull();
        settings.InvoiceRoundingGainAccountId.Should().Be(roundingGain.Id);
        settings.InvoiceRoundingLossAccountId.Should().Be(roundingLoss.Id);

        var deliberateGainMapping = Guid.NewGuid();
        var deliberateLossMapping = Guid.NewGuid();
        settings.InvoiceRoundingGainAccountId = deliberateGainMapping;
        settings.InvoiceRoundingLossAccountId = deliberateLossMapping;
        await context.SaveChangesAsync();

        await (Task)settingsMethod.Invoke(seeder, new object[] { tenantId, baseDate })!;
        await context.SaveChangesAsync();

        settings.InvoiceRoundingGainAccountId.Should().Be(deliberateGainMapping,
            "idempotent provisioning must preserve an existing tenant mapping");
        settings.InvoiceRoundingLossAccountId.Should().Be(deliberateLossMapping,
            "idempotent provisioning must preserve an existing tenant mapping");
    }

    [Fact]
    public void FinanceDemoCounterpartySeed_ShouldUseGovernedBusinessPartnerProfilesOnly()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Seeders", "FinanceDataSeeder.cs"));

        source.Should().Contain("SeedFinanceDemoCounterpartiesAsync");
        source.Should().Contain("BusinessPartnerRoleType.Supplier");
        source.Should().Contain("BusinessPartnerRoleType.Customer");
        source.Should().Contain("BusinessPartnerApProfileVersions.Add");
        source.Should().Contain("BusinessPartnerArProfileVersions.Add");
        source.Should().Contain("BusinessPartnerApWhtDefaults.Add");
        source.Should().NotContain("_context.Suppliers",
            "Finance demo counterparties must not recreate the retired Finance supplier master");
    }

    [Fact]
    public void BankLiquidityCode_ShouldRemainUniqueForDeterministicSeedIdentifiers()
    {
        // Finance seed GUIDs deliberately share a readable family prefix. This reproduces the
        // SQL Server collision that an eight-character liquidity code caused during a clean
        // database rehearsal and protects the longer stable suffix from being shortened again.
        var firstBankId = Guid.Parse("00000008-1001-0000-0000-000000000001");
        var secondBankId = Guid.Parse("00000008-1002-0000-0000-000000000001");
        var codeMethod = typeof(FinanceDataSeeder).GetMethod(
            "BuildBankLiquidityAccountCode",
            BindingFlags.Static | BindingFlags.NonPublic);

        codeMethod.Should().NotBeNull();
        var firstCode = (string)codeMethod!.Invoke(null, new object[] { firstBankId })!;
        var secondCode = (string)codeMethod.Invoke(null, new object[] { secondBankId })!;

        firstCode.Should().NotBe(secondCode);
        firstCode.Should().HaveLength(21).And.StartWith("BANK-");
        secondCode.Should().HaveLength(21).And.StartWith("BANK-");
    }

    [Fact]
    public async Task AccountCombinationSeed_DoesNotDuplicateTransactionDimensionsIntoAccountIdentity()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var departmentSegment = AddStructure(context, tenantId, "DEPT", 1, isNatural: false);
        var naturalSegment = AddStructure(context, tenantId, "ACCT", 2, isNatural: true);
        var projectSegment = AddStructure(context, tenantId, "PROJ", 3, isNatural: false);

        foreach (var value in new[] { "100", "200", "300", "500", "600" })
        {
            AddLookup(context, tenantId, departmentSegment.Id, value, $"Department {value}");
        }

        foreach (var value in new[] { "0000", "P101", "P103" })
        {
            AddLookup(context, tenantId, projectSegment.Id, value, $"Project {value}");
        }

        foreach (var code in new[] { "1000", "1500", "4100", "6000", "6100", "6200", "6400", "6500", "6600" })
        {
            context.Accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = code,
                AccountNumber = $"000-{code}-0000",
                AccountName = $"Natural {code}",
                AccountType = code.StartsWith('1') ? AccountType.Asset : code.StartsWith('4') ? AccountType.Revenue : AccountType.Expense,
                AccountCategory = "Test",
                CurrencyCode = "GHS",
                AllowDirectPosting = true,
                Status = AccountStatus.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            });
        }

        await context.SaveChangesAsync();
        var seeder = new FinanceDataSeeder(context, NullLogger<FinanceDataSeeder>.Instance);
        var seedMethod = typeof(FinanceDataSeeder).GetMethod(
            "SeedFinanceDemoAccountCombinationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        seedMethod.Should().NotBeNull();
        await (Task)seedMethod!.Invoke(seeder, new object[] { tenantId, DateTime.UtcNow })!;
        await context.SaveChangesAsync();

        var firstCount = await context.Accounts.CountAsync(account =>
            account.TenantId == tenantId && account.CreatedBy == "System (Finance Demo)");
        firstCount.Should().Be(0);
        (await context.Accounts.CountAsync(account => account.TenantId == tenantId)).Should().Be(9);

        await (Task)seedMethod.Invoke(seeder, new object[] { tenantId, DateTime.UtcNow })!;
        await context.SaveChangesAsync();

        (await context.Accounts.CountAsync(account =>
            account.TenantId == tenantId && account.CreatedBy == "System (Finance Demo)"))
            .Should().Be(firstCount);
    }

    private static AccountSegmentStructure AddStructure(
        ApplicationDbContext context,
        Guid tenantId,
        string code,
        int position,
        bool isNatural)
    {
        var structure = new AccountSegmentStructure
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SegmentName = code,
            SegmentCode = code,
            SegmentPosition = position,
            SegmentLength = code == "ACCT" ? 4 : code == "DEPT" ? 3 : 4,
            DataType = "Alphanumeric",
            LookupTableRequired = !isNatural,
            IsReportingDimension = true,
            IsNaturalAccount = isNatural,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        context.AccountSegmentStructures.Add(structure);
        return structure;
    }

    private static void AddLookup(
        ApplicationDbContext context,
        Guid tenantId,
        Guid structureId,
        string value,
        string description)
    {
        context.SegmentLookupValues.Add(new SegmentLookupValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SegmentStructureId = structureId,
            SegmentValue = value,
            Description = description,
            IsActive = true,
            EffectiveDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-demo-prerequisites-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
