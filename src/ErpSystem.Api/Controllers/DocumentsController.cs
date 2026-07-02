using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Interfaces.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentOutputService _documentOutputService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentOutputService documentOutputService,
        ILogger<DocumentsController> logger)
    {
        _documentOutputService = documentOutputService;
        _logger = logger;
    }

    [HttpGet("{documentType}/{entityId:guid}")]
    public async Task<IActionResult> RenderDocument(
        string documentType,
        Guid entityId,
        [FromQuery] string format = "pdf",
        [FromQuery] string copyType = "Original",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rendered = await _documentOutputService.RenderAsync(new DocumentRenderRequestDto
            {
                DocumentType = documentType,
                EntityId = entityId,
                Format = format,
                CopyType = copyType
            }, cancellationToken);

            return File(rendered.Content, rendered.ContentType, rendered.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render document {DocumentType} for entity {EntityId}", documentType, entityId);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("{documentType}")]
    public async Task<IActionResult> RenderParameterizedDocument(
        string documentType,
        [FromQuery] string format = "pdf",
        [FromQuery] string copyType = "Original",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = Request.Query
                .Where(pair => !string.Equals(pair.Key, "format", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(pair.Key, "copyType", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

            var rendered = await _documentOutputService.RenderAsync(new DocumentRenderRequestDto
            {
                DocumentType = documentType,
                EntityId = Guid.Empty,
                Format = format,
                CopyType = copyType,
                Options = options
            }, cancellationToken);

            return File(rendered.Content, rendered.ContentType, rendered.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render parameterized document {DocumentType}", documentType);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
