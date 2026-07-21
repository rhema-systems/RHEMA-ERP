using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheContractorService> _logger;

    public SheContractorService(
        ISheContractorRepository contractorRepository,
        ISheContractorInspectionRepository inspectionRepository,
        ISheContractorNonComplianceRepository nonComplianceRepository,
        ISheContractorDocumentRepository documentRepository,
        IUnitOfWork unitOfWork,
        ILogger<SheContractorService> logger)
    {
        _contractorRepository = contractorRepository;
        _inspectionRepository = inspectionRepository;
        _nonComplianceRepository = nonComplianceRepository;
        _documentRepository = documentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SheContractorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _contractorRepository.GetWithFullDetailsAsync(id)
            ?? throw new ArgumentException($"Contractor with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheContractorDto?> GetByCodeAsync(string contractorCode, CancellationToken cancellationToken = default)
    {
        var entity = await _contractorRepository.GetByCodeAsync(contractorCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SheContractorSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _contractorRepository.GetAllSummaryAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<SheContractorSummaryDto>> GetByStatusAsync(SheContractorStatus status, CancellationToken cancellationToken = default)
        => (await _contractorRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<SheContractorSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        => (await _contractorRepository.GetActiveAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<SheContractorSummaryDto>> GetExpiringPreQualificationAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _contractorRepository.GetExpiringPreQualificationAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<SheContractorSummaryDto>> GetWithOpenNonCompliancesAsync(CancellationToken cancellationToken = default)
        => (await _contractorRepository.GetWithOpenNonCompliancesAsync()).ToSummaryDtoList();

    public async Task<SheContractorDto> CreateAsync(CreateSheContractorDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _contractorRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheContractorDto> UpdateAsync(UpdateSheContractorDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _contractorRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Contractor with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _contractorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _contractorRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Contractor with ID '{id}' not found.");
        await _contractorRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> PreQualifyAsync(PreQualifySheContractorDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _contractorRepository.GetByIdAsync(dto.ContractorId)
            ?? throw new ArgumentException($"Contractor with ID '{dto.ContractorId}' not found.");

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
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheContractorInduction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheContractorInductionDto> UpdateInductionAsync(UpdateSheContractorInductionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheContractorInduction>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Contractor induction with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteInductionAsync(Guid inductionId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheContractorInduction>();
        var entity = await repo.GetByIdAsync(inductionId)
            ?? throw new ArgumentException($"Contractor induction with ID '{inductionId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── SHE inspections ──
    public async Task<IEnumerable<SheContractorInspectionDto>> GetInspectionsAsync(Guid contractorId, CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetByContractorIdAsync(contractorId)).Select(e => e.ToDto());

    public async Task<SheContractorInspectionDto> AddInspectionAsync(CreateSheContractorInspectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _inspectionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheContractorInspectionDto> UpdateInspectionAsync(UpdateSheContractorInspectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _inspectionRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Contractor inspection with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _inspectionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    // ── Non-compliances ──
    public async Task<IEnumerable<SheContractorNonComplianceDto>> GetNonCompliancesAsync(Guid contractorId, CancellationToken cancellationToken = default)
        => (await _nonComplianceRepository.GetByContractorIdAsync(contractorId)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheContractorNonComplianceDto>> GetOpenNonCompliancesAsync(CancellationToken cancellationToken = default)
        => (await _nonComplianceRepository.GetOpenAsync()).Select(e => e.ToDto());

    public async Task<IEnumerable<SheContractorNonComplianceDto>> GetOverdueNonCompliancesAsync(CancellationToken cancellationToken = default)
        => (await _nonComplianceRepository.GetOverdueAsync()).Select(e => e.ToDto());

    public async Task<SheContractorNonComplianceDto> AddNonComplianceAsync(CreateSheContractorNonComplianceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _nonComplianceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheContractorNonComplianceDto> UpdateNonComplianceAsync(UpdateSheContractorNonComplianceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _nonComplianceRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Contractor non-compliance with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _nonComplianceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> CloseNonComplianceAsync(CloseSheContractorNonComplianceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _nonComplianceRepository.GetByIdAsync(dto.NonComplianceId)
            ?? throw new ArgumentException($"Contractor non-compliance with ID '{dto.NonComplianceId}' not found.");

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
        => (await _documentRepository.GetByContractorIdAsync(contractorId)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheContractorDocumentDto>> GetExpiringDocumentsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _documentRepository.GetExpiringAsync(daysAhead)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheContractorDocumentDto>> GetUnverifiedDocumentsAsync(CancellationToken cancellationToken = default)
        => (await _documentRepository.GetUnverifiedAsync()).Select(e => e.ToDto());

    public async Task<SheContractorDocumentDto> AddDocumentAsync(CreateSheContractorDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> VerifyDocumentAsync(VerifySheContractorDocumentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _documentRepository.GetByIdAsync(dto.DocumentId)
            ?? throw new ArgumentException($"Contractor document with ID '{dto.DocumentId}' not found.");

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
        var entity = await _documentRepository.GetByIdAsync(documentId)
            ?? throw new ArgumentException($"Contractor document with ID '{documentId}' not found.");
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheTrainingService> _logger;

    public SheTrainingService(
        ISheTrainingPlanRepository planRepository,
        ISheTrainingProgramRepository programRepository,
        ISheTrainingAttendanceRepository attendanceRepository,
        IUnitOfWork unitOfWork,
        ILogger<SheTrainingService> logger)
    {
        _planRepository = planRepository;
        _programRepository = programRepository;
        _attendanceRepository = attendanceRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Plans ──
    public async Task<SheTrainingPlanDto> GetPlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetWithProgramsAsync(id)
            ?? throw new ArgumentException($"Training plan with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheTrainingPlanDto>> GetPlansByYearAsync(int year, CancellationToken cancellationToken = default)
        => (await _planRepository.GetByYearAsync(year)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheTrainingPlanDto>> GetPlansByStatusAsync(SheTrainingPlanStatus status, CancellationToken cancellationToken = default)
        => (await _planRepository.GetByStatusAsync(status)).Select(e => e.ToDto());

    public async Task<SheTrainingPlanDto> CreatePlanAsync(CreateSheTrainingPlanDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheTrainingPlanDto> UpdatePlanAsync(UpdateSheTrainingPlanDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Training plan with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Training plan with ID '{id}' not found.");
        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Programs ──
    public async Task<SheTrainingProgramDto> GetProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetWithAttendancesAsync(id)
            ?? throw new ArgumentException($"Training program with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheTrainingProgramSummaryDto>> GetProgramsByPlanAsync(Guid planId, CancellationToken cancellationToken = default)
        => (await _programRepository.GetByPlanIdAsync(planId)).ToSummaryDtoList();

    public async Task<IEnumerable<SheTrainingProgramSummaryDto>> GetProgramsByStatusAsync(SheTrainingStatus status, CancellationToken cancellationToken = default)
        => (await _programRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<SheTrainingProgramSummaryDto>> GetProgramsByCategoryAsync(SheTrainingCategory category, CancellationToken cancellationToken = default)
        => (await _programRepository.GetByCategoryAsync(category)).ToSummaryDtoList();

    public async Task<IEnumerable<SheTrainingProgramSummaryDto>> GetUpcomingProgramsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _programRepository.GetUpcomingAsync(daysAhead)).ToSummaryDtoList();

    public async Task<SheTrainingProgramDto> CreateProgramAsync(CreateSheTrainingProgramDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheTrainingProgramDto> UpdateProgramAsync(UpdateSheTrainingProgramDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Training program with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Training program with ID '{id}' not found.");
        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> EvaluateProgramAsync(EvaluateSheTrainingProgramDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetByIdAsync(dto.ProgramId)
            ?? throw new ArgumentException($"Training program with ID '{dto.ProgramId}' not found.");

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
        => (await _attendanceRepository.GetByProgramIdAsync(programId)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheTrainingAttendanceDto>> GetAttendancesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _attendanceRepository.GetByEmployeeAsync(employeeId)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheTrainingAttendanceDto>> GetExpiringCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _attendanceRepository.GetExpiringCertificatesAsync(daysAhead)).Select(e => e.ToDto());

    public async Task<SheTrainingAttendanceDto> AddAttendanceAsync(CreateSheTrainingAttendanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _attendanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheTrainingAttendanceDto> UpdateAttendanceAsync(UpdateSheTrainingAttendanceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _attendanceRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Training attendance with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _attendanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAttendanceAsync(Guid attendanceId, CancellationToken cancellationToken = default)
    {
        var entity = await _attendanceRepository.GetByIdAsync(attendanceId)
            ?? throw new ArgumentException($"Training attendance with ID '{attendanceId}' not found.");
        await _attendanceRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
