using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Entities.HR.StaffTravel;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 6: POLICY & VENDOR SERVICE
// ============================================================================

#region Staff Travel Policy Service

public class StaffTravelPolicyService : IStaffTravelPolicyService
{
    private readonly IStaffTravelPolicyRepository _policyRepository;
    private readonly IStaffTravelPolicyRuleRepository _ruleRepository;
    private readonly IStaffTravelPolicyExceptionRepository _exceptionRepository;
    private readonly IStaffTravelVendorRepository _vendorRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelPolicyService> _logger;

    public StaffTravelPolicyService(
        IStaffTravelPolicyRepository policyRepository,
        IStaffTravelPolicyRuleRepository ruleRepository,
        IStaffTravelPolicyExceptionRepository exceptionRepository,
        IStaffTravelVendorRepository vendorRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelPolicyService> logger)
    {
        _policyRepository = policyRepository;
        _ruleRepository = ruleRepository;
        _exceptionRepository = exceptionRepository;
        _vendorRepository = vendorRepository;
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

    private async Task<StaffTravelPolicy> GetOwnedPolicyAsync(Guid id)
    {
        var entity = await _policyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Travel policy with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelPolicyRule> GetOwnedRuleAsync(Guid id)
    {
        var entity = await _ruleRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Policy rule with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelPolicyException> GetOwnedExceptionAsync(Guid id)
    {
        var entity = await _exceptionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Policy exception with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelVendor> GetOwnedVendorAsync(Guid id)
    {
        var entity = await _vendorRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Vendor with ID '{id}' not found.");
        return entity;
    }

    // ---- Policies ----------------------------------------------------------

    public async Task<StaffTravelPolicyDto> GetPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _policyRepository.GetWithRulesAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Travel policy with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelPolicySummaryDto>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _policyRepository.GetAllAsync())
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelPolicySummaryDto>> GetCurrentPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _policyRepository.GetCurrentVersionsAsync())
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelPolicySummaryDto>> GetApplicablePoliciesAsync(Guid? staffLevelId, Guid? organizationUnitId, DateOnly onDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _policyRepository.GetApplicablePoliciesAsync(staffLevelId, organizationUnitId, onDate))
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.ToSummaryDto())
            .ToList();
    }

    public async Task<StaffTravelPolicyDto> CreatePolicyAsync(CreateStaffTravelPolicyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _policyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Travel policy created: {PolicyName} v{Version}", entity.PolicyName, entity.VersionNumber);
        return entity.ToDto();
    }

    public async Task<StaffTravelPolicyDto> UpdatePolicyAsync(UpdateStaffTravelPolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPolicyAsync(updateDto.Id);

        var entity = await _policyRepository.GetByIdAsync(updateDto.Id);
        entity!.UpdateEntity(updateDto, updatedByUserId);
        await _policyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _policyRepository.GetWithRulesAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Travel policy with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<bool> DeletePolicyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPolicyAsync(id);
        await _policyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Policy rules ------------------------------------------------------

    public async Task<StaffTravelPolicyRuleDto> AddRuleAsync(CreateStaffTravelPolicyRuleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPolicyAsync(createDto.PolicyId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _ruleRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelPolicyRuleDto>> GetRulesAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPolicyAsync(policyId);
        var tenantId = GetTenantId();
        return (await _ruleRepository.GetByPolicyIdAsync(policyId))
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelPolicyRuleDto>> GetActiveRulesAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPolicyAsync(policyId);
        var tenantId = GetTenantId();
        return (await _ruleRepository.GetActiveRulesAsync(policyId))
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.ToDto())
            .ToList();
    }

    public async Task<StaffTravelPolicyRuleDto> UpdateRuleAsync(UpdateStaffTravelPolicyRuleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRuleAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _ruleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRuleAsync(ruleId);
        await _ruleRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Policy exceptions -------------------------------------------------

    public async Task<StaffTravelPolicyExceptionDto> CreateExceptionAsync(CreateStaffTravelPolicyExceptionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _exceptionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelPolicyExceptionDto>> GetExceptionsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _exceptionRepository.GetByRequestIdAsync(requestId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelPolicyExceptionDto>> GetPendingExceptionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _exceptionRepository.GetPendingAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto())
            .ToList();
    }

    public async Task<bool> DecideExceptionAsync(DecideStaffTravelPolicyExceptionDto decideDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExceptionAsync(decideDto.ExceptionId);

        if (entity.Status != TravelPolicyExceptionStatus.Pending)
            throw new InvalidOperationException("Only pending exceptions can be decided.");

        entity.Status = decideDto.Status;
        entity.ApprovedById = decideDto.ApprovedById;
        entity.DecidedAt = decideDto.DecidedAt;
        entity.UpdatedBy = decideDto.ApprovedById.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _exceptionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Vendors -----------------------------------------------------------

    public async Task<StaffTravelVendorDto> GetVendorByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVendorAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffTravelVendorDto?> GetVendorByCodeAsync(string vendorCode, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _vendorRepository.GetByVendorCodeAsync(vendorCode);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelVendorSummaryDto>> GetAllVendorsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _vendorRepository.GetAllAsync())
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVendorSummaryDto>> GetActiveVendorsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _vendorRepository.GetActiveVendorsAsync())
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVendorSummaryDto>> GetVendorsByTypeAsync(TravelVendorType vendorType, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _vendorRepository.GetByTypeAsync(vendorType))
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVendorSummaryDto>> GetPreferredVendorsAsync(TravelVendorType? vendorType = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _vendorRepository.GetPreferredVendorsAsync(vendorType))
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<StaffTravelVendorDto> CreateVendorAsync(CreateStaffTravelVendorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _vendorRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Travel vendor created: {VendorCode}", entity.VendorCode);
        return entity.ToDto();
    }

    public async Task<StaffTravelVendorDto> UpdateVendorAsync(UpdateStaffTravelVendorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVendorAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _vendorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteVendorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVendorAsync(id);
        await _vendorRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
