using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class MentoringService : IMentoringService
{
    private readonly IMentoringProgramRepository _programRepository;
    private readonly IMentoringPairRepository _pairRepository;
    private readonly IMentoringSessionRepository _sessionRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MentoringService> _logger;

    public MentoringService(
        IMentoringProgramRepository programRepository,
        IMentoringPairRepository pairRepository,
        IMentoringSessionRepository sessionRepository,
        IGenericRepository<Employee> employeeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<MentoringService> logger)
    {
        _programRepository = programRepository;
        _pairRepository = pairRepository;
        _sessionRepository = sessionRepository;
        _employeeRepository = employeeRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A row owned by another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<MentoringProgram> GetOwnedProgramAsync(Guid id)
    {
        var entity = await _programRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Mentoring program with ID '{id}' not found.");
        return entity;
    }

    private async Task<MentoringProgram> GetOwnedProgramWithDetailsAsync(Guid id)
    {
        var entity = await _programRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Mentoring program with ID '{id}' not found.");
        return entity;
    }

    private async Task<MentoringPair> GetOwnedPairAsync(Guid id)
    {
        var entity = await _pairRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Mentoring pair with ID '{id}' not found.");
        return entity;
    }

    private async Task<MentoringPair> GetOwnedPairWithDetailsAsync(Guid id)
    {
        var entity = await _pairRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Mentoring pair with ID '{id}' not found.");
        return entity;
    }

    private async Task<MentoringSession> GetOwnedSessionAsync(Guid id)
    {
        var entity = await _sessionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Mentoring session with ID '{id}' not found.");
        return entity;
    }

    // ── Program queries ───────────────────────────────────────────────────────

    public async Task<MentoringProgramDto> GetProgramByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramWithDetailsAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<MentoringProgramSummaryDto>> GetAllProgramsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _programRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<MentoringProgramSummaryDto>> GetActiveProgramsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _programRepository.GetActiveAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Program CRUD ──────────────────────────────────────────────────────────

    public async Task<MentoringProgramDto> CreateProgramAsync(CreateMentoringProgramDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring program created: {ProgramName}", dto.ProgramName);

        return entity.ToDto();
    }

    public async Task<MentoringProgramDto> UpdateProgramAsync(UpdateMentoringProgramDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(dto.Id);

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(id);

        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring program {ProgramId} deleted", id);

        return true;
    }

    // ── Pair queries ──────────────────────────────────────────────────────────

    public async Task<MentoringPairDto> GetPairByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPairWithDetailsAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<MentoringPairSummaryDto>> GetActivePairsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pairRepository.GetActiveAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<MentoringPairSummaryDto>> GetPairsForProgramAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pairRepository.GetByProgramIdAsync(programId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<MentoringPairSummaryDto>> GetPairsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var asMentor = await _pairRepository.GetByMentorIdAsync(employeeId);
        var asMentee = await _pairRepository.GetByMenteeIdAsync(employeeId);

        return asMentor.Concat(asMentee)
            .Where(p => p.TenantId == tenantId)
            .DistinctBy(p => p.Id)
            .ToSummaryDtoList();
    }

    // ── Pair CRUD & workflow ──────────────────────────────────────────────────

    public async Task<MentoringPairDto> CreatePairAsync(CreateMentoringPairDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedProgramAsync(dto.ProgramId);

        var mentor = await _employeeRepository.GetByIdAsync(dto.MentorId);
        if (mentor == null || mentor.TenantId != current)
            throw new ArgumentException($"Employee with ID '{dto.MentorId}' not found.");

        var mentee = await _employeeRepository.GetByIdAsync(dto.MenteeId);
        if (mentee == null || mentee.TenantId != current)
            throw new ArgumentException($"Employee with ID '{dto.MenteeId}' not found.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _pairRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring pair created: mentor {MentorId} — mentee {MenteeId}", dto.MentorId, dto.MenteeId);

        return entity.ToDto();
    }

    public async Task<MentoringPairDto> UpdatePairAsync(UpdateMentoringPairDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPairAsync(dto.Id);

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _pairRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> ClosePairAsync(Guid pairId, string closureNotes, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPairAsync(pairId);

        if (entity.Status == MentoringStatus.Completed || entity.Status == MentoringStatus.Cancelled)
            throw new InvalidOperationException($"Mentoring pair is already closed with status '{entity.Status}'.");

        entity.Status = MentoringStatus.Completed;
        entity.ClosureNotes = closureNotes;
        entity.EndDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _pairRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring pair {PairId} closed", pairId);

        return true;
    }

    // ── Session sub-operations ────────────────────────────────────────────────

    public async Task<MentoringSessionDto> GetSessionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(id);
        return entity.ToDto();
    }

    public async Task<MentoringSessionDto> LogSessionAsync(CreateMentoringSessionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedPairAsync(dto.PairId);

        var entity = dto.ToEntity(current, createdByUserId);

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring session added for pair {PairId}", dto.PairId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<MentoringSessionDto>> GetSessionsForPairAsync(Guid pairId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _sessionRepository.GetByPairIdAsync(pairId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<MentoringSessionDto> UpdateSessionAsync(UpdateMentoringSessionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(dto.Id);

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(sessionId);

        await _sessionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
