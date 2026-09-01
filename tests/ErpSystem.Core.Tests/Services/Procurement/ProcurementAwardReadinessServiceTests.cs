using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementAwardReadinessServiceTests
{
    [Fact]
    public async Task RepeatedLegacyEvaluationIsIdempotentAndLatestHashRemainsCurrent()
    {
        await using var fixture = new Fixture();
        var request = fixture.Request("legacy-stable");

        var first = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            request,
            "legacy-stable");
        var replay = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            request,
            "legacy-stable-replay");
        var latest = await fixture.Service.GetLatestAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id);

        replay.Id.Should().Be(first.Id);
        replay.SourceIntegrityHash.Should().Be(first.SourceIntegrityHash);
        latest.Should().NotBeNull();
        latest!.Id.Should().Be(first.Id);
        latest.IsCurrent.Should().BeTrue();
        var count = await fixture.Context.ProcurementAwardReadinessDecisions
            .CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task ExpectedRecommendationAssertionsCannotReplaceServerDerivedRecommendation()
    {
        await using var fixture = new Fixture();
        var request = fixture.Request("assertion-mismatch");
        request.ExpectedRecommendedSubjectIds = [Guid.NewGuid()];

        await fixture.Service.Invoking(service => service.EvaluateAsync(
                ProcurementAwardReadinessSourceType.Tender,
                fixture.Tender.Id,
                request,
                "assertion-mismatch"))
            .Should().ThrowAsync<ProcurementAwardReadinessConflictException>()
            .Where(exception =>
                exception.Code ==
                "AWARD_READINESS_RECOMMENDATION_SUBJECT_MISMATCH");

        fixture.Context.ProcurementAwardReadinessDecisions.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedRecommendedBidderVerificationProducesRetainedBlockedDecision()
    {
        await using var fixture = new Fixture();
        await fixture.AddFailedVerificationAsync();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("failed-verification"),
            "failed-verification");

        result.Status.Should().Be(ProcurementAwardReadinessDecisionStatus.Blocked);
        result.Verifications.Should().ContainSingle(item =>
            item.TenderBidId == fixture.Bid.Id &&
            item.BusinessPartnerId == fixture.Partner.Id &&
            item.BidderStatus == "Failed");
        result.BlockedReasons.Should().Contain(item =>
            item.Contains("AWARD_VERIFICATION_PASSED",
                StringComparison.Ordinal));
        (await fixture.Context.ProcurementAwardReadinessDecisions
                .SingleAsync())
            .IntegrityHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task CrossTenantReadCannotDiscoverExistingDecision()
    {
        await using var fixture = new Fixture();
        await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("tenant-bound"),
            "tenant-bound");
        fixture.SwitchTenant(Guid.NewGuid());

        await fixture.Service.Invoking(service => service.GetLatestAsync(
                ProcurementAwardReadinessSourceType.Tender,
                fixture.Tender.Id))
            .Should().ThrowAsync<ProcurementAwardReadinessNotFoundException>()
            .Where(exception => exception.Code == "TENDER_NOT_FOUND");
    }

    [Fact]
    public async Task MissingDec011ConfigurationIsAdvisoryAtTenderAwardReadiness()
    {
        await using var fixture = new Fixture();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("optional-dec-011"),
            "optional-dec-011");

        var supplier = result.Suppliers.Should().ContainSingle().Subject;
        supplier.Errors.Should().NotContain(item =>
            item.Contains("DEC-011", StringComparison.OrdinalIgnoreCase));
        supplier.Warnings.Should().ContainSingle(item =>
            item.Contains("No effective DEC-011", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingOptionalAwardVerificationChecklistDoesNotBlockTenderReadiness()
    {
        await using var fixture = new Fixture();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("optional-award-verification"),
            "optional-award-verification");

        result.PrerequisiteGroups
            .SelectMany(group => group.Items)
            .Should().Contain(item =>
                item.Code == "AWARD_VERIFICATION_NOT_CONFIGURED" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.NotApplicable);
        result.BlockedReasons.Should().NotContain(item =>
            item.Contains("AWARD_VERIFICATION", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SodPreflightRequiresReadPermissionRatherThanAwardApproval()
    {
        await using var fixture = new Fixture();

        _ = await fixture.Service.GetEvaluatorAwardApproverSodStatusAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            "read-only-preflight");

        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.records.read"),
            "read-only-preflight",
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.tender.approve"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _current;
        private Guid _tenantId;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            _tenantId = TenantId;
            ActorId = Guid.NewGuid();
            SupplierControllerId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "TDC",
                Name = "TDC",
                Status = TenantStatus.Active
            });
            Partner = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PartnerCode = "SUP-READY-001",
                PartnerName = "Readiness Supplier",
                PartnerType = "Supplier",
                IsActive = true,
                IsBlacklisted = false,
                ApprovalStatus =
                    BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus,
                RegistrationStatus =
                    BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus,
                ApprovedById = SupplierControllerId
            };
            Tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderNumber = "TND-LEGACY-READY-001",
                Title = "Legacy award readiness",
                TenderType = "NCT",
                Status = "Closed",
                CreatedById = Guid.NewGuid()
            };
            Bid = new TenderBid
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                BusinessPartnerId = Partner.Id,
                BidNumber = "BID-READY-001",
                Status = "Accepted",
                TotalBidAmount = 1000m
            };
            var evaluator = new TenderEvaluator
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                UserId = Guid.NewGuid(),
                Role = "Evaluator",
                Status = "Completed",
                CompletedDate = DateTime.UtcNow.AddHours(-1)
            };
            Evaluation = new TenderEvaluation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderBidId = Bid.Id,
                TenderEvaluatorId = evaluator.Id,
                TenderEvaluator = evaluator,
                TenderBid = Bid,
                EvaluationDate = DateTime.UtcNow.AddHours(-2),
                SubmittedDate = DateTime.UtcNow.AddHours(-1),
                Status = "Approved",
                TotalScore = 90m,
                IsRecommended = true,
                Recommendation = "Highest current evaluated score."
            };
            Context.AddRange(Partner, Tender, Bid, evaluator, Evaluation);
            Context.SaveChanges();

            _unitOfWork = new UnitOfWork(Context);
            _current = new Mock<ICurrentUserProvider>();
            _current.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            _current.SetupGet(item => item.UserId).Returns(ActorId);
            _current.SetupGet(item => item.IsAuthenticated).Returns(true);
            _current.SetupGet(item => item.IsExternalUser).Returns(false);
            _current.SetupGet(item => item.Username).Returns("approver@tdc.test");
            _current.SetupGet(item => item.FullName).Returns("TDC Award Approver");
            _current.SetupGet(item => item.Roles)
                .Returns(["TDC_HEAD_OF_PROCUREMENT"]);
            _current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns(false);
            Access = new Mock<IProcurementAccessControlService>();
            Access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });
            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto
                {
                    Allowed = true
                });
            var events = new Mock<IProcurementControlEventService>();
            events.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            Service = new ProcurementAwardReadinessService(
                _unitOfWork,
                _current.Object,
                Access.Object,
                sod.Object,
                events.Object);
        }

        public Guid TenantId { get; }
        public Guid ActorId { get; }
        public Guid SupplierControllerId { get; }
        public ApplicationDbContext Context { get; }
        public Tender Tender { get; }
        public TenderBid Bid { get; }
        public TenderEvaluation Evaluation { get; }
        public BusinessPartner Partner { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public ProcurementAwardReadinessService Service { get; }

        public EvaluateProcurementAwardReadinessRequest Request(string key) => new()
        {
            IdempotencyKey = key,
            ExpectedRecommendedSubjectIds = [Bid.Id],
            ExpectedBusinessPartnerIds = [Partner.Id]
        };

        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;

        public async Task AddFailedVerificationAsync()
        {
            var template = new AwardVerificationChecklistTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Name = "Standard award verification",
                IsActive = true
            };
            var item = new AwardVerificationChecklistItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TemplateId = template.Id,
                ItemText = "Tax clearance",
                IsRequired = true,
                IsActive = true
            };
            var verification = new TenderAwardVerification
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                TemplateId = template.Id,
                Status = "Completed",
                StartedDate = DateTime.UtcNow.AddMinutes(-20),
                CompletedDate = DateTime.UtcNow,
                CompletedById = Guid.NewGuid()
            };
            var bidder = new TenderAwardVerificationBidder
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                VerificationId = verification.Id,
                TenderBidId = Bid.Id,
                BusinessPartnerId = Partner.Id,
                Status = "Failed",
                VerifiedDate = DateTime.UtcNow
            };
            var result = new TenderAwardVerificationItemResult
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BidderId = bidder.Id,
                ChecklistItemId = item.Id,
                IsVerified = true,
                Status = "Failed",
                VerifiedDate = DateTime.UtcNow
            };
            Context.AddRange(template, item, verification, bidder, result);
            await Context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
