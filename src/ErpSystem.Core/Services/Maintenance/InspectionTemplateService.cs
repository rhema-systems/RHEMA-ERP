using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

public class InspectionTemplateService : IInspectionTemplateService
{
    private readonly ILogger<InspectionTemplateService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private static readonly JsonSerializerOptions QrPayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public InspectionTemplateService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider, ILogger<InspectionTemplateService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region CRUD Operations

    public async Task<InspectionTemplateDto> CreateTemplateAsync(CreateInspectionTemplateDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating inspection template: {TemplateName}", createDto.Name);

            createDto ??= new CreateInspectionTemplateDto();
            if (string.IsNullOrWhiteSpace(createDto.Name)) throw new ArgumentException("Name is required.");
            if (string.IsNullOrWhiteSpace(createDto.Code)) throw new ArgumentException("Code is required.");

            var tenantId = _currentUserProvider.TenantId;
            var userId = _currentUserProvider.UserId;
            var now = DateTime.UtcNow;

            var repo = _unitOfWork.Repository<InspectionTemplate>();
            var exists = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == createDto.Code.Trim() && !t.IsDeleted);
            if (exists != null) throw new InvalidOperationException($"An inspection template with code '{createDto.Code}' already exists.");

            var entity = new InspectionTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = createDto.Name.Trim(),
                Code = createDto.Code.Trim(),
                Description = string.IsNullOrWhiteSpace(createDto.Description) ? null : createDto.Description.Trim(),
                Category = createDto.Category.Trim(),
                SheetType = NormalizeSheetType(createDto.SheetType),
                TemplateScope = NormalizeTemplateScope(createDto.TemplateScope),
                FleetInspectionKind = NormalizeFleetInspectionKind(createDto.FleetInspectionKind),
                AssignedAssetCategoryId = NormalizeGuid(createDto.AssignedAssetCategoryId),
                AssignedAssetId = NormalizeGuid(createDto.AssignedAssetId),
                IsQrEnabled = createDto.IsQrEnabled,
                MobileOfflineEnabled = createDto.MobileOfflineEnabled,
                QrPayloadVersion = createDto.QrPayloadVersion <= 0 ? 1 : createDto.QrPayloadVersion,
                AutoCreateWorkOrderOnFailure = createDto.AutoCreateWorkOrderOnFailure,
                FailureWorkOrderTypeId = NormalizeGuid(createDto.FailureWorkOrderTypeId),
                FailureMaintenanceTypeId = NormalizeGuid(createDto.FailureMaintenanceTypeId),
                FailurePriorityLevelId = NormalizeGuid(createDto.FailurePriorityLevelId),
                FailureBillingType = NormalizeBillingType(createDto.FailureBillingType),
                Frequency = createDto.Frequency.Trim(),
                EstimatedDuration = createDto.EstimatedDuration,
                RequiresSignature = createDto.RequiresSignature,
                AllowPhotos = createDto.AllowPhotos,
                Version = string.IsNullOrWhiteSpace(createDto.Version) ? "1.0" : createDto.Version.Trim(),
                Priority = string.IsNullOrWhiteSpace(createDto.Priority) ? "Medium" : createDto.Priority.Trim(),
                AssetTypes = JsonSerializer.Serialize(createDto.AssetTypes ?? new List<string>()),
                InspectorRoles = JsonSerializer.Serialize(createDto.InspectorRoles ?? new List<string>()),
                ChecklistItems = JsonSerializer.Serialize((createDto.ChecklistItems ?? new List<CreateInspectionChecklistItemDto>())
                    .Select((i, idx) => new
                    {
                        id = Guid.NewGuid(),
                        item = i.Item,
                        type = i.Type,
                        required = i.Required,
                        order = i.Order == 0 ? (idx + 1) : i.Order
                    })),
                InspectionType = string.IsNullOrWhiteSpace(createDto.Category) ? "General" : createDto.Category.Trim(),
                IsActive = createDto.IsActive,
                CreatedAt = now,
                CreatedById = userId
            };

            await repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            return Map(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inspection template: {TemplateName}", createDto.Name);
            throw;
        }
    }

    public async Task<InspectionTemplateDto> UpdateTemplateAsync(Guid id, UpdateInspectionTemplateDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating inspection template: {TemplateId}", id);

            if (id == Guid.Empty) throw new ArgumentException("Id is required.");
            updateDto ??= new UpdateInspectionTemplateDto();

            var tenantId = _currentUserProvider.TenantId;
            var userId = _currentUserProvider.UserId;
            var now = DateTime.UtcNow;

            var repo = _unitOfWork.Repository<InspectionTemplate>();
            var entity = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted)
                ?? throw new ArgumentException("Template not found.");

            if (!string.Equals(entity.Code, updateDto.Code.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var exists = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == updateDto.Code.Trim() && !t.IsDeleted);
                if (exists != null) throw new InvalidOperationException($"An inspection template with code '{updateDto.Code}' already exists.");
                entity.Code = updateDto.Code.Trim();
            }

            entity.Name = updateDto.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description.Trim();
            entity.Category = updateDto.Category.Trim();
            entity.SheetType = NormalizeSheetType(updateDto.SheetType);
            entity.TemplateScope = NormalizeTemplateScope(updateDto.TemplateScope);
            entity.FleetInspectionKind = NormalizeFleetInspectionKind(updateDto.FleetInspectionKind);
            entity.AssignedAssetCategoryId = NormalizeGuid(updateDto.AssignedAssetCategoryId);
            entity.AssignedAssetId = NormalizeGuid(updateDto.AssignedAssetId);
            entity.IsQrEnabled = updateDto.IsQrEnabled;
            entity.MobileOfflineEnabled = updateDto.MobileOfflineEnabled;
            entity.QrPayloadVersion = updateDto.QrPayloadVersion <= 0 ? 1 : updateDto.QrPayloadVersion;
            entity.AutoCreateWorkOrderOnFailure = updateDto.AutoCreateWorkOrderOnFailure;
            entity.FailureWorkOrderTypeId = NormalizeGuid(updateDto.FailureWorkOrderTypeId);
            entity.FailureMaintenanceTypeId = NormalizeGuid(updateDto.FailureMaintenanceTypeId);
            entity.FailurePriorityLevelId = NormalizeGuid(updateDto.FailurePriorityLevelId);
            entity.FailureBillingType = NormalizeBillingType(updateDto.FailureBillingType);
            entity.Frequency = updateDto.Frequency.Trim();
            entity.EstimatedDuration = updateDto.EstimatedDuration;
            entity.RequiresSignature = updateDto.RequiresSignature;
            entity.AllowPhotos = updateDto.AllowPhotos;
            entity.Version = string.IsNullOrWhiteSpace(updateDto.Version) ? entity.Version : updateDto.Version.Trim();
            entity.Priority = string.IsNullOrWhiteSpace(updateDto.Priority) ? entity.Priority : updateDto.Priority.Trim();
            entity.AssetTypes = JsonSerializer.Serialize(updateDto.AssetTypes ?? new List<string>());
            entity.InspectorRoles = JsonSerializer.Serialize(updateDto.InspectorRoles ?? new List<string>());
            entity.ChecklistItems = JsonSerializer.Serialize((updateDto.ChecklistItems ?? new List<CreateInspectionChecklistItemDto>())
                .Select((i, idx) => new
                {
                    id = Guid.NewGuid(),
                    item = i.Item,
                    type = i.Type,
                    required = i.Required,
                    order = i.Order == 0 ? (idx + 1) : i.Order
                }));
            entity.InspectionType = string.IsNullOrWhiteSpace(updateDto.Category) ? entity.InspectionType : updateDto.Category.Trim();
            entity.IsActive = updateDto.IsActive;
            entity.UpdatedAt = now;
            entity.LastModifiedById = userId;

            await repo.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            return Map(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inspection template: {TemplateId}", id);
            throw;
        }
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting inspection template: {TemplateId}", id);

            if (id == Guid.Empty) return;

            var tenantId = _currentUserProvider.TenantId;
            var userId = _currentUserProvider.UserId;
            var now = DateTime.UtcNow;

            var repo = _unitOfWork.Repository<InspectionTemplate>();
            var entity = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted);
            if (entity == null) return;

            entity.IsDeleted = true;
            entity.DeletedAt = now;
            entity.DeletedBy = userId.ToString();
            entity.UpdatedAt = now;
            entity.LastModifiedById = userId;

            await repo.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inspection template: {TemplateId}", id);
            throw;
        }
    }

    public async Task<InspectionTemplateDto?> GetTemplateByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<InspectionTemplate>();

        var entity = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted);
        return entity == null ? null : Map(entity);
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetAllTemplatesAsync()
    {
        return await GetTemplatesAsync(new InspectionTemplateFilterDto());
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetTemplatesByCategoryAsync(string category)
    {
        return await GetTemplatesAsync(new InspectionTemplateFilterDto { Category = category });
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetActiveTemplatesAsync()
    {
        return await GetTemplatesAsync(new InspectionTemplateFilterDto { IsActive = true });
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetTemplatesAsync(InspectionTemplateFilterDto filter)
    {
        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<InspectionTemplate>();
        filter ??= new InspectionTemplateFilterDto();

        var query = repo.GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(t =>
                t.Name.Contains(term) ||
                t.Code.Contains(term) ||
                (t.Description != null && t.Description.Contains(term)) ||
                (t.Category != null && t.Category.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var category = filter.Category.Trim();
            query = query.Where(t => t.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(filter.SheetType))
        {
            var sheetType = NormalizeSheetType(filter.SheetType);
            query = query.Where(t => t.SheetType == sheetType);
        }

        if (!string.IsNullOrWhiteSpace(filter.Frequency))
        {
            var frequency = filter.Frequency.Trim();
            query = query.Where(t => t.Frequency == frequency);
        }

        if (!string.IsNullOrWhiteSpace(filter.TemplateScope))
        {
            var scope = NormalizeTemplateScope(filter.TemplateScope);
            query = query.Where(t => t.TemplateScope == scope);
        }

        if (!string.IsNullOrWhiteSpace(filter.FleetInspectionKind))
        {
            var kind = NormalizeFleetInspectionKind(filter.FleetInspectionKind);
            query = query.Where(t => t.FleetInspectionKind == "Any" || t.FleetInspectionKind == kind);
        }

        if (filter.AssignedAssetCategoryId.HasValue && filter.AssignedAssetCategoryId.Value != Guid.Empty)
        {
            var categoryId = filter.AssignedAssetCategoryId.Value;
            query = query.Where(t => t.AssignedAssetCategoryId == null || t.AssignedAssetCategoryId == categoryId);
        }

        if (filter.AssignedAssetId.HasValue && filter.AssignedAssetId.Value != Guid.Empty)
        {
            var assetId = filter.AssignedAssetId.Value;
            query = query.Where(t => t.AssignedAssetId == null || t.AssignedAssetId == assetId);
        }

        if (filter.IsQrEnabled.HasValue)
        {
            query = query.Where(t => t.IsQrEnabled == filter.IsQrEnabled.Value);
        }

        if (filter.MobileOfflineEnabled.HasValue)
        {
            query = query.Where(t => t.MobileOfflineEnabled == filter.MobileOfflineEnabled.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == filter.IsActive.Value);
        }

        var hasAssetOrdering = filter.AssignedAssetId.HasValue && filter.AssignedAssetId.Value != Guid.Empty;
        var hasCategoryOrdering = filter.AssignedAssetCategoryId.HasValue && filter.AssignedAssetCategoryId.Value != Guid.Empty;
        var assetIdForOrdering = filter.AssignedAssetId.GetValueOrDefault();
        var categoryIdForOrdering = filter.AssignedAssetCategoryId.GetValueOrDefault();

        var entities = await query
            .OrderByDescending(t => hasAssetOrdering && t.AssignedAssetId == assetIdForOrdering)
            .ThenByDescending(t => hasCategoryOrdering && t.AssignedAssetCategoryId == categoryIdForOrdering)
            .ThenBy(t => t.FleetInspectionKind == "Any")
            .ThenBy(t => t.Name)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();

        return entities.Select(Map).ToList();
    }

    public async Task<InspectionTemplateQrPackageDto> GetQrPackageAsync(Guid id, InspectionTemplateQrPackageRequestDto request)
    {
        if (id == Guid.Empty) throw new ArgumentException("Template id is required.");
        request ??= new InspectionTemplateQrPackageRequestDto();

        var tenantId = _currentUserProvider.TenantId;
        var templateRepo = _unitOfWork.Repository<InspectionTemplate>();
        var template = await templateRepo.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted)
            ?? throw new ArgumentException("Template not found.");

        if (!template.IsQrEnabled)
            throw new InvalidOperationException("QR access is not enabled for this inspection or service sheet.");

        var requestedKind = ResolveInspectionKind(request.InspectionKind, template);
        var templateKind = NormalizeFleetInspectionKind(template.FleetInspectionKind);
        if (NormalizeTemplateScope(template.TemplateScope) == "Fleet" &&
            templateKind != "Any" && templateKind != requestedKind)
        {
            throw new InvalidOperationException($"Template '{template.Name}' is configured for {templateKind} inspections.");
        }

        MaintenanceAsset? asset = null;
        MaintenanceAssetCategory? assetCategory = null;

        if (request.AssetId.HasValue && request.AssetId.Value != Guid.Empty)
        {
            var assetId = request.AssetId.Value;
            asset = await _unitOfWork.Repository<MaintenanceAsset>()
                .GetQueryable(a => a.TenantId == tenantId && a.Id == assetId && !a.IsDeleted)
                .Include(a => a.AssetCategory)
                .FirstOrDefaultAsync();

            if (asset == null) throw new ArgumentException("Asset not found.");
            assetCategory = asset.AssetCategory;
        }

        var effectiveAssetCategoryId = request.AssetCategoryId.GetValueOrDefault();
        if (effectiveAssetCategoryId == Guid.Empty && asset != null)
        {
            effectiveAssetCategoryId = asset.AssetCategoryId;
        }

        if (effectiveAssetCategoryId != Guid.Empty && assetCategory == null)
        {
            assetCategory = await _unitOfWork.Repository<MaintenanceAssetCategory>()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == effectiveAssetCategoryId && !c.IsDeleted);
        }

        if (template.AssignedAssetId.HasValue && template.AssignedAssetId.Value != request.AssetId.GetValueOrDefault())
        {
            throw new InvalidOperationException("This template is assigned to a different asset.");
        }

        if (template.AssignedAssetCategoryId.HasValue && template.AssignedAssetCategoryId.Value != effectiveAssetCategoryId)
        {
            throw new InvalidOperationException("This template is assigned to a different asset category.");
        }

        var dto = Map(template);
        var generatedAtUtc = DateTime.UtcNow;
        var maxPayloadBytes = request.MaxQrPayloadBytes <= 0 ? 2500 : request.MaxQrPayloadBytes;

        var payloadShell = new
        {
            schema = "FleetInspectionQrPackage.v1",
            tenantId,
            templateId = template.Id,
            templateCode = template.Code,
            templateName = template.Name,
            templateVersion = template.Version,
            sheetType = NormalizeSheetType(template.SheetType),
            qrPayloadVersion = template.QrPayloadVersion <= 0 ? 1 : template.QrPayloadVersion,
            inspectionKind = requestedKind,
            assetId = asset?.Id,
            assetName = asset?.Name,
            assetNumber = asset?.AssetNumber,
            assetCategoryId = effectiveAssetCategoryId == Guid.Empty ? (Guid?)null : effectiveAssetCategoryId,
            assetCategoryName = assetCategory?.Name,
            fleetTripId = request.FleetTripId,
            requiresSignature = template.RequiresSignature,
            allowPhotos = template.AllowPhotos,
            checklistItems = dto.ChecklistItems.Select(i => new
            {
                id = i.Id,
                item = i.Item,
                type = i.Type,
                required = i.Required,
                order = i.Order
            }).ToList(),
            generatedAtUtc
        };

        var compactPayloadWithoutSignature = JsonSerializer.Serialize(payloadShell, QrPayloadJsonOptions);
        var payloadHash = ComputeSha256(compactPayloadWithoutSignature);
        var packageId = payloadHash[..Math.Min(payloadHash.Length, 24)];
        var signature = SignPayload(compactPayloadWithoutSignature, tenantId);

        var mobileUrl = BuildMobileUrl(
            request.FrontendBaseUrl,
            template.Id,
            template.Code,
            packageId,
            requestedKind,
            asset?.Id,
            asset?.Name,
            asset?.AssetNumber,
            effectiveAssetCategoryId,
            request.FleetTripId,
            signature);
        var signedPayload = new
        {
            schema = "FleetInspectionQrPackage.v1",
            packageId,
            payloadHash,
            signature,
            url = mobileUrl,
            payload = payloadShell
        };

        var compactPayloadJson = JsonSerializer.Serialize(signedPayload, QrPayloadJsonOptions);
        var payloadBytes = Encoding.UTF8.GetByteCount(compactPayloadJson);
        var base64Url = Base64UrlEncode(Encoding.UTF8.GetBytes(compactPayloadJson));

        return new InspectionTemplateQrPackageDto
        {
            PackageId = packageId,
            TemplateId = template.Id,
            TemplateName = template.Name,
            TemplateCode = template.Code,
            TemplateVersion = template.Version,
            SheetType = NormalizeSheetType(template.SheetType),
            TemplateScope = NormalizeTemplateScope(template.TemplateScope),
            FleetInspectionKind = templateKind,
            InspectionKind = requestedKind,
            AssetId = asset?.Id,
            AssetName = asset?.Name,
            AssetNumber = asset?.AssetNumber,
            AssetCategoryId = effectiveAssetCategoryId == Guid.Empty ? null : effectiveAssetCategoryId,
            AssetCategoryName = assetCategory?.Name,
            FleetTripId = request.FleetTripId,
            IsQrEnabled = template.IsQrEnabled,
            MobileOfflineEnabled = template.MobileOfflineEnabled,
            AllowPhotos = template.AllowPhotos,
            QrPayloadVersion = template.QrPayloadVersion <= 0 ? 1 : template.QrPayloadVersion,
            GeneratedAtUtc = generatedAtUtc,
            MobileUrl = mobileUrl,
            PayloadHash = payloadHash,
            Signature = signature,
            CompactPayloadJson = compactPayloadJson,
            CompactPayloadBase64Url = base64Url,
            QrValue = mobileUrl,
            QrPayloadMode = "Reference",
            CanEmbedFullPayload = false,
            PayloadSizeBytes = payloadBytes,
            MaxQrPayloadBytes = maxPayloadBytes,
            ChecklistItems = dto.ChecklistItems
        };
    }

    #endregion

    private static InspectionTemplateDto Map(InspectionTemplate t)
    {
        var assetTypes = SafeJson<List<string>>(t.AssetTypes) ?? new List<string>();
        var inspectorRoles = SafeJson<List<string>>(t.InspectorRoles) ?? new List<string>();

        var checklist = SafeJson<List<JsonElement>>(t.ChecklistItems) ?? new List<JsonElement>();
        var checklistItems = new List<InspectionChecklistItemDto>();

        foreach (var item in checklist)
        {
            try
            {
                var id = item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String && Guid.TryParse(idEl.GetString(), out var gid)
                    ? gid
                    : Guid.NewGuid();
                var text = item.TryGetProperty("item", out var itemEl) ? itemEl.GetString() ?? string.Empty : string.Empty;
                var type = item.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "checklist" : "checklist";
                var required = item.TryGetProperty("required", out var reqEl) && reqEl.ValueKind == JsonValueKind.True;
                var order = item.TryGetProperty("order", out var orderEl) && orderEl.TryGetInt32(out var ord) ? ord : 0;

                checklistItems.Add(new InspectionChecklistItemDto
                {
                    Id = id,
                    Item = text,
                    Type = type,
                    Required = required,
                    Order = order
                });
            }
            catch
            {
                // ignore bad item
            }
        }

        checklistItems = checklistItems.OrderBy(i => i.Order).ToList();

        return new InspectionTemplateDto
        {
            Id = t.Id,
            Name = t.Name,
            Code = t.Code,
            Description = t.Description ?? string.Empty,
            Category = t.Category ?? string.Empty,
            SheetType = NormalizeSheetType(t.SheetType),
            TemplateScope = string.IsNullOrWhiteSpace(t.TemplateScope) ? "General" : t.TemplateScope,
            FleetInspectionKind = string.IsNullOrWhiteSpace(t.FleetInspectionKind) ? "Any" : t.FleetInspectionKind,
            AssignedAssetCategoryId = t.AssignedAssetCategoryId,
            AssignedAssetId = t.AssignedAssetId,
            IsQrEnabled = t.IsQrEnabled,
            MobileOfflineEnabled = t.MobileOfflineEnabled,
            QrPayloadVersion = t.QrPayloadVersion <= 0 ? 1 : t.QrPayloadVersion,
            AutoCreateWorkOrderOnFailure = t.AutoCreateWorkOrderOnFailure,
            FailureWorkOrderTypeId = t.FailureWorkOrderTypeId,
            FailureMaintenanceTypeId = t.FailureMaintenanceTypeId,
            FailurePriorityLevelId = t.FailurePriorityLevelId,
            FailureBillingType = NormalizeBillingType(t.FailureBillingType),
            Frequency = t.Frequency,
            EstimatedDuration = t.EstimatedDuration,
            IsActive = t.IsActive,
            RequiresSignature = t.RequiresSignature,
            AllowPhotos = t.AllowPhotos,
            Version = t.Version,
            Priority = t.Priority,
            AssetTypes = assetTypes,
            InspectorRoles = inspectorRoles,
            ChecklistItems = checklistItems,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
    }

    private static T? SafeJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    private static string NormalizeTemplateScope(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "General";
        return value.Trim().Equals("Fleet", StringComparison.OrdinalIgnoreCase) ? "Fleet" : "General";
    }

    private static string NormalizeSheetType(string? value)
    {
        var key = (value ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        if (key.Equals("ServiceSheet", StringComparison.OrdinalIgnoreCase)) return "ServiceSheet";
        if (key.Equals("WeeklyChecklist", StringComparison.OrdinalIgnoreCase)) return "WeeklyChecklist";
        if (key.Equals("PreventiveMaintenanceForm", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("PreventiveMaintenance", StringComparison.OrdinalIgnoreCase)) return "PreventiveMaintenanceForm";
        return "InspectionSheet";
    }

    private static string ResolveInspectionKind(string? requestedKind, InspectionTemplate template)
    {
        if (NormalizeTemplateScope(template.TemplateScope) == "Fleet")
            return NormalizeFleetInspectionKind(requestedKind);

        if (!string.IsNullOrWhiteSpace(requestedKind)) return requestedKind.Trim();
        return NormalizeSheetType(template.SheetType) switch
        {
            "ServiceSheet" => "Service",
            "WeeklyChecklist" => "Weekly",
            "PreventiveMaintenanceForm" => "PreventiveMaintenance",
            _ => "Inspection"
        };
    }

    private static string NormalizeFleetInspectionKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Any";

        var trimmed = value.Trim();
        if (trimmed.Equals("PreTrip", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Pre-trip", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Pre Trip", StringComparison.OrdinalIgnoreCase))
        {
            return "PreTrip";
        }

        if (trimmed.Equals("PostTrip", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Post-trip", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Post Trip", StringComparison.OrdinalIgnoreCase))
        {
            return "PostTrip";
        }

        return "Any";
    }

    private static Guid? NormalizeGuid(Guid? value)
    {
        return value.HasValue && value.Value != Guid.Empty ? value : null;
    }

    private static string NormalizeBillingType(string? value)
    {
        if (string.Equals(value?.Trim(), "Default", StringComparison.OrdinalIgnoreCase)) return "Default";
        return string.Equals(value?.Trim(), "Maintenance", StringComparison.OrdinalIgnoreCase) ? "Maintenance" : "Repairs";
    }

    private static string BuildMobileUrl(
        string? frontendBaseUrl,
        Guid templateId,
        string templateCode,
        string packageId,
        string inspectionKind,
        Guid? assetId,
        string? assetName,
        string? assetNumber,
        Guid assetCategoryId,
        Guid? fleetTripId,
        string signature)
    {
        var baseUrl = string.IsNullOrWhiteSpace(frontendBaseUrl) ? "http://localhost:3000" : frontendBaseUrl.Trim().TrimEnd('/');
        var query = new List<string>
        {
            $"templateId={Uri.EscapeDataString(templateId.ToString())}",
            $"templateCode={Uri.EscapeDataString(templateCode)}",
            $"packageId={Uri.EscapeDataString(packageId)}",
            $"inspectionKind={Uri.EscapeDataString(inspectionKind)}",
            $"sig={Uri.EscapeDataString(signature)}"
        };

        if (assetId.HasValue && assetId.Value != Guid.Empty) query.Add($"assetId={Uri.EscapeDataString(assetId.Value.ToString())}");
        if (!string.IsNullOrWhiteSpace(assetName)) query.Add($"assetName={Uri.EscapeDataString(assetName)}");
        if (!string.IsNullOrWhiteSpace(assetNumber)) query.Add($"assetNumber={Uri.EscapeDataString(assetNumber)}");
        if (assetCategoryId != Guid.Empty) query.Add($"assetCategoryId={Uri.EscapeDataString(assetCategoryId.ToString())}");
        if (fleetTripId.HasValue && fleetTripId.Value != Guid.Empty) query.Add($"fleetTripId={Uri.EscapeDataString(fleetTripId.Value.ToString())}");

        return $"{baseUrl}/mobile/fleet/inspection?{string.Join("&", query)}";
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string SignPayload(string value, Guid tenantId)
    {
        var key = Environment.GetEnvironmentVariable("ERP_MOBILE_QR_SIGNING_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            key = $"erp-fleet-inspection:{tenantId:N}";
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
