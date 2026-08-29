using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Finance-owned administration boundary for transaction coding dimensions. Operational modules
/// consume immutable dimension-set ids through certified adapters; they do not own this setup.
/// </summary>
[ApiController]
[Authorize]
[Route("api/finance/dimensions")]
public sealed class FinanceDimensionsController : ControllerBase
{
    private readonly FinanceDimensionAdministrationService _service;

    public FinanceDimensionsController(FinanceDimensionAdministrationService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<FinanceDimensionDefinitionDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetDefinitionsAsync(includeInactive, cancellationToken));

    [HttpPost]
    [Authorize(Policy = FinancePermissions.ManageCodingDimensions)]
    public async Task<ActionResult<FinanceDimensionDefinitionDto>> Create(
        [FromBody] UpsertFinanceDimensionDefinitionDto dto,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.CreateDefinitionAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ManageCodingDimensions)]
    public async Task<ActionResult<FinanceDimensionDefinitionDto>> Update(
        Guid id,
        [FromBody] UpsertFinanceDimensionDefinitionDto dto,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.UpdateDefinitionAsync(id, dto, cancellationToken));

    [HttpPost("{definitionId:guid}/values")]
    [Authorize(Policy = FinancePermissions.ManageCodingDimensions)]
    public async Task<ActionResult<FinanceDimensionValueDto>> CreateValue(
        Guid definitionId,
        [FromBody] UpsertFinanceDimensionValueDto dto,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.CreateValueAsync(definitionId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = definitionId }, result);
    }

    [HttpPut("{definitionId:guid}/values/{valueId:guid}")]
    [Authorize(Policy = FinancePermissions.ManageCodingDimensions)]
    public async Task<ActionResult<FinanceDimensionValueDto>> UpdateValue(
        Guid definitionId,
        Guid valueId,
        [FromBody] UpsertFinanceDimensionValueDto dto,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.UpdateValueAsync(definitionId, valueId, dto, cancellationToken));

    [HttpGet("rules")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<FinanceDimensionAccountRuleDto>>> GetRules(
        [FromQuery] Guid? accountId,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.GetRulesAsync(accountId, cancellationToken));

    [HttpPost("rules")]
    [Authorize(Policy = FinancePermissions.ManageCodingDimensions)]
    public async Task<ActionResult<FinanceDimensionAccountRuleDto>> CreateRule(
        [FromBody] UpsertFinanceDimensionAccountRuleDto dto,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.UpsertRuleAsync(null, dto, cancellationToken);
        return CreatedAtAction(nameof(GetRules), new { id = result.Id }, result);
    }

    [HttpPut("rules/{id:guid}")]
    [Authorize(Policy = FinancePermissions.ManageCodingDimensions)]
    public async Task<ActionResult<FinanceDimensionAccountRuleDto>> UpdateRule(
        Guid id,
        [FromBody] UpsertFinanceDimensionAccountRuleDto dto,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.UpsertRuleAsync(id, dto, cancellationToken));
}
