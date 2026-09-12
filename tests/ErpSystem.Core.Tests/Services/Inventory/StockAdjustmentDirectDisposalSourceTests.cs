using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

/// <summary>
/// The persisted disposal owns its generated adjustment's approval decision. These
/// tests exercise that narrow delegation; they do not replace SQL rollback testing.
/// </summary>
public sealed class StockAdjustmentDirectDisposalSourceTests
{
    [Theory]
    [InlineData(InventoryDisposalStatus.ReadyForExecution)]
    [InlineData(InventoryDisposalStatus.AdjustmentPending)]
    public async Task Exact_persisted_no_workflow_source_allows_missing_optional_evidence(InventoryDisposalStatus status)
    {
        await using var fixture = new Fixture();
        fixture.Source.Status = status;
        await fixture.SeedAsync();

        (await fixture.ResolveAsync()).Should().NotBeNull();
        await fixture.RevalidateEvidenceAsync()();

        fixture.Workflow.Verify(value => value.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        (await fixture.Context.Set<StockMovement>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Initial_unlinked_draft_is_allowed_only_at_creation_not_submission_or_evidence_revalidation()
    {
        await using var fixture = new Fixture();
        fixture.Source.StockAdjustmentId = null;
        await fixture.SeedAsync();

        (await fixture.ResolveAsync(allowUnlinkedDraft: true)).Should().NotBeNull();
        (await fixture.ResolveAsync()).Should().BeNull();
        await fixture.RevalidateEvidenceAsync().Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Current published central-DMS evidence is required for this adjustment reason.");
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("linked-adjustment")]
    [InlineData("deleted")]
    [InlineData("approval-required")]
    [InlineData("workflow-id")]
    [InlineData("approver")]
    [InlineData("approval-date")]
    [InlineData("draft")]
    [InlineData("completed")]
    [InlineData("cancelled")]
    public async Task Ineligible_persisted_source_cannot_relax_evidence(string invalid)
    {
        await using var fixture = new Fixture();
        switch (invalid)
        {
            case "tenant": fixture.Source.TenantId = Guid.NewGuid(); break;
            case "linked-adjustment": fixture.Source.StockAdjustmentId = Guid.NewGuid(); break;
            case "deleted": fixture.Source.IsDeleted = true; break;
            case "approval-required": fixture.Source.ApprovalRequired = true; break;
            case "workflow-id": fixture.Source.WorkflowInstanceId = Guid.NewGuid(); break;
            case "approver": fixture.Source.ApprovedById = Guid.NewGuid(); break;
            case "approval-date": fixture.Source.ApprovedAtUtc = DateTime.UtcNow; break;
            case "draft": fixture.Source.Status = InventoryDisposalStatus.Identified; break;
            case "completed": fixture.Source.Status = InventoryDisposalStatus.Completed; break;
            case "cancelled": fixture.Source.Status = InventoryDisposalStatus.Cancelled; break;
        }
        await fixture.SeedAsync();

        (await fixture.ResolveAsync()).Should().BeNull();
        await fixture.RevalidateEvidenceAsync().Should().ThrowAsync<InvalidOperationException>().WithMessage("*evidence is required*");
    }

    [Theory]
    [InlineData("warehouse")]
    [InlineData("reference")]
    [InlineData("reason")]
    [InlineData("item")]
    [InlineData("bin")]
    [InlineData("quantity")]
    [InlineData("positive-direction")]
    [InlineData("lot")]
    [InlineData("batch")]
    [InlineData("serial")]
    [InlineData("line-count")]
    public async Task Forged_payload_cannot_reuse_a_real_disposal_identity(string invalid)
    {
        await using var fixture = new Fixture();
        var line = fixture.Adjustment.Items.Single();
        switch (invalid)
        {
            case "warehouse": fixture.Adjustment.WarehouseId = Guid.NewGuid(); break;
            case "reference": fixture.Adjustment.Reference = "DISP-OTHER"; break;
            case "reason": fixture.Adjustment.ReasonCode = StockAdjustmentReasonCodes.Damage; break;
            case "item": line.InventoryItemId = Guid.NewGuid(); break;
            case "bin": line.LocationId = Guid.NewGuid(); break;
            case "quantity": line.AdjustmentQuantity = -3; break;
            case "positive-direction": line.AdjustmentQuantity = 2; break;
            case "lot": line.LotNumber = "OTHER"; break;
            case "batch": line.BatchNumber = "OTHER"; break;
            case "serial": line.SerialNumber = "OTHER"; break;
            case "line-count": fixture.Adjustment.Items.Clear(); break;
        }
        await fixture.SeedAsync(persistAdjustment: false);

        await fixture.ResolveAction().Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The generated stock adjustment does not match its disposal source.");
    }

    [Theory]
    [InlineData("standalone-key")]
    [InlineData("disposal:not-a-guid:adjustment")]
    [InlineData("disposal:00000000000000000000000000000001:adjustment")]
    [InlineData("disposal:00000000000000000000000000000001:adjustment:extra")]
    public async Task A_typed_reference_or_idempotency_prefix_is_not_a_disposal_authorization(string key)
    {
        await using var fixture = new Fixture();
        fixture.Adjustment.IdempotencyKey = key;
        await fixture.SeedAsync();

        (await fixture.ResolveAsync()).Should().BeNull();
        await fixture.RevalidateEvidenceAsync().Should().ThrowAsync<InvalidOperationException>().WithMessage("*evidence is required*");
    }

    [Theory]
    [InlineData("StockAdjustment")]
    [InlineData("InventoryDisposal")]
    public async Task Retained_approval_instances_cannot_be_bypassed_by_an_inactive_source_flag(string entityType)
    {
        await using var fixture = new Fixture();
        fixture.Workflow.Setup(value => value.HasActiveApprovalInstanceAsync(entityType, It.IsAny<Guid>())).ReturnsAsync(true);
        await fixture.SeedAsync();

        await fixture.ResolveAction().Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A retained approval instance must be resolved before disposal posting.");
    }

    [Fact]
    public async Task Donation_reason_and_case_insensitive_tracking_keep_exact_source_lineage()
    {
        await using var fixture = new Fixture();
        fixture.Source.Method = InventoryDisposalMethod.Donation;
        fixture.Adjustment.ReasonCode = StockAdjustmentReasonCodes.Donation;
        fixture.Source.Lines.Single().LotNumber = "LOT-ONE";
        fixture.Adjustment.Items.Single().LotNumber = "lot-one";
        await fixture.SeedAsync();

        (await fixture.ResolveAsync()).Should().NotBeNull();
        await fixture.RevalidateEvidenceAsync()();
    }

    [Fact]
    public async Task Duplicate_adjustment_lines_cannot_replace_a_different_source_line()
    {
        await using var fixture = new Fixture();
        fixture.Source.Lines.Add(new InventoryDisposalLine
        {
            TenantId = fixture.TenantId, InventoryDisposalCaseId = fixture.Source.Id,
            InventoryItemId = Guid.NewGuid(), LocationId = Guid.NewGuid(), Quantity = 1, UnitCost = 5, TotalValue = 5
        });
        var original = fixture.Adjustment.Items.Single();
        fixture.Adjustment.Items.Add(new StockAdjustmentItem
        {
            TenantId = fixture.TenantId, AdjustmentId = fixture.Adjustment.Id,
            InventoryItemId = original.InventoryItemId, LocationId = original.LocationId,
            AdjustmentQuantity = original.AdjustmentQuantity, UnitCost = original.UnitCost, AdjustmentValue = original.AdjustmentValue
        });
        await fixture.SeedAsync(persistAdjustment: false);

        await fixture.ResolveAction().Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The generated stock adjustment does not match its disposal source.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Creation_skips_evidence_only_for_a_valid_ready_unlinked_disposal(bool fakeSource)
    {
        await using var fixture = new Fixture();
        fixture.Source.StockAdjustmentId = null;
        fixture.Source.Status = InventoryDisposalStatus.ReadyForExecution;
        if (fakeSource) fixture.Adjustment.IdempotencyKey = $"disposal:{Guid.NewGuid():N}:adjustment";
        await fixture.SeedAsync(persistAdjustment: false);

        if (fakeSource)
        {
            Func<Task> create = async () => { await fixture.CreateAsync(); };
            await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*evidence is required*");
            (await fixture.Context.Set<StockAdjustment>().CountAsync()).Should().Be(0);
        }
        else
        {
            var result = await fixture.CreateAsync();
            result.Status.Should().Be("Draft");
            result.Evidence.Should().BeEmpty();
            (await fixture.Context.Set<StockAdjustment>().CountAsync()).Should().Be(1);
            (await fixture.Context.Set<InventoryItem>().CountAsync()).Should().Be(1);
            (await fixture.Context.Set<WarehouseLocation>().CountAsync()).Should().Be(1);
            fixture.Source.StockAdjustmentId.Should().BeNull("only the disposal owner links the newly created adjustment");
        }
        fixture.Workflow.Verify(value => value.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        (await fixture.Context.Set<StockMovement>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Submission_inherits_persisted_source_decision_without_recording_a_human_approval()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();

        var result = await fixture.SubmitAsync();

        result.Status.Should().Be(InventoryOptionalApprovalPolicy.ReadyToPost);
        result.ApprovalRequired.Should().BeFalse();
        fixture.Adjustment.ApprovedById.Should().BeNull();
        fixture.Adjustment.ApprovedAt.Should().BeNull();
        fixture.Adjustment.WorkflowInstanceId.Should().BeNull();
        fixture.Adjustment.SubmittedById.Should().Be(fixture.ActorId);
        fixture.Workflow.Verify(value => value.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        var actions = await fixture.Context.Set<StockAdjustmentAction>().OrderBy(value => value.Sequence).ToListAsync();
        actions.Select(value => value.ActionType).Should().Equal("Submitted", "ApprovalNotRequired");
        actions.Last().Comment.Should().Contain(fixture.Source.DisposalNumber).And.Contain("no second workflow or human approval");
        (await fixture.Context.Set<StockMovement>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Standalone_adjustment_still_uses_its_own_workflow()
    {
        await using var fixture = new Fixture();
        fixture.Adjustment.IdempotencyKey = "standalone-adjustment";
        fixture.Adjustment.ReasonCode = StockAdjustmentReasonCodes.PhysicalCount;
        await fixture.SeedAsync();

        var result = await fixture.SubmitAsync();

        result.Status.Should().Be("PendingApproval");
        result.ApprovalRequired.Should().BeTrue();
        fixture.Workflow.Verify(value => value.SubmitAsync("StockAdjustment", fixture.Adjustment.Id), Times.Once);
    }

    [Fact]
    public async Task Attached_evidence_is_still_revalidated_for_a_direct_disposal()
    {
        await using var fixture = new Fixture();
        fixture.Adjustment.Evidence.Add(new StockAdjustmentEvidence
        {
            TenantId = fixture.TenantId, StockAdjustmentId = fixture.Adjustment.Id,
            CentralDocumentVersionId = Guid.NewGuid(), FileUploadRecordId = Guid.NewGuid(), EvidenceReference = "Stale file"
        });
        await fixture.SeedAsync(persistAdjustment: false);

        await fixture.RevalidateEvidenceAsync().Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Linked central-DMS evidence is no longer current and published.");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public UnitOfWork Unit { get; }
        public StockAdjustment Adjustment { get; }
        public InventoryDisposalCase Source { get; }
        public Mock<IWorkflowIntegrationService> Workflow { get; } = new();
        private StockAdjustmentService Service { get; }
        private Warehouse Warehouse { get; }
        private WarehouseLocation Bin { get; }
        private InventoryItem Item { get; }

        public Fixture()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(options => options.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
            Unit = new UnitOfWork(Context);
            Warehouse = new Warehouse { TenantId = TenantId, Code = "DISP", Name = "Disposal warehouse", IsActive = true };
            Bin = new WarehouseLocation { TenantId = TenantId, WarehouseId = Warehouse.Id, LocationCode = "LOC-001", IsActive = true };
            Item = new InventoryItem { TenantId = TenantId, ItemCode = "SKU-001", Name = "Disposal item", ItemType = ItemType.StockItem, Status = ItemStatus.Active, AverageCost = 10 };
            Adjustment = new StockAdjustment
            {
                TenantId = TenantId, WarehouseId = Warehouse.Id, Warehouse = Warehouse, AdjustmentNumber = "ADJ-DISPOSAL",
                ReasonCode = StockAdjustmentReasonCodes.WriteOff, Reference = "DISP-001", Description = "Damaged stock",
                RequestedById = ActorId, Status = "Draft", RowVersion = [1, 2, 3], TotalAdjustmentValue = -20
            };
            Adjustment.Items.Add(new StockAdjustmentItem
            {
                TenantId = TenantId, AdjustmentId = Adjustment.Id, InventoryItemId = Item.Id, LocationId = Bin.Id,
                AdjustmentQuantity = -2, SystemQuantity = 10, PhysicalQuantity = 8, UnitCost = 10, AdjustmentValue = -20
            });
            Source = new InventoryDisposalCase
            {
                TenantId = TenantId, WarehouseId = Warehouse.Id, DisposalNumber = "DISP-001", Method = InventoryDisposalMethod.WriteOff,
                StockAdjustmentId = Adjustment.Id, Status = InventoryDisposalStatus.AdjustmentPending, ApprovalRequired = false,
                RequestedById = ActorId, RequestedAtUtc = DateTime.UtcNow, Reason = "Damaged stock", TotalQuantity = 2, TotalValue = 20
            };
            Source.Lines.Add(new InventoryDisposalLine
            {
                TenantId = TenantId, InventoryDisposalCaseId = Source.Id, InventoryItemId = Item.Id, LocationId = Bin.Id,
                Quantity = 2, UnitCost = 10, TotalValue = 20
            });
            Adjustment.IdempotencyKey = $"disposal:{Source.Id:N}:adjustment";
            var actor = new Mock<ICurrentUserProvider>();
            actor.SetupGet(value => value.TenantId).Returns(TenantId);
            actor.SetupGet(value => value.UserId).Returns(ActorId);
            actor.SetupGet(value => value.Username).Returns("disposal.requester");
            actor.SetupGet(value => value.IsAuthenticated).Returns(true);
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(value => value.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
            access.Setup(value => value.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
            Workflow.Setup(value => value.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(
                new WorkflowIntegrationResult(new WorkflowExecutionResult { Success = true, WorkflowInstanceId = Guid.NewGuid() }, WorkflowOutcome.Pending));
            var adjustments = new Mock<IStockAdjustmentRepository>();
            adjustments.Setup(value => value.GenerateAdjustmentNumberAsync(TenantId)).ReturnsAsync("ADJ-CREATED");
            adjustments.Setup(value => value.AddAsync(It.IsAny<StockAdjustment>())).ReturnsAsync((StockAdjustment adjustment) =>
            { Context.Add(adjustment); return adjustment; });
            var items = new Mock<IInventoryItemRepository>();
            // The production repository reloads tracked entities after a retry clears
            // the context. Returning the originally seeded (now detached) graph here
            // would incorrectly mark existing item/bin records Added with the new line.
            items.Setup(value => value.GetByIdAsync(Item.Id))
                .Returns(() => Context.Set<InventoryItem>().SingleOrDefaultAsync(value => value.Id == Item.Id));
            var bins = new Mock<IWarehouseLocationRepository>();
            bins.Setup(value => value.GetByIdAsync(Bin.Id))
                .Returns(() => Context.Set<WarehouseLocation>().SingleOrDefaultAsync(value => value.Id == Bin.Id));
            var warehouses = new Mock<IWarehouseRepository>();
            warehouses.Setup(value => value.GetByIdAsync(Warehouse.Id))
                .Returns(() => Context.Set<Warehouse>().SingleOrDefaultAsync(value => value.Id == Warehouse.Id));
            Service = new StockAdjustmentService(adjustments.Object, items.Object,
                Mock.Of<IStockMovementRepository>(), Mock.Of<IWarehouseQuantityRepository>(), bins.Object,
                warehouses.Object, Mock.Of<IConsignmentSettlementService>(), actor.Object, Unit,
                Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(), access.Object,
                Mock.Of<IProcurementSodGuardService>(), Workflow.Object, Mock.Of<IProcurementControlEventService>(),
                Mock.Of<IInventoryAdjustmentFinancePostingService>(), Mock.Of<IInventoryValuationService>(), NullLogger<StockAdjustmentService>.Instance);
        }

        public async Task SeedAsync(bool persistAdjustment = true)
        {
            Context.AddRange(Warehouse, Bin, Item, Source);
            if (persistAdjustment) Context.Add(Adjustment);
            await Context.SaveChangesAsync();
        }

        public Task<InventoryDisposalCase?> ResolveAsync(bool allowUnlinkedDraft = false) =>
            (Task<InventoryDisposalCase?>)typeof(StockAdjustmentService).GetMethod("GetDirectDisposalSourceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Service, [Adjustment, allowUnlinkedDraft])!;

        public Func<Task> ResolveAction() => async () => { await ResolveAsync(); };

        public Func<Task> RevalidateEvidenceAsync() => () => (Task)typeof(StockAdjustmentService)
            .GetMethod("RevalidateEvidenceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Service, [Adjustment])!;

        public Task<StockAdjustmentDetailDto> SubmitAsync() => Service.SubmitAsync(Adjustment.Id, ActorId,
            new StockAdjustmentActionRequest { IdempotencyKey = "submit-disposal", RowVersion = Convert.ToBase64String(Adjustment.RowVersion) });

        public Task<StockAdjustmentDetailDto> CreateAsync() => Service.CreateAsync(new CreateStockAdjustmentDto
        {
            WarehouseId = Warehouse.Id, ReasonCode = Adjustment.ReasonCode, Reference = Adjustment.Reference,
            Description = Adjustment.Description, IdempotencyKey = Adjustment.IdempotencyKey,
            Items = Adjustment.Items.Select(line => new CreateStockAdjustmentItemDto
            { InventoryItemId = line.InventoryItemId, LocationId = line.LocationId, AdjustmentQuantity = line.AdjustmentQuantity }).ToList(),
            Evidence = []
        }, ActorId);

        public async ValueTask DisposeAsync() { Unit.Dispose(); await Context.DisposeAsync(); }
    }
}
