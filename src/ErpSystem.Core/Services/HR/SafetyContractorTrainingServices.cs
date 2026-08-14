using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SHE services — Contractor SHE Management (H) and Training (I).
// ============================================================================

#region Contractor Service

public class SheContractorService : ISheContractorService
{
    private readonly ISheContractorRepository _contractorRepository;
    private readonly ISheContractorInspectionRepository _inspectionRepository;
    private readonly ISheContractorNonComplianceRepository _nonComplianceRepository;
    private readonly ISheContractorDocumentRepository _documentRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheContractorService> _logger;

    public SheContractorService(
        ISheContractorRepository contractorRepository,
        ISheContractorInspectionRepository inspectionRepository,
        ISheContractorNonComplianceRepository nonComplianceRepository,
        ISheContractorDocumentRepository documentRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheContractorService> logger)
    {
        _contractorRepository = contractorRepository;
        _inspectionRepository = inspectionRepository;
        _nonComplianceRepository = nonComplianceRepository;
        _documentRepository = documentRepository;
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
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SheContractor> GetOwnedContractorAsync(Guid id)
    {
        var entity = await _contractorRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Contractor with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheContractorInduction> GetOwnedInductionAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SheContractorInduction>().GetByIdAsync(id, i => i.ConductedBy);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Contractor induction with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheContractorInspection> GetOwnedInspectionAsync(Guid id)
    {
        var entity = await _inspectionRepository.GetByIdAsync(id, i => i.Inspector, i => i.Location!);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Contractor inspection with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheContractorNonCompliance> GetOwnedNonComplianceAsync(Guid id)
    {
        var entity = await _nonComplianceRepository.GetByIdAsync(id, n => n.IssuedBy, n => n.ClosedBy!);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Contractor non-compliance with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheContractorDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _documentRepository.GetByIdAsync(id, d => d.UploadedBy, d => d.VerifiedBy!);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Contractor document with ID '{id}' not found.");
        return entity;
    }

    // Guards body-supplied FKs so a bad id surfaces as 404 instead of SQL 547/HTTP 500; fetching on
    // this context also lets change-tracker fixup resolve the navigation for the write response.
    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id);
        if (employee == null || employee.TenantId != GetTenantId())
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return employee;
    }

    private async Task<Location> GetOwnedLocationAsync(Guid id)
    {
        var location = await _unitOfWork.Repository<Location>().GetByIdAsync(id);
        if (location == null || location.TenantId != GetTenantId())
            throw new ArgumentException($"Location with ID '{id}' not found.");
        return location;
    }

    public async Task<SheContractorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _contractorRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Contractor with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheContractorDto?> GetByCodeAsync(string contractorCode, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var code = contractorCode.Trim();
        var id = await _contractorRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && c.ContractorCode == code)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (id == null) return null;

        var entity = await _contractorRepository.GetWithFullDetailsAsync(id.Value);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SheContractorSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _contractorRepository.GetAllSummaryAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheContractorSummaryDto>> GetByStatusAsync(SheContractorStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _contractorRepository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheContractorSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _contractorRepository.GetActiveAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheContractorSummaryDto>> GetExpiringPreQualificationAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _contractorRepository.GetExpiringPreQualificationAsync(daysAhead)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheContractorSummaryDto>> GetWithOpenNonCompliancesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _contractorRepository.GetWithOpenNonCompliancesAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<SheContractorDto> CreateAsync(CreateSheContractorDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var code = dto.ContractorCode.Trim();
        var exists = await _contractorRepository.GetQueryable()
            .AnyAsync(c => c.TenantId == tenantId && c.ContractorCode == code, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A contractor with code '{code}' already exists for this tenant.");

        dto.ContractorCode = code;
        var entity = dto.ToEntity(tenantId, userId);
        await _contractorRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheContractorDto> UpdateAsync(UpdateSheContractorDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedContractorAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _contractorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _contractorRepository.GetWithFullDetailsAsync(entity.Id);
        return (refreshed ?? entity).ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedContractorAsync(id);
        await _contractorRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> PreQualifyAsync(PreQualifySheContractorDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedContractorAsync(dto.ContractorId);

        if (dto.SheStatus == SheContractorStatus.PendingAssessment)
            throw new InvalidOperationException("Pre-qualification must record an assessment outcome; 'PendingAssessment' is not an outcome status.");
        await GetOwnedEmployeeAsync(dto.PreQualifiedById);

        entity.SheStatus = dto.SheStatus;
        entity.PreQualificationScore = dto.PreQualificationScore;
        entity.PreQualificationDate = dto.PreQualificationDate;
        entity.PreQualificationExpiryDate = dto.PreQualificationExpiryDate;
        entity.PreQualifiedById = dto.PreQualifiedById;
        entity.SheConditions = dto.SheConditions;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _contractorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Contractor pre-qualified: {ContractorCode}, Status: {Status}", entity.ContractorCode, entity.SheStatus);
        return true;
    }

    // ── Inductions ──
    public async Task<SheContractorInductionDto> AddInductionAsync(CreateSheContractorInductionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedContractorAsync(dto.ContractorId);
        await GetOwnedEmployeeAsync(dto.ConductedById);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheContractorInduction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheContractorInductionDto> UpdateInductionAsync(UpdateSheContractorInductionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInductionAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheContractorInduction>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteInductionAsync(Guid inductionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInductionAsync(inductionId);
        await _unitOfWork.Repository<SheContractorInduction>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── SHE inspections ──
    public async Task<IEnumerable<SheContractorInspectionDto>> GetInspectionsAsync(Guid contractorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedContractorAsync(contractorId);
        return (await _inspectionRepository.GetByContractorIdAsync(contractorId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SheContractorInspectionDto> AddInspectionAsync(CreateSheContractorInspectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedContractorAsync(dto.ContractorId);
        await GetOwnedEmployeeAsync(dto.InspectorId);
        if (dto.LocationId.HasValue) await GetOwnedLocationAsync(dto.LocationId.Value);

        var number = dto.InspectionNumber.Trim();
        var numberTaken = await _inspectionRepository.GetQueryable()
            .AnyAsync(i => i.TenantId == tenantId && i.InspectionNumber == number, cancellationToken);
        if (numberTaken)
            throw new InvalidOperationException($"A contractor SHE inspection with number '{number}' already exists for this tenant.");

        dto.InspectionNumber = number;
        var entity = dto.ToEntity(tenantId, userId);
        await _inspectionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheContractorInspectionDto> UpdateInspectionAsync(UpdateSheContractorInspectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionAsync(dto.Id);
        if (dto.LocationId.HasValue && dto.LocationId != entity.LocationId)
            await GetOwnedLocationAsync(dto.LocationId.Value);
        entity.UpdateEntity(dto, userId);
        await _inspectionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    // ── Non-compliances ──
    public async Task<IEnumerable<SheContractorNonComplianceDto>> GetNonCompliancesAsync(Guid contractorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedContractorAsync(contractorId);
        return (await _nonComplianceRepository.GetByContractorIdAsync(contractorId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheContractorNonComplianceDto>> GetOpenNonCompliancesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _nonComplianceRepository.GetOpenAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheContractorNonComplianceDto>> GetOverdueNonCompliancesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _nonComplianceRepository.GetOverdueAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SheContractorNonComplianceDto> AddNonComplianceAsync(CreateSheContractorNonComplianceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedContractorAsync(dto.ContractorId);
        await GetOwnedEmployeeAsync(dto.IssuedById);

        var number = dto.NoticeNumber.Trim();
        var numberTaken = await _nonComplianceRepository.GetQueryable()
            .AnyAsync(n => n.TenantId == tenantId && n.NoticeNumber == number, cancellationToken);
        if (numberTaken)
            throw new InvalidOperationException($"A non-compliance notice with number '{number}' already exists for this tenant.");

        // Repeat-violation flags are derived from the contractor's prior notices, not client-supplied.
        var priorCount = await _nonComplianceRepository.GetQueryable()
            .CountAsync(n => n.TenantId == tenantId && n.ContractorId == dto.ContractorId, cancellationToken);

        dto.NoticeNumber = number;
        var entity = dto.ToEntity(tenantId, userId);
        entity.RepeatCount = priorCount;
        entity.IsRepeatViolation = priorCount > 0;
        await _nonComplianceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheContractorNonComplianceDto> UpdateNonComplianceAsync(UpdateSheContractorNonComplianceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNonComplianceAsync(dto.Id);
        if (entity.Status == SheNonComplianceStatus.Closed)
            throw new InvalidOperationException("A closed non-compliance notice cannot be edited.");
        if (dto.Status == SheNonComplianceStatus.Closed)
            throw new InvalidOperationException("A non-compliance notice must be closed via the close endpoint, which records who closed it.");
        entity.UpdateEntity(dto, userId);
        await _nonComplianceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> CloseNonComplianceAsync(CloseSheContractorNonComplianceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNonComplianceAsync(dto.NonComplianceId);
        if (entity.Status == SheNonComplianceStatus.Closed)
            throw new InvalidOperationException("This non-compliance notice is already closed.");
        await GetOwnedEmployeeAsync(dto.ClosedById);

        entity.Status = SheNonComplianceStatus.Closed;
        entity.ClosedById = dto.ClosedById;
        entity.ClosedDate = dto.ClosedDate;
        entity.RectificationDate = dto.RectificationDate ?? entity.RectificationDate;
        entity.ClosureNotes = dto.ClosureNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _nonComplianceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Documents ──
    public async Task<IEnumerable<SheContractorDocumentDto>> GetDocumentsAsync(Guid contractorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedContractorAsync(contractorId);
        return (await _documentRepository.GetByContractorIdAsync(contractorId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheContractorDocumentDto>> GetExpiringDocumentsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetExpiringAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheContractorDocumentDto>> GetUnverifiedDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetUnverifiedAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SheContractorDocumentDto> AddDocumentAsync(CreateSheContractorDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedContractorAsync(dto.ContractorId);
        await GetOwnedEmployeeAsync(dto.UploadedById);
        var entity = dto.ToEntity(tenantId, userId);
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> VerifyDocumentAsync(VerifySheContractorDocumentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(dto.DocumentId);
        if (entity.IsVerified)
            throw new InvalidOperationException("This document has already been verified.");
        await GetOwnedEmployeeAsync(dto.VerifiedById);

        entity.IsVerified = true;
        entity.VerifiedById = dto.VerifiedById;
        entity.VerifiedDate = dto.VerifiedDate;
        entity.VerificationNotes = dto.VerificationNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _documentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(documentId);
        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Training Service

public class SheTrainingService : ISheTrainingService
{
    private readonly ISheTrainingPlanRepository _planRepository;
    private readonly ISheTrainingProgramRepository _programRepository;
    private readonly ISheTrainingAttendanceRepository _attendanceRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheTrainingService> _logger;

    public SheTrainingService(
        ISheTrainingPlanRepository planRepository,
        ISheTrainingProgramRepository programRepository,
        ISheTrainingAttendanceRepository attendanceRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheTrainingService> logger)
    {
        _planRepository = planRepository;
        _programRepository = programRepository;
        _attendanceRepository = attendanceRepository;
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
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SheTrainingPlan> GetOwnedPlanAsync(Guid id)
    {
        var entity = await _planRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training plan with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheTrainingProgram> GetOwnedProgramAsync(Guid id)
    {
        var entity = await _programRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training program with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheTrainingAttendance> GetOwnedAttendanceAsync(Guid id)
    {
        var entity = await _attendanceRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training attendance with ID '{id}' not found.");
        return entity;
    }

    // ── Plans ──
    public async Task<SheTrainingPlanDto> GetPlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _planRepository.GetWithProgramsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Training plan with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheTrainingPlanDto>> GetPlansByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetByYearAsync(year)).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheTrainingPlanDto>> GetPlansByStatusAsync(SheTrainingPlanStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<SheTrainingPlanDto> CreatePlanAsync(CreateSheTrainingPlanDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var planNumber = dto.PlanNumber.Trim();
        var exists = await _planRepository.GetQueryable()
            .AnyAsync(p => p.TenantId == tenantId && p.PlanNumber == planNumber, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A training plan with number '{planNumber}' already exists for this tenant.");

        var entity = dto.ToEntity(tenantId, userId);
        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheTrainingPlanDto> UpdatePlanAsync(UpdateSheTrainingPlanDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(id);
        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Programs ──
    public async Task<SheTrainingProgramDto> GetProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _programRepository.GetWithAttendancesAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Training program with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheTrainingProgramSummaryDto>> GetProgramsByPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedPlanAsync(planId);
        return (await _programRepository.GetByPlanIdAsync(planId)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheTrainingProgramSummaryDto>> GetProgramsByStatusAsync(SheTrainingStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _programRepository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheTrainingProgramSummaryDto>> GetProgramsByCategoryAsync(SheTrainingCategory category, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _programRepository.GetByCategoryAsync(category)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheTrainingProgramSummaryDto>> GetUpcomingProgramsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _programRepository.GetUpcomingAsync(daysAhead)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<SheTrainingProgramDto> CreateProgramAsync(CreateSheTrainingProgramDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        if (dto.PlanId.HasValue)
            await GetOwnedPlanAsync(dto.PlanId.Value);
        var code = dto.ProgramCode.Trim();
        var exists = await _programRepository.GetQueryable()
            .AnyAsync(p => p.TenantId == tenantId && p.ProgramCode == code, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A training program with code '{code}' already exists for this tenant.");

        var entity = dto.ToEntity(tenantId, userId);
        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheTrainingProgramDto> UpdateProgramAsync(UpdateSheTrainingProgramDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(id);
        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> EvaluateProgramAsync(EvaluateSheTrainingProgramDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(dto.ProgramId);

        entity.WasEvaluated = true;
        entity.EvaluatedById = dto.EvaluatedById;
        entity.EvaluationDate = dto.EvaluationDate;
        entity.EvaluationSummary = dto.EvaluationSummary;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Attendance ──
    public async Task<IEnumerable<SheTrainingAttendanceDto>> GetAttendancesByProgramAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedProgramAsync(programId);
        return (await _attendanceRepository.GetByProgramIdAsync(programId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheTrainingAttendanceDto>> GetAttendancesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _attendanceRepository.GetByEmployeeAsync(employeeId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheTrainingAttendanceDto>> GetExpiringCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _attendanceRepository.GetExpiringCertificatesAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SheTrainingAttendanceDto> AddAttendanceAsync(CreateSheTrainingAttendanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedProgramAsync(dto.ProgramId);
        var entity = dto.ToEntity(tenantId, userId);
        await _attendanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheTrainingAttendanceDto> UpdateAttendanceAsync(UpdateSheTrainingAttendanceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAttendanceAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _attendanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAttendanceAsync(Guid attendanceId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAttendanceAsync(attendanceId);
        await _attendanceRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
