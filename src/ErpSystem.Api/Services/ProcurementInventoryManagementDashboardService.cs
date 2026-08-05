using ErpSystem.Core.DTOs.Dashboard;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>
/// Composes the established procurement, supplier-risk, and inventory read owners into the
/// management slice of the shared enterprise dashboard. It intentionally owns no persistence
/// model and does not duplicate the statutory report engine.
/// </summary>
public sealed class ProcurementInventoryManagementDashboardService
{
    private const string SourceType = "ProcurementInventoryManagementDashboard";
    private static readonly HashSet<string> NonSpendStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Draft", "Cancelled", "Rejected"
    };
    private static readonly HashSet<string> ClosedPurchaseOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Draft", "Cancelled", "Rejected", "Closed", "Completed", "Received"
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IInventoryAnalyticsReportSource _inventoryAnalytics;
    private readonly IProcurementSupplierRiskService _supplierRisk;
    private readonly IProcurementControlEventService _controlEvents;

    public ProcurementInventoryManagementDashboardService(
        ApplicationDbContext db,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IInventoryAnalyticsReportSource inventoryAnalytics,
        IProcurementSupplierRiskService supplierRisk,
        IProcurementControlEventService controlEvents)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _inventoryAnalytics = inventoryAnalytics;
        _supplierRisk = supplierRisk;
        _controlEvents = controlEvents;
    }

    public async Task<ProcurementInventoryManagementDashboardDto> GetAsync(
        DateTime rangeStartDate,
        DateTime rangeEndDate,
        Guid? warehouseId,
        Guid? locationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        rangeStartDate = DateTime.SpecifyKind(rangeStartDate.Date, DateTimeKind.Utc);
        rangeEndDate = DateTime.SpecifyKind(rangeEndDate.Date, DateTimeKind.Utc);
        if (rangeStartDate > rangeEndDate)
            throw new ArgumentException("The management dashboard start date cannot be later than its end date.");
        if (locationId.HasValue && !warehouseId.HasValue)
            throw new ArgumentException("A warehouse is required when a warehouse location is selected.");

        var rangeEndExclusive = rangeEndDate.AddDays(1);
        var correlation = $"management-dashboard-{Guid.NewGuid():N}";
        await EnsureCapabilityAsync("procurement.reports.read", null, null, false, correlation, cancellationToken);
        if (warehouseId.HasValue)
            await EnsureCapabilityAsync("procurement.inventory.read", warehouseId, locationId, true,
                correlation, cancellationToken);

        var periodOrders = await _db.PurchaseOrders.AsNoTracking()
            .Include(order => order.SourceRequisition)
            .Include(order => order.Items).ThenInclude(item => item.InventoryItem).ThenInclude(item => item!.Category)
            .Include(order => order.Receipts).ThenInclude(receipt => receipt.Items)
            .Where(order => order.TenantId == _currentUser.TenantId && !order.IsDeleted &&
                            order.OrderDate >= rangeStartDate && order.OrderDate < rangeEndExclusive)
            .ToListAsync(cancellationToken);
        periodOrders = ApplyScope(periodOrders, warehouseId, locationId).ToList();

        var contractOrders = await _db.PurchaseOrders.AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.Receipts).ThenInclude(receipt => receipt.Items)
            .Where(order => order.TenantId == _currentUser.TenantId && !order.IsDeleted &&
                            order.ContractId.HasValue && order.OrderDate < rangeEndExclusive)
            .ToListAsync(cancellationToken);
        contractOrders = ApplyScope(contractOrders, warehouseId, locationId)
            .Where(order => !NonSpendStatuses.Contains(order.Status))
            .ToList();

        var contracts = await _db.Contracts.AsNoTracking()
            .Include(contract => contract.BusinessPartner)
            .Include(contract => contract.Amendments)
            .Where(contract => contract.TenantId == _currentUser.TenantId && !contract.IsDeleted &&
                               (contract.StartDate ?? contract.CreatedAt) < rangeEndExclusive &&
                               (!contract.EndDate.HasValue || contract.EndDate.Value >= rangeStartDate))
            .ToListAsync(cancellationToken);
        if (warehouseId.HasValue || locationId.HasValue)
        {
            var scopedContractIds = contractOrders.Where(order => order.ContractId.HasValue)
                .Select(order => order.ContractId!.Value).ToHashSet();
            contracts = contracts.Where(contract => scopedContractIds.Contains(contract.Id)).ToList();
        }

        var inventory = await _inventoryAnalytics.GetReportSourceAsync(
            warehouseId, null, 90, 180, 90, cancellationToken);
        var inventoryRows = inventory.Items
            .Where(item => !locationId.HasValue || item.LocationId == locationId.Value)
            .ToList();

        var riskRows = await LoadSupplierRiskAsync(rangeStartDate, rangeEndExclusive, cancellationToken);
        var spendOrders = periodOrders.Where(order => !NonSpendStatuses.Contains(order.Status)).ToList();

        var result = new ProcurementInventoryManagementDashboardDto
        {
            RangeStartDate = rangeStartDate,
            RangeEndDate = rangeEndDate,
            WarehouseId = warehouseId,
            LocationId = locationId,
            InventoryAsOfUtc = inventory.AsOfUtc,
            SpendByCurrency = Money(spendOrders.GroupBy(order => NormalizeCurrency(order.Currency)),
                group => group.Key, group => group.Sum(order => order.TotalAmount)),
            SpendByCategory = BuildSpendByCategory(spendOrders),
            SpendByDepartment = BuildSpendByDepartment(spendOrders),
            OpenPurchaseOrders = BuildOpenPurchaseOrders(periodOrders, rangeEndDate),
            Contracts = BuildContracts(contracts, contractOrders, rangeEndDate),
            Inventory = BuildInventory(inventoryRows),
            CycleTime = BuildCycleTime(spendOrders),
            ServiceLevel = BuildServiceLevel(spendOrders),
            SupplierRisk = BuildSupplierRisk(riskRows, rangeEndExclusive)
        };

        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("management-dashboard", _currentUser.TenantId,
                _currentUser.UserId, correlation),
            EventType = SourceType,
            Action = "Read",
            Result = ProcurementControlEventResult.Allowed,
            RuleCode = "RPT-003",
            SourceType = SourceType,
            SourceReference = $"{rangeStartDate:yyyyMMdd}:{rangeEndDate:yyyyMMdd}",
            Reason = "The protected management dashboard was generated from authoritative tenant data.",
            InputValues = new { rangeStartDate, rangeEndDate, warehouseId, locationId },
            ResultValues = new
            {
                SpendOrderCount = spendOrders.Count,
                result.OpenPurchaseOrders.Count,
                result.Contracts.ActiveCount,
                result.Inventory.ItemLocationCount,
                result.SupplierRisk.AssessedSupplierCount
            },
            CorrelationId = correlation,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);

        return result;
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        Guid? warehouseId,
        Guid? locationId,
        bool requireLocationScope,
        string correlation,
        CancellationToken cancellationToken)
    {
        var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            WarehouseId = warehouseId,
            LocationId = locationId,
            RequireLocationScope = requireLocationScope,
            SourceType = SourceType,
            SourceReference = correlation
        }, correlation, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementAccessAuthorizationException(decision.Message);
    }

    private static IEnumerable<PurchaseOrder> ApplyScope(
        IEnumerable<PurchaseOrder> orders,
        Guid? warehouseId,
        Guid? locationId) => orders.Where(order =>
        (!warehouseId.HasValue || order.DeliveryWarehouseId == warehouseId.Value ||
         order.Items.Any(item => item.WarehouseId == warehouseId.Value)) &&
        (!locationId.HasValue || order.Receipts.Any(receipt => !receipt.IsDeleted &&
            receipt.Items.Any(item => !item.IsDeleted && item.LocationId == locationId.Value))));

    private static List<ManagementDashboardMoneyPointDto> BuildSpendByCategory(
        IReadOnlyCollection<PurchaseOrder> orders) => orders
        .SelectMany(order => order.Items.Where(item => !item.IsDeleted).Select(item => new
        {
            Label = item.InventoryItem?.Category?.Name ?? "Unclassified",
            Currency = NormalizeCurrency(order.Currency),
            Amount = LineValue(item)
        }))
        .GroupBy(item => new { item.Label, item.Currency })
        .Select(group => new ManagementDashboardMoneyPointDto
        {
            Label = group.Key.Label,
            Currency = group.Key.Currency,
            Amount = Round(group.Sum(item => item.Amount)),
            Count = group.Count()
        })
        .OrderByDescending(item => item.Amount).ThenBy(item => item.Label).ToList();

    private static List<ManagementDashboardMoneyPointDto> BuildSpendByDepartment(
        IReadOnlyCollection<PurchaseOrder> orders) => orders
        .GroupBy(order => new
        {
            Label = string.IsNullOrWhiteSpace(order.SourceRequisition?.Department)
                ? "Unassigned"
                : order.SourceRequisition.Department!.Trim(),
            Currency = NormalizeCurrency(order.Currency)
        })
        .Select(group => new ManagementDashboardMoneyPointDto
        {
            Label = group.Key.Label,
            Currency = group.Key.Currency,
            Amount = Round(group.Sum(order => order.TotalAmount)),
            Count = group.Count()
        })
        .OrderByDescending(item => item.Amount).ThenBy(item => item.Label).ToList();

    private static ManagementDashboardOpenPurchaseOrderDto BuildOpenPurchaseOrders(
        IReadOnlyCollection<PurchaseOrder> periodOrders,
        DateTime rangeEndDate)
    {
        var open = periodOrders.Where(order => !ClosedPurchaseOrderStatuses.Contains(order.Status)).ToList();
        return new ManagementDashboardOpenPurchaseOrderDto
        {
            Count = open.Count,
            OverdueCount = open.Count(order => (order.PromisedDate ?? order.RequiredDate) is DateTime due &&
                                                due.Date < rangeEndDate.Date),
            OrderedValueByCurrency = Money(open.GroupBy(order => NormalizeCurrency(order.Currency)),
                group => group.Key, group => group.Sum(order => order.TotalAmount)),
            RemainingValueByCurrency = Money(open.GroupBy(order => NormalizeCurrency(order.Currency)),
                group => group.Key, group => group.Sum(RemainingValue))
        };
    }

    private static ManagementDashboardContractDto BuildContracts(
        IReadOnlyCollection<Contract> contracts,
        IReadOnlyCollection<PurchaseOrder> contractOrders,
        DateTime rangeEndDate)
    {
        var rows = contracts.Select(contract =>
        {
            var approvedValue = contract.ContractValue + (contract.Amendments
                .Where(amendment => !amendment.IsDeleted && amendment.Status == "Approved")
                .Sum(amendment => amendment.ValueChange) ?? 0m);
            var used = contractOrders.Where(order => order.ContractId == contract.Id).Sum(order => order.TotalAmount);
            var utilization = approvedValue == 0m ? 0m : Round(used / approvedValue * 100m);
            return new { Contract = contract, ApprovedValue = approvedValue, Used = used, Utilization = utilization };
        }).ToList();
        var active = rows.Where(row => string.Equals(row.Contract.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
                                      (!string.Equals(row.Contract.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                                       !string.Equals(row.Contract.Status, "Terminated", StringComparison.OrdinalIgnoreCase) &&
                                       (!row.Contract.EndDate.HasValue || row.Contract.EndDate.Value.Date >= rangeEndDate.Date)))
            .ToList();
        var expiryCutoff = rangeEndDate.Date.AddDays(90);
        var expiring = active.Where(row => row.Contract.EndDate.HasValue &&
                                          row.Contract.EndDate.Value.Date >= rangeEndDate.Date &&
                                          row.Contract.EndDate.Value.Date <= expiryCutoff)
            .OrderBy(row => row.Contract.EndDate).ToList();
        return new ManagementDashboardContractDto
        {
            ActiveCount = active.Count,
            ExpiringWithin90DaysCount = expiring.Count,
            AverageUtilizationPercent = active.Count == 0 ? 0m : Round(active.Average(row => row.Utilization)),
            ContractValueByCurrency = Money(active.GroupBy(row => NormalizeCurrency(row.Contract.Currency)),
                group => group.Key, group => group.Sum(row => row.ApprovedValue)),
            UtilizedValueByCurrency = Money(active.GroupBy(row => NormalizeCurrency(row.Contract.Currency)),
                group => group.Key, group => group.Sum(row => row.Used)),
            ExpiringContracts = expiring.Take(10).Select(row => new ManagementDashboardContractExpiryDto
            {
                ContractId = row.Contract.Id,
                ContractNumber = row.Contract.ContractNumber,
                ContractTitle = row.Contract.ContractTitle,
                SupplierName = row.Contract.BusinessPartner?.PartnerName ?? string.Empty,
                EndDate = row.Contract.EndDate!.Value,
                DaysToExpiry = Math.Max(0, (row.Contract.EndDate.Value.Date - rangeEndDate.Date).Days),
                UtilizationPercent = row.Utilization
            }).ToList()
        };
    }

    private static ManagementDashboardInventoryDto BuildInventory(
        IReadOnlyCollection<InventoryItemLocationAnalyticsDto> rows) => new()
    {
        StockValue = Round(rows.Sum(item => item.InventoryValue)),
        QuantityOnHand = Round(rows.Sum(item => item.QuantityOnHand)),
        ItemLocationCount = rows.Count,
        StockoutCount = rows.Count(item => item.ActivityClassification == InventoryActivityClassification.Stockout),
        ValueByCategory = InventoryValues(rows.GroupBy(item => item.CategoryName), group => group.Key),
        ValueByWarehouse = InventoryValues(rows.GroupBy(item => item.WarehouseName), group => group.Key)
    };

    private static ManagementDashboardCycleTimeDto BuildCycleTime(IReadOnlyCollection<PurchaseOrder> orders)
    {
        var requisitionToOrder = orders.Where(order => order.SourceRequisition is not null &&
                                                       order.OrderDate >= order.SourceRequisition.RequisitionDate)
            .Select(order => (decimal)(order.OrderDate - order.SourceRequisition!.RequisitionDate).TotalDays)
            .ToList();
        var orderToReceipt = orders.Select(order => new
            {
                order.OrderDate,
                ReceiptDate = order.Receipts.Where(receipt => !receipt.IsDeleted &&
                    !string.Equals(receipt.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(receipt => receipt.ReceiptDate).Select(receipt => (DateTime?)receipt.ReceiptDate).FirstOrDefault()
            })
            .Where(item => item.ReceiptDate.HasValue && item.ReceiptDate.Value >= item.OrderDate)
            .Select(item => (decimal)(item.ReceiptDate!.Value - item.OrderDate).TotalDays).ToList();
        return new ManagementDashboardCycleTimeDto
        {
            RequisitionToPurchaseOrderSampleCount = requisitionToOrder.Count,
            AverageRequisitionToPurchaseOrderDays = Average(requisitionToOrder),
            PurchaseOrderToReceiptSampleCount = orderToReceipt.Count,
            AveragePurchaseOrderToReceiptDays = Average(orderToReceipt)
        };
    }

    private static ManagementDashboardServiceLevelDto BuildServiceLevel(IReadOnlyCollection<PurchaseOrder> orders)
    {
        var completed = orders.Where(IsFullyReceived).Select(order => new
        {
            Order = order,
            Due = order.PromisedDate ?? order.RequiredDate,
            Delivered = order.ReceivedDate ?? order.Receipts.Where(receipt => !receipt.IsDeleted)
                .OrderByDescending(receipt => receipt.ReceiptDate).Select(receipt => (DateTime?)receipt.ReceiptDate).FirstOrDefault()
        }).Where(item => item.Due.HasValue && item.Delivered.HasValue).ToList();
        var accepted = completed.Sum(item => item.Order.Receipts.Where(receipt => !receipt.IsDeleted)
            .SelectMany(receipt => receipt.Items.Where(line => !line.IsDeleted)).Sum(line => line.AcceptedQuantity));
        var ordered = completed.Sum(item => item.Order.Items.Where(line => !line.IsDeleted)
            .Sum(line => line.OrderedQuantity));
        var onTime = completed.Count(item => item.Delivered!.Value.Date <= item.Due!.Value.Date);
        return new ManagementDashboardServiceLevelDto
        {
            EligibleOrderCount = completed.Count,
            OnTimeOrderCount = onTime,
            OnTimeDeliveryPercent = completed.Count == 0 ? null : Round((decimal)onTime / completed.Count * 100m),
            AcceptedFillRatePercent = ordered == 0m ? null : Round(Math.Min(100m, accepted / ordered * 100m))
        };
    }

    private static ManagementDashboardSupplierRiskDto BuildSupplierRisk(
        IReadOnlyCollection<ProcurementSupplierRiskListItemDto> rows,
        DateTime rangeEndExclusive) => new()
    {
        AssessedSupplierCount = rows.Count,
        HighOrCriticalSupplierCount = rows.Count(item =>
            string.Equals(item.RiskBand, "High", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.RiskBand, "Critical", StringComparison.OrdinalIgnoreCase)),
        AwardBlockedSupplierCount = rows.Count(item => item.AwardBlocked),
        OpenAlertCount = rows.Sum(item => item.OpenAlertCount + item.EscalatedAlertCount),
        OverdueAssessmentCount = rows.Count(item => item.NextReviewDueAtUtc < rangeEndExclusive),
        ByRiskBand = rows.GroupBy(item => string.IsNullOrWhiteSpace(item.RiskBand) ? "Unrated" : item.RiskBand!)
            .Select(group => new ManagementDashboardCountPointDto { Label = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count).ThenBy(item => item.Label).ToList()
    };

    private async Task<List<ProcurementSupplierRiskListItemDto>> LoadSupplierRiskAsync(
        DateTime rangeStartDate,
        DateTime rangeEndExclusive,
        CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var pageNumber = 1;
        var rows = new List<ProcurementSupplierRiskListItemDto>();
        ProcurementSupplierRiskPageDto page;
        do
        {
            page = await _supplierRisk.SearchAsync(new ProcurementSupplierRiskSearchRequest
            {
                Page = pageNumber,
                PageSize = pageSize
            }, cancellationToken);
            rows.AddRange(page.Items);
            pageNumber++;
        } while (rows.Count < page.TotalCount);
        return rows.Where(item => item.AssessedAtUtc >= rangeStartDate &&
                                  item.AssessedAtUtc < rangeEndExclusive).ToList();
    }

    private static List<ManagementDashboardMoneyPointDto> Money<T>(
        IEnumerable<IGrouping<string, T>> groups,
        Func<IGrouping<string, T>, string> label,
        Func<IGrouping<string, T>, decimal> amount) => groups
        .Select(group => new ManagementDashboardMoneyPointDto
        {
            Label = label(group),
            Currency = group.Key,
            Amount = Round(amount(group)),
            Count = group.Count()
        }).OrderByDescending(item => item.Amount).ThenBy(item => item.Label).ToList();

    private static List<ManagementDashboardInventoryValuePointDto> InventoryValues(
        IEnumerable<IGrouping<string, InventoryItemLocationAnalyticsDto>> groups,
        Func<IGrouping<string, InventoryItemLocationAnalyticsDto>, string> label) => groups
        .Select(group => new ManagementDashboardInventoryValuePointDto
        {
            Label = label(group),
            Value = Round(group.Sum(item => item.InventoryValue)),
            QuantityOnHand = Round(group.Sum(item => item.QuantityOnHand)),
            ItemLocationCount = group.Count()
        }).OrderByDescending(item => item.Value).ThenBy(item => item.Label).ToList();

    private static decimal RemainingValue(PurchaseOrder order) => Round(order.Items
        .Where(item => !item.IsDeleted)
        .Sum(item => Math.Max(0m, item.RemainingQuantity > 0m
            ? item.RemainingQuantity
            : item.OrderedQuantity - item.ReceivedQuantity) * item.UnitPrice));

    private static decimal LineValue(PurchaseOrderItem item) =>
        item.LineTotal != 0m ? item.LineTotal : item.OrderedQuantity * item.UnitPrice;

    private static bool IsFullyReceived(PurchaseOrder order) =>
        string.Equals(order.Status, "Received", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(order.Status, "Closed", StringComparison.OrdinalIgnoreCase) ||
        order.Items.Where(item => !item.IsDeleted).Any() && order.Items.Where(item => !item.IsDeleted)
            .All(item => item.ReceivedQuantity >= item.OrderedQuantity);

    private static decimal? Average(IReadOnlyCollection<decimal> values) =>
        values.Count == 0 ? null : Round(values.Average());

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? "N/A" : currency.Trim().ToUpperInvariant();

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private void EnsureActor()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.IsExternalUser ||
            _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementAccessAuthorizationException(
                "An authenticated internal tenant actor is required for the management dashboard.");
    }
}
