using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed partial class QuantitySurveyRateLibraryService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService) : IQuantitySurveyRateLibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A valid tenant context is required.");

    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated user is required.");

    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName)
        ? UserId.ToString()
        : currentUser.UserName.Trim();

    private string ActorRoles => string.Join(",", currentUser.Roles
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value));

    public async Task<QuantitySurveyLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var units = await db.UnitsOfMeasure.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.IsActive)
            .OrderBy(value => value.Code)
            .Select(value => Option(value.Id, $"{value.Code} · {value.Name}", value.Category))
            .ToListAsync(cancellationToken);
        var currencies = await db.Currencies.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.Status == "Active")
            .OrderByDescending(value => value.IsBaseCurrency)
            .ThenBy(value => value.CurrencyCode)
            .Select(value => Option(value.Id, $"{value.CurrencyCode} · {value.CurrencyName}", value.IsBaseCurrency ? "Base currency" : null))
            .ToListAsync(cancellationToken);
        var projectTypes = await db.ProjectTypes.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.IsActive)
            .OrderBy(value => value.Code)
            .Select(value => Option(value.Id, $"{value.Code} · {value.Name}", null))
            .ToListAsync(cancellationToken);
        var locations = await db.Locations.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.IsActive)
            .OrderBy(value => value.Code)
            .Select(value => Option(value.Id, $"{value.Code} · {value.Name}", null))
            .ToListAsync(cancellationToken);
        var partners = await db.BusinessPartners.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.IsActive &&
                BusinessPartnerRoles.ProcurementTypes.Contains(value.PartnerType))
            .OrderBy(value => value.PartnerCode)
            .Select(value => Option(value.Id, $"{value.PartnerCode} · {value.PartnerName}", value.PartnerType))
            .ToListAsync(cancellationToken);
        var inventoryItems = await db.InventoryItems.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.Status == ItemStatus.Active)
            .OrderBy(value => value.ItemCode)
            .Select(value => Option(value.Id, $"{value.ItemCode} · {value.Name}", value.UnitOfMeasure))
            .Take(1000)
            .ToListAsync(cancellationToken);
        var catalogueEntries = await db.ProjectCatalogEntries.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.IsActive && value.CatalogType.StartsWith("qs-"))
            .OrderBy(value => value.CatalogType)
            .ThenBy(value => value.Code)
            .Select(value => Option(value.Id, $"{value.Code} · {value.Name}", value.CatalogType))
            .ToListAsync(cancellationToken);
        var evidence = await db.CentralDocumentVersions.AsNoTracking()
            .Include(value => value.DocumentRecord)
            .Where(value => value.TenantId == tenantId)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .OrderByDescending(value => value.PublishedAt)
            .Select(value => Option(
                value.Id,
                value.DocumentRecord.DocumentReference + " · " + value.DocumentRecord.Title + " · " + value.VersionNumber,
                value.DocumentRecord.SourceModule))
            .Take(500)
            .ToListAsync(cancellationToken);

        return new QuantitySurveyLookupsDto
        {
            Sources = new Dictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>>(StringComparer.OrdinalIgnoreCase)
            {
                ["unitsOfMeasure"] = units,
                ["currencies"] = currencies,
                ["projectTypes"] = projectTypes,
                ["locations"] = locations,
                ["businessPartners"] = partners,
                ["inventoryItems"] = inventoryItems,
                ["catalogueEntries"] = catalogueEntries,
                ["evidenceDocuments"] = evidence
            }
        };
    }

    public async Task<IReadOnlyList<QuantitySurveyMarketSurveySourceDto>> GetMarketSurveySourcesAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var policy = await GetPolicyAsync(now, cancellationToken);
        var analyses = await db.MarketAnalyses.AsNoTracking()
            .Include(value => value.PriceHistories.Where(price => !price.IsDeleted))
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.Status == "Published")
            .OrderByDescending(value => value.AnalysisPeriodEnd)
            .ThenBy(value => value.AnalysisCode)
            .Take(500)
            .ToListAsync(cancellationToken);

        return analyses
            .Select(value => MapMarketSurveySource(value, policy.UpdateCadenceMonths, now))
            .Where(value => value.QuoteCount > 0)
            .ToList();
    }

    public async Task<IReadOnlyList<QuantitySurveyHistoricalRateSourceDto>> GetHistoricalRateSourcesAsync(
        Guid itemId,
        QuantitySurveyHistoricalRateSourceType? sourceType = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var item = await FindItemAsync(itemId, tracking: false, cancellationToken);
        return await GetHistoricalRateSourcesCoreAsync(item, sourceType, null, search, cancellationToken);
    }

    private async Task<IReadOnlyList<QuantitySurveyHistoricalRateSourceDto>> GetHistoricalRateSourcesCoreAsync(
        QuantitySurveyRateLibraryItem item,
        QuantitySurveyHistoricalRateSourceType? sourceType,
        Guid? sourceId,
        string? search,
        CancellationToken cancellationToken)
    {
        var completed = await projectService.LookupProjectsAsync(
            status: ProjectStatuses.Completed, take: 5000);
        var closed = await projectService.LookupProjectsAsync(
            status: ProjectStatuses.Closed, take: 5000);
        var accessibleProjectIds = completed.Concat(closed)
            .Select(value => value.Id)
            .Distinct()
            .ToList();
        if (accessibleProjectIds.Count == 0) return [];

        var projects = await db.Projects.AsNoTracking()
            .Where(value => value.TenantId == TenantId && accessibleProjectIds.Contains(value.Id) &&
                !value.IsDeleted && (value.Status == ProjectStatuses.Completed || value.Status == ProjectStatuses.Closed))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var candidates = new List<QuantitySurveyHistoricalRateSourceDto>();

        if (!sourceType.HasValue || sourceType == QuantitySurveyHistoricalRateSourceType.CompletedBoqLine)
        {
            var lines = await db.ProjectBoqVersionLines.AsNoTracking()
                .Include(value => value.Version)
                .Where(value => value.TenantId == TenantId && accessibleProjectIds.Contains(value.ProjectId) &&
                    !value.IsDeleted && value.UnitRate > 0 && value.Quantity > 0 &&
                    value.Currency != string.Empty &&
                    value.Version.TenantId == TenantId && !value.Version.IsDeleted &&
                    value.Version.VersionType == QuantitySurveyBoqVersionType.Approved &&
                    value.Version.Status == ProjectBoqVersionStatuses.Approved &&
                    value.Version.PublishedAt != null &&
                    (!sourceId.HasValue || value.Id == sourceId.Value))
                .OrderByDescending(value => value.Version.PublishedAt)
                .Take(sourceId.HasValue ? 2 : 1000)
                .ToListAsync(cancellationToken);
            candidates.AddRange(lines
                .Where(value => HistoricalCodeMatches(item, value.ItemCode) &&
                    MatchesUnit(value.UnitOfMeasure, item.UnitOfMeasure.Code, item.UnitOfMeasure.Name))
                .Where(value => projects.ContainsKey(value.ProjectId))
                .Select(value => Candidate(
                    QuantitySurveyHistoricalRateSourceType.CompletedBoqLine,
                    value.Id,
                    projects[value.ProjectId],
                    $"BOQ-{value.Version.VersionNumber}:{value.LineNumber ?? value.ItemCode ?? value.Id.ToString("N")}",
                    value.Description,
                    value.Version.PublishedAt!.Value,
                    value.UnitOfMeasure!,
                    value.Quantity,
                    value.UnitRate!.Value,
                    value.LineAmount ?? decimal.Round(value.Quantity * value.UnitRate.Value, 2),
                    value.Currency)));
        }

        if (!sourceType.HasValue || sourceType == QuantitySurveyHistoricalRateSourceType.CertifiedValuation)
        {
            var valuations = await db.ProjectInterimValuations.AsNoTracking()
                .Include(value => value.CompletedProjectPackages)
                .Where(value => value.TenantId == TenantId && accessibleProjectIds.Contains(value.ProjectId) &&
                    !value.IsDeleted && value.ProjectPackageId != null && value.GrossWorkValue > 0 &&
                    value.Currency != string.Empty &&
                    value.CompletedProjectPackages.Count(completion =>
                        completion.TenantId == TenantId && !completion.IsDeleted) == 1 &&
                    value.CompletedProjectPackages.Any(completion =>
                        completion.TenantId == TenantId && !completion.IsDeleted &&
                        completion.ProjectPackageId == value.ProjectPackageId) &&
                    (value.Status == ProjectInterimValuationStatuses.Certified || value.Status == ProjectInterimValuationStatuses.Paid) &&
                    (!sourceId.HasValue || value.Id == sourceId.Value))
                .OrderByDescending(value => value.ValuationDate)
                .Take(sourceId.HasValue ? 2 : 500)
                .ToListAsync(cancellationToken);
            var packageIds = valuations.Select(value => value.ProjectPackageId!.Value).Distinct().ToList();
            var valuationLines = packageIds.Count == 0
                ? []
                : await db.ProjectBoqVersionLines.AsNoTracking()
                    .Include(value => value.Version)
                    .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                        value.ProjectPackageId != null && packageIds.Contains(value.ProjectPackageId.Value) &&
                        value.Quantity > 0 &&
                        value.Version.TenantId == TenantId && !value.Version.IsDeleted &&
                        value.Version.VersionType == QuantitySurveyBoqVersionType.Approved &&
                        value.Version.Status == ProjectBoqVersionStatuses.Approved &&
                        value.Version.PublishedAt != null)
                    .ToListAsync(cancellationToken);
            var linesByPackage = valuationLines
                .GroupBy(value => value.ProjectPackageId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());
            foreach (var valuation in valuations.Where(value => projects.ContainsKey(value.ProjectId)))
            {
                if (!linesByPackage.TryGetValue(valuation.ProjectPackageId!.Value, out var packageLines)) continue;
                var latestPublicationId = packageLines.OrderByDescending(value => value.Version.PublishedAt)
                    .ThenByDescending(value => value.Version.VersionNumber)
                    .Select(value => value.ProjectBoqVersionId)
                    .FirstOrDefault();
                var applicable = packageLines.Where(value => value.ProjectBoqVersionId == latestPublicationId &&
                        HistoricalCodeMatches(item, value.ItemCode) &&
                        MatchesUnit(value.UnitOfMeasure, item.UnitOfMeasure.Code, item.UnitOfMeasure.Name))
                    .ToList();
                var allPublishedLines = packageLines.Count(value => value.ProjectBoqVersionId == latestPublicationId);
                if (applicable.Count != 1 || allPublishedLines != 1) continue;
                var line = applicable[0];
                var unitRate = decimal.Round(valuation.GrossWorkValue / line.Quantity, 4);
                candidates.Add(Candidate(
                    QuantitySurveyHistoricalRateSourceType.CertifiedValuation,
                    valuation.Id,
                    projects[valuation.ProjectId],
                    valuation.ValuationNumber ?? $"VAL-{valuation.Id:N}",
                    $"{valuation.Title} · single-line certified package valuation",
                    valuation.ValuationDate,
                    line.UnitOfMeasure!,
                    line.Quantity,
                    unitRate,
                    valuation.GrossWorkValue,
                    valuation.Currency));
            }
        }

        if ((!sourceType.HasValue || sourceType == QuantitySurveyHistoricalRateSourceType.ProcurementPrice) &&
            item.InventoryItemId.HasValue)
        {
            var orderLines = await db.PurchaseOrderItems.AsNoTracking()
                .Include(value => value.PurchaseOrder)
                    .ThenInclude(value => value.SourceRequisition)
                .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                    value.InventoryItemId == item.InventoryItemId && value.UnitPrice > 0 &&
                    (value.ReceivedQuantity > 0 || value.OrderedQuantity > 0) &&
                    value.PurchaseOrder.Currency != string.Empty &&
                    value.PurchaseOrder.TenantId == TenantId && !value.PurchaseOrder.IsDeleted &&
                    value.PurchaseOrder.SourceRequisition != null &&
                    value.PurchaseOrder.SourceRequisition.TenantId == TenantId &&
                    !value.PurchaseOrder.SourceRequisition.IsDeleted &&
                    value.PurchaseOrder.SourceRequisition.ProjectId != null &&
                    accessibleProjectIds.Contains(value.PurchaseOrder.SourceRequisition.ProjectId.Value) &&
                    (value.PurchaseOrder.Status == "Approved" || value.PurchaseOrder.Status == "Sent" ||
                     value.PurchaseOrder.Status == "Acknowledged" || value.PurchaseOrder.Status == "PartiallyReceived" ||
                     value.PurchaseOrder.Status == "Received") &&
                    (!sourceId.HasValue || value.Id == sourceId.Value))
                .OrderByDescending(value => value.PurchaseOrder.ReceivedDate ?? value.PurchaseOrder.ApprovedAt ?? value.PurchaseOrder.OrderDate)
                .Take(sourceId.HasValue ? 2 : 1000)
                .ToListAsync(cancellationToken);
            candidates.AddRange(orderLines
                .Where(value => MatchesUnit(value.UnitOfMeasure, item.UnitOfMeasure.Code, item.UnitOfMeasure.Name))
                .Where(value => projects.ContainsKey(value.PurchaseOrder.SourceRequisition!.ProjectId!.Value))
                .Select(value =>
                {
                    var quantity = value.ReceivedQuantity > 0 ? value.ReceivedQuantity : value.OrderedQuantity;
                    var unitRate = value.LandedUnitCost > 0 ? value.LandedUnitCost : value.UnitPrice;
                    return Candidate(
                        QuantitySurveyHistoricalRateSourceType.ProcurementPrice,
                        value.Id,
                        projects[value.PurchaseOrder.SourceRequisition!.ProjectId!.Value],
                        $"{value.PurchaseOrder.OrderNumber}:{value.BusinessPartnerItemCode ?? value.Id.ToString("N")}",
                        value.ItemDescription ?? item.Name,
                        value.PurchaseOrder.ReceivedDate ?? value.PurchaseOrder.ApprovedAt ?? value.PurchaseOrder.OrderDate,
                        value.UnitOfMeasure,
                        quantity,
                        unitRate,
                        decimal.Round(quantity * unitRate, 2),
                        value.PurchaseOrder.Currency,
                        value.PurchaseOrder.BusinessPartnerId);
                }));
        }

        if ((!sourceType.HasValue || sourceType == QuantitySurveyHistoricalRateSourceType.ActualProjectCost) &&
            item.InventoryItemId.HasValue)
        {
            var costs = await db.ProjectMaterialCostEntries.AsNoTracking()
                .Where(value => value.TenantId == TenantId && accessibleProjectIds.Contains(value.ProjectId) &&
                    !value.IsDeleted && value.InventoryItemId == item.InventoryItemId && value.Quantity > 0 &&
                    value.UnitCost > 0 && value.Amount > 0 && value.PostingState == "Posted" &&
                    value.Currency != string.Empty &&
                    value.AffectsActualCost && !value.IsReversed && !value.HasMissingSourceLink && !value.HasReversalGap &&
                    (!sourceId.HasValue || value.Id == sourceId.Value))
                .OrderByDescending(value => value.EntryDate)
                .Take(sourceId.HasValue ? 2 : 1000)
                .ToListAsync(cancellationToken);
            candidates.AddRange(costs
                .Where(value => MatchesUnit(value.UnitOfMeasure, item.UnitOfMeasure.Code, item.UnitOfMeasure.Name))
                .Where(value => projects.ContainsKey(value.ProjectId))
                .Select(value => Candidate(
                    QuantitySurveyHistoricalRateSourceType.ActualProjectCost,
                    value.Id,
                    projects[value.ProjectId],
                    value.SourceDocumentNumber ?? value.SourceTransactionType ?? $"COST-{value.Id:N}",
                    value.InventoryItemName ?? item.Name,
                    value.EntryDate,
                    value.UnitOfMeasure!,
                    value.Quantity,
                    value.UnitCost,
                    value.Amount,
                    value.Currency)));
        }

        var promoted = await db.QuantitySurveyRateLibraryRates.IgnoreQueryFilters().AsNoTracking()
            .Where(value => value.TenantId == TenantId &&
                value.HistoricalSourceType != null && value.HistoricalSourceId != null)
            .Select(value => new { value.Id, value.HistoricalSourceType, value.HistoricalSourceId })
            .ToListAsync(cancellationToken);
        var promotedLookup = promoted
            .GroupBy(value => (value.HistoricalSourceType!.Value, value.HistoricalSourceId!.Value))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(value => value.Id).First().Id);
        var normalizedSearch = search?.Trim();
        return candidates
            .Select(value => promotedLookup.TryGetValue((value.SourceType, value.SourceId), out var rateId)
                ? value with { ExistingRateId = rateId }
                : value)
            .Where(value => string.IsNullOrWhiteSpace(normalizedSearch) ||
                value.ProjectCode.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                value.ProjectTitle.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                value.SourceReference.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                value.SourceLabel.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(value => value.SourceDate)
            .ThenBy(value => value.ProjectCode)
            .Take(sourceId.HasValue ? 2 : 500)
            .ToList();
    }

    public async Task<QuantitySurveyRateLibraryPageDto> GetItemsAsync(
        QuantitySurveyRateLibraryListRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var query = db.QuantitySurveyRateLibraryItems.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted);
        if (!request.IncludeInactive) query = query.Where(value => value.IsActive);
        if (request.Category.HasValue) query = query.Where(value => value.Category == request.Category);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(value => value.Code.Contains(search) || value.Name.Contains(search) ||
                (value.Description != null && value.Description.Contains(search)));
        }

        if (request.ProjectTypeId.HasValue || request.LocationId.HasValue || request.BusinessPartnerId.HasValue)
        {
            query = query.Where(value => value.Rates.Any(rate =>
                !rate.IsDeleted &&
                (!request.ProjectTypeId.HasValue || rate.ProjectTypeId == request.ProjectTypeId) &&
                (!request.LocationId.HasValue || rate.LocationId == request.LocationId) &&
                (!request.BusinessPartnerId.HasValue || rate.BusinessPartnerId == request.BusinessPartnerId)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(value => value.UnitOfMeasure)
            .Include(value => value.ProjectCatalogEntry)
            .Include(value => value.InventoryItem)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted))
                .ThenInclude(rate => rate.ProjectType)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted))
                .ThenInclude(rate => rate.Location)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted))
                .ThenInclude(rate => rate.BusinessPartner)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted))
                .ThenInclude(rate => rate.CentralDocumentRecord)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted))
                .ThenInclude(rate => rate.CentralDocumentVersion)
            .OrderBy(value => value.Category)
            .ThenBy(value => value.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var effectiveAt = Utc(request.EffectiveAt ?? DateTime.UtcNow);

        return new QuantitySurveyRateLibraryPageDto
        {
            Items = items.Select(value => MapItem(value, effectiveAt)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<QuantitySurveyRateLibraryItemDto> GetItemAsync(Guid id, CancellationToken cancellationToken = default)
        => MapItem(await FindItemAsync(id, tracking: false, cancellationToken), DateTime.UtcNow);

    public async Task<QuantitySurveyRateLibraryItemDto> CreateItemAsync(
        CreateQuantitySurveyRateLibraryItemRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var code = NormalizeCode(request.Code);
        if (await db.QuantitySurveyRateLibraryItems.IgnoreQueryFilters().AnyAsync(
                value => value.TenantId == tenantId && value.Code == code,
                cancellationToken))
            throw new QuantitySurveyRateLibraryConflictException($"Rate-library item code '{code}' already exists, including archived records.");

        await ValidateItemReferencesAsync(request, cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new QuantitySurveyRateLibraryItem
        {
            TenantId = tenantId,
            Code = code,
            Name = Required(request.Name, "Item name", 200),
            Description = Clean(request.Description, 1000),
            Category = request.Category,
            UnitOfMeasureId = request.UnitOfMeasureId,
            ProjectCatalogEntryId = request.ProjectCatalogEntryId,
            InventoryItemId = request.InventoryItemId,
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        db.QuantitySurveyRateLibraryItems.Add(entity);
        AddRevision(entity.Id, null, QuantitySurveyAuditEventMap.CreateRateLibraryItem, correlationId, null, null, Snapshot(entity));
        await SaveAsync(cancellationToken);
        return await GetItemAsync(entity.Id, cancellationToken);
    }

    public async Task<QuantitySurveyRateLibraryItemDto> UpdateItemAsync(
        Guid id,
        UpdateQuantitySurveyRateLibraryItemRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindItemAsync(id, tracking: true, cancellationToken);
        CheckVersion(entity.RowVersion, request.RowVersion, "rate-library item");
        await ValidateItemReferencesAsync(request, cancellationToken);
        var code = NormalizeCode(request.Code);
        if (await db.QuantitySurveyRateLibraryItems.IgnoreQueryFilters().AnyAsync(
                value => value.TenantId == TenantId && value.Id != id && value.Code == code,
                cancellationToken))
            throw new QuantitySurveyRateLibraryConflictException($"Rate-library item code '{code}' already exists, including archived records.");

        var before = Snapshot(entity);
        entity.Code = code;
        entity.Name = Required(request.Name, "Item name", 200);
        entity.Description = Clean(request.Description, 1000);
        entity.Category = request.Category;
        entity.UnitOfMeasureId = request.UnitOfMeasureId;
        entity.ProjectCatalogEntryId = request.ProjectCatalogEntryId;
        entity.InventoryItemId = request.InventoryItemId;
        entity.IsActive = request.IsActive;
        Touch(entity);
        AddRevision(entity.Id, null, QuantitySurveyAuditEventMap.UpdateRateLibraryItem, correlationId,
            Required(request.Reason, "Reason", 1000), before, Snapshot(entity));
        await SaveAsync(cancellationToken);
        return await GetItemAsync(entity.Id, cancellationToken);
    }

    public async Task<QuantitySurveyRateDto> CreateRateAsync(
        Guid itemId,
        SaveQuantitySurveyRateRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var item = await FindItemAsync(itemId, tracking: false, cancellationToken);
        if (request.SourceType == QuantitySurveyRateSourceType.MarketSurvey)
            throw new QuantitySurveyRateLibraryValidationException(
                "Use the governed market-survey update so authoritative source lineage, prior value and audit evidence are retained.");
        if (request.SourceType == QuantitySurveyRateSourceType.RateBuildUp)
            throw new QuantitySurveyRateLibraryValidationException(
                "Use the governed component calculation workflow so rate inputs, policy caps and audit lineage are retained.");
        if (request.SourceType is QuantitySurveyRateSourceType.HistoricalProject or
            QuantitySurveyRateSourceType.PurchaseOrder)
            throw new QuantitySurveyRateLibraryValidationException(
                "Use the governed source action so authoritative source lineage, prior value and audit evidence are retained.");
        return await CreateRateCoreAsync(item, request, QuantitySurveyAuditEventMap.CreateRateDraft,
            before: null, marketSurvey: null, historical: null, buildUp: null, correlationId, cancellationToken);
    }

    public async Task<QuantitySurveyRateDto> PrepareMarketSurveyUpdateAsync(
        Guid itemId,
        PrepareQuantitySurveyMarketSurveyUpdateRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var item = await FindItemAsync(itemId, tracking: false, cancellationToken);
        if (request.MarketAnalysisId == Guid.Empty)
            throw new QuantitySurveyRateLibraryValidationException("Select a published Procurement market analysis.");
        if (request.CentralDocumentVersionId == Guid.Empty)
            throw new QuantitySurveyRateLibraryValidationException("Select current published market-survey evidence from the central DMS.");

        var effectiveFrom = Utc(request.EffectiveFrom);
        if (effectiveFrom == default)
            throw new QuantitySurveyRateLibraryValidationException("Effective from is required.");
        var policy = await GetPolicyAsync(effectiveFrom, cancellationToken);
        var analysis = await db.MarketAnalyses.AsNoTracking()
            .Include(value => value.PriceHistories.Where(price => !price.IsDeleted))
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.MarketAnalysisId &&
                !value.IsDeleted && value.Status == "Published", cancellationToken)
            ?? throw new QuantitySurveyRateLibraryValidationException(
                "Select a published Procurement market analysis available to this tenant.");
        var source = MapMarketSurveySource(analysis, policy.UpdateCadenceMonths, effectiveFrom);
        if (source.QuoteCount == 0)
            throw new QuantitySurveyRateLibraryValidationException(
                "The selected market analysis has no usable survey quotations.");
        if (analysis.AnalysisPeriodEnd > effectiveFrom)
            throw new QuantitySurveyRateLibraryValidationException(
                "The market-survey period must end on or before the rate effective date.");
        if (effectiveFrom > source.NextReviewDueAt)
            throw new QuantitySurveyRateLibraryValidationException(
                $"The selected market survey became stale on {source.NextReviewDueAt:dd MMM yyyy} under the effective {policy.UpdateCadenceMonths}-month cadence. Select a newer published analysis.");

        var usableQuotes = UsableMarketSurveyQuotes(analysis).ToList();
        if (usableQuotes.Any(value => !MatchesUnit(value.UnitOfMeasure, item.UnitOfMeasure.Code, item.UnitOfMeasure.Name)))
            throw new QuantitySurveyRateLibraryValidationException(
                $"Every survey quotation must use the selected rate item's controlled unit ({item.UnitOfMeasure.Code}).");

        var unitRate = request.PriceBasis switch
        {
            QuantitySurveyMarketSurveyPriceBasis.CurrentMarketPrice => source.CurrentMarketPrice,
            QuantitySurveyMarketSurveyPriceBasis.AverageSurveyPrice => source.AverageSurveyPrice,
            QuantitySurveyMarketSurveyPriceBasis.LowestSurveyPrice => source.LowestSurveyPrice,
            QuantitySurveyMarketSurveyPriceBasis.HighestSurveyPrice => source.HighestSurveyPrice,
            QuantitySurveyMarketSurveyPriceBasis.ForecastedPrice => source.ForecastedPrice,
            _ => throw new QuantitySurveyRateLibraryValidationException("Select a supported market-survey price basis.")
        };
        if (unitRate <= 0)
            throw new QuantitySurveyRateLibraryValidationException("The selected market-survey price basis must produce a positive unit rate.");

        var currency = await db.Currencies.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && !value.IsDeleted && value.Status == "Active" &&
            value.CurrencyCode == source.CurrencyCode, cancellationToken)
            ?? throw new QuantitySurveyRateLibraryValidationException(
                $"The survey currency '{source.CurrencyCode}' is not an active Finance currency for this tenant.");
        var previous = await db.QuantitySurveyRateLibraryRates.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.RateLibraryItemId == itemId && !value.IsDeleted &&
                value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published &&
                value.EffectiveFrom < effectiveFrom &&
                value.ProjectTypeId == request.ProjectTypeId && value.LocationId == request.LocationId &&
                value.BusinessPartnerId == null)
            .OrderByDescending(value => value.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
        var save = new SaveQuantitySurveyRateRequest
        {
            UnitRate = unitRate,
            CurrencyId = currency.Id,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = request.EffectiveTo,
            ProjectTypeId = request.ProjectTypeId,
            LocationId = request.LocationId,
            SourceType = QuantitySurveyRateSourceType.MarketSurvey,
            SourceReference = $"{analysis.AnalysisCode} · {request.PriceBasis}",
            SourceDate = Utc(analysis.AnalysisPeriodEnd),
            CentralDocumentVersionId = request.CentralDocumentVersionId,
            ChangeReason = request.ChangeReason
        };
        var lineage = new MarketSurveyPreparation(
            analysis.Id,
            analysis.AnalysisCode,
            previous?.Id,
            previous?.UnitRate,
            previous?.CurrencyCodeSnapshot,
            source.QuoteCount,
            source.NextReviewDueAt);
        var before = previous is null
            ? new { PreviousRate = (object?)null, Survey = source, PriceBasis = request.PriceBasis }
            : new { PreviousRate = Snapshot(previous), Survey = source, PriceBasis = request.PriceBasis };
        return await CreateRateCoreAsync(item, save, QuantitySurveyAuditEventMap.CreateMarketSurveyUpdate,
            before, lineage, historical: null, buildUp: null, correlationId, cancellationToken);
    }

    public async Task<QuantitySurveyRateDto> PrepareHistoricalRateAsync(
        Guid itemId,
        PrepareQuantitySurveyHistoricalRateRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var result = await PrepareHistoricalRateCoreAsync(
                itemId, request, correlationId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task<QuantitySurveyRateDto> PrepareHistoricalRateCoreAsync(
        Guid itemId,
        PrepareQuantitySurveyHistoricalRateRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var item = await FindItemAsync(itemId, tracking: false, cancellationToken);
        if (request.SourceId == Guid.Empty)
            throw new QuantitySurveyRateLibraryValidationException("Select an eligible historical source record.");
        var sources = await GetHistoricalRateSourcesCoreAsync(
            item, request.SourceType, request.SourceId, null, cancellationToken);
        var source = sources.SingleOrDefault(value => value.SourceId == request.SourceId &&
            value.SourceType == request.SourceType)
            ?? throw new QuantitySurveyRateLibraryValidationException(
                "The historical source is no longer eligible, accessible, completed or compatible with this rate item.");
        if (source.ExistingRateId.HasValue)
            throw new QuantitySurveyRateLibraryConflictException(
                "This historical source has already been promoted into the controlled rate library.");
        if (!FixedHashEquals(source.IntegrityHash, request.SourceIntegrityHash))
            throw new QuantitySurveyRateLibraryConflictException(
                "The historical source changed after it was selected. Refresh the source list and review the current values.");

        if (source.ProjectTypeId.HasValue && request.ProjectTypeId.HasValue &&
            source.ProjectTypeId != request.ProjectTypeId)
            throw new QuantitySurveyRateLibraryValidationException(
                "Project type is fixed by the historical project and cannot be replaced.");
        if (source.LocationId.HasValue && request.LocationId.HasValue &&
            source.LocationId != request.LocationId)
            throw new QuantitySurveyRateLibraryValidationException(
                "Location is fixed by the historical project and cannot be replaced.");
        var currency = await db.Currencies.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && !value.IsDeleted && value.Status == "Active" &&
            value.CurrencyCode == source.CurrencyCode, cancellationToken)
            ?? throw new QuantitySurveyRateLibraryValidationException(
                $"The historical currency '{source.CurrencyCode}' is not an active Finance currency for this tenant.");
        var effectiveFrom = Utc(request.EffectiveFrom);
        if (effectiveFrom == default)
            throw new QuantitySurveyRateLibraryValidationException("Effective from is required.");
        if (source.SourceDate > effectiveFrom)
            throw new QuantitySurveyRateLibraryValidationException(
                "The historical source date must be on or before the rate effective date.");
        var policy = await GetPolicyAsync(effectiveFrom, cancellationToken);
        var projectTypeId = policy.Dimensions.Contains(QuantitySurveyRateDimension.ProjectType)
            ? source.ProjectTypeId ?? request.ProjectTypeId
            : null;
        var locationId = policy.Dimensions.Contains(QuantitySurveyRateDimension.Location)
            ? source.LocationId ?? request.LocationId
            : null;
        Guid? businessPartnerId = null;
        if (source.BusinessPartnerId.HasValue &&
            (policy.Dimensions.Contains(QuantitySurveyRateDimension.Supplier) ||
             policy.Dimensions.Contains(QuantitySurveyRateDimension.Contractor)))
        {
            var partner = await db.BusinessPartners.AsNoTracking().SingleOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == source.BusinessPartnerId &&
                !value.IsDeleted && value.IsActive, cancellationToken);
            var partnerDimensionEnabled = partner?.PartnerType.Trim().ToUpperInvariant() switch
            {
                "SUPPLIER" => policy.Dimensions.Contains(QuantitySurveyRateDimension.Supplier),
                "CONTRACTOR" => policy.Dimensions.Contains(QuantitySurveyRateDimension.Contractor),
                "BOTH" => policy.Dimensions.Contains(QuantitySurveyRateDimension.Supplier) ||
                          policy.Dimensions.Contains(QuantitySurveyRateDimension.Contractor),
                _ => false
            };
            if (partnerDimensionEnabled) businessPartnerId = partner!.Id;
        }
        var previous = await db.QuantitySurveyRateLibraryRates.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.RateLibraryItemId == itemId && !value.IsDeleted &&
                value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published &&
                value.EffectiveFrom < effectiveFrom && value.ProjectTypeId == projectTypeId &&
                value.LocationId == locationId && value.BusinessPartnerId == businessPartnerId)
            .OrderByDescending(value => value.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
        var save = new SaveQuantitySurveyRateRequest
        {
            UnitRate = source.UnitRate,
            CurrencyId = currency.Id,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = request.EffectiveTo,
            ProjectTypeId = projectTypeId,
            LocationId = locationId,
            BusinessPartnerId = businessPartnerId,
            SourceType = QuantitySurveyRateSourceType.HistoricalProject,
            SourceReference = source.SourceReference,
            SourceDate = source.SourceDate,
            CentralDocumentVersionId = request.CentralDocumentVersionId,
            ChangeReason = request.ChangeReason
        };
        var lineage = new HistoricalPreparation(
            source.SourceType,
            source.SourceId,
            source.ProjectId,
            source.ProjectCode,
            source.SourceLabel,
            source.UnitOfMeasure,
            source.Quantity,
            source.TotalAmount,
            source.IntegrityHash,
            previous?.Id,
            previous?.UnitRate,
            previous?.CurrencyCodeSnapshot);
        var before = previous is null
            ? new { PreviousRate = (object?)null, HistoricalSource = source }
            : new { PreviousRate = Snapshot(previous), HistoricalSource = source };
        return await CreateRateCoreAsync(item, save, QuantitySurveyAuditEventMap.PromoteHistoricalRate,
            before, marketSurvey: null, lineage, buildUp: null, correlationId, cancellationToken);
    }

    private async Task<QuantitySurveyRateDto> CreateRateCoreAsync(
        QuantitySurveyRateLibraryItem item,
        SaveQuantitySurveyRateRequest request,
        string auditAction,
        object? before,
        MarketSurveyPreparation? marketSurvey,
        HistoricalPreparation? historical,
        RateBuildUpPreparation? buildUp,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!item.IsActive) throw new QuantitySurveyRateLibraryConflictException("Rates cannot be added to an inactive rate-library item.");
        if ((request.SourceType == QuantitySurveyRateSourceType.MarketSurvey) != (marketSurvey is not null))
            throw new QuantitySurveyRateLibraryValidationException(
                "Market-survey rates must be prepared through the governed survey update workflow.");
        if ((request.SourceType == QuantitySurveyRateSourceType.HistoricalProject) != (historical is not null))
            throw new QuantitySurveyRateLibraryValidationException(
                "Historical project rates must be prepared through the governed historical-cost promotion workflow.");
        if ((request.SourceType == QuantitySurveyRateSourceType.RateBuildUp) != (buildUp is not null))
            throw new QuantitySurveyRateLibraryValidationException(
                "Rate build-ups must be prepared through the governed component calculation workflow.");
        var references = await ValidateRateAsync(item, request, cancellationToken);
        var version = (await db.QuantitySurveyRateLibraryRates.IgnoreQueryFilters()
            .Where(value => value.TenantId == TenantId && value.RateLibraryItemId == item.Id)
            .MaxAsync(value => (int?)value.Version, cancellationToken) ?? 0) + 1;
        var now = DateTime.UtcNow;
        var entity = new QuantitySurveyRateLibraryRate
        {
            TenantId = TenantId,
            RateLibraryItemId = item.Id,
            Version = version,
            UnitRate = request.UnitRate,
            CurrencyId = references.Currency.Id,
            CurrencyCodeSnapshot = references.Currency.CurrencyCode,
            EffectiveFrom = Utc(request.EffectiveFrom),
            EffectiveTo = Utc(request.EffectiveTo),
            ProjectTypeId = request.ProjectTypeId,
            LocationId = request.LocationId,
            BusinessPartnerId = request.BusinessPartnerId,
            SourceType = request.SourceType,
            SourceReference = Clean(request.SourceReference, 250),
            SourceDate = Utc(request.SourceDate),
            CentralDocumentRecordId = references.Evidence?.DocumentRecordId,
            CentralDocumentVersionId = references.Evidence?.Id,
            MarketAnalysisId = marketSurvey?.MarketAnalysisId,
            MarketAnalysisCodeSnapshot = marketSurvey?.MarketAnalysisCode,
            MarketSurveyQuoteCount = marketSurvey?.QuoteCount,
            NextReviewDueAt = marketSurvey?.NextReviewDueAt,
            HistoricalSourceType = historical?.SourceType,
            HistoricalSourceId = historical?.SourceId,
            HistoricalProjectId = historical?.ProjectId,
            HistoricalProjectCodeSnapshot = historical?.ProjectCode,
            HistoricalSourceLabelSnapshot = historical?.SourceLabel,
            HistoricalUnitOfMeasureSnapshot = historical?.UnitOfMeasure,
            HistoricalQuantity = historical?.Quantity,
            HistoricalTotalAmount = historical?.TotalAmount,
            HistoricalSourceHash = historical?.IntegrityHash,
            RateBuildUpId = buildUp?.BuildUpId,
            PreviousRateId = buildUp?.PreviousRateId ?? historical?.PreviousRateId ?? marketSurvey?.PreviousRateId,
            PreviousUnitRate = buildUp?.PreviousUnitRate ?? historical?.PreviousUnitRate ?? marketSurvey?.PreviousUnitRate,
            PreviousCurrencyCodeSnapshot = buildUp?.PreviousCurrencyCode ?? historical?.PreviousCurrencyCode ?? marketSurvey?.PreviousCurrencyCode,
            LifecycleStatus = QuantitySurveyRateLifecycleStatus.Draft,
            ChangeReason = Required(request.ChangeReason, "Change reason", 1000),
            PreparedById = UserId,
            PreparedAt = now,
            AuditAction = auditAction,
            CorrelationId = RequiredCorrelation(correlationId),
            ActorRoles = ActorRoles,
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        db.QuantitySurveyRateLibraryRates.Add(entity);
        object after = buildUp is null
            ? Snapshot(entity)
            : new { Rate = Snapshot(entity), RateBuildUp = buildUp.AuditSnapshot };
        AddRevision(item.Id, entity.Id, entity.AuditAction, correlationId, entity.ChangeReason, before, after);
        await SaveAsync(cancellationToken);
        return MapRate(await FindRateAsync(item.Id, entity.Id, tracking: false, cancellationToken));
    }

    public async Task<QuantitySurveyRateDto> UpdateRateAsync(
        Guid itemId,
        Guid rateId,
        UpdateQuantitySurveyRateRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var item = await FindItemAsync(itemId, tracking: false, cancellationToken);
        var entity = await FindRateAsync(itemId, rateId, tracking: true, cancellationToken);
        if (entity.LifecycleStatus != QuantitySurveyRateLifecycleStatus.Draft)
            throw new QuantitySurveyRateLibraryConflictException("Only a draft rate can be amended. Create a new rate version instead.");
        if (entity.SourceType is QuantitySurveyRateSourceType.MarketSurvey or QuantitySurveyRateSourceType.HistoricalProject or QuantitySurveyRateSourceType.RateBuildUp ||
            request.SourceType is QuantitySurveyRateSourceType.MarketSurvey or QuantitySurveyRateSourceType.HistoricalProject or QuantitySurveyRateSourceType.RateBuildUp or QuantitySurveyRateSourceType.PurchaseOrder)
            throw new QuantitySurveyRateLibraryValidationException(
                "Governed source drafts preserve immutable source and comparison snapshots. Prepare a replacement source update instead.");
        CheckVersion(entity.RowVersion, request.RowVersion, "rate version");
        var references = await ValidateRateAsync(item, request, cancellationToken);
        var before = Snapshot(entity);
        entity.UnitRate = request.UnitRate;
        entity.CurrencyId = references.Currency.Id;
        entity.CurrencyCodeSnapshot = references.Currency.CurrencyCode;
        entity.EffectiveFrom = Utc(request.EffectiveFrom);
        entity.EffectiveTo = Utc(request.EffectiveTo);
        entity.ProjectTypeId = request.ProjectTypeId;
        entity.LocationId = request.LocationId;
        entity.BusinessPartnerId = request.BusinessPartnerId;
        entity.SourceType = request.SourceType;
        entity.SourceReference = Clean(request.SourceReference, 250);
        entity.SourceDate = Utc(request.SourceDate);
        entity.CentralDocumentRecordId = references.Evidence?.DocumentRecordId;
        entity.CentralDocumentVersionId = references.Evidence?.Id;
        entity.ChangeReason = Required(request.ChangeReason, "Change reason", 1000);
        entity.AuditAction = QuantitySurveyAuditEventMap.UpdateRateDraft;
        entity.CorrelationId = RequiredCorrelation(correlationId);
        entity.ActorRoles = ActorRoles;
        Touch(entity);
        AddRevision(itemId, rateId, entity.AuditAction, correlationId, entity.ChangeReason, before, Snapshot(entity));
        await SaveAsync(cancellationToken);
        return MapRate(await FindRateAsync(itemId, rateId, tracking: false, cancellationToken));
    }

    public async Task<QuantitySurveyRateDto> PublishRateAsync(
        Guid itemId,
        Guid rateId,
        QuantitySurveyRateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var candidate = await FindRateAsync(itemId, rateId, tracking: true, cancellationToken);
            if (candidate.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published)
                return MapRate(candidate);
            CheckVersion(candidate.RowVersion, request.RowVersion, "rate version");
            if (candidate.LifecycleStatus != QuantitySurveyRateLifecycleStatus.Draft)
                throw new QuantitySurveyRateLibraryConflictException("Only a draft rate can be published.");
            if (candidate.PreparedById == UserId)
                throw new QuantitySurveyRateLibraryConflictException("The rate preparer cannot publish the same rate version.");

            var reason = Required(request.Reason, "Publication reason", 1000);
            var existing = await db.QuantitySurveyRateLibraryRates
                .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.Id != candidate.Id &&
                    value.RateLibraryItemId == itemId &&
                    value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published &&
                    value.ProjectTypeId == candidate.ProjectTypeId && value.LocationId == candidate.LocationId &&
                    value.BusinessPartnerId == candidate.BusinessPartnerId)
                .OrderByDescending(value => value.EffectiveFrom)
                .ToListAsync(cancellationToken);
            if (existing.Any(value => value.EffectiveFrom >= candidate.EffectiveFrom))
                throw new QuantitySurveyRateLibraryConflictException("A published rate for the same dimensions already starts on or after this effective date.");

            var now = DateTime.UtcNow;
            foreach (var current in existing.Where(value => !value.EffectiveTo.HasValue || value.EffectiveTo >= candidate.EffectiveFrom))
            {
                var beforeCurrent = Snapshot(current);
                current.EffectiveTo = candidate.EffectiveFrom.AddTicks(-1);
                current.ChangeReason = $"Superseded by rate v{candidate.Version}. {reason}";
                if (candidate.EffectiveFrom <= now)
                {
                    current.LifecycleStatus = QuantitySurveyRateLifecycleStatus.Retired;
                    current.RetiredAt = now;
                    current.RetiredById = UserId;
                }
                current.AuditAction = QuantitySurveyAuditEventMap.RetireRate;
                current.CorrelationId = RequiredCorrelation(correlationId);
                current.ActorRoles = ActorRoles;
                Touch(current);
                AddRevision(itemId, current.Id, current.AuditAction, correlationId, current.ChangeReason, beforeCurrent, Snapshot(current));
            }

            var before = Snapshot(candidate);
            candidate.LifecycleStatus = QuantitySurveyRateLifecycleStatus.Published;
            candidate.PublishedAt = now;
            candidate.PublishedById = UserId;
            candidate.ChangeReason = reason;
            candidate.AuditAction = QuantitySurveyAuditEventMap.PublishRate;
            candidate.CorrelationId = RequiredCorrelation(correlationId);
            candidate.ActorRoles = ActorRoles;
            Touch(candidate);
            AddRevision(itemId, candidate.Id, candidate.AuditAction, correlationId, reason, before, Snapshot(candidate));
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MapRate(await FindRateAsync(itemId, rateId, tracking: false, cancellationToken));
        });
    }

    public async Task<QuantitySurveyRateDto> RetireRateAsync(
        Guid itemId,
        Guid rateId,
        QuantitySurveyRateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindRateAsync(itemId, rateId, tracking: true, cancellationToken);
        if (entity.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Retired) return MapRate(entity);
        CheckVersion(entity.RowVersion, request.RowVersion, "rate version");
        if (entity.LifecycleStatus != QuantitySurveyRateLifecycleStatus.Published)
            throw new QuantitySurveyRateLibraryConflictException("Only a published rate can be retired.");
        var before = Snapshot(entity);
        entity.LifecycleStatus = QuantitySurveyRateLifecycleStatus.Retired;
        entity.RetiredAt = DateTime.UtcNow;
        entity.RetiredById = UserId;
        entity.ChangeReason = Required(request.Reason, "Retirement reason", 1000);
        entity.AuditAction = QuantitySurveyAuditEventMap.RetireRate;
        entity.CorrelationId = RequiredCorrelation(correlationId);
        entity.ActorRoles = ActorRoles;
        Touch(entity);
        AddRevision(itemId, rateId, entity.AuditAction, correlationId, entity.ChangeReason, before, Snapshot(entity));
        await SaveAsync(cancellationToken);
        return MapRate(await FindRateAsync(itemId, rateId, tracking: false, cancellationToken));
    }

    public async Task<IReadOnlyList<QuantitySurveyRateLibraryRevisionDto>> GetHistoryAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        _ = await FindItemAsync(itemId, tracking: false, cancellationToken);
        return await db.QuantitySurveyRateLibraryRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.RateLibraryItemId == itemId && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new QuantitySurveyRateLibraryRevisionDto
            {
                Id = value.Id,
                RateLibraryItemId = value.RateLibraryItemId,
                RateId = value.RateId,
                Action = value.Action,
                ActorUserId = value.ActorUserId,
                ActorName = value.ActorName,
                ActorRoles = value.ActorRoles,
                CorrelationId = value.CorrelationId,
                Reason = value.Reason,
                BeforeJson = value.BeforeJson,
                AfterJson = value.AfterJson,
                CreatedAt = value.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private async Task ValidateItemReferencesAsync(
        CreateQuantitySurveyRateLibraryItemRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UnitOfMeasureId == Guid.Empty || !await db.UnitsOfMeasure.AsNoTracking().AnyAsync(
                value => value.TenantId == TenantId && value.Id == request.UnitOfMeasureId && !value.IsDeleted && value.IsActive,
                cancellationToken))
            throw new QuantitySurveyRateLibraryValidationException("Select an active unit of measure from the shared inventory master.");
        if (request.ProjectCatalogEntryId.HasValue && !await db.ProjectCatalogEntries.AsNoTracking().AnyAsync(
                value => value.TenantId == TenantId && value.Id == request.ProjectCatalogEntryId && !value.IsDeleted &&
                    value.IsActive && value.CatalogType.StartsWith("qs-"), cancellationToken))
            throw new QuantitySurveyRateLibraryValidationException("Select an active QS catalogue entry from the shared project catalogue.");
        if (request.InventoryItemId.HasValue && !await db.InventoryItems.AsNoTracking().AnyAsync(
                value => value.TenantId == TenantId && value.Id == request.InventoryItemId && !value.IsDeleted &&
                    value.Status == ItemStatus.Active, cancellationToken))
            throw new QuantitySurveyRateLibraryValidationException("Select an active inventory item from the shared inventory master.");
    }

    private async Task<ValidatedRateReferences> ValidateRateAsync(
        QuantitySurveyRateLibraryItem item,
        SaveQuantitySurveyRateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UnitRate < 0) throw new QuantitySurveyRateLibraryValidationException("Unit rate cannot be negative.");
        var effectiveFrom = Utc(request.EffectiveFrom);
        var effectiveTo = Utc(request.EffectiveTo);
        if (effectiveFrom == default) throw new QuantitySurveyRateLibraryValidationException("Effective from is required.");
        if (effectiveTo.HasValue && effectiveTo < effectiveFrom)
            throw new QuantitySurveyRateLibraryValidationException("Effective to cannot be before effective from.");
        if (request.SourceDate == default) throw new QuantitySurveyRateLibraryValidationException("Source date is required.");

        var currency = await db.Currencies.AsNoTracking().SingleOrDefaultAsync(
            value => value.TenantId == TenantId && value.Id == request.CurrencyId && !value.IsDeleted && value.Status == "Active",
            cancellationToken) ?? throw new QuantitySurveyRateLibraryValidationException("Select an active currency from the shared Finance currency master.");
        if (request.ProjectTypeId.HasValue && !await db.ProjectTypes.AsNoTracking().AnyAsync(
                value => value.TenantId == TenantId && value.Id == request.ProjectTypeId && !value.IsDeleted && value.IsActive,
                cancellationToken))
            throw new QuantitySurveyRateLibraryValidationException("Select an active project type from the shared project master.");
        if (request.LocationId.HasValue && !await db.Locations.AsNoTracking().AnyAsync(
                value => value.TenantId == TenantId && value.Id == request.LocationId && !value.IsDeleted && value.IsActive,
                cancellationToken))
            throw new QuantitySurveyRateLibraryValidationException("Select an active region or location from the shared HR location master.");

        Core.Entities.Procurement.BusinessPartner? partner = null;
        if (request.BusinessPartnerId.HasValue)
        {
            partner = await db.BusinessPartners.AsNoTracking().SingleOrDefaultAsync(
                value => value.TenantId == TenantId && value.Id == request.BusinessPartnerId && !value.IsDeleted && value.IsActive,
                cancellationToken) ?? throw new QuantitySurveyRateLibraryValidationException("Select an active supplier or contractor from the shared business-partner master.");
            if (request.SourceType == QuantitySurveyRateSourceType.SupplierQuotation &&
                !BusinessPartnerRoles.HasSupplier(partner.PartnerType))
                throw new QuantitySurveyRateLibraryValidationException("A supplier quotation must reference a supplier or Both-type business partner.");
            if ((request.SourceType == QuantitySurveyRateSourceType.ContractorQuotation || item.Category == QuantitySurveyRateItemCategory.Subcontract) &&
                partner.PartnerType is not ("Contractor" or "Both"))
                throw new QuantitySurveyRateLibraryValidationException("This rate must reference a contractor or Both-type business partner.");
        }

        CentralDocumentVersion? evidence = null;
        if (request.CentralDocumentVersionId.HasValue)
        {
            evidence = await db.CentralDocumentVersions.AsNoTracking()
                .Include(value => value.DocumentRecord)
                .Where(value => value.TenantId == TenantId && value.Id == request.CentralDocumentVersionId)
                .Where(CentralDocumentEvidenceRules.CurrentPublished())
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new QuantitySurveyRateLibraryValidationException("Select a current published evidence document from the central DMS.");
        }

        var policy = await GetPolicyAsync(effectiveFrom, cancellationToken);
        ValidatePolicy(policy, request, item, partner, evidence);
        return new ValidatedRateReferences(currency, evidence);
    }

    private static void ValidatePolicy(
        QsRateLibraryValue policy,
        SaveQuantitySurveyRateRequest request,
        QuantitySurveyRateLibraryItem item,
        Core.Entities.Procurement.BusinessPartner? partner,
        CentralDocumentVersion? evidence)
    {
        var projectDimension = policy.Dimensions.Contains(QuantitySurveyRateDimension.ProjectType);
        var locationDimension = policy.Dimensions.Contains(QuantitySurveyRateDimension.Location);
        var contractorDimension = policy.Dimensions.Contains(QuantitySurveyRateDimension.Contractor);
        var supplierDimension = policy.Dimensions.Contains(QuantitySurveyRateDimension.Supplier);
        if (projectDimension && !request.ProjectTypeId.HasValue)
            throw new QuantitySurveyRateLibraryValidationException("Project type is required by the effective QS rate-library policy.");
        if (!projectDimension && request.ProjectTypeId.HasValue)
            throw new QuantitySurveyRateLibraryValidationException("Project type is not enabled as a rate dimension in the effective QS policy.");
        if (locationDimension && !request.LocationId.HasValue)
            throw new QuantitySurveyRateLibraryValidationException("Region or location is required by the effective QS rate-library policy.");
        if (!locationDimension && request.LocationId.HasValue)
            throw new QuantitySurveyRateLibraryValidationException("Region or location is not enabled as a rate dimension in the effective QS policy.");
        if (request.ProjectTypeId.HasValue && !policy.ProjectTypeIds.Contains(request.ProjectTypeId.Value))
            throw new QuantitySurveyRateLibraryValidationException("The selected project type is not permitted by the effective QS rate-library policy.");
        if (request.LocationId.HasValue && !policy.LocationIds.Contains(request.LocationId.Value))
            throw new QuantitySurveyRateLibraryValidationException("The selected location is not permitted by the effective QS rate-library policy.");
        if (request.BusinessPartnerId.HasValue && !contractorDimension && !supplierDimension)
            throw new QuantitySurveyRateLibraryValidationException("Supplier and contractor dimensions are not enabled in the effective QS policy.");
        if (item.Category == QuantitySurveyRateItemCategory.Subcontract && contractorDimension && partner is null)
            throw new QuantitySurveyRateLibraryValidationException("A contractor is required for subcontract rates.");
        if (request.SourceType == QuantitySurveyRateSourceType.SupplierQuotation && supplierDimension && partner is null)
            throw new QuantitySurveyRateLibraryValidationException("A supplier is required for supplier-quotation rates.");
        if (policy.RequireMarketEvidence && request.SourceType is
                QuantitySurveyRateSourceType.MarketSurvey or
                QuantitySurveyRateSourceType.SupplierQuotation or
                QuantitySurveyRateSourceType.ContractorQuotation && evidence is null)
            throw new QuantitySurveyRateLibraryValidationException("A current published central-DMS evidence document is required for this market rate source.");
    }

    private async Task<QsRateLibraryValue> GetPolicyAsync(DateTime atUtc, CancellationToken cancellationToken)
    {
        var at = Utc(atUtc);
        var profile = await db.QuantitySurveyConfigurationProfiles.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                value.ProfileCode == "TDC-QUANTITY-SURVEY" &&
                value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published &&
                value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new QuantitySurveyRateLibraryValidationException("No published QS configuration profile is effective for the rate date.");
        var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profile.Id &&
                !value.IsDeleted && value.DecisionKey == "QS-DEC-005", cancellationToken)
            ?? throw new QuantitySurveyRateLibraryValidationException("The effective QS configuration does not contain the rate-library policy (QS-DEC-005).");
        return JsonSerializer.Deserialize<QsRateLibraryValue>(decision.ValueJson, JsonOptions)
            ?? throw new QuantitySurveyRateLibraryValidationException("The effective QS rate-library policy could not be read.");
    }

    private async Task<QuantitySurveyRateLibraryItem> FindItemAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.QuantitySurveyRateLibraryItems.AsQueryable() : db.QuantitySurveyRateLibraryItems.AsNoTracking();
        query = query
            .Include(value => value.UnitOfMeasure)
            .Include(value => value.ProjectCatalogEntry)
            .Include(value => value.InventoryItem)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted)).ThenInclude(rate => rate.ProjectType)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted)).ThenInclude(rate => rate.Location)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted)).ThenInclude(rate => rate.BusinessPartner)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted)).ThenInclude(rate => rate.CentralDocumentRecord)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted)).ThenInclude(rate => rate.CentralDocumentVersion)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted)).ThenInclude(rate => rate.MarketAnalysis)
            .Include(value => value.Rates.Where(rate => !rate.IsDeleted)).ThenInclude(rate => rate.HistoricalProject);
        return await query.SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, cancellationToken)
            ?? throw new QuantitySurveyRateLibraryNotFoundException("The rate-library item was not found for this tenant.");
    }

    private async Task<QuantitySurveyRateLibraryRate> FindRateAsync(Guid itemId, Guid rateId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.QuantitySurveyRateLibraryRates.AsQueryable() : db.QuantitySurveyRateLibraryRates.AsNoTracking();
        query = query
            .Include(value => value.ProjectType)
            .Include(value => value.Location)
            .Include(value => value.BusinessPartner)
            .Include(value => value.CentralDocumentRecord)
            .Include(value => value.CentralDocumentVersion)
            .Include(value => value.MarketAnalysis)
            .Include(value => value.HistoricalProject);
        return await query.SingleOrDefaultAsync(value => value.TenantId == TenantId && value.RateLibraryItemId == itemId &&
                value.Id == rateId && !value.IsDeleted, cancellationToken)
            ?? throw new QuantitySurveyRateLibraryNotFoundException("The rate version was not found for this tenant and rate item.");
    }

    private void AddRevision(Guid itemId, Guid? rateId, string action, string correlationId, string? reason, object? before, object? after)
    {
        QuantitySurveyAuditEventMap.GetRequired(action);
        db.QuantitySurveyRateLibraryRevisions.Add(new QuantitySurveyRateLibraryRevision
        {
            TenantId = TenantId,
            RateLibraryItemId = itemId,
            RateId = rateId,
            Action = action,
            ActorUserId = UserId,
            ActorName = UserName,
            ActorRoles = ActorRoles,
            CorrelationId = RequiredCorrelation(correlationId),
            Reason = Clean(reason, 1000),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = UserId
        });
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new QuantitySurveyRateLibraryConflictException("The rate-library record changed after it was loaded. Refresh and try again.");
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            throw new QuantitySurveyRateLibraryConflictException("A rate-library record with the same controlled key already exists.");
        }
    }

    private static bool IsUniqueConflict(DbUpdateException exception)
        => exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
           exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true;

    private static QuantitySurveyRateLibraryItemDto MapItem(QuantitySurveyRateLibraryItem entity, DateTime effectiveAt)
    {
        var rates = entity.Rates.Where(value => !value.IsDeleted).OrderByDescending(value => value.Version).Select(MapRate).ToList();
        var current = rates
            .Where(value => value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published &&
                value.EffectiveFrom <= effectiveAt && (!value.EffectiveTo.HasValue || value.EffectiveTo >= effectiveAt))
            .OrderByDescending(value => value.EffectiveFrom)
            .FirstOrDefault();
        return new QuantitySurveyRateLibraryItemDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            UnitOfMeasureId = entity.UnitOfMeasureId,
            UnitOfMeasureCode = entity.UnitOfMeasure.Code,
            UnitOfMeasureName = entity.UnitOfMeasure.Name,
            ProjectCatalogEntryId = entity.ProjectCatalogEntryId,
            ProjectCatalogEntryLabel = entity.ProjectCatalogEntry is null ? null : $"{entity.ProjectCatalogEntry.Code} · {entity.ProjectCatalogEntry.Name}",
            InventoryItemId = entity.InventoryItemId,
            InventoryItemLabel = entity.InventoryItem is null ? null : $"{entity.InventoryItem.ItemCode} · {entity.InventoryItem.Name}",
            IsActive = entity.IsActive,
            RateCount = rates.Count,
            CurrentRate = current,
            Rates = rates,
            RowVersion = Encode(entity.RowVersion)
        };
    }

    private static QuantitySurveyRateDto MapRate(QuantitySurveyRateLibraryRate entity) => new()
    {
        Id = entity.Id,
        RateLibraryItemId = entity.RateLibraryItemId,
        Version = entity.Version,
        UnitRate = entity.UnitRate,
        CurrencyId = entity.CurrencyId,
        CurrencyCode = entity.CurrencyCodeSnapshot,
        EffectiveFrom = entity.EffectiveFrom,
        EffectiveTo = entity.EffectiveTo,
        ProjectTypeId = entity.ProjectTypeId,
        ProjectTypeLabel = entity.ProjectType is null ? null : $"{entity.ProjectType.Code} · {entity.ProjectType.Name}",
        LocationId = entity.LocationId,
        LocationLabel = entity.Location is null ? null : $"{entity.Location.Code} · {entity.Location.Name}",
        BusinessPartnerId = entity.BusinessPartnerId,
        BusinessPartnerLabel = entity.BusinessPartner is null ? null : $"{entity.BusinessPartner.PartnerCode} · {entity.BusinessPartner.PartnerName}",
        SourceType = entity.SourceType,
        SourceReference = entity.SourceReference,
        SourceDate = entity.SourceDate,
        CentralDocumentRecordId = entity.CentralDocumentRecordId,
        CentralDocumentVersionId = entity.CentralDocumentVersionId,
        EvidenceLabel = entity.CentralDocumentRecord is null ? null : $"{entity.CentralDocumentRecord.DocumentReference} · {entity.CentralDocumentRecord.Title} · {entity.CentralDocumentVersion?.VersionNumber}",
        MarketAnalysisId = entity.MarketAnalysisId,
        MarketAnalysisCode = entity.MarketAnalysisCodeSnapshot,
        PreviousRateId = entity.PreviousRateId,
        PreviousUnitRate = entity.PreviousUnitRate,
        PreviousCurrencyCode = entity.PreviousCurrencyCodeSnapshot,
        VarianceAmount = entity.PreviousUnitRate.HasValue ? entity.UnitRate - entity.PreviousUnitRate.Value : null,
        VariancePercent = entity.PreviousUnitRate > 0
            ? Math.Round((entity.UnitRate - entity.PreviousUnitRate.Value) / entity.PreviousUnitRate.Value * 100m, 2)
            : null,
        MarketSurveyQuoteCount = entity.MarketSurveyQuoteCount,
        NextReviewDueAt = entity.NextReviewDueAt,
        HistoricalSourceType = entity.HistoricalSourceType,
        HistoricalSourceId = entity.HistoricalSourceId,
        HistoricalProjectId = entity.HistoricalProjectId,
        HistoricalProjectCode = entity.HistoricalProjectCodeSnapshot,
        HistoricalSourceLabel = entity.HistoricalSourceLabelSnapshot,
        HistoricalUnitOfMeasure = entity.HistoricalUnitOfMeasureSnapshot,
        HistoricalQuantity = entity.HistoricalQuantity,
        HistoricalTotalAmount = entity.HistoricalTotalAmount,
        HistoricalSourceHash = entity.HistoricalSourceHash,
        RateBuildUpId = entity.RateBuildUpId,
        LifecycleStatus = entity.LifecycleStatus,
        ChangeReason = entity.ChangeReason,
        PreparedById = entity.PreparedById,
        PreparedAt = entity.PreparedAt,
        PublishedById = entity.PublishedById,
        PublishedAt = entity.PublishedAt,
        RetiredById = entity.RetiredById,
        RetiredAt = entity.RetiredAt,
        RowVersion = Encode(entity.RowVersion)
    };

    private static object Snapshot(QuantitySurveyRateLibraryItem value) => new
    {
        value.Id, value.Code, value.Name, value.Description, value.Category, value.UnitOfMeasureId,
        value.ProjectCatalogEntryId, value.InventoryItemId, value.IsActive
    };

    private static object Snapshot(QuantitySurveyRateLibraryRate value) => new
    {
        value.Id, value.RateLibraryItemId, value.Version, value.UnitRate, value.CurrencyId,
        value.CurrencyCodeSnapshot, value.EffectiveFrom, value.EffectiveTo, value.ProjectTypeId,
        value.LocationId, value.BusinessPartnerId, value.SourceType, value.SourceReference,
        value.SourceDate, value.CentralDocumentRecordId, value.CentralDocumentVersionId,
        value.MarketAnalysisId, value.MarketAnalysisCodeSnapshot, value.PreviousRateId,
        value.PreviousUnitRate, value.PreviousCurrencyCodeSnapshot, value.MarketSurveyQuoteCount,
        value.NextReviewDueAt, value.HistoricalSourceType, value.HistoricalSourceId,
        value.HistoricalProjectId, value.HistoricalProjectCodeSnapshot,
        value.HistoricalSourceLabelSnapshot, value.HistoricalUnitOfMeasureSnapshot,
        value.HistoricalQuantity, value.HistoricalTotalAmount, value.HistoricalSourceHash,
        value.RateBuildUpId,
        value.LifecycleStatus, value.PreparedById, value.PreparedAt, value.PublishedById,
        value.PublishedAt, value.RetiredById, value.RetiredAt
    };

    private static QuantitySurveyHistoricalRateSourceDto Candidate(
        QuantitySurveyHistoricalRateSourceType sourceType,
        Guid sourceId,
        Project project,
        string sourceReference,
        string sourceLabel,
        DateTime sourceDate,
        string unitOfMeasure,
        decimal quantity,
        decimal unitRate,
        decimal totalAmount,
        string currencyCode,
        Guid? businessPartnerId = null)
    {
        var candidate = new QuantitySurveyHistoricalRateSourceDto
        {
            SourceType = sourceType,
            SourceId = sourceId,
            ProjectId = project.Id,
            ProjectCode = project.ProjectCode,
            ProjectTitle = project.Title,
            ProjectTypeId = project.ProjectTypeId,
            LocationId = project.LocationId,
            BusinessPartnerId = businessPartnerId ?? project.BusinessPartnerId,
            SourceReference = Required(sourceReference, "Historical source reference", 250),
            SourceLabel = Required(sourceLabel, "Historical source label", 250),
            SourceDate = Utc(sourceDate),
            UnitOfMeasure = Required(unitOfMeasure, "Historical source unit", 20),
            Quantity = quantity,
            UnitRate = unitRate,
            TotalAmount = totalAmount,
            CurrencyCode = NormalizeCurrency(currencyCode)
        };
        return candidate with { IntegrityHash = HistoricalHash(candidate) };
    }

    private static string HistoricalHash(QuantitySurveyHistoricalRateSourceDto value)
    {
        var canonical = string.Join("|",
            (int)value.SourceType,
            value.SourceId.ToString("N"),
            value.ProjectId.ToString("N"),
            value.ProjectTypeId?.ToString("N") ?? string.Empty,
            value.LocationId?.ToString("N") ?? string.Empty,
            value.BusinessPartnerId?.ToString("N") ?? string.Empty,
            Utc(value.SourceDate).Ticks.ToString(CultureInfo.InvariantCulture),
            value.SourceReference.Trim().ToUpperInvariant(),
            value.SourceLabel.Trim().ToUpperInvariant(),
            value.UnitOfMeasure.Trim().ToUpperInvariant(),
            value.Quantity.ToString("0.####", CultureInfo.InvariantCulture),
            value.UnitRate.ToString("0.####", CultureInfo.InvariantCulture),
            value.TotalAmount.ToString("0.##", CultureInfo.InvariantCulture),
            value.CurrencyCode.Trim().ToUpperInvariant());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool FixedHashEquals(string current, string supplied)
    {
        if (current.Length != 64 || supplied?.Trim().Length != 64) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(current),
                Convert.FromHexString(supplied.Trim()));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool HistoricalCodeMatches(QuantitySurveyRateLibraryItem item, string? sourceCode)
    {
        var normalized = sourceCode?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) &&
            (normalized.Equals(item.Code, StringComparison.OrdinalIgnoreCase) ||
             (item.ProjectCatalogEntry is not null &&
              normalized.Equals(item.ProjectCatalogEntry.Code, StringComparison.OrdinalIgnoreCase)));
    }

    private static QuantitySurveyMarketSurveySourceDto MapMarketSurveySource(
        MarketAnalysis analysis,
        int cadenceMonths,
        DateTime referenceAt)
    {
        var quotes = UsableMarketSurveyQuotes(analysis).OrderByDescending(value => value.PriceDate).ToList();
        var current = analysis.CurrentMarketPrice;
        var nextReview = Utc(analysis.AnalysisPeriodEnd).AddMonths(cadenceMonths);
        return new QuantitySurveyMarketSurveySourceDto
        {
            Id = analysis.Id,
            AnalysisCode = analysis.AnalysisCode,
            Title = analysis.Title,
            ItemCategory = analysis.ItemCategory,
            ItemDescription = analysis.ItemDescription,
            AnalysisPeriodStart = Utc(analysis.AnalysisPeriodStart),
            AnalysisPeriodEnd = Utc(analysis.AnalysisPeriodEnd),
            CurrencyCode = NormalizeCurrency(analysis.Currency),
            CurrentMarketPrice = current,
            AverageSurveyPrice = quotes.Count == 0 ? current : Math.Round(quotes.Average(value => value.UnitPrice), 6),
            LowestSurveyPrice = quotes.Count == 0 ? current : quotes.Min(value => value.UnitPrice),
            HighestSurveyPrice = quotes.Count == 0 ? current : quotes.Max(value => value.UnitPrice),
            ForecastedPrice = analysis.ForecastedPrice,
            QuoteCount = quotes.Count,
            LatestQuoteDate = quotes.FirstOrDefault()?.PriceDate,
            NextReviewDueAt = nextReview,
            IsOverdue = Utc(referenceAt) > nextReview
        };
    }

    private static IEnumerable<PriceHistory> UsableMarketSurveyQuotes(MarketAnalysis analysis)
    {
        var currency = NormalizeCurrency(analysis.Currency);
        return (analysis.PriceHistories ?? [])
            .Where(value => !value.IsDeleted && value.UnitPrice > 0 &&
                NormalizeCurrency(value.Currency) == currency &&
                IsMarketSurveySource(value.PriceSource));
    }

    private static bool IsMarketSurveySource(string? value)
        => value is not null && (value.Equals("Quote", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("MarketSurvey", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("SupplierQuote", StringComparison.OrdinalIgnoreCase));

    private static bool MatchesUnit(string? value, string code, string name)
    {
        var normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) &&
            (normalized.Equals(code, StringComparison.OrdinalIgnoreCase) ||
             normalized.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCurrency(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static QuantitySurveyLookupOptionDto Option(Guid id, string label, string? group = null)
        => new() { Value = id.ToString(), Label = label, Group = group };

    private static string NormalizeCode(string? value)
        => Required(value, "Item code", 50).ToUpperInvariant();

    private static string Required(string? value, string label, int maxLength)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean)) throw new QuantitySurveyRateLibraryValidationException($"{label} is required.");
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    private static string RequiredCorrelation(string? value)
        => string.IsNullOrWhiteSpace(value) ? throw new QuantitySurveyRateLibraryValidationException("A correlation ID is required.") : value.Trim()[..Math.Min(value.Trim().Length, 100)];

    private static string? Clean(string? value, int maxLength)
    {
        var clean = value?.Trim();
        return string.IsNullOrWhiteSpace(clean) ? null : clean[..Math.Min(clean.Length, maxLength)];
    }

    private void Touch(Core.Entities.BaseEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = UserName;
        entity.LastModifiedById = UserId;
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static DateTime? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    private static string Encode(byte[] value) => Convert.ToBase64String(value ?? []);

    private static void CheckVersion(byte[] current, string supplied, string label)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException) { throw new QuantitySurveyRateLibraryConflictException($"The {label} concurrency token is invalid. Refresh and try again."); }
        if (!current.SequenceEqual(expected))
            throw new QuantitySurveyRateLibraryConflictException($"The {label} changed after it was loaded. Refresh and try again.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        return options;
    }

    private sealed record ValidatedRateReferences(
        Core.Entities.Finance.Currency Currency,
        CentralDocumentVersion? Evidence);

    private sealed record MarketSurveyPreparation(
        Guid MarketAnalysisId,
        string MarketAnalysisCode,
        Guid? PreviousRateId,
        decimal? PreviousUnitRate,
        string? PreviousCurrencyCode,
        int QuoteCount,
        DateTime NextReviewDueAt);

    private sealed record HistoricalPreparation(
        QuantitySurveyHistoricalRateSourceType SourceType,
        Guid SourceId,
        Guid ProjectId,
        string ProjectCode,
        string SourceLabel,
        string UnitOfMeasure,
        decimal Quantity,
        decimal TotalAmount,
        string IntegrityHash,
        Guid? PreviousRateId,
        decimal? PreviousUnitRate,
        string? PreviousCurrencyCode);
}
