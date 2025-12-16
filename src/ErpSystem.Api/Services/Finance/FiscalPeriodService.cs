using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Api.Services.Finance
{
    public class FiscalPeriodService : IFiscalPeriodService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<FiscalPeriodService> _logger;

        public FiscalPeriodService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<FiscalPeriodService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;
        private string UserName => _currentUserService.UserName ?? "system";
        private Guid? CurrentUserId => Guid.TryParse(_currentUserService.UserId, out var id) ? id : null;

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
            var overlapping = await _unitOfWork.Repository<FiscalYear>()
                .GetQueryable(fy => fy.TenantId == TenantId
                    && ((dto.StartDate >= fy.StartDate && dto.StartDate <= fy.EndDate)
                        || (dto.EndDate >= fy.StartDate && dto.EndDate <= fy.EndDate)))
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
                StartDate = dto.StartDate.Date,
                EndDate = dto.EndDate.Date,
                TotalDays = (dto.EndDate - dto.StartDate).Days + 1,
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

            // Always auto-create periods (default behavior since flag is missing)
            var periodCount = dto.NumberOfPeriods;
            var periodType = "Monthly"; // Defaulting to Monthly as property missing in DTO

            var currentDate = dto.StartDate.Date;
            for (int i = 1; i <= periodCount; i++)
            {
                var periodStart = new DateTime(currentDate.Year, currentDate.Month, 1);
                // Adjust start date for first period if it doesn't start on 1st
                if (i == 1) periodStart = dto.StartDate.Date;

                var periodEnd = periodStart.AddMonths(1).AddDays(-1);

                if (periodEnd > dto.EndDate.Date || i == periodCount)
                    periodEnd = dto.EndDate.Date;

                var period = new FiscalPeriod
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    FiscalYearId = fiscalYear.Id,
                    PeriodCode = $"{dto.FiscalYearCode}-{i:D2}",
                    PeriodName = $"{periodStart:MMMM yyyy}",
                    PeriodNumber = i,
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
                
                // Move to next month
                currentDate = currentDate.AddMonths(1);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fiscal year {Code} created by {User}", fiscalYear.FiscalYearCode, UserName);

            return MapFiscalYearToDto(fiscalYear);
        }

        public async Task<IReadOnlyList<FiscalPeriodDto>> GetFiscalPeriodsAsync(
            Guid? fiscalYearId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId);

            if (fiscalYearId.HasValue)
                query = query.Where(fp => fp.FiscalYearId == fiscalYearId.Value);

            var periods = await query
                .OrderBy(fp => fp.StartDate)
                .ToListAsync(cancellationToken);

            return periods.Select(MapFiscalPeriodToDto).ToList();
        }

        public async Task<FiscalPeriodDto?> GetFiscalPeriodByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == id);

            return period == null ? null : MapFiscalPeriodToDto(period);
        }

        public async Task<FiscalPeriodDto?> GetPeriodForDateAsync(DateTime transactionDate, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId
                    && fp.StartDate <= transactionDate.Date
                    && fp.EndDate >= transactionDate.Date)
                .FirstOrDefaultAsync(cancellationToken);

            return period == null ? null : MapFiscalPeriodToDto(period);
        }

        public async Task<PeriodCloseResultDto> ClosePeriodAsync(PeriodCloseRequestDto request, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == request.FiscalPeriodId);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{request.FiscalPeriodId}' not found.");

            if (period.IsLocked)
                throw new InvalidOperationException("Cannot close a locked period.");

            if (period.PeriodStatus == "Closed")
                throw new InvalidOperationException("Period is already closed.");

            if (!request.SkipValidation)
            {
                var validation = await ValidatePeriodCloseAsync(request.FiscalPeriodId, cancellationToken);
                if (!validation.CanClose)
                {
                    return new PeriodCloseResultDto
                    {
                        Success = false,
                        Message = "Validation failed",
                        FiscalPeriodId = period.Id,
                        PeriodName = period.PeriodName,
                        Errors = validation.ValidationErrors
                    };
                }
            }

            period.PeriodStatus = "Closed";
            period.IsOpen = false;
            period.ClosedDate = DateTime.UtcNow;
            period.ClosedByUserId = CurrentUserId;
            period.ClosingNotes = request.ClosingNotes;
            period.UpdatedAt = DateTime.UtcNow;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
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

        public async Task<FiscalPeriodDto> ReopenPeriodAsync(PeriodReopenRequestDto request, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == request.FiscalPeriodId);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{request.FiscalPeriodId}' not found.");

            if (period.IsLocked)
                throw new InvalidOperationException("Cannot reopen a locked period.");

            if (period.PeriodStatus != "Closed")
                throw new InvalidOperationException("Only closed periods can be reopened.");

            period.PeriodStatus = "Open";
            period.IsOpen = true;
            period.HasBeenReopened = true;
            period.ReopenCount++;
            period.LastReopenedDate = DateTime.UtcNow;
            period.LastReopenedByUserId = CurrentUserId;
            period.ReopenReason = request.Reason;
            period.UpdatedAt = DateTime.UtcNow;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fiscal period {Code} reopened by {User}. Reason: {Reason}",
                period.PeriodCode, UserName, request.Reason);

            return MapFiscalPeriodToDto(period);
        }

        public async Task<FiscalPeriodDto> LockPeriodAsync(PeriodLockRequestDto request, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == request.FiscalPeriodId);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{request.FiscalPeriodId}' not found.");

            if (period.IsLocked)
                throw new InvalidOperationException("Period is already locked.");

            if (period.PeriodStatus != "Closed")
                throw new InvalidOperationException("Only closed periods can be locked.");

            period.IsLocked = true;
            period.LockedDate = DateTime.UtcNow;
            period.LockedByUserId = CurrentUserId;
            period.LockReason = request.LockReason;
            period.PeriodStatus = "Locked";
            period.UpdatedAt = DateTime.UtcNow;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fiscal period {Code} locked by {User}. Reason: {Reason}",
                period.PeriodCode, UserName, request.LockReason);

            return MapFiscalPeriodToDto(period);
        }

        public async Task<FiscalPeriodDto> UnlockPeriodAsync(Guid periodId, string reason, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == periodId);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");

            if (!period.IsLocked)
                throw new InvalidOperationException("Period is not locked.");

            period.IsLocked = false;
            period.LockedDate = null;
            period.LockedByUserId = null;
            period.LockReason = null;
            period.PeriodStatus = period.IsClosed ? "Closed" : "Open";
            
            period.UpdatedAt = DateTime.UtcNow;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Fiscal period {Code} UNLOCKED by {User}. Reason: {Reason}",
                period.PeriodCode, UserName, reason);

            return MapFiscalPeriodToDto(period);
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

            if (period.PeriodStatus == "Closed")
                errors.Add("Period is already closed.");

            if (period.IsLocked)
                errors.Add("Period is locked.");

            var unpostedEntries = await _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(je => je.FiscalPeriodId == periodId
                    && je.PostingStatus != "Posted"
                    && !je.IsDeleted)
                .CountAsync(cancellationToken);

            if (unpostedEntries > 0)
                errors.Add($"{unpostedEntries} unposted journal entries exist.");

            var futurePeriodsClosed = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId
                    && fp.StartDate > period.EndDate
                    && fp.PeriodStatus == "Closed")
                .AnyAsync(cancellationToken);

            if (futurePeriodsClosed)
                errors.Add("Cannot close period while future periods are closed.");

            // Validate Trial Balance (Debits = Credits)
            var transactions = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.TenantId == TenantId && t.FiscalPeriodId == periodId)
                .ToListAsync(cancellationToken);

            decimal totalDebits = transactions.Sum(t => t.DebitAmount);
            decimal totalCredits = transactions.Sum(t => t.CreditAmount);
            decimal difference = totalDebits - totalCredits;
            bool isBalanced = Math.Abs(difference) < 0.01m;

            if (!isBalanced)
            {
                errors.Add($"Trial Balance is not balanced. Difference: {difference}");
            }

            return new PeriodCloseValidationDto
            {
                CanClose = errors.Count == 0,
                ValidationErrors = errors,
                PeriodName = period.PeriodName,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                TotalDebits = totalDebits,
                TotalCredits = totalCredits,
                Difference = difference,
                TotalTransactionLines = transactions.Count
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
                StartDate = fiscalYear.StartDate,
                EndDate = fiscalYear.EndDate,
                IsActive = fiscalYear.IsActive,
                IsClosed = fiscalYear.IsClosed,
                CreatedAt = fiscalYear.CreatedAt,
                UpdatedAt = fiscalYear.UpdatedAt,
                CreatedBy = fiscalYear.CreatedBy,
                UpdatedBy = fiscalYear.UpdatedBy
            };
        }

        private FiscalPeriodDto MapFiscalPeriodToDto(FiscalPeriod period)
        {
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
                IsLocked = period.IsLocked,
                ClosedDate = period.ClosedDate,
                // ClosedBy = period.ClosedByUserId.ToString(), // TODO: Resolve username
                LastReopenedDate = period.LastReopenedDate,
                // ReopenedBy = period.LastReopenedByUserId.ToString(), // TODO: Resolve username
                LockedDate = period.LockedDate,
                // LockedBy = period.LockedByUserId.ToString(), // TODO: Resolve username
                CreatedAt = period.CreatedAt,
                UpdatedAt = period.UpdatedAt,
                CreatedBy = period.CreatedBy,
                UpdatedBy = period.UpdatedBy
            };
        }
    }
}
