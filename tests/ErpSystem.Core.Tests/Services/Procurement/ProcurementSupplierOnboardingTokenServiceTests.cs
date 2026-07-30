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

        fixture.SetUser(Guid.NewGuid());
        var verified = await fixture.Service.ReconcilePaymentAsync(
            issued.Token.Id,
            pending.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "PROVIDER-CONFIRM-0001",
                Notes = "Trusted provider settlement confirmed.",
                RowVersion = pending.RowVersion
            },
            "verify-payment");
        var verificationReplay = await fixture.Service.ReconcilePaymentAsync(
            issued.Token.Id,
            pending.Id,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "PROVIDER-CONFIRM-0001",
                Notes = "Trusted provider settlement confirmed.",
                RowVersion = pending.RowVersion
            },
            "verify-payment");

        verified.Status.Should().Be(ProcurementSupplierOnboardingTokenStatus.Active);
        verified.PaymentStatus.Should()
            .Be(ProcurementSupplierOnboardingPaymentStatus.Reconciled);
        verified.Payments.Should().ContainSingle(item =>
            item.ReceiptNumber == "SUP-ONB-2026-00001" &&
            item.PostingEventId == fixture.PostingEventId &&
            item.JournalEntryId == fixture.JournalEntryId &&
            item.ReconciliationReference == "PROVIDER-CONFIRM-0001");
        fixture.PostedRequest.Should().NotBeNull();
        fixture.PostedRequest!.OriginModuleCode.Should().Be("PROC");
        fixture.PostedRequest.SourceDocumentType.Should()
            .Be("SupplierOnboardingTokenPayment");
        fixture.PostedRequest.Lines.Sum(item => item.DebitAmount).Should()
            .Be(fixture.PostedRequest.Lines.Sum(item => item.CreditAmount));
        fixture.PostedRequest.Lines.Should().Contain(item =>
            item.TransactionTag == "SupplierOnboardingFee" &&
            item.AccountId == fixture.RevenueAccount!.Id);
        verificationReplay.Payments.Should().ContainSingle();
        fixture.FinancePostCount.Should().Be(1,
            "verification replays must not create duplicate Finance postings");
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

        approved.Status.Should().Be(ProcurementSupplierOnboardingTokenStatus.Active);
        approved.PaymentStatus.Should()
            .Be(ProcurementSupplierOnboardingPaymentStatus.Exempt);
        approved.Exemptions.Should().ContainSingle(item =>
            item.Status == ProcurementSupplierOnboardingExemptionStatus.Approved);
    }

    [Fact]
    public async Task FinanceReconciliationRequiresAnIndependentActorAndIsReplaySafe()
    {
        await using var fixture = new Fixture(paid: true);
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

        reconciled.PaymentStatus.Should()
            .Be(ProcurementSupplierOnboardingPaymentStatus.Reconciled);
        replay.Payments.Should().ContainSingle(item =>
            item.ReconciliationReference == "BANK-RECON-001" &&
            item.Status == ProcurementSupplierOnboardingPaymentStatus.Reconciled);
    }

    private static ProcurementControlEventEvidenceReference Evidence(string reference) =>
        new()
        {
            ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
            Reference = reference,
            Label = "Supplier-onboarding decision evidence",
            RequirementKey = "DEC-007"
        };

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _tenantId;
        private Guid _userId;
        private bool _external;
        private string _authenticationProvider = "Local";
        private readonly Dictionary<string, string> _claims = new();
        private readonly UnitOfWork _unitOfWork;

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
                _external ? ["External"] : ["TenantAdmin"]);
            current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => !_external && role == "TenantAdmin");

            _unitOfWork = new UnitOfWork(Context);
            var access = new Mock<IProcurementAccessControlService>();
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
                    It.IsAny<FinancePostingRequestDto>(),
                    It.IsAny<CancellationToken>()))
                .Callback((FinancePostingRequestDto request, CancellationToken _) =>
                {
                    PostedRequest = request;
                    FinancePostCount++;
                })
                .ReturnsAsync(new FinancePostingResultDto
                {
                    PostingEventId = PostingEventId,
                    JournalEntryId = JournalEntryId,
                    JournalEntryNumber = "JE-2026-00001",
                    PostingStatus = "Posted",
                    FunctionalCurrencyCode = "GHS"
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
        public FinancePostingRequestDto? PostedRequest { get; private set; }
        public int FinancePostCount { get; private set; }
        public ProcurementSupplierOnboardingTokenService Service { get; }
        public bool HasActiveTransaction => _unitOfWork.HasActiveTransaction;

        public void SetExternal(bool value) => _external = value;
        public void SetTenant(Guid value) => _tenantId = value;
        public void SetUser(Guid value) => _userId = value;
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
