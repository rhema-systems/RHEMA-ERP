using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineInvestigationService> _logger;

    public StaffDisciplineInvestigationService(
        IStaffDisciplineInvestigationRepository investigationRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineInvestigationService> logger)
    {
        _investigationRepository = investigationRepository;
        _caseRepository = caseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffDisciplineInvestigationDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await _investigationRepository.GetByCaseIdAsync(caseId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineInvestigationDto>> GetByInvestigatorAsync(Guid investigatorId, CancellationToken cancellationToken = default)
    {
        var entities = await _investigationRepository.GetByInvestigatorAsync(investigatorId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineInvestigationDto>> GetOpenInvestigationsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _investigationRepository.GetOpenInvestigationsAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineInvestigationDto>> GetOverdueInvestigationsAsync(int maxDays = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _investigationRepository.GetOverdueInvestigationsAsync(maxDays);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineInvestigationDto> OpenAsync(OpenInvestigationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var disciplinaryCase = await _caseRepository.GetByIdAsync(dto.CaseId);

        if (disciplinaryCase == null)
            throw new ArgumentException($"Disciplinary case with ID '{dto.CaseId}' not found.");

        if (!disciplinaryCase.RequiresInvestigation)
            throw new InvalidOperationException("This case is not flagged as requiring an investigation.");

        var existing = await _investigationRepository.GetByCaseIdAsync(dto.CaseId);
        if (existing != null)
            throw new InvalidOperationException("An investigation has already been opened for this case.");

        var entity = new StaffDisciplineInvestigation
        {
            TenantId              = disciplinaryCase.TenantId,
            DisciplinaryActionId  = dto.CaseId,
            InvestigatorId        = dto.InvestigatorId,
            InvestigationStartDate = dto.InvestigationStartDate ?? DateTime.UtcNow,
            CreatedBy             = userId.ToString(),
        };

        await _investigationRepository.AddAsync(entity);

        disciplinaryCase.Status    = DisciplinaryStatus.UnderInvestigation;
        disciplinaryCase.UpdatedAt = DateTime.UtcNow;
        disciplinaryCase.UpdatedBy = userId.ToString();
        await _caseRepository.UpdateAsync(disciplinaryCase);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Investigation opened for case {CaseId}", dto.CaseId);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineInvestigationDto> UpdateAsync(UpdateInvestigationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _investigationRepository.GetByCaseIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"No investigation found for case '{dto.CaseId}'.");

        entity.InvestigatorId        = dto.InvestigatorId        ?? entity.InvestigatorId;
        entity.InvestigationStartDate = dto.InvestigationStartDate ?? entity.InvestigationStartDate;
        entity.InvestigationEndDate  = dto.InvestigationEndDate  ?? entity.InvestigationEndDate;
        entity.InvestigationFindings = dto.InvestigationFindings ?? entity.InvestigationFindings;
        entity.EvidenceCollected     = dto.EvidenceCollected     ?? entity.EvidenceCollected;
        entity.UpdatedAt             = DateTime.UtcNow;
        entity.UpdatedBy             = userId.ToString();

        await _investigationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteAsync(Guid caseId, string findings, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _investigationRepository.GetByCaseIdAsync(caseId);

        if (entity == null)
            throw new ArgumentException($"No investigation found for case '{caseId}'.");

        entity.InvestigationFindings = findings;
        entity.InvestigationEndDate  = DateTime.UtcNow;
        entity.UpdatedAt             = DateTime.UtcNow;
        entity.UpdatedBy             = userId.ToString();
        await _investigationRepository.UpdateAsync(entity);

        var disciplinaryCase = await _caseRepository.GetByIdAsync(caseId);
        if (disciplinaryCase != null && disciplinaryCase.Status == DisciplinaryStatus.UnderInvestigation)
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineHearingService> _logger;

    public StaffDisciplineHearingService(
        IStaffDisciplineHearingRepository hearingRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineHearingService> logger)
    {
        _hearingRepository = hearingRepository;
        _caseRepository = caseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffDisciplineHearingDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await _hearingRepository.GetByCaseIdAsync(caseId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineHearingDto>> GetByHearingOfficerAsync(Guid officerId, CancellationToken cancellationToken = default)
    {
        var entities = await _hearingRepository.GetByHearingOfficerAsync(officerId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineHearingDto>> GetUpcomingHearingsAsync(int daysAhead = 14, CancellationToken cancellationToken = default)
    {
        var entities = await _hearingRepository.GetUpcomingHearingsAsync(daysAhead);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineHearingDto>> GetAwaitingOutcomeAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _hearingRepository.GetAwaitingOutcomeAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineHearingDto> ScheduleAsync(ScheduleHearingDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var disciplinaryCase = await _caseRepository.GetByIdAsync(dto.CaseId);

        if (disciplinaryCase == null)
            throw new ArgumentException($"Disciplinary case with ID '{dto.CaseId}' not found.");

        if (!disciplinaryCase.HearingRequired)
            throw new InvalidOperationException("This case is not flagged as requiring a hearing.");

        var existing = await _hearingRepository.GetByCaseIdAsync(dto.CaseId);
        if (existing != null)
            throw new InvalidOperationException("A hearing has already been scheduled for this case.");

        var entity = new StaffDisciplineHearing
        {
            TenantId             = disciplinaryCase.TenantId,
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

        return entity.ToDto();
    }

    public async Task<StaffDisciplineHearingDto> RecordOutcomeAsync(RecordHearingOutcomeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _hearingRepository.GetByCaseIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"No hearing found for case '{dto.CaseId}'.");

        entity.EmployeeAttendedHearing      = dto.EmployeeAttendedHearing;
        entity.EmployeeStatement            = dto.EmployeeStatement;
        entity.EmployeeHadRepresentation    = dto.EmployeeHadRepresentation;
        entity.RepresentativeType           = dto.RepresentativeType;
        entity.RepresentativeEmployeeId     = dto.RepresentativeEmployeeId;
        entity.RepresentativeName           = dto.RepresentativeName;
        entity.RepresentativePosition       = dto.RepresentativePosition;
        entity.RepresentativeContactInfo    = dto.RepresentativeContactInfo;
        entity.HearingNotes                 = dto.HearingNotes;
        entity.UpdatedAt                    = DateTime.UtcNow;
        entity.UpdatedBy                    = userId.ToString();
        await _hearingRepository.UpdateAsync(entity);

        var disciplinaryCase = await _caseRepository.GetByIdAsync(dto.CaseId);
        if (disciplinaryCase != null && disciplinaryCase.Status == DisciplinaryStatus.HearingScheduled)
        {
            disciplinaryCase.Status    = DisciplinaryStatus.HearingConducted;
            disciplinaryCase.UpdatedAt = DateTime.UtcNow;
            disciplinaryCase.UpdatedBy = userId.ToString();
            await _caseRepository.UpdateAsync(disciplinaryCase);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Hearing outcome recorded for case {CaseId}", dto.CaseId);

        return entity.ToDto();
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineWarningService> _logger;

    public StaffDisciplineWarningService(
        IStaffDisciplineWarningRepository warningRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineWarningService> logger)
    {
        _warningRepository = warningRepository;
        _caseRepository = caseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffDisciplineWarningDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await _warningRepository.GetByCaseIdAsync(caseId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineWarningDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _warningRepository.GetByEmployeeAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineWarningDto>> GetActiveWarningsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _warningRepository.GetActiveWarningsForEmployeeAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineWarningDto>> GetByTypeAsync(DisciplinaryWarningType warningType, CancellationToken cancellationToken = default)
    {
        var entities = await _warningRepository.GetByTypeAsync(warningType);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineWarningDto>> GetExpiringAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _warningRepository.GetExpiringAsync(daysAhead);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineWarningDto> RecordAsync(RecordWarningPenaltyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var disciplinaryCase = await _caseRepository.GetByIdAsync(dto.CaseId);

        if (disciplinaryCase == null)
            throw new ArgumentException($"Disciplinary case with ID '{dto.CaseId}' not found.");

        var existing = await _warningRepository.GetByCaseIdAsync(dto.CaseId);
        if (existing != null)
            throw new InvalidOperationException("A warning penalty already exists for this case. Use Update instead.");

        var entity = new StaffDisciplineWarning
        {
            TenantId             = disciplinaryCase.TenantId,
            DisciplinaryActionId = dto.CaseId,
            WarningType          = dto.WarningType,
            WarningExpiryDate    = dto.WarningExpiryDate,
            WarningLetterReference = dto.WarningLetterReference,
            CreatedBy            = userId.ToString(),
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
        var entity = await _warningRepository.GetByCaseIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"No warning penalty found for case '{dto.CaseId}'.");

        entity.WarningType             = dto.WarningType;
        entity.WarningExpiryDate       = dto.WarningExpiryDate;
        entity.WarningLetterReference  = dto.WarningLetterReference;
        entity.UpdatedAt               = DateTime.UtcNow;
        entity.UpdatedBy               = userId.ToString();

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineSuspensionService> _logger;

    public StaffDisciplineSuspensionService(
        IStaffDisciplineSuspensionRepository suspensionRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineSuspensionService> logger)
    {
        _suspensionRepository = suspensionRepository;
        _caseRepository = caseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffDisciplineSuspensionDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await _suspensionRepository.GetByCaseIdAsync(caseId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineSuspensionDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _suspensionRepository.GetByEmployeeAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineSuspensionDto>> GetCurrentlyActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _suspensionRepository.GetCurrentlyActiveAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineSuspensionDto>> GetUpcomingAsync(int daysAhead = 7, CancellationToken cancellationToken = default)
    {
        var entities = await _suspensionRepository.GetUpcomingAsync(daysAhead);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineSuspensionDto> RecordAsync(RecordSuspensionPenaltyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var disciplinaryCase = await _caseRepository.GetByIdAsync(dto.CaseId);

        if (disciplinaryCase == null)
            throw new ArgumentException($"Disciplinary case with ID '{dto.CaseId}' not found.");

        var existing = await _suspensionRepository.GetByCaseIdAsync(dto.CaseId);
        if (existing != null)
            throw new InvalidOperationException("A suspension penalty already exists for this case. Use Update instead.");

        var entity = new StaffDisciplineSuspension
        {
            TenantId             = disciplinaryCase.TenantId,
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
        var entity = await _suspensionRepository.GetByCaseIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"No suspension penalty found for case '{dto.CaseId}'.");

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineFineService> _logger;

    public StaffDisciplineFineService(
        IStaffDisciplineFineRepository fineRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineFineService> logger)
    {
        _fineRepository = fineRepository;
        _caseRepository = caseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffDisciplineFineDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await _fineRepository.GetByCaseIdAsync(caseId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineFineDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _fineRepository.GetByEmployeeAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineFineDto>> GetOutstandingAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _fineRepository.GetOutstandingAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineFineDto>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _fineRepository.GetOverdueAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<decimal> GetTotalOutstandingBalanceForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => await _fineRepository.GetTotalOutstandingBalanceForEmployeeAsync(employeeId);

    public async Task<StaffDisciplineFineDto> RecordAsync(RecordFinePenaltyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var disciplinaryCase = await _caseRepository.GetByIdAsync(dto.CaseId);

        if (disciplinaryCase == null)
            throw new ArgumentException($"Disciplinary case with ID '{dto.CaseId}' not found.");

        var existing = await _fineRepository.GetByCaseIdAsync(dto.CaseId);
        if (existing != null)
            throw new InvalidOperationException("A fine penalty already exists for this case. Use RecordPayment to update the payment status.");

        var entity = new StaffDisciplineFine
        {
            TenantId             = disciplinaryCase.TenantId,
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
        var entity = await _fineRepository.GetByCaseIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"No fine record found for case '{dto.CaseId}'.");

        if (entity.FinePaymentStatus == DisciplinaryFinePaymentStatus.FullyPaid)
            throw new InvalidOperationException("The fine for this case has already been fully paid.");

        entity.FinePaidAmount     = (entity.FinePaidAmount ?? 0m) + dto.AmountPaid;
        entity.FinePaymentDate    = dto.PaymentDate;
        entity.FinePaymentStatus  = dto.PaymentStatus;
        entity.UpdatedAt          = DateTime.UtcNow;
        entity.UpdatedBy          = userId.ToString();

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineAppealService> _logger;

    public StaffDisciplineAppealService(
        IStaffDisciplineAppealRepository appealRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineAppealService> logger)
    {
        _appealRepository = appealRepository;
        _caseRepository = caseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffDisciplineAppealDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await _appealRepository.GetByCaseIdAsync(caseId);
        return entity?.ToDto();
    }

    public async Task<StaffDisciplineAppealDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appealRepository.GetWithFullDetailsAsync(id);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineAppealDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _appealRepository.GetByEmployeeAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineAppealDto>> GetByStatusAsync(DisciplineAppealStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _appealRepository.GetByStatusAsync(status);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineAppealDto>> GetPendingHearingScheduleAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _appealRepository.GetPendingHearingScheduleAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineAppealDto>> GetAwaitingOutcomeAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _appealRepository.GetAwaitingOutcomeAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineAppealDto> FileAsync(FileAppealDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var disciplinaryCase = await _caseRepository.GetByIdAsync(dto.CaseId);

        if (disciplinaryCase == null)
            throw new ArgumentException($"Disciplinary case with ID '{dto.CaseId}' not found.");

        if (disciplinaryCase.Status != DisciplinaryStatus.DecisionMade
            && disciplinaryCase.Status != DisciplinaryStatus.AwaitingDecision)
            throw new InvalidOperationException("An appeal can only be filed for a case where a decision has been made or is awaiting confirmation.");

        var existing = await _appealRepository.GetByCaseIdAsync(dto.CaseId);
        if (existing != null)
            throw new InvalidOperationException("An appeal has already been filed for this case.");

        var entity = new StaffDisciplineAppeal
        {
            TenantId             = disciplinaryCase.TenantId,
            DisciplinaryActionId = dto.CaseId,
            EmployeeId           = dto.EmployeeId,
            FiledDate            = dto.FiledDate,
            Reason               = dto.Reason,
            AppealStatus         = DisciplineAppealStatus.Filed,
            CreatedBy            = userId.ToString(),
        };

        await _appealRepository.AddAsync(entity);

        disciplinaryCase.Status    = DisciplinaryStatus.UnderAppeal;
        disciplinaryCase.UpdatedAt = DateTime.UtcNow;
        disciplinaryCase.UpdatedBy = userId.ToString();
        await _caseRepository.UpdateAsync(disciplinaryCase);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal filed for case {CaseId}", dto.CaseId);

        return entity.ToDto();
    }

    public async Task<bool> ScheduleHearingAsync(ScheduleAppealHearingDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _appealRepository.GetByCaseIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"No appeal found for case '{dto.CaseId}'.");

        if (entity.AppealStatus != DisciplineAppealStatus.Filed
            && entity.AppealStatus != DisciplineAppealStatus.UnderReview)
            throw new InvalidOperationException($"Cannot schedule a hearing for an appeal in '{entity.AppealStatus}' status.");

        entity.HearingDate      = dto.HearingDate;
        entity.HearingVenue     = dto.HearingVenue;
        entity.AppealOfficerId  = dto.AppealOfficerId;
        entity.AppealStatus     = DisciplineAppealStatus.HearingScheduled;
        entity.UpdatedAt        = DateTime.UtcNow;
        entity.UpdatedBy        = userId.ToString();

        await _appealRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal hearing scheduled for case {CaseId}", dto.CaseId);

        return true;
    }

    public async Task<bool> RecordOutcomeAsync(RecordAppealOutcomeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _appealRepository.GetByCaseIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"No appeal found for case '{dto.CaseId}'.");

        entity.AppealOutcome       = dto.AppealOutcome;
        entity.AppealOutcomeNotes  = dto.AppealOutcomeNotes;
        entity.AppealOutcomeDate   = dto.AppealOutcomeDate;
        entity.AppealOutcomeById   = dto.AppealOutcomeById;
        entity.HearingNotes        = dto.HearingNotes ?? entity.HearingNotes;
        entity.AppealStatus        = DisciplineAppealStatus.DecisionMade;
        entity.UpdatedAt           = DateTime.UtcNow;
        entity.UpdatedBy           = userId.ToString();

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineCorrectiveActionService> _logger;

    public StaffDisciplineCorrectiveActionService(
        IStaffDisciplineCorrectiveActionRepository correctiveActionRepository,
        IStaffDisciplineCorrectiveActionItemRepository itemRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineCorrectiveActionService> logger)
    {
        _correctiveActionRepository = correctiveActionRepository;
        _itemRepository = itemRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffDisciplineCorrectiveActionDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await _correctiveActionRepository.GetByCaseIdAsync(caseId);
        return entity?.ToDto();
    }

    public async Task<StaffDisciplineCorrectiveActionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _correctiveActionRepository.GetWithItemsAsync(id);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _correctiveActionRepository.GetByEmployeeAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetBySupervisorAsync(Guid supervisorId, CancellationToken cancellationToken = default)
    {
        var entities = await _correctiveActionRepository.GetBySupervisorAsync(supervisorId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetByStatusAsync(DisciplineCorrectiveActionStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _correctiveActionRepository.GetByStatusAsync(status);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _correctiveActionRepository.GetOverdueAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineCorrectiveActionDto>> GetDueForReviewAsync(int daysAhead = 14, CancellationToken cancellationToken = default)
    {
        var entities = await _correctiveActionRepository.GetDueForReviewAsync(daysAhead);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineCorrectiveActionDto> CreateAsync(CreateStaffDisciplineCorrectiveActionDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, userId);

        await _correctiveActionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Corrective action plan created for case {CaseId}", createDto.DisciplinaryActionId);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineCorrectiveActionDto> UpdateAsync(UpdateStaffDisciplineCorrectiveActionDto updateDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _correctiveActionRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Corrective action plan with ID '{updateDto.Id}' not found.");

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
        var entity = await _correctiveActionRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Corrective action plan with ID '{id}' not found.");

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
        var entity = await _correctiveActionRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Corrective action plan with ID '{id}' not found.");

        if (entity.Status == DisciplineCorrectiveActionStatus.Completed)
            throw new InvalidOperationException("A completed corrective action plan cannot be deleted.");

        await _correctiveActionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<StaffDisciplineCorrectiveActionItemDto> AddItemAsync(CreateStaffDisciplineCorrectiveActionItemDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await _correctiveActionRepository.GetByIdAsync(createDto.CorrectiveActionId);

        if (plan == null)
            throw new ArgumentException($"Corrective action plan with ID '{createDto.CorrectiveActionId}' not found.");

        var entity = createDto.ToEntity(tenantId, userId);

        await _itemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineCorrectiveActionItemDto> UpdateItemAsync(UpdateStaffDisciplineCorrectiveActionItemDto updateDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _itemRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Corrective action item with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, userId);

        await _itemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteItemAsync(Guid itemId, string completionNotes, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _itemRepository.GetByIdAsync(itemId);

        if (entity == null)
            throw new ArgumentException($"Corrective action item with ID '{itemId}' not found.");

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
        var entity = await _itemRepository.GetByIdAsync(itemId);

        if (entity == null)
            throw new ArgumentException($"Corrective action item with ID '{itemId}' not found.");

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineTerminationService> _logger;

    public StaffDisciplineTerminationService(
        IStaffDisciplineTerminationRepository terminationRepository,
        IStaffDisciplineSeparationRepository separationRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineTerminationService> logger)
    {
        _terminationRepository = terminationRepository;
        _separationRepository = separationRepository;
        _caseRepository = caseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffDisciplineTerminationDto?> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await _terminationRepository.GetByCaseIdAsync(caseId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineTerminationDto>> GetByTypeAsync(EmployeeTerminationType type, CancellationToken cancellationToken = default)
    {
        var entities = await _terminationRepository.GetByTypeAsync(type);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineTerminationDto>> GetEligibleForRehireAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _terminationRepository.GetEligibleForRehireAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineTerminationDto>> GetPendingPaycheckProcessingAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _terminationRepository.GetPendingPaycheckProcessingAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineTerminationDto> RecordAsync(RecordTerminationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var disciplinaryCase = await _caseRepository.GetByIdAsync(dto.CaseId);

        if (disciplinaryCase == null)
            throw new ArgumentException($"Disciplinary case with ID '{dto.CaseId}' not found.");

        var existing = await _terminationRepository.GetByCaseIdAsync(dto.CaseId);
        if (existing != null)
            throw new InvalidOperationException("A termination record already exists for this case. Use Update instead.");

        var entity = new StaffDisciplineTermination
        {
            TenantId             = disciplinaryCase.TenantId,
            DisciplinaryActionId = dto.CaseId,
            Type                 = dto.Type,
            IsEligibleForRehire  = dto.IsEligibleForRehire,
            EligibleForRehireDate = dto.EligibleForRehireDate,
            RehireRestrictions   = dto.RehireRestrictions,
            SeparationNotes      = dto.SeparationNotes,
            CreatedBy            = userId.ToString(),
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

        return entity.ToDto();
    }

    public async Task<StaffDisciplineTerminationDto> UpdateAsync(UpdateTerminationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _terminationRepository.GetByCaseIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"No termination record found for case '{dto.CaseId}'.");

        entity.Type                  = dto.Type;
        entity.IsEligibleForRehire   = dto.IsEligibleForRehire;
        entity.EligibleForRehireDate = dto.EligibleForRehireDate;
        entity.RehireRestrictions    = dto.RehireRestrictions;
        entity.FinalPaycheckProcessed = dto.FinalPaycheckProcessed;
        entity.FinalPaycheckDate     = dto.FinalPaycheckDate;
        entity.FinalPaycheckAmount   = dto.FinalPaycheckAmount;
        entity.SeparationNotes       = dto.SeparationNotes;
        entity.UpdatedAt             = DateTime.UtcNow;
        entity.UpdatedBy             = userId.ToString();

        await _terminationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    // Separation operations

    public async Task<StaffDisciplineSeparationDto?> GetSeparationByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await _separationRepository.GetByCaseIdAsync(caseId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineSeparationDto>> GetIncompleteSeparationsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _separationRepository.GetIncompleteAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDisciplineSeparationDto> InitiateSeparationAsync(InitiateSeparationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var disciplinaryCase = await _caseRepository.GetByIdAsync(dto.CaseId);

        if (disciplinaryCase == null)
            throw new ArgumentException($"Disciplinary case with ID '{dto.CaseId}' not found.");

        var termination = await _terminationRepository.GetByCaseIdAsync(dto.CaseId);
        if (termination == null)
            throw new InvalidOperationException("A termination record must be created before initiating the separation process.");

        var existing = await _separationRepository.GetByCaseIdAsync(dto.CaseId);
        if (existing != null)
            throw new InvalidOperationException("A separation record has already been initiated for this case.");

        var entity = new StaffDisciplineSeparation
        {
            TenantId             = disciplinaryCase.TenantId,
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
        var entity = await _separationRepository.GetByCaseIdAsync(dto.CaseId);

        if (entity == null)
            throw new ArgumentException($"No separation record found for case '{dto.CaseId}'.");

        entity.ExitInterviewCompleted    = dto.ExitInterviewCompleted;
        entity.ExitInterviewDate         = dto.ExitInterviewDate;
        entity.ExitInterviewNotes        = dto.ExitInterviewNotes;
        entity.ExitInterviewerId         = dto.ExitInterviewerId;
        entity.EquipmentReturned         = dto.EquipmentReturned;
        entity.EquipmentReturnedDate     = dto.EquipmentReturnedDate;
        entity.MissingEquipment          = dto.MissingEquipment;
        entity.AccessRevoked             = dto.AccessRevoked;
        entity.AccessRevokedDate         = dto.AccessRevokedDate;
        entity.AccessRevokedById         = dto.AccessRevokedById;
        entity.FinalPayrollProcessed     = dto.FinalPayrollProcessed;
        entity.FinalPayrollDate          = dto.FinalPayrollDate;
        entity.BenefitsTerminated        = dto.BenefitsTerminated;
        entity.BenefitsTerminationDate   = dto.BenefitsTerminationDate;
        entity.ExitChecklistCompleted    = dto.ExitChecklistCompleted;
        entity.ExitChecklistCompletedDate = dto.ExitChecklistCompletedDate;
        entity.AdditionalNotes           = dto.AdditionalNotes;
        entity.UpdatedAt                 = DateTime.UtcNow;
        entity.UpdatedBy                 = userId.ToString();

        await _separationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }
}

#endregion
