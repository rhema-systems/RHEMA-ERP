using ErpSystem.Core.DTOs.Procurement;
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

public sealed class ProcurementContractOperationsServiceTests
{
    [Fact]
    public async Task SearchIsTenantSafeAndReturnsTheCompleteDecisionRegister()
    {
        await using var fixture = new Fixture();
        await fixture.SeedContractsAsync();

        var result = await fixture.Service.SearchAsync(
            new ProcurementContractOperationsSearchRequest(),
            "tdc-0408-search");

        result.TotalContracts.Should().Be(1);
        result.Items.Should().ContainSingle()
            .Which.ContractId.Should().Be(fixture.ContractId);
        result.DecisionKeys.Should().Equal(
            Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}"));
        fixture.AccessRequests.Should().ContainSingle()
            .Which.PermissionCode.Should().Be("procurement.reports.read");
    }

    [Fact]
    public async Task PublishingPromptsRecordsWarningsAndNeverPostsAnAmount()
    {
        await using var fixture = new Fixture();
        await fixture.SeedContractsAsync();

        var result = await fixture.Service.ProcessAlertsAsync(
            new ProcessProcurementContractOperationsAlertsRequest
            {
                ContractId = fixture.ContractId
            },
            "tdc-0408-alert");

        result.EvaluatedPromptCount.Should().Be(1);
        result.PublishedAlertCount.Should().Be(1);
        fixture.ControlEvents.Should().ContainSingle();
        var controlEvent = fixture.ControlEvents.Single();
        controlEvent.Result.Should().Be(ProcurementControlEventResult.Warning);
        controlEvent.DecisionKeys.Should().HaveCount(14);
        controlEvent.ResultValues.Should().NotBeNull();
        fixture.Notifications.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishingTheSameDailyPromptIsIdempotent()
    {
        await using var fixture = new Fixture();
        await fixture.SeedContractsAsync();
        var promptKey = $"EXPIRY-{fixture.ContractId:N}";
        fixture.Context.Add(new ProcurementControlEvent
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            EventKey = ProcurementControlEventKey.Create(
                "contract-operations-prompt",
                fixture.TenantId,
                fixture.ContractId,
                promptKey,
                DateTime.UtcNow.ToString("yyyyMMdd")),
            EventType = "ProcurementContractOperations",
            Action = "ContractOperationsPromptPublished",
            Result = ProcurementControlEventResult.Warning,
            SourceType = "ProcurementContract",
            SourceId = fixture.ContractId,
            SourceReference = "CON-TDC0408-001",
            ActorUserId = fixture.UserId,
            ActorName = "TDC 0408 Actor",
            CorrelationId = "existing",
            OccurredAtUtc = DateTime.UtcNow,
            IntegrityHash = new string('a', 64)
        });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ProcessAlertsAsync(
            new ProcessProcurementContractOperationsAlertsRequest
            {
                ContractId = fixture.ContractId
            },
            "tdc-0408-repeat");

        result.EvaluatedPromptCount.Should().Be(1);
        result.PublishedAlertCount.Should().Be(0);
        result.AlreadyPublishedCount.Should().Be(1);
        fixture.ControlEvents.Should().BeEmpty();
        fixture.Notifications.Should().BeEmpty();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            ContractId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            Context = new ApplicationDbContext(options);
            _unitOfWork = new UnitOfWork(Context);

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(false);
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(UserId);
            current.SetupGet(item => item.Username)
                .Returns("tdc0408@tests.local");
            current.SetupGet(item => item.FullName)
                .Returns("TDC 0408 Actor");
            current.SetupGet(item => item.Roles)
                .Returns(["Procurement Officer"]);
            current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns(false);

            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementAccessCapabilityRequest, string,
                    CancellationToken>((request, _, _) =>
                    AccessRequests.Add(request))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });

            var events = new Mock<IProcurementControlEventService>();
            events.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementControlEventWriteRequest,
                    CancellationToken>((request, _) =>
                    ControlEvents.Add(request))
                .ReturnsAsync(new ProcurementControlEventDto());

            var notifications = new Mock<INotificationTopicPublisher>();
            notifications.Setup(item => item.PublishAsync(
                    It.IsAny<NotificationTopicEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<NotificationTopicEvent, CancellationToken>(
                    (request, _) => Notifications.Add(request))
                .Returns(Task.CompletedTask);

            Service = new ProcurementContractOperationsService(
                _unitOfWork,
                current.Object,
                access.Object,
                events.Object,
                notifications.Object,
                NullLogger<ProcurementContractOperationsService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public Guid ContractId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementContractOperationsService Service { get; }
        public List<ProcurementAccessCapabilityRequest> AccessRequests { get; } =
            [];
        public List<ProcurementControlEventWriteRequest> ControlEvents { get; } =
            [];
        public List<NotificationTopicEvent> Notifications { get; } = [];

        public async Task SeedContractsAsync()
        {
            var supplierId = Guid.NewGuid();
            var otherTenantId = Guid.NewGuid();
            var otherSupplierId = Guid.NewGuid();
            Context.AddRange(
                new BusinessPartner
                {
                    Id = supplierId,
                    TenantId = TenantId,
                    PartnerCode = "SUP-TDC0408",
                    PartnerName = "TDC Operations Supplier",
                    PartnerType = "Supplier"
                },
                new Contract
                {
                    Id = ContractId,
                    TenantId = TenantId,
                    ContractNumber = "CON-TDC0408-001",
                    ContractTitle = "Tenant-safe operations contract",
                    ContractType = "Supply",
                    Status = "Active",
                    TenderAwardId = Guid.NewGuid(),
                    TenderId = Guid.NewGuid(),
                    BusinessPartnerId = supplierId,
                    ContractValue = 10000m,
                    Currency = "GHS",
                    EndDate = DateTime.UtcNow.Date.AddDays(10),
                    PenaltyClause = "Controlled decision required"
                },
                new BusinessPartner
                {
                    Id = otherSupplierId,
                    TenantId = otherTenantId,
                    PartnerCode = "SUP-OTHER",
                    PartnerName = "Other Tenant Supplier",
                    PartnerType = "Supplier"
                },
                new Contract
                {
                    Id = Guid.NewGuid(),
                    TenantId = otherTenantId,
                    ContractNumber = "CON-OTHER-001",
                    ContractTitle = "Other tenant contract",
                    ContractType = "Supply",
                    Status = "Active",
                    TenderAwardId = Guid.NewGuid(),
                    TenderId = Guid.NewGuid(),
                    BusinessPartnerId = otherSupplierId,
                    ContractValue = 99999m,
                    Currency = "USD",
                    EndDate = DateTime.UtcNow.Date.AddDays(10)
                });
            await Context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
