using System.Security.Claims;
using System.Text.Json;
using ErpSystem.Api.Middleware;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Middleware;

public sealed class GlobalExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task UnexpectedException_ReturnsFriendlyProblemWithoutDiagnosticDetails()
    {
        const string sensitiveMessage = "database password=top-secret failed";
        var context = CreateContext(new ServiceCollection().BuildServiceProvider());
        context.TraceIdentifier = "request-reference-123";

        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new ApplicationException(sensitiveMessage),
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Be("application/problem+json");

        var document = await ReadResponseAsync(context);
        document.RootElement.GetProperty("title").GetString()
            .Should().Be("We couldn't complete your request");
        document.RootElement.GetProperty("code").GetString()
            .Should().Be("UNEXPECTED_ERROR");
        document.RootElement.GetProperty("correlationId").GetString()
            .Should().Be("request-reference-123");
        document.RootElement.GetProperty("traceId").GetString()
            .Should().Be("request-reference-123");
        document.RootElement.GetProperty("detail").GetString()
            .Should().Contain("Reference ID: request-reference-123");

        var body = document.RootElement.GetRawText();
        body.Should().NotContain(sensitiveMessage);
        body.Should().NotContain("ApplicationException");
        body.Should().NotContain("stackTrace");
        body.Should().NotContain("developerMessage");
    }

    [Fact]
    public async Task UnexpectedException_PersistsRedactedDiagnosticsForTenantAdministratorAudit()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repository = new Mock<IGenericRepository<SystemExceptionLog>>();
        repository
            .Setup(value => value.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SystemExceptionLog, bool>>>()))
            .ReturnsAsync((SystemExceptionLog?)null);
        repository
            .Setup(value => value.AddAsync(It.IsAny<SystemExceptionLog>()))
            .ReturnsAsync((SystemExceptionLog value) => value);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.Repository<SystemExceptionLog>()).Returns(repository.Object);
        unitOfWork.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.TenantId).Returns(tenantId);

        var services = new ServiceCollection()
            .AddSingleton(unitOfWork.Object)
            .AddSingleton(currentUser.Object)
            .BuildServiceProvider();
        var context = CreateContext(services);
        context.TraceIdentifier = "audit-reference-456";
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/example";
        context.Request.QueryString = new QueryString("?token=plain-secret");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "tenant.admin")
        ], "test"));

        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("provider token=plain-secret was rejected"),
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        repository.Verify(value => value.AddAsync(It.Is<SystemExceptionLog>(entry =>
            entry.TenantId == tenantId &&
            entry.UserId == userId &&
            entry.Username == "tenant.admin" &&
            entry.TraceId == "audit-reference-456" &&
            entry.RequestMethod == HttpMethods.Post &&
            entry.RequestPath == "/api/example" &&
            entry.ExceptionType == typeof(InvalidOperationException).FullName &&
            entry.FullMessage != null &&
            entry.FullMessage.Contains("token=[REDACTED]") &&
            !entry.FullMessage.Contains("plain-secret") &&
            entry.QueryString != null &&
            entry.QueryString.Contains("token=[REDACTED]") &&
            !entry.QueryString.Contains("plain-secret") &&
            entry.StackTrace != null)), Times.Once);
        unitOfWork.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        var publicBody = (await ReadResponseAsync(context)).RootElement.GetRawText();
        publicBody.Should().NotContain("plain-secret");
        publicBody.Should().NotContain("stackTrace");
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonDocument> ReadResponseAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}
