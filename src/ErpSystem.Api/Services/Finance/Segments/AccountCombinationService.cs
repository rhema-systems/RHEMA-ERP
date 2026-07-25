// FILE: src/ErpSystem.Api/Services/Finance/AccountCombinationService.cs

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
using ErpSystem.Core.Enums;
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Api.Services.Finance.Segments
{
    /// <summary>
    /// Service for automatic segmented account combination generation.
    /// 
    /// KEY DESIGN CONSIDERATIONS:
    /// 1. Segment Position Ordering: Account numbers are constructed using current SegmentPosition
    ///    values, ensuring compatibility with segment reordering feature.
    /// 2. Cartesian Product: Generates all combinations of selected segment values.
    /// 3. Duplicate Detection: Checks existing accounts before creation.
    /// 4. Batch Creation: Uses transaction for atomicity.
    /// </summary>
    public class AccountCombinationService : IAccountCombinationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<AccountCombinationService> _logger;

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
        private string UserName => _currentUser.UserId ?? "system";

        public AccountCombinationService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITenantSettingsService tenantSettingsService,
            ILogger<AccountCombinationService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<List<AccountCombinationPreviewDto>> GenerateCombinationsAsync(
            CombinationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (request.SegmentSelections == null || !request.SegmentSelections.Any())
            {
                return new List<AccountCombinationPreviewDto>();
            }

            // 1. Get segment structures ordered by position (important for segment reordering compatibility)
            var segments = await _unitOfWork.Repository<AccountSegmentStructure>()
                .GetQueryable(s => s.TenantId == TenantId && !s.IsDeleted && s.IsActive)
                .OrderBy(s => s.SegmentPosition)
                .ToListAsync(cancellationToken);

            if (!segments.Any())
            {
                throw new InvalidOperationException("No segment structures configured. Please set up segments first.");
            }

            // 2. Get account separator from finance settings
            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId);
            var separator = settings?.AccountSeparator ?? "-";

            // 3. Get all selected lookup values with their descriptions
            var allSelectedLookupIds = request.SegmentSelections
                .SelectMany(s => s.SelectedLookupValueIds)
                .Distinct()
                .ToList();

            var lookupValues = await _unitOfWork.Repository<SegmentLookupValue>()
                .GetQueryable(lv => allSelectedLookupIds.Contains(lv.Id) && !lv.IsDeleted)
                .Include(lv => lv.SegmentStructure)
                .ToListAsync(cancellationToken);

            var naturalAccountSegment = segments.FirstOrDefault(s => s.IsNaturalAccount);
            if (naturalAccountSegment != null)
            {
                var naturalSelection = request.SegmentSelections
                    .FirstOrDefault(s => s.SegmentStructureId == naturalAccountSegment.Id);

                if (naturalSelection?.SelectedLookupValueIds.Any() == true)
                {
                    var naturalAccounts = await _unitOfWork.Repository<Account>()
                        .GetQueryable(a =>
                            a.TenantId == TenantId &&
                            naturalSelection.SelectedLookupValueIds.Contains(a.Id) &&
                            !a.IsDeleted &&
                            a.Status == AccountStatus.Active)
                        .Include(a => a.SegmentValues)
                        .ToListAsync(cancellationToken);

                    foreach (var account in naturalAccounts)
                    {
                        var segmentValue = account.SegmentValues
                            .FirstOrDefault(sv => sv.SegmentStructureId == naturalAccountSegment.Id && !sv.IsDeleted)
                            ?.SegmentValue;

                        if (string.IsNullOrWhiteSpace(segmentValue))
                        {
                            segmentValue = account.AccountCode;
                        }

                        if (string.IsNullOrWhiteSpace(segmentValue))
                        {
                            segmentValue = account.AccountNumber;
                        }

                        if (string.IsNullOrWhiteSpace(segmentValue))
                        {
                            continue;
                        }

                        lookupValues.Add(new SegmentLookupValue
                        {
                            Id = account.Id,
                            TenantId = TenantId,
                            SegmentStructureId = naturalAccountSegment.Id,
                            SegmentStructure = naturalAccountSegment,
                            SegmentValue = segmentValue,
                            Description = account.AccountName,
                            DisplayOrder = 1,
                            IsActive = true
                        });
                    }
                }
            }

            // 4. Build segment value lists for Cartesian product (ordered by SegmentPosition)
            var segmentValueLists = new List<List<SegmentLookupValue>>();
            
            foreach (var segment in segments)
            {
                var selection = request.SegmentSelections
                    .FirstOrDefault(s => s.SegmentStructureId == segment.Id);
                
                if (selection == null || !selection.SelectedLookupValueIds.Any())
                {
                    if (segment.IsMandatory)
                    {
                        throw new InvalidOperationException(
                            $"Segment '{segment.SegmentName}' is mandatory but no values were selected.");
                    }
                    // Skip optional segments with no selection
                    continue;
                }

                var valuesForSegment = lookupValues
                    .Where(lv => lv.SegmentStructureId == segment.Id &&
                                 selection.SelectedLookupValueIds.Contains(lv.Id))
                    .OrderBy(lv => lv.DisplayOrder)
                    .ThenBy(lv => lv.SegmentValue)
                    .ToList();

                if (!valuesForSegment.Any())
                {
                    throw new InvalidOperationException(
                        $"No valid lookup values found for segment '{segment.SegmentName}'.");
                }

                segmentValueLists.Add(valuesForSegment);
            }

            if (!segmentValueLists.Any())
            {
                return new List<AccountCombinationPreviewDto>();
            }

            // 5. Generate Cartesian product
            var combinations = CartesianProduct(segmentValueLists);

            // 6. Get existing account numbers for duplicate detection
            var existingAccounts = await _unitOfWork.Repository<Account>()
                .GetQueryable(a => a.TenantId == TenantId && !a.IsDeleted)
                .Select(a => new { a.Id, a.AccountNumber })
                .ToListAsync(cancellationToken);

            var existingAccountMap = existingAccounts.ToDictionary(a => a.AccountNumber, a => a.Id);
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            // 7. Build preview DTOs
            var previews = new List<AccountCombinationPreviewDto>();
            
            foreach (var combination in combinations)
            {
                var orderedValues = combination.OrderBy(lv => lv.SegmentStructure.SegmentPosition).ToList();
                var accountNumber = string.Join(separator, orderedValues.Select(lv => lv.SegmentValue));
                var generatedName = GenerateAccountName(orderedValues, naturalAccountSegment);

                var preview = new AccountCombinationPreviewDto
                {
                    AccountNumber = accountNumber,
                    GeneratedName = generatedName,
                    AccountType = request.AccountType,
                    AccountCategory = request.AccountCategory,
                    CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? baseCurrencyCode : request.CurrencyCode.Trim().ToUpperInvariant(),
                    SegmentValues = orderedValues.Select(lv => new SegmentValuePreviewDto
                    {
                        SegmentStructureId = lv.SegmentStructureId,
                        SegmentName = lv.SegmentStructure.SegmentName,
                        SegmentPosition = lv.SegmentStructure.SegmentPosition,
                        LookupValueId = lv.Id,
                        Value = lv.SegmentValue,
                        Description = lv.Description
                    }).ToList()
                };

                // Check for duplicates
                if (existingAccountMap.TryGetValue(accountNumber, out var existingId))
                {
                    preview.Status = CombinationStatus.Duplicate;
                    preview.ExistingAccountId = existingId;
                }
                else
                {
                    preview.Status = CombinationStatus.Valid;
                }

                // Include in preview based on request settings
                if (request.IncludeExistingInPreview || preview.Status == CombinationStatus.Valid)
                {
                    previews.Add(preview);
                }
            }

            _logger.LogInformation(
                "Generated {Count} account combination previews ({Valid} valid, {Duplicate} duplicates)",
                previews.Count,
                previews.Count(p => p.Status == CombinationStatus.Valid),
                previews.Count(p => p.Status == CombinationStatus.Duplicate));

            return previews;
        }

        /// <inheritdoc />
        public async Task<BulkCreationResultDto> BulkCreateAccountsAsync(
            BulkCreateAccountsRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var result = new BulkCreationResultDto
            {
                TotalRequested = request.Combinations.Count
            };

            if (!request.Combinations.Any())
            {
                return result;
            }

            // Filter to only valid combinations (optionally skip duplicates)
            var toCreate = request.Combinations
                .Where(c => c.Status == CombinationStatus.Valid || 
                           (!request.SkipDuplicates && c.Status == CombinationStatus.Duplicate))
                .ToList();

            result.SkipCount = request.Combinations.Count - toCreate.Count;

            // Get required segment structures for building AccountSegmentValue entries
            var segmentStructures = await _unitOfWork.Repository<AccountSegmentStructure>()
                .GetQueryable(s => s.TenantId == TenantId && !s.IsDeleted)
                .ToDictionaryAsync(s => s.Id, cancellationToken);

            // Get finance settings for separator
            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId);
            var separator = settings?.AccountSeparator ?? "-";
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            foreach (var combination in toCreate)
            {
                try
                {
                    // Parse account type
                    if (!Enum.TryParse<AccountType>(combination.AccountType, true, out var accountType))
                    {
                        result.Errors.Add(new BulkCreationErrorDto
                        {
                            AccountNumber = combination.AccountNumber,
                            Error = $"Invalid account type: {combination.AccountType}"
                        });
                        result.ErrorCount++;
                        continue;
                    }

                    // Create the account
                    var account = new Account
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        AccountCode = combination.AccountNumber, // Use account number as code
                        AccountNumber = combination.AccountNumber,
                        AccountName = combination.GeneratedName,
                        AccountType = accountType,
                        AccountCategory = combination.AccountCategory,
                        CurrencyCode = string.IsNullOrWhiteSpace(combination.CurrencyCode) ? baseCurrencyCode : combination.CurrencyCode.Trim().ToUpperInvariant(),
                        IsMultiCurrency = false, // Could be made configurable
                        IsSegmented = true,
                        AllowDirectPosting = true,
                        IsControlAccount = false,
                        BudgetTrackingEnabled = false,
                        Status = AccountStatus.Active,
                        Balance = 0,
                        DebitBalance = 0,
                        CreditBalance = 0,
                        OpeningBalance = 0,
                        IsSystemAccount = false,
                        IsIFRSClassified = true,
                        IsBaseClassified = true,
                        IsLocalClassified = false,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = UserName,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = UserName
                    };

                    await _unitOfWork.Accounts.AddAsync(account);

                    // Create segment value entries
                    foreach (var segValue in combination.SegmentValues)
                    {
                        var accountSegmentValue = new AccountSegmentValue
                        {
                            Id = Guid.NewGuid(),
                            TenantId = TenantId,
                            AccountId = account.Id,
                            SegmentStructureId = segValue.SegmentStructureId,
                            SegmentPosition = segValue.SegmentPosition,
                            SegmentValue = segValue.Value,
                            SegmentValueDescription = segValue.Description,
                            SegmentLookupValueId = segValue.LookupValueId,
                            IsLocked = false,
                            EffectiveDate = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = UserName,
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = UserName
                        };

                        await _unitOfWork.Repository<AccountSegmentValue>().AddAsync(accountSegmentValue);
                    }

                    result.CreatedAccountIds.Add(account.Id);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create account {AccountNumber}", combination.AccountNumber);
                    result.Errors.Add(new BulkCreationErrorDto
                    {
                        AccountNumber = combination.AccountNumber,
                        Error = ex.Message
                    });
                    result.ErrorCount++;
                }
            }

            // Save all changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Bulk created {Success} accounts, skipped {Skip}, errors {Error}",
                result.SuccessCount, result.SkipCount, result.ErrorCount);

            return result;
        }

        #region Private Helpers

        /// <summary>
        /// Generates Cartesian product of multiple lists
        /// </summary>
        private static IEnumerable<List<SegmentLookupValue>> CartesianProduct(
            List<List<SegmentLookupValue>> sequences)
        {
            IEnumerable<List<SegmentLookupValue>> emptyProduct = new[] { new List<SegmentLookupValue>() };

            return sequences.Aggregate(
                emptyProduct,
                (accumulator, sequence) =>
                    accumulator.SelectMany(
                        accseq => sequence,
                        (accseq, item) =>
                        {
                            var result = new List<SegmentLookupValue>(accseq) { item };
                            return result;
                        }));
        }

        /// <summary>
        /// Generates account name from segment values
        /// Pattern: {NaturalAccount} - {Segment1} - {Segment2} ...
        /// </summary>
        private static string GenerateAccountName(
            List<SegmentLookupValue> orderedValues,
            AccountSegmentStructure? naturalAccountSegment)
        {
            if (!orderedValues.Any())
                return string.Empty;

            // Find the natural account value (the "what" - e.g., Cash, Salaries)
            SegmentLookupValue? naturalValue = null;
            if (naturalAccountSegment != null)
            {
                naturalValue = orderedValues.FirstOrDefault(v => 
                    v.SegmentStructureId == naturalAccountSegment.Id);
            }

            // If no natural account segment defined, use last segment as natural account
            naturalValue ??= orderedValues.Last();

            // Build name: start with natural account, then add other segments
            var nameParts = new List<string> { naturalValue.Description };
            nameParts.AddRange(
                orderedValues
                    .Where(v => v.Id != naturalValue.Id)
                    .Select(v => v.Description));

            return string.Join(" - ", nameParts);
        }

        #endregion
    }
}
