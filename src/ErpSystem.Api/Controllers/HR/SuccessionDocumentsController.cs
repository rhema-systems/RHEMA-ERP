using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The file half of a succession document — upload through the controlled boundary, download
/// through an authorizing endpoint.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why one controller for three owners.</b> A <c>SuccessionDocument</c> hangs off a succession
/// plan, a plan candidate or a talent-pool member, and one table and one DTO serve all three. The
/// metadata routes stayed where their parents are (<c>succession-plans/{}/documents</c>,
/// <c>succession-candidates/{}/documents</c>, <c>talent-pools/members/{}/documents</c>) because
/// that is where the collection is read; the file transport lives here once rather than three
/// times, exactly as <c>api/succession-development</c> already serves all three owners for
/// development activities.
/// </para>
/// <para>
/// <b>D-14.</b> Before this existed the only way to create a succession document was a metadata
/// POST with a <c>[Required]</c> caller-supplied <c>DocumentUrl</c>, stored verbatim — the fourth
/// instance of the path-injection sink that <c>MedicalExpenseDocument</c>,
/// <c>EmployeeMedicalExamDocument</c> and <c>MedicalInsuranceProviderDocument</c> were each fixed
/// for. There was no upload route and no download route, so a row was unreadable even when the
/// path was honest, and the collection was therefore unbuildable.
/// </para>
/// <para>
/// <b>D-15.</b> <c>UploadedById</c> is stamped from the authenticated employee here and on the
/// metadata routes. It used to be a <c>[Required]</c> field on the create DTO copied straight onto
/// the entity's <c>Employee</c> FK, so a document could be attributed to a colleague.
/// </para>
/// </remarks>
[ApiController]
[Route("api/succession-documents")]
// A succession document names a person as a candidate to replace someone — often someone still in
// the post. Read is the class-level floor; the upload is tightened to the write policy.
[Authorize(Policy = HrPermissions.SuccessionReadPolicy)]
public class SuccessionDocumentsController : HrControllerBase
{
    private readonly ISuccessionPlanService _planService;
    private readonly ISuccessionCandidateService _candidateService;
    private readonly ITalentPoolService _talentPoolService;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;

    public SuccessionDocumentsController(
        ISuccessionPlanService planService,
        ISuccessionCandidateService candidateService,
        ITalentPoolService talentPoolService,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _planService = planService;
        _candidateService = candidateService;
        _talentPoolService = talentPoolService;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
    }

