using System.Data;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Ehc;

/// <summary>
/// Finance boundary for money received from a property prospect before that person becomes a
/// Customer Business Partner. The original receipt moves cash exactly once. Conversion later
/// reclassifies the existing prospect liability to the canonical customer-advance liability and
/// creates the AR subledger lot without another cash debit.
/// </summary>
public sealed class ProspectDepositFinancePostingService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IFinancePostingEngine posting,
    IFinanceSourceBookAuthorityService sourceBookAuthorities,
    ILogger<ProspectDepositFinancePostingService> logger) : IProspectDepositFinancePostingService
{
    private const string ReceiptDocumentType = "ProspectDepositReceipt";
    private const string ReceiptPostingAction = "Post";
    private const string CustomerPaymentDocumentType = "CustomerPayment";
    private const string CustomerPaymentPostingAction = "Post";

    public async Task<ProspectDepositFinancePostingResult> PostClearedReceiptAsync(
        ProspectDepositReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var tenantId = RequireTenantAndActor(receipt.TenantId);
        ValidateReceiptForPosting(receipt);

        if (receipt.PostingEventId.HasValue || receipt.JournalEntryId.HasValue)
        {
            var retained = await RequireExactReceiptPostingAsync(receipt, cancellationToken);
            return new(retained.Id, retained.JournalEntryId!.Value, true);
        }

        var settings = await RequireFinanceSettingsAsync(tenantId, cancellationToken);
        var functionalCurrency = Currency(settings.BaseCurrency);
        RequireFunctionalCurrency(receipt.Currency, functionalCurrency);
        var cashAccountId = await ResolveCashAccountAsync(receipt, cancellationToken);
        await RequirePostingAccountAsync(cashAccountId, "bank/liquidity", AccountType.Asset,
            allowControlAccount: true, requireDirectPosting: false, cancellationToken);
        await RequirePostingAccountAsync(receipt.DepositLiabilityAccountId, "prospect deposit liability",
            AccountType.Liability, allowControlAccount: false, requireDirectPosting: true, cancellationToken);
        var book = await RequirePrimaryPostingBookAsync(tenantId, functionalCurrency, cancellationToken);

        var result = await posting.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = "AR",
            OriginModuleCode = FinanceModuleLockCatalog.Sales,
            SourceDocumentType = ReceiptDocumentType,
            SourceDocumentId = receipt.Id,
            SourceDocumentTenantId = tenantId,
            PostingAction = ReceiptPostingAction,
            SourceDocumentReference = receipt.ReceiptNumber,
            Description = $"Property prospect deposit {receipt.ReceiptNumber}",
            PostingDate = (receipt.ClearedAt ?? DateTime.UtcNow).Date,
            JournalType = "Property Prospect Deposit",
            AccountingBookCode = book.Code,
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = $"AR:ProspectDepositReceipt:{tenantId:N}:{receipt.Id:N}:Post",
            ReturnExistingOnDuplicate = true,
            Lines =
            [
                Line(cashAccountId, StableLineId(receipt.Id, "cash"),
                    $"Cash received for prospect deposit {receipt.ReceiptNumber}", receipt.Amount, 0m,
                    functionalCurrency, receipt.ReceiptNumber, 1, "PROSPECT-DEPOSIT-CASH"),
                Line(receipt.DepositLiabilityAccountId, StableLineId(receipt.Id, "liability"),
                    $"Prospect deposit liability {receipt.ReceiptNumber}", 0m, receipt.Amount,
                    functionalCurrency, receipt.ReceiptNumber, 2, "PROSPECT-DEPOSIT-LIABILITY")
            ]
        }, cancellationToken);

        return new(result.PostingEventId, result.JournalEntryId, result.WasDuplicate);
    }

    public async Task<ProspectDepositFinancePostingResult> ReverseReceiptAsync(
        ProspectDepositReceipt receipt,
        string reason,
        DateTime? reversalDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        RequireTenantAndActor(receipt.TenantId);
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? throw new InvalidOperationException("A prospect deposit reversal reason is required.")
            : reason.Trim();
        if (!receipt.PostingEventId.HasValue || !receipt.JournalEntryId.HasValue)
            throw new InvalidOperationException("The prospect deposit does not have a Finance posting to reverse.");
        if (receipt.TransferredToCustomerAdvanceAt.HasValue || receipt.CustomerPaymentId.HasValue)
            throw new InvalidOperationException(
                "The prospect deposit has already been transferred to a customer advance. Reverse that governed AR lineage first.");

        _ = await RequireExactReceiptPostingAsync(receipt, cancellationToken);
        var result = await posting.ReverseAsync(
            receipt.PostingEventId.Value,
            normalizedReason,
            reversalDate?.Date,
            cancellationToken);
        return new(result.PostingEventId, result.JournalEntryId, result.WasDuplicate);
    }

    public async Task<ProspectDepositCustomerAdvanceTransferResult> TransferToCustomerAdvanceAsync(
        ProspectDepositReceipt receipt,
        Guid businessPartnerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var tenantId = RequireTenantAndActor(receipt.TenantId);
        if (businessPartnerId == Guid.Empty)
            throw new InvalidOperationException("A Customer Business Partner is required for the advance transfer.");
        if (!string.Equals(receipt.Status, ProspectDepositReceiptStatuses.Cleared, StringComparison.OrdinalIgnoreCase)
            || !receipt.PostingEventId.HasValue || !receipt.JournalEntryId.HasValue || receipt.ReversedAt.HasValue)
            throw new InvalidOperationException("Only a posted, cleared and unreversed prospect deposit can become a customer advance.");
        if (receipt.BusinessPartnerId.HasValue && receipt.BusinessPartnerId.Value != businessPartnerId)
            throw new InvalidOperationException("The prospect deposit is already linked to a different Business Partner.");

        var originalPosting = await RequireExactReceiptPostingAsync(receipt, cancellationToken);
        var paymentId = StableEntityId(receipt.Id, "customer-advance");
        if (receipt.CustomerPaymentId.HasValue && receipt.CustomerPaymentId.Value != paymentId)
            throw new InvalidOperationException("Prospect deposit is linked to a conflicting customer-advance payment.");
        if (receipt.TransferredToCustomerAdvanceAt.HasValue && !receipt.CustomerPaymentId.HasValue)
            throw new InvalidOperationException("Prospect deposit customer-advance transfer lineage is incomplete.");

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;

            try
            {
                var existing = await db.Set<CustomerPayment>().SingleOrDefaultAsync(x =>
                    x.TenantId == tenantId && x.Id == paymentId && !x.IsDeleted, cancellationToken);
                if (existing is not null)
                {
                    var duplicate = await RequireExactCustomerAdvanceAsync(
                        existing, receipt, businessPartnerId, cancellationToken);
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return duplicate;
                }

                var counterparty = await RequireApprovedCustomerAsync(
                    tenantId, businessPartnerId, DateTime.UtcNow.Date, cancellationToken);
                var settings = await RequireFinanceSettingsAsync(tenantId, cancellationToken);
                var functionalCurrency = Currency(settings.BaseCurrency);
                RequireFunctionalCurrency(receipt.Currency, functionalCurrency);
                var customerAdvanceAccountId = settings.CustomerAdvanceAccountId
                    ?? throw new InvalidOperationException("Configure the customer advance account in Finance settings before prospect conversion.");
                await RequirePostingAccountAsync(receipt.DepositLiabilityAccountId, "prospect deposit liability",
                    AccountType.Liability, allowControlAccount: false, requireDirectPosting: true, cancellationToken);
                await RequirePostingAccountAsync(customerAdvanceAccountId, "customer advance",
                    AccountType.Liability, allowControlAccount: false, requireDirectPosting: true, cancellationToken);

                var transferDate = DateTime.UtcNow.Date;
                var payment = new CustomerPayment
                {
                    Id = paymentId,
                    TenantId = tenantId,
                    PaymentNumber = receipt.ReceiptNumber,
                    ReferenceNumber = receipt.ReceiptNumber,
                    BusinessPartnerId = counterparty.Partner.Id,
                    BusinessPartnerRoleId = counterparty.Role.Id,
                    BusinessPartnerArProfileVersionId = counterparty.Profile.Id,
                    BusinessPartnerCode = counterparty.Partner.PartnerCode,
                    BusinessPartnerName = counterparty.Partner.PartnerName,
                    BusinessPartnerLegalName = counterparty.Partner.LegalName,
                    BusinessPartnerTaxIdentificationNumber = counterparty.Partner.TaxIdentificationNumber,
                    PaymentDate = transferDate,
                    TotalAmount = receipt.Amount,
                    AllocatedAmount = 0m,
                    IsCustomerAdvance = true,
                    PaymentMethod = receipt.PaymentMethod,
                    CurrencyCode = functionalCurrency,
                    ExchangeRate = 1m,
                    BankAccountId = receipt.BankAccountId,
                    LiquidityAccountId = receipt.LiquidityAccountId,
                    TransactionReference = receipt.TransactionReference,
                    Notes = $"Customer advance reclassified from property prospect deposit {receipt.ReceiptNumber}; " +
                            $"original posting event {receipt.PostingEventId:D}. No additional cash was posted.",
                    Status = "Cleared",
                    ClearedDate = transferDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = currentUser.UserName ?? "system",
                    CreatedById = ActorId()
                };
                db.Set<CustomerPayment>().Add(payment);
                await db.SaveChangesAsync(cancellationToken);

                var transferPosting = await posting.PostAsync(new FinancePostingRequestV2Dto
                {
                    SourceModule = "AR",
                    OriginModuleCode = FinanceModuleLockCatalog.Finance,
                    SourceDocumentType = CustomerPaymentDocumentType,
                    SourceDocumentId = payment.Id,
                    SourceDocumentTenantId = tenantId,
                    PostingAction = CustomerPaymentPostingAction,
                    SourceDocumentReference = payment.PaymentNumber,
                    Description = $"Reclassify prospect deposit {receipt.ReceiptNumber} to customer advance",
                    PostingDate = transferDate,
                    JournalType = "Customer Advance Transfer",
                    AccountingBookCode = originalPosting.BookClassification,
                    FunctionalCurrencyCode = originalPosting.FunctionalCurrencyCode,
                    IdempotencyKey = $"AR:ProspectDepositCustomerAdvance:{tenantId:N}:{receipt.Id:N}:{businessPartnerId:N}:Post",
                    ReturnExistingOnDuplicate = true,
                    Lines =
                    [
                        Line(receipt.DepositLiabilityAccountId, StableLineId(payment.Id, "prospect-liability-release"),
                            $"Release prospect deposit liability {receipt.ReceiptNumber}", receipt.Amount, 0m,
                            functionalCurrency, receipt.ReceiptNumber, 1, "PROSPECT-DEPOSIT-LIABILITY-RELEASE"),
                        Line(customerAdvanceAccountId, StableLineId(payment.Id, "customer-advance"),
                            $"Recognize customer advance {receipt.ReceiptNumber}", 0m, receipt.Amount,
                            functionalCurrency, receipt.ReceiptNumber, 2, "AR-CUSTOMER-ADVANCE")
                    ]
                }, cancellationToken);

                var authority = await sourceBookAuthorities.RetainExistingPostedOriginalAsync(
                    new FinanceSourceBookAuthorityFreezeRequest
                    {
                        OriginModuleCode = FinanceModuleLockCatalog.Finance,
                        SourceDocumentType = CustomerPaymentDocumentType,
                        SourceDocumentId = payment.Id,
                        PostingAction = CustomerPaymentPostingAction,
                        EffectiveDate = transferPosting.PostingDate.Date,
                        TransactionCurrencyCode = functionalCurrency,
                        FreezeStage = FinanceSourceBookAuthorityFreezeStages.LegacyPosted
                    },
                    transferPosting.JournalEntryId,
                    transferPosting.PostingEventId,
                    cancellationToken);

                payment.JournalEntryId = transferPosting.JournalEntryId;
                payment.SourceBookAuthorityId = authority.AuthorityId;
                payment.UpdatedAt = DateTime.UtcNow;
                payment.UpdatedBy = currentUser.UserName;
                payment.LastModifiedById = ActorId();
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);

                logger.LogInformation(
                    "Prospect deposit {ProspectDepositReceiptId} was reclassified to customer advance {CustomerPaymentId} without a second cash posting.",
                    receipt.Id,
                    payment.Id);
                return new(transferPosting.PostingEventId, transferPosting.JournalEntryId,
                    payment.Id, transferPosting.WasDuplicate);
            }
            catch
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<ProspectDepositCustomerAdvanceTransferResult> RequireExactCustomerAdvanceAsync(
        CustomerPayment payment,
        ProspectDepositReceipt receipt,
        Guid businessPartnerId,
        CancellationToken cancellationToken)
    {
        if (payment.BusinessPartnerId != businessPartnerId || !payment.IsCustomerAdvance
            || payment.TotalAmount != receipt.Amount
            || !string.Equals(Currency(payment.CurrencyCode), Currency(receipt.Currency), StringComparison.Ordinal)
            || !payment.JournalEntryId.HasValue)
            throw new InvalidOperationException("Existing customer-advance lineage conflicts with this prospect deposit.");

        var postingEvent = await db.FinancePostingEvents.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == receipt.TenantId && !x.IsDeleted
            && x.SourceModule == "AR"
            && x.OriginModuleCode == FinanceModuleLockCatalog.Finance
            && x.SourceDocumentType == CustomerPaymentDocumentType
            && x.SourceDocumentId == payment.Id
            && x.PostingAction == CustomerPaymentPostingAction
            && x.PostingStatus == "Posted"
            && x.JournalEntryId == payment.JournalEntryId.Value
            && x.TotalDebitAmount == receipt.Amount
            && x.TotalCreditAmount == receipt.Amount,
            cancellationToken) ?? throw new InvalidOperationException(
                "Existing customer advance is missing its exact posted Finance lineage.");
        if (receipt.CustomerAdvanceTransferPostingEventId.HasValue
            && receipt.CustomerAdvanceTransferPostingEventId.Value != postingEvent.Id
            || receipt.CustomerAdvanceTransferJournalEntryId.HasValue
            && receipt.CustomerAdvanceTransferJournalEntryId.Value != payment.JournalEntryId.Value)
            throw new InvalidOperationException("Prospect deposit customer-advance transfer points to different Finance evidence.");

        if (!payment.SourceBookAuthorityId.HasValue)
        {
            var authority = await sourceBookAuthorities.RetainExistingPostedOriginalAsync(
                new FinanceSourceBookAuthorityFreezeRequest
                {
                    OriginModuleCode = FinanceModuleLockCatalog.Finance,
                    SourceDocumentType = CustomerPaymentDocumentType,
                    SourceDocumentId = payment.Id,
                    PostingAction = CustomerPaymentPostingAction,
                    EffectiveDate = postingEvent.PostingDate.Date,
                    TransactionCurrencyCode = Currency(payment.CurrencyCode),
                    FreezeStage = FinanceSourceBookAuthorityFreezeStages.LegacyPosted
                }, payment.JournalEntryId.Value, postingEvent.Id, cancellationToken);
            payment.SourceBookAuthorityId = authority.AuthorityId;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.UpdatedBy = currentUser.UserName;
            await db.SaveChangesAsync(cancellationToken);
        }

        return new(postingEvent.Id, payment.JournalEntryId.Value, payment.Id, true);
    }

    private async Task<FinancePostingEvent> RequireExactReceiptPostingAsync(
        ProspectDepositReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (!receipt.PostingEventId.HasValue || !receipt.JournalEntryId.HasValue)
            throw new InvalidOperationException("Prospect deposit Finance lineage is incomplete.");
        var postingEvent = await db.FinancePostingEvents.AsNoTracking().Include(x => x.JournalEntry).SingleOrDefaultAsync(x =>
            x.Id == receipt.PostingEventId.Value
            && x.TenantId == receipt.TenantId
            && !x.IsDeleted
            && x.SourceModule == "AR"
            && x.OriginModuleCode == FinanceModuleLockCatalog.Sales
            && x.SourceDocumentType == ReceiptDocumentType
            && x.SourceDocumentId == receipt.Id
            && x.PostingAction == ReceiptPostingAction
            && x.PostingStatus == "Posted"
            && x.JournalEntryId == receipt.JournalEntryId.Value
            && x.TotalDebitAmount == receipt.Amount
            && x.TotalCreditAmount == receipt.Amount,
            cancellationToken) ?? throw new InvalidOperationException(
                "Prospect deposit Finance lineage does not match its exact posted event and journal.");
        if (postingEvent.JournalEntry is null
            || postingEvent.JournalEntry.TenantId != receipt.TenantId
            || !string.Equals(postingEvent.JournalEntry.PostingStatus, "Posted", StringComparison.OrdinalIgnoreCase)
            || postingEvent.JournalEntry.IsReversed
            || postingEvent.JournalEntry.ReversalJournalEntryId.HasValue
            || postingEvent.JournalEntry.SourceDocumentId != receipt.Id
            || !string.Equals(postingEvent.JournalEntry.SourceDocumentType, ReceiptDocumentType, StringComparison.Ordinal))
            throw new InvalidOperationException("Prospect deposit journal evidence is missing, reversed or belongs to another source.");
        return postingEvent;
    }

    private async Task<FinanceSettings> RequireFinanceSettingsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await db.FinanceSettings.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && !x.IsDeleted, cancellationToken)
        ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");

    private async Task<AccountingBook> RequirePrimaryPostingBookAsync(
        Guid tenantId,
        string functionalCurrency,
        CancellationToken cancellationToken)
    {
        var books = await db.AccountingBooks.AsNoTracking().Where(x => x.TenantId == tenantId
            && !x.IsDeleted && x.IsDefault && x.IsActive && x.AllowsPosting).Take(2).ToListAsync(cancellationToken);
        if (books.Count != 1)
            throw new InvalidOperationException("Exactly one active default accounting book is required for prospect deposit posting.");
        var book = books[0];
        if (book.BookType != AccountingBookType.PrimaryFull
            || book.LifecycleStatus != AccountingBookLifecycleStatus.Active)
            throw new InvalidOperationException("The default prospect deposit accounting book must be the active Primary book.");
        if (!string.Equals(Currency(book.FunctionalCurrencyCode), functionalCurrency, StringComparison.Ordinal))
            throw new InvalidOperationException("The default accounting book currency does not match the Finance functional currency.");
        return book;
    }

    private async Task<Guid> ResolveCashAccountAsync(ProspectDepositReceipt receipt, CancellationToken cancellationToken)
    {
        if (receipt.BankAccountId.HasValue == receipt.LiquidityAccountId.HasValue)
            throw new InvalidOperationException("Select exactly one configured bank or liquidity account for the prospect deposit.");
        var currency = Currency(receipt.Currency);
        if (receipt.BankAccountId.HasValue)
        {
            var bank = await db.BankAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receipt.BankAccountId.Value
                && x.TenantId == receipt.TenantId && !x.IsDeleted && x.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("The configured prospect deposit bank account is unavailable.");
            if (!string.Equals(Currency(bank.Currency), currency, StringComparison.Ordinal))
                throw new InvalidOperationException("The prospect deposit bank account currency does not match the receipt currency.");
            return bank.GLAccountId ?? throw new InvalidOperationException(
                "The configured prospect deposit bank account has no GL account mapping.");
        }

        var liquidity = await db.LiquidityAccounts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == receipt.LiquidityAccountId!.Value && x.TenantId == receipt.TenantId
            && !x.IsDeleted && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("The configured prospect deposit liquidity account is unavailable.");
        if (!string.Equals(Currency(liquidity.Currency), currency, StringComparison.Ordinal))
            throw new InvalidOperationException("The prospect deposit liquidity account currency does not match the receipt currency.");
        return liquidity.GLAccountId;
    }

    private async Task RequirePostingAccountAsync(
        Guid accountId,
        string role,
        AccountType requiredType,
        bool allowControlAccount,
        bool requireDirectPosting,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetRequiredFinanceTenantId();
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId
            && x.TenantId == tenantId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException($"The configured {role} account is unavailable for this tenant.");
        if (account.Status != AccountStatus.Active)
            throw new InvalidOperationException($"The configured {role} account is inactive.");
        if (account.AccountType != requiredType)
            throw new InvalidOperationException($"The configured {role} account must be a {requiredType} account.");
        if (!allowControlAccount && account.IsControlAccount)
            throw new InvalidOperationException($"The configured {role} account cannot be a control account.");
        if (requireDirectPosting && !account.AllowDirectPosting)
            throw new InvalidOperationException($"The configured {role} account does not allow direct posting.");
    }

    private async Task<CustomerCounterparty> RequireApprovedCustomerAsync(
        Guid tenantId,
        Guid businessPartnerId,
        DateTime effectiveDate,
        CancellationToken cancellationToken)
    {
        var partner = await db.BusinessPartners.AsNoTracking().SingleOrDefaultAsync(x => x.Id == businessPartnerId
            && x.TenantId == tenantId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The Customer Business Partner was not found for this tenant.");
        if (!partner.IsActive || !string.Equals(partner.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Customer Business Partner must be active and approved before conversion.");

        var roles = await db.Set<BusinessPartnerRole>().AsNoTracking().Where(x => x.TenantId == tenantId
            && x.BusinessPartnerId == partner.Id && !x.IsDeleted
            && x.RoleType == BusinessPartnerRoleType.Customer
            && x.Status == BusinessPartnerRoleStatus.Active).Take(2).ToListAsync(cancellationToken);
        if (roles.Count != 1)
            throw new InvalidOperationException("Exactly one active Customer role is required before prospect conversion.");
        var role = roles[0];
        var profiles = await db.Set<BusinessPartnerArProfileVersion>().AsNoTracking().Where(x =>
            x.TenantId == tenantId && x.BusinessPartnerRoleId == role.Id && !x.IsDeleted
            && x.Status == BusinessPartnerFinanceProfileStatus.Approved
            && x.EffectiveFrom.Date <= effectiveDate.Date
            && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= effectiveDate.Date))
            .OrderByDescending(x => x.VersionNumber).Take(2).ToListAsync(cancellationToken);
        if (profiles.Count != 1)
            throw new InvalidOperationException(
                "Exactly one approved, effective Customer AR profile is required before prospect conversion.");
        return new(partner, role, profiles[0]);
    }

    private Guid RequireTenantAndActor(Guid sourceTenantId)
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("An authenticated internal user is required for prospect deposit Finance posting.");
        var tenantId = currentUser.GetRequiredFinanceTenantId();
        if (sourceTenantId == Guid.Empty || sourceTenantId != tenantId)
            throw new UnauthorizedAccessException("Prospect deposit tenant does not match the authenticated tenant.");
        _ = ActorId();
        return tenantId;
    }

    private Guid ActorId() => Guid.TryParse(currentUser.UserId, out var actor) && actor != Guid.Empty
        ? actor
        : throw new UnauthorizedAccessException("An authenticated user identifier is required for prospect deposit Finance posting.");

    private static void ValidateReceiptForPosting(ProspectDepositReceipt receipt)
    {
        if (receipt.Id == Guid.Empty || receipt.ProspectId == Guid.Empty || receipt.TicketId == Guid.Empty
            || receipt.LeadId == Guid.Empty || receipt.OpportunityId == Guid.Empty)
            throw new InvalidOperationException("Prospect deposit source lineage is incomplete.");
        if (receipt.Amount <= 0m)
            throw new InvalidOperationException("Prospect deposit amount must be positive.");
        if (receipt.DepositLiabilityAccountId == Guid.Empty)
            throw new InvalidOperationException("Prospect deposit liability account is required.");
        if (string.IsNullOrWhiteSpace(receipt.ReceiptNumber))
            throw new InvalidOperationException("Prospect deposit receipt number is required.");
        if (receipt.ReversedAt.HasValue || string.Equals(receipt.Status, ProspectDepositReceiptStatuses.Reversed,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A reversed prospect deposit cannot be posted again.");
    }

    private static void RequireFunctionalCurrency(string receiptCurrency, string functionalCurrency)
    {
        if (!string.Equals(Currency(receiptCurrency), functionalCurrency, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Foreign-currency prospect deposits require approved exchange-rate ID and rate snapshot fields before they can be posted.");
    }

    private static FinancePostingLineDto Line(
        Guid accountId,
        Guid sourceLineId,
        string description,
        decimal debit,
        decimal credit,
        string currency,
        string reference,
        int number,
        string tag) => new()
    {
        AccountId = accountId,
        SourceDocumentLineId = sourceLineId,
        Description = description,
        DebitAmount = debit,
        CreditAmount = credit,
        TransactionCurrency = currency,
        TransactionDebitAmount = debit,
        TransactionCreditAmount = credit,
        SourceReferenceNumber = reference,
        LineNumber = number,
        TransactionTag = tag
    };

    private static string Currency(string? value) => string.IsNullOrWhiteSpace(value)
        ? "GHS"
        : value.Trim().ToUpperInvariant();

    private static Guid StableLineId(Guid sourceId, string role) => StableEntityId(sourceId, $"line:{role}");

    private static Guid StableEntityId(Guid sourceId, string role)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"RHEMA:EHC:PROSPECT-DEPOSIT:{sourceId:D}:{role}"));
        var bytes = hash[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private sealed record CustomerCounterparty(
        BusinessPartner Partner,
        BusinessPartnerRole Role,
        BusinessPartnerArProfileVersion Profile);
}
