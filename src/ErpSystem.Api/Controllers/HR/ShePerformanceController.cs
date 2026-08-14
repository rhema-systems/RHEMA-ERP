using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
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
    private readonly ISheKpiComputationService _kpiService;

    public ShePerformanceController(IShePerformanceService service, ISheKpiComputationService kpiService, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
        _kpiService = kpiService;
    }

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

    /// <summary>Corrects the reported figures. Refused with 422 once the snapshot is reviewed.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ShePerformanceSnapshotDto>> Update(Guid id, [FromBody] UpdateShePerformanceSnapshotDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
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

    // ── computed KPIs (slice 14) ─────────────────────────────────────────────

    /// <summary>Recomputes the snapshot's derivable figures from live data. Refused with 422 once reviewed.</summary>
    [HttpPost("{id:guid}/compute")]
    public async Task<ActionResult<ShePerformanceSnapshotDto>> Compute(Guid id)
        => Ok(await _kpiService.ComputeSnapshotAsync(id, UserId));

    /// <summary>What a snapshot for this period would compute, without persisting anything.
    /// Pass manHours to see the frequency rates (LTIFR/TRIR/near-miss).</summary>
    [HttpGet("compute/preview")]
    public async Task<ActionResult<SheComputedKpisDto>> Preview(
        [FromQuery] SheSnapshotPeriodType periodType, [FromQuery] int year,
        [FromQuery] int? periodNumber, [FromQuery] Guid? locationId, [FromQuery] long manHours = 0)
        => Ok(await _kpiService.PreviewAsync(periodType, year, periodNumber, locationId, manHours));

    /// <summary>Per-organization-unit compliance for a period (FR-SHE-230).</summary>
    [HttpGet("kpis/departmental")]
    public async Task<ActionResult<IEnumerable<SheDepartmentalComplianceDto>>> Departmental(
        [FromQuery] SheSnapshotPeriodType periodType, [FromQuery] int year, [FromQuery] int? periodNumber)
        => Ok(await _kpiService.GetDepartmentalComplianceAsync(periodType, year, periodNumber));

    /// <summary>Contractor SHE ranking for a period (FR-CON-001), best average inspection score first.</summary>
    [HttpGet("kpis/contractor-ranking")]
    public async Task<ActionResult<IEnumerable<SheContractorRankingDto>>> ContractorRanking(
        [FromQuery] SheSnapshotPeriodType periodType, [FromQuery] int year, [FromQuery] int? periodNumber)
        => Ok(await _kpiService.GetContractorRankingAsync(periodType, year, periodNumber));

    /// <summary>5×5 likelihood × severity counts over the active hazard register (FR-SHE-232).</summary>
    [HttpGet("kpis/hazard-heatmap")]
    public async Task<ActionResult<SheHazardHeatmapDto>> HazardHeatmap()
        => Ok(await _kpiService.GetHazardHeatmapAsync());
}
