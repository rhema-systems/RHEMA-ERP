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
        private readonly IVendorInvoiceService _vendorInvoices;
        private static readonly FinancePostingProducerContext RecognitionProducer =
            new(FinanceDimensionRouteId.FinanceLeaseRecognition);
        private static readonly FinancePostingProducerContext HistoricalPeriodProducer =
            new(FinanceDimensionRouteId.FinanceLeasePeriodPosting);
        public LeaseAccountingService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            IFixedAssetService fixedAssetService,
            IDocumentNumberingService documentNumberingService,
            IFinancePostingEngine postingEngine,
            IFixedAssetDimensionService fixedAssetDimensions,
            IVendorInvoiceService vendorInvoices)
        {
            _context = context;
            _currentUser = currentUser;
            _fixedAssetService = fixedAssetService;
            _documentNumberingService = documentNumberingService;
            _postingEngine = postingEngine;
            _fixedAssetDimensions = fixedAssetDimensions;
            _vendorInvoices = vendorInvoices;
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
                    .ThenInclude(s => s.VendorInvoices)
                        .ThenInclude(invoice => invoice.PaymentAllocations)
                .Where(l => l.TenantId == TenantId && l.Id == id)
                .FirstOrDefaultAsync();

            if (lease == null)
                return null;
            var result = MapToDetailDto(lease);
            var settings = await _context.FinanceSettings.AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && !item.IsDeleted);
            if (lease.RecognitionPostingEventId.HasValue && lease.RecognitionJournalEntryId.HasValue &&
                lease.RouAssetAccountId.HasValue && lease.LeaseLiabilityAccountId.HasValue)
            {
                result.RecognitionFinanceDimensions = await _fixedAssetDimensions.GetAsync(
                    RecognitionProducer, lease.Id, lease.StartDate,
                    BuildRecognitionEvidenceLines(lease));
            }
            else if (lease.Status == LeaseStatus.Draft && settings?.LeaseRouAssetAccountId.HasValue == true
                && settings.LeaseLiabilityAccountId.HasValue)
            {
                result.RecognitionFinanceDimensions = await _fixedAssetDimensions.GetAsync(
                    RecognitionProducer, lease.Id, lease.StartDate,
                    BuildRecognitionLines(lease, settings));
            }
            await PopulateHistoricalPeriodDimensionEvidenceAsync(lease, result);
            return result;
        }

        private async Task PopulateHistoricalPeriodDimensionEvidenceAsync(
            LeaseContract lease,
            LeaseContractDetailDto result)
        {
            var scheduleIds = lease.ScheduleLines.Select(item => item.Id).ToArray();
            if (scheduleIds.Length == 0) return;
            var events = await _context.Set<FinancePostingEvent>().AsNoTracking()
                .Include(item => item.JournalEntry)
                .Where(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.SourceDocumentType == "LeasePeriodPosting" &&
                    scheduleIds.Contains(item.SourceDocumentId) && item.PostingAction == "Post" &&
                    item.PostingStatus == "Posted" && item.JournalEntryId.HasValue &&
                    item.JournalEntry != null && item.JournalEntry.TenantId == TenantId &&
                    item.JournalEntry.SourceDocumentType == "LeasePeriodPosting" &&
                    item.JournalEntry.SourceDocumentId == item.SourceDocumentId &&
                    item.JournalEntry.PostingStatus == "Posted" &&
                    item.JournalEntry.ReplicatedFromJournalEntryId == null &&
                    !item.JournalEntry.IsReversed && !item.JournalEntry.ReversalJournalEntryId.HasValue)
                .ToListAsync();
            foreach (var schedule in lease.ScheduleLines)
            {
                var exactEvents = events.Where(item => item.SourceDocumentId == schedule.Id).ToArray();
                if (exactEvents.Length != 1) continue;
                var postingEvent = exactEvents[0];
                var historicalLines = await _context.AccountTransactions.AsNoTracking()
                    .Where(item => item.TenantId == TenantId && !item.IsDeleted &&
                        item.JournalEntryId == postingEvent.JournalEntryId &&
                        item.SourceDocumentType == "LeasePeriodPosting" &&
                        item.SourceDocumentId == schedule.Id && item.PostingStatus == "Posted" &&
                        item.SourceDocumentLineId.HasValue)
                    .OrderBy(item => item.LineNumber)
                    .Select(item => new FinancePostingLineDto
                    {
                        AccountId = item.AccountId,
                        SourceDocumentLineId = item.SourceDocumentLineId,
                        DebitAmount = item.DebitAmount,
                        CreditAmount = item.CreditAmount,
                        LineNumber = item.LineNumber
                    })
                    .ToListAsync();
                if (historicalLines.Count == 0 || historicalLines.Any(item => !item.SourceDocumentLineId.HasValue))
                    continue;
                var scheduleDto = result.ScheduleLines.Single(item => item.Id == schedule.Id);
                scheduleDto.FinanceDimensions = await _fixedAssetDimensions.GetAsync(
                    HistoricalPeriodProducer, schedule.Id, schedule.PeriodDate, historicalLines);
            }
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
                .FirstOrDefaultAsync(l => l.TenantId == TenantId && l.Id == leaseId && !l.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Lease contract not found.");

            if (lease.Status is LeaseStatus.Active or LeaseStatus.Completed)
                return await GetLeaseByIdAsync(leaseId)
                    ?? throw new InvalidOperationException("Failed to retrieve activated lease.");
            if (lease.Status != LeaseStatus.Draft)
                throw new InvalidOperationException($"Cannot activate a lease in '{lease.Status}' status.");

            // Load finance settings for GL account defaults
            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && !s.IsDeleted, cancellationToken);

            if (settings?.LeaseRouAssetAccountId == null || settings?.LeaseLiabilityAccountId == null ||
                settings.LeaseInterestExpenseAccountId == null)
                throw new InvalidOperationException(
                    "Lease GL accounts not configured. Set ROU Asset, Lease Liability and Interest Expense accounts in Finance Settings.");

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                // ── Gap Fix: Actually create the ROU Fixed Asset ──
                // EzFMC's original code only posted the GL journal but never created the FixedAsset record.

                // ROU category is explicit source authority; never capitalize into an arbitrary
                // first category merely because configuration is incomplete.
                var rouCategory = await _context.FixedAssetCategories
                    .SingleOrDefaultAsync(c => c.TenantId == TenantId && !c.IsDeleted && c.Code == "ROU", cancellationToken)
                    ?? throw new InvalidOperationException(
                        "An active fixed asset category with exact code 'ROU' is required before lease activation.");
                var categoryId = rouCategory.Id;
                if (rouCategory.AssetAccountId != settings.LeaseRouAssetAccountId)
                    throw new InvalidOperationException(
                        "The ROU category asset account must match the configured lease ROU recognition account.");

                var books = await _context.AccountingBooks.AsNoTracking().Where(book =>
                        book.TenantId == TenantId && !book.IsDeleted && book.IsDefault && book.IsActive &&
                        book.AllowsPosting && book.BookType == AccountingBookType.PrimaryFull &&
                        book.LifecycleStatus == AccountingBookLifecycleStatus.Active)
                    .Take(2).ToListAsync(cancellationToken);
                if (books.Count != 1)
                    throw new InvalidOperationException(
                        "PRIMARY_BOOK_AUTHORITY_AMBIGUOUS: Exactly one active default PrimaryFull posting book is required.");
                var book = books[0];
                if (string.IsNullOrWhiteSpace(book.Code) ||
                    !string.Equals(book.Code, book.Code.Trim().ToUpperInvariant(), StringComparison.Ordinal))
                    throw new InvalidOperationException("The primary accounting book code is unavailable or noncanonical.");
                var currency = book.FunctionalCurrencyCode?.Trim().ToUpperInvariant();
                if (currency?.Length != 3 || !string.Equals(currency, settings.BaseCurrency?.Trim().ToUpperInvariant(), StringComparison.Ordinal) ||
                    !string.Equals(book.FunctionalCurrencyCode, currency, StringComparison.Ordinal))
                    throw new InvalidOperationException("The primary accounting book and tenant functional-currency authority do not agree.");
                var periodReady = await _context.FiscalPeriods.AsNoTracking().AnyAsync(item =>
                    item.TenantId == TenantId && !item.IsDeleted &&
                    item.StartDate <= lease.StartDate.Date && item.EndDate >= lease.StartDate.Date &&
                    item.PeriodStatus == "Open" && item.IsOpen && !item.IsLocked, cancellationToken);
                if (!periodReady)
                    throw new InvalidOperationException("The lease commencement date has no governed open tenant fiscal period.");

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
                lease.AccountingBookId = book.Id;
                lease.AccountingBookCode = book.Code;
                lease.FunctionalCurrencyCode = currency;
                lease.RouAssetAccountId = settings.LeaseRouAssetAccountId;
                lease.LeaseLiabilityAccountId = settings.LeaseLiabilityAccountId;
                lease.InterestExpenseAccountId = settings.LeaseInterestExpenseAccountId;
                lease.UpdatedAt = DateTime.UtcNow;
                lease.UpdatedBy = UserName;

                var postingLines = BuildRecognitionLines(lease, settings);
                await _fixedAssetDimensions.SynchronizeAsync(
                    RecognitionProducer, lease.Id, lease.StartDate, postingLines,
                    dto?.FinanceDimensions, inheritedAssetJournalBySourceLine: null,
                    "Lease recognition dimensions synchronized.", cancellationToken);
                await _fixedAssetDimensions.ValidateFreezeAndApplyAsync(
                    RecognitionProducer, lease.Id, lease.StartDate, postingLines, cancellationToken);
                var recognition = await _postingEngine.PostAsync(BuildLeasePostingRequest(
                    RecognitionProducer,
                    lease.Id,
                    lease.ContractNumber,
                    lease.StartDate,
                    $"Lease activation — ROU asset recognition: {lease.Description}",
                    $"LEASE-RECOGNITION:{TenantId:D}:{lease.Id:D}",
                    book.Code,
                    currency,
                    postingLines), RecognitionProducer, cancellationToken);
                lease.RecognitionPostingEventId = recognition.PostingEventId;
                lease.RecognitionJournalEntryId = recognition.JournalEntryId;

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

        // ── Prepare canonical AP payable ─────────────────────────────────

        public async Task<LeaseContractDetailDto> PreparePeriodPayableAsync(
            Guid leaseId,
            Guid scheduleLineId,
            CancellationToken cancellationToken = default)
        {
            await _vendorInvoices.CreateLeaseInstallmentDraftAsync(
                leaseId, scheduleLineId, cancellationToken);

            return await GetLeaseByIdAsync(leaseId)
                ?? throw new InvalidOperationException("Failed to retrieve the lease after preparing its AP draft.");
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

        private static List<FinancePostingLineDto> BuildRecognitionEvidenceLines(LeaseContract lease) =>
        [
            new FinancePostingLineDto
            {
                AccountId = lease.RouAssetAccountId!.Value,
                SourceDocumentLineId = FinanceSourceLineIdentity.Create(lease.Id, "ROU-ASSET", lease.Id),
                DebitAmount = lease.PresentValue,
                Description = $"ROU Asset — {lease.ContractNumber}",
                LineNumber = 1
            },
            new FinancePostingLineDto
            {
                AccountId = lease.LeaseLiabilityAccountId!.Value,
                SourceDocumentLineId = FinanceSourceLineIdentity.Create(lease.Id, "LEASE-LIABILITY", lease.Id),
                CreditAmount = lease.PresentValue,
                Description = $"Lease liability — {lease.ContractNumber}",
                LineNumber = 2
            }
        ];

        private FinancePostingRequestV2Dto BuildLeasePostingRequest(
            FinancePostingProducerContext producer,
            Guid sourceDocumentId,
            string reference,
            DateTime postingDate,
            string description,
            string idempotencyKey,
            string accountingBookCode,
            string functionalCurrency,
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
            AccountingBookCode = accountingBookCode,
            FunctionalCurrencyCode = functionalCurrency,
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
            ScheduleLines = l.ScheduleLines.OrderBy(s => s.PeriodNumber)
                .Select(s => MapScheduleLineDto(l, s)).ToList()
        };

        private static LeaseScheduleLineDto MapScheduleLineDto(
            LeaseContract lease, LeaseScheduleLine schedule)
        {
            var retained = schedule.VendorInvoices.Where(item => !item.IsDeleted).ToArray();
            var active = retained.Where(item => item.Status != VendorInvoiceStatus.Voided).ToArray();
            var terminals = retained.Where(item => retained.All(successor =>
                successor.ReplacesLeaseVendorInvoiceId != item.Id)).ToArray();
            var current = active.Length == 1
                ? active[0]
                : active.Length == 0 && terminals.Length == 1 ? terminals[0] : null;
            var lineageReady = IsReusableLeaseInvoiceLineage(retained);
            return new LeaseScheduleLineDto
            {
                Id = schedule.Id,
                PeriodNumber = schedule.PeriodNumber,
                PeriodDate = schedule.PeriodDate,
                PaymentAmount = schedule.PaymentAmount,
                InterestExpense = schedule.InterestExpense,
                PrincipalReduction = schedule.PrincipalReduction,
                RemainingLiability = schedule.RemainingLiability,
                IsPosted = schedule.IsPosted,
                VendorInvoiceId = current?.Id,
                VendorInvoiceNumber = current?.InvoiceNumber,
                VendorInvoiceStatus = current?.Status,
                VendorInvoiceApprovalStatus = current?.ApprovalStatus,
                VendorInvoiceJournalEntryId = current?.JournalEntryId,
                VendorInvoicePaidAmount = current?.PaidAmount,
                VendorInvoiceBalanceAmount = current?.BalanceAmount,
                CanPreparePayable = lease.Status == LeaseStatus.Active && !schedule.IsPosted && lineageReady &&
                    lease.ScheduleLines.Where(previous => previous.PeriodNumber < schedule.PeriodNumber)
                        .All(previous => previous.IsPosted)
            };
        }

        private static bool IsReusableLeaseInvoiceLineage(IReadOnlyCollection<VendorInvoice> retained)
        {
            if (retained.Count == 0) return true;
            if (retained.Any(item => item.Status != VendorInvoiceStatus.Voided)) return false;
            var byId = retained.ToDictionary(item => item.Id);
            if (retained.Any(item => item.ReplacesLeaseVendorInvoiceId.HasValue &&
                    !byId.ContainsKey(item.ReplacesLeaseVendorInvoiceId.Value)) ||
                retained.Where(item => item.ReplacesLeaseVendorInvoiceId.HasValue)
                    .GroupBy(item => item.ReplacesLeaseVendorInvoiceId!.Value).Any(group => group.Count() != 1))
                return false;
            var roots = retained.Where(item => !item.ReplacesLeaseVendorInvoiceId.HasValue).ToArray();
            if (roots.Length != 1) return false;
            var visited = new HashSet<Guid>();
            var terminal = roots[0];
            while (visited.Add(terminal.Id))
            {
                var successor = retained.SingleOrDefault(item => item.ReplacesLeaseVendorInvoiceId == terminal.Id);
                if (successor == null)
                {
                    var settlement = terminal.PaymentAllocations.Where(item => !item.IsDeleted).Sum(item =>
                        item.AllocatedAmount + item.DiscountAmount + item.WithholdingTaxAmount);
                    return visited.Count == retained.Count && Math.Abs(terminal.PaidAmount) <= 0.01m &&
                        Math.Abs(settlement) <= 0.01m;
                }
                terminal = successor;
            }
            return false;
        }
    }
}
