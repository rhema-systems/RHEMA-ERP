using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class FinanceApprovalInvoiceProducerRouteTests
{
    [Theory]
    [InlineData(FinanceDimensionRouteId.FinanceArCustomerInvoice)]
    [InlineData(FinanceDimensionRouteId.FinanceFixedAssetDisposalSaleInvoice)]
    [InlineData(FinanceDimensionRouteId.InventoryDisposalAuctionInvoice)]
    [InlineData(FinanceDimensionRouteId.SalesOrderCustomerInvoice)]
    public async Task FinalApproval_ShouldRestoreTrustedCustomerInvoiceProducerRoute(
        FinanceDimensionRouteId routeId)
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using var db = CreateContext();
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            InvoiceNumber = "AR-ROUTE-001",
            BusinessPartnerId = Guid.NewGuid(),
            BusinessPartnerRoleId = Guid.NewGuid(),
            BusinessPartnerArProfileVersionId = Guid.NewGuid(),
            BusinessPartnerCode = "CUS-001",
            CustomerName = "Route customer",
            InvoiceDate = new DateTime(2026, 10, 4),
            CurrencyCode = "GHS",
            Status = InvoiceStatus.PendingApproval
        });
        var route = FinanceDimensionRouteCatalog.GetRequired(routeId);
        db.FinanceSourceDimensionAssignments.Add(new FinanceSourceDimensionAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RouteId = route.Id,
            ProducerModule = route.ProducerModule,
            SourceRoute = route.SourceRoute,
            SourceDocumentType = route.DocumentType,
            ContractVersion = route.ContractVersion,
            SourceDocumentId = invoiceId
        });
        await db.SaveChangesAsync();

        var invoiceService = new Mock<IInvoiceService>(MockBehavior.Strict);
        invoiceService
            .Setup(service => service.SendInvoiceAsync(
                invoiceId,
                It.Is<FinancePostingProducerContext>(producer => producer.RouteId == routeId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpSystem.Core.DTOs.Finance.InvoiceDto());
        var controller = CreateController(db, invoiceService.Object);

        await InvokeApprovedOutcomeAsync(controller, tenantId, invoiceId);

        invoiceService.VerifyAll();
        invoiceService.Verify(service => service.SendInvoiceAsync(invoiceId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FinalApproval_WithoutCertifiedRoute_ShouldRetainLegacyOverload()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using var db = CreateContext();
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            InvoiceNumber = "AR-LEGACY-001",
            BusinessPartnerId = Guid.NewGuid(),
            BusinessPartnerRoleId = Guid.NewGuid(),
            BusinessPartnerArProfileVersionId = Guid.NewGuid(),
            BusinessPartnerCode = "CUS-002",
            CustomerName = "Legacy customer",
            InvoiceDate = new DateTime(2026, 10, 4),
            CurrencyCode = "GHS",
            Status = InvoiceStatus.PendingApproval
        });
        await db.SaveChangesAsync();

        var invoiceService = new Mock<IInvoiceService>(MockBehavior.Strict);
        invoiceService.Setup(service => service.SendInvoiceAsync(invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpSystem.Core.DTOs.Finance.InvoiceDto());

        await InvokeApprovedOutcomeAsync(CreateController(db, invoiceService.Object), tenantId, invoiceId);

        invoiceService.VerifyAll();
    }

    [Fact]
    public async Task FinalApproval_WithConflictingCertifiedRoutes_ShouldFailClosed()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using var db = CreateContext();
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            InvoiceNumber = "AR-CONFLICT-001",
            BusinessPartnerId = Guid.NewGuid(),
            BusinessPartnerRoleId = Guid.NewGuid(),
            BusinessPartnerArProfileVersionId = Guid.NewGuid(),
            BusinessPartnerCode = "CUS-003",
            CustomerName = "Conflicting route customer",
            InvoiceDate = new DateTime(2026, 10, 4),
            CurrencyCode = "GHS",
            Status = InvoiceStatus.PendingApproval
        });
        foreach (var routeId in new[]
                 {
                     FinanceDimensionRouteId.FinanceArCustomerInvoice,
                     FinanceDimensionRouteId.SalesOrderCustomerInvoice
                 })
        {
            var route = FinanceDimensionRouteCatalog.GetRequired(routeId);
            db.FinanceSourceDimensionAssignments.Add(new FinanceSourceDimensionAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RouteId = route.Id,
                ProducerModule = route.ProducerModule,
                SourceRoute = route.SourceRoute,
                SourceDocumentType = route.DocumentType,
                ContractVersion = route.ContractVersion,
                SourceDocumentId = invoiceId
            });
        }
        await db.SaveChangesAsync();

        var invoiceService = new Mock<IInvoiceService>(MockBehavior.Strict);
        var controller = CreateController(db, invoiceService.Object);

        var act = () => InvokeApprovedOutcomeAsync(controller, tenantId, invoiceId);

        await act.Should().ThrowAsync<ErpSystem.Core.Exceptions.BusinessRuleException>()
            .WithMessage("Customer invoice has conflicting trusted producer routes.");
        invoiceService.VerifyNoOtherCalls();
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-approval-ar-route-{Guid.NewGuid():N}")
            .Options);

    private static FinanceApprovalsController CreateController(
        ApplicationDbContext db,
        IInvoiceService invoiceService) => new(
            db,
            Mock.Of<ICurrentUserService>(),
            Mock.Of<IAuthorizationService>(),
            Mock.Of<IWorkflowService>(),
            Mock.Of<IWorkflowEntityDisplayService>(),
            Mock.Of<IJournalEntryService>(),
            invoiceService,
            Mock.Of<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService>(),
            null!,
            Mock.Of<ILogger<FinanceApprovalsController>>());

    private static async Task InvokeApprovedOutcomeAsync(
        FinanceApprovalsController controller,
        Guid tenantId,
        Guid invoiceId)
    {
        var method = typeof(FinanceApprovalsController).GetMethod(
            "ApplyApprovedOutcomeAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(controller, new object?[]
        {
            tenantId,
            "Invoice",
            invoiceId,
            Guid.NewGuid(),
            "Approved",
            null,
            CancellationToken.None
        })!;
        await task;
    }
}
