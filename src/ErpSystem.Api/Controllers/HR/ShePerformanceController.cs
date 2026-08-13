using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/performance")]
[SafetyBusinessRules]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
public class ShePerformanceController : SheApiControllerBase
{
    private readonly IShePerformanceService _service;

    public ShePerformanceController(IShePerformanceService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShePerformanceSnapshotDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{snapshotNumber}")]
    public async Task<ActionResult<ShePerformanceSnapshotDto?>> GetByNumber(string snapshotNumber)
        => Ok(await _service.GetByNumberAsync(snapshotNumber));

    [HttpGet("year/{year:int}")]
    public async Task<ActionResult<IEnumerable<ShePerformanceSnapshotSummaryDto>>> GetByYear(int year)
        => Ok(await _service.GetByYearAsync(year));

    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<ShePerformanceSnapshotSummaryDto>>> GetByLocation(Guid locationId)
        => Ok(await _service.GetByLocationAsync(locationId));

    [HttpGet("latest")]
    public async Task<ActionResult<ShePerformanceSnapshotDto?>> GetLatest()
        => Ok(await _service.GetLatestAsync());

    [HttpPost]
    public async Task<ActionResult<ShePerformanceSnapshotDto>> Create([FromBody] CreateShePerformanceSnapshotDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewShePerformanceSnapshotDto dto)
    {
        dto.SnapshotId = id;
        await _service.ReviewAsync(dto, UserId);
        return Ok(new { message = "Performance snapshot reviewed." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
