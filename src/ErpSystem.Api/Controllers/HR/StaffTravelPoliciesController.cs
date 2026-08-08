using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/policies")]
[Authorize]
public class StaffTravelPoliciesController : ControllerBase
{
    private readonly IStaffTravelPolicyService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffTravelPoliciesController(IStaffTravelPolicyService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private (Guid tenantId, Guid userId)? ResolveContext()
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null || userId is null) return null;
        return (tenantId.Value, userId.Value);
    }

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

    [HttpPost]
    public async Task<ActionResult<StaffTravelPolicyDto>> Create([FromBody] CreateStaffTravelPolicyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreatePolicyAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffTravelPolicyDto>> Update(Guid id, [FromBody] UpdateStaffTravelPolicyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdatePolicyAsync(dto, ctx.Value.userId));
    }

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

    [HttpPost("{policyId:guid}/rules")]
    public async Task<ActionResult<StaffTravelPolicyRuleDto>> AddRule(Guid policyId, [FromBody] CreateStaffTravelPolicyRuleDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        dto.PolicyId = policyId;
        return Ok(await _service.AddRuleAsync(dto, ctx.Value.tenantId, ctx.Value.userId));
    }

    [HttpPut("rules/{ruleId:guid}")]
    public async Task<ActionResult<StaffTravelPolicyRuleDto>> UpdateRule(Guid ruleId, [FromBody] UpdateStaffTravelPolicyRuleDto dto)
    {
        if (ruleId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateRuleAsync(dto, ctx.Value.userId));
    }

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

    [HttpPost("exceptions")]
    public async Task<ActionResult<StaffTravelPolicyExceptionDto>> CreateException([FromBody] CreateStaffTravelPolicyExceptionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.CreateExceptionAsync(dto, ctx.Value.tenantId, ctx.Value.userId));
    }

    [HttpPost("exceptions/{id:guid}/decide")]
    public async Task<IActionResult> DecideException(Guid id, [FromBody] DecideStaffTravelPolicyExceptionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = _currentUser.EmployeeId;
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        dto.ExceptionId = id;
        dto.ApprovedById = userId.Value;
        await _service.DecideExceptionAsync(dto);
        return Ok(new { message = "Policy exception decision recorded." });
    }

    // =========================================================================
    // VENDORS
    // =========================================================================

    [HttpGet("vendors")]
    public async Task<ActionResult<IEnumerable<StaffTravelVendorSummaryDto>>> GetAllVendors()
        => Ok(await _service.GetAllVendorsAsync());

    [HttpGet("vendors/active")]
    public async Task<ActionResult<IEnumerable<StaffTravelVendorSummaryDto>>> GetActiveVendors()
        => Ok(await _service.GetActiveVendorsAsync());

    [HttpGet("vendors/type/{vendorType}")]
    public async Task<ActionResult<IEnumerable<StaffTravelVendorSummaryDto>>> GetVendorsByType(TravelVendorType vendorType)
        => Ok(await _service.GetVendorsByTypeAsync(vendorType));

    [HttpGet("vendors/preferred")]
    public async Task<ActionResult<IEnumerable<StaffTravelVendorSummaryDto>>> GetPreferredVendors([FromQuery] TravelVendorType? vendorType = null)
        => Ok(await _service.GetPreferredVendorsAsync(vendorType));

    [HttpGet("vendors/{id:guid}")]
    public async Task<ActionResult<StaffTravelVendorDto>> GetVendorById(Guid id)
        => Ok(await _service.GetVendorByIdAsync(id));

    [HttpGet("vendors/code/{vendorCode}")]
    public async Task<ActionResult<StaffTravelVendorDto?>> GetVendorByCode(string vendorCode)
        => Ok(await _service.GetVendorByCodeAsync(vendorCode));

    [HttpPost("vendors")]
    public async Task<ActionResult<StaffTravelVendorDto>> CreateVendor([FromBody] CreateStaffTravelVendorDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateVendorAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetVendorById), new { id = created.Id }, created);
    }

    [HttpPut("vendors/{id:guid}")]
    public async Task<ActionResult<StaffTravelVendorDto>> UpdateVendor(Guid id, [FromBody] UpdateStaffTravelVendorDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateVendorAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("vendors/{id:guid}")]
    public async Task<IActionResult> DeleteVendor(Guid id)
    {
        await _service.DeleteVendorAsync(id);
        return NoContent();
    }
}
