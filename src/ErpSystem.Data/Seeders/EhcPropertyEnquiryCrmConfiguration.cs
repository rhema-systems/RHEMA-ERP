namespace ErpSystem.Data.Seeders;

/// <summary>Idempotent Sales-department configuration for the property-enquiry CRM handoff.</summary>
public static class EhcPropertyEnquiryCrmConfiguration
{
    public const string Sql = """
        INSERT Departments(Id,TenantId,Name,Code,AccountCode,Description,DepartmentType,IsActive,CreatedAt,IsDeleted)
        SELECT NEWID(),t.Id,N'Sales',N'SALES',N'SALES',N'Property listing enquiry follow-up and CRM engagement',11,1,SYSUTCDATETIME(),0
        FROM Tenants t
        WHERE t.IsDeleted=0
          AND NOT EXISTS(
              SELECT 1 FROM Departments d
              WHERE d.TenantId=t.Id AND d.IsDeleted=0 AND d.DepartmentType=11);
        """;
}
