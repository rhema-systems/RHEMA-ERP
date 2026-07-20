using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
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

            if (!string.Equals(line.TransactionCurrencyCode, line.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                // This GL-only import records functional debit/credit amounts. It cannot safely
                // reconstruct an original foreign amount for the posting-engine FX snapshot.
                errors.Add($"Line {line.LineNumber}: foreign-currency opening balances are not supported by the controlled GL opening-balance flow.");
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
            batch.Status = StatusPosted;
            batch.JournalEntryId = result.JournalEntryId;
            batch.PostingEventId = result.PostingEventId;
            batch.PostedAt = DateTime.UtcNow;
            batch.FailureReason = null;
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = _currentUser.UserName ?? "system";
            batch.LastModifiedById = CurrentUserId();
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
                    TransactionDebitAmount = l.DebitAmount,
                    TransactionCreditAmount = l.CreditAmount,
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
