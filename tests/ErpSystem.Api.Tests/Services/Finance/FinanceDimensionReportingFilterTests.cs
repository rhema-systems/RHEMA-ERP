using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceDimensionReportingFilterTests
{
    [Fact]
    public async Task Filters_use_or_within_dimension_and_and_across_dimensions()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var department = AddDefinition(db, tenantId, "DEPT", "Department");
        var project = AddDefinition(db, tenantId, "PROJECT", "Project");
        var finance = AddValue(db, department, "FIN", "Finance", isActive: false);
        var operations = AddValue(db, department, "OPS", "Operations");
        var alpha = AddValue(db, project, "A", "Alpha");
        var beta = AddValue(db, project, "B", "Beta");
        var financeAlpha = AddSet(db, tenantId, (department, finance), (project, alpha));
        var operationsAlpha = AddSet(db, tenantId, (department, operations), (project, alpha));
        var financeBeta = AddSet(db, tenantId, (department, finance), (project, beta));
        AddTransaction(db, tenantId, financeAlpha.Id);
        AddTransaction(db, tenantId, operationsAlpha.Id);
        AddTransaction(db, tenantId, financeBeta.Id);
        AddTransaction(db, tenantId, null);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var filters = await service.ResolveAsync([
            new FinanceDimensionFilterDto { DimensionCode = "dept", ValueCodes = ["fin", "ops"] },
            new FinanceDimensionFilterDto { FinanceDimensionDefinitionId = project.Id, ValueCodes = ["A"] }
        ]);
        var ids = await service.Apply(db.AccountTransactions.AsNoTracking(), filters)
            .Select(transaction => transaction.FinanceDimensionSetId)
            .ToListAsync();

        ids.Should().BeEquivalentTo([financeAlpha.Id, operationsAlpha.Id]);
        filters.Should().HaveCount(2);
        filters.Single(filter => filter.DimensionCode == "DEPT").ValueCodes.Should().BeEquivalentTo(["FIN", "OPS"]);
    }

    [Fact]
    public async Task Cross_tenant_definition_and_value_codes_fail_closed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var otherDefinition = AddDefinition(db, Guid.NewGuid(), "SECRET", "Other tenant");
        AddValue(db, otherDefinition, "X", "Secret");
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var action = () => service.ResolveAsync([
            new FinanceDimensionFilterDto
            {
                FinanceDimensionDefinitionId = otherDefinition.Id,
                ValueCodes = ["X"]
            }
        ]);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*current tenant*");
    }

    [Fact]
    public async Task Empty_value_selection_is_rejected_instead_of_broadening_report()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var definition = AddDefinition(db, tenantId, "DEPT", "Department");
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var action = () => service.ResolveAsync([
            new FinanceDimensionFilterDto
            {
                FinanceDimensionDefinitionId = definition.Id,
                ValueCodes = []
            }
        ]);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least one value code*");
    }

    [Fact]
    public async Task Definition_id_and_code_must_identify_the_same_dimension()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var department = AddDefinition(db, tenantId, "DEPT", "Department");
        AddValue(db, department, "FIN", "Finance");
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var action = () => service.ResolveAsync([
            new FinanceDimensionFilterDto
            {
                FinanceDimensionDefinitionId = department.Id,
                DimensionCode = "PROJECT",
                ValueCodes = ["FIN"]
            }
        ]);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*id and code*");
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-dimension-reporting-{Guid.NewGuid():N}")
            .Options);

    private static FinanceDimensionReportingFilterService CreateService(
        ApplicationDbContext db,
        Guid tenantId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(current => current.TenantId).Returns(tenantId);
        user.SetupGet(current => current.UserId).Returns(Guid.NewGuid().ToString());
        user.SetupGet(current => current.UserName).Returns("report.user");
        user.SetupGet(current => current.Claims).Returns(new Dictionary<string, string>());
        return new FinanceDimensionReportingFilterService(db, user.Object);
    }

    private static FinanceDimensionDefinition AddDefinition(
        ApplicationDbContext db,
        Guid tenantId,
        string code,
        string name)
    {
        var definition = new FinanceDimensionDefinition
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = name,
            Classification = "Analytical", ValueSourceType = "Lookup", IsActive = true
        };
        db.FinanceDimensionDefinitions.Add(definition);
        return definition;
    }

    private static FinanceDimensionValue AddValue(
        ApplicationDbContext db,
        FinanceDimensionDefinition definition,
        string code,
        string name,
        bool isActive = true)
    {
        var value = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = definition.TenantId,
            FinanceDimensionDefinitionId = definition.Id, Code = code, Name = name,
            EffectiveDate = new DateTime(2025, 1, 1), IsActive = isActive
        };
        db.FinanceDimensionValues.Add(value);
        return value;
    }

    private static FinanceDimensionSet AddSet(
        ApplicationDbContext db,
        Guid tenantId,
        params (FinanceDimensionDefinition Definition, FinanceDimensionValue Value)[] assignments)
    {
        var set = new FinanceDimensionSet
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            CombinationHash = Guid.NewGuid().ToString("N"),
            DisplayValue = string.Join(" | ", assignments.Select(item => $"{item.Definition.Code}={item.Value.Code}"))
        };
        foreach (var assignment in assignments)
        {
            set.Items.Add(new FinanceDimensionSetItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                FinanceDimensionSetId = set.Id,
                FinanceDimensionDefinitionId = assignment.Definition.Id,
                FinanceDimensionValueId = assignment.Value.Id,
                DimensionCodeSnapshot = assignment.Definition.Code,
                DimensionValueCodeSnapshot = assignment.Value.Code,
                DimensionValueNameSnapshot = assignment.Value.Name
            });
        }
        db.FinanceDimensionSets.Add(set);
        return set;
    }

    private static void AddTransaction(ApplicationDbContext db, Guid tenantId, Guid? setId)
    {
        db.AccountTransactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = Guid.NewGuid(),
            JournalEntryId = Guid.NewGuid(), FinanceDimensionSetId = setId,
            FiscalPeriodId = Guid.NewGuid(), TransactionDate = new DateTime(2026, 1, 1),
            DebitAmount = 100m, FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS", PostingStatus = "Posted"
        });
    }
}
