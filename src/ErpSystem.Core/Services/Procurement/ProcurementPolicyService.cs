using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementPolicyService : IProcurementPolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IRoleService _roleService;
    private readonly ILogger<ProcurementPolicyService> _logger;

    public ProcurementPolicyService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IRoleService roleService,
        ILogger<ProcurementPolicyService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _roleService = roleService;
        _logger = logger;
    }

    private IGenericRepository<ProcurementPolicySet> PolicySets => _unitOfWork.Repository<ProcurementPolicySet>();
    private IGenericRepository<ProcurementPolicyCategoryRule> Categories => _unitOfWork.Repository<ProcurementPolicyCategoryRule>();
    private IGenericRepository<ProcurementPolicyMethodRule> Methods => _unitOfWork.Repository<ProcurementPolicyMethodRule>();
    private IGenericRepository<ProcurementPolicyThresholdRule> Thresholds => _unitOfWork.Repository<ProcurementPolicyThresholdRule>();
    private IGenericRepository<ProcurementPolicyAuthorityRule> Authorities => _unitOfWork.Repository<ProcurementPolicyAuthorityRule>();
    private IGenericRepository<ProcurementPolicyEvidenceRule> Evidence => _unitOfWork.Repository<ProcurementPolicyEvidenceRule>();
    private IGenericRepository<ProcurementPolicyExceptionRule> Exceptions => _unitOfWork.Repository<ProcurementPolicyExceptionRule>();
    private IGenericRepository<ProcurementPolicySodRule> SodRules => _unitOfWork.Repository<ProcurementPolicySodRule>();
    private IGenericRepository<ProcurementPolicyRevision> Revisions => _unitOfWork.Repository<ProcurementPolicyRevision>();
    private IGenericRepository<ProcurementConfigurationProfile> ConfigurationProfiles => _unitOfWork.Repository<ProcurementConfigurationProfile>();
    private IGenericRepository<ProcurementConfigurationDecision> ConfigurationDecisions => _unitOfWork.Repository<ProcurementConfigurationDecision>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions => _unitOfWork.Repository<WorkflowDefinition>();

    public async Task<ProcurementConfigurationPagedResult<ProcurementPolicySetSummaryDto>> GetPolicySetsAsync(
        ProcurementPolicySetListRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = PolicySets.GetQueryable(item => item.TenantId == _currentUser.TenantId);
        if (request.Status.HasValue) query = query.Where(item => item.LifecycleStatus == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.Code.Contains(search) || item.Name.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var sets = await query.AsNoTracking()
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.Version)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var summaries = new List<ProcurementPolicySetSummaryDto>();
        foreach (var policySet in sets)
            summaries.Add(await MapSummaryAsync(policySet, cancellationToken));

        return new ProcurementConfigurationPagedResult<ProcurementPolicySetSummaryDto>
        {
            Items = summaries,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<IReadOnlyList<ProcurementPolicyRoleOptionDto>> GetRoleOptionsAsync(
        Guid? workflowDefinitionId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var roles = (await _roleService.GetRolesForTenantAsync(_currentUser.TenantId, cancellationToken))
            .Where(role => !string.IsNullOrWhiteSpace(role.Name))
            .ToList();
        HashSet<string>? workflowRoles = null;
        if (workflowDefinitionId.HasValue)
            workflowRoles = await GetWorkflowRoleKeysAsync(workflowDefinitionId.Value, cancellationToken);

        return roles
            .Where(role => workflowRoles is null ||
                workflowRoles.Contains(role.Id.ToString()) ||
                workflowRoles.Contains(role.Name!))
            .OrderBy(role => role.Name)
            .Select(role => new ProcurementPolicyRoleOptionDto
            {
                Id = role.Id,
                Name = role.Name!,
                Description = role.Description,
                IsSystemRole = role.IsSystemRole,
                IsAssignedToSelectedWorkflow = workflowRoles is not null
            })
            .ToList();
    }

    public async Task<ProcurementPolicySetDto> GetPolicySetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var policySet = await FindPolicySetAsync(id, tracked: false, cancellationToken);
        return await MapPolicySetAsync(policySet, cancellationToken);
    }

    public async Task<ProcurementPolicySetDto?> GetEffectivePolicySetAsync(
        string code,
        DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var normalizedCode = NormalizeCode(code);
        var moment = EnsureUtc(atUtc);
        var policySet = await PolicySets.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Code == normalizedCode &&
                item.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published &&
                item.EffectiveFrom <= moment &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking()
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return policySet is null ? null : await MapPolicySetAsync(policySet, cancellationToken);
    }

    public async Task<ProcurementPolicySetDto> CreatePolicySetAsync(
        CreateProcurementPolicySetRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        ValidateDates(request.EffectiveFrom, request.EffectiveTo, "policy");
        var code = NormalizeCode(request.Code);
        if (await PolicySets.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.Code == code)
                .AnyAsync(cancellationToken))
            throw new ProcurementPolicyConflictException($"A procurement policy family with code '{code}' already exists. Clone its immutable version instead.");

        var source = await FindImmutableSourceConfigurationAsync(request.SourceConfigurationProfileId, cancellationToken);
        ProcurementPolicySet? basePolicy = null;
        if (request.ScopeType == ProcurementPolicyScopeType.TenantOverride)
        {
            if (!request.BasePolicySetId.HasValue)
                throw ValidationException("BASE_POLICY_REQUIRED", "A tenant override must reference an immutable base policy.");
            basePolicy = await FindPolicySetAsync(request.BasePolicySetId.Value, tracked: false, cancellationToken);
            if (basePolicy.LifecycleStatus == ProcurementPolicyLifecycleStatus.Draft)
                throw ValidationException("BASE_POLICY_DRAFT", "A tenant override cannot inherit from an editable Draft policy.");
        }
        else if (request.BasePolicySetId.HasValue)
        {
            throw ValidationException("BASE_POLICY_NOT_ALLOWED", "A tenant baseline cannot reference a base policy.");
        }

        var now = DateTime.UtcNow;
        var policySet = new ProcurementPolicySet
        {
            TenantId = _currentUser.TenantId,
            PolicyKey = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Description = NullIfWhiteSpace(request.Description),
            Version = 1,
            LifecycleStatus = ProcurementPolicyLifecycleStatus.Draft,
            ScopeType = request.ScopeType,
            SourceConfigurationProfileId = source.Id,
            BasePolicySetId = basePolicy?.Id,
            DefaultCurrencyCode = NormalizeCurrency(request.DefaultCurrencyCode),
            EffectiveFrom = EnsureUtc(request.EffectiveFrom),
            EffectiveTo = EnsureUtc(request.EffectiveTo),
            ChangeSummary = NullIfWhiteSpace(request.ChangeSummary),
            IsDefault = request.IsDefault,
            CreatedAt = now,
            CreatedBy = _currentUser.FullName,
            CreatedById = _currentUser.UserId
        };
        await PolicySets.AddAsync(policySet);
        var materialized = await MaterializeSourceRulesAsync(policySet, source, now, cancellationToken);
        await AddRevisionAsync(policySet.Id, null, null, "Create", "Succeeded", correlationId, request.ChangeSummary, null,
            new { policySet.Code, policySet.Version, policySet.ScopeType, SourceConfiguration = $"{source.ProfileCode}/v{source.Version}", MaterializedRules = materialized });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetPolicySetAsync(policySet.Id, cancellationToken);
    }

    public async Task<ProcurementPolicySetDto> UpdatePolicySetAsync(
        Guid id,
        UpdateProcurementPolicySetRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var policySet = await FindPolicySetAsync(id, tracked: true, cancellationToken);
        ProcurementPolicyLifecyclePolicy.EnsureEditable(policySet);
        EnsureRowVersion(policySet.RowVersion, request.RowVersion, "policy");
        ValidateDates(request.EffectiveFrom, request.EffectiveTo, "policy");
        var before = PolicySnapshot(policySet);
        policySet.Name = request.Name.Trim();
        policySet.Description = NullIfWhiteSpace(request.Description);
        policySet.DefaultCurrencyCode = NormalizeCurrency(request.DefaultCurrencyCode);
        policySet.EffectiveFrom = EnsureUtc(request.EffectiveFrom);
        policySet.EffectiveTo = EnsureUtc(request.EffectiveTo);
        policySet.ChangeSummary = NullIfWhiteSpace(request.ChangeSummary);
        policySet.IsDefault = request.IsDefault;
        Touch(policySet);
        await PolicySets.UpdateAsync(policySet);
        await AddRevisionAsync(policySet.Id, null, null, "UpdatePolicy", "Succeeded", correlationId, request.Reason, before, PolicySnapshot(policySet));
        await SaveWithConcurrencyAsync("policy", cancellationToken);
        return await GetPolicySetAsync(policySet.Id, cancellationToken);
    }

    public async Task<ProcurementPolicyRuleDto> SaveRuleAsync(
        Guid policySetId,
        Guid? ruleId,
        SaveProcurementPolicyRuleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var policySet = await FindPolicySetAsync(policySetId, tracked: true, cancellationToken);
        ProcurementPolicyLifecyclePolicy.EnsureEditable(policySet);
        var value = GetAndValidateRuleValue(request);
        EnsureRuleWithinPolicy(policySet, value);
        ValidateRuleValue(request.Kind, value);
        await EnsureRoleReferencesAsync(value, cancellationToken);
        await EnsureWorkflowReferenceAsync(value, cancellationToken);
        await EnsureRuleCodeUniqueAsync(policySet.Id, request.Kind, value.RuleCode, ruleId, cancellationToken);
        await EnsureOverrideReferenceAsync(policySet, request.Kind, value, cancellationToken);

        object? before = null;
        object entity;
        if (ruleId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(request.RowVersion))
                throw new ProcurementPolicyConflictException("A row version is required when editing an existing policy rule.");
            entity = await FindRuleEntityAsync(policySet.Id, request.Kind, ruleId.Value, tracked: true, cancellationToken);
            EnsureRowVersion(GetRuleRowVersion(entity), request.RowVersion, value.RuleCode);
            before = RuleSnapshot(entity, request.Kind);
            ApplyRuleValue(entity, request.Kind, value);
            TouchRule(entity);
            await UpdateRuleEntityAsync(entity, request.Kind);
        }
        else
        {
            entity = CreateRuleEntity(policySet, request.Kind, value);
            await AddRuleEntityAsync(entity, request.Kind);
        }

        Touch(policySet);
        await PolicySets.UpdateAsync(policySet);
        await AddRevisionAsync(policySet.Id, GetRuleId(entity), request.Kind, ruleId.HasValue ? "UpdateRule" : "CreateRule", "Succeeded",
            correlationId, request.Reason, before, RuleSnapshot(entity, request.Kind));
        await SaveWithConcurrencyAsync(value.RuleCode, cancellationToken);
        return MapRule(entity, request.Kind);
    }

    public async Task DeleteRuleAsync(
        Guid policySetId,
        ProcurementPolicyRuleKind kind,
        Guid ruleId,
        DeleteProcurementPolicyRuleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var policySet = await FindPolicySetAsync(policySetId, tracked: true, cancellationToken);
        ProcurementPolicyLifecyclePolicy.EnsureEditable(policySet);
        var entity = await FindRuleEntityAsync(policySet.Id, kind, ruleId, tracked: true, cancellationToken);
        EnsureRowVersion(GetRuleRowVersion(entity), request.RowVersion, "rule");
        var before = RuleSnapshot(entity, kind);
        SoftDeleteRule(entity);
        await UpdateRuleEntityAsync(entity, kind);
        Touch(policySet);
        await PolicySets.UpdateAsync(policySet);
        await AddRevisionAsync(policySet.Id, ruleId, kind, "DeleteRule", "Succeeded", correlationId, request.Reason, before, null);
        await SaveWithConcurrencyAsync("rule", cancellationToken);
    }

    public async Task<ProcurementPolicyValidationResultDto> ValidatePolicySetAsync(
        Guid id,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var policySet = await FindPolicySetAsync(id, tracked: false, cancellationToken);
        var validation = await BuildValidationAsync(policySet, cancellationToken);
        await AddRevisionAsync(policySet.Id, null, null, "Validate", validation.IsValid ? "Succeeded" : "Rejected", correlationId,
            validation.IsValid ? "Policy validation passed." : $"Policy validation returned {validation.Errors.Count} error(s).",
            null, new { validation.IsValid, ErrorCount = validation.Errors.Count, WarningCount = validation.Warnings.Count });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return validation;
    }

    public async Task<ProcurementPolicySetDto> PublishPolicySetAsync(
        Guid id,
        ProcurementPolicyLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var policySet = await FindPolicySetAsync(id, tracked: true, cancellationToken);
        if (!_currentUser.HasRole("SuperAdmin"))
        {
            await AddRevisionAsync(policySet.Id, null, null, "Publish", "Rejected", correlationId,
                "Only SuperAdmin may publish an executable procurement policy.", PolicySnapshot(policySet), null);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ProcurementPolicyAuthorizationException("Only SuperAdmin may publish executable procurement policies.");
        }

        ProcurementPolicyLifecyclePolicy.EnsureCanPublish(policySet);
        EnsureRowVersion(policySet.RowVersion, request.RowVersion, "policy");
        var validation = await BuildValidationAsync(policySet, cancellationToken);
        if (!validation.IsValid)
        {
            await AddRevisionAsync(policySet.Id, null, null, "Publish", "Rejected", correlationId,
                $"Publication blocked by {validation.Errors.Count} validation error(s).", PolicySnapshot(policySet), null);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ProcurementPolicyValidationException("The executable policy is not ready to publish.", validation);
        }

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var publishesInFuture = policySet.EffectiveFrom > now;
                var previous = await PolicySets.GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.PolicyKey == policySet.PolicyKey &&
                        item.Id != policySet.Id &&
                        item.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published)
                    .ToListAsync(cancellationToken);
                if (publishesInFuture && previous.Any(item => item.EffectiveFrom > now))
                    throw new ProcurementPolicyConflictException(
                        "A future replacement is already Published for this policy family. Retire it before scheduling another replacement.");
                foreach (var prior in previous)
                {
                    var priorBefore = PolicySnapshot(prior);
                    var isCurrentlyEffective = prior.EffectiveFrom <= now &&
                        (!prior.EffectiveTo.HasValue || prior.EffectiveTo.Value >= now);
                    if (publishesInFuture && isCurrentlyEffective)
                    {
                        if (!prior.EffectiveTo.HasValue || prior.EffectiveTo.Value >= policySet.EffectiveFrom)
                            prior.EffectiveTo = policySet.EffectiveFrom.AddTicks(-1);
                        Touch(prior);
                        await PolicySets.UpdateAsync(prior);
                        await AddRevisionAsync(prior.Id, null, null, "ScheduleSupersession", "Succeeded", correlationId,
                            $"Remains Published until version {policySet.Version} becomes effective at {policySet.EffectiveFrom:O}.",
                            priorBefore, PolicySnapshot(prior));
                        continue;
                    }

                    prior.LifecycleStatus = ProcurementPolicyLifecycleStatus.Retired;
                    prior.RetiredAt = now;
                    prior.RetiredById = _currentUser.UserId;
                    Touch(prior);
                    await PolicySets.UpdateAsync(prior);
                    await AddRevisionAsync(prior.Id, null, null, "RetireSuperseded", "Succeeded", correlationId,
                        $"Superseded by version {policySet.Version}.", priorBefore, PolicySnapshot(prior));
                }

                var before = PolicySnapshot(policySet);
                policySet.LifecycleStatus = ProcurementPolicyLifecycleStatus.Published;
                policySet.PublishedAt = now;
                policySet.PublishedById = _currentUser.UserId;
                policySet.RetiredAt = null;
                policySet.RetiredById = null;
                Touch(policySet);
                await PolicySets.UpdateAsync(policySet);
                await AddRevisionAsync(policySet.Id, null, null, "Publish", "Succeeded", correlationId, request.Reason, before, PolicySnapshot(policySet));
                await SaveWithConcurrencyAsync("policy", cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        });
        return await GetPolicySetAsync(policySet.Id, cancellationToken);
    }

    public async Task<ProcurementPolicySetDto> RetirePolicySetAsync(
        Guid id,
        ProcurementPolicyLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var policySet = await FindPolicySetAsync(id, tracked: true, cancellationToken);
        if (!_currentUser.HasRole("SuperAdmin"))
        {
            await AddRevisionAsync(policySet.Id, null, null, "Retire", "Rejected", correlationId,
                "Only SuperAdmin may retire an executable procurement policy.", PolicySnapshot(policySet), null);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ProcurementPolicyAuthorizationException("Only SuperAdmin may retire executable procurement policies.");
        }
        ProcurementPolicyLifecyclePolicy.EnsureCanRetire(policySet);
        EnsureRowVersion(policySet.RowVersion, request.RowVersion, "policy");
        var before = PolicySnapshot(policySet);
        policySet.LifecycleStatus = ProcurementPolicyLifecycleStatus.Retired;
        policySet.RetiredAt = DateTime.UtcNow;
        policySet.RetiredById = _currentUser.UserId;
        Touch(policySet);
        await PolicySets.UpdateAsync(policySet);
        await AddRevisionAsync(policySet.Id, null, null, "Retire", "Succeeded", correlationId, request.Reason, before, PolicySnapshot(policySet));
        await SaveWithConcurrencyAsync("policy", cancellationToken);
        return await GetPolicySetAsync(policySet.Id, cancellationToken);
    }

    public async Task<ProcurementPolicySetDto> CloneDraftAsync(
        Guid id,
        CloneProcurementPolicySetRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var source = await FindPolicySetAsync(id, tracked: false, cancellationToken);
        if (source.LifecycleStatus == ProcurementPolicyLifecycleStatus.Draft)
            throw new ProcurementPolicyConflictException("The selected policy is already a Draft.");
        var versions = await PolicySets.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId && item.PolicyKey == source.PolicyKey)
            .ToListAsync(cancellationToken);
        if (versions.Any(item => !item.IsDeleted && item.LifecycleStatus == ProcurementPolicyLifecycleStatus.Draft))
            throw new ProcurementPolicyConflictException("This policy family already has an editable Draft.");

        var now = DateTime.UtcNow;
        var clone = new ProcurementPolicySet
        {
            TenantId = source.TenantId,
            PolicyKey = source.PolicyKey,
            Code = source.Code,
            Name = source.Name,
            Description = source.Description,
            Version = ProcurementPolicyLifecyclePolicy.GetNextVersion(versions),
            LifecycleStatus = ProcurementPolicyLifecycleStatus.Draft,
            ScopeType = source.ScopeType,
            SourceConfigurationProfileId = source.SourceConfigurationProfileId,
            BasePolicySetId = source.BasePolicySetId,
            SupersedesPolicySetId = source.Id,
            DefaultCurrencyCode = source.DefaultCurrencyCode,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            ChangeSummary = NullIfWhiteSpace(request.ChangeSummary) ?? $"Drafted from version {source.Version}",
            IsDefault = source.IsDefault,
            CreatedAt = now,
            CreatedBy = _currentUser.FullName,
            CreatedById = _currentUser.UserId
        };
        await PolicySets.AddAsync(clone);
        await CloneRulesAsync(source.Id, clone, now, cancellationToken);
        await AddRevisionAsync(source.Id, null, null, "CloneSource", "Succeeded", correlationId,
            $"Created Draft version {clone.Version}.", PolicySnapshot(source), null);
        await AddRevisionAsync(clone.Id, null, null, "CloneDraft", "Succeeded", correlationId, request.ChangeSummary, null, PolicySnapshot(clone));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetPolicySetAsync(clone.Id, cancellationToken);
    }

    public async Task DeleteDraftAsync(
        Guid id,
        ProcurementPolicyLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEditor();
        var policySet = await FindPolicySetAsync(id, tracked: true, cancellationToken);
        ProcurementPolicyLifecyclePolicy.EnsureEditable(policySet);
        EnsureRowVersion(policySet.RowVersion, request.RowVersion, "policy");
        var before = PolicySnapshot(policySet);
        foreach (var (kind, rule) in await GetRuleEntitiesAsync(policySet.Id, tracked: true, cancellationToken))
        {
            SoftDeleteRule(rule);
            await UpdateRuleEntityAsync(rule, kind);
        }
        policySet.IsDeleted = true;
        policySet.DeletedAt = DateTime.UtcNow;
        policySet.DeletedBy = _currentUser.FullName;
        await PolicySets.UpdateAsync(policySet);
        await AddRevisionAsync(policySet.Id, null, null, "DeleteDraft", "Succeeded", correlationId, request.Reason, before, null);
        await SaveWithConcurrencyAsync("policy", cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementPolicyRevisionDto>> GetHistoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        await FindPolicySetAsync(id, tracked: false, cancellationToken);
        return (await Revisions.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == id)
                .AsNoTracking().OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken))
            .Select(MapRevision).ToList();
    }

    private async Task<ProcurementPolicyValidationResultDto> BuildValidationAsync(
        ProcurementPolicySet policySet,
        CancellationToken cancellationToken)
    {
        var errors = new List<ProcurementPolicyValidationIssueDto>();
        var warnings = new List<ProcurementPolicyValidationIssueDto>();
        ValidateDatesForResult(policySet.EffectiveFrom, policySet.EffectiveTo, errors, "POLICY_DATES", "Policy effective period is invalid.");
        if (policySet.SourceConfigurationProfileId == Guid.Empty)
            AddError(errors, "SOURCE_CONFIGURATION", "An immutable approved configuration source is required.");
        else
        {
            var source = await ConfigurationProfiles.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.Id == policySet.SourceConfigurationProfileId)
                .AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (source is null || source.PublishedAt is null || source.LifecycleStatus == ProcurementConfigurationProfileStatus.Draft)
                AddError(errors, "SOURCE_CONFIGURATION", "The source configuration must be a Published or Retired version that was previously published.");
        }
        if (policySet.ScopeType == ProcurementPolicyScopeType.TenantOverride && !policySet.BasePolicySetId.HasValue)
            AddError(errors, "BASE_POLICY_REQUIRED", "A tenant override requires an immutable base policy.");

        var rules = await GetRuleEntitiesAsync(policySet.Id, tracked: false, cancellationToken);
        foreach (var kind in Enum.GetValues<ProcurementPolicyRuleKind>())
        {
            if (!rules.Any(item => item.Kind == kind && GetRuleEnabled(item.Entity)))
                AddError(errors, "RULE_FAMILY_MISSING", RequiredRuleFamilyMessage(kind), kind);
        }

        var duplicateCodes = rules.GroupBy(item => GetRuleCode(item.Entity), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);
        foreach (var duplicate in duplicateCodes)
            AddError(errors, "RULE_CODE_DUPLICATE", $"Rule code '{duplicate.Key}' is duplicated across the policy.");

        foreach (var (kind, entity) in rules)
        {
            var from = GetRuleEffectiveFrom(entity);
            var to = GetRuleEffectiveTo(entity);
            if (to.HasValue && to.Value < from)
                AddError(errors, "RULE_DATES", "Rule effective-to cannot be before effective-from.", kind, GetRuleId(entity), GetRuleCode(entity));
            if (from < policySet.EffectiveFrom || (policySet.EffectiveTo.HasValue && (!to.HasValue || to.Value > policySet.EffectiveTo.Value)))
                AddError(errors, "RULE_OUTSIDE_POLICY", "Rule effective period must be contained by the policy effective period.", kind, GetRuleId(entity), GetRuleCode(entity));
            if (!ProcurementConfigurationDecisionRegistry.TryGet(GetRuleSourceDecisionKey(entity), out _))
                AddError(errors, "SOURCE_DECISION", "Rule source decision key is not registered.", kind, GetRuleId(entity), GetRuleCode(entity));
            if (GetRuleOverrideAction(entity) is ProcurementPolicyOverrideAction.Replace or ProcurementPolicyOverrideAction.Disable && !GetRuleSourceRuleId(entity).HasValue)
                AddError(errors, "OVERRIDE_SOURCE", "Replace/Disable rules must identify the source rule they override.", kind, GetRuleId(entity), GetRuleCode(entity));
        }

        ValidateThresholdRelationships(rules, errors);
        ValidateOperationalMethodRules(rules, errors);
        await ValidateRfqWorkflowReferencesAsync(rules, errors, cancellationToken);
        ValidateAuthorityBounds(rules, errors);
        ValidateSodRules(rules, errors);
        await ValidateRoleLineageAsync(rules, errors, cancellationToken);
        if (rules.Count > 0 && rules.All(item => !GetRuleEnabled(item.Entity)))
            warnings.Add(new ProcurementPolicyValidationIssueDto { Code = "ALL_RULES_DISABLED", Message = "Every policy rule is disabled.", Severity = "Warning" });

        return new ProcurementPolicyValidationResultDto { Errors = errors, Warnings = warnings };
    }

    private static void ValidateOperationalMethodRules(
        IEnumerable<(ProcurementPolicyRuleKind Kind, object Entity)> rules,
        ICollection<ProcurementPolicyValidationIssueDto> errors)
    {
        foreach (var method in rules.Where(item => item.Kind == ProcurementPolicyRuleKind.Method && GetRuleEnabled(item.Entity))
                     .Select(item => (ProcurementPolicyMethodRule)item.Entity))
        {
            if (method.Method == ProcurementMethodType.RequestForQuotation &&
                (!method.RequiresCompetition || method.MinimumQuotationCount <= 0))
                AddError(errors, "RFQ_COMPETITION_REQUIRED",
                    "An enabled Request for Quotation rule must require competition and a positive minimum quotation count.",
                    ProcurementPolicyRuleKind.Method, method.Id, method.RuleCode);
            if (method.Method == ProcurementMethodType.RequestForQuotation && !method.WorkflowDefinitionId.HasValue)
                AddError(errors, "RFQ_WORKFLOW_REQUIRED",
                    "An enabled Request for Quotation rule must select the shared evaluation approval workflow used at dispatch and award.",
                    ProcurementPolicyRuleKind.Method, method.Id, method.RuleCode);
        }
    }

    private async Task ValidateRfqWorkflowReferencesAsync(
        IEnumerable<(ProcurementPolicyRuleKind Kind, object Entity)> rules,
        ICollection<ProcurementPolicyValidationIssueDto> errors,
        CancellationToken cancellationToken)
    {
        var rfqMethods = rules
            .Where(item => item.Kind == ProcurementPolicyRuleKind.Method && GetRuleEnabled(item.Entity))
            .Select(item => (ProcurementPolicyMethodRule)item.Entity)
            .Where(item => item.Method == ProcurementMethodType.RequestForQuotation && item.WorkflowDefinitionId.HasValue)
            .ToList();
        if (rfqMethods.Count == 0) return;

        var definitionIds = rfqMethods.Select(item => item.WorkflowDefinitionId!.Value).Distinct().ToList();
        var definitions = await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && definitionIds.Contains(item.Id))
            .Include(item => item.EntityType)
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var method in rfqMethods)
        {
            if (!definitions.TryGetValue(method.WorkflowDefinitionId!.Value, out var definition) ||
                definition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published ||
                !definition.IsActive ||
                !definition.EntityType.IsActive ||
                !string.Equals(definition.EntityType.Code, "TENDER_EVALUATION", StringComparison.OrdinalIgnoreCase))
            {
                AddError(errors, "RFQ_WORKFLOW_ENTITY_INVALID",
                    "The RFQ evaluation workflow must be an active Published workflow for the TENDER_EVALUATION entity type.",
                    ProcurementPolicyRuleKind.Method, method.Id, method.RuleCode);
            }
        }
    }

    private static void ValidateThresholdRelationships(
        IReadOnlyList<(ProcurementPolicyRuleKind Kind, object Entity)> rules,
        ICollection<ProcurementPolicyValidationIssueDto> errors)
    {
        var categories = rules.Where(item => item.Kind == ProcurementPolicyRuleKind.Category && GetRuleEnabled(item.Entity))
            .Select(item => (ProcurementPolicyCategoryRule)item.Entity).ToList();
        var methods = rules.Where(item => item.Kind == ProcurementPolicyRuleKind.Method && GetRuleEnabled(item.Entity))
            .Select(item => (ProcurementPolicyMethodRule)item.Entity).ToList();
        var thresholds = rules.Where(item => item.Kind == ProcurementPolicyRuleKind.Threshold && GetRuleEnabled(item.Entity))
            .Select(item => (ProcurementPolicyThresholdRule)item.Entity).ToList();
        foreach (var threshold in thresholds)
        {
            if (threshold.UpperBound.HasValue && threshold.UpperBound.Value < threshold.LowerBound)
                AddError(errors, "THRESHOLD_BOUNDS", "Upper bound cannot be below lower bound.", ProcurementPolicyRuleKind.Threshold, threshold.Id, threshold.RuleCode);
            if (!categories.Any(item => item.Category == threshold.Category && ServiceClassMatches(item.ServiceClass, threshold.ServiceClass)))
                AddError(errors, "THRESHOLD_CATEGORY",
                    $"No enabled Category rule matches category {threshold.Category} and service class '{DisplayServiceClass(threshold.ServiceClass)}'. Select the intended Category rule again.",
                    ProcurementPolicyRuleKind.Threshold, threshold.Id, threshold.RuleCode);
            if (!methods.Any(item => item.Category == threshold.Category && item.Method == threshold.Method && ServiceClassMatches(item.ServiceClass, threshold.ServiceClass)))
                AddError(errors, "THRESHOLD_METHOD",
                    $"No enabled Method rule matches category {threshold.Category}, method {threshold.Method}, and service class '{DisplayServiceClass(threshold.ServiceClass)}'. Select the intended Method rule again.",
                    ProcurementPolicyRuleKind.Threshold, threshold.Id, threshold.RuleCode);
        }
        for (var leftIndex = 0; leftIndex < thresholds.Count; leftIndex++)
        for (var rightIndex = leftIndex + 1; rightIndex < thresholds.Count; rightIndex++)
        {
            var left = thresholds[leftIndex];
            var right = thresholds[rightIndex];
            if (left.Category != right.Category || left.Method != right.Method ||
                !string.Equals(left.CurrencyCode, right.CurrencyCode, StringComparison.OrdinalIgnoreCase) ||
                !ServiceClassMatches(left.ServiceClass, right.ServiceClass) ||
                !DateRangesOverlap(left.EffectiveFrom, left.EffectiveTo, right.EffectiveFrom, right.EffectiveTo) ||
                !AmountRangesOverlap(left, right)) continue;
            AddError(errors, "THRESHOLD_OVERLAP", $"Threshold overlaps rule '{right.RuleCode}'.", ProcurementPolicyRuleKind.Threshold, left.Id, left.RuleCode);
            AddError(errors, "THRESHOLD_OVERLAP", $"Threshold overlaps rule '{left.RuleCode}'.", ProcurementPolicyRuleKind.Threshold, right.Id, right.RuleCode);
        }
    }

    private static void ValidateAuthorityBounds(
        IEnumerable<(ProcurementPolicyRuleKind Kind, object Entity)> rules,
        ICollection<ProcurementPolicyValidationIssueDto> errors)
    {
        foreach (var authority in rules.Where(item => item.Kind == ProcurementPolicyRuleKind.Authority).Select(item => (ProcurementPolicyAuthorityRule)item.Entity))
            if (authority.UpperBound.HasValue && authority.UpperBound.Value < authority.LowerBound)
                AddError(errors, "AUTHORITY_BOUNDS", "Authority upper bound cannot be below lower bound.", ProcurementPolicyRuleKind.Authority, authority.Id, authority.RuleCode);
    }

    private static void ValidateSodRules(
        IEnumerable<(ProcurementPolicyRuleKind Kind, object Entity)> rules,
        ICollection<ProcurementPolicyValidationIssueDto> errors)
    {
        var sodRules = rules.Where(item => item.Kind == ProcurementPolicyRuleKind.SegregationOfDuties)
            .Select(item => (ProcurementPolicySodRule)item.Entity).ToList();
        foreach (var sod in sodRules)
            if (string.Equals(sod.InitiatorRole.Trim(), sod.ConflictingRole.Trim(), StringComparison.OrdinalIgnoreCase))
                AddError(errors, "SOD_ROLE_CONFLICT", "Initiator and conflicting roles must differ.", ProcurementPolicyRuleKind.SegregationOfDuties, sod.Id, sod.RuleCode);

        foreach (var definition in ProcurementSodRequiredControlRegistry.Definitions)
        {
            var match = sodRules.SingleOrDefault(item => item.IsEnabled &&
                string.Equals(item.RuleCode, definition.Code, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                AddError(errors, "SOD_REQUIRED_CONTROL_MISSING",
                    $"Required TDC SOD control '{definition.Code}' is missing.",
                    ProcurementPolicyRuleKind.SegregationOfDuties, ruleCode: definition.Code);
                continue;
            }

            if (!ProcurementSodRequiredControlRegistry.MatchesRequiredShape(definition,
                    match.InitiatorRole, match.ConflictingRole, match.EntityType, match.Action, match.Enforcement))
                AddError(errors, "SOD_REQUIRED_CONTROL_INVALID",
                    $"Required TDC SOD control '{definition.Code}' must retain its prescribed roles, entity, action, and HardStop enforcement.",
                    ProcurementPolicyRuleKind.SegregationOfDuties, match.Id, match.RuleCode);
        }
    }

    private async Task<int> MaterializeSourceRulesAsync(
        ProcurementPolicySet policySet,
        ProcurementConfigurationProfile source,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var decisions = await ConfigurationDecisions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.ProfileId == source.Id)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (decisions.Count != ProcurementConfigurationDecisionRegistry.Definitions.Count ||
            decisions.Any(item => item.Status != ProcurementConfigurationDecisionStatus.Approved ||
                                  item.ApprovalStatus != ProcurementConfigurationApprovalStatus.Approved))
            throw ValidationException("SOURCE_CONFIGURATION_INCOMPLETE", "The source configuration does not contain fourteen approved decisions.");

        var tenantRoles = (await _roleService.GetRolesForTenantAsync(_currentUser.TenantId, cancellationToken))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToList();
        Guid? RoleId(string? name) => tenantRoles.SingleOrDefault(item =>
            string.Equals(item.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;

        var count = 0;
        var dec001 = DeserializeDecision<ProcurementMethodThresholdDecisionValueDto>(decisions, "DEC-001");
        var dec005 = DeserializeDecision<ProcurementPettyPurchaseDecisionValueDto>(decisions, "DEC-005");
        var dec006 = DeserializeDecision<ProcurementExceptionPrerequisiteDecisionValueDto>(decisions, "DEC-006");
        var category = new ProcurementPolicyCategoryRule
        {
            TenantId = policySet.TenantId, PolicySetId = policySet.Id, RuleCode = $"CAT-{dec001.Category}".ToUpperInvariant(),
            Name = dec001.ServiceClass, Category = dec001.Category, ServiceClass = dec001.ServiceClass,
            Description = "Materialized from approved DEC-001 configuration.", RequiresSpecification = true,
            SourceDecisionKey = "DEC-001", EffectiveFrom = EnsureUtc(dec001.EffectiveFrom), EffectiveTo = EnsureUtc(dec001.EffectiveTo),
            CreatedAt = now, CreatedBy = _currentUser.FullName, CreatedById = _currentUser.UserId
        };
        await Categories.AddAsync(category); count++;
        var method = new ProcurementPolicyMethodRule
        {
            TenantId = policySet.TenantId, PolicySetId = policySet.Id, RuleCode = $"METHOD-{dec001.Category}-{dec001.Method}".ToUpperInvariant(),
            Name = dec001.Method.ToString(), Category = dec001.Category, ServiceClass = dec001.ServiceClass, Method = dec001.Method,
            IsAllowed = true, RequiresCompetition = dec001.Method != ProcurementMethodType.PettyPurchase, MinimumQuotationCount = 0,
            JustificationRequired = dec001.Method == ProcurementMethodType.PettyPurchase
                ? dec005.JustificationRequired
                : dec001.Method == dec006.Method,
            ApplicabilityConditions = "Materialized from approved DEC-001 configuration.", SourceDecisionKey = "DEC-001",
            EffectiveFrom = EnsureUtc(dec001.EffectiveFrom), EffectiveTo = EnsureUtc(dec001.EffectiveTo),
            CreatedAt = now, CreatedBy = _currentUser.FullName, CreatedById = _currentUser.UserId
        };
        await Methods.AddAsync(method); count++;
        await Thresholds.AddAsync(new ProcurementPolicyThresholdRule
        {
            TenantId = policySet.TenantId, PolicySetId = policySet.Id, RuleCode = "THRESHOLD-DEC-001", Name = "Approved method threshold",
            Category = dec001.Category, ServiceClass = dec001.ServiceClass, Method = dec001.Method, CurrencyCode = dec001.CurrencyCode,
            LowerBound = dec001.LowerBound, UpperBound = dec001.UpperBound, LowerInclusive = dec001.LowerInclusive,
            UpperInclusive = dec001.UpperInclusive, StatutoryReference = dec001.StatutoryReference, SourceDecisionKey = "DEC-001",
            EffectiveFrom = EnsureUtc(dec001.EffectiveFrom), EffectiveTo = EnsureUtc(dec001.EffectiveTo),
            CreatedAt = now, CreatedBy = _currentUser.FullName, CreatedById = _currentUser.UserId
        }); count++;

        var dec002 = DeserializeDecision<ProcurementAuthorityDecisionValueDto>(decisions, "DEC-002");
        foreach (var applicableCategory in dec002.ApplicableCategories)
        {
            await Authorities.AddAsync(new ProcurementPolicyAuthorityRule
            {
                TenantId = policySet.TenantId, PolicySetId = policySet.Id,
                RuleCode = $"AUTH-DEC-002-{applicableCategory}".ToUpperInvariant(), AuthorityName = dec002.AuthorityLevel,
                AuthorityRoleId = RoleId(dec002.AuthorityLevel), AuthorityRole = dec002.AuthorityLevel,
                Category = applicableCategory, CurrencyCode = dec002.CurrencyCode,
                LowerBound = dec002.LowerBound, UpperBound = dec002.UpperBound, LowerInclusive = dec002.LowerInclusive,
                UpperInclusive = dec002.UpperInclusive, EscalationAuthorityRoleId = RoleId(dec002.EscalationAuthority),
                EscalationAuthority = dec002.EscalationAuthority,
                SourceDecisionKey = "DEC-002", EffectiveFrom = EnsureUtc(dec002.EffectiveFrom), EffectiveTo = EnsureUtc(dec002.EffectiveTo),
                CreatedAt = now, CreatedBy = _currentUser.FullName, CreatedById = _currentUser.UserId
            }); count++;
        }

        await Exceptions.AddAsync(new ProcurementPolicyExceptionRule
        {
            TenantId = policySet.TenantId, PolicySetId = policySet.Id, RuleCode = "EXCEPTION-PETTY-PURCHASE",
            ExceptionName = "Petty purchase waiver", ExceptionType = "PettyPurchase", Method = ProcurementMethodType.PettyPurchase,
            Disposition = dec005.WaiverEligible ? ProcurementExceptionDisposition.ApprovalRequired : ProcurementExceptionDisposition.Prohibited,
            JustificationRequired = dec005.JustificationRequired, EvidenceRequired = dec005.EvidenceRequirements.Count > 0,
            ApproverRoleId = RoleId(dec005.ApproverRole), ApproverRole = dec005.ApproverRole,
            MaximumDurationDays = dec005.ExpiryDate.HasValue ? Math.Max(1, (int)(dec005.ExpiryDate.Value.Date - dec005.EffectiveFrom.Date).TotalDays) : null,
            SourceDecisionKey = "DEC-005", EffectiveFrom = EnsureUtc(dec005.EffectiveFrom), EffectiveTo = EnsureUtc(dec005.EffectiveTo),
            CreatedAt = now, CreatedBy = _currentUser.FullName, CreatedById = _currentUser.UserId
        }); count++;
        await Exceptions.AddAsync(new ProcurementPolicyExceptionRule
        {
            TenantId = policySet.TenantId, PolicySetId = policySet.Id, RuleCode = "EXCEPTION-DEC-006",
            ExceptionName = $"{dec006.Method} prerequisites", ExceptionType = dec006.Method.ToString(), Method = dec006.Method,
            Disposition = ProcurementExceptionDisposition.ApprovalRequired, JustificationRequired = true,
            EvidenceRequired = dec006.MandatoryEvidenceChecklist.Count > 0, PostAwardFilingRequired = true,
            ApproverRoleId = RoleId(dec006.ApprovalAuthority), ApproverRole = dec006.ApprovalAuthority, SourceDecisionKey = "DEC-006",
            EffectiveFrom = EnsureUtc(dec006.EffectiveFrom), EffectiveTo = EnsureUtc(dec006.EffectiveTo),
            CreatedAt = now, CreatedBy = _currentUser.FullName, CreatedById = _currentUser.UserId
        }); count++;

        count += await MaterializeEvidenceRulesAsync(policySet, decisions, now);
        return count;
    }

    private async Task<int> MaterializeEvidenceRulesAsync(ProcurementPolicySet policySet, IReadOnlyCollection<ProcurementConfigurationDecision> decisions, DateTime now)
    {
        var requirements = new List<(string DecisionKey, ProcurementEvidenceStage Stage, ProcurementMethodType? Method, IEnumerable<string> Names, DateTime From, DateTime? To)>
        {
            EvidenceTuple("DEC-004", ProcurementEvidenceStage.Evaluation, DeserializeDecision<ProcurementAuthorityStageDecisionValueDto>(decisions, "DEC-004"), item => item.EvidenceRequirements),
            EvidenceTuple("DEC-005", ProcurementEvidenceStage.Requisition, DeserializeDecision<ProcurementPettyPurchaseDecisionValueDto>(decisions, "DEC-005"), item => item.EvidenceRequirements, ProcurementMethodType.PettyPurchase),
            EvidenceTuple("DEC-006", ProcurementEvidenceStage.Sourcing, DeserializeDecision<ProcurementExceptionPrerequisiteDecisionValueDto>(decisions, "DEC-006"), item => item.MandatoryEvidenceChecklist,
                DeserializeDecision<ProcurementExceptionPrerequisiteDecisionValueDto>(decisions, "DEC-006").Method),
            EvidenceTuple("DEC-008", ProcurementEvidenceStage.PurchaseOrder, DeserializeDecision<ProcurementSignatureDecisionValueDto>(decisions, "DEC-008"), item => item.EvidenceRequirements)
        };
        var count = 0;
        foreach (var requirement in requirements)
        foreach (var name in requirement.Names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            count++;
            await Evidence.AddAsync(new ProcurementPolicyEvidenceRule
            {
                TenantId = policySet.TenantId, PolicySetId = policySet.Id,
                RuleCode = $"EVID-{requirement.DecisionKey}-{count:000}", EvidenceName = name, Stage = requirement.Stage,
                Method = requirement.Method,
                SharedRequirementKey = $"PROC-{requirement.DecisionKey}-{count:000}", IsMandatory = true, RequiresVerification = true,
                SourceDecisionKey = requirement.DecisionKey, EffectiveFrom = EnsureUtc(requirement.From), EffectiveTo = EnsureUtc(requirement.To),
                CreatedAt = now, CreatedBy = _currentUser.FullName, CreatedById = _currentUser.UserId
            });
        }
        return count;
    }

    private static (string DecisionKey, ProcurementEvidenceStage Stage, ProcurementMethodType? Method, IEnumerable<string> Names, DateTime From, DateTime? To) EvidenceTuple<T>(
        string key,
        ProcurementEvidenceStage stage,
        T value,
        Func<T, IEnumerable<string>> names,
        ProcurementMethodType? method = null) where T : EffectiveDatedDecisionValueDto =>
        (key, stage, method, names(value), value.EffectiveFrom, value.EffectiveTo);

    private static T DeserializeDecision<T>(IEnumerable<ProcurementConfigurationDecision> decisions, string key)
    {
        var decision = decisions.Single(item => item.DecisionKey == key);
        return JsonSerializer.Deserialize<T>(decision.ValueJson, JsonOptions)
               ?? throw ValidationException("SOURCE_DECISION_VALUE", $"{key} could not be materialized.");
    }

    private async Task CloneRulesAsync(Guid sourceId, ProcurementPolicySet clone, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var (kind, source) in await GetRuleEntitiesAsync(sourceId, tracked: false, cancellationToken))
        {
            var cloned = CloneRuleEntity(source, kind, clone, now);
            await AddRuleEntityAsync(cloned, kind);
        }
    }

    private object CloneRuleEntity(object source, ProcurementPolicyRuleKind kind, ProcurementPolicySet clone, DateTime now)
    {
        SaveProcurementPolicyRuleValueBase value = kind switch
        {
            ProcurementPolicyRuleKind.Category => ToValue((ProcurementPolicyCategoryRule)source),
            ProcurementPolicyRuleKind.Method => ToValue((ProcurementPolicyMethodRule)source),
            ProcurementPolicyRuleKind.Threshold => ToValue((ProcurementPolicyThresholdRule)source),
            ProcurementPolicyRuleKind.Authority => ToValue((ProcurementPolicyAuthorityRule)source),
            ProcurementPolicyRuleKind.Evidence => ToValue((ProcurementPolicyEvidenceRule)source),
            ProcurementPolicyRuleKind.Exception => ToValue((ProcurementPolicyExceptionRule)source),
            ProcurementPolicyRuleKind.SegregationOfDuties => ToValue((ProcurementPolicySodRule)source),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        value.SourceRuleId = GetRuleId(source);
        var cloned = CreateRuleEntity(clone, kind, value);
        SetCreated(cloned, now);
        return cloned;
    }

    private async Task<ProcurementPolicySetDto> MapPolicySetAsync(ProcurementPolicySet policySet, CancellationToken cancellationToken)
    {
        var rules = (await GetRuleEntitiesAsync(policySet.Id, tracked: false, cancellationToken))
            .Select(item => MapRule(item.Entity, item.Kind))
            .OrderBy(item => item.Kind).ThenBy(item => item.Priority).ThenBy(item => item.RuleCode).ToList();
        var source = await ConfigurationProfiles.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.Id == policySet.SourceConfigurationProfileId)
            .AsNoTracking().FirstAsync(cancellationToken);
        var validation = await BuildValidationAsync(policySet, cancellationToken);
        var history = await Revisions.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == policySet.Id)
            .AsNoTracking().OrderByDescending(item => item.CreatedAt).Take(50).ToListAsync(cancellationToken);
        var summary = MapSummary(policySet, source, rules);
        return new ProcurementPolicySetDto
        {
            Id = summary.Id, PolicyKey = summary.PolicyKey, Code = summary.Code, Name = summary.Name, Version = summary.Version,
            LifecycleStatus = summary.LifecycleStatus, ScopeType = summary.ScopeType,
            SourceConfigurationProfileId = summary.SourceConfigurationProfileId,
            SourceConfigurationProfileCode = summary.SourceConfigurationProfileCode,
            SourceConfigurationProfileVersion = summary.SourceConfigurationProfileVersion,
            DefaultCurrencyCode = summary.DefaultCurrencyCode, EffectiveFrom = summary.EffectiveFrom, EffectiveTo = summary.EffectiveTo,
            IsDefault = summary.IsDefault, RuleCount = summary.RuleCount, RuleFamilyCount = summary.RuleFamilyCount,
            IsComplete = summary.IsComplete, UpdatedBy = summary.UpdatedBy, UpdatedAt = summary.UpdatedAt, RowVersion = summary.RowVersion,
            Description = policySet.Description, ChangeSummary = policySet.ChangeSummary, BasePolicySetId = policySet.BasePolicySetId,
            SupersedesPolicySetId = policySet.SupersedesPolicySetId, PublishedAt = policySet.PublishedAt,
            PublishedById = policySet.PublishedById, RetiredAt = policySet.RetiredAt, RetiredById = policySet.RetiredById,
            Rules = rules, Validation = validation, RecentHistory = history.Select(MapRevision).ToList()
        };
    }

    private async Task<ProcurementPolicySetSummaryDto> MapSummaryAsync(ProcurementPolicySet policySet, CancellationToken cancellationToken)
    {
        var source = await ConfigurationProfiles.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.Id == policySet.SourceConfigurationProfileId)
            .AsNoTracking().FirstAsync(cancellationToken);
        var rules = (await GetRuleEntitiesAsync(policySet.Id, tracked: false, cancellationToken)).Select(item => MapRule(item.Entity, item.Kind)).ToList();
        return MapSummary(policySet, source, rules);
    }

    private static ProcurementPolicySetSummaryDto MapSummary(
        ProcurementPolicySet policySet,
        ProcurementConfigurationProfile source,
        IReadOnlyCollection<ProcurementPolicyRuleDto> rules)
    {
        var enabledFamilies = rules.Where(item => item.IsEnabled).Select(item => item.Kind).Distinct().Count();
        return new ProcurementPolicySetSummaryDto
        {
            Id = policySet.Id, PolicyKey = policySet.PolicyKey, Code = policySet.Code, Name = policySet.Name,
            Version = policySet.Version, LifecycleStatus = policySet.LifecycleStatus, ScopeType = policySet.ScopeType,
            SourceConfigurationProfileId = source.Id, SourceConfigurationProfileCode = source.ProfileCode,
            SourceConfigurationProfileVersion = source.Version, DefaultCurrencyCode = policySet.DefaultCurrencyCode,
            EffectiveFrom = policySet.EffectiveFrom, EffectiveTo = policySet.EffectiveTo, IsDefault = policySet.IsDefault,
            RuleCount = rules.Count, RuleFamilyCount = enabledFamilies, IsComplete = enabledFamilies == Enum.GetValues<ProcurementPolicyRuleKind>().Length,
            UpdatedBy = policySet.UpdatedBy ?? policySet.CreatedBy, UpdatedAt = policySet.UpdatedAt ?? policySet.CreatedAt,
            RowVersion = Convert.ToBase64String(policySet.RowVersion)
        };
    }

    private static ProcurementPolicyRuleDto MapRule(object entity, ProcurementPolicyRuleKind kind) => new()
    {
        Id = GetRuleId(entity), Kind = kind, RuleCode = GetRuleCode(entity), Name = GetRuleName(entity),
        Priority = GetRulePriority(entity), IsEnabled = GetRuleEnabled(entity), EffectiveFrom = GetRuleEffectiveFrom(entity),
        EffectiveTo = GetRuleEffectiveTo(entity), OverrideAction = GetRuleOverrideAction(entity), SourceRuleId = GetRuleSourceRuleId(entity),
        SourceDecisionKey = GetRuleSourceDecisionKey(entity), Value = RuleValue(entity, kind),
        RowVersion = Convert.ToBase64String(GetRuleRowVersion(entity))
    };

    private static JsonElement RuleValue(object entity, ProcurementPolicyRuleKind kind) => kind switch
    {
        ProcurementPolicyRuleKind.Category => JsonSerializer.SerializeToElement(ToValue((ProcurementPolicyCategoryRule)entity), JsonOptions),
        ProcurementPolicyRuleKind.Method => JsonSerializer.SerializeToElement(ToValue((ProcurementPolicyMethodRule)entity), JsonOptions),
        ProcurementPolicyRuleKind.Threshold => JsonSerializer.SerializeToElement(ToValue((ProcurementPolicyThresholdRule)entity), JsonOptions),
        ProcurementPolicyRuleKind.Authority => JsonSerializer.SerializeToElement(ToValue((ProcurementPolicyAuthorityRule)entity), JsonOptions),
        ProcurementPolicyRuleKind.Evidence => JsonSerializer.SerializeToElement(ToValue((ProcurementPolicyEvidenceRule)entity), JsonOptions),
        ProcurementPolicyRuleKind.Exception => JsonSerializer.SerializeToElement(ToValue((ProcurementPolicyExceptionRule)entity), JsonOptions),
        ProcurementPolicyRuleKind.SegregationOfDuties => JsonSerializer.SerializeToElement(ToValue((ProcurementPolicySodRule)entity), JsonOptions),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static ProcurementPolicyRevisionDto MapRevision(ProcurementPolicyRevision revision) => new()
    {
        Id = revision.Id, PolicySetId = revision.PolicySetId, RuleId = revision.RuleId, RuleKind = revision.RuleKind,
        Action = revision.Action, Result = revision.Result, CorrelationId = revision.CorrelationId,
        ActorUserId = revision.ActorUserId, ActorName = revision.ActorName, ActorRoles = revision.ActorRoles,
        Reason = revision.Reason, Before = ParseOptionalJson(revision.BeforeJson), After = ParseOptionalJson(revision.AfterJson), Timestamp = revision.CreatedAt
    };

    private async Task<IReadOnlyList<(ProcurementPolicyRuleKind Kind, object Entity)>> GetRuleEntitiesAsync(
        Guid policySetId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var result = new List<(ProcurementPolicyRuleKind, object)>();
        async Task AddAsync<T>(IQueryable<T> query, ProcurementPolicyRuleKind kind) where T : BaseEntity
        {
            if (!tracked) query = query.AsNoTracking();
            foreach (var item in await query.ToListAsync(cancellationToken)) result.Add((kind, item));
        }
        await AddAsync(Categories.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == policySetId), ProcurementPolicyRuleKind.Category);
        await AddAsync(Methods.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == policySetId), ProcurementPolicyRuleKind.Method);
        await AddAsync(Thresholds.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == policySetId), ProcurementPolicyRuleKind.Threshold);
        await AddAsync(Authorities.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == policySetId), ProcurementPolicyRuleKind.Authority);
        await AddAsync(Evidence.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == policySetId), ProcurementPolicyRuleKind.Evidence);
        await AddAsync(Exceptions.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == policySetId), ProcurementPolicyRuleKind.Exception);
        await AddAsync(SodRules.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == policySetId), ProcurementPolicyRuleKind.SegregationOfDuties);
        return result;
    }

    private async Task<object> FindRuleEntityAsync(Guid policySetId, ProcurementPolicyRuleKind kind, Guid ruleId, bool tracked, CancellationToken cancellationToken)
    {
        async Task<T?> FindAsync<T>(IGenericRepository<T> repository) where T : BaseEntity
        {
            var query = repository.GetQueryable(item => EF.Property<Guid>(item, "TenantId") == _currentUser.TenantId &&
                                                      EF.Property<Guid>(item, "PolicySetId") == policySetId &&
                                                      EF.Property<Guid>(item, "Id") == ruleId);
            if (!tracked) query = query.AsNoTracking();
            return await query.FirstOrDefaultAsync(cancellationToken);
        }
        object? found = kind switch
        {
            ProcurementPolicyRuleKind.Category => await FindAsync(Categories),
            ProcurementPolicyRuleKind.Method => await FindAsync(Methods),
            ProcurementPolicyRuleKind.Threshold => await FindAsync(Thresholds),
            ProcurementPolicyRuleKind.Authority => await FindAsync(Authorities),
            ProcurementPolicyRuleKind.Evidence => await FindAsync(Evidence),
            ProcurementPolicyRuleKind.Exception => await FindAsync(Exceptions),
            ProcurementPolicyRuleKind.SegregationOfDuties => await FindAsync(SodRules),
            _ => null
        };
        return found ?? throw new ProcurementPolicyNotFoundException("The policy rule was not found in this tenant.");
    }

    private async Task EnsureRuleCodeUniqueAsync(Guid policySetId, ProcurementPolicyRuleKind kind, string ruleCode, Guid? excludedId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(ruleCode);
        foreach (var (candidateKind, entity) in await GetRuleEntitiesAsync(policySetId, tracked: false, cancellationToken))
            if (GetRuleId(entity) != excludedId && string.Equals(GetRuleCode(entity), normalized, StringComparison.OrdinalIgnoreCase))
                throw new ProcurementPolicyConflictException($"Rule code '{normalized}' already exists as {RuleKindLabel(candidateKind)}.");
    }

    private async Task EnsureOverrideReferenceAsync(ProcurementPolicySet policySet, ProcurementPolicyRuleKind kind, SaveProcurementPolicyRuleValueBase value, CancellationToken cancellationToken)
    {
        if (value.OverrideAction is ProcurementPolicyOverrideAction.Add)
        {
            if (value.SourceRuleId.HasValue && policySet.ScopeType == ProcurementPolicyScopeType.TenantBaseline)
            {
                if (!policySet.SupersedesPolicySetId.HasValue)
                    throw ValidationException("OVERRIDE_SOURCE", "A baseline Add rule can reference a source rule only when it is a cloned policy revision.");

                // Baseline policy clones retain the prior rule id as immutable version lineage.
                // It is not tenant-override lineage, but it must still resolve to the directly
                // superseded policy and the same rule family before an inherited rule is edited.
                await FindRuleEntityAsync(
                    policySet.SupersedesPolicySetId.Value,
                    kind,
                    value.SourceRuleId.Value,
                    tracked: false,
                    cancellationToken);
            }
            return;
        }
        if (policySet.ScopeType != ProcurementPolicyScopeType.TenantOverride || !policySet.BasePolicySetId.HasValue || !value.SourceRuleId.HasValue)
            throw ValidationException("OVERRIDE_SOURCE", "Replace/Disable rules require a tenant-override policy and a source rule.");
        await FindRuleEntityAsync(policySet.BasePolicySetId.Value, kind, value.SourceRuleId.Value, tracked: false, cancellationToken);
    }

    private async Task EnsureWorkflowReferenceAsync(SaveProcurementPolicyRuleValueBase value, CancellationToken cancellationToken)
    {
        var workflowDefinitionId = value switch
        {
            SaveProcurementPolicyMethodRuleValue method => method.WorkflowDefinitionId,
            SaveProcurementPolicyAuthorityRuleValue authority => authority.WorkflowDefinitionId,
            SaveProcurementPolicyExceptionRuleValue exception => exception.WorkflowDefinitionId,
            _ => null
        };
        if (!workflowDefinitionId.HasValue) return;
        var definition = await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == workflowDefinitionId.Value &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                item.IsActive)
            .Include(item => item.EntityType)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (definition is null)
            throw ValidationException("WORKFLOW_REFERENCE", "Workflow references must identify an active Published workflow definition in this tenant.");
        if (value is SaveProcurementPolicyMethodRuleValue { Method: ProcurementMethodType.RequestForQuotation } &&
            (!definition.EntityType.IsActive ||
             !string.Equals(definition.EntityType.Code, "TENDER_EVALUATION", StringComparison.OrdinalIgnoreCase)))
            throw ValidationException("RFQ_WORKFLOW_ENTITY_INVALID",
                "The RFQ evaluation workflow must use the TENDER_EVALUATION entity type.");
    }

    private static SaveProcurementPolicyRuleValueBase GetAndValidateRuleValue(SaveProcurementPolicyRuleRequest request)
    {
        var values = new object?[] { request.Category, request.Method, request.Threshold, request.Authority, request.Evidence, request.Exception, request.SegregationOfDuties };
        if (values.Count(item => item is not null) != 1)
            throw ValidationException("RULE_PAYLOAD", "Exactly one typed rule value must be supplied.");
        SaveProcurementPolicyRuleValueBase? value = request.Kind switch
        {
            ProcurementPolicyRuleKind.Category => request.Category,
            ProcurementPolicyRuleKind.Method => request.Method,
            ProcurementPolicyRuleKind.Threshold => request.Threshold,
            ProcurementPolicyRuleKind.Authority => request.Authority,
            ProcurementPolicyRuleKind.Evidence => request.Evidence,
            ProcurementPolicyRuleKind.Exception => request.Exception,
            ProcurementPolicyRuleKind.SegregationOfDuties => request.SegregationOfDuties,
            _ => null
        };
        if (value is null) throw ValidationException("RULE_PAYLOAD", $"The typed payload does not match rule kind {request.Kind}.");
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        if (results.Count > 0)
            throw ValidationException("RULE_PAYLOAD", string.Join(" ", results.Select(item => item.ErrorMessage).Distinct()));
        value.RuleCode = NormalizeCode(value.RuleCode);
        value.SourceDecisionKey = value.SourceDecisionKey.Trim().ToUpperInvariant();
        value.EffectiveFrom = EnsureUtc(value.EffectiveFrom);
        value.EffectiveTo = EnsureUtc(value.EffectiveTo);
        if (value is SaveProcurementPolicyEvidenceRuleValue evidence &&
            string.IsNullOrWhiteSpace(evidence.SharedRequirementKey))
            evidence.SharedRequirementKey = value.RuleCode;
        return value;
    }

    private static void ValidateRuleValue(ProcurementPolicyRuleKind kind, SaveProcurementPolicyRuleValueBase value)
    {
        ValidateDates(value.EffectiveFrom, value.EffectiveTo, value.RuleCode);
        ProcurementConfigurationDecisionRegistry.GetRequired(value.SourceDecisionKey);
        if (value is SaveProcurementPolicyThresholdRuleValue threshold && threshold.UpperBound.HasValue && threshold.UpperBound.Value < threshold.LowerBound)
            throw ValidationException("THRESHOLD_BOUNDS", "Threshold upper bound cannot be below lower bound.");
        if (value is SaveProcurementPolicyAuthorityRuleValue authority && authority.UpperBound.HasValue && authority.UpperBound.Value < authority.LowerBound)
            throw ValidationException("AUTHORITY_BOUNDS", "Authority upper bound cannot be below lower bound.");
        if (value is SaveProcurementPolicySodRuleValue sod &&
            ((sod.InitiatorRoleId.HasValue && sod.InitiatorRoleId == sod.ConflictingRoleId) ||
             (!string.IsNullOrWhiteSpace(sod.InitiatorRole) && !string.IsNullOrWhiteSpace(sod.ConflictingRole) &&
              string.Equals(sod.InitiatorRole.Trim(), sod.ConflictingRole.Trim(), StringComparison.OrdinalIgnoreCase))))
            throw ValidationException("SOD_ROLE_CONFLICT", "Initiator and conflicting roles must differ.");
        if (kind == ProcurementPolicyRuleKind.Evidence && value is SaveProcurementPolicyEvidenceRuleValue evidence && evidence.IsMandatory && string.IsNullOrWhiteSpace(evidence.SharedRequirementKey))
            throw ValidationException("EVIDENCE_SHARED_KEY", "A mandatory evidence rule must reference a shared evidence requirement key.");
    }

    private async Task EnsureRoleReferencesAsync(
        SaveProcurementPolicyRuleValueBase value,
        CancellationToken cancellationToken)
    {
        if (value is not (SaveProcurementPolicyAuthorityRuleValue or
            SaveProcurementPolicyExceptionRuleValue or SaveProcurementPolicySodRuleValue)) return;

        var tenantRoles = (await _roleService.GetRolesForTenantAsync(_currentUser.TenantId, cancellationToken))
            .Where(role => !string.IsNullOrWhiteSpace(role.Name))
            .ToList();

        ApplicationRole Required(Guid? id, string? legacyName, string field)
        {
            var role = id.HasValue
                ? tenantRoles.SingleOrDefault(item => item.Id == id.Value)
                : tenantRoles.SingleOrDefault(item => string.Equals(
                    item.Name, legacyName?.Trim(), StringComparison.OrdinalIgnoreCase));
            return role ?? throw ValidationException(
                "POLICY_TENANT_ROLE_REQUIRED",
                $"{field} must identify a configured role assigned to an active user in the current tenant.");
        }

        ApplicationRole? Optional(Guid? id, string? legacyName, string field)
        {
            if (!id.HasValue && string.IsNullOrWhiteSpace(legacyName)) return null;
            return Required(id, legacyName, field);
        }

        if (value is SaveProcurementPolicyAuthorityRuleValue authority)
        {
            var role = Required(authority.AuthorityRoleId, authority.AuthorityRole, "Authority role");
            authority.AuthorityRoleId = role.Id;
            authority.AuthorityRole = role.Name!;
            var escalation = Optional(authority.EscalationAuthorityRoleId, authority.EscalationAuthority, "Escalation authority");
            authority.EscalationAuthorityRoleId = escalation?.Id;
            authority.EscalationAuthority = escalation?.Name;
            await EnsureWorkflowRolesAsync(authority.WorkflowDefinitionId,
                new[] { ("Authority role", role), ("Escalation authority", escalation) }, cancellationToken);
        }
        else if (value is SaveProcurementPolicyExceptionRuleValue exception)
        {
            var role = Required(exception.ApproverRoleId, exception.ApproverRole, "Approver role");
            exception.ApproverRoleId = role.Id;
            exception.ApproverRole = role.Name!;
            await EnsureWorkflowRolesAsync(exception.WorkflowDefinitionId,
                new[] { ("Approver role", role) }, cancellationToken);
        }
        else if (value is SaveProcurementPolicySodRuleValue sod)
        {
            var initiator = Required(sod.InitiatorRoleId, sod.InitiatorRole, "Initiator role");
            var conflicting = Required(sod.ConflictingRoleId, sod.ConflictingRole, "Conflicting role");
            if (initiator.Id == conflicting.Id)
                throw ValidationException("SOD_ROLE_CONFLICT", "Initiator and conflicting roles must differ.");
            sod.InitiatorRoleId = initiator.Id;
            sod.InitiatorRole = initiator.Name!;
            sod.ConflictingRoleId = conflicting.Id;
            sod.ConflictingRole = conflicting.Name!;
        }
    }

    private async Task ValidateRoleLineageAsync(
        IReadOnlyList<(ProcurementPolicyRuleKind Kind, object Entity)> rules,
        ICollection<ProcurementPolicyValidationIssueDto> errors,
        CancellationToken cancellationToken)
    {
        var tenantRoles = (await _roleService.GetRolesForTenantAsync(_currentUser.TenantId, cancellationToken))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => item.Id);
        var workflowIds = rules.Select(item => item.Entity switch
            {
                ProcurementPolicyAuthorityRule authority => authority.WorkflowDefinitionId,
                ProcurementPolicyExceptionRule exception => exception.WorkflowDefinitionId,
                _ => null
            })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
        var workflowDefinitions = workflowIds.Length == 0
            ? new Dictionary<Guid, WorkflowDefinition>()
            : (await WorkflowDefinitions.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && workflowIds.Contains(item.Id))
                .Include(item => item.Steps)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id);

        void Validate(Guid? roleId, string? snapshot, string field,
            ProcurementPolicyRuleKind kind, Guid ruleId, string ruleCode)
        {
            if (!roleId.HasValue || !tenantRoles.TryGetValue(roleId.Value, out var role))
            {
                AddError(errors, "POLICY_TENANT_ROLE_REQUIRED",
                    $"{field} must identify a role assigned to an active user in the current tenant.",
                    kind, ruleId, ruleCode);
                return;
            }
            if (!string.Equals(role.Name, snapshot?.Trim(), StringComparison.Ordinal))
                AddError(errors, "POLICY_ROLE_SNAPSHOT_MISMATCH",
                    $"{field} does not match its retained role-name snapshot. Re-select the role before publishing.",
                    kind, ruleId, ruleCode);
        }

        void ValidateWorkflowRole(Guid? workflowDefinitionId, Guid? roleId, string? snapshot,
            string field, ProcurementPolicyRuleKind kind, Guid ruleId, string ruleCode)
        {
            if (!workflowDefinitionId.HasValue) return;
            if (!workflowDefinitions.TryGetValue(workflowDefinitionId.Value, out var workflow) ||
                workflow.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published || !workflow.IsActive)
            {
                AddError(errors, "WORKFLOW_REFERENCE",
                    "The selected workflow must remain active and Published in the current tenant.",
                    kind, ruleId, ruleCode);
                return;
            }

            var workflowRoles = ExtractWorkflowRoleKeys(workflow);
            if (!roleId.HasValue || !tenantRoles.TryGetValue(roleId.Value, out var role) ||
                (!workflowRoles.Contains(role.Id.ToString()) && !workflowRoles.Contains(role.Name!)))
                AddError(errors, "POLICY_ROLE_WORKFLOW_MISMATCH",
                    $"{field} '{snapshot}' is not assigned to a stage in the selected workflow.",
                    kind, ruleId, ruleCode);
        }

        foreach (var authority in rules.Where(item => item.Kind == ProcurementPolicyRuleKind.Authority && GetRuleEnabled(item.Entity))
                     .Select(item => (ProcurementPolicyAuthorityRule)item.Entity))
        {
            Validate(authority.AuthorityRoleId, authority.AuthorityRole, "Authority role",
                ProcurementPolicyRuleKind.Authority, authority.Id, authority.RuleCode);
            ValidateWorkflowRole(authority.WorkflowDefinitionId, authority.AuthorityRoleId,
                authority.AuthorityRole, "Authority role",
                ProcurementPolicyRuleKind.Authority, authority.Id, authority.RuleCode);
            if (authority.EscalationAuthorityRoleId.HasValue || !string.IsNullOrWhiteSpace(authority.EscalationAuthority))
            {
                Validate(authority.EscalationAuthorityRoleId, authority.EscalationAuthority, "Escalation authority",
                    ProcurementPolicyRuleKind.Authority, authority.Id, authority.RuleCode);
                ValidateWorkflowRole(authority.WorkflowDefinitionId, authority.EscalationAuthorityRoleId,
                    authority.EscalationAuthority, "Escalation authority",
                    ProcurementPolicyRuleKind.Authority, authority.Id, authority.RuleCode);
            }
        }

        foreach (var exception in rules.Where(item => item.Kind == ProcurementPolicyRuleKind.Exception && GetRuleEnabled(item.Entity))
                     .Select(item => (ProcurementPolicyExceptionRule)item.Entity))
        {
            Validate(exception.ApproverRoleId, exception.ApproverRole, "Exception approver role",
                ProcurementPolicyRuleKind.Exception, exception.Id, exception.RuleCode);
            ValidateWorkflowRole(exception.WorkflowDefinitionId, exception.ApproverRoleId,
                exception.ApproverRole, "Exception approver role",
                ProcurementPolicyRuleKind.Exception, exception.Id, exception.RuleCode);
        }

        foreach (var sod in rules.Where(item => item.Kind == ProcurementPolicyRuleKind.SegregationOfDuties && GetRuleEnabled(item.Entity))
                     .Select(item => (ProcurementPolicySodRule)item.Entity))
        {
            Validate(sod.InitiatorRoleId, sod.InitiatorRole, "SOD initiator role",
                ProcurementPolicyRuleKind.SegregationOfDuties, sod.Id, sod.RuleCode);
            Validate(sod.ConflictingRoleId, sod.ConflictingRole, "SOD conflicting role",
                ProcurementPolicyRuleKind.SegregationOfDuties, sod.Id, sod.RuleCode);
        }
    }

    private async Task EnsureWorkflowRolesAsync(
        Guid? workflowDefinitionId,
        IEnumerable<(string Field, ApplicationRole? Role)> roles,
        CancellationToken cancellationToken)
    {
        if (!workflowDefinitionId.HasValue) return;
        var workflowRoles = await GetWorkflowRoleKeysAsync(workflowDefinitionId.Value, cancellationToken);
        if (workflowRoles.Count == 0)
            throw ValidationException("POLICY_WORKFLOW_ROLES_REQUIRED",
                "The selected workflow has no configured stage roles. Configure and publish its approval stages first.");
        foreach (var (field, role) in roles.Where(item => item.Role is not null))
            if (!workflowRoles.Contains(role!.Id.ToString()) && !workflowRoles.Contains(role.Name!))
                throw ValidationException("POLICY_ROLE_WORKFLOW_MISMATCH",
                    $"{field} '{role.Name}' is not assigned to a stage in the selected workflow.");
    }

    private async Task<HashSet<string>> GetWorkflowRoleKeysAsync(
        Guid workflowDefinitionId,
        CancellationToken cancellationToken)
    {
        var definition = await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == workflowDefinitionId &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                item.IsActive)
            .Include(item => item.Steps)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw ValidationException("WORKFLOW_REFERENCE",
                "Workflow references must identify an active Published workflow definition in this tenant.");
        return ExtractWorkflowRoleKeys(definition);
    }

    private static HashSet<string> ExtractWorkflowRoleKeys(WorkflowDefinition definition)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in definition.Steps.Where(item => !item.IsDeleted))
        {
            AddRoleKey(result, step.RequiredRole);
            CollectRoleKeys(result, step.Configuration);
            CollectRoleKeys(result, step.AssignmentConfiguration);
        }
        return result;
    }

    private static void AddRoleKey(ISet<string> target, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) target.Add(value.Trim());
    }

    private static void CollectRoleKeys(ISet<string> target, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            using var document = JsonDocument.Parse(json);
            Visit(document.RootElement, null);
        }
        catch (JsonException)
        {
            // Invalid workflow JSON is rejected by workflow publication. A malformed legacy
            // configuration must not make unrelated policy reads fail.
        }

        void Visit(JsonElement element, string? propertyName)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject()) Visit(property.Value, property.Name);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray()) Visit(item, propertyName);
                    break;
                case JsonValueKind.String when propertyName is not null &&
                    propertyName.Contains("role", StringComparison.OrdinalIgnoreCase):
                    AddRoleKey(target, element.GetString());
                    break;
            }
        }
    }

    private static void EnsureRuleWithinPolicy(ProcurementPolicySet policySet, SaveProcurementPolicyRuleValueBase value)
    {
        if (value.EffectiveFrom < policySet.EffectiveFrom ||
            (policySet.EffectiveTo.HasValue && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value > policySet.EffectiveTo.Value)))
            throw ValidationException("RULE_OUTSIDE_POLICY", "Rule effective period must be contained by the policy effective period.");
    }

    private object CreateRuleEntity(ProcurementPolicySet policySet, ProcurementPolicyRuleKind kind, SaveProcurementPolicyRuleValueBase value)
    {
        object entity = kind switch
        {
            ProcurementPolicyRuleKind.Category => new ProcurementPolicyCategoryRule(),
            ProcurementPolicyRuleKind.Method => new ProcurementPolicyMethodRule(),
            ProcurementPolicyRuleKind.Threshold => new ProcurementPolicyThresholdRule(),
            ProcurementPolicyRuleKind.Authority => new ProcurementPolicyAuthorityRule(),
            ProcurementPolicyRuleKind.Evidence => new ProcurementPolicyEvidenceRule(),
            ProcurementPolicyRuleKind.Exception => new ProcurementPolicyExceptionRule(),
            ProcurementPolicyRuleKind.SegregationOfDuties => new ProcurementPolicySodRule(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        SetRuleIdentity(entity, policySet);
        ApplyRuleValue(entity, kind, value);
        SetCreated(entity, DateTime.UtcNow);
        return entity;
    }

    private void SetRuleIdentity(object entity, ProcurementPolicySet policySet)
    {
        switch (entity)
        {
            case ProcurementPolicyCategoryRule rule: rule.TenantId = policySet.TenantId; rule.PolicySetId = policySet.Id; break;
            case ProcurementPolicyMethodRule rule: rule.TenantId = policySet.TenantId; rule.PolicySetId = policySet.Id; break;
            case ProcurementPolicyThresholdRule rule: rule.TenantId = policySet.TenantId; rule.PolicySetId = policySet.Id; break;
            case ProcurementPolicyAuthorityRule rule: rule.TenantId = policySet.TenantId; rule.PolicySetId = policySet.Id; break;
            case ProcurementPolicyEvidenceRule rule: rule.TenantId = policySet.TenantId; rule.PolicySetId = policySet.Id; break;
            case ProcurementPolicyExceptionRule rule: rule.TenantId = policySet.TenantId; rule.PolicySetId = policySet.Id; break;
            case ProcurementPolicySodRule rule: rule.TenantId = policySet.TenantId; rule.PolicySetId = policySet.Id; break;
        }
    }

    private static void ApplyRuleValue(object entity, ProcurementPolicyRuleKind kind, SaveProcurementPolicyRuleValueBase value)
    {
        switch (kind)
        {
            case ProcurementPolicyRuleKind.Category:
                Apply((ProcurementPolicyCategoryRule)entity, (SaveProcurementPolicyCategoryRuleValue)value); break;
            case ProcurementPolicyRuleKind.Method:
                Apply((ProcurementPolicyMethodRule)entity, (SaveProcurementPolicyMethodRuleValue)value); break;
            case ProcurementPolicyRuleKind.Threshold:
                Apply((ProcurementPolicyThresholdRule)entity, (SaveProcurementPolicyThresholdRuleValue)value); break;
            case ProcurementPolicyRuleKind.Authority:
                Apply((ProcurementPolicyAuthorityRule)entity, (SaveProcurementPolicyAuthorityRuleValue)value); break;
            case ProcurementPolicyRuleKind.Evidence:
                Apply((ProcurementPolicyEvidenceRule)entity, (SaveProcurementPolicyEvidenceRuleValue)value); break;
            case ProcurementPolicyRuleKind.Exception:
                Apply((ProcurementPolicyExceptionRule)entity, (SaveProcurementPolicyExceptionRuleValue)value); break;
            case ProcurementPolicyRuleKind.SegregationOfDuties:
                Apply((ProcurementPolicySodRule)entity, (SaveProcurementPolicySodRuleValue)value); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static void ApplyCommon(dynamic entity, SaveProcurementPolicyRuleValueBase value)
    {
        entity.RuleCode = value.RuleCode;
        entity.Priority = value.Priority;
        entity.IsEnabled = value.IsEnabled;
        entity.EffectiveFrom = value.EffectiveFrom;
        entity.EffectiveTo = value.EffectiveTo;
        entity.OverrideAction = value.OverrideAction;
        entity.SourceRuleId = value.SourceRuleId;
        entity.SourceDecisionKey = value.SourceDecisionKey;
    }

    private static void Apply(ProcurementPolicyCategoryRule entity, SaveProcurementPolicyCategoryRuleValue value)
    { ApplyCommon(entity, value); entity.Name = value.Name.Trim(); entity.Category = value.Category; entity.ServiceClass = NullIfWhiteSpace(value.ServiceClass); entity.Description = NullIfWhiteSpace(value.Description); entity.RequiresSpecification = value.RequiresSpecification; entity.SpecificationTemplateCode = NullIfWhiteSpace(value.SpecificationTemplateCode); }
    private static void Apply(ProcurementPolicyMethodRule entity, SaveProcurementPolicyMethodRuleValue value)
    { ApplyCommon(entity, value); entity.Name = value.Name.Trim(); entity.Category = value.Category; entity.ServiceClass = NullIfWhiteSpace(value.ServiceClass); entity.Method = value.Method; entity.IsAllowed = value.IsAllowed; entity.RequiresCompetition = value.RequiresCompetition; entity.JustificationRequired = value.JustificationRequired; entity.MinimumQuotationCount = value.MinimumQuotationCount; entity.WorkflowDefinitionId = value.WorkflowDefinitionId; entity.ApplicabilityConditions = NullIfWhiteSpace(value.ApplicabilityConditions); }
    private static void Apply(ProcurementPolicyThresholdRule entity, SaveProcurementPolicyThresholdRuleValue value)
    { ApplyCommon(entity, value); entity.Name = value.Name.Trim(); entity.Category = value.Category; entity.ServiceClass = NullIfWhiteSpace(value.ServiceClass); entity.Method = value.Method; entity.CurrencyCode = NormalizeCurrency(value.CurrencyCode); entity.LowerBound = value.LowerBound; entity.UpperBound = value.UpperBound; entity.LowerInclusive = value.LowerInclusive; entity.UpperInclusive = value.UpperInclusive; entity.StatutoryReference = value.StatutoryReference.Trim(); }
    private static void Apply(ProcurementPolicyAuthorityRule entity, SaveProcurementPolicyAuthorityRuleValue value)
    { ApplyCommon(entity, value); entity.AuthorityName = value.AuthorityName.Trim(); entity.AuthorityRoleId = value.AuthorityRoleId; entity.AuthorityRole = value.AuthorityRole.Trim(); entity.Category = value.Category; entity.CurrencyCode = NormalizeCurrency(value.CurrencyCode); entity.LowerBound = value.LowerBound; entity.UpperBound = value.UpperBound; entity.LowerInclusive = value.LowerInclusive; entity.UpperInclusive = value.UpperInclusive; entity.Sequence = value.Sequence; entity.Quorum = value.Quorum; entity.IsObserver = value.IsObserver; entity.EscalationAuthorityRoleId = value.EscalationAuthorityRoleId; entity.EscalationAuthority = NullIfWhiteSpace(value.EscalationAuthority); entity.WorkflowDefinitionId = value.WorkflowDefinitionId; }
    private static void Apply(ProcurementPolicyEvidenceRule entity, SaveProcurementPolicyEvidenceRuleValue value)
    { ApplyCommon(entity, value); entity.EvidenceName = value.EvidenceName.Trim(); entity.Stage = value.Stage; entity.Category = value.Category; entity.Method = value.Method; entity.SharedRequirementKey = NullIfWhiteSpace(value.SharedRequirementKey); entity.IsMandatory = value.IsMandatory; entity.RequiresVerification = value.RequiresVerification; entity.MaximumAgeDays = value.MaximumAgeDays; }
    private static void Apply(ProcurementPolicyExceptionRule entity, SaveProcurementPolicyExceptionRuleValue value)
    { ApplyCommon(entity, value); entity.ExceptionName = value.ExceptionName.Trim(); entity.ExceptionType = value.ExceptionType.Trim(); entity.Category = value.Category; entity.Method = value.Method; entity.Disposition = value.Disposition; entity.JustificationRequired = value.JustificationRequired; entity.EvidenceRequired = value.EvidenceRequired; entity.PostAwardFilingRequired = value.PostAwardFilingRequired; entity.ApproverRoleId = value.ApproverRoleId; entity.ApproverRole = value.ApproverRole.Trim(); entity.WorkflowDefinitionId = value.WorkflowDefinitionId; entity.MaximumDurationDays = value.MaximumDurationDays; }
    private static void Apply(ProcurementPolicySodRule entity, SaveProcurementPolicySodRuleValue value)
    { ApplyCommon(entity, value); entity.Name = value.Name.Trim(); entity.InitiatorRoleId = value.InitiatorRoleId; entity.InitiatorRole = value.InitiatorRole.Trim(); entity.ConflictingRoleId = value.ConflictingRoleId; entity.ConflictingRole = value.ConflictingRole.Trim(); entity.EntityType = value.EntityType.Trim(); entity.Action = value.Action.Trim(); entity.Enforcement = value.Enforcement; entity.Explanation = NullIfWhiteSpace(value.Explanation); }

    private static SaveProcurementPolicyCategoryRuleValue ToValue(ProcurementPolicyCategoryRule e) => new() { RuleCode=e.RuleCode,Name=e.Name,Category=e.Category,ServiceClass=e.ServiceClass,Description=e.Description,RequiresSpecification=e.RequiresSpecification,SpecificationTemplateCode=e.SpecificationTemplateCode,Priority=e.Priority,IsEnabled=e.IsEnabled,EffectiveFrom=e.EffectiveFrom,EffectiveTo=e.EffectiveTo,OverrideAction=e.OverrideAction,SourceRuleId=e.SourceRuleId,SourceDecisionKey=e.SourceDecisionKey };
    private static SaveProcurementPolicyMethodRuleValue ToValue(ProcurementPolicyMethodRule e) => new() { RuleCode=e.RuleCode,Name=e.Name,Category=e.Category,ServiceClass=e.ServiceClass,Method=e.Method,IsAllowed=e.IsAllowed,RequiresCompetition=e.RequiresCompetition,JustificationRequired=e.JustificationRequired,MinimumQuotationCount=e.MinimumQuotationCount,WorkflowDefinitionId=e.WorkflowDefinitionId,ApplicabilityConditions=e.ApplicabilityConditions,Priority=e.Priority,IsEnabled=e.IsEnabled,EffectiveFrom=e.EffectiveFrom,EffectiveTo=e.EffectiveTo,OverrideAction=e.OverrideAction,SourceRuleId=e.SourceRuleId,SourceDecisionKey=e.SourceDecisionKey };
    private static SaveProcurementPolicyThresholdRuleValue ToValue(ProcurementPolicyThresholdRule e) => new() { RuleCode=e.RuleCode,Name=e.Name,Category=e.Category,ServiceClass=e.ServiceClass,Method=e.Method,CurrencyCode=e.CurrencyCode,LowerBound=e.LowerBound,UpperBound=e.UpperBound,LowerInclusive=e.LowerInclusive,UpperInclusive=e.UpperInclusive,StatutoryReference=e.StatutoryReference,Priority=e.Priority,IsEnabled=e.IsEnabled,EffectiveFrom=e.EffectiveFrom,EffectiveTo=e.EffectiveTo,OverrideAction=e.OverrideAction,SourceRuleId=e.SourceRuleId,SourceDecisionKey=e.SourceDecisionKey };
    private static SaveProcurementPolicyAuthorityRuleValue ToValue(ProcurementPolicyAuthorityRule e) => new() { RuleCode=e.RuleCode,AuthorityName=e.AuthorityName,AuthorityRoleId=e.AuthorityRoleId,AuthorityRole=e.AuthorityRole,Category=e.Category,CurrencyCode=e.CurrencyCode,LowerBound=e.LowerBound,UpperBound=e.UpperBound,LowerInclusive=e.LowerInclusive,UpperInclusive=e.UpperInclusive,Sequence=e.Sequence,Quorum=e.Quorum,IsObserver=e.IsObserver,EscalationAuthorityRoleId=e.EscalationAuthorityRoleId,EscalationAuthority=e.EscalationAuthority,WorkflowDefinitionId=e.WorkflowDefinitionId,Priority=e.Priority,IsEnabled=e.IsEnabled,EffectiveFrom=e.EffectiveFrom,EffectiveTo=e.EffectiveTo,OverrideAction=e.OverrideAction,SourceRuleId=e.SourceRuleId,SourceDecisionKey=e.SourceDecisionKey };
    private static SaveProcurementPolicyEvidenceRuleValue ToValue(ProcurementPolicyEvidenceRule e) => new() { RuleCode=e.RuleCode,EvidenceName=e.EvidenceName,Stage=e.Stage,Category=e.Category,Method=e.Method,SharedRequirementKey=e.SharedRequirementKey,IsMandatory=e.IsMandatory,RequiresVerification=e.RequiresVerification,MaximumAgeDays=e.MaximumAgeDays,Priority=e.Priority,IsEnabled=e.IsEnabled,EffectiveFrom=e.EffectiveFrom,EffectiveTo=e.EffectiveTo,OverrideAction=e.OverrideAction,SourceRuleId=e.SourceRuleId,SourceDecisionKey=e.SourceDecisionKey };
    private static SaveProcurementPolicyExceptionRuleValue ToValue(ProcurementPolicyExceptionRule e) => new() { RuleCode=e.RuleCode,ExceptionName=e.ExceptionName,ExceptionType=e.ExceptionType,Category=e.Category,Method=e.Method,Disposition=e.Disposition,JustificationRequired=e.JustificationRequired,EvidenceRequired=e.EvidenceRequired,PostAwardFilingRequired=e.PostAwardFilingRequired,ApproverRoleId=e.ApproverRoleId,ApproverRole=e.ApproverRole,WorkflowDefinitionId=e.WorkflowDefinitionId,MaximumDurationDays=e.MaximumDurationDays,Priority=e.Priority,IsEnabled=e.IsEnabled,EffectiveFrom=e.EffectiveFrom,EffectiveTo=e.EffectiveTo,OverrideAction=e.OverrideAction,SourceRuleId=e.SourceRuleId,SourceDecisionKey=e.SourceDecisionKey };
    private static SaveProcurementPolicySodRuleValue ToValue(ProcurementPolicySodRule e) => new() { RuleCode=e.RuleCode,Name=e.Name,InitiatorRoleId=e.InitiatorRoleId,InitiatorRole=e.InitiatorRole,ConflictingRoleId=e.ConflictingRoleId,ConflictingRole=e.ConflictingRole,EntityType=e.EntityType,Action=e.Action,Enforcement=e.Enforcement,Explanation=e.Explanation,Priority=e.Priority,IsEnabled=e.IsEnabled,EffectiveFrom=e.EffectiveFrom,EffectiveTo=e.EffectiveTo,OverrideAction=e.OverrideAction,SourceRuleId=e.SourceRuleId,SourceDecisionKey=e.SourceDecisionKey };

    private async Task AddRuleEntityAsync(object entity, ProcurementPolicyRuleKind kind)
    {
        switch (kind)
        {
            case ProcurementPolicyRuleKind.Category: await Categories.AddAsync((ProcurementPolicyCategoryRule)entity); break;
            case ProcurementPolicyRuleKind.Method: await Methods.AddAsync((ProcurementPolicyMethodRule)entity); break;
            case ProcurementPolicyRuleKind.Threshold: await Thresholds.AddAsync((ProcurementPolicyThresholdRule)entity); break;
            case ProcurementPolicyRuleKind.Authority: await Authorities.AddAsync((ProcurementPolicyAuthorityRule)entity); break;
            case ProcurementPolicyRuleKind.Evidence: await Evidence.AddAsync((ProcurementPolicyEvidenceRule)entity); break;
            case ProcurementPolicyRuleKind.Exception: await Exceptions.AddAsync((ProcurementPolicyExceptionRule)entity); break;
            case ProcurementPolicyRuleKind.SegregationOfDuties: await SodRules.AddAsync((ProcurementPolicySodRule)entity); break;
        }
    }

    private async Task UpdateRuleEntityAsync(object entity, ProcurementPolicyRuleKind kind)
    {
        switch (kind)
        {
            case ProcurementPolicyRuleKind.Category: await Categories.UpdateAsync((ProcurementPolicyCategoryRule)entity); break;
            case ProcurementPolicyRuleKind.Method: await Methods.UpdateAsync((ProcurementPolicyMethodRule)entity); break;
            case ProcurementPolicyRuleKind.Threshold: await Thresholds.UpdateAsync((ProcurementPolicyThresholdRule)entity); break;
            case ProcurementPolicyRuleKind.Authority: await Authorities.UpdateAsync((ProcurementPolicyAuthorityRule)entity); break;
            case ProcurementPolicyRuleKind.Evidence: await Evidence.UpdateAsync((ProcurementPolicyEvidenceRule)entity); break;
            case ProcurementPolicyRuleKind.Exception: await Exceptions.UpdateAsync((ProcurementPolicyExceptionRule)entity); break;
            case ProcurementPolicyRuleKind.SegregationOfDuties: await SodRules.UpdateAsync((ProcurementPolicySodRule)entity); break;
        }
    }

    private async Task<ProcurementPolicySet> FindPolicySetAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = PolicySets.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.Id == id);
        if (!tracked) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(cancellationToken)
               ?? throw new ProcurementPolicyNotFoundException("The procurement policy was not found in this tenant.");
    }

    private async Task<ProcurementConfigurationProfile> FindImmutableSourceConfigurationAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await ConfigurationProfiles.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.Id == id)
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementPolicyNotFoundException("The source configuration profile was not found in this tenant.");
        if (source.PublishedAt is null || source.LifecycleStatus == ProcurementConfigurationProfileStatus.Draft)
            throw ValidationException("SOURCE_CONFIGURATION", "Only a Published or Retired configuration version that was previously published can source executable policy rules.");
        return source;
    }

    private async Task AddRevisionAsync(Guid policySetId, Guid? ruleId, ProcurementPolicyRuleKind? kind, string action, string result,
        string correlationId, string? reason, object? before, object? after)
    {
        await Revisions.AddAsync(new ProcurementPolicyRevision
        {
            TenantId = _currentUser.TenantId, PolicySetId = policySetId, RuleId = ruleId, RuleKind = kind,
            Action = action, Result = result, CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
            ActorUserId = _currentUser.UserId, ActorName = _currentUser.FullName, ActorRoles = string.Join(",", _currentUser.Roles),
            Reason = NullIfWhiteSpace(reason), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.FullName, CreatedById = _currentUser.UserId
        });
    }

    private async Task SaveWithConcurrencyAsync(string target, CancellationToken cancellationToken)
    {
        try { await _unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ProcurementPolicyConflictException($"The {target} changed by another user. Reload before saving."); }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception)) { throw new ProcurementPolicyConflictException($"The {target} conflicts with another policy record."); }
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementPolicyAuthorizationException("An authenticated tenant context is required.");
    }

    private void EnsureEditor()
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.HasRole("SuperAdmin") && !_currentUser.HasRole("TenantAdmin"))
            throw new ProcurementPolicyAuthorizationException("Procurement policy administration requires SuperAdmin or TenantAdmin.");
    }

    private void Touch(ProcurementPolicySet policySet)
    {
        policySet.UpdatedAt = DateTime.UtcNow; policySet.UpdatedBy = _currentUser.FullName; policySet.LastModifiedById = _currentUser.UserId;
    }

    private void TouchRule(object entity)
    {
        switch (entity)
        {
            case ProcurementPolicyCategoryRule e: SetUpdated(e); break; case ProcurementPolicyMethodRule e: SetUpdated(e); break;
            case ProcurementPolicyThresholdRule e: SetUpdated(e); break; case ProcurementPolicyAuthorityRule e: SetUpdated(e); break;
            case ProcurementPolicyEvidenceRule e: SetUpdated(e); break; case ProcurementPolicyExceptionRule e: SetUpdated(e); break;
            case ProcurementPolicySodRule e: SetUpdated(e); break;
        }
    }

    private void SetUpdated(BaseEntity entity)
    { entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = _currentUser.FullName; entity.LastModifiedById = _currentUser.UserId; }
    private void SetCreated(object entity, DateTime now)
    {
        if (entity is BaseEntity baseEntity) { baseEntity.CreatedAt = now; baseEntity.CreatedBy = _currentUser.FullName; baseEntity.CreatedById = _currentUser.UserId; }
    }
    private void SoftDeleteRule(object entity)
    {
        if (entity is BaseEntity baseEntity) { baseEntity.IsDeleted = true; baseEntity.DeletedAt = DateTime.UtcNow; baseEntity.DeletedBy = _currentUser.FullName; }
    }

    private static object PolicySnapshot(ProcurementPolicySet policySet) => new { policySet.Id, policySet.PolicyKey, policySet.Code, policySet.Name, policySet.Version, policySet.LifecycleStatus, policySet.ScopeType, policySet.SourceConfigurationProfileId, policySet.BasePolicySetId, policySet.EffectiveFrom, policySet.EffectiveTo, policySet.IsDefault };
    private static object RuleSnapshot(object entity, ProcurementPolicyRuleKind kind) => new { Kind = kind, Rule = MapRule(entity, kind) };
    private static string NormalizeCode(string value) => string.IsNullOrWhiteSpace(value) ? throw ValidationException("CODE_REQUIRED", "Code is required.") : value.Trim().ToUpperInvariant();
    private static string NormalizeCurrency(string value) => string.IsNullOrWhiteSpace(value) || value.Trim().Length != 3 ? throw ValidationException("CURRENCY", "Currency must be a three-letter code.") : value.Trim().ToUpperInvariant();
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? EnsureUtc(DateTime? value) => value.HasValue ? EnsureUtc(value.Value) : null;
    private static void ValidateDates(DateTime from, DateTime? to, string target) { if (from == default) throw ValidationException("EFFECTIVE_FROM", $"{target} effective-from is required."); if (to.HasValue && to.Value < from) throw ValidationException("EFFECTIVE_PERIOD", $"{target} effective-to cannot be before effective-from."); }
    private static void EnsureRowVersion(byte[] current, string supplied, string target) { byte[] parsed; try { parsed = Convert.FromBase64String(supplied); } catch { throw new ProcurementPolicyConflictException($"The {target} row version is invalid. Reload before saving."); } if (!current.SequenceEqual(parsed)) throw new ProcurementPolicyConflictException($"The {target} changed by another user. Reload before saving."); }
    private static bool IsUniqueConstraint(DbUpdateException exception) => exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true || exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;
    private static ProcurementPolicyValidationException ValidationException(string code, string message) => new(message, new ProcurementPolicyValidationResultDto { Errors = new[] { new ProcurementPolicyValidationIssueDto { Code = code, Message = message } } });
    private static void ValidateDatesForResult(DateTime from, DateTime? to, ICollection<ProcurementPolicyValidationIssueDto> errors, string code, string message) { if (from == default || (to.HasValue && to.Value < from)) AddError(errors, code, message); }
    private static void AddError(ICollection<ProcurementPolicyValidationIssueDto> errors, string code, string message, ProcurementPolicyRuleKind? kind = null, Guid? ruleId = null, string? ruleCode = null) => errors.Add(new ProcurementPolicyValidationIssueDto { Code = code, Message = message, RuleKind = kind, RuleId = ruleId, RuleCode = ruleCode });
    private static string RuleKindLabel(ProcurementPolicyRuleKind kind) => kind == ProcurementPolicyRuleKind.SegregationOfDuties ? "segregation-of-duties" : kind.ToString().ToLowerInvariant();
    private static string RequiredRuleFamilyMessage(ProcurementPolicyRuleKind kind) => kind switch
    {
        ProcurementPolicyRuleKind.Evidence =>
            "Add at least one enabled Evidence rule so document and verification requirements are explicit.",
        ProcurementPolicyRuleKind.Exception =>
            "Add at least one enabled Exception rule. Use a Prohibited disposition when no exception is allowed, so the policy fails closed.",
        ProcurementPolicyRuleKind.SegregationOfDuties =>
            "Apply the six required TDC segregation-of-duties controls so incompatible actors remain separated.",
        _ => $"At least one enabled {RuleKindLabel(kind)} rule is required."
    };
    private static string DisplayServiceClass(string? value) => string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
    private static bool ServiceClassMatches(string? left, string? right) => string.Equals(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    private static bool DateRangesOverlap(DateTime leftFrom, DateTime? leftTo, DateTime rightFrom, DateTime? rightTo) => leftFrom <= (rightTo ?? DateTime.MaxValue) && rightFrom <= (leftTo ?? DateTime.MaxValue);
    private static bool AmountRangesOverlap(ProcurementPolicyThresholdRule left, ProcurementPolicyThresholdRule right)
    {
        var leftUpper = left.UpperBound ?? decimal.MaxValue; var rightUpper = right.UpperBound ?? decimal.MaxValue;
        if (leftUpper < right.LowerBound || rightUpper < left.LowerBound) return false;
        if (leftUpper == right.LowerBound) return left.UpperInclusive && right.LowerInclusive;
        if (rightUpper == left.LowerBound) return right.UpperInclusive && left.LowerInclusive;
        return true;
    }
    private static JsonElement? ParseOptionalJson(string? json) { if (string.IsNullOrWhiteSpace(json)) return null; using var document = JsonDocument.Parse(json); return document.RootElement.Clone(); }

    private static Guid GetRuleId(object e) => e switch { ProcurementPolicyCategoryRule x=>x.Id,ProcurementPolicyMethodRule x=>x.Id,ProcurementPolicyThresholdRule x=>x.Id,ProcurementPolicyAuthorityRule x=>x.Id,ProcurementPolicyEvidenceRule x=>x.Id,ProcurementPolicyExceptionRule x=>x.Id,ProcurementPolicySodRule x=>x.Id,_=>Guid.Empty };
    private static string GetRuleCode(object e) => e switch { ProcurementPolicyCategoryRule x=>x.RuleCode,ProcurementPolicyMethodRule x=>x.RuleCode,ProcurementPolicyThresholdRule x=>x.RuleCode,ProcurementPolicyAuthorityRule x=>x.RuleCode,ProcurementPolicyEvidenceRule x=>x.RuleCode,ProcurementPolicyExceptionRule x=>x.RuleCode,ProcurementPolicySodRule x=>x.RuleCode,_=>string.Empty };
    private static string GetRuleName(object e) => e switch { ProcurementPolicyCategoryRule x=>x.Name,ProcurementPolicyMethodRule x=>x.Name,ProcurementPolicyThresholdRule x=>x.Name,ProcurementPolicyAuthorityRule x=>x.AuthorityName,ProcurementPolicyEvidenceRule x=>x.EvidenceName,ProcurementPolicyExceptionRule x=>x.ExceptionName,ProcurementPolicySodRule x=>x.Name,_=>string.Empty };
    private static int GetRulePriority(object e) => e switch { ProcurementPolicyCategoryRule x=>x.Priority,ProcurementPolicyMethodRule x=>x.Priority,ProcurementPolicyThresholdRule x=>x.Priority,ProcurementPolicyAuthorityRule x=>x.Priority,ProcurementPolicyEvidenceRule x=>x.Priority,ProcurementPolicyExceptionRule x=>x.Priority,ProcurementPolicySodRule x=>x.Priority,_=>0 };
    private static bool GetRuleEnabled(object e) => e switch { ProcurementPolicyCategoryRule x=>x.IsEnabled,ProcurementPolicyMethodRule x=>x.IsEnabled,ProcurementPolicyThresholdRule x=>x.IsEnabled,ProcurementPolicyAuthorityRule x=>x.IsEnabled,ProcurementPolicyEvidenceRule x=>x.IsEnabled,ProcurementPolicyExceptionRule x=>x.IsEnabled,ProcurementPolicySodRule x=>x.IsEnabled,_=>false };
    private static DateTime GetRuleEffectiveFrom(object e) => e switch { ProcurementPolicyCategoryRule x=>x.EffectiveFrom,ProcurementPolicyMethodRule x=>x.EffectiveFrom,ProcurementPolicyThresholdRule x=>x.EffectiveFrom,ProcurementPolicyAuthorityRule x=>x.EffectiveFrom,ProcurementPolicyEvidenceRule x=>x.EffectiveFrom,ProcurementPolicyExceptionRule x=>x.EffectiveFrom,ProcurementPolicySodRule x=>x.EffectiveFrom,_=>default };
    private static DateTime? GetRuleEffectiveTo(object e) => e switch { ProcurementPolicyCategoryRule x=>x.EffectiveTo,ProcurementPolicyMethodRule x=>x.EffectiveTo,ProcurementPolicyThresholdRule x=>x.EffectiveTo,ProcurementPolicyAuthorityRule x=>x.EffectiveTo,ProcurementPolicyEvidenceRule x=>x.EffectiveTo,ProcurementPolicyExceptionRule x=>x.EffectiveTo,ProcurementPolicySodRule x=>x.EffectiveTo,_=>null };
    private static ProcurementPolicyOverrideAction GetRuleOverrideAction(object e) => e switch { ProcurementPolicyCategoryRule x=>x.OverrideAction,ProcurementPolicyMethodRule x=>x.OverrideAction,ProcurementPolicyThresholdRule x=>x.OverrideAction,ProcurementPolicyAuthorityRule x=>x.OverrideAction,ProcurementPolicyEvidenceRule x=>x.OverrideAction,ProcurementPolicyExceptionRule x=>x.OverrideAction,ProcurementPolicySodRule x=>x.OverrideAction,_=>ProcurementPolicyOverrideAction.Add };
    private static Guid? GetRuleSourceRuleId(object e) => e switch { ProcurementPolicyCategoryRule x=>x.SourceRuleId,ProcurementPolicyMethodRule x=>x.SourceRuleId,ProcurementPolicyThresholdRule x=>x.SourceRuleId,ProcurementPolicyAuthorityRule x=>x.SourceRuleId,ProcurementPolicyEvidenceRule x=>x.SourceRuleId,ProcurementPolicyExceptionRule x=>x.SourceRuleId,ProcurementPolicySodRule x=>x.SourceRuleId,_=>null };
    private static string GetRuleSourceDecisionKey(object e) => e switch { ProcurementPolicyCategoryRule x=>x.SourceDecisionKey,ProcurementPolicyMethodRule x=>x.SourceDecisionKey,ProcurementPolicyThresholdRule x=>x.SourceDecisionKey,ProcurementPolicyAuthorityRule x=>x.SourceDecisionKey,ProcurementPolicyEvidenceRule x=>x.SourceDecisionKey,ProcurementPolicyExceptionRule x=>x.SourceDecisionKey,ProcurementPolicySodRule x=>x.SourceDecisionKey,_=>string.Empty };
    private static byte[] GetRuleRowVersion(object e) => e switch { ProcurementPolicyCategoryRule x=>x.RowVersion,ProcurementPolicyMethodRule x=>x.RowVersion,ProcurementPolicyThresholdRule x=>x.RowVersion,ProcurementPolicyAuthorityRule x=>x.RowVersion,ProcurementPolicyEvidenceRule x=>x.RowVersion,ProcurementPolicyExceptionRule x=>x.RowVersion,ProcurementPolicySodRule x=>x.RowVersion,_=>Array.Empty<byte>() };
}
