using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetDefectService : IFleetDefectService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkOrderService _workOrderService;
    private readonly ILogger<FleetDefectService> _logger;

    public FleetDefectService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkOrderService workOrderService,
        ILogger<FleetDefectService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workOrderService = workOrderService;
        _logger = logger;
    }

    public async Task<PagedResult<FleetDefectDto>> GetDefectsPagedAsync(int page, int pageSize, Guid? vehicleAssetId = null, string? status = null, string? searchTerm = null)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetDefect>();

        IQueryable<FleetDefect> q = repo.GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted)
            .Include(d => d.VehicleAsset)
            .Include(d => d.ReportedByEmployee);

        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            q = q.Where(d => d.VehicleAssetId == vehicleAssetId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(d => d.Status == status);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            q = q.Where(d => d.Title.Contains(term) || (d.Description != null && d.Description.Contains(term)));
        }

        var total = await q.CountAsync();

        var items = await q.OrderByDescending(d => d.ReportedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new FleetDefectDto
            {
                Id = d.Id,
                VehicleAssetId = d.VehicleAssetId,
                VehicleName = d.VehicleAsset.Name,
                FleetTripId = d.FleetTripId,
                FleetTripInspectionId = d.FleetTripInspectionId,
                Title = d.Title,
                Description = d.Description,
                Severity = d.Severity,
                Status = d.Status,
                ReportedAtUtc = d.ReportedAtUtc,
                ReportedByEmployeeId = d.ReportedByEmployeeId,
                ReportedByEmployeeName = d.ReportedByEmployee != null ? (d.ReportedByEmployee.FirstName + " " + d.ReportedByEmployee.LastName) : null,
                WorkOrderId = d.WorkOrderId
            })
            .ToListAsync();

        return new PagedResult<FleetDefectDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FleetDefectDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetDefect>();
        var d = await repo.GetQueryable(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted)
            .Include(x => x.VehicleAsset)
            .Include(x => x.ReportedByEmployee)
            .FirstOrDefaultAsync();

        if (d == null) return null;

        return new FleetDefectDto
        {
            Id = d.Id,
            VehicleAssetId = d.VehicleAssetId,
            VehicleName = d.VehicleAsset.Name,
            FleetTripId = d.FleetTripId,
            FleetTripInspectionId = d.FleetTripInspectionId,
            Title = d.Title,
            Description = d.Description,
            Severity = d.Severity,
            Status = d.Status,
            ReportedAtUtc = d.ReportedAtUtc,
            ReportedByEmployeeId = d.ReportedByEmployeeId,
            ReportedByEmployeeName = d.ReportedByEmployee != null ? (d.ReportedByEmployee.FirstName + " " + d.ReportedByEmployee.LastName) : null,
            WorkOrderId = d.WorkOrderId
        };
    }

    public async Task<FleetDefectDto> CreateAsync(CreateFleetDefectDto dto)
    {
        dto ??= new CreateFleetDefectDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ArgumentException("Title is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == dto.VehicleAssetId && !a.IsDeleted, a => a.AssetCategory);

        if (vehicle == null) throw new ArgumentException("Maintenance asset not found.");

        var entity = new FleetDefect
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            FleetTripId = dto.FleetTripId,
            FleetTripInspectionId = dto.FleetTripInspectionId,
            Title = dto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Severity = string.IsNullOrWhiteSpace(dto.Severity) ? "Medium" : dto.Severity.Trim(),
            Status = "Open",
            ReportedAtUtc = now,
            ReportedByEmployeeId = null,
            CreatedAt = now,
            CreatedById = userId
        };

        await _unitOfWork.Repository<FleetDefect>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetDefectDto> UpdateStatusAsync(Guid id, UpdateFleetDefectStatusDto dto)
    {
        if (id == Guid.Empty) throw new ArgumentException("DefectId is required.");
        dto ??= new UpdateFleetDefectStatusDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetDefect>();
        var entity = await repo.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id && !d.IsDeleted)
            ?? throw new ArgumentException("Defect not found.");

        entity.Status = dto.Status.Trim();
        entity.UpdatedAt = now;
        entity.LastModifiedById = userId;

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            entity.AdditionalData = string.IsNullOrWhiteSpace(entity.AdditionalData)
                ? $"{{\"Notes\":\"{dto.Notes.Trim()}\"}}"
                : entity.AdditionalData;
        }

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<Guid> CreateWorkOrderAsync(CreateWorkOrderFromFleetDefectDto dto)
    {
        dto ??= new CreateWorkOrderFromFleetDefectDto();
        if (dto.DefectId == Guid.Empty) throw new ArgumentException("DefectId is required.");

        var tenantId = _currentUserProvider.TenantId;

        var defectRepo = _unitOfWork.Repository<FleetDefect>();
        var defect = await defectRepo.GetQueryable(d => d.TenantId == tenantId && d.Id == dto.DefectId && !d.IsDeleted)
            .Include(d => d.VehicleAsset)
            .Include(d => d.FleetTripInspection)
                .ThenInclude(i => i!.InspectionTemplate)
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException("Defect not found.");

        if (defect.WorkOrderId.HasValue) return defect.WorkOrderId.Value;

        var billingType = string.IsNullOrWhiteSpace(dto.BillingType) ? "Repairs" : dto.BillingType.Trim();
        if (!string.Equals(billingType, "Repairs", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(billingType, "Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            billingType = "Repairs";
        }
        billingType = string.Equals(billingType, "Maintenance", StringComparison.OrdinalIgnoreCase) ? "Maintenance" : "Repairs";

        var workOrderType = await _unitOfWork.Repository<WorkOrderType>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == dto.WorkOrderTypeId && x.IsActive && !x.IsDeleted)
            ?? throw new ArgumentException("The selected work order type is not active or does not exist.");
        var maintenanceType = await _unitOfWork.Repository<MaintenanceType>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == dto.MaintenanceTypeId && x.IsActive && !x.IsDeleted)
            ?? throw new ArgumentException("The selected maintenance type is not active or does not exist.");
        var priority = await _unitOfWork.Repository<PriorityLevel>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == dto.PriorityLevelId && x.IsActive && !x.IsDeleted)
            ?? throw new ArgumentException("The selected priority is not active or does not exist.");
        var inspection = defect.FleetTripInspection;
        var findings = ExtractActionableFindings(inspection?.InspectionTemplate?.ChecklistItems, inspection?.InspectionData);
        var description = BuildWorkOrderDescription(defect, inspection, findings, dto.DescriptionOverride);
        var completionDays = Math.Max(1, maintenanceType?.LeadTimeDays ?? 1);

        var create = new CreateWorkOrderDto
        {
            Title = string.IsNullOrWhiteSpace(dto.TitleOverride) ? defect.Title : dto.TitleOverride.Trim(),
            Description = description,
            AssetId = defect.VehicleAssetId,
            WorkOrderTypeId = dto.WorkOrderTypeId,
            MaintenanceTypeId = dto.MaintenanceTypeId,
            PriorityLevelId = dto.PriorityLevelId,
            MaintenanceLocation = string.IsNullOrWhiteSpace(maintenanceType?.Location) ? "Internal" : maintenanceType.Location,
            BillingType = billingType,
            RequestedStartDate = DateTime.UtcNow,
            RequestedCompletionDate = DateTime.UtcNow.AddDays(completionDays),
            EstimatedHours = maintenanceType?.EstimatedHours ?? 0,
            EstimatedCost = maintenanceType?.EstimatedCost ?? 0,
            SafetyRequirements = maintenanceType?.SafetyRequirements,
            RequiresPermit = maintenanceType?.RequiresSafetyPermit ?? false,
            RequiresLockout = maintenanceType?.RequiresShutdown ?? false,
            FixedAmount = string.Equals(billingType, "Maintenance", StringComparison.OrdinalIgnoreCase)
                ? maintenanceType?.FixedAmount ?? 0
                : 0,
            CustomFieldValues = BuildSourceContext(defect, inspection, findings),
            GenerateDefaultTasks = findings.Count == 0
        };

        WorkOrderDto created;
        try
        {
            created = await _workOrderService.CreateWorkOrderAsync(create);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create work order from fleet defect {DefectId}", defect.Id);
            throw;
        }

        if (findings.Count > 0)
        {
            var taskRepository = _unitOfWork.Repository<WorkOrderTask>();
            var findingHours = Math.Max(0.5, create.EstimatedHours > 0 ? create.EstimatedHours / findings.Count : 0.5);
            for (var index = 0; index < findings.Count; index++)
            {
                var finding = findings[index];
                await taskRepository.AddAsync(new WorkOrderTask
                {
                    Id = Guid.NewGuid(),
                    WorkOrderId = created.Id,
                    TaskName = Truncate(finding.Item, 200),
                    Description = Truncate($"Resolve the checklist response '{finding.Response}'. Source inspection: {inspection?.Id}.", 1000),
                    Sequence = index + 1,
                    EstimatedHours = findingHours,
                    IsRequired = true,
                    Status = "Pending",
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _unitOfWork.SaveChangesAsync();
        }

        defect.WorkOrderId = created.Id;
        defect.Status = "InProgress";
        defect.UpdatedAt = DateTime.UtcNow;
        defect.LastModifiedById = _currentUserProvider.UserId;
        await defectRepo.UpdateAsync(defect);
        await _unitOfWork.SaveChangesAsync();

        return created.Id;
    }

    private static Dictionary<string, object> BuildSourceContext(
        FleetDefect defect,
        FleetTripInspection? inspection,
        IReadOnlyCollection<InspectionFinding> findings)
    {
        var context = new Dictionary<string, object>
        {
            ["workOrderSource"] = "FleetPreStartInspection",
            ["fleetDefectId"] = defect.Id,
            ["findingCount"] = findings.Count
        };

        if (defect.FleetTripId.HasValue) context["fleetTripId"] = defect.FleetTripId.Value;
        if (inspection == null) return context;

        context["fleetTripInspectionId"] = inspection.Id;
        context["inspectionTemplateId"] = inspection.InspectionTemplateId;
        context["inspectionKind"] = inspection.InspectionKind;
        context["inspectionResult"] = inspection.OverallResult ?? string.Empty;
        context["capturedOfflineAtUtc"] = inspection.CapturedOfflineAtUtc?.ToString("O") ?? string.Empty;
        return context;
    }

    private static string BuildWorkOrderDescription(
        FleetDefect defect,
        FleetTripInspection? inspection,
        IReadOnlyCollection<InspectionFinding> findings,
        string? overrideDescription)
    {
        if (!string.IsNullOrWhiteSpace(overrideDescription)) return Truncate(overrideDescription.Trim(), 2000);

        var parts = new List<string>
        {
            $"Automatically generated from fleet defect {defect.Id}."
        };

        if (inspection != null)
        {
            parts.Add($"Source pre-start inspection {inspection.Id} result: {inspection.OverallResult ?? "Unknown"}.");
        }

        if (!string.IsNullOrWhiteSpace(defect.Description)) parts.Add(defect.Description.Trim());
        if (findings.Count > 0)
        {
            parts.Add("Actionable findings: " + string.Join("; ", findings.Select(f => $"{f.Item} ({f.Response})")) + ".");
        }

        return Truncate(string.Join(" ", parts), 2000);
    }

    private static List<InspectionFinding> ExtractActionableFindings(string? checklistJson, string? inspectionDataJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(checklistJson) || string.IsNullOrWhiteSpace(inspectionDataJson)) return new();

            using var checklistDocument = JsonDocument.Parse(checklistJson);
            using var responseDocument = JsonDocument.Parse(inspectionDataJson);
            if (!responseDocument.RootElement.TryGetProperty("checklist", out var responses) || responses.ValueKind != JsonValueKind.Array)
                return new();

            var itemNames = new Dictionary<Guid, string>();
            foreach (var item in checklistDocument.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idProperty) || !Guid.TryParse(idProperty.GetString(), out var id)) continue;
                var name = item.TryGetProperty("item", out var itemProperty) ? itemProperty.GetString() : null;
                itemNames[id] = string.IsNullOrWhiteSpace(name) ? "Checklist item" : name.Trim();
            }

            var findings = new List<InspectionFinding>();
            foreach (var response in responses.EnumerateArray())
            {
                if (!response.TryGetProperty("id", out var idProperty) || !Guid.TryParse(idProperty.GetString(), out var id)) continue;
                var value = response.TryGetProperty("value", out var valueProperty) ? valueProperty.ToString() : string.Empty;
                if (!IsActionableResponse(value)) continue;
                findings.Add(new InspectionFinding(itemNames.GetValueOrDefault(id, "Checklist item"), value.Trim()));
            }

            return findings;
        }
        catch
        {
            return new();
        }
    }

    private static bool IsActionableResponse(string? value) =>
        string.Equals(value?.Trim(), "Fail", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "Failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "No", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "Flag", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "Flagged", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "Attention", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "ConditionalPass", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private sealed record InspectionFinding(string Item, string Response);
}
