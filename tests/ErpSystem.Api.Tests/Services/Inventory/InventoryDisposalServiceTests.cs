using ErpSystem.Api.Services.Inventory;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Inventory;

public sealed class InventoryDisposalServiceTests
{
    [Fact, Trait("Batch", "TDC-0615")]
    public void SqlServer_disposal_completion_lock_captures_and_rejects_negative_application_lock_results()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
        var source = File.ReadAllText(Path.Combine(root,
            "src", "ErpSystem.Api", "Services", "Inventory", "InventoryDisposalService.cs"));
        var start = source.IndexOf("private async Task AcquireDisposalLockAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private sealed record DisposalFinancePlan", start, StringComparison.Ordinal);
        var lockBody = source[start..end];

        lockBody.Should().Contain("DECLARE @result int;")
            .And.Contain("EXEC @result = sp_getapplock")
            .And.Contain("IF @result < 0 THROW 51000")
            .And.Contain("INV_DISPOSAL_LOCK_FAILED");
    }

    [Fact, Trait("Batch", "TDC-0615")]
    public async Task Identify_derives_exact_location_value_links_current_clean_dms_and_replays_idempotently()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request("identify-one");
        request.Lines.Single().LotNumber = "LOT-01";
        request.Lines.Single().BatchNumber = "BATCH-01";
        request.Lines.Single().SerialNumber = "SERIAL-01";

        var first = await fixture.Service.CreateAsync(request);
        var replay = await fixture.Service.CreateAsync(request);

        replay.Id.Should().Be(first.Id);
        first.Status.Should().Be(InventoryDisposalStatus.Identified);
        first.TotalQuantity.Should().Be(4m);
        first.TotalValue.Should().Be(50m);
        first.Lines.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            UnitCost = 12.5m,
            LotNumber = "LOT-01",
            BatchNumber = "BATCH-01",
            SerialNumber = "SERIAL-01"
        });
        first.Evidence.Should().ContainSingle(value => value.Stage == "Identification");
        first.Actions.Should().ContainSingle(value => value.ActionType == InventoryDisposalActionType.Identified);
        (await fixture.Db.InventoryDisposalCases.CountAsync()).Should().Be(1);
        fixture.ControlEvents.Verify(value => value.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request => request.RuleCode == "INV-020" &&
                request.DecisionKeys.Count == 14 && request.Evidence.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.TrackingControls.Verify(value => value.ValidateAvailabilityAsync(
            fixture.Item.Id, fixture.Warehouse.Id, fixture.Location.Id, 4m,
            "LOT-01", "BATCH-01", "SERIAL-01", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Batch", "TDC-0615")]
    public async Task Reusing_an_identification_key_with_a_different_payload_is_rejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateAsync(fixture.Request("identify-conflict"));
        var changed = fixture.Request("identify-conflict");
        changed.Reason = "A materially different disposal reason";

        var action = async () => await fixture.Service.CreateAsync(changed);

        (await action.Should().ThrowAsync<InventoryDisposalException>()).Which.Code
            .Should().Be("INV_DISPOSAL_IDEMPOTENCY_CONFLICT");
        (await fixture.Db.InventoryDisposalCases.CountAsync()).Should().Be(1);
    }

    [Fact, Trait("Batch", "TDC-0615")]
    public async Task Identification_accepts_a_consignment_bin_and_values_the_effective_ownership_balance()
    {
        await using var fixture = await Fixture.CreateAsync();
        var physicalHost = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = fixture.Warehouse.TenantId,
            Code = "WH-HOST", Name = "Physical host warehouse", IsActive = true
        };
        fixture.Db.Warehouses.Add(physicalHost);
        fixture.Location.WarehouseId = physicalHost.Id;
        fixture.Location.Warehouse = physicalHost;
        fixture.Location.IsConsignmentBin = true;
        fixture.Location.ConsignmentWarehouseId = fixture.Warehouse.Id;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var identified = await fixture.Service.CreateAsync(fixture.Request("identify-consignment"));

        identified.WarehouseId.Should().Be(fixture.Warehouse.Id);
        identified.Lines.Should().ContainSingle().Which.Should().Match<InventoryDisposalLineDto>(line =>
            line.LocationId == fixture.Location.Id && line.UnitCost == 12.5m && line.TotalValue == 50m);
    }

    [Fact, Trait("Batch", "TDC-0615")]
    public async Task Identification_rejects_evidence_without_a_clean_central_scan()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Upload.VirusScanStatus = FileVirusScanStatus.Infected;
        await fixture.Db.SaveChangesAsync();

        var action = async () => await fixture.Service.CreateAsync(fixture.Request("identify-infected"));

        (await action.Should().ThrowAsync<InventoryDisposalException>()).Which.Code
            .Should().Be("INV_DISPOSAL_EVIDENCE_NOT_CLEAN");
        (await fixture.Db.InventoryDisposalCases.CountAsync()).Should().Be(0);
    }

    [Fact, Trait("Batch", "TDC-0615")]
    public async Task Identification_rejects_insufficient_exact_tracked_stock()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.TrackingControls.Setup(value => value.ValidateAvailabilityAsync(
                fixture.Item.Id, fixture.Warehouse.Id, fixture.Location.Id, 4m,
                "LOT-LOW", null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryTrackingControlException(
                "INV_TRACKING_LOT_INSUFFICIENT", "The selected lot has only two units."));
        var request = fixture.Request("identify-tracked-insufficient");
        request.Lines.Single().LotNumber = "LOT-LOW";

        var action = () => fixture.Service.CreateAsync(request);

        await action.Should().ThrowAsync<InventoryTrackingControlException>()
            .Where(value => value.Code == "INV_TRACKING_LOT_INSUFFICIENT");
        (await fixture.Db.InventoryDisposalCases.CountAsync()).Should().Be(0);
    }

    [Fact, Trait("Batch", "TDC-0615")]
    public async Task Identification_reserves_the_exact_lot_across_active_disposal_cases()
    {
        await using var fixture = await Fixture.CreateAsync();
        var firstRequest = fixture.Request("identify-exact-lot-first");
        firstRequest.Lines.Single().LotNumber = "LOT-RESERVED";
        await fixture.Service.CreateAsync(firstRequest);
        fixture.TrackingControls.Setup(value => value.ValidateAvailabilityAsync(
                fixture.Item.Id, fixture.Warehouse.Id, fixture.Location.Id, 8m,
                " lot-reserved ", null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryTrackingControlException(
                "INV_TRACKING_LOT_INSUFFICIENT", "The exact lot has only four units."));
        var secondRequest = fixture.Request("identify-exact-lot-second");
        secondRequest.Lines.Single().LotNumber = " lot-reserved ";

        var action = () => fixture.Service.CreateAsync(secondRequest);

        await action.Should().ThrowAsync<InventoryTrackingControlException>()
            .Where(value => value.Code == "INV_TRACKING_LOT_INSUFFICIENT");
        fixture.TrackingControls.Verify(value => value.ValidateAvailabilityAsync(
            fixture.Item.Id, fixture.Warehouse.Id, fixture.Location.Id, 8m,
            " lot-reserved ", null, null, It.IsAny<CancellationToken>()), Times.Once);
        (await fixture.Db.InventoryDisposalCases.CountAsync()).Should().Be(1);
    }

    [Fact, Trait("Batch", "TDC-0615")]
    public async Task Committee_schedule_requires_three_independent_active_users_with_the_disposal_role()
    {
        await using var fixture = await Fixture.CreateAsync();
        var identified = await fixture.Service.CreateAsync(fixture.Request("identify-committee"));
        fixture.Db.ChangeTracker.Clear();
        fixture.Current.UserId = fixture.AuditorId;
        var verified = await fixture.Service.VerifyAsync(identified.Id, new VerifyInventoryDisposalRequest
        {
            Verified = true, Findings = "Physical verification agrees to the identified stock.",
            RowVersion = identified.RowVersion, IdempotencyKey = "audit-committee"
        });

        var schedule = new ScheduleInventoryDisposalCommitteeRequest
        {
            MeetingAtUtc = DateTime.UtcNow.AddDays(2), CommitteeReference = "DC-2026-001",
            MemberUserIds = fixture.CommitteeMemberIds.ToList(), RowVersion = verified.RowVersion,
            IdempotencyKey = "schedule-without-role"
        };
        var action = async () => await fixture.Service.ScheduleCommitteeAsync(identified.Id, schedule);

        (await action.Should().ThrowAsync<InventoryDisposalException>()).Which.Code
            .Should().Be("INV_DISPOSAL_COMMITTEE_MEMBER_INVALID");

        var role = new ApplicationRole("TDC_DISPOSAL_COMMITTEE_MEMBER")
        {
            Id = Guid.NewGuid(), NormalizedName = "TDC_DISPOSAL_COMMITTEE_MEMBER", IsSystemRole = true
        };
        fixture.Db.Roles.Add(role);
        fixture.Db.UserRoles.AddRange(fixture.CommitteeMemberIds.Select(userId => new ApplicationUserRole
        {
            UserId = userId, RoleId = role.Id
        }));
        await fixture.Db.SaveChangesAsync();
        schedule.IdempotencyKey = "schedule-with-role";

        var result = await fixture.Service.ScheduleCommitteeAsync(identified.Id, schedule);

        result.Status.Should().Be(InventoryDisposalStatus.CommitteeScheduled);
        result.CommitteeMembers.Should().HaveCount(3);
    }

    [Fact]
    public async Task No_workflow_moves_directly_to_execution_without_fabricating_approval_or_committee()
    {
        await using var fixture = await Fixture.CreateAsync();
        var draft = await fixture.Service.CreateAsync(fixture.Request("direct-draft"));
        var result = await fixture.Service.SubmitAsync(draft.Id, new SubmitInventoryDisposalRequest
            { RowVersion = draft.RowVersion, IdempotencyKey = "direct-submit" });
        result.Status.Should().Be(InventoryDisposalStatus.ReadyForExecution);
        result.ApprovalRequired.Should().BeFalse();
        result.WorkflowInstanceId.Should().BeNull();
        result.ApprovedById.Should().BeNull();
        result.AuditVerifiedById.Should().BeNull();
        result.CommitteeMembers.Should().BeEmpty();
        result.CanStageExecution.Should().BeTrue();
        result.Actions.First().ActionType.Should().Be(InventoryDisposalActionType.ApprovalNotRequired);
    }

    [Fact]
    public async Task Active_workflow_starts_from_draft_without_a_hardcoded_committee()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Workflow.Setup(value => value.HasActiveApprovalWorkflowAsync("InventoryDisposal")).ReturnsAsync(true);
        var workflowId = Guid.NewGuid();
        fixture.Workflow.Setup(value => value.SubmitAsync("InventoryDisposal", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowIntegrationResult(new ErpSystem.Core.DTOs.Workflow.WorkflowExecutionResult
                { Success = true, WorkflowInstanceId = workflowId }, WorkflowOutcome.Pending));
        var draft = await fixture.Service.CreateAsync(fixture.Request("active-draft"));
        var result = await fixture.Service.SubmitAsync(draft.Id, new SubmitInventoryDisposalRequest
            { RowVersion = draft.RowVersion, IdempotencyKey = "active-submit" });
        result.Status.Should().Be(InventoryDisposalStatus.PendingApproval);
        result.WorkflowInstanceId.Should().Be(workflowId);
        result.CanApprove.Should().BeFalse("the requester cannot approve its own disposal");
        result.CanStageExecution.Should().BeFalse();
    }

    [Fact]
    public async Task Draft_edit_retains_old_lines_in_history_and_changes_only_active_quantities()
    {
        await using var fixture = await Fixture.CreateAsync();
        var draft = await fixture.Service.CreateAsync(fixture.Request("edit-draft"));
        var result = await fixture.Service.UpdateAsync(draft.Id, new UpdateInventoryDisposalRequest
        {
            RowVersion = draft.RowVersion, IdempotencyKey = "edit-save", Reason = "Damaged item",
            Method = InventoryDisposalMethod.Destruction,
            Lines = [new() { InventoryItemId = fixture.Item.Id, LocationId = fixture.Location.Id, Quantity = 2m }]
        });
        result.Lines.Should().ContainSingle().Which.Quantity.Should().Be(2m);
        result.TotalQuantity.Should().Be(2m);
        (await fixture.Db.InventoryDisposalLines.IgnoreQueryFilters().CountAsync(value => value.IsDeleted)).Should().Be(1);
        result.Actions.First().ActionType.Should().Be(InventoryDisposalActionType.Edited);
        var blocked = () => fixture.Service.UpdateAsync(draft.Id, new UpdateInventoryDisposalRequest
        {
            RowVersion = result.RowVersion, IdempotencyKey = "bad-edit", Method = InventoryDisposalMethod.WriteOff,
            Reason = "Invalid", Lines = [new() { InventoryItemId = fixture.Item.Id, LocationId = fixture.Location.Id, Quantity = 20m }]
        });
        await blocked.Should().ThrowAsync<InventoryDisposalException>();
    }

    [Fact]
    public async Task Cancellation_retains_case_and_releases_reserved_disposal_quantity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var draft = await fixture.Service.CreateAsync(fixture.Request("cancel-draft"));
        var cancelled = await fixture.Service.CancelAsync(draft.Id, new CancelInventoryDisposalRequest
            { RowVersion = draft.RowVersion, IdempotencyKey = "cancel", Comment = "Wrong selection" });
        cancelled.Status.Should().Be(InventoryDisposalStatus.Cancelled);
        cancelled.CanSubmit.Should().BeFalse();
        var replacement = fixture.Request("replacement");
        replacement.Lines.Single().Quantity = 10m;
        (await fixture.Service.CreateAsync(replacement)).TotalQuantity.Should().Be(10m);
        (await fixture.Db.InventoryDisposalCases.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Direct_disposal_does_not_require_a_file()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request("draft-no-file");
        request.Evidence.Clear();
        request.IdentificationDetails = "";
        var draft = await fixture.Service.CreateAsync(request);
        var result = await fixture.Service.SubmitAsync(draft.Id, new SubmitInventoryDisposalRequest
            { RowVersion = draft.RowVersion, IdempotencyKey = "no-file-submit" });
        result.Status.Should().Be(InventoryDisposalStatus.ReadyForExecution);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Direct_disposal_stages_once_and_never_auto_approves_the_finance_intent(bool postImmediately)
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request("direct-post");
        request.Evidence.Clear();
        var draft = await fixture.Service.CreateAsync(request);
        var ready = await fixture.Service.SubmitAsync(draft.Id, new SubmitInventoryDisposalRequest
            { RowVersion = draft.RowVersion, IdempotencyKey = "continue" });
        var stageRequest = new StageInventoryDisposalExecutionRequest
            { RowVersion = ready.RowVersion, IdempotencyKey = "prepare", ExecutionReference = ready.DisposalNumber, PostImmediately = postImmediately };
        var staged = await fixture.Service.StageExecutionAsync(ready.Id, stageRequest);
        staged.Status.Should().Be(InventoryDisposalStatus.AdjustmentPending);
        staged.ApprovedById.Should().BeNull();
        (await fixture.Service.StageExecutionAsync(staged.Id, stageRequest)).Should().BeEquivalentTo(staged);
        fixture.ProducerIntents.Verify(value => value.PrepareAsync(
            It.IsAny<ProducerAccountingIntentDto>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.ProducerIntents.Verify(value => value.ApprovePreparedAsync(
            It.IsAny<Guid>(), It.IsAny<DecideProducerAccountingIntentDto>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.ProducerIntents.Verify(value => value.GetAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, InventoryDisposalService service, MutableCurrentUser current,
            Mock<IProcurementControlEventService> controlEvents,
            Mock<IInventoryTrackingControlService> trackingControls,
            Warehouse warehouse, WarehouseLocation location,
            InventoryItem item, CentralDocumentVersion version, FileUploadRecord upload, Guid auditorId,
            IReadOnlyList<Guid> committeeMemberIds, Mock<IWorkflowIntegrationService> workflow)
        {
            Db = db;
            Service = service;
            Current = current;
            ControlEvents = controlEvents;
            TrackingControls = trackingControls;
            Warehouse = warehouse;
            Location = location;
            Item = item;
            Version = version;
            Upload = upload;
            AuditorId = auditorId;
            CommitteeMemberIds = committeeMemberIds;
            Workflow = workflow;
        }

        public ApplicationDbContext Db { get; }
        public InventoryDisposalService Service { get; }
        public MutableCurrentUser Current { get; }
        public Mock<IProcurementControlEventService> ControlEvents { get; }
        public Mock<IInventoryTrackingControlService> TrackingControls { get; }
        public Warehouse Warehouse { get; }
        public WarehouseLocation Location { get; }
        public InventoryItem Item { get; }
        public CentralDocumentVersion Version { get; }
        public FileUploadRecord Upload { get; }
        public Guid AuditorId { get; }
        public IReadOnlyList<Guid> CommitteeMemberIds { get; }
        public Mock<IWorkflowIntegrationService> Workflow { get; }
        public Mock<IStockAdjustmentService> Adjustments { get; init; } = new();
        public Mock<IFinanceProducerIntentService> ProducerIntents { get; init; } = new();

        public CreateInventoryDisposalRequest Request(string key) => new()
        {
            WarehouseId = Warehouse.Id,
            Method = InventoryDisposalMethod.WriteOff,
            Reason = "Obsolete stores confirmed for controlled write-off",
            IdentificationDetails = "Exact bin count and condition recorded by stores.",
            IdempotencyKey = key,
            CorrelationId = $"tdc0615-{key}",
            Lines =
            [
                new CreateInventoryDisposalLineRequest
                {
                    InventoryItemId = Item.Id, LocationId = Location.Id, Quantity = 4m,
                    ConditionNotes = "Obsolete and no longer serviceable"
                }
            ],
            Evidence =
            [
                new InventoryControlEvidenceRequest
                {
                    CentralDocumentVersionId = Version.Id,
                    EvidenceReference = "Stores condition report"
                }
            ]
        };

        public static async Task<Fixture> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var auditorId = Guid.NewGuid();
            var memberIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tdc0615-disposal-{Guid.NewGuid():N}")
                .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var db = new ApplicationDbContext(options);
            var tenant = new Tenant { Id = tenantId, Name = "TDC 0615 Tenant", Code = $"T{tenantId:N}"[..20] };
            var requester = User(requesterId, tenantId, tenant, "Request", "Owner");
            var auditor = User(auditorId, tenantId, tenant, "Independent", "Auditor");
            var committeeUsers = memberIds.Select((id, index) => User(id, tenantId, tenant, "Committee", $"Member {index + 1}")).ToList();
            var category = new InventoryCategory
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "OBS", Name = "Obsolete"
            };
            var warehouse = new Warehouse
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "WH-DSP", Name = "Disposal Warehouse", IsActive = true
            };
            var location = new WarehouseLocation
            {
                Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id,
                LocationCode = "DSP-01", Name = "Disposal Bin", IsActive = true, Warehouse = warehouse
            };
            var item = new InventoryItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = category.Id, Category = category,
                ItemCode = "OBS-001", Name = "Obsolete Component", UnitOfMeasure = "EA",
                Status = ItemStatus.Active, AverageCost = 10m, StandardCost = 9m
            };
            var balance = new InventoryBalance
            {
                Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = item.Id,
                WarehouseId = warehouse.Id, LocationId = location.Id, QuantityOnHand = 10m,
                QuantityAvailable = 10m, AverageUnitCost = 12.5m, TotalValue = 125m,
                InventoryItem = item, Warehouse = warehouse, Location = location
            };
            var upload = new FileUploadRecord
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Category = "inventory-disposal-evidence",
                FilePath = "dms/inventory/disposal-report.pdf", StoredFileName = "stored.pdf",
                OriginalFileName = "disposal-report.pdf", ContentType = "application/pdf", FileSize = 128,
                StorageProvider = "Test", UploadedByUserId = requesterId,
                VirusScanStatus = FileVirusScanStatus.Clean, ScannedAtUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, CreatedById = requesterId
            };
            var record = new CentralDocumentRecord
            {
                Id = Guid.NewGuid(), TenantId = tenantId, DocumentReference = "DMS-INV-DSP-001",
                Title = "Disposal condition report", SourceModule = "Inventory",
                SourceLabel = "Inventory disposal evidence", SourceEntityType = "InventoryDisposal",
                SourceRecordId = Guid.NewGuid(), RepositoryStatus = "Linked", RepositoryPath = upload.FilePath,
                LifecycleStatus = "Active", CurrentVersion = "v1.0", VersionStatus = "Published"
            };
            var version = new CentralDocumentVersion
            {
                Id = Guid.NewGuid(), TenantId = tenantId, DocumentRecordId = record.Id, DocumentRecord = record,
                VersionNumber = "v1.0", Status = "Published", RepositoryPath = upload.FilePath,
                FileName = upload.OriginalFileName, ContentType = upload.ContentType, FileSize = upload.FileSize,
                FileUploadRecordId = upload.Id, PublishedAt = DateTime.UtcNow
            };
            db.Tenants.Add(tenant);
            db.Users.AddRange(new[] { requester, auditor }.Concat(committeeUsers));
            db.InventoryCategories.Add(category);
            db.Warehouses.Add(warehouse);
            db.WarehouseLocations.Add(location);
            db.InventoryItems.Add(item);
            db.InventoryBalances.Add(balance);
            db.FileUploadRecords.Add(upload);
            db.CentralDocumentRecords.Add(record);
            db.CentralDocumentVersions.Add(version);
            await db.SaveChangesAsync();

            var current = new MutableCurrentUser(requesterId, tenantId);
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(value => value.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                    new ProcurementAccessCapabilityDecisionDto
                    {
                        Allowed = true, PermissionCode = request.PermissionCode, TenantId = tenantId,
                        ActorUserId = current.UserId, WarehouseId = request.WarehouseId,
                        LocationId = request.LocationId, CorrelationId = correlation
                    });
            var events = new Mock<IProcurementControlEventService>();
            events.Setup(value => value.RecordAsync(It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            var trackingControls = new Mock<IInventoryTrackingControlService>();
            trackingControls.Setup(value => value.ValidateAvailabilityAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var workflow = new Mock<IWorkflowIntegrationService>();
            workflow.Setup(value => value.SubmitAsync("InventoryDisposal", It.IsAny<Guid>()))
                .ReturnsAsync(new WorkflowIntegrationResult(new ErpSystem.Core.DTOs.Workflow.WorkflowExecutionResult
                    { Success = true }, WorkflowOutcome.Approved, approvalRequired: false));
            var adjustments = new Mock<IStockAdjustmentService>();
            InventoryDisposalStockAdjustmentRequest? disposalRequest = null;
            var disposalParticipant = new Mock<IInventoryDisposalStockAdjustmentParticipant>();
            disposalParticipant.Setup(value => value.PreviewAsync(
                    It.IsAny<InventoryDisposalStockAdjustmentRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((InventoryDisposalStockAdjustmentRequest request, CancellationToken _) =>
                {
                    disposalRequest = request;
                    return new StockAdjustment
                    {
                        Id = request.AdjustmentId,
                        TenantId = request.TenantId,
                        AdjustmentNumber = request.AdjustmentNumber,
                        WarehouseId = request.Create.WarehouseId,
                        AdjustmentDate = request.PostingDateUtc,
                        ReasonCode = request.Create.ReasonCode,
                        Status = "Draft"
                    };
                });
            var valuation = new Mock<IStockAdjustmentValuationIntentBuilder>();
            valuation.Setup(value => value.BuildAsync(
                    It.IsAny<StockAdjustment>(), It.IsAny<ProducerOwnerEffectIdentityDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockAdjustment _, ProducerOwnerEffectIdentityDto owner, CancellationToken _) =>
                    new ProducerAccountingIntentDto
                    {
                        AccountingEventId = disposalRequest!.FinanceApprovalId,
                        ParticipantIdentity = owner.ParticipantCode,
                        ExpectedOwnerEffect = owner
                    });
            var producerIntents = new Mock<IFinanceProducerIntentService>();
            producerIntents.Setup(value => value.PrepareAsync(
                    It.IsAny<ProducerAccountingIntentDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProducerAccountingIntentDto intent, CancellationToken _) =>
                    new AccountingEventDto { Id = intent.AccountingEventId!.Value, Status = "Prepared" });
            var service = new InventoryDisposalService(db, current, access.Object,
                Mock.Of<IProcurementSodGuardService>(), workflow.Object,
                disposalParticipant.Object, valuation.Object,
                producerIntents.Object, Mock.Of<IFinanceProducerIntentGroupService>(),
                Mock.Of<IFinanceProducerApprovedExecution>(), Mock.Of<IFinanceProducerIntentGroupApprovedExecution>(),
                trackingControls.Object, events.Object);
            return new Fixture(db, service, current, events, trackingControls, warehouse, location, item, version, upload,
                auditorId, memberIds, workflow) { Adjustments = adjustments, ProducerIntents = producerIntents };
        }

        private static ApplicationUser User(Guid id, Guid tenantId, Tenant tenant, string first, string last) => new()
        {
            Id = id, TenantId = tenantId, Tenant = tenant, FirstName = first, LastName = last,
            UserName = $"{first}.{last}.{id:N}", NormalizedUserName = $"{first}.{last}.{id:N}".ToUpperInvariant(),
            Email = $"{id:N}@example.test", NormalizedEmail = $"{id:N}@EXAMPLE.TEST", IsActive = true
        };

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    public sealed class MutableCurrentUser(Guid userId, Guid tenantId) : ICurrentUserProvider
    {
        public Guid UserId { get; set; } = userId;
        public Guid TenantId { get; } = tenantId;
        public string Username => $"tdc0615.{UserId:N}";
        public string FullName => "TDC 0615 Test Actor";
        public bool IsAuthenticated => true;
        public IEnumerable<string> Roles => ["TDC Stores Manager"];
        public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string> Claims => new Dictionary<string, string>();
        public bool IsExternalUser => false;
        public string AuthenticationProvider => "Test";
    }
}
