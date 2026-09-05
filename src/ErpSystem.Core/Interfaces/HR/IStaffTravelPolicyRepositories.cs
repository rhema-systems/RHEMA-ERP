using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 6: POLICY & VENDOR
// ============================================================================

#region Staff Travel Policy

public interface IStaffTravelPolicyRepository : IGenericRepository<StaffTravelPolicy>
{
    /// <summary>Returns a policy with its rules loaded.</summary>
    Task<StaffTravelPolicy?> GetWithRulesAsync(Guid id);

    /// <summary>Returns all current-version policies.</summary>
    Task<IEnumerable<StaffTravelPolicy>> GetCurrentVersionsAsync();

    /// <summary>
    /// Returns current-version policies applicable on the given date for the supplied staff level
    /// and organization unit, ordered most-specific first.
    /// </summary>
    Task<IEnumerable<StaffTravelPolicy>> GetApplicablePoliciesAsync(Guid? staffLevelId, Guid? organizationUnitId, DateOnly onDate);
}

#endregion

#region Staff Travel Policy Rule

public interface IStaffTravelPolicyRuleRepository : IGenericRepository<StaffTravelPolicyRule>
{
    /// <summary>Returns all rules for a policy.</summary>
    Task<IEnumerable<StaffTravelPolicyRule>> GetByPolicyIdAsync(Guid policyId);

    /// <summary>Returns the active rules for a policy.</summary>
    Task<IEnumerable<StaffTravelPolicyRule>> GetActiveRulesAsync(Guid policyId);

    /// <summary>Returns a rule by its code within a policy.</summary>
    Task<StaffTravelPolicyRule?> GetByRuleCodeAsync(Guid policyId, string ruleCode);
}

#endregion

#region Staff Travel Policy Exception

public interface IStaffTravelPolicyExceptionRepository : IGenericRepository<StaffTravelPolicyException>
{
    /// <summary>Returns all policy exceptions raised against a request, with rule loaded.</summary>
    Task<IEnumerable<StaffTravelPolicyException>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns all exceptions raised against a particular policy rule.</summary>
    Task<IEnumerable<StaffTravelPolicyException>> GetByRuleIdAsync(Guid policyRuleId);

    /// <summary>Returns exceptions awaiting a decision.</summary>
    Task<IEnumerable<StaffTravelPolicyException>> GetPendingAsync();
}

#endregion
