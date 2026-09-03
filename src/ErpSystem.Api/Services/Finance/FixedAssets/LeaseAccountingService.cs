using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Api.Services.Finance;
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
        private readonly IFinancePostingEngine _postingEngine;
        private readonly IFixedAssetDimensionService _fixedAssetDimensions;
        private static readonly FinancePostingProducerContext RecognitionProducer =
            new(FinanceDimensionRouteId.FinanceLeaseRecognition);
        private static readonly FinancePostingProducerContext PeriodProducer =
            new(FinanceDimensionRouteId.FinanceLeasePeriodPosting);

        public LeaseAccountingService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            IFixedAssetService fixedAssetService,
            IDocumentNumberingService documentNumberingService,
            IFinancePostingEngine postingEngine,
            IFixedAssetDimensionService fixedAssetDimensions)
        {
            _context = context;
            _currentUser = currentUser;
            _fixedAssetService = fixedAssetService;
            _documentNumberingService = documentNumberingService;
            _postingEngine = postingEngine;
            _fixedAssetDimensions = fixedAssetDimensions;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
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

            if (lease == null)
                return null;
            var result = MapToDetailDto(lease);
            var settings = await _context.FinanceSettings.AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && !item.IsDeleted);
            if (settings?.LeaseRouAssetAccountId.HasValue == true
                && settings.LeaseLiabilityAccountId.HasValue)
            {
                result.RecognitionFinanceDimensions = await _fixedAssetDimensions.GetAsync(
                    RecognitionProducer, lease.Id, lease.StartDate,
                    BuildRecognitionLines(lease, settings));
            }
            if (settings?.LeaseLiabilityAccountId.HasValue == true
                && settings.LeaseInterestExpenseAccountId.HasValue)
            {
                foreach (var scheduleLine in lease.ScheduleLines)
                {
                    var scheduleDto = result.ScheduleLines.Single(item => item.Id == scheduleLine.Id);
                    scheduleDto.FinanceDimensions = await _fixedAssetDimensions.GetAsync(
                        PeriodProducer, scheduleLine.Id, scheduleLine.PeriodDate,
                        BuildPeriodLines(lease, scheduleLine, settings));
                }
            }
            return result;
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

        public async Task<LeaseContractDetailDto> ActivateLeaseAsync(
            Guid leaseId,
            ActivateLeaseDto? dto = null,
            CancellationToken cancellationToken = default)
        {
            var lease = await _context.LeaseContracts
                .Include(l => l.Lessor)
                .FirstOrDefaultAsync(l => l.TenantId == TenantId && l.Id == leaseId, cancellationToken)
                ?? throw new KeyNotFoundException("Lease contract not found.");

            if (lease.Status is LeaseStatus.Active or LeaseStatus.Completed)
                return await GetLeaseByIdAsync(leaseId)
                    ?? throw new InvalidOperationException("Failed to retrieve activated lease.");
            if (lease.Status != LeaseStatus.Draft)
                throw new InvalidOperationException($"Cannot activate a lease in '{lease.Status}' status.");

            // Load finance settings for GL account defaults
            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == TenantId, cancellationToken);

            if (settings?.LeaseRouAssetAccountId == null || settings?.LeaseLiabilityAccountId == null)
                throw new InvalidOperationException(
                    "Lease GL accounts not configured. Set ROU Asset and Lease Liability accounts in Finance Settings.");

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                // ── Gap Fix: Actually create the ROU Fixed Asset ──
                // EzFMC's original code only posted the GL journal but never created the FixedAsset record.

                // Find or use a default category for ROU assets
                var rouCategory = await _context.FixedAssetCategories
                    .FirstOrDefaultAsync(c => c.TenantId == TenantId &&
                        (c.Code == "ROU" || c.Name.Contains("Right-of-Use") || c.Name.Contains("Lease")), cancellationToken);

                Guid categoryId;
                if (rouCategory != null)
                {
                    categoryId = rouCategory.Id;
                }
                else
                {
                    // Fall back to first available category
                    var fallback = await _context.FixedAssetCategories
                        .FirstOrDefaultAsync(c => c.TenantId == TenantId, cancellationToken)
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

                var postingLines = BuildRecognitionLines(lease, settings);
                await _fixedAssetDimensions.SynchronizeAsync(
                    RecognitionProducer, lease.Id, lease.StartDate, postingLines,
                    dto?.FinanceDimensions, inheritedAssetJournalBySourceLine: null,
                    "Lease recognition dimensions synchronized.", cancellationToken);
                await _fixedAssetDimensions.ValidateFreezeAndApplyAsync(
                    RecognitionProducer, lease.Id, lease.StartDate, postingLines, cancellationToken);
                await _postingEngine.PostAsync(BuildLeasePostingRequest(
                    RecognitionProducer,
                    lease.Id,
                    lease.ContractNumber,
                    lease.StartDate,
                    $"Lease activation — ROU asset recognition: {lease.Description}",
                    $"LEASE-RECOGNITION:{TenantId:D}:{lease.Id:D}",
                    settings.BaseCurrency,
                    postingLines), RecognitionProducer, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new InvalidOperationException(
                        "The lease was modified by another user. Please refresh and try again.");
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });

            return await GetLeaseByIdAsync(leaseId)
                ?? throw new InvalidOperationException("Failed to activate lease.");
        }

        // ── Post Period Journal ──────────────────────────────────────────

        public async Task<LeaseContractDetailDto> PostPeriodJournalAsync(
            Guid leaseId,
            Guid scheduleLineId,
            PostLeasePeriodDto? dto = null,
            CancellationToken cancellationToken = default)
        {
            var lease = await _context.LeaseContracts
                .Include(l => l.ScheduleLines)
                .FirstOrDefaultAsync(l => l.TenantId == TenantId && l.Id == leaseId, cancellationToken)
                ?? throw new KeyNotFoundException("Lease contract not found.");

            if (lease.Status != LeaseStatus.Active)
                throw new InvalidOperationException("Can only post journals for active leases.");

            var line = lease.ScheduleLines.FirstOrDefault(s => s.Id == scheduleLineId)
                ?? throw new KeyNotFoundException("Schedule line not found.");

            if (line.IsPosted)
                return await GetLeaseByIdAsync(leaseId)
                    ?? throw new InvalidOperationException("Failed to retrieve posted lease period.");

            // Ensure periods are posted in order
            var previousUnposted = lease.ScheduleLines
                .Any(s => s.PeriodNumber < line.PeriodNumber && !s.IsPosted);
            if (previousUnposted)
                throw new InvalidOperationException("Earlier periods must be posted first.");

            // Load finance settings for GL accounts
            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == TenantId, cancellationToken);

            if (settings?.LeaseLiabilityAccountId == null || settings?.LeaseInterestExpenseAccountId == null)
                throw new InvalidOperationException(
                    "Lease GL accounts not configured. Set Liability and Interest Expense accounts in Finance Settings.");

            var postingLines = BuildPeriodLines(lease, line, settings);
            await _fixedAssetDimensions.SynchronizeAsync(
                PeriodProducer, line.Id, line.PeriodDate, postingLines,
                dto?.FinanceDimensions, inheritedAssetJournalBySourceLine: null,
                "Lease period dimensions synchronized.", cancellationToken);
            await _fixedAssetDimensions.ValidateFreezeAndApplyAsync(
                PeriodProducer, line.Id, line.PeriodDate, postingLines, cancellationToken);
            await _postingEngine.PostAsync(BuildLeasePostingRequest(
                PeriodProducer,
                line.Id,
                $"{lease.ContractNumber}-P{line.PeriodNumber}",
                line.PeriodDate,
                $"Lease payment period {line.PeriodNumber}: {lease.ContractNumber}",
                $"LEASE-PERIOD:{TenantId:D}:{lease.Id:D}:{line.Id:D}",
                settings.BaseCurrency,
                postingLines), PeriodProducer, cancellationToken);
            line.IsPosted = true;

            // Check if all periods are posted — complete the lease
            if (lease.ScheduleLines.All(s => s.IsPosted))
            {
                lease.Status = LeaseStatus.Completed;
            }

            lease.UpdatedAt = DateTime.UtcNow;
            lease.UpdatedBy = UserName;

            await _context.SaveChangesAsync(cancellationToken);

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

        private static List<FinancePostingLineDto> BuildRecognitionLines(
            LeaseContract lease,
            FinanceSettings settings) =>
        [
            new FinancePostingLineDto
            {
                AccountId = settings.LeaseRouAssetAccountId!.Value,
                SourceDocumentLineId = FinanceSourceLineIdentity.Create(lease.Id, "ROU-ASSET", lease.Id),
                DebitAmount = lease.PresentValue,
                Description = $"ROU Asset — {lease.ContractNumber}",
                LineNumber = 1
            },
            new FinancePostingLineDto
            {
                AccountId = settings.LeaseLiabilityAccountId!.Value,
                SourceDocumentLineId = FinanceSourceLineIdentity.Create(lease.Id, "LEASE-LIABILITY", lease.Id),
                CreditAmount = lease.PresentValue,
                Description = $"Lease liability — {lease.ContractNumber}",
                LineNumber = 2
            }
        ];

        private static List<FinancePostingLineDto> BuildPeriodLines(
            LeaseContract lease,
            LeaseScheduleLine scheduleLine,
            FinanceSettings settings)
        {
            var lines = new List<FinancePostingLineDto>();
            if (scheduleLine.InterestExpense > 0m)
            {
                lines.Add(new FinancePostingLineDto
                {
                    AccountId = settings.LeaseInterestExpenseAccountId!.Value,
                    SourceDocumentLineId = FinanceSourceLineIdentity.Create(
                        scheduleLine.Id, "INTEREST", lease.Id, scheduleLine.Id),
                    DebitAmount = scheduleLine.InterestExpense,
                    Description = $"Interest expense — {lease.ContractNumber} P{scheduleLine.PeriodNumber}"
                });
            }
            if (scheduleLine.PrincipalReduction > 0m)
            {
                lines.Add(new FinancePostingLineDto
                {
                    AccountId = settings.LeaseLiabilityAccountId!.Value,
                    SourceDocumentLineId = FinanceSourceLineIdentity.Create(
                        scheduleLine.Id, "PRINCIPAL", lease.Id, scheduleLine.Id),
                    DebitAmount = scheduleLine.PrincipalReduction,
                    Description = $"Lease liability reduction — {lease.ContractNumber} P{scheduleLine.PeriodNumber}"
                });
            }
            lines.Add(new FinancePostingLineDto
            {
                AccountId = settings.ControlAccountApId ?? settings.LeaseLiabilityAccountId!.Value,
                SourceDocumentLineId = FinanceSourceLineIdentity.Create(
                    scheduleLine.Id, "PAYABLE", lease.Id, scheduleLine.Id),
                CreditAmount = scheduleLine.PaymentAmount,
                Description = $"Lease payment — {lease.ContractNumber} P{scheduleLine.PeriodNumber}"
            });
            for (var index = 0; index < lines.Count; index++)
                lines[index].LineNumber = index + 1;
            return lines;
        }

        private FinancePostingRequestV2Dto BuildLeasePostingRequest(
            FinancePostingProducerContext producer,
            Guid sourceDocumentId,
            string reference,
            DateTime postingDate,
            string description,
            string idempotencyKey,
            string? functionalCurrency,
            IReadOnlyList<FinancePostingLineDto> lines) => new()
        {
            SourceModule = producer.Definition.PostingSourceModule,
            OriginModuleCode = producer.Definition.ProducerModule,
            SourceDocumentType = producer.Definition.DocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentTenantId = TenantId,
            SourceDocumentReference = reference,
            Description = description,
            PostingDate = postingDate,
            JournalType = "System Generated",
            FunctionalCurrencyCode = string.IsNullOrWhiteSpace(functionalCurrency)
                ? "GHS"
                : functionalCurrency.Trim().ToUpperInvariant(),
            IdempotencyKey = idempotencyKey,
            Lines = lines
        };

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
