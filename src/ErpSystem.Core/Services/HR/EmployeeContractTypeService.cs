using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc />
public class EmployeeContractTypeService : IEmployeeContractTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EmployeeContractTypeService> _logger;

    public EmployeeContractTypeService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<EmployeeContractTypeService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Every read and write below scopes to the authenticated tenant
    // explicitly, per the RHEMA convention.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private IGenericRepository<EmployeeContractType> Repo => _unitOfWork.Repository<EmployeeContractType>();

    private async Task<EmployeeContractType> GetOwnedAsync(Guid id)
    {
        var entity = await Repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Contract type not found.");
        return entity;
    }

    public async Task<IEnumerable<EmployeeContractTypeDto>> GetAllAsync(
        bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = Repo.GetQueryable().Where(t => t.TenantId == tenantId);
        if (activeOnly) query = query.Where(t => t.IsActive);

        var items = await query.OrderBy(t => t.Name).ToListAsync(cancellationToken);

        // One grouped count rather than a count per row: the picker asks for the whole list, and a
        // retire decision needs to see the reach of the kind it is about to withdraw.
        var ids = items.Select(t => t.Id).ToList();
        var counts = await _unitOfWork.Repository<EmployeeContractDetail>().GetQueryable()
            .Where(c => c.TenantId == tenantId && c.ContractTypeId != null && ids.Contains(c.ContractTypeId.Value))
            .GroupBy(c => c.ContractTypeId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return items.Select(t => ToDto(t, counts.TryGetValue(t.Id, out var n) ? n : 0));
    }

    public async Task<EmployeeContractTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId()) return null;

        var count = await _unitOfWork.Repository<EmployeeContractDetail>().GetQueryable()
            .CountAsync(c => c.TenantId == entity.TenantId && c.ContractTypeId == id, cancellationToken);

        return ToDto(entity, count);
    }

    public async Task<EmployeeContractTypeDto> CreateAsync(
        CreateEmployeeContractTypeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        var name = dto.Name.Trim();
        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();

        await RequireNameAndCodeFreeAsync(tenantId, name, code, null, cancellationToken);

        var entity = new EmployeeContractType
        {
            // Set explicitly: the context auto-stamp is inert, and an unstamped row fails
            // FK_EmployeeContractTypes_Tenants_TenantId.
            TenantId = tenantId,
            Code = code,
            Name = name,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Duration = dto.Duration,
            IsActive = dto.IsActive,
        };

        await Repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contract type created: {Name} ({Duration} months)", entity.Name, entity.Duration);
        return ToDto(entity, 0);
    }

    public async Task<EmployeeContractTypeDto> UpdateAsync(
        Guid id, UpdateEmployeeContractTypeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await GetOwnedAsync(id);

        var name = dto.Name.Trim();
        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();

        await RequireNameAndCodeFreeAsync(entity.TenantId, name, code, id, cancellationToken);

        entity.Code = code;
        entity.Name = name;
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.Duration = dto.Duration;
        entity.IsActive = dto.IsActive;

        await Repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken) ?? ToDto(entity, 0);
    }

    /// <remarks>
    /// Retire, never delete. Contracts name their kind by FK, and removing the row would either
    /// break the constraint or — with a soft delete — leave live contracts pointing at a
    /// vocabulary word nothing can resolve. A soft delete does not release the row its dependants
    /// are still holding.
    /// </remarks>
    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        entity.IsActive = false;
        await Repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contract type retired: {Name}", entity.Name);
    }

    private async Task RequireNameAndCodeFreeAsync(
        Guid tenantId, string name, string? code, Guid? excludeId, CancellationToken cancellationToken)
    {
        var query = Repo.GetQueryable().Where(t => t.TenantId == tenantId);
        if (excludeId.HasValue) query = query.Where(t => t.Id != excludeId.Value);

        if (await query.AnyAsync(t => t.Name == name, cancellationToken))
            throw new InvalidOperationException($"A contract type named '{name}' already exists.");

        if (code != null && await query.AnyAsync(t => t.Code == code, cancellationToken))
            throw new InvalidOperationException($"A contract type with the code '{code}' already exists.");
    }

    private static EmployeeContractTypeDto ToDto(EmployeeContractType e, int contractCount) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Name = e.Name,
        Description = e.Description,
        Duration = e.Duration,
        IsActive = e.IsActive,
        ContractCount = contractCount,
    };
}
