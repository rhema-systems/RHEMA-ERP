using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementPurchaseOrderComplianceService :
    IProcurementPurchaseOrderComplianceService
{
    private const string EventType = "ProcurementPurchaseOrderCompliance";
    private const string SourceType = "PurchaseOrder";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList();
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementPurchaseOrderSourceService _sources;
    private readonly IProcurementRequisitionBudgetControlService _budgetControl;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly IProcurementGhanepsExchangeService _ghaneps;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;
    private readonly ILogger<ProcurementPurchaseOrderComplianceService> _logger;

    public ProcurementPurchaseOrderComplianceService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementPurchaseOrderSourceService sources,
        IProcurementRequisitionBudgetControlService budgetControl,
        ISupplierValidationService supplierValidation,
        IProcurementGhanepsExchangeService ghaneps,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications,
        ILogger<ProcurementPurchaseOrderComplianceService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sources = sources;
        _budgetControl = budgetControl;
        _supplierValidation = supplierValidation;
        _ghaneps = ghaneps;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ProcurementPurchaseOrderComplianceDto> GetReadinessAsync(
        Guid purchaseOrderId,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var purchaseOrder = await _unitOfWork.Repository<PurchaseOrder>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == purchaseOrderId &&
                !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("PO_COMPLIANCE_NOT_FOUND",
                "The purchase order was not found in the current tenant.");
        return await EvaluateAsync(
            purchaseOrder,
            ProcurementPurchaseOrderComplianceRules.NormalizeAction(action),
            NormalizeCorrelation(correlationId),
            cancellationToken);
    }

    public async Task<ProcurementPurchaseOrderComplianceDto> EnforceAsync(
        PurchaseOrder purchaseOrder,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        if (purchaseOrder.TenantId != _currentUser.TenantId ||
            purchaseOrder.IsDeleted)
        {
            throw new ProcurementPurchaseOrderComplianceAuthorizationException(
                "The purchase order is not available in the current tenant.");
        }

        var normalizedAction =
            ProcurementPurchaseOrderComplianceRules.NormalizeAction(action);
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var readiness = await EvaluateAsync(
            purchaseOrder,
            normalizedAction,
            normalizedCorrelation,
            cancellationToken);
        await RecordAsync(readiness, normalizedCorrelation, cancellationToken);
        await PublishAsync(readiness, cancellationToken);
        if (!readiness.IsCompliant)
        {
            throw new ProcurementPurchaseOrderComplianceBlockedException(
                readiness.Code,
                readiness.Message,
                readiness);
        }

        purchaseOrder.SourceValidatedAtUtc = readiness.EvaluatedAtUtc;
        return readiness;
    }

    private async Task<ProcurementPurchaseOrderComplianceDto> EvaluateAsync(
        PurchaseOrder purchaseOrder,
        string action,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureCapabilityAsync(
            PermissionFor(action),
            purchaseOrder.OrderNumber,
            correlationId,
            cancellationToken);
        var checks = new List<ProcurementPurchaseOrderComplianceCheckDto>();
        var evaluatedAt = DateTime.UtcNow;

        ProcurementPurchaseOrderSourceResolution? source = null;
        try
        {
            source = await _sources.EvaluateCurrentAsync(
                purchaseOrder, cancellationToken);
            checks.Add(Check(
                "source",
                "Approved source",
                true,
                "PO_SOURCE_CURRENT",
                "The immutable requisition, sourcing-case, commercial, and award lineage is current.",
                source.SourceId,
                source.SourceReference,
                source.SourceIntegrityHash));
        }
        catch (Exception exception) when (
            exception is ProcurementPurchaseOrderSourceValidationException or
                ProcurementPurchaseOrderSourceAuthorizationException or
                InvalidOperationException)
        {
            checks.Add(Check(
                "source",
                "Approved source",
                false,
                exception is ProcurementPurchaseOrderSourceValidationException validation
                    ? validation.Code
                    : "PO_SOURCE_REVALIDATION_FAILED",
                exception.Message));
        }

        var categoryIds = await ResolveSupplierCategoryIdsAsync(
            purchaseOrder, cancellationToken);
        try
        {
            var supplier = await _supplierValidation.EvaluateEligibilityAsync(
                new SupplierEligibilityEvaluationRequest
                {
                    BusinessPartnerId = purchaseOrder.BusinessPartnerId,
                    Boundary = purchaseOrder.ProcurementSourceType ==
                               ProcurementPurchaseOrderSourceType.Contract
                        ? SupplierEligibilityBoundary.Contract
                        : purchaseOrder.ProcurementSourceType ==
                          ProcurementPurchaseOrderSourceType.FrameworkCallOff
                            ? SupplierEligibilityBoundary.FrameworkCallOff
                            : SupplierEligibilityBoundary.Award,
                    CategoryIds = categoryIds,
                    IncludeFinancialWarnings = true,
                    RecordAudit = false,
                    SourceType = SourceType,
                    SourceId = purchaseOrder.Id,
                    SourceReference = purchaseOrder.OrderNumber,
                    CorrelationId = correlationId
                },
                cancellationToken);
            checks.Add(Check(
                "supplier",
                "Supplier eligibility",
                supplier.IsValid,
                supplier.ValidationCode,
                supplier.IsValid
                    ? "The supplier remains active, approved, current, and eligible for the governed categories."
                    : string.Join("; ", supplier.Errors),
                purchaseOrder.BusinessPartnerId,
                $"{supplier.PartnerCode} · {supplier.PartnerName}",
                supplier.DecisionHash,
                supplier.Warnings));
        }
        catch (Exception exception)
        {
            checks.Add(Check(
                "supplier",
                "Supplier eligibility",
                false,
                "PO_SUPPLIER_ELIGIBILITY_FAILED",
                exception.Message));
        }

        await AddBudgetChecksAsync(
            purchaseOrder, checks, cancellationToken);

        ProcurementAwardReadinessDecision? awardDecision = null;
        if (source is not null)
        {
            awardDecision = await _unitOfWork
                .Repository<ProcurementAwardReadinessDecision>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == source.AwardReadinessDecisionId &&
                    !item.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
        }
        AddAwardChecks(purchaseOrder, source, awardDecision, checks);

        await AddGhanepsCheckAsync(
            purchaseOrder, source, checks, cancellationToken);
        await AddContractChecksAsync(
            purchaseOrder, checks, cancellationToken);

        var isCompliant = checks.Count == 10 &&
                          checks.All(item => item.Passed);
        var blockedReasons = checks
            .Where(item => !item.Passed)
            .Select(item => $"{item.Label}: {item.Message}")
            .ToList();
        return new ProcurementPurchaseOrderComplianceDto
        {
            PurchaseOrderId = purchaseOrder.Id,
            OrderNumber = purchaseOrder.OrderNumber,
            Status = purchaseOrder.Status,
            Action = action,
            IsCompliant = isCompliant,
            Code = isCompliant
                ? "PO_COMPLIANCE_READY"
                : "PO_COMPLIANCE_BLOCKED",
            Message = isCompliant
                ? $"Purchase order {purchaseOrder.OrderNumber} passed all pre-{action.ToLowerInvariant()} compliance checks."
                : $"Purchase order {purchaseOrder.OrderNumber} is blocked by {blockedReasons.Count} compliance check(s).",
            EvaluatedAtUtc = evaluatedAt,
            DecisionKeys = DecisionKeys,
            Checks = checks,
            BlockedReasons = blockedReasons
        };
    }

    private async Task AddBudgetChecksAsync(
        PurchaseOrder purchaseOrder,
        ICollection<ProcurementPurchaseOrderComplianceCheckDto> checks,
        CancellationToken cancellationToken)
    {
        if (!purchaseOrder.SourceRequisitionId.HasValue)
        {
            checks.Add(Check("budget", "Approved budget", false,
                "PO_BUDGET_REQUISITION_MISSING",
                "The purchase order has no approved source requisition."));
            checks.Add(Check("commitment", "Budget commitment", false,
                "PO_BUDGET_COMMITMENT_REQUISITION_MISSING",
                "An active budget commitment cannot be resolved without the source requisition."));
            return;
        }

        try
        {
            var exposure = await _unitOfWork.Repository<PurchaseOrder>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.SourceRequisitionId == purchaseOrder.SourceRequisitionId &&
                    !item.IsDeleted &&
                    item.Status != "Cancelled" &&
                    item.Status != "Rejected")
                .Select(item => (decimal?)item.TotalAmount)
                .SumAsync(cancellationToken) ?? 0m;
            var readiness = await _budgetControl.GetDownstreamReadinessAsync(
                purchaseOrder.SourceRequisitionId.Value,
                exposure,
                purchaseOrder.Currency,
                cancellationToken);
            var budgetReady = readiness.BudgetId.HasValue &&
                              readiness.BudgetId.Value != Guid.Empty &&
                              readiness.BudgetStatus is "Approved" or "Active";
            checks.Add(Check(
                "budget",
                "Approved budget",
                budgetReady,
                budgetReady ? "PO_BUDGET_CURRENT" : "PO_BUDGET_NOT_APPROVED",
                budgetReady
                    ? $"Budget {readiness.BudgetCode} is approved and current."
                    : readiness.Message,
                readiness.BudgetId,
                readiness.BudgetCode));

            var currencyMatches = string.Equals(
                readiness.Currency,
                purchaseOrder.Currency,
                StringComparison.OrdinalIgnoreCase);
            var hasActiveCommitment = string.Equals(
                readiness.CommitmentStatus,
                ProcurementBudgetCommitmentStatus.Reserved.ToString(),
                StringComparison.OrdinalIgnoreCase);
            var requiresActiveCommitment =
                ProcurementPurchaseOrderComplianceRules
                    .RequiresActiveBudgetCommitment(purchaseOrder.Status);
            var activeCommitmentCoversExposure =
                hasActiveCommitment &&
                currencyMatches &&
                ProcurementPurchaseOrderComplianceRules.IsBudgetExposureCovered(
                    readiness.RequestedAmount,
                    exposure);
            var preApprovalBudgetAvailable =
                !requiresActiveCommitment &&
                readiness.IsCompliant &&
                readiness.CanReserve &&
                currencyMatches &&
                readiness.AvailableAmount >= exposure;
            var commitmentReady = activeCommitmentCoversExposure ||
                                  preApprovalBudgetAvailable;
            checks.Add(Check(
                "commitment",
                "Budget commitment",
                commitmentReady,
                commitmentReady
                    ? activeCommitmentCoversExposure
                        ? "PO_BUDGET_COMMITMENT_CURRENT"
                        : "PO_BUDGET_AVAILABILITY_CONFIRMED"
                    : "PO_BUDGET_COMMITMENT_INSUFFICIENT",
                commitmentReady
                    ? activeCommitmentCoversExposure
                        ? $"Active reservation {readiness.CommitmentReference} covers cumulative PO exposure {exposure:N2} of {readiness.RequestedAmount:N2} {readiness.Currency}."
                        : $"Approved budget availability covers cumulative PO exposure {exposure:N2} {readiness.Currency}; the Finance commitment will be created atomically on final PO approval."
                    : requiresActiveCommitment
                        ? $"The approved purchase order has no active reservation covering cumulative PO exposure {exposure:N2}, or its status/currency is no longer valid."
                        : $"Approved budget availability does not cover cumulative PO exposure {exposure:N2}, or its currency is no longer valid.",
                readiness.CommitmentId,
                readiness.CommitmentReference,
                details:
                [
                    hasActiveCommitment
                        ? $"Reserved: {readiness.RequestedAmount:N2} {readiness.Currency}"
                        : "Commitment timing: final PO approval",
                    $"PO exposure: {exposure:N2} {purchaseOrder.Currency}"
                ],
                required: requiresActiveCommitment));
        }
        catch (Exception exception)
        {
            checks.Add(Check("budget", "Approved budget", false,
                "PO_BUDGET_CHECK_FAILED", exception.Message));
            checks.Add(Check("commitment", "Budget commitment", false,
                "PO_BUDGET_COMMITMENT_CHECK_FAILED", exception.Message));
        }
    }

    private static void AddAwardChecks(
        PurchaseOrder purchaseOrder,
        ProcurementPurchaseOrderSourceResolution? source,
        ProcurementAwardReadinessDecision? decision,
        ICollection<ProcurementPurchaseOrderComplianceCheckDto> checks)
    {
        if (IsReleaseOnlyRfqAward(purchaseOrder, source))
        {
            checks.Add(Check(
                "evaluation",
                "Evaluation evidence",
                true,
                "PO_RFQ_DIRECT_EVALUATION_RETAINED",
                "The submitted supplier quotation and selected RFQ award lines are retained in the immutable purchase-order source snapshot.",
                source!.SourceId,
                source.SourceReference,
                source.SourceIntegrityHash));
            checks.Add(Check(
                "award",
                "Award approval",
                true,
                "PO_RFQ_DIRECT_AWARD_RETAINED",
                "The direct approved-requisition RFQ winner selection is retained; the resulting draft purchase order must still complete its configured independent approval workflow.",
                source.SourceId,
                source.SourceReference,
                source.SourceIntegrityHash));
            checks.Add(Check(
                "sod",
                "Award SOD",
                true,
                "PO_RFQ_DIRECT_PO_APPROVAL_REQUIRED",
                "The direct RFQ award cannot self-approve the resulting purchase order; purchase-order maker-checker controls remain mandatory.",
                source.SourceId,
                source.SourceReference,
                source.SourceIntegrityHash));
            return;
        }

        var groups = Deserialize<List<ProcurementAwardReadinessPrerequisiteGroupDto>>(
            decision?.PrerequisiteSnapshotJson) ?? [];
        var evaluationReady =
            decision?.Status == ProcurementAwardReadinessDecisionStatus.Ready &&
            ProcurementPurchaseOrderComplianceRules.IsPrerequisiteGroupSatisfied(
                groups,
                ProcurementAwardReadinessPrerequisiteGroup.Evaluation) &&
            ProcurementPurchaseOrderComplianceRules.IsPrerequisiteGroupSatisfied(
                groups,
                ProcurementAwardReadinessPrerequisiteGroup.Evidence);
        checks.Add(Check(
            "evaluation",
            "Evaluation evidence",
            evaluationReady,
            evaluationReady
                ? "PO_EVALUATION_EVIDENCE_CURRENT"
                : "PO_EVALUATION_EVIDENCE_INCOMPLETE",
            evaluationReady
                ? "The retained readiness decision confirms current evaluation and evidence prerequisites."
                : "The current award-readiness decision does not retain passing evaluation and evidence prerequisites.",
            decision?.Id,
            decision?.SourceReference,
            decision?.IntegrityHash));

        var authorityReady =
            decision?.Status == ProcurementAwardReadinessDecisionStatus.Ready &&
            decision.Id == purchaseOrder.AwardReadinessDecisionId &&
            ProcurementPurchaseOrderSourceRules.ContainsApprovedSupplier(
                decision.RecommendedBusinessPartnerIdsJson,
                purchaseOrder.BusinessPartnerId) &&
            ProcurementPurchaseOrderComplianceRules.IsPrerequisiteGroupSatisfied(
                groups,
                ProcurementAwardReadinessPrerequisiteGroup.Recommendation) &&
            ProcurementPurchaseOrderComplianceRules.IsPrerequisiteGroupSatisfied(
                groups,
                ProcurementAwardReadinessPrerequisiteGroup.AuthorityAndWorkflow) &&
            IsSha256(decision.IntegrityHash) &&
            IsSha256(decision.SourceIntegrityHash);
        checks.Add(Check(
            "award",
            "Award approval",
            authorityReady,
            authorityReady
                ? "PO_AWARD_APPROVAL_CURRENT"
                : "PO_AWARD_APPROVAL_INCOMPLETE",
            authorityReady
                ? "The latest Ready award decision retains the recommended supplier, authority, workflow, and integrity lineage."
                : "The award decision, supplier recommendation, authority workflow, or integrity lineage is incomplete.",
            decision?.Id,
            decision?.SourceReference,
            decision?.IntegrityHash));

        var sodReady =
            authorityReady &&
            decision!.EvaluatedByUserId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(decision.AuthorityLineageJson);
        checks.Add(Check(
            "sod",
            "Award SOD",
            sodReady,
            sodReady ? "PO_AWARD_SOD_RETAINED" : "PO_AWARD_SOD_INCOMPLETE",
            sodReady
                ? "The current Ready award decision was produced only after the shared initiator/approver and evaluator/award-approver SOD guards passed."
                : "No current Ready award decision with retained SOD-controlled authority lineage exists.",
            decision?.Id,
            decision?.SourceReference,
            decision?.IntegrityHash));
    }

    internal static bool IsReleaseOnlyRfqAward(
        PurchaseOrder purchaseOrder,
        ProcurementPurchaseOrderSourceResolution? source) =>
        source is not null &&
        source.SourceType == ProcurementPurchaseOrderSourceType.RfqAward &&
        source.SourcingCaseId == Guid.Empty &&
        source.AwardReadinessDecisionId == Guid.Empty &&
        purchaseOrder.ProcurementSourceType == ProcurementPurchaseOrderSourceType.RfqAward &&
        purchaseOrder.SourceRequisitionId == source.PurchaseRequisitionId &&
        purchaseOrder.SourcingReleaseId == source.SourcingReleaseId &&
        !purchaseOrder.SourcingCaseId.HasValue &&
        !purchaseOrder.AwardReadinessDecisionId.HasValue &&
        purchaseOrder.BusinessPartnerId == source.BusinessPartnerId &&
        string.Equals(
            purchaseOrder.SourceIntegrityHash,
            source.SourceIntegrityHash,
            StringComparison.OrdinalIgnoreCase);

    private async Task AddGhanepsCheckAsync(
        PurchaseOrder purchaseOrder,
        ProcurementPurchaseOrderSourceResolution? source,
        ICollection<ProcurementPurchaseOrderComplianceCheckDto> checks,
        CancellationToken cancellationToken)
    {
        if (source is null)
        {
            checks.Add(Check("ghaneps", "GHANEPS evidence", true,
                "PO_GHANEPS_NOT_EVALUATED",
                "GHANEPS traceability is not evaluated while the approved-source check is unresolved.",
                required: false));
            return;
        }

        try
        {
            var route = await ResolveGhanepsRouteAsync(
                purchaseOrder, cancellationToken);
            if (route is null)
            {
                checks.Add(Check("ghaneps", "GHANEPS evidence", true,
                    "PO_GHANEPS_NOT_APPLICABLE",
                    "No GHANEPS award-exchange route applies to this governed purchase-order source.",
                    required: false));
                return;
            }

            var result = await _ghaneps.GetAwardComplianceAsync(
                route.Value.Type,
                route.Value.Id,
                cancellationToken);
            checks.Add(Check(
                "ghaneps",
                "GHANEPS evidence",
                !result.HasApplicableMapping || result.IsCompliant,
                result.Code,
                result.Message,
                result.Mappings.Select(item => item.ExchangeEventId)
                    .FirstOrDefault(item => item.HasValue),
                result.SourceReference,
                result.ConfigurationValueHash,
                result.Mappings.Select(item =>
                    $"{item.MappingKey}: {item.Message}"),
                required: result.HasApplicableMapping));
        }
        catch (ProcurementGhanepsExchangeConflictException exception) when (
            exception.Code == "GHANEPS_PROFILE_NOT_EFFECTIVE")
        {
            checks.Add(Check("ghaneps", "GHANEPS evidence", true,
                "PO_GHANEPS_NOT_CONFIGURED",
                "No effective GHANEPS award-exchange profile is configured; this optional traceability control does not block the purchase order.",
                required: false));
        }
        catch (Exception exception)
        {
            checks.Add(Check("ghaneps", "GHANEPS evidence", false,
                "PO_GHANEPS_CHECK_FAILED", exception.Message));
        }
    }

    private async Task AddContractChecksAsync(
        PurchaseOrder purchaseOrder,
        ICollection<ProcurementPurchaseOrderComplianceCheckDto> checks,
        CancellationToken cancellationToken)
    {
        var contract = await ResolveRequiredContractAsync(
            purchaseOrder, cancellationToken);
        if (!contract.Required)
        {
            checks.Add(Check("contract", "Required contract", true,
                "PO_CONTRACT_NOT_REQUIRED",
                contract.Reason,
                required: false));
            checks.Add(Check("signature", "Contract signatures", true,
                "PO_CONTRACT_SIGNATURE_NOT_REQUIRED",
                "No linked contract is being checked. Contract activation has not been verified.",
                required: false));
            return;
        }

        var item = contract.Contract;
        var contractReady = item is not null &&
                            item.TenantId == _currentUser.TenantId &&
                            item.BusinessPartnerId ==
                            purchaseOrder.BusinessPartnerId &&
                            string.Equals(
                                item.Status,
                                "Active",
                                StringComparison.OrdinalIgnoreCase);
        checks.Add(Check(
            "contract",
            "Required contract",
            contractReady,
            contractReady ? "PO_CONTRACT_CURRENT" : "PO_CONTRACT_INCOMPLETE",
            contractReady
                ? $"Contract {item!.ContractNumber} is active and belongs to the awarded supplier."
                : contract.Reason,
            item?.Id,
            item?.ContractNumber));

        var signedDocument = item is not null &&
                             (!string.IsNullOrWhiteSpace(
                                  item.ContractDocumentPath) ||
                              await _unitOfWork.Repository<ContractDocument>()
                                  .GetQueryable(document =>
                                      document.TenantId ==
                                      _currentUser.TenantId &&
                                      document.ContractId == item.Id &&
                                      !document.IsDeleted &&
                                      document.DocumentType == "SignedCopy" &&
                                      document.FilePath != "")
                                  .AsNoTracking()
                                  .AnyAsync(cancellationToken));
        var signatureReady = item is not null &&
                             ProcurementPurchaseOrderComplianceRules
                                 .IsContractSignatureComplete(
                                     item.SignedDate,
                                     item.SignedById,
                                     item.SignedByName,
                                     item.ContractorSignedDate,
                                     item.ContractorSignatoryName,
                                     signedDocument);
        checks.Add(Check(
            "signature",
            "Contract signatures",
            signatureReady,
            signatureReady
                ? "PO_CONTRACT_SIGNATURE_CURRENT"
                : "PO_CONTRACT_SIGNATURE_INCOMPLETE",
            signatureReady
                ? "Both organization and contractor signatures plus signed-document evidence are retained."
                : "The required contract lacks one or both signatories, signing dates, or signed-document evidence.",
            item?.Id,
            item?.ContractNumber));
    }

    private async Task<(bool Required, Contract? Contract, string Reason)>
        ResolveRequiredContractAsync(
            PurchaseOrder purchaseOrder,
            CancellationToken cancellationToken)
    {
        // Read the saved tenant setting without creating defaults or changing
        // configuration during a readiness/approval check. Missing settings
        // retain ProcurementSettings' existing optional-contract default.
        var setting = await _unitOfWork.Repository<ProcurementSettings>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking()
            .Select(item => (bool?)item.RequireContractForPO)
            .SingleOrDefaultAsync(cancellationToken);
        var requiredBySettings = setting == true;
        var settingsReason = requiredBySettings
            ? "Purchase Order Settings: 'Require contract for PO' is on. Link an active signed contract before submitting or approving this purchase order."
            : setting.HasValue
                ? "Purchase Order Settings: 'Require contract for PO' is off; no contract is linked to this source."
                : "No tenant contract requirement is configured; the default is optional and no contract is linked to this source.";

        if (purchaseOrder.ProcurementSourceType ==
            ProcurementPurchaseOrderSourceType.Contract ||
            purchaseOrder.ContractId.HasValue)
        {
            var contractId = purchaseOrder.ProcurementSourceType ==
                             ProcurementPurchaseOrderSourceType.Contract
                ? purchaseOrder.ProcurementSourceId
                : purchaseOrder.ContractId;
            if (!contractId.HasValue ||
                (purchaseOrder.ContractId.HasValue && purchaseOrder.ContractId != contractId))
                return (true, null, "The required source contract reference is missing or inconsistent.");

            var exact = await _unitOfWork.Repository<Contract>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == contractId.Value &&
                    !item.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (exact is not null &&
                ((purchaseOrder.TenderAwardId.HasValue &&
                  exact.TenderAwardId != purchaseOrder.TenderAwardId.Value) ||
                 (purchaseOrder.ProcurementSourceType == ProcurementPurchaseOrderSourceType.TenderAward &&
                  exact.TenderAwardId != purchaseOrder.ProcurementSourceId)))
                return (true, null, "The linked contract does not belong to the purchase order's awarded source.");

            return (true, exact,
                exact is null
                    ? "The required source contract was not found in the current tenant."
                    : "The required source contract is not active or does not match the awarded supplier.");
        }

        if (purchaseOrder.ProcurementSourceType ==
            ProcurementPurchaseOrderSourceType.TenderAward &&
            purchaseOrder.ProcurementSourceId.HasValue)
        {
            var award = await _unitOfWork.Repository<TenderAward>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == purchaseOrder.ProcurementSourceId.Value &&
                    !item.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (award is null)
                return (true, null, "The tender award was not found.");
            var contracts = await _unitOfWork.Repository<Contract>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.TenderAwardId == award.Id &&
                    !item.IsDeleted)
                .AsNoTracking()
                .OrderByDescending(item => item.ActivatedAt)
                .ThenByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
            var required = requiredBySettings || string.Equals(
                               award.Status,
                               "ContractSigned",
                               StringComparison.OrdinalIgnoreCase) ||
                           contracts.Count != 0;
            return (required, contracts.FirstOrDefault(),
                requiredBySettings || !required
                    ? settingsReason
                    : "The tender award's existing contract requirement needs a complete active signed contract.");
        }

        return (requiredBySettings, null, settingsReason);
    }

    private async Task<(ProcurementGhanepsSourceType Type, Guid Id)?>
        ResolveGhanepsRouteAsync(
            PurchaseOrder purchaseOrder,
            CancellationToken cancellationToken)
    {
        if (!purchaseOrder.ProcurementSourceId.HasValue)
            return null;
        switch (purchaseOrder.ProcurementSourceType)
        {
            case ProcurementPurchaseOrderSourceType.RfqAward:
                return (ProcurementGhanepsSourceType.RequestForQuotation,
                    purchaseOrder.ProcurementSourceId.Value);
            case ProcurementPurchaseOrderSourceType.ApprovedException:
                return (ProcurementGhanepsSourceType.ExceptionalSourcing,
                    purchaseOrder.ProcurementSourceId.Value);
            case ProcurementPurchaseOrderSourceType.TenderAward:
            {
                var tenderId = await _unitOfWork.Repository<TenderAward>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == purchaseOrder.ProcurementSourceId.Value &&
                        !item.IsDeleted)
                    .AsNoTracking()
                    .Select(item => (Guid?)item.TenderId)
                    .SingleOrDefaultAsync(cancellationToken);
                return tenderId.HasValue
                    ? (ProcurementGhanepsSourceType.Tender, tenderId.Value)
                    : null;
            }
            case ProcurementPurchaseOrderSourceType.Contract:
            {
                var tenderId = await _unitOfWork.Repository<Contract>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == purchaseOrder.ProcurementSourceId.Value &&
                        !item.IsDeleted)
                    .AsNoTracking()
                    .Select(item => (Guid?)item.TenderId)
                    .SingleOrDefaultAsync(cancellationToken);
                return tenderId.HasValue
                    ? (ProcurementGhanepsSourceType.Tender, tenderId.Value)
                    : null;
            }
            case ProcurementPurchaseOrderSourceType.FrameworkCallOff:
            {
                var agreement = await _unitOfWork
                    .Repository<ProcurementFrameworkCallOff>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == purchaseOrder.ProcurementSourceId.Value &&
                        !item.IsDeleted)
                    .AsNoTracking()
                    .Select(item => new
                    {
                        item.Agreement.SourceType,
                        item.Agreement.SourceId
                    })
                    .SingleOrDefaultAsync(cancellationToken);
                return agreement is null
                    ? null
                    : ((ProcurementGhanepsSourceType)agreement.SourceType,
                        agreement.SourceId);
            }
            default:
                return null;
        }
    }

    private async Task<List<Guid>> ResolveSupplierCategoryIdsAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken)
    {
        if (!purchaseOrder.ProcurementCategory.HasValue)
            return [];

        var categoryCode = SupplierCategoryCode(
            purchaseOrder.ProcurementCategory.Value);
        var categoryIds = await _unitOfWork.Repository<PartnerCategory>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.IsActive &&
                item.CategoryCode == categoryCode &&
                !item.IsDeleted)
            .AsNoTracking()
            .Select(item => item.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (categoryIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"No active supplier category is configured for procurement class {purchaseOrder.ProcurementCategory.Value}. Configure partner category {categoryCode} before submitting the purchase order.");
        }

        return categoryIds;
    }

    internal static string SupplierCategoryCode(
        ProcurementCategoryClass category) =>
        ProcurementSupplierCategoryRegistry.CodeFor(category);

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var decision = await _accessControl.EnforceCapabilityAsync(
                new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = permission,
                    SourceType = EventType,
                    SourceReference = sourceReference
                },
                correlationId,
                cancellationToken);
            if (decision.Allowed)
                return;
            throw new ProcurementPurchaseOrderComplianceAuthorizationException(
                decision.Message);
        }
        catch (ProcurementAccessAuthorizationException exception)
        {
            throw new ProcurementPurchaseOrderComplianceAuthorizationException(
                exception.Message);
        }
    }

    private async Task RecordAsync(
        ProcurementPurchaseOrderComplianceDto readiness,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "po-compliance",
                _currentUser.TenantId,
                readiness.PurchaseOrderId,
                readiness.Action,
                correlationId),
            EventType = EventType,
            Action = $"Pre{readiness.Action}ComplianceGate",
            Result = readiness.IsCompliant
                ? ProcurementControlEventResult.Allowed
                : ProcurementControlEventResult.Denied,
            RuleCode = "PO-003",
            RuleVersion = "TDC-0404",
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceType,
            SourceId = readiness.PurchaseOrderId,
            SourceReference = readiness.OrderNumber,
            Reason = readiness.Message,
            InputValues = new
            {
                readiness.Action,
                readiness.Status
            },
            ResultValues = new
            {
                readiness.IsCompliant,
                readiness.Code,
                Checks = readiness.Checks.Select(item => new
                {
                    item.Key,
                    item.Required,
                    item.Passed,
                    item.Code,
                    item.ReferenceId,
                    item.Reference,
                    item.IntegrityHash
                }),
                readiness.BlockedReasons
            },
            CorrelationId = correlationId,
            CausationId = correlationId,
            OccurredAtUtc = readiness.EvaluatedAtUtc,
            Evidence = BuildEvidence(readiness.Checks)
        }, cancellationToken);
    }

    internal static List<ProcurementControlEventEvidenceReference> BuildEvidence(
        IEnumerable<ProcurementPurchaseOrderComplianceCheckDto> checks)
    {
        return checks
            .Where(item => item.ReferenceId.HasValue ||
                           !string.IsNullOrWhiteSpace(item.Reference))
            .Select(item => new ProcurementControlEventEvidenceReference
            {
                ReferenceKind =
                    ProcurementControlEvidenceReferenceKind.ExternalReference,
                ReferenceId = item.ReferenceId,
                Reference = string.IsNullOrWhiteSpace(item.Reference)
                    ? item.ReferenceId?.ToString("D")
                    : item.Reference.Trim(),
                Label = item.Label,
                RequirementKey = item.Key
            })
            .GroupBy(
                item => new
                {
                    item.ReferenceKind,
                    Reference = item.Reference!.ToUpperInvariant()
                })
            .Select(group => group.First())
            .ToList();
    }

    private async Task PublishAsync(
        ProcurementPurchaseOrderComplianceDto readiness,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(new NotificationTopicEvent
            {
                TenantId = _currentUser.TenantId,
                TopicKey = readiness.IsCompliant
                    ? "procurement.purchase-order.compliance-passed"
                    : "procurement.purchase-order.compliance-blocked",
                NotificationType = EventType,
                EntityType = SourceType,
                EntityId = readiness.PurchaseOrderId,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["orderNumber"] = readiness.OrderNumber,
                    ["action"] = readiness.Action,
                    ["code"] = readiness.Code,
                    ["blockedReasons"] = readiness.BlockedReasons.ToArray()
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish PO compliance notification for {PurchaseOrderId}",
                readiness.PurchaseOrderId);
        }
    }

    private static ProcurementPurchaseOrderComplianceCheckDto Check(
        string key,
        string label,
        bool passed,
        string code,
        string message,
        Guid? referenceId = null,
        string? reference = null,
        string? integrityHash = null,
        IEnumerable<string>? details = null,
        bool required = true) =>
        new()
        {
            Key = key,
            Label = label,
            Required = required,
            Passed = passed,
            Code = code,
            Message = message,
            ReferenceId = referenceId,
            Reference = reference,
            IntegrityHash = integrityHash,
            Details = details?.Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? []
        };

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static bool IsSha256(string? value) =>
        value?.Length == 64 &&
        value.All(character =>
            char.IsAsciiHexDigit(character));

    private static string PermissionFor(string action) =>
        action.StartsWith("Submit", StringComparison.OrdinalIgnoreCase)
            ? "procurement.purchase-order.create"
            : action.Equals("Preview", StringComparison.OrdinalIgnoreCase)
                ? "procurement.records.read"
                : "procurement.purchase-order.approve";

    private void EnsureTenant()
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.TenantId == Guid.Empty ||
            _currentUser.UserId == Guid.Empty)
        {
            throw new ProcurementPurchaseOrderComplianceAuthorizationException(
                "An authenticated tenant context is required.");
        }
        if (_currentUser.IsExternalUser)
        {
            throw new ProcurementPurchaseOrderComplianceAuthorizationException(
                "Supplier portal users cannot access purchase-order compliance controls.");
        }
    }

    private static ProcurementPurchaseOrderComplianceNotFoundException NotFound(
        string code,
        string message) => new(code, message);

    private static string NormalizeCorrelation(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = Guid.NewGuid().ToString("N");
        return normalized.Length <= 100
            ? normalized
            : normalized[..100];
    }
}
