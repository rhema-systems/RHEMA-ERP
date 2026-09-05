using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
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
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierOnboardingTokenServiceTests
{
    [Fact]
    public async Task IssueOptionsReturnTenantDraftsWithoutExistingTokens()
    {
        await using var fixture = new Fixture(paid: false);

        var beforeIssue = await fixture.Service.GetIssueOptionsAsync(
            "issue-options-before");
        await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-options-token");
        var afterIssue = await fixture.Service.GetIssueOptionsAsync(
            "issue-options-after");

        beforeIssue.Should().ContainSingle().Which.RegistrationId
            .Should().Be(fixture.Registration.Id);
        afterIssue.Should().BeEmpty();
    }

    private static readonly JsonSerializerOptions DecisionJsonOptions = CreateDecisionJsonOptions();

    [Fact]
    public async Task VerifiedApplicantIssueJoinsCallerOwnedTransaction()
    {
        await using var fixture = new Fixture(paid: false);
        fixture.SeedSystemActor();
        await fixture.BeginTransactionAsync();

        var issued = await fixture.Service.IssueForVerifiedApplicantAsync(
            fixture.TenantId,
            fixture.Registration.Id,
            "caller-owned-transaction");

        issued.Token.Id.Should().NotBeEmpty();
        fixture.HasActiveTransaction.Should().BeTrue(
            "the applicant lifecycle must own the atomic registration, token, and access transaction");
        await fixture.RollbackAsync();
    }

    [Fact]
    public async Task IssueUsesServerTimeInsteadOfCallerSelectedPolicyDate()
    {
        await using var fixture = new Fixture(paid: true);

        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id,
                EffectiveAtUtc = DateTime.UtcNow.AddYears(-10)
            },
            "server-time-policy-selection");

        issued.Token.FeeMode.Should().Be(ProcurementSupplierOnboardingFeeMode.Paid);
        issued.Token.SourceConfigurationProfileVersion.Should().BePositive();
    }

    [Fact]
    public async Task IssueRejectsCorrelationReuseForAnotherRegistration()
    {
        await using var fixture = new Fixture(paid: false);
        await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-correlation-bound-to-registration");

        var action = () => fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = Guid.NewGuid()
            },
            "issue-correlation-bound-to-registration");

        await action.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenConflictException>()
            .Where(exception => exception.Code ==
                "SUPPLIER_ONBOARDING_IDEMPOTENCY_MISMATCH");
    }

    [Fact]
    public async Task VerifiedApplicantIssueRejectsCorrelationReuseForAnotherRegistration()
    {
        await using var fixture = new Fixture(paid: false);
        fixture.SeedSystemActor();
        await fixture.Service.IssueForVerifiedApplicantAsync(
            fixture.TenantId,
            fixture.Registration.Id,
            "applicant-correlation-bound-to-registration");

        var action = () => fixture.Service.IssueForVerifiedApplicantAsync(
            fixture.TenantId,
            Guid.NewGuid(),
            "applicant-correlation-bound-to-registration");

        await action.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenConflictException>()
            .Where(exception => exception.Code ==
                "SUPPLIER_ONBOARDING_IDEMPOTENCY_MISMATCH");
    }

    [Fact]
    public async Task FreeTokenIsIssuedOnceRemainsActiveAndExpiresOnlyAtTerminalApplicationState()
    {
        await using var fixture = new Fixture(paid: false);

        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-free");
        var replay = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-free");

        issued.PlaintextToken.Should().NotBeNullOrWhiteSpace();
        issued.Token.Status.Should().Be(ProcurementSupplierOnboardingTokenStatus.Active);
        issued.Token.PaymentStatus.Should()
            .Be(ProcurementSupplierOnboardingPaymentStatus.NotRequired);
        issued.Token.ExpiredAtUtc.Should().BeNull();
        replay.Token.Id.Should().Be(issued.Token.Id);
        replay.PlaintextToken.Should().BeNull(
            "plaintext tokens are returned only on their original issue response");

        fixture.Registration.Status = "Rejected";
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.ExpireForTerminalRegistrationAsync(
            fixture.Registration.Id,
            "Rejected",
            fixture.UserId,
            "registration-rejected");
        await fixture.Service.ExpireForTerminalRegistrationAsync(
            fixture.Registration.Id,
            "Rejected",
            fixture.UserId,
            "registration-rejected-recovery");

        var terminal = await fixture.Service.GetAsync(issued.Token.Id);
        terminal.Status.Should().Be(ProcurementSupplierOnboardingTokenStatus.Expired);
        terminal.ExpiredAtUtc.Should().NotBeNull();
        terminal.ExpiryReason.Should().Be("Application Rejected.");
    }

    [Fact]
    public async Task PaidTokenRequiresTrustedVerificationBeforePostingAndActivation()
    {
        await using var fixture = new Fixture(paid: true);

        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-paid");
        var paymentSession = await fixture.SeedApplicantSessionAsync(issued.Token.Id);
        var submitted = await fixture.Service.RecordPaymentAsync(
            issued.Token.Id,
            new RecordProcurementSupplierOnboardingPaymentRequest
            {
                PaymentMethodId = fixture.PaymentMethod!.Id,
                PaymentReference = "MOMO-2026-0001",
                RowVersion = issued.Token.RowVersion
            },
            "post-payment");
        var replay = await fixture.Service.RecordPaymentAsync(
            issued.Token.Id,
            new RecordProcurementSupplierOnboardingPaymentRequest
            {
                PaymentMethodId = fixture.PaymentMethod.Id,
                PaymentReference = "MOMO-2026-0001",
                RowVersion = issued.Token.RowVersion
            },
            "post-payment");

        submitted.Token.Status.Should()
            .Be(ProcurementSupplierOnboardingTokenStatus.AwaitingPayment);
        submitted.Token.PaymentStatus.Should()
            .Be(ProcurementSupplierOnboardingPaymentStatus.Pending);
        var pending = submitted.Token.Payments.Should().ContainSingle().Subject;
        pending.Status.Should().Be(ProcurementSupplierOnboardingPaymentStatus.Pending);
        pending.PostingEventId.Should().BeNull();
        pending.JournalEntryId.Should().BeNull();
        pending.ReceiptNumber.Should().BeNull();
        fixture.FinancePostCount.Should().Be(0);
        replay.Token.Payments.Should().ContainSingle();
        var mismatchedPaymentReplay = () => fixture.Service.RecordPaymentAsync(
            issued.Token.Id,
            new RecordProcurementSupplierOnboardingPaymentRequest
            {
                PaymentMethodId = fixture.PaymentMethod.Id,
                PaymentReference = "DIFFERENT-CLAIM-REFERENCE",
                RowVersion = issued.Token.RowVersion
            },
            "post-payment");
        await mismatchedPaymentReplay.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenConflictException>()
            .Where(exception => exception.Code ==
                "SUPPLIER_ONBOARDING_IDEMPOTENCY_MISMATCH");

        fixture.ResetPaymentLifecycleSnapshots();
        fixture.SetUser(Guid.NewGuid());
        var verified = await fixture.Service.ReconcilePaymentAsync(
            issued.Token.Id,
            pending.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "PROVIDER-CONFIRM-0001",
                RowVersion = pending.RowVersion
            },
            "verify-payment");
        var verificationReplay = await fixture.Service.ReconcilePaymentAsync(
            issued.Token.Id,
            pending.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "PROVIDER-CONFIRM-0001",
                RowVersion = pending.RowVersion
            },
            "verify-payment");

        verified.Token.Status.Should().Be(ProcurementSupplierOnboardingTokenStatus.Active);
        verified.Token.PaymentStatus.Should()
            .Be(ProcurementSupplierOnboardingPaymentStatus.Reconciled);
        verified.PlaintextToken.Should().NotBeNullOrWhiteSpace();
        verified.Token.Generation.Should().Be(2);
        verified.Token.Payments.Should().ContainSingle(item =>
            item.ReceiptNumber == "SUP-ONB-2026-00001" &&
            item.PostingEventId == fixture.PostingEventId &&
            item.JournalEntryId == fixture.JournalEntryId &&
            item.ReconciliationReference == "PROVIDER-CONFIRM-0001" &&
            item.ReconciliationNotes == null);
        fixture.PostedRequest.Should().NotBeNull();
        fixture.PostedRequest!.OriginModuleCode.Should().Be("PROC");
        fixture.PostedRequest.SourceDocumentType.Should()
            .Be("SupplierOnboardingTokenPayment");
        fixture.PostedRequest.SourceDocumentId.Should().Be(pending.Id);
        fixture.PostedRequest.SourceDocumentTenantId.Should().Be(fixture.TenantId);
        fixture.PostedRequest.AccountingBookCode.Should().Be("IFRS");
        fixture.PostedRequest.IdempotencyKey.Should().Be(
            $"PROCUREMENT|SUPPLIER-ONBOARDING|{pending.Id:N}|IFRS|POST");
        fixture.PostedRequest.ReturnExistingOnDuplicate.Should().BeTrue();
        fixture.PostedRequest.SourceDocumentReference.Should()
            .Be("PROVIDER-CONFIRM-0001",
                "Finance must use the independently verified provider reference, not the applicant claim reference");
        fixture.PostedRequest.Lines.Should().OnlyContain(item =>
            item.SourceReferenceNumber == "PROVIDER-CONFIRM-0001");
        fixture.PostedRequest.Lines.Sum(item => item.DebitAmount).Should()
            .Be(fixture.PostedRequest.Lines.Sum(item => item.CreditAmount));
        fixture.PostedRequest.Lines.Should().Contain(item =>
            item.TransactionTag == "SupplierOnboardingFee" &&
            item.AccountId == fixture.RevenueAccount!.Id);
        verificationReplay.Token.Payments.Should().ContainSingle();
        verificationReplay.PlaintextToken.Should().BeNull();
        var mismatchedVerificationReplay = () => fixture.Service.ReconcilePaymentAsync(
            issued.Token.Id,
            pending.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "DIFFERENT-PROVIDER-REFERENCE",
                RowVersion = pending.RowVersion
            },
            "verify-payment");
        await mismatchedVerificationReplay.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenConflictException>()
            .Where(exception => exception.Code ==
                "SUPPLIER_ONBOARDING_IDEMPOTENCY_MISMATCH");
        (await fixture.Context.ProcurementSupplierApplicantSessions
                .SingleAsync(item => item.Id == paymentSession.Id))
            .Status.Should().Be(ProcurementSupplierApplicantSessionStatus.Revoked);
        fixture.FinancePostCount.Should().Be(1,
            "verification replays must not create duplicate Finance postings");
        fixture.AssertPostedThenReconciledLifecycle();
    }

    [Fact]
    public async Task ReconciliationDoesNotExposeProtectedPaymentMutationToFinanceSave()
    {
        await using var fixture = new Fixture(paid: true);
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-for-shared-save");
        var submitted = await fixture.Service.RecordPaymentAsync(
            issued.Token.Id,
            new RecordProcurementSupplierOnboardingPaymentRequest
            {
                PaymentMethodId = fixture.PaymentMethod!.Id,
                PaymentReference = "MOMO-SHARED-SAVE-001",
                RowVersion = issued.Token.RowVersion
            },
            "record-for-shared-save");
        var pending = submitted.Token.Payments.Should().ContainSingle().Subject;
        var originalPaidAtUtc = DateTime.UtcNow.AddDays(-2);
        var storedPayment = await fixture.Context.ProcurementSupplierOnboardingPayments
            .SingleAsync(item => item.Id == pending.Id);
        storedPayment.PaidAtUtc = originalPaidAtUtc;
        await fixture.Context.SaveChangesAsync();

        fixture.FinancePostHandler = async (request, cancellationToken) =>
        {
            var entry = fixture.Context.Entry(storedPayment);
            entry.Property(item => item.PaidAtUtc).IsModified.Should().BeFalse(
                "Finance validation and audit saves must not flush a protected payment timestamp");
            storedPayment.Status.Should().Be(ProcurementSupplierOnboardingPaymentStatus.Pending);
            storedPayment.PaidAtUtc.Should().Be(originalPaidAtUtc);
            request.PostingDate.Should().Be(DateTime.UtcNow.Date);

            // Finance audit records use the shared DbContext and call
            // SaveChangesAsync while validating a posting request.
            await fixture.Context.SaveChangesAsync(cancellationToken);
        };
        fixture.SetUser(Guid.NewGuid());

        var reconciled = await fixture.Service.ReconcilePaymentAsync(
            issued.Token.Id,
            pending.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "PROVIDER-SHARED-SAVE-001",
                RowVersion = pending.RowVersion
            },
            "verify-after-shared-save");

        var payment = reconciled.Token.Payments.Should().ContainSingle().Subject;
        payment.Status.Should().Be(ProcurementSupplierOnboardingPaymentStatus.Reconciled);
        payment.PaidAtUtc.Should().BeAfter(originalPaidAtUtc);
    }

    [Fact]
    public async Task TokenRotationRequiresCurrentRowVersionAndNeverReturnsTheOldSecret()
    {
        await using var fixture = new Fixture(paid: false);
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-for-rotation");
        var activeSession = await fixture.SeedApplicantSessionAsync(issued.Token.Id);

        var invalid = () => fixture.Service.ReissueAsync(
            issued.Token.Id,
            new ReissueProcurementSupplierOnboardingTokenRequest
            {
                Reason = "Applicant reported the token compromised.",
                RowVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            },
            "rotate-invalid");
        await invalid.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenConflictException>()
            .Where(exception => exception.Code ==
                "SUPPLIER_ONBOARDING_CONCURRENCY_CONFLICT");

        var rotated = await fixture.Service.ReissueAsync(
            issued.Token.Id,
            new ReissueProcurementSupplierOnboardingTokenRequest
            {
                Reason = "Applicant reported the token compromised.",
                RowVersion = issued.Token.RowVersion
            },
            "rotate-valid");

        rotated.PlaintextToken.Should().NotBeNullOrWhiteSpace();
        rotated.PlaintextToken.Should().NotBe(issued.PlaintextToken);
        rotated.Token.Generation.Should().Be(2);
        rotated.Token.ExpiredAtUtc.Should().BeNull();
        var revokedSession = await fixture.Context.ProcurementSupplierApplicantSessions
            .SingleAsync(item => item.Id == activeSession.Id);
        revokedSession.Status.Should()
            .Be(ProcurementSupplierApplicantSessionStatus.Revoked);
        revokedSession.RevokedAtUtc.Should().NotBeNull();
        revokedSession.RevocationReason.Should().Be("Application token reissued.");
        revokedSession.LastModifiedById.Should().Be(fixture.UserId);
        revokedSession.IntegrityHash.Should().HaveLength(64);

        var replay = await fixture.Service.ReissueAsync(
            issued.Token.Id,
            new ReissueProcurementSupplierOnboardingTokenRequest
            {
                Reason = "Applicant reported the token compromised.",
                RowVersion = issued.Token.RowVersion
            },
            "rotate-valid");
        replay.Token.Generation.Should().Be(2);
        replay.PlaintextToken.Should().BeNull();
    }

    [Fact]
    public async Task ApplicantBindingAuthorizesRetainedRegistrationWithoutReplacingAuditCreator()
    {
        await using var fixture = new Fixture(paid: false);
        var originalAuditCreator = Guid.NewGuid();
        fixture.Registration.CreatedById = originalAuditCreator;
        await fixture.Context.SaveChangesAsync();
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-retained-registration");
        var session = await fixture.SeedApplicantSessionAsync(issued.Token.Id);
        fixture.UseApplicantSession(
            fixture.Registration.Id,
            issued.Token.Id,
            session.SessionReference);

        var retained = await fixture.Service.GetForRegistrationAsync(
            fixture.Registration.Id);

        retained.Should().NotBeNull();
        retained!.Id.Should().Be(issued.Token.Id);
        fixture.Registration.CreatedById.Should().Be(originalAuditCreator);

        fixture.UseApplicantSession(
            Guid.NewGuid(),
            issued.Token.Id,
            session.SessionReference);
        var mismatchedClaim = () => fixture.Service.GetForRegistrationAsync(
            fixture.Registration.Id);
        await mismatchedClaim.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenAuthorizationException>();
    }

    [Fact]
    public async Task ExternalApplicantCannotIssueOrReadAnotherApplicantsToken()
    {
        await using var fixture = new Fixture(paid: false);
        var foreignRegistration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationNumber = "REG-FOREIGN",
            ApplicantName = "Foreign applicant",
            PartnerType = "Supplier",
            Status = "Draft",
            CreatedById = Guid.NewGuid()
        };
        fixture.Context.Add(foreignRegistration);
        await fixture.Context.SaveChangesAsync();
        fixture.SetExternal(true);

        var issue = () => fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = foreignRegistration.Id
            },
            "foreign-issue");

        await issue.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenAuthorizationException>();
        (await fixture.Context.ProcurementSupplierOnboardingTokens.CountAsync())
            .Should().Be(0);
    }

    [Fact]
    public async Task TerminalExpiryRejectsEveryNonApprovedOrRejectedState()
    {
        await using var fixture = new Fixture(paid: false);
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-terminal-test");

        var action = () => fixture.Service.ExpireForTerminalRegistrationAsync(
            fixture.Registration.Id,
            "Cancelled",
            fixture.UserId,
            "cancelled-terminal");

        await action.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenValidationException>()
            .Where(exception => exception.Code ==
                "SUPPLIER_ONBOARDING_TERMINAL_STATUS_INVALID");
        (await fixture.Service.GetAsync(issued.Token.Id)).Status.Should()
            .Be(ProcurementSupplierOnboardingTokenStatus.Active);
    }

    [Fact]
    public async Task IssueFailsClosedWhenTheEffectiveDecisionEvidenceIsNotVerified()
    {
        await using var fixture = new Fixture(paid: false);
        fixture.Decision.EvidenceStatus = ProcurementConfigurationEvidenceStatus.Attached;
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "unverified-decision");

        await action.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenValidationException>()
            .Where(exception => exception.Code ==
                "SUPPLIER_ONBOARDING_DEC007_NOT_EFFECTIVE");
    }

    [Fact]
    public async Task TokenReadsAreTenantIsolated()
    {
        await using var fixture = new Fixture(paid: false);
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "tenant-isolation");
        fixture.SetTenant(Guid.NewGuid());

        var action = () => fixture.Service.GetAsync(issued.Token.Id);

        await action.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenNotFoundException>();
    }

    [Fact]
    public async Task CompletedSharedWorkflowAllowsIndependentEvidenceBackedExemption()
    {
        await using var fixture = new Fixture(paid: true);
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-for-exemption");
        var requested = await fixture.Service.RequestExemptionAsync(
            issued.Token.Id,
            new RequestProcurementSupplierOnboardingExemptionRequest
            {
                Reason = "Public-interest supplier onboarding.",
                Evidence = [Evidence("REQUEST-MINUTE-001")],
                RowVersion = issued.Token.RowVersion
            },
            "request-exemption");
        var exemption = requested.Exemptions.Should().ContainSingle().Subject;
        fixture.WorkflowInstance!.Status = WorkflowInstanceStatus.Completed;
        fixture.SetUser(Guid.NewGuid());

        var approved = await fixture.Service.DecideExemptionAsync(
            issued.Token.Id,
            exemption.Id,
            new DecideProcurementSupplierOnboardingExemptionRequest
            {
                Approve = true,
                Comment = "Shared workflow completed and evidence independently verified.",
                Evidence = [Evidence("APPROVAL-MINUTE-001")],
                RowVersion = exemption.RowVersion
            },
            "approve-exemption");

        approved.Token.Status.Should().Be(ProcurementSupplierOnboardingTokenStatus.Active);
        approved.Token.PaymentStatus.Should()
            .Be(ProcurementSupplierOnboardingPaymentStatus.Exempt);
        approved.Token.Exemptions.Should().ContainSingle(item =>
            item.Status == ProcurementSupplierOnboardingExemptionStatus.Approved);
    }

    [Fact]
    public async Task FinanceReconciliationRequiresAnIndependentActorAndIsReplaySafe()
    {
        await using var fixture = new Fixture(paid: true);
        fixture.SetPostingMode("Local");
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-for-reconciliation");
        var submitted = await fixture.Service.RecordPaymentAsync(
            issued.Token.Id,
            new RecordProcurementSupplierOnboardingPaymentRequest
            {
                PaymentMethodId = fixture.PaymentMethod!.Id,
                PaymentReference = "MOMO-RECON-001",
                RowVersion = issued.Token.RowVersion
            },
            "post-for-reconciliation");
        var payment = submitted.Token.Payments.Should().ContainSingle().Subject;
        var selfVerification = () => fixture.Service.ReconcilePaymentAsync(
            issued.Token.Id,
            payment.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "SELF-VERIFY-REJECTED",
                Notes = "The submitting actor cannot verify their own claim.",
                RowVersion = payment.RowVersion
            },
            "self-verification");
        await selfVerification.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenAuthorizationException>();
        fixture.FinancePostCount.Should().Be(0);
        fixture.SetUser(Guid.NewGuid());

        var reconciled = await fixture.Service.ReconcilePaymentAsync(
            issued.Token.Id,
            payment.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "BANK-RECON-001",
                Notes = "Matched to the Finance settlement report.",
                RowVersion = payment.RowVersion
            },
            "reconcile-payment");
        var replay = await fixture.Service.ReconcilePaymentAsync(
            issued.Token.Id,
            payment.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "BANK-RECON-001",
                Notes = "Matched to the Finance settlement report.",
                RowVersion = payment.RowVersion
            },
            "reconcile-payment");

        reconciled.Token.PaymentStatus.Should()
            .Be(ProcurementSupplierOnboardingPaymentStatus.Reconciled);
        replay.Token.Payments.Should().ContainSingle(item =>
            item.ReconciliationReference == "BANK-RECON-001" &&
            item.Status == ProcurementSupplierOnboardingPaymentStatus.Reconciled);
        fixture.PostedRequest!.AccountingBookCode.Should().Be("LOCAL_STATUTORY");
        fixture.PostedRequest.IdempotencyKey.Should().Contain("|LOCAL_STATUTORY|POST");
    }

    [Theory]
    [InlineData("")]
    [InlineData("AllClassifiedBooks")]
    [InlineData("UnknownBook")]
    public async Task ReconciliationRejectsInvalidBookConfigurationBeforeFinanceOrStateMutation(string configured)
    {
        await using var fixture = new Fixture(paid: true);
        fixture.SetPostingMode(configured);
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest { RegistrationId = fixture.Registration.Id }, "issue-book-denial");
        var submitted = await fixture.Service.RecordPaymentAsync(issued.Token.Id,
            new RecordProcurementSupplierOnboardingPaymentRequest
            {
                PaymentMethodId = fixture.PaymentMethod!.Id, PaymentReference = "BOOK-DENIAL",
                RowVersion = issued.Token.RowVersion
            }, "record-book-denial");
        var payment = submitted.Token.Payments.Single();
        fixture.SetUser(Guid.NewGuid());

        var action = () => fixture.Service.ReconcilePaymentAsync(issued.Token.Id, payment.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "BOOK-DENIAL-CONFIRMED", RowVersion = payment.RowVersion
            }, "reconcile-book-denial");

        await action.Should().ThrowAsync<ProcurementSupplierOnboardingTokenValidationException>();
        fixture.FinancePostCount.Should().Be(0);
        (await fixture.Context.ProcurementSupplierOnboardingPayments.SingleAsync(item => item.Id == payment.Id))
            .Status.Should().Be(ProcurementSupplierOnboardingPaymentStatus.Pending);
    }

    [Theory]
    [InlineData("IFRS", "IFRS")]
    [InlineData("Local", "LOCAL_STATUTORY")]
    [InlineData("Management", "MANAGEMENT")]
    public async Task ReconciliationMapsSupportedSingleBookAliases(string configured, string expected)
    {
        await using var fixture = new Fixture(paid: true);
        fixture.SetPostingMode(configured);
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest { RegistrationId = fixture.Registration.Id }, "issue-book-alias");
        var submitted = await fixture.Service.RecordPaymentAsync(issued.Token.Id,
            new RecordProcurementSupplierOnboardingPaymentRequest
            {
                PaymentMethodId = fixture.PaymentMethod!.Id, PaymentReference = $"BOOK-{expected}",
                RowVersion = issued.Token.RowVersion
            }, "record-book-alias");
        var payment = submitted.Token.Payments.Single();
        fixture.SetUser(Guid.NewGuid());

        await fixture.Service.ReconcilePaymentAsync(issued.Token.Id, payment.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = $"CONFIRMED-{expected}", RowVersion = payment.RowVersion
            }, "reconcile-book-alias");

        fixture.PostedRequest!.AccountingBookCode.Should().Be(expected);
        fixture.PostedRequest.IdempotencyKey.Should().Contain($"|{expected}|POST");
    }

    [Fact]
    public async Task ApplicantPaymentUsesSessionLineageAndDoesNotImpersonateAdministrator()
    {
        await using var fixture = new Fixture(paid: true);
        fixture.SeedSystemActor();
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-for-applicant-lineage");
        var session = await fixture.SeedApplicantSessionAsync(issued.Token.Id);
        fixture.SetUser(session.ApplicantAccessId);
        fixture.UseApplicantSession(
            fixture.Registration.Id,
            issued.Token.Id,
            session.SessionReference);

        var submitted = await fixture.Service.RecordApplicantPaymentAsync(
            issued.Token.Id,
            session.Id,
            new RecordProcurementSupplierOnboardingPaymentRequest
            {
                PaymentMethodId = fixture.PaymentMethod!.Id,
                PaymentReference = "MOMO-APPLICANT-001",
                RowVersion = issued.Token.RowVersion
            },
            "applicant-payment-lineage");

        var submittedDto = submitted.Token.Payments.Should().ContainSingle().Subject;
        submittedDto.SubmittedByApplicant.Should().BeTrue();
        submittedDto.SubmittedByApplicantSessionId.Should().Be(session.Id);
        var stored = await fixture.Context.ProcurementSupplierOnboardingPayments
            .SingleAsync(item => item.Id == submittedDto.Id);
        stored.CreatedBy.Should().Be("Verified Supplier Applicant");
        stored.CreatedById.Should().BeNull(
            "a pre-approval applicant is not an ERP user and must not borrow an administrator ID");
        stored.SubmittedByApplicantSessionId.Should().Be(session.Id);

        fixture.SetExternal(false);
        fixture.SetUser(fixture.UserId);
        var reconciled = await fixture.Service.ReconcilePaymentAsync(
            issued.Token.Id,
            stored.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "CASHIER-VERIFIED-001",
                Notes = "Applicant payment independently verified by the cashier.",
                RowVersion = submittedDto.RowVersion
            },
            "verify-applicant-payment");

        reconciled.Token.PaymentStatus.Should()
            .Be(ProcurementSupplierOnboardingPaymentStatus.Reconciled);
        fixture.FinancePostCount.Should().Be(1);
    }

    [Fact]
    public async Task ApplicantPaymentRejectsASessionFromAnotherToken()
    {
        await using var fixture = new Fixture(paid: true);
        var issued = await fixture.Service.IssueAsync(
            new IssueProcurementSupplierOnboardingTokenRequest
            {
                RegistrationId = fixture.Registration.Id
            },
            "issue-for-session-boundary");
        var session = await fixture.SeedApplicantSessionAsync(issued.Token.Id);
        fixture.SetUser(session.ApplicantAccessId);
        fixture.UseApplicantSession(
            fixture.Registration.Id,
            issued.Token.Id,
            session.SessionReference);

        var action = () => fixture.Service.RecordApplicantPaymentAsync(
            Guid.NewGuid(),
            session.Id,
            new RecordProcurementSupplierOnboardingPaymentRequest
            {
                PaymentMethodId = fixture.PaymentMethod!.Id,
                PaymentReference = "MOMO-CROSS-TOKEN",
                RowVersion = issued.Token.RowVersion
            },
            "reject-cross-token-session");

        await action.Should()
            .ThrowAsync<ProcurementSupplierOnboardingTokenAuthorizationException>();
        fixture.FinancePostCount.Should().Be(0);
    }

    private static ProcurementControlEventEvidenceReference Evidence(string reference) =>
        new()
        {
            ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
            Reference = reference,
            Label = "Supplier-onboarding decision evidence",
            RequirementKey = "DEC-007"
        };

    private sealed class PaymentLifecycleSaveInterceptor : SaveChangesInterceptor
    {
        private readonly List<(
            ProcurementSupplierOnboardingPaymentStatus TokenPaymentStatus,
            ProcurementSupplierOnboardingPaymentStatus PaymentStatus)> _snapshots = [];

        public void Reset() => _snapshots.Clear();

        public void AssertPostedThenReconciled()
        {
            _snapshots.Should().Equal(
                (
                    ProcurementSupplierOnboardingPaymentStatus.Posted,
                    ProcurementSupplierOnboardingPaymentStatus.Posted),
                (
                    ProcurementSupplierOnboardingPaymentStatus.Reconciled,
                    ProcurementSupplierOnboardingPaymentStatus.Reconciled));
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            Capture(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Capture(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void Capture(DbContext? context)
        {
            if (context is null)
                return;

            var token = context.ChangeTracker
                .Entries<ProcurementSupplierOnboardingToken>()
                .FirstOrDefault(entry => entry.State == EntityState.Modified)?.Entity;
            var payment = context.ChangeTracker
                .Entries<ProcurementSupplierOnboardingPayment>()
                .FirstOrDefault(entry => entry.State == EntityState.Modified)?.Entity;
            if (token is null || payment is null)
                return;

            _snapshots.Add((token.PaymentStatus, payment.Status));
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _tenantId;
        private Guid _userId;
        private bool _external;
        private string _authenticationProvider = "Local";
        private readonly Dictionary<string, string> _claims = new();
        private readonly UnitOfWork _unitOfWork;
        private readonly PaymentLifecycleSaveInterceptor _paymentLifecycleInterceptor = new();

        public Fixture(bool paid)
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            _tenantId = TenantId;
            _userId = UserId;
            PostingEventId = Guid.NewGuid();
            JournalEntryId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .AddInterceptors(_paymentLifecycleInterceptor)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "TDC",
                Name = "TDC",
                Status = TenantStatus.Active
            });

            Registration = new BusinessPartnerRegistration
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                RegistrationNumber = paid ? "REG-PAID" : "REG-FREE",
                ApplicantName = paid ? "Paid applicant" : "Free applicant",
                ApplicantEmail = "applicant@tdc.test",
                PartnerType = "Supplier",
                Status = "Draft",
                CreatedById = UserId
            };
            var profile = new ProcurementConfigurationProfile
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileKey = Guid.NewGuid(),
                ProfileCode = "TDC-PROCUREMENT",
                Name = "TDC Procurement",
                Version = 4,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                IsDefault = true
            };

            if (paid)
            {
                RevenueAccount = Account(
                    AccountType.Revenue, "ONBOARDING-REVENUE", "Supplier onboarding revenue");
                TaxAccount = Account(
                    AccountType.Liability, "ONBOARDING-TAX", "Supplier onboarding tax");
                ReceiptAccount = Account(
                    AccountType.Asset, "MOMO-CLEARING", "Mobile money clearing");
                PaymentMethod = new FinancePaymentMethod
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    Code = "MOMO",
                    Name = "Mobile Money",
                    Type = PaymentMethodType.MobileMoney,
                    IsActive = true,
                    RequiresReference = true,
                    DefaultGLAccountId = ReceiptAccount.Id
                };
                Context.AddRange(
                    RevenueAccount, TaxAccount, ReceiptAccount, PaymentMethod);
                WorkflowDefinition = new WorkflowDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    DefinitionKey = Guid.NewGuid(),
                    Name = "Supplier onboarding fee exemption",
                    EntityTypeId = Guid.NewGuid(),
                    Version = 1,
                    LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                    IsActive = true
                };
                Context.Add(WorkflowDefinition);
            }

            var value = new ProcurementSupplierFeeDecisionValueDto
            {
                Mode = paid
                    ? ProcurementSupplierOnboardingFeeMode.Paid
                    : ProcurementSupplierOnboardingFeeMode.Free,
                FeeType = "Supplier application token",
                Amount = paid ? 100m : 0m,
                CurrencyCode = "GHS",
                TaxPercent = paid ? 15m : 0m,
                PaymentChannels = paid ? ["MOMO"] : [],
                RevenueAccountId = RevenueAccount?.Id,
                TaxAccountId = TaxAccount?.Id,
                ExemptionWorkflowDefinitionId = WorkflowDefinition?.Id,
                ReceiptNumberFormat = "SUP-ONB-{YYYY}-{#####}",
                ExemptionRule = "Requires an independently approved shared workflow.",
                RefundRule = "Refund only through a separately approved Finance adjustment.",
                RenewalRule = "Rotation is explicit and retains the application lifetime.",
                EffectiveFrom = DateTime.UtcNow.AddDays(-30)
            };
            var validated = ProcurementConfigurationDecisionRegistry.Validate(
                "DEC-007", 1,
                JsonSerializer.SerializeToElement(value, DecisionJsonOptions));
            validated.IsValid.Should().BeTrue(string.Join("; ", validated.Errors));
            Decision = new ProcurementConfigurationDecision
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileId = profile.Id,
                DecisionKey = "DEC-007",
                SchemaVersion = 1,
                OwnerGroup = "Procurement + Finance",
                Status = ProcurementConfigurationDecisionStatus.Approved,
                ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved,
                EvidenceStatus = ProcurementConfigurationEvidenceStatus.Verified,
                ValueJson = validated.CanonicalJson!,
                EffectiveFrom = value.EffectiveFrom,
                ApprovedById = Guid.NewGuid(),
                ApprovedAt = DateTime.UtcNow.AddDays(-1)
            };
            profile.Decisions.Add(Decision);
            Context.AddRange(Registration, profile);
            Context.SaveChanges();

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            current.SetupGet(item => item.UserId).Returns(() => _userId);
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(() => _external);
            current.SetupGet(item => item.Username).Returns("supplier.controls");
            current.SetupGet(item => item.FullName).Returns("Supplier Controls");
            current.SetupGet(item => item.AuthenticationProvider)
                .Returns(() => _authenticationProvider);
            current.SetupGet(item => item.Claims).Returns(_claims);
            current.SetupGet(item => item.Roles).Returns(() =>
                _external ? ["External"] : ["TDC_PROCUREMENT_OFFICER"]);
            current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) =>
                    !_external && role == "TDC_PROCUREMENT_OFFICER");

            _unitOfWork = new UnitOfWork(Context);
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                    ProcurementAccessCapabilityRequest request,
                    string _,
                    CancellationToken _) => new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Code = "ACCESS_ALLOWED",
                    Message = "The test actor has the required controlled capability.",
                    ActorUserId = _userId,
                    TenantId = _tenantId,
                    PermissionCode = request.PermissionCode,
                    EvaluatedAtUtc = DateTime.UtcNow
                });
            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    ProcurementSodGuardRequest request,
                    string _,
                    CancellationToken _) => Task.FromResult(
                    new ProcurementSodGuardDecisionDto
                    {
                        Allowed = !request.ProhibitedActorUserIds.Contains(_userId)
                    }));
            var workflow = new Mock<IWorkflowInstanceService>();
            workflow.Setup(item => item.StartWorkflowAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                    It.IsAny<Guid>(), It.IsAny<object?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Guid definitionId,
                    Guid entityTypeId,
                    string entityId,
                    Guid startedById,
                    object? _,
                    CancellationToken _) =>
                {
                    WorkflowInstance = new WorkflowInstance
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        WorkflowDefinitionId = definitionId,
                        EntityTypeId = entityTypeId,
                        EntityId = Guid.Parse(entityId),
                        InitiatedById = startedById,
                        Status = WorkflowInstanceStatus.InProgress
                    };
                    return Task.FromResult(WorkflowInstance);
                });
            workflow.Setup(item => item.GetInstanceAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((
                    Guid _,
                    CancellationToken _) =>
                    Task.FromResult<WorkflowInstance?>(WorkflowInstance));
            var finance = new Mock<IFinancePostingEngine>();
            finance.Setup(item => item.PostAsync(
                    It.IsAny<FinancePostingRequestV2Dto>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (FinancePostingRequestV2Dto request, CancellationToken cancellationToken) =>
                {
                    PostedRequest = request;
                    FinancePostCount++;
                    if (FinancePostHandler != null)
                        await FinancePostHandler(request, cancellationToken);
                    return new FinancePostingResultDto
                    {
                        PostingEventId = PostingEventId,
                        JournalEntryId = JournalEntryId,
                        JournalEntryNumber = "JE-2026-00001",
                        PostingStatus = "Posted",
                        FunctionalCurrencyCode = "GHS"
                    };
                });
            var numbering = new Mock<IDocumentNumberingService>();
            numbering.Setup(item => item.GenerateConfiguredAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                    It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("SUP-ONB-2026-00001");
            var notifications = new Mock<INotificationTopicPublisher>();
            notifications.Setup(item => item.PublishAsync(
                    It.IsAny<NotificationTopicEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var events = new ProcurementControlEventService(
                _unitOfWork, current.Object,
                NullLogger<ProcurementControlEventService>.Instance);

            Service = new ProcurementSupplierOnboardingTokenService(
                _unitOfWork, current.Object, access.Object, sod.Object, events,
                workflow.Object, finance.Object, numbering.Object,
                notifications.Object,
                NullLogger<ProcurementSupplierOnboardingTokenService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public Guid PostingEventId { get; }
        public Guid JournalEntryId { get; }
        public ApplicationDbContext Context { get; }
        public BusinessPartnerRegistration Registration { get; }
        public ProcurementConfigurationDecision Decision { get; }
        public Account? RevenueAccount { get; }
        public Account? TaxAccount { get; }
        public Account? ReceiptAccount { get; }
        public FinancePaymentMethod? PaymentMethod { get; }
        public WorkflowDefinition? WorkflowDefinition { get; }
        public WorkflowInstance? WorkflowInstance { get; private set; }
        public FinancePostingRequestV2Dto? PostedRequest { get; private set; }
        public int FinancePostCount { get; private set; }
        public Func<FinancePostingRequestV2Dto, CancellationToken, Task>? FinancePostHandler { get; set; }
        public ProcurementSupplierOnboardingTokenService Service { get; }
        public bool HasActiveTransaction => _unitOfWork.HasActiveTransaction;

        public void ResetPaymentLifecycleSnapshots() =>
            _paymentLifecycleInterceptor.Reset();

        public void AssertPostedThenReconciledLifecycle() =>
            _paymentLifecycleInterceptor.AssertPostedThenReconciled();

        public void SetExternal(bool value) => _external = value;
        public void SetTenant(Guid value) => _tenantId = value;
        public void SetUser(Guid value) => _userId = value;
        public void SetPostingMode(string? value)
        {
            var settings = Context.FinanceSettings.SingleOrDefault(item => item.TenantId == TenantId);
            if (settings is null)
            {
                Context.FinanceSettings.Add(new FinanceSettings
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, BaseCurrency = "GHS",
                    SubledgerPostingMode = value!, CreatedAt = DateTime.UtcNow
                });
            }
            else settings.SubledgerPostingMode = value!;
            Context.SaveChanges();
        }
        public void UseApplicantSession(
            Guid registrationId,
            Guid tokenId,
            Guid sessionReference)
        {
            _external = true;
            _authenticationProvider = "ApplicantToken";
            _claims["supplier_applicant_registration"] = registrationId.ToString();
            _claims["supplier_applicant_token"] = tokenId.ToString();
            _claims["supplier_applicant_session"] = sessionReference.ToString();
        }

        public async Task<ProcurementSupplierApplicantSession> SeedApplicantSessionAsync(
            Guid tokenId)
        {
            var access = new ProcurementSupplierApplicantAccess
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                RegistrationId = Registration.Id,
                TokenId = tokenId,
                VerifiedChannel =
                    ProcurementSupplierApplicantVerificationChannel.Email,
                VerifiedContactHashSha256 = new string('a', 64),
                VerifiedContactMasked = "ap***@tdc.test",
                VerifiedContact = Registration.ApplicantEmail!,
                VerifiedAtUtc = DateTime.UtcNow,
                Status =
                    ProcurementSupplierApplicantAccessStatus.ApplicationInProgress,
                CreatedById = UserId,
                IntegrityHash = new string('b', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            var session = new ProcurementSupplierApplicantSession
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ApplicantAccessId = access.Id,
                SessionReference = Guid.NewGuid(),
                Status = ProcurementSupplierApplicantSessionStatus.Active,
                IssuedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                CreatedById = UserId,
                IntegrityHash = new string('c', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            Context.AddRange(access, session);
            await Context.SaveChangesAsync();
            return session;
        }

        public Task BeginTransactionAsync() => _unitOfWork.BeginTransactionAsync();
        public Task RollbackAsync() => _unitOfWork.RollbackAsync();
        public void SeedSystemActor()
        {
            Context.Users.Add(new ApplicationUser
            {
                Id = UserId,
                UserName = "tenant.admin",
                NormalizedUserName = "TENANT.ADMIN",
                FirstName = "Tenant",
                LastName = "Admin",
                TenantId = TenantId,
                IsActive = true
            });
            Context.UserTenants.Add(new UserTenant
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                TenantId = TenantId,
                Status = UserTenantStatus.Active,
                IsDefault = true,
                GrantedAt = DateTime.UtcNow
            });
            Context.SaveChanges();
        }

        private Account Account(AccountType type, string code, string name) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            AccountCode = code,
            AccountNumber = code,
            AccountName = name,
            AccountType = type,
            Status = AccountStatus.Active,
            AllowDirectPosting = true,
            CurrencyCode = "GHS"
        };

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }

    private static JsonSerializerOptions CreateDecisionJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
