using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public sealed class InventoryNegativeStockControlService : IInventoryNegativeStockControlService
{
    private const string ProfileCode = "TDC-PROCUREMENT";
    private const string DecisionKey = "DEC-010";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementConfigurationService _configuration;
    private readonly IProcurementAccessControlService _access;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IInventoryNegativeStockMutationStore _mutationStore;

    public InventoryNegativeStockControlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementConfigurationService configuration,
        IProcurementAccessControlService access,
        IProcurementControlEventService controlEvents,
        IInventoryNegativeStockMutationStore mutationStore)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _configuration = configuration;
        _access = access;
        _controlEvents = controlEvents;
        _mutationStore = mutationStore;
    }

    private Guid TenantId => _currentUser.TenantId;
    private Guid UserId => _currentUser.UserId;
    private IQueryable<InventoryNegativeStockOverride> Overrides =>
        _unitOfWork.Repository<InventoryNegativeStockOverride>().GetQueryable();

    public async Task<InventoryNegativeStockPolicyDto> GetEffectivePolicyAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var effective = await ResolvePolicyAsync(DateTime.UtcNow, cancellationToken);
        return new InventoryNegativeStockPolicyDto
        {
            ConfigurationProfileId = effective.Profile.Id,
            ConfigurationProfileVersion = effective.Profile.Version,
            DefaultPolicy = effective.Value.DefaultPolicy,
            EmergencyOverrideEligible = effective.Value.EmergencyOverrideEligible,
            OverridePermission = effective.Value.OverridePermission,
            WorkflowDefinitionId = effective.Value.WorkflowDefinitionId,
            EvidenceRequirements = effective.Value.EvidenceRequirements.ToList(),
            OverrideDurationHours = effective.Value.OverrideDurationHours,
            AuditRequired = effective.Value.AuditRequired
        };
    }

    public async Task<IReadOnlyList<InventoryNegativeStockOverrideDto>> GetOverridesAsync(
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        take = Math.Clamp(take, 1, 500);
        var values = await Overrides
            .Where(value => value.TenantId == TenantId && !value.IsDeleted)
            .AsNoTracking()
            .Include(value => value.InventoryItem)
            .Include(value => value.Warehouse)
            .OrderByDescending(value => value.ApprovedAtUtc)
            .ToListAsync(cancellationToken);
        var accessByScope = new Dictionary<(Guid WarehouseId, Guid? LocationId), bool>();
        var result = new List<InventoryNegativeStockOverrideDto>(Math.Min(values.Count, take));
        foreach (var value in values)
        {
            var scope = (value.WarehouseId, value.LocationId);
            if (!accessByScope.TryGetValue(scope, out var allowed))
            {
                allowed = await CanReadOverrideAsync(value, cancellationToken);
                accessByScope[scope] = allowed;
            }
            if (allowed) result.Add(Map(value));
            if (result.Count == take) break;
        }
        return result;
    }

    public async Task<InventoryNegativeStockOverrideDto> RegisterApprovedOverrideAsync(
        RegisterInventoryNegativeStockOverrideRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        if (request.InventoryItemId == Guid.Empty || request.WarehouseId == Guid.Empty ||
            request.ReferenceId == Guid.Empty || request.WorkflowInstanceId == Guid.Empty ||
            request.CentralDocumentVersionId == Guid.Empty || request.AuthorizedQuantity <= 0)
            throw Error("INV_NEGATIVE_OVERRIDE_REQUEST_INVALID",
                "Item, warehouse, exact transaction reference, positive quantity, completed workflow, and central-DMS evidence are required.");

        var now = DateTime.UtcNow;
        var effective = await ResolvePolicyAsync(now, cancellationToken);
        if (effective.Value.DefaultPolicy != ProcurementNegativeStockPolicy.ControlledEmergencyOverride ||
            !effective.Value.EmergencyOverrideEligible)
            throw Error("INV_NEGATIVE_OVERRIDE_DISABLED",
                "DEC-010 prohibits emergency negative-stock overrides for the effective configuration profile.");
        if (!effective.Value.WorkflowDefinitionId.HasValue)
            throw Error("INV_NEGATIVE_OVERRIDE_WORKFLOW_NOT_CONFIGURED",
                "DEC-010 must select a shared workflow definition before an emergency override can be registered.");

        var expires = EnsureUtc(request.ExpiresAtUtc);
        if (expires <= now || expires > now.AddHours(effective.Value.OverrideDurationHours))
            throw Error("INV_NEGATIVE_OVERRIDE_EXPIRY_INVALID",
                $"The override must expire within {effective.Value.OverrideDurationHours} hour(s) of registration.");

        await RequireCapabilityAsync(effective.Value.OverridePermission, request.WarehouseId,
            request.LocationId, request.ReferenceNumber, correlationId, cancellationToken);
        await EnsureStockScopeAsync(request.InventoryItemId, request.WarehouseId, request.LocationId, cancellationToken);

        var workflow = await _unitOfWork.Repository<WorkflowInstance>()
            .GetQueryable(value => value.TenantId == TenantId && value.Id == request.WorkflowInstanceId && !value.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("INV_NEGATIVE_OVERRIDE_WORKFLOW_NOT_FOUND",
                "The shared workflow instance was not found in the current tenant.");
        if (workflow.WorkflowDefinitionId != effective.Value.WorkflowDefinitionId.Value ||
            workflow.Status != WorkflowInstanceStatus.Completed || workflow.EntityId != request.ReferenceId)
            throw Error("INV_NEGATIVE_OVERRIDE_WORKFLOW_INVALID",
                "The exact DEC-010 workflow must be completed and bound to the same transaction reference.");

        var approval = await _unitOfWork.Repository<WorkflowApproval>()
            .GetQueryable(value => value.TenantId == TenantId && !value.IsDeleted &&
                value.StepInstance.WorkflowInstanceId == workflow.Id &&
                value.Status == WorkflowApprovalStatus.Approved)
            .AsNoTracking()
            .OrderByDescending(value => value.ProcessedDate)
            .FirstOrDefaultAsync(value => (value.ProcessedById ?? value.ApproverId).HasValue &&
                (value.ProcessedById ?? value.ApproverId) != workflow.InitiatedById, cancellationToken)
            ?? throw Error("INV_NEGATIVE_OVERRIDE_SOD_INVALID",
                "The emergency workflow requires an approval by an actor independent from the requester.");
        var approvedById = approval.ProcessedById ?? approval.ApproverId!.Value;
        if (approvedById == UserId)
            throw Error("INV_NEGATIVE_OVERRIDE_REGISTRAR_SOD",
                "The emergency approver cannot register their own override for consumption.");

        var evidence = await _unitOfWork.Repository<CentralDocumentVersion>()
            .GetQueryable(value => value.TenantId == TenantId && value.Id == request.CentralDocumentVersionId)
            .Include(value => value.DocumentRecord)
            .AsNoTracking()
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("INV_NEGATIVE_OVERRIDE_EVIDENCE_INVALID",
                "Evidence must reference the current Published version in central DMS.");
        if (!evidence.FileUploadRecordId.HasValue)
            throw Error("INV_NEGATIVE_OVERRIDE_EVIDENCE_FILE_MISSING",
                "The central-DMS version has no retained upload record.");
        var upload = await _unitOfWork.Repository<FileUploadRecord>()
            .GetQueryable(value => value.TenantId == TenantId && value.Id == evidence.FileUploadRecordId.Value && !value.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("INV_NEGATIVE_OVERRIDE_EVIDENCE_FILE_MISSING",
                "The central-DMS upload record was not found in the current tenant.");
        if (upload.VirusScanStatus != FileVirusScanStatus.Clean)
            throw Error("INV_NEGATIVE_OVERRIDE_EVIDENCE_SCAN_INVALID",
                "Emergency evidence must pass the centralized malware scan before it can authorize stock.");

        if (await Overrides.AnyAsync(value => value.TenantId == TenantId &&
                value.WorkflowInstanceId == workflow.Id && !value.IsDeleted, cancellationToken))
            throw Error("INV_NEGATIVE_OVERRIDE_WORKFLOW_REUSED",
                "This completed workflow has already authorized a negative-stock override.");

        var decisionHash = Hash(JsonSerializer.Serialize(new
        {
            ProfileId = effective.Profile.Id,
            ProfileVersion = effective.Profile.Version,
            DecisionId = effective.Decision.Id,
            DecisionValue = effective.Decision.Value,
            DecisionRowVersion = effective.Decision.RowVersion
        }, JsonOptions));
        var entity = new InventoryNegativeStockOverride
        {
            TenantId = TenantId,
            InventoryItemId = request.InventoryItemId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            ReferenceId = request.ReferenceId,
            ReferenceLineId = request.ReferenceLineId,
            ReferenceType = Required(request.ReferenceType, 100),
            ReferenceNumber = Required(request.ReferenceNumber, 100),
            Reason = Required(request.Reason, 2000),
            AuthorizedQuantity = request.AuthorizedQuantity,
            ConfigurationProfileId = effective.Profile.Id,
            ConfigurationDecisionId = effective.Decision.Id,
            ConfigurationProfileVersion = effective.Profile.Version,
            DecisionSnapshotHash = decisionHash,
            WorkflowInstanceId = workflow.Id,
            CentralDocumentVersionId = evidence.Id,
            FileUploadRecordId = upload.Id,
            EvidenceReference = Required(request.EvidenceReference, 500),
            RequestedById = workflow.InitiatedById,
            ApprovedById = approvedById,
            ApprovedAtUtc = EnsureUtc(approval.ProcessedDate ?? workflow.CompletedDate ?? now),
            ExpiresAtUtc = expires,
            CreatedById = UserId
        };
        entity.IntegrityHash = Integrity(entity, upload);
        await _unitOfWork.Repository<InventoryNegativeStockOverride>().AddAsync(entity);
        await AddAuditAsync("InventoryNegativeStockOverride.Registered", entity.Id, new
        {
            entity.InventoryItemId, entity.WarehouseId, entity.LocationId, entity.ReferenceId,
            entity.ReferenceLineId, entity.AuthorizedQuantity, entity.ConfigurationProfileVersion,
            entity.WorkflowInstanceId, entity.CentralDocumentVersionId, entity.ExpiresAtUtc,
            entity.DecisionSnapshotHash, entity.IntegrityHash
        }, correlationId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordControlEventAsync(entity, "Register", ProcurementControlEventResult.Succeeded,
            correlationId, "The independently approved emergency override was registered.", cancellationToken);
        return await MapAsync(entity.Id, cancellationToken);
    }

    public async Task<InventoryStockDecreaseAuthorization> PrepareDecreaseAsync(
        InventoryStockDecreaseRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        if (request.Quantity <= 0 || request.InventoryItemId == Guid.Empty || request.WarehouseId == Guid.Empty ||
            request.ReferenceId == Guid.Empty || string.IsNullOrWhiteSpace(request.ReferenceType))
            throw Error("INV_NEGATIVE_DECREASE_INVALID", "A positive quantity and exact tenant stock reference are required.");
        if (!_unitOfWork.HasActiveTransaction || !_mutationStore.HasRequiredTransaction)
            throw Error("INV_NEGATIVE_TRANSACTION_REQUIRED",
                "Stock decreases must execute inside the shared database transaction before balances are loaded.");

        await _unitOfWork.AcquireTransactionLockAsync(
            $"inventory-stock:{TenantId:N}:{request.WarehouseId:N}:{request.InventoryItemId:N}", cancellationToken);
        await EnsureStockScopeAsync(request.InventoryItemId, request.WarehouseId, request.LocationId, cancellationToken);

        var warehouseQuantity = await _unitOfWork.Repository<WarehouseQuantity>()
            .GetQueryable(value => value.TenantId == TenantId && value.WarehouseId == request.WarehouseId &&
                value.InventoryItemId == request.InventoryItemId && !value.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        var inventoryItem = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(value => value.TenantId == TenantId && value.Id == request.InventoryItemId && !value.IsDeleted)
            .AsNoTracking()
            .SingleAsync(cancellationToken);
        if (warehouseQuantity is null)
            throw Error("INV_NEGATIVE_BALANCE_NOT_FOUND",
                "The warehouse item balance must exist before a controlled stock decrease can be prepared.");
        var warehouseWouldBeNegative =
            (request.DecreaseCurrentStock && warehouseQuantity.CurrentStock - request.Quantity < 0) ||
            (request.DecreaseAvailableStock && warehouseQuantity.AvailableStock - request.Quantity < 0);
        var itemWouldBeNegative = request.CheckInventoryItemBalance &&
            (
            (request.DecreaseCurrentStock && inventoryItem.CurrentStock - request.Quantity < 0) ||
            (request.DecreaseAvailableStock && inventoryItem.AvailableStock - request.Quantity < 0));
        if (!warehouseWouldBeNegative && !itemWouldBeNegative)
            return new InventoryStockDecreaseAuthorization();

        var effective = await ResolvePolicyAsync(DateTime.UtcNow, cancellationToken);
        if (effective.Value.DefaultPolicy != ProcurementNegativeStockPolicy.ControlledEmergencyOverride ||
            !effective.Value.EmergencyOverrideEligible || !request.NegativeStockOverrideId.HasValue)
            throw Error("INV_NEGATIVE_STOCK_PROHIBITED",
                "DEC-010 prohibits this decrease because it would create unauthorized negative stock.");

        await RequireCapabilityAsync(effective.Value.OverridePermission, request.WarehouseId,
            request.LocationId, request.ReferenceNumber, request.CorrelationId, cancellationToken);
        var entity = await Overrides.SingleOrDefaultAsync(value => value.TenantId == TenantId &&
            value.Id == request.NegativeStockOverrideId.Value && !value.IsDeleted, cancellationToken)
            ?? throw Error("INV_NEGATIVE_OVERRIDE_NOT_FOUND",
                "The negative-stock override was not found in the current tenant.");
        if (entity.ConsumedAtUtc.HasValue || entity.ExpiresAtUtc <= DateTime.UtcNow ||
            entity.InventoryItemId != request.InventoryItemId || entity.WarehouseId != request.WarehouseId ||
            entity.ReferenceId != request.ReferenceId || entity.AuthorizedQuantity < request.Quantity ||
            (entity.ReferenceLineId.HasValue && entity.ReferenceLineId != request.ReferenceLineId) ||
            (entity.LocationId.HasValue && entity.LocationId != request.LocationId))
            throw Error("INV_NEGATIVE_OVERRIDE_SCOPE_INVALID",
                "The approved override is expired, consumed, or does not cover the exact item, warehouse, location, transaction line, and quantity.");

        var upload = await _unitOfWork.Repository<FileUploadRecord>()
            .GetQueryable(value => value.TenantId == TenantId && value.Id == entity.FileUploadRecordId && !value.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var evidenceCurrent = await _unitOfWork.Repository<CentralDocumentVersion>()
            .GetQueryable(value => value.TenantId == TenantId && value.Id == entity.CentralDocumentVersionId)
            .Include(value => value.DocumentRecord).AsNoTracking()
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .AnyAsync(cancellationToken);
        var workflowValid = await _unitOfWork.Repository<WorkflowInstance>()
            .GetQueryable(value => value.TenantId == TenantId && value.Id == entity.WorkflowInstanceId && !value.IsDeleted &&
                value.Status == WorkflowInstanceStatus.Completed && value.EntityId == request.ReferenceId)
            .AsNoTracking().AnyAsync(cancellationToken);
        if (upload?.VirusScanStatus != FileVirusScanStatus.Clean || !evidenceCurrent || !workflowValid ||
            !string.Equals(entity.IntegrityHash, Integrity(entity, upload), StringComparison.Ordinal))
            throw Error("INV_NEGATIVE_OVERRIDE_STALE",
                "The override workflow, current central-DMS evidence, malware result, or integrity snapshot is no longer valid.");

        var transactionId = await _mutationStore.GetCurrentTransactionIdAsync(cancellationToken);
        entity.ConsumedAtUtc = DateTime.UtcNow;
        entity.ConsumedByUserId = UserId;
        entity.ConsumedByReferenceId = request.ReferenceId;
        entity.ConsumptionTransactionId = transactionId;
        await _unitOfWork.Repository<InventoryNegativeStockOverride>().UpdateAsync(entity);
        await AddAuditAsync("InventoryNegativeStockOverride.Consumed", entity.Id, new
        {
            request.ReferenceId, request.ReferenceLineId, request.Quantity, transactionId
        }, request.CorrelationId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordControlEventAsync(entity, "Consume", ProcurementControlEventResult.Allowed,
            request.CorrelationId, "DEC-010 controlled emergency override consumed for the exact stock decrease.", cancellationToken);
        await _mutationStore.SetMutationContextAsync(entity.Id, transactionId, cancellationToken);
        return new InventoryStockDecreaseAuthorization
        {
            WouldBeNegative = true,
            EmergencyOverrideApplied = true,
            NegativeStockOverrideId = entity.Id,
            TransactionId = transactionId
        };
    }

    public Task ClearMutationContextAsync(CancellationToken cancellationToken = default) =>
        _mutationStore.ClearMutationContextAsync(cancellationToken);

    private async Task<(ProcurementConfigurationProfileDto Profile, ProcurementConfigurationDecisionDto Decision,
        ProcurementNegativeStockDecisionValueDto Value)> ResolvePolicyAsync(DateTime atUtc, CancellationToken cancellationToken)
    {
        var profile = await _configuration.GetEffectiveProfileAsync(ProfileCode, atUtc, cancellationToken)
            ?? throw Error("INV_NEGATIVE_CONFIGURATION_MISSING",
                "No effective Published TDC procurement configuration profile exists; negative stock is fail-closed.");
        var decision = profile.Decisions.SingleOrDefault(value => value.DecisionKey == DecisionKey && value.IsComplete)
            ?? throw Error("INV_NEGATIVE_CONFIGURATION_INCOMPLETE",
                "The effective DEC-010 negative-stock decision is not complete; negative stock is fail-closed.");
        var value = decision.Value.Deserialize<ProcurementNegativeStockDecisionValueDto>(JsonOptions)
            ?? throw Error("INV_NEGATIVE_CONFIGURATION_INVALID", "The effective DEC-010 value could not be read.");
        return (profile, decision, value);
    }

    private async Task EnsureStockScopeAsync(Guid itemId, Guid warehouseId, Guid? locationId, CancellationToken cancellationToken)
    {
        if (!await _unitOfWork.Repository<InventoryItem>().GetQueryable(value => value.TenantId == TenantId &&
                value.Id == itemId && !value.IsDeleted).AnyAsync(cancellationToken))
            throw Error("INV_NEGATIVE_ITEM_NOT_FOUND", "The inventory item was not found in the current tenant.");
        if (!await _unitOfWork.Repository<Warehouse>().GetQueryable(value => value.TenantId == TenantId &&
                value.Id == warehouseId && !value.IsDeleted && value.IsActive).AnyAsync(cancellationToken))
            throw Error("INV_NEGATIVE_WAREHOUSE_NOT_FOUND", "The warehouse was not found in the current tenant.");
        if (locationId.HasValue && !await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(value =>
                value.TenantId == TenantId && value.Id == locationId && !value.IsDeleted && value.IsActive &&
                (value.WarehouseId == warehouseId ||
                 (value.IsConsignmentBin && value.ConsignmentWarehouseId == warehouseId))).AnyAsync(cancellationToken))
            throw Error("INV_NEGATIVE_LOCATION_INVALID", "The location is inactive or outside the selected tenant warehouse.");
    }

    private async Task RequireCapabilityAsync(string permission, Guid warehouseId, Guid? locationId,
        string reference, string correlationId, CancellationToken cancellationToken)
    {
        var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = Required(permission, 200),
            WarehouseId = warehouseId,
            LocationId = locationId,
            RequireLocationScope = true,
            SourceType = "InventoryNegativeStockControl",
            SourceReference = string.IsNullOrWhiteSpace(reference) ? "negative-stock" : reference.Trim()
        }, NormalizeCorrelation(correlationId), cancellationToken);
        if (!decision.Allowed) throw new InventoryNegativeStockAuthorizationException(decision.Message);
    }

    private async Task<bool> CanReadOverrideAsync(
        InventoryNegativeStockOverride value,
        CancellationToken cancellationToken)
    {
        foreach (var permission in new[] { "procurement.inventory.read", "Inventory.EmergencyOverride" })
        {
            var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                WarehouseId = value.WarehouseId,
                LocationId = value.LocationId,
                RequireLocationScope = true,
                SourceType = "InventoryNegativeStockOverride",
                SourceReference = value.ReferenceNumber
            }, $"inventory-negative-stock-read:{value.Id:N}:{permission}", cancellationToken);
            if (decision.Allowed) return true;
        }
        return false;
    }

    private async Task RecordControlEventAsync(InventoryNegativeStockOverride entity, string action,
        ProcurementControlEventResult result, string correlationId, string reason, CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("negative-stock", TenantId, entity.Id, action,
                entity.ReferenceId, entity.ReferenceLineId, entity.IntegrityHash),
            EventType = "InventoryNegativeStockControl",
            Action = action,
            Result = result,
            RuleCode = DecisionKey,
            RuleId = entity.ConfigurationDecisionId,
            RuleVersion = $"{ProfileCode}:v{entity.ConfigurationProfileVersion}",
            DecisionKeys = new() { DecisionKey },
            SourceType = entity.ReferenceType,
            SourceId = entity.ReferenceId,
            SourceReference = entity.ReferenceNumber,
            Reason = reason,
            InputValues = new { entity.InventoryItemId, entity.WarehouseId, entity.LocationId, entity.AuthorizedQuantity },
            ResultValues = new { OverrideId = entity.Id, entity.ExpiresAtUtc, entity.ConsumedAtUtc, entity.ConsumptionTransactionId },
            CorrelationId = NormalizeCorrelation(correlationId),
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = new()
            {
                new ProcurementControlEventEvidenceReference
                {
                    ReferenceKind = ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                    ReferenceId = entity.FileUploadRecordId,
                    Reference = $"dms-version:{entity.CentralDocumentVersionId:N}",
                    Label = entity.EvidenceReference,
                    RequirementKey = DecisionKey
                }
            }
        }, cancellationToken);
    }

    private async Task AddAuditAsync(string action, Guid id, object after, string correlationId)
    {
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = TenantId,
            UserId = UserId,
            Username = string.IsNullOrWhiteSpace(_currentUser.Username) ? "Unknown" : _currentUser.Username,
            Action = action,
            Resource = "InventoryNegativeStockControl",
            ResourceId = id.ToString(),
            NewValues = JsonSerializer.Serialize(after, JsonOptions),
            IpAddress = "system",
            UserAgent = NormalizeCorrelation(correlationId),
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task<InventoryNegativeStockOverrideDto> MapAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await Overrides.Where(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted)
            .AsNoTracking().Include(value => value.InventoryItem).Include(value => value.Warehouse)
            .SingleAsync(cancellationToken);
        return Map(entity);
    }

    private static InventoryNegativeStockOverrideDto Map(InventoryNegativeStockOverride value) => new()
    {
        Id = value.Id,
        InventoryItemId = value.InventoryItemId,
        ItemCode = value.InventoryItem.ItemCode,
        ItemName = value.InventoryItem.Name,
        WarehouseId = value.WarehouseId,
        WarehouseName = value.Warehouse.Name,
        LocationId = value.LocationId,
        ReferenceId = value.ReferenceId,
        ReferenceLineId = value.ReferenceLineId,
        ReferenceType = value.ReferenceType,
        ReferenceNumber = value.ReferenceNumber,
        AuthorizedQuantity = value.AuthorizedQuantity,
        Reason = value.Reason,
        ConfigurationProfileVersion = value.ConfigurationProfileVersion,
        WorkflowInstanceId = value.WorkflowInstanceId,
        CentralDocumentVersionId = value.CentralDocumentVersionId,
        EvidenceReference = value.EvidenceReference,
        RequestedById = value.RequestedById,
        ApprovedById = value.ApprovedById,
        ApprovedAtUtc = value.ApprovedAtUtc,
        ExpiresAtUtc = value.ExpiresAtUtc,
        ConsumedAtUtc = value.ConsumedAtUtc,
        ConsumedByReferenceId = value.ConsumedByReferenceId,
        IsAvailable = !value.ConsumedAtUtc.HasValue && value.ExpiresAtUtc > DateTime.UtcNow,
        RowVersion = Convert.ToBase64String(value.RowVersion)
    };

    private void EnsureActor()
    {
        if (!_currentUser.IsAuthenticated || TenantId == Guid.Empty || UserId == Guid.Empty || _currentUser.IsExternalUser)
            throw new InventoryNegativeStockAuthorizationException("An authenticated internal tenant user is required.");
    }

    private static string Integrity(InventoryNegativeStockOverride entity, FileUploadRecord upload) => Hash(
        $"{entity.TenantId:N}|{entity.InventoryItemId:N}|{entity.WarehouseId:N}|{entity.LocationId:N}|" +
        $"{entity.ReferenceId:N}|{entity.ReferenceLineId:N}|{entity.AuthorizedQuantity}|{entity.ConfigurationProfileId:N}|" +
        $"{entity.ConfigurationDecisionId:N}|{entity.ConfigurationProfileVersion}|{entity.DecisionSnapshotHash}|" +
        $"{entity.WorkflowInstanceId:N}|{entity.CentralDocumentVersionId:N}|{entity.FileUploadRecordId:N}|" +
        $"{upload.StoredFileName}|{upload.FileSize}|{entity.ApprovedById:N}|{entity.ApprovedAtUtc:O}|{entity.ExpiresAtUtc:O}");

    private static InventoryNegativeStockControlException Error(string code, string message) => new(code, message);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Required(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max
        ? value.Trim()
        : throw Error("INV_NEGATIVE_VALUE_INVALID", $"A required value of at most {max} characters is invalid.");
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Guid.NewGuid().ToString("N")
        : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
