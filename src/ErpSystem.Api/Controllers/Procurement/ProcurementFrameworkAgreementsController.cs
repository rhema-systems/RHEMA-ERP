using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/framework-agreements")]
[Authorize(Policy = "InternalOnly")]
public sealed class ProcurementFrameworkAgreementsController : ControllerBase
{
    private readonly IProcurementFrameworkAgreementService _service;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICurrentUserProvider _currentUser;

    public ProcurementFrameworkAgreementsController(
        IProcurementFrameworkAgreementService service,
        IControlledFileUploadService controlledFiles,
        ICurrentUserProvider currentUser)
    {
        _service = service;
        _controlledFiles = controlledFiles;
        _currentUser = currentUser;
    }

    [HttpGet("summary")]
    public Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search(
        [FromQuery] ProcurementFrameworkAgreementSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpGet("options/workflows")]
    public Task<IActionResult> WorkflowOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetWorkflowOptionsAsync(cancellationToken)));

    [HttpGet("options/categories")]
    public Task<IActionResult> CategoryOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetCategoryOptionsAsync(cancellationToken)));

    [HttpGet("options/items")]
    public Task<IActionResult> ItemOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetItemOptionsAsync(cancellationToken)));

    [HttpGet("options/sources")]
    public Task<IActionResult> SourceOptions(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSourceOptionsAsync(cancellationToken)));

    [HttpPost]
    public Task<IActionResult> Create(
        [FromBody] CreateProcurementFrameworkAgreementRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var agreement = await _service.CreateAsync(
                request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = agreement.Id }, agreement);
        });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProcurementFrameworkAgreementRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.UpdateAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/clone")]
    public Task<IActionResult> Clone(
        Guid id,
        [FromBody] CloneProcurementFrameworkAgreementRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.CloneAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(
        Guid id,
        [FromBody] ProcurementFrameworkAgreementLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SubmitAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(
        Guid id,
        [FromBody] ProcurementFrameworkAgreementLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.ApproveAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(
        Guid id,
        [FromBody] ProcurementFrameworkAgreementLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RejectAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/terminate")]
    public Task<IActionResult> Terminate(
        Guid id,
        [FromBody] ProcurementFrameworkAgreementLifecycleRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.TerminateAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> AddDocument(
        Guid id,
        IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? title,
        [FromForm] bool isRequired = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (file is null || file.Length == 0)
                return ValidationProblem(
                    "FRAMEWORK_AGREEMENT_FILE_REQUIRED",
                    "Select a non-empty document to upload.");

            var upload = await _controlledFiles.UploadAsync(
                new ControlledFileUploadRequest
                {
                    TenantId = _currentUser.TenantId,
                    ActorUserId = _currentUser.UserId,
                    ActorName = ActorName,
                    Category = ControlledFileUploadCategories.DocumentManagement,
                    FileName = Path.GetFileName(file.FileName),
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType,
                    FileSize = file.Length,
                    OpenReadStream = file.OpenReadStream
                },
                cancellationToken);

            try
            {
                var document = await _service.AddDocumentAsync(
                    id,
                    new AddProcurementFrameworkAgreementDocumentRequest
                    {
                        FileUploadRecordId = upload.Record.Id,
                        DocumentType = documentType,
                        Title = string.IsNullOrWhiteSpace(title)
                            ? upload.Record.OriginalFileName
                            : title,
                        IsRequired = isRequired
                    },
                    CorrelationId,
                    cancellationToken);
                return Created(
                    $"/api/procurement/framework-agreements/{id}/documents/{document.Id}",
                    document);
            }
            catch
            {
                await _controlledFiles.DeleteAsync(
                    _currentUser.TenantId,
                    upload.Record.Id,
                    _currentUser.UserId,
                    cancellationToken);
                throw;
            }
        }
        catch (ControlledFileUploadException exception)
        {
            return StatusCode(exception.StatusCode,
                Problem(exception.StatusCode, exception.Code,
                    "Framework agreement upload failed", exception.Message));
        }
        catch (ProcurementFrameworkAgreementNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Framework agreement record not found", exception.Message));
        }
        catch (ProcurementFrameworkAgreementAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "FRAMEWORK_AGREEMENT_ACCESS_FORBIDDEN",
                    "Framework agreement access forbidden", exception.Message));
        }
        catch (ProcurementFrameworkAgreementConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Framework agreement conflict", exception.Message));
        }
        catch (ProcurementFrameworkAgreementValidationException exception)
        {
            return ValidationProblem(exception.Code, exception.Message);
        }
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    public Task<IActionResult> RetireDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RetireDocumentAsync(
            id, documentId, CorrelationId, cancellationToken)));

    [HttpGet("{id:guid}/documents/{documentId:guid}/content")]
    public Task<IActionResult> DownloadDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var document = await _service.OpenDocumentAsync(
                id, documentId, cancellationToken);
            if (document is null)
                throw new ProcurementFrameworkAgreementNotFoundException(
                    "FRAMEWORK_AGREEMENT_DOCUMENT_CONTENT_NOT_FOUND",
                    "The framework agreement document content was not found.");

            Response.Headers.CacheControl = "no-store, private";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.RegisterForDisposeAsync(document);
            return File(
                document.Content,
                string.IsNullOrWhiteSpace(document.ContentType)
                    ? "application/octet-stream"
                    : document.ContentType,
                document.FileName,
                enableRangeProcessing: false);
        });

    [HttpPost("{id:guid}/extensions")]
    public Task<IActionResult> RequestExtension(
        Guid id,
        [FromBody] RequestProcurementFrameworkAgreementExtension request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var extension = await _service.RequestExtensionAsync(
                id, request, CorrelationId, cancellationToken);
            return Created(
                $"/api/procurement/framework-agreements/{id}/extensions/{extension.Id}",
                extension);
        });

    [HttpPost("{id:guid}/extensions/{extensionId:guid}/decision")]
    public Task<IActionResult> DecideExtension(
        Guid id,
        Guid extensionId,
        [FromBody] DecideProcurementFrameworkAgreementExtension request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.DecideExtensionAsync(
            id, extensionId, request, CorrelationId, cancellationToken)));

    [HttpPost("process-lifecycle")]
    public Task<IActionResult> ProcessLifecycle(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(new
        {
            processed = await _service.ProcessLifecycleAsync(
                cancellationToken: cancellationToken)
        }));

    private string ActorName =>
        string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName;

    private string CorrelationId
    {
        get
        {
            var supplied = Request.Headers["X-Correlation-ID"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(supplied)
                ? string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier)
                    ? Guid.NewGuid().ToString("N")
                    : HttpContext.TraceIdentifier
                : supplied;
        }
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementFrameworkAgreementNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Framework agreement record not found", exception.Message));
        }
        catch (ProcurementFrameworkAgreementAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "FRAMEWORK_AGREEMENT_ACCESS_FORBIDDEN",
                    "Framework agreement access forbidden", exception.Message));
        }
        catch (ProcurementFrameworkAgreementConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Framework agreement conflict", exception.Message));
        }
        catch (ProcurementFrameworkAgreementValidationException exception)
        {
            return ValidationProblem(exception.Code, exception.Message);
        }
    }

    private UnprocessableEntityObjectResult ValidationProblem(string code, string detail)
    {
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [code] = [detail]
        })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Framework agreement validation failed",
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = CorrelationId;
        return UnprocessableEntity(problem);
    }

    private ProblemDetails Problem(int status, string code, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = code,
            ["correlationId"] = CorrelationId
        }
    };
}
