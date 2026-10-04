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
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Finance;

namespace ErpSystem.Api.Services.Finance.UnitAccounting
{
    /// <summary>
    /// Service implementation for managing Unit Types.
    /// Unit Types define the measurement units for non-financial quantities.
    /// </summary>
    public class UnitTypeService : IUnitTypeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UnitTypeService> _logger;

        public UnitTypeService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<UnitTypeService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";

        public async Task<IReadOnlyList<UnitTypeDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var unitTypes = await _unitOfWork.Repository<UnitType>()
                .GetQueryable(ut => ut.TenantId == TenantId && !ut.IsDeleted)
                .OrderBy(ut => ut.Code)
                .ToListAsync(cancellationToken);

            return unitTypes.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<UnitTypeDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            var unitTypes = await _unitOfWork.Repository<UnitType>()
                .GetQueryable(ut => ut.TenantId == TenantId && ut.IsActive && !ut.IsDeleted)
                .OrderBy(ut => ut.Code)
                .ToListAsync(cancellationToken);

            return unitTypes.Select(MapToDto).ToList();
        }

        public async Task<UnitTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var unitType = await _unitOfWork.Repository<UnitType>()
                .FirstOrDefaultAsync(ut => ut.Id == id && ut.TenantId == TenantId && !ut.IsDeleted);

            return unitType == null ? null : MapToDto(unitType);
        }

        public async Task<UnitTypeDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var unitType = await _unitOfWork.Repository<UnitType>()
                .FirstOrDefaultAsync(ut => ut.Code == code && ut.TenantId == TenantId && !ut.IsDeleted);

            return unitType == null ? null : MapToDto(unitType);
        }

        public async Task<UnitTypeDto> CreateAsync(CreateUnitTypeDto dto, CancellationToken cancellationToken = default)
        {
            PrecisionRoundingPolicy.ValidateQuantity(0m, dto.DecimalPlaces, dto.RoundingIncrement);
            // Check for duplicate code
            var exists = await _unitOfWork.Repository<UnitType>()
                .GetQueryable(ut => ut.TenantId == TenantId && ut.Code == dto.Code && !ut.IsDeleted)
                .AnyAsync(cancellationToken);

            if (exists)
                throw new InvalidOperationException($"Unit type with code '{dto.Code}' already exists.");

            var now = DateTime.UtcNow;
            var unitType = new UnitType
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = dto.Code.ToUpper(),
                Name = dto.Name,
                Description = dto.Description,
                DecimalPlaces = dto.DecimalPlaces,
                RoundingIncrement = dto.RoundingIncrement,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<UnitType>().AddAsync(unitType);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit type {Code} created by {User}", unitType.Code, UserName);

            return MapToDto(unitType);
        }

        public async Task<UnitTypeDto> UpdateAsync(Guid id, UpdateUnitTypeDto dto, CancellationToken cancellationToken = default)
        {
            var unitType = await _unitOfWork.Repository<UnitType>()
                .FirstOrDefaultAsync(ut => ut.Id == id && ut.TenantId == TenantId && !ut.IsDeleted);

            if (unitType == null)
                throw new ArgumentException($"Unit type with ID '{id}' not found.");

            if (!string.IsNullOrEmpty(dto.Name))
                unitType.Name = dto.Name;
            if (!string.IsNullOrEmpty(dto.Description))
                unitType.Description = dto.Description;
            var nextDecimalPlaces = dto.DecimalPlaces ?? unitType.DecimalPlaces;
            var nextRoundingIncrement = dto.RoundingIncrementSpecified
                ? dto.RoundingIncrement
                : unitType.RoundingIncrement;
            PrecisionRoundingPolicy.ValidateQuantity(0m, nextDecimalPlaces, nextRoundingIncrement);

            if ((nextDecimalPlaces != unitType.DecimalPlaces || nextRoundingIncrement != unitType.RoundingIncrement)
                && await HasPostedOrBudgetUsageAsync(unitType.Id, cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Quantity precision for unit type '{unitType.Code}' cannot change after posted journal or budget usage. Create a new Unit Type for the new precision policy.");
            }

            unitType.DecimalPlaces = nextDecimalPlaces;
            if (dto.RoundingIncrementSpecified)
                unitType.RoundingIncrement = dto.RoundingIncrement;

            PrecisionRoundingPolicy.ValidateQuantity(0m, unitType.DecimalPlaces, unitType.RoundingIncrement);

            unitType.UpdatedAt = DateTime.UtcNow;
            unitType.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitType>().UpdateAsync(unitType);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit type {Code} updated by {User}", unitType.Code, UserName);

            return MapToDto(unitType);
        }

        private async Task<bool> HasPostedOrBudgetUsageAsync(Guid unitTypeId, CancellationToken cancellationToken)
        {
            var accountIds = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(account =>
                    account.TenantId == TenantId &&
                    account.UnitTypeId == unitTypeId &&
                    !account.IsDeleted)
                .Select(account => account.Id)
                .ToListAsync(cancellationToken);

            if (accountIds.Count == 0)
                return false;

            var postedJournalUsage = await _unitOfWork.Repository<UnitJournalEntryLine>()
                .GetQueryable(line =>
                    line.TenantId == TenantId &&
                    accountIds.Contains(line.UnitAccountId) &&
                    !line.IsDeleted &&
                    line.UnitJournalEntry != null &&
                    (line.UnitJournalEntry.Status == UnitJournalEntryStatus.Posted ||
                     line.UnitJournalEntry.Status == UnitJournalEntryStatus.Reversed))
                .AnyAsync(cancellationToken);

            if (postedJournalUsage)
                return true;

            return await _unitOfWork.Repository<UnitAccountBudget>()
                .GetQueryable(budget =>
                    budget.TenantId == TenantId &&
                    accountIds.Contains(budget.UnitAccountId) &&
                    !budget.IsDeleted)
                .AnyAsync(cancellationToken);
        }

        public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var unitType = await _unitOfWork.Repository<UnitType>()
                .FirstOrDefaultAsync(ut => ut.Id == id && ut.TenantId == TenantId && !ut.IsDeleted);

            if (unitType == null)
                throw new ArgumentException($"Unit type with ID '{id}' not found.");

            unitType.IsActive = true;
            unitType.UpdatedAt = DateTime.UtcNow;
            unitType.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitType>().UpdateAsync(unitType);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit type {Code} activated by {User}", unitType.Code, UserName);
        }

        public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var unitType = await _unitOfWork.Repository<UnitType>()
                .FirstOrDefaultAsync(ut => ut.Id == id && ut.TenantId == TenantId && !ut.IsDeleted);

            if (unitType == null)
                throw new ArgumentException($"Unit type with ID '{id}' not found.");

            // Check if any unit accounts are using this type
            var hasAccounts = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.TenantId == TenantId && ua.UnitTypeId == id && !ua.IsDeleted)
                .AnyAsync(cancellationToken);

            if (hasAccounts)
                throw new InvalidOperationException($"Cannot deactivate unit type '{unitType.Code}' because it has associated unit accounts.");

            unitType.IsActive = false;
            unitType.UpdatedAt = DateTime.UtcNow;
            unitType.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitType>().UpdateAsync(unitType);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit type {Code} deactivated by {User}", unitType.Code, UserName);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var unitType = await _unitOfWork.Repository<UnitType>()
                .FirstOrDefaultAsync(ut => ut.Id == id && ut.TenantId == TenantId && !ut.IsDeleted);

            if (unitType == null)
                return;

            // Check if any unit accounts are using this type
            var hasAccounts = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.TenantId == TenantId && ua.UnitTypeId == id && !ua.IsDeleted)
                .AnyAsync(cancellationToken);

            if (hasAccounts)
                throw new InvalidOperationException($"Cannot delete unit type '{unitType.Code}' because it has associated unit accounts.");

            // Soft delete
            unitType.IsDeleted = true;
            unitType.DeletedAt = DateTime.UtcNow;
            unitType.DeletedBy = UserName;

            await _unitOfWork.Repository<UnitType>().UpdateAsync(unitType);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit type {Code} deleted by {User}", unitType.Code, UserName);
        }

        private UnitTypeDto MapToDto(UnitType unitType)
        {
            var accountCount = _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.TenantId == TenantId && ua.UnitTypeId == unitType.Id && !ua.IsDeleted)
                .Count();

            return new UnitTypeDto
            {
                Id = unitType.Id,
                Code = unitType.Code,
                Name = unitType.Name,
                Description = unitType.Description,
                DecimalPlaces = unitType.DecimalPlaces,
                RoundingIncrement = unitType.RoundingIncrement,
                IsActive = unitType.IsActive,
                AccountCount = accountCount,
                CreatedAt = unitType.CreatedAt,
                CreatedBy = unitType.CreatedBy,
                UpdatedAt = unitType.UpdatedAt,
                UpdatedBy = unitType.UpdatedBy
            };
        }
    }
}
