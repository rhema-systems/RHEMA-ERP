using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Migration;

public sealed class OpeningBalanceService : IOpeningBalanceService
{
    private const string EntityType = "OpeningBalanceBatch";
    private const string SourceModule = "MIGRATION";
    private const string PostingAction = "PostOpeningBalance";
    private const string StatusDraft = "Draft";
    private const string StatusValidated = "Validated";
    private const string StatusPendingApproval = "PendingApproval";
    private const string StatusApproved = "Approved";
    private const string StatusRejected = "Rejected";
    private const string StatusPosted = "Posted";
    private const string StatusFailed = "Failed";
    private const string StatusPostingFailed = "PostingFailed";
    private const string FixedAssetOpeningCost = "FixedAssetOpeningCost";
    private const string FixedAssetOpeningDepreciation = "FixedAssetOpeningDep";
    private const string SupplierAdvanceOpening = "SupplierAdvanceOpening";
    private const string CustomerAdvanceOpening = "CustomerAdvanceOpening";
    private const string ApWithholdingOpening = "ApWhtOpening";
    private const string ArWithholdingOpening = "ArWhtOpening";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinancePostingEngine _postingEngine;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IWorkflowService? _workflowService;

    public OpeningBalanceService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinancePostingEngine postingEngine,
        IFinanceAuditService? financeAuditService = null,
        IWorkflowService? workflowService = null)
    {
        _db = db;
        _currentUser = currentUser;
        _postingEngine = postingEngine;
        _financeAuditService = financeAuditService;
        _workflowService = workflowService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<OpeningBalanceBatchDto> CreateBatchAsync(
        CreateOpeningBalanceBatchDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        if (dto.Lines.Count == 0)
        {
            throw new InvalidOperationException("Opening balance batch requires at least one line.");
        }

        var book = NormalizeBook(dto.BookClassification);
        if (IsAllActiveBooks(book))
        {
            throw new InvalidOperationException("ALL_ACTIVE_BOOKS opening-balance posting remains disabled. Create an explicit balanced batch for one book classification.");
        }

        var period = await _db.FiscalPeriods
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == dto.FiscalPeriodId && !p.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Opening balance fiscal period was not found for the current tenant.");

        var batchNumber = string.IsNullOrWhiteSpace(dto.BatchNumber)
            ? $"OB-{dto.OpeningDate:yyyyMMdd}-{Guid.NewGuid():N}"[..24]
            : dto.BatchNumber.Trim();

        var duplicate = await _db.OpeningBalanceBatches.AnyAsync(
            b => b.TenantId == tenantId && b.BatchNumber == batchNumber && !b.IsDeleted,
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"Opening balance batch '{batchNumber}' already exists for this tenant.");
        }

        var now = DateTime.UtcNow;
        var userName = _currentUser.UserName ?? "system";
        var userId = CurrentUserId();
        var functionalCurrency = await GetFunctionalCurrencyAsync(tenantId, cancellationToken);
        var batch = new OpeningBalanceBatch
        {
            TenantId = tenantId,
            BatchNumber = batchNumber,
            ReferenceNumber = batchNumber,
            SourceReference = dto.SourceReference,
            Description = dto.Description,
            OpeningDate = dto.OpeningDate.Date,
            FiscalPeriodId = period.Id,
            BookClassification = book,
            Status = StatusDraft,
            IdempotencyKey = string.IsNullOrWhiteSpace(dto.IdempotencyKey)
                ? $"MIGRATION:OpeningBalance:{tenantId:N}:{Guid.NewGuid():N}"
                : dto.IdempotencyKey.Trim(),
            CreatedAt = now,
            CreatedBy = userName,
            CreatedById = userId
        };

        var lineNumber = 1;
        foreach (var lineDto in dto.Lines)
        {
            batch.Lines.Add(new OpeningBalanceLine
            {
                TenantId = tenantId,
                LineNumber = lineNumber++,
                AccountId = lineDto.AccountId,
                DebitAmount = RoundMoney(lineDto.DebitAmount),
                CreditAmount = RoundMoney(lineDto.CreditAmount),
                TransactionDebitAmount = lineDto.TransactionDebitAmount,
                TransactionCreditAmount = lineDto.TransactionCreditAmount,
                TransactionCurrencyCode = NormalizeCurrency(lineDto.TransactionCurrencyCode, functionalCurrency),
                FunctionalCurrencyCode = NormalizeCurrency(lineDto.FunctionalCurrencyCode, functionalCurrency),
                ExchangeRateId = lineDto.ExchangeRateId,
                ExchangeRateDate = lineDto.ExchangeRateDate,
                SegmentString = lineDto.SegmentString,
                BankAccountId = lineDto.BankAccountId,
                CounterpartyType = lineDto.CounterpartyType,
                CounterpartyId = lineDto.CounterpartyId,
                SourceReference = lineDto.SourceReference,
                Notes = lineDto.Notes,
                CreatedAt = now,
                CreatedBy = userName,
                CreatedById = userId
            });
        }

        RecalculateTotals(batch);
        _db.OpeningBalanceBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(
            FinanceAuditEvents.OpeningBalanceBatchCreated,
            batch,
            afterValues: new { batch.BatchNumber, batch.OpeningDate, batch.TotalDebit, batch.TotalCredit, lineCount = batch.Lines.Count },
            cancellationToken: cancellationToken);

        return await MapBatchAsync(batch.Id, cancellationToken)
            ?? throw new InvalidOperationException("Opening balance batch was created but could not be reloaded.");
    }

    public async Task<OpeningBalanceBatchDto> CreateFixedAssetBatchAsync(
        CreateFixedAssetOpeningBalanceBatchDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var requestedBookValueIds = dto.FixedAssetBookValueIds.Distinct().ToArray();
        if (requestedBookValueIds.Length == 0)
        {
            // An explicit selection prevents a user from accidentally posting every imported asset
            // when the screen is filtered or when another preparer imports more rows concurrently.
            throw new InvalidOperationException("Select at least one fixed asset for the opening-balance batch.");
        }

        var book = NormalizeBook(dto.BookClassification);
        var bookValues = await _db.FixedAssetBookValues
            .Include(value => value.FixedAsset)
                .ThenInclude(asset => asset.Category)
            .Include(value => value.AccountingBook)
            .Where(value =>
                value.TenantId == tenantId &&
                requestedBookValueIds.Contains(value.Id) &&
                value.BookClassification == book &&
                !value.IsDeleted &&
                !value.FixedAsset.IsDeleted)
            .OrderBy(value => value.FixedAsset.AssetCode)
            .ToListAsync(cancellationToken);

        var loadedBookValueIds = bookValues.Select(value => value.Id).ToHashSet();
        var missingBookValueIds = requestedBookValueIds.Where(id => !loadedBookValueIds.Contains(id)).ToArray();
        if (missingBookValueIds.Length > 0)
        {
            // Treat a cross-tenant row and a row from a different accounting book identically.
            // This avoids leaking another tenant's evidence while ensuring a stale UI selection
            // can never be reinterpreted as the current header book.
            throw new InvalidOperationException(
                $"{missingBookValueIds.Length} selected fixed-asset book value(s) do not belong to the {book} book for the current tenant.");
        }

        var openingDate = dto.OpeningDate.Date;
        foreach (var value in bookValues)
        {
            ValidateFixedAssetOpeningCandidate(value, openingDate);
        }

        var settings = await _db.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && !candidate.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are required before fixed-asset opening balances can be prepared.");
        var clearingAccountId = settings.MigrationClearingAccountId
            ?? throw new InvalidOperationException("Migration Clearing Account is not configured in Finance Settings.");
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");

        var lines = new List<CreateOpeningBalanceLineDto>();
        foreach (var value in bookValues)
        {
            // Keep one evidenced cost line per asset/book rather than aggregating by category. The
            // posting engine still creates one journal, while each register row retains an exact
            // source-to-ledger path that migration sign-off can verify independently.
            lines.Add(new CreateOpeningBalanceLineDto
            {
                AccountId = value.FixedAsset.Category.AssetAccountId,
                DebitAmount = RoundMoney(value.AcquisitionCost),
                TransactionCurrencyCode = functionalCurrency,
                FunctionalCurrencyCode = functionalCurrency,
                SegmentString = value.FixedAsset.CurrentSegmentString,
                CounterpartyType = FixedAssetOpeningCost,
                CounterpartyId = value.Id,
                SourceReference = value.FixedAsset.AssetCode,
                Notes = $"Opening acquisition cost for {value.FixedAsset.AssetCode} ({book})"
            });

            if (RoundMoney(value.AccumulatedDepreciation) > 0m)
            {
                lines.Add(new CreateOpeningBalanceLineDto
                {
                    AccountId = value.FixedAsset.Category.AccumulatedDepreciationAccountId,
                    CreditAmount = RoundMoney(value.AccumulatedDepreciation),
                    TransactionCurrencyCode = functionalCurrency,
                    FunctionalCurrencyCode = functionalCurrency,
                    SegmentString = value.FixedAsset.CurrentSegmentString,
                    CounterpartyType = FixedAssetOpeningDepreciation,
                    CounterpartyId = value.Id,
                    SourceReference = value.FixedAsset.AssetCode,
                    Notes = $"Opening accumulated depreciation for {value.FixedAsset.AssetCode} ({book})"
                });
            }
        }

        var totalNetBookValue = RoundMoney(bookValues.Sum(value => value.NetBookValue));
        if (totalNetBookValue > 0m)
        {
            lines.Add(new CreateOpeningBalanceLineDto
            {
                AccountId = clearingAccountId,
                CreditAmount = totalNetBookValue,
                TransactionCurrencyCode = functionalCurrency,
                FunctionalCurrencyCode = functionalCurrency,
                SourceReference = dto.SourceReference,
                Notes = "Migration clearing offset for fixed-asset opening net book value"
            });
        }

        return await CreateBatchAsync(new CreateOpeningBalanceBatchDto
        {
            BatchNumber = dto.BatchNumber ?? string.Empty,
            SourceReference = dto.SourceReference,
            Description = string.IsNullOrWhiteSpace(dto.Description)
                ? $"Fixed-asset {book} opening balances at {openingDate:yyyy-MM-dd}"
                : dto.Description,
            OpeningDate = openingDate,
            FiscalPeriodId = dto.FiscalPeriodId,
            BookClassification = book,
            IdempotencyKey = dto.IdempotencyKey,
            Lines = lines
        }, cancellationToken);
    }

    public async Task<SubledgerOpeningBalanceReadinessDto> GetSubledgerReadinessAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var ap = await _db.VendorInvoices
            .AsNoTracking()
            .Where(invoice => invoice.TenantId == tenantId && invoice.IsOpeningBalance && !invoice.IsDeleted)
            .Select(invoice => new { invoice.JournalEntryId, invoice.BaseCurrencyAmount })
            .ToListAsync(cancellationToken);
        var ar = await _db.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.TenantId == tenantId && invoice.IsOpeningBalance && !invoice.IsDeleted)
            .Select(invoice => new { invoice.JournalEntryId, invoice.BaseCurrencyAmount })
            .ToListAsync(cancellationToken);
        var candidates = await GetFixedAssetOpeningCandidatesAsync(tenantId, cancellationToken);
        var vendorOpeningFacts = await _db.Set<VendorPayment>().AsNoTracking()
            .Where(payment => payment.TenantId == tenantId && !payment.IsDeleted && payment.OpeningBalanceType != null)
            .Select(payment => new
            {
                payment.OpeningBalanceType,
                payment.JournalEntryId,
                payment.TotalAmount,
                payment.ExchangeRate,
                payment.WithholdingTaxAmount
            })
            .ToListAsync(cancellationToken);
        var customerOpeningFacts = await _db.Set<CustomerPayment>().AsNoTracking()
            .Where(payment => payment.TenantId == tenantId && !payment.IsDeleted && payment.OpeningBalanceType != null)
            .Select(payment => new
            {
                payment.OpeningBalanceType,
                payment.JournalEntryId,
                payment.TotalAmount,
                payment.ExchangeRate,
                payment.WithholdingTaxAmount
            })
            .ToListAsync(cancellationToken);
        var supplierAdvances = vendorOpeningFacts.Where(item => item.OpeningBalanceType == SupplierAdvanceOpening).ToList();
        var customerAdvances = customerOpeningFacts.Where(item => item.OpeningBalanceType == CustomerAdvanceOpening).ToList();
        var apWithholding = vendorOpeningFacts.Where(item => item.OpeningBalanceType == ApWithholdingOpening).ToList();
        var arWithholding = customerOpeningFacts.Where(item => item.OpeningBalanceType == ArWithholdingOpening).ToList();

        var warnings = new List<string>();
        if (ap.Any(item => !item.JournalEntryId.HasValue))
            warnings.Add("One or more AP opening invoices are not posted and therefore do not yet support supplier aging sign-off.");
        if (ar.Any(item => !item.JournalEntryId.HasValue))
            warnings.Add("One or more AR opening invoices are not posted and therefore do not yet support customer aging sign-off.");
        if (candidates.Any(item => !item.OpeningPostedToGl))
            warnings.Add("One or more imported fixed-asset opening book values are not linked to an approved opening GL journal.");
        if (supplierAdvances.Concat(apWithholding).Any(item => !item.JournalEntryId.HasValue)
            || customerAdvances.Concat(arWithholding).Any(item => !item.JournalEntryId.HasValue))
            warnings.Add("One or more specialised advance/WHT cutover facts are awaiting approved opening-batch posting.");

        return new SubledgerOpeningBalanceReadinessDto
        {
            ApOpeningInvoiceCount = ap.Count,
            PostedApOpeningInvoiceCount = ap.Count(item => item.JournalEntryId.HasValue),
            ApOpeningInvoiceFunctionalAmount = RoundMoney(ap.Sum(item => item.BaseCurrencyAmount)),
            ArOpeningInvoiceCount = ar.Count,
            PostedArOpeningInvoiceCount = ar.Count(item => item.JournalEntryId.HasValue),
            ArOpeningInvoiceFunctionalAmount = RoundMoney(ar.Sum(item => item.BaseCurrencyAmount)),
            SupplierAdvanceOpeningCount = supplierAdvances.Count,
            PostedSupplierAdvanceOpeningCount = supplierAdvances.Count(item => item.JournalEntryId.HasValue),
            SupplierAdvanceOpeningFunctionalAmount = RoundMoney(supplierAdvances.Sum(item => item.TotalAmount * item.ExchangeRate)),
            CustomerAdvanceOpeningCount = customerAdvances.Count,
            PostedCustomerAdvanceOpeningCount = customerAdvances.Count(item => item.JournalEntryId.HasValue),
            CustomerAdvanceOpeningFunctionalAmount = RoundMoney(customerAdvances.Sum(item => item.TotalAmount * item.ExchangeRate)),
            ApWithholdingOpeningCount = apWithholding.Count,
            PostedApWithholdingOpeningCount = apWithholding.Count(item => item.JournalEntryId.HasValue),
            ApWithholdingOpeningAmount = RoundMoney(apWithholding.Sum(item => item.WithholdingTaxAmount)),
            ArWithholdingOpeningCount = arWithholding.Count,
            PostedArWithholdingOpeningCount = arWithholding.Count(item => item.JournalEntryId.HasValue),
            ArWithholdingOpeningAmount = RoundMoney(arWithholding.Sum(item => item.WithholdingTaxAmount)),
            FixedAssetOpeningBookValueCount = candidates.Count,
            PostedFixedAssetOpeningBookValueCount = candidates.Count(item => item.OpeningPostedToGl),
            FixedAssetOpeningCost = RoundMoney(candidates.Sum(item => item.AcquisitionCost)),
            FixedAssetOpeningAccumulatedDepreciation = RoundMoney(candidates.Sum(item => item.AccumulatedDepreciation)),
            FixedAssetOpeningNetBookValue = RoundMoney(candidates.Sum(item => item.NetBookValue)),
            FixedAssetCandidates = candidates,
            Warnings = warnings
        };
    }

    public async Task<OpeningBalanceBatchDto> CreateSupplierAdvanceBatchAsync(
        CreateSupplierAdvanceOpeningBalanceDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == dto.SupplierId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Supplier was not found for the current tenant.");
        var context = await ResolveSpecializedOpeningContextAsync(dto, cancellationToken);
        var advanceAccountId = context.Settings.SupplierAdvanceAccountId
            ?? throw new InvalidOperationException("Supplier Advance Account is not configured in Finance Settings.");
        var paymentId = Guid.NewGuid();
        var payment = new VendorPayment
        {
            Id = paymentId, TenantId = tenantId, SupplierId = supplier.Id,
            PaymentNumber = BuildOpeningReference("AP-ADV-OPEN", paymentId), PaymentDate = dto.OpeningDate.Date,
            TotalAmount = RoundMoney(dto.Amount), AllocatedAmount = 0m, IsSupplierAdvance = true,
            CurrencyCode = context.Currency, ExchangeRate = context.Rate, ExchangeRateId = dto.ExchangeRateId,
            Status = VendorPaymentStatus.Draft, OpeningBalanceType = SupplierAdvanceOpening,
            OpeningSourceReference = NormalizeOptional(dto.SourceReference), TransactionReference = NormalizeOptional(dto.SourceReference),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName ?? "system", CreatedById = CurrentUserId()
        };
        _db.Set<VendorPayment>().Add(payment);
        var batch = await CreateSpecializedBatchAsync(dto, paymentId, SupplierAdvanceOpening,
            advanceAccountId, debit: context.FunctionalAmount, credit: 0m,
            context.Settings.MigrationClearingAccountId!.Value, clearingDebit: 0m, clearingCredit: context.FunctionalAmount,
            transactionDebit: dto.Amount, transactionCredit: dto.Amount, context, cancellationToken);
        payment.OpeningBalanceBatchId = batch.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<SpecializedOpeningBalanceOptionsDto> GetSpecializedOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var settings = await _db.FinanceSettings.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        // Return only the tenant-scoped lookup projection needed by the Finance cutover screen.
        // This keeps source IDs server-owned and avoids coupling the workflow to another module's UI.
        var suppliers = await _db.Suppliers.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive && item.Status == "Active")
            .OrderBy(item => item.Name)
            .Select(item => new OpeningBalancePartyOptionDto { Id = item.Id, Code = item.SupplierCode, Name = item.Name })
            .ToListAsync(cancellationToken);
        var customers = await _db.Set<Customer>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.Status == "Active")
            .OrderBy(item => item.CustomerName)
            .Select(item => new OpeningBalancePartyOptionDto { Id = item.Id, Code = item.CustomerCode, Name = item.CustomerName })
            .ToListAsync(cancellationToken);
        var taxes = await _db.Taxes.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive && item.Category == TaxCategory.Withholding)
            .OrderBy(item => item.Code)
            .Select(item => new OpeningBalanceWhtOptionDto
            {
                Id = item.Id, Code = item.Code, Name = item.Name, Rate = item.Rate,
                PayableAccountId = item.TaxPayableAccountId, ReceivableAccountId = item.TaxReceivableAccountId
            })
            .ToListAsync(cancellationToken);

        return new SpecializedOpeningBalanceOptionsDto
        {
            FunctionalCurrencyCode = NormalizeCurrency(settings?.BaseCurrency, "GHS"),
            Suppliers = suppliers,
            Customers = customers,
            WithholdingTaxes = taxes
        };
    }

    public async Task<OpeningBalanceBatchDto> CreateCustomerAdvanceBatchAsync(
        CreateCustomerAdvanceOpeningBalanceDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var customer = await _db.Set<Customer>().AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == dto.CustomerId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Customer was not found for the current tenant.");
        var context = await ResolveSpecializedOpeningContextAsync(dto, cancellationToken);
        var advanceAccountId = context.Settings.CustomerAdvanceAccountId
            ?? throw new InvalidOperationException("Customer Advance Account is not configured in Finance Settings.");
        var paymentId = Guid.NewGuid();
        var reference = BuildOpeningReference("AR-ADV-OPEN", paymentId);
        var payment = new CustomerPayment
        {
            Id = paymentId, TenantId = tenantId, CustomerId = customer.Id,
            PaymentNumber = reference, ReferenceNumber = reference, PaymentDate = dto.OpeningDate.Date,
            TotalAmount = RoundMoney(dto.Amount), AllocatedAmount = 0m, IsCustomerAdvance = true,
            CurrencyCode = context.Currency, ExchangeRate = context.Rate, ExchangeRateId = dto.ExchangeRateId,
            PaymentMethod = "OpeningBalance", Status = "Pending", OpeningBalanceType = CustomerAdvanceOpening,
            OpeningSourceReference = NormalizeOptional(dto.SourceReference), TransactionReference = NormalizeOptional(dto.SourceReference),
            CreatedAt = DateTime.UtcNow, CreatedBy = _currentUser.UserName ?? "system", CreatedById = CurrentUserId()
        };
        _db.Set<CustomerPayment>().Add(payment);
        var batch = await CreateSpecializedBatchAsync(dto, paymentId, CustomerAdvanceOpening,
            // The source-bearing line must be the customer-advance liability, not migration clearing.
            // Validation and later settlement rebuilds follow this line back to the canonical receipt lot.
            advanceAccountId, debit: 0m, credit: context.FunctionalAmount,
            context.Settings.MigrationClearingAccountId!.Value, clearingDebit: context.FunctionalAmount, clearingCredit: 0m,
            transactionDebit: dto.Amount, transactionCredit: dto.Amount, context, cancellationToken);
        payment.OpeningBalanceBatchId = batch.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<OpeningBalanceBatchDto> CreateApWithholdingBatchAsync(
        CreateApWithholdingOpeningBalanceDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == dto.SupplierId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Supplier was not found for the current tenant.");
        var context = await ResolveSpecializedOpeningContextAsync(dto, cancellationToken, requireFunctionalCurrency: true);
        await ValidateWithholdingConfigurationAsync(dto.TaxId, dto.WithholdingTaxAccountId, useReceivableAccount: false, cancellationToken);
        if (dto.TaxableBase < dto.Amount || dto.NetPaidAmount < 0m)
            throw new InvalidOperationException("AP WHT opening taxable base/net-paid evidence is invalid.");
        var paymentId = Guid.NewGuid();
        var reference = BuildOpeningReference("AP-WHT-OPEN", paymentId);
        var payment = new VendorPayment
        {
            Id = paymentId, TenantId = tenantId, SupplierId = supplier.Id, PaymentNumber = reference,
            PaymentDate = dto.OpeningDate.Date, TotalAmount = RoundMoney(dto.NetPaidAmount), AllocatedAmount = RoundMoney(dto.NetPaidAmount),
            CurrencyCode = context.Currency, ExchangeRate = 1m, Status = VendorPaymentStatus.Draft,
            WithholdingTaxId = dto.TaxId, WithholdingTaxAccountId = dto.WithholdingTaxAccountId,
            WithholdingTaxBaseAmount = RoundMoney(dto.TaxableBase), WithholdingTaxAmount = RoundMoney(dto.Amount),
            // Preserve the statutory rate as immutable cutover evidence. Certificate and remittance
            // reports use this snapshot rather than silently inheriting a later tax-master change.
            WithholdingTaxRate = RoundRate(dto.Amount / dto.TaxableBase * 100m),
            OpeningBalanceType = ApWithholdingOpening, OpeningSourceReference = NormalizeOptional(dto.SourceReference),
            TransactionReference = NormalizeOptional(dto.SourceReference), CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName ?? "system", CreatedById = CurrentUserId()
        };
        _db.Set<VendorPayment>().Add(payment);
        var batch = await CreateSpecializedBatchAsync(dto, paymentId, ApWithholdingOpening,
            // WHT source evidence belongs on the statutory payable line. Keeping the canonical
            // payment ID off migration clearing prevents evidence drift during approval checks.
            dto.WithholdingTaxAccountId, debit: 0m, credit: context.FunctionalAmount,
            context.Settings.MigrationClearingAccountId!.Value, clearingDebit: context.FunctionalAmount, clearingCredit: 0m,
            transactionDebit: dto.Amount, transactionCredit: dto.Amount, context, cancellationToken);
        payment.OpeningBalanceBatchId = batch.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<OpeningBalanceBatchDto> CreateArWithholdingBatchAsync(
        CreateArWithholdingOpeningBalanceDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var customer = await _db.Set<Customer>().AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == dto.CustomerId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Customer was not found for the current tenant.");
        var context = await ResolveSpecializedOpeningContextAsync(dto, cancellationToken, requireFunctionalCurrency: true);
        await ValidateWithholdingConfigurationAsync(dto.TaxId, dto.WithholdingTaxAccountId, useReceivableAccount: true, cancellationToken);
        var paymentId = Guid.NewGuid();
        var reference = BuildOpeningReference("AR-WHT-OPEN", paymentId);
        var payment = new CustomerPayment
        {
            Id = paymentId, TenantId = tenantId, CustomerId = customer.Id, PaymentNumber = reference, ReferenceNumber = reference,
            PaymentDate = dto.OpeningDate.Date, TotalAmount = 0m, AllocatedAmount = 0m,
            CurrencyCode = context.Currency, ExchangeRate = 1m, PaymentMethod = "OpeningBalance", Status = "Pending",
            WithholdingTaxId = dto.TaxId, WithholdingTaxAccountId = dto.WithholdingTaxAccountId,
            WithholdingTaxAmount = RoundMoney(dto.Amount), WithholdingCertificateNumber = NormalizeOptional(dto.CertificateNumber),
            WithholdingCertificateDate = dto.CertificateDate?.Date, OpeningBalanceType = ArWithholdingOpening,
            OpeningSourceReference = NormalizeOptional(dto.SourceReference), TransactionReference = NormalizeOptional(dto.SourceReference),
            CreatedAt = DateTime.UtcNow, CreatedBy = _currentUser.UserName ?? "system", CreatedById = CurrentUserId()
        };
        _db.Set<CustomerPayment>().Add(payment);
        var batch = await CreateSpecializedBatchAsync(dto, paymentId, ArWithholdingOpening,
            dto.WithholdingTaxAccountId, debit: context.FunctionalAmount, credit: 0m,
            context.Settings.MigrationClearingAccountId!.Value, clearingDebit: 0m, clearingCredit: context.FunctionalAmount,
            transactionDebit: dto.Amount, transactionCredit: dto.Amount, context, cancellationToken);
        payment.OpeningBalanceBatchId = batch.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    private async Task<OpeningBalanceBatchDto> CreateSpecializedBatchAsync(
        CreateSpecializedOpeningBalanceDto dto,
        Guid sourceId,
        string evidenceType,
        Guid primaryAccountId,
        decimal debit,
        decimal credit,
        Guid offsetAccountId,
        decimal clearingDebit,
        decimal clearingCredit,
        decimal transactionDebit,
        decimal transactionCredit,
        SpecializedOpeningContext context,
        CancellationToken cancellationToken)
    {
        // Only the source-facing line carries the canonical record ID. The migration-clearing
        // line is the balancing cutover bridge and must not be mistaken for a second subledger fact.
        var primaryTransactionDebit = debit > 0m ? RoundMoney(transactionDebit) : 0m;
        var primaryTransactionCredit = credit > 0m ? RoundMoney(transactionCredit) : 0m;
        return await CreateBatchAsync(new CreateOpeningBalanceBatchDto
        {
            BatchNumber = dto.BatchNumber ?? string.Empty,
            SourceReference = NormalizeOptional(dto.SourceReference),
            Description = NormalizeOptional(dto.Description) ?? $"{evidenceType} cutover at {dto.OpeningDate:yyyy-MM-dd}",
            OpeningDate = dto.OpeningDate.Date,
            FiscalPeriodId = dto.FiscalPeriodId,
            BookClassification = dto.BookClassification,
            IdempotencyKey = $"MIGRATION:{evidenceType}:{TenantId:N}:{sourceId:N}",
            Lines = new[]
            {
                new CreateOpeningBalanceLineDto
                {
                    AccountId = primaryAccountId, DebitAmount = debit, CreditAmount = credit,
                    TransactionDebitAmount = primaryTransactionDebit, TransactionCreditAmount = primaryTransactionCredit,
                    TransactionCurrencyCode = context.Currency, FunctionalCurrencyCode = context.FunctionalCurrency,
                    ExchangeRateId = context.ExchangeRateId, ExchangeRateDate = dto.OpeningDate.Date,
                    CounterpartyType = evidenceType, CounterpartyId = sourceId,
                    SourceReference = NormalizeOptional(dto.SourceReference), Notes = $"Controlled {evidenceType} source evidence"
                },
                new CreateOpeningBalanceLineDto
                {
                    AccountId = offsetAccountId, DebitAmount = clearingDebit, CreditAmount = clearingCredit,
                    TransactionDebitAmount = clearingDebit, TransactionCreditAmount = clearingCredit,
                    TransactionCurrencyCode = context.FunctionalCurrency, FunctionalCurrencyCode = context.FunctionalCurrency,
                    SourceReference = NormalizeOptional(dto.SourceReference), Notes = $"Migration clearing offset for {evidenceType}"
                }
            }
        }, cancellationToken);
    }

    private async Task<SpecializedOpeningContext> ResolveSpecializedOpeningContextAsync(
        CreateSpecializedOpeningBalanceDto dto,
        CancellationToken cancellationToken,
        bool requireFunctionalCurrency = false)
    {
        if (dto.Amount <= 0m || dto.OpeningDate == default || dto.FiscalPeriodId == Guid.Empty)
            throw new InvalidOperationException("A positive amount, opening date, and fiscal period are required.");
        var settings = await _db.FinanceSettings.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance Settings are required for specialised opening balances.");
        if (!settings.MigrationClearingAccountId.HasValue)
            throw new InvalidOperationException("Migration Clearing Account is not configured in Finance Settings.");
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
        var currency = NormalizeCurrency(dto.CurrencyCode, functionalCurrency);
        if (requireFunctionalCurrency && !string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Statutory WHT opening balances must use functional currency {functionalCurrency}.");
        var rate = string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? 1m : dto.ExchangeRate;
        if (rate <= 0m)
            throw new InvalidOperationException("A positive opening exchange rate is required.");
        if (!string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            if (!dto.ExchangeRateId.HasValue)
                throw new InvalidOperationException("Foreign-currency opening advances require approved exchange-rate evidence.");
            var approvedRate = await _db.ExchangeRates.AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == dto.ExchangeRateId.Value && !item.IsDeleted,
                cancellationToken)
                ?? throw new InvalidOperationException("Opening exchange-rate evidence was not found for the current tenant.");
            if (approvedRate.ApprovalStatus != RateApprovalStatus.Approved ||
                !string.Equals(approvedRate.TargetCurrencyCode, currency, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(approvedRate.BaseCurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase) ||
                RoundRate(approvedRate.Rate) != RoundRate(rate))
                throw new InvalidOperationException("Opening exchange-rate currency/value does not match approved rate evidence.");
        }
        return new SpecializedOpeningContext(settings, functionalCurrency, currency, rate,
            RoundMoney(dto.Amount * rate), dto.ExchangeRateId);
    }

    private async Task ValidateWithholdingConfigurationAsync(
        Guid taxId,
        Guid accountId,
        bool useReceivableAccount,
        CancellationToken cancellationToken)
    {
        if (taxId == Guid.Empty || accountId == Guid.Empty)
            throw new InvalidOperationException("A configured WHT tax and GL account are required.");
        var tax = await _db.Taxes.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == taxId && !item.IsDeleted && item.IsActive && item.Category == TaxCategory.Withholding,
            cancellationToken);
        var account = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == accountId && !item.IsDeleted,
            cancellationToken);
        // BusinessEntity.IsActive is a computed CLR property and therefore cannot be translated
        // by every EF provider. Materialise the single tenant-scoped account before evaluating it.
        if (tax == null || account == null || !account.IsActive)
            throw new InvalidOperationException("WHT tax/account configuration was not found or is inactive for the current tenant.");

        // A preparer must not be able to route a statutory opening balance through an arbitrary
        // active GL account. Enforce the payable/receivable mapping approved on the tax master.
        var configuredAccountId = useReceivableAccount ? tax.TaxReceivableAccountId : tax.TaxPayableAccountId;
        if (configuredAccountId != accountId)
        {
            throw new InvalidOperationException(useReceivableAccount
                ? "The selected account is not the receivable account configured for this WHT tax."
                : "The selected account is not the payable account configured for this WHT tax.");
        }
    }

    private static string BuildOpeningReference(string prefix, Guid id) => $"{prefix}-{id:N}"[..Math.Min(50, prefix.Length + 1 + 32)];
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal RoundRate(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    private sealed record SpecializedOpeningContext(
        FinanceSettings Settings,
        string FunctionalCurrency,
        string Currency,
        decimal Rate,
        decimal FunctionalAmount,
        Guid? ExchangeRateId);

    public async Task<OpeningBalanceBatchDto?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        => await MapBatchAsync(batchId, cancellationToken);

    public async Task<IReadOnlyList<OpeningBalanceBatchDto>> GetBatchesAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        return await _db.OpeningBalanceBatches
            .AsNoTracking()
            .Where(batch => batch.TenantId == tenantId && !batch.IsDeleted)
            .OrderByDescending(batch => batch.UpdatedAt ?? batch.CreatedAt)
            .ThenByDescending(batch => batch.CreatedAt)
            .Select(batch => new OpeningBalanceBatchDto
            {
                Id = batch.Id,
                TenantId = batch.TenantId,
                BatchNumber = batch.BatchNumber,
                SourceReference = batch.SourceReference,
                Description = batch.Description,
                OpeningDate = batch.OpeningDate,
                FiscalPeriodId = batch.FiscalPeriodId,
                FiscalPeriodCode = batch.FiscalPeriod.PeriodCode,
                BookClassification = batch.BookClassification,
                Status = batch.Status,
                IdempotencyKey = batch.IdempotencyKey,
                TotalDebit = batch.TotalDebit,
                TotalCredit = batch.TotalCredit,
                Difference = batch.Difference,
                JournalEntryId = batch.JournalEntryId,
                PostingEventId = batch.PostingEventId,
                WorkflowInstanceId = batch.WorkflowInstanceId,
                ValidatedAt = batch.ValidatedAt,
                SubmittedAt = batch.SubmittedAt,
                ApprovedAt = batch.ApprovedAt,
                PostedAt = batch.PostedAt,
                FailureReason = batch.FailureReason,
                CreatedAt = batch.CreatedAt,
                UpdatedAt = batch.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<OpeningBalanceBatchDto> UpdateBatchAsync(
        Guid batchId,
        UpdateOpeningBalanceBatchDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
        if (HasGeneratedSubledgerOpeningEvidence(batch))
        {
            // Generated fixed-asset lines are derived from the imported register and category GL
            // mappings. Letting a user replace them with arbitrary accounts would break the exact
            // subledger-to-ledger evidence FIN-LIM-0048 is intended to provide.
            throw new InvalidOperationException(
                "Generated subledger opening batches cannot be edited manually. Correct the source evidence and create a new batch.");
        }
        if (!IsEditableStatus(batch.Status))
        {
            throw new InvalidOperationException($"Opening balance batch '{batch.BatchNumber}' cannot be edited while it is {batch.Status}.");
        }

        if (dto.Lines == null || dto.Lines.Count == 0)
        {
            throw new InvalidOperationException("Opening balance batch requires at least one line.");
        }

        var book = NormalizeBook(dto.BookClassification);
        if (IsAllActiveBooks(book))
        {
            throw new InvalidOperationException("ALL_ACTIVE_BOOKS opening-balance posting remains disabled. Select one explicit book classification.");
        }

        var period = await _db.FiscalPeriods
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == dto.FiscalPeriodId && !candidate.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Opening balance fiscal period was not found for the current tenant.");

        var beforeValues = new
        {
            batch.SourceReference,
            batch.Description,
            batch.OpeningDate,
            batch.FiscalPeriodId,
            batch.BookClassification,
            batch.TotalDebit,
            batch.TotalCredit,
            lineCount = batch.Lines.Count
        };

        batch.SourceReference = string.IsNullOrWhiteSpace(dto.SourceReference) ? null : dto.SourceReference.Trim();
        batch.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        batch.OpeningDate = dto.OpeningDate.Date;
        batch.FiscalPeriodId = period.Id;
        batch.BookClassification = book;

        var now = DateTime.UtcNow;
        var userName = _currentUser.UserName ?? "system";
        var userId = CurrentUserId();
        var functionalCurrency = await GetFunctionalCurrencyAsync(tenantId, cancellationToken);
        var existingLines = batch.Lines.OrderBy(line => line.LineNumber).ToList();
        for (var index = 0; index < dto.Lines.Count; index++)
        {
            var lineDto = dto.Lines[index];
            var line = index < existingLines.Count
                ? existingLines[index]
                : new OpeningBalanceLine
                {
                    TenantId = tenantId,
                    OpeningBalanceBatchId = batch.Id,
                    CreatedAt = now,
                    CreatedBy = userName,
                    CreatedById = userId
                };

            line.LineNumber = index + 1;
            line.AccountId = lineDto.AccountId;
            line.DebitAmount = RoundMoney(lineDto.DebitAmount);
            line.CreditAmount = RoundMoney(lineDto.CreditAmount);
            line.TransactionDebitAmount = lineDto.TransactionDebitAmount;
            line.TransactionCreditAmount = lineDto.TransactionCreditAmount;
            line.TransactionCurrencyCode = NormalizeCurrency(lineDto.TransactionCurrencyCode, functionalCurrency);
            line.FunctionalCurrencyCode = NormalizeCurrency(lineDto.FunctionalCurrencyCode, functionalCurrency);
            line.ExchangeRateId = lineDto.ExchangeRateId;
            line.ExchangeRateDate = lineDto.ExchangeRateDate;
            line.SegmentString = lineDto.SegmentString;
            line.BankAccountId = lineDto.BankAccountId;
            line.CounterpartyType = lineDto.CounterpartyType;
            line.CounterpartyId = lineDto.CounterpartyId;
            line.SourceReference = lineDto.SourceReference;
            line.Notes = lineDto.Notes;
            line.UpdatedAt = now;
            line.UpdatedBy = userName;
            line.LastModifiedById = userId;

            if (index >= existingLines.Count)
            {
                batch.Lines.Add(line);
            }
        }

        if (existingLines.Count > dto.Lines.Count)
        {
            _db.OpeningBalanceLines.RemoveRange(existingLines.Skip(dto.Lines.Count));
        }

        RecalculateTotals(batch);
        batch.Status = StatusDraft;
        batch.ValidatedAt = null;
        batch.SubmittedAt = null;
        batch.ApprovedAt = null;
        batch.WorkflowInstanceId = null;
        batch.FailureReason = null;
        batch.FailedAt = null;
        batch.UpdatedAt = now;
        batch.UpdatedBy = userName;
        batch.LastModifiedById = userId;
        await _db.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(
            FinanceAuditEvents.OpeningBalanceBatchUpdated,
            batch,
            beforeValues: beforeValues,
            afterValues: new
            {
                batch.SourceReference,
                batch.Description,
                batch.OpeningDate,
                batch.FiscalPeriodId,
                batch.BookClassification,
                batch.TotalDebit,
                batch.TotalCredit,
                lineCount = batch.Lines.Count
            },
            cancellationToken: cancellationToken);

        return await MapBatchAsync(batch.Id, cancellationToken)
            ?? throw new InvalidOperationException("Opening balance batch was updated but could not be reloaded.");
    }

    public async Task<OpeningBalanceValidationResultDto> ValidateBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
        var errors = new List<string>();
        var warnings = new List<string>();
        var tenantFunctionalCurrency = await GetFunctionalCurrencyAsync(tenantId, cancellationToken);

        if (batch.Lines.Count == 0)
        {
            errors.Add("Opening balance batch requires at least one line.");
        }

        if (IsAllActiveBooks(batch.BookClassification))
        {
            errors.Add("ALL_ACTIVE_BOOKS opening-balance posting remains disabled.");
        }

        var period = await _db.FiscalPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == batch.FiscalPeriodId && !p.IsDeleted, cancellationToken);
        if (period == null)
        {
            errors.Add("Opening balance fiscal period is missing or belongs to another tenant.");
        }
        else if (batch.OpeningDate.Date < period.StartDate.Date || batch.OpeningDate.Date > period.EndDate.Date)
        {
            errors.Add("Opening date must fall within the selected fiscal period date range.");
        }

        foreach (var line in batch.Lines.OrderBy(l => l.LineNumber))
        {
            if (line.DebitAmount < 0 || line.CreditAmount < 0)
            {
                errors.Add($"Line {line.LineNumber}: debit and credit amounts cannot be negative.");
            }

            if (line.DebitAmount == 0 && line.CreditAmount == 0)
            {
                errors.Add($"Line {line.LineNumber}: either debit or credit amount is required.");
            }

            if (line.DebitAmount > 0 && line.CreditAmount > 0)
            {
                errors.Add($"Line {line.LineNumber}: a line cannot contain both debit and credit amounts.");
            }

            if (!string.Equals(line.FunctionalCurrencyCode, tenantFunctionalCurrency, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Line {line.LineNumber}: functional currency must match the tenant functional currency '{tenantFunctionalCurrency}'.");
            }

            if (!string.Equals(line.TransactionCurrencyCode, tenantFunctionalCurrency, StringComparison.OrdinalIgnoreCase))
            {
                // Freehand GL imports still cannot invent an original foreign amount. The only
                // exception is a server-generated specialised advance line: its canonical payment,
                // approved rate ID, native amount, and functional amount are revalidated below.
                if (!IsSpecializedOpeningLine(line) || !line.ExchangeRateId.HasValue ||
                    (!line.TransactionDebitAmount.HasValue && !line.TransactionCreditAmount.HasValue))
                {
                    errors.Add($"Line {line.LineNumber}: foreign-currency opening balances are not supported outside controlled specialised source and approved FX evidence.");
                }
            }

            var account = line.Account;
            if (account == null)
            {
                errors.Add($"Line {line.LineNumber}: account is missing.");
                continue;
            }

            if (account.TenantId != tenantId)
            {
                errors.Add($"Line {line.LineNumber}: account '{account.AccountCode}' belongs to another tenant.");
            }

            if (account.IsDeleted || account.Status != AccountStatus.Active)
            {
                errors.Add($"Line {line.LineNumber}: account '{account.AccountCode}' is not active.");
            }

            if (!account.AllowDirectPosting)
            {
                errors.Add($"Line {line.LineNumber}: account '{account.AccountCode}' does not allow direct posting.");
            }
        }

        await ValidateFixedAssetOpeningEvidenceAsync(batch, errors, cancellationToken);
        await ValidateSpecializedOpeningEvidenceAsync(batch, errors, cancellationToken);

        RecalculateTotals(batch);
        if (batch.TotalDebit != batch.TotalCredit)
        {
            errors.Add("Opening balance batch must be balanced. Single-sided imports and silent suspense plugs are not enabled.");
        }

        batch.ValidatedAt = DateTime.UtcNow;
        if (errors.Count == 0 && string.Equals(batch.Status, StatusDraft, StringComparison.OrdinalIgnoreCase))
        {
            batch.Status = StatusValidated;
        }
        batch.FailureReason = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = _currentUser.UserName ?? "system";
        batch.LastModifiedById = CurrentUserId();
        await _db.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(
            FinanceAuditEvents.OpeningBalanceBatchValidated,
            batch,
            afterValues: new { isValid = errors.Count == 0, errors, warnings, batch.TotalDebit, batch.TotalCredit, batch.Difference },
            reason: batch.FailureReason,
            cancellationToken: cancellationToken);

        return new OpeningBalanceValidationResultDto
        {
            BatchId = batch.Id,
            IsValid = errors.Count == 0,
            TotalDebit = batch.TotalDebit,
            TotalCredit = batch.TotalCredit,
            Difference = batch.Difference,
            Errors = errors,
            Warnings = warnings
        };
    }

    public async Task<OpeningBalanceBatchDto> SubmitForApprovalAsync(
        Guid batchId,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var validation = await ValidateBatchAsync(batchId, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Opening balance batch failed validation: {string.Join("; ", validation.Errors)}");
        }

        var batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
        if (string.Equals(batch.Status, StatusPosted, StringComparison.OrdinalIgnoreCase))
        {
            return await MapBatchAsync(batch.Id, cancellationToken)
                ?? throw new InvalidOperationException("Opening balance batch was not found.");
        }

        if (string.Equals(batch.Status, StatusRejected, StringComparison.OrdinalIgnoreCase))
        {
            await RecordAuditAsync(
                FinanceAuditEvents.FinancePostingBlockedAfterRejection,
                batch,
                reason: "Rejected opening-balance batches cannot be posted.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException("Rejected opening-balance batches cannot be submitted or posted.");
        }

        var now = DateTime.UtcNow;
        if (_workflowService == null)
        {
            batch.Status = StatusApproved;
            batch.ApprovedAt = now;
        }
        else
        {
            batch.Status = StatusPendingApproval;
            batch.SubmittedAt = now;
            var workflowResult = await _workflowService.StartApprovalWorkflowAsync(EntityType, batch.Id);
            if (!workflowResult.Success)
            {
                batch.Status = StatusFailed;
                batch.FailedAt = now;
                batch.FailureReason = workflowResult.Message ?? "Opening balance approval workflow could not be started.";
                await _db.SaveChangesAsync(cancellationToken);
                await RecordAuditAsync(
                    FinanceAuditEvents.FinanceWorkflowApprovalFailed,
                    batch,
                    afterValues: new { workflowResult.Status, workflowResult.Message },
                    reason: batch.FailureReason,
                    cancellationToken: cancellationToken);
                throw new InvalidOperationException(batch.FailureReason);
            }

            batch.WorkflowInstanceId = workflowResult.WorkflowInstanceId;
            if (workflowResult.Status == ErpSystem.Core.Enums.WorkflowInstanceStatus.Completed)
            {
                batch.Status = StatusApproved;
                batch.ApprovedAt = now;
            }
        }

        batch.UpdatedAt = now;
        batch.UpdatedBy = _currentUser.UserName ?? "system";
        batch.LastModifiedById = CurrentUserId();
        await _db.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(
            FinanceAuditEvents.FinanceWorkflowSubmitted,
            batch,
            afterValues: new { batch.Status, batch.WorkflowInstanceId },
            comment: comment,
            cancellationToken: cancellationToken);

        return await MapBatchAsync(batch.Id, cancellationToken)
            ?? throw new InvalidOperationException("Opening balance batch was not found.");
    }

    public async Task<OpeningBalanceBatchDto> PostAsync(
        Guid batchId,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
        if (string.Equals(batch.Status, StatusPosted, StringComparison.OrdinalIgnoreCase) &&
            batch.JournalEntryId.HasValue &&
            batch.PostingEventId.HasValue)
        {
            return await MapBatchAsync(batch.Id, cancellationToken)
                ?? throw new InvalidOperationException("Opening balance batch was not found.");
        }

        if (string.Equals(batch.Status, StatusRejected, StringComparison.OrdinalIgnoreCase))
        {
            await RecordAuditAsync(
                FinanceAuditEvents.FinancePostingBlockedAfterRejection,
                batch,
                reason: "Rejected opening-balance batches cannot be posted.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException("Rejected opening-balance batches cannot be posted.");
        }

        if (!string.Equals(batch.Status, StatusApproved, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(batch.Status, StatusPostingFailed, StringComparison.OrdinalIgnoreCase))
        {
            await RecordAuditAsync(
                FinanceAuditEvents.FinancePostingBlockedPendingApproval,
                batch,
                afterValues: new { batch.Status },
                reason: "Opening balance batch is not approved.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException("Opening balance batch must be approved before posting.");
        }

        var validation = await ValidateBatchAsync(batchId, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Opening balance batch failed validation: {string.Join("; ", validation.Errors)}");
        }

        batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
        var request = await BuildPostingRequestAsync(batch, cancellationToken);
        try
        {
            var result = await _postingEngine.PostAsync(request, cancellationToken);
            // The posting engine may clear the tracker while recovering an idempotent/concurrent
            // insert race. Rehydrate before saving batch/register back-links; otherwise GL could
            // be correct while the migration workspace still appears unposted to another user.
            if (_db.Entry(batch).State == EntityState.Detached)
            {
                batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
            }
            batch.Status = StatusPosted;
            batch.JournalEntryId = result.JournalEntryId;
            batch.PostingEventId = result.PostingEventId;
            batch.PostedAt = DateTime.UtcNow;
            batch.FailureReason = null;
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = _currentUser.UserName ?? "system";
            batch.LastModifiedById = CurrentUserId();

            // The existing asset import establishes book-level source facts but deliberately does
            // not claim they reached GL. Only after the approved central posting succeeds do we
            // attach its immutable journal/event evidence to those imported book values.
            await ApplyFixedAssetOpeningLinksAsync(
                batch,
                result.JournalEntryId,
                result.PostingEventId,
                cancellationToken);
            await ApplySpecializedOpeningLinksAsync(batch, result.JournalEntryId, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            await RecordAuditAsync(
                FinanceAuditEvents.OpeningBalancePosted,
                batch,
                afterValues: new
                {
                    result.JournalEntryId,
                    result.PostingEventId,
                    result.WasDuplicate,
                    result.TotalDebitAmount,
                    result.TotalCreditAmount
                },
                comment: comment,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // Only a failure after a recorded approval may be retried. A failed workflow start
            // remains StatusFailed and can never be interpreted as permission to post.
            batch.Status = StatusPostingFailed;
            batch.FailedAt = DateTime.UtcNow;
            batch.FailureReason = ex.Message;
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = _currentUser.UserName ?? "system";
            batch.LastModifiedById = CurrentUserId();
            await _db.SaveChangesAsync(cancellationToken);

            await RecordAuditAsync(
                FinanceAuditEvents.OpeningBalancePostingFailed,
                batch,
                reason: ex.Message,
                cancellationToken: cancellationToken);
            throw;
        }

        return await MapBatchAsync(batch.Id, cancellationToken)
            ?? throw new InvalidOperationException("Opening balance batch was posted but could not be reloaded.");
    }

    public async Task<IReadOnlyList<OpeningBalanceDiagnosticDto>> GetDiagnosticsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var diagnostics = new List<OpeningBalanceDiagnosticDto>();
        var batches = await _db.OpeningBalanceBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && !b.IsDeleted)
            .ToListAsync(cancellationToken);

        diagnostics.AddRange(batches
            .Where(b => string.Equals(b.Status, StatusDraft, StringComparison.OrdinalIgnoreCase)
                || string.Equals(b.Status, StatusValidated, StringComparison.OrdinalIgnoreCase)
                || string.Equals(b.Status, StatusPendingApproval, StringComparison.OrdinalIgnoreCase))
            .Select(b => new OpeningBalanceDiagnosticDto
            {
                DiagnosticCode = "UNPOSTED_OPENING_BALANCE_BATCH",
                Severity = "Warning",
                BatchId = b.Id,
                Reference = b.BatchNumber,
                Message = $"Opening balance batch {b.BatchNumber} is {b.Status} and has not posted to GL."
            }));

        diagnostics.AddRange(batches
            .Where(b => string.Equals(b.Status, StatusFailed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(b.Status, StatusPostingFailed, StringComparison.OrdinalIgnoreCase))
            .Select(b => new OpeningBalanceDiagnosticDto
            {
                DiagnosticCode = "FAILED_OPENING_BALANCE_BATCH",
                Severity = "Error",
                BatchId = b.Id,
                Reference = b.BatchNumber,
                Message = $"Opening balance batch {b.BatchNumber} failed: {b.FailureReason}"
            }));

        diagnostics.AddRange(batches
            .Where(b => string.Equals(b.Status, StatusPosted, StringComparison.OrdinalIgnoreCase)
                && (!b.JournalEntryId.HasValue || !b.PostingEventId.HasValue))
            .Select(b => new OpeningBalanceDiagnosticDto
            {
                DiagnosticCode = "POSTED_OPENING_BALANCE_MISSING_REFERENCES",
                Severity = "Error",
                BatchId = b.Id,
                Reference = b.BatchNumber,
                Message = $"Opening balance batch {b.BatchNumber} is posted but is missing journal or posting-event references."
            }));

        var duplicateSourcePostings = await _db.FinancePostingEvents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                e.SourceModule == SourceModule &&
                e.SourceDocumentType == EntityType)
            .GroupBy(e => e.SourceDocumentId)
            .Where(g => g.Count() > 1)
            .Select(g => new { BatchId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        diagnostics.AddRange(duplicateSourcePostings.Select(item => new OpeningBalanceDiagnosticDto
        {
            DiagnosticCode = "DUPLICATE_OPENING_BALANCE_POSTING",
            Severity = "Error",
            BatchId = item.BatchId,
            Message = $"Opening balance batch {item.BatchId} has {item.Count} posting events."
        }));

        if (_financeAuditService != null)
        {
            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = FinanceAuditEvents.MigrationDiagnosticRun,
                TenantId = tenantId,
                SourceModule = SourceModule,
                SourceDocumentType = "OpeningBalanceDiagnostics",
                AfterValues = new { diagnostics.Count },
                Resource = "Finance.OpeningBalanceDiagnostics",
                ResourceId = tenantId.ToString()
            }, cancellationToken);
        }

        return diagnostics;
    }

    private async Task<FinancePostingRequestDto> BuildPostingRequestAsync(
        OpeningBalanceBatch batch,
        CancellationToken cancellationToken)
    {
        var functionalCurrency = await GetFunctionalCurrencyAsync(batch.TenantId, cancellationToken);
        return new FinancePostingRequestDto
        {
            SourceModule = SourceModule,
            SourceDocumentType = EntityType,
            SourceDocumentId = batch.Id,
            SourceDocumentTenantId = batch.TenantId,
            PostingAction = PostingAction,
            SourceDocumentReference = batch.BatchNumber,
            Description = string.IsNullOrWhiteSpace(batch.Description)
                ? $"Opening balance batch {batch.BatchNumber}"
                : batch.Description,
            PostingDate = batch.OpeningDate,
            FiscalPeriodId = batch.FiscalPeriodId,
            JournalType = "Opening Balance",
            BookClassification = batch.BookClassification,
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = batch.IdempotencyKey,
            ReturnExistingOnDuplicate = true,
            Lines = batch.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new FinancePostingLineDto
                {
                    AccountId = l.AccountId,
                    Description = $"Opening balance {batch.BatchNumber}",
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount,
                    TransactionCurrency = l.TransactionCurrencyCode,
                    // Functional GL values and native cutover quantities are deliberately
                    // separate. A USD advance may carry USD 100 at GHS 15 without recording
                    // the GHS 1,500 functional value as if it were USD 1,500.
                    TransactionDebitAmount = l.TransactionDebitAmount ?? l.DebitAmount,
                    TransactionCreditAmount = l.TransactionCreditAmount ?? l.CreditAmount,
                    // FinancePostingEngine keeps this compatibility field as the explicit
                    // original-foreign-amount gate, even when debit/credit native amounts are
                    // already supplied. Populate it only for the controlled foreign source line.
                    ForeignCurrencyAmount = string.Equals(l.TransactionCurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : l.TransactionDebitAmount.GetValueOrDefault() > 0m
                            ? l.TransactionDebitAmount
                            : l.TransactionCreditAmount,
                    ExchangeRateId = l.ExchangeRateId,
                    ExchangeRateDate = l.ExchangeRateDate,
                    SourceReferenceNumber = l.SourceReference ?? batch.SourceReference ?? batch.BatchNumber,
                    LineNumber = l.LineNumber,
                    SegmentString = l.SegmentString,
                    Notes = l.Notes,
                    TransactionTag = "OpeningBalance"
                })
                .ToList()
        };
    }

    private async Task<OpeningBalanceBatch> LoadBatchAsync(
        Guid tenantId,
        Guid batchId,
        CancellationToken cancellationToken)
        => await _db.OpeningBalanceBatches
            .Include(b => b.FiscalPeriod)
            .Include(b => b.Lines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == batchId && !b.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Opening balance batch was not found for the current tenant.");

    private async Task<IReadOnlyList<FixedAssetOpeningBalanceCandidateDto>> GetFixedAssetOpeningCandidatesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
        => await _db.FixedAssetBookValues
            .AsNoTracking()
            .Where(value =>
                value.TenantId == tenantId &&
                !value.IsDeleted &&
                !value.FixedAsset.IsDeleted &&
                (value.OpeningAsOfDate.HasValue || value.OpeningSource.Contains("Opening")))
            .OrderBy(value => value.FixedAsset.AssetCode)
            .ThenBy(value => value.BookClassification)
            .Select(value => new FixedAssetOpeningBalanceCandidateDto
            {
                FixedAssetId = value.FixedAssetId,
                FixedAssetBookValueId = value.Id,
                AssetCode = value.FixedAsset.AssetCode,
                AssetName = value.FixedAsset.Name,
                CategoryCode = value.FixedAsset.Category.Code,
                BookClassification = value.BookClassification,
                OpeningAsOfDate = value.OpeningAsOfDate,
                AcquisitionCost = value.AcquisitionCost,
                AccumulatedDepreciation = value.AccumulatedDepreciation,
                NetBookValue = value.NetBookValue,
                OpeningPostedToGl = value.OpeningPostedToGl,
                OpeningJournalEntryId = value.OpeningJournalEntryId
            })
            .ToListAsync(cancellationToken);

    private async Task ValidateFixedAssetOpeningEvidenceAsync(
        OpeningBalanceBatch batch,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var evidenceLines = batch.Lines
            .Where(IsFixedAssetOpeningLine)
            .ToList();
        if (evidenceLines.Count == 0)
        {
            return;
        }

        var valueIds = evidenceLines
            .Where(line => line.CounterpartyId.HasValue)
            .Select(line => line.CounterpartyId!.Value)
            .Distinct()
            .ToArray();
        var values = await _db.FixedAssetBookValues
            .AsNoTracking()
            .Include(value => value.FixedAsset)
                .ThenInclude(asset => asset.Category)
            .Where(value =>
                value.TenantId == batch.TenantId &&
                valueIds.Contains(value.Id) &&
                !value.IsDeleted &&
                !value.FixedAsset.IsDeleted)
            .ToDictionaryAsync(value => value.Id, cancellationToken);

        foreach (var valueId in valueIds)
        {
            if (!values.TryGetValue(valueId, out var value))
            {
                errors.Add($"Fixed-asset opening evidence {valueId} is missing or belongs to another tenant.");
                continue;
            }

            try
            {
                ValidateFixedAssetOpeningCandidate(value, batch.OpeningDate.Date);
            }
            catch (InvalidOperationException ex)
            {
                errors.Add(ex.Message);
                continue;
            }

            var costLine = evidenceLines.SingleOrDefault(line =>
                line.CounterpartyId == valueId &&
                string.Equals(line.CounterpartyType, FixedAssetOpeningCost, StringComparison.Ordinal));
            if (costLine == null ||
                costLine.AccountId != value.FixedAsset.Category.AssetAccountId ||
                costLine.DebitAmount != RoundMoney(value.AcquisitionCost) ||
                costLine.CreditAmount != 0m)
            {
                errors.Add($"Fixed asset {value.FixedAsset.AssetCode}: opening cost or category account changed after batch preparation.");
            }

            var depreciationLine = evidenceLines.SingleOrDefault(line =>
                line.CounterpartyId == valueId &&
                string.Equals(line.CounterpartyType, FixedAssetOpeningDepreciation, StringComparison.Ordinal));
            var expectedDepreciation = RoundMoney(value.AccumulatedDepreciation);
            if ((expectedDepreciation == 0m && depreciationLine != null) ||
                (expectedDepreciation > 0m &&
                 (depreciationLine == null ||
                  depreciationLine.AccountId != value.FixedAsset.Category.AccumulatedDepreciationAccountId ||
                  depreciationLine.CreditAmount != expectedDepreciation ||
                  depreciationLine.DebitAmount != 0m)))
            {
                errors.Add($"Fixed asset {value.FixedAsset.AssetCode}: opening depreciation or category account changed after batch preparation.");
            }
        }
    }

    private async Task ApplyFixedAssetOpeningLinksAsync(
        OpeningBalanceBatch batch,
        Guid journalEntryId,
        Guid postingEventId,
        CancellationToken cancellationToken)
    {
        var valueIds = batch.Lines
            .Where(IsFixedAssetOpeningLine)
            .Where(line => line.CounterpartyId.HasValue)
            .Select(line => line.CounterpartyId!.Value)
            .Distinct()
            .ToArray();
        if (valueIds.Length == 0)
        {
            return;
        }

        var values = await _db.FixedAssetBookValues
            .Include(value => value.FixedAsset)
            .Where(value => value.TenantId == batch.TenantId && valueIds.Contains(value.Id))
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var value in values)
        {
            value.OpeningJournalEntryId = journalEntryId;
            value.OpeningPostedToGl = true;
            value.OpeningPostedDate = now;
            value.SourceDocumentType = EntityType;
            value.SourceDocumentId = batch.Id;
            value.UpdatedAt = now;
            value.UpdatedBy = _currentUser.UserName ?? "system";
            value.LastModifiedById = CurrentUserId();

            // The fixed-asset summary carries the primary IFRS opening lineage used by register
            // inquiries. Parallel books retain their own journal link on FixedAssetBookValue and
            // therefore never overwrite the primary-book evidence.
            if (string.Equals(value.BookClassification, "IFRS", StringComparison.OrdinalIgnoreCase))
            {
                value.FixedAsset.JournalEntryId = journalEntryId;
                value.FixedAsset.PostingEventId = postingEventId;
                value.FixedAsset.SourceDocumentType = EntityType;
                value.FixedAsset.SourceDocumentId = batch.Id;
                value.FixedAsset.CapitalizationDate ??= batch.OpeningDate.Date;
                value.FixedAsset.CapitalizedAt ??= now;
                value.FixedAsset.UpdatedAt = now;
                value.FixedAsset.UpdatedBy = _currentUser.UserName ?? "system";
                value.FixedAsset.LastModifiedById = CurrentUserId();
            }
        }

        // The importer already created immutable opening acquisition/depreciation transactions.
        // Add the journal link to those exact rows instead of creating duplicate register events.
        var assetIds = values.Select(value => value.FixedAssetId).Distinct().ToArray();
        var openingTransactions = await _db.AssetTransactions
            .Where(transaction =>
                transaction.TenantId == batch.TenantId &&
                assetIds.Contains(transaction.FixedAssetId) &&
                transaction.BookClassification == batch.BookClassification &&
                (transaction.TransactionType == "Opening Acquisition" ||
                 transaction.TransactionType == "Opening Accumulated Depreciation"))
            .ToListAsync(cancellationToken);
        foreach (var transaction in openingTransactions)
        {
            transaction.RelatedEntityId = journalEntryId;
            transaction.UpdatedAt = now;
            transaction.UpdatedBy = _currentUser.UserName ?? "system";
            transaction.LastModifiedById = CurrentUserId();
        }
    }

    private static void ValidateFixedAssetOpeningCandidate(FixedAssetBookValue value, DateTime openingDate)
    {
        if (value.OpeningPostedToGl || value.OpeningJournalEntryId.HasValue)
            throw new InvalidOperationException($"Fixed asset {value.FixedAsset.AssetCode}: opening balance is already posted to GL.");
        if (!value.OpeningAsOfDate.HasValue || value.OpeningAsOfDate.Value.Date != openingDate)
            throw new InvalidOperationException($"Fixed asset {value.FixedAsset.AssetCode}: opening as-of date must equal {openingDate:yyyy-MM-dd}.");
        if (RoundMoney(value.AcquisitionCost) <= 0m)
            throw new InvalidOperationException($"Fixed asset {value.FixedAsset.AssetCode}: opening acquisition cost must be positive.");
        if (RoundMoney(value.AccumulatedDepreciation) < 0m || value.AccumulatedDepreciation > value.AcquisitionCost)
            throw new InvalidOperationException($"Fixed asset {value.FixedAsset.AssetCode}: opening accumulated depreciation is outside the valid cost range.");
        if (RoundMoney(value.AcquisitionCost - value.AccumulatedDepreciation) != RoundMoney(value.NetBookValue))
            throw new InvalidOperationException($"Fixed asset {value.FixedAsset.AssetCode}: opening cost, accumulated depreciation, and NBV do not reconcile.");
        if (value.FixedAsset.Category.AssetAccountId == Guid.Empty ||
            value.FixedAsset.Category.AccumulatedDepreciationAccountId == Guid.Empty)
            throw new InvalidOperationException($"Fixed asset {value.FixedAsset.AssetCode}: category opening-balance accounts are incomplete.");
    }

    private static bool HasFixedAssetOpeningEvidence(OpeningBalanceBatch batch)
        => batch.Lines.Any(IsFixedAssetOpeningLine);

    private async Task ValidateSpecializedOpeningEvidenceAsync(
        OpeningBalanceBatch batch,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var line = batch.Lines.SingleOrDefault(IsSpecializedOpeningLine);
        if (line?.CounterpartyId == null)
            return;

        if (line.CounterpartyType is SupplierAdvanceOpening or ApWithholdingOpening)
        {
            var payment = await _db.Set<VendorPayment>().AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == batch.TenantId && item.Id == line.CounterpartyId && !item.IsDeleted,
                cancellationToken);
            if (payment == null || payment.OpeningBalanceBatchId != batch.Id || payment.JournalEntryId.HasValue ||
                !string.Equals(payment.OpeningBalanceType, line.CounterpartyType, StringComparison.Ordinal))
            {
                errors.Add("AP specialised opening source evidence is missing, changed, or already posted.");
                return;
            }
            var expected = line.CounterpartyType == SupplierAdvanceOpening
                ? RoundMoney(payment.TotalAmount * payment.ExchangeRate)
                : RoundMoney(payment.WithholdingTaxAmount);
            if ((line.CounterpartyType == SupplierAdvanceOpening && line.DebitAmount != expected) ||
                (line.CounterpartyType == ApWithholdingOpening && line.CreditAmount != expected))
                errors.Add("AP specialised opening amount changed after batch preparation.");
            if (line.CounterpartyType == SupplierAdvanceOpening)
            {
                await ValidateAdvanceFxEvidenceAsync(
                    batch, line, payment.CurrencyCode, payment.TotalAmount, payment.ExchangeRate,
                    payment.ExchangeRateId, sourceUsesDebit: true, errors, cancellationToken);
            }
        }
        else
        {
            var payment = await _db.Set<CustomerPayment>().AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == batch.TenantId && item.Id == line.CounterpartyId && !item.IsDeleted,
                cancellationToken);
            if (payment == null || payment.OpeningBalanceBatchId != batch.Id || payment.JournalEntryId.HasValue ||
                !string.Equals(payment.OpeningBalanceType, line.CounterpartyType, StringComparison.Ordinal))
            {
                errors.Add("AR specialised opening source evidence is missing, changed, or already posted.");
                return;
            }
            var expected = line.CounterpartyType == CustomerAdvanceOpening
                ? RoundMoney(payment.TotalAmount * payment.ExchangeRate)
                : RoundMoney(payment.WithholdingTaxAmount);
            if ((line.CounterpartyType == CustomerAdvanceOpening && line.CreditAmount != expected) ||
                (line.CounterpartyType == ArWithholdingOpening && line.DebitAmount != expected))
                errors.Add("AR specialised opening amount changed after batch preparation.");
            if (line.CounterpartyType == CustomerAdvanceOpening)
            {
                await ValidateAdvanceFxEvidenceAsync(
                    batch, line, payment.CurrencyCode, payment.TotalAmount, payment.ExchangeRate,
                    payment.ExchangeRateId, sourceUsesDebit: false, errors, cancellationToken);
            }
        }
    }

    private async Task ValidateAdvanceFxEvidenceAsync(
        OpeningBalanceBatch batch,
        OpeningBalanceLine line,
        string sourceCurrency,
        decimal nativeAmount,
        decimal sourceRate,
        Guid? sourceRateId,
        bool sourceUsesDebit,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var functionalCurrency = NormalizeCurrency(line.FunctionalCurrencyCode, "GHS");
        var currency = NormalizeCurrency(sourceCurrency, functionalCurrency);
        var expectedNative = RoundMoney(nativeAmount);
        var lineNative = sourceUsesDebit ? line.TransactionDebitAmount : line.TransactionCreditAmount;
        if (!string.Equals(line.TransactionCurrencyCode, currency, StringComparison.OrdinalIgnoreCase) ||
            lineNative != expectedNative)
        {
            errors.Add("Advance native-currency evidence changed after batch preparation.");
        }

        if (string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            if (RoundRate(sourceRate) != 1m)
                errors.Add("Functional-currency opening advance must retain an exchange rate of 1.");
            return;
        }

        if (!sourceRateId.HasValue || line.ExchangeRateId != sourceRateId)
        {
            errors.Add("Foreign-currency opening advance lost its approved exchange-rate linkage.");
            return;
        }

        // Approval can be withdrawn or the master record can be corrected between preparation
        // and posting. Re-check the frozen rate evidence at the maker-checker boundary.
        var approvedRate = await _db.ExchangeRates.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == batch.TenantId && item.Id == sourceRateId.Value && !item.IsDeleted,
            cancellationToken);
        if (approvedRate == null || approvedRate.ApprovalStatus != RateApprovalStatus.Approved ||
            !string.Equals(approvedRate.BaseCurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(approvedRate.TargetCurrencyCode, currency, StringComparison.OrdinalIgnoreCase) ||
            RoundRate(approvedRate.Rate) != RoundRate(sourceRate))
        {
            errors.Add("Foreign-currency opening advance rate is missing, unapproved, or no longer matches its source evidence.");
        }
    }

    private async Task ApplySpecializedOpeningLinksAsync(
        OpeningBalanceBatch batch,
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        var line = batch.Lines.SingleOrDefault(IsSpecializedOpeningLine);
        if (line?.CounterpartyId == null)
            return;
        var now = DateTime.UtcNow;
        if (line.CounterpartyType is SupplierAdvanceOpening or ApWithholdingOpening)
        {
            var payment = await _db.Set<VendorPayment>().FirstAsync(item =>
                item.TenantId == batch.TenantId && item.Id == line.CounterpartyId, cancellationToken);
            payment.JournalEntryId = journalEntryId;
            payment.Status = VendorPaymentStatus.Cleared;
            payment.UpdatedAt = now;
            payment.UpdatedBy = _currentUser.UserName ?? "system";
            payment.LastModifiedById = CurrentUserId();
        }
        else
        {
            var payment = await _db.Set<CustomerPayment>().FirstAsync(item =>
                item.TenantId == batch.TenantId && item.Id == line.CounterpartyId, cancellationToken);
            payment.JournalEntryId = journalEntryId;
            payment.Status = "Cleared";
            payment.ClearedDate = batch.OpeningDate.Date;
            payment.UpdatedAt = now;
            payment.UpdatedBy = _currentUser.UserName ?? "system";
            payment.LastModifiedById = CurrentUserId();
        }
    }

    private static bool HasGeneratedSubledgerOpeningEvidence(OpeningBalanceBatch batch)
        => HasFixedAssetOpeningEvidence(batch) || batch.Lines.Any(IsSpecializedOpeningLine);

    private static bool IsSpecializedOpeningLine(OpeningBalanceLine line)
        => line.CounterpartyType is SupplierAdvanceOpening or CustomerAdvanceOpening or ApWithholdingOpening or ArWithholdingOpening;

    private static bool IsFixedAssetOpeningLine(OpeningBalanceLine line)
        => string.Equals(line.CounterpartyType, FixedAssetOpeningCost, StringComparison.Ordinal)
            || string.Equals(line.CounterpartyType, FixedAssetOpeningDepreciation, StringComparison.Ordinal);

    private async Task<OpeningBalanceBatchDto?> MapBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var batch = await _db.OpeningBalanceBatches
            .AsNoTracking()
            .Include(b => b.FiscalPeriod)
            .Include(b => b.Lines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == batchId && !b.IsDeleted, cancellationToken);
        if (batch == null)
        {
            return null;
        }

        return new OpeningBalanceBatchDto
        {
            Id = batch.Id,
            TenantId = batch.TenantId,
            BatchNumber = batch.BatchNumber,
            SourceReference = batch.SourceReference,
            Description = batch.Description,
            OpeningDate = batch.OpeningDate,
            FiscalPeriodId = batch.FiscalPeriodId,
            FiscalPeriodCode = batch.FiscalPeriod?.PeriodCode ?? string.Empty,
            BookClassification = batch.BookClassification,
            Status = batch.Status,
            IdempotencyKey = batch.IdempotencyKey,
            TotalDebit = batch.TotalDebit,
            TotalCredit = batch.TotalCredit,
            Difference = batch.Difference,
            JournalEntryId = batch.JournalEntryId,
            PostingEventId = batch.PostingEventId,
            WorkflowInstanceId = batch.WorkflowInstanceId,
            ValidatedAt = batch.ValidatedAt,
            SubmittedAt = batch.SubmittedAt,
            ApprovedAt = batch.ApprovedAt,
            PostedAt = batch.PostedAt,
            FailureReason = batch.FailureReason,
            CreatedAt = batch.CreatedAt,
            UpdatedAt = batch.UpdatedAt,
            Lines = batch.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new OpeningBalanceLineDto
                {
                    Id = l.Id,
                    LineNumber = l.LineNumber,
                    AccountId = l.AccountId,
                    AccountCode = l.Account?.AccountCode ?? string.Empty,
                    AccountName = l.Account?.AccountName ?? string.Empty,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount,
                    TransactionDebitAmount = l.TransactionDebitAmount,
                    TransactionCreditAmount = l.TransactionCreditAmount,
                    TransactionCurrencyCode = l.TransactionCurrencyCode,
                    FunctionalCurrencyCode = l.FunctionalCurrencyCode,
                    ExchangeRateId = l.ExchangeRateId,
                    ExchangeRateDate = l.ExchangeRateDate,
                    SegmentString = l.SegmentString,
                    BankAccountId = l.BankAccountId,
                    CounterpartyType = l.CounterpartyType,
                    CounterpartyId = l.CounterpartyId,
                    SourceReference = l.SourceReference,
                    Notes = l.Notes
                })
                .ToList()
        };
    }

    private async Task<string> GetFunctionalCurrencyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var settings = await _db.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken);
        return NormalizeCurrency(settings?.BaseCurrency, "GHS");
    }

    private async Task RecordAuditAsync(
        string eventType,
        OpeningBalanceBatch batch,
        object? beforeValues = null,
        object? afterValues = null,
        string? reason = null,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = batch.TenantId,
            SourceModule = SourceModule,
            SourceDocumentType = EntityType,
            SourceDocumentId = batch.Id,
            JournalEntryId = batch.JournalEntryId,
            PostingEventId = batch.PostingEventId,
            WorkflowInstanceId = batch.WorkflowInstanceId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Reason = reason,
            Comment = comment,
            Resource = "Finance.OpeningBalanceBatch",
            ResourceId = batch.Id.ToString()
        }, cancellationToken);
    }

    private static void RecalculateTotals(OpeningBalanceBatch batch)
    {
        batch.TotalDebit = RoundMoney(batch.Lines.Sum(l => l.DebitAmount));
        batch.TotalCredit = RoundMoney(batch.Lines.Sum(l => l.CreditAmount));
        batch.Difference = RoundMoney(batch.TotalDebit - batch.TotalCredit);
    }

    private static string NormalizeBook(string? value)
        => string.IsNullOrWhiteSpace(value) ? "IFRS" : value.Trim();

    private static bool IsAllActiveBooks(string? value)
        => string.Equals(NormalizeBook(value), "ALL_ACTIVE_BOOKS", StringComparison.OrdinalIgnoreCase);

    private static bool IsEditableStatus(string? status)
        => string.Equals(status, StatusDraft, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, StatusValidated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, StatusFailed, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCurrency(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback.ToUpperInvariant() : value.Trim().ToUpperInvariant();

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private Guid? CurrentUserId()
        => Guid.TryParse(_currentUser.UserId, out var userId) ? userId : null;
}
