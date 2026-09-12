using System.Security.Cryptography;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffGrievance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Anonymous / whistleblower intake — area 9c slice 6. See
/// <see cref="IEmployeeRelationsConcernService"/> for what must never be recorded here.
/// </summary>
public class EmployeeRelationsConcernService : IEmployeeRelationsConcernService
{
    /// <summary>
    /// Retrieval-code alphabet. No <c>0/O</c>, <c>1/I/L</c>, <c>5/S</c>, <c>8/B</c> — the code is
    /// read off a screen and typed back in, often written down first, and a reporter who mistypes
    /// it has permanently lost their thread.
    /// </summary>
    private const string CodeAlphabet = "ACDEFGHJKMNPQRTUVWXY2346789";

    private const int CodeLength = 16;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Pbkdf2Iterations = 100_000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EmployeeRelationsConcernService> _logger;

    public EmployeeRelationsConcernService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<EmployeeRelationsConcernService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private IQueryable<EmployeeRelationsConcern> Scoped(Guid tenantId) =>
        _unitOfWork.Repository<EmployeeRelationsConcern>()
            .GetQueryable()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Include(c => c.TriagedBy)
            .Include(c => c.ClosedBy)
            .Include(c => c.ConvertedCase)
            .Include(c => c.Updates.Where(u => !u.IsDeleted)).ThenInclude(u => u.Author)
            .AsSplitQuery();

    // ── The reporter's side — nothing here learns who they are ────────────────

    public async Task<ConcernReceiptDto> ReportAsync(
        ReportConcernDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;

        var code = GenerateCode();
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);

        var concern = new EmployeeRelationsConcern
        {
            TenantId = tenantId,
            ConcernNumber = await GenerateNumberAsync(tenantId, cancellationToken),
            Category = dto.Category,
            Subject = dto.Subject.Trim(),
            Statement = dto.Statement.Trim(),
            Status = ConcernStatus.New,
            ReportedAt = now,
            RetrievalCodeSalt = Convert.ToBase64String(salt),
            RetrievalCodeHash = Convert.ToBase64String(HashCode(code, salt)),
            // ⚠ CreatedBy and CreatedById are deliberately NOT set, and there is no employee id to
            // set them from — ReportAsync takes no actor parameter at all. That absence is the
            // feature; do not "fix" it by threading the caller through for consistency.
        };

        await _unitOfWork.Repository<EmployeeRelationsConcern>().AddAsync(concern);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ⚠ The log line carries the NUMBER and the CATEGORY and nothing else — no subject, no
        // statement, no caller. A log entry travels further than the record it is about, and this
        // is the one record whose whole value is that nobody can trace it back.
        _logger.LogInformation("Anonymous concern reported: {Number} ({Category})",
            concern.ConcernNumber, concern.Category);

