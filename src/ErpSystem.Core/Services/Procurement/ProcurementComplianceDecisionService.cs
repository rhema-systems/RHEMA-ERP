using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementComplianceDecisionService : IProcurementComplianceDecisionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<ProcurementComplianceDecisionService> _logger;

    public ProcurementComplianceDecisionService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<ProcurementComplianceDecisionService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
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
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions => _unitOfWork.Repository<WorkflowDefinition>();

    public async Task<IReadOnlyList<ProcurementCompliancePolicyOptionDto>> GetEffectivePolicyOptionsAsync(
        DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var moment = EnsureUtc(atUtc);
        return await EffectivePolicyQuery(moment)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Code)
            .ThenByDescending(item => item.Version)
            .Select(item => new ProcurementCompliancePolicyOptionDto
            {
                PolicySetId = item.Id,
                PolicyCode = item.Code,
                PolicyName = item.Name,
                Version = item.Version,
                ScopeType = item.ScopeType,
                CurrencyCode = item.DefaultCurrencyCode,
                EffectiveFrom = item.EffectiveFrom,
                EffectiveTo = item.EffectiveTo,
                IsDefault = item.IsDefault
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProcurementAuthorityRouteDecisionDto> EvaluateAuthorityRouteAsync(
        ProcurementAuthorityRouteDecisionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        ValidateAuthorityRequest(request);
        var moment = EnsureUtc(request.AtUtc ?? DateTime.UtcNow);
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var normalizedCorrelation = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId.Trim();
        var evaluationId = Guid.NewGuid();
        PolicySelection selectedPolicy;
        try
        {
            selectedPolicy = await ResolvePolicyAsync(new ProcurementComplianceDecisionRequest
            {
                PolicySetId = request.PolicySetId,
                PolicyCode = request.PolicyCode,
                Category = request.Category,
                Amount = request.Amount,
                CurrencyCode = currency,
                AtUtc = moment,
                SourceType = request.SourceType,
                SourceReference = request.SourceReference,
                EntityType = "PurchaseRequisition",
                Action = "Submit"
            }, moment, cancellationToken);
        }
        catch (ProcurementCompliancePolicyNotFoundException exception)
        {
            return AuthorityBlocked(evaluationId, moment, normalizedCorrelation, request, currency,
                "PR_AUTHORITY_POLICY_NOT_EFFECTIVE", exception.Message,
                "Publish one approved, effective procurement policy for this tenant and submission date.");
        }
        catch (ProcurementCompliancePolicyConflictException exception)
        {
            return AuthorityBlocked(evaluationId, moment, normalizedCorrelation, request, currency,
                "PR_AUTHORITY_POLICY_AMBIGUOUS", exception.Message,
                "Resolve overlapping/default policy publication so exactly one effective policy is selected.");
        }

        var policyDto = MapPolicy(selectedPolicy.Policy, selectedPolicy.Reason);
        ResolvedRuleBundle rules;
        try
        {
            rules = await LoadResolvedRulesAsync(selectedPolicy.Policy, moment, cancellationToken);
        }
        catch (ProcurementCompliancePolicyConflictException exception)
        {
            return AuthorityBlocked(evaluationId, moment, normalizedCorrelation, request, currency,
                "PR_AUTHORITY_POLICY_LINEAGE_INVALID", exception.Message,
                "Correct the immutable tenant-override/base-policy lineage and publish a valid replacement.", policyDto);
        }

        var categoryRules = rules.Authorities
            .Where(item => !item.Rule.Category.HasValue || item.Rule.Category == request.Category)
            .ToList();
        var currencyRules = categoryRules
            .Where(item => CurrencyMatches(item.Rule.CurrencyCode, currency))
            .ToList();
        var matches = currencyRules
            .Where(item => AmountMatches(request.Amount, item.Rule.LowerBound, item.Rule.UpperBound,
                item.Rule.LowerInclusive, item.Rule.UpperInclusive))
            .OrderBy(item => item.Rule.Sequence)
            .ThenByDescending(item => item.Rule.Priority)
            .ThenBy(item => item.Rule.RuleCode)
            .ToList();

        if (matches.Count == 0)
        {
            if (categoryRules.Any(item => AmountMatches(request.Amount, item.Rule.LowerBound, item.Rule.UpperBound,
                    item.Rule.LowerInclusive, item.Rule.UpperInclusive)))
            {
                return AuthorityBlocked(evaluationId, moment, normalizedCorrelation, request, currency,
                    "PR_AUTHORITY_CURRENCY_MISMATCH",
                    $"Authority coverage exists for {request.Category} and {request.Amount:N2}, but not in {currency}.",
                    $"Configure and publish a {currency} authority band for this category and amount.", policyDto);
            }

            var code = currencyRules.Count > 0 ? "PR_AUTHORITY_COVERAGE_GAP" : "PR_AUTHORITY_NOT_CONFIGURED";
            var message = currencyRules.Count > 0
                ? $"The effective authority matrix has no band covering {request.Amount:N2} {currency} for {request.Category}."
                : $"The effective policy has no authority rules for {request.Category} in {currency}.";
            return AuthorityBlocked(evaluationId, moment, normalizedCorrelation, request, currency, code, message,
                "Add a non-overlapping effective authority band with an approved shared-workflow reference.", policyDto);
        }

        var overlappingSequences = matches.GroupBy(item => item.Rule.Sequence)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .ToList();
        if (overlappingSequences.Count > 0)
        {
            var details = string.Join(", ", overlappingSequences.Select(group =>
                $"sequence {group.Key}: {string.Join("/", group.Select(item => item.Rule.RuleCode))}"));
            return AuthorityBlocked(evaluationId, moment, normalizedCorrelation, request, currency,
                "PR_AUTHORITY_ROUTE_AMBIGUOUS",
                $"Multiple effective authority rules match the same route sequence ({details}).",
                "Retire or replace overlapping authority rules so each route sequence resolves once.", policyDto);
        }

        if (matches.Any(item => !item.Rule.WorkflowDefinitionId.HasValue))
        {
            return AuthorityBlocked(evaluationId, moment, normalizedCorrelation, request, currency,
                "PR_AUTHORITY_WORKFLOW_REQUIRED",
                "Every matched authority stage must reference the shared workflow definition selected for this route.",
                "Assign one Published Purchase Requisition workflow definition to every matched authority stage.", policyDto);
        }

        var workflowIds = matches.Select(item => item.Rule.WorkflowDefinitionId!.Value).Distinct().ToList();
        if (workflowIds.Count != 1)
        {
            return AuthorityBlocked(evaluationId, moment, normalizedCorrelation, request, currency,
                "PR_AUTHORITY_WORKFLOW_AMBIGUOUS",
                $"The matched authority route references {workflowIds.Count} different workflow definitions.",
                "Configure every authority stage in the route to use one immutable Published workflow version.", policyDto);
        }

        var workflow = await WorkflowDefinitions.GetQueryable(item =>
                item.Id == workflowIds[0] && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.EntityType)
            .Include(item => item.Steps)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (workflow is null || !WorkflowDefinitionLifecyclePolicy.IsRuntimeEligible(workflow) ||
            workflow.EntityType is null || !IsPurchaseRequisitionEntityType(workflow.EntityType))
        {
            return AuthorityBlocked(evaluationId, moment, normalizedCorrelation, request, currency,
                "PR_AUTHORITY_WORKFLOW_INVALID",
                "The configured workflow is missing, foreign, inactive, unpublished, or not a Purchase Requisition workflow.",
                "Publish an active Purchase Requisition workflow and reference that exact version from the authority rules.", policyDto);
        }

        var findings = new List<ProcurementComplianceFindingDto>();
        var resolvedSteps = new List<ProcurementAuthorityRouteStepDecisionDto>();
        foreach (var match in matches)
        {
            var roleSteps = workflow.Steps
                .Where(step => !step.IsDeleted && StepContainsRole(step, match.Rule.AuthorityRole))
                .OrderBy(step => step.Order)
                .ThenBy(step => step.Id)
                .ToList();
            if (roleSteps.Count == 0)
            {
                findings.Add(Finding("PR_AUTHORITY_WORKFLOW_STAGE_MISSING",
                    $"Workflow '{workflow.Name}' has no stage assigned to authority role '{match.Rule.AuthorityRole}'.",
                    ProcurementComplianceFindingSeverity.HardStop, match.Rule.Id, match.Rule.RuleCode,
                    ProcurementPolicyRuleKind.Authority, match.Rule.SourceDecisionKey));
                continue;
            }
            if (roleSteps.Count > 1)
            {
                findings.Add(Finding("PR_AUTHORITY_WORKFLOW_STAGE_AMBIGUOUS",
                    $"Workflow '{workflow.Name}' assigns authority role '{match.Rule.AuthorityRole}' to multiple stages.",
                    ProcurementComplianceFindingSeverity.HardStop, match.Rule.Id, match.Rule.RuleCode,
                    ProcurementPolicyRuleKind.Authority, match.Rule.SourceDecisionKey));
                continue;
            }

            var workflowStep = roleSteps[0];
            if (!match.Rule.IsObserver && workflowStep.StepType != WorkflowStepType.Approval)
            {
                findings.Add(Finding("PR_AUTHORITY_WORKFLOW_STAGE_INVALID",
                    $"Authority role '{match.Rule.AuthorityRole}' must map to an Approval workflow stage.",
                    ProcurementComplianceFindingSeverity.HardStop, match.Rule.Id, match.Rule.RuleCode,
                    ProcurementPolicyRuleKind.Authority, match.Rule.SourceDecisionKey));
                continue;
            }

            if (!match.Rule.IsObserver)
            {
                var approval = ReadApprovalConfiguration(workflowStep);
                if (approval is null || approval.MinApprovalsRequired < match.Rule.Quorum)
                {
                    findings.Add(Finding("PR_AUTHORITY_WORKFLOW_QUORUM_MISMATCH",
                        $"Workflow stage '{workflowStep.Name}' requires fewer than the configured quorum of {match.Rule.Quorum}.",
                        ProcurementComplianceFindingSeverity.HardStop, match.Rule.Id, match.Rule.RuleCode,
                        ProcurementPolicyRuleKind.Authority, match.Rule.SourceDecisionKey));
                    continue;
                }
                if (!approval.PreventInitiatorApproval)
                {
                    findings.Add(Finding("PR_AUTHORITY_WORKFLOW_SOD_INCOMPLETE",
                        $"Workflow stage '{workflowStep.Name}' does not prevent the requisition initiator from approving.",
                        ProcurementComplianceFindingSeverity.HardStop, match.Rule.Id, match.Rule.RuleCode,
                        ProcurementPolicyRuleKind.Authority, "DEC-004"));
                    continue;
                }
            }

            resolvedSteps.Add(new ProcurementAuthorityRouteStepDecisionDto
            {
                Sequence = match.Rule.Sequence,
                RuleId = match.Rule.Id,
                RulePolicySetId = match.Owner.Id,
                RulePolicyCode = match.Owner.Code,
                RulePolicyVersion = match.Owner.Version,
                RuleCode = match.Rule.RuleCode,
                SourceRuleId = match.Rule.SourceRuleId,
                SourceDecisionKey = match.Rule.SourceDecisionKey,
                AuthorityName = match.Rule.AuthorityName,
                AuthorityRole = match.Rule.AuthorityRole,
                CurrencyCode = match.Rule.CurrencyCode,
                LowerBound = match.Rule.LowerBound,
                UpperBound = match.Rule.UpperBound,
                LowerInclusive = match.Rule.LowerInclusive,
                UpperInclusive = match.Rule.UpperInclusive,
                Quorum = match.Rule.Quorum,
                IsObserver = match.Rule.IsObserver,
                EscalationAuthority = match.Rule.EscalationAuthority,
                WorkflowDefinitionId = workflow.Id,
                WorkflowStepId = workflowStep.Id,
                WorkflowStepName = workflowStep.Name,
                WorkflowStepOrder = workflowStep.Order
            });
        }

        if (findings.Count == 0 && resolvedSteps.Select(item => item.WorkflowStepId).Distinct().Count() != resolvedSteps.Count)
            findings.Add(Finding("PR_AUTHORITY_WORKFLOW_SEQUENCE_MISMATCH",
                "Multiple ordered authority stages resolve to the same workflow stage."));
        if (findings.Count == 0)
        {
            var workflowOrders = resolvedSteps.OrderBy(item => item.Sequence).Select(item => item.WorkflowStepOrder).ToArray();
            if (!workflowOrders.SequenceEqual(workflowOrders.OrderBy(item => item)))
                findings.Add(Finding("PR_AUTHORITY_WORKFLOW_SEQUENCE_MISMATCH",
                    "Authority sequence order does not match the configured shared-workflow stage order."));
        }

        if (findings.Count > 0)
        {
            var first = findings[0];
            return new ProcurementAuthorityRouteDecisionDto
            {
                EvaluationId = evaluationId,
                EvaluatedAtUtc = DateTime.UtcNow,
                PolicyDateUtc = moment,
                CorrelationId = normalizedCorrelation,
                IsReady = false,
                DecisionCode = first.Code,
                Message = first.Message,
                Policy = policyDto,
                Category = request.Category,
                Amount = request.Amount,
                CurrencyCode = currency,
                Workflow = MapWorkflow(workflow),
                Steps = resolvedSteps,
                Findings = findings,
                RequiredActions = ["Align workflow roles, order, quorum, and initiator/approver separation with the effective authority route, then publish a replacement workflow/policy version."]
            };
        }

        return new ProcurementAuthorityRouteDecisionDto
        {
            EvaluationId = evaluationId,
            EvaluatedAtUtc = DateTime.UtcNow,
            PolicyDateUtc = moment,
            CorrelationId = normalizedCorrelation,
            IsReady = true,
            DecisionCode = "PR_AUTHORITY_ROUTE_READY",
            Message = $"Resolved {resolvedSteps.Count} authority stage(s) through workflow '{workflow.Name}' v{workflow.Version}.",
            Policy = policyDto,
            Category = request.Category,
            Amount = request.Amount,
            CurrencyCode = currency,
            Workflow = MapWorkflow(workflow),
            Steps = resolvedSteps,
            Findings = Array.Empty<ProcurementComplianceFindingDto>(),
            RequiredActions = Array.Empty<string>()
        };
    }

    public async Task<ProcurementComplianceDecisionDto> EvaluateAsync(
        ProcurementComplianceDecisionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        ValidateRequest(request);
        var moment = EnsureUtc(request.AtUtc ?? DateTime.UtcNow);
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var serviceClass = NullIfWhiteSpace(request.ServiceClass);
        var sourceType = request.SourceType.Trim();
        var sourceReference = request.SourceReference.Trim();
        var selectedPolicy = await ResolvePolicyAsync(request, moment, cancellationToken);
        var rules = await LoadResolvedRulesAsync(selectedPolicy.Policy, moment, cancellationToken);

        var hardStops = new List<ProcurementComplianceFindingDto>();
        var reviewRequirements = new List<ProcurementComplianceFindingDto>();
        var warnings = new List<ProcurementComplianceFindingDto>();
        var matchedRules = new List<ProcurementComplianceRuleReferenceDto>();
        var trace = new List<ProcurementComplianceTraceStepDto>();
        var route = new List<ProcurementComplianceRouteStepDto>();

        var categoryMatches = rules.Categories
            .Where(item => item.Rule.Category == request.Category && ServiceClassMatches(item.Rule.ServiceClass, serviceClass))
            .OrderByDescending(item => item.Rule.Priority)
            .ThenBy(item => item.Rule.RuleCode)
            .ToList();
        if (categoryMatches.Count == 0)
        {
            hardStops.Add(Finding("CATEGORY_NOT_CONFIGURED",
                $"No effective category rule covers {request.Category}{ServiceSuffix(serviceClass)}."));
        }
        foreach (var item in categoryMatches)
            AddMatched(matchedRules, Reference(item, ProcurementPolicyRuleKind.Category, item.Rule.RuleCode, item.Rule.Name,
                item.Rule.SourceDecisionKey, item.Rule.SourceRuleId, item.Rule.OverrideAction,
                $"Category {request.Category}{ServiceSuffix(serviceClass)} matched."));
        var categoryRequirements = categoryMatches.Select(item => new ProcurementComplianceCategoryRequirementDto
        {
            RuleId = item.Rule.Id,
            RuleCode = item.Rule.RuleCode,
            Name = item.Rule.Name,
            RequiresSpecification = item.Rule.RequiresSpecification,
            SpecificationTemplateCode = item.Rule.SpecificationTemplateCode,
            SourceDecisionKey = item.Rule.SourceDecisionKey
        }).ToList();
        trace.Add(Trace(1, "Category", categoryMatches.Count == 0
            ? "No category rule matched."
            : $"Matched {categoryMatches.Count} category rule(s).", categoryMatches.Select(item => item.Rule.RuleCode)));

        var methodEvaluation = EvaluateMethods(rules, request, serviceClass, currency);
        hardStops.AddRange(methodEvaluation.Findings);
        if (methodEvaluation.Selected is not null)
        {
            var selected = methodEvaluation.Selected;
            AddMatched(matchedRules, Reference(selected.MethodRule, ProcurementPolicyRuleKind.Method,
                selected.MethodRule.Rule.RuleCode, selected.MethodRule.Rule.Name, selected.MethodRule.Rule.SourceDecisionKey,
                selected.MethodRule.Rule.SourceRuleId, selected.MethodRule.Rule.OverrideAction,
                request.RequestedMethod.HasValue ? "Requested method is allowed by the effective policy." : "Highest-priority viable method was selected."));
            if (selected.ThresholdRule is not null)
                AddMatched(matchedRules, Reference(selected.ThresholdRule, ProcurementPolicyRuleKind.Threshold,
                    selected.ThresholdRule.Rule.RuleCode, selected.ThresholdRule.Rule.Name, selected.ThresholdRule.Rule.SourceDecisionKey,
                    selected.ThresholdRule.Rule.SourceRuleId, selected.ThresholdRule.Rule.OverrideAction,
                    $"Amount {request.Amount:N2} {currency} matched the configured threshold band."));

            if (selected.MethodRule.Rule.WorkflowDefinitionId.HasValue)
            {
                route.Add(new ProcurementComplianceRouteStepDto
                {
                    StepType = ProcurementComplianceRouteStepType.MethodWorkflow,
                    Sequence = 0,
                    Name = selected.MethodRule.Rule.Name,
                    ResponsibleRole = "Configured workflow",
                    WorkflowDefinitionId = selected.MethodRule.Rule.WorkflowDefinitionId,
                    RuleId = selected.MethodRule.Rule.Id,
                    RuleCode = selected.MethodRule.Rule.RuleCode,
                    SourceDecisionKey = selected.MethodRule.Rule.SourceDecisionKey
                });
            }
        }
        trace.Add(Trace(2, "Method and threshold", methodEvaluation.TraceResult,
            methodEvaluation.Selected is null
                ? methodEvaluation.Candidates.Select(item => item.MethodRule.Rule.RuleCode)
                : new[] { methodEvaluation.Selected.MethodRule.Rule.RuleCode, methodEvaluation.Selected.ThresholdRule?.Rule.RuleCode }
                    .Where(item => item is not null)!));

        var selectedMethod = methodEvaluation.Selected?.MethodRule.Rule.Method;
        var authorityMatches = rules.Authorities
            .Where(item => (!item.Rule.Category.HasValue || item.Rule.Category == request.Category) &&
                           CurrencyMatches(item.Rule.CurrencyCode, currency) &&
                           AmountMatches(request.Amount, item.Rule.LowerBound, item.Rule.UpperBound,
                               item.Rule.LowerInclusive, item.Rule.UpperInclusive))
            .OrderBy(item => item.Rule.Sequence)
            .ThenByDescending(item => item.Rule.Priority)
            .ThenBy(item => item.Rule.RuleCode)
            .ToList();
        if (authorityMatches.Count == 0)
        {
            hardStops.Add(Finding("AUTHORITY_NOT_CONFIGURED",
                $"No effective approval authority covers {request.Amount:N2} {currency} for {request.Category}."));
        }
        else
        {
            reviewRequirements.Add(Finding("AUTHORITY_ROUTE_REQUIRED",
                $"The decision must follow {authorityMatches.Count} configured authority step(s).",
                ProcurementComplianceFindingSeverity.Information));
        }
        var requiredAuthorities = authorityMatches.Select(item =>
        {
            AddMatched(matchedRules, Reference(item, ProcurementPolicyRuleKind.Authority,
                item.Rule.RuleCode, item.Rule.AuthorityName, item.Rule.SourceDecisionKey, item.Rule.SourceRuleId,
                item.Rule.OverrideAction, $"Authority amount band matched {request.Amount:N2} {currency}."));
            route.Add(new ProcurementComplianceRouteStepDto
            {
                StepType = ProcurementComplianceRouteStepType.Authority,
                Sequence = item.Rule.Sequence,
                Name = item.Rule.AuthorityName,
                ResponsibleRole = item.Rule.AuthorityRole,
                Quorum = item.Rule.Quorum,
                WorkflowDefinitionId = item.Rule.WorkflowDefinitionId,
                EscalationAuthority = item.Rule.EscalationAuthority,
                RuleId = item.Rule.Id,
                RuleCode = item.Rule.RuleCode,
                SourceDecisionKey = item.Rule.SourceDecisionKey
            });
            return new ProcurementComplianceAuthorityDto
            {
                RuleId = item.Rule.Id,
                RuleCode = item.Rule.RuleCode,
                AuthorityName = item.Rule.AuthorityName,
                AuthorityRole = item.Rule.AuthorityRole,
                Sequence = item.Rule.Sequence,
                Quorum = item.Rule.Quorum,
                IsObserver = item.Rule.IsObserver,
                EscalationAuthority = item.Rule.EscalationAuthority,
                WorkflowDefinitionId = item.Rule.WorkflowDefinitionId,
                SourceDecisionKey = item.Rule.SourceDecisionKey
            };
        }).ToList();
        trace.Add(Trace(3, "Authority route", authorityMatches.Count == 0
            ? "No approval authority matched."
            : $"Resolved {authorityMatches.Count} ordered authority step(s).", authorityMatches.Select(item => item.Rule.RuleCode)));

        var suppliedEvidence = NormalizeSet(request.EvidenceReferenceKeys);
        var evidenceMatches = rules.Evidence
            .Where(item => (!item.Rule.Category.HasValue || item.Rule.Category == request.Category) &&
                           (!item.Rule.Method.HasValue || item.Rule.Method == selectedMethod) &&
                           (!request.EvidenceStage.HasValue || item.Rule.Stage == request.EvidenceStage.Value))
            .OrderBy(item => item.Rule.Stage)
            .ThenByDescending(item => item.Rule.Priority)
            .ThenBy(item => item.Rule.RuleCode)
            .ToList();
        var requiredEvidence = new List<ProcurementComplianceEvidenceDto>();
        foreach (var item in evidenceMatches)
        {
            var requirementKey = NullIfWhiteSpace(item.Rule.SharedRequirementKey) ?? item.Rule.RuleCode;
            requiredEvidence.Add(new ProcurementComplianceEvidenceDto
            {
                RuleId = item.Rule.Id,
                RuleCode = item.Rule.RuleCode,
                EvidenceName = item.Rule.EvidenceName,
                Stage = item.Rule.Stage,
                SharedRequirementKey = item.Rule.SharedRequirementKey,
                IsMandatory = item.Rule.IsMandatory,
                RequiresVerification = item.Rule.RequiresVerification,
                MaximumAgeDays = item.Rule.MaximumAgeDays,
                SourceDecisionKey = item.Rule.SourceDecisionKey
            });
            AddMatched(matchedRules, Reference(item, ProcurementPolicyRuleKind.Evidence,
                item.Rule.RuleCode, item.Rule.EvidenceName, item.Rule.SourceDecisionKey, item.Rule.SourceRuleId,
                item.Rule.OverrideAction, "Category/method evidence applicability matched."));
            if (item.Rule.IsMandatory && !suppliedEvidence.Contains(requirementKey))
            {
                hardStops.Add(Finding("MANDATORY_EVIDENCE_MISSING",
                    $"Evidence '{item.Rule.EvidenceName}' is required using shared requirement key '{requirementKey}'.",
                    ProcurementComplianceFindingSeverity.HardStop, item.Rule.Id, item.Rule.RuleCode,
                    ProcurementPolicyRuleKind.Evidence, item.Rule.SourceDecisionKey));
            }
            else if (item.Rule.RequiresVerification && suppliedEvidence.Contains(requirementKey))
            {
                reviewRequirements.Add(Finding("EVIDENCE_VERIFICATION_REQUIRED",
                    $"Evidence '{item.Rule.EvidenceName}' must be verified through the shared evidence controls.",
                    ProcurementComplianceFindingSeverity.Information, item.Rule.Id, item.Rule.RuleCode,
                    ProcurementPolicyRuleKind.Evidence, item.Rule.SourceDecisionKey));
            }
        }
        trace.Add(Trace(4, "Evidence", evidenceMatches.Count == 0
            ? "No evidence rule applied."
            : $"Resolved {evidenceMatches.Count} evidence requirement(s); {hardStops.Count(item => item.Code == "MANDATORY_EVIDENCE_MISSING")} mandatory item(s) are missing.",
            evidenceMatches.Select(item => item.Rule.RuleCode)));

        var exceptionMatches = rules.Exceptions
            .Where(item => (!item.Rule.Category.HasValue || item.Rule.Category == request.Category) &&
                           (!item.Rule.Method.HasValue || item.Rule.Method == selectedMethod))
            .OrderByDescending(item => item.Rule.Priority)
            .ThenBy(item => item.Rule.RuleCode)
            .ToList();
        var applicableExceptions = exceptionMatches.Select(MapException).ToList();
        ProcurementComplianceExceptionDto? selectedException = null;
        if (!string.IsNullOrWhiteSpace(request.ExceptionType))
        {
            var requestedExceptions = exceptionMatches
                .Where(item => string.Equals(item.Rule.ExceptionType, request.ExceptionType.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (requestedExceptions.Count == 0)
            {
                hardStops.Add(Finding("EXCEPTION_NOT_CONFIGURED",
                    $"Exception type '{request.ExceptionType.Trim()}' is not configured for this decision."));
            }
            else
            {
                var highestPriority = requestedExceptions.Max(item => item.Rule.Priority);
                var top = requestedExceptions.Where(item => item.Rule.Priority == highestPriority).ToList();
                if (top.Count > 1)
                {
                    hardStops.Add(Finding("EXCEPTION_RULE_AMBIGUOUS",
                        $"Exception type '{request.ExceptionType.Trim()}' matched {top.Count} rules at the same priority."));
                }
                else
                {
                    var item = top[0];
                    selectedException = MapException(item);
                    AddMatched(matchedRules, Reference(item, ProcurementPolicyRuleKind.Exception,
                        item.Rule.RuleCode, item.Rule.ExceptionName, item.Rule.SourceDecisionKey, item.Rule.SourceRuleId,
                        item.Rule.OverrideAction, $"Requested exception type '{request.ExceptionType.Trim()}' matched."));
                    ApplyExceptionFindings(item.Rule, request, hardStops, reviewRequirements, suppliedEvidence);
                    if (item.Rule.Disposition == ProcurementExceptionDisposition.ApprovalRequired)
                    {
                        route.Add(new ProcurementComplianceRouteStepDto
                        {
                            StepType = ProcurementComplianceRouteStepType.ExceptionApproval,
                            Sequence = route.Count == 0 ? 1 : route.Max(step => step.Sequence) + 1,
                            Name = item.Rule.ExceptionName,
                            ResponsibleRole = item.Rule.ApproverRole,
                            WorkflowDefinitionId = item.Rule.WorkflowDefinitionId,
                            RuleId = item.Rule.Id,
                            RuleCode = item.Rule.RuleCode,
                            SourceDecisionKey = item.Rule.SourceDecisionKey
                        });
                    }
                }
            }
        }
        trace.Add(Trace(5, "Exception", selectedException is not null
            ? $"Evaluated requested exception '{selectedException.ExceptionType}' as {selectedException.Disposition}."
            : $"Returned {applicableExceptions.Count} applicable exception option(s); no exception was requested.",
            selectedException is null ? Array.Empty<string>() : new[] { selectedException.RuleCode }));

        var actorUserId = request.ActorUserId.GetValueOrDefault(_currentUser.UserId);
        if (actorUserId == Guid.Empty) actorUserId = _currentUser.UserId;
        var actorRoles = NormalizeRoles(request.ActorRoles.Count == 0 ? _currentUser.Roles : request.ActorRoles);
        var sourceOwnerUserId = request.SourceOwnerUserId.GetValueOrDefault(actorUserId);
        var sourceOwnerRoles = NormalizeRoles(request.SourceOwnerRoles.Count == 0 ? actorRoles : request.SourceOwnerRoles);
        var sodMatches = rules.SodRules
            .Where(item => string.Equals(item.Rule.EntityType, request.EntityType.Trim(), StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(item.Rule.Action, request.Action.Trim(), StringComparison.OrdinalIgnoreCase) &&
                           actorUserId == sourceOwnerUserId &&
                           actorRoles.Contains(item.Rule.ConflictingRole) &&
                           sourceOwnerRoles.Contains(item.Rule.InitiatorRole))
            .OrderByDescending(item => item.Rule.Priority)
            .ThenBy(item => item.Rule.RuleCode)
            .ToList();
        foreach (var item in sodMatches)
        {
            AddMatched(matchedRules, Reference(item, ProcurementPolicyRuleKind.SegregationOfDuties,
                item.Rule.RuleCode, item.Rule.Name, item.Rule.SourceDecisionKey, item.Rule.SourceRuleId,
                item.Rule.OverrideAction,
                $"Actor is the source owner and holds conflicting role '{item.Rule.ConflictingRole}'."));
            var message = item.Rule.Explanation ??
                          $"Role '{item.Rule.InitiatorRole}' conflicts with '{item.Rule.ConflictingRole}' for {item.Rule.Action}.";
            switch (item.Rule.Enforcement)
            {
                case ProcurementSodEnforcement.HardStop:
                    hardStops.Add(Finding("SOD_CONFLICT", message, ProcurementComplianceFindingSeverity.HardStop,
                        item.Rule.Id, item.Rule.RuleCode, ProcurementPolicyRuleKind.SegregationOfDuties,
                        item.Rule.SourceDecisionKey));
                    break;
                case ProcurementSodEnforcement.ApprovalRequired:
                    reviewRequirements.Add(Finding("SOD_REVIEW_REQUIRED", message,
                        ProcurementComplianceFindingSeverity.Warning, item.Rule.Id, item.Rule.RuleCode,
                        ProcurementPolicyRuleKind.SegregationOfDuties, item.Rule.SourceDecisionKey));
                    break;
                default:
                    warnings.Add(Finding("SOD_WARNING", message, ProcurementComplianceFindingSeverity.Warning,
                        item.Rule.Id, item.Rule.RuleCode, ProcurementPolicyRuleKind.SegregationOfDuties,
                        item.Rule.SourceDecisionKey));
                    break;
            }
        }
        trace.Add(Trace(6, "Segregation of duties", sodMatches.Count == 0
            ? "No declarative SOD conflict matched the simulated actor/source context."
            : $"Matched {sodMatches.Count} declarative SOD rule(s). Runtime consumers must invoke the reusable SOD guard before performing the protected action.",
            sodMatches.Select(item => item.Rule.RuleCode)));

        var outcome = hardStops.Count > 0
            ? ProcurementComplianceOutcome.Blocked
            : reviewRequirements.Count > 0
                ? ProcurementComplianceOutcome.ReviewRequired
                : ProcurementComplianceOutcome.Allowed;
        trace.Add(Trace(7, "Outcome", outcome switch
        {
            ProcurementComplianceOutcome.Blocked => $"Blocked by {hardStops.Count} hard stop(s).",
            ProcurementComplianceOutcome.ReviewRequired => $"May proceed into {reviewRequirements.Count} required review(s).",
            _ => "Allowed with no hard stop or review requirement."
        }, matchedRules.Select(item => item.RuleCode)));

        var decision = new ProcurementComplianceDecisionDto
        {
            EvaluationId = Guid.NewGuid(),
            EvaluatedAtUtc = DateTime.UtcNow,
            PolicyDateUtc = moment,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
            EvaluationOnly = true,
            Outcome = outcome,
            Policy = MapPolicy(selectedPolicy.Policy, selectedPolicy.Reason),
            Category = request.Category,
            ServiceClass = serviceClass,
            Amount = request.Amount,
            CurrencyCode = currency,
            SourceType = sourceType,
            SourceReference = sourceReference,
            ActorUserId = actorUserId,
            ActorRoles = actorRoles.OrderBy(item => item).ToArray(),
            RequestedMethod = request.RequestedMethod,
            SelectedMethod = selectedMethod,
            MethodCandidates = methodEvaluation.Candidates.Select(MapMethodCandidate).ToList(),
            CategoryRequirements = categoryRequirements,
            RequiredAuthorities = requiredAuthorities,
            RequiredEvidence = requiredEvidence,
            Route = route.OrderBy(item => item.Sequence).ThenBy(item => item.StepType).ToList(),
            ApplicableExceptions = applicableExceptions,
            SelectedException = selectedException,
            HardStops = hardStops,
            ReviewRequirements = reviewRequirements,
            Warnings = warnings,
            MatchedRules = matchedRules.OrderBy(item => item.RuleKind).ThenBy(item => item.RuleCode).ToList(),
            Trace = trace
        };

        _logger.LogInformation(
            "Procurement compliance evaluation {EvaluationId} resolved policy {PolicyCode}/v{Version} with outcome {Outcome}, {MatchedRules} matched rules, and {HardStops} hard stops. CorrelationId={CorrelationId}",
            decision.EvaluationId, decision.Policy.PolicyCode, decision.Policy.Version, decision.Outcome,
            decision.MatchedRules.Count, decision.HardStops.Count, decision.CorrelationId);
        return decision;
    }

    private async Task<PolicySelection> ResolvePolicyAsync(
        ProcurementComplianceDecisionRequest request,
        DateTime moment,
        CancellationToken cancellationToken)
    {
        var effective = EffectivePolicyQuery(moment);
        if (request.PolicySetId.HasValue)
        {
            var policy = await effective.SingleOrDefaultAsync(item => item.Id == request.PolicySetId.Value, cancellationToken);
            if (policy is null)
                throw new ProcurementCompliancePolicyNotFoundException("The selected policy is not Published and effective for this tenant and date.");
            if (!string.IsNullOrWhiteSpace(request.PolicyCode) &&
                !string.Equals(policy.Code, request.PolicyCode.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ProcurementComplianceRequestValidationException("POLICY_SELECTION_MISMATCH",
                    "PolicySetId and PolicyCode must identify the same effective policy.");
            return new PolicySelection(policy, "Selected by immutable policy version ID.");
        }

        if (!string.IsNullOrWhiteSpace(request.PolicyCode))
        {
            var code = request.PolicyCode.Trim().ToUpperInvariant();
            var candidates = await effective.Where(item => item.Code == code)
                .OrderByDescending(item => item.Version)
                .ToListAsync(cancellationToken);
            if (candidates.Count == 0)
                throw new ProcurementCompliancePolicyNotFoundException($"No Published, effective policy with code '{code}' exists for this tenant and date.");
            if (candidates.Count > 1)
                throw new ProcurementCompliancePolicyConflictException($"Policy code '{code}' resolves to multiple effective versions.");
            return new PolicySelection(candidates[0], "Selected by policy code.");
        }

        var defaults = await effective.Where(item => item.IsDefault).ToListAsync(cancellationToken);
        if (defaults.Count == 1) return new PolicySelection(defaults[0], "Selected as the tenant's default effective policy.");
        if (defaults.Count > 1)
            throw new ProcurementCompliancePolicyConflictException("Multiple default Published policies are effective. Select a policy explicitly.");

        var all = await effective.ToListAsync(cancellationToken);
        if (all.Count == 1) return new PolicySelection(all[0], "Selected as the tenant's only effective policy.");
        if (all.Count == 0)
            throw new ProcurementCompliancePolicyNotFoundException("No Published procurement policy is effective for this tenant and date.");
        throw new ProcurementCompliancePolicyConflictException("Multiple Published policies are effective and none is the default. Select a policy explicitly.");
    }

    private IQueryable<ProcurementPolicySet> EffectivePolicyQuery(DateTime moment) =>
        PolicySets.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published &&
                item.EffectiveFrom <= moment &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking();

    private async Task<ResolvedRuleBundle> LoadResolvedRulesAsync(
        ProcurementPolicySet selected,
        DateTime moment,
        CancellationToken cancellationToken)
    {
        ProcurementPolicySet? basePolicy = null;
        if (selected.ScopeType == ProcurementPolicyScopeType.TenantOverride)
        {
            if (!selected.BasePolicySetId.HasValue)
                throw new ProcurementCompliancePolicyConflictException("The selected tenant override has no immutable base policy reference.");
            basePolicy = await PolicySets.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.Id == selected.BasePolicySetId.Value)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (basePolicy is null || basePolicy.LifecycleStatus == ProcurementPolicyLifecycleStatus.Draft)
                throw new ProcurementCompliancePolicyConflictException("The selected tenant override references a missing or editable base policy.");
        }

        var baseLayers = basePolicy is null
            ? ResolvedRuleBundle.Empty
            : await LoadPolicyRulesAsync(basePolicy, moment, cancellationToken);
        var selectedLayers = await LoadPolicyRulesAsync(selected, moment, cancellationToken);
        if (basePolicy is null) return selectedLayers.WithoutDisableInstructions();

        return new ResolvedRuleBundle(
            Merge(baseLayers.Categories, selectedLayers.Categories, item => item.Rule.Id, item => item.Rule.OverrideAction, item => item.Rule.SourceRuleId),
            Merge(baseLayers.Methods, selectedLayers.Methods, item => item.Rule.Id, item => item.Rule.OverrideAction, item => item.Rule.SourceRuleId),
            Merge(baseLayers.Thresholds, selectedLayers.Thresholds, item => item.Rule.Id, item => item.Rule.OverrideAction, item => item.Rule.SourceRuleId),
            Merge(baseLayers.Authorities, selectedLayers.Authorities, item => item.Rule.Id, item => item.Rule.OverrideAction, item => item.Rule.SourceRuleId),
            Merge(baseLayers.Evidence, selectedLayers.Evidence, item => item.Rule.Id, item => item.Rule.OverrideAction, item => item.Rule.SourceRuleId),
            Merge(baseLayers.Exceptions, selectedLayers.Exceptions, item => item.Rule.Id, item => item.Rule.OverrideAction, item => item.Rule.SourceRuleId),
            Merge(baseLayers.SodRules, selectedLayers.SodRules, item => item.Rule.Id, item => item.Rule.OverrideAction, item => item.Rule.SourceRuleId));
    }

    private async Task<ResolvedRuleBundle> LoadPolicyRulesAsync(
        ProcurementPolicySet owner,
        DateTime moment,
        CancellationToken cancellationToken)
    {
        var categories = await Categories.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == owner.Id &&
                item.IsEnabled && item.EffectiveFrom <= moment && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking().ToListAsync(cancellationToken);
        var methods = await Methods.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == owner.Id &&
                item.IsEnabled && item.EffectiveFrom <= moment && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking().ToListAsync(cancellationToken);
        var thresholds = await Thresholds.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == owner.Id &&
                item.IsEnabled && item.EffectiveFrom <= moment && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking().ToListAsync(cancellationToken);
        var authorities = await Authorities.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == owner.Id &&
                item.IsEnabled && item.EffectiveFrom <= moment && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking().ToListAsync(cancellationToken);
        var evidence = await Evidence.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == owner.Id &&
                item.IsEnabled && item.EffectiveFrom <= moment && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking().ToListAsync(cancellationToken);
        var exceptions = await Exceptions.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == owner.Id &&
                item.IsEnabled && item.EffectiveFrom <= moment && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking().ToListAsync(cancellationToken);
        var sodRules = await SodRules.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.PolicySetId == owner.Id &&
                item.IsEnabled && item.EffectiveFrom <= moment && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= moment))
            .AsNoTracking().ToListAsync(cancellationToken);

        return new ResolvedRuleBundle(
            categories.Select(item => new OwnedRule<ProcurementPolicyCategoryRule>(item, owner)).ToList(),
            methods.Select(item => new OwnedRule<ProcurementPolicyMethodRule>(item, owner)).ToList(),
            thresholds.Select(item => new OwnedRule<ProcurementPolicyThresholdRule>(item, owner)).ToList(),
            authorities.Select(item => new OwnedRule<ProcurementPolicyAuthorityRule>(item, owner)).ToList(),
            evidence.Select(item => new OwnedRule<ProcurementPolicyEvidenceRule>(item, owner)).ToList(),
            exceptions.Select(item => new OwnedRule<ProcurementPolicyExceptionRule>(item, owner)).ToList(),
            sodRules.Select(item => new OwnedRule<ProcurementPolicySodRule>(item, owner)).ToList());
    }

    private static IReadOnlyList<OwnedRule<T>> Merge<T>(
        IReadOnlyList<OwnedRule<T>> baseRules,
        IReadOnlyList<OwnedRule<T>> overrideRules,
        Func<OwnedRule<T>, Guid> id,
        Func<OwnedRule<T>, ProcurementPolicyOverrideAction> action,
        Func<OwnedRule<T>, Guid?> sourceRuleId)
    {
        var resolved = baseRules.ToDictionary(id);
        foreach (var item in overrideRules.OrderBy(item => action(item)).ThenBy(id))
        {
            var sourceId = sourceRuleId(item);
            if (action(item) is ProcurementPolicyOverrideAction.Replace or ProcurementPolicyOverrideAction.Disable)
            {
                if (sourceId.HasValue) resolved.Remove(sourceId.Value);
                if (action(item) == ProcurementPolicyOverrideAction.Disable) continue;
            }
            resolved[id(item)] = item;
        }
        return resolved.Values.ToList();
    }

    private static MethodEvaluation EvaluateMethods(
        ResolvedRuleBundle rules,
        ProcurementComplianceDecisionRequest request,
        string? serviceClass,
        string currency)
    {
        var applicableMethods = rules.Methods
            .Where(item => item.Rule.Category == request.Category && ServiceClassMatches(item.Rule.ServiceClass, serviceClass))
            .GroupBy(item => item.Rule.Method)
            .Select(group => group.OrderByDescending(item => item.Rule.Priority).ThenBy(item => item.Rule.RuleCode).First())
            .ToList();
        var candidates = new List<MethodCandidate>();
        foreach (var method in applicableMethods)
        {
            var thresholds = rules.Thresholds
                .Where(item => item.Rule.Category == request.Category &&
                               ServiceClassMatches(item.Rule.ServiceClass, serviceClass) &&
                               item.Rule.Method == method.Rule.Method &&
                               CurrencyMatches(item.Rule.CurrencyCode, currency) &&
                               AmountMatches(request.Amount, item.Rule.LowerBound, item.Rule.UpperBound,
                                   item.Rule.LowerInclusive, item.Rule.UpperInclusive))
                .OrderByDescending(item => item.Rule.Priority)
                .ThenBy(item => item.Rule.RuleCode)
                .ToList();
            candidates.Add(new MethodCandidate(method, thresholds.FirstOrDefault(), thresholds.Count,
                Math.Max(method.Rule.Priority, thresholds.FirstOrDefault()?.Rule.Priority ?? 0)));
        }

        var findings = new List<ProcurementComplianceFindingDto>();
        MethodCandidate? selected = null;
        if (request.RequestedMethod.HasValue)
        {
            var requested = candidates.SingleOrDefault(item => item.MethodRule.Rule.Method == request.RequestedMethod.Value);
            if (requested is null)
                findings.Add(Finding("METHOD_NOT_CONFIGURED", $"Requested method {request.RequestedMethod.Value} is not configured for this category."));
            else if (!requested.MethodRule.Rule.IsAllowed)
                findings.Add(Finding("METHOD_PROHIBITED", $"Requested method {request.RequestedMethod.Value} is prohibited by rule {requested.MethodRule.Rule.RuleCode}.",
                    ProcurementComplianceFindingSeverity.HardStop, requested.MethodRule.Rule.Id, requested.MethodRule.Rule.RuleCode,
                    ProcurementPolicyRuleKind.Method, requested.MethodRule.Rule.SourceDecisionKey));
            else if (requested.ThresholdMatchCount == 0)
                findings.Add(Finding("THRESHOLD_NOT_CONFIGURED", $"No threshold band covers {request.Amount:N2} {currency} for {request.RequestedMethod.Value}."));
            else if (requested.ThresholdMatchCount > 1)
                findings.Add(Finding("THRESHOLD_AMBIGUOUS", $"{requested.ThresholdMatchCount} threshold bands cover {request.Amount:N2} {currency} for {request.RequestedMethod.Value}."));
            else
                selected = requested;
        }
        else
        {
            var viable = candidates.Where(item => item.MethodRule.Rule.IsAllowed && item.ThresholdMatchCount == 1)
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.MethodRule.Rule.Method)
                .ToList();
            if (viable.Count == 0)
            {
                findings.Add(Finding("METHOD_NOT_RESOLVED", $"No allowed method has a threshold covering {request.Amount:N2} {currency}."));
            }
            else
            {
                var topPriority = viable[0].Priority;
                var top = viable.Where(item => item.Priority == topPriority).ToList();
                if (top.Count > 1)
                    findings.Add(Finding("METHOD_SELECTION_AMBIGUOUS",
                        $"{top.Count} methods are equally preferred for {request.Amount:N2} {currency}; select a method explicitly."));
                else
                    selected = top[0];
            }
        }

        var traceResult = selected is not null
            ? $"Resolved {selected.MethodRule.Rule.Method} using method rule {selected.MethodRule.Rule.RuleCode} and threshold {selected.ThresholdRule?.Rule.RuleCode}."
            : findings.FirstOrDefault()?.Message ?? "No method was selected.";
        return new MethodEvaluation(candidates, selected, findings, traceResult);
    }

    private static void ApplyExceptionFindings(
        ProcurementPolicyExceptionRule rule,
        ProcurementComplianceDecisionRequest request,
        ICollection<ProcurementComplianceFindingDto> hardStops,
        ICollection<ProcurementComplianceFindingDto> reviews,
        IReadOnlySet<string> suppliedEvidence)
    {
        if (rule.Disposition == ProcurementExceptionDisposition.Prohibited)
            hardStops.Add(Finding("EXCEPTION_PROHIBITED", $"Exception '{rule.ExceptionName}' is prohibited.",
                ProcurementComplianceFindingSeverity.HardStop, rule.Id, rule.RuleCode,
                ProcurementPolicyRuleKind.Exception, rule.SourceDecisionKey));
        if (rule.JustificationRequired && !request.JustificationProvided)
            hardStops.Add(Finding("EXCEPTION_JUSTIFICATION_REQUIRED", $"Exception '{rule.ExceptionName}' requires justification.",
                ProcurementComplianceFindingSeverity.HardStop, rule.Id, rule.RuleCode,
                ProcurementPolicyRuleKind.Exception, rule.SourceDecisionKey));
        if (rule.EvidenceRequired && suppliedEvidence.Count == 0)
            hardStops.Add(Finding("EXCEPTION_EVIDENCE_REQUIRED", $"Exception '{rule.ExceptionName}' requires evidence.",
                ProcurementComplianceFindingSeverity.HardStop, rule.Id, rule.RuleCode,
                ProcurementPolicyRuleKind.Exception, rule.SourceDecisionKey));
        if (rule.Disposition == ProcurementExceptionDisposition.ApprovalRequired)
        {
            if (string.IsNullOrWhiteSpace(request.ExceptionApprovalReference))
                hardStops.Add(Finding("EXCEPTION_APPROVAL_REQUIRED",
                    $"Exception '{rule.ExceptionName}' requires approval by {rule.ApproverRole}.",
                    ProcurementComplianceFindingSeverity.HardStop, rule.Id, rule.RuleCode,
                    ProcurementPolicyRuleKind.Exception, rule.SourceDecisionKey));
            else
                reviews.Add(Finding("EXCEPTION_APPROVAL_REFERENCE_REVIEW",
                    $"Approval reference '{request.ExceptionApprovalReference.Trim()}' must be verified through the shared workflow controls.",
                    ProcurementComplianceFindingSeverity.Information, rule.Id, rule.RuleCode,
                    ProcurementPolicyRuleKind.Exception, rule.SourceDecisionKey));
        }
    }

    private static ProcurementComplianceMethodCandidateDto MapMethodCandidate(MethodCandidate item) => new()
    {
        Method = item.MethodRule.Rule.Method,
        IsAllowed = item.MethodRule.Rule.IsAllowed,
        RequiresCompetition = item.MethodRule.Rule.RequiresCompetition,
        MinimumQuotationCount = item.MethodRule.Rule.MinimumQuotationCount,
        WorkflowDefinitionId = item.MethodRule.Rule.WorkflowDefinitionId,
        ApplicabilityConditions = item.MethodRule.Rule.ApplicabilityConditions,
        MethodRuleCode = item.MethodRule.Rule.RuleCode,
        ThresholdRuleCode = item.ThresholdRule?.Rule.RuleCode,
        StatutoryReference = item.ThresholdRule?.Rule.StatutoryReference,
        Priority = item.Priority,
        MatchesAmount = item.ThresholdMatchCount == 1,
        Explanation = !item.MethodRule.Rule.IsAllowed
            ? "Method is explicitly prohibited."
            : item.ThresholdMatchCount == 0
                ? "Method has no threshold covering the evaluated amount."
                : item.ThresholdMatchCount > 1
                    ? "Method has ambiguous threshold coverage."
                    : "Method is allowed and its threshold covers the evaluated amount."
    };

    private static ProcurementComplianceExceptionDto MapException(OwnedRule<ProcurementPolicyExceptionRule> item) => new()
    {
        RuleId = item.Rule.Id,
        RuleCode = item.Rule.RuleCode,
        ExceptionName = item.Rule.ExceptionName,
        ExceptionType = item.Rule.ExceptionType,
        Disposition = item.Rule.Disposition,
        JustificationRequired = item.Rule.JustificationRequired,
        EvidenceRequired = item.Rule.EvidenceRequired,
        PostAwardFilingRequired = item.Rule.PostAwardFilingRequired,
        ApproverRole = item.Rule.ApproverRole,
        WorkflowDefinitionId = item.Rule.WorkflowDefinitionId,
        MaximumDurationDays = item.Rule.MaximumDurationDays,
        SourceDecisionKey = item.Rule.SourceDecisionKey
    };

    private static ProcurementCompliancePolicySelectionDto MapPolicy(ProcurementPolicySet policy, string reason) => new()
    {
        PolicySetId = policy.Id,
        PolicyKey = policy.PolicyKey,
        PolicyCode = policy.Code,
        PolicyName = policy.Name,
        Version = policy.Version,
        ScopeType = policy.ScopeType,
        CurrencyCode = policy.DefaultCurrencyCode,
        EffectiveFrom = policy.EffectiveFrom,
        EffectiveTo = policy.EffectiveTo,
        IsDefault = policy.IsDefault,
        SourceConfigurationProfileId = policy.SourceConfigurationProfileId,
        BasePolicySetId = policy.BasePolicySetId,
        SelectionReason = reason
    };

    private static ProcurementComplianceRuleReferenceDto Reference<T>(
        OwnedRule<T> item,
        ProcurementPolicyRuleKind kind,
        string ruleCode,
        string name,
        string sourceDecisionKey,
        Guid? sourceRuleId,
        ProcurementPolicyOverrideAction overrideAction,
        string reason) where T : class => new()
    {
        PolicySetId = item.Owner.Id,
        PolicyCode = item.Owner.Code,
        PolicyVersion = item.Owner.Version,
        RuleId = item.Rule switch
        {
            ProcurementPolicyCategoryRule value => value.Id,
            ProcurementPolicyMethodRule value => value.Id,
            ProcurementPolicyThresholdRule value => value.Id,
            ProcurementPolicyAuthorityRule value => value.Id,
            ProcurementPolicyEvidenceRule value => value.Id,
            ProcurementPolicyExceptionRule value => value.Id,
            ProcurementPolicySodRule value => value.Id,
            _ => Guid.Empty
        },
        RuleKind = kind,
        RuleCode = ruleCode,
        RuleName = name,
        SourceDecisionKey = sourceDecisionKey,
        SourceRuleId = sourceRuleId,
        OverrideAction = overrideAction,
        MatchReason = reason
    };

    private static void AddMatched(
        ICollection<ProcurementComplianceRuleReferenceDto> destination,
        ProcurementComplianceRuleReferenceDto reference)
    {
        if (destination.All(item => item.RuleId != reference.RuleId)) destination.Add(reference);
    }

    private static ProcurementComplianceFindingDto Finding(
        string code,
        string message,
        ProcurementComplianceFindingSeverity severity = ProcurementComplianceFindingSeverity.HardStop,
        Guid? ruleId = null,
        string? ruleCode = null,
        ProcurementPolicyRuleKind? ruleKind = null,
        string? sourceDecisionKey = null) => new()
    {
        Code = code,
        Message = message,
        Severity = severity,
        RuleId = ruleId,
        RuleCode = ruleCode,
        RuleKind = ruleKind,
        SourceDecisionKey = sourceDecisionKey
    };

    private static ProcurementComplianceTraceStepDto Trace(
        int sequence,
        string stage,
        string result,
        IEnumerable<string?> ruleCodes) => new()
    {
        Sequence = sequence,
        Stage = stage,
        Result = result,
        RuleCodes = ruleCodes.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct().ToArray()
    };

    private static bool AmountMatches(decimal amount, decimal lower, decimal? upper, bool lowerInclusive, bool upperInclusive)
    {
        var lowerMatches = lowerInclusive ? amount >= lower : amount > lower;
        var upperMatches = !upper.HasValue || (upperInclusive ? amount <= upper.Value : amount < upper.Value);
        return lowerMatches && upperMatches;
    }

    private static bool ServiceClassMatches(string? ruleValue, string? requested) =>
        string.IsNullOrWhiteSpace(ruleValue) ||
        (!string.IsNullOrWhiteSpace(requested) && string.Equals(ruleValue.Trim(), requested.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool CurrencyMatches(string ruleValue, string requested) =>
        string.Equals(ruleValue.Trim(), requested, StringComparison.OrdinalIgnoreCase);

    private static string ServiceSuffix(string? serviceClass) =>
        string.IsNullOrWhiteSpace(serviceClass) ? string.Empty : $" / {serviceClass}";

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProcurementAuthorityRouteDecisionDto AuthorityBlocked(
        Guid evaluationId,
        DateTime moment,
        string correlationId,
        ProcurementAuthorityRouteDecisionRequest request,
        string currency,
        string code,
        string message,
        string requiredAction,
        ProcurementCompliancePolicySelectionDto? policy = null) => new()
    {
        EvaluationId = evaluationId,
        EvaluatedAtUtc = DateTime.UtcNow,
        PolicyDateUtc = moment,
        CorrelationId = correlationId,
        IsReady = false,
        DecisionCode = code,
        Message = message,
        Policy = policy,
        Category = request.Category,
        Amount = request.Amount,
        CurrencyCode = currency,
        Findings = [Finding(code, message)],
        RequiredActions = [requiredAction]
    };

    private static ProcurementAuthorityWorkflowSelectionDto MapWorkflow(WorkflowDefinition workflow) => new()
    {
        WorkflowDefinitionId = workflow.Id,
        DefinitionKey = workflow.DefinitionKey,
        Name = workflow.Name,
        Version = workflow.Version,
        EntityTypeCode = workflow.EntityType.Code,
        EntityTypeName = workflow.EntityType.Name,
        PublishedAt = workflow.PublishedAt
    };

    private static bool IsPurchaseRequisitionEntityType(WorkflowEntityType entityType) =>
        EqualsNormalized(entityType.Code, "PURCHASE_REQUISITION") ||
        EqualsNormalized(entityType.Code, "PurchaseRequisition") ||
        EqualsNormalized(entityType.Name, "Purchase Requisition") ||
        EqualsNormalized(entityType.Name, "PurchaseRequisition");

    private static bool StepContainsRole(WorkflowStep step, string authorityRole)
    {
        if (EqualsNormalized(step.RequiredRole, authorityRole)) return true;
        var config = ReadStepConfiguration(step);
        if (config?.ApprovalConfig?.ApproverRules.Any(rule => EqualsNormalized(rule.Role, authorityRole)) == true)
            return true;
        return JsonContainsRole(step.AssignmentConfiguration, authorityRole);
    }

    private static WorkflowApprovalConfigDto? ReadApprovalConfiguration(WorkflowStep step) =>
        ReadStepConfiguration(step)?.ApprovalConfig;

    private static WorkflowStepConfigurationDto? ReadStepConfiguration(WorkflowStep step)
    {
        if (string.IsNullOrWhiteSpace(step.Configuration)) return null;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(step.Configuration, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool JsonContainsRole(string? json, string role)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return ContainsRole(document.RootElement, role);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ContainsRole(JsonElement element, string role)
    {
        if (element.ValueKind == JsonValueKind.String)
            return EqualsNormalized(element.GetString(), role);
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Any(item => ContainsRole(item, role));
        if (element.ValueKind == JsonValueKind.Object)
            return element.EnumerateObject().Any(property => ContainsRole(property.Value, role));
        return false;
    }

    private static bool EqualsNormalized(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static HashSet<string> NormalizeSet(IEnumerable<string> values) =>
        values.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> NormalizeRoles(IEnumerable<string> values) => NormalizeSet(values);

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementCompliancePolicyNotFoundException("An authenticated tenant context is required.");
    }

    private static void ValidateRequest(ProcurementComplianceDecisionRequest request)
    {
        if (request.Amount < 0)
            throw new ProcurementComplianceRequestValidationException("AMOUNT_INVALID", "Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Trim().Length != 3 ||
            !request.CurrencyCode.Trim().All(char.IsLetter))
            throw new ProcurementComplianceRequestValidationException("CURRENCY_INVALID", "CurrencyCode must contain exactly three letters.");
        if (string.IsNullOrWhiteSpace(request.SourceType))
            throw new ProcurementComplianceRequestValidationException("SOURCE_TYPE_REQUIRED", "SourceType is required.");
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            throw new ProcurementComplianceRequestValidationException("SOURCE_REFERENCE_REQUIRED", "SourceReference is required.");
        if (string.IsNullOrWhiteSpace(request.EntityType))
            throw new ProcurementComplianceRequestValidationException("ENTITY_TYPE_REQUIRED", "EntityType is required.");
        if (string.IsNullOrWhiteSpace(request.Action))
            throw new ProcurementComplianceRequestValidationException("ACTION_REQUIRED", "Action is required.");
    }

    private static void ValidateAuthorityRequest(ProcurementAuthorityRouteDecisionRequest request)
    {
        if (request.Amount < 0)
            throw new ProcurementComplianceRequestValidationException("AMOUNT_INVALID", "Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Trim().Length != 3 ||
            !request.CurrencyCode.Trim().All(char.IsLetter))
            throw new ProcurementComplianceRequestValidationException("CURRENCY_INVALID", "CurrencyCode must contain exactly three letters.");
        if (string.IsNullOrWhiteSpace(request.SourceType))
            throw new ProcurementComplianceRequestValidationException("SOURCE_TYPE_REQUIRED", "SourceType is required.");
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            throw new ProcurementComplianceRequestValidationException("SOURCE_REFERENCE_REQUIRED", "SourceReference is required.");
    }

    private sealed record PolicySelection(ProcurementPolicySet Policy, string Reason);
    private sealed record OwnedRule<T>(T Rule, ProcurementPolicySet Owner);
    private sealed record MethodCandidate(
        OwnedRule<ProcurementPolicyMethodRule> MethodRule,
        OwnedRule<ProcurementPolicyThresholdRule>? ThresholdRule,
        int ThresholdMatchCount,
        int Priority);
    private sealed record MethodEvaluation(
        IReadOnlyList<MethodCandidate> Candidates,
        MethodCandidate? Selected,
        IReadOnlyList<ProcurementComplianceFindingDto> Findings,
        string TraceResult);

    private sealed record ResolvedRuleBundle(
        IReadOnlyList<OwnedRule<ProcurementPolicyCategoryRule>> Categories,
        IReadOnlyList<OwnedRule<ProcurementPolicyMethodRule>> Methods,
        IReadOnlyList<OwnedRule<ProcurementPolicyThresholdRule>> Thresholds,
        IReadOnlyList<OwnedRule<ProcurementPolicyAuthorityRule>> Authorities,
        IReadOnlyList<OwnedRule<ProcurementPolicyEvidenceRule>> Evidence,
        IReadOnlyList<OwnedRule<ProcurementPolicyExceptionRule>> Exceptions,
        IReadOnlyList<OwnedRule<ProcurementPolicySodRule>> SodRules)
    {
        public static readonly ResolvedRuleBundle Empty = new(
            Array.Empty<OwnedRule<ProcurementPolicyCategoryRule>>(),
            Array.Empty<OwnedRule<ProcurementPolicyMethodRule>>(),
            Array.Empty<OwnedRule<ProcurementPolicyThresholdRule>>(),
            Array.Empty<OwnedRule<ProcurementPolicyAuthorityRule>>(),
            Array.Empty<OwnedRule<ProcurementPolicyEvidenceRule>>(),
            Array.Empty<OwnedRule<ProcurementPolicyExceptionRule>>(),
            Array.Empty<OwnedRule<ProcurementPolicySodRule>>());

        public ResolvedRuleBundle WithoutDisableInstructions() => new(
            Categories.Where(item => item.Rule.OverrideAction != ProcurementPolicyOverrideAction.Disable).ToList(),
            Methods.Where(item => item.Rule.OverrideAction != ProcurementPolicyOverrideAction.Disable).ToList(),
            Thresholds.Where(item => item.Rule.OverrideAction != ProcurementPolicyOverrideAction.Disable).ToList(),
            Authorities.Where(item => item.Rule.OverrideAction != ProcurementPolicyOverrideAction.Disable).ToList(),
            Evidence.Where(item => item.Rule.OverrideAction != ProcurementPolicyOverrideAction.Disable).ToList(),
            Exceptions.Where(item => item.Rule.OverrideAction != ProcurementPolicyOverrideAction.Disable).ToList(),
            SodRules.Where(item => item.Rule.OverrideAction != ProcurementPolicyOverrideAction.Disable).ToList());
    }
}
