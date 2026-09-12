using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Api.Services.Finance.UnitAccounting
{
    /// <summary>
    /// Service implementation for managing Unit Journal Entries.
    /// Handles creation, workflow (approval/rejection), posting, and reversal.
    /// </summary>
    public class UnitJournalEntryService : IUnitJournalEntryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UnitJournalEntryService> _logger;
        private readonly IDocumentNumberingService _documentNumberingService;
        private readonly IWorkflowIntegrationService _workflowService;

        public UnitJournalEntryService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<UnitJournalEntryService> logger,
            IDocumentNumberingService documentNumberingService,
            IWorkflowIntegrationService workflowService)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
            _documentNumberingService = documentNumberingService;
            _workflowService = workflowService;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";
        private Guid UserId => Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;

        public async Task<IReadOnlyList<UnitJournalEntryDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entries = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.EntryNumber)
                .ToListAsync(cancellationToken);

            return entries.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<UnitJournalEntryDto>> GetFilteredAsync(
            UnitJournalEntryFilters filters,
            CancellationToken cancellationToken = default)
        {
            // Use IQueryable to avoid type mismatch with Include/Where chain
            IQueryable<UnitJournalEntry> query = _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines);

            if (!string.IsNullOrEmpty(filters.Status))
            {
                if (Enum.TryParse<UnitJournalEntryStatus>(filters.Status, out var status))
                {
                    query = query.Where(e => e.Status == status);
                }
            }

            if (filters.StartDate.HasValue)
                query = query.Where(e => e.EntryDate >= filters.StartDate.Value);

            if (filters.EndDate.HasValue)
                query = query.Where(e => e.EntryDate <= filters.EndDate.Value);

            if (filters.FiscalPeriodId.HasValue)
                query = query.Where(e => e.FiscalPeriodId == filters.FiscalPeriodId.Value);

            var entries = await query
                .OrderByDescending(e => e.EntryDate)
                .ToListAsync(cancellationToken);

            return entries.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<UnitJournalEntryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
        {
            var entries = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.TenantId == TenantId && e.ApprovalRequired && e.Status == UnitJournalEntryStatus.PendingApproval && !e.IsDeleted)
                .Include(e => e.Lines)
                .OrderBy(e => e.EntryDate)
                .ToListAsync(cancellationToken);

            return entries.Select(MapToDto).ToList();
        }

        public async Task<UnitJournalEntryDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                    .ThenInclude(l => l.UnitAccount)
                .Include(e => e.FiscalPeriod)
                .FirstOrDefaultAsync(cancellationToken);

            return entry == null ? null : MapToDetailDto(entry);
        }

        public async Task<UnitJournalEntryDto?> GetByEntryNumberAsync(string entryNumber, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.EntryNumber == entryNumber && e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(cancellationToken);

            return entry == null ? null : MapToDto(entry);
        }

        public async Task<UnitJournalEntryDto> CreateAsync(CreateUnitJournalEntryDto dto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var entryNumber = await GenerateEntryNumberAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var period = await ResolvePostingPeriodAsync(dto.EntryDate, dto.FiscalPeriodId, requireOpenPeriod: true, cancellationToken);
            await ValidateCreateLinesAsync(dto.Lines, cancellationToken);

            var entry = new UnitJournalEntry
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                EntryNumber = entryNumber,
                EntryDate = dto.EntryDate.Date,
                Description = NormalizeOptional(dto.Description),
                SourceDocument = NormalizeOptional(dto.SourceDocument),
                FiscalPeriodId = period.Id,
                FiscalYearId = period.FiscalYearId,
                Status = UnitJournalEntryStatus.Draft,
                CreatedAt = now,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<UnitJournalEntry>().AddAsync(entry);

            // Add lines
            int lineNumber = 1;
            foreach (var lineDto in dto.Lines)
            {
                var line = new UnitJournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    UnitJournalEntryId = entry.Id,
                    LineNumber = lineNumber++,
                    UnitAccountId = lineDto.UnitAccountId,
                    Quantity = lineDto.Quantity,
                    Description = NormalizeOptional(lineDto.Description),
                    CreatedAt = now,
                    CreatedBy = UserName
                };
                await _unitOfWork.Repository<UnitJournalEntryLine>().AddAsync(line);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit journal entry {EntryNumber} created by {User}", entryNumber, UserName);

            return MapToDto(entry);
        }

        public async Task<UnitJournalEntryDto> UpdateAsync(Guid id, UpdateUnitJournalEntryDto dto, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry == null)
                throw new ArgumentException($"Unit journal entry with ID '{id}' not found.");

            if (entry.Status is not (UnitJournalEntryStatus.Draft or UnitJournalEntryStatus.Rejected))
                throw new InvalidOperationException("Only draft or rejected entries can be modified.");

            entry.Description = dto.Description ?? entry.Description;
            entry.SourceDocument = dto.SourceDocument ?? entry.SourceDocument;
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit journal entry {EntryNumber} updated by {User}", entry.EntryNumber, UserName);

            return MapToDto(entry);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted);

            if (entry == null)
                return;

            if (entry.Status is not (UnitJournalEntryStatus.Draft or UnitJournalEntryStatus.Rejected))
                throw new InvalidOperationException("Only draft or rejected entries can be deleted.");

            entry.IsDeleted = true;
            entry.DeletedAt = DateTime.UtcNow;
            entry.DeletedBy = UserName;

            await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit journal entry {EntryNumber} deleted by {User}", entry.EntryNumber, UserName);
        }

        public async Task<UnitJournalEntryDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                UnitJournalEntry? entry = null;
                UnitJournalEntryStatus previousStatus = UnitJournalEntryStatus.Draft;
                string? previousRejectionReason = null;
                bool previousApprovalRequired = true;
                Guid? previousWorkflowInstanceId = null;
                try
                {
                    entry = await _unitOfWork.Repository<UnitJournalEntry>()
                        .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                        .Include(e => e.Lines).FirstOrDefaultAsync(cancellationToken)
                        ?? throw new ArgumentException($"Unit journal entry with ID '{id}' not found.");
                    previousStatus = entry.Status;
                    previousRejectionReason = entry.RejectionReason;
                    previousApprovalRequired = entry.ApprovalRequired;
                    previousWorkflowInstanceId = entry.WorkflowInstanceId;
                    if (entry.Status is not (UnitJournalEntryStatus.Draft or UnitJournalEntryStatus.Rejected))
                        throw new InvalidOperationException("Only draft or rejected entries can be submitted.");
                    await ValidateEntryReadyForPostingAsync(entry, requireOpenPeriod: true, cancellationToken);

                    // Only active approvals need the pending staging state. A direct submission
                    // must transition from its original editable state, never from a legacy
                    // pending record whose approval history would otherwise be abandoned.
                    var approvalRequired = await _workflowService.HasActiveApprovalInstanceAsync("UnitJournalEntry", id) ||
                        await _workflowService.HasActiveApprovalWorkflowAsync("UnitJournalEntry");
                    entry.RejectionReason = null;
                    entry.UpdatedAt = DateTime.UtcNow;
                    entry.UpdatedBy = UserName;
                    if (approvalRequired)
                    {
                        entry.Status = UnitJournalEntryStatus.PendingApproval;
                        await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    var workflow = await _workflowService.SubmitAsync("UnitJournalEntry", id);
                    if (!workflow.ExecutionResult.Success)
                        throw new InvalidOperationException(workflow.ExecutionResult.Message ?? "Unable to submit the unit journal entry.");
                    if (workflow.ApprovalRequired != approvalRequired)
                        throw new InvalidOperationException("The unit journal approval configuration changed. Reload and retry.");
                    if (workflow.ApprovalRequired && (!workflow.ExecutionResult.WorkflowInstanceId.HasValue ||
                        workflow.ExecutionResult.WorkflowInstanceId == Guid.Empty))
                        throw new InvalidOperationException("The active approval process did not retain a workflow instance.");
                    if (!workflow.ApprovalRequired && (workflow.ExecutionResult.WorkflowInstanceId.HasValue ||
                        workflow.Outcome != WorkflowOutcome.Approved || workflow.ExecutionResult.Status != WorkflowInstanceStatus.Completed ||
                        entry.ApprovedBy.HasValue || entry.ApprovedAt.HasValue ||
                        !string.IsNullOrEmpty(entry.ApprovedByName)))
                        throw new InvalidOperationException("The unit journal approval state changed. Reload and retry.");

                    entry.ApprovalRequired = workflow.ApprovalRequired;
                    entry.WorkflowInstanceId = workflow.ExecutionResult.WorkflowInstanceId;
                    entry.Status = workflow.ApprovalRequired
                        ? workflow.Outcome == WorkflowOutcome.Approved ? UnitJournalEntryStatus.Approved : UnitJournalEntryStatus.PendingApproval
                        : UnitJournalEntryStatus.ReadyToPost;
                    await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
                    await _unitOfWork.CommitAsync(cancellationToken);
                    _logger.LogInformation("Unit journal entry {EntryNumber} submitted by {User}; approval required: {ApprovalRequired}",
                        entry.EntryNumber, UserName, entry.ApprovalRequired);
                    return MapToDto(entry);
                }
                catch
                {
                    await TryRollbackAsync(cancellationToken);
                    if (entry is not null)
                    {
                        entry.Status = previousStatus;
                        entry.RejectionReason = previousRejectionReason;
                        entry.ApprovalRequired = previousApprovalRequired;
                        entry.WorkflowInstanceId = previousWorkflowInstanceId;
                    }
                    _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<UnitJournalEntryDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry == null)
                throw new ArgumentException($"Unit journal entry with ID '{id}' not found.");

            if (!entry.ApprovalRequired || entry.Status != UnitJournalEntryStatus.PendingApproval)
                throw new InvalidOperationException("Only pending entries can be approved.");

            if (UserId == Guid.Empty)
                throw new InvalidOperationException("Unable to resolve the current approver.");

            if (!await _workflowService.CanUserApproveAsync("UnitJournalEntry", id, UserId))
                throw new InvalidOperationException("This unit journal entry is assigned to another workflow approver.");

            var workflowResult = (await _workflowService.ProcessApprovalAsync("UnitJournalEntry", id, UserId, "Approve")).ExecutionResult;
            if (!workflowResult.Success)
                throw new InvalidOperationException(workflowResult.Message ?? "Unable to process unit journal entry approval.");

            if (workflowResult.Status != WorkflowInstanceStatus.Completed)
            {
                _logger.LogInformation(
                    "Recorded intermediate approval for unit journal entry {EntryNumber}; workflow status is {WorkflowStatus}",
                    entry.EntryNumber,
                    workflowResult.Status);
                return MapToDto(entry);
            }

            entry.Status = UnitJournalEntryStatus.Approved;
            entry.ApprovedAt = DateTime.UtcNow;
            entry.ApprovedBy = UserId;
            entry.ApprovedByName = UserName;
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit journal entry {EntryNumber} approved by {User}", entry.EntryNumber, UserName);

            return MapToDto(entry);
        }

        public async Task<UnitJournalEntryDto> RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry == null)
                throw new ArgumentException($"Unit journal entry with ID '{id}' not found.");

            if (!entry.ApprovalRequired || entry.Status != UnitJournalEntryStatus.PendingApproval)
                throw new InvalidOperationException("Only pending entries can be rejected.");

            if (UserId == Guid.Empty)
                throw new InvalidOperationException("Unable to resolve the current approver.");

            if (!await _workflowService.CanUserApproveAsync("UnitJournalEntry", id, UserId))
                throw new InvalidOperationException("This unit journal entry is assigned to another workflow approver.");

            var workflowResult = (await _workflowService.ProcessApprovalAsync("UnitJournalEntry", id, UserId, "Reject", reason)).ExecutionResult;
            if (!workflowResult.Success)
                throw new InvalidOperationException(workflowResult.Message ?? "Unable to process unit journal entry rejection.");

            if (workflowResult.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            {
                _logger.LogInformation(
                    "Recorded unit journal entry rejection workflow action for {EntryNumber}; workflow status is {WorkflowStatus}",
                    entry.EntryNumber,
                    workflowResult.Status);
                return MapToDto(entry);
            }

            entry.Status = UnitJournalEntryStatus.Rejected;
            entry.RejectionReason = reason;
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit journal entry {EntryNumber} rejected by {User}. Reason: {Reason}", 
                entry.EntryNumber, UserName, reason);

            return MapToDto(entry);
        }

        public async Task<UnitJournalEntryDto> PostAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                        .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                        .Include(e => e.Lines)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (entry == null)
                        throw new ArgumentException($"Unit journal entry with ID '{id}' not found.");

                    var ready = entry.ApprovalRequired
                        ? entry.Status == UnitJournalEntryStatus.Approved
                        : entry.Status == UnitJournalEntryStatus.ReadyToPost && !entry.WorkflowInstanceId.HasValue &&
                            !entry.ApprovedBy.HasValue && !entry.ApprovedAt.HasValue && string.IsNullOrEmpty(entry.ApprovedByName);
                    if (!ready)
                        throw new InvalidOperationException("Only approved entries or submitted entries with no required approval can be posted.");

                    var period = await ValidateEntryReadyForPostingAsync(entry, requireOpenPeriod: true, cancellationToken);
                    var now = DateTime.UtcNow;
                    await ApplyUnitBalanceMovementsAsync(period, entry.Lines.Where(l => !l.IsDeleted), now, cancellationToken);

                    entry.Status = UnitJournalEntryStatus.Posted;
                    entry.PostedAt = now;
                    entry.PostedBy = UserId;
                    entry.PostedByName = UserName;
                    entry.UpdatedAt = now;
                    entry.UpdatedBy = UserName;

                    await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
                    await _unitOfWork.CommitAsync(cancellationToken);

                    _logger.LogInformation("Unit journal entry {EntryNumber} posted by {User}", entry.EntryNumber, UserName);

                    return MapToDto(entry);
                }
                catch
                {
                    await TryRollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<UnitJournalEntryDto> ReverseAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reversal reason is required.", nameof(reason));

            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var original = await _unitOfWork.Repository<UnitJournalEntry>()
                        .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                        .Include(e => e.Lines)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (original == null)
                        throw new ArgumentException($"Unit journal entry with ID '{id}' not found.");

                    if (original.Status != UnitJournalEntryStatus.Posted)
                        throw new InvalidOperationException("Only posted entries can be reversed.");

                    if (original.ReversalEntryId.HasValue)
                        throw new InvalidOperationException("This unit journal entry has already been reversed.");

                    var now = DateTime.UtcNow;
                    var reversalNumber = await GenerateEntryNumberAsync(cancellationToken);
                    var reversalPeriod = await ResolvePostingPeriodAsync(now.Date, null, requireOpenPeriod: true, cancellationToken);

                    // Reversals are posted into the currently open period instead of mutating a closed historical period.
                    var reversal = new UnitJournalEntry
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        EntryNumber = reversalNumber,
                        EntryDate = now.Date,
                        Description = $"Reversal of {original.EntryNumber}: {reason.Trim()}",
                        SourceDocument = original.EntryNumber,
                        FiscalYearId = reversalPeriod.FiscalYearId,
                        FiscalPeriodId = reversalPeriod.Id,
                        Status = UnitJournalEntryStatus.Posted,
                        IsReversal = true,
                        ReversedEntryId = original.Id,
                        ReversalReason = reason.Trim(),
                        PostedAt = now,
                        PostedBy = UserId,
                        PostedByName = UserName,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };

                    await _unitOfWork.Repository<UnitJournalEntry>().AddAsync(reversal);

                    var reversalLines = new List<UnitJournalEntryLine>();
                    int lineNumber = 1;
                    foreach (var origLine in original.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber))
                    {
                        var reversalLine = new UnitJournalEntryLine
                        {
                            Id = Guid.NewGuid(),
                            TenantId = TenantId,
                            UnitJournalEntryId = reversal.Id,
                            LineNumber = lineNumber++,
                            UnitAccountId = origLine.UnitAccountId,
                            Quantity = -origLine.Quantity,
                            Description = $"Reversal: {NormalizeOptional(origLine.Description) ?? original.EntryNumber}",
                            CreatedAt = now,
                            CreatedBy = UserName
                        };
                        reversalLines.Add(reversalLine);
                        await _unitOfWork.Repository<UnitJournalEntryLine>().AddAsync(reversalLine);
                    }

                    await ValidateJournalLinesAsync(reversalLines, cancellationToken);
                    await ApplyUnitBalanceMovementsAsync(reversalPeriod, reversalLines, now, cancellationToken);

                    original.Status = UnitJournalEntryStatus.Reversed;
                    original.ReversalEntryId = reversal.Id;
                    original.UpdatedAt = now;
                    original.UpdatedBy = UserName;
                    await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(original);

                    await _unitOfWork.CommitAsync(cancellationToken);

                    _logger.LogInformation("Unit journal entry {EntryNumber} reversed by {User}. Reversal: {ReversalNumber}",
                        original.EntryNumber, UserName, reversalNumber);

                    return MapToDto(reversal);
                }
                catch
                {
                    await TryRollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<bool> ValidateEntryAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry == null) return false;
            try
            {
                await ValidateEntryReadyForPostingAsync(entry, requireOpenPeriod: false, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GenerateEntryNumberAsync(CancellationToken cancellationToken = default)
        {
            return await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.UnitJournalEntry,
                TenantId,
                DateTime.UtcNow,
                nameof(UnitJournalEntry),
                cancellationToken: cancellationToken);
        }

        private async Task<FiscalPeriod> ResolvePostingPeriodAsync(
            DateTime entryDate,
            Guid? fiscalPeriodId,
            bool requireOpenPeriod,
            CancellationToken cancellationToken)
        {
            var normalizedDate = entryDate.Date;
            IQueryable<FiscalPeriod> query = _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(p => p.TenantId == TenantId && !p.IsDeleted);

            query = fiscalPeriodId.HasValue
                ? query.Where(p => p.Id == fiscalPeriodId.Value)
                : query.Where(p => p.StartDate <= normalizedDate && p.EndDate >= normalizedDate);

            var period = await query
                .OrderBy(p => p.StartDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (period == null)
            {
                throw fiscalPeriodId.HasValue
                    ? new ArgumentException($"Fiscal period with ID '{fiscalPeriodId}' not found.")
                    : new InvalidOperationException("No fiscal period was found for the unit journal entry date.");
            }

            if (normalizedDate < period.StartDate.Date || normalizedDate > period.EndDate.Date)
                throw new InvalidOperationException("Unit journal entry date does not fall inside the selected fiscal period.");

            if (requireOpenPeriod && (!period.IsOpen || period.IsClosed || period.IsLocked))
                throw new InvalidOperationException("Unit journal posting period is not open.");

            return period;
        }

        private async Task ValidateCreateLinesAsync(
            IReadOnlyCollection<CreateUnitJournalEntryLineDto> lines,
            CancellationToken cancellationToken)
        {
            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("At least one unit journal line is required.");

            if (lines.Any(l => l.UnitAccountId == Guid.Empty))
                throw new InvalidOperationException("Each unit journal line must have a unit account.");

            if (lines.Any(l => l.Quantity == 0m))
                throw new InvalidOperationException("Unit journal line quantities cannot be zero.");

            await ValidateLineAccountsAsync(lines.Select(l => l.UnitAccountId).Distinct().ToList(), cancellationToken);
        }

        private async Task<FiscalPeriod> ValidateEntryReadyForPostingAsync(
            UnitJournalEntry entry,
            bool requireOpenPeriod,
            CancellationToken cancellationToken)
        {
            var lines = entry.Lines.Where(l => !l.IsDeleted).ToList();
            if (lines.Count == 0)
                throw new InvalidOperationException("Cannot process a unit journal entry with no lines.");

            await ValidateJournalLinesAsync(lines, cancellationToken);
            return await ResolvePostingPeriodAsync(entry.EntryDate, entry.FiscalPeriodId, requireOpenPeriod, cancellationToken);
        }

        private async Task ValidateJournalLinesAsync(
            IReadOnlyCollection<UnitJournalEntryLine> lines,
            CancellationToken cancellationToken)
        {
            if (lines.Any(l => l.UnitAccountId == Guid.Empty))
                throw new InvalidOperationException("Each unit journal line must have a unit account.");

            if (lines.Any(l => l.Quantity == 0m))
                throw new InvalidOperationException("Unit journal line quantities cannot be zero.");

            await ValidateLineAccountsAsync(lines.Select(l => l.UnitAccountId).Distinct().ToList(), cancellationToken);
        }

        private async Task ValidateLineAccountsAsync(
            IReadOnlyCollection<Guid> unitAccountIds,
            CancellationToken cancellationToken)
        {
            var accounts = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(a => a.TenantId == TenantId && unitAccountIds.Contains(a.Id) && !a.IsDeleted)
                .Select(a => new
                {
                    a.Id,
                    a.AccountNumber,
                    a.IsActive,
                    a.IsPostingAccount,
                    HasChildren = a.ChildAccounts.Any(c => !c.IsDeleted)
                })
                .ToListAsync(cancellationToken);

            if (accounts.Count != unitAccountIds.Count)
                throw new ArgumentException("One or more unit journal line accounts were not found for the current tenant.");

            var inactive = accounts.FirstOrDefault(a => !a.IsActive);
            if (inactive != null)
                throw new InvalidOperationException($"Unit account '{inactive.AccountNumber}' is inactive and cannot receive postings.");

            var nonPosting = accounts.FirstOrDefault(a => !a.IsPostingAccount || a.HasChildren);
            if (nonPosting != null)
                throw new InvalidOperationException($"Unit account '{nonPosting.AccountNumber}' is a summary account and cannot receive direct postings.");
        }

        private async Task ApplyUnitBalanceMovementsAsync(
            FiscalPeriod period,
            IEnumerable<UnitJournalEntryLine> sourceLines,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var deltas = sourceLines
                .Where(l => !l.IsDeleted)
                .GroupBy(l => l.UnitAccountId)
                .Select(g => new { UnitAccountId = g.Key, Quantity = g.Sum(l => l.Quantity) })
                .Where(g => g.Quantity != 0m)
                .ToList();

            foreach (var delta in deltas)
            {
                await ApplyBalanceDeltaToAccountAndParentsAsync(
                    delta.UnitAccountId,
                    period,
                    delta.Quantity,
                    now,
                    cancellationToken);
            }
        }

        private async Task ApplyBalanceDeltaToAccountAndParentsAsync(
            Guid unitAccountId,
            FiscalPeriod period,
            decimal quantityDelta,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var visitedAccountIds = new HashSet<Guid>();
            Guid? currentAccountId = unitAccountId;

            while (currentAccountId.HasValue)
            {
                if (!visitedAccountIds.Add(currentAccountId.Value))
                    throw new InvalidOperationException("Unit account hierarchy contains a cycle.");

                var account = await _unitOfWork.Repository<UnitAccount>()
                    .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == currentAccountId.Value && !a.IsDeleted);

                if (account == null)
                    throw new ArgumentException("One or more unit journal line accounts were not found for the current tenant.");

                account.CurrentBalance += quantityDelta;
                account.UpdatedAt = now;
                account.UpdatedBy = UserName;
                await _unitOfWork.Repository<UnitAccount>().UpdateAsync(account);

                await ApplyPeriodBalanceDeltaAsync(account.Id, period, quantityDelta, now, cancellationToken);
                currentAccountId = account.ParentAccountId;
            }
        }

        private async Task ApplyPeriodBalanceDeltaAsync(
            Guid unitAccountId,
            FiscalPeriod period,
            decimal quantityDelta,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var balance = await _unitOfWork.Repository<UnitAccountBalance>()
                .FirstOrDefaultAsync(b => b.TenantId == TenantId
                    && b.UnitAccountId == unitAccountId
                    && b.FiscalPeriodId == period.Id
                    && !b.IsDeleted);

            if (balance == null)
            {
                var openingBalance = await GetPriorClosingBalanceAsync(unitAccountId, period, cancellationToken);
                balance = new UnitAccountBalance
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    UnitAccountId = unitAccountId,
                    FiscalYearId = period.FiscalYearId,
                    FiscalPeriodId = period.Id,
                    OpeningBalance = openingBalance,
                    PeriodActivity = 0m,
                    ClosingBalance = openingBalance,
                    CreatedAt = now,
                    CreatedBy = UserName
                };
                await _unitOfWork.Repository<UnitAccountBalance>().AddAsync(balance);
            }

            balance.PeriodActivity += quantityDelta;
            balance.ClosingBalance = balance.OpeningBalance + balance.PeriodActivity;
            balance.UpdatedAt = now;
            balance.UpdatedBy = UserName;
            await _unitOfWork.Repository<UnitAccountBalance>().UpdateAsync(balance);

            // If an adjustment is posted into an earlier open period, later period snapshots must carry the same delta forward.
            var futureBalances = await _unitOfWork.Repository<UnitAccountBalance>()
                .GetQueryable(b => b.TenantId == TenantId
                    && b.UnitAccountId == unitAccountId
                    && b.FiscalPeriodId != period.Id
                    && !b.IsDeleted)
                .Include(b => b.FiscalPeriod)
                .Where(b => b.FiscalPeriod!.StartDate > period.StartDate)
                .OrderBy(b => b.FiscalPeriod!.StartDate)
                .ToListAsync(cancellationToken);

            foreach (var futureBalance in futureBalances)
            {
                futureBalance.OpeningBalance += quantityDelta;
                futureBalance.ClosingBalance += quantityDelta;
                futureBalance.UpdatedAt = now;
                futureBalance.UpdatedBy = UserName;
                await _unitOfWork.Repository<UnitAccountBalance>().UpdateAsync(futureBalance);
            }
        }

        private async Task<decimal> GetPriorClosingBalanceAsync(
            Guid unitAccountId,
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var priorBalance = await _unitOfWork.Repository<UnitAccountBalance>()
                .GetQueryable(b => b.TenantId == TenantId
                    && b.UnitAccountId == unitAccountId
                    && !b.IsDeleted)
                .Include(b => b.FiscalPeriod)
                .Where(b => b.FiscalPeriod!.EndDate < period.StartDate)
                .OrderByDescending(b => b.FiscalPeriod!.EndDate)
                .Select(b => (decimal?)b.ClosingBalance)
                .FirstOrDefaultAsync(cancellationToken);

            return priorBalance ?? 0m;
        }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private async Task TryRollbackAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // CommitAsync already rolls back and clears the active transaction when SaveChanges fails.
            }
        }

        private UnitJournalEntryDto MapToDto(UnitJournalEntry entry)
        {
            return new UnitJournalEntryDto
            {
                Id = entry.Id,
                EntryNumber = entry.EntryNumber,
                EntryDate = entry.EntryDate,
                Description = entry.Description,
                SourceDocument = entry.SourceDocument,
                FiscalPeriodId = entry.FiscalPeriodId,
                Status = entry.Status.ToString(),
                ApprovalRequired = entry.ApprovalRequired,
                WorkflowInstanceId = entry.WorkflowInstanceId,
                LineCount = entry.Lines?.Count(l => !l.IsDeleted) ?? 0,
                CreatedAt = entry.CreatedAt,
                CreatedBy = entry.CreatedBy
            };
        }

        private UnitJournalEntryDetailDto MapToDetailDto(UnitJournalEntry entry)
        {
            return new UnitJournalEntryDetailDto
            {
                Id = entry.Id,
                EntryNumber = entry.EntryNumber,
                EntryDate = entry.EntryDate,
                Description = entry.Description,
                SourceDocument = entry.SourceDocument,
                FiscalPeriodId = entry.FiscalPeriodId,
                FiscalPeriodName = entry.FiscalPeriod?.PeriodName,
                Status = entry.Status.ToString(),
                ApprovalRequired = entry.ApprovalRequired,
                WorkflowInstanceId = entry.WorkflowInstanceId,
                ApprovedAt = entry.ApprovedAt,
                PostedAt = entry.PostedAt,
                RejectionReason = entry.RejectionReason,
                Lines = entry.Lines.Where(l => !l.IsDeleted).Select(l => new UnitJournalEntryLineDto
                {
                    Id = l.Id,
                    LineNumber = l.LineNumber,
                    UnitAccountId = l.UnitAccountId,
                    UnitAccountNumber = l.UnitAccount?.AccountNumber ?? "",
                    UnitAccountName = l.UnitAccount?.Name ?? "",
                    Quantity = l.Quantity,
                    Description = l.Description
                }).OrderBy(l => l.LineNumber).ToList(),
                CreatedAt = entry.CreatedAt,
                CreatedBy = entry.CreatedBy,
                UpdatedAt = entry.UpdatedAt,
                UpdatedBy = entry.UpdatedBy
            };
        }
    }
}
