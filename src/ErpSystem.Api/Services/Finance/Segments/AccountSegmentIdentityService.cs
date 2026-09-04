using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Segments;

/// <summary>
/// Authoritative validator and composer for GL account-number identity. Transaction coding
/// dimensions deliberately do not participate in this service.
/// </summary>
public sealed class AccountSegmentIdentityService : IAccountSegmentIdentityService
{
    private readonly ApplicationDbContext _db;

    public AccountSegmentIdentityService(ApplicationDbContext db) => _db = db;

    public async Task<AccountSegmentIdentityResultDto> ValidateAndComposeAsync(
        Guid tenantId,
        IReadOnlyCollection<AccountSegmentValueCreateDto> values,
        string? clientAccountNumber = null,
        Guid? existingAccountId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required for GL account segment validation.");
        if (values == null)
            throw new InvalidOperationException("Segment values are required for GL account identity.");

        var definitions = await ActiveDefinitions(tenantId)
            .Include(item => item.LookupValues.Where(value => !value.IsDeleted))
            .ToListAsync(cancellationToken);
        if (definitions.Count == 0)
            throw new InvalidOperationException("No active GL account-number structure is configured for this tenant.");
        if (definitions.Count(item => item.IsNaturalAccount) != 1)
            throw new InvalidOperationException("The active GL account-number structure must contain exactly one Natural Account segment.");
        if (definitions.Select(item => item.SegmentPosition).Distinct().Count() != definitions.Count)
            throw new InvalidOperationException("The active GL account-number structure contains duplicate positions.");

        var submitted = values.ToList();
        if (submitted.Count != submitted.Select(item => item.SegmentStructureId).Distinct().Count())
            throw new InvalidOperationException("Each active GL account segment must be supplied exactly once; a segment was duplicated.");
        if (submitted.Count != submitted.Select(item => item.SegmentPosition).Distinct().Count())
            throw new InvalidOperationException("Each active GL account segment position must be supplied exactly once; a position was duplicated.");

        var activeById = definitions.ToDictionary(item => item.Id);
        var submittedIds = submitted.Select(item => item.SegmentStructureId).ToHashSet();
        var missing = definitions.Where(item => !submittedIds.Contains(item.Id)).Select(item => item.SegmentCode).OrderBy(item => item).ToList();
        var unknown = submitted.Where(item => !activeById.ContainsKey(item.SegmentStructureId)).Select(item => item.SegmentStructureId).Distinct().ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Missing required GL account segments: {string.Join(", ", missing)}.");
        if (unknown.Count > 0)
            throw new InvalidOperationException("One or more submitted GL account segments are unknown, inactive, retired, or belong to another tenant.");
        if (submitted.Count != definitions.Count)
            throw new InvalidOperationException("GL account identity must contain exactly the active segment set; extra segments are not allowed.");

        var normalized = new List<AccountSegmentValueCreateDto>(definitions.Count);
        foreach (var definition in definitions.OrderBy(item => item.SegmentPosition))
        {
            var input = submitted.Single(item => item.SegmentStructureId == definition.Id);
            if (input.SegmentPosition != definition.SegmentPosition)
                throw new InvalidOperationException($"Segment {definition.SegmentCode} must be supplied at position {definition.SegmentPosition}.");
            var value = (input.SegmentValue ?? string.Empty).Trim().ToUpperInvariant();
            if (value.Length != definition.SegmentLength)
                throw new InvalidOperationException($"Segment {definition.SegmentCode} must contain exactly {definition.SegmentLength} characters.");
            ValidateDataType(definition, value);

            SegmentLookupValue? lookup = null;
            if (input.SegmentLookupValueId.HasValue)
            {
                lookup = definition.LookupValues.SingleOrDefault(item =>
                    item.Id == input.SegmentLookupValueId.Value
                    && item.TenantId == tenantId
                    && item.IsActive
                    && string.Equals(item.SegmentValue, value, StringComparison.OrdinalIgnoreCase));
                if (lookup == null)
                    throw new InvalidOperationException($"The lookup value for segment {definition.SegmentCode} is inactive, mismatched, or belongs to another tenant.");
            }
            else if (definition.LookupTableRequired)
            {
                lookup = definition.LookupValues.SingleOrDefault(item =>
                    item.TenantId == tenantId && item.IsActive
                    && string.Equals(item.SegmentValue, value, StringComparison.OrdinalIgnoreCase));
                if (lookup == null)
                    throw new InvalidOperationException($"Segment {definition.SegmentCode} requires an active configured lookup value.");
            }

            normalized.Add(new AccountSegmentValueCreateDto
            {
                SegmentStructureId = definition.Id,
                SegmentPosition = definition.SegmentPosition,
                SegmentValue = value,
                SegmentLookupValueId = lookup?.Id
            });
        }

        var settings = await _db.FinanceSettings.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        var fallbackSeparator = settings?.AccountSeparator ?? "-";
        var orderedDefinitions = definitions.OrderBy(item => item.SegmentPosition).ToList();
        var parts = new List<string>();
        for (var index = 0; index < orderedDefinitions.Count; index++)
        {
            var definition = orderedDefinitions[index];
            parts.Add(normalized[index].SegmentValue);
            if (index < orderedDefinitions.Count - 1)
                parts.Add(definition.SeparatorCharacter ?? fallbackSeparator);
        }
        var accountNumber = string.Concat(parts);
        if (!string.IsNullOrWhiteSpace(clientAccountNumber)
            && !string.Equals(clientAccountNumber.Trim().ToUpperInvariant(), accountNumber, StringComparison.Ordinal))
            throw new InvalidOperationException($"The submitted account number does not match the server-composed identity '{accountNumber}'.");

        if (await _db.Accounts.AsNoTracking().AnyAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted
                && (!existingAccountId.HasValue || item.Id != existingAccountId.Value) && item.AccountNumber == accountNumber,
                cancellationToken))
            throw new InvalidOperationException($"GL account number '{accountNumber}' already exists for this tenant.");

