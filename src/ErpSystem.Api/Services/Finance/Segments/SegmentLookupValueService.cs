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
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Api.Services.Finance.Segments
{
    /// <summary>
    /// Service implementation for managing Segment Lookup Values.
    /// </summary>
    public class SegmentLookupValueService : ISegmentLookupValueService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SegmentLookupValueService> _logger;

        public SegmentLookupValueService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<SegmentLookupValueService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";

        public async Task<IReadOnlyList<SegmentLookupValueDto>> GetBySegmentStructureAsync(
            Guid segmentStructureId,
            CancellationToken cancellationToken = default)
        {
            var values = await _unitOfWork.Repository<SegmentLookupValue>()
                .GetQueryable(v => v.SegmentStructureId == segmentStructureId && v.TenantId == TenantId && !v.IsDeleted)
                .Include(v => v.SegmentStructure)
                .OrderBy(v => v.DisplayOrder)
                .ThenBy(v => v.SegmentValue)
                .ToListAsync(cancellationToken);

            return values.Select(MapToDto).ToList();
        }

        public async Task<SegmentLookupValueDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var value = await _unitOfWork.Repository<SegmentLookupValue>()
                .GetQueryable(v => v.Id == id && v.TenantId == TenantId && !v.IsDeleted)
                .Include(v => v.SegmentStructure)
                .FirstOrDefaultAsync(cancellationToken);

            return value == null ? null : MapToDto(value);
        }

        public async Task<SegmentLookupValueDto> CreateAsync(
            SegmentLookupValueCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate segment structure exists and requires lookup
            var segment = await _unitOfWork.Repository<AccountSegmentStructure>()
                .FirstOrDefaultAsync(s => s.Id == dto.SegmentStructureId && s.TenantId == TenantId && !s.IsDeleted);

            if (segment == null)
                throw new ArgumentException($"Segment structure with ID '{dto.SegmentStructureId}' not found.");

            if (!segment.LookupTableRequired)
                throw new InvalidOperationException($"Segment '{segment.SegmentName}' does not require lookup values.");

            // Validate unique segment value
            var existing = await _unitOfWork.Repository<SegmentLookupValue>()
                .FirstOrDefaultAsync(v => v.SegmentStructureId == dto.SegmentStructureId && 
                    v.SegmentValue == dto.SegmentValue && !v.IsDeleted);

            if (existing != null)
                throw new InvalidOperationException($"Segment value '{dto.SegmentValue}' already exists for this segment.");

            // Validate length
            if (dto.SegmentValue.Length != segment.SegmentLength)
                throw new ArgumentException($"Segment value must be exactly {segment.SegmentLength} characters long.");

            var lookupValue = new SegmentLookupValue
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SegmentStructureId = dto.SegmentStructureId,
                SegmentValue = dto.SegmentValue,
                Description = dto.Description,
                ParentValueId = dto.ParentValueId,
                EffectiveDate = dto.EffectiveDate ?? DateTime.UtcNow,
                ExpiryDate = dto.ExpiryDate,
                IsActive = dto.IsActive,
                DisplayOrder = dto.DisplayOrder,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<SegmentLookupValue>().AddAsync(lookupValue);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Segment lookup value '{Value}' created for segment '{SegmentId}'", 
                dto.SegmentValue, dto.SegmentStructureId);

            return (await GetByIdAsync(lookupValue.Id, cancellationToken))!;
        }

        public async Task<SegmentLookupValueDto> UpdateAsync(
            SegmentLookupValueUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var lookupValue = await _unitOfWork.Repository<SegmentLookupValue>()
                .GetQueryable(v => v.Id == dto.Id && v.TenantId == TenantId && !v.IsDeleted)
                .Include(v => v.SegmentStructure)
                .FirstOrDefaultAsync(cancellationToken);

            if (lookupValue == null)
                throw new ArgumentException($"Segment lookup value with ID '{dto.Id}' not found.");

            // Check if value changed and is unique
            if (lookupValue.SegmentValue != dto.SegmentValue)
            {
                var existing = await _unitOfWork.Repository<SegmentLookupValue>()
                    .FirstOrDefaultAsync(v => v.SegmentStructureId == lookupValue.SegmentStructureId && 
                        v.SegmentValue == dto.SegmentValue && v.Id != dto.Id && !v.IsDeleted);

                if (existing != null)
                    throw new InvalidOperationException($"Segment value '{dto.SegmentValue}' already exists.");

                // Validate length
                if (dto.SegmentValue.Length != lookupValue.SegmentStructure!.SegmentLength)
                    throw new ArgumentException($"Segment value must be exactly {lookupValue.SegmentStructure.SegmentLength} characters long.");
            }

            lookupValue.SegmentValue = dto.SegmentValue;
            lookupValue.Description = dto.Description;
            lookupValue.ParentValueId = dto.ParentValueId;
            lookupValue.EffectiveDate = dto.EffectiveDate ?? lookupValue.EffectiveDate;
            lookupValue.ExpiryDate = dto.ExpiryDate;
            lookupValue.IsActive = dto.IsActive;
            lookupValue.DisplayOrder = dto.DisplayOrder;
            lookupValue.Notes = dto.Notes;
            lookupValue.UpdatedAt = DateTime.UtcNow;
            lookupValue.UpdatedBy = UserName;

            await _unitOfWork.Repository<SegmentLookupValue>().UpdateAsync(lookupValue);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Segment lookup value '{Value}' updated", dto.SegmentValue);

            return (await GetByIdAsync(lookupValue.Id, cancellationToken))!;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var lookupValue = await _unitOfWork.Repository<SegmentLookupValue>()
                .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId && !v.IsDeleted);

            if (lookupValue == null)
                return;

            // Check if any accounts use this value
            var accountsUsing = await _unitOfWork.Repository<AccountSegmentValue>()
                .CountAsync(asv => asv.SegmentLookupValueId == id && !asv.IsDeleted);

            if (accountsUsing > 0)
            {
                // Deactivate instead of delete
                lookupValue.IsActive = false;
                lookupValue.UpdatedAt = DateTime.UtcNow;
                lookupValue.UpdatedBy = UserName;
                await _unitOfWork.Repository<SegmentLookupValue>().UpdateAsync(lookupValue);
                _logger.LogWarning("Segment value '{Value}' deactivated instead of deleted due to {Count} accounts using it", 
                    lookupValue.SegmentValue, accountsUsing);
            }
            else
            {
                lookupValue.IsDeleted = true;
                lookupValue.DeletedAt = DateTime.UtcNow;
                lookupValue.DeletedBy = UserName;
                await _unitOfWork.Repository<SegmentLookupValue>().UpdateAsync(lookupValue);
                _logger.LogInformation("Segment value '{Value}' deleted", lookupValue.SegmentValue);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static SegmentLookupValueDto MapToDto(SegmentLookupValue value)
        {
            return new SegmentLookupValueDto
            {
                Id = value.Id,
                TenantId = value.TenantId,
                SegmentStructureId = value.SegmentStructureId,
                SegmentName = value.SegmentStructure?.SegmentName ?? "",
                SegmentValue = value.SegmentValue,
                Description = value.Description,
                ParentValueId = value.ParentValueId,
                EffectiveDate = value.EffectiveDate,
                ExpiryDate = value.ExpiryDate,
                IsActive = value.IsActive,
                DisplayOrder = value.DisplayOrder,
                Notes = value.Notes,
                CreatedBy = value.CreatedBy,
                CreatedAt = value.CreatedAt,
                UpdatedBy = value.UpdatedBy,
                UpdatedAt = value.UpdatedAt
            };
        }
    }
}
