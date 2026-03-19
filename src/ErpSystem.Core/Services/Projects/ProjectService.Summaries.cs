using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<ProjectFinancialControlSummaryDto> GetFinancialControlSummaryAsync(Guid projectId)
    {
        var project = await GetProjectForOperationAsync(projectId, ProjectAccessOperation.ManageFinancials);
        await SyncProjectMaterialCostAsync(projectId);
        var timesheets = (await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var expenses = (await _unitOfWork.Repository<ProjectExpense>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var schedules = (await _unitOfWork.Repository<ProjectBillingSchedule>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var invoiceRequests = (await _unitOfWork.Repository<ProjectInvoiceRequest>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var revenueRecognitions = (await _unitOfWork.Repository<ProjectRevenueRecognition>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var purchaseRequisitions = (await _unitOfWork.Repository<PurchaseRequisition>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var purchaseRequisitionIds = purchaseRequisitions.Select(x => x.Id).ToHashSet();
        var purchaseOrders = (await _unitOfWork.Repository<PurchaseOrder>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.SourceRequisitionId.HasValue
                && purchaseRequisitionIds.Contains(x.SourceRequisitionId.Value)))
            .ToList();
        var purchaseOrderIds = purchaseOrders.Select(x => x.Id).ToHashSet();
        var purchaseOrderItems = purchaseOrderIds.Count == 0
            ? new List<PurchaseOrderItem>()
            : (await _unitOfWork.Repository<PurchaseOrderItem>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && purchaseOrderIds.Contains(x.PurchaseOrderId))).ToList();
        var purchaseReceipts = purchaseOrderIds.Count == 0
            ? new List<PurchaseOrderReceipt>()
            : (await _unitOfWork.Repository<PurchaseOrderReceipt>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && purchaseOrderIds.Contains(x.PurchaseOrderId))).ToList();
        var purchaseReceiptIds = purchaseReceipts.Select(x => x.Id).ToHashSet();
        var purchaseReceiptItems = purchaseReceiptIds.Count == 0
            ? new List<PurchaseOrderReceiptItem>()
            : (await _unitOfWork.Repository<PurchaseOrderReceiptItem>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && purchaseReceiptIds.Contains(x.ReceiptId))).ToList();
        var budgetRevisions = await GetBudgetRevisionEntitiesAsync(projectId);
        var forecastVersions = await GetForecastVersionEntitiesAsync(projectId);
        var approvedRevision = budgetRevisions
            .Where(x => string.Equals(x.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault();
        var activeForecast = forecastVersions
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.VersionNumber)
            .FirstOrDefault(x => x.IsActive)
            ?? forecastVersions.OrderByDescending(x => x.VersionNumber).FirstOrDefault();

        var baselineBudget = approvedRevision?.ApprovedBudget ?? project.ApprovedBudget ?? project.EstimatedBudget ?? 0m;
        var actualCost = project.ActualCost ?? 0m;
        var manualCommittedCost = approvedRevision?.CommittedCost ?? 0m;
        var pendingOperationalCost = timesheets.Where(x => !string.Equals(x.Status, "Approved", StringComparison.OrdinalIgnoreCase)).Sum(x => x.CostAmount)
            + expenses.Where(x => !string.Equals(x.Status, "Approved", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount + x.TaxAmount);
        var procurementRequestedAmount = purchaseRequisitions.Where(IsPendingPurchaseRequisitionStatus).Sum(x => x.TotalAmount);
        var procurementCommittedAmount = purchaseOrders.Where(x => !string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)).Sum(x => x.TotalAmount);
        var procurementOpenCommitmentAmount = purchaseOrders.Where(IsOpenPurchaseOrderStatus).Sum(x => x.TotalAmount);
        var purchaseOrderItemsById = purchaseOrderItems.ToDictionary(x => x.Id, x => x);
        var procurementReceivedAmount = decimal.Round(purchaseReceiptItems.Sum(receiptItem =>
        {
            if (!purchaseOrderItemsById.TryGetValue(receiptItem.PurchaseOrderItemId, out var orderItem))
            {
                return 0m;
            }

            var valuedQuantity = receiptItem.AcceptedQuantity > 0m ? receiptItem.AcceptedQuantity : receiptItem.ReceivedQuantity;
            if (valuedQuantity <= 0m)
            {
                return 0m;
            }

            var unitCost = ResolvePurchaseOrderItemUnitCost(orderItem);
            return decimal.Round(unitCost * valuedQuantity, 2);
        }), 2);
        var receiptIdsPendingInspection = purchaseReceipts
            .Where(x => x.RequiresInspection && !string.Equals(x.Status, "Accepted", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToHashSet();
        var procurementPendingInspectionAmount = decimal.Round(purchaseReceiptItems
            .Where(x => receiptIdsPendingInspection.Contains(x.ReceiptId))
            .Sum(receiptItem =>
            {
                if (!purchaseOrderItemsById.TryGetValue(receiptItem.PurchaseOrderItemId, out var orderItem))
                {
                    return 0m;
                }

                var valuedQuantity = receiptItem.AcceptedQuantity > 0m ? receiptItem.AcceptedQuantity : receiptItem.ReceivedQuantity;
                if (valuedQuantity <= 0m)
                {
                    return 0m;
                }

                return decimal.Round(ResolvePurchaseOrderItemUnitCost(orderItem) * valuedQuantity, 2);
            }), 2);
        var committedCost = Math.Max(manualCommittedCost, procurementCommittedAmount);
        var pendingCost = pendingOperationalCost + procurementRequestedAmount;
        var totalExposureAmount = actualCost + pendingCost + procurementOpenCommitmentAmount;
        var forecastFloor = actualCost + pendingOperationalCost + procurementRequestedAmount + procurementOpenCommitmentAmount;
        var forecastCost = activeForecast?.ForecastCost ?? approvedRevision?.ForecastCost ?? forecastFloor;
        var estimateAtCompletion = activeForecast?.EstimateAtCompletion
            ?? Math.Max(
                project.ProgressPercent > 0m
                    ? decimal.Round(Math.Max(actualCost, actualCost / Math.Max(project.ProgressPercent / 100m, 0.01m)), 2)
                    : forecastFloor,
                forecastFloor);
        var budgetConsumption = baselineBudget <= 0m ? 0m : decimal.Round((actualCost / baselineBudget) * 100m, 2);
        var remainingBudget = baselineBudget - Math.Max(actualCost, committedCost);
        var scheduledBillingAmount = schedules.Where(x => x.IsBillable).Sum(x => x.Amount);
        var invoiceRequestedAmount = invoiceRequests.Sum(x => x.RequestedAmount);
        var recognizedRevenue = revenueRecognitions.Sum(x => x.RecognizedRevenue);
        var grossMargin = recognizedRevenue - actualCost;
        var warningThreshold = approvedRevision?.ThresholdWarningPercent ?? 75m;
        var criticalThreshold = approvedRevision?.ThresholdCriticalPercent ?? 90m;
        var plannedValue = CalculatePlannedValue(project, baselineBudget);
        var earnedValue = baselineBudget <= 0m
            ? 0m
            : decimal.Round(baselineBudget * Math.Clamp(project.ProgressPercent / 100m, 0m, 1m), 2);
        var scheduleVariance = decimal.Round(earnedValue - plannedValue, 2);
        var costVariance = decimal.Round(earnedValue - actualCost, 2);
        decimal? costPerformanceIndex = actualCost > 0m ? decimal.Round(earnedValue / actualCost, 2) : null;
        decimal? schedulePerformanceIndex = plannedValue > 0m ? decimal.Round(earnedValue / plannedValue, 2) : null;
        decimal? toCompletePerformanceIndex = baselineBudget > earnedValue && estimateAtCompletion > actualCost
            ? decimal.Round((baselineBudget - earnedValue) / Math.Max(estimateAtCompletion - actualCost, 0.01m), 2)
            : null;
        var profitabilityPercent = recognizedRevenue > 0m
            ? decimal.Round((grossMargin / recognizedRevenue) * 100m, 2)
            : 0m;
        var healthStatus = ResolveFinancialHealthStatus(costPerformanceIndex, schedulePerformanceIndex, estimateAtCompletion, baselineBudget, budgetConsumption, criticalThreshold, warningThreshold);

        var alerts = new List<ProjectFinancialAlertDto>();
        if (baselineBudget <= 0m)
        {
            alerts.Add(new ProjectFinancialAlertDto { Severity = "High", Message = "No approved or estimated budget baseline is available." });
        }
        if (budgetConsumption >= criticalThreshold)
        {
            alerts.Add(new ProjectFinancialAlertDto { Severity = "Critical", Message = $"Budget consumption is {budgetConsumption}% of baseline." });
        }
        else if (budgetConsumption >= warningThreshold)
        {
            alerts.Add(new ProjectFinancialAlertDto { Severity = "High", Message = $"Budget consumption is {budgetConsumption}% of baseline." });
        }
        if (estimateAtCompletion > baselineBudget && baselineBudget > 0m)
        {
            alerts.Add(new ProjectFinancialAlertDto { Severity = "High", Message = "Forecast cost exceeds the current budget baseline." });
        }
        if (committedCost > baselineBudget && baselineBudget > 0m)
        {
            alerts.Add(new ProjectFinancialAlertDto { Severity = "High", Message = "Committed cost exceeds the current budget baseline." });
        }
        if (totalExposureAmount > baselineBudget && baselineBudget > 0m)
        {
            alerts.Add(new ProjectFinancialAlertDto { Severity = "High", Message = "Combined actual, pending, and open procurement exposure exceeds the current budget baseline." });
        }
        if (procurementPendingInspectionAmount > 0m)
        {
            alerts.Add(new ProjectFinancialAlertDto { Severity = "Medium", Message = "Received procurement is still pending inspection or acceptance." });
        }
        if (invoiceRequestedAmount < actualCost && invoiceRequestedAmount > 0m)
        {
            alerts.Add(new ProjectFinancialAlertDto { Severity = "Medium", Message = "Actual cost is ahead of invoice requests." });
        }

        return new ProjectFinancialControlSummaryDto
        {
            ProjectId = project.Id,
            EstimatedBudget = project.EstimatedBudget ?? 0m,
            ApprovedBudget = project.ApprovedBudget ?? 0m,
            BudgetBaseline = baselineBudget,
            ActualCost = actualCost,
            CommittedCost = committedCost,
            PendingCost = pendingCost,
            ForecastCost = forecastCost,
            EstimateAtCompletion = estimateAtCompletion,
            RemainingBudget = remainingBudget,
            BudgetVariance = baselineBudget - estimateAtCompletion,
            BudgetConsumptionPercent = budgetConsumption,
            ThresholdWarningPercent = warningThreshold,
            ThresholdCriticalPercent = criticalThreshold,
            ProcurementRequestedAmount = procurementRequestedAmount,
            ProcurementCommittedAmount = procurementCommittedAmount,
            ProcurementOpenCommitmentAmount = procurementOpenCommitmentAmount,
            ProcurementReceivedAmount = procurementReceivedAmount,
            ProcurementPendingInspectionAmount = procurementPendingInspectionAmount,
            TotalExposureAmount = totalExposureAmount,
            ScheduledBillingAmount = scheduledBillingAmount,
            InvoiceRequestedAmount = invoiceRequestedAmount,
            RecognizedRevenue = recognizedRevenue,
            GrossMargin = grossMargin,
            PlannedValue = plannedValue,
            EarnedValue = earnedValue,
            ScheduleVariance = scheduleVariance,
            CostVariance = costVariance,
            CostPerformanceIndex = costPerformanceIndex,
            SchedulePerformanceIndex = schedulePerformanceIndex,
            ToCompletePerformanceIndex = toCompletePerformanceIndex,
            ProfitabilityPercent = profitabilityPercent,
            HealthStatus = healthStatus,
            BudgetRevisionCount = budgetRevisions.Count,
            ForecastVersionCount = forecastVersions.Count,
            CurrentBudgetRevisionName = approvedRevision?.RevisionName,
            ActiveForecastVersionName = activeForecast?.VersionName,
            ThresholdExceeded = budgetConsumption >= warningThreshold || (baselineBudget > 0m && (estimateAtCompletion > baselineBudget || committedCost > baselineBudget)),
            Alerts = alerts
        };
    }

    public async Task<ProjectIntegrationSummaryDto> GetIntegrationSummaryAsync(Guid projectId)
    {
        var project = await GetProjectForOperationAsync(projectId, ProjectAccessOperation.View);
        await SyncProjectMaterialCostAsync(projectId);
        var assetLinks = (await _unitOfWork.Repository<ProjectAssetLink>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var resourceAllocations = (await _unitOfWork.Repository<ProjectResourceAllocation>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var externalPolicies = (await _unitOfWork.Repository<ProjectExternalAccessPolicy>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var ledgerEntries = await GetMaterialCostEntryEntitiesAsync(new[] { projectId });
        var purchaseRequisitions = (await _unitOfWork.Repository<PurchaseRequisition>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var purchaseRequisitionIds = purchaseRequisitions.Select(x => x.Id).ToHashSet();
        var purchaseOrders = (await _unitOfWork.Repository<PurchaseOrder>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.SourceRequisitionId.HasValue
                && purchaseRequisitionIds.Contains(x.SourceRequisitionId.Value)))
            .ToList();
        var purchaseOrderIds = purchaseOrders.Select(x => x.Id).ToHashSet();
        var purchaseReceipts = (await _unitOfWork.Repository<PurchaseOrderReceipt>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && purchaseOrderIds.Contains(x.PurchaseOrderId)))
            .ToList();
        var inventoryRequisitions = (await _unitOfWork.Repository<InventoryRequisition>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var issuedInventoryValue = ledgerEntries
            .Where(x => string.Equals(x.EntryType, "InventoryIssue", StringComparison.OrdinalIgnoreCase))
            .Sum(x => Math.Abs(x.Amount));
        var trackedMaterialCost = ledgerEntries
            .Where(x => x.AffectsActualCost)
            .Sum(x => x.Amount);
        var returnedInventoryValue = ledgerEntries
            .Where(x => string.Equals(x.EntryType, "InventoryReturn", StringComparison.OrdinalIgnoreCase))
            .Sum(x => Math.Abs(x.Amount));
        var netIssuedInventoryValue = decimal.Round(issuedInventoryValue - returnedInventoryValue, 2);
        var invoiceRequests = (await _unitOfWork.Repository<ProjectInvoiceRequest>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var revenueRecognitions = (await _unitOfWork.Repository<ProjectRevenueRecognition>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var materialExceptions = ledgerEntries.Count(x => x.HasMissingSourceLink || x.HasReversalGap);

        var links = new List<ProjectIntegrationLinkDto>();
        if (project.BusinessPartnerId.HasValue)
        {
            links.Add(new ProjectIntegrationLinkDto { LinkType = "BusinessPartner", Status = "Linked", Reference = project.BusinessPartnerId.Value.ToString() });
        }
        if (project.ContractId.HasValue)
        {
            links.Add(new ProjectIntegrationLinkDto { LinkType = "Contract", Status = "Linked", Reference = project.ContractId.Value.ToString() });
        }
        if (project.TenderId.HasValue)
        {
            links.Add(new ProjectIntegrationLinkDto { LinkType = "Tender", Status = "Linked", Reference = project.TenderId.Value.ToString() });
        }
        if (project.PortfolioId.HasValue)
        {
            links.Add(new ProjectIntegrationLinkDto { LinkType = "Portfolio", Status = "Linked", Reference = project.PortfolioId.Value.ToString() });
        }
        if (project.ProgramId.HasValue)
        {
            links.Add(new ProjectIntegrationLinkDto { LinkType = "Program", Status = "Linked", Reference = project.ProgramId.Value.ToString() });
        }

        links.AddRange(assetLinks.Select(x => new ProjectIntegrationLinkDto
        {
            LinkType = x.LinkType,
            Status = x.Status,
            Reference = x.MaintenanceAssetId?.ToString() ?? x.CompanyAssetId?.ToString() ?? x.JobCardId?.ToString() ?? x.Notes ?? x.Id.ToString()
        }));
        links.AddRange(purchaseRequisitions
            .OrderByDescending(x => x.RequisitionDate)
            .Select(x => new ProjectIntegrationLinkDto
            {
                LinkType = "PurchaseRequisition",
                Status = x.Status,
                Reference = $"{x.RequisitionNumber} | {x.Currency} {x.TotalAmount:N2}"
            }));
        links.AddRange(purchaseOrders
            .OrderByDescending(x => x.OrderDate)
            .Select(x => new ProjectIntegrationLinkDto
            {
                LinkType = "PurchaseOrder",
                Status = x.Status,
                Reference = $"{x.OrderNumber} | {x.Currency} {x.TotalAmount:N2}"
            }));
        links.AddRange(purchaseReceipts
            .OrderByDescending(x => x.ReceiptDate)
            .Select(x => new ProjectIntegrationLinkDto
            {
                LinkType = "PurchaseReceipt",
                Status = x.Status,
                Reference = $"{x.ReceiptNumber} | {x.ReceiptDate:yyyy-MM-dd}"
            }));
        links.AddRange(inventoryRequisitions
            .OrderByDescending(x => x.RequestDate)
            .Select(x => new ProjectIntegrationLinkDto
            {
                LinkType = "InventoryRequisition",
                Status = x.Status.ToString(),
                Reference = $"{x.RequisitionNumber} | {x.TotalValue:N2}"
            }));

        var warnings = new List<string>();
        if (!project.BusinessPartnerId.HasValue && project.ExternalPortalAccessEnabled)
        {
            warnings.Add("External portal access is enabled but no business partner is linked.");
        }
        if (!project.ContractId.HasValue && invoiceRequests.Count > 0)
        {
            warnings.Add("Invoice requests exist without a linked contract.");
        }
        if (!assetLinks.Any() && string.Equals(project.Methodology, "Capital", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Capital-style project has no linked assets.");
        }
        if (purchaseRequisitions.Any(x => string.Equals(x.Status, "Pending Approval", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Status, "Submitted", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("Procurement requisitions are pending approval for this project.");
        }
        if (purchaseOrders.Any(IsOpenPurchaseOrderStatus))
        {
            warnings.Add("Purchase orders are still open or partially received for this project.");
        }
        if (purchaseReceipts.Any(x => x.RequiresInspection && !string.Equals(x.Status, "Accepted", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("Purchase receipts are awaiting inspection or acceptance for this project.");
        }
        if (inventoryRequisitions.Any(x => x.Status == RequisitionStatus.Submitted || x.Status == RequisitionStatus.Approved))
        {
            warnings.Add("Inventory requisitions are pending issue or completion for this project.");
        }
        if (netIssuedInventoryValue > trackedMaterialCost)
        {
            warnings.Add("Inventory has been issued to this project, but some material consumption is still not reflected in project cost tracking.");
        }
        if (materialExceptions > 0)
        {
            warnings.Add("Project material ledger contains procurement or reversal exceptions that need reconciliation.");
        }

        return new ProjectIntegrationSummaryDto
        {
            ProjectId = project.Id,
            HasBusinessPartner = project.BusinessPartnerId.HasValue,
            HasContract = project.ContractId.HasValue,
            HasTender = project.TenderId.HasValue,
            HasPortfolio = project.PortfolioId.HasValue,
            HasProgram = project.ProgramId.HasValue,
            LinkedAssetCount = assetLinks.Count,
            ResourceAllocationCount = resourceAllocations.Count,
            SharedExternalPolicyCount = externalPolicies.Count,
            PurchaseRequisitionCount = purchaseRequisitions.Count,
            PendingPurchaseRequisitionCount = purchaseRequisitions.Count(x => string.Equals(x.Status, "Pending Approval", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Status, "Submitted", StringComparison.OrdinalIgnoreCase)),
            PurchaseRequisitionAmount = purchaseRequisitions.Sum(x => x.TotalAmount),
            PurchaseOrderCount = purchaseOrders.Count,
            OpenPurchaseOrderCount = purchaseOrders.Count(IsOpenPurchaseOrderStatus),
            PurchaseOrderAmount = purchaseOrders.Sum(x => x.TotalAmount),
            PurchaseReceiptCount = purchaseReceipts.Count,
            PendingPurchaseReceiptInspectionCount = purchaseReceipts.Count(x => x.RequiresInspection && !string.Equals(x.Status, "Accepted", StringComparison.OrdinalIgnoreCase)),
            InventoryRequisitionCount = inventoryRequisitions.Count,
            PendingInventoryRequisitionCount = inventoryRequisitions.Count(x => x.Status == RequisitionStatus.Submitted || x.Status == RequisitionStatus.Approved),
            InventoryRequisitionValue = inventoryRequisitions.Sum(x => x.TotalValue),
            IssuedInventoryRequisitionCount = inventoryRequisitions.Count(x => x.Status == RequisitionStatus.PartiallyIssued || x.Status == RequisitionStatus.Issued || x.Status == RequisitionStatus.Completed),
            IssuedInventoryValue = issuedInventoryValue,
            ReturnedInventoryValue = returnedInventoryValue,
            NetIssuedInventoryValue = netIssuedInventoryValue,
            InvoiceRequestCount = invoiceRequests.Count,
            RevenueRecognitionCount = revenueRecognitions.Count,
            Links = links,
            Warnings = warnings
        };
    }

    public async Task<ProjectGovernanceSummaryDto> GetGovernanceSummaryAsync(Guid projectId)
    {
        var project = await GetProjectForOperationAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var deliverables = (await _unitOfWork.Repository<ProjectDeliverable>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var risks = (await _unitOfWork.Repository<ProjectRisk>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var issues = (await _unitOfWork.Repository<ProjectIssue>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var changes = (await _unitOfWork.Repository<ProjectChangeRequest>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var actionItems = (await _unitOfWork.Repository<ProjectActionItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var timesheets = (await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var expenses = (await _unitOfWork.Repository<ProjectExpense>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var baselines = (await _unitOfWork.Repository<ProjectBaseline>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var closure = await _unitOfWork.Repository<ProjectClosure>().FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId);

        var violations = new List<ProjectPolicyViolationDto>();
        if (!project.SponsorId.HasValue)
        {
            violations.Add(new ProjectPolicyViolationDto { Area = "Initiation", Severity = "High", Message = "Project sponsor is not assigned." });
        }
        if (!project.ProjectManagerId.HasValue)
        {
            violations.Add(new ProjectPolicyViolationDto { Area = "Initiation", Severity = "High", Message = "Project manager is not assigned." });
        }
        if (!baselines.Any() && string.Equals(project.Status, ProjectStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(new ProjectPolicyViolationDto { Area = "Planning", Severity = "Medium", Message = "Project is in progress without a baseline." });
        }
        if (project.Status == ProjectStatuses.Closed && closure == null)
        {
            violations.Add(new ProjectPolicyViolationDto { Area = "Closure", Severity = "Critical", Message = "Project is closed without a closure record." });
        }
        if (deliverables.Any(x => x.ExternalSignOffRequired && string.Equals(x.Status, "Approved", StringComparison.OrdinalIgnoreCase) && !x.ExternalApprovedAt.HasValue))
        {
            violations.Add(new ProjectPolicyViolationDto { Area = "Deliverables", Severity = "High", Message = "Deliverables require external sign-off but are still awaiting acceptance." });
        }

        return new ProjectGovernanceSummaryDto
        {
            ProjectId = project.Id,
            OpenRiskCount = risks.Count(x => string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase)),
            OpenIssueCount = issues.Count(x => string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase)),
            OpenChangeRequestCount = changes.Count(x => !string.Equals(x.Status, "Rejected", StringComparison.OrdinalIgnoreCase) && !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase)),
            PendingDeliverableApprovalCount = deliverables.Count(x => string.Equals(x.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Status, "PendingExternalSignOff", StringComparison.OrdinalIgnoreCase)),
            PendingTimesheetApprovalCount = timesheets.Count(x => string.Equals(x.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase)),
            PendingExpenseApprovalCount = expenses.Count(x => string.Equals(x.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase)),
            OpenActionItemCount = actionItems.Count(x => !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase) && !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase)),
            HasClosureDraft = closure != null,
            HasApprovedClosure = closure != null && string.Equals(closure.Status, ProjectStatuses.Closed, StringComparison.OrdinalIgnoreCase),
            HasLockedBaseline = baselines.Any(x => x.IsLocked),
            Violations = violations
        };
    }

    private static decimal CalculatePlannedValue(Project project, decimal baselineBudget)
    {
        if (baselineBudget <= 0m || !project.StartDate.HasValue || !project.TargetEndDate.HasValue)
        {
            return 0m;
        }

        var startDate = project.StartDate.Value.Date;
        var endDate = project.TargetEndDate.Value.Date;
        if (endDate <= startDate)
        {
            return 0m;
        }

        var today = DateTime.UtcNow.Date;
        if (today <= startDate)
        {
            return 0m;
        }

        if (today >= endDate)
        {
            return baselineBudget;
        }

        var elapsedDays = (decimal)(today - startDate).TotalDays;
        var totalDays = (decimal)(endDate - startDate).TotalDays;
        return decimal.Round(baselineBudget * Math.Clamp(elapsedDays / Math.Max(totalDays, 1m), 0m, 1m), 2);
    }

    private static string ResolveFinancialHealthStatus(decimal? costPerformanceIndex, decimal? schedulePerformanceIndex, decimal estimateAtCompletion, decimal baselineBudget, decimal budgetConsumption, decimal criticalThreshold, decimal warningThreshold)
    {
        if (baselineBudget <= 0m)
        {
            return "NoBaseline";
        }

        if (estimateAtCompletion > baselineBudget || budgetConsumption >= criticalThreshold || (costPerformanceIndex.HasValue && costPerformanceIndex.Value < 0.9m) || (schedulePerformanceIndex.HasValue && schedulePerformanceIndex.Value < 0.9m))
        {
            return "Watch";
        }

        if (budgetConsumption >= warningThreshold || (costPerformanceIndex.HasValue && costPerformanceIndex.Value < 1m) || (schedulePerformanceIndex.HasValue && schedulePerformanceIndex.Value < 1m))
        {
            return "Attention";
        }

        return "OnTrack";
    }

    private static decimal ResolvePurchaseOrderItemUnitCost(PurchaseOrderItem purchaseOrderItem)
    {
        if (purchaseOrderItem.LandedUnitCost > 0m)
        {
            return purchaseOrderItem.LandedUnitCost;
        }

        if (purchaseOrderItem.OrderedQuantity > 0m && purchaseOrderItem.LineTotal > 0m)
        {
            return decimal.Round(purchaseOrderItem.LineTotal / purchaseOrderItem.OrderedQuantity, 4);
        }

        return purchaseOrderItem.UnitPrice;
    }

    private static decimal ResolveReceiptReceivedQuantity(PurchaseOrderReceipt receipt, PurchaseOrderReceiptItem receiptItem)
    {
        if (receiptItem.ReceivedQuantity > 0m)
        {
            return receiptItem.ReceivedQuantity;
        }

        return receiptItem.AcceptedQuantity > 0m ? receiptItem.AcceptedQuantity : 0m;
    }

    private static decimal ResolveAcceptedReceiptQuantity(PurchaseOrderReceipt receipt, PurchaseOrderReceiptItem receiptItem)
    {
        if (receiptItem.AcceptedQuantity > 0m)
        {
            return receiptItem.AcceptedQuantity;
        }

        if (!receipt.RequiresInspection)
        {
            return ResolveReceiptReceivedQuantity(receipt, receiptItem);
        }

        if (string.Equals(receipt.Status, "Accepted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(receiptItem.QualityStatus, "Passed", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveReceiptReceivedQuantity(receipt, receiptItem);
        }

        return 0m;
    }

    private static decimal ResolvePendingInspectionQuantity(PurchaseOrderReceipt receipt, PurchaseOrderReceiptItem receiptItem)
    {
        if (!receipt.RequiresInspection)
        {
            return 0m;
        }

        var receivedQuantity = ResolveReceiptReceivedQuantity(receipt, receiptItem);
        var acceptedQuantity = ResolveAcceptedReceiptQuantity(receipt, receiptItem);
        return Math.Max(receivedQuantity - acceptedQuantity, 0m);
    }

    private static bool IsPendingPurchaseRequisitionStatus(PurchaseRequisition purchaseRequisition)
        => string.Equals(purchaseRequisition.Status, "Draft", StringComparison.OrdinalIgnoreCase)
           || string.Equals(purchaseRequisition.Status, "Submitted", StringComparison.OrdinalIgnoreCase)
           || string.Equals(purchaseRequisition.Status, "Approved", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpenPurchaseOrderStatus(PurchaseOrder purchaseOrder)
        => string.Equals(purchaseOrder.Status, "Draft", StringComparison.OrdinalIgnoreCase)
           || string.Equals(purchaseOrder.Status, "Approved", StringComparison.OrdinalIgnoreCase)
           || string.Equals(purchaseOrder.Status, "Sent", StringComparison.OrdinalIgnoreCase)
           || string.Equals(purchaseOrder.Status, "Acknowledged", StringComparison.OrdinalIgnoreCase)
           || string.Equals(purchaseOrder.Status, "PartiallyReceived", StringComparison.OrdinalIgnoreCase);
}
