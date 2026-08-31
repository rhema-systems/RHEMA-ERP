using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public class SubledgerAdjustmentJournalService : ISubledgerAdjustmentJournalService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly IFinancePostingEngine _postingEngine;
    private readonly ITenantSettingsService _tenantSettingsService;

    public SubledgerAdjustmentJournalService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDocumentNumberingService documentNumberingService,
        IFinancePostingEngine postingEngine,
        ITenantSettingsService tenantSettingsService)
    {
        _context = context;
        _currentUser = currentUser;
        _documentNumberingService = documentNumberingService;
        _postingEngine = postingEngine;
        _tenantSettingsService = tenantSettingsService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName) ? "System" : _currentUser.UserName;

    public async Task<IReadOnlyList<SubledgerAdjustmentJournalDto>> GetAllAsync(
        string? module = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedModule = NormalizeModule(module, allowNull: true);
        var query = _context.SubledgerAdjustmentJournals
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Supplier)
            .Include(a => a.ContraAccount)
            .Include(a => a.JournalEntry)
            .Where(a => a.TenantId == TenantId && !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(normalizedModule))
            query = query.Where(a => a.Module == normalizedModule);

        var adjustments = await query
            .OrderByDescending(a => a.AdjustmentDate)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return adjustments.Select(MapToDto).ToList();
    }

    public async Task<SubledgerAdjustmentJournalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var adjustment = await LoadAdjustmentAsync(id, asNoTracking: true, cancellationToken);
        return adjustment == null ? null : MapToDto(adjustment);
    }

    public async Task<SubledgerAdjustmentJournalDto> CreateAndPostAsync(
        CreateSubledgerAdjustmentJournalDto dto,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var adjustment = await CreateAndPostCoreAsync(dto, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapToDto(adjustment);
    }

    public async Task<SubledgerAdjustmentJournalDto> ReverseAsync(
        Guid id,
        ReverseSubledgerAdjustmentJournalDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("A reversal reason is required.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var original = await LoadAdjustmentAsync(id, asNoTracking: false, cancellationToken)
            ?? throw new KeyNotFoundException($"Subledger adjustment journal '{id}' was not found.");

        if (!string.Equals(original.Status, SubledgerAdjustmentStatuses.Posted, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only posted subledger adjustment journals can be reversed.");
        if (original.ReversalAdjustmentId.HasValue)
            throw new InvalidOperationException("This subledger adjustment journal has already been reversed.");
        if (original.OriginalAdjustmentId.HasValue)
            throw new InvalidOperationException("Reversal adjustment journals cannot be reversed from this action. Reverse the original adjustment instead.");

        var reversalDto = new CreateSubledgerAdjustmentJournalDto
        {
            Module = original.Module,
            Purpose = original.Purpose,
            CustomerId = original.CustomerId,
            SupplierId = original.SupplierId,
            AdjustmentDate = (dto.ReversalDate ?? DateTime.UtcNow).Date,
            DueDate = original.DueDate,
            AdjustmentType = string.Equals(original.AdjustmentType, SubledgerAdjustmentTypes.Debit, StringComparison.OrdinalIgnoreCase)
                ? SubledgerAdjustmentTypes.Credit
                : SubledgerAdjustmentTypes.Debit,
            Amount = original.Amount,
            CurrencyCode = original.CurrencyCode,
            ExchangeRate = original.ExchangeRate,
            ContraAccountId = original.ContraAccountId,
            Reference = $"REV-{original.AdjustmentNumber}",
            Reason = $"Reversal of {original.AdjustmentNumber}: {dto.Reason.Trim()}",
            Notes = original.Notes
        };

        var reversal = await CreateAndPostCoreAsync(reversalDto, original.Id, cancellationToken);

        original.Status = SubledgerAdjustmentStatuses.Reversed;
        original.ReversalAdjustmentId = reversal.Id;
        original.ReversalReason = dto.Reason.Trim();
        original.ReversedAt = DateTime.UtcNow;
        original.UpdatedAt = DateTime.UtcNow;
        original.UpdatedBy = UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var reloaded = await LoadAdjustmentAsync(original.Id, asNoTracking: true, cancellationToken)
            ?? original;
        return MapToDto(reloaded);
    }

    private async Task<SubledgerAdjustmentJournal> CreateAndPostCoreAsync(
        CreateSubledgerAdjustmentJournalDto dto,
        Guid? originalAdjustmentId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required.");

        var module = NormalizeModule(dto.Module, allowNull: false)!;
        var purpose = NormalizePurpose(dto.Purpose, allowRetiredOpeningBalance: originalAdjustmentId.HasValue);
        var adjustmentType = NormalizeAdjustmentType(dto.AdjustmentType);
        if (dto.Amount <= 0)
            throw new InvalidOperationException("Adjustment amount must be greater than zero.");

        var settings = await _context.FinanceSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");

        var controlAccountId = module == SubledgerModules.AccountsReceivable
            ? settings.ControlAccountArId
            : settings.ControlAccountApId;
        if (!controlAccountId.HasValue)
            throw new InvalidOperationException($"{module} control account is not configured in Finance Settings.");

        var controlAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == controlAccountId.Value && a.TenantId == tenantId && !a.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException($"{module} control account was not found.");

        var contraAccountId = ResolveContraAccountId(
            dto.ContraAccountId,
            purpose,
            settings,
            originalAdjustmentId.HasValue);
        var contraAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == contraAccountId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Contra GL account was not found.");

        if (contraAccount.IsControlAccount)
            throw new InvalidOperationException("Contra GL account cannot be a control account. Use the relevant subledger flow for control accounts.");
        if (contraAccount.Id == controlAccount.Id)
            throw new InvalidOperationException("Contra GL account cannot be the same as the subledger control account.");

        var counterparty = await ResolveCounterpartyAsync(module, dto.CustomerId, dto.SupplierId, cancellationToken);
        var baseCurrencyCode = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync()) ?? "GHS";
        var currencyCode = NormalizeCurrency(dto.CurrencyCode) ?? baseCurrencyCode;
        var exchangeRate = dto.ExchangeRate <= 0 ? 1m : dto.ExchangeRate;
        var baseAmount = Math.Round(dto.Amount * exchangeRate, 2, MidpointRounding.AwayFromZero);
        var documentType = module == SubledgerModules.AccountsReceivable
            ? FinanceDocumentTypes.ARAdjustmentJournal
            : FinanceDocumentTypes.APAdjustmentJournal;
        var adjustmentNumber = await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            documentType,
            tenantId,
            dto.AdjustmentDate,
            nameof(SubledgerAdjustmentJournal),
            cancellationToken: cancellationToken);

        var now = DateTime.UtcNow;
        var adjustment = new SubledgerAdjustmentJournal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Module = module,
            AdjustmentNumber = adjustmentNumber,
            Purpose = purpose,
            CustomerId = module == SubledgerModules.AccountsReceivable ? counterparty.Customer?.Id : null,
            SupplierId = module == SubledgerModules.AccountsPayable ? counterparty.Supplier?.Id : null,
            AdjustmentDate = dto.AdjustmentDate.Date,
            DueDate = dto.DueDate?.Date,
            AdjustmentType = adjustmentType,
            Amount = dto.Amount,
            CurrencyCode = currencyCode,
            ExchangeRate = exchangeRate,
            BaseCurrencyAmount = baseAmount,
            ContraAccountId = contraAccount.Id,
            Status = SubledgerAdjustmentStatuses.Posted,
            Reference = string.IsNullOrWhiteSpace(dto.Reference) ? null : dto.Reference.Trim(),
            Reason = dto.Reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            OriginalAdjustmentId = originalAdjustmentId,
            CreatedAt = now,
            CreatedBy = UserName
        };

        _context.SubledgerAdjustmentJournals.Add(adjustment);
        await _context.SaveChangesAsync(cancellationToken);

        var postingResult = await PostJournalAsync(
            adjustment,
            controlAccount.Id,
            contraAccount.Id,
            counterparty.Name,
            baseCurrencyCode,
            cancellationToken);
        adjustment.JournalEntryId = postingResult.JournalEntryId;
        adjustment.UpdatedAt = DateTime.UtcNow;
        adjustment.UpdatedBy = UserName;

        if (module == SubledgerModules.AccountsReceivable && counterparty.Customer != null)
        {
            counterparty.Customer.OutstandingBalance = (counterparty.Customer.OutstandingBalance ?? 0m) + GetSignedBaseAmount(adjustment);
            counterparty.Customer.UpdatedAt = DateTime.UtcNow;
            counterparty.Customer.UpdatedBy = UserName;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var reloaded = await LoadAdjustmentAsync(adjustment.Id, asNoTracking: true, cancellationToken)
            ?? adjustment;
        return reloaded;
    }

    private async Task<FinancePostingResultDto> PostJournalAsync(
        SubledgerAdjustmentJournal adjustment,
        Guid controlAccountId,
        Guid contraAccountId,
        string counterpartyName,
        string functionalCurrencyCode,
        CancellationToken cancellationToken)
    {
        var isDebit = string.Equals(adjustment.AdjustmentType, SubledgerAdjustmentTypes.Debit, StringComparison.OrdinalIgnoreCase);
        var controlLineType = adjustment.Module == SubledgerModules.AccountsReceivable
            ? (isDebit ? "Debit" : "Credit")
            : (isDebit ? "Debit" : "Credit");
        var contraLineType = string.Equals(controlLineType, "Debit", StringComparison.OrdinalIgnoreCase) ? "Credit" : "Debit";

        var purposeLabel = string.Equals(adjustment.Purpose, SubledgerAdjustmentPurposes.OpeningBalance, StringComparison.OrdinalIgnoreCase)
            ? "opening balance"
            : "adjustment";
        var description = adjustment.Module == SubledgerModules.AccountsReceivable
            ? $"AR {adjustment.AdjustmentType} {purposeLabel} {adjustment.AdjustmentNumber} - {counterpartyName}"
            : $"AP {adjustment.AdjustmentType} {purposeLabel} {adjustment.AdjustmentNumber} - {counterpartyName}";

        return await _postingEngine.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = adjustment.Module,
            SourceDocumentType = "SubledgerAdjustmentJournal",
            SourceDocumentId = adjustment.Id,
            SourceDocumentTenantId = adjustment.TenantId,
            PostingAction = adjustment.OriginalAdjustmentId.HasValue
                ? "PostSubledgerAdjustmentJournalReversal"
                : "PostSubledgerAdjustmentJournal",
            SourceDocumentReference = adjustment.AdjustmentNumber,
            Description = description,
            PostingDate = adjustment.AdjustmentDate,
            JournalType = $"{adjustment.Module} Adjustment",
            FunctionalCurrencyCode = functionalCurrencyCode,
            IdempotencyKey = $"SubledgerAdjustmentJournal:{adjustment.TenantId:N}:{adjustment.Id:N}:Post",
            ReturnExistingOnDuplicate = true,
            Lines = new List<FinancePostingLineDto>
            {
                BuildPostingLine(controlAccountId, description, controlLineType, adjustment, 1),
                BuildPostingLine(contraAccountId, adjustment.Reason, contraLineType, adjustment, 2)
            }
        }, cancellationToken);
    }

    private static FinancePostingLineDto BuildPostingLine(
        Guid accountId,
        string description,
        string transactionType,
        SubledgerAdjustmentJournal adjustment,
        int lineNumber)
    {
        var isDebit = string.Equals(transactionType, "Debit", StringComparison.OrdinalIgnoreCase);
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            Description = description,
            DebitAmount = isDebit ? adjustment.BaseCurrencyAmount : 0m,
            CreditAmount = isDebit ? 0m : adjustment.BaseCurrencyAmount,
            TransactionCurrency = adjustment.CurrencyCode,
            TransactionDebitAmount = isDebit ? adjustment.Amount : 0m,
            TransactionCreditAmount = isDebit ? 0m : adjustment.Amount,
            ForeignCurrencyAmount = adjustment.Amount,
            ExchangeRate = adjustment.ExchangeRate,
            ExchangeRateDate = adjustment.AdjustmentDate,
            SourceReferenceNumber = adjustment.AdjustmentNumber,
            LineNumber = lineNumber
        };
    }

    private async Task<CounterpartyResult> ResolveCounterpartyAsync(
        string module,
        Guid? customerId,
        Guid? supplierId,
        CancellationToken cancellationToken)
    {
        if (module == SubledgerModules.AccountsReceivable)
        {
            if (!customerId.HasValue)
                throw new InvalidOperationException("Customer is required for AR adjustment journals.");

            var customer = await _context.Set<BusinessPartner>()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == TenantId &&
                    p.Id == customerId.Value &&
                    !p.IsDeleted &&
                    (p.PartnerType == "Customer" || p.PartnerType == "Both"),
                    cancellationToken)
                ?? throw new InvalidOperationException("Customer business partner was not found.");

            return new CounterpartyResult(customer.PartnerName, customer, null);
        }

        if (!supplierId.HasValue)
            throw new InvalidOperationException("Supplier is required for AP adjustment journals.");

        var supplier = await _context.Set<Supplier>()
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == supplierId.Value && !s.IsDeleted, cancellationToken)
            ?? await ResolveSupplierFromBusinessPartnerAsync(supplierId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Supplier or supplier business partner was not found.");

        return new CounterpartyResult(supplier.Name, null, supplier);
    }

    private async Task<Supplier?> ResolveSupplierFromBusinessPartnerAsync(
        Guid supplierBusinessPartnerId,
        CancellationToken cancellationToken)
    {
        var partner = await _context.Set<BusinessPartner>()
            .FirstOrDefaultAsync(p =>
                p.TenantId == TenantId &&
                !p.IsDeleted &&
                p.Id == supplierBusinessPartnerId,
                cancellationToken);

        if (partner == null)
            return null;
        if (string.Equals(partner.PartnerType, "Customer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Customer business partners cannot be used for AP adjustment journals.");
        if (partner.IsBlacklisted)
            throw new InvalidOperationException($"Business partner '{partner.PartnerName}' is blacklisted and cannot be used for AP adjustment journals.");

        var supplier = await _context.Set<Supplier>()
            .FirstOrDefaultAsync(s =>
                s.TenantId == TenantId &&
                !s.IsDeleted &&
                (s.SupplierCode == partner.PartnerCode || s.Name == partner.PartnerName),
                cancellationToken);

        if (supplier != null)
            return supplier;

        supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SupplierCode = string.IsNullOrWhiteSpace(partner.PartnerCode)
                ? $"BP-{partner.Id.ToString("N")[..8].ToUpperInvariant()}"
                : partner.PartnerCode,
            Name = partner.PartnerName,
            SupplierType = partner.PartnerType.Contains("Manufacturer", StringComparison.OrdinalIgnoreCase)
                ? "Manufacturer"
                : "Vendor",
            Address = partner.PhysicalAddress ?? partner.MailingAddress,
            City = partner.PhysicalCity ?? partner.MailingCity,
            State = partner.PhysicalState ?? partner.MailingState,
            Country = partner.PhysicalCountry ?? partner.MailingCountry,
            Phone = partner.PrimaryPhone ?? partner.SecondaryPhone,
            Email = partner.PrimaryEmail,
            Website = partner.Website,
            PrimaryContactName = partner.PrimaryContactName,
            PrimaryContactPhone = partner.PrimaryPhone ?? partner.SecondaryPhone,
            PrimaryContactEmail = partner.PrimaryEmail,
            TaxId = partner.TaxIdentificationNumber,
            PaymentTerms = partner.PaymentTerms,
            CreditLimit = partner.CreditLimit,
            IsActive = partner.IsActive,
            Status = partner.RegistrationStatus,
            IsBlacklisted = partner.IsBlacklisted,
            BlacklistReason = partner.BlacklistReason,
            PaymentTermId = partner.PaymentTermId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        _context.Set<Supplier>().Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);
        return supplier;
    }

    private Task<SubledgerAdjustmentJournal?> LoadAdjustmentAsync(
        Guid id,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var query = _context.SubledgerAdjustmentJournals
            .Include(a => a.Customer)
            .Include(a => a.Supplier)
            .Include(a => a.ContraAccount)
            .Include(a => a.JournalEntry)
            .Where(a => a.TenantId == TenantId && a.Id == id && !a.IsDeleted);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    private static SubledgerAdjustmentJournalDto MapToDto(SubledgerAdjustmentJournal adjustment)
    {
        return new SubledgerAdjustmentJournalDto
        {
            Id = adjustment.Id,
            Module = adjustment.Module,
            AdjustmentNumber = adjustment.AdjustmentNumber,
            Purpose = adjustment.Purpose,
            CustomerId = adjustment.CustomerId,
            CustomerName = adjustment.Customer?.PartnerName,
            SupplierId = adjustment.SupplierId,
            SupplierName = adjustment.Supplier?.Name,
            AdjustmentDate = adjustment.AdjustmentDate,
            DueDate = adjustment.DueDate,
            AdjustmentType = adjustment.AdjustmentType,
            Amount = adjustment.Amount,
            SignedSubledgerAmount = adjustment.SignedSubledgerAmount,
            CurrencyCode = adjustment.CurrencyCode,
            ExchangeRate = adjustment.ExchangeRate,
            BaseCurrencyAmount = adjustment.BaseCurrencyAmount,
            ContraAccountId = adjustment.ContraAccountId,
            ContraAccountNumber = adjustment.ContraAccount?.AccountNumber,
            ContraAccountName = adjustment.ContraAccount?.AccountName,
            JournalEntryId = adjustment.JournalEntryId,
            JournalNumber = adjustment.JournalEntry?.JournalEntryNumber,
            Status = adjustment.Status,
            Reference = adjustment.Reference,
            Reason = adjustment.Reason,
            Notes = adjustment.Notes,
            OriginalAdjustmentId = adjustment.OriginalAdjustmentId,
            ReversalAdjustmentId = adjustment.ReversalAdjustmentId,
            ReversalReason = adjustment.ReversalReason,
            ReversedAt = adjustment.ReversedAt,
            CreatedAt = adjustment.CreatedAt,
            CreatedBy = adjustment.CreatedBy
        };
    }

    private static string? NormalizeModule(string? module, bool allowNull)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            if (allowNull) return null;
            throw new InvalidOperationException("Subledger module is required.");
        }

        var normalized = module.Trim().ToUpperInvariant();
        if (normalized is "AR" or "ACCOUNTSRECEIVABLE" or "ACCOUNTS_RECEIVABLE")
            return SubledgerModules.AccountsReceivable;
        if (normalized is "AP" or "ACCOUNTSPAYABLE" or "ACCOUNTS_PAYABLE")
            return SubledgerModules.AccountsPayable;

        throw new InvalidOperationException("Subledger module must be AR or AP.");
    }

    private static string NormalizeAdjustmentType(string? adjustmentType)
    {
        if (string.IsNullOrWhiteSpace(adjustmentType))
            throw new InvalidOperationException("Adjustment type is required.");

        var normalized = adjustmentType.Trim();
        if (string.Equals(normalized, SubledgerAdjustmentTypes.Debit, StringComparison.OrdinalIgnoreCase))
            return SubledgerAdjustmentTypes.Debit;
        if (string.Equals(normalized, SubledgerAdjustmentTypes.Credit, StringComparison.OrdinalIgnoreCase))
            return SubledgerAdjustmentTypes.Credit;

        throw new InvalidOperationException("Adjustment type must be Debit or Credit.");
    }

    private static string NormalizePurpose(string? purpose, bool allowRetiredOpeningBalance)
    {
        if (string.IsNullOrWhiteSpace(purpose))
            return SubledgerAdjustmentPurposes.StandardAdjustment;

        var normalized = purpose.Trim();
        if (string.Equals(normalized, SubledgerAdjustmentPurposes.StandardAdjustment, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Standard", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Adjustment", StringComparison.OrdinalIgnoreCase))
        {
            return SubledgerAdjustmentPurposes.StandardAdjustment;
        }

        if (string.Equals(normalized, SubledgerAdjustmentPurposes.OpeningBalance, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Opening Balance", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "OpenBalance", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "OB", StringComparison.OrdinalIgnoreCase))
        {
            if (allowRetiredOpeningBalance)
                return SubledgerAdjustmentPurposes.OpeningBalance;

            throw new InvalidOperationException(
                "Subledger opening-balance adjustments are retired. Use the controlled Opening Balances workspace and its source-specific processes.");
        }

        throw new InvalidOperationException("Adjustment purpose must be StandardAdjustment.");
    }

    private static Guid ResolveContraAccountId(
        Guid requestedContraAccountId,
        string purpose,
        FinanceSettings settings,
        bool isReversal)
    {
        if (!string.Equals(purpose, SubledgerAdjustmentPurposes.OpeningBalance, StringComparison.OrdinalIgnoreCase))
        {
            if (requestedContraAccountId == Guid.Empty)
                throw new InvalidOperationException("Contra GL account is required.");

            return requestedContraAccountId;
        }

        if (isReversal && requestedContraAccountId != Guid.Empty)
            return requestedContraAccountId;

        if (!settings.MigrationClearingAccountId.HasValue)
            throw new InvalidOperationException("Migration clearing account is not configured in Finance Settings.");

        var clearingAccountId = settings.MigrationClearingAccountId.Value;
        if (requestedContraAccountId != Guid.Empty && requestedContraAccountId != clearingAccountId)
        {
            throw new InvalidOperationException(
                "Opening-balance subledger adjustment journals must use the configured Migration Clearing Account as the contra account.");
        }

        return clearingAccountId;
    }

    private static string? NormalizeCurrency(string? currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? null
            : currencyCode.Trim().ToUpperInvariant();
    }

    private static decimal GetSignedBaseAmount(SubledgerAdjustmentJournal adjustment)
    {
        var isDebit = string.Equals(adjustment.AdjustmentType, SubledgerAdjustmentTypes.Debit, StringComparison.OrdinalIgnoreCase);
        return string.Equals(adjustment.Module, SubledgerModules.AccountsReceivable, StringComparison.OrdinalIgnoreCase)
            ? (isDebit ? adjustment.BaseCurrencyAmount : -adjustment.BaseCurrencyAmount)
            : (isDebit ? -adjustment.BaseCurrencyAmount : adjustment.BaseCurrencyAmount);
    }

    private sealed record CounterpartyResult(string Name, BusinessPartner? Customer, Supplier? Supplier);
}
