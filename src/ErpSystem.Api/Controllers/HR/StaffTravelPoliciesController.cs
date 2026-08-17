using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using ErpSystem.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/policies")]
[StaffTravelBusinessRules]
[Authorize(Policy = HrPermissions.TravelReadPolicy)]
public class StaffTravelPoliciesController : HrControllerBase
{
    private readonly IStaffTravelPolicyService _service;

    public StaffTravelPoliciesController(IStaffTravelPolicyService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    /// <summary>
    /// Tenant + platform user id for audit fields. Deliberately does not require an employee
    /// link — see <see cref="HrControllerBase"/>.
    /// </summary>
    private (Guid tenantId, Guid userId)? ResolveContext()
        => TryGetWriteContext(out var tenantId, out var userId) is null ? (tenantId, userId) : null;

    // =========================================================================
    // POLICIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffTravelPolicySummaryDto>>> GetAll()
        => Ok(await _service.GetAllPoliciesAsync());

    [HttpGet("current")]
    public async Task<ActionResult<IEnumerable<StaffTravelPolicySummaryDto>>> GetCurrent()
        => Ok(await _service.GetCurrentPoliciesAsync());

    [HttpGet("applicable")]
    public async Task<ActionResult<IEnumerable<StaffTravelPolicySummaryDto>>> GetApplicable(
        [FromQuery] DateOnly onDate, [FromQuery] Guid? staffLevelId = null, [FromQuery] Guid? organizationUnitId = null)
        => Ok(await _service.GetApplicablePoliciesAsync(staffLevelId, organizationUnitId, onDate));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffTravelPolicyDto>> GetById(Guid id)
        => Ok(await _service.GetPolicyByIdAsync(id));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<StaffTravelPolicyDto>> Create([FromBody] CreateStaffTravelPolicyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreatePolicyAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffTravelPolicyDto>> Update(Guid id, [FromBody] UpdateStaffTravelPolicyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdatePolicyAsync(dto, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeletePolicyAsync(id);
        return NoContent();
    }

    // =========================================================================
    // POLICY RULES
    // =========================================================================

    [HttpGet("{policyId:guid}/rules")]
    public async Task<ActionResult<IEnumerable<StaffTravelPolicyRuleDto>>> GetRules(Guid policyId)
        => Ok(await _service.GetRulesAsync(policyId));

    [HttpGet("{policyId:guid}/rules/active")]
    public async Task<ActionResult<IEnumerable<StaffTravelPolicyRuleDto>>> GetActiveRules(Guid policyId)
        => Ok(await _service.GetActiveRulesAsync(policyId));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{policyId:guid}/rules")]
    public async Task<ActionResult<StaffTravelPolicyRuleDto>> AddRule(Guid policyId, [FromBody] CreateStaffTravelPolicyRuleDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        dto.PolicyId = policyId;
        return Ok(await _service.AddRuleAsync(dto, ctx.Value.tenantId, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("rules/{ruleId:guid}")]
    public async Task<ActionResult<StaffTravelPolicyRuleDto>> UpdateRule(Guid ruleId, [FromBody] UpdateStaffTravelPolicyRuleDto dto)
    {
        if (ruleId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateRuleAsync(dto, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("rules/{ruleId:guid}")]
    public async Task<IActionResult> DeleteRule(Guid ruleId)
    {
        await _service.DeleteRuleAsync(ruleId);
        return NoContent();
    }

    // =========================================================================
    // POLICY EXCEPTIONS
    // =========================================================================

    [HttpGet("exceptions/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelPolicyExceptionDto>>> GetExceptionsByRequest(Guid requestId)
        => Ok(await _service.GetExceptionsByRequestAsync(requestId));

    [HttpGet("exceptions/pending")]
    public async Task<ActionResult<IEnumerable<StaffTravelPolicyExceptionDto>>> GetPendingExceptions()
        => Ok(await _service.GetPendingExceptionsAsync());

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("exceptions")]
    public async Task<ActionResult<StaffTravelPolicyExceptionDto>> CreateException([FromBody] CreateStaffTravelPolicyExceptionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.CreateExceptionAsync(dto, ctx.Value.tenantId, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("exceptions/{id:guid}/decide")]
    public async Task<IActionResult> DecideException(Guid id, [FromBody] DecideStaffTravelPolicyExceptionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        dto.ExceptionId = id;
        dto.ApprovedById = userId;
        await _service.DecideExceptionAsync(dto);
        return Ok(new { message = "Policy exception decision recorded." });
    }

    // =========================================================================
    // VENDORS — retired in slice 3
    // =========================================================================
    //
    // Travel vendors were a second supplier master: VendorCode, VendorName, contact, account
    // number, contract dates, IsPreferred, Rating and PaymentTerms as free text — all of which
    // Procurement's Supplier already models, with SupplierPerformanceMetric behind the rating and
    // PaymentTerm as a real entity rather than a string. An airline paid through travel and the
    // same airline paid through procurement must not be two records that can disagree.
    //
    // The six VendorId columns across flights, hotels, ground transport, car rentals, visa
    // applications and insurance now point at Suppliers. Onboard a travel vendor through
    // Procurement; there is deliberately no travel-side create.

}
