using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Api.Services.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Finance.AR;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly ISubledgerSettlementReadModelService _settlementReadModelService;
    private readonly IFinanceAccessScopeService _financeAccessScopeService;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDocumentNumberingService documentNumberingService,
        ISubledgerSettlementReadModelService settlementReadModelService,
        IFinanceAccessScopeService financeAccessScopeService,
        ILogger<CustomerService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _documentNumberingService = documentNumberingService;
        _settlementReadModelService = settlementReadModelService;
        _financeAccessScopeService = financeAccessScopeService;
        _logger = logger;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => _currentUser.UserName ?? "system";

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var partner = await CustomerPartners()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return partner == null ? null : MapToDto(partner);
    }

    public async Task<CustomerDto?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = customerCode.Trim();

        var partner = await CustomerPartners()
            .FirstOrDefaultAsync(p =>
                p.PartnerCode == normalizedCode ||
                p.CustomerAccountNumber == normalizedCode,
                cancellationToken);

        return partner == null ? null : MapToDto(partner);
    }

    public async Task<PagedResult<CustomerDto>> GetAllAsync(CustomerQueryDto query, CancellationToken cancellationToken = default)
    {
        var customers = CustomerPartners();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.Trim();
            customers = customers.Where(p =>
                p.PartnerName.Contains(search) ||
                p.PartnerCode.Contains(search) ||
                (p.CustomerAccountNumber != null && p.CustomerAccountNumber.Contains(search)) ||
                (p.PrimaryEmail != null && p.PrimaryEmail.Contains(search)) ||
                (p.PrimaryPhone != null && p.PrimaryPhone.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerType))
        {
            customers = customers.Where(p => p.CustomerType == query.CustomerType);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            customers = customers.Where(p => p.PhysicalCity == query.City || p.MailingCity == query.City);
        }

        if (!string.IsNullOrWhiteSpace(query.Country))
        {
            customers = customers.Where(p => p.PhysicalCountry == query.Country || p.MailingCountry == query.Country);
        }

        if (query.IsActive.HasValue)
        {
            customers = customers.Where(p => p.IsActive == query.IsActive.Value);
        }

        customers = query.SortBy?.ToLowerInvariant() switch
        {
            "code" => query.SortDescending ? customers.OrderByDescending(p => p.PartnerCode) : customers.OrderBy(p => p.PartnerCode),
            "balance" => query.SortDescending ? customers.OrderByDescending(p => p.OutstandingBalance ?? 0) : customers.OrderBy(p => p.OutstandingBalance ?? 0),
            "created" => query.SortDescending ? customers.OrderByDescending(p => p.CreatedAt) : customers.OrderBy(p => p.CreatedAt),
            _ => query.SortDescending ? customers.OrderByDescending(p => p.PartnerName) : customers.OrderBy(p => p.PartnerName)
        };

        var totalCount = await customers.CountAsync(cancellationToken);
        var items = await customers
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<CustomerDto> CreateAsync(CustomerCreateDto dto, CancellationToken cancellationToken = default)
    {
        var code = string.IsNullOrWhiteSpace(dto.CustomerCode)
            ? await GenerateCustomerCodeAsync(cancellationToken)
            : dto.CustomerCode.Trim();

        var exists = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(p => p.TenantId == TenantId &&
                               !p.IsDeleted &&
                               (p.PartnerCode == code || p.CustomerAccountNumber == code))
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Customer code '{code}' already exists.");
        }

        var paymentTerm = await ResolveCustomerPaymentTermAsync(dto.PaymentTermId, cancellationToken);
        var paymentTermsDays = paymentTerm?.DueDays ?? dto.PaymentTermsDays;
        var now = DateTime.UtcNow;
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            PartnerCode = code,
            CustomerAccountNumber = code,
            PartnerName = dto.CustomerName,
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            IsActive = true,
            CustomerType = dto.CustomerType,
            PrimaryContactName = dto.ContactPerson,
            PrimaryEmail = dto.Email,
            PrimaryPhone = dto.Phone,
            PhysicalAddress = dto.Address,
            PhysicalCity = dto.City,
            PhysicalState = dto.State,
            PhysicalPostalCode = dto.PostalCode,
            PhysicalCountry = dto.Country,
            TaxIdentificationNumber = dto.TaxId,
            CreditLimit = dto.CreditLimit,
            OutstandingBalance = 0m,
            PaymentTermId = paymentTerm?.Id,
            PaymentTerms = BuildPaymentTermsLabel(paymentTermsDays),
            PriceList = dto.PriceGroup,
            Currency = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "GHS" : dto.CurrencyCode,
            Notes = dto.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = UserName,
            UpdatedBy = UserName
        };

        await _unitOfWork.Repository<BusinessPartner>().AddAsync(partner);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(partner);
    }

    public async Task<CustomerDto> UpdateAsync(CustomerUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var partner = await CustomerPartners()
            .FirstOrDefaultAsync(p => p.Id == dto.Id, cancellationToken);

        if (partner == null)
        {
            throw new InvalidOperationException("Customer not found.");
        }

        partner.PartnerName = dto.CustomerName;
        partner.CustomerType = dto.CustomerType;
        partner.PrimaryContactName = dto.ContactPerson;
        partner.PrimaryEmail = dto.Email;
        partner.PrimaryPhone = dto.Phone;
        partner.PhysicalAddress = dto.Address;
        partner.PhysicalCity = dto.City;
        partner.PhysicalState = dto.State;
        partner.PhysicalPostalCode = dto.PostalCode;
        partner.PhysicalCountry = dto.Country;
        partner.TaxIdentificationNumber = dto.TaxId;
        partner.CreditLimit = dto.CreditLimit;
        var paymentTerm = await ResolveCustomerPaymentTermAsync(dto.PaymentTermId, cancellationToken);
        var paymentTermsDays = paymentTerm?.DueDays ?? dto.PaymentTermsDays;
        partner.PaymentTermId = paymentTerm?.Id;
        partner.PaymentTerms = BuildPaymentTermsLabel(paymentTermsDays);
        partner.PriceList = dto.PriceGroup;
        partner.Currency = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? partner.Currency : dto.CurrencyCode;
        partner.IsActive = dto.IsActive;
        partner.Notes = dto.Notes;
        partner.UpdatedAt = DateTime.UtcNow;
        partner.UpdatedBy = UserName;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(partner);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var partner = await CustomerPartners()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (partner == null)
        {
            return;
        }

        if ((partner.OutstandingBalance ?? 0m) != 0m)
        {
            throw new InvalidOperationException("Cannot delete a customer with an outstanding balance.");
        }

        partner.IsActive = false;
        partner.RegistrationStatus = "Inactive";
        partner.UpdatedAt = DateTime.UtcNow;
        partner.UpdatedBy = UserName;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerBalanceDto> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var partner = await CustomerPartners()
            .FirstOrDefaultAsync(p => p.Id == customerId, cancellationToken);

        if (partner == null)
        {
            throw new InvalidOperationException("Customer not found.");
        }

        var asOfDate = DateTime.UtcNow;

        // PaidAmount/CreditedAmount are operational snapshots. Rebuild the AR read model from
        // posted invoices, receipts, credit notes, withholding, and posting events before using
        // it for customer credit exposure or aging; no posted GL/source document is changed.
        await _settlementReadModelService.RebuildAsync(new SubledgerSettlementRebuildRequestDto
        {
            SourceModule = SubledgerSettlementModules.AccountsReceivable,
            AsOfDate = asOfDate,
            RecordAudit = false
        }, cancellationToken);

        var settlementBalances = (await _settlementReadModelService.GetBalancesAsync(
                SubledgerSettlementModules.AccountsReceivable,
                asOfDate,
                customerId,
                cancellationToken))
            .Where(b => b.OutstandingAmount != 0m)
            .ToList();

        var outstandingBalance = settlementBalances.Sum(b => b.OutstandingAmount);
        var creditLimit = partner.CreditLimit ?? 0m;

        var balance = new CustomerBalanceDto
        {
            CustomerId = partner.Id,
            CustomerName = partner.PartnerName,
            TotalOutstanding = outstandingBalance,
            CreditLimit = creditLimit,
            AvailableCredit = Math.Max(0m, creditLimit - outstandingBalance)
        };

        foreach (var settlementBalance in settlementBalances)
        {
            AddSettlementBalanceToBalanceBuckets(balance, settlementBalance, asOfDate);
        }

        return balance;
    }

    public async Task<CreditCheckResultDto> CheckCreditLimitAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default)
    {
        var balance = await GetBalanceAsync(customerId, cancellationToken);
        var availableCredit = balance.AvailableCredit;

        return new CreditCheckResultDto
        {
            CreditLimit = balance.CreditLimit,
            CurrentOutstanding = balance.TotalOutstanding,
            RequestedAmount = amount,
            AvailableCredit = availableCredit,
            IsApproved = amount <= availableCredit,
            Message = amount > availableCredit
                ? $"Requested amount exceeds available credit by {amount - availableCredit:C}."
                : null
        };
    }

    public async Task<List<InvoiceDto>> GetCustomerInvoicesAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        await EnsureCustomerExistsAsync(customerId, cancellationToken);

        var invoices = await GetCustomerInvoicesQuery(customerId)
            .Include(i => i.LineItems)
            .ThenInclude(li => li.GLAccount)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.InvoiceNumber)
            .ToListAsync(cancellationToken);

        return invoices.Select(MapInvoiceToDto).ToList();
    }

    public async Task<List<CustomerPaymentDto>> GetCustomerPaymentsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var partner = await EnsureCustomerExistsAsync(customerId, cancellationToken);

        var paymentsQuery = GetCustomerPaymentsQuery(customerId);
        var permittedBankAccountIds = await _financeAccessScopeService
            .GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read, cancellationToken);
        if (permittedBankAccountIds != null)
        {
            // This customer-centric route must not become a side door around the receipt list's
            // bank scope. Liquidity-held receipts remain tenant-wide only until that dimension is assignable.
            paymentsQuery = paymentsQuery.Where(payment =>
                payment.BankAccountId.HasValue &&
                permittedBankAccountIds.Contains(payment.BankAccountId.Value));
        }

        var payments = await paymentsQuery
            .Include(p => p.Allocations)
            .ThenInclude(a => a.Invoice)
            .Include(p => p.BankAccount)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentNumber)
            .ToListAsync(cancellationToken);

        return payments.Select(p => MapPaymentToDto(p, partner)).ToList();
    }

    private IQueryable<BusinessPartner> CustomerPartners()
    {
        return _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(p => p.TenantId == TenantId &&
                               !p.IsDeleted &&
                               (p.PartnerType == "Customer" || p.PartnerType == "Both"));
    }

    private async Task<BusinessPartner> EnsureCustomerExistsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var partner = await CustomerPartners()
            .FirstOrDefaultAsync(p => p.Id == customerId, cancellationToken);

        return partner ?? throw new InvalidOperationException("Customer not found.");
    }

    // Customer account review must read the current Finance AR source tables; legacy CRM/customer
    // navigation collections are intentionally not used for invoice/payment history.
    private IQueryable<Invoice> GetCustomerInvoicesQuery(Guid customerId)
    {
        return _unitOfWork.Repository<Invoice>()
            .GetQueryable(i =>
                i.TenantId == TenantId &&
                !i.IsDeleted &&
                i.BusinessPartnerId == customerId);
    }

    private IQueryable<CustomerPayment> GetCustomerPaymentsQuery(Guid customerId)
    {
        return _unitOfWork.Repository<CustomerPayment>()
            .GetQueryable(p =>
                p.TenantId == TenantId &&
                !p.IsDeleted &&
                p.CustomerId == customerId);
    }

    private async Task<string> GenerateCustomerCodeAsync(CancellationToken cancellationToken)
    {
        // Reserve codes through central document numbering instead of counting visible rows:
        // concurrent creates would generate the same count-based code, and soft-deleted
        // customers shrink the count so it can reissue a code that already exists.
        return await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            FinanceDocumentTypes.CustomerAccount,
            TenantId,
            DateTime.UtcNow,
            nameof(BusinessPartner),
            cancellationToken: cancellationToken);
    }

    private static CustomerDto MapToDto(BusinessPartner partner)
    {
        var paymentTermsDays = TryParsePaymentTermsDays(partner.PaymentTerms) ?? 30;

        return new CustomerDto
        {
            Id = partner.Id,
            CustomerCode = partner.CustomerAccountNumber ?? partner.PartnerCode,
            CustomerName = partner.PartnerName,
            CustomerType = string.IsNullOrWhiteSpace(partner.CustomerType) ? "Customer" : partner.CustomerType,
            ContactPerson = partner.PrimaryContactName,
            Email = partner.PrimaryEmail,
            Phone = partner.PrimaryPhone,
            Address = partner.PhysicalAddress ?? partner.MailingAddress,
            City = partner.PhysicalCity ?? partner.MailingCity,
            State = partner.PhysicalState ?? partner.MailingState,
            PostalCode = partner.PhysicalPostalCode ?? partner.MailingPostalCode,
            Country = partner.PhysicalCountry ?? partner.MailingCountry,
            TaxId = partner.TaxIdentificationNumber,
            CreditLimit = partner.CreditLimit ?? 0m,
            OutstandingBalance = partner.OutstandingBalance ?? 0m,
            PaymentTermsDays = paymentTermsDays,
            PaymentTermId = partner.PaymentTermId,
            PriceGroup = partner.PriceList,
            CurrencyCode = string.IsNullOrWhiteSpace(partner.Currency) ? "GHS" : partner.Currency,
            IsActive = partner.IsActive &&
                       !partner.IsBlacklisted &&
                       !string.Equals(partner.RegistrationStatus, "Blacklisted", StringComparison.OrdinalIgnoreCase),
            Notes = partner.Notes,
            CreatedAt = partner.CreatedAt
        };
    }

    private static void AddSettlementBalanceToBalanceBuckets(
        CustomerBalanceDto balance,
        SubledgerSettlementBalance settlementBalance,
        DateTime asOfDate)
    {
        var outstanding = settlementBalance.OutstandingAmount;
        if (outstanding == 0)
        {
            return;
        }

        var dueDate = (settlementBalance.DueDate ?? settlementBalance.TransactionDate).Date;
        var daysOverdue = (asOfDate.Date - dueDate).Days;

        if (daysOverdue <= 0)
        {
            balance.Current += outstanding;
        }
        else if (daysOverdue <= 30)
        {
            balance.Days1To30 += outstanding;
        }
        else if (daysOverdue <= 60)
        {
            balance.Days31To60 += outstanding;
        }
        else if (daysOverdue <= 90)
        {
            balance.Days61To90 += outstanding;
        }
        else
        {
            balance.Days90Plus += outstanding;
        }
    }

    private static InvoiceDto MapInvoiceToDto(Invoice invoice)
    {
        return new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CustomerId = invoice.CustomerId,
            CustomerName = invoice.CustomerName,
            CustomerAddress = invoice.CustomerAddress,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            SubTotal = invoice.SubTotal,
            TaxAmount = invoice.TaxAmount,
            DiscountAmount = invoice.DiscountAmount,
            TotalAmount = invoice.TotalAmount,
            PaidAmount = invoice.PaidAmount,
            BalanceAmount = invoice.BalanceAmount,
            Status = invoice.Status.ToString(),
            Notes = invoice.Notes,
            Reference = invoice.Reference,
            IsOpeningBalance = invoice.IsOpeningBalance,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            PaymentTermsDays = invoice.PaymentTermsDays,
            PaymentTermId = invoice.PaymentTermId,
            EarlyPaymentDiscountPercentage = invoice.EarlyPaymentDiscountPercentage,
            EarlyPaymentDiscountDueDate = invoice.EarlyPaymentDiscountDueDate,
            EarlyPaymentDiscountAmount = invoice.EarlyPaymentDiscountAmount,
            TaxGroupId = invoice.TaxGroupId,
            JournalEntryId = invoice.JournalEntryId,
            LineItems = invoice.LineItems.Select(li => new InvoiceLineItemDto
            {
                Id = li.Id,
                InvoiceId = li.InvoiceId,
                LineItemType = li.LineItemType.ToString(),
                ProductId = li.ProductId,
                GLAccountId = li.GLAccountId,
                GLAccountCode = li.GLAccount?.AccountCode,
                GLAccountName = li.GLAccount?.AccountName,
                Description = li.Description,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice,
                LineTotal = li.LineTotal,
                TaxRate = li.TaxRate,
                TaxAmount = li.TaxAmount,
                TaxCode = li.TaxCode,
                TaxGroupId = li.TaxGroupId,
                TaxTreatment = li.TaxTreatment,
                Unit = li.Unit,
                DiscountPercentage = li.DiscountPercentage,
                DiscountAmount = li.DiscountAmount
            }).ToList(),
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }

    private static CustomerPaymentDto MapPaymentToDto(CustomerPayment payment, BusinessPartner customer)
    {
        return new CustomerPaymentDto
        {
            Id = payment.Id,
            PaymentNumber = payment.PaymentNumber,
            CustomerId = payment.CustomerId,
            CustomerName = customer.PartnerName,
            PaymentDate = payment.PaymentDate,
            TotalAmount = payment.TotalAmount,
            AllocatedAmount = payment.AllocatedAmount,
            UnallocatedAmount = payment.UnallocatedAmount,
            PaymentMethod = payment.PaymentMethod,
            CurrencyCode = payment.CurrencyCode,
            ExchangeRate = payment.ExchangeRate,
            BankAccountId = payment.BankAccountId,
            BankAccountName = payment.BankAccount?.AccountName,
            CheckNumber = payment.CheckNumber,
            TransactionReference = payment.TransactionReference,
            WithholdingTaxId = payment.WithholdingTaxId,
            WithholdingTaxAccountId = payment.WithholdingTaxAccountId,
            WithholdingTaxAmount = payment.WithholdingTaxAmount,
            VatWithholdingTaxId = payment.VatWithholdingTaxId,
            VatWithholdingAccountId = payment.VatWithholdingAccountId,
            VatWithholdingAmount = payment.VatWithholdingAmount,
            WithholdingCertificateNumber = payment.WithholdingCertificateNumber,
            WithholdingCertificateDate = payment.WithholdingCertificateDate,
            Notes = payment.Notes,
            Status = payment.Status,
            ClearedDate = payment.ClearedDate,
            IsCreditNote = payment.IsCreditNote,
            JournalEntryId = payment.JournalEntryId,
            ReversalJournalEntryId = payment.ReversalJournalEntryId,
            ReversalPostingEventId = payment.ReversalPostingEventId,
            ReversalCashTransactionId = payment.ReversalCashTransactionId,
            ReversalLiquidityAccountEntryId = payment.ReversalLiquidityAccountEntryId,
            ReversalDate = payment.ReversalDate,
            ReversedAt = payment.ReversedAt,
            ReversedById = payment.ReversedById,
            ReversalReason = payment.ReversalReason,
            Allocations = payment.Allocations.Select(a => new PaymentAllocationDto
            {
                Id = a.Id,
                CustomerPaymentId = a.CustomerPaymentId,
                PaymentNumber = payment.PaymentNumber,
                InvoiceId = a.InvoiceId,
                InvoiceNumber = a.Invoice?.InvoiceNumber ?? string.Empty,
                AllocatedAmount = a.AllocatedAmount,
                DiscountAmount = a.DiscountAmount,
                AllocationDate = a.AllocationDate,
                Notes = a.Notes,
                IsReversal = a.IsReversal,
                OriginalAllocationId = a.OriginalAllocationId
            }).ToList(),
            CreatedAt = payment.CreatedAt
        };
    }

    private static Guid? NormalizeGuid(Guid? value)
    {
        return value.HasValue && value.Value != Guid.Empty ? value : null;
    }

    private async Task<PaymentTerm?> ResolveCustomerPaymentTermAsync(Guid? paymentTermId, CancellationToken cancellationToken)
    {
        var normalizedPaymentTermId = NormalizeGuid(paymentTermId);
        if (!normalizedPaymentTermId.HasValue)
        {
            return await _unitOfWork.Repository<PaymentTerm>()
                .GetQueryable(pt =>
                    pt.TenantId == TenantId &&
                    pt.IsActive &&
                    pt.IsDefault &&
                    !pt.IsDeleted &&
                    (pt.ApplicableTo == "All" || pt.ApplicableTo == "Customer" || pt.ApplicableTo == "Client"))
                .OrderBy(pt => pt.ApplicableTo == "Customer" ? 0 : 1)
                .ThenBy(pt => pt.DisplayOrder)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var paymentTerm = await _unitOfWork.Repository<PaymentTerm>()
            .GetQueryable(pt =>
                pt.TenantId == TenantId &&
                pt.Id == normalizedPaymentTermId.Value &&
                pt.IsActive &&
                !pt.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (paymentTerm == null)
        {
            throw new InvalidOperationException($"Active payment term with Id '{normalizedPaymentTermId.Value}' was not found.");
        }

        if (!IsCustomerPaymentTerm(paymentTerm.ApplicableTo))
        {
            throw new InvalidOperationException($"Payment term '{paymentTerm.Code}' is not applicable to customers.");
        }

        return paymentTerm;
    }

    private static bool IsCustomerPaymentTerm(string applicableTo)
    {
        return string.Equals(applicableTo, "All", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(applicableTo, "Customer", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(applicableTo, "Client", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildPaymentTermsLabel(int paymentTermsDays)
    {
        return paymentTermsDays switch
        {
            < 0 => null,
            0 => "COD",
            _ => $"Net {paymentTermsDays}"
        };
    }

    private static int? TryParsePaymentTermsDays(string? paymentTerms)
    {
        if (string.IsNullOrWhiteSpace(paymentTerms))
        {
            return null;
        }

        if (paymentTerms.Equals("COD", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var digits = new string(paymentTerms.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var days) ? days : null;
    }
}
