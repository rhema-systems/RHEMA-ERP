using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924233000_BusinessPartnerRoleCompatibility")]
public sealed class BusinessPartnerRoleCompatibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, true);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.BusinessPartners WHERE PartnerType='CustomerAndSupplier')
                OR EXISTS (SELECT 1 FROM dbo.BusinessPartnerRegistrations WHERE PartnerType='CustomerAndSupplier')
                THROW 51726, 'Retain dual-role partner compatibility while dual-role partners or applications exist.', 1;
            """);
        Apply(migrationBuilder, false);
    }

    private static void Apply(MigrationBuilder builder, bool upgrade)
    {
        Patch(builder, "TR_ProcurementTenderDocumentIssuances_Immutable", "('Supplier', 'Contractor', 'Both')", upgrade);
        Patch(builder, "TR_QS0521_Subcontracts_Governance", "('Supplier','Contractor','Both')", upgrade);
    }

    private static void Patch(MigrationBuilder builder, string trigger, string legacyRoles, bool upgrade)
    {
        const string roles = "('Supplier','Vendor','Manufacturer','Contractor','Both','CustomerAndSupplier')";
        var before = ("bp.PartnerType NOT IN " + (upgrade ? legacyRoles : roles)).Replace("'", "''");
        var after = ("bp.PartnerType NOT IN " + (upgrade ? roles : legacyRoles)).Replace("'", "''");
        // Patch only the role predicate in the installed trigger so other source,
        // tenant, workflow and immutability guards retain their current definitions.
        builder.Sql($"""
            DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.{trigger}', N'TR'));
            IF @definition IS NULL
                THROW 51727, 'Required partner governance trigger is missing; reconcile migration history before deployment.', 1;
            IF CHARINDEX(N'{before}', @definition)=0
            BEGIN
                IF CHARINDEX(N'{after}', @definition)>0 RETURN;
                THROW 51727, 'Partner governance trigger has an unrecognized role predicate; review it before deployment.', 1;
            END;
            SET @definition=REPLACE(@definition,N'{before}',N'{after}');
            -- SQL Server may store CREATE OR ALTER as CREATE followed by several
            -- spaces. Normalize only the DDL header; preserve the entire body.
            SET @definition=LTRIM(@definition);
            DECLARE @triggerPosition int=CHARINDEX(N'TRIGGER',UPPER(@definition));
            IF @triggerPosition=0 OR LEFT(UPPER(@definition),6) NOT IN (N'CREATE',N'ALTER ')
                THROW 51727, 'Partner governance trigger has an unrecognized DDL header; review it before deployment.', 1;
            SET @definition=N'ALTER '+SUBSTRING(@definition,@triggerPosition,LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """);
    }
}
