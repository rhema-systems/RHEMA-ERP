using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosCollectionService
{
    Task<MobilePosCollectionResultDto> CompleteAsync(
        MobilePosCompleteCollectionRequestDto request,
        CancellationToken cancellationToken);

    Task<MobilePosCollectionResultDto> CompleteOfflineAsync(
        MobilePosCompleteCollectionRequestDto request,
        MobilePosOfflineGrantAuthorization authorization,
        CancellationToken cancellationToken);
}

/// <summary>
/// Records a governed receipt against existing AR invoices. The Mobile POS rows are source and
/// audit envelopes only; IPaymentService owns the CustomerPayment, allocations, posting, liquidity,
/// numbering, currency, and settlement-dimension rules.
/// </summary>
public sealed class MobilePosCollectionService : IMobilePosCollectionService
{
    private const string CommandType = "CustomerCollection";
    private const int SchemaVersion = 1;
    private static readonly FinancePostingProducerContext PaymentProducer =
        FinanceExternalProducerContractCatalog.GetRequired(
            FinanceExternalProducerContractId.MobilePosCustomerPayment);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMobilePosFoundationService _foundation;
    private readonly IMobilePosMutationExecutionService _mutations;
    private readonly IPaymentService _payments;
    private readonly IFinanceAccessScopeService _financeAccessScope;

