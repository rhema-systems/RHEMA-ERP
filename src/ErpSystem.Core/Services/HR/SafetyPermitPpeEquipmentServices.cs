using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SHE services — Permit-to-Work (E), PPE Management (F) and Equipment (G).
// ============================================================================

#region Permit-to-Work Service

public class ShePermitToWorkService : IShePermitToWorkService
{
    private readonly IShePermitToWorkRepository _permitRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShePermitToWorkService> _logger;

    public ShePermitToWorkService(IShePermitToWorkRepository permitRepository, IUnitOfWork unitOfWork, ILogger<ShePermitToWorkService> logger)
    {
        _permitRepository = permitRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ShePermitToWorkDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _permitRepository.GetWithFullDetailsAsync(id)
            ?? throw new ArgumentException($"Permit-to-work with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<ShePermitToWorkDto?> GetByNumberAsync(string permitNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _permitRepository.GetByNumberAsync(permitNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _permitRepository.GetAllSummaryAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByStatusAsync(ShePermitStatus status, CancellationToken cancellationToken = default)
        => (await _permitRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByTypeAsync(ShePermitType type, CancellationToken cancellationToken = default)
        => (await _permitRepository.GetByTypeAsync(type)).ToSummaryDtoList();

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        => (await _permitRepository.GetActiveAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByContractorAsync(Guid contractorId, CancellationToken cancellationToken = default)
        => (await _permitRepository.GetByContractorAsync(contractorId)).ToSummaryDtoList();

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByRequestorAsync(Guid requestedById, CancellationToken cancellationToken = default)
        => (await _permitRepository.GetByRequestorAsync(requestedById)).ToSummaryDtoList();

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetExpiringAsync(int daysAhead = 1, CancellationToken cancellationToken = default)
        => (await _permitRepository.GetExpiringAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetSuspendedAsync(CancellationToken cancellationToken = default)
        => (await _permitRepository.GetSuspendedAsync()).ToSummaryDtoList();

    public async Task<ShePermitToWorkDto> CreateAsync(CreateShePermitToWorkDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        entity.PermitNumber = await _permitRepository.GetNextPermitNumberAsync();
        await _permitRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Permit-to-work created: {PermitNumber}", entity.PermitNumber);
        return entity.ToDto();
    }

    public async Task<ShePermitToWorkDto> UpdateAsync(UpdateShePermitToWorkDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _permitRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Permit-to-work with ID '{dto.Id}' not found.");
        if (entity.Status is ShePermitStatus.Completed or ShePermitStatus.Cancelled)
            throw new InvalidOperationException("A completed or cancelled permit cannot be edited.");

        entity.UpdateEntity(dto, userId);
        await _permitRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _permitRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Permit-to-work with ID '{id}' not found.");
        await _permitRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApproveAsync(ApproveShePermitToWorkDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _permitRepository.GetByIdAsync(dto.PermitId)
            ?? throw new ArgumentException($"Permit-to-work with ID '{dto.PermitId}' not found.");
        if (entity.Status is not (ShePermitStatus.Draft or ShePermitStatus.PendingApproval))
            throw new InvalidOperationException("Only draft or pending permits can be approved.");

        entity.ApprovedById = dto.ApprovedById;
        entity.ApprovedDate = dto.ApprovedDate;
        entity.IssuedById = dto.IssuedById;
        entity.IssuedDate = dto.IssuedDate;
        entity.Status = ShePermitStatus.Active;
        Touch(entity, userId);

        await _permitRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SuspendAsync(SuspendShePermitToWorkDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _permitRepository.GetByIdAsync(dto.PermitId)
            ?? throw new ArgumentException($"Permit-to-work with ID '{dto.PermitId}' not found.");
        if (entity.Status != ShePermitStatus.Active)
            throw new InvalidOperationException("Only active permits can be suspended.");

        entity.IsSuspended = true;
        entity.SuspendedById = dto.SuspendedById;
        entity.SuspendedDate = dto.SuspendedDate;
        entity.SuspensionReason = dto.SuspensionReason;
        entity.Status = ShePermitStatus.Suspended;
        Touch(entity, userId);

        await _permitRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResumeAsync(Guid permitId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _permitRepository.GetByIdAsync(permitId)
            ?? throw new ArgumentException($"Permit-to-work with ID '{permitId}' not found.");
        if (entity.Status != ShePermitStatus.Suspended)
            throw new InvalidOperationException("Only suspended permits can be resumed.");

        entity.IsSuspended = false;
        entity.Status = ShePermitStatus.Active;
        Touch(entity, userId);

        await _permitRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CloseAsync(CloseShePermitToWorkDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _permitRepository.GetByIdAsync(dto.PermitId)
            ?? throw new ArgumentException($"Permit-to-work with ID '{dto.PermitId}' not found.");

        entity.Status = ShePermitStatus.Completed;
        entity.ClosedById = dto.ClosedById;
        entity.ClosedDate = dto.ClosedDate;
        entity.WorkCompletedSatisfactorily = dto.WorkCompletedSatisfactorily;
        entity.AreaLeftSafe = dto.AreaLeftSafe;
        entity.ReinstatementNotes = dto.ReinstatementNotes;
        entity.ClosureNotes = dto.ClosureNotes;
        entity.ActualEndDate ??= dto.ClosedDate;
        Touch(entity, userId);

        await _permitRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Permit-to-work closed: {PermitNumber}", entity.PermitNumber);
        return true;
    }

    public async Task<ShePermitToWorkWorkerDto> AddWorkerAsync(CreateShePermitToWorkWorkerDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<ShePermitToWorkWorker>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<ShePermitToWorkWorkerDto> UpdateWorkerAsync(UpdateShePermitToWorkWorkerDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<ShePermitToWorkWorker>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Permit worker with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteWorkerAsync(Guid workerId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<ShePermitToWorkWorker>();
        var entity = await repo.GetByIdAsync(workerId)
            ?? throw new ArgumentException($"Permit worker with ID '{workerId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ShePermitToWorkExtensionDto> AddExtensionAsync(CreateShePermitToWorkExtensionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<ShePermitToWorkExtension>().AddAsync(entity);

        // Extend the parent permit's planned end window.
        var permit = await _permitRepository.GetByIdAsync(dto.PermitToWorkId);
        if (permit != null)
        {
            permit.PlannedEndDate = dto.NewEndDate;
            permit.PlannedEndTime = dto.NewEndTime;
            await _permitRepository.UpdateAsync(permit);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<ShePermitToWorkDocumentDto> AddDocumentAsync(CreateShePermitToWorkDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<ShePermitToWorkDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<ShePermitToWorkDocument>();
        var entity = await repo.GetByIdAsync(documentId)
            ?? throw new ArgumentException($"Permit document with ID '{documentId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void Touch(ShePermitToWork entity, Guid userId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }
}

#endregion

#region PPE Management Service

public class PpeManagementService : IPpeManagementService
{
    private readonly IPpeTypeRepository _typeRepository;
    private readonly IPpeInventoryRepository _inventoryRepository;
    private readonly IPpeIssuanceRepository _issuanceRepository;
    private readonly IJobRolePpeRequirementRepository _requirementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PpeManagementService> _logger;

    public PpeManagementService(
        IPpeTypeRepository typeRepository,
        IPpeInventoryRepository inventoryRepository,
        IPpeIssuanceRepository issuanceRepository,
        IJobRolePpeRequirementRepository requirementRepository,
        IUnitOfWork unitOfWork,
        ILogger<PpeManagementService> logger)
    {
        _typeRepository = typeRepository;
        _inventoryRepository = inventoryRepository;
        _issuanceRepository = issuanceRepository;
        _requirementRepository = requirementRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Types ──
    public async Task<IEnumerable<PpeTypeDto>> GetTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var entities = activeOnly ? await _typeRepository.GetActiveAsync() : await _typeRepository.GetAllAsync();
        return entities.Select(e => e.ToDto());
    }

    public async Task<PpeTypeDto> GetTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _typeRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"PPE type with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<PpeTypeDto> CreateTypeAsync(CreatePpeTypeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _typeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<PpeTypeDto> UpdateTypeAsync(UpdatePpeTypeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _typeRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"PPE type with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _typeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _typeRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"PPE type with ID '{id}' not found.");
        await _typeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Inventory ──
    public async Task<PpeInventoryDto> GetInventoryItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _inventoryRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"PPE inventory item with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<PpeInventoryDto>> GetInventoryByTypeAsync(Guid ppeTypeId, CancellationToken cancellationToken = default)
        => (await _inventoryRepository.GetByPpeTypeAsync(ppeTypeId)).Select(e => e.ToDto());

    public async Task<IEnumerable<PpeInventoryDto>> GetBelowReorderLevelAsync(CancellationToken cancellationToken = default)
        => (await _inventoryRepository.GetBelowReorderLevelAsync()).Select(e => e.ToDto());

    public async Task<PpeInventoryDto> CreateInventoryAsync(CreatePpeInventoryDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _inventoryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<PpeInventoryDto> UpdateInventoryAsync(UpdatePpeInventoryDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _inventoryRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"PPE inventory item with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _inventoryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RestockAsync(RestockPpeInventoryDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _inventoryRepository.GetByIdAsync(dto.PpeInventoryId)
            ?? throw new ArgumentException($"PPE inventory item with ID '{dto.PpeInventoryId}' not found.");

        entity.QuantityInStock += dto.Quantity;
        entity.LastRestockDate = dto.RestockDate;
        entity.LastRestockedById = dto.RestockedById;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _inventoryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteInventoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _inventoryRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"PPE inventory item with ID '{id}' not found.");
        await _inventoryRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Issuance ──
    public async Task<IEnumerable<PpeIssuanceDto>> GetIssuancesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _issuanceRepository.GetByEmployeeAsync(employeeId)).Select(e => e.ToDto());

    public async Task<IEnumerable<PpeIssuanceDto>> GetOutstandingIssuancesAsync(CancellationToken cancellationToken = default)
        => (await _issuanceRepository.GetOutstandingAsync()).Select(e => e.ToDto());

    public async Task<IEnumerable<PpeIssuanceDto>> GetOverdueReturnsAsync(CancellationToken cancellationToken = default)
        => (await _issuanceRepository.GetOverdueReturnsAsync()).Select(e => e.ToDto());

    public async Task<PpeIssuanceDto> IssueAsync(CreatePpeIssuanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _issuanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> ReturnAsync(ReturnPpeIssuanceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _issuanceRepository.GetByIdAsync(dto.IssuanceId)
            ?? throw new ArgumentException($"PPE issuance with ID '{dto.IssuanceId}' not found.");
        if (entity.IsReturned)
            throw new InvalidOperationException("This PPE issuance has already been returned.");

        entity.IsReturned = true;
        entity.ActualReturnDate = dto.ActualReturnDate;
        entity.ConditionWhenReturned = dto.ConditionWhenReturned;
        entity.ReturnedToId = dto.ReturnedToId;
        if (!string.IsNullOrWhiteSpace(dto.Notes)) entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _issuanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Job-role requirements ──
    public async Task<IEnumerable<JobRolePpeRequirementDto>> GetRequirementsByJobRoleAsync(string jobRoleCode, CancellationToken cancellationToken = default)
        => (await _requirementRepository.GetByJobRoleAsync(jobRoleCode)).Select(e => e.ToDto());

    public async Task<JobRolePpeRequirementDto> AddRequirementAsync(CreateJobRolePpeRequirementDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _requirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<JobRolePpeRequirementDto> UpdateRequirementAsync(UpdateJobRolePpeRequirementDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _requirementRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Job-role PPE requirement with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _requirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var entity = await _requirementRepository.GetByIdAsync(requirementId)
            ?? throw new ArgumentException($"Job-role PPE requirement with ID '{requirementId}' not found.");
        await _requirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Safety Equipment Service

public class SafetyEquipmentService : ISafetyEquipmentService
{
    private readonly ISafetyEquipmentRepository _equipmentRepository;
    private readonly ISafetyEquipmentInspectionRepository _inspectionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SafetyEquipmentService> _logger;

    public SafetyEquipmentService(
        ISafetyEquipmentRepository equipmentRepository,
        ISafetyEquipmentInspectionRepository inspectionRepository,
        IUnitOfWork unitOfWork,
        ILogger<SafetyEquipmentService> logger)
    {
        _equipmentRepository = equipmentRepository;
        _inspectionRepository = inspectionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SafetyEquipmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _equipmentRepository.GetWithFullDetailsAsync(id)
            ?? throw new ArgumentException($"Safety equipment with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SafetyEquipmentDto?> GetByNumberAsync(string equipmentNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _equipmentRepository.GetByNumberAsync(equipmentNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _equipmentRepository.GetAllSummaryAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetByStatusAsync(SheSafetyEquipmentStatus status, CancellationToken cancellationToken = default)
        => (await _equipmentRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetByTypeAsync(SheSafetyEquipmentType type, CancellationToken cancellationToken = default)
        => (await _equipmentRepository.GetByTypeAsync(type)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => (await _equipmentRepository.GetByLocationAsync(locationId)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetDueForInspectionAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _equipmentRepository.GetDueForInspectionAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetDueForMaintenanceAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _equipmentRepository.GetDueForMaintenanceAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetExpiringCertificationAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _equipmentRepository.GetExpiringCertificationAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetOutOfServiceAsync(CancellationToken cancellationToken = default)
        => (await _equipmentRepository.GetOutOfServiceAsync()).ToSummaryDtoList();

    public async Task<SafetyEquipmentDto> CreateAsync(CreateSafetyEquipmentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        entity.EquipmentNumber = await _equipmentRepository.GetNextEquipmentNumberAsync();
        await _equipmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Safety equipment created: {EquipmentNumber}", entity.EquipmentNumber);
        return entity.ToDto();
    }

    public async Task<SafetyEquipmentDto> UpdateAsync(UpdateSafetyEquipmentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _equipmentRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Safety equipment with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _equipmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _equipmentRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Safety equipment with ID '{id}' not found.");
        await _equipmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Inspections ──
    public async Task<SafetyEquipmentInspectionDto> AddInspectionAsync(CreateSafetyEquipmentInspectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _inspectionRepository.AddAsync(entity);

        // Roll the equipment's inspection dates forward.
        var equipment = await _equipmentRepository.GetByIdAsync(dto.EquipmentId);
        if (equipment != null)
        {
            equipment.LastInspectionDate = dto.InspectionDate;
            equipment.NextInspectionDueDate = dto.NextInspectionDate;
            await _equipmentRepository.UpdateAsync(equipment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyEquipmentInspectionDto> UpdateInspectionAsync(UpdateSafetyEquipmentInspectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _inspectionRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Equipment inspection with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _inspectionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SafetyEquipmentInspectionDto>> GetInspectionsForEquipmentAsync(Guid equipmentId, CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetByEquipmentIdAsync(equipmentId)).Select(e => e.ToDto());

    public async Task<SafetyEquipmentInspectionActionDto> AddInspectionActionAsync(CreateSafetyEquipmentInspectionActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyEquipmentInspectionAction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyEquipmentInspectionActionDto> UpdateInspectionActionAsync(UpdateSafetyEquipmentInspectionActionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyEquipmentInspectionAction>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Equipment inspection action with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteInspectionActionAsync(Guid actionId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyEquipmentInspectionAction>();
        var entity = await repo.GetByIdAsync(actionId)
            ?? throw new ArgumentException($"Equipment inspection action with ID '{actionId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Maintenance ──
    public async Task<SafetyEquipmentMaintenanceDto> AddMaintenanceAsync(CreateSafetyEquipmentMaintenanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyEquipmentMaintenance>().AddAsync(entity);

        var equipment = await _equipmentRepository.GetByIdAsync(dto.EquipmentId);
        if (equipment != null)
        {
            equipment.LastMaintenanceDate = dto.MaintenanceDate;
            await _equipmentRepository.UpdateAsync(equipment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyEquipmentMaintenanceDto> UpdateMaintenanceAsync(UpdateSafetyEquipmentMaintenanceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyEquipmentMaintenance>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Equipment maintenance with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteMaintenanceAsync(Guid maintenanceId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyEquipmentMaintenance>();
        var entity = await repo.GetByIdAsync(maintenanceId)
            ?? throw new ArgumentException($"Equipment maintenance with ID '{maintenanceId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
