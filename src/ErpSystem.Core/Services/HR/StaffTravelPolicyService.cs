using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelPolicyService> _logger;

    public StaffTravelPolicyService(
        IStaffTravelPolicyRepository policyRepository,
        IStaffTravelPolicyRuleRepository ruleRepository,
        IStaffTravelPolicyExceptionRepository exceptionRepository,
        IStaffTravelVendorRepository vendorRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelPolicyService> logger)
    {
        _policyRepository = policyRepository;
        _ruleRepository = ruleRepository;
        _exceptionRepository = exceptionRepository;
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ---- Policies ----------------------------------------------------------

    public async Task<StaffTravelPolicyDto> GetPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _policyRepository.GetWithRulesAsync(id);
        if (entity == null)
            throw new ArgumentException($"Travel policy with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelPolicySummaryDto>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
        => (await _policyRepository.GetAllAsync()).Select(p => p.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelPolicySummaryDto>> GetCurrentPoliciesAsync(CancellationToken cancellationToken = default)
        => (await _policyRepository.GetCurrentVersionsAsync()).Select(p => p.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelPolicySummaryDto>> GetApplicablePoliciesAsync(Guid? staffLevelId, Guid? organizationUnitId, DateOnly onDate, CancellationToken cancellationToken = default)
        => (await _policyRepository.GetApplicablePoliciesAsync(staffLevelId, organizationUnitId, onDate)).Select(p => p.ToSummaryDto()).ToList();

    public async Task<StaffTravelPolicyDto> CreatePolicyAsync(CreateStaffTravelPolicyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _policyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Travel policy created: {PolicyName} v{Version}", entity.PolicyName, entity.VersionNumber);
        return entity.ToDto();
    }

    public async Task<StaffTravelPolicyDto> UpdatePolicyAsync(UpdateStaffTravelPolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _policyRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Travel policy with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _policyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await _policyRepository.GetWithRulesAsync(entity.Id))!.ToDto();
    }

    public async Task<bool> DeletePolicyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _policyRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Travel policy with ID '{id}' not found.");

        await _policyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Policy rules ------------------------------------------------------

    public async Task<StaffTravelPolicyRuleDto> AddRuleAsync(CreateStaffTravelPolicyRuleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _ruleRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelPolicyRuleDto>> GetRulesAsync(Guid policyId, CancellationToken cancellationToken = default)
        => (await _ruleRepository.GetByPolicyIdAsync(policyId)).Select(r => r.ToDto()).ToList();

    public async Task<IEnumerable<StaffTravelPolicyRuleDto>> GetActiveRulesAsync(Guid policyId, CancellationToken cancellationToken = default)
        => (await _ruleRepository.GetActiveRulesAsync(policyId)).Select(r => r.ToDto()).ToList();

    public async Task<StaffTravelPolicyRuleDto> UpdateRuleAsync(UpdateStaffTravelPolicyRuleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _ruleRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Policy rule with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _ruleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var entity = await _ruleRepository.GetByIdAsync(ruleId);
        if (entity == null)
            throw new ArgumentException($"Policy rule with ID '{ruleId}' not found.");

        await _ruleRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Policy exceptions -------------------------------------------------

    public async Task<StaffTravelPolicyExceptionDto> CreateExceptionAsync(CreateStaffTravelPolicyExceptionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _exceptionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelPolicyExceptionDto>> GetExceptionsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _exceptionRepository.GetByRequestIdAsync(requestId)).Select(e => e.ToDto()).ToList();

    public async Task<IEnumerable<StaffTravelPolicyExceptionDto>> GetPendingExceptionsAsync(CancellationToken cancellationToken = default)
        => (await _exceptionRepository.GetPendingAsync()).Select(e => e.ToDto()).ToList();

    public async Task<bool> DecideExceptionAsync(DecideStaffTravelPolicyExceptionDto decideDto, CancellationToken cancellationToken = default)
    {
        var entity = await _exceptionRepository.GetByIdAsync(decideDto.ExceptionId);
        if (entity == null)
            throw new ArgumentException($"Policy exception with ID '{decideDto.ExceptionId}' not found.");

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
        var entity = await _vendorRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Vendor with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffTravelVendorDto?> GetVendorByCodeAsync(string vendorCode, CancellationToken cancellationToken = default)
    {
        var entity = await _vendorRepository.GetByVendorCodeAsync(vendorCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffTravelVendorSummaryDto>> GetAllVendorsAsync(CancellationToken cancellationToken = default)
        => (await _vendorRepository.GetAllAsync()).Select(v => v.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelVendorSummaryDto>> GetActiveVendorsAsync(CancellationToken cancellationToken = default)
        => (await _vendorRepository.GetActiveVendorsAsync()).Select(v => v.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelVendorSummaryDto>> GetVendorsByTypeAsync(TravelVendorType vendorType, CancellationToken cancellationToken = default)
        => (await _vendorRepository.GetByTypeAsync(vendorType)).Select(v => v.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelVendorSummaryDto>> GetPreferredVendorsAsync(TravelVendorType? vendorType = null, CancellationToken cancellationToken = default)
        => (await _vendorRepository.GetPreferredVendorsAsync(vendorType)).Select(v => v.ToSummaryDto()).ToList();

    public async Task<StaffTravelVendorDto> CreateVendorAsync(CreateStaffTravelVendorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _vendorRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Travel vendor created: {VendorCode}", entity.VendorCode);
        return entity.ToDto();
    }

    public async Task<StaffTravelVendorDto> UpdateVendorAsync(UpdateStaffTravelVendorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _vendorRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Vendor with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _vendorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteVendorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _vendorRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Vendor with ID '{id}' not found.");

        await _vendorRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
