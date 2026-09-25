using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize]
[ApiController]
[Route("api/sales/collections")]
public class CollectionController : ControllerBase
{
    private readonly ICollectionService _collectionService;

    public CollectionController(ICollectionService collectionService)
    {
        _collectionService = collectionService;
    }

    // ── Collection Activities ──

    [HttpGet("activities")]
    public async Task<IActionResult> GetActivities(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] string? activityType = null, [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] Guid? assignedToId = null,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var result = await _collectionService.GetActivitiesAsync(page, pageSize, search, status, activityType, businessPartnerId, assignedToId, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("activities/{id:guid}")]
    public async Task<IActionResult> GetActivity(Guid id)
    {
        var activity = await _collectionService.GetActivityByIdAsync(id);
        return activity == null ? NotFound() : Ok(activity);
    }

    [HttpPost("activities")]
    public async Task<IActionResult> CreateActivity([FromBody] CreateCollectionActivityDto dto)
    {
        var activity = await _collectionService.CreateActivityAsync(dto);
        return CreatedAtAction(nameof(GetActivity), new { id = activity.Id }, activity);
    }

    [HttpPut("activities/{id:guid}")]
    public async Task<IActionResult> UpdateActivity(Guid id, [FromBody] UpdateCollectionActivityDto dto)
    {
        var activity = await _collectionService.UpdateActivityAsync(id, dto);
        return Ok(activity);
    }

    [HttpGet("activities/overdue")]
    public async Task<IActionResult> GetOverdueFollowUps([FromQuery] int daysOverdue = 0, [FromQuery] Guid? assignedToId = null)
    {
        var activities = await _collectionService.GetOverdueFollowUpsAsync(daysOverdue, assignedToId);
        return Ok(activities);
    }

    // ── Payment Plans ──

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var result = await _collectionService.GetPlansAsync(page, pageSize, search, status, businessPartnerId, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("plans/{id:guid}")]
    public async Task<IActionResult> GetPlan(Guid id)
    {
        var plan = await _collectionService.GetPlanByIdAsync(id);
        return plan == null ? NotFound() : Ok(plan);
    }

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePaymentPlanDto dto)
    {
        var plan = await _collectionService.CreatePlanAsync(dto);
        return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, plan);
    }

    [HttpPost("plans/{id:guid}/approve")]
    public async Task<IActionResult> ApprovePlan(Guid id) => Ok(await _collectionService.ApprovePlanAsync(id));

    [HttpPost("plans/{id:guid}/cancel")]
    public async Task<IActionResult> CancelPlan(Guid id, [FromQuery] string? reason = null)
        => Ok(await _collectionService.CancelPlanAsync(id, reason));

    [HttpPost("plans/{planId:guid}/installments/{installmentId:guid}/pay")]
    public async Task<IActionResult> RecordPayment(Guid planId, Guid installmentId, [FromBody] RecordInstallmentPaymentDto dto)
        => Ok(await _collectionService.RecordInstallmentPaymentAsync(planId, installmentId, dto));

    [HttpGet("installments/overdue")]
    public async Task<IActionResult> GetOverdueInstallments([FromQuery] Guid? businessPartnerId = null)
    {
        var installments = await _collectionService.GetOverdueInstallmentsAsync(businessPartnerId);
        return Ok(installments);
    }
}
