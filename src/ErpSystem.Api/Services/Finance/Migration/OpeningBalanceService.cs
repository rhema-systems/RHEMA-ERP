using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Shared;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Security.Cryptography;
using System.Text;

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
    private const string StatusReversed = "Reversed";
    private const string StatusFailed = "Failed";
    private const string StatusPostingFailed = "PostingFailed";
    private const string FixedAssetOpeningCost = "FixedAssetOpeningCost";
    private const string FixedAssetOpeningDepreciation = "FixedAssetOpeningDep";
    private const string SupplierAdvanceOpening = "SupplierAdvanceOpening";
    private const string CustomerAdvanceOpening = "CustomerAdvanceOpening";
    private const string ApWithholdingOpening = "ApWhtOpening";
    private const string ArWithholdingOpening = "ArWhtOpening";
    private const string BankAccountOpening = "BankAccountOpening";
    private const string BankOpeningClearing = "BankOpeningClearing";
    private const string ResidualAccruedOpening = "ResidualAccruedOpening";
    private const string ResidualShareCapitalOpening = "ResidualShareCapitalOpening";
    private const string ResidualRetainedEarnings = "ResidualRetainedEarnings";
    private const string ResidualMigrationClearing = "ResidualMigrationClearing";
    private const string SourceKindFreeForm = "FreeForm";
    private const string SourceKindBankAccountOpening = "BankAccountOpening";
    private const string SourceKindResidualGlEquityOpening = "ResidualGlEquityOpening";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinancePostingEngine _postingEngine;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IWorkflowService? _workflowService;
    private readonly ILogger<OpeningBalanceService>? _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFinanceReversalPolicyService? _reversalPolicyService;

    public OpeningBalanceService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinancePostingEngine postingEngine,
        IFinanceAuditService? financeAuditService = null,
        IWorkflowService? workflowService = null,
        ILogger<OpeningBalanceService>? logger = null,
        IUnitOfWork? unitOfWork = null,
        IFinanceReversalPolicyService? reversalPolicyService = null)
    {
        _db = db;
        _currentUser = currentUser;
        _postingEngine = postingEngine;
        _financeAuditService = financeAuditService;
        _workflowService = workflowService;
        _logger = logger;
        _unitOfWork = unitOfWork ?? new UnitOfWork(db);
        _reversalPolicyService = reversalPolicyService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public Task<OpeningBalanceBatchDto> CreateBatchAsync(
        CreateOpeningBalanceBatchDto dto,
        CancellationToken cancellationToken = default)
        => CreateBatchCoreAsync(dto, isServerDerived: false, cancellationToken);

    private async Task<OpeningBalanceBatchDto> CreateBatchCoreAsync(
        CreateOpeningBalanceBatchDto dto,
        bool isServerDerived,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (dto.Lines.Count == 0)
        {
            throw new InvalidOperationException("Opening balance batch requires at least one line.");
        }

        if (!isServerDerived)
        {
            // The public DTO is a free-form GL cutover surface. AP/AR, cash, tax and fixed-asset
            // balances need their canonical subledger/register evidence, so accepting those
            // accounts here would create a balanced GL that cannot reconcile to its owner.
            await EnsureFreeFormOpeningLinesAllowedAsync(tenantId, dto.Lines, cancellationToken);
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

        return await CreateBatchCoreAsync(new CreateOpeningBalanceBatchDto
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
        }, isServerDerived: true, cancellationToken);
    }

    public async Task<OpeningBalanceBatchDto> CreateBankAccountOpeningBatchAsync(
        CreateBankAccountOpeningBalanceDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.BankAccountId == Guid.Empty)
            throw new InvalidOperationException("Select a Finance bank account for the governed opening.");
        var amount = RequirePositiveOpeningAmount(dto.Amount, "Bank opening amount");
        var tenantId = TenantId;
        var defaultKey = $"MIGRATION:BankAccountOpening:{tenantId:N}:{dto.BankAccountId:N}";
        var requestedKey = NormalizeOptional(dto.IdempotencyKey) ?? defaultKey;
        return await ExecuteGovernedCreationAsync(
            tenantId,
            $"Bank:{dto.BankAccountId:N}",
            requestedKey,
            async token =>
            {
                var header = await ResolveGovernedHeaderAsync(
                    dto.BatchNumber,
                    dto.SourceReference,
                    dto.Description,
                    dto.OpeningDate,
                    dto.FiscalPeriodId,
                    dto.BookClassification,
                    dto.IdempotencyKey,
                    defaultKey,
                    token);

                var idempotency = await ResolveGovernedIdempotencyAsync(header.IdempotencyKey, token);
                if (idempotency.Existing != null)
                {
                    EnsureBankOpeningRetryMatches(idempotency.Existing, dto.BankAccountId, amount, dto.ExchangeRateId, header);
                    return await MapBatchAsync(idempotency.Existing.Id, token)
                        ?? throw new InvalidOperationException("The existing bank opening batch could not be reloaded.");
                }
                header = header with { IdempotencyKey = idempotency.EffectiveKey };

                var context = await ResolveBankOpeningContextAsync(
                    tenantId,
                    dto.BankAccountId,
                    header.OpeningDate,
                    dto.ExchangeRateId,
                    allowDerivedRate: false,
                    excludeBatchId: null,
                    requireUnusedState: true,
                    cancellationToken: token);

                var nativeAmount = amount;
                var functionalAmount = RoundMoney(nativeAmount * context.ExchangeRate.Rate);

                return await CreateBatchCoreAsync(new CreateOpeningBalanceBatchDto
                {
                    BatchNumber = header.BatchNumber,
                    SourceReference = header.SourceReference,
                    Description = header.Description ?? $"Governed opening balance for {context.Bank.AccountNumber} {context.Bank.AccountName}",
                    OpeningDate = header.OpeningDate,
                    FiscalPeriodId = header.FiscalPeriodId,
                    BookClassification = header.BookClassification,
                    IdempotencyKey = header.IdempotencyKey,
                    Lines = new[]
                    {
                        new CreateOpeningBalanceLineDto
                        {
                            AccountId = context.BankGl.Id,
                            DebitAmount = functionalAmount,
                            TransactionDebitAmount = nativeAmount,
                            TransactionCreditAmount = 0m,
                            TransactionCurrencyCode = context.BankCurrency,
                            FunctionalCurrencyCode = header.FunctionalCurrency,
                            ExchangeRateId = context.ExchangeRate.ExchangeRateId,
                            ExchangeRateDate = context.ExchangeRate.EffectiveDate,
                            BankAccountId = context.Bank.Id,
                            CounterpartyType = BankAccountOpening,
                            CounterpartyId = context.Bank.Id,
                            SourceReference = header.SourceReference,
                            Notes = $"Governed bank opening for {context.Bank.AccountNumber}"
                        },
                        new CreateOpeningBalanceLineDto
                        {
                            AccountId = context.MigrationClearing.Id,
                            DebitAmount = 0m,
                            CreditAmount = functionalAmount,
                            TransactionDebitAmount = 0m,
                            TransactionCreditAmount = functionalAmount,
                            TransactionCurrencyCode = header.FunctionalCurrency,
                            FunctionalCurrencyCode = header.FunctionalCurrency,
                            BankAccountId = context.Bank.Id,
                            CounterpartyType = BankOpeningClearing,
                            CounterpartyId = context.Bank.Id,
                            SourceReference = header.SourceReference,
                            Notes = $"Migration clearing offset for bank {context.Bank.AccountNumber}"
                        }
                    }
                }, isServerDerived: true, token);
            },
            async token => await ReloadBankCreationCollisionAsync(dto, amount, defaultKey, token),
            cancellationToken);
    }

    public async Task<OpeningBalanceBatchDto> CreateResidualGlEquityOpeningBatchAsync(
        CreateResidualGlEquityOpeningBalanceDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.AccruedExpensesAccountId == Guid.Empty || dto.ShareCapitalAccountId == Guid.Empty)
            throw new InvalidOperationException("Select the accrued-expenses and share-capital accounts for the governed residual opening.");
        var accruedAmount = RequirePositiveOpeningAmount(dto.AccruedExpensesAmount, "Accrued-expenses opening amount");
        var shareCapitalAmount = RequirePositiveOpeningAmount(dto.ShareCapitalAmount, "Share-capital opening amount");
        var retainedEarningsAmount = RequirePositiveOpeningAmount(dto.RetainedEarningsAmount, "Retained-earnings opening amount");
        var tenantId = TenantId;
        var provisionalBook = NormalizeBook(dto.BookClassification);
        var defaultKey = $"MIGRATION:ResidualGlEquity:{tenantId:N}:{dto.OpeningDate:yyyyMMdd}:{provisionalBook}";
        var requestedKey = NormalizeOptional(dto.IdempotencyKey) ?? defaultKey;
        return await ExecuteGovernedCreationAsync(
            tenantId,
            $"Residual:{dto.OpeningDate:yyyyMMdd}:{provisionalBook}",
            requestedKey,
            async token =>
            {
                var header = await ResolveGovernedHeaderAsync(
                    dto.BatchNumber,
                    dto.SourceReference,
                    dto.Description,
                    dto.OpeningDate,
                    dto.FiscalPeriodId,
                    provisionalBook,
                    dto.IdempotencyKey,
                    defaultKey,
                    token);

                var idempotency = await ResolveGovernedIdempotencyAsync(header.IdempotencyKey, token);
                if (idempotency.Existing != null)
                {
                    EnsureResidualOpeningRetryMatches(
                        idempotency.Existing,
                        dto.AccruedExpensesAccountId,
                        accruedAmount,
                        dto.ShareCapitalAccountId,
                        shareCapitalAmount,
                        retainedEarningsAmount,
                        header);
                    return await MapBatchAsync(idempotency.Existing.Id, token)
                        ?? throw new InvalidOperationException("The existing residual opening batch could not be reloaded.");
                }
                header = header with { IdempotencyKey = idempotency.EffectiveKey };

                var context = await ResolveResidualOpeningContextAsync(
                    tenantId,
                    dto.AccruedExpensesAccountId,
                    dto.ShareCapitalAccountId,
                    header.OpeningDate,
                    header.FunctionalCurrency,
                    excludeBatchId: null,
                    bookClassification: header.BookClassification,
                    cancellationToken: token);
                var totalCredit = RoundMoney(accruedAmount + shareCapitalAmount + retainedEarningsAmount);
                await EnsureResidualClearsMigrationBalanceAsync(
                    tenantId,
                    context.MigrationClearing.Id,
                    header.OpeningDate,
                    header.BookClassification,
                    totalCredit,
                    excludeBatchId: null,
                    token);

                return await CreateBatchCoreAsync(new CreateOpeningBalanceBatchDto
                {
                    BatchNumber = header.BatchNumber,
                    SourceReference = header.SourceReference,
                    Description = header.Description ?? $"Governed residual GL and equity opening at {header.OpeningDate:yyyy-MM-dd}",
                    OpeningDate = header.OpeningDate,
                    FiscalPeriodId = header.FiscalPeriodId,
                    BookClassification = header.BookClassification,
                    IdempotencyKey = header.IdempotencyKey,
                    Lines = new[]
                    {
                        GovernedResidualLine(context.MigrationClearing.Id, totalCredit, 0m, ResidualMigrationClearing,
                            "Migration clearing debit for residual opening", header),
                        GovernedResidualLine(context.AccruedExpenses.Id, 0m, accruedAmount, ResidualAccruedOpening,
                            "Accrued expenses brought forward", header),
                        GovernedResidualLine(context.ShareCapital.Id, 0m, shareCapitalAmount, ResidualShareCapitalOpening,
                            "Share capital brought forward", header),
                        GovernedResidualLine(context.RetainedEarnings.Id, 0m, retainedEarningsAmount, ResidualRetainedEarnings,
                            "Retained earnings brought forward from approved cutover evidence", header)
                    }
                }, isServerDerived: true, token);
            },
            async token => await ReloadResidualCreationCollisionAsync(
                dto,
                accruedAmount,
                shareCapitalAmount,
                retainedEarningsAmount,
                defaultKey,
                token),
            cancellationToken);
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
        var supplier = await ResolveOpeningApPartnerAsync(
            dto.BusinessPartnerId, dto.BusinessPartnerRoleId, dto.OpeningDate, cancellationToken);
        var context = await ResolveSpecializedOpeningContextAsync(dto, cancellationToken);
        var advanceAccountId = context.Settings.SupplierAdvanceAccountId
            ?? throw new InvalidOperationException("Supplier Advance Account is not configured in Finance Settings.");
        var paymentId = Guid.NewGuid();
        var payment = new VendorPayment
        {
            Id = paymentId, TenantId = tenantId, BusinessPartnerId = supplier.Partner.Id,
            BusinessPartnerRoleId = supplier.Role.Id,
            BusinessPartnerApProfileVersionId = supplier.Profile.Id,
            BusinessPartnerCode = supplier.Partner.PartnerCode,
            BusinessPartnerName = supplier.Partner.PartnerName,
            BusinessPartnerLegalName = supplier.Partner.LegalName,
            BusinessPartnerTaxIdentificationNumber = supplier.Partner.TaxIdentificationNumber,
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
        var suppliers = await _db.BusinessPartnerRoles.AsNoTracking()
            .Where(role => role.TenantId == tenantId && !role.IsDeleted &&
                role.Status == BusinessPartnerRoleStatus.Active &&
                (role.RoleType == BusinessPartnerRoleType.Supplier || role.RoleType == BusinessPartnerRoleType.Contractor) &&
                role.BusinessPartner.IsActive && !role.BusinessPartner.IsDeleted)
            .OrderBy(role => role.BusinessPartner.PartnerName)
            .Select(role => new OpeningBalancePartyOptionDto
            {
                Id = role.BusinessPartnerId,
                Code = role.BusinessPartner.PartnerCode,
                Name = role.BusinessPartner.PartnerName
            })
            .Distinct()
            .ToListAsync(cancellationToken);
        // AR customer identity is owned by the canonical BusinessPartner master. The legacy
        // Customer entity is not a deployed table in current tenants and must never be queried
        // by a Finance cutover projection.
        var customers = await _db.BusinessPartners.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive &&
                item.Roles.Any(role => role.TenantId == tenantId && !role.IsDeleted &&
                    role.RoleType == BusinessPartnerRoleType.Customer))
            .OrderBy(item => item.PartnerName)
            .ThenBy(item => item.PartnerCode)
            .Select(item => new OpeningBalancePartyOptionDto
            {
                Id = item.Id,
                Code = item.CustomerAccountNumber ?? item.PartnerCode,
                Name = item.PartnerName
            })
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
        var customer = await ResolveOpeningArPartnerAsync(
            dto.BusinessPartnerId, dto.BusinessPartnerRoleId, dto.OpeningDate, cancellationToken);
        var context = await ResolveSpecializedOpeningContextAsync(dto, cancellationToken);
        var advanceAccountId = context.Settings.CustomerAdvanceAccountId
            ?? throw new InvalidOperationException("Customer Advance Account is not configured in Finance Settings.");
        var paymentId = Guid.NewGuid();
        var reference = BuildOpeningReference("AR-ADV-OPEN", paymentId);
        var payment = new CustomerPayment
        {
            Id = paymentId, TenantId = tenantId, BusinessPartnerId = customer.Partner.Id,
            BusinessPartnerRoleId = customer.Role.Id,
            BusinessPartnerArProfileVersionId = customer.Profile.Id,
            BusinessPartnerCode = customer.Partner.PartnerCode,
            BusinessPartnerName = customer.Partner.PartnerName,
            BusinessPartnerLegalName = customer.Partner.LegalName,
            BusinessPartnerTaxIdentificationNumber = customer.Partner.TaxIdentificationNumber,
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
        var supplier = await ResolveOpeningApPartnerAsync(
            dto.BusinessPartnerId, dto.BusinessPartnerRoleId, dto.OpeningDate, cancellationToken);
        var context = await ResolveSpecializedOpeningContextAsync(dto, cancellationToken, requireFunctionalCurrency: true);
        await ValidateWithholdingConfigurationAsync(dto.TaxId, dto.WithholdingTaxAccountId, useReceivableAccount: false, cancellationToken);
        if (dto.TaxableBase < dto.Amount || dto.NetPaidAmount < 0m)
            throw new InvalidOperationException("AP WHT opening taxable base/net-paid evidence is invalid.");
        var paymentId = Guid.NewGuid();
        var reference = BuildOpeningReference("AP-WHT-OPEN", paymentId);
        var payment = new VendorPayment
        {
            Id = paymentId, TenantId = tenantId, BusinessPartnerId = supplier.Partner.Id,
            BusinessPartnerRoleId = supplier.Role.Id,
            BusinessPartnerApProfileVersionId = supplier.Profile.Id,
            BusinessPartnerCode = supplier.Partner.PartnerCode,
            BusinessPartnerName = supplier.Partner.PartnerName,
            BusinessPartnerLegalName = supplier.Partner.LegalName,
            BusinessPartnerTaxIdentificationNumber = supplier.Partner.TaxIdentificationNumber,
            PaymentNumber = reference,
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
        var customer = await ResolveOpeningArPartnerAsync(
            dto.BusinessPartnerId, dto.BusinessPartnerRoleId, dto.OpeningDate, cancellationToken);
        var context = await ResolveSpecializedOpeningContextAsync(dto, cancellationToken, requireFunctionalCurrency: true);
        await ValidateWithholdingConfigurationAsync(dto.TaxId, dto.WithholdingTaxAccountId, useReceivableAccount: true, cancellationToken);
        var paymentId = Guid.NewGuid();
        var reference = BuildOpeningReference("AR-WHT-OPEN", paymentId);
        var payment = new CustomerPayment
        {
            Id = paymentId, TenantId = tenantId, BusinessPartnerId = customer.Partner.Id,
            BusinessPartnerRoleId = customer.Role.Id,
            BusinessPartnerArProfileVersionId = customer.Profile.Id,
            BusinessPartnerCode = customer.Partner.PartnerCode,
            BusinessPartnerName = customer.Partner.PartnerName,
            BusinessPartnerLegalName = customer.Partner.LegalName,
            BusinessPartnerTaxIdentificationNumber = customer.Partner.TaxIdentificationNumber,
            PaymentNumber = reference, ReferenceNumber = reference,
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
        return await CreateBatchCoreAsync(new CreateOpeningBalanceBatchDto
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
        }, isServerDerived: true, cancellationToken);
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
                RoundRate(approvedRate.InverseRate) != RoundRate(rate))
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
    private sealed record GovernedOpeningHeader(
        string BatchNumber,
        string SourceReference,
        string? Description,
        DateTime OpeningDate,
        Guid FiscalPeriodId,
        string BookClassification,
        string FunctionalCurrency,
        string IdempotencyKey);
    private sealed record BankOpeningContext(
        BankAccount Bank,
        Account BankGl,
        Account MigrationClearing,
        FinanceSettings Settings,
        string BankCurrency,
        BankExchangeRateSnapshot ExchangeRate);
    private sealed record BankOpeningInspection(
        Account? BankGl,
        Account? MigrationClearing,
        string BankCurrency,
        BankExchangeRateSnapshot? ExchangeRate,
        IReadOnlyList<string> Errors);
    private sealed record BankExchangeRateSnapshot(
        Guid? ExchangeRateId,
        decimal Rate,
        DateTime? EffectiveDate,
        ExchangeRateType RateType,
        ExchangeRateQuoteSide QuoteSide,
        string Source);
    private sealed record GovernedIdempotencyResolution(
        OpeningBalanceBatch? Existing,
        string EffectiveKey);
    private sealed record ResidualOpeningContext(
        Account AccruedExpenses,
        Account ShareCapital,
        Account RetainedEarnings,
        Account MigrationClearing,
        FinanceSettings Settings);

    public async Task<OpeningBalanceBatchDto?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        => await MapBatchAsync(batchId, cancellationToken);

    public async Task<IReadOnlyList<OpeningBalanceBatchDto>> GetBatchesAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var batches = await _db.OpeningBalanceBatches
            .AsNoTracking()
            .Include(batch => batch.FiscalPeriod)
            .Include(batch => batch.Lines)
            .Where(batch => batch.TenantId == tenantId && !batch.IsDeleted)
            .OrderByDescending(batch => batch.UpdatedAt ?? batch.CreatedAt)
            .ThenByDescending(batch => batch.CreatedAt)
            .ToListAsync(cancellationToken);
        return batches.Select(batch =>
        {
            var sourceKind = GetOpeningSourceKind(batch);
            var isSystemGenerated = !string.Equals(sourceKind, SourceKindFreeForm, StringComparison.Ordinal);
            return new OpeningBalanceBatchDto
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
                SourceKind = sourceKind,
                IsSystemGenerated = isSystemGenerated,
                IsEditable = !isSystemGenerated && IsEditableStatus(batch.Status),
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
            };
        }).ToList();
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
            // Generated lines are derived from canonical subledger/register, bank-master, or
            // governed residual-GL evidence. Letting a user replace them with arbitrary accounts
            // would convert a server-owned trust decision back into a free-form journal.
            throw new InvalidOperationException(
                "Generated opening batches cannot be edited manually. Correct the source evidence and create a new batch.");
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

        await EnsureFreeFormOpeningLinesAllowedAsync(tenantId, dto.Lines, cancellationToken);

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

        if (!HasGeneratedSubledgerOpeningEvidence(batch))
        {
            // Re-evaluate at validation/submit time. A draft that was created before this control
            // existed—or before an account became a configured control—must not be grandfathered
            // into posting merely because it is already stored.
            errors.AddRange(await GetFreeFormOpeningLineErrorsAsync(
                tenantId,
                batch.Lines.Select(line => new FreeFormOpeningLineCandidate(
                    line.LineNumber,
                    line.AccountId,
                    line.CounterpartyType,
                    line.BankAccountId)),
                cancellationToken));
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
        else if (IsGovernedFinanceOpeningBatch(batch) && (!period.IsOpen || period.IsClosed || period.IsLocked))
        {
            // Governed sources are immutable after preparation, so period eligibility must be
            // re-established at every Validate -> Submit -> Post boundary rather than relying on
            // the period state observed when the server first derived the draft.
            errors.Add("Governed opening sources require an open and unlocked fiscal period.");
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
                // exceptions are server-generated specialised advances and the primary governed
                // bank line. Both carry canonical source linkage, an approved rate ID, native
                // amount and functional amount which are revalidated below and again at posting.
                var isControlledForeignSource = IsSpecializedOpeningLine(line) ||
                    IsBankAccountOpeningPrimaryLine(line);
                if (!isControlledForeignSource || !line.ExchangeRateId.HasValue ||
                    (!line.TransactionDebitAmount.HasValue && !line.TransactionCreditAmount.HasValue))
                {
                    errors.Add($"Line {line.LineNumber}: foreign-currency opening balances require a controlled source and approved FX evidence.");
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

            if (!account.AllowDirectPosting && !IsGovernedProtectedAccountLine(line))
            {
                errors.Add($"Line {line.LineNumber}: account '{account.AccountCode}' does not allow direct posting.");
            }
        }

        await ValidateFixedAssetOpeningEvidenceAsync(batch, errors, cancellationToken);
        await ValidateSpecializedOpeningEvidenceAsync(batch, errors, cancellationToken);
        await ValidateBankAccountOpeningEvidenceAsync(batch, errors, cancellationToken);
        await ValidateResidualGlEquityOpeningEvidenceAsync(batch, errors, cancellationToken);

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
        var batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
        if (IsSubmissionTerminalOrInFlightStatus(batch.Status))
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
        if (!string.Equals(batch.Status, StatusDraft, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(batch.Status, StatusValidated, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Opening balance batch cannot be submitted while it is {batch.Status}; only Draft or Validated batches may start approval.");
        }

        var validation = await ValidateBatchAsync(batchId, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Opening balance batch failed validation: {string.Join("; ", validation.Errors)}");
        }

        batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
        if (IsSubmissionTerminalOrInFlightStatus(batch.Status))
        {
            return await MapBatchAsync(batch.Id, cancellationToken)
                ?? throw new InvalidOperationException("Opening balance batch was not found.");
        }
        if (!string.Equals(batch.Status, StatusValidated, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Opening balance batch cannot start approval from status {batch.Status}.");
        }

        var now = DateTime.UtcNow;
        if (_workflowService == null)
        {
            throw new InvalidOperationException(
                "Controlled opening balances require the Finance approval workflow; approval cannot be inferred when workflow integration is unavailable.");
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
            var isActiveWorkflow = workflowResult.WorkflowInstanceId.HasValue &&
                workflowResult.WorkflowInstanceId.Value != Guid.Empty &&
                workflowResult.Status is WorkflowInstanceStatus.Created or
                    WorkflowInstanceStatus.InProgress or
                    WorkflowInstanceStatus.Waiting or
                    WorkflowInstanceStatus.Suspended;
            if (!isActiveWorkflow)
            {
                batch.Status = StatusFailed;
                batch.FailedAt = now;
                batch.FailureReason =
                    "Opening-balance approval must start as a real pending workflow; completed-at-start or missing workflow evidence cannot auto-approve the source.";
                await _db.SaveChangesAsync(cancellationToken);
                await RecordAuditAsync(
                    FinanceAuditEvents.FinanceWorkflowApprovalFailed,
                    batch,
                    afterValues: new { workflowResult.Status, workflowResult.WorkflowInstanceId },
                    reason: batch.FailureReason,
                    cancellationToken: cancellationToken);
                throw new InvalidOperationException(batch.FailureReason);
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

    private static bool IsSubmissionTerminalOrInFlightStatus(string? status)
        => string.Equals(status, StatusPendingApproval, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StatusApproved, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StatusPostingFailed, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StatusPosted, StringComparison.OrdinalIgnoreCase);

    public async Task<OpeningBalanceBatchDto> PostAsync(
        Guid batchId,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
        if (string.Equals(batch.Status, StatusPosted, StringComparison.OrdinalIgnoreCase))
        {
            if (!await HasDurablePostedEvidenceAsync(batch, cancellationToken))
            {
                throw new InvalidOperationException(
                    "Opening balance batch reports Posted but its durable journal/posting evidence is incomplete.");
            }
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
        FinancePostingResultDto? result = null;
        try
        {
            if (HasBankAccountOpeningEvidence(batch))
            {
                // The bank read-side snapshot is part of the controlled opening fact. Post the GL,
                // batch back-links, and exact bank snapshot in one serializable transaction so a
                // retry cannot move cash twice or leave Cash Management out of step with GL.
                result = await PostBankAccountOpeningAtomicallyAsync(batchId, cancellationToken);
                batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
            }
            else if (HasResidualGlEquityOpeningEvidence(batch))
            {
                // The residual is the cutover close-out of Migration Clearing. Recheck the exact
                // posted clearing balance and post its zeroing journal inside one serializable
                // transaction so no upstream source can race between validation and posting.
                result = await PostResidualOpeningAtomicallyAsync(batchId, cancellationToken);
                batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
            }
            else
            {
                var request = await BuildPostingRequestAsync(batch, cancellationToken);
                result = await _postingEngine.PostAsync(request, cancellationToken);
                // The posting engine may clear the tracker while recovering an idempotent/concurrent
                // insert race. Rehydrate before saving batch/register back-links; otherwise GL could
                // be correct while the migration workspace still appears unposted to another user.
                if (_db.Entry(batch).State == EntityState.Detached)
                {
                    batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
                }
                ApplyPostedBatchState(batch, result);

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
            }

        }
        catch (Exception ex)
        {
            try
            {
                _db.ChangeTracker.Clear();
                batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
            }
            catch (Exception reloadException)
            {
                _logger?.LogError(
                    reloadException,
                    "Opening-balance posting for batch {BatchId} failed and its durable state could not be reloaded; no failure status was written.",
                    batchId);
                throw new InvalidOperationException(
                    "Opening-balance posting failed and its durable state could not be verified; no failure status was written.",
                    new AggregateException(ex, reloadException));
            }

            if (string.Equals(batch.Status, StatusPosted, StringComparison.OrdinalIgnoreCase))
            {
                if (!await HasDurablePostedEvidenceAsync(batch, cancellationToken))
                {
                    _logger?.LogError(
                        ex,
                        "Opening-balance batch {BatchId} reports Posted but journal {JournalEntryId} and posting event {PostingEventId} are not durably complete; the Posted state was preserved for investigation.",
                        batch.Id,
                        batch.JournalEntryId,
                        batch.PostingEventId);
                    throw new InvalidOperationException(
                        "Opening balance batch reports Posted but its durable journal/posting evidence is incomplete; its status was preserved for investigation.",
                        ex);
                }

                _logger?.LogWarning(
                    ex,
                    "Opening-balance posting for batch {BatchId} raised after durable journal {JournalEntryId} and posting event {PostingEventId} completed; the Posted state was preserved.",
                    batch.Id,
                    batch.JournalEntryId,
                    batch.PostingEventId);
            }
            else
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

                try
                {
                    await RecordAuditAsync(
                        FinanceAuditEvents.OpeningBalancePostingFailed,
                        batch,
                        reason: ex.Message,
                        cancellationToken: cancellationToken);
                }
                catch (Exception auditException)
                {
                    _logger?.LogError(
                        auditException,
                        "Opening-balance failure audit could not be recorded for batch {BatchId}; posting remains {Status}.",
                        batch.Id,
                        batch.Status);
                }
                throw;
            }
        }

        try
        {
            await RecordAuditAsync(
                FinanceAuditEvents.OpeningBalancePosted,
                batch,
                afterValues: new
                {
                    JournalEntryId = result?.JournalEntryId ?? batch.JournalEntryId,
                    PostingEventId = result?.PostingEventId ?? batch.PostingEventId,
                    WasDuplicate = result?.WasDuplicate,
                    TotalDebitAmount = result?.TotalDebitAmount ?? batch.TotalDebit,
                    TotalCreditAmount = result?.TotalCreditAmount ?? batch.TotalCredit
                },
                comment: comment,
                cancellationToken: cancellationToken);
        }
        catch (Exception auditException)
        {
            // Audit transport cannot roll back or relabel an already committed ledger fact. Log the
            // exact durable back-links for operational recovery and return truthful Posted state.
            _logger?.LogError(
                auditException,
                "Opening-balance success audit failed after durable posting for batch {BatchId}, journal {JournalEntryId}, posting event {PostingEventId}; ledger state remains Posted.",
                batch.Id,
                batch.JournalEntryId,
                batch.PostingEventId);
        }

        return await MapBatchAsync(batch.Id, cancellationToken)
            ?? throw new InvalidOperationException("Opening balance batch was posted but could not be reloaded.");
    }

    public async Task<OpeningBalanceBatchReversalDto> RequestReversalAsync(
        Guid batchId,
        RequestOpeningBalanceBatchReversalDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (_reversalPolicyService == null)
            throw new InvalidOperationException("Finance reversal policy is not configured for opening balances.");
        var userId = CurrentUserId() ?? throw new InvalidOperationException("A resolved user identity is required to request an opening-balance reversal.");
        var batch = await LoadBatchAsync(TenantId, batchId, cancellationToken);
        EnsurePostedBatchCanBeReversed(batch);
        await EnsureOpeningReversalDependenciesAsync(batch, cancellationToken);

        var existing = await _db.OpeningBalanceBatchReversals.AnyAsync(item =>
            item.TenantId == batch.TenantId && item.OpeningBalanceBatchId == batch.Id &&
            item.Status != OpeningBalanceBatchReversalStatuses.Rejected && !item.IsDeleted,
            cancellationToken);
        if (existing)
            throw new InvalidOperationException("This opening-balance batch already has an active or posted reversal request.");

        var policy = await _reversalPolicyService.ResolveAsync(batch.OpeningDate, dto.Reason, dto.ReversalDate, cancellationToken);
        var impact = dto.ImpactAssessment?.Trim() ?? string.Empty;
        if (impact.Length < 20)
            throw new ArgumentException("The opening-balance reversal impact assessment must contain at least 20 characters.", nameof(dto));

        var request = new OpeningBalanceBatchReversal
        {
            TenantId = batch.TenantId,
            OpeningBalanceBatchId = batch.Id,
            OriginalPostingEventId = batch.PostingEventId!.Value,
            OriginalJournalEntryId = batch.JournalEntryId!.Value,
            SourceKind = GetOpeningSourceKind(batch),
            BookClassification = batch.BookClassification,
            OriginalOpeningDate = batch.OpeningDate,
            OriginalTotalDebit = batch.TotalDebit,
            OriginalTotalCredit = batch.TotalCredit,
            Status = OpeningBalanceBatchReversalStatuses.PendingApproval,
            Reason = policy.Reason,
            ImpactAssessment = impact,
            RequestedReversalDate = policy.ReversalDate,
            RequestedByUserId = userId,
            RequestedByUserName = _currentUser.UserName ?? "system",
            RequestedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName ?? "system",
            CreatedById = userId
        };
        _db.OpeningBalanceBatchReversals.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(
            FinanceAuditEvents.OpeningBalanceReversalRequested,
            batch,
            beforeValues: new { batch.Status, batch.JournalEntryId, batch.PostingEventId },
            afterValues: MapReversal(request),
            reason: request.Reason,
            comment: impact,
            cancellationToken: cancellationToken);
        return MapReversal(request);
    }

    public async Task<OpeningBalanceBatchReversalDto> ReviewReversalAsync(
        Guid batchId,
        Guid requestId,
        ReviewOpeningBalanceBatchReversalDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var userId = CurrentUserId() ?? throw new InvalidOperationException("A resolved user identity is required to review an opening-balance reversal.");
        var request = await LoadReversalAsync(batchId, requestId, cancellationToken);
        if (request.Status != OpeningBalanceBatchReversalStatuses.PendingApproval)
            throw new InvalidOperationException("Only an opening-balance reversal awaiting approval can be reviewed.");
        if (request.RequestedByUserId == userId)
            throw new InvalidOperationException("The reversal requester cannot review the same request.");
        var comment = dto.ReviewComment?.Trim() ?? string.Empty;
        if (comment.Length < 20)
            throw new ArgumentException("The opening-balance reversal review comment must contain at least 20 characters.", nameof(dto));

        if (dto.Approved)
        {
            EnsureReversalSnapshotStillMatches(request);
            await EnsureOpeningReversalDependenciesAsync(request.OpeningBalanceBatch, cancellationToken);
        }
        request.Status = dto.Approved
            ? OpeningBalanceBatchReversalStatuses.Approved
            : OpeningBalanceBatchReversalStatuses.Rejected;
        request.ReviewedByUserId = userId;
        request.ReviewedByUserName = _currentUser.UserName ?? "system";
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewComment = comment;
        request.UpdatedAt = DateTime.UtcNow;
        request.UpdatedBy = _currentUser.UserName ?? "system";
        request.LastModifiedById = userId;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(
            dto.Approved ? FinanceAuditEvents.OpeningBalanceReversalApproved : FinanceAuditEvents.OpeningBalanceReversalRejected,
            request.OpeningBalanceBatch,
            beforeValues: new { Status = OpeningBalanceBatchReversalStatuses.PendingApproval },
            afterValues: new { request.Id, request.Status, request.ReviewedByUserId, request.ReviewedAt },
            reason: request.Reason,
            comment: comment,
            cancellationToken: cancellationToken);
        return MapReversal(request);
    }

    public async Task<OpeningBalanceBatchReversalDto> PostReversalAsync(
        Guid batchId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (_reversalPolicyService == null)
            throw new InvalidOperationException("Finance reversal policy is not configured for opening balances.");
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var request = await LoadReversalAsync(batchId, requestId, cancellationToken);
                if (request.Status == OpeningBalanceBatchReversalStatuses.Posted)
                    return MapReversal(request);
                if (request.Status != OpeningBalanceBatchReversalStatuses.Approved)
                    throw new InvalidOperationException("Only an independently approved opening-balance reversal can be posted.");
                EnsureReversalSnapshotStillMatches(request);
                await EnsureOpeningReversalDependenciesAsync(request.OpeningBalanceBatch, cancellationToken);
                var policy = await _reversalPolicyService.ResolveAsync(
                    request.OriginalOpeningDate, request.Reason, request.RequestedReversalDate, cancellationToken);
                var plan = await _postingEngine.GetReversalPlanAsync(
                    request.OriginalPostingEventId, policy.Reason, policy.ReversalDate, cancellationToken);
                var functionalCurrency = await GetFunctionalCurrencyAsync(request.TenantId, cancellationToken);
                var posting = await _postingEngine.PostAsync(new FinancePostingRequestV2Dto
                {
                    SourceModule = SourceModule,
                    SourceDocumentType = "OpeningBalanceBatchReversal",
                    SourceDocumentId = request.Id,
                    SourceDocumentTenantId = request.TenantId,
                    PostingAction = "ReverseOpeningBalance",
                    SourceDocumentReference = request.OpeningBalanceBatch.BatchNumber,
                    Description = $"Reverse opening balance batch {request.OpeningBalanceBatch.BatchNumber}",
                    PostingDate = plan.ReversalDate,
                    JournalType = "Opening Balance Reversal",
                    AccountingBookCode = request.BookClassification,
                    FunctionalCurrencyCode = functionalCurrency,
                    ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
                    ReversalReason = policy.Reason,
                    ReversalType = "Opening Balance",
                    IdempotencyKey = $"MIGRATION:OpeningBalanceReversal:{request.TenantId:N}:{request.Id:N}",
                    ReturnExistingOnDuplicate = true,
                    Lines = plan.ReversalLines
                }, cancellationToken);

                if (_db.Entry(request).State == EntityState.Detached)
                    request = await LoadReversalAsync(batchId, requestId, cancellationToken);
                await ApplyOpeningReversalToSourceAsync(request, posting, cancellationToken);
                request.ReversalJournalEntryId = posting.JournalEntryId;
                request.ReversalPostingEventId = posting.PostingEventId;
                request.PostedAt = posting.PostingDate;
                request.Status = OpeningBalanceBatchReversalStatuses.Posted;
                request.OpeningBalanceBatch.Status = StatusReversed;
                request.OpeningBalanceBatch.UpdatedAt = DateTime.UtcNow;
                request.OpeningBalanceBatch.UpdatedBy = _currentUser.UserName ?? "system";
                request.OpeningBalanceBatch.LastModifiedById = CurrentUserId();
                await _db.SaveChangesAsync(cancellationToken);
                await RecordAuditAsync(
                    FinanceAuditEvents.OpeningBalanceReversed,
                    request.OpeningBalanceBatch,
                    beforeValues: new { Status = StatusPosted, request.OriginalJournalEntryId, request.OriginalPostingEventId },
                    afterValues: new { request.Status, posting.JournalEntryId, posting.PostingEventId, posting.PostingDate },
                    reason: policy.Reason,
                    comment: request.ImpactAssessment,
                    cancellationToken: cancellationToken);
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
                return MapReversal(request);
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<IReadOnlyList<OpeningBalanceBatchReversalDto>> GetReversalsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var exists = await _db.OpeningBalanceBatches.AsNoTracking().AnyAsync(batch =>
            batch.TenantId == tenantId && batch.Id == batchId && !batch.IsDeleted, cancellationToken);
        if (!exists)
            throw new InvalidOperationException("Opening balance batch was not found for the current tenant.");
        var reversals = await _db.OpeningBalanceBatchReversals.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.OpeningBalanceBatchId == batchId && !item.IsDeleted)
            .OrderByDescending(item => item.RequestedAt)
            .ToListAsync(cancellationToken);
        return reversals.Select(MapReversal).ToList();
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

    private async Task<FinancePostingRequestV2Dto> BuildPostingRequestAsync(
        OpeningBalanceBatch batch,
        CancellationToken cancellationToken)
    {
        var functionalCurrency = await GetFunctionalCurrencyAsync(batch.TenantId, cancellationToken);
        return new FinancePostingRequestV2Dto
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
            AccountingBookCode = batch.BookClassification,
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
                OpeningJournalEntryId = value.OpeningJournalEntryId,
                OpeningReversalJournalEntryId = value.OpeningReversalJournalEntryId,
                OpeningReversalPostingEventId = value.OpeningReversalPostingEventId,
                OpeningReversedAt = value.OpeningReversedAt
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

        if (evidenceLines.Any(line => !line.CounterpartyId.HasValue))
        {
            errors.Add("Fixed-asset opening lines are missing canonical book-value evidence.");
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
        // A posted compensating reversal reopens the source without erasing the immutable
        // original journal reference. The reversal posting-event is the authoritative evidence
        // that the old journal no longer consumes opening eligibility. Pending/rejected/failed
        // requests never populate that reference and therefore remain blocked.
        if (value.OpeningPostedToGl ||
            (value.OpeningJournalEntryId.HasValue && !value.OpeningReversalPostingEventId.HasValue))
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
        var specializedLines = batch.Lines.Where(IsSpecializedOpeningLine).ToList();
        if (specializedLines.Count == 0)
            return;
        if (specializedLines.Count != 1)
        {
            errors.Add("A specialised opening batch must contain exactly one canonical source line.");
            return;
        }

        var line = specializedLines[0];
        if (!line.CounterpartyId.HasValue)
        {
            errors.Add("The specialised opening source line is missing its canonical source record.");
            return;
        }

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

    private async Task ValidateBankAccountOpeningEvidenceAsync(
        OpeningBalanceBatch batch,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        if (!HasBankAccountOpeningEvidence(batch))
            return;
        if (batch.Lines.Count != 2 ||
            batch.Lines.Count(IsBankAccountOpeningPrimaryLine) != 1 ||
            batch.Lines.Count(line => line.CounterpartyType == BankOpeningClearing) != 1)
        {
            errors.Add("A governed bank opening must contain exactly one bank debit and one migration-clearing credit.");
            return;
        }

        var primary = batch.Lines.Single(IsBankAccountOpeningPrimaryLine);
        var clearing = batch.Lines.Single(line => line.CounterpartyType == BankOpeningClearing);
        if (!primary.BankAccountId.HasValue || primary.CounterpartyId != primary.BankAccountId ||
            clearing.BankAccountId != primary.BankAccountId || clearing.CounterpartyId != primary.BankAccountId)
            errors.Add("Governed bank opening lines lost their canonical bank-master linkage.");
        if (primary.DebitAmount <= 0m || primary.CreditAmount != 0m ||
            clearing.DebitAmount != 0m || clearing.CreditAmount != primary.DebitAmount)
            errors.Add("Governed bank opening amounts or debit/credit directions changed after preparation.");
        if (primary.TransactionDebitAmount.GetValueOrDefault() <= 0m || primary.TransactionCreditAmount != 0m ||
            clearing.TransactionDebitAmount != 0m || clearing.TransactionCreditAmount != clearing.CreditAmount)
            errors.Add("Governed bank opening native/functional amount evidence changed after preparation.");
        if (string.IsNullOrWhiteSpace(batch.SourceReference) ||
            batch.Lines.Any(line => !string.Equals(line.SourceReference, batch.SourceReference, StringComparison.Ordinal)))
            errors.Add("Governed bank opening source evidence reference is missing or inconsistent.");
        if (!primary.BankAccountId.HasValue)
            return;

        var isPosted = string.Equals(batch.Status, StatusPosted, StringComparison.OrdinalIgnoreCase) &&
            batch.JournalEntryId.HasValue && batch.PostingEventId.HasValue;
        try
        {
            var context = await ResolveBankOpeningContextAsync(
                batch.TenantId,
                primary.BankAccountId.Value,
                batch.OpeningDate.Date,
                primary.ExchangeRateId,
                allowDerivedRate: false,
                excludeBatchId: batch.Id,
                requireUnusedState: !isPosted,
                cancellationToken: cancellationToken);
            if (primary.AccountId != context.BankGl.Id || clearing.AccountId != context.MigrationClearing.Id)
                errors.Add("Governed bank opening GL mapping changed after batch preparation.");
            var expectedFunctionalAmount = RoundMoney(
                primary.TransactionDebitAmount.GetValueOrDefault() * context.ExchangeRate.Rate);
            if (primary.DebitAmount != expectedFunctionalAmount || clearing.CreditAmount != expectedFunctionalAmount ||
                !string.Equals(primary.TransactionCurrencyCode, context.BankCurrency, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(primary.FunctionalCurrencyCode, NormalizeCurrency(context.Settings.BaseCurrency, "GHS"), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(clearing.TransactionCurrencyCode, NormalizeCurrency(context.Settings.BaseCurrency, "GHS"), StringComparison.OrdinalIgnoreCase) ||
                primary.ExchangeRateId != context.ExchangeRate.ExchangeRateId ||
                primary.ExchangeRateDate?.Date != context.ExchangeRate.EffectiveDate?.Date ||
                clearing.ExchangeRateId.HasValue || clearing.ExchangeRateDate.HasValue)
                errors.Add("Governed bank opening currency or approved exchange-rate evidence changed after preparation.");
            if (isPosted)
            {
                var nativeAmount = primary.TransactionDebitAmount.GetValueOrDefault();
                if (context.Bank.OpeningBalance != 0m ||
                    context.Bank.CurrentBalance != nativeAmount ||
                    context.Bank.AvailableBalance != nativeAmount)
                    errors.Add("Posted governed bank opening no longer reconciles to the bank read-side snapshot.");
                var postedBankMovement = await _db.AccountTransactions.AsNoTracking()
                    .Where(item =>
                        item.TenantId == batch.TenantId &&
                        item.JournalEntryId == batch.JournalEntryId &&
                        item.AccountId == context.BankGl.Id &&
                        item.PostingStatus == "Posted" &&
                        !item.IsDeleted)
                    .SumAsync(item => item.DebitAmount - item.CreditAmount, cancellationToken);
                if (RoundMoney(postedBankMovement) != expectedFunctionalAmount)
                    errors.Add("Posted governed bank opening journal no longer matches its canonical bank amount.");
            }
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
        }
    }

    private async Task ValidateResidualGlEquityOpeningEvidenceAsync(
        OpeningBalanceBatch batch,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        if (!HasResidualGlEquityOpeningEvidence(batch))
            return;
        var markerSet = new[]
        {
            ResidualMigrationClearing,
            ResidualAccruedOpening,
            ResidualShareCapitalOpening,
            ResidualRetainedEarnings
        };
        if (batch.Lines.Count != 4 || markerSet.Any(marker => batch.Lines.Count(line => line.CounterpartyType == marker) != 1))
        {
            errors.Add("A governed residual GL/equity opening must contain exactly one clearing, accrued-expenses, share-capital, and retained-earnings line.");
            return;
        }
        var clearing = batch.Lines.Single(line => line.CounterpartyType == ResidualMigrationClearing);
        var accrued = batch.Lines.Single(line => line.CounterpartyType == ResidualAccruedOpening);
        var share = batch.Lines.Single(line => line.CounterpartyType == ResidualShareCapitalOpening);
        var retained = batch.Lines.Single(line => line.CounterpartyType == ResidualRetainedEarnings);
        if (batch.Lines.Any(line => line.BankAccountId.HasValue || line.CounterpartyId != line.AccountId))
            errors.Add("Governed residual opening lines lost their server-derived account evidence.");
        if (accrued.DebitAmount != 0m || accrued.CreditAmount <= 0m ||
            share.DebitAmount != 0m || share.CreditAmount <= 0m ||
            retained.DebitAmount != 0m || retained.CreditAmount <= 0m ||
            clearing.CreditAmount != 0m || clearing.DebitAmount <= 0m ||
            clearing.DebitAmount != RoundMoney(accrued.CreditAmount + share.CreditAmount + retained.CreditAmount))
            errors.Add("Governed residual opening amounts or debit/credit directions changed after preparation.");
        if (batch.Lines.Any(line =>
                line.TransactionDebitAmount != line.DebitAmount ||
                line.TransactionCreditAmount != line.CreditAmount))
            errors.Add("Governed residual opening native/functional amount evidence changed after preparation.");
        if (string.IsNullOrWhiteSpace(batch.SourceReference) ||
            batch.Lines.Any(line => !string.Equals(line.SourceReference, batch.SourceReference, StringComparison.Ordinal)))
            errors.Add("Governed residual opening source evidence reference is missing or inconsistent.");
        try
        {
            var context = await ResolveResidualOpeningContextAsync(
                batch.TenantId,
                accrued.AccountId,
                share.AccountId,
                batch.OpeningDate.Date,
                NormalizeCurrency(accrued.FunctionalCurrencyCode, "GHS"),
                batch.Id,
                batch.BookClassification,
                cancellationToken);
            if (clearing.AccountId != context.MigrationClearing.Id || retained.AccountId != context.RetainedEarnings.Id)
                errors.Add("Governed residual opening Finance Settings mappings changed after batch preparation.");
            await EnsureResidualClearsMigrationBalanceAsync(
                batch.TenantId,
                context.MigrationClearing.Id,
                batch.OpeningDate,
                batch.BookClassification,
                clearing.DebitAmount,
                batch.Id,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
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
            RoundRate(approvedRate.InverseRate) != RoundRate(sourceRate))
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

    private async Task<OpeningBalanceBatchReversal> LoadReversalAsync(
        Guid batchId,
        Guid requestId,
        CancellationToken cancellationToken)
        => await _db.OpeningBalanceBatchReversals
            .Include(item => item.OpeningBalanceBatch)
                .ThenInclude(batch => batch.Lines)
            .SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == requestId &&
                item.OpeningBalanceBatchId == batchId && !item.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("Opening-balance reversal request was not found for the current tenant.");

    private static void EnsurePostedBatchCanBeReversed(OpeningBalanceBatch batch)
    {
        if (!string.Equals(batch.Status, StatusPosted, StringComparison.OrdinalIgnoreCase) ||
            !batch.JournalEntryId.HasValue || !batch.PostingEventId.HasValue)
        {
            throw new InvalidOperationException("Only a posted opening-balance batch with complete journal evidence can be reversed.");
        }
    }

    private static void EnsureReversalSnapshotStillMatches(OpeningBalanceBatchReversal request)
    {
        var batch = request.OpeningBalanceBatch;
        EnsurePostedBatchCanBeReversed(batch);
        if (batch.JournalEntryId != request.OriginalJournalEntryId ||
            batch.PostingEventId != request.OriginalPostingEventId ||
            batch.OpeningDate != request.OriginalOpeningDate ||
            !string.Equals(batch.BookClassification, request.BookClassification, StringComparison.OrdinalIgnoreCase) ||
            batch.TotalDebit != request.OriginalTotalDebit || batch.TotalCredit != request.OriginalTotalCredit ||
            !string.Equals(GetOpeningSourceKind(batch), request.SourceKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The posted opening-balance evidence no longer matches the approved reversal snapshot.");
        }
    }

    private async Task EnsureOpeningReversalDependenciesAsync(
        OpeningBalanceBatch batch,
        CancellationToken cancellationToken)
    {
        EnsurePostedBatchCanBeReversed(batch);
        if (!HasResidualGlEquityOpeningEvidence(batch))
        {
            var residualStillPosted = await _db.OpeningBalanceBatches.AnyAsync(candidate =>
                candidate.TenantId == batch.TenantId && candidate.Id != batch.Id && !candidate.IsDeleted &&
                candidate.Status == StatusPosted && candidate.OpeningDate == batch.OpeningDate &&
                candidate.BookClassification == batch.BookClassification &&
                candidate.Lines.Any(line => line.CounterpartyType == ResidualAccruedOpening ||
                    line.CounterpartyType == ResidualShareCapitalOpening ||
                    line.CounterpartyType == ResidualRetainedEarnings ||
                    line.CounterpartyType == ResidualMigrationClearing) &&
                !candidate.Reversals.Any(reversal => !reversal.IsDeleted &&
                    reversal.Status == OpeningBalanceBatchReversalStatuses.Posted),
                cancellationToken);
            if (residualStillPosted)
                throw new InvalidOperationException("Reverse the posted residual GL/equity close-out batch before reversing an upstream opening source.");
        }

        if (HasBankAccountOpeningEvidence(batch))
        {
            var primary = batch.Lines.Single(IsBankAccountOpeningPrimaryLine);
            var bankId = primary.BankAccountId ?? throw new InvalidOperationException("Bank opening evidence is missing its bank account link.");
            var bank = await _db.BankAccounts.AsNoTracking().SingleAsync(item =>
                item.TenantId == batch.TenantId && item.Id == bankId && !item.IsDeleted, cancellationToken);
            var originalAmount = primary.TransactionDebitAmount ?? primary.DebitAmount;
            if (bank.CurrentBalance != originalAmount || bank.AvailableBalance != originalAmount || bank.OpeningDate.Date != batch.OpeningDate.Date)
                throw new InvalidOperationException("The bank balance has changed since cutover; reverse or reconcile downstream bank activity before reversing this opening batch.");
        }

        var specialized = batch.Lines.SingleOrDefault(IsSpecializedOpeningLine);
        if (specialized?.CounterpartyId is Guid counterpartyId)
        {
            if (specialized.CounterpartyType is SupplierAdvanceOpening or ApWithholdingOpening)
            {
                var payment = await _db.Set<VendorPayment>().AsNoTracking().SingleAsync(item =>
                    item.TenantId == batch.TenantId && item.Id == counterpartyId && !item.IsDeleted, cancellationToken);
                if (payment.ReversalPostingEventId.HasValue)
                    throw new InvalidOperationException("The AP opening source has already been reversed through another workflow.");
                if (specialized.CounterpartyType == SupplierAdvanceOpening && payment.AllocatedAmount != 0m)
                    throw new InvalidOperationException("The supplier advance has downstream allocations and must be unapplied before its opening batch can be reversed.");
                if (specialized.CounterpartyType == ApWithholdingOpening &&
                    await _db.WithholdingTaxCertificates.AsNoTracking().AnyAsync(certificate =>
                        certificate.TenantId == batch.TenantId && certificate.VendorPaymentId == payment.Id && !certificate.IsDeleted,
                        cancellationToken))
                    throw new InvalidOperationException("The AP withholding opening has issued certificate evidence and cannot be reversed until that evidence is cancelled.");
            }
            else
            {
                var payment = await _db.Set<CustomerPayment>().AsNoTracking().SingleAsync(item =>
                    item.TenantId == batch.TenantId && item.Id == counterpartyId && !item.IsDeleted, cancellationToken);
                if (payment.ReversalPostingEventId.HasValue)
                    throw new InvalidOperationException("The AR opening source has already been reversed through another workflow.");
                if (specialized.CounterpartyType == CustomerAdvanceOpening && payment.AllocatedAmount != 0m)
                    throw new InvalidOperationException("The customer advance has downstream allocations and must be unapplied before its opening batch can be reversed.");
                if (specialized.CounterpartyType == ArWithholdingOpening &&
                    (!string.IsNullOrWhiteSpace(payment.WithholdingCertificateNumber) || payment.WithholdingCertificateDate.HasValue))
                    throw new InvalidOperationException("The AR withholding opening has certificate evidence and cannot be reversed until that evidence is cancelled.");
            }
        }

        if (HasFixedAssetOpeningEvidence(batch))
        {
            var valueIds = batch.Lines.Where(IsFixedAssetOpeningLine).Where(line => line.CounterpartyId.HasValue)
                .Select(line => line.CounterpartyId!.Value).Distinct().ToArray();
            var values = await _db.FixedAssetBookValues.AsNoTracking()
                .Where(value => value.TenantId == batch.TenantId && valueIds.Contains(value.Id) && !value.IsDeleted)
                .ToListAsync(cancellationToken);
            if (values.Any(value => value.OpeningJournalEntryId != batch.JournalEntryId || !value.OpeningPostedToGl ||
                value.OpeningReversalPostingEventId.HasValue || value.LastDepreciationDate.HasValue ||
                value.CapitalizationPostingEventId.HasValue || value.CapitalizationReversalPostingEventId.HasValue))
                throw new InvalidOperationException("Fixed-asset opening evidence has changed or has downstream accounting and cannot be reversed as a simple opening correction.");
            var assetIds = values.Select(value => value.FixedAssetId).Distinct().ToArray();
            var hasDownstream = await _db.AssetTransactions.AsNoTracking().AnyAsync(transaction =>
                transaction.TenantId == batch.TenantId && assetIds.Contains(transaction.FixedAssetId) && !transaction.IsDeleted &&
                transaction.TransactionType != "Opening Acquisition" &&
                transaction.TransactionType != "Opening Accumulated Depreciation",
                cancellationToken);
            if (hasDownstream)
                throw new InvalidOperationException("Fixed-asset depreciation, valuation, transfer, or disposal activity must be reversed before the opening batch.");
        }
    }

    private async Task ApplyOpeningReversalToSourceAsync(
        OpeningBalanceBatchReversal request,
        FinancePostingResultDto posting,
        CancellationToken cancellationToken)
    {
        var batch = request.OpeningBalanceBatch;
        var now = DateTime.UtcNow;
        if (HasBankAccountOpeningEvidence(batch))
        {
            var bankId = batch.Lines.Single(IsBankAccountOpeningPrimaryLine).BankAccountId!.Value;
            var bank = await _db.BankAccounts.SingleAsync(item => item.TenantId == batch.TenantId && item.Id == bankId, cancellationToken);
            bank.CurrentBalance = 0m;
            bank.AvailableBalance = 0m;
            bank.OpeningDate = default;
            bank.UpdatedAt = now;
            bank.UpdatedBy = _currentUser.UserName ?? "system";
            bank.LastModifiedById = CurrentUserId();
        }

        var specialized = batch.Lines.SingleOrDefault(IsSpecializedOpeningLine);
        if (specialized?.CounterpartyId is Guid counterpartyId)
        {
            if (specialized.CounterpartyType is SupplierAdvanceOpening or ApWithholdingOpening)
            {
                var payment = await _db.Set<VendorPayment>().SingleAsync(item => item.TenantId == batch.TenantId && item.Id == counterpartyId, cancellationToken);
                payment.Status = VendorPaymentStatus.Reversed;
                payment.ReversalJournalEntryId = posting.JournalEntryId;
                payment.ReversalPostingEventId = posting.PostingEventId;
                payment.ReversalDate = posting.PostingDate;
                payment.ReversedAt = now;
                payment.ReversedById = CurrentUserId();
                payment.ReversalReason = request.Reason;
            }
            else
            {
                var payment = await _db.Set<CustomerPayment>().SingleAsync(item => item.TenantId == batch.TenantId && item.Id == counterpartyId, cancellationToken);
                payment.Status = "Reversed";
                payment.ReversalJournalEntryId = posting.JournalEntryId;
                payment.ReversalPostingEventId = posting.PostingEventId;
                payment.ReversalDate = posting.PostingDate;
                payment.ReversedAt = now;
                payment.ReversedById = CurrentUserId();
                payment.ReversalReason = request.Reason;
            }
        }

        if (HasFixedAssetOpeningEvidence(batch))
        {
            var valueIds = batch.Lines.Where(IsFixedAssetOpeningLine).Where(line => line.CounterpartyId.HasValue)
                .Select(line => line.CounterpartyId!.Value).Distinct().ToArray();
            var values = await _db.FixedAssetBookValues.Where(value =>
                value.TenantId == batch.TenantId && valueIds.Contains(value.Id)).ToListAsync(cancellationToken);
            foreach (var value in values)
            {
                value.OpeningPostedToGl = false;
                value.OpeningReversalJournalEntryId = posting.JournalEntryId;
                value.OpeningReversalPostingEventId = posting.PostingEventId;
                value.OpeningReversedAt = now;
                value.UpdatedAt = now;
                value.UpdatedBy = _currentUser.UserName ?? "system";
                value.LastModifiedById = CurrentUserId();
            }
        }
    }

    private static OpeningBalanceBatchReversalDto MapReversal(OpeningBalanceBatchReversal item)
        => new()
        {
            Id = item.Id,
            OpeningBalanceBatchId = item.OpeningBalanceBatchId,
            OriginalPostingEventId = item.OriginalPostingEventId,
            OriginalJournalEntryId = item.OriginalJournalEntryId,
            ReversalPostingEventId = item.ReversalPostingEventId,
            ReversalJournalEntryId = item.ReversalJournalEntryId,
            SourceKind = item.SourceKind,
            BookClassification = item.BookClassification,
            OriginalOpeningDate = item.OriginalOpeningDate,
            OriginalTotalDebit = item.OriginalTotalDebit,
            OriginalTotalCredit = item.OriginalTotalCredit,
            Status = item.Status,
            Reason = item.Reason,
            ImpactAssessment = item.ImpactAssessment,
            RequestedReversalDate = item.RequestedReversalDate,
            RequestedByUserId = item.RequestedByUserId,
            RequestedByUserName = item.RequestedByUserName,
            RequestedAt = item.RequestedAt,
            ReviewedByUserId = item.ReviewedByUserId,
            ReviewedByUserName = item.ReviewedByUserName,
            ReviewedAt = item.ReviewedAt,
            ReviewComment = item.ReviewComment,
            PostedAt = item.PostedAt,
            FailureReason = item.FailureReason
        };

    private static bool HasGeneratedSubledgerOpeningEvidence(OpeningBalanceBatch batch)
        => HasFixedAssetOpeningEvidence(batch) ||
           batch.Lines.Any(IsSpecializedOpeningLine) ||
           HasBankAccountOpeningEvidence(batch) ||
           HasResidualGlEquityOpeningEvidence(batch);

    private static bool HasBankAccountOpeningEvidence(OpeningBalanceBatch batch)
        => batch.Lines.Any(line => line.CounterpartyType is BankAccountOpening or BankOpeningClearing);

    private static bool HasResidualGlEquityOpeningEvidence(OpeningBalanceBatch batch)
        => batch.Lines.Any(line => line.CounterpartyType is ResidualAccruedOpening or ResidualShareCapitalOpening or
            ResidualRetainedEarnings or ResidualMigrationClearing);

    private static bool IsBankAccountOpeningPrimaryLine(OpeningBalanceLine line)
        => string.Equals(line.CounterpartyType, BankAccountOpening, StringComparison.Ordinal);

    private static bool IsGovernedProtectedAccountLine(OpeningBalanceLine line)
        => line.CounterpartyType is BankAccountOpening or ResidualRetainedEarnings;

    private static bool IsGovernedFinanceOpeningBatch(OpeningBalanceBatch batch)
        => HasBankAccountOpeningEvidence(batch) || HasResidualGlEquityOpeningEvidence(batch);

    private static string GetOpeningSourceKind(OpeningBalanceBatch batch)
    {
        if (HasBankAccountOpeningEvidence(batch))
            return SourceKindBankAccountOpening;
        if (HasResidualGlEquityOpeningEvidence(batch))
            return SourceKindResidualGlEquityOpening;
        if (HasFixedAssetOpeningEvidence(batch))
            return "FixedAssetOpening";
        var specialized = batch.Lines.FirstOrDefault(IsSpecializedOpeningLine)?.CounterpartyType;
        return string.IsNullOrWhiteSpace(specialized) ? SourceKindFreeForm : specialized;
    }

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
            .Include(b => b.Reversals.Where(reversal => !reversal.IsDeleted))
            .Include(b => b.Lines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == batchId && !b.IsDeleted, cancellationToken);
        if (batch == null)
        {
            return null;
        }

        var sourceKind = GetOpeningSourceKind(batch);
        var isSystemGenerated = !string.Equals(sourceKind, SourceKindFreeForm, StringComparison.Ordinal);
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
            SourceKind = sourceKind,
            IsSystemGenerated = isSystemGenerated,
            IsEditable = !isSystemGenerated && IsEditableStatus(batch.Status),
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
            Reversals = batch.Reversals.OrderByDescending(item => item.RequestedAt).Select(MapReversal).ToList(),
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

    private async Task<FinancePostingResultDto> PostBankAccountOpeningAtomicallyAsync(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                var batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
                if (!string.Equals(batch.Status, StatusApproved, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(batch.Status, StatusPostingFailed, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Governed bank opening must remain approved before atomic posting.");
                }

                var errors = new List<string>();
                await ValidateBankAccountOpeningEvidenceAsync(batch, errors, cancellationToken);
                if (errors.Count > 0)
                    throw new InvalidOperationException($"Governed bank opening failed final validation: {string.Join("; ", errors)}");

                var primary = batch.Lines.Single(IsBankAccountOpeningPrimaryLine);
                var bank = await _db.BankAccounts.FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.Id == primary.BankAccountId && !item.IsDeleted,
                    cancellationToken)
                    ?? throw new InvalidOperationException("The governed bank opening source is no longer available for this tenant.");
                var result = await _postingEngine.PostAsync(
                    await BuildPostingRequestAsync(batch, cancellationToken),
                    cancellationToken);

                // BankAccount.OpeningBalance deliberately stays zero: bank reconciliation already
                // adds that legacy field to posted GL movement. The controlled journal is the sole
                // opening ledger fact, while Current/Available are exact Finance read-side snapshots.
                // BankAccount balances are maintained in the bank's own currency; the journal line
                // carries the separately derived functional amount used by GL and reporting.
                bank.CurrentBalance = primary.TransactionDebitAmount!.Value;
                bank.AvailableBalance = primary.TransactionDebitAmount.Value;
                bank.OpeningDate = batch.OpeningDate.Date;
                bank.UpdatedAt = DateTime.UtcNow;
                bank.UpdatedBy = _currentUser.UserName ?? "system";
                bank.LastModifiedById = CurrentUserId();
                ApplyPostedBatchState(batch, result);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private async Task<FinancePostingResultDto> PostResidualOpeningAtomicallyAsync(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                var batch = await LoadBatchAsync(tenantId, batchId, cancellationToken);
                if (!string.Equals(batch.Status, StatusApproved, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(batch.Status, StatusPostingFailed, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Governed residual opening must remain approved before atomic posting.");
                }

                await AcquireGovernedTransactionLockAsync(
                    BuildGovernedCreationLockResource(
                        tenantId,
                        "ResidualPost",
                        $"{batch.OpeningDate:yyyyMMdd}:{batch.BookClassification}"),
                    cancellationToken);
                var errors = new List<string>();
                await ValidateResidualGlEquityOpeningEvidenceAsync(batch, errors, cancellationToken);
                if (errors.Count > 0)
                    throw new InvalidOperationException($"Governed residual opening failed final validation: {string.Join("; ", errors)}");

                var result = await _postingEngine.PostAsync(
                    await BuildPostingRequestAsync(batch, cancellationToken),
                    cancellationToken);
                ApplyPostedBatchState(batch, result);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private void ApplyPostedBatchState(OpeningBalanceBatch batch, FinancePostingResultDto result)
    {
        var now = DateTime.UtcNow;
        batch.Status = StatusPosted;
        batch.JournalEntryId = result.JournalEntryId;
        batch.PostingEventId = result.PostingEventId;
        batch.PostedAt = now;
        batch.FailureReason = null;
        batch.UpdatedAt = now;
        batch.UpdatedBy = _currentUser.UserName ?? "system";
        batch.LastModifiedById = CurrentUserId();
    }

    private async Task<bool> HasDurablePostedEvidenceAsync(
        OpeningBalanceBatch batch,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(batch.Status, StatusPosted, StringComparison.OrdinalIgnoreCase) ||
            !batch.JournalEntryId.HasValue ||
            !batch.PostingEventId.HasValue ||
            !batch.PostedAt.HasValue)
        {
            return false;
        }

        var journalId = batch.JournalEntryId.Value;
        var postingEventId = batch.PostingEventId.Value;
        var journalExists = await _db.JournalEntries.AsNoTracking().AnyAsync(item =>
            item.TenantId == batch.TenantId &&
            item.Id == journalId &&
            !item.IsDeleted &&
            item.PostingStatus == StatusPosted,
            cancellationToken);
        if (!journalExists)
            return false;

        return await _db.FinancePostingEvents.AsNoTracking().AnyAsync(item =>
            item.TenantId == batch.TenantId &&
            item.Id == postingEventId &&
            !item.IsDeleted &&
            item.SourceModule == SourceModule &&
            item.SourceDocumentType == EntityType &&
            item.SourceDocumentId == batch.Id &&
            item.JournalEntryId == journalId &&
            item.PostingStatus == StatusPosted,
            cancellationToken);
    }

    private async Task<GovernedOpeningHeader> ResolveGovernedHeaderAsync(
        string? batchNumber,
        string? sourceReference,
        string? description,
        DateTime openingDate,
        Guid fiscalPeriodId,
        string? bookClassification,
        string? idempotencyKey,
        string defaultIdempotencyKey,
        CancellationToken cancellationToken)
    {
        if (openingDate == default || fiscalPeriodId == Guid.Empty)
            throw new InvalidOperationException("Opening date and fiscal period are required for a governed opening source.");
        var normalizedBatchNumber = NormalizeOptional(batchNumber) ?? string.Empty;
        if (normalizedBatchNumber.Length > 50)
            throw new InvalidOperationException("Opening balance batch number cannot exceed 50 characters.");
        var normalizedReference = NormalizeOptional(sourceReference)
            ?? throw new InvalidOperationException("A source evidence reference is required for a governed opening source.");
        if (normalizedReference.Length > 100)
            throw new InvalidOperationException("Opening source reference cannot exceed 100 characters.");
        var normalizedDescription = NormalizeOptional(description);
        if (normalizedDescription?.Length > 500)
            throw new InvalidOperationException("Opening description cannot exceed 500 characters.");
        var book = NormalizeBook(bookClassification);
        if (IsAllActiveBooks(book))
            throw new InvalidOperationException("Governed opening sources require one explicit book classification.");
        if (book.Length > 30)
            throw new InvalidOperationException("Opening book classification cannot exceed 30 characters.");

        var tenantId = TenantId;
        var period = await _db.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == fiscalPeriodId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("Opening balance fiscal period was not found for the current tenant.");
        if (!period.IsOpen || period.IsClosed || period.IsLocked)
            throw new InvalidOperationException("Governed opening sources require an open and unlocked fiscal period.");
        if (openingDate.Date < period.StartDate.Date || openingDate.Date > period.EndDate.Date)
            throw new InvalidOperationException("Opening date must fall within the selected fiscal period date range.");

        var settings = await _db.FinanceSettings.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("Finance Settings are required before governed opening sources can be prepared.");
        var normalizedIdempotencyKey = NormalizeOptional(idempotencyKey) ?? defaultIdempotencyKey;
        if (normalizedIdempotencyKey.Length > 120)
            throw new InvalidOperationException("Opening idempotency key cannot exceed 120 characters.");

        return new GovernedOpeningHeader(
            normalizedBatchNumber,
            normalizedReference,
            normalizedDescription,
            openingDate.Date,
            fiscalPeriodId,
            book,
            NormalizeCurrency(settings.BaseCurrency, "GHS"),
            normalizedIdempotencyKey);
    }

    private async Task<OpeningBalanceBatchDto> ExecuteGovernedCreationAsync(
        Guid tenantId,
        string logicalSource,
        string requestedKey,
        Func<CancellationToken, Task<OpeningBalanceBatchDto>> operation,
        Func<CancellationToken, Task<OpeningBalanceBatchDto?>> collisionReload,
        CancellationToken cancellationToken)
    {
        var lockResources = new[]
        {
            BuildGovernedCreationLockResource(tenantId, "Idempotency", requestedKey),
            BuildGovernedCreationLockResource(tenantId, "LogicalSource", logicalSource)
        }
        .Distinct(StringComparer.Ordinal)
        .OrderBy(resource => resource, StringComparer.Ordinal)
        .ToArray();
        var strategy = _db.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                try
                {
                    foreach (var resource in lockResources)
                        await AcquireGovernedTransactionLockAsync(resource, cancellationToken);

                    var result = await operation(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (DbUpdateException ex)
        {
            // The application locks serialize normal SQL Server traffic. A database uniqueness
            // collision can still surface during failover/retry or on a provider without that lock
            // primitive. Reload the durable winner and return it only when the complete payload
            // matches; otherwise convert the collision to a clear, fail-closed domain error.
            _db.ChangeTracker.Clear();
            var recovered = await collisionReload(cancellationToken);
            if (recovered != null)
                return recovered;
            throw new InvalidOperationException(
                "Concurrent governed opening creation conflicted with another durable source; reload the opening workspace before retrying.",
                ex);
        }
    }

    private static string BuildGovernedCreationLockResource(Guid tenantId, string scope, string value)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        return $"Finance:Opening:{tenantId:N}:{scope}:{digest}";
    }

    private async Task AcquireGovernedTransactionLockAsync(
        string resource,
        CancellationToken cancellationToken)
    {
        // EF's InMemory provider intentionally ignores transactions and cannot expose the
        // transaction required by the SQL Server application-lock implementation. Production
        // providers must still fail closed unless the serializable transaction is genuinely active.
        if (string.Equals(
                _db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            return;
        }

        if (!_unitOfWork.HasActiveTransaction)
        {
            throw new InvalidOperationException(
                "Governed opening serialization requires an active database transaction.");
        }

        await _unitOfWork.AcquireTransactionLockAsync(resource, cancellationToken);
    }

    private async Task<OpeningBalanceBatchDto?> ReloadBankCreationCollisionAsync(
        CreateBankAccountOpeningBalanceDto dto,
        decimal amount,
        string defaultKey,
        CancellationToken cancellationToken)
    {
        var header = await ResolveGovernedHeaderAsync(
            dto.BatchNumber,
            dto.SourceReference,
            dto.Description,
            dto.OpeningDate,
            dto.FiscalPeriodId,
            dto.BookClassification,
            dto.IdempotencyKey,
            defaultKey,
            cancellationToken);
        var idempotency = await ResolveGovernedIdempotencyAsync(header.IdempotencyKey, cancellationToken);
        if (idempotency.Existing != null)
        {
            EnsureBankOpeningRetryMatches(idempotency.Existing, dto.BankAccountId, amount, dto.ExchangeRateId, header);
            return await MapBatchAsync(idempotency.Existing.Id, cancellationToken)
                ?? throw new InvalidOperationException("The existing bank opening batch could not be reloaded.");
        }

        await ThrowIfConcurrentBatchNumberExistsAsync(header.BatchNumber, cancellationToken);
        if (await HasBlockingBankOpeningAsync(TenantId, dto.BankAccountId, excludeBatchId: null, cancellationToken))
            throw new InvalidOperationException("A governed opening batch already exists for this bank account.");
        return null;
    }

    private async Task<OpeningBalanceBatchDto?> ReloadResidualCreationCollisionAsync(
        CreateResidualGlEquityOpeningBalanceDto dto,
        decimal accruedAmount,
        decimal shareCapitalAmount,
        decimal retainedEarningsAmount,
        string defaultKey,
        CancellationToken cancellationToken)
    {
        var header = await ResolveGovernedHeaderAsync(
            dto.BatchNumber,
            dto.SourceReference,
            dto.Description,
            dto.OpeningDate,
            dto.FiscalPeriodId,
            dto.BookClassification,
            dto.IdempotencyKey,
            defaultKey,
            cancellationToken);
        var idempotency = await ResolveGovernedIdempotencyAsync(header.IdempotencyKey, cancellationToken);
        if (idempotency.Existing != null)
        {
            EnsureResidualOpeningRetryMatches(
                idempotency.Existing,
                dto.AccruedExpensesAccountId,
                accruedAmount,
                dto.ShareCapitalAccountId,
                shareCapitalAmount,
                retainedEarningsAmount,
                header);
            return await MapBatchAsync(idempotency.Existing.Id, cancellationToken)
                ?? throw new InvalidOperationException("The existing residual opening batch could not be reloaded.");
        }

        await ThrowIfConcurrentBatchNumberExistsAsync(header.BatchNumber, cancellationToken);
        if (await HasBlockingResidualOpeningAsync(
                TenantId,
                header.OpeningDate,
                header.BookClassification,
                excludeBatchId: null,
                cancellationToken))
        {
            throw new InvalidOperationException("A governed residual GL/equity opening already exists for this date and book.");
        }
        return null;
    }

    private async Task ThrowIfConcurrentBatchNumberExistsAsync(
        string batchNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(batchNumber))
            return;
        if (await _db.OpeningBalanceBatches.AsNoTracking().AnyAsync(batch =>
                batch.TenantId == TenantId &&
                batch.BatchNumber == batchNumber &&
                !batch.IsDeleted,
                cancellationToken))
        {
            throw new InvalidOperationException(
                $"Opening balance batch '{batchNumber}' already exists for this tenant.");
        }
    }

    private async Task<OpeningBalanceBatch?> FindExistingByIdempotencyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
        => await _db.OpeningBalanceBatches.AsNoTracking()
            .Include(batch => batch.Lines)
            .FirstOrDefaultAsync(batch =>
                batch.TenantId == TenantId &&
                batch.IdempotencyKey == idempotencyKey &&
                !batch.IsDeleted,
                cancellationToken);

    private async Task<GovernedIdempotencyResolution> ResolveGovernedIdempotencyAsync(
        string requestedKey,
        CancellationToken cancellationToken)
    {
        var effectiveKey = requestedKey;
        var visitedKeys = new HashSet<string>(StringComparer.Ordinal);
        while (visitedKeys.Add(effectiveKey))
        {
            var existing = await FindExistingByIdempotencyAsync(effectiveKey, cancellationToken);
            if (existing == null)
                return new GovernedIdempotencyResolution(null, effectiveKey);

            if (!string.Equals(existing.Status, StatusRejected, StringComparison.OrdinalIgnoreCase))
                return new GovernedIdempotencyResolution(existing, effectiveKey);

            if (!await IsTerminalRejectedWithoutPostingEvidenceAsync(existing, cancellationToken))
            {
                throw new InvalidOperationException(
                    "A rejected governed opening batch retains posting evidence and cannot be replaced automatically.");
            }

            // Preserve the terminal rejected record and its original client key. A deterministic,
            // length-bounded successor key lets retries through either the original key or a returned
            // physical replacement key converge without deleting or rewriting approval history.
            effectiveKey = BuildRejectedReplacementIdempotencyKey(effectiveKey, existing.Id);
        }

        throw new InvalidOperationException("The governed opening replacement idempotency chain is invalid.");
    }

    private async Task<bool> IsTerminalRejectedWithoutPostingEvidenceAsync(
        OpeningBalanceBatch batch,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(batch.Status, StatusRejected, StringComparison.OrdinalIgnoreCase) ||
            batch.JournalEntryId.HasValue ||
            batch.PostingEventId.HasValue ||
            batch.PostedAt.HasValue)
        {
            return false;
        }

        return !await _db.FinancePostingEvents.AsNoTracking().AnyAsync(postingEvent =>
            postingEvent.TenantId == batch.TenantId &&
            postingEvent.SourceModule == SourceModule &&
            postingEvent.SourceDocumentType == EntityType &&
            postingEvent.SourceDocumentId == batch.Id &&
            postingEvent.PostingStatus == StatusPosted &&
            !postingEvent.IsDeleted,
            cancellationToken);
    }

    private static string BuildRejectedReplacementIdempotencyKey(string previousKey, Guid rejectedBatchId)
    {
        var suffix = $":R:{rejectedBatchId:N}";
        var prefixLength = 120 - suffix.Length;
        var prefix = previousKey[..Math.Min(previousKey.Length, prefixLength)];
        return prefix + suffix;
    }

    private static decimal RequirePositiveOpeningAmount(decimal value, string label)
    {
        var rounded = RoundMoney(value);
        if (rounded <= 0m)
            throw new InvalidOperationException($"{label} must be positive.");
        if (value != rounded)
            throw new InvalidOperationException($"{label} cannot contain more than two decimal places.");
        return rounded;
    }

    private static CreateOpeningBalanceLineDto GovernedResidualLine(
        Guid accountId,
        decimal debit,
        decimal credit,
        string marker,
        string notes,
        GovernedOpeningHeader header)
        => new()
        {
            AccountId = accountId,
            DebitAmount = debit,
            CreditAmount = credit,
            TransactionDebitAmount = debit,
            TransactionCreditAmount = credit,
            TransactionCurrencyCode = header.FunctionalCurrency,
            FunctionalCurrencyCode = header.FunctionalCurrency,
            CounterpartyType = marker,
            CounterpartyId = accountId,
            SourceReference = header.SourceReference,
            Notes = notes
        };

    private static void EnsureBankOpeningRetryMatches(
        OpeningBalanceBatch existing,
        Guid bankAccountId,
        decimal amount,
        Guid? exchangeRateId,
        GovernedOpeningHeader header)
    {
        var primary = existing.Lines.SingleOrDefault(IsBankAccountOpeningPrimaryLine);
        if (primary == null ||
            existing.Lines.Count != 2 ||
            primary.BankAccountId != bankAccountId ||
            primary.CounterpartyId != bankAccountId ||
            primary.TransactionDebitAmount != amount ||
            primary.CreditAmount != 0m ||
            primary.ExchangeRateId != exchangeRateId ||
            existing.OpeningDate.Date != header.OpeningDate ||
            existing.FiscalPeriodId != header.FiscalPeriodId ||
            !string.Equals(existing.BookClassification, header.BookClassification, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(existing.SourceReference, header.SourceReference, StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(header.BatchNumber) && !string.Equals(existing.BatchNumber, header.BatchNumber, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The bank opening idempotency key is already bound to a different payload.");
        }
    }

    private static void EnsureResidualOpeningRetryMatches(
        OpeningBalanceBatch existing,
        Guid accruedAccountId,
        decimal accruedAmount,
        Guid shareCapitalAccountId,
        decimal shareCapitalAmount,
        decimal retainedEarningsAmount,
        GovernedOpeningHeader header)
    {
        var accrued = existing.Lines.SingleOrDefault(line => line.CounterpartyType == ResidualAccruedOpening);
        var share = existing.Lines.SingleOrDefault(line => line.CounterpartyType == ResidualShareCapitalOpening);
        var retained = existing.Lines.SingleOrDefault(line => line.CounterpartyType == ResidualRetainedEarnings);
        if (existing.Lines.Count != 4 || accrued?.AccountId != accruedAccountId || accrued.CreditAmount != accruedAmount ||
            share?.AccountId != shareCapitalAccountId || share.CreditAmount != shareCapitalAmount ||
            retained?.CreditAmount != retainedEarningsAmount ||
            existing.OpeningDate.Date != header.OpeningDate || existing.FiscalPeriodId != header.FiscalPeriodId ||
            !string.Equals(existing.BookClassification, header.BookClassification, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(existing.SourceReference, header.SourceReference, StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(header.BatchNumber) && !string.Equals(existing.BatchNumber, header.BatchNumber, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The residual opening idempotency key is already bound to a different payload.");
        }
    }

    private async Task<BankOpeningContext> ResolveBankOpeningContextAsync(
        Guid tenantId,
        Guid bankAccountId,
        DateTime openingDate,
        Guid? exchangeRateId,
        bool allowDerivedRate,
        Guid? excludeBatchId,
        bool requireUnusedState,
        CancellationToken cancellationToken)
    {
        var settings = await _db.FinanceSettings.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("Finance Settings are required before a governed bank opening can be prepared.");
        var bank = await _db.BankAccounts.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == bankAccountId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("Bank account was not found for the current tenant.");
        var inspection = await InspectBankOpeningContextAsync(
            tenantId,
            bank,
            settings,
            openingDate,
            exchangeRateId,
            allowDerivedRate,
            excludeBatchId,
            requireUnusedState,
            cancellationToken);
        if (inspection.Errors.Count > 0)
            throw new InvalidOperationException($"Governed bank opening is unavailable: {string.Join("; ", inspection.Errors)}");
        return new BankOpeningContext(
            bank,
            inspection.BankGl!,
            inspection.MigrationClearing!,
            settings,
            inspection.BankCurrency,
            inspection.ExchangeRate!);
    }

    private async Task<BankOpeningInspection> InspectBankOpeningContextAsync(
        Guid tenantId,
        BankAccount bank,
        FinanceSettings settings,
        DateTime openingDate,
        Guid? exchangeRateId,
        bool allowDerivedRate,
        Guid? excludeBatchId,
        bool requireUnusedState,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
        var bankCurrency = NormalizeCurrency(bank.Currency, functionalCurrency);
        if (!bank.IsActive)
            errors.Add("Bank account is inactive.");

        Account? bankGl = null;
        if (!bank.GLAccountId.HasValue)
        {
            errors.Add("Bank account has no mapped GL account.");
        }
        else
        {
            bankGl = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(account =>
                account.TenantId == tenantId && account.Id == bank.GLAccountId.Value && !account.IsDeleted,
                cancellationToken);
            errors.AddRange(ValidateDerivedOpeningAccount(
                bankGl,
                bankCurrency,
                "Bank GL",
                AccountType.Asset,
                requireDirectPosting: false,
                effectiveDate: openingDate));
            if (bankGl != null && !bankGl.AllowDirectPosting && !bankGl.IsControlAccount)
                errors.Add("Bank GL must be a protected control account or allow direct posting.");
        }

        Account? migrationClearing = null;
        if (!settings.MigrationClearingAccountId.HasValue)
        {
            errors.Add("Migration Clearing Account is not configured in Finance Settings.");
        }
        else
        {
            migrationClearing = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(account =>
                account.TenantId == tenantId && account.Id == settings.MigrationClearingAccountId.Value && !account.IsDeleted,
                cancellationToken);
            errors.AddRange(ValidateDerivedOpeningAccount(
                migrationClearing,
                functionalCurrency,
                "Migration clearing",
                expectedType: null,
                requireDirectPosting: true,
                effectiveDate: openingDate));
        }

        if (bankGl != null && migrationClearing != null && bankGl.Id == migrationClearing.Id)
            errors.Add("Bank GL and migration clearing must be different accounts.");

        BankExchangeRateSnapshot? exchangeRate = null;
        if (bankGl != null)
        {
            try
            {
                exchangeRate = await ResolveBankOpeningExchangeRateAsync(
                    tenantId,
                    bankGl,
                    settings,
                    bankCurrency,
                    functionalCurrency,
                    openingDate,
                    exchangeRateId,
                    allowDerivedRate,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                errors.Add(ex.Message);
            }
        }
        if (bank.GLAccountId.HasValue && await _db.BankAccounts.AsNoTracking().AnyAsync(other =>
                other.TenantId == tenantId &&
                other.Id != bank.Id &&
                !other.IsDeleted &&
                other.IsActive &&
                other.GLAccountId == bank.GLAccountId,
                cancellationToken))
        {
            errors.Add("Bank GL is shared by more than one active bank master.");
        }

        var duplicateOpening = await HasBlockingBankOpeningAsync(
            tenantId,
            bank.Id,
            excludeBatchId,
            cancellationToken);
        if (duplicateOpening)
            errors.Add("A governed opening batch already exists for this bank account.");

        if (requireUnusedState)
        {
            if (bank.OpeningBalance != 0m)
                errors.Add("Bank master OpeningBalance must remain zero because the governed journal is the opening ledger fact.");
            if (bank.CurrentBalance != 0m || bank.AvailableBalance != 0m)
                errors.Add("Bank current and available balances must both be zero before governed opening.");
            if (await _db.Set<CashTransaction>().AsNoTracking().AnyAsync(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    (item.BankAccountId == bank.Id || item.ToBankAccountId == bank.Id),
                    cancellationToken))
                errors.Add("Bank account already has cash/bank transactions.");
            if (await _db.Set<BankStatement>().AsNoTracking().AnyAsync(item =>
                    item.TenantId == tenantId && !item.IsDeleted && item.BankAccountId == bank.Id,
                    cancellationToken))
                errors.Add("Bank account already has statement evidence.");
            if (await _db.Set<BankReconciliation>().AsNoTracking().AnyAsync(item =>
                    item.TenantId == tenantId && !item.IsDeleted && item.BankAccountId == bank.Id,
                    cancellationToken))
                errors.Add("Bank account already has reconciliation evidence.");
            if (bank.GLAccountId.HasValue && await _db.AccountTransactions.AsNoTracking().AnyAsync(item =>
                    item.TenantId == tenantId &&
                    item.AccountId == bank.GLAccountId.Value &&
                    !item.IsDeleted &&
                    item.PostingStatus == "Posted",
                    cancellationToken))
                errors.Add("Mapped bank GL already has posted movement.");
        }

        return new BankOpeningInspection(bankGl, migrationClearing, bankCurrency, exchangeRate, errors);
    }

    private async Task<BankExchangeRateSnapshot> ResolveBankOpeningExchangeRateAsync(
        Guid tenantId,
        Account bankGl,
        FinanceSettings settings,
        string bankCurrency,
        string functionalCurrency,
        DateTime openingDate,
        Guid? requestedRateId,
        bool allowDerivedRate,
        CancellationToken cancellationToken)
    {
        if (string.Equals(bankCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            if (requestedRateId.HasValue)
                throw new InvalidOperationException("Functional-currency bank openings must not supply exchange-rate evidence.");
            return new BankExchangeRateSnapshot(null, 1m, null, ExchangeRateType.Daily, ExchangeRateQuoteSide.Mid, "Functional currency");
        }

        var rateType = ExchangeRateType.Daily;
        var quoteSide = settings.DirectionalExchangeRatePolicyEnabled
            ? settings.DefaultTransactionQuoteSide
            : ExchangeRateQuoteSide.Mid;
        var currencyLink = await _db.AccountCurrencyLinks.AsNoTracking().FirstOrDefaultAsync(link =>
            link.TenantId == tenantId &&
            link.AccountId == bankGl.Id &&
            link.LinkedCurrencyCode == bankCurrency &&
            !link.IsDeleted &&
            link.IsActive &&
            link.EffectiveDate.Date <= openingDate.Date &&
            (!link.EffectiveEndDate.HasValue || link.EffectiveEndDate.Value.Date >= openingDate.Date),
            cancellationToken);
        if (settings.DirectionalExchangeRatePolicyEnabled && currencyLink != null)
        {
            rateType = ParseOpeningExchangeRateType(currencyLink.TransactionRateType);
            quoteSide = currencyLink.TransactionQuoteSide;
        }

        var query = _db.ExchangeRates.AsNoTracking().Where(rate =>
            rate.TenantId == tenantId &&
            !rate.IsDeleted &&
            rate.BaseCurrencyCode == functionalCurrency &&
            rate.TargetCurrencyCode == bankCurrency &&
            rate.RateType == rateType &&
            rate.QuoteSide == quoteSide &&
            rate.IsActive &&
            rate.Rate > 0m &&
            (rate.ApprovalStatus == RateApprovalStatus.Approved || rate.ApprovalStatus == RateApprovalStatus.AutoApproved) &&
            rate.EffectiveDate.Date <= openingDate.Date &&
            (!rate.EndDate.HasValue || rate.EndDate.Value.Date >= openingDate.Date));
        if (!requestedRateId.HasValue && !allowDerivedRate)
            throw new InvalidOperationException("Foreign-currency bank openings require the approved exchange rate selected by the dated options contract.");

        ExchangeRate? approvedRate;
        if (requestedRateId.HasValue)
            approvedRate = await query.FirstOrDefaultAsync(rate => rate.Id == requestedRateId.Value, cancellationToken);
        else
            approvedRate = await query
                .OrderByDescending(rate => rate.EffectiveDate)
                .ThenByDescending(rate => rate.Priority)
                .ThenByDescending(rate => rate.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        if (approvedRate == null)
        {
            throw new InvalidOperationException(
                $"No active approved {rateType} {quoteSide} exchange rate exists for {bankCurrency} to {functionalCurrency} on {openingDate:yyyy-MM-dd}.");
        }

        return new BankExchangeRateSnapshot(
            approvedRate.Id,
            RoundRate(approvedRate.InverseRate),
            approvedRate.EffectiveDate.Date,
            approvedRate.RateType,
            approvedRate.QuoteSide,
            approvedRate.RateSource);
    }

    private static ExchangeRateType ParseOpeningExchangeRateType(string? value)
    {
        var normalized = value?.Trim().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
        return Enum.TryParse<ExchangeRateType>(normalized, ignoreCase: true, out var rateType)
            ? rateType
            : throw new InvalidOperationException("Bank GL transaction exchange-rate type is invalid.");
    }

    private async Task<ResidualOpeningContext> ResolveResidualOpeningContextAsync(
        Guid tenantId,
        Guid accruedExpensesAccountId,
        Guid shareCapitalAccountId,
        DateTime openingDate,
        string functionalCurrency,
        Guid? excludeBatchId,
        string bookClassification,
        CancellationToken cancellationToken)
    {
        var settings = await _db.FinanceSettings.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("Finance Settings are required before a governed residual opening can be prepared.");
        var migrationClearingId = settings.MigrationClearingAccountId
            ?? throw new InvalidOperationException("Migration Clearing Account is not configured in Finance Settings.");
        var retainedEarningsId = settings.RetainedEarningsAccountId
            ?? throw new InvalidOperationException("Retained Earnings Account is not configured in Finance Settings.");
        var requestedIds = new[]
        {
            accruedExpensesAccountId,
            shareCapitalAccountId,
            migrationClearingId,
            retainedEarningsId
        };
        if (requestedIds.Distinct().Count() != requestedIds.Length)
            throw new InvalidOperationException("Residual opening accrued expenses, share capital, retained earnings, and migration clearing must use distinct accounts.");
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(account => account.TenantId == tenantId && requestedIds.Contains(account.Id) && !account.IsDeleted)
            .ToDictionaryAsync(account => account.Id, cancellationToken);
        if (accounts.Count != requestedIds.Length)
            throw new InvalidOperationException("One or more governed residual opening accounts are unavailable for the current tenant.");
        var accrued = accounts[accruedExpensesAccountId];
        var shareCapital = accounts[shareCapitalAccountId];
        var migrationClearing = accounts[migrationClearingId];
        var retainedEarnings = accounts[retainedEarningsId];
        var errors = new List<string>();
        errors.AddRange(ValidateDerivedOpeningAccount(accrued, functionalCurrency, "Accrued expenses", AccountType.Liability, true, openingDate));
        errors.AddRange(ValidateDerivedOpeningAccount(shareCapital, functionalCurrency, "Share capital", AccountType.Equity, true, openingDate));
        errors.AddRange(ValidateDerivedOpeningAccount(migrationClearing, functionalCurrency, "Migration clearing", null, true, openingDate));
        errors.AddRange(ValidateDerivedOpeningAccount(retainedEarnings, functionalCurrency, "Retained earnings", AccountType.Equity, false, openingDate));
        errors.AddRange(await GetFreeFormOpeningLineErrorsAsync(
            tenantId,
            new[]
            {
                new FreeFormOpeningLineCandidate(1, accrued.Id, null),
                new FreeFormOpeningLineCandidate(2, shareCapital.Id, null)
            },
            cancellationToken));
        if (accrued.IsControlAccount || shareCapital.IsControlAccount)
            errors.Add("Operator-selected residual accounts cannot be control accounts.");

        var duplicate = await HasBlockingResidualOpeningAsync(
            tenantId,
            openingDate,
            bookClassification,
            excludeBatchId,
            cancellationToken);
        if (duplicate)
            errors.Add("A governed residual GL/equity opening already exists for this date and book.");
        if (errors.Count > 0)
            throw new InvalidOperationException($"Governed residual opening is unavailable: {string.Join("; ", errors.Distinct(StringComparer.Ordinal))}");
        return new ResidualOpeningContext(accrued, shareCapital, retainedEarnings, migrationClearing, settings);
    }

    private Task<bool> HasBlockingBankOpeningAsync(
        Guid tenantId,
        Guid bankAccountId,
        Guid? excludeBatchId,
        CancellationToken cancellationToken)
        => BlockingGovernedOpeningBatches(tenantId).AnyAsync(batch =>
            (!excludeBatchId.HasValue || batch.Id != excludeBatchId.Value) &&
            batch.Lines.Any(line =>
                line.TenantId == tenantId &&
                !line.IsDeleted &&
                line.BankAccountId == bankAccountId &&
                line.CounterpartyType == BankAccountOpening),
            cancellationToken);

    private Task<bool> HasBlockingResidualOpeningAsync(
        Guid tenantId,
        DateTime openingDate,
        string bookClassification,
        Guid? excludeBatchId,
        CancellationToken cancellationToken)
        => BlockingGovernedOpeningBatches(tenantId).AnyAsync(batch =>
            (!excludeBatchId.HasValue || batch.Id != excludeBatchId.Value) &&
            batch.OpeningDate == openingDate.Date &&
            batch.BookClassification == bookClassification &&
            batch.Lines.Any(line =>
                line.TenantId == tenantId &&
                !line.IsDeleted &&
                line.CounterpartyType == ResidualMigrationClearing),
            cancellationToken);

    private IQueryable<OpeningBalanceBatch> BlockingGovernedOpeningBatches(Guid tenantId)
        => _db.OpeningBalanceBatches.AsNoTracking().Where(batch =>
            batch.TenantId == tenantId &&
            !batch.IsDeleted &&
            !(batch.Status == StatusRejected &&
              batch.JournalEntryId == null &&
              batch.PostingEventId == null &&
              batch.PostedAt == null &&
              !_db.FinancePostingEvents.Any(postingEvent =>
                  postingEvent.TenantId == tenantId &&
                  postingEvent.SourceModule == SourceModule &&
                  postingEvent.SourceDocumentType == EntityType &&
                  postingEvent.SourceDocumentId == batch.Id &&
                  postingEvent.PostingStatus == StatusPosted &&
                  !postingEvent.IsDeleted)));

    private async Task EnsureResidualClearsMigrationBalanceAsync(
        Guid tenantId,
        Guid migrationClearingAccountId,
        DateTime openingDate,
        string bookClassification,
        decimal residualDebit,
        Guid? excludeBatchId,
        CancellationToken cancellationToken)
    {
        var ownJournalIds = new HashSet<Guid>();
        if (excludeBatchId.HasValue)
        {
            var eventJournalIds = await _db.FinancePostingEvents.AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.SourceModule == SourceModule &&
                    item.SourceDocumentType == EntityType &&
                    item.SourceDocumentId == excludeBatchId.Value &&
                    item.PostingStatus == StatusPosted &&
                    item.JournalEntryId.HasValue &&
                    !item.IsDeleted)
                .Select(item => item.JournalEntryId!.Value)
                .ToListAsync(cancellationToken);
            ownJournalIds.UnionWith(eventJournalIds);
        }

        var cutoffExclusive = openingDate.Date.AddDays(1);
        var transactions = _db.AccountTransactions.AsNoTracking().Where(item =>
            item.TenantId == tenantId &&
            item.AccountId == migrationClearingAccountId &&
            item.TransactionDate < cutoffExclusive &&
            item.BookClassification == bookClassification &&
            item.PostingStatus == StatusPosted &&
            !item.IsDeleted);
        if (ownJournalIds.Count > 0)
            transactions = transactions.Where(item => !ownJournalIds.Contains(item.JournalEntryId));

        // Finance reversals keep the original posted line and add an equal/opposite posted line.
        // Sum both; filtering IsReversed originals would manufacture the reversal's opposite balance.
        var availableCredit = RoundMoney(await transactions.SumAsync(
            item => item.CreditAmount - item.DebitAmount,
            cancellationToken));
        var requiredDebit = RoundMoney(residualDebit);
        if (availableCredit <= 0m)
        {
            throw new InvalidOperationException(
                "Governed residual opening is blocked until upstream opening sources leave a positive posted Migration Clearing credit balance for this cutover date and book.");
        }
        if (availableCredit != requiredDebit)
        {
            throw new InvalidOperationException(
                $"Governed residual opening debit {requiredDebit:0.00} must exactly clear the posted Migration Clearing credit balance {availableCredit:0.00} for this cutover date and book.");
        }
    }

    private static IReadOnlyList<string> ValidateDerivedOpeningAccount(
        Account? account,
        string functionalCurrency,
        string label,
        AccountType? expectedType,
        bool requireDirectPosting,
        DateTime? effectiveDate = null)
    {
        var errors = new List<string>();
        if (account == null)
        {
            errors.Add($"{label} account is missing or belongs to another tenant.");
            return errors;
        }
        var date = (effectiveDate ?? DateTime.UtcNow).Date;
        if (account.IsDeleted || account.Status != AccountStatus.Active ||
            (account.EffectiveDate.HasValue && account.EffectiveDate.Value.Date > date) ||
            (account.ExpirationDate.HasValue && account.ExpirationDate.Value.Date < date))
            errors.Add($"{label} account is not active and effective on {date:yyyy-MM-dd}.");
        if (expectedType.HasValue && account.AccountType != expectedType.Value)
            errors.Add($"{label} account must be {expectedType.Value}.");
        if (requireDirectPosting && !account.AllowDirectPosting)
            errors.Add($"{label} account must allow direct posting.");
        if (!account.IsMultiCurrency && !string.Equals(account.CurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase))
            errors.Add($"{label} account must accept functional currency {functionalCurrency}.");
        return errors;
    }

    private async Task<HashSet<Guid>> GetProtectedResidualAccountIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> requestedIds,
        FinanceSettings settings,
        CancellationToken cancellationToken)
    {
        var protectedIds = new HashSet<Guid>();
        foreach (var property in typeof(FinanceSettings).GetProperties()
                     .Where(property => property.Name.Contains("Account", StringComparison.Ordinal) &&
                         property.Name.EndsWith("Id", StringComparison.Ordinal) &&
                         property.Name is not nameof(FinanceSettings.DefaultBankAccountId)))
        {
            if (property.GetValue(settings) is Guid accountId && requestedIds.Contains(accountId))
                protectedIds.Add(accountId);
        }
        protectedIds.UnionWith(await _db.BankAccounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.GLAccountId.HasValue && requestedIds.Contains(item.GLAccountId.Value))
            .Select(item => item.GLAccountId!.Value)
            .ToListAsync(cancellationToken));
        protectedIds.UnionWith(await _db.LiquidityAccounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && requestedIds.Contains(item.GLAccountId))
            .Select(item => item.GLAccountId)
            .ToListAsync(cancellationToken));
        var taxes = await _db.Taxes.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new { item.TaxPayableAccountId, item.TaxReceivableAccountId })
            .ToListAsync(cancellationToken);
        foreach (var tax in taxes)
        {
            if (tax.TaxPayableAccountId.HasValue && requestedIds.Contains(tax.TaxPayableAccountId.Value))
                protectedIds.Add(tax.TaxPayableAccountId.Value);
            if (tax.TaxReceivableAccountId.HasValue && requestedIds.Contains(tax.TaxReceivableAccountId.Value))
                protectedIds.Add(tax.TaxReceivableAccountId.Value);
        }
        var categories = await _db.FixedAssetCategories.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var category in categories)
        {
            foreach (var property in typeof(FixedAssetCategory).GetProperties()
                         .Where(property => property.Name.EndsWith("AccountId", StringComparison.Ordinal)))
            {
                if (property.GetValue(category) is Guid accountId && requestedIds.Contains(accountId))
                    protectedIds.Add(accountId);
            }
        }
        return protectedIds;
    }

    private static GovernedOpeningDerivedAccountDto BuildDerivedOpeningAccountOption(
        Account? account,
        string postingDirection,
        IEnumerable<string> errors)
    {
        var blockers = errors.Distinct(StringComparer.Ordinal).ToList();
        return new GovernedOpeningDerivedAccountDto
        {
            AccountId = account?.Id ?? Guid.Empty,
            AccountCode = account?.AccountCode ?? string.Empty,
            AccountName = account?.AccountName ?? string.Empty,
            PostingDirection = postingDirection,
            IsEligible = blockers.Count == 0,
            Blockers = blockers
        };
    }

    private static ResidualOpeningAccountOptionDto MapResidualOption(Account account)
        => new()
        {
            Id = account.Id,
            AccountCode = account.AccountCode,
            AccountName = account.AccountName,
            AccountType = account.AccountType.ToString(),
            PostingDirection = "Credit"
        };

    public async Task<GovernedOpeningBalanceOptionsDto> GetGovernedOptionsAsync(
        GovernedOpeningBalanceOptionsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        if (dto.OpeningDate == default || dto.FiscalPeriodId == Guid.Empty)
            throw new InvalidOperationException("Opening date and fiscal period are required for governed opening options.");
        var openingDate = dto.OpeningDate.Date;
        var book = NormalizeBook(dto.BookClassification);
        if (IsAllActiveBooks(book))
            throw new InvalidOperationException("Governed opening options require one explicit book classification.");
        if (book.Length > 30)
            throw new InvalidOperationException("Opening book classification cannot exceed 30 characters.");
        var period = await _db.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == dto.FiscalPeriodId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("Opening balance fiscal period was not found for the current tenant.");
        if (!period.IsOpen || period.IsClosed || period.IsLocked)
            throw new InvalidOperationException("Governed opening options require an open and unlocked fiscal period.");
        if (openingDate < period.StartDate.Date || openingDate > period.EndDate.Date)
            throw new InvalidOperationException("Opening date must fall within the selected fiscal period date range.");

        var settings = await _db.FinanceSettings.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        var functionalCurrency = NormalizeCurrency(settings?.BaseCurrency, "GHS");
        var blockers = new List<string>();
        if (settings == null)
            blockers.Add("Finance Settings are required before governed opening sources can be prepared.");

        var bankMasters = await _db.BankAccounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .OrderBy(item => item.AccountNumber)
            .ToListAsync(cancellationToken);
        var bankOptions = new List<BankAccountOpeningOptionDto>(bankMasters.Count);
        foreach (var bank in bankMasters)
        {
            BankOpeningInspection? inspection = null;
            if (settings != null)
            {
                inspection = await InspectBankOpeningContextAsync(
                    tenantId,
                    bank,
                    settings,
                    openingDate,
                    exchangeRateId: null,
                    allowDerivedRate: true,
                    excludeBatchId: null,
                    requireUnusedState: true,
                    cancellationToken: cancellationToken);
            }
            var optionBlockers = inspection?.Errors.ToList()
                ?? new List<string> { "Finance Settings are not configured." };
            var gl = bank.GLAccountId.HasValue
                ? await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.Id == bank.GLAccountId.Value && !item.IsDeleted,
                    cancellationToken)
                : null;
            bankOptions.Add(new BankAccountOpeningOptionDto
            {
                Id = bank.Id,
                AccountNumber = bank.AccountNumber,
                AccountName = bank.AccountName,
                BankName = bank.BankName,
                CurrencyCode = NormalizeCurrency(bank.Currency, functionalCurrency),
                GlAccountId = gl?.Id,
                GlAccountCode = gl?.AccountCode,
                GlAccountName = gl?.AccountName,
                PostingDirection = "Debit",
                ExchangeRateId = inspection?.ExchangeRate?.ExchangeRateId,
                ExchangeRate = inspection?.ExchangeRate?.Rate ?? 1m,
                ExchangeRateDate = inspection?.ExchangeRate?.EffectiveDate,
                ExchangeRateType = inspection?.ExchangeRate?.RateType.ToString(),
                ExchangeRateQuoteSide = inspection?.ExchangeRate?.QuoteSide.ToString(),
                ExchangeRateSource = inspection?.ExchangeRate?.Source,
                IsEligible = optionBlockers.Count == 0,
                Blockers = optionBlockers
            });
        }

        var accountCandidates = await _db.Accounts.AsNoTracking()
            .Where(account =>
                account.TenantId == tenantId &&
                !account.IsDeleted &&
                account.Status == AccountStatus.Active &&
                account.AllowDirectPosting &&
                !account.IsControlAccount &&
                (account.AccountType == AccountType.Liability || account.AccountType == AccountType.Equity))
            .OrderBy(account => account.AccountCode)
            .ToListAsync(cancellationToken);
        var protectedIds = settings == null
            ? accountCandidates.Select(account => account.Id).ToHashSet()
            : await GetProtectedResidualAccountIdsAsync(
                tenantId,
                accountCandidates.Select(account => account.Id).ToArray(),
                settings,
                cancellationToken);
        var safeCandidates = accountCandidates
            .Where(account =>
                !protectedIds.Contains(account.Id) &&
                (!account.EffectiveDate.HasValue || account.EffectiveDate.Value.Date <= openingDate) &&
                (!account.ExpirationDate.HasValue || account.ExpirationDate.Value.Date >= openingDate) &&
                (account.IsMultiCurrency || string.Equals(account.CurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var migrationClearing = settings?.MigrationClearingAccountId is Guid clearingId
            ? await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(account =>
                account.TenantId == tenantId && account.Id == clearingId && !account.IsDeleted,
                cancellationToken)
            : null;
        var retainedEarnings = settings?.RetainedEarningsAccountId is Guid retainedId
            ? await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(account =>
                account.TenantId == tenantId && account.Id == retainedId && !account.IsDeleted,
                cancellationToken)
            : null;
        var clearingOption = BuildDerivedOpeningAccountOption(
            migrationClearing,
            "Debit",
            settings?.MigrationClearingAccountId.HasValue == true
                ? ValidateDerivedOpeningAccount(migrationClearing, functionalCurrency, "Migration clearing", null, requireDirectPosting: true, openingDate)
                : new[] { "Migration Clearing Account is not configured in Finance Settings." });
        var retainedOption = BuildDerivedOpeningAccountOption(
            retainedEarnings,
            "Credit",
            settings?.RetainedEarningsAccountId.HasValue == true
                ? ValidateDerivedOpeningAccount(retainedEarnings, functionalCurrency, "Retained earnings", AccountType.Equity, requireDirectPosting: false, openingDate)
                : new[] { "Retained Earnings Account is not configured in Finance Settings." });
        blockers.AddRange(clearingOption.Blockers);
        blockers.AddRange(retainedOption.Blockers);

        return new GovernedOpeningBalanceOptionsDto
        {
            FunctionalCurrencyCode = functionalCurrency,
            BankAccounts = bankOptions,
            AccruedExpensesAccounts = safeCandidates
                .Where(account => account.AccountType == AccountType.Liability)
                .Select(MapResidualOption)
                .ToList(),
            ShareCapitalAccounts = safeCandidates
                .Where(account => account.AccountType == AccountType.Equity)
                .Select(MapResidualOption)
                .ToList(),
            MigrationClearingAccount = clearingOption,
            RetainedEarningsAccount = retainedOption,
            Blockers = blockers.Distinct(StringComparer.Ordinal).ToList()
        };
    }

    private async Task EnsureFreeFormOpeningLinesAllowedAsync(
        Guid tenantId,
        IReadOnlyList<CreateOpeningBalanceLineDto> lines,
        CancellationToken cancellationToken)
    {
        var errors = await GetFreeFormOpeningLineErrorsAsync(
            tenantId,
            lines.Select((line, index) => new FreeFormOpeningLineCandidate(
                index + 1,
                line.AccountId,
                line.CounterpartyType,
                line.BankAccountId)),
            cancellationToken);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Free-form opening-balance lines failed protected-account validation: {string.Join("; ", errors)}");
        }
    }

    private async Task<IReadOnlyList<string>> GetFreeFormOpeningLineErrorsAsync(
        Guid tenantId,
        IEnumerable<FreeFormOpeningLineCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var lines = candidates.ToList();
        var errors = new List<string>();
        var requestedAccountIds = lines.Select(line => line.AccountId).Where(id => id != Guid.Empty).Distinct().ToArray();
        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && requestedAccountIds.Contains(account.Id) && !account.IsDeleted)
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        foreach (var line in lines)
        {
            if (line.AccountId == Guid.Empty || !accounts.ContainsKey(line.AccountId))
            {
                // Cross-tenant and unknown IDs deliberately produce the same message. Opening
                // validation must be tenant-scoped without disclosing another tenant's chart.
                errors.Add($"Line {line.LineNumber}: account selection is unavailable for the current tenant.");
            }

            if (line.BankAccountId.HasValue)
            {
                errors.Add($"Line {line.LineNumber}: bank-account evidence is reserved for the controlled cash/bank opening process.");
            }

            if (line.CounterpartyType is FixedAssetOpeningCost or FixedAssetOpeningDepreciation or
                SupplierAdvanceOpening or CustomerAdvanceOpening or ApWithholdingOpening or ArWithholdingOpening or
                BankAccountOpening or BankOpeningClearing or ResidualAccruedOpening or ResidualShareCapitalOpening or
                ResidualRetainedEarnings or ResidualMigrationClearing)
            {
                // These values are server-owned evidence markers. Accepting them from the public
                // DTO would let a free-form request pretend it came from a canonical source.
                errors.Add($"Line {line.LineNumber}: '{line.CounterpartyType}' is a reserved server-derived opening source.");
            }
        }

        var settings = await _db.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (settings == null)
        {
            errors.Add("Finance Settings are required before free-form opening-balance accounts can be classified safely.");
            return errors.Distinct(StringComparer.Ordinal).ToList();
        }

        var restrictions = new Dictionary<Guid, HashSet<string>>();
        void Restrict(Guid? accountId, string reason)
        {
            if (!accountId.HasValue || accountId.Value == Guid.Empty || !requestedAccountIds.Contains(accountId.Value))
                return;
            if (!restrictions.TryGetValue(accountId.Value, out var reasons))
            {
                reasons = new HashSet<string>(StringComparer.Ordinal);
                restrictions[accountId.Value] = reasons;
            }
            reasons.Add(reason);
        }

        foreach (var account in accounts.Values)
        {
            if (account.IsControlAccount)
                Restrict(account.Id, "the chart marks it as a control account");
            if (!account.AllowDirectPosting)
                Restrict(account.Id, "the chart disallows direct posting");
        }

        // Finance Settings is the authoritative tenant mapping catalogue. Reflection is deliberate:
        // a future *Account*Id mapping becomes protected automatically rather than opening a quiet
        // bypass until this validator is manually updated. DefaultBankAccountId is a bank-master ID,
        // not a GL account. MigrationClearingAccountId remains the one intentional free-form bridge.
        foreach (var property in typeof(FinanceSettings).GetProperties()
                     .Where(property => property.Name.Contains("Account", StringComparison.Ordinal)
                         && property.Name.EndsWith("Id", StringComparison.Ordinal)
                         && property.Name is not nameof(FinanceSettings.DefaultBankAccountId)
                         && property.Name is not nameof(FinanceSettings.MigrationClearingAccountId)))
        {
            var value = property.GetValue(settings);
            var accountId = value switch
            {
                Guid id => id,
                _ => (Guid?)null
            };
            Restrict(accountId, $"Finance Settings maps it as {property.Name}");
        }

        var bankAccountIds = await _db.BankAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.GLAccountId.HasValue && requestedAccountIds.Contains(item.GLAccountId.Value))
            .Select(item => item.GLAccountId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var accountId in bankAccountIds)
            Restrict(accountId, "a tenant bank master owns this GL balance");

        var liquidityAccountIds = await _db.LiquidityAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && requestedAccountIds.Contains(item.GLAccountId))
            .Select(item => item.GLAccountId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var accountId in liquidityAccountIds)
            Restrict(accountId, "a tenant liquidity account owns this custody/settlement balance");

        var taxMappings = await _db.Taxes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new { item.TaxPayableAccountId, item.TaxReceivableAccountId })
            .ToListAsync(cancellationToken);
        foreach (var mapping in taxMappings)
        {
            Restrict(mapping.TaxPayableAccountId, "a tenant tax master maps it as tax payable");
            Restrict(mapping.TaxReceivableAccountId, "a tenant tax master maps it as tax receivable");
        }

        var fixedAssetCategories = await _db.FixedAssetCategories
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var category in fixedAssetCategories)
        {
            foreach (var property in typeof(FixedAssetCategory).GetProperties()
                         .Where(property => property.Name.EndsWith("AccountId", StringComparison.Ordinal)))
            {
                var value = property.GetValue(category);
                var accountId = value switch
                {
                    Guid id => id,
                    _ => (Guid?)null
                };
                Restrict(accountId, $"fixed-asset category '{category.Code}' maps it as {property.Name}");
            }
        }

        foreach (var line in lines)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account) || !restrictions.TryGetValue(line.AccountId, out var reasons))
                continue;
            errors.Add(
                $"Line {line.LineNumber}: account '{account.AccountCode}' is protected; {string.Join(", ", reasons.OrderBy(reason => reason, StringComparer.Ordinal))}. Use the owning subledger/register opening process.");
        }

        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    private sealed record FreeFormOpeningLineCandidate(
        int LineNumber,
        Guid AccountId,
        string? CounterpartyType,
        Guid? BankAccountId = null);

    private sealed record CanonicalOpeningApPartner(
        BusinessPartner Partner,
        BusinessPartnerRole Role,
        BusinessPartnerApProfileVersion Profile);

    private sealed record CanonicalOpeningArPartner(
        BusinessPartner Partner,
        BusinessPartnerRole Role,
        BusinessPartnerArProfileVersion Profile);

    private async Task<CanonicalOpeningArPartner> ResolveOpeningArPartnerAsync(
        Guid businessPartnerId,
        Guid? requestedRoleId,
        DateTime accountingDate,
        CancellationToken cancellationToken)
    {
        var partner = await _db.BusinessPartners.IgnoreQueryFilters().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == businessPartnerId, cancellationToken)
            ?? throw new InvalidOperationException("The selected Business Partner was not found for the current tenant.");
        var roles = await _db.BusinessPartnerRoles.AsNoTracking().Where(role =>
            role.TenantId == TenantId && role.BusinessPartnerId == businessPartnerId && !role.IsDeleted &&
            role.RoleType == BusinessPartnerRoleType.Customer)
            .OrderBy(role => role.CreatedAt)
            .ThenBy(role => role.Id)
            .ToListAsync(cancellationToken);
        if (requestedRoleId.HasValue)
            roles = roles.Where(role => role.Id == requestedRoleId.Value).ToList();
        else if (roles.Count > 1)
            throw new InvalidOperationException("Select the Customer role to use for this opening balance.");

        var role = roles.SingleOrDefault();
        var profiles = role is null
            ? new List<BusinessPartnerArProfileVersion>()
            : await _db.BusinessPartnerArProfileVersions.AsNoTracking()
                .Where(profile => profile.TenantId == TenantId &&
                    profile.BusinessPartnerRoleId == role.Id && !profile.IsDeleted)
                .ToListAsync(cancellationToken);
        var readiness = BusinessPartnerFinanceProfilePolicy.ResolveAr(partner, role, profiles, accountingDate);
        if (!readiness.IsReady || role is null || readiness.ArProfile is null)
            throw new InvalidOperationException($"{readiness.Code}: {readiness.Message}");
        return new CanonicalOpeningArPartner(partner, role, readiness.ArProfile);
    }

    private async Task<CanonicalOpeningApPartner> ResolveOpeningApPartnerAsync(
        Guid businessPartnerId,
        Guid? requestedRoleId,
        DateTime accountingDate,
        CancellationToken cancellationToken)
    {
        var partner = await _db.BusinessPartners.IgnoreQueryFilters().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == businessPartnerId, cancellationToken)
            ?? throw new InvalidOperationException("The selected Business Partner was not found for the current tenant.");
        var roles = await _db.BusinessPartnerRoles.AsNoTracking().Where(role =>
            role.TenantId == TenantId && role.BusinessPartnerId == businessPartnerId && !role.IsDeleted &&
            role.Status == BusinessPartnerRoleStatus.Active &&
            (role.RoleType == BusinessPartnerRoleType.Supplier || role.RoleType == BusinessPartnerRoleType.Contractor))
            .ToListAsync(cancellationToken);
        if (requestedRoleId.HasValue)
            roles = roles.Where(role => role.Id == requestedRoleId.Value).ToList();
        else if (roles.Count > 1)
            throw new InvalidOperationException(
                "Select the Supplier or Contractor role because this Business Partner has both roles.");

        var role = roles.SingleOrDefault();
        var profiles = role is null
            ? new List<BusinessPartnerApProfileVersion>()
            : await _db.BusinessPartnerApProfileVersions.AsNoTracking()
                .Where(profile => profile.TenantId == TenantId &&
                    profile.BusinessPartnerRoleId == role.Id && !profile.IsDeleted)
                .Include(profile => profile.WithholdingDefaults)
                .ToListAsync(cancellationToken);
        var readiness = BusinessPartnerFinanceProfilePolicy.ResolveAp(partner, role, profiles, accountingDate);
        if (!readiness.IsReady || role is null || readiness.ApProfile is null)
            throw new InvalidOperationException($"{readiness.Code}: {readiness.Message}");
        return new CanonicalOpeningApPartner(partner, role, readiness.ApProfile);
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
