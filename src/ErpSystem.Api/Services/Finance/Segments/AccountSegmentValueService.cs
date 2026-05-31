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
using ErpSystem.Core.Services.Finance;

namespace ErpSystem.Api.Services.Finance.Segments
{
    /// <summary>
    /// Service implementation for managing Account Segment Values.
    /// Manages the per-account segment assignments.
    /// </summary>
    public class AccountSegmentValueService : IAccountSegmentValueService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<AccountSegmentValueService> _logger;

        public AccountSegmentValueService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<AccountSegmentValueService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;
        private string UserName => _currentUserService.UserName ?? "system";

        public async Task<IReadOnlyList<AccountSegmentValueDto>> GetByAccountAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            var values = await _unitOfWork.Repository<AccountSegmentValue>()
                .GetQueryable(v => v.AccountId == accountId && v.TenantId == TenantId && !v.IsDeleted)
                .Include(v => v.SegmentStructure)
                .Include(v => v.SegmentLookupValue)
                .OrderBy(v => v.SegmentStructure!.SegmentPosition)
                .ToListAsync(cancellationToken);

            return values.Select(MapToDto).ToList();
        }

        public async Task<AccountSegmentValueDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var value = await _unitOfWork.Repository<AccountSegmentValue>()
                .GetQueryable(v => v.Id == id && v.TenantId == TenantId && !v.IsDeleted)
                .Include(v => v.SegmentStructure)
                .Include(v => v.SegmentLookupValue)
                .FirstOrDefaultAsync(cancellationToken);

            return value == null ? null : MapToDto(value);
        }

        public async Task<AccountSegmentValueDto> CreateAsync(
            AccountSegmentValueCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            if (!dto.AccountId.HasValue || dto.AccountId.Value == Guid.Empty)
                throw new ArgumentException("Account ID is required for creating a segment value assignment.", nameof(dto.AccountId));

            var accountId = dto.AccountId.Value;

            // Validate account exists
            var account = await _unitOfWork.Repository<Account>()
                .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == TenantId && !a.IsDeleted);

            if (account == null)
                throw new ArgumentException($"Account with ID '{accountId}' not found.");

            // Validate segment structure exists
            var segment = await _unitOfWork.Repository<AccountSegmentStructure>()
                .FirstOrDefaultAsync(s => s.Id == dto.SegmentStructureId && s.TenantId == TenantId && !s.IsDeleted);

            if (segment == null)
                throw new ArgumentException($"Segment structure with ID '{dto.SegmentStructureId}' not found.");

            // Validate no duplicate position for this account
            var existing = await _unitOfWork.Repository<AccountSegmentValue>()
                .FirstOrDefaultAsync(v => v.AccountId == accountId && 
                    v.SegmentStructure!.SegmentPosition == segment.SegmentPosition && !v.IsDeleted);

            if (existing != null)
                throw new InvalidOperationException($"A segment value already exists at position {segment.SegmentPosition} for this account.");

            var segmentValue = new AccountSegmentValue
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AccountId = accountId,
                SegmentStructureId = dto.SegmentStructureId,
                SegmentValue = dto.SegmentValue,
                SegmentLookupValueId = dto.SegmentLookupValueId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<AccountSegmentValue>().AddAsync(segmentValue);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Account segment value created for account {AccountId}", accountId);

            return (await GetByIdAsync(segmentValue.Id, cancellationToken))!;
        }

        public async Task<AccountSegmentValueDto> UpdateAsync(
            AccountSegmentValueUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var segmentValue = await _unitOfWork.Repository<AccountSegmentValue>()
                .FirstOrDefaultAsync(v => v.Id == dto.Id && v.TenantId == TenantId && !v.IsDeleted);

            if (segmentValue == null)
                throw new ArgumentException($"Account segment value with ID '{dto.Id}' not found.");

            segmentValue.SegmentValue = dto.SegmentValue;
            segmentValue.SegmentLookupValueId = dto.SegmentLookupValueId;
            segmentValue.UpdatedAt = DateTime.UtcNow;
            segmentValue.UpdatedBy = UserName;

            await _unitOfWork.Repository<AccountSegmentValue>().UpdateAsync(segmentValue);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Account segment value {Id} updated", dto.Id);

            return (await GetByIdAsync(segmentValue.Id, cancellationToken))!;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var segmentValue = await _unitOfWork.Repository<AccountSegmentValue>()
                .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId && !v.IsDeleted);

            if (segmentValue == null)
                return;

            segmentValue.IsDeleted = true;
            segmentValue.DeletedAt = DateTime.UtcNow;
            segmentValue.DeletedBy = UserName;

            await _unitOfWork.Repository<AccountSegmentValue>().UpdateAsync(segmentValue);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Account segment value {Id} deleted", id);
        }

        private static AccountSegmentValueDto MapToDto(AccountSegmentValue value)
        {
            return new AccountSegmentValueDto
            {
                Id = value.Id,
                TenantId = value.TenantId,
                AccountId = value.AccountId,
                SegmentStructureId = value.SegmentStructureId,
                SegmentPosition = value.SegmentStructure?.SegmentPosition ?? 0,
                SegmentName = value.SegmentStructure?.SegmentName ?? "",
                SegmentValue = value.SegmentValue,
                SegmentLookupValueId = value.SegmentLookupValueId,
                LookupValueId = value.SegmentLookupValueId,
                SegmentValueDescription = value.SegmentValueDescription,
                LookupValueDescription = value.SegmentLookupValue?.Description,
                CreatedBy = value.CreatedBy,
                CreatedAt = value.CreatedAt,
                UpdatedBy = value.UpdatedBy,
                UpdatedAt = value.UpdatedAt
            };
        }
    }
}
