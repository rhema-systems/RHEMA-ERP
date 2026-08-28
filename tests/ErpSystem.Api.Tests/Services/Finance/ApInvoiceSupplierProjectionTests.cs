using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ApInvoiceSupplierProjectionTests
{
    [Fact]
    [Trait("Category", "TenantIsolation")]
    public async Task Lookup_ShouldReturnOnlyActiveCanonicalSuppliersForCurrentTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var paymentTermId = Guid.NewGuid();
        await using var db = CreateContext();
        var expected = Supplier(
            tenantId,
            "TDC-DEMO-SUP-001",
            "Akua Payables",
            paymentTermId: paymentTermId);

        db.Suppliers.AddRange(
            expected,
            Supplier(tenantId, "INACTIVE-FLAG", "Inactive Flag", isActive: false),
            Supplier(tenantId, "INACTIVE-STATUS", "Inactive Status", status: "Inactive"),
            Supplier(tenantId, "DELETED", "Deleted Supplier", isDeleted: true),
            Supplier(otherTenantId, "DEFAULT-SUP", "Other Tenant Supplier"));
        await db.SaveChangesAsync();

        var action = await CreateController(db, tenantId)
            .GetSuppliers(CancellationToken.None);

        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        var suppliers = ok.Value.Should()
            .BeAssignableTo<IReadOnlyList<ApInvoiceSupplierDto>>()
            .Subject;
        suppliers.Should().ContainSingle();
        suppliers.Single().Should().BeEquivalentTo(new ApInvoiceSupplierDto
        {
            Id = expected.Id,
            Code = expected.SupplierCode,
            Name = expected.Name,
            PaymentTermId = paymentTermId
        });
    }

    [Fact]
    [Trait("Category", "TenantIsolation")]
    public async Task Lookup_WithoutTenant_ShouldFailClosed()
    {
        await using var db = CreateContext();
        db.Suppliers.Add(Supplier(Guid.NewGuid(), "MUST-NOT-LEAK", "Unscoped Supplier"));
        await db.SaveChangesAsync();

        var action = await CreateController(db, null)
            .GetSuppliers(CancellationToken.None);

        action.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    [Trait("Category", "FinanceSecurity")]
    public void Lookup_ShouldRequireFinanceReadPermission()
    {
        var action = typeof(VendorInvoiceController).GetMethod(
            nameof(VendorInvoiceController.GetSuppliers),
            BindingFlags.Instance | BindingFlags.Public);

        action.Should().NotBeNull();
        action!.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(attribute => attribute.Policy)
            .Should()
            .ContainSingle(policy => policy == FinancePermissions.ViewFinance);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ap-invoice-supplier-projection-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static VendorInvoiceController CreateController(
        ApplicationDbContext db,
        Guid? tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        return new VendorInvoiceController(
            Mock.Of<IVendorInvoiceService>(),
            currentUser.Object,
            db);
    }

    private static Supplier Supplier(
        Guid tenantId,
        string code,
        string name,
        bool isActive = true,
        string status = "Active",
        bool isDeleted = false,
        Guid? paymentTermId = null) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCode = code,
            Name = name,
            IsActive = isActive,
            Status = status,
            IsDeleted = isDeleted,
            PaymentTermId = paymentTermId
        };
}
