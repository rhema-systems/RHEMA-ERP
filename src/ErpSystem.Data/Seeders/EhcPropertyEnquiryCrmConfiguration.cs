namespace ErpSystem.Data.Seeders;

/// <summary>Prevents the property-enquiry CRM handoff from provisioning legacy departments.</summary>
public static class EhcPropertyEnquiryCrmConfiguration
{
    public const string Sql = """
        -- UNIT-MKT is created by TdcOrganogramSeeder. Department records are historical only.
        SELECT 1;
        """;
}
