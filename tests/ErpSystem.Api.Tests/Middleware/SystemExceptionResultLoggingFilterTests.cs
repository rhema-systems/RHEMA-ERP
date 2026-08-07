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
        unitOfWork.Verify(value => value.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
