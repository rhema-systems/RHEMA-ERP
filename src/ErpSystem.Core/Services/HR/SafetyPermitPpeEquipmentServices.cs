using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SHE services — Permit-to-Work (E), PPE Management (F) and Equipment (G).
// ============================================================================

#region Permit-to-Work Service

public class ShePermitToWorkService : IShePermitToWorkService
{
    private readonly IShePermitToWorkRepository _permitRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShePermitToWorkService> _logger;

    public ShePermitToWorkService(
        IShePermitToWorkRepository permitRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ShePermitToWorkService> logger)
    {
        _permitRepository = permitRepository;
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

    private async Task<ShePermitToWork> GetOwnedPermitAsync(Guid id)
    {
        var entity = await _permitRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Permit-to-work with ID '{id}' not found.");
        return entity;
    }

    private async Task<ShePermitToWorkWorker> GetOwnedWorkerAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<ShePermitToWorkWorker>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Permit worker with ID '{id}' not found.");
        return entity;
    }

    private async Task<ShePermitToWorkDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<ShePermitToWorkDocument>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Permit document with ID '{id}' not found.");
        return entity;
    }

    private async Task<string> GeneratePermitNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PTW-{year}-";

        // Numeric max, not string ordering: the seeder wrote 3-digit suffixes while this generator
        // emits 4-digit ones, and across mixed widths string ordering picks the wrong "latest"
        // ("002" sorts above "0003"), silently re-issuing taken numbers. Soft-deleted permits keep
        // their number, so they count toward the max too.
        var numbers = await _permitRepository
            .GetQueryableIncludingDeleted(p => p.TenantId == tenantId && p.PermitNumber.StartsWith(prefix))
            .Select(p => p.PermitNumber)
            .ToListAsync(cancellationToken);

        var max = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{max + 1:D4}";
    }

    public async Task<ShePermitToWorkDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _permitRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Permit-to-work with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<ShePermitToWorkDto?> GetByNumberAsync(string permitNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _permitRepository.GetByNumberAsync(permitNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _permitRepository.GetAllSummaryAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByStatusAsync(ShePermitStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _permitRepository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByTypeAsync(ShePermitType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _permitRepository.GetByTypeAsync(type)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _permitRepository.GetActiveAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByContractorAsync(Guid contractorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _permitRepository.GetByContractorAsync(contractorId)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByRequestorAsync(Guid requestedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _permitRepository.GetByRequestorAsync(requestedById)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetExpiringAsync(int daysAhead = 1, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _permitRepository.GetExpiringAsync(daysAhead)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShePermitToWorkSummaryDto>> GetSuspendedAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _permitRepository.GetSuspendedAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<ShePermitToWorkDto> CreateAsync(CreateShePermitToWorkDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        entity.PermitNumber = await GeneratePermitNumberAsync(tenantId, cancellationToken);
        await _permitRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Permit-to-work created: {PermitNumber}", entity.PermitNumber);
        // Write responses re-read through the include-bearing path — the tracked entity's navs
        // (location, requestor, contractor) are unloaded and would map blank.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ShePermitToWorkDto> UpdateAsync(UpdateShePermitToWorkDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(dto.Id);
        if (entity.Status is ShePermitStatus.Completed or ShePermitStatus.Cancelled)
            throw new InvalidOperationException("A completed or cancelled permit cannot be edited.");

        entity.UpdateEntity(dto, userId);
        await _permitRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(id);
        await _permitRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApproveAsync(ApproveShePermitToWorkDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPermitAsync(dto.PermitId);
        if (entity.Status is not (ShePermitStatus.Draft or ShePermitStatus.PendingApproval))
            throw new InvalidOperationException("Only draft or pending permits can be approved.");

        // FR-PTW-002 — approval is blocked until the mandatory safety sections are complete. Gas
        // testing is mandatory only where an atmosphere can kill: hot work and confined spaces.
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(entity.HazardsIdentified)) missing.Add("hazards identified");
        if (string.IsNullOrWhiteSpace(entity.ControlMeasures)) missing.Add("control measures");
        if (entity.PermitType is ShePermitType.HotWork or ShePermitType.ConfinedSpaceEntry
            && string.IsNullOrWhiteSpace(entity.GasTestResults))
            missing.Add("gas test results");
        if (missing.Count > 0)
            throw new InvalidOperationException($"Permit cannot be approved until mandatory fields are completed: {string.Join(", ", missing)}.");

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
        var entity = await GetOwnedPermitAsync(dto.PermitId);
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
        var entity = await GetOwnedPermitAsync(permitId);
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
        var entity = await GetOwnedPermitAsync(dto.PermitId);

        // Only work that was authorised to start can be closed out — a draft is deleted, not
        // closed, and a completed/cancelled permit does not close twice.
        if (entity.Status is not (ShePermitStatus.Active or ShePermitStatus.Suspended))
            throw new InvalidOperationException("Only an active or suspended permit can be closed.");

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
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPermitAsync(dto.PermitToWorkId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<ShePermitToWorkWorker>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<ShePermitToWorkWorkerDto> UpdateWorkerAsync(UpdateShePermitToWorkWorkerDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWorkerAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<ShePermitToWorkWorker>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteWorkerAsync(Guid workerId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWorkerAsync(workerId);
        await _unitOfWork.Repository<ShePermitToWorkWorker>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ShePermitToWorkExtensionDto> AddExtensionAsync(CreateShePermitToWorkExtensionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var permit = await GetOwnedPermitAsync(dto.PermitToWorkId);

        // An extension prolongs live authorisation — nothing else has a window to extend.
        if (permit.Status != ShePermitStatus.Active)
            throw new InvalidOperationException("Only an active permit can be extended.");

        var entity = dto.ToEntity(tenantId, userId);
        // Numbered server-side in sequence; the client does not control it.
        entity.ExtensionNumber = await _unitOfWork.Repository<ShePermitToWorkExtension>().GetQueryable()
            .CountAsync(x => x.PermitToWorkId == permit.Id && !x.IsDeleted, cancellationToken) + 1;
        await _unitOfWork.Repository<ShePermitToWorkExtension>().AddAsync(entity);

        permit.PlannedEndDate = dto.NewEndDate;
        permit.PlannedEndTime = dto.NewEndTime;
        await _permitRepository.UpdateAsync(permit);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Re-read for the approver name — the tracked entity's nav is unloaded and maps blank.
        var full = await GetByIdAsync(permit.Id, cancellationToken);
        return full.Extensions.First(x => x.Id == entity.Id);
    }

    public async Task<ShePermitToWorkDocumentDto> AddDocumentAsync(CreateShePermitToWorkDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPermitAsync(dto.PermitToWorkId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<ShePermitToWorkDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Re-read for the uploader name — the tracked entity's nav is unloaded and maps blank.
        var full = await GetByIdAsync(dto.PermitToWorkId, cancellationToken);
        return full.Documents.First(d => d.Id == entity.Id);
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(documentId);
        await _unitOfWork.Repository<ShePermitToWorkDocument>().DeleteAsync(entity);
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PpeManagementService> _logger;

    public PpeManagementService(
        IPpeTypeRepository typeRepository,
        IPpeInventoryRepository inventoryRepository,
        IPpeIssuanceRepository issuanceRepository,
        IJobRolePpeRequirementRepository requirementRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PpeManagementService> logger)
    {
        _typeRepository = typeRepository;
        _inventoryRepository = inventoryRepository;
        _issuanceRepository = issuanceRepository;
        _requirementRepository = requirementRepository;
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

    private async Task<PpeType> GetOwnedTypeAsync(Guid id)
    {
        var entity = await _typeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"PPE type with ID '{id}' not found.");
        return entity;
    }

    private async Task<PpeInventory> GetOwnedInventoryAsync(Guid id)
    {
        var entity = await _inventoryRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"PPE inventory item with ID '{id}' not found.");
        return entity;
    }

    private async Task<PpeIssuance> GetOwnedIssuanceAsync(Guid id)
    {
        var entity = await _issuanceRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"PPE issuance with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobRolePpeRequirement> GetOwnedRequirementAsync(Guid id)
    {
        var entity = await _requirementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Job-role PPE requirement with ID '{id}' not found.");
        return entity;
    }

    // ── Types ──
    public async Task<IEnumerable<PpeTypeDto>> GetTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _typeRepository.GetActiveAsync() : await _typeRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<PpeTypeDto> GetTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTypeAsync(id);
        return entity.ToDto();
    }

    public async Task<PpeTypeDto> CreateTypeAsync(CreatePpeTypeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var code = dto.Code.Trim();
        var exists = await _typeRepository.GetQueryable()
            .AnyAsync(t => t.TenantId == tenantId && t.Code == code, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A PPE type with code '{code}' already exists for this tenant.");

        var entity = dto.ToEntity(tenantId, userId);
        await _typeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<PpeTypeDto> UpdateTypeAsync(UpdatePpeTypeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTypeAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _typeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTypeAsync(id);
        await _typeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Inventory ──
    public async Task<PpeInventoryDto> GetInventoryItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInventoryAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<PpeInventoryDto>> GetInventoryByTypeAsync(Guid ppeTypeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inventoryRepository.GetByPpeTypeAsync(ppeTypeId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<PpeInventoryDto>> GetBelowReorderLevelAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inventoryRepository.GetBelowReorderLevelAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<PpeInventoryDto> CreateInventoryAsync(CreatePpeInventoryDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedTypeAsync(dto.PpeTypeId);
        var itemCode = dto.ItemCode.Trim();
        var exists = await _inventoryRepository.GetQueryable()
            .AnyAsync(i => i.TenantId == tenantId && i.ItemCode == itemCode, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A PPE inventory item with code '{itemCode}' already exists for this tenant.");

        var entity = dto.ToEntity(tenantId, userId);
        await _inventoryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<PpeInventoryDto> UpdateInventoryAsync(UpdatePpeInventoryDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInventoryAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _inventoryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RestockAsync(RestockPpeInventoryDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInventoryAsync(dto.PpeInventoryId);

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
        var entity = await GetOwnedInventoryAsync(id);
        await _inventoryRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Issuance ──
    public async Task<IEnumerable<PpeIssuanceDto>> GetIssuancesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _issuanceRepository.GetByEmployeeAsync(employeeId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<PpeIssuanceDto>> GetOutstandingIssuancesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _issuanceRepository.GetOutstandingAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<PpeIssuanceDto>> GetOverdueReturnsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _issuanceRepository.GetOverdueReturnsAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<PpeIssuanceDto> IssueAsync(CreatePpeIssuanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedTypeAsync(dto.PpeTypeId);
        var entity = dto.ToEntity(tenantId, userId);
        await _issuanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> ReturnAsync(ReturnPpeIssuanceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIssuanceAsync(dto.IssuanceId);
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
    {
        var tenantId = GetTenantId();
        return (await _requirementRepository.GetByJobRoleAsync(jobRoleCode))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<JobRolePpeRequirementDto> AddRequirementAsync(CreateJobRolePpeRequirementDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedTypeAsync(dto.PpeTypeId);
        var entity = dto.ToEntity(tenantId, userId);
        await _requirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<JobRolePpeRequirementDto> UpdateRequirementAsync(UpdateJobRolePpeRequirementDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequirementAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _requirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequirementAsync(requirementId);
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SafetyEquipmentService> _logger;

    public SafetyEquipmentService(
        ISafetyEquipmentRepository equipmentRepository,
        ISafetyEquipmentInspectionRepository inspectionRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SafetyEquipmentService> logger)
    {
        _equipmentRepository = equipmentRepository;
        _inspectionRepository = inspectionRepository;
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

    private async Task<SafetyEquipment> GetOwnedEquipmentAsync(Guid id)
    {
        var entity = await _equipmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Safety equipment with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyEquipmentInspection> GetOwnedInspectionAsync(Guid id)
    {
        var entity = await _inspectionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Equipment inspection with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyEquipmentInspectionAction> GetOwnedInspectionActionAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyEquipmentInspectionAction>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Equipment inspection action with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyEquipmentMaintenance> GetOwnedMaintenanceAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyEquipmentMaintenance>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Equipment maintenance with ID '{id}' not found.");
        return entity;
    }

    private async Task<string> GenerateEquipmentNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SEQ-{year}-";
        var last = await _equipmentRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && e.EquipmentNumber.StartsWith(prefix))
            .OrderByDescending(e => e.EquipmentNumber)
            .Select(e => e.EquipmentNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            next = n + 1;
        return $"{prefix}{next:D4}";
    }

    public async Task<SafetyEquipmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _equipmentRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Safety equipment with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SafetyEquipmentDto?> GetByNumberAsync(string equipmentNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _equipmentRepository.GetByNumberAsync(equipmentNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _equipmentRepository.GetAllSummaryAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetByStatusAsync(SheSafetyEquipmentStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _equipmentRepository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetByTypeAsync(SheSafetyEquipmentType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _equipmentRepository.GetByTypeAsync(type)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _equipmentRepository.GetByLocationAsync(locationId)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetDueForInspectionAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _equipmentRepository.GetDueForInspectionAsync(daysAhead)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetDueForMaintenanceAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _equipmentRepository.GetDueForMaintenanceAsync(daysAhead)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetExpiringCertificationAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _equipmentRepository.GetExpiringCertificationAsync(daysAhead)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyEquipmentSummaryDto>> GetOutOfServiceAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _equipmentRepository.GetOutOfServiceAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<SafetyEquipmentDto> CreateAsync(CreateSafetyEquipmentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        entity.EquipmentNumber = await GenerateEquipmentNumberAsync(tenantId, cancellationToken);
        await _equipmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Safety equipment created: {EquipmentNumber}", entity.EquipmentNumber);
        return entity.ToDto();
    }

    public async Task<SafetyEquipmentDto> UpdateAsync(UpdateSafetyEquipmentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEquipmentAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _equipmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEquipmentAsync(id);
        await _equipmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Inspections ──
    public async Task<SafetyEquipmentInspectionDto> AddInspectionAsync(CreateSafetyEquipmentInspectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var equipment = await GetOwnedEquipmentAsync(dto.EquipmentId);
        var entity = dto.ToEntity(tenantId, userId);
        await _inspectionRepository.AddAsync(entity);

        equipment.LastInspectionDate = dto.InspectionDate;
        equipment.NextInspectionDueDate = dto.NextInspectionDate;
        await _equipmentRepository.UpdateAsync(equipment);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyEquipmentInspectionDto> UpdateInspectionAsync(UpdateSafetyEquipmentInspectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _inspectionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<SafetyEquipmentInspectionDto>> GetInspectionsForEquipmentAsync(Guid equipmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEquipmentAsync(equipmentId);
        return (await _inspectionRepository.GetByEquipmentIdAsync(equipmentId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SafetyEquipmentInspectionActionDto> AddInspectionActionAsync(CreateSafetyEquipmentInspectionActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedInspectionAsync(dto.SafetyEquipmentInspectionId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyEquipmentInspectionAction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyEquipmentInspectionActionDto> UpdateInspectionActionAsync(UpdateSafetyEquipmentInspectionActionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionActionAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SafetyEquipmentInspectionAction>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteInspectionActionAsync(Guid actionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionActionAsync(actionId);
        await _unitOfWork.Repository<SafetyEquipmentInspectionAction>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Maintenance ──
    public async Task<SafetyEquipmentMaintenanceDto> AddMaintenanceAsync(CreateSafetyEquipmentMaintenanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var equipment = await GetOwnedEquipmentAsync(dto.EquipmentId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyEquipmentMaintenance>().AddAsync(entity);

        equipment.LastMaintenanceDate = dto.MaintenanceDate;
        await _equipmentRepository.UpdateAsync(equipment);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyEquipmentMaintenanceDto> UpdateMaintenanceAsync(UpdateSafetyEquipmentMaintenanceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMaintenanceAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SafetyEquipmentMaintenance>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteMaintenanceAsync(Guid maintenanceId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMaintenanceAsync(maintenanceId);
        await _unitOfWork.Repository<SafetyEquipmentMaintenance>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
