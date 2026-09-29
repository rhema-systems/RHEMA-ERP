using System.Text.Json;
using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesProviderOptionsControllerTests
{
    [Fact]
    public async Task InvoicesIncludeDirectApInvoicesOnlyForCurrentTenantBusinessPartner()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantId);
        var provider = Partner(tenantId, "Approved");
        var otherProvider = Partner(Guid.NewGuid(), "Approved");
        var sameNameProvider = Partner(tenantId, "Approved");
        db.BusinessPartners.AddRange(provider, otherProvider, sameNameProvider);
        var deleted = Invoice(tenantId, provider.Id, "DELETED", null);
        deleted.IsDeleted = true;
        db.VendorInvoices.AddRange(
            Invoice(tenantId, provider.Id, "DIRECT", null),
            Invoice(tenantId, sameNameProvider.Id, "SAME-NAME-OTHER-PARTNER", null),
            Invoice(Guid.NewGuid(), provider.Id, "OTHER-TENANT", null), deleted);
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        var controller = new FacilitiesProviderOptionsController(db, user.Object);

        var result = await controller.GetInvoices(provider.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var invoices = json.RootElement.GetProperty("data");
        invoices.GetArrayLength().Should().Be(1);
        invoices[0].GetProperty("InvoiceNumber").GetString().Should().Be("DIRECT");
        invoices[0].GetProperty("PurchaseOrderId").ValueKind.Should().Be(JsonValueKind.Null);
        (await controller.GetInvoices(otherProvider.Id, CancellationToken.None))
            .Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ReturnsOnlyCurrentTenantsApprovedProvidersAndActiveContracts()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantId);
        var approved = Partner(tenantId, "Approved");
        var suspended = Partner(tenantId, "Suspended");
        var otherTenant = Partner(Guid.NewGuid(), "Approved");
        db.BusinessPartners.AddRange(approved, suspended, otherTenant);
        db.Contracts.AddRange(
            Contract(tenantId, approved.Id, "Active", DateTime.UtcNow.Date),
            Contract(tenantId, approved.Id, "Expired", DateTime.UtcNow.Date.AddDays(-1)));
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);

        var result = await new FacilitiesProviderOptionsController(db, user.Object).Get(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var providers = json.RootElement.GetProperty("data");
        providers.GetArrayLength().Should().Be(1);
        providers[0].GetProperty("Id").GetGuid().Should().Be(approved.Id);
        providers[0].GetProperty("Contracts").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task RatesRequireApprovedProviderOwnedContractAndNonOverlappingTerms()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantId);
        var approved = Partner(tenantId, "Approved");
        var suspended = Partner(tenantId, "Suspended");
        var otherTenant = Partner(Guid.NewGuid(), "Approved");
        var contract = Contract(tenantId, approved.Id, "CT-1", new DateTime(2027, 12, 31));
        contract.StartDate = new DateTime(2026, 1, 1);
        var unrelatedContract = Contract(tenantId, suspended.Id, "CT-2", new DateTime(2027, 12, 31));
        db.BusinessPartners.AddRange(approved, suspended, otherTenant);
        db.Contracts.AddRange(contract, unrelatedContract);
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        var controller = new FacilitiesProviderOptionsController(db, user.Object);
        var request = new ProviderRateRequest(contract.Id, "Cleaning", "visit", 125.5m, "ghs",
            new DateTime(2026, 10, 1), new DateTime(2026, 12, 31), true);

        (await controller.CreateRate(suspended.Id, request, CancellationToken.None))
            .Should().BeOfType<BadRequestObjectResult>();
        (await controller.CreateRate(approved.Id, request with { ContractId = unrelatedContract.Id }, CancellationToken.None))
            .Should().BeOfType<BadRequestObjectResult>();
        (await controller.CreateRate(approved.Id, request with { EffectiveTo = new DateTime(2028, 1, 1) }, CancellationToken.None))
            .Should().BeOfType<BadRequestObjectResult>();
        (await controller.CreateRate(approved.Id, request, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.CreateRate(approved.Id, request with { EffectiveFrom = new DateTime(2026, 12, 1) }, CancellationToken.None))
            .Should().BeOfType<ConflictObjectResult>();
        (await controller.GetRates(otherTenant.Id, CancellationToken.None))
            .Should().BeOfType<NotFoundResult>();

        var rate = await db.EstateFacilityProviderRates.SingleAsync();
        rate.Currency.Should().Be("GHS");
        (await controller.UpdateRate(approved.Id, rate.Id, request with { IsActive = false }, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.CreateRate(approved.Id, request, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        var result = (await controller.GetRates(approved.Id, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>().Subject;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        json.RootElement.GetProperty("data").GetArrayLength().Should().Be(2);
        json.RootElement.GetProperty("data")[0].GetProperty("ContractNumber").GetString().Should().Be("CT-1");
    }

    private static BusinessPartner Partner(Guid tenantId, string status) => new()
    {
        TenantId = tenantId, PartnerCode = Guid.NewGuid().ToString("N")[..8],
        PartnerName = status + " supplier", PartnerType = "Supplier",
        RegistrationStatus = status, ApprovalStatus = "Approved", IsActive = true
    };

    private static Contract Contract(Guid tenantId, Guid providerId, string number, DateTime endDate) => new()
    {
        TenantId = tenantId, BusinessPartnerId = providerId, ContractNumber = number,
        ContractTitle = "Service", Status = "Active", StartDate = DateTime.UtcNow.Date.AddDays(-10),
        EndDate = endDate
    };

    private static VendorInvoice Invoice(Guid tenantId, Guid partnerId, string number, Guid? purchaseOrderId) => new()
    {
        TenantId = tenantId, BusinessPartnerId = partnerId, SupplierName = "AP supplier",
        InvoiceNumber = number, InvoiceDate = DateTime.UtcNow,
        PurchaseOrderId = purchaseOrderId, TotalAmount = 100, CurrencyCode = "GHS"
    };
}
