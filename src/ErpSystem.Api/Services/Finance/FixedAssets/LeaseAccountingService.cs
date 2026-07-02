using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets
{
    public class LeaseAccountingService : ILeaseAccountingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IFixedAssetService _fixedAssetService;
        private readonly IDocumentNumberingService _documentNumberingService;

        public LeaseAccountingService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            IFixedAssetService fixedAssetService,
            IDocumentNumberingService documentNumberingService)
        {
            _context = context;
            _currentUser = currentUser;
            _fixedAssetService = fixedAssetService;
            _documentNumberingService = documentNumberingService;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
        private string UserName => _currentUser.UserName ?? "system";

        // ── Queries ──────────────────────────────────────────────────────

        public async Task<IEnumerable<LeaseContractListDto>> GetLeasesAsync()
        {
            return await _context.LeaseContracts
                .Include(l => l.Lessor)
                .Where(l => l.TenantId == TenantId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => MapToListDto(l))
                .ToListAsync();
        }

        public async Task<LeaseContractDetailDto?> GetLeaseByIdAsync(Guid id)
        {
            var lease = await _context.LeaseContracts
                .Include(l => l.Lessor)
                .Include(l => l.ScheduleLines.OrderBy(s => s.PeriodNumber))
                .Where(l => l.TenantId == TenantId && l.Id == id)
                .FirstOrDefaultAsync();

            return lease == null ? null : MapToDetailDto(lease);
        }

        // ── Preview ──────────────────────────────────────────────────────

        public Task<List<LeaseScheduleLineDto>> PreviewScheduleAsync(CreateLeaseContractDto dto)
        {
            var (_, schedule) = CalculatePvAndSchedule(
                dto.MonthlyPaymentAmount,
                dto.AnnualDiscountRate,
                dto.PaymentFrequency,
                dto.StartDate,
                dto.EndDate);

            var result = schedule.Select((s, i) => new LeaseScheduleLineDto
            {
                PeriodNumber = i + 1,
                PeriodDate = s.PeriodDate,
                PaymentAmount = s.PaymentAmount,
                InterestExpense = s.InterestExpense,
                PrincipalReduction = s.PrincipalReduction,
                RemainingLiability = s.RemainingLiability,
                IsPosted = false
            }).ToList();

            return Task.FromResult(result);
        }

        // ── Create ───────────────────────────────────────────────────────

        public async Task<LeaseContractDetailDto> CreateLeaseAsync(CreateLeaseContractDto dto)
        {
            // Validate lessor exists
            var lessorExists = await _context.BusinessPartners
                .AnyAsync(bp => bp.TenantId == TenantId && bp.Id == dto.LessorId);
            if (!lessorExists)
                throw new InvalidOperationException("Lessor (Business Partner) not found.");

            // Validate dates
            if (dto.EndDate <= dto.StartDate)
                throw new InvalidOperationException("End date must be after start date.");

            var (pv, schedule) = CalculatePvAndSchedule(
                dto.MonthlyPaymentAmount,
                dto.AnnualDiscountRate,
                dto.PaymentFrequency,
                dto.StartDate,
                dto.EndDate);

            var lease = new LeaseContract
            {
                TenantId = TenantId,
                ContractNumber = dto.ContractNumber,
                Description = dto.Description,
                LessorId = dto.LessorId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                MonthlyPaymentAmount = dto.MonthlyPaymentAmount,
                PaymentFrequency = dto.PaymentFrequency,
                AnnualDiscountRate = dto.AnnualDiscountRate,
                TotalPeriods = schedule.Count,
                PresentValue = pv,
                Status = LeaseStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            // Create schedule lines
            for (int i = 0; i < schedule.Count; i++)
            {
                lease.ScheduleLines.Add(new LeaseScheduleLine
                {
                    TenantId = TenantId,
                    PeriodNumber = i + 1,
                    PeriodDate = schedule[i].PeriodDate,
                    PaymentAmount = schedule[i].PaymentAmount,
                    InterestExpense = schedule[i].InterestExpense,
                    PrincipalReduction = schedule[i].PrincipalReduction,
                    RemainingLiability = schedule[i].RemainingLiability,
                    IsPosted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                });
            }

            _context.LeaseContracts.Add(lease);
            await SaveWithConcurrencyAsync();

            return await GetLeaseByIdAsync(lease.Id)
                ?? throw new InvalidOperationException("Failed to create lease.");
        }

        // ── Activate (with ROU asset creation — EzFMC gap fix) ───────────

        public async Task<LeaseContractDetailDto> ActivateLeaseAsync(Guid leaseId)
        {
            var lease = await _context.LeaseContracts
                .Include(l => l.Lessor)
                .FirstOrDefaultAsync(l => l.TenantId == TenantId && l.Id == leaseId)
                ?? throw new KeyNotFoundException("Lease contract not found.");

            if (lease.Status != LeaseStatus.Draft)
                throw new InvalidOperationException($"Cannot activate a lease in '{lease.Status}' status.");

            // Load finance settings for GL account defaults
            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == TenantId);

            if (settings?.LeaseRouAssetAccountId == null || settings?.LeaseLiabilityAccountId == null)
                throw new InvalidOperationException(
                    "Lease GL accounts not configured. Set ROU Asset and Lease Liability accounts in Finance Settings.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ── Gap Fix: Actually create the ROU Fixed Asset ──
                // EzFMC's original code only posted the GL journal but never created the FixedAsset record.

                // Find or use a default category for ROU assets
                var rouCategory = await _context.FixedAssetCategories
                    .FirstOrDefaultAsync(c => c.TenantId == TenantId &&
                        (c.Code == "ROU" || c.Name.Contains("Right-of-Use") || c.Name.Contains("Lease")));

                Guid categoryId;
                if (rouCategory != null)
                {
                    categoryId = rouCategory.Id;
                }
                else
                {
                    // Fall back to first available category
                    var fallback = await _context.FixedAssetCategories
                        .FirstOrDefaultAsync(c => c.TenantId == TenantId)
                        ?? throw new InvalidOperationException(
                            "No fixed asset categories found. Create an ROU/Lease category first.");
                    categoryId = fallback.Id;
                }

                var assetCode = await _fixedAssetService.GenerateAssetCodeAsync(categoryId);
                var rouAsset = await _fixedAssetService.CreateAsync(new CreateFixedAssetDto
                {
                    AssetCode = assetCode,
                    Name = $"ROU Asset — {lease.ContractNumber}",
                    Description = $"Right-of-Use asset for lease {lease.ContractNumber} ({lease.Lessor?.PartnerName ?? "N/A"})",
                    FixedAssetCategoryId = categoryId,
                    PurchaseDate = lease.StartDate,
                    PlacedInServiceDate = lease.StartDate,
                    PurchasePrice = lease.PresentValue,
                    AcquisitionCost = lease.PresentValue,
                    DepreciationMethod = DepreciationMethod.StraightLine,
                    UsefulLifeMonths = (int)((lease.EndDate - lease.StartDate).TotalDays / 30.44),
                    ResidualValue = 0
                });

                lease.RouAssetId = rouAsset.Id;
                lease.Status = LeaseStatus.Active;
                lease.UpdatedAt = DateTime.UtcNow;
                lease.UpdatedBy = UserName;

                // ── Post recognition GL journal ──
                // DR ROU Asset Account    (balance sheet)
                // CR Lease Liability      (balance sheet)
                var journal = new JournalEntry
                {
                    TenantId = TenantId,
                    JournalEntryNumber = await _documentNumberingService.GenerateAsync(
                        DocumentNumberingModules.Finance,
                        FinanceDocumentTypes.LeaseJournal,
                        TenantId,
                        lease.StartDate,
                        nameof(JournalEntry)),
                    EntryDate = lease.StartDate,
                    ReferenceNumber = $"LEASE-ACT-{lease.ContractNumber}",
                    Description = $"Lease activation — ROU asset recognition: {lease.Description}",
                    PostingStatus = "Posted",
                    JournalType = "System Generated",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                };

                journal.Transactions.Add(new AccountTransaction
                {
                    TenantId = TenantId,
                    AccountId = settings.LeaseRouAssetAccountId.Value,
                    TransactionDate = lease.StartDate,
                    DebitAmount = lease.PresentValue,
                    CreditAmount = 0,
                    Description = $"ROU Asset — {lease.ContractNumber}",
                    CreatedAt = DateTime.UtcNow
                });

                journal.Transactions.Add(new AccountTransaction
                {
                    TenantId = TenantId,
                    AccountId = settings.LeaseLiabilityAccountId.Value,
                    TransactionDate = lease.StartDate,
                    DebitAmount = 0,
                    CreditAmount = lease.PresentValue,
                    Description = $"Lease liability — {lease.ContractNumber}",
                    CreatedAt = DateTime.UtcNow
                });

                _context.JournalEntries.Add(journal);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException(
                    "The lease was modified by another user. Please refresh and try again.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return await GetLeaseByIdAsync(leaseId)
                ?? throw new InvalidOperationException("Failed to activate lease.");
        }

        // ── Post Period Journal ──────────────────────────────────────────

        public async Task<LeaseContractDetailDto> PostPeriodJournalAsync(Guid leaseId, Guid scheduleLineId)
        {
            var lease = await _context.LeaseContracts
                .Include(l => l.ScheduleLines)
                .FirstOrDefaultAsync(l => l.TenantId == TenantId && l.Id == leaseId)
                ?? throw new KeyNotFoundException("Lease contract not found.");

            if (lease.Status != LeaseStatus.Active)
                throw new InvalidOperationException("Can only post journals for active leases.");

            var line = lease.ScheduleLines.FirstOrDefault(s => s.Id == scheduleLineId)
                ?? throw new KeyNotFoundException("Schedule line not found.");

            if (line.IsPosted)
                throw new InvalidOperationException($"Period {line.PeriodNumber} has already been posted.");

            // Ensure periods are posted in order
            var previousUnposted = lease.ScheduleLines
                .Any(s => s.PeriodNumber < line.PeriodNumber && !s.IsPosted);
            if (previousUnposted)
                throw new InvalidOperationException("Earlier periods must be posted first.");

            // Load finance settings for GL accounts
            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == TenantId);

            if (settings?.LeaseLiabilityAccountId == null || settings?.LeaseInterestExpenseAccountId == null)
                throw new InvalidOperationException(
                    "Lease GL accounts not configured. Set Liability and Interest Expense accounts in Finance Settings.");

            // DR Interest Expense
            // DR Lease Liability (principal reduction)
            // CR Cash/Payable (payment amount)
            var journal = new JournalEntry
            {
                TenantId = TenantId,
                JournalEntryNumber = await _documentNumberingService.GenerateAsync(
                    DocumentNumberingModules.Finance,
                    FinanceDocumentTypes.LeaseJournal,
                    TenantId,
                    line.PeriodDate,
                    nameof(JournalEntry)),
                EntryDate = line.PeriodDate,
                ReferenceNumber = $"LEASE-PMT-{lease.ContractNumber}-P{line.PeriodNumber}",
                Description = $"Lease payment period {line.PeriodNumber}: {lease.ContractNumber}",
                PostingStatus = "Posted",
                JournalType = "System Generated",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            // Debit: Interest Expense
            if (line.InterestExpense > 0)
            {
                journal.Transactions.Add(new AccountTransaction
                {
                    TenantId = TenantId,
                    AccountId = settings.LeaseInterestExpenseAccountId.Value,
                    TransactionDate = line.PeriodDate,
                    DebitAmount = line.InterestExpense,
                    CreditAmount = 0,
                    Description = $"Interest expense — {lease.ContractNumber} P{line.PeriodNumber}",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Debit: Lease Liability (principal reduction)
            if (line.PrincipalReduction > 0)
            {
                journal.Transactions.Add(new AccountTransaction
                {
                    TenantId = TenantId,
                    AccountId = settings.LeaseLiabilityAccountId.Value,
                    TransactionDate = line.PeriodDate,
                    DebitAmount = line.PrincipalReduction,
                    CreditAmount = 0,
                    Description = $"Lease liability reduction — {lease.ContractNumber} P{line.PeriodNumber}",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Credit: AP control (use AP control from settings, or Liability account as fallback)
            var creditAccountId = settings.ControlAccountApId ?? settings.LeaseLiabilityAccountId.Value;
            journal.Transactions.Add(new AccountTransaction
            {
                TenantId = TenantId,
                AccountId = creditAccountId,
                TransactionDate = line.PeriodDate,
                DebitAmount = 0,
                CreditAmount = line.PaymentAmount,
                Description = $"Lease payment — {lease.ContractNumber} P{line.PeriodNumber}",
                CreatedAt = DateTime.UtcNow
            });

            _context.JournalEntries.Add(journal);
            line.IsPosted = true;

            // Check if all periods are posted — complete the lease
            if (lease.ScheduleLines.All(s => s.IsPosted))
            {
                lease.Status = LeaseStatus.Completed;
            }

            lease.UpdatedAt = DateTime.UtcNow;
            lease.UpdatedBy = UserName;

            await SaveWithConcurrencyAsync();

            return await GetLeaseByIdAsync(leaseId)
                ?? throw new InvalidOperationException("Failed to post period journal.");
        }

        // ── PV & Amortization Calculation ────────────────────────────────

        private static (decimal PresentValue, List<ScheduleCalcLine> Schedule) CalculatePvAndSchedule(
            decimal paymentAmount,
            decimal annualRate,
            PaymentFrequency frequency,
            DateTime startDate,
            DateTime endDate)
        {
            int monthsPerPeriod = (int)frequency;
            var totalMonths = ((endDate.Year - startDate.Year) * 12) + endDate.Month - startDate.Month;
            var totalPeriods = Math.Max(1, totalMonths / monthsPerPeriod);

            // Periodic rate from annual rate
            double periodicRate = (double)(annualRate / (12m / monthsPerPeriod));

            // Present Value of ordinary annuity: PV = PMT × [(1 - (1+r)^-n) / r]
            double pvFactor;
            if (periodicRate > 0)
                pvFactor = (1 - Math.Pow(1 + periodicRate, -totalPeriods)) / periodicRate;
            else
                pvFactor = totalPeriods; // No discount

            decimal pv = Math.Round((decimal)((double)paymentAmount * pvFactor), 2);

            // Build amortization schedule
            var schedule = new List<ScheduleCalcLine>();
            decimal remainingLiability = pv;

            for (int i = 0; i < totalPeriods; i++)
            {
                var periodDate = startDate.AddMonths(monthsPerPeriod * (i + 1));
                var interest = Math.Round(remainingLiability * (decimal)periodicRate, 2);
                var principal = paymentAmount - interest;

                // Last period: adjust for rounding
                if (i == totalPeriods - 1)
                {
                    principal = remainingLiability;
                    interest = paymentAmount - principal;
                    if (interest < 0) interest = 0;
                }

                remainingLiability -= principal;
                if (remainingLiability < 0) remainingLiability = 0;

                schedule.Add(new ScheduleCalcLine
                {
                    PeriodDate = periodDate,
                    PaymentAmount = paymentAmount,
                    InterestExpense = interest,
                    PrincipalReduction = principal,
                    RemainingLiability = remainingLiability
                });
            }

            return (pv, schedule);
        }

        private record ScheduleCalcLine
        {
            public DateTime PeriodDate { get; init; }
            public decimal PaymentAmount { get; init; }
            public decimal InterestExpense { get; init; }
            public decimal PrincipalReduction { get; init; }
            public decimal RemainingLiability { get; init; }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private async Task SaveWithConcurrencyAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "The record was modified by another user. Please refresh and try again.");
            }
        }

        private static LeaseContractListDto MapToListDto(LeaseContract l) => new()
        {
            Id = l.Id,
            ContractNumber = l.ContractNumber,
            Description = l.Description,
            LessorName = l.Lessor?.PartnerName,
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            MonthlyPaymentAmount = l.MonthlyPaymentAmount,
            PresentValue = l.PresentValue,
            Status = l.Status,
            RouAssetId = l.RouAssetId,
            CreatedAt = l.CreatedAt
        };

        private static LeaseContractDetailDto MapToDetailDto(LeaseContract l) => new()
        {
            Id = l.Id,
            ContractNumber = l.ContractNumber,
            Description = l.Description,
            LessorId = l.LessorId,
            LessorName = l.Lessor?.PartnerName,
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            MonthlyPaymentAmount = l.MonthlyPaymentAmount,
            PaymentFrequency = l.PaymentFrequency,
            AnnualDiscountRate = l.AnnualDiscountRate,
            TotalPeriods = l.TotalPeriods,
            PresentValue = l.PresentValue,
            Status = l.Status,
            RouAssetId = l.RouAssetId,
            CreatedAt = l.CreatedAt,
            CreatedBy = l.CreatedBy,
            UpdatedAt = l.UpdatedAt,
            UpdatedBy = l.UpdatedBy,
            ScheduleLines = l.ScheduleLines.Select(s => new LeaseScheduleLineDto
            {
                Id = s.Id,
                PeriodNumber = s.PeriodNumber,
                PeriodDate = s.PeriodDate,
                PaymentAmount = s.PaymentAmount,
                InterestExpense = s.InterestExpense,
                PrincipalReduction = s.PrincipalReduction,
                RemainingLiability = s.RemainingLiability,
                IsPosted = s.IsPosted
            }).ToList()
        };
    }
}
