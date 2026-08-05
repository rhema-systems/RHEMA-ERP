using ErpSystem.Api.Services.Finance.Fiscal;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceCloseAlertServiceTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodCloseAlerts")]
    public async Task ProcessDueAlerts_ShouldDeliverTaskReminderOnceAcrossRepeatedRuns()
    {
        var now = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var assignee = CreateUser(tenantId, "Ama", "Mensah");
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var cycle = SeedCycle(db, tenantId, now);
        db.Users.Add(assignee);
        db.FinanceCloseTasks.Add(new FinanceCloseTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycle.Id,
            TaskCode = "BANK_RECONCILIATION",
            Title = "Cash and bank reconciliation",
            Category = "Cash & Bank",
            Sequence = 80,
            Status = FinanceCloseTaskStatuses.Pending,
            AssignedToUserId = assignee.Id,
            AssignedToUserName = assignee.FullName,
            DueAt = now.AddHours(12)
        });
        await db.SaveChangesAsync();

        var notificationService = CreateSuccessfulNotificationMock();
        var service = CreateService(db, notificationService.Object);

        var first = await service.ProcessDueAlertsAsync(now);
        var second = await service.ProcessDueAlertsAsync(now.AddMinutes(15));

        first.DeliveredCount.Should().Be(1);
        second.DeliveredCount.Should().Be(0);
        second.SkippedCount.Should().Be(1);
        var delivery = await db.FinanceCloseAlertDeliveries.SingleAsync();
        delivery.AlertType.Should().Be(FinanceCloseAlertTypes.TaskDueSoon);
        delivery.Status.Should().Be(FinanceCloseAlertDeliveryStatuses.Delivered);
        delivery.RecipientUserId.Should().Be(assignee.Id);
        notificationService.Verify(service => service.CreateNotificationAsync(
            It.Is<CreateNotificationDto>(dto =>
                dto.RecipientId == assignee.Id &&
                dto.Type == $"FinanceClose.{FinanceCloseAlertTypes.TaskDueSoon}" &&
                dto.EntityId == cycle.Id),
            assignee.Id,
            tenantId), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodCloseAlerts")]
    public async Task ProcessDueAlerts_ShouldExcludeMakerAndCrossTenantReviewerAndEscalateToTdcHigherTier()
    {
        var now = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTHER");

        var requester = CreateUserWithPermission(
            tenantId,
            "Kojo",
            "Requester",
            "Finance Manager",
            FinancePermissions.ApproveCloseExceptionWaivers);
        var controller = CreateUserWithPermission(
            tenantId,
            "Esi",
            "Controller",
            "Financial Controller",
            FinancePermissions.ApproveCloseExceptionWaivers);
        var crossTenantReviewer = CreateUserWithPermission(
            otherTenantId,
            "Kweku",
            "Other",
            "Financial Controller",
            FinancePermissions.ApproveCloseExceptionWaivers);
        db.Users.AddRange(requester, controller, crossTenantReviewer);

        var cycle = SeedCycle(db, tenantId, now);
        db.FinanceCloseExceptionWaivers.Add(new FinanceCloseExceptionWaiver
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycle.Id,
            FinanceCloseTaskId = Guid.NewGuid(),
            FinanceCloseCheckSnapshotId = Guid.NewGuid(),
            FinanceCloseEvidenceAttachmentId = Guid.NewGuid(),
            CheckCode = "FX_REVALUATION",
            EvidenceFingerprint = new string('a', 64),
            Status = FinanceCloseWaiverStatuses.Requested,
            Justification = "Controlled temporary acceptance supported by retained reconciliation evidence.",
            RequestedByUserId = requester.Id,
            RequestedByUserName = requester.FullName,
            RequestedAt = now.AddHours(-25)
        });
        await db.SaveChangesAsync();

        var notificationService = CreateSuccessfulNotificationMock();
        var result = await CreateService(db, notificationService.Object).ProcessDueAlertsAsync(now);

        // The Financial Controller receives the initial permission-based review request and the
        // separate 24-hour higher-tier escalation. The requester and other tenant receive none.
        result.DeliveredCount.Should().Be(2);
        (await db.FinanceCloseAlertDeliveries
                .Where(item => item.RecipientUserId == controller.Id)
                .Select(item => item.AlertType)
                .ToListAsync())
            .Should().BeEquivalentTo(
                FinanceCloseAlertTypes.WaiverReviewRequested,
                FinanceCloseAlertTypes.WaiverReviewEscalation);
        (await db.FinanceCloseAlertDeliveries.AnyAsync(item => item.RecipientUserId == requester.Id)).Should().BeFalse();
        (await db.FinanceCloseAlertDeliveries.AnyAsync(item => item.RecipientUserId == crossTenantReviewer.Id)).Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodCloseAlerts")]
    public async Task ProcessDueAlerts_ShouldRetryFailedDeliveryOnlyAfterControlledDelay()
    {
        var now = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var assignee = CreateUser(tenantId, "Adwoa", "Officer");
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var cycle = SeedCycle(db, tenantId, now);
        db.Users.Add(assignee);
        db.FinanceCloseTasks.Add(new FinanceCloseTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycle.Id,
            TaskCode = "AP_UNAPPLIED_BALANCES",
            Title = "Review unapplied supplier balances",
            Category = "Accounts Payable",
            Sequence = 60,
            Status = FinanceCloseTaskStatuses.Pending,
            AssignedToUserId = assignee.Id,
            DueAt = now.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var attempt = 0;
        var notificationService = new Mock<INotificationService>();
        notificationService
            .Setup(service => service.CreateNotificationAsync(
                It.IsAny<CreateNotificationDto>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(() =>
            {
                attempt++;
                if (attempt == 1)
                    throw new InvalidOperationException("Notification queue temporarily unavailable");
                return new NotificationDto { Id = Guid.NewGuid() };
            });
        var service = CreateService(db, notificationService.Object);

        (await service.ProcessDueAlertsAsync(now)).FailedCount.Should().Be(1);
        (await service.ProcessDueAlertsAsync(now.AddMinutes(30))).SkippedCount.Should().Be(1);
        (await service.ProcessDueAlertsAsync(now.AddMinutes(61))).DeliveredCount.Should().Be(1);

        var delivery = await db.FinanceCloseAlertDeliveries.SingleAsync();
        delivery.AttemptCount.Should().Be(2);
        delivery.Status.Should().Be(FinanceCloseAlertDeliveryStatuses.Delivered);
        delivery.LastError.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodCloseAlerts")]
    public async Task ProcessDueAlerts_ShouldNotifyIndependentReopenApproverAndRetainRequestLink()
    {
        var now = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var requester = CreateUser(tenantId, "Akosua", "Manager");
        var controller = CreateUserWithPermission(
            tenantId,
            "Esi",
            "Controller",
            "Financial Controller",
            FinancePermissions.ApproveAccountingPeriodReopens);
        db.Users.AddRange(requester, controller);
        var cycle = SeedCycle(db, tenantId, now);
        cycle.Status = FinanceCloseStatuses.Closed;
        cycle.ClosedAt = now.AddDays(-2);
        cycle.FiscalPeriod.PeriodStatus = "Closed";
        cycle.FiscalPeriod.IsOpen = false;
        cycle.FiscalPeriod.IsClosed = true;
        var reopenRequest = new FinancePeriodReopenRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalPeriodId = cycle.FiscalPeriodId,
            FinanceCloseCycleId = cycle.Id,
            Status = FinancePeriodReopenStatuses.PendingApproval,
            Reason = "External audit correction requires controlled reopening",
            AffectedPeriodAssessment = "The adjustment is confined to the closed period and requires recertification.",
            ImpactSnapshotJson = "{}",
            ImpactFingerprint = new string('a', 64),
            RequestedByUserId = requester.Id,
            RequestedByUserName = requester.FullName,
            RequestedAt = now.AddHours(-2)
        };
        db.FinancePeriodReopenRequests.Add(reopenRequest);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateSuccessfulNotificationMock().Object)
            .ProcessDueAlertsAsync(now);

        result.DeliveredCount.Should().Be(1);
        var delivery = await db.FinanceCloseAlertDeliveries.SingleAsync();
        delivery.AlertType.Should().Be(FinanceCloseAlertTypes.PeriodReopenApprovalRequested);
        delivery.FinancePeriodReopenRequestId.Should().Be(reopenRequest.Id);
        delivery.RecipientUserId.Should().Be(controller.Id);
        delivery.RecipientUserId.Should().NotBe(requester.Id);
    }

    private static FinanceCloseAlertService CreateService(
        ApplicationDbContext db,
        INotificationService notificationService) => new(
            db,
            notificationService,
            Mock.Of<ILogger<FinanceCloseAlertService>>());

    private static Mock<INotificationService> CreateSuccessfulNotificationMock()
    {
        var service = new Mock<INotificationService>();
        service.Setup(item => item.CreateNotificationAsync(
                It.IsAny<CreateNotificationDto>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(() => new NotificationDto { Id = Guid.NewGuid() });
        return service;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TDC")
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"{code} Tenant",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
    }

    private static FinanceCloseCycle SeedCycle(ApplicationDbContext db, Guid tenantId, DateTime now)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PeriodName = "July 2026",
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodNumber = 7,
            FiscalYearId = Guid.NewGuid(),
            IsOpen = true,
            PeriodStatus = "Open"
        };
        var cycle = new FinanceCloseCycle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalPeriodId = period.Id,
            FiscalPeriod = period,
            CycleNumber = 1,
            TemplateCode = "TDC-MONTH-END",
            CloseType = FinanceCloseTemplateTypes.MonthEnd,
            TemplateVersion = 3,
            Status = FinanceCloseStatuses.InProgress,
            StartedAt = now.AddDays(-2)
        };
        db.FiscalPeriods.Add(period);
        db.FinanceCloseCycles.Add(cycle);
        return cycle;
    }

    private static ApplicationUser CreateUser(Guid tenantId, string firstName, string lastName) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        UserName = $"{firstName}.{lastName}".ToLowerInvariant(),
        NormalizedUserName = $"{firstName}.{lastName}".ToUpperInvariant(),
        FirstName = firstName,
        LastName = lastName,
        Email = $"{firstName}.{lastName}@example.test".ToLowerInvariant(),
        IsActive = true
    };

    private static ApplicationUser CreateUserWithPermission(
        Guid tenantId,
        string firstName,
        string lastName,
        string roleName,
        string permissionName)
    {
        var user = CreateUser(tenantId, firstName, lastName);
        var role = new ApplicationRole(roleName) { Id = Guid.NewGuid(), NormalizedName = roleName.ToUpperInvariant() };
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Name = permissionName,
            DisplayName = permissionName,
            Category = FinancePermissions.CategoryPeriodClose
        };
        role.RolePermissions.Add(new RolePermission
        {
            RoleId = role.Id,
            PermissionId = permission.Id,
            Role = role,
            Permission = permission
        });
        user.UserRoles.Add(new ApplicationUserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            User = user,
            Role = role
        });
        return user;
    }
}
