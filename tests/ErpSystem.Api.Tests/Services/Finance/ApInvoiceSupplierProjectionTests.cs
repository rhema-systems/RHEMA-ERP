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
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        var action = await controller.GetEntrySuppliers(CancellationToken.None);
        var entries = (IReadOnlyList<ApInvoiceSupplierEntryOptionDto>)((OkObjectResult)action.Result!).Value!;
        entries.Should().HaveCount(3);
        entries.Should().ContainSingle(x => x.Id == fresh.Id && x.BusinessPartnerId == fresh.Id);
        entries.Should().ContainSingle(x => x.Id == canonical.Id && x.BusinessPartnerId == linked.Id);
        entries.Should().ContainSingle(x => x.Id == sameNameDifferentIdentity.Id && x.BusinessPartnerId == null);
        db.ChangeTracker.Entries().Should().BeEmpty();
        (await db.Suppliers.CountAsync()).Should().Be(3);

        var reportAction = await controller.GetSuppliers(CancellationToken.None);
        var reports = (IReadOnlyList<ApInvoiceSupplierDto>)((OkObjectResult)reportAction.Result!).Value!;
        reports.Should().HaveCount(2).And.NotContain(x => x.Id == fresh.Id);
        (await CreateController(db, null).GetEntrySuppliers(CancellationToken.None))
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
        var unitOfWork = new ErpSystem.Data.UnitOfWork(db);
        var identityService = new ApSupplierIdentityService(db, unitOfWork, user.Object);
        var service = new ErpSystem.Api.Services.Finance.AP.VendorInvoiceService(
            unitOfWork, user.Object,
            Mock.Of<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<ErpSystem.Api.Services.Finance.AP.VendorInvoiceService>>(),
            Mock.Of<ErpSystem.Core.Interfaces.Numbering.IDocumentNumberingService>(),
            Mock.Of<IWorkflowService>(), apSupplierIdentityService: identityService);
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
        PartnerType = "Supplier", ApprovalStatus = "Approved", RegistrationStatus = "Active", IsActive = true
    };

    [Fact]
    public async Task LinkedApprovedContractor_ShouldBeSelectableAndKeepCanonicalIdentityForCreateAndEdit()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var partner = Partner(tenant, "CONT-GH-ADOM-BUILD", "Adom contractor");
        partner.PartnerType = "Contractor";
        partner.RegistrationStatus = "Approved";
        var supplier = Supplier(tenant, partner.PartnerCode, partner.PartnerName);
        db.BusinessPartners.Add(partner);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var entries = await EntryOptions(db, tenant);
        entries.Should().ContainSingle(x => x.Id == supplier.Id && x.BusinessPartnerId == partner.Id);
        var service = CreateInvoiceService(db, tenant);
        (await Resolve(service, "ResolveSupplierForInvoiceAsync", partner.Id)).Id.Should().Be(supplier.Id);
        (await Resolve(service, "ResolveSupplierForInvoiceAsync", supplier.Id)).Id.Should().Be(supplier.Id);
        (await Resolve(service, "ResolveExistingSupplierForInvoiceAsync", supplier.Id)).Id.Should().Be(supplier.Id);
        db.ChangeTracker.HasChanges().Should().BeFalse();
        (await db.Suppliers.CountAsync()).Should().Be(1);
        (await db.BusinessPartners.SingleAsync()).PartnerType.Should().Be("Contractor");
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("InactivePartner")]
    [InlineData("DeletedPartner")]
    [InlineData("BlacklistedPartner")]
    [InlineData("InactiveSupplier")]
    [InlineData("DeletedSupplier")]
    [InlineData("BlacklistedSupplier")]
    [InlineData("Customer")]
    [InlineData("ArbitraryType")]
    [InlineData("AmbiguousPartner")]
    [InlineData("AmbiguousSupplier")]
    public async Task UnsafeContractorIdentity_ShouldBeExcludedAndRejectedByCreateAndEdit(string scenario)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var partner = Partner(tenant, "CONTRACTOR", "Contractor");
        partner.PartnerType = "Contractor";
        var supplier = Supplier(tenant, partner.PartnerCode, partner.PartnerName);
        switch (scenario)
        {
            case "Pending": partner.RegistrationStatus = "Pending"; break;
            case "InactivePartner": partner.IsActive = false; break;
            case "DeletedPartner": partner.IsDeleted = true; break;
            case "BlacklistedPartner": partner.IsBlacklisted = true; break;
            case "InactiveSupplier": supplier.IsActive = false; break;
            case "DeletedSupplier": supplier.IsDeleted = true; break;
            case "BlacklistedSupplier": supplier.IsBlacklisted = true; break;
            case "Customer": partner.PartnerType = "Customer"; break;
            case "ArbitraryType": partner.PartnerType = "Unrecognised"; break;
            case "AmbiguousPartner": db.BusinessPartners.Add(Partner(tenant, partner.PartnerCode, "Duplicate")); break;
            case "AmbiguousSupplier": db.Suppliers.Add(Supplier(tenant, partner.PartnerCode, "Duplicate")); break;
        }
        db.BusinessPartners.Add(partner);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await EntryOptions(db, tenant)).Should().BeEmpty();
        var service = CreateInvoiceService(db, tenant);
        foreach (var method in new[] { "ResolveSupplierForInvoiceAsync", "ResolveExistingSupplierForInvoiceAsync" })
        {
            Func<Task> attempt = () => Resolve(service, method, supplier.Id);
            await attempt.Should().ThrowAsync<Exception>();
        }
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task UnlinkedOrForeignContractor_ShouldNotBeOnboardedByOrdinaryInvoiceEntry()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var partner = Partner(tenant, "UNLINKED", "No Finance identity");
        partner.PartnerType = "Contractor";
        var foreign = Supplier(Guid.NewGuid(), partner.PartnerCode, "Foreign supplier");
        db.BusinessPartners.Add(partner);
        db.Suppliers.Add(foreign);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        (await EntryOptions(db, tenant)).Should().BeEmpty();
        var service = CreateInvoiceService(db, tenant);
        Func<Task> unlinked = () => Resolve(service, "ResolveSupplierForInvoiceAsync", partner.Id);
        await unlinked.Should().ThrowAsync<InvalidOperationException>().WithMessage("*active linked Finance supplier*");
        Func<Task> wrongTenant = () => Resolve(service, "ResolveSupplierForInvoiceAsync", foreign.Id);
        await wrongTenant.Should().ThrowAsync<KeyNotFoundException>();
        Func<Task> editWrongTenant = () => Resolve(service, "ResolveExistingSupplierForInvoiceAsync", foreign.Id);
        await editWrongTenant.Should().ThrowAsync<KeyNotFoundException>();
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task ApprovedLandedCostContractorHandoff_ShouldRemainSupportedButPendingMustFail()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var partner = Partner(tenant, "COST-CONTRACTOR", "Approved freight contractor");
        partner.PartnerType = "Contractor";
        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync();
        var service = CreateInvoiceService(db, tenant);
        var method = service.GetType().GetMethod("ResolveSupplierIdentityForInvoiceAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Task<Supplier> Handoff() => (Task<Supplier>)method.Invoke(service, new object[] { partner.Id, true, CancellationToken.None })!;
        var supplier = await Handoff();
        supplier.SupplierCode.Should().Be(partner.PartnerCode);
        await db.SaveChangesAsync();
        partner.RegistrationStatus = "Pending";
        await db.SaveChangesAsync();
        await ((Func<Task>)(() => Handoff())).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DraftEdit_ShouldRevalidateInactiveCanonicalSupplierBeforeChangingInvoice()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var supplier = Supplier(tenant, "INACTIVE-EDIT", "Inactive", isActive: false);
        var invoice = new ErpSystem.Core.Entities.Finance.VendorInvoice
        {
            Id = Guid.NewGuid(), TenantId = tenant, SupplierId = supplier.Id, SupplierName = supplier.Name,
            InvoiceNumber = "VI-UNTOUCHED", Status = ErpSystem.Core.Entities.Finance.VendorInvoiceStatus.Draft
        };
        db.Suppliers.Add(supplier);
        db.Set<ErpSystem.Core.Entities.Finance.VendorInvoice>().Add(invoice);
        await db.SaveChangesAsync();
        Func<Task> update = () => CreateInvoiceService(db, tenant).UpdateAsync(new VendorInvoiceUpdateDto
        {
            Id = invoice.Id, SupplierInvoiceNumber = "MUST-NOT-SAVE", InvoiceDate = DateTime.Today
        });
        await update.Should().ThrowAsync<InvalidOperationException>().WithMessage("*inactive*invoice entry*");
        invoice.SupplierInvoiceNumber.Should().BeNull();
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Theory]
    [InlineData("Both")]
    [InlineData("CustomerAndSupplier")]
    [InlineData("Vendor")]
    [InlineData("Manufacturer")]
    public async Task CombinedSupplierPartner_ShouldRetainApprovedOnboarding(string partnerType)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var partner = Partner(tenant, "COMBINED", "Combined supplier");
        partner.PartnerType = partnerType;
        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync();
        (await EntryOptions(db, tenant)).Should().ContainSingle(x => x.Id == partner.Id);
        var supplier = await Resolve(CreateInvoiceService(db, tenant), "ResolveSupplierForInvoiceAsync", partner.Id);
        supplier.SupplierCode.Should().Be(partner.PartnerCode);
        supplier.SupplierType.Should().Be(partnerType == "Manufacturer" ? "Manufacturer" : "Vendor");
    }

    private static ErpSystem.Api.Services.Finance.AP.VendorInvoiceService CreateInvoiceService(ApplicationDbContext db, Guid tenant)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenant);
        user.SetupGet(x => x.UserName).Returns("uat-maker");
        var unitOfWork = new UnitOfWork(db);
        return new(unitOfWork, user.Object,
            Mock.Of<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<ErpSystem.Api.Services.Finance.AP.VendorInvoiceService>>(),
            Mock.Of<ErpSystem.Core.Interfaces.Numbering.IDocumentNumberingService>(), Mock.Of<IWorkflowService>(),
            apSupplierIdentityService: new ApSupplierIdentityService(db, unitOfWork, user.Object));
    }

    private static Task<Supplier> Resolve(ErpSystem.Api.Services.Finance.AP.VendorInvoiceService service, string method, Guid id) =>
        (Task<Supplier>)service.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, new object[] { id, CancellationToken.None })!;

    private static async Task<IReadOnlyList<ApInvoiceSupplierEntryOptionDto>> EntryOptions(ApplicationDbContext db, Guid tenant)
    {
        var result = await CreateController(db, tenant).GetEntrySuppliers(CancellationToken.None);
        return (IReadOnlyList<ApInvoiceSupplierEntryOptionDto>)((OkObjectResult)result.Result!).Value!;
    }

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
        partner.DefaultApAccountId = Guid.NewGuid();
        partner.DefaultExpenseAccountId = Guid.NewGuid();
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
        supplier.DefaultApAccountId.Should().Be(partner.DefaultApAccountId);
        supplier.DefaultExpenseAccountId.Should().Be(partner.DefaultExpenseAccountId);
        supplier.Notes.Should().Contain("Finance AP projection");
        var link = await db.ApSupplierIdentityLinks.SingleAsync();
        link.BusinessPartnerId.Should().Be(partner.Id);
        link.SupplierId.Should().Be(supplier.Id);
        link.MappingSource.Should().Be("BusinessPartnerProjection");
    }

    [Fact]
    public async Task EntryLookup_ShouldHonorDurablePairingAfterPartnerCodeChanges()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var partner = Partner(tenant, "PARTNER-ORIGINAL", "Linked supplier");
        var supplier = Supplier(tenant, partner.PartnerCode, partner.PartnerName);
        db.BusinessPartners.Add(partner);
        db.Suppliers.Add(supplier);
        db.ApSupplierIdentityLinks.Add(new ErpSystem.Core.Entities.Finance.ApSupplierIdentityLink
        {
            TenantId = tenant,
            BusinessPartnerId = partner.Id,
            SupplierId = supplier.Id,
            MappingSource = "ExactCode"
        });
        await db.SaveChangesAsync();
        partner.PartnerCode = "PARTNER-UPDATED";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var entries = await EntryOptions(db, tenant);
        entries.Should().ContainSingle(option => option.Id == supplier.Id &&
            option.SupplierId == supplier.Id && option.BusinessPartnerId == partner.Id &&
            option.Code == "PARTNER-UPDATED");
        entries.Should().NotContain(option => option.Id == partner.Id);
        db.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [Trait("Category", "AccountsPayable")]
    public async Task FirstApCommand_ShouldRejectUnavailableExactSupplierWithoutCreatingAnother(
        bool deleted, bool inactive)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var partner = Partner(tenantId, "BP-SUP-COLLISION", "Controlled Supplier", "USD");
        var supplier = Supplier(tenantId, partner.PartnerCode, "Existing unavailable supplier");
        supplier.IsDeleted = deleted;
        supplier.IsActive = !inactive;
        db.BusinessPartners.Add(partner);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(item => item.HasActiveTransaction).Returns(true);
        unitOfWork.Setup(item => item.AcquireTransactionLockAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);

        Func<Task> resolve = async () => await new ApSupplierIdentityService(db, unitOfWork.Object, currentUser.Object)
            .ResolveByBusinessPartnerAsync(partner.Id);
        await resolve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive, deleted or blacklisted*");
        (await db.Suppliers.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await db.ApSupplierIdentityLinks.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "FinanceSecurity")]
    public void Lookup_ShouldRequireFinanceReadPermission()
    {
        typeof(VendorInvoiceController).GetMethod(nameof(VendorInvoiceController.GetEntrySuppliers))!
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
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
