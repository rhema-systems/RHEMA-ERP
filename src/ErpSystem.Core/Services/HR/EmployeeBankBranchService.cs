using ErpSystem.Core.Services.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Application service for Bank Branch reference data.
/// Responsibilities:
/// - CRUD operations, tenant-scoped
/// - Unique Code invariant enforcement (per bank)
/// - Delete safety (blocked when referenced by EmployeeBankDetail)
/// </summary>
public class EmployeeBankBranchService : IEmployeeBankBranchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EmployeeBankBranchService> _logger;

    public EmployeeBankBranchService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<EmployeeBankBranchService> logger)
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

    public async Task<IReadOnlyList<EmployeeBankBranchDto>> GetByBankAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var items = await _unitOfWork.Repository<EmployeeBankBranch>()
            .GetQueryable(br => br.TenantId == tenantId && !br.IsDeleted && br.BankId == bankId)
            .Include(br => br.Bank)
            .Include(br => br.Country)
            .OrderBy(br => br.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return items.Select(br => br.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<EmployeeBankBranchDto>> GetActiveByBankAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var items = await _unitOfWork.Repository<EmployeeBankBranch>()
            .GetQueryable(br => br.TenantId == tenantId && !br.IsDeleted && br.BankId == bankId && br.IsActive)
            .Include(br => br.Bank)
            .Include(br => br.Country)
            .OrderBy(br => br.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return items.Select(br => br.ToDto()).ToList();
    }

    public async Task<EmployeeBankBranchDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await FindOrThrowAsync(tenantId, id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeBankBranchDto?> GetByCodeAsync(Guid tenantId, Guid bankId, string code, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var normalized = NormalizeCode(code);
        var entity = await _unitOfWork.Repository<EmployeeBankBranch>()
            .GetQueryable(br => br.TenantId == tenantId && !br.IsDeleted && br.BankId == bankId && br.Code == normalized)
            .Include(br => br.Bank)
            .Include(br => br.Country)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return entity?.ToDto();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public async Task<EmployeeBankBranchDto> CreateAsync(Guid tenantId, CreateEmployeeBankBranchDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        tenantId = RequireCurrentTenant(tenantId);

        // Ensure parent bank exists in this tenant
        var bankExists = await _unitOfWork.Repository<EmployeeBank>()
            .ExistsAsync(b => b.TenantId == tenantId && !b.IsDeleted && b.Id == dto.BankId);

        if (!bankExists)
            throw new ArgumentException($"Bank with ID '{dto.BankId}' not found.");

        // Enforce unique Code per bank (when provided)
        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var normalizedCode = NormalizeCode(dto.Code);
            if (await CodeExistsAsync(tenantId, dto.BankId, normalizedCode, null, cancellationToken))
                throw new InvalidOperationException($"A branch with code '{normalizedCode}' already exists for this bank.");
            dto.Code = normalizedCode;
        }

        var entity = dto.ToEntity();
        entity.TenantId = tenantId;

        await _unitOfWork.Repository<EmployeeBankBranch>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with nav props for accurate DTO
        var created = await FindOrThrowAsync(tenantId, entity.Id, cancellationToken);
        _logger.LogInformation("BankBranch '{Name}' created for bank {BankId}, tenant {TenantId}.", entity.Name, entity.BankId, tenantId);
        return created.ToDto();
    }

    public async Task<EmployeeBankBranchDto> UpdateAsync(Guid tenantId, UpdateEmployeeBankBranchDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        tenantId = RequireCurrentTenant(tenantId);

        var entity = await FindOrThrowAsync(tenantId, dto.Id, cancellationToken);

        if (dto.Code != null && !string.IsNullOrWhiteSpace(dto.Code))
        {
            var normalizedCode = NormalizeCode(dto.Code);
            if (await CodeExistsAsync(tenantId, entity.BankId, normalizedCode, entity.Id, cancellationToken))
                throw new InvalidOperationException($"A branch with code '{normalizedCode}' already exists for this bank.");
            dto.Code = normalizedCode;
        }

        dto.Apply(entity);
        entity.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<EmployeeBankBranch>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("BankBranch '{Name}' updated for tenant {TenantId}.", entity.Name, tenantId);
        return entity.ToDto();
    }

    public async Task<EmployeeBankBranchDto> ActivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await FindOrThrowAsync(tenantId, id, cancellationToken);
        if (!entity.IsActive)
        {
            entity.IsActive = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<EmployeeBankBranch>().UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return entity.ToDto();
    }

    public async Task<EmployeeBankBranchDto> DeactivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await FindOrThrowAsync(tenantId, id, cancellationToken);
        if (entity.IsActive)
        {
            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<EmployeeBankBranch>().UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await FindOrThrowAsync(tenantId, id, cancellationToken);

        var isReferenced = await _unitOfWork.Repository<EmployeeBankDetail>()
            .ExistsAsync(e => e.TenantId == tenantId && !e.IsDeleted && e.BranchId == id);

        if (isReferenced)
            throw new InvalidOperationException("Cannot delete this branch because it is referenced by one or more employee bank detail records.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.Repository<EmployeeBankBranch>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("BankBranch '{Name}' soft-deleted for tenant {TenantId}.", entity.Name, tenantId);
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<EmployeeBankBranch> FindOrThrowAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _unitOfWork.Repository<EmployeeBankBranch>()
            .GetQueryable(br => br.TenantId == tenantId && !br.IsDeleted && br.Id == id)
            .Include(br => br.Bank)
            .Include(br => br.Country)
            .FirstOrDefaultAsync(cancellationToken);

        return entity ?? throw new ArgumentException($"Bank branch with ID '{id}' not found.");
    }

    private async Task<bool> CodeExistsAsync(Guid tenantId, Guid bankId, string normalizedCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        return await _unitOfWork.Repository<EmployeeBankBranch>()
            .ExistsAsync(br => br.TenantId == tenantId && !br.IsDeleted && br.BankId == bankId
                                && br.Code == normalizedCode
                                && (excludeId == null || br.Id != excludeId));
    }

    private static string NormalizeCode(string code) => code?.Trim().ToUpperInvariant() ?? string.Empty;
}
