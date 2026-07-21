using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SHE services — Emergency (M), Regulatory (N), Signage (O), KPI (P),
// Committee & Meetings (Q) and Return-to-Work (R).
// ============================================================================

#region Emergency Service

public class SheEmergencyService : ISheEmergencyService
{
    private readonly IEmergencyPlanRepository _planRepository;
    private readonly IEmergencyDrillRepository _drillRepository;
    private readonly IEmergencyResponseTeamRepository _teamRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheEmergencyService> _logger;

    public SheEmergencyService(
        IEmergencyPlanRepository planRepository,
        IEmergencyDrillRepository drillRepository,
        IEmergencyResponseTeamRepository teamRepository,
        IUnitOfWork unitOfWork,
        ILogger<SheEmergencyService> logger)
    {
        _planRepository = planRepository;
        _drillRepository = drillRepository;
        _teamRepository = teamRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<EmergencyPlanDto> GetPlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetWithFullDetailsAsync(id)
            ?? throw new ArgumentException($"Emergency plan with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<EmergencyPlanDto?> GetPlanByNumberAsync(string planNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByNumberAsync(planNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<EmergencyPlanSummaryDto>> GetAllPlansAsync(CancellationToken cancellationToken = default)
        => (await _planRepository.GetAllSummaryAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<EmergencyPlanSummaryDto>> GetPlansByTypeAsync(SheEmergencyType type, CancellationToken cancellationToken = default)
        => (await _planRepository.GetByTypeAsync(type)).ToSummaryDtoList();

    public async Task<IEnumerable<EmergencyPlanSummaryDto>> GetActivePlansAsync(CancellationToken cancellationToken = default)
        => (await _planRepository.GetActiveAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<EmergencyPlanSummaryDto>> GetPlansDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _planRepository.GetDueForReviewAsync(daysAhead)).ToSummaryDtoList();

    public async Task<EmergencyPlanDto> CreatePlanAsync(CreateEmergencyPlanDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmergencyPlanDto> UpdatePlanAsync(UpdateEmergencyPlanDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Emergency plan with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Emergency plan with ID '{id}' not found.");
        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Assembly points ──
    public async Task<SheAssemblyPointDto> AddAssemblyPointAsync(CreateSheAssemblyPointDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheAssemblyPoint>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheAssemblyPointDto> UpdateAssemblyPointAsync(UpdateSheAssemblyPointDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheAssemblyPoint>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Assembly point with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAssemblyPointAsync(Guid assemblyPointId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheAssemblyPoint>();
        var entity = await repo.GetByIdAsync(assemblyPointId)
            ?? throw new ArgumentException($"Assembly point with ID '{assemblyPointId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Emergency contacts ──
    public async Task<EmergencyContactDto> AddContactAsync(CreateEmergencyContactDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<EmergencyContact>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmergencyContactDto> UpdateContactAsync(UpdateEmergencyContactDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmergencyContact>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Emergency contact with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmergencyContact>();
        var entity = await repo.GetByIdAsync(contactId)
            ?? throw new ArgumentException($"Emergency contact with ID '{contactId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Drills ──
    public async Task<IEnumerable<EmergencyDrillDto>> GetDrillsForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
        => (await _drillRepository.GetByPlanIdAsync(planId)).Select(e => e.ToDto());

    public async Task<IEnumerable<EmergencyDrillDto>> GetUpcomingDrillsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _drillRepository.GetUpcomingAsync(daysAhead)).Select(e => e.ToDto());

    public async Task<EmergencyDrillDto> AddDrillAsync(CreateEmergencyDrillDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _drillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmergencyDrillDto> UpdateDrillAsync(UpdateEmergencyDrillDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _drillRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Emergency drill with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _drillRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteDrillAsync(Guid drillId, CancellationToken cancellationToken = default)
    {
        var entity = await _drillRepository.GetByIdAsync(drillId)
            ?? throw new ArgumentException($"Emergency drill with ID '{drillId}' not found.");
        await _drillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Response team ──
    public async Task<EmergencyResponseTeamDto> AddTeamMemberAsync(CreateEmergencyResponseTeamDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _teamRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmergencyResponseTeamDto> UpdateTeamMemberAsync(UpdateEmergencyResponseTeamDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _teamRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Emergency response team member with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _teamRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteTeamMemberAsync(Guid teamMemberId, CancellationToken cancellationToken = default)
    {
        var entity = await _teamRepository.GetByIdAsync(teamMemberId)
            ?? throw new ArgumentException($"Emergency response team member with ID '{teamMemberId}' not found.");
        await _teamRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<EmergencyResponseTeamDto>> GetExpiringTeamCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _teamRepository.GetExpiringCertificatesAsync(daysAhead)).Select(e => e.ToDto());

    public async Task<IEnumerable<EmergencyResponseTeamDto>> GetTeamMembershipsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _teamRepository.GetByEmployeeAsync(employeeId)).Select(e => e.ToDto());
}

#endregion

#region Regulatory Compliance Service

public class SheRegulatoryComplianceService : ISheRegulatoryComplianceService
{
    private readonly ISheRegulatoryObligationRepository _obligationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheRegulatoryComplianceService> _logger;

    public SheRegulatoryComplianceService(ISheRegulatoryObligationRepository obligationRepository, IUnitOfWork unitOfWork, ILogger<SheRegulatoryComplianceService> logger)
    {
        _obligationRepository = obligationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SheRegulatoryObligationDto> GetObligationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _obligationRepository.GetWithEvidenceAsync(id)
            ?? throw new ArgumentException($"Regulatory obligation with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheRegulatoryObligationDto?> GetObligationByCodeAsync(string obligationCode, CancellationToken cancellationToken = default)
    {
        var entity = await _obligationRepository.GetByCodeAsync(obligationCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetAllObligationsAsync(CancellationToken cancellationToken = default)
        => (await _obligationRepository.GetAllSummaryAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsByDomainAsync(SheRegulatoryDomain domain, CancellationToken cancellationToken = default)
        => (await _obligationRepository.GetByDomainAsync(domain)).ToSummaryDtoList();

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsByStatusAsync(SheComplianceStatus status, CancellationToken cancellationToken = default)
        => (await _obligationRepository.GetByComplianceStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
        => (await _obligationRepository.GetByOwnerAsync(ownerId)).ToSummaryDtoList();

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _obligationRepository.GetDueForReviewAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetNonCompliantObligationsAsync(CancellationToken cancellationToken = default)
        => (await _obligationRepository.GetNonCompliantAsync()).ToSummaryDtoList();

    public async Task<SheRegulatoryObligationDto> CreateObligationAsync(CreateSheRegulatoryObligationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _obligationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheRegulatoryObligationDto> UpdateObligationAsync(UpdateSheRegulatoryObligationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _obligationRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Regulatory obligation with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _obligationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteObligationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _obligationRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Regulatory obligation with ID '{id}' not found.");
        await _obligationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheRegulatoryComplianceEvidenceDto> AddEvidenceAsync(CreateSheRegulatoryComplianceEvidenceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheRegulatoryComplianceEvidence>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteEvidenceAsync(Guid evidenceId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheRegulatoryComplianceEvidence>();
        var entity = await repo.GetByIdAsync(evidenceId)
            ?? throw new ArgumentException($"Compliance evidence with ID '{evidenceId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Safety Signage Service

public class SafetySignageService : ISafetySignageService
{
    private readonly ISafetySignRepository _signRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SafetySignageService> _logger;

    public SafetySignageService(ISafetySignRepository signRepository, IUnitOfWork unitOfWork, ILogger<SafetySignageService> logger)
    {
        _signRepository = signRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SafetySignDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _signRepository.GetByIdAsync(id, s => s.Location)
            ?? throw new ArgumentException($"Safety sign with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SafetySignDto?> GetByCodeAsync(string signCode, CancellationToken cancellationToken = default)
    {
        var entity = await _signRepository.GetByCodeAsync(signCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SafetySignDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => (await _signRepository.GetByLocationAsync(locationId)).Select(e => e.ToDto());

    public async Task<IEnumerable<SafetySignDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _signRepository.GetAllSummaryAsync()).Select(e => e.ToDto());

    public async Task<IEnumerable<SafetySignDto>> GetByTypeAsync(SheSafetySignType type, CancellationToken cancellationToken = default)
        => (await _signRepository.GetByTypeAsync(type)).Select(e => e.ToDto());

    public async Task<IEnumerable<SafetySignDto>> GetByStatusAsync(SheSafetySignStatus status, CancellationToken cancellationToken = default)
        => (await _signRepository.GetByStatusAsync(status)).Select(e => e.ToDto());

    public async Task<IEnumerable<SafetySignDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        => (await _signRepository.GetActiveAsync()).Select(e => e.ToDto());

    public async Task<IEnumerable<SafetySignDto>> GetDueForInspectionAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _signRepository.GetDueForInspectionAsync(daysAhead)).Select(e => e.ToDto());

    public async Task<SafetySignDto> CreateAsync(CreateSafetySignDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _signRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetySignDto> UpdateAsync(UpdateSafetySignDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _signRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Safety sign with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _signRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _signRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Safety sign with ID '{id}' not found.");
        await _signRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region SHE Performance Service

public class ShePerformanceService : IShePerformanceService
{
    private readonly IShePerformanceSnapshotRepository _snapshotRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShePerformanceService> _logger;

    public ShePerformanceService(IShePerformanceSnapshotRepository snapshotRepository, IUnitOfWork unitOfWork, ILogger<ShePerformanceService> logger)
    {
        _snapshotRepository = snapshotRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ShePerformanceSnapshotDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _snapshotRepository.GetByIdAsync(id, s => s.Location, s => s.PreparedBy, s => s.ReviewedBy)
            ?? throw new ArgumentException($"Performance snapshot with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<ShePerformanceSnapshotDto?> GetByNumberAsync(string snapshotNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _snapshotRepository.GetByNumberAsync(snapshotNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<ShePerformanceSnapshotSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
        => (await _snapshotRepository.GetByYearAsync(year)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<ShePerformanceSnapshotSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => (await _snapshotRepository.GetByLocationAsync(locationId)).Select(e => e.ToSummaryDto());

    public async Task<ShePerformanceSnapshotDto?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _snapshotRepository.GetLatestAsync();
        return entity?.ToDto();
    }

    public async Task<ShePerformanceSnapshotDto> CreateAsync(CreateShePerformanceSnapshotDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _snapshotRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> ReviewAsync(ReviewShePerformanceSnapshotDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _snapshotRepository.GetByIdAsync(dto.SnapshotId)
            ?? throw new ArgumentException($"Performance snapshot with ID '{dto.SnapshotId}' not found.");

        entity.ReviewedById = dto.ReviewedById;
        entity.ReviewedDate = dto.ReviewedDate;
        entity.ManagementComments = dto.ManagementComments;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _snapshotRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _snapshotRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Performance snapshot with ID '{id}' not found.");
        await _snapshotRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Safety Committee Service

public class SafetyCommitteeService : ISafetyCommitteeService
{
    private readonly ISafetyCommitteeRepository _committeeRepository;
    private readonly ISafetyCommitteeMemberRepository _memberRepository;
    private readonly ISafetyMeetingRepository _meetingRepository;
    private readonly ISafetyMeetingActionItemRepository _actionItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SafetyCommitteeService> _logger;

    public SafetyCommitteeService(
        ISafetyCommitteeRepository committeeRepository,
        ISafetyCommitteeMemberRepository memberRepository,
        ISafetyMeetingRepository meetingRepository,
        ISafetyMeetingActionItemRepository actionItemRepository,
        IUnitOfWork unitOfWork,
        ILogger<SafetyCommitteeService> logger)
    {
        _committeeRepository = committeeRepository;
        _memberRepository = memberRepository;
        _meetingRepository = meetingRepository;
        _actionItemRepository = actionItemRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Committees ──
    public async Task<SafetyCommitteeDto> GetCommitteeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _committeeRepository.GetWithMembersAsync(id)
            ?? throw new ArgumentException($"Safety committee with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SafetyCommitteeDto>> GetActiveCommitteesAsync(CancellationToken cancellationToken = default)
        => (await _committeeRepository.GetActiveAsync()).Select(e => e.ToDto());

    public async Task<SafetyCommitteeDto> CreateCommitteeAsync(CreateSafetyCommitteeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _committeeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyCommitteeDto> UpdateCommitteeAsync(UpdateSafetyCommitteeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _committeeRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Safety committee with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _committeeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteCommitteeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _committeeRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Safety committee with ID '{id}' not found.");
        await _committeeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Members ──
    public async Task<IEnumerable<SafetyCommitteeMemberDto>> GetMembersAsync(Guid committeeId, CancellationToken cancellationToken = default)
        => (await _memberRepository.GetByCommitteeIdAsync(committeeId)).Select(e => e.ToDto());

    public async Task<SafetyCommitteeMemberDto> AddMemberAsync(CreateSafetyCommitteeMemberDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _memberRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyCommitteeMemberDto> UpdateMemberAsync(UpdateSafetyCommitteeMemberDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _memberRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Committee member with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _memberRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var entity = await _memberRepository.GetByIdAsync(memberId)
            ?? throw new ArgumentException($"Committee member with ID '{memberId}' not found.");
        await _memberRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Meetings ──
    public async Task<SafetyMeetingDto> GetMeetingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _meetingRepository.GetWithFullDetailsAsync(id)
            ?? throw new ArgumentException($"Safety meeting with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SafetyMeetingSummaryDto>> GetMeetingsByCommitteeAsync(Guid committeeId, CancellationToken cancellationToken = default)
        => (await _meetingRepository.GetByCommitteeIdAsync(committeeId)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyMeetingSummaryDto>> GetMeetingsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        => (await _meetingRepository.GetByDateRangeAsync(fromDate, toDate)).ToSummaryDtoList();

    public async Task<SafetyMeetingDto> CreateMeetingAsync(CreateSafetyMeetingDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _meetingRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyMeetingDto> UpdateMeetingAsync(UpdateSafetyMeetingDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _meetingRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Safety meeting with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _meetingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteMeetingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _meetingRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Safety meeting with ID '{id}' not found.");
        await _meetingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Attendees ──
    public async Task<SafetyMeetingAttendeeDto> AddAttendeeAsync(CreateSafetyMeetingAttendeeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyMeetingAttendee>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RemoveAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyMeetingAttendee>();
        var entity = await repo.GetByIdAsync(attendeeId)
            ?? throw new ArgumentException($"Meeting attendee with ID '{attendeeId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Action items ──
    public async Task<IEnumerable<SafetyMeetingActionItemDto>> GetOpenActionItemsAsync(CancellationToken cancellationToken = default)
        => (await _actionItemRepository.GetOpenAsync()).Select(e => e.ToDto());

    public async Task<IEnumerable<SafetyMeetingActionItemDto>> GetOverdueActionItemsAsync(CancellationToken cancellationToken = default)
        => (await _actionItemRepository.GetOverdueAsync()).Select(e => e.ToDto());

    public async Task<IEnumerable<SafetyMeetingActionItemDto>> GetActionItemsByAssigneeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _actionItemRepository.GetByAssigneeAsync(employeeId)).Select(e => e.ToDto());

    public async Task<SafetyMeetingActionItemDto> AddActionItemAsync(CreateSafetyMeetingActionItemDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _actionItemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyMeetingActionItemDto> UpdateActionItemAsync(UpdateSafetyMeetingActionItemDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _actionItemRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Meeting action item with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _actionItemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteActionItemAsync(Guid actionItemId, CancellationToken cancellationToken = default)
    {
        var entity = await _actionItemRepository.GetByIdAsync(actionItemId)
            ?? throw new ArgumentException($"Meeting action item with ID '{actionItemId}' not found.");
        await _actionItemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Documents ──
    public async Task<SafetyMeetingDocumentDto> AddMeetingDocumentAsync(CreateSafetyMeetingDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyMeetingDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteMeetingDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyMeetingDocument>();
        var entity = await repo.GetByIdAsync(documentId)
            ?? throw new ArgumentException($"Meeting document with ID '{documentId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Return-to-Work Service

public class SheReturnToWorkService : ISheReturnToWorkService
{
    private readonly ISheReturnToWorkPlanRepository _planRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheReturnToWorkService> _logger;

    public SheReturnToWorkService(ISheReturnToWorkPlanRepository planRepository, IUnitOfWork unitOfWork, ILogger<SheReturnToWorkService> logger)
    {
        _planRepository = planRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SheReturnToWorkPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetWithFullDetailsAsync(id)
            ?? throw new ArgumentException($"Return-to-work plan with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheReturnToWorkPlanDto?> GetByNumberAsync(string planNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByNumberAsync(planNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _planRepository.GetAllSummaryAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _planRepository.GetByEmployeeAsync(employeeId)).ToSummaryDtoList();

    public async Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetByStatusAsync(SheReturnToWorkStatus status, CancellationToken cancellationToken = default)
        => (await _planRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetByIncidentAsync(Guid safetyIncidentId, CancellationToken cancellationToken = default)
        => (await _planRepository.GetByIncidentAsync(safetyIncidentId)).ToSummaryDtoList();

    public async Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        => (await _planRepository.GetActiveAsync()).ToSummaryDtoList();

    public async Task<SheReturnToWorkPlanDto> CreateAsync(CreateSheReturnToWorkPlanDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheReturnToWorkPlanDto> UpdateAsync(UpdateSheReturnToWorkPlanDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Return-to-work plan with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Return-to-work plan with ID '{id}' not found.");
        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheReturnToWorkPhaseDto> AddPhaseAsync(CreateSheReturnToWorkPhaseDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheReturnToWorkPhase>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheReturnToWorkPhaseDto> UpdatePhaseAsync(UpdateSheReturnToWorkPhaseDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheReturnToWorkPhase>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Return-to-work phase with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeletePhaseAsync(Guid phaseId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheReturnToWorkPhase>();
        var entity = await repo.GetByIdAsync(phaseId)
            ?? throw new ArgumentException($"Return-to-work phase with ID '{phaseId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<SheReturnToWorkReviewDto>> GetReviewsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var reviews = await _unitOfWork.Repository<SheReturnToWorkReview>()
            .FindAsync(r => r.ReturnToWorkPlanId == planId, r => r.ReviewedBy);
        return reviews.OrderBy(r => r.ReviewNumber).Select(r => r.ToDto());
    }

    public async Task<SheReturnToWorkReviewDto> AddReviewAsync(CreateSheReturnToWorkReviewDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheReturnToWorkReview>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

#endregion