        var naturalDefinition = definitions.Single(item => item.IsNaturalAccount);
        return new AccountSegmentIdentityResultDto
        {
            AccountNumber = accountNumber,
            NaturalAccountCode = normalized.Single(item => item.SegmentStructureId == naturalDefinition.Id).SegmentValue,
            Values = normalized
        };
    }

    public async Task<AccountSegmentIdentityResultDto> ResolveProvisioningIdentityAsync(
        Guid tenantId,
        string naturalAccountCode,
        CancellationToken cancellationToken = default)
    {
        var definitions = await ActiveDefinitions(tenantId)
            .Include(item => item.LookupValues.Where(value => !value.IsDeleted && value.IsActive))
            .ToListAsync(cancellationToken);
        var company = definitions.SingleOrDefault(item => item.SegmentCode == "COMPANY");
        var natural = definitions.SingleOrDefault(item => item.SegmentCode == "NATURAL_ACCOUNT" && item.IsNaturalAccount);
        if (definitions.Count != 2 || company == null || natural == null)
            throw new InvalidOperationException(
                "Finance account provisioning can derive only the approved COMPANY/NATURAL_ACCOUNT identity. Additional active segments require a coordinated contract update.");
        var companyValue = company.LookupValues.SingleOrDefault();
        if (companyValue == null)
            throw new InvalidOperationException("The COMPANY segment has no active tenant-owned value.");

        return await ValidateAndComposeAsync(tenantId,
        [
            new AccountSegmentValueCreateDto
            {
                SegmentStructureId = company.Id, SegmentPosition = company.SegmentPosition,
                SegmentValue = companyValue.SegmentValue, SegmentLookupValueId = companyValue.Id
            },
            new AccountSegmentValueCreateDto
            {
                SegmentStructureId = natural.Id, SegmentPosition = natural.SegmentPosition,
                SegmentValue = naturalAccountCode
            }
        ], cancellationToken: cancellationToken);
    }

    public async Task<AccountSegmentReadinessDto> GetReadinessAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == accountId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("GL account was not found.");
        var assignments = await _db.AccountSegmentValues.AsNoTracking()
            .Include(value => value.SegmentStructure)
            .Include(value => value.SegmentLookupValue)
            .Where(value => value.AccountId == account.Id && !value.IsDeleted)
            .ToListAsync(cancellationToken);
        var definitions = await ActiveDefinitions(tenantId).AsNoTracking().ToListAsync(cancellationToken);
        var activeIds = definitions.Select(item => item.Id).ToHashSet();
        var presentIds = assignments.Select(item => item.SegmentStructureId).ToHashSet();
        var readiness = new AccountSegmentReadinessDto
        {
            AccountId = account.Id,
            AccountNumber = account.AccountNumber,
            MissingSegmentCodes = definitions.Where(item => !presentIds.Contains(item.Id)).Select(item => item.SegmentCode).OrderBy(item => item).ToList(),
            ExtraSegmentCodes = assignments.Where(item => !activeIds.Contains(item.SegmentStructureId))
                .Select(item => item.SegmentStructure?.SegmentCode ?? item.SegmentStructureId.ToString()).Distinct().OrderBy(item => item).ToList()
        };

        var lineageIssues = new List<string>();
        foreach (var value in assignments)
        {
            if (value.TenantId != tenantId || value.AccountId != account.Id)
                lineageIssues.Add($"Segment assignment '{value.Id}' has foreign tenant or account lineage.");
            if (value.SegmentStructure == null
                || value.SegmentStructure.TenantId != tenantId
                || value.SegmentStructure.Id != value.SegmentStructureId)
                lineageIssues.Add($"Segment assignment '{value.Id}' has foreign or missing segment-definition lineage.");
            if (value.SegmentLookupValueId.HasValue
                && (value.SegmentLookupValue == null
                    || value.SegmentLookupValue.TenantId != tenantId
                    || value.SegmentLookupValue.SegmentStructureId != value.SegmentStructureId))
                lineageIssues.Add($"Segment assignment '{value.Id}' has foreign, missing, or mismatched lookup lineage.");
        }

        if (lineageIssues.Count > 0)
        {
            readiness.Issues = lineageIssues.Distinct().ToList();
            return readiness;
        }

        try
        {
            await ValidateAndComposeAsync(tenantId, assignments.Select(item => new AccountSegmentValueCreateDto
            {
                SegmentStructureId = item.SegmentStructureId,
                SegmentPosition = item.SegmentPosition,
                SegmentValue = item.SegmentValue,
                SegmentLookupValueId = item.SegmentLookupValueId
            }).ToList(), account.AccountNumber, account.Id, cancellationToken);
            readiness.IsReady = true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            readiness.Issues = [exception.Message];
        }
        return readiness;
    }

    private IQueryable<AccountSegmentStructure> ActiveDefinitions(Guid tenantId) =>
        _db.AccountSegmentStructures.Where(item => item.TenantId == tenantId && !item.IsDeleted
            && item.IsActive && item.LifecycleStatus != AccountSegmentLifecycleStatus.Retired);

    private static void ValidateDataType(AccountSegmentStructure definition, string value)
    {
        var valid = definition.DataType.Trim().ToUpperInvariant() switch
        {
            "NUMERIC" => value.All(char.IsDigit),
            "ALPHA" or "ALPHABETIC" => value.All(char.IsLetter),
            "ALPHANUMERIC" => value.All(char.IsLetterOrDigit),
            _ => false
        };
        if (!valid)
            throw new InvalidOperationException($"Segment {definition.SegmentCode} does not satisfy its {definition.DataType} format.");
    }
}
