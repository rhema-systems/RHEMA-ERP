using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/employee-health")]
// Medical records are special-category personal data. This controller previously carried a
// bare [Authorize], so any authenticated employee could read them. Read is the class-level
// floor; write and delete are tightened per action.
[Authorize(Policy = HrPermissions.MedicalReadPolicy)]
public class EmployeeHealthController : MedicalControllerBase
{
    private readonly IEmployeeHealthService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;

    public EmployeeHealthController(
        IEmployeeHealthService service,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
    }

    // =========================================================================
    // HEALTH PROFILES
    // =========================================================================

    [HttpGet("profiles")]
    public async Task<ActionResult<IEnumerable<EmployeeHealthProfileDto>>> GetAllProfiles(CancellationToken ct)
        => Ok(await _service.GetAllProfilesAsync(ct));

    [HttpGet("profiles/{id:guid}")]
    public async Task<ActionResult<EmployeeHealthProfileDto>> GetProfile(Guid id, CancellationToken ct)
        => Ok(await _service.GetProfileByIdAsync(id, ct));

    [HttpGet("profiles/{id:guid}/details")]
    public async Task<ActionResult<EmployeeHealthProfileDetailDto>> GetProfileWithDetails(Guid id, CancellationToken ct)
        => Ok(await _service.GetProfileWithDetailsAsync(id, ct));

