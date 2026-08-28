using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Procurement;

public sealed class ProcurementReceiptSourceEvidenceServiceTests
{
    private static readonly Guid TenantScope = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task OverviewDoesNotRevealAnotherTenantsReceipt()
    {
        var tenantId = TenantScope;
        var otherTenantId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        await using var db = Database();
        SeedReceipt(db, otherTenantId, receiptId);
        await db.SaveChangesAsync();

        var action = () => Service(db, User(tenantId)).GetOverviewAsync(receiptId);

        await action.Should().ThrowAsync<ProcurementReceiptSourceEvidenceNotFoundException>();
    }

    [Fact]
    public async Task ExternalUserCannotReadInternalReceiptEvidence()
    {
        var tenantId = TenantScope;
        var receiptId = Guid.NewGuid();
        await using var db = Database();
        SeedReceipt(db, tenantId, receiptId);
        await db.SaveChangesAsync();

        var action = () => Service(db, User(tenantId, external: true)).GetOverviewAsync(receiptId);

        await action.Should().ThrowAsync<ProcurementReceiptSourceEvidenceAuthorizationException>()
            .WithMessage("*External users*");
    }

    [Fact]
    public async Task ProcurementRecordsReaderCanViewReceiptEvidenceWithoutInventoryAssignment()
    {
        var receiptId = Guid.NewGuid();
        await using var db = Database();
        SeedReceipt(db, TenantScope, receiptId);
        await db.SaveChangesAsync();
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(item => item.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = false,
                Code = "ACCESS_DENIED"
            });
        access.Setup(item => item.CheckCapabilityAsync(
                It.Is<ProcurementAccessCapabilityRequest>(request =>
                    request.PermissionCode == "procurement.records.read"),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = true,
                Code = "ACCESS_ALLOWED"
            });

        var result = await Service(db, User(TenantScope), access.Object)
            .GetOverviewAsync(receiptId);

        result.ReceiptId.Should().Be(receiptId);
        result.CanUpload.Should().BeFalse();
        access.Verify(item => item.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.records.read"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ProcurementReceiptSourceEvidenceService Service(
        ApplicationDbContext db,
        ICurrentUserProvider user,
        IProcurementAccessControlService? access = null) => new(
        db,
        user,
        access ?? Mock.Of<IProcurementAccessControlService>(),
        Mock.Of<IControlledFileUploadService>(),
        Mock.Of<ICentralDocumentRepositoryFileService>(),
        Mock.Of<IProcurementControlEventService>(),
        NullLogger<ProcurementReceiptSourceEvidenceService>.Instance);

    private static ICurrentUserProvider User(Guid tenantId, bool external = false)
    {
        var user = new Mock<ICurrentUserProvider>();
        user.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        user.SetupGet(item => item.Username).Returns("receipt.user");
        user.SetupGet(item => item.FullName).Returns("Receipt User");
        user.SetupGet(item => item.IsAuthenticated).Returns(true);
        user.SetupGet(item => item.IsExternalUser).Returns(external);
        user.SetupGet(item => item.Roles).Returns(Array.Empty<string>());
        user.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());
        user.SetupGet(item => item.AuthenticationProvider).Returns(external ? "Local" : "ActiveDirectory");
        user.Setup(item => item.HasRole(It.IsAny<string>())).Returns(false);
        return user.Object;
    }

    private static void SeedReceipt(ApplicationDbContext db, Guid tenantId, Guid receiptId)
    {
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = $"SUP-{Guid.NewGuid():N}"[..18],
            PartnerName = "Receipt Test Supplier"
        };
        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = $"PO-{Guid.NewGuid():N}"[..18],
            Status = "Approved",
            BusinessPartnerId = partner.Id,
            BusinessPartner = partner
        };
        db.BusinessPartners.Add(partner);
        db.PurchaseOrders.Add(purchaseOrder);
        db.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt
        {
            Id = receiptId,
            TenantId = tenantId,
            PurchaseOrderId = purchaseOrder.Id,
            PurchaseOrder = purchaseOrder,
            ReceiptNumber = $"RCV-{Guid.NewGuid():N}"[..18]
        });
    }

    private static ApplicationDbContext Database() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"receipt-evidence-{Guid.NewGuid():N}")
            .Options,
        TenantScope);
}
