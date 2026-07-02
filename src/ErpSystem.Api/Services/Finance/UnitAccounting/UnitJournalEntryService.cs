using System;
using System.Collections.Generic;
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
        private readonly IWorkflowService _workflowService;

        public UnitJournalEntryService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<UnitJournalEntryService> logger,
            IDocumentNumberingService documentNumberingService,
            IWorkflowService workflowService)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
            _documentNumberingService = documentNumberingService;
            _workflowService = workflowService;
        }

        private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;
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
                .GetQueryable(e => e.TenantId == TenantId && e.Status == UnitJournalEntryStatus.PendingApproval && !e.IsDeleted)
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
            var entryNumber = await GenerateEntryNumberAsync(cancellationToken);
            var now = DateTime.UtcNow;

            // Get current fiscal period if not specified
            Guid fiscalPeriodId;
            Guid fiscalYearId;
            if (dto.FiscalPeriodId.HasValue)
            {
                var period = await _unitOfWork.Repository<FiscalPeriod>()
                    .FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId.Value && !p.IsDeleted);
                if (period == null)
                    throw new ArgumentException($"Fiscal period with ID '{dto.FiscalPeriodId}' not found.");
                fiscalPeriodId = period.Id;
                fiscalYearId = period.FiscalYearId;
            }
            else
            {
                // Find current open period
                var currentPeriod = await _unitOfWork.Repository<FiscalPeriod>()
                    .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.IsOpen && !p.IsDeleted);
                if (currentPeriod == null)
                    throw new InvalidOperationException("No open fiscal period found.");
                fiscalPeriodId = currentPeriod.Id;
                fiscalYearId = currentPeriod.FiscalYearId;
            }

            var entry = new UnitJournalEntry
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                EntryNumber = entryNumber,
                EntryDate = dto.EntryDate,
                Description = dto.Description,
                SourceDocument = dto.SourceDocument,
                FiscalPeriodId = fiscalPeriodId,
                FiscalYearId = fiscalYearId,
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
                    Description = lineDto.Description,
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

            if (entry.Status != UnitJournalEntryStatus.Draft)
                throw new InvalidOperationException("Only draft entries can be modified.");

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

            if (entry.Status != UnitJournalEntryStatus.Draft)
                throw new InvalidOperationException("Only draft entries can be deleted.");

            entry.IsDeleted = true;
            entry.DeletedAt = DateTime.UtcNow;
            entry.DeletedBy = UserName;

            await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit journal entry {EntryNumber} deleted by {User}", entry.EntryNumber, UserName);
        }

        public async Task<UnitJournalEntryDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry == null)
                throw new ArgumentException($"Unit journal entry with ID '{id}' not found.");

            if (entry.Status != UnitJournalEntryStatus.Draft)
                throw new InvalidOperationException("Only draft entries can be submitted for approval.");

            if (!entry.Lines.Any(l => !l.IsDeleted))
                throw new InvalidOperationException("Cannot submit an entry with no lines.");

            entry.Status = UnitJournalEntryStatus.PendingApproval;
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var workflowResult = await _workflowService.StartApprovalWorkflowAsync("UnitJournalEntry", id);
            if (!workflowResult.Success)
            {
                entry.Status = UnitJournalEntryStatus.Draft;
                entry.UpdatedAt = DateTime.UtcNow;
                entry.UpdatedBy = UserName;
                await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                throw new InvalidOperationException(workflowResult.Message ?? "Unable to start unit journal entry approval workflow.");
            }

            _logger.LogInformation("Unit journal entry {EntryNumber} submitted for approval by {User}", entry.EntryNumber, UserName);

            return MapToDto(entry);
        }

        public async Task<UnitJournalEntryDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry == null)
                throw new ArgumentException($"Unit journal entry with ID '{id}' not found.");

            if (entry.Status != UnitJournalEntryStatus.PendingApproval)
                throw new InvalidOperationException("Only pending entries can be approved.");

            if (UserId == Guid.Empty)
                throw new InvalidOperationException("Unable to resolve the current approver.");

            if (!await _workflowService.CanUserApproveAsync("UnitJournalEntry", id, UserId))
                throw new InvalidOperationException("This unit journal entry is assigned to another workflow approver.");

            var workflowResult = await _workflowService.ProcessApprovalStepAsync("UnitJournalEntry", id, UserId, "Approve");
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

            if (entry.Status != UnitJournalEntryStatus.PendingApproval)
                throw new InvalidOperationException("Only pending entries can be rejected.");

            if (UserId == Guid.Empty)
                throw new InvalidOperationException("Unable to resolve the current approver.");

            if (!await _workflowService.CanUserApproveAsync("UnitJournalEntry", id, UserId))
                throw new InvalidOperationException("This unit journal entry is assigned to another workflow approver.");

            var workflowResult = await _workflowService.ProcessApprovalStepAsync("UnitJournalEntry", id, UserId, "Reject", reason);
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
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry == null)
                throw new ArgumentException($"Unit journal entry with ID '{id}' not found.");

            if (entry.Status != UnitJournalEntryStatus.Approved)
                throw new InvalidOperationException("Only approved entries can be posted.");

            // Update account balances
            foreach (var line in entry.Lines.Where(l => !l.IsDeleted))
            {
                var account = await _unitOfWork.Repository<UnitAccount>()
                    .FirstOrDefaultAsync(a => a.Id == line.UnitAccountId && !a.IsDeleted);

                if (account != null)
                {
                    account.CurrentBalance += line.Quantity;
                    account.UpdatedAt = DateTime.UtcNow;
                    account.UpdatedBy = "system";
                    await _unitOfWork.Repository<UnitAccount>().UpdateAsync(account);
                }
            }

            entry.Status = UnitJournalEntryStatus.Posted;
            entry.PostedAt = DateTime.UtcNow;
            entry.PostedBy = UserId;
            entry.PostedByName = UserName;
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(entry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit journal entry {EntryNumber} posted by {User}", entry.EntryNumber, UserName);

            return MapToDto(entry);
        }

        public async Task<UnitJournalEntryDto> ReverseAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            var original = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted)
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(cancellationToken);

            if (original == null)
                throw new ArgumentException($"Unit journal entry with ID '{id}' not found.");

            if (original.Status != UnitJournalEntryStatus.Posted)
                throw new InvalidOperationException("Only posted entries can be reversed.");

            var now = DateTime.UtcNow;
            var reversalNumber = await GenerateEntryNumberAsync(cancellationToken);

            // Create reversal entry
            var reversal = new UnitJournalEntry
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                EntryNumber = reversalNumber,
                EntryDate = now,
                Description = $"Reversal of {original.EntryNumber}: {reason}",
                FiscalYearId = original.FiscalYearId,
                FiscalPeriodId = original.FiscalPeriodId,
                Status = UnitJournalEntryStatus.Posted,
                IsReversal = true,
                ReversedEntryId = original.Id,
                ReversalReason = reason,
                PostedAt = now,
                PostedBy = UserId,
                PostedByName = UserName,
                CreatedAt = now,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<UnitJournalEntry>().AddAsync(reversal);

            // Create reversed lines and update balances
            int lineNumber = 1;
            foreach (var origLine in original.Lines.Where(l => !l.IsDeleted))
            {
                // Reverse quantity
                var reversalLine = new UnitJournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    UnitJournalEntryId = reversal.Id,
                    LineNumber = lineNumber++,
                    UnitAccountId = origLine.UnitAccountId,
                    Quantity = -origLine.Quantity,
                    Description = $"Reversal: {origLine.Description}",
                    CreatedAt = now,
                    CreatedBy = UserName
                };
                await _unitOfWork.Repository<UnitJournalEntryLine>().AddAsync(reversalLine);

                // Update account balance
                var account = await _unitOfWork.Repository<UnitAccount>()
                    .FirstOrDefaultAsync(a => a.Id == origLine.UnitAccountId && !a.IsDeleted);

                if (account != null)
                {
                    account.CurrentBalance -= origLine.Quantity;
                    account.UpdatedAt = now;
                    account.UpdatedBy = "system";
                    await _unitOfWork.Repository<UnitAccount>().UpdateAsync(account);
                }
            }

            // Mark original as reversed
            original.Status = UnitJournalEntryStatus.Reversed;
            original.ReversalEntryId = reversal.Id;
            original.UpdatedAt = now;
            original.UpdatedBy = UserName;
            await _unitOfWork.Repository<UnitJournalEntry>().UpdateAsync(original);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit journal entry {EntryNumber} reversed by {User}. Reversal: {ReversalNumber}", 
                original.EntryNumber, UserName, reversalNumber);

            return MapToDto(reversal);
        }

        public async Task<bool> ValidateEntryAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.Repository<UnitJournalEntry>()
                .GetQueryable(e => e.Id == id && !e.IsDeleted)
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(cancellationToken);

            if (entry == null) return false;
            return entry.Lines.Any(l => !l.IsDeleted);
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
