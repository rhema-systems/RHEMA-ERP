using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
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

public sealed class ProcurementTenderDocumentControlServiceTests
{
    [Fact]
    public async Task PublishedPolicyOptionsAreTenantSafeAndExposeExactProfileLineage()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);

        var options = await fixture.Service.GetTemplatePolicyOptionsAsync();
        fixture.SwitchTenant(Guid.NewGuid());
        var foreign = await fixture.Service.GetTemplatePolicyOptionsAsync();

        options.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            fixture.Policy.Id,
            fixture.Policy.Code,
            fixture.Policy.Name,
            fixture.Policy.Version,
            SourceConfigurationProfileId = fixture.Profile.Id,
            SourceConfigurationProfileCode = fixture.Profile.ProfileCode
        }, options => options.ExcludingMissingMembers());
        foreign.Should().BeEmpty();
    }

    [Fact]
    public async Task PaidIssuanceRequiresExactFeeAndReplaysSameCorrelationWithoutDuplicate()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Paid);
        var register = await fixture.BindAsync("bind-paid");
        var request = fixture.Issue(register.RowVersion, amountPaid: 25m);

        var issued = await fixture.Service.IssueAsync(request, "issue-paid");
        var replay = await fixture.Service.IssueAsync(request, "issue-paid");

        replay.Id.Should().Be(issued.Id);
        replay.ReceiptNumber.Should().Be("DOC-REC-001");
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(1);
        issued.IntegrityHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task PaidIssuanceRejectsFeeMismatchBeforeAnyControlledWrite()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Paid);
        var register = await fixture.BindAsync("bind-fee-mismatch");

        var action = () => fixture.Service.IssueAsync(
            fixture.Issue(register.RowVersion, amountPaid: 20m), "issue-fee-mismatch");

        await action.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_FEE_MISMATCH");
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ActiveApprovedSupplierCanReadItsIssuedRegister()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var register = await fixture.BindAsync("bind-active-supplier");
        await fixture.Service.IssueAsync(
            fixture.Issue(register.RowVersion, amountPaid: 0m),
            "issue-active-supplier");
        await fixture.SwitchToExternalAsync();

        var external = await fixture.Service.GetRegisterAsync(
            ProcurementTenderDocumentSourceType.Tender,
            fixture.Tender.Id);

        external.Issuances.Should().ContainSingle(item =>
            item.BusinessPartnerId == fixture.Supplier.Id);
    }

    [Fact]
    public async Task SupplierOnlyDispatchDoesNotRequireAnExternalEmailCollection()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var register = await fixture.BindAsync("bind-supplier-only-dispatch");
        await fixture.Service.IssueAsync(
            fixture.Issue(register.RowVersion, amountPaid: 0m),
            "issue-supplier-only-dispatch");

        var ready = await fixture.Service.EnsureDispatchReadyAsync(
            ProcurementTenderDocumentSourceType.Tender,
            fixture.Tender.Id,
            [fixture.Supplier.Id],
            null!,
            "dispatch-supplier-only");

        ready.Ready.Should().BeTrue();
    }

    [Fact]
    public async Task MandatoryApprovedChangeAcknowledgementBlocksSubmissionUntilAcknowledged()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var register = await fixture.BindAsync("bind-ack");
        var issuance = await fixture.Service.IssueAsync(
            fixture.Issue(register.RowVersion, amountPaid: 0m), "issue-ack");
        var change = new ProcurementTenderDocumentChange
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegisterId = register.Id,
            Sequence = 1,
            ChangeType = ProcurementTenderDocumentChangeType.BidValidityExtension,
            Status = ProcurementTenderDocumentChangeStatus.Approved,
            PreviousValueUtc = register.OriginalBidValidityUntilUtc,
            NewValueUtc = register.OriginalBidValidityUntilUtc.AddDays(7),
            RequiresAcknowledgement = true,
            Reason = "Extend bid validity.",
            WorkflowDefinitionId = Guid.NewGuid(),
            WorkflowOutcome = "Approved",
            ApprovalReference = "APP-001",
            EvidenceReference = "EVID-001",
            RequestedAtUtc = DateTime.UtcNow,
            RequestedByUserId = Guid.NewGuid(),
            DecidedAtUtc = DateTime.UtcNow,
            DecidedByUserId = Guid.NewGuid(),
            CorrelationId = "change-ack",
            LifecycleSnapshotJson = "{}",
            IntegrityHash = Hash("{}"),
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        var recipient = new ProcurementTenderDocumentChangeRecipient
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            ChangeId = change.Id,
            SourceType = ProcurementTenderDocumentRecipientSourceType.Issuance,
            IssuanceId = issuance.Id,
            BusinessPartnerId = fixture.Supplier.Id,
            RecipientKey = issuance.RecipientKey,
            RecipientName = fixture.Supplier.PartnerName,
            DispatchChannel = "Portal",
            DispatchReference = "DSP-001",
            DispatchedAtUtc = DateTime.UtcNow,
            DispatchedByUserId = Guid.NewGuid(),
            DispatchEvidenceReference = "DSP-EVID-001",
            DispatchSnapshotJson = "{}",
            IntegrityHash = Hash("{}")
        };
        fixture.Context.AddRange(change, recipient);
        await fixture.Context.SaveChangesAsync();

        var blocked = () => fixture.Service.EnsureSubmissionReadyAsync(
            ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id,
            fixture.Supplier.Id, "submit-blocked");
        await blocked.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_SUBMISSION_BLOCKED");

        fixture.Context.ProcurementTenderDocumentAcknowledgements.Add(
            new ProcurementTenderDocumentAcknowledgement
            {
                Id = Guid.NewGuid(),
                TenantId = fixture.TenantId,
                ChangeRecipientId = recipient.Id,
                BusinessPartnerId = fixture.Supplier.Id,
                Outcome = ProcurementTenderDocumentAcknowledgementOutcome.Acknowledged,
                AcknowledgedAtUtc = DateTime.UtcNow,
                AcknowledgedByUserId = Guid.NewGuid(),
                AcknowledgementChannel = "Portal",
                AcknowledgementReference = "ACK-001",
                EvidenceReference = "ACK-EVID-001",
                CorrelationId = "ack-ready",
                AcknowledgementSnapshotJson = "{}",
                IntegrityHash = Hash("{}")
            });
        await fixture.Context.SaveChangesAsync();

        var ready = await fixture.Service.EnsureSubmissionReadyAsync(
            ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id,
            fixture.Supplier.Id, "submit-ready");
        ready.Ready.Should().BeTrue();
    }

    [Fact]
    public async Task ApprovedAddendumDispatchKeepsExistingRecipientEligibleWithoutSecondIssuance()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var register = await fixture.BindAsync("bind-addendum");
        var issuance = await fixture.Service.IssueAsync(
            fixture.Issue(register.RowVersion, amountPaid: 0m), "issue-addendum");
        var initialTemplate = await fixture.Context.ProcurementTenderDocumentTemplateVersions
            .SingleAsync(item => item.Id == fixture.TemplateId);
        var nextTemplate = new ProcurementTenderDocumentTemplateVersion
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TemplateKey = initialTemplate.TemplateKey,
            TemplateCode = initialTemplate.TemplateCode,
            Name = initialTemplate.Name,
            DocumentTypeCode = initialTemplate.DocumentTypeCode,
            Version = 2,
            Status = ProcurementTenderDocumentTemplateStatus.Published,
            EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
            PolicySetId = fixture.Policy.Id,
            PolicySetCode = fixture.Policy.Code,
            PolicySetVersion = fixture.Policy.Version,
            SourceConfigurationProfileId = fixture.Profile.Id,
            ContentReference = "evidence:tender-document-v2",
            ContentChecksumSha256 = new string('e', 64),
            WorkflowDefinitionId = initialTemplate.WorkflowDefinitionId,
            LifecycleSnapshotJson = "{}",
            IntegrityHash = Hash("{}"),
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        var change = new ProcurementTenderDocumentChange
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegisterId = register.Id,
            Sequence = 1,
            ChangeType = ProcurementTenderDocumentChangeType.Addendum,
            Status = ProcurementTenderDocumentChangeStatus.Approved,
            PreviousTemplateVersionId = initialTemplate.Id,
            NewTemplateVersionId = nextTemplate.Id,
            RequiresAcknowledgement = false,
            Reason = "Issue revised schedules.",
            WorkflowDefinitionId = Guid.NewGuid(),
            WorkflowOutcome = "Approved",
            ApprovalReference = "APP-ADD-001",
            EvidenceReference = "EVID-ADD-001",
            RequestedAtUtc = DateTime.UtcNow,
            RequestedByUserId = Guid.NewGuid(),
            DecidedAtUtc = DateTime.UtcNow,
            DecidedByUserId = Guid.NewGuid(),
            CorrelationId = "change-addendum",
            LifecycleSnapshotJson = "{}",
            IntegrityHash = Hash("{}"),
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        var recipient = new ProcurementTenderDocumentChangeRecipient
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            ChangeId = change.Id,
            SourceType = ProcurementTenderDocumentRecipientSourceType.Issuance,
            IssuanceId = issuance.Id,
            BusinessPartnerId = fixture.Supplier.Id,
            RecipientKey = issuance.RecipientKey,
            RecipientName = fixture.Supplier.PartnerName,
            RecipientEmail = issuance.RecipientEmail,
            DispatchChannel = "Portal",
            DispatchReference = "DSP-ADD-001",
            DispatchedAtUtc = DateTime.UtcNow,
            DispatchedByUserId = Guid.NewGuid(),
            DispatchEvidenceReference = "DSP-EVID-ADD-001",
            DispatchSnapshotJson = "{}",
            IntegrityHash = Hash("{}")
        };
        fixture.Context.AddRange(nextTemplate, change, recipient);
        await fixture.Context.SaveChangesAsync();

        var dispatchReady = await fixture.Service.EnsureDispatchReadyAsync(
            ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id,
            [fixture.Supplier.Id], [], "dispatch-addendum");
        var submissionReady = await fixture.Service.EnsureSubmissionReadyAsync(
            ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id,
            fixture.Supplier.Id, "submit-addendum");

        dispatchReady.EffectiveTemplateVersionId.Should().Be(nextTemplate.Id);
        submissionReady.Ready.Should().BeTrue();
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task BidValidityExtensionCanBeRequestedAfterTenderCloseWhileCurrentValidityIsActive()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var bound = await fixture.BindAsync("bind-validity-after-close");
        var register = await fixture.Context.ProcurementTenderDocumentRegisters
            .SingleAsync(item => item.Id == bound.Id);
        fixture.Tender.Status = "Closed";
        fixture.Tender.SubmissionDeadline = DateTime.UtcNow.AddDays(-2);
        fixture.Tender.OpeningDate = DateTime.UtcNow.AddDays(-2).AddHours(1);
        register.OriginalSubmissionDeadlineUtc = fixture.Tender.SubmissionDeadline.Value;
        register.OpeningScheduledAtUtc = fixture.Tender.OpeningDate;
        register.OriginalBidValidityUntilUtc = DateTime.UtcNow.AddDays(5);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CreateChangeAsync(new CreateProcurementTenderDocumentChangeRequest
        {
            SourceType = ProcurementTenderDocumentSourceType.Tender,
            SourceId = fixture.Tender.Id,
            ChangeType = ProcurementTenderDocumentChangeType.BidValidityExtension,
            NewValueUtc = register.OriginalBidValidityUntilUtc.AddDays(7),
            RequiresAcknowledgement = true,
            Reason = "Preserve validity while evaluation is completed.",
            WorkflowDefinitionId = fixture.WorkflowDefinitionId,
            EvidenceReference = "EVID-VALIDITY-001",
            RegisterRowVersion = Convert.ToBase64String(register.RowVersion)
        }, "validity-after-close");

        result.Status.Should().Be(ProcurementTenderDocumentChangeStatus.PendingApproval);
        result.PreviousValueUtc.Should().Be(register.OriginalBidValidityUntilUtc);
    }

    [Fact]
    public async Task BidValidityExtensionIsRejectedAfterCurrentValidityExpires()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var bound = await fixture.BindAsync("bind-expired-validity");
        var register = await fixture.Context.ProcurementTenderDocumentRegisters
            .SingleAsync(item => item.Id == bound.Id);
        fixture.Tender.Status = "Closed";
        fixture.Tender.SubmissionDeadline = DateTime.UtcNow.AddDays(-10);
        fixture.Tender.OpeningDate = DateTime.UtcNow.AddDays(-10).AddHours(1);
        register.OriginalSubmissionDeadlineUtc = fixture.Tender.SubmissionDeadline.Value;
        register.OpeningScheduledAtUtc = fixture.Tender.OpeningDate;
        register.OriginalBidValidityUntilUtc = DateTime.UtcNow.AddDays(-1);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.CreateChangeAsync(
            new CreateProcurementTenderDocumentChangeRequest
            {
                SourceType = ProcurementTenderDocumentSourceType.Tender,
                SourceId = fixture.Tender.Id,
                ChangeType = ProcurementTenderDocumentChangeType.BidValidityExtension,
                NewValueUtc = DateTime.UtcNow.AddDays(7),
                RequiresAcknowledgement = true,
                Reason = "Attempted retroactive extension.",
                WorkflowDefinitionId = fixture.WorkflowDefinitionId,
                EvidenceReference = "EVID-VALIDITY-EXPIRED",
                RegisterRowVersion = Convert.ToBase64String(register.RowVersion)
            }, "validity-expired");

        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_VALIDITY_EXTENSION_RETROACTIVE");
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _tenantId;
        private Guid _userId = Guid.NewGuid();
        private bool _external;
        private readonly UnitOfWork _unitOfWork;

        public Fixture(ProcurementTenderDocumentFeeMode feeMode)
        {
            TenantId = Guid.NewGuid();
            _tenantId = TenantId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "TDC",
                Name = "TDC",
                Status = TenantStatus.Active
            });
            Profile = new ProcurementConfigurationProfile
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileCode = "TDC-PROCUREMENT",
                Name = "TDC Procurement",
                Version = 1,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = DateTime.UtcNow.AddDays(-10),
                IsDefault = true
            };
            Policy = new ProcurementPolicySet
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "TDC-POLICY",
                Name = "TDC Policy",
                Version = 1,
                LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
                SourceConfigurationProfileId = Profile.Id,
                EffectiveFrom = DateTime.UtcNow.AddDays(-10),
                DefaultCurrencyCode = "GHS",
                IsDefault = true
            };
            var rule = new ProcurementPolicyMethodRule
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PolicySetId = Policy.Id,
                RuleCode = "M-NCT",
                Name = "NCT",
                Category = ProcurementCategoryClass.Goods,
                Method = ProcurementMethodType.NationalCompetitiveTendering,
                IsAllowed = true,
                IsEnabled = true,
                EffectiveFrom = DateTime.UtcNow.AddDays(-10)
            };
            var sourcingCase = new ProcurementSourcingCase
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PurchaseRequisitionId = Guid.NewGuid(),
                SourcingReleaseId = Guid.NewGuid(),
                CaseSequence = 1,
                CaseNumber = "CASE-001",
                SourcePlanId = Guid.NewGuid(),
                SourcePlanItemId = Guid.NewGuid(),
                Category = ProcurementCategoryClass.Goods,
                RecommendedMethod = rule.Method,
                SelectedMethod = rule.Method,
                EstimatedValue = 1000m,
                CurrencyCode = "GHS",
                PolicySetId = Policy.Id,
                PolicyCode = Policy.Code,
                PolicyVersion = Policy.Version,
                MethodRuleId = rule.Id,
                MethodRuleCode = rule.RuleCode,
                ThresholdRuleId = Guid.NewGuid(),
                ThresholdRuleCode = "TH-001",
                AuthorityRouteId = Guid.NewGuid(),
                AuthorityRouteReference = "AUTH-001",
                Justification = "Competitive sourcing.",
                Status = ProcurementSourcingCaseStatus.InProgress,
                CreatedByName = "Officer",
                SourceControlFingerprint = new string('a', 64),
                CaseFingerprint = new string('b', 64),
                SnapshotJson = "{}",
                IntegrityHash = Hash("{}")
            };
            Tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderNumber = "TND-001",
                Title = "Controlled tender",
                TenderType = "NCT",
                Status = "Approved",
                SubmissionDeadline = DateTime.UtcNow.AddDays(10),
                OpeningDate = DateTime.UtcNow.AddDays(10).AddHours(1),
                EstimatedValue = 1000m,
                Currency = "GHS",
                SourcingCaseId = sourcingCase.Id
            };
            Supplier = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PartnerCode = "SUP-001",
                PartnerName = "Supplier One",
                PartnerType = "Supplier",
                IsActive = true,
                IsBlacklisted = false,
                ApprovalStatus =
                    BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus,
                RegistrationStatus =
                    BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus
            };
            var workflowEntityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "PROCUREMENT_SOURCING",
                Name = "Procurement Sourcing",
                IsActive = true
            };
            var workflowDefinition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DefinitionKey = Guid.NewGuid(),
                Name = "Tender Document Approval",
                EntityTypeId = workflowEntityType.Id,
                Version = 1,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                IsActive = true
            };
            var template = new ProcurementTenderDocumentTemplateVersion
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TemplateKey = Guid.NewGuid(),
                TemplateCode = "TDC-NCT",
                Name = "NCT Document",
                DocumentTypeCode = "TENDER",
                Version = 1,
                Status = ProcurementTenderDocumentTemplateStatus.Published,
                EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
                PolicySetId = Policy.Id,
                PolicySetCode = Policy.Code,
                PolicySetVersion = Policy.Version,
                SourceConfigurationProfileId = Profile.Id,
                ContentReference = "evidence:tender-document",
                ContentChecksumSha256 = new string('c', 64),
                WorkflowDefinitionId = workflowDefinition.Id,
                LifecycleSnapshotJson = "{}",
                IntegrityHash = Hash("{}"),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            template.ApplicableMethods.Add(new ProcurementTenderDocumentTemplateMethod
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TemplateVersionId = template.Id,
                Method = rule.Method,
                IntegrityHash = new string('d', 64)
            });
            Context.AddRange(Profile, Policy, rule, sourcingCase, Tender, Supplier,
                workflowEntityType, workflowDefinition, template);
            Context.SaveChanges();
            TemplateId = template.Id;
            WorkflowDefinitionId = workflowDefinition.Id;
            FeeMode = feeMode;

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            current.SetupGet(item => item.UserId).Returns(() => _userId);
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(() => _external);
            current.SetupGet(item => item.Username).Returns("officer@tdc.test");
            current.SetupGet(item => item.FullName).Returns("Procurement Officer");
            current.SetupGet(item => item.Roles).Returns(() =>
                _external ? ["Supplier"] : ["TenantAdmin"]);
            current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => !_external && role == "TenantAdmin");
            _unitOfWork = new UnitOfWork(Context);
            var access = new Mock<IProcurementAccessControlService>();
            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.EnforceAsync(It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto { Allowed = true });
            var workflow = new Mock<IWorkflowInstanceService>();
            workflow.Setup(item => item.StartWorkflowAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(),
                    It.IsAny<object?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorkflowInstance { Id = Guid.NewGuid(), TenantId = TenantId });
            var supplierValidation = new Mock<ISupplierValidationService>();
            supplierValidation.Setup(item => item.ValidateForTenderAsync(
                    It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<decimal?>()))
                .ReturnsAsync(new SupplierValidationResult { IsValid = true });
            supplierValidation.Setup(item => item.ValidateForRfqAsync(
                    It.IsAny<Guid>(), It.IsAny<List<Guid>?>(), It.IsAny<decimal?>()))
                .ReturnsAsync(new SupplierValidationResult { IsValid = true });
            var notifications = new Mock<INotificationTopicPublisher>();
            var events = new ProcurementControlEventService(
                _unitOfWork, current.Object, NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementTenderDocumentControlService(
                _unitOfWork, current.Object, access.Object, sod.Object, events,
                workflow.Object, supplierValidation.Object, notifications.Object,
                NullLogger<ProcurementTenderDocumentControlService>.Instance);
        }

        public Guid TenantId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementConfigurationProfile Profile { get; }
        public ProcurementPolicySet Policy { get; }
        public Tender Tender { get; }
        public BusinessPartner Supplier { get; }
        public Guid TemplateId { get; }
        public Guid WorkflowDefinitionId { get; }
        public ProcurementTenderDocumentFeeMode FeeMode { get; }
        public ProcurementTenderDocumentControlService Service { get; }

        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;

        public async Task SwitchToExternalAsync()
        {
            _external = true;
            _userId = Guid.NewGuid();
            Context.Add(new BusinessPartnerUser
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BusinessPartnerId = Supplier.Id,
                UserId = _userId,
                Role = "User",
                IsActive = true
            });
            await Context.SaveChangesAsync();
        }

        public Task<ProcurementTenderDocumentRegisterDto> BindAsync(string correlation) =>
            Service.BindAsync(new BindProcurementTenderDocumentRegisterRequest
            {
                SourceType = ProcurementTenderDocumentSourceType.Tender,
                SourceId = Tender.Id,
                TemplateVersionId = TemplateId,
                SubmissionDeadlineUtc = Tender.SubmissionDeadline!.Value,
                OpeningScheduledAtUtc = Tender.OpeningDate,
                BidValidityUntilUtc = Tender.SubmissionDeadline.Value.AddDays(30),
                FeeMode = FeeMode,
                FeeAmount = FeeMode == ProcurementTenderDocumentFeeMode.Paid ? 25m : 0m,
                CurrencyCode = "GHS"
            }, correlation);

        public IssueProcurementTenderDocumentControlRequest Issue(string rowVersion, decimal amountPaid) => new()
        {
            SourceType = ProcurementTenderDocumentSourceType.Tender,
            SourceId = Tender.Id,
            BusinessPartnerId = Supplier.Id,
            RecipientName = Supplier.PartnerName,
            RecipientEmail = "supplier@example.test",
            AmountPaid = amountPaid,
            PaymentReference = FeeMode == ProcurementTenderDocumentFeeMode.Paid ? "PAY-001" : null,
            ReceiptNumber = "DOC-REC-001",
            IssueChannel = "Portal",
            EvidenceReference = "EVID-ISSUE-001",
            RegisterRowVersion = rowVersion
        };

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
