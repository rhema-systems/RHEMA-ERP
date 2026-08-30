using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF DISCIPLINE INVESTIGATION SERVICE
// ============================================================================

#region Staff Discipline Investigation Service

public class StaffDisciplineInvestigationService : IStaffDisciplineInvestigationService
{
    private readonly IStaffDisciplineInvestigationRepository _investigationRepository;
    private readonly IStaffDisciplinaryActionRepository _caseRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineInvestigationService> _logger;

    public StaffDisciplineInvestigationService(
        IStaffDisciplineInvestigationRepository investigationRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineInvestigationService> logger)
    {
        _investigationRepository = investigationRepository;
        _caseRepository = caseRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplinaryAction> GetOwnedCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _caseRepository.GetByIdAsync(caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");
        return entity;
    }

    private async Task<StaffDisciplineInvestigation> GetOwnedInvestigationByCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _investigationRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"No investigation found for case '{caseId}'.");
        return entity;
    }

    public async Task<StaffDisciplineInvestigationDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _investigationRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineInvestigationDto>> GetByInvestigatorAsync(Guid investigatorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _investigationRepository.GetByInvestigatorAsync(tenantId, investigatorId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineInvestigationDto>> GetOpenInvestigationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _investigationRepository.GetOpenInvestigationsAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    /// <remarks>
    /// "Overdue" is FR-HR-178's four weeks, taken from <see cref="DisciplineProcessDeadlines"/> — not
    /// the caller's choice. It used to be a <c>maxDays</c> argument defaulting to 30, so this queue,
    /// the case advisory and any future reminder could each answer the same question differently and
    /// the spec's own figure appeared nowhere. The parameter is kept so an ad-hoc wider sweep is
    /// still possible, but it now defaults to the rule.
    /// </remarks>
    public async Task<IEnumerable<StaffDisciplineInvestigationDto>> GetOverdueInvestigationsAsync(int? maxDays = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _investigationRepository.GetOverdueInvestigationsAsync(
            tenantId, maxDays ?? DisciplineProcessDeadlines.InvestigationDays);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineInvestigationDto> OpenAsync(OpenInvestigationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var disciplinaryCase = await GetOwnedCaseAsync(dto.CaseId);

        if (!disciplinaryCase.RequiresInvestigation)
            throw new InvalidOperationException("This case is not flagged as requiring an investigation.");

        var existing = await _investigationRepository.GetByCaseIdAsync(tenantId, dto.CaseId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("An investigation has already been opened for this case.");

        var entity = new StaffDisciplineInvestigation
        {
            TenantId               = tenantId,
            DisciplinaryActionId   = dto.CaseId,
            InvestigatorId         = dto.InvestigatorId,
            InvestigationStartDate = dto.InvestigationStartDate ?? DateTime.UtcNow,
            CreatedBy              = userId.ToString(),
        };

        await _investigationRepository.AddAsync(entity);

        disciplinaryCase.Status    = DisciplinaryStatus.UnderInvestigation;
        disciplinaryCase.UpdatedAt = DateTime.UtcNow;
        disciplinaryCase.UpdatedBy = userId.ToString();
        await _caseRepository.UpdateAsync(disciplinaryCase);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Investigation opened for case {CaseId}", dto.CaseId);
        // Re-read so the response carries the resolved names. Mapping the just-written
        // entity returns investigatorName / hearingOfficerName / representativeEmployeeName
        // as null, because a freshly added or updated entity has no navigation loaded — the
        // create response then disagrees with the detail read that follows it. Caught by
        // slice 11; same shape as the succession document uploader (D-14's slice).

        var saved = await _investigationRepository.GetByCaseIdAsync(GetTenantId(), dto.CaseId);

        return (saved ?? entity).ToDto();
    }

    public async Task<StaffDisciplineInvestigationDto> UpdateAsync(UpdateInvestigationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInvestigationByCaseAsync(dto.CaseId);

        entity.InvestigatorId         = dto.InvestigatorId        ?? entity.InvestigatorId;
        entity.InvestigationStartDate = dto.InvestigationStartDate ?? entity.InvestigationStartDate;
        entity.InvestigationEndDate   = dto.InvestigationEndDate  ?? entity.InvestigationEndDate;
        entity.InvestigationFindings  = dto.InvestigationFindings ?? entity.InvestigationFindings;
        entity.EvidenceCollected      = dto.EvidenceCollected     ?? entity.EvidenceCollected;
        entity.UpdatedAt              = DateTime.UtcNow;
        entity.UpdatedBy              = userId.ToString();

        await _investigationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read so the response carries the resolved names — see the note on the sibling writer.
        var saved = await _investigationRepository.GetByCaseIdAsync(GetTenantId(), dto.CaseId);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> CompleteAsync(Guid caseId, string findings, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInvestigationByCaseAsync(caseId);

        entity.InvestigationFindings = findings;
        entity.InvestigationEndDate  = DateTime.UtcNow;
        entity.UpdatedAt             = DateTime.UtcNow;
        entity.UpdatedBy             = userId.ToString();
        await _investigationRepository.UpdateAsync(entity);

        var disciplinaryCase = await GetOwnedCaseAsync(caseId);
        if (disciplinaryCase.Status == DisciplinaryStatus.UnderInvestigation)
        {
            disciplinaryCase.Status    = DisciplinaryStatus.InvestigationComplete;
            disciplinaryCase.UpdatedAt = DateTime.UtcNow;
            disciplinaryCase.UpdatedBy = userId.ToString();
            await _caseRepository.UpdateAsync(disciplinaryCase);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Investigation completed for case {CaseId}", caseId);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE HEARING SERVICE
// ============================================================================

#region Staff Discipline Hearing Service

public class StaffDisciplineHearingService : IStaffDisciplineHearingService
{
    private readonly IStaffDisciplineHearingRepository _hearingRepository;
    private readonly IStaffDisciplinaryActionRepository _caseRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineHearingService> _logger;

    public StaffDisciplineHearingService(
        IStaffDisciplineHearingRepository hearingRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineHearingService> logger)
    {
        _hearingRepository = hearingRepository;
        _caseRepository = caseRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplinaryAction> GetOwnedCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _caseRepository.GetByIdAsync(caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");
        return entity;
    }

    private async Task<StaffDisciplineHearing> GetOwnedHearingByCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _hearingRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"No hearing found for case '{caseId}'.");
        return entity;
    }

    public async Task<StaffDisciplineHearingDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _hearingRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineHearingDto>> GetByHearingOfficerAsync(Guid officerId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _hearingRepository.GetByHearingOfficerAsync(tenantId, officerId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineHearingDto>> GetUpcomingHearingsAsync(int daysAhead = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _hearingRepository.GetUpcomingHearingsAsync(tenantId, daysAhead);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineHearingDto>> GetAwaitingOutcomeAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _hearingRepository.GetAwaitingOutcomeAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineHearingDto> ScheduleAsync(ScheduleHearingDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var disciplinaryCase = await GetOwnedCaseAsync(dto.CaseId);

        if (!disciplinaryCase.HearingRequired)
            throw new InvalidOperationException("This case is not flagged as requiring a hearing.");

        var existing = await _hearingRepository.GetByCaseIdAsync(tenantId, dto.CaseId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("A hearing has already been scheduled for this case.");

        var entity = new StaffDisciplineHearing
        {
            TenantId             = tenantId,
            DisciplinaryActionId = dto.CaseId,
            HearingDate          = dto.HearingDate,
            HearingVenue         = dto.HearingVenue,
            HearingOfficerId     = dto.HearingOfficerId,
            CreatedBy            = userId.ToString(),
        };

        await _hearingRepository.AddAsync(entity);

        disciplinaryCase.Status    = DisciplinaryStatus.HearingScheduled;
        disciplinaryCase.UpdatedAt = DateTime.UtcNow;
        disciplinaryCase.UpdatedBy = userId.ToString();
        await _caseRepository.UpdateAsync(disciplinaryCase);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Hearing scheduled for case {CaseId} on {HearingDate}", dto.CaseId, dto.HearingDate);

        // Re-read so the response carries the resolved names — see the note on the sibling writer.
        var saved = await _hearingRepository.GetByCaseIdAsync(GetTenantId(), dto.CaseId);
        return (saved ?? entity).ToDto();
    }

    public async Task<StaffDisciplineHearingDto> RecordOutcomeAsync(RecordHearingOutcomeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHearingByCaseAsync(dto.CaseId);

        entity.EmployeeAttendedHearing   = dto.EmployeeAttendedHearing;
        entity.EmployeeStatement         = dto.EmployeeStatement;
        entity.EmployeeHadRepresentation = dto.EmployeeHadRepresentation;
        entity.RepresentativeType        = dto.RepresentativeType;
        entity.RepresentativeEmployeeId  = dto.RepresentativeEmployeeId;
        entity.RepresentativeName        = dto.RepresentativeName;
        entity.RepresentativePosition    = dto.RepresentativePosition;
        entity.RepresentativeContactInfo = dto.RepresentativeContactInfo;
        entity.HearingNotes              = dto.HearingNotes;
        entity.UpdatedAt                 = DateTime.UtcNow;
        entity.UpdatedBy                 = userId.ToString();
        await _hearingRepository.UpdateAsync(entity);

        var disciplinaryCase = await GetOwnedCaseAsync(dto.CaseId);
        if (disciplinaryCase.Status == DisciplinaryStatus.HearingScheduled)
        {
            disciplinaryCase.Status    = DisciplinaryStatus.HearingConducted;
            disciplinaryCase.UpdatedAt = DateTime.UtcNow;
            disciplinaryCase.UpdatedBy = userId.ToString();
            await _caseRepository.UpdateAsync(disciplinaryCase);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Hearing outcome recorded for case {CaseId}", dto.CaseId);
        // Re-read so the response carries the resolved names. Mapping the just-written
        // entity returns investigatorName / hearingOfficerName / representativeEmployeeName
        // as null, because a freshly added or updated entity has no navigation loaded — the
        // create response then disagrees with the detail read that follows it. Caught by
        // slice 11; same shape as the succession document uploader (D-14's slice).

        var saved = await _hearingRepository.GetByCaseIdAsync(GetTenantId(), dto.CaseId);

        return (saved ?? entity).ToDto();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WARNING SERVICE
// ============================================================================

#region Staff Discipline Warning Service

public class StaffDisciplineWarningService : IStaffDisciplineWarningService
{
    private readonly IStaffDisciplineWarningRepository _warningRepository;
    private readonly IStaffDisciplinaryActionRepository _caseRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineWarningService> _logger;

    public StaffDisciplineWarningService(
        IStaffDisciplineWarningRepository warningRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineWarningService> logger)
    {
        _warningRepository = warningRepository;
        _caseRepository = caseRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplinaryAction> GetOwnedCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _caseRepository.GetByIdAsync(caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");
        return entity;
    }

    private async Task<StaffDisciplineWarning> GetOwnedWarningByCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _warningRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"No warning penalty found for case '{caseId}'.");
        return entity;
    }

    public async Task<StaffDisciplineWarningDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _warningRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineWarningDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _warningRepository.GetByEmployeeAsync(tenantId, employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineWarningDto>> GetActiveWarningsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _warningRepository.GetActiveWarningsForEmployeeAsync(tenantId, employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineWarningDto>> GetByTypeAsync(DisciplinaryWarningType warningType, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _warningRepository.GetByTypeAsync(tenantId, warningType);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineWarningDto>> GetExpiringAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _warningRepository.GetExpiringAsync(tenantId, daysAhead);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineWarningDto> RecordAsync(RecordWarningPenaltyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var disciplinaryCase = await GetOwnedCaseAsync(dto.CaseId);

        var existing = await _warningRepository.GetByCaseIdAsync(tenantId, dto.CaseId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("A warning penalty already exists for this case. Use Update instead.");

        var entity = new StaffDisciplineWarning
        {
            TenantId               = tenantId,
            DisciplinaryActionId   = dto.CaseId,
            WarningType            = dto.WarningType,
            WarningExpiryDate      = dto.WarningExpiryDate,
            WarningLetterReference = dto.WarningLetterReference,
            CreatedBy              = userId.ToString(),
        };

        await _warningRepository.AddAsync(entity);

        if (disciplinaryCase.Status == DisciplinaryStatus.AwaitingDecision)
        {
            disciplinaryCase.Status    = DisciplinaryStatus.DecisionMade;
            disciplinaryCase.UpdatedAt = DateTime.UtcNow;
            disciplinaryCase.UpdatedBy = userId.ToString();
            await _caseRepository.UpdateAsync(disciplinaryCase);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Warning penalty recorded for case {CaseId}", dto.CaseId);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineWarningDto> UpdateAsync(UpdateWarningPenaltyDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWarningByCaseAsync(dto.CaseId);

        entity.WarningType            = dto.WarningType;
        entity.WarningExpiryDate      = dto.WarningExpiryDate;
        entity.WarningLetterReference = dto.WarningLetterReference;
        entity.UpdatedAt              = DateTime.UtcNow;
        entity.UpdatedBy              = userId.ToString();

        await _warningRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE SUSPENSION SERVICE
// ============================================================================

#region Staff Discipline Suspension Service

public class StaffDisciplineSuspensionService : IStaffDisciplineSuspensionService
{
    private readonly IStaffDisciplineSuspensionRepository _suspensionRepository;
    private readonly IStaffDisciplinaryActionRepository _caseRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineSuspensionService> _logger;

    public StaffDisciplineSuspensionService(
        IStaffDisciplineSuspensionRepository suspensionRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineSuspensionService> logger)
    {
        _suspensionRepository = suspensionRepository;
        _caseRepository = caseRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplinaryAction> GetOwnedCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _caseRepository.GetByIdAsync(caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");
        return entity;
    }

    private async Task<StaffDisciplineSuspension> GetOwnedSuspensionByCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _suspensionRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"No suspension penalty found for case '{caseId}'.");
        return entity;
    }

    public async Task<StaffDisciplineSuspensionDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _suspensionRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineSuspensionDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _suspensionRepository.GetByEmployeeAsync(tenantId, employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineSuspensionDto>> GetCurrentlyActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _suspensionRepository.GetCurrentlyActiveAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineSuspensionDto>> GetUpcomingAsync(int daysAhead = 7, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _suspensionRepository.GetUpcomingAsync(tenantId, daysAhead);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineSuspensionDto> RecordAsync(RecordSuspensionPenaltyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var disciplinaryCase = await GetOwnedCaseAsync(dto.CaseId);

        var existing = await _suspensionRepository.GetByCaseIdAsync(tenantId, dto.CaseId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("A suspension penalty already exists for this case. Use Update instead.");

        var entity = new StaffDisciplineSuspension
        {
            TenantId             = tenantId,
            DisciplinaryActionId = dto.CaseId,
            SuspensionStartDate  = dto.SuspensionStartDate,
            SuspensionEndDate    = dto.SuspensionEndDate,
            SuspensionWithPay    = dto.SuspensionWithPay,
            CreatedBy            = userId.ToString(),
        };

        await _suspensionRepository.AddAsync(entity);

        if (disciplinaryCase.Status == DisciplinaryStatus.AwaitingDecision)
        {
            disciplinaryCase.Status    = DisciplinaryStatus.DecisionMade;
            disciplinaryCase.UpdatedAt = DateTime.UtcNow;
            disciplinaryCase.UpdatedBy = userId.ToString();
            await _caseRepository.UpdateAsync(disciplinaryCase);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Suspension penalty recorded for case {CaseId}", dto.CaseId);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineSuspensionDto> UpdateAsync(UpdateSuspensionPenaltyDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSuspensionByCaseAsync(dto.CaseId);

        entity.SuspensionStartDate = dto.SuspensionStartDate;
        entity.SuspensionEndDate   = dto.SuspensionEndDate;
        entity.SuspensionWithPay   = dto.SuspensionWithPay;
        entity.UpdatedAt           = DateTime.UtcNow;
        entity.UpdatedBy           = userId.ToString();

        await _suspensionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE FINE SERVICE
// ============================================================================

#region Staff Discipline Fine Service

public class StaffDisciplineFineService : IStaffDisciplineFineService
{
    private readonly IStaffDisciplineFineRepository _fineRepository;
    private readonly IStaffDisciplinaryActionRepository _caseRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineFineService> _logger;

    public StaffDisciplineFineService(
        IStaffDisciplineFineRepository fineRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineFineService> logger)
    {
        _fineRepository = fineRepository;
        _caseRepository = caseRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplinaryAction> GetOwnedCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _caseRepository.GetByIdAsync(caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");
        return entity;
    }

    private async Task<StaffDisciplineFine> GetOwnedFineByCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _fineRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"No fine record found for case '{caseId}'.");
        return entity;
    }

    public async Task<StaffDisciplineFineDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _fineRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineFineDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _fineRepository.GetByEmployeeAsync(tenantId, employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineFineDto>> GetOutstandingAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _fineRepository.GetOutstandingAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineFineDto>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _fineRepository.GetOverdueAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<decimal> GetTotalOutstandingBalanceForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _fineRepository.GetByEmployeeAsync(tenantId, employeeId);
        return entities
            .Where(e => e.TenantId == tenantId
                     && e.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid)
            .Sum(f => (f.FineAmount ?? 0m) - (f.FinePaidAmount ?? 0m));
    }

    public async Task<StaffDisciplineFineDto> RecordAsync(RecordFinePenaltyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var disciplinaryCase = await GetOwnedCaseAsync(dto.CaseId);

        var existing = await _fineRepository.GetByCaseIdAsync(tenantId, dto.CaseId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("A fine penalty already exists for this case. Use RecordPayment to update the payment status.");

        var entity = new StaffDisciplineFine
        {
            TenantId             = tenantId,
            DisciplinaryActionId = dto.CaseId,
            FineAmount           = dto.FineAmount,
            FineDueDate          = dto.FineDueDate,
            FinePaymentStatus    = DisciplinaryFinePaymentStatus.Pending,
            CreatedBy            = userId.ToString(),
        };

        await _fineRepository.AddAsync(entity);

        if (disciplinaryCase.Status == DisciplinaryStatus.AwaitingDecision)
        {
            disciplinaryCase.Status    = DisciplinaryStatus.DecisionMade;
            disciplinaryCase.UpdatedAt = DateTime.UtcNow;
            disciplinaryCase.UpdatedBy = userId.ToString();
            await _caseRepository.UpdateAsync(disciplinaryCase);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fine penalty recorded for case {CaseId}, Amount: {Amount}", dto.CaseId, dto.FineAmount);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineFineDto> RecordPaymentAsync(RecordFinePaymentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFineByCaseAsync(dto.CaseId);

        if (entity.FinePaymentStatus == DisciplinaryFinePaymentStatus.FullyPaid)
            throw new InvalidOperationException("The fine for this case has already been fully paid.");

        entity.FinePaidAmount    = (entity.FinePaidAmount ?? 0m) + dto.AmountPaid;
        entity.FinePaymentDate   = dto.PaymentDate;
        entity.FinePaymentStatus = dto.PaymentStatus;
        entity.UpdatedAt         = DateTime.UtcNow;
        entity.UpdatedBy         = userId.ToString();

        await _fineRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fine payment recorded for case {CaseId}, Amount paid: {Amount}", dto.CaseId, dto.AmountPaid);

        return entity.ToDto();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE APPEAL SERVICE
// ============================================================================

#region Staff Discipline Appeal Service

public class StaffDisciplineAppealService : IStaffDisciplineAppealService
{
    private readonly IStaffDisciplineAppealRepository _appealRepository;
    private readonly IStaffDisciplinaryActionRepository _caseRepository;
    private readonly IHrWorkingDayCalculator _workingDays;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineAppealService> _logger;

    public StaffDisciplineAppealService(
        IStaffDisciplineAppealRepository appealRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        IHrWorkingDayCalculator workingDays,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineAppealService> logger)
    {
        _appealRepository = appealRepository;
        _caseRepository = caseRepository;
        _workingDays = workingDays;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplinaryAction> GetOwnedCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _caseRepository.GetByIdAsync(caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");
        return entity;
    }

    private async Task<StaffDisciplineAppeal> GetOwnedAppealByCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _appealRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"No appeal found for case '{caseId}'.");
        return entity;
    }

    public async Task<StaffDisciplineAppealDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _appealRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<StaffDisciplineAppealDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _appealRepository.GetWithFullDetailsAsync(tenantId, id);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineAppealDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _appealRepository.GetByEmployeeAsync(tenantId, employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineAppealDto>> GetByStatusAsync(DisciplineAppealStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _appealRepository.GetByStatusAsync(tenantId, status);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineAppealDto>> GetPendingHearingScheduleAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _appealRepository.GetPendingHearingScheduleAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineAppealDto>> GetAwaitingOutcomeAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _appealRepository.GetAwaitingOutcomeAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    /// <remarks>
    /// The appellant is the case's own employee and is never taken from the request. An appeal is
    /// the subject's act, so this refuses the call from anyone else — HR included, exactly as the
    /// staff-movement acceptance path does. HR's role here is to schedule and decide, not to appeal
    /// on someone's behalf.
    /// </remarks>
    public async Task<StaffDisciplineAppealDto> FileAsync(FileAppealDto dto, Guid tenantId, Guid appellantEmployeeId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var disciplinaryCase = await GetOwnedCaseAsync(dto.CaseId);

        if (disciplinaryCase.EmployeeId != appellantEmployeeId)
            throw new UnauthorizedAccessException("Only the employee a case was brought against can appeal it.");

        // FR-HR-180 — five WORKING days from the decision. The window is enforced, unlike the two
        // advisory clocks on the case: an appeal filed out of time is not a late act that can still
        // be dealt with, it is one the employer is entitled to refuse to hear, and letting it through
        // silently would misrepresent the position to both sides.
        //
        // It runs from the DECISION DATE, not from the case being closed: the decision is the thing
        // being appealed and the point from which the employee knows there is something to appeal.
        if (disciplinaryCase.DecisionDate is DateTime decidedAt)
        {
            var closesAt = await _workingDays.AddWorkingDaysAsync(
                tenantId, decidedAt, DisciplineProcessDeadlines.AppealFilingWorkingDays, cancellationToken);

            if (DateTime.UtcNow > closesAt)
                throw new InvalidOperationException(
                    $"The window to appeal this decision closed on {closesAt:dd MMM yyyy}. "
                    + $"FR-HR-180 allows {DisciplineProcessDeadlines.AppealFilingWorkingDays} working days from the decision.");
        }

        // The duplicate check runs BEFORE the status check, and the order is load-bearing. Filing an
        // appeal moves the case to UnderAppeal, so a second attempt fails the status test first and
        // the caller is told "an appeal can only be filed where a decision has been made" — on a case
        // that plainly has one. The status guard was firing on a condition the first filing created,
        // and the real reason was never reachable. Same shape as the decision-gate ordering in slice 4:
        // answer with the thing that is actually wrong.
        var existing = await _appealRepository.GetByCaseIdAsync(tenantId, dto.CaseId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("An appeal has already been filed for this case.");

        if (disciplinaryCase.Status != DisciplinaryStatus.DecisionMade
            && disciplinaryCase.Status != DisciplinaryStatus.AwaitingDecision)
            throw new InvalidOperationException("An appeal can only be filed for a case where a decision has been made or is awaiting confirmation.");

        var entity = new StaffDisciplineAppeal
        {
            TenantId             = tenantId,
            DisciplinaryActionId = dto.CaseId,
            EmployeeId           = appellantEmployeeId,
            FiledDate            = DateTime.UtcNow,
            Reason               = dto.Reason,
            AppealStatus         = DisciplineAppealStatus.Filed,
            CreatedBy            = appellantEmployeeId.ToString(),
        };

        await _appealRepository.AddAsync(entity);

        disciplinaryCase.Status    = DisciplinaryStatus.UnderAppeal;
        disciplinaryCase.UpdatedAt = DateTime.UtcNow;
        disciplinaryCase.UpdatedBy = appellantEmployeeId.ToString();
        await _caseRepository.UpdateAsync(disciplinaryCase);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal filed for case {CaseId}", dto.CaseId);

        return entity.ToDto();
    }

    public async Task<bool> ScheduleHearingAsync(ScheduleAppealHearingDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAppealByCaseAsync(dto.CaseId);

        if (entity.AppealStatus != DisciplineAppealStatus.Filed
            && entity.AppealStatus != DisciplineAppealStatus.UnderReview)
            throw new InvalidOperationException($"Cannot schedule a hearing for an appeal in '{entity.AppealStatus}' status.");

        entity.HearingDate     = dto.HearingDate;
        entity.HearingVenue    = dto.HearingVenue;
        entity.AppealOfficerId = dto.AppealOfficerId;
        entity.AppealStatus    = DisciplineAppealStatus.HearingScheduled;
        entity.UpdatedAt       = DateTime.UtcNow;
        entity.UpdatedBy       = userId.ToString();

        await _appealRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal hearing scheduled for case {CaseId}", dto.CaseId);

        return true;
    }

    public async Task<bool> RecordOutcomeAsync(RecordAppealOutcomeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAppealByCaseAsync(dto.CaseId);

        entity.AppealOutcome      = dto.AppealOutcome;
        entity.AppealOutcomeNotes = dto.AppealOutcomeNotes;
        entity.AppealOutcomeDate  = dto.AppealOutcomeDate;
        entity.AppealOutcomeById  = userId;
        entity.HearingNotes       = dto.HearingNotes ?? entity.HearingNotes;
        entity.AppealStatus       = DisciplineAppealStatus.DecisionMade;
        entity.UpdatedAt          = DateTime.UtcNow;
        entity.UpdatedBy          = userId.ToString();

        await _appealRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal outcome recorded for case {CaseId}, Outcome: {Outcome}", dto.CaseId, dto.AppealOutcome);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE CORRECTIVE ACTION SERVICE
// ============================================================================

#region Staff Discipline Corrective Action Service

public class StaffDisciplineCorrectiveActionService : IStaffDisciplineCorrectiveActionService
{
    private readonly IStaffDisciplineCorrectiveActionRepository _correctiveActionRepository;
    private readonly IStaffDisciplineCorrectiveActionItemRepository _itemRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineCorrectiveActionService> _logger;

    public StaffDisciplineCorrectiveActionService(
        IStaffDisciplineCorrectiveActionRepository correctiveActionRepository,
        IStaffDisciplineCorrectiveActionItemRepository itemRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineCorrectiveActionService> logger)
    {
        _correctiveActionRepository = correctiveActionRepository;
        _itemRepository = itemRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplineCorrectiveAction> GetOwnedPlanAsync(Guid id)
    {
        var tenantId = GetTenantId();
        var entity = await _correctiveActionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Corrective action plan with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffDisciplineCorrectiveActionItem> GetOwnedItemAsync(Guid itemId)
    {
        var tenantId = GetTenantId();
        var entity = await _itemRepository.GetByIdAsync(itemId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Corrective action item with ID '{itemId}' not found.");
        return entity;
    }

    public async Task<StaffDisciplineCorrectiveActionDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _correctiveActionRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<StaffDisciplineCorrectiveActionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _correctiveActionRepository.GetWithItemsAsync(tenantId, id);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _correctiveActionRepository.GetByEmployeeAsync(tenantId, employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetBySupervisorAsync(Guid supervisorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _correctiveActionRepository.GetBySupervisorAsync(tenantId, supervisorId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetByStatusAsync(DisciplineCorrectiveActionStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _correctiveActionRepository.GetByStatusAsync(tenantId, status);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _correctiveActionRepository.GetOverdueAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetDueForReviewAsync(int daysAhead = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _correctiveActionRepository.GetDueForReviewAsync(tenantId, daysAhead);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineCorrectiveActionDto> CreateAsync(CreateStaffDisciplineCorrectiveActionDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, userId);

        await _correctiveActionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Corrective action plan created for case {CaseId}", createDto.DisciplinaryActionId);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineCorrectiveActionDto> UpdateAsync(UpdateStaffDisciplineCorrectiveActionDto updateDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(updateDto.Id);

        if (entity.Status == DisciplineCorrectiveActionStatus.Completed
            || entity.Status == DisciplineCorrectiveActionStatus.Cancelled)
            throw new InvalidOperationException("A completed or cancelled corrective action plan cannot be edited.");

        entity.UpdateEntity(updateDto, userId);

        await _correctiveActionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(id);

        if (entity.Status == DisciplineCorrectiveActionStatus.Completed)
            throw new InvalidOperationException("The corrective action plan is already completed.");

        entity.Status        = DisciplineCorrectiveActionStatus.Completed;
        entity.CompletedDate = DateTime.UtcNow;
        entity.UpdatedAt     = DateTime.UtcNow;
        entity.UpdatedBy     = userId.ToString();

        await _correctiveActionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Corrective action plan completed: {Id}", id);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(id);

        if (entity.Status == DisciplineCorrectiveActionStatus.Completed)
            throw new InvalidOperationException("A completed corrective action plan cannot be deleted.");

        await _correctiveActionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<StaffDisciplineCorrectiveActionItemDto> AddItemAsync(CreateStaffDisciplineCorrectiveActionItemDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(createDto.CorrectiveActionId);

        var entity = createDto.ToEntity(tenantId, userId);

        await _itemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineCorrectiveActionItemDto> UpdateItemAsync(UpdateStaffDisciplineCorrectiveActionItemDto updateDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, userId);

        await _itemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteItemAsync(Guid itemId, string completionNotes, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(itemId);

        if (entity.Status == DisciplineCorrectiveActionStatus.Completed)
            throw new InvalidOperationException("The item is already completed.");

        entity.Status          = DisciplineCorrectiveActionStatus.Completed;
        entity.CompletedDate   = DateTime.UtcNow;
        entity.CompletionNotes = completionNotes;
        entity.UpdatedAt       = DateTime.UtcNow;
        entity.UpdatedBy       = userId.ToString();

        await _itemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(itemId);

        await _itemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE TERMINATION SERVICE
// ============================================================================

#region Staff Discipline Termination Service

public class StaffDisciplineTerminationService : IStaffDisciplineTerminationService
{
    private readonly IStaffDisciplineTerminationRepository _terminationRepository;
    private readonly IStaffDisciplineSeparationRepository _separationRepository;
    private readonly IStaffDisciplinaryActionRepository _caseRepository;
    private readonly IStaffDisciplineAppealRepository _appealRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    // Area 9b: the disciplinary route raises the same exit record every other route does (D1).
    private readonly ISeparationService _separationService;
    private readonly ILogger<StaffDisciplineTerminationService> _logger;

    public StaffDisciplineTerminationService(
        IStaffDisciplineTerminationRepository terminationRepository,
        IStaffDisciplineSeparationRepository separationRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        IStaffDisciplineAppealRepository appealRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ISeparationService separationService,
        ILogger<StaffDisciplineTerminationService> logger)
    {
        _terminationRepository = terminationRepository;
        _separationRepository = separationRepository;
        _caseRepository = caseRepository;
        _appealRepository = appealRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _separationService = separationService;
        _logger = logger;
    }

    /// <summary>
    /// Validates a body-supplied employee id and returns the row.
    /// </summary>
    /// <remarks>
    /// Two jobs in one call, as elsewhere in HR. It stops an unknown or another tenant's id reaching
    /// the database as an FK violation (SQL 547, surfacing as an unexplained 500 rather than "that
    /// employee was not found"), and because the row ends up tracked, EF fixes up the navigation on
    /// the entity being written — so the separation write response carries the exit interviewer's
    /// name instead of null, with no second read. The harness caught the null.
    /// </remarks>
    private async Task<Employee> GetOwnedEmployeeAsync(Guid employeeId, string role)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId);
        if (employee == null || employee.IsDeleted || employee.TenantId != GetTenantId())
            throw new ArgumentException($"The {role} employee with ID '{employeeId}' was not found.");
        return employee;
    }

    /// <summary>
    /// Refuses a termination until the decision behind it is final and any appeal against it has
    /// been answered.
    /// </summary>
    /// <remarks>
    /// <para>Two things were unguarded. A termination could be recorded against a case in ANY status
    /// — a draft, or one whose proposed sanction was still sitting in an approver's queue — so the
    /// record could say an employee had been dismissed on a decision nobody had confirmed. And it
    /// could be recorded while an appeal was live and undecided, which is the procedural failure this
    /// whole area exists to prevent: dismissing someone while they are still contesting the finding.</para>
    ///
    /// <para><b>FR-HR-092 is NOT implemented as a flag here.</b> "The MD shall sign all terminations
    /// except procedural ones, which HR approves automatically per policy" is a statement about WHO
    /// CONFIRMS the decision, and that is already expressed: the action type carries its
    /// <c>MinimumAuthority</c>, and the workflow definition routes on it. A dismissal action type set
    /// to Management routes to the MD; a procedural one set to Hr does not. Adding an "MD signed"
    /// boolean here would be a second, unenforced copy of a fact the approval record already holds —
    /// and it would have to be kept in step with it by hand.</para>
    /// </remarks>
    private async Task EnsureTerminationIsFoundedAsync(StaffDisciplinaryAction disciplinaryCase, Guid tenantId)
    {
        if (disciplinaryCase.Status is not (DisciplinaryStatus.DecisionMade
            or DisciplinaryStatus.UnderAppeal
            or DisciplinaryStatus.Closed))
        {
            throw new InvalidOperationException(
                $"A termination cannot be recorded for a case in '{disciplinaryCase.Status}' status. "
                + "The decision must be confirmed first.");
        }

        var appeal = await _appealRepository.GetByCaseIdAsync(tenantId, disciplinaryCase.Id);
        if (appeal != null && appeal.AppealOutcome == null)
            throw new InvalidOperationException(
                "This decision is under appeal and the appeal has not been decided. "
                + "Record the appeal outcome before terminating.");
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplinaryAction> GetOwnedCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _caseRepository.GetByIdAsync(caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");
        return entity;
    }

    private async Task<StaffDisciplineTermination> GetOwnedTerminationByCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _terminationRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"No termination record found for case '{caseId}'.");
        return entity;
    }

    private async Task<StaffDisciplineSeparation> GetOwnedSeparationByCaseAsync(Guid caseId)
    {
        var tenantId = GetTenantId();
        var entity = await _separationRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"No separation record found for case '{caseId}'.");
        return entity;
    }

    public async Task<StaffDisciplineTerminationDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _terminationRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineTerminationDto>> GetByTypeAsync(EmployeeTerminationType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _terminationRepository.GetByTypeAsync(tenantId, type);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineTerminationDto>> GetEligibleForRehireAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _terminationRepository.GetEligibleForRehireAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineTerminationDto>> GetPendingPaycheckProcessingAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _terminationRepository.GetPendingPaycheckProcessingAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineTerminationDto> RecordAsync(RecordTerminationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var disciplinaryCase = await GetOwnedCaseAsync(dto.CaseId);

        // Duplicate first, then the state rules — the slice-5 ordering lesson: a second attempt must
        // be told it is a duplicate, not handed a state message about a condition the first one made.
        var existing = await _terminationRepository.GetByCaseIdAsync(tenantId, dto.CaseId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("A termination record already exists for this case. Use Update instead.");

        await EnsureTerminationIsFoundedAsync(disciplinaryCase, tenantId);

        var entity = new StaffDisciplineTermination
        {
            TenantId              = tenantId,
            DisciplinaryActionId  = dto.CaseId,
            Type                  = dto.Type,
            IsEligibleForRehire   = dto.IsEligibleForRehire,
            EligibleForRehireDate = dto.EligibleForRehireDate,
            RehireRestrictions    = dto.RehireRestrictions,
            SeparationNotes       = dto.SeparationNotes,
            CreatedBy             = userId.ToString(),
        };

        await _terminationRepository.AddAsync(entity);

        if (disciplinaryCase.Status == DisciplinaryStatus.AwaitingDecision)
        {
            disciplinaryCase.Status    = DisciplinaryStatus.DecisionMade;
            disciplinaryCase.UpdatedAt = DateTime.UtcNow;
            disciplinaryCase.UpdatedBy = userId.ToString();
            await _caseRepository.UpdateAsync(disciplinaryCase);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Termination recorded for case {CaseId}, Type: {Type}", dto.CaseId, dto.Type);

        // ⚠ Area 9b, decision D1: the disciplinary route does NOT keep a parallel exit store — it
        // raises the same EmployeeSeparation every other route does. Before this, a termination was
        // recorded here and nothing carried it any further: measured 2026-08-20, 29 disciplinary
        // terminations whose employees were ALL still StaffStatus = Active. Area 9 still owns the
        // decision and the hearing; the exit itself now lives in one register.
        //
        // Deliberately after SaveChangesAsync and deliberately not fatal. The decision has been
        // recorded and must not be rolled back because the exit could not be opened — the service
        // returns null in that case and the orphan repair picks it up.
        await _separationService.CreateFromDisciplinaryOutcomeAsync(
            disciplinaryCase.Id,
            disciplinaryCase.EmployeeId,
            dto.Type,
            dto.SeparationNotes,
            actorEmployeeId: null,
            cancellationToken);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineTerminationDto> UpdateAsync(UpdateTerminationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTerminationByCaseAsync(dto.CaseId);

        entity.Type                   = dto.Type;
        entity.IsEligibleForRehire    = dto.IsEligibleForRehire;
        entity.EligibleForRehireDate  = dto.EligibleForRehireDate;
        entity.RehireRestrictions     = dto.RehireRestrictions;
        entity.FinalPaycheckProcessed = dto.FinalPaycheckProcessed;
        entity.FinalPaycheckDate      = dto.FinalPaycheckDate;
        entity.FinalPaycheckAmount    = dto.FinalPaycheckAmount;
        entity.SeparationNotes        = dto.SeparationNotes;
        entity.UpdatedAt              = DateTime.UtcNow;
        entity.UpdatedBy              = userId.ToString();

        await _terminationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    // Separation operations

    public async Task<StaffDisciplineSeparationDto?> GetSeparationByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _separationRepository.GetByCaseIdAsync(tenantId, caseId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineSeparationDto>> GetIncompleteSeparationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _separationRepository.GetIncompleteAsync(tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineSeparationDto> InitiateSeparationAsync(InitiateSeparationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var disciplinaryCase = await GetOwnedCaseAsync(dto.CaseId);

        // ⚠ SCOPE BOUNDARY, recorded here because it is easy to mistake this checklist for more than
        // it is. What this area owns is the DECISION to end the employment and a record of the exit
        // steps as they are ticked off. It does NOT compute what the leaver is owed, does not block
        // anything on a clearance form (FR-HR-091), and does not handle resignation, retirement or
        // contract expiry — none of which involve a disciplinary case at all. Those belong to a
        // separation module that does not exist yet; this store becomes its data when it is built,
        // so it is deliberately kept honest rather than extended into a half-built exit process.
        var termination = await _terminationRepository.GetByCaseIdAsync(tenantId, dto.CaseId);
        if (termination == null || termination.TenantId != tenantId)
            throw new InvalidOperationException("A termination record must be created before initiating the separation process.");

        var existing = await _separationRepository.GetByCaseIdAsync(tenantId, dto.CaseId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("A separation record has already been initiated for this case.");

        if (dto.ExitInterviewerId is Guid interviewerId)
            await GetOwnedEmployeeAsync(interviewerId, "exit interviewer");

        var entity = new StaffDisciplineSeparation
        {
            TenantId             = tenantId,
            DisciplinaryActionId = dto.CaseId,
            ExitInterviewerId    = dto.ExitInterviewerId,
            CreatedBy            = userId.ToString(),
        };

        await _separationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Separation initiated for case {CaseId}", dto.CaseId);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineSeparationDto> UpdateSeparationAsync(UpdateSeparationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSeparationByCaseAsync(dto.CaseId);

        // The same guard as on initiate. A guard on one endpoint is worthless if another endpoint
        // writes the same column — the area-8 slice-3 lesson, where a demotion's employee response
        // could be written straight round the subject-only guard via a plain HR edit.
        if (dto.ExitInterviewerId is Guid interviewerId)
            await GetOwnedEmployeeAsync(interviewerId, "exit interviewer");

        // AccessRevokedById is set here too and is an Employee FK, so it gets the same treatment.
        if (dto.AccessRevokedById is Guid revokedById)
            await GetOwnedEmployeeAsync(revokedById, "access-revoking");

        entity.ExitInterviewCompleted     = dto.ExitInterviewCompleted;
        entity.ExitInterviewDate          = dto.ExitInterviewDate;
        entity.ExitInterviewNotes         = dto.ExitInterviewNotes;
        entity.ExitInterviewerId          = dto.ExitInterviewerId;
        entity.EquipmentReturned          = dto.EquipmentReturned;
        entity.EquipmentReturnedDate      = dto.EquipmentReturnedDate;
        entity.MissingEquipment           = dto.MissingEquipment;
        entity.AccessRevoked              = dto.AccessRevoked;
        entity.AccessRevokedDate          = dto.AccessRevokedDate;
        entity.AccessRevokedById          = dto.AccessRevokedById;
        entity.FinalPayrollProcessed      = dto.FinalPayrollProcessed;
        entity.FinalPayrollDate           = dto.FinalPayrollDate;
        entity.BenefitsTerminated         = dto.BenefitsTerminated;
        entity.BenefitsTerminationDate    = dto.BenefitsTerminationDate;
        entity.ExitChecklistCompleted     = dto.ExitChecklistCompleted;
        entity.ExitChecklistCompletedDate = dto.ExitChecklistCompletedDate;
        entity.AdditionalNotes            = dto.AdditionalNotes;
        entity.UpdatedAt                  = DateTime.UtcNow;
        entity.UpdatedBy                  = userId.ToString();

        await _separationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }
}

#endregion
