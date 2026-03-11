using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[Authorize]
[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IInventoryRequisitionService _inventoryRequisitionService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        IProjectService projectService,
        IInventoryRequisitionService inventoryRequisitionService,
        ICurrentUserProvider currentUserProvider,
        ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _inventoryRequisitionService = inventoryRequisitionService;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private async Task<ActionResult<T>> ExecuteProjectReadAsync<T>(Func<Task<T>> action, string errorMessage)
    {
        try
        {
            return Ok(await action());
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, errorMessage);
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectDto>>> GetProjects([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? projectTypeId = null, [FromQuery] Guid? portfolioId = null, [FromQuery] Guid? programId = null)
    {
        try
        {
            return Ok(await _projectService.GetProjectsAsync(page, pageSize, search, status, projectTypeId, portfolioId, programId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects");
            return StatusCode(500, "An error occurred while retrieving projects");
        }
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<IEnumerable<ProjectLookupDto>>> LookupProjects([FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? projectTypeId = null, [FromQuery] Guid? portfolioId = null, [FromQuery] Guid? programId = null, [FromQuery] int take = 20)
        => await ExecuteProjectReadAsync(() => _projectService.LookupProjectsAsync(search, status, projectTypeId, portfolioId, programId, take), "Error looking up projects");

    [HttpGet("resources/lookup")]
    public async Task<ActionResult<IEnumerable<ProjectResourceLookupDto>>> LookupResources([FromQuery] string? search = null, [FromQuery] int take = 50)
        => await ExecuteProjectReadAsync(() => _projectService.LookupResourcesAsync(search, take), "Error looking up project resources");

    [HttpGet("dashboard")]
    public async Task<ActionResult<ProjectDashboardDto>> GetDashboard()
        => await ExecuteProjectReadAsync(() => _projectService.GetDashboardAsync(), "Error loading project dashboard");

    [HttpGet("reports/register")]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjectRegisterReport([FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? projectTypeId = null, [FromQuery] int take = 200)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectRegisterReportAsync(search, status, projectTypeId, take), "Error loading project register report");

    [HttpGet("reports/task-aging")]
    public async Task<ActionResult<IEnumerable<ProjectTaskAgingReportItemDto>>> GetTaskAgingReport([FromQuery] Guid? projectId = null, [FromQuery] int take = 100)
        => await ExecuteProjectReadAsync(() => _projectService.GetTaskAgingReportAsync(projectId, take), "Error loading task aging report");

    [HttpGet("reports/milestones")]
    public async Task<ActionResult<IEnumerable<ProjectMilestoneTrackerReportItemDto>>> GetMilestoneTrackerReport([FromQuery] Guid? projectId = null, [FromQuery] int take = 100)
        => await ExecuteProjectReadAsync(() => _projectService.GetMilestoneTrackerReportAsync(projectId, take), "Error loading milestone tracker report");

    [HttpGet("reports/budget-vs-actual")]
    public async Task<ActionResult<IEnumerable<ProjectBudgetActualReportItemDto>>> GetBudgetActualReport([FromQuery] int take = 200)
        => await ExecuteProjectReadAsync(() => _projectService.GetBudgetActualReportAsync(take), "Error loading budget vs actual report");

    [HttpGet("reports/risk-issue-summary")]
    public async Task<ActionResult<IEnumerable<ProjectRiskIssueSummaryReportItemDto>>> GetRiskIssueSummaryReport([FromQuery] int take = 200)
        => await ExecuteProjectReadAsync(() => _projectService.GetRiskIssueSummaryReportAsync(take), "Error loading risk and issue summary report");

    [HttpGet("reports/portfolio-summary")]
    public async Task<ActionResult<IEnumerable<ProjectPortfolioSummaryReportItemDto>>> GetPortfolioSummaryReport([FromQuery] int take = 100)
        => await ExecuteProjectReadAsync(() => _projectService.GetPortfolioSummaryReportAsync(take), "Error loading portfolio summary report");

    [HttpGet("reports/program-summary")]
    public async Task<ActionResult<IEnumerable<ProjectProgramSummaryReportItemDto>>> GetProgramSummaryReport([FromQuery] Guid? portfolioId = null, [FromQuery] int take = 100)
        => await ExecuteProjectReadAsync(() => _projectService.GetProgramSummaryReportAsync(portfolioId, take), "Error loading program summary report");

    [HttpGet("reports/performance-analytics")]
    public async Task<ActionResult<IEnumerable<ProjectPerformanceAnalyticsReportItemDto>>> GetPerformanceAnalyticsReport([FromQuery] int take = 200)
        => await ExecuteProjectReadAsync(() => _projectService.GetPerformanceAnalyticsReportAsync(take), "Error loading performance analytics report");

    [HttpGet("reports/portfolio-prioritization")]
    public async Task<ActionResult<IEnumerable<ProjectPortfolioPrioritizationReportItemDto>>> GetPortfolioPrioritizationReport([FromQuery] Guid? portfolioId = null, [FromQuery] int take = 100)
        => await ExecuteProjectReadAsync(() => _projectService.GetPortfolioPrioritizationReportAsync(portfolioId, take), "Error loading portfolio prioritization report");

    [HttpGet("reports/dependency-watch")]
    public async Task<ActionResult<IEnumerable<ProjectDependencyWatchReportItemDto>>> GetDependencyWatchReport([FromQuery] Guid? portfolioId = null, [FromQuery] Guid? programId = null, [FromQuery] int take = 100)
        => await ExecuteProjectReadAsync(() => _projectService.GetDependencyWatchReportAsync(portfolioId, programId, take), "Error loading dependency watch report");

    [HttpGet("reports/strategic-initiatives")]
    public async Task<ActionResult<IEnumerable<ProjectStrategicInitiativeReportItemDto>>> GetStrategicInitiativeReport([FromQuery] Guid? portfolioId = null, [FromQuery] int take = 100)
        => await ExecuteProjectReadAsync(() => _projectService.GetStrategicInitiativeReportAsync(portfolioId, take), "Error loading strategic initiative report");

    [HttpGet("reports/material-reconciliation")]
    public async Task<ActionResult<IEnumerable<ProjectMaterialReconciliationReportItemDto>>> GetMaterialReconciliationReport([FromQuery] int take = 200, [FromQuery] string? reconciliationStatus = null)
        => await ExecuteProjectReadAsync(() => _projectService.GetMaterialReconciliationReportAsync(take, reconciliationStatus), "Error loading material reconciliation report");

    [HttpGet("reports/procurement-reconciliation")]
    public async Task<ActionResult<IEnumerable<ProjectProcurementReconciliationReportItemDto>>> GetProcurementReconciliationReport([FromQuery] int take = 200, [FromQuery] string? reconciliationStatus = null)
        => await ExecuteProjectReadAsync(() => _projectService.GetProcurementReconciliationReportAsync(take, reconciliationStatus), "Error loading procurement reconciliation report");

    [HttpGet("reports/resource-capacity")]
    public async Task<ActionResult<IEnumerable<ProjectResourceCapacityReportItemDto>>> GetResourceCapacityReport([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] Guid? userId = null)
        => await ExecuteProjectReadAsync(() => _projectService.GetResourceCapacityReportAsync(startDate, endDate, userId), "Error loading resource capacity report");

    [HttpGet("reports/resource-capacity-recommendations")]
    public async Task<ActionResult<IEnumerable<ProjectResourceCapacityRecommendationDto>>> GetResourceCapacityRecommendations([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] Guid? userId = null)
        => await ExecuteProjectReadAsync(() => _projectService.GetResourceCapacityRecommendationsAsync(startDate, endDate, userId), "Error loading resource capacity recommendations");

    [HttpGet("reports/resource-optimization")]
    public async Task<ActionResult<IEnumerable<ProjectResourceOptimizationSuggestionDto>>> GetResourceOptimizationSuggestions([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        => await ExecuteProjectReadAsync(() => _projectService.GetResourceOptimizationSuggestionsAsync(startDate, endDate), "Error loading resource optimization suggestions");

    [HttpGet("reports/billing-summary")]
    public async Task<ActionResult<IEnumerable<ProjectBillingSummaryReportItemDto>>> GetBillingSummaryReport([FromQuery] int take = 200)
        => await ExecuteProjectReadAsync(() => _projectService.GetBillingSummaryReportAsync(take), "Error loading billing summary report");

    [HttpGet("reports/invoice-request-queue")]
    public async Task<ActionResult<IEnumerable<ProjectInvoiceRequestQueueItemDto>>> GetInvoiceRequestQueueReport([FromQuery] int take = 200, [FromQuery] string? status = null)
        => await ExecuteProjectReadAsync(() => _projectService.GetInvoiceRequestQueueReportAsync(take, status), "Error loading invoice request queue");

    [HttpGet("reports/workflow-approval-queue")]
    public async Task<ActionResult<IEnumerable<ProjectWorkflowApprovalQueueItemDto>>> GetWorkflowApprovalQueueReport([FromQuery] int take = 200, [FromQuery] string? entityType = null)
        => await ExecuteProjectReadAsync(() => _projectService.GetWorkflowApprovalQueueReportAsync(take, entityType), "Error loading workflow approval queue");

    [HttpGet("reports/external-collaboration")]
    public async Task<ActionResult<IEnumerable<ProjectExternalCollaborationReportItemDto>>> GetExternalCollaborationReport([FromQuery] int take = 200, [FromQuery] string? collaborationState = null)
        => await ExecuteProjectReadAsync(() => _projectService.GetExternalCollaborationReportAsync(take, collaborationState), "Error loading external collaboration report");

    [HttpGet("contracts/lookup")]
    public async Task<ActionResult<IEnumerable<ProjectContractLookupDto>>> GetContractLookup([FromQuery] Guid? businessPartnerId = null, [FromQuery] string? search = null)
        => Ok(await _projectService.GetContractLookupAsync(businessPartnerId, search));

    [HttpGet("contracts/{contractId:guid}/milestones")]
    public async Task<ActionResult<IEnumerable<ProjectContractMilestoneLookupDto>>> GetContractMilestones(Guid contractId)
    {
        try
        {
            return Ok(await _projectService.GetContractMilestonesAsync(contractId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDetailDto>> GetProject(Guid id)
    {
        var project = await _projectService.GetProjectByIdAsync(id);
        return project == null ? NotFound() : Ok(project);
    }

    [Authorize(Policy = "InternalOnly")]
    [HttpGet("{id:guid}/materials/requisitions")]
    public async Task<ActionResult<IEnumerable<InventoryRequisitionDto>>> GetProjectMaterialRequisitions(Guid id)
    {
        var project = await _projectService.GetProjectByIdAsync(id);
        if (project == null)
        {
            return NotFound();
        }

        return Ok(await _inventoryRequisitionService.GetByProjectAsync(id));
    }

    [Authorize(Policy = "InternalOnly")]
    [HttpGet("{id:guid}/financial-control-summary")]
    public async Task<ActionResult<ProjectFinancialControlSummaryDto>> GetFinancialControlSummary(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetFinancialControlSummaryAsync(id), "Error loading financial control summary");

    [Authorize(Policy = "InternalOnly")]
    [HttpGet("{id:guid}/integration-summary")]
    public async Task<ActionResult<ProjectIntegrationSummaryDto>> GetIntegrationSummary(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetIntegrationSummaryAsync(id), "Error loading integration summary");

    [Authorize(Policy = "InternalOnly")]
    [HttpGet("{id:guid}/governance-summary")]
    public async Task<ActionResult<ProjectGovernanceSummaryDto>> GetGovernanceSummary(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetGovernanceSummaryAsync(id), "Error loading governance summary");

    [HttpPost]
    public async Task<ActionResult<ProjectDetailDto>> CreateProject([FromBody] CreateProjectDto dto)
    {
        try
        {
            var project = await _projectService.CreateProjectAsync(dto);
            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDetailDto>> UpdateProject(Guid id, [FromBody] UpdateProjectDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectAsync(id, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitProject(Guid id)
    {
        await _projectService.SubmitProjectForApprovalAsync(id, _currentUserProvider.UserId);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveProject(Guid id, [FromBody] ApproveProjectRequest? request = null)
    {
        await _projectService.ApproveProjectAsync(id, _currentUserProvider.UserId, request?.Comments);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectProject(Guid id, [FromBody] RejectProjectRequest request)
    {
        await _projectService.RejectProjectAsync(id, _currentUserProvider.UserId, request.Reason, request.Comments);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/members")]
    public async Task<ActionResult<IEnumerable<ProjectMemberDto>>> GetMembers(Guid projectId) => Ok(await _projectService.GetMembersAsync(projectId));

    [HttpPost("{projectId:guid}/members")]
    public async Task<ActionResult<ProjectMemberDto>> AddMember(Guid projectId, [FromBody] AddProjectMemberDto dto) => Ok(await _projectService.AddMemberAsync(projectId, dto));

    [HttpDelete("members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid memberId)
    {
        await _projectService.RemoveMemberAsync(memberId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/work-items")]
    public async Task<ActionResult<IEnumerable<ProjectWorkItemDto>>> GetWorkItems(Guid projectId) => Ok(await _projectService.GetWorkItemsAsync(projectId));

    [HttpPost("{projectId:guid}/work-items")]
    public async Task<ActionResult<ProjectWorkItemDto>> AddWorkItem(Guid projectId, [FromBody] CreateProjectWorkItemDto dto)
    {
        try
        {
            return Ok(await _projectService.AddWorkItemAsync(projectId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("work-items/{workItemId:guid}")]
    public async Task<ActionResult<ProjectWorkItemDto>> UpdateWorkItem(Guid workItemId, [FromBody] CreateProjectWorkItemDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateWorkItemAsync(workItemId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{projectId:guid}/work-items/reorder")]
    public async Task<IActionResult> ReorderWorkItems(Guid projectId, [FromBody] ReorderProjectWorkItemsDto dto)
    {
        await _projectService.ReorderWorkItemsAsync(projectId, dto);
        return NoContent();
    }

    [HttpDelete("work-items/{workItemId:guid}")]
    public async Task<IActionResult> DeleteWorkItem(Guid workItemId)
    {
        await _projectService.DeleteWorkItemAsync(workItemId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/milestones")]
    public async Task<ActionResult<IEnumerable<ProjectMilestoneDto>>> GetMilestones(Guid projectId) => Ok(await _projectService.GetMilestonesAsync(projectId));

    [HttpPost("{projectId:guid}/milestones")]
    public async Task<ActionResult<ProjectMilestoneDto>> AddMilestone(Guid projectId, [FromBody] CreateProjectMilestoneDto dto) => Ok(await _projectService.AddMilestoneAsync(projectId, dto));

    [HttpPut("milestones/{milestoneId:guid}")]
    public async Task<ActionResult<ProjectMilestoneDto>> UpdateMilestone(Guid milestoneId, [FromBody] CreateProjectMilestoneDto dto) => Ok(await _projectService.UpdateMilestoneAsync(milestoneId, dto));

    [HttpDelete("milestones/{milestoneId:guid}")]
    public async Task<IActionResult> DeleteMilestone(Guid milestoneId)
    {
        await _projectService.DeleteMilestoneAsync(milestoneId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/resource-allocations")]
    public async Task<ActionResult<IEnumerable<ProjectResourceAllocationDto>>> GetResourceAllocations(Guid projectId) => Ok(await _projectService.GetResourceAllocationsAsync(projectId));

    [HttpPost("{projectId:guid}/resource-allocations")]
    public async Task<ActionResult<ProjectResourceAllocationDto>> AddResourceAllocation(Guid projectId, [FromBody] CreateProjectResourceAllocationDto dto) => Ok(await _projectService.AddResourceAllocationAsync(projectId, dto));

    [HttpPut("resource-allocations/{allocationId:guid}")]
    public async Task<ActionResult<ProjectResourceAllocationDto>> UpdateResourceAllocation(Guid allocationId, [FromBody] CreateProjectResourceAllocationDto dto) => Ok(await _projectService.UpdateResourceAllocationAsync(allocationId, dto));

    [HttpPost("resource-allocations/{allocationId:guid}/approve")]
    public async Task<ActionResult<ProjectResourceAllocationDto>> ApproveResourceAllocation(Guid allocationId) => Ok(await _projectService.ApproveResourceAllocationAsync(allocationId));

    [HttpPost("resource-allocations/{allocationId:guid}/substitute")]
    public async Task<ActionResult<ProjectResourceSubstitutionResultDto>> SubstituteResourceAllocation(Guid allocationId, [FromBody] SubstituteProjectResourceAllocationDto dto) => Ok(await _projectService.SubstituteResourceAllocationAsync(allocationId, dto));

    [HttpDelete("resource-allocations/{allocationId:guid}")]
    public async Task<IActionResult> DeleteResourceAllocation(Guid allocationId)
    {
        await _projectService.DeleteResourceAllocationAsync(allocationId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/risks")]
    public async Task<ActionResult<IEnumerable<ProjectRiskDto>>> GetRisks(Guid projectId) => Ok(await _projectService.GetRisksAsync(projectId));

    [HttpPost("{projectId:guid}/risks")]
    public async Task<ActionResult<ProjectRiskDto>> AddRisk(Guid projectId, [FromBody] CreateProjectRiskDto dto) => Ok(await _projectService.AddRiskAsync(projectId, dto));

    [HttpPut("risks/{riskId:guid}")]
    public async Task<ActionResult<ProjectRiskDto>> UpdateRisk(Guid riskId, [FromBody] CreateProjectRiskDto dto) => Ok(await _projectService.UpdateRiskAsync(riskId, dto));

    [HttpDelete("risks/{riskId:guid}")]
    public async Task<IActionResult> DeleteRisk(Guid riskId)
    {
        await _projectService.DeleteRiskAsync(riskId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/issues")]
    public async Task<ActionResult<IEnumerable<ProjectIssueDto>>> GetIssues(Guid projectId) => Ok(await _projectService.GetIssuesAsync(projectId));

    [HttpPost("{projectId:guid}/issues")]
    public async Task<ActionResult<ProjectIssueDto>> AddIssue(Guid projectId, [FromBody] CreateProjectIssueDto dto) => Ok(await _projectService.AddIssueAsync(projectId, dto));

    [HttpPut("issues/{issueId:guid}")]
    public async Task<ActionResult<ProjectIssueDto>> UpdateIssue(Guid issueId, [FromBody] CreateProjectIssueDto dto) => Ok(await _projectService.UpdateIssueAsync(issueId, dto));

    [HttpDelete("issues/{issueId:guid}")]
    public async Task<IActionResult> DeleteIssue(Guid issueId)
    {
        await _projectService.DeleteIssueAsync(issueId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/quality-checkpoints")]
    public async Task<ActionResult<IEnumerable<ProjectQualityCheckpointDto>>> GetQualityCheckpoints(Guid projectId) => Ok(await _projectService.GetQualityCheckpointsAsync(projectId));

    [HttpPost("{projectId:guid}/quality-checkpoints")]
    public async Task<ActionResult<ProjectQualityCheckpointDto>> AddQualityCheckpoint(Guid projectId, [FromBody] CreateProjectQualityCheckpointDto dto) => Ok(await _projectService.AddQualityCheckpointAsync(projectId, dto));

    [HttpPost("quality-checkpoints/{checkpointId:guid}/sign-off")]
    public async Task<ActionResult<ProjectQualityCheckpointDto>> SignOffQualityCheckpoint(Guid checkpointId, [FromBody] ApproveProjectRequest? request = null) => Ok(await _projectService.SignOffQualityCheckpointAsync(checkpointId, request?.Comments));

    [HttpDelete("quality-checkpoints/{checkpointId:guid}")]
    public async Task<IActionResult> DeleteQualityCheckpoint(Guid checkpointId)
    {
        await _projectService.DeleteQualityCheckpointAsync(checkpointId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/non-conformances")]
    public async Task<ActionResult<IEnumerable<ProjectNonConformanceDto>>> GetNonConformances(Guid projectId) => Ok(await _projectService.GetNonConformancesAsync(projectId));

    [HttpPost("{projectId:guid}/non-conformances")]
    public async Task<ActionResult<ProjectNonConformanceDto>> AddNonConformance(Guid projectId, [FromBody] CreateProjectNonConformanceDto dto) => Ok(await _projectService.AddNonConformanceAsync(projectId, dto));

    [HttpPost("non-conformances/{nonConformanceId:guid}/resolve")]
    public async Task<ActionResult<ProjectNonConformanceDto>> ResolveNonConformance(Guid nonConformanceId, [FromBody] ApproveProjectRequest? request = null) => Ok(await _projectService.ResolveNonConformanceAsync(nonConformanceId, request?.Comments));

    [HttpDelete("non-conformances/{nonConformanceId:guid}")]
    public async Task<IActionResult> DeleteNonConformance(Guid nonConformanceId)
    {
        await _projectService.DeleteNonConformanceAsync(nonConformanceId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/changes")]
    public async Task<ActionResult<IEnumerable<ProjectChangeRequestDto>>> GetChanges(Guid projectId) => Ok(await _projectService.GetChangeRequestsAsync(projectId));

    [HttpPost("{projectId:guid}/changes")]
    public async Task<ActionResult<ProjectChangeRequestDto>> AddChange(Guid projectId, [FromBody] CreateProjectChangeRequestDto dto) => Ok(await _projectService.AddChangeRequestAsync(projectId, dto));

    [HttpPut("changes/{changeId:guid}")]
    public async Task<ActionResult<ProjectChangeRequestDto>> UpdateChange(Guid changeId, [FromBody] CreateProjectChangeRequestDto dto) => Ok(await _projectService.UpdateChangeRequestAsync(changeId, dto));

    [HttpDelete("changes/{changeId:guid}")]
    public async Task<IActionResult> DeleteChange(Guid changeId)
    {
        await _projectService.DeleteChangeRequestAsync(changeId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/billing-schedules")]
    public async Task<ActionResult<IEnumerable<ProjectBillingScheduleDto>>> GetBillingSchedules(Guid projectId) => Ok(await _projectService.GetBillingSchedulesAsync(projectId));

    [HttpPost("{projectId:guid}/billing-schedules")]
    public async Task<ActionResult<ProjectBillingScheduleDto>> AddBillingSchedule(Guid projectId, [FromBody] CreateProjectBillingScheduleDto dto) => Ok(await _projectService.AddBillingScheduleAsync(projectId, dto));

    [HttpPut("billing-schedules/{billingScheduleId:guid}")]
    public async Task<ActionResult<ProjectBillingScheduleDto>> UpdateBillingSchedule(Guid billingScheduleId, [FromBody] CreateProjectBillingScheduleDto dto) => Ok(await _projectService.UpdateBillingScheduleAsync(billingScheduleId, dto));

    [HttpDelete("billing-schedules/{billingScheduleId:guid}")]
    public async Task<IActionResult> DeleteBillingSchedule(Guid billingScheduleId)
    {
        await _projectService.DeleteBillingScheduleAsync(billingScheduleId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/invoice-requests")]
    public async Task<ActionResult<IEnumerable<ProjectInvoiceRequestDto>>> GetInvoiceRequests(Guid projectId) => Ok(await _projectService.GetInvoiceRequestsAsync(projectId));

    [HttpPost("{projectId:guid}/invoice-requests")]
    public async Task<ActionResult<ProjectInvoiceRequestDto>> CreateInvoiceRequest(Guid projectId, [FromBody] CreateProjectInvoiceRequestDto dto) => Ok(await _projectService.CreateInvoiceRequestAsync(projectId, dto));

    [HttpPost("billing-schedules/{billingScheduleId:guid}/generate-invoice-request")]
    public async Task<ActionResult<ProjectInvoiceRequestDto>> GenerateInvoiceRequest(Guid billingScheduleId, [FromBody] GenerateProjectInvoiceRequestRequest? request = null)
        => Ok(await _projectService.GenerateInvoiceRequestFromScheduleAsync(billingScheduleId, request?.Notes));

    [HttpPost("invoice-requests/{invoiceRequestId:guid}/submit")]
    public async Task<ActionResult<ProjectInvoiceRequestDto>> SubmitInvoiceRequest(Guid invoiceRequestId, [FromBody] ApproveProjectRequest? request = null)
        => Ok(await _projectService.SubmitInvoiceRequestAsync(invoiceRequestId, request?.Comments));

    [HttpPost("invoice-requests/{invoiceRequestId:guid}/send-to-finance")]
    public async Task<ActionResult<ProjectInvoiceRequestDto>> SendInvoiceRequestToFinance(Guid invoiceRequestId, [FromBody] UpdateProjectInvoiceRequestWorkflowDto? request = null)
        => Ok(await _projectService.MarkInvoiceRequestSentToFinanceAsync(invoiceRequestId, request?.ExternalReference, request?.Comments));

    [HttpPost("invoice-requests/{invoiceRequestId:guid}/mark-invoiced")]
    public async Task<ActionResult<ProjectInvoiceRequestDto>> MarkInvoiceRequestInvoiced(Guid invoiceRequestId, [FromBody] UpdateProjectInvoiceRequestWorkflowDto? request = null)
        => Ok(await _projectService.MarkInvoiceRequestInvoicedAsync(invoiceRequestId, request?.ExternalReference, request?.Comments));

    [HttpPost("invoice-requests/{invoiceRequestId:guid}/mark-paid")]
    public async Task<ActionResult<ProjectInvoiceRequestDto>> MarkInvoiceRequestPaid(Guid invoiceRequestId, [FromBody] UpdateProjectInvoiceRequestWorkflowDto? request = null)
        => Ok(await _projectService.MarkInvoiceRequestPaidAsync(invoiceRequestId, request?.Comments));

    [HttpGet("{projectId:guid}/deliverables")]
    public async Task<ActionResult<IEnumerable<ProjectDeliverableDto>>> GetDeliverables(Guid projectId) => Ok(await _projectService.GetDeliverablesAsync(projectId));

    [HttpPost("{projectId:guid}/deliverables")]
    public async Task<ActionResult<ProjectDeliverableDto>> AddDeliverable(Guid projectId, [FromBody] CreateProjectDeliverableDto dto) => Ok(await _projectService.AddDeliverableAsync(projectId, dto));

    [HttpPut("deliverables/{deliverableId:guid}")]
    public async Task<ActionResult<ProjectDeliverableDto>> UpdateDeliverable(Guid deliverableId, [FromBody] CreateProjectDeliverableDto dto) => Ok(await _projectService.UpdateDeliverableAsync(deliverableId, dto));

    [HttpPost("deliverables/{deliverableId:guid}/submit")]
    public async Task<ActionResult<ProjectDeliverableDto>> SubmitDeliverable(Guid deliverableId, [FromBody] SubmitProjectDeliverableDto dto) => Ok(await _projectService.SubmitDeliverableAsync(deliverableId, dto));

    [HttpPost("deliverables/{deliverableId:guid}/approve")]
    public async Task<ActionResult<ProjectDeliverableDto>> ApproveDeliverable(Guid deliverableId, [FromBody] ApproveProjectRequest? request = null) => Ok(await _projectService.ApproveDeliverableAsync(deliverableId, request?.Comments));

    [HttpPost("deliverables/{deliverableId:guid}/reject")]
    public async Task<ActionResult<ProjectDeliverableDto>> RejectDeliverable(Guid deliverableId, [FromBody] ApproveProjectRequest? request = null) => Ok(await _projectService.RejectDeliverableAsync(deliverableId, request?.Comments));

    [HttpGet("{projectId:guid}/task-dependencies")]
    public async Task<ActionResult<IEnumerable<ProjectTaskDependencyDto>>> GetTaskDependencies(Guid projectId) => Ok(await _projectService.GetTaskDependenciesAsync(projectId));

    [HttpPost("{projectId:guid}/task-dependencies")]
    public async Task<ActionResult<ProjectTaskDependencyDto>> AddTaskDependency(Guid projectId, [FromBody] CreateProjectTaskDependencyDto dto)
    {
        try
        {
            return Ok(await _projectService.AddTaskDependencyAsync(projectId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("task-dependencies/{dependencyId:guid}")]
    public async Task<IActionResult> DeleteTaskDependency(Guid dependencyId)
    {
        await _projectService.DeleteTaskDependencyAsync(dependencyId);
        return NoContent();
    }

    [HttpGet("interdependencies")]
    public async Task<ActionResult<IEnumerable<ProjectInterdependencyDto>>> GetInterdependencies([FromQuery] Guid? projectId = null, [FromQuery] Guid? portfolioId = null, [FromQuery] Guid? programId = null)
        => Ok(await _projectService.GetInterdependenciesAsync(projectId, portfolioId, programId));

    [HttpPost("interdependencies")]
    public async Task<ActionResult<ProjectInterdependencyDto>> CreateInterdependency([FromBody] CreateProjectInterdependencyDto dto)
    {
        try
        {
            return Ok(await _projectService.CreateInterdependencyAsync(dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("interdependencies/{id:guid}")]
    public async Task<ActionResult<ProjectInterdependencyDto>> UpdateInterdependency(Guid id, [FromBody] CreateProjectInterdependencyDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateInterdependencyAsync(id, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("interdependencies/{id:guid}")]
    public async Task<IActionResult> DeleteInterdependency(Guid id)
    {
        await _projectService.DeleteInterdependencyAsync(id);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/baselines")]
    public async Task<ActionResult<IEnumerable<ProjectBaselineDto>>> GetBaselines(Guid projectId) => Ok(await _projectService.GetBaselinesAsync(projectId));

    [HttpPost("{projectId:guid}/baselines")]
    public async Task<ActionResult<ProjectBaselineDto>> CreateBaseline(Guid projectId, [FromBody] CreateProjectBaselineDto dto) => Ok(await _projectService.CreateBaselineAsync(projectId, dto));

    [HttpGet("baselines/{baselineId:guid}/compare")]
    public async Task<ActionResult<ProjectBaselineComparisonDto>> CompareBaseline(Guid baselineId) => Ok(await _projectService.CompareBaselineAsync(baselineId));

    [HttpGet("{projectId:guid}/schedule-analysis")]
    public async Task<ActionResult<ProjectScheduleAnalysisDto>> AnalyzeSchedule(Guid projectId) => Ok(await _projectService.AnalyzeScheduleAsync(projectId));

    [HttpPost("{projectId:guid}/schedule-analysis/recalculate")]
    public async Task<ActionResult<ProjectScheduleAnalysisDto>> RecalculateSchedule(Guid projectId)
    {
        try
        {
            return Ok(await _projectService.RecalculateScheduleAsync(projectId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{projectId:guid}/timesheets")]
    public async Task<ActionResult<IEnumerable<ProjectTimesheetEntryDto>>> GetTimesheets(Guid projectId) => Ok(await _projectService.GetTimesheetEntriesAsync(projectId));

    [HttpGet("my/timesheets")]
    public async Task<ActionResult<IEnumerable<ProjectTimesheetEntryDto>>> GetMyTimesheets([FromQuery] string? status = null) => Ok(await _projectService.GetMyTimesheetEntriesAsync(status));

    [HttpGet("timesheets/approval-summary")]
    public async Task<ActionResult<ProjectApprovalQueueSummaryDto>> GetTimesheetApprovalSummary([FromQuery] Guid? projectId = null) => Ok(await _projectService.GetTimesheetApprovalSummaryAsync(projectId));

    [HttpGet("timesheets/approval-queue")]
    public async Task<ActionResult<IEnumerable<ProjectTimesheetApprovalQueueItemDto>>> GetTimesheetApprovalQueue([FromQuery] Guid? projectId = null, [FromQuery] string? status = null, [FromQuery] Guid? userId = null, [FromQuery] int take = 200)
        => Ok(await _projectService.GetTimesheetApprovalQueueAsync(projectId, status, userId, take));

    [HttpPost("{projectId:guid}/timesheets")]
    public async Task<ActionResult<ProjectTimesheetEntryDto>> AddTimesheet(Guid projectId, [FromBody] CreateProjectTimesheetEntryDto dto)
    {
        try
        {
            return Ok(await _projectService.AddTimesheetEntryAsync(projectId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("timesheets/{entryId:guid}")]
    public async Task<ActionResult<ProjectTimesheetEntryDto>> UpdateTimesheet(Guid entryId, [FromBody] CreateProjectTimesheetEntryDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateTimesheetEntryAsync(entryId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("timesheets/{entryId:guid}/submit")]
    public async Task<ActionResult<ProjectTimesheetEntryDto>> SubmitTimesheet(Guid entryId)
    {
        try
        {
            return Ok(await _projectService.SubmitTimesheetEntryAsync(entryId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("timesheets/{entryId:guid}/approve")]
    public async Task<ActionResult<ProjectTimesheetEntryDto>> ApproveTimesheet(Guid entryId, [FromBody] ApproveProjectRequest? request = null)
    {
        try
        {
            return Ok(await _projectService.ApproveTimesheetEntryAsync(entryId, request?.Comments));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("timesheets/{entryId:guid}/reject")]
    public async Task<ActionResult<ProjectTimesheetEntryDto>> RejectTimesheet(Guid entryId, [FromBody] ApproveProjectRequest? request = null)
    {
        try
        {
            return Ok(await _projectService.RejectTimesheetEntryAsync(entryId, request?.Comments));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("timesheets/{entryId:guid}")]
    public async Task<IActionResult> DeleteTimesheet(Guid entryId)
    {
        await _projectService.DeleteTimesheetEntryAsync(entryId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/expenses")]
    public async Task<ActionResult<IEnumerable<ProjectExpenseDto>>> GetExpenses(Guid projectId) => Ok(await _projectService.GetExpensesAsync(projectId));

    [HttpGet("my/expenses")]
    public async Task<ActionResult<IEnumerable<ProjectExpenseDto>>> GetMyExpenses([FromQuery] string? status = null) => Ok(await _projectService.GetMyExpensesAsync(status));

    [HttpGet("expenses/approval-summary")]
    public async Task<ActionResult<ProjectApprovalQueueSummaryDto>> GetExpenseApprovalSummary([FromQuery] Guid? projectId = null) => Ok(await _projectService.GetExpenseApprovalSummaryAsync(projectId));

    [HttpGet("expenses/approval-queue")]
    public async Task<ActionResult<IEnumerable<ProjectExpenseApprovalQueueItemDto>>> GetExpenseApprovalQueue([FromQuery] Guid? projectId = null, [FromQuery] string? status = null, [FromQuery] Guid? userId = null, [FromQuery] int take = 200)
        => Ok(await _projectService.GetExpenseApprovalQueueAsync(projectId, status, userId, take));

    [HttpPost("{projectId:guid}/expenses")]
    public async Task<ActionResult<ProjectExpenseDto>> AddExpense(Guid projectId, [FromBody] CreateProjectExpenseDto dto)
    {
        try
        {
            return Ok(await _projectService.AddExpenseAsync(projectId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("expenses/{expenseId:guid}")]
    public async Task<ActionResult<ProjectExpenseDto>> UpdateExpense(Guid expenseId, [FromBody] CreateProjectExpenseDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateExpenseAsync(expenseId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("expenses/{expenseId:guid}/submit")]
    public async Task<ActionResult<ProjectExpenseDto>> SubmitExpense(Guid expenseId)
    {
        try
        {
            return Ok(await _projectService.SubmitExpenseAsync(expenseId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("expenses/{expenseId:guid}/approve")]
    public async Task<ActionResult<ProjectExpenseDto>> ApproveExpense(Guid expenseId, [FromBody] ApproveProjectRequest? request = null)
    {
        try
        {
            return Ok(await _projectService.ApproveExpenseAsync(expenseId, request?.Comments));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("expenses/{expenseId:guid}/reject")]
    public async Task<ActionResult<ProjectExpenseDto>> RejectExpense(Guid expenseId, [FromBody] ApproveProjectRequest? request = null)
    {
        try
        {
            return Ok(await _projectService.RejectExpenseAsync(expenseId, request?.Comments));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("expenses/{expenseId:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid expenseId)
    {
        await _projectService.DeleteExpenseAsync(expenseId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/revenue-recognition")]
    public async Task<ActionResult<IEnumerable<ProjectRevenueRecognitionDto>>> GetRevenueRecognition(Guid projectId) => Ok(await _projectService.GetRevenueRecognitionsAsync(projectId));

    [HttpPost("{projectId:guid}/revenue-recognition/generate")]
    public async Task<ActionResult<IEnumerable<ProjectRevenueRecognitionDto>>> GenerateRevenueRecognition(Guid projectId) => Ok(await _projectService.GenerateRevenueRecognitionAsync(projectId));

    [HttpGet("{projectId:guid}/budget-revisions")]
    public async Task<ActionResult<IEnumerable<ProjectBudgetRevisionDto>>> GetBudgetRevisions(Guid projectId) => Ok(await _projectService.GetBudgetRevisionsAsync(projectId));

    [HttpPost("{projectId:guid}/budget-revisions")]
    public async Task<ActionResult<ProjectBudgetRevisionDto>> CreateBudgetRevision(Guid projectId, [FromBody] CreateProjectBudgetRevisionDto dto)
        => Ok(await _projectService.CreateBudgetRevisionAsync(projectId, dto));

    [HttpPost("budget-revisions/{revisionId:guid}/submit")]
    public async Task<ActionResult<ProjectBudgetRevisionDto>> SubmitBudgetRevision(Guid revisionId)
        => Ok(await _projectService.SubmitBudgetRevisionAsync(revisionId, _currentUserProvider.UserId));

    [HttpPost("budget-revisions/{revisionId:guid}/approve")]
    public async Task<ActionResult<ProjectBudgetRevisionDto>> ApproveBudgetRevision(Guid revisionId, [FromBody] ApproveProjectRequest? request = null)
        => Ok(await _projectService.ApproveBudgetRevisionAsync(revisionId, _currentUserProvider.UserId, request?.Comments));

    [HttpPost("budget-revisions/{revisionId:guid}/reject")]
    public async Task<ActionResult<ProjectBudgetRevisionDto>> RejectBudgetRevision(Guid revisionId, [FromBody] RejectProjectRequest request)
        => Ok(await _projectService.RejectBudgetRevisionAsync(revisionId, _currentUserProvider.UserId, request.Reason, request.Comments));

    [HttpGet("{projectId:guid}/forecast-versions")]
    public async Task<ActionResult<IEnumerable<ProjectForecastVersionDto>>> GetForecastVersions(Guid projectId) => Ok(await _projectService.GetForecastVersionsAsync(projectId));

    [HttpPost("{projectId:guid}/forecast-versions")]
    public async Task<ActionResult<ProjectForecastVersionDto>> CreateForecastVersion(Guid projectId, [FromBody] CreateProjectForecastVersionDto dto)
        => Ok(await _projectService.CreateForecastVersionAsync(projectId, dto));

    [HttpPost("forecast-versions/{forecastVersionId:guid}/activate")]
    public async Task<ActionResult<ProjectForecastVersionDto>> ActivateForecastVersion(Guid forecastVersionId)
        => Ok(await _projectService.SetActiveForecastVersionAsync(forecastVersionId));

    [HttpGet("{projectId:guid}/asset-links")]
    public async Task<ActionResult<IEnumerable<ProjectAssetLinkDto>>> GetAssetLinks(Guid projectId) => Ok(await _projectService.GetAssetLinksAsync(projectId));

    [HttpPost("{projectId:guid}/asset-links")]
    public async Task<ActionResult<ProjectAssetLinkDto>> AddAssetLink(Guid projectId, [FromBody] CreateProjectAssetLinkDto dto) => Ok(await _projectService.AddAssetLinkAsync(projectId, dto));

    [HttpGet("{projectId:guid}/external-access-policies")]
    public async Task<ActionResult<IEnumerable<ProjectExternalAccessPolicyDto>>> GetExternalAccessPolicies(Guid projectId) => Ok(await _projectService.GetExternalAccessPoliciesAsync(projectId));

    [HttpPost("{projectId:guid}/external-access-policies")]
    public async Task<ActionResult<ProjectExternalAccessPolicyDto>> UpsertExternalAccessPolicy(Guid projectId, [FromBody] CreateProjectExternalAccessPolicyDto dto) => Ok(await _projectService.UpsertExternalAccessPolicyAsync(projectId, dto));

    [HttpDelete("external-access-policies/{policyId:guid}")]
    public async Task<IActionResult> DeleteExternalAccessPolicy(Guid policyId)
    {
        await _projectService.DeleteExternalAccessPolicyAsync(policyId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/decisions")]
    public async Task<ActionResult<IEnumerable<ProjectDecisionDto>>> GetDecisions(Guid projectId) => Ok(await _projectService.GetDecisionsAsync(projectId));

    [HttpPost("{projectId:guid}/decisions")]
    public async Task<ActionResult<ProjectDecisionDto>> AddDecision(Guid projectId, [FromBody] CreateProjectDecisionDto dto) => Ok(await _projectService.AddDecisionAsync(projectId, dto));

    [HttpPut("decisions/{decisionId:guid}")]
    public async Task<ActionResult<ProjectDecisionDto>> UpdateDecision(Guid decisionId, [FromBody] CreateProjectDecisionDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateDecisionAsync(decisionId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("decisions/{decisionId:guid}")]
    public async Task<IActionResult> DeleteDecision(Guid decisionId)
    {
        await _projectService.DeleteDecisionAsync(decisionId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/meetings")]
    public async Task<ActionResult<IEnumerable<ProjectMeetingMinuteDto>>> GetMeetings(Guid projectId) => Ok(await _projectService.GetMeetingsAsync(projectId));

    [HttpPost("{projectId:guid}/meetings")]
    public async Task<ActionResult<ProjectMeetingMinuteDto>> AddMeeting(Guid projectId, [FromBody] CreateProjectMeetingMinuteDto dto) => Ok(await _projectService.AddMeetingAsync(projectId, dto));

    [HttpPut("meetings/{meetingId:guid}")]
    public async Task<ActionResult<ProjectMeetingMinuteDto>> UpdateMeeting(Guid meetingId, [FromBody] CreateProjectMeetingMinuteDto dto) => Ok(await _projectService.UpdateMeetingAsync(meetingId, dto));

    [HttpDelete("meetings/{meetingId:guid}")]
    public async Task<IActionResult> DeleteMeeting(Guid meetingId)
    {
        await _projectService.DeleteMeetingAsync(meetingId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/action-items")]
    public async Task<ActionResult<IEnumerable<ProjectActionItemDto>>> GetActionItems(Guid projectId) => Ok(await _projectService.GetActionItemsAsync(projectId));

    [HttpPost("{projectId:guid}/action-items")]
    public async Task<ActionResult<ProjectActionItemDto>> AddActionItem(Guid projectId, [FromBody] CreateProjectActionItemDto dto)
    {
        try
        {
            return Ok(await _projectService.AddActionItemAsync(projectId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("action-items/{actionItemId:guid}")]
    public async Task<ActionResult<ProjectActionItemDto>> UpdateActionItem(Guid actionItemId, [FromBody] CreateProjectActionItemDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateActionItemAsync(actionItemId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("action-items/{actionItemId:guid}")]
    public async Task<IActionResult> DeleteActionItem(Guid actionItemId)
    {
        await _projectService.DeleteActionItemAsync(actionItemId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/lessons-learned")]
    public async Task<ActionResult<IEnumerable<ProjectLessonLearnedDto>>> GetLessonsLearned(Guid projectId) => Ok(await _projectService.GetLessonsLearnedAsync(projectId));

    [HttpPost("{projectId:guid}/lessons-learned")]
    public async Task<ActionResult<ProjectLessonLearnedDto>> AddLessonLearned(Guid projectId, [FromBody] CreateProjectLessonLearnedDto dto) => Ok(await _projectService.AddLessonLearnedAsync(projectId, dto));

    [HttpPut("lessons-learned/{lessonId:guid}")]
    public async Task<ActionResult<ProjectLessonLearnedDto>> UpdateLessonLearned(Guid lessonId, [FromBody] CreateProjectLessonLearnedDto dto) => Ok(await _projectService.UpdateLessonLearnedAsync(lessonId, dto));

    [HttpDelete("lessons-learned/{lessonId:guid}")]
    public async Task<IActionResult> DeleteLessonLearned(Guid lessonId)
    {
        await _projectService.DeleteLessonLearnedAsync(lessonId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/closure")]
    public async Task<ActionResult<ProjectClosureDto?>> GetClosure(Guid projectId) => Ok(await _projectService.GetClosureAsync(projectId));

    [HttpPut("{projectId:guid}/closure")]
    public async Task<ActionResult<ProjectClosureDto>> UpsertClosure(Guid projectId, [FromBody] UpsertProjectClosureDto dto)
    {
        try
        {
            return Ok(await _projectService.UpsertClosureAsync(projectId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{projectId:guid}/closure/submit")]
    public async Task<IActionResult> SubmitClosure(Guid projectId)
    {
        try
        {
            await _projectService.SubmitClosureForApprovalAsync(projectId, _currentUserProvider.UserId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("closure/{closureId:guid}/approve")]
    public async Task<IActionResult> ApproveClosure(Guid closureId, [FromBody] ApproveProjectRequest? request = null)
    {
        try
        {
            await _projectService.ApproveClosureAsync(closureId, _currentUserProvider.UserId, request?.Comments);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("closure/{closureId:guid}/reject")]
    public async Task<IActionResult> RejectClosure(Guid closureId, [FromBody] RejectProjectRequest request)
    {
        try
        {
            await _projectService.RejectClosureAsync(closureId, _currentUserProvider.UserId, request.Reason, request.Comments);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{projectId:guid}/ai-insights")]
    public async Task<ActionResult<IEnumerable<ProjectAiInsightDto>>> GetAiInsights(Guid projectId) => Ok(await _projectService.GetAiInsightsAsync(projectId));

    [HttpGet("{projectId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<ProjectDocumentDto>>> GetDocuments(Guid projectId) => Ok(await _projectService.GetDocumentsAsync(projectId));

    [HttpPost("{projectId:guid}/documents")]
    public async Task<ActionResult<ProjectDocumentDto>> AttachDocument(Guid projectId, [FromBody] AttachProjectDocumentDto dto) => Ok(await _projectService.AttachDocumentAsync(projectId, dto));

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _projectService.DeleteDocumentAsync(documentId);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/comments")]
    public async Task<ActionResult<IEnumerable<ProjectCommentDto>>> GetComments(Guid projectId) => Ok(await _projectService.GetCommentsAsync(projectId));

    [HttpPost("{projectId:guid}/comments")]
    public async Task<ActionResult<ProjectCommentDto>> AddComment(Guid projectId, [FromBody] CreateProjectCommentDto dto) => Ok(await _projectService.AddCommentAsync(projectId, dto));

    [HttpGet("external/my-projects")]
    public async Task<ActionResult<IEnumerable<ProjectExternalSummaryDto>>> GetExternalProjects()
    {
        try
        {
            return Ok(await _projectService.GetExternalProjectsAsync(_currentUserProvider.UserId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("external/my-projects/{projectId:guid}")]
    public async Task<ActionResult<ProjectExternalDetailDto>> GetExternalProject(Guid projectId)
    {
        try
        {
            var project = await _projectService.GetExternalProjectByIdAsync(projectId, _currentUserProvider.UserId);
            return project == null ? NotFound() : Ok(project);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("external/my-projects/{projectId:guid}/comments")]
    public async Task<ActionResult<ProjectCommentDto>> AddExternalComment(Guid projectId, [FromBody] CreateProjectCommentDto dto)
    {
        try
        {
            return Ok(await _projectService.AddExternalCommentAsync(projectId, dto, _currentUserProvider.UserId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("external/my-projects/{projectId:guid}/documents")]
    public async Task<ActionResult<ProjectDocumentDto>> AttachExternalDocument(Guid projectId, [FromBody] AttachProjectDocumentDto dto)
    {
        try
        {
            return Ok(await _projectService.AttachExternalDocumentAsync(projectId, dto, _currentUserProvider.UserId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("external/my-projects/{projectId:guid}/work-items/{workItemId:guid}/progress")]
    public async Task<ActionResult<ProjectWorkItemDto>> UpdateExternalWorkItemProgress(Guid projectId, Guid workItemId, [FromBody] UpdateProjectWorkItemProgressDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateExternalWorkItemProgressAsync(projectId, workItemId, dto, _currentUserProvider.UserId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("external/my-projects/{projectId:guid}/deliverables/{deliverableId:guid}/submit")]
    public async Task<ActionResult<ProjectDeliverableDto>> SubmitExternalDeliverable(Guid projectId, Guid deliverableId, [FromBody] SubmitProjectDeliverableDto dto)
    {
        try
        {
            return Ok(await _projectService.SubmitExternalDeliverableAsync(projectId, deliverableId, dto, _currentUserProvider.UserId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("external/my-projects/{projectId:guid}/deliverables/{deliverableId:guid}/approve")]
    public async Task<ActionResult<ProjectDeliverableDto>> ApproveExternalDeliverable(Guid projectId, Guid deliverableId, [FromBody] ApproveProjectRequest? request = null)
    {
        try
        {
            return Ok(await _projectService.ApproveExternalDeliverableAsync(projectId, deliverableId, request?.Comments, _currentUserProvider.UserId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

public class RejectProjectRequest
{
    public string Reason { get; set; } = string.Empty;
    public string? Comments { get; set; }
}

public class ApproveProjectRequest
{
    public string? Comments { get; set; }
}

public class GenerateProjectInvoiceRequestRequest
{
    public string? Notes { get; set; }
}
