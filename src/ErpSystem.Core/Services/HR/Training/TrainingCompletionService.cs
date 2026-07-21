using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingCompletionService : ITrainingCompletionService
{
    private readonly ITrainingCompletionRepository _completionRepository;
    private readonly ITrainingCertificateRepository _certificateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingCompletionService> _logger;

    private readonly INumberSequenceService _numberSequence;

    private readonly IGenericRepository<TrainingNomination> _nominationRepo;
    private readonly IGenericRepository<TrainingProgramSkill> _programSkillRepo;
    private readonly IGenericRepository<EmployeeSkill> _employeeSkillRepo;

    public TrainingCompletionService(
        ITrainingCompletionRepository completionRepository,
        ITrainingCertificateRepository certificateRepository,
        IGenericRepository<TrainingNomination> nominationRepo,
        IGenericRepository<TrainingProgramSkill> programSkillRepo,
        IGenericRepository<EmployeeSkill> employeeSkillRepo,
        IUnitOfWork unitOfWork,
        INumberSequenceService numberSequence,
        ILogger<TrainingCompletionService> logger)
    {
        _completionRepository = completionRepository;
        _certificateRepository = certificateRepository;
        _nominationRepo = nominationRepo;
        _programSkillRepo = programSkillRepo;
        _employeeSkillRepo = employeeSkillRepo;
        _unitOfWork = unitOfWork;
        _numberSequence = numberSequence;
        _logger = logger;
    }

    // ── Completion queries ────────────────────────────────────────────────────

    public async Task<TrainingCompletionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _completionRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training completion record with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingCompletionDto?> GetByNominationIdAsync(Guid nominationId, CancellationToken cancellationToken = default)
    {
        var entity = await _completionRepository.GetByNominationIdAsync(nominationId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<TrainingCompletionDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _completionRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingCompletionDto>> GetByScheduleIdAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var entities = await _completionRepository.GetByScheduleIdAsync(scheduleId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingCompletionDto>> GetPendingVerificationAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _completionRepository.GetPendingManagerVerificationAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    // ── Completion recording ──────────────────────────────────────────────────

    public async Task<TrainingCompletionDto> RecordCompletionAsync(RecordTrainingCompletionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var existing = await _completionRepository.GetByNominationIdAsync(dto.NominationId);

        if (existing != null)
            throw new InvalidOperationException($"A completion record already exists for nomination ID '{dto.NominationId}'.");

        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _completionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training completion recorded for nomination {NominationId}", dto.NominationId);

        return entity.ToDto();
    }

    public async Task<BulkCompletionResultDto> BulkRecordCompletionAsync(BulkRecordCompletionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var result = new BulkCompletionResultDto { RequestedCount = dto.Items.Count };

        // Nominations for this schedule that already have a completion, so we don't double-record.
        var existing = (await _completionRepository.GetByScheduleIdAsync(dto.ScheduleId))
            .Select(c => c.NominationId).ToHashSet();

        foreach (var item in dto.Items)
        {
            if (existing.Contains(item.NominationId))
            {
                result.Skipped.Add(new BulkCompletionSkipDto { NominationId = item.NominationId, Reason = "Completion already recorded." });
                continue;
            }

            var entity = new RecordTrainingCompletionDto
            {
                NominationId = item.NominationId,
                EmployeeId = item.EmployeeId,
                CompletionDate = item.CompletionDate,
                Status = item.Status,
                FinalScore = item.FinalScore,
                IsPassed = item.IsPassed
            }.ToEntity(tenantId, createdByUserId);

            await _completionRepository.AddAsync(entity);
            existing.Add(item.NominationId);
            result.CreatedCount++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bulk completion for schedule {ScheduleId}: {Created} created, {Skipped} skipped",
            dto.ScheduleId, result.CreatedCount, result.Skipped.Count);

        return result;
    }

    public async Task<TrainingCompletionDto> UpdateAsync(UpdateTrainingCompletionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _completionRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Training completion with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _completionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training completion {CompletionId} updated", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<TrainingCompletionDto> VerifyCompletionAsync(VerifyTrainingCompletionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _completionRepository.GetByIdAsync(dto.CompletionId);

        if (entity == null)
            throw new ArgumentException($"Training completion with ID '{dto.CompletionId}' not found.");

        if (entity.IsVerifiedByManager)
            throw new InvalidOperationException("This completion record has already been verified.");

        entity.IsVerifiedByManager = true;
        entity.VerifiedById = dto.VerifiedById;
        entity.VerificationDate = dto.VerificationDate;
        entity.VerificationNotes = dto.VerificationNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _completionRepository.UpdateAsync(entity);

        // Profile write-back: a verified, passed completion adds the programme's target skills to the
        // employee's profile (same SaveChanges → atomic with the verification).
        if (entity.IsPassed)
            await WriteBackSkillsAsync(entity, updatedByUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training completion {CompletionId} verified by {VerifierId}", dto.CompletionId, dto.VerifiedById);

        return entity.ToDto();
    }

    /// <summary>Upserts the training programme's target skills onto the employee's profile.</summary>
    private async Task WriteBackSkillsAsync(TrainingCompletion completion, Guid userId, CancellationToken ct)
    {
        var nomination = await _nominationRepo.GetQueryable()
            .Include(n => n.Schedule)
            .FirstOrDefaultAsync(n => n.Id == completion.NominationId, ct);
        if (nomination?.Schedule == null) return;

        var programSkills = await _programSkillRepo.GetQueryable()
            .Where(ps => ps.ProgramId == nomination.Schedule.ProgramId)
            .ToListAsync(ct);
        if (programSkills.Count == 0) return;

        var existing = await _employeeSkillRepo.GetQueryable()
            .Where(es => es.EmployeeId == completion.EmployeeId)
            .ToListAsync(ct);

        var acquired = DateOnly.FromDateTime(completion.CompletionDate);

        foreach (var ps in programSkills)
        {
            // Proficiency (1-5) maps 1:1 by ordinal onto SkillLevel (1-5).
            var level = (SkillLevel)(int)ps.TargetProficiency;
            var current = existing.FirstOrDefault(es => es.SkillId == ps.SkillId);

            if (current == null)
            {
                await _employeeSkillRepo.AddAsync(new EmployeeSkill
                {
                    TenantId = completion.TenantId,
                    EmployeeId = completion.EmployeeId,
                    SkillId = ps.SkillId,
                    SkillLevel = level,
                    AcquiredDate = acquired,
                    Notes = "Acquired via completed training",
                    CreatedBy = userId.ToString()
                });
            }
            else if ((int)current.SkillLevel < (int)level)
            {
                current.SkillLevel = level;
                current.UpdatedAt = DateTime.UtcNow;
                current.UpdatedBy = userId.ToString();
                await _employeeSkillRepo.UpdateAsync(current);
            }
        }

        _logger.LogInformation("Skill write-back for employee {EmployeeId} from completion {CompletionId}: {Count} programme skill(s)",
            completion.EmployeeId, completion.Id, programSkills.Count);
    }

    // ── Certificate sub-operations ────────────────────────────────────────────

    public async Task<TrainingCertificateDto> IssueCertificateAsync(IssueCertificateDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);
        entity.CertificateNumber = await GenerateCertificateNumberAsync(cancellationToken);
        entity.VerificationCode = await GenerateUniqueVerificationCodeAsync(cancellationToken);
        entity.Status = CertificateStatus.Active;

        await _certificateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training certificate issued: {CertificateNumber} for employee {EmployeeId}", entity.CertificateNumber, dto.EmployeeId);

        return entity.ToDto();
    }

    public async Task<bool> RevokeCertificateAsync(RevokeCertificateDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _certificateRepository.GetByIdAsync(dto.CertificateId);

        if (entity == null)
            throw new ArgumentException($"Training certificate with ID '{dto.CertificateId}' not found.");

        if (entity.Status == CertificateStatus.Revoked)
            throw new InvalidOperationException("Certificate is already revoked.");

        entity.Status = CertificateStatus.Revoked;
        entity.RevokedDate = dto.RevokedDate;
        entity.RevokedReason = dto.RevokedReason;
        entity.RevokedById = dto.RevokedById;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _certificateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training certificate revoked: {CertificateNumber} — Reason: {Reason}", entity.CertificateNumber, dto.RevokedReason);

        return true;
    }

    public async Task<IEnumerable<TrainingCertificateSummaryDto>> GetCertificatesForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _certificateRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingCertificateSummaryDto>> GetExpiringCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _certificateRepository.GetExpiringAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task<string> GenerateCertificateNumberAsync(CancellationToken ct)
        => _numberSequence.GenerateAsync("CERT", ct);

    // Crockford base32 alphabet — omits I, L, O, U to avoid ambiguity when read/typed by a verifier.
    private const string CodeAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Generates a random 12-char verification code and retries until it is globally unique.</summary>
    private async Task<string> GenerateUniqueVerificationCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var code = GenerateVerificationCode();
            var exists = await _certificateRepository.GetQueryable()
                .IgnoreQueryFilters()
                .AnyAsync(c => c.VerificationCode == code, ct);
            if (!exists)
                return code;
        }
        // Extremely unlikely; fall back to a longer code from a GUID.
        return Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
    }

    private static string GenerateVerificationCode()
    {
        Span<byte> bytes = stackalloc byte[12];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var chars = new char[12];
        for (var i = 0; i < 12; i++)
            chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
        return new string(chars);
    }
}
