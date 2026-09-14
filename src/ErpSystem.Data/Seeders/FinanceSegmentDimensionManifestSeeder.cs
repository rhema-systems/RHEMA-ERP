using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>Missing-only, stable-code manifest for Finance identity segments and transaction dimensions.</summary>
public sealed class FinanceSegmentDimensionManifestSeeder
{
    public const string CompanyCode = "COMPANY";
    public const string NaturalAccountCode = "NATURAL_ACCOUNT";
    public static readonly string[] TransactionDimensionCodes =
        ["DEPARTMENT", "PROJECT", "ESTATE", "CONTRACT", "FUNDING_SOURCE", "ACTIVITY"];

    private readonly ApplicationDbContext _db;
    private readonly ILogger _logger;

    public FinanceSegmentDimensionManifestSeeder(ApplicationDbContext db, ILogger logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId, DateTime seedDate, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking().SingleAsync(item => item.Id == tenantId, cancellationToken);
        var tenantCode = NormalizeValue(tenant.Code, 10);
        var structures = await _db.AccountSegmentStructures
            .Where(item => item.TenantId == tenantId && !item.IsDeleted).ToListAsync(cancellationToken);

        // Upgrade only untouched system-owned legacy identity definitions. Administrator rows are evidence, not seed input.
        foreach (var legacy in structures.Where(item => IsUntouchedSystemManaged(item)
            && item.LifecycleStatus != AccountSegmentLifecycleStatus.Retired
            && item.SegmentCode is "DEPT" or "PROJ"))
        {
            legacy.IsActive = false;
            legacy.LifecycleStatus = AccountSegmentLifecycleStatus.Retired;
            legacy.RetirementReason = "Phase 5 separates transaction dimensions from GL account identity.";
            legacy.UpdatedAt = seedDate;
            legacy.UpdatedBy = "System (Finance Segment Manifest 1.0)";
        }

        var natural = structures.SingleOrDefault(item => item.SegmentCode == NaturalAccountCode)
            ?? structures.SingleOrDefault(item => item.IsNaturalAccount && IsUntouchedSystemManaged(item));
        if (natural == null)
        {
            natural = NewStructure(tenantId, NaturalAccountCode, "Natural Account", 2, 4, "Numeric", false, true, seedDate);
            _db.AccountSegmentStructures.Add(natural);
            structures.Add(natural);
        }
        else if (NeedsSystemUpgrade(natural, NaturalAccountCode, 2, 4, "Numeric", false, true))
        {
            SetSystemStructure(natural, NaturalAccountCode, "Natural Account", 2, 4, "Numeric", false, true, seedDate);
        }

        var company = structures.SingleOrDefault(item => item.SegmentCode == CompanyCode);
        if (company == null)
        {
            company = NewStructure(tenantId, CompanyCode, "Legal Entity / Company", 1, tenantCode.Length, "Alphanumeric", true, false, seedDate);
            _db.AccountSegmentStructures.Add(company);
            structures.Add(company);
        }
        else if (NeedsSystemUpgrade(company, CompanyCode, 1, tenantCode.Length, "Alphanumeric", true, false))
        {
            SetSystemStructure(company, CompanyCode, "Legal Entity / Company", 1, tenantCode.Length, "Alphanumeric", true, false, seedDate);
        }

        var companyValue = await _db.SegmentLookupValues.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.SegmentStructureId == company.Id && item.SegmentValue == tenantCode && !item.IsDeleted,
            cancellationToken);
        if (companyValue == null)
        {
            _db.SegmentLookupValues.Add(new SegmentLookupValue
            {
                Id = StableGuid($"finance:segment:{tenantId}:company:{tenantCode}"), TenantId = tenantId,
                SegmentStructureId = company.Id, SegmentValue = tenantCode, Description = tenant.Name,
                DisplayOrder = 1, EffectiveDate = seedDate, IsActive = true, CreatedAt = seedDate,
                CreatedBy = "System (Finance Segment Manifest 1.0)"
            });
        }

