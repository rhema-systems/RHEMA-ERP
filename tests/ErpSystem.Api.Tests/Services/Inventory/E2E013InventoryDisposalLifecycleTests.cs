using ErpSystem.Api.Services.Inventory;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
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

public sealed class E2E013InventoryDisposalLifecycleTests
{
    [Fact]
    [Trait("Batch", "E2E-013")]
    public async Task Obsolete_stock_follows_independent_disposal_auction_stock_and_balanced_proceeds_posting()
    {
        var tenantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var auditorId = Guid.NewGuid();
        var schedulerId = Guid.NewGuid();
        var committeeIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var approverId = Guid.NewGuid();
        var executorId = Guid.NewGuid();
        var completionId = Guid.NewGuid();
        var proceedsAccountId = Guid.NewGuid();
        var recoveryAccountId = Guid.NewGuid();
        var inventoryControlAccountId = Guid.NewGuid();
        var writeOffExpenseAccountId = Guid.NewGuid();
        var postingEventId = Guid.NewGuid();
        var journalEntryId = Guid.NewGuid();

        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"e2e-013-disposal-{Guid.NewGuid():N}")
            .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var tenant = new Tenant { Id = tenantId, Name = "E2E 013 Tenant", Code = $"E{tenantId:N}"[..20] };
        var users = new[]
        {
            User(requesterId, tenantId, tenant, "Stores", "Requester"),
            User(auditorId, tenantId, tenant, "Internal", "Auditor"),
            User(schedulerId, tenantId, tenant, "Disposal", "Secretary"),
            User(committeeIds[0], tenantId, tenant, "Committee", "One"),
            User(committeeIds[1], tenantId, tenant, "Committee", "Two"),
            User(committeeIds[2], tenantId, tenant, "Committee", "Three"),
            User(approverId, tenantId, tenant, "Board", "Approver"),
            User(executorId, tenantId, tenant, "Auction", "Executor"),
            User(completionId, tenantId, tenant, "Stores", "Poster")
        };
        var committeeRole = new ApplicationRole("TDC_DISPOSAL_COMMITTEE_MEMBER")
        {
            Id = Guid.NewGuid(),
            NormalizedName = "TDC_DISPOSAL_COMMITTEE_MEMBER",
            IsSystemRole = true
        };
        var category = new InventoryCategory
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "OBS-E2E", Name = "Obsolete stock"
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "WH-E2E-013",
            Name = "E2E Disposal Warehouse", IsActive = true
        };
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id,
            LocationCode = "OBS-01", Name = "Obsolete Stock Bin", IsActive = true, Warehouse = warehouse
        };
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = category.Id, Category = category,
            ItemCode = "OBS-E2E-001", Name = "Obsolete pump component", UnitOfMeasure = "EA",
            Status = ItemStatus.Active, AverageCost = 12.5m, StandardCost = 12m
        };
        var balance = new InventoryBalance
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = item.Id,
            WarehouseId = warehouse.Id, LocationId = location.Id, QuantityOnHand = 10m,
            QuantityAvailable = 10m, AverageUnitCost = 12.5m, TotalValue = 125m,
            InventoryItem = item, Warehouse = warehouse, Location = location
        };
        var identificationEvidence = Evidence(tenantId, requesterId, "DMS-E2E-013-ID", "condition-report.pdf");
        var executionEvidence = Evidence(tenantId, executorId, "DMS-E2E-013-EXEC", "auction-completion.pdf");
        db.Tenants.Add(tenant);
        db.Users.AddRange(users);
        db.Roles.Add(committeeRole);
        db.UserRoles.AddRange(committeeIds.Select(userId => new ApplicationUserRole
        {
            UserId = userId, RoleId = committeeRole.Id
        }));
        db.InventoryCategories.Add(category);
        db.Warehouses.Add(warehouse);
        db.WarehouseLocations.Add(location);
        db.InventoryItems.Add(item);
        db.InventoryBalances.Add(balance);
        AddEvidence(db, identificationEvidence);
        AddEvidence(db, executionEvidence);
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS",
            ControlAccountInventoryId = inventoryControlAccountId,
            WriteOffExpenseAccountId = writeOffExpenseAccountId,
            WriteOffRecoveryAccountId = recoveryAccountId
        });
        db.Accounts.AddRange(
            Account(proceedsAccountId, tenantId, "101-E2E-013", "Disposal proceeds", AccountType.Asset),
            Account(inventoryControlAccountId, tenantId, "141-E2E-013", "Inventory control", AccountType.Asset),
            Account(writeOffExpenseAccountId, tenantId, "611-E2E-013", "Disposal write-off", AccountType.Expense),
            Account(recoveryAccountId, tenantId, "409-E2E-013", "Disposal recovery", AccountType.Revenue));
        await db.SaveChangesAsync();

        var current = new MutableCurrentUser(requesterId, tenantId);
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(value => value.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true, PermissionCode = request.PermissionCode, TenantId = tenantId,
                    ActorUserId = current.UserId, WarehouseId = request.WarehouseId,
                    LocationId = request.LocationId, CorrelationId = correlation
                });
        ProcurementSodGuardRequest? sodRequest = null;
        var sod = new Mock<IProcurementSodGuardService>();
        sod.Setup(value => value.EnforceAsync(
                It.IsAny<ProcurementSodGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<ProcurementSodGuardRequest, string, CancellationToken>((request, _, _) => sodRequest = request)
            .ReturnsAsync(new ProcurementSodGuardDecisionDto { Allowed = true, Code = "ALLOWED" });
        var workflowInstanceId = Guid.NewGuid();
        var workflow = new Mock<IWorkflowIntegrationService>();
        workflow.Setup(value => value.SubmitAsync("InventoryDisposal", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowIntegrationResult(new WorkflowExecutionResult
            {
                Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = workflowInstanceId
            }, WorkflowOutcome.Pending));
        workflow.Setup(value => value.CanUserApproveAsync("InventoryDisposal", It.IsAny<Guid>(), approverId))
            .ReturnsAsync(true);
        workflow.Setup(value => value.ProcessApprovalAsync(
                "InventoryDisposal", It.IsAny<Guid>(), approverId, "Approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowIntegrationResult(new WorkflowExecutionResult
            {
                Success = true, Status = WorkflowInstanceStatus.Completed, WorkflowInstanceId = workflowInstanceId
            }, WorkflowOutcome.Approved));

        InventoryDisposalStockAdjustmentRequest? adjustmentRequest = null;
        var adjustmentPostingEventId = Guid.NewGuid();
        var adjustmentJournalId = Guid.NewGuid();
        var valuationEventId = Guid.NewGuid();
        var participant = new Mock<IInventoryDisposalStockAdjustmentParticipant>();
        participant.Setup(value => value.PreviewAsync(It.IsAny<InventoryDisposalStockAdjustmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryDisposalStockAdjustmentRequest request, CancellationToken _) => new StockAdjustment
            {
                Id = request.AdjustmentId, TenantId = tenantId, AdjustmentNumber = request.AdjustmentNumber,
                AdjustmentDate = request.PostingDateUtc, WarehouseId = request.Create.WarehouseId,
                ReasonCode = request.Create.ReasonCode, Status = "Draft",
                Items = request.Create.Items.Select((line, index) => new StockAdjustmentItem
                {
                    Id = request.ItemIds[index], TenantId = tenantId, AdjustmentId = request.AdjustmentId,
                    InventoryItemId = line.InventoryItemId, LocationId = line.LocationId,
                    LotNumber = line.LotNumber, BatchNumber = line.BatchNumber, SerialNumber = line.SerialNumber,
                    AdjustmentQuantity = line.AdjustmentQuantity, UnitCost = line.UnitCost ?? 0m,
                    AdjustmentValue = decimal.Round(line.AdjustmentQuantity * (line.UnitCost ?? 0m), 4),
                    CreatedAt = request.PostingDateUtc
                }).ToList()
            });
        participant.Setup(value => value.StageApprovedAsync(It.IsAny<InventoryDisposalStockAdjustmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryDisposalStockAdjustmentRequest request, CancellationToken _) =>
            {
                adjustmentRequest = request;
                return new StockAdjustment { Id = request.AdjustmentId, TenantId = tenantId, Status = "Approved" };
            });
        participant.Setup(value => value.StagePostedAsync(It.IsAny<StockAdjustment>(), completionId,
                adjustmentPostingEventId, adjustmentJournalId, It.IsAny<IReadOnlyDictionary<Guid, Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var valuation = new StockAdjustmentValuationIntentBuilder(db);
        var groups = new Mock<IFinanceProducerIntentGroupService>();
        ProducerIntentGroupRequestDto? preparedGroup = null;
        groups.Setup(value => value.PrepareAsync(It.IsAny<ProducerIntentGroupRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProducerIntentGroupRequestDto request, CancellationToken _) =>
            {
                preparedGroup = request;
                return new ProducerIntentGroupDto { Id = request.ProducerIntentGroupId!.Value };
            });
        groups.Setup(value => value.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new ProducerIntentGroupDto
            {
                Id = id, Status = "Approved", PreparedByUserId = executorId, DecidedByUserId = completionId,
                DecidedAtUtc = DateTime.UtcNow, GroupFingerprint = "E2E-GROUP",
                ExpectedOwnerEffect = preparedGroup!.ExpectedOwnerEffect,
                Members = preparedGroup.Members.Select((member, index) => new ProducerIntentGroupMemberDto
                {
                    MemberOrder = index + 1, MemberFingerprint = index == 0 ? "P" : "V",
                    AccountingEvent = new AccountingEventDto { Id = member.AccountingEventId!.Value,
                        RequestFingerprint = index == 0 ? "P" : "V" }
                }).ToList()
            });
        var intents = new Mock<IFinanceProducerIntentService>();
        var groupExecution = new Mock<IFinanceProducerIntentGroupApprovedExecution>();
        groupExecution.Setup(value => value.ExecuteWithCompatibilityResultInAmbientTransactionAsync(It.IsAny<Guid>(), It.IsAny<ProducerIntentGroupRequestDto>(), It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid groupId, ProducerIntentGroupRequestDto request, ProducerOwnerEffectReceiptDto _, CancellationToken _) =>
                new FinanceProducerIntentGroupApprovedExecutionResult(groupId, "E2E-GROUP", "Posted",
                [new FinanceProducerIntentGroupMemberExecutionResult(1, "P", request.Members[0].AccountingEventId!.Value, "P", postingEventId, journalEntryId),
                 new FinanceProducerIntentGroupMemberExecutionResult(2, "V", request.Members[1].AccountingEventId!.Value, "V", adjustmentPostingEventId, adjustmentJournalId)]));
        var intentExecution = new Mock<IFinanceProducerApprovedExecution>();
        var controlEventRequests = new List<ProcurementControlEventWriteRequest>();
        var controlEvents = new Mock<IProcurementControlEventService>();
        controlEvents.Setup(value => value.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcurementControlEventWriteRequest, CancellationToken>((request, _) => controlEventRequests.Add(request))
            .ReturnsAsync(new ProcurementControlEventDto { Id = Guid.NewGuid(), TenantId = tenantId });

        var service = new InventoryDisposalService(db, current, access.Object, sod.Object, workflow.Object,
            participant.Object, valuation, intents.Object, groups.Object, intentExecution.Object, groupExecution.Object,
            Mock.Of<IInventoryTrackingControlService>(), controlEvents.Object);
        var identified = await service.CreateAsync(new CreateInventoryDisposalRequest
        {
            WarehouseId = warehouse.Id,
            Method = InventoryDisposalMethod.Auction,
            Reason = "Obsolete component approved for governed auction disposal",
            IdentificationDetails = "Stores isolated and counted four obsolete units in the exact bin.",
            IdempotencyKey = "e2e-013-identify",
            CorrelationId = "E2E-013",
            Lines =
            [
                new CreateInventoryDisposalLineRequest
                {
                    InventoryItemId = item.Id, LocationId = location.Id, Quantity = 4m,
                    ConditionNotes = "Obsolete and no longer serviceable"
                }
            ],
            Evidence =
            [
                new InventoryControlEvidenceRequest
                {
                    CentralDocumentVersionId = identificationEvidence.Version.Id,
                    EvidenceReference = "Independent condition and quantity report"
                }
            ]
        });
        identified.TotalQuantity.Should().Be(4m);
        identified.TotalValue.Should().Be(50m);

        current.UserId = auditorId;
        var verified = await service.VerifyAsync(identified.Id, new VerifyInventoryDisposalRequest
        {
            Verified = true,
            Findings = "Internal Audit independently confirmed the exact stock, condition and valuation.",
            RowVersion = identified.RowVersion,
            IdempotencyKey = "e2e-013-audit"
        });
        current.UserId = schedulerId;
        var scheduled = await service.ScheduleCommitteeAsync(identified.Id, new ScheduleInventoryDisposalCommitteeRequest
        {
            MeetingAtUtc = DateTime.UtcNow.AddDays(1),
            CommitteeReference = "DC-E2E-013",
            MemberUserIds = committeeIds.ToList(),
            RowVersion = verified.RowVersion,
            IdempotencyKey = "e2e-013-schedule"
        });
        var scheduledEntity = await db.InventoryDisposalCases.SingleAsync(value => value.Id == identified.Id);
        scheduledEntity.CommitteeMeetingAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var voted = scheduled;
        for (var index = 0; index < committeeIds.Length; index++)
        {
            current.UserId = committeeIds[index];
            voted = await service.VoteAsync(identified.Id, new VoteInventoryDisposalRequest
            {
                RecommendApproval = true,
                ConflictDeclared = false,
                Comment = $"Committee member {index + 1} recommends the controlled auction.",
                RowVersion = voted.RowVersion,
                IdempotencyKey = $"e2e-013-vote-{index + 1}"
            });
        }
        voted.Status.Should().Be(InventoryDisposalStatus.CommitteeRecommended);

        current.UserId = schedulerId;
        var submitted = await service.SubmitAsync(identified.Id, new SubmitInventoryDisposalRequest
        {
            RowVersion = voted.RowVersion,
            IdempotencyKey = "e2e-013-submit",
            Comment = "Submit committee recommendation to the configured MD/Board workflow."
        });
        current.UserId = approverId;
        var approved = await service.DecideAsync(identified.Id, new DecideInventoryDisposalRequest
        {
            Approved = true,
            RowVersion = submitted.RowVersion,
            IdempotencyKey = "e2e-013-approve",
            Comment = "Independent delegated authority approval."
        });
        current.UserId = executorId;
        var staged = await service.StageExecutionAsync(identified.Id, new StageInventoryDisposalExecutionRequest
        {
            ProceedsAmount = 40m,
            ProceedsAccountId = proceedsAccountId,
            BuyerOrRecipient = "E2E Auction Buyer Limited",
            ExecutionReference = "AUCTION-E2E-013",
            RowVersion = approved.RowVersion,
            IdempotencyKey = "e2e-013-execute",
            Evidence =
            [
                new InventoryControlEvidenceRequest
                {
                    CentralDocumentVersionId = executionEvidence.Version.Id,
                    EvidenceReference = "Auction award and settlement evidence"
                }
            ]
        });
        current.UserId = completionId;
        var completed = await service.CompleteAsync(identified.Id, new CompleteInventoryDisposalRequest
        {
            RowVersion = staged.RowVersion,
            IdempotencyKey = "e2e-013-complete",
            Comment = "Independently approve and post the linked stock adjustment and proceeds."
        });

        completed.Status.Should().Be(InventoryDisposalStatus.Completed);
        completed.StockAdjustmentId.Should().NotBeNull();
        completed.ProceedsAmount.Should().Be(40m);
        completed.ProceedsPostingEventId.Should().Be(postingEventId);
        completed.ProceedsJournalEntryId.Should().Be(journalEntryId);
        completed.Evidence.Should().Contain(value => value.Stage == "Identification");
        completed.Evidence.Should().Contain(value => value.Stage == "Execution");
        completed.Actions.Select(value => value.ActionType).Should().ContainInOrder(
            InventoryDisposalActionType.Identified,
            InventoryDisposalActionType.AuditVerified,
            InventoryDisposalActionType.CommitteeScheduled,
            InventoryDisposalActionType.CommitteeRecommended,
            InventoryDisposalActionType.Submitted,
            InventoryDisposalActionType.Approved,
            InventoryDisposalActionType.AdjustmentStaged,
            InventoryDisposalActionType.Completed);
        completed.Actions.Select(value => value.ActorUserId).Should().Contain(
            new[] { requesterId, auditorId, schedulerId, approverId, executorId, completionId });
        completed.CommitteeMembers.Should().OnlyContain(value => value.RecommendApproval == true && !value.ConflictDeclared);

        adjustmentRequest.Should().NotBeNull();
        adjustmentRequest!.Create.ReasonCode.Should().Be(StockAdjustmentReasonCodes.WriteOff);
        adjustmentRequest.Create.Reference.Should().Be(completed.DisposalNumber);
        adjustmentRequest.Create.Items.Should().ContainSingle(value =>
            value.InventoryItemId == item.Id && value.LocationId == location.Id && value.AdjustmentQuantity == -4m);
        adjustmentRequest.Create.Evidence.Should().HaveCount(2);
        participant.Verify(value => value.PreviewAsync(It.IsAny<InventoryDisposalStockAdjustmentRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        participant.Verify(value => value.StageApprovedAsync(It.IsAny<InventoryDisposalStockAdjustmentRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        sodRequest.Should().NotBeNull();
        sodRequest!.ProhibitedActorUserIds.Should().BeEquivalentTo(new[] { requesterId, auditorId }.Concat(committeeIds));
        groups.Verify(value => value.PrepareAsync(It.Is<ProducerIntentGroupRequestDto>(group =>
            group.Members.Count == 2 && group.Members[0].PostingRequest.SourceDocumentType == "InventoryDisposal" &&
            group.Members[1].PostingRequest.SourceDocumentType == "StockAdjustment"), It.IsAny<CancellationToken>()), Times.Once);
        groupExecution.Verify(value => value.ExecuteWithCompatibilityResultInAmbientTransactionAsync(
            It.IsAny<Guid>(), It.IsAny<ProducerIntentGroupRequestDto>(), It.IsAny<ProducerOwnerEffectReceiptDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
        controlEventRequests.Should().HaveCount(completed.Actions.Count);
        controlEventRequests.Should().OnlyContain(value =>
            value.RuleCode == "INV-020" && value.DecisionKeys.Count == 14 &&
            value.DecisionKeys.First() == "DEC-001" && value.DecisionKeys.Last() == "DEC-014");
        (await db.AuditLogs.CountAsync(value => value.TenantId == tenantId && value.Resource == "InventoryDisposal"))
            .Should().Be(completed.Actions.Count);
    }

    private static (FileUploadRecord Upload, CentralDocumentRecord Record, CentralDocumentVersion Version) Evidence(
        Guid tenantId, Guid userId, string reference, string fileName)
    {
        var upload = new FileUploadRecord
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Category = "inventory-disposal-evidence",
            FilePath = $"dms/inventory/{fileName}", StoredFileName = $"stored-{fileName}",
            OriginalFileName = fileName, ContentType = "application/pdf", FileSize = 256,
            StorageProvider = "Test", UploadedByUserId = userId,
            VirusScanStatus = FileVirusScanStatus.Clean, ScannedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedById = userId
        };
        var record = new CentralDocumentRecord
        {
            Id = Guid.NewGuid(), TenantId = tenantId, DocumentReference = reference,
            Title = fileName, SourceModule = "Inventory", SourceLabel = "Inventory disposal evidence",
            SourceEntityType = "InventoryDisposal", SourceRecordId = Guid.NewGuid(), RepositoryStatus = "Linked",
            RepositoryPath = upload.FilePath, LifecycleStatus = "Active", CurrentVersion = "v1.0",
            VersionStatus = "Published"
        };
        var version = new CentralDocumentVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, DocumentRecordId = record.Id, DocumentRecord = record,
            VersionNumber = "v1.0", Status = "Published", RepositoryPath = upload.FilePath,
            FileName = fileName, ContentType = upload.ContentType, FileSize = upload.FileSize,
            FileUploadRecordId = upload.Id, PublishedAt = DateTime.UtcNow
        };
        return (upload, record, version);
    }

    private static void AddEvidence(ApplicationDbContext db,
        (FileUploadRecord Upload, CentralDocumentRecord Record, CentralDocumentVersion Version) evidence)
    {
        db.FileUploadRecords.Add(evidence.Upload);
        db.CentralDocumentRecords.Add(evidence.Record);
        db.CentralDocumentVersions.Add(evidence.Version);
    }

    private static ApplicationUser User(Guid id, Guid tenantId, Tenant tenant, string first, string last) => new()
    {
        Id = id, TenantId = tenantId, Tenant = tenant, FirstName = first, LastName = last,
        UserName = $"{first}.{last}.{id:N}", NormalizedUserName = $"{first}.{last}.{id:N}".ToUpperInvariant(),
        Email = $"{id:N}@example.test", NormalizedEmail = $"{id:N}@EXAMPLE.TEST", IsActive = true
    };

    private static Account Account(Guid id, Guid tenantId, string number, string name, AccountType type) => new()
    {
        Id = id, TenantId = tenantId, AccountCode = number, AccountNumber = number,
        AccountName = name, AccountType = type, CurrencyCode = "GHS", AllowDirectPosting = true
    };

    private sealed class MutableCurrentUser(Guid userId, Guid tenantId) : ICurrentUserProvider
    {
        public Guid UserId { get; set; } = userId;
        public Guid TenantId { get; } = tenantId;
        public string Username => $"e2e013.{UserId:N}";
        public string FullName => "E2E 013 Test Actor";
        public bool IsAuthenticated => true;
        public IEnumerable<string> Roles => ["TDC Inventory Control"];
        public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string> Claims => new Dictionary<string, string>();
        public bool IsExternalUser => false;
        public string AuthenticationProvider => "Test";
    }
}
