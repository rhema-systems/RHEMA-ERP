using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance
{
    public class SegmentConfigurationService : ISegmentConfigurationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public SegmentConfigurationService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        #region Segment Structure Management

        public async Task<SegmentStructureDto> CreateSegmentStructureAsync(SegmentStructureCreateDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                throw new InvalidOperationException("Tenant context is required.");

            // Validate segment position is not already used
            var existingSegment = await _context.Set<AccountSegmentStructure>()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SegmentPosition == dto.SegmentPosition);

            if (existingSegment != null)
                throw new InvalidOperationException($"Segment position {dto.SegmentPosition} is already in use.");

            var segment = new AccountSegmentStructure
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                SegmentName = dto.SegmentName,
                SegmentCode = dto.SegmentCode,
                SegmentPosition = dto.SegmentPosition,
                SegmentLength = dto.SegmentLength,
                SeparatorCharacter = dto.SeparatorCharacter,
                LookupTableRequired = dto.LookupTableRequired,
                IsMandatory = dto.IsMandatory,
                IsReportingDimension = dto.IsReportingDimension,
                IsNaturalAccount = dto.IsNaturalAccount,
                Description = dto.Description,
                IsActive = true
            };

            _context.Set<AccountSegmentStructure>().Add(segment);
            await _context.SaveChangesAsync();

            return MapToDto(segment);
        }

        public async Task<SegmentStructureDto> UpdateSegmentStructureAsync(SegmentStructureUpdateDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                throw new InvalidOperationException("Tenant context is required.");

            var segment = await _context.Set<AccountSegmentStructure>()
                .FirstOrDefaultAsync(s => s.Id == dto.Id && s.TenantId == tenantId);

            if (segment == null)
                throw new ArgumentException($"Segment structure {dto.Id} not found.");

            // Update only allowed fields
            if (dto.SegmentName != null)
                segment.SegmentName = dto.SegmentName;

            if (dto.Description != null)
                segment.Description = dto.Description;

            if (dto.IsReportingDimension.HasValue)
                segment.IsReportingDimension = dto.IsReportingDimension.Value;

            if (dto.IsActive.HasValue)
                segment.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            return MapToDto(segment);
        }

        public async Task<List<SegmentStructureDto>> GetAllSegmentStructuresAsync()
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                throw new InvalidOperationException("Tenant context is required.");

            var segments = await _context.Set<AccountSegmentStructure>()
                .Where(s => s.TenantId == tenantId)
                .Include(s => s.LookupValues)
                .OrderBy(s => s.SegmentPosition)
                .ToListAsync();

            return segments.Select(MapToDto).ToList();
        }

        public async Task<SegmentStructureDto?> GetSegmentStructureAsync(Guid id)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                throw new InvalidOperationException("Tenant context is required.");

            var segment = await _context.Set<AccountSegmentStructure>()
                .Include(s => s.LookupValues)
                .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);

            return segment == null ? null : MapToDto(segment);
        }

        public async Task DeleteSegmentStructureAsync(Guid id)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                throw new InvalidOperationException("Tenant context is required.");

            var segment = await _context.Set<AccountSegmentStructure>()
                .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);

            if (segment == null)
                throw new ArgumentException($"Segment structure {id} not found.");

            // Check if any accounts use this segment
            var accountsUsingSegment = await _context.AccountSegmentValues
                .AnyAsync(v => v.SegmentStructureId == id);

            if (accountsUsingSegment)
                throw new InvalidOperationException("Cannot delete segment structure that is in use by accounts. Inactivate it instead.");

            _context.Set<AccountSegmentStructure>().Remove(segment);
            await _context.SaveChangesAsync();
        }

        #endregion

        #region Account Number Validation and Construction

        public async Task<SegmentedAccountValidationDto> ValidateSegmentedAccountAsync(string accountNumber)
        {
            var result = new SegmentedAccountValidationDto
            {
                AccountNumber = accountNumber,
                IsValid = true
            };

            var parsedSegments = await ParseAccountNumberAsync(accountNumber);
            result.ParsedSegments = parsedSegments;

            // Check for validation errors
            foreach (var segment in parsedSegments)
            {
                if (!segment.IsValid)
                {
                    result.IsValid = false;
                    result.ValidationErrors.Add(segment.ValidationMessage ?? "Unknown validation error");
                }
            }

            return result;
        }

        public async Task<AccountNumberConstructionDto> ConstructAccountNumberAsync(Dictionary<int, string> segmentValues)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                throw new InvalidOperationException("Tenant context is required.");

            var segments = await _context.Set<AccountSegmentStructure>()
                .Where(s => s.TenantId == tenantId && s.IsActive)
                .OrderBy(s => s.SegmentPosition)
                .ToListAsync();

            var accountNumberParts = new List<string>();

            foreach (var segment in segments)
            {
                if (segmentValues.TryGetValue(segment.SegmentPosition, out var value))
                {
                    // Validate length
                    if (value.Length > segment.SegmentLength)
                        throw new ArgumentException($"Segment {segment.SegmentName} value '{value}' exceeds maximum length of {segment.SegmentLength}");

                    // Pad to required length if needed
                    value = value.PadRight(segment.SegmentLength, '0');
                    accountNumberParts.Add(value);
                }
                else if (segment.IsMandatory)
                {
                    throw new ArgumentException($"Mandatory segment {segment.SegmentName} (position {segment.SegmentPosition}) is missing");
                }
            }

            var accountNumber = string.Join(segments.FirstOrDefault()?.SeparatorCharacter ?? "-", accountNumberParts);

            return new AccountNumberConstructionDto
            {
                SegmentValues = segmentValues,
                ConstructedAccountNumber = accountNumber
            };
        }

        public async Task<List<SegmentValueDto>> ParseAccountNumberAsync(string accountNumber)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                throw new InvalidOperationException("Tenant context is required.");

            var segments = await _context.Set<AccountSegmentStructure>()
                .Where(s => s.TenantId == tenantId && s.IsActive)
                .OrderBy(s => s.SegmentPosition)
                .ToListAsync();

            if (!segments.Any())
                return new List<SegmentValueDto>();

            var separator = segments.FirstOrDefault()?.SeparatorCharacter ?? "-";
            var parts = accountNumber.Split(separator);

            var result = new List<SegmentValueDto>();

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var segmentDto = new SegmentValueDto
                {
                    SegmentPosition = segment.SegmentPosition,
                    SegmentName = segment.SegmentName,
                    IsValid = true
                };

                if (i < parts.Length)
                {
                    segmentDto.SegmentValue = parts[i];

                    // Validate length
                    if (parts[i].Length != segment.SegmentLength)
                    {
                        segmentDto.IsValid = false;
                        segmentDto.ValidationMessage = $"Segment {segment.SegmentName} should be {segment.SegmentLength} characters, got {parts[i].Length}";
                    }
                }
                else if (segment.IsMandatory)
                {
                    segmentDto.IsValid = false;
                    segmentDto.ValidationMessage = $"Mandatory segment {segment.SegmentName} is missing";
                }

                result.Add(segmentDto);
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private SegmentStructureDto MapToDto(AccountSegmentStructure segment)
        {
            return new SegmentStructureDto
            {
                Id = segment.Id,
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
                LookupValueCount = segment.LookupValues?.Count ?? 0
            };
        }

        #endregion
    }
}
