using ErpSystem.Shared;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementContractOperationsService :
    IProcurementContractOperationsService
{
    private const string EventType = "ProcurementContractOperations";
    private const string ReadPermission = "procurement.reports.read";
    private const string ManagePermission = "procurement.contract.manage";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToList();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;
    private readonly ILogger<ProcurementContractOperationsService> _logger;

    public ProcurementContractOperationsService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications,
        ILogger<ProcurementContractOperationsService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ProcurementContractOperationsPortfolioDto> SearchAsync(
        ProcurementContractOperationsSearchRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        request ??= new ProcurementContractOperationsSearchRequest();
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            ReadPermission, "portfolio", correlation, cancellationToken);

        var details = await LoadDetailsAsync(null, cancellationToken);
        IEnumerable<ProcurementContractOperationsDetailDto> filtered = details;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            filtered = filtered.Where(item =>
                item.Summary.ContractNumber.Contains(search,
                    StringComparison.OrdinalIgnoreCase) ||
                item.Summary.ContractTitle.Contains(search,
                    StringComparison.OrdinalIgnoreCase) ||
                item.Summary.BusinessPartnerName.Contains(search,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
            filtered = filtered.Where(item => item.Summary.Status.Equals(
                request.Status.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.Risk))
            filtered = filtered.Where(item => item.Summary.OverallRisk.Equals(
                request.Risk.Trim(), StringComparison.OrdinalIgnoreCase));

        var items = filtered
            .OrderBy(item => RiskRank(item.Summary.OverallRisk))
            .ThenBy(item => item.Summary.DaysToExpiry ?? int.MaxValue)
            .ThenBy(item => item.Summary.ContractNumber)
            .Take(Math.Clamp(request.Take, 1, 250))
            .Select(item => item.Summary)
            .ToList();

        return new ProcurementContractOperationsPortfolioDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            TotalContracts = items.Count,
            ActiveContracts = items.Count(item =>
                item.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)),
            ContractsWithPrompts = items.Count(item => item.PromptCount > 0),
            CriticalPromptCount = items.Sum(item => item.CriticalPromptCount),
            CurrencyTotals = items.GroupBy(item =>
                    NormalizeCurrency(item.Currency),
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key)
                .Select(group => new ProcurementContractOperationsCurrencyTotalDto
                {
                    Currency = group.Key,
                    ContractValue = RoundMoney(group.Sum(item => item.ContractValue)),
                    CommittedSpend = RoundMoney(group.Sum(item => item.CommittedSpend)),
                    InvoicedAmount = RoundMoney(group.Sum(item => item.InvoicedAmount)),
                    PaidAmount = RoundMoney(group.Sum(item => item.PaidAmount)),
                    OutstandingAmount = RoundMoney(group.Sum(item => item.OutstandingAmount)),
                    RetentionHeldAmount = RoundMoney(group.Sum(item =>
                        Math.Max(0m, item.RetentionHeldAmount -
                                    item.RetentionReleasedAmount)))
                }).ToList(),
            Items = items,
            DecisionKeys = DecisionKeys
        };
    }

    public async Task<ProcurementContractOperationsDetailDto> GetAsync(
        Guid contractId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (contractId == Guid.Empty)
            throw new ProcurementContractOperationsValidationException(
                "CONTRACT_ID_REQUIRED", "ContractId is required.");
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            ReadPermission, contractId.ToString("N"), correlation,
            cancellationToken);

        var detail = (await LoadDetailsAsync(contractId, cancellationToken))
            .SingleOrDefault();
        return detail ??
            throw new ProcurementContractOperationsNotFoundException(
                "CONTRACT_NOT_FOUND",
                "The contract does not exist in the current tenant.");
    }

    public async Task<ProcessProcurementContractOperationsAlertsResult>
        ProcessAlertsAsync(
            ProcessProcurementContractOperationsAlertsRequest request,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        request ??= new ProcessProcurementContractOperationsAlertsRequest();
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            ManagePermission,
            request.ContractId?.ToString("N") ?? "portfolio",
            correlation,
            cancellationToken);

        var details = await LoadDetailsAsync(request.ContractId, cancellationToken);
        if (request.ContractId.HasValue && details.Count == 0)
            throw new ProcurementContractOperationsNotFoundException(
                "CONTRACT_NOT_FOUND",
                "The contract does not exist in the current tenant.");

        var now = DateTime.UtcNow;
        var prompts = details
            .SelectMany(detail => detail.Prompts.Select(prompt =>
                (detail, prompt)))
            .Where(item =>
                item.prompt.Severity is "Critical" or "High" or "Medium")
            .ToList();
        var keys = prompts.Select(item => ProcurementControlEventKey.Create(
                "contract-operations-prompt",
                _currentUser.TenantId,
                item.detail.Summary.ContractId,
                item.prompt.Key,
                now.ToString("yyyyMMdd")))
            .ToList();
        var existing = keys.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _unitOfWork.Repository<ProcurementControlEvent>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        keys.Contains(item.EventKey) && !item.IsDeleted)
                    .AsNoTracking()
                    .Select(item => item.EventKey)
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var published = new List<string>();
        var already = 0;

        for (var index = 0; index < prompts.Count; index++)
        {
            var (detail, prompt) = prompts[index];
            var eventKey = keys[index];
            if (existing.Contains(eventKey))
            {
                already++;
                continue;
            }

            try
            {
                await _controlEvents.RecordAsync(
                    new ProcurementControlEventWriteRequest
                    {
                        EventKey = eventKey,
                        EventType = EventType,
                        Action = "ContractOperationsPromptPublished",
                        Result = ProcurementControlEventResult.Warning,
                        RuleCode = $"TDC-0408-{prompt.Type}",
                        RuleId = detail.Summary.ContractId,
                        RuleVersion = "TDC-0408",
                        DecisionKeys = DecisionKeys.ToList(),
                        SourceType = "ProcurementContract",
                        SourceId = detail.Summary.ContractId,
                        SourceReference = detail.Summary.ContractNumber,
                        Reason = prompt.Message,
                        InputValues = new
                        {
                            prompt.Key,
                            prompt.Type,
                            prompt.Severity,
                            prompt.SourceId,
                            prompt.SourceReference,
                            prompt.DueAtUtc,
                            prompt.DaysOverdue
                        },
                        ResultValues = new
                        {
                            prompt.RecommendedAction,
                            prompt.EstimatedPenaltyAmount,
                            prompt.CalculationBasis,
                            prompt.RequiresIndependentApproval,
                            prompt.AmountAutoPosted
                        },
                        CorrelationId = correlation,
                        CausationId = correlation,
                        OccurredAtUtc = now
                    }, cancellationToken);
            }
            catch (ProcurementControlEventConflictException)
            {
                already++;
                continue;
            }

            published.Add(prompt.Key);
            await PublishNotificationAsync(detail, prompt, cancellationToken);
        }

        return new ProcessProcurementContractOperationsAlertsResult
        {
            ProcessedAtUtc = now,
            EvaluatedPromptCount = prompts.Count,
            PublishedAlertCount = published.Count,
            AlreadyPublishedCount = already,
            PublishedPromptKeys = published
        };
    }

    private async Task<IReadOnlyList<ProcurementContractOperationsDetailDto>>
        LoadDetailsAsync(
            Guid? contractId,
            CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;
        var now = DateTime.UtcNow;
        var contractsQuery = _unitOfWork.Repository<Contract>()
            .GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                (!contractId.HasValue || item.Id == contractId.Value))
            .Include(item => item.BusinessPartner)
            .AsNoTracking();
        var contracts = await contractsQuery
            .OrderBy(item => item.ContractNumber)
            .Take(contractId.HasValue ? 1 : 250)
            .ToListAsync(cancellationToken);
        if (contracts.Count == 0) return [];

        var contractIds = contracts.Select(item => item.Id).ToList();
        var partnerIds = contracts.Select(item => item.BusinessPartnerId)
            .Distinct().ToList();
        var milestones = await _unitOfWork.Repository<ContractMilestone>()
            .GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                contractIds.Contains(item.ContractId))
            .AsNoTracking().ToListAsync(cancellationToken);
        var purchaseOrders = await _unitOfWork.Repository<PurchaseOrder>()
            .GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                item.ContractId.HasValue &&
                contractIds.Contains(item.ContractId.Value))
            .AsNoTracking().ToListAsync(cancellationToken);
        var purchaseOrderIds = purchaseOrders.Select(item => item.Id).ToList();
        var receipts = purchaseOrderIds.Count == 0
            ? []
            : await _unitOfWork.Repository<PurchaseOrderReceipt>()
                .GetQueryable(item =>
                    item.TenantId == tenantId && !item.IsDeleted &&
                    purchaseOrderIds.Contains(item.PurchaseOrderId))
                .AsNoTracking().ToListAsync(cancellationToken);
        var invoices = purchaseOrderIds.Count == 0
            ? []
            : await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(item =>
                    item.TenantId == tenantId && !item.IsDeleted &&
                    item.PurchaseOrderId.HasValue &&
                    purchaseOrderIds.Contains(item.PurchaseOrderId.Value))
                .AsNoTracking().ToListAsync(cancellationToken);
        var certificates = await _unitOfWork
            .Repository<ProjectPaymentCertificate>()
            .GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                item.ContractId.HasValue &&
                contractIds.Contains(item.ContractId.Value))
            .AsNoTracking().ToListAsync(cancellationToken);
        var activations = await _unitOfWork
            .Repository<ProcurementContractActivation>()
            .GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                contractIds.Contains(item.ContractId))
            .AsNoTracking().ToListAsync(cancellationToken);
        var scorecards = await _unitOfWork
            .Repository<ProcurementSupplierPerformanceScorecard>()
            .GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                partnerIds.Contains(item.BusinessPartnerId))
            .AsNoTracking().ToListAsync(cancellationToken);
        var riskAssessments = await _unitOfWork
            .Repository<ProcurementSupplierRiskAssessment>()
            .GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                partnerIds.Contains(item.BusinessPartnerId))
            .AsNoTracking().ToListAsync(cancellationToken);

        return contracts.Select(contract =>
        {
            var currency = NormalizeCurrency(contract.Currency);
            var contractMilestones = milestones
                .Where(item => item.ContractId == contract.Id)
                .OrderBy(item => item.SequenceNumber).ToList();
            var contractOrders = purchaseOrders
                .Where(item => item.ContractId == contract.Id).ToList();
            var orderIds = contractOrders.Select(item => item.Id)
                .ToHashSet();
            var contractReceipts = receipts
                .Where(item => orderIds.Contains(item.PurchaseOrderId)).ToList();
            var contractInvoices = invoices
                .Where(item => item.PurchaseOrderId.HasValue &&
                               orderIds.Contains(item.PurchaseOrderId.Value))
                .ToList();
            var contractCertificates = certificates
                .Where(item => item.ContractId == contract.Id &&
                    IsActualCertificate(item.Status)).ToList();
            var scorecard = scorecards
                .Where(item => item.BusinessPartnerId ==
                               contract.BusinessPartnerId)
                .OrderByDescending(item => item.CalculatedAtUtc)
                .ThenByDescending(item => item.ScorecardSequence)
                .FirstOrDefault();
            var risk = riskAssessments
                .Where(item => item.BusinessPartnerId ==
                               contract.BusinessPartnerId)
                .OrderByDescending(item => item.AssessedAtUtc)
                .ThenByDescending(item => item.AssessmentSequence)
                .FirstOrDefault();
            var activation = activations
                .Where(item => item.ContractId == contract.Id)
                .OrderByDescending(item => item.Sequence)
                .FirstOrDefault();
            var currencyConflict =
                contractOrders.Any(item =>
                    !CurrencyMatches(item.Currency, currency)) ||
                contractInvoices.Any(item =>
                    !CurrencyMatches(item.CurrencyCode, currency)) ||
                contractCertificates.Any(item =>
                    !CurrencyMatches(item.Currency, currency));
            var compatibleOrders = contractOrders.Where(item =>
                CurrencyMatches(item.Currency, currency)).ToList();
            var compatibleInvoices = contractInvoices.Where(item =>
                CurrencyMatches(item.CurrencyCode, currency) &&
                item.Status is not VendorInvoiceStatus.Voided and
                    not VendorInvoiceStatus.Rejected).ToList();
            var compatibleCertificates = contractCertificates.Where(item =>
                CurrencyMatches(item.Currency, currency)).ToList();
            var committed = RoundMoney(compatibleOrders
                .Where(item =>
                    !item.Status.Equals("Draft",
                        StringComparison.OrdinalIgnoreCase) &&
                    !item.Status.Equals("Cancelled",
                        StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.TotalAmount));
            var invoiced = RoundMoney(
                compatibleInvoices.Sum(item => item.TotalAmount));
            var paid = RoundMoney(
                compatibleInvoices.Sum(item => item.PaidAmount));
            var held = RoundMoney(
                compatibleCertificates.Sum(item =>
                    item.RetentionHeldAmount));
            var released = RoundMoney(
                compatibleCertificates.Sum(item =>
                    item.RetentionReleasedAmount));

            var ruleInput = new ProcurementContractOperationsRuleInput(
                contract.Id,
                contract.ContractNumber,
                contract.Status,
                contract.ContractValue,
                currency,
                contract.EndDate,
                contract.PenaltyClause,
                held,
                released,
                scorecard?.OverallScore,
                scorecard?.MinimumScore,
                scorecard?.MinimumScoreBreached == true,
                risk?.RiskScore,
                risk is { MinimumScoreBreached: true } or
                    { ConcentrationBreached: true },
                currencyConflict,
                contractMilestones.Select(item =>
                    new ProcurementContractOperationsMilestoneInput(
                        item.Id,
                        item.MilestoneName,
                        item.Status,
                        item.PlannedDate,
                        item.PaymentAmount)).ToList(),
                contractOrders.Select(item =>
                    new ProcurementContractOperationsDeliveryInput(
                        item.Id,
                        item.OrderNumber,
                        item.Status,
                        item.PromisedDate ?? item.RequiredDate,
                        item.TotalAmount)).ToList(),
                compatibleInvoices.Select(item =>
                    new ProcurementContractOperationsInvoiceInput(
                        item.Id,
                        item.InvoiceNumber,
                        item.Status.ToString(),
                        item.DueDate,
                        Math.Max(0m, item.TotalAmount - item.PaidAmount)))
                    .ToList());
            var prompts =
                ProcurementContractOperationsRules.Evaluate(ruleInput, now);
            var completedMilestones = contractMilestones.Count(item =>
                IsCompletedMilestone(item.Status));
            var overdueMilestones = contractMilestones.Count(item =>
                !IsCompletedMilestone(item.Status) &&
                item.PlannedDate.HasValue &&
                item.PlannedDate.Value.Date < now.Date);
            var daysToExpiry = contract.EndDate.HasValue
                ? (contract.EndDate.Value.Date - now.Date).Days
                : (int?)null;
            var summary = new ProcurementContractOperationsListItemDto
            {
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                ContractTitle = contract.ContractTitle,
                ContractType = contract.ContractType,
                Status = contract.Status,
                BusinessPartnerId = contract.BusinessPartnerId,
                BusinessPartnerName =
                    contract.BusinessPartner.PartnerName,
                Currency = currency,
                ContractValue = RoundMoney(contract.ContractValue),
                CommittedSpend = committed,
                InvoicedAmount = invoiced,
                PaidAmount = paid,
                OutstandingAmount = RoundMoney(
                    Math.Max(0m, invoiced - paid)),
                RetentionHeldAmount = held,
                RetentionReleasedAmount = released,
                PurchaseOrderCount = contractOrders.Count,
                ReceiptCount = contractReceipts.Count,
                TotalMilestones = contractMilestones.Count,
                CompletedMilestones = completedMilestones,
                OverdueMilestones = overdueMilestones,
                MilestoneCompletionPercent = contractMilestones.Count == 0
                    ? 0m
                    : decimal.Round(
                        completedMilestones * 100m /
                        contractMilestones.Count, 2,
                        MidpointRounding.AwayFromZero),
                EndDate = contract.EndDate,
                DaysToExpiry = daysToExpiry,
                RenewalStatus =
                    ProcurementContractOperationsRules.RenewalStatus(
                        contract.EndDate, contract.Status, now),
                SupplierPerformanceScore = scorecard?.OverallScore,
                SupplierPerformanceBand = scorecard?.PerformanceBand,
                SupplierRiskScore = risk?.RiskScore,
                SupplierRiskBand = risk?.RiskBand,
                PromptCount = prompts.Count,
                CriticalPromptCount = prompts.Count(item =>
                    item.Severity == "Critical"),
                OverallRisk =
                    ProcurementContractOperationsRules.OverallRisk(prompts)
            };

            return new ProcurementContractOperationsDetailDto
            {
                Summary = summary,
                GeneratedAtUtc = now,
                PenaltyClause = contract.PenaltyClause,
                PaymentTerms = contract.PaymentTerms,
                RetentionPercentage = contract.RetentionPercentage,
                Milestones = contractMilestones.Select(item =>
                    new ProcurementContractOperationsMilestoneDto
                    {
                        Id = item.Id,
                        Name = item.MilestoneName,
                        Sequence = item.SequenceNumber,
                        Status = item.Status,
                        PaymentAmount = item.PaymentAmount,
                        PlannedDate = item.PlannedDate,
                        ActualDate = item.ActualDate,
                        DaysLate = MilestoneDaysLate(item, now)
                    }).ToList(),
                PurchaseOrders = contractOrders
                    .OrderByDescending(item => item.OrderDate)
                    .Select(item =>
                        new ProcurementContractOperationsPurchaseOrderDto
                        {
                            Id = item.Id,
                            Number = item.OrderNumber,
                            Status = item.Status,
                            Currency = NormalizeCurrency(item.Currency),
                            Amount = item.TotalAmount,
                            OrderDate = item.OrderDate,
                            PromisedDate =
                                item.PromisedDate ?? item.RequiredDate,
                            ReceivedDate = item.ReceivedDate,
                            ReceiptCount = contractReceipts.Count(receipt =>
                                receipt.PurchaseOrderId == item.Id)
                        }).ToList(),
                Invoices = contractInvoices
                    .OrderByDescending(item => item.InvoiceDate)
                    .Select(item =>
                        new ProcurementContractOperationsInvoiceDto
                        {
                            Id = item.Id,
                            Number = item.InvoiceNumber,
                            Status = item.Status.ToString(),
                            Currency =
                                NormalizeCurrency(item.CurrencyCode),
                            TotalAmount = item.TotalAmount,
                            PaidAmount = item.PaidAmount,
                            OutstandingAmount = Math.Max(
                                0m, item.TotalAmount - item.PaidAmount),
                            InvoiceDate = item.InvoiceDate,
                            DueDate = item.DueDate,
                            IsOverdue = item.DueDate.HasValue &&
                                item.DueDate.Value.Date < now.Date &&
                                item.TotalAmount > item.PaidAmount &&
                                item.Status is not
                                    VendorInvoiceStatus.Voided and
                                    not VendorInvoiceStatus.Rejected
                        }).ToList(),
                Kpis = BuildKpis(
                    scorecard, completedMilestones,
                    contractMilestones.Count, compatibleInvoices, now),
                Prompts = prompts,
                Lineage = activation is null
                    ? new ProcurementContractOperationsLineageDto()
                    : new ProcurementContractOperationsLineageDto
                    {
                        ActivationId = activation.Id,
                        ActivationSequence = activation.Sequence,
                        ActivationStatus = activation.Status.ToString(),
                        ConfigurationProfileId =
                            activation.ConfigurationProfileId,
                        ConfigurationProfileVersion =
                            activation.ConfigurationProfileVersion,
                        PolicySetId = activation.PolicySetId,
                        PolicyVersion = activation.PolicyVersion,
                        WorkflowDefinitionId =
                            activation.WorkflowDefinitionId,
                        WorkflowInstanceId =
                            activation.WorkflowInstanceId,
                        AwardReadinessDecisionId =
                            activation.AwardReadinessDecisionId,
                        IntegrityHash = activation.IntegrityHash
                    },
                DecisionKeys = DecisionKeys
            };
        }).ToList();
    }

    private static IReadOnlyList<ProcurementContractOperationsKpiDto>
        BuildKpis(
            ProcurementSupplierPerformanceScorecard? scorecard,
            int completedMilestones,
            int totalMilestones,
            IReadOnlyCollection<VendorInvoice> invoices,
            DateTime now)
    {
        var milestoneScore = totalMilestones == 0
            ? (decimal?)null
            : decimal.Round(completedMilestones * 100m /
                totalMilestones, 2, MidpointRounding.AwayFromZero);
        var payableInvoices = invoices.Where(item =>
            item.Status is not VendorInvoiceStatus.Voided and
                not VendorInvoiceStatus.Rejected).ToList();
        var onTimePayment = payableInvoices.Count == 0
            ? (decimal?)null
            : decimal.Round(payableInvoices.Count(item =>
                    !item.DueDate.HasValue ||
                    item.PaidAmount >= item.TotalAmount ||
                    item.DueDate.Value.Date >= now.Date) *
                100m / payableInvoices.Count, 2,
                MidpointRounding.AwayFromZero);
        var reference = scorecard?.ScorecardReference ??
                        "No governed scorecard";

        return
        [
            Kpi("MILESTONE_COMPLETION", "Milestone completion",
                milestoneScore, null, milestoneScore.HasValue
                    ? "Measured" : "Unavailable", "Contract milestones"),
            Kpi("PAYMENT_TIMELINESS", "Payment timeliness",
                onTimePayment, null, onTimePayment.HasValue
                    ? "Measured" : "Unavailable", "Accounts Payable"),
            Kpi("SUPPLIER_OVERALL", "Supplier overall performance",
                scorecard?.OverallScore, scorecard?.MinimumScore,
                scorecard is null ? "Unavailable" :
                    scorecard.MinimumScoreBreached ? "Breach" : "Passed",
                reference),
            Kpi("DELIVERY_TIMELINESS", "Delivery timeliness",
                scorecard?.DeliveryTimelinessScore, null,
                scorecard?.DeliveryTimelinessScore.HasValue == true
                    ? "Measured" : "Unavailable", reference),
            Kpi("GRN_QUALITY", "GRN quality",
                scorecard?.GrnQualityScore, null,
                scorecard?.GrnQualityScore.HasValue == true
                    ? "Measured" : "Unavailable", reference),
            Kpi("REJECTION_RATE", "Rejection performance",
                scorecard?.RejectionRateScore, null,
                scorecard?.RejectionRateScore.HasValue == true
                    ? "Measured" : "Unavailable", reference),
            Kpi("RESPONSIVENESS", "Supplier responsiveness",
                scorecard?.ResponsivenessScore, null,
                scorecard?.ResponsivenessScore.HasValue == true
                    ? "Measured" : "Unavailable", reference),
            Kpi("CONTRACT_COMPLETION", "Contract completion",
                scorecard?.ContractCompletionScore, null,
                scorecard?.ContractCompletionScore.HasValue == true
                    ? "Measured" : "Unavailable", reference)
        ];
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (HasPlatformSuperAdministratorBypass()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                SourceType = EventType,
                SourceReference = sourceReference
            }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementContractOperationsAuthorizationException(
                decision.Message);
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty ||
            _currentUser.IsExternalUser)
            throw new ProcurementContractOperationsAuthorizationException(
                "An authenticated internal tenant context is required.");
    }

    private bool HasPlatformSuperAdministratorBypass() =>
        _currentUser.HasRole(Constants.Roles.SuperAdmin);

    private async Task PublishNotificationAsync(
        ProcurementContractOperationsDetailDto detail,
        ProcurementContractOperationsPromptDto prompt,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(new NotificationTopicEvent
            {
                TenantId = _currentUser.TenantId,
                TopicKey = "procurement.contract.operations-prompt",
                NotificationType = "ProcurementContractOperationsPrompt",
                EntityType = "ProcurementContract",
                EntityId = detail.Summary.ContractId,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["contractNumber"] =
                        detail.Summary.ContractNumber,
                    ["businessPartnerName"] =
                        detail.Summary.BusinessPartnerName,
                    ["promptKey"] = prompt.Key,
                    ["promptType"] = prompt.Type,
                    ["severity"] = prompt.Severity,
                    ["title"] = prompt.Title,
                    ["message"] = prompt.Message,
                    ["recommendedAction"] =
                        prompt.RecommendedAction,
                    ["requiresIndependentApproval"] =
                        prompt.RequiresIndependentApproval,
                    ["amountAutoPosted"] = prompt.AmountAutoPosted
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish contract operations prompt {PromptKey} for {ContractId}",
                prompt.Key, detail.Summary.ContractId);
        }
    }

    private static ProcurementContractOperationsKpiDto Kpi(
        string key,
        string label,
        decimal? score,
        decimal? target,
        string status,
        string reference) => new()
        {
            Key = key,
            Label = label,
            Score = score,
            Target = target,
            Status = status,
            SourceReference = reference
        };

    private static bool IsCompletedMilestone(string status) =>
        status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Invoiced", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Paid", StringComparison.OrdinalIgnoreCase);

    private static int MilestoneDaysLate(
        ContractMilestone milestone,
        DateTime now)
    {
        if (!milestone.PlannedDate.HasValue) return 0;
        var actual = milestone.ActualDate ??
            (IsCompletedMilestone(milestone.Status)
                ? milestone.CompletedAt
                : now);
        return actual.HasValue
            ? Math.Max(0,
                (actual.Value.Date -
                 milestone.PlannedDate.Value.Date).Days)
            : 0;
    }

    private static bool IsActualCertificate(string status) =>
        status.Equals(ProjectPaymentCertificateStatuses.Issued,
            StringComparison.OrdinalIgnoreCase) ||
        status.Equals(ProjectPaymentCertificateStatuses.Approved,
            StringComparison.OrdinalIgnoreCase) ||
        status.Equals(ProjectPaymentCertificateStatuses.Paid,
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCurrency(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "UNKNOWN"
            : value.Trim().ToUpperInvariant();

    private static bool CurrencyMatches(string? value, string currency) =>
        NormalizeCurrency(value).Equals(
            currency, StringComparison.OrdinalIgnoreCase);

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string NormalizeCorrelation(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value.Trim().Length <= 100
                ? value.Trim()
                : value.Trim()[..100];

    private static int RiskRank(string value) => value switch
    {
        "Critical" => 0,
        "High" => 1,
        "Medium" => 2,
        _ => 3
    };
}
