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

    /// <summary>Approve a travel policy and put it in force.</summary>
    /// <remarks>
    /// <para><b>Admin-gated, deliberately.</b> A policy decides what everyone may spend on travel
    /// and its caps genuinely refuse bookings, so approving one is the same authority as
    /// authorising a booking above a cap — <c>HR.Travel.Admin</c>, which HR holds since the travel final
    /// closure's lane 4 (D-3); the policy's author does not approve it (C3). Before this endpoint existed nothing
    /// wrote <c>ApprovedById</c> at all, so every policy was unapproved and the field was decoration.</para>
    ///
    /// <para>Approving makes room among the versions for the same scope by date (O-4): an earlier one stays in
    /// force until the day before this one starts.</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<StaffTravelPolicyDto>> Approve(Guid id, CancellationToken ct)
    {
        // ApprovedById is an Employee FK, so this is one of the travel writes that genuinely needs
        // the caller's employee link — an unlinked administrator cannot sign a spending policy.
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Approving a travel policy") is { } contextError) return contextError;

        return Ok(await _service.ApprovePolicyAsync(id, employeeId, ct));
    }

    /// <summary>Stand an approved policy down so it stops capping bookings.</summary>
    /// <remarks>
    /// Admin-gated like approval — putting a rule in force and taking it out are the same
    /// authority. The policy stays approved; approval is a fact about the past and withdrawing does
    /// not unmake it. Without this, undoing an approval meant approving a replacement with an
    /// identical scope, or deleting the record of a rule that really did govern spending.
    /// </remarks>
    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<StaffTravelPolicyDto>> Withdraw(Guid id, CancellationToken ct)
        => Ok(await _service.WithdrawPolicyAsync(id, ct));

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

    /// <summary>
    /// Lane 4, C4: a travel administrator's act, as authorising a booking's breach is — it was Write, so whoever
    /// could raise an exception could grant it. The decision's time is the server's, and the requester does not decide.
    /// </summary>
    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpPost("exceptions/{id:guid}/decide")]
    public async Task<IActionResult> DecideException(Guid id, [FromBody] DecideStaffTravelPolicyExceptionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        // ApprovedById is an Employee FK — who granted an exception to travel policy is a person.
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Deciding a travel policy exception") is { } contextError) return contextError;

        dto.ExceptionId = id;
        await _service.DecideExceptionAsync(dto, employeeId);
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
