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
    public async Task GoodsEntryAndSaveGuard_ShouldUseAcceptedUninvoicedQuantitiesIncludingSplitLines()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var supplier = Supplier(tenant, "RECEIPT-SUP", "Receipt supplier");
        var po = new PurchaseOrder { Id = Guid.NewGuid(), TenantId = tenant,
            BusinessPartnerId = supplier.Id, OrderNumber = "PO-TEST", ProcurementCategory = ErpSystem.Core.Enums.ProcurementCategoryClass.Goods };
        var poLine = Guid.NewGuid();
        var accepted = new ErpSystem.Core.DTOs.Procurement.ProcurementAcceptedSupplyResolutionDto
        {
            Kind = ErpSystem.Core.Enums.ProcurementAcceptedSupplyKind.GoodsReceiptInspection,
            SourceId = po.Id, PurchaseOrderId = po.Id, BusinessPartnerId = supplier.Id,
            Lines = [new() { PurchaseOrderItemId = poLine, AcceptedQuantity = 8, UnitPrice = 100 }]
        };
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenant);
        var acceptance = new Mock<ErpSystem.Core.Interfaces.Procurement.IProcurementAcceptedSupplyService>();
        acceptance.Setup(x => x.ResolveAsync(accepted.Kind, po.Id, po.Id, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(accepted);
        var service = new ErpSystem.Api.Services.Finance.AP.VendorInvoiceService(
            new UnitOfWork(db), user.Object,
            Mock.Of<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<ErpSystem.Api.Services.Finance.AP.VendorInvoiceService>>(),
            Mock.Of<ErpSystem.Core.Interfaces.Numbering.IDocumentNumberingService>(), Mock.Of<IWorkflowService>(),
            acceptedSupply: acceptance.Object);
        ErpSystem.Core.Entities.Finance.VendorInvoice Invoice(Guid invoiceTenant,
            ErpSystem.Core.Entities.Finance.VendorInvoiceStatus status, decimal quantity) => new()
        {
            Id = Guid.NewGuid(), TenantId = invoiceTenant, PurchaseOrderId = po.Id,
            SupplierId = supplier.Id, SupplierName = supplier.Name, InvoiceNumber = Guid.NewGuid().ToString(), Status = status,
            LineItems = [new() { Id = Guid.NewGuid(), TenantId = invoiceTenant,
                PurchaseOrderItemId = poLine, Description = "Goods", Quantity = quantity }]
        };
        var approved = Invoice(tenant, ErpSystem.Core.Entities.Finance.VendorInvoiceStatus.Approved, 3);
        db.Suppliers.Add(supplier);
        db.PurchaseOrders.Add(po);
        db.Set<ErpSystem.Core.Entities.Finance.VendorInvoice>().AddRange(approved,
            Invoice(tenant, ErpSystem.Core.Entities.Finance.VendorInvoiceStatus.Draft, 99),
            Invoice(tenant, ErpSystem.Core.Entities.Finance.VendorInvoiceStatus.Rejected, 99),
            Invoice(Guid.NewGuid(), ErpSystem.Core.Entities.Finance.VendorInvoiceStatus.Approved, 99));
        await db.SaveChangesAsync();
        var entry = await service.GetGoodsInvoiceEntryAsync(po.Id);
        entry.Lines.Single().AcceptedQuantity.Should().Be(8);
        entry.Lines.Single().InvoicedQuantity.Should().Be(3);
        entry.Lines.Single().AvailableQuantity.Should().Be(5);
        Func<Task> excludeApproved = () => service.GetGoodsInvoiceEntryAsync(po.Id, approved.Id);
        await excludeApproved.Should().ThrowAsync<InvalidOperationException>();

        var request = new VendorInvoiceCreateDto { PurchaseOrderId = po.Id,
            LineItems = [new() { PurchaseOrderItemId = poLine, Quantity = 3 }, new() { PurchaseOrderItemId = poLine, Quantity = 2 }] };
        var method = service.GetType().GetMethod("ResolveAcceptedSupplyForCreateAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Task Check() => (Task)method.Invoke(service, new object?[] { request, supplier, CancellationToken.None, null })!;
        await Check();
        request.LineItems[1].Quantity = 3;
        await ((Func<Task>)Check).Should().ThrowAsync<InvalidOperationException>().WithMessage("*accepted, uninvoiced*");
        request.LineItems[1].Quantity = 2;
        request.LineItems[1].PurchaseOrderItemId = Guid.NewGuid();
        await ((Func<Task>)Check).Should().ThrowAsync<InvalidOperationException>();
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task EntryLookup_ShouldExposeApprovedOnboardingWithoutWritingOrChangingReportIdentities()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fresh = Partner(tenant, "NEW-001", "Fresh onboarded supplier");
        var linked = Partner(tenant, "LINKED-001", "Linked supplier");
        var blocked = Partner(tenant, "BLOCKED-001", "Inactive Finance mapping");
        var pending = Partner(tenant, "PENDING-001", "Pending");
        pending.RegistrationStatus = "Pending";
        var blacklisted = Partner(tenant, "BLACKLISTED", "Blacklisted");
        blacklisted.IsBlacklisted = true;
        var inactive = Partner(tenant, "INACTIVE", "Inactive");
        inactive.IsActive = false;
        var deleted = Partner(tenant, "DELETED", "Deleted");
        deleted.IsDeleted = true;
        var customer = Partner(tenant, "CUSTOMER", "Customer");
        customer.PartnerType = "Customer";
        var canonical = Supplier(tenant, linked.PartnerCode, linked.PartnerName);
        var sameNameDifferentIdentity = Supplier(tenant, "DIFFERENT-CODE", fresh.PartnerName);
        db.Suppliers.AddRange(canonical, sameNameDifferentIdentity,
            Supplier(tenant, blocked.PartnerCode, blocked.PartnerName, isActive: false));
        db.BusinessPartners.AddRange(fresh, linked, blocked, pending, blacklisted, inactive,
            deleted, customer, Partner(Guid.NewGuid(), "OTHER-TENANT", "Foreign"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var controller = CreateController(db, tenant);
        var action = await controller.GetSupplierEntryOptions(CancellationToken.None);
        var entries = (IReadOnlyList<ApInvoiceSupplierEntryDto>)((OkObjectResult)action.Result!).Value!;
        entries.Should().HaveCount(3);
        entries.Should().ContainSingle(x => x.Id == fresh.Id && x.BusinessPartnerId == fresh.Id);
        entries.Should().ContainSingle(x => x.Id == canonical.Id && x.BusinessPartnerId == linked.Id);
        entries.Should().ContainSingle(x => x.Id == sameNameDifferentIdentity.Id && x.BusinessPartnerId == null);
        db.ChangeTracker.Entries().Should().BeEmpty();
        (await db.Suppliers.CountAsync()).Should().Be(3);

        var reportAction = await controller.GetSuppliers(CancellationToken.None);
        var reports = (IReadOnlyList<ApInvoiceSupplierDto>)((OkObjectResult)reportAction.Result!).Value!;
        reports.Should().HaveCount(2).And.NotContain(x => x.Id == fresh.Id);
        (await CreateController(db, null).GetSupplierEntryOptions(CancellationToken.None))
            .Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CommandResolver_ShouldRetainCanonicalIdentityAndRejectUnsafeOnboardingMappings()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenant);
        user.SetupGet(x => x.UserName).Returns("uat-maker");
        var service = new ErpSystem.Api.Services.Finance.AP.VendorInvoiceService(
            new ErpSystem.Data.UnitOfWork(db), user.Object,
            Mock.Of<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<ErpSystem.Api.Services.Finance.AP.VendorInvoiceService>>(),
            Mock.Of<ErpSystem.Core.Interfaces.Numbering.IDocumentNumberingService>(),
            Mock.Of<IWorkflowService>());
        var method = service.GetType().GetMethod("ResolveSupplierForInvoiceAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Task<Supplier> Resolve(Guid id) => (Task<Supplier>)method.Invoke(service, new object[] { id, CancellationToken.None })!;
        var fresh = Partner(tenant, "FRESH", "Shared display name");
        var unrelated = Supplier(tenant, "UNRELATED", fresh.PartnerName);
        db.BusinessPartners.Add(fresh);
        db.Suppliers.Add(unrelated);
        await db.SaveChangesAsync();
        var resolved = await Resolve(fresh.Id);
        resolved.Id.Should().NotBe(fresh.Id).And.NotBe(unrelated.Id);
        resolved.SupplierCode.Should().Be("FRESH");
        await db.SaveChangesAsync();
        (await Resolve(fresh.Id)).Id.Should().Be(resolved.Id);
        (await Resolve(resolved.Id)).Id.Should().Be(resolved.Id);
        (await db.Suppliers.CountAsync()).Should().Be(2);

        fresh.RegistrationStatus = "Pending";
        await db.SaveChangesAsync();
        Func<Task> pending = () => Resolve(fresh.Id);
        await pending.Should().ThrowAsync<InvalidOperationException>();
        fresh.RegistrationStatus = "Active";
        resolved.IsActive = false;
        await db.SaveChangesAsync();
        Func<Task> inactive = () => Resolve(fresh.Id);
        await inactive.Should().ThrowAsync<InvalidOperationException>();
        resolved.IsActive = true;
        resolved.IsDeleted = true;
        await db.SaveChangesAsync();
        Func<Task> deleted = () => Resolve(fresh.Id);
        await deleted.Should().ThrowAsync<InvalidOperationException>();
        var foreign = Partner(Guid.NewGuid(), "FOREIGN", "Foreign");
        db.BusinessPartners.Add(foreign);
        await db.SaveChangesAsync();
        Func<Task> tenantDenied = () => Resolve(foreign.Id);
        await tenantDenied.Should().ThrowAsync<KeyNotFoundException>();
    }

    private static BusinessPartner Partner(Guid tenant, string code, string name) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant, PartnerCode = code, PartnerName = name,
        PartnerType = "Supplier", RegistrationStatus = "Active", IsActive = true
    };

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
        typeof(VendorInvoiceController).GetMethod(nameof(VendorInvoiceController.GetSupplierEntryOptions))!
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should().ContainSingle(a => a.Policy == FinancePermissions.ViewFinance);
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