        var dimensions = new (string Code, string Name, int Order)[]
        {
            ("DEPARTMENT", "Department / Cost Centre", 10), ("PROJECT", "Project / Development", 20),
            ("ESTATE", "Estate / Property / Site", 30), ("CONTRACT", "Contract", 40),
            ("FUNDING_SOURCE", "Funding Source", 50), ("ACTIVITY", "Activity / Programme", 60)
        };
        foreach (var definition in dimensions)
        {
            if (await _db.FinanceDimensionDefinitions.AnyAsync(item => item.TenantId == tenantId && item.Code == definition.Code && !item.IsDeleted, cancellationToken))
                continue;
            _db.FinanceDimensionDefinitions.Add(new FinanceDimensionDefinition
            {
                Id = StableGuid($"finance:dimension:{tenantId}:{definition.Code}"), TenantId = tenantId,
                Code = definition.Code, Name = definition.Name, Description = "Finance transaction coding dimension.",
                Classification = "Analytical", ValueSourceType = "Lookup", IsActive = true,
                DisplayOrder = definition.Order, CreatedAt = seedDate, CreatedBy = "System (Finance Dimension Manifest 1.0)"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Ensured Phase 5 Finance segment/dimension manifest for tenant {TenantId}", tenantId);
    }

    private static AccountSegmentStructure NewStructure(Guid tenantId, string code, string name, int position, int length,
        string type, bool lookup, bool natural, DateTime at) => new()
    {
        Id = StableGuid($"finance:account-segment:{tenantId}:{code}"), TenantId = tenantId, SegmentCode = code,
        SegmentName = name, SegmentPosition = position, SegmentLength = length, DataType = type,
        SeparatorCharacter = position == 1 ? "-" : null, LookupTableRequired = lookup,
        IsReportingDimension = false, IsNaturalAccount = natural, IsActive = true,
        LifecycleStatus = AccountSegmentLifecycleStatus.Active, IsSystemDefined = true,
        Description = code == CompanyCode ? "Legal entity component of every GL account number." : "Natural GL account component.",
        CreatedAt = at, CreatedBy = "System (Finance Segment Manifest 1.0)"
    };

    private static void SetSystemStructure(AccountSegmentStructure item, string code, string name, int position, int length,
        string type, bool lookup, bool natural, DateTime at)
    {
        item.SegmentCode = code; item.SegmentName = name; item.SegmentPosition = position; item.SegmentLength = length;
        item.DataType = type; item.SeparatorCharacter = position == 1 ? "-" : null; item.LookupTableRequired = lookup;
        item.IsReportingDimension = false; item.IsNaturalAccount = natural; item.IsActive = true;
        item.LifecycleStatus = AccountSegmentLifecycleStatus.Active; item.IsSystemDefined = true;
        item.UpdatedAt = at; item.UpdatedBy = "System (Finance Segment Manifest 1.0)";
    }

    private static bool IsSystemManaged(AccountSegmentStructure item) => item.IsSystemDefined ||
        (!string.IsNullOrWhiteSpace(item.CreatedBy) && item.CreatedBy.StartsWith("System", StringComparison.OrdinalIgnoreCase));

    private static bool IsUntouchedSystemManaged(AccountSegmentStructure item) => IsSystemManaged(item)
        && (string.IsNullOrWhiteSpace(item.UpdatedBy) || item.UpdatedBy.StartsWith("System", StringComparison.OrdinalIgnoreCase));

    private static bool NeedsSystemUpgrade(AccountSegmentStructure item, string code, int position, int length,
        string type, bool lookup, bool natural) => IsUntouchedSystemManaged(item)
        && item.LifecycleStatus is not (AccountSegmentLifecycleStatus.Frozen or AccountSegmentLifecycleStatus.Retired)
        && (item.SegmentCode != code || item.SegmentPosition != position || item.SegmentLength != length
            || item.DataType != type || item.LookupTableRequired != lookup || item.IsNaturalAccount != natural
            || item.IsReportingDimension || !item.IsActive || item.LifecycleStatus != AccountSegmentLifecycleStatus.Active);

    private static string NormalizeValue(string? value, int maximum)
    {
        var normalized = new string((value ?? "COMPANY").Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (normalized.Length == 0) normalized = "COMPANY";
        return normalized[..Math.Min(normalized.Length, maximum)];
    }

    private static Guid StableGuid(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
