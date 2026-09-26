using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;

namespace ErpSystem.Api.Services.Finance.Fiscal
{
    public class FiscalPeriodService : IFiscalPeriodService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<FiscalPeriodService> _logger;
        private readonly ISubledgerSettlementReadModelService _settlementReadModelService;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly FinanceCloseTemplateBaselineSeeder? _closeTemplateBaselineSeeder;
        private readonly IFileStorageService? _fileStorageService;

        public FiscalPeriodService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<FiscalPeriodService> logger,
            ISubledgerSettlementReadModelService settlementReadModelService,
            IFinanceAuditService? financeAuditService = null,
            FinanceCloseTemplateBaselineSeeder? closeTemplateBaselineSeeder = null,
            IFileStorageService? fileStorageService = null)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
            _settlementReadModelService = settlementReadModelService;
            _financeAuditService = financeAuditService;
            _closeTemplateBaselineSeeder = closeTemplateBaselineSeeder;
            _fileStorageService = fileStorageService;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";
        private Guid? CurrentUserId => Guid.TryParse(_currentUserService.UserId, out var id) ? id : null;

        // These definitions now seed the three TDC baseline templates. They remain in code only
        // as bootstrap policy; every live cycle copies an approved database template version.
        // Automated check codes remain a controlled catalogue so configuration extends the close
        // engine instead of introducing an arbitrary or parallel rules path.
        private static readonly IReadOnlySet<string> SupportedAutomatedCloseCheckCodes =
            FinanceCloseTemplateBaselineCatalog.Tasks
                .Where(item => item.IsAutomated && item.CheckCode != null)
                .Select(item => item.CheckCode!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // These checks protect basic ledger validity (and FIN-LIM-0034). An administrator may
        // customize wording, order and dependencies, but an approved template cannot downgrade
        // them to warnings. Recurring-journal failures are equally non-waivable here because the
        // recurring-journal model already supplies a controlled waiver outcome; downgrading the
        // close check would create an ungoverned second exception route. Depreciation applicability
        // remains governed by the audited tenant setting; when disabled the provider records
        // NotApplicable rather than a false pass.
        private static readonly IReadOnlySet<string> NonWaivableCloseCheckCodes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "POSTING_INTEGRITY",
                "TRIAL_BALANCE",
                "AP_CONTROL_RECONCILIATION",
                "AR_CONTROL_RECONCILIATION",
                "RECURRING_JOURNAL_EXCEPTIONS",
                "FIXED_ASSET_DEPRECIATION"
            };

        public async Task<IReadOnlyList<FiscalYearDto>> GetFiscalYearsAsync(CancellationToken cancellationToken = default)
        {
            var fiscalYears = await _unitOfWork.Repository<FiscalYear>()
                .GetQueryable(fy => fy.TenantId == TenantId)
                .OrderByDescending(fy => fy.StartDate)
                .ToListAsync(cancellationToken);

            return fiscalYears.Select(MapFiscalYearToDto).ToList();
        }

        public async Task<FiscalYearDto?> GetFiscalYearByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fiscalYear = await _unitOfWork.Repository<FiscalYear>()
                .FirstOrDefaultAsync(fy => fy.TenantId == TenantId && fy.Id == id);

            return fiscalYear == null ? null : MapFiscalYearToDto(fiscalYear);
        }

        public async Task<FiscalYearDto> CreateFiscalYearAsync(CreateFiscalYearDto dto, CancellationToken cancellationToken = default)
        {
            var startDate = dto.StartDate.Date;
            var endDate = dto.EndDate.Date;
            if (endDate < startDate)
                throw new InvalidOperationException("Fiscal year end date must be on or after its start date.");

            var overlapping = await _unitOfWork.Repository<FiscalYear>()
                .GetQueryable(fy => fy.TenantId == TenantId
                    && !fy.IsDeleted
                    && startDate <= fy.EndDate
                    && endDate >= fy.StartDate)
                .AnyAsync(cancellationToken);

            if (overlapping)
                throw new InvalidOperationException("Fiscal year dates overlap with an existing fiscal year.");

            var now = DateTime.UtcNow;
            var fiscalYear = new FiscalYear
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                FiscalYearCode = dto.FiscalYearCode,
                FiscalYearName = dto.FiscalYearName,
                Year = dto.Year,
                FiscalYearType = dto.FiscalYearType,
                StartDate = startDate,
                EndDate = endDate,
                TotalDays = (endDate - startDate).Days + 1,
                NumberOfPeriods = dto.NumberOfPeriods,
                Status = "Future",
                IsActive = true, // Default to true as property missing in DTO
                ReportingFramework = dto.ReportingFramework,
                BaseCurrency = dto.BaseCurrency,
                IsClosed = false,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = CurrentUserId
            };

            await _unitOfWork.Repository<FiscalYear>().AddAsync(fiscalYear);

            // Generate periods based on type
            var periodType = dto.PeriodType;
            var currentDate = startDate;
            int maxPeriods = dto.NumberOfPeriods;

            // Safety check for daily/weekly if user put wrong number
            if (periodType == PeriodType.Daily && maxPeriods < 360) maxPeriods = 365;
            if (periodType == PeriodType.Weekly && maxPeriods < 50) maxPeriods = 52;
            
            // Adjust based on leap year or actual year length
            // We'll iterate until EndDate is reached or maxPeriods
            
            int periodNumber = 1;

            while (currentDate <= endDate && periodNumber <= maxPeriods)
            {
                DateTime periodStart = currentDate;
                DateTime periodEnd;

                switch (periodType)
                {
                    case PeriodType.Quarterly:
                        // First day of next quarter minus 1 day
                        // Or just add 3 months and subtract 1 day from start
                        periodEnd = periodStart.AddMonths(3).AddDays(-1);
                        break;
                    
                    case PeriodType.Weekly:
                        periodEnd = periodStart.AddDays(6);
                        break;

                    case PeriodType.Daily:
                        periodEnd = periodStart;
                        break;

                    case PeriodType.Monthly:
                    default:
                        // First day of next month minus 1 day
                        // Handle case where start date isn't 1st of month (fiscal year starts mid-month?)
                        // Standard practice: Fiscal month ends on last day of month usually.
                        // Impl: Start + 1 month - 1 day might drift if starting Jan 15.
                        // Better: Get end of current month if starting 1st.
                        if (periodStart.Day == 1)
                        {
                            var daysInMonth = DateTime.DaysInMonth(periodStart.Year, periodStart.Month);
                            periodEnd = new DateTime(periodStart.Year, periodStart.Month, daysInMonth);
                        }
                        else
                        {
                            // "Fiscal Month" - just add 1 month relative to start
                            periodEnd = periodStart.AddMonths(1).AddDays(-1);
                        }
                        break;
                }

                // Cap at fiscal year end
                if (periodEnd > endDate)
                    periodEnd = endDate;

                // Create period
                var period = new FiscalPeriod
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    FiscalYearId = fiscalYear.Id,
                    PeriodCode = $"{dto.FiscalYearCode}-{periodNumber:D2}", 
                    // Name formatting based on type
                    PeriodName = GetPeriodName(periodType, periodStart, periodEnd, periodNumber),
                    PeriodNumber = periodNumber,
                    PeriodType = periodType,
                    StartDate = periodStart,
                    EndDate = periodEnd,
                    PeriodDays = (periodEnd - periodStart).Days + 1,
                    PeriodStatus = "Future",
                    IsOpen = false,
                    IsLocked = false,
                    CreatedAt = now,
                    CreatedBy = UserName,
                    CreatedById = CurrentUserId
                };

                await _unitOfWork.Repository<FiscalPeriod>().AddAsync(period);
                
                // Prepare for next iteration
                currentDate = periodEnd.AddDays(1);
                periodNumber++;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fiscal year {Code} created by {User}", fiscalYear.FiscalYearCode, UserName);

            return MapFiscalYearToDto(fiscalYear);
        }

        public async Task DeleteFiscalYearAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fiscalYear = await _unitOfWork.Repository<FiscalYear>()
                .GetQueryable(fy => fy.TenantId == TenantId && fy.Id == id)
                .Include(fy => fy.FiscalPeriods)
                .FirstOrDefaultAsync(cancellationToken);

            if (fiscalYear == null)
                throw new ArgumentException($"Fiscal year with ID {id} not found");

            if (fiscalYear.IsClosed)
                throw new InvalidOperationException("Cannot delete a closed fiscal year.");

            var periodIds = fiscalYear.FiscalPeriods.Select(p => p.Id).ToList();

            var dependencies = await GetFiscalYearDeletionDependenciesAsync(
                fiscalYear.Id,
                periodIds,
                cancellationToken);

            if (dependencies.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot delete fiscal year because it has associated records: {string.Join(", ", dependencies)}.");
            }

            // Fiscal years and periods are soft-deleted. Mark the generated child periods too,
            // otherwise they remain queryable after their parent year disappears.
            var moduleLocks = periodIds.Count == 0
                ? new List<PeriodModuleLock>()
                : await _unitOfWork.Repository<PeriodModuleLock>()
                    .GetQueryable(l => l.TenantId == TenantId && periodIds.Contains(l.FiscalPeriodId))
                    .ToListAsync(cancellationToken);

            if (moduleLocks.Count > 0)
                await _unitOfWork.Repository<PeriodModuleLock>().DeleteRangeAsync(moduleLocks);

            if (fiscalYear.FiscalPeriods.Count > 0)
                await _unitOfWork.Repository<FiscalPeriod>().DeleteRangeAsync(fiscalYear.FiscalPeriods);

            await _unitOfWork.Repository<FiscalYear>().DeleteAsync(fiscalYear);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fiscal year {Code} deleted by {User}", fiscalYear.FiscalYearCode, UserName);
        }

        private async Task<List<string>> GetFiscalYearDeletionDependenciesAsync(
            Guid fiscalYearId,
            IReadOnlyCollection<Guid> periodIds,
            CancellationToken cancellationToken)
        {
            var dependencies = new List<string>();

            if (await _unitOfWork.Repository<FiscalYear>()
                .GetQueryable(fy => fy.TenantId == TenantId && fy.NextFiscalYearId == fiscalYearId)
                .AnyAsync(cancellationToken))
                dependencies.Add("linked fiscal years");

            if (await _unitOfWork.Repository<BudgetScenario>()
                .GetQueryable(b => b.TenantId == TenantId && b.FiscalYearId == fiscalYearId)
                .AnyAsync(cancellationToken))
                dependencies.Add("budget scenarios");

            if (periodIds.Count == 0)
                return dependencies;

            if (await _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(e => e.TenantId == TenantId && periodIds.Contains(e.FiscalPeriodId))
                .AnyAsync(cancellationToken))
                dependencies.Add("journal entries");

            // Check transaction lines independently. This protects against legacy/orphaned
            // lines even when their journal header is missing or soft-deleted.
            if (await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.TenantId == TenantId && periodIds.Contains(t.FiscalPeriodId))
                .AnyAsync(cancellationToken))
                dependencies.Add("account transactions");

            if (await _unitOfWork.Repository<BudgetEntry>()
                .GetQueryable(b => b.TenantId == TenantId && periodIds.Contains(b.FiscalPeriodId))
                .AnyAsync(cancellationToken))
                dependencies.Add("budget entries");

            if (await _unitOfWork.Repository<FxRevaluationBatch>()
                .GetQueryable(b => b.TenantId == TenantId && periodIds.Contains(b.FiscalPeriodId))
                .AnyAsync(cancellationToken))
                dependencies.Add("foreign-currency revaluation batches");

            if (await _unitOfWork.Repository<OpeningBalanceBatch>()
                .GetQueryable(b => b.TenantId == TenantId && periodIds.Contains(b.FiscalPeriodId))
                .AnyAsync(cancellationToken))
                dependencies.Add("opening-balance batches");

            if (await _unitOfWork.Repository<AssetDepreciationSchedule>()
                .GetQueryable(s => s.TenantId == TenantId && periodIds.Contains(s.FiscalPeriodId))
                .AnyAsync(cancellationToken))
                dependencies.Add("asset depreciation schedules");

            if (await _unitOfWork.Repository<FixedAssetDepreciationRun>()
                .GetQueryable(r => r.TenantId == TenantId && periodIds.Contains(r.FiscalPeriodId))
                .AnyAsync(cancellationToken))
                dependencies.Add("asset depreciation runs");

            if (await _unitOfWork.Repository<AssetDisposal>()
                .GetQueryable(d => d.TenantId == TenantId
                    && d.FiscalPeriodId.HasValue
                    && periodIds.Contains(d.FiscalPeriodId.Value))
                .AnyAsync(cancellationToken))
                dependencies.Add("asset disposals");

            if (await _unitOfWork.Repository<AssetTransfer>()
                .GetQueryable(t => t.TenantId == TenantId
                    && t.FiscalPeriodId.HasValue
                    && periodIds.Contains(t.FiscalPeriodId.Value))
                .AnyAsync(cancellationToken))
                dependencies.Add("asset transfers");

            if (await _unitOfWork.Repository<AssetValuation>()
                .GetQueryable(v => v.TenantId == TenantId
                    && v.FiscalPeriodId.HasValue
                    && periodIds.Contains(v.FiscalPeriodId.Value))
                .AnyAsync(cancellationToken))
                dependencies.Add("asset valuations");

            return dependencies;
        }

        public async Task<FiscalYearDto> UpdateFiscalYearAsync(Guid id, UpdateFiscalYearDto dto, CancellationToken cancellationToken = default)
        {
            var fiscalYear = await _unitOfWork.Repository<FiscalYear>()
                .GetQueryable(fy => fy.TenantId == TenantId && fy.Id == id)
                .FirstOrDefaultAsync(cancellationToken);

            if (fiscalYear == null)
                throw new ArgumentException($"Fiscal year with ID {id} not found");

            if (fiscalYear.IsClosed)
                throw new InvalidOperationException("Cannot update a closed fiscal year. Reopen it first.");

            if (!string.IsNullOrWhiteSpace(dto.FiscalYearName))
                fiscalYear.FiscalYearName = dto.FiscalYearName.Trim();

            if (dto.Notes != null)
                fiscalYear.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

            fiscalYear.UpdatedAt = DateTime.UtcNow;
            fiscalYear.UpdatedBy = UserName;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fiscal year {Code} updated by {User}", fiscalYear.FiscalYearCode, UserName);
            return MapFiscalYearToDto(fiscalYear);
        }

        public async Task<IReadOnlyList<FiscalPeriodDto>> GetFiscalPeriodsAsync(
            Guid? fiscalYearId = null,
            string? status = null,
            CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId);

            if (fiscalYearId.HasValue)
                query = query.Where(fp => fp.FiscalYearId == fiscalYearId.Value);

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedStatus = status.Trim();
                query = query.Where(fp => fp.PeriodStatus == normalizedStatus);
            }

            var periods = await query
                .Include(fp => fp.PeriodModuleLocks)
                    .ThenInclude(moduleLock => moduleLock.ModuleDefinition)
                .OrderBy(fp => fp.StartDate)
                .ToListAsync(cancellationToken);

            var periodIds = periods.Select(period => period.Id).ToList();
            var closePackCycles = await _unitOfWork.Repository<FinanceCloseCycle>()
                .GetQueryable(cycle => cycle.TenantId == TenantId &&
                    periodIds.Contains(cycle.FiscalPeriodId) &&
                    (cycle.Status == FinanceCloseStatuses.Closed ||
                     cycle.Status == FinanceCloseStatuses.Reopened))
                .Select(cycle => new { cycle.FiscalPeriodId, cycle.Id, cycle.CycleNumber })
                .ToListAsync(cancellationToken);
            var latestClosePackByPeriod = closePackCycles
                .GroupBy(cycle => cycle.FiscalPeriodId)
                .ToDictionary(
                    group => group.Key,
                    group => (Guid?)group.OrderByDescending(cycle => cycle.CycleNumber).First().Id);
            var reopenRequests = await _unitOfWork.Repository<FinancePeriodReopenRequest>()
                .GetQueryable(request => request.TenantId == TenantId &&
                    periodIds.Contains(request.FiscalPeriodId))
                .Include(request => request.FinanceCloseCycle)
                .OrderByDescending(request => request.RequestedAt)
                .ToListAsync(cancellationToken);
            var latestReopenByPeriod = reopenRequests
                .GroupBy(request => request.FiscalPeriodId)
                .ToDictionary(
                    group => group.Key,
                    group => MapFinancePeriodReopenRequestToDto(group.First()));

            // The cycle ID is a read-model convenience only. The document builder independently
            // validates tenant ownership and a complete maker-checker certificate before rendering.
            return periods
                .Select(period => MapFiscalPeriodToDto(
                    period,
                    latestClosePackByPeriod.GetValueOrDefault(period.Id),
                    latestReopenByPeriod.GetValueOrDefault(period.Id)))
                .ToList();
        }

        public async Task<FiscalPeriodDto?> GetFiscalPeriodByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId && fp.Id == id)
                .Include(fp => fp.PeriodModuleLocks)
                    .ThenInclude(moduleLock => moduleLock.ModuleDefinition)
                .FirstOrDefaultAsync(cancellationToken);

            if (period == null)
                return null;

            var latestClosePackCycleId = await _unitOfWork.Repository<FinanceCloseCycle>()
                .GetQueryable(cycle => cycle.TenantId == TenantId &&
                    cycle.FiscalPeriodId == period.Id &&
                    (cycle.Status == FinanceCloseStatuses.Closed ||
                     cycle.Status == FinanceCloseStatuses.Reopened))
                .OrderByDescending(cycle => cycle.CycleNumber)
                .Select(cycle => (Guid?)cycle.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var latestReopenRequest = await _unitOfWork.Repository<FinancePeriodReopenRequest>()
                .GetQueryable(request => request.TenantId == TenantId && request.FiscalPeriodId == period.Id)
                .Include(request => request.FinanceCloseCycle)
                .OrderByDescending(request => request.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return MapFiscalPeriodToDto(
                period,
                latestClosePackCycleId,
                latestReopenRequest == null ? null : MapFinancePeriodReopenRequestToDto(latestReopenRequest));
        }

        public async Task<FiscalPeriodDto?> GetPeriodForDateAsync(DateTime transactionDate, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId
                    && fp.StartDate <= transactionDate.Date
                    && fp.EndDate >= transactionDate.Date)
                .Include(fp => fp.PeriodModuleLocks)
                    .ThenInclude(moduleLock => moduleLock.ModuleDefinition)
                .FirstOrDefaultAsync(cancellationToken);

            return period == null ? null : MapFiscalPeriodToDto(period);
        }

        public Task<FiscalPeriodDto> OpenPeriodAsync(
            PeriodOpenRequestDto request,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                request.FiscalPeriodId,
                () => OpenPeriodCoreAsync(request, cancellationToken),
                cancellationToken);

        public async Task<FiscalPeriodDto> UpdatePostingDatePolicyAsync(
            Guid periodId,
            PeriodPostingDatePolicyRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length < 10)
                throw new InvalidOperationException("A reason of at least 10 characters is required to change the posting-date policy.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(candidate => candidate.TenantId == TenantId
                    && candidate.Id == periodId
                    && !candidate.IsDeleted);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");
            if (period.IsClosed || period.IsLocked || period.IsCloseInitiated)
                throw new InvalidOperationException("Posting-date policy cannot be changed while a period is closing, closed, or locked.");
            if (period.AllowFutureDating == request.AllowFutureDating)
                return MapFiscalPeriodToDto(period);

            var beforePolicyChange = BuildPeriodAuditSnapshot(period);
            period.AllowFutureDating = request.AllowFutureDating;
            period.UpdatedAt = DateTime.UtcNow;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodPostingDatePolicyUpdated,
                period,
                beforeValues: beforePolicyChange,
                afterValues: BuildPeriodAuditSnapshot(period),
                reason: reason,
                comment: request.AllowFutureDating
                    ? "Future-dated Finance posting enabled for this period."
                    : "Future-dated Finance posting disabled for this period.",
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Fiscal period {Code} future-dating policy set to {AllowFutureDating} by {User}. Reason: {Reason}",
                period.PeriodCode,
                period.AllowFutureDating,
                UserName,
                reason);

            return MapFiscalPeriodToDto(period);
        }

        private async Task<FiscalPeriodDto> OpenPeriodCoreAsync(
            PeriodOpenRequestDto request,
            CancellationToken cancellationToken)
        {
            var reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length < 10)
                throw new InvalidOperationException("The period opening reason must contain at least 10 characters.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == request.FiscalPeriodId)
                .Include(item => item.FiscalYear)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException($"Fiscal period with Id '{request.FiscalPeriodId}' not found.");

            if (period.IsOpen || string.Equals(period.PeriodStatus, "Open", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Period is already open.");
            if (period.IsClosed || string.Equals(period.PeriodStatus, "Closed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A closed period must use the controlled reopen request and approval workflow.");
            if (period.IsLocked || string.Equals(period.PeriodStatus, "Locked", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A locked period cannot be opened.");
            if (!string.Equals(period.PeriodStatus, "Future", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Only Future periods can be opened; this period is '{period.PeriodStatus}'.");

            var fiscalYear = period.FiscalYear;
            if (fiscalYear.IsClosed || fiscalYear.IsLocked ||
                string.Equals(fiscalYear.Status, "Closed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fiscalYear.Status, "Locked", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fiscalYear.Status, "Archived", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A period cannot be opened inside a closed, locked, or archived fiscal year.");
            }

            // Multiple adjacent periods may remain open while Finance completes the prior close.
            // We still prohibit chronological gaps: every earlier period must first leave Future
            // status, either by being opened or by completing its own controlled lifecycle.
            var firstUnopenedEarlierPeriod = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.FiscalYearId == period.FiscalYearId &&
                    item.StartDate < period.StartDate &&
                    item.PeriodStatus == "Future")
                .OrderBy(item => item.StartDate)
                .Select(item => new { item.PeriodCode, item.PeriodName })
                .FirstOrDefaultAsync(cancellationToken);
            if (firstUnopenedEarlierPeriod != null)
            {
                throw new InvalidOperationException(
                    $"Open earlier period '{firstUnopenedEarlierPeriod.PeriodCode} - {firstUnopenedEarlierPeriod.PeriodName}' before opening {period.PeriodCode}.");
            }

            var beforeOpen = BuildPeriodAuditSnapshot(period);
            var now = DateTime.UtcNow;
            period.PeriodStatus = "Open";
            period.Status = "Open";
            period.IsOpen = true;
            period.IsClosed = false;
            period.UpdatedAt = now;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            // A fiscal year may be provisioned in Future state with every period unopened. Opening
            // its first period activates the year; subsequent adjacent opens leave it active.
            if (string.Equals(fiscalYear.Status, "Future", StringComparison.OrdinalIgnoreCase))
                fiscalYear.Status = "Open";
            fiscalYear.IsActive = true;
            fiscalYear.UpdatedAt = now;
            fiscalYear.UpdatedBy = UserName;
            fiscalYear.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
            await _unitOfWork.Repository<FiscalYear>().UpdateAsync(fiscalYear);
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodOpened,
                period,
                beforeValues: beforeOpen,
                afterValues: BuildPeriodAuditSnapshot(period),
                reason: reason,
                comment: "Future accounting period opened for posting.",
                context: new { fiscalYear.FiscalYearCode, AllowsConcurrentAdjacentPeriods = true },
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Fiscal period {Code} opened by {User}. Reason: {Reason}",
                period.PeriodCode,
                UserName,
                reason);
            return MapFiscalPeriodToDto(period);
        }

        public Task<FinanceCloseWorkspaceDto> EvaluatePeriodCloseWorkspaceAsync(
            Guid periodId,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                periodId,
                () => EvaluatePeriodCloseWorkspaceCoreAsync(periodId, cancellationToken),
                cancellationToken);

        private async Task<FinanceCloseWorkspaceDto> EvaluatePeriodCloseWorkspaceCoreAsync(
            Guid periodId,
            CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == periodId)
                ?? throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");

            if (period.IsLocked || period.IsClosed || !period.IsOpen ||
                !string.Equals(period.PeriodStatus, "Open", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only an open, unlocked fiscal period can be evaluated for close.");
            }

            var cycle = await GetOrCreateActiveCloseCycleAsync(period, cancellationToken);
            var validation = await ValidatePeriodCloseAsync(period.Id, cancellationToken);
            var results = await BuildStructuredCloseChecksAsync(period, validation, cancellationToken);
            var now = DateTime.UtcNow;
            var evaluationNumber = cycle.EvaluationCount + 1;
            var effectiveResults = new List<CloseCheckResult>(results.Count);
            var approvedWaivers = await _unitOfWork.Repository<FinanceCloseExceptionWaiver>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.FinanceCloseCycleId == cycle.Id &&
                    item.Status == FinanceCloseWaiverStatuses.Approved &&
                    !item.IsDeleted)
                .ToListAsync(cancellationToken);
            var appliedWaiverIds = new List<Guid>();

            foreach (var providerResult in results)
            {
                var task = cycle.Tasks.FirstOrDefault(item => item.CheckCode == providerResult.CheckCode);
                if (task == null)
                {
                    // A cycle is governed by the immutable template copied when it started. New
                    // providers introduced in a later system baseline must not be retrofitted into
                    // that historical cycle; they begin with the next cycle/template version.
                    continue;
                }
                // The approved template owns close severity. This mapping works in both
                // directions: an optional task softens a provider failure, while a tenant that
                // deliberately makes a review-warning task mandatory turns an exception warning
                // into a blocker. Passed and NotApplicable measurements retain their status.
                var result = task.IsMandatory
                    ? providerResult with
                    {
                        Severity = FinanceCloseCheckSeverities.Mandatory,
                        Status = providerResult.Status == FinanceCloseCheckStatuses.Warning
                            ? FinanceCloseCheckStatuses.Failed
                            : providerResult.Status,
                        ResultSummary = providerResult.Status == FinanceCloseCheckStatuses.Warning
                            ? $"Approved template mandatory control: {providerResult.ResultSummary}"
                            : providerResult.ResultSummary
                    }
                    : providerResult with
                    {
                        Severity = FinanceCloseCheckSeverities.Warning,
                        Status = providerResult.Status == FinanceCloseCheckStatuses.Failed
                            ? FinanceCloseCheckStatuses.Warning
                            : providerResult.Status,
                        ResultSummary = providerResult.Status == FinanceCloseCheckStatuses.Failed
                            ? $"Approved template warning: {providerResult.ResultSummary}"
                            : providerResult.ResultSummary
                    };
                // Fingerprint the provider/template result before changing its effective status.
                // This prevents an approval for one exception state from silently accepting a
                // later state that happens to use the same check code.
                var evidenceJson = JsonSerializer.Serialize(result.Evidence);
                var evidenceFingerprint = ComputeCloseEvidenceFingerprint(result, evidenceJson);
                FinanceCloseExceptionWaiver? appliedWaiver = null;
                if (result.Status is FinanceCloseCheckStatuses.Failed or FinanceCloseCheckStatuses.Warning &&
                    !NonWaivableCloseCheckCodes.Contains(result.CheckCode))
                {
                    appliedWaiver = approvedWaivers
                        .Where(item => item.CheckCode.Equals(result.CheckCode, StringComparison.OrdinalIgnoreCase) &&
                            item.EvidenceFingerprint == evidenceFingerprint)
                        .OrderByDescending(item => item.ReviewedAt)
                        .FirstOrDefault();
                    if (appliedWaiver != null)
                    {
                        result = result with
                        {
                            Status = FinanceCloseCheckStatuses.Waived,
                            ResultSummary = $"Approved waiver {appliedWaiver.Id}: {result.ResultSummary}"
                        };
                        appliedWaiverIds.Add(appliedWaiver.Id);
                    }
                }

                effectiveResults.Add(result);

                // Appending one row per check per evaluation provides a before/after trail when a
                // close blocker is corrected. Never update earlier snapshots.
                await _unitOfWork.Repository<FinanceCloseCheckSnapshot>().AddAsync(new FinanceCloseCheckSnapshot
                {
                    TenantId = TenantId,
                    FinanceCloseCycleId = cycle.Id,
                    EvaluationNumber = evaluationNumber,
                    CheckCode = result.CheckCode,
                    Title = result.Title,
                    Category = result.Category,
                    Severity = result.Severity,
                    Status = result.Status,
                    ResultSummary = result.ResultSummary,
                    ExceptionCount = result.ExceptionCount,
                    ExceptionAmount = result.ExceptionAmount,
                    EvidenceJson = evidenceJson,
                    EvidenceFingerprint = evidenceFingerprint,
                    AppliedWaiverId = appliedWaiver?.Id,
                    EvaluatedAt = now,
                    EvaluatedByUserId = CurrentUserId,
                    EvaluatedByUserName = UserName,
                    CreatedBy = UserName,
                    CreatedById = CurrentUserId
                });

                var passed = result.Status is FinanceCloseCheckStatuses.Passed or FinanceCloseCheckStatuses.NotApplicable or FinanceCloseCheckStatuses.Waived
                    || (result.Severity == FinanceCloseCheckSeverities.Warning && result.Status == FinanceCloseCheckStatuses.Warning);
                task.Status = passed ? FinanceCloseTaskStatuses.Completed : FinanceCloseTaskStatuses.Blocked;
                task.CompletedAt = passed ? now : null;
                task.CompletedByUserId = passed ? CurrentUserId : null;
                task.CompletedByUserName = passed ? UserName : null;
                task.EvidenceSummary = $"Evaluation {evaluationNumber}: {result.ResultSummary}";
                task.UpdatedAt = now;
                task.UpdatedBy = UserName;
                task.LastModifiedById = CurrentUserId;
            }

            // Subsequent blocker, audit and certification decisions must use the approved
            // template severity, not the provider's baseline severity.
            results = effectiveResults;

            cycle.EvaluationCount = evaluationNumber;
            cycle.LastEvaluatedAt = now;
            cycle.UpdatedAt = now;
            cycle.UpdatedBy = UserName;
            cycle.LastModifiedById = CurrentUserId;

            var hasMandatoryBlocker = results.Any(result =>
                result.Severity == FinanceCloseCheckSeverities.Mandatory &&
                result.Status == FinanceCloseCheckStatuses.Failed);

            if (hasMandatoryBlocker && cycle.Status == FinanceCloseStatuses.Prepared)
            {
                // A preparation signature is only valid for a passing evidence set. If a later
                // just-in-time evaluation finds a blocker, supersede it and require a fresh maker
                // declaration after the exception is fixed.
                var certification = await _unitOfWork.Repository<FinanceCloseCertification>()
                    .FirstOrDefaultAsync(item => item.TenantId == TenantId &&
                        item.FinanceCloseCycleId == cycle.Id && !item.IsSuperseded);
                if (certification != null)
                {
                    certification.IsSuperseded = true;
                    certification.SupersededAt = now;
                    certification.SupersededReason = "A later close evaluation found a mandatory blocker.";
                    certification.UpdatedAt = now;
                    certification.UpdatedBy = UserName;
                    certification.LastModifiedById = CurrentUserId;
                }

                var preparationTask = cycle.Tasks.First(item => item.TaskCode == "PREPARER_CERTIFICATION");
                preparationTask.Status = FinanceCloseTaskStatuses.Pending;
                preparationTask.CompletedAt = null;
                preparationTask.CompletedByUserId = null;
                preparationTask.CompletedByUserName = null;
                preparationTask.EvidenceSummary = "A fresh declaration is required after close blockers are cleared.";
                cycle.Status = FinanceCloseStatuses.InProgress;
                cycle.PreparedAt = null;
            }

            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodCloseCycleEvaluated,
                period,
                afterValues: new { cycle.Id, cycle.CycleNumber, EvaluationNumber = evaluationNumber, Results = results },
                comment: hasMandatoryBlocker ? "Close evaluation recorded mandatory blockers." : "Close evaluation passed mandatory checks.",
                cancellationToken: cancellationToken);
            if (appliedWaiverIds.Count > 0)
            {
                await RecordPeriodAuditAsync(
                    FinanceAuditEvents.AccountingPeriodCloseWaiverApplied,
                    period,
                    afterValues: new
                    {
                        cycle.Id,
                        EvaluationNumber = evaluationNumber,
                        WaiverIds = appliedWaiverIds.Distinct().ToList()
                    },
                    comment: "Approved exception waivers matched the unchanged check evidence and were applied.",
                    cancellationToken: cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await LoadCloseWorkspaceDtoAsync(cycle.Id, cancellationToken);
        }

        public Task<FinanceCloseWorkspaceDto> PreparePeriodCloseAsync(
            Guid periodId,
            PeriodClosePreparationRequestDto request,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                periodId,
                () => PreparePeriodCloseCoreAsync(periodId, request, cancellationToken),
                cancellationToken);

        private async Task<FinanceCloseWorkspaceDto> PreparePeriodCloseCoreAsync(
            Guid periodId,
            PeriodClosePreparationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var declaration = request.Declaration?.Trim() ?? string.Empty;
            if (declaration.Length < 20)
                throw new InvalidOperationException("The close preparation declaration must contain at least 20 characters.");

            var workspace = await EvaluatePeriodCloseWorkspaceCoreAsync(periodId, cancellationToken);
            if (workspace.MandatoryBlockerCount > 0)
                throw new InvalidOperationException("Resolve every mandatory close blocker before signing the preparation declaration.");

            if (!CurrentUserId.HasValue)
                throw new InvalidOperationException("A resolved user identity is required to certify a Finance close.");

            var cycle = await _unitOfWork.Repository<FinanceCloseCycle>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == workspace.CycleId)
                .Include(item => item.Tasks)
                .FirstAsync(cancellationToken);
            var existing = await _unitOfWork.Repository<FinanceCloseCertification>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId &&
                    item.FinanceCloseCycleId == cycle.Id && !item.IsSuperseded);
            if (existing?.PreparedAt != null)
                throw new InvalidOperationException("This close cycle already has an active preparation certificate.");

            var now = DateTime.UtcNow;
            var certification = new FinanceCloseCertification
            {
                TenantId = TenantId,
                FinanceCloseCycleId = cycle.Id,
                PreparedByUserId = CurrentUserId,
                PreparedByUserName = UserName,
                PreparedAt = now,
                PreparerDeclaration = declaration,
                CreatedBy = UserName,
                CreatedById = CurrentUserId
            };
            await _unitOfWork.Repository<FinanceCloseCertification>().AddAsync(certification);

            var preparationTask = cycle.Tasks.First(item => item.TaskCode == "PREPARER_CERTIFICATION");
            preparationTask.Status = FinanceCloseTaskStatuses.Completed;
            preparationTask.CompletedAt = now;
            preparationTask.CompletedByUserId = CurrentUserId;
            preparationTask.CompletedByUserName = UserName;
            preparationTask.EvidenceSummary = $"Signed preparation certificate {certification.Id}.";
            preparationTask.UpdatedAt = now;
            preparationTask.UpdatedBy = UserName;
            preparationTask.LastModifiedById = CurrentUserId;
            cycle.Status = FinanceCloseStatuses.Prepared;
            cycle.PreparedAt = now;
            cycle.UpdatedAt = now;
            cycle.UpdatedBy = UserName;
            cycle.LastModifiedById = CurrentUserId;

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == periodId)
                ?? throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");
            period.IsCloseInitiated = true;
            period.CloseInitiatedDate ??= cycle.StartedAt;
            period.CloseInitiatedByUserId ??= cycle.StartedByUserId;

            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodClosePrepared,
                period,
                afterValues: new
                {
                    CloseCycleId = cycle.Id,
                    cycle.CycleNumber,
                    CertificationId = certification.Id,
                    certification.PreparedAt
                },
                comment: declaration,
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await LoadCloseWorkspaceDtoAsync(cycle.Id, cancellationToken);
        }

        public Task<IReadOnlyList<FinanceCloseTemplateDto>> GetFinanceCloseTemplatesAsync(
            CancellationToken cancellationToken = default)
            => ExecuteCloseTemplateControlAsync(
                () => GetFinanceCloseTemplatesCoreAsync(cancellationToken),
                cancellationToken);

        private async Task<IReadOnlyList<FinanceCloseTemplateDto>> GetFinanceCloseTemplatesCoreAsync(
            CancellationToken cancellationToken)
        {
            // Default creation is a write even though the user requested a list. Running the
            // bootstrap under the same tenant lock as version approval prevents two first-time
            // requests from racing to create duplicate baseline templates.
            await EnsureDefaultCloseTemplatesAsync(cancellationToken);

            var templates = await _unitOfWork.Repository<FinanceCloseTemplate>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted)
                .Include(item => item.TaskDefinitions)
                .OrderBy(item => item.CloseType)
                .ThenBy(item => item.TemplateCode)
                .ThenByDescending(item => item.Version)
                .ToListAsync(cancellationToken);

            return templates.Select(MapFinanceCloseTemplateToDto).ToList();
        }

        public Task<FinanceCloseTemplateDto> CreateFinanceCloseTemplateVersionAsync(
            SaveFinanceCloseTemplateVersionDto request,
            CancellationToken cancellationToken = default)
            => ExecuteCloseTemplateControlAsync(
                () => CreateFinanceCloseTemplateVersionCoreAsync(request, cancellationToken),
                cancellationToken);

        private async Task<FinanceCloseTemplateDto> CreateFinanceCloseTemplateVersionCoreAsync(
            SaveFinanceCloseTemplateVersionDto request,
            CancellationToken cancellationToken)
        {
            ValidateAndNormalizeCloseTemplateRequest(request);
            await EnsureDefaultCloseTemplatesAsync(cancellationToken);

            var templateCode = request.TemplateCode;
            var existingVersions = await _unitOfWork.Repository<FinanceCloseTemplate>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.TemplateCode == templateCode && !item.IsDeleted)
                .OrderByDescending(item => item.Version)
                .ToListAsync(cancellationToken);

            if (existingVersions.Any(item => item.Status == FinanceCloseTemplateStatuses.Draft))
            {
                throw new InvalidOperationException(
                    $"Template '{templateCode}' already has a draft version. Update or approve that draft before creating another version.");
            }
            if (existingVersions.Count > 0 && existingVersions.Any(item =>
                !string.Equals(item.CloseType, request.CloseType, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Template code '{templateCode}' is already assigned to a different close type. Use a new code instead of changing version lineage.");
            }

            var template = new FinanceCloseTemplate
            {
                TenantId = TenantId,
                TemplateCode = templateCode,
                Name = request.Name,
                CloseType = request.CloseType,
                Version = (existingVersions.FirstOrDefault()?.Version ?? 0) + 1,
                Status = FinanceCloseTemplateStatuses.Draft,
                IsActive = false,
                IsSystemDefault = false,
                Description = request.Description,
                CreatedBy = UserName,
                CreatedById = CurrentUserId
            };

            await _unitOfWork.Repository<FinanceCloseTemplate>().AddAsync(template);
            await AddCloseTemplateTaskDefinitionsAsync(template, request.Tasks);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await RecordCloseTemplateAuditAsync(
                FinanceAuditEvents.AccountingPeriodCloseTemplateVersionCreated,
                template,
                afterValues: BuildCloseTemplateAuditSnapshot(template),
                cancellationToken: cancellationToken);

            return MapFinanceCloseTemplateToDto(template);
        }

        public Task<FinanceCloseTemplateDto> UpdateFinanceCloseTemplateDraftAsync(
            Guid templateId,
            SaveFinanceCloseTemplateVersionDto request,
            CancellationToken cancellationToken = default)
            => ExecuteCloseTemplateControlAsync(
                () => UpdateFinanceCloseTemplateDraftCoreAsync(templateId, request, cancellationToken),
                cancellationToken);

        private async Task<FinanceCloseTemplateDto> UpdateFinanceCloseTemplateDraftCoreAsync(
            Guid templateId,
            SaveFinanceCloseTemplateVersionDto request,
            CancellationToken cancellationToken)
        {
            ValidateAndNormalizeCloseTemplateRequest(request);

            var template = await _unitOfWork.Repository<FinanceCloseTemplate>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == templateId && !item.IsDeleted)
                .Include(item => item.TaskDefinitions)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException($"Finance close template '{templateId}' was not found.");

            if (template.Status != FinanceCloseTemplateStatuses.Draft)
                throw new InvalidOperationException("Only a draft close-template version can be edited.");
            if (!string.Equals(template.TemplateCode, request.TemplateCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A draft version cannot be moved to a different template code.");
            if (!string.Equals(template.CloseType, request.CloseType, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A draft version cannot be moved to a different close type.");

            var before = BuildCloseTemplateAuditSnapshot(template);
            template.Name = request.Name;
            template.Description = request.Description;
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = UserName;
            template.LastModifiedById = CurrentUserId;

            // Draft definitions have not governed a close cycle, so replacing them is safe. Once
            // approved, the service rejects edits and requires a new version instead.
            if (template.TaskDefinitions.Count > 0)
            {
                await _unitOfWork.Repository<FinanceCloseTemplateTaskDefinition>()
                    .DeleteRangeAsync(template.TaskDefinitions);
                template.TaskDefinitions.Clear();
            }

            await AddCloseTemplateTaskDefinitionsAsync(template, request.Tasks);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await RecordCloseTemplateAuditAsync(
                FinanceAuditEvents.AccountingPeriodCloseTemplateDraftUpdated,
                template,
                beforeValues: before,
                afterValues: BuildCloseTemplateAuditSnapshot(template),
                cancellationToken: cancellationToken);

            return MapFinanceCloseTemplateToDto(template);
        }

        public Task<FinanceCloseTemplateDto> ApproveFinanceCloseTemplateAsync(
            Guid templateId,
            ApproveFinanceCloseTemplateDto request,
            CancellationToken cancellationToken = default)
            => ExecuteCloseTemplateControlAsync(
                () => ApproveFinanceCloseTemplateCoreAsync(templateId, request, cancellationToken),
                cancellationToken);

        private async Task<FinanceCloseTemplateDto> ApproveFinanceCloseTemplateCoreAsync(
            Guid templateId,
            ApproveFinanceCloseTemplateDto request,
            CancellationToken cancellationToken)
        {
            var declaration = request.Declaration?.Trim() ?? string.Empty;
            if (declaration.Length < 20)
                throw new InvalidOperationException("The template approval declaration must contain at least 20 characters.");
            if (!CurrentUserId.HasValue)
                throw new InvalidOperationException("A resolved user identity is required to approve a Finance close template.");

            var template = await _unitOfWork.Repository<FinanceCloseTemplate>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == templateId && !item.IsDeleted)
                .Include(item => item.TaskDefinitions)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException($"Finance close template '{templateId}' was not found.");

            if (template.Status != FinanceCloseTemplateStatuses.Draft)
                throw new InvalidOperationException("Only a draft close-template version can be approved.");
            if (template.CreatedById == CurrentUserId)
                throw new InvalidOperationException("The template author cannot approve the same version. A second Finance administrator must review it.");

            // Re-run full structural validation against the stored draft. This prevents a future
            // API or import path from activating an incomplete template merely because it bypassed
            // the DTO validation used by the current controller.
            ValidateAndNormalizeCloseTemplateRequest(new SaveFinanceCloseTemplateVersionDto
            {
                TemplateCode = template.TemplateCode,
                Name = template.Name,
                CloseType = template.CloseType,
                Description = template.Description,
                Tasks = template.TaskDefinitions.Select(MapTemplateTaskToSaveDto).ToList()
            });

            var now = DateTime.UtcNow;
            var activeTemplates = await _unitOfWork.Repository<FinanceCloseTemplate>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.CloseType == template.CloseType && item.IsActive && !item.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var active in activeTemplates)
            {
                active.IsActive = false;
                active.Status = FinanceCloseTemplateStatuses.Superseded;
                active.SupersededAt = now;
                active.UpdatedAt = now;
                active.UpdatedBy = UserName;
                active.LastModifiedById = CurrentUserId;
            }

            template.Status = FinanceCloseTemplateStatuses.Approved;
            template.IsActive = true;
            template.ApprovedByUserId = CurrentUserId;
            template.ApprovedByUserName = UserName;
            template.ApprovedAt = now;
            template.ApprovalDeclaration = declaration;
            template.UpdatedAt = now;
            template.UpdatedBy = UserName;
            template.LastModifiedById = CurrentUserId;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await RecordCloseTemplateAuditAsync(
                FinanceAuditEvents.AccountingPeriodCloseTemplateApproved,
                template,
                afterValues: BuildCloseTemplateAuditSnapshot(template),
                comment: declaration,
                context: new { SupersededTemplateIds = activeTemplates.Select(item => item.Id).ToList() },
                cancellationToken: cancellationToken);

            return MapFinanceCloseTemplateToDto(template);
        }

        public Task<FinanceCloseWorkspaceDto> UpdateFinanceCloseTaskAsync(
            Guid periodId,
            Guid taskId,
            UpdateFinanceCloseTaskDto request,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                periodId,
                () => UpdateFinanceCloseTaskCoreAsync(periodId, taskId, request, cancellationToken),
                cancellationToken);

        private async Task<FinanceCloseWorkspaceDto> UpdateFinanceCloseTaskCoreAsync(
            Guid periodId,
            Guid taskId,
            UpdateFinanceCloseTaskDto request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!CurrentUserId.HasValue)
                throw new InvalidOperationException("A resolved user identity is required to maintain a Finance close task.");
            if (request.AssignedToUserId == Guid.Empty)
                throw new InvalidOperationException("Assigned user ID cannot be empty.");
            if (request.AssignToCurrentUser && request.AssignedToUserId.HasValue &&
                request.AssignedToUserId != CurrentUserId)
                throw new InvalidOperationException("Choose either self-assignment or a named assignee, not both.");
            if (!request.AssignToCurrentUser && !request.AssignedToUserId.HasValue &&
                !request.DueAt.HasValue && !request.MarkCompleted)
                throw new InvalidOperationException("No close-task change was requested.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == periodId)
                ?? throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");
            if (period.IsClosed || period.IsLocked || !period.IsOpen)
                throw new InvalidOperationException("Manual close tasks can be maintained only while the fiscal period is open and unlocked.");

            var cycle = await _unitOfWork.Repository<FinanceCloseCycle>()
                .GetQueryable(item => item.TenantId == TenantId && item.FiscalPeriodId == periodId &&
                    (item.Status == FinanceCloseStatuses.InProgress || item.Status == FinanceCloseStatuses.Prepared))
                .Include(item => item.Tasks)
                .OrderByDescending(item => item.CycleNumber)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Evaluate the close workspace before maintaining its manual tasks.");

            if (cycle.Status != FinanceCloseStatuses.InProgress)
                throw new InvalidOperationException("A prepared close cycle is frozen. Supersede its preparation evidence before changing tasks.");

            var task = cycle.Tasks.FirstOrDefault(item => item.Id == taskId && !item.IsDeleted)
                ?? throw new ArgumentException($"Finance close task '{taskId}' was not found in the active cycle.");
            if (task.IsAutomated)
                throw new InvalidOperationException("Automated close tasks can only be changed by a persisted control evaluation.");
            if (task.TaskCode == "PREPARER_CERTIFICATION")
                throw new InvalidOperationException("Use the preparation operation to complete the certification task.");
            if (task.Status == FinanceCloseTaskStatuses.Completed)
                throw new InvalidOperationException("Completed manual close-task evidence is immutable.");

            var before = new
            {
                task.AssignedToUserId,
                task.AssignedToUserName,
                task.DueAt,
                task.Status,
                task.EvidenceSummary
            };

            if (request.AssignToCurrentUser || request.AssignedToUserId.HasValue)
            {
                task.AssignedToUserId = request.AssignToCurrentUser
                    ? CurrentUserId
                    : request.AssignedToUserId;
                task.AssignedToUserName = task.AssignedToUserId == CurrentUserId ? UserName : null;
            }

            if (request.DueAt.HasValue)
            {
                var dueAt = request.DueAt.Value;
                var minimum = period.StartDate.Date.AddDays(-30);
                var maximum = period.EndDate.Date.AddDays(120).AddDays(1).AddTicks(-1);
                if (dueAt < minimum || dueAt > maximum)
                    throw new InvalidOperationException("Manual close-task due date must fall between 30 days before period start and 120 days after period end.");
                task.DueAt = dueAt;
            }

            if (request.MarkCompleted)
            {
                var evidence = request.EvidenceSummary?.Trim() ?? string.Empty;
                if (evidence.Length < 20)
                    throw new InvalidOperationException("Manual task completion requires an evidence summary of at least 20 characters.");
                if (task.AssignedToUserId.HasValue && task.AssignedToUserId != CurrentUserId)
                    throw new InvalidOperationException("Only the assigned user may complete this manual close task.");

                if (task.DependsOnTaskCode != null)
                {
                    var dependency = cycle.Tasks.First(item => item.TaskCode == task.DependsOnTaskCode);
                    if (dependency.Status != FinanceCloseTaskStatuses.Completed)
                        throw new InvalidOperationException($"Complete dependency '{dependency.Title}' before this task.");
                }

                var now = DateTime.UtcNow;
                task.AssignedToUserId ??= CurrentUserId;
                task.AssignedToUserName ??= UserName;
                task.Status = FinanceCloseTaskStatuses.Completed;
                task.CompletedAt = now;
                task.CompletedByUserId = CurrentUserId;
                task.CompletedByUserName = UserName;
                task.EvidenceSummary = evidence;
            }

            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = UserName;
            task.LastModifiedById = CurrentUserId;
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodCloseTaskUpdated,
                period,
                beforeValues: before,
                afterValues: new
                {
                    CloseCycleId = cycle.Id,
                    task.Id,
                    task.TaskCode,
                    task.AssignedToUserId,
                    task.AssignedToUserName,
                    task.DueAt,
                    task.Status,
                    task.CompletedAt,
                    task.CompletedByUserId,
                    task.EvidenceSummary
                },
                comment: request.MarkCompleted ? "Manual close task completed with retained evidence." : "Manual close task assignment or due date updated.",
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await LoadCloseWorkspaceDtoAsync(cycle.Id, cancellationToken);
        }

        public Task<FinanceCloseWorkspaceDto> LinkFinanceCloseEvidenceAsync(
            Guid periodId,
            Guid taskId,
            LinkFinanceCloseEvidenceDto request,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                periodId,
                () => LinkFinanceCloseEvidenceCoreAsync(periodId, taskId, request, cancellationToken),
                cancellationToken);

        private async Task<FinanceCloseWorkspaceDto> LinkFinanceCloseEvidenceCoreAsync(
            Guid periodId,
            Guid taskId,
            LinkFinanceCloseEvidenceDto request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!CurrentUserId.HasValue)
                throw new InvalidOperationException("A resolved user identity is required to link Finance close evidence.");
            if (request.FileUploadRecordId == Guid.Empty)
                throw new InvalidOperationException("A controlled file record is required.");

            var evidenceType = request.EvidenceType?.Trim() ?? string.Empty;
            var allowedEvidenceTypes = new[]
            {
                FinanceCloseEvidenceTypes.SupportingDocument,
                FinanceCloseEvidenceTypes.Reconciliation,
                FinanceCloseEvidenceTypes.ManagementApproval
            };
            if (!allowedEvidenceTypes.Contains(evidenceType, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Evidence type must be SupportingDocument, Reconciliation, or ManagementApproval.");
            evidenceType = allowedEvidenceTypes.First(item => item.Equals(evidenceType, StringComparison.OrdinalIgnoreCase));

            var (period, cycle) = await GetActiveCloseCycleForEvidenceAsync(periodId, cancellationToken);
            var task = cycle.Tasks.FirstOrDefault(item => item.Id == taskId && !item.IsDeleted)
                ?? throw new ArgumentException($"Finance close task '{taskId}' was not found in the active cycle.");
            if (task.TaskCode == "PREPARER_CERTIFICATION")
                throw new InvalidOperationException("The preparation declaration is the retained evidence for the certification task.");

            var file = await _unitOfWork.Repository<FileUploadRecord>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId &&
                    item.Id == request.FileUploadRecordId && !item.IsDeleted)
                ?? throw new ArgumentException("The controlled tenant file record was not found.");
            if (!file.Category.Equals(ControlledFileUploadCategories.FinanceCloseEvidence, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only files uploaded through the Finance close evidence category can be linked.");
            if (file.VirusScanStatus is FileVirusScanStatus.Pending or FileVirusScanStatus.Infected or FileVirusScanStatus.Error)
                throw new InvalidOperationException("The file cannot be linked until it has a clean or policy-approved scan status.");
            if (file.FileSize > 20 * 1024 * 1024)
                throw new InvalidOperationException("Finance close evidence files cannot exceed 20 MB.");

            var duplicate = await _unitOfWork.Repository<FinanceCloseEvidenceAttachment>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.FinanceCloseTaskId == task.Id &&
                    item.FileUploadRecordId == file.Id && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (duplicate)
                throw new InvalidOperationException("This file is already linked to the close task.");

            var attachment = new FinanceCloseEvidenceAttachment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                FinanceCloseCycleId = cycle.Id,
                FinanceCloseTaskId = task.Id,
                FileUploadRecordId = file.Id,
                EvidenceType = evidenceType,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = CurrentUserId
            };
            await _unitOfWork.Repository<FinanceCloseEvidenceAttachment>().AddAsync(attachment);
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodCloseEvidenceLinked,
                period,
                afterValues: new
                {
                    CloseCycleId = cycle.Id,
                    TaskId = task.Id,
                    task.TaskCode,
                    AttachmentId = attachment.Id,
                    FileUploadRecordId = file.Id,
                    file.OriginalFileName,
                    attachment.EvidenceType,
                    attachment.Description
                },
                comment: "Controlled Finance close evidence linked to the active task.",
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await LoadCloseWorkspaceDtoAsync(cycle.Id, cancellationToken);
        }

        public Task<FinanceCloseWorkspaceDto> RemoveFinanceCloseEvidenceAsync(
            Guid periodId,
            Guid taskId,
            Guid attachmentId,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                periodId,
                () => RemoveFinanceCloseEvidenceCoreAsync(periodId, taskId, attachmentId, cancellationToken),
                cancellationToken);

        private async Task<FinanceCloseWorkspaceDto> RemoveFinanceCloseEvidenceCoreAsync(
            Guid periodId,
            Guid taskId,
            Guid attachmentId,
            CancellationToken cancellationToken)
        {
            if (!CurrentUserId.HasValue)
                throw new InvalidOperationException("A resolved user identity is required to remove Finance close evidence.");
            var (period, cycle) = await GetActiveCloseCycleForEvidenceAsync(periodId, cancellationToken);
            var task = cycle.Tasks.FirstOrDefault(item => item.Id == taskId && !item.IsDeleted)
                ?? throw new ArgumentException($"Finance close task '{taskId}' was not found in the active cycle.");
            var attachment = await _unitOfWork.Repository<FinanceCloseEvidenceAttachment>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId &&
                    item.Id == attachmentId && item.FinanceCloseCycleId == cycle.Id &&
                    item.FinanceCloseTaskId == task.Id && !item.IsDeleted)
                ?? throw new ArgumentException("Finance close evidence attachment was not found.");
            if (!task.IsAutomated && task.Status == FinanceCloseTaskStatuses.Completed)
                throw new InvalidOperationException("Evidence for a completed manual close task is immutable.");

            var isWaiverEvidence = await _unitOfWork.Repository<FinanceCloseExceptionWaiver>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.FinanceCloseEvidenceAttachmentId == attachment.Id && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (isWaiverEvidence)
                throw new InvalidOperationException("Evidence referenced by a waiver request is immutable and cannot be removed.");

            var now = DateTime.UtcNow;
            attachment.IsDeleted = true;
            attachment.DeletedAt = now;
            attachment.DeletedBy = CurrentUserId.Value.ToString();
            attachment.UpdatedAt = now;
            attachment.UpdatedBy = UserName;
            attachment.LastModifiedById = CurrentUserId;
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodCloseEvidenceRemoved,
                period,
                beforeValues: new
                {
                    CloseCycleId = cycle.Id,
                    TaskId = task.Id,
                    task.TaskCode,
                    AttachmentId = attachment.Id,
                    attachment.FileUploadRecordId,
                    attachment.EvidenceType,
                    attachment.Description
                },
                comment: "Unused Finance close evidence link removed; the controlled upload record was retained.",
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await LoadCloseWorkspaceDtoAsync(cycle.Id, cancellationToken);
        }

        public Task<FinanceCloseWorkspaceDto> RequestFinanceCloseExceptionWaiverAsync(
            Guid periodId,
            Guid snapshotId,
            RequestFinanceCloseWaiverDto request,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                periodId,
                () => RequestFinanceCloseExceptionWaiverCoreAsync(periodId, snapshotId, request, cancellationToken),
                cancellationToken);

        private async Task<FinanceCloseWorkspaceDto> RequestFinanceCloseExceptionWaiverCoreAsync(
            Guid periodId,
            Guid snapshotId,
            RequestFinanceCloseWaiverDto request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!CurrentUserId.HasValue)
                throw new InvalidOperationException("A resolved user identity is required to request a Finance close waiver.");
            var justification = request.Justification?.Trim() ?? string.Empty;
            if (justification.Length < 50)
                throw new InvalidOperationException("A waiver request must explain the business need and mitigating control in at least 50 characters.");

            var (period, cycle) = await GetActiveCloseCycleForEvidenceAsync(periodId, cancellationToken);
            var snapshot = await _unitOfWork.Repository<FinanceCloseCheckSnapshot>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == snapshotId &&
                    item.FinanceCloseCycleId == cycle.Id && item.EvaluationNumber == cycle.EvaluationCount && !item.IsDeleted)
                ?? throw new ArgumentException("Only a check from the latest close evaluation can be waived.");
            if (snapshot.Status is not (FinanceCloseCheckStatuses.Failed or FinanceCloseCheckStatuses.Warning))
                throw new InvalidOperationException("Only a failed or warning close check can be submitted for waiver.");
            if (NonWaivableCloseCheckCodes.Contains(snapshot.CheckCode))
                throw new InvalidOperationException($"Close check '{snapshot.CheckCode}' is a non-waivable ledger control and must be resolved.");
            if (string.IsNullOrWhiteSpace(snapshot.EvidenceFingerprint))
                throw new InvalidOperationException("Re-evaluate the close workspace to generate fingerprinted waiver evidence.");

            var task = cycle.Tasks.FirstOrDefault(item =>
                    item.CheckCode != null && item.CheckCode.Equals(snapshot.CheckCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("The check is not linked to a task in this close cycle.");
            var attachment = await _unitOfWork.Repository<FinanceCloseEvidenceAttachment>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId &&
                    item.Id == request.FinanceCloseEvidenceAttachmentId &&
                    item.FinanceCloseCycleId == cycle.Id && item.FinanceCloseTaskId == task.Id && !item.IsDeleted)
                ?? throw new InvalidOperationException("Select active evidence linked to the same close task.");
            var existing = await _unitOfWork.Repository<FinanceCloseExceptionWaiver>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.FinanceCloseCheckSnapshotId == snapshot.Id && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (existing)
                throw new InvalidOperationException("A waiver request already exists for this check snapshot.");

            var waiver = new FinanceCloseExceptionWaiver
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                FinanceCloseCycleId = cycle.Id,
                FinanceCloseTaskId = task.Id,
                FinanceCloseCheckSnapshotId = snapshot.Id,
                FinanceCloseEvidenceAttachmentId = attachment.Id,
                CheckCode = snapshot.CheckCode,
                EvidenceFingerprint = snapshot.EvidenceFingerprint,
                Status = FinanceCloseWaiverStatuses.Requested,
                Justification = justification,
                RequestedByUserId = CurrentUserId.Value,
                RequestedByUserName = UserName,
                RequestedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = CurrentUserId
            };
            await _unitOfWork.Repository<FinanceCloseExceptionWaiver>().AddAsync(waiver);
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodCloseWaiverRequested,
                period,
                afterValues: new
                {
                    CloseCycleId = cycle.Id,
                    WaiverId = waiver.Id,
                    SnapshotId = snapshot.Id,
                    TaskId = task.Id,
                    waiver.CheckCode,
                    waiver.EvidenceFingerprint,
                    EvidenceAttachmentId = attachment.Id,
                    waiver.Justification,
                    waiver.RequestedByUserId
                },
                comment: "Finance close exception waiver submitted for independent review.",
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await LoadCloseWorkspaceDtoAsync(cycle.Id, cancellationToken);
        }

        public Task<FinanceCloseWorkspaceDto> ReviewFinanceCloseExceptionWaiverAsync(
            Guid periodId,
            Guid waiverId,
            ReviewFinanceCloseWaiverDto request,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                periodId,
                () => ReviewFinanceCloseExceptionWaiverCoreAsync(periodId, waiverId, request, cancellationToken),
                cancellationToken);

        private async Task<FinanceCloseWorkspaceDto> ReviewFinanceCloseExceptionWaiverCoreAsync(
            Guid periodId,
            Guid waiverId,
            ReviewFinanceCloseWaiverDto request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!CurrentUserId.HasValue)
                throw new InvalidOperationException("A resolved user identity is required to review a Finance close waiver.");
            var comment = request.Comment?.Trim() ?? string.Empty;
            if (comment.Length < 20)
                throw new InvalidOperationException("The waiver review comment must contain at least 20 characters.");

            var (period, cycle) = await GetActiveCloseCycleForEvidenceAsync(periodId, cancellationToken);
            var waiver = await _unitOfWork.Repository<FinanceCloseExceptionWaiver>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == waiverId &&
                    item.FinanceCloseCycleId == cycle.Id && !item.IsDeleted)
                ?? throw new ArgumentException("Finance close waiver request was not found.");
            if (waiver.Status != FinanceCloseWaiverStatuses.Requested)
                throw new InvalidOperationException("Only a pending waiver request can be reviewed.");
            if (waiver.RequestedByUserId == CurrentUserId.Value)
                throw new InvalidOperationException("The waiver requester cannot review their own request.");

            var now = DateTime.UtcNow;
            waiver.Status = request.Approve
                ? FinanceCloseWaiverStatuses.Approved
                : FinanceCloseWaiverStatuses.Rejected;
            waiver.ReviewedByUserId = CurrentUserId;
            waiver.ReviewedByUserName = UserName;
            waiver.ReviewedAt = now;
            waiver.ReviewComment = comment;
            waiver.UpdatedAt = now;
            waiver.UpdatedBy = UserName;
            waiver.LastModifiedById = CurrentUserId;
            await RecordPeriodAuditAsync(
                request.Approve
                    ? FinanceAuditEvents.AccountingPeriodCloseWaiverApproved
                    : FinanceAuditEvents.AccountingPeriodCloseWaiverRejected,
                period,
                beforeValues: new { Status = FinanceCloseWaiverStatuses.Requested },
                afterValues: new
                {
                    CloseCycleId = cycle.Id,
                    WaiverId = waiver.Id,
                    waiver.CheckCode,
                    waiver.EvidenceFingerprint,
                    waiver.Status,
                    waiver.ReviewedByUserId,
                    waiver.ReviewComment
                },
                comment: request.Approve
                    ? "Eligible Finance close exception waiver independently approved."
                    : "Finance close exception waiver rejected.",
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Approval is not itself a pass. Re-run every provider immediately so only an exact
            // fingerprint match can produce Waived; changed evidence remains a live blocker.
            return request.Approve
                ? await EvaluatePeriodCloseWorkspaceCoreAsync(periodId, cancellationToken)
                : await LoadCloseWorkspaceDtoAsync(cycle.Id, cancellationToken);
        }

        private async Task<(FiscalPeriod Period, FinanceCloseCycle Cycle)> GetActiveCloseCycleForEvidenceAsync(
            Guid periodId,
            CancellationToken cancellationToken)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == periodId)
                ?? throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");
            if (period.IsClosed || period.IsLocked || !period.IsOpen)
                throw new InvalidOperationException("Finance close evidence can be maintained only while the fiscal period is open and unlocked.");

            var cycle = await _unitOfWork.Repository<FinanceCloseCycle>()
                .GetQueryable(item => item.TenantId == TenantId && item.FiscalPeriodId == periodId &&
                    item.Status == FinanceCloseStatuses.InProgress)
                .Include(item => item.Tasks)
                .OrderByDescending(item => item.CycleNumber)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Evaluate the close workspace before maintaining evidence or waivers.");
            return (period, cycle);
        }

        public Task<PeriodCloseResultDto> ClosePeriodAsync(
            PeriodCloseRequestDto request,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                request.FiscalPeriodId,
                () => ClosePeriodCoreAsync(request, cancellationToken),
                cancellationToken);

        private async Task<PeriodCloseResultDto> ClosePeriodCoreAsync(
            PeriodCloseRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var reviewerDeclaration = request.ReviewerDeclaration?.Trim() ?? string.Empty;
            if (reviewerDeclaration.Length < 20)
                throw new InvalidOperationException("The close reviewer declaration must contain at least 20 characters.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == request.FiscalPeriodId);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{request.FiscalPeriodId}' not found.");

            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodCloseRequested,
                period,
                beforeValues: null,
                afterValues: BuildPeriodAuditSnapshot(period),
                comment: request.ClosingNotes,
                context: new { ReviewerDeclarationProvided = true },
                cancellationToken: cancellationToken);

            if (period.IsLocked)
                throw new InvalidOperationException("Cannot close a locked period.");

            if (period.PeriodStatus == "Closed")
                throw new InvalidOperationException("Period is already closed.");

            if (!period.IsOpen || !string.Equals(period.PeriodStatus, "Open", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only open periods can be closed.");
            }

            // Re-evaluate immediately before close so a journal posted after preparation cannot
            // slip through on stale evidence. A new blocker automatically supersedes the maker's
            // certificate in EvaluatePeriodCloseWorkspaceAsync.
            var workspace = await EvaluatePeriodCloseWorkspaceCoreAsync(request.FiscalPeriodId, cancellationToken);
            if (workspace.MandatoryBlockerCount > 0)
            {
                await RecordPeriodAuditAsync(
                    FinanceAuditEvents.AccountingPeriodCloseValidationFailed,
                    period,
                    beforeValues: BuildPeriodAuditSnapshot(period),
                    afterValues: workspace,
                    comment: "Accounting period close validation failed.",
                    context: new { workspace.MandatoryBlockerCount, workspace.WarningCount },
                    cancellationToken: cancellationToken);

                return new PeriodCloseResultDto
                {
                    Success = false,
                    Message = "Validation failed",
                    FiscalPeriodId = period.Id,
                    PeriodName = period.PeriodName,
                    Errors = workspace.Checks
                        .Where(item => item.Severity == FinanceCloseCheckSeverities.Mandatory &&
                            item.Status == FinanceCloseCheckStatuses.Failed)
                        .Select(item => item.ResultSummary)
                        .ToList()
                };
            }

            var cycle = await _unitOfWork.Repository<FinanceCloseCycle>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == workspace.CycleId)
                ?? throw new InvalidOperationException("The active close cycle could not be resolved.");
            var certification = await _unitOfWork.Repository<FinanceCloseCertification>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId &&
                    item.FinanceCloseCycleId == cycle.Id && !item.IsSuperseded);
            if (certification?.PreparedAt == null || cycle.Status != FinanceCloseStatuses.Prepared)
                throw new InvalidOperationException("A signed preparation certificate is required before the period can close.");
            if (!CurrentUserId.HasValue)
                throw new InvalidOperationException("A resolved user identity is required to approve a Finance close.");
            if (certification.PreparedByUserId == CurrentUserId)
                throw new InvalidOperationException("The close preparer cannot approve the same close cycle. A second authorised user must review and close it.");

            var validation = await ValidatePeriodCloseAsync(request.FiscalPeriodId, cancellationToken);

            var beforeClose = BuildPeriodAuditSnapshot(period);
            period.PeriodStatus = "Closed";
            period.IsOpen = false;
            period.IsClosed = true;
            period.ClosedDate = DateTime.UtcNow;
            period.ClosedByUserId = CurrentUserId;
            period.ClosingNotes = request.ClosingNotes;
            period.UpdatedAt = DateTime.UtcNow;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            var approvalTime = DateTime.UtcNow;
            certification.ReviewedByUserId = CurrentUserId;
            certification.ReviewedByUserName = UserName;
            certification.ReviewedAt = approvalTime;
            certification.ReviewerDeclaration = reviewerDeclaration;
            certification.ApprovedByUserId = CurrentUserId;
            certification.ApprovedByUserName = UserName;
            certification.ApprovedAt = approvalTime;
            certification.UpdatedAt = approvalTime;
            certification.UpdatedBy = UserName;
            certification.LastModifiedById = CurrentUserId;
            cycle.Status = FinanceCloseStatuses.Closed;
            cycle.ClosedAt = approvalTime;
            cycle.UpdatedAt = approvalTime;
            cycle.UpdatedBy = UserName;
            cycle.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodClosed,
                period,
                beforeValues: beforeClose,
                afterValues: BuildPeriodAuditSnapshot(period),
                comment: request.ClosingNotes,
                context: new
                {
                    validation.TotalDebits,
                    validation.TotalCredits,
                    validation.Difference,
                    validation.TotalJournalEntries,
                    validation.TotalTransactionLines,
                    CloseCycleId = cycle.Id,
                    cycle.CycleNumber,
                    CertificationId = certification.Id
                },
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fiscal period {Code} closed by {User}", period.PeriodCode, UserName);

            return new PeriodCloseResultDto
            {
                Success = true,
                Message = "Period closed successfully.",
                FiscalPeriodId = period.Id,
                PeriodName = period.PeriodName,
                ClosedDate = period.ClosedDate,
                ClosedByUserName = UserName
            };
        }

        public Task<FinancePeriodReopenRequestDto> RequestPeriodReopenAsync(
            PeriodReopenRequestDto request,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                request.FiscalPeriodId,
                () => RequestPeriodReopenCoreAsync(request, cancellationToken),
                cancellationToken);

        private async Task<FinancePeriodReopenRequestDto> RequestPeriodReopenCoreAsync(
            PeriodReopenRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var reason = request.Reason?.Trim() ?? string.Empty;
            var assessment = request.AffectedPeriodAssessment?.Trim() ?? string.Empty;
            if (reason.Length < 20)
                throw new InvalidOperationException("The period reopen reason must contain at least 20 characters.");
            if (assessment.Length < 20)
                throw new InvalidOperationException("The affected-period assessment must contain at least 20 characters.");
            if (!CurrentUserId.HasValue)
                throw new InvalidOperationException("A resolved user identity is required to request an accounting-period reopen.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == request.FiscalPeriodId);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{request.FiscalPeriodId}' not found.");

            if (period.IsLocked)
                throw new InvalidOperationException("Cannot reopen a locked period.");

            if (period.PeriodStatus != "Closed")
                throw new InvalidOperationException("Only closed periods can be reopened.");

            var pendingRequestExists = await _unitOfWork.Repository<FinancePeriodReopenRequest>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.FiscalPeriodId == period.Id &&
                    item.Status == FinancePeriodReopenStatuses.PendingApproval &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (pendingRequestExists)
                throw new InvalidOperationException("This period already has a reopen request awaiting higher-tier review.");

            var closedCycle = await _unitOfWork.Repository<FinanceCloseCycle>()
                .GetQueryable(item => item.TenantId == TenantId && item.FiscalPeriodId == period.Id &&
                    item.Status == FinanceCloseStatuses.Closed)
                .OrderByDescending(item => item.CycleNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (closedCycle == null)
                throw new InvalidOperationException("The period has no completed numbered Finance close cycle to supersede.");

            var validation = await BuildPeriodReopenImpactValidationAsync(period, closedCycle, cancellationToken);
            if (validation.Blockers.Count > 0)
                throw new InvalidOperationException(string.Join(" ", validation.Blockers));

            var reopenRequest = new FinancePeriodReopenRequest
            {
                TenantId = TenantId,
                FiscalPeriodId = period.Id,
                FinanceCloseCycleId = closedCycle.Id,
                Status = FinancePeriodReopenStatuses.PendingApproval,
                Reason = reason,
                AffectedPeriodAssessment = assessment,
                ImpactSnapshotJson = validation.SnapshotJson,
                ImpactFingerprint = validation.Fingerprint,
                AffectedPeriodCount = validation.Snapshot.AffectedPeriods.Count,
                RequestedByUserId = CurrentUserId.Value,
                RequestedByUserName = UserName,
                RequestedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = CurrentUserId
            };
            await _unitOfWork.Repository<FinancePeriodReopenRequest>().AddAsync(reopenRequest);

            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodReopenRequested,
                period,
                beforeValues: BuildPeriodAuditSnapshot(period),
                afterValues: MapFinancePeriodReopenRequestToDto(reopenRequest, closedCycle.CycleNumber),
                reason: reason,
                comment: assessment,
                context: new
                {
                    ReopenRequestId = reopenRequest.Id,
                    CloseCycleId = closedCycle.Id,
                    closedCycle.CycleNumber,
                    validation.Fingerprint,
                    validation.Snapshot.AffectedPeriods
                },
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Fiscal period {Code} reopen requested by {User}; close cycle {CycleNumber} remains closed pending independent approval.",
                period.PeriodCode,
                UserName,
                closedCycle.CycleNumber);
            return MapFinancePeriodReopenRequestToDto(reopenRequest, closedCycle.CycleNumber);
        }

        public Task<FinancePeriodReopenRequestDto> ReviewPeriodReopenAsync(
            Guid periodId,
            Guid requestId,
            PeriodReopenReviewDto request,
            CancellationToken cancellationToken = default)
            => ExecutePeriodCloseControlAsync(
                periodId,
                () => ReviewPeriodReopenCoreAsync(periodId, requestId, request, cancellationToken),
                cancellationToken);

        private async Task<FinancePeriodReopenRequestDto> ReviewPeriodReopenCoreAsync(
            Guid periodId,
            Guid requestId,
            PeriodReopenReviewDto request,
            CancellationToken cancellationToken)
        {
            var reviewComment = request.ReviewComment?.Trim() ?? string.Empty;
            if (reviewComment.Length < 20)
                throw new InvalidOperationException("The reopen review declaration must contain at least 20 characters.");
            if (!CurrentUserId.HasValue)
                throw new InvalidOperationException("A resolved user identity is required to review an accounting-period reopen.");

            var reopenRequest = await _unitOfWork.Repository<FinancePeriodReopenRequest>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.Id == requestId &&
                    item.FiscalPeriodId == periodId &&
                    !item.IsDeleted)
                .Include(item => item.FiscalPeriod)
                .Include(item => item.FinanceCloseCycle)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException($"Period reopen request with Id '{requestId}' was not found.");

            if (reopenRequest.Status != FinancePeriodReopenStatuses.PendingApproval)
                throw new InvalidOperationException("Only a reopen request awaiting approval can be reviewed.");
            if (reopenRequest.RequestedByUserId == CurrentUserId)
                throw new InvalidOperationException("The reopen requester cannot review the same request. A higher-tier authorised user must decide it.");

            var now = DateTime.UtcNow;
            reopenRequest.ReviewedByUserId = CurrentUserId;
            reopenRequest.ReviewedByUserName = UserName;
            reopenRequest.ReviewedAt = now;
            reopenRequest.ReviewComment = reviewComment;
            reopenRequest.UpdatedAt = now;
            reopenRequest.UpdatedBy = UserName;
            reopenRequest.LastModifiedById = CurrentUserId;

            if (!request.Approved)
            {
                reopenRequest.Status = FinancePeriodReopenStatuses.Rejected;
                await RecordPeriodAuditAsync(
                    FinanceAuditEvents.AccountingPeriodReopenRejected,
                    reopenRequest.FiscalPeriod,
                    beforeValues: new { Status = FinancePeriodReopenStatuses.PendingApproval },
                    afterValues: new { reopenRequest.Status, reopenRequest.ReviewedByUserName, reopenRequest.ReviewedAt },
                    reason: reopenRequest.Reason,
                    comment: reviewComment,
                    context: new { ReopenRequestId = reopenRequest.Id, reopenRequest.FinanceCloseCycleId },
                    cancellationToken: cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return MapFinancePeriodReopenRequestToDto(reopenRequest);
            }

            // A reviewer approves the exact affected-period state retained by the maker. Any
            // intervening close, lock or reopen changes the fingerprint and requires a new request
            // rather than silently broadening the authority granted by this decision.
            var validation = await BuildPeriodReopenImpactValidationAsync(
                reopenRequest.FiscalPeriod,
                reopenRequest.FinanceCloseCycle,
                cancellationToken);
            if (validation.Blockers.Count > 0)
                throw new InvalidOperationException(string.Join(" ", validation.Blockers));
            if (!string.Equals(validation.Fingerprint, reopenRequest.ImpactFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Affected-period state changed after the request was submitted. Reject this stale request and submit a new validated request.");

            var period = reopenRequest.FiscalPeriod;
            var closedCycle = reopenRequest.FinanceCloseCycle;
            var beforeReopen = BuildPeriodAuditSnapshot(period);
            period.PeriodStatus = "Open";
            period.IsOpen = true;
            period.IsClosed = false;
            period.HasBeenReopened = true;
            period.ReopenCount++;
            period.LastReopenedDate = now;
            period.LastReopenedByUserId = CurrentUserId;
            period.ReopenReason = reopenRequest.Reason;
            period.UpdatedAt = now;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            closedCycle.Status = FinanceCloseStatuses.Reopened;
            closedCycle.ReopenedAt = now;
            closedCycle.ReopenedByUserId = CurrentUserId;
            closedCycle.ReopenReason = reopenRequest.Reason;
            closedCycle.UpdatedAt = now;
            closedCycle.UpdatedBy = UserName;
            closedCycle.LastModifiedById = CurrentUserId;

            var certification = await _unitOfWork.Repository<FinanceCloseCertification>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId &&
                    item.FinanceCloseCycleId == closedCycle.Id && !item.IsSuperseded)
                ?? throw new InvalidOperationException("The signed close certificate to supersede could not be resolved.");
            certification.IsSuperseded = true;
            certification.SupersededAt = now;
            certification.SupersededReason = $"Approved period reopen request {reopenRequest.Id}: {reopenRequest.Reason}";
            certification.UpdatedAt = now;
            certification.UpdatedBy = UserName;
            certification.LastModifiedById = CurrentUserId;

            reopenRequest.Status = FinancePeriodReopenStatuses.Approved;
            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);

            // Create cycle N+1 immediately, within this same serialised transaction. Waiting for a
            // later workspace visit would leave reopened books without a visible re-certification
            // obligation and would weaken the audit link from approval to required re-close.
            var resultingCycle = await GetOrCreateActiveCloseCycleAsync(period, cancellationToken);
            reopenRequest.ResultingFinanceCloseCycleId = resultingCycle.Id;

            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodReopenApproved,
                period,
                beforeValues: new { Status = FinancePeriodReopenStatuses.PendingApproval },
                afterValues: new
                {
                    reopenRequest.Status,
                    reopenRequest.ReviewedByUserName,
                    reopenRequest.ReviewedAt,
                    ResultingCloseCycleId = resultingCycle.Id,
                    resultingCycle.CycleNumber
                },
                reason: reopenRequest.Reason,
                comment: reviewComment,
                context: new { ReopenRequestId = reopenRequest.Id, ImpactFingerprint = validation.Fingerprint },
                cancellationToken: cancellationToken);
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodReopened,
                period,
                beforeValues: beforeReopen,
                afterValues: BuildPeriodAuditSnapshot(period),
                reason: reopenRequest.Reason,
                context: new
                {
                    ReopenRequestId = reopenRequest.Id,
                    CloseCycleId = closedCycle.Id,
                    closedCycle.CycleNumber,
                    ResultingCloseCycleId = resultingCycle.Id,
                    ResultingCycleNumber = resultingCycle.CycleNumber
                },
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Fiscal period {Code} reopen request {RequestId} approved by {User}; cycle {OldCycle} superseded and cycle {NewCycle} started.",
                period.PeriodCode,
                reopenRequest.Id,
                UserName,
                closedCycle.CycleNumber,
                resultingCycle.CycleNumber);
            return MapFinancePeriodReopenRequestToDto(reopenRequest);
        }

        public async Task<FiscalPeriodDto> LockPeriodAsync(PeriodLockRequestDto request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.LockReason))
                throw new InvalidOperationException("A reason is required to lock an accounting period.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == request.FiscalPeriodId);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{request.FiscalPeriodId}' not found.");

            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodLockAttempted,
                period,
                beforeValues: BuildPeriodAuditSnapshot(period),
                afterValues: BuildPeriodAuditSnapshot(period),
                reason: request.LockReason.Trim(),
                cancellationToken: cancellationToken);

            if (period.IsLocked)
                throw new InvalidOperationException("Period is already locked.");

            if (period.PeriodStatus != "Closed")
                throw new InvalidOperationException("Only closed periods can be locked.");

            var beforeLock = BuildPeriodAuditSnapshot(period);
            period.IsLocked = true;
            period.LockedDate = DateTime.UtcNow;
            period.LockedByUserId = CurrentUserId;
            period.LockReason = request.LockReason.Trim();
            period.PeriodStatus = "Locked";
            period.UpdatedAt = DateTime.UtcNow;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodLocked,
                period,
                beforeValues: beforeLock,
                afterValues: BuildPeriodAuditSnapshot(period),
                reason: period.LockReason,
                comment: "Accounting period locked.",
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fiscal period {Code} locked by {User}. Reason: {Reason}",
                period.PeriodCode, UserName, request.LockReason);

            return MapFiscalPeriodToDto(period);
        }

        public async Task<FiscalPeriodDto> UnlockPeriodAsync(Guid periodId, string reason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("A reason is required to unlock an accounting period.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == periodId);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");

            if (!period.IsLocked)
                throw new InvalidOperationException("Period is not locked.");

            var beforeUnlock = BuildPeriodAuditSnapshot(period);
            period.IsLocked = false;
            period.LockedDate = null;
            period.LockedByUserId = null;
            period.LockReason = null;
            period.PeriodStatus = period.IsClosed ? "Closed" : "Open";

            period.UpdatedAt = DateTime.UtcNow;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodUnlocked,
                period,
                beforeValues: beforeUnlock,
                afterValues: BuildPeriodAuditSnapshot(period),
                reason: reason.Trim(),
                comment: "Accounting period unlocked.",
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Fiscal period {Code} UNLOCKED by {User}. Reason: {Reason}",
                period.PeriodCode, UserName, reason);

            return MapFiscalPeriodToDto(period);
        }



        public async Task<FiscalPeriodDto> LockPeriodForModuleAsync(Guid periodId, string moduleCode, string reason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("A reason is required to lock a module for an accounting period.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId && fp.Id == periodId)
                .Include(fp => fp.PeriodModuleLocks)
                    .ThenInclude(pml => pml.ModuleDefinition)
                .FirstOrDefaultAsync(cancellationToken);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");

            if (period.IsLocked)
                throw new InvalidOperationException("The period is globally locked; every module is already locked.");

            if (!period.IsOpen || period.IsClosed)
                throw new InvalidOperationException("Module locks can only be changed while the accounting period is open.");

            var module = await GetLockableModuleAsync(moduleCode, cancellationToken);
            var normalizedReason = reason.Trim();
            var now = DateTime.UtcNow;
            var beforeValues = BuildModuleLockAuditSnapshot(
                period.PeriodModuleLocks.FirstOrDefault(item => item.ModuleDefinitionId == module.Id));

            var existingLock = period.PeriodModuleLocks.FirstOrDefault(l => l.ModuleDefinitionId == module.Id);

            if (existingLock != null)
            {
                if (existingLock.IsLocked)
                    throw new InvalidOperationException($"Period is already locked for module {moduleCode}.");

                existingLock.IsLocked = true;
                existingLock.LockedDate = now;
                existingLock.LockedByUserId = CurrentUserId;
                existingLock.LockReason = normalizedReason;
                existingLock.ReopenExpiresAtUtc = null;
                existingLock.ExpiryWarningSentAtUtc = null;
                existingLock.AutoRelockedDate = null;
                existingLock.UpdatedAt = now;
                existingLock.UpdatedBy = UserName;

                await _unitOfWork.Repository<PeriodModuleLock>().UpdateAsync(existingLock);
            }
            else
            {
                var newLock = new PeriodModuleLock
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    FiscalPeriodId = period.Id,
                    ModuleDefinitionId = module.Id,
                    IsLocked = true,
                    LockedDate = now,
                    LockedByUserId = CurrentUserId,
                    LockReason = normalizedReason,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                await _unitOfWork.Repository<PeriodModuleLock>().AddAsync(newLock);
                period.PeriodModuleLocks.Add(newLock);
                existingLock = newLock;
            }

            await RestoreGlobalLockIfNoModulesOpenAsync(period, cancellationToken);
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodModuleLocked,
                period,
                beforeValues,
                BuildModuleLockAuditSnapshot(existingLock),
                normalizedReason,
                $"{module.ModuleName} locked for {period.PeriodName}.",
                new { module.ModuleCode, module.ModuleName },
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "Module {ModuleCode} locked for fiscal period {PeriodCode} by {User}. Reason: {Reason}",
                module.ModuleCode, period.PeriodCode, UserName, normalizedReason);
            return MapFiscalPeriodToDto(period);
        }

        public async Task<FiscalPeriodDto> UnlockPeriodForModuleAsync(
            Guid periodId,
            string moduleCode,
            string reason,
            DateTime reopenUntilUtc,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("A reason is required to reopen a module for an accounting period.");

            var now = DateTime.UtcNow;
            var normalizedExpiry = reopenUntilUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(reopenUntilUtc, DateTimeKind.Utc)
                : reopenUntilUtc.ToUniversalTime();
            if (normalizedExpiry <= now.AddMinutes(1))
                throw new InvalidOperationException("The module reopening expiry must be at least one minute in the future.");
            if (normalizedExpiry > now.AddHours(24))
                throw new InvalidOperationException("A module can be reopened for no more than 24 hours.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId && fp.Id == periodId)
                .Include(fp => fp.PeriodModuleLocks)
                    .ThenInclude(pml => pml.ModuleDefinition)
                .FirstOrDefaultAsync(cancellationToken);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");

            if (!period.IsLocked && (!period.IsOpen || period.IsClosed))
                throw new InvalidOperationException("A closed period must be reopened before an individual module can be reopened.");

            var lockableModules = await GetLockableModulesAsync(cancellationToken);
            var module = lockableModules.FirstOrDefault(item =>
                item.ModuleCode.Equals(moduleCode?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Module '{moduleCode}' is not enabled and integrated with Finance for this tenant.");

            var normalizedReason = reason.Trim();
            var wasGloballyLocked = period.IsLocked;
            var beforePeriod = BuildPeriodAuditSnapshot(period);

            if (wasGloballyLocked)
            {
                // The old global state overrides every historical module row, including
                // modules that have since been disabled for the tenant. This prevents a
                // stale open row from keeping the suspended global lock alive forever.
                foreach (var existingModuleLock in period.PeriodModuleLocks)
                {
                    existingModuleLock.IsLocked = true;
                    existingModuleLock.LockedDate = period.LockedDate ?? now;
                    existingModuleLock.LockedByUserId = period.LockedByUserId;
                    existingModuleLock.LockReason = period.LockReason ?? "Global accounting period lock";
                    existingModuleLock.ReopenExpiresAtUtc = null;
                    existingModuleLock.ExpiryWarningSentAtUtc = null;
                    existingModuleLock.AutoRelockedDate = null;
                    existingModuleLock.UpdatedAt = now;
                    existingModuleLock.UpdatedBy = UserName;
                }

                foreach (var lockableModule in lockableModules)
                {
                    var moduleLock = period.PeriodModuleLocks.FirstOrDefault(item =>
                        item.ModuleDefinitionId == lockableModule.Id);
                    if (moduleLock == null)
                    {
                        moduleLock = new PeriodModuleLock
                        {
                            Id = Guid.NewGuid(),
                            TenantId = TenantId,
                            FiscalPeriodId = period.Id,
                            ModuleDefinitionId = lockableModule.Id,
                            CreatedAt = now,
                            CreatedBy = UserName,
                            ModuleDefinition = lockableModule
                        };
                        await _unitOfWork.Repository<PeriodModuleLock>().AddAsync(moduleLock);
                        period.PeriodModuleLocks.Add(moduleLock);
                    }

                    moduleLock.IsLocked = lockableModule.Id != module.Id;
                    moduleLock.LockedDate = moduleLock.IsLocked ? period.LockedDate ?? now : null;
                    moduleLock.LockedByUserId = moduleLock.IsLocked ? period.LockedByUserId : null;
                    moduleLock.LockReason = moduleLock.IsLocked
                        ? period.LockReason ?? "Global accounting period lock"
                        : moduleLock.LockReason;
                    moduleLock.ReopenExpiresAtUtc = moduleLock.IsLocked ? null : normalizedExpiry;
                    moduleLock.ExpiryWarningSentAtUtc = null;
                    moduleLock.AutoRelockedDate = null;
                    moduleLock.UnlockedDate = moduleLock.IsLocked ? moduleLock.UnlockedDate : now;
                    moduleLock.UnlockedByUserId = moduleLock.IsLocked ? moduleLock.UnlockedByUserId : CurrentUserId;
                    moduleLock.UnlockReason = moduleLock.IsLocked ? moduleLock.UnlockReason : normalizedReason;
                    moduleLock.UpdatedAt = now;
                    moduleLock.UpdatedBy = UserName;
                }

                // A global lock remains absolute. Reopening one module converts the period
                // into an explicitly represented partial lock instead of overriding it.
                period.IsLocked = false;
                period.IsGlobalLockSuspended = true;
                period.IsOpen = true;
                period.IsClosed = false;
                period.PeriodStatus = "Open";
                period.UpdatedAt = now;
                period.UpdatedBy = UserName;
                period.LastModifiedById = CurrentUserId;
            }

            var existingLock = period.PeriodModuleLocks.FirstOrDefault(l => l.ModuleDefinitionId == module.Id);
            if (existingLock == null && period.IsGlobalLockSuspended)
            {
                existingLock = new PeriodModuleLock
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    FiscalPeriodId = period.Id,
                    ModuleDefinitionId = module.Id,
                    ModuleDefinition = module,
                    IsLocked = true,
                    LockedDate = period.LockedDate ?? now,
                    LockedByUserId = period.LockedByUserId,
                    LockReason = period.LockReason ?? "Suspended global accounting period lock",
                    CreatedAt = now,
                    CreatedBy = UserName
                };
                await _unitOfWork.Repository<PeriodModuleLock>().AddAsync(existingLock);
                period.PeriodModuleLocks.Add(existingLock);
            }

            if (existingLock == null)
            {
                throw new InvalidOperationException($"Period is not currently locked for module {module.ModuleName}.");
            }

            var reopeningHasExpired = !existingLock.IsLocked
                && existingLock.ReopenExpiresAtUtc.HasValue
                && existingLock.ReopenExpiresAtUtc <= now;
            if (!wasGloballyLocked && !existingLock.IsLocked && !reopeningHasExpired)
            {
                if (existingLock.ReopenExpiresAtUtc.HasValue && existingLock.ReopenExpiresAtUtc > now)
                    throw new InvalidOperationException($"Module {module.ModuleName} is already temporarily open.");

                throw new InvalidOperationException($"Period is not currently locked for module {module.ModuleName}.");
            }

            var beforeLock = BuildModuleLockAuditSnapshot(existingLock);
            existingLock.IsLocked = false;
            existingLock.UnlockedDate = now;
            existingLock.UnlockedByUserId = CurrentUserId;
            existingLock.UnlockReason = normalizedReason;
            existingLock.ReopenExpiresAtUtc = normalizedExpiry;
            existingLock.ExpiryWarningSentAtUtc = null;
            existingLock.AutoRelockedDate = null;
            existingLock.UpdatedAt = now;
            existingLock.UpdatedBy = UserName;

            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodModuleReopened,
                period,
                beforeLock,
                BuildModuleLockAuditSnapshot(existingLock),
                normalizedReason,
                $"{module.ModuleName} temporarily reopened for {period.PeriodName}.",
                new
                {
                    module.ModuleCode,
                    module.ModuleName,
                    reopenUntilUtc = normalizedExpiry,
                    convertedFromGlobalLock = wasGloballyLocked,
                    beforePeriod,
                    afterPeriod = BuildPeriodAuditSnapshot(period)
                },
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Module {ModuleCode} reopened for fiscal period {PeriodCode} by {User} until {Expiry:o}. Reason: {Reason}",
                module.ModuleCode, period.PeriodCode, UserName, normalizedExpiry, normalizedReason);
            return MapFiscalPeriodToDto(period);
        }

        public async Task<bool> IsPeriodLockedForModuleAsync(Guid periodId, string moduleCode, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId && fp.Id == periodId)
                .Include(fp => fp.PeriodModuleLocks)
                    .ThenInclude(pml => pml.ModuleDefinition)
                .FirstOrDefaultAsync(cancellationToken);

            if (period == null || period.IsLocked || period.IsClosed || !period.IsOpen)
                return true;

            var moduleLock = period.PeriodModuleLocks.FirstOrDefault(moduleItem =>
                moduleItem.TenantId == TenantId
                && moduleItem.ModuleDefinition.TenantId == TenantId
                && moduleItem.ModuleDefinition.ModuleCode == moduleCode);

            if (moduleLock == null)
                return period.IsGlobalLockSuspended;

            return moduleLock.IsLocked
                || (moduleLock.ReopenExpiresAtUtc.HasValue && moduleLock.ReopenExpiresAtUtc <= now);
        }

        public async Task<IReadOnlyList<ModuleDefinitionDto>> GetModuleDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            var modules = await GetLockableModulesAsync(cancellationToken);

            return modules.Select(m => new ModuleDefinitionDto
            {
                Id = m.Id,
                ModuleCode = m.ModuleCode,
                ModuleName = m.ModuleName,
                Description = m.Description,
                IsActive = m.IsActive,
                SortOrder = m.SortOrder
            }).ToList();
        }

        private async Task<ModuleDefinition> GetLockableModuleAsync(
            string moduleCode,
            CancellationToken cancellationToken)
        {
            var modules = await GetLockableModulesAsync(cancellationToken);
            return modules.FirstOrDefault(item =>
                item.ModuleCode.Equals(moduleCode?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Module '{moduleCode}' is not enabled and integrated with Finance for this tenant.");
        }

        private async Task<List<ModuleDefinition>> GetLockableModulesAsync(CancellationToken cancellationToken)
        {
            var enabledTenantModules = (await _unitOfWork.Repository<TenantModule>()
                    .GetQueryable(module => module.TenantId == TenantId && module.Status == ModuleStatus.Enabled)
                    .Select(module => module.ModuleName)
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var modules = await _unitOfWork.Repository<ModuleDefinition>()
                .GetQueryable(module => module.TenantId == TenantId && module.IsActive)
                .OrderBy(module => module.SortOrder)
                .ToListAsync(cancellationToken);

            return modules
                .Where(module => FinanceModuleLockCatalog.IsEnabledForTenant(module.ModuleCode, enabledTenantModules))
                .ToList();
        }

        private async Task<T> ExecuteCloseTemplateControlAsync<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken)
        {
            if (_unitOfWork.HasActiveTransaction)
                return await operation();

            // SQL Server retry strategies cannot execute inside a transaction created by the
            // caller before the strategy begins. Put transaction creation, application locking,
            // all reads/writes and commit inside one retriable delegate so a transient failure
            // restarts the complete template decision rather than only its last database query.
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
                try
                {
                    // Template version allocation and active-version replacement are tenant-wide
                    // decisions. One logical lock prevents two administrators from producing the same
                    // version number or activating competing close types concurrently.
                    await _unitOfWork.AcquireTransactionLockAsync(
                        $"FIN:CLOSE-TEMPLATE:{TenantId:N}",
                        cancellationToken);
                    var result = await operation();
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    if (_unitOfWork.HasActiveTransaction)
                        await _unitOfWork.RollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
        }

        private async Task EnsureDefaultCloseTemplatesAsync(CancellationToken cancellationToken)
        {
            if (_closeTemplateBaselineSeeder != null)
            {
                // The scoped seeder and unit of work share one DbContext in production. Reusing
                // it here keeps lazy tenant bootstrap aligned with startup and provisioning while
                // preserving a lightweight catalogue fallback for isolated unit tests.
                await _closeTemplateBaselineSeeder.SeedTenantAsync(TenantId, cancellationToken);
                return;
            }

            var existingTypes = (await _unitOfWork.Repository<FinanceCloseTemplate>()
                    .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted)
                    .Select(item => item.CloseType)
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var added = false;
            foreach (var baseline in FinanceCloseTemplateBaselineCatalog.Templates
                .Where(item => !existingTypes.Contains(item.CloseType)))
            {
                var template = new FinanceCloseTemplate
                {
                    TenantId = TenantId,
                    TemplateCode = baseline.TemplateCode,
                    Name = baseline.Name,
                    CloseType = baseline.CloseType,
                    Version = 1,
                    Status = FinanceCloseTemplateStatuses.Approved,
                    IsActive = true,
                    IsSystemDefault = true,
                    Description = FinanceCloseTemplateBaselineCatalog.Description,
                    ApprovedByUserId = CurrentUserId,
                    ApprovedByUserName = FinanceCloseTemplateBaselineCatalog.SystemActorName,
                    ApprovedAt = DateTime.UtcNow,
                    ApprovalDeclaration = $"System-provided TDC Finance close control set v{FinanceCloseTemplateBaselineCatalog.ControlSetVersion}.",
                    CreatedBy = FinanceCloseTemplateBaselineCatalog.SystemActorName,
                    CreatedById = CurrentUserId
                };
                await _unitOfWork.Repository<FinanceCloseTemplate>().AddAsync(template);

                foreach (var definition in FinanceCloseTemplateBaselineCatalog.Tasks)
                {
                    var task = new FinanceCloseTemplateTaskDefinition
                    {
                        TenantId = TenantId,
                        FinanceCloseTemplateId = template.Id,
                        TaskCode = definition.TaskCode,
                        Title = definition.Title,
                        Category = definition.Category,
                        DependsOnTaskCode = definition.DependsOnTaskCode,
                        CheckCode = definition.CheckCode,
                        Sequence = definition.Sequence,
                        IsMandatory = definition.IsMandatory,
                        IsAutomated = definition.IsAutomated,
                        DueDaysAfterPeriodEnd = baseline.DueDaysAfterPeriodEnd,
                        Instructions = definition.Instructions,
                        CreatedBy = FinanceCloseTemplateBaselineCatalog.SystemActorName,
                        CreatedById = CurrentUserId
                    };
                    template.TaskDefinitions.Add(task);
                    await _unitOfWork.Repository<FinanceCloseTemplateTaskDefinition>().AddAsync(task);
                }

                added = true;
            }

            if (added)
            {
                // Defaults are data rather than migration seed rows because tenants can be created
                // after deployment. Lazy creation keeps every tenant complete without cross-tenant
                // seed scripts or a second configuration source.
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task<FinanceCloseTemplate> GetActiveFinanceCloseTemplateAsync(
            string closeType,
            CancellationToken cancellationToken)
        {
            await EnsureDefaultCloseTemplatesAsync(cancellationToken);

            return await _unitOfWork.Repository<FinanceCloseTemplate>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.CloseType == closeType && item.IsActive &&
                    item.Status == FinanceCloseTemplateStatuses.Approved && !item.IsDeleted)
                .Include(item => item.TaskDefinitions)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    $"No approved active {closeType} Finance close template is configured. A Finance administrator must approve one before starting this close cycle.");
        }

        private async Task<string> ResolveFinanceCloseTypeAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var fiscalYear = await _unitOfWork.Repository<FiscalYear>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == period.FiscalYearId);

            if (period.IsYearEnd ||
                (fiscalYear != null &&
                    (period.EndDate.Date == fiscalYear.EndDate.Date ||
                     period.PeriodNumber == fiscalYear.NumberOfPeriods)) ||
                (fiscalYear == null &&
                    ((period.PeriodType == PeriodType.Monthly && period.PeriodNumber is 12 or 13) ||
                     (period.PeriodType == PeriodType.Quarterly && period.PeriodNumber == 4))))
            {
                return FinanceCloseTemplateTypes.YearEnd;
            }

            if (period.PeriodType == PeriodType.Quarterly ||
                (period.PeriodType == PeriodType.Monthly && period.PeriodNumber % 3 == 0))
            {
                return FinanceCloseTemplateTypes.QuarterEnd;
            }

            return FinanceCloseTemplateTypes.MonthEnd;
        }

        private async Task AddCloseTemplateTaskDefinitionsAsync(
            FinanceCloseTemplate template,
            IEnumerable<SaveFinanceCloseTemplateTaskDto> tasks)
        {
            foreach (var input in tasks.OrderBy(item => item.Sequence))
            {
                var definition = new FinanceCloseTemplateTaskDefinition
                {
                    TenantId = TenantId,
                    FinanceCloseTemplateId = template.Id,
                    TaskCode = input.TaskCode,
                    Title = input.Title,
                    Category = input.Category,
                    DependsOnTaskCode = input.DependsOnTaskCode,
                    CheckCode = input.CheckCode,
                    Sequence = input.Sequence,
                    IsMandatory = input.IsMandatory,
                    IsAutomated = input.IsAutomated,
                    DueDaysAfterPeriodEnd = input.DueDaysAfterPeriodEnd,
                    DefaultAssigneeUserId = input.DefaultAssigneeUserId,
                    Instructions = input.Instructions,
                    CreatedBy = UserName,
                    CreatedById = CurrentUserId
                };
                template.TaskDefinitions.Add(definition);
                await _unitOfWork.Repository<FinanceCloseTemplateTaskDefinition>().AddAsync(definition);
            }
        }

        private static void ValidateAndNormalizeCloseTemplateRequest(
            SaveFinanceCloseTemplateVersionDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            request.TemplateCode = (request.TemplateCode ?? string.Empty).Trim().ToUpperInvariant();
            request.Name = (request.Name ?? string.Empty).Trim();
            request.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            request.CloseType = NormalizeFinanceCloseType(request.CloseType);

            if (request.TemplateCode.Length is < 3 or > 40 ||
                request.TemplateCode.Any(character =>
                    !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            {
                throw new InvalidOperationException("Template code must contain 3-40 letters, numbers, hyphens or underscores.");
            }
            if (request.Name.Length is < 3 or > 160)
                throw new InvalidOperationException("Template name must contain 3-160 characters.");
            if (request.Tasks == null || request.Tasks.Count == 0)
                throw new InvalidOperationException("A Finance close template requires task definitions.");
            if (request.Tasks.Count > 100)
                throw new InvalidOperationException("A Finance close template cannot contain more than 100 tasks.");

            foreach (var task in request.Tasks)
            {
                task.TaskCode = (task.TaskCode ?? string.Empty).Trim().ToUpperInvariant();
                task.Title = (task.Title ?? string.Empty).Trim();
                task.Category = (task.Category ?? string.Empty).Trim();
                task.CheckCode = string.IsNullOrWhiteSpace(task.CheckCode)
                    ? null
                    : task.CheckCode.Trim().ToUpperInvariant();
                task.DependsOnTaskCode = string.IsNullOrWhiteSpace(task.DependsOnTaskCode)
                    ? null
                    : task.DependsOnTaskCode.Trim().ToUpperInvariant();
                task.Instructions = string.IsNullOrWhiteSpace(task.Instructions)
                    ? null
                    : task.Instructions.Trim();

                if (task.TaskCode.Length is < 3 or > 60 ||
                    task.TaskCode.Any(character =>
                        !(char.IsLetterOrDigit(character) || character is '-' or '_')))
                    throw new InvalidOperationException($"Task code '{task.TaskCode}' is invalid.");
                if (task.Title.Length is < 3 or > 200)
                    throw new InvalidOperationException($"Task '{task.TaskCode}' requires a title of 3-200 characters.");
                if (task.Category.Length is < 2 or > 50)
                    throw new InvalidOperationException($"Task '{task.TaskCode}' requires a category of 2-50 characters.");
                if (task.Sequence is < 1 or > 10000)
                    throw new InvalidOperationException($"Task '{task.TaskCode}' has an invalid sequence.");
                if (task.DueDaysAfterPeriodEnd is < -30 or > 120)
                    throw new InvalidOperationException($"Task '{task.TaskCode}' due offset must be between -30 and 120 days.");
                if (task.IsAutomated && string.IsNullOrWhiteSpace(task.CheckCode))
                    throw new InvalidOperationException($"Automated task '{task.TaskCode}' requires a supported check code.");
                if (!task.IsAutomated && task.CheckCode != null)
                    throw new InvalidOperationException($"Manual task '{task.TaskCode}' cannot declare an automated check code.");
                if (task.CheckCode != null && !SupportedAutomatedCloseCheckCodes.Contains(task.CheckCode))
                    throw new InvalidOperationException($"Task '{task.TaskCode}' uses unsupported close check '{task.CheckCode}'.");
                if (task.CheckCode != null && NonWaivableCloseCheckCodes.Contains(task.CheckCode) && !task.IsMandatory)
                    throw new InvalidOperationException($"Close check '{task.CheckCode}' is a non-waivable mandatory control.");
            }

            var duplicateCode = request.Tasks
                .GroupBy(item => item.TaskCode, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateCode != null)
                throw new InvalidOperationException($"Task code '{duplicateCode.Key}' is duplicated.");
            var duplicateSequence = request.Tasks.GroupBy(item => item.Sequence).FirstOrDefault(group => group.Count() > 1);
            if (duplicateSequence != null)
                throw new InvalidOperationException($"Task sequence '{duplicateSequence.Key}' is duplicated.");

            foreach (var checkCode in SupportedAutomatedCloseCheckCodes)
            {
                if (request.Tasks.Count(item => string.Equals(item.CheckCode, checkCode, StringComparison.OrdinalIgnoreCase)) != 1)
                    throw new InvalidOperationException($"Approved templates require exactly one '{checkCode}' automated task.");
            }

            var certification = request.Tasks.SingleOrDefault(item => item.TaskCode == "PREPARER_CERTIFICATION");
            if (certification == null || certification.IsAutomated || !certification.IsMandatory)
                throw new InvalidOperationException("Templates require one mandatory manual PREPARER_CERTIFICATION task.");

            var codes = request.Tasks.Select(item => item.TaskCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var task in request.Tasks.Where(item => item.DependsOnTaskCode != null))
            {
                if (task.TaskCode == task.DependsOnTaskCode)
                    throw new InvalidOperationException($"Task '{task.TaskCode}' cannot depend on itself.");
                if (!codes.Contains(task.DependsOnTaskCode!))
                    throw new InvalidOperationException($"Task '{task.TaskCode}' depends on missing task '{task.DependsOnTaskCode}'.");
            }

            // Detect dependency cycles before approval. A cyclic close checklist would leave TDC
            // with tasks that can never be completed and no defensible route to certification.
            var dependencies = request.Tasks.ToDictionary(
                item => item.TaskCode,
                item => item.DependsOnTaskCode,
                StringComparer.OrdinalIgnoreCase);
            var visitState = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in dependencies.Keys)
                VisitCloseTaskDependency(code, dependencies, visitState);
        }

        private static void VisitCloseTaskDependency(
            string taskCode,
            IReadOnlyDictionary<string, string?> dependencies,
            IDictionary<string, int> visitState)
        {
            if (visitState.TryGetValue(taskCode, out var state))
            {
                if (state == 1)
                    throw new InvalidOperationException($"Close-template task dependency cycle includes '{taskCode}'.");
                if (state == 2)
                    return;
            }

            visitState[taskCode] = 1;
            var dependency = dependencies[taskCode];
            if (dependency != null)
                VisitCloseTaskDependency(dependency, dependencies, visitState);
            visitState[taskCode] = 2;
        }

        private static string NormalizeFinanceCloseType(string? closeType)
            => closeType?.Trim().ToUpperInvariant() switch
            {
                "MONTHEND" => FinanceCloseTemplateTypes.MonthEnd,
                "QUARTEREND" => FinanceCloseTemplateTypes.QuarterEnd,
                "YEAREND" => FinanceCloseTemplateTypes.YearEnd,
                _ => throw new InvalidOperationException("Close type must be MonthEnd, QuarterEnd or YearEnd.")
            };

        private async Task<T> ExecutePeriodCloseControlAsync<T>(
            Guid periodId,
            Func<Task<T>> operation,
            CancellationToken cancellationToken)
        {
            if (_unitOfWork.HasActiveTransaction)
                return await operation();

            // Keep the user transaction inside EF's retry strategy. Creating it outside produces
            // SqlServerRetryingExecutionStrategy's "does not support user-initiated transactions"
            // failure on the first close-workspace query (Issue #32). Retrying the entire unit also
            // preserves the invariant that lock acquisition and the resulting close decision are
            // committed together; neither operation is safe to retry independently.
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
                try
                {
                    // The SQL Server application lock serializes evaluation, certification, close and
                    // reopen decisions for this tenant/period. Serializable isolation alone cannot
                    // lock a cycle row before cycle one exists, so both controls are intentional.
                    await _unitOfWork.AcquireTransactionLockAsync(
                        $"FIN:CLOSE:{TenantId:N}:{periodId:N}",
                        cancellationToken);
                    var result = await operation();
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    if (_unitOfWork.HasActiveTransaction)
                        await _unitOfWork.RollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
        }

        private async Task<FinanceCloseCycle> GetOrCreateActiveCloseCycleAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var cycleRepository = _unitOfWork.Repository<FinanceCloseCycle>();
            var latestCycle = await cycleRepository
                .GetQueryable(item => item.TenantId == TenantId && item.FiscalPeriodId == period.Id)
                .Include(item => item.Tasks)
                .OrderByDescending(item => item.CycleNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestCycle != null &&
                latestCycle.Status is FinanceCloseStatuses.InProgress or FinanceCloseStatuses.Prepared)
            {
                return latestCycle;
            }

            var now = DateTime.UtcNow;
            var closeType = await ResolveFinanceCloseTypeAsync(period, cancellationToken);
            var template = await GetActiveFinanceCloseTemplateAsync(closeType, cancellationToken);
            var cycle = new FinanceCloseCycle
            {
                TenantId = TenantId,
                FiscalPeriodId = period.Id,
                CycleNumber = (latestCycle?.CycleNumber ?? 0) + 1,
                FinanceCloseTemplateId = template.Id,
                TemplateCode = template.TemplateCode,
                CloseType = template.CloseType,
                TemplateVersion = template.Version,
                Status = FinanceCloseStatuses.InProgress,
                StartedAt = now,
                StartedByUserId = CurrentUserId,
                StartedByUserName = UserName,
                CreatedBy = UserName,
                CreatedById = CurrentUserId
            };
            await cycleRepository.AddAsync(cycle);

            foreach (var definition in template.TaskDefinitions.OrderBy(item => item.Sequence))
            {
                var task = new FinanceCloseTask
                {
                    TenantId = TenantId,
                    FinanceCloseCycleId = cycle.Id,
                    TaskCode = definition.TaskCode,
                    Title = definition.Title,
                    Category = definition.Category,
                    DependsOnTaskCode = definition.DependsOnTaskCode,
                    CheckCode = definition.CheckCode,
                    Sequence = definition.Sequence,
                    IsMandatory = definition.IsMandatory,
                    IsAutomated = definition.IsAutomated,
                    Status = FinanceCloseTaskStatuses.Pending,
                    // The due date is copied from the approved definition. It never shifts when a
                    // later template version changes TDC's close timetable.
                    DueAt = period.EndDate.Date.AddDays(definition.DueDaysAfterPeriodEnd),
                    AssignedToUserId = definition.DefaultAssigneeUserId ??
                        (definition.IsAutomated ? null : CurrentUserId),
                    AssignedToUserName = !definition.IsAutomated &&
                        (!definition.DefaultAssigneeUserId.HasValue || definition.DefaultAssigneeUserId == CurrentUserId)
                            ? UserName
                            : null,
                    CreatedBy = UserName,
                    CreatedById = CurrentUserId
                };
                cycle.Tasks.Add(task);
                await _unitOfWork.Repository<FinanceCloseTask>().AddAsync(task);
            }

            period.IsCloseInitiated = true;
            period.CloseInitiatedDate = now;
            period.CloseInitiatedByUserId = CurrentUserId;
            period.UpdatedAt = now;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return cycle;
        }

        private async Task<FinanceCloseWorkspaceDto> LoadCloseWorkspaceDtoAsync(
            Guid cycleId,
            CancellationToken cancellationToken)
        {
            var cycle = await _unitOfWork.Repository<FinanceCloseCycle>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == cycleId)
                .Include(item => item.FiscalPeriod)
                .Include(item => item.FinanceCloseTemplate)
                .Include(item => item.Tasks)
                .Include(item => item.CheckSnapshots)
                .Include(item => item.Certifications)
                .Include(item => item.EvidenceAttachments)
                    .ThenInclude(item => item.FileUploadRecord)
                .Include(item => item.ExceptionWaivers)
                .Include(item => item.AlertDeliveries)
                .FirstAsync(cancellationToken);

            var latestChecks = cycle.CheckSnapshots
                .Where(item => item.EvaluationNumber == cycle.EvaluationCount)
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Title)
                .ToList();
            var activeCertification = cycle.Certifications
                .Where(item => !item.IsSuperseded)
                .OrderByDescending(item => item.PreparedAt)
                .FirstOrDefault();
            var automatedMandatoryBlockerCount = latestChecks.Count(item =>
                item.Severity == FinanceCloseCheckSeverities.Mandatory &&
                item.Status == FinanceCloseCheckStatuses.Failed);
            var unresolvedManualMandatoryCount = cycle.Tasks.Count(item =>
                !item.IsAutomated &&
                item.IsMandatory &&
                item.TaskCode != "PREPARER_CERTIFICATION" &&
                item.Status != FinanceCloseTaskStatuses.Completed);
            var mandatoryBlockerCount = automatedMandatoryBlockerCount + unresolvedManualMandatoryCount;
            var warningCount = latestChecks.Count(item => item.Status == FinanceCloseCheckStatuses.Warning) +
                cycle.Tasks.Count(item =>
                    !item.IsAutomated && !item.IsMandatory &&
                    item.Status != FinanceCloseTaskStatuses.Completed);

            var evidenceByTask = new Dictionary<Guid, List<FinanceCloseEvidenceAttachmentDto>>();
            foreach (var attachment in cycle.EvidenceAttachments
                         .Where(item => !item.IsDeleted)
                         .OrderByDescending(item => item.CreatedAt))
            {
                var fileUrl = attachment.FileUploadRecord.FilePath;
                if (_fileStorageService != null)
                {
                    try
                    {
                        fileUrl = await _fileStorageService.GetPublicUrlAsync(attachment.FileUploadRecord.FilePath);
                    }
                    catch (Exception exception)
                    {
                        // A temporary URL failure must not hide the retained accounting evidence
                        // metadata or make the close workspace itself unavailable.
                        _logger.LogWarning(exception,
                            "Could not resolve public URL for Finance close attachment {AttachmentId}.",
                            attachment.Id);
                    }
                }

                if (!evidenceByTask.TryGetValue(attachment.FinanceCloseTaskId, out var taskEvidence))
                {
                    taskEvidence = new List<FinanceCloseEvidenceAttachmentDto>();
                    evidenceByTask[attachment.FinanceCloseTaskId] = taskEvidence;
                }
                taskEvidence.Add(MapFinanceCloseEvidenceAttachmentToDto(attachment, fileUrl));
            }

            var history = await _unitOfWork.Repository<FinanceCloseCycle>()
                .GetQueryable(item => item.TenantId == TenantId && item.FiscalPeriodId == cycle.FiscalPeriodId)
                .OrderByDescending(item => item.CycleNumber)
                .Select(item => new FinanceCloseCycleHistoryDto
                {
                    CycleId = item.Id,
                    CycleNumber = item.CycleNumber,
                    Status = item.Status,
                    StartedAt = item.StartedAt,
                    PreparedAt = item.PreparedAt,
                    ClosedAt = item.ClosedAt,
                    ReopenedAt = item.ReopenedAt,
                    ReopenReason = item.ReopenReason
                })
                .ToListAsync(cancellationToken);

            return new FinanceCloseWorkspaceDto
            {
                CycleId = cycle.Id,
                FiscalPeriodId = cycle.FiscalPeriodId,
                PeriodName = cycle.FiscalPeriod.PeriodName,
                CycleNumber = cycle.CycleNumber,
                TemplateVersion = cycle.TemplateVersion,
                TemplateCode = cycle.TemplateCode,
                CloseType = cycle.CloseType,
                TemplateName = cycle.FinanceCloseTemplate?.Name ?? cycle.TemplateCode,
                Status = cycle.Status,
                EvaluationNumber = cycle.EvaluationCount,
                StartedAt = cycle.StartedAt,
                LastEvaluatedAt = cycle.LastEvaluatedAt,
                MandatoryBlockerCount = mandatoryBlockerCount,
                WarningCount = warningCount,
                CanPrepare = mandatoryBlockerCount == 0 && cycle.Status == FinanceCloseStatuses.InProgress,
                CanApproveAndClose = mandatoryBlockerCount == 0 &&
                    cycle.Status == FinanceCloseStatuses.Prepared &&
                    activeCertification?.PreparedAt != null &&
                    CurrentUserId.HasValue &&
                    activeCertification.PreparedByUserId != CurrentUserId,
                Tasks = cycle.Tasks
                    .OrderBy(item => item.Sequence)
                    .Select(item => new FinanceCloseTaskDto
                    {
                        Id = item.Id,
                        TaskCode = item.TaskCode,
                        Title = item.Title,
                        Category = item.Category,
                        DependsOnTaskCode = item.DependsOnTaskCode,
                        CheckCode = item.CheckCode,
                        Sequence = item.Sequence,
                        IsMandatory = item.IsMandatory,
                        IsAutomated = item.IsAutomated,
                        Status = item.Status,
                        AssignedToUserId = item.AssignedToUserId,
                        AssignedToUserName = item.AssignedToUserName,
                        DueAt = item.DueAt,
                        IsOverdue = item.DueAt.HasValue && item.DueAt < DateTime.UtcNow &&
                            item.Status != FinanceCloseTaskStatuses.Completed,
                        CompletedAt = item.CompletedAt,
                        CompletedByUserName = item.CompletedByUserName,
                        EvidenceSummary = item.EvidenceSummary,
                        EvidenceAttachments = evidenceByTask.TryGetValue(item.Id, out var taskEvidence)
                            ? taskEvidence
                            : new List<FinanceCloseEvidenceAttachmentDto>()
                    }).ToList(),
                Checks = latestChecks.Select(item => new FinanceCloseCheckSnapshotDto
                {
                    Id = item.Id,
                    EvaluationNumber = item.EvaluationNumber,
                    CheckCode = item.CheckCode,
                    Title = item.Title,
                    Category = item.Category,
                    Severity = item.Severity,
                    Status = item.Status,
                    ResultSummary = item.ResultSummary,
                    ExceptionCount = item.ExceptionCount,
                    ExceptionAmount = item.ExceptionAmount,
                    EvaluatedAt = item.EvaluatedAt,
                    EvidenceFingerprint = item.EvidenceFingerprint,
                    AppliedWaiverId = item.AppliedWaiverId,
                    IsWaivable = !NonWaivableCloseCheckCodes.Contains(item.CheckCode) &&
                        item.Status is FinanceCloseCheckStatuses.Failed or FinanceCloseCheckStatuses.Warning
                }).ToList(),
                ExceptionWaivers = cycle.ExceptionWaivers
                    .Where(item => !item.IsDeleted)
                    .OrderByDescending(item => item.RequestedAt)
                    .Select(item => new FinanceCloseExceptionWaiverDto
                    {
                        Id = item.Id,
                        FinanceCloseTaskId = item.FinanceCloseTaskId,
                        FinanceCloseCheckSnapshotId = item.FinanceCloseCheckSnapshotId,
                        FinanceCloseEvidenceAttachmentId = item.FinanceCloseEvidenceAttachmentId,
                        CheckCode = item.CheckCode,
                        EvidenceFingerprint = item.EvidenceFingerprint,
                        Status = item.Status,
                        Justification = item.Justification,
                        RequestedByUserId = item.RequestedByUserId,
                        RequestedByUserName = item.RequestedByUserName,
                        RequestedAt = item.RequestedAt,
                        ReviewedByUserId = item.ReviewedByUserId,
                        ReviewedByUserName = item.ReviewedByUserName,
                        ReviewedAt = item.ReviewedAt,
                        ReviewComment = item.ReviewComment,
                        MatchesLatestEvidence = latestChecks.Any(check =>
                            check.CheckCode == item.CheckCode &&
                            check.EvidenceFingerprint == item.EvidenceFingerprint)
                    }).ToList(),
                AlertDeliveries = cycle.AlertDeliveries
                    .Where(item => !item.IsDeleted)
                    .OrderByDescending(item => item.DeliveredAtUtc ?? item.LastAttemptAtUtc ?? item.CreatedAt)
                    .Select(item => new FinanceCloseAlertDeliveryDto
                    {
                        Id = item.Id,
                        FinanceCloseTaskId = item.FinanceCloseTaskId,
                        FinanceCloseExceptionWaiverId = item.FinanceCloseExceptionWaiverId,
                        AlertType = item.AlertType,
                        RecipientUserId = item.RecipientUserId,
                        RecipientUserName = item.RecipientUserName,
                        Status = item.Status,
                        DueAtUtc = item.DueAtUtc,
                        DeliveredAtUtc = item.DeliveredAtUtc,
                        AttemptCount = item.AttemptCount,
                        LastError = item.LastError
                    }).ToList(),
                Certification = activeCertification == null ? null : new FinanceCloseCertificationDto
                {
                    Id = activeCertification.Id,
                    PreparedByUserName = activeCertification.PreparedByUserName,
                    PreparedAt = activeCertification.PreparedAt,
                    PreparerDeclaration = activeCertification.PreparerDeclaration,
                    ReviewedByUserName = activeCertification.ReviewedByUserName,
                    ReviewedAt = activeCertification.ReviewedAt,
                    ReviewerDeclaration = activeCertification.ReviewerDeclaration,
                    ApprovedByUserName = activeCertification.ApprovedByUserName,
                    ApprovedAt = activeCertification.ApprovedAt,
                    IsSuperseded = activeCertification.IsSuperseded
                },
                History = history
            };
        }

        private static FinanceCloseEvidenceAttachmentDto MapFinanceCloseEvidenceAttachmentToDto(
            FinanceCloseEvidenceAttachment attachment,
            string fileUrl)
        {
            return new FinanceCloseEvidenceAttachmentDto
            {
                Id = attachment.Id,
                FinanceCloseTaskId = attachment.FinanceCloseTaskId,
                FileUploadRecordId = attachment.FileUploadRecordId,
                EvidenceType = attachment.EvidenceType,
                Description = attachment.Description,
                OriginalFileName = attachment.FileUploadRecord.OriginalFileName,
                ContentType = attachment.FileUploadRecord.ContentType,
                FileSize = attachment.FileUploadRecord.FileSize,
                FileUrl = fileUrl,
                UploadedByUserName = attachment.CreatedBy,
                UploadedAt = attachment.CreatedAt
            };
        }

        private async Task<List<CloseCheckResult>> BuildStructuredCloseChecksAsync(
            FiscalPeriod period,
            PeriodCloseValidationDto validation,
            CancellationToken cancellationToken)
        {
            var trialBalanceErrors = validation.ValidationErrors
                .Where(message => message.Contains("trial balance", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("unbalanced", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var bankErrors = validation.ValidationErrors
                .Where(message => message.Contains("bank reconciliation", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("cash or bank transactions", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var depreciationErrors = validation.ValidationErrors
                .Where(message => message.Contains("depreciation", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var fxErrors = validation.ValidationErrors
                .Where(message => message.Contains("foreign-currency", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("revaluation", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var postingErrors = validation.ValidationErrors
                .Except(trialBalanceErrors)
                .Except(bankErrors)
                .Except(depreciationErrors)
                .Except(fxErrors)
                .ToList();

            var bankControl = await BuildBankReconciliationCheckAsync(period, cancellationToken);
            if (bankControl.Status == FinanceCloseCheckStatuses.Failed)
                bankErrors.Add(bankControl.ResultSummary);

            var depreciationControl = await BuildDepreciationCheckAsync(period, cancellationToken);
            var fxControl = await BuildFxRevaluationCheckAsync(period, cancellationToken);
            var subledgerControls = await BuildSubledgerCloseChecksAsync(period, cancellationToken);
            var recurringJournalControl = await BuildRecurringJournalExceptionCheckAsync(period, cancellationToken);
            var budgetAdoptionControl = await BuildBudgetAdoptionReviewCheckAsync(period, cancellationToken);
            var failedPostingEvents = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.PostingDate >= period.StartDate.Date &&
                    item.PostingDate < period.EndDate.Date.AddDays(1) &&
                    item.PostingStatus == "Failed")
                .CountAsync(cancellationToken);
            if (failedPostingEvents > 0 && !postingErrors.Any(message =>
                message.Contains("posting events failed", StringComparison.OrdinalIgnoreCase)))
                postingErrors.Add($"{failedPostingEvents} Finance posting events failed in this period and require resolution.");

            var posting = BuildMessageCheck(
                "POSTING_INTEGRITY",
                "General ledger and posting integrity",
                "General Ledger",
                postingErrors,
                "All journals, source documents and posting-event links passed integrity checks.");
            var trialBalance = BuildMessageCheck(
                "TRIAL_BALANCE",
                "Trial balance and posted journal balance",
                "General Ledger",
                trialBalanceErrors,
                $"Trial balance is balanced at {validation.TotalDebits:N2} debits and {validation.TotalCredits:N2} credits.",
                Math.Abs(validation.Difference));
            var bank = bankErrors.Count == 0
                ? bankControl
                : bankControl with
                {
                    Status = FinanceCloseCheckStatuses.Failed,
                    ResultSummary = string.Join(" ", bankErrors.Distinct()),
                    ExceptionCount = Math.Max(bankControl.ExceptionCount, bankErrors.Count),
                    Evidence = new { Messages = bankErrors.Distinct().ToList(), bankControl.Evidence }
                };

            return new List<CloseCheckResult>
            {
                posting,
                trialBalance,
                recurringJournalControl,
                subledgerControls.ApControl,
                subledgerControls.ArControl,
                subledgerControls.ApUnapplied,
                subledgerControls.ArUnapplied,
                bank,
                budgetAdoptionControl,
                depreciationControl,
                fxControl
            };
        }

        private async Task<CloseCheckResult> BuildRecurringJournalExceptionCheckAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var periodStart = DateOnly.FromDateTime(period.StartDate);
            var periodEnd = DateOnly.FromDateTime(period.EndDate);
            var exceptionStatuses = new[]
            {
                RecurringJournalOccurrenceStatus.Due,
                RecurringJournalOccurrenceStatus.Generating,
                RecurringJournalOccurrenceStatus.SubmissionFailed,
                RecurringJournalOccurrenceStatus.Failed,
                RecurringJournalOccurrenceStatus.WaiverPending
            };

            // Occurrence status is the authoritative generation lifecycle. PendingApproval and
            // Approved are deliberately excluded: generation succeeded and the established
            // journal approval/posting validations own those later stages. Waived and Superseded
            // are also resolved outcomes and must not be re-opened by the close workspace.
            var occurrenceExceptions = await _unitOfWork.Repository<RecurringJournalOccurrence>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.EffectiveDate >= periodStart && item.EffectiveDate <= periodEnd &&
                    exceptionStatuses.Contains(item.Status))
                .Include(item => item.Template)
                .OrderBy(item => item.EffectiveDate)
                .ThenBy(item => item.Template.TemplateNumber)
                .ToListAsync(cancellationToken);

            // A still-active template whose next due date has not advanced past period end may
            // have failed before an occurrence row was created. Checking the schedule cursor
            // closes that omission gap without introducing a second scheduler or tracker.
            var overdueTemplates = await _unitOfWork.Repository<RecurringJournalTemplate>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.Status == RecurringJournalStatus.Active && item.NextDueDate.HasValue &&
                    item.NextDueDate.Value <= periodEnd && item.EffectiveFrom <= periodEnd &&
                    (!item.EndDate.HasValue || item.EndDate.Value >= periodStart))
                .OrderBy(item => item.NextDueDate)
                .ThenBy(item => item.TemplateNumber)
                .ToListAsync(cancellationToken);

            // Once the occurrence has posted, a later template pause, completion,
            // cancellation or version change cannot suppress its authorised reversal.
            var reversalExceptions = await _unitOfWork.Repository<RecurringJournalOccurrence>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.Status == RecurringJournalOccurrenceStatus.Posted &&
                    item.JournalEntryId.HasValue && item.ReversalDueDate.HasValue &&
                    item.ReversalDueDate.Value <= periodEnd && item.ReversalAuthorizedAt.HasValue &&
                    !item.ReversalJournalEntryId.HasValue && !item.ReversedAt.HasValue &&
                    !item.WaivedAt.HasValue &&
                    (item.ReversalStatus == RecurringJournalReversalStatus.Scheduled ||
                     item.ReversalStatus == RecurringJournalReversalStatus.Processing ||
                     item.ReversalStatus == RecurringJournalReversalStatus.Failed))
                .Include(item => item.Template)
                .OrderBy(item => item.ReversalDueDate)
                .ThenBy(item => item.Template.TemplateNumber)
                .ToListAsync(cancellationToken);

            var exceptionCount = occurrenceExceptions.Count + overdueTemplates.Count + reversalExceptions.Count;
            if (exceptionCount == 0)
            {
                return new CloseCheckResult(
                    "RECURRING_JOURNAL_EXCEPTIONS",
                    "Resolve recurring-journal generation exceptions",
                    "General Ledger",
                    FinanceCloseCheckSeverities.Mandatory,
                    FinanceCloseCheckStatuses.Passed,
                    "No unresolved recurring-journal generation, schedule, or due automatic-reversal exceptions exist at period end.",
                    0,
                    null,
                    new { OccurrenceExceptions = 0, OverdueTemplates = 0, ReversalExceptions = 0 });
            }

            return new CloseCheckResult(
                "RECURRING_JOURNAL_EXCEPTIONS",
                "Resolve recurring-journal generation exceptions",
                "General Ledger",
                FinanceCloseCheckSeverities.Mandatory,
                FinanceCloseCheckStatuses.Failed,
                $"{occurrenceExceptions.Count} recurring-journal occurrence exception(s), {overdueTemplates.Count} overdue active template schedule(s), and {reversalExceptions.Count} due automatic reversal(s) require resolution before close.",
                exceptionCount,
                null,
                new
                {
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    Occurrences = occurrenceExceptions.Select(item => new
                    {
                        item.Id,
                        item.TemplateId,
                        item.Template.TemplateNumber,
                        TemplateName = item.Template.Name,
                        item.TemplateVersion,
                        item.SequenceNumber,
                        item.ScheduledDate,
                        item.EffectiveDate,
                        Status = item.Status.ToString(),
                        item.AttemptCount,
                        item.ErrorMessage,
                        item.JournalEntryId,
                        item.WorkflowInstanceId
                    }).ToList(),
                    Reversals = reversalExceptions.Select(item => new
                    {
                        item.Id,
                        item.TemplateId,
                        item.Template.TemplateNumber,
                        TemplateName = item.Template.Name,
                        item.ReversalDueDate,
                        Status = item.ReversalStatus.ToString(),
                        item.ReversalAttemptCount,
                        item.ReversalLastAttemptAt,
                        item.ReversalError,
                        item.JournalEntryId,
                        item.ReversalJournalEntryId
                    }).ToList(),
                    OverdueTemplates = overdueTemplates.Select(item => new
                    {
                        item.Id,
                        item.TemplateNumber,
                        item.Name,
                        item.Version,
                        item.NextDueDate,
                        item.LastGeneratedDueDate,
                        item.ConsecutiveFailureCount,
                        item.LastFailure
                    }).ToList()
                });
        }

        private async Task<CloseCheckResult> BuildBudgetAdoptionReviewCheckAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var periodEndExclusive = period.EndDate.Date.AddDays(1);

            // Approval locks a budget for governance review, but adoption is the separate action
            // that makes it the official variance-reporting baseline. An approved inactive
            // scenario can therefore be legitimate; the TDC baseline records a warning for an
            // explicit Finance decision rather than silently adopting it or blocking the ledger.
            var awaitingAdoption = await _unitOfWork.Repository<BudgetScenario>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.FiscalYearId == period.FiscalYearId && item.Status == "Approved" &&
                    !item.IsActive && !item.AdoptedAt.HasValue && item.LockedDate.HasValue &&
                    item.LockedDate.Value < periodEndExclusive)
                .OrderBy(item => item.LockedDate)
                .ThenBy(item => item.Name)
                .ToListAsync(cancellationToken);

            var hasReviewItems = awaitingAdoption.Count > 0;
            return new CloseCheckResult(
                "BUDGET_ADOPTION_REVIEW",
                "Review approved budgets awaiting adoption",
                "Budgeting",
                FinanceCloseCheckSeverities.Warning,
                hasReviewItems ? FinanceCloseCheckStatuses.Warning : FinanceCloseCheckStatuses.Passed,
                hasReviewItems
                    ? $"{awaitingAdoption.Count} approved fiscal-year budget scenario(s) are not adopted as the official reporting baseline; record the adoption or retention decision."
                    : "No approved fiscal-year budget scenarios are awaiting an adoption decision at period end.",
                awaitingAdoption.Count,
                null,
                new
                {
                    period.FiscalYearId,
                    ReviewItems = awaitingAdoption.Select(item => new
                    {
                        item.Id,
                        item.Name,
                        item.Status,
                        item.LockedDate,
                        item.LockedByUserId,
                        item.IsActive,
                        item.AdoptedAt,
                        item.AdoptionEffectiveDate
                    }).ToList()
                });
        }

        private async Task<SubledgerCloseCheckResults> BuildSubledgerCloseChecksAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            // Control reports rebuild the existing settlement read model at period end before
            // comparison. The close workspace therefore reuses the AP/AR reporting foundation
            // and snapshots the same figures Finance users can independently reproduce.
            var apReport = await _settlementReadModelService.GetControlReconciliationAsync(
                SubledgerSettlementModules.AccountsPayable,
                period.EndDate,
                cancellationToken);
            var apUnapplied = await _settlementReadModelService.GetUnappliedBalancesAsync(
                SubledgerSettlementModules.AccountsPayable,
                period.EndDate,
                cancellationToken: cancellationToken);
            var arReport = await _settlementReadModelService.GetControlReconciliationAsync(
                SubledgerSettlementModules.AccountsReceivable,
                period.EndDate,
                cancellationToken);
            var arUnapplied = await _settlementReadModelService.GetUnappliedBalancesAsync(
                SubledgerSettlementModules.AccountsReceivable,
                period.EndDate,
                cancellationToken: cancellationToken);

            return new SubledgerCloseCheckResults(
                BuildSubledgerControlCheck(apReport),
                BuildSubledgerControlCheck(arReport),
                BuildUnappliedBalanceCheck(SubledgerSettlementModules.AccountsPayable, apUnapplied),
                BuildUnappliedBalanceCheck(SubledgerSettlementModules.AccountsReceivable, arUnapplied));
        }

        private static CloseCheckResult BuildSubledgerControlCheck(SubledgerControlReconciliationDto report)
        {
            var isAccountsPayable = report.SourceModule == SubledgerSettlementModules.AccountsPayable;
            var code = isAccountsPayable ? "AP_CONTROL_RECONCILIATION" : "AR_CONTROL_RECONCILIATION";
            var title = isAccountsPayable
                ? "Accounts payable control-account reconciliation"
                : "Accounts receivable control-account reconciliation";
            var category = isAccountsPayable ? "Accounts Payable" : "Accounts Receivable";
            var variance = Math.Abs(report.Variance);
            var hasVariance = variance >= 0.01m;
            var exceptionCount = report.DiagnosticCount + (hasVariance ? 1 : 0);
            var passed = exceptionCount == 0 && report.ControlAccountId.HasValue;

            var summary = passed
                ? $"{report.SourceModule} subledger and control account {report.ControlAccountNumber} agree at {report.ReadModelOutstanding:N2}."
                : $"{report.SourceModule} reconciliation has variance {report.Variance:N2} and {report.DiagnosticCount} diagnostic(s); resolve before close.";

            return new CloseCheckResult(
                code,
                title,
                category,
                FinanceCloseCheckSeverities.Mandatory,
                passed ? FinanceCloseCheckStatuses.Passed : FinanceCloseCheckStatuses.Failed,
                summary,
                Math.Max(exceptionCount, passed ? 0 : 1),
                hasVariance ? variance : null,
                new
                {
                    report.SourceModule,
                    report.AsOfDate,
                    report.ControlAccountId,
                    report.ControlAccountNumber,
                    report.ControlAccountName,
                    report.ReadModelOutstanding,
                    report.PostedGlControlBalance,
                    report.Variance,
                    report.DocumentCount,
                    report.DiagnosticCount,
                    report.Diagnostics
                });
        }

        private static CloseCheckResult BuildUnappliedBalanceCheck(
            string sourceModule,
            IReadOnlyCollection<SubledgerUnappliedSettlementBalance> balances)
        {
            var isAccountsPayable = sourceModule == SubledgerSettlementModules.AccountsPayable;
            var code = isAccountsPayable ? "AP_UNAPPLIED_BALANCES" : "AR_UNAPPLIED_BALANCES";
            var title = isAccountsPayable
                ? "Review unapplied supplier payments and advances"
                : "Review unapplied customer receipts and advances";
            var category = isAccountsPayable ? "Accounts Payable" : "Accounts Receivable";
            var total = balances.Sum(item => item.UnappliedAmount);
            var diagnosticCount = balances.Count(item => item.HasDiagnostics);
            var hasBalances = balances.Count > 0;

            // Unapplied cash can be a valid advance, so TDC's baseline treats its existence as
            // a review warning rather than a close blocker. The complete item-level references
            // remain in immutable evidence for subsequent allocation/refund follow-up.
            return new CloseCheckResult(
                code,
                title,
                category,
                FinanceCloseCheckSeverities.Warning,
                hasBalances ? FinanceCloseCheckStatuses.Warning : FinanceCloseCheckStatuses.Passed,
                hasBalances
                    ? $"{balances.Count} {sourceModule} unapplied balance(s) totalling {total:N2} require Finance review; {diagnosticCount} carry diagnostic flags."
                    : $"No {sourceModule} unapplied payments, receipts or advances exist at period end.",
                balances.Count,
                hasBalances ? total : null,
                new
                {
                    SourceModule = sourceModule,
                    BalanceCount = balances.Count,
                    TotalUnapplied = total,
                    DiagnosticCount = diagnosticCount,
                    ByClassification = balances
                        .GroupBy(item => item.Classification)
                        .Select(group => new
                        {
                            Classification = group.Key,
                            Count = group.Count(),
                            Amount = group.Sum(item => item.UnappliedAmount)
                        })
                        .OrderBy(item => item.Classification)
                        .ToList(),
                    Items = balances.Select(item => new
                    {
                        item.Id,
                        item.CounterpartyId,
                        item.SettlementSourceType,
                        item.SettlementSourceId,
                        item.SettlementSourceNumber,
                        item.Classification,
                        item.SettlementDate,
                        item.DocumentCurrencyCode,
                        item.UnappliedAmount,
                        item.HasDiagnostics,
                        item.DiagnosticFlags
                    }).ToList()
                });
        }

        private async Task<CloseCheckResult> BuildBankReconciliationCheckAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var start = period.StartDate.Date;
            var end = period.EndDate.Date.AddDays(1);
            var postedCashCount = await _unitOfWork.Repository<CashTransaction>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted && item.IsPosted &&
                    item.TransactionDate >= start && item.TransactionDate < end)
                .CountAsync(cancellationToken);
            var unreconciledCount = await _unitOfWork.Repository<CashTransaction>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted && item.IsPosted &&
                    !item.IsReconciled && item.TransactionDate >= start && item.TransactionDate < end)
                .CountAsync(cancellationToken);

            if (postedCashCount == 0)
            {
                return new CloseCheckResult(
                    "BANK_RECONCILIATION", "Cash and bank reconciliation", "Cash & Bank",
                    FinanceCloseCheckSeverities.Mandatory, FinanceCloseCheckStatuses.NotApplicable,
                    "No posted cash or bank transactions occurred in this period.", 0, null,
                    new { PostedCashTransactions = 0, UnreconciledTransactions = 0 });
            }

            return unreconciledCount == 0
                ? new CloseCheckResult(
                    "BANK_RECONCILIATION", "Cash and bank reconciliation", "Cash & Bank",
                    FinanceCloseCheckSeverities.Mandatory, FinanceCloseCheckStatuses.Passed,
                    $"All {postedCashCount} posted cash and bank transactions are reconciled.", 0, null,
                    new { PostedCashTransactions = postedCashCount, UnreconciledTransactions = 0 })
                : new CloseCheckResult(
                    "BANK_RECONCILIATION", "Cash and bank reconciliation", "Cash & Bank",
                    FinanceCloseCheckSeverities.Mandatory, FinanceCloseCheckStatuses.Failed,
                    $"{unreconciledCount} posted cash or bank transactions in this period are not reconciled.",
                    unreconciledCount, null,
                    new { PostedCashTransactions = postedCashCount, UnreconciledTransactions = unreconciledCount });
        }

        private async Task<CloseCheckResult> BuildDepreciationCheckAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId);
            var isMandatory = settings?.RequireDepreciationBeforePeriodClose ?? true;
            if (!isMandatory)
            {
                return new CloseCheckResult(
                    "FIXED_ASSET_DEPRECIATION", "Fixed-asset depreciation", "Fixed Assets",
                    FinanceCloseCheckSeverities.Warning, FinanceCloseCheckStatuses.NotApplicable,
                    "Tenant policy does not make depreciation a mandatory period-close blocker.", 0, null,
                    new { RequireDepreciationBeforePeriodClose = false });
            }

            var dueAssetIds = _unitOfWork.Repository<FixedAsset>()
                .GetQueryable(asset => asset.TenantId == TenantId && !asset.IsDeleted &&
                    asset.Status == FixedAssetStatus.Active &&
                    asset.DepreciationMethod != DepreciationMethod.None &&
                    asset.PlacedInServiceDate.HasValue && asset.PlacedInServiceDate.Value <= period.EndDate &&
                    asset.NetBookValue > asset.ResidualValue &&
                    (asset.PostingEventId.HasValue || asset.JournalEntryId.HasValue || asset.CapitalizedAt.HasValue ||
                        asset.BookValues.Any(value => !value.IsDeleted &&
                            (value.CapitalizationPostingEventId.HasValue || value.CapitalizationJournalEntryId.HasValue ||
                                value.OpeningPostedToGl))))
                .Select(asset => asset.Id);
            var dueAssetCount = await dueAssetIds.CountAsync(cancellationToken);
            if (dueAssetCount == 0)
            {
                return new CloseCheckResult(
                    "FIXED_ASSET_DEPRECIATION", "Fixed-asset depreciation", "Fixed Assets",
                    FinanceCloseCheckSeverities.Mandatory, FinanceCloseCheckStatuses.NotApplicable,
                    "No active capitalized assets require depreciation in this period.", 0, null,
                    new { DueAssets = 0, period.DepreciationComplete });
            }

            var postedAssetIds = _unitOfWork.Repository<AssetDepreciationSchedule>()
                .GetQueryable(schedule => schedule.TenantId == TenantId && !schedule.IsDeleted &&
                    schedule.FiscalPeriodId == period.Id && schedule.IsPosted && !schedule.IsProjected && !schedule.IsReversed)
                .Select(schedule => schedule.FixedAssetId);
            var incompleteAssetCount = await dueAssetIds
                .CountAsync(assetId => !postedAssetIds.Contains(assetId), cancellationToken);
            var failedRunCount = await _unitOfWork.Repository<FixedAssetDepreciationRun>()
                .GetQueryable(run => run.TenantId == TenantId && !run.IsDeleted &&
                    run.FiscalPeriodId == period.Id && run.Status == "Failed")
                .CountAsync(cancellationToken);
            var validPostedJournalIds = _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(journal => journal.TenantId == TenantId && !journal.IsDeleted &&
                    journal.FiscalPeriodId == period.Id && journal.PostingStatus == "Posted")
                .Select(journal => journal.Id);
            var validDepreciationPostingEventIds = _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(postingEvent => postingEvent.TenantId == TenantId && !postingEvent.IsDeleted &&
                    postingEvent.PostingStatus == "Posted" && postingEvent.JournalEntryId.HasValue &&
                    validPostedJournalIds.Contains(postingEvent.JournalEntryId.Value))
                .Select(postingEvent => postingEvent.Id);
            var invalidPostingEvidenceCount = await _unitOfWork.Repository<AssetDepreciationSchedule>()
                .GetQueryable(schedule => schedule.TenantId == TenantId && !schedule.IsDeleted &&
                    schedule.FiscalPeriodId == period.Id && schedule.IsPosted && !schedule.IsProjected && !schedule.IsReversed &&
                    (!schedule.JournalEntryId.HasValue || !schedule.PostingEventId.HasValue ||
                        !validPostedJournalIds.Contains(schedule.JournalEntryId.Value) ||
                        !validDepreciationPostingEventIds.Contains(schedule.PostingEventId.Value)))
                .CountAsync(cancellationToken);
            var exceptions = incompleteAssetCount + failedRunCount + invalidPostingEvidenceCount;

            return exceptions == 0
                ? new CloseCheckResult(
                    "FIXED_ASSET_DEPRECIATION", "Fixed-asset depreciation", "Fixed Assets",
                    FinanceCloseCheckSeverities.Mandatory, FinanceCloseCheckStatuses.Passed,
                    $"Depreciation is posted for all {dueAssetCount} due fixed assets.", 0, null,
                    new
                    {
                        DueAssets = dueAssetCount,
                        IncompleteAssets = 0,
                        FailedRuns = 0,
                        InvalidPostingEvidence = 0,
                        period.DepreciationComplete
                    })
                : new CloseCheckResult(
                    "FIXED_ASSET_DEPRECIATION", "Fixed-asset depreciation", "Fixed Assets",
                    FinanceCloseCheckSeverities.Mandatory, FinanceCloseCheckStatuses.Failed,
                    $"Depreciation is incomplete for {incompleteAssetCount} due assets, {failedRunCount} runs failed, and {invalidPostingEvidenceCount} posted schedules have invalid journal/posting-event evidence.",
                    exceptions, null,
                    new
                    {
                        DueAssets = dueAssetCount,
                        IncompleteAssets = incompleteAssetCount,
                        FailedRuns = failedRunCount,
                        InvalidPostingEvidence = invalidPostingEvidenceCount,
                        period.DepreciationComplete
                    });
        }

        private async Task<CloseCheckResult> BuildFxRevaluationCheckAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var hasForeignCurrencyActivity = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.PostingStatus == "Posted" && item.HasForeignCurrencyLines &&
                    item.PostingDate >= period.StartDate.Date &&
                    item.PostingDate < period.EndDate.Date.AddDays(1))
                .AnyAsync(cancellationToken);

            var postedBatches = await _unitOfWork.Repository<FxRevaluationBatch>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.FiscalPeriodId == period.Id && item.Status == "Posted" &&
                    !item.ReversalPostingEventId.HasValue)
                .OrderBy(item => item.AccountingBookCode)
                .ThenBy(item => item.RevaluationDate)
                .Select(item => new
                {
                    item.Id,
                    item.BatchNumber,
                    item.AccountingBookId,
                    item.AccountingBookCode,
                    item.RevaluationDate,
                    item.PreviewFingerprint,
                    item.PostingEventId,
                    item.JournalEntryId
                })
                .ToListAsync(cancellationToken);
            var postedBatchIds = postedBatches.Select(item => item.Id).ToArray();
            var nonstandardEvidence = await _unitOfWork.Repository<FxRevaluationLine>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                    postedBatchIds.Contains(item.FxRevaluationBatchId) && item.HasGovernanceWarning)
                .OrderBy(item => item.AccountClassificationCode)
                .ThenBy(item => item.AccountId)
                .ThenBy(item => item.TransactionCurrency)
                .Select(item => new
                {
                    item.FxRevaluationBatchId,
                    item.AccountId,
                    item.AccountClassificationId,
                    item.AccountClassificationCode,
                    item.AccountClassificationName,
                    item.CoreAccountType,
                    item.TransactionCurrency,
                    item.EffectivePolicySource,
                    item.GovernanceWarning
                })
                .ToListAsync(cancellationToken);

            if (!hasForeignCurrencyActivity)
            {
                return new CloseCheckResult(
                    "FX_REVALUATION", "Foreign-currency revaluation", "Foreign Exchange",
                    FinanceCloseCheckSeverities.Mandatory, FinanceCloseCheckStatuses.NotApplicable,
                    "No posted foreign-currency activity requires period-end revaluation.", 0, null,
                    new
                    {
                        HasForeignCurrencyActivity = false,
                        PostedBookBatches = postedBatches,
                        NonstandardPolicyWarnings = nonstandardEvidence
                    });
            }

            return period.CurrencyRevaluationComplete
                ? new CloseCheckResult(
                    "FX_REVALUATION", "Foreign-currency revaluation", "Foreign Exchange",
                    FinanceCloseCheckSeverities.Mandatory, FinanceCloseCheckStatuses.Passed,
                    "Foreign-currency revaluation is marked complete for this period.", 0, null,
                    new
                    {
                        HasForeignCurrencyActivity = true,
                        period.CurrencyRevaluationComplete,
                        period.CurrencyRevaluationDate,
                        PostedBookBatches = postedBatches,
                        NonstandardPolicyWarnings = nonstandardEvidence
                    })
                : new CloseCheckResult(
                    "FX_REVALUATION", "Foreign-currency revaluation", "Foreign Exchange",
                    FinanceCloseCheckSeverities.Mandatory, FinanceCloseCheckStatuses.Failed,
                    "Foreign-currency activity exists but period-end revaluation is not complete.", 1, null,
                    new
                    {
                        HasForeignCurrencyActivity = true,
                        period.CurrencyRevaluationComplete,
                        period.CurrencyRevaluationDate,
                        PostedBookBatches = postedBatches,
                        NonstandardPolicyWarnings = nonstandardEvidence
                    });
        }

        private static CloseCheckResult BuildMessageCheck(
            string code,
            string title,
            string category,
            IReadOnlyCollection<string> errors,
            string successSummary,
            decimal? exceptionAmount = null)
        {
            return errors.Count == 0
                ? new CloseCheckResult(
                    code, title, category, FinanceCloseCheckSeverities.Mandatory,
                    FinanceCloseCheckStatuses.Passed, successSummary, 0, null,
                    new { Messages = Array.Empty<string>() })
                : new CloseCheckResult(
                    code, title, category, FinanceCloseCheckSeverities.Mandatory,
                    FinanceCloseCheckStatuses.Failed, string.Join(" ", errors), errors.Count,
                    exceptionAmount, new { Messages = errors });
        }

        private async Task RestoreGlobalLockIfNoModulesOpenAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            if (!period.IsGlobalLockSuspended)
                return;

            var now = DateTime.UtcNow;
            var hasOpenModule = period.PeriodModuleLocks.Any(moduleLock =>
                !moduleLock.IsLocked
                && moduleLock.ReopenExpiresAtUtc.HasValue
                && moduleLock.ReopenExpiresAtUtc > now);
            if (hasOpenModule)
                return;

            period.IsGlobalLockSuspended = false;
            period.IsLocked = true;
            period.IsOpen = false;
            period.IsClosed = true;
            period.PeriodStatus = "Locked";
            period.UpdatedAt = now;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;
            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
        }

        private static object? BuildModuleLockAuditSnapshot(PeriodModuleLock? moduleLock)
        {
            if (moduleLock == null)
                return null;

            return new
            {
                moduleLock.Id,
                moduleLock.TenantId,
                moduleLock.FiscalPeriodId,
                moduleLock.ModuleDefinitionId,
                moduleLock.IsLocked,
                moduleLock.LockedDate,
                moduleLock.LockedByUserId,
                moduleLock.LockReason,
                moduleLock.UnlockedDate,
                moduleLock.UnlockedByUserId,
                moduleLock.UnlockReason,
                moduleLock.ReopenExpiresAtUtc,
                moduleLock.AutoRelockedDate
            };
        }

        public async Task<PeriodCloseValidationDto> ValidatePeriodCloseAsync(Guid periodId, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == periodId);

            if (period == null)
            {
                return new PeriodCloseValidationDto
                {
                    CanClose = false,
                    ValidationErrors = new List<string> { "Period not found." }
                };
            }

            var errors = new List<string>();
            var warnings = new List<string>();

            if (period.PeriodStatus == "Closed")
                errors.Add("Period is already closed.");

            if (period.IsLocked)
                errors.Add("Period is locked.");

            if (!period.IsOpen || !string.Equals(period.PeriodStatus, "Open", StringComparison.OrdinalIgnoreCase))
                errors.Add("Period must be open before it can be closed.");

            var postedJournalsQuery = _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(je => je.TenantId == TenantId
                    && je.FiscalPeriodId == periodId
                    && je.PostingStatus == "Posted"
                    && !je.IsDeleted);

            var unpostedApprovedJournalEntries = await _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(je => je.TenantId == TenantId
                    && je.FiscalPeriodId == periodId
                    && !je.IsDeleted
                    && je.PostingStatus != "Posted"
                    && (je.PostingStatus == "Approved" || je.ApprovalStatus == "Approved"))
                .CountAsync(cancellationToken);

            if (unpostedApprovedJournalEntries > 0)
                errors.Add($"{unpostedApprovedJournalEntries} approved journal entries are not posted.");

            var submittedUnapprovedJournalEntries = await _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(je => je.TenantId == TenantId
                    && je.FiscalPeriodId == periodId
                    && !je.IsDeleted
                    && je.PostingStatus != "Posted"
                    && (je.PostingStatus == "Submitted"
                        || je.ApprovalStatus == "Pending"
                        || je.ApprovalStatus == "PendingApproval"))
                .CountAsync(cancellationToken);

            if (submittedUnapprovedJournalEntries > 0)
                errors.Add($"{submittedUnapprovedJournalEntries} submitted journal entries are still awaiting approval.");

            var futurePeriodsClosed = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId
                    && fp.StartDate > period.EndDate
                    && fp.PeriodStatus == "Closed")
                .AnyAsync(cancellationToken);

            if (futurePeriodsClosed)
                errors.Add("Cannot close period while future periods are closed.");

            var approvedUnpostedSourceDocuments = await CountApprovedUnpostedSourceDocumentsAsync(period, cancellationToken);
            if (approvedUnpostedSourceDocuments > 0)
                errors.Add($"{approvedUnpostedSourceDocuments} approved Finance source documents in this period are not posted.");

            var submittedUnapprovedSourceDocuments = await CountSubmittedUnapprovedSourceDocumentsAsync(period, cancellationToken);
            if (submittedUnapprovedSourceDocuments > 0)
                errors.Add($"{submittedUnapprovedSourceDocuments} submitted Finance source documents in this period still require approval.");

            var unfinalizedReconciliations = await CountUnfinalizedReconciliationsAsync(period, cancellationToken);
            if (unfinalizedReconciliations > 0)
                errors.Add($"{unfinalizedReconciliations} bank reconciliations in this period are not finalized or approved.");

            var orphanedPostingEvents = await CountOrphanedPostingEventsAsync(period, cancellationToken);
            if (orphanedPostingEvents > 0)
                errors.Add($"{orphanedPostingEvents} posted Finance posting events have missing or invalid same-tenant journal references.");

            var postedEventJournalIds = _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(e => e.TenantId == TenantId
                    && !e.IsDeleted
                    && e.PostingStatus == "Posted"
                    && e.JournalEntryId.HasValue)
                .Select(e => e.JournalEntryId!.Value);

            var postedJournalsMissingPostingEvents = await postedJournalsQuery
                .CountAsync(je => !postedEventJournalIds.Contains(je.Id), cancellationToken);

            if (postedJournalsMissingPostingEvents > 0)
                errors.Add($"{postedJournalsMissingPostingEvents} posted journal entries are missing posting events.");

            var postedDocumentsMissingPostingEvents = await CountPostedDocumentsMissingPostingEventsAsync(period, cancellationToken);
            if (postedDocumentsMissingPostingEvents > 0)
                errors.Add($"{postedDocumentsMissingPostingEvents} posted Finance source documents are missing posting events.");

            var postedDocumentJournalTenantMismatches = await CountPostedDocumentJournalTenantMismatchesAsync(period, cancellationToken);
            if (postedDocumentJournalTenantMismatches > 0)
                errors.Add($"{postedDocumentJournalTenantMismatches} posted Finance source documents reference journals outside the tenant.");

            var periodReferenceMismatches = await CountCrossTenantPeriodReferenceMismatchesAsync(period, cancellationToken);
            if (periodReferenceMismatches > 0)
                errors.Add($"{periodReferenceMismatches} ledger records reference this period from another tenant.");

            // TDC-0613: a year-end close with inventory history is not valid until the
            // authoritative movement subledger, landed costs and Inventory control account
            // have been reconciled and independently frozen at the exact period cut-off.
            if (period.IsYearEnd)
            {
                var cutoff = period.EndDate.Date.AddDays(1);
                var hasInventoryHistory = await _unitOfWork.Repository<InventoryMovement>()
                    .GetQueryable(value => value.TenantId == TenantId && value.IsPosted && !value.IsDeleted &&
                                           value.PostingDate < cutoff)
                    .AnyAsync(cancellationToken);
                if (hasInventoryHistory)
                {
                    var inventoryReconciliation = await _unitOfWork
                        .Repository<InventoryValuationReconciliation>()
                        .GetQueryable(value => value.TenantId == TenantId &&
                                               value.FiscalPeriodId == period.Id && !value.IsDeleted)
                        .OrderByDescending(value => value.GeneratedAtUtc)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (inventoryReconciliation is null ||
                        inventoryReconciliation.Status != InventoryValuationReconciliationStatus.Frozen ||
                        inventoryReconciliation.ExceptionCount != 0 ||
                        Math.Abs(inventoryReconciliation.ReconciliationVariance) >
                        inventoryReconciliation.ToleranceAmount ||
                        inventoryReconciliation.CutoffDateUtc < cutoff.AddTicks(-1) ||
                        !inventoryReconciliation.PeriodModuleLockId.HasValue)
                    {
                        errors.Add(
                            "Year-end inventory valuation must have a clean, independently frozen WAC/landed-cost/GL reconciliation at the exact cut-off.");
                    }
                }
            }

            // Validate Trial Balance (Debits = Credits)
            var transactions = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.TenantId == TenantId && t.FiscalPeriodId == periodId &&
                    !t.IsDeleted && t.PostingStatus == "Posted")
                .ToListAsync(cancellationToken);

            decimal totalDebits = transactions.Sum(t => t.DebitAmount);
            decimal totalCredits = transactions.Sum(t => t.CreditAmount);
            decimal difference = totalDebits - totalCredits;
            bool isBalanced = Math.Abs(difference) < 0.01m;

            if (!isBalanced)
            {
                errors.Add($"Trial Balance is not balanced. Difference: {difference}");
            }

            var unbalancedPostedJournals = await postedJournalsQuery
                .CountAsync(je => je.TotalDebitAmount != je.TotalCreditAmount, cancellationToken);

            if (unbalancedPostedJournals > 0)
                errors.Add($"{unbalancedPostedJournals} posted journal entries are unbalanced.");

            // These controls complete the previously transient checklist. They are also exposed
            // through ValidatePeriodCloseAsync so API callers cannot receive a weaker answer than
            // the persisted workspace uses at the actual close boundary.
            var bankControl = await BuildBankReconciliationCheckAsync(period, cancellationToken);
            if (bankControl.Severity == FinanceCloseCheckSeverities.Mandatory &&
                bankControl.Status == FinanceCloseCheckStatuses.Failed)
            {
                errors.Add(bankControl.ResultSummary);
            }

            var depreciationControl = await BuildDepreciationCheckAsync(period, cancellationToken);
            if (depreciationControl.Severity == FinanceCloseCheckSeverities.Mandatory &&
                depreciationControl.Status == FinanceCloseCheckStatuses.Failed)
            {
                errors.Add(depreciationControl.ResultSummary);
            }

            var fxControl = await BuildFxRevaluationCheckAsync(period, cancellationToken);
            if (fxControl.Severity == FinanceCloseCheckSeverities.Mandatory &&
                fxControl.Status == FinanceCloseCheckStatuses.Failed)
            {
                errors.Add(fxControl.ResultSummary);
            }

            var failedPostingEvents = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.PostingStatus == "Failed" &&
                    item.PostingDate >= period.StartDate.Date &&
                    item.PostingDate < period.EndDate.Date.AddDays(1))
                .CountAsync(cancellationToken);
            if (failedPostingEvents > 0)
                errors.Add($"{failedPostingEvents} Finance posting events failed in this period and require resolution.");

            return new PeriodCloseValidationDto
            {
                CanClose = errors.Count == 0,
                ValidationErrors = errors,
                ValidationWarnings = warnings,
                PeriodName = period.PeriodName,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                TotalDebits = totalDebits,
                TotalCredits = totalCredits,
                Difference = difference,
                TotalJournalEntries = await postedJournalsQuery.CountAsync(cancellationToken),
                TotalTransactionLines = transactions.Count
            };
        }

        private async Task<int> CountApprovedUnpostedSourceDocumentsAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var start = period.StartDate.Date;
            var end = period.EndDate.Date.AddDays(1);

            var apInvoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId
                    && !i.IsDeleted
                    && i.InvoiceDate >= start
                    && i.InvoiceDate < end
                    && i.Status == VendorInvoiceStatus.Approved
                    && !i.JournalEntryId.HasValue)
                .CountAsync(cancellationToken);

            var apPayments = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId
                    && !p.IsDeleted
                    && p.PaymentDate >= start
                    && p.PaymentDate < end
                    && p.Status == VendorPaymentStatus.Authorized
                    && !p.JournalEntryId.HasValue)
                .CountAsync(cancellationToken);

            var arInvoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId
                    && !i.IsDeleted
                    && i.InvoiceDate >= start
                    && i.InvoiceDate < end
                    && i.Status == InvoiceStatus.Sent
                    && !i.JournalEntryId.HasValue)
                .CountAsync(cancellationToken);

            var arReceipts = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId
                    && !p.IsDeleted
                    && p.PaymentDate >= start
                    && p.PaymentDate < end
                    && p.Status == "Cleared"
                    && !p.JournalEntryId.HasValue)
                .CountAsync(cancellationToken);

            var cashBankTransactions = await _unitOfWork.Repository<CashTransaction>()
                .GetQueryable(t => t.TenantId == TenantId
                    && !t.IsDeleted
                    && t.TransactionDate >= start
                    && t.TransactionDate < end
                    && t.ApprovalStatus == CashTransactionApprovalStatus.Approved
                    && !t.IsPosted
                    && !t.JournalEntryId.HasValue)
                .CountAsync(cancellationToken);

            return apInvoices + apPayments + arInvoices + arReceipts + cashBankTransactions;
        }

        private async Task<int> CountSubmittedUnapprovedSourceDocumentsAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var start = period.StartDate.Date;
            var end = period.EndDate.Date.AddDays(1);

            var apInvoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId
                    && !i.IsDeleted
                    && i.InvoiceDate >= start
                    && i.InvoiceDate < end
                    && i.Status == VendorInvoiceStatus.PendingApproval)
                .CountAsync(cancellationToken);

            var apPayments = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId
                    && !p.IsDeleted
                    && p.PaymentDate >= start
                    && p.PaymentDate < end
                    && p.Status == VendorPaymentStatus.PendingAuthorization)
                .CountAsync(cancellationToken);

            var cashBankTransactions = await _unitOfWork.Repository<CashTransaction>()
                .GetQueryable(t => t.TenantId == TenantId
                    && !t.IsDeleted
                    && t.TransactionDate >= start
                    && t.TransactionDate < end
                    && t.ApprovalStatus == CashTransactionApprovalStatus.Submitted)
                .CountAsync(cancellationToken);

            return apInvoices + apPayments + cashBankTransactions;
        }

        private async Task<int> CountUnfinalizedReconciliationsAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var start = period.StartDate.Date;
            var end = period.EndDate.Date.AddDays(1);

            return await _unitOfWork.Repository<BankReconciliation>()
                .GetQueryable(r => r.TenantId == TenantId
                    && !r.IsDeleted
                    && r.ReconciliationDate >= start
                    && r.ReconciliationDate < end
                    && r.Status != ReconciliationStatus.Completed
                    && r.Status != ReconciliationStatus.Approved
                    && r.Status != ReconciliationStatus.Cancelled)
                .CountAsync(cancellationToken);
        }

        private async Task<int> CountOrphanedPostingEventsAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var start = period.StartDate.Date;
            var end = period.EndDate.Date.AddDays(1);
            var postedJournalIds = _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(j => j.TenantId == TenantId
                    && !j.IsDeleted
                    && j.PostingStatus == "Posted")
                .Select(j => j.Id);

            return await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(e => e.TenantId == TenantId
                    && !e.IsDeleted
                    && e.PostingStatus == "Posted"
                    && e.PostingDate >= start
                    && e.PostingDate < end
                    && (!e.JournalEntryId.HasValue
                        || !postedJournalIds.Contains(e.JournalEntryId.Value)))
                .CountAsync(cancellationToken);
        }

        private async Task<int> CountPostedDocumentsMissingPostingEventsAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var start = period.StartDate.Date;
            var end = period.EndDate.Date.AddDays(1);
            var count = 0;

            count += await CountPostedDocumentsMissingEventAsync<VendorInvoice>(
                i => i.TenantId == TenantId && !i.IsDeleted && i.InvoiceDate >= start && i.InvoiceDate < end && i.JournalEntryId.HasValue,
                "VendorInvoice",
                cancellationToken);

            count += await CountPostedDocumentsMissingEventAsync<VendorPayment>(
                p => p.TenantId == TenantId && !p.IsDeleted && p.PaymentDate >= start && p.PaymentDate < end && p.JournalEntryId.HasValue,
                "VendorPayment",
                cancellationToken);

            count += await CountPostedDocumentsMissingEventAsync<Invoice>(
                i => i.TenantId == TenantId && !i.IsDeleted && i.InvoiceDate >= start && i.InvoiceDate < end && i.JournalEntryId.HasValue,
                "CustomerInvoice",
                cancellationToken);

            count += await CountPostedDocumentsMissingEventAsync<CustomerPayment>(
                p => p.TenantId == TenantId && !p.IsDeleted && p.PaymentDate >= start && p.PaymentDate < end && p.JournalEntryId.HasValue && !p.IsCreditNote,
                "CustomerPayment",
                cancellationToken);

            count += await CountPostedDocumentsMissingEventAsync<CustomerPayment>(
                p => p.TenantId == TenantId && !p.IsDeleted && p.PaymentDate >= start && p.PaymentDate < end && p.JournalEntryId.HasValue && p.IsCreditNote,
                "CustomerCreditNote",
                cancellationToken);

            count += await CountPostedDocumentsMissingEventAsync<CashTransaction>(
                t => t.TenantId == TenantId && !t.IsDeleted && t.TransactionDate >= start && t.TransactionDate < end && t.IsPosted && t.JournalEntryId.HasValue && t.TransactionType == CashTransactionType.Receipt,
                "CashBankReceipt",
                cancellationToken);

            count += await CountPostedDocumentsMissingEventAsync<CashTransaction>(
                t => t.TenantId == TenantId && !t.IsDeleted && t.TransactionDate >= start && t.TransactionDate < end && t.IsPosted && t.JournalEntryId.HasValue && t.TransactionType == CashTransactionType.Payment,
                "CashBankPayment",
                cancellationToken);

            count += await CountPostedDocumentsMissingEventAsync<CashTransaction>(
                t => t.TenantId == TenantId && !t.IsDeleted && t.TransactionDate >= start && t.TransactionDate < end && t.IsPosted && t.JournalEntryId.HasValue && t.TransactionType == CashTransactionType.Transfer,
                "CashBankTransfer",
                cancellationToken);

            return count;
        }

        private async Task<int> CountPostedDocumentsMissingEventAsync<T>(
            System.Linq.Expressions.Expression<Func<T, bool>> predicate,
            string sourceDocumentType,
            CancellationToken cancellationToken)
            where T : BaseEntity
        {
            var postedDocumentIds = _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(e => e.TenantId == TenantId
                    && !e.IsDeleted
                    && e.SourceDocumentType == sourceDocumentType
                    && e.PostingAction == "Post"
                    && e.PostingStatus == "Posted")
                .Select(e => e.SourceDocumentId);

            return await _unitOfWork.Repository<T>()
                .GetQueryable(predicate)
                .CountAsync(document => !postedDocumentIds.Contains(document.Id), cancellationToken);
        }

        private async Task<int> CountPostedDocumentJournalTenantMismatchesAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var start = period.StartDate.Date;
            var end = period.EndDate.Date.AddDays(1);
            var count = 0;
            var tenantJournalIds = _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(j => j.TenantId == TenantId && !j.IsDeleted)
                .Select(j => j.Id);

            count += await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId
                    && !i.IsDeleted
                    && i.InvoiceDate >= start
                    && i.InvoiceDate < end
                    && i.JournalEntryId.HasValue
                    && !tenantJournalIds.Contains(i.JournalEntryId.Value))
                .CountAsync(cancellationToken);

            count += await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId
                    && !p.IsDeleted
                    && p.PaymentDate >= start
                    && p.PaymentDate < end
                    && p.JournalEntryId.HasValue
                    && !tenantJournalIds.Contains(p.JournalEntryId.Value))
                .CountAsync(cancellationToken);

            count += await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId
                    && !i.IsDeleted
                    && i.InvoiceDate >= start
                    && i.InvoiceDate < end
                    && i.JournalEntryId.HasValue
                    && !tenantJournalIds.Contains(i.JournalEntryId.Value))
                .CountAsync(cancellationToken);

            count += await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId
                    && !p.IsDeleted
                    && p.PaymentDate >= start
                    && p.PaymentDate < end
                    && p.JournalEntryId.HasValue
                    && !tenantJournalIds.Contains(p.JournalEntryId.Value))
                .CountAsync(cancellationToken);

            count += await _unitOfWork.Repository<CashTransaction>()
                .GetQueryable(t => t.TenantId == TenantId
                    && !t.IsDeleted
                    && t.TransactionDate >= start
                    && t.TransactionDate < end
                    && t.JournalEntryId.HasValue
                    && !tenantJournalIds.Contains(t.JournalEntryId.Value))
                .CountAsync(cancellationToken);

            return count;
        }

        private async Task<int> CountCrossTenantPeriodReferenceMismatchesAsync(
            FiscalPeriod period,
            CancellationToken cancellationToken)
        {
            var journalMismatches = await _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(je => je.FiscalPeriodId == period.Id
                    && je.TenantId != TenantId
                    && !je.IsDeleted)
                .CountAsync(cancellationToken);

            var transactionMismatches = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.FiscalPeriodId == period.Id
                    && t.TenantId != TenantId
                    && !t.IsDeleted)
                .CountAsync(cancellationToken);

            return journalMismatches + transactionMismatches;
        }

        private async Task RecordPeriodAuditAsync(
            string eventType,
            FiscalPeriod period,
            object? beforeValues = null,
            object? afterValues = null,
            string? reason = null,
            string? comment = null,
            object? context = null,
            CancellationToken cancellationToken = default)
        {
            if (_financeAuditService == null)
            {
                return;
            }

            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = period.TenantId,
                SourceModule = "FINANCE",
                SourceDocumentType = "FiscalPeriod",
                SourceDocumentId = period.Id,
                BeforeValues = beforeValues,
                AfterValues = afterValues,
                Reason = reason,
                Comment = comment,
                Context = context,
                Resource = "Finance.FiscalPeriod",
                ResourceId = period.Id.ToString()
            }, cancellationToken);
        }

        private FinanceCloseTemplateDto MapFinanceCloseTemplateToDto(FinanceCloseTemplate template)
        {
            return new FinanceCloseTemplateDto
            {
                Id = template.Id,
                TemplateCode = template.TemplateCode,
                Name = template.Name,
                CloseType = template.CloseType,
                Version = template.Version,
                Status = template.Status,
                IsActive = template.IsActive,
                IsSystemDefault = template.IsSystemDefault,
                Description = template.Description,
                CreatedBy = template.CreatedBy,
                CreatedAt = template.CreatedAt,
                ApprovedByUserName = template.ApprovedByUserName,
                ApprovedAt = template.ApprovedAt,
                ApprovalDeclaration = template.ApprovalDeclaration,
                SupersededAt = template.SupersededAt,
                CanEdit = template.Status == FinanceCloseTemplateStatuses.Draft,
                CanApprove = template.Status == FinanceCloseTemplateStatuses.Draft &&
                    CurrentUserId.HasValue && template.CreatedById != CurrentUserId,
                Tasks = template.TaskDefinitions
                    .Where(item => !item.IsDeleted)
                    .OrderBy(item => item.Sequence)
                    .Select(item => new FinanceCloseTemplateTaskDto
                    {
                        Id = item.Id,
                        TaskCode = item.TaskCode,
                        Title = item.Title,
                        Category = item.Category,
                        DependsOnTaskCode = item.DependsOnTaskCode,
                        CheckCode = item.CheckCode,
                        Sequence = item.Sequence,
                        IsMandatory = item.IsMandatory,
                        IsAutomated = item.IsAutomated,
                        DueDaysAfterPeriodEnd = item.DueDaysAfterPeriodEnd,
                        DefaultAssigneeUserId = item.DefaultAssigneeUserId,
                        Instructions = item.Instructions
                    })
                    .ToList()
            };
        }

        private static SaveFinanceCloseTemplateTaskDto MapTemplateTaskToSaveDto(
            FinanceCloseTemplateTaskDefinition item)
        {
            return new SaveFinanceCloseTemplateTaskDto
            {
                TaskCode = item.TaskCode,
                Title = item.Title,
                Category = item.Category,
                DependsOnTaskCode = item.DependsOnTaskCode,
                CheckCode = item.CheckCode,
                Sequence = item.Sequence,
                IsMandatory = item.IsMandatory,
                IsAutomated = item.IsAutomated,
                DueDaysAfterPeriodEnd = item.DueDaysAfterPeriodEnd,
                DefaultAssigneeUserId = item.DefaultAssigneeUserId,
                Instructions = item.Instructions
            };
        }

        private static object BuildCloseTemplateAuditSnapshot(FinanceCloseTemplate template)
        {
            return new
            {
                template.Id,
                template.TemplateCode,
                template.Name,
                template.CloseType,
                template.Version,
                template.Status,
                template.IsActive,
                template.IsSystemDefault,
                template.Description,
                template.ApprovedByUserId,
                template.ApprovedAt,
                template.SupersededAt,
                Tasks = template.TaskDefinitions
                    .Where(item => !item.IsDeleted)
                    .OrderBy(item => item.Sequence)
                    .Select(item => new
                    {
                        item.TaskCode,
                        item.Title,
                        item.Category,
                        item.DependsOnTaskCode,
                        item.CheckCode,
                        item.Sequence,
                        item.IsMandatory,
                        item.IsAutomated,
                        item.DueDaysAfterPeriodEnd,
                        item.DefaultAssigneeUserId,
                        item.Instructions
                    })
                    .ToList()
            };
        }

        private async Task RecordCloseTemplateAuditAsync(
            string eventType,
            FinanceCloseTemplate template,
            object? beforeValues = null,
            object? afterValues = null,
            string? comment = null,
            object? context = null,
            CancellationToken cancellationToken = default)
        {
            if (_financeAuditService == null)
                return;

            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = template.TenantId,
                SourceModule = "FINANCE",
                SourceDocumentType = "FinanceCloseTemplate",
                SourceDocumentId = template.Id,
                BeforeValues = beforeValues,
                AfterValues = afterValues,
                Comment = comment,
                Context = context,
                Resource = "Finance.CloseTemplate",
                ResourceId = template.Id.ToString()
            }, cancellationToken);
        }

        private static object BuildPeriodAuditSnapshot(FiscalPeriod period)
        {
            return new
            {
                period.Id,
                period.TenantId,
                period.FiscalYearId,
                period.PeriodCode,
                period.PeriodName,
                period.StartDate,
                period.EndDate,
                period.PeriodStatus,
                period.IsOpen,
                period.IsClosed,
                period.IsLocked,
                period.AllowFutureDating,
                period.IsGlobalLockSuspended,
                period.ClosedDate,
                period.ClosedByUserId,
                period.LockedDate,
                period.LockedByUserId,
                period.LockReason,
                period.HasBeenReopened,
                period.ReopenCount,
                period.LastReopenedDate,
                period.LastReopenedByUserId,
                period.ReopenReason,
                period.ClosingNotes
            };
        }

        private FiscalYearDto MapFiscalYearToDto(FiscalYear fiscalYear)
        {
            return new FiscalYearDto
            {
                Id = fiscalYear.Id,
                TenantId = fiscalYear.TenantId,
                FiscalYearCode = fiscalYear.FiscalYearCode,
                FiscalYearName = fiscalYear.FiscalYearName,
                Year = fiscalYear.Year,
                FiscalYearType = fiscalYear.FiscalYearType,
                StartDate = fiscalYear.StartDate,
                EndDate = fiscalYear.EndDate,
                TotalDays = fiscalYear.TotalDays,
                NumberOfPeriods = fiscalYear.NumberOfPeriods,
                Status = fiscalYear.Status,
                IsActive = fiscalYear.IsActive,
                IsLocked = fiscalYear.IsLocked,
                LockedDate = fiscalYear.LockedDate,
                IsClosed = fiscalYear.IsClosed,
                ClosedDate = fiscalYear.ClosedDate,
                AllPeriodsClosedValidated = fiscalYear.AllPeriodsClosedValidated,
                RetainedEarningsTransferComplete = fiscalYear.RetainedEarningsTransferComplete,
                NetIncomeTransferred = fiscalYear.NetIncomeTransferred,
                OpeningBalancesGenerated = fiscalYear.OpeningBalancesGenerated,
                ReportingFramework = fiscalYear.ReportingFramework,
                BaseCurrency = fiscalYear.BaseCurrency,
                TotalJournalEntries = fiscalYear.TotalJournalEntries,
                TotalTransactionLines = fiscalYear.TotalTransactionLines,
                TotalDebits = fiscalYear.TotalDebits,
                TotalCredits = fiscalYear.TotalCredits,
                TotalRevenue = fiscalYear.TotalRevenue,
                TotalExpenses = fiscalYear.TotalExpenses,
                NetIncome = fiscalYear.NetIncome,
                IsBudgetApproved = fiscalYear.IsBudgetApproved,
                BudgetedRevenue = fiscalYear.BudgetedRevenue,
                BudgetedExpenses = fiscalYear.BudgetedExpenses,
                IsAuditComplete = fiscalYear.IsAuditComplete,
                AuditFirm = fiscalYear.AuditFirm,
                AuditOpinion = fiscalYear.AuditOpinion,
                CreatedAt = fiscalYear.CreatedAt,
                UpdatedAt = fiscalYear.UpdatedAt,
                CreatedBy = fiscalYear.CreatedBy,
                UpdatedBy = fiscalYear.UpdatedBy
            };
        }

        private FiscalPeriodDto MapFiscalPeriodToDto(
            FiscalPeriod period,
            Guid? latestClosePackCycleId = null,
            FinancePeriodReopenRequestDto? latestReopenRequest = null)
        {
            var now = DateTime.UtcNow;
            var moduleLocks = period.PeriodModuleLocks?
                .Where(moduleLock => moduleLock.TenantId == period.TenantId)
                .Select(moduleLock =>
                {
                    var reopeningExpired = !moduleLock.IsLocked
                        && moduleLock.ReopenExpiresAtUtc.HasValue
                        && moduleLock.ReopenExpiresAtUtc <= now;
                    return new PeriodModuleLockDto
                    {
                        Id = moduleLock.Id,
                        FiscalPeriodId = moduleLock.FiscalPeriodId,
                        ModuleDefinitionId = moduleLock.ModuleDefinitionId,
                        ModuleCode = moduleLock.ModuleDefinition?.ModuleCode ?? string.Empty,
                        ModuleName = moduleLock.ModuleDefinition?.ModuleName ?? string.Empty,
                        IsLocked = moduleLock.IsLocked || reopeningExpired,
                        LockedDate = moduleLock.LockedDate,
                        LockReason = reopeningExpired
                            ? $"Temporary reopening expired at {moduleLock.ReopenExpiresAtUtc:u}."
                            : moduleLock.LockReason,
                        UnlockedDate = moduleLock.UnlockedDate,
                        UnlockReason = moduleLock.UnlockReason,
                        ReopenExpiresAtUtc = moduleLock.ReopenExpiresAtUtc,
                        AutoRelockedDate = moduleLock.AutoRelockedDate,
                        IsTemporaryReopening = !moduleLock.IsLocked
                            && moduleLock.ReopenExpiresAtUtc.HasValue
                            && moduleLock.ReopenExpiresAtUtc > now
                    };
                })
                .ToList() ?? new List<PeriodModuleLockDto>();

            return new FiscalPeriodDto
            {
                Id = period.Id,
                TenantId = period.TenantId,
                FiscalYearId = period.FiscalYearId,
                PeriodCode = period.PeriodCode,
                PeriodName = period.PeriodName,
                PeriodNumber = period.PeriodNumber,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                PeriodStatus = period.PeriodStatus,
                IsOpen = period.IsOpen,
                AllowFutureDating = period.AllowFutureDating,
                IsLocked = period.IsLocked,
                IsGlobalLockSuspended = period.IsGlobalLockSuspended,
                IsPartiallyLocked = period.IsOpen
                    && !period.IsLocked
                    && (period.IsGlobalLockSuspended || moduleLocks.Any(moduleLock => moduleLock.IsLocked)),
                IsClosed = period.IsClosed,
                IsCloseInitiated = period.IsCloseInitiated,
                CloseInitiatedDate = period.CloseInitiatedDate,
                ClosedDate = period.ClosedDate,
                LatestClosePackCycleId = latestClosePackCycleId,
                LatestReopenRequest = latestReopenRequest,
                // ClosedBy = period.ClosedByUserId.ToString(), // TODO: Resolve username
                TrialBalanceValidated = period.TrialBalanceValidated,
                BankReconciliationComplete = period.BankReconciliationComplete,
                CurrencyRevaluationComplete = period.CurrencyRevaluationComplete,
                DepreciationComplete = period.DepreciationComplete,
                InventoryValuationComplete = period.InventoryValuationComplete,
                AccrualsComplete = period.AccrualsComplete,
                HasBeenReopened = period.HasBeenReopened,
                ReopenCount = period.ReopenCount,
                LastReopenedDate = period.LastReopenedDate,
                // ReopenedBy = period.LastReopenedByUserId.ToString(), // TODO: Resolve username
                LockedDate = period.LockedDate,
                // LockedBy = period.LockedByUserId.ToString(), // TODO: Resolve username
                IsYearEnd = period.IsYearEnd,
                YearEndCloseComplete = period.YearEndCloseComplete,
                TotalJournalEntries = period.TotalJournalEntries,
                TotalTransactionLines = period.TotalTransactionLines,
                TotalDebits = period.TotalDebits,
                TotalCredits = period.TotalCredits,
                BalanceDifference = period.BalanceDifference,
                CreatedAt = period.CreatedAt,
                UpdatedAt = period.UpdatedAt,
                CreatedBy = period.CreatedBy,
                UpdatedBy = period.UpdatedBy,
                ModuleLocks = moduleLocks
            };
        }

        private async Task<PeriodReopenImpactValidation> BuildPeriodReopenImpactValidationAsync(
            FiscalPeriod period,
            FinanceCloseCycle closedCycle,
            CancellationToken cancellationToken)
        {
            var blockers = new List<string>();
            if (period.IsLocked || string.Equals(period.PeriodStatus, "Locked", StringComparison.OrdinalIgnoreCase))
                blockers.Add("A locked accounting period cannot be reopened through this workflow.");
            if (!period.IsClosed || !string.Equals(period.PeriodStatus, "Closed", StringComparison.OrdinalIgnoreCase))
                blockers.Add("The target accounting period is no longer closed.");

            var fiscalYear = await _unitOfWork.Repository<FiscalYear>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == period.FiscalYearId);
            if (fiscalYear == null)
            {
                blockers.Add("The fiscal year owning this accounting period could not be resolved.");
            }
            else
            {
                if (fiscalYear.IsLocked)
                    blockers.Add("The fiscal year is locked and must remain immutable.");
                if (fiscalYear.IsClosed)
                    blockers.Add("The fiscal year is closed. Complete the controlled fiscal-year reopen before requesting a period reopen.");
            }

            var latestCycle = await _unitOfWork.Repository<FinanceCloseCycle>()
                .GetQueryable(item => item.TenantId == TenantId && item.FiscalPeriodId == period.Id && !item.IsDeleted)
                .OrderByDescending(item => item.CycleNumber)
                .Select(item => new { item.Id, item.CycleNumber, item.Status })
                .FirstOrDefaultAsync(cancellationToken);
            if (latestCycle == null || latestCycle.Id != closedCycle.Id ||
                !string.Equals(latestCycle.Status, FinanceCloseStatuses.Closed, StringComparison.Ordinal))
            {
                blockers.Add("The request does not reference the latest completed Finance close cycle.");
            }

            var hasSignedCertificate = await _unitOfWork.Repository<FinanceCloseCertification>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.FinanceCloseCycleId == closedCycle.Id &&
                    !item.IsSuperseded &&
                    item.PreparedAt.HasValue &&
                    item.ApprovedAt.HasValue &&
                    item.ApprovedByUserId.HasValue)
                .AnyAsync(cancellationToken);
            if (!hasSignedCertificate)
                blockers.Add("The latest close cycle has no active signed maker-checker certificate to supersede.");

            var laterPeriods = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(item => item.TenantId == TenantId &&
                    item.FiscalYearId == period.FiscalYearId &&
                    item.StartDate > period.EndDate &&
                    !string.Equals(item.PeriodStatus, "Future"))
                .OrderBy(item => item.StartDate)
                .Select(item => new PeriodReopenImpactPeriod(
                    item.Id,
                    item.PeriodCode,
                    item.PeriodName,
                    item.PeriodStatus,
                    item.IsOpen,
                    item.IsClosed,
                    item.IsLocked))
                .ToListAsync(cancellationToken);

            var protectedLaterPeriods = laterPeriods
                .Where(item => item.IsClosed || item.IsLocked ||
                    string.Equals(item.PeriodStatus, "Closed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.PeriodStatus, "Locked", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (protectedLaterPeriods.Length > 0)
            {
                blockers.Add(
                    $"Reopen later closed/locked periods first, working backwards from {protectedLaterPeriods[^1].PeriodCode}; " +
                    "an earlier period cannot change while a downstream certificate remains protected.");
            }

            var warnings = new List<string>();
            var openLaterPeriods = laterPeriods.Count(item => item.IsOpen ||
                string.Equals(item.PeriodStatus, "Open", StringComparison.OrdinalIgnoreCase));
            if (openLaterPeriods > 0)
            {
                warnings.Add(
                    $"{openLaterPeriods} later open accounting period(s) are affected and must be reviewed before their next close certification.");
            }

            var snapshot = new PeriodReopenImpactSnapshot(
                period.Id,
                period.PeriodCode,
                fiscalYear?.Id ?? period.FiscalYearId,
                fiscalYear?.IsClosed ?? false,
                fiscalYear?.IsLocked ?? false,
                closedCycle.Id,
                closedCycle.CycleNumber,
                laterPeriods,
                warnings);
            var snapshotJson = JsonSerializer.Serialize(snapshot);
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson)))
                .ToLowerInvariant();
            return new PeriodReopenImpactValidation(snapshot, snapshotJson, fingerprint, blockers);
        }

        private static FinancePeriodReopenRequestDto MapFinancePeriodReopenRequestToDto(
            FinancePeriodReopenRequest request,
            int? closedCycleNumber = null)
        {
            PeriodReopenImpactSnapshot? snapshot = null;
            try
            {
                snapshot = JsonSerializer.Deserialize<PeriodReopenImpactSnapshot>(request.ImpactSnapshotJson);
            }
            catch (JsonException)
            {
                // A malformed retained snapshot should remain visible as request metadata instead
                // of breaking the whole Fiscal Periods screen. Approval still fails its fingerprint
                // comparison, so this defensive read path cannot weaken the control.
            }

            return new FinancePeriodReopenRequestDto
            {
                Id = request.Id,
                FiscalPeriodId = request.FiscalPeriodId,
                FinanceCloseCycleId = request.FinanceCloseCycleId,
                ResultingFinanceCloseCycleId = request.ResultingFinanceCloseCycleId,
                ClosedCycleNumber = closedCycleNumber ?? request.FinanceCloseCycle?.CycleNumber ?? 0,
                Status = request.Status,
                Reason = request.Reason,
                AffectedPeriodAssessment = request.AffectedPeriodAssessment,
                ImpactFingerprint = request.ImpactFingerprint,
                AffectedPeriodCount = request.AffectedPeriodCount,
                RequestedByUserName = request.RequestedByUserName,
                RequestedAt = request.RequestedAt,
                ReviewedByUserName = request.ReviewedByUserName,
                ReviewedAt = request.ReviewedAt,
                ReviewComment = request.ReviewComment,
                AffectedPeriods = snapshot?.AffectedPeriods
                    .Select(item => new FinancePeriodReopenImpactPeriodDto
                    {
                        FiscalPeriodId = item.FiscalPeriodId,
                        PeriodCode = item.PeriodCode,
                        PeriodName = item.PeriodName,
                        PeriodStatus = item.PeriodStatus
                    })
                    .ToArray() ?? Array.Empty<FinancePeriodReopenImpactPeriodDto>(),
                ValidationWarnings = snapshot?.Warnings.ToArray() ?? Array.Empty<string>()
            };
        }

        private string GetPeriodName(PeriodType type, DateTime start, DateTime end, int number)
        {
            return type switch
            {
                PeriodType.Monthly => $"{start:MMMM yyyy}",
                PeriodType.Quarterly => $"Q{number} {start:yyyy}",
                PeriodType.Weekly => $"Week {number} ({start:MMM d}-{end:MMM d})",
                PeriodType.Daily => $"{start:MMM d, yyyy}",
                _ => $"Period {number}"
            };
        }

        private static string ComputeCloseEvidenceFingerprint(CloseCheckResult result, string evidenceJson)
        {
            // Invariant numeric formatting and explicit separators make this stable across server
            // cultures. Evidence JSON is generated from controlled provider objects whose property
            // and list ordering is deterministic; any material measurement change changes the hash.
            var canonical = string.Join("\n", new[]
            {
                result.CheckCode,
                result.Severity,
                result.Status,
                result.ResultSummary,
                result.ExceptionCount.ToString(CultureInfo.InvariantCulture),
                result.ExceptionAmount?.ToString("0.################", CultureInfo.InvariantCulture) ?? string.Empty,
                evidenceJson
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
                .ToLowerInvariant();
        }

        private sealed record PeriodReopenImpactPeriod(
            Guid FiscalPeriodId,
            string PeriodCode,
            string PeriodName,
            string PeriodStatus,
            bool IsOpen,
            bool IsClosed,
            bool IsLocked);

        private sealed record PeriodReopenImpactSnapshot(
            Guid FiscalPeriodId,
            string PeriodCode,
            Guid FiscalYearId,
            bool FiscalYearIsClosed,
            bool FiscalYearIsLocked,
            Guid FinanceCloseCycleId,
            int CloseCycleNumber,
            IReadOnlyList<PeriodReopenImpactPeriod> AffectedPeriods,
            IReadOnlyList<string> Warnings);

        private sealed record PeriodReopenImpactValidation(
            PeriodReopenImpactSnapshot Snapshot,
            string SnapshotJson,
            string Fingerprint,
            IReadOnlyList<string> Blockers);

        private sealed record CloseCheckResult(
            string CheckCode,
            string Title,
            string Category,
            string Severity,
            string Status,
            string ResultSummary,
            int ExceptionCount,
            decimal? ExceptionAmount,
            object Evidence);

        private sealed record SubledgerCloseCheckResults(
            CloseCheckResult ApControl,
            CloseCheckResult ArControl,
            CloseCheckResult ApUnapplied,
            CloseCheckResult ArUnapplied);
    }
}
