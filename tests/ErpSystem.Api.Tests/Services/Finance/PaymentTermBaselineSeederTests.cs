using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class PaymentTermBaselineSeederTests
{
    [Fact]
    public async Task SeedTenantBaselineAsync_AddsOnlyMissingTerms_AndPreservesTenantConfiguration()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        context.PaymentTerms.Add(new PaymentTerm
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "CUSTOM",
            Name = "Tenant default",
            DueDays = 14,
            IsActive = true,
            IsDefault = true,
            ApplicableTo = "All"
        });
        await context.SaveChangesAsync();

        var added = await PaymentTermBaselineSeeder.SeedTenantBaselineAsync(context, tenantId);

        added.Should().Be(7);
        var net30 = await context.PaymentTerms.SingleAsync(term => term.TenantId == tenantId && term.Code == "NET30");
        net30.IsDefault.Should().BeFalse("the tenant already selected a default");
        net30.Name = "Tenant-customized Net 30";
        await context.SaveChangesAsync();

        (await PaymentTermBaselineSeeder.SeedTenantBaselineAsync(context, tenantId)).Should().Be(0);
        (await context.PaymentTerms.SingleAsync(term => term.Id == net30.Id)).Name
            .Should().Be("Tenant-customized Net 30");
    }

    [Fact]
    public async Task SeedAllActiveTenantsAsync_SeedsActiveTenantsOnly()
    {
        await using var context = CreateContext();
        var activeTenant = new Tenant { Id = Guid.NewGuid(), Code = "ACTIVE", Name = "Active", Status = TenantStatus.Active };
        var inactiveTenant = new Tenant { Id = Guid.NewGuid(), Code = "INACTIVE", Name = "Inactive", Status = TenantStatus.Inactive };
        context.Tenants.AddRange(activeTenant, inactiveTenant);
        await context.SaveChangesAsync();
        var seeder = new PaymentTermBaselineSeeder(
            context,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentTermBaselineSeeder>.Instance);

        (await seeder.SeedAllActiveTenantsAsync()).Should().Be(7);
        (await context.PaymentTerms.CountAsync(term => term.TenantId == activeTenant.Id)).Should().Be(7);
        (await context.PaymentTerms.CountAsync(term => term.TenantId == inactiveTenant.Id)).Should().Be(0);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"payment-term-baseline-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
