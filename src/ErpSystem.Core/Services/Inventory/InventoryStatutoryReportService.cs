using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public sealed class InventoryStatutoryReportService : IInventoryStatutoryReportService
{
    private const string SourceType = "InventoryStatutoryReport";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IInventoryAnalyticsReportSource _analytics;
    private readonly IPhysicalCountService _physicalCounts;
    private readonly IInventoryValuationReconciliationReportSource _valuation;
    private readonly IInventoryDisposalReportSource _disposals;

    public InventoryStatutoryReportService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IInventoryAnalyticsReportSource analytics,
        IPhysicalCountService physicalCounts,
        IInventoryValuationReconciliationReportSource valuation,
        IInventoryDisposalReportSource disposals)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _access = access;
        _analytics = analytics;
        _physicalCounts = physicalCounts;
        _valuation = valuation;
        _disposals = disposals;
    }

    public bool CanHandle(string? reportQuery) => InventoryStatutoryReportCatalogue.Resolve(reportQuery) is not null;

    public bool OwnsIdentifier(string? reportQuery) =>
        !string.IsNullOrWhiteSpace(reportQuery) &&
        reportQuery.StartsWith(InventoryStatutoryReportCatalogue.QueryPrefix, StringComparison.OrdinalIgnoreCase);

    public string? ResolveCode(string? reportQuery) => InventoryStatutoryReportCatalogue.Resolve(reportQuery)?.Code;

    public async Task<bool> CanReadAsync(bool isAdministrator, CancellationToken cancellationToken = default)
    {
        if (_currentUser.TenantId == Guid.Empty) return false;
        if (isAdministrator) return true;
        try
        {
            return (await _access.CheckCapabilityAsync(Capability(
                InventoryStatutoryReportCatalogue.ReadPermission, "catalogue"), Correlation(), cancellationToken)).Allowed;
        }
        catch (ProcurementAccessValidationException) { return false; }
        catch (ProcurementAccessNotFoundException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public async Task AuthorizeExportAsync(
        string reportQuery,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantContext();
        var definition = Resolve(reportQuery);
        await EnsureCapabilityAsync(InventoryStatutoryReportCatalogue.ExportPermission,
            definition.Code, isAdministrator, cancellationToken);
    }

    public async Task<ReportResultDto> ExecuteAsync(
        string reportQuery,
        ExecuteReportDto request,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantContext();
        var definition = Resolve(reportQuery);
        await EnsureCapabilityAsync(InventoryStatutoryReportCatalogue.ReadPermission,
            definition.Code, isAdministrator, cancellationToken);
        var filters = ReportFilters.Parse(request);

        return definition.Code switch
        {
            InventoryStatutoryReportCatalogue.BalanceCode =>
                await ExecuteBalanceAsync(definition, filters, request, cancellationToken),
            InventoryStatutoryReportCatalogue.MovementCode =>
                await ExecuteMovementAsync(definition, filters, request, cancellationToken),
            InventoryStatutoryReportCatalogue.AgeingCode =>
                await ExecuteAgeingAsync(definition, filters, request, cancellationToken),
            InventoryStatutoryReportCatalogue.ReorderCode =>
                await ExecuteReorderAsync(definition, filters, request, cancellationToken),
            InventoryStatutoryReportCatalogue.CountVarianceCode =>
                await ExecuteCountVarianceAsync(definition, filters, request, cancellationToken),
            InventoryStatutoryReportCatalogue.ValuationGlCode =>
                await ExecuteValuationAsync(definition, filters, request, isAdministrator, cancellationToken),
            InventoryStatutoryReportCatalogue.SlowNonMovingCode =>
                await ExecuteSlowNonMovingAsync(definition, filters, request, cancellationToken),
            InventoryStatutoryReportCatalogue.ExpiryCode =>
                await ExecuteExpiryAsync(definition, filters, request, cancellationToken),
            InventoryStatutoryReportCatalogue.DisposalCode =>
                await ExecuteDisposalAsync(definition, filters, request, cancellationToken),
            _ => throw new InvalidOperationException("The inventory system report is not implemented.")
        };
    }

    private async Task<InventoryAnalyticsDto> AnalyticsAsync(ReportFilters filters, CancellationToken cancellationToken) =>
        await _analytics.GetReportSourceAsync(filters.WarehouseId, filters.CategoryId,
            filters.SlowMovingDays, filters.NonMovingDays, filters.ExpiryWarningDays, cancellationToken);

    private async Task<ReportResultDto> ExecuteBalanceAsync(
        InventorySystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var source = await AnalyticsAsync(filters, cancellationToken);
        var rows = source.Items.OrderBy(item => item.ItemCode).ThenBy(item => item.WarehouseCode)
            .ThenBy(item => item.LocationCode).Select(item => Row(
                ("ItemCode", item.ItemCode), ("ItemName", item.ItemName), ("Category", item.CategoryName),
                ("UnitOfMeasure", item.UnitOfMeasure), ("WarehouseCode", item.WarehouseCode),
                ("WarehouseName", item.WarehouseName), ("LocationCode", item.LocationCode),
                ("LocationName", item.LocationName), ("QuantityOnHand", item.QuantityOnHand),
                ("QuantityAllocated", item.QuantityAllocated), ("QuantityAvailable", item.QuantityAvailable),
                ("QuantityOnOrder", item.QuantityOnOrder), ("AverageUnitCost", item.AverageUnitCost),
                ("InventoryValue", item.InventoryValue), ("LastMovementDate", item.LastMovementDateUtc),
                ("LastCountDate", item.LastCountDateUtc))).ToList();
        return PageRows(rows, definition, request, filters, source.AsOfUtc);
    }

    private async Task<ReportResultDto> ExecuteMovementAsync(
        InventorySystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var query = Query<StockMovement>();
        if (filters.StartUtc.HasValue) query = query.Where(item => item.MovementDate >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) query = query.Where(item => item.MovementDate < filters.EndExclusiveUtc.Value);
        if (filters.WarehouseId.HasValue) query = query.Where(item => item.WarehouseId == filters.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(filters.MovementType))
            query = query.Where(item => item.MovementType == filters.MovementType);

        var scopes = await query.Select(item => new InventoryScope(item.WarehouseId, item.LocationId))
            .Distinct().ToListAsync(cancellationToken);
        var allowed = await ReadableScopesAsync(scopes, "movement-register", cancellationToken);
        if (scopes.Count > 0 && allowed.Count == 0)
            throw new UnauthorizedAccessException("The current actor has no assigned inventory scope for this report.");
        if (allowed.Count > 0) query = query.Where(BuildScopePredicate<StockMovement>(allowed));

        var projected = query.OrderByDescending(item => item.MovementDate).ThenByDescending(item => item.Id)
            .Select(item => new MovementRow
            {
                MovementDate = item.MovementDate,
                ItemCode = item.InventoryItem.ItemCode,
                ItemName = item.InventoryItem.Name,
                WarehouseCode = item.Warehouse == null ? string.Empty : item.Warehouse.Code,
                WarehouseName = item.Warehouse == null ? string.Empty : item.Warehouse.Name,
                LocationCode = item.Location == null ? null : item.Location.LocationCode,
                LocationName = item.Location == null ? null : item.Location.Name,
                MovementType = item.MovementType,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                TotalValue = item.TotalValue,
                RunningBalance = item.RunningBalance,
                ReferenceType = item.ReferenceType,
                ReferenceNumber = item.ReferenceNumber,
                LotNumber = item.LotNumber,
                BatchNumber = item.BatchNumber,
                SerialNumber = item.SerialNumber,
                ProcessedBy = item.ProcessedBy == null
                    ? string.Empty
                    : (item.ProcessedBy.FirstName + " " + item.ProcessedBy.LastName).Trim()
            });
        return await PageQueryAsync(projected, definition, request, filters, item => Row(
            ("MovementDate", item.MovementDate), ("ItemCode", item.ItemCode), ("ItemName", item.ItemName),
            ("WarehouseCode", item.WarehouseCode), ("WarehouseName", item.WarehouseName),
            ("LocationCode", item.LocationCode), ("LocationName", item.LocationName),
            ("MovementType", item.MovementType), ("Quantity", item.Quantity), ("UnitCost", item.UnitCost),
            ("TotalValue", item.TotalValue), ("RunningBalance", item.RunningBalance),
            ("ReferenceType", item.ReferenceType.ToString()), ("ReferenceNumber", item.ReferenceNumber),
            ("LotNumber", item.LotNumber), ("BatchNumber", item.BatchNumber),
            ("SerialNumber", item.SerialNumber), ("ProcessedBy", item.ProcessedBy)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteAgeingAsync(
        InventorySystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var source = await AnalyticsAsync(filters, cancellationToken);
        var rows = source.Items.SelectMany(item => item.AgeingBands
                .Where(band => band.Quantity != 0m || band.Value != 0m)
                .Select(band => new { item, band }))
            .OrderBy(item => item.item.ItemCode).ThenBy(item => item.item.WarehouseCode)
            .ThenBy(item => item.item.LocationCode).ThenBy(item => item.band.FromDays)
            .Select(value => Row(("ItemCode", value.item.ItemCode), ("ItemName", value.item.ItemName),
                ("WarehouseCode", value.item.WarehouseCode), ("WarehouseName", value.item.WarehouseName),
                ("LocationCode", value.item.LocationCode), ("LocationName", value.item.LocationName),
                ("BandKey", value.band.Key), ("AgeingBand", value.band.Label),
                ("FromDays", value.band.FromDays), ("ToDays", value.band.ToDays),
                ("Quantity", value.band.Quantity), ("Value", value.band.Value), ("AsOfUtc", source.AsOfUtc)))
            .ToList();
        return PageRows(rows, definition, request, filters, source.AsOfUtc);
    }

    private async Task<ReportResultDto> ExecuteReorderAsync(
        InventorySystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var source = await AnalyticsAsync(filters, cancellationToken);
        var candidates = source.Items.Where(item => item.ReplenishmentCandidate ||
                                                     item.QuantityAvailable <= item.ReorderLevel);
        if (!string.IsNullOrWhiteSpace(filters.Status))
            candidates = candidates.Where(item => string.Equals(item.ReplenishmentRecommendationStatus,
                filters.Status, StringComparison.OrdinalIgnoreCase));
        var rows = candidates.OrderBy(item => item.ItemCode).ThenBy(item => item.WarehouseCode)
            .Select(item => Row(("ItemCode", item.ItemCode), ("ItemName", item.ItemName),
                ("WarehouseCode", item.WarehouseCode), ("WarehouseName", item.WarehouseName),
                ("LocationCode", item.LocationCode), ("LocationName", item.LocationName),
                ("QuantityAvailable", item.QuantityAvailable), ("QuantityOnOrder", item.QuantityOnOrder),
                ("ReorderLevel", item.ReorderLevel), ("AverageDailyDemand", item.AverageDailyDemand),
                ("EstimatedDaysOfCover", item.EstimatedDaysOfCover), ("CurrentStockoutDays", item.CurrentStockoutDays),
                ("RecommendationNumber", item.ReplenishmentRecommendationNumber),
                ("RecommendationStatus", item.ReplenishmentRecommendationStatus),
                ("PurchaseRequisitionNumber", item.ReplenishmentPurchaseRequisitionNumber),
                ("RecommendedAction", item.RecommendedAction))).ToList();
        return PageRows(rows, definition, request, filters, source.AsOfUtc);
    }

    private async Task<ReportResultDto> ExecuteCountVarianceAsync(
        InventorySystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var from = filters.StartUtc ?? DateTime.UtcNow.AddYears(-100);
        var to = filters.EndExclusiveUtc ?? DateTime.UtcNow.AddDays(1);
        var counts = (await _physicalCounts.GetAllAsync(from, to)).Where(item =>
            !filters.WarehouseId.HasValue || item.WarehouseId == filters.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status))
            counts = counts.Where(item => item.Status.Equals(filters.Status, StringComparison.OrdinalIgnoreCase));

        var rows = new List<Dictionary<string, object>>();
        foreach (var count in counts.OrderByDescending(item => item.CountDate))
        {
            var detail = await _physicalCounts.GetByIdAsync(count.Id);
            if (detail is null) continue;
            foreach (var item in detail.Items.Where(item => item.VarianceQuantity != 0m || item.RequiresRecount))
            {
                rows.Add(Row(("CountNumber", detail.CountNumber), ("CountType", detail.CountType.ToString()),
                    ("Status", detail.Status), ("CountDate", detail.CountDate), ("WarehouseName", detail.WarehouseName),
                    ("LocationName", detail.LocationName), ("ItemCode", item.ItemCode), ("ItemName", item.ItemName),
                    ("ItemLocation", item.LocationName), ("SystemQuantity", item.SystemQuantity),
                    ("CountedQuantity", item.CountedQuantity), ("VarianceQuantity", item.VarianceQuantity),
                    ("UnitCost", item.VarianceQuantity == 0m ? 0m : item.VarianceValue / item.VarianceQuantity),
                    ("VarianceValue", item.VarianceValue), ("RequiresRecount", item.RequiresRecount),
                    ("RecountedQuantity", item.RecountedQuantity), ("InvestigationNotes", item.InvestigationNotes),
                    ("StoresApprovedAt", detail.StoresApprovedAtUtc), ("FinanceApprovedAt", detail.FinanceApprovedAtUtc),
                    ("AuditAttestedAt", detail.AuditAttestedAtUtc), ("StockAdjustmentId", detail.StockAdjustmentId)));
            }
        }
        return PageRows(rows, definition, request, filters, DateTime.UtcNow);
    }

    private async Task<ReportResultDto> ExecuteValuationAsync(
        InventorySystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        bool isAdministrator, CancellationToken cancellationToken)
    {
        if (!isAdministrator) await EnsureTenantWideInventoryAccessAsync(cancellationToken);
        InventoryValuationReconciliationStatus? status = null;
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<InventoryValuationReconciliationStatus>(filters.Status, true, out var parsed)) status = parsed;
        var source = await _valuation.GetReportSourceAsync(filters.FiscalPeriodId, status, cancellationToken);
        var filtered = source.Where(item => !filters.StartUtc.HasValue || item.GeneratedAtUtc >= filters.StartUtc.Value)
            .Where(item => !filters.EndExclusiveUtc.HasValue || item.GeneratedAtUtc < filters.EndExclusiveUtc.Value);
        var rows = filtered.Select(item => Row(("ReconciliationNumber", item.ReconciliationNumber),
            ("FiscalPeriodCode", item.FiscalPeriodCode), ("Status", item.Status.ToString()),
            ("CutoffDate", item.CutoffDateUtc), ("Currency", item.FunctionalCurrencyCode),
            ("ControlAccountCode", item.InventoryControlAccountCode), ("ReceiptInventoryValue", item.ReceiptInventoryValue),
            ("LandedCostInventoryValue", item.LandedCostInventoryValue),
            ("InventorySubledgerValue", item.InventorySubledgerValue), ("BalanceCacheValue", item.InventoryBalanceCacheValue),
            ("GeneralLedgerValue", item.GeneralLedgerValue), ("ReconciliationVariance", item.ReconciliationVariance),
            ("ToleranceAmount", item.ToleranceAmount), ("ExceptionCount", item.ExceptionCount),
            ("GeneratedAt", item.GeneratedAtUtc), ("FrozenAt", item.FrozenAtUtc),
            ("SnapshotHash", item.SnapshotHash))).ToList();
        return PageRows(rows, definition, request, filters, DateTime.UtcNow);
    }

    private async Task<ReportResultDto> ExecuteSlowNonMovingAsync(
        InventorySystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var source = await AnalyticsAsync(filters, cancellationToken);
        var candidates = source.Items.Where(item => item.ActivityClassification is
            InventoryActivityClassification.SlowMoving or InventoryActivityClassification.NonMoving);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<InventoryActivityClassification>(filters.Status, true, out var classification))
            candidates = candidates.Where(item => item.ActivityClassification == classification);
        var rows = candidates.Select(item => Row(("ItemCode", item.ItemCode), ("ItemName", item.ItemName),
            ("Category", item.CategoryName), ("WarehouseCode", item.WarehouseCode),
            ("WarehouseName", item.WarehouseName), ("LocationCode", item.LocationCode),
            ("LocationName", item.LocationName), ("Classification", item.ActivityClassification.ToString()),
            ("DaysSinceActivity", item.DaysSinceActivity), ("QuantityOnHand", item.QuantityOnHand),
            ("InventoryValue", item.InventoryValue), ("LastMovementDate", item.LastMovementDateUtc),
            ("OldestStockAgeDays", item.OldestStockAgeDays), ("OldestAgeingBand", item.OldestAgeingBand),
            ("DisposalCandidate", item.DisposalCandidate), ("RecommendedAction", item.RecommendedAction))).ToList();
        return PageRows(rows, definition, request, filters, source.AsOfUtc);
    }

    private async Task<ReportResultDto> ExecuteExpiryAsync(
        InventorySystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var source = await AnalyticsAsync(filters, cancellationToken);
        var rows = source.Items.Where(item => item.ExpiredQuantity != 0m || item.ExpiringQuantity != 0m)
            .Select(item => Row(("ItemCode", item.ItemCode), ("ItemName", item.ItemName),
                ("Category", item.CategoryName), ("WarehouseCode", item.WarehouseCode),
                ("WarehouseName", item.WarehouseName), ("LocationCode", item.LocationCode),
                ("LocationName", item.LocationName), ("ExpiredQuantity", item.ExpiredQuantity),
                ("ExpiredValue", item.ExpiredValue), ("ExpiringQuantity", item.ExpiringQuantity),
                ("ExpiringValue", item.ExpiringValue), ("ExpiryWarningDays", source.ExpiryWarningDays),
                ("DisposalCandidate", item.DisposalCandidate), ("RecommendedAction", item.RecommendedAction),
                ("AsOfUtc", source.AsOfUtc))).ToList();
        return PageRows(rows, definition, request, filters, source.AsOfUtc);
    }

    private async Task<ReportResultDto> ExecuteDisposalAsync(
        InventorySystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        InventoryDisposalStatus? status = null;
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<InventoryDisposalStatus>(filters.Status, true, out var parsed)) status = parsed;
        var source = await _disposals.GetReportSourceAsync(status, filters.WarehouseId, cancellationToken);
        var rows = source.Where(item => !filters.StartUtc.HasValue || item.RequestedAtUtc >= filters.StartUtc.Value)
            .Where(item => !filters.EndExclusiveUtc.HasValue || item.RequestedAtUtc < filters.EndExclusiveUtc.Value)
            .SelectMany(item => item.Lines.Select(line => Row(("DisposalNumber", item.DisposalNumber),
                ("Status", item.Status.ToString()), ("Method", item.Method.ToString()),
                ("RequestedAt", item.RequestedAtUtc), ("WarehouseCode", item.WarehouseCode),
                ("WarehouseName", item.WarehouseName), ("ItemCode", line.ItemCode), ("ItemName", line.ItemName),
                ("LocationCode", line.LocationCode), ("Quantity", line.Quantity), ("UnitCost", line.UnitCost),
                ("LineValue", line.TotalValue), ("LotNumber", line.LotNumber), ("BatchNumber", line.BatchNumber),
                ("SerialNumber", line.SerialNumber), ("AuthorityRoute", item.AuthorityRoute),
                ("CommitteeReference", item.CommitteeReference), ("StockAdjustmentId", item.StockAdjustmentId),
                ("ProceedsAmount", item.ProceedsAmount), ("ExecutionReference", item.ExecutionReference),
                ("CompletedAt", item.CompletedAtUtc)))).ToList();
        return PageRows(rows, definition, request, filters, DateTime.UtcNow);
    }

    private IQueryable<T> Query<T>() where T : TenantEntity
    {
        EnsureTenantContext();
        var tenantId = _currentUser.TenantId;
        return _unitOfWork.Repository<T>().GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
            .AsNoTracking();
    }

    private async Task<HashSet<InventoryScope>> ReadableScopesAsync(
        IReadOnlyCollection<InventoryScope> scopes, string sourceReference, CancellationToken cancellationToken)
    {
        var allowed = new HashSet<InventoryScope>();
        foreach (var scope in scopes)
        {
            try
            {
                var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = "procurement.inventory.read",
                    WarehouseId = scope.WarehouseId,
                    LocationId = scope.LocationId,
                    RequireLocationScope = true,
                    SourceType = SourceType,
                    SourceReference = $"{sourceReference}:{scope.WarehouseId:N}:{scope.LocationId?.ToString("N") ?? "warehouse"}"
                }, Correlation(), cancellationToken);
                if (decision.Allowed) allowed.Add(scope);
            }
            catch (ProcurementAccessValidationException) { }
            catch (ProcurementAccessNotFoundException) { }
            catch (ProcurementAccessAuthorizationException) { }
        }
        return allowed;
    }

    private async Task EnsureTenantWideInventoryAccessAsync(CancellationToken cancellationToken)
    {
        var warehouses = await Query<Warehouse>().Where(item => item.IsActive).Select(item => item.Id)
            .ToListAsync(cancellationToken);
        foreach (var warehouseId in warehouses)
        {
            var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = "procurement.inventory.read",
                WarehouseId = warehouseId,
                RequireLocationScope = true,
                SourceType = SourceType,
                SourceReference = $"valuation-gl-register:{warehouseId:N}"
            }, Correlation(), cancellationToken);
            if (!decision.Allowed)
                throw new UnauthorizedAccessException(
                    "Tenant-wide valuation/GL reporting requires inventory read authority for every warehouse and location.");
        }
    }

    private static Expression<Func<T, bool>> BuildScopePredicate<T>(IEnumerable<InventoryScope> scopes)
        where T : StockMovement
    {
        var parameter = Expression.Parameter(typeof(T), "item");
        Expression body = Expression.Constant(false);
        foreach (var scope in scopes)
        {
            var warehouse = Expression.Equal(Expression.Property(parameter, nameof(StockMovement.WarehouseId)),
                Expression.Constant(scope.WarehouseId));
            var location = Expression.Equal(Expression.Property(parameter, nameof(StockMovement.LocationId)),
                Expression.Constant(scope.LocationId, typeof(Guid?)));
            body = Expression.OrElse(body, Expression.AndAlso(warehouse, location));
        }
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private async Task EnsureCapabilityAsync(
        string permission, string sourceReference, bool isAdministrator, CancellationToken cancellationToken)
    {
        if (isAdministrator) return;
        var decision = await _access.EnforceCapabilityAsync(Capability(permission, sourceReference),
            Correlation(), cancellationToken);
        if (!decision.Allowed) throw new UnauthorizedAccessException(decision.Message);
    }

    private static ProcurementAccessCapabilityRequest Capability(string permission, string sourceReference) => new()
    {
        PermissionCode = permission,
        SourceType = SourceType,
        SourceReference = sourceReference
    };

    private void EnsureTenantContext()
    {
        if (_currentUser.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("A tenant context is required to execute inventory reports.");
    }

    private static InventorySystemReportDefinition Resolve(string query) =>
        InventoryStatutoryReportCatalogue.Resolve(query)
        ?? throw new InvalidOperationException("The inventory system report is not registered.");

    private static string Correlation() => $"tdc0702-{Guid.NewGuid():N}";

    private static Dictionary<string, object> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value!);

    private static ReportResultDto PageRows(
        IReadOnlyList<Dictionary<string, object>> rows,
        InventorySystemReportDefinition definition,
        ExecuteReportDto request,
        ReportFilters filters,
        DateTime dataAsOf)
    {
        var (page, pageSize) = Page(request);
        var totalPages = rows.Count == 0 ? 0 : (int)Math.Ceiling(rows.Count / (double)pageSize);
        return Result(definition, filters, dataAsOf, rows.Count,
            rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, totalPages);
    }

    private static async Task<ReportResultDto> PageQueryAsync<T>(
        IQueryable<T> query,
        InventorySystemReportDefinition definition,
        ExecuteReportDto request,
        ReportFilters filters,
        Func<T, Dictionary<string, object>> map,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var (page, pageSize) = Page(request);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return Result(definition, filters, DateTime.UtcNow, total, rows.Select(map).ToList(),
            page, pageSize, totalPages);
    }

    private static ReportResultDto Result(
        InventorySystemReportDefinition definition,
        ReportFilters filters,
        DateTime dataAsOf,
        int totalRows,
        List<Dictionary<string, object>> rows,
        int page,
        int pageSize,
        int totalPages) => new()
    {
        TotalRows = totalRows,
        Columns = definition.Columns.Select((item, index) => new ReportColumnDto
        {
            Name = item.Name, DisplayName = item.DisplayName, DataType = item.DataType,
            Format = item.Format, IsVisible = item.IsVisible, Order = index,
            AggregationType = item.AggregationType
        }).ToList(),
        Data = rows,
        CurrentPage = page,
        PageSize = pageSize,
        TotalPages = totalPages,
        HasNextPage = page < totalPages,
        HasPreviousPage = page > 1 && totalPages > 0,
        Metadata = new ReportMetadataDto
        {
            Parameters = filters.ToMetadata(),
            Query = definition.Query,
            DataAsOf = dataAsOf,
            DataSource = "Tenant and assigned-scope ERP inventory transaction owners",
            Statistics = new Dictionary<string, object>
            {
                ["systemCode"] = definition.Code,
                ["page"] = page,
                ["pageSize"] = pageSize,
                ["totalRows"] = totalRows
            }
        }
    };

    private static (int Page, int PageSize) Page(ExecuteReportDto request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 1000);
        if (request.MaxRows is > 0) pageSize = Math.Min(pageSize, Math.Clamp(request.MaxRows.Value, 1, 1000));
        return (page, pageSize);
    }

    private sealed record InventoryScope(Guid WarehouseId, Guid? LocationId);

    private sealed record ReportFilters(
        DateTime? StartUtc,
        DateTime? EndExclusiveUtc,
        Guid? WarehouseId,
        Guid? CategoryId,
        Guid? FiscalPeriodId,
        string? Status,
        string? MovementType,
        int SlowMovingDays,
        int NonMovingDays,
        int ExpiryWarningDays)
    {
        public static ReportFilters Parse(ExecuteReportDto request)
        {
            var start = request.StartDate ?? GetDate(request.Parameters, "startDate");
            var end = request.EndDate ?? GetDate(request.Parameters, "endDate");
            start = start.HasValue ? DateTime.SpecifyKind(start.Value.Date, DateTimeKind.Utc) : null;
            DateTime? endExclusive = end.HasValue
                ? DateTime.SpecifyKind(end.Value.Date.AddDays(1), DateTimeKind.Utc)
                : null;
            if (start.HasValue && endExclusive.HasValue && start.Value >= endExclusive.Value)
                throw new InvalidOperationException("Start date must be on or before end date.");
            var slow = GetInt(request.Parameters, "slowMovingDays") ?? 90;
            var non = GetInt(request.Parameters, "nonMovingDays") ?? 180;
            var expiry = GetInt(request.Parameters, "expiryWarningDays") ?? 90;
            if (slow is < 1 or > 3650 || non <= slow || non > 3650 || expiry is < 1 or > 730)
                throw new InvalidOperationException(
                    "Slow-moving days must be 1-3650, non-moving days must be greater and at most 3650, and expiry warning days must be 1-730.");
            return new ReportFilters(start, endExclusive, GetGuid(request.Parameters, "warehouseId"),
                GetGuid(request.Parameters, "categoryId"), GetGuid(request.Parameters, "fiscalPeriodId"),
                GetString(request.Parameters, "status"), GetString(request.Parameters, "movementType"),
                slow, non, expiry);
        }

        public Dictionary<string, object> ToMetadata()
        {
            var values = new Dictionary<string, object>
            {
                ["slowMovingDays"] = SlowMovingDays,
                ["nonMovingDays"] = NonMovingDays,
                ["expiryWarningDays"] = ExpiryWarningDays
            };
            if (StartUtc.HasValue) values["startDate"] = StartUtc.Value;
            if (EndExclusiveUtc.HasValue) values["endDate"] = EndExclusiveUtc.Value.AddDays(-1);
            if (WarehouseId.HasValue) values["warehouseId"] = WarehouseId.Value;
            if (CategoryId.HasValue) values["categoryId"] = CategoryId.Value;
            if (FiscalPeriodId.HasValue) values["fiscalPeriodId"] = FiscalPeriodId.Value;
            if (!string.IsNullOrWhiteSpace(Status)) values["status"] = Status;
            if (!string.IsNullOrWhiteSpace(MovementType)) values["movementType"] = MovementType;
            return values;
        }

        private static string? GetString(Dictionary<string, object>? values, string key)
        {
            if (values is null || !values.TryGetValue(key, out var value) || value is null) return null;
            var text = value is JsonElement element ? element.ToString() : Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) || text.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null : text.Trim();
        }

        private static DateTime? GetDate(Dictionary<string, object>? values, string key) =>
            DateTime.TryParse(GetString(values, key), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var value) ? value : null;

        private static int? GetInt(Dictionary<string, object>? values, string key) =>
            int.TryParse(GetString(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value : null;

        private static Guid? GetGuid(Dictionary<string, object>? values, string key) =>
            Guid.TryParse(GetString(values, key), out var value) ? value : null;
    }

    private sealed class MovementRow
    {
        public DateTime MovementDate { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string WarehouseCode { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string? LocationCode { get; set; }
        public string? LocationName { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
        public decimal RunningBalance { get; set; }
        public ReferenceType ReferenceType { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? LotNumber { get; set; }
        public string? BatchNumber { get; set; }
        public string? SerialNumber { get; set; }
        public string ProcessedBy { get; set; } = string.Empty;
    }
}
