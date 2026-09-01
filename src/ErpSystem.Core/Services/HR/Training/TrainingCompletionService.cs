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
    private readonly ICurrentUserProvider _currentUserProvider;
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
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        INumberSequenceService numberSequence,
        ILogger<TrainingCompletionService> logger)
    {
        _completionRepository = completionRepository;
        _certificateRepository = certificateRepository;
        _nominationRepo = nominationRepo;
        _programSkillRepo = programSkillRepo;
        _employeeSkillRepo = employeeSkillRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _numberSequence = numberSequence;
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
    private async Task<TrainingCompletion> GetOwnedCompletionAsync(Guid id)
    {
        var entity = await _completionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training completion with ID '{id}' not found.");
        return entity;
    }

    private async Task<TrainingCertificate> GetOwnedCertificateAsync(Guid id)
    {
        var entity = await _certificateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training certificate with ID '{id}' not found.");
        return entity;
    }

    // ── Completion queries ────────────────────────────────────────────────────

    public async Task<TrainingCompletionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _completionRepository.GetByIdAsync(id);

        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training completion record with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingCompletionDto?> GetByNominationIdAsync(Guid nominationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _completionRepository.GetByNominationIdAsync(nominationId);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<IEnumerable<TrainingCompletionDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _completionRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingCompletionDto>> GetByScheduleIdAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _completionRepository.GetByScheduleIdAsync(scheduleId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingCompletionDto>> GetPendingVerificationAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _completionRepository.GetPendingManagerVerificationAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    // ── Completion recording ──────────────────────────────────────────────────

    public async Task<TrainingCompletionDto> RecordCompletionAsync(RecordTrainingCompletionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Completions are unique per nomination within a tenant: an unscoped check would block this tenant
        // on the strength of another tenant's row, and would confirm that the row exists there.
        var existing = await _completionRepository.GetByNominationIdAsync(dto.NominationId);

        if (existing != null && existing.TenantId == current)
            throw new InvalidOperationException($"A completion record already exists for nomination ID '{dto.NominationId}'.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _completionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training completion recorded for nomination {NominationId}", dto.NominationId);

        // Freshly written: no Nomination/Employee loaded, so map from a re-read instead.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<BulkCompletionResultDto> BulkRecordCompletionAsync(BulkRecordCompletionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var result = new BulkCompletionResultDto { RequestedCount = dto.Items.Count };

        // Nominations for this schedule that already have a completion, so we don't double-record.
        var existing = (await _completionRepository.GetByScheduleIdAsync(dto.ScheduleId))
            .Where(c => c.TenantId == current)
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
            }.ToEntity(current, createdByUserId);

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
        var entity = await GetOwnedCompletionAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _completionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training completion {CompletionId} updated", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<TrainingCompletionDto> VerifyCompletionAsync(VerifyTrainingCompletionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCompletionAsync(dto.CompletionId);

        if (entity.IsVerifiedByManager)
            throw new InvalidOperationException("This completion record has already been verified.");

        // ⚠ Finish-plan lane 4 (2026-09-01): the certificate gates the completion. A programme flagged
        // ProvidesCertificate had the flag read by nothing — a passed completion could be verified,
        // write its skills to the profile and close, with no certificate ever issued. The flag is
        // per programme, so this is the per-programme gate TDC asked for: a PASSED completion of a
        // certificate-bearing programme is not verifiable until an active certificate exists for
        // the nomination. A failed completion needs no certificate and is not held.
        var program = entity.Nomination?.Schedule?.Program;
        if (entity.IsPassed && program is { ProvidesCertificate: true })
        {
            var tenantId = GetTenantId();
            var hasCertificate = await _certificateRepository.GetQueryable()
                .AnyAsync(c => c.TenantId == tenantId
                            && c.NominationId == entity.NominationId
                            && c.Status == CertificateStatus.Active, cancellationToken);
            if (!hasCertificate)
                throw new InvalidOperationException(
                    $"\"{program.ProgramName}\" issues a certificate. Issue the certificate for this nomination before verifying the completion.");
        }

        entity.IsVerifiedByManager = true;
        entity.VerifiedById = updatedByUserId;
        entity.VerificationDate = DateTime.UtcNow;
        entity.VerificationNotes = dto.VerificationNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _completionRepository.UpdateAsync(entity);

        // Profile write-back: a verified, passed completion adds the programme's target skills to the
        // employee's profile (same SaveChanges → atomic with the verification).
        if (entity.IsPassed)
            await WriteBackSkillsAsync(entity, updatedByUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training completion {CompletionId} verified by {VerifierId}", dto.CompletionId, updatedByUserId);

        return entity.ToDto();
    }

    /// <summary>Upserts the training programme's target skills onto the employee's profile.</summary>
    private async Task WriteBackSkillsAsync(TrainingCompletion completion, Guid userId, CancellationToken ct)
    {
        // The completion has already been tenant-checked, so its TenantId is the authenticated one. Scoping
        // the profile read matters beyond the leak: an unscoped match would upgrade another tenant's skill
        // row instead of creating this tenant's.
        var tenantId = completion.TenantId;

        var nomination = await _nominationRepo.GetQueryable()
            .Where(n => n.TenantId == tenantId)
            .Include(n => n.Schedule)
            .FirstOrDefaultAsync(n => n.Id == completion.NominationId, ct);
        if (nomination?.Schedule == null) return;

        var programSkills = await _programSkillRepo.GetQueryable()
            .Where(ps => ps.TenantId == tenantId && ps.ProgramId == nomination.Schedule.ProgramId)
            .ToListAsync(ct);
        if (programSkills.Count == 0) return;

        var existing = await _employeeSkillRepo.GetQueryable()
            .Where(es => es.TenantId == tenantId && es.EmployeeId == completion.EmployeeId)
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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, createdByUserId);
        entity.CertificateNumber = await GenerateCertificateNumberAsync(cancellationToken);
        entity.VerificationCode = await GenerateUniqueVerificationCodeAsync(cancellationToken);
        entity.Status = CertificateStatus.Active;

        await _certificateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training certificate issued: {CertificateNumber} for employee {EmployeeId}", entity.CertificateNumber, dto.EmployeeId);

        // A freshly written entity has no Employee/Program/Nomination loaded, so mapping it directly
        // hands back a certificate with a blank holder and programme — on the one response the caller
        // is most likely to render straight onto a screen.
        var saved = await _certificateRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> RevokeCertificateAsync(RevokeCertificateDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCertificateAsync(dto.CertificateId);

        if (entity.Status == CertificateStatus.Revoked)
            throw new InvalidOperationException("Certificate is already revoked.");

        entity.Status = CertificateStatus.Revoked;
        entity.RevokedDate = DateTime.UtcNow;
        entity.RevokedReason = dto.RevokedReason;
        entity.RevokedById = updatedByUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _certificateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training certificate revoked: {CertificateNumber} — Reason: {Reason}", entity.CertificateNumber, dto.RevokedReason);

        return true;
    }

    public async Task<IEnumerable<TrainingCertificateSummaryDto>> GetCertificatesForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _certificateRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingCertificateSummaryDto>> GetExpiringCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _certificateRepository.GetExpiringAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task<string> GenerateCertificateNumberAsync(CancellationToken ct)
        => _numberSequence.GenerateAsync("CERT", ct);

    // Crockford base32 alphabet — omits I, L, O, U to avoid ambiguity when read/typed by a verifier.
    private const string CodeAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Generates a random 12-char verification code and retries until it is globally unique.</summary>
    // Unlike the other uniqueness checks in this service, this one must stay cross-tenant: verification
    // codes are resolved by an unauthenticated public lookup that has no tenant to scope by.
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
