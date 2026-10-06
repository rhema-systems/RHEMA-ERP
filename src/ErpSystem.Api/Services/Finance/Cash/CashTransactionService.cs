using System.Data;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Cash;

public class CashTransactionService : ICashTransactionService
{
    private const string CashTransactionWorkflowEntityType = "CashTransaction";

    private readonly ApplicationDbContext _context;
    private readonly ITenantSettingsService _tenantSettingsService;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFinanceAccessScopeService _financeAccessScopeService;
    private readonly IFinanceReversalPolicyService _financeReversalPolicyService;
    private readonly IFinancePostingEngine? _financePostingEngine;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IWorkflowIntegrationService? _workflowIntegrationService;
    private readonly IFinanceControlledDocumentIssueService? _controlledDocumentIssueService;
    private readonly IFinanceSourceDimensionService? _sourceDimensions;
    private readonly IFinanceSourceBookAuthorityService? _sourceBookAuthority;

    public CashTransactionService(
        ApplicationDbContext context,
        IBankAccountService bankAccountService,
        ITenantSettingsService tenantSettingsService,
        IDocumentNumberingService documentNumberingService,
        ICurrentUserService currentUserService,
        IFinanceAccessScopeService financeAccessScopeService,
        IFinanceReversalPolicyService financeReversalPolicyService,
        IFinancePostingEngine? financePostingEngine = null,
        IFinanceAuditService? financeAuditService = null,
        IWorkflowIntegrationService? workflowIntegrationService = null,
        IFinanceControlledDocumentIssueService? controlledDocumentIssueService = null,
        IFinanceSourceDimensionService? sourceDimensions = null,
        IFinanceSourceBookAuthorityService? sourceBookAuthority = null)
    {
        _context = context;
        _tenantSettingsService = tenantSettingsService;
        _documentNumberingService = documentNumberingService;
        _currentUserService = currentUserService;
        _financeAccessScopeService = financeAccessScopeService;
        _financeReversalPolicyService = financeReversalPolicyService;
        _financePostingEngine = financePostingEngine;
        _financeAuditService = financeAuditService;
        _workflowIntegrationService = workflowIntegrationService;
        _controlledDocumentIssueService = controlledDocumentIssueService;
        _sourceDimensions = sourceDimensions;
        _sourceBookAuthority = sourceBookAuthority;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    public async Task<CashTransactionDto?> GetByIdAsync(Guid id)
    {
        var tenantId = TenantId;
        var permittedBankAccountIds = await _financeAccessScopeService
            .GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read);
        var query = _context.Set<CashTransaction>()
            .Where(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted);
        if (permittedBankAccountIds != null)
            query = query.Where(t => permittedBankAccountIds.Contains(t.BankAccountId));

        var result = await query
            .Select(t => new CashTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                BankAccountId = t.BankAccountId,
                BankAccountName = t.BankAccount.AccountName,
                ToBankAccountId = t.ToBankAccountId,
                ToBankAccountName = t.ToBankAccount != null ? t.ToBankAccount.AccountName : null,
                TransferPairId = t.TransferPairId,
                TransferLeg = t.TransferLeg,
                Amount = t.Amount,
                RoundingAdjustmentAmount = t.RoundingAdjustmentAmount,
                FinanceRoundingEvidenceId = t.FinanceRoundingEvidenceId,
                Currency = t.Currency,
                ExchangeRate = t.ExchangeRate,
                ExchangeRateId = t.ExchangeRateId,
                ExchangeRateSource = t.ExchangeRateSource,
                ExchangeRateDate = t.ExchangeRateDate,
                ExchangeRateQuoteSide = t.ExchangeRateQuoteSide.HasValue ? t.ExchangeRateQuoteSide.Value.ToString() : null,
                BaseAmount = t.BaseAmount,
                TransferCrossRate = t.TransferCrossRate,
                TransferFxGainLossBaseAmount = t.TransferFxGainLossBaseAmount,
                PaymentMethodId = t.PaymentMethodId,
                PaymentMethodName = t.PaymentMethod != null ? t.PaymentMethod.Name : null,
                ReferenceNumber = t.ReferenceNumber,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                GLAccountId = t.GLAccountId,
                IsReconciled = t.IsReconciled,
                ReconciliationId = t.ReconciliationId,
                ChequeId = t.ChequeId,
                ChequeNumber = t.Cheque != null ? t.Cheque.ChequeNumber : null,
                IsPosted = t.IsPosted,
                ApprovalStatus = t.ApprovalStatus,
                ApprovalStatusName = t.ApprovalStatus.ToString(),
                WorkflowInstanceId = t.WorkflowInstanceId,
                SubmittedAt = t.SubmittedAt,
                SubmittedById = t.SubmittedById,
                ApprovedAt = t.ApprovedAt,
                ApprovedById = t.ApprovedById,
                RejectedAt = t.RejectedAt,
                RejectedById = t.RejectedById,
                ApprovalComments = t.ApprovalComments,
                RejectionReason = t.RejectionReason,
                CancelledAt = t.CancelledAt,
                CancelledById = t.CancelledById,
                CancellationReason = t.CancellationReason,
                JournalEntryId = t.JournalEntryId,
                PostedDate = t.PostedDate,
                IsReversed = t.IsReversed,
                ReversalOfCashTransactionId = t.ReversalOfCashTransactionId,
                ReversalCashTransactionId = t.ReversalCashTransactionId,
                ReversalJournalEntryId = t.ReversalJournalEntryId,
                ReversalPostingEventId = t.ReversalPostingEventId,
                ReversalDate = t.ReversalDate,
                ReversedAt = t.ReversedAt,
                ReversedById = t.ReversedById,
                ReversalReason = t.ReversalReason,
                CreatedAt = t.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (result != null && result.TransactionType == CashTransactionType.Payment && _controlledDocumentIssueService != null)
        {
            // Detail responses expose issue history from the shared append-only register so the
            // UI can offer either the one original or a reason-backed replacement—not a blind
            // browser print that bypasses document controls.
            result.PaymentSlipIssuance = await _controlledDocumentIssueService.GetSummaryAsync(
                DocumentTypes.FinanceCashBankPaymentSlip,
                result.Id);
        }

        if (result != null && _sourceDimensions is not null
            && result.TransactionType is CashTransactionType.Receipt
                or CashTransactionType.Payment
                or CashTransactionType.Transfer)
        {
            var dimensionSource = await LoadCashTransactionForDimensionAsync(result.Id, default);
            dimensionSource = await ResolvePostingSourceTransactionAsync(dimensionSource, default);
            var producer = await ResolveCashProducerAsync(dimensionSource, requestedProducer: null, default);
            if (await HasCashDimensionProvenanceAsync(dimensionSource, producer, default))
                result.FinanceDimensions = await _sourceDimensions.GetAsync(
                    producer, dimensionSource.Id, dimensionSource.TransactionDate,
                    await BuildCashDimensionLineContextsAsync(dimensionSource, producer, default),
                    default);
        }

        return result;
    }

    public async Task<IEnumerable<CashTransactionDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var tenantId = TenantId;
        var query = _context.Set<CashTransaction>()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        // Lists are filtered rather than rejected so a scoped cashier sees a coherent register
        // for the bank accounts assigned to them without learning that other accounts exist.
        var permittedBankAccountIds = await _financeAccessScopeService
            .GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read);
        if (permittedBankAccountIds != null)
            query = query.Where(t => permittedBankAccountIds.Contains(t.BankAccountId));

        if (fromDate.HasValue)
            query = query.Where(t => t.TransactionDate >= fromDate.Value.Date);

        if (toDate.HasValue)
        {
            var endExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(t => t.TransactionDate < endExclusive);
        }

