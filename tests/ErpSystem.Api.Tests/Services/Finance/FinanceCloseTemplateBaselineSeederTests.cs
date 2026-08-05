using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceCloseTemplateBaselineSeederTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "SeedData")]
    public async Task SeedTenant_ShouldInstallCurrentTdcControlSetAndRemainIdempotent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var seeder = CreateSeeder(db);

        var firstAdded = await seeder.SeedTenantAsync(tenantId);
        var secondAdded = await seeder.SeedTenantAsync(tenantId);

        firstAdded.Should().Be(3);
        secondAdded.Should().Be(0);
        var templates = await db.FinanceCloseTemplates
            .Include(template => template.TaskDefinitions)
            .OrderBy(template => template.CloseType)
            .ToListAsync();
        templates.Should().HaveCount(3);
        templates.Should().OnlyContain(template =>
            template.IsActive && template.IsSystemDefault &&
            template.Status == FinanceCloseTemplateStatuses.Approved &&
            template.TaskDefinitions.Count == FinanceCloseTemplateBaselineCatalog.Tasks.Count);
        templates.Should().OnlyContain(template => template.TaskDefinitions.Any(task =>
            task.CheckCode == "AP_CONTROL_RECONCILIATION"));
        templates.Should().OnlyContain(template => template.TaskDefinitions.Any(task =>
            task.CheckCode == "AR_CONTROL_RECONCILIATION"));
        templates.Should().OnlyContain(template => template.TaskDefinitions.Any(task =>
            task.CheckCode == "RECURRING_JOURNAL_EXCEPTIONS" && task.IsMandatory));
        templates.Should().OnlyContain(template => template.TaskDefinitions.Any(task =>
            task.CheckCode == "BUDGET_ADOPTION_REVIEW" && !task.IsMandatory));
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "SeedData")]
    public async Task SeedTenant_ShouldSupersedeOldSystemBaselineWithoutEditingIt()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var oldBaseline = new FinanceCloseTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateCode = "TDC-MONTH-END",
            Name = "TDC month-end close",
            CloseType = FinanceCloseTemplateTypes.MonthEnd,
            Version = 1,
            Status = FinanceCloseTemplateStatuses.Approved,
            IsActive = true,
            IsSystemDefault = true,
            Description = "Original close control set"
        };
        db.FinanceCloseTemplates.Add(oldBaseline);
        await db.SaveChangesAsync();

        await CreateSeeder(db).SeedTenantAsync(tenantId);

        var monthTemplates = await db.FinanceCloseTemplates
            .Include(template => template.TaskDefinitions)
            .Where(template => template.CloseType == FinanceCloseTemplateTypes.MonthEnd)
            .OrderBy(template => template.Version)
            .ToListAsync();
        monthTemplates.Should().HaveCount(2);
        monthTemplates[0].Id.Should().Be(oldBaseline.Id);
        monthTemplates[0].Status.Should().Be(FinanceCloseTemplateStatuses.Superseded);
        monthTemplates[0].IsActive.Should().BeFalse();
        monthTemplates[0].Description.Should().Be("Original close control set");
        monthTemplates[1].Version.Should().Be(2);
        monthTemplates[1].IsActive.Should().BeTrue();
        monthTemplates[1].TaskDefinitions.Should().HaveCount(FinanceCloseTemplateBaselineCatalog.Tasks.Count);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "SeedData")]
    public async Task SeedTenant_ShouldPreserveCustomActiveTemplateAsTenantPolicy()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var custom = new FinanceCloseTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateCode = "TDC-CUSTOM-MONTH-END",
            Name = "Approved custom month-end close",
            CloseType = FinanceCloseTemplateTypes.MonthEnd,
            Version = 7,
            Status = FinanceCloseTemplateStatuses.Approved,
            IsActive = true,
            IsSystemDefault = false
        };
        db.FinanceCloseTemplates.Add(custom);
        await db.SaveChangesAsync();

        await CreateSeeder(db).SeedTenantAsync(tenantId);

        var monthTemplates = await db.FinanceCloseTemplates
            .Where(template => template.CloseType == FinanceCloseTemplateTypes.MonthEnd)
            .ToListAsync();
        monthTemplates.Should().ContainSingle();
        monthTemplates.Single().Id.Should().Be(custom.Id);
        monthTemplates.Single().IsActive.Should().BeTrue();
        (await db.FinanceCloseTemplates.CountAsync(template =>
            template.CloseType != FinanceCloseTemplateTypes.MonthEnd && template.IsSystemDefault))
            .Should().Be(2);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-close-template-seed-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static FinanceCloseTemplateBaselineSeeder CreateSeeder(ApplicationDbContext db)
        => new(db, Mock.Of<ILogger<FinanceCloseTemplateBaselineSeeder>>());

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId)
        => db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "TDC Finance Seed Tenant",
            Code = "TDC-SEED",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
}
