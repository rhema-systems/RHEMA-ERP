using System.Text.Json;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosReceiptService
{
    Task<MobilePosReceiptDto> GetAsync(
        Guid saleId,
        string installationId,
        CancellationToken cancellationToken);

    Task<MobilePosReceiptDto> RecordReprintAsync(
        Guid saleId,
        MobilePosReceiptReprintRequestDto request,
        CancellationToken cancellationToken);

    Task<MobilePosCollectionReceiptDto> GetCollectionAsync(
        Guid collectionId,
        string installationId,
        CancellationToken cancellationToken);

    Task<MobilePosCollectionReceiptDto> RecordCollectionReprintAsync(
        Guid collectionId,
        MobilePosReceiptReprintRequestDto request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Projects an immutable receipt from the canonical Mobile POS sale, Finance invoice, and one
/// canonical CustomerPayment per tender. Reprints append audit evidence only; they never recreate,
/// repost, or reallocate the underlying Finance documents.
/// </summary>
public sealed class MobilePosReceiptService : IMobilePosReceiptService
{
    private const string AuditAction = "Reprint";
    private const string AuditResource = "MobilePosReceipt";
    private const string CollectionAuditResource = "MobilePosCollectionReceipt";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMobilePosFoundationService _foundation;

    public MobilePosReceiptService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IMobilePosFoundationService foundation)
    {
        _db = db;
        _currentUser = currentUser;
        _foundation = foundation;
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A current tenant is required for Mobile POS.");

    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated user is required for Mobile POS.");

    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName)
        ? "Unknown"
        : _currentUser.UserName.Trim();

    public async Task<MobilePosReceiptDto> GetAsync(
        Guid saleId,
        string installationId,
        CancellationToken cancellationToken)
    {
        var bootstrap = await _foundation.GetBootstrapAsync(
            Required(installationId, 200, "installation ID"), cancellationToken);
        var sale = await GetSaleAsync(saleId, bootstrap.Store.Id, cancellationToken);
        var reprintCount = await GetReprintCountAsync(saleId, cancellationToken);

        return await MapAsync(
            sale,
            "ORIGINAL",
            0,
            null,
            sale.SynchronizedAtUtc ?? sale.CreatedAt,
            null,
            reprintCount,
            cancellationToken);
    }

    public async Task<MobilePosReceiptDto> RecordReprintAsync(
        Guid saleId,
        MobilePosReceiptReprintRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var installationId = Required(request.InstallationId, 200, "installation ID");
        var clientEventId = Required(request.ClientEventId, 100, "client event ID");
        var reason = Optional(request.Reason, 500, "reason");
        var bootstrap = await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        var sale = await GetSaleAsync(saleId, bootstrap.Store.Id, cancellationToken);
        var idempotencyKey = $"MobilePosReceiptReprint:{saleId:N}:{bootstrap.Device.Id:N}:{clientEventId}";

        var existing = await _db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (existing != null)
        {
            var existingPayload = ReadPayload(existing.NewValues);
            return await MapAsync(
                sale,
                "REPRINT",
                existingPayload?.CopyNumber ?? await GetReprintCountAsync(saleId, cancellationToken),
                existing.Id,
                existing.Timestamp,
                existingPayload?.Reason,
                await GetReprintCountAsync(saleId, cancellationToken),
                cancellationToken);
        }

        var copyNumber = await GetReprintCountAsync(saleId, cancellationToken) + 1;
        var requestedAtUtc = DateTime.UtcNow;
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = UserId,
            Username = UserName,
            Action = AuditAction,
            Resource = AuditResource,
            ResourceId = sale.Id.ToString(),
            IdempotencyKey = idempotencyKey,
            NewValues = JsonSerializer.Serialize(new ReprintAuditPayload(
                sale.Id,
                sale.InvoiceId!.Value,
                sale.InvoiceNumber!,
                sale.LocalReference,
                bootstrap.Device.Id,
                sale.MobilePosStoreId,
                sale.MobilePosTillId,
                copyNumber,
                reason)),
            IpAddress = Limit(_currentUser.IpAddress, 45) ?? "Unknown",
            UserAgent = Limit(_currentUser.UserAgent, 500),
            Timestamp = requestedAtUtc,
            CreatedAt = requestedAtUtc,
            CreatedBy = UserName,
            CreatedById = UserId
        };

        _db.AuditLogs.Add(audit);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.Entry(audit).State = EntityState.Detached;
            existing = await _db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
            if (existing == null) throw;

            var existingPayload = ReadPayload(existing.NewValues);
            return await MapAsync(
                sale,
                "REPRINT",
                existingPayload?.CopyNumber ?? copyNumber,
                existing.Id,
                existing.Timestamp,
                existingPayload?.Reason,
                await GetReprintCountAsync(saleId, cancellationToken),
                cancellationToken);
        }

        return await MapAsync(
            sale,
            "REPRINT",
            copyNumber,
            audit.Id,
            requestedAtUtc,
            reason,
            copyNumber,
            cancellationToken);
    }

    public async Task<MobilePosCollectionReceiptDto> GetCollectionAsync(
        Guid collectionId,
        string installationId,
        CancellationToken cancellationToken)
    {
        var bootstrap = await _foundation.GetBootstrapAsync(
            Required(installationId, 200, "installation ID"), cancellationToken);
        var collection = await GetCollectionAsync(collectionId, bootstrap.Store.Id, cancellationToken);
        var reprintCount = await GetReprintCountAsync(
            CollectionAuditResource, collectionId, cancellationToken);

        return await MapCollectionAsync(
            collection,
            "ORIGINAL",
            0,
            null,
            collection.SynchronizedAtUtc ?? collection.CreatedAt,
            null,
            reprintCount,
            cancellationToken);
    }

    public async Task<MobilePosCollectionReceiptDto> RecordCollectionReprintAsync(
        Guid collectionId,
        MobilePosReceiptReprintRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var installationId = Required(request.InstallationId, 200, "installation ID");
        var clientEventId = Required(request.ClientEventId, 100, "client event ID");
        var reason = Optional(request.Reason, 500, "reason");
        var bootstrap = await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        var collection = await GetCollectionAsync(collectionId, bootstrap.Store.Id, cancellationToken);
        var idempotencyKey = $"MobilePosCollectionReceiptReprint:{collectionId:N}:{bootstrap.Device.Id:N}:{clientEventId}";

        var existing = await _db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (existing != null)
        {
            var existingPayload = ReadCollectionPayload(existing.NewValues);
            return await MapCollectionAsync(
                collection,
                "REPRINT",
                existingPayload?.CopyNumber ?? await GetReprintCountAsync(CollectionAuditResource, collectionId, cancellationToken),
                existing.Id,
                existing.Timestamp,
                existingPayload?.Reason,
                await GetReprintCountAsync(CollectionAuditResource, collectionId, cancellationToken),
                cancellationToken);
        }

        var copyNumber = await GetReprintCountAsync(CollectionAuditResource, collectionId, cancellationToken) + 1;
        var requestedAtUtc = DateTime.UtcNow;
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = UserId,
            Username = UserName,
            Action = AuditAction,
            Resource = CollectionAuditResource,
            ResourceId = collection.Id.ToString(),
            IdempotencyKey = idempotencyKey,
            NewValues = JsonSerializer.Serialize(new CollectionReprintAuditPayload(
                collection.Id,
                collection.LocalReference,
                collection.MobilePosDeviceId,
                collection.MobilePosStoreId,
                collection.MobilePosTillId,
                collection.Tenders.OrderBy(item => item.Sequence).Select(item => item.PaymentNumber).ToArray(),
                copyNumber,
                reason)),
            IpAddress = Limit(_currentUser.IpAddress, 45) ?? "Unknown",
            UserAgent = Limit(_currentUser.UserAgent, 500),
            Timestamp = requestedAtUtc,
            CreatedAt = requestedAtUtc,
            CreatedBy = UserName,
            CreatedById = UserId
        };

        _db.AuditLogs.Add(audit);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.Entry(audit).State = EntityState.Detached;
            existing = await _db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
            if (existing == null) throw;

            var existingPayload = ReadCollectionPayload(existing.NewValues);
            return await MapCollectionAsync(
                collection,
                "REPRINT",
                existingPayload?.CopyNumber ?? copyNumber,
                existing.Id,
                existing.Timestamp,
                existingPayload?.Reason,
                await GetReprintCountAsync(CollectionAuditResource, collectionId, cancellationToken),
                cancellationToken);
        }

        return await MapCollectionAsync(
            collection,
            "REPRINT",
            copyNumber,
            audit.Id,
            requestedAtUtc,
            reason,
            copyNumber,
            cancellationToken);
    }

    private async Task<MobilePosSale> GetSaleAsync(
        Guid saleId,
        Guid assignedStoreId,
        CancellationToken cancellationToken)
    {
        if (saleId == Guid.Empty)
            throw new KeyNotFoundException("The Mobile POS receipt was not found.");

        var sale = await _db.MobilePosSales.AsNoTracking()
            .Include(item => item.MobilePosStore).ThenInclude(item => item.Location)
            .Include(item => item.MobilePosTill)
            .Include(item => item.CashierTillSession)
            .Include(item => item.MobilePosDevice)
            .Include(item => item.OperatorUser)
            .Include(item => item.BusinessPartner)
            .Include(item => item.Invoice)
            .Include(item => item.Lines)
            .Include(item => item.Tenders).ThenInclude(item => item.PaymentMethod)
            .SingleOrDefaultAsync(item => item.TenantId == TenantId
                && item.Id == saleId
                && item.MobilePosStoreId == assignedStoreId
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("The Mobile POS receipt was not found for your assigned store.");

        if (sale.Status != MobilePosSaleStatus.Completed
            || !sale.InvoiceId.HasValue
            || string.IsNullOrWhiteSpace(sale.InvoiceNumber)
            || sale.Tenders.Count == 0
            || sale.Tenders.Any(item => !item.CustomerPaymentId.HasValue
                || string.IsNullOrWhiteSpace(item.PaymentNumber)
                || item.Status != MobilePosTenderStatus.Completed))
        {
            throw new MobilePosCommandRejectedException(
                "MOBILE_POS_RECEIPT_NOT_FINAL",
                "A canonical receipt is available only after the invoice and every tender have completed successfully.");
        }

        return sale;
    }

    private Task<int> GetReprintCountAsync(Guid saleId, CancellationToken cancellationToken) =>
        GetReprintCountAsync(AuditResource, saleId, cancellationToken);

    private Task<int> GetReprintCountAsync(
        string resource,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        _db.AuditLogs.AsNoTracking().CountAsync(item =>
            item.TenantId == TenantId
            && item.Action == AuditAction
            && item.Resource == resource
            && item.ResourceId == sourceId.ToString(),
            cancellationToken);

    private async Task<MobilePosReceiptDto> MapAsync(
        MobilePosSale sale,
        string copyType,
        int copyNumber,
        Guid? auditEventId,
        DateTime generatedAtUtc,
        string? reprintReason,
        int reprintCount,
        CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .Where(item => item.Id == TenantId)
            .Select(item => new { item.Code, item.Name })
            .SingleOrDefaultAsync(cancellationToken);
        var cashierName = !string.IsNullOrWhiteSpace(sale.CashierTillSession.CashierName)
            ? sale.CashierTillSession.CashierName
            : sale.OperatorUser.FullName.Trim();
        if (string.IsNullOrWhiteSpace(cashierName))
            cashierName = sale.OperatorUser.UserName ?? UserName;

        return new MobilePosReceiptDto
        {
            ReceiptKind = "SALE",
            ReceiptId = sale.Id,
            CopyType = copyType,
            CopyNumber = copyNumber,
            ReprintCount = reprintCount,
            AuditEventId = auditEventId,
            GeneratedAtUtc = generatedAtUtc,
            ReprintReason = reprintReason,
            QrReference = $"RHEMA|MOBILEPOS|{sale.InvoiceNumber}|{sale.Id:N}",
            TenantId = sale.TenantId,
            TenantCode = tenant?.Code ?? string.Empty,
            TenantName = tenant?.Name ?? string.Empty,
            StoreId = sale.MobilePosStoreId,
            StoreCode = sale.MobilePosStore.Code,
            StoreName = sale.MobilePosStore.Name,
            LocationName = sale.MobilePosStore.Location?.Name ?? string.Empty,
            TillId = sale.MobilePosTillId,
            TillNumber = sale.MobilePosTill.TillNumber,
            TillName = sale.MobilePosTill.Name,
            TillSessionId = sale.CashierTillSessionId,
            TillSessionNumber = sale.CashierTillSession.SessionNumber,
            BusinessDate = sale.BusinessDate,
            DeviceId = sale.MobilePosDeviceId,
            DeviceName = sale.MobilePosDevice.DeviceName,
            CashierUserId = sale.OperatorUserId,
            CashierName = cashierName,
            BusinessPartnerId = sale.BusinessPartnerId,
            BusinessPartnerRoleId = sale.BusinessPartnerRoleId,
            CustomerCode = sale.BusinessPartner.PartnerCode,
            CustomerName = sale.BusinessPartner.PartnerName,
            UsedStoreDefaultCustomer = sale.UsedStoreDefaultCustomer,
            InvoiceId = sale.InvoiceId!.Value,
            InvoiceNumber = sale.InvoiceNumber!,
            InvoiceStatus = sale.Invoice?.Status.ToString() ?? sale.Status.ToString(),
            LocalReference = sale.LocalReference,
            OccurredAtUtc = sale.OccurredAtUtc,
            CurrencyCode = sale.CurrencyCode,
            SubTotal = sale.SubTotal,
            TaxAmount = sale.TaxAmount,
            DiscountAmount = sale.DiscountAmount,
            TotalAmount = sale.TotalAmount,
            Lines = sale.Lines.OrderBy(item => item.Sequence).Select(item => new MobilePosReceiptLineDto
            {
                Sequence = item.Sequence,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountAmount = item.DiscountAmount,
                TaxAmount = item.TaxAmount,
                LineTotal = item.LineTotal,
                UnitOfMeasureCode = item.UnitOfMeasureCode ?? string.Empty
            }).ToArray(),
            Tenders = sale.Tenders.OrderBy(item => item.Sequence).Select(item => new MobilePosReceiptTenderDto
            {
                Sequence = item.Sequence,
                PaymentMethodCode = item.PaymentMethod.Code ?? item.PaymentMethod.Name,
                PaymentMethodName = item.PaymentMethod.Name,
                Amount = item.Amount,
                ExternalReference = item.ExternalReference,
                CustomerPaymentId = item.CustomerPaymentId!.Value,
                PaymentNumber = item.PaymentNumber!,
                PaymentStatus = item.ProviderStatus ?? item.Status.ToString()
            }).ToArray()
        };
    }

    private async Task<MobilePosCollection> GetCollectionAsync(
        Guid collectionId,
        Guid assignedStoreId,
        CancellationToken cancellationToken)
    {
        if (collectionId == Guid.Empty)
            throw new KeyNotFoundException("The Mobile POS collection receipt was not found.");

        var collection = await _db.MobilePosCollections.AsNoTracking()
            .Include(item => item.MobilePosStore).ThenInclude(item => item.Location)
            .Include(item => item.MobilePosTill)
            .Include(item => item.CashierTillSession)
            .Include(item => item.MobilePosDevice)
            .Include(item => item.OperatorUser)
            .Include(item => item.BusinessPartner)
            .Include(item => item.Allocations)
            .Include(item => item.Tenders).ThenInclude(item => item.PaymentMethod)
            .SingleOrDefaultAsync(item => item.TenantId == TenantId
                && item.Id == collectionId
                && item.MobilePosStoreId == assignedStoreId
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("The Mobile POS collection receipt was not found for your assigned store.");

        if (collection.Status != MobilePosCollectionStatus.Completed
            || collection.Allocations.Count == 0
            || collection.Tenders.Count == 0
            || collection.Tenders.Any(item => item.CustomerPaymentId == Guid.Empty
                || string.IsNullOrWhiteSpace(item.PaymentNumber)
                || item.Status != MobilePosTenderStatus.Completed))
        {
            throw new MobilePosCommandRejectedException(
                "MOBILE_POS_COLLECTION_RECEIPT_NOT_FINAL",
                "A canonical collection receipt is available only after every payment and allocation has completed successfully.");
        }

        return collection;
    }

    private async Task<MobilePosCollectionReceiptDto> MapCollectionAsync(
        MobilePosCollection collection,
        string copyType,
        int copyNumber,
        Guid? auditEventId,
        DateTime generatedAtUtc,
        string? reprintReason,
        int reprintCount,
        CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .Where(item => item.Id == TenantId)
            .Select(item => new { item.Code, item.Name })
            .SingleOrDefaultAsync(cancellationToken);
        var cashierName = !string.IsNullOrWhiteSpace(collection.CashierTillSession.CashierName)
            ? collection.CashierTillSession.CashierName
            : collection.OperatorUser.FullName.Trim();
        if (string.IsNullOrWhiteSpace(cashierName))
            cashierName = collection.OperatorUser.UserName ?? UserName;

        return new MobilePosCollectionReceiptDto
        {
            ReceiptKind = "COLLECTION",
            ReceiptId = collection.Id,
            CopyType = copyType,
            CopyNumber = copyNumber,
            ReprintCount = reprintCount,
            AuditEventId = auditEventId,
            GeneratedAtUtc = generatedAtUtc,
            ReprintReason = reprintReason,
            QrReference = $"RHEMA|MOBILEPOS|COLLECTION|{collection.LocalReference}|{collection.Id:N}",
            TenantId = collection.TenantId,
            TenantCode = tenant?.Code ?? string.Empty,
            TenantName = tenant?.Name ?? string.Empty,
            StoreId = collection.MobilePosStoreId,
            StoreCode = collection.MobilePosStore.Code,
            StoreName = collection.MobilePosStore.Name,
            LocationName = collection.MobilePosStore.Location?.Name ?? string.Empty,
            TillId = collection.MobilePosTillId,
            TillNumber = collection.MobilePosTill.TillNumber,
            TillName = collection.MobilePosTill.Name,
            TillSessionId = collection.CashierTillSessionId,
            TillSessionNumber = collection.CashierTillSession.SessionNumber,
            BusinessDate = collection.BusinessDate,
            DeviceId = collection.MobilePosDeviceId,
            DeviceName = collection.MobilePosDevice.DeviceName,
            CashierUserId = collection.OperatorUserId,
            CashierName = cashierName,
            BusinessPartnerId = collection.BusinessPartnerId,
            BusinessPartnerRoleId = collection.BusinessPartnerRoleId,
            CustomerCode = collection.BusinessPartner.PartnerCode,
            CustomerName = collection.BusinessPartner.PartnerName,
            LocalReference = collection.LocalReference,
            OccurredAtUtc = collection.OccurredAtUtc,
            CurrencyCode = collection.CurrencyCode,
            TotalAmount = collection.TotalAmount,
            WasRecordedOffline = collection.Tenders.Any(item => item.WasRecordedOffline),
            Allocations = collection.Allocations.OrderBy(item => item.Sequence).Select(item =>
                new MobilePosCollectionReceiptAllocationDto
                {
                    Sequence = item.Sequence,
                    InvoiceId = item.InvoiceId,
                    InvoiceNumber = item.InvoiceNumber,
                    Amount = item.Amount
                }).ToArray(),
            Tenders = collection.Tenders.OrderBy(item => item.Sequence).Select(item =>
                new MobilePosReceiptTenderDto
                {
                    Sequence = item.Sequence,
                    PaymentMethodCode = item.PaymentMethod.Code ?? item.PaymentMethod.Name,
                    PaymentMethodName = item.PaymentMethod.Name,
                    Amount = item.Amount,
                    ExternalReference = item.ExternalReference,
                    CustomerPaymentId = item.CustomerPaymentId,
                    PaymentNumber = item.PaymentNumber,
                    PaymentStatus = item.PaymentStatus ?? item.Status.ToString()
                }).ToArray()
        };
    }

    private static ReprintAuditPayload? ReadPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ReprintAuditPayload>(json); }
        catch (JsonException) { return null; }
    }

    private static CollectionReprintAuditPayload? ReadCollectionPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<CollectionReprintAuditPayload>(json); }
        catch (JsonException) { return null; }
    }

    private static string Required(string? value, int maximumLength, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
            throw new MobilePosCommandRejectedException(
                "MOBILE_POS_REQUIRED_VALUE_INVALID",
                $"The {label} must contain 1 to {maximumLength} characters.");
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string label)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength)
            throw new MobilePosCommandRejectedException(
                "MOBILE_POS_VALUE_TOO_LONG",
                $"The {label} cannot exceed {maximumLength} characters.");
        return normalized;
    }

    private static string? Limit(string? value, int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is { Length: > 0 } && normalized.Length > maximumLength
            ? normalized[..maximumLength]
            : normalized;
    }

    private sealed record ReprintAuditPayload(
        Guid SaleId,
        Guid InvoiceId,
        string InvoiceNumber,
        string LocalReference,
        Guid DeviceId,
        Guid StoreId,
        Guid TillId,
        int CopyNumber,
        string? Reason);

    private sealed record CollectionReprintAuditPayload(
        Guid CollectionId,
        string LocalReference,
        Guid DeviceId,
        Guid StoreId,
        Guid TillId,
        IReadOnlyList<string> PaymentNumbers,
        int CopyNumber,
        string? Reason);
}
