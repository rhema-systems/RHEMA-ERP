using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Estate;

public interface IGroundRentAdministrationService
{
    Task<EstateGroundRentOptionsDto> GetOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<EstateGroundRentAccountDto>> GetAccountsAsync(CancellationToken cancellationToken);
    Task<EstateGroundRentAccountDto> UpsertAccountAsync(UpsertEstateGroundRentAccountDto request, CancellationToken cancellationToken);
    Task<EstateGroundRentActionResultDto> GenerateInvoiceAsync(Guid accountId, GenerateEstateGroundRentInvoiceDto request, CancellationToken cancellationToken);
    Task<EstateGroundRentActionResultDto> AssessPenaltyAsync(Guid chargeId, CancellationToken cancellationToken);
    Task<EstateGroundRentActionResultDto> PostInvoiceAsync(Guid chargeId, string target, CancellationToken cancellationToken);
    Task<EstateGroundRentActionResultDto> RecordReceiptAsync(Guid chargeId, RecordEstateGroundRentReceiptDto request, CancellationToken cancellationToken);
    Task<EstateGroundRentActionResultDto> ApplyReviewAsync(Guid accountId, ApplyEstateGroundRentReviewDto request, CancellationToken cancellationToken);
}

public sealed class GroundRentAdministrationService : IGroundRentAdministrationService
{
    private static readonly HashSet<string> Frequencies =
        new(StringComparer.OrdinalIgnoreCase) { "Annual", "SemiAnnual", "Quarterly", "Monthly" };

    private static readonly HashSet<string> CalculationMethods =
        new(StringComparer.OrdinalIgnoreCase) { "ApprovedAssessment", "FixedAnnualAmount", "RatePerAcre" };

    private static readonly HashSet<string> EscalationMethods =
        new(StringComparer.OrdinalIgnoreCase) { "None", "Percentage", "FixedAmount" };

    private static readonly HashSet<string> PenaltyMethods =
        new(StringComparer.OrdinalIgnoreCase) { "None", "PercentageOfOutstanding", "FixedAmount" };

    private static readonly HashSet<string> AccountStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Active", "Held", "Closed" };

    private readonly ApplicationDbContext _db;
    private readonly IInvoiceService _invoiceService;
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUserService;

    public GroundRentAdministrationService(
        ApplicationDbContext db,
        IInvoiceService invoiceService,
        IPaymentService paymentService,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _invoiceService = invoiceService;
        _paymentService = paymentService;
        _currentUserService = currentUserService;
    }

