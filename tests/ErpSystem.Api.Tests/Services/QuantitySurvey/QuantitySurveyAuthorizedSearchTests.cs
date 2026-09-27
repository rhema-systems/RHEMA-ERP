using ErpSystem.Api.Services.QuantitySurvey;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Data;
using ErpSystem.Data.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyAuthorizedSearchTests
{
    [Fact]
    public async Task Civil_search_and_exact_read_apply_the_same_tenant_project_and_deleted_scope()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid(); var permitted = Guid.NewGuid(); var forbidden = Guid.NewGuid();
        db.Projects.AddRange(new Project { Id = permitted, TenantId = tenant }, new Project { Id = forbidden, TenantId = tenant });
        ProjectCivilDirectTaskControl TaskRow(Guid scope, Guid project, int day, bool deleted = false) => new()
        {
            TenantId = scope, ProjectId = project, Instructions = "Boundary survey", IsDeleted = deleted,
            DueDate = DateTime.UtcNow.AddDays(day), RowVersion = [1],
            WorkItem = new ProjectWorkItem { TenantId = scope, ProjectId = project, Title = "Boundary survey" }
        };
        for (var index = 0; index < 55; index++) db.Add(TaskRow(tenant, forbidden, index));
        var expected = TaskRow(tenant, permitted, -5);
        var otherTenant = TaskRow(Guid.NewGuid(), permitted, 10);
        var deleted = TaskRow(tenant, permitted, 10, true);
        db.AddRange(expected, otherTenant, deleted);
        await db.SaveChangesAsync();
        var actor = new Mock<ICurrentUserService>(); actor.SetupGet(value => value.TenantId).Returns(tenant);
        var projects = new Mock<IProjectService>();
        projects.Setup(value => value.GetProjectByIdAsync(permitted)).ReturnsAsync(new ProjectDetailDto { Id = permitted });
        var service = new CivilEngineeringDirectTaskService(db, actor.Object, projects.Object, Mock.Of<IWorkflowIntegrationService>());
        var found = await service.SearchAsync("Boundary", 1);
        found.Should().ContainSingle().Which.Id.Should().Be(expected.Id);
        (await service.GetAsync(expected.Id)).Id.Should().Be(expected.Id);
        await FluentActions.Awaiting(() => service.GetAsync(otherTenant.Id)).Should().ThrowAsync<CivilEngineeringDirectTaskNotFoundException>();
        await FluentActions.Awaiting(() => service.GetAsync(deleted.Id)).Should().ThrowAsync<CivilEngineeringDirectTaskNotFoundException>();
        var forbiddenTask = await db.Set<ProjectCivilDirectTaskControl>().FirstAsync(value => value.ProjectId == forbidden);
        await FluentActions.Awaiting(() => service.GetAsync(forbiddenTask.Id)).Should().ThrowAsync<UnauthorizedAccessException>();
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Search_continues_after_a_full_denied_page_and_keeps_the_owner_scope()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid(); var allowed = Guid.NewGuid(); var denied = Guid.NewGuid();
        for (var index = 0; index < 60; index++)
            db.ProjectWorkItems.Add(new ProjectWorkItem { TenantId = tenant, ProjectId = denied,
                Title = "Search target", CreatedAt = DateTime.UtcNow.AddMinutes(index) });
        var expected = new ProjectWorkItem { TenantId = tenant, ProjectId = allowed,
            Title = "Search target", CreatedAt = DateTime.UtcNow.AddDays(-2) };
        db.ProjectWorkItems.AddRange(expected,
            new ProjectWorkItem { TenantId = Guid.NewGuid(), ProjectId = allowed, Title = "Search target" },
            new ProjectWorkItem { TenantId = tenant, ProjectId = allowed, Title = "Search target", IsDeleted = true });
        // Preserve the deliberate age ordering so denied rows fill the first query batch.
        await db.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None);
        var calls = new List<Guid>();
        var result = await QuantitySurveyAuthorizedSearch.ReadAsync(db.ProjectWorkItems.AsNoTracking()
            .Where(value => value.TenantId == tenant && !value.IsDeleted && value.Title.Contains("target"))
            .OrderByDescending(value => value.CreatedAt).ThenBy(value => value.Id), value => value.ProjectId,
            id => { calls.Add(id); return Task.FromResult(id == allowed); }, 1, CancellationToken.None);
        result.Should().ContainSingle().Which.Id.Should().Be(expected.Id);
        calls.Count(id => id == denied).Should().Be(1);
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Cancelled_search_stops_before_reading_or_authorizing_candidates()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var calls = 0;
        var action = () => QuantitySurveyAuthorizedSearch.ReadAsync(db.ProjectWorkItems.OrderBy(value => value.Id),
            value => value.ProjectId, _ => { calls++; return Task.FromResult(true); }, 5, cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(0);
    }
}
