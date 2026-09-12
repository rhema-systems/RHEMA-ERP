using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The policy library — the desk that writes, publishes and monitors it (area 25 slice 12d, D7).
/// </summary>
/// <remarks>
/// <para>Gated on <c>HR.Company</c>, like announcements: a policy is issued by the organisation,
/// not performed on anybody's record.</para>
///
/// <para>The lifecycle only goes forward, and a published policy is <b>superseded rather than
/// edited</b> — every acknowledgement points at the version it was collected for, and editing
/// under a signature would turn "I agree to this" into "I agree to whatever this becomes".</para>
/// </remarks>
[ApiController]
[Route("api/hr/policies")]
[Authorize(Policy = "InternalOnly")]
public class HrPoliciesController : ControllerBase
{
    private readonly IHrPolicyService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<HrPoliciesController> _logger;

    public HrPoliciesController(
        IHrPolicyService service,
        IHrControlledDocumentService hrDocuments,
        ICurrentUserService currentUser,
        ILogger<HrPoliciesController> logger)
    {
        _service = service;
        _hrDocuments = hrDocuments;
        _currentUser = currentUser;
        _logger = logger;
    }

    private IActionResult NoActor() =>
        Problem(
            detail:     "Your account is not linked to an employee record, so a publication cannot be attributed to you.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    [HttpGet]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<IActionResult> GetAll(
        [FromQuery] HrPolicyStatus? status, CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(status, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var policy = await _service.GetByIdAsync(id, ct);
        return policy is null ? NotFound() : Ok(policy);
    }

    /// <summary>
    /// Who has and has not acknowledged it. Computed at request time from the current audience,
    /// so somebody who joined after publication appears as outstanding.
    /// </summary>
    [HttpGet("{id:guid}/compliance")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<IActionResult> GetCompliance(
        Guid id,
        [FromQuery] PolicyComplianceFilter filter = PolicyComplianceFilter.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var compliance = await _service.GetComplianceAsync(id, filter, page, pageSize, ct);
        return compliance is null ? NotFound() : Ok(compliance);
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Create(
        [FromBody] SaveHrPolicyDto dto, CancellationToken ct = default)
    {
        try
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] SaveHrPolicyDto dto, CancellationToken ct = default)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, dto, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>
    /// Publishes it — refused without a document, without a declaration when one is asked for,
    /// or with an audience that reaches nobody. Supersedes the previous version if named.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid actorId) return NoActor();

        try
        {
            return Ok(await _service.PublishAsync(id, actorId, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid actorId) return NoActor();

        try
        {
            return Ok(await _service.ArchiveAsync(id, actorId, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> DeleteDraft(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _service.DeleteDraftAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>Attaches the policy document. Drafts only — see the class remarks.</summary>
    [HttpPost("{id:guid}/document")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadDocument(
        Guid id, IFormFile file, CancellationToken ct = default)
    {
        // Entitlement before storage.
        var policy = await _service.GetByIdAsync(id, ct);
        if (policy is null) return NotFound();
        if (policy.Status != HrPolicyStatus.Draft)
            return UnprocessableEntity(new
            {
                message = "The document of a published policy cannot be replaced. Publish a new "
                        + "version that supersedes it.",
            });

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "HrPolicyDocument",
            sourceRecordId: id,
            sourceLabel: policy.PolicyNumber,
            documentType: $"Company policy — {policy.CategoryName}",
            description: policy.Title,
            persist: async (_, document) =>
            {
                await _service.AttachDocumentAsync(
                    id,
                    document.FileUploadRecordId,
                    document.DocumentRecordId,
                    document.DocumentVersionId,
                    document.FilePath,
                    document.OriginalFileName,
                    document.ContentType,
                    document.FileSize,
                    ct);
                return await _service.GetByIdAsync(id, ct);
            },
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrPolicyDocuments);
    }
}
