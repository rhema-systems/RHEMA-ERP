using ErpSystem.Api.Services.Notifications;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
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
    private readonly INotificationService _notificationService;
    private readonly ApplicationDbContext _db;
    private readonly IQuantitySurveyVariationService _quantitySurveyVariations;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        IProjectService projectService,
        IInventoryRequisitionService inventoryRequisitionService,
        ICurrentUserProvider currentUserProvider,
        INotificationService notificationService,
        ApplicationDbContext db,
        IQuantitySurveyVariationService quantitySurveyVariations,
        ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _inventoryRequisitionService = inventoryRequisitionService;
        _currentUserProvider = currentUserProvider;
        _notificationService = notificationService;
        _db = db;
        _quantitySurveyVariations = quantitySurveyVariations;
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

    [HttpGet("reports/material-cost-ledger")]
    public async Task<ActionResult<IEnumerable<ProjectMaterialCostEntryDto>>> GetMaterialCostLedgerReport([FromQuery] Guid? projectId = null, [FromQuery] int take = 300, [FromQuery] string? sourceDocumentType = null, [FromQuery] string? postingState = null, [FromQuery] bool? isReversed = null, [FromQuery] bool? exceptionsOnly = null)
        => await ExecuteProjectReadAsync(() => _projectService.GetMaterialCostLedgerReportAsync(projectId, take, sourceDocumentType, postingState, isReversed, exceptionsOnly), "Error loading material cost ledger report");

    [HttpGet("reports/procurement-reconciliation")]
    public async Task<ActionResult<IEnumerable<ProjectProcurementReconciliationReportItemDto>>> GetProcurementReconciliationReport([FromQuery] int take = 200, [FromQuery] string? reconciliationStatus = null)
        => await ExecuteProjectReadAsync(() => _projectService.GetProcurementReconciliationReportAsync(take, reconciliationStatus), "Error loading procurement reconciliation report");

    [HttpGet("reports/phase-gate-readiness")]
    public async Task<ActionResult<IEnumerable<ProjectPhaseGateReadinessReportItemDto>>> GetPhaseGateReadinessReport([FromQuery] Guid? projectId = null, [FromQuery] int take = 250)
        => await ExecuteProjectReadAsync(() => _projectService.GetPhaseGateReadinessReportAsync(projectId, take), "Error loading phase gate readiness report");

    [HttpGet("reports/approval-watch")]
    public async Task<ActionResult<IEnumerable<ProjectApprovalWatchReportItemDto>>> GetApprovalWatchReport([FromQuery] Guid? projectId = null, [FromQuery] int take = 250)
        => await ExecuteProjectReadAsync(() => _projectService.GetApprovalWatchReportAsync(projectId, take), "Error loading approval watch report");

    [HttpGet("reports/construction-commercial")]
    public async Task<ActionResult<IEnumerable<ProjectCommercialAdministrationReportItemDto>>> GetConstructionCommercialReport([FromQuery] Guid? projectId = null, [FromQuery] int take = 200)
        => await ExecuteProjectReadAsync(() => _projectService.GetCommercialAdministrationReportAsync(projectId, take), "Error loading construction commercial report");

    [HttpGet("reports/post-handover-watch")]
    public async Task<ActionResult<IEnumerable<ProjectPostHandoverWatchReportItemDto>>> GetPostHandoverWatchReport([FromQuery] Guid? projectId = null, [FromQuery] int take = 200)
        => await ExecuteProjectReadAsync(() => _projectService.GetPostHandoverWatchReportAsync(projectId, take), "Error loading post-handover watch report");

    [HttpGet("reports/design-control-watch")]
    public async Task<ActionResult<IEnumerable<ProjectDesignControlReportItemDto>>> GetDesignControlWatchReport([FromQuery] Guid? projectId = null, [FromQuery] int take = 250)
        => await ExecuteProjectReadAsync(() => _projectService.GetDesignControlWatchReportAsync(projectId, take), "Error loading design control watch report");

    [HttpGet("reports/site-controls-watch")]
    public async Task<ActionResult<IEnumerable<ProjectSiteControlReportItemDto>>> GetSiteControlsWatchReport([FromQuery] Guid? projectId = null, [FromQuery] int take = 250)
        => await ExecuteProjectReadAsync(() => _projectService.GetSiteControlsWatchReportAsync(projectId, take), "Error loading site controls watch report");

    [HttpGet("reports/unit-commercialization-watch")]
    public async Task<ActionResult<IEnumerable<ProjectUnitCommercializationReportItemDto>>> GetUnitCommercializationWatchReport([FromQuery] Guid? projectId = null, [FromQuery] int take = 250)
        => await ExecuteProjectReadAsync(() => _projectService.GetUnitCommercializationWatchReportAsync(projectId, take), "Error loading unit commercialization watch report");

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

    [HttpGet("tenders/lookup")]
    public async Task<ActionResult<IEnumerable<ProjectTenderLookupDto>>> GetTenderLookup([FromQuery] string? search = null)
        => Ok(await _projectService.GetTenderLookupAsync(search));

    [HttpGet("{id:guid}/procurement-plan-items/lookup")]
    public async Task<ActionResult<IEnumerable<ProjectProcurementPlanItemLookupDto>>> GetProcurementPlanItemLookup(Guid id, [FromQuery] string? search = null)
        => Ok(await _projectService.GetProcurementPlanItemLookupAsync(id, search));

    [HttpGet("{id:guid}/purchase-requisitions/lookup")]
    public async Task<ActionResult<IEnumerable<ProjectPurchaseRequisitionLookupDto>>> GetPurchaseRequisitionLookup(Guid id, [FromQuery] string? search = null)
        => Ok(await _projectService.GetPurchaseRequisitionLookupAsync(id, search));

    [HttpGet("{id:guid}/purchase-orders/lookup")]
    public async Task<ActionResult<IEnumerable<ProjectPurchaseOrderLookupDto>>> GetPurchaseOrderLookup(Guid id, [FromQuery] string? search = null)
        => Ok(await _projectService.GetPurchaseOrderLookupAsync(id, search));

    [HttpGet("{id:guid}/link-options")]
    public async Task<ActionResult<ProjectLinkOptionsDto>> GetProjectLinkOptions(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectLinkOptionsAsync(id), "Error loading project link options");

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDetailDto>> GetProject(Guid id)
    {
        var project = await _projectService.GetProjectByIdAsync(id);
        return project == null ? NotFound() : Ok(project);
    }

    [HttpGet("{id:guid}/development-profile")]
    public async Task<ActionResult<ProjectDevelopmentProfileDto?>> GetDevelopmentProfile(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetDevelopmentProfileAsync(id), "Error loading project development profile");

    [HttpPut("{id:guid}/development-profile")]
    public async Task<ActionResult<ProjectDevelopmentProfileDto>> UpsertDevelopmentProfile(Guid id, [FromBody] UpsertProjectDevelopmentProfileDto dto)
    {
        try
        {
            return Ok(await _projectService.UpsertDevelopmentProfileAsync(id, dto));
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

    [HttpGet("{id:guid}/phases")]
    public async Task<ActionResult<IEnumerable<ProjectPhaseDto>>> GetProjectPhases(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectPhasesAsync(id), "Error loading project phases");

    [HttpGet("{id:guid}/phase-gates")]
    public async Task<ActionResult<IEnumerable<ProjectPhaseGateEvaluationDto>>> GetProjectPhaseGates(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectPhaseGateEvaluationsAsync(id), "Error loading project phase gates");

    [HttpPost("{id:guid}/phases")]
    public async Task<ActionResult<ProjectPhaseDto>> AddProjectPhase(Guid id, [FromBody] CreateProjectPhaseDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectPhaseAsync(id, dto));
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

    [HttpGet("{id:guid}/packages")]
    public async Task<ActionResult<IEnumerable<ProjectPackageDto>>> GetProjectPackages(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectPackagesAsync(id), "Error loading project packages");

    [HttpPost("{id:guid}/packages")]
    public async Task<ActionResult<ProjectPackageDto>> AddProjectPackage(Guid id, [FromBody] CreateProjectPackageDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectPackageAsync(id, dto));
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

    [HttpPut("packages/{packageId:guid}")]
    public async Task<ActionResult<ProjectPackageDto>> UpdateProjectPackage(Guid packageId, [FromBody] UpdateProjectPackageDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectPackageAsync(packageId, dto));
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

    [HttpDelete("packages/{packageId:guid}")]
    public async Task<IActionResult> DeleteProjectPackage(Guid packageId)
    {
        try
        {
            await _projectService.DeleteProjectPackageAsync(packageId);
            return NoContent();
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

    [HttpGet("{id:guid}/boq-items")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<IEnumerable<ProjectBoqItemDto>>> GetProjectBoqItems(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectBoqItemsAsync(id), "Error loading project BOQ items");

    [HttpGet("{id:guid}/boq-classifications")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<ProjectBoqClassificationOptionsDto>> GetProjectBoqClassifications(
        Guid id,
        [FromQuery] DateTime? effectiveAtUtc = null)
        => await ExecuteProjectReadAsync(
            () => _projectService.GetProjectBoqClassificationOptionsAsync(id, effectiveAtUtc),
            "Error loading project BOQ classification options");

    [HttpPost("{id:guid}/boq-items")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    public async Task<ActionResult<ProjectBoqItemDto>> AddProjectBoqItem(Guid id, [FromBody] CreateProjectBoqItemDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectBoqItemAsync(id, dto));
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

    [HttpPut("boq-items/{boqItemId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    public async Task<ActionResult<ProjectBoqItemDto>> UpdateProjectBoqItem(Guid boqItemId, [FromBody] UpdateProjectBoqItemDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectBoqItemAsync(boqItemId, dto));
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

    [HttpDelete("boq-items/{boqItemId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    public async Task<IActionResult> DeleteProjectBoqItem(Guid boqItemId)
    {
        try
        {
            await _projectService.DeleteProjectBoqItemAsync(boqItemId);
            return NoContent();
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

    [HttpGet("{id:guid}/boq-versions")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<ProjectBoqVersionWorkspaceDto>> GetProjectBoqVersions(Guid id)
        => await ExecuteProjectReadAsync(
            () => _projectService.GetProjectBoqVersionWorkspaceAsync(id),
            "Error loading project BoQ versions");

    [HttpGet("{id:guid}/boq-versions/published")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<ProjectBoqVersionDetailDto>> GetPublishedProjectBoqVersion(Guid id)
        => await ExecuteProjectReadAsync(
            () => _projectService.GetPublishedProjectBoqVersionAsync(id),
            "Error loading the approved published project BoQ");

    [HttpGet("{id:guid}/boq-versions/{versionId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<ProjectBoqVersionDetailDto>> GetProjectBoqVersion(Guid id, Guid versionId)
        => await ExecuteProjectReadAsync(
            () => _projectService.GetProjectBoqVersionAsync(id, versionId),
            "Error loading project BoQ version");

    [HttpPost("{id:guid}/boq-versions")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    public async Task<ActionResult<ProjectBoqVersionDetailDto>> CreateProjectBoqVersion(
        Guid id,
        [FromBody] CreateProjectBoqVersionDto dto)
    {
        try
        {
            var result = await _projectService.CreateProjectBoqVersionAsync(
                id,
                dto,
                HttpContext.TraceIdentifier);
            return CreatedAtAction(
                nameof(GetProjectBoqVersion),
                new { id, versionId = result.Id },
                result);
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

    [HttpPost("{id:guid}/boq-versions/{versionId:guid}/submit")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    public async Task<ActionResult<ProjectBoqVersionDetailDto>> SubmitProjectBoqVersion(Guid id, Guid versionId)
        => await ExecuteProjectBoqWorkflowAsync(() => _projectService.SubmitProjectBoqVersionAsync(
            id,
            versionId,
            _currentUserProvider.UserId,
            HttpContext.TraceIdentifier));

    [HttpPost("{id:guid}/boq-versions/{versionId:guid}/approve")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public async Task<ActionResult<ProjectBoqVersionDetailDto>> ApproveProjectBoqVersion(
        Guid id,
        Guid versionId,
        [FromBody] ProjectBoqWorkflowActionDto? request = null)
        => await ExecuteProjectBoqWorkflowAsync(() => _projectService.ApproveProjectBoqVersionAsync(
            id,
            versionId,
            _currentUserProvider.UserId,
            request?.Comments,
            HttpContext.TraceIdentifier));

    [HttpPost("{id:guid}/boq-versions/{versionId:guid}/reject")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public async Task<ActionResult<ProjectBoqVersionDetailDto>> RejectProjectBoqVersion(
        Guid id,
        Guid versionId,
        [FromBody] RejectProjectBoqVersionDto request)
        => await ExecuteProjectBoqWorkflowAsync(() => _projectService.RejectProjectBoqVersionAsync(
            id,
            versionId,
            _currentUserProvider.UserId,
            request.Reason,
            HttpContext.TraceIdentifier));

    [HttpPost("{id:guid}/boq-versions/{versionId:guid}/recall")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    public async Task<ActionResult<ProjectBoqVersionDetailDto>> RecallProjectBoqVersion(
        Guid id,
        Guid versionId,
        [FromBody] RecallProjectBoqVersionDto request)
        => await ExecuteProjectBoqWorkflowAsync(() => _projectService.RecallProjectBoqVersionAsync(
            id,
            versionId,
            _currentUserProvider.UserId,
            request.Reason,
            HttpContext.TraceIdentifier));

    [HttpGet("{id:guid}/boq-versions/compare")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<ProjectBoqVersionComparisonDto>> CompareProjectBoqVersions(
        Guid id,
        [FromQuery] Guid baselineVersionId,
        [FromQuery] Guid comparisonVersionId)
    {
        try
        {
            return Ok(await _projectService.CompareProjectBoqVersionsAsync(
                id,
                baselineVersionId,
                comparisonVersionId));
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

    [HttpGet("{id:guid}/boq-remeasurements/workspace")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<IActionResult> GetProjectBoqRemeasurementWorkspace(Guid id)
        => ExecuteProjectRemeasurementAsync(async () =>
            Ok(await _projectService.GetProjectBoqRemeasurementWorkspaceAsync(id)));

    [HttpPost("{id:guid}/boq-remeasurements")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.BoqManage)]
    public Task<IActionResult> CreateProjectBoqRemeasurement(
        Guid id,
        [FromBody] CreateProjectBoqRemeasurementDto request)
        => ExecuteProjectRemeasurementAsync(async () =>
        {
            var result = await _projectService.CreateProjectBoqRemeasurementAsync(
                id, request, HttpContext.TraceIdentifier);
            return CreatedAtAction(nameof(GetProjectBoqVersion), new { id, versionId = result.Id }, result);
        });

    private async Task<IActionResult> ExecuteProjectRemeasurementAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ProjectRemeasurementProblem(
                403, "QS remeasurement access forbidden", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ProjectRemeasurementProblem(
                400, "QS remeasurement validation failed", exception.Message));
        }
    }

    private ProblemDetails ProjectRemeasurementProblem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-remeasurement-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = $"QS_REMEASUREMENT_{status}",
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };

    private async Task<ActionResult<ProjectBoqVersionDetailDto>> ExecuteProjectBoqWorkflowAsync(
        Func<Task<ProjectBoqVersionDetailDto>> action)
    {
        try
        {
            return Ok(await action());
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

    [HttpGet("{id:guid}/quantity-survey-estimates")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<ActionResult<QuantitySurveyEstimateWorkspaceDto>> GetQuantitySurveyEstimates(Guid id)
        => ExecuteProjectEstimateAsync(() => _projectService.GetQuantitySurveyEstimateWorkspaceAsync(id));

    [HttpGet("{id:guid}/quantity-survey-estimates/{estimateVersionId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<ActionResult<QuantitySurveyEstimateVersionDto>> GetQuantitySurveyEstimate(Guid id, Guid estimateVersionId)
        => ExecuteProjectEstimateAsync(() => _projectService.GetQuantitySurveyEstimateVersionAsync(id, estimateVersionId));

    [HttpPost("{id:guid}/quantity-survey-estimates")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.EstimatesManage)]
    public Task<ActionResult<QuantitySurveyEstimateVersionDto>> CreateQuantitySurveyEstimate(Guid id, [FromBody] CreateQuantitySurveyEstimateRequest request)
        => ExecuteProjectEstimateAsync(() => _projectService.CreateQuantitySurveyEstimateVersionAsync(id, request, HttpContext.TraceIdentifier));

    [HttpPost("{id:guid}/quantity-survey-estimates/{estimateVersionId:guid}/submit")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.EstimatesManage)]
    public Task<ActionResult<QuantitySurveyEstimateVersionDto>> SubmitQuantitySurveyEstimate(Guid id, Guid estimateVersionId)
        => ExecuteProjectEstimateAsync(() => _projectService.SubmitQuantitySurveyEstimateVersionAsync(id, estimateVersionId, _currentUserProvider.UserId, HttpContext.TraceIdentifier));

    [HttpPost("{id:guid}/quantity-survey-estimates/{estimateVersionId:guid}/approve")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<ActionResult<QuantitySurveyEstimateVersionDto>> ApproveQuantitySurveyEstimate(Guid id, Guid estimateVersionId, [FromBody] QuantitySurveyEstimateWorkflowRequest? request = null)
        => ExecuteProjectEstimateAsync(() => _projectService.ApproveQuantitySurveyEstimateVersionAsync(id, estimateVersionId, _currentUserProvider.UserId, request?.Comments, HttpContext.TraceIdentifier));

    [HttpPost("{id:guid}/quantity-survey-estimates/{estimateVersionId:guid}/reject")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public Task<ActionResult<QuantitySurveyEstimateVersionDto>> RejectQuantitySurveyEstimate(Guid id, Guid estimateVersionId, [FromBody] RejectQuantitySurveyEstimateRequest request)
        => ExecuteProjectEstimateAsync(() => _projectService.RejectQuantitySurveyEstimateVersionAsync(id, estimateVersionId, _currentUserProvider.UserId, request.Reason, HttpContext.TraceIdentifier));

    [HttpGet("{id:guid}/quantity-survey-cost-reconciliation")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public Task<ActionResult<QuantitySurveyCostReconciliationDto>> GetQuantitySurveyCostReconciliation(
        Guid id,
        [FromQuery] Guid estimateVersionId)
        => ExecuteProjectEstimateAsync(() => _projectService.GetQuantitySurveyCostReconciliationAsync(id, estimateVersionId));

    [HttpGet("{id:guid}/quantity-survey-cost-dashboard")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.ReportsRead)]
    public Task<ActionResult<QuantitySurveyCostDashboardDto>> GetQuantitySurveyCostDashboard(Guid id)
        => ExecuteProjectEstimateAsync(() => _projectService.GetQuantitySurveyCostDashboardAsync(id));

    private async Task<ActionResult<T>> ExecuteProjectEstimateAsync<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException exception) when (
            exception.Message.StartsWith("Project with ID ", StringComparison.Ordinal) &&
            exception.Message.EndsWith(" not found", StringComparison.Ordinal))
        {
            // Tenant query filters deliberately make a foreign project indistinguishable from a
            // missing project. Preserve that non-disclosure contract with a bounded 404 rather
            // than exposing the lookup failure as a validation error.
            return NotFound(new ProblemDetails
            {
                Type = "https://tdc.gov.gh/problems/quantity-survey-project-not-found",
                Title = "Quantity Survey project not found",
                Status = StatusCodes.Status404NotFound,
                Detail = "The requested project was not found or is outside your tenant scope.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["code"] = "QS_PROJECT_NOT_FOUND",
                    ["correlationId"] = HttpContext.TraceIdentifier
                }
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tdc.gov.gh/problems/quantity-survey-estimate-400",
                Title = "Quantity Survey request could not be completed",
                Status = StatusCodes.Status400BadRequest,
                Detail = exception.Message,
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["code"] = "QS_ESTIMATE_400",
                    ["correlationId"] = HttpContext.TraceIdentifier
                }
            });
        }
    }

    [HttpGet("{id:guid}/approval-register")]
    public async Task<ActionResult<IEnumerable<ProjectApprovalRegisterItemDto>>> GetApprovalRegister(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetApprovalRegisterAsync(id), "Error loading project approval register");

    [HttpPost("{id:guid}/approval-register")]
    public async Task<ActionResult<ProjectApprovalRegisterItemDto>> AddApprovalRegisterItem(Guid id, [FromBody] CreateProjectApprovalRegisterItemDto dto)
    {
        try
        {
            return Ok(await _projectService.AddApprovalRegisterItemAsync(id, dto));
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

    [HttpPut("approval-register/{approvalRegisterItemId:guid}")]
    public async Task<ActionResult<ProjectApprovalRegisterItemDto>> UpdateApprovalRegisterItem(Guid approvalRegisterItemId, [FromBody] UpdateProjectApprovalRegisterItemDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateApprovalRegisterItemAsync(approvalRegisterItemId, dto));
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

    [HttpDelete("approval-register/{approvalRegisterItemId:guid}")]
    public async Task<IActionResult> DeleteApprovalRegisterItem(Guid approvalRegisterItemId)
    {
        try
        {
            await _projectService.DeleteApprovalRegisterItemAsync(approvalRegisterItemId);
            return NoContent();
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

    [HttpGet("{id:guid}/drawings")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<IEnumerable<ProjectDrawingDto>>> GetProjectDrawings(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectDrawingsAsync(id), "Error loading project drawings");

    [HttpPost("{id:guid}/drawings")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignManage)]
    public async Task<ActionResult<ProjectDrawingDto>> AddProjectDrawing(Guid id, [FromBody] CreateProjectDrawingDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectDrawingAsync(id, dto));
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

    [HttpPut("drawings/{drawingId:guid}")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignManage)]
    public async Task<ActionResult<ProjectDrawingDto>> UpdateProjectDrawing(Guid drawingId, [FromBody] UpdateProjectDrawingDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectDrawingAsync(drawingId, dto));
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

    [HttpDelete("drawings/{drawingId:guid}")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.DesignManage)]
    public async Task<IActionResult> DeleteProjectDrawing(Guid drawingId)
    {
        try
        {
            await _projectService.DeleteProjectDrawingAsync(drawingId);
            return NoContent();
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

    [HttpGet("{id:guid}/submittals")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<IEnumerable<ProjectSubmittalDto>>> GetProjectSubmittals(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectSubmittalsAsync(id), "Error loading project submittals");

    [HttpPost("{id:guid}/submittals")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public async Task<ActionResult<ProjectSubmittalDto>> AddProjectSubmittal(Guid id, [FromBody] CreateProjectSubmittalDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectSubmittalAsync(id, dto));
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

    [HttpPut("submittals/{submittalId:guid}")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public async Task<ActionResult<ProjectSubmittalDto>> UpdateProjectSubmittal(Guid submittalId, [FromBody] UpdateProjectSubmittalDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectSubmittalAsync(submittalId, dto));
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

    [HttpDelete("submittals/{submittalId:guid}")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public async Task<IActionResult> DeleteProjectSubmittal(Guid submittalId)
    {
        try
        {
            await _projectService.DeleteProjectSubmittalAsync(submittalId);
            return NoContent();
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

    [HttpGet("{id:guid}/rfis")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<IEnumerable<ProjectRfiDto>>> GetProjectRfis(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectRfisAsync(id), "Error loading project RFIs");

    [HttpPost("{id:guid}/rfis")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public async Task<ActionResult<ProjectRfiDto>> AddProjectRfi(Guid id, [FromBody] CreateProjectRfiDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectRfiAsync(id, dto));
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

    [HttpPut("rfis/{rfiId:guid}")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public async Task<ActionResult<ProjectRfiDto>> UpdateProjectRfi(Guid rfiId, [FromBody] UpdateProjectRfiDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectRfiAsync(rfiId, dto));
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

    [HttpDelete("rfis/{rfiId:guid}")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public async Task<IActionResult> DeleteProjectRfi(Guid rfiId)
    {
        try
        {
            await _projectService.DeleteProjectRfiAsync(rfiId);
            return NoContent();
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

    [HttpGet("{id:guid}/site-instructions")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<IEnumerable<ProjectSiteInstructionDto>>> GetProjectSiteInstructions(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectSiteInstructionsAsync(id), "Error loading project site instructions");

    [HttpPost("{id:guid}/site-instructions")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public async Task<ActionResult<ProjectSiteInstructionDto>> AddProjectSiteInstruction(Guid id, [FromBody] CreateProjectSiteInstructionDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectSiteInstructionAsync(id, dto));
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

    [HttpPut("site-instructions/{siteInstructionId:guid}")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public async Task<ActionResult<ProjectSiteInstructionDto>> UpdateProjectSiteInstruction(Guid siteInstructionId, [FromBody] UpdateProjectSiteInstructionDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectSiteInstructionAsync(siteInstructionId, dto));
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

    [HttpDelete("site-instructions/{siteInstructionId:guid}")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.SupervisionManage)]
    public async Task<IActionResult> DeleteProjectSiteInstruction(Guid siteInstructionId)
    {
        try
        {
            await _projectService.DeleteProjectSiteInstructionAsync(siteInstructionId);
            return NoContent();
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

    [HttpGet("{id:guid}/variation-orders")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<IEnumerable<ProjectVariationOrderDto>>> GetProjectVariationOrders(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectVariationOrdersAsync(id), "Error loading project variation orders");

    [HttpPost("{id:guid}/variation-orders")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.VariationsManage)]
    public async Task<ActionResult<ProjectVariationOrderDto>> AddProjectVariationOrder(Guid id, [FromBody] CreateProjectVariationOrderDto dto)
    {
        try
        {
            if (await _quantitySurveyVariations.IsGovernedAsync(null))
                return Conflict("Use the governed Quantity Survey variation workspace while QS-DEC-011 is effective.");
            return Ok(await _projectService.AddProjectVariationOrderAsync(id, dto));
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

    [HttpPut("variation-orders/{variationOrderId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.VariationsManage)]
    public async Task<ActionResult<ProjectVariationOrderDto>> UpdateProjectVariationOrder(Guid variationOrderId, [FromBody] UpdateProjectVariationOrderDto dto)
    {
        try
        {
            if (await _quantitySurveyVariations.IsGovernedAsync(variationOrderId))
                return Conflict("Use the governed Quantity Survey variation workspace while QS-DEC-011 is effective.");
            return Ok(await _projectService.UpdateProjectVariationOrderAsync(variationOrderId, dto));
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

    [HttpDelete("variation-orders/{variationOrderId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.VariationsManage)]
    public async Task<IActionResult> DeleteProjectVariationOrder(Guid variationOrderId)
    {
        try
        {
            if (await _quantitySurveyVariations.IsGovernedAsync(variationOrderId))
                return Conflict("Use the governed Quantity Survey variation workspace while QS-DEC-011 is effective.");
            await _projectService.DeleteProjectVariationOrderAsync(variationOrderId);
            return NoContent();
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

    [HttpGet("{id:guid}/interim-valuations")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<IEnumerable<ProjectInterimValuationDto>>> GetProjectInterimValuations(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectInterimValuationsAsync(id), "Error loading project interim valuations");

    [HttpPost("{id:guid}/interim-valuations")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public async Task<ActionResult<ProjectInterimValuationDto>> AddProjectInterimValuation(Guid id, [FromBody] CreateProjectInterimValuationDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectInterimValuationAsync(id, dto));
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

    [HttpPut("interim-valuations/{interimValuationId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public async Task<ActionResult<ProjectInterimValuationDto>> UpdateProjectInterimValuation(Guid interimValuationId, [FromBody] UpdateProjectInterimValuationDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectInterimValuationAsync(interimValuationId, dto));
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

    [HttpDelete("interim-valuations/{interimValuationId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.ValuationsManage)]
    public async Task<IActionResult> DeleteProjectInterimValuation(Guid interimValuationId)
    {
        try
        {
            await _projectService.DeleteProjectInterimValuationAsync(interimValuationId);
            return NoContent();
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

    [HttpGet("{id:guid}/payment-certificates")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<IEnumerable<ProjectPaymentCertificateDto>>> GetProjectPaymentCertificates(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectPaymentCertificatesAsync(id), "Error loading project payment certificates");

    [HttpPost("{id:guid}/payment-certificates")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public async Task<ActionResult<ProjectPaymentCertificateDto>> AddProjectPaymentCertificate(Guid id, [FromBody] CreateProjectPaymentCertificateDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectPaymentCertificateAsync(id, dto));
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

    [HttpPut("payment-certificates/{paymentCertificateId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public async Task<ActionResult<ProjectPaymentCertificateDto>> UpdateProjectPaymentCertificate(Guid paymentCertificateId, [FromBody] UpdateProjectPaymentCertificateDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectPaymentCertificateAsync(paymentCertificateId, dto));
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

    [HttpDelete("payment-certificates/{paymentCertificateId:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.CertificatesManage)]
    public async Task<IActionResult> DeleteProjectPaymentCertificate(Guid paymentCertificateId)
    {
        try
        {
            await _projectService.DeleteProjectPaymentCertificateAsync(paymentCertificateId);
            return NoContent();
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

    [HttpGet("{id:guid}/extension-of-time-requests")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<IEnumerable<ProjectExtensionOfTimeDto>>> GetProjectExtensionOfTimeRequests(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectExtensionOfTimeRequestsAsync(id), "Error loading project extension of time requests");

    [HttpPost("{id:guid}/extension-of-time-requests")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.CommercialManage)]
    public async Task<ActionResult<ProjectExtensionOfTimeDto>> AddProjectExtensionOfTimeRequest(Guid id, [FromBody] CreateProjectExtensionOfTimeDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectExtensionOfTimeRequestAsync(id, dto));
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

    [HttpPut("extension-of-time-requests/{extensionOfTimeId:guid}")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.CommercialManage)]
    public async Task<ActionResult<ProjectExtensionOfTimeDto>> UpdateProjectExtensionOfTimeRequest(Guid extensionOfTimeId, [FromBody] UpdateProjectExtensionOfTimeDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectExtensionOfTimeRequestAsync(extensionOfTimeId, dto));
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

    [HttpDelete("extension-of-time-requests/{extensionOfTimeId:guid}")]
    [Authorize(Policy = CivilEngineeringAccessControlRegistry.CommercialManage)]
    public async Task<IActionResult> DeleteProjectExtensionOfTimeRequest(Guid extensionOfTimeId)
    {
        try
        {
            await _projectService.DeleteProjectExtensionOfTimeRequestAsync(extensionOfTimeId);
            return NoContent();
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

    [HttpGet("{id:guid}/final-account")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public async Task<ActionResult<ProjectFinalAccountDto?>> GetProjectFinalAccount(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectFinalAccountAsync(id), "Error loading project final account");

    [HttpPut("{id:guid}/final-account")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.FinalAccountsManage)]
    public async Task<ActionResult<ProjectFinalAccountDto>> UpsertProjectFinalAccount(Guid id, [FromBody] UpsertProjectFinalAccountDto dto)
    {
        try
        {
            return Ok(await _projectService.UpsertProjectFinalAccountAsync(id, dto));
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

    [HttpGet("{id:guid}/units")]
    public async Task<ActionResult<IEnumerable<ProjectUnitDto>>> GetProjectUnits(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectUnitsAsync(id), "Error loading project units");

    [HttpGet("{id:guid}/buildings")]
    public async Task<ActionResult<IEnumerable<ProjectBuildingDto>>> GetProjectBuildings(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectBuildingsAsync(id), "Error loading project buildings");

    [HttpPost("{id:guid}/buildings")]
    public async Task<ActionResult<ProjectBuildingDto>> AddProjectBuilding(Guid id, [FromBody] CreateProjectBuildingDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectBuildingAsync(id, dto));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("buildings/{buildingId:guid}")]
    public async Task<ActionResult<ProjectBuildingDto>> UpdateProjectBuilding(Guid buildingId, [FromBody] UpdateProjectBuildingDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectBuildingAsync(buildingId, dto));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("buildings/{buildingId:guid}")]
    public async Task<IActionResult> DeleteProjectBuilding(Guid buildingId)
    {
        try
        {
            await _projectService.DeleteProjectBuildingAsync(buildingId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}/floors")]
    public async Task<ActionResult<IEnumerable<ProjectFloorDto>>> GetProjectFloors(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectFloorsAsync(id), "Error loading project floors");

    [HttpPost("{id:guid}/floors")]
    public async Task<ActionResult<ProjectFloorDto>> AddProjectFloor(Guid id, [FromBody] CreateProjectFloorDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectFloorAsync(id, dto));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("floors/{floorId:guid}")]
    public async Task<ActionResult<ProjectFloorDto>> UpdateProjectFloor(Guid floorId, [FromBody] UpdateProjectFloorDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectFloorAsync(floorId, dto));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("floors/{floorId:guid}")]
    public async Task<IActionResult> DeleteProjectFloor(Guid floorId)
    {
        try
        {
            await _projectService.DeleteProjectFloorAsync(floorId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}/unit-release-batches")]
    public async Task<ActionResult<IEnumerable<ProjectUnitReleaseBatchDto>>> GetProjectUnitReleaseBatches(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectUnitReleaseBatchesAsync(id), "Error loading project unit release batches");

    [HttpPost("{id:guid}/unit-release-batches")]
    public async Task<ActionResult<ProjectUnitReleaseBatchDto>> AddProjectUnitReleaseBatch(Guid id, [FromBody] CreateProjectUnitReleaseBatchDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectUnitReleaseBatchAsync(id, dto));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("unit-release-batches/{unitReleaseBatchId:guid}")]
    public async Task<ActionResult<ProjectUnitReleaseBatchDto>> UpdateProjectUnitReleaseBatch(Guid unitReleaseBatchId, [FromBody] UpdateProjectUnitReleaseBatchDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectUnitReleaseBatchAsync(unitReleaseBatchId, dto));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("unit-release-batches/{unitReleaseBatchId:guid}")]
    public async Task<IActionResult> DeleteProjectUnitReleaseBatch(Guid unitReleaseBatchId)
    {
        try
        {
            await _projectService.DeleteProjectUnitReleaseBatchAsync(unitReleaseBatchId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}/unit-handover-batches")]
    public async Task<ActionResult<IEnumerable<ProjectUnitHandoverBatchDto>>> GetProjectUnitHandoverBatches(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectUnitHandoverBatchesAsync(id), "Error loading project unit handover batches");

    [HttpPost("{id:guid}/unit-handover-batches")]
    public async Task<ActionResult<ProjectUnitHandoverBatchDto>> AddProjectUnitHandoverBatch(Guid id, [FromBody] CreateProjectUnitHandoverBatchDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectUnitHandoverBatchAsync(id, dto));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("unit-handover-batches/{unitHandoverBatchId:guid}")]
    public async Task<ActionResult<ProjectUnitHandoverBatchDto>> UpdateProjectUnitHandoverBatch(Guid unitHandoverBatchId, [FromBody] UpdateProjectUnitHandoverBatchDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectUnitHandoverBatchAsync(unitHandoverBatchId, dto));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("unit-handover-batches/{unitHandoverBatchId:guid}")]
    public async Task<IActionResult> DeleteProjectUnitHandoverBatch(Guid unitHandoverBatchId)
    {
        try
        {
            await _projectService.DeleteProjectUnitHandoverBatchAsync(unitHandoverBatchId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("units/released-market")]
    public async Task<ActionResult<IEnumerable<ProjectReleasedUnitSalesLookupDto>>> GetReleasedProjectUnitsForSales([FromQuery] string? search = null, [FromQuery] int take = 50)
        => await ExecuteProjectReadAsync(() => _projectService.GetReleasedProjectUnitsForSalesAsync(search, take), "Error loading released project units for sales");

    [HttpPost("{id:guid}/units")]
    public async Task<ActionResult<ProjectUnitDto>> AddProjectUnit(Guid id, [FromBody] CreateProjectUnitDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectUnitAsync(id, dto));
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

    [HttpPut("units/{unitId:guid}")]
    public async Task<ActionResult<ProjectUnitDto>> UpdateProjectUnit(Guid unitId, [FromBody] UpdateProjectUnitDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectUnitAsync(unitId, dto));
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

    [HttpPost("units/{unitId:guid}/release")]
    public async Task<ActionResult<ProjectUnitDto>> ReleaseProjectUnit(Guid unitId)
    {
        try
        {
            return Ok(await _projectService.ReleaseProjectUnitAsync(unitId));
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

    [HttpPost("units/{unitId:guid}/withdraw-release")]
    public async Task<ActionResult<ProjectUnitDto>> WithdrawProjectUnitRelease(Guid unitId)
    {
        try
        {
            return Ok(await _projectService.WithdrawProjectUnitReleaseAsync(unitId));
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

    [HttpPost("units/{unitId:guid}/publish-to-estate")]
    public async Task<ActionResult<EstateManagedAssetDto>> PublishProjectUnitToEstate(Guid unitId)
    {
        try
        {
            // Estate/Project integration: Project remains the source of constructed units; Estate receives the managed asset.
            var asset = await _projectService.PublishProjectUnitToEstateAsync(unitId);
            await NotifyEstateProjectUnitHandoffAsync(asset);
            return Ok(asset);
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

    private async Task NotifyEstateProjectUnitHandoffAsync(EstateManagedAssetDto asset)
    {
        try
        {
            // Estate/Project integration: notify Estate roles about the handoff without changing Project module ownership.
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                _currentUserProvider.TenantId,
                _currentUserProvider.UserId,
                new[] { "Property Manager", "Property Officer", "Estate Manager", "Estate Officer", "Facilities Manager" },
                "Project unit pushed to Estate / Property Management",
                $"{asset.ProjectCode} unit {asset.ProjectUnitCode ?? asset.AssetCode} is ready in Estate / Property Management.",
                "project.unit.estate-handoff",
                "EstateManagedAsset",
                asset.Id,
                "/estate/property-management/EstatePropertyManagementPropertyUnit",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = "Source: Project Management -> Estate / Property Management",
                    ["sourceModule"] = "Project Management",
                    ["targetModule"] = "Estate / Property Management",
                    ["assetCode"] = asset.AssetCode,
                    ["assetName"] = asset.Name,
                    ["projectId"] = asset.ProjectId?.ToString() ?? string.Empty,
                    ["projectCode"] = asset.ProjectCode ?? string.Empty,
                    ["projectTitle"] = asset.ProjectTitle ?? string.Empty,
                    ["projectUnitId"] = asset.ProjectUnitId?.ToString() ?? string.Empty,
                    ["projectUnitCode"] = asset.ProjectUnitCode ?? string.Empty
                },
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Estate notification failed for project unit handoff {AssetId}", asset.Id);
        }
    }

    [HttpPost("units/{unitId:guid}/create-sales-agreement")]
    public async Task<ActionResult<ProjectUnitDto>> CreateSalesAgreementFromProjectUnit(Guid unitId)
    {
        try
        {
            return Ok(await _projectService.CreateSalesAgreementFromProjectUnitAsync(unitId));
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

    [HttpPost("units/{unitId:guid}/create-lease-agreement")]
    public async Task<ActionResult<ProjectUnitDto>> CreateLeaseAgreementFromProjectUnit(Guid unitId)
    {
        try
        {
            return Ok(await _projectService.CreateLeaseAgreementFromProjectUnitAsync(unitId));
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

    [HttpPost("units/{unitId:guid}/create-sales-order")]
    public async Task<ActionResult<ProjectUnitDto>> CreateSalesOrderFromProjectUnit(Guid unitId)
    {
        try
        {
            return Ok(await _projectService.CreateSalesOrderFromProjectUnitAsync(unitId));
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

    [HttpPost("units/{unitId:guid}/link-sales-agreement")]
    public async Task<ActionResult<ProjectUnitDto>> LinkSalesAgreementToProjectUnit(Guid unitId, [FromBody] LinkProjectUnitSalesAgreementDto dto)
    {
        try
        {
            return Ok(await _projectService.LinkSalesAgreementToProjectUnitAsync(unitId, dto.SalesAgreementId));
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

    [HttpPost("units/{unitId:guid}/link-sales-order")]
    public async Task<ActionResult<ProjectUnitDto>> LinkSalesOrderToProjectUnit(Guid unitId, [FromBody] LinkProjectUnitSalesOrderDto dto)
    {
        try
        {
            return Ok(await _projectService.LinkSalesOrderToProjectUnitAsync(unitId, dto.SalesOrderId));
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

    [HttpDelete("units/{unitId:guid}")]
    public async Task<IActionResult> DeleteProjectUnit(Guid unitId)
    {
        try
        {
            await _projectService.DeleteProjectUnitAsync(unitId);
            return NoContent();
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

    [HttpGet("{id:guid}/customer-variations")]
    public async Task<ActionResult<IEnumerable<ProjectCustomerVariationDto>>> GetCustomerVariations(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetCustomerVariationsAsync(id), "Error loading project customer variations");

    [HttpPost("{id:guid}/customer-variations")]
    public async Task<ActionResult<ProjectCustomerVariationDto>> AddCustomerVariation(Guid id, [FromBody] CreateProjectCustomerVariationDto dto)
    {
        try
        {
            return Ok(await _projectService.AddCustomerVariationAsync(id, dto));
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

    [HttpPut("customer-variations/{variationId:guid}")]
    public async Task<ActionResult<ProjectCustomerVariationDto>> UpdateCustomerVariation(Guid variationId, [FromBody] UpdateProjectCustomerVariationDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateCustomerVariationAsync(variationId, dto));
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

    [HttpPost("customer-variations/{variationId:guid}/create-job-card")]
    public async Task<ActionResult<ProjectCustomerVariationDto>> CreateCustomerVariationJobCard(Guid variationId, [FromBody] CreateProjectMaintenanceFollowThroughDto? dto)
    {
        try
        {
            return Ok(await _projectService.CreateJobCardFromCustomerVariationAsync(variationId, dto));
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

    [HttpPost("customer-variations/{variationId:guid}/create-work-order")]
    public async Task<ActionResult<ProjectCustomerVariationDto>> CreateCustomerVariationWorkOrder(Guid variationId, [FromBody] CreateProjectMaintenanceFollowThroughDto? dto)
    {
        try
        {
            return Ok(await _projectService.CreateWorkOrderFromCustomerVariationAsync(variationId, dto));
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

    [HttpDelete("customer-variations/{variationId:guid}")]
    public async Task<IActionResult> DeleteCustomerVariation(Guid variationId)
    {
        try
        {
            await _projectService.DeleteCustomerVariationAsync(variationId);
            return NoContent();
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

    [HttpGet("{id:guid}/commissioning")]
    public async Task<ActionResult<IEnumerable<ProjectCommissioningItemDto>>> GetProjectCommissioningItems(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectCommissioningItemsAsync(id), "Error loading project commissioning items");

    [HttpPost("{id:guid}/commissioning")]
    public async Task<ActionResult<ProjectCommissioningItemDto>> AddProjectCommissioningItem(Guid id, [FromBody] CreateProjectCommissioningItemDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectCommissioningItemAsync(id, dto));
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

    [HttpPut("commissioning/{commissioningItemId:guid}")]
    public async Task<ActionResult<ProjectCommissioningItemDto>> UpdateProjectCommissioningItem(Guid commissioningItemId, [FromBody] UpdateProjectCommissioningItemDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectCommissioningItemAsync(commissioningItemId, dto));
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

    [HttpDelete("commissioning/{commissioningItemId:guid}")]
    public async Task<IActionResult> DeleteProjectCommissioningItem(Guid commissioningItemId)
    {
        try
        {
            await _projectService.DeleteProjectCommissioningItemAsync(commissioningItemId);
            return NoContent();
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

    [HttpGet("{id:guid}/handover-items")]
    public async Task<ActionResult<IEnumerable<ProjectHandoverItemDto>>> GetProjectHandoverItems(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectHandoverItemsAsync(id), "Error loading project handover items");

    [HttpPost("{id:guid}/handover-items")]
    public async Task<ActionResult<ProjectHandoverItemDto>> AddProjectHandoverItem(Guid id, [FromBody] CreateProjectHandoverItemDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectHandoverItemAsync(id, dto));
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

    [HttpPut("handover-items/{handoverItemId:guid}")]
    public async Task<ActionResult<ProjectHandoverItemDto>> UpdateProjectHandoverItem(Guid handoverItemId, [FromBody] UpdateProjectHandoverItemDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectHandoverItemAsync(handoverItemId, dto));
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

    [HttpDelete("handover-items/{handoverItemId:guid}")]
    public async Task<IActionResult> DeleteProjectHandoverItem(Guid handoverItemId)
    {
        try
        {
            await _projectService.DeleteProjectHandoverItemAsync(handoverItemId);
            return NoContent();
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

    [HttpGet("{id:guid}/snag-items")]
    public async Task<ActionResult<IEnumerable<ProjectSnagItemDto>>> GetProjectSnagItems(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectSnagItemsAsync(id), "Error loading project snag items");

    [HttpPost("{id:guid}/snag-items")]
    public async Task<ActionResult<ProjectSnagItemDto>> AddProjectSnagItem(Guid id, [FromBody] CreateProjectSnagItemDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectSnagItemAsync(id, dto));
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

    [HttpPut("snag-items/{snagItemId:guid}")]
    public async Task<ActionResult<ProjectSnagItemDto>> UpdateProjectSnagItem(Guid snagItemId, [FromBody] UpdateProjectSnagItemDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectSnagItemAsync(snagItemId, dto));
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

    [HttpDelete("snag-items/{snagItemId:guid}")]
    public async Task<IActionResult> DeleteProjectSnagItem(Guid snagItemId)
    {
        try
        {
            await _projectService.DeleteProjectSnagItemAsync(snagItemId);
            return NoContent();
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

    [HttpGet("{id:guid}/defect-liability")]
    public async Task<ActionResult<IEnumerable<ProjectDefectLiabilityCaseDto>>> GetProjectDefectLiabilityCases(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetProjectDefectLiabilityCasesAsync(id), "Error loading project defect liability cases");

    [HttpPost("{id:guid}/defect-liability")]
    public async Task<ActionResult<ProjectDefectLiabilityCaseDto>> AddProjectDefectLiabilityCase(Guid id, [FromBody] CreateProjectDefectLiabilityCaseDto dto)
    {
        try
        {
            return Ok(await _projectService.AddProjectDefectLiabilityCaseAsync(id, dto));
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

    [HttpPut("defect-liability/{defectLiabilityCaseId:guid}")]
    public async Task<ActionResult<ProjectDefectLiabilityCaseDto>> UpdateProjectDefectLiabilityCase(Guid defectLiabilityCaseId, [FromBody] UpdateProjectDefectLiabilityCaseDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectDefectLiabilityCaseAsync(defectLiabilityCaseId, dto));
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

    [HttpPost("defect-liability/{defectLiabilityCaseId:guid}/create-job-card")]
    public async Task<ActionResult<ProjectDefectLiabilityCaseDto>> CreateDefectLiabilityJobCard(Guid defectLiabilityCaseId, [FromBody] CreateProjectMaintenanceFollowThroughDto? dto)
    {
        try
        {
            return Ok(await _projectService.CreateJobCardFromDefectLiabilityCaseAsync(defectLiabilityCaseId, dto));
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

    [HttpPost("defect-liability/{defectLiabilityCaseId:guid}/create-work-order")]
    public async Task<ActionResult<ProjectDefectLiabilityCaseDto>> CreateDefectLiabilityWorkOrder(Guid defectLiabilityCaseId, [FromBody] CreateProjectMaintenanceFollowThroughDto? dto)
    {
        try
        {
            return Ok(await _projectService.CreateWorkOrderFromDefectLiabilityCaseAsync(defectLiabilityCaseId, dto));
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

    [HttpDelete("defect-liability/{defectLiabilityCaseId:guid}")]
    public async Task<IActionResult> DeleteProjectDefectLiabilityCase(Guid defectLiabilityCaseId)
    {
        try
        {
            await _projectService.DeleteProjectDefectLiabilityCaseAsync(defectLiabilityCaseId);
            return NoContent();
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

    [HttpPut("phases/{phaseId:guid}")]
    public async Task<ActionResult<ProjectPhaseDto>> UpdateProjectPhase(Guid phaseId, [FromBody] UpdateProjectPhaseDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateProjectPhaseAsync(phaseId, dto));
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

    [HttpPost("phases/{phaseId:guid}/advance")]
    public async Task<ActionResult<ProjectPhaseProgressionResultDto>> AdvanceProjectPhase(Guid phaseId, [FromBody] AdvanceProjectPhaseDto dto)
    {
        try
        {
            return Ok(await _projectService.AdvanceProjectPhaseAsync(phaseId, dto));
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

    [HttpPost("{id:guid}/phases/reorder")]
    public async Task<IActionResult> ReorderProjectPhases(Guid id, [FromBody] ReorderProjectPhasesDto dto)
    {
        try
        {
            await _projectService.ReorderProjectPhasesAsync(id, dto);
            return NoContent();
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

    [HttpDelete("phases/{phaseId:guid}")]
    public async Task<IActionResult> DeleteProjectPhase(Guid phaseId)
    {
        try
        {
            await _projectService.DeleteProjectPhaseAsync(phaseId);
            return NoContent();
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

    [Authorize(Policy = "InternalOnly")]
    [HttpGet("{id:guid}/workspace")]
    public async Task<ActionResult<ProjectWorkspaceDto>> GetProjectWorkspace(Guid id)
    {
        try
        {
            var workspace = await _projectService.GetProjectWorkspaceAsync(id);
            return workspace == null ? NotFound() : Ok(workspace);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading project workspace");
            return StatusCode(500, "An error occurred while processing the request");
        }
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
    [HttpGet("{id:guid}/materials/cost-entries")]
    public async Task<ActionResult<IEnumerable<ProjectMaterialCostEntryDto>>> GetProjectMaterialCostEntries(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetMaterialCostEntriesAsync(id), "Error loading project material cost entries");

    [Authorize(Policy = "InternalOnly")]
    [HttpGet("{id:guid}/financial-control-summary")]
    public async Task<ActionResult<ProjectFinancialControlSummaryDto>> GetFinancialControlSummary(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetFinancialControlSummaryAsync(id), "Error loading financial control summary");

    [Authorize(Policy = "InternalOnly")]
    [HttpGet("{id:guid}/commercial-summary")]
    public async Task<ActionResult<ProjectCommercialSummaryDto>> GetCommercialSummary(Guid id)
        => await ExecuteProjectReadAsync(() => _projectService.GetCommercialSummaryAsync(id), "Error loading commercial summary");

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
    public async Task<ActionResult<ProjectMilestoneDto>> AddMilestone(Guid projectId, [FromBody] CreateProjectMilestoneDto dto)
    {
        try
        {
            return Ok(await _projectService.AddMilestoneAsync(projectId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("milestones/{milestoneId:guid}")]
    public async Task<ActionResult<ProjectMilestoneDto>> UpdateMilestone(Guid milestoneId, [FromBody] CreateProjectMilestoneDto dto)
    {
        try
        {
            return Ok(await _projectService.UpdateMilestoneAsync(milestoneId, dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

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

    [HttpDelete("deliverables/{deliverableId:guid}")]
    public async Task<IActionResult> DeleteDeliverable(Guid deliverableId)
    {
        await _projectService.DeleteDeliverableAsync(deliverableId);
        return NoContent();
    }

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

    [HttpPost("external/my-projects/{projectId:guid}/deliverables/{deliverableId:guid}/reject")]
    public async Task<ActionResult<ProjectDeliverableDto>> RejectExternalDeliverable(Guid projectId, Guid deliverableId, [FromBody] RejectProjectRequest? request = null)
    {
        try
        {
            return Ok(await _projectService.RejectExternalDeliverableAsync(projectId, deliverableId, request?.Comments ?? request?.Reason, _currentUserProvider.UserId));
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
