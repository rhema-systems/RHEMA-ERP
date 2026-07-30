using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementWorksCloseoutServiceTests
{
    [Fact]
    public async Task OverviewIsTenantSafeAndExposesCompleteDecisionRegister()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();

        var result = await fixture.Service.GetOverviewAsync(fixture.ContractId);

        result.IsWorksContract.Should().BeTrue();
        result.Project.Should().NotBeNull();
        result.Project!.ProjectId.Should().Be(fixture.ProjectId);
        result.Project.ProjectCode.Should().Be("PRJ-TDC0409");
        result.DecisionKeys.Should().Equal(
            Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}"));
        result.RequiredEvidence.Keys.Should().HaveCount(11);
        result.History.Should().BeEmpty();
        fixture.AccessRequests.Should().ContainSingle()
            .Which.PermissionCode.Should().Be("procurement.records.read");
    }

    [Fact]
    public async Task ExternalContextFailsBeforeTenantDataIsRead()
    {
        await using var fixture = new Fixture(external: true);

        var action = () => fixture.Service.GetOverviewAsync(Guid.NewGuid());

        await action.Should()
            .ThrowAsync<ProcurementWorksCloseoutAuthorizationException>();
        fixture.AccessRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task InitialTakeoverSubmissionIsIdempotentAndIndependentApprovalIsAudited()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.InitialTakeoverRequest("initial-takeover-001");

        var submitted = await fixture.Service.SubmitAsync(
            fixture.ContractId, request, "trace-submit");
        var replay = await fixture.Service.SubmitAsync(
            fixture.ContractId, request, "trace-replay");

        submitted.Id.Should().Be(replay.Id);
        submitted.Status.Should().Be(
            ProcurementWorksCloseoutActionStatus.PendingApproval);
        submitted.Evidence.Should().HaveCount(2);
        submitted.AmountAutoPosted.Should().BeFalse();
        submitted.WorkflowInstanceId.Should().Be(fixture.WorkflowInstanceId);
        (await fixture.Context.ProcurementWorksCloseoutActions.CountAsync())
            .Should().Be(1);
        fixture.Workflow.Verify(item => item.SubmitAsync(
            "PROCUREMENT_CONTRACT",
            submitted.Id,
            fixture.WorkflowDefinitionId), Times.Once);

        await fixture.CompleteWorkflowAsync(submitted.Id);
        fixture.SwitchToApprover();
        var approved = await fixture.Service.DecideAsync(
            submitted.Id,
            new DecideProcurementWorksCloseoutActionRequest
            {
                Approved = true,
                Comment = "Independent approval after completed shared workflow.",
                RowVersion = submitted.RowVersion
            },
            "trace-approve");

        approved.Status.Should().Be(ProcurementWorksCloseoutActionStatus.Approved);
        approved.DecidedById.Should().Be(fixture.ApproverUserId);
        approved.AmountAutoPosted.Should().BeFalse();
        fixture.Store.Verify(item => item.SetMutationContextAsync(
            submitted.Id, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Store.Verify(item => item.ClearMutationContextAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.ControlEvents.Verify(item => item.RecordAsync(
            It.IsAny<ProcurementControlEventWriteRequest>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SubmitterCannotApproveOwnCloseoutAction()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var submitted = await fixture.Service.SubmitAsync(
            fixture.ContractId,
            fixture.InitialTakeoverRequest("initial-takeover-sod"),
            "trace-submit-sod");

        var action = () => fixture.Service.DecideAsync(
            submitted.Id,
            new DecideProcurementWorksCloseoutActionRequest
            {
                Approved = true,
                Comment = "Self approval must be rejected.",
                RowVersion = submitted.RowVersion
            },
            "trace-self-approve");

        await action.Should()
            .ThrowAsync<ProcurementWorksCloseoutAuthorizationException>();
        fixture.Store.Verify(item => item.SetMutationContextAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StaleContractAndForeignTenantAreRejectedBeforeMutation()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.InitialTakeoverRequest("initial-takeover-stale");
        request.ContractRowVersion = Convert.ToBase64String([42]);

        var stale = () => fixture.Service.SubmitAsync(
            fixture.ContractId, request, "trace-stale");
        var foreign = () => fixture.Service.GetOverviewAsync(fixture.OtherContractId);

        (await stale.Should()
            .ThrowAsync<ProcurementWorksCloseoutConflictException>())
            .Which.Code.Should().Be("WORKS_CLOSEOUT_CONTRACT_STALE");
        (await foreign.Should()
            .ThrowAsync<ProcurementWorksCloseoutNotFoundException>())
            .Which.Code.Should().Be("CONTRACT_NOT_FOUND");
        (await fixture.Context.ProcurementWorksCloseoutActions.CountAsync())
            .Should().Be(0);
        fixture.Workflow.Verify(item => item.SubmitAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private Guid _activeUserId;

        public Fixture(bool external = false)
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            ApproverUserId = Guid.NewGuid();
            ContractCreatorUserId = Guid.NewGuid();
            ContractId = Guid.NewGuid();
            OtherContractId = Guid.NewGuid();
            ProjectId = Guid.NewGuid();
            HandoverId = Guid.NewGuid();
            WorkflowDefinitionId = Guid.NewGuid();
            WorkflowInstanceId = Guid.NewGuid();
            PolicySetId = Guid.NewGuid();
            AuthorityRuleId = Guid.NewGuid();
            _activeUserId = UserId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            _unitOfWork = new UnitOfWork(Context);

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(external);
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(() => _activeUserId);
            current.SetupGet(item => item.Username).Returns("tdc0409@tests.local");
            current.SetupGet(item => item.FullName).Returns("TDC 0409 Actor");
            current.SetupGet(item => item.Roles)
                .Returns(["Procurement Officer"]);

            Access = new Mock<IProcurementAccessControlService>();
            Access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementAccessCapabilityRequest, string,
                    CancellationToken>((request, _, _) =>
                    AccessRequests.Add(request))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Code = "ACCESS_ALLOWED",
                    Message = "Allowed"
                });

            var configuration = new Mock<IProcurementConfigurationService>();
            configuration.Setup(item => item.GetEffectiveProfileAsync(
                    "TDC-PROCUREMENT",
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Profile());

            Compliance = new Mock<IProcurementComplianceDecisionService>();
            Compliance.Setup(item => item.EvaluateAuthorityRouteAsync(
                    It.IsAny<ProcurementAuthorityRouteDecisionRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ReadyAuthority());
            Workflow = new Mock<IWorkflowIntegrationService>();
            Workflow.Setup(item => item.SubmitAsync(
                    "PROCUREMENT_CONTRACT",
                    It.IsAny<Guid>(),
                    WorkflowDefinitionId))
                .ReturnsAsync(new WorkflowIntegrationResult(
                    new WorkflowExecutionResult
                    {
                        Success = true,
                        Status = WorkflowInstanceStatus.InProgress,
                        WorkflowInstanceId = WorkflowInstanceId
                    },
                    WorkflowOutcome.Pending));
            Store = new Mock<IProcurementWorksCloseoutStore>();
            Store.SetupGet(item => item.HasRequiredTransaction).Returns(true);
            Store.Setup(item => item.SetMutationContextAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Store.Setup(item => item.ClearMutationContextAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            ControlEvents = new Mock<IProcurementControlEventService>();
            ControlEvents.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            var notifications = new Mock<INotificationTopicPublisher>();
            notifications.Setup(item => item.PublishAsync(
                    It.IsAny<NotificationTopicEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Service = new ProcurementWorksCloseoutService(
                _unitOfWork,
                Store.Object,
                current.Object,
                Access.Object,
                configuration.Object,
                Compliance.Object,
                Workflow.Object,
                ControlEvents.Object,
                notifications.Object,
                NullLogger<ProcurementWorksCloseoutService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public Guid ApproverUserId { get; }
        public Guid ContractCreatorUserId { get; }
        public Guid ContractId { get; }
        public Guid OtherContractId { get; }
        public Guid ProjectId { get; }
        public Guid HandoverId { get; }
        public Guid WorkflowDefinitionId { get; }
        public Guid WorkflowInstanceId { get; }
        public Guid PolicySetId { get; }
        public Guid AuthorityRuleId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementWorksCloseoutService Service { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public Mock<IProcurementComplianceDecisionService> Compliance { get; }
        public Mock<IWorkflowIntegrationService> Workflow { get; }
        public Mock<IProcurementWorksCloseoutStore> Store { get; }
        public Mock<IProcurementControlEventService> ControlEvents { get; }
        public List<ProcurementAccessCapabilityRequest> AccessRequests { get; } = [];
        private List<Guid> EvidenceIds { get; } = [];

        public async Task SeedAsync()
        {
            var otherTenant = Guid.NewGuid();
            var currentContract = new Contract
                {
                    Id = ContractId,
                    TenantId = TenantId,
                    ContractNumber = "CON-TDC0409",
                    ContractTitle = "Works closeout test",
                    ContractType = "Works",
                    Status = "Active",
                    TenderAwardId = Guid.NewGuid(),
                    TenderId = Guid.NewGuid(),
                    BusinessPartnerId = Guid.NewGuid(),
                    ContractValue = 1_000_000m,
                    Currency = "GHS",
                    RetentionPercentage = 5m,
                    WarrantyPeriodDays = 365,
                    CreatedById = ContractCreatorUserId
                };
            var otherContract = new Contract
                {
                    Id = OtherContractId,
                    TenantId = otherTenant,
                    ContractNumber = "CON-OTHER",
                    ContractTitle = "Other tenant Works closeout",
                    ContractType = "Works",
                    Status = "Active",
                    TenderAwardId = Guid.NewGuid(),
                    TenderId = Guid.NewGuid(),
                    BusinessPartnerId = Guid.NewGuid(),
                    ContractValue = 2_000_000m,
                    Currency = "USD"
                };
            Context.AddRange(
                currentContract,
                new Project
                {
                    Id = ProjectId,
                    TenantId = TenantId,
                    ProjectCode = "PRJ-TDC0409",
                    Title = "Current tenant Works project",
                    Status = ProjectStatuses.InProgress,
                    ContractId = ContractId
                },
                otherContract,
                new Project
                {
                    Id = Guid.NewGuid(),
                    TenantId = otherTenant,
                    ProjectCode = "PRJ-OTHER",
                    Title = "Other tenant project",
                    Status = ProjectStatuses.InProgress,
                    ContractId = OtherContractId
                },
                new ProjectHandoverItem
                {
                    Id = HandoverId,
                    TenantId = TenantId,
                    ProjectId = ProjectId,
                    HandoverType = ProjectHandoverItemTypes.PracticalCompletion,
                    Title = "Practical completion",
                    Status = ProjectHandoverItemStatuses.Completed,
                    CompletedDate = DateTime.UtcNow.AddDays(-1)
                });
            foreach (var key in new[]
                     {
                         "practical-completion-certificate",
                         "takeover-inspection"
                     })
            {
                var id = Guid.NewGuid();
                EvidenceIds.Add(id);
                Context.Add(new WorkflowEvidenceDocument
                {
                    Id = id,
                    TenantId = TenantId,
                    StepInstanceId = Guid.NewGuid(),
                    AttachmentId = id.ToString("N"),
                    RequirementKey = key,
                    FileName = $"{key}.pdf",
                    FilePath = $"/controlled/{id:N}.pdf",
                    ContentType = "application/pdf",
                    FileSizeBytes = 1024,
                    Sha256 = new string(key[0], 64),
                    UploadedById = UserId,
                    DocumentOwnerId = UserId,
                    IsCurrent = true,
                    VerificationStatus =
                        WorkflowEvidenceVerificationStatus.Verified,
                    MalwareScanStatus = WorkflowMalwareScanStatus.Clean,
                    RetainUntil = DateTime.UtcNow.AddYears(7)
                });
            }
            await Context.SaveChangesAsync();
        }

        public SubmitProcurementWorksCloseoutActionRequest InitialTakeoverRequest(
            string idempotencyKey)
        {
            var contract = Context.Contracts.Single(item => item.Id == ContractId);
            var keys = new[]
            {
                "practical-completion-certificate",
                "takeover-inspection"
            };
            return new SubmitProcurementWorksCloseoutActionRequest
            {
                ActionType = ProcurementWorksCloseoutActionType.InitialTakeover,
                ProjectHandoverItemId = HandoverId,
                EffectiveAtUtc = DateTime.UtcNow,
                Reason = "Practical completion is certified for initial takeover.",
                IdempotencyKey = idempotencyKey,
                ContractRowVersion = Convert.ToBase64String(contract.RowVersion),
                Evidence = keys.Select((key, index) =>
                    new ProcurementWorksCloseoutEvidenceRequest
                    {
                        RequirementKey = key,
                        ReferenceKind =
                            ProcurementContractActivationEvidenceKind
                                .WorkflowEvidenceDocument,
                        WorkflowEvidenceDocumentId = EvidenceIds[index],
                        EvidenceReference = $"client-reference-{index}"
                    }).ToList()
            };
        }

        public async Task CompleteWorkflowAsync(Guid actionId)
        {
            Context.Add(new WorkflowInstance
            {
                Id = WorkflowInstanceId,
                TenantId = TenantId,
                WorkflowDefinitionId = WorkflowDefinitionId,
                EntityId = actionId,
                EntityTypeId = Guid.NewGuid(),
                Status = WorkflowInstanceStatus.Completed,
                InitiatedById = UserId,
                StartedById = UserId,
                StartedDate = DateTime.UtcNow.AddMinutes(-1),
                CompletedDate = DateTime.UtcNow
            });
            await Context.SaveChangesAsync();
        }

        public void SwitchToApprover() => _activeUserId = ApproverUserId;

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }

        private ProcurementAuthorityRouteDecisionDto ReadyAuthority() => new()
        {
            IsReady = true,
            DecisionCode = "WORKS_AUTHORITY_READY",
            Message = "Works authority and exact workflow are ready.",
            Category = ProcurementCategoryClass.Works,
            Amount = 1_000_000m,
            CurrencyCode = "GHS",
            Policy = new ProcurementCompliancePolicySelectionDto
            {
                PolicySetId = PolicySetId,
                PolicyCode = "TDC-WORKS",
                PolicyName = "TDC Works Authority",
                Version = 1,
                CurrencyCode = "GHS",
                EffectiveFrom = DateTime.UtcNow.AddDays(-1)
            },
            Workflow = new ProcurementAuthorityWorkflowSelectionDto
            {
                WorkflowDefinitionId = WorkflowDefinitionId,
                DefinitionKey = Guid.NewGuid(),
                Name = "Works closeout approval",
                Version = 1,
                EntityTypeCode = "ProcurementWorksCloseout",
                EntityTypeName = "Procurement Works Closeout"
            },
            Steps =
            [
                new ProcurementAuthorityRouteStepDecisionDto
                {
                    Sequence = 1,
                    RuleId = AuthorityRuleId,
                    RulePolicySetId = PolicySetId,
                    RulePolicyCode = "TDC-WORKS",
                    RulePolicyVersion = 1,
                    RuleCode = "WORKS-CLOSEOUT",
                    SourceDecisionKey = "DEC-014",
                    AuthorityName = "Works Closeout Committee",
                    AuthorityRole = "WorksApprover",
                    CurrencyCode = "GHS",
                    LowerBound = 0m,
                    LowerInclusive = true,
                    Quorum = 1,
                    WorkflowDefinitionId = WorkflowDefinitionId,
                    WorkflowStepId = Guid.NewGuid(),
                    WorkflowStepName = "Independent approval",
                    WorkflowStepOrder = 1
                }
            ]
        };

        private static ProcurementConfigurationProfileDto Profile() => new()
        {
            Id = Guid.NewGuid(),
            ProfileCode = "TDC-PROCUREMENT",
            Version = 1,
            Decisions = Enumerable.Range(1, 14)
                .Select(item => new ProcurementConfigurationDecisionDto
                {
                    Id = Guid.NewGuid(),
                    DecisionKey = $"DEC-{item:000}",
                    IsComplete = true
                }).ToList()
        };
    }
}
