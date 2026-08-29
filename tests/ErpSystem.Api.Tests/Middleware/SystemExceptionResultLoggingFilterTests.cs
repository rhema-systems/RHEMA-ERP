using System.Security.Claims;
using ErpSystem.Api.Filters;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Middleware;

public sealed class SystemExceptionResultLoggingFilterTests
{
    [Fact]
    public async Task LegacyServerErrorStringIsNormalizedWithoutLeakingControllerText()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var http = new DefaultHttpContext
        {
            RequestServices = provider,
            TraceIdentifier = "friendly-error-1"
        };
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = "/api/procurement/marketanalyses";
        var actionContext = new ActionContext(
            http, new RouteData(), new ActionDescriptor { DisplayName = "Create market analysis" });
        var executing = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new ObjectResult("An error occurred while saving the database record")
            {
                StatusCode = StatusCodes.Status500InternalServerError
            },
            new object());
        var filter = new SystemExceptionResultLoggingFilter(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SystemExceptionResultLoggingFilter>.Instance);

        await filter.OnResultExecutionAsync(executing, () =>
            Task.FromResult(new ResultExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                executing.Result,
                new object())));

        var normalized = executing.Result.Should().BeOfType<ObjectResult>().Subject;
        normalized.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var problem = normalized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("We couldn't complete your request");
        problem.Detail.Should().Contain("Reference ID: friendly-error-1");
        problem.Detail.Should().NotContain("database record");
        problem.Extensions["code"].Should().Be("UNEXPECTED_ERROR");
    }

    [Fact]
    public async Task AnonymousServerFailureIsNormalizedAndPersistedForTenantAudit()
    {
        var tenantId = Guid.NewGuid();
        var repository = new Mock<IGenericRepository<SystemExceptionLog>>();
        repository.Setup(value => value.AddAsync(It.IsAny<SystemExceptionLog>()))
            .ReturnsAsync((SystemExceptionLog value) => value);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.Repository<SystemExceptionLog>())
            .Returns(repository.Object);
        unitOfWork.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
        var provider = new ServiceCollection()
            .AddSingleton(unitOfWork.Object)
            .AddSingleton(currentUser.Object)
            .BuildServiceProvider();
        var http = new DefaultHttpContext
        {
            RequestServices = provider,
            TraceIdentifier = "anonymous-server-failure-1"
        };
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = "/api/procurement/rfqs/example/send";
        var actionContext = new ActionContext(
            http, new RouteData(), new ActionDescriptor { DisplayName = "Send RFQ" });
        var executing = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new ObjectResult(new
            {
                message = "RFQ send failed",
                details = "provider token=plain-secret"
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            },
            new object());
        var filter = new SystemExceptionResultLoggingFilter(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SystemExceptionResultLoggingFilter>.Instance);

        await filter.OnResultExecutionAsync(executing, () =>
            Task.FromResult(new ResultExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                executing.Result,
                new object())));

        var normalized = executing.Result.Should().BeOfType<ObjectResult>().Subject;
        var problem = normalized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain("Reference ID: anonymous-server-failure-1");
        problem.Detail.Should().NotContain("RFQ send failed");
        problem.Detail.Should().NotContain("plain-secret");
        repository.Verify(value => value.AddAsync(It.Is<SystemExceptionLog>(entry =>
            entry.TenantId == tenantId &&
            entry.TraceId == "anonymous-server-failure-1" &&
            entry.Level == "Critical" &&
            entry.ExceptionType == "HandledApiProblem" &&
            entry.FullMessage != null &&
            entry.FullMessage.Contains("Handled response payload") &&
            entry.FullMessage.Contains("token=[REDACTED]") &&
            !entry.FullMessage.Contains("plain-secret"))), Times.Once);
    }

    [Fact]
    public async Task CapturedHandledExceptionIsPersistedWithDiagnosticDetail()
    {
        var tenantId = Guid.NewGuid();
        var repository = new Mock<IGenericRepository<SystemExceptionLog>>();
        repository.Setup(value => value.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SystemExceptionLog, bool>>>() ))
            .ReturnsAsync((SystemExceptionLog?)null);
        repository.Setup(value => value.AddAsync(It.IsAny<SystemExceptionLog>()))
            .ReturnsAsync((SystemExceptionLog value) => value);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.Repository<SystemExceptionLog>())
            .Returns(repository.Object);
        unitOfWork.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
        var provider = new ServiceCollection()
            .AddSingleton(unitOfWork.Object)
            .AddSingleton(currentUser.Object)
            .BuildServiceProvider();
        var http = new DefaultHttpContext
        {
            RequestServices = provider,
            TraceIdentifier = "handled-trace-1"
        };
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = "/api/procurement/supplier-applicant-access/portal/submit";
        http.Items[SystemExceptionResultLoggingFilter.HandledExceptionItemKey] =
            new InvalidOperationException("Supplier binding failed for token=secret-value.");
        var actionContext = new ActionContext(
            http, new RouteData(), new ActionDescriptor { DisplayName = "Submit supplier" });
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Detail = "The supplier applicant request could not be completed."
        };
        var result = new ObjectResult(problem) { StatusCode = problem.Status };
        var filters = new List<IFilterMetadata>();
        var executing = new ResultExecutingContext(
            actionContext, filters, result, new object());
        var filter = new SystemExceptionResultLoggingFilter(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SystemExceptionResultLoggingFilter>.Instance);

        await filter.OnResultExecutionAsync(executing, () =>
            Task.FromResult(new ResultExecutedContext(
                actionContext, filters, result, new object())));

        repository.Verify(value => value.AddAsync(It.Is<SystemExceptionLog>(entry =>
            entry.ExceptionType == typeof(InvalidOperationException).FullName &&
            entry.ShortMessage.Contains("token=[REDACTED]") &&
            entry.FullMessage != null &&
            entry.FullMessage.Contains("Handled exception") &&
            !entry.FullMessage.Contains("secret-value"))), Times.Once);
    }

    [Fact]
    public async Task AutomaticValidationProblemIsPersistedForTenantAudit()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repository = new Mock<IGenericRepository<SystemExceptionLog>>();
        repository.Setup(value => value.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SystemExceptionLog, bool>>>() ))
            .ReturnsAsync((SystemExceptionLog?)null);
        repository.Setup(value => value.AddAsync(It.IsAny<SystemExceptionLog>()))
            .ReturnsAsync((SystemExceptionLog value) => value);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.Repository<SystemExceptionLog>())
            .Returns(repository.Object);
        unitOfWork.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
        var provider = new ServiceCollection()
            .AddSingleton(unitOfWork.Object)
            .AddSingleton(currentUser.Object)
            .BuildServiceProvider();

        var http = new DefaultHttpContext
        {
            RequestServices = provider,
            TraceIdentifier = "validation-trace-1",
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "procurement.user")
            ], "test"))
        };
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = "/api/procurement/supplier-onboarding-tokens";
        var actionContext = new ActionContext(
            http,
            new RouteData(),
            new ActionDescriptor { DisplayName = "Issue supplier token" });
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["registrationId"] = ["The supplied token=plain-secret is not a valid UUID."]
        })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
        var result = new BadRequestObjectResult(problem);
        var filters = new List<IFilterMetadata>();
        var executing = new ResultExecutingContext(
            actionContext, filters, result, new object());
        var filter = new SystemExceptionResultLoggingFilter(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SystemExceptionResultLoggingFilter>.Instance);
        var resultExecuted = false;

        await filter.OnResultExecutionAsync(executing, () =>
        {
            resultExecuted = true;
            return Task.FromResult(new ResultExecutedContext(
                actionContext, filters, result, new object()));
        });

        resultExecuted.Should().BeTrue();
        repository.Verify(value => value.AddAsync(It.Is<SystemExceptionLog>(entry =>
            entry.TenantId == tenantId &&
            entry.UserId == userId &&
            entry.Username == "procurement.user" &&
            entry.TraceId == "validation-trace-1" &&
            entry.RequestPath == "/api/procurement/supplier-onboarding-tokens" &&
            entry.ExceptionType == "HandledApiValidationProblem" &&
            entry.FullMessage != null &&
            entry.FullMessage.Contains("token=[REDACTED]") &&
            !entry.FullMessage.Contains("plain-secret"))), Times.Once);
        repository.Verify(value => value.FirstOrDefaultAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<SystemExceptionLog, bool>>>()),
            Times.Never);
        repository.Verify(value => value.UpdateAsync(It.IsAny<SystemExceptionLog>()),
            Times.Never);
        unitOfWork.Verify(value => value.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
