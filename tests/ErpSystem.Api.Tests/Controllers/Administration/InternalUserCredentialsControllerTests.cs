using System.Reflection;
using ErpSystem.Api.Controllers.Administration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Administration;

public sealed class InternalUserCredentialsControllerTests
{
    [Fact]
    public void RouteAllowsOnlyTenantAdminOrSuperAdmin()
    {
        var authorize = typeof(InternalUserCredentialsController)
            .GetCustomAttribute<AuthorizeAttribute>();
        var action = typeof(InternalUserCredentialsController)
            .GetMethod(nameof(InternalUserCredentialsController.ResetPassword));

        authorize!.Roles.Should().Be(
            Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin);
        action!.GetCustomAttribute<HttpPostAttribute>()!.Template
            .Should().Be("{userId:guid}/reset-password");
    }

    [Fact]
    public async Task ResetRejectsMissingServerActorWithoutCallingService()
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.IsAuthenticated).Returns(true);
        current.SetupGet(item => item.TenantId).Returns(Guid.NewGuid());
        var fixture = Fixture(current);

        var result = await fixture.Controller.ResetPassword(
            Guid.NewGuid(), Request(), default);

        result.Should().BeOfType<ForbidResult>();
        fixture.Service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResetUsesTokenTenantAndServerActorAndReturnsOnlyExpiryMetadata()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var current = Current(tenantId, actorId);
        var fixture = Fixture(current);
        InternalUserTemporaryPasswordResetCommand? captured = null;
        fixture.Service.Setup(item => item.ResetAsync(
                It.IsAny<InternalUserTemporaryPasswordResetCommand>(), default))
            .Callback<InternalUserTemporaryPasswordResetCommand, CancellationToken>(
                (command, _) => captured = command)
            .ReturnsAsync(InternalUserTemporaryPasswordResetResult.Success(expiresAt));

        var result = await fixture.Controller.ResetPassword(targetId, Request(), default);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(tenantId);
        captured.ActorUserId.Should().Be(actorId);
        captured.ActorUserName.Should().Be("tenant.admin");
        captured.TargetUserId.Should().Be(targetId);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var serialized = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        serialized.Should().Contain(targetId.ToString());
        serialized.Should().NotContain(Request().NewPassword!);
    }

    [Fact]
    public async Task ResetReturnsProblemDetailsCodeForPolicyFailure()
    {
        var fixture = Fixture(Current(Guid.NewGuid(), Guid.NewGuid()));
        fixture.Service.Setup(item => item.ResetAsync(
                It.IsAny<InternalUserTemporaryPasswordResetCommand>(), default))
            .ReturnsAsync(InternalUserTemporaryPasswordResetResult.Failure(
                "TEMPORARY_PASSWORD_POLICY_FAILED",
                "Temporary password does not meet policy.",
                new[] { "Minimum length not met." }));

        var result = await fixture.Controller.ResetPassword(
            Guid.NewGuid(), Request(), default);

        var invalid = result.Should().BeOfType<ObjectResult>().Subject;
        invalid.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var problem = invalid.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("TEMPORARY_PASSWORD_POLICY_FAILED");
        problem.Extensions.Should().ContainKey("errors");
    }

    private static ResetInternalUserPasswordRequest Request() => new()
    {
        NewPassword = "Temporary#123",
        Reason = "Authorized support reset"
    };

    private static Mock<ICurrentUserService> Current(Guid tenantId, Guid actorId)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.IsAuthenticated).Returns(true);
        current.SetupGet(item => item.TenantId).Returns(tenantId);
        current.SetupGet(item => item.UserId).Returns(actorId.ToString());
        current.SetupGet(item => item.UserName).Returns("tenant.admin");
        return current;
    }

    private static TestFixture Fixture(Mock<ICurrentUserService> current) => new(current);

    private sealed class TestFixture
    {
        public TestFixture(Mock<ICurrentUserService> current)
        {
            Controller = new InternalUserCredentialsController(Service.Object, current.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        TraceIdentifier = "password-reset-test"
                    }
                }
            };
        }

        public Mock<IInternalUserTemporaryPasswordResetService> Service { get; } = new();
        public InternalUserCredentialsController Controller { get; }
    }
}
