using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Adds a compact, missing-only transaction-dimension baseline for the DEFAULT tenant's
/// demonstrations and integration testing. It never changes an existing definition or value,
/// creates account rules, or assigns journal defaults.
/// </summary>
public sealed class FinanceDemoDimensionValueSeeder
{
    public const string SeedVersion = "FIN-DEMO-DIMENSIONS-1.0";

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ExpectedValueCodes =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["DEPARTMENT"] = ["DEPT-FIN", "DEPT-DEV", "DEPT-EST", "DEPT-HRA", "DEPT-IA", "DEPT-LEG", "DEPT-CPC"],
            ["PROJECT"] = ["DEMO-PROJ-HOUSING", "DEMO-PROJ-INFRA"],
            ["ESTATE"] = ["DEMO-SITE-HO", "DEMO-EST-HOUSING", "DEMO-SITE-INDUSTRIAL"],
            ["CONTRACT"] = ["DEMO-CTR-WORKS", "DEMO-CTR-SERVICES", "DEMO-CTR-SUPPLIES"],
            ["FUNDING_SOURCE"] = ["INTERNAL", "GOG", "COMMERCIAL_LOAN"],
            ["ACTIVITY"] = ["HOUSING_DEVELOPMENT", "ESTATE_MANAGEMENT", "INFRASTRUCTURE_WORKS", "CORPORATE_SUPPORT"]
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<(string Code, string Name)>> ValueManifest =
        new Dictionary<string, IReadOnlyList<(string Code, string Name)>>(StringComparer.Ordinal)
        {
            ["DEPARTMENT"] =
            [
                ("DEPT-FIN", "Finance Department"),
                ("DEPT-DEV", "Development Department"),
                ("DEPT-EST", "Estates Department"),
                ("DEPT-HRA", "HR / Administration Department"),
                ("DEPT-IA", "Internal Audit Department"),
                ("DEPT-LEG", "Legal Department"),
                ("DEPT-CPC", "Corporate Planning & Communications Department")
            ],
            ["PROJECT"] =
            [
                ("DEMO-PROJ-HOUSING", "Community Housing Development (Demo)"),
                ("DEMO-PROJ-INFRA", "Industrial Estate Infrastructure Upgrade (Demo)")
            ],
            ["ESTATE"] =
            [
                ("DEMO-SITE-HO", "TDC Head Office Site (Demo)"),
                ("DEMO-EST-HOUSING", "Community Housing Estate (Demo)"),
                ("DEMO-SITE-INDUSTRIAL", "Industrial Estate Site (Demo)")
            ],
            ["CONTRACT"] =
            [
                ("DEMO-CTR-WORKS", "Housing Works Contract (Demo)"),
                ("DEMO-CTR-SERVICES", "Engineering Services Contract (Demo)"),
                ("DEMO-CTR-SUPPLIES", "Office Supplies Contract (Demo)")
            ],
            ["FUNDING_SOURCE"] =
            [
                ("INTERNAL", "Internally Funded"),
                ("GOG", "Government of Ghana Funding"),
                ("COMMERCIAL_LOAN", "Commercial Loan Funding")
            ],
            ["ACTIVITY"] =
            [
                ("HOUSING_DEVELOPMENT", "Housing Development"),
                ("ESTATE_MANAGEMENT", "Estate Management"),
                ("INFRASTRUCTURE_WORKS", "Infrastructure Works"),
                ("CORPORATE_SUPPORT", "Corporate Support")
            ]
        };

    private readonly ApplicationDbContext _db;
    private readonly ILogger _logger;

    public FinanceDemoDimensionValueSeeder(ApplicationDbContext db, ILogger logger) => (_db, _logger) = (db, logger);

    public async Task SeedAsync(Guid tenantId, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        await new FinanceSegmentDimensionManifestSeeder(_db, _logger)
            .SeedAsync(tenantId, effectiveDate, cancellationToken);

        var definitions = await _db.FinanceDimensionDefinitions
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && FinanceSegmentDimensionManifestSeeder.TransactionDimensionCodes.Contains(item.Code))
            .ToDictionaryAsync(item => item.Code, StringComparer.Ordinal, cancellationToken);

        var departmentNames = await _db.OrganizationUnits.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive
                && ExpectedValueCodes["DEPARTMENT"].Contains(item.Code))
            .ToDictionaryAsync(item => item.Code, item => item.Name, StringComparer.Ordinal, cancellationToken);

        var inserted = 0;
        foreach (var (dimensionCode, values) in ValueManifest)
        {
            if (!definitions.TryGetValue(dimensionCode, out var definition)
                || !definition.IsActive
                || !string.Equals(definition.ValueSourceType, "Lookup", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Skipped demo values for Finance dimension {DimensionCode}; the administrator-owned definition is absent, inactive, or not lookup-backed.",
                    dimensionCode);
                continue;
            }

            var existingCodeList = await _db.FinanceDimensionValues.AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && item.FinanceDimensionDefinitionId == definition.Id && !item.IsDeleted)
                .Select(item => item.Code)
                .ToListAsync(cancellationToken);
            var existingCodes = existingCodeList.ToHashSet(StringComparer.Ordinal);

            var displayOrder = 0;
            foreach (var (code, manifestName) in values)
            {
                displayOrder += 10;
                if (existingCodes.Contains(code))
                    continue;

                var name = dimensionCode == "DEPARTMENT" && departmentNames.TryGetValue(code, out var organizationName)
                    ? organizationName
                    : manifestName;
                _db.FinanceDimensionValues.Add(new FinanceDimensionValue
                {
                    Id = StableGuid($"finance:demo-dimension:{tenantId}:{dimensionCode}:{code}"),
                    TenantId = tenantId,
                    FinanceDimensionDefinitionId = definition.Id,
                    Code = code,
                    Name = name,
                    EffectiveDate = effectiveDate.Date,
                    IsActive = true,
                    DisplayOrder = displayOrder,
                    CreatedAt = effectiveDate,
                    CreatedBy = $"System ({SeedVersion})"
                });
                inserted++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Ensured Finance demo dimension baseline {Version} for tenant {TenantId}; inserted {InsertedCount} missing values.",
            SeedVersion, tenantId, inserted);
    }

    private static Guid StableGuid(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
