using System.Text.Json;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderSourceOptionsTests
{
    [Fact]
    public async Task RequisitionScopeExcludesUnrelatedSourceBeforeGraphResolution()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var targetRequisitionId = Guid.NewGuid();
        var sourceRequisition = new PurchaseRequisition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequisitionNumber = "PR-UNRELATED-001",
            RequestedById = Guid.NewGuid(),
            Status = "Approved"
        };
        var release = new ProcurementRequisitionSourcingRelease
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseRequisitionId = sourceRequisition.Id,
            AttemptNumber = 1,
            ReleaseReference = "REL-UNRELATED-001"
        };
        var sourcingCase = new ProcurementSourcingCase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseRequisitionId = sourceRequisition.Id,
            SourcingReleaseId = release.Id,
            CaseSequence = 1,
            CaseNumber = "CASE-UNRELATED-001",
            Status = ProcurementSourcingCaseStatus.Ready
        };
        var tender = new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderNumber = "TDR-UNRELATED-001",
            Title = "Unrelated source",
            Status = "Awarded",
            SourcePurchaseRequisitionId = sourceRequisition.Id,
            SourcingReleaseId = release.Id,
            SourcingCaseId = sourcingCase.Id,
            Currency = "GHS"
        };
        var bid = new TenderBid
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            BusinessPartnerId = supplierId,
            BidNumber = "BID-UNRELATED-001",
            Status = "Accepted",
            TotalBidAmount = 100m,
            Currency = "GHS"
        };
        var negotiation = new TenderNegotiation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            TenderBidId = bid.Id,
            BusinessPartnerId = supplierId,
            Status = "Completed",
            OriginalAmount = 100m,
            NegotiatedAmount = 90m,
            Currency = "GHS"
        };
        var tenderItem = new TenderItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            LineNumber = 1,
            Description = "Unrelated item",
            Quantity = 1m,
            UnitOfMeasure = "EA"
        };
        var bidItem = new TenderBidItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderBidId = bid.Id,
            TenderItemId = tenderItem.Id,
            OfferedQuantity = 1m,
            UnitPrice = 100m,
            TotalPrice = 100m
        };
        var control = new ProcurementExceptionalSourcingControl
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            Tender = tender,
            SourcingCaseId = sourcingCase.Id,
            Status = ProcurementExceptionalSourcingControlStatus.Awarded,
            AwardBidId = bid.Id,
            NegotiationId = negotiation.Id,
            NegotiatedAmount = 90m,
            AwardReference = "EXC-UNRELATED-001"
        };
        var readiness = new ProcurementAwardReadinessDecision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceType = ProcurementAwardReadinessSourceType.ExceptionalSourcing,
            SourceId = tender.Id,
            SourceReference = tender.TenderNumber,
            DecisionSequence = 1,
            Status = ProcurementAwardReadinessDecisionStatus.Ready,
            RecommendedBusinessPartnerIdsJson =
                JsonSerializer.Serialize(new[] { supplierId }),
            IntegrityHash = new string('a', 64)
        };
        var duplicateNegotiationItems = new[]
        {
            new TenderNegotiationItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                NegotiationId = negotiation.Id,
                TenderBidItemId = bidItem.Id,
                Quantity = 1m,
                OriginalUnitPrice = 100m,
                OriginalTotalPrice = 100m,
                NegotiatedUnitPrice = 90m,
                NegotiatedTotalPrice = 90m
            },
            new TenderNegotiationItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                NegotiationId = negotiation.Id,
                TenderBidItemId = bidItem.Id,
                Quantity = 1m,
                OriginalUnitPrice = 100m,
                OriginalTotalPrice = 100m,
                NegotiatedUnitPrice = 90m,
                NegotiatedTotalPrice = 90m
            }
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.AddRange(
            sourceRequisition,
            release,
            sourcingCase,
            tender,
            bid,
            negotiation,
            tenderItem,
            bidItem,
            control,
            readiness);
        context.AddRange(duplicateNegotiationItems);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.IsExternalUser).Returns(false);
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        var service = new ProcurementPurchaseOrderSourceService(
            unitOfWork,
            currentUser.Object,
            new Mock<IProcurementAccessControlService>().Object,
            new Mock<IProcurementControlEventService>().Object,
            new Mock<IProcurementRequisitionBudgetControlService>().Object,
            new Mock<IProcurementBudgetReservationStore>().Object,
            new Mock<INotificationTopicPublisher>().Object,
            NullLogger<ProcurementPurchaseOrderSourceService>.Instance);

        var result = await service.GetOptionsAsync(
            targetRequisitionId,
            "scoped-source-options");

        result.Ready.Should().BeFalse();
        result.CandidateCount.Should().Be(0);
        result.Sources.Should().BeEmpty();
    }
}
