using System.Net;
using System.Net.Http.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public class ProjectMobileControllerTests
{
    [Fact]
    public async Task GetSummary_ShouldReturnMockedMobileSummary()
    {
        var userId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetMobileSummaryAsync(userId))
            .ReturnsAsync(new ProjectMobileSummaryDto
            {
                AssignmentCount = 2,
                OverdueCount = 1,
                PendingHours = 6.5m,
                PendingExpenses = 48m,
                Assignments =
                {
                    new ProjectMobileAssignmentDto
                    {
                        ProjectId = Guid.NewGuid(),
                        WorkItemId = Guid.NewGuid(),
                        ProjectCode = "PRJ-2026-0099",
                        ProjectTitle = "Field Rollout",
                        WorkItemTitle = "Install gateway",
                        Status = "In Progress",
                        PercentComplete = 50
                    }
                }
            });

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/mobile/projects/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<ProjectMobileSummaryDto>();
        summary.Should().NotBeNull();
        summary!.AssignmentCount.Should().Be(2);
        summary.Assignments.Should().ContainSingle();
        summary.Assignments[0].ProjectCode.Should().Be("PRJ-2026-0099");
    }

    [Fact]
    public async Task SubmitTimesheet_ShouldReturnMockedEntry()
    {
        var projectId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new CreateProjectTimesheetEntryDto
        {
            WorkItemId = workItemId,
            UserId = userId,
            EntryDate = new DateTime(2026, 3, 9),
            Hours = 7.5m,
            IsBillable = true,
            HourlyRate = 120m,
            WorkType = "Field",
            Notes = "Commissioning support"
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.SubmitMobileTimesheetAsync(projectId, workItemId, It.IsAny<CreateProjectTimesheetEntryDto>(), userId))
            .ReturnsAsync(new ProjectTimesheetEntryDto
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                WorkItemId = workItemId,
                UserId = userId,
                EntryDate = request.EntryDate,
                Hours = request.Hours,
                IsBillable = request.IsBillable,
                HourlyRate = request.HourlyRate,
                CostAmount = request.Hours * request.HourlyRate,
                WorkType = request.WorkType,
                Notes = request.Notes,
                Status = "PendingApproval"
            });

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/mobile/projects/{projectId}/timesheets", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = await response.Content.ReadFromJsonAsync<ProjectTimesheetEntryDto>();
        entry.Should().NotBeNull();
        entry!.ProjectId.Should().Be(projectId);
        entry.WorkItemId.Should().Be(workItemId);
        entry.Status.Should().Be("PendingApproval");
        entry.CostAmount.Should().Be(900m);
    }

    private static WebApplicationFactory<Program> CreateFactory(Mock<IProjectService> projectService, Guid userId)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CandidatePortal:PortalUrl", "https://candidate.test/");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IAuthorizationHandler>();
                services.RemoveAll<IProjectService>();
                services.RemoveAll<ICurrentUserProvider>();

                services.AddSingleton<IPolicyEvaluator, TestPolicyEvaluator>();
                services.AddSingleton<IAuthorizationHandler, AllowAnonymousHandler>();
                services.AddSingleton(projectService.Object);
                services.AddSingleton<ICurrentUserProvider>(new FakeCurrentUserProvider(userId, Guid.NewGuid()));
            });
        });
}
