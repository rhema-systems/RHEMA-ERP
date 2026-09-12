using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ApPaymentPostingMigrationTests
{
    [Fact]
    public async Task PaymentEvidence_NoWorkflowAcceptsCleanPaymentOwnedDocumentWithoutHumanVerification()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var files = new Mock<ICentralDocumentRepositoryFileService>();
        var evidence = await SeedDirectPaymentEvidenceAsync(db, fixture.Payment, files);
        var workflow = OptionalPaymentWorkflow();
        var (service, _) = CreateService(db, tenant, workflowService: workflow.Object,
            approvalPolicyResolver: OptionalPaymentPolicy(RequiredPaymentEvidencePolicy()).Object,
            useRealPaymentSod: true, paymentEvidenceFiles: files.Object);

        var control = await service.GetControlAsync(fixture.Payment.Id);

        control!.ApprovalRequired.Should().BeFalse();
        control.CanSubmit.Should().BeTrue();
        control.EvidenceRequirementsSatisfied.Should().BeTrue();
        var requirement = control.EvidenceRequirements.Should().ContainSingle().Subject;
        requirement.RequireVerification.Should().BeFalse();
        requirement.CurrentDocumentCount.Should().Be(1);
        requirement.VerifiedDocumentCount.Should().Be(0, "no person has verified this attachment");
        var document = control.EvidenceDocuments.Should().ContainSingle().Subject;
        document.VerificationStatus.Should().Be("NotRequired");
        document.VerifiedById.Should().BeNull();
        document.VerifiedAt.Should().BeNull();

        var submitted = await service.SubmitAsync(fixture.Payment.Id, new SubmitVendorPaymentDto());
        submitted.ApprovalRequired.Should().BeFalse();
        submitted.AuthorizedById.Should().BeNull();
        submitted.WorkflowInstanceId.Should().BeNull();
        var posted = await service.PostAsync(fixture.Payment.Id);
        posted.Status.Should().Be(VendorPaymentStatus.Processed);
        posted.JournalEntryId.Should().NotBeNull();
        var journal = await db.JournalEntries.SingleAsync(item => item.Id == posted.JournalEntryId);
        journal.TotalDebitAmount.Should().Be(100m);
        journal.TotalCreditAmount.Should().Be(100m);
        journal.IsBalanced.Should().BeTrue();
        files.Verify(item => item.OpenAsync(tenant, evidence.Document.Id, evidence.Version.Id,
            It.IsAny<CancellationToken>()), Times.AtLeast(2), "completion and posting both revalidate the actual file");
        workflow.Verify(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never());

        // A later retention/availability change must not turn a durable posting
        // replay into a new posting or retroactively reject its historical result.
        evidence.Document.ExpiryDate = DateTime.UtcNow.AddSeconds(-1);
        SetDirectPaymentEvidenceContent(files, evidence, null);
        await db.SaveChangesAsync();
        var postedReplay = await service.PostAsync(fixture.Payment.Id);
        postedReplay.JournalEntryId.Should().Be(posted.JournalEntryId);
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentType == "VendorPayment" &&
            item.SourceDocumentId == fixture.Payment.Id && item.PostingStatus == "Posted")).Should().Be(1);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("zero-minimum-still-needs-one")]
    [InlineData("minimum-two")]
    [InlineData("wrong-payment")]
    [InlineData("foreign-link-tenant")]
    [InlineData("foreign-document-tenant")]
    [InlineData("foreign-version-tenant")]
    [InlineData("foreign-upload-tenant")]
    [InlineData("wrong-source")]
    [InlineData("wrong-module")]
    [InlineData("wrong-source-type")]
    [InlineData("stale-version")]
    [InlineData("draft-version")]
    [InlineData("deleted-link")]
    [InlineData("deleted-upload")]
    [InlineData("expired-link")]
    [InlineData("expired-document")]
    [InlineData("infected")]
    [InlineData("scan-skipped")]
    [InlineData("missing-storage")]
    [InlineData("changed-bytes")]
    public async Task PaymentEvidence_NoWorkflowFailsClosedForMissingOrInvalidCurrentAttachment(string scenario)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var files = new Mock<ICentralDocumentRepositoryFileService>();
        if (scenario is not "missing" and not "zero-minimum-still-needs-one")
        {
            var evidence = await SeedDirectPaymentEvidenceAsync(db, fixture.Payment, files);
            InvalidateDirectPaymentEvidence(evidence, files, scenario);
            await db.SaveChangesAsync();
        }
        var minimum = scenario == "minimum-two" ? 2 : scenario == "zero-minimum-still-needs-one" ? 0 : 1;
        var (service, _) = CreateService(db, tenant, workflowService: OptionalPaymentWorkflow().Object,
            approvalPolicyResolver: OptionalPaymentPolicy(RequiredPaymentEvidencePolicy(minimum)).Object,
            useRealPaymentSod: true, paymentEvidenceFiles: files.Object);

        // A bad document is a user-correctable unsatisfied requirement, not a 500
        // in the control preview and never permission to use an approval-free bypass.
        var control = await service.GetControlAsync(fixture.Payment.Id);
        control!.ApprovalRequired.Should().BeFalse();
        control.EvidenceRequirementsSatisfied.Should().BeFalse();
        control.CanSubmit.Should().BeFalse();
        control.EvidenceRequirements.Should().ContainSingle().Which.MinimumDocuments.Should().Be(Math.Max(minimum, 1));
        await ((Func<Task>)(async () => await service.SubmitAsync(fixture.Payment.Id, new SubmitVendorPaymentDto())))
            .Should().ThrowAsync<InvalidOperationException>();
        db.ChangeTracker.Clear();
        var retained = await db.Set<VendorPayment>().SingleAsync(item => item.Id == fixture.Payment.Id);
        retained.Status.Should().Be(VendorPaymentStatus.Draft);
        retained.ApprovalRequired.Should().BeTrue();
        retained.AuthorizedById.Should().BeNull();
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentType == "VendorPayment")).Should().Be(0);
    }

    [Theory]
    [InlineData("expired-link")]
    [InlineData("expired-document")]
    [InlineData("stale-version")]
    [InlineData("infected")]
    [InlineData("deleted-upload")]
    [InlineData("wrong-source")]
    [InlineData("changed-bytes")]
    [InlineData("missing-storage")]
    public async Task PaymentEvidence_PostRevalidatesTheCurrentAttachmentAndBytes(string scenario)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var files = new Mock<ICentralDocumentRepositoryFileService>();
        var evidence = await SeedDirectPaymentEvidenceAsync(db, fixture.Payment, files);
        var (service, _) = CreateService(db, tenant, workflowService: OptionalPaymentWorkflow().Object,
            approvalPolicyResolver: OptionalPaymentPolicy(RequiredPaymentEvidencePolicy()).Object,
            useRealPaymentSod: true, paymentEvidenceFiles: files.Object);
        await service.SubmitAsync(fixture.Payment.Id, new SubmitVendorPaymentDto());
        InvalidateDirectPaymentEvidence(evidence, files, scenario);
        await db.SaveChangesAsync();

        await ((Func<Task>)(async () => await service.PostAsync(fixture.Payment.Id)))
            .Should().ThrowAsync<InvalidOperationException>();

        db.ChangeTracker.Clear();
        var retained = await db.Set<VendorPayment>().SingleAsync(item => item.Id == fixture.Payment.Id);
        retained.Status.Should().Be(VendorPaymentStatus.Authorized);
        retained.ApprovalRequired.Should().BeFalse();
        retained.AuthorizedById.Should().BeNull();
        retained.JournalEntryId.Should().BeNull();
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentType == "VendorPayment")).Should().Be(0);
        (await db.JournalEntries.CountAsync(item => item.SourceDocumentType == "VendorPayment")).Should().Be(0);
    }

    [Theory]
    [InlineData("unverified", false)]
    [InlineData("self-verified", false)]
    [InlineData("independently-verified", true)]
    public async Task PaymentEvidence_ActiveWorkflowKeepsItsIndependentVerificationGate(string verification, bool satisfied)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var workflow = OptionalPaymentWorkflow(active: true);
        var instanceId = Guid.NewGuid();
        workflow.Setup(item => item.StartApprovalWorkflowAsync("VendorPayment", fixture.Payment.Id))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, WorkflowInstanceId = instanceId });
        var (service, _) = CreateService(db, tenant, workflowService: workflow.Object,
            approvalPolicyResolver: OptionalPaymentPolicy(RequiredPaymentEvidencePolicy()).Object);
        await service.SubmitAsync(fixture.Payment.Id, new SubmitVendorPaymentDto());

        var definitionId = Guid.NewGuid();
        var instance = new WorkflowInstance
        {
            Id = instanceId, TenantId = tenant, EntityId = fixture.Payment.Id,
            EntityTypeId = Guid.NewGuid(), WorkflowDefinitionId = definitionId,
            Status = WorkflowInstanceStatus.InProgress, InitiatedById = fixture.Payment.SubmittedById!.Value
        };
        var step = new WorkflowStep
        {
            Id = Guid.NewGuid(), TenantId = tenant, WorkflowDefinitionId = definitionId,
            Name = "Review payment evidence", Order = 1, StepType = WorkflowStepType.Approval
        };
        var stepInstance = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(), TenantId = tenant, WorkflowInstanceId = instance.Id,
            WorkflowInstance = instance, WorkflowStepId = step.Id, WorkflowStep = step,
            Status = WorkflowStepInstanceStatus.InProgress
        };
        var uploaderId = Guid.NewGuid();
        var evidence = new WorkflowEvidenceDocument
        {
            Id = Guid.NewGuid(), TenantId = tenant, StepInstanceId = stepInstance.Id,
            AttachmentId = Guid.NewGuid().ToString("N"), RequirementKey = "bank-instruction",
            DocumentName = "Bank instruction", DocumentType = "PaymentInstruction",
            FileName = "bank-instruction.pdf", FilePath = "protected/test/bank-instruction.pdf",
            Sha256 = new string('a', 64), FileSizeBytes = 128,
            UploadedById = uploaderId, DocumentOwnerId = uploaderId,
            MalwareScanStatus = WorkflowMalwareScanStatus.Clean,
            VerificationStatus = verification == "unverified"
                ? WorkflowEvidenceVerificationStatus.Pending : WorkflowEvidenceVerificationStatus.Verified,
            VerifiedById = verification == "unverified" ? null
                : verification == "self-verified" ? uploaderId : Guid.NewGuid(),
            VerifiedAt = verification == "unverified" ? null : DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(10), RetainUntil = DateTime.UtcNow.AddYears(1), IsCurrent = true
        };
        db.AddRange(instance, step, stepInstance, evidence);
        await db.SaveChangesAsync();

        var control = await service.GetControlAsync(fixture.Payment.Id);

        control!.ApprovalRequired.Should().BeTrue();
        control.EvidenceRequirementsSatisfied.Should().Be(satisfied);
        var requirement = control.EvidenceRequirements.Should().ContainSingle().Subject;
        requirement.RequireVerification.Should().BeTrue();
        requirement.CurrentDocumentCount.Should().Be(1);
        requirement.VerifiedDocumentCount.Should().Be(satisfied ? 1 : 0);
        requirement.IsSatisfied.Should().Be(satisfied);
        fixture.Payment.Status.Should().Be(VendorPaymentStatus.PendingAuthorization);
        fixture.Payment.AuthorizedById.Should().BeNull();
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentType == "VendorPayment")).Should().Be(0);
    }

    private static WorkflowApprovalConfigDto RequiredPaymentEvidencePolicy(int minimum = 1) => new()
    {
        EvidenceRequirements =
        [
            new WorkflowEvidenceRequirementDto
            {
                RequirementKey = "bank-instruction", DocumentName = "Bank instruction",
                DocumentType = "PaymentInstruction", MinimumDocuments = minimum, RequireVerification = true
            }
        ]
    };

    private sealed record DirectPaymentEvidenceFixture(
        VendorPaymentEvidenceLink Link, CentralDocumentRecord Document,
        CentralDocumentVersion Version, FileUploadRecord Upload, byte[] Bytes);

    private static async Task<DirectPaymentEvidenceFixture> SeedDirectPaymentEvidenceAsync(
        ApplicationDbContext db, VendorPayment payment, Mock<ICentralDocumentRepositoryFileService> files)
    {
        var bytes = Encoding.UTF8.GetBytes("Controlled supplier payment instruction for this exact payment.");
        var checksum = Convert.ToHexString(SHA256.HashData(bytes));
        var uploader = Guid.NewGuid();
        var upload = new FileUploadRecord
        {
            Id = Guid.NewGuid(), TenantId = payment.TenantId, Category = "document-management",
            FilePath = "private/test/payment-instruction.pdf", StoredFileName = "payment-instruction.pdf",
            OriginalFileName = "payment-instruction.pdf", ContentType = "application/pdf",
            FileSize = bytes.Length, StorageProvider = "Test", UploadedByUserId = uploader,
            VirusScanStatus = FileVirusScanStatus.Clean, ScannedAtUtc = DateTime.UtcNow
        };
        var document = new CentralDocumentRecord
        {
            Id = Guid.NewGuid(), TenantId = payment.TenantId, DocumentReference = "DMS-PAYMENT-EVIDENCE",
            Title = "Bank instruction", SourceModule = "Finance", SourceEntityType = "VendorPayment",
            SourceRecordId = payment.Id, SourceRecordReference = payment.PaymentNumber,
            SourceLabel = payment.PaymentNumber, RepositoryStatus = "Linked", CurrentVersion = "v1.0",
            VersionStatus = "Submitted", LifecycleStatus = "Active", EffectiveDate = DateTime.UtcNow.AddDays(-1),
            ExpiryDate = DateTime.UtcNow.AddDays(20)
        };
        var version = new CentralDocumentVersion
        {
            Id = Guid.NewGuid(), TenantId = payment.TenantId, DocumentRecordId = document.Id,
            DocumentRecord = document, VersionNumber = "v1.0", Status = "Submitted",
            RepositoryPath = upload.FilePath,
            FileUploadRecordId = upload.Id, FileName = upload.OriginalFileName, ContentType = upload.ContentType,
            FileSize = upload.FileSize, CreatedByUserId = uploader
        };
        var link = new VendorPaymentEvidenceLink
        {
            Id = Guid.NewGuid(), TenantId = payment.TenantId, VendorPaymentId = payment.Id,
            RequirementKey = "bank-instruction", ClientRequestId = Guid.NewGuid(), RequestHash = new string('b', 64),
            FileUploadRecordId = upload.Id, CentralDocumentRecordId = document.Id,
            CentralDocumentVersionId = version.Id, FileName = upload.OriginalFileName,
            ContentType = upload.ContentType!, FileSize = upload.FileSize, ChecksumSha256 = checksum,
            ExpiryDate = DateTime.UtcNow.AddDays(10), CreatedById = uploader, CreatedBy = "payment-evidence-test"
        };
        var result = new DirectPaymentEvidenceFixture(link, document, version, upload, bytes);
        SetDirectPaymentEvidenceContent(files, result, bytes);
        db.AddRange(upload, document, version, link);
        await db.SaveChangesAsync();
        return result;
    }

    private static void SetDirectPaymentEvidenceContent(Mock<ICentralDocumentRepositoryFileService> files,
        DirectPaymentEvidenceFixture evidence, byte[]? bytes) =>
        files.Setup(item => item.OpenAsync(evidence.Link.TenantId, evidence.Document.Id, evidence.Version.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => bytes == null ? null : new CentralDocumentRepositoryContent
            {
                Content = new MemoryStream(bytes, writable: false), FileName = evidence.Upload.OriginalFileName,
                ContentType = evidence.Upload.ContentType!, FileSize = bytes.Length, UploadRecord = evidence.Upload
            });

    private static void InvalidateDirectPaymentEvidence(DirectPaymentEvidenceFixture evidence,
        Mock<ICentralDocumentRepositoryFileService> files, string scenario)
    {
        switch (scenario)
        {
            case "wrong-payment": evidence.Link.VendorPaymentId = Guid.NewGuid(); break;
            case "foreign-link-tenant": evidence.Link.TenantId = Guid.NewGuid(); break;
            case "foreign-document-tenant": evidence.Document.TenantId = Guid.NewGuid(); break;
            case "foreign-version-tenant": evidence.Version.TenantId = Guid.NewGuid(); break;
            case "foreign-upload-tenant": evidence.Upload.TenantId = Guid.NewGuid(); break;
            case "wrong-source": evidence.Document.SourceRecordId = Guid.NewGuid(); break;
            case "wrong-module": evidence.Document.SourceModule = "Inventory"; break;
            case "wrong-source-type": evidence.Document.SourceEntityType = "VendorInvoice"; break;
            case "stale-version": evidence.Document.CurrentVersion = "v2.0"; break;
            case "draft-version": evidence.Version.Status = "Draft"; break;
            case "deleted-link": evidence.Link.IsDeleted = true; break;
            case "deleted-upload": evidence.Upload.IsDeleted = true; break;
            case "expired-link": evidence.Link.ExpiryDate = DateTime.UtcNow.AddSeconds(-1); break;
            case "expired-document": evidence.Document.ExpiryDate = DateTime.UtcNow.AddSeconds(-1); break;
            case "infected": evidence.Upload.VirusScanStatus = FileVirusScanStatus.Infected; break;
            case "scan-skipped": evidence.Upload.VirusScanStatus = FileVirusScanStatus.Skipped; break;
            case "missing-storage": SetDirectPaymentEvidenceContent(files, evidence, null); break;
            case "changed-bytes": SetDirectPaymentEvidenceContent(files, evidence,
                Encoding.UTF8.GetBytes("This is not the content whose checksum was retained.")); break;
        }
    }
}
