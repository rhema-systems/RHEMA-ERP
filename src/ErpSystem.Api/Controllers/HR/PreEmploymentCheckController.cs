using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/pre-employment-checks")]
[Authorize]
public class PreEmploymentCheckController : ControllerBase
{
    private readonly IPreEmploymentCheckService _service;
    private readonly ICurrentUserService _currentUser;

    public PreEmploymentCheckController(IPreEmploymentCheckService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // CHECK QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PreEmploymentCheckDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("offer/{offerId:guid}")]
    public async Task<ActionResult<PreEmploymentCheckDto?>> GetByOffer(Guid offerId)
        => Ok(await _service.GetByOfferIdAsync(offerId));

    [HttpGet("{id:guid}/with-items")]
    public async Task<ActionResult<PreEmploymentCheckDetailDto>> GetWithItems(Guid id)
        => Ok(await _service.GetWithItemsAsync(id));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckDto>>> GetByStatus(
        PreEmploymentCheckStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    // =========================================================================
    // CHECK CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<PreEmploymentCheckDto>> Create([FromBody] CreatePreEmploymentCheckDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteCheck(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CompleteCheckAsync(id, employeeId.Value);
        return Ok(new { message = "Pre-employment check completed." });
    }

    // =========================================================================
    // CHECK ITEMS
    // =========================================================================

    [HttpGet("{checkId:guid}/items")]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckItemDto>>> GetItems(Guid checkId)
        => Ok(await _service.GetItemsAsync(checkId));

    [HttpGet("{checkId:guid}/items/blocking-failures")]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckItemDto>>> GetBlockingFailures(Guid checkId)
        => Ok(await _service.GetBlockingFailuresAsync(checkId));

    [HttpGet("{checkId:guid}/items/mandatory")]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckItemDto>>> GetMandatoryItems(Guid checkId)
        => Ok(await _service.GetMandatoryItemsAsync(checkId));

    [HttpGet("items/status/{status}")]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckItemDto>>> GetItemsByStatus(
        CheckItemStatus status, [FromQuery] Guid? checkId = null)
        => Ok(await _service.GetItemsByStatusAsync(status, checkId));

    [HttpPost("{checkId:guid}/items")]
    public async Task<ActionResult<PreEmploymentCheckItemDto>> AddItem(
        Guid checkId, [FromBody] CreatePreEmploymentCheckItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddItemAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<PreEmploymentCheckItemDto>> UpdateItem(
        Guid itemId, [FromBody] UpdatePreEmploymentCheckItemDto dto)
    {
        if (itemId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateItemAsync(dto, employeeId.Value));
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid itemId)
    {
        await _service.DeleteItemAsync(itemId);
        return NoContent();
    }

    // =========================================================================
    // REFERENCE RESPONSES
    // =========================================================================

    [HttpGet("items/{checkItemId:guid}/reference-responses")]
    public async Task<ActionResult<IEnumerable<ReferenceCheckResponseDto>>> GetReferenceResponses(
        Guid checkItemId)
        => Ok(await _service.GetReferenceResponsesAsync(checkItemId));

    [HttpGet("reference-responses/referee/{refereeId:guid}")]
    public async Task<ActionResult<IEnumerable<ReferenceCheckResponseDto>>> GetReferenceResponsesByReferee(
        Guid refereeId)
        => Ok(await _service.GetReferenceResponsesByRefereeAsync(refereeId));

    [HttpPost("items/{checkItemId:guid}/reference-responses")]
    public async Task<ActionResult<ReferenceCheckResponseDto>> AddReferenceResponse(
        Guid checkItemId, [FromBody] CreateReferenceCheckResponseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddReferenceResponseAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("reference-responses/{id:guid}")]
    public async Task<ActionResult<ReferenceCheckResponseDto>> UpdateReferenceResponse(
        Guid id, [FromBody] UpdateReferenceCheckResponseDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateReferenceResponseAsync(dto, employeeId.Value));
    }

    [HttpDelete("reference-responses/{id:guid}")]
    public async Task<IActionResult> DeleteReferenceResponse(Guid id)
    {
        await _service.DeleteReferenceResponseAsync(id);
        return NoContent();
    }

    // =========================================================================
    // TEMPLATE APPLICATION

    /// <summary>
    /// Apply a pre-employment check template to an existing check, seeding
    /// its items from the template. Existing items of the same CheckType are
    /// skipped unless overwriteExisting is true.
    /// </summary>
    [HttpPost("{id:guid}/apply-template")]
    public async Task<ActionResult<PreEmploymentCheckDetailDto>> ApplyTemplate(
        Guid id,
        [FromBody] ApplyTemplateDto dto)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return Unauthorized();

        var result = await _service.ApplyTemplateAsync(id, dto.TemplateId, employeeId.Value, dto.OverwriteExisting);
        return Ok(result);
    }
}
