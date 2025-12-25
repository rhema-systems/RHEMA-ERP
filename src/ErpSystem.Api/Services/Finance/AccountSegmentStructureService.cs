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
    /// <summary>
    /// Service implementation for managing Account Segment Structures.
    /// Handles the definition and maintenance of segmented Chart of Accounts structure.
    /// </summary>
    public class AccountSegmentStructureService : IAccountSegmentStructureService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<AccountSegmentStructureService> _logger;

        public AccountSegmentStructureService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<AccountSegmentStructureService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;
        private string UserName => _currentUserService.UserName ?? "system";

        public async Task<IReadOnlyList<AccountSegmentStructureDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var segments = await _unitOfWork.Repository<AccountSegmentStructure>()
                .GetQueryable(s => s.TenantId == TenantId && !s.IsDeleted)
                .Include(s => s.LookupValues.Where(v => !v.IsDeleted))
                .OrderBy(s => s.SegmentPosition)
                .ToListAsync(cancellationToken);

            return segments.Select(MapToDto).ToList();
        }

        public async Task<AccountSegmentStructureDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var segment = await _unitOfWork.Repository<AccountSegmentStructure>()
                .GetQueryable(s => s.Id == id && s.TenantId == TenantId && !s.IsDeleted)
                .Include(s => s.LookupValues.Where(v => !v.IsDeleted))
                .FirstOrDefaultAsync(cancellationToken);

            return segment == null ? null : MapToDto(segment);
        }

        public async Task<AccountSegmentStructureDto> CreateAsync(
            AccountSegmentStructureCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate unique segment code
            var existingCode = await _unitOfWork.Repository<AccountSegmentStructure>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.SegmentCode == dto.SegmentCode && !s.IsDeleted);
            if (existingCode != null)
                throw new InvalidOperationException($"A segment with code '{dto.SegmentCode}' already exists.");

            // Validate unique segment position
            var existingPosition = await _unitOfWork.Repository<AccountSegmentStructure>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.SegmentPosition == dto.SegmentPosition && !s.IsDeleted);
            if (existingPosition != null)
                throw new InvalidOperationException($"A segment at position {dto.SegmentPosition} already exists.");

            // Validate only one natural account segment
            if (dto.IsNaturalAccount)
            {
                var existingNatural = await _unitOfWork.Repository<AccountSegmentStructure>()
                    .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.IsNaturalAccount && !s.IsDeleted);
                if (existingNatural != null)
                    throw new InvalidOperationException("Only one segment can be marked as the Natural Account segment.");
            }

            var segment = new AccountSegmentStructure
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SegmentName = dto.SegmentName,
                SegmentCode = dto.SegmentCode,
                SegmentPosition = dto.SegmentPosition,
                SegmentLength = dto.SegmentLength,
                DataType = dto.DataType,
                SeparatorCharacter = dto.SeparatorCharacter,
                LookupTableRequired = dto.LookupTableRequired,
                IsMandatory = dto.IsMandatory,
                IsReportingDimension = dto.IsReportingDimension,
                IsNaturalAccount = dto.IsNaturalAccount,
                IsActive = dto.IsActive,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<AccountSegmentStructure>().AddAsync(segment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Account segment structure '{Code}' created at position {Position}", 
                dto.SegmentCode, dto.SegmentPosition);

            return (await GetByIdAsync(segment.Id, cancellationToken))!;
        }

        public async Task<AccountSegmentStructureDto> UpdateAsync(
            AccountSegmentStructureUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var segment = await _unitOfWork.Repository<AccountSegmentStructure>()
                .FirstOrDefaultAsync(s => s.Id == dto.Id && s.TenantId == TenantId && !s.IsDeleted);

            if (segment == null)
                throw new ArgumentException($"Segment with ID '{dto.Id}' not found.");

            // Check if segment code changed and is unique
            if (segment.SegmentCode != dto.SegmentCode)
            {
                var existingCode = await _unitOfWork.Repository<AccountSegmentStructure>()
                    .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.SegmentCode == dto.SegmentCode && s.Id != dto.Id && !s.IsDeleted);
                if (existingCode != null)
                    throw new InvalidOperationException($"A segment with code '{dto.SegmentCode}' already exists.");
            }

            // Check if position changed and is unique
            if (segment.SegmentPosition != dto.SegmentPosition)
            {
                var existingPosition = await _unitOfWork.Repository<AccountSegmentStructure>()
                    .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.SegmentPosition == dto.SegmentPosition && s.Id != dto.Id && !s.IsDeleted);
                if (existingPosition != null)
                    throw new InvalidOperationException($"A segment at position {dto.SegmentPosition} already exists.");
            }

            // Validate natural account uniqueness
            if (dto.IsNaturalAccount && !segment.IsNaturalAccount)
            {
                var existingNatural = await _unitOfWork.Repository<AccountSegmentStructure>()
                    .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.IsNaturalAccount && s.Id != dto.Id && !s.IsDeleted);
                if (existingNatural != null)
                    throw new InvalidOperationException("Only one segment can be marked as the Natural Account segment.");
            }

            // Update properties
            segment.SegmentName = dto.SegmentName;
            segment.SegmentCode = dto.SegmentCode;
            segment.SegmentPosition = dto.SegmentPosition;
            segment.SegmentLength = dto.SegmentLength;
            segment.DataType = dto.DataType;
            segment.SeparatorCharacter = dto.SeparatorCharacter;
            segment.LookupTableRequired = dto.LookupTableRequired;
            segment.IsMandatory = dto.IsMandatory;
            segment.IsReportingDimension = dto.IsReportingDimension;
            segment.IsNaturalAccount = dto.IsNaturalAccount;
            segment.IsActive = dto.IsActive;
            segment.Description = dto.Description;
            segment.UpdatedAt = DateTime.UtcNow;
            segment.UpdatedBy = UserName;

            await _unitOfWork.Repository<AccountSegmentStructure>().UpdateAsync(segment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Account segment structure '{Code}' updated", dto.SegmentCode);

            return (await GetByIdAsync(segment.Id, cancellationToken))!;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var segment = await _unitOfWork.Repository<AccountSegmentStructure>()
                .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId && !s.IsDeleted);

            if (segment == null)
                return;

            // Check if any accounts use this segment
            var accountsUsingSegment = await _unitOfWork.Repository<AccountSegmentValue>()
                .CountAsync(asv => asv.SegmentStructureId == id && !asv.IsDeleted);

            if (accountsUsingSegment > 0)
            {
                // Deactivate instead of delete
                segment.IsActive = false;
                segment.UpdatedAt = DateTime.UtcNow;
                segment.UpdatedBy = UserName;
                await _unitOfWork.Repository<AccountSegmentStructure>().UpdateAsync(segment);
                _logger.LogWarning("Segment '{Code}' deactivated instead of deleted due to {Count} accounts using it", 
                    segment.SegmentCode, accountsUsingSegment);
            }
            else
            {
                // Safe to soft delete
                segment.IsDeleted = true;
                segment.DeletedAt = DateTime.UtcNow;
                segment.DeletedBy = UserName;
                await _unitOfWork.Repository<AccountSegmentStructure>().UpdateAsync(segment);
                _logger.LogInformation("Segment '{Code}' deleted", segment.SegmentCode);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static AccountSegmentStructureDto MapToDto(AccountSegmentStructure segment)
        {
            return new AccountSegmentStructureDto
            {
                Id = segment.Id,
                TenantId = segment.TenantId,
                SegmentName = segment.SegmentName,
                SegmentCode = segment.SegmentCode,
                SegmentPosition = segment.SegmentPosition,
                SegmentLength = segment.SegmentLength,
                DataType = segment.DataType,
                SeparatorCharacter = segment.SeparatorCharacter,
                LookupTableRequired = segment.LookupTableRequired,
                IsMandatory = segment.IsMandatory,
                IsReportingDimension = segment.IsReportingDimension,
                IsNaturalAccount = segment.IsNaturalAccount,
                IsActive = segment.IsActive,
                Description = segment.Description,
                LookupValuesCount = segment.LookupValues?.Count(v => !v.IsDeleted) ?? 0,
                CanBeModified = true, // TODO: Check if accounts exist using this segment
                RestrictionWarning = null,
                LookupValues = segment.LookupValues?
                    .Where(v => !v.IsDeleted)
                    .OrderBy(v => v.DisplayOrder)
                    .ThenBy(v => v.SegmentValue)
                    .Select(v => new SegmentLookupValueSummaryDto
                    {
                        Id = v.Id,
                        SegmentValue = v.SegmentValue,
                        Description = v.Description,
                        IsActive = v.IsActive,
                        DisplayOrder = v.DisplayOrder
                    })
                    .ToList() ?? new List<SegmentLookupValueSummaryDto>(),
                CreatedBy = segment.CreatedBy,
                CreatedAt = segment.CreatedAt,
                UpdatedBy = segment.UpdatedBy,
                UpdatedAt = segment.UpdatedAt
            };
        }
    }
}
