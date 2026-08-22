using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The trade-union register and the collective bargaining agreements negotiated with each.
/// </summary>
/// <remarks>
/// <para><b>Reads are open to any authenticated user; writes are SuperAdmin / TenantAdmin / HR.</b>
/// The union a role falls under is printed on the job description every employee can read, and a
/// collective agreement is published to the members it binds — gating the reads would be gating the
/// noticeboard. Which unions exist, and what has been agreed with them, is master data.</para>
///
/// <para>⚠ Until areas 19-23 slice 6 this controller carried a bare <c>[Authorize]</c>, so any
/// authenticated employee could create, rename or delete a union and its agreements.</para>
///
/// <para>⚠ It also caught nothing. <c>UnionService</c> throws <c>ArgumentException</c> for a missing
/// record and <c>InvalidOperationException</c> for every business rule — a duplicate code, an
/// agreement that expires before it starts, a union that still has agreements — and all of them
/// arrived as a bare 500 with no body. The same mute-rule shape slice 3 fixed on
/// <c>OrganizationUnitController</c>; a rule that fires correctly but cannot say why leaves the user
/// staring at a server error.</para>
/// </remarks>
[ApiController]
[Route("api/hr/unions")]
[Authorize]
public class UnionController : ControllerBase
{
    private const string WriteRoles =
        Constants.Roles.SuperAdmin + "," + Constants.Roles.TenantAdmin + "," + Constants.Roles.Hr;

    private readonly IUnionService _unionService;
    private readonly ILogger<UnionController> _logger;

    public UnionController(IUnionService unionService, ILogger<UnionController> logger)
    {
        _unionService = unionService;
        _logger = logger;
    }

    /// <summary>
    /// One error contract for every action here, so a rule can explain itself.
    /// </summary>
    /// <remarks>
    /// <c>ArgumentException</c> is the service's "not found", <c>InvalidOperationException</c> its
    /// "you may not do that", and <c>UnauthorizedAccessException</c> its cross-tenant refusal. Each
    /// carries the sentence the service wrote; the catch-all keeps its detail out of the response and
    /// puts it in the log.
    /// </remarks>
    private async Task<IActionResult> RunAsync<T>(Func<Task<T>> action, string what)
    {
        try
        {
            return Ok(await action());
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error {What}", what);
            return StatusCode(500, $"An error occurred while {what}.");
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UnionDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAll()
        => RunAsync(() => _unionService.GetAllAsync(), "retrieving unions");

    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<UnionDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetActive()
        => RunAsync(() => _unionService.GetActiveAsync(), "retrieving active unions");

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UnionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(Guid id)
        => RunAsync(() => _unionService.GetByIdAsync(id), "retrieving the union");

    [HttpPost]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(UnionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUnionDto dto)
    {
        try
        {
            var created = await _unionService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating a union");
            return StatusCode(500, "An error occurred while creating the union.");
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(UnionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateUnionDto dto)
    {
        if (id != dto.Id)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "The id in the route does not match the id in the body." }));

        return RunAsync(() => _unionService.UpdateAsync(dto), "updating the union");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _unionService.DeleteAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // The union still has agreements. The message names how many.
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting union {Id}", id);
            return StatusCode(500, "An error occurred while deleting the union.");
        }
    }

    #region Collective Bargaining Agreements

    [HttpPost("{unionId:guid}/agreements")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(CollectiveBargainingAgreementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAgreement(Guid unionId, [FromBody] CreateCollectiveBargainingAgreementDto dto)
    {
        // The route owns the union, not the body — a body that disagreed used to win silently.
        dto.UnionId = unionId;
        try
        {
            var created = await _unionService.AddAgreementAsync(dto);
            return CreatedAtAction(nameof(GetAgreements), new { unionId }, created);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding an agreement to union {UnionId}", unionId);
            return StatusCode(500, "An error occurred while adding the agreement.");
        }
    }

    [HttpGet("{unionId:guid}/agreements")]
    [ProducesResponseType(typeof(IEnumerable<CollectiveBargainingAgreementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetAgreements(Guid unionId)
        => RunAsync(() => _unionService.GetAgreementsAsync(unionId), "retrieving the union's agreements");

    [HttpPut("agreements/{id:guid}")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(CollectiveBargainingAgreementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateAgreement(Guid id, [FromBody] UpdateCollectiveBargainingAgreementDto dto)
    {
        if (id != dto.Id)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "The id in the route does not match the id in the body." }));

        return RunAsync(() => _unionService.UpdateAgreementAsync(dto), "updating the agreement");
    }

    [HttpDelete("agreements/{id:guid}")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAgreement(Guid id)
    {
        try
        {
            await _unionService.DeleteAgreementAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting agreement {Id}", id);
            return StatusCode(500, "An error occurred while deleting the agreement.");
        }
    }

    #endregion
}
