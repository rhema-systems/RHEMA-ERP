using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Records oaths of secrecy (FRD FR-HR-030, priority M).
/// </summary>
/// <remarks>
/// <para>Two paths, kept apart on purpose. <b>Affirm</b> is the employee's own act — server-stamped,
/// with their IP and a tamper hash, and available to nobody else. <b>Administer</b> is HR recording
/// a paper oath after the fact, which requires a named witness because "sworn before" is what makes
/// it an oath rather than a note.</para>
///
/// <para>⚠ HR cannot affirm on someone's behalf. That is the same rule area 8 applies to accepting a
/// movement and area 9 to acknowledging a disciplinary decision, and the reason area 15's
/// acknowledgement bug mattered: signing "I swear" in another person's name, with their IP recorded,
/// is the one thing this record must make impossible.</para>
/// </remarks>
public class EmployeeOathOfSecrecyService : IEmployeeOathOfSecrecyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EmployeeOathOfSecrecyService> _logger;

    /// <summary>
    /// The wording used when a caller supplies none.
    /// </summary>
    /// <remarks>
    /// A default rather than a required field: FR-HR-030 asks that the oath be recorded, and a
    /// tenant that has not yet agreed its own wording should still be able to record one. Whatever
    /// is used is snapshotted onto the row, so changing this never rewrites an oath already sworn.
    /// </remarks>
    public const string DefaultOathText =
        "I solemnly swear that I will not, during or after my employment, disclose to any "
        + "unauthorised person any information which comes to my knowledge in the course of my "
        + "duties, and that I will handle all records and information entrusted to me in "
        + "confidence and solely for the purposes of my employment.";

    public EmployeeOathOfSecrecyService(
        IUnitOfWork unitOfWork,
        IEmployeeRepository employeeRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<EmployeeOathOfSecrecyService> logger)
    {
        _unitOfWork = unitOfWork;
        _employeeRepository = employeeRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private IQueryable<EmployeeOathOfSecrecy> Scoped(Guid tenantId)
        => _unitOfWork.Repository<EmployeeOathOfSecrecy>().GetQueryable()
            .Include(o => o.Employee)
            .Include(o => o.WitnessedBy)
            .Include(o => o.RecordedBy)
            .Where(o => o.TenantId == tenantId && !o.IsDeleted);

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<EmployeeOathOfSecrecyDto>> GetForEmployeeAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(GetTenantId())
            .Where(o => o.EmployeeId == employeeId)
            .OrderByDescending(o => o.SwornOn)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OathOutstandingEmployeeDto>> GetOutstandingAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var sworn = await _unitOfWork.Repository<EmployeeOathOfSecrecy>().GetQueryable()
            .Where(o => o.TenantId == tenantId && !o.IsDeleted)
            .Select(o => o.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var swornSet = sworn.ToHashSet();

        // Only people currently employed: chasing an oath from a leaver is noise, and FR-HR-030 is
        // an onboarding requirement.
        var employees = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive)
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.EmployeeNumber, e.DateEmployed, e.StaffStatus })
            .ToListAsync(cancellationToken);

        return employees
            .Where(e => !swornSet.Contains(e.Id))
            .Select(e => new OathOutstandingEmployeeDto
            {
                EmployeeId = e.Id,
                EmployeeName = $"{e.FirstName} {e.LastName}".Trim(),
                EmployeeNumber = e.EmployeeNumber,
                DateEmployed = e.DateEmployed,
                IsOnProbation = e.StaffStatus == StaffStatus.Probation,
            })
            .OrderByDescending(e => e.DateEmployed)
            .ToList();
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<EmployeeOathOfSecrecyDto> AffirmAsync(
        AffirmOathOfSecrecyDto dto, Guid actorEmployeeId, string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var employee = await RequireEmployeeAsync(tenantId, actorEmployeeId);

        // ⚠ No employeeId on the payload, and that is the design. The person affirming is the person
        // on the token — there is deliberately nothing here to point at somebody else.
        var text = string.IsNullOrWhiteSpace(dto.OathText) ? DefaultOathText : dto.OathText.Trim();
        var now = DateTime.UtcNow;

        var oath = new EmployeeOathOfSecrecy
        {
            TenantId = tenantId,
            EmployeeId = employee.Id,
            Method = OathAdministrationMethod.Affirmed,
            OathText = text,
            SwornOn = DateOnly.FromDateTime(now),   // server-stamped: you cannot back-date your own oath
            RecordedAt = now,
            RecordedById = employee.Id,
            SignatureIpAddress = ipAddress,
            SignatureHash = Hash(employee.Id, text, now),
            Notes = dto.Notes,
        };

        await _unitOfWork.Repository<EmployeeOathOfSecrecy>().AddAsync(oath);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Oath of secrecy affirmed by employee {EmployeeId}", employee.Id);
        return ToDto(await ReloadAsync(tenantId, oath.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<EmployeeOathOfSecrecyDto> RecordAdministeredAsync(
        RecordAdministeredOathDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // ⚠ Checked here, not left to [Required]: that attribute is a no-op on a non-nullable Guid,
        // so an omitted witness arrives as Guid.Empty and would otherwise be reported as "employee
        // 00000000-... was not found" — an answer about the wrong thing, which is worse than none.
        if (dto.EmployeeId == Guid.Empty)
            throw ProbationWorkflowException.Invalid("Name the employee whose oath this is.");
        if (dto.WitnessedById == Guid.Empty)
            throw ProbationWorkflowException.Invalid(
                "A witness is required: an oath sworn before nobody is a note, not an oath.");

        var employee = await RequireEmployeeAsync(tenantId, dto.EmployeeId);
        await RequireEmployeeAsync(tenantId, dto.WitnessedById);
        await RequireEmployeeAsync(tenantId, actorEmployeeId);

        if (dto.SwornOn > DateOnly.FromDateTime(DateTime.UtcNow))
            throw ProbationWorkflowException.Invalid("An oath cannot be recorded as sworn in the future.");

        // DateEmployed is already a DateOnly — no conversion, and none of the timezone ambiguity
        // that converting would quietly introduce.
        if (employee.DateEmployed is { } employed && dto.SwornOn < employed)
            throw ProbationWorkflowException.Invalid(
                $"The oath is dated {dto.SwornOn:yyyy-MM-dd}, before this employee joined on {employed:yyyy-MM-dd}.");

        var text = string.IsNullOrWhiteSpace(dto.OathText) ? DefaultOathText : dto.OathText.Trim();

        var oath = new EmployeeOathOfSecrecy
        {
            TenantId = tenantId,
            EmployeeId = employee.Id,
            Method = OathAdministrationMethod.Administered,
            OathText = text,
            SwornOn = dto.SwornOn,
            RecordedAt = DateTime.UtcNow,
            WitnessedById = dto.WitnessedById,
            RecordedById = actorEmployeeId,
            // ⚠ No IP and no signature hash on this path. Those attest to somebody clicking; here the
            // attestation is the witness and the scanned document, and pretending otherwise would
            // dress an HR data-entry row as the employee's own act.
            Notes = dto.Notes,
        };

        await _unitOfWork.Repository<EmployeeOathOfSecrecy>().AddAsync(oath);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Paper oath of secrecy recorded for employee {EmployeeId}, witnessed by {WitnessId}",
            employee.Id, dto.WitnessedById);
        return ToDto(await ReloadAsync(tenantId, oath.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<EmployeeOathOfSecrecyDto> AttachScanAsync(
        Guid oathId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string fileName, string mimeType, long fileSizeBytes, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var oath = await ReloadAsync(tenantId, oathId, cancellationToken);

        // Replacing a scan is allowed — a better copy of the same signed page is an improvement,
        // not a rewrite. What cannot change is what was sworn, by whom, or when.
        oath.FileUploadRecordId = fileUploadRecordId;
        oath.DocumentRecordId = documentRecordId;
        oath.DocumentVersionId = documentVersionId;
        oath.FileName = fileName;
        oath.MimeType = mimeType;
        oath.FileSizeBytes = fileSizeBytes;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Scan attached to oath {OathId} for employee {EmployeeId}", oath.Id, oath.EmployeeId);
        return ToDto(await ReloadAsync(tenantId, oathId, cancellationToken));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Employee> RequireEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId)
            throw ProbationWorkflowException.NotFound($"Employee '{employeeId}' was not found.");
        return employee;
    }

    private async Task<EmployeeOathOfSecrecy> ReloadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await Scoped(tenantId).FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
           ?? throw ProbationWorkflowException.NotFound($"Oath '{id}' was not found.");

    /// <summary>Tamper detection over the three things that make the affirmation what it is.</summary>
    private static string Hash(Guid employeeId, string text, DateTime signedAt)
    {
        var payload = $"{employeeId:N}|{text}|{signedAt:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static EmployeeOathOfSecrecyDto ToDto(EmployeeOathOfSecrecy o) => new()
    {
        Id = o.Id,
        EmployeeId = o.EmployeeId,
        EmployeeName = o.Employee?.FullName ?? string.Empty,
        EmployeeNumber = o.Employee?.EmployeeNumber ?? string.Empty,
        Method = o.Method,
        OathText = o.OathText,
        SwornOn = o.SwornOn,
        RecordedAt = o.RecordedAt,
        WitnessedById = o.WitnessedById,
        WitnessedByName = o.WitnessedBy?.FullName,
        RecordedById = o.RecordedById,
        RecordedByName = o.RecordedBy?.FullName ?? string.Empty,
        HasSignature = !string.IsNullOrWhiteSpace(o.SignatureHash),
        FileUploadRecordId = o.FileUploadRecordId,
        DocumentRecordId = o.DocumentRecordId,
        FileName = o.FileName,
        MimeType = o.MimeType,
        FileSizeBytes = o.FileSizeBytes,
        Notes = o.Notes,
    };
}
