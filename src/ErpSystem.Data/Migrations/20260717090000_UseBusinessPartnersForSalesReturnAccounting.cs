using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Replaces the legacy CRM Customer key at the return/credit/refund boundary with the canonical
/// tenant-scoped BusinessPartner key used by Sales orders and Finance AR.
/// </summary>
[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
[Migration("20260717090000_UseBusinessPartnersForSalesReturnAccounting")]
public partial class UseBusinessPartnersForSalesReturnAccounting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A CustomerId cannot be inferred as a BusinessPartnerId. This development-stage hard cut
        // deliberately refuses to transform test documents rather than attaching them to the wrong AR party.
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [dbo].[ReturnOrders])
               OR EXISTS (SELECT 1 FROM [dbo].[CreditNotes])
               OR EXISTS (SELECT 1 FROM [dbo].[Refunds])
            BEGIN
                THROW 51000, 'Reset legacy ReturnOrders, CreditNotes, and Refunds before applying the BusinessPartner accounting identity migration. CustomerId values are not a safe BusinessPartner mapping.', 1;
            END;
            """);

        DropLegacyCustomerForeignKeys(migrationBuilder, "CustomerId");

        migrationBuilder.RenameColumn(name: "CustomerId", table: "ReturnOrders", newName: "BusinessPartnerId");
        migrationBuilder.RenameColumn(name: "CustomerId", table: "CreditNotes", newName: "BusinessPartnerId");
        migrationBuilder.RenameColumn(name: "CustomerId", table: "Refunds", newName: "BusinessPartnerId");

        RenameLegacyCustomerIndexes(migrationBuilder, "CustomerId", "BusinessPartnerId");

        migrationBuilder.AddForeignKey(
            name: "FK_ReturnOrders_BusinessPartners_BusinessPartnerId",
            table: "ReturnOrders",
            column: "BusinessPartnerId",
            principalTable: "BusinessPartners",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_CreditNotes_BusinessPartners_BusinessPartnerId",
            table: "CreditNotes",
            column: "BusinessPartnerId",
            principalTable: "BusinessPartners",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_Refunds_BusinessPartners_BusinessPartnerId",
            table: "Refunds",
            column: "BusinessPartnerId",
            principalTable: "BusinessPartners",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback is also a clean dev-data transition: a BusinessPartnerId is not a safe legacy CustomerId.
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [dbo].[ReturnOrders])
               OR EXISTS (SELECT 1 FROM [dbo].[CreditNotes])
               OR EXISTS (SELECT 1 FROM [dbo].[Refunds])
            BEGIN
                THROW 51001, 'Reset ReturnOrders, CreditNotes, and Refunds before rolling back the BusinessPartner accounting identity migration.', 1;
            END;
            """);

        migrationBuilder.DropForeignKey(name: "FK_ReturnOrders_BusinessPartners_BusinessPartnerId", table: "ReturnOrders");
        migrationBuilder.DropForeignKey(name: "FK_CreditNotes_BusinessPartners_BusinessPartnerId", table: "CreditNotes");
        migrationBuilder.DropForeignKey(name: "FK_Refunds_BusinessPartners_BusinessPartnerId", table: "Refunds");

        migrationBuilder.RenameColumn(name: "BusinessPartnerId", table: "ReturnOrders", newName: "CustomerId");
        migrationBuilder.RenameColumn(name: "BusinessPartnerId", table: "CreditNotes", newName: "CustomerId");
        migrationBuilder.RenameColumn(name: "BusinessPartnerId", table: "Refunds", newName: "CustomerId");

        RenameLegacyCustomerIndexes(migrationBuilder, "BusinessPartnerId", "CustomerId");

        AddLegacyCustomerForeignKeys(migrationBuilder, "CustomerId");
    }

    private static void DropLegacyCustomerForeignKeys(MigrationBuilder migrationBuilder, string columnName)
    {
        migrationBuilder.Sql($"""
            DECLARE @sql nvarchar(max) = N'';
            SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            WHERE OBJECT_NAME(fk.parent_object_id) IN (N'ReturnOrders', N'CreditNotes', N'Refunds')
              AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = N'{columnName}';
            IF @sql <> N'' EXEC sp_executesql @sql;
            """);
    }

    private static void RenameLegacyCustomerIndexes(MigrationBuilder migrationBuilder, string fromColumnName, string toColumnName)
    {
        migrationBuilder.Sql($"""
            DECLARE @table sysname;
            DECLARE @fromIndex sysname;
            DECLARE @toIndex sysname;

            DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT v.TableName, N'IX_' + v.TableName + N'_{fromColumnName}', N'IX_' + v.TableName + N'_{toColumnName}'
            FROM (VALUES (N'ReturnOrders'), (N'CreditNotes'), (N'Refunds')) v(TableName);

            OPEN index_cursor;
            FETCH NEXT FROM index_cursor INTO @table, @fromIndex, @toIndex;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[' + @table + N']') AND name = @fromIndex)
                    EXEC sp_rename N'[dbo].[' + @table + N'].[' + @fromIndex + N']', @toIndex, N'INDEX';

                FETCH NEXT FROM index_cursor INTO @table, @fromIndex, @toIndex;
            END;
            CLOSE index_cursor;
            DEALLOCATE index_cursor;
            """);
    }

    private static void AddLegacyCustomerForeignKeys(MigrationBuilder migrationBuilder, string columnName)
    {
        migrationBuilder.Sql($"""
            DECLARE @customerTable sysname = CASE
                WHEN OBJECT_ID(N'[dbo].[Customer]', N'U') IS NOT NULL THEN N'Customer'
                WHEN OBJECT_ID(N'[dbo].[Customers]', N'U') IS NOT NULL THEN N'Customers'
                ELSE NULL
            END;

            IF @customerTable IS NULL
                THROW 51002, 'Legacy Customer table was not found while rolling back the BusinessPartner accounting identity migration.', 1;

            EXEC(N'ALTER TABLE [dbo].[ReturnOrders] ADD CONSTRAINT [FK_ReturnOrders_Customer_CustomerId] FOREIGN KEY ([{columnName}]) REFERENCES [dbo].[' + @customerTable + N'] ([Id]);');
            EXEC(N'ALTER TABLE [dbo].[CreditNotes] ADD CONSTRAINT [FK_CreditNotes_Customer_CustomerId] FOREIGN KEY ([{columnName}]) REFERENCES [dbo].[' + @customerTable + N'] ([Id]);');
            EXEC(N'ALTER TABLE [dbo].[Refunds] ADD CONSTRAINT [FK_Refunds_Customer_CustomerId] FOREIGN KEY ([{columnName}]) REFERENCES [dbo].[' + @customerTable + N'] ([Id]);');
            """);
    }
}
