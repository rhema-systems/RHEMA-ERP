using System.Reflection;
using System.Security.Claims;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services;
using ErpSystem.Api.Middleware;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class SupplierApplicantAccessSecurityTests
{
    [Fact]
    public void ApplicantAndAdministrationRoutesUseSeparateAuthorizationPolicies()
    {
        var controller = typeof(SupplierApplicantAccessController);

        Policy(controller, nameof(SupplierApplicantAccessController.GetPortal))
            .Should().Be("SupplierApplicantOnly");
        Policy(controller, nameof(SupplierApplicantAccessController.UpdateApplication))
            .Should().Be("SupplierApplicantOnly");
        Policy(controller, nameof(SupplierApplicantAccessController.Submit))
            .Should().Be("SupplierApplicantOnly");
        Policy(controller, nameof(SupplierApplicantAccessController.GetSummary))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(SupplierApplicantAccessController.GetHistory))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(SupplierApplicantAccessController.ResendCredential))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(SupplierApplicantAccessController.RetryActivation))
            .Should().Be("InternalOnly");

        controller.GetMethod(nameof(SupplierApplicantAccessController.StartSession))!
            .GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        controller.GetMethod(nameof(SupplierApplicantAccessController.VerifyAndIssue))!
            .GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
    }

    [Fact]
    public async Task ApplicantSessionCannotReachAnyOtherApi()
    {
        var middleware = new SupplierApplicantAccessMiddleware();
        var blocked = Context(
            "/api/procurement/purchase-orders",
            new Claim("supplier_applicant_session", Guid.NewGuid().ToString()));
        var nextCalled = false;

        await middleware.InvokeAsync(blocked, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeFalse();
        blocked.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        var permitted = Context(
            "/api/procurement/supplier-applicant-access/portal",
            new Claim("supplier_applicant_session", Guid.NewGuid().ToString()));
        await middleware.InvokeAsync(permitted, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/user/change-password", true)]
    [InlineData("/api/auth/logout", true)]
    [InlineData("/api/procurement/business-partners", false)]
    [InlineData("/api/procurement/purchase-orders", false)]
    public async Task TemporaryCredentialAllowsOnlyPasswordReplacementAndLogout(
        string path,
        bool shouldCallNext)
    {
        var middleware = new TemporaryPasswordChangeMiddleware();
        var context = Context(path, new Claim("password_change_required", "true"));
        var nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().Be(shouldCallNext);
        context.Response.StatusCode.Should()
            .Be(shouldCallNext ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void LegacyAccountFirstSupplierMutationRoutesAreInternalOnly()
    {
        var controller = typeof(BusinessPartnerRegistrationsController);

        Policy(controller, nameof(BusinessPartnerRegistrationsController.CreateRegistration))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(BusinessPartnerRegistrationsController.UpdateRegistration))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(BusinessPartnerRegistrationsController.SubmitRegistration))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(BusinessPartnerRegistrationsController.UploadDocument))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(BusinessPartnerRegistrationsController.ReviewRegistration))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(BusinessPartnerRegistrationsController.ApproveRegistration))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(BusinessPartnerRegistrationsController.RejectRegistration))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(BusinessPartnerRegistrationsController.VerifyDocument))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(BusinessPartnerRegistrationsController.RejectDocument))
            .Should().Be("InternalOnly");
        Policy(controller, nameof(BusinessPartnerRegistrationsController.RevertDocumentRejection))
            .Should().Be("InternalOnly");
    }

    [Fact]
    public void ReviewActorCannotBeSuppliedByTheClient()
    {
        typeof(ReviewRegistrationRequest).GetProperty("ReviewedById")
            .Should().BeNull();
        typeof(ReviewRegistrationRequest).GetConstructors()
            .Single().GetParameters().Select(item => item.Name)
            .Should().Equal("ReviewNotes");
    }

    [Fact]
    public async Task RegistrationReviewUsesAuthenticatedActorAndReturnsStructuredForbidden()
    {
        var actorId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var registrations = new Mock<IBusinessPartnerRegistrationService>();
        var controller = RegistrationController(registrations.Object, actorId);

        var result = await controller.ReviewRegistration(
            registrationId, new ReviewRegistrationRequest("reviewed"));

        result.Should().BeOfType<NoContentResult>();
        registrations.Verify(item => item.ReviewRegistrationAsync(
            It.Is<ReviewBusinessPartnerRegistrationDto>(request =>
                request.RegistrationId == registrationId &&
                request.Notes == "reviewed"),
            actorId), Times.Once);

        registrations.Setup(item => item.ReviewRegistrationAsync(
                It.IsAny<ReviewBusinessPartnerRegistrationDto>(),
                It.IsAny<Guid>()))
            .ThrowsAsync(new ProcurementAccessAuthorizationException("Denied"));
        result = await controller.ReviewRegistration(
            registrationId, new ReviewRegistrationRequest("reviewed"));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task RegistrationApproveAndRejectUseOnlyAuthenticatedActor()
    {
        var actorId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var registrations = new Mock<IBusinessPartnerRegistrationService>();
        var applicantAccess = new Mock<IProcurementSupplierApplicantAccessService>();
        var controller = RegistrationController(
            registrations.Object, actorId, applicantAccess.Object);

        await controller.ApproveRegistration(
            registrationId, new ApproveRegistrationRequest("approved"));
        await controller.RejectRegistration(
            registrationId, new RejectRegistrationRequest("rejected"));

        registrations.Verify(item => item.ApproveRegistrationAsync(
            registrationId, actorId, "approved"), Times.Once);
        registrations.Verify(item => item.RejectRegistrationAsync(
            registrationId, actorId, "rejected"), Times.Once);
        applicantAccess.Verify(item => item.CloseForTerminalRegistrationAsync(
            registrationId,
            "Rejected",
            actorId,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SharedAndSupplierUploadControllersUseTheSameControlledUploadService()
    {
        var sharedField = typeof(FileUploadController).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(item =>
                item.FieldType == typeof(IControlledFileUploadService));
        var applicantField = typeof(SupplierApplicantAccessController).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(item =>
                item.FieldType == typeof(IControlledFileUploadService));

        sharedField.Should().NotBeNull();
        applicantField.Should().NotBeNull();
    }

    private static string? Policy(Type controller, string method) =>
        controller.GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()?.Policy;

    private static DefaultHttpContext Context(string path, params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticationType: "test"));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static BusinessPartnerRegistrationsController RegistrationController(
        IBusinessPartnerRegistrationService registrations,
        Guid actorId,
        IProcurementSupplierApplicantAccessService? applicantAccess = null)
    {
        var controller = new BusinessPartnerRegistrationsController(
            registrations,
            applicantAccess ?? Mock.Of<IProcurementSupplierApplicantAccessService>(),
            Mock.Of<IControlledFileUploadService>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<ICurrentUserService>(),
            NullLogger<BusinessPartnerRegistrationsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = Context(
                "/api/procurement/business-partner-registrations",
                new Claim(ClaimTypes.NameIdentifier, actorId.ToString()))
        };
        return controller;
    }
}
