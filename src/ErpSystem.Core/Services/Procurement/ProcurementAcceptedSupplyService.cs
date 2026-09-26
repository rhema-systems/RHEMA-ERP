using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Normalizes accepted supplier performance without taking ownership away
/// from Procurement receiving, Projects deliverables, or QS certificates.
/// </summary>
public sealed partial class ProcurementAcceptedSupplyService : IProcurementAcceptedSupplyService
{
    private const string Approved = "Approved";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;

    public ProcurementAcceptedSupplyService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ProcurementAcceptedSupplyOptionsDto> GetOptionsAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await RequiredOrderAsync(purchaseOrderId, cancellationToken);
        var category = RequiredCategory(order);
        var options = new List<ProcurementAcceptedSupplyOptionDto>();
        var blocked = new List<string>();

        if (category == ProcurementCategoryClass.Goods)
        {
            try
            {
                var accepted = await ResolveGoodsAsync(order, cancellationToken);
                options.Add(ToOption(accepted,
                    $"Accepted GRN/inspection aggregate for {order.OrderNumber}"));
            }
            catch (ProcurementAcceptedSupplyValidationException exception)
            {
                blocked.Add(exception.Message);
            }
        }
        else if (IsService(category))
        {
            var candidates = await ServiceCandidatesAsync(order, cancellationToken);
            options.AddRange(candidates.Select(value => ToOption(value,
                $"{value.SourceReference} · approved service completion")));
            if (options.Count == 0)
                blocked.Add("No approved, documented Project deliverable is linked to this service purchase order.");
        }
        else
        {
            blocked.Add("Works invoices are created only through the approved QS payment-certificate handoff.");
        }

