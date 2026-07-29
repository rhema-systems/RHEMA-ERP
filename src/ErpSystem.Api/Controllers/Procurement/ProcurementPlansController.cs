using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class ProcurementPlansController : ControllerBase
{
    private readonly IProcurementPlanService _planService;
    private readonly ILogger<ProcurementPlansController> _logger;

    public ProcurementPlansController(
        IProcurementPlanService planService,
        ILogger<ProcurementPlansController> logger)
    {
        _planService = planService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProcurementPlanDto>>> GetPlans(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] int? fiscalYear = null)
    {
        try
        {
            var result = await _planService.GetPlansAsync(page, pageSize, search, status, departmentId, fiscalYear);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting procurement plans");
            return StatusCode(500, "An error occurred while retrieving procurement plans");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcurementPlanDetailDto>> GetPlan(Guid id)
    {
        try
        {
            var plan = await _planService.GetByIdAsync(id);
            if (plan == null) return NotFound($"Procurement plan with ID {id} not found");
            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting procurement plan {PlanId}", id);
            return StatusCode(500, "An error occurred while retrieving the procurement plan");
        }
    }

    [HttpGet("by-number/{planNumber}")]
    public async Task<ActionResult<ProcurementPlanDto>> GetByPlanNumber(string planNumber)
    {
        try
        {
            var plan = await _planService.GetByPlanNumberAsync(planNumber);
            if (plan == null) return NotFound($"Procurement plan with number {planNumber} not found");
            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting procurement plan by number {PlanNumber}", planNumber);
            return StatusCode(500, "An error occurred while retrieving the procurement plan");
        }
    }

    [HttpGet("department/{departmentId}")]
    public async Task<ActionResult<IEnumerable<ProcurementPlanDto>>> GetByDepartment(Guid departmentId)
    {
        try
        {
            var plans = await _planService.GetByDepartmentAsync(departmentId);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting procurement plans for department {DepartmentId}", departmentId);
            return StatusCode(500, "An error occurred while retrieving procurement plans");
        }
    }

    [HttpGet("fiscal-year/{fiscalYear}")]
    public async Task<ActionResult<IEnumerable<ProcurementPlanDto>>> GetByFiscalYear(int fiscalYear)
    {
        try
        {
            var plans = await _planService.GetByFiscalYearAsync(fiscalYear);
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting procurement plans for fiscal year {FiscalYear}", fiscalYear);
            return StatusCode(500, "An error occurred while retrieving procurement plans");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ProcurementPlanDto>>> GetActivePlans()
    {
        try
        {
            var plans = await _planService.GetActivePlansAsync();
            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active procurement plans");
            return StatusCode(500, "An error occurred while retrieving active procurement plans");
        }
    }

    [HttpGet("consolidation-opportunities")]
    public async Task<ActionResult<IEnumerable<ProcurementPlanConsolidationOpportunityDto>>> GetConsolidationOpportunities(
        [FromQuery] int? fiscalYear = null,
        [FromQuery] string? planningQuarter = null,
        [FromQuery] Guid? departmentId = null)
    {
        try
        {
            var opportunities = await _planService.GetConsolidationOpportunitiesAsync(fiscalYear, planningQuarter, departmentId);
            return Ok(opportunities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting procurement plan consolidation opportunities");
            return StatusCode(500, "An error occurred while retrieving consolidation opportunities");
        }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ProcurementPlanningDashboardDto>> GetDashboard(
        [FromQuery] int? fiscalYear = null,
        [FromQuery] string? planningQuarter = null,
        [FromQuery] Guid? departmentId = null)
    {
        try
        {
            var dashboard = await _planService.GetDashboardAsync(fiscalYear, planningQuarter, departmentId);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting procurement planning dashboard");
            return StatusCode(500, "An error occurred while retrieving procurement planning dashboard");
        }
    }

    [HttpGet("reports/{reportType}")]
    public async Task<ActionResult<ProcurementPlanningReportDto>> GetReport(
        string reportType,
        [FromQuery] int? fiscalYear = null,
        [FromQuery] string? planningQuarter = null,
        [FromQuery] Guid? departmentId = null)
    {
        try
        {
            var report = await _planService.GetReportAsync(reportType, fiscalYear, planningQuarter, departmentId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting procurement planning report {ReportType}", reportType);
            return StatusCode(500, "An error occurred while retrieving procurement planning report");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProcurementPlanDetailDto>> CreatePlan([FromBody] CreateProcurementPlanDto dto)
    {
        try
        {
            var plan = await _planService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating procurement plan");
            return StatusCode(500, "An error occurred while creating the procurement plan");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProcurementPlanDetailDto>> UpdatePlan(Guid id, [FromBody] UpdateProcurementPlanDto dto)
    {
        try
        {
            var plan = await _planService.UpdateAsync(id, dto);
            return Ok(plan);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating procurement plan {PlanId}", id);
            return StatusCode(500, "An error occurred while updating the procurement plan");
        }
    }

    [HttpPost("{id}/submit")]
    public async Task<ActionResult<ProcurementPlanDetailDto>> SubmitForApproval(Guid id, [FromBody] SubmitProcurementPlanDto dto)
    {
        try
        {
            var plan = await _planService.SubmitForApprovalAsync(id, dto);
            return Ok(plan);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting procurement plan {PlanId}", id);
            return StatusCode(500, "An error occurred while submitting the procurement plan");
        }
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult<ProcurementPlanDetailDto>> Approve(Guid id, [FromBody] ApproveProcurementPlanDto dto)
    {
        try
        {
            var plan = await _planService.ApproveAsync(id, dto);
            return Ok(plan);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving procurement plan {PlanId}", id);
            return StatusCode(500, "An error occurred while approving the procurement plan");
        }
    }

    [HttpPost("{id}/publish")]
    public async Task<ActionResult<ProcurementPlanDetailDto>> Publish(Guid id, [FromBody] PublishProcurementPlanDto dto)
    {
        try
        {
            var plan = await _planService.PublishAsync(id, dto);
            return Ok(plan);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing procurement plan {PlanId}", id);
            return StatusCode(500, "An error occurred while publishing the procurement plan");
        }
    }

    [HttpGet("{id}/versions")]
    public async Task<ActionResult<IEnumerable<ProcurementPlanDto>>> GetVersionHistory(Guid id)
    {
        try
        {
            var versions = await _planService.GetVersionHistoryAsync(id);
            return Ok(versions);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting version history for procurement plan {PlanId}", id);
            return StatusCode(500, "An error occurred while retrieving the procurement plan version history");
        }
    }

    [HttpPost("{id}/amendments")]
    public async Task<ActionResult<ProcurementPlanDetailDto>> CreateAmendment(Guid id, [FromBody] CreateProcurementPlanAmendmentDto dto)
    {
        try
        {
            var plan = await _planService.CreateAmendmentAsync(id, dto);
            return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, plan);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating amendment for procurement plan {PlanId}", id);
            return StatusCode(500, "An error occurred while creating the procurement plan amendment");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeletePlan(Guid id)
    {
        try
        {
            await _planService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting procurement plan {PlanId}", id);
            return StatusCode(500, "An error occurred while deleting the procurement plan");
        }
    }

    [HttpPost("{planId}/items")]
    public async Task<ActionResult<ProcurementPlanItemDto>> AddItem(Guid planId, [FromBody] CreateProcurementPlanItemDto dto)
    {
        try
        {
            var item = await _planService.AddItemAsync(planId, dto);
            return Ok(item);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to procurement plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while adding the item");
        }
    }

    [HttpPut("items/{itemId}")]
    public async Task<ActionResult<ProcurementPlanItemDto>> UpdateItem(Guid itemId, [FromBody] UpdateProcurementPlanItemDto dto)
    {
        try
        {
            var item = await _planService.UpdateItemAsync(itemId, dto);
            return Ok(item);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating plan item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while updating the item");
        }
    }

    [HttpDelete("items/{itemId}")]
    public async Task<ActionResult> DeleteItem(Guid itemId)
    {
        try
        {
            await _planService.DeleteItemAsync(itemId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting plan item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while deleting the item");
        }
    }

    [HttpGet("{planId}/items")]
    public async Task<ActionResult<IEnumerable<ProcurementPlanItemDto>>> GetItems(Guid planId)
    {
        try
        {
            var items = await _planService.GetItemsByPlanIdAsync(planId);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting items for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while retrieving items");
        }
    }

    [HttpGet("{planId}/items/critical")]
    public async Task<ActionResult<IEnumerable<ProcurementPlanItemDto>>> GetCriticalItems(Guid planId)
    {
        try
        {
            var items = await _planService.GetCriticalItemsAsync(planId);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting critical items for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while retrieving critical items");
        }
    }

    [HttpPost("items/convert-to-tender")]
    public async Task<ActionResult<PlanItemConversionResultDto>> ConvertItemToTender([FromBody] ConvertPlanItemToTenderDto dto)
    {
        try
        {
            var result = await _planService.ConvertItemToTenderAsync(dto);
            return Ok(result);
        }
        catch (ProcurementRequisitionSourcingBlockedException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Readiness.DecisionCode, ex.Message, ex.Readiness));
        }
        catch (ProcurementRequisitionSourcingValidationException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Code, ex.Message));
        }
        catch (ProcurementRequisitionSourcingAuthorizationException ex)
        {
            return StatusCode(403, SourcingProblem("PR_SOURCING_CONTROL_FORBIDDEN", ex.Message, status: 403));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting plan item {PlanItemId} to tender", dto.PlanItemId);
            return StatusCode(500, "An error occurred while converting the plan item to tender");
        }
    }

    [HttpPost("items/convert-to-purchase-order")]
    public async Task<ActionResult<PlanItemConversionResultDto>> ConvertItemToPurchaseOrder([FromBody] ConvertPlanItemToPurchaseOrderDto dto)
    {
        try
        {
            var result = await _planService.ConvertItemToPurchaseOrderAsync(dto);
            return Ok(result);
        }
        catch (ProcurementPurchaseOrderSourceValidationException ex)
        {
            return UnprocessableEntity(new
            {
                code = ex.Code,
                message = ex.Message,
                correlationId = HttpContext.TraceIdentifier
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting plan item {PlanItemId} to purchase order", dto.PlanItemId);
            return StatusCode(500, "An error occurred while converting the plan item to purchase order");
        }
    }

    [HttpPost("items/convert-to-rfq")]
    public async Task<ActionResult<PlanItemConversionResultDto>> ConvertItemToRfq([FromBody] ConvertPlanItemToTenderDto dto)
    {
        try
        {
            var result = await _planService.ConvertItemToRfqAsync(dto);
            return Ok(result);
        }
        catch (ProcurementRequisitionSourcingBlockedException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Readiness.DecisionCode, ex.Message, ex.Readiness));
        }
        catch (ProcurementRequisitionSourcingValidationException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Code, ex.Message));
        }
        catch (ProcurementRequisitionSourcingAuthorizationException ex)
        {
            return StatusCode(403, SourcingProblem("PR_SOURCING_CONTROL_FORBIDDEN", ex.Message, status: 403));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting plan item {PlanItemId} to RFQ", dto.PlanItemId);
            return StatusCode(500, "An error occurred while converting the plan item to RFQ");
        }
    }

    [HttpPut("items/{itemId}/status")]
    public async Task<ActionResult<ProcurementPlanItemDto>> UpdateItemStatus(Guid itemId, [FromBody] UpdateItemStatusDto dto)
    {
        try
        {
            var result = await _planService.UpdateItemStatusAsync(itemId, dto.Status);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating plan item {ItemId} status", itemId);
            return StatusCode(500, "An error occurred while updating the plan item status");
        }
    }

    [HttpGet("items/{itemId}/validate-budget")]
    public async Task<ActionResult<BudgetValidationResultDto>> ValidateBudgetForItem(Guid itemId, [FromQuery] decimal? amount = null)
    {
        try
        {
            var result = await _planService.ValidateBudgetForItemAsync(itemId, amount);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating budget for plan item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while validating the budget");
        }
    }

    private ProblemDetails SourcingProblem(
        string code,
        string detail,
        PurchaseRequisitionSourcingReadinessDto? readiness = null,
        int status = 422)
    {
        var problem = new ProblemDetails { Status = status, Title = code, Detail = detail, Instance = HttpContext.Request.Path };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
        if (readiness is not null) problem.Extensions["readiness"] = readiness;
        return problem;
    }
}
