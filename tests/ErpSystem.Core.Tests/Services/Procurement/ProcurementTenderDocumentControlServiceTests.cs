using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    public async Task MetadataOnlyDraftCanStartItsExactApprovalWorkflowBeforeContentExists()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);

        var draft = await fixture.Service.CreateTemplateAsync(
            fixture.DraftTemplateRequest(), "create-metadata-draft");
        var submitted = await fixture.Service.SubmitTemplateAsync(draft.Id,
            Lifecycle(draft.RowVersion), "submit-metadata-draft");

        draft.Status.Should().Be(ProcurementTenderDocumentTemplateStatus.Draft);
        draft.ContentWorkflowEvidenceDocumentId.Should().BeNull();
        draft.BlockedReasons.Should().Contain(reason => reason.Contains("content", StringComparison.OrdinalIgnoreCase));
        submitted.Status.Should().Be(ProcurementTenderDocumentTemplateStatus.PendingApproval);
        submitted.WorkflowInstanceId.Should().NotBeNull();
        submitted.BlockedReasons.Should().Contain(reason => reason.Contains("exact approval workflow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PendingTemplateAcceptsOnlyVerifiedCleanContentFromItsExactWorkflowInstance()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var draft = await fixture.Service.CreateTemplateAsync(
            fixture.DraftTemplateRequest(), "create-content-draft");
        var submitted = await fixture.Service.SubmitTemplateAsync(draft.Id,
            Lifecycle(draft.RowVersion), "submit-content-draft");
        var definition = await fixture.Context.WorkflowDefinitions.SingleAsync(item =>
            item.Id == fixture.WorkflowDefinitionId);
        var workflow = new WorkflowInstance
        {
            Id = submitted.WorkflowInstanceId!.Value,
            TenantId = fixture.TenantId,
            WorkflowDefinitionId = definition.Id,
            EntityTypeId = definition.EntityTypeId,
            EntityId = submitted.Id,
            InitiatedById = Guid.NewGuid(),
            Status = WorkflowInstanceStatus.InProgress
        };
        var step = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            WorkflowInstanceId = workflow.Id,
            WorkflowStepId = Guid.NewGuid(),
            Status = WorkflowStepInstanceStatus.InProgress
        };
        var artifact = new WorkflowEvidenceDocument
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            StepInstanceId = step.Id,
            AttachmentId = Guid.NewGuid().ToString("N"),
            DocumentName = "Approved NCT tender document",
            FileName = "nct-tender.pdf",
            FilePath = "workflow/tdc/nct-tender.pdf",
            Sha256 = new string('a', 64),
            FileSizeBytes = 128,
            UploadedById = Guid.NewGuid(),
            DocumentOwnerId = Guid.NewGuid(),
            IsCurrent = true,
            VerificationStatus = WorkflowEvidenceVerificationStatus.Verified,
            MalwareScanStatus = WorkflowMalwareScanStatus.Clean,
            RetainUntil = DateTime.UtcNow.AddYears(7)
        };
        var unrelatedStep = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            WorkflowInstanceId = Guid.NewGuid(),
            WorkflowStepId = Guid.NewGuid(),
            Status = WorkflowStepInstanceStatus.InProgress
        };
        var unrelatedArtifact = new WorkflowEvidenceDocument
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            StepInstanceId = unrelatedStep.Id,
            AttachmentId = Guid.NewGuid().ToString("N"),
            FileName = "unrelated-pr.pdf",
            FilePath = "workflow/tdc/unrelated-pr.pdf",
            Sha256 = new string('b', 64),
            UploadedById = Guid.NewGuid(),
            DocumentOwnerId = Guid.NewGuid(),
            IsCurrent = true,
            VerificationStatus = WorkflowEvidenceVerificationStatus.Verified,
            MalwareScanStatus = WorkflowMalwareScanStatus.Clean,
            RetainUntil = DateTime.UtcNow.AddYears(7)
        };
        fixture.Context.AddRange(workflow, step, artifact, unrelatedStep, unrelatedArtifact);
        await fixture.Context.SaveChangesAsync();

        var unrelated = () => fixture.Service.AttachTemplateContentAsync(submitted.Id,
            new AttachProcurementTenderDocumentTemplateContentRequest
            {
                ContentWorkflowEvidenceDocumentId = unrelatedArtifact.Id,
                RowVersion = submitted.RowVersion
            }, "reject-unrelated-workflow-content");
        await unrelated.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_CONTENT_WORKFLOW_MISMATCH");

        var attached = await fixture.Service.AttachTemplateContentAsync(submitted.Id,
            new AttachProcurementTenderDocumentTemplateContentRequest
            {
                ContentWorkflowEvidenceDocumentId = artifact.Id,
                RowVersion = submitted.RowVersion
            }, "attach-exact-workflow-content");

        attached.ContentWorkflowEvidenceDocumentId.Should().Be(artifact.Id);
        attached.ContentReference.Should().Be(artifact.FilePath);
        attached.ContentChecksumSha256.Should().Be(artifact.Sha256);
        attached.BlockedReasons.Should().NotContain(reason =>
            reason.Contains("attach", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExactRecordedPolicyCanUseCompletedIndependentReviewInsteadOfSeparateVerification()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var scenario = await CreateConfiguredReviewAsync(fixture);

        var detail = await fixture.Service.GetTemplateAsync(scenario.Template.Id);
        detail.EligibleContentEvidenceDocumentIds.Should().ContainSingle().Which.Should().Be(scenario.Artifact.Id);
        var attached = await fixture.Service.AttachTemplateContentAsync(scenario.Template.Id,
            new AttachProcurementTenderDocumentTemplateContentRequest
            {
                ContentWorkflowEvidenceDocumentId = scenario.Artifact.Id,
                RowVersion = scenario.Template.RowVersion
            }, "attach-policy-reviewed-content");
        var published = await fixture.Service.PublishTemplateAsync(attached.Id,
            Lifecycle(attached.RowVersion), "publish-policy-reviewed-content");

        published.Status.Should().Be(ProcurementTenderDocumentTemplateStatus.Published);
        scenario.Artifact.VerificationStatus.Should().Be(WorkflowEvidenceVerificationStatus.Pending,
            "configured review must not fabricate a separate verification record");
        scenario.Policy.ApprovalConfiguration.Should().Be(WaivedReviewConfiguration,
            "published configuration must remain immutable");
    }

    [Theory]
    [InlineData("no-recorded-policy")]
    [InlineData("malformed-step-result")]
    [InlineData("malformed-policy")]
    [InlineData("verification-unspecified")]
    [InlineData("verification-required")]
    [InlineData("unrelated-requirement")]
    [InlineData("duplicate-requirement")]
    [InlineData("wrong-document-type")]
    [InlineData("multiple-documents")]
    [InlineData("foreign-policy")]
    [InlineData("unpublished-policy")]
    [InlineData("policy-not-effective")]
    [InlineData("unfinished-workflow")]
    [InlineData("self-approval")]
    [InlineData("approval-before-upload")]
    [InlineData("missing-actor")]
    [InlineData("no-approval")]
    [InlineData("missing-key")]
    [InlineData("unknown-verification")]
    [InlineData("missing-system-log")]
    [InlineData("forged-policy-result")]
    [InlineData("malformed-system-log")]
    [InlineData("wrong-system-log-code")]
    [InlineData("user-authored-log")]
    [InlineData("foreign-system-log")]
    [InlineData("log-after-approval")]
    [InlineData("duplicate-system-log")]
    [InlineData("ambiguous-policy-property")]
    [InlineData("ambiguous-result-property")]
    public async Task ConfiguredReviewFallsBackToStrictVerificationWhenAuthorityIsUnclear(string condition)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var scenario = await CreateConfiguredReviewAsync(fixture, condition);
        switch (condition)
        {
            case "no-recorded-policy": scenario.Step.ResultData = "{}"; break;
            case "malformed-step-result": scenario.Step.ResultData = "invalid"; break;
            case "malformed-policy": scenario.Policy.ApprovalConfiguration = "invalid"; break;
            case "verification-unspecified": scenario.Policy.ApprovalConfiguration =
                "{\"evidenceRequirements\":[{\"requirementKey\":\"TENDER_CONTENT\"}]}"; break;
            case "verification-required": scenario.Policy.ApprovalConfiguration =
                WaivedReviewConfiguration.Replace("false", "true"); break;
            case "unrelated-requirement": scenario.Artifact.RequirementKey = "OTHER"; break;
            case "duplicate-requirement": scenario.Policy.ApprovalConfiguration =
                "{\"evidenceRequirements\":[{\"requirementKey\":\"TENDER_CONTENT\",\"requireVerification\":false},{\"requirementKey\":\"TENDER_CONTENT\",\"requireVerification\":false}]}"; break;
            case "wrong-document-type": scenario.Artifact.DocumentType = "INVOICE"; break;
            case "multiple-documents": scenario.Policy.ApprovalConfiguration =
                WaivedReviewConfiguration.Replace("\"minimumDocuments\":1", "\"minimumDocuments\":2"); break;
            case "foreign-policy": scenario.Policy.TenantId = Guid.NewGuid(); break;
            case "unpublished-policy": scenario.Policy.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft; break;
            case "policy-not-effective": scenario.Policy.EffectiveFrom = DateTime.UtcNow.AddDays(1); break;
            case "unfinished-workflow": scenario.Workflow.Status = WorkflowInstanceStatus.InProgress; break;
            case "self-approval": scenario.Approval.ProcessedById = scenario.Artifact.UploadedById; break;
            case "approval-before-upload": scenario.Approval.ProcessedDate = scenario.Artifact.UploadedAt.AddMinutes(-1); break;
            case "missing-actor": scenario.Approval.ProcessedById = null; break;
            case "no-approval": scenario.Approval.Status = WorkflowApprovalStatus.Pending; break;
            case "missing-key": scenario.Artifact.RequirementKey = null; break;
            case "unknown-verification": scenario.Artifact.VerificationStatus = (WorkflowEvidenceVerificationStatus)99; break;
            case "forged-policy-result":
                scenario.Policy.ApprovalConfiguration = WaivedReviewConfiguration.Replace("false", "true");
                var forgedPolicy = new WorkflowApprovalPolicySet
                {
                    Id = Guid.NewGuid(), TenantId = fixture.TenantId, Code = "UNRELATED-WAIVER",
                    Name = "Unrelated policy", EntityType = "Procurement Sourcing",
                    EffectiveFrom = DateTime.UtcNow.AddDays(-1), LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                    ApprovalConfiguration = WaivedReviewConfiguration
                };
                fixture.Context.Add(forgedPolicy);
                scenario.Step.ResultData = JsonSerializer.Serialize(new { appliedApprovalPolicySetId = forgedPolicy.Id });
                break;
            case "duplicate-system-log": fixture.Context.Add(new WorkflowActivityLog
                {
                    Id = Guid.NewGuid(), TenantId = fixture.TenantId, WorkflowInstanceId = scenario.Workflow.Id,
                    StepInstanceId = scenario.Step.Id, ActivityType = WorkflowActivityType.DataUpdated,
                    Title = "Approval policy applied", Data = scenario.Activity.Data,
                    ActivityDate = scenario.Activity.ActivityDate
                }); break;
            case "ambiguous-policy-property": scenario.Policy.ApprovalConfiguration =
                WaivedReviewConfiguration.Replace("\"requireVerification\":false", "\"requireVerification\":true,\"RequireVerification\":false"); break;
            case "ambiguous-result-property": scenario.Step.ResultData =
                "{\"appliedApprovalPolicySetId\":\"" + scenario.Policy.Id + "\",\"AppliedApprovalPolicySetId\":\"" + scenario.Policy.Id + "\"}"; break;
        }
        await fixture.Context.SaveChangesAsync();

        (await fixture.Service.GetTemplateAsync(scenario.Template.Id))
            .EligibleContentEvidenceDocumentIds.Should().BeEmpty();
        var action = () => fixture.Service.AttachTemplateContentAsync(scenario.Template.Id,
            new AttachProcurementTenderDocumentTemplateContentRequest
            {
                ContentWorkflowEvidenceDocumentId = scenario.Artifact.Id,
                RowVersion = scenario.Template.RowVersion
            }, "reject-unclear-review-policy");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_CONTENT_NOT_APPROVED");
    }

    [Theory]
    [InlineData("pending-scan", "TENDER_DOCUMENT_CONTENT_NOT_APPROVED")]
    [InlineData("infected", "TENDER_DOCUMENT_CONTENT_SCAN_FAILED")]
    [InlineData("failed-scan", "TENDER_DOCUMENT_CONTENT_SCAN_FAILED")]
    [InlineData("rejected", "TENDER_DOCUMENT_CONTENT_VERIFICATION_REJECTED")]
    [InlineData("superseded", "TENDER_DOCUMENT_CONTENT_ARTIFACT_SUPERSEDED")]
    [InlineData("expired", "TENDER_DOCUMENT_CONTENT_ARTIFACT_EXPIRED")]
    [InlineData("unrelated-workflow", "TENDER_DOCUMENT_CONTENT_WORKFLOW_MISMATCH")]
    public async Task ConfiguredReviewNeverWaivesDocumentSafetyOrExactWorkflow(string condition, string expectedCode)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var scenario = await CreateConfiguredReviewAsync(fixture);
        switch (condition)
        {
            case "pending-scan": scenario.Artifact.MalwareScanStatus = WorkflowMalwareScanStatus.Pending; break;
            case "infected": scenario.Artifact.MalwareScanStatus = WorkflowMalwareScanStatus.Infected; break;
            case "failed-scan": scenario.Artifact.MalwareScanStatus = WorkflowMalwareScanStatus.Failed; break;
            case "rejected": scenario.Artifact.VerificationStatus = WorkflowEvidenceVerificationStatus.Rejected; break;
            case "superseded": scenario.Artifact.IsCurrent = false; break;
            case "expired": scenario.Artifact.ExpiryDate = DateTime.UtcNow.AddMinutes(-1); break;
            case "unrelated-workflow": scenario.Artifact.StepInstanceId = Guid.NewGuid(); break;
        }
        await fixture.Context.SaveChangesAsync();

        (await fixture.Service.GetTemplateAsync(scenario.Template.Id))
            .EligibleContentEvidenceDocumentIds.Should().BeEmpty();
        var action = () => fixture.Service.AttachTemplateContentAsync(scenario.Template.Id,
            new AttachProcurementTenderDocumentTemplateContentRequest
            {
                ContentWorkflowEvidenceDocumentId = scenario.Artifact.Id,
                RowVersion = scenario.Template.RowVersion
            }, "reject-unsafe-policy-content");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == expectedCode);
    }

    [Fact]
    public async Task ConfiguredReviewRechecksTheAttachedArtifactChecksumBeforePublication()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var scenario = await CreateConfiguredReviewAsync(fixture);
        var attached = await fixture.Service.AttachTemplateContentAsync(scenario.Template.Id,
            new AttachProcurementTenderDocumentTemplateContentRequest
            {
                ContentWorkflowEvidenceDocumentId = scenario.Artifact.Id,
                RowVersion = scenario.Template.RowVersion
            }, "attach-before-checksum-change");
        scenario.Artifact.Sha256 = new string('b', 64);
        await fixture.Context.SaveChangesAsync();

        var publish = () => fixture.Service.PublishTemplateAsync(attached.Id,
            Lifecycle(attached.RowVersion), "reject-changed-content-checksum");
        await publish.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_CHECKSUM_MISMATCH");
    }

    private const string WaivedReviewConfiguration =
        "{\"evidenceRequirements\":[{\"requirementKey\":\"TENDER_CONTENT\",\"documentType\":\"TENDER\",\"minimumDocuments\":1,\"requireVerification\":false}]}";

    private static async Task<(ProcurementTenderDocumentTemplateDto Template, WorkflowInstance Workflow,
        WorkflowStepInstance Step, WorkflowApproval Approval, WorkflowEvidenceDocument Artifact,
        WorkflowApprovalPolicySet Policy, WorkflowActivityLog Activity)> CreateConfiguredReviewAsync(
            Fixture fixture, string? logVariant = null)
    {
        var draft = await fixture.Service.CreateTemplateAsync(fixture.DraftTemplateRequest(), "draft-configured-review");
        var submitted = await fixture.Service.SubmitTemplateAsync(draft.Id,
            Lifecycle(draft.RowVersion), "submit-configured-review");
        var definition = await fixture.Context.WorkflowDefinitions.SingleAsync(item => item.Id == fixture.WorkflowDefinitionId);
        var workflow = new WorkflowInstance
        {
            Id = submitted.WorkflowInstanceId!.Value, TenantId = fixture.TenantId,
            WorkflowDefinitionId = definition.Id, EntityTypeId = definition.EntityTypeId,
            EntityId = submitted.Id, InitiatedById = Guid.NewGuid(),
            Status = WorkflowInstanceStatus.Completed, StartedDate = DateTime.UtcNow.AddMinutes(-10)
        };
        var policy = new WorkflowApprovalPolicySet
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Code = "TDC-TEMPLATE-REVIEW",
            Name = "Configured template review", EntityType = "Procurement Sourcing",
            EffectiveFrom = DateTime.UtcNow.AddDays(-1), LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
            ApprovalConfiguration = WaivedReviewConfiguration
        };
        var stepDefinition = new WorkflowStep
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, WorkflowDefinitionId = definition.Id,
            Name = "TDC configured approval", StepType = WorkflowStepType.Approval
        };
        var step = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, WorkflowInstanceId = workflow.Id,
            WorkflowStepId = stepDefinition.Id, Status = WorkflowStepInstanceStatus.Completed,
            CreatedDate = DateTime.UtcNow.AddMinutes(-8),
            ResultData = JsonSerializer.Serialize(new { appliedApprovalPolicySetId = policy.Id })
        };
        var artifact = new WorkflowEvidenceDocument
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, StepInstanceId = step.Id,
            AttachmentId = Guid.NewGuid().ToString("N"), RequirementKey = "TENDER_CONTENT", DocumentType = "TENDER",
            FileName = "tender.pdf", FilePath = "workflow/tdc/tender.pdf", Sha256 = new string('a', 64),
            IsCurrent = true, MalwareScanStatus = WorkflowMalwareScanStatus.Clean,
            VerificationStatus = WorkflowEvidenceVerificationStatus.Pending,
            UploadedById = Guid.NewGuid(), UploadedAt = DateTime.UtcNow.AddMinutes(-5), RetainUntil = DateTime.UtcNow.AddYears(7)
        };
        var approval = new WorkflowApproval
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, StepInstanceId = step.Id,
            Status = WorkflowApprovalStatus.Approved, ProcessedById = Guid.NewGuid(),
            ProcessedDate = DateTime.UtcNow.AddMinutes(-1), ApproverRole = "TDC_HEAD_OF_PROCUREMENT"
        };
        var activity = new WorkflowActivityLog
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, WorkflowInstanceId = workflow.Id,
            StepInstanceId = step.Id, ActivityType = WorkflowActivityType.DataUpdated,
            Title = "Approval policy applied", ActivityDate = DateTime.UtcNow.AddMinutes(-7),
            Data = JsonSerializer.Serialize(new { PolicySetId = policy.Id, PolicyCode = policy.Code })
        };
        // Activity logs are immutable after insertion. Arrange legacy/corrupt provenance before
        // the initial save, rather than weakening the production append-only protection.
        switch (logVariant)
        {
            case "malformed-system-log": activity.Data = "invalid"; break;
            case "wrong-system-log-code": activity.Data = JsonSerializer.Serialize(new
                { PolicySetId = policy.Id, PolicyCode = "OTHER" }); break;
            case "user-authored-log": activity.PerformedById = Guid.NewGuid(); break;
            case "foreign-system-log": activity.TenantId = Guid.NewGuid(); break;
            case "log-after-approval": activity.ActivityDate = DateTime.UtcNow.AddDays(1); break;
        }
        fixture.Context.AddRange(workflow, policy, stepDefinition, step, artifact, approval);
        if (logVariant != "missing-system-log")
            fixture.Context.Add(activity);
        await fixture.Context.SaveChangesAsync();
        return (submitted, workflow, step, approval, artifact, policy, activity);
    }

    private static ProcurementTenderDocumentTemplateLifecycleRequest Lifecycle(string rowVersion) => new()
    {
        RowVersion = rowVersion,
        Comment = "Start governed content approval.",
        Evidence =
        [
            new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                Reference = "UAT-F05B-CONTENT"
            }
        ]
    };

    [Fact]
    public async Task LegacyRfqMethodKeepsControlledIssueBeforeItsDispatchPublication()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free,
            ProcurementMethodType.RequestForQuotation, published: false);
        var register = await fixture.BindAsync("bind-legacy-rfq-prepub");
        var issued = await fixture.Service.IssueAsync(fixture.Issue(register.RowVersion, 0m), "issue-legacy-rfq-prepub");
        issued.BusinessPartnerId.Should().Be(fixture.Supplier.Id);
        register.AllowedActions.Should().Contain("Issue");
        fixture.SupplierValidation.Verify(item => item.ValidateForTenderAsync(fixture.Supplier.Id, false, null), Times.Once);
    }

    [Theory]
    [InlineData("Approved", false)]
    [InlineData("Approved", true)]
    [InlineData("Published", false)]
    [InlineData("Draft", false)]
    public async Task PublishedTemplateDoesNotAllowSupplierIssueBeforeActualTenderPublication(string status, bool publishDate)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        fixture.Tender.Status = status;
        fixture.Tender.PublishDate = publishDate ? DateTime.UtcNow.AddMinutes(-1) : null;
        await fixture.Context.SaveChangesAsync();
        var register = await fixture.BindAsync("bind-prepub-issue");
        var action = () => fixture.Service.IssueAsync(fixture.Issue(register.RowVersion, 0m), "issue-prepub");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_TENDER_NOT_PUBLISHED");
        register.IsSourcePublished.Should().BeFalse();
        register.AllowedActions.Should().NotContain("Issue");
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UnpublishedExpiredScheduleRequiresApprovalThenProjectsBothDatesWithoutReplacingOriginals()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        var register = await fixture.BindExpiredUnpublishedAsync();
        var originalDeadline = register.OriginalSubmissionDeadlineUtc;
        var originalOpening = register.OpeningScheduledAtUtc;
        var originalSnapshot = register.LifecycleSnapshotJson;
        var originalHash = register.IntegrityHash;
        var request = fixture.Reschedule(register);
        var created = await fixture.Service.CreateChangeAsync(request, "schedule-create");
        var replay = await fixture.Service.CreateChangeAsync(request, "schedule-create");
        replay.Id.Should().Be(created.Id);
        created.Status.Should().Be(ProcurementTenderDocumentChangeStatus.PendingApproval);
        created.RequiresAcknowledgement.Should().BeFalse();
        fixture.Tender.SubmissionDeadline.Should().Be(originalDeadline);
        fixture.Tender.OpeningDate.Should().Be(originalOpening);
        var pendingPublish = () => fixture.Service.EnsurePublicationReadyAsync(
            ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id, originalDeadline, "publish-pending-schedule");
        await pendingPublish.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>();

        await fixture.CompleteChangeWorkflowAndSwitchActorAsync(created);
        var approved = await fixture.Service.DecideChangeAsync(created.Id,
            new DecideProcurementTenderDocumentChangeRequest
            {
                Action = "Approve", ApprovalReference = "TDC-SCHEDULE-APPROVAL",
                RowVersion = created.RowVersion
            }, "schedule-approve");
        var effective = await fixture.Service.GetRegisterAsync(ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id);

        approved.Status.Should().Be(ProcurementTenderDocumentChangeStatus.Approved);
        approved.PreviousOpeningScheduledAtUtc.Should().Be(originalOpening);
        approved.NewOpeningScheduledAtUtc.Should().Be(request.NewOpeningScheduledAtUtc);
        effective.OriginalSubmissionDeadlineUtc.Should().Be(originalDeadline);
        effective.OriginalOpeningScheduledAtUtc.Should().Be(originalOpening);
        effective.EffectiveSubmissionDeadlineUtc.Should().Be(request.NewValueUtc!.Value);
        effective.OpeningScheduledAtUtc.Should().Be(request.NewOpeningScheduledAtUtc);
        fixture.Tender.SubmissionDeadline.Should().Be(request.NewValueUtc);
        fixture.Tender.OpeningDate.Should().Be(request.NewOpeningScheduledAtUtc);
        fixture.Tender.Status.Should().Be("Approved");
        fixture.Tender.PublishDate.Should().BeNull();
        register.LifecycleSnapshotJson.Should().Be(originalSnapshot);
        register.IntegrityHash.Should().Be(originalHash);
        (await fixture.Context.ProcurementTenderDocumentChanges.CountAsync()).Should().Be(1);
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
        var ready = await fixture.Service.EnsurePublicationReadyAsync(ProcurementTenderDocumentSourceType.Tender,
            fixture.Tender.Id, request.NewValueUtc.Value, "publish-approved-schedule");
        ready.Ready.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FirstBindingCanRequestScheduleApprovalWithoutChangingSourceDates(bool expired)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        fixture.Tender.SubmissionDeadline = DateTime.UtcNow.AddHours(expired ? -2 : 2);
        fixture.Tender.OpeningDate = fixture.Tender.SubmissionDeadline.Value.AddMinutes(5);
        await fixture.Context.SaveChangesAsync();
        var request = fixture.FirstBindingWithSchedule();
        var readiness = await fixture.Service.GetRegisterReadinessAsync(ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id);
        readiness.AllowedActions.Should().Contain("BindWithScheduleChange");
        readiness.OpeningScheduledAtUtc.Should().Be(fixture.Tender.OpeningDate);
        var original = fixture.Tender.SubmissionDeadline.Value;
        var opening = fixture.Tender.OpeningDate;
        var bound = await fixture.Service.BindAsync(request, "first-bind-schedule");
        var replay = await fixture.Service.BindAsync(request, "first-bind-schedule");
        replay.Id.Should().Be(bound.Id);
        bound.OriginalSubmissionDeadlineUtc.Should().Be(original);
        bound.EffectiveSubmissionDeadlineUtc.Should().Be(original);
        bound.BoundAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
        fixture.Tender.SubmissionDeadline.Should().Be(original);
        fixture.Tender.OpeningDate.Should().Be(opening);
        var change = bound.Changes.Should().ContainSingle().Subject;
        change.Status.Should().Be(ProcurementTenderDocumentChangeStatus.PendingApproval);
        change.NewValueUtc.Should().Be(request.ScheduleChange!.SubmissionDeadlineUtc);
        (await fixture.Context.ProcurementTenderDocumentRegisters.CountAsync()).Should().Be(1);
        (await fixture.Context.ProcurementTenderDocumentChanges.CountAsync()).Should().Be(1);
        var publish = () => fixture.Service.EnsurePublicationReadyAsync(ProcurementTenderDocumentSourceType.Tender,
            fixture.Tender.Id, original, "first-bind-publish-pending");
        await publish.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>();
        await fixture.CompleteChangeWorkflowAndSwitchActorAsync(change);
        await fixture.Service.DecideChangeAsync(change.Id,
            new DecideProcurementTenderDocumentChangeRequest { Action = "Approve", ApprovalReference = "FIRST-BIND-APPROVED", RowVersion = change.RowVersion },
            "first-bind-approve");
        var effective = await fixture.Service.GetRegisterAsync(ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id);
        effective.OriginalSubmissionDeadlineUtc.Should().Be(original);
        effective.EffectiveSubmissionDeadlineUtc.Should().Be(request.ScheduleChange.SubmissionDeadlineUtc);
        fixture.Tender.SubmissionDeadline.Should().Be(request.ScheduleChange.SubmissionDeadlineUtc);
        fixture.Tender.OpeningDate.Should().Be(request.ScheduleChange.OpeningScheduledAtUtc);
        fixture.Tender.Status.Should().Be("Approved");
        fixture.Tender.PublishDate.Should().BeNull();
    }

    [Fact]
    public async Task FirstBindingExpiredWithoutScheduleRequestRemainsRejected()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        fixture.Tender.SubmissionDeadline = DateTime.UtcNow.AddHours(-2);
        fixture.Tender.OpeningDate = DateTime.UtcNow.AddHours(-1);
        await fixture.Context.SaveChangesAsync();
        var action = () => fixture.BindAsync("no-schedule");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
            .Where(error => error.Code == "TENDER_DOCUMENT_DEADLINE_ELAPSED");
        (await fixture.Context.ProcurementTenderDocumentRegisters.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("published")]
    [InlineData("historically-published")]
    [InlineData("bid")]
    [InlineData("statutory")]
    [InlineData("prequalified")]
    [InlineData("qcbs")]
    public async Task FirstBindingScheduleRequestRejectsIneligibleSources(string variant)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        switch (variant)
        {
            case "published": fixture.Tender.Status = "Published"; fixture.Tender.PublishDate = DateTime.UtcNow; break;
            case "historically-published": fixture.Tender.PublishedById = Guid.NewGuid(); break;
            case "bid": fixture.Context.Add(new TenderBid { Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = fixture.Tender.Id, BusinessPartnerId = fixture.Supplier.Id }); break;
            case "statutory": fixture.Context.Add(new ProcurementTenderControl { Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = fixture.Tender.Id }); break;
            case "prequalified": fixture.Tender.RequiresPrequalification = true; break;
            case "qcbs": fixture.Tender.UseQCBSEvaluation = true; break;
        }
        await fixture.Context.SaveChangesAsync();
        var readiness = await fixture.Service.GetRegisterReadinessAsync(ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id);
        readiness.AllowedActions.Should().NotContain("BindWithScheduleChange");
        var action = () => fixture.Service.BindAsync(fixture.FirstBindingWithSchedule(), "first-bind-ineligible");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(error => error.Code == "TENDER_DOCUMENT_RESCHEDULE_NOT_ALLOWED");
        (await fixture.Context.ProcurementTenderDocumentRegisters.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("past")]
    [InlineData("opening-before-deadline")]
    [InlineData("beyond-validity")]
    [InlineData("source-mismatch")]
    public async Task FirstBindingScheduleRequestRejectsInvalidDatesBeforeWriting(string variant)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        var request = fixture.FirstBindingWithSchedule();
        switch (variant)
        {
            case "past": request.ScheduleChange!.SubmissionDeadlineUtc = DateTime.UtcNow.AddDays(-1); break;
            case "opening-before-deadline": request.ScheduleChange!.OpeningScheduledAtUtc = request.ScheduleChange.SubmissionDeadlineUtc.AddMinutes(-1); break;
            case "beyond-validity": request.BidValidityUntilUtc = request.ScheduleChange!.SubmissionDeadlineUtc.AddMinutes(-1); break;
            case "source-mismatch": request.SubmissionDeadlineUtc = request.SubmissionDeadlineUtc.AddMinutes(1); break;
        }
        var action = () => fixture.Service.BindAsync(request, "first-bind-invalid");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>();
        (await fixture.Context.ProcurementTenderDocumentRegisters.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("published")]
    [InlineData("historically-published")]
    [InlineData("prequalified")]
    [InlineData("bid")]
    [InlineData("issuance")]
    [InlineData("legacy-advertisement")]
    public async Task UnpublishedScheduleRejectsAnyExternalOrRestrictedHistory(string variant)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        var register = await fixture.BindExpiredUnpublishedAsync();
        switch (variant)
        {
            case "published": fixture.Tender.Status = "Published"; fixture.Tender.PublishDate = DateTime.UtcNow; break;
            case "historically-published": fixture.Tender.PublishedById = Guid.NewGuid(); break;
            case "prequalified": fixture.Tender.RequiresPrequalification = true; break;
            case "bid": fixture.Context.Add(new TenderBid
                { Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = fixture.Tender.Id, BusinessPartnerId = fixture.Supplier.Id }); break;
            case "issuance": fixture.Context.Add(new ProcurementTenderDocumentIssuance
                { Id = Guid.NewGuid(), TenantId = fixture.TenantId, RegisterId = register.Id, TemplateVersionId = fixture.TemplateId }); break;
            case "legacy-advertisement": fixture.Context.Add(new ProcurementTenderControl
                { Id = Guid.NewGuid(), TenantId = fixture.TenantId, TenderId = fixture.Tender.Id }); break;
        }
        await fixture.Context.SaveChangesAsync();
        var action = () => fixture.Service.CreateChangeAsync(fixture.Reschedule(register), "schedule-history-denied");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_RESCHEDULE_NOT_ALLOWED");
        (await fixture.Context.ProcurementTenderDocumentChanges.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("past-deadline")]
    [InlineData("opening-before-deadline")]
    [InlineData("missing-opening")]
    [InlineData("beyond-validity")]
    public async Task UnpublishedScheduleRejectsInvalidNewDates(string variant)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        var register = await fixture.BindExpiredUnpublishedAsync();
        var request = fixture.Reschedule(register);
        switch (variant)
        {
            case "past-deadline": request.NewValueUtc = DateTime.UtcNow.AddMinutes(-1); break;
            case "opening-before-deadline": request.NewOpeningScheduledAtUtc = request.NewValueUtc!.Value.AddMinutes(-1); break;
            case "missing-opening": request.NewOpeningScheduledAtUtc = null; break;
            case "beyond-validity": request.NewValueUtc = register.OriginalBidValidityUntilUtc.AddDays(1); request.NewOpeningScheduledAtUtc = request.NewValueUtc.Value.AddHours(1); break;
        }
        var action = () => fixture.Service.CreateChangeAsync(request, "schedule-invalid-dates");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_RESCHEDULE_DATES_INVALID");
    }

    [Theory]
    [InlineData("published")]
    [InlineData("source-drift")]
    public async Task UnpublishedScheduleRechecksSourceAtApproval(string variant)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        var register = await fixture.BindExpiredUnpublishedAsync();
        var created = await fixture.Service.CreateChangeAsync(fixture.Reschedule(register), "schedule-stale-create");
        await fixture.CompleteChangeWorkflowAndSwitchActorAsync(created);
        if (variant == "published") { fixture.Tender.Status = "Published"; fixture.Tender.PublishDate = DateTime.UtcNow; }
        else fixture.Tender.OpeningDate = fixture.Tender.OpeningDate!.Value.AddMinutes(10);
        await fixture.Context.SaveChangesAsync();
        var action = () => fixture.Service.DecideChangeAsync(created.Id,
            new DecideProcurementTenderDocumentChangeRequest { Action = "Approve", ApprovalReference = "STALE", RowVersion = created.RowVersion }, "schedule-stale-decide");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == (variant == "published"
                ? "TENDER_DOCUMENT_RESCHEDULE_NOT_ALLOWED" : "TENDER_DOCUMENT_SCHEDULE_BASE_STALE"));
    }

    [Fact]
    public async Task UnpublishedScheduleCannotApplyWithoutCompletedSharedWorkflow()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        var register = await fixture.BindExpiredUnpublishedAsync();
        var created = await fixture.Service.CreateChangeAsync(fixture.Reschedule(register), "schedule-not-completed");
        await fixture.CompleteChangeWorkflowAndSwitchActorAsync(created, WorkflowInstanceStatus.InProgress);
        var action = () => fixture.Service.DecideChangeAsync(created.Id,
            new DecideProcurementTenderDocumentChangeRequest { Action = "Approve", ApprovalReference = "INCOMPLETE", RowVersion = created.RowVersion }, "schedule-decide-incomplete");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_WORKFLOW_NOT_APPROVED");
        fixture.Tender.SubmissionDeadline.Should().Be(register.OriginalSubmissionDeadlineUtc);
    }

    [Fact]
    public async Task UnpublishedScheduleRejectsAnImmediatelyCompletedWorkflow()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        var register = await fixture.BindExpiredUnpublishedAsync();
        fixture.WorkflowStartStatus = WorkflowInstanceStatus.Completed;
        var action = () => fixture.Service.CreateChangeAsync(fixture.Reschedule(register), "schedule-instant-workflow");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_RESCHEDULE_APPROVAL_REQUIRED");
        fixture.Tender.SubmissionDeadline.Should().Be(register.OriginalSubmissionDeadlineUtc);
        fixture.Tender.OpeningDate.Should().Be(register.OpeningScheduledAtUtc);
    }

    [Fact]
    public async Task UnpublishedScheduleRetainsIndependentApproverEnforcement()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        var register = await fixture.BindExpiredUnpublishedAsync();
        var created = await fixture.Service.CreateChangeAsync(fixture.Reschedule(register), "schedule-sod-create");
        await fixture.CompleteChangeWorkflowAndSwitchActorAsync(created, switchActor: false);
        fixture.Sod.Setup(item => item.EnforceAsync(It.IsAny<ProcurementSodGuardRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSodGuardDecisionDto { Allowed = false, Message = "An independent approver is required." });
        var action = () => fixture.Service.DecideChangeAsync(created.Id,
            new DecideProcurementTenderDocumentChangeRequest { Action = "Approve", ApprovalReference = "SELF", RowVersion = created.RowVersion }, "schedule-sod-denied");
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlAuthorizationException>();
        fixture.Tender.SubmissionDeadline.Should().Be(register.OriginalSubmissionDeadlineUtc);
    }

    [Fact]
    public async Task RescheduleWritesOnlyScheduleFieldsAndNeverMarksPublicationOrImmutableRegisterGraphModified()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, published: false);
        var register = await fixture.BindExpiredUnpublishedAsync();
        fixture.CaptureTenderWrites = true;
        var created = await fixture.Service.CreateChangeAsync(fixture.Reschedule(register), "schedule-partial-write");
        await fixture.CompleteChangeWorkflowAndSwitchActorAsync(created);
        await fixture.Service.DecideChangeAsync(created.Id,
            new DecideProcurementTenderDocumentChangeRequest { Action = "Approve", ApprovalReference = "PARTIAL-WRITE", RowVersion = created.RowVersion }, "schedule-partial-approve");
        fixture.ModifiedTenderFields.Should().Contain("SubmissionDeadline").And.Contain("OpeningDate")
            .And.NotContain("Status").And.NotContain("PublishDate").And.NotContain("PublishedById")
            .And.NotContain("Title").And.NotContain("IsDeleted");
        fixture.ModifiedImmutableRegisterGraph.Should().BeFalse();
    }

    [Fact]
    public async Task OpenNctDocumentAccessDoesNotGrantBidAwardOrPurchaseOrderApproval()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        fixture.Supplier.ApprovalStatus = "Pending";
        fixture.Supplier.RegistrationStatus = "Pending";
        fixture.RejectSupplierEligibility();
        await fixture.Context.SaveChangesAsync();
        var register = await fixture.BindAsync("bind-open-document");

        var issued = await fixture.Service.IssueAsync(
            fixture.Issue(register.RowVersion, 0m), "issue-open-document");

        register.AllowsNewRecipient.Should().BeTrue();
        issued.BusinessPartnerId.Should().Be(fixture.Supplier.Id);
        issued.TemplateVersionId.Should().Be(register.EffectiveTemplateVersionId);
        fixture.Supplier.ApprovalStatus.Should().Be("Pending");
        fixture.Supplier.RegistrationStatus.Should().Be("Pending");
        fixture.Tender.Status.Should().Be("Published");
        fixture.SupplierValidation.VerifyNoOtherCalls();
        (await fixture.Context.Set<TenderInvitation>().CountAsync()).Should().Be(0);
        (await fixture.Context.Set<TenderBid>().CountAsync()).Should().Be(0);
        (await fixture.Context.Set<TenderAward>().CountAsync()).Should().Be(0);
        (await fixture.Context.Set<PurchaseOrder>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OpenNctAllowsNewInterestedRecipientWithoutCreatingSupplierApproval()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var unbound = await fixture.Service.GetRegisterReadinessAsync(
            ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id);
        var register = await fixture.BindAsync("bind-new-recipient");
        var request = fixture.NewRecipient(register.RowVersion);

        var issued = await fixture.Service.IssueAsync(request, "issue-new-recipient");
        var ready = await fixture.Service.GetRegisterReadinessAsync(
            ProcurementTenderDocumentSourceType.Tender, fixture.Tender.Id);

        unbound.AllowsNewRecipient.Should().BeTrue();
        ready.AllowsNewRecipient.Should().BeTrue();
        issued.BusinessPartnerId.Should().BeNull();
        issued.RecipientName.Should().Be("New Interested Supplier");
        issued.RecipientEmail.Should().Be("new.supplier@example.test");
        (await fixture.Context.Set<BusinessPartner>().CountAsync()).Should().Be(1);
        fixture.SupplierValidation.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("inactive", "TENDER_DOCUMENT_RECIPIENT_INELIGIBLE")]
    [InlineData("blacklisted", "TENDER_DOCUMENT_RECIPIENT_INELIGIBLE")]
    [InlineData("customer-only", "TENDER_DOCUMENT_RECIPIENT_INELIGIBLE")]
    [InlineData("foreign-tenant", "TENDER_DOCUMENT_RECIPIENT_NOT_FOUND")]
    [InlineData("deleted", "TENDER_DOCUMENT_RECIPIENT_NOT_FOUND")]
    public async Task OpenNctRetainsSavedSupplierSafetyChecks(string variant, string code)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        switch (variant)
        {
            case "inactive": fixture.Supplier.IsActive = false; break;
            case "blacklisted": fixture.Supplier.IsBlacklisted = true; break;
            case "customer-only": fixture.Supplier.PartnerType = "Customer"; break;
            case "foreign-tenant": fixture.Supplier.TenantId = Guid.NewGuid(); break;
            case "deleted": fixture.Supplier.IsDeleted = true; break;
        }
        await fixture.Context.SaveChangesAsync();
        var register = await fixture.BindAsync("bind-unsafe-recipient");

        var action = () => fixture.Service.IssueAsync(
            fixture.Issue(register.RowVersion, 0m), "issue-unsafe-recipient");

        if (code == "TENDER_DOCUMENT_RECIPIENT_NOT_FOUND")
            await action.Should().ThrowAsync<ProcurementTenderDocumentControlNotFoundException>()
                .Where(exception => exception.Code == code);
        else
            await action.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
                .Where(exception => exception.Code == code);
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
        fixture.SupplierValidation.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("primary-email")]
    [InlineData("contact-email")]
    [InlineData("name")]
    public async Task NewRecipientCannotHideKnownBlacklistedSupplierIdentity(string match)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        fixture.Supplier.IsBlacklisted = true;
        fixture.Supplier.PrimaryEmail = match == "primary-email" ? "  New.Supplier@Example.Test  " : null;
        if (match == "contact-email")
            fixture.Context.Add(new BusinessPartnerContact
            {
                Id = Guid.NewGuid(), TenantId = fixture.TenantId,
                BusinessPartnerId = fixture.Supplier.Id, ContactName = "Supplier Contact",
                Email = "  New.Supplier@Example.Test  "
            });
        if (match == "name") fixture.Supplier.PartnerName = "  NEW INTERESTED SUPPLIER  ";
        await fixture.Context.SaveChangesAsync();
        var register = await fixture.BindAsync("bind-known-recipient");

        var action = () => fixture.Service.IssueAsync(
            fixture.NewRecipient(register.RowVersion), "issue-known-without-id");

        await action.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_SAVED_RECIPIENT_REQUIRED");
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("foreign-tenant")]
    [InlineData("deleted")]
    [InlineData("deleted-contact")]
    [InlineData("foreign-contact")]
    public async Task NewRecipientIdentityLookupDoesNotUseForeignOrDeletedRecords(string variant)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        if (variant is "foreign-tenant" or "deleted")
        {
            fixture.Supplier.PrimaryEmail = "new.supplier@example.test";
            if (variant == "foreign-tenant") fixture.Supplier.TenantId = Guid.NewGuid();
            else fixture.Supplier.IsDeleted = true;
        }
        else
        {
            fixture.Context.Add(new BusinessPartnerContact
            {
                Id = Guid.NewGuid(),
                TenantId = variant == "foreign-contact" ? Guid.NewGuid() : fixture.TenantId,
                BusinessPartnerId = fixture.Supplier.Id, ContactName = "Unused Contact",
                Email = "new.supplier@example.test", IsDeleted = variant == "deleted-contact"
            });
        }
        await fixture.Context.SaveChangesAsync();
        var register = await fixture.BindAsync("bind-unrelated-recipient");

        var issued = await fixture.Service.IssueAsync(
            fixture.NewRecipient(register.RowVersion), "issue-unrelated-recipient");

        issued.BusinessPartnerId.Should().BeNull();
    }

    [Theory]
    [InlineData(ProcurementMethodType.NationalCompetitiveTendering, true, false)]
    [InlineData(ProcurementMethodType.NationalCompetitiveTendering, false, true)]
    [InlineData(ProcurementMethodType.RestrictedTendering, false, false)]
    [InlineData(ProcurementMethodType.RequestForQuotation, false, false)]
    [InlineData(ProcurementMethodType.InternationalCompetitiveTendering, false, false)]
    [InlineData(ProcurementMethodType.SingleSource, false, false)]
    [InlineData(ProcurementMethodType.QualityBasedSelection, false, false)]
    [InlineData(ProcurementMethodType.QualityAndCostBasedSelection, false, false)]
    public async Task OtherOrPrequalifiedRoutesRetainEligibilityAndRequireSavedRecipient(
        ProcurementMethodType method, bool requiresPrequalification, bool useQcbs)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, method);
        fixture.Tender.RequiresPrequalification = requiresPrequalification;
        fixture.Tender.UseQCBSEvaluation = useQcbs;
        fixture.Tender.MinimumPerformanceRating = 4m;
        fixture.RejectSupplierEligibility();
        await fixture.Context.SaveChangesAsync();
        var register = await fixture.BindAsync("bind-restricted-recipient");

        var known = () => fixture.Service.IssueAsync(
            fixture.Issue(register.RowVersion, 0m), "issue-restricted-known");
        var unlinked = () => fixture.Service.IssueAsync(
            fixture.NewRecipient(register.RowVersion), "issue-restricted-new");

        register.AllowsNewRecipient.Should().BeFalse();
        await known.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_RECIPIENT_INELIGIBLE");
        await unlinked.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_SAVED_RECIPIENT_REQUIRED");
        fixture.SupplierValidation.Verify(item => item.ValidateForTenderAsync(
            fixture.Supplier.Id, requiresPrequalification, 4m), Times.Once);
        fixture.SupplierValidation.VerifyNoOtherCalls();
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UnknownAuthoritativeMethodCannotUseTenderDisplayLabelToEnableDocumentAccess()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free, (ProcurementMethodType)999);
        var register = await fixture.BindAsync("bind-unknown-method");
        fixture.Tender.TenderType.Should().Be("NCT");

        var action = () => fixture.Service.IssueAsync(
            fixture.Issue(register.RowVersion, 0m), "issue-unknown-method");

        register.AllowsNewRecipient.Should().BeFalse();
        await action.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_METHOD_UNSUPPORTED");
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DocumentIssueRejectsStaleRegisterMethodLineage()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var register = await fixture.BindAsync("bind-stale-lineage");
        var stored = await fixture.Context.ProcurementTenderDocumentRegisters.SingleAsync();
        stored.Method = ProcurementMethodType.RestrictedTendering;
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.IssueAsync(
            fixture.Issue(register.RowVersion, 0m), "issue-stale-lineage");

        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_REGISTER_LINEAGE_STALE");
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("missing-email", "TENDER_DOCUMENT_RECIPIENT_CONTACT_REQUIRED")]
    [InlineData("invalid-email", "TENDER_DOCUMENT_RECIPIENT_EMAIL_INVALID")]
    [InlineData("paid-fee", "TENDER_DOCUMENT_FEE_MISMATCH")]
    [InlineData("free-payment", "TENDER_DOCUMENT_FREE_PAYMENT_REFERENCE_INVALID")]
    public async Task NewRecipientRetainsContactAndFeeChecks(string variant, string code)
    {
        await using var fixture = new Fixture(variant == "paid-fee"
            ? ProcurementTenderDocumentFeeMode.Paid : ProcurementTenderDocumentFeeMode.Free);
        var register = await fixture.BindAsync("bind-new-recipient-controls");
        var request = fixture.NewRecipient(register.RowVersion);
        if (variant == "missing-email") request.RecipientEmail = null;
        if (variant == "invalid-email") request.RecipientEmail = "not-an-email";
        if (variant == "free-payment") request.PaymentReference = "NOT-A-FREE-ISSUE";

        var action = () => fixture.Service.IssueAsync(request, "issue-new-recipient-controls");

        await action.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
            .Where(exception => exception.Code == code);
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("elapsed", "TENDER_DOCUMENT_ISSUE_WINDOW_CLOSED")]
    [InlineData("row-version", "TENDER_DOCUMENT_REGISTER_VERSION_CONFLICT")]
    public async Task OpenDocumentAccessRetainsDeadlineAndConcurrencyGuards(string variant, string code)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free);
        var register = await fixture.BindAsync("bind-open-guards");
        var request = fixture.NewRecipient(register.RowVersion);
        if (variant == "elapsed")
        {
            var stored = await fixture.Context.ProcurementTenderDocumentRegisters.SingleAsync();
            stored.OriginalSubmissionDeadlineUtc = DateTime.UtcNow.AddMinutes(-1);
            await fixture.Context.SaveChangesAsync();
        }
        else request.RegisterRowVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        var action = () => fixture.Service.IssueAsync(request, "issue-open-guards");

        await action.Should().ThrowAsync<ProcurementTenderDocumentControlConflictException>()
            .Where(exception => exception.Code == code);
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RfqPreservesOnlyItsConfiguredEmailRecipientsAndHidesThemFromOtherSuppliers()
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free,
            ProcurementMethodType.RequestForQuotation);
        var (rfq, register) = await fixture.BindRfqAsync(
            "  New.Supplier@Example.Test ; other@example.test\nnew.supplier@example.test;not-an-email");
        var known = fixture.Issue(register.RowVersion, 0m);
        known.SourceType = ProcurementTenderDocumentSourceType.RequestForQuotation;
        known.SourceId = rfq.Id;
        await fixture.Service.IssueAsync(known, "issue-rfq-known");
        var request = fixture.NewRecipient(register.RowVersion);
        request.SourceType = ProcurementTenderDocumentSourceType.RequestForQuotation;
        request.SourceId = rfq.Id;
        request.ReceiptNumber = "RFQ-EXTERNAL-RECEIPT";

        var issued = await fixture.Service.IssueAsync(request, "issue-rfq-configured-email");

        register.AllowsNewRecipient.Should().BeFalse();
        register.AllowedExternalRecipientEmails.Should().BeEquivalentTo(
            ["new.supplier@example.test", "other@example.test"]);
        issued.BusinessPartnerId.Should().BeNull();
        issued.RecipientEmail.Should().Be("new.supplier@example.test");
        fixture.SupplierValidation.Verify(item => item.ValidateForRfqAsync(
            fixture.Supplier.Id, null, null), Times.Once);
        fixture.SupplierValidation.VerifyNoOtherCalls();

        await fixture.SwitchToExternalAsync();
        var external = await fixture.Service.GetRegisterAsync(
            ProcurementTenderDocumentSourceType.RequestForQuotation, rfq.Id);
        var externalReadiness = await fixture.Service.GetRegisterReadinessAsync(
            ProcurementTenderDocumentSourceType.RequestForQuotation, rfq.Id);
        external.AllowsNewRecipient.Should().BeFalse();
        external.AllowedExternalRecipientEmails.Should().BeEmpty();
        externalReadiness.AllowedExternalRecipientEmails.Should().BeEmpty();
        external.Issuances.Should().ContainSingle(item => item.BusinessPartnerId == fixture.Supplier.Id);
    }

    [Theory]
    [InlineData("unknown-email", "TENDER_DOCUMENT_SAVED_RECIPIENT_REQUIRED")]
    [InlineData("known-blacklisted-alias", "TENDER_DOCUMENT_SAVED_RECIPIENT_REQUIRED")]
    [InlineData("supplier-eligibility", "TENDER_DOCUMENT_RECIPIENT_INELIGIBLE")]
    public async Task RfqRecipientsCannotBypassSelectionOrExistingEligibility(string variant, string code)
    {
        await using var fixture = new Fixture(ProcurementTenderDocumentFeeMode.Free,
            ProcurementMethodType.RequestForQuotation);
        if (variant == "known-blacklisted-alias")
        {
            fixture.Supplier.IsBlacklisted = true;
            fixture.Supplier.PrimaryEmail = "  NEW.SUPPLIER@example.test  ";
            await fixture.Context.SaveChangesAsync();
        }
        fixture.RejectSupplierEligibility();
        var (rfq, register) = await fixture.BindRfqAsync(variant == "unknown-email"
            ? "another.selected@example.test" : "new.supplier@example.test");
        var request = variant == "supplier-eligibility"
            ? fixture.Issue(register.RowVersion, 0m) : fixture.NewRecipient(register.RowVersion);
        request.SourceType = ProcurementTenderDocumentSourceType.RequestForQuotation;
        request.SourceId = rfq.Id;

        var action = () => fixture.Service.IssueAsync(request, "issue-rfq-recipient-checks");

        await action.Should().ThrowAsync<ProcurementTenderDocumentControlValidationException>()
            .Where(exception => exception.Code == code);
        if (variant == "supplier-eligibility")
            fixture.SupplierValidation.Verify(item => item.ValidateForRfqAsync(
                fixture.Supplier.Id, null, null), Times.Once);
        fixture.SupplierValidation.VerifyNoOtherCalls();
        (await fixture.Context.ProcurementTenderDocumentIssuances.CountAsync()).Should().Be(0);
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

        public Fixture(ProcurementTenderDocumentFeeMode feeMode,
            ProcurementMethodType method = ProcurementMethodType.NationalCompetitiveTendering,
            bool published = true)
        {
            TenantId = Guid.NewGuid();
            _tenantId = TenantId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .AddInterceptors(new TenderWriteCaptureInterceptor(this))
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
                Method = method,
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
                Status = published ? "Published" : "Approved",
                PublishDate = published ? DateTime.UtcNow.AddMinutes(-1) : null,
                PublishedById = published ? Guid.NewGuid() : null,
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
                _external ? ["Supplier"] : ["TenantAdmin", "TDC_PROCUREMENT_OFFICER"]);
            current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => !_external && role == "TenantAdmin");
            _unitOfWork = new UnitOfWork(Context);
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Code = "ACCESS_ALLOWED",
                    Message = "Allowed"
                });
            var sod = new Mock<IProcurementSodGuardService>();
            Sod = sod;
            sod.Setup(item => item.EnforceAsync(It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto { Allowed = true });
            var workflow = new Mock<IWorkflowInstanceService>();
            workflow.Setup(item => item.StartWorkflowAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(),
                    It.IsAny<object?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new WorkflowInstance { Id = Guid.NewGuid(), TenantId = TenantId, Status = WorkflowStartStatus });
            SupplierValidation = new Mock<ISupplierValidationService>();
            SupplierValidation.Setup(item => item.ValidateForTenderAsync(
                    It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<decimal?>()))
                .ReturnsAsync(new SupplierValidationResult { IsValid = true });
            SupplierValidation.Setup(item => item.ValidateForRfqAsync(
                    It.IsAny<Guid>(), It.IsAny<List<Guid>?>(), It.IsAny<decimal?>()))
                .ReturnsAsync(new SupplierValidationResult { IsValid = true });
            var notifications = new Mock<INotificationTopicPublisher>();
            var events = new ProcurementControlEventService(
                _unitOfWork, current.Object, NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementTenderDocumentControlService(
                _unitOfWork, current.Object, access.Object, sod.Object, events,
                workflow.Object, SupplierValidation.Object, notifications.Object,
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
        public Mock<ISupplierValidationService> SupplierValidation { get; }
        public Mock<IProcurementSodGuardService> Sod { get; }
        public WorkflowInstanceStatus WorkflowStartStatus { get; set; } = WorkflowInstanceStatus.Created;
        public bool CaptureTenderWrites { get; set; }
        public HashSet<string> ModifiedTenderFields { get; } = new();
        public bool ModifiedImmutableRegisterGraph { get; set; }

        private sealed class TenderWriteCaptureInterceptor(Fixture fixture) : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
                InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (fixture.CaptureTenderWrites && eventData.Context is { } context)
                {
                    context.ChangeTracker.DetectChanges();
                    foreach (var entry in context.ChangeTracker.Entries<Tender>())
                        foreach (var property in entry.Properties.Where(item => item.IsModified))
                            fixture.ModifiedTenderFields.Add(property.Metadata.Name);
                    fixture.ModifiedImmutableRegisterGraph |= context.ChangeTracker.Entries().Any(entry =>
                        entry.State == EntityState.Modified && entry.Entity is ProcurementTenderDocumentRegister or ProcurementTenderDocumentIssuance);
                }
                return ValueTask.FromResult(result);
            }
        }

        public void RejectSupplierEligibility()
        {
            SupplierValidation.Setup(item => item.ValidateForTenderAsync(
                    It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<decimal?>()))
                .ReturnsAsync(new SupplierValidationResult
                {
                    IsValid = false,
                    Errors = ["Supplier due diligence and approved-vendor clearance are required."]
                });
            SupplierValidation.Setup(item => item.ValidateForRfqAsync(
                    It.IsAny<Guid>(), It.IsAny<List<Guid>?>(), It.IsAny<decimal?>()))
                .ReturnsAsync(new SupplierValidationResult
                {
                    IsValid = false,
                    Errors = ["Supplier due diligence and approved-vendor clearance are required."]
                });
        }

        public SaveProcurementTenderDocumentTemplateRequest DraftTemplateRequest() => new()
        {
            TemplateCode = "TDC-NCT-DRAFT",
            Name = "Standard NCT tender document",
            DocumentTypeCode = "TENDER-DOCUMENT",
            EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
            PolicySetId = Policy.Id,
            PolicySetCode = Policy.Code,
            PolicySetVersion = Policy.Version,
            SourceConfigurationProfileId = Profile.Id,
            WorkflowDefinitionId = WorkflowDefinitionId,
            ApplicableMethods = [ProcurementMethodType.NationalCompetitiveTendering]
        };

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

        public BindProcurementTenderDocumentRegisterRequest FirstBindingWithSchedule() => new()
        {
            SourceType = ProcurementTenderDocumentSourceType.Tender, SourceId = Tender.Id,
            TemplateVersionId = TemplateId, SubmissionDeadlineUtc = Tender.SubmissionDeadline!.Value,
            OpeningScheduledAtUtc = Tender.OpeningDate, BidValidityUntilUtc = DateTime.UtcNow.AddDays(90),
            FeeMode = FeeMode, CurrencyCode = "GHS",
            ScheduleChange = new()
            {
                SubmissionDeadlineUtc = DateTime.UtcNow.AddDays(35), OpeningScheduledAtUtc = DateTime.UtcNow.AddDays(35).AddHours(1),
                WorkflowDefinitionId = WorkflowDefinitionId, Reason = "Document preparation delayed publication",
                EvidenceReference = "UAT-FIRST-BIND-SCHEDULE"
            }
        };

        public async Task<(RequestForQuotation Rfq, ProcurementTenderDocumentRegisterDto Register)> BindRfqAsync(
            string externalRecipientEmails)
        {
            var rfq = new RequestForQuotation
            {
                Id = Guid.NewGuid(), TenantId = TenantId, RfqNumber = "RFQ-001",
                Title = "Selected RFQ recipients", Status = "Draft",
                SourcingCaseId = Tender.SourcingCaseId, Currency = "GHS",
                EstimatedValue = 1000m, SubmissionDeadline = Tender.SubmissionDeadline,
                ExternalRecipientEmails = externalRecipientEmails
            };
            Context.Add(rfq);
            await Context.SaveChangesAsync();
            var register = await Service.BindAsync(new BindProcurementTenderDocumentRegisterRequest
            {
                SourceType = ProcurementTenderDocumentSourceType.RequestForQuotation,
                SourceId = rfq.Id, TemplateVersionId = TemplateId,
                SubmissionDeadlineUtc = rfq.SubmissionDeadline!.Value,
                BidValidityUntilUtc = rfq.SubmissionDeadline.Value.AddDays(30),
                FeeMode = FeeMode, FeeAmount = 0m, CurrencyCode = "GHS"
            }, "bind-rfq");
            return (rfq, register);
        }

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

        public IssueProcurementTenderDocumentControlRequest NewRecipient(string rowVersion)
        {
            var request = Issue(rowVersion, 0m);
            request.BusinessPartnerId = null;
            request.RecipientName = "New Interested Supplier";
            request.RecipientEmail = "new.supplier@example.test";
            return request;
        }

        public async Task<ProcurementTenderDocumentRegister> BindExpiredUnpublishedAsync()
        {
            var bound = await BindAsync("bind-unpublished-schedule");
            var register = await Context.ProcurementTenderDocumentRegisters.SingleAsync(item => item.Id == bound.Id);
            Tender.SubmissionDeadline = DateTime.UtcNow.AddHours(-2);
            Tender.OpeningDate = DateTime.UtcNow.AddHours(-1);
            register.OriginalSubmissionDeadlineUtc = Tender.SubmissionDeadline.Value;
            register.OpeningScheduledAtUtc = Tender.OpeningDate;
            await Context.SaveChangesAsync();
            return register;
        }

        public CreateProcurementTenderDocumentChangeRequest Reschedule(ProcurementTenderDocumentRegister register) => new()
        {
            SourceType = ProcurementTenderDocumentSourceType.Tender, SourceId = Tender.Id,
            ChangeType = ProcurementTenderDocumentChangeType.UnpublishedScheduleReschedule,
            NewValueUtc = DateTime.UtcNow.AddDays(2), NewOpeningScheduledAtUtc = DateTime.UtcNow.AddDays(2).AddHours(1),
            Reason = "Reschedule an unpublished tender; no bidders have received documents.",
            WorkflowDefinitionId = WorkflowDefinitionId, EvidenceReference = "SCHEDULE-REVIEW",
            RegisterRowVersion = Convert.ToBase64String(register.RowVersion)
        };

        public async Task CompleteChangeWorkflowAndSwitchActorAsync(ProcurementTenderDocumentChangeDto change,
            WorkflowInstanceStatus status = WorkflowInstanceStatus.Completed, bool switchActor = true)
        {
            var definition = await Context.WorkflowDefinitions.SingleAsync(item => item.Id == WorkflowDefinitionId);
            Context.Add(new WorkflowInstance
            {
                Id = change.WorkflowInstanceId!.Value, TenantId = TenantId, WorkflowDefinitionId = definition.Id,
                EntityTypeId = definition.EntityTypeId, EntityId = change.Id, InitiatedById = _userId, Status = status
            });
            await Context.SaveChangesAsync();
            if (switchActor) _userId = Guid.NewGuid();
        }

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