        return new ConcernReceiptDto
        {
            ConcernNumber = concern.ConcernNumber,
            RetrievalCode = code,
            ReportedAt = now,
        };
    }

    public async Task<EmployeeRelationsConcernDto> TrackAsync(
        TrackConcernDto dto, CancellationToken cancellationToken = default)
        => ToDto(await UnlockAsync(dto.ConcernNumber, dto.RetrievalCode, cancellationToken));

    public async Task<EmployeeRelationsConcernDto> AddReporterUpdateAsync(
        AddConcernUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var concern = await UnlockAsync(dto.ConcernNumber, dto.RetrievalCode, cancellationToken);

        if (concern.Status is ConcernStatus.Closed or ConcernStatus.ConvertedToCase)
            throw new InvalidOperationException(
                $"This concern is {concern.Status} and no longer accepts messages.");

        await _unitOfWork.Repository<EmployeeRelationsConcernUpdate>().AddAsync(new EmployeeRelationsConcernUpdate
        {
            TenantId = concern.TenantId,
            ConcernId = concern.Id,
            IsFromReporter = true,
            // ⚠ AuthorEmployeeId stays null. IsFromReporter is what marks a reporter's message —
            // never the absence of an author, which would make that absence the tell.
            Body = dto.Body.Trim(),
            PostedAt = DateTime.UtcNow,
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(concern.Id, cancellationToken));
    }

    /// <summary>
    /// Finds a concern by number and code.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>The same refusal whichever half is wrong.</b> Distinguishing "no such concern" from
    /// "wrong code" would turn this endpoint into an oracle for whether a given number exists, which
    /// is a slow but perfectly good way to discover that somebody reported something. The comparison
    /// is also fixed-time.
    /// </remarks>
    private async Task<EmployeeRelationsConcern> UnlockAsync(
        string concernNumber, string retrievalCode, CancellationToken cancellationToken)
    {
        const string refusal = "That concern number and retrieval code do not match a report.";

        var concern = await Scoped(GetTenantId())
            .FirstOrDefaultAsync(c => c.ConcernNumber == concernNumber.Trim(), cancellationToken);

        if (concern == null) throw new UnauthorizedAccessException(refusal);

        var salt = Convert.FromBase64String(concern.RetrievalCodeSalt);
        var expected = Convert.FromBase64String(concern.RetrievalCodeHash);
        var actual = HashCode(retrievalCode.Trim().ToUpperInvariant(), salt);

        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new UnauthorizedAccessException(refusal);

        return concern;
    }

    // ── HR's side, all attributed ─────────────────────────────────────────────

    public async Task<IEnumerable<EmployeeRelationsConcernDto>> GetAllAsync(
        ConcernStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = Scoped(GetTenantId());
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);

        return (await query.OrderByDescending(c => c.ReportedAt).ToListAsync(cancellationToken))
            .Select(ToDto).ToList();
    }

    public async Task<EmployeeRelationsConcernDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ToDto(await GetOwnedAsync(id, cancellationToken));

    public async Task<EmployeeRelationsConcernDto> TriageAsync(
        Guid id, TriageConcernDto dto, Guid triagedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var concern = await GetOwnedAsync(id, cancellationToken);
        EnsureOpen(concern);

        if (dto.Status is not (ConcernStatus.UnderTriage or ConcernStatus.UnderReview or ConcernStatus.Closed))
            throw new InvalidOperationException(
                "Triage can leave a concern under triage, under review, or closed. Converting it to a "
                + "case is a separate act.");

        var now = DateTime.UtcNow;
        concern.TriageNotes = dto.Notes.Trim();
        concern.TriagedAt = now;
        concern.TriagedById = triagedByEmployeeId;
        concern.Status = dto.Status;
        concern.UpdatedAt = now;

        if (dto.Status == ConcernStatus.Closed)
        {
            concern.ClosedAt = now;
            concern.ClosedById = triagedByEmployeeId;
            concern.ClosureReason = dto.Notes.Trim();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(id, cancellationToken));
    }

    public async Task<EmployeeRelationsConcernDto> ReplyAsync(
        Guid id, ReplyToConcernDto dto, Guid authorEmployeeId, CancellationToken cancellationToken = default)
    {
        var concern = await GetOwnedAsync(id, cancellationToken);
        EnsureOpen(concern);

        await _unitOfWork.Repository<EmployeeRelationsConcernUpdate>().AddAsync(new EmployeeRelationsConcernUpdate
        {
            TenantId = concern.TenantId,
            ConcernId = concern.Id,
            IsFromReporter = false,
            AuthorEmployeeId = authorEmployeeId,
            Body = dto.Body.Trim(),
            PostedAt = DateTime.UtcNow,
            CreatedBy = authorEmployeeId.ToString(),
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(id, cancellationToken));
    }

    public async Task<EmployeeRelationsConcernDto> CloseAsync(
        Guid id, CloseConcernDto dto, Guid closedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var concern = await GetOwnedAsync(id, cancellationToken);
        EnsureOpen(concern);

        var now = DateTime.UtcNow;
        concern.Status = ConcernStatus.Closed;
        concern.ClosedAt = now;
        concern.ClosureReason = dto.Reason.Trim();
        concern.ClosedById = closedByEmployeeId;
        concern.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(id, cancellationToken));
    }

    public async Task<EmployeeRelationsConcernDto> ConvertAsync(
        Guid id, ConvertConcernDto dto, Guid convertedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var concern = await GetOwnedAsync(id, cancellationToken);
        EnsureOpen(concern);

        // ⚠ The same refusal as OpenCaseAsync, for the same reason. A grievance is the employee's
        // own act; converting a concern into one would be HR raising a grievance on somebody's
        // behalf, arrived at by a longer route.
        if (dto.CaseType == EmployeeRelationsCaseType.Grievance)
            throw new InvalidOperationException(
                "A concern cannot become a grievance: a grievance can only be raised by the employee "
                + "it belongs to. Convert it to another case type, or ask the reporter to file one.");

        var tenantId = concern.TenantId;
        var subject = await _unitOfWork.Repository<Employee>().GetByIdAsync(dto.EmployeeId);
        if (subject == null || subject.IsDeleted || subject.TenantId != tenantId)
            throw new ArgumentException($"The employee with ID '{dto.EmployeeId}' was not found.");

        var now = DateTime.UtcNow;

        // ⚠ The case carries the concern's SUBJECT and STATEMENT, and nothing else from it — not
        // the thread, which may contain things the reporter said only because they were anonymous,
        // and which would become readable by the case's primary party.
        var caseNumber = await GenerateCaseNumberAsync(tenantId, cancellationToken);
        var newCase = new StaffGrievance
        {
            TenantId = tenantId,
            GrievanceNumber = caseNumber,
            CaseType = dto.CaseType,
            EmployeeId = subject.Id,
            Subject = concern.Subject,
            Statement = concern.Statement,
            FiledDate = now,
            Status = GrievanceStatus.Filed,
            CurrentLevel = GrievanceEscalationLevel.HumanResources,
            CreatedBy = convertedByEmployeeId.ToString(),
        };

        newCase.Steps.Add(new StaffGrievanceStep
        {
            TenantId = tenantId,
            Level = GrievanceEscalationLevel.HumanResources,
            Sequence = 1,
            ReachedDate = now,
            Outcome = GrievanceStepOutcome.AwaitingResponse,
            CreatedBy = convertedByEmployeeId.ToString(),
        });

        await _unitOfWork.Repository<StaffGrievance>().AddAsync(newCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        concern.Status = ConcernStatus.ConvertedToCase;
        concern.ConvertedCaseId = newCase.Id;
        concern.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Concern {Number} converted to case {Case}",
            concern.ConcernNumber, caseNumber);

        return ToDto(await GetOwnedAsync(id, cancellationToken));
    }

    // ── Plumbing ──────────────────────────────────────────────────────────────

    private async Task<EmployeeRelationsConcern> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
        => await Scoped(GetTenantId()).FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
           ?? throw new ArgumentException($"Concern with ID '{id}' was not found.");

    private static void EnsureOpen(EmployeeRelationsConcern concern)
    {
        if (concern.Status is ConcernStatus.Closed or ConcernStatus.ConvertedToCase)
            throw new InvalidOperationException($"This concern is {concern.Status} and cannot be changed.");
    }

    /// <summary>Cryptographically random, from an alphabet chosen for being read and retyped.</summary>
    private static string GenerateCode()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }

    private static byte[] HashCode(string code, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(code, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashBytes);

    /// <remarks>
    /// Over rows INCLUDING soft-deleted ones — counting live rows re-issues a number the moment
    /// anything is deleted, the shape fixed across every other generator in this module.
    /// </remarks>
    private async Task<string> GenerateNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"CON-{DateTime.UtcNow.Year}-";
        var issued = await _unitOfWork.Repository<EmployeeRelationsConcern>()
            .GetQueryableIncludingDeleted(c => c.TenantId == tenantId && c.ConcernNumber.StartsWith(prefix))
            .Select(c => c.ConcernNumber)
            .ToListAsync(cancellationToken);

        var highest = issued
            .Select(n => int.TryParse(n[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D5}";
    }

    private async Task<string> GenerateCaseNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"GRV-{DateTime.UtcNow.Year}-";
        var issued = await _unitOfWork.Repository<StaffGrievance>()
            .GetQueryableIncludingDeleted(g => g.TenantId == tenantId && g.GrievanceNumber.StartsWith(prefix))
            .Select(g => g.GrievanceNumber)
            .ToListAsync(cancellationToken);

        var highest = issued
            .Select(n => int.TryParse(n[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D5}";
    }

    private static EmployeeRelationsConcernDto ToDto(EmployeeRelationsConcern c) => new()
    {
        Id = c.Id,
        ConcernNumber = c.ConcernNumber,
        Category = c.Category,
        Subject = c.Subject,
        Statement = c.Statement,
        Status = c.Status,
        ReportedAt = c.ReportedAt,
        TriageNotes = c.TriageNotes,
        TriagedAt = c.TriagedAt,
        TriagedByName = c.TriagedBy?.FullName,
        ClosedAt = c.ClosedAt,
        ClosureReason = c.ClosureReason,
        ClosedByName = c.ClosedBy?.FullName,
        ConvertedCaseId = c.ConvertedCaseId,
        ConvertedCaseNumber = c.ConvertedCase?.GrievanceNumber,
        Updates = c.Updates
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.PostedAt)
            .Select(u => new ConcernUpdateDto
            {
                Id = u.Id,
                ConcernId = u.ConcernId,
                IsFromReporter = u.IsFromReporter,
                // ⚠ Only HR's messages have an author, and only HR's messages show one.
                AuthorName = u.IsFromReporter ? null : u.Author?.FullName,
                Body = u.Body,
                PostedAt = u.PostedAt,
            })
            .ToList(),
    };
}
