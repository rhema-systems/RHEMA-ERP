using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class BusinessPartnerRegistrationAuthorizationTests
{
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

            Service = new BusinessPartnerRegistrationService(
                Registrations.Object,
                Mock.Of<IBusinessPartnerRegistrationDocumentRepository>(),
                Mock.Of<IBusinessPartnerRegistrationStatusHistoryRepository>(),
                Mock.Of<IBusinessPartnerRepository>(),
                Mock.Of<IBusinessPartnerContactRepository>(),
                Mock.Of<IBusinessPartnerFinancialRepository>(),
                Mock.Of<IBusinessPartnerDocumentRepository>(),
                Mock.Of<IBusinessPartnerLicenseRepository>(),
                Mock.Of<IUnitOfWork>(),
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