    public MobilePosCollectionService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IMobilePosFoundationService foundation,
        IMobilePosMutationExecutionService mutations,
        IPaymentService payments,
        IFinanceAccessScopeService financeAccessScope)
    {
        _db = db;
        _currentUser = currentUser;
        _foundation = foundation;
        _mutations = mutations;
        _payments = payments;
        _financeAccessScope = financeAccessScope;
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

    public async Task<MobilePosCollectionResultDto> CompleteAsync(
        MobilePosCompleteCollectionRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bootstrap = await _foundation.GetBootstrapAsync(
            Required(request.InstallationId, 200, "installation ID"), cancellationToken);
        if (!bootstrap.CurrentTillSessionId.HasValue)
            throw Reject("MOBILE_POS_TILL_SESSION_REQUIRED", "Open your assigned till session before collecting a customer payment.");

        var context = new CollectionExecutionContext(
            bootstrap.Device.Id,
            bootstrap.Store.Id,
            bootstrap.Till.Id,
            bootstrap.CurrentTillSessionId.Value,
            false,
            null,
            null,
            false);
        var execution = await _mutations.ExecuteAsync<MobilePosCompleteCollectionRequestDto, MobilePosCollectionResultDto>(
            bootstrap.Device.Id,
            request.ClientMutationId,
            CommandType,
            SchemaVersion,
            request,
            token => CompleteCoreAsync(context, request, token),
            cancellationToken);
        execution.Result.MutationReceiptId = execution.ReceiptId;
        execution.Result.IsReplay = execution.IsReplay;
        return execution.Result;
    }

    public async Task<MobilePosCollectionResultDto> CompleteOfflineAsync(
        MobilePosCompleteCollectionRequestDto request,
        MobilePosOfflineGrantAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authorization);
        if (!request.OccurredAtUtc.HasValue)
            throw Reject("MOBILE_POS_OFFLINE_OCCURRED_AT_REQUIRED", "An offline collection must retain the time recorded on the device.");

        var grant = authorization.Grant;
        var context = new CollectionExecutionContext(
            grant.MobilePosDeviceId,
            grant.MobilePosStoreId,
            grant.MobilePosTillId,
            grant.CashierTillSessionId,
            true,
            grant.Id,
            grant.PolicySnapshotHash,
            authorization.Policy.AllowPartialPayment
                && authorization.Policy.AllowedCommandTypes.Contains("PartialPayment", StringComparer.Ordinal));
        var execution = await _mutations.ExecuteAsync<MobilePosCompleteCollectionRequestDto, MobilePosCollectionResultDto>(
            grant.MobilePosDeviceId,
            request.ClientMutationId,
            CommandType,
            SchemaVersion,
            request,
            token => CompleteCoreAsync(context, request, token),
            cancellationToken);
        execution.Result.MutationReceiptId = execution.ReceiptId;
        execution.Result.IsReplay = execution.IsReplay;
        return execution.Result;
    }

    private async Task<MobilePosMutationCompletion<MobilePosCollectionResultDto>> CompleteCoreAsync(
        CollectionExecutionContext context,
        MobilePosCompleteCollectionRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var tenantId = TenantId;
        var userId = UserId;
        var now = DateTime.UtcNow;
        var localReference = Required(request.LocalReference, 100, "local reference");

        var device = await _db.MobilePosDevices.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == context.DeviceId && !item.IsDeleted,
            cancellationToken) ?? throw Reject("MOBILE_POS_DEVICE_NOT_FOUND", "The enrolled device was not found.");
        if (device.Status != MobilePosDeviceStatus.Active
            || device.MobilePosStoreId != context.StoreId
            || device.MobilePosTillId != context.TillId)
        {
            throw Reject("MOBILE_POS_DEVICE_ASSIGNMENT_CHANGED", "The device assignment changed. Refresh Mobile POS before retrying.");
        }

        if (!context.RecordedOffline)
        {
            var assignmentActive = await _db.MobilePosUserStoreAssignments.AnyAsync(item =>
                item.TenantId == tenantId && item.UserId == userId && item.IsActive && !item.IsDeleted
                && item.MobilePosStoreId == context.StoreId
                && item.EffectiveFromUtc <= now
                && (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc > now),
                cancellationToken);
            if (!assignmentActive)
                throw Reject("MOBILE_POS_STORE_ASSIGNMENT_EXPIRED", "Your Mobile POS store assignment is no longer active.");
        }

        var store = await _db.MobilePosStores.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == context.StoreId && !item.IsDeleted,
            cancellationToken) ?? throw Reject("MOBILE_POS_STORE_NOT_FOUND", "The assigned Mobile POS store was not found.");
        var till = await _db.MobilePosTills.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == context.TillId
            && item.MobilePosStoreId == store.Id && !item.IsDeleted,
            cancellationToken) ?? throw Reject("MOBILE_POS_TILL_NOT_FOUND", "The assigned Mobile POS till was not found.");
        if (store.Status != MobilePosStoreStatus.Active || till.Status != MobilePosTillStatus.Active)
            throw Reject("MOBILE_POS_STORE_OR_TILL_INACTIVE", "The assigned store or till is no longer active.");

        var session = await _db.CashierTillSessions.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == context.TillSessionId && !item.IsDeleted,
            cancellationToken) ?? throw Reject("MOBILE_POS_TILL_SESSION_NOT_FOUND", "The till session was not found.");
        var allowedSessionState = session.Status == CashierTillSessionStatus.Open
            || (context.RecordedOffline && session.Status == CashierTillSessionStatus.PendingReview);
        if (!allowedSessionState
            || session.CashierUserId != userId
            || session.LiquidityAccountId != till.LiquidityAccountId)
        {
            throw Reject("MOBILE_POS_TILL_SESSION_CHANGED", "The till session is no longer open for this operator and till.");
        }

        var customerRole = await _db.EligibleMobilePosCustomerRoles(tenantId, now)
            .AsNoTracking()
            .Include(role => role.BusinessPartner)
            .SingleOrDefaultAsync(role => role.Id == request.BusinessPartnerRoleId
                && role.BusinessPartnerId == request.BusinessPartnerId, cancellationToken)
            ?? throw Reject("MOBILE_POS_CUSTOMER_INELIGIBLE", "The selected customer is not active, approved, and transaction ready.");
        if (!string.IsNullOrWhiteSpace(customerRole.BusinessPartner.Currency)
            && !string.Equals(customerRole.BusinessPartner.Currency, store.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw Reject("MOBILE_POS_CUSTOMER_CURRENCY_MISMATCH",
                $"The selected customer uses {customerRole.BusinessPartner.Currency}; this store transacts in {store.CurrencyCode}.");
        }

        var invoiceIds = request.Allocations.Select(item => item.InvoiceId).ToArray();
        var invoices = await _db.Invoices.Where(invoice =>
                invoice.TenantId == tenantId && invoiceIds.Contains(invoice.Id) && !invoice.IsDeleted)
            .ToDictionaryAsync(invoice => invoice.Id, cancellationToken);
        if (invoices.Count != invoiceIds.Length)
            throw Reject("MOBILE_POS_COLLECTION_INVOICE_NOT_FOUND", "One or more selected invoices were not found in the current tenant.");

        foreach (var allocation in request.Allocations)
        {
            var invoice = invoices[allocation.InvoiceId];
            if (invoice.BusinessPartnerId != request.BusinessPartnerId
                || invoice.BusinessPartnerRoleId != request.BusinessPartnerRoleId)
                throw Reject("MOBILE_POS_COLLECTION_CUSTOMER_MISMATCH", "Every invoice must belong to the selected customer and Customer role.");
            if (!string.Equals(invoice.CurrencyCode, store.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw Reject("MOBILE_POS_COLLECTION_CURRENCY_MISMATCH", "Every invoice must use the assigned store currency.");
            if (invoice.Status is not (InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue)
                || (invoice.IsOpeningBalance && !invoice.JournalEntryId.HasValue))
                throw Reject("MOBILE_POS_COLLECTION_INVOICE_NOT_OUTSTANDING", $"Invoice {invoice.InvoiceNumber} is not open for collection.");
            if (RoundMoney(allocation.Amount) > RoundMoney(invoice.BalanceAmount))
                throw Reject("MOBILE_POS_COLLECTION_EXCEEDS_BALANCE", $"The amount for invoice {invoice.InvoiceNumber} exceeds its outstanding balance.");
            if (context.RecordedOffline && RoundMoney(allocation.Amount) < RoundMoney(invoice.BalanceAmount)
                && !context.AllowPartialPayment)
            {
                throw Reject("MOBILE_POS_OFFLINE_PARTIAL_PAYMENT_NOT_ALLOWED",
                    "The signed offline policy does not authorize partial invoice payments.");
            }
        }

        var tenderQuery = _db.MobilePosTillPaymentMethods.AsNoTracking()
            .Where(mapping => mapping.TenantId == tenantId && mapping.MobilePosTillId == till.Id
                && !mapping.IsDeleted && mapping.PaymentMethod.IsActive);
        tenderQuery = context.RecordedOffline
            ? tenderQuery.Where(mapping => mapping.AllowOffline)
            : tenderQuery.Where(mapping => mapping.AllowOnline);
        var allowedTenders = await tenderQuery.Include(mapping => mapping.PaymentMethod)
            .ToDictionaryAsync(mapping => mapping.PaymentMethodId, cancellationToken);
        await ValidateTendersAsync(
            request.Tenders,
            allowedTenders,
            store.CurrencyCode,
            context.RecordedOffline,
            cancellationToken);

        var allocationTotal = RoundMoney(request.Allocations.Sum(item => item.Amount));
        var tenderTotal = RoundMoney(request.Tenders.Sum(item => item.Amount));
        if (allocationTotal != tenderTotal)
            throw Reject("MOBILE_POS_COLLECTION_TOTAL_MISMATCH", "Tender amounts must equal the total invoice allocation.");

        var remainingAllocations = request.Allocations
            .Select(item => new RemainingAllocation(item.InvoiceId, item.Amount))
            .ToList();
        var paymentResults = new List<CustomerPaymentDto>(request.Tenders.Count);
        for (var tenderIndex = 0; tenderIndex < request.Tenders.Count; tenderIndex++)
        {
            var tender = request.Tenders[tenderIndex];
            var mapping = allowedTenders[tender.PaymentMethodId];
            var tenderAllocations = AllocateTender(tender.Amount, remainingAllocations, localReference);
            var liquidityAccountId = tender.LiquidityAccountId;
            if (!liquidityAccountId.HasValue && mapping.PaymentMethod.Type == PaymentMethodType.Cash)
                liquidityAccountId = till.LiquidityAccountId;

            var payment = await _payments.CreateAsync(new PaymentCreateDto
            {
                BusinessPartnerId = request.BusinessPartnerId,
                BusinessPartnerRoleId = request.BusinessPartnerRoleId,
                PaymentDate = session.BusinessDate.Date,
                TotalAmount = tender.Amount,
                PaymentMethodId = mapping.PaymentMethodId,
                PaymentMethod = mapping.PaymentMethod.Name,
                CurrencyCode = store.CurrencyCode,
                ExchangeRateId = request.ExchangeRateId,
                ExchangeRate = 1m,
                BankAccountId = tender.BankAccountId,
                LiquidityAccountId = liquidityAccountId,
                TransactionReference = Clean(tender.ExternalReference),
                Notes = $"Mobile POS {(context.RecordedOffline ? "offline " : string.Empty)}collection {localReference}; tender {tenderIndex + 1}/{request.Tenders.Count}",
                Allocations = tenderAllocations
            }, PaymentProducer, cancellationToken);
            paymentResults.Add(payment);
        }

        if (remainingAllocations.Any(item => RoundMoney(item.RemainingAmount) != 0m))
            throw Reject("MOBILE_POS_COLLECTION_ALLOCATION_INCOMPLETE", "The collection could not be distributed across every selected invoice.");

        var occurredAtUtc = NormalizeOccurredAt(request.OccurredAtUtc, now);
        var collection = new MobilePosCollection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MobilePosStoreId = store.Id,
            MobilePosTillId = till.Id,
            CashierTillSessionId = session.Id,
            MobilePosDeviceId = device.Id,
            OperatorUserId = userId,
            ClientMutationId = Required(request.ClientMutationId, 100, "client mutation ID"),
            LocalReference = localReference,
            BusinessPartnerId = request.BusinessPartnerId,
            BusinessPartnerRoleId = request.BusinessPartnerRoleId,
            BusinessDate = session.BusinessDate.Date,
            OccurredAtUtc = occurredAtUtc,
            CurrencyCode = store.CurrencyCode,
            TotalAmount = allocationTotal,
            Status = MobilePosCollectionStatus.Completed,
            MobilePosOfflineGrantId = context.OfflineGrantId,
            OfflinePolicySnapshotHash = context.OfflinePolicySnapshotHash,
            SynchronizedAtUtc = now,
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = userId
        };
        foreach (var allocation in request.Allocations.Select((value, index) => (value, index)))
        {
            collection.Allocations.Add(new MobilePosCollectionAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Sequence = allocation.index + 1,
                InvoiceId = allocation.value.InvoiceId,
                InvoiceNumber = invoices[allocation.value.InvoiceId].InvoiceNumber,
                Amount = allocation.value.Amount,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = userId
            });
        }
        foreach (var payment in paymentResults.Select((value, index) => (value, index)))
        {
            var tender = request.Tenders[payment.index];
            collection.Tenders.Add(new MobilePosCollectionTender
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Sequence = payment.index + 1,
                PaymentMethodId = tender.PaymentMethodId,
                Amount = tender.Amount,
                ExternalReference = Clean(tender.ExternalReference),
                LiquidityAccountId = payment.value.LiquidityAccountId,
                BankAccountId = payment.value.BankAccountId,
                CustomerPaymentId = payment.value.Id,
                PaymentNumber = payment.value.PaymentNumber,
                PaymentStatus = payment.value.Status,
                WasRecordedOffline = context.RecordedOffline,
                Status = MobilePosTenderStatus.Completed,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = userId
            });
        }

        _db.MobilePosCollections.Add(collection);
        await _db.SaveChangesAsync(cancellationToken);
        var result = new MobilePosCollectionResultDto
        {
            CollectionId = collection.Id,
            LocalReference = collection.LocalReference,
            BusinessPartnerId = collection.BusinessPartnerId,
            BusinessPartnerRoleId = collection.BusinessPartnerRoleId,
            CustomerCode = customerRole.BusinessPartner.PartnerCode,
            CustomerName = customerRole.BusinessPartner.PartnerName,
            CurrencyCode = collection.CurrencyCode,
            TotalAmount = collection.TotalAmount,
            BusinessDate = collection.BusinessDate,
            Allocations = collection.Allocations.OrderBy(item => item.Sequence).Select(item =>
                new MobilePosCollectionAllocationResultDto
                {
                    InvoiceId = item.InvoiceId,
                    InvoiceNumber = item.InvoiceNumber,
                    Amount = item.Amount
                }).ToArray(),
            Tenders = collection.Tenders.OrderBy(item => item.Sequence).Select(item =>
                new MobilePosCollectionTenderResultDto
                {
                    TenderId = item.Id,
                    PaymentMethodId = item.PaymentMethodId,
                    Amount = item.Amount,
                    CustomerPaymentId = item.CustomerPaymentId,
                    PaymentNumber = item.PaymentNumber,
                    PaymentStatus = item.PaymentStatus ?? string.Empty
                }).ToArray()
        };
        return new MobilePosMutationCompletion<MobilePosCollectionResultDto>(
            result,
            CanonicalInvoiceId: request.Allocations.Count == 1 ? request.Allocations[0].InvoiceId : null,
            CanonicalCustomerPaymentIds: paymentResults.Select(item => item.Id).ToArray(),
            MobilePosCollectionId: collection.Id);
    }

    private async Task ValidateTendersAsync(
        IReadOnlyList<MobilePosTenderInputDto> tenders,
        IReadOnlyDictionary<Guid, MobilePosTillPaymentMethod> allowedTenders,
        string currencyCode,
        bool recordedOffline,
        CancellationToken cancellationToken)
    {
        if (tenders.Select(item => item.PaymentMethodId).Any(id => !allowedTenders.ContainsKey(id)))
            throw Reject("MOBILE_POS_TENDER_NOT_ALLOWED",
                $"One or more tender methods are not active and enabled for {(recordedOffline ? "offline" : "online")} use at this till.");

        foreach (var tender in tenders)
        {
            var mapping = allowedTenders[tender.PaymentMethodId];
            var reference = Clean(tender.ExternalReference);
            if ((mapping.PaymentMethod.RequiresReference || mapping.RequireExternalAuthorizationReference) && reference is null)
                throw Reject("MOBILE_POS_TENDER_REFERENCE_REQUIRED", $"{mapping.PaymentMethod.Name} requires a provider or transaction reference.");
            if (mapping.PaymentMethod.RequiresBankAccount && !tender.BankAccountId.HasValue)
                throw Reject("MOBILE_POS_TENDER_BANK_ACCOUNT_REQUIRED", $"{mapping.PaymentMethod.Name} requires a bank account.");
            if (!mapping.PaymentMethod.RequiresBankAccount && tender.BankAccountId.HasValue)
                throw Reject("MOBILE_POS_TENDER_BANK_ACCOUNT_NOT_ALLOWED", $"{mapping.PaymentMethod.Name} does not accept a bank account selection.");
            if (tender.BankAccountId.HasValue && tender.LiquidityAccountId.HasValue)
                throw Reject("MOBILE_POS_TENDER_DESTINATION_INVALID", "A tender cannot select both a bank account and a liquidity account.");
        }

        var bankAccountIds = tenders.Where(item => item.BankAccountId.HasValue)
            .Select(item => item.BankAccountId!.Value).Distinct().ToArray();
        if (bankAccountIds.Length == 0) return;
        var permittedIds = await _financeAccessScope.GetPermittedBankAccountIdsAsync(
            FinanceAccessLevel.Operate, cancellationToken);
        var eligibleQuery = _db.BankAccounts.AsNoTracking().Where(account =>
            account.TenantId == TenantId && bankAccountIds.Contains(account.Id)
            && account.IsActive && !account.IsDeleted && account.GLAccountId.HasValue
            && account.Currency == currencyCode);
        if (permittedIds != null)
        {
            var permitted = permittedIds.ToArray();
            eligibleQuery = eligibleQuery.Where(account => permitted.Contains(account.Id));
        }
        var eligibleIds = await eligibleQuery.Select(account => account.Id).ToArrayAsync(cancellationToken);
        if (eligibleIds.Length != bankAccountIds.Length)
            throw Reject("MOBILE_POS_TENDER_BANK_ACCOUNT_INELIGIBLE",
                "One or more selected bank accounts are inactive, outside your Finance scope, use another currency, or lack a GL account mapping.");
    }

    private static List<InvoiceAllocationDto> AllocateTender(
        decimal tenderAmount,
        IReadOnlyList<RemainingAllocation> allocations,
        string localReference)
    {
        var remainingTender = tenderAmount;
        var result = new List<InvoiceAllocationDto>();
        foreach (var allocation in allocations)
        {
            if (remainingTender <= 0m) break;
            if (allocation.RemainingAmount <= 0m) continue;
            var amount = Math.Min(remainingTender, allocation.RemainingAmount);
            result.Add(new InvoiceAllocationDto
            {
                InvoiceId = allocation.InvoiceId,
                AllocatedAmount = amount,
                PaymentCurrencyAmount = amount,
                Notes = $"Mobile POS collection {localReference}"
            });
            allocation.RemainingAmount -= amount;
            remainingTender -= amount;
        }
        if (RoundMoney(remainingTender) != 0m)
            throw Reject("MOBILE_POS_COLLECTION_TENDER_UNALLOCATED", "A tender amount could not be allocated completely to the selected invoices.");
        return result;
    }

    private static void ValidateRequest(MobilePosCompleteCollectionRequestDto request)
    {
        _ = Required(request.ClientMutationId, 100, "client mutation ID");
        _ = Required(request.LocalReference, 100, "local reference");
        if (request.BusinessPartnerId == Guid.Empty || request.BusinessPartnerRoleId == Guid.Empty)
            throw Reject("MOBILE_POS_COLLECTION_CUSTOMER_REQUIRED", "Select an approved customer before collecting a payment.");
        if (request.Allocations is null
            || request.Allocations.Count is < 1 or > 50
            || request.Allocations.Any(item => item.InvoiceId == Guid.Empty || item.Amount <= 0m)
            || request.Allocations.Select(item => item.InvoiceId).Distinct().Count() != request.Allocations.Count)
            throw Reject("MOBILE_POS_COLLECTION_ALLOCATIONS_INVALID", "Select between 1 and 50 unique invoices with positive allocation amounts.");
        if (request.Tenders is null
            || request.Tenders.Count is < 1 or > 10
            || request.Tenders.Any(item => item.PaymentMethodId == Guid.Empty || item.Amount <= 0m)
            || request.Tenders.Select(item => item.PaymentMethodId).Distinct().Count() != request.Tenders.Count)
            throw Reject("MOBILE_POS_TENDERS_INVALID", "Select between 1 and 10 unique tender methods with positive amounts.");
        if (request.Tenders.Any(item => Clean(item.ExternalReference)?.Length > 150))
            throw Reject("MOBILE_POS_TENDER_REFERENCE_INVALID", "Tender authorization references cannot exceed 150 characters.");
    }

    private static DateTime NormalizeOccurredAt(DateTime? value, DateTime now)
    {
        if (!value.HasValue) return now;
        var occurredAt = value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime();
        if (occurredAt > now.AddMinutes(5))
            throw Reject("MOBILE_POS_DEVICE_CLOCK_AHEAD", "The device collection time is too far ahead of the server clock.");
        return occurredAt;
    }

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Required(string? value, int maximumLength, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
            throw Reject("MOBILE_POS_REQUIRED_VALUE_INVALID", $"The {label} must contain 1 to {maximumLength} characters.");
        return normalized;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MobilePosCommandRejectedException Reject(string code, string detail) => new(code, detail);

    private sealed record CollectionExecutionContext(
        Guid DeviceId,
        Guid StoreId,
        Guid TillId,
        Guid TillSessionId,
        bool RecordedOffline,
        Guid? OfflineGrantId,
        string? OfflinePolicySnapshotHash,
        bool AllowPartialPayment);

    private sealed class RemainingAllocation(Guid invoiceId, decimal remainingAmount)
    {
        public Guid InvoiceId { get; } = invoiceId;
        public decimal RemainingAmount { get; set; } = remainingAmount;
    }
}
