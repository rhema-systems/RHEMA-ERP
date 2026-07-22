using ErpSystem.Core.Entities.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds all countries in the world with ISO codes
/// </summary>
public class CountrySeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CountrySeeder> _logger;

    public CountrySeeder(ApplicationDbContext context, ILogger<CountrySeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding countries for tenant {TenantId}", tenantId);

        // Add only countries whose Code isn't already present for this tenant. Per-code (not a
        // coarse "any exist -> skip") so ordering is irrelevant: e.g. seed-hr-full inserts a single
        // placeholder country before this runs, which must not suppress the full ISO list.
        var countries = GetAllCountries(tenantId);
        var existingCodes = await _context.Countries
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.Code)
            .ToListAsync();
        var toAdd = countries.Where(c => !existingCodes.Contains(c.Code)).ToList();
        if (toAdd.Count == 0)
        {
            _logger.LogInformation("Countries already seeded for tenant {TenantId}. Skipping.", tenantId);
            return;
        }

        await _context.Countries.AddRangeAsync(toAdd);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded {Count} countries for tenant {TenantId}", toAdd.Count, tenantId);
    }

    private static List<Country> GetAllCountries(Guid tenantId)
    {
        return new List<Country>
        {
            // A
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Afghanistan", Code = "AFG", Alpha2Code = "AF", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Albania", Code = "ALB", Alpha2Code = "AL", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Algeria", Code = "DZA", Alpha2Code = "DZ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Andorra", Code = "AND", Alpha2Code = "AD", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Angola", Code = "AGO", Alpha2Code = "AO", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Antigua and Barbuda", Code = "ATG", Alpha2Code = "AG", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Argentina", Code = "ARG", Alpha2Code = "AR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Armenia", Code = "ARM", Alpha2Code = "AM", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Australia", Code = "AUS", Alpha2Code = "AU", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Austria", Code = "AUT", Alpha2Code = "AT", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Azerbaijan", Code = "AZE", Alpha2Code = "AZ", IsActive = true },

            // B
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bahamas", Code = "BHS", Alpha2Code = "BS", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bahrain", Code = "BHR", Alpha2Code = "BH", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bangladesh", Code = "BGD", Alpha2Code = "BD", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Barbados", Code = "BRB", Alpha2Code = "BB", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Belarus", Code = "BLR", Alpha2Code = "BY", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Belgium", Code = "BEL", Alpha2Code = "BE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Belize", Code = "BLZ", Alpha2Code = "BZ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Benin", Code = "BEN", Alpha2Code = "BJ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bhutan", Code = "BTN", Alpha2Code = "BT", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bolivia", Code = "BOL", Alpha2Code = "BO", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bosnia and Herzegovina", Code = "BIH", Alpha2Code = "BA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Botswana", Code = "BWA", Alpha2Code = "BW", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Brazil", Code = "BRA", Alpha2Code = "BR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Brunei", Code = "BRN", Alpha2Code = "BN", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bulgaria", Code = "BGR", Alpha2Code = "BG", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Burkina Faso", Code = "BFA", Alpha2Code = "BF", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Burundi", Code = "BDI", Alpha2Code = "BI", IsActive = true },

            // C
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Cabo Verde", Code = "CPV", Alpha2Code = "CV", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Cambodia", Code = "KHM", Alpha2Code = "KH", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Cameroon", Code = "CMR", Alpha2Code = "CM", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Canada", Code = "CAN", Alpha2Code = "CA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Central African Republic", Code = "CAF", Alpha2Code = "CF", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Chad", Code = "TCD", Alpha2Code = "TD", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Chile", Code = "CHL", Alpha2Code = "CL", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "China", Code = "CHN", Alpha2Code = "CN", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Colombia", Code = "COL", Alpha2Code = "CO", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Comoros", Code = "COM", Alpha2Code = "KM", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Congo", Code = "COG", Alpha2Code = "CG", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Costa Rica", Code = "CRI", Alpha2Code = "CR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Côte d'Ivoire", Code = "CIV", Alpha2Code = "CI", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Croatia", Code = "HRV", Alpha2Code = "HR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Cuba", Code = "CUB", Alpha2Code = "CU", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Cyprus", Code = "CYP", Alpha2Code = "CY", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Czech Republic", Code = "CZE", Alpha2Code = "CZ", IsActive = true },

            // D
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Democratic Republic of the Congo", Code = "COD", Alpha2Code = "CD", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Denmark", Code = "DNK", Alpha2Code = "DK", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Djibouti", Code = "DJI", Alpha2Code = "DJ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Dominica", Code = "DMA", Alpha2Code = "DM", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Dominican Republic", Code = "DOM", Alpha2Code = "DO", IsActive = true },

            // E
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Ecuador", Code = "ECU", Alpha2Code = "EC", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Egypt", Code = "EGY", Alpha2Code = "EG", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "El Salvador", Code = "SLV", Alpha2Code = "SV", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Equatorial Guinea", Code = "GNQ", Alpha2Code = "GQ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Eritrea", Code = "ERI", Alpha2Code = "ER", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Estonia", Code = "EST", Alpha2Code = "EE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Eswatini", Code = "SWZ", Alpha2Code = "SZ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Ethiopia", Code = "ETH", Alpha2Code = "ET", IsActive = true },

            // F
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Fiji", Code = "FJI", Alpha2Code = "FJ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Finland", Code = "FIN", Alpha2Code = "FI", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "France", Code = "FRA", Alpha2Code = "FR", IsActive = true },

            // G
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Gabon", Code = "GAB", Alpha2Code = "GA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Gambia", Code = "GMB", Alpha2Code = "GM", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Georgia", Code = "GEO", Alpha2Code = "GE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Germany", Code = "DEU", Alpha2Code = "DE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Ghana", Code = "GHA", Alpha2Code = "GH", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Greece", Code = "GRC", Alpha2Code = "GR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Grenada", Code = "GRD", Alpha2Code = "GD", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Guatemala", Code = "GTM", Alpha2Code = "GT", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Guinea", Code = "GIN", Alpha2Code = "GN", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Guinea-Bissau", Code = "GNB", Alpha2Code = "GW", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Guyana", Code = "GUY", Alpha2Code = "GY", IsActive = true },

            // H
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Haiti", Code = "HTI", Alpha2Code = "HT", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Honduras", Code = "HND", Alpha2Code = "HN", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Hungary", Code = "HUN", Alpha2Code = "HU", IsActive = true },

            // I
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Iceland", Code = "ISL", Alpha2Code = "IS", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "India", Code = "IND", Alpha2Code = "IN", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Indonesia", Code = "IDN", Alpha2Code = "ID", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Iran", Code = "IRN", Alpha2Code = "IR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Iraq", Code = "IRQ", Alpha2Code = "IQ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Ireland", Code = "IRL", Alpha2Code = "IE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Israel", Code = "ISR", Alpha2Code = "IL", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Italy", Code = "ITA", Alpha2Code = "IT", IsActive = true },

            // J
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Jamaica", Code = "JAM", Alpha2Code = "JM", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Japan", Code = "JPN", Alpha2Code = "JP", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Jordan", Code = "JOR", Alpha2Code = "JO", IsActive = true },

            // K
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Kazakhstan", Code = "KAZ", Alpha2Code = "KZ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Kenya", Code = "KEN", Alpha2Code = "KE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Kiribati", Code = "KIR", Alpha2Code = "KI", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Kuwait", Code = "KWT", Alpha2Code = "KW", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Kyrgyzstan", Code = "KGZ", Alpha2Code = "KG", IsActive = true },

            // L
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Laos", Code = "LAO", Alpha2Code = "LA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Latvia", Code = "LVA", Alpha2Code = "LV", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Lebanon", Code = "LBN", Alpha2Code = "LB", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Lesotho", Code = "LSO", Alpha2Code = "LS", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Liberia", Code = "LBR", Alpha2Code = "LR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Libya", Code = "LBY", Alpha2Code = "LY", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Liechtenstein", Code = "LIE", Alpha2Code = "LI", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Lithuania", Code = "LTU", Alpha2Code = "LT", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Luxembourg", Code = "LUX", Alpha2Code = "LU", IsActive = true },

            // M
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Madagascar", Code = "MDG", Alpha2Code = "MG", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Malawi", Code = "MWI", Alpha2Code = "MW", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Malaysia", Code = "MYS", Alpha2Code = "MY", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Maldives", Code = "MDV", Alpha2Code = "MV", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Mali", Code = "MLI", Alpha2Code = "ML", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Malta", Code = "MLT", Alpha2Code = "MT", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Marshall Islands", Code = "MHL", Alpha2Code = "MH", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Mauritania", Code = "MRT", Alpha2Code = "MR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Mauritius", Code = "MUS", Alpha2Code = "MU", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Mexico", Code = "MEX", Alpha2Code = "MX", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Micronesia", Code = "FSM", Alpha2Code = "FM", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Moldova", Code = "MDA", Alpha2Code = "MD", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Monaco", Code = "MCO", Alpha2Code = "MC", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Mongolia", Code = "MNG", Alpha2Code = "MN", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Montenegro", Code = "MNE", Alpha2Code = "ME", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Morocco", Code = "MAR", Alpha2Code = "MA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Mozambique", Code = "MOZ", Alpha2Code = "MZ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Myanmar", Code = "MMR", Alpha2Code = "MM", IsActive = true },

            // N
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Namibia", Code = "NAM", Alpha2Code = "NA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Nauru", Code = "NRU", Alpha2Code = "NR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Nepal", Code = "NPL", Alpha2Code = "NP", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Netherlands", Code = "NLD", Alpha2Code = "NL", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "New Zealand", Code = "NZL", Alpha2Code = "NZ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Nicaragua", Code = "NIC", Alpha2Code = "NI", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Niger", Code = "NER", Alpha2Code = "NE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Nigeria", Code = "NGA", Alpha2Code = "NG", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "North Korea", Code = "PRK", Alpha2Code = "KP", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "North Macedonia", Code = "MKD", Alpha2Code = "MK", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Norway", Code = "NOR", Alpha2Code = "NO", IsActive = true },

            // O
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Oman", Code = "OMN", Alpha2Code = "OM", IsActive = true },

            // P
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pakistan", Code = "PAK", Alpha2Code = "PK", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Palau", Code = "PLW", Alpha2Code = "PW", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Palestine", Code = "PSE", Alpha2Code = "PS", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Panama", Code = "PAN", Alpha2Code = "PA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Papua New Guinea", Code = "PNG", Alpha2Code = "PG", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Paraguay", Code = "PRY", Alpha2Code = "PY", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Peru", Code = "PER", Alpha2Code = "PE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Philippines", Code = "PHL", Alpha2Code = "PH", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Poland", Code = "POL", Alpha2Code = "PL", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Portugal", Code = "PRT", Alpha2Code = "PT", IsActive = true },

            // Q
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Qatar", Code = "QAT", Alpha2Code = "QA", IsActive = true },

            // R
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Romania", Code = "ROU", Alpha2Code = "RO", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Russia", Code = "RUS", Alpha2Code = "RU", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Rwanda", Code = "RWA", Alpha2Code = "RW", IsActive = true },

            // S
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Saint Kitts and Nevis", Code = "KNA", Alpha2Code = "KN", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Saint Lucia", Code = "LCA", Alpha2Code = "LC", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Saint Vincent and the Grenadines", Code = "VCT", Alpha2Code = "VC", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Samoa", Code = "WSM", Alpha2Code = "WS", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "San Marino", Code = "SMR", Alpha2Code = "SM", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Sao Tome and Principe", Code = "STP", Alpha2Code = "ST", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Saudi Arabia", Code = "SAU", Alpha2Code = "SA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Senegal", Code = "SEN", Alpha2Code = "SN", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Serbia", Code = "SRB", Alpha2Code = "RS", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Seychelles", Code = "SYC", Alpha2Code = "SC", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Sierra Leone", Code = "SLE", Alpha2Code = "SL", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Singapore", Code = "SGP", Alpha2Code = "SG", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Slovakia", Code = "SVK", Alpha2Code = "SK", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Slovenia", Code = "SVN", Alpha2Code = "SI", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Solomon Islands", Code = "SLB", Alpha2Code = "SB", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Somalia", Code = "SOM", Alpha2Code = "SO", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "South Africa", Code = "ZAF", Alpha2Code = "ZA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "South Korea", Code = "KOR", Alpha2Code = "KR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "South Sudan", Code = "SSD", Alpha2Code = "SS", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Spain", Code = "ESP", Alpha2Code = "ES", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Sri Lanka", Code = "LKA", Alpha2Code = "LK", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Sudan", Code = "SDN", Alpha2Code = "SD", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Suriname", Code = "SUR", Alpha2Code = "SR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Sweden", Code = "SWE", Alpha2Code = "SE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Switzerland", Code = "CHE", Alpha2Code = "CH", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Syria", Code = "SYR", Alpha2Code = "SY", IsActive = true },

            // T
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Taiwan", Code = "TWN", Alpha2Code = "TW", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Tajikistan", Code = "TJK", Alpha2Code = "TJ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Tanzania", Code = "TZA", Alpha2Code = "TZ", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Thailand", Code = "THA", Alpha2Code = "TH", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Timor-Leste", Code = "TLS", Alpha2Code = "TL", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Togo", Code = "TGO", Alpha2Code = "TG", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Tonga", Code = "TON", Alpha2Code = "TO", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Trinidad and Tobago", Code = "TTO", Alpha2Code = "TT", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Tunisia", Code = "TUN", Alpha2Code = "TN", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Turkey", Code = "TUR", Alpha2Code = "TR", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Turkmenistan", Code = "TKM", Alpha2Code = "TM", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Tuvalu", Code = "TUV", Alpha2Code = "TV", IsActive = true },

            // U
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Uganda", Code = "UGA", Alpha2Code = "UG", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Ukraine", Code = "UKR", Alpha2Code = "UA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "United Arab Emirates", Code = "ARE", Alpha2Code = "AE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "United Kingdom", Code = "GBR", Alpha2Code = "GB", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "United States", Code = "USA", Alpha2Code = "US", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Uruguay", Code = "URY", Alpha2Code = "UY", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Uzbekistan", Code = "UZB", Alpha2Code = "UZ", IsActive = true },

            // V
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Vanuatu", Code = "VUT", Alpha2Code = "VU", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Vatican City", Code = "VAT", Alpha2Code = "VA", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Venezuela", Code = "VEN", Alpha2Code = "VE", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Vietnam", Code = "VNM", Alpha2Code = "VN", IsActive = true },

            // Y
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Yemen", Code = "YEM", Alpha2Code = "YE", IsActive = true },

            // Z
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Zambia", Code = "ZMB", Alpha2Code = "ZM", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Zimbabwe", Code = "ZWE", Alpha2Code = "ZW", IsActive = true }
        };
    }
}

