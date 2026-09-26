using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementSodGuardService : IProcurementSodGuardService
{
    private const string AuditAction = "SOD_BYPASS_BLOCKED";
    private const string AuditResource = "ProcurementSodGuard";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementPolicyService _policyService;
    private readonly IRoleService _roleService;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly ILogger<ProcurementSodGuardService> _logger;
    private readonly IProcurementSodPolicy? _sodPolicy;

    public ProcurementSodGuardService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementPolicyService policyService,
        IRoleService roleService,
        IProcurementControlEventService controlEvents,
        ILogger<ProcurementSodGuardService> logger,
        IProcurementSodPolicy? sodPolicy = null)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _policyService = policyService;
        _roleService = roleService;
        _controlEvents = controlEvents;
        _logger = logger;
        _sodPolicy = sodPolicy;
    }

    private IGenericRepository<ProcurementPolicySet> PolicySets => _unitOfWork.Repository<ProcurementPolicySet>();
    private IGenericRepository<ProcurementPolicySodRule> SodRules => _unitOfWork.Repository<ProcurementPolicySodRule>();
    private IGenericRepository<AuditLog> AuditLogs => _unitOfWork.Repository<AuditLog>();

    public async Task<ProcurementSodCoverageDto> GetCoverageAsync(
        DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var moment = EnsureUtc(atUtc);
        var selection = await ResolveEffectivePolicyAsync(moment, cancellationToken);
        if (selection.Policy is null)
            return EmptyCoverage(moment, selection.Status);

        var rules = await LoadResolvedRulesAsync(selection.Policy, moment, cancellationToken);
        var controls = ProcurementSodRequiredControlRegistry.Definitions
            .Select(definition => MapCoverage(definition, rules))
            .ToList();
        return new ProcurementSodCoverageDto
        {
            EvaluatedAtUtc = moment,
            CurrentActorUserId = _currentUser.UserId,
            CurrentActorRoles = NormalizeRoles(_currentUser.Roles),
            Status = controls.All(item => item.IsConfigured && item.IsEffective && item.IsHardStop)
                ? "Complete"
                : "Incomplete",
            IsComplete = controls.All(item => item.IsConfigured && item.IsEffective && item.IsHardStop),
            PolicySetId = selection.Policy.Id,
            PolicyCode = selection.Policy.Code,
            PolicyName = selection.Policy.Name,
            PolicyVersion = selection.Policy.Version,
            Controls = controls
        };
    }

    public async Task<ProcurementSodProvisionResultDto> ApplyRequiredControlsAsync(
        Guid policySetId,
        ApplyRequiredProcurementSodControlsRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ProcurementSodRequestValidationException("REASON_REQUIRED", "A reason is required when applying the six TDC SOD controls.");

        var policy = await _policyService.GetPolicySetAsync(policySetId, cancellationToken);
        if (policy.LifecycleStatus != ProcurementPolicyLifecycleStatus.Draft)
            throw new ProcurementPolicyConflictException("Required SOD controls can be applied only to a Draft policy.");

        var existing = policy.Rules
            .Where(item => item.Kind == ProcurementPolicyRuleKind.SegregationOfDuties)
            .Select(item => item.RuleCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingDefinitions = ProcurementSodRequiredControlRegistry.Definitions
            .Where(definition => !existing.Contains(definition.Code))
            .ToList();
        var activeTenantRoleNames = (await _roleService.GetRolesForTenantAsync(
                _currentUser.TenantId, cancellationToken))
            .Where(role => !string.IsNullOrWhiteSpace(role.Name))
            .Select(role => role.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingRoleNames = missingDefinitions
            .SelectMany(definition => new[] { definition.InitiatorRole, definition.ConflictingRole })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(roleName => !activeTenantRoleNames.Contains(roleName))
            .OrderBy(roleName => roleName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missingRoleNames.Count > 0)
            throw new ProcurementSodRequestValidationException(
                "SOD_TENANT_ROLES_REQUIRED",
                "Before applying the optional SOD templates, assign at least one active user in the current tenant to each required role: " +
                string.Join(", ", missingRoleNames) + ". No controls were created.");

        var created = new List<string>();
        var retained = new List<string>();
        foreach (var definition in ProcurementSodRequiredControlRegistry.Definitions)
        {
            if (existing.Contains(definition.Code))
            {
                retained.Add(definition.Code);
                continue;
            }

            await _policyService.SaveRuleAsync(policySetId, null, new SaveProcurementPolicyRuleRequest
            {
                Kind = ProcurementPolicyRuleKind.SegregationOfDuties,
                Reason = request.Reason.Trim(),
                SegregationOfDuties = new SaveProcurementPolicySodRuleValue
                {
                    RuleCode = definition.Code,
                    Name = definition.Name,
                    InitiatorRole = definition.InitiatorRole,
                    ConflictingRole = definition.ConflictingRole,
                    EntityType = definition.EntityType,
                    Action = definition.Action,
                    Enforcement = ProcurementSodEnforcement.HardStop,
                    Explanation = definition.Explanation,
                    Priority = 100,
                    IsEnabled = true,
                    EffectiveFrom = policy.EffectiveFrom,
                    EffectiveTo = policy.EffectiveTo,
                    OverrideAction = ProcurementPolicyOverrideAction.Add,
                    SourceDecisionKey = definition.SourceDecisionKey
                }
            }, correlationId, cancellationToken);
            created.Add(definition.Code);
        }

        return new ProcurementSodProvisionResultDto
        {
            PolicySetId = policySetId,
            CreatedCount = created.Count,
            ExistingCount = retained.Count,
            CreatedControlCodes = created,
            ExistingControlCodes = retained
        };
    }

    public Task<ProcurementSodGuardDecisionDto> CheckAsync(
        ProcurementSodGuardRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EvaluateAsync(request, correlationId, auditDeniedAttempt: false, cancellationToken);

    public Task<ProcurementSodGuardDecisionDto> EnforceAsync(
        ProcurementSodGuardRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EvaluateAsync(request, correlationId, auditDeniedAttempt: true, cancellationToken);

    public async Task<IReadOnlyList<ProcurementSodBypassAuditDto>> GetBlockedAttemptsAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        var logs = await AuditLogs.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Action == AuditAction &&
                item.Resource == AuditResource)
            .AsNoTracking()
            .OrderByDescending(item => item.Timestamp)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

        return logs.Select(MapAudit).ToList();
    }

    private async Task<ProcurementSodGuardDecisionDto> EvaluateAsync(
        ProcurementSodGuardRequest request,
        string correlationId,
        bool auditDeniedAttempt,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        ValidateRequest(request);
        var definition = ProcurementSodRequiredControlRegistry.Definitions.Single(item =>
            string.Equals(item.Code, request.ControlCode.Trim(), StringComparison.OrdinalIgnoreCase));
        var now = DateTime.UtcNow;
        var coverage = await GetCoverageAsync(now, cancellationToken);
        var control = coverage.Controls.Single(item => item.Code == definition.Code);
        var isProhibitedActor =
            request.ProhibitedActorUserIds.Contains(_currentUser.UserId);
        var hasQualifyingIndependentActor =
            request.IndependentActorUserIds.Any(item =>
                item != Guid.Empty &&
                item != _currentUser.UserId &&
                !request.ProhibitedActorUserIds.Contains(item));
        var isIdentityConflict =
            isProhibitedActor &&
            (!request.RequireSoleActorConflict ||
             !hasQualifyingIndependentActor);

        ProcurementSodGuardDecisionDto decision;
        var enforceSeparation = _sodPolicy is null || await _sodPolicy.IsRequiredForSourceAsync(
            _currentUser.TenantId, request.SourceType,
            Guid.TryParse(request.SourceReference, out var sourceId) ? sourceId : null, cancellationToken);
        if (!enforceSeparation)
        {
            decision = Decision(true, "SOD_DISABLED",
                "Actor separation is disabled for this tenant's procurement transaction chain. Permissions and workflow approvals still apply.",
                definition, request, coverage, control, correlationId, now);
        }
        else if (isIdentityConflict)
        {
            decision = Decision(false, "SOD_CONFLICT", definition.Explanation,
                definition, request, coverage, control, correlationId, now);
        }
        else if (isProhibitedActor &&
                 request.RequireSoleActorConflict &&
                 hasQualifyingIndependentActor)
        {
            decision = Decision(true, "SOD_ALLOWED",
                $"The current actor participated as {definition.InitiatorRole}, but the exact approval lineage contains a distinct non-conflicting actor.",
                definition, request, coverage, control, correlationId, now);
        }
        else
        {
            decision = Decision(true, "SOD_ALLOWED",
                control.IsConfigured && control.IsEffective && control.IsHardStop
                    ? $"The current actor is independent of the recorded {definition.InitiatorRole} participant(s)."
                    : $"The shared maker-checker baseline confirmed that the current actor is independent of the recorded {definition.InitiatorRole} participant(s). Optional policy-specific SOD metadata is not configured.",
                definition, request, coverage, control, correlationId, now);
        }

        if (auditDeniedAttempt)
        {
            if (!decision.Allowed)
            {
                await AuditBlockedAttemptAsync(decision, cancellationToken);
                decision = decision.WithAudit();
            }
            await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
            {
                EventKey = ProcurementControlEventKey.Create("sod", _currentUser.TenantId, _currentUser.UserId,
                    correlationId, decision.ControlCode, decision.SourceType, decision.SourceReference),
                EventType = "SegregationOfDutiesDecision",
                Action = "Enforce",
                Result = decision.Allowed ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Denied,
                RuleCode = decision.RuleCode ?? decision.ControlCode,
                RuleId = decision.RuleId,
                RuleVersion = decision.PolicyVersion?.ToString(),
                DecisionKeys = new() { "DEC-004" },
                SourceType = decision.SourceType,
                SourceReference = decision.SourceReference,
                Reason = decision.Message,
                InputValues = new
                {
                    request.ProhibitedActorUserIds,
                    request.IndependentActorUserIds,
                    request.RequireSoleActorConflict,
                    hasQualifyingIndependentActor,
                    decision.ActorUserId,
                    decision.ActorRoles
                },
                ResultValues = new { decision.Allowed, decision.Code, decision.PolicySetId, decision.PolicyCode, decision.WasAudited },
                CorrelationId = decision.CorrelationId,
                OccurredAtUtc = decision.EvaluatedAtUtc
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Procurement SOD guard {Result} control {ControlCode} for {SourceType} {SourceReference}; correlation {CorrelationId}",
            decision.Allowed ? "allowed" : "blocked", decision.ControlCode, decision.SourceType,
            decision.SourceReference, correlationId);
        return decision;
    }

    private async Task AuditBlockedAttemptAsync(ProcurementSodGuardDecisionDto decision, CancellationToken cancellationToken)
    {
        var payload = new AuditPayload(
            decision.CorrelationId, decision.ControlCode, decision.SourceType, decision.SourceReference,
            decision.Message, decision.RuleId, decision.RuleCode, decision.PolicySetId,
            decision.ActorRoles);
        await AuditLogs.AddAsync(new AuditLog
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            Username = Truncate(_currentUser.Username, 255),
            Action = AuditAction,
            Resource = AuditResource,
            ResourceId = Truncate(decision.SourceReference, 100),
            NewValues = JsonSerializer.Serialize(payload),
            IpAddress = "Unknown",
            Timestamp = decision.EvaluatedAtUtc,
            CreatedBy = _currentUser.FullName,
            CreatedById = _currentUser.UserId
        });
    }

    private async Task<PolicySelection> ResolveEffectivePolicyAsync(DateTime moment, CancellationToken cancellationToken)
    {
        var effective = PolicySets.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published &&
                item.EffectiveFrom <= moment &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking();
        var defaults = await effective.Where(item => item.IsDefault).ToListAsync(cancellationToken);
        if (defaults.Count == 1) return new PolicySelection(defaults[0], "Effective");
        if (defaults.Count > 1) return new PolicySelection(null, "AmbiguousPolicy");
        var all = await effective.ToListAsync(cancellationToken);
        return all.Count switch
        {
            0 => new PolicySelection(null, "NoEffectivePolicy"),
            1 => new PolicySelection(all[0], "Effective"),
            _ => new PolicySelection(null, "AmbiguousPolicy")
        };
    }

    private async Task<IReadOnlyList<ProcurementPolicySodRule>> LoadResolvedRulesAsync(
        ProcurementPolicySet selected,
        DateTime moment,
        CancellationToken cancellationToken)
    {
        var selectedRules = await LoadRulesAsync(selected.Id, moment, cancellationToken);
        if (selected.ScopeType != ProcurementPolicyScopeType.TenantOverride)
            return selectedRules.Where(item => item.OverrideAction != ProcurementPolicyOverrideAction.Disable).ToList();
        if (!selected.BasePolicySetId.HasValue) return Array.Empty<ProcurementPolicySodRule>();
        var basePolicy = await PolicySets.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.Id == selected.BasePolicySetId.Value &&
                item.LifecycleStatus != ProcurementPolicyLifecycleStatus.Draft)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (basePolicy is null) return Array.Empty<ProcurementPolicySodRule>();

        var resolved = (await LoadRulesAsync(basePolicy.Id, moment, cancellationToken))
            .ToDictionary(item => item.Id);
        foreach (var rule in selectedRules.OrderBy(item => item.OverrideAction).ThenBy(item => item.Id))
        {
            if (rule.OverrideAction is ProcurementPolicyOverrideAction.Replace or ProcurementPolicyOverrideAction.Disable)
            {
                if (rule.SourceRuleId.HasValue) resolved.Remove(rule.SourceRuleId.Value);
                if (rule.OverrideAction == ProcurementPolicyOverrideAction.Disable) continue;
            }
            resolved[rule.Id] = rule;
        }
        return resolved.Values.ToList();
    }

    private Task<List<ProcurementPolicySodRule>> LoadRulesAsync(Guid policySetId, DateTime moment, CancellationToken cancellationToken) =>
        SodRules.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.PolicySetId == policySetId && item.IsEnabled &&
                item.EffectiveFrom <= moment && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking().ToListAsync(cancellationToken);

    private ProcurementSodCoverageDto EmptyCoverage(DateTime moment, string status)
    {
        var issue = status == "AmbiguousPolicy"
            ? "Multiple Published policies are effective and the guard cannot select a unique default."
            : "No Published procurement policy is effective for this tenant and date.";
        return new ProcurementSodCoverageDto
        {
            EvaluatedAtUtc = moment,
            CurrentActorUserId = _currentUser.UserId,
            CurrentActorRoles = NormalizeRoles(_currentUser.Roles),
            Status = status,
            IsComplete = false,
            Controls = ProcurementSodRequiredControlRegistry.Definitions.Select(item => new ProcurementSodRequiredControlDto
            {
                Code = item.Code,
                Name = item.Name,
                InitiatorRole = item.InitiatorRole,
                ConflictingRole = item.ConflictingRole,
                EntityType = item.EntityType,
                Action = item.Action,
                Explanation = item.Explanation,
                SourceRequirement = item.SourceRequirement,
                SourceDecisionKey = item.SourceDecisionKey,
                ConfigurationIssue = issue
            }).ToList()
        };
    }

    private static ProcurementSodRequiredControlDto MapCoverage(
        ProcurementSodRequiredControlDefinition definition,
        IReadOnlyList<ProcurementPolicySodRule> rules)
    {
        var matches = rules.Where(item => string.Equals(item.RuleCode, definition.Code, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Priority).ToList();
        var rule = matches.FirstOrDefault();
        var shapeMatches = rule is not null && matches.Count == 1 &&
                           ProcurementSodRequiredControlRegistry.MatchesRequiredShape(definition,
                               rule.InitiatorRole, rule.ConflictingRole, rule.EntityType, rule.Action, rule.Enforcement);
        var issue = rule is null
            ? "The required control is missing from the effective policy."
            : matches.Count > 1
                ? "The required control code resolves to multiple effective rules."
                : shapeMatches
                    ? null
                    : "The rule does not match the required roles, entity, action, and HardStop enforcement.";
        return new ProcurementSodRequiredControlDto
        {
            Code = definition.Code,
            Name = definition.Name,
            InitiatorRole = definition.InitiatorRole,
            ConflictingRole = definition.ConflictingRole,
            EntityType = definition.EntityType,
            Action = definition.Action,
            Explanation = definition.Explanation,
            SourceRequirement = definition.SourceRequirement,
            SourceDecisionKey = definition.SourceDecisionKey,
            IsConfigured = rule is not null,
            IsEffective = rule is not null,
            IsHardStop = shapeMatches,
            RuleId = rule?.Id,
            RuleCode = rule?.RuleCode,
            Enforcement = rule?.Enforcement,
            SourceRuleId = rule?.SourceRuleId,
            OverrideAction = rule?.OverrideAction,
            ConfigurationIssue = issue
        };
    }

    private ProcurementSodGuardDecisionDto Decision(
        bool allowed,
        string code,
        string message,
        ProcurementSodRequiredControlDefinition definition,
        ProcurementSodGuardRequest request,
        ProcurementSodCoverageDto coverage,
        ProcurementSodRequiredControlDto control,
        string correlationId,
        DateTime now) => new()
    {
        CorrelationId = correlationId,
        EvaluatedAtUtc = now,
        Allowed = allowed,
        IsHardStop = !allowed,
        Code = code,
        Message = message,
        ActorUserId = _currentUser.UserId,
        ActorRoles = NormalizeRoles(_currentUser.Roles),
        ControlCode = definition.Code,
        SourceType = request.SourceType.Trim(),
        SourceReference = request.SourceReference.Trim(),
        PolicySetId = coverage.PolicySetId,
        PolicyCode = coverage.PolicyCode,
        PolicyVersion = coverage.PolicyVersion,
        RuleId = control.RuleId,
        RuleCode = control.RuleCode,
        SourceDecisionKey = control.SourceDecisionKey,
        SourceRequirement = control.SourceRequirement
    };

    private static ProcurementSodBypassAuditDto MapAudit(AuditLog log)
    {
        AuditPayload? payload = null;
        try { payload = string.IsNullOrWhiteSpace(log.NewValues) ? null : JsonSerializer.Deserialize<AuditPayload>(log.NewValues); }
        catch (JsonException) { }
        return new ProcurementSodBypassAuditDto
        {
            Id = log.Id,
            Timestamp = log.Timestamp,
            ActorUserId = log.UserId,
            ActorName = log.Username,
            ControlCode = payload?.ControlCode ?? string.Empty,
            SourceType = payload?.SourceType ?? string.Empty,
            SourceReference = payload?.SourceReference ?? log.ResourceId ?? string.Empty,
            CorrelationId = payload?.CorrelationId ?? string.Empty,
            Message = payload?.Message ?? string.Empty,
            RuleId = payload?.RuleId,
            RuleCode = payload?.RuleCode
        };
    }

    private static void ValidateRequest(ProcurementSodGuardRequest request)
    {
        if (!ProcurementSodRequiredControlRegistry.TryGet(request.ControlCode, out _))
            throw new ProcurementSodRequestValidationException("CONTROL_UNKNOWN", "ControlCode must identify one of the six required TDC SOD controls.");
        if (string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.SourceReference))
            throw new ProcurementSodRequestValidationException("SOURCE_REQUIRED", "SourceType and SourceReference are required.");
        if (request.ProhibitedActorUserIds.Count == 0)
            throw new ProcurementSodRequestValidationException("PARTICIPANT_REQUIRED", "At least one prohibited prior participant user ID is required.");
        if (request.ProhibitedActorUserIds.Any(item => item == Guid.Empty))
            throw new ProcurementSodRequestValidationException("PARTICIPANT_INVALID", "Prohibited participant user IDs cannot be empty GUIDs.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    }

    private void EnsureAdministrator()
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.HasRole(ErpSystem.Shared.Constants.Roles.SuperAdmin) &&
            !_currentUser.Roles.Any(role =>
                ProcurementAccessControlRegistry.RoleGrantsPermission(
                    role, "procurement.access.manage")))
            throw new ProcurementPolicyAuthorizationException(
                "SuperAdmin or the TDC ICT Administrator role is required to administer procurement SOD controls.");
    }

    private void EnsureEditor() => EnsureAdministrator();

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static IReadOnlyList<string> NormalizeRoles(IEnumerable<string> roles) => roles
        .Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();

    private static string Truncate(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record PolicySelection(ProcurementPolicySet? Policy, string Status);
    private sealed record AuditPayload(
        string CorrelationId,
        string ControlCode,
        string SourceType,
        string SourceReference,
        string Message,
        Guid? RuleId,
        string? RuleCode,
        Guid? PolicySetId,
        IReadOnlyList<string> ActorRoles);
}

public sealed class ProcurementSodRequestValidationException : Exception
{
    public ProcurementSodRequestValidationException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}

internal static class ProcurementSodDecisionExtensions
{
    public static ProcurementSodGuardDecisionDto WithAudit(this ProcurementSodGuardDecisionDto source) => new()
    {
        DecisionId = source.DecisionId,
        CorrelationId = source.CorrelationId,
        EvaluatedAtUtc = source.EvaluatedAtUtc,
        Allowed = source.Allowed,
        IsHardStop = source.IsHardStop,
        WasAudited = true,
        Code = source.Code,
        Message = source.Message,
        ActorUserId = source.ActorUserId,
        ActorRoles = source.ActorRoles,
        ControlCode = source.ControlCode,
        SourceType = source.SourceType,
        SourceReference = source.SourceReference,
        PolicySetId = source.PolicySetId,
        PolicyCode = source.PolicyCode,
        PolicyVersion = source.PolicyVersion,
        RuleId = source.RuleId,
        RuleCode = source.RuleCode,
        SourceDecisionKey = source.SourceDecisionKey,
        SourceRequirement = source.SourceRequirement
    };
}
