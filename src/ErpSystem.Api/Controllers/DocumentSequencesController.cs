using ErpSystem.Core.DTOs.Numbering;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/document-sequences")]
public class DocumentSequencesController : ControllerBase
{
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly ICurrentUserService _currentUserService;

    public DocumentSequencesController(
        IDocumentNumberingService documentNumberingService,
        ICurrentUserService currentUserService)
    {
        _documentNumberingService = documentNumberingService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentSequenceDefinitionDto>>> GetDefinitions(
        [FromQuery] string? module,
        CancellationToken cancellationToken)
    {
        var definitions = await _documentNumberingService.GetDefinitionsAsync(module, _currentUserService.TenantId, cancellationToken);
        return Ok(definitions);
    }

    [HttpPost("ensure-defaults")]
    public async Task<IActionResult> EnsureDefaults(CancellationToken cancellationToken)
    {
        if (_currentUserService.TenantId == null)
        {
            return BadRequest("Tenant context is required.");
        }

        await _documentNumberingService.EnsureDefaultsAsync(_currentUserService.TenantId.Value, cancellationToken);
        return NoContent();
    }

    [HttpPost("generate")]
    public async Task<ActionResult<GeneratedDocumentNumberDto>> Generate(
        GenerateDocumentNumberRequestDto request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.TenantId == null)
        {
            return BadRequest("Tenant context is required.");
        }

        // Never trust a client-supplied tenant for sequence generation; generating a number advances tenant state.
        var documentNumber = await _documentNumberingService.GenerateAsync(
            request.Module,
            request.DocumentType,
            _currentUserService.TenantId.Value,
            request.DocumentDate,
            request.EntityType,
            request.EntityId,
            cancellationToken);

        return Ok(new GeneratedDocumentNumberDto { DocumentNumber = documentNumber });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentSequenceDefinitionDto>> Update(
        Guid id,
        UpdateDocumentSequenceDefinitionDto request,
        CancellationToken cancellationToken)
    {
        var definition = await _documentNumberingService.UpdateDefinitionAsync(id, request, cancellationToken);
        return Ok(definition);
    }
}
