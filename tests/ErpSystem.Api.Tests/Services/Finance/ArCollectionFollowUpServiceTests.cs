using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Regression coverage for the Finance-owned AR collection workspace. These tests
/// deliberately seed the posted settlement projection directly because that is the
/// accounting evidence the service must trust after invoices, receipts, credit notes,
/// and reversals have been posted by their respective workflows.
/// </summary>
public sealed class ArCollectionFollowUpServiceTests
{
    [Fact]
    [Trait("Requirement", "FR-AR-009")]
    public async Task WorkQueue_UsesPostedSettlementBalanceAndEnforcesTenantBoundary()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        SeedExposure(db, tenantId, "AR-TDC-001", 420m, customerOperationalBalance: 9_999m);
        SeedExposure(db, otherTenantId, "AR-OTHER-001", 770m, customerOperationalBalance: 770m);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId, userId);
        var result = await service.GetWorkQueueAsync(new DateTime(2026, 8, 8));

        result.Items.Should().ContainSingle();
        result.Items[0].InvoiceNumber.Should().Be("AR-TDC-001");
        result.Items[0].OutstandingAmount.Should().Be(420m,
            "collection decisions must use the posted settlement projection, not a mutable customer/invoice operational total");
        result.Items.Should().NotContain(item => item.InvoiceNumber == "AR-OTHER-001",
            "Finance work queues must never disclose another tenant's receivables");
    }

    [Fact]
    [Trait("Requirement", "FR-AR-009")]
    public async Task GenerateTasks_IsIdempotentAndLeavesPerTaskAuditEvidence()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        SeedExposure(db, tenantId, "AR-TDC-002", 1_250m);
        await db.SaveChangesAsync();
        var audit = new CapturingFinanceAuditService();
        var service = CreateService(db, tenantId, userId, audit);
        var request = new GenerateArCollectionTasksDto { AsOfDate = new DateTime(2026, 8, 8), AssignedToId = userId };

        var first = await service.GenerateTasksAsync(request);
        var second = await service.GenerateTasksAsync(request);

        first.CreatedCount.Should().Be(1);
        second.CreatedCount.Should().Be(0);
        second.ExistingCount.Should().Be(1);
        var primaryTasks = await db.CollectionActivities
            .Where(item => item.CollectionContext == CollectionActivityValues.FinanceArContext && item.IsPrimaryTask)
            .ToListAsync();
        primaryTasks.Should().ContainSingle("retries must not create duplicate officer work");
        primaryTasks[0].AssignedToId.Should().Be(userId);
        audit.Events.Should().Contain(item =>
            item.EventType == FinanceAuditEvents.ArCollectionTaskCreated &&
            item.ResourceId == primaryTasks[0].Id.ToString());
        audit.Events.Count(item => item.EventType == FinanceAuditEvents.ArCollectionTasksGenerated).Should().Be(2);
    }

    [Fact]
    [Trait("Requirement", "FR-AR-009")]
    public async Task GenerateTasks_AutoResolvesTaskWhenPostedExposureIsSettled()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        var exposure = SeedExposure(db, tenantId, "AR-TDC-003", 500m);
        await db.SaveChangesAsync();
        var audit = new CapturingFinanceAuditService();
        var service = CreateService(db, tenantId, userId, audit);
        var request = new GenerateArCollectionTasksDto { AsOfDate = new DateTime(2026, 8, 8), AssignedToId = userId };
        await service.GenerateTasksAsync(request);

        // This simulates the read model after a receipt/credit-note posting. The
        // collection task must follow posted evidence instead of remaining open
        // merely because an officer has not manually refreshed an invoice field.
        exposure.OutstandingAmount = 0m;
        exposure.SettlementStatus = SubledgerSettlementStatuses.Settled;
        exposure.LastRebuiltAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await service.GenerateTasksAsync(request);

        result.AutoResolvedCount.Should().Be(1);
        var task = await db.CollectionActivities.SingleAsync(item => item.IsPrimaryTask);
        task.CollectionStatus.Should().Be(CollectionActivityValues.ResolvedStatus);
        task.Outcome.Should().Be("Settled");
        task.CompletedById.Should().Be(userId);
        (await db.CollectionActivities.CountAsync(item => item.ParentActivityId == task.Id)).Should().Be(1,
            "the operational timeline should explain why Finance closed the task");
        audit.Events.Should().Contain(item => item.EventType == FinanceAuditEvents.ArCollectionTaskAutoResolved);
        (await service.GetWorkQueueAsync(new DateTime(2026, 8, 8))).Items.Should().BeEmpty();

        // A later receipt reversal can restore the posted debt. The same durable
        // task must return to the active queue instead of leaving a terminal task
        // attached to a live exposure or losing its previous recovery history.
        exposure.OutstandingAmount = 500m;
        exposure.SettlementStatus = SubledgerSettlementStatuses.Open;
        exposure.LastRebuiltAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var reactivation = await service.GenerateTasksAsync(request);

        reactivation.ReactivatedCount.Should().Be(1);
        task.CollectionStatus.Should().Be(CollectionActivityValues.PendingStatus);
        task.CompletedAt.Should().BeNull();
        audit.Events.Should().Contain(item => item.EventType == FinanceAuditEvents.ArCollectionTaskReactivated);
        (await service.GetWorkQueueAsync(new DateTime(2026, 8, 8))).Items.Should().ContainSingle();
    }

    [Fact]
    public void Model_ProtectsPrimaryTaskUniquenessAndConcurrentUpdates()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(CollectionActivity));

        entity.Should().NotBeNull();
        entity!.FindProperty(nameof(CollectionActivity.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        entity.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(CollectionActivity.TenantId), nameof(CollectionActivity.InvoiceId), nameof(CollectionActivity.CollectionContext) }));
    }

    [Fact]
    [Trait("Requirement", "FR-AR-009")]
    public async Task UpdateTask_RequiresEvidenceWhenEscalatingAnUnsettledExposure()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        SeedExposure(db, tenantId, "AR-TDC-004", 800m);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, userId);
        await service.GenerateTasksAsync(new GenerateArCollectionTasksDto
        {
            AsOfDate = new DateTime(2026, 8, 8),
            AssignedToId = userId
        });
        var task = await db.CollectionActivities.SingleAsync(item => item.IsPrimaryTask);

        // SQL Server generates this token. The InMemory provider used by this focused
        // service test does not, so provide a deterministic token to exercise the same
        // optimistic-concurrency contract before validating the escalation evidence.
        task.RowVersion = new byte[] { 1 };
        await db.SaveChangesAsync();
        var request = new UpdateArCollectionTaskDto
        {
            CollectionStatus = CollectionActivityValues.EscalatedStatus,
            AssignedToId = userId,
            Priority = 8,
            RowVersion = Convert.ToBase64String(task.RowVersion)
        };

        var action = () => service.UpdateTaskAsync(task.Id, request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires an outcome or explanatory note*");
        task.CollectionStatus.Should().Be(CollectionActivityValues.PendingStatus,
            "an unexplained escalation must not alter the active Finance task");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ar-collection-follow-up-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ArCollectionFollowUpService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        Guid userId,
        CapturingFinanceAuditService? audit = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns("tdc.finance.officer");
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());

        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(service => service.CreateNotificationAsync(
                It.IsAny<CreateNotificationDto>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(new NotificationDto());

        return new ArCollectionFollowUpService(
            db,
            currentUser.Object,
            audit ?? new CapturingFinanceAuditService(),
            notifications.Object,
            Mock.Of<ILogger<ArCollectionFollowUpService>>());
    }

    private static void SeedUser(ApplicationDbContext db, Guid tenantId, Guid userId)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "tdc.finance.officer",
            FirstName = "Finance",
            LastName = "Officer",
            IsActive = true
        });
    }

    private static SubledgerSettlementBalance SeedExposure(
        ApplicationDbContext db,
        Guid tenantId,
        string invoiceNumber,
        decimal outstandingAmount,
        decimal customerOperationalBalance = 0m)
    {
        var customerId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        db.Set<Customer>().Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            ReferenceNumber = $"CUST-{invoiceNumber}",
            CustomerCode = $"C-{invoiceNumber}",
            CustomerName = $"Customer {invoiceNumber}",
            Email = "accounts@example.test",
            Phone = "+233200000000",
            OutstandingBalance = customerOperationalBalance,
            CurrencyCode = "GHS"
        });
        var balance = new SubledgerSettlementBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReferenceNumber = $"SET-{invoiceNumber}",
            SourceModule = SubledgerSettlementModules.AccountsReceivable,
            CounterpartyId = customerId,
            SourceDocumentType = "CustomerInvoice",
            SourceDocumentId = invoiceId,
            SourceDocumentNumber = invoiceNumber,
            TransactionDate = new DateTime(2026, 6, 1),
            DueDate = new DateTime(2026, 6, 30),
            DocumentCurrencyCode = "GHS",
            FunctionalCurrencyCode = "GHS",
            OriginalDocumentAmount = outstandingAmount,
            OriginalFunctionalAmount = outstandingAmount,
            OutstandingAmount = outstandingAmount,
            SettlementStatus = SubledgerSettlementStatuses.Open,
            RebuildBatchId = Guid.NewGuid(),
            LastRebuiltAt = DateTime.UtcNow
        };
        db.SubledgerSettlementBalances.Add(balance);
        return balance;
    }

    private sealed class CapturingFinanceAuditService : IFinanceAuditService
    {
        public List<FinanceAuditEventDto> Events { get; } = new();

        public Task<AuditLog> RecordAsync(FinanceAuditEventDto auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.FromResult(new AuditLog());
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
            Guid tenantId,
            string resource,
            string resourceId,
            int limit = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
    }
}
