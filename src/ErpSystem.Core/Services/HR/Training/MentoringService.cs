using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MentoringService> _logger;

    public MentoringService(
        IMentoringProgramRepository programRepository,
        IMentoringPairRepository pairRepository,
        IMentoringSessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<MentoringService> logger)
    {
        _programRepository = programRepository;
        _pairRepository = pairRepository;
        _sessionRepository = sessionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Program queries ───────────────────────────────────────────────────────

    public async Task<MentoringProgramDto> GetProgramByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Mentoring program with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<MentoringProgramSummaryDto>> GetAllProgramsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _programRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MentoringProgramSummaryDto>> GetActiveProgramsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _programRepository.GetActiveAsync();
        return entities.ToSummaryDtoList();
    }

    // ── Program CRUD ──────────────────────────────────────────────────────────

    public async Task<MentoringProgramDto> CreateProgramAsync(CreateMentoringProgramDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring program created: {ProgramName}", dto.ProgramName);

        return entity.ToDto();
    }

    public async Task<MentoringProgramDto> UpdateProgramAsync(UpdateMentoringProgramDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Mentoring program with ID '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Mentoring program with ID '{id}' not found.");

        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring program {ProgramId} deleted", id);

        return true;
    }

    // ── Pair queries ──────────────────────────────────────────────────────────

    public async Task<MentoringPairDto> GetPairByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _pairRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Mentoring pair with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<MentoringPairSummaryDto>> GetActivePairsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _pairRepository.GetActiveAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MentoringPairSummaryDto>> GetPairsForProgramAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var entities = await _pairRepository.GetByProgramIdAsync(programId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MentoringPairSummaryDto>> GetPairsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var asMentor = await _pairRepository.GetByMentorIdAsync(employeeId);
        var asMentee = await _pairRepository.GetByMenteeIdAsync(employeeId);

        return asMentor.Concat(asMentee)
            .DistinctBy(p => p.Id)
            .ToSummaryDtoList();
    }

    // ── Pair CRUD & workflow ──────────────────────────────────────────────────

    public async Task<MentoringPairDto> CreatePairAsync(CreateMentoringPairDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _pairRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring pair created: mentor {MentorId} — mentee {MenteeId}", dto.MentorId, dto.MenteeId);

        return entity.ToDto();
    }

    public async Task<MentoringPairDto> UpdatePairAsync(UpdateMentoringPairDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _pairRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Mentoring pair with ID '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _pairRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> ClosePairAsync(Guid pairId, string closureNotes, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _pairRepository.GetByIdAsync(pairId);

        if (entity == null)
            throw new ArgumentException($"Mentoring pair with ID '{pairId}' not found.");

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
        var entity = await _sessionRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Mentoring session with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<MentoringSessionDto> LogSessionAsync(CreateMentoringSessionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var pair = await _pairRepository.GetByIdAsync(dto.PairId);

        if (pair == null)
            throw new ArgumentException($"Mentoring pair with ID '{dto.PairId}' not found.");

        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring session added for pair {PairId}", dto.PairId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<MentoringSessionDto>> GetSessionsForPairAsync(Guid pairId, CancellationToken cancellationToken = default)
    {
        var entities = await _sessionRepository.GetByPairIdAsync(pairId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<MentoringSessionDto> UpdateSessionAsync(UpdateMentoringSessionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Mentoring session with ID '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entity = await _sessionRepository.GetByIdAsync(sessionId);

        if (entity == null)
            throw new ArgumentException($"Mentoring session with ID '{sessionId}' not found.");

        await _sessionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
