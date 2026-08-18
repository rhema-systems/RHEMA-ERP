using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Entities.HR.StaffTravel;
using Microsoft.EntityFrameworkCore;
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
        return (await _policyRepository.GetQueryable()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted)
                .Include(p => p.Rules)
                .Include(p => p.ApprovedBy)
                .ToListAsync(cancellationToken))
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

    /// <summary>
    /// Raises a travel policy. <b>It is a draft: it enforces nothing until it is approved.</b>
    /// </summary>
    /// <remarks>
    /// A policy caps what everyone may spend on travel and, since slice 8, actually refuses bookings
    /// above those caps — so authoring one and having it bind immediately would let any
    /// <c>HR.Travel.Write</c> holder set the organisation's travel spending rules unilaterally.
    /// <see cref="StaffTravelPolicyGuard"/> ignores an unapproved policy entirely.
    /// </remarks>
    public async Task<StaffTravelPolicyDto> CreatePolicyAsync(CreateStaffTravelPolicyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        // A new policy never arrives current — approval is what puts one in force (ApprovePolicyAsync).
        // Accepting the payload's word for it let two policies covering the same scope both claim to
        // be in force, and the guard then picked between them by ordering alone.
        entity.IsCurrentVersion = false;
        entity.ApprovedById = null;
        entity.ApprovedAt = null;

        await _policyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Travel policy drafted: {PolicyName} v{Version}", entity.PolicyName, entity.VersionNumber);
        return entity.ToDto();
    }

    /// <summary>
    /// Approves a policy, making it eligible to enforce its caps.
    /// </summary>
    /// <remarks>
    /// <para><b>Admin-gated, and the approver is the token's.</b> <c>ApprovedById</c> and
    /// <c>ApprovedAt</c> existed on the entity and the read DTO with <b>no writer anywhere</b> —
    /// the reader-with-no-writer shape — so every policy in every tenant was unapproved and the
    /// field was decoration. It stopped being decoration when policy caps started refusing
    /// bookings.</para>
    ///
    /// <para>Approving also makes the policy the current version for its scope, superseding
    /// whichever policy held that place: approving a rule and then separately remembering to
    /// activate it is two chances to get it wrong, and a policy approved but not in force is not a
    /// state anyone asked for.</para>
    /// </remarks>
    public async Task<StaffTravelPolicyDto> ApprovePolicyAsync(
        Guid policyId, Guid approverEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPolicyAsync(policyId);

        if (entity.ApprovedById is not null)
            throw new InvalidOperationException("This policy has already been approved.");

        entity.ApprovedById = approverEmployeeId;
        entity.ApprovedAt = DateTime.UtcNow;
        await SupersedeSiblingsAsync(entity, cancellationToken);
        entity.IsCurrentVersion = true;

        await _policyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Travel policy approved and now in force: {PolicyName} v{Version}",
            entity.PolicyName, entity.VersionNumber);

        var refreshed = await _policyRepository.GetWithRulesAsync(entity.Id);
        return (refreshed ?? entity).ToDto();
    }

    /// <summary>
    /// Stands down every other current policy covering the same scope, so exactly one is in force.
    /// </summary>
    /// <remarks>
    /// Scope is the pair the guard resolves on — the organisation unit and the staff-level band.
    /// Two policies covering different units may both be current; two covering the same one may
    /// not, because the guard would then pick between them by ordering alone and which cap applied
    /// would be an accident.
    /// </remarks>
    private async Task SupersedeSiblingsAsync(
        StaffTravelPolicy entity, CancellationToken cancellationToken)
    {
        var siblings = (await _policyRepository.GetCurrentVersionsAsync())
            .Where(p => p.TenantId == entity.TenantId
                     && p.Id != entity.Id
                     && p.AppliesToOrganizationUnitId == entity.AppliesToOrganizationUnitId
                     && p.AppliesToLevelFromId == entity.AppliesToLevelFromId
                     && p.AppliesToLevelToId == entity.AppliesToLevelToId)
            .ToList();

        foreach (var sibling in siblings)
        {
            sibling.IsCurrentVersion = false;
            await _policyRepository.UpdateAsync(sibling);
        }
    }

    public async Task<StaffTravelPolicyDto> UpdatePolicyAsync(UpdateStaffTravelPolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPolicyAsync(updateDto.Id);

        var entity = await _policyRepository.GetByIdAsync(updateDto.Id);

        // Editing an approved policy would change what everyone may spend without anyone approving
        // the change. Raise a new version instead — that is what versions are for.
        if (entity!.ApprovedById is not null)
            throw new InvalidOperationException(
                "An approved policy cannot be edited. Raise a new version and have it approved.");

        var wasCurrent = entity.IsCurrentVersion;
        entity.UpdateEntity(updateDto, updatedByUserId);
        entity.IsCurrentVersion = wasCurrent;   // not the payload's to change; approval sets it

        await _policyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _policyRepository.GetWithRulesAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Travel policy with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    /// <summary>
    /// Stands an approved policy down so it no longer caps anything, without deleting it.
    /// </summary>
    /// <remarks>
    /// <para><b>The verb that was missing.</b> Once approval began putting a policy in force, the
    /// only ways one could stop binding were another policy for <i>exactly</i> the same scope being
    /// approved, or an administrator hard-deleting it. A policy approved in error therefore capped
    /// everyone's travel until somebody drafted and approved a replacement with an identical scope
    /// — which is a strange thing to have to do to undo a mistake.</para>
    ///
    /// <para><b>It stays approved.</b> Approval is a fact about the past and withdrawing does not
    /// unmake it; what changes is whether the policy is currently in force. Deleting would erase
    /// the record of a rule that really did govern spending for a period, which is the opposite of
    /// what an audit trail is for.</para>
    /// </remarks>
    public async Task<StaffTravelPolicyDto> WithdrawPolicyAsync(
        Guid policyId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPolicyAsync(policyId);

        if (!entity.IsCurrentVersion)
            throw new InvalidOperationException("This policy is not in force.");

        entity.IsCurrentVersion = false;
        await _policyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Travel policy withdrawn from force: {PolicyName} v{Version}",
            entity.PolicyName, entity.VersionNumber);

        var refreshed = await _policyRepository.GetWithRulesAsync(entity.Id);
        return (refreshed ?? entity).ToDto();
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

    public async Task<bool> DecideExceptionAsync(DecideStaffTravelPolicyExceptionDto decideDto, Guid deciderEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExceptionAsync(decideDto.ExceptionId);

        if (entity.Status != TravelPolicyExceptionStatus.Pending)
            throw new InvalidOperationException("Only pending exceptions can be decided.");

        entity.Status = decideDto.Status;
        entity.ApprovedById = deciderEmployeeId;   // the caller, not a payload value
        entity.DecidedAt = decideDto.DecidedAt;
        // Audit field: the USER id, not the Employee FK stamped above.
        entity.UpdatedBy = _currentUserProvider.UserId.ToString();
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
        return (await _vendorRepository.GetQueryable()
                .Where(v => v.TenantId == tenantId && !v.IsDeleted)
                .ToListAsync(cancellationToken))
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
