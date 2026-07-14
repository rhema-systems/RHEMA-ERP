using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Shared;

namespace ErpSystem.Api.Services.Finance.Fiscal
{
    public class FiscalPeriodService : IFiscalPeriodService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<FiscalPeriodService> _logger;
        private readonly IFinanceAuditService? _financeAuditService;

        public FiscalPeriodService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<FiscalPeriodService> logger,
            IFinanceAuditService? financeAuditService = null)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
            _financeAuditService = financeAuditService;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
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

            // Generate periods based on type
            var periodType = dto.PeriodType;
            var currentDate = dto.StartDate.Date;
            int maxPeriods = dto.NumberOfPeriods;

            // Safety check for daily/weekly if user put wrong number
            if (periodType == PeriodType.Daily && maxPeriods < 360) maxPeriods = 365;
            if (periodType == PeriodType.Weekly && maxPeriods < 50) maxPeriods = 52;
            
            // Adjust based on leap year or actual year length
            // We'll iterate until EndDate is reached or maxPeriods
            
            int periodNumber = 1;

            while (currentDate <= dto.EndDate.Date && periodNumber <= maxPeriods)
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
                if (periodEnd > dto.EndDate.Date)
                    periodEnd = dto.EndDate.Date;

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

            // Check for any transactions in periods
            var periodIds = fiscalYear.FiscalPeriods.Select(p => p.Id).ToList();
            if (periodIds.Any())
            {
                var hasTransactions = await _unitOfWork.Repository<JournalEntry>()
                    .GetQueryable(je => periodIds.Contains(je.FiscalPeriodId))
                    .AnyAsync(cancellationToken);

                if (hasTransactions)
                    throw new InvalidOperationException("Cannot delete fiscal year because it has associated transactions.");
            }
            
            await _unitOfWork.Repository<FiscalYear>().DeleteAsync(fiscalYear);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fiscal year {Code} deleted by {User}", fiscalYear.FiscalYearCode, UserName);
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

            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodCloseRequested,
                period,
                beforeValues: null,
                afterValues: BuildPeriodAuditSnapshot(period),
                comment: request.ClosingNotes,
                context: new { request.SkipValidation },
                cancellationToken: cancellationToken);

            if (period.IsLocked)
                throw new InvalidOperationException("Cannot close a locked period.");

            if (period.PeriodStatus == "Closed")
                throw new InvalidOperationException("Period is already closed.");

            if (!period.IsOpen || !string.Equals(period.PeriodStatus, "Open", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only open periods can be closed.");
            }

            var validation = await ValidatePeriodCloseAsync(request.FiscalPeriodId, cancellationToken);
            if (!validation.CanClose)
            {
                await RecordPeriodAuditAsync(
                    FinanceAuditEvents.AccountingPeriodCloseValidationFailed,
                    period,
                    beforeValues: BuildPeriodAuditSnapshot(period),
                    afterValues: validation,
                    comment: "Accounting period close validation failed.",
                    context: new { request.SkipValidation, validation.ValidationErrors, validation.ValidationWarnings },
                    cancellationToken: cancellationToken);

                return new PeriodCloseResultDto
                {
                    Success = false,
                    Message = "Validation failed",
                    FiscalPeriodId = period.Id,
                    PeriodName = period.PeriodName,
                    Errors = validation.ValidationErrors
                };
            }

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
                    validation.TotalTransactionLines
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

        public async Task<FiscalPeriodDto> ReopenPeriodAsync(PeriodReopenRequestDto request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new InvalidOperationException("A reason is required to reopen an accounting period.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(fp => fp.TenantId == TenantId && fp.Id == request.FiscalPeriodId);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{request.FiscalPeriodId}' not found.");

            if (period.IsLocked)
                throw new InvalidOperationException("Cannot reopen a locked period.");

            if (period.PeriodStatus != "Closed")
                throw new InvalidOperationException("Only closed periods can be reopened.");

            var beforeReopen = BuildPeriodAuditSnapshot(period);
            var reason = request.Reason.Trim();
            period.PeriodStatus = "Open";
            period.IsOpen = true;
            period.IsClosed = false;
            period.HasBeenReopened = true;
            period.ReopenCount++;
            period.LastReopenedDate = DateTime.UtcNow;
            period.LastReopenedByUserId = CurrentUserId;
            period.ReopenReason = reason;
            period.UpdatedAt = DateTime.UtcNow;
            period.UpdatedBy = UserName;
            period.LastModifiedById = CurrentUserId;

            await _unitOfWork.Repository<FiscalPeriod>().UpdateAsync(period);
            await RecordPeriodAuditAsync(
                FinanceAuditEvents.AccountingPeriodReopened,
                period,
                beforeValues: beforeReopen,
                afterValues: BuildPeriodAuditSnapshot(period),
                reason: reason,
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fiscal period {Code} reopened by {User}. Reason: {Reason}",
                period.PeriodCode, UserName, reason);

            return MapFiscalPeriodToDto(period);
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
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId && fp.Id == periodId)
                .Include(fp => fp.PeriodModuleLocks)
                    .ThenInclude(pml => pml.ModuleDefinition)
                .FirstOrDefaultAsync(cancellationToken);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");

            var module = await _unitOfWork.Repository<ModuleDefinition>()
                .FirstOrDefaultAsync(m => m.ModuleCode == moduleCode);
            
            if (module == null)
                throw new ArgumentException($"Module '{moduleCode}' not found.");

            var existingLock = period.PeriodModuleLocks.FirstOrDefault(l => l.ModuleDefinitionId == module.Id);
            
            if (existingLock != null)
            {
                if (existingLock.IsLocked)
                    throw new InvalidOperationException($"Period is already locked for module {moduleCode}.");
                
                existingLock.IsLocked = true;
                existingLock.LockedDate = DateTime.UtcNow;
                existingLock.LockedByUserId = CurrentUserId;
                existingLock.LockReason = reason;
                existingLock.UnlockedDate = null;
                existingLock.UnlockedByUserId = null;
                
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
                    LockedDate = DateTime.UtcNow,
                    LockedByUserId = CurrentUserId,
                    LockReason = reason,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                };
                
                await _unitOfWork.Repository<PeriodModuleLock>().AddAsync(newLock);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return MapFiscalPeriodToDto(period);
        }

        public async Task<FiscalPeriodDto> UnlockPeriodForModuleAsync(Guid periodId, string moduleCode, string reason, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId && fp.Id == periodId)
                .Include(fp => fp.PeriodModuleLocks)
                    .ThenInclude(pml => pml.ModuleDefinition)
                .FirstOrDefaultAsync(cancellationToken);

            if (period == null)
                throw new ArgumentException($"Fiscal period with Id '{periodId}' not found.");

            var module = await _unitOfWork.Repository<ModuleDefinition>()
                .FirstOrDefaultAsync(m => m.ModuleCode == moduleCode);
            
            if (module == null)
                throw new ArgumentException($"Module '{moduleCode}' not found.");

            var existingLock = period.PeriodModuleLocks.FirstOrDefault(l => l.ModuleDefinitionId == module.Id);
            
            if (existingLock == null)
            {
                 var newLock = new PeriodModuleLock
                 {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    FiscalPeriodId = period.Id,
                    ModuleDefinitionId = module.Id,
                    IsLocked = false, 
                    UnlockedDate = DateTime.UtcNow,
                    UnlockedByUserId = CurrentUserId,
                    UnlockReason = reason,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                 };
                 await _unitOfWork.Repository<PeriodModuleLock>().AddAsync(newLock);
            }
            else
            {
                existingLock.IsLocked = false;
                existingLock.UnlockedDate = DateTime.UtcNow;
                existingLock.UnlockedByUserId = CurrentUserId;
                existingLock.UnlockReason = reason;
                existingLock.LockedDate = null;
                await _unitOfWork.Repository<PeriodModuleLock>().UpdateAsync(existingLock);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return MapFiscalPeriodToDto(period);
        }

        public async Task<bool> IsPeriodLockedForModuleAsync(Guid periodId, string moduleCode, CancellationToken cancellationToken = default)
        {
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(fp => fp.TenantId == TenantId && fp.Id == periodId)
                .Include(fp => fp.PeriodModuleLocks)
                    .ThenInclude(pml => pml.ModuleDefinition)
                .FirstOrDefaultAsync(cancellationToken);
                
            if (period == null) return true;

            var moduleLock = period.PeriodModuleLocks.FirstOrDefault(ml => ml.ModuleDefinition.ModuleCode == moduleCode);
            
            if (period.IsLocked)
            {
                if (moduleLock != null && !moduleLock.IsLocked)
                    return false;
                
                return true;
            }
            else
            {
                if (moduleLock != null && moduleLock.IsLocked)
                    return true;
                    
                return false;
            }
        }

        public async Task<IReadOnlyList<ModuleDefinitionDto>> GetModuleDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            var modules = await _unitOfWork.Repository<ModuleDefinition>()
                .GetQueryable(m => m.IsActive)
                .OrderBy(m => m.SortOrder)
                .ToListAsync(cancellationToken);

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

            var unbalancedPostedJournals = await postedJournalsQuery
                .CountAsync(je => je.TotalDebitAmount != je.TotalCreditAmount, cancellationToken);

            if (unbalancedPostedJournals > 0)
                errors.Add($"{unbalancedPostedJournals} posted journal entries are unbalanced.");

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
                IsClosed = period.IsClosed,
                IsCloseInitiated = period.IsCloseInitiated,
                CloseInitiatedDate = period.CloseInitiatedDate,
                ClosedDate = period.ClosedDate,
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
                ModuleLocks = period.PeriodModuleLocks?.Select(ml => new PeriodModuleLockDto
                {
                    Id = ml.Id,
                    FiscalPeriodId = ml.FiscalPeriodId,
                    ModuleDefinitionId = ml.ModuleDefinitionId,
                    ModuleCode = ml.ModuleDefinition?.ModuleCode ?? string.Empty,
                    ModuleName = ml.ModuleDefinition?.ModuleName ?? string.Empty,
                    IsLocked = ml.IsLocked,
                    LockedDate = ml.LockedDate,
                    // LockedByUserName = ... resolve user
                    LockReason = ml.LockReason,
                    UnlockedDate = ml.UnlockedDate
                }).ToList() ?? new List<PeriodModuleLockDto>()
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
    }
}
