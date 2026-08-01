using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260801150000_TDC0601InventoryItemIdentifiers")]
public sealed class TDC0601InventoryItemIdentifiers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_InventoryItems_ItemCode",
            table: "InventoryItems");

        migrationBuilder.AddColumn<string>(
            name: "AlternateBarcode",
            table: "InventoryItems",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Barcode",
            table: "InventoryItems",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "QRCode",
            table: "InventoryItems",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE [dbo].[ItemUnitsOfMeasure]
            SET [Barcode] = NULLIF(UPPER(LTRIM(RTRIM([Barcode]))), N'')
            WHERE [Barcode] IS NOT NULL;

            IF EXISTS
            (
                SELECT 1
                FROM [dbo].[ItemUnitsOfMeasure] AS [unit]
                INNER JOIN [dbo].[InventoryItems] AS [item] ON [item].[Id] = [unit].[InventoryItemId]
                INNER JOIN [dbo].[UnitsOfMeasure] AS [definition] ON [definition].[Id] = [unit].[UnitOfMeasureId]
                WHERE [unit].[TenantId] <> [item].[TenantId]
                   OR [unit].[TenantId] <> [definition].[TenantId]
            )
                THROW 51660, 'TDC-0601 cannot activate because an item-unit record crosses tenant boundaries.', 1;

            IF EXISTS
            (
                SELECT [TenantId], [Identifier]
                FROM
                (
                    SELECT [TenantId], [Barcode] AS [Identifier]
                    FROM [dbo].[ItemUnitsOfMeasure]
                    WHERE [IsDeleted] = 0 AND [Barcode] IS NOT NULL
                ) AS [identifiers]
                GROUP BY [TenantId], [Identifier]
                HAVING COUNT_BIG(*) > 1
            )
                THROW 51661, 'TDC-0601 cannot activate because duplicate current-tenant item-unit barcodes already exist.', 1;
            """);

        migrationBuilder.AddCheckConstraint(
            name: "CK_InventoryItems_Identifiers_Normalized",
            table: "InventoryItems",
            sql: "([Barcode] IS NULL OR [Barcode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([Barcode]))) COLLATE Latin1_General_100_BIN2) AND ([AlternateBarcode] IS NULL OR [AlternateBarcode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([AlternateBarcode]))) COLLATE Latin1_General_100_BIN2) AND ([QRCode] IS NULL OR [QRCode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([QRCode]))) COLLATE Latin1_General_100_BIN2)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ItemUnitsOfMeasure_Barcode_Normalized",
            table: "ItemUnitsOfMeasure",
            sql: "[Barcode] IS NULL OR [Barcode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([Barcode]))) COLLATE Latin1_General_100_BIN2");

        migrationBuilder.CreateIndex(
            name: "IX_InventoryItems_TenantId_AlternateBarcode",
            table: "InventoryItems",
            columns: new[] { "TenantId", "AlternateBarcode" },
            unique: true,
            filter: "[AlternateBarcode] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_InventoryItems_TenantId_Barcode",
            table: "InventoryItems",
            columns: new[] { "TenantId", "Barcode" },
            unique: true,
            filter: "[Barcode] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_InventoryItems_TenantId_ItemCode",
            table: "InventoryItems",
            columns: new[] { "TenantId", "ItemCode" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_InventoryItems_TenantId_QRCode",
            table: "InventoryItems",
            columns: new[] { "TenantId", "QRCode" },
            unique: true,
            filter: "[QRCode] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_ItemUnitsOfMeasure_TenantId_Barcode",
            table: "ItemUnitsOfMeasure",
            columns: new[] { "TenantId", "Barcode" },
            unique: true,
            filter: "[Barcode] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.Sql(CreateInventoryItemTrigger);
        migrationBuilder.Sql(CreateItemUnitTrigger);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_TDC0601_ItemUnitsOfMeasure_IdentifierIntegrity];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_TDC0601_InventoryItems_IdentifierIntegrity];");

        migrationBuilder.DropIndex(name: "IX_ItemUnitsOfMeasure_TenantId_Barcode", table: "ItemUnitsOfMeasure");
        migrationBuilder.DropIndex(name: "IX_InventoryItems_TenantId_QRCode", table: "InventoryItems");
        migrationBuilder.DropIndex(name: "IX_InventoryItems_TenantId_ItemCode", table: "InventoryItems");
        migrationBuilder.DropIndex(name: "IX_InventoryItems_TenantId_Barcode", table: "InventoryItems");
        migrationBuilder.DropIndex(name: "IX_InventoryItems_TenantId_AlternateBarcode", table: "InventoryItems");
        migrationBuilder.DropCheckConstraint(name: "CK_ItemUnitsOfMeasure_Barcode_Normalized", table: "ItemUnitsOfMeasure");
        migrationBuilder.DropCheckConstraint(name: "CK_InventoryItems_Identifiers_Normalized", table: "InventoryItems");
        migrationBuilder.DropColumn(name: "QRCode", table: "InventoryItems");
        migrationBuilder.DropColumn(name: "Barcode", table: "InventoryItems");
        migrationBuilder.DropColumn(name: "AlternateBarcode", table: "InventoryItems");

        migrationBuilder.CreateIndex(
            name: "IX_InventoryItems_ItemCode",
            table: "InventoryItems",
            column: "ItemCode",
            unique: true);
    }

    private const string CreateInventoryItemTrigger =
        """
        CREATE OR ALTER TRIGGER [dbo].[TR_TDC0601_InventoryItems_IdentifierIntegrity]
        ON [dbo].[InventoryItems]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS
            (
                SELECT [TenantId], [Identifier]
                FROM
                (
                    SELECT [TenantId], [Barcode] AS [Identifier] FROM [dbo].[InventoryItems] WHERE [IsDeleted] = 0 AND [Barcode] IS NOT NULL
                    UNION ALL
                    SELECT [TenantId], [AlternateBarcode] FROM [dbo].[InventoryItems] WHERE [IsDeleted] = 0 AND [AlternateBarcode] IS NOT NULL
                    UNION ALL
                    SELECT [TenantId], [QRCode] FROM [dbo].[InventoryItems] WHERE [IsDeleted] = 0 AND [QRCode] IS NOT NULL
                    UNION ALL
                    SELECT [TenantId], [Barcode] FROM [dbo].[ItemUnitsOfMeasure] WHERE [IsDeleted] = 0 AND [Barcode] IS NOT NULL
                ) AS [identifiers]
                GROUP BY [TenantId], [Identifier]
                HAVING COUNT_BIG(*) > 1
            )
                THROW 51662, 'Primary, alternate, QR, and item-unit identifiers must be unique in the current tenant.', 1;
        END;
        """;

    private const string CreateItemUnitTrigger =
        """
        CREATE OR ALTER TRIGGER [dbo].[TR_TDC0601_ItemUnitsOfMeasure_IdentifierIntegrity]
        ON [dbo].[ItemUnitsOfMeasure]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS
            (
                SELECT 1
                FROM [dbo].[ItemUnitsOfMeasure] AS [unit]
                INNER JOIN [dbo].[InventoryItems] AS [item] ON [item].[Id] = [unit].[InventoryItemId]
                INNER JOIN [dbo].[UnitsOfMeasure] AS [definition] ON [definition].[Id] = [unit].[UnitOfMeasureId]
                WHERE [unit].[TenantId] <> [item].[TenantId]
                   OR [unit].[TenantId] <> [definition].[TenantId]
            )
                THROW 51663, 'Item-unit identifiers cannot cross tenant boundaries.', 1;

            IF EXISTS
            (
                SELECT [TenantId], [Identifier]
                FROM
                (
                    SELECT [TenantId], [Barcode] AS [Identifier] FROM [dbo].[InventoryItems] WHERE [IsDeleted] = 0 AND [Barcode] IS NOT NULL
                    UNION ALL
                    SELECT [TenantId], [AlternateBarcode] FROM [dbo].[InventoryItems] WHERE [IsDeleted] = 0 AND [AlternateBarcode] IS NOT NULL
                    UNION ALL
                    SELECT [TenantId], [QRCode] FROM [dbo].[InventoryItems] WHERE [IsDeleted] = 0 AND [QRCode] IS NOT NULL
                    UNION ALL
                    SELECT [TenantId], [Barcode] FROM [dbo].[ItemUnitsOfMeasure] WHERE [IsDeleted] = 0 AND [Barcode] IS NOT NULL
                ) AS [identifiers]
                GROUP BY [TenantId], [Identifier]
                HAVING COUNT_BIG(*) > 1
            )
                THROW 51664, 'Primary, alternate, QR, and item-unit identifiers must be unique in the current tenant.', 1;
        END;
        """;
}
