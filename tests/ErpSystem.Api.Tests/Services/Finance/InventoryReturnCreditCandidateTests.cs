using System.Reflection;
using System.Security.Claims;
using ErpSystem.Api.Authorization;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class InventoryReturnCreditCandidateTests
{
    [Fact]
    public async Task Candidate_list_is_tenant_scoped_shipped_uncredited_and_read_only()
    {
        using var db = Context();
        var tenant = Guid.NewGuid();
        var eligible = Seed(db, tenant, "RTV-CURRENT");
        var otherTenant = Seed(db, Guid.NewGuid(), "RTV-FOREIGN");
        var draft = Seed(db, tenant, "RTV-DRAFT"); draft.Status = "Draft";
        var unposted = Seed(db, tenant, "RTV-NO-INVOICE");
        db.VendorInvoices.Local.Single(invoice => invoice.PurchaseOrderId == unposted.PurchaseOrderId).JournalEntryId = null;
        var credited = Seed(db, tenant, "RTV-CREDITED");
        db.SupplierDebitNotes.Add(new SupplierDebitNote { TenantId = tenant, InventoryPurchaseReturnId = credited.Id });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var result = await Service(db, tenant).GetInventoryReturnCreditCandidatesAsync();
        result.Should().ContainSingle().Which.ReturnId.Should().Be(eligible.Id);
        result.Should().NotContain(source => source.ReturnId == otherTenant.Id);
        db.ChangeTracker.HasChanges().Should().BeFalse();
        (await Service(db, tenant).GetInventoryReturnCreditCandidatesAsync("not-a-match")).Should().BeEmpty();
        (await Service(db, tenant).GetInventoryReturnCreditCandidatesAsync("Harbourline")).Should().ContainSingle();
    }

    [Fact]
    public async Task Foreign_return_is_not_disclosed_by_the_original_invoice_endpoint()
    {
        using var db = Context();
        var source = Seed(db, Guid.NewGuid(), "RTV-FOREIGN"); await db.SaveChangesAsync();
        Func<Task> read = () => Service(db, Guid.NewGuid()).GetInventoryReturnCreditSourcesAsync(source.Id);
        await read.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task Candidate_endpoint_uses_real_Finance_permission_without_Inventory_grants(bool grantFinance, bool wrongTenant, bool allowed)
    {
        using var db = Context();
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var role = Guid.NewGuid(); var permission = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenant, Name = "Finance tenant", Code = "AP", Status = TenantStatus.Active });
        db.Users.Add(new ApplicationUser { Id = user, UserName = "ap.officer", FirstName = "AP", LastName = "Officer", TenantId = tenant, IsActive = true });
        db.Roles.Add(new ApplicationRole("AP Officer") { Id = role });
        db.UserRoles.Add(new ApplicationUserRole { UserId = user, RoleId = role });
        db.Permissions.Add(new Permission { Id = permission, Name = FinancePermissions.ViewFinance, DisplayName = "Finance Read", Category = "Finance" });
        if (grantFinance) db.RolePermissions.Add(new RolePermission { RoleId = role, PermissionId = permission, GrantedBy = "Test" });
        await db.SaveChangesAsync();
        var endpointPolicy = typeof(SupplierDebitNotesController).GetMethod("ReturnCandidates")!
            .GetCustomAttributes<AuthorizeAttribute>().Single().Policy!;
        endpointPolicy.Should().Be(FinancePermissions.ViewFinance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, user.ToString()),
            new Claim(Constants.Claims.TenantId, (wrongTenant ? Guid.NewGuid() : tenant).ToString())
        }, "Test"));
        var requirement = new PermissionRequirement(endpointPolicy);
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);
        await new PermissionAuthorizationHandler(db, NullLogger<PermissionAuthorizationHandler>.Instance).HandleAsync(context);
        context.HasSucceeded.Should().Be(allowed);
        db.Permissions.Should().NotContain(value => value.Name.StartsWith("procurement.inventory."));
    }

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"return-credit-candidates-{Guid.NewGuid():N}", options => options.EnableNullChecks(false)).Options);

    private static SupplierDebitNoteService Service(ApplicationDbContext db, Guid tenant)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(user => user.TenantId).Returns(tenant);
        current.SetupGet(user => user.UserId).Returns(Guid.NewGuid().ToString());
        current.SetupGet(user => user.IsAuthenticated).Returns(true);
        return new SupplierDebitNoteService(db, null!, current.Object, null!, null!, null!, null!, null!, null!, NullLogger<SupplierDebitNoteService>.Instance);
    }

    private static PurchaseReturn Seed(ApplicationDbContext db, Guid tenant, string number)
    {
        var supplier = Guid.NewGuid(); var vendor = Guid.NewGuid(); var po = Guid.NewGuid(); var poLine = Guid.NewGuid();
        var item = Guid.NewGuid(); var receipt = Guid.NewGuid();
        var grn = new GoodsReceiptNote { TenantId = tenant, SupplierId = supplier, PurchaseOrderId = po, PurchaseOrderReceiptId = receipt };
        var grnLine = new GoodsReceiptNoteItem { TenantId = tenant, GoodsReceiptNoteId = grn.Id, GoodsReceiptNote = grn, PurchaseOrderItemId = poLine, InventoryItemId = item };
        var source = new PurchaseReturn { TenantId = tenant, ReturnNumber = number, SupplierId = supplier, SupplierName = "Harbourline",
            PurchaseOrderId = po, GoodsReceiptNoteId = grn.Id, Status = "Shipped", ApprovalRequired = false, ShippedDate = DateTime.UtcNow };
        source.Items.Add(new PurchaseReturnItem { TenantId = tenant, InventoryItemId = item, GoodsReceiptNoteItemId = grnLine.Id,
            GoodsReceiptNoteItem = grnLine, ReturnQuantity = 1, StockReversed = true, StockReversedAt = DateTime.UtcNow });
        var invoice = new VendorInvoice { TenantId = tenant, SupplierId = vendor, PurchaseOrderId = po, InvoiceNumber = "VI-TEST", TotalAmount = 700,
            Status = VendorInvoiceStatus.Approved, JournalEntryId = Guid.NewGuid(), CurrencyCode = "GHS" };
        invoice.LineItems.Add(new VendorInvoiceLineItem { TenantId = tenant, InventoryItemId = item, PurchaseOrderItemId = poLine, Quantity = 1, UnitPrice = 700 });
        db.AddRange(source, invoice,
            new JournalEntry { Id = invoice.JournalEntryId.Value, TenantId = tenant, SourceDocumentId = invoice.Id, SourceDocumentType = "VendorInvoice", PostingStatus = "Posted" },
            new ApSupplierIdentityLink { TenantId = tenant, BusinessPartnerId = supplier, SupplierId = vendor },
            new PurchaseOrderReceiptItem { TenantId = tenant, ReceiptId = receipt, PurchaseOrderItemId = poLine, AcceptedQuantity = 1 });
        return source;
    }
}
