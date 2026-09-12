using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ApPaymentPostingMigrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task OptionalPayment_SubmitsAndPostsThroughNormalFinanceOwner_WithoutFabricatedApprover(bool compatibilitySubmit, bool optionalSignature)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var workflow = OptionalPaymentWorkflow();
        var (service, _) = CreateService(db, tenant, workflowService: workflow.Object,
            approvalPolicyResolver: OptionalPaymentPolicy(optionalSignature
                ? new WorkflowApprovalConfigDto { SignaturePolicy = new WorkflowSignaturePolicyDto { IsRequired = false } }
                : null).Object, useRealPaymentSod: true);

        fixture.Payment.ApprovalRequired.Should().BeTrue("Draft retains the fail-closed persistence default");
        var draftControl = await service.GetControlAsync(fixture.Payment.Id);
        draftControl!.ApprovalRequired.Should().BeFalse("the UI must see current draft policy, not the persistence default");
        draftControl.CanSubmit.Should().BeTrue();

        var submitted = compatibilitySubmit
            ? await service.SubmitForAuthorizationAsync(fixture.Payment.Id)
            : await service.SubmitAsync(fixture.Payment.Id, new SubmitVendorPaymentDto());
        submitted.ApprovalRequired.Should().BeFalse();
        submitted.Status.Should().Be(VendorPaymentStatus.Authorized);
        submitted.AuthorizedById.Should().BeNull();
        submitted.AuthorizedDate.Should().BeNull();
        submitted.WorkflowInstanceId.Should().BeNull();
        submitted.SubmittedById.Should().NotBeNull();
        submitted.ApprovalControlSnapshotHash.Should().HaveLength(64);

        var posted = await service.PostAsync(fixture.Payment.Id);
        posted.Status.Should().Be(VendorPaymentStatus.Processed);
        posted.JournalEntryId.Should().NotBeNull();
        posted.ApprovalRequired.Should().BeFalse();
        posted.AuthorizedById.Should().BeNull();
        var journal = await db.JournalEntries.SingleAsync(item => item.Id == posted.JournalEntryId);
        journal.TotalDebitAmount.Should().Be(100m);
        journal.TotalCreditAmount.Should().Be(100m);
        journal.IsBalanced.Should().BeTrue();
        (await service.PostAsync(fixture.Payment.Id)).JournalEntryId.Should().Be(posted.JournalEntryId);
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentType == "VendorPayment" &&
            item.SourceDocumentId == fixture.Payment.Id && item.PostingStatus == "Posted")).Should().Be(1);
        workflow.Verify(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task OptionalPayment_ActiveDefinitionOrRetainedInstanceStillUsesApproval(bool active, bool inFlight)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var workflow = OptionalPaymentWorkflow(active, inFlight);
        var instanceId = Guid.NewGuid();
        workflow.Setup(item => item.StartApprovalWorkflowAsync("VendorPayment", fixture.Payment.Id))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, WorkflowInstanceId = instanceId });
        var policy = OptionalPaymentPolicy(new WorkflowApprovalConfigDto());
        var (service, _) = CreateService(db, tenant, workflowService: workflow.Object, approvalPolicyResolver: policy.Object);

        var result = await service.SubmitAsync(fixture.Payment.Id, new SubmitVendorPaymentDto());

        result.ApprovalRequired.Should().BeTrue();
        result.Status.Should().Be(VendorPaymentStatus.PendingAuthorization);
        result.WorkflowInstanceId.Should().Be(instanceId);
        result.AuthorizedById.Should().BeNull();
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentType == "VendorPayment")).Should().Be(0);
    }

    [Theory]
    [InlineData("md")]
    [InlineData("evidence")]
    [InlineData("signature")]
    [InlineData("exceptional")]
    [InlineData("evidence-exception")]
    [InlineData("lookup-error")]
    [InlineData("policy-error")]
    public async Task OptionalPayment_DoesNotWaiveAuthorityEvidenceOrLookupFailures(string scenario)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var workflow = OptionalPaymentWorkflow();
        var control = new WorkflowApprovalConfigDto { RequiresManagingDirectorApproval = scenario == "md" };
        if (scenario == "evidence") control.EvidenceRequirements.Add(new WorkflowEvidenceRequirementDto { RequirementKey = "bank-instruction" });
        if (scenario == "signature") control.SignaturePolicy = new WorkflowSignaturePolicyDto { IsRequired = true };
        var policy = OptionalPaymentPolicy(control);
        if (scenario == "lookup-error") workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("VendorPayment"))
            .ThrowsAsync(new InvalidOperationException("Workflow lookup unavailable."));
        if (scenario == "policy-error") policy.Setup(item => item.ResolveAsync(
                It.IsAny<WorkflowApprovalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Policy lookup unavailable."));
        var (service, _) = CreateService(db, tenant, workflowService: workflow.Object, approvalPolicyResolver: policy.Object);

        var action = () => service.SubmitAsync(fixture.Payment.Id, new SubmitVendorPaymentDto
        {
            IsExceptionalPayment = scenario == "exceptional",
            RequestEvidenceException = scenario == "evidence-exception"
        });
        await action.Should().ThrowAsync<InvalidOperationException>();
        db.ChangeTracker.Clear();
        var retained = await db.Set<VendorPayment>().SingleAsync(item => item.Id == fixture.Payment.Id);
        retained.Status.Should().Be(VendorPaymentStatus.Draft);
        retained.ApprovalRequired.Should().BeTrue();
        retained.AuthorizedById.Should().BeNull();
    }

    [Theory]
    [InlineData("closed-period")]
    [InlineData("missing-invoice-journal")]
    [InlineData("fake-approver")]
    [InlineData("tampered-policy")]
    [InlineData("active-instance")]
    public async Task OptionalPayment_PostStillRejectsFinanceSourceAndSnapshotFailures(string scenario)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var (service, _) = CreateService(db, tenant, workflowService: OptionalPaymentWorkflow().Object,
            approvalPolicyResolver: OptionalPaymentPolicy().Object, useRealPaymentSod: true);
        await service.SubmitAsync(fixture.Payment.Id, new SubmitVendorPaymentDto());
        switch (scenario)
        {
            case "closed-period":
                foreach (var period in await db.FiscalPeriods.ToListAsync()) { period.IsClosed = true; period.IsOpen = false; }
                break;
            case "missing-invoice-journal": fixture.Invoice.JournalEntryId = null; break;
            case "fake-approver": fixture.Payment.AuthorizedById = Guid.NewGuid(); break;
            case "tampered-policy": fixture.Payment.ApprovalControlSnapshotHash = new string('0', 64); break;
            case "active-instance":
                db.Set<WorkflowInstance>().Add(new WorkflowInstance
                {
                    Id = Guid.NewGuid(), TenantId = tenant, EntityId = fixture.Payment.Id,
                    EntityTypeId = Guid.NewGuid(), WorkflowDefinitionId = Guid.NewGuid(),
                    Status = WorkflowInstanceStatus.InProgress, InitiatedById = Guid.NewGuid()
                });
                break;
        }
        await db.SaveChangesAsync();
        await ((Func<Task>)(async () => await service.PostAsync(fixture.Payment.Id))).Should().ThrowAsync<Exception>();
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentType == "VendorPayment" &&
            item.PostingStatus == "Posted")).Should().Be(0);
    }

    private static Task<ApPaymentFixture> SeedOptionalPaymentDraftAsync(ErpSystem.Data.ApplicationDbContext db, Guid tenant) =>
        SeedApprovedApPaymentAsync(db, tenant, payment =>
        {
            payment.Status = VendorPaymentStatus.Draft;
            payment.AuthorizedById = null;
            payment.AuthorizedDate = null;
        }, invoice => { invoice.SubmittedById = Guid.NewGuid(); invoice.SubmittedDate = DateTime.UtcNow; });

    [Fact]
    public async Task OptionalPayment_BatchOwnedPaymentCannotBypassItsBatchProcessor()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var (service, _) = CreateService(db, tenant, workflowService: OptionalPaymentWorkflow().Object,
            approvalPolicyResolver: OptionalPaymentPolicy().Object, useRealPaymentSod: true);
        await service.SubmitAsync(fixture.Payment.Id, new SubmitVendorPaymentDto());
        var batch = new PaymentBatch
        {
            Id = Guid.NewGuid(), TenantId = tenant, BatchNumber = "PB-DIRECT",
            ApprovalRequired = false, Status = PaymentBatchStatus.Approved, CreatedById = Guid.NewGuid()
        };
        db.Set<PaymentBatch>().Add(batch);
        fixture.Payment.PaymentBatchId = batch.Id;
        await db.SaveChangesAsync();
        var action = () => service.PostAsync(fixture.Payment.Id);
        var error = await action.Should().ThrowAsync<ErpSystem.Core.Services.Procurement.VendorPaymentControlException>();
        error.Which.Code.Should().Be("AP_PAYMENT_BATCH_DIRECT_POST_BLOCKED");
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentType == "VendorPayment" &&
            item.PostingStatus == "Posted")).Should().Be(0);
    }

    private static Mock<IWorkflowService> OptionalPaymentWorkflow(bool active = false, bool inFlight = false)
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(active);
        workflow.Setup(item => item.HasActiveApprovalInstanceAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(inFlight);
        return workflow;
    }

    private static Mock<IWorkflowApprovalPolicyResolver> OptionalPaymentPolicy(WorkflowApprovalConfigDto? config = null)
    {
        var policy = new Mock<IWorkflowApprovalPolicyResolver>();
        policy.Setup(item => item.ResolveAsync(It.IsAny<WorkflowApprovalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config == null ? null : new WorkflowApprovalPolicyResolution(Guid.NewGuid(), "PAYMENT-POLICY", config));
        return policy;
    }
}
