using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetInspectionService : IFleetInspectionService
{
    private const string WorkflowEntityType = "FleetTripInspection";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFleetDefectService _fleetDefectService;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ILogger<FleetInspectionService> _logger;

    public FleetInspectionService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IFleetDefectService fleetDefectService,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ILogger<FleetInspectionService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _fleetDefectService = fleetDefectService;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FleetTripInspectionDto>> GetTripInspectionsAsync(Guid tripId)
    {
        if (tripId == Guid.Empty) return Array.Empty<FleetTripInspectionDto>();

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetTripInspection>();

        var list = await repo.GetQueryable(i => i.TenantId == tenantId && i.FleetTripId == tripId && !i.IsDeleted)
            .Include(i => i.InspectionTemplate)
            .Include(i => i.InspectorEmployee)
            .Include(i => i.VehicleAsset)
            .OrderByDescending(i => i.StartedAtUtc)
            .Select(i => new FleetTripInspectionDto
            {
                Id = i.Id,
                FleetTripId = i.FleetTripId,
                VehicleAssetId = i.VehicleAssetId,
                VehicleAssetName = i.VehicleAsset.Name,
                VehicleAssetNumber = i.VehicleAsset.AssetNumber,
                InspectionTemplateId = i.InspectionTemplateId,
                InspectionTemplateName = i.InspectionTemplate.Name,
                SheetType = i.InspectionTemplate.SheetType,
                InspectorEmployeeId = i.InspectorEmployeeId,
                InspectorEmployeeName = i.InspectorEmployee != null ? (i.InspectorEmployee.FirstName + " " + i.InspectorEmployee.LastName) : null,
                InspectionKind = i.InspectionKind,
                StartedAtUtc = i.StartedAtUtc,
                CompletedAtUtc = i.CompletedAtUtc,
                Status = i.Status,
                OverallResult = i.OverallResult,
                InspectionData = i.InspectionData,
                Notes = i.Notes,
                ClientSubmissionId = i.ClientSubmissionId,
                CapturedOfflineAtUtc = i.CapturedOfflineAtUtc,
                SyncedAtUtc = i.SyncedAtUtc
            })
            .ToListAsync();

        await EnrichDefectLinksAsync(list);
        return list;
    }

    public async Task<IReadOnlyList<FleetTripInspectionDto>> GetAssetInspectionsAsync(Guid assetId, int take = 50)
    {
        if (assetId == Guid.Empty) return Array.Empty<FleetTripInspectionDto>();
        if (take <= 0) take = 50;
        if (take > 200) take = 200;

        var tenantId = _currentUserProvider.TenantId;
        var list = await _unitOfWork.Repository<FleetTripInspection>()
            .GetQueryable(i => i.TenantId == tenantId && i.VehicleAssetId == assetId && !i.IsDeleted)
            .Include(i => i.InspectionTemplate)
            .Include(i => i.InspectorEmployee)
            .Include(i => i.VehicleAsset)
            .OrderByDescending(i => i.StartedAtUtc)
            .Take(take)
            .Select(i => new FleetTripInspectionDto
            {
                Id = i.Id,
                FleetTripId = i.FleetTripId,
                VehicleAssetId = i.VehicleAssetId,
                VehicleAssetName = i.VehicleAsset.Name,
                VehicleAssetNumber = i.VehicleAsset.AssetNumber,
                InspectionTemplateId = i.InspectionTemplateId,
                InspectionTemplateName = i.InspectionTemplate.Name,
                SheetType = i.InspectionTemplate.SheetType,
                InspectorEmployeeId = i.InspectorEmployeeId,
                InspectorEmployeeName = i.InspectorEmployee != null ? (i.InspectorEmployee.FirstName + " " + i.InspectorEmployee.LastName) : null,
                InspectionKind = i.InspectionKind,
                StartedAtUtc = i.StartedAtUtc,
                CompletedAtUtc = i.CompletedAtUtc,
                Status = i.Status,
                OverallResult = i.OverallResult,
                InspectionData = i.InspectionData,
                Notes = i.Notes,
                ClientSubmissionId = i.ClientSubmissionId,
                CapturedOfflineAtUtc = i.CapturedOfflineAtUtc,
                SyncedAtUtc = i.SyncedAtUtc
            })
            .ToListAsync();

        await EnrichDefectLinksAsync(list);
        return list;
    }

    public async Task<FleetTripInspectionDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetTripInspection>();
        var i = await repo.GetQueryable(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted)
            .Include(x => x.InspectionTemplate)
            .Include(x => x.InspectorEmployee)
            .Include(x => x.VehicleAsset)
            .FirstOrDefaultAsync();

        if (i == null) return null;

        var dto = new FleetTripInspectionDto
        {
            Id = i.Id,
            FleetTripId = i.FleetTripId,
            VehicleAssetId = i.VehicleAssetId,
            VehicleAssetName = i.VehicleAsset.Name,
            VehicleAssetNumber = i.VehicleAsset.AssetNumber,
            InspectionTemplateId = i.InspectionTemplateId,
            InspectionTemplateName = i.InspectionTemplate.Name,
            SheetType = i.InspectionTemplate.SheetType,
            InspectorEmployeeId = i.InspectorEmployeeId,
            InspectorEmployeeName = i.InspectorEmployee != null ? (i.InspectorEmployee.FirstName + " " + i.InspectorEmployee.LastName) : null,
            InspectionKind = i.InspectionKind,
            StartedAtUtc = i.StartedAtUtc,
            CompletedAtUtc = i.CompletedAtUtc,
            Status = i.Status,
            OverallResult = i.OverallResult,
            InspectionData = i.InspectionData,
            Notes = i.Notes,
            ClientSubmissionId = i.ClientSubmissionId,
            CapturedOfflineAtUtc = i.CapturedOfflineAtUtc,
            SyncedAtUtc = i.SyncedAtUtc
        };

        await EnrichDefectLinksAsync(new List<FleetTripInspectionDto> { dto });
        return dto;
    }

    public async Task<FleetTripInspectionDto> StartAsync(StartFleetTripInspectionDto dto)
    {
        dto ??= new StartFleetTripInspectionDto();
        if (dto.FleetTripId == Guid.Empty) throw new ArgumentException("FleetTripId is required.");
        if (dto.InspectionTemplateId == Guid.Empty) throw new ArgumentException("InspectionTemplateId is required.");
        if (string.IsNullOrWhiteSpace(dto.InspectionKind)) dto.InspectionKind = "PreTrip";

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var trip = await _unitOfWork.Repository<FleetTrip>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == dto.FleetTripId && !t.IsDeleted);
        if (trip == null) throw new ArgumentException("Trip not found.");

        var template = await _unitOfWork.Repository<InspectionTemplate>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == dto.InspectionTemplateId && !t.IsDeleted);
        if (template == null) throw new ArgumentException("Inspection template not found.");

        var repo = _unitOfWork.Repository<FleetTripInspection>();

        var existing = await repo.FirstOrDefaultAsync(i =>
            i.TenantId == tenantId &&
            i.FleetTripId == dto.FleetTripId &&
            i.InspectionKind == dto.InspectionKind &&
            i.Status != "Cancelled" &&
            !i.IsDeleted);

        if (existing != null)
        {
            return (await GetByIdAsync(existing.Id))!;
        }

        var inspectorEmployeeId = dto.InspectorEmployeeId ?? trip.DriverEmployeeId;

        var entity = new FleetTripInspection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FleetTripId = dto.FleetTripId,
            VehicleAssetId = trip.VehicleAssetId,
            InspectionTemplateId = dto.InspectionTemplateId,
            InspectorEmployeeId = inspectorEmployeeId,
            InspectionKind = dto.InspectionKind.Trim(),
            StartedAtUtc = now,
            Status = "InProgress",
            InspectionData = "{}",
            CreatedAt = now,
            CreatedById = userId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetTripInspectionDto> CompleteAsync(Guid inspectionId, CompleteFleetTripInspectionDto dto)
    {
        if (inspectionId == Guid.Empty) throw new ArgumentException("InspectionId is required.");
        dto ??= new CompleteFleetTripInspectionDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetTripInspection>();
        var entity = await repo.FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == inspectionId && !i.IsDeleted)
            ?? throw new ArgumentException("Inspection not found.");

        var template = await _unitOfWork.Repository<InspectionTemplate>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == entity.InspectionTemplateId && !t.IsDeleted);
        var overallResult = ValidateAndNormalizeResult(template, dto.InspectionData, dto.OverallResult);

        entity.CompletedAtUtc = (dto.CompletedAtUtc ?? now).ToUniversalTime();
        entity.Status = "Completed";
        entity.OverallResult = overallResult;
        entity.InspectionData = string.IsNullOrWhiteSpace(dto.InspectionData) ? "{}" : dto.InspectionData;
        entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? entity.Notes : dto.Notes.Trim();
        entity.UpdatedAt = now;
        entity.LastModifiedById = userId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await EnsureDefectAndWorkOrderAsync(entity, template);
        await TryStartWorkflowIfConfiguredAsync(entity);
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetTripInspectionDto> SubmitAssetInspectionAsync(Guid assetId, SubmitFleetAssetInspectionDto dto)
    {
        if (assetId == Guid.Empty) throw new ArgumentException("AssetId is required.");
        dto ??= new SubmitFleetAssetInspectionDto();
        if (dto.InspectionTemplateId == Guid.Empty) throw new ArgumentException("InspectionTemplateId is required.");
        if (string.IsNullOrWhiteSpace(dto.ClientSubmissionId)) throw new ArgumentException("ClientSubmissionId is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;
        var submissionId = dto.ClientSubmissionId.Trim();
        var repo = _unitOfWork.Repository<FleetTripInspection>();

        var existing = await repo.FirstOrDefaultAsync(i =>
            i.TenantId == tenantId && i.ClientSubmissionId == submissionId && !i.IsDeleted);
        if (existing != null) return (await GetByIdAsync(existing.Id))!;

        var asset = await _unitOfWork.Repository<MaintenanceAsset>()
            .GetQueryable(a => a.TenantId == tenantId && a.Id == assetId && !a.IsDeleted)
            .Include(a => a.AssetCategory)
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException("Asset not found.");

        var template = await _unitOfWork.Repository<InspectionTemplate>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == dto.InspectionTemplateId && t.IsActive && !t.IsDeleted)
            ?? throw new ArgumentException("Inspection template not found.");

        if (!template.IsQrEnabled)
            throw new InvalidOperationException("QR/mobile access is not enabled for this inspection or service sheet.");

        var inspectionKind = ResolveInspectionKind(dto.InspectionKind, template);
        if (string.Equals(template.TemplateScope, "Fleet", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(template.FleetInspectionKind, "Any", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(template.FleetInspectionKind, inspectionKind, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The selected template is configured for {template.FleetInspectionKind} inspections.");
        if (template.AssignedAssetId.HasValue && template.AssignedAssetId.Value != assetId)
            throw new InvalidOperationException("This inspection template is assigned to a different asset.");
        if (template.AssignedAssetCategoryId.HasValue && template.AssignedAssetCategoryId.Value != asset.AssetCategoryId)
            throw new InvalidOperationException("This inspection template is assigned to a different asset category.");

        var overallResult = ValidateAndNormalizeResult(template, dto.InspectionData, dto.OverallResult);
        var startedAt = (dto.StartedAtUtc ?? dto.CapturedOfflineAtUtc ?? now).ToUniversalTime();
        var completedAt = (dto.CompletedAtUtc ?? now).ToUniversalTime();
        var linkedTrip = await ResolvePendingTripForMobileSubmissionAsync(assetId, inspectionKind, completedAt);
        var inspectorEmployeeId = dto.InspectorEmployeeId ?? linkedTrip?.DriverEmployeeId;

        if (linkedTrip != null)
        {
            var existingTripInspection = await repo.GetQueryable(i =>
                    i.TenantId == tenantId &&
                    i.FleetTripId == linkedTrip.Id &&
                    i.InspectionKind == inspectionKind &&
                    i.Status != "Cancelled" &&
                    !i.IsDeleted)
                .OrderByDescending(i => i.StartedAtUtc)
                .FirstOrDefaultAsync();

            if (existingTripInspection != null && IsMobileTripInspectionUpdateAllowed(existingTripInspection))
            {
                existingTripInspection.VehicleAssetId = assetId;
                existingTripInspection.InspectionTemplateId = template.Id;
                existingTripInspection.InspectorEmployeeId = inspectorEmployeeId ?? existingTripInspection.InspectorEmployeeId;
                existingTripInspection.StartedAtUtc = startedAt;
                existingTripInspection.CompletedAtUtc = completedAt;
                existingTripInspection.Status = "Completed";
                existingTripInspection.OverallResult = overallResult;
                existingTripInspection.InspectionData = string.IsNullOrWhiteSpace(dto.InspectionData) ? "{}" : dto.InspectionData;
                existingTripInspection.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? existingTripInspection.Notes : dto.Notes.Trim();
                existingTripInspection.ClientSubmissionId = submissionId;
                existingTripInspection.CapturedOfflineAtUtc = dto.CapturedOfflineAtUtc?.ToUniversalTime();
                existingTripInspection.SyncedAtUtc = now;
                existingTripInspection.UpdatedAt = now;
                existingTripInspection.LastModifiedById = userId;

                await repo.UpdateAsync(existingTripInspection);
                await _unitOfWork.SaveChangesAsync();
                await EnsureDefectAndWorkOrderAsync(existingTripInspection, template);
                await TryStartWorkflowIfConfiguredAsync(existingTripInspection);

                _logger.LogInformation(
                    "Mobile fleet inspection {InspectionId} updated pending trip {FleetTripId} for asset {AssetId}.",
                    existingTripInspection.Id,
                    linkedTrip.Id,
                    assetId);

                return (await GetByIdAsync(existingTripInspection.Id))!;
            }
        }

        var entity = new FleetTripInspection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FleetTripId = linkedTrip?.Id,
            VehicleAssetId = assetId,
            InspectionTemplateId = template.Id,
            InspectorEmployeeId = inspectorEmployeeId,
            InspectionKind = inspectionKind,
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Status = "Completed",
            OverallResult = overallResult,
            InspectionData = string.IsNullOrWhiteSpace(dto.InspectionData) ? "{}" : dto.InspectionData,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            ClientSubmissionId = submissionId,
            CapturedOfflineAtUtc = dto.CapturedOfflineAtUtc?.ToUniversalTime(),
            SyncedAtUtc = now,
            CreatedAt = now,
            CreatedById = userId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await EnsureDefectAndWorkOrderAsync(entity, template);
        await TryStartWorkflowIfConfiguredAsync(entity);

        if (linkedTrip != null)
        {
            _logger.LogInformation(
                "Mobile fleet inspection {InspectionId} linked to pending trip {FleetTripId} for asset {AssetId}.",
                entity.Id,
                linkedTrip.Id,
                assetId);
        }

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetTripInspectionDto> SubmitForApprovalAsync(Guid inspectionId)
    {
        var entity = await RequireInspectionAsync(inspectionId);
        if (!string.Equals(entity.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entity.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only completed or rejected inspections can be submitted for approval.");

        var result = await _workflowIntegrationService.SubmitAsync(WorkflowEntityType, inspectionId);
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(result.ExecutionResult.Message ?? "Failed to start inspection workflow.");

        _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType)
            .ApplySubmitOutcome(entity, result, _currentUserProvider.UserId);
        await SaveWorkflowStatusAsync(entity);
        return (await GetByIdAsync(entity.Id))!;
    }

    public Task<FleetTripInspectionDto> ApproveAsync(Guid inspectionId, string? comments = null)
        => ProcessApprovalAsync(inspectionId, "Approve", comments);

    public Task<FleetTripInspectionDto> RejectAsync(Guid inspectionId, string? comments = null)
        => ProcessApprovalAsync(inspectionId, "Reject", comments);

    private async Task<FleetTripInspectionDto> ProcessApprovalAsync(Guid inspectionId, string action, string? comments)
    {
        var entity = await RequireInspectionAsync(inspectionId);
        if (!string.Equals(entity.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Inspection must be submitted before it can be reviewed.");

        var userId = _currentUserProvider.UserId;
        if (!await _workflowIntegrationService.CanUserApproveAsync(WorkflowEntityType, inspectionId, userId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current inspection workflow step.");

        var result = await _workflowIntegrationService.ProcessApprovalAsync(WorkflowEntityType, inspectionId, userId, action, comments);
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(result.ExecutionResult.Message ?? "Failed to process inspection approval.");

        _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType)
            .ApplyApprovalOutcome(entity, result.Outcome, userId, comments);
        await SaveWorkflowStatusAsync(entity);
        return (await GetByIdAsync(entity.Id))!;
    }

    private async Task<FleetTripInspection> RequireInspectionAsync(Guid inspectionId)
    {
        if (inspectionId == Guid.Empty) throw new ArgumentException("InspectionId is required.");
        return await _unitOfWork.Repository<FleetTripInspection>()
            .FirstOrDefaultAsync(i => i.Id == inspectionId && i.TenantId == _currentUserProvider.TenantId && !i.IsDeleted)
            ?? throw new ArgumentException("Inspection not found.");
    }

    private async Task SaveWorkflowStatusAsync(FleetTripInspection entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await _unitOfWork.Repository<FleetTripInspection>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task TryStartWorkflowIfConfiguredAsync(FleetTripInspection inspection)
    {
        if (!string.Equals(inspection.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(inspection.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var result = await _workflowIntegrationService.SubmitAsync(WorkflowEntityType, inspection.Id);
            if (!result.ExecutionResult.Success)
            {
                _logger.LogWarning(
                    "Inspection {InspectionId} was saved but workflow submission did not complete: {Message}",
                    inspection.Id,
                    result.ExecutionResult.Message);
                return;
            }

            _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType)
                .ApplySubmitOutcome(inspection, result, _currentUserProvider.UserId);
            await SaveWorkflowStatusAsync(inspection);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active workflow definition", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Inspection {InspectionId} was saved without workflow because no active FleetTripInspection workflow definition is configured.",
                inspection.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inspection {InspectionId} was saved but automatic workflow submission failed.", inspection.Id);
        }
    }

    private static string ValidateAndNormalizeResult(InspectionTemplate? template, string? inspectionData, string? requestedResult)
    {
        var result = string.IsNullOrWhiteSpace(requestedResult) ? "Pass" : requestedResult.Trim();
        if (template == null) return result;

        var checklistItems = ParseChecklistItems(template.ChecklistItems);
        if (checklistItems.Count == 0) return result;

        var responses = ParseChecklistResponses(inspectionData)
            ?? throw new InvalidOperationException("This inspection template requires a checklist. Please complete the checklist before finishing the inspection.");
        var missingRequired = checklistItems
            .Where(i => i.Required)
            .Where(i => !responses.TryGetValue(i.Id, out var value) || string.IsNullOrWhiteSpace(value))
            .ToList();
        if (missingRequired.Count > 0)
            throw new InvalidOperationException($"Checklist incomplete. Please complete required items ({missingRequired.Count}) before finishing the inspection.");

        if (responses.Values.Any(IsFailedResponse)) return "Fail";
        if (responses.Values.Any(IsFlaggedResponse)) return "ConditionalPass";
        return result;
    }

    private static bool IsFailedResponse(string? value) =>
        string.Equals(value?.Trim(), "Fail", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "Failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "No", StringComparison.OrdinalIgnoreCase);

    private static bool IsFlaggedResponse(string? value) =>
        string.Equals(value?.Trim(), "Flag", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "Flagged", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "Attention", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "ConditionalPass", StringComparison.OrdinalIgnoreCase);

    private async Task EnsureDefectAndWorkOrderAsync(FleetTripInspection inspection, InspectionTemplate? template)
    {
        var isFailed = string.Equals(inspection.OverallResult, "Fail", StringComparison.OrdinalIgnoreCase);
        var isFlagged = string.Equals(inspection.OverallResult, "ConditionalPass", StringComparison.OrdinalIgnoreCase);
        if (!isFailed && !isFlagged) return;

        try
        {
            var tenantId = _currentUserProvider.TenantId;
            var existing = await _unitOfWork.Repository<FleetDefect>()
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.FleetTripInspectionId == inspection.Id && !d.IsDeleted);
            var defect = existing == null
                ? await _fleetDefectService.CreateAsync(new CreateFleetDefectDto
                {
                    VehicleAssetId = inspection.VehicleAssetId,
                    FleetTripId = inspection.FleetTripId,
                    FleetTripInspectionId = inspection.Id,
                    Title = $"{(isFailed ? "Failed" : "Flagged")} {GetSheetLabel(template)}",
                    Description = inspection.Notes,
                    Severity = isFailed ? "High" : "Medium"
                })
                : await _fleetDefectService.GetByIdAsync(existing.Id);

            if (defect == null || defect.WorkOrderId.HasValue || template?.AutoCreateWorkOrderOnFailure == false) return;

            var defaults = await ResolveWorkOrderDefaultsAsync(template, isFailed);
            if (defaults == null)
            {
                _logger.LogWarning("Inspection {InspectionId} created defect {DefectId}, but no active maintenance work-order setup was available", inspection.Id, defect.Id);
                return;
            }

            await _fleetDefectService.CreateWorkOrderAsync(new CreateWorkOrderFromFleetDefectDto
            {
                DefectId = defect.Id,
                WorkOrderTypeId = defaults.Value.WorkOrderTypeId,
                MaintenanceTypeId = defaults.Value.MaintenanceTypeId,
                PriorityLevelId = defaults.Value.PriorityLevelId,
                BillingType = defaults.Value.BillingType,
                TitleOverride = $"{GetSheetLabel(template)}: {inspection.OverallResult}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create maintenance follow-up for fleet inspection {InspectionId}", inspection.Id);
        }
    }

    private async Task<(Guid WorkOrderTypeId, Guid MaintenanceTypeId, Guid PriorityLevelId, string BillingType)?> ResolveWorkOrderDefaultsAsync(
        InspectionTemplate? template,
        bool isFailed)
    {
        var tenantId = _currentUserProvider.TenantId;
        var settings = await _unitOfWork.Repository<MaintenanceSettings>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted);
        var workOrderTypes = await _unitOfWork.Repository<WorkOrderType>()
            .GetQueryable(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted)
            .ToListAsync();
        var maintenanceTypes = await _unitOfWork.Repository<MaintenanceType>()
            .GetQueryable(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted)
            .ToListAsync();
        var priorities = await _unitOfWork.Repository<PriorityLevel>()
            .GetQueryable(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted)
            .ToListAsync();

        var workOrderType = workOrderTypes.FirstOrDefault(x => x.Id == template?.FailureWorkOrderTypeId)
            ?? workOrderTypes.FirstOrDefault(x => x.Id == settings?.DefaultFleetDefectWorkOrderTypeId)
            ?? workOrderTypes.FirstOrDefault(x => ContainsAny(x.Code, "INSP", "REPAIR", "CORR"))
            ?? workOrderTypes.FirstOrDefault(x => ContainsAny(x.Name, "inspection", "repair", "corrective"))
            ?? workOrderTypes.FirstOrDefault();
        var maintenanceType = maintenanceTypes.FirstOrDefault(x => x.Id == template?.FailureMaintenanceTypeId)
            ?? maintenanceTypes.FirstOrDefault(x => x.Id == settings?.DefaultFleetDefectMaintenanceTypeId)
            ?? maintenanceTypes.FirstOrDefault(x => ContainsAny(x.MaintenanceClass, "corrective") || ContainsAny(x.Category, "corrective", "inspection"))
            ?? maintenanceTypes.FirstOrDefault();
        var priority = priorities.FirstOrDefault(x => x.Id == template?.FailurePriorityLevelId)
            ?? priorities.FirstOrDefault(x => x.Id == settings?.DefaultFleetDefectPriorityLevelId)
            ?? priorities.OrderBy(x => Math.Abs(x.Level - (isFailed ? 2 : 3))).FirstOrDefault();

        return workOrderType == null || maintenanceType == null || priority == null
            ? null
            : (workOrderType.Id, maintenanceType.Id, priority.Id,
                NormalizeBillingType(string.IsNullOrWhiteSpace(template?.FailureBillingType) ||
                    string.Equals(template.FailureBillingType, "Default", StringComparison.OrdinalIgnoreCase)
                    ? settings?.DefaultFleetDefectBillingType
                    : template.FailureBillingType));
    }

    private static string ResolveInspectionKind(string? requestedKind, InspectionTemplate template)
    {
        var requested = requestedKind?.Trim();
        if (string.Equals(template.TemplateScope, "Fleet", StringComparison.OrdinalIgnoreCase))
            return string.Equals(requested, "PostTrip", StringComparison.OrdinalIgnoreCase) ? "PostTrip" : "PreTrip";

        if (!string.IsNullOrWhiteSpace(requested)) return requested.Length <= 30 ? requested : requested[..30];
        return template.SheetType switch
        {
            "ServiceSheet" => "Service",
            "WeeklyChecklist" => "Weekly",
            "PreventiveMaintenanceForm" => "PreventiveMaintenance",
            _ => "Inspection"
        };
    }

    private static string GetSheetLabel(InspectionTemplate? template) => template?.SheetType switch
    {
        "ServiceSheet" => "service sheet",
        "WeeklyChecklist" => "weekly checklist",
        "PreventiveMaintenanceForm" => "preventive maintenance form",
        _ => "inspection sheet"
    };

    private static bool ContainsAny(string? value, params string[] terms) =>
        !string.IsNullOrWhiteSpace(value) && terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeBillingType(string? value) =>
        string.Equals(value?.Trim(), "Maintenance", StringComparison.OrdinalIgnoreCase)
            ? "Maintenance"
            : "Repairs";

    private async Task EnrichDefectLinksAsync(IList<FleetTripInspectionDto> inspections)
    {
        if (inspections.Count == 0) return;
        var tenantId = _currentUserProvider.TenantId;
        var ids = inspections.Select(i => i.Id).ToList();
        var defects = await _unitOfWork.Repository<FleetDefect>()
            .GetQueryable(d => d.TenantId == tenantId && d.FleetTripInspectionId.HasValue && ids.Contains(d.FleetTripInspectionId.Value) && !d.IsDeleted)
            .Select(d => new { InspectionId = d.FleetTripInspectionId!.Value, d.Id, d.WorkOrderId })
            .ToListAsync();
        var byInspection = defects.GroupBy(d => d.InspectionId).ToDictionary(g => g.Key, g => g.First());
        foreach (var inspection in inspections)
        {
            if (!byInspection.TryGetValue(inspection.Id, out var defect)) continue;
            inspection.DefectId = defect.Id;
            inspection.WorkOrderId = defect.WorkOrderId;
        }
    }

    private sealed class ChecklistItem
    {
        public Guid Id { get; set; }
        public bool Required { get; set; }
    }

    private static List<ChecklistItem> ParseChecklistItems(string? json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<ChecklistItem>();

            var items = JsonSerializer.Deserialize<List<JsonElement>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<JsonElement>();

            var result = new List<ChecklistItem>();
            foreach (var el in items)
            {
                if (el.ValueKind != JsonValueKind.Object) continue;

                Guid id = Guid.Empty;
                if (el.TryGetProperty("id", out var idProp))
                {
                    if (idProp.ValueKind == JsonValueKind.String)
                        Guid.TryParse(idProp.GetString(), out id);
                    else if (idProp.ValueKind == JsonValueKind.Undefined || idProp.ValueKind == JsonValueKind.Null)
                        id = Guid.Empty;
                }
                if (id == Guid.Empty) continue;

                var required = false;
                if (el.TryGetProperty("required", out var reqProp))
                {
                    if (reqProp.ValueKind == JsonValueKind.True) required = true;
                    else if (reqProp.ValueKind == JsonValueKind.False) required = false;
                }

                result.Add(new ChecklistItem { Id = id, Required = required });
            }

            return result;
        }
        catch
        {
            return new List<ChecklistItem>();
        }
    }

    private static Dictionary<Guid, string>? ParseChecklistResponses(string? inspectionDataJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(inspectionDataJson)) return null;

            var root = JsonSerializer.Deserialize<JsonElement>(inspectionDataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (root.ValueKind != JsonValueKind.Object) return null;

            if (!root.TryGetProperty("schema", out var schemaProp) || schemaProp.ValueKind != JsonValueKind.String)
                return null;

            var schema = schemaProp.GetString() ?? string.Empty;
            if (!string.Equals(schema, "FleetTripInspectionChecklist.v1", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!root.TryGetProperty("checklist", out var listProp) || listProp.ValueKind != JsonValueKind.Array)
                return null;

            var dict = new Dictionary<Guid, string>();
            foreach (var row in listProp.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) continue;

                if (!row.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String) continue;
                if (!Guid.TryParse(idProp.GetString(), out var id) || id == Guid.Empty) continue;

                string value = string.Empty;
                if (row.TryGetProperty("value", out var vProp))
                {
                    if (vProp.ValueKind == JsonValueKind.String) value = vProp.GetString() ?? string.Empty;
                    else value = vProp.ToString() ?? string.Empty;
                }

                dict[id] = value;
            }

            return dict;
        }
        catch
        {
            return null;
        }
    }

    private async Task<FleetTrip?> ResolvePendingTripForMobileSubmissionAsync(Guid assetId, string inspectionKind, DateTime completedAtUtc)
    {
        if (!string.Equals(inspectionKind, "PreTrip", StringComparison.OrdinalIgnoreCase))
            return null;

        var tenantId = _currentUserProvider.TenantId;
        var candidateTrips = await _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t =>
                t.TenantId == tenantId &&
                t.VehicleAssetId == assetId &&
                !t.IsDeleted &&
                (t.Status == FleetTripStatuses.Draft ||
                 t.Status == FleetTripStatuses.Submitted ||
                 t.Status == FleetTripStatuses.Approved))
            .ToListAsync();

        if (candidateTrips.Count == 0)
            return null;

        return candidateTrips
            .OrderBy(GetPendingTripStatusRank)
            .ThenBy(t => GetPlannedStartDistanceTicks(t, completedAtUtc))
            .ThenByDescending(t => t.CreatedAt)
            .FirstOrDefault();
    }

    private static bool IsMobileTripInspectionUpdateAllowed(FleetTripInspection inspection) =>
        string.Equals(inspection.Status, "InProgress", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(inspection.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(inspection.Status, "Rejected", StringComparison.OrdinalIgnoreCase);

    private static int GetPendingTripStatusRank(FleetTrip trip) => trip.Status switch
    {
        FleetTripStatuses.Approved => 0,
        FleetTripStatuses.Submitted => 1,
        FleetTripStatuses.Draft => 2,
        _ => 9
    };

    private static long GetPlannedStartDistanceTicks(FleetTrip trip, DateTime completedAtUtc)
    {
        if (!trip.PlannedStartAt.HasValue)
            return long.MaxValue;

        var plannedStartUtc = trip.PlannedStartAt.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(trip.PlannedStartAt.Value, DateTimeKind.Utc)
            : trip.PlannedStartAt.Value.ToUniversalTime();

        var distance = plannedStartUtc - completedAtUtc;
        return Math.Abs(distance.Ticks);
    }

    public async Task<bool> CancelAsync(Guid inspectionId, string? notes = null)
    {
        if (inspectionId == Guid.Empty) return false;

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetTripInspection>();
        var entity = await repo.FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == inspectionId && !i.IsDeleted);
        if (entity == null) return false;

        entity.Status = "Cancelled";
        entity.Notes = string.IsNullOrWhiteSpace(notes) ? entity.Notes : notes.Trim();
        entity.UpdatedAt = now;
        entity.LastModifiedById = userId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}

