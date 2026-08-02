using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/medical-benefit-schemes")]
// Medical records are special-category personal data. This controller previously carried a
// bare [Authorize], so any authenticated employee could read them. Read is the class-level
// floor; write and delete are tightened per action.
[Authorize(Policy = HrPermissions.MedicalReadPolicy)]
public class MedicalBenefitSchemesController : MedicalControllerBase
{
    private readonly IMedicalBenefitSchemeService _service;

    public MedicalBenefitSchemesController(IMedicalBenefitSchemeService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    // =========================================================================
    // SCHEMES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MedicalBenefitSchemeSummaryDto>>> GetAll(
        [FromQuery] bool onlyActive = false,
        CancellationToken ct = default)
        => Ok(onlyActive
            ? await _service.GetActiveSchemesAsync(ct)
            : await _service.GetAllSchemesAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MedicalBenefitSchemeDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetSchemeByIdAsync(id, ct));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<MedicalBenefitSchemeDetailDto>> GetWithTiers(Guid id, CancellationToken ct)
        => Ok(await _service.GetSchemeWithTiersAsync(id, ct));

    [HttpGet("code/{code}")]
    public async Task<ActionResult<MedicalBenefitSchemeDto?>> GetByCode(string code, CancellationToken ct)
        => Ok(await _service.GetSchemeByCodeAsync(code, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<MedicalBenefitSchemeDto>> Create(
        [FromBody] CreateMedicalBenefitSchemeDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateSchemeAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MedicalBenefitSchemeDto>> Update(
        Guid id,
        [FromBody] UpdateMedicalBenefitSchemeDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateSchemeAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteSchemeAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // TIERS
    // =========================================================================

    [HttpGet("tiers/{id:guid}")]
    public async Task<ActionResult<MedicalBenefitTierDto>> GetTier(Guid id, CancellationToken ct)
        => Ok(await _service.GetTierByIdAsync(id, ct));

    [HttpGet("{schemeId:guid}/tiers")]
    public async Task<ActionResult<IEnumerable<MedicalBenefitTierDto>>> GetTiersByScheme(
        Guid schemeId,
        CancellationToken ct)
        => Ok(await _service.GetTiersBySchemeAsync(schemeId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("tiers")]
    public async Task<ActionResult<MedicalBenefitTierDto>> CreateTier(
        [FromBody] CreateMedicalBenefitTierDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateTierAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetTier), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("tiers/{id:guid}")]
    public async Task<ActionResult<MedicalBenefitTierDto>> UpdateTier(
        Guid id,
        [FromBody] UpdateMedicalBenefitTierDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateTierAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("tiers/{id:guid}")]
    public async Task<IActionResult> DeleteTier(Guid id, CancellationToken ct)
    {
        await _service.DeleteTierAsync(id, ct);
        return NoContent();
    }
}
