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
[Route("api/staff-travel/finance")]
[StaffTravelBusinessRules]
[Authorize(Policy = HrPermissions.TravelReadPolicy)]
public class StaffTravelFinanceController : HrControllerBase
{
    private readonly IStaffTravelFinanceService _service;

    public StaffTravelFinanceController(IStaffTravelFinanceService service, ICurrentUserService currentUser)
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
    // BUDGET
    // =========================================================================

    [HttpGet("budgets/request/{requestId:guid}")]
    public async Task<ActionResult<StaffTravelBudgetDto?>> GetBudgetByRequest(Guid requestId)
        => Ok(await _service.GetBudgetByRequestAsync(requestId));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("budgets")]
    public async Task<ActionResult<StaffTravelBudgetDto>> CreateBudget([FromBody] CreateStaffTravelBudgetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.CreateBudgetAsync(dto, ctx.Value.tenantId, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("budgets/{id:guid}")]
    public async Task<ActionResult<StaffTravelBudgetDto>> UpdateBudget(Guid id, [FromBody] UpdateStaffTravelBudgetDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateBudgetAsync(dto, ctx.Value.userId));
    }

    /// <summary>A travel administrator's verb (lane 3, B10): the budget's approver is an Employee FK, and is never the traveller.</summary>
    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpPost("budgets/{id:guid}/approve")]
    public async Task<ActionResult<StaffTravelBudgetDto>> ApproveBudget(Guid id)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Approving a travel budget") is { } contextError) return contextError;

        return Ok(await _service.ApproveBudgetAsync(id, employeeId));
    }

    // =========================================================================
    // EXPENSE CLAIMS
    // =========================================================================

    [HttpGet("claims")]
    public async Task<ActionResult<IEnumerable<StaffTravelExpenseClaimSummaryDto>>> GetAllClaims()
        => Ok(await _service.GetAllClaimsAsync());

    [HttpGet("claims/{id:guid}")]
    public async Task<ActionResult<StaffTravelExpenseClaimDto>> GetClaimById(Guid id)
        => Ok(await _service.GetClaimByIdAsync(id));

    [HttpGet("claims/number/{claimNumber}")]
    public async Task<ActionResult<StaffTravelExpenseClaimDto?>> GetClaimByNumber(string claimNumber)
        => Ok(await _service.GetClaimByNumberAsync(claimNumber));

    [HttpGet("claims/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelExpenseClaimSummaryDto>>> GetClaimsByRequest(Guid requestId)
        => Ok(await _service.GetClaimsByRequestAsync(requestId));

    [HttpGet("claims/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelExpenseClaimSummaryDto>>> GetClaimsByEmployee(Guid employeeId)
        => Ok(await _service.GetClaimsByEmployeeAsync(employeeId));

    [HttpGet("claims/status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffTravelExpenseClaimSummaryDto>>> GetClaimsByStatus(TravelClaimStatus status)
        => Ok(await _service.GetClaimsByStatusAsync(status));

    [HttpGet("claims/unpaid-approved")]
    public async Task<ActionResult<IEnumerable<StaffTravelExpenseClaimSummaryDto>>> GetUnpaidApprovedClaims()
        => Ok(await _service.GetUnpaidApprovedClaimsAsync());

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("claims")]
    public async Task<ActionResult<StaffTravelExpenseClaimDto>> CreateClaim([FromBody] CreateStaffTravelExpenseClaimDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateClaimAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetClaimById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("claims/{id:guid}")]
    public async Task<ActionResult<StaffTravelExpenseClaimDto>> UpdateClaim(Guid id, [FromBody] UpdateStaffTravelExpenseClaimDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateClaimAsync(dto, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("claims/{id:guid}")]
    public async Task<IActionResult> DeleteClaim(Guid id)
    {
        await _service.DeleteClaimAsync(id);
        return NoContent();
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("claims/{id:guid}/submit")]
    public async Task<IActionResult> SubmitClaim(Guid id)
    {
        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        await _service.SubmitClaimAsync(id, userId);
        return Ok(new { message = "Expense claim submitted." });
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("claims/{id:guid}/review")]
    public async Task<IActionResult> ReviewClaim(Guid id, [FromBody] ReviewStaffTravelExpenseClaimDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        // FinanceReviewedById is an Employee FK — this is one of the travel writes that genuinely
        // needs the caller's employee link.
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reviewing an expense claim") is { } contextError) return contextError;

        dto.ClaimId = id;
        await _service.ReviewClaimAsync(dto, employeeId);
        return Ok(new { message = "Expense claim reviewed." });
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("claims/{id:guid}/pay")]
    public async Task<IActionResult> PayClaim(Guid id, [FromBody] PayStaffTravelExpenseClaimDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        // PaidById is an Employee FK, and the two-person rule compares the payer with the claimant and the
        // reviewers (lane 3, D-2) — so paying, like reviewing, needs the caller's employee link.
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Paying an expense claim") is { } contextError) return contextError;

        dto.ClaimId = id;
        await _service.PayClaimAsync(dto, employeeId);
        return Ok(new { message = "Expense claim paid." });
    }

    /// <summary>
    /// A travel administrator's verb (lane 3, T-39): the payment's journal reversed, its advance settlement undone, the
    /// claim back to approved. Never the claimant's or the payer's own — so it needs the caller's employee link.
    /// </summary>
    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpPost("claims/{id:guid}/void-payment")]
    public async Task<IActionResult> VoidClaimPayment(Guid id, [FromBody] VoidStaffTravelClaimPaymentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Voiding a claim's payment") is { } contextError) return contextError;

        await _service.VoidClaimPaymentAsync(id, dto, employeeId);
        return Ok(new { message = "Payment voided." });
    }

    // ---- Expense claim lines -----------------------------------------------

    [HttpGet("claims/{claimId:guid}/lines")]
    public async Task<ActionResult<IEnumerable<StaffTravelExpenseClaimLineDto>>> GetClaimLines(Guid claimId)
        => Ok(await _service.GetClaimLinesAsync(claimId));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("claims/{claimId:guid}/lines")]
    public async Task<ActionResult<StaffTravelExpenseClaimLineDto>> AddClaimLine(Guid claimId, [FromBody] CreateStaffTravelExpenseClaimLineDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        dto.StaffTravelExpenseClaimId = claimId;
        return Ok(await _service.AddClaimLineAsync(dto, ctx.Value.tenantId, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("lines/{lineId:guid}")]
    public async Task<ActionResult<StaffTravelExpenseClaimLineDto>> UpdateClaimLine(Guid lineId, [FromBody] UpdateStaffTravelExpenseClaimLineDto dto)
    {
        if (lineId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateClaimLineAsync(dto, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("lines/{lineId:guid}/review")]
    public async Task<IActionResult> ReviewClaimLine(Guid lineId, [FromBody] ReviewStaffTravelExpenseClaimLineDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        // ReviewedById is an Employee FK — this is one of the travel writes that genuinely
        // needs the caller's employee link.
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reviewing a claim line") is { } contextError) return contextError;

        dto.LineId = lineId;
        await _service.ReviewClaimLineAsync(dto, employeeId);
        return Ok(new { message = "Expense line reviewed." });
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("lines/{lineId:guid}")]
    public async Task<IActionResult> DeleteClaimLine(Guid lineId)
    {
        await _service.DeleteClaimLineAsync(lineId);
        return NoContent();
    }

    // =========================================================================
    // ADVANCES
    // =========================================================================

    [HttpGet("advances")]
    public async Task<ActionResult<IEnumerable<StaffTravelAdvanceSummaryDto>>> GetAllAdvances()
        => Ok(await _service.GetAllAdvancesAsync());

    [HttpGet("advances/status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffTravelAdvanceSummaryDto>>> GetAdvancesByStatus(TravelAdvanceStatus status)
        => Ok(await _service.GetAdvancesByStatusAsync(status));

    [HttpGet("advances/{id:guid}")]
    public async Task<ActionResult<StaffTravelAdvanceDto>> GetAdvanceById(Guid id)
        => Ok(await _service.GetAdvanceByIdAsync(id));

    [HttpGet("advances/number/{advanceNumber}")]
    public async Task<ActionResult<StaffTravelAdvanceDto?>> GetAdvanceByNumber(string advanceNumber)
        => Ok(await _service.GetAdvanceByNumberAsync(advanceNumber));

    [HttpGet("advances/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelAdvanceSummaryDto>>> GetAdvancesByRequest(Guid requestId)
        => Ok(await _service.GetAdvancesByRequestAsync(requestId));

    [HttpGet("advances/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelAdvanceSummaryDto>>> GetAdvancesByEmployee(Guid employeeId)
        => Ok(await _service.GetAdvancesByEmployeeAsync(employeeId));

    [HttpGet("advances/employee/{employeeId:guid}/outstanding")]
    public async Task<ActionResult<IEnumerable<StaffTravelAdvanceSummaryDto>>> GetOutstandingAdvancesByEmployee(Guid employeeId)
        => Ok(await _service.GetOutstandingAdvancesByEmployeeAsync(employeeId));

    [HttpGet("advances/overdue-settlements")]
    public async Task<ActionResult<IEnumerable<StaffTravelAdvanceSummaryDto>>> GetOverdueSettlements()
        => Ok(await _service.GetOverdueSettlementsAsync());

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("advances")]
    public async Task<ActionResult<StaffTravelAdvanceDto>> CreateAdvance([FromBody] CreateStaffTravelAdvanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateAdvanceAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetAdvanceById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("advances/{id:guid}")]
    public async Task<ActionResult<StaffTravelAdvanceDto>> UpdateAdvance(Guid id, [FromBody] UpdateStaffTravelAdvanceDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateAdvanceAsync(dto, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("advances/{id:guid}")]
    public async Task<IActionResult> DeleteAdvance(Guid id)
    {
        await _service.DeleteAdvanceAsync(id);
        return NoContent();
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("advances/{id:guid}/approve")]
    public async Task<IActionResult> ApproveAdvance(Guid id, [FromBody] ApproveStaffTravelAdvanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        // ApprovedById is an Employee FK — this is one of the travel writes that genuinely
        // needs the caller's employee link.
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Approving a travel advance") is { } contextError) return contextError;

        dto.AdvanceId = id;
        await _service.ApproveAdvanceAsync(dto, employeeId);
        return Ok(new { message = "Advance approved." });
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("advances/{id:guid}/disburse")]
    public async Task<IActionResult> DisburseAdvance(Guid id, [FromBody] DisburseStaffTravelAdvanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        // DisbursedById is an Employee FK — this is one of the travel writes that genuinely
        // needs the caller's employee link.
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Disbursing a travel advance") is { } contextError) return contextError;

        dto.AdvanceId = id;
        await _service.DisburseAdvanceAsync(dto, employeeId);
        return Ok(new { message = "Advance disbursed." });
    }

    // Lane 3: an advance's other verbs. Each records an Employee actor, so each needs the caller's employee link;
    // the service refuses the traveller on their own advance (D-2).

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("advances/{id:guid}/reject")]
    public async Task<IActionResult> RejectAdvance(Guid id, [FromBody] DecideStaffTravelAdvanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Rejecting a travel advance") is { } contextError) return contextError;

        await _service.RejectAdvanceAsync(id, dto.Reason, employeeId);
        return Ok(new { message = "Advance rejected." });
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("advances/{id:guid}/cancel")]
    public async Task<IActionResult> CancelAdvance(Guid id, [FromBody] DecideStaffTravelAdvanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Cancelling a travel advance") is { } contextError) return contextError;

        await _service.CancelAdvanceAsync(id, dto.Reason, employeeId);
        return Ok(new { message = "Advance cancelled." });
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("advances/{id:guid}/refund")]
    public async Task<IActionResult> RecordAdvanceRefund(Guid id, [FromBody] RefundStaffTravelAdvanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Recording cash handed back on a travel advance") is { } contextError) return contextError;

        await _service.RecordAdvanceRefundAsync(id, dto, employeeId);
        return Ok(new { message = "Refund recorded." });
    }

    /// <summary>A travel administrator's verb: what is written off is money the organisation gives up.</summary>
    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpPost("advances/{id:guid}/write-off")]
    public async Task<IActionResult> WriteOffAdvance(Guid id, [FromBody] DecideStaffTravelAdvanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Writing off a travel advance") is { } contextError) return contextError;

        await _service.WriteOffAdvanceAsync(id, dto.Reason, employeeId);
        return Ok(new { message = "Advance written off." });
    }

    // =========================================================================
    // PER-DIEM RATES
    // =========================================================================

    [HttpGet("per-diem-rates/{id:guid}")]
    public async Task<ActionResult<StaffTravelPerDiemRateDto>> GetPerDiemRateById(Guid id)
        => Ok(await _service.GetPerDiemRateByIdAsync(id));

    [HttpGet("per-diem-rates/active")]
    public async Task<ActionResult<IEnumerable<StaffTravelPerDiemRateDto>>> GetActivePerDiemRates()
        => Ok(await _service.GetActivePerDiemRatesAsync());

    [HttpGet("per-diem-rates/country/{countryId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelPerDiemRateDto>>> GetPerDiemRatesByCountry(Guid countryId)
        => Ok(await _service.GetPerDiemRatesByCountryAsync(countryId));

    [HttpGet("per-diem-rates/effective")]
    public async Task<ActionResult<StaffTravelPerDiemRateDto?>> GetEffectivePerDiemRate(
        [FromQuery] Guid countryId, [FromQuery] DateOnly onDate, [FromQuery] string? city = null, [FromQuery] Guid? staffLevelId = null)
        => Ok(await _service.GetEffectivePerDiemRateAsync(countryId, city, staffLevelId, onDate));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("per-diem-rates")]
    public async Task<ActionResult<StaffTravelPerDiemRateDto>> CreatePerDiemRate([FromBody] CreateStaffTravelPerDiemRateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreatePerDiemRateAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetPerDiemRateById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("per-diem-rates/{id:guid}")]
    public async Task<ActionResult<StaffTravelPerDiemRateDto>> UpdatePerDiemRate(Guid id, [FromBody] UpdateStaffTravelPerDiemRateDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdatePerDiemRateAsync(dto, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("per-diem-rates/{id:guid}")]
    public async Task<IActionResult> DeletePerDiemRate(Guid id)
    {
        await _service.DeletePerDiemRateAsync(id);
        return NoContent();
    }
}