        return new ProcurementAcceptedSupplyOptionsDto
        {
            PurchaseOrderId = order.Id,
            Category = category,
            Options = options,
            BlockedReasons = blocked
        };
    }

    public async Task<ProcurementAcceptedSupplyResolutionDto> ResolveAsync(
        ProcurementAcceptedSupplyKind kind,
        Guid sourceId,
        Guid? purchaseOrderId,
        Guid? currentVendorInvoiceId,
        CancellationToken cancellationToken = default)
    {
        if (sourceId == Guid.Empty)
            throw Invalid("ACCEPTED_SUPPLY_SOURCE_REQUIRED",
                "Select an authoritative accepted-supply record.");

        ProcurementAcceptedSupplyResolutionDto resolution;
        if (kind == ProcurementAcceptedSupplyKind.WorksPaymentCertificate)
        {
            resolution = await ResolveWorksAsync(sourceId, cancellationToken);
            if (purchaseOrderId.HasValue && resolution.PurchaseOrderId != purchaseOrderId)
                throw Invalid("ACCEPTED_SUPPLY_PO_MISMATCH",
                    "The Works certificate does not belong to the selected purchase order.");
        }
        else
        {
            if (!purchaseOrderId.HasValue || purchaseOrderId == Guid.Empty)
                throw Invalid("ACCEPTED_SUPPLY_PO_REQUIRED",
                    "Goods and Services acceptance must be linked to a purchase order.");
            var order = await RequiredOrderAsync(purchaseOrderId.Value, cancellationToken);
            var category = RequiredCategory(order);
            resolution = kind switch
            {
                ProcurementAcceptedSupplyKind.GoodsReceiptInspection
                    when category == ProcurementCategoryClass.Goods && sourceId == order.Id =>
                    await ResolveGoodsAsync(order, cancellationToken),
                ProcurementAcceptedSupplyKind.ServiceCompletion when IsService(category) =>
                    await ResolveServiceAsync(order, sourceId, cancellationToken),
                _ => throw Invalid("ACCEPTED_SUPPLY_KIND_MISMATCH",
                    "The selected acceptance record does not match the purchase-order category.")
            };
        }

        // A Goods PO can legitimately be invoiced in parts; the existing AP
        // cumulative-quantity match owns that over-invoicing control. A single
        // approved service completion/certificate, however, is a one-time
        // accepted-supply authority and cannot authorize two invoices.
        var alreadyUsed = kind != ProcurementAcceptedSupplyKind.GoodsReceiptInspection &&
            await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(value =>
                    value.TenantId == _currentUser.TenantId &&
                    value.AcceptedSupplyKind == kind &&
                value.AcceptedSupplySourceId == sourceId &&
                (!currentVendorInvoiceId.HasValue || value.Id != currentVendorInvoiceId.Value) &&
                !value.IsDeleted)
                .IgnoreQueryFilters()
                .AsNoTracking().AnyAsync(cancellationToken);
        if (alreadyUsed)
            throw Invalid("ACCEPTED_SUPPLY_ALREADY_INVOICED",
                "The selected acceptance record is already bound to another vendor invoice.");

        return resolution;
    }

    private async Task<ProcurementAcceptedSupplyResolutionDto> ResolveGoodsAsync(
        PurchaseOrder order,
        CancellationToken cancellationToken)
    {
        var receipts = await ReadGoodsReceiptLinesAsync(order, cancellationToken);
        var lines = receipts.GroupBy(value => value.PurchaseOrderItemId)
            .Select(group => new ProcurementAcceptedSupplyLineDto
            {
                PurchaseOrderItemId = group.Key,
                AcceptedQuantity = group.Sum(value => value.NetAcceptedQuantity),
                UnitPrice = group.First().UnitPrice
            }).OrderBy(value => value.PurchaseOrderItemId).ToList();
        if (lines.Count == 0 || lines.Sum(value => value.AcceptedQuantity) <= 0m)
            throw Invalid("ACCEPTED_GOODS_QUANTITY_MISSING",
                "No approved, accepted and unreturned inspection quantity is available. Pending inspections cannot authorize an invoice.");
        var snapshot = new
        {
            schemaVersion = "tdc.accepted-supply.v2",
            kind = ProcurementAcceptedSupplyKind.GoodsReceiptInspection,
            purchaseOrderId = order.Id,
            receipts,
            lines
        };
        return Build(ProcurementAcceptedSupplyKind.GoodsReceiptInspection, order.Id,
            order.OrderNumber, ProcurementCategoryClass.Goods, order.Id, order.BusinessPartnerId,
            order.Currency, lines.Sum(value => value.AcceptedQuantity * value.UnitPrice),
            receipts.Max(value => value.AcceptedAtUtc), lines, snapshot);
    }

    private async Task<List<ProcurementAcceptedSupplyResolutionDto>> ServiceCandidatesAsync(
        PurchaseOrder order,
        CancellationToken cancellationToken)
    {
        var used = await _unitOfWork.Repository<VendorInvoice>()
            .GetQueryable(value => value.TenantId == _currentUser.TenantId &&
                value.AcceptedSupplyKind == ProcurementAcceptedSupplyKind.ServiceCompletion &&
                value.AcceptedSupplySourceId.HasValue && !value.IsDeleted)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(value => value.AcceptedSupplySourceId!.Value)
            .ToListAsync(cancellationToken);
        var deliverables = await _unitOfWork.Repository<ProjectDeliverable>()
            .GetQueryable(value => value.TenantId == _currentUser.TenantId &&
                value.Status == Approved && value.ApprovedById.HasValue &&
                value.ApprovedAt.HasValue && value.SubmittedDocumentId.HasValue &&
                value.WorkItemId.HasValue && !used.Contains(value.Id) && !value.IsDeleted)
            .IgnoreQueryFilters()
            .Include(value => value.WorkItem).ThenInclude(value => value!.ProjectPackage)
            .Include(value => value.SubmittedDocument).ThenInclude(value => value!.FileUploadRecord)
            .AsNoTracking().ToListAsync(cancellationToken);

        return deliverables.Where(value =>
                value.WorkItem is { IsDeleted: false } &&
                value.WorkItem.TenantId == order.TenantId &&
                value.WorkItem.ProjectPackage is { IsDeleted: false } package &&
                package.TenantId == order.TenantId &&
                package.PurchaseOrderId == order.Id &&
                package.BusinessPartnerId == order.BusinessPartnerId &&
                value.SubmittedDocument is { IsDeleted: false } document &&
                document.TenantId == order.TenantId &&
                document.FileUploadRecord is { IsDeleted: false,
                    VirusScanStatus: FileVirusScanStatus.Clean } upload &&
                upload.TenantId == order.TenantId)
            .Select(value => BuildService(order, value)).ToList();
    }

    private async Task<ProcurementAcceptedSupplyResolutionDto> ResolveServiceAsync(
        PurchaseOrder order,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var deliverable = await _unitOfWork.Repository<ProjectDeliverable>()
            .GetQueryable(value => value.TenantId == _currentUser.TenantId &&
                value.Id == sourceId && !value.IsDeleted)
            .IgnoreQueryFilters()
            .Include(value => value.WorkItem).ThenInclude(value => value!.ProjectPackage)
            .Include(value => value.SubmittedDocument).ThenInclude(value => value!.FileUploadRecord)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("SERVICE_COMPLETION_NOT_FOUND",
                "The selected service completion was not found in the current tenant.");
        if (deliverable.Status != Approved || !deliverable.ApprovedById.HasValue ||
            !deliverable.ApprovedAt.HasValue || !deliverable.SubmittedDocumentId.HasValue)
            throw Invalid("SERVICE_COMPLETION_NOT_APPROVED",
                "A service completion requires workflow approval and submitted DMS evidence.");
        var document = deliverable.SubmittedDocument;
        var upload = document?.FileUploadRecord;
        if (document is not { IsDeleted: false } || document.TenantId != order.TenantId ||
            upload is not { IsDeleted: false, VirusScanStatus: FileVirusScanStatus.Clean } ||
            upload.TenantId != order.TenantId)
            throw Invalid("SERVICE_COMPLETION_DOCUMENT_NOT_CLEAN",
                "The approved service completion requires a current malware-clean controlled document.");
        if (deliverable.ExternalSignOffRequired && !deliverable.ExternalApprovedById.HasValue)
            throw Invalid("SERVICE_COMPLETION_EXTERNAL_SIGNOFF_REQUIRED",
                "The required external service-completion sign-off is not approved.");
        var package = deliverable.WorkItem?.ProjectPackage;
        if (deliverable.WorkItem is not { IsDeleted: false } workItem ||
            workItem.TenantId != order.TenantId || package is not { IsDeleted: false } ||
            package.TenantId != order.TenantId || package.PurchaseOrderId != order.Id ||
            package.BusinessPartnerId != order.BusinessPartnerId)
            throw Invalid("SERVICE_COMPLETION_PO_MISMATCH",
                "The service completion does not belong to the selected purchase order and supplier.");
        return BuildService(order, deliverable);
    }

    private static ProcurementAcceptedSupplyResolutionDto BuildService(
        PurchaseOrder order,
        ProjectDeliverable deliverable)
    {
        var lines = order.Items.Where(value => !value.IsDeleted)
            .Select(value => new ProcurementAcceptedSupplyLineDto
            {
                PurchaseOrderItemId = value.Id,
                AcceptedQuantity = value.OrderedQuantity,
                UnitPrice = value.UnitPrice
            }).OrderBy(value => value.PurchaseOrderItemId).ToList();
        var snapshot = new
        {
            schemaVersion = "tdc.accepted-supply.v1",
            kind = ProcurementAcceptedSupplyKind.ServiceCompletion,
            purchaseOrderId = order.Id,
            deliverable = new
            {
                deliverable.Id, deliverable.Title, deliverable.Status,
                deliverable.SubmittedDocumentId, deliverable.SubmittedAt,
                deliverable.SubmittedDocument!.FileUploadRecordId,
                deliverable.SubmittedDocument.FileUploadRecord!.VirusScanStatus,
                deliverable.SubmittedDocument.FileUploadRecord.ScannedAtUtc,
                deliverable.ApprovedById, deliverable.ApprovedAt,
                deliverable.ExternalSignOffRequired,
                deliverable.ExternalApprovedById, deliverable.ExternalApprovedAt
            },
            lines
        };
        return Build(
            ProcurementAcceptedSupplyKind.ServiceCompletion,
            deliverable.Id,
            deliverable.Title,
            RequiredCategory(order),
            order.Id,
            order.BusinessPartnerId,
            order.Currency,
            lines.Sum(value => value.AcceptedQuantity * value.UnitPrice),
            deliverable.ApprovedAt!.Value,
            lines,
            snapshot);
    }

    private async Task<ProcurementAcceptedSupplyResolutionDto> ResolveWorksAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var certificate = await _unitOfWork.Repository<ProjectPaymentCertificate>()
            .GetQueryable(value => value.TenantId == _currentUser.TenantId &&
                value.Id == sourceId && !value.IsDeleted)
            .IgnoreQueryFilters()
            .Include(value => value.Contract)
            .Include(value => value.ProjectPackage)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Invalid("WORKS_CERTIFICATE_NOT_FOUND",
                "The selected QS payment certificate was not found in the current tenant.");
        if (certificate.Status is not (ProjectPaymentCertificateStatuses.Approved or
            ProjectPaymentCertificateStatuses.Paid) || certificate.ApprovalStatus != Approved ||
            !certificate.ApprovedById.HasValue || !certificate.ApprovedAt.HasValue)
            throw Invalid("WORKS_CERTIFICATE_NOT_APPROVED",
                "Only an approved QS payment certificate can authorize a Works invoice.");
        if (certificate.Contract == null ||
            certificate.Contract.IsDeleted ||
            certificate.Contract.TenantId != _currentUser.TenantId ||
            !string.Equals(certificate.Contract.ContractType, "Works", StringComparison.OrdinalIgnoreCase))
            throw Invalid("WORKS_CERTIFICATE_CONTRACT_INVALID",
                "The payment certificate is not linked to an active Works contract.");
        var subcontractEvidence = certificate.QuantitySurveySubcontractValuationId.HasValue
            ? await _unitOfWork.Repository<QuantitySurveySubcontractEvidence>()
                .GetQueryable(value => value.TenantId == _currentUser.TenantId &&
                    value.ValuationId == certificate.QuantitySurveySubcontractValuationId &&
                    !value.IsDeleted)
                .IgnoreQueryFilters()
                .AsNoTracking().OrderBy(value => value.Id).ToListAsync(cancellationToken)
            : [];
        if ((!certificate.CentralDocumentRecordId.HasValue ||
             string.IsNullOrWhiteSpace(certificate.GeneratedDocumentHash)) &&
            subcontractEvidence.Count == 0)
            throw Invalid("WORKS_CERTIFICATE_DOCUMENT_MISSING",
                "The approved Works certificate has no governed generated document.");

        var snapshot = new
        {
            schemaVersion = "tdc.accepted-supply.v1",
            kind = ProcurementAcceptedSupplyKind.WorksPaymentCertificate,
            certificate = new
            {
                certificate.Id, certificate.CertificateNumber, certificate.ContractId,
                certificate.ProjectId, certificate.ProjectPackageId,
                certificate.Status, certificate.ApprovalStatus,
                certificate.ApprovedById, certificate.ApprovedAt,
                certificate.NetCertifiedAmount, certificate.Currency,
                certificate.CentralDocumentRecordId, certificate.CentralDocumentVersionId,
                certificate.GeneratedDocumentHash, certificate.PolicyHash,
                subcontractEvidence = subcontractEvidence.Select(value => new
                {
                    value.Id, value.EvidenceType, value.FileUploadRecordId,
                    value.CentralDocumentRecordId, value.CentralDocumentVersionId,
                    value.ChecksumSha256
                })
            }
        };
        return Build(
            ProcurementAcceptedSupplyKind.WorksPaymentCertificate,
            certificate.Id,
            certificate.CertificateNumber ?? certificate.Id.ToString(),
            ProcurementCategoryClass.Works,
            certificate.ProjectPackage?.PurchaseOrderId,
            certificate.SubcontractorBusinessPartnerId ??
                certificate.Contract.BusinessPartnerId,
            certificate.Currency,
            certificate.NetCertifiedAmount,
            certificate.ApprovedAt.Value,
            [],
            snapshot);
    }

    private async Task<PurchaseOrder> RequiredOrderAsync(Guid purchaseOrderId,
        CancellationToken cancellationToken) =>
        await _unitOfWork.Repository<PurchaseOrder>()
            .GetQueryable(value => value.TenantId == _currentUser.TenantId &&
                value.Id == purchaseOrderId && !value.IsDeleted)
            .IgnoreQueryFilters()
            .Include(value => value.Items)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
        ?? throw Invalid("ACCEPTED_SUPPLY_PO_NOT_FOUND",
            "The purchase order was not found in the current tenant.");

    private static ProcurementAcceptedSupplyResolutionDto Build(
        ProcurementAcceptedSupplyKind kind,
        Guid sourceId,
        string reference,
        ProcurementCategoryClass category,
        Guid? purchaseOrderId,
        Guid businessPartnerId,
        string currency,
        decimal? amount,
        DateTime acceptedAt,
        IReadOnlyList<ProcurementAcceptedSupplyLineDto> lines,
        object snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new ProcurementAcceptedSupplyResolutionDto
        {
            Kind = kind,
            SourceId = sourceId,
            SourceReference = reference,
            Category = category,
            PurchaseOrderId = purchaseOrderId,
            BusinessPartnerId = businessPartnerId,
            CurrencyCode = currency.Trim().ToUpperInvariant(),
            AcceptedAmount = amount,
            AcceptedAtUtc = acceptedAt.Kind == DateTimeKind.Utc ? acceptedAt : acceptedAt.ToUniversalTime(),
            Lines = lines,
            SnapshotJson = json,
            SourceIntegrityHash = ProcurementInvoiceThreeWayMatchRules.HashSnapshot(snapshot)
        };
    }

    private static ProcurementAcceptedSupplyOptionDto ToOption(
        ProcurementAcceptedSupplyResolutionDto value,
        string label) => new()
    {
        Kind = value.Kind,
        SourceId = value.SourceId,
        SourceReference = value.SourceReference,
        Label = label,
        Category = value.Category,
        PurchaseOrderId = value.PurchaseOrderId,
        BusinessPartnerId = value.BusinessPartnerId,
        CurrencyCode = value.CurrencyCode,
        AcceptedAmount = value.AcceptedAmount,
        AcceptedAtUtc = value.AcceptedAtUtc,
        SourceIntegrityHash = value.SourceIntegrityHash
    };

    private static ProcurementCategoryClass RequiredCategory(PurchaseOrder order) =>
        order.ProcurementCategory ?? throw Invalid("PURCHASE_ORDER_CATEGORY_REQUIRED",
            "The purchase order has no governed category snapshot. Revalidate its approved requisition lineage.");

    private static bool IsService(ProcurementCategoryClass category) =>
        category is ProcurementCategoryClass.TechnicalServices or
            ProcurementCategoryClass.ConsultancyServices or
            ProcurementCategoryClass.GeneralServices;

    private static ProcurementAcceptedSupplyValidationException Invalid(
        string code,
        string message) => new(code, message);
}
