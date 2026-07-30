using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Benefit Policy application service.
///
/// Responsibilities:
/// - Orchestrates Benefit Policy use cases
/// - Enforces policy invariants (effective date validation, code uniqueness)
/// - Applies safety checks (prevent delete when referenced)
/// - Uses repositories for data access only
/// - Uses <see cref="IUnitOfWork"/> for transactional persistence
///
/// The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
/// TenantId auto-stamp are inert. This service scopes every read and mutation to the authenticated
/// tenant explicitly.
/// </summary>
public class BenefitPolicyService : IBenefitPolicyService
{
    private readonly IBenefitPolicyRepository _benefitPolicyRepository;
    private readonly IGenericRepository<EmployeePositionBenefit> _positionBenefitRepository;
    private readonly IGenericRepository<BenefitGradeValue> _gradeValueRepository;
    private readonly IGenericRepository<PayComponent> _payComponentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<BenefitPolicyService> _logger;

    public BenefitPolicyService(
        IBenefitPolicyRepository benefitPolicyRepository,
        IGenericRepository<EmployeePositionBenefit> positionBenefitRepository,
        IGenericRepository<BenefitGradeValue> gradeValueRepository,
        IGenericRepository<PayComponent> payComponentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<BenefitPolicyService> logger)
    {
        _benefitPolicyRepository = benefitPolicyRepository ?? throw new ArgumentNullException(nameof(benefitPolicyRepository));
        _positionBenefitRepository = positionBenefitRepository ?? throw new ArgumentNullException(nameof(positionBenefitRepository));
        _gradeValueRepository = gradeValueRepository ?? throw new ArgumentNullException(nameof(gradeValueRepository));
        _payComponentRepository = payComponentRepository ?? throw new ArgumentNullException(nameof(payComponentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserProvider = currentUserProvider ?? throw new ArgumentNullException(nameof(currentUserProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    /// <inheritdoc />
    public async Task<BenefitPolicyDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        var tenantId = GetTenantId();
        var entity = await _benefitPolicyRepository
            .GetQueryable(p => p.Id == id && p.TenantId == tenantId)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.BenefitPolicyRelations)
            .Include(p => p.GradeValues)
            .FirstOrDefaultAsync();

        return entity?.ToReadDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitPolicyDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var entities = await _benefitPolicyRepository
            .GetQueryable(p => p.TenantId == tenantId)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.BenefitPolicyRelations)
            .OrderBy(p => p.PolicyName)
            .ToListAsync();

        return entities.Select(e => e.ToReadDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<PagedResult<BenefitPolicyListDto>> GetPagedAsync(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be >= 1.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be >= 1.");
        }

        var tenantId = GetTenantId();
        var query = _benefitPolicyRepository
            .GetQueryable(p => p.TenantId == tenantId)
            .AsNoTracking();

        var totalCount = await query.CountAsync();

        var entities = await query
            .OrderBy(p => p.PolicyName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<BenefitPolicyListDto>
        {
            Items = entities.Select(e => e.ToListDto()).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitPolicyDto>> GetAllActiveAsync()
    {
        var tenantId = GetTenantId();
        var entities = await _benefitPolicyRepository
            .GetQueryable(p => p.TenantId == tenantId && p.IsActive)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.BenefitPolicyRelations)
            .OrderBy(p => p.PolicyName)
            .ToListAsync();

        return entities.Select(e => e.ToReadDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitPolicyDto>> GetByPolicyTypeAsync(BenefitPolicyType policyType)
    {
        var tenantId = GetTenantId();
        var entities = await _benefitPolicyRepository
            .GetQueryable(p => p.TenantId == tenantId && p.PolicyType == policyType)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.BenefitPolicyRelations)
            .OrderBy(p => p.PolicyName)
            .ToListAsync();

        return entities.Select(e => e.ToReadDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitPolicyDto>> GetEffectivePoliciesAsync(DateTime asOfDate)
    {
        var tenantId = GetTenantId();
        var entities = await _benefitPolicyRepository
            .GetQueryable(p => p.TenantId == tenantId && p.IsActive && p.EffectiveFrom <= asOfDate && (p.EffectiveTo == null || p.EffectiveTo >= asOfDate))
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.BenefitPolicyRelations)
            .OrderBy(p => p.PolicyType)
            .ThenBy(p => p.PolicyName)
            .ToListAsync();

        return entities.Select(e => e.ToReadDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<BenefitPolicyDto> CreateAsync(CreateBenefitPolicyDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ValidateEffectiveDates(dto.EffectiveFrom, dto.EffectiveTo);

        var tenantId = GetTenantId();
        var normalizedCode = NormalizeCode(dto.PolicyCode);
        if (!string.IsNullOrWhiteSpace(normalizedCode))
        {
            var exists = await PolicyCodeExistsAsync(normalizedCode, excludeId: null);
            if (exists)
            {
                throw new InvalidOperationException($"A benefit policy with code '{normalizedCode}' already exists.");
            }
        }

        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        entity.PolicyName = NormalizeName(dto.PolicyName);
        entity.PolicyCode = normalizedCode;

        await _benefitPolicyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Created BenefitPolicy {BenefitPolicyId} for Tenant {TenantId}.",
            entity.Id,
            entity.TenantId);

        return entity.ToReadDto();
    }

    /// <inheritdoc />
    public async Task<BenefitPolicyDto> UpdateAsync(Guid id, UpdateBenefitPolicyDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        ValidateEffectiveDates(dto.EffectiveFrom, dto.EffectiveTo);

        var tenantId = GetTenantId();
        var entity = await _benefitPolicyRepository
            .GetQueryable(p => p.Id == id && p.TenantId == tenantId)
            .AsSplitQuery()
            .Include(p => p.BenefitPolicyRelations)
            .Include(p => p.PositionBenefits)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            throw new ArgumentException($"Benefit policy with ID '{id}' not found.");
        }

        var normalizedCode = NormalizeCode(dto.PolicyCode);
        if (!string.IsNullOrWhiteSpace(normalizedCode))
        {
            var exists = await PolicyCodeExistsAsync(normalizedCode, excludeId: id);
            if (exists)
            {
                throw new InvalidOperationException($"A benefit policy with code '{normalizedCode}' already exists.");
            }
        }

        dto.PolicyName = NormalizeName(dto.PolicyName);
        dto.PolicyCode = normalizedCode;

        dto.UpdateEntity(entity);

        await _benefitPolicyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Updated BenefitPolicy {BenefitPolicyId} for Tenant {TenantId}.",
            entity.Id,
            entity.TenantId);

        return entity.ToReadDto();
    }

    /// <inheritdoc />
    public async Task<bool> DeactivateAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        var tenantId = GetTenantId();
        var entity = await _benefitPolicyRepository
            .GetQueryable(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return false;
        }

        if (entity.IsActive)
        {
            entity.IsActive = false;
            await _benefitPolicyRepository.UpdateAsync(entity);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Deactivated BenefitPolicy {BenefitPolicyId} for Tenant {TenantId}.",
            entity.Id,
            entity.TenantId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        var tenantId = GetTenantId();
        var entity = await _benefitPolicyRepository
            .GetQueryable(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return false;
        }

        var isReferenced = await _positionBenefitRepository
            .GetQueryable(pb => pb.TenantId == tenantId && pb.PolicyId == id)
            .AsNoTracking()
            .AnyAsync();

        if (isReferenced)
        {
            _logger.LogWarning(
                "Blocked delete of BenefitPolicy {BenefitPolicyId} because it is referenced by EmployeePositionBenefit.",
                id);
            throw new InvalidOperationException("Cannot delete this benefit policy because it is referenced by one or more employee position benefits.");
        }

        await _benefitPolicyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Deleted BenefitPolicy {BenefitPolicyId} for Tenant {TenantId}.",
            entity.Id,
            entity.TenantId);

        return true;
    }

    private async Task<bool> PolicyCodeExistsAsync(string policyCode, Guid? excludeId)
    {
        var tenantId = GetTenantId();
        var normalizedUpper = NormalizeCode(policyCode).ToUpperInvariant();

        var query = _benefitPolicyRepository
            .GetQueryable(p => p.TenantId == tenantId && (p.PolicyCode ?? string.Empty).Trim().ToUpperInvariant() == normalizedUpper)
            .AsNoTracking();

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    private static void ValidateEffectiveDates(DateTime effectiveFrom, DateTime? effectiveTo)
    {
        if (effectiveTo.HasValue && effectiveFrom > effectiveTo.Value)
        {
            throw new InvalidOperationException("EffectiveFrom cannot be after EffectiveTo.");
        }
    }

    /// <inheritdoc />
    public async Task<BenefitPolicyLookupsDto> GetLookupsAsync()
    {
        var tenantId = GetTenantId();
        var payComponents = await _payComponentRepository
            .GetQueryable(c => c.TenantId == tenantId && c.IsActive)
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new PayComponentLookupDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name
            })
            .ToListAsync();

        return new BenefitPolicyLookupsDto
        {
            PolicyTypes = ToOptions<BenefitPolicyType>(),
            Recipients = ToOptions<BenefitRecipient>(),
            RelationTypes = ToOptions<BenefitRelationType>(),
            LimitPeriods = ToOptions<BenefitLimitPeriod>(),
            DeliveryTypes = ToOptions<BenefitDeliveryType>(),
            TaxTreatments = ToOptions<BenefitTaxTreatment>(),
            ValuationMethods = ToOptions<BenefitValuationMethod>(),
            CalculationBases = ToOptions<BenefitCalculationBasis>(),
            ContributionResponsibilities = ToOptions<BenefitContributionResponsibility>(),
            Frequencies = ToOptions<PayFrequency>(),
            EnrollmentStatuses = ToOptions<EmployeeBenefitEnrollmentStatus>(),
            PayComponents = payComponents
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitGradeValueDto>> GetGradeValuesAsync(Guid policyId)
    {
        var tenantId = GetTenantId();
        var rows = await _gradeValueRepository
            .GetQueryable(g => g.TenantId == tenantId && g.BenefitPolicyId == policyId)
            .AsNoTracking()
            .ToListAsync();

        return rows.Select(g => g.ToReadDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<BenefitGradeValueDto> AddGradeValueAsync(Guid policyId, CreateBenefitGradeValueDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var tenantId = GetTenantId();
        var policy = await _benefitPolicyRepository
            .GetQueryable(p => p.Id == policyId && p.TenantId == tenantId)
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException($"Benefit policy with ID '{policyId}' not found.");

        var entity = dto.ToEntity();
        entity.BenefitPolicyId = policyId;
        entity.TenantId = policy.TenantId;

        await _gradeValueRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return entity.ToReadDto();
    }

    /// <inheritdoc />
    public async Task<BenefitGradeValueDto> UpdateGradeValueAsync(Guid policyId, Guid gradeValueId, UpdateBenefitGradeValueDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var tenantId = GetTenantId();
        var entity = await _gradeValueRepository
            .GetQueryable(g => g.Id == gradeValueId && g.BenefitPolicyId == policyId && g.TenantId == tenantId)
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException($"Grade value '{gradeValueId}' not found for policy '{policyId}'.");

        entity.SalaryGradeId = dto.SalaryGradeId;
        entity.StaffLevelId = dto.StaffLevelId;
        entity.Amount = dto.Amount;
        entity.Rate = dto.Rate;
        entity.CoverageLimit = dto.CoverageLimit;
        entity.IsActive = dto.IsActive;

        await _gradeValueRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return entity.ToReadDto();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteGradeValueAsync(Guid policyId, Guid gradeValueId)
    {
        var tenantId = GetTenantId();
        var entity = await _gradeValueRepository
            .GetQueryable(g => g.Id == gradeValueId && g.BenefitPolicyId == policyId && g.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return false;
        }

        await _gradeValueRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private static IReadOnlyList<EnumOptionDto> ToOptions<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>()
            .Select(v => new EnumOptionDto
            {
                Value = Convert.ToInt32(v),
                Name = v.ToString(),
                Label = SplitPascalCase(v.ToString())
            })
            .ToList();

    private static string SplitPascalCase(string value)
        => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));

    private static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim();

    private static string NormalizeName(string? name)
        => (name ?? string.Empty).Trim();
}
