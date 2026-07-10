using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
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
    private readonly IFinancePostingEngine? _financePostingEngine;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IWorkflowIntegrationService? _workflowIntegrationService;

    public CashTransactionService(
        ApplicationDbContext context,
        IBankAccountService bankAccountService,
        ITenantSettingsService tenantSettingsService,
        IDocumentNumberingService documentNumberingService,
        ICurrentUserService currentUserService,
        IFinancePostingEngine? financePostingEngine = null,
        IFinanceAuditService? financeAuditService = null,
        IWorkflowIntegrationService? workflowIntegrationService = null)
    {
        _context = context;
        _tenantSettingsService = tenantSettingsService;
        _documentNumberingService = documentNumberingService;
        _currentUserService = currentUserService;
        _financePostingEngine = financePostingEngine;
        _financeAuditService = financeAuditService;
        _workflowIntegrationService = workflowIntegrationService;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    public async Task<CashTransactionDto?> GetByIdAsync(Guid id)
    {
        var tenantId = TenantId;
        return await _context.Set<CashTransaction>()
            .Where(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted)
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
                Amount = t.Amount,
                Currency = t.Currency,
                ExchangeRate = t.ExchangeRate,
                BaseAmount = t.BaseAmount,
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
                CreatedAt = t.CreatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<CashTransactionDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var tenantId = TenantId;
        var query = _context.Set<CashTransaction>()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        if (fromDate.HasValue)
            query = query.Where(t => t.TransactionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.TransactionDate <= toDate.Value);

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
                Currency = t.Currency,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                IsReconciled = t.IsReconciled,
                IsPosted = t.IsPosted,
                ApprovalStatus = t.ApprovalStatus,
                ApprovalStatusName = t.ApprovalStatus.ToString(),
                WorkflowInstanceId = t.WorkflowInstanceId,
                JournalEntryId = t.JournalEntryId,
                PostedDate = t.PostedDate
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
        var query = _context.Set<CashTransaction>()
            .Where(t => t.TenantId == tenantId && t.BankAccountId == bankAccountId && !t.IsDeleted);

        if (fromDate.HasValue)
            query = query.Where(t => t.TransactionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.TransactionDate <= toDate.Value);

        return await query
            .Select(t => new CashTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                Amount = t.Amount,
                Currency = t.Currency,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                IsReconciled = t.IsReconciled,
                IsPosted = t.IsPosted,
                ApprovalStatus = t.ApprovalStatus,
                ApprovalStatusName = t.ApprovalStatus.ToString(),
                WorkflowInstanceId = t.WorkflowInstanceId,
                JournalEntryId = t.JournalEntryId,
                PostedDate = t.PostedDate
            })
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CashTransactionDto>> GetUnreconciledAsync(Guid bankAccountId)
    {
        var tenantId = TenantId;
        return await _context.Set<CashTransaction>()
            .Where(t => t.TenantId == tenantId && t.BankAccountId == bankAccountId && !t.IsReconciled && !t.IsDeleted)
            .Select(t => new CashTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                Amount = t.Amount,
                Currency = t.Currency,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                ReferenceNumber = t.ReferenceNumber,
                IsPosted = t.IsPosted,
                ApprovalStatus = t.ApprovalStatus,
                ApprovalStatusName = t.ApprovalStatus.ToString(),
                WorkflowInstanceId = t.WorkflowInstanceId,
                JournalEntryId = t.JournalEntryId
            })
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<CashTransactionDto> CreateReceiptAsync(CreateCashReceiptDto dto)
    {
        var tenantId = TenantId;
        await ValidateBankAccountAsync(dto.BankAccountId, tenantId, "receipt");
        if (dto.GLAccountId.HasValue)
        {
            await ValidateGLAccountAsync(dto.GLAccountId.Value, tenantId, "receipt");
        }

        var transactionNumber = await GenerateTransactionNumberAsync(FinanceDocumentTypes.CashReceipt, dto.TransactionDate);
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        var transactionCurrency = string.IsNullOrWhiteSpace(dto.Currency) ? baseCurrencyCode : dto.Currency.Trim().ToUpperInvariant();
        var exchangeRate = ResolveExchangeRate(transactionCurrency, baseCurrencyCode, dto.ExchangeRate);
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

        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to create receipt");
    }

    public async Task<CashTransactionDto> CreatePaymentAsync(CreateCashPaymentDto dto)
    {
        var tenantId = TenantId;
        await ValidateBankAccountAsync(dto.BankAccountId, tenantId, "payment");
        if (dto.GLAccountId.HasValue)
        {
            await ValidateGLAccountAsync(dto.GLAccountId.Value, tenantId, "payment");
        }

        var transactionNumber = await GenerateTransactionNumberAsync(FinanceDocumentTypes.CashPayment, dto.TransactionDate);
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        var transactionCurrency = string.IsNullOrWhiteSpace(dto.Currency) ? baseCurrencyCode : dto.Currency.Trim().ToUpperInvariant();
        var exchangeRate = ResolveExchangeRate(transactionCurrency, baseCurrencyCode, dto.ExchangeRate);
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

        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to create payment");
    }

    public async Task<(CashTransactionDto FromTransaction, CashTransactionDto ToTransaction)> CreateTransferAsync(CreateBankTransferDto dto)
    {
        if (dto.Amount <= 0m)
        {
            throw new InvalidOperationException("Transfer amount must be greater than zero.");
        }

        if (dto.FromBankAccountId == dto.ToBankAccountId)
        {
            throw new InvalidOperationException("Source and destination bank accounts must be different.");
        }

        var tenantId = TenantId;
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();

        var baseCurrencyCode = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync());
        var fromBankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == dto.FromBankAccountId && !a.IsDeleted)
            ?? throw new InvalidOperationException("Source bank account not found.");
        var toBankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == dto.ToBankAccountId && !a.IsDeleted)
            ?? throw new InvalidOperationException("Destination bank account not found.");

        ValidateTransferAccount(fromBankAccount, "Source");
        ValidateTransferAccount(toBankAccount, "Destination");

        if (fromBankAccount.GLAccountId == toBankAccount.GLAccountId)
        {
            throw new InvalidOperationException("Source and destination bank accounts must be linked to different GL accounts.");
        }

        var fromCurrencyCode = NormalizeCurrency(fromBankAccount.Currency);
        var toCurrencyCode = NormalizeCurrency(toBankAccount.Currency);
        if (!string.Equals(fromCurrencyCode, toCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cross-currency bank transfers are not supported by this endpoint yet.");
        }

        var exchangeRate = ResolveExchangeRate(fromCurrencyCode, baseCurrencyCode, dto.ExchangeRate);
        var baseAmount = ToBaseAmount(dto.Amount, exchangeRate);
        if (baseAmount <= 0m)
        {
            throw new InvalidOperationException("Transfer base amount must be greater than zero.");
        }

        await ValidateGLAccountAsync(fromBankAccount.GLAccountId!.Value, tenantId, "source");
        await ValidateGLAccountAsync(toBankAccount.GLAccountId!.Value, tenantId, "destination");

        var transactionNumber = await GenerateTransactionNumberAsync(FinanceDocumentTypes.BankTransfer, dto.TransactionDate);
        var description = string.IsNullOrWhiteSpace(dto.Description)
            ? $"Bank transfer from {fromBankAccount.AccountName} to {toBankAccount.AccountName}"
            : dto.Description.Trim();

        var fromTransaction = new CashTransaction
        {
            TenantId = tenantId,
            TransactionNumber = $"{transactionNumber}-OUT",
            TransactionDate = dto.TransactionDate,
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = dto.FromBankAccountId,
            ToBankAccountId = dto.ToBankAccountId,
            Amount = dto.Amount,
            Currency = fromCurrencyCode,
            ExchangeRate = exchangeRate,
            BaseAmount = baseAmount,
            ReferenceNumber = dto.ReferenceNumber,
            Description = description,
            IsReconciled = false,
            IsPosted = false,
            ApprovalStatus = CashTransactionApprovalStatus.Captured
        };

        var toTransaction = new CashTransaction
        {
            TenantId = tenantId,
            TransactionNumber = $"{transactionNumber}-IN",
            TransactionDate = dto.TransactionDate,
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = dto.ToBankAccountId,
            ToBankAccountId = dto.FromBankAccountId,
            Amount = dto.Amount,
            Currency = toCurrencyCode,
            ExchangeRate = exchangeRate,
            BaseAmount = baseAmount,
            ReferenceNumber = dto.ReferenceNumber,
            Description = description,
            IsReconciled = false,
            IsPosted = false,
            ApprovalStatus = CashTransactionApprovalStatus.Captured
        };

        _context.Set<CashTransaction>().AddRange(fromTransaction, toTransaction);
        await _context.SaveChangesAsync();

        await RecordCashBankAuditAsync(
            FinanceAuditEvents.CashBankTransactionCaptured,
            fromTransaction,
            afterValues: new
                {
                    fromTransaction.Id,
                    sourceTransactionNumber = fromTransaction.TransactionNumber,
                    fromTransaction.TransactionType,
                    fromTransaction.BankAccountId,
                    fromTransaction.ToBankAccountId,
                    toTransactionId = toTransaction.Id,
                    destinationTransactionNumber = toTransaction.TransactionNumber,
                    Amount = dto.Amount,
                    Currency = fromCurrencyCode,
                    BaseAmount = baseAmount
                },
            comment: "Bank transfer captured as an unposted operational transaction pair.",
            cancellationToken: default);

        await dbTransaction.CommitAsync();

        var fromDto = await GetByIdAsync(fromTransaction.Id) ?? throw new Exception("Failed to create transfer");
        var toDto = await GetByIdAsync(toTransaction.Id) ?? throw new Exception("Failed to create transfer");

        return (fromDto, toDto);
    }

    public async Task<CashTransactionDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await LoadCashTransactionForWorkflowAsync(id, cancellationToken);
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

        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to load submitted cash/bank transaction");
    }

    public async Task<CashTransactionDto> ApproveAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default)
    {
        var transaction = await LoadCashTransactionForWorkflowAsync(id, cancellationToken);
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
        var transaction = await LoadCashTransactionForWorkflowAsync(id, cancellationToken);
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
        var transaction = await LoadCashTransactionForWorkflowAsync(id, cancellationToken);
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

        var transaction = await LoadCashTransactionForWorkflowAsync(id, cancellationToken);
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

    public async Task<CashTransactionDto> PostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_financePostingEngine == null)
        {
            throw new InvalidOperationException("Central finance posting engine is not configured for cash/bank transaction posting.");
        }

        var requestedTransaction = await LoadCashTransactionForPostingAsync(id, cancellationToken);
        var sourceTransaction = await ResolvePostingSourceTransactionAsync(requestedTransaction, cancellationToken);
        var wasAlreadyLinked = sourceTransaction.JournalEntryId.HasValue;

        await EnforceCashBankPostingEligibilityAsync(sourceTransaction, cancellationToken);

        try
        {
            var postingRequest = await BuildCashBankPostingRequestAsync(sourceTransaction, cancellationToken);
            var postingResult = await _financePostingEngine.PostAsync(postingRequest, cancellationToken);

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
            }

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

            return await GetByIdAsync(id) ?? await GetByIdAsync(sourceTransaction.Id) ?? throw new Exception("Failed to load posted cash/bank transaction");
        }
        catch (Exception ex)
        {
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

    public async Task DeleteAsync(Guid id)
    {
        var tenantId = TenantId;
        var transaction = await _context.Set<CashTransaction>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted)
            ?? throw new Exception("Transaction not found");

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

    private async Task<CashTransaction> LoadCashTransactionForWorkflowAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        return await _context.Set<CashTransaction>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Cash/bank transaction was not found for this tenant.");
    }

    private async Task EnforceCashBankPostingEligibilityAsync(CashTransaction transaction, CancellationToken cancellationToken)
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

    private async Task<FinancePostingRequestDto> BuildCashBankPostingRequestAsync(
        CashTransaction transaction,
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

        var sourceDocumentType = GetCashBankSourceDocumentType(transaction);
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

        var lines = transaction.TransactionType switch
        {
            CashTransactionType.Receipt => await BuildReceiptLinesAsync(transaction, description, transactionCurrency, exchangeRate, tenantId, cancellationToken),
            CashTransactionType.Payment => await BuildPaymentLinesAsync(transaction, description, transactionCurrency, exchangeRate, tenantId, cancellationToken),
            CashTransactionType.Transfer => await BuildTransferLinesAsync(transaction, description, transactionCurrency, exchangeRate, tenantId, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported cash/bank transaction type.")
        };

        var totalDebit = lines.Sum(l => l.DebitAmount);
        var totalCredit = lines.Sum(l => l.CreditAmount);
        if (RoundMoney(totalDebit) != RoundMoney(totalCredit))
        {
            throw new InvalidOperationException("Cash/bank posting request is not balanced.");
        }

        return new FinancePostingRequestDto
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
            BookClassification = "IFRS",
            FunctionalCurrencyCode = baseCurrencyCode,
            IdempotencyKey = $"CASHBANK:{sourceDocumentType}:{tenantId:N}:{transaction.Id:N}:Post",
            ReturnExistingOnDuplicate = true,
            Lines = lines
        };
    }

    private async Task<IReadOnlyList<FinancePostingLineDto>> BuildReceiptLinesAsync(
        CashTransaction transaction,
        string description,
        string transactionCurrency,
        decimal exchangeRate,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!transaction.GLAccountId.HasValue)
        {
            throw new InvalidOperationException("Cash receipt requires an offset GL account before posting.");
        }

        await ValidatePostingAccountAsync(transaction.BankAccount.GLAccountId!.Value, tenantId, "cash receipt bank account", cancellationToken);
        await ValidatePostingAccountAsync(transaction.GLAccountId.Value, tenantId, "cash receipt offset account", cancellationToken);

        return new List<FinancePostingLineDto>
        {
            CreatePostingLine(transaction.BankAccount.GLAccountId.Value, description, debitAmount: transaction.BaseAmount, creditAmount: 0m, transaction, transactionCurrency, exchangeRate, 1, "CashBankReceipt.Bank"),
            CreatePostingLine(transaction.GLAccountId.Value, description, debitAmount: 0m, creditAmount: transaction.BaseAmount, transaction, transactionCurrency, exchangeRate, 2, "CashBankReceipt.Offset")
        };
    }

    private async Task<IReadOnlyList<FinancePostingLineDto>> BuildPaymentLinesAsync(
        CashTransaction transaction,
        string description,
        string transactionCurrency,
        decimal exchangeRate,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!transaction.GLAccountId.HasValue)
        {
            throw new InvalidOperationException("Cash payment requires an offset GL account before posting.");
        }

        await ValidatePostingAccountAsync(transaction.BankAccount.GLAccountId!.Value, tenantId, "cash payment bank account", cancellationToken);
        await ValidatePostingAccountAsync(transaction.GLAccountId.Value, tenantId, "cash payment offset account", cancellationToken);

        return new List<FinancePostingLineDto>
        {
            CreatePostingLine(transaction.GLAccountId.Value, description, debitAmount: transaction.BaseAmount, creditAmount: 0m, transaction, transactionCurrency, exchangeRate, 1, "CashBankPayment.Offset"),
            CreatePostingLine(transaction.BankAccount.GLAccountId.Value, description, debitAmount: 0m, creditAmount: transaction.BaseAmount, transaction, transactionCurrency, exchangeRate, 2, "CashBankPayment.Bank")
        };
    }

    private async Task<IReadOnlyList<FinancePostingLineDto>> BuildTransferLinesAsync(
        CashTransaction transaction,
        string description,
        string transactionCurrency,
        decimal exchangeRate,
        Guid tenantId,
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

        await ValidatePostingAccountAsync(transaction.BankAccount.GLAccountId!.Value, tenantId, "transfer source bank account", cancellationToken);
        await ValidatePostingAccountAsync(transaction.ToBankAccount.GLAccountId.Value, tenantId, "transfer destination bank account", cancellationToken);

        return new List<FinancePostingLineDto>
        {
            CreatePostingLine(transaction.ToBankAccount.GLAccountId.Value, description, debitAmount: transaction.BaseAmount, creditAmount: 0m, transaction, transactionCurrency, exchangeRate, 1, "CashBankTransfer.Destination"),
            CreatePostingLine(transaction.BankAccount.GLAccountId.Value, description, debitAmount: 0m, creditAmount: transaction.BaseAmount, transaction, transactionCurrency, exchangeRate, 2, "CashBankTransfer.Source")
        };
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
        string transactionTag)
    {
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            Description = description,
            DebitAmount = RoundMoney(debitAmount),
            CreditAmount = RoundMoney(creditAmount),
            TransactionCurrency = transactionCurrency,
            ForeignCurrencyAmount = transaction.Amount,
            ExchangeRate = exchangeRate,
            ExchangeRateSource = "CashBankTransaction",
            ExchangeRateDate = transaction.TransactionDate.Date,
            SourceReferenceNumber = string.IsNullOrWhiteSpace(transaction.ReferenceNumber)
                ? transaction.TransactionNumber
                : transaction.ReferenceNumber,
            LineNumber = lineNumber,
            TransactionTag = transactionTag
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

    private static string GetCashBankSourceDocumentType(CashTransaction transaction)
        => transaction.TransactionType switch
        {
            CashTransactionType.Receipt => "CashBankReceipt",
            CashTransactionType.Payment => "CashBankPayment",
            CashTransactionType.Transfer => "CashBankTransfer",
            _ => "CashBankTransaction"
        };

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
            && transaction.TransactionNumber.EndsWith("-IN", StringComparison.OrdinalIgnoreCase);

    private static bool IsOutgoingTransferLeg(CashTransaction transaction)
        => transaction.TransactionType == CashTransactionType.Transfer
            && transaction.TransactionNumber.EndsWith("-OUT", StringComparison.OrdinalIgnoreCase);

    private static decimal RoundMoney(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

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

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = transaction.TenantId,
            SourceModule = "CASHBANK",
            SourceDocumentType = GetCashBankSourceDocumentType(transaction),
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
                transaction.GLAccountId,
                transaction.Amount,
                transaction.Currency,
                transaction.BaseAmount,
                transaction.ApprovalStatus,
                transaction.WorkflowInstanceId,
                transaction.IsPosted,
                transaction.IsReconciled
            },
            Resource = "Finance.CashTransaction",
            ResourceId = transaction.Id.ToString()
        }, cancellationToken);
    }

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