        return await query
            .Select(t => new CashTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                BankAccountId = t.BankAccountId,
                BankAccountName = t.BankAccount.AccountName,
                Amount = t.Amount,
                RoundingAdjustmentAmount = t.RoundingAdjustmentAmount,
                FinanceRoundingEvidenceId = t.FinanceRoundingEvidenceId,
                Currency = t.Currency,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                IsReconciled = t.IsReconciled,
                IsPosted = t.IsPosted,
                ApprovalStatus = t.ApprovalStatus,
                ApprovalStatusName = t.ApprovalStatus.ToString(),
                WorkflowInstanceId = t.WorkflowInstanceId,
                JournalEntryId = t.JournalEntryId,
                PostedDate = t.PostedDate,
                IsReversed = t.IsReversed,
                ReversalOfCashTransactionId = t.ReversalOfCashTransactionId,
                ReversalCashTransactionId = t.ReversalCashTransactionId,
                ReversalJournalEntryId = t.ReversalJournalEntryId,
                ReversalPostingEventId = t.ReversalPostingEventId,
                ReversalDate = t.ReversalDate,
                ReversedAt = t.ReversedAt,
                ReversedById = t.ReversedById,
                ReversalReason = t.ReversalReason
            })
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CashTransactionDto>> GetByBankAccountAsync(
        Guid bankAccountId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null)
    {
        var tenantId = TenantId;
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            bankAccountId,
            FinanceAccessLevel.Read);
        var query = _context.Set<CashTransaction>()
            .Where(t => t.TenantId == tenantId && t.BankAccountId == bankAccountId && !t.IsDeleted);

        if (fromDate.HasValue)
            query = query.Where(t => t.TransactionDate >= fromDate.Value.Date);

        if (toDate.HasValue)
        {
            var endExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(t => t.TransactionDate < endExclusive);
        }

        return await query
            .Select(t => new CashTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                Amount = t.Amount,
                RoundingAdjustmentAmount = t.RoundingAdjustmentAmount,
                FinanceRoundingEvidenceId = t.FinanceRoundingEvidenceId,
                Currency = t.Currency,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                IsReconciled = t.IsReconciled,
                IsPosted = t.IsPosted,
                ApprovalStatus = t.ApprovalStatus,
                ApprovalStatusName = t.ApprovalStatus.ToString(),
                WorkflowInstanceId = t.WorkflowInstanceId,
                JournalEntryId = t.JournalEntryId,
                PostedDate = t.PostedDate,
                IsReversed = t.IsReversed,
                ReversalOfCashTransactionId = t.ReversalOfCashTransactionId,
                ReversalCashTransactionId = t.ReversalCashTransactionId,
                ReversalJournalEntryId = t.ReversalJournalEntryId,
                ReversalPostingEventId = t.ReversalPostingEventId,
                ReversalDate = t.ReversalDate,
                ReversedAt = t.ReversedAt,
                ReversedById = t.ReversedById,
                ReversalReason = t.ReversalReason
            })
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CashTransactionDto>> GetUnreconciledAsync(Guid bankAccountId)
    {
        var tenantId = TenantId;
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            bankAccountId,
            FinanceAccessLevel.Read);
        return await _context.Set<CashTransaction>()
            .Where(t => t.TenantId == tenantId
                && t.BankAccountId == bankAccountId
                && t.IsPosted
                && t.ApprovalStatus == CashTransactionApprovalStatus.Posted
                && t.JournalEntryId.HasValue
                && !t.IsReversed
                && !t.IsReconciled
                && !t.IsDeleted
                && _context.JournalEntries.Any(j =>
                    j.TenantId == tenantId
                    && j.Id == t.JournalEntryId.Value
                    && j.PostingStatus == "Posted"
                    && !j.IsDeleted))
            .Select(t => new CashTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                Amount = t.Amount,
                RoundingAdjustmentAmount = t.RoundingAdjustmentAmount,
                FinanceRoundingEvidenceId = t.FinanceRoundingEvidenceId,
                Currency = t.Currency,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                ReferenceNumber = t.ReferenceNumber,
                IsPosted = t.IsPosted,
                ApprovalStatus = t.ApprovalStatus,
                ApprovalStatusName = t.ApprovalStatus.ToString(),
                WorkflowInstanceId = t.WorkflowInstanceId,
                JournalEntryId = t.JournalEntryId,
                IsReversed = t.IsReversed,
                ReversalOfCashTransactionId = t.ReversalOfCashTransactionId,
                ReversalCashTransactionId = t.ReversalCashTransactionId,
                ReversalJournalEntryId = t.ReversalJournalEntryId,
                ReversalPostingEventId = t.ReversalPostingEventId,
                ReversalDate = t.ReversalDate,
                ReversedAt = t.ReversedAt,
                ReversedById = t.ReversedById,
                ReversalReason = t.ReversalReason
            })
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();
    }

    public Task<CashTransactionDto> CreateReceiptAsync(CreateCashReceiptDto dto) =>
        CreateReceiptAsync(dto, executionStrategyScope: false, producer: null, cancellationToken: default);

    public Task<CashTransactionDto> CreateReceiptForProducerAsync(
        CreateCashReceiptDto dto,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default)
    {
        ValidateTrustedCashProducer(producer);
        return CreateReceiptAsync(dto, executionStrategyScope: false, producer, cancellationToken);
    }

    private async Task<CashTransactionDto> CreateReceiptAsync(
        CreateCashReceiptDto dto,
        bool executionStrategyScope,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        if (!executionStrategyScope)
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync(
                () => CreateReceiptAsync(dto, executionStrategyScope: true, producer, cancellationToken));
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var tenantId = TenantId;
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            dto.BankAccountId,
            FinanceAccessLevel.Operate);
        await ValidateBankAccountAsync(dto.BankAccountId, tenantId, "receipt");
        await ValidatePaymentMethodAsync(dto.PaymentMethodId, tenantId, dto.ReferenceNumber, dto.BankAccountId, "receipt");
        if (!dto.GLAccountId.HasValue)
            throw new InvalidOperationException("A direct cash receipt requires an offset GL account.");
        await ValidateGLAccountAsync(dto.GLAccountId.Value, tenantId, "receipt");

        var transactionNumber = await GenerateTransactionNumberAsync(FinanceDocumentTypes.CashReceipt, dto.TransactionDate);
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        var transactionCurrency = string.IsNullOrWhiteSpace(dto.Currency) ? baseCurrencyCode : dto.Currency.Trim().ToUpperInvariant();
        var rateSnapshot = await ResolveDirectCashRateSnapshotAsync(
            tenantId, baseCurrencyCode, transactionCurrency, dto.TransactionDate, dto.ExchangeRateId);
        if (dto.ExchangeRate.HasValue && RoundRate(dto.ExchangeRate.Value) != RoundRate(rateSnapshot.Rate))
            throw new InvalidOperationException("The cash receipt exchange-rate value does not match the approved rate record.");
        var exchangeRate = rateSnapshot.Rate;
        var baseAmount = ToBaseAmount(dto.Amount, exchangeRate);

        var transaction = new CashTransaction
        {
            TenantId = tenantId,
            TransactionNumber = transactionNumber,
            TransactionDate = dto.TransactionDate,
            TransactionType = CashTransactionType.Receipt,
            BankAccountId = dto.BankAccountId,
            Amount = dto.Amount,
            Currency = transactionCurrency,
            ExchangeRate = exchangeRate,
            ExchangeRateId = rateSnapshot.ExchangeRateId,
            ExchangeRateSource = rateSnapshot.RateSource,
            ExchangeRateDate = rateSnapshot.RateDate,
            ExchangeRateQuoteSide = rateSnapshot.QuoteSide,
            BaseAmount = baseAmount,
            PaymentMethodId = dto.PaymentMethodId,
            ReferenceNumber = dto.ReferenceNumber,
            PayeeOrPayer = dto.PayerName,
            Description = dto.Description,
            GLAccountId = dto.GLAccountId,
            IsReconciled = false,
            IsPosted = false,
            ApprovalStatus = CashTransactionApprovalStatus.Captured
        };

        _context.Set<CashTransaction>().Add(transaction);
        await _context.SaveChangesAsync();
        await SynchronizeCashDimensionsAsync(
            transaction,
            dto.FinanceDimensions,
            producer ?? CashProducer(transaction),
            cancellationToken);
        if (producer?.RouteId == FinanceDimensionRouteId.FinanceBankReconciliationAdjustment)
            await ValidateAndFreezeCashDimensionsAsync(
                transaction, producer, cancellationToken);

        await RecordCashBankAuditAsync(
            FinanceAuditEvents.CashBankTransactionCaptured,
            transaction,
            afterValues: new
            {
                transaction.Id,
                transaction.TransactionNumber,
                transaction.TransactionType,
                transaction.BankAccountId,
                transaction.GLAccountId,
                transaction.Amount,
                transaction.Currency,
                transaction.BaseAmount
            },
            comment: "Cash/bank receipt captured as an unposted operational transaction.",
            cancellationToken: default);

        await dbTransaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to create receipt");
    }

    public Task<CashTransactionDto> CreatePaymentAsync(CreateCashPaymentDto dto) =>
        CreatePaymentAsync(dto, executionStrategyScope: false, producer: null, cancellationToken: default);

    public Task<CashTransactionDto> CreatePaymentForProducerAsync(
        CreateCashPaymentDto dto,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default)
    {
        ValidateTrustedCashProducer(producer);
        return CreatePaymentAsync(dto, executionStrategyScope: false, producer, cancellationToken);
    }

    private async Task<CashTransactionDto> CreatePaymentAsync(
        CreateCashPaymentDto dto,
        bool executionStrategyScope,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        if (!executionStrategyScope)
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync(
                () => CreatePaymentAsync(dto, executionStrategyScope: true, producer, cancellationToken));
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var tenantId = TenantId;
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            dto.BankAccountId,
            FinanceAccessLevel.Operate);
        await ValidateBankAccountAsync(dto.BankAccountId, tenantId, "payment");
        await ValidatePaymentMethodAsync(dto.PaymentMethodId, tenantId, dto.ReferenceNumber, dto.BankAccountId, "payment");
        if (!dto.GLAccountId.HasValue)
            throw new InvalidOperationException("A direct cash payment requires an offset GL account.");
        await ValidateGLAccountAsync(dto.GLAccountId.Value, tenantId, "payment");

        var transactionNumber = await GenerateTransactionNumberAsync(FinanceDocumentTypes.CashPayment, dto.TransactionDate);
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        var transactionCurrency = string.IsNullOrWhiteSpace(dto.Currency) ? baseCurrencyCode : dto.Currency.Trim().ToUpperInvariant();
        var rateSnapshot = await ResolveDirectCashRateSnapshotAsync(
            tenantId, baseCurrencyCode, transactionCurrency, dto.TransactionDate, dto.ExchangeRateId);
        if (dto.ExchangeRate.HasValue && RoundRate(dto.ExchangeRate.Value) != RoundRate(rateSnapshot.Rate))
            throw new InvalidOperationException("The cash payment exchange-rate value does not match the approved rate record.");
        var exchangeRate = rateSnapshot.Rate;
        var baseAmount = ToBaseAmount(dto.Amount, exchangeRate);

        var transaction = new CashTransaction
        {
            TenantId = tenantId,
            TransactionNumber = transactionNumber,
            TransactionDate = dto.TransactionDate,
            TransactionType = CashTransactionType.Payment,
            BankAccountId = dto.BankAccountId,
            Amount = dto.Amount,
            Currency = transactionCurrency,
            ExchangeRate = exchangeRate,
            ExchangeRateId = rateSnapshot.ExchangeRateId,
            ExchangeRateSource = rateSnapshot.RateSource,
            ExchangeRateDate = rateSnapshot.RateDate,
            ExchangeRateQuoteSide = rateSnapshot.QuoteSide,
            BaseAmount = baseAmount,
            PaymentMethodId = dto.PaymentMethodId,
            ReferenceNumber = dto.ReferenceNumber,
            PayeeOrPayer = dto.PayeeName,
            Description = dto.Description,
            GLAccountId = dto.GLAccountId,
            ChequeId = dto.ChequeId,
            IsReconciled = false,
            IsPosted = false,
            ApprovalStatus = CashTransactionApprovalStatus.Captured
        };

        _context.Set<CashTransaction>().Add(transaction);
        await _context.SaveChangesAsync();
        await SynchronizeCashDimensionsAsync(
            transaction,
            dto.FinanceDimensions,
            producer ?? CashProducer(transaction),
            cancellationToken);
        if (producer?.RouteId == FinanceDimensionRouteId.FinanceBankReconciliationAdjustment)
            await ValidateAndFreezeCashDimensionsAsync(
                transaction, producer, cancellationToken);

        await RecordCashBankAuditAsync(
            FinanceAuditEvents.CashBankTransactionCaptured,
            transaction,
            afterValues: new
            {
                transaction.Id,
                transaction.TransactionNumber,
                transaction.TransactionType,
                transaction.BankAccountId,
                transaction.GLAccountId,
                transaction.Amount,
                transaction.Currency,
                transaction.BaseAmount
            },
            comment: "Cash/bank payment captured as an unposted operational transaction.",
            cancellationToken: default);

        await dbTransaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to create payment");
    }

    public async Task<BankTransferPreviewDto> PreviewTransferAsync(
        CreateBankTransferDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateTransferRequestBasics(dto);

        // Preview is not merely a currency calculator: it reveals controlled bank balances,
        // account names, approved rate sources and the projected GL outcome. Require operating
        // scope over both sides just as capture does so the preview cannot become a scope bypass.
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            dto.FromBankAccountId,
            FinanceAccessLevel.Operate,
            cancellationToken);
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            dto.ToBankAccountId,
            FinanceAccessLevel.Operate,
            cancellationToken);

        var plan = await ResolveBankTransferPlanAsync(
            dto,
            allowDerivedDestinationAmount: true,
            cancellationToken);
        return MapBankTransferPreview(plan);
    }

    public Task<(CashTransactionDto FromTransaction, CashTransactionDto ToTransaction)> CreateTransferAsync(
        CreateBankTransferDto dto) =>
        CreateTransferAsync(dto, executionStrategyScope: false);

    private async Task<(CashTransactionDto FromTransaction, CashTransactionDto ToTransaction)> CreateTransferAsync(
        CreateBankTransferDto dto,
        bool executionStrategyScope)
    {
        if (!executionStrategyScope)
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync(
                () => CreateTransferAsync(dto, executionStrategyScope: true));
        ArgumentNullException.ThrowIfNull(dto);
        ValidateTransferRequestBasics(dto);

        var tenantId = TenantId;
        // A transfer changes two controlled cash positions; access to only one side is not enough
        // to preview, capture, approve, post, reconcile, or later correct the accounting command.
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            dto.FromBankAccountId,
            FinanceAccessLevel.Operate);
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            dto.ToBankAccountId,
            FinanceAccessLevel.Operate);

        var transferPairId = dto.TransferPairId.GetValueOrDefault();
        if (transferPairId == Guid.Empty)
        {
            transferPairId = Guid.NewGuid();
        }

        // Number allocation and the pair-id uniqueness guard share one serializable boundary.
        // A client retry therefore either sees the committed pair or creates it once; it cannot
        // consume a second number while the first request is still being committed.
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var existingPair = await LoadTransferPairAsync(transferPairId, cancellationToken: default);
        if (existingPair != null)
        {
            EnsureTransferRetryMatches(existingPair.Value.Outgoing, existingPair.Value.Incoming, dto);
            await RecordCashBankAuditAsync(
                FinanceAuditEvents.CashBankCrossCurrencyTransferDuplicateCapture,
                existingPair.Value.Outgoing,
                afterValues: new
                {
                    TransferPairId = transferPairId,
                    existingPair.Value.Outgoing.Id,
                    IncomingTransactionId = existingPair.Value.Incoming.Id
                },
                comment: "Duplicate bank-transfer capture returned the existing operational pair.",
                cancellationToken: default);
            await dbTransaction.CommitAsync();
            return (MapCashTransactionDto(existingPair.Value.Outgoing), MapCashTransactionDto(existingPair.Value.Incoming));
        }

        // Creation requires Finance to confirm the destination amount for a cross-currency
        // transfer. The preview may calculate an indicative amount, but the committed source
        // must retain the actual amount expected to arrive at the destination bank.
        var plan = await ResolveBankTransferPlanAsync(
            dto,
            allowDerivedDestinationAmount: false,
            cancellationToken: default);

        await ValidateGLAccountAsync(plan.FromBankAccount.GLAccountId!.Value, tenantId, "source");
        await ValidateGLAccountAsync(plan.ToBankAccount.GLAccountId!.Value, tenantId, "destination");

        var transactionNumber = await GenerateTransactionNumberAsync(FinanceDocumentTypes.BankTransfer, dto.TransactionDate);
        var description = string.IsNullOrWhiteSpace(dto.Description)
            ? $"Bank transfer from {plan.FromBankAccount.AccountName} to {plan.ToBankAccount.AccountName}"
            : dto.Description.Trim();

        var fromTransaction = CreateTransferLeg(
            plan,
            transferPairId,
            BankTransferLeg.Outgoing,
            $"{transactionNumber}-OUT",
            plan.FromBankAccount.Id,
            plan.ToBankAccount.Id,
            plan.SourceAmount,
            plan.SourceCurrency,
            plan.SourceRate,
            plan.SourceBaseAmount,
            dto,
            description);
        var toTransaction = CreateTransferLeg(
            plan,
            transferPairId,
            BankTransferLeg.Incoming,
            $"{transactionNumber}-IN",
            plan.ToBankAccount.Id,
            plan.FromBankAccount.Id,
            plan.DestinationAmount,
            plan.DestinationCurrency,
            plan.DestinationRate,
            plan.DestinationBaseAmount,
            dto,
            description);

        _context.Set<CashTransaction>().AddRange(fromTransaction, toTransaction);
        await _context.SaveChangesAsync();
        await SynchronizeCashDimensionsAsync(
            fromTransaction,
            dto.FinanceDimensions,
            CashProducer(fromTransaction),
            default);

        await RecordCashBankAuditAsync(
            plan.IsCrossCurrency
                ? FinanceAuditEvents.CashBankCrossCurrencyTransferCaptured
                : FinanceAuditEvents.CashBankTransactionCaptured,
            fromTransaction,
            afterValues: new
            {
                fromTransaction.Id,
                TransferPairId = transferPairId,
                sourceTransactionNumber = fromTransaction.TransactionNumber,
                fromTransaction.BankAccountId,
                fromTransaction.ToBankAccountId,
                toTransactionId = toTransaction.Id,
                destinationTransactionNumber = toTransaction.TransactionNumber,
                plan.SourceAmount,
                plan.SourceCurrency,
                plan.SourceBaseAmount,
                SourceExchangeRateId = plan.SourceRate.ExchangeRateId,
                SourceExchangeRate = plan.SourceRate.Rate,
                plan.DestinationAmount,
                plan.DestinationCurrency,
                plan.DestinationBaseAmount,
                DestinationExchangeRateId = plan.DestinationRate.ExchangeRateId,
                DestinationExchangeRate = plan.DestinationRate.Rate,
                plan.CrossRate,
                plan.RealizedFxGainLossBaseAmount,
                plan.RealizedFxOutcome
            },
            comment: plan.IsCrossCurrency
                ? "Cross-currency bank transfer captured with approved rate snapshots and projected realised FX."
                : "Bank transfer captured as an unposted operational transaction pair.",
            cancellationToken: default);

        await dbTransaction.CommitAsync();

        var fromDto = await GetByIdAsync(fromTransaction.Id) ?? throw new Exception("Failed to create transfer");
        var toDto = await GetByIdAsync(toTransaction.Id) ?? throw new Exception("Failed to create transfer");
        return (fromDto, toDto);
    }

    public async Task<CashTransactionDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var transaction = await LoadCashTransactionForWorkflowAsync(id, FinanceAccessLevel.Operate, cancellationToken);
        transaction = await ResolvePostingSourceTransactionAsync(transaction, cancellationToken);
        var priorStatus = transaction.ApprovalStatus;
        if (transaction.IsPosted || transaction.ApprovalStatus == CashTransactionApprovalStatus.Posted)
        {
            throw new InvalidOperationException("Posted cash/bank transactions cannot be submitted.");
        }

        if (transaction.ApprovalStatus == CashTransactionApprovalStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled cash/bank transactions cannot be submitted.");
        }

        if (transaction.ApprovalStatus is not CashTransactionApprovalStatus.Captured and not CashTransactionApprovalStatus.Returned)
        {
            throw new InvalidOperationException("Only captured or returned cash/bank transactions can be submitted.");
        }

        var producer = await ResolveCashProducerAsync(transaction, requestedProducer: null, cancellationToken);
        await ValidateAndFreezeCashDimensionsAsync(transaction, producer, cancellationToken);

        var workflow = RequireWorkflowIntegration();
        var workflowResult = await workflow.SubmitAsync(CashTransactionWorkflowEntityType, transaction.Id);
        EnsureWorkflowSucceeded(workflowResult, "submit");

        var now = DateTime.UtcNow;
        var userId = GetCurrentUserGuid();
        ApplyCashWorkflowOutcome(transaction, workflowResult.Outcome, userId, reasonOrComments: null, now);
        transaction.WorkflowInstanceId = workflowResult.ExecutionResult.WorkflowInstanceId ?? transaction.WorkflowInstanceId;
        if (workflowResult.Outcome == WorkflowOutcome.Pending)
        {
            transaction.ApprovalStatus = CashTransactionApprovalStatus.Submitted;
        }

        transaction.SubmittedAt = now;
        transaction.SubmittedById = userId;
        transaction.UpdatedAt = now;
        transaction.UpdatedBy = _currentUserService.UserName;

        var request = await CashAuthorityRequestAsync(
            transaction,
            producer,
            workflowResult.Outcome == WorkflowOutcome.Pending ? "Submitted" : "Authorized",
            cancellationToken);
        var authority = priorStatus == CashTransactionApprovalStatus.Returned
            ? await RequireSourceBookAuthority().FreezeResubmissionAsync(
                request,
                transaction.SourceBookAuthorityId
                    ?? throw new InvalidOperationException(
                        "SOURCE_BOOK_AUTHORITY_MISSING: returned cash transactions require their prior frozen authority."),
                cancellationToken)
            : await RequireSourceBookAuthority().FreezeInitialPrimaryAsync(request, cancellationToken);
        transaction.SourceBookAuthorityId = authority.AuthorityId;
        if (transaction.TransactionType == CashTransactionType.Transfer)
        {
            var linkedTransferLeg = await FindLinkedTransferLegAsync(transaction, cancellationToken);
            if (linkedTransferLeg is not null)
                linkedTransferLeg.SourceBookAuthorityId = authority.AuthorityId;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await RecordCashBankAuditAsync(
            FinanceAuditEvents.CashBankTransactionSubmitted,
            transaction,
            afterValues: new
            {
                transaction.ApprovalStatus,
                transaction.WorkflowInstanceId,
                transaction.SubmittedAt,
                transaction.SubmittedById
            },
            comment: "Cash/bank transaction submitted through the configured workflow engine.",
            cancellationToken: cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to load submitted cash/bank transaction");
    }

    public async Task<CashTransactionDto> ApproveAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default)
    {
        var transaction = await LoadCashTransactionForWorkflowAsync(id, FinanceAccessLevel.Approve, cancellationToken);
        transaction = await ResolvePostingSourceTransactionAsync(transaction, cancellationToken);
        if (transaction.ApprovalStatus != CashTransactionApprovalStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted cash/bank transactions can be approved.");
        }

        var workflow = RequireWorkflowIntegration();
        var userId = GetCurrentUserGuid();
        if (!await workflow.CanUserApproveAsync(CashTransactionWorkflowEntityType, transaction.Id, userId))
        {
            throw new UnauthorizedAccessException("The current user is not authorized to approve this cash/bank workflow step.");
        }

        await ValidateAndFreezeCashDimensionsAsync(
            transaction,
            await ResolveCashProducerAsync(transaction, requestedProducer: null, cancellationToken),
            cancellationToken);

        var workflowResult = await workflow.ProcessApprovalAsync(CashTransactionWorkflowEntityType, transaction.Id, userId, "Approve", comments);
        EnsureWorkflowSucceeded(workflowResult, "approve");

        var now = DateTime.UtcNow;
        ApplyCashWorkflowOutcome(transaction, workflowResult.Outcome, userId, comments, now);
        transaction.UpdatedAt = now;
        transaction.UpdatedBy = _currentUserService.UserName;

        await _context.SaveChangesAsync(cancellationToken);
        if (transaction.ApprovalStatus == CashTransactionApprovalStatus.Approved)
        {
            await RecordCashBankAuditAsync(
                FinanceAuditEvents.CashBankTransactionApproved,
                transaction,
                afterValues: new
                {
                    transaction.ApprovalStatus,
                    transaction.ApprovedAt,
                    transaction.ApprovedById,
                    transaction.ApprovalComments
                },
                comment: comments,
                cancellationToken: cancellationToken);
        }

        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to load approved cash/bank transaction");
    }

    public async Task<CashTransactionDto> RejectAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default)
    {
        var transaction = await LoadCashTransactionForWorkflowAsync(id, FinanceAccessLevel.Approve, cancellationToken);
        if (transaction.ApprovalStatus != CashTransactionApprovalStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted cash/bank transactions can be rejected.");
        }

        var workflow = RequireWorkflowIntegration();
        var userId = GetCurrentUserGuid();
        if (!await workflow.CanUserApproveAsync(CashTransactionWorkflowEntityType, transaction.Id, userId))
        {
            throw new UnauthorizedAccessException("The current user is not authorized to reject this cash/bank workflow step.");
        }

        var workflowResult = await workflow.ProcessApprovalAsync(CashTransactionWorkflowEntityType, transaction.Id, userId, "Reject", reason);
        EnsureWorkflowSucceeded(workflowResult, "reject");

        var now = DateTime.UtcNow;
        transaction.ApprovalStatus = CashTransactionApprovalStatus.Rejected;
        transaction.RejectedAt = now;
        transaction.RejectedById = userId;
        transaction.RejectionReason = reason;
        transaction.UpdatedAt = now;
        transaction.UpdatedBy = _currentUserService.UserName;

        await _context.SaveChangesAsync(cancellationToken);
        await RecordCashBankAuditAsync(
            FinanceAuditEvents.CashBankTransactionRejected,
            transaction,
            afterValues: new
            {
                transaction.ApprovalStatus,
                transaction.RejectedAt,
                transaction.RejectedById,
                transaction.RejectionReason
            },
            reason: reason,
            cancellationToken: cancellationToken);

        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to load rejected cash/bank transaction");
    }

    public async Task<CashTransactionDto> ReturnAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default)
    {
        var transaction = await LoadCashTransactionForWorkflowAsync(id, FinanceAccessLevel.Approve, cancellationToken);
        transaction = await ResolvePostingSourceTransactionAsync(transaction, cancellationToken);
        if (transaction.ApprovalStatus != CashTransactionApprovalStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted cash/bank transactions can be returned for changes.");
        }

        var workflow = RequireWorkflowIntegration();
        var userId = GetCurrentUserGuid();
        if (!await workflow.CanUserApproveAsync(CashTransactionWorkflowEntityType, transaction.Id, userId))
        {
            throw new UnauthorizedAccessException("The current user is not authorized to return this cash/bank workflow step.");
        }

        var workflowResult = await workflow.ProcessApprovalAsync(CashTransactionWorkflowEntityType, transaction.Id, userId, "RequestChanges", comments);
        EnsureWorkflowSucceeded(workflowResult, "return");

        var now = DateTime.UtcNow;
        transaction.ApprovalStatus = CashTransactionApprovalStatus.Returned;
        transaction.RejectedAt = now;
        transaction.RejectedById = userId;
        transaction.RejectionReason = comments;
        transaction.UpdatedAt = now;
        transaction.UpdatedBy = _currentUserService.UserName;

        await _context.SaveChangesAsync(cancellationToken);
        var producer = await ResolveCashProducerAsync(
            transaction, requestedProducer: null, cancellationToken);
        await SynchronizeCashDimensionsAsync(
            transaction, input: null, producer, cancellationToken);
        await RecordCashBankAuditAsync(
            FinanceAuditEvents.CashBankTransactionReturned,
            transaction,
            afterValues: new
            {
                transaction.ApprovalStatus,
                transaction.RejectedAt,
                transaction.RejectedById,
                transaction.RejectionReason
            },
            comment: comments,
            cancellationToken: cancellationToken);

        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to load returned cash/bank transaction");
    }

    public async Task<CashTransactionDto> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("A cancellation reason is required.");
        }

        var transaction = await LoadCashTransactionForWorkflowAsync(id, FinanceAccessLevel.Operate, cancellationToken);
        if (transaction.IsPosted || transaction.ApprovalStatus == CashTransactionApprovalStatus.Posted)
        {
            throw new InvalidOperationException("Posted cash/bank transactions cannot be cancelled by mutation.");
        }

        if (transaction.ApprovalStatus == CashTransactionApprovalStatus.Cancelled)
        {
            return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to load cancelled cash/bank transaction");
        }

        if (transaction.ApprovalStatus == CashTransactionApprovalStatus.Submitted)
        {
            var workflowResult = await RequireWorkflowIntegration().CancelWorkflowAsync(CashTransactionWorkflowEntityType, transaction.Id, reason);
            if (!workflowResult.Success)
            {
                throw new InvalidOperationException($"Cash/bank workflow cancel failed: {workflowResult.Message ?? "No workflow error details were returned."}");
            }
        }

        var now = DateTime.UtcNow;
        var userId = GetCurrentUserGuid();
        transaction.ApprovalStatus = CashTransactionApprovalStatus.Cancelled;
        transaction.CancelledAt = now;
        transaction.CancelledById = userId;
        transaction.CancellationReason = reason;
        transaction.UpdatedAt = now;
        transaction.UpdatedBy = _currentUserService.UserName;

        await _context.SaveChangesAsync(cancellationToken);
        await RecordCashBankAuditAsync(
            FinanceAuditEvents.CashBankTransactionCancelled,
            transaction,
            afterValues: new
            {
                transaction.ApprovalStatus,
                transaction.CancelledAt,
                transaction.CancelledById,
                transaction.CancellationReason
            },
            reason: reason,
            cancellationToken: cancellationToken);

        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to load cancelled cash/bank transaction");
    }

    public Task<CashTransactionDto> PostAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        PostAsync(id, requestedProducer: null, cancellationToken);

    public Task<CashTransactionDto> PostForProducerAsync(
        Guid id,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default)
    {
        ValidateTrustedCashProducer(producer);
        return PostAsync(id, producer, cancellationToken);
    }

    private Task<CashTransactionDto> PostAsync(
        Guid id,
        FinancePostingProducerContext? requestedProducer,
        CancellationToken cancellationToken) =>
        _context.Database.CreateExecutionStrategy().ExecuteAsync(
            () => PostWithinExecutionStrategyAsync(id, requestedProducer, cancellationToken));

    private async Task<CashTransactionDto> PostWithinExecutionStrategyAsync(
        Guid id,
        FinancePostingProducerContext? requestedProducer,
        CancellationToken cancellationToken)
    {
        if (_financePostingEngine == null)
        {
            throw new InvalidOperationException("Central finance posting engine is not configured for cash/bank transaction posting.");
        }
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var requestedTransaction = await LoadCashTransactionForPostingAsync(id, cancellationToken);
        var sourceTransaction = await ResolvePostingSourceTransactionAsync(requestedTransaction, cancellationToken);
        var producer = await ResolveCashProducerAsync(
            sourceTransaction, requestedProducer, cancellationToken);
        await EnsureTransactionAccessAsync(sourceTransaction, FinanceAccessLevel.Operate, cancellationToken);
        var wasAlreadyLinked = sourceTransaction.JournalEntryId.HasValue;

        await EnforceCashBankPostingEligibilityAsync(sourceTransaction, cancellationToken);
        await ValidateAndFreezeCashDimensionsAsync(sourceTransaction, producer, cancellationToken);

        try
        {
            var authority = await RequireCashPostingAuthorityAsync(
                sourceTransaction, producer, cancellationToken);
            var postingRequest = await BuildCashBankPostingRequestAsync(
                sourceTransaction, producer, cancellationToken);
            postingRequest.AccountingBookCode = authority.AccountingBookCode;
            var postingResult = await _financePostingEngine.PostAsync(
                postingRequest, producer, cancellationToken);

            if (sourceTransaction.JournalEntryId.HasValue && sourceTransaction.JournalEntryId.Value != postingResult.JournalEntryId)
            {
                throw new InvalidOperationException("Cash/bank transaction is linked to a different journal entry than the posting engine result.");
            }

            var postedAt = DateTime.UtcNow;
            var postedBy = Guid.TryParse(_currentUserService.UserId, out var postedById) ? postedById : (Guid?)null;

            sourceTransaction.JournalEntryId = postingResult.JournalEntryId;
            sourceTransaction.IsPosted = true;
            sourceTransaction.ApprovalStatus = CashTransactionApprovalStatus.Posted;
            sourceTransaction.PostedDate = postedAt;
            sourceTransaction.PostedBy = postedBy;
            sourceTransaction.UpdatedAt = postedAt;
            sourceTransaction.UpdatedBy = _currentUserService.UserName;
            sourceTransaction.SourceBookAuthorityId = authority.AuthorityId;

            var linkedTransferLeg = sourceTransaction.TransactionType == CashTransactionType.Transfer
                ? await FindLinkedTransferLegAsync(sourceTransaction, cancellationToken)
                : null;

            if (linkedTransferLeg != null)
            {
                linkedTransferLeg.JournalEntryId = postingResult.JournalEntryId;
                linkedTransferLeg.IsPosted = true;
                linkedTransferLeg.ApprovalStatus = CashTransactionApprovalStatus.Posted;
                linkedTransferLeg.PostedDate = postedAt;
                linkedTransferLeg.PostedBy = postedBy;
                linkedTransferLeg.UpdatedAt = postedAt;
                linkedTransferLeg.UpdatedBy = _currentUserService.UserName;
                linkedTransferLeg.SourceBookAuthorityId = authority.AuthorityId;
            }

            await RequireSourceBookAuthority().BindOriginalPostingAsync(
                authority.AuthorityId,
                postingResult.PostingEventId,
                postingResult.JournalEntryId,
                cancellationToken);

            await ApplyPostedCashBankBalanceSnapshotAsync(
                sourceTransaction,
                linkedTransferLeg,
                postingResult.WasDuplicate || wasAlreadyLinked,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            if (postingResult.WasDuplicate || wasAlreadyLinked)
            {
                await RecordCashBankAuditAsync(
                    FinanceAuditEvents.CashBankTransactionDuplicatePostingAttempt,
                    sourceTransaction,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new
                    {
                        postingResult.PostingEventId,
                        postingResult.JournalEntryId,
                        postingResult.PostingAction,
                        postingResult.WasDuplicate
                    },
                    comment: "Duplicate cash/bank posting request returned the existing posting.",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await RecordCashBankAuditAsync(
                    FinanceAuditEvents.CashBankTransactionPostedAfterApproval,
                    sourceTransaction,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new
                    {
                        postingResult.PostingEventId,
                        postingResult.JournalEntryId,
                        postingResult.JournalEntryNumber,
                        postingResult.TotalDebitAmount,
                        postingResult.TotalCreditAmount,
                        postingResult.FunctionalCurrencyCode,
                        postingResult.PostingDate
                    },
                    comment: "Cash/bank transaction posted through the central finance posting engine.",
                    cancellationToken: cancellationToken);
            }

            if (sourceTransaction.TransactionType == CashTransactionType.Transfer &&
                sourceTransaction.TransferPairId.HasValue &&
                !string.Equals(sourceTransaction.Currency, linkedTransferLeg?.Currency, StringComparison.OrdinalIgnoreCase))
            {
                // The generic posting event remains useful to shared cash/bank reporting. This
                // additional event makes the FX valuation and gain/loss decision independently
                // searchable during TDC review without introducing a second posting event.
                await RecordCashBankAuditAsync(
                    FinanceAuditEvents.CashBankCrossCurrencyTransferPosted,
                    sourceTransaction,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new
                    {
                        sourceTransaction.TransferPairId,
                        SourceAmount = sourceTransaction.Amount,
                        SourceCurrency = sourceTransaction.Currency,
                        SourceBaseAmount = sourceTransaction.BaseAmount,
                        DestinationAmount = linkedTransferLeg?.Amount,
                        DestinationCurrency = linkedTransferLeg?.Currency,
                        DestinationBaseAmount = linkedTransferLeg?.BaseAmount,
                        sourceTransaction.TransferCrossRate,
                        sourceTransaction.TransferFxGainLossBaseAmount,
                        postingResult.JournalEntryNumber
                    },
                    comment: "Cross-currency transfer posted once through the central Finance posting engine.",
                    cancellationToken: cancellationToken);
            }

            await dbTransaction.CommitAsync(cancellationToken);

            return await GetByIdAsync(id) ?? await GetByIdAsync(sourceTransaction.Id) ?? throw new Exception("Failed to load posted cash/bank transaction");
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();

            if (sourceTransaction.TransactionType == CashTransactionType.Transfer &&
                sourceTransaction.TransferPairId.HasValue &&
                sourceTransaction.ToBankAccount != null &&
                !string.Equals(sourceTransaction.Currency, sourceTransaction.ToBankAccount.Currency, StringComparison.OrdinalIgnoreCase))
            {
                await RecordCashBankAuditAsync(
                    FinanceAuditEvents.CashBankCrossCurrencyTransferPostingBlocked,
                    sourceTransaction,
                    afterValues: new
                    {
                        sourceTransaction.TransferPairId,
                        sourceTransaction.TransferFxGainLossBaseAmount,
                        error = ex.Message
                    },
                    reason: ex.Message,
                    comment: "Cross-currency transfer posting failed before any ledger or bank-balance change committed.",
                    cancellationToken: cancellationToken);
            }

            await RecordCashBankAuditAsync(
                FinanceAuditEvents.CashBankTransactionPostingFailed,
                sourceTransaction,
                afterValues: new
                {
                    sourceTransaction.JournalEntryId,
                    sourceTransaction.IsPosted,
                    error = ex.Message
                },
                reason: ex.Message,
                cancellationToken: cancellationToken);

            throw;
        }
    }

    public async Task<CashTransactionTraceDto?> GetTraceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var requested = await LoadCashTransactionForPostingOrNullAsync(id, cancellationToken);
        if (requested == null)
            return null;

        var traceSourceCandidate = requested.ReversalOfCashTransactionId.HasValue
            ? await LoadCashTransactionForPostingAsync(
                requested.ReversalOfCashTransactionId.Value,
                cancellationToken)
            : requested;
        var source = await ResolvePostingSourceTransactionAsync(traceSourceCandidate, cancellationToken);
        await EnsureTransactionAccessAsync(source, FinanceAccessLevel.Read, cancellationToken);
        var linkedTransferLeg = source.TransactionType == CashTransactionType.Transfer
            ? await FindLinkedTransferLegAsync(source, cancellationToken)
            : null;

        var originalIds = new[] { source.Id, linkedTransferLeg?.Id }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToArray();
        var related = await _context.Set<CashTransaction>()
            .AsNoTracking()
            .Include(item => item.BankAccount)
            .Include(item => item.ToBankAccount)
            .Include(item => item.PaymentMethod)
            .Include(item => item.Cheque)
            .Where(item =>
                item.TenantId == TenantId &&
                !item.IsDeleted &&
                (originalIds.Contains(item.Id) ||
                 (item.ReversalOfCashTransactionId.HasValue &&
                  originalIds.Contains(item.ReversalOfCashTransactionId.Value))))
            .OrderBy(item => item.TransactionDate)
            .ThenBy(item => item.TransactionNumber)
            .ToListAsync(cancellationToken);

        var sourceDocumentType = (await ResolveCashProducerAsync(
            source, requestedProducer: null, cancellationToken)).Definition.DocumentType;
        var postingEvents = await _context.FinancePostingEvents
            .AsNoTracking()
            .Include(item => item.JournalEntry)
                .ThenInclude(item => item!.Transactions)
                    .ThenInclude(item => item.Account)
            .Where(item =>
                item.TenantId == TenantId &&
                !item.IsDeleted &&
                item.SourceDocumentType == sourceDocumentType &&
                item.SourceDocumentId == source.Id)
            .OrderBy(item => item.PostingDate)
            .ThenBy(item => item.RequestedAt)
            .ToListAsync(cancellationToken);
        var auditEvents = await _context.Set<AuditLog>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == TenantId &&
                item.Resource == "Finance.CashTransaction" &&
                item.ResourceId == source.Id.ToString())
            .OrderBy(item => item.Timestamp)
            .ToListAsync(cancellationToken);

        var trace = new CashTransactionTraceDto
        {
            Transaction = await GetByIdAsync(requested.Id) ?? MapCashTransactionDto(requested),
            RelatedTransactions = related
                .Where(item => item.Id != requested.Id)
                .Select(MapCashTransactionDto)
                .ToList(),
            Postings = postingEvents.Select(MapPostingTrace).ToList(),
            AuditEvents = auditEvents.Select(item => new FinanceAuditTraceDto
            {
                AuditLogId = item.Id,
                EventType = item.Action,
                Timestamp = item.Timestamp,
                UserId = item.UserId,
                Username = item.Username,
                BeforeValuesJson = item.OldValues,
                DetailsJson = item.NewValues
            }).ToList()
        };

        await RecordCashBankAuditAsync(
            FinanceAuditEvents.CashBankTransactionTraceViewed,
            source,
            afterValues: new
            {
                RelatedTransactionCount = trace.RelatedTransactions.Count,
                PostingCount = trace.Postings.Count,
                AuditEventCount = trace.AuditEvents.Count
            },
            comment: "Cash/bank source-to-ledger trace viewed.",
            cancellationToken: cancellationToken);

        return trace;
    }

    public async Task<CashTransactionDto> ReverseAsync(
        Guid id,
        ReverseCashTransactionDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (_financePostingEngine == null)
            throw new InvalidOperationException("Central finance posting engine is not configured for cash/bank transaction reversal.");

        var requested = await LoadCashTransactionForPostingAsync(id, cancellationToken);
        var initialSource = await ResolvePostingSourceTransactionAsync(requested, cancellationToken);
        await EnsureTransactionAccessAsync(initialSource, FinanceAccessLevel.Approve, cancellationToken);

        // A retry of an already committed correction must not become dependent on today's open
        // periods or a later settings change. Return the stored lineage before evaluating a new
        // reversal policy; the in-transaction check below still handles concurrent requests.
        if (initialSource.IsReversed &&
            initialSource.ReversalCashTransactionId.HasValue &&
            initialSource.ReversalJournalEntryId.HasValue &&
            initialSource.ReversalPostingEventId.HasValue)
        {
            return await GetByIdAsync(id) ?? MapCashTransactionDto(requested);
        }

        var policy = await _financeReversalPolicyService.ResolveAsync(
            initialSource.TransactionDate,
            dto.Reason,
            dto.ReversalDate,
            cancellationToken);

        var initialJournalEntryId = initialSource.JournalEntryId;
        var transactionStarted = false;
        try
        {
            // The GL reversal, both transfer legs (when applicable), bank-balance snapshots, and
            // immutable lineage are one accounting command. Serializable isolation prevents a
            // reconciliation from consuming a source leg midway through that correction.
            await using var dbTransaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            transactionStarted = true;

            var reloadedRequested = await LoadCashTransactionForPostingAsync(id, cancellationToken);
            var source = await ResolvePostingSourceTransactionAsync(reloadedRequested, cancellationToken);
            var linkedTransferLeg = source.TransactionType == CashTransactionType.Transfer
                ? await FindLinkedTransferLegAsync(source, cancellationToken)
                : null;

            // A committed retry returns the existing correction. This is intentionally checked
            // before the remaining eligibility rules, while the posting engine's deterministic
            // key independently protects against concurrent duplicate journals.
            if (source.IsReversed &&
                source.ReversalCashTransactionId.HasValue &&
                source.ReversalJournalEntryId.HasValue &&
                source.ReversalPostingEventId.HasValue)
            {
                await dbTransaction.CommitAsync(cancellationToken);
                transactionStarted = false;
                return await GetByIdAsync(id) ?? MapCashTransactionDto(reloadedRequested);
            }

            if (source.ReversalOfCashTransactionId.HasValue)
                throw new InvalidOperationException("A compensating cash/bank transaction cannot itself be reversed.");
            if (!source.IsPosted ||
                source.ApprovalStatus != CashTransactionApprovalStatus.Posted ||
                !source.JournalEntryId.HasValue)
            {
                throw new InvalidOperationException("Only a posted cash/bank transaction can be reversed.");
            }
            if (source.TransactionType is not CashTransactionType.Receipt and
                not CashTransactionType.Payment and
                not CashTransactionType.Transfer)
            {
                throw new InvalidOperationException("This cash/bank transaction type has a dedicated correction workflow.");
            }
            if (source.IsReconciled || source.ReconciliationId.HasValue)
                throw new InvalidOperationException("Remove the transaction from bank reconciliation before reversing it.");

            if (source.TransactionType == CashTransactionType.Transfer)
            {
                if (linkedTransferLeg == null ||
                    !linkedTransferLeg.IsPosted ||
                    linkedTransferLeg.JournalEntryId != source.JournalEntryId)
                {
                    throw new InvalidOperationException("The posted bank transfer pair is incomplete or has inconsistent journal lineage.");
                }
                if (linkedTransferLeg.IsReconciled || linkedTransferLeg.ReconciliationId.HasValue)
                    throw new InvalidOperationException("Remove both bank-transfer legs from reconciliation before reversing the transfer.");
                if (linkedTransferLeg.ReversalOfCashTransactionId.HasValue)
                    throw new InvalidOperationException("A compensating bank-transfer pair cannot itself be reversed.");
            }

            var sourceDocumentType = (await ResolveCashProducerAsync(
                source, requestedProducer: null, cancellationToken)).Definition.DocumentType;
            var originalPosting = await _context.FinancePostingEvents
                .SingleOrDefaultAsync(item =>
                    item.TenantId == TenantId &&
                    !item.IsDeleted &&
                    item.SourceDocumentType == sourceDocumentType &&
                    item.SourceDocumentId == source.Id &&
                    item.PostingAction == "Post" &&
                    item.PostingStatus == "Posted",
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "This transaction was not posted by the Cash/Bank workflow. Reverse it from its owning Finance subledger instead.");
            if (originalPosting.JournalEntryId != source.JournalEntryId)
                throw new InvalidOperationException("Cash/bank source and posting-event journal links are inconsistent.");
            var originalAuthority = await RequireCashBoundAuthorityAsync(
                source,
                await ResolveCashProducerAsync(source, requestedProducer: null, cancellationToken),
                originalPosting,
                cancellationToken);

            var reversalPlan = await _financePostingEngine.GetReversalPlanAsync(
                originalPosting.Id,
                policy.Reason,
                policy.ReversalDate,
                cancellationToken);
            var reversalResult = await _financePostingEngine.PostAsync(new FinancePostingRequestV2Dto
            {
                SourceModule = "CASHBANK",
                SourceDocumentType = sourceDocumentType,
                SourceDocumentId = source.Id,
                SourceDocumentTenantId = source.TenantId,
                PostingAction = "Reverse",
                SourceDocumentReference = source.TransactionNumber,
                Description = $"Reverse {source.TransactionNumber}: {policy.Reason}",
                PostingDate = policy.ReversalDate,
                JournalType = $"{GetCashBankJournalType(source)} Reversal",
                AccountingBookCode = originalAuthority.AccountingBookCode,
                FunctionalCurrencyCode = originalPosting.FunctionalCurrencyCode,
                ReversalOfJournalEntryId = reversalPlan.OriginalJournalEntryId,
                ReversalReason = policy.Reason,
                ReversalType = "SourceDocument",
                IdempotencyKey = $"CASHBANK:{sourceDocumentType}:{source.TenantId:N}:{source.Id:N}:Reverse",
                ReturnExistingOnDuplicate = true,
                Lines = reversalPlan.ReversalLines.ToList()
            }, cancellationToken);

            var now = DateTime.UtcNow;
            var reversedBy = GetCurrentUserGuid();
            IReadOnlyList<CashTransaction> corrections;
            if (source.TransactionType == CashTransactionType.Transfer)
            {
                corrections = await CreateTransferReversalPairAsync(
                    source,
                    linkedTransferLeg!,
                    reversalResult,
                    policy,
                    now,
                    reversedBy,
                    cancellationToken);

                // The original transfer reduced the source bank and increased the destination.
                // The correction restores those same operational snapshots in the opposite order.
                await AdjustBankBalanceSnapshotAsync(source.TenantId, source.BankAccountId, source.Amount, decrease: false, cancellationToken);
                await AdjustBankBalanceSnapshotAsync(linkedTransferLeg!.TenantId, linkedTransferLeg.BankAccountId, linkedTransferLeg.Amount, decrease: true, cancellationToken);
            }
            else
            {
                var correction = await CreateReceiptOrPaymentReversalAsync(
                    source,
                    reversalResult,
                    policy,
                    now,
                    reversedBy,
                    cancellationToken);
                corrections = new[] { correction };
                await AdjustBankBalanceSnapshotAsync(
                    source.TenantId,
                    source.BankAccountId,
                    source.Amount,
                    decrease: source.TransactionType == CashTransactionType.Receipt,
                    cancellationToken);
            }

            ApplyReversalLineage(source, corrections.Single(item => item.ReversalOfCashTransactionId == source.Id), reversalResult, policy, now, reversedBy);
            if (linkedTransferLeg != null)
            {
                ApplyReversalLineage(
                    linkedTransferLeg,
                    corrections.Single(item => item.ReversalOfCashTransactionId == linkedTransferLeg.Id),
                    reversalResult,
                    policy,
                    now,
                    reversedBy);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await RecordCashBankAuditAsync(
                FinanceAuditEvents.CashBankTransactionReversed,
                source,
                postingEventId: reversalResult.PostingEventId,
                journalEntryId: reversalResult.JournalEntryId,
                beforeValues: new
                {
                    IsReversed = false,
                    JournalEntryId = initialJournalEntryId,
                    source.IsReconciled
                },
                afterValues: new
                {
                    source.IsReversed,
                    source.ReversalCashTransactionId,
                    source.ReversalJournalEntryId,
                    source.ReversalPostingEventId,
                    source.ReversalDate,
                    CorrectionTransactionIds = corrections.Select(item => item.Id).ToArray()
                },
                reason: policy.Reason,
                comment: "Posted cash/bank transaction reversed through linked compensating ledger and operational entries.",
                cancellationToken: cancellationToken);

            await dbTransaction.CommitAsync(cancellationToken);
            transactionStarted = false;
            return await GetByIdAsync(id) ?? MapCashTransactionDto(reloadedRequested);
        }
        catch (Exception ex)
        {
            if (transactionStarted)
            {
                // A database rollback does not reset EF's tracked entity states. Clearing them
                // prevents the subsequent failure-audit save from leaking any failed correction.
                _context.ChangeTracker.Clear();
            }

            await RecordCashBankAuditAsync(
                FinanceAuditEvents.CashBankTransactionReversalFailed,
                initialSource,
                afterValues: new { TransactionId = id, ReversalDate = policy.ReversalDate, error = ex.Message },
                reason: policy.Reason,
                comment: "Cash/bank reversal failed before its compensating entries could commit.",
                cancellationToken: cancellationToken);
            throw;
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var tenantId = TenantId;
        var transaction = await _context.Set<CashTransaction>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted)
            ?? throw new Exception("Transaction not found");

        await EnsureTransactionAccessAsync(transaction, FinanceAccessLevel.Operate, default);

        if (transaction.IsReconciled)
        {
            throw new Exception("Cannot delete reconciled transaction");
        }

        if (transaction.IsPosted)
        {
            throw new Exception("Cannot delete posted transaction");
        }

        transaction.IsDeleted = true;
        transaction.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task MarkAsReconciledAsync(Guid id, Guid reconciliationId)
    {
        var tenantId = TenantId;
        var transaction = await _context.Set<CashTransaction>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted)
            ?? throw new Exception("Transaction not found");

        await EnsureTransactionAccessAsync(transaction, FinanceAccessLevel.Operate, default);

        var reconciliationBelongsToTenant = await _context.Set<BankReconciliation>()
            .AnyAsync(r => r.TenantId == tenantId && r.Id == reconciliationId && !r.IsDeleted);
        if (!reconciliationBelongsToTenant)
        {
            throw new InvalidOperationException("Reconciliation not found.");
        }

        transaction.IsReconciled = true;
        transaction.ReconciliationId = reconciliationId;

        await _context.SaveChangesAsync();
    }

    private async Task<CashTransaction> LoadCashTransactionForWorkflowAsync(
        Guid id,
        FinanceAccessLevel requiredLevel,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var transaction = await _context.Set<CashTransaction>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Cash/bank transaction was not found for this tenant.");

        await EnsureTransactionAccessAsync(transaction, requiredLevel, cancellationToken);
        return transaction;
    }

    private async Task EnsureTransactionAccessAsync(
        CashTransaction transaction,
        FinanceAccessLevel requiredLevel,
        CancellationToken cancellationToken)
    {
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            transaction.BankAccountId,
            requiredLevel,
            cancellationToken);

        if (transaction.TransactionType == CashTransactionType.Transfer && transaction.ToBankAccountId.HasValue)
        {
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                transaction.ToBankAccountId.Value,
                requiredLevel,
                cancellationToken);
        }
    }

    private async Task EnforceCashBankPostingEligibilityAsync(
        CashTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (transaction.IsPosted || transaction.JournalEntryId.HasValue || transaction.ApprovalStatus == CashTransactionApprovalStatus.Posted)
        {
            return;
        }

        if (transaction.ApprovalStatus == CashTransactionApprovalStatus.Approved)
        {
            return;
        }

        var reason = transaction.ApprovalStatus switch
        {
            CashTransactionApprovalStatus.Captured => "Cash/bank transaction must be submitted and approved before posting.",
            CashTransactionApprovalStatus.Submitted => "Cash/bank transaction is submitted but not approved.",
            CashTransactionApprovalStatus.Rejected => "Rejected cash/bank transaction cannot be posted.",
            CashTransactionApprovalStatus.Returned => "Returned cash/bank transaction cannot be posted until resubmitted and approved.",
            CashTransactionApprovalStatus.Cancelled => "Cancelled cash/bank transaction cannot be posted.",
            _ => "Cash/bank transaction is not eligible for posting."
        };

        await RecordCashBankAuditAsync(
            FinanceAuditEvents.CashBankTransactionPostingBlockedApprovalMissing,
            transaction,
            afterValues: new
            {
                transaction.ApprovalStatus,
                transaction.IsPosted,
                transaction.JournalEntryId,
                blockedReason = reason
            },
            reason: reason,
            cancellationToken: cancellationToken);

        throw new InvalidOperationException(reason);
    }

    private async Task ApplyPostedCashBankBalanceSnapshotAsync(
        CashTransaction sourceTransaction,
        CashTransaction? linkedTransferLeg,
        bool isExistingPosting,
        CancellationToken cancellationToken)
    {
        if (isExistingPosting)
        {
            return;
        }

        switch (sourceTransaction.TransactionType)
        {
            case CashTransactionType.Receipt:
                await AdjustBankBalanceSnapshotAsync(sourceTransaction.TenantId, sourceTransaction.BankAccountId, sourceTransaction.Amount, decrease: false, cancellationToken);
                break;
            case CashTransactionType.Payment:
                await AdjustBankBalanceSnapshotAsync(sourceTransaction.TenantId, sourceTransaction.BankAccountId, sourceTransaction.Amount, decrease: true, cancellationToken);
                break;
            case CashTransactionType.Transfer:
                if (linkedTransferLeg == null)
                {
                    throw new InvalidOperationException("Bank transfer destination leg was not found for balance snapshot update.");
                }

                await AdjustBankBalanceSnapshotAsync(sourceTransaction.TenantId, sourceTransaction.BankAccountId, sourceTransaction.Amount, decrease: true, cancellationToken);
                await AdjustBankBalanceSnapshotAsync(linkedTransferLeg.TenantId, linkedTransferLeg.BankAccountId, linkedTransferLeg.Amount, decrease: false, cancellationToken);
                break;
        }
    }

    private async Task AdjustBankBalanceSnapshotAsync(
        Guid tenantId,
        Guid bankAccountId,
        decimal amount,
        bool decrease,
        CancellationToken cancellationToken)
    {
        if (tenantId != TenantId)
        {
            throw new InvalidOperationException("Bank balance snapshot update tenant does not match the current tenant context.");
        }

        if (amount <= 0m)
        {
            throw new InvalidOperationException("Bank balance snapshot amount must be greater than zero.");
        }

        var account = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == bankAccountId && !a.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Bank account was not found for balance snapshot update.");

        if (decrease)
        {
            account.CurrentBalance -= amount;
            account.AvailableBalance -= amount;
        }
        else
        {
            account.CurrentBalance += amount;
            account.AvailableBalance += amount;
        }

        account.UpdatedAt = DateTime.UtcNow;
        account.UpdatedBy = _currentUserService.UserName;
    }

    private IWorkflowIntegrationService RequireWorkflowIntegration()
        => _workflowIntegrationService ?? throw new InvalidOperationException("Cash/bank workflow engine integration is not configured.");

    private Guid GetCurrentUserGuid()
    {
        if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user id is required for cash/bank workflow actions.");
        }

        return userId;
    }

    private static void EnsureWorkflowSucceeded(WorkflowIntegrationResult workflowResult, string action)
    {
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException($"Cash/bank workflow {action} failed: {workflowResult.ExecutionResult.Message ?? "No workflow error details were returned."}");
        }
    }

    private static void ApplyCashWorkflowOutcome(
        CashTransaction transaction,
        WorkflowOutcome outcome,
        Guid? userId,
        string? reasonOrComments,
        DateTime now)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                transaction.ApprovalStatus = CashTransactionApprovalStatus.Approved;
                transaction.ApprovedAt = now;
                transaction.ApprovedById = userId;
                transaction.ApprovalComments = reasonOrComments;
                break;
            case WorkflowOutcome.Rejected:
                transaction.ApprovalStatus = CashTransactionApprovalStatus.Rejected;
                transaction.RejectedAt = now;
                transaction.RejectedById = userId;
                transaction.RejectionReason = reasonOrComments;
                break;
            default:
                transaction.ApprovalStatus = CashTransactionApprovalStatus.Submitted;
                break;
        }
    }

    private async Task<CashTransaction> LoadCashTransactionForPostingAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        return await _context.Set<CashTransaction>()
            .Include(t => t.BankAccount)
            .Include(t => t.ToBankAccount)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Cash/bank transaction was not found for this tenant.");
    }

    private async Task<CashTransaction> LoadCashTransactionForDimensionAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await _context.Set<CashTransaction>()
            .Include(item => item.BankAccount)
            .Include(item => item.ToBankAccount)
            .SingleOrDefaultAsync(item => item.TenantId == TenantId
                && item.Id == id && !item.IsDeleted, cancellationToken)
        ?? throw new InvalidOperationException("Cash/bank transaction was not found for this tenant.");

    private async Task<IReadOnlyList<FinanceSourceDocumentLineContext>> BuildCashDimensionLineContextsAsync(
        CashTransaction source,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken)
    {
        if (source.TransactionType is CashTransactionType.Receipt or CashTransactionType.Payment)
        {
            var offsetAccountId = source.GLAccountId
                ?? throw new InvalidOperationException(
                    "A direct cash transaction requires an offset GL account before Finance dimensions can be validated.");
            if (producer.RouteId == FinanceDimensionRouteId.FinanceBankReconciliationAdjustment)
            {
                var bankAccountId = await _context.BankAccounts.AsNoTracking()
                    .Where(item => item.TenantId == TenantId && item.Id == source.BankAccountId
                        && !item.IsDeleted)
                    .Select(item => item.GLAccountId)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The reconciliation bank account must have a tenant-scoped GL account before Finance dimensions can be validated.");
                return new[]
                {
                    new FinanceSourceDocumentLineContext(
                        FinanceReconciliationDimensionIdentity.BankLine(source.Id), bankAccountId),
                    new FinanceSourceDocumentLineContext(
                        FinanceReconciliationDimensionIdentity.OffsetLine(source.Id), offsetAccountId)
                };
            }
            return new[] { new FinanceSourceDocumentLineContext(source.Id, offsetAccountId) };
        }
        if (source.TransactionType != CashTransactionType.Transfer)
            throw new InvalidOperationException(
                "This cash/bank route is outside the certified Finance dimension scope.");

        var outgoing = IsIncomingTransferLeg(source)
            ? await ResolvePostingSourceTransactionAsync(source, cancellationToken)
            : source;
        var incoming = await FindLinkedTransferLegAsync(outgoing, cancellationToken)
            ?? throw new InvalidOperationException(
                "The transfer destination leg was not found for Finance dimension validation.");
        var bankAccounts = await _context.BankAccounts.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted
                && (item.Id == outgoing.BankAccountId || item.Id == incoming.BankAccountId))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (!bankAccounts.TryGetValue(outgoing.BankAccountId, out var sourceBank)
            || !sourceBank.GLAccountId.HasValue
            || !bankAccounts.TryGetValue(incoming.BankAccountId, out var destinationBank)
            || !destinationBank.GLAccountId.HasValue)
            throw new InvalidOperationException(
                "Both transfer bank accounts must have tenant-scoped GL accounts before dimensions can be captured.");
        return new[]
        {
            new FinanceSourceDocumentLineContext(outgoing.Id, sourceBank.GLAccountId.Value),
            new FinanceSourceDocumentLineContext(incoming.Id, destinationBank.GLAccountId.Value)
        };
    }

    private async Task<FinanceSourceDocumentDimensionDto?> SynchronizeCashDimensionsAsync(
        CashTransaction source,
        FinanceSourceDocumentDimensionInputDto? input,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken)
    {
        if (_sourceDimensions is null)
            return null;
        source = await ResolvePostingSourceTransactionAsync(source, cancellationToken);
        var lines = await BuildCashDimensionLineContextsAsync(source, producer, cancellationToken);
        FinanceSourceDocumentDimensionInputDto? trustedInput = input;
        if (input is not null)
        {
            if (input.Lines.Count > lines.Count)
                throw new InvalidOperationException(
                    "The Finance dimension payload contains more lines than the cash/bank source document.");
            var contextsByAccount = lines.ToDictionary(item => item.AccountId);
            var normalized = new List<FinanceSourceLineDimensionInputDto>();
            foreach (var supplied in input.Lines)
            {
                if (!contextsByAccount.TryGetValue(supplied.AccountId, out var context))
                    throw new InvalidOperationException(
                        "A Finance dimension line does not match a server-resolved cash/bank account.");
                if (normalized.Any(item => item.SourceLineId == context.SourceLineId))
                    throw new InvalidOperationException(
                        "A cash/bank economic line may receive only one Finance dimension assignment.");
                normalized.Add(new FinanceSourceLineDimensionInputDto
                {
                    SourceLineId = context.SourceLineId,
                    AccountId = context.AccountId,
                    Dimensions = supplied.Dimensions
                });
            }
            trustedInput = new FinanceSourceDocumentDimensionInputDto
            {
                DefaultDimensions = input.DefaultDimensions,
                ApplyDefaultToEligibleLines = input.ApplyDefaultToEligibleLines,
                Lines = normalized
            };
        }

        return await _sourceDimensions.SynchronizeDraftAsync(
            producer, source.Id, source.TransactionDate, lines, trustedInput,
            inheritDefaultForUnassignedLines: true,
            budgetReservationSourceDocumentType: null,
            reason: "Cash/bank Finance dimensions synchronized from the captured source.",
            cancellationToken);
    }

    public async Task<FinanceSourceDocumentDimensionDto?> ValidateDimensionsForProducerAsync(
        Guid id,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default)
    {
        ValidateTrustedCashProducer(producer);
        if (_sourceDimensions is null)
            return null;
        var source = await LoadCashTransactionForDimensionAsync(id, cancellationToken);
        source = await ResolvePostingSourceTransactionAsync(source, cancellationToken);
        producer = await ResolveCashProducerAsync(source, producer, cancellationToken);
        if (!await HasCashDimensionProvenanceAsync(source, producer, cancellationToken))
            await SynchronizeCashDimensionsAsync(
                source, input: null, producer, cancellationToken);
        var result = await _sourceDimensions.ValidateAndFreezeAsync(
            producer, source.Id, source.TransactionDate,
            await BuildCashDimensionLineContextsAsync(source, producer, cancellationToken),
            requireCurrentBudgetEvidence: false,
            cancellationToken);
        if (result.ReadinessWarnings.Any(message =>
                message.Contains(" is required ", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "Required Finance dimensions are missing from one or more cash/bank economic lines.");
        return result;
    }

    private async Task ValidateAndFreezeCashDimensionsAsync(
        CashTransaction source,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken)
    {
        if (_sourceDimensions is null)
            return;
        source = await ResolvePostingSourceTransactionAsync(source, cancellationToken);
        if (!await HasCashDimensionProvenanceAsync(source, producer, cancellationToken))
            await SynchronizeCashDimensionsAsync(
                source, input: null, producer, cancellationToken);
        var result = await _sourceDimensions.ValidateAndFreezeAsync(
            producer, source.Id, source.TransactionDate,
            await BuildCashDimensionLineContextsAsync(source, producer, cancellationToken),
            requireCurrentBudgetEvidence: false,
            cancellationToken);
        if (result.ReadinessWarnings.Any(message =>
                message.Contains(" is required ", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "Required Finance dimensions are missing from one or more cash/bank economic lines.");
    }

    private async Task<bool> HasCashDimensionProvenanceAsync(
        CashTransaction source,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken)
    {
        return await _context.FinanceSourceDimensionAssignments.AsNoTracking()
            .AnyAsync(item => item.TenantId == TenantId && !item.IsDeleted
                && item.RouteId == producer.RouteId && item.SourceDocumentId == source.Id
                && !item.SourceLineId.HasValue,
                cancellationToken);
    }

    private async Task<FinancePostingProducerContext> ResolveCashProducerAsync(
        CashTransaction source,
        FinancePostingProducerContext? requestedProducer,
        CancellationToken cancellationToken)
    {
        var persistedRoutes = await _context.FinanceSourceDimensionAssignments.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted
                && item.SourceDocumentId == source.Id && !item.SourceLineId.HasValue)
            .Select(item => item.RouteId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (persistedRoutes.Count > 1)
            throw new InvalidOperationException(
                "The cash/bank source document has conflicting trusted Finance route provenance.");
        if (requestedProducer is not null)
        {
            if (persistedRoutes.Count == 1 && persistedRoutes[0] != requestedProducer.RouteId)
                throw new InvalidOperationException(
                    "The requested Finance producer does not match the persisted cash/bank route provenance.");
            EnsureCashProducerCompatible(source, requestedProducer);
            return requestedProducer;
        }
        var resolved = persistedRoutes.Count == 1
            ? new FinancePostingProducerContext(persistedRoutes[0])
            : CashProducer(source);
        EnsureCashProducerCompatible(source, resolved);
        return resolved;
    }

    private static void ValidateTrustedCashProducer(FinancePostingProducerContext producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (producer.RouteId != FinanceDimensionRouteId.FinanceBankReconciliationAdjustment)
            throw new InvalidOperationException(
                "This Finance cash adapter accepts only the compiled bank-reconciliation adjustment route.");
    }

    private static void EnsureCashProducerCompatible(
        CashTransaction source,
        FinancePostingProducerContext producer)
    {
        if (producer.RouteId == FinanceDimensionRouteId.FinanceBankReconciliationAdjustment)
        {
            if (source.TransactionType is not (CashTransactionType.Receipt or CashTransactionType.Payment))
                throw new InvalidOperationException(
                    "The bank-reconciliation adjustment route accepts only receipt or payment cash transactions.");
            return;
        }

        if (producer.RouteId != CashProducer(source).RouteId)
            throw new InvalidOperationException(
                "The persisted Finance route is incompatible with the cash/bank transaction type.");
    }

    private static FinancePostingProducerContext CashProducer(CashTransaction source) =>
        new(source.TransactionType switch
        {
            CashTransactionType.Payment => FinanceDimensionRouteId.FinanceCashPayment,
            CashTransactionType.Receipt => FinanceDimensionRouteId.FinanceCashReceipt,
            CashTransactionType.Transfer => FinanceDimensionRouteId.FinanceCashBankTransfer,
            _ => throw new InvalidOperationException(
                "This cash/bank transaction is outside the certified Finance dimension routes.")
        });

    private IFinanceSourceBookAuthorityService RequireSourceBookAuthority() =>
        _sourceBookAuthority
        ?? throw new InvalidOperationException(
            "Finance source-book authority is not configured for cash/bank posting.");

    private async Task<FinanceSourceBookAuthorityFreezeRequest> CashAuthorityRequestAsync(
        CashTransaction source,
        FinancePostingProducerContext producer,
        string freezeStage,
        CancellationToken cancellationToken)
    {
        var transactionCurrency = NormalizeCurrency(source.Currency);
        if (source.TransactionType == CashTransactionType.Transfer)
        {
            var linked = await FindLinkedTransferLegAsync(source, cancellationToken);
            if (linked is not null
                && !transactionCurrency.Equals(NormalizeCurrency(linked.Currency), StringComparison.OrdinalIgnoreCase))
                transactionCurrency = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync());
        }
        return new FinanceSourceBookAuthorityFreezeRequest
        {
            OriginModuleCode = ErpSystem.Core.Finance.FinanceModuleLockCatalog.ResolveOriginModuleCode(
                producer.Definition.ProducerModule),
            SourceDocumentType = producer.Definition.DocumentType,
            SourceDocumentId = source.Id,
            PostingAction = "Post",
            EffectiveDate = source.TransactionDate,
            TransactionCurrencyCode = transactionCurrency,
            FreezeStage = freezeStage,
            SourceWorkflowInstanceId = source.WorkflowInstanceId,
            SourceWorkflowEntityType = CashTransactionWorkflowEntityType
        };
    }

    private async Task<FinanceSourceBookAuthorityResult> RequireCashPostingAuthorityAsync(
        CashTransaction source,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken)
    {
        if (!source.SourceBookAuthorityId.HasValue)
            throw new InvalidOperationException(
                "SOURCE_BOOK_AUTHORITY_MISSING: unposted legacy cash/bank transactions require governed resubmission.");
        var retained = await _context.FinanceSourceBookAuthorities.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == TenantId
                && item.Id == source.SourceBookAuthorityId.Value && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_MISSING: retained cash/bank authority was not found.");
        return await RequireSourceBookAuthority().RequireForPostingAsync(
            await CashAuthorityRequestAsync(source, producer, retained.FreezeStage, cancellationToken),
            cancellationToken);
    }

    private async Task<FinanceSourceBookAuthorityResult> RequireCashBoundAuthorityAsync(
        CashTransaction source,
        FinancePostingProducerContext producer,
        FinancePostingEvent originalPosting,
        CancellationToken cancellationToken)
    {
        var authorityService = RequireSourceBookAuthority();
        if (!source.SourceBookAuthorityId.HasValue)
        {
            var retained = await authorityService.RetainExistingPostedOriginalAsync(
                await CashAuthorityRequestAsync(source, producer, "LegacyPosted", cancellationToken),
                source.JournalEntryId
                    ?? throw new InvalidOperationException("Cash/bank original journal evidence is missing."),
                originalPosting.Id,
                cancellationToken);
            source.SourceBookAuthorityId = retained.AuthorityId;
            await _context.SaveChangesAsync(cancellationToken);
        }
        var authority = await authorityService.RequireBoundOriginalAsync(
            source.SourceBookAuthorityId.Value,
            cancellationToken);
        if (authority.OriginalFinancePostingEventId != originalPosting.Id
            || authority.OriginalJournalEntryId != source.JournalEntryId)
            throw new InvalidOperationException(
                "SOURCE_BOOK_AUTHORITY_ORIGINAL_MISMATCH: cash/bank reversal evidence differs from the retained original.");
        return authority;
    }

    private async Task<CashTransaction> ResolvePostingSourceTransactionAsync(
        CashTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (transaction.TransactionType != CashTransactionType.Transfer)
        {
            return transaction;
        }

        if (IsIncomingTransferLeg(transaction))
        {
            if (transaction.TransferPairId.HasValue)
            {
                var pairedSource = await _context.Set<CashTransaction>()
                    .Include(t => t.BankAccount)
                    .Include(t => t.ToBankAccount)
                    .FirstOrDefaultAsync(
                        t => t.TenantId == transaction.TenantId
                            && !t.IsDeleted
                            && t.TransferPairId == transaction.TransferPairId
                            && t.TransferLeg == BankTransferLeg.Outgoing,
                        cancellationToken);

                return pairedSource
                    ?? throw new InvalidOperationException("Transfer source transaction was not found for this tenant.");
            }

            // Development rows created before explicit pair lineage used the human-readable
            // suffix as their only link. Retain this narrow diagnostic path until dev databases
            // are reseeded; every newly captured transfer uses TransferPairId and TransferLeg.
            var sourceNumber = transaction.TransactionNumber[..^3] + "-OUT";
            var source = await _context.Set<CashTransaction>()
                .Include(t => t.BankAccount)
                .Include(t => t.ToBankAccount)
                .FirstOrDefaultAsync(
                    t => t.TenantId == transaction.TenantId
                        && !t.IsDeleted
                        && t.TransactionNumber == sourceNumber
                        && t.BankAccountId == transaction.ToBankAccountId
                        && t.ToBankAccountId == transaction.BankAccountId,
                    cancellationToken);

            return source ?? throw new InvalidOperationException("Transfer source transaction was not found for this tenant.");
        }

        return transaction;
    }

    private async Task<CashTransaction?> FindLinkedTransferLegAsync(
        CashTransaction sourceTransaction,
        CancellationToken cancellationToken)
    {
        if (sourceTransaction.TransactionType != CashTransactionType.Transfer ||
            !sourceTransaction.ToBankAccountId.HasValue)
        {
            return null;
        }

        if (sourceTransaction.TransferPairId.HasValue)
        {
            var oppositeLeg = sourceTransaction.TransferLeg == BankTransferLeg.Incoming
                ? BankTransferLeg.Outgoing
                : BankTransferLeg.Incoming;
            return await _context.Set<CashTransaction>()
                .Include(t => t.BankAccount)
                .Include(t => t.ToBankAccount)
                .FirstOrDefaultAsync(
                    t => t.TenantId == sourceTransaction.TenantId
                        && !t.IsDeleted
                        && t.Id != sourceTransaction.Id
                        && t.TransferPairId == sourceTransaction.TransferPairId
                        && t.TransferLeg == oppositeLeg,
                    cancellationToken);
        }

        // See ResolvePostingSourceTransactionAsync: suffix matching exists only to make already
        // seeded development rows diagnosable. It is not the lineage mechanism for new rows.
        var destinationNumber = IsOutgoingTransferLeg(sourceTransaction)
            ? sourceTransaction.TransactionNumber[..^4] + "-IN"
            : null;

        var query = _context.Set<CashTransaction>()
            .Where(t =>
                t.TenantId == sourceTransaction.TenantId &&
                !t.IsDeleted &&
                t.Id != sourceTransaction.Id &&
                t.TransactionType == CashTransactionType.Transfer &&
                t.BankAccountId == sourceTransaction.ToBankAccountId.Value &&
                t.ToBankAccountId == sourceTransaction.BankAccountId);

        if (!string.IsNullOrWhiteSpace(destinationNumber))
        {
            query = query.Where(t => t.TransactionNumber == destinationNumber);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<FinancePostingRequestV2Dto> BuildCashBankPostingRequestAsync(
        CashTransaction transaction,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (transaction.TenantId != tenantId)
        {
            throw new InvalidOperationException("Cash/bank transaction belongs to another tenant.");
        }

        if (transaction.Amount <= 0m || transaction.BaseAmount <= 0m)
        {
            throw new InvalidOperationException("Cash/bank transaction amount must be greater than zero.");
        }

        var sourceDocumentType = producer.Definition.DocumentType;
        if (transaction.IsPosted || transaction.JournalEntryId.HasValue)
        {
            var hasPostingEvent = await _context.FinancePostingEvents
                .AnyAsync(
                    e => e.TenantId == tenantId
                        && !e.IsDeleted
                        && e.SourceDocumentType == sourceDocumentType
                        && e.SourceDocumentId == transaction.Id
                        && e.PostingAction == "Post"
                        && e.PostingStatus == "Posted",
                    cancellationToken);

            if (!hasPostingEvent)
            {
                throw new InvalidOperationException("Cash/bank transaction is marked posted or linked to a journal without a valid finance posting event. Run posting back-reference diagnostics before retrying.");
            }
        }

        if (transaction.BankAccount.TenantId != tenantId)
        {
            throw new InvalidOperationException("Cash/bank transaction bank account belongs to another tenant.");
        }

        if (!transaction.BankAccount.IsActive)
        {
            throw new InvalidOperationException("Cash/bank transaction bank account is inactive.");
        }

        if (!transaction.BankAccount.GLAccountId.HasValue)
        {
            throw new InvalidOperationException("Cash/bank transaction bank account must be linked to a GL account before posting.");
        }

        var baseCurrencyCode = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync());
        var transactionCurrency = NormalizeCurrency(transaction.Currency);
        var exchangeRate = ResolveExchangeRate(transactionCurrency, baseCurrencyCode, transaction.ExchangeRate);
        var description = string.IsNullOrWhiteSpace(transaction.Description)
            ? $"{transaction.TransactionType} {transaction.TransactionNumber}"
            : transaction.Description.Trim();
        var sourceDimensions = _sourceDimensions is null
            ? new Dictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>>()
            : await _sourceDimensions.GetPostingDimensionsAsync(
                producer, transaction.Id, cancellationToken);

        var lines = transaction.TransactionType switch
        {
            CashTransactionType.Receipt => await BuildReceiptLinesAsync(transaction, producer, description, transactionCurrency, exchangeRate, tenantId, sourceDimensions, cancellationToken),
            CashTransactionType.Payment => await BuildPaymentLinesAsync(transaction, producer, description, transactionCurrency, exchangeRate, tenantId, sourceDimensions, cancellationToken),
            CashTransactionType.Transfer => await BuildTransferLinesAsync(transaction, description, baseCurrencyCode, tenantId, sourceDimensions, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported cash/bank transaction type.")
        };

        var totalDebit = lines.Sum(l => l.DebitAmount);
        var totalCredit = lines.Sum(l => l.CreditAmount);
        if (RoundMoney(totalDebit) != RoundMoney(totalCredit))
        {
            throw new InvalidOperationException("Cash/bank posting request is not balanced.");
        }

        return new FinancePostingRequestV2Dto
        {
            SourceModule = "CASHBANK",
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = transaction.Id,
            SourceDocumentTenantId = transaction.TenantId,
            PostingAction = "Post",
            SourceDocumentReference = string.IsNullOrWhiteSpace(transaction.ReferenceNumber)
                ? transaction.TransactionNumber
                : transaction.ReferenceNumber,
            Description = description,
            PostingDate = transaction.TransactionDate.Date,
            JournalType = GetCashBankJournalType(transaction),
            AccountingBookCode = string.Empty,
            FunctionalCurrencyCode = baseCurrencyCode,
            IdempotencyKey = $"CASHBANK:{sourceDocumentType}:{tenantId:N}:{transaction.Id:N}:Post",
            ReturnExistingOnDuplicate = true,
            Lines = lines
        };
    }

    private async Task<IReadOnlyList<FinancePostingLineDto>> BuildReceiptLinesAsync(
        CashTransaction transaction,
        FinancePostingProducerContext producer,
        string description,
        string transactionCurrency,
        decimal exchangeRate,
        Guid tenantId,
        IReadOnlyDictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>> sourceDimensions,
        CancellationToken cancellationToken)
    {
        if (!transaction.GLAccountId.HasValue)
        {
            throw new InvalidOperationException("Cash receipt requires an offset GL account before posting.");
        }

        await ValidatePostingAccountAsync(transaction.BankAccount.GLAccountId!.Value, tenantId, "cash receipt bank account", cancellationToken);
        await ValidatePostingAccountAsync(transaction.GLAccountId.Value, tenantId, "cash receipt offset account", cancellationToken);

        var reconciliationAdjustment =
            producer.RouteId == FinanceDimensionRouteId.FinanceBankReconciliationAdjustment;
        var bankLineId = reconciliationAdjustment
            ? FinanceReconciliationDimensionIdentity.BankLine(transaction.Id)
            : (Guid?)null;
        var offsetLineId = reconciliationAdjustment
            ? FinanceReconciliationDimensionIdentity.OffsetLine(transaction.Id)
            : transaction.Id;

        return new List<FinancePostingLineDto>
        {
            CreatePostingLine(transaction.BankAccount.GLAccountId.Value, description, debitAmount: transaction.BaseAmount, creditAmount: 0m, transaction, transactionCurrency, exchangeRate, 1, "CashBankReceipt.Bank",
                bankLineId, bankLineId.HasValue
                    ? sourceDimensions.GetValueOrDefault(bankLineId.Value) ?? Array.Empty<FinancePostingDimensionValueDto>()
                    : Array.Empty<FinancePostingDimensionValueDto>()),
            CreatePostingLine(transaction.GLAccountId.Value, description, debitAmount: 0m, creditAmount: transaction.BaseAmount, transaction, transactionCurrency, exchangeRate, 2, "CashBankReceipt.Offset",
                offsetLineId, sourceDimensions.GetValueOrDefault(offsetLineId) ?? Array.Empty<FinancePostingDimensionValueDto>())
        };
    }

    private async Task<IReadOnlyList<FinancePostingLineDto>> BuildPaymentLinesAsync(
        CashTransaction transaction,
        FinancePostingProducerContext producer,
        string description,
        string transactionCurrency,
        decimal exchangeRate,
        Guid tenantId,
        IReadOnlyDictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>> sourceDimensions,
        CancellationToken cancellationToken)
    {
        if (!transaction.GLAccountId.HasValue)
        {
            throw new InvalidOperationException("Cash payment requires an offset GL account before posting.");
        }

        await ValidatePostingAccountAsync(transaction.BankAccount.GLAccountId!.Value, tenantId, "cash payment bank account", cancellationToken);
        await ValidatePostingAccountAsync(transaction.GLAccountId.Value, tenantId, "cash payment offset account", cancellationToken);

        var reconciliationAdjustment =
            producer.RouteId == FinanceDimensionRouteId.FinanceBankReconciliationAdjustment;
        var bankLineId = reconciliationAdjustment
            ? FinanceReconciliationDimensionIdentity.BankLine(transaction.Id)
            : (Guid?)null;
        var offsetLineId = reconciliationAdjustment
            ? FinanceReconciliationDimensionIdentity.OffsetLine(transaction.Id)
            : transaction.Id;

        return new List<FinancePostingLineDto>
        {
            CreatePostingLine(transaction.GLAccountId.Value, description, debitAmount: transaction.BaseAmount, creditAmount: 0m, transaction, transactionCurrency, exchangeRate, 1, "CashBankPayment.Offset",
                offsetLineId, sourceDimensions.GetValueOrDefault(offsetLineId) ?? Array.Empty<FinancePostingDimensionValueDto>()),
            CreatePostingLine(transaction.BankAccount.GLAccountId.Value, description, debitAmount: 0m, creditAmount: transaction.BaseAmount, transaction, transactionCurrency, exchangeRate, 2, "CashBankPayment.Bank",
                bankLineId, bankLineId.HasValue
                    ? sourceDimensions.GetValueOrDefault(bankLineId.Value) ?? Array.Empty<FinancePostingDimensionValueDto>()
                    : Array.Empty<FinancePostingDimensionValueDto>())
        };
    }

    private async Task<IReadOnlyList<FinancePostingLineDto>> BuildTransferLinesAsync(
        CashTransaction transaction,
        string description,
        string functionalCurrency,
        Guid tenantId,
        IReadOnlyDictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>> sourceDimensions,
        CancellationToken cancellationToken)
    {
        if (IsIncomingTransferLeg(transaction))
        {
            throw new InvalidOperationException("Post the outgoing/source side of a bank transfer. The destination leg will be linked automatically.");
        }

        if (!transaction.ToBankAccountId.HasValue || transaction.ToBankAccount == null)
        {
            throw new InvalidOperationException("Bank transfer requires a destination bank account.");
        }

        if (transaction.BankAccountId == transaction.ToBankAccountId.Value)
        {
            throw new InvalidOperationException("Transfer source and destination bank accounts must be different.");
        }

        if (transaction.ToBankAccount.TenantId != tenantId)
        {
            throw new InvalidOperationException("Transfer destination bank account belongs to another tenant.");
        }

        if (!transaction.ToBankAccount.IsActive)
        {
            throw new InvalidOperationException("Transfer destination bank account is inactive.");
        }

        if (!transaction.ToBankAccount.GLAccountId.HasValue)
        {
            throw new InvalidOperationException("Transfer destination bank account must be linked to a GL account before posting.");
        }

        if (transaction.BankAccount.GLAccountId == transaction.ToBankAccount.GLAccountId)
        {
            throw new InvalidOperationException("Transfer source and destination bank accounts must be linked to different GL accounts.");
        }

        var destinationLeg = await FindLinkedTransferLegAsync(transaction, cancellationToken)
            ?? throw new InvalidOperationException("Transfer destination transaction was not found for this tenant.");

        if (!IsIncomingTransferLeg(destinationLeg)
            || destinationLeg.BankAccountId != transaction.ToBankAccountId.Value
            || destinationLeg.ToBankAccountId != transaction.BankAccountId)
        {
            throw new InvalidOperationException("Transfer destination lineage is inconsistent with the source transaction.");
        }

        if (destinationLeg.Amount <= 0m || destinationLeg.BaseAmount <= 0m)
        {
            throw new InvalidOperationException("Transfer destination amount must be greater than zero.");
        }

        var sourceCurrency = NormalizeCurrency(transaction.Currency);
        var destinationCurrency = NormalizeCurrency(destinationLeg.Currency);
        if (!string.Equals(sourceCurrency, NormalizeCurrency(transaction.BankAccount.Currency), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(destinationCurrency, NormalizeCurrency(transaction.ToBankAccount.Currency), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Transfer leg currencies must match their respective bank-account currencies.");
        }

        // The captured rate snapshots are immutable evidence. Recompute the signed difference
        // before posting so a partial/manual edit cannot silently change which FX account is hit.
        var realizedFxGainLoss = RoundMoney(destinationLeg.BaseAmount - transaction.BaseAmount);
        if (RoundMoney(transaction.TransferFxGainLossBaseAmount) != realizedFxGainLoss
            || RoundMoney(destinationLeg.TransferFxGainLossBaseAmount) != realizedFxGainLoss)
        {
            throw new InvalidOperationException("Transfer realised FX snapshot no longer agrees with the captured leg valuations.");
        }

        await ValidatePostingAccountAsync(transaction.BankAccount.GLAccountId!.Value, tenantId, "transfer source bank account", cancellationToken);
        await ValidatePostingAccountAsync(transaction.ToBankAccount.GLAccountId.Value, tenantId, "transfer destination bank account", cancellationToken);

        var lines = new List<FinancePostingLineDto>
        {
            CreatePostingLine(destinationLeg.BankAccount.GLAccountId!.Value, description, debitAmount: destinationLeg.BaseAmount, creditAmount: 0m, destinationLeg, destinationCurrency, ResolveExchangeRate(destinationCurrency, functionalCurrency, destinationLeg.ExchangeRate), 1, "CashBankTransfer.Destination",
                destinationLeg.Id, sourceDimensions.GetValueOrDefault(destinationLeg.Id) ?? Array.Empty<FinancePostingDimensionValueDto>()),
            CreatePostingLine(transaction.BankAccount.GLAccountId.Value, description, debitAmount: 0m, creditAmount: transaction.BaseAmount, transaction, sourceCurrency, ResolveExchangeRate(sourceCurrency, functionalCurrency, transaction.ExchangeRate), 2, "CashBankTransfer.Source",
                transaction.Id, sourceDimensions.GetValueOrDefault(transaction.Id) ?? Array.Empty<FinancePostingDimensionValueDto>())
        };

        if (realizedFxGainLoss != 0m)
        {
            var settings = await _context.FinanceSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("Finance settings must be configured before posting a cross-currency bank transfer.");

            // Positive difference: the destination value exceeds the source value, so credit
            // realised gain. Negative difference: debit realised loss for the shortfall.
            var gain = realizedFxGainLoss > 0m;
            var fxAccountId = gain
                ? settings.RealizedFxGainAccountId
                : settings.RealizedFxLossAccountId;
            if (!fxAccountId.HasValue)
            {
                throw new InvalidOperationException(
                    $"A realised FX {(gain ? "gain" : "loss")} account must be configured in Finance Settings before posting this transfer.");
            }

            await ValidatePostingAccountAsync(
                fxAccountId.Value,
                tenantId,
                gain ? "realised FX gain" : "realised FX loss",
                cancellationToken);

            var derivedAmount = Math.Abs(realizedFxGainLoss);
            var legShares = AllocateTransferDerivedAmount(
                derivedAmount,
                new[]
                {
                    (transaction.Id, transaction.BaseAmount),
                    (destinationLeg.Id, destinationLeg.BaseAmount)
                });
            var lineNumber = 3;
            foreach (var share in legShares)
            {
                var dimensions = _sourceDimensions is null
                    ? sourceDimensions.GetValueOrDefault(share.SourceLineId)
                        ?? Array.Empty<FinancePostingDimensionValueDto>()
                    : await _sourceDimensions.ResolvePostingDimensionsAsync(
                        CashProducer(transaction), transaction.Id, share.SourceLineId,
                        fxAccountId.Value, transaction.TransactionDate, cancellationToken);
                lines.Add(CreateFunctionalPostingLine(
                    fxAccountId.Value,
                    $"Realised FX {(gain ? "gain" : "loss")} - {description}",
                    debitAmount: gain ? 0m : share.Amount,
                    creditAmount: gain ? share.Amount : 0m,
                    functionalCurrency,
                    transaction,
                    lineNumber: lineNumber++,
                    transactionTag: gain ? "CashBankTransfer.RealizedFxGain" : "CashBankTransfer.RealizedFxLoss",
                    sourceDocumentLineId: share.SourceLineId,
                    dimensions: dimensions));
            }
        }

        return lines;
    }

    private static FinancePostingLineDto CreatePostingLine(
        Guid accountId,
        string description,
        decimal debitAmount,
        decimal creditAmount,
        CashTransaction transaction,
        string transactionCurrency,
        decimal exchangeRate,
        int lineNumber,
        string transactionTag,
        Guid? sourceDocumentLineId = null,
        IReadOnlyList<FinancePostingDimensionValueDto>? dimensions = null)
    {
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            SourceDocumentLineId = sourceDocumentLineId,
            Description = description,
            DebitAmount = RoundMoney(debitAmount),
            CreditAmount = RoundMoney(creditAmount),
            TransactionCurrency = transactionCurrency,
            ForeignCurrencyAmount = transaction.Amount,
            ExchangeRateId = transaction.ExchangeRateId,
            ExchangeRate = exchangeRate,
            ExchangeRateSource = transaction.ExchangeRateSource ?? "CashBankTransaction",
            ExchangeRateDate = transaction.ExchangeRateDate?.Date ?? transaction.TransactionDate.Date,
            SourceReferenceNumber = string.IsNullOrWhiteSpace(transaction.ReferenceNumber)
                ? transaction.TransactionNumber
                : transaction.ReferenceNumber,
            LineNumber = lineNumber,
            TransactionTag = transactionTag,
            Dimensions = dimensions ?? Array.Empty<FinancePostingDimensionValueDto>()
        };
    }

    private async Task ValidatePostingAccountAsync(
        Guid accountId,
        Guid tenantId,
        string label,
        CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId && !a.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException($"The {label} GL account was not found for this tenant.");

        if (account.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException($"The {label} GL account is not active.");
        }

        if (!account.AllowDirectPosting)
        {
            throw new InvalidOperationException($"The {label} GL account does not allow direct posting.");
        }
    }

    private static string GetCashBankJournalType(CashTransaction transaction)
        => transaction.TransactionType switch
        {
            CashTransactionType.Receipt => "Cash Receipt",
            CashTransactionType.Payment => "Cash Payment",
            CashTransactionType.Transfer => "Bank Transfer",
            _ => "Cash/Bank Transaction"
        };

    private static bool IsIncomingTransferLeg(CashTransaction transaction)
        => transaction.TransactionType == CashTransactionType.Transfer
            && (transaction.TransferLeg == BankTransferLeg.Incoming
                || (!transaction.TransferLeg.HasValue
                    && transaction.TransactionNumber.EndsWith("-IN", StringComparison.OrdinalIgnoreCase)));

    private static bool IsOutgoingTransferLeg(CashTransaction transaction)
        => transaction.TransactionType == CashTransactionType.Transfer
            && (transaction.TransferLeg == BankTransferLeg.Outgoing
                || (!transaction.TransferLeg.HasValue
                    && transaction.TransactionNumber.EndsWith("-OUT", StringComparison.OrdinalIgnoreCase)));

    private static decimal RoundMoney(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRate(decimal amount)
        => decimal.Round(amount, 6, MidpointRounding.AwayFromZero);

    private static IReadOnlyList<TransferDerivedShare> AllocateTransferDerivedAmount(
        decimal total,
        IReadOnlyCollection<(Guid SourceLineId, decimal Weight)> sourceLines)
    {
        var ordered = sourceLines.OrderBy(item => item.SourceLineId).ToArray();
        if (ordered.Length == 0 || ordered.Any(item => item.SourceLineId == Guid.Empty || item.Weight <= 0m))
            throw new InvalidOperationException(
                "Transfer-derived Finance evidence requires stable source lines with positive weights.");
        var totalWeight = ordered.Sum(item => item.Weight);
        var result = new List<TransferDerivedShare>(ordered.Length);
        decimal allocated = 0m;
        for (var index = 0; index < ordered.Length; index++)
        {
            var independentlyRounded = RoundMoney(total * ordered[index].Weight / totalWeight);
            var amount = index == ordered.Length - 1
                ? RoundMoney(total - allocated)
                : independentlyRounded;
            result.Add(new TransferDerivedShare(
                ordered[index].SourceLineId,
                amount,
                index == ordered.Length - 1,
                index == ordered.Length - 1 ? RoundMoney(amount - independentlyRounded) : 0m));
            allocated += amount;
        }
        if (RoundMoney(result.Sum(item => item.Amount)) != RoundMoney(total))
            throw new InvalidOperationException(
                "Transfer-derived Finance dimension allocation did not reconcile to the realised FX total.");
        return result;
    }

    private static decimal RoundCrossRate(decimal rate)
        => decimal.Round(rate, 8, MidpointRounding.AwayFromZero);

    private async Task<CashTransaction?> LoadCashTransactionForPostingOrNullAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        return await _context.Set<CashTransaction>()
            .AsNoTracking()
            .Include(item => item.BankAccount)
            .Include(item => item.ToBankAccount)
            .Include(item => item.PaymentMethod)
            .Include(item => item.Cheque)
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted,
                cancellationToken);
    }

    private async Task<CashTransaction> CreateReceiptOrPaymentReversalAsync(
        CashTransaction source,
        FinancePostingResultDto reversalResult,
        FinanceReversalPolicyDecision policy,
        DateTime now,
        Guid reversedBy,
        CancellationToken cancellationToken)
    {
        var correctionType = source.TransactionType == CashTransactionType.Receipt
            ? CashTransactionType.Payment
            : CashTransactionType.Receipt;
        var documentType = correctionType == CashTransactionType.Receipt
            ? FinanceDocumentTypes.CashReceipt
            : FinanceDocumentTypes.CashPayment;
        var transactionNumber = await GenerateTransactionNumberAsync(documentType, policy.ReversalDate);
        var correction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = source.TenantId,
            TransactionNumber = transactionNumber,
            TransactionDate = policy.ReversalDate,
            TransactionType = correctionType,
            BankAccountId = source.BankAccountId,
            Amount = source.Amount,
            Currency = source.Currency,
            ExchangeRate = source.ExchangeRate,
            BaseAmount = source.BaseAmount,
            PaymentMethodId = source.PaymentMethodId,
            ReferenceNumber = LimitText($"{source.TransactionNumber}-REV", 100),
            PayeeOrPayer = source.PayeeOrPayer,
            Description = LimitText($"Reversal of {source.TransactionNumber}: {policy.Reason}", 500),
            GLAccountId = source.GLAccountId,
            IsReconciled = false,
            IsPosted = true,
            ApprovalStatus = CashTransactionApprovalStatus.Posted,
            JournalEntryId = reversalResult.JournalEntryId,
            PostedDate = now,
            PostedBy = reversedBy,
            ReversalOfCashTransactionId = source.Id,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName
        };
        _context.Set<CashTransaction>().Add(correction);
        return correction;
    }

    private async Task<IReadOnlyList<CashTransaction>> CreateTransferReversalPairAsync(
        CashTransaction source,
        CashTransaction linkedDestinationLeg,
        FinancePostingResultDto reversalResult,
        FinanceReversalPolicyDecision policy,
        DateTime now,
        Guid reversedBy,
        CancellationToken cancellationToken)
    {
        var baseNumber = await GenerateTransactionNumberAsync(
            FinanceDocumentTypes.BankTransfer,
            policy.ReversalDate);
        var reversalPairId = Guid.NewGuid();
        var reference = LimitText($"{source.TransactionNumber}-REV", 100);
        var description = LimitText($"Reversal of {source.TransactionNumber}: {policy.Reason}", 500);
        var inverseCrossRate = linkedDestinationLeg.Amount > 0m
            ? RoundCrossRate(source.Amount / linkedDestinationLeg.Amount)
            : (decimal?)null;
        var reversedFxGainLoss = -RoundMoney(source.TransferFxGainLossBaseAmount);

        // The compensating transfer moves funds from the original destination back to the
        // original source. Each correction leg deliberately copies the amount, currency and
        // immutable rate snapshot of the original leg affecting that bank. Copying the source
        // amount to both sides would corrupt foreign bank balances and statement matching.
        var correctionOut = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = source.TenantId,
            TransactionNumber = $"{baseNumber}-OUT",
            TransactionDate = policy.ReversalDate,
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = linkedDestinationLeg.BankAccountId,
            ToBankAccountId = source.BankAccountId,
            TransferPairId = reversalPairId,
            TransferLeg = BankTransferLeg.Outgoing,
            Amount = linkedDestinationLeg.Amount,
            Currency = linkedDestinationLeg.Currency,
            ExchangeRate = linkedDestinationLeg.ExchangeRate,
            ExchangeRateId = linkedDestinationLeg.ExchangeRateId,
            ExchangeRateSource = linkedDestinationLeg.ExchangeRateSource,
            ExchangeRateDate = linkedDestinationLeg.ExchangeRateDate,
            ExchangeRateQuoteSide = linkedDestinationLeg.ExchangeRateQuoteSide,
            BaseAmount = linkedDestinationLeg.BaseAmount,
            TransferCrossRate = inverseCrossRate,
            TransferFxGainLossBaseAmount = reversedFxGainLoss,
            ReferenceNumber = reference,
            Description = description,
            IsReconciled = false,
            IsPosted = true,
            ApprovalStatus = CashTransactionApprovalStatus.Posted,
            JournalEntryId = reversalResult.JournalEntryId,
            PostedDate = now,
            PostedBy = reversedBy,
            ReversalOfCashTransactionId = linkedDestinationLeg.Id,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName
        };
        var correctionIn = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = source.TenantId,
            TransactionNumber = $"{baseNumber}-IN",
            TransactionDate = policy.ReversalDate,
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = source.BankAccountId,
            ToBankAccountId = linkedDestinationLeg.BankAccountId,
            TransferPairId = reversalPairId,
            TransferLeg = BankTransferLeg.Incoming,
            Amount = source.Amount,
            Currency = source.Currency,
            ExchangeRate = source.ExchangeRate,
            ExchangeRateId = source.ExchangeRateId,
            ExchangeRateSource = source.ExchangeRateSource,
            ExchangeRateDate = source.ExchangeRateDate,
            ExchangeRateQuoteSide = source.ExchangeRateQuoteSide,
            BaseAmount = source.BaseAmount,
            TransferCrossRate = inverseCrossRate,
            TransferFxGainLossBaseAmount = reversedFxGainLoss,
            ReferenceNumber = reference,
            Description = description,
            IsReconciled = false,
            IsPosted = true,
            ApprovalStatus = CashTransactionApprovalStatus.Posted,
            JournalEntryId = reversalResult.JournalEntryId,
            PostedDate = now,
            PostedBy = reversedBy,
            ReversalOfCashTransactionId = source.Id,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName
        };
        _context.Set<CashTransaction>().AddRange(correctionOut, correctionIn);
        return new[] { correctionOut, correctionIn };
    }

    private void ApplyReversalLineage(
        CashTransaction original,
        CashTransaction correction,
        FinancePostingResultDto reversalResult,
        FinanceReversalPolicyDecision policy,
        DateTime now,
        Guid reversedBy)
    {
        original.IsReversed = true;
        original.ReversalCashTransactionId = correction.Id;
        original.ReversalJournalEntryId = reversalResult.JournalEntryId;
        original.ReversalPostingEventId = reversalResult.PostingEventId;
        original.ReversalDate = policy.ReversalDate;
        original.ReversedAt = now;
        original.ReversedById = reversedBy;
        original.ReversalReason = policy.Reason;
        original.UpdatedAt = now;
        original.UpdatedBy = _currentUserService.UserName;
    }

    private static string LimitText(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static CashTransactionDto MapCashTransactionDto(CashTransaction transaction)
        => new()
        {
            Id = transaction.Id,
            TransactionNumber = transaction.TransactionNumber,
            TransactionDate = transaction.TransactionDate,
            TransactionType = transaction.TransactionType,
            BankAccountId = transaction.BankAccountId,
            BankAccountName = transaction.BankAccount?.AccountName ?? string.Empty,
            ToBankAccountId = transaction.ToBankAccountId,
            ToBankAccountName = transaction.ToBankAccount?.AccountName,
            TransferPairId = transaction.TransferPairId,
            TransferLeg = transaction.TransferLeg,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            ExchangeRate = transaction.ExchangeRate,
            ExchangeRateId = transaction.ExchangeRateId,
            ExchangeRateSource = transaction.ExchangeRateSource,
            ExchangeRateDate = transaction.ExchangeRateDate,
            ExchangeRateQuoteSide = transaction.ExchangeRateQuoteSide?.ToString(),
            BaseAmount = transaction.BaseAmount,
            TransferCrossRate = transaction.TransferCrossRate,
            TransferFxGainLossBaseAmount = transaction.TransferFxGainLossBaseAmount,
            PaymentMethodId = transaction.PaymentMethodId,
            PaymentMethodName = transaction.PaymentMethod?.Name,
            ReferenceNumber = transaction.ReferenceNumber,
            PayeeOrPayer = transaction.PayeeOrPayer,
            Description = transaction.Description,
            GLAccountId = transaction.GLAccountId,
            IsReconciled = transaction.IsReconciled,
            ReconciliationId = transaction.ReconciliationId,
            ChequeId = transaction.ChequeId,
            ChequeNumber = transaction.Cheque?.ChequeNumber,
            IsPosted = transaction.IsPosted,
            ApprovalStatus = transaction.ApprovalStatus,
            ApprovalStatusName = transaction.ApprovalStatus.ToString(),
            WorkflowInstanceId = transaction.WorkflowInstanceId,
            SubmittedAt = transaction.SubmittedAt,
            SubmittedById = transaction.SubmittedById,
            ApprovedAt = transaction.ApprovedAt,
            ApprovedById = transaction.ApprovedById,
            RejectedAt = transaction.RejectedAt,
            RejectedById = transaction.RejectedById,
            ApprovalComments = transaction.ApprovalComments,
            RejectionReason = transaction.RejectionReason,
            CancelledAt = transaction.CancelledAt,
            CancelledById = transaction.CancelledById,
            CancellationReason = transaction.CancellationReason,
            JournalEntryId = transaction.JournalEntryId,
            PostedDate = transaction.PostedDate,
            IsReversed = transaction.IsReversed,
            ReversalOfCashTransactionId = transaction.ReversalOfCashTransactionId,
            ReversalCashTransactionId = transaction.ReversalCashTransactionId,
            ReversalJournalEntryId = transaction.ReversalJournalEntryId,
            ReversalPostingEventId = transaction.ReversalPostingEventId,
            ReversalDate = transaction.ReversalDate,
            ReversedAt = transaction.ReversedAt,
            ReversedById = transaction.ReversedById,
            ReversalReason = transaction.ReversalReason,
            CreatedAt = transaction.CreatedAt,
            CreatedBy = transaction.CreatedBy
        };

    private static FinancePostingTraceDto MapPostingTrace(FinancePostingEvent postingEvent)
    {
        var journal = postingEvent.JournalEntry;
        return new FinancePostingTraceDto
        {
            PostingEventId = postingEvent.Id,
            PostingAction = postingEvent.PostingAction,
            PostingStatus = postingEvent.PostingStatus,
            PostingDate = postingEvent.PostingDate,
            PostedAt = postingEvent.PostedAt,
            JournalEntryId = postingEvent.JournalEntryId,
            JournalEntryNumber = journal?.JournalEntryNumber,
            OriginalJournalEntryId = journal?.OriginalJournalEntryId,
            ReversalJournalEntryId = journal?.ReversalJournalEntryId,
            TotalDebitAmount = postingEvent.TotalDebitAmount,
            TotalCreditAmount = postingEvent.TotalCreditAmount,
            FunctionalCurrencyCode = postingEvent.FunctionalCurrencyCode,
            Lines = journal?.Transactions
                .OrderBy(item => item.LineNumber)
                .Select(item => new FinanceJournalLineTraceDto
                {
                    TransactionId = item.Id,
                    LineNumber = item.LineNumber,
                    AccountId = item.AccountId,
                    AccountNumber = item.Account?.AccountNumber ?? string.Empty,
                    AccountName = item.Account?.AccountName ?? string.Empty,
                    Description = item.Description ?? string.Empty,
                    DebitAmount = item.DebitAmount,
                    CreditAmount = item.CreditAmount,
                    TransactionCurrency = item.TransactionCurrency ?? postingEvent.FunctionalCurrencyCode,
                    ForeignCurrencyAmount = item.ForeignCurrencyAmount,
                    ExchangeRate = item.ExchangeRate,
                    OriginalTransactionId = item.OriginalTransactionId,
                    ReversalTransactionId = item.ReversalTransactionId
                })
                .ToList() ?? new List<FinanceJournalLineTraceDto>()
        };
    }

    private static FinancePostingLineDto CreateFunctionalPostingLine(
        Guid accountId,
        string description,
        decimal debitAmount,
        decimal creditAmount,
        string functionalCurrency,
        CashTransaction sourceTransaction,
        int lineNumber,
        string transactionTag,
        Guid? sourceDocumentLineId = null,
        IReadOnlyList<FinancePostingDimensionValueDto>? dimensions = null)
    {
        // Functional-currency gain/loss lines intentionally carry no foreign amount or rate id;
        // those belong to the two bank legs whose valuation difference produced this line.
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            SourceDocumentLineId = sourceDocumentLineId,
            Description = description,
            DebitAmount = RoundMoney(debitAmount),
            CreditAmount = RoundMoney(creditAmount),
            TransactionCurrency = functionalCurrency,
            SourceReferenceNumber = string.IsNullOrWhiteSpace(sourceTransaction.ReferenceNumber)
                ? sourceTransaction.TransactionNumber
                : sourceTransaction.ReferenceNumber,
            LineNumber = lineNumber,
            TransactionTag = transactionTag,
            Dimensions = dimensions ?? Array.Empty<FinancePostingDimensionValueDto>()
        };
    }

    private readonly record struct TransferDerivedShare(
        Guid SourceLineId,
        decimal Amount,
        bool IsFinalResidualRecipient,
        decimal RoundingResidualAmount);

    private async Task RecordCashBankAuditAsync(
        string eventType,
        CashTransaction transaction,
        Guid? postingEventId = null,
        Guid? journalEntryId = null,
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

        var producer = await ResolveCashProducerAsync(
            transaction, requestedProducer: null, cancellationToken);

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = transaction.TenantId,
            SourceModule = producer.Definition.PostingSourceModule,
            SourceDocumentType = producer.Definition.DocumentType,
            SourceDocumentId = transaction.Id,
            JournalEntryId = journalEntryId ?? transaction.JournalEntryId,
            PostingEventId = postingEventId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Reason = reason,
            Comment = comment,
            Context = new
            {
                transaction.TransactionNumber,
                transaction.TransactionType,
                transaction.BankAccountId,
                transaction.ToBankAccountId,
                transaction.TransferPairId,
                transaction.TransferLeg,
                transaction.GLAccountId,
                transaction.Amount,
                transaction.Currency,
                transaction.ExchangeRateId,
                transaction.ExchangeRate,
                transaction.ExchangeRateSource,
                transaction.ExchangeRateDate,
                transaction.BaseAmount,
                transaction.TransferCrossRate,
                transaction.TransferFxGainLossBaseAmount,
                transaction.ApprovalStatus,
                transaction.WorkflowInstanceId,
                transaction.IsPosted,
                transaction.IsReconciled
            },
            Resource = "Finance.CashTransaction",
            ResourceId = transaction.Id.ToString()
        }, cancellationToken);
    }

    private static void ValidateTransferRequestBasics(CreateBankTransferDto dto)
    {
        if (dto.FromBankAccountId == Guid.Empty || dto.ToBankAccountId == Guid.Empty)
            throw new InvalidOperationException("Transfer source and destination bank accounts are required.");

        if (dto.FromBankAccountId == dto.ToBankAccountId)
            throw new InvalidOperationException("Transfer source and destination bank accounts must be different.");

        if (dto.TransactionDate == default)
            throw new InvalidOperationException("Transfer date is required.");

        if (dto.Amount <= 0m)
            throw new InvalidOperationException("Transfer source amount must be greater than zero.");

        if (dto.DestinationAmount.HasValue && dto.DestinationAmount.Value < 0m)
            throw new InvalidOperationException("Transfer destination amount cannot be negative.");

        if (dto.TransferPairId == Guid.Empty)
            throw new InvalidOperationException("Transfer pair id cannot be an empty GUID when supplied.");

        if (dto.SourceExchangeRateId == Guid.Empty || dto.DestinationExchangeRateId == Guid.Empty)
            throw new InvalidOperationException("Exchange-rate ids cannot be empty GUIDs when supplied.");
    }

    private async Task<(CashTransaction Outgoing, CashTransaction Incoming)?> LoadTransferPairAsync(
        Guid transferPairId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var rows = await _context.Set<CashTransaction>()
            .Include(t => t.BankAccount)
            .Include(t => t.ToBankAccount)
            .Where(t => t.TenantId == tenantId
                && !t.IsDeleted
                && t.TransactionType == CashTransactionType.Transfer
                && t.TransferPairId == transferPairId)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return null;

        var outgoing = rows.SingleOrDefault(t => t.TransferLeg == BankTransferLeg.Outgoing);
        var incoming = rows.SingleOrDefault(t => t.TransferLeg == BankTransferLeg.Incoming);
        if (rows.Count != 2 || outgoing == null || incoming == null)
        {
            throw new InvalidOperationException(
                "The transfer retry key is already present but its OUT/IN lineage is incomplete. Review the development data before retrying.");
        }

        return (outgoing, incoming);
    }

    private static void EnsureTransferRetryMatches(
        CashTransaction outgoing,
        CashTransaction incoming,
        CreateBankTransferDto dto)
    {
        var matches = outgoing.BankAccountId == dto.FromBankAccountId
            && outgoing.ToBankAccountId == dto.ToBankAccountId
            && incoming.BankAccountId == dto.ToBankAccountId
            && incoming.ToBankAccountId == dto.FromBankAccountId
            && outgoing.TransactionDate.Date == dto.TransactionDate.Date
            && RoundMoney(outgoing.Amount) == RoundMoney(dto.Amount);

        if (dto.DestinationAmount is > 0m)
            matches = matches && RoundMoney(incoming.Amount) == RoundMoney(dto.DestinationAmount.Value);

        if (dto.SourceExchangeRateId.HasValue)
            matches = matches && outgoing.ExchangeRateId == dto.SourceExchangeRateId;

        if (dto.DestinationExchangeRateId.HasValue)
            matches = matches && incoming.ExchangeRateId == dto.DestinationExchangeRateId;

        if (!matches)
        {
            throw new InvalidOperationException(
                "The transfer pair id has already been used for different transfer facts. Generate a new pair id for a new transfer.");
        }
    }

    private async Task<BankTransferPlan> ResolveBankTransferPlanAsync(
        CreateBankTransferDto dto,
        bool allowDerivedDestinationAmount,
        CancellationToken cancellationToken)
    {
        ValidateTransferRequestBasics(dto);
        var tenantId = TenantId;
        var accounts = await _context.BankAccounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId
                && !a.IsDeleted
                && (a.Id == dto.FromBankAccountId || a.Id == dto.ToBankAccountId))
            .ToListAsync(cancellationToken);
        var fromAccount = accounts.SingleOrDefault(a => a.Id == dto.FromBankAccountId)
            ?? throw new InvalidOperationException("The source bank account was not found for this tenant.");
        var toAccount = accounts.SingleOrDefault(a => a.Id == dto.ToBankAccountId)
            ?? throw new InvalidOperationException("The destination bank account was not found for this tenant.");

        ValidateTransferAccount(fromAccount, "Source");
        ValidateTransferAccount(toAccount, "Destination");
        if (fromAccount.GLAccountId == toAccount.GLAccountId)
            throw new InvalidOperationException("Transfer bank accounts must be linked to different GL accounts.");

        var functionalCurrency = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync());
        var sourceCurrency = NormalizeCurrency(fromAccount.Currency);
        var destinationCurrency = NormalizeCurrency(toAccount.Currency);
        var isCrossCurrency = !string.Equals(sourceCurrency, destinationCurrency, StringComparison.OrdinalIgnoreCase);
        var transactionDate = dto.TransactionDate.Date;
        var settings = await _context.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken);

        var sourcePolicy = await ResolveBankTransferRatePolicyAsync(
            tenantId,
            fromAccount.GLAccountId!.Value,
            sourceCurrency,
            transactionDate,
            settings,
            cancellationToken);
        var destinationPolicy = await ResolveBankTransferRatePolicyAsync(
            tenantId,
            toAccount.GLAccountId!.Value,
            destinationCurrency,
            transactionDate,
            settings,
            cancellationToken);

        // Moving the same foreign currency between two bank accounts must use one valuation.
        // Divergent account policies would make a no-conversion transfer manufacture FX, so the
        // configuration must be aligned before Finance can capture it.
        if (!isCrossCurrency
            && !string.Equals(sourceCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase)
            && sourcePolicy != destinationPolicy)
        {
            throw new InvalidOperationException(
                $"The two {sourceCurrency} bank GL accounts have different transaction-rate policies. Align their currency links before transferring funds.");
        }

        var sourceRate = await ResolveBankTransferRateSnapshotAsync(
            tenantId,
            functionalCurrency,
            sourceCurrency,
            transactionDate,
            dto.SourceExchangeRateId,
            sourcePolicy,
            cancellationToken);
        var destinationRate = isCrossCurrency
            ? await ResolveBankTransferRateSnapshotAsync(
                tenantId,
                functionalCurrency,
                destinationCurrency,
                transactionDate,
                dto.DestinationExchangeRateId,
                destinationPolicy,
                cancellationToken)
            : sourceRate;

        var sourceAmount = RoundMoney(dto.Amount);
        var sourceBaseAmount = ToBaseAmount(sourceAmount, sourceRate.Rate);
        var destinationWasDerived = false;
        decimal destinationAmount;
        if (!isCrossCurrency)
        {
            if (dto.DestinationAmount is > 0m
                && RoundMoney(dto.DestinationAmount.Value) != sourceAmount)
            {
                throw new InvalidOperationException("Same-currency transfers must move the same amount into the destination bank account.");
            }

            destinationAmount = sourceAmount;
        }
        else if (dto.DestinationAmount is > 0m)
        {
            destinationAmount = RoundMoney(dto.DestinationAmount.Value);
        }
        else if (allowDerivedDestinationAmount)
        {
            destinationAmount = RoundMoney(sourceBaseAmount / destinationRate.Rate);
            destinationWasDerived = true;
        }
        else
        {
            throw new InvalidOperationException(
                "Confirm the destination amount before capturing a cross-currency bank transfer.");
        }

        if (destinationAmount <= 0m)
            throw new InvalidOperationException("Transfer destination amount must be greater than zero.");

        var destinationBaseAmount = ToBaseAmount(destinationAmount, destinationRate.Rate);
        var realizedFxGainLoss = isCrossCurrency
            ? RoundMoney(destinationBaseAmount - sourceBaseAmount)
            : 0m;
        var crossRate = RoundCrossRate(destinationAmount / sourceAmount);
        var realizedFxOutcome = realizedFxGainLoss > 0m
            ? "Gain"
            : realizedFxGainLoss < 0m
                ? "Loss"
                : "None";

        return new BankTransferPlan(
            fromAccount,
            toAccount,
            transactionDate,
            functionalCurrency,
            isCrossCurrency,
            sourceCurrency,
            sourceAmount,
            sourceRate,
            sourceBaseAmount,
            destinationCurrency,
            destinationAmount,
            destinationWasDerived,
            destinationRate,
            destinationBaseAmount,
            crossRate,
            realizedFxGainLoss,
            realizedFxOutcome);
    }

    private async Task<BankTransferRatePolicy> ResolveBankTransferRatePolicyAsync(
        Guid tenantId,
        Guid accountId,
        string currency,
        DateTime transactionDate,
        FinanceSettings? settings,
        CancellationToken cancellationToken)
    {
        var directionalPolicyEnabled = settings?.DirectionalExchangeRatePolicyEnabled == true;
        if (!directionalPolicyEnabled)
            return new BankTransferRatePolicy(ExchangeRateType.Daily, ExchangeRateQuoteSide.Mid);

        var link = await _context.AccountCurrencyLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId
                && l.AccountId == accountId
                && l.LinkedCurrencyCode == currency
                && !l.IsDeleted
                && l.IsActive
                && l.EffectiveDate.Date <= transactionDate.Date
                && (!l.EffectiveEndDate.HasValue || l.EffectiveEndDate.Value.Date >= transactionDate.Date),
                cancellationToken);

        return link == null
            ? new BankTransferRatePolicy(
                ExchangeRateType.Daily,
                settings?.DefaultTransactionQuoteSide ?? ExchangeRateQuoteSide.Mid)
            : new BankTransferRatePolicy(
                ParseBankTransferRateType(link.TransactionRateType),
                link.TransactionQuoteSide);
    }

    private async Task<BankTransferRateSnapshot> ResolveDirectCashRateSnapshotAsync(
        Guid tenantId,
        string functionalCurrency,
        string transactionCurrency,
        DateTime transactionDate,
        Guid? requestedRateId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _context.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        var quoteSide = settings?.DirectionalExchangeRatePolicyEnabled == true
            ? settings.DefaultTransactionQuoteSide
            : ExchangeRateQuoteSide.Mid;
        return await ResolveBankTransferRateSnapshotAsync(
            tenantId,
            functionalCurrency,
            transactionCurrency,
            transactionDate,
            requestedRateId,
            new BankTransferRatePolicy(ExchangeRateType.Daily, quoteSide),
            cancellationToken);
    }

    private async Task<BankTransferRateSnapshot> ResolveBankTransferRateSnapshotAsync(
        Guid tenantId,
        string functionalCurrency,
        string transactionCurrency,
        DateTime transactionDate,
        Guid? requestedRateId,
        BankTransferRatePolicy policy,
        CancellationToken cancellationToken)
    {
        if (string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            if (requestedRateId.HasValue)
                throw new InvalidOperationException("A functional-currency bank leg does not require an exchange-rate id.");

            return new BankTransferRateSnapshot(
                null,
                1m,
                "Functional currency",
                transactionDate.Date,
                ExchangeRateQuoteSide.Mid,
                ExchangeRateType.Daily);
        }

        ExchangeRate? rate;
        if (requestedRateId.HasValue)
        {
            rate = await _context.ExchangeRates
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId
                    && r.Id == requestedRateId.Value
                    && !r.IsDeleted,
                    cancellationToken);
        }
        else
        {
            rate = await _context.ExchangeRates
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId
                    && !r.IsDeleted
                    && r.BaseCurrencyCode == functionalCurrency
                    && r.TargetCurrencyCode == transactionCurrency
                    && r.RateType == policy.RateType
                    && r.QuoteSide == policy.QuoteSide
                    && r.IsActive
                    && r.Rate > 0m
                    && (r.ApprovalStatus == RateApprovalStatus.Approved
                        || r.ApprovalStatus == RateApprovalStatus.AutoApproved)
                    && r.EffectiveDate.Date <= transactionDate.Date
                    && (!r.EndDate.HasValue || r.EndDate.Value.Date >= transactionDate.Date))
                .OrderByDescending(r => r.EffectiveDate)
                .ThenByDescending(r => r.Priority)
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (rate == null)
        {
            throw new InvalidOperationException(
                $"No approved {policy.QuoteSide} {policy.RateType} exchange rate exists for {transactionCurrency} to {functionalCurrency} on {transactionDate:yyyy-MM-dd}.");
        }

        if (!rate.IsActive
            || rate.Rate <= 0m
            || rate.ApprovalStatus is not (RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved))
        {
            throw new InvalidOperationException("The selected exchange rate must be active, approved, and greater than zero.");
        }

        if (!string.Equals(rate.BaseCurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(rate.TargetCurrencyCode, transactionCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected exchange-rate currency pair does not match the bank-leg currency.");
        }

        if (rate.EffectiveDate.Date > transactionDate.Date
            || (rate.EndDate.HasValue && rate.EndDate.Value.Date < transactionDate.Date))
        {
            throw new InvalidOperationException("The selected exchange rate is not effective for the transfer date.");
        }

        // Capture rejects policy overrides because an ordinary transfer screen does not collect
        // exceptional-rate approval evidence. Finance can first correct/approve its rate setup,
        // keeping preview, capture and the central posting engine on one deterministic policy.
        if (rate.RateType != policy.RateType || rate.QuoteSide != policy.QuoteSide)
        {
            throw new InvalidOperationException(
                $"The selected exchange rate does not match the required {policy.QuoteSide} {policy.RateType} policy for this bank account.");
        }

        return new BankTransferRateSnapshot(
            rate.Id,
            rate.InverseRate,
            string.IsNullOrWhiteSpace(rate.RateSource) ? "Approved tenant rate" : rate.RateSource.Trim(),
            rate.EffectiveDate.Date,
            rate.QuoteSide,
            rate.RateType);
    }

    private static ExchangeRateType ParseBankTransferRateType(string? value)
    {
        var normalized = value?.Trim()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty);
        return Enum.TryParse<ExchangeRateType>(normalized, ignoreCase: true, out var rateType)
            ? rateType
            : throw new InvalidOperationException("The bank GL account has an invalid transaction exchange-rate type.");
    }

    private CashTransaction CreateTransferLeg(
        BankTransferPlan plan,
        Guid transferPairId,
        BankTransferLeg leg,
        string transactionNumber,
        Guid bankAccountId,
        Guid toBankAccountId,
        decimal amount,
        string currency,
        BankTransferRateSnapshot rate,
        decimal baseAmount,
        CreateBankTransferDto dto,
        string description)
    {
        var now = DateTime.UtcNow;
        return new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TransactionNumber = transactionNumber,
            TransactionDate = plan.TransactionDate,
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = bankAccountId,
            ToBankAccountId = toBankAccountId,
            TransferPairId = transferPairId,
            TransferLeg = leg,
            Amount = amount,
            Currency = currency,
            ExchangeRate = rate.Rate,
            ExchangeRateId = rate.ExchangeRateId,
            ExchangeRateSource = rate.RateSource,
            ExchangeRateDate = rate.RateDate,
            ExchangeRateQuoteSide = rate.QuoteSide,
            BaseAmount = baseAmount,
            TransferCrossRate = plan.CrossRate,
            TransferFxGainLossBaseAmount = plan.RealizedFxGainLossBaseAmount,
            ReferenceNumber = string.IsNullOrWhiteSpace(dto.ReferenceNumber)
                ? null
                : LimitText(dto.ReferenceNumber.Trim(), 100),
            Description = LimitText(description, 500),
            IsReconciled = false,
            IsPosted = false,
            ApprovalStatus = CashTransactionApprovalStatus.Captured,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName
        };
    }

    private static BankTransferPreviewDto MapBankTransferPreview(BankTransferPlan plan)
        => new()
        {
            FromBankAccountId = plan.FromBankAccount.Id,
            FromBankAccountName = plan.FromBankAccount.AccountName,
            ToBankAccountId = plan.ToBankAccount.Id,
            ToBankAccountName = plan.ToBankAccount.AccountName,
            TransactionDate = plan.TransactionDate,
            IsCrossCurrency = plan.IsCrossCurrency,
            SourceCurrency = plan.SourceCurrency,
            SourceAmount = plan.SourceAmount,
            SourceExchangeRate = plan.SourceRate.Rate,
            SourceExchangeRateId = plan.SourceRate.ExchangeRateId,
            SourceExchangeRateSource = plan.SourceRate.RateSource,
            SourceExchangeRateDate = plan.SourceRate.RateDate,
            SourceExchangeRateQuoteSide = plan.SourceRate.QuoteSide.ToString(),
            SourceBaseAmount = plan.SourceBaseAmount,
            DestinationCurrency = plan.DestinationCurrency,
            DestinationAmount = plan.DestinationAmount,
            DestinationAmountWasDerived = plan.DestinationAmountWasDerived,
            DestinationExchangeRate = plan.DestinationRate.Rate,
            DestinationExchangeRateId = plan.DestinationRate.ExchangeRateId,
            DestinationExchangeRateSource = plan.DestinationRate.RateSource,
            DestinationExchangeRateDate = plan.DestinationRate.RateDate,
            DestinationExchangeRateQuoteSide = plan.DestinationRate.QuoteSide.ToString(),
            DestinationBaseAmount = plan.DestinationBaseAmount,
            CrossRate = plan.CrossRate,
            RealizedFxGainLossBaseAmount = plan.RealizedFxGainLossBaseAmount,
            RealizedFxOutcome = plan.RealizedFxOutcome,
            FunctionalCurrency = plan.FunctionalCurrency
        };

    private sealed record BankTransferRatePolicy(
        ExchangeRateType RateType,
        ExchangeRateQuoteSide QuoteSide);

    private sealed record BankTransferRateSnapshot(
        Guid? ExchangeRateId,
        decimal Rate,
        string RateSource,
        DateTime RateDate,
        ExchangeRateQuoteSide QuoteSide,
        ExchangeRateType RateType);

    private sealed record BankTransferPlan(
        BankAccount FromBankAccount,
        BankAccount ToBankAccount,
        DateTime TransactionDate,
        string FunctionalCurrency,
        bool IsCrossCurrency,
        string SourceCurrency,
        decimal SourceAmount,
        BankTransferRateSnapshot SourceRate,
        decimal SourceBaseAmount,
        string DestinationCurrency,
        decimal DestinationAmount,
        bool DestinationAmountWasDerived,
        BankTransferRateSnapshot DestinationRate,
        decimal DestinationBaseAmount,
        decimal CrossRate,
        decimal RealizedFxGainLossBaseAmount,
        string RealizedFxOutcome);

    private async Task<string> GenerateTransactionNumberAsync(string documentType, DateTime transactionDate)
    {
        return await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            documentType,
            documentDate: transactionDate,
            entityType: nameof(CashTransaction));
    }

    private static decimal ResolveExchangeRate(string transactionCurrency, string baseCurrencyCode, decimal? exchangeRate)
    {
        if (string.Equals(transactionCurrency, baseCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var resolvedRate = exchangeRate.GetValueOrDefault();
        if (resolvedRate <= 0m)
        {
            throw new InvalidOperationException($"An exchange rate is required for {transactionCurrency} cash transactions.");
        }

        return resolvedRate;
    }

    private static decimal ToBaseAmount(decimal amount, decimal exchangeRate)
    {
        return decimal.Round(amount * exchangeRate, 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeCurrency(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "GHS"
            : currency.Trim().ToUpperInvariant();
    }

    private static void ValidateTransferAccount(BankAccount account, string label)
    {
        if (!account.IsActive)
        {
            throw new InvalidOperationException($"{label} bank account is inactive.");
        }

        if (!account.GLAccountId.HasValue)
        {
            throw new InvalidOperationException($"{label} bank account must be linked to a GL account before transfers can be posted.");
        }
    }

    private async Task ValidateBankAccountAsync(Guid bankAccountId, Guid tenantId, string label)
    {
        var exists = await _context.BankAccounts
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.Id == bankAccountId && !a.IsDeleted && a.IsActive);

        if (!exists)
        {
            throw new InvalidOperationException($"The {label} bank account was not found for this tenant.");
        }
    }

    private async Task ValidatePaymentMethodAsync(Guid? paymentMethodId, Guid tenantId, string? referenceNumber, Guid bankAccountId, string label)
    {
        if (!paymentMethodId.HasValue)
        {
            return;
        }

        var method = await _context.PaymentMethods
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == paymentMethodId.Value && !m.IsDeleted);

        if (method == null)
        {
            throw new InvalidOperationException($"The selected {label} payment method was not found for this tenant.");
        }

        if (!method.IsActive)
        {
            throw new InvalidOperationException($"The selected {label} payment method is inactive.");
        }

        if (method.RequiresBankAccount && bankAccountId == Guid.Empty)
        {
            throw new InvalidOperationException($"The selected {label} payment method requires a bank account.");
        }

        if (method.RequiresReference && string.IsNullOrWhiteSpace(referenceNumber))
        {
            throw new InvalidOperationException($"The selected {label} payment method requires a reference number.");
        }
    }

    private async Task ValidateGLAccountAsync(Guid accountId, Guid tenantId, string label)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId && !a.IsDeleted)
            ?? throw new InvalidOperationException($"The {label} bank GL account was not found.");

        if (account.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException($"The {label} bank GL account is not active.");
        }
    }

}