    public async Task<EstateGroundRentOptionsDto> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var configuredAssetIds = await _db.EstateGroundRentAccounts
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => item.EstateManagedAssetId)
            .ToListAsync(cancellationToken);

        var assets = await _db.EstateManagedAssets
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && item.AssetType == EstateManagedAssetType.Land
                && item.CustomerBusinessPartnerId.HasValue)
            .OrderBy(item => item.AssetCode)
            .ToListAsync(cancellationToken);

        var customerIds = assets
            .Where(item => item.CustomerBusinessPartnerId.HasValue)
            .Select(item => item.CustomerBusinessPartnerId!.Value)
            .Distinct()
            .ToList();

        var customers = await _db.BusinessPartners
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && customerIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var assetOptions = assets.Select(asset =>
        {
            var customerName = asset.CustomerBusinessPartnerId is { } customerId
                && customers.TryGetValue(customerId, out var customer)
                    ? customer.PartnerName
                    : asset.LesseeName;

            return new EstateGroundRentAssetOptionDto(
                asset.Id,
                asset.AssetCode,
                asset.Name,
                asset.Location,
                GetAreaAcres(asset),
                asset.CustomerBusinessPartnerId,
                customerName,
                asset.GroundRentPayable,
                asset.GroundRentRatePerAcre,
                NormalizeCurrency(asset.Currency),
                configuredAssetIds.Contains(asset.Id));
        }).ToList();

        var incomeAccounts = await _db.Accounts
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && item.Status == AccountStatus.Active
                && item.AccountType == AccountType.Revenue
                && item.AllowDirectPosting
                && !item.IsControlAccount)
            .OrderBy(item => item.AccountNumber)
            .Select(item => new EstateGroundRentIncomeAccountOptionDto(
                item.Id,
                item.AccountNumber,
                item.AccountName,
                item.CurrencyCode))
            .ToListAsync(cancellationToken);

        return new EstateGroundRentOptionsDto(assetOptions, incomeAccounts);
    }

    public async Task<IReadOnlyList<EstateGroundRentAccountDto>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await LoadAccountsAsync(null, cancellationToken);
        return await MapAccountsAsync(accounts, cancellationToken);
    }

    public async Task<EstateGroundRentAccountDto> UpsertAccountAsync(
        UpsertEstateGroundRentAccountDto request,
        CancellationToken cancellationToken)
    {
        ValidateAccountRequest(request);
        var tenantId = TenantId;

        var asset = await _db.EstateManagedAssets
            .FirstOrDefaultAsync(item =>
                item.Id == request.EstateManagedAssetId
                && item.TenantId == tenantId
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("The selected Estate property or unit was not found.");

        if (asset.AssetType != EstateManagedAssetType.Land)
        {
            throw new InvalidOperationException(
                "Ground rent applies to land assets only. Use the apartment/unit rent amount for non-land leases.");
        }

        var customer = await _db.BusinessPartners
            .FirstOrDefaultAsync(item =>
                item.Id == request.CustomerBusinessPartnerId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsActive
                && (item.PartnerType == "Customer" || item.PartnerType == "Both"),
                cancellationToken)
            ?? throw new InvalidOperationException("The selected customer is not an active Finance AR customer.");

        var incomeAccount = await _db.Accounts
            .FirstOrDefaultAsync(item =>
                item.Id == request.GroundRentIncomeAccountId
                && item.TenantId == tenantId
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("The selected ground-rent income account was not found.");

        if (asset.CustomerBusinessPartnerId.HasValue
            && asset.CustomerBusinessPartnerId.Value != customer.Id)
        {
            throw new InvalidOperationException(
                "The selected Finance AR customer does not match the customer assigned in the Lease Register.");
        }

        if (incomeAccount.Status != AccountStatus.Active
            || incomeAccount.AccountType != AccountType.Revenue
            || !incomeAccount.AllowDirectPosting
            || incomeAccount.IsControlAccount)
        {
            throw new InvalidOperationException(
                "Ground rent must use an active, directly postable revenue account that is not a control account.");
        }

        var (annualAmount, ratePerAcre) = ResolveAnnualAmount(asset, request);
        var now = DateTime.UtcNow;
        var account = await _db.EstateGroundRentAccounts
            .FirstOrDefaultAsync(item =>
                item.EstateManagedAssetId == asset.Id
                && item.TenantId == tenantId
                && !item.IsDeleted,
                cancellationToken);

        if (account is null)
        {
            account = new EstateGroundRentAccount
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                EstateManagedAssetId = asset.Id,
                CreatedAt = now,
                CreatedBy = UserName
            };
            _db.EstateGroundRentAccounts.Add(account);
        }

        account.CustomerBusinessPartnerId = customer.Id;
        account.PaymentFrequency = CanonicalFrequency(request.PaymentFrequency);
        account.CalculationMethod = Canonical(request.CalculationMethod, CalculationMethods);
        account.AnnualAmount = annualAmount;
        account.RatePerAcre = ratePerAcre;
        account.CurrencyCode = NormalizeCurrency(request.CurrencyCode);
        account.NextDueDate = request.NextDueDate.Date;
        account.PaymentTermsDays = request.PaymentTermsDays;
        account.ReviewFrequencyMonths = request.ReviewFrequencyMonths;
        account.NextReviewDate = request.NextReviewDate?.Date;
        account.EscalationMethod = Canonical(request.EscalationMethod, EscalationMethods);
        account.EscalationValue = request.EscalationValue;
        account.GracePeriodDays = request.GracePeriodDays;
        account.PenaltyMethod = Canonical(request.PenaltyMethod, PenaltyMethods);
        account.PenaltyValue = request.PenaltyValue;
        account.PenaltyCapAmount = request.PenaltyCapAmount;
        account.GroundRentIncomeAccountId = incomeAccount.Id;
        account.AutoPostInvoices = request.AutoPostInvoices;
        account.Status = Canonical(request.Status, AccountStatuses);
        account.Notes = CleanText(request.Notes, 1000);
        account.UpdatedAt = account.CreatedAt == now ? null : now;
        account.UpdatedBy = account.CreatedAt == now ? null : UserName;

        asset.CustomerBusinessPartnerId = customer.Id;
        asset.LesseeName = customer.PartnerName;
        asset.LesseeAddress = customer.PhysicalAddress ?? customer.MailingAddress;
        asset.GroundRentPayable = annualAmount;
        if (ratePerAcre.HasValue)
        {
            asset.GroundRentRatePerAcre = ratePerAcre;
            asset.GroundRentComputed = GetAreaAcres(asset) * ratePerAcre;
        }
        asset.Currency = account.CurrencyCode;
        asset.UpdatedAt = now;
        asset.UpdatedBy = UserName;

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAccountAsync(account.Id, cancellationToken);
    }

    public async Task<EstateGroundRentActionResultDto> GenerateInvoiceAsync(
        Guid accountId,
        GenerateEstateGroundRentInvoiceDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var account = await _db.EstateGroundRentAccounts
            .Include(item => item.EstateManagedAsset)
            .FirstOrDefaultAsync(item =>
                item.Id == accountId
                && item.TenantId == tenantId
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Ground-rent account was not found.");

        if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only active ground-rent accounts can generate invoices.");
        }

        var billingStart = ResolveBillingStart(account.EstateManagedAsset);
        if (!billingStart.HasValue)
        {
            throw new InvalidOperationException(
                "Ground-rent invoices cannot be generated until the lease agreement start date or right-of-entry / move-in date is recorded in Lease Management.");
        }

        if (string.IsNullOrWhiteSpace(account.EstateManagedAsset.PropertyFileReference))
        {
            throw new InvalidOperationException(
                "Ground-rent invoices cannot be generated until the signed lease or tenancy agreement reference is recorded in Lease Management.");
        }

        var dueDate = account.NextDueDate.Date;
        if (dueDate < billingStart.Value.Date)
        {
            throw new InvalidOperationException(
                $"The next ground-rent due date cannot be before the billing start date {billingStart.Value:yyyy-MM-dd}.");
        }

        if (!request.AllowFutureDueDate && dueDate > DateTime.UtcNow.Date)
        {
            throw new InvalidOperationException(
                $"The next ground-rent invoice is not due until {dueDate:yyyy-MM-dd}.");
        }

        var existingCharge = await _db.EstateGroundRentCharges
            .FirstOrDefaultAsync(item =>
                item.GroundRentAccountId == account.Id
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.DueDate == dueDate,
                cancellationToken);

        if (existingCharge is not null)
        {
            return new EstateGroundRentActionResultDto(
                $"Ground-rent invoice {existingCharge.FinanceInvoiceNumber ?? "record"} already exists for {dueDate:yyyy-MM-dd}.",
                await GetAccountAsync(account.Id, cancellationToken));
        }

        var invoiceDate = (request.InvoiceDate ?? DateTime.UtcNow).Date;
        var periodMonths = MonthsPerPeriod(account.PaymentFrequency);
        var latestCharge = await _db.EstateGroundRentCharges
            .AsNoTracking()
            .Where(item =>
                item.GroundRentAccountId == account.Id
                && item.TenantId == tenantId
                && !item.IsDeleted)
            .OrderByDescending(item => item.PeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);
        var periodStart = latestCharge?.PeriodEnd.Date.AddDays(1) ?? billingStart.Value.Date;
        var periodEnd = periodStart.AddMonths(periodMonths).AddDays(-1);
        var amount = AmountPerPeriod(account.AnnualAmount, account.PaymentFrequency);
        var reference = TrimTo($"GR-{account.EstateManagedAsset.AssetCode}-{dueDate:yyyyMMdd}", 100);
        var description = TrimTo(
            $"Ground rent {periodStart:dd MMM yyyy} - {periodEnd:dd MMM yyyy}: {account.EstateManagedAsset.Name}",
            200);

        var invoice = await _invoiceService.CreateAsync(
            new InvoiceCreateDto
            {
                CustomerId = account.CustomerBusinessPartnerId,
                InvoiceDate = invoiceDate,
                DueDate = dueDate,
                Reference = reference,
                CurrencyCode = account.CurrencyCode,
                ExchangeRate = 1m,
                Notes = TrimTo(
                    $"Ground rent for {account.EstateManagedAsset.AssetCode}. Source: Estate / Property Management -> Finance AR.",
                    500),
                LineItems =
                [
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "GLAccount",
                        GLAccountId = account.GroundRentIncomeAccountId,
                        Description = description,
                        Quantity = 1m,
                        UnitPrice = amount,
                        TaxTreatment = TaxTreatment.Exempt,
                        Unit = "Period"
                    }
                ]
            },
            cancellationToken);

        if (account.AutoPostInvoices)
        {
            invoice = await _invoiceService.SendInvoiceAsync(invoice.Id, cancellationToken);
        }

        var charge = new EstateGroundRentCharge
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            GroundRentAccountId = account.Id,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            DueDate = dueDate,
            BaseAmount = amount,
            FinanceInvoiceId = invoice.Id,
            FinanceInvoiceNumber = invoice.InvoiceNumber,
            Status = invoice.JournalEntryId.HasValue ? "Posted" : "Invoiced",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        _db.EstateGroundRentCharges.Add(charge);
        account.NextDueDate = dueDate.AddMonths(periodMonths);
        account.UpdatedAt = DateTime.UtcNow;
        account.UpdatedBy = UserName;
        await _db.SaveChangesAsync(cancellationToken);

        return new EstateGroundRentActionResultDto(
            $"Finance AR invoice {invoice.InvoiceNumber} was created for ground rent due {dueDate:yyyy-MM-dd}.",
            await GetAccountAsync(account.Id, cancellationToken));
    }

    public async Task<EstateGroundRentActionResultDto> AssessPenaltyAsync(
        Guid chargeId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var charge = await _db.EstateGroundRentCharges
            .Include(item => item.GroundRentAccount)
                .ThenInclude(item => item.EstateManagedAsset)
            .FirstOrDefaultAsync(item =>
                item.Id == chargeId
                && item.TenantId == tenantId
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Ground-rent charge was not found.");

        var account = charge.GroundRentAccount;
        if (string.Equals(account.PenaltyMethod, "None", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("No arrears penalty rule is configured for this account.");
        }

        if (charge.PenaltyInvoiceId.HasValue)
        {
            return new EstateGroundRentActionResultDto(
                $"Penalty invoice {charge.PenaltyInvoiceNumber} already exists.",
                await GetAccountAsync(account.Id, cancellationToken));
        }

        if (!charge.FinanceInvoiceId.HasValue)
        {
            throw new InvalidOperationException("The base ground-rent invoice has not been generated.");
        }

        var baseInvoice = await _invoiceService.GetByIdAsync(charge.FinanceInvoiceId.Value, cancellationToken)
            ?? throw new InvalidOperationException("The linked Finance AR invoice was not found.");

        if (baseInvoice.BalanceAmount <= 0m)
        {
            throw new InvalidOperationException("The ground-rent invoice has no outstanding balance.");
        }

        var penaltyDate = charge.DueDate.Date.AddDays(account.GracePeriodDays);
        if (penaltyDate >= DateTime.UtcNow.Date)
        {
            throw new InvalidOperationException(
                $"The arrears grace period ends on {penaltyDate:yyyy-MM-dd}.");
        }

        var penaltyAmount = account.PenaltyMethod.ToLowerInvariant() switch
        {
            "percentageofoutstanding" => RoundMoney(baseInvoice.BalanceAmount * account.PenaltyValue / 100m),
            "fixedamount" => RoundMoney(account.PenaltyValue),
            _ => throw new InvalidOperationException("The configured penalty method is not supported.")
        };

        if (account.PenaltyCapAmount is { } cap)
        {
            penaltyAmount = Math.Min(penaltyAmount, cap);
        }

        if (penaltyAmount <= 0m)
        {
            throw new InvalidOperationException("The configured penalty produces a zero amount.");
        }

        var invoice = await _invoiceService.CreateAsync(
            new InvoiceCreateDto
            {
                CustomerId = account.CustomerBusinessPartnerId,
                InvoiceDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date,
                Reference = TrimTo($"GRP-{account.EstateManagedAsset.AssetCode}-{charge.DueDate:yyyyMMdd}", 100),
                CurrencyCode = account.CurrencyCode,
                ExchangeRate = 1m,
                Notes = TrimTo(
                    $"Ground-rent arrears penalty linked to {baseInvoice.InvoiceNumber}. Source: Estate / Property Management -> Finance AR.",
                    500),
                LineItems =
                [
                    new InvoiceLineItemCreateDto
                    {
                        LineItemType = "GLAccount",
                        GLAccountId = account.GroundRentIncomeAccountId,
                        Description = TrimTo(
                            $"Ground-rent arrears penalty for {account.EstateManagedAsset.Name}",
                            200),
                        Quantity = 1m,
                        UnitPrice = penaltyAmount,
                        TaxTreatment = TaxTreatment.Exempt,
                        Unit = "Penalty"
                    }
                ]
            },
            cancellationToken);

        if (account.AutoPostInvoices)
        {
            invoice = await _invoiceService.SendInvoiceAsync(invoice.Id, cancellationToken);
        }

        charge.PenaltyAmount = penaltyAmount;
        charge.PenaltyInvoiceId = invoice.Id;
        charge.PenaltyInvoiceNumber = invoice.InvoiceNumber;
        charge.UpdatedAt = DateTime.UtcNow;
        charge.UpdatedBy = UserName;
        await _db.SaveChangesAsync(cancellationToken);

        return new EstateGroundRentActionResultDto(
            $"Finance AR penalty invoice {invoice.InvoiceNumber} was created.",
            await GetAccountAsync(account.Id, cancellationToken));
    }

    public async Task<EstateGroundRentActionResultDto> PostInvoiceAsync(
        Guid chargeId,
        string target,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var charge = await _db.EstateGroundRentCharges
            .FirstOrDefaultAsync(item =>
                item.Id == chargeId
                && item.TenantId == tenantId
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Ground-rent charge was not found.");

        var invoiceId = IsPenaltyTarget(target) ? charge.PenaltyInvoiceId : charge.FinanceInvoiceId;
        if (!invoiceId.HasValue)
        {
            throw new InvalidOperationException("The selected Finance AR invoice has not been generated.");
        }

        var invoice = await _invoiceService.GetByIdAsync(invoiceId.Value, cancellationToken)
            ?? throw new InvalidOperationException("The selected Finance AR invoice was not found.");

        if (!invoice.JournalEntryId.HasValue)
        {
            invoice = string.Equals(invoice.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                ? await _invoiceService.SendInvoiceAsync(invoice.Id, cancellationToken)
                : await _invoiceService.PostAsync(invoice.Id, cancellationToken);
        }

        charge.Status = "Posted";
        charge.UpdatedAt = DateTime.UtcNow;
        charge.UpdatedBy = UserName;
        await _db.SaveChangesAsync(cancellationToken);

        return new EstateGroundRentActionResultDto(
            $"Finance AR invoice {invoice.InvoiceNumber} is posted to journal {invoice.JournalEntryId}.",
            await GetAccountAsync(charge.GroundRentAccountId, cancellationToken));
    }

    public async Task<EstateGroundRentActionResultDto> RecordReceiptAsync(
        Guid chargeId,
        RecordEstateGroundRentReceiptDto request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0m)
        {
            throw new InvalidOperationException("Receipt amount must be greater than zero.");
        }

        var tenantId = TenantId;
        var charge = await _db.EstateGroundRentCharges
            .Include(item => item.GroundRentAccount)
            .FirstOrDefaultAsync(item =>
                item.Id == chargeId
                && item.TenantId == tenantId
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Ground-rent charge was not found.");

        var invoiceId = IsPenaltyTarget(request.Target) ? charge.PenaltyInvoiceId : charge.FinanceInvoiceId;
        if (!invoiceId.HasValue)
        {
            throw new InvalidOperationException("The selected Finance AR invoice has not been generated.");
        }

        var invoice = await _invoiceService.GetByIdAsync(invoiceId.Value, cancellationToken)
            ?? throw new InvalidOperationException("The selected Finance AR invoice was not found.");

        if (request.Amount > invoice.BalanceAmount)
        {
            throw new InvalidOperationException(
                $"Receipt amount cannot exceed the invoice balance of {invoice.BalanceAmount:0.00} {invoice.CurrencyCode}.");
        }

        var payment = await _paymentService.CreateAsync(
            new PaymentCreateDto
            {
                CustomerId = charge.GroundRentAccount.CustomerBusinessPartnerId,
                PaymentDate = request.PaymentDate == default ? DateTime.UtcNow.Date : request.PaymentDate.Date,
                TotalAmount = request.Amount,
                PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "Cash" : request.PaymentMethod.Trim(),
                PaymentMethodId = request.PaymentMethodId,
                BankAccountId = request.BankAccountId,
                CheckNumber = CleanText(request.CheckNumber, 100),
                TransactionReference = CleanText(request.TransactionReference, 100),
                CurrencyCode = NormalizeCurrency(request.CurrencyCode),
                ExchangeRate = request.ExchangeRate <= 0m ? 1m : request.ExchangeRate,
                Notes = TrimTo(
                    $"{request.Notes?.Trim()} Source: Estate / Property Management ground rent -> Finance AR.",
                    500),
                Allocations =
                [
                    new InvoiceAllocationDto
                    {
                        InvoiceId = invoice.Id,
                        AllocatedAmount = request.Amount,
                        DiscountAmount = 0m,
                        Notes = $"Ground-rent receipt allocation for {invoice.InvoiceNumber}."
                    }
                ]
            },
            cancellationToken);

        charge.UpdatedAt = DateTime.UtcNow;
        charge.UpdatedBy = UserName;
        await _db.SaveChangesAsync(cancellationToken);

        return new EstateGroundRentActionResultDto(
            $"Finance AR receipt {payment.PaymentNumber} was posted and allocated to {invoice.InvoiceNumber}.",
            await GetAccountAsync(charge.GroundRentAccountId, cancellationToken));
    }

    public async Task<EstateGroundRentActionResultDto> ApplyReviewAsync(
        Guid accountId,
        ApplyEstateGroundRentReviewDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var account = await _db.EstateGroundRentAccounts
            .Include(item => item.EstateManagedAsset)
            .FirstOrDefaultAsync(item =>
                item.Id == accountId
                && item.TenantId == tenantId
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Ground-rent account was not found.");

        var method = Canonical(
            string.IsNullOrWhiteSpace(request.EscalationMethod)
                ? account.EscalationMethod
                : request.EscalationMethod,
            EscalationMethods);
        var value = request.EscalationValue ?? account.EscalationValue;

        if (string.Equals(method, "None", StringComparison.OrdinalIgnoreCase) || value <= 0m)
        {
            throw new InvalidOperationException("Select a percentage or fixed-amount escalation greater than zero.");
        }

        var previousAnnualAmount = account.AnnualAmount;
        var previousRate = account.RatePerAcre;
        var newAnnualAmount = method.ToLowerInvariant() switch
        {
            "percentage" => RoundMoney(previousAnnualAmount * (1m + value / 100m)),
            "fixedamount" => RoundMoney(previousAnnualAmount + value),
            _ => throw new InvalidOperationException("The selected escalation method is not supported.")
        };

        decimal? newRate = previousRate;
        var areaAcres = GetAreaAcres(account.EstateManagedAsset);
        if (previousRate.HasValue && areaAcres is > 0m)
        {
            newRate = RoundMoney(newAnnualAmount / areaAcres.Value);
        }

        var review = new EstateGroundRentReview
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            GroundRentAccountId = account.Id,
            EffectiveDate = request.EffectiveDate == default ? DateTime.UtcNow.Date : request.EffectiveDate.Date,
            PreviousAnnualAmount = previousAnnualAmount,
            NewAnnualAmount = newAnnualAmount,
            PreviousRatePerAcre = previousRate,
            NewRatePerAcre = newRate,
            EscalationMethod = method,
            EscalationValue = value,
            Notes = CleanText(request.Notes, 500),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        _db.EstateGroundRentReviews.Add(review);
        account.AnnualAmount = newAnnualAmount;
        account.RatePerAcre = newRate;
        account.NextReviewDate = review.EffectiveDate.AddMonths(account.ReviewFrequencyMonths);
        account.UpdatedAt = DateTime.UtcNow;
        account.UpdatedBy = UserName;

        account.EstateManagedAsset.GroundRentPayable = newAnnualAmount;
        if (newRate.HasValue)
        {
            account.EstateManagedAsset.GroundRentRatePerAcre = newRate;
            account.EstateManagedAsset.GroundRentComputed = areaAcres * newRate;
        }
        account.EstateManagedAsset.UpdatedAt = DateTime.UtcNow;
        account.EstateManagedAsset.UpdatedBy = UserName;

        await _db.SaveChangesAsync(cancellationToken);

        return new EstateGroundRentActionResultDto(
            $"Ground rent was reviewed from {previousAnnualAmount:0.00} to {newAnnualAmount:0.00} {account.CurrencyCode}.",
            await GetAccountAsync(account.Id, cancellationToken));
    }

    private async Task<EstateGroundRentAccountDto> GetAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = (await LoadAccountsAsync(accountId, cancellationToken)).SingleOrDefault()
            ?? throw new KeyNotFoundException("Ground-rent account was not found.");
        return (await MapAccountsAsync([account], cancellationToken)).Single();
    }

    private async Task<List<EstateGroundRentAccount>> LoadAccountsAsync(
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var query = _db.EstateGroundRentAccounts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.EstateManagedAsset)
            .Include(item => item.GroundRentIncomeAccount)
            .Include(item => item.Charges.Where(child =>
                child.TenantId == tenantId && !child.IsDeleted))
            .Include(item => item.Reviews.Where(child =>
                child.TenantId == tenantId && !child.IsDeleted))
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .AsQueryable();

        if (accountId.HasValue)
        {
            query = query.Where(item => item.Id == accountId.Value);
        }

        return await query
            .OrderBy(item => item.NextDueDate)
            .ThenBy(item => item.EstateManagedAsset.AssetCode)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<EstateGroundRentAccountDto>> MapAccountsAsync(
        IReadOnlyList<EstateGroundRentAccount> accounts,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return [];
        }

        var tenantId = TenantId;
        var customerIds = accounts.Select(item => item.CustomerBusinessPartnerId).Distinct().ToList();
        var customers = await _db.BusinessPartners
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && customerIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var invoiceIds = accounts
            .SelectMany(item => item.Charges)
            .SelectMany(item => new[] { item.FinanceInvoiceId, item.PenaltyInvoiceId })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && invoiceIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return accounts.Select(account =>
        {
            var chargeDtos = account.Charges
                .OrderByDescending(item => item.DueDate)
                .Select(charge => MapCharge(charge, invoices))
                .ToList();
            var outstanding = chargeDtos.Sum(item => item.OutstandingAmount);
            var arrears = chargeDtos
                .Where(item => item.DueDate.Date < DateTime.UtcNow.Date && item.OutstandingAmount > 0m)
                .Sum(item => item.OutstandingAmount);
            var customerName = customers.TryGetValue(account.CustomerBusinessPartnerId, out var customer)
                ? customer.PartnerName
                : account.EstateManagedAsset.LesseeName ?? "Unknown customer";
            var billingStart = ResolveBillingStart(account.EstateManagedAsset);
            var invoiceHoldReason = ResolveInvoiceHoldReason(account, billingStart);

            return new EstateGroundRentAccountDto(
                account.Id,
                account.EstateManagedAssetId,
                account.EstateManagedAsset.AssetCode,
                account.EstateManagedAsset.Name,
                account.EstateManagedAsset.Location,
                account.CustomerBusinessPartnerId,
                customerName,
                account.PaymentFrequency,
                account.CalculationMethod,
                account.AnnualAmount,
                AmountPerPeriod(account.AnnualAmount, account.PaymentFrequency),
                account.RatePerAcre,
                account.CurrencyCode,
                billingStart?.Date,
                ResolveBillingStartSource(account.EstateManagedAsset),
                invoiceHoldReason is null,
                invoiceHoldReason,
                account.NextDueDate,
                account.PaymentTermsDays,
                account.ReviewFrequencyMonths,
                account.NextReviewDate,
                account.EscalationMethod,
                account.EscalationValue,
                account.GracePeriodDays,
                account.PenaltyMethod,
                account.PenaltyValue,
                account.PenaltyCapAmount,
                account.GroundRentIncomeAccountId,
                account.GroundRentIncomeAccount is null
                    ? "Not configured"
                    : $"{account.GroundRentIncomeAccount.AccountNumber} · {account.GroundRentIncomeAccount.AccountName}",
                account.AutoPostInvoices,
                account.Status,
                account.Notes,
                outstanding,
                arrears,
                chargeDtos.Count(item => item.Status == "Overdue"),
                chargeDtos,
                account.Reviews
                    .OrderByDescending(item => item.EffectiveDate)
                    .Select(item => new EstateGroundRentReviewDto(
                        item.Id,
                        item.EffectiveDate,
                        item.PreviousAnnualAmount,
                        item.NewAnnualAmount,
                        item.PreviousRatePerAcre,
                        item.NewRatePerAcre,
                        item.EscalationMethod,
                        item.EscalationValue,
                        item.Notes,
                        item.CreatedAt))
                    .ToList());
        }).ToList();
    }

    private static DateTime? ResolveBillingStart(EstateManagedAsset asset)
        => asset.DateOfTenancy?.Date ?? asset.RightOfEntryDate?.Date;

    private static string ResolveBillingStartSource(EstateManagedAsset asset)
    {
        if (asset.DateOfTenancy.HasValue)
        {
            return "Agreement start date";
        }

        if (asset.RightOfEntryDate.HasValue)
        {
            return "Right-of-entry / move-in date";
        }

        return "Not recorded";
    }

    private static string? ResolveInvoiceHoldReason(EstateGroundRentAccount account, DateTime? billingStart)
    {
        if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return "Only active ground-rent accounts can generate invoices.";
        }

        if (!billingStart.HasValue)
        {
            return "Record the lease agreement start date or right-of-entry / move-in date before billing.";
        }

        if (string.IsNullOrWhiteSpace(account.EstateManagedAsset.PropertyFileReference))
        {
            return "Record the signed lease or tenancy agreement reference before billing.";
        }

        if (account.NextDueDate.Date < billingStart.Value.Date)
        {
            return $"Next due date is before the billing start date {billingStart.Value:yyyy-MM-dd}.";
        }

        if (account.NextDueDate.Date > DateTime.UtcNow.Date)
        {
            return $"Next invoice is not due until {account.NextDueDate:yyyy-MM-dd}.";
        }

        return null;
    }

    private static EstateGroundRentChargeDto MapCharge(
        EstateGroundRentCharge charge,
        IReadOnlyDictionary<Guid, Invoice> invoices)
    {
        invoices.TryGetValue(charge.FinanceInvoiceId ?? Guid.Empty, out var baseInvoice);
        invoices.TryGetValue(charge.PenaltyInvoiceId ?? Guid.Empty, out var penaltyInvoice);

        var basePaid = baseInvoice?.PaidAmount ?? 0m;
        var penaltyPaid = penaltyInvoice?.PaidAmount ?? 0m;
        var baseOutstanding = baseInvoice?.BalanceAmount ?? charge.BaseAmount;
        var penaltyOutstanding = penaltyInvoice?.BalanceAmount ?? 0m;
        var outstanding = Math.Max(0m, baseOutstanding) + Math.Max(0m, penaltyOutstanding);
        var paid = basePaid + penaltyPaid;

        var status = outstanding <= 0m && charge.FinanceInvoiceId.HasValue
            ? "Paid"
            : paid > 0m
                ? "PartiallyPaid"
                : charge.DueDate.Date < DateTime.UtcNow.Date
                    ? "Overdue"
                    : baseInvoice?.JournalEntryId.HasValue == true
                        ? "Posted"
                        : "Invoiced";

        return new EstateGroundRentChargeDto(
            charge.Id,
            charge.PeriodStart,
            charge.PeriodEnd,
            charge.DueDate,
            charge.BaseAmount,
            charge.PenaltyAmount,
            paid,
            outstanding,
            status,
            charge.FinanceInvoiceId,
            charge.FinanceInvoiceNumber,
            baseInvoice?.JournalEntryId,
            charge.PenaltyInvoiceId,
            charge.PenaltyInvoiceNumber,
            penaltyInvoice?.JournalEntryId);
    }

    private static void ValidateAccountRequest(UpsertEstateGroundRentAccountDto request)
    {
        if (request.EstateManagedAssetId == Guid.Empty)
            throw new InvalidOperationException("Select an Estate property or unit.");
        if (request.CustomerBusinessPartnerId == Guid.Empty)
            throw new InvalidOperationException("Select the customer responsible for ground rent.");
        if (request.GroundRentIncomeAccountId == Guid.Empty)
            throw new InvalidOperationException("Select a ground-rent income account.");
        if (!Frequencies.Contains(request.PaymentFrequency))
            throw new InvalidOperationException("Payment frequency must be Annual, SemiAnnual, Quarterly, or Monthly.");
        if (!CalculationMethods.Contains(request.CalculationMethod))
            throw new InvalidOperationException("The ground-rent calculation method is invalid.");
        if (!EscalationMethods.Contains(request.EscalationMethod))
            throw new InvalidOperationException("The rent-review escalation method is invalid.");
        if (!PenaltyMethods.Contains(request.PenaltyMethod))
            throw new InvalidOperationException("The arrears penalty method is invalid.");
        if (!AccountStatuses.Contains(request.Status))
            throw new InvalidOperationException("Ground-rent account status must be Active, Held, or Closed.");
        if (request.NextDueDate == default)
            throw new InvalidOperationException("Enter the next ground-rent due date.");
        if (request.PaymentTermsDays is < 0 or > 365)
            throw new InvalidOperationException("Payment terms must be between 0 and 365 days.");
        if (request.ReviewFrequencyMonths is < 1 or > 120)
            throw new InvalidOperationException("Rent-review frequency must be between 1 and 120 months.");
        if (request.GracePeriodDays is < 0 or > 365)
            throw new InvalidOperationException("Penalty grace period must be between 0 and 365 days.");
        if (request.EscalationValue < 0m || request.PenaltyValue < 0m || request.PenaltyCapAmount < 0m)
            throw new InvalidOperationException("Escalation and penalty values cannot be negative.");
    }

    private static (decimal AnnualAmount, decimal? RatePerAcre) ResolveAnnualAmount(
        EstateManagedAsset asset,
        UpsertEstateGroundRentAccountDto request)
    {
        if (request.CalculationMethod.Equals("ApprovedAssessment", StringComparison.OrdinalIgnoreCase))
        {
            if (asset.GroundRentPayable is not > 0m)
                throw new InvalidOperationException("The selected property has no approved annual ground-rent assessment.");
            return (RoundMoney(asset.GroundRentPayable.Value), asset.GroundRentRatePerAcre);
        }

        if (request.CalculationMethod.Equals("RatePerAcre", StringComparison.OrdinalIgnoreCase))
        {
            if (request.RatePerAcre is not > 0m)
                throw new InvalidOperationException("Enter a ground-rent rate per acre greater than zero.");
            var areaAcres = GetAreaAcres(asset);
            if (areaAcres is not > 0m)
                throw new InvalidOperationException("The selected property has no usable acreage for rate calculation.");
            return (decimal.Ceiling(areaAcres.Value * request.RatePerAcre.Value), request.RatePerAcre);
        }

        if (request.AnnualAmount is not > 0m)
            throw new InvalidOperationException("Enter an annual ground-rent amount greater than zero.");
        return (RoundMoney(request.AnnualAmount.Value), request.RatePerAcre);
    }

    private static decimal? GetAreaAcres(EstateManagedAsset asset)
    {
        if (asset.AreaValue.HasValue
            && (string.Equals(asset.AreaUnit, "Acre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(asset.AreaUnit, "Acres", StringComparison.OrdinalIgnoreCase)))
        {
            return asset.AreaValue;
        }

        return asset.AreaSquareMeters.HasValue
            ? asset.AreaSquareMeters.Value / 4046.8564224m
            : null;
    }

    private static decimal AmountPerPeriod(decimal annualAmount, string frequency)
        => RoundMoney(annualAmount / PeriodsPerYear(frequency));

    private static int PeriodsPerYear(string frequency)
        => frequency.ToLowerInvariant() switch
        {
            "monthly" => 12,
            "quarterly" => 4,
            "semiannual" => 2,
            _ => 1
        };

    private static int MonthsPerPeriod(string frequency) => 12 / PeriodsPerYear(frequency);

    private static string CanonicalFrequency(string value) => Canonical(value, Frequencies);

    private static string Canonical(string value, HashSet<string> choices)
        => choices.First(item => item.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static bool IsPenaltyTarget(string value)
        => string.Equals(value, "Penalty", StringComparison.OrdinalIgnoreCase);

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string NormalizeCurrency(string? value)
        => string.IsNullOrWhiteSpace(value) ? "GHS" : value.Trim().ToUpperInvariant()[..Math.Min(3, value.Trim().Length)];

    private static string? CleanText(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value) ? null : TrimTo(value.Trim(), maxLength);

    private static string TrimTo(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private Guid TenantId
        => _currentUserService.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new UnauthorizedAccessException("Tenant context is required.");

    private string UserName => _currentUserService.UserName ?? "System";
}
