using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed partial class QuantitySurveyRateLibraryService
{
    private static readonly HashSet<QuantitySurveyRateComponent> QuantityComponents =
    [
        QuantitySurveyRateComponent.Material,
        QuantitySurveyRateComponent.Labour,
        QuantitySurveyRateComponent.Plant,
        QuantitySurveyRateComponent.Equipment,
        QuantitySurveyRateComponent.Subcontract,
        QuantitySurveyRateComponent.Transport,
        QuantitySurveyRateComponent.Other
    ];

    private static readonly HashSet<QuantitySurveyRateComponent> PercentageComponents =
    [
        QuantitySurveyRateComponent.Overhead,
        QuantitySurveyRateComponent.Profit,
        QuantitySurveyRateComponent.Attendance,
        QuantitySurveyRateComponent.Contingency,
        QuantitySurveyRateComponent.Wastage
    ];

    public async Task<QuantitySurveyRateBuildUpContextDto> GetRateBuildUpContextAsync(
        Guid itemId,
        DateTime sourceDate,
        DateTime effectiveAt,
        Guid currencyId,
        Guid? projectTypeId = null,
        Guid? locationId = null,
        Guid? businessPartnerId = null,
        CancellationToken cancellationToken = default)
    {
        var item = await FindItemAsync(itemId, tracking: false, cancellationToken);
        var sourceAt = Utc(sourceDate);
        var effectiveAtUtc = Utc(effectiveAt);
        if (sourceAt == default) throw new QuantitySurveyRateLibraryValidationException("Select the source-rate date.");
        if (effectiveAtUtc == default) throw new QuantitySurveyRateLibraryValidationException("Select the effective date.");
        if (sourceAt > effectiveAtUtc)
            throw new QuantitySurveyRateLibraryValidationException("Source-rate date must be on or before the effective date.");
        if (currencyId == Guid.Empty) throw new QuantitySurveyRateLibraryValidationException("Select an active Finance currency.");

        var currency = await db.Currencies.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == currencyId && !value.IsDeleted && value.Status == "Active",
            cancellationToken)
            ?? throw new QuantitySurveyRateLibraryValidationException("Select an active Finance currency available to this tenant.");
        var policy = await GetBuildUpPolicyAsync(effectiveAtUtc, cancellationToken);
        var ratePolicy = await GetPolicyAsync(effectiveAtUtc, cancellationToken);
        var requireProjectType = ratePolicy.Dimensions.Contains(QuantitySurveyRateDimension.ProjectType);
        var requireLocation = ratePolicy.Dimensions.Contains(QuantitySurveyRateDimension.Location);
        var allowBusinessPartner = ratePolicy.Dimensions.Contains(QuantitySurveyRateDimension.Contractor) ||
            ratePolicy.Dimensions.Contains(QuantitySurveyRateDimension.Supplier);
        var sources = await GetEligibleBuildUpSourcesAsync(
            itemId, sourceAt, currency.Id, projectTypeId, locationId, businessPartnerId, cancellationToken);

        return new QuantitySurveyRateBuildUpContextDto
        {
            AllowedComponents = policy.Value.Components.OrderBy(value => value).ToList(),
            RequireProjectType = requireProjectType,
            RequireLocation = requireLocation,
            AllowBusinessPartner = allowBusinessPartner,
            RequireBusinessPartner = item.Category == QuantitySurveyRateItemCategory.Subcontract &&
                ratePolicy.Dimensions.Contains(QuantitySurveyRateDimension.Contractor),
            AllowedProjectTypeIds = requireProjectType
                ? ratePolicy.ProjectTypeIds.OrderBy(value => value).ToList()
                : [],
            AllowedLocationIds = requireLocation
                ? ratePolicy.LocationIds.OrderBy(value => value).ToList()
                : [],
            MaximumOverheadPercent = policy.Value.MaximumOverheadPercent,
            MaximumProfitPercent = policy.Value.MaximumProfitPercent,
            MaximumContingencyPercent = policy.Value.MaximumContingencyPercent,
            MaximumWastagePercent = policy.Value.MaximumWastagePercent,
            DecimalPlaces = policy.Value.DecimalPlaces,
            Sources = sources.Select(MapBuildUpSource).ToList()
        };
    }

    public async Task<IReadOnlyList<QuantitySurveyRateBuildUpDto>> GetRateBuildUpsAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        _ = await FindItemAsync(itemId, tracking: false, cancellationToken);
        var values = await BuildUpQuery()
            .Where(value => value.TenantId == TenantId && value.RateLibraryItemId == itemId && !value.IsDeleted)
            .OrderByDescending(value => value.Version)
            .ToListAsync(cancellationToken);
        return values.Select(MapBuildUp).ToList();
    }

    public async Task<QuantitySurveyRateBuildUpPreviewDto> PreviewRateBuildUpAsync(
        Guid itemId,
        PreviewQuantitySurveyRateBuildUpRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await FindItemAsync(itemId, tracking: false, cancellationToken);
        return await BuildRateBuildUpPreviewAsync(item, request, cancellationToken);
    }

    public async Task<QuantitySurveyRateBuildUpDto> PrepareRateBuildUpAsync(
        Guid itemId,
        PrepareQuantitySurveyRateBuildUpRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw new QuantitySurveyRateLibraryValidationException("A client request ID is required for safe retry.");

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            var existing = await BuildUpQuery(ignoreQueryFilters: true)
                .SingleOrDefaultAsync(value => value.TenantId == TenantId &&
                    value.ClientRequestId == request.ClientRequestId, cancellationToken);
            if (existing is not null)
            {
                if (existing.IsDeleted)
                    throw new QuantitySurveyRateLibraryConflictException(
                        "This rate build-up request was previously archived and cannot be reused.");
                if (existing.RateLibraryItemId != itemId)
                    throw new QuantitySurveyRateLibraryConflictException(
                        "The client request ID was already used for a different rate-library item.");
                if (!FixedHashEquals(existing.CalculationHash, request.PreviewIntegrityHash))
                    throw new QuantitySurveyRateLibraryConflictException(
                        "The client request ID was already used for a different rate build-up.");
                await transaction.CommitAsync(cancellationToken);
                return MapBuildUp(existing);
            }

            var item = await FindItemAsync(itemId, tracking: false, cancellationToken);
            var preview = await BuildRateBuildUpPreviewAsync(item, request, cancellationToken);
            if (!FixedHashEquals(preview.IntegrityHash, request.PreviewIntegrityHash))
                throw new QuantitySurveyRateLibraryConflictException(
                    "The build-up policy or a component rate changed after preview. Refresh and review the calculation again.");

            var version = (await db.QuantitySurveyRateBuildUps.IgnoreQueryFilters()
                .Where(value => value.TenantId == TenantId && value.RateLibraryItemId == itemId)
                .MaxAsync(value => (int?)value.Version, cancellationToken) ?? 0) + 1;
            var now = DateTime.UtcNow;
            var reason = Required(request.ChangeReason, "Change reason", 1000);
            CentralDocumentVersion? evidence = null;
            if (request.CentralDocumentVersionId.HasValue)
            {
                evidence = await db.CentralDocumentVersions.AsNoTracking()
                    .Include(value => value.DocumentRecord)
                    .Where(CentralDocumentEvidenceRules.CurrentPublished())
                    .SingleOrDefaultAsync(value => value.TenantId == TenantId &&
                        value.Id == request.CentralDocumentVersionId.Value, cancellationToken)
                    ?? throw new QuantitySurveyRateLibraryValidationException(
                        "Select a current published evidence version from the central DMS.");
            }

            var buildUp = new QuantitySurveyRateBuildUp
            {
                TenantId = TenantId,
                RateLibraryItemId = itemId,
                Version = version,
                ClientRequestId = request.ClientRequestId,
                BuildUpNumber = $"RBU-{item.Code}-{version:0000}",
                SourceDate = preview.SourceDate,
                CurrencyId = preview.CurrencyId,
                CurrencyCodeSnapshot = preview.CurrencyCode,
                ConfigurationProfileId = preview.ConfigurationProfileId,
                ConfigurationDecisionId = preview.ConfigurationDecisionId,
                ConfigurationProfileVersion = preview.ConfigurationProfileVersion,
                DecimalPlaces = preview.DecimalPlaces,
                MaterialSubtotal = preview.MaterialSubtotal,
                DirectCost = preview.DirectCost,
                AddOnCost = preview.AddOnCost,
                UnitRate = preview.UnitRate,
                CalculationHash = preview.IntegrityHash,
                CentralDocumentRecordId = evidence?.DocumentRecordId,
                CentralDocumentVersionId = evidence?.Id,
                ChangeReason = reason,
                PreparedById = UserId,
                PreparedAt = now,
                AuditAction = QuantitySurveyAuditEventMap.CreateRateBuildUp,
                CorrelationId = RequiredCorrelation(correlationId),
                ActorRoles = ActorRoles,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = UserId
            };
            buildUp.Lines = preview.Lines.Select(line => new QuantitySurveyRateBuildUpLine
            {
                TenantId = TenantId,
                RateBuildUpId = buildUp.Id,
                Sequence = line.Sequence,
                Component = line.Component,
                CalculationMethod = line.CalculationMethod,
                PercentageBasis = line.PercentageBasis,
                Description = line.Description,
                SourceRateLibraryItemId = line.SourceRateLibraryItemId,
                SourceRateId = line.SourceRateId,
                SourceItemCodeSnapshot = line.SourceItemCode,
                SourceItemNameSnapshot = line.SourceItemName,
                SourceUnitOfMeasureSnapshot = line.SourceUnitOfMeasure,
                SourceRateVersion = line.SourceRateVersion,
                SourceUnitRate = line.SourceUnitRate,
                InputQuantity = line.Quantity,
                InputPercentage = line.Percentage,
                InputFixedAmount = line.FixedAmount,
                BasisAmount = line.BasisAmount,
                CalculatedAmount = line.CalculatedAmount,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = UserId
            }).ToList();
            db.QuantitySurveyRateBuildUps.Add(buildUp);

            var previous = await db.QuantitySurveyRateLibraryRates.AsNoTracking()
                .Where(value => value.TenantId == TenantId && value.RateLibraryItemId == itemId &&
                    !value.IsDeleted && value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published &&
                    value.EffectiveFrom < Utc(request.EffectiveFrom) &&
                    value.ProjectTypeId == request.ProjectTypeId && value.LocationId == request.LocationId &&
                    value.BusinessPartnerId == request.BusinessPartnerId)
                .OrderByDescending(value => value.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken);
            var rateRequest = new SaveQuantitySurveyRateRequest
            {
                UnitRate = preview.UnitRate,
                CurrencyId = preview.CurrencyId,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                ProjectTypeId = request.ProjectTypeId,
                LocationId = request.LocationId,
                BusinessPartnerId = request.BusinessPartnerId,
                SourceType = QuantitySurveyRateSourceType.RateBuildUp,
                SourceReference = buildUp.BuildUpNumber,
                SourceDate = request.SourceDate,
                CentralDocumentVersionId = request.CentralDocumentVersionId,
                ChangeReason = reason
            };
            var before = new
            {
                PreviousRate = previous is null ? null : (object?)Snapshot(previous),
                Preview = preview
            };
            _ = await CreateRateCoreAsync(
                item,
                rateRequest,
                QuantitySurveyAuditEventMap.CreateRateBuildUp,
                before,
                marketSurvey: null,
                historical: null,
                buildUp: new RateBuildUpPreparation(
                    buildUp.Id,
                    previous?.Id,
                    previous?.UnitRate,
                    previous?.CurrencyCodeSnapshot,
                    preview),
                correlationId: correlationId,
                cancellationToken: cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return MapBuildUp(await BuildUpQuery()
                .SingleAsync(value => value.TenantId == TenantId && value.Id == buildUp.Id && !value.IsDeleted,
                    cancellationToken));
        });
    }

    private async Task<QuantitySurveyRateBuildUpPreviewDto> BuildRateBuildUpPreviewAsync(
        QuantitySurveyRateLibraryItem item,
        PreviewQuantitySurveyRateBuildUpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Lines.Count is < 1 or > 250)
            throw new QuantitySurveyRateLibraryValidationException("A rate build-up requires between 1 and 250 component lines.");
        if (request.Lines.Any(value => value.Sequence <= 0) ||
            request.Lines.GroupBy(value => value.Sequence).Any(group => group.Count() > 1))
            throw new QuantitySurveyRateLibraryValidationException("Every component line requires a unique positive sequence.");

        var sourceDate = Utc(request.SourceDate);
        var effectiveFrom = Utc(request.EffectiveFrom);
        if (sourceDate == default) throw new QuantitySurveyRateLibraryValidationException("Source-rate date is required.");
        if (effectiveFrom == default) throw new QuantitySurveyRateLibraryValidationException("Effective from is required.");
        if (sourceDate > effectiveFrom)
            throw new QuantitySurveyRateLibraryValidationException("Source-rate date must be on or before the effective date.");

        var policy = await GetBuildUpPolicyAsync(effectiveFrom, cancellationToken);
        var sources = await GetEligibleBuildUpSourcesAsync(
            item.Id,
            sourceDate,
            request.CurrencyId,
            request.ProjectTypeId,
            request.LocationId,
            request.BusinessPartnerId,
            cancellationToken);
        var sourceByRate = sources.ToDictionary(value => value.Id);

        var directInputs = request.Lines
            .Where(value => value.CalculationMethod != QuantitySurveyRateBuildUpCalculationMethod.Percentage)
            .OrderBy(value => value.Sequence)
            .ToList();
        var percentageInputs = request.Lines
            .Where(value => value.CalculationMethod == QuantitySurveyRateBuildUpCalculationMethod.Percentage)
            .OrderBy(value => value.Sequence)
            .ToList();
        if (directInputs.Count == 0)
            throw new QuantitySurveyRateLibraryValidationException("At least one direct quantity-rate or fixed component is required.");
        if (percentageInputs.Count > 0 && directInputs.Max(value => value.Sequence) > percentageInputs.Min(value => value.Sequence))
            throw new QuantitySurveyRateLibraryValidationException("Percentage additions must follow every direct-cost component in sequence.");
        if (percentageInputs.GroupBy(value => value.Component).Any(group => group.Count() > 1))
            throw new QuantitySurveyRateLibraryValidationException("Each percentage component can be applied only once in a build-up.");

        var previewLines = new List<QuantitySurveyRateBuildUpPreviewLineDto>(request.Lines.Count);
        decimal materialSubtotal = 0;
        decimal directCost = 0;
        foreach (var input in directInputs)
        {
            ValidateAllowedComponent(policy.Value, input.Component);
            var description = BuildUpDescription(input, null);
            if (input.CalculationMethod == QuantitySurveyRateBuildUpCalculationMethod.QuantityTimesPublishedRate)
            {
                if (!QuantityComponents.Contains(input.Component))
                    throw new QuantitySurveyRateLibraryValidationException($"{input.Component} cannot use a quantity-times-rate calculation.");
                if (!input.SourceRateId.HasValue || !sourceByRate.TryGetValue(input.SourceRateId.Value, out var source))
                    throw new QuantitySurveyRateLibraryValidationException(
                        $"Select a current published source rate for component {input.Sequence}.");
                ValidateSourceCategory(input.Component, source.RateLibraryItem.Category);
                var quantity = Positive(input.Quantity, $"Quantity for component {input.Sequence}");
                var amount = decimal.Round(quantity * source.UnitRate, policy.Value.DecimalPlaces, MidpointRounding.AwayFromZero);
                if (amount <= 0)
                    throw new QuantitySurveyRateLibraryValidationException($"Component {input.Sequence} rounds to zero under the effective QS policy.");
                description = BuildUpDescription(input, $"{source.RateLibraryItem.Code} · {source.RateLibraryItem.Name}");
                previewLines.Add(new QuantitySurveyRateBuildUpPreviewLineDto
                {
                    Sequence = input.Sequence,
                    Component = input.Component,
                    CalculationMethod = input.CalculationMethod,
                    Description = description,
                    SourceRateLibraryItemId = source.RateLibraryItemId,
                    SourceRateId = source.Id,
                    SourceItemCode = source.RateLibraryItem.Code,
                    SourceItemName = source.RateLibraryItem.Name,
                    SourceUnitOfMeasure = source.RateLibraryItem.UnitOfMeasure.Code,
                    SourceRateVersion = source.Version,
                    SourceUnitRate = source.UnitRate,
                    Quantity = quantity,
                    BasisAmount = source.UnitRate,
                    CalculatedAmount = amount
                });
                directCost += amount;
                if (input.Component == QuantitySurveyRateComponent.Material) materialSubtotal += amount;
            }
            else if (input.CalculationMethod == QuantitySurveyRateBuildUpCalculationMethod.FixedAmount)
            {
                if (input.Component is not (QuantitySurveyRateComponent.Transport or QuantitySurveyRateComponent.Other))
                    throw new QuantitySurveyRateLibraryValidationException("Fixed amounts are allowed only for Transport or Other components.");
                var amount = decimal.Round(Positive(input.FixedAmount, $"Fixed amount for component {input.Sequence}"),
                    policy.Value.DecimalPlaces, MidpointRounding.AwayFromZero);
                previewLines.Add(new QuantitySurveyRateBuildUpPreviewLineDto
                {
                    Sequence = input.Sequence,
                    Component = input.Component,
                    CalculationMethod = input.CalculationMethod,
                    Description = description,
                    FixedAmount = amount,
                    BasisAmount = amount,
                    CalculatedAmount = amount
                });
                directCost += amount;
            }
            else
            {
                throw new QuantitySurveyRateLibraryValidationException($"Select a supported calculation method for component {input.Sequence}.");
            }
        }

        materialSubtotal = decimal.Round(materialSubtotal, policy.Value.DecimalPlaces, MidpointRounding.AwayFromZero);
        directCost = decimal.Round(directCost, policy.Value.DecimalPlaces, MidpointRounding.AwayFromZero);
        var runningTotal = directCost;
        decimal addOnCost = 0;
        foreach (var input in percentageInputs)
        {
            ValidateAllowedComponent(policy.Value, input.Component);
            if (!PercentageComponents.Contains(input.Component))
                throw new QuantitySurveyRateLibraryValidationException($"{input.Component} cannot use a percentage calculation.");
            if (!input.PercentageBasis.HasValue)
                throw new QuantitySurveyRateLibraryValidationException($"Select a percentage basis for component {input.Sequence}.");
            if (input.Component == QuantitySurveyRateComponent.Wastage &&
                input.PercentageBasis != QuantitySurveyRateBuildUpPercentageBasis.MaterialSubtotal)
                throw new QuantitySurveyRateLibraryValidationException("Wastage must be calculated from the material subtotal.");

            var percentage = Positive(input.Percentage, $"Percentage for component {input.Sequence}");
            ValidatePercentageCeiling(policy.Value, input.Component, percentage);
            var basis = input.PercentageBasis.Value switch
            {
                QuantitySurveyRateBuildUpPercentageBasis.MaterialSubtotal => materialSubtotal,
                QuantitySurveyRateBuildUpPercentageBasis.DirectCost => directCost,
                QuantitySurveyRateBuildUpPercentageBasis.RunningTotal => runningTotal,
                _ => throw new QuantitySurveyRateLibraryValidationException("Select a supported percentage basis.")
            };
            if (basis <= 0)
                throw new QuantitySurveyRateLibraryValidationException($"The selected basis for component {input.Sequence} has no positive value.");
            var amount = decimal.Round(basis * percentage / 100m, policy.Value.DecimalPlaces, MidpointRounding.AwayFromZero);
            if (amount <= 0)
                throw new QuantitySurveyRateLibraryValidationException($"Component {input.Sequence} rounds to zero under the effective QS policy.");
            previewLines.Add(new QuantitySurveyRateBuildUpPreviewLineDto
            {
                Sequence = input.Sequence,
                Component = input.Component,
                CalculationMethod = input.CalculationMethod,
                PercentageBasis = input.PercentageBasis,
                Description = BuildUpDescription(input, input.Component.ToString()),
                Percentage = percentage,
                BasisAmount = basis,
                CalculatedAmount = amount
            });
            addOnCost += amount;
            runningTotal += amount;
        }

        addOnCost = decimal.Round(addOnCost, policy.Value.DecimalPlaces, MidpointRounding.AwayFromZero);
        var unitRate = decimal.Round(directCost + addOnCost, policy.Value.DecimalPlaces, MidpointRounding.AwayFromZero);
        if (unitRate <= 0) throw new QuantitySurveyRateLibraryValidationException("The calculated unit rate must be positive.");

        var rateRequest = new SaveQuantitySurveyRateRequest
        {
            UnitRate = unitRate,
            CurrencyId = request.CurrencyId,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            ProjectTypeId = request.ProjectTypeId,
            LocationId = request.LocationId,
            BusinessPartnerId = request.BusinessPartnerId,
            SourceType = QuantitySurveyRateSourceType.RateBuildUp,
            SourceReference = "Rate build-up preview",
            SourceDate = request.SourceDate,
            CentralDocumentVersionId = request.CentralDocumentVersionId,
            ChangeReason = "Preview"
        };
        var references = await ValidateRateAsync(item, rateRequest, cancellationToken);
        var orderedLines = previewLines.OrderBy(value => value.Sequence).ToList();
        var hash = BuildUpHash(item.Id, request, policy, references.Currency.CurrencyCode, orderedLines, unitRate);
        return new QuantitySurveyRateBuildUpPreviewDto
        {
            RateLibraryItemId = item.Id,
            SourceDate = sourceDate,
            CurrencyId = references.Currency.Id,
            CurrencyCode = references.Currency.CurrencyCode,
            ConfigurationProfileId = policy.Profile.Id,
            ConfigurationDecisionId = policy.Decision.Id,
            ConfigurationProfileVersion = policy.Profile.Version,
            DecimalPlaces = policy.Value.DecimalPlaces,
            MaterialSubtotal = materialSubtotal,
            DirectCost = directCost,
            AddOnCost = addOnCost,
            UnitRate = unitRate,
            IntegrityHash = hash,
            Lines = orderedLines
        };
    }

    private async Task<BuildUpPolicyContext> GetBuildUpPolicyAsync(DateTime atUtc, CancellationToken cancellationToken)
    {
        var at = Utc(atUtc);
        var profile = await db.QuantitySurveyConfigurationProfiles.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                value.ProfileCode == QuantitySurveyConfigurationService.ProfileCode &&
                value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published &&
                value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new QuantitySurveyRateLibraryValidationException(
                "No published QS configuration profile is effective for the build-up date.");
        var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profile.Id &&
                !value.IsDeleted && value.DecisionKey == "QS-DEC-004", cancellationToken)
            ?? throw new QuantitySurveyRateLibraryValidationException(
                "The effective QS configuration does not contain the rate build-up policy (QS-DEC-004).");
        var value = JsonSerializer.Deserialize<QsRateBuildUpValue>(decision.ValueJson, JsonOptions)
            ?? throw new QuantitySurveyRateLibraryValidationException("The effective QS rate build-up policy could not be read.");
        if (value.Components.Count == 0 || value.DecimalPlaces is < 0 or > 6)
            throw new QuantitySurveyRateLibraryValidationException("The effective QS rate build-up policy is incomplete.");
        return new BuildUpPolicyContext(profile, decision, value);
    }

    private async Task<IReadOnlyList<QuantitySurveyRateLibraryRate>> GetEligibleBuildUpSourcesAsync(
        Guid targetItemId,
        DateTime atUtc,
        Guid currencyId,
        Guid? projectTypeId,
        Guid? locationId,
        Guid? businessPartnerId,
        CancellationToken cancellationToken)
    {
        var at = Utc(atUtc);
        var values = await db.QuantitySurveyRateLibraryRates.AsNoTracking()
            .Include(value => value.RateLibraryItem).ThenInclude(value => value.UnitOfMeasure)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                value.RateLibraryItemId != targetItemId && value.RateLibraryItem.TenantId == TenantId &&
                !value.RateLibraryItem.IsDeleted && value.RateLibraryItem.IsActive &&
                value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published &&
                value.CurrencyId == currencyId && value.UnitRate > 0 &&
                value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at) &&
                (projectTypeId.HasValue
                    ? value.ProjectTypeId == null || value.ProjectTypeId == projectTypeId
                    : value.ProjectTypeId == null) &&
                (locationId.HasValue
                    ? value.LocationId == null || value.LocationId == locationId
                    : value.LocationId == null) &&
                (businessPartnerId.HasValue
                    ? value.BusinessPartnerId == null || value.BusinessPartnerId == businessPartnerId
                    : value.BusinessPartnerId == null))
            .ToListAsync(cancellationToken);

        return values
            .GroupBy(value => value.RateLibraryItemId)
            .Select(group => group
                .OrderByDescending(value => BuildUpSpecificity(value, projectTypeId, locationId, businessPartnerId))
                .ThenByDescending(value => value.EffectiveFrom)
                .ThenByDescending(value => value.Version)
                .First())
            .OrderBy(value => value.RateLibraryItem.Category)
            .ThenBy(value => value.RateLibraryItem.Code)
            .ToList();
    }

    private IQueryable<QuantitySurveyRateBuildUp> BuildUpQuery(bool ignoreQueryFilters = false)
    {
        IQueryable<QuantitySurveyRateBuildUp> query = ignoreQueryFilters
            ? db.QuantitySurveyRateBuildUps.IgnoreQueryFilters()
            : db.QuantitySurveyRateBuildUps;
        return query
            .Include(value => value.Lines)
            .Include(value => value.GeneratedRate).ThenInclude(value => value!.Currency)
            .Include(value => value.GeneratedRate).ThenInclude(value => value!.ProjectType)
            .Include(value => value.GeneratedRate).ThenInclude(value => value!.Location)
            .Include(value => value.GeneratedRate).ThenInclude(value => value!.BusinessPartner)
            .Include(value => value.GeneratedRate).ThenInclude(value => value!.CentralDocumentRecord)
            .Include(value => value.GeneratedRate).ThenInclude(value => value!.CentralDocumentVersion);
    }

    private static QuantitySurveyRateBuildUpSourceDto MapBuildUpSource(QuantitySurveyRateLibraryRate value) => new()
    {
        RateLibraryItemId = value.RateLibraryItemId,
        RateId = value.Id,
        ItemCode = value.RateLibraryItem.Code,
        ItemName = value.RateLibraryItem.Name,
        Category = value.RateLibraryItem.Category,
        UnitOfMeasure = value.RateLibraryItem.UnitOfMeasure.Code,
        RateVersion = value.Version,
        UnitRate = value.UnitRate,
        CurrencyId = value.CurrencyId,
        CurrencyCode = value.CurrencyCodeSnapshot,
        EffectiveFrom = value.EffectiveFrom,
        EffectiveTo = value.EffectiveTo,
        ProjectTypeId = value.ProjectTypeId,
        LocationId = value.LocationId,
        BusinessPartnerId = value.BusinessPartnerId
    };

    private static QuantitySurveyRateBuildUpDto MapBuildUp(QuantitySurveyRateBuildUp value) => new()
    {
        Id = value.Id,
        RateLibraryItemId = value.RateLibraryItemId,
        Version = value.Version,
        ClientRequestId = value.ClientRequestId,
        BuildUpNumber = value.BuildUpNumber,
        SourceDate = value.SourceDate,
        CurrencyId = value.CurrencyId,
        CurrencyCode = value.CurrencyCodeSnapshot,
        ConfigurationProfileVersion = value.ConfigurationProfileVersion,
        DecimalPlaces = value.DecimalPlaces,
        MaterialSubtotal = value.MaterialSubtotal,
        DirectCost = value.DirectCost,
        AddOnCost = value.AddOnCost,
        UnitRate = value.UnitRate,
        CalculationHash = value.CalculationHash,
        CentralDocumentRecordId = value.CentralDocumentRecordId,
        CentralDocumentVersionId = value.CentralDocumentVersionId,
        ChangeReason = value.ChangeReason,
        PreparedById = value.PreparedById,
        PreparedAt = value.PreparedAt,
        GeneratedRate = value.GeneratedRate is null
            ? throw new QuantitySurveyRateLibraryConflictException("The rate build-up is missing its generated rate draft.")
            : MapRate(value.GeneratedRate),
        Lines = value.Lines.OrderBy(line => line.Sequence).Select(MapBuildUpLine).ToList()
    };

    private static QuantitySurveyRateBuildUpPreviewLineDto MapBuildUpLine(QuantitySurveyRateBuildUpLine value) => new()
    {
        Sequence = value.Sequence,
        Component = value.Component,
        CalculationMethod = value.CalculationMethod,
        PercentageBasis = value.PercentageBasis,
        Description = value.Description,
        SourceRateLibraryItemId = value.SourceRateLibraryItemId,
        SourceRateId = value.SourceRateId,
        SourceItemCode = value.SourceItemCodeSnapshot,
        SourceItemName = value.SourceItemNameSnapshot,
        SourceUnitOfMeasure = value.SourceUnitOfMeasureSnapshot,
        SourceRateVersion = value.SourceRateVersion,
        SourceUnitRate = value.SourceUnitRate,
        Quantity = value.InputQuantity,
        Percentage = value.InputPercentage,
        FixedAmount = value.InputFixedAmount,
        BasisAmount = value.BasisAmount,
        CalculatedAmount = value.CalculatedAmount
    };

    private static void ValidateAllowedComponent(QsRateBuildUpValue policy, QuantitySurveyRateComponent component)
    {
        if (!Enum.IsDefined(component) || !policy.Components.Contains(component))
            throw new QuantitySurveyRateLibraryValidationException(
                $"{component} is not enabled by the effective rate build-up policy.");
    }

    private static void ValidateSourceCategory(
        QuantitySurveyRateComponent component,
        QuantitySurveyRateItemCategory category)
    {
        var expected = component switch
        {
            QuantitySurveyRateComponent.Material => QuantitySurveyRateItemCategory.Material,
            QuantitySurveyRateComponent.Labour => QuantitySurveyRateItemCategory.Labour,
            QuantitySurveyRateComponent.Plant => QuantitySurveyRateItemCategory.Plant,
            QuantitySurveyRateComponent.Equipment => QuantitySurveyRateItemCategory.Equipment,
            QuantitySurveyRateComponent.Subcontract => QuantitySurveyRateItemCategory.Subcontract,
            QuantitySurveyRateComponent.Transport or QuantitySurveyRateComponent.Other => QuantitySurveyRateItemCategory.StandardItem,
            _ => throw new QuantitySurveyRateLibraryValidationException($"{component} does not accept a source rate.")
        };
        if (category != expected)
            throw new QuantitySurveyRateLibraryValidationException(
                $"{component} requires a published {expected} rate-library item.");
    }

    private static void ValidatePercentageCeiling(
        QsRateBuildUpValue policy,
        QuantitySurveyRateComponent component,
        decimal percentage)
    {
        var ceiling = component switch
        {
            QuantitySurveyRateComponent.Overhead => policy.MaximumOverheadPercent,
            QuantitySurveyRateComponent.Profit => policy.MaximumProfitPercent,
            QuantitySurveyRateComponent.Contingency => policy.MaximumContingencyPercent,
            QuantitySurveyRateComponent.Wastage => policy.MaximumWastagePercent,
            _ => 100m
        };
        if (percentage > ceiling)
            throw new QuantitySurveyRateLibraryValidationException(
                $"{component} cannot exceed the effective policy ceiling of {ceiling.ToString("0.####", CultureInfo.InvariantCulture)}%.");
    }

    private static string BuildUpDescription(QuantitySurveyRateBuildUpLineRequest input, string? fallback)
    {
        var description = Clean(input.Description, 250) ?? fallback;
        if (input.Component == QuantitySurveyRateComponent.Other && string.IsNullOrWhiteSpace(description))
            throw new QuantitySurveyRateLibraryValidationException("Describe every Other rate component.");
        return Required(description ?? input.Component.ToString(), "Component description", 250);
    }

    private static decimal Positive(decimal? value, string label)
        => value.HasValue && value.Value > 0
            ? value.Value
            : throw new QuantitySurveyRateLibraryValidationException($"{label} must be greater than zero.");

    private static int BuildUpSpecificity(
        QuantitySurveyRateLibraryRate value,
        Guid? projectTypeId,
        Guid? locationId,
        Guid? businessPartnerId)
        => (projectTypeId.HasValue && value.ProjectTypeId == projectTypeId ? 4 : 0) +
           (locationId.HasValue && value.LocationId == locationId ? 2 : 0) +
           (businessPartnerId.HasValue && value.BusinessPartnerId == businessPartnerId ? 1 : 0);

    private static string BuildUpHash(
        Guid itemId,
        PreviewQuantitySurveyRateBuildUpRequest request,
        BuildUpPolicyContext policy,
        string currencyCode,
        IReadOnlyList<QuantitySurveyRateBuildUpPreviewLineDto> lines,
        decimal unitRate)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            ItemId = itemId,
            SourceDate = Utc(request.SourceDate),
            EffectiveFrom = Utc(request.EffectiveFrom),
            EffectiveTo = Utc(request.EffectiveTo),
            request.CurrencyId,
            CurrencyCode = currencyCode,
            request.ProjectTypeId,
            request.LocationId,
            request.BusinessPartnerId,
            request.CentralDocumentVersionId,
            ProfileId = policy.Profile.Id,
            ProfileVersion = policy.Profile.Version,
            DecisionId = policy.Decision.Id,
            Policy = policy.Value,
            UnitRate = unitRate,
            Lines = lines
        }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record BuildUpPolicyContext(
        QuantitySurveyConfigurationProfile Profile,
        QuantitySurveyConfigurationDecision Decision,
        QsRateBuildUpValue Value);

    private sealed record RateBuildUpPreparation(
        Guid BuildUpId,
        Guid? PreviousRateId,
        decimal? PreviousUnitRate,
        string? PreviousCurrencyCode,
        object AuditSnapshot);
}
