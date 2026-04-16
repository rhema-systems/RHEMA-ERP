using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    private async Task<ProjectCommercialDerivationContext> BuildProjectCommercialDerivationContextAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectPackage> packages,
        IReadOnlyCollection<ProjectBoqItem> boqItems,
        IReadOnlyCollection<ProjectPaymentCertificate>? paymentCertificates = null,
        IReadOnlyCollection<ProjectInterimValuation>? interimValuations = null)
    {
        var packageOrderIds = packages
            .Where(x => x.PurchaseOrderId.HasValue)
            .Select(x => x.PurchaseOrderId!.Value);
        var boqOrderItemIds = boqItems
            .Where(x => x.PurchaseOrderItemId.HasValue)
            .Select(x => x.PurchaseOrderItemId!.Value)
            .Distinct()
            .ToList();

        var initialOrderItems = boqOrderItemIds.Count == 0
            ? new List<PurchaseOrderItem>()
            : (await _unitOfWork.Repository<PurchaseOrderItem>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && boqOrderItemIds.Contains(x.Id)))
                .ToList();

        var purchaseOrderIds = packageOrderIds
            .Concat(initialOrderItems.Select(x => x.PurchaseOrderId))
            .Distinct()
            .ToList();

        var purchaseOrders = purchaseOrderIds.Count == 0
            ? new List<PurchaseOrder>()
            : (await _unitOfWork.Repository<PurchaseOrder>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && purchaseOrderIds.Contains(x.Id)))
                .ToList();

        var purchaseOrderItems = purchaseOrderIds.Count == 0
            ? initialOrderItems
            : (await _unitOfWork.Repository<PurchaseOrderItem>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && purchaseOrderIds.Contains(x.PurchaseOrderId)))
                .ToList();

        var purchaseReceipts = purchaseOrderIds.Count == 0
            ? new List<PurchaseOrderReceipt>()
            : (await _unitOfWork.Repository<PurchaseOrderReceipt>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && purchaseOrderIds.Contains(x.PurchaseOrderId)))
                .ToList();
        var purchaseReceiptIds = purchaseReceipts.Select(x => x.Id).ToList();
        var purchaseReceiptItems = purchaseReceiptIds.Count == 0
            ? new List<PurchaseOrderReceiptItem>()
            : (await _unitOfWork.Repository<PurchaseOrderReceiptItem>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && purchaseReceiptIds.Contains(x.ReceiptId)))
                .ToList();

        var contractIds = packages
            .Where(x => x.ContractId.HasValue)
            .Select(x => x.ContractId!.Value)
            .Concat(purchaseOrders.Where(x => x.ContractId.HasValue).Select(x => x.ContractId!.Value))
            .Distinct()
            .ToList();
        var contracts = contractIds.Count == 0
            ? new List<Contract>()
            : (await _unitOfWork.Repository<Contract>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && contractIds.Contains(x.Id)))
                .ToList();

        var packageCountByContractId = packages
            .Where(x => x.ContractId.HasValue)
            .GroupBy(x => x.ContractId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var packageCountByPurchaseOrderId = packages
            .Where(x => x.PurchaseOrderId.HasValue)
            .GroupBy(x => x.PurchaseOrderId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var packageCountByInventoryItemId = boqItems
            .Where(x => x.InventoryItemId.HasValue)
            .GroupBy(x => x.InventoryItemId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(x => x.ProjectPackageId)
                    .Distinct()
                    .Count());
        var boqCountByInventoryItemId = boqItems
            .Where(x => x.InventoryItemId.HasValue)
            .GroupBy(x => x.InventoryItemId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var postedMaterialCostEntries = (await _unitOfWork.Repository<ProjectMaterialCostEntry>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.ProjectId == projectId
                && x.AffectsActualCost
                && x.PostingState == "Posted"))
            .ToList();

        var approvedTimesheetActualCost = packages.Count == 1
            ? decimal.Round((await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && x.ProjectId == projectId
                    && x.Status == "Approved"))
                .Sum(x => x.CostAmount), 2)
            : 0m;
        var approvedExpenseActualCost = packages.Count == 1
            ? decimal.Round((await _unitOfWork.Repository<ProjectExpense>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && x.ProjectId == projectId
                    && x.Status == "Approved"))
                .Where(x => !IsLegacyAutoMaterialExpense(x))
                .Sum(x => x.Amount + x.TaxAmount), 2)
            : 0m;

        var certificateList = (paymentCertificates ?? await GetProjectPaymentCertificateEntitiesAsync(projectId))
            .Where(IsProjectPaymentCertificateCertifiedStatus)
            .ToList();
        var valuationList = (interimValuations ?? await GetProjectInterimValuationEntitiesAsync(projectId))
            .Where(IsProjectInterimValuationActualStatus)
            .ToList();

        return new ProjectCommercialDerivationContext(
            purchaseOrders.ToDictionary(x => x.Id),
            purchaseOrderItems.ToDictionary(x => x.Id),
            purchaseOrderItems
                .GroupBy(x => x.PurchaseOrderId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<PurchaseOrderItem>)group.ToList()),
            purchaseReceipts.ToDictionary(x => x.Id),
            purchaseReceiptItems
                .GroupBy(x => x.PurchaseOrderItemId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<PurchaseOrderReceiptItem>)group.ToList()),
            contracts.ToDictionary(x => x.Id),
            packageCountByContractId,
            packageCountByPurchaseOrderId,
            packageCountByInventoryItemId,
            boqCountByInventoryItemId,
            postedMaterialCostEntries
                .Where(x => x.InventoryItemId.HasValue)
                .GroupBy(x => x.InventoryItemId!.Value)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<ProjectMaterialCostEntry>)group.ToList()),
            packages.Count == 1 ? packages.First().Id : null,
            approvedTimesheetActualCost,
            approvedExpenseActualCost,
            certificateList,
            valuationList);
    }

    private ProjectDerivedCommercialAmounts DeriveProjectBoqCommercialAmounts(
        ProjectBoqItem entity,
        ProjectPackage? package,
        ProjectCommercialDerivationContext context)
    {
        decimal? committedAmount = null;
        decimal? actualAmount = null;

        if (entity.PurchaseOrderItemId.HasValue
            && context.PurchaseOrderItemsById.TryGetValue(entity.PurchaseOrderItemId.Value, out var orderItem)
            && context.PurchaseOrdersById.TryGetValue(orderItem.PurchaseOrderId, out var order))
        {
            if (IsPurchaseOrderCommittedStatus(order.Status))
            {
                committedAmount = ResolvePurchaseOrderItemCommittedAmount(orderItem);
            }

            actualAmount = ResolveProjectBoqPostedActualAmount(entity, context)
                ?? ResolvePurchaseOrderItemActualAmount(orderItem, context);
        }
        else
        {
            actualAmount = ResolveProjectBoqPostedActualAmount(entity, context);
        }

        return new ProjectDerivedCommercialAmounts(
            committedAmount ?? entity.CommittedAmount,
            actualAmount ?? entity.ActualAmount);
    }

    private ProjectDerivedCommercialAmounts DeriveProjectPackageCommercialAmounts(
        ProjectPackage entity,
        IReadOnlyCollection<ProjectBoqItem> rawBoqItems,
        IReadOnlyCollection<ProjectBoqItemDto> boqItems,
        ProjectCommercialDerivationContext context)
    {
        decimal? committedAmount = null;
        decimal? actualAmount = null;

        var boqProcurementItems = rawBoqItems
            .Where(x => x.PurchaseOrderItemId.HasValue)
            .ToList();
        var hasBoqCommittedAmounts = boqItems.Any(x => x.CommittedAmount.HasValue);
        var hasBoqActualAmounts = boqItems.Any(x => x.ActualAmount.HasValue);
        var boqCommittedAmount = decimal.Round(boqItems.Sum(x => x.CommittedAmount ?? 0m), 2);
        var boqActualAmount = decimal.Round(boqItems.Sum(x => x.ActualAmount ?? 0m), 2);

        decimal? procurementCommittedAmount = null;
        decimal? procurementActualAmount = null;
        Guid? procurementContractId = null;

        if (boqProcurementItems.Count > 0)
        {
            procurementCommittedAmount = boqCommittedAmount;
            procurementActualAmount = boqActualAmount;

            var linkedContractIds = boqProcurementItems
                .Where(x => x.PurchaseOrderItemId.HasValue)
                .Select(x => context.PurchaseOrderItemsById.TryGetValue(x.PurchaseOrderItemId!.Value, out var orderItem)
                    && context.PurchaseOrdersById.TryGetValue(orderItem.PurchaseOrderId, out var order)
                    && order.ContractId.HasValue
                        ? order.ContractId
                        : null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
            procurementContractId = linkedContractIds.Count == 1 ? linkedContractIds[0] : null;
        }
        else if (entity.PurchaseOrderId.HasValue
                 && context.PackageCountByPurchaseOrderId.TryGetValue(entity.PurchaseOrderId.Value, out var packageCountForOrder)
                 && packageCountForOrder == 1
                 && context.PurchaseOrdersById.TryGetValue(entity.PurchaseOrderId.Value, out var packageOrder))
        {
            if (IsPurchaseOrderCommittedStatus(packageOrder.Status))
            {
                procurementCommittedAmount = ResolvePurchaseOrderCommittedAmount(packageOrder, context);
            }

            procurementActualAmount = ResolvePurchaseOrderActualAmount(packageOrder, context);
            procurementContractId = packageOrder.ContractId;
        }

        var contractCommittedAmount = ResolvePackageContractCommittedAmount(entity, context);
        var certifiedActualAmount = ResolvePackageCertifiedActualAmount(entity, context);

        if (procurementCommittedAmount.HasValue || contractCommittedAmount.HasValue)
        {
            committedAmount = CombinePackageCommitments(
                procurementCommittedAmount,
                contractCommittedAmount,
                entity.ContractId,
                procurementContractId);
        }
        else if (hasBoqCommittedAmounts)
        {
            committedAmount = boqCommittedAmount;
        }
        else
        {
            committedAmount = entity.CommittedAmount;
        }

        var actualComponents = new List<decimal>();
        if (procurementActualAmount.HasValue)
        {
            actualComponents.Add(procurementActualAmount.Value);
        }

        if (certifiedActualAmount.HasValue)
        {
            actualComponents.Add(certifiedActualAmount.Value);
        }

        var packageOverheadActualAmount = ResolveSinglePackageOverheadActualAmount(entity, context);
        if (packageOverheadActualAmount.HasValue)
        {
            actualComponents.Add(packageOverheadActualAmount.Value);
        }

        if (actualComponents.Count > 0)
        {
            actualAmount = decimal.Round(actualComponents.Sum(), 2);
        }
        else if (hasBoqActualAmounts)
        {
            actualAmount = boqActualAmount;
        }
        else
        {
            actualAmount = entity.ActualAmount;
        }

        return new ProjectDerivedCommercialAmounts(committedAmount, actualAmount);
    }

    private decimal? ResolvePackageContractCommittedAmount(ProjectPackage entity, ProjectCommercialDerivationContext context)
    {
        if (!entity.ContractId.HasValue
            || !context.ContractsById.TryGetValue(entity.ContractId.Value, out var contract)
            || !IsContractCommittedStatus(contract.Status)
            || !context.PackageCountByContractId.TryGetValue(contract.Id, out var packageCount)
            || packageCount != 1)
        {
            return null;
        }

        return contract.ContractValue > 0m
            ? decimal.Round(contract.ContractValue, 2)
            : null;
    }

    private decimal? ResolvePackageCertifiedActualAmount(ProjectPackage entity, ProjectCommercialDerivationContext context)
    {
        var paymentCertificates = GetPackageAttributedPaymentCertificates(entity, context).ToList();
        if (paymentCertificates.Count > 0)
        {
            return decimal.Round(paymentCertificates.Sum(x => x.GrossCertifiedAmount > 0m ? x.GrossCertifiedAmount : x.NetCertifiedAmount), 2);
        }

        var interimValuations = GetPackageAttributedInterimValuations(entity, context).ToList();
        return interimValuations.Count > 0
            ? decimal.Round(interimValuations.Sum(x => x.NetValuationAmount), 2)
            : null;
    }

    private IEnumerable<ProjectPaymentCertificate> GetPackageAttributedPaymentCertificates(ProjectPackage entity, ProjectCommercialDerivationContext context)
    {
        var packageCertificates = context.PaymentCertificates
            .Where(x => x.ProjectPackageId == entity.Id);

        if (entity.ContractId.HasValue
            && context.PackageCountByContractId.TryGetValue(entity.ContractId.Value, out var packageCount)
            && packageCount == 1)
        {
            packageCertificates = packageCertificates.Concat(context.PaymentCertificates.Where(x =>
                !x.ProjectPackageId.HasValue
                && x.ContractId == entity.ContractId));
        }

        return packageCertificates
            .GroupBy(x => x.Id)
            .Select(group => group.First());
    }

    private IEnumerable<ProjectInterimValuation> GetPackageAttributedInterimValuations(ProjectPackage entity, ProjectCommercialDerivationContext context)
    {
        var packageValuations = context.InterimValuations
            .Where(x => x.ProjectPackageId == entity.Id);

        if (entity.ContractId.HasValue
            && context.PackageCountByContractId.TryGetValue(entity.ContractId.Value, out var packageCount)
            && packageCount == 1)
        {
            packageValuations = packageValuations.Concat(context.InterimValuations.Where(x =>
                !x.ProjectPackageId.HasValue
                && x.ContractId == entity.ContractId));
        }

        return packageValuations
            .GroupBy(x => x.Id)
            .Select(group => group.First());
    }

    private decimal ResolvePurchaseOrderCommittedAmount(PurchaseOrder order, ProjectCommercialDerivationContext context)
    {
        if (order.ContractValue is > 0m)
        {
            return decimal.Round(order.ContractValue.Value, 2);
        }

        if (order.TotalAmount > 0m)
        {
            return decimal.Round(order.TotalAmount, 2);
        }

        return context.PurchaseOrderItemsByOrderId.TryGetValue(order.Id, out var items)
            ? decimal.Round(items.Sum(ResolvePurchaseOrderItemCommittedAmount), 2)
            : 0m;
    }

    private decimal ResolvePurchaseOrderActualAmount(PurchaseOrder order, ProjectCommercialDerivationContext context)
    {
        if (!context.PurchaseOrderItemsByOrderId.TryGetValue(order.Id, out var items))
        {
            return 0m;
        }

        return decimal.Round(items.Sum(item => ResolvePurchaseOrderItemActualAmount(item, context)), 2);
    }

    private decimal ResolvePurchaseOrderItemActualAmount(PurchaseOrderItem orderItem, ProjectCommercialDerivationContext context)
    {
        var totalAmount = 0m;
        if (context.PurchaseReceiptItemsByOrderItemId.TryGetValue(orderItem.Id, out var receiptItems))
        {
            foreach (var receiptItem in receiptItems)
            {
                if (!context.PurchaseReceiptsById.TryGetValue(receiptItem.ReceiptId, out var receipt))
                {
                    continue;
                }

                totalAmount += ResolvePurchaseOrderReceiptAmount(receipt, receiptItem, orderItem);
            }
        }

        if (totalAmount > 0m)
        {
            return decimal.Round(totalAmount, 2);
        }

        return orderItem.ReceivedQuantity > 0m
            ? decimal.Round(ResolvePurchaseOrderItemUnitCost(orderItem) * orderItem.ReceivedQuantity, 2)
            : 0m;
    }

    private static decimal ResolvePurchaseOrderReceiptAmount(PurchaseOrderReceipt receipt, PurchaseOrderReceiptItem receiptItem, PurchaseOrderItem orderItem)
    {
        var acceptedQuantity = ResolveAcceptedReceiptQuantity(receipt, receiptItem);
        if (acceptedQuantity <= 0m)
        {
            return 0m;
        }

        return decimal.Round(ResolvePurchaseOrderItemUnitCost(orderItem) * acceptedQuantity, 2);
    }

    private static decimal ResolvePurchaseOrderItemCommittedAmount(PurchaseOrderItem orderItem)
    {
        if (orderItem.LineTotal > 0m)
        {
            return decimal.Round(orderItem.LineTotal, 2);
        }

        if (orderItem.OrderedQuantity <= 0m)
        {
            return 0m;
        }

        return decimal.Round(ResolvePurchaseOrderItemUnitCost(orderItem) * orderItem.OrderedQuantity, 2);
    }

    private static decimal? CombinePackageCommitments(
        decimal? procurementCommittedAmount,
        decimal? contractCommittedAmount,
        Guid? packageContractId,
        Guid? procurementContractId)
    {
        if (procurementCommittedAmount.HasValue && contractCommittedAmount.HasValue)
        {
            if (packageContractId.HasValue
                && procurementContractId.HasValue
                && packageContractId == procurementContractId)
            {
                return decimal.Round(Math.Max(procurementCommittedAmount.Value, contractCommittedAmount.Value), 2);
            }

            return decimal.Round(procurementCommittedAmount.Value + contractCommittedAmount.Value, 2);
        }

        return procurementCommittedAmount ?? contractCommittedAmount;
    }

    private decimal? ResolveProjectBoqPostedActualAmount(ProjectBoqItem entity, ProjectCommercialDerivationContext context)
    {
        if (!entity.InventoryItemId.HasValue
            || !context.BoqCountByInventoryItemId.TryGetValue(entity.InventoryItemId.Value, out var boqCount)
            || boqCount != 1
            || !context.PostedMaterialCostEntriesByInventoryItemId.TryGetValue(entity.InventoryItemId.Value, out var costEntries)
            || costEntries.Count == 0)
        {
            return null;
        }

        return decimal.Round(costEntries.Sum(x => x.Amount), 2);
    }

    private static decimal? ResolveSinglePackageOverheadActualAmount(ProjectPackage entity, ProjectCommercialDerivationContext context)
    {
        if (!context.SolePackageId.HasValue || context.SolePackageId.Value != entity.Id)
        {
            return null;
        }

        var amount = context.ApprovedProjectTimesheetActualCost + context.ApprovedProjectExpenseActualCost;
        return amount != 0m
            ? decimal.Round(amount, 2)
            : null;
    }

    private static bool IsPurchaseOrderCommittedStatus(string? status)
        => string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "Acknowledged", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "PartiallyReceived", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "Received", StringComparison.OrdinalIgnoreCase);

    private static bool IsContractCommittedStatus(string? status)
        => string.Equals(status, "PendingSignature", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);

    private static bool IsProjectPaymentCertificateCertifiedStatus(ProjectPaymentCertificate certificate)
        => string.Equals(certificate.Status, ProjectPaymentCertificateStatuses.Issued, StringComparison.OrdinalIgnoreCase)
           || string.Equals(certificate.Status, ProjectPaymentCertificateStatuses.Approved, StringComparison.OrdinalIgnoreCase)
           || string.Equals(certificate.Status, ProjectPaymentCertificateStatuses.Paid, StringComparison.OrdinalIgnoreCase);

    private static bool IsProjectInterimValuationActualStatus(ProjectInterimValuation valuation)
        => string.Equals(valuation.Status, ProjectInterimValuationStatuses.Certified, StringComparison.OrdinalIgnoreCase)
           || string.Equals(valuation.Status, ProjectInterimValuationStatuses.Paid, StringComparison.OrdinalIgnoreCase);

    private sealed record ProjectCommercialDerivationContext(
        IReadOnlyDictionary<Guid, PurchaseOrder> PurchaseOrdersById,
        IReadOnlyDictionary<Guid, PurchaseOrderItem> PurchaseOrderItemsById,
        IReadOnlyDictionary<Guid, IReadOnlyList<PurchaseOrderItem>> PurchaseOrderItemsByOrderId,
        IReadOnlyDictionary<Guid, PurchaseOrderReceipt> PurchaseReceiptsById,
        IReadOnlyDictionary<Guid, IReadOnlyList<PurchaseOrderReceiptItem>> PurchaseReceiptItemsByOrderItemId,
        IReadOnlyDictionary<Guid, Contract> ContractsById,
        IReadOnlyDictionary<Guid, int> PackageCountByContractId,
        IReadOnlyDictionary<Guid, int> PackageCountByPurchaseOrderId,
        IReadOnlyDictionary<Guid, int> PackageCountByInventoryItemId,
        IReadOnlyDictionary<Guid, int> BoqCountByInventoryItemId,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProjectMaterialCostEntry>> PostedMaterialCostEntriesByInventoryItemId,
        Guid? SolePackageId,
        decimal ApprovedProjectTimesheetActualCost,
        decimal ApprovedProjectExpenseActualCost,
        IReadOnlyList<ProjectPaymentCertificate> PaymentCertificates,
        IReadOnlyList<ProjectInterimValuation> InterimValuations);

    private sealed record ProjectDerivedCommercialAmounts(
        decimal? CommittedAmount,
        decimal? ActualAmount);
}
