using ErpSystem.Core.DTOs.Compliance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Audit;
using ErpSystem.Core.Services.Audit;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Security;

public sealed class AuditGovernanceServiceTests
{
    public static IEnumerable<object[]> RequiredActions()
    {
        yield return ["Created", AuditOperationKind.Create];
        yield return ["Updated", AuditOperationKind.Update];
        yield return ["InvoiceMatchExceptionApproved", AuditOperationKind.Approve];
        yield return ["InvoiceMatchExceptionRejected", AuditOperationKind.Reject];
        yield return ["OverrideSourcingMethod", AuditOperationKind.Override];
        yield return ["PostStockAdjustment", AuditOperationKind.Post];
        yield return ["ReverseStockAdjustment", AuditOperationKind.Reverse];
        yield return ["Dispatch", AuditOperationKind.Dispatch];
        yield return ["Receive", AuditOperationKind.Receive];
        yield return ["CalculateEscalationRun", AuditOperationKind.Create];
        yield return ["ReviewEscalationRun", AuditOperationKind.Update];
        yield return ["OpenEscalationDispute", AuditOperationKind.Create];
        yield return ["RecordEscalationContractorResponse", AuditOperationKind.Update];
        yield return ["AttachMeasurementEvidence", AuditOperationKind.Create];
        yield return ["RecordMeasurementSheet", AuditOperationKind.Approve];
        yield return ["ScheduleJointMeasurement", AuditOperationKind.Update];
        yield return ["EndorseJointMeasurement", AuditOperationKind.Approve];
        yield return ["ApplyJointMeasurementBoqRevision", AuditOperationKind.Approve];
    }

    [Theory]
    [MemberData(nameof(RequiredActions))]
    public void Classifier_maps_every_required_operation(string action, AuditOperationKind expected)
        => AuditOperationClassifier.Classify(action).Should().Be(expected);

    [Fact]
    public void Coverage_registry_is_complete_and_reusable()
    {
        var contributor = new ProcurementInventoryAuditEventCoverageContributor();
        var service = new AuditEventCoverageService([contributor]);

        var report = service.GetReport();

        report.IsComplete.Should().BeTrue();
        report.MissingOperations.Should().BeEmpty();
        report.Definitions.Select(value => value.Operation).Should().BeEquivalentTo(
            Enum.GetValues<AuditOperationKind>().Where(value => value != AuditOperationKind.Other));
        report.Definitions.Should().OnlyContain(value => value.Module == "Procurement and Inventory");
    }

    [Fact]
    public async Task Legal_hold_archive_and_restore_are_append_only_and_replay_safe()
    {
        await using var fixture = new Fixture();
        var audit = fixture.AddAuditLog();

        var initial = await fixture.Service.GetAsync(PlatformAuditLogRecordProvider.Key, audit.Id);
        initial.RetentionDays.Should().Be(AuditGovernanceService.MinimumRetentionDays);
        initial.RetainUntilUtc.Should().Be(audit.Timestamp.AddDays(2555));
        initial.IsImmutable.Should().BeTrue();

        var held = await fixture.Service.PlaceLegalHoldAsync(
            PlatformAuditLogRecordProvider.Key,
            audit.Id,
            Command("hold-1", "Litigation hold requested"));
        held.IsLegalHold.Should().BeTrue();

        var archived = await fixture.Service.ArchiveAsync(
            PlatformAuditLogRecordProvider.Key,
            audit.Id,
            Command("archive-1", "Move to statutory archive"));
        archived.IsArchived.Should().BeTrue();
        archived.ArchiveReference.Should().StartWith("audit-archive://");

        var restored = await fixture.Service.RestoreAsync(
            PlatformAuditLogRecordProvider.Key,
            audit.Id,
            Command("restore-1", "Restore for audit inspection"));
        restored.IsArchived.Should().BeFalse();
        restored.IsLegalHold.Should().BeTrue();

        var released = await fixture.Service.ReleaseLegalHoldAsync(
            PlatformAuditLogRecordProvider.Key,
            audit.Id,
            Command("release-1", "Legal owner released hold"));
        released.IsLegalHold.Should().BeFalse();
        released.Actions.Should().HaveCount(4).And.OnlyContain(value => value.IntegrityValid);
        released.Actions.Select(value => value.SequenceNumber).Should().ContainInOrder(1, 2, 3, 4);

        var replay = await fixture.Service.RestoreAsync(
            PlatformAuditLogRecordProvider.Key,
            audit.Id,
            Command("restore-1", "Restore for audit inspection"));
        replay.Actions.Should().HaveCount(4);
        (await fixture.Context.AuditRecordLifecycleEvents.CountAsync()).Should().Be(4);
        (await fixture.Context.AuditLogs.SingleAsync()).Action.Should().Be("Created");
    }

    [Fact]
    public async Task Provider_fails_closed_across_tenants()
    {
        await using var fixture = new Fixture();
        var audit = fixture.AddAuditLog(fixture.ForeignTenantId);

        var action = () => fixture.Service.GetAsync(PlatformAuditLogRecordProvider.Key, audit.Id);

        await action.Should().ThrowAsync<AuditGovernanceNotFoundException>();
    }

    private static AuditLifecycleCommandDto Command(string key, string reason) => new()
    {
        RequestKey = key,
        Reason = reason,
        CorrelationId = $"correlation-{key}"
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.AddRange(
                new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active },
                new Tenant { Id = ForeignTenantId, Code = "OTHER", Name = "Other", Status = TenantStatus.Active });
            Context.Users.Add(new ApplicationUser
            {
                Id = UserId,
                TenantId = TenantId,
                UserName = "auditor@tdc.test",
                NormalizedUserName = "AUDITOR@TDC.TEST",
                FirstName = "Internal",
                LastName = "Auditor",
                IsActive = true
            });
            Context.SaveChanges();

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(value => value.TenantId).Returns(TenantId);
            current.SetupGet(value => value.UserId).Returns(UserId);
            current.SetupGet(value => value.IsAuthenticated).Returns(true);
            current.SetupGet(value => value.Username).Returns("auditor@tdc.test");
            current.SetupGet(value => value.FullName).Returns("Internal Auditor");
            current.SetupGet(value => value.Roles).Returns(["TDC_INTERNAL_AUDIT"]);
            _unitOfWork = new UnitOfWork(Context);
            IAuditRecordProvider[] providers =
            [
                new PlatformAuditLogRecordProvider(_unitOfWork),
                new ProcurementControlEventAuditRecordProvider(_unitOfWork)
            ];
            Service = new AuditGovernanceService(_unitOfWork, current.Object, providers);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public AuditGovernanceService Service { get; }

        public AuditLog AddAuditLog(Guid? tenantId = null)
        {
            var item = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId ?? TenantId,
                UserId = UserId,
                Username = "auditor@tdc.test",
                Action = "Created",
                Resource = "InventoryItem",
                ResourceId = Guid.NewGuid().ToString("N"),
                IpAddress = "127.0.0.1",
                Timestamp = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
            };
            Context.AuditLogs.Add(item);
            Context.SaveChanges();
            return item;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