    [HttpGet("employees/{employeeId:guid}/profile")]
    public async Task<ActionResult<EmployeeHealthProfileDto?>> GetProfileByEmployee(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetProfileByEmployeeAsync(employeeId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("profiles")]
    public async Task<ActionResult<EmployeeHealthProfileDto>> CreateProfile(
        [FromBody] CreateEmployeeHealthProfileDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateProfileAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetProfile), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("profiles/{id:guid}")]
    public async Task<ActionResult<EmployeeHealthProfileDto>> UpdateProfile(
        Guid id,
        [FromBody] UpdateEmployeeHealthProfileDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateProfileAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("profiles/{id:guid}")]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken ct)
    {
        await _service.DeleteProfileAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // CONDITIONS
    // =========================================================================

    [HttpGet("profiles/{healthProfileId:guid}/conditions")]
    public async Task<ActionResult<IEnumerable<EmployeeHealthConditionDto>>> GetConditions(
        Guid healthProfileId,
        [FromQuery] bool onlyActive = false,
        CancellationToken ct = default)
        => Ok(onlyActive
            ? await _service.GetActiveConditionsAsync(healthProfileId, ct)
            : await _service.GetConditionsAsync(healthProfileId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("conditions")]
    public async Task<ActionResult<EmployeeHealthConditionDto>> AddCondition(
        [FromBody] CreateEmployeeHealthConditionDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddConditionAsync(dto, tenantId, userId, ct);
        return Ok(created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("conditions/{id:guid}")]
    public async Task<ActionResult<EmployeeHealthConditionDto>> UpdateCondition(
        Guid id,
        [FromBody] UpdateEmployeeHealthConditionDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateConditionAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("conditions/{id:guid}")]
    public async Task<IActionResult> DeleteCondition(Guid id, CancellationToken ct)
    {
        await _service.DeleteConditionAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // ALLERGIES
    // =========================================================================

    [HttpGet("profiles/{healthProfileId:guid}/allergies")]
    public async Task<ActionResult<IEnumerable<EmployeeAllergyDto>>> GetAllergies(
        Guid healthProfileId,
        CancellationToken ct)
        => Ok(await _service.GetAllergiesAsync(healthProfileId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("allergies")]
    public async Task<ActionResult<EmployeeAllergyDto>> AddAllergy(
        [FromBody] CreateEmployeeAllergyDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddAllergyAsync(dto, tenantId, userId, ct);
        return Ok(created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("allergies/{id:guid}")]
    public async Task<ActionResult<EmployeeAllergyDto>> UpdateAllergy(
        Guid id,
        [FromBody] UpdateEmployeeAllergyDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateAllergyAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("allergies/{id:guid}")]
    public async Task<IActionResult> DeleteAllergy(Guid id, CancellationToken ct)
    {
        await _service.DeleteAllergyAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // MEDICAL EXAMS
    // =========================================================================

    [HttpGet("exams/{id:guid}")]
    public async Task<ActionResult<EmployeeMedicalExamDto>> GetExam(Guid id, CancellationToken ct)
        => Ok(await _service.GetExamByIdAsync(id, ct));

    [HttpGet("exams/{id:guid}/details")]
    public async Task<ActionResult<EmployeeMedicalExamDetailDto>> GetExamWithDocuments(Guid id, CancellationToken ct)
        => Ok(await _service.GetExamWithDocumentsAsync(id, ct));

    [HttpGet("profiles/{healthProfileId:guid}/exams")]
    public async Task<ActionResult<IEnumerable<EmployeeMedicalExamSummaryDto>>> GetExamsByProfile(
        Guid healthProfileId,
        CancellationToken ct)
        => Ok(await _service.GetExamsByProfileAsync(healthProfileId, ct));

    [HttpGet("exams/due")]
    public async Task<ActionResult<IEnumerable<EmployeeMedicalExamSummaryDto>>> GetExamsDue(
        [FromQuery] int daysAhead = 30,
        CancellationToken ct = default)
        => Ok(await _service.GetExamsDueAsync(daysAhead, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("exams")]
    public async Task<ActionResult<EmployeeMedicalExamDto>> CreateExam(
        [FromBody] CreateEmployeeMedicalExamDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateExamAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetExam), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("exams/{id:guid}")]
    public async Task<ActionResult<EmployeeMedicalExamDto>> UpdateExam(
        Guid id,
        [FromBody] UpdateEmployeeMedicalExamDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateExamAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("exams/{id:guid}")]
    public async Task<IActionResult> DeleteExam(Guid id, CancellationToken ct)
    {
        await _service.DeleteExamAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // EXAM DOCUMENTS
    // =========================================================================

    [HttpGet("exams/{examId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<EmployeeMedicalExamDocumentDto>>> GetExamDocuments(
        Guid examId,
        CancellationToken ct)
        => Ok(await _service.GetExamDocumentsAsync(examId, ct));

    /// <summary>
    /// Uploads a document against a medical exam.
    /// </summary>
    /// <remarks>
    /// This used to be a JSON endpoint taking a caller-supplied <c>FilePath</c>, which let any
    /// authenticated user attach arbitrary bytes on disk — including another tenant's — to a
    /// medical record. It is now a real multipart upload routed through the shared gate, so the
    /// file is malware-scanned and stored outside the publicly served web root.
    /// </remarks>
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("exam-documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<EmployeeMedicalExamDocumentDto>> AddExamDocument(
        [FromForm] Guid examId,
        IFormFile file,
        [FromForm] string? description,
        CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        if (file is null || file.Length == 0)
            return BadRequest("No file was provided.");
        if (examId == Guid.Empty)
            return BadRequest("An exam id is required.");

        // Confirms the exam is real and in this tenant before anything is stored.
        var exam = await _db.Set<EmployeeMedicalExam>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == examId && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (exam is null)
            return NotFound("Medical exam not found.");

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                ActorUserId = userId,
                ActorName = CurrentUser.UserName,
                Category = ControlledFileUploadCategories.HrMedicalExamDocuments,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "Employee medical exam document",
                    SourceEntityType = nameof(EmployeeMedicalExam),
                    SourceRecordId = examId,
                    Title = Path.GetFileName(file.FileName),
                    DocumentType = "MedicalExamDocument",
                    AccessProfile = "Medical restricted",
                    ChangeSummary = description
                }
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            var created = await _service.AddExamDocumentAsync(
                new CreateEmployeeMedicalExamDocumentDto
                {
                    ExamId = examId,
                    FileName = document.OriginalFileName,
                    FilePath = string.Empty,
                    FileUploadRecordId = document.FileUploadRecordId,
                    DocumentRecordId = document.DocumentRecordId,
                    DocumentVersionId = document.DocumentVersionId,
                    Description = description
                },
                tenantId, userId, ct);

            return Ok(created);
        }
        catch
        {
            await _hrDocuments.RollbackAsync(document, tenantId, userId, ct);
            throw;
        }
    }

    /// <summary>Streams a medical exam document.</summary>
    [HttpGet("exam-documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadExamDocument(Guid id, CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var document = await _db.Set<EmployeeMedicalExamDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (document is null)
            return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, document.FilePath,
            document.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("exam-documents/{id:guid}")]
    public async Task<IActionResult> DeleteExamDocument(Guid id, CancellationToken ct)
    {
        await _service.DeleteExamDocumentAsync(id, ct);
        return NoContent();
    }
}
