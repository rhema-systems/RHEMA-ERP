using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF PROMOTION SERVICE
// ============================================================================

#region Staff Promotion Service

public class StaffPromotionService : IStaffPromotionService
{
    private readonly IStaffPromotionRepository _repo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffPromotionService> _logger;

    public StaffPromotionService(
        IStaffPromotionRepository repo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffPromotionService> logger)
    {
        _repo       = repo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger     = logger;
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

    private async Task<StaffPromotion> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff promotion with ID '{id}' was not found.");
        return entity;
    }

    public async Task<StaffPromotionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffPromotionDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffPromotionDto>> GetByTypeAsync(StaffPromotionType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByTypeAsync(type);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffPromotionDto>> GetActiveActingPromotionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetActiveActingPromotionsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffPromotionDto> CreateAsync(CreateStaffPromotionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff promotion detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffPromotionDto> UpdateAsync(UpdateStaffPromotionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTransferService> _logger;

    public StaffTransferService(
        IStaffTransferRepository repo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTransferService> logger)
    {
        _repo       = repo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger     = logger;
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

    private async Task<StaffTransfer> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff transfer with ID '{id}' was not found.");
        return entity;
    }

    public async Task<StaffTransferDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffTransferDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffTransferDto>> GetByTypeAsync(StaffTransferType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByTypeAsync(type);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetByReasonCategoryAsync(StaffTransferReasonCategory reasonCategory, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByReasonCategoryAsync(reasonCategory);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetInterCompanyTransfersAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetInterCompanyTransfersAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetRelocationTransfersAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetRelocationTransfersAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetInTransitionAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetInTransitionAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffTransferDto> CreateAsync(CreateStaffTransferDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff transfer detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffTransferDto> UpdateAsync(UpdateStaffTransferDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDemotionService> _logger;

    public StaffDemotionService(
        IStaffDemotionRepository repo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDemotionService> logger)
    {
        _repo       = repo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger     = logger;
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

    private async Task<StaffDemotion> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff demotion with ID '{id}' was not found.");
        return entity;
    }

    public async Task<StaffDemotionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffDemotionDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffDemotionDto>> GetDisciplinaryDemotionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetDisciplinaryDemotionsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDemotionDto>> GetPerformanceRelatedDemotionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetPerformanceRelatedDemotionsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDemotionDto>> GetWithPendingAppealsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetWithPendingAppealsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDemotionDto> CreateAsync(CreateStaffDemotionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff demotion detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffDemotionDto> UpdateAsync(UpdateStaffDemotionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RecordEmployeeResponseAsync(Guid demotionId, string response, Guid respondingEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(demotionId);

        // The demoted employee's own words. The actor was accepted and discarded before, so anyone
        // could file an appeal — or an acceptance — in someone else's name, on the one record where
        // that testimony decides whether the demotion is contested.
        var tenantId = GetTenantId();
        var subjectEmployeeId = await _repo.GetQueryable()
            .Where(d => d.Id == demotionId && d.TenantId == tenantId)
            .Select(d => (Guid?)d.Movement.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (subjectEmployeeId != respondingEmployeeId)
            throw new UnauthorizedAccessException("Only the demoted employee can respond to this demotion notice.");

        if (!string.IsNullOrWhiteSpace(entity.EmployeeResponse))
            throw new InvalidOperationException("A response to this demotion notice has already been recorded.");

        entity.EmployeeResponse     = response;
        entity.EmployeeResponseDate = DateTime.UtcNow;
        entity.EmployeeNotified     = true;
        entity.NotificationDate   ??= DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee response recorded for demotion {DemotionId}", demotionId);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffSecondmentService> _logger;

    public StaffSecondmentService(
        IStaffSecondmentRepository repo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffSecondmentService> logger)
    {
        _repo       = repo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger     = logger;
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

    private async Task<StaffSecondment> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff secondment with ID '{id}' was not found.");
        return entity;
    }

    public async Task<StaffSecondmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffSecondmentDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetByTypeAsync(StaffSecondmentType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByTypeAsync(type);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetExternalSecondmentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetExternalSecondmentsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetByHostOrganizationAsync(string hostOrganization, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByHostOrganizationAsync(hostOrganization);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetEndingSoonAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetEndingSoonAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffSecondmentDto> CreateAsync(CreateStaffSecondmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff secondment detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffSecondmentDto> UpdateAsync(UpdateStaffSecondmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<StaffSecondmentDto> ExtendAsync(ExtendStaffSecondmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.SecondmentId);

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
        var entity = await GetOwnedAsync(id);

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion
