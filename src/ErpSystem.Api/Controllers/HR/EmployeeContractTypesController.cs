using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The tenant's contract-kind vocabulary — Permanent, Contract, Fixed Term, National Service,
/// Internship, Casual, Consultancy at TDC — each with the duration that kind of engagement runs for.
/// </summary>
/// <remarks>
/// <para>⚠ NOT the <c>EmploymentType</c> enum. The enum is the system's fixed set and code branches
/// on it (the probation rule is keyed to <c>Permanent</c>; the staff-number register is chosen by
/// it). This is the organisation's own naming, and its <c>Duration</c> is what gives a fixed-term
/// contract's end date a default.</para>
///
/// <para>The table was seeded with seven TDC rows in 2026 and then reachable from nowhere — no DTO,
/// no service, no controller, no screen. Surfaced in round-2 lane D1 (question Q-4), which chose to
/// surface rather than delete the seed precisely because <c>Duration</c> is load-bearing.</para>
///
/// <para>Gating follows every other People Reference Data lookup: reading is part of filling in an
/// employee's contract, so <c>HR.Employee.Read</c>; changing the vocabulary is
/// <c>HR.Employee.Write</c>.</para>
/// </remarks>
[ApiController]
[Route("api/hr/contract-types")]
[Authorize(Policy = "InternalOnly")]
public class EmployeeContractTypesController : ControllerBase
{
    private readonly IEmployeeContractTypeService _service;
    private readonly ILogger<EmployeeContractTypesController> _logger;

    public EmployeeContractTypesController(
        IEmployeeContractTypeService service,
        ILogger<EmployeeContractTypesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>The contract kinds. Pass <c>activeOnly</c> for a picker; omit it for the master screen.</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeContractTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeContractTypeDto>>> GetAll(
        [FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(activeOnly, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeContractTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractTypeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeContractTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeContractTypeDto>> Create(
        [FromBody] CreateEmployeeContractTypeDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeContractTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractTypeDto>> Update(
        Guid id, [FromBody] UpdateEmployeeContractTypeDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, dto, cancellationToken));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Retires a contract kind. There is no delete — contracts name their kind by FK.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeactivateAsync(id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }
}
