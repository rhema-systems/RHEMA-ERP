using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ErpSystem.Shared;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierApplicantLifecycleTests
{
    [Fact]
    public async Task ExternalStatusHistoryDoesNotRequireAnInternalUserForeignKey()
    {
        await using var fixture = new Fixture();
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationNumber = "APP26EXT01",
            ApplicantName = "External Applicant",
            ApplicantEmail = "external.history@example.test",
            PartnerType = "Supplier",
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        };
        fixture.Context.BusinessPartnerRegistrations.Add(registration);
        await fixture.Context.SaveChangesAsync();

        var repository = new BusinessPartnerRegistrationRepository(
            fixture.Context,
            NullLogger<BusinessPartnerRegistrationRepository>.Instance);
        await repository.UpdateStatusAsync(
            registration.Id,
            "Submitted",
            changedById: null,
            "Submitted by verified supplier applicant");
        await fixture.Context.SaveChangesAsync();

        var history = await fixture.Context.BusinessPartnerRegistrationStatusHistories
            .SingleAsync(item => item.RegistrationId == registration.Id);
        history.ChangedById.Should().BeNull();
        history.ToStatus.Should().Be("Submitted");
    }

    [Fact]
    public async Task ExistingErpIdentityCannotStartSupplierApplication()
    {
        await using var fixture = new Fixture();
        fixture.Context.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "internal.admin@example.test",
            NormalizedUserName = "INTERNAL.ADMIN@EXAMPLE.TEST",
            Email = "internal.admin@example.test",
            NormalizedEmail = "INTERNAL.ADMIN@EXAMPLE.TEST",
            EmailConfirmed = true,
            FirstName = "Internal",
            LastName = "Administrator",
            TenantId = fixture.TenantId,
            IsActive = true,
            AuthenticationProvider = AuthenticationProvider.LDAP,
            CreatedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var prepare = () => fixture.Service.PrepareVerificationChallengeAsync(
            fixture.TenantId,
            ProcurementSupplierApplicantVerificationChannel.Email,
            "INTERNAL.ADMIN@example.test");
        (await prepare.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should()
            .Be("SUPPLIER_APPLICANT_CONTACT_ALREADY_REGISTERED");

        var create = () => fixture.Service.CreateVerifiedApplicationAsync(
            new VerifyAndIssueSupplierApplicantTokenRequest
            {
                TenantId = fixture.TenantId,
                Channel = ProcurementSupplierApplicantVerificationChannel.Email,
                Contact = "INTERNAL.ADMIN@example.test",
                CompanyName = "Unsafe Supplier",
                RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods
            },
            "existing-internal-identity");

        (await create.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should()
            .Be("SUPPLIER_APPLICANT_CONTACT_ALREADY_REGISTERED");
        (await fixture.Context.BusinessPartnerRegistrations.AnyAsync())
            .Should().BeFalse();
        (await fixture.Context.ProcurementSupplierApplicantAccesses.AnyAsync())
            .Should().BeFalse();
        (await fixture.Context.ProcurementSupplierOnboardingTokens.AnyAsync())
            .Should().BeFalse();
    }

    [Fact]
    public async Task ApplicationTokenDeliveryAcceptsUppercaseOnboardingHash()
    {
        await using var fixture = new Fixture();
        var issued = await fixture.Service.CreateVerifiedApplicationAsync(
            new VerifyAndIssueSupplierApplicantTokenRequest
            {
                TenantId = fixture.TenantId,
                Channel = ProcurementSupplierApplicantVerificationChannel.Email,
                Contact = "token-delivery@example.test",
                CompanyName = "Token Delivery Supplier",
                RegistrationCategory = ProcurementSupplierRegistrationCategory.Services
            },
            "issue-token-delivery-test");
        const string plaintextToken = "Application_Token-Case-Safe-001";
        var token = await fixture.Context.ProcurementSupplierOnboardingTokens
            .SingleAsync(item => item.Id == issued.TokenId);
        token.TokenHashSha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken)));
        await fixture.Context.SaveChangesAsync();

        var delivery = await fixture.Service.DeliverApplicationTokenAsync(
            token.Id,
            plaintextToken,
            "deliver-uppercase-token");

        delivery.ApplicantAccessFound.Should().BeTrue();
        delivery.Delivered.Should().BeTrue();
        delivery.Status.Should().Be("Sent");
        fixture.DeliveredMessages.Should().ContainSingle(message =>
            message.Contains(plaintextToken, StringComparison.Ordinal));
        var access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync(item => item.TokenId == token.Id);
        access.NotificationAttemptCount.Should().Be(1);
        access.LastNotificationStatus.Should().Be("ApplicationTokenSent");
    }

    [Fact]
    public async Task PaidVerifiedApplicationWithholdsTokenAndCreatesOnlyPaymentSession()
    {
        await using var fixture = new Fixture(paid: true);
        fixture.SeedSystemActor();

        var issued = await fixture.Service.CreateVerifiedApplicationAsync(
            new VerifyAndIssueSupplierApplicantTokenRequest
            {
                TenantId = fixture.TenantId,
                Channel = ProcurementSupplierApplicantVerificationChannel.Email,
                Contact = "paid-applicant@example.test",
                CompanyName = "Paid Applicant Limited",
                RegistrationCategory = ProcurementSupplierRegistrationCategory.Services
            },
            "paid-verified-application");

        issued.PlaintextToken.Should().BeNull();
        issued.TokenStatus.Should()
            .Be(ProcurementSupplierOnboardingTokenStatus.AwaitingPayment);
        issued.RestrictedSession.Should().NotBeNull();
        issued.RestrictedSession!.PaymentOnly.Should().BeTrue();
        issued.RestrictedSession.TokenId.Should().Be(issued.TokenId);
        var access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync();
        var registration = await fixture.Context.BusinessPartnerRegistrations
            .SingleAsync();
        var session = await fixture.Context.ProcurementSupplierApplicantSessions
            .SingleAsync();
        issued.RestrictedSession.SessionId.Should().Be(session.Id);
        issued.RestrictedSession.ApplicantActorId.Should().Be(access.Id);
        registration.CreatedBy.Should().Be("Verified Supplier Applicant");
        registration.CreatedById.Should().Be(access.Id);
        access.CreatedById.Should().Be(access.Id);
        session.CreatedById.Should().Be(access.Id);
        access.Id.Should().NotBe(fixture.ActorId,
            "a pre-approval applicant must not borrow the tenant administrator identity");
    }

    [Fact]
    public async Task ReverifiedPaidContactResumesOneApplicationAndRotatesRestrictedSession()
    {
        await using var fixture = new Fixture(paid: true);
        fixture.SeedSystemActor();

        var first = await fixture.Service.CreateVerifiedApplicationAsync(
            new VerifyAndIssueSupplierApplicantTokenRequest
            {
                TenantId = fixture.TenantId,
                Channel = ProcurementSupplierApplicantVerificationChannel.Sms,
                Contact = "024 123 4567",
                CompanyName = "Recoverable Supplier Limited",
                RegistrationCategory = ProcurementSupplierRegistrationCategory.Services
            },
            "paid-recovery-first");
        first.RestrictedSession.Should().NotBeNull();
        var firstSession = first.RestrictedSession!;

        var preparation = await fixture.Service.PrepareVerificationChallengeAsync(
            fixture.TenantId,
            ProcurementSupplierApplicantVerificationChannel.Sms,
            "+233 24 123 4567");
        preparation.ResumesExistingApplication.Should().BeTrue();
        preparation.NormalizedContact.Should().Be("+233241234567");

        var resumed = await fixture.Service.CreateVerifiedApplicationAsync(
            new VerifyAndIssueSupplierApplicantTokenRequest
            {
                TenantId = fixture.TenantId,
                Channel = ProcurementSupplierApplicantVerificationChannel.Sms,
                Contact = "+233 24 123 4567",
                CompanyName = "A changed name must not create a duplicate",
                RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods
            },
            "paid-recovery-second");

        resumed.ResumedExistingApplication.Should().BeTrue();
        resumed.RegistrationId.Should().Be(first.RegistrationId);
        resumed.TokenId.Should().Be(first.TokenId);
        resumed.PlaintextToken.Should().BeNull();
        resumed.RestrictedSession.Should().NotBeNull();
        resumed.RestrictedSession!.PaymentOnly.Should().BeTrue();
        resumed.RestrictedSession.SessionReference.Should()
            .NotBe(firstSession.SessionReference);
        (await fixture.Context.BusinessPartnerRegistrations.CountAsync()).Should().Be(1);
        (await fixture.Context.ProcurementSupplierApplicantAccesses.CountAsync()).Should().Be(1);
        (await fixture.Context.ProcurementSupplierOnboardingTokens.CountAsync()).Should().Be(1);

        var sessions = await fixture.Context.ProcurementSupplierApplicantSessions
            .OrderBy(item => item.IssuedAtUtc)
            .ToListAsync();
        sessions.Should().HaveCount(2);
        sessions.Single(item => item.SessionReference == firstSession.SessionReference)
            .Status.Should().Be(ProcurementSupplierApplicantSessionStatus.Revoked);
        sessions.Single(item => item.SessionReference == resumed.RestrictedSession.SessionReference)
            .Status.Should().Be(ProcurementSupplierApplicantSessionStatus.Active);
    }

    [Fact]
    public async Task VerifiedContactMigratesRetainedDraftWithoutReplacingAuditOwner()
    {
        await using var fixture = new Fixture();
        fixture.SeedSystemActor();
        var originalOwnerId = Guid.NewGuid();
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationNumber = "LEGACY-APP-001",
            ApplicantName = "Retained Supplier",
            ApplicantEmail = "retained@example.test",
            PartnerType = "Supplier",
            RegistrationCategory = ProcurementSupplierRegistrationCategory.Services,
            Status = "MoreInfoRequired",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            CreatedBy = "Legacy External Applicant",
            CreatedById = originalOwnerId
        };
        fixture.Context.BusinessPartnerRegistrations.Add(registration);
        await fixture.Context.SaveChangesAsync();

        var issued = await fixture.Service.CreateVerifiedApplicationAsync(
            new VerifyAndIssueSupplierApplicantTokenRequest
            {
                TenantId = fixture.TenantId,
                Channel = ProcurementSupplierApplicantVerificationChannel.Email,
                Contact = " RETAINED@EXAMPLE.TEST ",
                RetainedRegistrationId = registration.Id
            },
            "migrate-retained-draft");

        issued.RegistrationId.Should().Be(registration.Id);
        issued.RegistrationNumber.Should().Be("LEGACY-APP-001");
        (await fixture.Context.BusinessPartnerRegistrations
                .IgnoreQueryFilters().CountAsync())
            .Should().Be(1);
        var retained = await fixture.Context.BusinessPartnerRegistrations
            .IgnoreQueryFilters()
            .SingleAsync();
        retained.Status.Should().Be("MoreInfoRequired");
        retained.ApplicantName.Should().Be("Retained Supplier");
        retained.CreatedById.Should().Be(originalOwnerId);
        var access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync();
        access.RegistrationId.Should().Be(registration.Id);
        access.CreatedById.Should().Be(access.Id);
        access.Id.Should().NotBe(fixture.ActorId);
    }

    [Fact]
    public async Task RetainedDraftMigrationRejectsContactThatIsNotRecordedOnApplication()
    {
        await using var fixture = new Fixture();
        fixture.SeedSystemActor();
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationNumber = "LEGACY-APP-002",
            ApplicantName = "Retained Supplier",
            ApplicantEmail = "owner@example.test",
            PartnerType = "Supplier",
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            CreatedById = Guid.NewGuid()
        };
        fixture.Context.BusinessPartnerRegistrations.Add(registration);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.CreateVerifiedApplicationAsync(
            new VerifyAndIssueSupplierApplicantTokenRequest
            {
                TenantId = fixture.TenantId,
                Channel = ProcurementSupplierApplicantVerificationChannel.Email,
                Contact = "attacker@example.test",
                RetainedRegistrationId = registration.Id
            },
            "reject-wrong-retained-contact");

        (await action.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should()
            .Be("SUPPLIER_APPLICANT_RETAINED_APPLICATION_NOT_ELIGIBLE");
        (await fixture.Context.ProcurementSupplierApplicantAccesses.CountAsync())
            .Should().Be(0);
        (await fixture.Context.ProcurementSupplierOnboardingTokens.CountAsync())
            .Should().Be(0);
    }

    [Theory]
    [InlineData(
        ProcurementSupplierApplicantVerificationChannel.Email,
        " APPLICANT@EXAMPLE.TEST ",
        "applicant@example.test",
        null)]
    [InlineData(
        ProcurementSupplierApplicantVerificationChannel.Sms,
        " +233 24-123-4567 ",
        null,
        "+233241234567")]
    public async Task VerifiedContactRunsRepeatedRestrictedSessionsAndRejectedTerminalLifecycle(
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact,
        string? expectedEmail,
        string? expectedPhone)
    {
        await using var fixture = new Fixture();
        fixture.SeedSystemActor();
        var issued = await fixture.Service.CreateVerifiedApplicationAsync(
            new VerifyAndIssueSupplierApplicantTokenRequest
            {
                TenantId = fixture.TenantId,
                Channel = channel,
                Contact = contact,
                CompanyName = "Applicant Limited",
                RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods
            },
            "verified-application");

        issued.PlaintextToken.Should().Be("one-time-application-token");
        var registration = await fixture.Context.BusinessPartnerRegistrations
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == issued.RegistrationId);
        registration.ApplicantEmail.Should().Be(expectedEmail);
        registration.ApplicantPhone.Should().Be(expectedPhone);

        var firstSession = await fixture.Service.StartSessionAsync(
            new StartSupplierApplicantSessionRequest
            {
                TenantId = fixture.TenantId,
                ApplicationToken = issued.PlaintextToken!
            },
            "session-start-one");
        var secondSession = await fixture.Service.StartSessionAsync(
            new StartSupplierApplicantSessionRequest
            {
                TenantId = fixture.TenantId,
                ApplicationToken = issued.PlaintextToken!
            },
            "session-start-two");
        firstSession.PaymentOnly.Should().BeFalse();
        secondSession.SessionReference.Should().NotBe(firstSession.SessionReference);
        (await fixture.Context.ProcurementSupplierApplicantSessions.CountAsync())
            .Should().Be(2);

        fixture.UseApplicant(Guid.NewGuid());
        var crossTenant = () => fixture.Service.ValidateSessionAsync(
            firstSession.SessionReference, "cross-tenant");
        (await crossTenant.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_SESSION_TENANT_MISMATCH");

        fixture.UseApplicant(fixture.TenantId);
        (await fixture.Service.ValidateSessionAsync(
                firstSession.SessionReference, "first-session-reuse"))
            .RegistrationId.Should().Be(issued.RegistrationId);
        (await fixture.Service.ValidateSessionAsync(
                secondSession.SessionReference, "second-session-reuse"))
            .RegistrationId.Should().Be(issued.RegistrationId);
        var initialPortal = await fixture.Service.GetPortalAsync(
            firstSession.SessionReference, "initial-status");
        initialPortal.Status.Should().Be("Draft");
        initialPortal.TokenId.Should().Be(issued.TokenId);
        initialPortal.CanEdit.Should().BeTrue();
        initialPortal.CanSubmit.Should().BeTrue();

        var updated = await fixture.Service.UpdateApplicationAsync(
            firstSession.SessionReference,
            new UpdateSupplierApplicantApplicationRequest
            {
                CompanyName = "Applicant Limited Updated",
                RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods,
                Email = expectedEmail,
                Phone = expectedPhone,
                RegistrationData = "{\"complete\":true}"
            },
            "draft-update");
        updated.CompanyName.Should().Be("Applicant Limited Updated");

        var submitted = await fixture.Service.SubmitAsync(
            secondSession.SessionReference, "submit-application");
        submitted.Status.Should().Be("Submitted");
        (await fixture.Service.GetPortalAsync(
                firstSession.SessionReference, "submitted-status"))
            .Status.Should().Be("Submitted");

        fixture.UseInternal();
        await fixture.Service.CloseForTerminalRegistrationAsync(
            issued.RegistrationId,
            "Rejected",
            fixture.ActorId,
            "terminal-rejection");

        var access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync();
        access.Status.Should().Be(ProcurementSupplierApplicantAccessStatus.Rejected);
        access.TerminalOutcome.Should().Be("Rejected");
        (await fixture.Context.ProcurementSupplierApplicantSessions
                .AllAsync(item =>
                    item.Status == ProcurementSupplierApplicantSessionStatus.Revoked))
            .Should().BeTrue();

        var postCompletion = () => fixture.Service.StartSessionAsync(
            new StartSupplierApplicantSessionRequest
            {
                TenantId = fixture.TenantId,
                ApplicationToken = issued.PlaintextToken!
            },
            "post-completion");
        (await postCompletion.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_APPLICATION_COMPLETE");
    }

    [Fact]
    public async Task ApprovalDeliveryFailureCanRetryThenActivateExactlyOnce()
    {
        await using var fixture = new Fixture();
        var subject = fixture.SeedApprovedApplication();
        fixture.FailCredentialDelivery = true;

        await fixture.Service.ProvisionApprovedSupplierAsync(
            subject.RegistrationId,
            subject.BusinessPartnerId,
            fixture.ActorId,
            "approval-first-attempt");

        var access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync();
        access.Status.Should()
            .Be(ProcurementSupplierApplicantAccessStatus.ActivationFailed);
        access.NotificationAttemptCount.Should().Be(1);
        access.ApprovedUserId.Should().NotBeNull();
        var approvedUser = await fixture.Context.Users
            .SingleAsync(item => item.Id == access.ApprovedUserId);
        approvedUser.MustChangePassword.Should().BeTrue();
        approvedUser.TemporaryPasswordExpiresAtUtc.Should().NotBeNull();
        fixture.DeliveredMessages.Should().BeEmpty();
        (await fixture.Context.ProcurementSupplierApplicantSessions.SingleAsync())
            .Status.Should().Be(ProcurementSupplierApplicantSessionStatus.Revoked);

        fixture.FailCredentialDelivery = false;
        await fixture.Service.ProvisionApprovedSupplierAsync(
            subject.RegistrationId,
            subject.BusinessPartnerId,
            fixture.ActorId,
            "approval-retry");

        access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync();
        access.Status.Should()
            .Be(ProcurementSupplierApplicantAccessStatus.CredentialDelivered);
        access.NotificationAttemptCount.Should().Be(2);
        access.TemporaryCredentialExpiresAtUtc.Should().BeCloseTo(
            DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(2));
        (await fixture.Context.UserTenants.CountAsync(item =>
            item.UserId == access.ApprovedUserId &&
            item.TenantId == fixture.TenantId)).Should().Be(1);
        (await fixture.Context.BusinessPartnerUsers.CountAsync(item =>
            item.UserId == access.ApprovedUserId &&
            item.BusinessPartnerId == subject.BusinessPartnerId)).Should().Be(1);
        var partner = await fixture.Context.BusinessPartners
            .SingleAsync(item => item.Id == subject.BusinessPartnerId);
        partner.UserId.Should().Be(access.ApprovedUserId);
        partner.CreatedById.Should().Be(fixture.ActorId);
        (await fixture.Context.BusinessPartnerRegistrations
                .SingleAsync(item => item.Id == subject.RegistrationId))
            .CreatedById.Should().Be(fixture.ActorId);
        fixture.DeliveredMessages.Should().ContainSingle()
            .Which.Should().Contain(access.LoginIdentifier)
            .And.Contain(fixture.LastTemporaryPassword);

        partner.UserId = fixture.ActorId;
        var membership = await fixture.Context.BusinessPartnerUsers.SingleAsync(
            item => item.BusinessPartnerId == subject.BusinessPartnerId &&
                    item.UserId == access.ApprovedUserId);
        membership.IsActive = false;
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.ProvisionApprovedSupplierAsync(
            subject.RegistrationId,
            subject.BusinessPartnerId,
            fixture.ActorId,
            "approval-link-repair");
        partner.UserId.Should().Be(access.ApprovedUserId);
        membership.IsActive.Should().BeTrue();
        (await fixture.Context.BusinessPartnerUsers.CountAsync(item =>
            item.UserId == access.ApprovedUserId &&
            item.BusinessPartnerId == subject.BusinessPartnerId)).Should().Be(1);

        fixture.UseSupplierAccount(Guid.NewGuid());
        var wrongActor = () => fixture.Service.CompleteCredentialActivationAsync(
            access.ApprovedUserId!.Value, "activate-wrong-actor");
        (await wrongActor.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_ACTIVATION_ACTOR_MISMATCH");
        fixture.UseSupplierAccount(access.ApprovedUserId!.Value);
        await fixture.Service.CompleteCredentialActivationAsync(
            access.ApprovedUserId!.Value, "activate");
        var activatedAt = (await fixture.Context.ProcurementSupplierApplicantAccesses
                .SingleAsync())
            .CredentialActivatedAtUtc;
        activatedAt.Should().NotBeNull();

        await fixture.Service.CompleteCredentialActivationAsync(
            access.ApprovedUserId.Value, "activate-replay");
        (await fixture.Context.ProcurementSupplierApplicantAccesses.SingleAsync())
            .CredentialActivatedAtUtc.Should().Be(activatedAt);

        fixture.UseInternal();
        var resend = () => fixture.Service.ResendTemporaryCredentialAsync(
            subject.RegistrationId, "resend-after-activation");
        (await resend.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_RESEND_NOT_ALLOWED");
    }

    [Fact]
    public async Task ApprovalPreflightRejectsExistingErpIdentityBeforeProvisioning()
    {
        await using var fixture = new Fixture();
        var subject = fixture.SeedApprovedApplication();
        fixture.Context.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "approved@example.test",
            NormalizedUserName = "APPROVED@EXAMPLE.TEST",
            Email = "approved@example.test",
            NormalizedEmail = "APPROVED@EXAMPLE.TEST",
            EmailConfirmed = true,
            FirstName = "Existing",
            LastName = "Administrator",
            TenantId = fixture.TenantId,
            IsActive = true,
            AuthenticationProvider = AuthenticationProvider.LDAP,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        });
        await fixture.Context.SaveChangesAsync();

        var validate = () => fixture.Service
            .ValidateApprovedSupplierProvisioningAsync(
                subject.RegistrationId,
                fixture.ActorId,
                "approval-identity-preflight");

        var exception = (await validate.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which;
        exception.Code.Should().Be("SUPPLIER_APPLICANT_LOGIN_ALREADY_EXISTS");
        exception.Message.Should().Contain("existing ERP account");
        (await fixture.Context.ProcurementSupplierApplicantAccesses.SingleAsync())
            .ApprovedUserId.Should().BeNull();
        (await fixture.Context.BusinessPartnerUsers.AnyAsync())
            .Should().BeFalse();
    }

    [Fact]
    public async Task CorrectedVerifiedContactIsAtomicAuditedAndProvisionsSupplier()
    {
        await using var fixture = new Fixture();
        var subject = fixture.SeedApprovedApplication();
        var access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync(item => item.RegistrationId == subject.RegistrationId);
        access.Status =
            ProcurementSupplierApplicantAccessStatus.ApprovedPendingCredentialDelivery;
        access.TerminalOutcome = "Approved";
        access.TerminalAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var registration = await fixture.Context.BusinessPartnerRegistrations
            .SingleAsync(item => item.Id == subject.RegistrationId);
        registration.RegistrationDataJson =
            "{\"email\":\"approved@example.test\",\"companyName\":\"Approved Supplier\"}";
        var partner = await fixture.Context.BusinessPartners
            .SingleAsync(item => item.Id == subject.BusinessPartnerId);
        partner.PrimaryEmail = "approved@example.test";
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "approved@example.test",
            NormalizedUserName = "APPROVED@EXAMPLE.TEST",
            Email = "approved@example.test",
            NormalizedEmail = "APPROVED@EXAMPLE.TEST",
            FirstName = "Existing",
            LastName = "Administrator",
            TenantId = fixture.TenantId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CorrectVerifiedContactAndRetryAsync(
            subject.RegistrationId,
            new CorrectSupplierApplicantVerifiedContactRequest
            {
                Channel = ProcurementSupplierApplicantVerificationChannel.Email,
                Contact = " SUPPLIER.OWNER@EXAMPLE.TEST ",
                Reason = "Replace approved@example.test with SUPPLIER.OWNER@EXAMPLE.TEST after supplier verification."
            },
            fixture.ActorId,
            "correct-approved-contact");

        result.ContactCorrected.Should().BeTrue();
        result.ProvisioningRetried.Should().BeTrue();
        result.CredentialDelivered.Should().BeTrue();
        result.MaskedContact.Should().Be("su***@example.test");
        access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync(item => item.RegistrationId == subject.RegistrationId);
        access.VerifiedContact.Should().Be("supplier.owner@example.test");
        access.VerifiedContactMasked.Should().Be("su***@example.test");
        access.VerifiedContactHashSha256.Should().HaveLength(64);
        access.IntegrityHash.Should().HaveLength(64);
        access.Status.Should()
            .Be(ProcurementSupplierApplicantAccessStatus.CredentialDelivered);
        registration = await fixture.Context.BusinessPartnerRegistrations
            .SingleAsync(item => item.Id == subject.RegistrationId);
        registration.ApplicantEmail.Should().Be("supplier.owner@example.test");
        registration.RegistrationDataJson.Should()
            .Contain("supplier.owner@example.test")
            .And.Contain("Approved Supplier");
        partner = await fixture.Context.BusinessPartners
            .SingleAsync(item => item.Id == subject.BusinessPartnerId);
        partner.PrimaryEmail.Should().Be("supplier.owner@example.test");
        var supplierUser = await fixture.Context.Users
            .SingleAsync(item => item.Id == access.ApprovedUserId);
        supplierUser.Email.Should().Be("supplier.owner@example.test");
        fixture.DeliveredMessages.Should().ContainSingle(message =>
            message.Contains("supplier.owner@example.test", StringComparison.Ordinal));

        var correctionEvent = fixture.RecordedControlEvents.Single(item =>
            item.Action == "ApplicantVerifiedContactCorrected");
        var eventJson = System.Text.Json.JsonSerializer.Serialize(correctionEvent);
        eventJson.Should().NotContain("approved@example.test");
        eventJson.Should().NotContain("supplier.owner@example.test");
        correctionEvent.Reason.Should().Contain("[redacted contact]");
    }

    [Fact]
    public async Task ContactCorrectionRejectsErpIdentityAndAnotherActiveApplication()
    {
        await using var fixture = new Fixture();
        var subject = fixture.SeedApprovedApplication();
        var access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync(item => item.RegistrationId == subject.RegistrationId);
        access.Status =
            ProcurementSupplierApplicantAccessStatus.ApprovedPendingCredentialDelivery;
        access.TerminalOutcome = "Approved";
        access.TerminalAtUtc = DateTime.UtcNow;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "internal@example.test",
            NormalizedUserName = "INTERNAL@EXAMPLE.TEST",
            Email = "internal@example.test",
            NormalizedEmail = "INTERNAL@EXAMPLE.TEST",
            FirstName = "Internal",
            LastName = "User",
            TenantId = fixture.TenantId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var identityConflict = () => fixture.Service
            .PrepareVerifiedContactCorrectionAsync(
                subject.RegistrationId,
                new PrepareSupplierApplicantContactCorrectionRequest
                {
                    Channel = ProcurementSupplierApplicantVerificationChannel.Email,
                    Contact = "internal@example.test"
                },
                fixture.ActorId,
                "contact-conflict-identity");
        (await identityConflict.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_CONTACT_ALREADY_REGISTERED");

        await fixture.Service.CreateVerifiedApplicationAsync(
            new VerifyAndIssueSupplierApplicantTokenRequest
            {
                TenantId = fixture.TenantId,
                Channel = ProcurementSupplierApplicantVerificationChannel.Email,
                Contact = "other.applicant@example.test",
                CompanyName = "Other Applicant",
                RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods
            },
            "other-active-application");
        var applicationConflict = () => fixture.Service
            .PrepareVerifiedContactCorrectionAsync(
                subject.RegistrationId,
                new PrepareSupplierApplicantContactCorrectionRequest
                {
                    Channel = ProcurementSupplierApplicantVerificationChannel.Email,
                    Contact = "other.applicant@example.test"
                },
                fixture.ActorId,
                "contact-conflict-application");
        (await applicationConflict.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_CONTACT_IN_USE");
    }

    [Fact]
    public async Task ContactCorrectionChannelSwitchRemovesOnlyMatchingOldContact()
    {
        await using var fixture = new Fixture();
        var subject = fixture.SeedApprovedApplication();
        var access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync(item => item.RegistrationId == subject.RegistrationId);
        access.Status =
            ProcurementSupplierApplicantAccessStatus.ApprovedPendingCredentialDelivery;
        access.TerminalOutcome = "Approved";
        access.TerminalAtUtc = DateTime.UtcNow;
        var registration = await fixture.Context.BusinessPartnerRegistrations
            .SingleAsync(item => item.Id == subject.RegistrationId);
        registration.ApplicantEmail = "approved@example.test";
        registration.RegistrationDataJson =
            "{\"email\":\"approved@example.test\",\"phone\":null," +
            "\"contactPersonEmail\":\"genuine.contact@example.test\"}";
        var partner = await fixture.Context.BusinessPartners
            .SingleAsync(item => item.Id == subject.BusinessPartnerId);
        partner.PrimaryEmail = "approved@example.test";
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CorrectVerifiedContactAndRetryAsync(
            subject.RegistrationId,
            new CorrectSupplierApplicantVerifiedContactRequest
            {
                Channel = ProcurementSupplierApplicantVerificationChannel.Sms,
                Contact = "+233 24 555 0199",
                Reason = "Move credential delivery to the supplier-owned verified phone."
            },
            fixture.ActorId,
            "correct-channel-switch");

        result.CredentialDelivered.Should().BeTrue();
        registration = await fixture.Context.BusinessPartnerRegistrations
            .SingleAsync(item => item.Id == subject.RegistrationId);
        registration.ApplicantEmail.Should().BeNull();
        registration.ApplicantPhone.Should().Be("+233245550199");
        using (var registrationData = System.Text.Json.JsonDocument.Parse(
                   registration.RegistrationDataJson))
        {
            var root = registrationData.RootElement;
            root.GetProperty("email").ValueKind.Should()
                .Be(System.Text.Json.JsonValueKind.Null);
            root.GetProperty("phone").GetString().Should().Be("+233245550199");
            root.GetProperty("contactPersonEmail").GetString().Should()
                .Be("genuine.contact@example.test");
        }
        partner = await fixture.Context.BusinessPartners
            .SingleAsync(item => item.Id == subject.BusinessPartnerId);
        partner.PrimaryEmail.Should().BeNull();
        partner.PrimaryPhone.Should().Be("+233245550199");
        access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync(item => item.RegistrationId == subject.RegistrationId);
        var supplierUser = await fixture.Context.Users
            .SingleAsync(item => item.Id == access.ApprovedUserId);
        supplierUser.Email.Should().BeNull();
        supplierUser.PhoneNumber.Should().Be("+233245550199");
    }

    [Fact]
    public async Task ActivationRetryResumesStrictlyMatchedPartialIdentity()
    {
        await using var fixture = new Fixture
        {
            PersistIdentityImmediately = true,
            FailRoleAssignmentOnce = true
        };
        var subject = fixture.SeedApprovedApplication();

        var firstAttempt = () => fixture.Service.ProvisionApprovedSupplierAsync(
            subject.RegistrationId,
            subject.BusinessPartnerId,
            fixture.ActorId,
            "approval-partial-identity");
        (await firstAttempt.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_IDENTITY_PROVISION_FAILED");

        var partialUser = await fixture.Context.Users
            .SingleAsync(item => item.Email == "approved@example.test");
        (await fixture.Context.ProcurementSupplierApplicantAccesses.SingleAsync())
            .ApprovedUserId.Should().BeNull();
        (await fixture.Context.UserTenants.AnyAsync(item =>
            item.UserId == partialUser.Id)).Should().BeFalse();
        (await fixture.Context.BusinessPartnerUsers.AnyAsync(item =>
            item.UserId == partialUser.Id)).Should().BeFalse();

        await fixture.Service.RetryApprovedSupplierActivationAsync(
            subject.RegistrationId,
            fixture.ActorId,
            "approval-partial-identity-retry");

        var access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .SingleAsync();
        access.ApprovedUserId.Should().Be(partialUser.Id);
        access.Status.Should()
            .Be(ProcurementSupplierApplicantAccessStatus.CredentialDelivered);
        (await fixture.Context.Users.CountAsync(item =>
            item.Email == "approved@example.test")).Should().Be(1);
        (await fixture.Context.UserTenants.CountAsync(item =>
            item.UserId == partialUser.Id &&
            item.TenantId == fixture.TenantId)).Should().Be(1);
        (await fixture.Context.BusinessPartnerUsers.CountAsync(item =>
            item.UserId == partialUser.Id &&
            item.BusinessPartnerId == subject.BusinessPartnerId)).Should().Be(1);
        (await fixture.Context.BusinessPartners.SingleAsync(item =>
            item.Id == subject.BusinessPartnerId)).UserId.Should().Be(partialUser.Id);
        fixture.DeliveredMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task ActivationRetryDoesNotClaimLinkedMatchingIdentity()
    {
        await using var fixture = new Fixture();
        var subject = fixture.SeedApprovedApplication();
        var registration = await fixture.Context.BusinessPartnerRegistrations
            .SingleAsync(item => item.Id == subject.RegistrationId);
        var existingUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "approved@example.test",
            NormalizedUserName = "APPROVED@EXAMPLE.TEST",
            Email = "approved@example.test",
            NormalizedEmail = "APPROVED@EXAMPLE.TEST",
            EmailConfirmed = true,
            FirstName = registration.ApplicantName,
            LastName = "Supplier",
            TenantId = fixture.TenantId,
            IsActive = true,
            AuthenticationProvider = AuthenticationProvider.Local,
            MustChangePassword = true,
            TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAt = registration.ApprovedDate!.Value.AddSeconds(1),
            CreatedBy = fixture.ActorId.ToString()
        };
        fixture.Context.Users.Add(existingUser);
        fixture.Context.UserTenants.Add(new UserTenant
        {
            Id = Guid.NewGuid(),
            UserId = existingUser.Id,
            TenantId = fixture.TenantId,
            AccessLevel = UserTenantAccessLevel.Standard,
            Status = UserTenantStatus.Active,
            IsDefault = true,
            GrantedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var retry = () => fixture.Service.ProvisionApprovedSupplierAsync(
            subject.RegistrationId,
            subject.BusinessPartnerId,
            fixture.ActorId,
            "approval-existing-identity");

        (await retry.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_LOGIN_ALREADY_EXISTS");
        (await fixture.Context.ProcurementSupplierApplicantAccesses.SingleAsync())
            .ApprovedUserId.Should().BeNull();
        (await fixture.Context.BusinessPartnerUsers.AnyAsync(item =>
            item.UserId == existingUser.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task TenantAdministratorCannotResendOrRetryAnotherTenantsApplication()
    {
        await using var fixture = new Fixture();
        var subject = fixture.SeedApprovedApplication();
        var foreignTenantId = Guid.NewGuid();
        var registration = await fixture.Context.BusinessPartnerRegistrations
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == subject.RegistrationId);
        var access = await fixture.Context.ProcurementSupplierApplicantAccesses
            .IgnoreQueryFilters()
            .SingleAsync(item => item.RegistrationId == subject.RegistrationId);
        var token = await fixture.Context.ProcurementSupplierOnboardingTokens
            .IgnoreQueryFilters()
            .SingleAsync(item => item.RegistrationId == subject.RegistrationId);
        var partner = await fixture.Context.BusinessPartners
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == subject.BusinessPartnerId);
        registration.TenantId = foreignTenantId;
        access.TenantId = foreignTenantId;
        token.TenantId = foreignTenantId;
        partner.TenantId = foreignTenantId;
        await fixture.Context.SaveChangesAsync();

        var resend = () => fixture.Service.ResendTemporaryCredentialAsync(
            subject.RegistrationId, "foreign-resend");
        var retry = () => fixture.Service.RetryApprovedSupplierActivationAsync(
            subject.RegistrationId, fixture.ActorId, "foreign-retry");

        (await resend.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_ACCESS_NOT_FOUND");
        (await retry.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_REGISTRATION_NOT_FOUND");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly bool _paid;
        private Guid _currentTenantId;
        private Guid _currentUserId;
        private bool _external;
        private string _registrationStatus = "Draft";
        private readonly Dictionary<Guid, ProcurementSupplierOnboardingTokenDto> _tokens = new();
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<RoleManager<ApplicationRole>> _roleManager;
        private readonly Mock<IBusinessPartnerRegistrationService> _registrations = new();
        private readonly Mock<IProcurementSupplierOnboardingTokenService> _tokenService = new();
        private readonly Mock<INotificationService> _notifications = new();
        private readonly Dictionary<Guid, HashSet<string>> _userRoles = new();

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public bool FailCredentialDelivery { get; set; }
        public bool PersistIdentityImmediately { get; set; }
        public bool FailRoleAssignmentOnce { get; set; }
        public string LastTemporaryPassword { get; private set; } = string.Empty;
        public List<string> DeliveredMessages { get; } = [];
        public List<ProcurementControlEventWriteRequest> RecordedControlEvents { get; } = [];
        public ApplicationDbContext Context { get; }
        public ProcurementSupplierApplicantAccessService Service { get; }

        public Fixture(bool paid = false)
        {
            _paid = paid;
            _currentTenantId = TenantId;
            _currentUserId = ActorId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(
                    InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "TDC",
                Name = "TDC",
                Status = TenantStatus.Active
            });
            Context.SaveChanges();
            _unitOfWork = new UnitOfWork(Context);

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(() => _external);
            current.SetupGet(item => item.TenantId).Returns(() => _currentTenantId);
            current.SetupGet(item => item.UserId).Returns(() => _currentUserId);
            current.SetupGet(item => item.Username).Returns("supplier-controls");
            current.SetupGet(item => item.FullName).Returns("Supplier Controls");
            current.SetupGet(item => item.Roles)
                .Returns(() => _external ? ["ExternalUser"] : ["TenantAdmin"]);
            current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => !_external && role == "TenantAdmin");

            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object,
                null!,
                null!,
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                null!,
                null!,
                null!,
                null!);
            _userManager.SetupGet(item => item.Users).Returns(Context.Users);
            _userManager.Setup(item => item.CreateAsync(
                    It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Callback<ApplicationUser, string>((user, password) =>
                {
                    LastTemporaryPassword = password;
                    Context.Users.Add(user);
                    if (PersistIdentityImmediately)
                        Context.SaveChanges();
                })
                .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(item => item.AddToRolesAsync(
                    It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
                .Returns<ApplicationUser, IEnumerable<string>>((user, roles) =>
                {
                    if (FailRoleAssignmentOnce)
                    {
                        FailRoleAssignmentOnce = false;
                        return Task.FromResult(IdentityResult.Failed(
                            new IdentityError
                            {
                                Code = "RoleAssignmentUnavailable",
                                Description = "Role assignment is temporarily unavailable."
                            }));
                    }

                    if (!_userRoles.TryGetValue(user.Id, out var assigned))
                    {
                        assigned = new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase);
                        _userRoles[user.Id] = assigned;
                    }
                    assigned.UnionWith(roles);
                    return Task.FromResult(IdentityResult.Success);
                });
            _userManager.Setup(item => item.GetRolesAsync(
                    It.IsAny<ApplicationUser>()))
                .ReturnsAsync((ApplicationUser user) =>
                    (IList<string>)(_userRoles.TryGetValue(
                        user.Id,
                        out var roles)
                        ? roles.ToList()
                        : []));
            _userManager.Setup(item => item.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(item => item.GeneratePasswordResetTokenAsync(
                    It.IsAny<ApplicationUser>()))
                .ReturnsAsync("reset-token");
            _userManager.Setup(item => item.ResetPasswordAsync(
                    It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<ApplicationUser, string, string>((_, _, password) =>
                    LastTemporaryPassword = password)
                .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(item => item.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((string id) => Context.Users.Local
                    .SingleOrDefault(user => user.Id.ToString() == id));

            var roleStore = new Mock<IRoleStore<ApplicationRole>>();
            _roleManager = new Mock<RoleManager<ApplicationRole>>(
                roleStore.Object,
                Array.Empty<IRoleValidator<ApplicationRole>>(),
                null!,
                null!,
                null!);
            _roleManager.Setup(item => item.RoleExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            ConfigureRegistrationService();
            ConfigureTokenService();
            _notifications.Setup(item => item.SendEmailAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<bool>()))
                .Returns((string _, string _, string body, bool _) =>
                {
                    if (FailCredentialDelivery)
                        return Task.FromException(
                            new InvalidOperationException("delivery unavailable"));
                    DeliveredMessages.Add(body);
                    return Task.CompletedTask;
                });
            _notifications.Setup(item => item.SendSmsAsync(
                    It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string _, string message) =>
                {
                    if (FailCredentialDelivery)
                        return Task.FromException(
                            new InvalidOperationException("delivery unavailable"));
                    DeliveredMessages.Add(message);
                    return Task.CompletedTask;
                });

            var events = new Mock<IProcurementControlEventService>();
            events.Setup(item => item.RecordSystemAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(),
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            events.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementControlEventWriteRequest, CancellationToken>(
                    (request, _) => RecordedControlEvents.Add(request))
                .ReturnsAsync(new ProcurementControlEventDto());

            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    TenantId = TenantId,
                    ActorUserId = ActorId
                });

            Service = new ProcurementSupplierApplicantAccessService(
                _unitOfWork,
                _tokenService.Object,
                _registrations.Object,
                events.Object,
                access.Object,
                current.Object,
                _userManager.Object,
                _roleManager.Object,
                _notifications.Object,
                Options.Create(new SupplierApplicantAccessOptions()),
                NullLogger<ProcurementSupplierApplicantAccessService>.Instance);
        }

        public void SeedSystemActor()
        {
            var user = new ApplicationUser
            {
                Id = ActorId,
                UserName = "tenant.admin",
                NormalizedUserName = "TENANT.ADMIN",
                FirstName = "Tenant",
                LastName = "Admin",
                TenantId = TenantId,
                IsActive = true
            };
            Context.Users.Add(user);
            Context.UserTenants.Add(new UserTenant
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = TenantId,
                Status = UserTenantStatus.Active,
                IsDefault = true,
                GrantedAt = DateTime.UtcNow
            });
            Context.SaveChanges();
        }

        public (Guid RegistrationId, Guid BusinessPartnerId) SeedApprovedApplication()
        {
            var registrationId = Guid.NewGuid();
            var businessPartnerId = Guid.NewGuid();
            var token = Token(registrationId);
            var registration = new BusinessPartnerRegistration
            {
                Id = registrationId,
                TenantId = TenantId,
                RegistrationNumber = "APPROVED-001",
                ApplicantName = "Approved Supplier",
                ApplicantEmail = "approved@example.test",
                PartnerType = "Supplier",
                Status = "Approved",
                ApprovedDate = DateTime.UtcNow.AddMinutes(-1),
                ApprovedById = ActorId,
                BusinessPartnerId = businessPartnerId,
                CreatedById = ActorId
            };
            Context.AddRange(
                registration,
                token,
                new BusinessPartner
                {
                    Id = businessPartnerId,
                    TenantId = TenantId,
                    PartnerCode = "SUP-001",
                    PartnerName = "Approved Supplier",
                    PartnerType = "Supplier",
                    RegistrationStatus =
                        BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus,
                    ApprovalStatus =
                        BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus,
                    IsActive = true,
                    UserId = ActorId,
                    CreatedById = ActorId
                });
            var access = new ProcurementSupplierApplicantAccess
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                RegistrationId = registrationId,
                TokenId = token.Id,
                VerifiedChannel = ProcurementSupplierApplicantVerificationChannel.Email,
                VerifiedContact = "approved@example.test",
                VerifiedContactMasked = "ap***@example.test",
                VerifiedContactHashSha256 = new string('a', 64),
                VerifiedAtUtc = DateTime.UtcNow,
                Status = ProcurementSupplierApplicantAccessStatus.ApplicationInProgress,
                CreatedById = ActorId,
                IntegrityHash = new string('b', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            Context.Add(access);
            Context.ProcurementSupplierApplicantSessions.Add(
                new ProcurementSupplierApplicantSession
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    ApplicantAccessId = access.Id,
                    SessionReference = Guid.NewGuid(),
                    Status = ProcurementSupplierApplicantSessionStatus.Active,
                    IssuedAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                    IntegrityHash = new string('c', 64),
                    RowVersion = Guid.NewGuid().ToByteArray()
                });
            Context.SaveChanges();
            _tokens[registrationId] = TokenDto(token, registration);
            return (registrationId, businessPartnerId);
        }

        public void UseApplicant(Guid tenantId)
        {
            _external = true;
            _currentTenantId = tenantId;
        }

        public void UseInternal()
        {
            _external = false;
            _currentTenantId = TenantId;
            _currentUserId = ActorId;
        }

        public void UseSupplierAccount(Guid userId)
        {
            _external = true;
            _currentTenantId = TenantId;
            _currentUserId = userId;
        }

        private void ConfigureRegistrationService()
        {
            _registrations.Setup(item => item.UpdateAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UpdateBusinessPartnerRegistrationDto>(),
                    It.IsAny<Guid>()))
                .ReturnsAsync((Guid id, UpdateBusinessPartnerRegistrationDto request, Guid _) =>
                    Detail(id, request.CompanyName, request.Email, _registrationStatus));
            _registrations.Setup(item => item.SubmitExternalApplicantForReviewAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Callback(() => _registrationStatus = "Submitted")
                .Returns(Task.CompletedTask);
            _registrations.Setup(item => item.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) =>
                {
                    var row = Context.BusinessPartnerRegistrations
                        .IgnoreQueryFilters().SingleOrDefault(item => item.Id == id);
                    return row is null
                        ? null
                        : Detail(id, row.ApplicantName, row.ApplicantEmail,
                            _registrationStatus, row.BusinessPartnerId);
                });
        }

        private void ConfigureTokenService()
        {
            _tokenService.Setup(item => item.IssueForVerifiedApplicantAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid, string, CancellationToken>((_, registrationId, _, _) =>
                {
                    var registration = Context.BusinessPartnerRegistrations
                        .IgnoreQueryFilters().Single(item => item.Id == registrationId);
                    var token = Token(registrationId);
                    Context.Add(token);
                    Context.SaveChanges();
                    _tokens[registrationId] = TokenDto(token, registration);
                })
                .ReturnsAsync((Guid _, Guid registrationId, string _, CancellationToken _) =>
                    new ProcurementSupplierOnboardingTokenIssueResultDto
                    {
                        Token = _tokens[registrationId],
                        PlaintextToken = _paid
                            ? null
                            : "one-time-application-token"
                    });
            _tokenService.Setup(item => item.ValidateApplicantTokenAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _tokens.Values.Single());
            _tokenService.Setup(item => item.GetForRegistrationAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid registrationId, CancellationToken _) =>
                    _tokens.GetValueOrDefault(registrationId));
        }

        private ProcurementSupplierOnboardingToken Token(Guid registrationId) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RegistrationId = registrationId,
            TokenReference = $"TOK-{registrationId:N}"[..20],
            TokenHashSha256 = new string('d', 64),
            TokenLastFour = "1234",
            Generation = 1,
            Status = _paid
                ? ProcurementSupplierOnboardingTokenStatus.AwaitingPayment
                : ProcurementSupplierOnboardingTokenStatus.Active,
            PaymentStatus = _paid
                ? ProcurementSupplierOnboardingPaymentStatus.Pending
                : ProcurementSupplierOnboardingPaymentStatus.NotRequired,
            IssuedAtUtc = DateTime.UtcNow,
            SourceConfigurationProfileId = Guid.NewGuid(),
            SourceConfigurationProfileCode = "TDC-PROCUREMENT",
            SourceConfigurationProfileVersion = 1,
            SourceConfigurationDecisionId = Guid.NewGuid(),
            FeeMode = _paid
                ? ProcurementSupplierOnboardingFeeMode.Paid
                : ProcurementSupplierOnboardingFeeMode.Free,
            FeeType = "Supplier application token",
            FeeAmount = _paid ? 100m : 0m,
            TotalAmount = _paid ? 100m : 0m,
            CurrencyCode = "GHS",
            ReceiptNumberFormat = "SUP-{#####}",
            ExemptionRule = "Controlled",
            RefundRule = "Controlled",
            RenewalRule = "Controlled",
            DecisionSnapshotHash = new string('e', 64),
            IntegrityHash = new string('f', 64),
            CreationCorrelationId = "test-creation",
            LastOperationCorrelationId = "test-creation",
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        private static ProcurementSupplierOnboardingTokenDto TokenDto(
            ProcurementSupplierOnboardingToken token,
            BusinessPartnerRegistration registration) => new()
        {
            Id = token.Id,
            RegistrationId = registration.Id,
            RegistrationNumber = registration.RegistrationNumber,
            ApplicantName = registration.ApplicantName,
            TokenReference = token.TokenReference,
            MaskedToken = $"***{token.TokenLastFour}",
            Generation = token.Generation,
            Status = token.Status,
            PaymentStatus = token.PaymentStatus,
            FeeMode = token.FeeMode,
            CurrencyCode = token.CurrencyCode,
            IssuedAtUtc = token.IssuedAtUtc,
            SourceConfigurationProfileId = token.SourceConfigurationProfileId,
            SourceConfigurationProfileCode = token.SourceConfigurationProfileCode,
            SourceConfigurationProfileVersion = token.SourceConfigurationProfileVersion,
            SourceConfigurationDecisionId = token.SourceConfigurationDecisionId,
            FeeType = token.FeeType,
            ReceiptNumberFormat = token.ReceiptNumberFormat,
            ExemptionRule = token.ExemptionRule,
            RefundRule = token.RefundRule,
            RenewalRule = token.RenewalRule,
            DecisionSnapshotHash = token.DecisionSnapshotHash,
            IntegrityHash = token.IntegrityHash
        };

        private static BusinessPartnerRegistrationDetailDto Detail(
            Guid id,
            string company,
            string? email,
            string status,
            Guid? businessPartnerId = null) => new()
        {
            Id = id,
            BusinessPartnerId = businessPartnerId,
            ApplicationNumber = $"APP-{id:N}",
            RegistrationNumber = $"APP-{id:N}",
            CompanyName = company,
            Email = email,
            PartnerType = "Supplier",
            RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods,
            Status = status,
            RegistrationData = "{}"
        };

        public ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
