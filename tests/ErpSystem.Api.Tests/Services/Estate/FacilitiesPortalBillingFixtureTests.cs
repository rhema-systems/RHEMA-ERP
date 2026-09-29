using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesPortalBillingFixtureTests
{
    [Fact]
    public async Task SeededLeasedUnitShowsOnlyReleasedBillsForItsCustomer()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantId);
        db.EstateManagedAssets.Add(new EstateManagedAsset
        {
            TenantId = tenantId,
            AssetCode = "TEST-FAC-UNIT-001",
            ProjectUnitCode = "TEST-FAC-APT-101",
            Name = "Facilities test apartment",
            CustomerBusinessPartnerId = customerId,
            Status = EstateManagedAssetStatus.Leased,
            DateOfTenancy = DateTime.UtcNow.Date.AddMonths(-1),
            ExternalLeaseTermMonths = 12
        });
        db.Invoices.AddRange(
            Bill(tenantId, customerId, "FAC-DRAFT", InvoiceStatus.Draft, true),
            Bill(tenantId, customerId, "FAC-APPROVED", InvoiceStatus.Approved, true),
            Bill(tenantId, customerId, "FAC-SENT", InvoiceStatus.Sent, true),
            Bill(tenantId, customerId, "FAC-PARTIAL", InvoiceStatus.PartiallyPaid, true),
            Bill(tenantId, customerId, "UNRELATED", InvoiceStatus.Sent, false),
            Bill(tenantId, otherCustomerId, "OTHER-CUSTOMER", InvoiceStatus.Sent, true),
            Bill(Guid.NewGuid(), customerId, "OTHER-TENANT", InvoiceStatus.Sent, true));
        await db.SaveChangesAsync();

        var property = await db.EstateManagedAssets.SingleAsync(asset =>
            asset.AssetCode == "TEST-FAC-UNIT-001");
        Assert.Equal(customerId, property.CustomerBusinessPartnerId);
        var visible = await db.Invoices.AsNoTracking()
            .ForCustomerProperties(tenantId, [customerId])
            .Select(invoice => invoice.InvoiceNumber)
            .OrderBy(number => number)
            .ToListAsync();

        Assert.Equal(["FAC-PARTIAL", "FAC-SENT"], visible);
    }

    private static Invoice Bill(Guid tenantId, Guid customerId, string number,
        InvoiceStatus status, bool facilitiesSource) => new()
    {
        TenantId = tenantId,
        BusinessPartnerId = customerId,
        CustomerName = "Facilities test customer",
        InvoiceNumber = number,
        InvoiceDate = DateTime.UtcNow.Date,
        Status = status,
        Reference = "TEST-FAC-UNIT-001 TEST-FAC-APT-101",
        Notes = facilitiesSource ? "Source: Estate / Facilities -> Finance AR" : "Other module",
        CurrencyCode = "GHS",
        TotalAmount = 250m
    };
}
