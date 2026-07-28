using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ErpSystem.Shared;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierApplicantLifecycleTests
{
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
        access.CreatedById.Should().Be(fixture.ActorId);
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

        var duplicate = () => fixture.Service.CreateVerifiedApplicationAsync(
            new VerifyAndIssueSupplierApplicantTokenRequest
            {
                TenantId = fixture.TenantId,
                Channel = channel,
                Contact = contact,
                CompanyName = "Duplicate Applicant",
                RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods
            },
            "duplicate-contact");
        (await duplicate.Should()
                .ThrowAsync<ProcurementSupplierApplicantAccessException>())
            .Which.Code.Should().Be("SUPPLIER_APPLICANT_ACTIVE_APPLICATION_EXISTS");

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
        fixture.DeliveredMessages.Should().ContainSingle()
            .Which.Should().Contain(access.LoginIdentifier)
            .And.Contain(fixture.LastTemporaryPassword);

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

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public bool FailCredentialDelivery { get; set; }
        public string LastTemporaryPassword { get; private set; } = string.Empty;
        public List<string> DeliveredMessages { get; } = [];
        public ApplicationDbContext Context { get; }
        public ProcurementSupplierApplicantAccessService Service { get; }

        public Fixture()
        {
            _currentTenantId = TenantId;
            _currentUserId = ActorId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
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
                })
                .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(item => item.AddToRolesAsync(
                    It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(IdentityResult.Success);
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
                    PartnerType = "Supplier"
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
            _registrations.Setup(item => item.SubmitForReviewAsync(
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
                        PlaintextToken = "one-time-application-token"
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
            Status = ProcurementSupplierOnboardingTokenStatus.Active,
            PaymentStatus = ProcurementSupplierOnboardingPaymentStatus.NotRequired,
            IssuedAtUtc = DateTime.UtcNow,
            SourceConfigurationProfileId = Guid.NewGuid(),
            SourceConfigurationProfileCode = "TDC-PROCUREMENT",
            SourceConfigurationProfileVersion = 1,
            SourceConfigurationDecisionId = Guid.NewGuid(),
            FeeMode = ProcurementSupplierOnboardingFeeMode.Free,
            FeeType = "Supplier application token",
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
