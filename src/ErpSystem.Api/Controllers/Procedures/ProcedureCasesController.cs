using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Interfaces.Procedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procedures;

[ApiController]
[Route("api/procedure-cases")]
[Authorize]
public sealed class ProcedureCasesController : ControllerBase
{
    private readonly IProcedureCaseService _procedureCaseService;

    public ProcedureCasesController(IProcedureCaseService procedureCaseService)
    {
        _procedureCaseService = procedureCaseService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCases([FromQuery] string? module, [FromQuery] string? entityType, [FromQuery] bool mineOnly = false)
    {
        var cases = await _procedureCaseService.GetCasesAsync(module, entityType, mineOnly);
        return Ok(new { success = true, data = cases });
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetCasesPage(
        [FromQuery] string? module,
        [FromQuery] string? entityType,
        [FromQuery] bool mineOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var cases = await _procedureCaseService.GetCasesPageAsync(module, entityType, mineOnly, page, pageSize);
        return Ok(new { success = true, data = cases });
    }

    [HttpGet("submission-document-requirements")]
    public async Task<IActionResult> GetSubmissionDocumentRequirements(
        [FromQuery] string module,
        [FromQuery] string entityType)
    {
        var requirements = await _procedureCaseService.GetSubmissionDocumentRequirementsAsync(module, entityType);
        return Ok(new { success = true, data = requirements });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCase(Guid id)
    {
        var procedureCase = await _procedureCaseService.GetCaseAsync(id);
        return procedureCase is null
            ? NotFound(new { success = false, message = "Procedure case was not found." })
            : Ok(new { success = true, data = procedureCase });
    }

    [HttpPost]
    public async Task<IActionResult> CreateCase([FromBody] CreateProcedureCaseRequest request)
    {
        try
        {
            var procedureCase = await _procedureCaseService.CreateCaseAsync(request);
            return CreatedAtAction(nameof(GetCase), new { id = procedureCase.Id }, new { success = true, data = procedureCase });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/legal-matters")]
    public async Task<IActionResult> CreateLinkedLegalMatter(
        Guid id,
        [FromBody] CreateLinkedLegalMatterRequest request)
    {
        try
        {
            var legalMatter = await _procedureCaseService.CreateLinkedLegalMatterAsync(id, request);
            return Ok(new { success = true, data = legalMatter });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/fields")]
    public async Task<IActionResult> UpdateFields(Guid id, [FromBody] UpdateProcedureCaseFieldsRequest request)
    {
        return await ExecuteUpdate(() => _procedureCaseService.UpdateFieldsAsync(id, request));
    }

    [HttpPut("{id:guid}/checklist/{checklistItemId:guid}")]
    public async Task<IActionResult> UpdateChecklistItem(Guid id, Guid checklistItemId, [FromBody] UpdateProcedureCaseChecklistRequest request)
    {
        return await ExecuteUpdate(() => _procedureCaseService.UpdateChecklistItemAsync(id, checklistItemId, request));
    }

    [HttpPut("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> AttachDocument(Guid id, Guid documentId, [FromBody] AttachProcedureCaseDocumentRequest request)
    {
        return await ExecuteUpdate(() => _procedureCaseService.AttachDocumentAsync(id, documentId, request));
    }

    [HttpPost("{id:guid}/documents/{documentId:guid}/upload")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadDocument(Guid id, Guid documentId, [FromForm] IFormFile? file, [FromForm] string? notes)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "Select a document to upload." });
        }

        return await ExecuteUpdate(async () =>
        {
            await using var stream = file.OpenReadStream();
            return await _procedureCaseService.UploadDocumentAsync(
                id,
                documentId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                notes);
        });
    }

    [HttpPost("{id:guid}/customer-intake-documents/{documentId:guid}/upload")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadCustomerIntakeDocument(
        Guid id,
        Guid documentId,
        [FromForm] IFormFile? file,
        [FromForm] string? notes)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "Select a document to upload." });
        }

        return await ExecuteUpdate(async () =>
        {
            await using var stream = file.OpenReadStream();
            return await _procedureCaseService.UploadCustomerIntakeDocumentAsync(
                id,
                documentId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                notes);
        });
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(Guid id, Guid documentId)
    {
        try
        {
            var document = await _procedureCaseService.GetDocumentContentAsync(id, documentId);
            return document is null
                ? NotFound(new { success = false, message = "Procedure case document was not found." })
                : File(document.FileStream, document.ContentType, document.FileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/documents/{documentId:guid}/legal-transfer-signature")]
    public async Task<IActionResult> SignLegalTransferExecutedDocument(
        Guid id,
        Guid documentId,
        [FromBody] SignProcedureCaseDocumentRequest request)
    {
        return await ExecuteUpdate(() => _procedureCaseService.SignLegalTransferExecutedDocumentAsync(id, documentId, request));
    }

    [HttpPost("{id:guid}/documents/{documentId:guid}/property-agreement-signature")]
    public async Task<IActionResult> SignPropertyAgreement(
        Guid id,
        Guid documentId,
        [FromBody] SignProcedureCaseDocumentRequest request)
    {
        return await ExecuteUpdate(() => _procedureCaseService.SignPropertyAgreementAsync(id, documentId, request));
    }

    [HttpPost("{id:guid}/complete-stage")]
    public async Task<IActionResult> CompleteCurrentStage(Guid id, [FromBody] CompleteProcedureCaseStageRequest request)
    {
        return await ExecuteUpdate(() => _procedureCaseService.CompleteCurrentStageAsync(id, request));
    }

    [HttpPost("{id:guid}/legal-transfer-fee-payment/sync")]
    public async Task<IActionResult> SyncLegalTransferFeePaymentStatus(Guid id)
    {
        return await ExecuteUpdate(() => _procedureCaseService.SyncLegalTransferFeePaymentStatusAsync(id));
    }

    [HttpPost("{id:guid}/review-action")]
    public async Task<IActionResult> ApplyReviewAction(Guid id, [FromBody] ReviewProcedureCaseRequest request)
    {
        return await ExecuteUpdate(() => _procedureCaseService.ApplyReviewActionAsync(id, request));
    }

    private static async Task<IActionResult> ExecuteUpdate(Func<Task<ProcedureCaseDetailDto?>> action)
    {
        try
        {
            var result = await action();
            return result is null
                ? new NotFoundObjectResult(new { success = false, message = "Procedure case was not found." })
                : new OkObjectResult(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden };
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { success = false, message = ex.Message });
        }
    }
}
