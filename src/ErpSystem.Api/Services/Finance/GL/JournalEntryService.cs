using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Data;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;
using AutoMapper;

namespace ErpSystem.Api.Services.Finance.GL
{
    public class JournalEntryService : IJournalEntryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IGeneralLedgerService _generalLedgerService;
        private readonly IMapper _mapper;

        public JournalEntryService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IGeneralLedgerService generalLedgerService,
            IMapper mapper)
        {
            _context = context;
            _currentUserService = currentUserService;
            _generalLedgerService = generalLedgerService;
            _mapper = mapper;
        }

        public async Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesAsync(CancellationToken cancellationToken = default)
        {
            var tenantId = _currentUserService.TenantId;
            var entries = await _context.JournalEntries
                .Include(j => j.Transactions)
                .ThenInclude(t => t.Account)
                .Where(j => j.TenantId == tenantId && !j.IsDeleted)
                .OrderByDescending(j => j.EntryDate)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<JournalEntryDto>>(entries);
        }

        public async Task<JournalEntryDto?> GetJournalEntryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .ThenInclude(t => t.Account)
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

            return _mapper.Map<JournalEntryDto>(entry);
        }

        public async Task<JournalEntryDto?> GetJournalEntryByNumberAsync(string journalNumber, CancellationToken cancellationToken = default)
        {
            var tenantId = _currentUserService.TenantId;
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .ThenInclude(t => t.Account)
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.JournalEntryNumber == journalNumber, cancellationToken);

            return _mapper.Map<JournalEntryDto>(entry);
        }

        public async Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var tenantId = _currentUserService.TenantId;
            var entries = await _context.JournalEntries
                .Include(j => j.Transactions)
                .ThenInclude(t => t.Account)
                .Where(j => j.TenantId == tenantId && !j.IsDeleted && j.EntryDate >= startDate && j.EntryDate <= endDate)
                .OrderByDescending(j => j.EntryDate)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<JournalEntryDto>>(entries);
        }

        public async Task<JournalEntryDto> CreateJournalEntryAsync(CreateJournalEntryDto dto, CancellationToken cancellationToken = default)
        {
            var tenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant context is required.");

            // Basic validation
            if (dto.Transactions == null || !dto.Transactions.Any())
                throw new InvalidOperationException("Journal entry must have at least one transaction line.");

            var debitSum = dto.Transactions.Where(t => t.TransactionType == "Debit").Sum(t => t.Amount);
            var creditSum = dto.Transactions.Where(t => t.TransactionType == "Credit").Sum(t => t.Amount);

            if (debitSum != creditSum)
                throw new InvalidOperationException($"Journal entry is not balanced. Total Debit: {debitSum}, Total Credit: {creditSum}");

            // Create Entity
            var journalEntry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                JournalEntryNumber = dto.JournalNumber ?? await _generalLedgerService.GenerateJournalEntryNumberAsync(cancellationToken),
                EntryDate = dto.TransactionDate,
                Description = dto.Description,
                ReferenceNumber = dto.Reference,
                SourceModule = dto.SourceModule,
                TotalDebitAmount = debitSum,
                TotalCreditAmount = creditSum,
                PostingStatus = "Draft", // Created as Draft
                IsBalanced = true,
                TenantId = tenantId,
                BookClassification = "IFRS", // Default
                FiscalPeriodId = dto.FiscalPeriodId ?? await GetOpenFiscalPeriodIdAsync(dto.TransactionDate, tenantId)
            };

            // Process Transactions
            int lineNum = 1;
            foreach (var txnDto in dto.Transactions)
            {
                var transaction = new AccountTransaction
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = txnDto.AccountId,
                    Description = txnDto.Description,
                    DebitAmount = txnDto.TransactionType == "Debit" ? txnDto.Amount : 0,
                    CreditAmount = txnDto.TransactionType == "Credit" ? txnDto.Amount : 0,
                    TransactionDate = dto.TransactionDate,
                    LineNumber = lineNum++,
                    TransactionCurrency = txnDto.CurrencyCode,
                    ExchangeRate = txnDto.ExchangeRate,
                    ForeignCurrencyAmount = txnDto.ForeignAmount,
                    TenantId = tenantId,
                    FiscalPeriodId = journalEntry.FiscalPeriodId,
                    BookClassification = "IFRS"
                };
                
                _context.AccountTransactions.Add(transaction);
            }

            _context.JournalEntries.Add(journalEntry);
            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<JournalEntryDto>(journalEntry);
        }

        public async Task<JournalEntryDto> UpdateJournalEntryAsync(Guid id, UpdateJournalEntryDto dto, CancellationToken cancellationToken = default)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

            if (entry == null) throw new ArgumentException($"Journal Entry {id} not found.");
            if (entry.PostingStatus != "Draft") throw new InvalidOperationException("Only Draft journal entries can be updated.");

            // Update Header
            if (dto.TransactionDate.HasValue) entry.EntryDate = dto.TransactionDate.Value;
            if (dto.Description != null) entry.Description = dto.Description;
            if (dto.Reference != null) entry.ReferenceNumber = dto.Reference;

            // Simple implementation: Remove all existing transactions and re-add (if transactions provided)
            // In a real app, you might want to reconcile
            // For MVP, if transactions are provided in update, replace them.
            
            // NOTE: The DTO might not contain transactions for partial updates. 
            // Assuming full update for now if we were to implement transaction updates here.
            
            await _context.SaveChangesAsync(cancellationToken);
            return _mapper.Map<JournalEntryDto>(entry);
        }

        public async Task DeleteJournalEntryAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _context.JournalEntries.FindAsync(new object[] { id }, cancellationToken);
            if (entry == null) throw new ArgumentException($"Journal Entry {id} not found.");
            if (entry.PostingStatus != "Draft") throw new InvalidOperationException("Only Draft journal entries can be deleted.");

            entry.IsDeleted = true;
            entry.DeletedAt = DateTime.UtcNow;
            
            // Soft delete transactions too
            var transactions = await _context.AccountTransactions.Where(t => t.JournalEntryId == id).ToListAsync(cancellationToken);
            foreach(var t in transactions)
            {
                t.IsDeleted = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<JournalEntryDto> PostJournalEntryAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
            
            if (entry == null) throw new ArgumentException($"Journal Entry {id} not found.");
            if (entry.PostingStatus == "Posted") throw new InvalidOperationException("Journal Entry is already posted.");
            if (entry.PostingStatus != "Draft" && entry.PostingStatus != "Approved")
                throw new InvalidOperationException($"Cannot post entry with status '{entry.PostingStatus}'. Entry must be 'Draft' or 'Approved'.");

            // Validate Balance
            var debit = entry.Transactions.Sum(t => t.DebitAmount);
            var credit = entry.Transactions.Sum(t => t.CreditAmount);
            if (debit != credit) throw new InvalidOperationException("Journal Entry must be balanced to post.");

            // Update Account Balances
            foreach (var txn in entry.Transactions)
            {
                var account = await _context.Accounts.FindAsync(txn.AccountId);
                if (account == null) throw new InvalidOperationException($"Account {txn.AccountId} not found.");

                // Validate Control Account Posting
                if (account.IsControlAccount)
                {
                    var allowedModules = new[] { "AP", "AR", "INVENTORY", "TAX", "BANK", "SYSTEM", "POS" };
                    bool isSystemPosting = !string.IsNullOrEmpty(entry.SourceModule) && allowedModules.Contains(entry.SourceModule.ToUpper());
                    
                    if (!isSystemPosting)
                    {
                        throw new InvalidOperationException($"Direct manual posting to Control Account '{account.AccountName}' is not allowed.");
                    }
                }

                if (txn.DebitAmount > 0)
                {
                     if (account.AccountType == AccountType.Asset || account.AccountType == AccountType.Expense)
                            account.Balance += txn.DebitAmount;
                        else
                            account.Balance -= txn.DebitAmount;
                }
                else // Credit
                {
                     if (account.AccountType == AccountType.Liability || account.AccountType == AccountType.Equity || account.AccountType == AccountType.Revenue)
                            account.Balance += txn.CreditAmount;
                        else
                            account.Balance -= txn.CreditAmount;
                }
            }

            entry.PostingStatus = "Posted";
            entry.PostingDate = DateTime.UtcNow;
            // entry.PostedById = userId; 

            await _context.SaveChangesAsync(cancellationToken);
            return _mapper.Map<JournalEntryDto>(entry);
        }

        public async Task<JournalEntryDto> ReverseJournalEntryAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            var original = await _context.JournalEntries
                .Include(j => j.Transactions)
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

            if (original == null) throw new ArgumentException($"Journal Entry {id} not found.");
            if (original.PostingStatus != "Posted") throw new InvalidOperationException("Only posted entries can be reversed.");

            var reversal = new JournalEntry
            {
                Id = Guid.NewGuid(),
                JournalEntryNumber = await _generalLedgerService.GenerateJournalEntryNumberAsync(cancellationToken),
                EntryDate = DateTime.UtcNow,
                Description = $"Reversal of {original.ReferenceNumber}: {reason}",
                ReferenceNumber = $"REV-{original.ReferenceNumber}",
                SourceModule = original.SourceModule,
                TotalDebitAmount = original.TotalCreditAmount, // Swap
                TotalCreditAmount = original.TotalDebitAmount, // Swap
                PostingStatus = "Posted", // Auto-post reversal
                PostingDate = DateTime.UtcNow,
                IsBalanced = true,
                TenantId = original.TenantId,
                FiscalPeriodId = original.FiscalPeriodId, // Should strictly be current open period, simplified for now
                BookClassification = original.BookClassification
            };

            int lineNum = 1;
            foreach (var txn in original.Transactions)
            {
                // Create reversing transaction (Debit -> Credit, Credit -> Debit)
                var revTxn = new AccountTransaction
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = reversal.Id,
                    AccountId = txn.AccountId,
                    Description = $"Reversal: {txn.Description}",
                    DebitAmount = txn.CreditAmount, // Swap
                    CreditAmount = txn.DebitAmount, // Swap
                    TransactionDate = reversal.EntryDate,
                    LineNumber = lineNum++,
                    TransactionCurrency = txn.TransactionCurrency,
                    ExchangeRate = txn.ExchangeRate,
                    ForeignCurrencyAmount = txn.ForeignCurrencyAmount,
                    TenantId = reversal.TenantId,
                    FiscalPeriodId = reversal.FiscalPeriodId,
                    BookClassification = txn.BookClassification
                };

                // Update Balances (Reversing effect)
                var account = await _context.Accounts.FindAsync(txn.AccountId);
                if (account != null)
                {
                    if (revTxn.DebitAmount > 0)
                    {
                         if (account.AccountType == AccountType.Asset || account.AccountType == AccountType.Expense)
                                account.Balance += revTxn.DebitAmount;
                            else
                                account.Balance -= revTxn.DebitAmount;
                    }
                    else
                    {
                         if (account.AccountType == AccountType.Liability || account.AccountType == AccountType.Equity || account.AccountType == AccountType.Revenue)
                                account.Balance += revTxn.CreditAmount;
                            else
                                account.Balance -= revTxn.CreditAmount;
                    }
                }
                
                _context.AccountTransactions.Add(revTxn);
            }

            original.PostingStatus = "Reversed";
            _context.JournalEntries.Add(reversal);
            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<JournalEntryDto>(reversal);
        }

        public async Task<bool> ValidateBalanceAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
            
            if (entry == null) return false;

            return entry.Transactions.Sum(t => t.DebitAmount) == entry.Transactions.Sum(t => t.CreditAmount);
        }

        public async Task<string> GenerateJournalEntryNumberAsync(CancellationToken cancellationToken = default)
        {
            return await _generalLedgerService.GenerateJournalEntryNumberAsync(cancellationToken);
        }
        
        // Helper to get fiscal period (Duplicated from GeneralLedgerService - should be centralized)
        private async Task<Guid> GetOpenFiscalPeriodIdAsync(DateTime date, Guid tenantId)
        {
            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date && p.TenantId == tenantId);
            
            if (period != null) return period.Id;

            // Simple fallback
            return Guid.Empty; // Should fallback to creating one or throwing error
        }

        public async Task UpdateApprovalStatusAsync(
            Guid id,
            string postingStatus,
            string approvalStatus,
            Guid? approvedByUserId = null,
            string? rejectionReason = null,
            CancellationToken cancellationToken = default)
        {
            var entry = await _context.JournalEntries.FindAsync(new object[] { id }, cancellationToken);
            if (entry == null) throw new ArgumentException($"Journal Entry {id} not found.");

            entry.PostingStatus = postingStatus;
            entry.ApprovalStatus = approvalStatus;

            if (approvedByUserId.HasValue)
            {
                entry.ApprovedByUserId = approvedByUserId;
                entry.ApprovedDate = DateTime.UtcNow;
            }

            if (rejectionReason != null)
            {
                entry.RejectionReason = rejectionReason;
            }

            if (approvalStatus == "Pending")
            {
                entry.RequiresApproval = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
