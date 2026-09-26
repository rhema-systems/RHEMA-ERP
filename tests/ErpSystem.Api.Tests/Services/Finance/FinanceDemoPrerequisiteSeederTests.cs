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
        usd.Rate.Should().Be(12.5m, "1 USD is seeded as 12.50 GHS under the ExchangeRate entity contract");
        usd.InverseRate.Should().Be(0.08m);

        var eur = await context.ExchangeRates.SingleAsync(rate =>
            rate.TenantId == tenantId && rate.BaseCurrencyCode == "GHS" && rate.TargetCurrencyCode == "EUR");
        eur.Rate.Should().Be(13.1579m);
        eur.InverseRate.Should().Be(0.076m);

        var gbp = await context.ExchangeRates.SingleAsync(rate =>
            rate.TenantId == tenantId && rate.BaseCurrencyCode == "GHS" && rate.TargetCurrencyCode == "GBP");
        gbp.Rate.Should().Be(15.873m);
        gbp.InverseRate.Should().Be(0.063m);
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
