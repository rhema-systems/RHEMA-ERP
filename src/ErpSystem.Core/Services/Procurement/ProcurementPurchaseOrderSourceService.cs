using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementPurchaseOrderSourceService :
    IProcurementPurchaseOrderSourceService
{
    private const string Permission = "procurement.purchase-order.create";
    private const string EventType = "ProcurementPurchaseOrderSourceControl";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(number => $"DEC-{number:000}").ToArray();
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IProcurementRequisitionBudgetControlService _budgetControl;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly ILogger<ProcurementPurchaseOrderSourceService> _logger;

    public ProcurementPurchaseOrderSourceService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents,
        IProcurementRequisitionBudgetControlService budgetControl,
        INotificationTopicPublisher notificationTopics,
        ILogger<ProcurementPurchaseOrderSourceService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
        _budgetControl = budgetControl;
        _notificationTopics = notificationTopics;
        _logger = logger;
    }

    private IGenericRepository<PurchaseOrder> PurchaseOrders =>
        _unitOfWork.Repository<PurchaseOrder>();
    private IGenericRepository<PurchaseRequisition> Requisitions =>
        _unitOfWork.Repository<PurchaseRequisition>();
    private IGenericRepository<ProcurementSourcingCase> SourcingCases =>
        _unitOfWork.Repository<ProcurementSourcingCase>();
    private IGenericRepository<ProcurementRequisitionSourcingRelease> SourcingReleases =>
        _unitOfWork.Repository<ProcurementRequisitionSourcingRelease>();
    private IGenericRepository<ProcurementAwardReadinessDecision> ReadinessDecisions =>
        _unitOfWork.Repository<ProcurementAwardReadinessDecision>();

    public async Task<ProcurementPurchaseOrderSourceStatusDto> GetOptionsAsync(
        Guid? purchaseRequisitionId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(
            purchaseRequisitionId?.ToString() ?? "source-options",
            correlationId,
            cancellationToken);

        var options = new List<ProcurementPurchaseOrderSourceOptionDto>();
        await AddTenderAwardOptionsAsync(options, purchaseRequisitionId, cancellationToken);
        await AddContractOptionsAsync(options, purchaseRequisitionId, cancellationToken);
        await AddExceptionOptionsAsync(options, purchaseRequisitionId, cancellationToken);
        options = options
            .OrderBy(item => item.SourceType)
            .ThenBy(item => item.SourceReference)
            .ToList();

        var blocked = new List<string>();
        if (options.Count == 0)
        {
            blocked.Add(purchaseRequisitionId.HasValue
                ? "No approved award, active contract, or awarded approved-exception source is available for this requisition."
                : "No approved ordinary-PO source is currently available. Complete sourcing and award, or use the dedicated framework call-off route.");
        }

        return new ProcurementPurchaseOrderSourceStatusDto
        {
            Ready = options.Count != 0,
            CandidateCount = options.Count,
            BlockedReasons = blocked,
            Sources = options
        };
    }

    public async Task<ProcurementPurchaseOrderSourceResolution> ResolveAsync(
        ProcurementPurchaseOrderSourceType sourceType,
        Guid sourceId,
        Guid businessPartnerId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(sourceId.ToString(), correlationId, cancellationToken);
        try
        {
            if (sourceId == Guid.Empty)
                throw Invalid("PO_SOURCE_REQUIRED", "An approved purchase-order source is required.");
            if (businessPartnerId == Guid.Empty)
                throw Invalid("PO_SOURCE_SUPPLIER_REQUIRED", "A source supplier is required.");
            if (!ProcurementPurchaseOrderSourceRules.CanCreate(sourceType))
                throw sourceType switch
                {
                    ProcurementPurchaseOrderSourceType.FrameworkCallOff =>
                        Invalid("PO_FRAMEWORK_DEDICATED_ROUTE_REQUIRED",
                            "Framework purchase orders must be created through the dedicated framework call-off route."),
                    ProcurementPurchaseOrderSourceType.HistoricalMigration =>
                        Invalid("PO_HISTORICAL_SOURCE_FORBIDDEN",
                            "HistoricalMigration is reserved for migration backfill and cannot create a purchase order."),
                    _ => Invalid("PO_SOURCE_TYPE_INVALID",
                        "The purchase-order source type is not supported.")
                };

            var resolution = await ResolveCoreAsync(
                sourceType, sourceId, businessPartnerId, null, cancellationToken);

            await RecordAsync(
                resolution.SourceId,
                resolution.SourceReference,
                "SourceValidated",
                ProcurementControlEventResult.Allowed,
                correlationId,
                "The approved source lineage passed tenant, requisition, sourcing-case, award-readiness, and supplier checks.",
                new
                {
                    resolution.SourceType,
                    resolution.PurchaseRequisitionId,
                    resolution.SourcingReleaseId,
                    resolution.SourcingCaseId,
                    resolution.AwardReadinessDecisionId,
                    resolution.BusinessPartnerId,
                    resolution.SourceIntegrityHash
                },
                cancellationToken);
            return resolution;
        }
        catch (ProcurementPurchaseOrderSourceValidationException exception)
        {
            await TryRecordDeniedAsync(
                sourceId,
                sourceId == Guid.Empty ? "missing" : sourceId.ToString(),
                "SourceValidationDenied",
                correlationId,
                exception,
                new
                {
                    SourceType = sourceType,
                    BusinessPartnerId = businessPartnerId
                },
                cancellationToken);
            throw;
        }
    }

    public void Apply(
        PurchaseOrder purchaseOrder,
        ProcurementPurchaseOrderSourceResolution source)
    {
        if (purchaseOrder.TenantId != _currentUser.TenantId ||
            purchaseOrder.BusinessPartnerId != source.BusinessPartnerId)
        {
            throw Invalid("PO_SOURCE_TENANT_OR_SUPPLIER_MISMATCH",
                "The purchase order does not match the validated source tenant and supplier.");
        }

        purchaseOrder.ProcurementSourceType = source.SourceType;
        purchaseOrder.ProcurementSourceId = source.SourceId;
        purchaseOrder.ProcurementSourceReference = source.SourceReference;
        purchaseOrder.SourceRequisitionId = source.PurchaseRequisitionId;
        purchaseOrder.SourceRequisitionNumber = source.PurchaseRequisitionNumber;
        purchaseOrder.SourcingReleaseId = source.SourcingReleaseId;
        purchaseOrder.SourcingCaseId = source.SourcingCaseId;
        purchaseOrder.AwardReadinessDecisionId = source.AwardReadinessDecisionId;
        purchaseOrder.ProcurementCategory = source.ProcurementCategory;
        purchaseOrder.SourceSnapshotJson = source.SourceSnapshotJson;
        purchaseOrder.SourceIntegrityHash = source.SourceIntegrityHash;
        purchaseOrder.SourceValidatedAtUtc = source.ValidatedAtUtc;
        purchaseOrder.Currency = source.CurrencyCode;
        if (source.SourceType == ProcurementPurchaseOrderSourceType.TenderAward)
            purchaseOrder.TenderAwardId = source.SourceId;
        if (source.SourceType == ProcurementPurchaseOrderSourceType.Contract)
            purchaseOrder.ContractId = source.SourceId;
    }

    public async Task<ProcurementPurchaseOrderSourceResolution> ResolveFrameworkCallOffAsync(
        ProcurementFrameworkCallOff callOff,
        ProcurementFrameworkAgreement agreement,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (callOff.TenantId != _currentUser.TenantId ||
            agreement.TenantId != _currentUser.TenantId ||
            callOff.AgreementId != agreement.Id ||
            callOff.BusinessPartnerId != agreement.BusinessPartnerId)
        {
            throw Invalid("PO_FRAMEWORK_SOURCE_MISMATCH",
                "The framework call-off does not match its governed agreement.");
        }

        var retainedReadiness = await ReadinessDecisions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == agreement.AwardReadinessDecisionId &&
                item.SourceType == agreement.SourceType &&
                item.SourceId == agreement.SourceId &&
                !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_SOURCE_READINESS_NOT_FOUND",
                "The framework agreement award-readiness decision was not found.");
        var readiness = await ReadinessDecisions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.SourceType == retainedReadiness.SourceType &&
                item.SourceId == retainedReadiness.SourceId &&
                !item.IsDeleted)
            .OrderByDescending(item => item.DecisionSequence)
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_SOURCE_READINESS_NOT_FOUND",
                "The framework source has no current award-readiness decision.");
        if (readiness.Id != retainedReadiness.Id)
        {
            throw Invalid(
                "PO_FRAMEWORK_SOURCE_READINESS_SUPERSEDED",
                "The framework agreement does not retain the latest award-readiness decision.");
        }
        EnsureReady(readiness);
        var sourceLink = await ResolveSourceLinkAsync(
            readiness.SourceType, readiness.SourceId, cancellationToken);
        var requisition = await RequireApprovedRequisitionAsync(
            callOff.SourceRequisitionId, cancellationToken);
        var snapshot = new
        {
            SourceType = ProcurementPurchaseOrderSourceType.FrameworkCallOff,
            SourceId = callOff.Id,
            SourceReference = callOff.CallOffNumber,
            callOff.AgreementId,
            agreement.AgreementNumber,
            agreement.Version,
            DemandRequisitionId = requisition.Id,
            DemandRequisitionNumber = requisition.RequisitionNumber,
            AwardSourcingCaseId = sourceLink.SourcingCase.Id,
            AwardSourcingReleaseId = sourceLink.SourcingCase.SourcingReleaseId,
            agreement.AwardReadinessDecisionId,
            agreement.BusinessPartnerId,
            agreement.CurrencyCode,
            agreement.IntegrityHash
        };
        return BuildResolution(
            ProcurementPurchaseOrderSourceType.FrameworkCallOff,
            callOff.Id,
            callOff.CallOffNumber,
            requisition,
            sourceLink.SourcingCase,
            readiness,
            agreement.BusinessPartnerId,
            agreement.CurrencyCode,
            snapshot);
    }

    public async Task<ProcurementPurchaseOrderSourceResolution> RevalidateAsync(
        PurchaseOrder purchaseOrder,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var current = await EvaluateCurrentAsync(
                purchaseOrder, cancellationToken);

            purchaseOrder.SourceValidatedAtUtc = current.ValidatedAtUtc;
            await RecordAsync(
                purchaseOrder.Id,
                purchaseOrder.OrderNumber,
                $"SourceRevalidatedFor{NormalizeAction(action)}",
                ProcurementControlEventResult.Allowed,
                correlationId,
                "The immutable approved source lineage remains authoritative.",
                new
                {
                    purchaseOrder.ProcurementSourceType,
                    purchaseOrder.ProcurementSourceId,
                    purchaseOrder.SourceRequisitionId,
                    purchaseOrder.SourcingCaseId,
                    purchaseOrder.AwardReadinessDecisionId,
                    purchaseOrder.SourceIntegrityHash
                },
                cancellationToken);
            await PublishNotificationAsync(
                "procurement.purchase-order.source-revalidated",
                purchaseOrder,
                action,
                cancellationToken);
            return current;
        }
        catch (ProcurementPurchaseOrderSourceValidationException exception)
        {
            await TryRecordDeniedAsync(
                purchaseOrder.Id,
                purchaseOrder.OrderNumber,
                $"SourceRevalidationDeniedFor{NormalizeAction(action)}",
                correlationId,
                exception,
                new
                {
                    purchaseOrder.ProcurementSourceType,
                    purchaseOrder.ProcurementSourceId,
                    purchaseOrder.BusinessPartnerId
                },
                cancellationToken);
            throw;
        }
    }

    public async Task<ProcurementPurchaseOrderSourceResolution> EvaluateCurrentAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken = default)
    {
        if (purchaseOrder.TenantId != _currentUser.TenantId)
            throw new ProcurementPurchaseOrderSourceAuthorizationException(
                "The purchase order is not in the current tenant.");
        if (!purchaseOrder.ProcurementSourceType.HasValue ||
            !purchaseOrder.ProcurementSourceId.HasValue)
        {
            throw Invalid("PO_SOURCE_REQUIRED",
                "The purchase order has no governed approved source lineage.");
        }
        if (purchaseOrder.ProcurementSourceType ==
            ProcurementPurchaseOrderSourceType.HistoricalMigration)
        {
            throw Invalid("PO_HISTORICAL_SOURCE_REVALIDATION_REQUIRED",
                "This historical purchase order predates mandatory source lineage and cannot enter a new approval lifecycle without remediation.");
        }

        ProcurementPurchaseOrderSourceResolution current;
        if (purchaseOrder.ProcurementSourceType ==
            ProcurementPurchaseOrderSourceType.FrameworkCallOff)
        {
            var callOff = await _unitOfWork.Repository<ProcurementFrameworkCallOff>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == purchaseOrder.ProcurementSourceId.Value &&
                    item.PurchaseOrderId == purchaseOrder.Id && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw Invalid("PO_FRAMEWORK_SOURCE_NOT_FOUND",
                    "The linked framework call-off was not found.");
            if (callOff.Status is ProcurementFrameworkCallOffStatus.Rejected or
                ProcurementFrameworkCallOffStatus.Cancelled)
            {
                throw Invalid("PO_FRAMEWORK_SOURCE_TERMINAL",
                    "A rejected or cancelled framework call-off cannot authorize a purchase order.");
            }
            var agreement = await _unitOfWork.Repository<ProcurementFrameworkAgreement>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == callOff.AgreementId && !item.IsDeleted)
                .AsNoTracking().SingleAsync(cancellationToken);
            current = await ResolveFrameworkCallOffAsync(
                callOff, agreement, string.Empty, cancellationToken);
        }
        else
        {
            current = await ResolveCoreAsync(
                purchaseOrder.ProcurementSourceType.Value,
                purchaseOrder.ProcurementSourceId.Value,
                purchaseOrder.BusinessPartnerId,
                purchaseOrder.Id,
                cancellationToken);
        }

        if (current.PurchaseRequisitionId != purchaseOrder.SourceRequisitionId ||
            current.SourcingReleaseId != purchaseOrder.SourcingReleaseId ||
            current.SourcingCaseId != purchaseOrder.SourcingCaseId ||
            current.AwardReadinessDecisionId != purchaseOrder.AwardReadinessDecisionId ||
            !string.Equals(current.SourceIntegrityHash,
                purchaseOrder.SourceIntegrityHash, StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("PO_SOURCE_LINEAGE_CHANGED",
                "The persisted purchase-order source snapshot no longer matches the authoritative approved source.");
        }
        if (current.ProcurementCategory != purchaseOrder.ProcurementCategory)
        {
            throw Invalid("PO_SOURCE_CATEGORY_CHANGED",
                "The persisted purchase-order category no longer matches the approved requisition.");
        }

        if (current.SourceType !=
            ProcurementPurchaseOrderSourceType.FrameworkCallOff)
        {
            var persistedLines = await _unitOfWork.Repository<PurchaseOrderItem>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseOrderId == purchaseOrder.Id && !item.IsDeleted)
                .AsNoTracking()
                .Select(item => new ProcurementPurchaseOrderSourceOrderLine
                {
                    InventoryItemId = item.InventoryItemId,
                    ItemDescription = item.ItemDescription,
                    OrderedQuantity = item.OrderedQuantity,
                    UnitOfMeasure = item.UnitOfMeasure,
                    UnitPrice = item.UnitPrice
                })
                .ToListAsync(cancellationToken);
            EnsureOrderMatchesSource(
                current,
                persistedLines,
                purchaseOrder.TotalAmount,
                purchaseOrder.Currency);
            if (current.SourceType ==
                ProcurementPurchaseOrderSourceType.Contract)
            {
                await EnsureContractCapacityAsync(
                    current,
                    persistedLines,
                    purchaseOrder.TotalAmount,
                    purchaseOrder.Currency,
                    purchaseOrder.Id,
                    cancellationToken);
            }
        }

        return current;
    }

    public async Task ValidateOrderAsync(
        ProcurementPurchaseOrderSourceResolution source,
        IReadOnlyCollection<ProcurementPurchaseOrderSourceOrderLine> lines,
        decimal totalAmount,
        string? currencyCode,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureOrderMatchesSource(source, lines, totalAmount, currencyCode);
            await RecordAsync(
                source.SourceId,
                source.SourceReference,
                "SourceCommercialTermsValidated",
                ProcurementControlEventResult.Allowed,
                correlationId,
                "The purchase-order lines, quantities, prices, currency, and total match the authoritative approved source.",
                new
                {
                    source.SourceType,
                    source.SourceId,
                    source.ApprovedAmount,
                    ApprovedLineCount = source.ApprovedLines.Count,
                    SubmittedLineCount = lines.Count,
                    TotalAmount = totalAmount,
                    CurrencyCode = currencyCode
                },
                cancellationToken);
        }
        catch (ProcurementPurchaseOrderSourceValidationException exception)
        {
            await TryRecordDeniedAsync(
                source.SourceId,
                source.SourceReference,
                "SourceCommercialTermsDenied",
                correlationId,
                exception,
                new
                {
                    source.SourceType,
                    source.SourceId,
                    source.ApprovedAmount,
                    ApprovedLineCount = source.ApprovedLines.Count,
                    SubmittedLineCount = lines.Count,
                    TotalAmount = totalAmount,
                    CurrencyCode = currencyCode
                },
                cancellationToken);
            throw;
        }
    }

    public async Task ReserveAsync(
        ProcurementPurchaseOrderSourceResolution source,
        IReadOnlyCollection<ProcurementPurchaseOrderSourceOrderLine> lines,
        decimal totalAmount,
        string? currencyCode,
        Guid purchaseOrderId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!_unitOfWork.HasActiveTransaction)
        {
            throw Invalid(
                "PO_SOURCE_RESERVATION_TRANSACTION_REQUIRED",
                "Approved-source reservation must occur inside the purchase-order transaction.");
        }
        if (purchaseOrderId == Guid.Empty)
        {
            throw Invalid(
                "PO_SOURCE_RESERVATION_ORDER_REQUIRED",
                "A purchase-order identifier is required to reserve an approved source.");
        }

        var lockSupplier = source.SourceType ==
            ProcurementPurchaseOrderSourceType.Contract
                ? Guid.Empty
                : source.BusinessPartnerId;
        var lockResource =
            $"TDC:PO-SOURCE:{_currentUser.TenantId:N}:{(int)source.SourceType}:{source.SourceId:N}:{lockSupplier:N}";
        if (ProcurementPurchaseOrderSourceRules.IsOneTime(source.SourceType))
        {
            var awardReservation = await ResolveAwardReservationAsync(
                source.SourceType,
                source.SourceId,
                source.BusinessPartnerId,
                cancellationToken);
            lockResource =
                ProcurementPurchaseOrderSourceRules.BuildAwardReservationLock(
                    _currentUser.TenantId,
                    awardReservation.SourceType,
                    awardReservation.SourceId,
                    source.BusinessPartnerId);
        }
        try
        {
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    lockResource,
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                throw Invalid(
                    "PO_SOURCE_RESERVATION_BUSY",
                    "Another purchase order is currently reserving this approved source. Retry after that transaction completes.");
            }

            var current = await ResolveCoreAsync(
                source.SourceType,
                source.SourceId,
                source.BusinessPartnerId,
                purchaseOrderId,
                cancellationToken);
            if (!string.Equals(
                    current.SourceIntegrityHash,
                    source.SourceIntegrityHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw Invalid(
                    "PO_SOURCE_LINEAGE_CHANGED",
                    "The approved source changed before its purchase-order capacity could be reserved.");
            }

            EnsureOrderMatchesSource(current, lines, totalAmount, currencyCode);
            if (current.SourceType ==
                ProcurementPurchaseOrderSourceType.Contract)
            {
                await EnsureContractCapacityAsync(
                    current,
                    lines,
                    totalAmount,
                    currencyCode,
                    purchaseOrderId,
                    cancellationToken);
            }
            else
            {
                await EnsureOneTimeSourceAvailableAsync(
                    current.SourceType,
                    current.SourceId,
                    current.BusinessPartnerId,
                    purchaseOrderId,
                    cancellationToken);
            }

            await RecordAsync(
                current.SourceId,
                current.SourceReference,
                "SourceCapacityReserved",
                ProcurementControlEventResult.Allowed,
                correlationId,
                current.SourceType ==
                    ProcurementPurchaseOrderSourceType.Contract
                    ? "The proposed purchase order remains within the transactionally reserved contract line and value capacity."
                    : "The one-time approved source is transactionally reserved for this purchase order and supplier.",
                new
                {
                    current.SourceType,
                    current.SourceId,
                    current.BusinessPartnerId,
                    PurchaseOrderId = purchaseOrderId,
                    TotalAmount = totalAmount,
                    CurrencyCode = currencyCode
                },
                cancellationToken);
        }
        catch (ProcurementPurchaseOrderSourceValidationException exception)
        {
            await TryRecordDeniedAsync(
                source.SourceId,
                source.SourceReference,
                "SourceCapacityReservationDenied",
                correlationId,
                exception,
                new
                {
                    source.SourceType,
                    source.SourceId,
                    source.BusinessPartnerId,
                    PurchaseOrderId = purchaseOrderId,
                    TotalAmount = totalAmount,
                    CurrencyCode = currencyCode
                },
                cancellationToken);
            throw;
        }
    }

    public async Task EnsureBudgetCommitmentForIssueAsync(
        PurchaseOrder purchaseOrder,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!_unitOfWork.HasActiveTransaction)
            throw Invalid(
                "PO_BUDGET_TRANSACTION_REQUIRED",
                "The purchase-order budget commitment must be created inside the submission transaction.");

        var source = await EvaluateCurrentAsync(purchaseOrder, cancellationToken);
        await EnsureBudgetCommitmentAsync(
            source,
            purchaseOrder.TotalAmount,
            purchaseOrder.Currency,
            purchaseOrder.Id,
            correlationId,
            cancellationToken);
    }

    public async Task ClaimTenderAwardAsync(
        Guid tenderAwardId,
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken = default)
    {
        if (!_unitOfWork.HasActiveTransaction)
        {
            throw Invalid(
                "PO_SOURCE_CLAIM_TRANSACTION_REQUIRED",
                "Tender-award consumption must occur inside the purchase-order transaction.");
        }
        if (tenderAwardId == Guid.Empty ||
            purchaseOrder.TenantId != _currentUser.TenantId ||
            purchaseOrder.BusinessPartnerId == Guid.Empty)
        {
            throw Invalid(
                "PO_TENDER_AWARD_CLAIM_INVALID",
                "The tender-award claim does not match the purchase order tenant and supplier.");
        }

        var affected = await _unitOfWork.Repository<TenderAward>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == tenderAwardId &&
                item.BusinessPartnerId == purchaseOrder.BusinessPartnerId &&
                item.PurchaseOrderId == null &&
                !item.IsDeleted)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.PurchaseOrderId, purchaseOrder.Id)
                    .SetProperty(item => item.UpdatedAt, DateTime.UtcNow)
                    .SetProperty(item => item.LastModifiedById, _currentUser.UserId),
                cancellationToken);
        if (affected == 1)
            return;

        var existingOwner = await _unitOfWork.Repository<TenderAward>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == tenderAwardId && !item.IsDeleted)
            .AsNoTracking()
            .Select(item => new
            {
                item.BusinessPartnerId,
                item.PurchaseOrderId
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (existingOwner?.PurchaseOrderId == purchaseOrder.Id)
            return;

        throw Invalid(
            "PO_TENDER_AWARD_ALREADY_CONSUMED",
            existingOwner?.PurchaseOrderId.HasValue == true
                ? $"The tender award has already been consumed by purchase order {existingOwner.PurchaseOrderId}."
                : "The tender award is unavailable or does not match the purchase-order supplier.");
    }

    public async Task RecordBoundAsync(
        PurchaseOrder purchaseOrder,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!_unitOfWork.HasActiveTransaction)
        {
            throw Invalid(
                "PO_SOURCE_BOUND_TRANSACTION_REQUIRED",
                "Purchase-order source binding, audit, and notification outbox records must be committed atomically.");
        }

        await RecordAsync(
            purchaseOrder.Id,
            purchaseOrder.OrderNumber,
            NormalizeAction(action),
            ProcurementControlEventResult.Succeeded,
            correlationId,
            "The purchase order was bound to an immutable approved source snapshot.",
            new
            {
                purchaseOrder.ProcurementSourceType,
                purchaseOrder.ProcurementSourceId,
                purchaseOrder.ProcurementSourceReference,
                purchaseOrder.SourceRequisitionId,
                purchaseOrder.SourcingReleaseId,
                purchaseOrder.SourcingCaseId,
                purchaseOrder.AwardReadinessDecisionId,
                purchaseOrder.SourceIntegrityHash
            },
            cancellationToken);
        // NotificationTopicPublisher only appends durable Notification queue rows
        // through this same scoped unit of work. The dispatcher cannot observe
        // them unless the surrounding source-claim transaction commits.
        await PublishNotificationAsync(
            "procurement.purchase-order.source-bound",
            purchaseOrder,
            action,
            cancellationToken);
    }

    private Task<ProcurementPurchaseOrderSourceResolution> ResolveCoreAsync(
        ProcurementPurchaseOrderSourceType sourceType,
        Guid sourceId,
        Guid businessPartnerId,
        Guid? owningPurchaseOrderId,
        CancellationToken cancellationToken) =>
        sourceType switch
        {
            ProcurementPurchaseOrderSourceType.RfqAward =>
                ResolveRfqAsync(
                    sourceId,
                    businessPartnerId,
                    owningPurchaseOrderId,
                    cancellationToken),
            ProcurementPurchaseOrderSourceType.TenderAward =>
                ResolveTenderAwardAsync(
                    sourceId,
                    businessPartnerId,
                    owningPurchaseOrderId,
                    cancellationToken),
            ProcurementPurchaseOrderSourceType.Contract =>
                ResolveContractAsync(sourceId, businessPartnerId, cancellationToken),
            ProcurementPurchaseOrderSourceType.ApprovedException =>
                ResolveExceptionAsync(
                    sourceId,
                    businessPartnerId,
                    owningPurchaseOrderId,
                    cancellationToken),
            _ => throw Invalid(
                "PO_SOURCE_TYPE_INVALID",
                "The purchase-order source type is not supported.")
        };

    private async Task<ProcurementPurchaseOrderSourceResolution> ResolveRfqAsync(
        Guid rfqId,
        Guid businessPartnerId,
        Guid? owningPurchaseOrderId,
        CancellationToken cancellationToken)
    {
        var rfq = await _unitOfWork.Repository<RequestForQuotation>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == rfqId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_RFQ_SOURCE_NOT_FOUND",
                "The RFQ award source was not found in the current tenant.");
        if (!string.Equals(rfq.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(rfq.Status, "Awarded", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("PO_RFQ_SOURCE_NOT_APPROVED",
                $"The RFQ award source is not approved or awarded (current status: {rfq.Status}).");
        }
        await EnsureOneTimeSourceAvailableAsync(
            ProcurementPurchaseOrderSourceType.RfqAward,
            rfq.Id,
            businessPartnerId,
            owningPurchaseOrderId,
            cancellationToken);
        var link = await RequireSourceLinkAsync(
            rfq.SourcePurchaseRequisitionId, rfq.SourcingReleaseId,
            rfq.SourcingCaseId, cancellationToken);
        var readiness = await RequireCurrentReadinessAsync(
            ProcurementAwardReadinessSourceType.RequestForQuotation,
            rfq.Id,
            businessPartnerId,
            cancellationToken);
        var approvedLines = await ResolveRfqLinesAsync(
            rfq.Id,
            businessPartnerId,
            cancellationToken);
        var approvedAmount = approvedLines.Sum(item => item.LineTotal);
        return BuildResolution(
            ProcurementPurchaseOrderSourceType.RfqAward,
            rfq.Id,
            rfq.RfqNumber,
            link.Requisition,
            link.SourcingCase,
            readiness,
            businessPartnerId,
            rfq.Currency,
            new
            {
                SourceType = ProcurementPurchaseOrderSourceType.RfqAward,
                SourceId = rfq.Id,
                SourceReference = rfq.RfqNumber,
                rfq.Status,
                link.Requisition.Id,
                link.Requisition.RequisitionNumber,
                SourcingReleaseId = link.SourcingCase.SourcingReleaseId,
                SourcingCaseId = link.SourcingCase.Id,
                AwardReadinessDecisionId = readiness.Id,
                readiness.IntegrityHash,
                BusinessPartnerId = businessPartnerId,
                rfq.Currency,
                ApprovedAmount = approvedAmount,
                ApprovedLines = approvedLines.Select(line => new
                {
                    line.SourceLineId,
                    line.ItemCode,
                    line.Description,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.UnitPrice,
                    line.LineTotal
                })
            },
            approvedLines,
            approvedAmount);
    }

    private async Task<ProcurementPurchaseOrderSourceResolution> ResolveTenderAwardAsync(
        Guid awardId,
        Guid businessPartnerId,
        Guid? owningPurchaseOrderId,
        CancellationToken cancellationToken)
    {
        var award = await _unitOfWork.Repository<TenderAward>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == awardId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_TENDER_AWARD_NOT_FOUND",
                "The tender award was not found in the current tenant.");
        if (award.BusinessPartnerId != businessPartnerId)
            throw Invalid("PO_SOURCE_SUPPLIER_MISMATCH",
                "The selected supplier is not the awarded supplier.");
        if (award.PurchaseOrderId.HasValue &&
            award.PurchaseOrderId.Value != owningPurchaseOrderId)
        {
            throw Invalid(
                "PO_TENDER_AWARD_ALREADY_CONSUMED",
                $"The tender award has already been consumed by purchase order {award.PurchaseOrderId}.");
        }
        if (!string.Equals(award.Status, "Awarded", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(award.Status, "ContractSigned", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("PO_TENDER_AWARD_NOT_APPROVED",
                $"The tender award is not usable (current status: {award.Status}).");
        }
        await EnsureOneTimeSourceAvailableAsync(
            ProcurementPurchaseOrderSourceType.TenderAward,
            award.Id,
            businessPartnerId,
            owningPurchaseOrderId,
            cancellationToken);
        var tender = await RequireTenderAsync(award.TenderId, cancellationToken);
        var link = await RequireSourceLinkAsync(
            tender.SourcePurchaseRequisitionId, tender.SourcingReleaseId,
            tender.SourcingCaseId, cancellationToken);
        var readiness = await RequireCurrentReadinessAsync(
            ProcurementAwardReadinessSourceType.Tender,
            tender.Id,
            businessPartnerId,
            cancellationToken);
        var approvedLines = await ResolveTenderBidLinesAsync(
            award.TenderBidId,
            award.BidLotId,
            award.NegotiationId,
            cancellationToken);
        return BuildResolution(
            ProcurementPurchaseOrderSourceType.TenderAward,
            award.Id,
            $"{tender.TenderNumber}/{award.Id:N}",
            link.Requisition,
            link.SourcingCase,
            readiness,
            businessPartnerId,
            award.Currency ?? tender.Currency ?? "USD",
            new
            {
                SourceType = ProcurementPurchaseOrderSourceType.TenderAward,
                SourceId = award.Id,
                SourceReference = tender.TenderNumber,
                award.TenderId,
                award.TenderBidId,
                award.Status,
                award.AwardedAmount,
                link.Requisition.Id,
                link.Requisition.RequisitionNumber,
                SourcingReleaseId = link.SourcingCase.SourcingReleaseId,
                SourcingCaseId = link.SourcingCase.Id,
                AwardReadinessDecisionId = readiness.Id,
                readiness.IntegrityHash,
                award.BusinessPartnerId,
                award.Currency,
                ApprovedLines = approvedLines.Select(line => new
                {
                    line.SourceLineId,
                    line.ItemCode,
                    line.Description,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.UnitPrice,
                    line.LineTotal
                })
            },
            approvedLines,
            award.AwardedAmount);
    }

    private async Task<ProcurementPurchaseOrderSourceResolution> ResolveContractAsync(
        Guid contractId,
        Guid businessPartnerId,
        CancellationToken cancellationToken)
    {
        var contract = await _unitOfWork.Repository<Contract>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == contractId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_CONTRACT_SOURCE_NOT_FOUND",
                "The contract source was not found in the current tenant.");
        if (contract.BusinessPartnerId != businessPartnerId)
            throw Invalid("PO_SOURCE_SUPPLIER_MISMATCH",
                "The selected supplier is not the contract supplier.");
        if (!string.Equals(contract.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw Invalid("PO_CONTRACT_NOT_ACTIVE",
                $"Only an active contract can authorize a purchase order (current status: {contract.Status}).");
        var effectivePeriod =
            ProcurementPurchaseOrderSourceRules.ValidateContractEffectivePeriod(
                contract.StartDate,
                contract.EndDate,
                DateTime.UtcNow);
        if (!effectivePeriod.IsValid)
            throw Invalid(effectivePeriod.Code, effectivePeriod.Message);
        var award = await _unitOfWork.Repository<TenderAward>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == contract.TenderAwardId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_CONTRACT_AWARD_NOT_FOUND",
                "The contract's tender award was not found.");
        var tender = await RequireTenderAsync(contract.TenderId, cancellationToken);
        var link = await RequireSourceLinkAsync(
            tender.SourcePurchaseRequisitionId, tender.SourcingReleaseId,
            tender.SourcingCaseId, cancellationToken);
        var readiness = await RequireCurrentReadinessAsync(
            ProcurementAwardReadinessSourceType.Tender,
            tender.Id,
            businessPartnerId,
            cancellationToken);
        var approvedLines = await ResolveTenderBidLinesAsync(
            award.TenderBidId,
            award.BidLotId,
            award.NegotiationId,
            cancellationToken);
        return BuildResolution(
            ProcurementPurchaseOrderSourceType.Contract,
            contract.Id,
            contract.ContractNumber,
            link.Requisition,
            link.SourcingCase,
            readiness,
            businessPartnerId,
            contract.Currency,
            new
            {
                SourceType = ProcurementPurchaseOrderSourceType.Contract,
                SourceId = contract.Id,
                SourceReference = contract.ContractNumber,
                contract.Status,
                contract.TenderAwardId,
                contract.TenderId,
                contract.BusinessPartnerId,
                contract.ContractValue,
                contract.Currency,
                contract.StartDate,
                contract.EndDate,
                AwardStatus = award.Status,
                link.Requisition.Id,
                link.Requisition.RequisitionNumber,
                SourcingReleaseId = link.SourcingCase.SourcingReleaseId,
                SourcingCaseId = link.SourcingCase.Id,
                AwardReadinessDecisionId = readiness.Id,
                readiness.IntegrityHash,
                ApprovedLines = approvedLines.Select(line => new
                {
                    line.SourceLineId,
                    line.ItemCode,
                    line.Description,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.UnitPrice,
                    line.LineTotal
                })
            },
            approvedLines,
            contract.ContractValue);
    }

    private async Task<ProcurementPurchaseOrderSourceResolution> ResolveExceptionAsync(
        Guid controlId,
        Guid businessPartnerId,
        Guid? owningPurchaseOrderId,
        CancellationToken cancellationToken)
    {
        var control = await _unitOfWork.Repository<ProcurementExceptionalSourcingControl>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == controlId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_EXCEPTION_SOURCE_NOT_FOUND",
                "The approved-exception sourcing control was not found in the current tenant.");
        if (control.Status < ProcurementExceptionalSourcingControlStatus.Awarded ||
            control.Status > ProcurementExceptionalSourcingControlStatus.Filed ||
            !control.AwardBidId.HasValue)
        {
            throw Invalid("PO_EXCEPTION_SOURCE_NOT_AWARDED",
                $"The approved-exception source has not reached an awarded outcome (current status: {control.Status}).");
        }
        var bid = await _unitOfWork.Repository<TenderBid>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == control.AwardBidId.Value && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_EXCEPTION_AWARD_BID_NOT_FOUND",
                "The approved-exception award bid was not found.");
        if (bid.BusinessPartnerId != businessPartnerId)
            throw Invalid("PO_SOURCE_SUPPLIER_MISMATCH",
                "The selected supplier is not the approved-exception awarded supplier.");
        await EnsureOneTimeSourceAvailableAsync(
            ProcurementPurchaseOrderSourceType.ApprovedException,
            control.Id,
            businessPartnerId,
            owningPurchaseOrderId,
            cancellationToken);
        var tender = await RequireTenderAsync(control.TenderId, cancellationToken);
        var link = await RequireSourceLinkAsync(
            tender.SourcePurchaseRequisitionId, tender.SourcingReleaseId,
            control.SourcingCaseId, cancellationToken);
        var readiness = await RequireCurrentReadinessAsync(
            ProcurementAwardReadinessSourceType.ExceptionalSourcing,
            ProcurementPurchaseOrderSourceRules.ResolveAwardReadinessSourceId(
                ProcurementPurchaseOrderSourceType.ApprovedException,
                control.Id,
                control.TenderId),
            businessPartnerId,
            cancellationToken);
        var approvedLines = await ResolveTenderBidLinesAsync(
            bid.Id,
            null,
            control.NegotiationId,
            cancellationToken);
        var approvedAmount = control.NegotiatedAmount ?? bid.TotalBidAmount;
        return BuildResolution(
            ProcurementPurchaseOrderSourceType.ApprovedException,
            control.Id,
            control.AwardReference ?? $"EXC-{control.Id:N}",
            link.Requisition,
            link.SourcingCase,
            readiness,
            businessPartnerId,
            bid.Currency ?? tender.Currency ?? "USD",
            new
            {
                SourceType = ProcurementPurchaseOrderSourceType.ApprovedException,
                SourceId = control.Id,
                SourceReference = control.AwardReference,
                control.Status,
                control.TenderId,
                control.SourcingCaseId,
                control.ExceptionRuleId,
                control.ExceptionRuleCode,
                control.WorkflowInstanceId,
                control.ApprovedAtUtc,
                control.AwardBidId,
                control.AwardReference,
                control.AwardEvidenceReference,
                control.AwardedAtUtc,
                link.Requisition.Id,
                link.Requisition.RequisitionNumber,
                SourcingReleaseId = link.SourcingCase.SourcingReleaseId,
                AwardReadinessDecisionId = readiness.Id,
                readiness.IntegrityHash,
                bid.BusinessPartnerId,
                bid.Currency,
                ApprovedAmount = approvedAmount,
                ApprovedLines = approvedLines
            },
            approvedLines,
            approvedAmount);
    }

    private async Task<IReadOnlyList<ProcurementPurchaseOrderSourceLineDto>>
        ResolveRfqLinesAsync(
            Guid rfqId,
            Guid businessPartnerId,
            CancellationToken cancellationToken)
    {
        var awardLines = await _unitOfWork
            .Repository<RequestForQuotationAwardLine>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.RfqId == rfqId &&
                item.BusinessPartnerId == businessPartnerId &&
                !item.IsDeleted)
            .AsNoTracking()
            .OrderBy(item => item.RfqItemId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (awardLines.Count == 0)
        {
            throw Invalid(
                "PO_RFQ_AWARD_LINES_NOT_FOUND",
                "The RFQ has no awarded commercial lines for the selected supplier.");
        }

        var itemIds = awardLines.Select(item => item.RfqItemId).Distinct().ToList();
        var rfqItems = await _unitOfWork.Repository<RequestForQuotationItem>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.RfqId == rfqId &&
                itemIds.Contains(item.Id) &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (rfqItems.Count != itemIds.Count)
        {
            throw Invalid(
                "PO_RFQ_AWARD_LINEAGE_INCOMPLETE",
                "One or more RFQ award lines no longer identify an authoritative RFQ item.");
        }

        return awardLines.Select(award =>
        {
            var item = rfqItems[award.RfqItemId];
            return new ProcurementPurchaseOrderSourceLineDto
            {
                SourceLineId = award.Id,
                InventoryItemId = item.InventoryItemId,
                ItemCode = item.ItemCode ?? string.Empty,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitOfMeasure = string.IsNullOrWhiteSpace(item.UnitOfMeasure)
                    ? "EA"
                    : item.UnitOfMeasure.Trim(),
                UnitPrice = award.UnitPrice,
                LineTotal = award.LineTotal
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<ProcurementPurchaseOrderSourceLineDto>>
        ResolveTenderBidLinesAsync(
            Guid tenderBidId,
            Guid? bidLotId,
            Guid? negotiationId,
            CancellationToken cancellationToken)
    {
        var bidItemsQuery = _unitOfWork.Repository<TenderBidItem>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderBidId == tenderBidId &&
                !item.IsDeleted);
        if (bidLotId.HasValue)
            bidItemsQuery = bidItemsQuery.Where(item => item.BidLotId == bidLotId.Value);

        var bidItems = await bidItemsQuery
            .AsNoTracking()
            .OrderBy(item => item.TenderItemId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (bidItems.Count == 0)
        {
            throw Invalid(
                "PO_TENDER_AWARD_LINES_NOT_FOUND",
                "The awarded tender bid has no authoritative commercial lines.");
        }

        var tenderItemIds = bidItems.Select(item => item.TenderItemId).Distinct().ToList();
        var tenderItems = await _unitOfWork.Repository<TenderItem>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                tenderItemIds.Contains(item.Id) &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (tenderItems.Count != tenderItemIds.Count)
        {
            throw Invalid(
                "PO_TENDER_AWARD_LINEAGE_INCOMPLETE",
                "One or more awarded bid lines no longer identify an authoritative tender item.");
        }

        var negotiationByBidItem = new Dictionary<Guid, TenderNegotiationItem>();
        if (negotiationId.HasValue)
        {
            negotiationByBidItem = await _unitOfWork
                .Repository<TenderNegotiationItem>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.NegotiationId == negotiationId.Value &&
                    !item.IsDeleted)
                .AsNoTracking()
                .ToDictionaryAsync(
                    item => item.TenderBidItemId,
                    cancellationToken);
        }

        return bidItems.Select(bidItem =>
        {
            var tenderItem = tenderItems[bidItem.TenderItemId];
            negotiationByBidItem.TryGetValue(bidItem.Id, out var negotiation);
            var quantity = negotiation?.Quantity > 0
                ? negotiation.Quantity
                : bidItem.OfferedQuantity;
            var unitPrice =
                negotiation?.NegotiatedUnitPrice ?? bidItem.UnitPrice;
            var lineTotal =
                negotiation?.NegotiatedTotalPrice ??
                decimal.Round(
                    quantity * unitPrice,
                    2,
                    MidpointRounding.AwayFromZero);
            return new ProcurementPurchaseOrderSourceLineDto
            {
                SourceLineId = bidItem.Id,
                // Tender lines do not own a stable inventory-item FK. Item-code
                // lookups can change after award and must not alter the approved
                // source identity or its immutable snapshot.
                InventoryItemId = null,
                ItemCode = tenderItem.ItemCode ?? string.Empty,
                Description = tenderItem.Description,
                Quantity = quantity,
                UnitOfMeasure = string.IsNullOrWhiteSpace(tenderItem.UnitOfMeasure)
                    ? "EA"
                    : tenderItem.UnitOfMeasure.Trim(),
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            };
        }).ToList();
    }

    private async Task AddTenderAwardOptionsAsync(
        ICollection<ProcurementPurchaseOrderSourceOptionDto> result,
        Guid? requisitionId,
        CancellationToken cancellationToken)
    {
        var awardsQuery = _unitOfWork.Repository<TenderAward>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.PurchaseOrderId == null &&
                (item.Status == "Awarded" || item.Status == "ContractSigned"));
        if (requisitionId.HasValue)
        {
            awardsQuery = awardsQuery.Where(item =>
                item.Tender.TenantId == _currentUser.TenantId &&
                !item.Tender.IsDeleted &&
                item.Tender.SourcePurchaseRequisitionId == requisitionId.Value);
        }
        var awards = await awardsQuery
            .AsNoTracking().ToListAsync(cancellationToken);
        var supplierNames = await LoadSupplierNamesAsync(
            awards.Select(item => item.BusinessPartnerId),
            cancellationToken);
        foreach (var award in awards)
        {
            try
            {
                var resolution = await ResolveTenderAwardAsync(
                    award.Id, award.BusinessPartnerId, null, cancellationToken);
                if (requisitionId.HasValue &&
                    resolution.PurchaseRequisitionId != requisitionId.Value)
                    continue;
                result.Add(MapOption(
                    resolution,
                    award.AwardedAmount,
                    supplierNames));
            }
            catch (ProcurementPurchaseOrderSourceValidationException)
            {
                // Fail closed: invalid/stale sources are intentionally omitted.
            }
        }
    }

    private async Task AddContractOptionsAsync(
        ICollection<ProcurementPurchaseOrderSourceOptionDto> result,
        Guid? requisitionId,
        CancellationToken cancellationToken)
    {
        var contractsQuery = _unitOfWork.Repository<Contract>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.Status == "Active");
        if (requisitionId.HasValue)
        {
            contractsQuery = contractsQuery.Where(item =>
                item.Tender.TenantId == _currentUser.TenantId &&
                !item.Tender.IsDeleted &&
                item.Tender.SourcePurchaseRequisitionId == requisitionId.Value);
        }
        var contracts = await contractsQuery
            .AsNoTracking().ToListAsync(cancellationToken);
        var supplierNames = await LoadSupplierNamesAsync(
            contracts.Select(item => item.BusinessPartnerId),
            cancellationToken);
        foreach (var contract in contracts)
        {
            try
            {
                var resolution = await ResolveContractAsync(
                    contract.Id, contract.BusinessPartnerId, cancellationToken);
                if (requisitionId.HasValue &&
                    resolution.PurchaseRequisitionId != requisitionId.Value)
                    continue;
                var committedAmount = await PurchaseOrders
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.ProcurementSourceType ==
                            ProcurementPurchaseOrderSourceType.Contract &&
                        item.ProcurementSourceId == contract.Id &&
                        item.BusinessPartnerId == contract.BusinessPartnerId &&
                        !item.IsDeleted &&
                        item.Status != "Cancelled" &&
                        item.Status != "Rejected")
                    .SumAsync(item => item.TotalAmount, cancellationToken);
                var remainingAmount = Math.Max(
                    0m,
                    contract.ContractValue - committedAmount);
                if (remainingAmount <= 0)
                    continue;
                result.Add(MapOption(
                    resolution,
                    remainingAmount,
                    supplierNames));
            }
            catch (ProcurementPurchaseOrderSourceValidationException)
            {
                // Fail closed: invalid/stale sources are intentionally omitted.
            }
        }
    }

    private async Task AddExceptionOptionsAsync(
        ICollection<ProcurementPurchaseOrderSourceOptionDto> result,
        Guid? requisitionId,
        CancellationToken cancellationToken)
    {
        var controlsQuery = _unitOfWork
            .Repository<ProcurementExceptionalSourcingControl>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.Status >= ProcurementExceptionalSourcingControlStatus.Awarded &&
                item.Status <= ProcurementExceptionalSourcingControlStatus.Filed &&
                item.AwardBidId != null);
        if (requisitionId.HasValue)
        {
            controlsQuery = controlsQuery.Where(item =>
                item.Tender.TenantId == _currentUser.TenantId &&
                !item.Tender.IsDeleted &&
                item.Tender.SourcePurchaseRequisitionId == requisitionId.Value);
        }
        var controls = await controlsQuery
            .AsNoTracking().ToListAsync(cancellationToken);
        var awardBidIds = controls
            .Select(item => item.AwardBidId!.Value)
            .Distinct()
            .ToArray();
        var bids = await _unitOfWork.Repository<TenderBid>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                awardBidIds.Contains(item.Id) &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var supplierNames = await LoadSupplierNamesAsync(
            bids.Values.Select(item => item.BusinessPartnerId),
            cancellationToken);
        foreach (var control in controls)
        {
            if (!bids.TryGetValue(control.AwardBidId!.Value, out var bid))
                continue;
            try
            {
                var resolution = await ResolveExceptionAsync(
                    control.Id,
                    bid.BusinessPartnerId,
                    null,
                    cancellationToken);
                if (requisitionId.HasValue &&
                    resolution.PurchaseRequisitionId != requisitionId.Value)
                    continue;
                result.Add(MapOption(
                    resolution,
                    control.NegotiatedAmount,
                    supplierNames));
            }
            catch (ProcurementPurchaseOrderSourceValidationException)
            {
                // Fail closed: invalid/stale sources are intentionally omitted.
            }
        }
    }

    private static ProcurementPurchaseOrderSourceOptionDto MapOption(
        ProcurementPurchaseOrderSourceResolution source,
        decimal? approvedAmount,
        IReadOnlyDictionary<Guid, string> supplierNames)
    {
        var supplierName = supplierNames.GetValueOrDefault(
            source.BusinessPartnerId,
            "Supplier");
        return new ProcurementPurchaseOrderSourceOptionDto
        {
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            SourceReference = source.SourceReference,
            SourceLabel = $"{source.SourceType}: {source.SourceReference}",
            PurchaseRequisitionId = source.PurchaseRequisitionId,
            PurchaseRequisitionNumber = source.PurchaseRequisitionNumber,
            SourcingCaseId = source.SourcingCaseId,
            SourcingReleaseId = source.SourcingReleaseId,
            AwardReadinessDecisionId = source.AwardReadinessDecisionId,
            BusinessPartnerId = source.BusinessPartnerId,
            BusinessPartnerName = supplierName,
            ApprovedAmount = approvedAmount,
            CurrencyCode = source.CurrencyCode,
            ApprovedLines = source.ApprovedLines
        };
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadSupplierNamesAsync(
        IEnumerable<Guid> supplierIds,
        CancellationToken cancellationToken)
    {
        var ids = supplierIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, string>();

        return await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                ids.Contains(item.Id) &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToDictionaryAsync(
                item => item.Id,
                item => item.PartnerName,
                cancellationToken);
    }

    private async Task<(PurchaseRequisition Requisition, ProcurementSourcingCase SourcingCase)>
        RequireSourceLinkAsync(
            Guid? requisitionId,
            Guid? sourcingReleaseId,
            Guid? sourcingCaseId,
            CancellationToken cancellationToken)
    {
        if (!requisitionId.HasValue || !sourcingReleaseId.HasValue ||
            !sourcingCaseId.HasValue)
        {
            throw Invalid("PO_SOURCE_LINEAGE_INCOMPLETE",
                "The approved source does not identify its requisition, sourcing release, and sourcing case.");
        }
        var requisition = await RequireApprovedRequisitionAsync(
            requisitionId.Value, cancellationToken);
        var sourcingCase = await SourcingCases.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == sourcingCaseId.Value &&
                item.PurchaseRequisitionId == requisition.Id &&
                item.SourcingReleaseId == sourcingReleaseId.Value &&
                !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_SOURCING_CASE_NOT_FOUND",
                "The source sourcing case was not found or does not match the requisition release.");
        EnsureSourcingCaseUsable(sourcingCase);
        var releaseExists = await SourcingReleases.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == sourcingCase.SourcingReleaseId &&
                item.PurchaseRequisitionId == requisition.Id &&
                !item.IsDeleted)
            .AsNoTracking().AnyAsync(cancellationToken);
        if (!releaseExists)
            throw Invalid("PO_SOURCING_RELEASE_NOT_FOUND",
                "The immutable sourcing release was not found.");
        return (requisition, sourcingCase);
    }

    private async Task EnsureBudgetCommitmentAsync(
        ProcurementPurchaseOrderSourceResolution source,
        decimal totalAmount,
        string? currencyCode,
        Guid purchaseOrderId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var release = await SourcingReleases.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == source.SourcingReleaseId &&
                item.PurchaseRequisitionId == source.PurchaseRequisitionId &&
                !item.IsDeleted)
            .Include(item => item.BudgetCommitment)
                .ThenInclude(item => item.ProcurementBudget)
            .Include(item => item.PurchaseRequisition)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_BUDGET_SOURCING_RELEASE_NOT_FOUND",
                "The approved sourcing release and budget commitment were not found in the current tenant.");
        var commitment = release.BudgetCommitment;
        var budget = commitment?.ProcurementBudget;
        if (commitment is null || budget is null)
        {
            var readiness = await _budgetControl.ReserveForDownstreamAsync(
                release.PurchaseRequisition,
                Permission,
                correlationId,
                cancellationToken);
            if (!readiness.IsCompliant || !readiness.CommitmentId.HasValue)
                throw Invalid(
                    readiness.DecisionCode,
                    readiness.Message);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            commitment = await _unitOfWork.Repository<ProcurementBudgetCommitment>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == readiness.CommitmentId.Value &&
                    !item.IsDeleted)
                .Include(item => item.ProcurementBudget)
                .SingleOrDefaultAsync(cancellationToken);
            budget = commitment?.ProcurementBudget;
        }
        if (commitment is null || budget is null)
            throw Invalid("PO_BUDGET_COMMITMENT_NOT_FOUND",
                "The approved sourcing release has no authoritative budget commitment.");

        var priorExposure = await PurchaseOrders.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.SourceRequisitionId == source.PurchaseRequisitionId &&
                item.Id != purchaseOrderId &&
                !item.IsDeleted &&
                item.Status != "Cancelled" &&
                item.Status != "Rejected")
            .Select(item => (decimal?)item.TotalAmount)
            .SumAsync(cancellationToken) ?? 0m;
        var requiredExposure = decimal.Round(
            priorExposure + totalAmount, 2, MidpointRounding.AwayFromZero);
        var result = ProcurementPurchaseOrderComplianceRules.ValidateCommitment(
            new ProcurementCommitmentLifecycleSnapshot(
                _currentUser.TenantId,
                release.PurchaseRequisition.Id,
                release.PurchaseRequisition.TenantId,
                release.PurchaseRequisition.Currency,
                release.PurchaseRequisition.BudgetId,
                release.TenantId,
                release.PurchaseRequisitionId,
                release.BudgetCommitmentId,
                release.BudgetCommitmentReference,
                commitment.Id,
                commitment.TenantId,
                commitment.PurchaseRequisitionId,
                commitment.ProcurementBudgetId,
                commitment.ReservationReference,
                commitment.Status,
                commitment.ReservedAmount,
                commitment.Currency,
                budget.Id,
                budget.TenantId,
                budget.Status,
                budget.Currency,
                budget.CommittedAmount,
                budget.ApprovedById,
                budget.ApprovedDate,
                budget.EffectiveDate,
                budget.ExpiryDate,
                requiredExposure,
                currencyCode ?? source.CurrencyCode,
                DateTime.UtcNow));
        if (!result.IsValid)
            throw Invalid(result.Code, result.Message);
    }

    private async Task<(PurchaseRequisition Requisition, ProcurementSourcingCase SourcingCase)>
        ResolveSourceLinkAsync(
            ProcurementAwardReadinessSourceType sourceType,
            Guid sourceId,
            CancellationToken cancellationToken)
    {
        return sourceType switch
        {
            ProcurementAwardReadinessSourceType.RequestForQuotation =>
                await ResolveRfqSourceLinkAsync(sourceId, cancellationToken),
            ProcurementAwardReadinessSourceType.Tender =>
                await ResolveTenderSourceLinkAsync(sourceId, cancellationToken),
            ProcurementAwardReadinessSourceType.ExceptionalSourcing =>
                await ResolveExceptionSourceLinkAsync(sourceId, cancellationToken),
            _ => throw Invalid("PO_SOURCE_TYPE_INVALID",
                "The award-readiness source type is not supported.")
        };
    }

    private async Task<(PurchaseRequisition Requisition, ProcurementSourcingCase SourcingCase)>
        ResolveRfqSourceLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await _unitOfWork.Repository<RequestForQuotation>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.Id == id && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_RFQ_SOURCE_NOT_FOUND", "The RFQ source was not found.");
        return await RequireSourceLinkAsync(
            source.SourcePurchaseRequisitionId, source.SourcingReleaseId,
            source.SourcingCaseId, cancellationToken);
    }

    private async Task<(PurchaseRequisition Requisition, ProcurementSourcingCase SourcingCase)>
        ResolveTenderSourceLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await RequireTenderAsync(id, cancellationToken);
        return await RequireSourceLinkAsync(
            source.SourcePurchaseRequisitionId, source.SourcingReleaseId,
            source.SourcingCaseId, cancellationToken);
    }

    private async Task<(PurchaseRequisition Requisition, ProcurementSourcingCase SourcingCase)>
        ResolveExceptionSourceLinkAsync(
            Guid tenderId,
            CancellationToken cancellationToken)
    {
        var control = await _unitOfWork.Repository<ProcurementExceptionalSourcingControl>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.TenderId == tenderId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_EXCEPTION_SOURCE_NOT_FOUND",
                "The approved-exception source was not found.");
        var tender = await RequireTenderAsync(control.TenderId, cancellationToken);
        return await RequireSourceLinkAsync(
            tender.SourcePurchaseRequisitionId, tender.SourcingReleaseId,
            control.SourcingCaseId, cancellationToken);
    }

    private async Task<PurchaseRequisition> RequireApprovedRequisitionAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var requisition = await Requisitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == id && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_SOURCE_REQUISITION_NOT_FOUND",
                "The source purchase requisition was not found in the current tenant.");
        if (!ProcurementPurchaseOrderSourceRules.IsApprovedRequisitionStatus(
                requisition.Status))
        {
            throw Invalid("PO_SOURCE_REQUISITION_NOT_APPROVED",
                $"The source purchase requisition is not approved (current status: {requisition.Status}).");
        }
        return requisition;
    }

    private async Task<Tender> RequireTenderAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await _unitOfWork.Repository<Tender>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.Id == id && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
        ?? throw Invalid("PO_TENDER_SOURCE_NOT_FOUND",
            "The tender source was not found in the current tenant.");

    private async Task<ProcurementAwardReadinessDecision>
        RequireCurrentReadinessAsync(
            ProcurementAwardReadinessSourceType sourceType,
            Guid sourceId,
            Guid businessPartnerId,
            CancellationToken cancellationToken)
    {
        var readiness = await ReadinessDecisions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.SourceType == sourceType &&
                item.SourceId == sourceId && !item.IsDeleted)
            .OrderByDescending(item => item.DecisionSequence)
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw Invalid("PO_SOURCE_READINESS_NOT_FOUND",
                "No award-readiness decision exists for the selected source.");
        EnsureReady(readiness);
        if (!ProcurementPurchaseOrderSourceRules.ContainsApprovedSupplier(
                readiness.RecommendedBusinessPartnerIdsJson, businessPartnerId))
        {
            throw Invalid("PO_SOURCE_SUPPLIER_NOT_RECOMMENDED",
                "The selected supplier is not part of the approved award-readiness decision.");
        }
        return readiness;
    }

    private static void EnsureReady(ProcurementAwardReadinessDecision readiness)
    {
        if (readiness.Status != ProcurementAwardReadinessDecisionStatus.Ready)
            throw Invalid("PO_SOURCE_READINESS_BLOCKED",
                "The latest award-readiness decision does not permit purchase-order creation.");
    }

    private static void EnsureSourcingCaseUsable(ProcurementSourcingCase sourcingCase)
    {
        if (sourcingCase.Status == ProcurementSourcingCaseStatus.Cancelled)
            throw Invalid("PO_SOURCING_CASE_CANCELLED",
                "A cancelled sourcing case cannot authorize a purchase order.");
    }

    private static ProcurementPurchaseOrderSourceResolution BuildResolution(
        ProcurementPurchaseOrderSourceType sourceType,
        Guid sourceId,
        string sourceReference,
        PurchaseRequisition requisition,
        ProcurementSourcingCase sourcingCase,
        ProcurementAwardReadinessDecision readiness,
        Guid businessPartnerId,
        string currencyCode,
        object snapshot,
        IReadOnlyList<ProcurementPurchaseOrderSourceLineDto>? approvedLines = null,
        decimal? approvedAmount = null)
    {
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        return new ProcurementPurchaseOrderSourceResolution
        {
            SourceType = sourceType,
            SourceId = sourceId,
            SourceReference = sourceReference,
            PurchaseRequisitionId = requisition.Id,
            PurchaseRequisitionNumber = requisition.RequisitionNumber,
            PurchaseRequisitionRequestedById = requisition.RequestedById,
            SourcingCaseId = sourcingCase.Id,
            SourcingReleaseId = sourcingCase.SourcingReleaseId,
            AwardReadinessDecisionId = readiness.Id,
            BusinessPartnerId = businessPartnerId,
            ProcurementCategory = requisition.ProcurementCategory ??
                throw Invalid("PO_SOURCE_CATEGORY_REQUIRED",
                    "The approved requisition has no governed procurement category."),
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode)
                ? "USD"
                : currencyCode.Trim().ToUpperInvariant(),
            ApprovedAmount = approvedAmount,
            ApprovedLines = approvedLines ?? [],
            SourceSnapshotJson = json,
            SourceIntegrityHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant(),
            ValidatedAtUtc = DateTime.UtcNow
        };
    }

    private static void EnsureOrderMatchesSource(
        ProcurementPurchaseOrderSourceResolution source,
        IReadOnlyCollection<ProcurementPurchaseOrderSourceOrderLine> lines,
        decimal totalAmount,
        string? currencyCode)
    {
        var result = ProcurementPurchaseOrderSourceRules.ValidateOrder(
            source.SourceType,
            source.ApprovedLines,
            lines,
            source.ApprovedAmount,
            totalAmount,
            source.CurrencyCode,
            currencyCode);
        if (!result.IsValid)
            throw Invalid(result.Code, result.Message);
    }

    private async Task EnsureOneTimeSourceAvailableAsync(
        ProcurementPurchaseOrderSourceType sourceType,
        Guid sourceId,
        Guid businessPartnerId,
        Guid? owningPurchaseOrderId,
        CancellationToken cancellationToken)
    {
        if (!ProcurementPurchaseOrderSourceRules.IsOneTime(sourceType))
        {
            return;
        }

        // A one-time award remains consumed even when its purchase order is
        // soft-deleted. Keep this lookup aligned with the unfiltered unique
        // database index so reuse fails as a controlled domain outcome rather
        // than surfacing as a duplicate-key exception during SaveChanges.
        var existing = await PurchaseOrders.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProcurementSourceType == sourceType &&
                item.ProcurementSourceId == sourceId &&
                item.BusinessPartnerId == businessPartnerId &&
                (!owningPurchaseOrderId.HasValue ||
                    item.Id != owningPurchaseOrderId.Value))
            .AsNoTracking()
            .OrderBy(item => item.CreatedAt)
            .Select(item => new { item.Id, item.OrderNumber })
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            throw Invalid(
                "PO_ONE_TIME_SOURCE_ALREADY_CONSUMED",
                $"The {sourceType} source has already been consumed by purchase order {existing.OrderNumber} ({existing.Id}) for this supplier.");
        }

        var awardReservation = await ResolveAwardReservationAsync(
            sourceType,
            sourceId,
            businessPartnerId,
            cancellationToken);
        var framework = await _unitOfWork
            .Repository<ProcurementFrameworkAgreement>()
            .GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId &&
                item.SourceType == awardReservation.SourceType &&
                item.SourceId == awardReservation.SourceId &&
                item.BusinessPartnerId == businessPartnerId)
            .AsNoTracking()
            .OrderBy(item => item.CreatedAt)
            .Select(item => new { item.Id, item.AgreementNumber })
            .FirstOrDefaultAsync(cancellationToken);
        if (framework is not null)
        {
            throw Invalid(
                "PO_ONE_TIME_SOURCE_RESERVED_FOR_FRAMEWORK",
                $"The {sourceType} award is permanently reserved by framework agreement {framework.AgreementNumber} ({framework.Id}) for this supplier.");
        }
    }

    private async Task<AwardReservation> ResolveAwardReservationAsync(
        ProcurementPurchaseOrderSourceType sourceType,
        Guid sourceId,
        Guid businessPartnerId,
        CancellationToken cancellationToken)
    {
        if (sourceType == ProcurementPurchaseOrderSourceType.RfqAward)
        {
            return new AwardReservation(
                ProcurementAwardReadinessSourceType.RequestForQuotation,
                sourceId);
        }

        if (sourceType == ProcurementPurchaseOrderSourceType.TenderAward)
        {
            var award = await _unitOfWork.Repository<TenderAward>()
                .GetQueryableIncludingDeleted(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == sourceId)
                .AsNoTracking()
                .Select(item => new
                {
                    item.TenderId,
                    item.BusinessPartnerId
                })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw Invalid(
                    "PO_TENDER_AWARD_NOT_FOUND",
                    "The tender award was not found in the current tenant.");
            if (award.BusinessPartnerId != businessPartnerId)
            {
                throw Invalid(
                    "PO_SOURCE_SUPPLIER_MISMATCH",
                    "The selected supplier is not the awarded supplier.");
            }

            return new AwardReservation(
                ProcurementAwardReadinessSourceType.Tender,
                award.TenderId);
        }

        if (sourceType == ProcurementPurchaseOrderSourceType.ApprovedException)
        {
            var tenderId = await _unitOfWork
                .Repository<ProcurementExceptionalSourcingControl>()
                .GetQueryableIncludingDeleted(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == sourceId)
                .AsNoTracking()
                .Select(item => (Guid?)item.TenderId)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw Invalid(
                    "PO_EXCEPTION_SOURCE_NOT_FOUND",
                    "The approved-exception sourcing control was not found in the current tenant.");
            return new AwardReservation(
                ProcurementAwardReadinessSourceType.ExceptionalSourcing,
                tenderId);
        }

        throw Invalid(
            "PO_SOURCE_TYPE_INVALID",
            "The purchase-order source is not a one-time award.");
    }

    private async Task EnsureContractCapacityAsync(
        ProcurementPurchaseOrderSourceResolution source,
        IReadOnlyCollection<ProcurementPurchaseOrderSourceOrderLine> proposedLines,
        decimal proposedTotalAmount,
        string? currencyCode,
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var existingOrders = await PurchaseOrders.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ProcurementSourceType ==
                    ProcurementPurchaseOrderSourceType.Contract &&
                item.ProcurementSourceId == source.SourceId &&
                item.BusinessPartnerId == source.BusinessPartnerId &&
                item.Id != purchaseOrderId &&
                !item.IsDeleted &&
                item.Status != "Cancelled" &&
                item.Status != "Rejected")
            .AsNoTracking()
            .Select(item => new
            {
                item.Id,
                item.OrderNumber,
                item.TotalAmount,
                item.Currency
            })
            .ToListAsync(cancellationToken);
        if (existingOrders.Any(item =>
                !string.Equals(
                    item.Currency?.Trim(),
                    source.CurrencyCode,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw Invalid(
                "PO_CONTRACT_CAPACITY_CURRENCY_INVALID",
                "An existing purchase order reserved against this contract has a different currency and must be remediated.");
        }

        var existingIds = existingOrders.Select(item => item.Id).ToList();
        var cumulativeLines = existingIds.Count == 0
            ? new List<ProcurementPurchaseOrderSourceOrderLine>()
            : await _unitOfWork.Repository<PurchaseOrderItem>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    existingIds.Contains(item.PurchaseOrderId) &&
                    !item.IsDeleted)
                .AsNoTracking()
                .Select(item => new ProcurementPurchaseOrderSourceOrderLine
                {
                    InventoryItemId = item.InventoryItemId,
                    ItemDescription = item.ItemDescription,
                    OrderedQuantity = item.OrderedQuantity,
                    UnitOfMeasure = item.UnitOfMeasure,
                    UnitPrice = item.UnitPrice
                })
                .ToListAsync(cancellationToken);
        cumulativeLines.AddRange(proposedLines);

        var cumulativeTotal =
            existingOrders.Sum(item => item.TotalAmount) + proposedTotalAmount;
        var result = ProcurementPurchaseOrderSourceRules.ValidateOrder(
            ProcurementPurchaseOrderSourceType.Contract,
            source.ApprovedLines,
            cumulativeLines,
            source.ApprovedAmount,
            cumulativeTotal,
            source.CurrencyCode,
            currencyCode);
        if (!result.IsValid)
        {
            throw Invalid(
                "PO_CONTRACT_CAPACITY_EXCEEDED",
                $"The cumulative non-cancelled purchase orders exceed the approved contract capacity. {result.Message}");
        }
    }

    private async Task EnsureCapabilityAsync(
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.TenantId == Guid.Empty ||
            _currentUser.UserId == Guid.Empty || _currentUser.IsExternalUser)
        {
            throw new ProcurementPurchaseOrderSourceAuthorizationException(
                "An authenticated internal tenant user is required.");
        }
        await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = Permission,
                SourceType = "PurchaseOrderSource",
                SourceReference = sourceReference
            },
            NormalizeCorrelation(correlationId),
            cancellationToken);
    }

    private Task RecordAsync(
        Guid sourceId,
        string sourceReference,
        string action,
        ProcurementControlEventResult result,
        string correlationId,
        string reason,
        object values,
        CancellationToken cancellationToken) =>
        _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "purchase-order-source",
                _currentUser.TenantId,
                sourceId,
                $"{NormalizeAction(action)}-{NormalizeCorrelation(correlationId)}"),
            EventType = EventType,
            Action = NormalizeAction(action),
            Result = result,
            RuleCode = "PO_APPROVED_SOURCE_REQUIRED",
            RuleId = sourceId,
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = "PurchaseOrder",
            SourceId = sourceId,
            SourceReference = sourceReference,
            Reason = reason,
            ResultValues = values,
            CorrelationId = NormalizeCorrelation(correlationId),
            CausationId = NormalizeCorrelation(correlationId),
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);

    private async Task TryRecordDeniedAsync(
        Guid sourceId,
        string sourceReference,
        string action,
        string correlationId,
        ProcurementPurchaseOrderSourceValidationException exception,
        object values,
        CancellationToken cancellationToken)
    {
        try
        {
            await RecordAsync(
                sourceId,
                sourceReference,
                action,
                ProcurementControlEventResult.Denied,
                correlationId,
                $"{exception.Code}: {exception.Message}",
                values,
                cancellationToken);
        }
        catch (Exception auditException)
        {
            _logger.LogWarning(
                auditException,
                "Failed to record purchase-order source denial {Code} for {SourceId}",
                exception.Code,
                sourceId);
        }
    }

    private async Task PublishNotificationAsync(
        string topic,
        PurchaseOrder purchaseOrder,
        string action,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = purchaseOrder.TenantId,
                TopicKey = topic,
                NotificationType = EventType,
                EntityType = "PurchaseOrder",
                EntityId = purchaseOrder.Id,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["orderNumber"] = purchaseOrder.OrderNumber,
                    ["action"] = NormalizeAction(action),
                    ["sourceType"] = purchaseOrder.ProcurementSourceType?.ToString()
                        ?? string.Empty,
                    ["sourceId"] = purchaseOrder.ProcurementSourceId ?? Guid.Empty,
                    ["sourceReference"] =
                        purchaseOrder.ProcurementSourceReference ?? string.Empty,
                    ["purchaseRequisitionId"] =
                        purchaseOrder.SourceRequisitionId ?? Guid.Empty,
                    ["sourcingCaseId"] = purchaseOrder.SourcingCaseId ?? Guid.Empty,
                    ["awardReadinessDecisionId"] =
                        purchaseOrder.AwardReadinessDecisionId ?? Guid.Empty,
                    ["supplierId"] = purchaseOrder.BusinessPartnerId,
                    ["status"] = purchaseOrder.Status
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish purchase-order source notification {Topic} for {PurchaseOrderId}",
                topic,
                purchaseOrder.Id);
        }
    }

    private sealed record AwardReservation(
        ProcurementAwardReadinessSourceType SourceType,
        Guid SourceId);

    private static string NormalizeCorrelation(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value.Trim().Length <= 100
                ? value.Trim()
                : value.Trim()[..100];

    private static string NormalizeAction(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Validated" : value.Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }

    private static ProcurementPurchaseOrderSourceValidationException Invalid(
        string code,
        string message) => new(code, message);
}
