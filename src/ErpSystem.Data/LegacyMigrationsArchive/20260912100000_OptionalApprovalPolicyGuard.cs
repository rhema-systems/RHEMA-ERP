using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912100000_OptionalApprovalPolicyGuard")]
public sealed class OptionalApprovalPolicyGuard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER FUNCTION dbo.WorkflowApprovalEntityKey(@value nvarchar(100))
            RETURNS nvarchar(100)
            AS BEGIN
                SET @value = UPPER(COALESCE(@value,N''));
                WHILE PATINDEX(N'%[^A-Z0-9]%', @value COLLATE Latin1_General_100_BIN2) > 0
                    SET @value = STUFF(@value, PATINDEX(N'%[^A-Z0-9]%', @value COLLATE Latin1_General_100_BIN2), 1, N'');
                RETURN @value;
            END
            """);
        migrationBuilder.Sql("""
            CREATE OR ALTER FUNCTION dbo.WorkflowApprovalRequiredAtSubmission(
                @tenant uniqueidentifier, @entity nvarchar(100), @record uniqueidentifier)
            RETURNS bit
            AS BEGIN
                IF @tenant IS NULL OR @tenant = '00000000-0000-0000-0000-000000000000' RETURN 1;
                IF @record IS NULL OR @record = '00000000-0000-0000-0000-000000000000' RETURN 1;
                IF NULLIF(LTRIM(RTRIM(@entity)),N'') IS NULL RETURN 1;
                IF EXISTS (
                    SELECT 1 FROM dbo.WorkflowEntityTypes e
                    WHERE e.TenantId=@tenant
                      AND (dbo.WorkflowApprovalEntityKey(e.Code)=dbo.WorkflowApprovalEntityKey(@entity)
                        OR dbo.WorkflowApprovalEntityKey(e.Name)=dbo.WorkflowApprovalEntityKey(@entity))
                      AND (EXISTS (SELECT 1 FROM dbo.WorkflowDefinitions d
                            WHERE e.IsDeleted=0 AND e.IsActive=1 AND d.TenantId=@tenant AND d.EntityTypeId=e.Id AND d.IsDeleted=0 AND d.IsActive=1 AND d.LifecycleStatus=1)
                        OR EXISTS (SELECT 1 FROM dbo.WorkflowInstances w
                            WHERE w.TenantId=@tenant AND w.EntityTypeId=e.Id AND w.EntityId=@record
                              AND w.IsDeleted=0 AND w.Status IN (0,1,5,6)))) RETURN 1;
                RETURN 0;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.WorkflowApprovalRequiredAtSubmission;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.WorkflowApprovalEntityKey;");
    }
}
