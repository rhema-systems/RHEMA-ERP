using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ErpSystem.Shared;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryReplenishmentControlTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public void Recommendation_register_is_tenant_safe_concurrent_and_append_only()
    {
        var model = _context.GetService<IDesignTimeModel>().Model;
        var recommendation = model.FindEntityType(typeof(InventoryReplenishmentRecommendation))!;
        recommendation.GetTableName().Should().Be("InventoryReplenishmentRecommendations");
        recommendation.FindProperty(nameof(InventoryReplenishmentRecommendation.RowVersion))!
            .IsConcurrencyToken.Should().BeTrue();
        recommendation.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "TenantId", "WarehouseId", "InventoryItemId", "Status" }) &&
            index.GetFilter()!.Contains("Status] IN (1,2,3)", StringComparison.Ordinal));
        recommendation.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "TenantId", "IdempotencyKey" }));
        recommendation.GetCheckConstraints().Select(value => value.Name).Should().Contain(new[]
        {
            "CK_InventoryReplenishment_DemandWindow",
            "CK_InventoryReplenishment_Quantities",
            "CK_InventoryReplenishment_Hashes",
            "CK_InventoryReplenishment_Status"
        });

        var action = model.FindEntityType(typeof(InventoryReplenishmentAction))!;
        action.GetTableName().Should().Be("InventoryReplenishmentActions");
        action.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "TenantId", "RecommendationId", "Sequence" }));
    }

    [Fact]
    public void Calculation_accounts_for_demand_open_supply_supplier_moq_and_order_multiple()
    {
        var item = new InventoryItem
        {
            MinimumLevel = 10,
            MaximumLevel = 120,
            ReorderLevel = 25,
            ReorderQuantity = 40,
            SafetyStock = 20,
            LeadTimeDays = 20,
            SafetyLeadTimeDays = 5
        };
        var balance = new WarehouseQuantity
        {
            CurrentStock = 30,
            AvailableStock = 20,
            AllocatedStock = 10,
            ReorderLevel = 30,
            MaxStock = 100,
            InventoryItem = item
        };
        var supplier = new ItemSupplier
        {
            LeadTimeDays = 10,
            MinimumOrderQuantity = 50,
            OrderMultiple = 25
        };
        var calculate = typeof(ErpSystem.Core.Services.Inventory.InventoryReplenishmentService)
            .GetMethod("Calculate", BindingFlags.Static | BindingFlags.NonPublic)!;
        var from = DateTime.UtcNow.AddDays(-90);
        var result = calculate.Invoke(null, new object[] { balance, supplier, 90m, 90, 10m, 5m, from, DateTime.UtcNow })!;

        result.GetType().GetProperty("AverageDailyDemand")!.GetValue(result).Should().Be(1m);
        result.GetType().GetProperty("LeadTimeDemand")!.GetValue(result).Should().Be(15m);
        result.GetType().GetProperty("RecommendedQuantity")!.GetValue(result).Should().Be(100m);
        result.GetType().GetProperty("Explanation")!.GetValue(result)!.ToString()
            .Should().Contain("open replenishment PR 5").And.Contain("MOQ 50").And.Contain("multiple 25");
    }

    [Fact]
    public void Migration_is_bounded_and_enforces_lifecycle_sod_and_immutable_history()
    {
        var migration = new TDC0612InventoryReplenishment();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<CreateTableOperation>().Select(value => value.Name).Should().Equal(
            "InventoryReplenishmentRecommendations", "InventoryReplenishmentActions");
        builder.Operations.OfType<AddColumnOperation>().Should().BeEmpty();
        builder.Operations.OfType<DropTableOperation>().Should().BeEmpty();
        var sql = string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        sql.Should().Contain("TR_InventoryReplenishmentActions_Immutable");
        sql.Should().Contain("TR_InventoryReplenishmentRecommendations_Guard");
        sql.Should().Contain("INV_REPLENISHMENT_CALCULATION_IMMUTABLE");
        sql.Should().Contain("INV_REPLENISHMENT_INDEPENDENT_DECISION_REQUIRED");
        sql.Should().Contain("d.Status = 3 AND i.Status IN (5,6,7)");
    }

    [Fact]
    public void Service_composes_shared_owners_and_creates_only_a_governed_draft_pr()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Inventory",
            "InventoryReplenishmentService.cs"));
        service.Should().Contain("Repository<WarehouseQuantity>()");
        service.Should().Contain("Repository<StockMovement>()");
        service.Should().Contain("Repository<PurchaseOrderItem>()");
        service.Should().Contain("Repository<ItemSupplier>()");
        service.Should().Contain("IWorkflowIntegrationService");
        service.Should().Contain("INotificationService");
        service.Should().Contain("IProcurementRequisitionLinkageService");
        service.Should().Contain("RequisitionType.StockReplenishment");
        service.Should().Contain("Status = \"Draft\"");
        service.Should().NotContain("Status = \"Approved\"");
        service.Should().Contain("value.GeneratedById == _currentUser.UserId || value.SubmittedById == _currentUser.UserId");
    }

    [Fact, Trait("Batch", "E2E-023")]
    public async Task Lifecycle_generates_alerts_requires_independent_approval_and_creates_one_draft_pr()
    {
        var tenantId = Guid.NewGuid();
        var generatorId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var generator = new ApplicationUser
        {
            Id = generatorId, TenantId = tenantId, UserName = "generator@tdc.test",
            FirstName = "Replenishment", LastName = "Generator", IsActive = true
        };
        var approver = new ApplicationUser
        {
            Id = approverId, TenantId = tenantId, UserName = "approver@tdc.test",
            FirstName = "Independent", LastName = "Approver", IsActive = true
        };
        var warehouse = new Warehouse
        {
            Id = warehouseId, TenantId = tenantId, Code = "MAIN", Name = "Main Stores", IsActive = true
        };
        var item = new InventoryItem
        {
            Id = itemId, TenantId = tenantId, ItemCode = "REP-001", Name = "Replenishment item",
            CategoryId = Guid.NewGuid(), UnitOfMeasure = "EA", Status = ItemStatus.Active,
            MinimumLevel = 20, MaximumLevel = 100, ReorderLevel = 30, ReorderQuantity = 40,
            SafetyStock = 10, LeadTimeDays = 7, SafetyLeadTimeDays = 3, AverageCost = 5
        };
        var balance = new WarehouseQuantity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouseId, InventoryItemId = itemId,
            CurrentStock = 15, AvailableStock = 10, AllocatedStock = 5, ReorderLevel = 30,
            MaxStock = 100, AverageCost = 5, Warehouse = warehouse, InventoryItem = item
        };
        context.AddRange(
            new Tenant { Id = tenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active },
            generator, approver, warehouse, item, balance,
            new StockMovement
            {
                Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = itemId, WarehouseId = warehouseId,
                MovementType = "Issue", Quantity = 90, MovementDate = DateTime.UtcNow.AddDays(-30),
                ReferenceType = ReferenceType.Manual
            });
        await context.SaveChangesAsync();

        var actorId = generatorId;
        var actorTenantId = tenantId;
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(value => value.IsAuthenticated).Returns(true);
        currentUser.SetupGet(value => value.UserId).Returns(() => actorId);
        currentUser.SetupGet(value => value.TenantId).Returns(() => actorTenantId);
        currentUser.SetupGet(value => value.Username).Returns(() => actorId == generatorId ? generator.UserName! : approver.UserName!);

        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(value => value.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true, Code = "ALLOWED", Message = "Allowed", ActorUserId = actorId,
                    TenantId = tenantId, PermissionCode = request.PermissionCode,
                    WarehouseId = request.WarehouseId, CorrelationId = correlation
                });
        var controlEvents = new Mock<IProcurementControlEventService>();
        controlEvents.Setup(value => value.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementControlEventDto { Id = Guid.NewGuid(), TenantId = tenantId });
        var notifications = new Mock<INotificationService>();
        notifications.Setup(value => value.CreateNotificationAsync(
                It.IsAny<CreateNotificationDto>(), It.IsAny<Guid>(), tenantId))
            .ReturnsAsync(() => new NotificationDto { Id = Guid.NewGuid() });

        var workflowInstanceId = Guid.NewGuid();
        var workflow = new Mock<IWorkflowIntegrationService>();
        workflow.Setup(value => value.SubmitAsync("InventoryReplenishmentRecommendation", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowIntegrationResult(new WorkflowExecutionResult
            {
                Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = workflowInstanceId
            }, WorkflowOutcome.Pending));
        workflow.Setup(value => value.CanUserApproveAsync(
                "InventoryReplenishmentRecommendation", It.IsAny<Guid>(), approverId)).ReturnsAsync(true);
        workflow.Setup(value => value.ProcessApprovalAsync(
                "InventoryReplenishmentRecommendation", It.IsAny<Guid>(), approverId, "Approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowIntegrationResult(new WorkflowExecutionResult
            {
                Success = true, Status = WorkflowInstanceStatus.Completed, WorkflowInstanceId = workflowInstanceId
            }, WorkflowOutcome.Approved));

        var linkage = new Mock<IProcurementRequisitionLinkageService>();
        linkage.Setup(value => value.PrepareAsync(It.IsAny<PurchaseRequisition>(),
                It.IsAny<SavePurchaseRequisitionLinkageRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        linkage.Setup(value => value.RecordMutationAsync(It.IsAny<PurchaseRequisition>(), It.IsAny<string>(),
                It.IsAny<PurchaseRequisitionLinkageDto?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var requisitions = new Mock<IPurchaseRequisitionRepository>();
        PurchaseRequisition? createdRequisition = null;
        requisitions.Setup(value => value.GenerateRequisitionNumberAsync()).ReturnsAsync("PR-REP-001");
        requisitions.Setup(value => value.CreateRequisitionAsync(It.IsAny<PurchaseRequisition>()))
            .Callback<PurchaseRequisition>(value => createdRequisition = value)
            .ReturnsAsync((PurchaseRequisition value) => value);
        var requisitionItems = new Mock<IPurchaseRequisitionItemRepository>();
        PurchaseRequisitionItem? createdItem = null;
        requisitionItems.Setup(value => value.CreateItemAsync(It.IsAny<PurchaseRequisitionItem>()))
            .Callback<PurchaseRequisitionItem>(value => createdItem = value)
            .ReturnsAsync((PurchaseRequisitionItem value) => value);

        using var unitOfWork = new UnitOfWork(context);
        var service = new InventoryReplenishmentService(unitOfWork, currentUser.Object, access.Object,
            controlEvents.Object, notifications.Object, workflow.Object, linkage.Object,
            requisitions.Object, requisitionItems.Object, NullLogger<InventoryReplenishmentService>.Instance);

        var generated = await service.GenerateAsync(new GenerateInventoryReplenishmentRequest
        {
            WarehouseId = warehouseId, DemandWindowDays = 90, IdempotencyKey = "generate-lifecycle",
            CorrelationId = "tdc0612-lifecycle"
        });
        generated.Should().ContainSingle();
        generated[0].Status.Should().Be(InventoryReplenishmentRecommendationStatus.Draft);
        generated[0].RecommendedQuantity.Should().BeGreaterThan(0);
        generated[0].Explanation.Should().Contain("outbound demand").And.Contain("open replenishment PR");
        notifications.Verify(value => value.CreateNotificationAsync(
            It.Is<CreateNotificationDto>(dto => dto.Type == "InventoryReplenishmentRequired" && dto.EntityId == generated[0].Id),
            generatorId, tenantId), Times.Once);

        var recommendation = await context.InventoryReplenishmentRecommendations.SingleAsync();
        recommendation.RowVersion = new byte[] { 1 };
        await context.SaveChangesAsync();
        var current = await service.GetByIdAsync(recommendation.Id);

        var staleSubmit = async () => await service.SubmitAsync(recommendation.Id, new SubmitInventoryReplenishmentRequest
        {
            Reason = "Stale submit", IdempotencyKey = "stale-submit", CorrelationId = "tdc0612-stale",
            RowVersion = Convert.ToBase64String(new byte[] { 2 })
        });
        (await staleSubmit.Should().ThrowAsync<InventoryReplenishmentControlException>())
            .Which.Code.Should().Be("INV_REPLENISHMENT_CONCURRENCY_CONFLICT");

        var submitted = await service.SubmitAsync(recommendation.Id, new SubmitInventoryReplenishmentRequest
        {
            Reason = "Calculated demand requires replenishment", IdempotencyKey = "submit-lifecycle",
            CorrelationId = "tdc0612-submit", RowVersion = current.RowVersion
        });
        submitted.Status.Should().Be(InventoryReplenishmentRecommendationStatus.PendingApproval);
        submitted.WorkflowInstanceId.Should().Be(workflowInstanceId);

        var selfApproval = async () => await service.DecideAsync(recommendation.Id, new DecideInventoryReplenishmentRequest
        {
            Approved = true, Comment = "Self approval must fail", IdempotencyKey = "self-approval",
            CorrelationId = "tdc0612-sod", RowVersion = submitted.RowVersion
        });
        (await selfApproval.Should().ThrowAsync<InventoryReplenishmentControlException>())
            .Which.Code.Should().Be("INV_REPLENISHMENT_SOD_VIOLATION");

        actorId = approverId;
        var approved = await service.DecideAsync(recommendation.Id, new DecideInventoryReplenishmentRequest
        {
            Approved = true, Comment = "Independent replenishment approval", IdempotencyKey = "approve-lifecycle",
            CorrelationId = "tdc0612-approve", RowVersion = submitted.RowVersion
        });
        approved.Status.Should().Be(InventoryReplenishmentRecommendationStatus.Approved);

        var conversionRequest = new ConvertInventoryReplenishmentRequest
        {
            Department = "Stores", CostCenter = "CC-STORE", Justification = "Maintain approved service level",
            IdempotencyKey = "convert-lifecycle", CorrelationId = "tdc0612-convert", RowVersion = approved.RowVersion
        };
        var converted = await service.ConvertToPurchaseRequisitionAsync(recommendation.Id, conversionRequest);
        converted.Status.Should().Be(InventoryReplenishmentRecommendationStatus.ConvertedToRequisition);
        converted.PurchaseRequisitionNumber.Should().Be("PR-REP-001");
        createdRequisition.Should().NotBeNull();
        createdRequisition!.Status.Should().Be("Draft");
        createdRequisition.RequisitionType.Should().Be(PurchaseRequisitionType.StockReplenishment);
        createdRequisition.IsAutoGenerated.Should().BeTrue();
        createdRequisition.GeneratedFrom.Should().Be($"InventoryReplenishmentRecommendation:{recommendation.Id:N}");
        createdItem.Should().NotBeNull();
        createdItem!.InventoryItemId.Should().Be(itemId);
        createdItem.Quantity.Should().BeGreaterThan(0);
        linkage.Verify(value => value.PrepareAsync(It.Is<PurchaseRequisition>(request => request.Status == "Draft"),
            It.Is<SavePurchaseRequisitionLinkageRequest>(request => request.RequisitionType == PurchaseRequisitionType.StockReplenishment),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        conversionRequest.RowVersion = converted.RowVersion;
        var replay = await service.ConvertToPurchaseRequisitionAsync(recommendation.Id, conversionRequest);
        replay.PurchaseRequisitionId.Should().Be(converted.PurchaseRequisitionId);
        requisitions.Verify(value => value.CreateRequisitionAsync(It.IsAny<PurchaseRequisition>()), Times.Once);
        (await context.InventoryReplenishmentActions.CountAsync()).Should().Be(5);
        (await context.AuditLogs.CountAsync(value => value.Resource == "InventoryReplenishmentRecommendation"))
            .Should().Be(4);

        actorTenantId = Guid.NewGuid();
        var crossTenantRead = async () => await service.GetByIdAsync(recommendation.Id);
        await crossTenantRead.Should().ThrowAsync<InventoryReplenishmentNotFoundException>();
    }

    [Fact]
    public void Current_relational_model_matches_the_compiled_snapshot()
    {
        using var sqlServerContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=(local);Database=Tdc0612ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
                .Options);
        var snapshot = sqlServerContext.GetService<IMigrationsAssembly>().ModelSnapshot;
        if (snapshot is null) return;
        var differ = sqlServerContext.GetService<IMigrationsModelDiffer>();
        var initializer = sqlServerContext.GetService<IModelRuntimeInitializer>();
        var snapshotModel = initializer.Initialize(snapshot.Model, designTime: true);
        var current = sqlServerContext.GetService<IDesignTimeModel>().Model;
        differ.GetDifferences(snapshotModel.GetRelationalModel(), current.GetRelationalModel()).Should().BeEmpty();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    public void Dispose() => _context.Dispose();
}
