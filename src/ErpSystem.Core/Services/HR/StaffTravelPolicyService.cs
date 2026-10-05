using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Entities.HR;
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
    // Injected so an exception can verify the travel request it is raised against is this
    // tenant's — the foreign key alone was deciding, and it accepts any valid id.
    private readonly IStaffTravelRequestRepository _requestRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HrCurrencyBridge _currency;
    private readonly IHrAudienceResolver _audience;
    private readonly ILogger<StaffTravelPolicyService> _logger;

    public StaffTravelPolicyService(
        IStaffTravelPolicyRepository policyRepository,
        IStaffTravelPolicyRuleRepository ruleRepository,
        IStaffTravelPolicyExceptionRepository exceptionRepository,
        IStaffTravelRequestRepository requestRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        HrCurrencyBridge currency,
        IHrAudienceResolver audience,
        ILogger<StaffTravelPolicyService> logger)
    {
        _currency = currency;
        _audience = audience;
        _policyRepository = policyRepository;
        _ruleRepository = ruleRepository;
        _exceptionRepository = exceptionRepository;
        _requestRepository = requestRepository;
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

    // ---- Policy rules of shape (lane 4) ----------------------------------------

    /// <summary>
    /// The currency a policy's money limits are set in (C3/T-9): the one given, checked against HR's list; else the
    /// policy's own; else the base currency. The hotel cap was a bare number compared with a rate in any currency.
    /// </summary>
    private async Task<string> ResolvePolicyCurrencyAsync(string? given, string? current, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(given))
        {
            var code = given.Trim().ToUpperInvariant();
            await _currency.RequireKnownCurrencyAsync(code, cancellationToken);
            return code;
        }
        if (!string.IsNullOrWhiteSpace(current)) return current.Trim().ToUpperInvariant();
        return await _currency.GetBaseCurrencyCodeAsync(cancellationToken)
               ?? throw new InvalidOperationException(
                   "Finance marks no base currency, so the policy's limits have no currency. Choose one, or set the base currency in Finance.");
    }

    /// <summary>
    /// What a policy must be to cap anything sensibly (lane 4, C2, C3): real cabin classes — an omitted class was
    /// stored as 0, under every booked class, so once approved every flight exceeded the cap; an end not before the
    /// start; a unit and a level band that are this organisation's; and a band that runs from the lower rank to
    /// the higher, which the guard's band test assumes.
    /// </summary>
    private async Task RequirePolicyShapeAsync(StaffTravelPolicy entity, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(entity.MaxFlightClassDomestic) || !Enum.IsDefined(entity.MaxFlightClassInternational))
            throw new InvalidOperationException(
                "Choose the highest cabin class allowed for domestic and for international flights — Economy, Premium " +
                "Economy, Business or First.");

        if (entity.EffectiveTo is DateOnly to && to < entity.EffectiveFrom)
            throw new InvalidOperationException(
                $"The policy ends ({to:d MMM yyyy}) before it starts ({entity.EffectiveFrom:d MMM yyyy}).");

        var tenantId = entity.TenantId;
        if (entity.AppliesToOrganizationUnitId is Guid unitId
            && !await _unitOfWork.Repository<OrganizationUnit>()
                .GetQueryable(u => u.Id == unitId && u.TenantId == tenantId).AnyAsync(cancellationToken))
            throw new ArgumentException($"Organisation unit '{unitId}' not found.");

        var levelIds = new[] { entity.AppliesToLevelFromId, entity.AppliesToLevelToId }.OfType<Guid>().Distinct().ToList();
        if (levelIds.Count == 0) return;
        var ranks = await _unitOfWork.Repository<StaffLevel>()
            .GetQueryable(l => levelIds.Contains(l.Id) && l.TenantId == tenantId)
            .Select(l => new { l.Id, l.Rank, l.Name })
            .ToListAsync(cancellationToken);
        var missing = levelIds.FirstOrDefault(id => ranks.All(r => r.Id != id));
        if (missing != Guid.Empty)
            throw new ArgumentException($"Staff level '{missing}' not found.");

        var from = ranks.FirstOrDefault(r => r.Id == entity.AppliesToLevelFromId);
        var upTo = ranks.FirstOrDefault(r => r.Id == entity.AppliesToLevelToId);
        if (from is not null && upTo is not null && from.Rank > upTo.Rank)
            throw new InvalidOperationException(
                $"The staff-level band runs backwards: from {from.Name} (rank {from.Rank}) to {upTo.Name} (rank {upTo.Rank}). " +
                "Put the lower rank first.");
    }

    /// <summary>The next version of a policy name in this organisation — deleted drafts included, so none is reused (T-50).</summary>
    private async Task<int> NextVersionAsync(Guid tenantId, string policyName, CancellationToken cancellationToken)
    {
        var highest = await _policyRepository
            .GetQueryableIncludingDeleted(p => p.TenantId == tenantId && p.PolicyName == policyName)
            .Select(p => (int?)p.VersionNumber)
            .MaxAsync(cancellationToken);
        return (highest ?? 0) + 1;
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

    /// <summary>The policies covering a level, a unit (or any unit above it — lane 4, O-5) and a date, most specific first.</summary>
    public async Task<IEnumerable<StaffTravelPolicySummaryDto>> GetApplicablePoliciesAsync(Guid? staffLevelId, Guid? organizationUnitId, DateOnly onDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var chain = organizationUnitId is Guid unitId
            ? await _audience.UnitAncestryAsync(tenantId, unitId, cancellationToken)
            : Array.Empty<Guid>();
        var applicable = (await _policyRepository.GetApplicablePoliciesAsync(staffLevelId, chain, onDate))
            .Where(p => p.TenantId == tenantId)
            .ToList();
        if (applicable.Count == 0) return Array.Empty<StaffTravelPolicySummaryDto>();

        // The approver's name and the rule count, read narrowly — the resolution query carries neither (see
        // GetApplicablePoliciesAsync's remarks: the includes cost a ~600 MB memory grant).
        var ids = applicable.Select(p => p.Id).ToList();
        var extras = await _policyRepository.GetQueryable()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                RuleCount = p.Rules.Count(r => !r.IsDeleted),
                First = p.ApprovedBy != null ? p.ApprovedBy.FirstName : null,
                Middle = p.ApprovedBy != null ? p.ApprovedBy.MiddleName : null,
                Last = p.ApprovedBy != null ? p.ApprovedBy.LastName : null,
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return applicable.Select(p =>
        {
            var dto = p.ToSummaryDto();
            if (extras.TryGetValue(p.Id, out var x))
            {
                dto.RuleCount = x.RuleCount;
                dto.ApprovedByName = x.First is null ? null
                    : string.IsNullOrEmpty(x.Middle) ? $"{x.First} {x.Last}" : $"{x.First} {x.Middle} {x.Last}";
            }
            return dto;
        }).ToList();
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
        entity.CurrencyCode = await ResolvePolicyCurrencyAsync(createDto.CurrencyCode, null, cancellationToken);
        await RequirePolicyShapeAsync(entity, cancellationToken);

        // T-50 (lane 4): the version is the next for the name — it was the payload's, so two drafts could both be
        // version 1 (409 against batch 1's index) or skip numbers at will.
        entity.VersionNumber = await NextVersionAsync(tenantId, entity.PolicyName, cancellationToken);

        // A new policy never arrives current — approval is what puts one in force (ApprovePolicyAsync).
        // Accepting the payload's word for it let two policies covering the same scope both claim to
        // be in force, and the guard then picked between them by ordering alone.
        entity.IsCurrentVersion = false;
        entity.ApprovedById = null;
        entity.ApprovedAt = null;

        await _policyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Travel policy drafted: {PolicyName} v{Version}", entity.PolicyName, entity.VersionNumber);

        // Re-read before mapping. The DTO resolves three scope names off navigations the freshly
        // added entity has never loaded, so the row came back naming no staff-level band and no
        // organisation unit — blank on the screen that had just set them, correct after a refetch.
        // Update, approve and withdraw all re-read already; create was the one that did not.
        var refreshed = await _policyRepository.GetWithRulesAsync(entity.Id);
        return (refreshed ?? entity).ToDto();
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

        // C3 (lane 4): the rule that caps everyone's travel is not signed by the officer who wrote it. Anyone who
        // drafted or last changed the draft is its author. A 403 with the sentence, as the money chain's D-2.
        var caller = _currentUserProvider.UserId.ToString();
        if (string.Equals(entity.CreatedBy, caller, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.UpdatedBy, caller, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                $"You drafted or last changed {entity.PolicyName} v{entity.VersionNumber}, so another travel administrator " +
                "must approve it — the officer who writes a spending rule does not also sign it.");

        // A draft written before lane 4 was never held to the policy's shape — approval is where it is.
        await RequirePolicyShapeAsync(entity, cancellationToken);
        if (entity.EffectiveTo is DateOnly end && end < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new InvalidOperationException(
                $"{entity.PolicyName} v{entity.VersionNumber} ended on {end:d MMM yyyy}; there is nothing left to approve it for.");
        entity.CurrencyCode ??= await ResolvePolicyCurrencyAsync(null, null, cancellationToken);

        entity.ApprovedById = approverEmployeeId;
        entity.ApprovedAt = DateTime.UtcNow;
        var superseded = await SupersedeSiblingsAsync(entity, cancellationToken);
        entity.IsCurrentVersion = true;
        entity.UpdatedAt = DateTime.UtcNow;

        // Tracked — `UpdateAsync` on a policy read with its rules and approver marks them modified too.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Travel policy approved: {PolicyName} v{Version}, in force from {From}; {Superseded}",
            entity.PolicyName, entity.VersionNumber, entity.EffectiveFrom, string.Join("; ", superseded));

        var refreshed = await _policyRepository.GetWithRulesAsync(entity.Id);
        return (refreshed ?? entity).ToDto();
    }

    /// <summary>
    /// Makes room for an approved version among the other versions in force for the same scope, by date (lane 4, O-4).
    /// </summary>
    /// <remarks>
    /// <para>Scope is the pair the guard resolves on — the organisation unit and the staff-level band. Two
    /// policies covering different units may both be in force; two covering the same one may not cover the same
    /// day, because the guard would then pick between them by ordering alone.</para>
    ///
    /// <para><b>By date, not at once.</b> Approving stood every sibling down the moment it was signed, and resolution
    /// needs a version in force — so a version approved today to start next year left every trip before then with
    /// no policy and no cap (O-4). Now a sibling that starts before the new version keeps going until the day
    /// before it starts; one that starts on or after it (and overlaps it) is replaced; one whose dates do not
    /// meet it is left alone. The guard picks the version in force on the trip's own departure date.</para>
    /// </remarks>
    private async Task<List<string>> SupersedeSiblingsAsync(
        StaffTravelPolicy entity, CancellationToken cancellationToken)
    {
        var siblings = (await _policyRepository.GetCurrentVersionsAsync())
            .Where(p => p.TenantId == entity.TenantId
                     && p.Id != entity.Id
                     && p.AppliesToOrganizationUnitId == entity.AppliesToOrganizationUnitId
                     && p.AppliesToLevelFromId == entity.AppliesToLevelFromId
                     && p.AppliesToLevelToId == entity.AppliesToLevelToId)
            .ToList();

        var outcome = new List<string>();
        foreach (var sibling in siblings)
        {
            var endsBefore = sibling.EffectiveTo is DateOnly sEnd && sEnd < entity.EffectiveFrom;
            var startsAfter = entity.EffectiveTo is DateOnly nEnd && nEnd < sibling.EffectiveFrom;
            if (endsBefore || startsAfter)
            {
                outcome.Add($"v{sibling.VersionNumber} untouched (no common day)");
                continue;
            }

            if (sibling.EffectiveFrom < entity.EffectiveFrom)
            {
                sibling.EffectiveTo = entity.EffectiveFrom.AddDays(-1);
                outcome.Add($"v{sibling.VersionNumber} in force until {sibling.EffectiveTo:yyyy-MM-dd}");
            }
            else
            {
                sibling.IsCurrentVersion = false;
                outcome.Add($"v{sibling.VersionNumber} stood down");
            }
            sibling.UpdatedAt = DateTime.UtcNow;
            // Tracked: saved with the approval. No `UpdateAsync` — the sibling was read with its rules and approver.
        }
        return outcome;
    }

    public async Task<StaffTravelPolicyDto> UpdatePolicyAsync(UpdateStaffTravelPolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPolicyAsync(updateDto.Id);

        // Editing an approved policy would change what everyone may spend without anyone approving
        // the change. Raise a new version instead — that is what versions are for.
        if (entity.ApprovedById is not null)
            throw new InvalidOperationException(
                "An approved policy cannot be edited. Raise a new version and have it approved.");

        var wasCurrent = entity.IsCurrentVersion;
        var previousName = entity.PolicyName;
        var previousCurrency = entity.CurrencyCode;
        entity.UpdateEntity(updateDto, updatedByUserId);
        entity.IsCurrentVersion = wasCurrent;   // not the payload's to change; approval sets it
        entity.CurrencyCode = await ResolvePolicyCurrencyAsync(updateDto.CurrencyCode, previousCurrency, cancellationToken);
        await RequirePolicyShapeAsync(entity, cancellationToken);
        // A draft renamed is the next version of its new name (T-50).
        if (!string.Equals(previousName, entity.PolicyName, StringComparison.OrdinalIgnoreCase))
            entity.VersionNumber = await NextVersionAsync(entity.TenantId, entity.PolicyName, cancellationToken);

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

    /// <summary>Removes a draft policy.</summary>
    /// <remarks>
    /// <b>An approved policy cannot be deleted.</b> <see cref="UpdatePolicyAsync"/> refuses to edit
    /// one because that would change what everyone may spend without anyone approving the change —
    /// and deleting it does exactly that, more completely, while also erasing the record of a rule
    /// that really did govern spending for a period. The guard existed on the sibling and not on
    /// this one. <see cref="WithdrawPolicyAsync"/> is the verb for standing a policy down; it keeps
    /// the approval as the fact about the past that it is.
    /// </remarks>
    public async Task<bool> DeletePolicyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPolicyAsync(id);

        if (entity.ApprovedById is not null)
            throw new InvalidOperationException(
                "An approved policy cannot be deleted. Withdraw it instead — that stops it capping " +
                "bookings while keeping the record that it once did.");

        await _policyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Policy rules ------------------------------------------------------

    /// <summary>Adds a rule to a policy, or revives one whose code was used and removed.</summary>
    /// <remarks>
    /// ⚠ <b>Re-using a rule code REVIVES the removed row; it does not insert a second one.</b>
    /// <c>IX_StaffTravelPolicyRules_TenantId_PolicyId_RuleCode</c> is unique with no
    /// <c>IsDeleted</c> filter while <c>DeleteRuleAsync</c> is a soft delete, so a removed rule
    /// went on occupying its code and adding it back hit the index and 500'd naming nothing —
    /// <b>a rule code could be used once per policy, ever</b>. Unreachable until a screen could
    /// delete a rule, which is the recurring shape: giving a dormant path teeth turns its
    /// neighbours into defects. Seventh face of this defect across HR, and fixed the way the
    /// others were — reviving keeps the row's history instead of pretending this is the first time.
    /// </remarks>
    public async Task<StaffTravelPolicyRuleDto> AddRuleAsync(CreateStaffTravelPolicyRuleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPolicyAsync(createDto.PolicyId);

        // IgnoreQueryFilters drops the tenant filter along with the soft-delete one, so the tenant
        // is re-applied by hand: reviving another tenant's row would be worse than the 500.
        var existing = await _ruleRepository
            .GetQueryableIncludingDeleted(r => r.TenantId == tenantId
                                            && r.PolicyId == createDto.PolicyId
                                            && r.RuleCode == createDto.RuleCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null)
        {
            if (!existing.IsDeleted)
                throw new InvalidOperationException(
                    $"This policy already has a rule with the code '{createDto.RuleCode}'.");

            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedBy = null;
            existing.UpdateEntity(new UpdateStaffTravelPolicyRuleDto
            {
                Id                        = existing.Id,
                RuleCode                  = createDto.RuleCode,
                RuleName                  = createDto.RuleName,
                RuleType                  = createDto.RuleType,
                ExpenseCategory           = createDto.ExpenseCategory,
                TravelType                = createDto.TravelType,
                LimitValue                = createDto.LimitValue,
                LimitUnit                 = createDto.LimitUnit,
                ExceptionAllowed          = createDto.ExceptionAllowed,
                ExceptionRequiresApproval = createDto.ExceptionRequiresApproval,
                ViolationAction           = createDto.ViolationAction,
                IsActive                  = createDto.IsActive,
            }, createdByUserId);

            await _ruleRepository.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return existing.ToDto();
        }

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

    /// <summary>Raises a request for permission to breach one of a policy's rules.</summary>
    /// <remarks>
    /// ⚠ <b>Both parents are real records, and neither was checked.</b> The travel request id and
    /// the policy rule id went straight from the request body onto the row, so a caller could
    /// attach an exception to another tenant's trip — the foreign key accepts it, because the id
    /// is perfectly valid, it just is not theirs — and it would then appear on this tenant's
    /// pending queue. The sibling <c>CreateAlertNotificationAsync</c> checks both of its parents
    /// and says why; this one did not follow it.
    /// </remarks>
    public async Task<StaffTravelPolicyExceptionDto> CreateExceptionAsync(CreateStaffTravelPolicyExceptionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var request = await _requestRepository.GetByIdAsync(createDto.StaffTravelRequestId);
        if (request == null || request.TenantId != tenantId)
            throw new ArgumentException(
                $"Travel request with ID '{createDto.StaffTravelRequestId}' not found.");

        var rule = await _ruleRepository.GetByIdAsync(createDto.PolicyRuleId);
        if (rule == null || rule.TenantId != tenantId)
            throw new ArgumentException(
                $"Policy rule with ID '{createDto.PolicyRuleId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _exceptionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The panel binds policyRuleName; the freshly added entity has no PolicyRule loaded, so
        // mapping it here returned the row with a blank rule name.
        entity.PolicyRule = rule;
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
        // Lane 4, C4: approve or reject — "Pending" or "Expired" are not decisions.
        if (decideDto.Status is not (TravelPolicyExceptionStatus.Approved or TravelPolicyExceptionStatus.Rejected))
            throw new InvalidOperationException("Approve the exception or reject it.");
        // ...and not by whoever raised it (its CreatedBy, the platform user) — the two-person rule of D-2 and D-8.
        if (string.Equals(entity.CreatedBy, _currentUserProvider.UserId.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                "You raised this policy exception, so another travel administrator decides it.");

        entity.Status = decideDto.Status;
        entity.ApprovedById = deciderEmployeeId;   // the caller, not a payload value
        entity.DecidedAt = DateTime.UtcNow;         // the clock, not the payload (C4)
        entity.DecisionNotes = decideDto.DecisionNotes;
        // Audit field: the USER id, not the Employee FK stamped above.
        entity.UpdatedBy = _currentUserProvider.UserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _exceptionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

}

#endregion
