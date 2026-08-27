using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Finance-owned control plane for transaction dimensions. The first certified producer is the
/// manual journal. Operational adapters remain optional until their own consumer contracts pass.
/// </summary>
public sealed class FinanceDimensionAdministrationService
{
    private static readonly HashSet<string> Classifications = new(StringComparer.Ordinal)
        { "Analytical", "Balancing", "Derived" };
    private static readonly HashSet<string> ValueSources = new(StringComparer.Ordinal)
        { "Lookup", "EntityBacked" };
    private static readonly HashSet<string> RuleTypes = new(StringComparer.Ordinal)
        { "Required", "Optional", "Prohibited", "Fixed" };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FinanceDimensionAdministrationService(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<IReadOnlyList<FinanceDimensionDefinitionDto>> GetDefinitionsAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var query = _context.FinanceDimensionDefinitions.AsNoTracking()
            .Include(item => item.Values)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted);
        if (!includeInactive)
            query = query.Where(item => item.IsActive);

        return (await query.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Code)
                .ToListAsync(cancellationToken))
            .Select(MapDefinition).ToList();
    }

    public async Task<FinanceDimensionDefinitionDto> CreateDefinitionAsync(
        UpsertFinanceDimensionDefinitionDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var code = Code(dto.Code, 30);
        await EnsureDefinitionShapeAsync(dto, cancellationToken);
        if (await _context.FinanceDimensionDefinitions.AnyAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted && item.Code == code, cancellationToken))
            throw new InvalidOperationException($"Finance dimension '{code}' already exists.");

        var entity = new FinanceDimensionDefinition
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = Text(dto.Name, 100),
            Description = Optional(dto.Description, 500),
            Classification = Canonical(dto.Classification, Classifications, "classification"),
            ValueSourceType = Canonical(dto.ValueSourceType, ValueSources, "value source type"),
            SourceEntityType = Optional(dto.SourceEntityType, 100),
            IsActive = dto.IsActive, DisplayOrder = dto.DisplayOrder, CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName, CreatedById = UserId()
        };
        _context.FinanceDimensionDefinitions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return MapDefinition(entity);
    }

    public async Task<FinanceDimensionDefinitionDto> UpdateDefinitionAsync(
        Guid id,
        UpsertFinanceDimensionDefinitionDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var entity = await _context.FinanceDimensionDefinitions.Include(item => item.Values)
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Finance dimension was not found.");
        var code = Code(dto.Code, 30);
        await EnsureDefinitionShapeAsync(dto, cancellationToken);
        var classification = Canonical(dto.Classification, Classifications, "classification");
        var valueSourceType = Canonical(dto.ValueSourceType, ValueSources, "value source type");
        if (await _context.FinanceDimensionDefinitions.AnyAsync(item =>
                item.TenantId == tenantId && item.Id != id && !item.IsDeleted && item.Code == code, cancellationToken))
            throw new InvalidOperationException($"Finance dimension '{code}' already exists.");
        if (entity.Values.Any() && (!string.Equals(entity.Code, code, StringComparison.Ordinal)
                                    || !string.Equals(entity.ValueSourceType, valueSourceType, StringComparison.Ordinal)))
            throw new InvalidOperationException("A dimension code or value-source type cannot change after values exist.");

        entity.Code = code;
        entity.Name = Text(dto.Name, 100);
        entity.Description = Optional(dto.Description, 500);
        entity.Classification = classification;
        entity.ValueSourceType = valueSourceType;
        entity.SourceEntityType = Optional(dto.SourceEntityType, 100);
        entity.IsActive = dto.IsActive;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName;
        entity.LastModifiedById = UserId();
        await _context.SaveChangesAsync(cancellationToken);
        return MapDefinition(entity);
    }

    public async Task<FinanceDimensionValueDto> CreateValueAsync(
        Guid definitionId,
        UpsertFinanceDimensionValueDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var definition = await DefinitionAsync(definitionId, tenantId, cancellationToken);
        var code = Code(dto.Code, 50);
        ValidateValue(definition, dto);
        await ValidateValueReferencesAsync(definition, dto, null, cancellationToken);
        if (await _context.FinanceDimensionValues.AnyAsync(item => item.TenantId == tenantId
                && item.FinanceDimensionDefinitionId == definitionId && !item.IsDeleted && item.Code == code,
                cancellationToken))
            throw new InvalidOperationException($"Dimension value '{definition.Code}={code}' already exists.");

        var value = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinanceDimensionDefinitionId = definitionId,
            Code = code, Name = Text(dto.Name, 200), ParentValueId = dto.ParentValueId,
            SourceEntityType = Optional(dto.SourceEntityType, 100), SourceEntityId = dto.SourceEntityId,
            EffectiveDate = RequireDate(dto.EffectiveDate, "Effective date"), ExpiryDate = dto.ExpiryDate,
            IsActive = dto.IsActive, DisplayOrder = dto.DisplayOrder, CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName, CreatedById = UserId()
        };
        _context.FinanceDimensionValues.Add(value);
        await _context.SaveChangesAsync(cancellationToken);
        return MapValue(value);
    }

    public async Task<FinanceDimensionValueDto> UpdateValueAsync(
        Guid definitionId,
        Guid valueId,
        UpsertFinanceDimensionValueDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var definition = await DefinitionAsync(definitionId, tenantId, cancellationToken);
        var value = await _context.FinanceDimensionValues.SingleOrDefaultAsync(item =>
                item.Id == valueId && item.TenantId == tenantId
                && item.FinanceDimensionDefinitionId == definitionId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Finance dimension value was not found.");
        var code = Code(dto.Code, 50);
        ValidateValue(definition, dto);
        await ValidateValueReferencesAsync(definition, dto, valueId, cancellationToken);
        if (await _context.FinanceDimensionValues.AnyAsync(item => item.TenantId == tenantId
                && item.FinanceDimensionDefinitionId == definitionId && item.Id != valueId
                && !item.IsDeleted && item.Code == code, cancellationToken))
            throw new InvalidOperationException($"Dimension value '{definition.Code}={code}' already exists.");
        if (await _context.FinanceDimensionSetItems.AnyAsync(item => item.TenantId == tenantId
                && item.FinanceDimensionValueId == valueId && !item.IsDeleted, cancellationToken)
            && (!string.Equals(value.Code, code, StringComparison.Ordinal)
                || value.SourceEntityId != dto.SourceEntityId
                || !string.Equals(value.SourceEntityType, Optional(dto.SourceEntityType, 100), StringComparison.Ordinal)))
            throw new InvalidOperationException("Posted dimension value identity cannot be changed; deactivate it and create a successor.");

        value.Code = code;
        value.Name = Text(dto.Name, 200);
        value.ParentValueId = dto.ParentValueId;
        value.SourceEntityType = Optional(dto.SourceEntityType, 100);
        value.SourceEntityId = dto.SourceEntityId;
        value.EffectiveDate = RequireDate(dto.EffectiveDate, "Effective date");
        value.ExpiryDate = dto.ExpiryDate;
        value.IsActive = dto.IsActive;
        value.DisplayOrder = dto.DisplayOrder;
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = _currentUser.UserName;
        value.LastModifiedById = UserId();
        await _context.SaveChangesAsync(cancellationToken);
        return MapValue(value);
    }

    public async Task<IReadOnlyList<FinanceDimensionAccountRuleDto>> GetRulesAsync(
        Guid? accountId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var query = _context.FinanceDimensionAccountRules.AsNoTracking()
            .Include(item => item.Account)
            .Include(item => item.FinanceDimensionDefinition)
            .Include(item => item.DefaultDimensionValue)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted);
        if (accountId.HasValue) query = query.Where(item => item.AccountId == accountId.Value);
        return (await query.OrderBy(item => item.Account.AccountNumber)
                .ThenBy(item => item.FinanceDimensionDefinition.DisplayOrder).ToListAsync(cancellationToken))
            .Select(MapRule).ToList();
    }

    public async Task<FinanceDimensionAccountRuleDto> UpsertRuleAsync(
        Guid? id,
        UpsertFinanceDimensionAccountRuleDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var ruleType = Canonical(dto.RuleType, RuleTypes, "rule type");
        var sourceModule = OptionalUpper(dto.SourceModule, 50);
        var sourceDocumentType = Optional(dto.SourceDocumentType, 100);
        var postingAction = Optional(dto.PostingAction, 50);
        if (ruleType != "Optional" && sourceDocumentType is null)
            throw new InvalidOperationException("Required, Fixed, and Prohibited rules must name a certified source document type.");
        if (sourceDocumentType is not null
            && string.Equals(sourceDocumentType, "ManualJournalEntry", StringComparison.OrdinalIgnoreCase))
        {
            sourceModule = "GL";
            sourceDocumentType = "ManualJournalEntry";
            postingAction = "Post";
        }
        if (ruleType != "Optional"
            && (sourceModule != "GL" || sourceDocumentType != "ManualJournalEntry" || postingAction != "Post"))
            throw new InvalidOperationException("This source is not yet certified for mandatory Finance dimension enforcement.");
        if (dto.EffectiveDate == default) throw new InvalidOperationException("Rule effective date is required.");
        if (dto.ExpiryDate.HasValue && dto.ExpiryDate.Value.Date < dto.EffectiveDate.Date)
            throw new InvalidOperationException("Rule expiry date cannot precede its effective date.");
        var account = await _context.Accounts.SingleOrDefaultAsync(item =>
                item.Id == dto.AccountId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Finance account was not found.");
        var definition = await DefinitionAsync(dto.FinanceDimensionDefinitionId, tenantId, cancellationToken);
        FinanceDimensionValue? defaultValue = null;
        if (dto.DefaultDimensionValueId.HasValue)
        {
            defaultValue = await _context.FinanceDimensionValues.SingleOrDefaultAsync(item =>
                    item.Id == dto.DefaultDimensionValueId.Value && item.TenantId == tenantId && !item.IsDeleted
                    && item.FinanceDimensionDefinitionId == definition.Id && item.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("The default value does not belong to the selected Finance dimension.");
        }
        if (ruleType == "Fixed" && defaultValue is null)
            throw new InvalidOperationException("A Fixed dimension rule requires a default value.");
        if (ruleType == "Prohibited" && defaultValue is not null)
            throw new InvalidOperationException("A Prohibited dimension rule cannot have a default value.");

        var duplicate = await _context.FinanceDimensionAccountRules.AnyAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted && item.Id != id
            && item.AccountId == dto.AccountId && item.FinanceDimensionDefinitionId == definition.Id
            && item.SourceModule == sourceModule && item.SourceDocumentType == sourceDocumentType
            && item.PostingAction == postingAction, cancellationToken);
        if (duplicate) throw new InvalidOperationException("The same account, dimension, and source scope already has a rule.");

        var entity = id.HasValue
            ? await _context.FinanceDimensionAccountRules.SingleOrDefaultAsync(item =>
                    item.Id == id.Value && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Finance dimension account rule was not found.")
            : new FinanceDimensionAccountRule
            {
                Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName, CreatedById = UserId()
            };
        entity.AccountId = account.Id;
        entity.FinanceDimensionDefinitionId = definition.Id;
        entity.RuleType = ruleType;
        entity.DefaultDimensionValueId = defaultValue?.Id;
        entity.SourceModule = sourceModule;
        entity.SourceDocumentType = sourceDocumentType;
        entity.PostingAction = postingAction;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName;
        entity.LastModifiedById = UserId();
        if (!id.HasValue) _context.FinanceDimensionAccountRules.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        entity.Account = account;
        entity.FinanceDimensionDefinition = definition;
        entity.DefaultDimensionValue = defaultValue;
        return MapRule(entity);
    }

    public async Task<FinanceDimensionSet?> ResolveManualJournalLineAsync(
        Guid accountId,
        DateTime postingDate,
        IReadOnlyList<FinancePostingDimensionValueDto>? supplied,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var inputs = NormalizeInputs(supplied);
        var userSuppliedCodes = inputs.Keys.ToHashSet(StringComparer.Ordinal);
        var rules = await ApplicableRulesAsync(tenantId, accountId, postingDate, cancellationToken);
        ApplyRules(inputs, rules);
        if (inputs.Count == 0) return null;

        var codes = inputs.Keys.ToArray();
        var definitions = await _context.FinanceDimensionDefinitions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive && codes.Contains(item.Code))
            .ToListAsync(cancellationToken);
        if (definitions.Count != inputs.Count)
            throw new InvalidOperationException("One or more Finance dimensions were not found or are inactive for this tenant.");
        var suppliedDerived = definitions.FirstOrDefault(item =>
            item.Classification == "Derived" && userSuppliedCodes.Contains(item.Code));
        if (suppliedDerived is not null)
            throw new InvalidOperationException($"Derived dimension {suppliedDerived.Code} must be resolved by Finance, not entered on a manual journal line.");

        var resolved = new List<(FinanceDimensionDefinition Definition, FinanceDimensionValue Value)>();
        foreach (var definition in definitions)
        {
            var input = inputs[definition.Code];
            var value = await ResolveValueAsync(tenantId, definition, input, postingDate, cancellationToken);
            resolved.Add((definition, value));
        }
        resolved = resolved.OrderBy(item => item.Definition.DisplayOrder).ThenBy(item => item.Definition.Code).ToList();
        var canonical = string.Join("|", resolved.Select(item => $"{item.Definition.Id:N}:{item.Value.Id:N}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var idBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"FIN-DIMSET|{tenantId:N}|{hash}"));
        var setId = new Guid(idBytes.AsSpan(0, 16));
        var existing = _context.FinanceDimensionSets.Local.FirstOrDefault(item => item.Id == setId)
            ?? await _context.FinanceDimensionSets.Include(item => item.Items).SingleOrDefaultAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted
                && (item.Id == setId || item.CombinationHash == hash), cancellationToken);
        if (existing is not null)
        {
            if (existing.Id != setId || existing.CombinationHash != hash)
                throw new InvalidOperationException("Finance dimension-set identity collision detected.");
            return existing;
        }

        var set = new FinanceDimensionSet
        {
            Id = setId, TenantId = tenantId, CombinationHash = hash,
            DisplayValue = string.Join(" · ", resolved.Select(item => $"{item.Definition.Code}={item.Value.Code}")),
            CreatedAt = DateTime.UtcNow, CreatedBy = _currentUser.UserName, CreatedById = UserId()
        };
        foreach (var item in resolved)
        {
            set.Items.Add(new FinanceDimensionSetItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FinanceDimensionSetId = set.Id,
                FinanceDimensionDefinitionId = item.Definition.Id, FinanceDimensionValueId = item.Value.Id,
                DimensionCodeSnapshot = item.Definition.Code, DimensionValueCodeSnapshot = item.Value.Code,
                DimensionValueNameSnapshot = item.Value.Name, CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName, CreatedById = UserId()
            });
        }
        _context.FinanceDimensionSets.Add(set);
        return set;
    }

    private async Task<List<FinanceDimensionAccountRule>> ApplicableRulesAsync(
        Guid tenantId, Guid accountId, DateTime date, CancellationToken cancellationToken)
    {
        var candidates = await _context.FinanceDimensionAccountRules.AsNoTracking()
            .Include(item => item.FinanceDimensionDefinition)
            .Include(item => item.DefaultDimensionValue)
            .Where(item => item.TenantId == tenantId && item.AccountId == accountId && !item.IsDeleted && item.IsActive
                           && item.EffectiveDate.Date <= date.Date
                           && (!item.ExpiryDate.HasValue || item.ExpiryDate.Value.Date >= date.Date)
                           && (item.SourceModule == null || item.SourceModule == "GL")
                           && (item.SourceDocumentType == null || item.SourceDocumentType == "ManualJournalEntry")
                           && (item.PostingAction == null || item.PostingAction == "Post"))
            .ToListAsync(cancellationToken);
        return candidates.GroupBy(item => item.FinanceDimensionDefinitionId).Select(group =>
        {
            var ordered = group.OrderByDescending(Specificity).ToList();
            if (ordered.Count > 1 && Specificity(ordered[0]) == Specificity(ordered[1]))
                throw new InvalidOperationException($"Ambiguous Finance dimension rules exist for {ordered[0].FinanceDimensionDefinition.Code}.");
            return ordered[0];
        }).ToList();
    }

    private static int Specificity(FinanceDimensionAccountRule rule)
        => (rule.SourceModule is null ? 0 : 1) + (rule.SourceDocumentType is null ? 0 : 1) + (rule.PostingAction is null ? 0 : 1);

    private static void ApplyRules(
        Dictionary<string, FinancePostingDimensionValueDto> inputs,
        IEnumerable<FinanceDimensionAccountRule> rules)
    {
        foreach (var rule in rules)
        {
            var code = rule.FinanceDimensionDefinition.Code;
            var hasInput = inputs.TryGetValue(code, out var supplied);
            switch (rule.RuleType)
            {
                case "Prohibited" when hasInput:
                    throw new InvalidOperationException($"Dimension {code} is prohibited for this account and manual-journal source.");
                case "Fixed":
                    if (rule.DefaultDimensionValue is null)
                        throw new InvalidOperationException($"Fixed dimension rule {code} has no configured value.");
                    if (hasInput && !Matches(supplied!, rule.DefaultDimensionValue))
                        throw new InvalidOperationException($"Dimension {code} is fixed at {rule.DefaultDimensionValue.Code}.");
                    inputs[code] = Input(rule.FinanceDimensionDefinition, rule.DefaultDimensionValue);
                    break;
                case "Required" when !hasInput:
                    if (rule.DefaultDimensionValue is null)
                        throw new InvalidOperationException($"Dimension {code} is required for this account and manual-journal source.");
                    inputs[code] = Input(rule.FinanceDimensionDefinition, rule.DefaultDimensionValue);
                    break;
                case "Optional" when !hasInput && rule.DefaultDimensionValue is not null:
                    inputs[code] = Input(rule.FinanceDimensionDefinition, rule.DefaultDimensionValue);
                    break;
            }
        }
    }

    private static Dictionary<string, FinancePostingDimensionValueDto> NormalizeInputs(
        IReadOnlyList<FinancePostingDimensionValueDto>? supplied)
    {
        var result = new Dictionary<string, FinancePostingDimensionValueDto>(StringComparer.Ordinal);
        foreach (var item in supplied ?? [])
        {
            var code = Code(item.DimensionCode, 30);
            if (!result.TryAdd(code, new FinancePostingDimensionValueDto
                {
                    DimensionCode = code, ValueCode = OptionalUpper(item.ValueCode, 50),
                    SourceEntityType = Optional(item.SourceEntityType, 100), SourceEntityId = item.SourceEntityId
                }))
                throw new InvalidOperationException($"Dimension {code} is repeated on the same journal line.");
        }
        return result;
    }

    private async Task<FinanceDimensionValue> ResolveValueAsync(
        Guid tenantId, FinanceDimensionDefinition definition, FinancePostingDimensionValueDto input,
        DateTime date, CancellationToken cancellationToken)
    {
        var lookup = !string.IsNullOrWhiteSpace(input.ValueCode);
        var entity = !string.IsNullOrWhiteSpace(input.SourceEntityType) || input.SourceEntityId.HasValue;
        if (lookup == entity || (entity && (string.IsNullOrWhiteSpace(input.SourceEntityType) || !input.SourceEntityId.HasValue)))
            throw new InvalidOperationException($"Dimension {definition.Code} must identify exactly one lookup value or source entity.");
        if (definition.ValueSourceType == "Lookup" && !lookup)
            throw new InvalidOperationException($"Dimension {definition.Code} requires a lookup value.");
        if (definition.ValueSourceType == "EntityBacked" && !entity)
            throw new InvalidOperationException($"Dimension {definition.Code} requires source-entity lineage.");
        return await _context.FinanceDimensionValues.SingleOrDefaultAsync(item =>
                item.TenantId == tenantId && item.FinanceDimensionDefinitionId == definition.Id
                && !item.IsDeleted && item.IsActive && item.EffectiveDate.Date <= date.Date
                && (!item.ExpiryDate.HasValue || item.ExpiryDate.Value.Date >= date.Date)
                && (lookup ? item.Code == input.ValueCode
                    : item.SourceEntityType == input.SourceEntityType && item.SourceEntityId == input.SourceEntityId), cancellationToken)
            ?? throw new InvalidOperationException($"Dimension value for {definition.Code} is not active and effective for the journal date.");
    }

    private async Task<FinanceDimensionDefinition> DefinitionAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
        => await _context.FinanceDimensionDefinitions.SingleOrDefaultAsync(item =>
                item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Finance dimension was not found.");

    private async Task EnsureDefinitionShapeAsync(UpsertFinanceDimensionDefinitionDto dto, CancellationToken _)
    {
        Canonical(dto.Classification, Classifications, "classification");
        var source = Canonical(dto.ValueSourceType, ValueSources, "value source type");
        if (source == "EntityBacked" && string.IsNullOrWhiteSpace(dto.SourceEntityType))
            throw new InvalidOperationException("An entity-backed dimension requires a source entity type.");
        if (source == "Lookup" && !string.IsNullOrWhiteSpace(dto.SourceEntityType))
            throw new InvalidOperationException("A lookup dimension cannot declare a source entity type.");
        await Task.CompletedTask;
    }

    private void ValidateValue(FinanceDimensionDefinition definition, UpsertFinanceDimensionValueDto dto)
    {
        if (dto.EffectiveDate == default) throw new InvalidOperationException("Dimension value effective date is required.");
        if (dto.ExpiryDate.HasValue && dto.ExpiryDate.Value.Date < dto.EffectiveDate.Date)
            throw new InvalidOperationException("Dimension value expiry date cannot precede its effective date.");
        var hasEntity = !string.IsNullOrWhiteSpace(dto.SourceEntityType) || dto.SourceEntityId.HasValue;
        if (definition.ValueSourceType == "Lookup" && hasEntity)
            throw new InvalidOperationException("A lookup dimension value cannot identify an operational entity.");
        if (definition.ValueSourceType == "EntityBacked"
            && (string.IsNullOrWhiteSpace(dto.SourceEntityType) || !dto.SourceEntityId.HasValue))
            throw new InvalidOperationException("An entity-backed dimension value requires exact source-entity lineage.");
        if (definition.ValueSourceType == "EntityBacked"
            && !string.Equals(definition.SourceEntityType, dto.SourceEntityType?.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("Dimension value source entity type does not match its definition.");
    }

    private async Task ValidateValueReferencesAsync(
        FinanceDimensionDefinition definition, UpsertFinanceDimensionValueDto dto, Guid? currentId,
        CancellationToken cancellationToken)
    {
        if (dto.ParentValueId.HasValue && currentId.HasValue && dto.ParentValueId.Value == currentId.Value)
            throw new InvalidOperationException("A dimension value cannot be its own parent.");
        if (dto.ParentValueId.HasValue && !await _context.FinanceDimensionValues.AnyAsync(item =>
                item.Id == dto.ParentValueId.Value && item.TenantId == definition.TenantId && !item.IsDeleted
                && item.FinanceDimensionDefinitionId == definition.Id, cancellationToken))
            throw new InvalidOperationException("Parent value must belong to the same Finance dimension.");
        if (dto.SourceEntityId.HasValue && await _context.FinanceDimensionValues.AnyAsync(item =>
                item.TenantId == definition.TenantId && item.FinanceDimensionDefinitionId == definition.Id
                && item.Id != currentId && !item.IsDeleted && item.SourceEntityType == dto.SourceEntityType!.Trim()
                && item.SourceEntityId == dto.SourceEntityId, cancellationToken))
            throw new InvalidOperationException("The operational entity is already mapped to this Finance dimension.");
    }

    private static bool Matches(FinancePostingDimensionValueDto input, FinanceDimensionValue value)
        => input.ValueCode is not null
            ? string.Equals(input.ValueCode, value.Code, StringComparison.Ordinal)
            : input.SourceEntityId == value.SourceEntityId
              && string.Equals(input.SourceEntityType, value.SourceEntityType, StringComparison.Ordinal);

    private static FinancePostingDimensionValueDto Input(FinanceDimensionDefinition definition, FinanceDimensionValue value)
        => definition.ValueSourceType == "Lookup"
            ? new FinancePostingDimensionValueDto { DimensionCode = definition.Code, ValueCode = value.Code }
            : new FinancePostingDimensionValueDto
                { DimensionCode = definition.Code, SourceEntityType = value.SourceEntityType, SourceEntityId = value.SourceEntityId };

    private static FinanceDimensionDefinitionDto MapDefinition(FinanceDimensionDefinition item) => new()
    {
        Id = item.Id, Code = item.Code, Name = item.Name, Description = item.Description,
        Classification = item.Classification, ValueSourceType = item.ValueSourceType,
        SourceEntityType = item.SourceEntityType, IsActive = item.IsActive, DisplayOrder = item.DisplayOrder,
        Values = item.Values.Where(value => !value.IsDeleted).OrderBy(value => value.DisplayOrder)
            .ThenBy(value => value.Code).Select(MapValue).ToList()
    };

    private static FinanceDimensionValueDto MapValue(FinanceDimensionValue item) => new()
    {
        Id = item.Id, FinanceDimensionDefinitionId = item.FinanceDimensionDefinitionId,
        Code = item.Code, Name = item.Name, ParentValueId = item.ParentValueId,
        SourceEntityType = item.SourceEntityType, SourceEntityId = item.SourceEntityId,
        EffectiveDate = item.EffectiveDate, ExpiryDate = item.ExpiryDate,
        IsActive = item.IsActive, DisplayOrder = item.DisplayOrder
    };

    private static FinanceDimensionAccountRuleDto MapRule(FinanceDimensionAccountRule item) => new()
    {
        Id = item.Id, AccountId = item.AccountId, AccountNumber = item.Account.AccountNumber ?? item.Account.AccountCode,
        AccountName = item.Account.AccountName, FinanceDimensionDefinitionId = item.FinanceDimensionDefinitionId,
        DimensionCode = item.FinanceDimensionDefinition.Code, DimensionName = item.FinanceDimensionDefinition.Name,
        RuleType = item.RuleType, DefaultDimensionValueId = item.DefaultDimensionValueId,
        DefaultValueCode = item.DefaultDimensionValue?.Code, SourceModule = item.SourceModule,
        SourceDocumentType = item.SourceDocumentType, PostingAction = item.PostingAction,
        EffectiveDate = item.EffectiveDate, ExpiryDate = item.ExpiryDate, IsActive = item.IsActive
    };

    private Guid? UserId() => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty ? id : null;
    private static DateTime RequireDate(DateTime value, string label)
        => value == default ? throw new InvalidOperationException($"{label} is required.") : value;
    private static string Code(string? value, int max) => Text(value, max).ToUpperInvariant();
    private static string? OptionalUpper(string? value, int max) => Optional(value, max)?.ToUpperInvariant();
    private static string Text(string? value, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("A required Finance dimension value is missing.");
        if (text.Length > max) throw new InvalidOperationException($"Finance dimension text cannot exceed {max} characters.");
        return text;
    }
    private static string? Optional(string? value, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.Length > max) throw new InvalidOperationException($"Finance dimension text cannot exceed {max} characters.");
        return text;
    }
    private static string Canonical(string? value, HashSet<string> allowed, string label)
    {
        var candidate = value?.Trim();
        var canonical = allowed.FirstOrDefault(item => string.Equals(item, candidate, StringComparison.OrdinalIgnoreCase));
        return canonical ?? throw new InvalidOperationException($"Unsupported Finance dimension {label} '{candidate}'.");
    }
}
