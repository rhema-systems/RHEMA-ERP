using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Lets an employee read their own occupational-health records.
/// </summary>
/// <remarks>
/// <para>The medical controllers now require an HR medical permission, which correctly excludes
/// ordinary employees — but an employee has a legitimate need to see their own file. Rather than
/// carving exceptions into those controllers and weakening their class-level policy, the entire
/// self-service surface lives here, behind a plain <c>[Authorize]</c>, in one auditable file.</para>
///
/// <para>The scoping rule has no exceptions: every query filters on the employee resolved from
/// <c>CurrentUser.EmployeeId</c> (through their health profile where records are profile-keyed),
/// and <b>no route or query parameter on this controller may ever carry an employee or profile
/// id</b>. Read-only by design — corrections to a medical record go through HR.</para>
/// </remarks>
[ApiController]
[Route("api/employee-health/me")]
[Authorize(Policy = "InternalOnly")]
public class EmployeeHealthSelfServiceController : MedicalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;

    public EmployeeHealthSelfServiceController(
        ApplicationDbContext db,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _db = db;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
    }

    /// <summary>Returns the caller's own health profile.</summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetOwnProfile(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out var tenantId, out _, out var employeeId) is { } error)
            return error;

        var profile = await LoadOwnProfileAsync(tenantId, employeeId, ct);
        if (profile is null)
            return NotFound();

        return Ok(new
        {
            profile.Id,
            profile.EmployeeId,
            profile.BloodGroup,
            profile.HeightCm,
            profile.WeightKg,
            profile.EmergencyContactName,
            profile.EmergencyContactPhone,
            profile.EmergencyContactRelationship
        });
    }

    /// <summary>Returns the caller's own recorded health conditions.</summary>
    [HttpGet("conditions")]
    public async Task<IActionResult> GetOwnConditions(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out var tenantId, out _, out var employeeId) is { } error)
            return error;

        var profile = await LoadOwnProfileAsync(tenantId, employeeId, ct);
        if (profile is null)
            return Ok(Array.Empty<object>());

        var conditions = await _db.Set<EmployeeHealthCondition>()
            .AsNoTracking()
            .Where(item => item.HealthProfileId == profile.Id &&
                           item.TenantId == tenantId &&
                           !item.IsDeleted)
            .ToListAsync(ct);

        return Ok(conditions.Select(item => new
        {
            item.Id,
            item.ConditionName,
            item.DiagnosedDate,
            item.Severity,
            item.Status
        }));
    }

    /// <summary>Returns the caller's own recorded allergies.</summary>
    [HttpGet("allergies")]
    public async Task<IActionResult> GetOwnAllergies(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out var tenantId, out _, out var employeeId) is { } error)
            return error;

        var profile = await LoadOwnProfileAsync(tenantId, employeeId, ct);
        if (profile is null)
            return Ok(Array.Empty<object>());

        var allergies = await _db.Set<EmployeeAllergy>()
            .AsNoTracking()
            .Where(item => item.HealthProfileId == profile.Id &&
                           item.TenantId == tenantId &&
                           !item.IsDeleted)
            .ToListAsync(ct);

        return Ok(allergies.Select(item => new { item.Id, item.Allergen, item.Severity }));
    }

    /// <summary>Returns the caller's own medical examinations.</summary>
    [HttpGet("exams")]
    public async Task<IActionResult> GetOwnExams(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out var tenantId, out _, out var employeeId) is { } error)
            return error;

        var profile = await LoadOwnProfileAsync(tenantId, employeeId, ct);
        if (profile is null)
            return Ok(Array.Empty<object>());

        var exams = await _db.Set<EmployeeMedicalExam>()
            .AsNoTracking()
            .Where(item => item.HealthProfileId == profile.Id &&
                           item.TenantId == tenantId &&
                           !item.IsDeleted)
            .ToListAsync(ct);

        return Ok(exams.Select(item => new
        {
            item.Id,
            item.ExamDate,
            item.Result,
            item.NextExamDueDate
        }));
    }

    /// <summary>Streams a document attached to one of the caller's own examinations.</summary>
    [HttpGet("exam-documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadOwnExamDocument(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out var tenantId, out _, out var employeeId) is { } error)
            return error;

        var profile = await LoadOwnProfileAsync(tenantId, employeeId, ct);
        if (profile is null)
            return NotFound();

        // Joined to the caller's own profile, so a document id belonging to another employee is
        // a lookup miss rather than a disclosure.
        var document = await _db.Set<EmployeeMedicalExamDocument>()
            .AsNoTracking()
            .Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted)
            .Join(_db.Set<EmployeeMedicalExam>().AsNoTracking()
                    .Where(exam => exam.HealthProfileId == profile.Id &&
                                   exam.TenantId == tenantId &&
                                   !exam.IsDeleted),
                doc => doc.ExamId,
                exam => exam.Id,
                (doc, _) => doc)
            .SingleOrDefaultAsync(ct);
        if (document is null)
            return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, document.FilePath,
            document.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    // ── Occupational-health surveillance (area 25 slice 8) ──────────────────────────────────
    // Surveillance rows live on the SHE side of the SHE↔Medical boundary but ARE medical-grade
    // data about this employee, so the self arm belongs on this controller, not on
    // api/safety/occupational-health — whose class-level MedicalReadPolicy cannot be relaxed
    // per-action. Keyed by EmployeeId directly (surveillance predates any health profile).

    /// <summary>Lists the caller's own health-surveillance records.</summary>
    [HttpGet("surveillance")]
    public async Task<IActionResult> GetOwnSurveillance(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out var tenantId, out _, out var employeeId) is { } error)
            return error;

        var rows = await _db.Set<SheOccupationalHealthSurveillance>()
            .AsNoTracking()
            .Where(item => item.EmployeeId == employeeId &&
                           item.TenantId == tenantId &&
                           !item.IsDeleted)
            .OrderByDescending(item => item.ExaminationDate)
            .ToListAsync(ct);

        return Ok(rows.Select(item => new
        {
            item.Id,
            item.SurveillanceNumber,
            item.Type,
            item.ExaminationDate,
            item.NextExaminationDate,
            item.Result,
            item.WorkRestrictionIssued
        }));
    }

    /// <summary>Returns one of the caller's own surveillance records, findings included.</summary>
    /// <remarks>The subject sees the full record — findings, recommendations, restriction detail —
    /// the natural-justice rule every subject-facing read in the module follows. DocumentPath is a
    /// server path and RecordedBy an internal actor; neither is projected.</remarks>
    [HttpGet("surveillance/{id:guid}")]
    public async Task<IActionResult> GetOwnSurveillanceRecord(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out var tenantId, out _, out var employeeId) is { } error)
            return error;

        // Filtered on the caller's employee id in the same query, so somebody else's record id is
        // a 404 lookup miss, never a 403.
        var row = await _db.Set<SheOccupationalHealthSurveillance>()
            .AsNoTracking()
            .Include(item => item.HealthcareFacility)
            .SingleOrDefaultAsync(
                item => item.Id == id &&
                        item.EmployeeId == employeeId &&
                        item.TenantId == tenantId &&
                        !item.IsDeleted, ct);
        if (row is null)
            return NotFound();

        return Ok(new
        {
            row.Id,
            row.SurveillanceNumber,
            row.Type,
            row.ExposureHazard,
            row.ExaminationDate,
            row.NextExaminationDate,
            HealthcareFacilityName = row.HealthcareFacility?.FacilityName,
            row.ExaminingPhysician,
            row.Result,
            row.Findings,
            row.Recommendations,
            row.WorkRestrictionIssued,
            row.WorkRestrictionDetails
        });
    }

    /// <summary>
    /// Resolves the health profile belonging to the authenticated employee. Every profile-keyed
    /// read on this controller goes through here — it is the single point where "own" is defined.
    /// (Surveillance is keyed by employee id directly and filters in its own query.)
    /// </summary>
    private Task<EmployeeHealthProfile?> LoadOwnProfileAsync(
        Guid tenantId, Guid employeeId, CancellationToken ct)
        => _db.Set<EmployeeHealthProfile>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.EmployeeId == employeeId &&
                        item.TenantId == tenantId &&
                        !item.IsDeleted,
                ct);
}
