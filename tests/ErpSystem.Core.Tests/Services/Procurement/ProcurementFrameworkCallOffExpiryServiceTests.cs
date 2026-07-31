using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementFrameworkCallOffExpiryServiceTests
{
    [Fact]
    public async Task ExpiryProcessingCreatesLedgerForUnusedGoverningAgreement()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var context = new ApplicationDbContext(options);
        using var unitOfWork = new UnitOfWork(context);
        var agreement = new ProcurementFrameworkAgreement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AgreementKey = Guid.NewGuid(),
            AgreementNumber = "FA-UNUSED-EXPIRING",
            Title = "Unused expiring framework",
            Version = 1,
            Status = ProcurementFrameworkAgreementStatus.Published,
            BusinessPartnerId = Guid.NewGuid(),
            SourceType = ProcurementAwardReadinessSourceType.Tender,
            SourceId = Guid.NewGuid(),
            SourceReference = "TDR-UNUSED-EXPIRING",
            AwardReadinessDecisionId = Guid.NewGuid(),
            SourceIntegrityHash = new string('a', 64),
            SupplierEligibilityDecisionHash = new string('b', 64),
            PriceListReference = "PL-UNUSED-EXPIRING",
            PriceListVersion = 1,
            CeilingAmount = 125_000m,
            CurrencyCode = "GHS",
            EffectiveFromUtc = now.AddMonths(-2),
            EffectiveToUtc = now.AddDays(10),
            WorkflowDefinitionId = Guid.NewGuid(),
            CreationCorrelationId = "unused-expiry-create",
            LastOperationCorrelationId = "unused-expiry-publish",
            LastOperation = "Published",
            IntegrityHash = new string('c', 64)
        };
        context.Add(agreement);
        await context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.IsExternalUser).Returns(false);
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        currentUser.SetupGet(item => item.Username).Returns("expiry.processor");
        currentUser.SetupGet(item => item.FullName).Returns("Expiry processor");
        currentUser.Setup(item => item.HasRole("Admin")).Returns(true);
        var controlEvents = new Mock<IProcurementControlEventService>();
        var notifications = new Mock<INotificationTopicPublisher>();
        var service = new ProcurementFrameworkCallOffService(
            unitOfWork,
            currentUser.Object,
            new Mock<IProcurementAccessControlService>().Object,
            new Mock<ISupplierValidationService>().Object,
            new Mock<IWorkflowIntegrationService>().Object,
            new Mock<IWorkflowStatusAdapterRegistry>().Object,
            controlEvents.Object,
            notifications.Object,
            new Mock<IDocumentNumberingService>().Object,
            new Mock<IProcurementPurchaseOrderSourceService>().Object,
            new Mock<IProcurementPurchaseOrderComplianceService>().Object,
            new Mock<IProcurementPurchaseOrderSodService>().Object,
            NullLogger<ProcurementFrameworkCallOffService>.Instance);

        var alerted = await service.ProcessExpiryAlertsAsync();

        alerted.Should().Be(1);
        var balance = await context
            .Set<ProcurementFrameworkAgreementBalance>()
            .SingleAsync();
        balance.AgreementId.Should().Be(agreement.Id);
        balance.CommittedAmount.Should().Be(0m);
        balance.AvailableAmount.Should().Be(agreement.CeilingAmount);
        balance.LastExpiryAlertAtUtc.Should().NotBeNull();
        controlEvents.Verify(
            item => item.RecordAsync(
                It.Is<ProcurementControlEventWriteRequest>(request =>
                    request.Action == "AgreementExpiryAlerted" &&
                    request.SourceId == agreement.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        notifications.Verify(
            item => item.PublishAsync(
                It.Is<NotificationTopicEvent>(request =>
                    request.TopicKey ==
                    "procurement.framework-call-off.expiry-warning" &&
                    request.EntityId == agreement.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
