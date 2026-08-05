using ErpSystem.Core.Entities.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds common identification types with their issuing authorities
/// </summary>
public class IdentificationTypeSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<IdentificationTypeSeeder> _logger;

    public IdentificationTypeSeeder(ApplicationDbContext context, ILogger<IdentificationTypeSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding identification types for tenant {TenantId}", tenantId);

        // Check if identification types already exist for this tenant
        var existingCount = await _context.Set<IdentificationType>().CountAsync(i => i.TenantId == tenantId);
        if (existingCount > 0)
        {
            _logger.LogInformation("Identification types already seeded for tenant {TenantId}. Skipping.", tenantId);
            return;
        }

        // Get Ghana country ID for Ghana-specific IDs
        var ghanaCountry = await _context.Countries.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "GHA");
        var usaCountry = await _context.Countries.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "USA");
        var ukCountry = await _context.Countries.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "GBR");

        var identificationTypes = GetIdentificationTypes(tenantId, ghanaCountry?.Id, usaCountry?.Id, ukCountry?.Id);

        await _context.Set<IdentificationType>().AddRangeAsync(identificationTypes);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded {Count} identification types for tenant {TenantId}", identificationTypes.Count, tenantId);
    }

    private static List<IdentificationType> GetIdentificationTypes(Guid tenantId, Guid? ghanaCountryId, Guid? usaCountryId, Guid? ukCountryId)
    {
        var types = new List<IdentificationType>();

        // Ghana-specific IDs
        if (ghanaCountryId.HasValue)
        {
            types.Add(new IdentificationType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Ghana Card",
                Code = "GH_CARD",
                Description = "National Identification Card issued by the National Identification Authority of Ghana",
                IssuingAuthorityName = "National Identification Authority (NIA)",
                IssuingCountryId = ghanaCountryId.Value,
                HasExpiryDate = false,
                IsActive = true
            });

            types.Add(new IdentificationType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Ghana Voter ID",
                Code = "GH_VOTER",
                Description = "Voter Identification Card issued by the Electoral Commission of Ghana",
                IssuingAuthorityName = "Electoral Commission of Ghana",
                IssuingCountryId = ghanaCountryId.Value,
                HasExpiryDate = false,
                IsActive = true
            });

            types.Add(new IdentificationType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Ghana Driver's License",
                Code = "GH_DL",
                Description = "Driver's License issued by the Driver and Vehicle Licensing Authority of Ghana",
                IssuingAuthorityName = "Driver and Vehicle Licensing Authority (DVLA)",
                IssuingCountryId = ghanaCountryId.Value,
                HasExpiryDate = true,
                IsActive = true
            });

            types.Add(new IdentificationType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Ghana Passport",
                Code = "GH_PASSPORT",
                Description = "International Passport issued by the Government of Ghana",
                IssuingAuthorityName = "Ghana Immigration Service",
                IssuingCountryId = ghanaCountryId.Value,
                HasExpiryDate = true,
                IsActive = true
            });

            types.Add(new IdentificationType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "SSNIT Card",
                Code = "SSNIT",
                Description = "Social Security and National Insurance Trust identification card",
                IssuingAuthorityName = "Social Security and National Insurance Trust (SSNIT)",
                IssuingCountryId = ghanaCountryId.Value,
                HasExpiryDate = false,
                IsActive = true
            });
        }

        // International/Generic IDs
        types.Add(new IdentificationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "International Passport",
            Code = "PASSPORT",
            Description = "International travel document issued by national government",
            IssuingAuthorityName = "Various National Authorities",
            IssuingCountryId = null,
            HasExpiryDate = true,
            IsActive = true
        });

        types.Add(new IdentificationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "National ID Card",
            Code = "NATIONAL_ID",
            Description = "National identification card",
            IssuingAuthorityName = "Various National Authorities",
            IssuingCountryId = null,
            HasExpiryDate = true,
            IsActive = true
        });

        types.Add(new IdentificationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Driver's License",
            Code = "DRIVERS_LICENSE",
            Description = "Driver's license issued by licensing authority",
            IssuingAuthorityName = "Various Licensing Authorities",
            IssuingCountryId = null,
            HasExpiryDate = true,
            IsActive = true
        });

        // USA-specific IDs
        if (usaCountryId.HasValue)
        {
            types.Add(new IdentificationType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "US Social Security Card",
                Code = "US_SSN",
                Description = "Social Security Card issued by the Social Security Administration",
                IssuingAuthorityName = "Social Security Administration (SSA)",
                IssuingCountryId = usaCountryId.Value,
                HasExpiryDate = false,
                IsActive = true
            });

            types.Add(new IdentificationType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "US State ID",
                Code = "US_STATE_ID",
                Description = "State-issued identification card",
                IssuingAuthorityName = "State Department of Motor Vehicles",
                IssuingCountryId = usaCountryId.Value,
                HasExpiryDate = true,
                IsActive = true
            });
        }

        // UK-specific IDs
        if (ukCountryId.HasValue)
        {
            types.Add(new IdentificationType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "UK National Insurance Number",
                Code = "UK_NINO",
                Description = "National Insurance Number issued by HM Revenue and Customs",
                IssuingAuthorityName = "HM Revenue and Customs (HMRC)",
                IssuingCountryId = ukCountryId.Value,
                HasExpiryDate = false,
                IsActive = true
            });

            types.Add(new IdentificationType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "UK Driving Licence",
                Code = "UK_DL",
                Description = "UK Driving Licence issued by DVLA",
                IssuingAuthorityName = "Driver and Vehicle Licensing Agency (DVLA)",
                IssuingCountryId = ukCountryId.Value,
                HasExpiryDate = true,
                IsActive = true
            });
        }

        // Work/Residence Permits
        types.Add(new IdentificationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Work Permit",
            Code = "WORK_PERMIT",
            Description = "Work authorization document",
            IssuingAuthorityName = "Various Immigration Authorities",
            IssuingCountryId = null,
            HasExpiryDate = true,
            IsActive = true
        });

        types.Add(new IdentificationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Residence Permit",
            Code = "RESIDENCE_PERMIT",
            Description = "Residence authorization document",
            IssuingAuthorityName = "Various Immigration Authorities",
            IssuingCountryId = null,
            HasExpiryDate = true,
            IsActive = true
        });

        types.Add(new IdentificationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Refugee ID",
            Code = "REFUGEE_ID",
            Description = "Refugee identification document",
            IssuingAuthorityName = "UNHCR / National Authorities",
            IssuingCountryId = null,
            HasExpiryDate = true,
            IsActive = true
        });

        // Professional IDs
        types.Add(new IdentificationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Professional License",
            Code = "PROF_LICENSE",
            Description = "Professional practice license",
            IssuingAuthorityName = "Professional Regulatory Bodies",
            IssuingCountryId = null,
            HasExpiryDate = true,
            IsActive = true
        });

        types.Add(new IdentificationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Tax Identification Number",
            Code = "TIN",
            Description = "Tax identification number",
            IssuingAuthorityName = "Tax Authorities",
            IssuingCountryId = null,
            HasExpiryDate = false,
            IsActive = true
        });

        // Military/Government IDs
        types.Add(new IdentificationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Military ID",
            Code = "MILITARY_ID",
            Description = "Military identification card",
            IssuingAuthorityName = "Armed Forces",
            IssuingCountryId = null,
            HasExpiryDate = true,
            IsActive = true
        });

        types.Add(new IdentificationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Government Employee ID",
            Code = "GOV_ID",
            Description = "Government employee identification",
            IssuingAuthorityName = "Government Departments",
            IssuingCountryId = null,
            HasExpiryDate = true,
            IsActive = true
        });

        return types;
    }
}
