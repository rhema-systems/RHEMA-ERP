using System.Data;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Cash;

/// <summary>
/// Owns the operational settlement subledger. Source documents post to liquidity accounts first;
/// an approved deposit then creates one bank-facing transaction for its net amount.
/// </summary>
public sealed class BankingSettlementService : IBankingSettlementService
{
    private const long MaximumEvidenceFileSize = 10 * 1024 * 1024;
    private const string DepositWorkflowEntityType = "BankDepositBatch";
    private const string ReturnedChequeWorkflowEntityType = "ReturnedChequeCase";
    private static readonly LiquidityAccountType[] RequiredHoldingTypes =
    [
        LiquidityAccountType.UndepositedCash,
        LiquidityAccountType.ChequesAwaitingDeposit,
        LiquidityAccountType.MobileMoneyClearing,
        LiquidityAccountType.CardSettlementClearing
    ];

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _numbering;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IFinancePostingEngine _postingEngine;

    public BankingSettlementService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDocumentNumberingService numbering,
        IWorkflowIntegrationService workflow,
        IFinancePostingEngine postingEngine)
    {
        _context = context;
        _currentUser = currentUser;
        _numbering = numbering;
        _workflow = workflow;
        _postingEngine = postingEngine;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    private Guid UserId
        => Guid.TryParse(_currentUser.UserId, out var userId) && userId != Guid.Empty
            ? userId
            : throw new InvalidOperationException("An authenticated application user is required.");

    public async Task<IReadOnlyList<LiquidityAccountDto>> GetLiquidityAccountsAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var query = _context.LiquidityAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Include(item => item.GLAccount)
            .Include(item => item.BankAccount)
            .AsQueryable();
        if (activeOnly)
        {
            query = query.Where(item => item.IsActive);
        }

        var accounts = await query.OrderBy(item => item.Name).ToListAsync(cancellationToken);
        return await MapLiquidityAccountsAsync(accounts, cancellationToken);
    }

    public async Task<LiquidityAccountDto?> GetLiquidityAccountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var account = await _context.LiquidityAccounts
            .AsNoTracking()
            .Include(item => item.GLAccount)
            .Include(item => item.BankAccount)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);
        if (account == null)
        {
            return null;
        }

        return (await MapLiquidityAccountsAsync([account], cancellationToken)).Single();
    }

    public async Task<LiquidityAccountDto> CreateLiquidityAccountAsync(
        CreateLiquidityAccountDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        await ValidateLiquidityMasterAsync(
            dto.Code,
            dto.AccountType,
            dto.Currency,
            dto.GLAccountId,
            dto.BankAccountId,
            null,
            cancellationToken);

        var account = new LiquidityAccount
        {
            TenantId = tenantId,
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = RequireText(dto.Name, "Account name", 200),
            AccountType = dto.AccountType,
            Currency = NormalizeCurrency(dto.Currency),
            GLAccountId = dto.GLAccountId,
            BankAccountId = dto.BankAccountId,
            ProviderName = Clean(dto.ProviderName),
            ProviderAccountReference = Clean(dto.ProviderAccountReference),
            AllowsNegativeBalance = dto.AllowsNegativeBalance,
            AllowsManualAllocations = dto.AllowsManualAllocations,
            Notes = Clean(dto.Notes),
            IsActive = true,
            IsSystemAccount = false,
            CreatedById = UserId,
            CreatedBy = _currentUser.UserName
        };
        _context.LiquidityAccounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetLiquidityAccountAsync(account.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the liquidity account.");
    }

    public async Task<LiquidityAccountDto> UpdateLiquidityAccountAsync(
        Guid id,
        UpdateLiquidityAccountDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var account = await _context.LiquidityAccounts
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Liquidity account was not found.");

        SetRowVersion(account, dto.RowVersion);
        await ValidateLiquidityMasterAsync(
            account.Code,
            account.AccountType,
            account.Currency,
            dto.GLAccountId,
            dto.BankAccountId,
            account.Id,
            cancellationToken);

        account.Name = RequireText(dto.Name, "Account name", 200);
        account.GLAccountId = dto.GLAccountId;
        account.BankAccountId = dto.BankAccountId;
        account.ProviderName = Clean(dto.ProviderName);
        account.ProviderAccountReference = Clean(dto.ProviderAccountReference);
        account.AllowsNegativeBalance = dto.AllowsNegativeBalance;
        account.AllowsManualAllocations = dto.AllowsManualAllocations;
        account.IsActive = dto.IsActive;
        account.Notes = Clean(dto.Notes);
        StampModified(account);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetLiquidityAccountAsync(account.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the liquidity account.");
    }

    public async Task<BankingSetupStatusDto> GetSetupStatusAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var settings = await GetSettingsAsync(cancellationToken);
        var accounts = await GetLiquidityAccountsAsync(activeOnly: true, cancellationToken);
        var configured = accounts.Select(item => item.AccountType).ToHashSet();
        var missing = RequiredHoldingTypes.Where(item => !configured.Contains(item)).ToArray();
        return new BankingSetupStatusDto
        {
            IsConfigured = missing.Length == 0,
            RequiresProvisioningWizard = missing.Length != 0,
            CoaType = settings.CoaType,
            BaseCurrency = settings.BaseCurrency,
            DepositPolicy = settings.BankDepositPolicy,
            RequirePrimaryEvidence = settings.RequireBankDepositPrimaryEvidence,
            MissingAccountTypes = missing,
            Accounts = accounts
        };
    }

    public async Task<BankingSetupStatusDto> CompleteSetupAsync(
        CompleteBankingSetupDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var settings = await GetSettingsAsync(cancellationToken);
        var submittedTypes = dto.Accounts.Select(item => item.AccountType).ToHashSet();
        var existingTypes = await _context.LiquidityAccounts
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .Select(item => item.AccountType)
            .ToListAsync(cancellationToken);
        var unresolved = RequiredHoldingTypes
            .Where(item => !existingTypes.Contains(item) && !submittedTypes.Contains(item))
            .ToArray();
        if (unresolved.Length != 0)
        {
            throw new InvalidOperationException(
                $"Map a GL control account for every required holding type: {string.Join(", ", unresolved)}.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        foreach (var line in dto.Accounts)
        {
            if (existingTypes.Contains(line.AccountType))
            {
                continue;
            }

            await ValidateLiquidityMasterAsync(
                line.Code,
                line.AccountType,
                line.Currency,
                line.GLAccountId,
                null,
                null,
                cancellationToken);
            _context.LiquidityAccounts.Add(new LiquidityAccount
            {
                TenantId = tenantId,
                Code = line.Code.Trim().ToUpperInvariant(),
                Name = RequireText(line.Name, "Account name", 200),
                AccountType = line.AccountType,
                Currency = NormalizeCurrency(line.Currency),
                GLAccountId = line.GLAccountId,
                IsActive = true,
                IsSystemAccount = true,
                AllowsManualAllocations = true,
                CreatedById = UserId,
                CreatedBy = _currentUser.UserName,
                Notes = "Created by the Banking & Settlement setup wizard."
            });
        }

        settings.BankDepositPolicy = dto.DepositPolicy;
        settings.RequireBankDepositPrimaryEvidence = dto.RequirePrimaryEvidence;
        StampModified(settings);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetSetupStatusAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LiquidityAccountEntryDto>> GetEligibleEntriesAsync(
        Guid? liquidityAccountId = null,
        string? currency = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var query = _context.LiquidityAccountEntries
            .AsNoTracking()
            .Include(item => item.LiquidityAccount)
            .Where(item => item.TenantId == tenantId &&
                           !item.IsReversed &&
                           item.Amount > item.AllocatedAmount &&
                           item.LiquidityAccount.IsActive);
        if (liquidityAccountId.HasValue)
        {
            query = query.Where(item => item.LiquidityAccountId == liquidityAccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            var normalized = NormalizeCurrency(currency);
            query = query.Where(item => item.Currency == normalized);
        }

        var entries = await query
            .OrderBy(item => item.EntryDate)
            .ThenBy(item => item.EntryNumber)
            .ToListAsync(cancellationToken);
        return entries.Select(MapEntry).ToArray();
    }

    public async Task<IReadOnlyList<PostedLiquidityPaymentCandidateDto>> GetPostedPaymentCandidatesAsync(
        Guid? liquidityAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var query =
            from line in _context.AccountTransactions.AsNoTracking()
            join liquidityAccount in _context.LiquidityAccounts.AsNoTracking()
                on line.AccountId equals liquidityAccount.GLAccountId
            where line.TenantId == tenantId
                  && liquidityAccount.TenantId == tenantId
                  && liquidityAccount.IsActive
                  && liquidityAccount.AccountType != LiquidityAccountType.Bank
                  && line.JournalEntry.PostingStatus == "Posted"
                  && line.JournalEntry.SourceDocumentType != DepositWorkflowEntityType
                  && line.JournalEntry.SourceDocumentType != ReturnedChequeWorkflowEntityType
                  && line.CreditAmount > line.DebitAmount
                  && line.FunctionalCurrencyCode == liquidityAccount.Currency
                  && !(line.JournalEntry.SourceDocumentId != null
                       && _context.LiquidityAccountEntries.Any(entry =>
                           entry.TenantId == tenantId
                           && entry.SourceDocumentType == line.JournalEntry.SourceDocumentType
                           && entry.SourceDocumentId == line.JournalEntry.SourceDocumentId))
                  && !_context.LiquidityAccountEntries.Any(entry =>
                      entry.TenantId == tenantId
                      && entry.SourceDocumentType == nameof(AccountTransaction)
                      && entry.SourceDocumentId == line.Id)
            select new PostedLiquidityPaymentCandidateDto
            {
                AccountTransactionId = line.Id,
                JournalEntryId = line.JournalEntryId,
                JournalEntryNumber = line.JournalEntry.JournalEntryNumber,
                TransactionDate = line.TransactionDate,
                LiquidityAccountId = liquidityAccount.Id,
                LiquidityAccountName = liquidityAccount.Name,
                Currency = liquidityAccount.Currency,
                Amount = line.CreditAmount - line.DebitAmount,
                ReferenceNumber = line.JournalEntry.ReferenceNumber,
                Description = line.Description ?? line.JournalEntry.Description
            };

        if (liquidityAccountId.HasValue)
        {
            query = query.Where(item => item.LiquidityAccountId == liquidityAccountId.Value);
        }

        return await query
            .OrderBy(item => item.TransactionDate)
            .ThenBy(item => item.JournalEntryNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<LiquidityAccountEntryDto> RegisterPostedPaymentAsync(
        RegisterPostedLiquidityPaymentDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.EntryType is not (
            LiquidityEntryType.CashExpense
            or LiquidityEntryType.PettyCashReplenishment
            or LiquidityEntryType.CustomerRefund
            or LiquidityEntryType.OtherPayment))
        {
            throw new InvalidOperationException(
                "Posted payment entries must be classified as a cash expense, petty-cash replenishment, customer refund, or other payment.");
        }

        var tenantId = TenantId;
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var line = await _context.AccountTransactions
            .Include(item => item.JournalEntry)
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == dto.AccountTransactionId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Posted GL payment line was not found.");
        if (line.JournalEntry.PostingStatus != "Posted")
        {
            throw new InvalidOperationException("Only posted GL journal lines can enter the banking queue.");
        }
        if (line.JournalEntry.SourceDocumentType is DepositWorkflowEntityType or ReturnedChequeWorkflowEntityType)
        {
            throw new InvalidOperationException(
                "A banking-generated journal cannot be registered again as a payment deduction.");
        }
        if (line.JournalEntry.SourceDocumentId.HasValue
            && await _context.LiquidityAccountEntries.AnyAsync(
                item => item.TenantId == tenantId
                        && item.SourceDocumentType == line.JournalEntry.SourceDocumentType
                        && item.SourceDocumentId == line.JournalEntry.SourceDocumentId.Value,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The journal's source document is already represented in the banking queue.");
        }

        var liquidityAccount = await _context.LiquidityAccounts
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId
                        && item.GLAccountId == line.AccountId
                        && item.IsActive
                        && item.AccountType != LiquidityAccountType.Bank,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The GL line does not credit an active non-bank liquidity control account.");
        if (!line.FunctionalCurrencyCode.Equals(liquidityAccount.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The posted payment and liquidity account must use the same currency in this release.");
        }

        var amount = line.CreditAmount - line.DebitAmount;
        if (amount <= 0)
        {
            throw new InvalidOperationException(
                "The selected GL line must be a net credit to the liquidity control account.");
        }

        var alreadyRegistered = await _context.LiquidityAccountEntries.AnyAsync(
            item => item.TenantId == tenantId
                    && item.SourceDocumentType == nameof(AccountTransaction)
                    && item.SourceDocumentId == line.Id,
            cancellationToken);
        if (alreadyRegistered)
        {
            throw new InvalidOperationException("This posted payment is already in the banking queue.");
        }

        var entry = new LiquidityAccountEntry
        {
            TenantId = tenantId,
            LiquidityAccountId = liquidityAccount.Id,
            EntryNumber = await _numbering.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.LiquidityEntry,
                tenantId,
                line.TransactionDate,
                nameof(LiquidityAccountEntry),
                cancellationToken: cancellationToken),
            EntryDate = line.TransactionDate,
            EntryType = dto.EntryType,
            Direction = LiquidityEntryDirection.Decrease,
            Amount = amount,
            AllocatedAmount = 0m,
            Currency = liquidityAccount.Currency,
            SourceDocumentType = nameof(AccountTransaction),
            SourceDocumentId = line.Id,
            ReferenceNumber = line.JournalEntry.ReferenceNumber ?? line.JournalEntry.JournalEntryNumber,
            Description = line.Description ?? line.JournalEntry.Description,
            CreatedById = UserId,
            CreatedBy = _currentUser.UserName
        };
        _context.LiquidityAccountEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        entry.LiquidityAccount = liquidityAccount;
        return MapEntry(entry);
    }

    public async Task<IReadOnlyList<BankDepositDto>> GetDepositsAsync(
        BankDepositStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var query = DepositQuery().Where(item => item.TenantId == tenantId);
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        var deposits = await query
            .OrderByDescending(item => item.DepositDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return deposits.Select(MapDeposit).ToArray();
    }

    public async Task<BankDepositDto?> GetDepositAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var deposit = await DepositQuery()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);
        return deposit == null ? null : MapDeposit(deposit);
    }

    public async Task<BankDepositDto> CreateDepositAsync(
        CreateBankDepositDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var prepared = await PrepareDepositAsync(dto.BankAccountId, dto.Allocations, cancellationToken);
        var deposit = new BankDepositBatch
        {
            TenantId = tenantId,
            DepositNumber = await _numbering.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.BankDeposit,
                tenantId,
                dto.DepositDate,
                DepositWorkflowEntityType,
                cancellationToken: cancellationToken),
            BankAccountId = dto.BankAccountId,
            DepositDate = dto.DepositDate.Date,
            DepositReference = RequireText(dto.DepositReference, "Deposit reference", 100),
            Currency = prepared.Currency,
            PolicySnapshot = prepared.Settings.BankDepositPolicy,
            TotalReceipts = prepared.TotalReceipts,
            TotalDeductions = prepared.TotalDeductions,
            NetAmount = prepared.NetAmount,
            Notes = Clean(dto.Notes),
            CreatedById = UserId,
            CreatedBy = _currentUser.UserName
        };
        _context.BankDepositBatches.Add(deposit);
        ReserveAllocations(deposit, dto.Allocations, prepared.Entries);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetDepositAsync(deposit.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the bank deposit.");
    }

    public async Task<BankDepositDto> UpdateDepositAsync(
        Guid id,
        UpdateBankDepositDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var deposit = await _context.BankDepositBatches
            .Include(item => item.Allocations)
            .ThenInclude(item => item.LiquidityAccountEntry)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Bank deposit was not found.");
        EnsureEditable(deposit);
        SetRowVersion(deposit, dto.RowVersion);

        foreach (var allocation in deposit.Allocations)
        {
            allocation.LiquidityAccountEntry.AllocatedAmount -= allocation.Amount;
        }
        _context.BankDepositAllocations.RemoveRange(deposit.Allocations);
        await _context.SaveChangesAsync(cancellationToken);

        var prepared = await PrepareDepositAsync(dto.BankAccountId, dto.Allocations, cancellationToken);
        deposit.BankAccountId = dto.BankAccountId;
        deposit.DepositDate = dto.DepositDate.Date;
        deposit.DepositReference = RequireText(dto.DepositReference, "Deposit reference", 100);
        deposit.Currency = prepared.Currency;
        deposit.PolicySnapshot = prepared.Settings.BankDepositPolicy;
        deposit.TotalReceipts = prepared.TotalReceipts;
        deposit.TotalDeductions = prepared.TotalDeductions;
        deposit.NetAmount = prepared.NetAmount;
        deposit.Notes = Clean(dto.Notes);
        StampModified(deposit);
        ReserveAllocations(deposit, dto.Allocations, prepared.Entries);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetDepositAsync(deposit.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the bank deposit.");
    }

    public async Task<BankDepositDto> LinkDepositAttachmentAsync(
        Guid id,
        LinkBankingAttachmentDto dto,
        CancellationToken cancellationToken = default)
    {
        var deposit = await LoadDepositForActionAsync(id, cancellationToken);
        EnsureEditable(deposit);
        var file = await GetValidFileAsync(dto.FileUploadRecordId, cancellationToken);
        var exists = await _context.BankDepositAttachments.AnyAsync(
            item => item.TenantId == TenantId &&
                    item.BankDepositBatchId == id &&
                    item.FileUploadRecordId == file.Id,
            cancellationToken);
        if (!exists)
        {
            _context.BankDepositAttachments.Add(new BankDepositAttachment
            {
                TenantId = TenantId,
                BankDepositBatchId = id,
                FileUploadRecordId = file.Id,
                DocumentType = RequireText(dto.DocumentType, "Document type", 50),
                IsPrimaryEvidence = dto.IsPrimaryEvidence,
                CreatedById = UserId,
                CreatedBy = _currentUser.UserName
            });
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetDepositAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the bank deposit.");
    }

    public async Task<BankDepositDto> UnlinkDepositAttachmentAsync(
        Guid id,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var deposit = await LoadDepositForActionAsync(id, cancellationToken);
        EnsureEditable(deposit);
        var attachment = await _context.BankDepositAttachments.FirstOrDefaultAsync(
            item => item.TenantId == TenantId &&
                    item.BankDepositBatchId == id &&
                    item.Id == attachmentId,
            cancellationToken) ?? throw new KeyNotFoundException("Attachment link was not found.");
        _context.BankDepositAttachments.Remove(attachment);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetDepositAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the bank deposit.");
    }

    public async Task<BankDepositDto> SubmitDepositAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deposit = await LoadDepositForActionAsync(id, cancellationToken);
        if (deposit.Status is not BankDepositStatus.Draft and not BankDepositStatus.Returned)
        {
            throw new InvalidOperationException("Only draft or returned deposits can be submitted.");
        }

        var settings = await GetSettingsAsync(cancellationToken);
        if (settings.RequireBankDepositPrimaryEvidence &&
            !await _context.BankDepositAttachments.AnyAsync(
                item => item.TenantId == TenantId && item.BankDepositBatchId == id && item.IsPrimaryEvidence,
                cancellationToken))
        {
            throw new InvalidOperationException("A primary deposit slip or equivalent bank evidence is required before submission.");
        }

        var result = await _workflow.SubmitAsync(DepositWorkflowEntityType, id);
        deposit.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId ?? deposit.WorkflowInstanceId;
        deposit.Status = result.Outcome == WorkflowOutcome.Approved
            ? BankDepositStatus.Approved
            : BankDepositStatus.Submitted;
        deposit.SubmittedAt = DateTime.UtcNow;
        deposit.SubmittedById = UserId;
        StampModified(deposit);
        await _context.SaveChangesAsync(cancellationToken);
        if (deposit.Status == BankDepositStatus.Approved && settings.AutoPostBankDepositAfterApproval)
        {
            await PostDepositAsync(deposit, cancellationToken);
        }

        return await GetDepositAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the submitted deposit.");
    }

    public async Task<BankDepositDto> ApproveDepositAsync(
        Guid id,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var deposit = await LoadDepositForActionAsync(id, cancellationToken);
        if (deposit.Status != BankDepositStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted deposits can be approved.");
        }
        EnsureMakerChecker(deposit.SubmittedById, "approve");

        if (!await _workflow.CanUserApproveAsync(DepositWorkflowEntityType, id, UserId))
        {
            throw new UnauthorizedAccessException("The current user cannot approve this deposit.");
        }

        var result = await _workflow.ProcessApprovalAsync(DepositWorkflowEntityType, id, UserId, "Approve", comments);
        deposit.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId ?? deposit.WorkflowInstanceId;
        deposit.Status = result.Outcome == WorkflowOutcome.Approved
            ? BankDepositStatus.Approved
            : BankDepositStatus.Submitted;
        if (deposit.Status == BankDepositStatus.Approved)
        {
            deposit.ApprovedAt = DateTime.UtcNow;
            deposit.ApprovedById = UserId;
        }
        StampModified(deposit);
        await _context.SaveChangesAsync(cancellationToken);

        var settings = await GetSettingsAsync(cancellationToken);
        if (deposit.Status == BankDepositStatus.Approved && settings.AutoPostBankDepositAfterApproval)
        {
            await PostDepositAsync(deposit, cancellationToken);
        }

        return await GetDepositAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the approved deposit.");
    }

    public async Task<BankDepositDto> RejectDepositAsync(
        Guid id,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var deposit = await LoadDepositForActionAsync(id, cancellationToken);
        if (deposit.Status != BankDepositStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted deposits can be rejected.");
        }
        EnsureMakerChecker(deposit.SubmittedById, "reject");

        if (!await _workflow.CanUserApproveAsync(DepositWorkflowEntityType, id, UserId))
        {
            throw new UnauthorizedAccessException("The current user cannot reject this deposit.");
        }

        await _workflow.ProcessApprovalAsync(DepositWorkflowEntityType, id, UserId, "Reject", reason);
        await ReleaseDepositReservationsAsync(deposit, cancellationToken);
        deposit.Status = BankDepositStatus.Rejected;
        deposit.RejectionReason = Clean(reason);
        StampModified(deposit);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetDepositAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the rejected deposit.");
    }

    public async Task<BankDepositDto> ReturnDepositAsync(
        Guid id,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var deposit = await LoadDepositForActionAsync(id, cancellationToken);
        if (deposit.Status != BankDepositStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted deposits can be returned for changes.");
        }
        EnsureMakerChecker(deposit.SubmittedById, "return");

        if (!await _workflow.CanUserApproveAsync(DepositWorkflowEntityType, id, UserId))
        {
            throw new UnauthorizedAccessException("The current user cannot return this deposit.");
        }

        await _workflow.ProcessApprovalAsync(DepositWorkflowEntityType, id, UserId, "Return", comments);
        deposit.Status = BankDepositStatus.Returned;
        deposit.RejectionReason = Clean(comments);
        StampModified(deposit);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetDepositAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the returned deposit.");
    }

    public async Task<BankDepositDto> CancelDepositAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var deposit = await LoadDepositForActionAsync(id, cancellationToken);
        if (deposit.Status is BankDepositStatus.Posted or BankDepositStatus.Reversed)
        {
            throw new InvalidOperationException("Posted deposits must be reversed, not cancelled.");
        }

        if (deposit.WorkflowInstanceId.HasValue)
        {
            await _workflow.CancelWorkflowAsync(DepositWorkflowEntityType, id, RequireText(reason, "Cancellation reason", 1000));
        }

        await ReleaseDepositReservationsAsync(deposit, cancellationToken);
        deposit.Status = BankDepositStatus.Cancelled;
        deposit.CancellationReason = RequireText(reason, "Cancellation reason", 1000);
        StampModified(deposit);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetDepositAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the cancelled deposit.");
    }

    public async Task<BankDepositDto> PostDepositAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deposit = await LoadDepositForActionAsync(id, cancellationToken);
        if (deposit.Status != BankDepositStatus.Approved)
        {
            throw new InvalidOperationException("Only a fully approved deposit can be posted.");
        }

        await PostDepositAsync(deposit, cancellationToken);
        return await GetDepositAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the posted deposit.");
    }

    public async Task<IReadOnlyList<ReturnedChequeCaseDto>> GetReturnedChequesAsync(
        ReturnedChequeCaseStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var query = ReturnedChequeQuery().Where(item => item.TenantId == tenantId);
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        var cases = await query
            .OrderByDescending(item => item.ReturnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return await MapReturnedChequesAsync(cases, cancellationToken);
    }

    public async Task<ReturnedChequeCaseDto?> GetReturnedChequeAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var item = await ReturnedChequeQuery()
            .FirstOrDefaultAsync(value => value.TenantId == tenantId && value.Id == id, cancellationToken);
        return item == null
            ? null
            : (await MapReturnedChequesAsync([item], cancellationToken)).Single();
    }

    public async Task<ReturnedChequeCaseDto> CreateReturnedChequeAsync(
        CreateReturnedChequeCaseDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var payment = await _context.Set<CustomerPayment>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == dto.CustomerPaymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Customer receipt was not found.");
        if (string.IsNullOrWhiteSpace(payment.CheckNumber))
        {
            throw new InvalidOperationException("Only a cheque receipt can be processed as a returned cheque.");
        }
        if (payment.Status.Equals("Bounced", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This cheque receipt has already been marked as bounced.");
        }
        if (payment.JournalEntryId == null)
        {
            throw new InvalidOperationException("The cheque receipt must be posted before it can be returned.");
        }
        if (await _context.ReturnedChequeCases.AnyAsync(
                item => item.TenantId == tenantId
                        && item.CustomerPaymentId == payment.Id
                        && item.Status != ReturnedChequeCaseStatus.Rejected,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "An active returned-cheque case already exists for this receipt.");
        }

        var deposit = dto.BankDepositBatchId.HasValue
            ? await _context.BankDepositBatches
                .FirstOrDefaultAsync(
                    item => item.TenantId == tenantId &&
                            item.Id == dto.BankDepositBatchId.Value &&
                            item.Status == BankDepositStatus.Posted,
                    cancellationToken)
            : await _context.BankDepositBatches
                .Where(item => item.TenantId == tenantId && item.Status == BankDepositStatus.Posted)
                .Where(item => item.Allocations.Any(
                    allocation => allocation.LiquidityAccountEntry.SourceDocumentType == "CustomerPayment" &&
                                  allocation.LiquidityAccountEntry.SourceDocumentId == payment.Id))
                .OrderByDescending(item => item.DepositDate)
                .FirstOrDefaultAsync(cancellationToken);
        if (deposit == null)
        {
            throw new InvalidOperationException("The cheque must belong to a posted bank deposit before it can be returned.");
        }
        if (deposit.BankAccountId != dto.BankAccountId)
        {
            throw new InvalidOperationException("The returned cheque bank account must match the posted deposit.");
        }
        if (dto.ReturnDate.Date < deposit.DepositDate.Date)
        {
            throw new InvalidOperationException("The cheque return date cannot be before its deposit date.");
        }

        var (customerCharge, expenseCharge) = SplitCharge(dto);
        var item = new ReturnedChequeCase
        {
            TenantId = tenantId,
            CaseNumber = await _numbering.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.ReturnedCheque,
                tenantId,
                dto.ReturnDate,
                ReturnedChequeWorkflowEntityType,
                cancellationToken: cancellationToken),
            CustomerPaymentId = payment.Id,
            BankDepositBatchId = deposit.Id,
            BankAccountId = deposit.BankAccountId,
            ChequeNumber = payment.CheckNumber,
            DrawerBank = Clean(dto.DrawerBank ?? payment.ChequeDrawerBank),
            ReturnDate = dto.ReturnDate.Date,
            BankReference = RequireText(dto.BankReference, "Bank reference", 100),
            ReturnReason = RequireText(dto.ReturnReason, "Return reason", 500),
            ReturnedAmount = payment.TotalAmount,
            BankChargeAmount = dto.BankChargeAmount,
            ChargeTreatment = dto.ChargeTreatment,
            CustomerRecoverableChargeAmount = customerCharge,
            ExpenseChargeAmount = expenseCharge,
            Notes = Clean(dto.Notes),
            CreatedById = UserId,
            CreatedBy = _currentUser.UserName
        };
        _context.ReturnedChequeCases.Add(item);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetReturnedChequeAsync(item.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the returned cheque.");
    }

    public async Task<ReturnedChequeCaseDto> LinkReturnedChequeAttachmentAsync(
        Guid id,
        LinkBankingAttachmentDto dto,
        CancellationToken cancellationToken = default)
    {
        var item = await LoadReturnedChequeForActionAsync(id, cancellationToken);
        if (item.Status is not ReturnedChequeCaseStatus.Draft and not ReturnedChequeCaseStatus.Returned)
        {
            throw new InvalidOperationException("Attachments can only be changed while the case is draft or returned.");
        }
        var file = await GetValidFileAsync(dto.FileUploadRecordId, cancellationToken);
        var exists = await _context.ReturnedChequeAttachments.AnyAsync(
            link => link.TenantId == TenantId &&
                    link.ReturnedChequeCaseId == id &&
                    link.FileUploadRecordId == file.Id,
            cancellationToken);
        if (!exists)
        {
            _context.ReturnedChequeAttachments.Add(new ReturnedChequeAttachment
            {
                TenantId = TenantId,
                ReturnedChequeCaseId = id,
                FileUploadRecordId = file.Id,
                DocumentType = RequireText(dto.DocumentType, "Document type", 50),
                IsPrimaryEvidence = dto.IsPrimaryEvidence,
                CreatedById = UserId,
                CreatedBy = _currentUser.UserName
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        return await GetReturnedChequeAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the returned cheque.");
    }

    public async Task<ReturnedChequeCaseDto> SubmitReturnedChequeAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await LoadReturnedChequeForActionAsync(id, cancellationToken);
        if (item.Status is not ReturnedChequeCaseStatus.Draft and not ReturnedChequeCaseStatus.Returned)
        {
            throw new InvalidOperationException("Only draft or returned cheque cases can be submitted.");
        }
        if (!await _context.ReturnedChequeAttachments.AnyAsync(
                link => link.TenantId == TenantId && link.ReturnedChequeCaseId == id && link.IsPrimaryEvidence,
                cancellationToken))
        {
            throw new InvalidOperationException("Primary bank-return evidence is required before submission.");
        }

        var result = await _workflow.SubmitAsync(ReturnedChequeWorkflowEntityType, id);
        item.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId ?? item.WorkflowInstanceId;
        item.Status = result.Outcome == WorkflowOutcome.Approved
            ? ReturnedChequeCaseStatus.Approved
            : ReturnedChequeCaseStatus.Submitted;
        item.SubmittedAt = DateTime.UtcNow;
        item.SubmittedById = UserId;
        StampModified(item);
        await _context.SaveChangesAsync(cancellationToken);
        if (item.Status == ReturnedChequeCaseStatus.Approved)
        {
            await PostReturnedChequeAsync(item, cancellationToken);
        }
        return await GetReturnedChequeAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the returned cheque.");
    }

    public async Task<ReturnedChequeCaseDto> ApproveReturnedChequeAsync(
        Guid id,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var item = await LoadReturnedChequeForActionAsync(id, cancellationToken);
        if (item.Status != ReturnedChequeCaseStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted returned-cheque cases can be approved.");
        }
        EnsureMakerChecker(item.SubmittedById, "approve");
        if (!await _workflow.CanUserApproveAsync(ReturnedChequeWorkflowEntityType, id, UserId))
        {
            throw new UnauthorizedAccessException("The current user cannot approve this returned cheque.");
        }

        var result = await _workflow.ProcessApprovalAsync(ReturnedChequeWorkflowEntityType, id, UserId, "Approve", comments);
        item.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId ?? item.WorkflowInstanceId;
        item.Status = result.Outcome == WorkflowOutcome.Approved
            ? ReturnedChequeCaseStatus.Approved
            : ReturnedChequeCaseStatus.Submitted;
        if (item.Status == ReturnedChequeCaseStatus.Approved)
        {
            item.ApprovedAt = DateTime.UtcNow;
            item.ApprovedById = UserId;
        }
        StampModified(item);
        await _context.SaveChangesAsync(cancellationToken);
        if (item.Status == ReturnedChequeCaseStatus.Approved)
        {
            await PostReturnedChequeAsync(item, cancellationToken);
        }
        return await GetReturnedChequeAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the returned cheque.");
    }

    public async Task<ReturnedChequeCaseDto> RejectReturnedChequeAsync(
        Guid id,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var item = await LoadReturnedChequeForActionAsync(id, cancellationToken);
        if (item.Status != ReturnedChequeCaseStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted returned-cheque cases can be rejected.");
        }
        EnsureMakerChecker(item.SubmittedById, "reject");
        if (!await _workflow.CanUserApproveAsync(ReturnedChequeWorkflowEntityType, id, UserId))
        {
            throw new UnauthorizedAccessException("The current user cannot reject this returned cheque.");
        }
        await _workflow.ProcessApprovalAsync(ReturnedChequeWorkflowEntityType, id, UserId, "Reject", reason);
        item.Status = ReturnedChequeCaseStatus.Rejected;
        item.RejectionReason = Clean(reason);
        StampModified(item);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetReturnedChequeAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload the returned cheque.");
    }

    private async Task PostDepositAsync(BankDepositBatch deposit, CancellationToken cancellationToken)
    {
        if (deposit.Status == BankDepositStatus.Posted)
        {
            return;
        }

        var tenantId = TenantId;
        var loaded = await _context.BankDepositBatches
            .Include(item => item.BankAccount)
            .Include(item => item.Allocations)
            .ThenInclude(item => item.LiquidityAccountEntry)
            .ThenInclude(item => item.LiquidityAccount)
            .FirstAsync(item => item.TenantId == tenantId && item.Id == deposit.Id, cancellationToken);
        if (loaded.BankAccount.GLAccountId == null)
        {
            throw new InvalidOperationException("The destination bank account must be linked to a GL account.");
        }
        if (loaded.NetAmount <= 0)
        {
            throw new InvalidOperationException("A bank deposit must have a positive net amount.");
        }

        var lines = new List<FinancePostingLineDto>
        {
            new()
            {
                AccountId = loaded.BankAccount.GLAccountId.Value,
                DebitAmount = loaded.NetAmount,
                CreditAmount = 0m,
                TransactionCurrency = loaded.Currency,
                TransactionDebitAmount = loaded.NetAmount,
                Description = $"Bank deposit {loaded.DepositNumber}",
                SourceReferenceNumber = loaded.DepositReference
            }
        };
        foreach (var allocation in loaded.Allocations.OrderBy(item => item.CreatedAt))
        {
            lines.Add(new FinancePostingLineDto
            {
                AccountId = allocation.LiquidityAccountEntry.LiquidityAccount.GLAccountId,
                DebitAmount = allocation.AllocationType == BankDepositAllocationType.Deduction ? allocation.Amount : 0m,
                CreditAmount = allocation.AllocationType == BankDepositAllocationType.Receipt ? allocation.Amount : 0m,
                TransactionCurrency = loaded.Currency,
                TransactionDebitAmount = allocation.AllocationType == BankDepositAllocationType.Deduction ? allocation.Amount : 0m,
                TransactionCreditAmount = allocation.AllocationType == BankDepositAllocationType.Receipt ? allocation.Amount : 0m,
                Description = allocation.Notes ?? allocation.LiquidityAccountEntry.Description,
                SourceReferenceNumber = allocation.LiquidityAccountEntry.ReferenceNumber
            });
        }

        var posting = await _postingEngine.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "CashManagement",
            OriginModuleCode = FinanceModuleLockCatalog.Finance,
            SourceDocumentType = DepositWorkflowEntityType,
            SourceDocumentId = loaded.Id,
            SourceDocumentTenantId = tenantId,
            SourceDocumentReference = loaded.DepositNumber,
            Description = $"Bank deposit {loaded.DepositNumber} / {loaded.DepositReference}",
            PostingDate = loaded.DepositDate,
            FunctionalCurrencyCode = loaded.Currency,
            JournalType = "Bank Deposit",
            IdempotencyKey = $"bank-deposit:{tenantId:N}:{loaded.Id:N}:post",
            Lines = lines
        }, cancellationToken);

        var cashTransaction = await _context.Set<CashTransaction>()
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId &&
                        item.JournalEntryId == posting.JournalEntryId &&
                        item.TransactionType == CashTransactionType.Deposit,
                cancellationToken);
        if (cashTransaction == null)
        {
            cashTransaction = new CashTransaction
            {
                TenantId = tenantId,
                TransactionNumber = loaded.DepositNumber,
                TransactionDate = loaded.DepositDate,
                TransactionType = CashTransactionType.Deposit,
                BankAccountId = loaded.BankAccountId,
                Amount = loaded.NetAmount,
                Currency = loaded.Currency,
                BaseAmount = loaded.NetAmount,
                ReferenceNumber = loaded.DepositReference,
                PayeeOrPayer = "Bank deposit",
                Description = $"Settlement batch {loaded.DepositNumber}",
                IsPosted = true,
                ApprovalStatus = CashTransactionApprovalStatus.Posted,
                JournalEntryId = posting.JournalEntryId,
                PostedDate = posting.PostingDate,
                PostedBy = UserId,
                CreatedById = UserId,
                CreatedBy = _currentUser.UserName
            };
            _context.Set<CashTransaction>().Add(cashTransaction);
            loaded.BankAccount.CurrentBalance += loaded.NetAmount;
            loaded.BankAccount.AvailableBalance += loaded.NetAmount;
        }

        loaded.JournalEntryId = posting.JournalEntryId;
        loaded.CashTransactionId = cashTransaction.Id;
        loaded.Status = BankDepositStatus.Posted;
        loaded.PostedAt = posting.PostingDate;
        loaded.PostedById = UserId;
        StampModified(loaded);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task PostReturnedChequeAsync(ReturnedChequeCase item, CancellationToken cancellationToken)
    {
        if (item.Status == ReturnedChequeCaseStatus.Posted)
        {
            return;
        }

        var tenantId = TenantId;
        var loaded = await _context.ReturnedChequeCases
            .Include(value => value.BankAccount)
            .Include(value => value.CustomerPayment)
            .ThenInclude(value => value.Allocations)
            .ThenInclude(value => value.Invoice)
            .FirstAsync(value => value.TenantId == tenantId && value.Id == item.Id, cancellationToken);
        var settings = await GetSettingsAsync(cancellationToken);
        if (loaded.BankAccount.GLAccountId == null)
        {
            throw new InvalidOperationException("The bank account must be linked to a GL account.");
        }
        if (settings.ControlAccountArId == null)
        {
            throw new InvalidOperationException("Configure the AR control account before posting returned cheques.");
        }
        if (loaded.ExpenseChargeAmount > 0 && settings.ReturnedChequeBankChargeAccountId == null)
        {
            throw new InvalidOperationException("Configure the returned-cheque bank charge expense account.");
        }

        var activeAllocations = loaded.CustomerPayment.Allocations.Where(value => !value.IsReversal).ToArray();
        var activeAppliedAmount = activeAllocations.Sum(value => value.AllocatedAmount + value.DiscountAmount);
        var discountToReverse = activeAllocations.Sum(value => value.DiscountAmount);
        if (discountToReverse > 0m && settings.DiscountAllowedAccountId == null)
        {
            throw new InvalidOperationException(
                "Configure the discount-allowed account before reversing a cheque receipt that used a payment discount.");
        }

        var totalBankCredit = loaded.ReturnedAmount + loaded.BankChargeAmount;
        var lines = new List<FinancePostingLineDto>
        {
            new()
            {
                AccountId = settings.ControlAccountArId.Value,
                DebitAmount = loaded.ReturnedAmount + loaded.CustomerRecoverableChargeAmount + discountToReverse,
                CreditAmount = 0m,
                TransactionCurrency = loaded.CustomerPayment.CurrencyCode,
                TransactionDebitAmount = loaded.ReturnedAmount + loaded.CustomerRecoverableChargeAmount + discountToReverse,
                Description = $"Reopen customer receivable for returned cheque {loaded.ChequeNumber}",
                SourceReferenceNumber = loaded.BankReference
            },
            new()
            {
                AccountId = loaded.BankAccount.GLAccountId.Value,
                DebitAmount = 0m,
                CreditAmount = totalBankCredit,
                TransactionCurrency = loaded.CustomerPayment.CurrencyCode,
                TransactionCreditAmount = totalBankCredit,
                Description = $"Bank debit for returned cheque {loaded.ChequeNumber}",
                SourceReferenceNumber = loaded.BankReference
            }
        };
        if (loaded.ExpenseChargeAmount > 0)
        {
            lines.Add(new FinancePostingLineDto
            {
                AccountId = settings.ReturnedChequeBankChargeAccountId!.Value,
                DebitAmount = loaded.ExpenseChargeAmount,
                CreditAmount = 0m,
                TransactionCurrency = loaded.CustomerPayment.CurrencyCode,
                TransactionDebitAmount = loaded.ExpenseChargeAmount,
                Description = $"Bank charge for returned cheque {loaded.ChequeNumber}",
                SourceReferenceNumber = loaded.BankReference
            });
        }
        if (discountToReverse > 0m)
        {
            lines.Add(new FinancePostingLineDto
            {
                AccountId = settings.DiscountAllowedAccountId!.Value,
                DebitAmount = 0m,
                CreditAmount = discountToReverse,
                TransactionCurrency = loaded.CustomerPayment.CurrencyCode,
                TransactionCreditAmount = discountToReverse,
                Description = $"Reverse payment discount for returned cheque {loaded.ChequeNumber}",
                SourceReferenceNumber = loaded.BankReference
            });
        }

        var posting = await _postingEngine.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "AccountsReceivable",
            OriginModuleCode = FinanceModuleLockCatalog.Finance,
            SourceDocumentType = ReturnedChequeWorkflowEntityType,
            SourceDocumentId = loaded.Id,
            SourceDocumentTenantId = tenantId,
            SourceDocumentReference = loaded.CaseNumber,
            Description = $"Returned cheque {loaded.ChequeNumber}: {loaded.ReturnReason}",
            PostingDate = loaded.ReturnDate,
            FunctionalCurrencyCode = loaded.CustomerPayment.CurrencyCode,
            JournalType = "Returned Cheque",
            IdempotencyKey = $"returned-cheque:{tenantId:N}:{loaded.Id:N}:post",
            Lines = lines
        }, cancellationToken);

        foreach (var allocation in activeAllocations)
        {
            var totalApplied = allocation.AllocatedAmount + allocation.DiscountAmount;
            allocation.IsReversal = true;
            allocation.UpdatedAt = DateTime.UtcNow;
            allocation.UpdatedBy = _currentUser.UserName;
            allocation.Invoice.PaidAmount = Math.Max(0m, allocation.Invoice.PaidAmount - totalApplied);
            if (allocation.Invoice.BalanceAmount > 0m)
            {
                allocation.Invoice.Status = allocation.Invoice.PaidAmount > 0m
                    ? InvoiceStatus.PartiallyPaid
                    : InvoiceStatus.Sent;
            }
            allocation.Invoice.UpdatedAt = DateTime.UtcNow;
            allocation.Invoice.UpdatedBy = _currentUser.UserName;
        }

        loaded.CustomerPayment.Status = "Bounced";
        loaded.CustomerPayment.ClearedDate = null;
        loaded.CustomerPayment.AllocatedAmount = 0m;
        var customer = await _context.BusinessPartners.FirstOrDefaultAsync(
            value => value.TenantId == tenantId && value.Id == loaded.CustomerPayment.CustomerId,
            cancellationToken);
        if (customer != null)
        {
            customer.OutstandingBalance =
                (customer.OutstandingBalance ?? 0m)
                + activeAppliedAmount
                + loaded.CustomerRecoverableChargeAmount;
            customer.UpdatedAt = DateTime.UtcNow;
            customer.UpdatedBy = _currentUser.UserName;
        }
        var returnTransaction = new CashTransaction
        {
            TenantId = tenantId,
            TransactionNumber = loaded.CaseNumber,
            TransactionDate = loaded.ReturnDate,
            TransactionType = CashTransactionType.ReturnedCheque,
            BankAccountId = loaded.BankAccountId,
            Amount = totalBankCredit,
            Currency = loaded.CustomerPayment.CurrencyCode,
            BaseAmount = totalBankCredit,
            ReferenceNumber = loaded.BankReference,
            PayeeOrPayer = "Returned customer cheque",
            Description = $"Cheque {loaded.ChequeNumber}: {loaded.ReturnReason}",
            IsPosted = true,
            ApprovalStatus = CashTransactionApprovalStatus.Posted,
            JournalEntryId = posting.JournalEntryId,
            PostedDate = posting.PostingDate,
            PostedBy = UserId,
            CreatedById = UserId,
            CreatedBy = _currentUser.UserName
        };
        _context.Set<CashTransaction>().Add(returnTransaction);
        loaded.BankAccount.CurrentBalance -= totalBankCredit;
        loaded.BankAccount.AvailableBalance -= totalBankCredit;
        loaded.JournalEntryId = posting.JournalEntryId;
        loaded.ReturnCashTransactionId = returnTransaction.Id;
        loaded.Status = ReturnedChequeCaseStatus.Posted;
        loaded.PostedAt = posting.PostingDate;
        StampModified(loaded);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<PreparedDeposit> PrepareDepositAsync(
        Guid bankAccountId,
        IReadOnlyCollection<BankDepositAllocationRequestDto> allocations,
        CancellationToken cancellationToken)
    {
        if (allocations.Count == 0)
        {
            throw new InvalidOperationException("Select at least one receipt or eligible payment.");
        }
        if (allocations.Any(item => item.Amount <= 0m))
        {
            throw new InvalidOperationException("Every allocation amount must be greater than zero.");
        }
        if (allocations.GroupBy(item => item.LiquidityAccountEntryId).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("A source entry can only appear once in a deposit.");
        }

        var tenantId = TenantId;
        var bank = await _context.BankAccounts
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == bankAccountId && item.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Destination bank account was not found or is inactive.");
        var entryIds = allocations.Select(item => item.LiquidityAccountEntryId).ToArray();
        var entries = await _context.LiquidityAccountEntries
            .Include(item => item.LiquidityAccount)
            .Where(item => item.TenantId == tenantId && entryIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (entries.Count != entryIds.Length)
        {
            throw new InvalidOperationException("One or more liquidity entries were not found.");
        }

        var settings = await GetSettingsAsync(cancellationToken);
        var currency = NormalizeCurrency(bank.Currency);
        foreach (var allocation in allocations)
        {
            var entry = entries[allocation.LiquidityAccountEntryId];
            if (!entry.LiquidityAccount.IsActive || entry.IsReversed)
            {
                throw new InvalidOperationException($"Entry {entry.EntryNumber} is not eligible for settlement.");
            }
            if (!entry.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("All source entries and the destination bank must use the same currency.");
            }
            if (entry.Amount - entry.AllocatedAmount < allocation.Amount)
            {
                throw new InvalidOperationException($"Allocation exceeds the remaining balance for entry {entry.EntryNumber}.");
            }
            if (entry.LiquidityAccount.AccountType == LiquidityAccountType.ChequesAwaitingDeposit
                && (entry.AllocatedAmount != 0m || allocation.Amount != entry.Amount))
            {
                throw new InvalidOperationException(
                    $"Cheque entry {entry.EntryNumber} must remain intact and can only be deposited once.");
            }
            if (allocation.AllocationType == BankDepositAllocationType.Receipt &&
                entry.Direction != LiquidityEntryDirection.Increase)
            {
                throw new InvalidOperationException($"Entry {entry.EntryNumber} is a payment/deduction, not a receipt.");
            }
            if (allocation.AllocationType == BankDepositAllocationType.Deduction &&
                entry.Direction != LiquidityEntryDirection.Decrease)
            {
                throw new InvalidOperationException($"Entry {entry.EntryNumber} is a receipt, not an eligible deduction.");
            }
        }

        var totalReceipts = allocations
            .Where(item => item.AllocationType == BankDepositAllocationType.Receipt)
            .Sum(item => item.Amount);
        var totalDeductions = allocations
            .Where(item => item.AllocationType == BankDepositAllocationType.Deduction)
            .Sum(item => item.Amount);
        if (totalReceipts <= 0m)
        {
            throw new InvalidOperationException("A deposit must contain at least one receipt.");
        }
        if (settings.BankDepositPolicy == DepositPolicy.DepositIntact && totalDeductions > 0m)
        {
            throw new InvalidOperationException("This tenant uses a deposit-intact policy; deductions are not allowed.");
        }
        if (settings.MaximumDepositDeductionAmount.HasValue &&
            totalDeductions > settings.MaximumDepositDeductionAmount.Value)
        {
            throw new InvalidOperationException("The deposit exceeds the configured maximum deduction amount.");
        }
        if (settings.MaximumDepositDeductionPercentage.HasValue &&
            totalDeductions > totalReceipts * settings.MaximumDepositDeductionPercentage.Value / 100m)
        {
            throw new InvalidOperationException("The deposit exceeds the configured maximum deduction percentage.");
        }
        if (totalDeductions >= totalReceipts)
        {
            throw new InvalidOperationException("Deposit deductions must be less than selected receipts.");
        }

        return new PreparedDeposit(
            bank,
            settings,
            entries,
            currency,
            totalReceipts,
            totalDeductions,
            totalReceipts - totalDeductions);
    }

    private static void ReserveAllocations(
        BankDepositBatch deposit,
        IEnumerable<BankDepositAllocationRequestDto> requests,
        IReadOnlyDictionary<Guid, LiquidityAccountEntry> entries)
    {
        foreach (var request in requests)
        {
            var entry = entries[request.LiquidityAccountEntryId];
            entry.AllocatedAmount += request.Amount;
            deposit.Allocations.Add(new BankDepositAllocation
            {
                TenantId = deposit.TenantId,
                LiquidityAccountEntryId = entry.Id,
                AllocationType = request.AllocationType,
                Amount = request.Amount,
                Notes = Clean(request.Notes),
                CreatedById = deposit.CreatedById,
                CreatedBy = deposit.CreatedBy
            });
        }
    }

    private async Task ReleaseDepositReservationsAsync(
        BankDepositBatch deposit,
        CancellationToken cancellationToken)
    {
        await _context.Entry(deposit).Collection(item => item.Allocations).Query()
            .Include(item => item.LiquidityAccountEntry)
            .LoadAsync(cancellationToken);
        foreach (var allocation in deposit.Allocations)
        {
            allocation.LiquidityAccountEntry.AllocatedAmount =
                Math.Max(0m, allocation.LiquidityAccountEntry.AllocatedAmount - allocation.Amount);
        }
    }

    private async Task<IReadOnlyList<LiquidityAccountDto>> MapLiquidityAccountsAsync(
        IReadOnlyCollection<LiquidityAccount> accounts,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return Array.Empty<LiquidityAccountDto>();
        }

        var ids = accounts.Select(item => item.Id).ToArray();
        var entries = await _context.LiquidityAccountEntries
            .AsNoTracking()
            .Where(item => item.TenantId == TenantId && ids.Contains(item.LiquidityAccountId) && !item.IsReversed)
            .Select(item => new
            {
                item.LiquidityAccountId,
                item.Direction,
                item.Amount,
                item.AllocatedAmount
            })
            .ToListAsync(cancellationToken);
        return accounts.Select(account =>
        {
            var accountEntries = entries.Where(item => item.LiquidityAccountId == account.Id).ToArray();
            var balance = accountEntries.Sum(item =>
                item.Direction == LiquidityEntryDirection.Increase ? item.Amount : -item.Amount);
            var available = accountEntries.Sum(item =>
            {
                var remaining = Math.Max(item.Amount - item.AllocatedAmount, 0m);
                return item.Direction == LiquidityEntryDirection.Increase ? remaining : -remaining;
            });
            return new LiquidityAccountDto
            {
                Id = account.Id,
                Code = account.Code,
                Name = account.Name,
                AccountType = account.AccountType,
                Currency = account.Currency,
                GLAccountId = account.GLAccountId,
                GLAccountNumber = account.GLAccount?.AccountNumber ?? string.Empty,
                GLAccountName = account.GLAccount?.AccountName ?? string.Empty,
                BankAccountId = account.BankAccountId,
                BankAccountName = account.BankAccount?.AccountName,
                ProviderName = account.ProviderName,
                ProviderAccountReference = account.ProviderAccountReference,
                AllowsNegativeBalance = account.AllowsNegativeBalance,
                AllowsManualAllocations = account.AllowsManualAllocations,
                IsActive = account.IsActive,
                IsSystemAccount = account.IsSystemAccount,
                Notes = account.Notes,
                CurrentBalance = balance,
                AvailableToSettle = available,
                OpenEntryCount = accountEntries.Count(item => item.Amount > item.AllocatedAmount),
                CreatedAt = account.CreatedAt,
                RowVersion = Convert.ToBase64String(account.RowVersion)
            };
        }).ToArray();
    }

    private IQueryable<BankDepositBatch> DepositQuery()
        => _context.BankDepositBatches
            .AsNoTracking()
            .Include(item => item.BankAccount)
            .Include(item => item.Allocations)
            .ThenInclude(item => item.LiquidityAccountEntry)
            .ThenInclude(item => item.LiquidityAccount)
            .Include(item => item.Attachments)
            .ThenInclude(item => item.FileUploadRecord)
            .AsSplitQuery();

    private IQueryable<ReturnedChequeCase> ReturnedChequeQuery()
        => _context.ReturnedChequeCases
            .AsNoTracking()
            .Include(item => item.CustomerPayment)
            .Include(item => item.BankDepositBatch)
            .Include(item => item.BankAccount)
            .Include(item => item.Attachments)
            .ThenInclude(item => item.FileUploadRecord)
            .AsSplitQuery();

    private static LiquidityAccountEntryDto MapEntry(LiquidityAccountEntry item)
        => new()
        {
            Id = item.Id,
            LiquidityAccountId = item.LiquidityAccountId,
            LiquidityAccountName = item.LiquidityAccount.Name,
            EntryNumber = item.EntryNumber,
            EntryDate = item.EntryDate,
            EntryType = item.EntryType,
            Direction = item.Direction,
            Amount = item.Amount,
            AllocatedAmount = item.AllocatedAmount,
            RemainingAmount = Math.Max(item.Amount - item.AllocatedAmount, 0m),
            Currency = item.Currency,
            SourceDocumentType = item.SourceDocumentType,
            SourceDocumentId = item.SourceDocumentId,
            ReferenceNumber = item.ReferenceNumber,
            CounterpartyName = item.CounterpartyName,
            Description = item.Description,
            IsReversed = item.IsReversed,
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };

    private static BankDepositDto MapDeposit(BankDepositBatch item)
        => new()
        {
            Id = item.Id,
            DepositNumber = item.DepositNumber,
            BankAccountId = item.BankAccountId,
            BankAccountName = item.BankAccount.AccountName,
            DepositDate = item.DepositDate,
            DepositReference = item.DepositReference,
            Currency = item.Currency,
            Status = item.Status,
            PolicySnapshot = item.PolicySnapshot,
            TotalReceipts = item.TotalReceipts,
            TotalDeductions = item.TotalDeductions,
            NetAmount = item.NetAmount,
            Notes = item.Notes,
            WorkflowInstanceId = item.WorkflowInstanceId,
            SubmittedAt = item.SubmittedAt,
            SubmittedById = item.SubmittedById,
            ApprovedAt = item.ApprovedAt,
            ApprovedById = item.ApprovedById,
            PostedAt = item.PostedAt,
            JournalEntryId = item.JournalEntryId,
            CashTransactionId = item.CashTransactionId,
            RejectionReason = item.RejectionReason,
            CancellationReason = item.CancellationReason,
            Allocations = item.Allocations.Select(allocation => new BankDepositAllocationDto
            {
                Id = allocation.Id,
                LiquidityAccountEntryId = allocation.LiquidityAccountEntryId,
                SourceDocumentType = allocation.LiquidityAccountEntry.SourceDocumentType,
                SourceDocumentId = allocation.LiquidityAccountEntry.SourceDocumentId,
                EntryNumber = allocation.LiquidityAccountEntry.EntryNumber,
                EntryDate = allocation.LiquidityAccountEntry.EntryDate,
                LiquidityAccountName = allocation.LiquidityAccountEntry.LiquidityAccount.Name,
                EntryType = allocation.LiquidityAccountEntry.EntryType,
                AllocationType = allocation.AllocationType,
                Amount = allocation.Amount,
                ReferenceNumber = allocation.LiquidityAccountEntry.ReferenceNumber,
                CounterpartyName = allocation.LiquidityAccountEntry.CounterpartyName,
                Description = allocation.LiquidityAccountEntry.Description,
                Notes = allocation.Notes
            }).OrderBy(value => value.EntryDate).ToArray(),
            Attachments = item.Attachments.Select(MapAttachment).ToArray(),
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };

    private async Task<IReadOnlyList<ReturnedChequeCaseDto>> MapReturnedChequesAsync(
        IReadOnlyCollection<ReturnedChequeCase> cases,
        CancellationToken cancellationToken)
    {
        var customerIds = cases.Select(item => item.CustomerPayment.CustomerId).Distinct().ToArray();
        var customerNames = await _context.BusinessPartners
            .AsNoTracking()
            .Where(item => item.TenantId == TenantId && customerIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.PartnerName, cancellationToken);
        return cases.Select(item => new ReturnedChequeCaseDto
        {
            Id = item.Id,
            CaseNumber = item.CaseNumber,
            CustomerPaymentId = item.CustomerPaymentId,
            PaymentNumber = item.CustomerPayment.PaymentNumber,
            CustomerId = item.CustomerPayment.CustomerId,
            CustomerName = customerNames.GetValueOrDefault(item.CustomerPayment.CustomerId, "Customer"),
            BankDepositBatchId = item.BankDepositBatchId,
            DepositNumber = item.BankDepositBatch?.DepositNumber,
            BankAccountId = item.BankAccountId,
            BankAccountName = item.BankAccount.AccountName,
            ChequeNumber = item.ChequeNumber,
            DrawerBank = item.DrawerBank,
            ReturnDate = item.ReturnDate,
            BankReference = item.BankReference,
            ReturnReason = item.ReturnReason,
            ReturnedAmount = item.ReturnedAmount,
            BankChargeAmount = item.BankChargeAmount,
            ChargeTreatment = item.ChargeTreatment,
            CustomerRecoverableChargeAmount = item.CustomerRecoverableChargeAmount,
            ExpenseChargeAmount = item.ExpenseChargeAmount,
            Status = item.Status,
            WorkflowInstanceId = item.WorkflowInstanceId,
            SubmittedAt = item.SubmittedAt,
            ApprovedAt = item.ApprovedAt,
            PostedAt = item.PostedAt,
            JournalEntryId = item.JournalEntryId,
            ReturnCashTransactionId = item.ReturnCashTransactionId,
            ChargeCashTransactionId = item.ChargeCashTransactionId,
            Notes = item.Notes,
            RejectionReason = item.RejectionReason,
            Attachments = item.Attachments.Select(MapAttachment).ToArray(),
            RowVersion = Convert.ToBase64String(item.RowVersion)
        }).ToArray();
    }

    private static BankingAttachmentDto MapAttachment(BankDepositAttachment item)
        => MapAttachment(item.Id, item.FileUploadRecord, item.DocumentType, item.IsPrimaryEvidence);

    private static BankingAttachmentDto MapAttachment(ReturnedChequeAttachment item)
        => MapAttachment(item.Id, item.FileUploadRecord, item.DocumentType, item.IsPrimaryEvidence);

    private static BankingAttachmentDto MapAttachment(
        Guid id,
        FileUploadRecord file,
        string documentType,
        bool isPrimary)
        => new()
        {
            Id = id,
            FileId = file.Id,
            FileName = file.OriginalFileName,
            FileUrl = file.FilePath,
            ContentType = file.ContentType ?? "application/octet-stream",
            FileSize = file.FileSize,
            DocumentType = documentType,
            IsPrimaryEvidence = isPrimary,
            UploadedAt = file.CreatedAt,
            UploadedBy = file.CreatedBy
        };

    private async Task ValidateLiquidityMasterAsync(
        string code,
        LiquidityAccountType type,
        string currency,
        Guid glAccountId,
        Guid? bankAccountId,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var normalizedCode = RequireText(code, "Account code", 30).ToUpperInvariant();
        var normalizedCurrency = NormalizeCurrency(currency);
        if (await _context.LiquidityAccounts.AnyAsync(
                item => item.TenantId == tenantId &&
                        item.Code == normalizedCode &&
                        (!excludeId.HasValue || item.Id != excludeId.Value),
                cancellationToken))
        {
            throw new InvalidOperationException($"Liquidity account code '{normalizedCode}' already exists.");
        }
        if (!await _context.Accounts.AnyAsync(
                item => item.TenantId == tenantId && item.Id == glAccountId && item.IsActive,
                cancellationToken))
        {
            throw new InvalidOperationException("Select an active GL control account belonging to this tenant.");
        }
        if (type == LiquidityAccountType.Bank && !bankAccountId.HasValue)
        {
            throw new InvalidOperationException("A bank liquidity account must link to a bank account.");
        }
        if (type != LiquidityAccountType.Bank && bankAccountId.HasValue)
        {
            throw new InvalidOperationException("Only the Bank liquidity type can link to a bank account.");
        }
        if (bankAccountId.HasValue)
        {
            var bank = await _context.BankAccounts.FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == bankAccountId.Value,
                cancellationToken) ?? throw new InvalidOperationException("The linked bank account was not found.");
            if (!bank.Currency.Equals(normalizedCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The liquidity account and bank account currencies must match.");
            }
        }
    }

    private async Task<FinanceSettings> GetSettingsAsync(CancellationToken cancellationToken)
        => await _context.FinanceSettings.FirstOrDefaultAsync(item => item.TenantId == TenantId, cancellationToken)
           ?? throw new InvalidOperationException("Finance settings have not been configured for this tenant.");

    private async Task<BankDepositBatch> LoadDepositForActionAsync(Guid id, CancellationToken cancellationToken)
        => await _context.BankDepositBatches.FirstOrDefaultAsync(
               item => item.TenantId == TenantId && item.Id == id,
               cancellationToken)
           ?? throw new KeyNotFoundException("Bank deposit was not found.");

    private async Task<ReturnedChequeCase> LoadReturnedChequeForActionAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await _context.ReturnedChequeCases.FirstOrDefaultAsync(
               item => item.TenantId == TenantId && item.Id == id,
               cancellationToken)
           ?? throw new KeyNotFoundException("Returned-cheque case was not found.");

    private async Task<FileUploadRecord> GetValidFileAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await _context.FileUploadRecords.FirstOrDefaultAsync(
            item => item.TenantId == TenantId && item.Id == fileId,
            cancellationToken) ?? throw new KeyNotFoundException("Uploaded file was not found.");
        if (file.VirusScanStatus == FileVirusScanStatus.Infected)
        {
            throw new InvalidOperationException("An infected file cannot be attached.");
        }
        var extension = Path.GetExtension(file.OriginalFileName);
        if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Banking evidence must be a PDF, PNG, JPG, or JPEG file.");
        }
        if (file.FileSize <= 0 || file.FileSize > MaximumEvidenceFileSize)
        {
            throw new InvalidOperationException("Banking evidence must be no larger than 10 MB.");
        }
        return file;
    }

    private static void EnsureEditable(BankDepositBatch deposit)
    {
        if (deposit.Status is not BankDepositStatus.Draft and not BankDepositStatus.Returned)
        {
            throw new InvalidOperationException("Only draft or returned deposits can be edited.");
        }
    }

    private void EnsureMakerChecker(Guid? submittedById, string action)
    {
        if (submittedById == UserId)
        {
            throw new UnauthorizedAccessException(
                $"The user who submitted this banking document cannot {action} it.");
        }
    }

    private static (decimal Customer, decimal Expense) SplitCharge(CreateReturnedChequeCaseDto dto)
    {
        if (dto.BankChargeAmount < 0m)
        {
            throw new InvalidOperationException("Bank charge cannot be negative.");
        }
        var split = dto.ChargeTreatment switch
        {
            ReturnedChequeChargeTreatment.CustomerRecoverable => (dto.BankChargeAmount, 0m),
            ReturnedChequeChargeTreatment.BankChargeExpense => (0m, dto.BankChargeAmount),
            ReturnedChequeChargeTreatment.Split => (
                dto.CustomerRecoverableChargeAmount ?? 0m,
                dto.ExpenseChargeAmount ?? 0m),
            _ => throw new InvalidOperationException("Unsupported returned-cheque charge treatment.")
        };
        if (split.Item1 < 0m || split.Item2 < 0m ||
            Math.Abs(split.Item1 + split.Item2 - dto.BankChargeAmount) > 0.01m)
        {
            throw new InvalidOperationException("Customer and expense charge allocations must equal the bank charge.");
        }
        return split;
    }

    private void SetRowVersion(BaseEntity entity, string rowVersion)
    {
        if (entity is not LiquidityAccount and not BankDepositBatch)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw new InvalidOperationException("Row version is required.");
        }
        try
        {
            var bytes = Convert.FromBase64String(rowVersion);
            switch (entity)
            {
                case LiquidityAccount account:
                    _context.Entry(account).Property(item => item.RowVersion).OriginalValue = bytes;
                    break;
                case BankDepositBatch deposit:
                    _context.Entry(deposit).Property(item => item.RowVersion).OriginalValue = bytes;
                    break;
            }
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Row version is invalid.");
        }
    }

    private void StampModified(BaseEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName;
        entity.LastModifiedById = UserId;
    }

    private static string NormalizeCurrency(string? value)
    {
        var currency = RequireText(value, "Currency", 3).ToUpperInvariant();
        if (currency.Length != 3)
        {
            throw new InvalidOperationException("Currency must be a three-letter ISO code.");
        }
        return currency;
    }

    private static string RequireText(string? value, string label, int maximumLength)
    {
        var cleaned = Clean(value);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new InvalidOperationException($"{label} is required.");
        }
        if (cleaned.Length > maximumLength)
        {
            throw new InvalidOperationException($"{label} cannot exceed {maximumLength} characters.");
        }
        return cleaned;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record PreparedDeposit(
        BankAccount Bank,
        FinanceSettings Settings,
        IReadOnlyDictionary<Guid, LiquidityAccountEntry> Entries,
        string Currency,
        decimal TotalReceipts,
        decimal TotalDeductions,
        decimal NetAmount);
}
