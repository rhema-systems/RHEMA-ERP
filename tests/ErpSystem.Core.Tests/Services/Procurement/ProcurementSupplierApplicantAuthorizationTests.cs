using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierApplicantAuthorizationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdministrationReadsRequireSupplierReviewPermission(bool summary)
    {
        var fixture = new Fixture();

        Func<Task> act = summary
            ? async () => await fixture.Service.GetSummaryAsync()
            : async () => await fixture.Service.GetHistoryAsync();

        var exception = await act.Should()
            .ThrowAsync<ProcurementSupplierApplicantAccessException>();
        exception.Which.Code.Should().Be("SUPPLIER_APPLICANT_REVIEW_DENIED");
        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.supplier.review" &&
                request.SourceType == "ProcurementSupplierApplicantAccess"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CredentialResendRequiresSupplierApproveNotMakerPermission()
    {
        var fixture = new Fixture("procurement.supplier.manage");

        var act = () => fixture.Service.ResendTemporaryCredentialAsync(
            Guid.NewGuid(), "credential-resend");

        var exception = await act.Should()
            .ThrowAsync<ProcurementSupplierApplicantAccessException>();
        exception.Which.Code.Should().Be("SUPPLIER_APPLICANT_APPROVE_DENIED");
        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.supplier.approve"),
            "credential-resend",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TerminalSessionClosureRequiresSupplierApprovePermission()
    {
        var fixture = new Fixture("procurement.supplier.review");

        var act = () => fixture.Service.CloseForTerminalRegistrationAsync(
            Guid.NewGuid(), "Rejected", fixture.ActorId, "registration-rejected");

        var exception = await act.Should()
            .ThrowAsync<ProcurementSupplierApplicantAccessException>();
        exception.Which.Code.Should().Be("SUPPLIER_APPLICANT_APPROVE_DENIED");
    }

    [Fact]
    public async Task ProvisioningRejectsSpoofedApprovalActorBeforeCapabilityEvaluation()
    {
        var fixture = new Fixture("procurement.supplier.approve");

        var act = () => fixture.Service.ProvisionApprovedSupplierAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "activation-retry");

        var exception = await act.Should()
            .ThrowAsync<ProcurementSupplierApplicantAccessException>();
        exception.Which.Code.Should().Be("SUPPLIER_APPLICANT_ACTOR_MISMATCH");
        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
            It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenericAdministratorRoleDoesNotBypassAcceptedProcurementPermissions()
    {
        var fixture = new Fixture(roles: ["Administrator"]);

        var act = () => fixture.Service.GetSummaryAsync();

        await act.Should().ThrowAsync<ProcurementSupplierApplicantAccessException>();
        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.supplier.review"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public Mock<IProcurementAccessControlService> Access { get; } = new();
        public ProcurementSupplierApplicantAccessService Service { get; }

        public Fixture(
            string? allowedPermission = null,
            IReadOnlyCollection<string>? roles = null)
        {
            roles ??= Array.Empty<string>();
            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(false);
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(ActorId);
            current.SetupGet(item => item.Username).Returns("internal.user");
            current.SetupGet(item => item.Roles).Returns(roles);
            current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => roles.Contains(
                    role, StringComparer.OrdinalIgnoreCase));

            Access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                    ProcurementAccessCapabilityRequest request,
                    string _,
                    CancellationToken _) => new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = string.Equals(
                        request.PermissionCode,
                        allowedPermission,
                        StringComparison.OrdinalIgnoreCase),
                    Code = "TEST_DECISION",
                    Message = string.Equals(
                        request.PermissionCode,
                        allowedPermission,
                        StringComparison.OrdinalIgnoreCase)
                        ? "Allowed"
                        : "Denied",
                    TenantId = TenantId,
                    ActorUserId = ActorId
                });

            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!,
                null!,
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                null!,
                null!,
                null!,
                null!);
            var roleManager = new Mock<RoleManager<ApplicationRole>>(
                Mock.Of<IRoleStore<ApplicationRole>>(),
                Array.Empty<IRoleValidator<ApplicationRole>>(),
                null!,
                null!,
                null!);

            Service = new ProcurementSupplierApplicantAccessService(
                Mock.Of<IUnitOfWork>(),
                Mock.Of<IProcurementSupplierOnboardingTokenService>(),
                Mock.Of<IBusinessPartnerRegistrationService>(),
                Mock.Of<IProcurementControlEventService>(),
                Access.Object,
                current.Object,
                userManager.Object,
                roleManager.Object,
                Mock.Of<INotificationService>(),
                Options.Create(new SupplierApplicantAccessOptions()),
                NullLogger<ProcurementSupplierApplicantAccessService>.Instance);
        }
    }
}
