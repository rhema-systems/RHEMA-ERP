using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class BusinessPartnerRegistrationAuthorizationTests
{
    [Fact]
    public async Task RestrictedApplicantClaimCanUpdateRetainedDraftWithoutChangingAuditOwner()
    {
        var tenantId = Guid.NewGuid();
        var systemActorId = Guid.NewGuid();
        var originalOwnerId = Guid.NewGuid();
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegistrationNumber = "LEGACY-APP-003",
            ApplicantName = "Retained Supplier",
            ApplicantEmail = "retained@example.test",
            PartnerType = "Supplier",
            RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods,
            Status = "Draft",
            CreatedById = originalOwnerId
        };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new ApplicationDbContext(options);
        db.ProcurementSupplierApplicantAccesses.Add(
            new ProcurementSupplierApplicantAccess
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RegistrationId = registration.Id,
                TokenId = Guid.NewGuid(),
                VerifiedChannel =
                    ProcurementSupplierApplicantVerificationChannel.Email,
                VerifiedContact = "retained@example.test",
                VerifiedContactMasked = "re***@example.test",
                VerifiedContactHashSha256 = new string('a', 64),
                VerifiedAtUtc = DateTime.UtcNow,
                Status =
                    ProcurementSupplierApplicantAccessStatus.ApplicationInProgress,
                CreatedById = systemActorId,
                RowVersion = Guid.NewGuid().ToByteArray()
            });
        await db.SaveChangesAsync();

        var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
        registrations.Setup(item => item.GetByIdAsync(registration.Id))
            .ReturnsAsync(registration);
        registrations.Setup(item => item.UpdateAsync(registration))
            .ReturnsAsync(registration);
        var statusHistory =
            new Mock<IBusinessPartnerRegistrationStatusHistoryRepository>();
        statusHistory.Setup(item => item.GetHistoryByRegistrationAsync(registration.Id))
            .ReturnsAsync([]);
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(item => item.IsAuthenticated).Returns(true);
        current.SetupGet(item => item.IsExternalUser).Returns(true);
        current.SetupGet(item => item.TenantId).Returns(tenantId);
        current.SetupGet(item => item.UserId).Returns(systemActorId);
        current.SetupGet(item => item.AuthenticationProvider).Returns("ApplicantToken");
        var claims = new Dictionary<string, string>
        {
            ["supplier_applicant_registration"] = registration.Id.ToString()
        };
        current.SetupGet(item => item.Claims).Returns(claims);
        current.SetupGet(item => item.Roles).Returns(["ExternalUser"]);
        var unitOfWork = new UnitOfWork(db);
        var service = new BusinessPartnerRegistrationService(
            registrations.Object,
            Mock.Of<IBusinessPartnerRegistrationDocumentRepository>(),
            statusHistory.Object,
            Mock.Of<IBusinessPartnerRepository>(),
            Mock.Of<IBusinessPartnerContactRepository>(),
            Mock.Of<IBusinessPartnerFinancialRepository>(),
            Mock.Of<IBusinessPartnerDocumentRepository>(),
            Mock.Of<IBusinessPartnerLicenseRepository>(),
            unitOfWork,
            current.Object,
            Mock.Of<IAppEventBus>(),
            Mock.Of<IProcurementAccessControlService>(),
            NullLogger<BusinessPartnerRegistrationService>.Instance);

        var updated = await service.UpdateAsync(
            registration.Id,
            new UpdateBusinessPartnerRegistrationDto
            {
                CompanyName = "Retained Supplier Updated",
                Email = "retained@example.test",
                RegistrationCategory =
                    ProcurementSupplierRegistrationCategory.Goods,
                RegistrationData =
                    "{\"taxNumber\":\"TAX-001\",\"legacyField\":\"retained\"}"
            },
            systemActorId);

        updated.CompanyName.Should().Be("Retained Supplier Updated");
        updated.TaxNumber.Should().Be("TAX-001");
        registration.RegistrationDataJson.Should().Be(
            "{\"taxNumber\":\"TAX-001\",\"legacyField\":\"retained\"}");
        registration.CreatedById.Should().Be(originalOwnerId);
        registrations.Verify(item => item.UpdateAsync(registration), Times.Once);

        claims["supplier_applicant_registration"] = Guid.NewGuid().ToString();
        var crossApplication = () => service.UpdateAsync(
            registration.Id,
            new UpdateBusinessPartnerRegistrationDto
            {
                CompanyName = "Cross-application update",
                Email = "retained@example.test",
                RegistrationCategory =
                    ProcurementSupplierRegistrationCategory.Goods
            },
            systemActorId);
        await crossApplication.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*permission to update*");
        registrations.Verify(item => item.UpdateAsync(registration), Times.Once);
    }

    [Fact]
    public async Task RegistrationDetailIncludesEffectiveEvidenceRequirements()
    {
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            RegistrationNumber = "APP-EVIDENCE-001",
            ApplicantName = "Evidence Supplier",
            PartnerType = "Supplier",
            RegistrationCategory = ProcurementSupplierRegistrationCategory.Services,
            Status = "Draft",
            RegistrationDataJson = "{\"taxNumber\":\"TAX-200\"}"
        };
        var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
        registrations.Setup(item => item.GetWithDocumentsAsync(registration.Id))
            .ReturnsAsync(registration);
        var statusHistory =
            new Mock<IBusinessPartnerRegistrationStatusHistoryRepository>();
        statusHistory.Setup(item => item.GetHistoryByRegistrationAsync(registration.Id))
            .ReturnsAsync([]);
        var evidence = new Mock<IProcurementSupplierEvidencePackService>();
        evidence.Setup(item => item.GetRegistrationReadinessAsync(
                registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSupplierEvidenceReadinessDto
            {
                RegistrationId = registration.Id,
                Category = ProcurementSupplierRegistrationCategory.Services,
                PackCode = "SUP-SERVICES-TEST",
                PackVersion = 1,
                Requirements =
                [
                    new ProcurementSupplierEvidenceRequirementReadinessDto
                    {
                        RequirementCode = "SUP-SERVICES-TAX",
                        Name = "Tax clearance"
                    }
                ]
            });
        var service = new BusinessPartnerRegistrationService(
            registrations.Object,
            Mock.Of<IBusinessPartnerRegistrationDocumentRepository>(),
            statusHistory.Object,
            Mock.Of<IBusinessPartnerRepository>(),
            Mock.Of<IBusinessPartnerContactRepository>(),
            Mock.Of<IBusinessPartnerFinancialRepository>(),
            Mock.Of<IBusinessPartnerDocumentRepository>(),
            Mock.Of<IBusinessPartnerLicenseRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ICurrentUserProvider>(),
            Mock.Of<IAppEventBus>(),
            Mock.Of<IProcurementAccessControlService>(),
            NullLogger<BusinessPartnerRegistrationService>.Instance,
            evidencePackService: evidence.Object);

        var result = await service.GetByIdAsync(registration.Id);

        result.Should().NotBeNull();
        result!.EvidenceReadiness.Should().NotBeNull();
        result.EvidenceReadiness!.PackCode.Should().Be("SUP-SERVICES-TEST");
        result.EvidenceReadiness.Requirements.Should().ContainSingle()
            .Which.RequirementCode.Should().Be("SUP-SERVICES-TAX");
    }

    [Fact]
    public async Task RegistrationDetailPreservesRepeatableContactsAndBankAccounts()
    {
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            RegistrationNumber = "APP-REPEATABLE-001",
            ApplicantName = "Repeatable Details Supplier",
            PartnerType = "Supplier",
            RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods,
            Status = "Submitted",
            RegistrationDataJson = """
                {
                  "contacts": [
                    { "contactName": "Primary Contact", "isPrimary": true },
                    { "contactName": "Accounts Contact", "isPrimary": false }
                  ],
                  "bankAccounts": [
                    { "bankName": "First Test Bank", "accountNumber": "TEST-001", "isPrimary": true },
                    { "bankName": "Second Test Bank", "accountNumber": "TEST-002", "isPrimary": false }
                  ]
                }
                """
        };
        var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
        registrations.Setup(item => item.GetWithDocumentsAsync(registration.Id))
            .ReturnsAsync(registration);
        var statusHistory =
            new Mock<IBusinessPartnerRegistrationStatusHistoryRepository>();
        statusHistory.Setup(item => item.GetHistoryByRegistrationAsync(registration.Id))
            .ReturnsAsync([]);
        var service = new BusinessPartnerRegistrationService(
            registrations.Object,
            Mock.Of<IBusinessPartnerRegistrationDocumentRepository>(),
            statusHistory.Object,
            Mock.Of<IBusinessPartnerRepository>(),
            Mock.Of<IBusinessPartnerContactRepository>(),
            Mock.Of<IBusinessPartnerFinancialRepository>(),
            Mock.Of<IBusinessPartnerDocumentRepository>(),
            Mock.Of<IBusinessPartnerLicenseRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ICurrentUserProvider>(),
            Mock.Of<IAppEventBus>(),
            Mock.Of<IProcurementAccessControlService>(),
            NullLogger<BusinessPartnerRegistrationService>.Instance);

        var result = await service.GetByIdAsync(registration.Id);

        result.Should().NotBeNull();
        result!.RegistrationData.Should().NotBeNullOrWhiteSpace();
        using var registrationData = System.Text.Json.JsonDocument.Parse(
            result.RegistrationData!);
        registrationData.RootElement.GetProperty("contacts")
            .GetArrayLength().Should().Be(2);
        registrationData.RootElement.GetProperty("bankAccounts")
            .GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task ReviewRejectsAnActorThatWasNotDerivedFromCurrentIdentity()
    {
        var fixture = new Fixture(allowed: true);

        var act = () => fixture.Service.ReviewRegistrationAsync(
            fixture.Request, Guid.NewGuid());

        await act.Should().ThrowAsync<ProcurementAccessAuthorizationException>()
            .WithMessage("*actor must be derived*");
        fixture.Registrations.Verify(item => item.UpdateStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ReviewRequiresSupplierReviewCapability()
    {
        var fixture = new Fixture(allowed: false);

        var act = () => fixture.Service.ReviewRegistrationAsync(
            fixture.Request, fixture.ActorId);

        await act.Should().ThrowAsync<ProcurementAccessAuthorizationException>();
        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.supplier.review" &&
                request.SourceType == "SupplierRegistration"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Registrations.Verify(item => item.UpdateStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ReviewWritesOnlyTheAuthenticatedAuthorizedActor()
    {
        var fixture = new Fixture(allowed: true);

        await fixture.Service.ReviewRegistrationAsync(
            fixture.Request, fixture.ActorId);

        fixture.Registrations.Verify(item => item.UpdateStatusAsync(
            fixture.RegistrationId,
            "UnderReview",
            fixture.ActorId,
            "reviewed"), Times.Once);
    }

    [Fact]
    public async Task ApprovalCannotCrossTenantBoundaryEvenWithCapability()
    {
        var fixture = new Fixture(allowed: true, registrationTenant: Guid.NewGuid());

        var act = () => fixture.Service.ApproveRegistrationAsync(
            fixture.RegistrationId, fixture.ActorId);

        await act.Should().ThrowAsync<ProcurementAccessAuthorizationException>()
            .WithMessage("*different tenant*");
        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
            It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnauthenticatedAndExternalActorsAreDeniedBeforeCapabilityEvaluation()
    {
        var unauthenticated = new Fixture(
            allowedPermissions: ["procurement.supplier.review"],
            isAuthenticated: false);
        var external = new Fixture(
            allowedPermissions: ["procurement.supplier.review"],
            isExternal: true);

        await ((Func<Task>)(() => unauthenticated.Service.ReviewRegistrationAsync(
                unauthenticated.Request, unauthenticated.ActorId)))
            .Should().ThrowAsync<ProcurementAccessAuthorizationException>();
        await ((Func<Task>)(() => external.Service.ReviewRegistrationAsync(
                external.Request, external.ActorId)))
            .Should().ThrowAsync<ProcurementAccessAuthorizationException>();

        unauthenticated.Access.Verify(item => item.EnforceCapabilityAsync(
            It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        external.Access.Verify(item => item.EnforceCapabilityAsync(
            It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MakerPermissionCannotReviewOrApproveRegistration()
    {
        var fixture = new Fixture(
            allowedPermissions: ["procurement.supplier.manage"]);

        await ((Func<Task>)(() => fixture.Service.ReviewRegistrationAsync(
                fixture.Request, fixture.ActorId)))
            .Should().ThrowAsync<ProcurementAccessAuthorizationException>();
        await ((Func<Task>)(() => fixture.Service.ApproveRegistrationAsync(
                fixture.RegistrationId, fixture.ActorId)))
            .Should().ThrowAsync<ProcurementAccessAuthorizationException>();

        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.supplier.review"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.supplier.approve"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewerCanReviewButCannotApproveOrRejectRegistration()
    {
        var fixture = new Fixture(
            allowedPermissions: ["procurement.supplier.review"]);

        await fixture.Service.ReviewRegistrationAsync(
            fixture.Request, fixture.ActorId);
        await ((Func<Task>)(() => fixture.Service.ApproveRegistrationAsync(
                fixture.RegistrationId, fixture.ActorId)))
            .Should().ThrowAsync<ProcurementAccessAuthorizationException>();
        await ((Func<Task>)(() => fixture.Service.RejectRegistrationAsync(
                fixture.RegistrationId, fixture.ActorId, "not acceptable")))
            .Should().ThrowAsync<ProcurementAccessAuthorizationException>();

        fixture.Registrations.Verify(item => item.UpdateStatusAsync(
            fixture.RegistrationId,
            "UnderReview",
            fixture.ActorId,
            "reviewed"), Times.Once);
        fixture.Registrations.Verify(item => item.UpdateStatusAsync(
            fixture.RegistrationId,
            "Rejected",
            It.IsAny<Guid?>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ApproverPermissionCanRejectAndWritesAuthenticatedActor()
    {
        var fixture = new Fixture(
            allowedPermissions: ["procurement.supplier.approve"]);

        await fixture.Service.RejectRegistrationAsync(
            fixture.RegistrationId, fixture.ActorId, "not acceptable");

        fixture.Registrations.Verify(item => item.UpdateStatusAsync(
            fixture.RegistrationId,
            "Rejected",
            fixture.ActorId,
            "not acceptable"), Times.Once);
        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.supplier.approve"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectedRegistrationReplayRepairsTerminalTokenClosure()
    {
        var fixture = new Fixture(
            allowedPermissions: ["procurement.supplier.approve"]);

        await fixture.Service.RejectRegistrationAsync(
            fixture.RegistrationId, fixture.ActorId, "not acceptable");
        await fixture.Service.RejectRegistrationAsync(
            fixture.RegistrationId, fixture.ActorId, "not acceptable");

        fixture.Registrations.Verify(item => item.UpdateStatusAsync(
            fixture.RegistrationId,
            "Rejected",
            fixture.ActorId,
            "not acceptable"), Times.Once);
        fixture.Tokens.Verify(item => item.ExpireForTerminalRegistrationAsync(
            fixture.RegistrationId,
            "Rejected",
            fixture.ActorId,
            $"registration-rejected-{fixture.RegistrationId:N}",
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ApprovalRejectsAnActorThatDiffersFromAuthenticatedIdentity()
    {
        var fixture = new Fixture(allowed: true);

        await ((Func<Task>)(() => fixture.Service.ApproveRegistrationAsync(
                fixture.RegistrationId, Guid.NewGuid())))
            .Should().ThrowAsync<ProcurementAccessAuthorizationException>()
            .WithMessage("*actor must be derived*");

        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
            It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid RegistrationId { get; } = Guid.NewGuid();
        public Mock<IBusinessPartnerRegistrationRepository> Registrations { get; } = new();
        public Mock<IProcurementAccessControlService> Access { get; } = new();
        public Mock<IProcurementSupplierOnboardingTokenService> Tokens { get; } = new();
        public ReviewBusinessPartnerRegistrationDto Request { get; }
        public BusinessPartnerRegistrationService Service { get; }

        public Fixture(bool allowed, Guid? registrationTenant = null)
            : this(
                allowed
                    ? ["procurement.supplier.review", "procurement.supplier.approve"]
                    : Array.Empty<string>(),
                registrationTenant)
        {
        }

        public Fixture(
            IReadOnlyCollection<string> allowedPermissions,
            Guid? registrationTenant = null,
            bool isAuthenticated = true,
            bool isExternal = false)
        {
            var registration = new BusinessPartnerRegistration
            {
                Id = RegistrationId,
                TenantId = registrationTenant ?? TenantId,
                RegistrationNumber = "SUP-REG-001",
                ApplicantName = "Supplier",
                ApplicantEmail = "supplier@example.test",
                PartnerType = "Supplier",
                Status = "Submitted"
            };
            Registrations.Setup(item => item.GetByIdAsync(RegistrationId))
                .ReturnsAsync(registration);
            Registrations.Setup(item => item.UpdateStatusAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                    It.IsAny<string?>()))
                .Callback<Guid, string, Guid?, string?>((_, status, _, _) =>
                    registration.Status = status)
                .Returns(Task.CompletedTask);
            Tokens.Setup(item => item.ExpireForTerminalRegistrationAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.IsAuthenticated).Returns(isAuthenticated);
            current.SetupGet(item => item.IsExternalUser).Returns(isExternal);
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(ActorId);
            current.SetupGet(item => item.Roles).Returns(Array.Empty<string>());
            current.Setup(item => item.HasRole(It.IsAny<string>())).Returns(false);

            Access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                    ProcurementAccessCapabilityRequest request,
                    string _,
                    CancellationToken _) => new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = allowedPermissions.Contains(
                        request.PermissionCode,
                        StringComparer.OrdinalIgnoreCase),
                    Code = allowedPermissions.Contains(
                        request.PermissionCode,
                        StringComparer.OrdinalIgnoreCase)
                        ? "ALLOWED"
                        : "DENIED",
                    Message = allowedPermissions.Contains(
                        request.PermissionCode,
                        StringComparer.OrdinalIgnoreCase)
                        ? "Allowed"
                        : "Denied",
                    ActorUserId = ActorId,
                    TenantId = TenantId
                });

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(item => item.ExecuteInStrategyAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Func<Task> operation, CancellationToken _) => operation());
            unitOfWork.Setup(item => item.BeginTransactionAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            unitOfWork.Setup(item => item.AcquireTransactionLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            unitOfWork.Setup(item => item.RollbackAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Service = new BusinessPartnerRegistrationService(
                Registrations.Object,
                Mock.Of<IBusinessPartnerRegistrationDocumentRepository>(),
                Mock.Of<IBusinessPartnerRegistrationStatusHistoryRepository>(),
                Mock.Of<IBusinessPartnerRepository>(),
                Mock.Of<IBusinessPartnerContactRepository>(),
                Mock.Of<IBusinessPartnerFinancialRepository>(),
                Mock.Of<IBusinessPartnerDocumentRepository>(),
                Mock.Of<IBusinessPartnerLicenseRepository>(),
                unitOfWork.Object,
                current.Object,
                Mock.Of<IAppEventBus>(),
                Access.Object,
                NullLogger<BusinessPartnerRegistrationService>.Instance,
                onboardingTokenService: Tokens.Object);

            Request = new ReviewBusinessPartnerRegistrationDto
            {
                RegistrationId = RegistrationId,
                Action = "Review",
                ReviewNotes = "reviewed"
            };
        }
    }
}
