using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/hr/unions")]
[Authorize]
public class UnionController : ControllerBase
{
    private readonly IUnionService _unionService;

    public UnionController(IUnionService unionService)
    {
        _unionService = unionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UnionDto>>> GetAll()
        => Ok(await _unionService.GetAllAsync());

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<UnionDto>>> GetActive()
        => Ok(await _unionService.GetActiveAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UnionDto>> GetById(Guid id)
        => Ok(await _unionService.GetByIdAsync(id));

    [HttpPost]
    public async Task<ActionResult<UnionDto>> Create([FromBody] CreateUnionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _unionService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UnionDto>> Update(Guid id, [FromBody] UpdateUnionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _unionService.UpdateAsync(dto));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _unionService.DeleteAsync(id);
        return NoContent();
    }

    #region Collective Bargaining Agreements

    [HttpPost("{unionId:guid}/agreements")]
    public async Task<ActionResult<CollectiveBargainingAgreementDto>> AddAgreement(Guid unionId, [FromBody] CreateCollectiveBargainingAgreementDto dto)
    {
        dto.UnionId = unionId;
        var created = await _unionService.AddAgreementAsync(dto);
        return CreatedAtAction(nameof(GetAgreements), new { unionId }, created);
    }

    [HttpGet("{unionId:guid}/agreements")]
    public async Task<ActionResult<IEnumerable<CollectiveBargainingAgreementDto>>> GetAgreements(Guid unionId)
        => Ok(await _unionService.GetAgreementsAsync(unionId));

    [HttpPut("agreements/{id:guid}")]
    public async Task<ActionResult<CollectiveBargainingAgreementDto>> UpdateAgreement(Guid id, [FromBody] UpdateCollectiveBargainingAgreementDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        return Ok(await _unionService.UpdateAgreementAsync(dto));
    }

    [HttpDelete("agreements/{id:guid}")]
    public async Task<IActionResult> DeleteAgreement(Guid id)
    {
        await _unionService.DeleteAgreementAsync(id);
        return NoContent();
    }

    #endregion
}
