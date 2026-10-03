using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Services.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Finance.AR;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ISubledgerSettlementReadModelService _settlementReadModelService;
    private readonly IFinanceAccessScopeService _financeAccessScopeService;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ISubledgerSettlementReadModelService settlementReadModelService,
        IFinanceAccessScopeService financeAccessScopeService,
        ILogger<CustomerService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _settlementReadModelService = settlementReadModelService;
        _financeAccessScopeService = financeAccessScopeService;
        _logger = logger;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var partner = await CustomerPartners()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return partner == null ? null : (await MapCustomersAsync(
            new[] { partner }, includeBalances: true, cancellationToken)).Single();
    }

    public async Task<CustomerDto?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = customerCode.Trim();

        var partner = await CustomerPartners()
            .FirstOrDefaultAsync(p =>
                p.PartnerCode == normalizedCode ||
                p.CustomerAccountNumber == normalizedCode,
                cancellationToken);

        return partner == null ? null : (await MapCustomersAsync(
            new[] { partner }, includeBalances: true, cancellationToken)).Single();
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

        if (!string.IsNullOrWhiteSpace(query.TransactionReadiness))
        {
            var allPartners = await customers.ToListAsync(cancellationToken);
            var allDtos = await MapCustomersAsync(allPartners, query.IncludeBalances, cancellationToken);
            var ready = query.TransactionReadiness.Equals("Ready", StringComparison.OrdinalIgnoreCase);
            if (!ready && !query.TransactionReadiness.Equals("NotReady", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("TransactionReadiness must be Ready or NotReady.");
            }

            var filtered = allDtos.Where(item => item.IsTransactionReady == ready).ToList();
            return new PagedResult<CustomerDto>
            {
                Items = filtered.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToList(),
                TotalCount = filtered.Count,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

        var totalCount = await customers.CountAsync(cancellationToken);
        var items = await customers
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>
        {
            Items = await MapCustomersAsync(items, query.IncludeBalances, cancellationToken),
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
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
        var readiness = (await ResolveCustomerProfilesAsync(new[] { partner }, asOfDate, cancellationToken))[partner.Id];
        var creditLimit = readiness.ArProfile?.CreditLimit ?? 0m;

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
        var partner = await EnsureCustomerExistsAsync(customerId, cancellationToken);
        var readiness = (await ResolveCustomerProfilesAsync(new[] { partner }, DateTime.UtcNow, cancellationToken))[partner.Id];

        return new CreditCheckResultDto
        {
            CreditLimit = balance.CreditLimit,
            CurrentOutstanding = balance.TotalOutstanding,
            RequestedAmount = amount,
            AvailableCredit = availableCredit,
            IsApproved = readiness.IsReady && amount <= availableCredit,
            Message = !readiness.IsReady ? $"{readiness.Code}: {readiness.Message}" : amount > availableCredit
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
                               p.Roles.Any(role =>
                                   role.TenantId == TenantId &&
                                   !role.IsDeleted &&
                                   role.RoleType == BusinessPartnerRoleType.Customer));
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
                p.BusinessPartnerId == customerId);
    }

    private async Task<Dictionary<Guid, BusinessPartnerFinanceProfileReadiness>> ResolveCustomerProfilesAsync(
        IReadOnlyCollection<BusinessPartner> partners, DateTime asOfDate, CancellationToken cancellationToken)
    {
        var partnerIds = partners.Select(partner => partner.Id).ToArray();
        var roles = await _unitOfWork.Repository<BusinessPartnerRole>().GetQueryable(role =>
                role.TenantId == TenantId && partnerIds.Contains(role.BusinessPartnerId) && !role.IsDeleted &&
                role.RoleType == BusinessPartnerRoleType.Customer)
            .AsNoTracking().ToListAsync(cancellationToken);
        var roleIds = roles.Select(role => role.Id).ToArray();
        var profiles = await _unitOfWork.Repository<BusinessPartnerArProfileVersion>().GetQueryable(profile =>
                profile.TenantId == TenantId && roleIds.Contains(profile.BusinessPartnerRoleId) && !profile.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        return partners.ToDictionary(partner => partner.Id, partner =>
        {
            var customerRoles = roles.Where(role => role.BusinessPartnerId == partner.Id).ToArray();
            return BusinessPartnerFinanceProfilePolicy.ResolveAr(partner,
                customerRoles.Length == 1 ? customerRoles[0] : null, profiles, asOfDate);
        });
    }

    private async Task<List<CustomerDto>> MapCustomersAsync(
        IReadOnlyCollection<BusinessPartner> partners,
        bool includeBalances = false,
        CancellationToken cancellationToken = default)
    {
        if (partners.Count == 0) return new List<CustomerDto>();
        var readiness = await ResolveCustomerProfilesAsync(partners, DateTime.UtcNow, cancellationToken);
        var termIds = readiness.Values.Where(value => value.ArProfile?.PaymentTermId.HasValue == true)
            .Select(value => value.ArProfile!.PaymentTermId!.Value).ToArray();
        var terms = await _unitOfWork.Repository<PaymentTerm>().GetQueryable(term =>
                term.TenantId == TenantId && !term.IsDeleted && term.IsActive &&
                (term.IsDefault || termIds.Contains(term.Id)) &&
                (term.ApplicableTo == "All" || term.ApplicableTo == "Customer" || term.ApplicableTo == "Client"))
            .AsNoTracking().ToListAsync(cancellationToken);
        var defaultTerm = terms.Where(term => term.IsDefault)
            .OrderBy(term => term.ApplicableTo == "Customer" ? 0 : 1).ThenBy(term => term.DisplayOrder).FirstOrDefault();
        var asOfDate = DateTime.UtcNow;
        if (includeBalances)
        {
            // Keep the customer register aligned with the same canonical projection used by
            // aging and unapplied-receipt reporting. This changes read-side evidence only.
            await _settlementReadModelService.RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = SubledgerSettlementModules.AccountsReceivable,
                AsOfDate = asOfDate,
                RecordAudit = false
            }, cancellationToken);
        }

        var settlementBalances = includeBalances
            ? (await _settlementReadModelService.GetBalancesAsync(
                    SubledgerSettlementModules.AccountsReceivable,
                    asOfDate,
                    cancellationToken: cancellationToken))
                .GroupBy(item => item.CounterpartyId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.OutstandingAmount))
            : new Dictionary<Guid, decimal>();
        var customerCredits = includeBalances
            ? (await _settlementReadModelService.GetUnappliedBalancesAsync(
                    SubledgerSettlementModules.AccountsReceivable,
                    asOfDate,
                    cancellationToken: cancellationToken))
                .GroupBy(item => item.CounterpartyId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.UnappliedAmount))
            : new Dictionary<Guid, decimal>();
        return partners.Select(partner =>
        {
            var state = readiness[partner.Id];
            var term = state.IsReady
                ? state.ArProfile!.PaymentTermId.HasValue
                    ? terms.SingleOrDefault(value => value.Id == state.ArProfile.PaymentTermId.Value)
                    : defaultTerm
                : null;
            return MapToDto(
                partner,
                state,
                term,
                settlementBalances.TryGetValue(partner.Id, out var authoritativeBalance)
                    ? authoritativeBalance
                    : includeBalances ? 0m : null,
                customerCredits.GetValueOrDefault(partner.Id));
        }).ToList();
    }

    private static CustomerDto MapToDto(
        BusinessPartner partner,
        BusinessPartnerFinanceProfileReadiness readiness,
        PaymentTerm? paymentTerm,
        decimal? outstandingBalance = null,
        decimal customerCreditBalance = 0m)
    {

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
            CreditLimit = readiness.ArProfile?.CreditLimit ?? 0m,
            OutstandingBalance = outstandingBalance ?? partner.OutstandingBalance ?? 0m,
            CustomerCreditBalance = customerCreditBalance,
            PaymentTermsDays = paymentTerm?.DueDays ?? 30,
            PaymentTermId = paymentTerm?.Id,
            PriceGroup = partner.PriceList,
            CurrencyCode = string.IsNullOrWhiteSpace(partner.Currency) ? "GHS" : partner.Currency,
            IsActive = partner.IsActive,
            IsBlacklisted = partner.IsBlacklisted,
            IsTransactionReady = readiness.IsReady,
            ReadinessCode = readiness.Code,
            ReadinessMessage = readiness.Message,
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
            BusinessPartnerId = invoice.BusinessPartnerId,
            BusinessPartnerRoleId = invoice.BusinessPartnerRoleId,
            BusinessPartnerArProfileVersionId = invoice.BusinessPartnerArProfileVersionId,
            BusinessPartnerCode = invoice.BusinessPartnerCode,
            BusinessPartnerLegalName = invoice.BusinessPartnerLegalName,
            BusinessPartnerTin = invoice.BusinessPartnerTin,
            CustomerName = invoice.CustomerName,
            CustomerAddress = invoice.CustomerAddress,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            SubTotal = invoice.SubTotal,
            TaxAmount = invoice.TaxAmount,
            DiscountAmount = invoice.DiscountAmount,
            TotalAmount = invoice.TotalAmount,
            RoundingAdjustmentAmount = invoice.RoundingAdjustmentAmount,
            FinanceRoundingEvidenceId = invoice.FinanceRoundingEvidenceId,
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
            BusinessPartnerId = payment.BusinessPartnerId,
            BusinessPartnerRoleId = payment.BusinessPartnerRoleId,
            BusinessPartnerArProfileVersionId = payment.BusinessPartnerArProfileVersionId,
            BusinessPartnerCode = payment.BusinessPartnerCode,
            BusinessPartnerLegalName = payment.BusinessPartnerLegalName,
            BusinessPartnerTaxIdentificationNumber = payment.BusinessPartnerTaxIdentificationNumber,
            CustomerName = customer.PartnerName,
            PaymentDate = payment.PaymentDate,
            TotalAmount = payment.TotalAmount,
            AllocatedAmount = payment.AllocatedAmount,
            UnallocatedAmount = payment.UnallocatedAmount,
            RoundingAdjustmentAmount = payment.RoundingAdjustmentAmount,
            FinanceRoundingEvidenceId = payment.FinanceRoundingEvidenceId,
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
                PaymentCurrencyAmount = a.PaymentCurrencyAmount,
                InvoiceCurrencyCode = a.InvoiceCurrencyCode,
                PaymentCurrencyCode = a.PaymentCurrencyCode,
                IsCrossCurrency = a.IsCrossCurrency,
                InvoiceSettlementExchangeRateId = a.InvoiceSettlementExchangeRateId,
                InvoiceSettlementExchangeRate = a.InvoiceSettlementExchangeRate,
                PaymentExchangeRateId = a.PaymentExchangeRateId,
                PaymentExchangeRate = a.PaymentExchangeRate,
                PaymentFunctionalAmount = a.PaymentFunctionalAmount,
                SettlementFunctionalAmount = a.SettlementFunctionalAmount,
                DiscountAmount = a.DiscountAmount,
                DiscountFunctionalAmount = a.DiscountFunctionalAmount,
                WithholdingTaxAmount = a.WithholdingTaxAmount,
                WithholdingTaxFunctionalAmount = a.WithholdingTaxFunctionalAmount,
                VatWithholdingAmount = a.VatWithholdingAmount,
                VatWithholdingFunctionalAmount = a.VatWithholdingFunctionalAmount,
                AllocationDate = a.AllocationDate,
                Notes = a.Notes,
                IsReversal = a.IsReversal,
                OriginalAllocationId = a.OriginalAllocationId
            }).ToList(),
            CreatedAt = payment.CreatedAt
        };
    }

}
