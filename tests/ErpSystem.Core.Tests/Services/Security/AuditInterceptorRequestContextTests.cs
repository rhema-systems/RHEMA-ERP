using System.Security.Claims;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Security;

public sealed class AuditInterceptorRequestContextTests
{
    [Fact]
    public async Task Auditing_uses_request_claims_without_resolving_a_second_database_context()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "inventory.acceptance"),
                new Claim("tenant_id", tenantId.ToString())
            ], "Test"))
        };
        httpContext.Request.Headers.UserAgent = "INV-REQ-FU-004";

        var interceptor = new AuditInterceptor(
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<AuditInterceptor>.Instance,
            AuditConfiguration.CreateDefault());
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new ApplicationDbContext(options);
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "AUDIT-CTX",
            Name = "Audit request-context proof"
        };
        context.Warehouses.Add(warehouse);

        await context.SaveChangesAsync();

        var audit = await context.AuditLogs.SingleAsync(value => value.ResourceId == warehouse.Id.ToString());
        audit.UserId.Should().Be(userId);
        audit.Username.Should().Be("inventory.acceptance");
        audit.TenantId.Should().Be(tenantId);
        audit.UserAgent.Should().Be("INV-REQ-FU-004");
    }

    [Fact]
    public async Task Unauthenticated_system_save_does_not_create_a_request_audit_entry()
    {
        var interceptor = new AuditInterceptor(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            NullLogger<AuditInterceptor>.Instance,
            AuditConfiguration.CreateDefault());
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.Warehouses.Add(new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Code = "SYSTEM-SEED",
            Name = "Unauthenticated system seed"
        });

        await context.SaveChangesAsync();

        (await context.Warehouses.CountAsync()).Should().Be(1);
        (await context.AuditLogs.CountAsync()).Should().Be(0);
    }
}
