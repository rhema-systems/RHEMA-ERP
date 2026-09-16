using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0616ItemMasterProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ValuationMethod",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "IsCostCentreApplicable",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsProjectApplicable",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InventoryItems",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.Sql(
                """
                UPDATE [dbo].[InventoryItems]
                SET [ValuationMethod] = CASE WHEN [ValuationMethod] BETWEEN 1 AND 5 THEN [ValuationMethod] ELSE 1 END,
                    [ItemType] = CASE WHEN [ItemType] BETWEEN 1 AND 4 THEN [ItemType] ELSE 1 END,
                    [Status] = CASE WHEN [Status] BETWEEN 1 AND 4 THEN [Status] ELSE 1 END,
                    [MinimumLevel] = CASE WHEN [MinimumLevel] < 0 THEN 0 ELSE [MinimumLevel] END,
                    [MaximumLevel] = CASE
                        WHEN [MaximumLevel] < 0 THEN 0
                        WHEN [MaximumLevel] > 0 AND ([MaximumLevel] < [MinimumLevel] OR [MaximumLevel] < [ReorderLevel])
                            THEN CASE WHEN [MinimumLevel] > [ReorderLevel] THEN [MinimumLevel] ELSE [ReorderLevel] END
                        ELSE [MaximumLevel]
                    END,
                    [ReorderLevel] = CASE WHEN [ReorderLevel] < 0 THEN 0 ELSE [ReorderLevel] END,
                    [ReorderQuantity] = CASE WHEN [ReorderQuantity] < 0 THEN 0 ELSE [ReorderQuantity] END,
                    [SafetyStock] = CASE WHEN [SafetyStock] < 0 THEN 0 ELSE [SafetyStock] END,
                    [ShelfLifeDays] = CASE
                        WHEN [IsExpirationTracked] = 1 AND ISNULL([ShelfLifeDays], 0) < 1 THEN 1
                        ELSE [ShelfLifeDays]
                    END;

                IF EXISTS
                (
                    SELECT 1
                    FROM [dbo].[InventoryItems]
                    WHERE LEN(LTRIM(RTRIM([ItemCode]))) = 0
                       OR LEN(LTRIM(RTRIM([Name]))) = 0
                       OR LEN(LTRIM(RTRIM([UnitOfMeasure]))) = 0
                )
                    THROW 51120, 'INV_ITEM_PROFILE_REQUIRED_LEGACY_DATA_INVALID', 1;

                UPDATE [dbo].[InventoryItems]
                SET [ItemCode] = UPPER(LTRIM(RTRIM([ItemCode]))),
                    [Name] = LTRIM(RTRIM([Name])),
                    [UnitOfMeasure] = UPPER(LTRIM(RTRIM([UnitOfMeasure])));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId_IsProjectApplicable_IsCostCentreApplicable_Status",
                table: "InventoryItems",
                columns: new[] { "TenantId", "IsProjectApplicable", "IsCostCentreApplicable", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItems_ProfileEnums",
                table: "InventoryItems",
                sql: "[ValuationMethod] BETWEEN 1 AND 5 AND [Status] BETWEEN 1 AND 4 AND [ItemType] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItems_ProfileLevels",
                table: "InventoryItems",
                sql: "[MinimumLevel] >= 0 AND [MaximumLevel] >= 0 AND [ReorderLevel] >= 0 AND [ReorderQuantity] >= 0 AND [SafetyStock] >= 0 AND ([MaximumLevel] = 0 OR ([MinimumLevel] <= [MaximumLevel] AND [ReorderLevel] <= [MaximumLevel]))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItems_ProfileRequired",
                table: "InventoryItems",
                sql: "LEN(LTRIM(RTRIM([ItemCode]))) > 0 AND LEN(LTRIM(RTRIM([Name]))) > 0 AND LEN(LTRIM(RTRIM([UnitOfMeasure]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItems_ProfileTracking",
                table: "InventoryItems",
                sql: "[IsExpirationTracked] = 0 OR [ShelfLifeDays] > 0");

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_TDC0616_InventoryItems_ProfileIntegrity]
                ON [dbo].[InventoryItems]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[IsValuationLocked] = 1
                          AND i.[ValuationMethod] <> d.[ValuationMethod]
                    )
                        THROW 51121, 'INV_ITEM_VALUATION_METHOD_LOCKED', 1;

                    DECLARE @ProfileRows TABLE ([Id] uniqueidentifier NOT NULL PRIMARY KEY);

                    INSERT INTO @ProfileRows ([Id])
                    SELECT i.[Id]
                    FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE d.[Id] IS NULL
                           OR i.[ItemCode] COLLATE Latin1_General_100_BIN2 <> d.[ItemCode] COLLATE Latin1_General_100_BIN2
                           OR DATALENGTH(i.[ItemCode]) <> DATALENGTH(d.[ItemCode])
                           OR i.[Name] <> d.[Name]
                           OR DATALENGTH(i.[Name]) <> DATALENGTH(d.[Name])
                           OR ISNULL(i.[Description], '') <> ISNULL(d.[Description], '')
                           OR DATALENGTH(ISNULL(i.[Description], '')) <> DATALENGTH(ISNULL(d.[Description], ''))
                           OR i.[CategoryId] <> d.[CategoryId]
                           OR i.[UnitOfMeasure] COLLATE Latin1_General_100_BIN2 <> d.[UnitOfMeasure] COLLATE Latin1_General_100_BIN2
                           OR DATALENGTH(i.[UnitOfMeasure]) <> DATALENGTH(d.[UnitOfMeasure])
                           OR ISNULL(i.[UnitOfMeasureScheduleId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[UnitOfMeasureScheduleId], '00000000-0000-0000-0000-000000000000')
                           OR i.[ItemType] <> d.[ItemType]
                           OR i.[Status] <> d.[Status]
                           OR i.[ValuationMethod] <> d.[ValuationMethod]
                           OR i.[StandardCost] <> d.[StandardCost]
                           OR i.[MinimumLevel] <> d.[MinimumLevel]
                           OR i.[MaximumLevel] <> d.[MaximumLevel]
                           OR i.[ReorderLevel] <> d.[ReorderLevel]
                           OR i.[ReorderQuantity] <> d.[ReorderQuantity]
                           OR i.[SafetyStock] <> d.[SafetyStock]
                           OR i.[LeadTimeDays] <> d.[LeadTimeDays]
                           OR i.[SafetyLeadTimeDays] <> d.[SafetyLeadTimeDays]
                           OR i.[IsBatchTracked] <> d.[IsBatchTracked]
                           OR i.[IsSerialTracked] <> d.[IsSerialTracked]
                           OR i.[IsLotTracked] <> d.[IsLotTracked]
                           OR i.[IsExpirationTracked] <> d.[IsExpirationTracked]
                           OR ISNULL(i.[ShelfLifeDays], -1) <> ISNULL(d.[ShelfLifeDays], -1)
                           OR i.[IsProjectApplicable] <> d.[IsProjectApplicable]
                           OR i.[IsCostCentreApplicable] <> d.[IsCostCentreApplicable];

                    IF NOT EXISTS (SELECT 1 FROM @ProfileRows)
                        RETURN;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN @ProfileRows p ON p.[Id] = i.[Id]
                        WHERE DATALENGTH(i.[ItemCode]) <> DATALENGTH(LTRIM(RTRIM(i.[ItemCode])))
                           OR i.[ItemCode] COLLATE Latin1_General_100_BIN2 <> UPPER(i.[ItemCode]) COLLATE Latin1_General_100_BIN2
                           OR DATALENGTH(i.[Name]) <> DATALENGTH(LTRIM(RTRIM(i.[Name])))
                           OR DATALENGTH(i.[UnitOfMeasure]) <> DATALENGTH(LTRIM(RTRIM(i.[UnitOfMeasure])))
                           OR i.[UnitOfMeasure] COLLATE Latin1_General_100_BIN2 <> UPPER(i.[UnitOfMeasure]) COLLATE Latin1_General_100_BIN2
                    )
                        THROW 51122, 'INV_ITEM_PROFILE_NOT_NORMALIZED', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN @ProfileRows p ON p.[Id] = i.[Id]
                        LEFT JOIN [dbo].[InventoryCategories] c
                          ON c.[Id] = i.[CategoryId]
                         AND c.[TenantId] = i.[TenantId]
                         AND c.[IsDeleted] = 0
                         AND c.[IsActive] = 1
                        WHERE c.[Id] IS NULL
                    )
                        THROW 51123, 'INV_ITEM_CATEGORY_LINEAGE_INVALID', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN @ProfileRows p ON p.[Id] = i.[Id]
                        LEFT JOIN [dbo].[UnitsOfMeasure] u
                          ON u.[TenantId] = i.[TenantId]
                         AND u.[Code] = i.[UnitOfMeasure]
                         AND u.[IsDeleted] = 0
                         AND u.[IsActive] = 1
                        WHERE u.[Id] IS NULL
                    )
                        THROW 51124, 'INV_ITEM_UOM_LINEAGE_INVALID', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN @ProfileRows p ON p.[Id] = i.[Id]
                        LEFT JOIN [dbo].[UnitOfMeasureSchedules] s
                          ON s.[Id] = i.[UnitOfMeasureScheduleId]
                         AND s.[TenantId] = i.[TenantId]
                         AND s.[IsDeleted] = 0
                         AND s.[IsActive] = 1
                        WHERE i.[UnitOfMeasureScheduleId] IS NOT NULL
                          AND s.[Id] IS NULL
                    )
                        THROW 51125, 'INV_ITEM_UOM_SCHEDULE_LINEAGE_INVALID', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN @ProfileRows p ON p.[Id] = i.[Id]
                        WHERE i.[ValuationMethod] = 4 AND i.[StandardCost] <= 0
                    )
                        THROW 51126, 'INV_ITEM_STANDARD_COST_REQUIRED', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_TDC0616_InventoryItems_ProfileIntegrity];");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_TenantId_IsProjectApplicable_IsCostCentreApplicable_Status",
                table: "InventoryItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItems_ProfileEnums",
                table: "InventoryItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItems_ProfileLevels",
                table: "InventoryItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItems_ProfileRequired",
                table: "InventoryItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItems_ProfileTracking",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsCostCentreApplicable",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsProjectApplicable",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InventoryItems");

            migrationBuilder.AlterColumn<int>(
                name: "ValuationMethod",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);
        }
    }
}
