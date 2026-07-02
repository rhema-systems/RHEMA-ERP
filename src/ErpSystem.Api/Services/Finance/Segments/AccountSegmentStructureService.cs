using System;
using System.Text;
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

namespace ErpSystem.Api.Services.Finance.Segments
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

        public async Task ReorderSegmentsAsync(List<ReorderSegmentDto> reorderList, CancellationToken cancellationToken = default)
        {
            if (reorderList == null || !reorderList.Any())
                throw new ArgumentException("Reorder list cannot be empty.");

            // 1. Fetch all active segments
            var segments = await _unitOfWork.Repository<AccountSegmentStructure>()
                .GetQueryable(s => s.TenantId == TenantId && !s.IsDeleted)
                .ToListAsync(cancellationToken);

            // 2. Validation
            var distinctPositions = reorderList.Select(x => x.NewPosition).Distinct().ToList();
            if (distinctPositions.Count != reorderList.Count)
                throw new InvalidOperationException("Duplicate positions found in reorder list.");

            if (distinctPositions.Min() != 1 || distinctPositions.Max() != segments.Count)
                throw new InvalidOperationException($"Positions must be sequential from 1 to {segments.Count}.");

            if (reorderList.Count != segments.Count)
                 throw new InvalidOperationException("Reorder list must contain all active segments.");

            // 3. Update Structure Positions
            foreach (var item in reorderList)
            {
                var segment = segments.FirstOrDefault(s => s.Id == item.SegmentId);
                if (segment == null)
                    throw new KeyNotFoundException($"Segment with ID {item.SegmentId} not found.");

                segment.SegmentPosition = item.NewPosition;
                segment.UpdatedAt = DateTime.UtcNow;
                segment.UpdatedBy = UserName;
                await _unitOfWork.Repository<AccountSegmentStructure>().UpdateAsync(segment);
            }
            
            // Save structure changes first
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 4. Update AccountSegmentValue positions
            // Efficient bulk update using raw SQL desirable, but falling back to EF core loading for safety/portability
            // given we also need to regenerate AccountNumber strings.
            
            // Getting separator for Account Number generation
            var financeSettings = await _unitOfWork.Repository<FinanceSettings>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId);
            var separator = financeSettings?.AccountSeparator ?? "-";

            // Fetch ALL accounts with their segment values
            // PERFORMANCE WARNING: fine for < 10k accounts. For larger datasets, move to Stored Procedure/SQL.
            var accounts = await _unitOfWork.Repository<Account>()
                .GetQueryable(a => a.TenantId == TenantId && !a.IsDeleted)
                .Include(a => a.SegmentValues.Where(sv => !sv.IsDeleted))
                .ToListAsync(cancellationToken);

            var accountsUpdated = 0;
            var accountsSkipped = 0;

            foreach (var account in accounts)
            {
                // Skip accounts with no segment values - preserve their existing code
                if (account.SegmentValues == null || !account.SegmentValues.Any())
                {
                    _logger.LogWarning("Account {AccountId} ({AccountCode}) has no segment values - skipping reorder",
                        account.Id, account.AccountCode);
                    accountsSkipped++;
                    continue;
                }

                foreach (var segValue in account.SegmentValues)
                {
                    // Update the cached position from the structure
                    var structure = segments.FirstOrDefault(s => s.Id == segValue.SegmentStructureId);
                    if (structure != null)
                    {
                        segValue.SegmentPosition = structure.SegmentPosition;
                    }
                }

                // Regenerate AccountNumber from segment values
                var orderedSegmentValues = account.SegmentValues
                    .OrderBy(v => v.SegmentPosition)
                    .ToList();

                var sb = new StringBuilder();
                for (int i = 0; i < orderedSegmentValues.Count; i++)
                {
                    var val = orderedSegmentValues[i];
                    sb.Append(val.SegmentValue);

                    // Add separator if not the last segment
                    if (i < orderedSegmentValues.Count - 1)
                    {
                        var structure = segments.FirstOrDefault(s => s.Id == val.SegmentStructureId);
                        // Use segment-specific separator if defined, otherwise fallback to global setting
                        var sep = !string.IsNullOrEmpty(structure?.SeparatorCharacter)
                            ? structure.SeparatorCharacter
                            : separator;
                        sb.Append(sep);
                    }
                }

                var newAccountCode = sb.ToString();

                // Only update if we have a valid new code
                if (string.IsNullOrWhiteSpace(newAccountCode))
                {
                    _logger.LogWarning("Account {AccountId} ({AccountCode}) generated empty code from segments - skipping",
                        account.Id, account.AccountCode);
                    accountsSkipped++;
                    continue;
                }

                // Debug logging for the first account to verify logic
                if (accountsUpdated == 0)
                {
                    _logger.LogInformation("Reordering validation - First Account: OldCode={OldCode}, OldNumber={OldNumber}, NewCode={NewCode}",
                        account.AccountCode, account.AccountNumber, newAccountCode);
                    _logger.LogInformation("Segments order: {Order}", string.Join(", ", orderedSegmentValues.Select(v => $"{v.SegmentValue} (Pos: {v.SegmentPosition})")));
                }

                // Update both AccountCode and AccountNumber to reflect new segment order
                account.AccountCode = newAccountCode;
                account.AccountNumber = newAccountCode;
                account.UpdatedAt = DateTime.UtcNow;
                accountsUpdated++;

                // No need to call UpdateAsync explicitly for tracked entities, but doing it for safety if repository requires
                await _unitOfWork.Accounts.UpdateAsync(account);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Segments reordered: {Updated} accounts updated, {Skipped} accounts skipped (no segment values)",
                accountsUpdated, accountsSkipped);
        }

        public async Task RegenerateAccountNumbersAsync(CancellationToken cancellationToken = default)
        {
            var segments = await _unitOfWork.Repository<AccountSegmentStructure>()
                .GetQueryable(s => s.TenantId == TenantId && !s.IsDeleted)
                .ToListAsync(cancellationToken);

            var financeSettings = await _unitOfWork.Repository<FinanceSettings>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId);
            var separator = financeSettings?.AccountSeparator ?? "-";

            var accounts = await _unitOfWork.Repository<Account>()
                .GetQueryable(a => a.TenantId == TenantId && !a.IsDeleted)
                .Include(a => a.SegmentValues.Where(sv => !sv.IsDeleted))
                .ToListAsync(cancellationToken);

            var accountsUpdated = 0;
            var accountsSkipped = 0;

            foreach (var account in accounts)
            {
                // Skip accounts with no segment values - preserve their existing code
                if (account.SegmentValues == null || !account.SegmentValues.Any())
                {
                    _logger.LogWarning("Account {AccountId} ({AccountCode}) has no segment values - skipping regeneration",
                        account.Id, account.AccountCode);
                    accountsSkipped++;
                    continue;
                }

                // Ensure positions are consistent with structure
                foreach (var segValue in account.SegmentValues)
                {
                    var structure = segments.FirstOrDefault(s => s.Id == segValue.SegmentStructureId);
                    if (structure != null)
                    {
                        segValue.SegmentPosition = structure.SegmentPosition;
                    }
                }

                var orderedSegmentValues = account.SegmentValues
                    .OrderBy(v => v.SegmentPosition)
                    .ToList();

                var sb = new StringBuilder();
                for (int i = 0; i < orderedSegmentValues.Count; i++)
                {
                    var val = orderedSegmentValues[i];
                    sb.Append(val.SegmentValue);

                    if (i < orderedSegmentValues.Count - 1)
                    {
                        var structure = segments.FirstOrDefault(s => s.Id == val.SegmentStructureId);
                        var sep = !string.IsNullOrEmpty(structure?.SeparatorCharacter)
                            ? structure.SeparatorCharacter
                            : separator;
                        sb.Append(sep);
                    }
                }

                var newAccountCode = sb.ToString();

                // Only update if we have a valid new code
                if (string.IsNullOrWhiteSpace(newAccountCode))
                {
                    _logger.LogWarning("Account {AccountId} ({AccountCode}) generated empty code from segments - skipping",
                        account.Id, account.AccountCode);
                    accountsSkipped++;
                    continue;
                }

                // Update both AccountCode and AccountNumber to reflect current segment structure
                account.AccountCode = newAccountCode;
                account.AccountNumber = newAccountCode;
                account.UpdatedAt = DateTime.UtcNow;
                accountsUpdated++;
                _logger.LogDebug("Regenerated Account: {Id} -> Code={Code}", account.Id, account.AccountCode);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Regenerated account codes: {Updated} updated, {Skipped} skipped (no segment values)",
                accountsUpdated, accountsSkipped);
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
                CreatedBy = segment.CreatedBy ?? "system",
                CreatedAt = segment.CreatedAt,
                UpdatedBy = segment.UpdatedBy,
                UpdatedAt = segment.UpdatedAt
            };
        }
    }
}
