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

public class ProjectsControllerRouteTests
{
    [Fact]
    public async Task CreateProject_ShouldReturnCreatedProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var request = new CreateProjectDto
        {
            Title = "New Delivery Program",
            Methodology = "Hybrid",
            EstimatedBudget = 150000m,
            StartDate = new DateTime(2026, 3, 10),
            TargetEndDate = new DateTime(2026, 8, 31),
            ApprovalRequired = true
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.CreateProjectAsync(It.Is<CreateProjectDto>(dto => dto.Title == request.Title)))
            .ReturnsAsync(new ProjectDetailDto
            {
                Id = projectId,
                Title = request.Title,
                Methodology = request.Methodology,
                EstimatedBudget = request.EstimatedBudget,
                Status = "Draft",
                ApprovalRequired = true,
                ProjectCode = "PRJ-2026-0200",
                CreatedAt = DateTime.UtcNow
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        project.Should().NotBeNull();
        project!.Id.Should().Be(projectId);
        project.Title.Should().Be(request.Title);
        project.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task GetProject_ShouldReturnMockedProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProjectByIdAsync(projectId))
            .ReturnsAsync(new ProjectDetailDto
            {
                Id = projectId,
                ProjectCode = "PRJ-2026-0001",
                Title = "Route Test Project",
                Status = "Planned",
                ApprovalRequired = true,
                ExternalPortalAccessEnabled = true,
                ExternalCollaborationEnabled = false,
                CreatedAt = DateTime.UtcNow
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var project = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        project.Should().NotBeNull();
        project!.Id.Should().Be(projectId);
        project.Title.Should().Be("Route Test Project");
        project.ProjectCode.Should().Be("PRJ-2026-0001");
    }

    [Fact]
    public async Task GetAiInsights_ShouldReturnMockedInsights()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetAiInsightsAsync(projectId))
            .ReturnsAsync(new[]
            {
                new ProjectAiInsightDto
                {
                    Category = "Risk",
                    Severity = "High",
                    Title = "Schedule compression risk",
                    Recommendation = "Re-sequence critical tasks."
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/ai-insights");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var insights = await response.Content.ReadFromJsonAsync<List<ProjectAiInsightDto>>();
        insights.Should().NotBeNull();
        insights.Should().HaveCount(1);
        insights![0].Severity.Should().Be("High");
        insights[0].Title.Should().Be("Schedule compression risk");
    }

    [Fact]
    public async Task SubmitAndApproveProject_ShouldForwardCurrentUserAndComments()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.SubmitProjectForApprovalAsync(projectId, userId))
            .Returns(Task.CompletedTask);
        projectService
            .Setup(service => service.ApproveProjectAsync(projectId, userId, "Looks good"))
            .Returns(Task.CompletedTask);

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var submitResponse = await client.PostAsync($"/api/projects/{projectId}/submit", content: null);
        var approveResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/approve", new { comments = "Looks good" });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        projectService.Verify(service => service.SubmitProjectForApprovalAsync(projectId, userId), Times.Once);
        projectService.Verify(service => service.ApproveProjectAsync(projectId, userId, "Looks good"), Times.Once);
    }

    [Fact]
    public async Task BudgetRevisionWorkflow_ShouldCreateSubmitAndApproveRevision()
    {
        var projectId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new CreateProjectBudgetRevisionDto
        {
            RevisionName = "Q2 Rebaseline",
            RevisionType = "Revision",
            EstimatedBudget = 210000m,
            ApprovedBudget = 200000m,
            CommittedCost = 65000m,
            ForecastCost = 185000m,
            ThresholdWarningPercent = 75m,
            ThresholdCriticalPercent = 90m,
            ChangeReason = "Approved scope increase",
            Notes = "Add two rollout sites"
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.CreateBudgetRevisionAsync(projectId, It.Is<CreateProjectBudgetRevisionDto>(dto => dto.RevisionName == request.RevisionName)))
            .ReturnsAsync(new ProjectBudgetRevisionDto
            {
                Id = revisionId,
                ProjectId = projectId,
                RevisionName = request.RevisionName,
                RevisionType = request.RevisionType,
                EstimatedBudget = request.EstimatedBudget,
                ApprovedBudget = request.ApprovedBudget,
                Status = "Draft",
                ChangeReason = request.ChangeReason
            });
        projectService
            .Setup(service => service.SubmitBudgetRevisionAsync(revisionId, userId))
            .ReturnsAsync(new ProjectBudgetRevisionDto
            {
                Id = revisionId,
                ProjectId = projectId,
                RevisionName = request.RevisionName,
                Status = "PendingApproval"
            });
        projectService
            .Setup(service => service.ApproveBudgetRevisionAsync(revisionId, userId, "Approved for execution"))
            .ReturnsAsync(new ProjectBudgetRevisionDto
            {
                Id = revisionId,
                ProjectId = projectId,
                RevisionName = request.RevisionName,
                Status = "Approved",
                ApprovedAt = DateTime.UtcNow,
                ApprovedById = userId
            });

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/budget-revisions", request);
        var submitResponse = await client.PostAsync($"/api/projects/budget-revisions/{revisionId}/submit", content: null);
        var approveResponse = await client.PostAsJsonAsync($"/api/projects/budget-revisions/{revisionId}/approve", new { comments = "Approved for execution" });

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createdRevision = await createResponse.Content.ReadFromJsonAsync<ProjectBudgetRevisionDto>();
        var submittedRevision = await submitResponse.Content.ReadFromJsonAsync<ProjectBudgetRevisionDto>();
        var approvedRevision = await approveResponse.Content.ReadFromJsonAsync<ProjectBudgetRevisionDto>();

        createdRevision.Should().NotBeNull();
        submittedRevision.Should().NotBeNull();
        approvedRevision.Should().NotBeNull();
        createdRevision!.Status.Should().Be("Draft");
        submittedRevision!.Status.Should().Be("PendingApproval");
        approvedRevision!.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task InvoiceRequestWorkflow_ShouldSubmitAndSendToFinance()
    {
        var invoiceRequestId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.SubmitInvoiceRequestAsync(invoiceRequestId, "Ready for finance"))
            .ReturnsAsync(new ProjectInvoiceRequestDto
            {
                Id = invoiceRequestId,
                ProjectId = projectId,
                RequestNumber = "INVREQ-2026-0200",
                RequestedAmount = 850m,
                Status = "Submitted",
                SubmittedAt = DateTime.UtcNow
            });
        projectService
            .Setup(service => service.MarkInvoiceRequestSentToFinanceAsync(invoiceRequestId, "AR-2026-0091", "Exported"))
            .ReturnsAsync(new ProjectInvoiceRequestDto
            {
                Id = invoiceRequestId,
                ProjectId = projectId,
                RequestNumber = "INVREQ-2026-0200",
                RequestedAmount = 850m,
                Status = "SentToFinance",
                SubmittedAt = DateTime.UtcNow,
                ExternalReference = "AR-2026-0091"
            });

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var submitResponse = await client.PostAsJsonAsync($"/api/projects/invoice-requests/{invoiceRequestId}/submit", new { comments = "Ready for finance" });
        var sendResponse = await client.PostAsJsonAsync($"/api/projects/invoice-requests/{invoiceRequestId}/send-to-finance", new
        {
            externalReference = "AR-2026-0091",
            comments = "Exported"
        });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitted = await submitResponse.Content.ReadFromJsonAsync<ProjectInvoiceRequestDto>();
        var sent = await sendResponse.Content.ReadFromJsonAsync<ProjectInvoiceRequestDto>();

        submitted.Should().NotBeNull();
        sent.Should().NotBeNull();
        submitted!.Status.Should().Be("Submitted");
        sent!.Status.Should().Be("SentToFinance");
        sent.ExternalReference.Should().Be("AR-2026-0091");
    }

    [Fact]
    public async Task InvoiceRequestFinanceCompletion_ShouldMarkInvoicedAndPaid()
    {
        var invoiceRequestId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.MarkInvoiceRequestInvoicedAsync(invoiceRequestId, "INV-2026-0101", "Invoice raised"))
            .ReturnsAsync(new ProjectInvoiceRequestDto
            {
                Id = invoiceRequestId,
                ProjectId = projectId,
                RequestNumber = "INVREQ-2026-0200",
                RequestedAmount = 850m,
                Status = "Invoiced",
                ExternalReference = "INV-2026-0101"
            });
        projectService
            .Setup(service => service.MarkInvoiceRequestPaidAsync(invoiceRequestId, "Cash received"))
            .ReturnsAsync(new ProjectInvoiceRequestDto
            {
                Id = invoiceRequestId,
                ProjectId = projectId,
                RequestNumber = "INVREQ-2026-0200",
                RequestedAmount = 850m,
                Status = "Paid",
                ExternalReference = "INV-2026-0101"
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var invoicedResponse = await client.PostAsJsonAsync($"/api/projects/invoice-requests/{invoiceRequestId}/mark-invoiced", new
        {
            externalReference = "INV-2026-0101",
            comments = "Invoice raised"
        });
        var paidResponse = await client.PostAsJsonAsync($"/api/projects/invoice-requests/{invoiceRequestId}/mark-paid", new
        {
            comments = "Cash received"
        });

        invoicedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        paidResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invoiced = await invoicedResponse.Content.ReadFromJsonAsync<ProjectInvoiceRequestDto>();
        var paid = await paidResponse.Content.ReadFromJsonAsync<ProjectInvoiceRequestDto>();

        invoiced.Should().NotBeNull();
        paid.Should().NotBeNull();
        invoiced!.Status.Should().Be("Invoiced");
        invoiced.ExternalReference.Should().Be("INV-2026-0101");
        paid!.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task InvoiceRequestQueueReport_ShouldReturnFinanceQueueItems()
    {
        var projectId = Guid.NewGuid();
        var invoiceRequestId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetInvoiceRequestQueueReportAsync(100, "Submitted"))
            .ReturnsAsync(new[]
            {
                new ProjectInvoiceRequestQueueItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-FIN-1",
                    ProjectTitle = "Finance Queue Project",
                    InvoiceRequestId = invoiceRequestId,
                    RequestNumber = "INVREQ-2026-0501",
                    Status = "Submitted",
                    QueueStage = "Approval Complete",
                    RequestedAmount = 1250m,
                    Currency = "USD",
                    RequestedAt = DateTime.UtcNow.AddDays(-2),
                    DaysOutstanding = 2
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/invoice-request-queue?take=100&status=Submitted");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectInvoiceRequestQueueItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].RequestNumber.Should().Be("INVREQ-2026-0501");
        queue[0].QueueStage.Should().Be("Approval Complete");
    }

    [Fact]
    public async Task WorkflowApprovalQueueReport_ShouldReturnWorkflowItems()
    {
        var projectId = Guid.NewGuid();
        var deliverableId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetWorkflowApprovalQueueReportAsync(100, "ProjectDeliverable"))
            .ReturnsAsync(new[]
            {
                new ProjectWorkflowApprovalQueueItemDto
                {
                    EntityType = "ProjectDeliverable",
                    EntityId = deliverableId,
                    ProjectId = projectId,
                    ProjectCode = "PRJ-WF-1",
                    ProjectTitle = "Workflow Queue Project",
                    ItemTitle = "Acceptance dossier",
                    Status = "PendingApproval",
                    SubmittedAt = DateTime.UtcNow.AddDays(-2),
                    DaysPending = 2,
                    QueueStage = "Awaiting Approval"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/workflow-approval-queue?take=100&entityType=ProjectDeliverable");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectWorkflowApprovalQueueItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].EntityType.Should().Be("ProjectDeliverable");
        queue[0].QueueStage.Should().Be("Awaiting Approval");
    }

    [Fact]
    public async Task PortfolioPrioritizationReport_ShouldReturnRankedProjects()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetPortfolioPrioritizationReportAsync(null, 50))
            .ReturnsAsync(new[]
            {
                new ProjectPortfolioPrioritizationReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-PORT-1",
                    ProjectTitle = "Priority Rollout",
                    Status = "AtRisk",
                    HealthStatus = "Watch",
                    PriorityScore = 38m,
                    PriorityBand = "Stabilize",
                    OpenRiskCount = 2,
                    OpenIssueCount = 1,
                    OverdueMilestoneCount = 1,
                    RecommendedAction = "Escalate governance review, address blockers, and rebaseline near-term commitments."
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/portfolio-prioritization?take=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectPortfolioPrioritizationReportItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].PriorityBand.Should().Be("Stabilize");
        queue[0].PriorityScore.Should().Be(38m);
    }

    [Fact]
    public async Task StrategicInitiativeReport_ShouldReturnGroupedInitiatives()
    {
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetStrategicInitiativeReportAsync(null, 25))
            .ReturnsAsync(new[]
            {
                new ProjectStrategicInitiativeReportItemDto
                {
                    Initiative = "Operating resilience",
                    ProjectCount = 2,
                    ActiveProjectCount = 1,
                    DelayedProjectCount = 1,
                    HighRiskItemCount = 3,
                    TotalEstimatedBudget = 180000m,
                    PortfolioNames = new List<string> { "Transformation" },
                    ProgramNames = new List<string> { "ERP Delivery" }
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/strategic-initiatives?take=25");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectStrategicInitiativeReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].Initiative.Should().Be("Operating resilience");
        report[0].ProjectCount.Should().Be(2);
    }

    [Fact]
    public async Task StrategicInitiativeReport_ShouldReturnForbiddenWhenServiceDeniesAccess()
    {
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetStrategicInitiativeReportAsync(null, 25))
            .ThrowsAsync(new UnauthorizedAccessException("Forbidden"));

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/strategic-initiatives?take=25");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DependencyWatchReport_ShouldReturnCrossProjectWatchItems()
    {
        var dependencyId = Guid.NewGuid();
        var sourceProjectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetDependencyWatchReportAsync(null, null, 25))
            .ReturnsAsync(new[]
            {
                new ProjectDependencyWatchReportItemDto
                {
                    InterdependencyId = dependencyId,
                    SourceProjectId = sourceProjectId,
                    TargetProjectId = targetProjectId,
                    SourceProjectCode = "PRJ-DEP-1",
                    SourceProjectTitle = "Source Project",
                    TargetProjectCode = "PRJ-DEP-2",
                    TargetProjectTitle = "Target Project",
                    DependencyType = "Schedule",
                    Status = "Open",
                    ImpactLevel = "Critical",
                    Title = "Environment handoff",
                    CoordinationState = "Overdue",
                    DueDate = DateTime.UtcNow.Date.AddDays(-1)
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/dependency-watch?take=25");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<List<ProjectDependencyWatchReportItemDto>>();
        report.Should().NotBeNull();
        report.Should().ContainSingle();
        report![0].Title.Should().Be("Environment handoff");
        report[0].CoordinationState.Should().Be("Overdue");
    }

    [Fact]
    public async Task DependencyWatchReport_ShouldReturnForbiddenWhenServiceDeniesAccess()
    {
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetDependencyWatchReportAsync(null, null, 25))
            .ThrowsAsync(new UnauthorizedAccessException("Forbidden"));

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/dependency-watch?take=25");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MaterialReconciliationReport_ShouldReturnProjectVariance()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetMaterialReconciliationReportAsync(200, "UnderTracked"))
            .ReturnsAsync(new[]
            {
                new ProjectMaterialReconciliationReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-MAT-001",
                    ProjectTitle = "Materials Reconciliation",
                    Status = "InProgress",
                    RequisitionCount = 2,
                    PendingRequisitionCount = 1,
                    IssuedRequisitionCount = 1,
                    RequestedValue = 420m,
                    IssuedValue = 300m,
                    ReturnedValue = 20m,
                    NetIssuedValue = 280m,
                    TrackedMaterialCost = 200m,
                    MaterialCostVariance = 80m,
                    ReconciliationStatus = "UnderTracked"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/material-reconciliation?reconciliationStatus=UnderTracked&take=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectMaterialReconciliationReportItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].MaterialCostVariance.Should().Be(80m);
        queue[0].ReconciliationStatus.Should().Be("UnderTracked");
    }

    [Fact]
    public async Task ProcurementReconciliationReport_ShouldReturnReceiptIssueAndPostingVariance()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetProcurementReconciliationReportAsync(200, "PendingInspection"))
            .ReturnsAsync(new[]
            {
                new ProjectProcurementReconciliationReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-PROC-001",
                    ProjectTitle = "Procurement Reconciliation",
                    Status = "InProgress",
                    PurchaseRequisitionCount = 1,
                    OpenPurchaseRequisitionCount = 0,
                    PurchaseRequisitionAmount = 800m,
                    PurchaseOrderCount = 1,
                    OpenPurchaseOrderCount = 1,
                    PurchaseOrderAmount = 800m,
                    PurchaseReceiptCount = 1,
                    ReceivedAmount = 600m,
                    AcceptedReceiptAmount = 500m,
                    PendingInspectionAmount = 100m,
                    IssuedInventoryValue = 450m,
                    NetIssuedInventoryValue = 450m,
                    PostedMaterialCost = 300m,
                    ReceiptToIssueVariance = 50m,
                    IssueToPostingVariance = 150m,
                    ReconciliationStatus = "PendingInspection"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/procurement-reconciliation?reconciliationStatus=PendingInspection&take=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectProcurementReconciliationReportItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].AcceptedReceiptAmount.Should().Be(500m);
        queue[0].IssueToPostingVariance.Should().Be(150m);
        queue[0].ReconciliationStatus.Should().Be("PendingInspection");
    }

    [Fact]
    public async Task FinancialControlSummary_ShouldReturnForbiddenWhenServiceDeniesAccess()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetFinancialControlSummaryAsync(projectId))
            .ThrowsAsync(new UnauthorizedAccessException("Forbidden"));

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{projectId}/financial-control-summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExternalCollaborationReport_ShouldReturnSharedProjects()
    {
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetExternalCollaborationReportAsync(200, "ActionRequired"))
            .ReturnsAsync(new[]
            {
                new ProjectExternalCollaborationReportItemDto
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-EXT-2",
                    ProjectTitle = "Portal Oversight",
                    Status = "InProgress",
                    ExternalPortalAccessEnabled = true,
                    ExternalCollaborationEnabled = true,
                    PolicyCount = 2,
                    ExternalVisibleDocumentCount = 3,
                    ExternalVisibleDeliverableCount = 2,
                    PendingExternalSubmissionCount = 1,
                    PendingExternalSignOffCount = 1,
                    ExternalCommentCount = 4,
                    CollaborationState = "ActionRequired"
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/reports/external-collaboration?collaborationState=ActionRequired&take=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ProjectExternalCollaborationReportItemDto>>();
        items.Should().NotBeNull();
        items.Should().ContainSingle();
        items![0].ProjectCode.Should().Be("PRJ-EXT-2");
        items[0].PendingExternalSignOffCount.Should().Be(1);
        items[0].CollaborationState.Should().Be("ActionRequired");
    }

    [Fact]
    public async Task TimesheetApprovalQueue_ShouldReturnSubmittedItems()
    {
        var projectId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetTimesheetApprovalQueueAsync(projectId, "Submitted", null, 100))
            .ReturnsAsync(new[]
            {
                new ProjectTimesheetApprovalQueueItemDto
                {
                    EntryId = entryId,
                    ProjectId = projectId,
                    ProjectCode = "PRJ-TS-9",
                    ProjectTitle = "Approval Route Project",
                    UserId = Guid.NewGuid(),
                    EntryDate = new DateTime(2026, 3, 10),
                    Hours = 6m,
                    HourlyRate = 30m,
                    CostAmount = 180m,
                    WorkType = "Implementation",
                    Status = "Submitted",
                    QueueStage = "Pending Approval",
                    DaysOpen = 2
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/timesheets/approval-queue?projectId={projectId}&status=Submitted&take=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectTimesheetApprovalQueueItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].QueueStage.Should().Be("Pending Approval");
    }

    [Fact]
    public async Task ExpenseApprovalQueue_ShouldReturnSubmittedItems()
    {
        var projectId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.GetExpenseApprovalQueueAsync(projectId, "Submitted", null, 100))
            .ReturnsAsync(new[]
            {
                new ProjectExpenseApprovalQueueItemDto
                {
                    ExpenseId = expenseId,
                    ProjectId = projectId,
                    ProjectCode = "PRJ-EX-9",
                    ProjectTitle = "Expense Route Project",
                    UserId = Guid.NewGuid(),
                    ExpenseDate = new DateTime(2026, 3, 10),
                    Category = "Travel",
                    Currency = "USD",
                    Amount = 90m,
                    TaxAmount = 9m,
                    TotalAmount = 99m,
                    Status = "Submitted",
                    QueueStage = "Pending Approval",
                    DaysOpen = 3
                }
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/expenses/approval-queue?projectId={projectId}&status=Submitted&take=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<ProjectExpenseApprovalQueueItemDto>>();
        queue.Should().NotBeNull();
        queue.Should().ContainSingle();
        queue![0].QueueStage.Should().Be("Pending Approval");
    }

    [Fact]
    public async Task AddQualityCheckpoint_ShouldReturnCheckpoint()
    {
        var projectId = Guid.NewGuid();
        var checkpointId = Guid.NewGuid();
        var request = new CreateProjectQualityCheckpointDto
        {
            Title = "Configuration QA gate",
            Status = "Open",
            RequiresQaSignOff = true
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.AddQualityCheckpointAsync(projectId, It.Is<CreateProjectQualityCheckpointDto>(dto => dto.Title == request.Title)))
            .ReturnsAsync(new ProjectQualityCheckpointDto
            {
                Id = checkpointId,
                ProjectId = projectId,
                Title = request.Title,
                Status = request.Status,
                RequiresQaSignOff = request.RequiresQaSignOff
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/quality-checkpoints", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkpoint = await response.Content.ReadFromJsonAsync<ProjectQualityCheckpointDto>();
        checkpoint.Should().NotBeNull();
        checkpoint!.Id.Should().Be(checkpointId);
        checkpoint.Title.Should().Be("Configuration QA gate");
        checkpoint.RequiresQaSignOff.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveNonConformance_ShouldReturnResolvedItem()
    {
        var nonConformanceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.ResolveNonConformanceAsync(nonConformanceId, "Corrective action completed"))
            .ReturnsAsync(new ProjectNonConformanceDto
            {
                Id = nonConformanceId,
                ProjectId = projectId,
                Title = "Interface mapping mismatch",
                Severity = "High",
                Status = "Resolved",
                ResolutionNotes = "Corrective action completed",
                ResolvedAt = DateTime.UtcNow
            });

        using var factory = CreateFactory(projectService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/non-conformances/{nonConformanceId}/resolve", new
        {
            comments = "Corrective action completed"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var nonConformance = await response.Content.ReadFromJsonAsync<ProjectNonConformanceDto>();
        nonConformance.Should().NotBeNull();
        nonConformance!.Status.Should().Be("Resolved");
        nonConformance.ResolutionNotes.Should().Be("Corrective action completed");
    }

    [Fact]
    public async Task ClosureWorkflow_ShouldUpsertSubmitAndApproveClosure()
    {
        var projectId = Guid.NewGuid();
        var closureId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new UpsertProjectClosureDto
        {
            FinalBudget = 200000m,
            FinalCost = 192500m,
            DeliverablesAccepted = true,
            TasksCompletedOrWaived = true,
            AssetsReconciled = true,
            OpenItemsDisposed = true,
            LessonsLearnedSummary = "Early procurement planning reduced delays.",
            PostImplementationReview = "Objectives met."
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.UpsertClosureAsync(projectId, It.Is<UpsertProjectClosureDto>(dto => dto.FinalCost == request.FinalCost)))
            .ReturnsAsync(new ProjectClosureDto
            {
                Id = closureId,
                ProjectId = projectId,
                Status = "Draft",
                FinalBudget = request.FinalBudget,
                FinalCost = request.FinalCost,
                DeliverablesAccepted = request.DeliverablesAccepted,
                TasksCompletedOrWaived = request.TasksCompletedOrWaived,
                AssetsReconciled = request.AssetsReconciled,
                OpenItemsDisposed = request.OpenItemsDisposed,
                LessonsLearnedSummary = request.LessonsLearnedSummary,
                PostImplementationReview = request.PostImplementationReview
            });
        projectService
            .Setup(service => service.SubmitClosureForApprovalAsync(projectId, userId))
            .Returns(Task.CompletedTask);
        projectService
            .Setup(service => service.ApproveClosureAsync(closureId, userId, "Closure accepted"))
            .Returns(Task.CompletedTask);

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var upsertResponse = await client.PutAsJsonAsync($"/api/projects/{projectId}/closure", request);
        var submitResponse = await client.PostAsync($"/api/projects/{projectId}/closure/submit", content: null);
        var approveResponse = await client.PostAsJsonAsync($"/api/projects/closure/{closureId}/approve", new { comments = "Closure accepted" });

        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var closure = await upsertResponse.Content.ReadFromJsonAsync<ProjectClosureDto>();
        closure.Should().NotBeNull();
        closure!.Status.Should().Be("Draft");
        closure.FinalCost.Should().Be(request.FinalCost);

        projectService.Verify(service => service.SubmitClosureForApprovalAsync(projectId, userId), Times.Once);
        projectService.Verify(service => service.ApproveClosureAsync(closureId, userId, "Closure accepted"), Times.Once);
    }

    [Fact]
    public async Task SubmitExternalDeliverable_ShouldReturnMockedDeliverable()
    {
        var projectId = Guid.NewGuid();
        var deliverableId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new SubmitProjectDeliverableDto
        {
            SubmittedDocumentId = Guid.NewGuid(),
            Notes = "Portal submission"
        };

        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.SubmitExternalDeliverableAsync(projectId, deliverableId, It.IsAny<SubmitProjectDeliverableDto>(), userId))
            .ReturnsAsync(new ProjectDeliverableDto
            {
                Id = deliverableId,
                ProjectId = projectId,
                Title = "UAT Evidence",
                Status = "In Review",
                ExternalSubmissionAllowed = true,
                ExternalSignOffRequired = true,
                IsExternalVisible = true,
                SubmittedAt = DateTime.UtcNow,
                AcceptanceNotes = request.Notes
            });

        using var factory = CreateFactory(projectService, userId);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/external/my-projects/{projectId}/deliverables/{deliverableId}/submit", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deliverable = await response.Content.ReadFromJsonAsync<ProjectDeliverableDto>();
        deliverable.Should().NotBeNull();
        deliverable!.Id.Should().Be(deliverableId);
        deliverable.Status.Should().Be("In Review");
        deliverable.AcceptanceNotes.Should().Be("Portal submission");
    }

    private static WebApplicationFactory<Program> CreateFactory(Mock<IProjectService> projectService, Guid? userId = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
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
                services.AddSingleton<ICurrentUserProvider>(new FakeCurrentUserProvider(userId ?? Guid.NewGuid(), Guid.NewGuid()));
            });
        });
}

internal sealed class FakeCurrentUserProvider : ICurrentUserProvider
{
    public FakeCurrentUserProvider(Guid userId, Guid tenantId)
    {
        UserId = userId;
        TenantId = tenantId;
    }

    public Guid UserId { get; }
    public Guid TenantId { get; }
    public string Username => "test.user@erp.local";
    public string FullName => "Test User";
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => Array.Empty<string>();
    public IDictionary<string, string> Claims => new Dictionary<string, string>();
    public bool IsExternalUser => false;
    public string AuthenticationProvider => "Test";

    public bool HasRole(string role) => false;
}