    /// <summary>Attaches a file to a succession plan, a plan candidate or a talent-pool member.</summary>
    /// <remarks>
    /// Exactly one owner id is required. Sending none would orphan the row — every per-parent read
    /// filters on one of the three columns, so a document with all three null is written and then
    /// invisible — and sending two would make it appear in two collections at once.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<SuccessionDocumentDto>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string documentName,
        [FromForm] string documentType,
        [FromForm] Guid? successionPlanId = null,
        [FromForm] Guid? candidateId = null,
        [FromForm] Guid? talentPoolMemberId = null,
        [FromForm] string? description = null,
        [FromForm] bool isConfidential = false,
        [FromForm] DateTime? retentionDate = null,
        CancellationToken ct = default)
    {
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId,
                "Uploading a succession document") is { } error) return error;

        if (file is null || file.Length == 0)
            return BadRequest("No file was provided.");

        if (string.IsNullOrWhiteSpace(documentName))
            return BadRequest("A document name is required.");

        var owners = new[] { successionPlanId, candidateId, talentPoolMemberId }.Count(id => id.HasValue);
        if (owners != 1)
        {
            return BadRequest(
                "Supply exactly one of successionPlanId, candidateId or talentPoolMemberId. " +
                "A document with none is written and then invisible to every read; a document " +
                "with two appears in two collections.");
        }

        // Refuse an owner from another tenant before any bytes are stored. Each service's own
        // ownership helper does this and throws if the row is not ours.
        var ownerLabel = await ResolveOwnerAsync(successionPlanId, candidateId, talentPoolMemberId);
        if (ownerLabel is null)
            return NotFound("That succession plan, candidate or talent-pool member could not be found.");

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                ActorUserId = userId,
                ActorName = CurrentUser.UserName,
                Category = ControlledFileUploadCategories.HrSuccessionDocuments,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "Succession document",
                    SourceEntityType = nameof(SuccessionDocument),
                    SourceRecordId = successionPlanId ?? candidateId ?? talentPoolMemberId!.Value,
                    Title = documentName,
                    DocumentType = documentType,
                    ChangeSummary = description
                }
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            // The gate's own {code, message} contract — a refused file is not a server fault.
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        var dto = new CreateSuccessionDocumentDto
        {
            SuccessionPlanId = successionPlanId,
            CandidateId = candidateId,
            TalentPoolMemberId = talentPoolMemberId,
            DocumentName = documentName,
            DocumentType = documentType,
            DocumentUrl = string.Empty,
            FileUploadRecordId = document.FileUploadRecordId,
            DocumentRecordId = document.DocumentRecordId,
            DocumentVersionId = document.DocumentVersionId,
            Description = description,
            FileSizeBytes = document.FileSize,
            IsConfidential = isConfidential,
            RetentionDate = retentionDate
        };

        try
        {
            var created = talentPoolMemberId.HasValue
                ? await _talentPoolService.AddDocumentForMemberAsync(dto, tenantId, userId, employeeId, ct)
                : candidateId.HasValue
                    ? await _candidateService.AddDocumentAsync(dto, tenantId, userId, employeeId, ct)
                    : await _planService.AddDocumentAsync(dto, tenantId, userId, employeeId, ct);

            return Ok(created);
        }
        catch
        {
            // Leave no scanned-and-registered document behind pointing at a row never written.
            await _hrDocuments.RollbackAsync(document, tenantId, userId, ct);
            throw;
        }
    }

    /// <summary>Streams a succession document to a caller entitled to see it.</summary>
    /// <remarks>
    /// A confidential document is Admin-only, matching the separate
    /// <c>succession-plans/{}/documents/confidential</c> read — that list is not a filter over the
    /// ordinary one, and a download route that ignored the flag would hand back through one door
    /// what the other deliberately withholds.
    /// </remarks>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var document = await _db.Set<SuccessionDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (document is null) return NotFound();

        if (document.IsConfidential && !await HoldsPolicyAsync(HrPermissions.SuccessionAdminPolicy))
            return Forbid();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, document.DocumentUrl,
            document.DocumentName, fallbackContentType: null,
            inline: false, ct);
    }

    private async Task<bool> HoldsPolicyAsync(string policy)
    {
        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>
    /// Confirms the named owner exists in this tenant. Returns null when it does not, so the
    /// caller answers 404 rather than storing bytes against a row it cannot see.
    /// </summary>
    private async Task<string?> ResolveOwnerAsync(Guid? planId, Guid? candidateId, Guid? memberId)
    {
        var tenantId = CurrentUser.TenantId!.Value;

        if (planId.HasValue)
        {
            return await _db.Set<SuccessionPlan>().AsNoTracking().AnyAsync(
                p => p.Id == planId.Value && p.TenantId == tenantId && !p.IsDeleted)
                ? "plan" : null;
        }

        if (candidateId.HasValue)
        {
            return await _db.Set<SuccessionCandidate>().AsNoTracking().AnyAsync(
                c => c.Id == candidateId.Value && c.TenantId == tenantId && !c.IsDeleted)
                ? "candidate" : null;
        }

        return await _db.Set<TalentPoolMember>().AsNoTracking().AnyAsync(
            m => m.Id == memberId!.Value && m.TenantId == tenantId && !m.IsDeleted)
            ? "member" : null;
    }
}
