using System.Reflection;
using ErpSystem.Core.Entities;
using ErpSystem.Data;
using ErpSystem.Shared;
using ErpSystem.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public class DatabaseSeedingServiceTests
{
    [Fact]
    public async Task EnsureProjectWorkflowsSeededAsync_ShouldCreateBaselineProjectWorkflowDefinitions()
    {
        await using var context = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Tenant",
            Code = "TEST",
            Status = TenantStatus.Active,
            ContactEmail = "tenant@test.local",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        var service = new DatabaseSeedingService(
            context,
            CreateUserManager(),
            CreateRoleManager(),
            NullLogger<DatabaseSeedingService>.Instance,
            CreateEnvironment());

        var seedMethod = typeof(DatabaseSeedingService)
            .GetMethod("EnsureProjectWorkflowsSeededAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        seedMethod.Should().NotBeNull();
        await ((Task)seedMethod!.Invoke(service, null)!).ConfigureAwait(false);

        var definitions = await context.WorkflowDefinitions
            .Include(definition => definition.EntityType)
            .Include(definition => definition.Steps)
            .Include(definition => definition.Transitions)
            .Where(definition => definition.TenantId == tenant.Id)
            .OrderBy(definition => definition.Name)
            .ToListAsync();

        definitions.Should().HaveCount(3);
        definitions.Select(definition => definition.Name).Should().BeEquivalentTo(new[]
        {
            "Project Approval",
            "Project Budget Revision Approval",
            "Project Closure Approval"
        });

        var projectDefinition = definitions.Single(definition => definition.Name == "Project Approval");
        projectDefinition.Steps.Select(step => step.Name).Should().Contain(new[] { "Draft", "PendingApproval", "Approved" });
        projectDefinition.Transitions.Select(transition => transition.Name).Should().BeEquivalentTo(new[] { "Submit", "Approve" });

        var closureDefinition = definitions.Single(definition => definition.Name == "Project Closure Approval");
        closureDefinition.Steps.Select(step => step.Name).Should().Contain("Closed");

        await ((Task)seedMethod.Invoke(service, null)!).ConfigureAwait(false);

        (await context.WorkflowDefinitions.CountAsync(definition => definition.TenantId == tenant.Id)).Should().Be(3);
        (await context.WorkflowEntityTypes.CountAsync(entityType => entityType.TenantId == tenant.Id)).Should().Be(3);
    }

    [Fact]
    public async Task EnsureProjectCatalogDefaultsSeededAsync_ShouldCreateRecommendedCatalogEntries()
    {
        await using var context = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Tenant",
            Code = "TEST",
            Status = TenantStatus.Active,
            ContactEmail = "tenant@test.local",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        var service = new DatabaseSeedingService(
            context,
            CreateUserManager(),
            CreateRoleManager(),
            NullLogger<DatabaseSeedingService>.Instance,
            CreateEnvironment());

        var seedMethod = typeof(DatabaseSeedingService)
            .GetMethod("EnsureProjectCatalogDefaultsSeededAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        seedMethod.Should().NotBeNull();
        await ((Task)seedMethod!.Invoke(service, null)!).ConfigureAwait(false);

        var entries = await context.ProjectCatalogEntries
            .Where(entry => entry.TenantId == tenant.Id)
            .ToListAsync();

        entries.Should().NotBeEmpty();
        entries.Should().Contain(entry => entry.CatalogType == "methodologies" && entry.Code == "Hybrid");
        entries.Should().Contain(entry => entry.CatalogType == "billing-types" && entry.Code == "FixedPrice");
        entries.Should().Contain(entry => entry.CatalogType == "resource-roles" && entry.Code == "ProjectManager");

        var initialCount = entries.Count;
        await ((Task)seedMethod.Invoke(service, null)!).ConfigureAwait(false);

        (await context.ProjectCatalogEntries.CountAsync(entry => entry.TenantId == tenant.Id)).Should().Be(initialCount);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static UserManager<ApplicationUser> CreateUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new UserManager<ApplicationUser>(
            store.Object,
            null!,
            null!,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            null!,
            null!,
            null!,
            null!);
    }

    private static RoleManager<ApplicationRole> CreateRoleManager()
    {
        var store = new Mock<IRoleStore<ApplicationRole>>();
        return new RoleManager<ApplicationRole>(
            store.Object,
            Array.Empty<IRoleValidator<ApplicationRole>>(),
            null!,
            null!,
            null!);
    }

    private static IWebHostEnvironment CreateEnvironment()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns("Testing");
        return environment.Object;
    }
}
