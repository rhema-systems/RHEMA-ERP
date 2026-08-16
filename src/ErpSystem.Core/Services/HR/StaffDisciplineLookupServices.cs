using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF OFFENSE SERVICE
// ============================================================================

#region Staff Offense Service

public class StaffOffenseService : IStaffOffenseService
{
    private readonly IStaffOffenseRepository _offenseRepository;
    private readonly IStaffOffenseProcedureRepository _procedureRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffOffenseService> _logger;

    public StaffOffenseService(
        IStaffOffenseRepository offenseRepository,
        IStaffOffenseProcedureRepository procedureRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffOffenseService> logger)
    {
        _offenseRepository = offenseRepository;
        _procedureRepository = procedureRepository;
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

    private async Task<StaffOffense> GetOwnedOffenseAsync(Guid id)
    {
        var entity = await _offenseRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff offense with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffOffenseProcedure> GetOwnedProcedureAsync(Guid id)
    {
        var entity = await _procedureRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Offense procedure with ID '{id}' not found.");
        return entity;
    }

    public async Task<StaffOffenseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _offenseRepository.GetByIdAsync(id);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<StaffOffenseDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _offenseRepository.GetByCodeAsync(code);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<StaffOffenseDto?> GetWithProceduresAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _offenseRepository.GetWithProceduresAsync(id);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffOffenseSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _offenseRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffOffenseSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _offenseRepository.GetActiveOffensesAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<StaffOffenseDto> CreateAsync(CreateStaffOffenseDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        if (await _offenseRepository.CodeExistsAsync(createDto.OffenseCode, tenantId))
            throw new InvalidOperationException($"A staff offense with code '{createDto.OffenseCode}' already exists.");

        var entity = createDto.ToEntity(tenantId, userId);

        await _offenseRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff offense created: {Code} — {Name}", entity.OffenseCode, entity.OffenseName);

        return entity.ToDto();
    }

    public async Task<StaffOffenseDto> UpdateAsync(UpdateStaffOffenseDto updateDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOffenseAsync(updateDto.Id);

        if (!string.IsNullOrWhiteSpace(updateDto.OffenseCode)
            && !string.Equals(updateDto.OffenseCode, entity.OffenseCode, StringComparison.OrdinalIgnoreCase)
            && await _offenseRepository.CodeExistsAsync(updateDto.OffenseCode, entity.TenantId))
            throw new InvalidOperationException($"A staff offense with code '{updateDto.OffenseCode}' already exists.");

        entity.UpdateEntity(updateDto, userId);

        await _offenseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOffenseAsync(id);

        await _offenseRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff offense deleted: {Id}", id);

        return true;
    }

    // Procedure operations

    public async Task<StaffOffenseProcedureDto?> GetProcedureByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _procedureRepository.GetByIdAsync(id);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffOffenseProcedureDto>> GetProceduresByOffenseAsync(Guid offenseId, CancellationToken cancellationToken = default)
    {
        await GetOwnedOffenseAsync(offenseId);
        var tenantId = GetTenantId();
        var entities = await _procedureRepository.GetByOffenseIdAsync(offenseId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffOffenseProcedureDto> AddProcedureAsync(CreateStaffOffenseProcedureDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedOffenseAsync(createDto.OffenseId);

        var entity = createDto.ToEntity(tenantId, userId);

        await _procedureRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<StaffOffenseProcedureDto> UpdateProcedureAsync(UpdateStaffOffenseProcedureDto updateDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProcedureAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, userId);

        await _procedureRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteProcedureAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProcedureAsync(id);

        await _procedureRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IEnumerable<StaffOffenseProcedureDto>> ReorderProceduresAsync(
        Guid offenseId,
        IReadOnlyList<Guid> orderedProcedureIds,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (orderedProcedureIds.Count == 0)
            throw new ArgumentException("At least one procedure id is required.");

        await GetOwnedOffenseAsync(offenseId);
        var tenantId = GetTenantId();
        var procedures = (await _procedureRepository.GetByOffenseIdAsync(offenseId))
            .Where(p => p.TenantId == tenantId)
            .ToList();

        if (procedures.Count == 0)
            throw new ArgumentException($"No procedures found for offense '{offenseId}'.");

        if (orderedProcedureIds.Count != procedures.Count)
            throw new ArgumentException("Ordered id count must match the number of procedures for this offense.");

        var procedureIds = procedures.Select(p => p.Id).ToHashSet();
        if (orderedProcedureIds.Any(id => !procedureIds.Contains(id))
            || orderedProcedureIds.Distinct().Count() != orderedProcedureIds.Count)
            throw new ArgumentException("Ordered procedure ids must match this offense exactly, with no duplicates.");

        var byId = procedures.ToDictionary(p => p.Id);

        // Avoid unique-index (OffenseId, Sequence) conflicts during renumbering.
        for (var i = 0; i < orderedProcedureIds.Count; i++)
        {
            var entity = byId[orderedProcedureIds[i]];
            entity.Sequence = 1000 + i;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userId.ToString();
            await _procedureRepository.UpdateAsync(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < orderedProcedureIds.Count; i++)
        {
            var entity = byId[orderedProcedureIds[i]];
            entity.Sequence = i + 1;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userId.ToString();
            await _procedureRepository.UpdateAsync(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Reordered {Count} procedure steps for offense {OffenseId}",
            orderedProcedureIds.Count,
            offenseId);

        return orderedProcedureIds.Select(id => byId[id].ToDto()).ToList();
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINARY ACTION TYPE SERVICE
// ============================================================================

#region Staff Disciplinary Action Type Service

public class StaffDisciplinaryActionTypeService : IStaffDisciplinaryActionTypeService
{
    private readonly IStaffDisciplinaryActionTypeRepository _actionTypeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplinaryActionTypeService> _logger;

    public StaffDisciplinaryActionTypeService(
        IStaffDisciplinaryActionTypeRepository actionTypeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplinaryActionTypeService> logger)
    {
        _actionTypeRepository = actionTypeRepository;
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

    private async Task<StaffDisciplinaryActionType> GetOwnedActionTypeAsync(Guid id)
    {
        var entity = await _actionTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Disciplinary action type with ID '{id}' not found.");
        return entity;
    }

    public async Task<StaffDisciplinaryActionTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _actionTypeRepository.GetByIdAsync(id);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<StaffDisciplinaryActionTypeDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _actionTypeRepository.GetByCodeAsync(code);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffDisciplinaryActionTypeSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _actionTypeRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplinaryActionTypeSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _actionTypeRepository.GetActiveTypesAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<StaffDisciplinaryActionTypeDto> CreateAsync(CreateStaffDisciplinaryActionTypeDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        if (await _actionTypeRepository.CodeExistsAsync(createDto.Code, tenantId))
            throw new InvalidOperationException($"A disciplinary action type with code '{createDto.Code}' already exists.");

        var entity = createDto.ToEntity(tenantId, userId);

        await _actionTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary action type created: {Code} — {Name}", entity.Code, entity.Name);

        return entity.ToDto();
    }

    public async Task<StaffDisciplinaryActionTypeDto> UpdateAsync(UpdateStaffDisciplinaryActionTypeDto updateDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActionTypeAsync(updateDto.Id);

        if (!string.IsNullOrWhiteSpace(updateDto.Code)
            && !string.Equals(updateDto.Code, entity.Code, StringComparison.OrdinalIgnoreCase)
            && await _actionTypeRepository.CodeExistsAsync(updateDto.Code, entity.TenantId))
            throw new InvalidOperationException($"A disciplinary action type with code '{updateDto.Code}' already exists.");

        entity.UpdateEntity(updateDto, userId);

        await _actionTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActionTypeAsync(id);

        await _actionTypeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Disciplinary action type deleted: {Id}", id);

        return true;
    }
}

#endregion
