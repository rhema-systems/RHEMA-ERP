using ErpSystem.Core.Services.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Application service for Bank (financial institution) reference data.
/// Responsibilities:
/// - CRUD operations, tenant-scoped
/// - Unique Code invariant enforcement
/// - Delete safety (blocked when referenced by EmployeeBankDetail)
/// </summary>
public class EmployeeBankService : IEmployeeBankService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EmployeeBankService> _logger;

    public EmployeeBankService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<EmployeeBankService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
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

    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Live branch counts, keyed by bank.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>BranchCount</c> used to be hardcoded to zero on every read except
    /// <c>GetWithBranchesAsync</c> — the list, the active list, by-id and by-code all answered
    /// "0 branches" however many a bank had. A field that is always present and always wrong is
    /// worse than an absent one; it survived because no screen has ever rendered a bank.
    /// </remarks>
    private async Task<Dictionary<Guid, int>> BranchCountsAsync(
        Guid tenantId, IEnumerable<Guid> bankIds, CancellationToken cancellationToken)
    {
        var ids = bankIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        return await _unitOfWork.Repository<EmployeeBankBranch>()
            .GetQueryable(br => br.TenantId == tenantId && !br.IsDeleted && ids.Contains(br.BankId))
            .GroupBy(br => br.BankId)
            .Select(g => new { BankId = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToDictionaryAsync(x => x.BankId, x => x.Count, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeBankDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var items = await _unitOfWork.Repository<EmployeeBank>()
            .GetQueryable(b => b.TenantId == tenantId && !b.IsDeleted)
            .Include(b => b.Country)
            .OrderBy(b => b.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var counts = await BranchCountsAsync(tenantId, items.Select(b => b.Id), cancellationToken);
        return items.Select(b => b.ToDto(counts.GetValueOrDefault(b.Id))).ToList();
    }

    public async Task<IReadOnlyList<EmployeeBankDto>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var items = await _unitOfWork.Repository<EmployeeBank>()
            .GetQueryable(b => b.TenantId == tenantId && !b.IsDeleted && b.IsActive)
            .Include(b => b.Country)
            .OrderBy(b => b.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var counts = await BranchCountsAsync(tenantId, items.Select(b => b.Id), cancellationToken);
        return items.Select(b => b.ToDto(counts.GetValueOrDefault(b.Id))).ToList();
    }

    /// <summary>
    /// ⚠ <c>BranchCount</c> was hardcoded to zero on every read but <c>GetWithBranchesAsync</c> —
    /// the list, the active list, by-id and by-code all answered "0 branches" however many a bank
    /// had. A field that is always present and always wrong is worse than an absent one, and it was
    /// invisible because no screen has ever rendered a bank. Counted properly now.
    /// </summary>
    public async Task<EmployeeBankDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await FindOrThrowAsync(tenantId, id, cancellationToken);
        var counts = await BranchCountsAsync(tenantId, new[] { id }, cancellationToken);
        return entity.ToDto(counts.GetValueOrDefault(id));
    }

    public async Task<EmployeeBankDto?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var normalized = NormalizeCode(code);
        var entity = await _unitOfWork.Repository<EmployeeBank>()
            .GetQueryable(b => b.TenantId == tenantId && !b.IsDeleted && b.Code == normalized)
            .Include(b => b.Country)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null) return null;

        var counts = await BranchCountsAsync(tenantId, new[] { entity.Id }, cancellationToken);
        return entity.ToDto(counts.GetValueOrDefault(entity.Id));
    }

    public async Task<EmployeeBankDto> GetWithBranchesAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await _unitOfWork.Repository<EmployeeBank>()
            .GetQueryable(b => b.TenantId == tenantId && !b.IsDeleted && b.Id == id)
            .Include(b => b.Country)
            .Include(b => b.Branches.Where(br => !br.IsDeleted))
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Bank with ID '{id}' not found.");

        var branchCount = entity.Branches.Count;
        return entity.ToDto(branchCount);
    }

    public async Task<IReadOnlyList<EmployeeBankBranchDto>> GetBranchesAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await FindOrThrowAsync(tenantId, bankId, cancellationToken);

        var branches = await _unitOfWork.Repository<EmployeeBankBranch>()
            .GetQueryable(br => br.TenantId == tenantId && !br.IsDeleted && br.BankId == bankId)
            .Include(br => br.Bank)
            .Include(br => br.Country)
            .OrderBy(br => br.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return branches.Select(br => br.ToDto()).ToList();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public async Task<EmployeeBankDto> CreateAsync(Guid tenantId, CreateEmployeeBankDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        tenantId = RequireCurrentTenant(tenantId);

        var normalizedCode = NormalizeCode(dto.Code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
            throw new ArgumentException("Bank Code is required.");

        if (await CodeExistsAsync(tenantId, normalizedCode, null, cancellationToken))
            throw new InvalidOperationException($"A bank with code '{normalizedCode}' already exists.");

        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        entity.Code = normalizedCode;

        await _unitOfWork.Repository<EmployeeBank>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bank '{Code}' created for tenant {TenantId}.", entity.Code, tenantId);
        return entity.ToDto(0);
    }

    public async Task<EmployeeBankDto> UpdateAsync(Guid tenantId, UpdateEmployeeBankDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        tenantId = RequireCurrentTenant(tenantId);

        var entity = await FindOrThrowAsync(tenantId, dto.Id, cancellationToken);

        if (dto.Code != null)
        {
            var normalizedCode = NormalizeCode(dto.Code);
            if (await CodeExistsAsync(tenantId, normalizedCode, entity.Id, cancellationToken))
                throw new InvalidOperationException($"A bank with code '{normalizedCode}' already exists.");
            dto.Code = normalizedCode;
        }

        dto.Apply(entity);
        entity.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<EmployeeBank>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bank '{Code}' updated for tenant {TenantId}.", entity.Code, tenantId);
        return entity.ToDto((await BranchCountsAsync(tenantId, new[] { entity.Id }, cancellationToken))
            .GetValueOrDefault(entity.Id));
    }

    public async Task<EmployeeBankDto> ActivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await FindOrThrowAsync(tenantId, id, cancellationToken);
        if (!entity.IsActive)
        {
            entity.IsActive = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<EmployeeBank>().UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return entity.ToDto((await BranchCountsAsync(tenantId, new[] { id }, cancellationToken))
            .GetValueOrDefault(id));
    }

    public async Task<EmployeeBankDto> DeactivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await FindOrThrowAsync(tenantId, id, cancellationToken);
        if (entity.IsActive)
        {
            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<EmployeeBank>().UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return entity.ToDto((await BranchCountsAsync(tenantId, new[] { id }, cancellationToken))
            .GetValueOrDefault(id));
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await FindOrThrowAsync(tenantId, id, cancellationToken);

        var isReferenced = await _unitOfWork.Repository<EmployeeBankDetail>()
            .ExistsAsync(e => e.TenantId == tenantId && !e.IsDeleted && e.BankId == id);

        if (isReferenced)
            throw new InvalidOperationException("Cannot delete this bank because it is referenced by one or more employee bank detail records.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.Repository<EmployeeBank>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bank '{Code}' soft-deleted for tenant {TenantId}.", entity.Code, tenantId);
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<EmployeeBank> FindOrThrowAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _unitOfWork.Repository<EmployeeBank>()
            .GetQueryable(b => b.TenantId == tenantId && !b.IsDeleted && b.Id == id)
            .Include(b => b.Country)
            .FirstOrDefaultAsync(cancellationToken);

        return entity ?? throw new ArgumentException($"Bank with ID '{id}' not found.");
    }

    private async Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _unitOfWork.Repository<EmployeeBank>()
            .ExistsAsync(b => b.TenantId == tenantId && !b.IsDeleted && b.Code == normalizedCode
                               && (excludeId == null || b.Id != excludeId));
    }

    private static string NormalizeCode(string code) => code?.Trim().ToUpperInvariant() ?? string.Empty;
}
