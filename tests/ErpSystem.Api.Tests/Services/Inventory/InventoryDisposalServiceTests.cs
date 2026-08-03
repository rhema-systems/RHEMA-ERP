using ErpSystem.Api.Services.Inventory;
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
    public async Task Identify_derives_exact_location_value_links_current_clean_dms_and_replays_idempotently()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request("identify-one");

        var first = await fixture.Service.CreateAsync(request);
        var replay = await fixture.Service.CreateAsync(request);

        replay.Id.Should().Be(first.Id);
        first.Status.Should().Be(InventoryDisposalStatus.Identified);
        first.TotalQuantity.Should().Be(4m);
        first.TotalValue.Should().Be(50m);
        first.Lines.Should().ContainSingle().Which.UnitCost.Should().Be(12.5m);
        first.Evidence.Should().ContainSingle(value => value.Stage == "Identification");
        first.Actions.Should().ContainSingle(value => value.ActionType == InventoryDisposalActionType.Identified);
        (await fixture.Db.InventoryDisposalCases.CountAsync()).Should().Be(1);
        fixture.ControlEvents.Verify(value => value.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request => request.RuleCode == "INV-020" &&
                request.DecisionKeys.Count == 14 && request.Evidence.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.TrackingControls.Verify(value => value.ValidateAvailabilityAsync(
            fixture.Item.Id, fixture.Warehouse.Id, fixture.Location.Id, 4m,
            null, null, null, It.IsAny<CancellationToken>()), Times.Once);
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

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, InventoryDisposalService service, MutableCurrentUser current,
            Mock<IProcurementControlEventService> controlEvents,
            Mock<IInventoryTrackingControlService> trackingControls,
            Warehouse warehouse, WarehouseLocation location,
            InventoryItem item, CentralDocumentVersion version, FileUploadRecord upload, Guid auditorId,
            IReadOnlyList<Guid> committeeMemberIds)
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
            var service = new InventoryDisposalService(db, current, access.Object,
                Mock.Of<IProcurementSodGuardService>(), Mock.Of<IWorkflowIntegrationService>(),
                Mock.Of<IStockAdjustmentService>(), Mock.Of<IFinancePostingEngine>(),
                trackingControls.Object, events.Object);
            return new Fixture(db, service, current, events, trackingControls, warehouse, location, item, version, upload,
                auditorId, memberIds);
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
