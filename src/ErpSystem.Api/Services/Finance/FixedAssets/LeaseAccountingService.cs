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
        private readonly IWorkflowService _workflowService;
        private const string LeaseWorkflowEntityType = "LeaseContract";
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
            IVendorInvoiceService vendorInvoices,
            IWorkflowService workflowService)
        {
            _context = context;
            _currentUser = currentUser;
            _fixedAssetService = fixedAssetService;
            _documentNumberingService = documentNumberingService;
            _postingEngine = postingEngine;
            _fixedAssetDimensions = fixedAssetDimensions;
            _vendorInvoices = vendorInvoices;
            _workflowService = workflowService;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
        private string UserName => _currentUser.UserName ?? "system";
        private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var userId) && userId != Guid.Empty
            ? userId
            : throw new UnauthorizedAccessException("An authenticated Finance user is required.");

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
                if (postingEvent.JournalEntry?.EntryDate.Date != schedule.PeriodDate.Date) continue;
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

        // ── Submit / complete governed activation ───────────────────────

        public async Task<LeaseContractDetailDto> ActivateLeaseAsync(
            Guid leaseId,
            ActivateLeaseDto? dto = null,
            CancellationToken cancellationToken = default)
        {
            var makerId = CurrentUserId;
            var lease = await _context.LeaseContracts
                .Include(l => l.Lessor)
                .FirstOrDefaultAsync(l => l.TenantId == TenantId && l.Id == leaseId && !l.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Lease contract not found.");

            if (lease.Status is LeaseStatus.Active or LeaseStatus.Completed)
                return await GetLeaseByIdAsync(leaseId)
                    ?? throw new InvalidOperationException("Failed to retrieve activated lease.");
            if (lease.Status == LeaseStatus.PendingApproval)
            {
                if (lease.ActivationWorkflowInstanceId.HasValue &&
                    await _workflowService.HasActiveApprovalInstanceAsync(LeaseWorkflowEntityType, lease.Id))
                    return await GetLeaseByIdAsync(leaseId)
                        ?? throw new InvalidOperationException("Failed to retrieve submitted lease.");
                throw new InvalidOperationException("The lease has pending status without one active approval instance. Finance remediation is required.");
            }
            if (lease.Status is not (LeaseStatus.Draft or LeaseStatus.Rejected))
                throw new InvalidOperationException($"Cannot submit a lease in '{lease.Status}' status for activation.");

            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && !s.IsDeleted, cancellationToken);

            if (settings?.LeaseRouAssetAccountId == null || settings?.LeaseLiabilityAccountId == null ||
                settings.LeaseInterestExpenseAccountId == null)
                throw new InvalidOperationException(
                    "Lease GL accounts not configured. Set ROU Asset, Lease Liability and Interest Expense accounts in Finance Settings.");
            if (!await _workflowService.HasActiveApprovalWorkflowAsync(LeaseWorkflowEntityType))
                throw new InvalidOperationException("A published LeaseContract approval workflow is required before activation can be submitted.");

            var (rouCategory, primaryBook, currency) = await ResolveRecognitionProposalAsync(
                lease.StartDate, settings, cancellationToken);
            var proposalLines = BuildRecognitionLines(lease, settings);

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                lease.RouAssetId = null;
                lease.RecognitionPostingEventId = null;
                lease.RecognitionJournalEntryId = null;
                lease.Status = LeaseStatus.PendingApproval;
                lease.AccountingBookId = primaryBook.Id;
                lease.AccountingBookCode = primaryBook.Code;
                lease.FunctionalCurrencyCode = currency;
                lease.RouAssetAccountId = settings.LeaseRouAssetAccountId;
                lease.LeaseLiabilityAccountId = settings.LeaseLiabilityAccountId;
                lease.InterestExpenseAccountId = settings.LeaseInterestExpenseAccountId;
                lease.ActivationWorkflowInstanceId = null;
                lease.ActivationSubmittedByUserId = makerId;
                lease.ActivationSubmittedAtUtc = DateTime.UtcNow;
                lease.ActivationApprovedByUserId = null;
                lease.ActivationApprovedAtUtc = null;
                lease.UpdatedAt = DateTime.UtcNow;
                lease.UpdatedBy = UserName;

                await _fixedAssetDimensions.SynchronizeAsync(
                    RecognitionProducer, lease.Id, lease.StartDate, proposalLines,
                    dto?.FinanceDimensions, inheritedAssetJournalBySourceLine: null,
                    "Lease recognition proposal submitted for approval.", cancellationToken);
                await _fixedAssetDimensions.ValidateFreezeAndApplyAsync(
                    RecognitionProducer, lease.Id, lease.StartDate, proposalLines, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                var workflow = await _workflowService.StartApprovalWorkflowAsync(LeaseWorkflowEntityType, lease.Id);
                if (!workflow.Success || !workflow.WorkflowInstanceId.HasValue ||
                    workflow.Status == WorkflowInstanceStatus.Completed)
                    throw new InvalidOperationException(workflow.Message ??
                        "Lease activation approval workflow could not be started as a pending independent review.");
                lease.ActivationWorkflowInstanceId = workflow.WorkflowInstanceId;
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
                ?? throw new InvalidOperationException("Failed to retrieve the submitted lease.");
        }

        public async Task<LeaseContractDetailDto> CompleteApprovedActivationAsync(
            Guid leaseId,
            Guid approvedByUserId,
            CancellationToken cancellationToken = default)
        {
            if (approvedByUserId == Guid.Empty || approvedByUserId != CurrentUserId)
                throw new UnauthorizedAccessException("The authenticated approver identity is required.");

            if (_context.Database.CurrentTransaction != null)
            {
                await CompleteApprovedActivationCoreAsync(leaseId, approvedByUserId, cancellationToken);
                return await GetLeaseByIdAsync(leaseId)
                    ?? throw new InvalidOperationException("Failed to retrieve the approved lease activation.");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await CompleteApprovedActivationCoreAsync(leaseId, approvedByUserId, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });

            return await GetLeaseByIdAsync(leaseId)
                ?? throw new InvalidOperationException("Failed to retrieve the approved lease activation.");
        }

        private async Task CompleteApprovedActivationCoreAsync(
            Guid leaseId,
            Guid approvedByUserId,
            CancellationToken cancellationToken)
        {
            var lease = await _context.LeaseContracts
                .Include(item => item.Lessor)
                .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == leaseId && !item.IsDeleted,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Lease contract not found.");
            if (lease.Status == LeaseStatus.Active && lease.RouAssetId.HasValue &&
                lease.ActivationApprovedByUserId == approvedByUserId)
                return;
            if (lease.Status != LeaseStatus.PendingApproval || !lease.ActivationWorkflowInstanceId.HasValue ||
                !lease.ActivationSubmittedByUserId.HasValue || !lease.ActivationSubmittedAtUtc.HasValue)
                throw new InvalidOperationException("The lease has no complete pending activation proposal evidence.");
            if (lease.ActivationSubmittedByUserId == approvedByUserId)
                throw new InvalidOperationException("LEASE_ACTIVATION_SOD: The lease maker cannot approve activation.");

            var workflow = await _context.WorkflowInstances
                .Include(item => item.EntityType)
                .Include(item => item.StepInstances)
                    .ThenInclude(item => item.Approvals)
                .SingleOrDefaultAsync(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.Id == lease.ActivationWorkflowInstanceId && item.EntityId == lease.Id,
                    cancellationToken)
                ?? throw new InvalidOperationException("The bound lease approval workflow evidence is unavailable.");
            if (!string.Equals(workflow.EntityType.Code, LeaseWorkflowEntityType, StringComparison.Ordinal) ||
                workflow.InitiatedById != lease.ActivationSubmittedByUserId ||
                workflow.Status != WorkflowInstanceStatus.Completed || !workflow.CompletedDate.HasValue ||
                !workflow.StepInstances.SelectMany(item => item.Approvals).Any(item =>
                    !item.IsDeleted && item.Status == WorkflowApprovalStatus.Approved &&
                    item.ProcessedById == approvedByUserId && item.ProcessedDate.HasValue))
                throw new InvalidOperationException("The lease activation workflow does not prove an independent completed approval.");

            var (rouCategory, primaryBook, representationBooks) = await ValidateFrozenRecognitionAuthorityAsync(
                lease, cancellationToken);
            var postingLines = BuildRecognitionEvidenceLines(lease);
            await _fixedAssetDimensions.ValidateFreezeAndApplyAsync(
                RecognitionProducer, lease.Id, lease.StartDate, postingLines, cancellationToken);
            var recognition = await _postingEngine.PostAsync(BuildLeasePostingRequest(
                RecognitionProducer,
                lease.Id,
                lease.ContractNumber,
                lease.StartDate,
                $"Lease activation — ROU asset recognition: {lease.Description}",
                $"LEASE-RECOGNITION:{TenantId:D}:{lease.Id:D}",
                primaryBook.Code,
                lease.FunctionalCurrencyCode!,
                postingLines), RecognitionProducer, cancellationToken);

            var journals = await _context.JournalEntries.AsNoTracking()
                .Where(item => item.TenantId == TenantId && !item.IsDeleted &&
                    (item.Id == recognition.JournalEntryId || item.ReplicatedFromJournalEntryId == recognition.JournalEntryId))
                .ToListAsync(cancellationToken);
            if (journals.Count != representationBooks.Count)
                throw new InvalidOperationException("Lease recognition did not create exactly one representation in every governed full book.");

            var journalByBook = journals.GroupBy(item => item.AccountingBookId).ToDictionary(item => item.Key, item => item.ToArray());
            if (representationBooks.Any(book => !journalByBook.TryGetValue(book.Id, out var values) || values.Length != 1) ||
                journalByBook.Keys.Except(representationBooks.Select(item => item.Id)).Any())
                throw new InvalidOperationException("Lease recognition book representations are missing, duplicated, or unexpected.");
            var journalIds = journals.Select(item => item.Id).ToArray();
            var postingEvents = await _context.Set<FinancePostingEvent>().AsNoTracking()
                .Where(item => item.TenantId == TenantId && !item.IsDeleted && item.JournalEntryId.HasValue &&
                    journalIds.Contains(item.JournalEntryId.Value))
                .ToListAsync(cancellationToken);
            if (postingEvents.Count != representationBooks.Count)
                throw new InvalidOperationException(
                    "Lease recognition representation posting-event evidence is missing or duplicated.");

            var rouSourceLineId = FinanceSourceLineIdentity.Create(lease.Id, "ROU-ASSET", lease.Id);
            var liabilitySourceLineId = FinanceSourceLineIdentity.Create(lease.Id, "LEASE-LIABILITY", lease.Id);
            var representationCosts = new Dictionary<Guid, decimal>();
            var postingEventByBook = new Dictionary<Guid, FinancePostingEvent>();
            foreach (var book in representationBooks)
            {
                var journal = journalByBook[book.Id].Single();
                var isPrimary = book.Id == primaryBook.Id;
                var journalEvents = postingEvents.Where(item => item.JournalEntryId == journal.Id).ToArray();
                if (journalEvents.Length != 1)
                    throw new InvalidOperationException(
                        "Lease recognition representation posting-event evidence is missing or duplicated.");
                var postingEvent = journalEvents[0];
                var bookCurrency = CanonicalBookCurrency(book);
                if (postingEvent.AccountingBookId != book.Id || postingEvent.BookClassification != book.Code ||
                    postingEvent.FunctionalCurrencyCode != bookCurrency ||
                    postingEvent.SourceDocumentType != RecognitionProducer.Definition.DocumentType ||
                    postingEvent.SourceDocumentId != lease.Id || postingEvent.PostingAction != "Post" ||
                    postingEvent.PostingStatus != "Posted" || postingEvent.PostingDate.Date != lease.StartDate.Date ||
                    isPrimary && (postingEvent.Id != recognition.PostingEventId ||
                        postingEvent.JournalEntryId != recognition.JournalEntryId))
                    throw new InvalidOperationException(
                        "A lease recognition representation posting event has inconsistent source, book, currency, or journal authority.");
                postingEventByBook[book.Id] = postingEvent;
                if (journal.EntryDate.Date != lease.StartDate.Date || journal.PostingStatus != "Posted" ||
                    journal.IsReversed || journal.ReversalJournalEntryId.HasValue ||
                    journal.SourceDocumentType != RecognitionProducer.Definition.DocumentType ||
                    journal.SourceDocumentId != lease.Id || journal.BookClassification != book.Code ||
                    (isPrimary ? journal.ReplicatedFromJournalEntryId.HasValue || journal.Id != recognition.JournalEntryId
                               : journal.ReplicatedFromJournalEntryId != recognition.JournalEntryId))
                    throw new InvalidOperationException("A lease recognition journal representation is inconsistent or not authoritative.");

                var lines = await _context.AccountTransactions.AsNoTracking()
                    .Where(item => item.TenantId == TenantId && !item.IsDeleted && item.JournalEntryId == journal.Id &&
                        item.SourceDocumentType == RecognitionProducer.Definition.DocumentType && item.SourceDocumentId == lease.Id &&
                        item.PostingStatus == "Posted" &&
                        (item.SourceDocumentLineId == rouSourceLineId || item.SourceDocumentLineId == liabilitySourceLineId))
                    .ToListAsync(cancellationToken);
                var rou = lines.Where(item => item.SourceDocumentLineId == rouSourceLineId &&
                    item.DebitAmount > 0m && item.CreditAmount == 0m).ToArray();
                var liability = lines.Where(item => item.SourceDocumentLineId == liabilitySourceLineId &&
                    item.CreditAmount > 0m && item.DebitAmount == 0m).ToArray();
                if (lines.Count != 2 || rou.Length != 1 || liability.Length != 1 ||
                    Math.Abs(rou[0].DebitAmount - liability[0].CreditAmount) > 0.01m ||
                    lines.Any(item => item.FunctionalCurrencyCode != bookCurrency) ||
                    (isPrimary && (rou[0].AccountId != lease.RouAssetAccountId ||
                        liability[0].AccountId != lease.LeaseLiabilityAccountId ||
                        Math.Abs(rou[0].DebitAmount - lease.PresentValue) > 0.01m)))
                    throw new InvalidOperationException("A lease recognition book representation has inconsistent source lines or amounts.");
                representationCosts[book.Id] = rou[0].DebitAmount;
            }
            var primaryPostingEvent = postingEventByBook[primaryBook.Id];

            var usefulLifeMonths = Math.Max(1, (int)Math.Ceiling((lease.EndDate.Date - lease.StartDate.Date).TotalDays / 30.44d));
            var asset = new FixedAsset
            {
                TenantId = TenantId,
                AssetCode = await _fixedAssetService.GenerateAssetCodeAsync(rouCategory.Id),
                Name = $"ROU Asset — {lease.ContractNumber}",
                Description = $"Right-of-Use asset for lease {lease.ContractNumber} ({lease.Lessor?.PartnerName ?? "N/A"})",
                FixedAssetCategoryId = rouCategory.Id,
                PurchaseDate = lease.StartDate,
                PlacedInServiceDate = lease.StartDate,
                CapitalizationDate = lease.StartDate,
                PurchasePrice = lease.PresentValue,
                AcquisitionCost = lease.PresentValue,
                NetBookValue = lease.PresentValue,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationConvention = DepreciationConvention.FullMonth,
                UsefulLifeMonths = usefulLifeMonths,
                ResidualValue = 0m,
                Status = FixedAssetStatus.Active,
                FunctionalCurrencyCode = lease.FunctionalCurrencyCode!,
                SourceDocumentType = RecognitionProducer.Definition.DocumentType,
                SourceDocumentId = lease.Id,
                SourceDocumentLineId = rouSourceLineId,
                JournalEntryId = recognition.JournalEntryId,
                PostingEventId = recognition.PostingEventId,
                CapitalizedAt = primaryPostingEvent.PostedAt ?? DateTime.UtcNow,
                CapitalizationApprovalWorkflowInstanceId = lease.ActivationWorkflowInstanceId,
                CapitalizationApprovalSubmittedByUserId = lease.ActivationSubmittedByUserId,
                CapitalizationApprovalSubmittedAt = lease.ActivationSubmittedAtUtc,
                CapitalizationApprovalApprovedByUserId = approvedByUserId,
                CapitalizationApprovalApprovedAt = workflow.CompletedDate,
                CreatedBy = UserName
            };
            foreach (var book in representationBooks)
            {
                var journal = journalByBook[book.Id].Single();
                var postingEvent = postingEventByBook[book.Id];
                var cost = representationCosts[book.Id];
                asset.BookValues.Add(new FixedAssetBookValue
                {
                    TenantId = TenantId,
                    AccountingBookId = book.Id,
                    BookClassification = book.Code,
                    AcquisitionCost = cost,
                    AccumulatedDepreciation = 0m,
                    NetBookValue = cost,
                    ResidualValue = 0m,
                    UsefulLifeMonths = usefulLifeMonths,
                    RemainingUsefulLifeMonths = usefulLifeMonths,
                    DepreciationMethod = DepreciationMethod.StraightLine,
                    DepreciationConvention = DepreciationConvention.FullMonth,
                    PlacedInServiceDate = lease.StartDate,
                    CapitalizationDate = lease.StartDate,
                    CapitalizationJournalEntryId = journal.Id,
                    CapitalizationPostingEventId = postingEvent.Id,
                    OpeningSource = "LeaseRecognition",
                    SourceDocumentType = RecognitionProducer.Definition.DocumentType,
                    SourceDocumentId = lease.Id,
                    SourceDocumentLineId = rouSourceLineId,
                    CreatedBy = UserName
                });
                asset.Transactions.Add(new AssetTransaction
                {
                    TenantId = TenantId,
                    AccountingBookId = book.Id,
                    BookClassification = book.Code,
                    TransactionDate = lease.StartDate,
                    TransactionType = "LeaseRecognition",
                    Description = $"Approved ROU recognition for lease {lease.ContractNumber}",
                    Amount = cost,
                    ResultingBookValue = cost,
                    RelatedEntityId = journal.Id,
                    PerformedByUserId = approvedByUserId,
                    CreatedBy = UserName
                });
            }

            _context.FixedAssets.Add(asset);
            lease.RouAssetId = asset.Id;
            lease.RecognitionPostingEventId = recognition.PostingEventId;
            lease.RecognitionJournalEntryId = recognition.JournalEntryId;
            lease.ActivationApprovedByUserId = approvedByUserId;
            lease.ActivationApprovedAtUtc = workflow.CompletedDate;
            lease.Status = LeaseStatus.Active;
            lease.UpdatedAt = DateTime.UtcNow;
            lease.UpdatedBy = UserName;
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<(FixedAssetCategory Category, AccountingBook PrimaryBook, string Currency)>
            ResolveRecognitionProposalAsync(
                DateTime commencementDate,
                FinanceSettings settings,
                CancellationToken cancellationToken)
        {
            var category = await _context.FixedAssetCategories.AsNoTracking()
                .SingleOrDefaultAsync(item => item.TenantId == TenantId && !item.IsDeleted && item.Code == "ROU",
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "An active fixed asset category with exact code 'ROU' is required before lease activation.");
            if (category.AssetAccountId != settings.LeaseRouAssetAccountId)
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
            var currency = CanonicalBookCurrency(book);
            if (!string.Equals(currency, settings.BaseCurrency?.Trim().ToUpperInvariant(), StringComparison.Ordinal))
                throw new InvalidOperationException("The primary accounting book and tenant functional-currency authority do not agree.");
            await ValidateRecognitionAccountsAsync(
                settings.LeaseRouAssetAccountId!.Value,
                settings.LeaseLiabilityAccountId!.Value,
                settings.LeaseInterestExpenseAccountId!.Value,
                cancellationToken);
            await ValidateOpenTenantPeriodAsync(commencementDate, cancellationToken);
            return (category, book, currency);
        }

        private async Task<(FixedAssetCategory Category, AccountingBook PrimaryBook, List<AccountingBook> RepresentationBooks)>
            ValidateFrozenRecognitionAuthorityAsync(LeaseContract lease, CancellationToken cancellationToken)
        {
            if (!lease.AccountingBookId.HasValue || string.IsNullOrWhiteSpace(lease.AccountingBookCode) ||
                string.IsNullOrWhiteSpace(lease.FunctionalCurrencyCode) || !lease.RouAssetAccountId.HasValue ||
                !lease.LeaseLiabilityAccountId.HasValue || !lease.InterestExpenseAccountId.HasValue)
                throw new InvalidOperationException("The approved lease recognition proposal is incomplete.");

            var primary = await _context.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(book =>
                book.TenantId == TenantId && !book.IsDeleted && book.Id == lease.AccountingBookId &&
                book.IsDefault && book.IsActive && book.AllowsPosting &&
                book.BookType == AccountingBookType.PrimaryFull &&
                book.LifecycleStatus == AccountingBookLifecycleStatus.Active,
                cancellationToken)
                ?? throw new InvalidOperationException("The approved primary-book authority is no longer eligible for posting.");
            var primaryCurrency = CanonicalBookCurrency(primary);
            if (primary.Code != lease.AccountingBookCode || primaryCurrency != lease.FunctionalCurrencyCode)
                throw new InvalidOperationException("The approved lease book code or currency no longer matches its authority.");

            var category = await _context.FixedAssetCategories.AsNoTracking()
                .SingleOrDefaultAsync(item => item.TenantId == TenantId && !item.IsDeleted && item.Code == "ROU",
                    cancellationToken)
                ?? throw new InvalidOperationException("The governed ROU asset category is unavailable.");
            if (category.AssetAccountId != lease.RouAssetAccountId)
                throw new InvalidOperationException("The governed ROU category no longer matches the approved recognition account.");
            await ValidateRecognitionAccountsAsync(
                lease.RouAssetAccountId.Value,
                lease.LeaseLiabilityAccountId.Value,
                lease.InterestExpenseAccountId.Value,
                cancellationToken);
            await ValidateOpenTenantPeriodAsync(lease.StartDate, cancellationToken);

            var parallelBooks = await _context.AccountingBooks.AsNoTracking().Where(book =>
                    book.TenantId == TenantId && !book.IsDeleted && book.IsActive && book.AllowsPosting &&
                    book.LifecycleStatus == AccountingBookLifecycleStatus.Active &&
                    book.BookType == AccountingBookType.ParallelFull && book.BaseAccountingBookId == primary.Id &&
                    book.ReplicationStartDate.HasValue && book.ReplicationStartDate.Value.Date <= lease.StartDate.Date)
                .OrderBy(book => book.Code)
                .ToListAsync(cancellationToken);
            foreach (var book in parallelBooks)
                _ = CanonicalBookCurrency(book);
            var representationBooks = new List<AccountingBook> { primary };
            representationBooks.AddRange(parallelBooks);
            return (category, primary, representationBooks);
        }

        private async Task ValidateRecognitionAccountsAsync(
            Guid rouAccountId,
            Guid liabilityAccountId,
            Guid interestAccountId,
            CancellationToken cancellationToken)
        {
            var ids = new[] { rouAccountId, liabilityAccountId, interestAccountId };
            var count = await _context.Accounts.AsNoTracking().CountAsync(item =>
                item.TenantId == TenantId && !item.IsDeleted && ids.Contains(item.Id), cancellationToken);
            if (count != ids.Distinct().Count())
                throw new InvalidOperationException("Every approved lease recognition account must be an active tenant account.");
        }

        private async Task ValidateOpenTenantPeriodAsync(DateTime date, CancellationToken cancellationToken)
        {
            var dateOnly = date.Date;
            var periodCount = await _context.FiscalPeriods.AsNoTracking().CountAsync(item =>
                item.TenantId == TenantId && !item.IsDeleted &&
                item.StartDate <= dateOnly && item.EndDate >= dateOnly &&
                item.PeriodStatus == "Open" && item.IsOpen && !item.IsLocked,
                cancellationToken);
            if (periodCount != 1)
                throw new InvalidOperationException("The lease commencement date must have exactly one governed open tenant fiscal period.");
        }

        private static string CanonicalBookCurrency(AccountingBook book)
        {
            if (string.IsNullOrWhiteSpace(book.Code) ||
                !string.Equals(book.Code, book.Code.Trim().ToUpperInvariant(), StringComparison.Ordinal))
                throw new InvalidOperationException("An accounting-book code is unavailable or noncanonical.");
            var currency = book.FunctionalCurrencyCode?.Trim().ToUpperInvariant();
            if (currency?.Length != 3 || !string.Equals(book.FunctionalCurrencyCode, currency, StringComparison.Ordinal))
                throw new InvalidOperationException($"Accounting book '{book.Code}' has no canonical functional currency.");
            return currency;
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
            ActivationWorkflowInstanceId = l.ActivationWorkflowInstanceId,
            ActivationSubmittedByUserId = l.ActivationSubmittedByUserId,
            ActivationSubmittedAtUtc = l.ActivationSubmittedAtUtc,
            ActivationApprovedByUserId = l.ActivationApprovedByUserId,
            ActivationApprovedAtUtc = l.ActivationApprovedAtUtc,
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
