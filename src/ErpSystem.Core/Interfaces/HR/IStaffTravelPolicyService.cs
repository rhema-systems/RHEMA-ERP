using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 6: POLICY & VENDOR SERVICE
// ============================================================================

#region Staff Travel Policy Service

public interface IStaffTravelPolicyService
{
    // Policies
    Task<StaffTravelPolicyDto> GetPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelPolicySummaryDto>> GetAllPoliciesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelPolicySummaryDto>> GetCurrentPoliciesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelPolicySummaryDto>> GetApplicablePoliciesAsync(Guid? staffLevelId, Guid? organizationUnitId, DateOnly onDate, CancellationToken cancellationToken = default);
    Task<StaffTravelPolicyDto> CreatePolicyAsync(CreateStaffTravelPolicyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelPolicyDto> UpdatePolicyAsync(UpdateStaffTravelPolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeletePolicyAsync(Guid id, CancellationToken cancellationToken = default);

    // Policy rules
    Task<StaffTravelPolicyRuleDto> AddRuleAsync(CreateStaffTravelPolicyRuleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelPolicyRuleDto>> GetRulesAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelPolicyRuleDto>> GetActiveRulesAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<StaffTravelPolicyRuleDto> UpdateRuleAsync(UpdateStaffTravelPolicyRuleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);

    // Policy exceptions
    Task<StaffTravelPolicyExceptionDto> CreateExceptionAsync(CreateStaffTravelPolicyExceptionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelPolicyExceptionDto>> GetExceptionsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelPolicyExceptionDto>> GetPendingExceptionsAsync(CancellationToken cancellationToken = default);
    Task<bool> DecideExceptionAsync(DecideStaffTravelPolicyExceptionDto decideDto, CancellationToken cancellationToken = default);

    // Vendors
    Task<StaffTravelVendorDto> GetVendorByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffTravelVendorDto?> GetVendorByCodeAsync(string vendorCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelVendorSummaryDto>> GetAllVendorsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelVendorSummaryDto>> GetActiveVendorsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelVendorSummaryDto>> GetVendorsByTypeAsync(TravelVendorType vendorType, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelVendorSummaryDto>> GetPreferredVendorsAsync(TravelVendorType? vendorType = null, CancellationToken cancellationToken = default);
    Task<StaffTravelVendorDto> CreateVendorAsync(CreateStaffTravelVendorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelVendorDto> UpdateVendorAsync(UpdateStaffTravelVendorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteVendorAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion
