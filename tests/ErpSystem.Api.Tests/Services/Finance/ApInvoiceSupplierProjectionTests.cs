using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.AP;
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
    [Trait("Category", "TenantIsolation")]
    public async Task EntryLookup_ShouldMergeExactIdentitiesAndIncludeUnpairedApprovedPartners()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var canonical = Supplier(tenantId, "MATCH-001", "Legacy supplier name");
        var matchedPartner = Partner(tenantId, "MATCH-001", "Matched Business Partner", "EUR");
        var usdPartner = Partner(tenantId, "SUP260001", "USD Supplier", "USD");

        db.Suppliers.Add(canonical);
        db.BusinessPartners.AddRange(
            matchedPartner,
            usdPartner,
            Partner(tenantId, "PENDING", "Pending Supplier", "GHS", approvalStatus: "Pending"),
            Partner(tenantId, "CUSTOMER", "Customer Only", "GHS", partnerType: "Customer"));
        await db.SaveChangesAsync();

        var action = await CreateController(db, tenantId)
            .GetEntrySuppliers(CancellationToken.None);

        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        var options = ok.Value.Should()
            .BeAssignableTo<IReadOnlyList<ApInvoiceSupplierEntryOptionDto>>()
            .Subject;
        options.Should().HaveCount(2);
        options.Should().ContainEquivalentOf(new ApInvoiceSupplierEntryOptionDto
        {
            Id = canonical.Id,
            SupplierId = canonical.Id,
            BusinessPartnerId = matchedPartner.Id,
            Code = matchedPartner.PartnerCode,
            Name = matchedPartner.PartnerName,
            Currency = "EUR"
        }, configuration => configuration.Excluding(item => item.PaymentTermId));
        options.Should().ContainEquivalentOf(new ApInvoiceSupplierEntryOptionDto
        {
            Id = usdPartner.Id,
            BusinessPartnerId = usdPartner.Id,
            Code = usdPartner.PartnerCode,
            Name = usdPartner.PartnerName,
            Currency = "USD"
        }, configuration => configuration.Excluding(item => item.PaymentTermId));
    }

    [Fact]
    [Trait("Category", "AccountsPayable")]
    public async Task FirstApCommand_ShouldMaterializeAndLinkApprovedPartnerWithoutNameMatching()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        var partner = Partner(tenantId, "BP-SUP-001", "Controlled Supplier", "USD");
        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync();

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(item => item.HasActiveTransaction).Returns(true);
        unitOfWork.Setup(item => item.AcquireTransactionLockAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId.ToString());
        currentUser.SetupGet(item => item.UserName).Returns("finance-maker");

        var result = await new ApSupplierIdentityService(
                db, unitOfWork.Object, currentUser.Object)
            .ResolveByBusinessPartnerAsync(partner.Id);

        result.BusinessPartnerId.Should().Be(partner.Id);
        var supplier = await db.Suppliers.SingleAsync();
        supplier.Id.Should().Be(result.SupplierId);
        supplier.SupplierCode.Should().Be(partner.PartnerCode);
        supplier.Name.Should().Be(partner.PartnerName);
        supplier.Notes.Should().Contain("Finance AP projection");
        var link = await db.ApSupplierIdentityLinks.SingleAsync();
        link.BusinessPartnerId.Should().Be(partner.Id);
        link.SupplierId.Should().Be(supplier.Id);
        link.MappingSource.Should().Be("BusinessPartnerProjection");
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

    private static BusinessPartner Partner(
        Guid tenantId,
        string code,
        string name,
        string currency,
        string approvalStatus = "Approved",
        string registrationStatus = "Active",
        string partnerType = "Supplier") => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = code,
            PartnerName = name,
            PartnerType = partnerType,
            Currency = currency,
            ApprovalStatus = approvalStatus,
            RegistrationStatus = registrationStatus,
            IsActive = true,
            IsBlacklisted = false
        };
}
