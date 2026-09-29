using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesProviderSelectionServiceTests
{
    [Fact]
    public async Task ApprovedProviderWithActiveContractCanBeSelected()
    {
        var (db, tenantId, provider, contract) = await SeedAsync();
        await using (db)
        {
            var selected = await new FacilitiesProviderSelectionService(db).ResolveAsync(
                tenantId, provider.Id.ToString(), contract.Id.ToString(), DateTime.UtcNow);

            selected.Should().NotBeNull();
            selected!.Contract.ContractNumber.Should().Be("CT-001");
        }
    }

    [Fact]
    public async Task ContractRemainsUsableThroughItsEndDate()
    {
        var (db, tenantId, provider, contract) = await SeedAsync();
        await using (db)
        {
            contract.EndDate = DateTime.UtcNow.Date;
            await db.SaveChangesAsync();

            var selected = await new FacilitiesProviderSelectionService(db).ResolveAsync(
                tenantId, provider.Id.ToString(), contract.Id.ToString(), DateTime.UtcNow);
            selected.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task SuspendedProviderCannotBeRoutedToMaintenance()
    {
        var (db, tenantId, provider, contract) = await SeedAsync();
        await using (db)
        {
            provider.RegistrationStatus = "Suspended";
            await db.SaveChangesAsync();

            var action = () => new FacilitiesProviderSelectionService(db).ResolveAsync(
                tenantId, provider.Id.ToString(), contract.Id.ToString(), DateTime.UtcNow);
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not approved*");
        }
    }

    [Fact]
    public async Task ExpiredOrUnrelatedContractCannotBeRoutedToMaintenance()
    {
        var (db, tenantId, provider, contract) = await SeedAsync();
        await using (db)
        {
            contract.EndDate = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();

            var action = () => new FacilitiesProviderSelectionService(db).ResolveAsync(
                tenantId, provider.Id.ToString(), contract.Id.ToString(), DateTime.UtcNow);
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not active*");
        }
    }

    [Fact]
    public async Task PartialProviderSelectionIsRejected()
    {
        var (db, tenantId, provider, _) = await SeedAsync();
        await using (db)
        {
            var action = () => new FacilitiesProviderSelectionService(db).ResolveAsync(
                tenantId, provider.Id.ToString(), null, DateTime.UtcNow);
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*both an approved service provider*");
        }
    }

    private static async Task<(ApplicationDbContext Db, Guid TenantId, BusinessPartner Provider, Contract Contract)> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantId);
        var provider = new BusinessPartner
        {
            TenantId = tenantId,
            PartnerCode = "BP-001",
            PartnerName = "Facilities Contractor",
            PartnerType = "Contractor",
            RegistrationStatus = "Active",
            ApprovalStatus = "Approved",
            IsActive = true
        };
        var contract = new Contract
        {
            TenantId = tenantId,
            BusinessPartnerId = provider.Id,
            ContractNumber = "CT-001",
            ContractTitle = "Facilities service",
            Status = "Active",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        db.BusinessPartners.Add(provider);
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return (db, tenantId, provider, contract);
    }
}
