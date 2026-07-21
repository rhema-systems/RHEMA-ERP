using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF PROMOTION SERVICE
// ============================================================================

#region Staff Promotion Service

public class StaffPromotionService : IStaffPromotionService
{
    private readonly IStaffPromotionRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffPromotionService> _logger;

    public StaffPromotionService(
        IStaffPromotionRepository repo,
        IUnitOfWork unitOfWork,
        ILogger<StaffPromotionService> logger)
    {
        _repo       = repo;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    public async Task<StaffPromotionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Staff promotion with ID '{id}' was not found.");

        return entity.ToDto();
    }

    public async Task<StaffPromotionDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffPromotionDto>> GetByTypeAsync(StaffPromotionType type, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetByTypeAsync(type);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffPromotionDto>> GetActiveActingPromotionsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetActiveActingPromotionsAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffPromotionDto> CreateAsync(CreateStaffPromotionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff promotion detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffPromotionDto> UpdateAsync(UpdateStaffPromotionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Staff promotion with ID '{updateDto.Id}' was not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Staff promotion with ID '{id}' was not found.");

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF TRANSFER SERVICE
// ============================================================================

#region Staff Transfer Service

public class StaffTransferService : IStaffTransferService
{
    private readonly IStaffTransferRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTransferService> _logger;

    public StaffTransferService(
        IStaffTransferRepository repo,
        IUnitOfWork unitOfWork,
        ILogger<StaffTransferService> logger)
    {
        _repo       = repo;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    public async Task<StaffTransferDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Staff transfer with ID '{id}' was not found.");

        return entity.ToDto();
    }

    public async Task<StaffTransferDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetByTypeAsync(StaffTransferType type, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetByTypeAsync(type);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetByReasonCategoryAsync(StaffTransferReasonCategory reasonCategory, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetByReasonCategoryAsync(reasonCategory);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetInterCompanyTransfersAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetInterCompanyTransfersAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetRelocationTransfersAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetRelocationTransfersAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetInTransitionAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetInTransitionAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffTransferDto> CreateAsync(CreateStaffTransferDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff transfer detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffTransferDto> UpdateAsync(UpdateStaffTransferDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Staff transfer with ID '{updateDto.Id}' was not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Staff transfer with ID '{id}' was not found.");

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF DEMOTION SERVICE
// ============================================================================

#region Staff Demotion Service

public class StaffDemotionService : IStaffDemotionService
{
    private readonly IStaffDemotionRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDemotionService> _logger;

    public StaffDemotionService(
        IStaffDemotionRepository repo,
        IUnitOfWork unitOfWork,
        ILogger<StaffDemotionService> logger)
    {
        _repo       = repo;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    public async Task<StaffDemotionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Staff demotion with ID '{id}' was not found.");

        return entity.ToDto();
    }

    public async Task<StaffDemotionDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffDemotionDto>> GetDisciplinaryDemotionsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetDisciplinaryDemotionsAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDemotionDto>> GetPerformanceRelatedDemotionsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetPerformanceRelatedDemotionsAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDemotionDto>> GetWithPendingAppealsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetWithPendingAppealsAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDemotionDto> CreateAsync(CreateStaffDemotionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff demotion detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffDemotionDto> UpdateAsync(UpdateStaffDemotionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Staff demotion with ID '{updateDto.Id}' was not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RecordEmployeeResponseAsync(Guid demotionId, string response, DateTime responseDate, Guid respondedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(demotionId);

        if (entity == null)
            throw new ArgumentException($"Staff demotion with ID '{demotionId}' was not found.");

        entity.EmployeeResponse     = response;
        entity.EmployeeResponseDate = responseDate;
        entity.EmployeeNotified     = true;

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee response recorded for demotion {DemotionId}", demotionId);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Staff demotion with ID '{id}' was not found.");

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF SECONDMENT SERVICE
// ============================================================================

#region Staff Secondment Service

public class StaffSecondmentService : IStaffSecondmentService
{
    private readonly IStaffSecondmentRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffSecondmentService> _logger;

    public StaffSecondmentService(
        IStaffSecondmentRepository repo,
        IUnitOfWork unitOfWork,
        ILogger<StaffSecondmentService> logger)
    {
        _repo       = repo;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    public async Task<StaffSecondmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Staff secondment with ID '{id}' was not found.");

        return entity.ToDto();
    }

    public async Task<StaffSecondmentDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetByTypeAsync(StaffSecondmentType type, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetByTypeAsync(type);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetExternalSecondmentsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetExternalSecondmentsAsync();
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetByHostOrganizationAsync(string hostOrganization, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetByHostOrganizationAsync(hostOrganization);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetEndingSoonAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetEndingSoonAsync(daysAhead);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffSecondmentDto> CreateAsync(CreateStaffSecondmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff secondment detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffSecondmentDto> UpdateAsync(UpdateStaffSecondmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Staff secondment with ID '{updateDto.Id}' was not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<StaffSecondmentDto> ExtendAsync(ExtendStaffSecondmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(dto.SecondmentId);

        if (entity == null)
            throw new ArgumentException($"Staff secondment with ID '{dto.SecondmentId}' was not found.");

        if (!entity.ExtensionAllowed)
            throw new InvalidOperationException("Extension is not permitted for this secondment.");

        if (entity.MaxExtensionMonths.HasValue && dto.ExtensionMonths > entity.MaxExtensionMonths.Value)
            throw new InvalidOperationException($"Extension cannot exceed {entity.MaxExtensionMonths} months for this secondment.");

        entity.EndDate = dto.NewEndDate;

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Secondment {SecondmentId} extended to {NewEndDate}", dto.SecondmentId, dto.NewEndDate);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Staff secondment with ID '{id}' was not found.");

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion
