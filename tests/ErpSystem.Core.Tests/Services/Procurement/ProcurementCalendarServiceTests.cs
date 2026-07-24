using ErpSystem.Core.DTOs.Notifications;
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

public sealed class ProcurementCalendarServiceTests
{
    [Fact]
    public async Task DraftProfileNeverGeneratesOccurrences()
    {
        await using var fixture = new Fixture();
        await fixture.Service.CreateProfileAsync(fixture.ValidRequest(), "calendar-create");

        var run = await fixture.Service.RunAsync(new ProcurementCalendarRunRequest
        {
            EvaluationAtUtc = DateTime.UtcNow,
            HorizonDays = 30,
            Reason = "Verify that unapproved dates cannot generate tasks."
        }, "calendar-draft-run");

        run.ProfilesEvaluated.Should().Be(0);
        run.CreatedCount.Should().Be(0);
        (await fixture.Context.ProcurementCalendarOccurrences.CountAsync()).Should().Be(0);
        fixture.Notifications.Verify(item => item.CreateNotificationAsync(
            It.IsAny<CreateNotificationDto>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PublicationRequiresIndependentActorApprovalAndAllSevenObligations()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.Service.CreateProfileAsync(fixture.ValidRequest(), "calendar-create");

        await fixture.Service.Invoking(service => service.PublishProfileAsync(draft.Id,
                fixture.Lifecycle(draft.RowVersion, "TDC-CALENDAR-APPROVAL-001"), "calendar-self-publish"))
            .Should().ThrowAsync<ProcurementCalendarAuthorizationException>();

        fixture.SwitchActor(Guid.NewGuid(), "Calendar Approver");
        await fixture.Service.Invoking(service => service.PublishProfileAsync(draft.Id,
                fixture.Lifecycle(draft.RowVersion, null), "calendar-no-approval"))
            .Should().ThrowAsync<ProcurementCalendarValidationException>()
            .Where(exception => exception.Code == "APPROVAL_REFERENCE_REQUIRED");

        var published = await fixture.Service.PublishProfileAsync(draft.Id,
            fixture.Lifecycle(draft.RowVersion, "TDC-CALENDAR-APPROVAL-001"), "calendar-publish");

        published.Status.Should().Be(ProcurementCalendarProfileStatus.Published);
        published.Rules.Should().HaveCount(7).And.OnlyContain(item => item.IsEnabled);
        published.ApprovalReference.Should().Be("TDC-CALENDAR-APPROVAL-001");
        (await fixture.Context.ProcurementCalendarOccurrences.CountAsync()).Should().Be(7);
        fixture.Notifications.Verify(item => item.CreateNotificationAsync(
            It.IsAny<CreateNotificationDto>(), Guid.Empty, fixture.TenantId), Times.Exactly(7));
    }

    [Fact]
    public async Task PublicationResolvesSameDayAssignmentAtTheConfiguredDueInstant()
    {
        await using var fixture = new Fixture();
        var assignmentStart = DateTime.UtcNow.Date.AddHours(1);
        foreach (var assignment in await fixture.Context.ProcurementResponsibilityAssignments.ToListAsync())
            assignment.EffectiveFrom = assignmentStart;
        await fixture.Context.SaveChangesAsync();
        var request = fixture.ValidRequest();
        foreach (var rule in request.Rules)
        {
            rule.DueMonth = assignmentStart.Month;
            rule.DueDay = assignmentStart.Day;
            rule.DueLocalTime = TimeSpan.FromHours(9);
        }
        var draft = await fixture.Service.CreateProfileAsync(request, "calendar-same-day-create");
        fixture.SwitchActor(Guid.NewGuid(), "Calendar Approver");

        var published = await fixture.Service.PublishProfileAsync(draft.Id,
            fixture.Lifecycle(draft.RowVersion, "TDC-CALENDAR-APPROVAL-SAME-DAY"), "calendar-same-day-publish");

        published.Status.Should().Be(ProcurementCalendarProfileStatus.Published);
        (await fixture.Context.ProcurementCalendarOccurrences.CountAsync()).Should().Be(7);
    }

    [Fact]
    public async Task RepeatedRunCorrelationIsIdempotent()
    {
        await using var fixture = new Fixture();
        await fixture.CreatePublishedAsync();
        var before = await fixture.Context.ProcurementCalendarOccurrences.CountAsync();
        var request = new ProcurementCalendarRunRequest
        {
            EvaluationAtUtc = DateTime.UtcNow,
            HorizonDays = 30,
            Reason = "Idempotent manual replay."
        };

        var first = await fixture.Service.RunAsync(request, "calendar-idempotency");
        var stored = await fixture.Context.ProcurementCalendarRuns.SingleAsync(item => item.Id == first.Id);
        stored.EvaluationAtUtc = DateTime.SpecifyKind(stored.EvaluationAtUtc, DateTimeKind.Unspecified);
        stored.WindowStartUtc = DateTime.SpecifyKind(stored.WindowStartUtc, DateTimeKind.Unspecified);
        stored.WindowEndUtc = DateTime.SpecifyKind(stored.WindowEndUtc, DateTimeKind.Unspecified);
        stored.StartedAtUtc = DateTime.SpecifyKind(stored.StartedAtUtc, DateTimeKind.Unspecified);
        stored.CompletedAtUtc = DateTime.SpecifyKind(stored.CompletedAtUtc!.Value, DateTimeKind.Unspecified);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var second = await fixture.Service.RunAsync(request, "calendar-idempotency");

        second.Id.Should().Be(first.Id);
        second.AttemptCount.Should().Be(1);
        (await fixture.Context.ProcurementCalendarOccurrences.CountAsync()).Should().Be(before);
        (await fixture.Context.ProcurementCalendarRuns.CountAsync(item => item.Trigger == ProcurementCalendarRunTrigger.Manual))
            .Should().Be(1);
    }

    [Fact]
    public async Task ScheduledRetryRetainsOneRunAndOneReferentialSystemAuditEvent()
    {
        await using var fixture = new Fixture();
        var evaluationAtUtc = new DateTime(2026, 7, 22, 11, 15, 0, DateTimeKind.Utc);

        var first = await fixture.Service.ProcessTenantAsync(fixture.TenantId, evaluationAtUtc, 30,
            ProcurementCalendarRunTrigger.Scheduled, null, "Procurement calendar scheduler",
            "Scheduled calendar reconciliation.", "scheduler-acceptance");
        var second = await fixture.Service.ProcessTenantAsync(fixture.TenantId, evaluationAtUtc, 30,
            ProcurementCalendarRunTrigger.Scheduled, null, "Procurement calendar scheduler",
            "Scheduled calendar reconciliation.", "scheduler-acceptance");

        second.Id.Should().Be(first.Id);
        (await fixture.Context.ProcurementCalendarRuns.CountAsync()).Should().Be(1);
        var controlEvent = await fixture.Context.ProcurementControlEvents.SingleAsync(item =>
            item.EventType == "ProcurementCalendarLifecycle" && item.Action == "GenerationRun");
        controlEvent.ActorUserId.Should().NotBeEmpty();
        controlEvent.ActorRolesJson.Should().Contain("System");
    }

    [Fact]
    public async Task DeletedDraftCloneDoesNotCauseVersionReuse()
    {
        await using var fixture = new Fixture();
        var published = await fixture.CreatePublishedAsync();
        var firstClone = await fixture.Service.CloneProfileAsync(published.Id, fixture.Clone(10), "calendar-clone-v2");
        await fixture.Service.DeleteDraftAsync(firstClone.Id, fixture.Lifecycle(firstClone.RowVersion), "calendar-delete-v2");

        var secondClone = await fixture.Service.CloneProfileAsync(published.Id, fixture.Clone(20), "calendar-clone-v3");

        secondClone.Version.Should().Be(3);
        (await fixture.Context.ProcurementCalendarProfiles.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == firstClone.Id)).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task FutureReplacementKeepsCurrentProfileEffectiveUntilReplacementStarts()
    {
        await using var fixture = new Fixture();
        var current = await fixture.CreatePublishedAsync();
        var replacement = await fixture.Service.CloneProfileAsync(current.Id, fixture.Clone(10), "calendar-clone-future");
        fixture.SwitchActor(Guid.NewGuid(), "Replacement Approver");
        var publishedReplacement = await fixture.Service.PublishProfileAsync(replacement.Id,
            fixture.Lifecycle(replacement.RowVersion, "TDC-CALENDAR-APPROVAL-002"), "calendar-publish-future");
        var profiles = await fixture.Service.GetProfilesAsync();

        profiles.Single(item => item.Id == current.Id).IsEffective.Should().BeTrue();
        profiles.Single(item => item.Id == publishedReplacement.Id).IsEffective.Should().BeFalse();
        (await fixture.Context.ProcurementCalendarProfiles.SingleAsync(item => item.Id == current.Id)).Status
            .Should().Be(ProcurementCalendarProfileStatus.Published);
        (await fixture.Context.ProcurementCalendarOccurrences.CountAsync()).Should().Be(7);
        (await fixture.Context.ProcurementCalendarOccurrences.ToListAsync()).Should()
            .OnlyContain(item => item.ProfileVersion == current.Version);
    }

    [Fact]
    public async Task OccurrenceActionsEnforceTenantOwnershipConcurrencyAndTerminalState()
    {
        await using var fixture = new Fixture();
        await fixture.CreatePublishedAsync();
        var occurrence = (await fixture.Service.SearchOccurrencesAsync(new ProcurementCalendarOccurrenceSearchRequest()))
            .Items.First();
        fixture.SwitchTenant(fixture.ForeignTenantId);

        (await fixture.Service.SearchOccurrencesAsync(new ProcurementCalendarOccurrenceSearchRequest())).TotalCount.Should().Be(0);
        await fixture.Service.Invoking(service => service.GetOccurrenceAsync(occurrence.Id))
            .Should().ThrowAsync<ProcurementCalendarNotFoundException>();

        fixture.SwitchTenant(fixture.TenantId);
        fixture.SwitchActor(fixture.OwnerUserId, "Calendar Owner");
        var acknowledged = await fixture.Service.AcknowledgeAsync(occurrence.Id,
            new ProcurementCalendarOccurrenceActionRequest { RowVersion = occurrence.RowVersion, Reason = "Task accepted." },
            "calendar-acknowledge");
        await fixture.Service.Invoking(service => service.CompleteAsync(occurrence.Id,
                new ProcurementCalendarOccurrenceActionRequest { RowVersion = occurrence.RowVersion, Reason = "Stale completion." },
                "calendar-stale-complete"))
            .Should().ThrowAsync<ProcurementCalendarConflictException>();

        var completed = await fixture.Service.CompleteAsync(occurrence.Id,
            new ProcurementCalendarOccurrenceActionRequest { RowVersion = acknowledged.RowVersion, Reason = "Obligation completed." },
            "calendar-complete");
        completed.Status.Should().Be(ProcurementCalendarOccurrenceStatus.Completed);
        await fixture.Service.Invoking(service => service.AcknowledgeAsync(occurrence.Id,
                new ProcurementCalendarOccurrenceActionRequest { RowVersion = completed.RowVersion, Reason = "Cannot reopen." },
                "calendar-reopen"))
            .Should().ThrowAsync<ProcurementCalendarConflictException>();
    }

    [Fact]
    public async Task EscalatedOccurrenceCannotMoveBackwardToAcknowledged()
    {
        await using var fixture = new Fixture();
        await fixture.CreatePublishedAsync();
        var entity = await fixture.Context.ProcurementCalendarOccurrences.FirstAsync();
        entity.Status = ProcurementCalendarOccurrenceStatus.Escalated;
        await fixture.Context.SaveChangesAsync();
        var occurrence = await fixture.Service.GetOccurrenceAsync(entity.Id);

        await fixture.Service.Invoking(service => service.AcknowledgeAsync(entity.Id,
                new ProcurementCalendarOccurrenceActionRequest
                {
                    RowVersion = occurrence.RowVersion,
                    Reason = "Acknowledge after escalation."
                }, "calendar-escalated-acknowledge"))
            .Should().ThrowAsync<ProcurementCalendarConflictException>();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _tenantId;
        private Guid _userId;
        private string _fullName = "Calendar Author";
        private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase) { "TenantAdmin" };
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            OwnerUserId = Guid.NewGuid();
            EscalationUserId = Guid.NewGuid();
            _tenantId = TenantId;
            _userId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            var tenant = new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active };
            var foreignTenant = new Tenant { Id = ForeignTenantId, Code = "OTHER", Name = "Other", Status = TenantStatus.Active };
            var ownerRole = new ApplicationRole("TDC_PROCUREMENT_OFFICER") { Id = Guid.NewGuid(), NormalizedName = "TDC_PROCUREMENT_OFFICER" };
            var escalationRole = new ApplicationRole("TDC_HEAD_OF_PROCUREMENT") { Id = Guid.NewGuid(), NormalizedName = "TDC_HEAD_OF_PROCUREMENT" };
            var owner = User(OwnerUserId, TenantId, "Calendar", "Owner");
            var escalation = User(EscalationUserId, TenantId, "Calendar", "Escalation");
            Context.AddRange(tenant, foreignTenant, ownerRole, escalationRole, owner, escalation);
            Context.Add(new UserTenant
            {
                Id = Guid.NewGuid(),
                UserId = owner.Id,
                TenantId = TenantId,
                User = owner,
                Tenant = tenant,
                Status = UserTenantStatus.Active,
                IsDefault = true
            });
            Context.ProcurementResponsibilityAssignments.AddRange(
                Assignment(TenantId, owner, ownerRole),
                Assignment(TenantId, escalation, escalationRole));
            Context.SaveChanges();

            var currentUser = new Mock<ICurrentUserProvider>();
            currentUser.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            currentUser.SetupGet(item => item.UserId).Returns(() => _userId);
            currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            currentUser.SetupGet(item => item.Username).Returns(() => $"{_userId:N}@tdc.test");
            currentUser.SetupGet(item => item.FullName).Returns(() => _fullName);
            currentUser.SetupGet(item => item.Roles).Returns(() => _roles);
            currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));

            _unitOfWork = new UnitOfWork(Context);
            var events = new ProcurementControlEventService(_unitOfWork, currentUser.Object,
                NullLogger<ProcurementControlEventService>.Instance);
            Access = new Mock<IProcurementAccessControlService>();
            Access.Setup(item => item.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true, Code = "ACCESS_ALLOWED", Message = "Allowed" });
            Notifications = new Mock<INotificationService>();
            Notifications.Setup(item => item.CreateNotificationAsync(
                    It.IsAny<CreateNotificationDto>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(() => new NotificationDto { Id = Guid.NewGuid() });
            Service = new ProcurementCalendarService(_unitOfWork, currentUser.Object, Access.Object, events,
                Notifications.Object, NullLogger<ProcurementCalendarService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public Guid OwnerUserId { get; }
        public Guid EscalationUserId { get; }
        public ApplicationDbContext Context { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public Mock<INotificationService> Notifications { get; }
        public ProcurementCalendarService Service { get; }

        public SaveProcurementCalendarProfileRequest ValidRequest()
        {
            var due = DateTime.UtcNow.AddDays(1);
            return new SaveProcurementCalendarProfileRequest
            {
                ProfileCode = "TDC-ANNUAL-CALENDAR",
                Name = "TDC annual procurement calendar",
                Description = "Controlled annual procurement obligations.",
                TimeZoneId = "UTC",
                GenerationHorizonDays = 30,
                CatchUpDays = 30,
                EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
                ChangeSummary = "Establish the approved annual procurement obligation calendar.",
                Rules = Enum.GetValues<ProcurementCalendarEventType>().Select(eventType => new ProcurementCalendarRuleRequest
                {
                    EventType = eventType,
                    Title = $"{eventType} obligation",
                    DueMonth = due.Month,
                    DueDay = due.Day,
                    DueLocalTime = due.TimeOfDay,
                    ReminderLeadDays = 14,
                    EscalationAfterDays = 1,
                    OwnerUserId = OwnerUserId,
                    EscalationUserId = EscalationUserId,
                    StatutoryReference = $"TDC-CALENDAR-SOURCE-{eventType}",
                    IsEnabled = true
                }).ToList()
            };
        }

        public ProcurementCalendarLifecycleRequest Lifecycle(string rowVersion, string? approvalReference = null) => new()
        {
            RowVersion = rowVersion,
            Reason = "Independent calendar lifecycle review completed.",
            ApprovalReference = approvalReference
        };

        public CloneProcurementCalendarProfileRequest Clone(int startsInDays) => new()
        {
            EffectiveFromUtc = DateTime.UtcNow.AddDays(startsInDays),
            ChangeSummary = $"Approved calendar replacement beginning in {startsInDays} days."
        };

        public async Task<ProcurementCalendarProfileDto> CreatePublishedAsync()
        {
            var created = await Service.CreateProfileAsync(ValidRequest(), "calendar-create");
            SwitchActor(Guid.NewGuid(), "Calendar Approver");
            return await Service.PublishProfileAsync(created.Id,
                Lifecycle(created.RowVersion, "TDC-CALENDAR-APPROVAL-001"), "calendar-publish");
        }

        public void SwitchActor(Guid userId, string fullName)
        {
            _userId = userId;
            _fullName = fullName;
        }

        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;

        private static ApplicationUser User(Guid id, Guid tenantId, string firstName, string lastName) => new()
        {
            Id = id,
            TenantId = tenantId,
            FirstName = firstName,
            LastName = lastName,
            UserName = $"{firstName}.{lastName}@tdc.test".ToLowerInvariant(),
            NormalizedUserName = $"{firstName}.{lastName}@tdc.test".ToUpperInvariant(),
            Email = $"{firstName}.{lastName}@tdc.test".ToLowerInvariant(),
            NormalizedEmail = $"{firstName}.{lastName}@tdc.test".ToUpperInvariant(),
            IsActive = true
        };

        private static ProcurementResponsibilityAssignment Assignment(Guid tenantId, ApplicationUser user, ApplicationRole role) => new()
        {
            TenantId = tenantId,
            UserId = user.Id,
            RoleId = role.Id,
            RoleName = role.Name!,
            EffectiveFrom = new DateTime(2020, 1, 1),
            IsActive = true,
            Reason = "Calendar test responsibility assignment.",
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
