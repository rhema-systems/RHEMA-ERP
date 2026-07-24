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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementAppSubmissionServiceTests
{
    [Fact]
    public async Task ExportRequiresPublishedPlanAndCreatesAuditedRegisterAttempt()
    {
        await using var fixture = new Fixture();

        var created = await fixture.Service.RecordExportAsync(fixture.ExportRequest(fixture.PublishedPlanId), "trace-export");

        created.Status.Should().Be(ProcurementAppSubmissionStatus.Exported);
        created.AttemptNumber.Should().Be(1);
        created.SubmissionNumber.Should().MatchRegex("^APP-[0-9]{4}-00001$");
        created.PlanNumber.Should().Be("APP-PLAN-001");
        created.Timeline.Should().ContainSingle(item => item.Action == "ExportRecorded" &&
            item.Evidence.Any(evidence => evidence.Reference == $"sha256:{Fixture.Checksum}"));
        (await fixture.Context.ProcurementControlEvents.SingleAsync()).DecisionKeysJson.Should().Contain("DEC-009");

        var draftAction = () => fixture.Service.RecordExportAsync(fixture.ExportRequest(fixture.DraftPlanId), "trace-draft");
        await draftAction.Should().ThrowAsync<ProcurementAppSubmissionConflictException>();
    }

    [Fact]
    public async Task RejectionResubmissionAndAcknowledgementPreserveOneSeriesTimeline()
    {
        await using var fixture = new Fixture();
        var first = await fixture.Service.RecordExportAsync(fixture.ExportRequest(fixture.PublishedPlanId), "trace-first-export");
        var submitted = await fixture.Service.SubmitAsync(first.Id, new SubmitProcurementAppRequest
        {
            ExternalSubmissionReference = "GH-2026-001",
            SubmittedAtUtc = DateTime.UtcNow,
            RowVersion = first.RowVersion
        }, "trace-first-submit");
        var rejected = await fixture.Service.RejectAsync(first.Id, new RejectProcurementAppRequest
        {
            RejectionReference = "PPA-REJ-001",
            RejectionReason = "Template validation failed.",
            RejectedAtUtc = DateTime.UtcNow,
            RowVersion = submitted.RowVersion
        }, "trace-reject");
        var second = await fixture.Service.ResubmitAsync(first.Id, new ResubmitProcurementAppRequest
        {
            ExportFileName = "app-plan-r2.xlsx",
            ExportFormat = "XLSX",
            ExportTemplateVersion = "PPA-APP-v2",
            ExportChecksumSha256 = new string('B', 64),
            RowVersion = rejected.RowVersion
        }, "trace-resubmit");
        var resubmitted = await fixture.Service.SubmitAsync(second.Id, new SubmitProcurementAppRequest
        {
            ExternalSubmissionReference = "GH-2026-001-R1",
            SubmittedAtUtc = DateTime.UtcNow,
            RowVersion = second.RowVersion
        }, "trace-second-submit");
        var acknowledged = await fixture.Service.AcknowledgeAsync(second.Id, new AcknowledgeProcurementAppRequest
        {
            AcknowledgementReference = "PPA-ACK-001",
            AcknowledgedAtUtc = DateTime.UtcNow,
            RowVersion = resubmitted.RowVersion
        }, "trace-acknowledge");

        acknowledged.Status.Should().Be(ProcurementAppSubmissionStatus.Acknowledged);
        acknowledged.AttemptNumber.Should().Be(2);
        acknowledged.SupersedesSubmissionId.Should().Be(first.Id);
        acknowledged.TimelineCorrelationId.Should().Be(first.TimelineCorrelationId);
        acknowledged.Timeline.Select(item => item.Action).Should().Equal(
            "ExportRecorded", "Submitted", "Rejected", "ResubmissionRecorded", "Submitted", "Acknowledged");
        acknowledged.Timeline.Should().OnlyContain(item => item.IntegrityHash.Length == 64 && item.Evidence.Count > 0);
        (await fixture.Context.ProcurementAppSubmissions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task DuplicateRegisterInvalidTransitionAndStaleVersionAreBlocked()
    {
        await using var fixture = new Fixture();
        var created = await fixture.Service.RecordExportAsync(fixture.ExportRequest(fixture.PublishedPlanId), "trace-first");

        await fixture.Service.Invoking(service => service.RecordExportAsync(fixture.ExportRequest(fixture.PublishedPlanId), "trace-duplicate"))
            .Should().ThrowAsync<ProcurementAppSubmissionConflictException>();
        await fixture.Service.Invoking(service => service.AcknowledgeAsync(created.Id, new AcknowledgeProcurementAppRequest
        {
            AcknowledgementReference = "ACK-EARLY",
            AcknowledgedAtUtc = DateTime.UtcNow,
            RowVersion = created.RowVersion
        }, "trace-early-ack"))
            .Should().ThrowAsync<ProcurementAppSubmissionConflictException>();
        await fixture.Service.Invoking(service => service.SubmitAsync(created.Id, new SubmitProcurementAppRequest
        {
            ExternalSubmissionReference = "GH-STALE",
            SubmittedAtUtc = DateTime.UtcNow,
            RowVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
        }, "trace-stale"))
            .Should().ThrowAsync<ProcurementAppSubmissionConflictException>();
    }

    [Fact]
    public async Task SearchAndDetailNeverCrossTenantBoundary()
    {
        await using var fixture = new Fixture();
        var created = await fixture.Service.RecordExportAsync(fixture.ExportRequest(fixture.PublishedPlanId), "trace-tenant");
        fixture.SwitchTenant(fixture.ForeignTenantId);

        (await fixture.Service.SearchAsync(new ProcurementAppSubmissionSearchRequest())).TotalCount.Should().Be(0);
        await fixture.Service.Invoking(service => service.GetAsync(created.Id))
            .Should().ThrowAsync<ProcurementAppSubmissionNotFoundException>();
    }

    [Fact]
    public async Task NonAdministratorMutationRequiresPlanManagerCapability()
    {
        await using var fixture = new Fixture();
        fixture.SwitchRoles("TDC_PROCUREMENT_OFFICER");
        fixture.Access.Setup(service => service.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = false,
                Code = "ACCESS_PERMISSION_DENIED",
                Message = "No active plan responsibility assignment."
            });

        var action = () => fixture.Service.RecordExportAsync(fixture.ExportRequest(fixture.PublishedPlanId), "trace-denied");

        await action.Should().ThrowAsync<ProcurementAppSubmissionAuthorizationException>();
        (await fixture.Context.ProcurementAppSubmissions.CountAsync()).Should().Be(0);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _tenantId;
        private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase) { "TenantAdmin" };
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            PublishedPlanId = Guid.NewGuid();
            DraftPlanId = Guid.NewGuid();
            _tenantId = TenantId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.AddRange(
                new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active },
                new Tenant { Id = ForeignTenantId, Code = "OTHER", Name = "Other", Status = TenantStatus.Active });
            Context.Users.Add(new ApplicationUser
            {
                Id = UserId,
                UserName = "planner@tdc.test",
                Email = "planner@tdc.test",
                FirstName = "APP",
                LastName = "Planner",
                IsActive = true
            });
            Context.ProcurementPlans.AddRange(
                Plan(PublishedPlanId, "APP-PLAN-001", "Active", DateTime.UtcNow.AddDays(-2), TenantId),
                Plan(DraftPlanId, "APP-PLAN-DRAFT", "Draft", null, TenantId));
            Context.ProcurementPlanItems.Add(new ProcurementPlanItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProcurementPlanId = PublishedPlanId,
                ItemDescription = "Office equipment",
                EstimatedQuantity = 1,
                EstimatedUnitPrice = 1000,
                EstimatedTotalCost = 1000
            });
            Context.SaveChanges();

            _currentUser.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            _currentUser.SetupGet(item => item.UserId).Returns(UserId);
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Username).Returns("planner@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("APP Planner");
            _currentUser.SetupGet(item => item.Roles).Returns(() => _roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));
            _unitOfWork = new UnitOfWork(Context);
            var events = new ProcurementControlEventService(_unitOfWork, _currentUser.Object,
                NullLogger<ProcurementControlEventService>.Instance);
            Access = new Mock<IProcurementAccessControlService>();
            Access.Setup(service => service.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true, Code = "ACCESS_ALLOWED", Message = "Allowed" });
            Service = new ProcurementAppSubmissionService(_unitOfWork, _currentUser.Object, Access.Object, events,
                NullLogger<ProcurementAppSubmissionService>.Instance);
        }

        public const string Checksum = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public Guid UserId { get; }
        public Guid PublishedPlanId { get; }
        public Guid DraftPlanId { get; }
        public ApplicationDbContext Context { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public ProcurementAppSubmissionService Service { get; }

        public RecordProcurementAppExportRequest ExportRequest(Guid planId) => new()
        {
            ProcurementPlanId = planId,
            ExportFileName = "app-plan.xlsx",
            ExportFormat = "XLSX",
            ExportTemplateVersion = "PPA-APP-v1",
            ExportChecksumSha256 = Checksum,
            Notes = "Published-plan APP export package"
        };

        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;

        public void SwitchRoles(params string[] roles)
        {
            _roles.Clear();
            foreach (var role in roles) _roles.Add(role);
        }

        private static ProcurementPlan Plan(Guid id, string number, string status, DateTime? published, Guid tenantId) => new()
        {
            Id = id,
            TenantId = tenantId,
            PlanNumber = number,
            Title = $"Plan {number}",
            DepartmentId = Guid.NewGuid(),
            FiscalYear = 2026,
            PlanStartDate = new DateTime(2026, 1, 1),
            PlanEndDate = new DateTime(2026, 12, 31),
            Status = status,
            PublishedDate = published,
            RevisionNumber = 1
        };

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
