using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
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

public sealed class ArCollectionFollowUpServiceTests
{
    [Fact]
    public async Task WorkQueue_ResolvesBusinessPartnerByIdAndEnforcesTenantBoundary()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        SeedExposure(db, tenantId, "AR-TDC-001", 420m);
        SeedExposure(db, otherTenantId, "AR-OTHER-001", 770m);
        await db.SaveChangesAsync();

        var result = await CreateService(db, tenantId, userId).GetWorkQueueAsync(new DateTime(2026, 8, 8));

        result.Items.Should().ContainSingle();
        var item = result.Items[0];
        item.InvoiceNumber.Should().Be("AR-TDC-001");
        item.OutstandingAmount.Should().Be(420m);
        item.CustomerCode.Should().Be("C-AR-TDC-001");
        item.CustomerName.Should().Be("Customer AR-TDC-001");
        item.CustomerEmail.Should().Be("accounts@example.test");
        item.CustomerPhone.Should().Be("+233200000000");
        item.CustomerAddress.Should().Be("1 Finance Avenue");
        item.CustomerCity.Should().Be("Tema");
        item.CustomerState.Should().Be("Greater Accra");
        item.CustomerCountry.Should().Be("Ghana");
        item.CustomerPostalCode.Should().Be("GT-001-0001");
        item.IsPartnerResolved.Should().BeTrue();
    }

    [Fact]
    public async Task WorkQueue_KeepsInactiveAndMissingPartnersReadableWithoutCrossTenantFallback()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        SeedExposure(db, tenantId, "AR-INACTIVE", 100m, partnerIsActive: false);
        var missing = SeedExposure(db, tenantId, "AR-MISSING", 200m, seedPartner: false);
        db.BusinessPartners.Add(NewPartner(otherTenantId, missing.CounterpartyId, "OTHER", "Wrong tenant"));
        await db.SaveChangesAsync();

        var result = await CreateService(db, tenantId, userId).GetWorkQueueAsync(new DateTime(2026, 8, 8));

        result.Items.Should().HaveCount(2);
        result.Items.Single(item => item.InvoiceNumber == "AR-INACTIVE").IsPartnerResolved.Should().BeTrue();
        var unresolved = result.Items.Single(item => item.InvoiceNumber == "AR-MISSING");
        unresolved.IsPartnerResolved.Should().BeFalse();
        unresolved.CustomerName.Should().Be("Unresolved business partner");
        unresolved.PartnerResolutionMessage.Should().Contain("current tenant");
    }

    [Fact]
    public async Task GenerateTasks_IsIdempotent_DefaultsToCurrentUser_Notifies_AndSkipsUnresolvedNewTasks()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        SeedExposure(db, tenantId, "AR-VALID", 900m);
        SeedExposure(db, tenantId, "AR-NO-PARTNER", 400m, seedPartner: false);
        await db.SaveChangesAsync();
        var notifications = NotificationMock();
        var audit = new CapturingFinanceAuditService();
        var service = CreateService(db, tenantId, userId, audit, notifications);
        var request = new GenerateArCollectionTasksDto { AsOfDate = new DateTime(2026, 8, 8) };

        var first = await service.GenerateTasksAsync(request);
        var second = await service.GenerateTasksAsync(request);

        first.CreatedCount.Should().Be(1);
        first.SkippedUnresolvedPartnerCount.Should().Be(1);
        second.CreatedCount.Should().Be(0);
        second.ExistingCount.Should().Be(1);
        (await db.CollectionActivities.CountAsync(item => item.IsPrimaryTask)).Should().Be(1);
        var task = await db.CollectionActivities.SingleAsync(item => item.IsPrimaryTask);
        task.AssignedToId.Should().Be(userId);
        notifications.Verify(service => service.CreateNotificationAsync(
            It.Is<CreateNotificationDto>(notification => notification.Type == "Finance.AR.CollectionFollowUp"),
            userId,
            tenantId), Times.Once);
        audit.Events.Should().Contain(item => item.EventType == FinanceAuditEvents.ArCollectionTaskCreated);
    }

    [Fact]
    public async Task GenerateTasks_ResolvesSettledTaskAndReactivatesReturnedExposure()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        var exposure = SeedExposure(db, tenantId, "AR-LIFECYCLE", 500m);
        await db.SaveChangesAsync();
        var audit = new CapturingFinanceAuditService();
        var service = CreateService(db, tenantId, userId, audit);
        var request = new GenerateArCollectionTasksDto { AsOfDate = new DateTime(2026, 8, 8) };
        await service.GenerateTasksAsync(request);

        exposure.OutstandingAmount = 0m;
        exposure.SettlementStatus = SubledgerSettlementStatuses.Settled;
        exposure.LastRebuiltAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        (await service.GenerateTasksAsync(request)).AutoResolvedCount.Should().Be(1);
        var task = await db.CollectionActivities.SingleAsync(item => item.IsPrimaryTask);
        task.CollectionStatus.Should().Be(CollectionActivityValues.ResolvedStatus);
        (await db.CollectionActivities.CountAsync(item => item.ParentActivityId == task.Id)).Should().Be(1);

        exposure.OutstandingAmount = 500m;
        exposure.SettlementStatus = SubledgerSettlementStatuses.Open;
        exposure.LastRebuiltAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        (await service.GenerateTasksAsync(request)).ReactivatedCount.Should().Be(1);
        task.CollectionStatus.Should().Be(CollectionActivityValues.PendingStatus);
        audit.Events.Should().Contain(item => item.EventType == FinanceAuditEvents.ArCollectionTaskReactivated);
    }

    [Fact]
    public async Task Summary_GroupsNativeCurrenciesAndUsesHistoricalFunctionalEvidenceOnly()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        SeedExposure(db, tenantId, "AR-GHS", 300_000m, currencyCode: "GHS", originalFunctionalAmount: 300_000m);
        SeedExposure(db, tenantId, "AR-USD", 70_000m, currencyCode: "USD", originalFunctionalAmount: 700_000m);
        await db.SaveChangesAsync();

        var result = await CreateService(db, tenantId, userId).GetSummaryAsync(new DateTime(2026, 8, 8));

        result.NativeCurrencyTotals.Should().Contain(item => item.CurrencyCode == "GHS" && item.OutstandingAmount == 300_000m);
        result.NativeCurrencyTotals.Should().Contain(item => item.CurrencyCode == "USD" && item.OutstandingAmount == 70_000m);
        result.FunctionalCurrencyCode.Should().Be("GHS");
        result.FunctionalOutstandingTotal.Should().Be(1_000_000m);
        result.FunctionalTotalUnavailableReason.Should().BeNull();
    }

    [Fact]
    public async Task Summary_OmitsFunctionalTotalWhenHistoricalEvidenceIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        SeedExposure(db, tenantId, "AR-NO-FX", 70_000m, currencyCode: "USD", originalFunctionalAmount: 0m);
        await db.SaveChangesAsync();

        var result = await CreateService(db, tenantId, userId).GetSummaryAsync(new DateTime(2026, 8, 8));

        result.NativeCurrencyTotals.Should().ContainSingle(item => item.CurrencyCode == "USD");
        result.FunctionalOutstandingTotal.Should().BeNull();
        result.FunctionalPromisedTotal.Should().BeNull();
        result.FunctionalTotalUnavailableReason.Should().Contain("AR-NO-FX");
    }

    [Fact]
    public async Task UpdateReminderDetailAndHistory_ResolveThroughBusinessPartner()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedUser(db, tenantId, userId);
        SeedExposure(db, tenantId, "AR-REMINDER", 500m);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, userId);
        await service.GenerateTasksAsync(new GenerateArCollectionTasksDto { AsOfDate = new DateTime(2026, 8, 8) });
        var task = await db.CollectionActivities.SingleAsync(item => item.IsPrimaryTask);
        task.RowVersion = new byte[] { 1 };
        await db.SaveChangesAsync();

        var updated = await service.UpdateTaskAsync(task.Id, new UpdateArCollectionTaskDto
        {
            CollectionStatus = CollectionActivityValues.InProgressStatus,
            AssignedToId = userId,
            FollowUpDate = new DateTime(2026, 8, 10),
            Priority = 7,
            PromisedAmount = 0m,
            Notes = "Customer contact started.",
            RowVersion = Convert.ToBase64String(task.RowVersion)
        });

        var reminder = await service.RecordReminderAsync(task.Id, new RecordArCollectionReminderDto
        {
            Channel = "Email",
            Message = "Please settle the overdue balance.",
            ConfirmedDispatched = true,
            RowVersion = updated.RowVersion ?? Convert.ToBase64String(task.RowVersion)
        });

        updated.CustomerName.Should().Be("Customer AR-REMINDER");
        updated.CustomerEmail.Should().Be("accounts@example.test");
        updated.Priority.Should().Be(7);
        reminder.ReminderRecipient.Should().Be("accounts@example.test");
        (await service.GetHistoryAsync(task.Id)).Should().Contain(item => item.Id == reminder.Id);
    }

    [Fact]
    public void Model_ProtectsPrimaryTaskUniquenessAndConcurrentUpdates()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(CollectionActivity));

        entity.Should().NotBeNull();
        entity!.FindProperty(nameof(CollectionActivity.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        entity.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { nameof(CollectionActivity.TenantId), nameof(CollectionActivity.InvoiceId), nameof(CollectionActivity.CollectionContext) }));
    }

    [Fact]
    public void FinanceCollectionService_DoesNotReferenceLegacyCustomerStorage()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AR", "ArCollectionFollowUpService.cs"));
        var entity = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Entities", "Sales", "CollectionEntities.cs"));
        var migration = Directory.GetFiles(Path.Combine(root, "src", "ErpSystem.Data", "Migrations"), "*CanonicalCollectionBusinessPartnerIdentity.cs")
            .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));
        var migrationSource = File.ReadAllText(migration);

        source.Should().NotContain("Set<Customer>");
        source.Should().NotContain("Include(item => item.Customer)");
        source.Should().NotContain("task.Customer?.");
        source.Should().NotContain("CustomerId");
        source.Should().Contain("_db.BusinessPartners");
        entity.Should().Contain("public Guid BusinessPartnerId")
            .And.Contain("public virtual BusinessPartner BusinessPartner")
            .And.NotContain("public Guid CustomerId")
            .And.NotContain("public virtual Customer Customer");
        migrationSource.Should().Contain("requires CollectionActivities to be empty")
            .And.Contain("requires PaymentPlans to be empty")
            .And.Contain("FK_CollectionActivities_BusinessPartners_BusinessPartnerId")
            .And.Contain("FK_PaymentPlans_BusinessPartners_BusinessPartnerId");
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ar-collection-{Guid.NewGuid():N}")
            .Options);

    private static ArCollectionFollowUpService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        Guid userId,
        CapturingFinanceAuditService? audit = null,
        Mock<INotificationService>? notifications = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns("finance.officer");
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        return new ArCollectionFollowUpService(
            db,
            currentUser.Object,
            audit ?? new CapturingFinanceAuditService(),
            (notifications ?? NotificationMock()).Object,
            Mock.Of<ILogger<ArCollectionFollowUpService>>());
    }

    private static Mock<INotificationService> NotificationMock()
    {
        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.CreateNotificationAsync(
                It.IsAny<CreateNotificationDto>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(new NotificationDto());
        return notifications;
    }

    private static void SeedUser(ApplicationDbContext db, Guid tenantId, Guid userId) => db.Users.Add(new ApplicationUser
    {
        Id = userId,
        TenantId = tenantId,
        UserName = "finance.officer",
        FirstName = "Finance",
        LastName = "Officer",
        IsActive = true
    });

    private static SubledgerSettlementBalance SeedExposure(
        ApplicationDbContext db,
        Guid tenantId,
        string invoiceNumber,
        decimal outstandingAmount,
        string currencyCode = "GHS",
        decimal? originalFunctionalAmount = null,
        bool seedPartner = true,
        bool partnerIsActive = true)
    {
        var partnerId = Guid.NewGuid();
        if (seedPartner)
        {
            var partner = NewPartner(tenantId, partnerId, $"C-{invoiceNumber}", $"Customer {invoiceNumber}");
            partner.Currency = currencyCode;
            partner.IsActive = partnerIsActive;
            db.BusinessPartners.Add(partner);
        }

        var balance = new SubledgerSettlementBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReferenceNumber = $"SET-{invoiceNumber}",
            SourceModule = SubledgerSettlementModules.AccountsReceivable,
            CounterpartyId = partnerId,
            SourceDocumentType = "CustomerInvoice",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentNumber = invoiceNumber,
            TransactionDate = new DateTime(2026, 6, 1),
            DueDate = new DateTime(2026, 6, 30),
            DocumentCurrencyCode = currencyCode,
            FunctionalCurrencyCode = "GHS",
            OriginalDocumentAmount = outstandingAmount,
            OriginalFunctionalAmount = originalFunctionalAmount ?? outstandingAmount,
            OutstandingAmount = outstandingAmount,
            SettlementStatus = SubledgerSettlementStatuses.Open,
            RebuildBatchId = Guid.NewGuid(),
            LastRebuiltAt = DateTime.UtcNow
        };
        db.SubledgerSettlementBalances.Add(balance);
        return balance;
    }

    private static BusinessPartner NewPartner(Guid tenantId, Guid id, string code, string name) => new()
    {
        Id = id,
        TenantId = tenantId,
        PartnerCode = code,
        PartnerName = name,
        PartnerType = "Customer",
        PrimaryEmail = "accounts@example.test",
        PrimaryPhone = "+233200000000",
        PhysicalAddress = "1 Finance Avenue",
        PhysicalCity = "Tema",
        PhysicalState = "Greater Accra",
        PhysicalCountry = "Ghana",
        PhysicalPostalCode = "GT-001-0001",
        IsActive = true
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
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
