using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Authorization;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using ErpSystem.Shared;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceDimensionAdministrationServiceTests
{
    private static readonly DateTime JournalDate = new(2026, 1, 15);

    [Fact]
    public void Dimension_routes_use_read_and_dedicated_manage_permissions()
    {
        FinancePermissionPolicyMap.GetRequiredPolicies("FinanceDimensionsController", "GetAll", ["GET"])
            .Should().Equal(FinancePermissions.ViewFinance);
        FinancePermissionPolicyMap.GetRequiredPolicies("FinanceDimensionsController", "Create", ["POST"])
            .Should().Equal(FinancePermissions.ManageCodingDimensions);
    }

    [Fact]
    public async Task Definition_admin_is_tenant_scoped_and_canonical()
    {
        await using var db = CreateContext();
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        db.FinanceDimensionDefinitions.Add(new FinanceDimensionDefinition
        {
            Id = Guid.NewGuid(), TenantId = otherTenant, Code = "SECRET", Name = "Other tenant",
            Classification = "Analytical", ValueSourceType = "Lookup", IsActive = true
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenant);

        var created = await service.CreateDefinitionAsync(new UpsertFinanceDimensionDefinitionDto
        {
            Code = "dept", Name = "Department", Classification = "analytical",
            ValueSourceType = "lookup"
        });

        created.Code.Should().Be("DEPT");
        created.Classification.Should().Be("Analytical");
        created.ValueSourceType.Should().Be("Lookup");
        (await service.GetDefinitionsAsync(true)).Should().ContainSingle(item => item.Code == "DEPT")
            .And.NotContain(item => item.Code == "SECRET");
    }

    [Fact]
    public async Task Top_level_lookup_value_can_be_created_without_a_parent()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var definition = SeedDefinition(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var created = await service.CreateValueAsync(definition.Id, new UpsertFinanceDimensionValueDto
        {
            Code = "fin",
            Name = "Finance",
            EffectiveDate = new DateTime(2026, 1, 1),
            IsActive = true
        });

        created.Code.Should().Be("FIN");
        created.ParentValueId.Should().BeNull();
        db.FinanceDimensionValues.Should().ContainSingle(item => item.Id == created.Id);
    }

    [Fact]
    public async Task Required_manual_journal_rule_fails_closed_when_value_is_missing()
    {
        await using var fixture = await Fixture.CreateAsync("Required");

        var action = () => fixture.Service.ResolveManualJournalLineAsync(
            fixture.Account.Id, JournalDate, [], CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DEPT*required*");
        fixture.Context.FinanceDimensionSets.Should().BeEmpty();
    }

    [Fact]
    public async Task Fixed_rule_derives_one_deterministic_immutable_set()
    {
        await using var fixture = await Fixture.CreateAsync("Fixed", defaultValue: true);

        var first = await fixture.Service.ResolveManualJournalLineAsync(
            fixture.Account.Id, JournalDate, [], CancellationToken.None);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var second = await fixture.Service.ResolveManualJournalLineAsync(
            fixture.Account.Id, JournalDate, [], CancellationToken.None);

        first.Should().NotBeNull();
        second!.Id.Should().Be(first!.Id);
        second.DisplayValue.Should().Be("DEPT=FIN");
        second.Items.Should().ContainSingle(item => item.DimensionValueCodeSnapshot == "FIN");
    }

    [Fact]
    public async Task Prohibited_manual_journal_rule_rejects_supplied_value()
    {
        await using var fixture = await Fixture.CreateAsync("Prohibited");

        var action = () => fixture.Service.ResolveManualJournalLineAsync(
            fixture.Account.Id,
            JournalDate,
            [new FinancePostingDimensionValueDto { DimensionCode = "DEPT", ValueCode = "FIN" }],
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DEPT*prohibited*");
    }

    [Fact]
    public async Task Rule_for_uncertified_source_does_not_affect_manual_journal()
    {
        await using var fixture = await Fixture.CreateAsync("Required", sourceDocumentType: "VendorInvoice");

        var result = await fixture.Service.ResolveManualJournalLineAsync(
            fixture.Account.Id, JournalDate, [], CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Non_optional_rule_requires_a_certified_source_document_type()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var account = SeedAccount(db, tenantId);
        var definition = SeedDefinition(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var action = () => service.UpsertRuleAsync(null, new UpsertFinanceDimensionAccountRuleDto
        {
            AccountId = account.Id,
            FinanceDimensionDefinitionId = definition.Id,
            RuleType = "Required",
            EffectiveDate = JournalDate,
            IsActive = true
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*certified source document type*");
    }

    [Fact]
    public async Task Mandatory_rule_cannot_claim_an_uncertified_operational_adapter()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var account = SeedAccount(db, tenantId);
        var definition = SeedDefinition(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var action = () => service.UpsertRuleAsync(null, new UpsertFinanceDimensionAccountRuleDto
        {
            AccountId = account.Id,
            FinanceDimensionDefinitionId = definition.Id,
            RuleType = "Required",
            SourceModule = "AP",
            SourceDocumentType = "VendorInvoice",
            PostingAction = "Post",
            EffectiveDate = JournalDate,
            IsActive = true
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not yet certified*");
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-dimensions-{Guid.NewGuid():N}").Options);

    private static FinanceDimensionAdministrationService CreateService(ApplicationDbContext db, Guid tenantId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        user.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        user.SetupGet(item => item.UserName).Returns("dimension.admin");
        user.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());
        return new FinanceDimensionAdministrationService(db, user.Object);
    }

    private static Account SeedAccount(ApplicationDbContext db, Guid tenantId)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "6000", AccountNumber = "6000",
            AccountName = "Operating expense", AccountType = AccountType.Expense, CurrencyCode = "GHS",
            Status = AccountStatus.Active, AllowDirectPosting = true
        };
        db.Accounts.Add(account);
        return account;
    }

    private static FinanceDimensionDefinition SeedDefinition(ApplicationDbContext db, Guid tenantId)
    {
        var definition = new FinanceDimensionDefinition
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "DEPT", Name = "Department",
            Classification = "Analytical", ValueSourceType = "Lookup", IsActive = true
        };
        db.FinanceDimensionDefinitions.Add(definition);
        return definition;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required ApplicationDbContext Context { get; init; }
        public required FinanceDimensionAdministrationService Service { get; init; }
        public required Account Account { get; init; }

        public static async Task<Fixture> CreateAsync(
            string ruleType,
            bool defaultValue = false,
            string sourceDocumentType = "ManualJournalEntry")
        {
            var db = CreateContext();
            var tenantId = Guid.NewGuid();
            var account = SeedAccount(db, tenantId);
            var definition = SeedDefinition(db, tenantId);
            var value = new FinanceDimensionValue
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FinanceDimensionDefinitionId = definition.Id,
                Code = "FIN", Name = "Finance", EffectiveDate = new DateTime(2025, 1, 1), IsActive = true
            };
            db.FinanceDimensionValues.Add(value);
            db.FinanceDimensionAccountRules.Add(new FinanceDimensionAccountRule
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                FinanceDimensionDefinitionId = definition.Id, RuleType = ruleType,
                DefaultDimensionValueId = defaultValue ? value.Id : null,
                SourceModule = "GL", SourceDocumentType = sourceDocumentType, PostingAction = "Post",
                EffectiveDate = new DateTime(2025, 1, 1), IsActive = true
            });
            await db.SaveChangesAsync();
            return new Fixture { Context = db, Service = CreateService(db, tenantId), Account = account };
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
