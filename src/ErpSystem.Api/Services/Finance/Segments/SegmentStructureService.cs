using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Api.Services.Finance.Segments
{
    /// <summary>
    /// Service implementation for managing Segment Structures and Lookup Values.
    /// Handles segmented Chart of Accounts configuration.
    /// </summary>
    public class SegmentStructureService : ISegmentStructureService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SegmentStructureService> _logger;

        public SegmentStructureService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<SegmentStructureService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

        // Segment Structure Operations
        public async Task<IReadOnlyList<SegmentStructureDto>> GetSegmentStructuresAsync(CancellationToken cancellationToken = default)
        {
            var structures = await _unitOfWork.Repository<AccountSegmentStructure>()
                .GetQueryable(s => s.TenantId == TenantId && !s.IsDeleted)
                .Include(s => s.LookupValues.Where(v => !v.IsDeleted))
                .OrderBy(s => s.SegmentPosition)
                .ToListAsync(cancellationToken);

            return structures.Select(s => new SegmentStructureDto
            {
                Id = s.Id,
                SegmentName = s.SegmentName,
                SegmentCode = s.SegmentCode,
                SegmentPosition = s.SegmentPosition,
                SegmentLength = s.SegmentLength,
                DataType = s.DataType,
                SeparatorCharacter = s.SeparatorCharacter,
                LookupTableRequired = s.LookupTableRequired,
                IsRequired = true,
                IsReportingDimension = s.IsReportingDimension,
                IsNaturalAccount = s.IsNaturalAccount,
                IsActive = s.IsActive,
                LifecycleStatus = s.LifecycleStatus.ToString(),
                RowVersion = Convert.ToBase64String(s.RowVersion ?? Array.Empty<byte>()),
                IsSystemDefined = s.IsSystemDefined,
                Description = s.Description,
                LookupValues = s.LookupValues?.Select(lv => new SegmentLookupValueDto
                {
                    Id = lv.Id,
                    TenantId = lv.TenantId,
                    SegmentStructureId = lv.SegmentStructureId,
                    SegmentValue = lv.SegmentValue,
                    Description = lv.Description,
                    DisplayOrder = lv.DisplayOrder,
                    IsActive = lv.IsActive,
                    ParentValueId = lv.ParentValueId,
                    EffectiveDate = lv.EffectiveDate,
                    ExpiryDate = lv.ExpiryDate,
                    Notes = lv.Notes,
                    SegmentName = s.SegmentName,
                    CreatedAt = lv.CreatedAt,
                    UpdatedAt = lv.UpdatedAt,
                    CreatedBy = lv.CreatedBy,
                    UpdatedBy = lv.UpdatedBy
                }).ToList(),
                LookupValueCount = s.LookupValues?.Count(lv => !lv.IsDeleted) ?? 0
            }).ToList();
        }

        public async Task<SegmentStructureDto?> GetSegmentStructureByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var structure = await _unitOfWork.Repository<AccountSegmentStructure>()
                .GetQueryable(s => s.Id == id && s.TenantId == TenantId && !s.IsDeleted)
                .Include(s => s.LookupValues.Where(v => !v.IsDeleted))
                .FirstOrDefaultAsync(cancellationToken);

            if (structure == null) return null;

            return new SegmentStructureDto
            {
                Id = structure.Id,
                SegmentName = structure.SegmentName,
                SegmentCode = structure.SegmentCode,
                SegmentPosition = structure.SegmentPosition,
                SegmentLength = structure.SegmentLength,
                DataType = structure.DataType,
                SeparatorCharacter = structure.SeparatorCharacter,
                LookupTableRequired = structure.LookupTableRequired,
                IsRequired = true,
                IsReportingDimension = structure.IsReportingDimension,
                IsNaturalAccount = structure.IsNaturalAccount,
                IsActive = structure.IsActive,
                LifecycleStatus = structure.LifecycleStatus.ToString(),
                RowVersion = Convert.ToBase64String(structure.RowVersion ?? Array.Empty<byte>()),
                IsSystemDefined = structure.IsSystemDefined,
                Description = structure.Description,
                LookupValues = structure.LookupValues?.Select(lv => new SegmentLookupValueDto
                {
                    Id = lv.Id,
                    TenantId = lv.TenantId,
                    SegmentStructureId = lv.SegmentStructureId,
                    SegmentValue = lv.SegmentValue,
                    Description = lv.Description,
                    DisplayOrder = lv.DisplayOrder,
                    IsActive = lv.IsActive,
                    ParentValueId = lv.ParentValueId,
                    EffectiveDate = lv.EffectiveDate,
                    ExpiryDate = lv.ExpiryDate,
                    Notes = lv.Notes,
                    SegmentName = structure.SegmentName,
                    CreatedAt = lv.CreatedAt,
                    UpdatedAt = lv.UpdatedAt,
                    CreatedBy = lv.CreatedBy,
                    UpdatedBy = lv.UpdatedBy
                }).ToList(),
                LookupValueCount = structure.LookupValues?.Count(lv => !lv.IsDeleted) ?? 0
            };
        }

        public Task<SegmentStructureDto> CreateSegmentStructureAsync(SegmentStructureCreateDto dto, CancellationToken cancellationToken = default)
            => Task.FromException<SegmentStructureDto>(new NotImplementedException("Use AccountSegmentStructureService.CreateAsync instead"));

        public Task<SegmentStructureDto> UpdateSegmentStructureAsync(Guid id, SegmentStructureUpdateDto dto, CancellationToken cancellationToken = default)
            => Task.FromException<SegmentStructureDto>(new NotImplementedException("Use AccountSegmentStructureService.UpdateAsync instead"));

        public Task DeleteSegmentStructureAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromException(new NotImplementedException("Use AccountSegmentStructureService.DeleteAsync instead"));

        // Segment Lookup Value Operations
        public async Task<IReadOnlyList<SegmentLookupValueDto>> GetSegmentLookupValuesAsync(Guid segmentStructureId, CancellationToken cancellationToken = default)
        {
            var values = await _unitOfWork.Repository<SegmentLookupValue>()
                .GetQueryable(v => v.SegmentStructureId == segmentStructureId && v.TenantId == TenantId && !v.IsDeleted)
                .OrderBy(v => v.DisplayOrder)
                .ThenBy(v => v.SegmentValue)
                .ToListAsync(cancellationToken);

            return values.Select(v => new SegmentLookupValueDto
            {
                Id = v.Id,
                TenantId = v.TenantId,
                SegmentStructureId = v.SegmentStructureId,
                SegmentValue = v.SegmentValue,
                Description = v.Description,
                IsActive = v.IsActive,
                DisplayOrder = v.DisplayOrder
            }).ToList();
        }

        public async Task<ReportingSegmentOptionsDto> GetReportingOptionsAsync(
            Guid segmentStructureId,
            string? search,
            int take,
            CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var structureExists = await _unitOfWork.Repository<AccountSegmentStructure>()
                .GetQueryable(s =>
                    s.Id == segmentStructureId &&
                    s.TenantId == tenantId &&
                    !s.IsDeleted &&
                    s.IsActive &&
                    s.IsReportingDimension)
                .AnyAsync(cancellationToken);

            if (!structureExists)
            {
                throw new ArgumentException("The reporting dimension was not found for the current tenant.");
            }

            var boundedTake = Math.Clamp(take, 1, 100);
            var normalizedSearch = search?.Trim();
            var now = DateTime.UtcNow;

            var optionQuery = _unitOfWork.Repository<AccountSegmentValue>()
                .GetQueryable(value =>
                    value.TenantId == tenantId &&
                    value.SegmentStructureId == segmentStructureId &&
                    !value.IsDeleted &&
                    value.EffectiveDate <= now &&
                    (value.EndDate == null || value.EndDate > now) &&
                    value.Account.TenantId == tenantId &&
                    !value.Account.IsDeleted &&
                    value.Account.Status == AccountStatus.Active &&
                    (value.Account.EffectiveDate == null || value.Account.EffectiveDate <= now) &&
                    (value.Account.ExpirationDate == null || value.Account.ExpirationDate > now));

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                optionQuery = optionQuery.Where(value =>
                    value.SegmentValue.Contains(normalizedSearch) ||
                    value.Account.AccountCode.Contains(normalizedSearch) ||
                    value.Account.AccountNumber.Contains(normalizedSearch) ||
                    value.Account.AccountName.Contains(normalizedSearch));
            }

            var options = await optionQuery
                .GroupBy(value => value.SegmentValue)
                .Select(group => new ReportingSegmentOptionDto
                {
                    SegmentValue = group.Key,
                    Description = group
                        .Where(value => value.SegmentValueDescription != null && value.SegmentValueDescription != string.Empty)
                        .Select(value => value.SegmentValueDescription!)
                        .Min() ?? group.Select(value => value.Account.AccountName).Min() ?? group.Key,
                    AccountCombinationCount = group.Select(value => value.AccountId).Distinct().Count()
                })
                .OrderBy(option => option.SegmentValue)
                .Take(boundedTake + 1)
                .ToListAsync(cancellationToken);

            return new ReportingSegmentOptionsDto
            {
                Items = options.Take(boundedTake).ToList(),
                HasMore = options.Count > boundedTake
            };
        }

        public Task<SegmentLookupValueDto> AddSegmentLookupValueAsync(Guid segmentStructureId, SegmentLookupValueCreateDto dto, CancellationToken cancellationToken = default)
            => Task.FromException<SegmentLookupValueDto>(new NotImplementedException("Use SegmentLookupValueService.CreateAsync instead"));

        public Task<SegmentLookupValueDto> UpdateSegmentLookupValueAsync(Guid valueId, SegmentLookupValueUpdateDto dto, CancellationToken cancellationToken = default)
            => Task.FromException<SegmentLookupValueDto>(new NotImplementedException("Use SegmentLookupValueService.UpdateAsync instead"));

        public Task DeleteSegmentLookupValueAsync(Guid valueId, CancellationToken cancellationToken = default)
            => Task.FromException(new NotImplementedException("Use SegmentLookupValueService.DeleteAsync instead"));

        // Account Number Utility Operations
        public async Task<bool> ValidateAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
        {
            var segments = (await GetSegmentStructuresAsync(cancellationToken)).Where(item => item.IsActive).ToList();
            
            // Build expected pattern from segments
            var expectedLength = segments.Sum(s => s.SegmentLength) + segments.Count(s => !string.IsNullOrEmpty(s.SeparatorCharacter));
            
            return accountNumber.Length == expectedLength;
        }

        public async Task<string> ConstructAccountNumberAsync(Dictionary<int, string> segmentValues, CancellationToken cancellationToken = default)
        {
            var segments = (await GetSegmentStructuresAsync(cancellationToken)).Where(item => item.IsActive).ToList();
            var orderedSegments = segments.OrderBy(s => s.SegmentPosition).ToList();
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < orderedSegments.Count; i++)
            {
                var segment = orderedSegments[i];
                if (segmentValues.TryGetValue(segment.SegmentPosition, out var value))
                {
                    sb.Append(value);
                }
                else
                {
                    throw new ArgumentException($"Missing required segment at position {segment.SegmentPosition}");
                }

                // Append separator after each segment except the last
                if (i < orderedSegments.Count - 1 && !string.IsNullOrEmpty(segment.SeparatorCharacter))
                {
                    sb.Append(segment.SeparatorCharacter);
                }
            }

            return sb.ToString();
        }

        public async Task<List<SegmentValueDto>> ParseAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
        {
            var segments = (await GetSegmentStructuresAsync(cancellationToken)).Where(item => item.IsActive).ToList();
            var result = new List<SegmentValueDto>();
            var position = 0;

            foreach (var segment in segments.OrderBy(s => s.SegmentPosition))
            {
                if (position + segment.SegmentLength <= accountNumber.Length)
                {
                    var value = accountNumber.Substring(position, segment.SegmentLength);
                    result.Add(new SegmentValueDto
                    {
                        SegmentPosition = segment.SegmentPosition,
                        SegmentName = segment.SegmentName,
                        SegmentValue = value
                    });
                    position += segment.SegmentLength;

                    if (!string.IsNullOrEmpty(segment.SeparatorCharacter))
                        position++; // Skip separator
                }
            }

            return result;
        }
    }
}
