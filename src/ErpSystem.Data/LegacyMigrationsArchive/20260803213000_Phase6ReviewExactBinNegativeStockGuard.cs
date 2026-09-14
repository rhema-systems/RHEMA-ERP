using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260803213000_Phase6ReviewExactBinNegativeStockGuard")]
public partial class Phase6ReviewExactBinNegativeStockGuard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_InventoryLocations_NegativeStockGuard]
            ON [dbo].[InventoryLocations]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF NOT EXISTS (SELECT 1 FROM inserted WHERE [Quantity] < 0 OR [AvailableQuantity] < 0 OR [AllocatedQuantity] < 0)
                    RETURN;

                IF EXISTS (SELECT 1 FROM inserted WHERE [AllocatedQuantity] < 0)
                    THROW 51066, 'INV_NEGATIVE_STOCK_BIN_ALLOCATION_PROHIBITED: allocated bin stock cannot be negative.', 1;

                DECLARE @overrideId uniqueidentifier = TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0610_OVERRIDE_ID'));
                DECLARE @contextTransactionId bigint = TRY_CONVERT(bigint, SESSION_CONTEXT(N'TDC0610_TRANSACTION_ID'));
                DECLARE @currentTransactionId bigint = CONVERT(bigint, CURRENT_TRANSACTION_ID());

                IF @overrideId IS NULL OR @contextTransactionId IS NULL OR @contextTransactionId <> @currentTransactionId
                    THROW 51067, 'INV_NEGATIVE_STOCK_BIN_SQL_PROHIBITED: transaction-bound DEC-010 override context is required.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    LEFT JOIN [dbo].[WarehouseLocations] wl
                      ON wl.[Id] = i.[LocationId] AND wl.[TenantId] = i.[TenantId] AND wl.[IsDeleted] = 0
                    WHERE (i.[Quantity] < 0 OR i.[AvailableQuantity] < 0)
                      AND NOT EXISTS (
                          SELECT 1
                          FROM [dbo].[InventoryNegativeStockOverrides] o
                          WHERE o.[Id] = @overrideId
                            AND o.[TenantId] = i.[TenantId]
                            AND o.[InventoryItemId] = i.[InventoryItemId]
                            AND o.[WarehouseId] = CASE
                                WHEN wl.[IsConsignmentBin] = 1 AND wl.[ConsignmentWarehouseId] IS NOT NULL
                                    THEN wl.[ConsignmentWarehouseId]
                                ELSE wl.[WarehouseId]
                            END
                            AND o.[LocationId] = i.[LocationId]
                            AND o.[IsDeleted] = 0
                            AND o.[ConsumedAtUtc] IS NOT NULL
                            AND o.[ConsumedByReferenceId] = o.[ReferenceId]
                            AND o.[ConsumptionTransactionId] = @currentTransactionId
                            AND o.[ConsumedAtUtc] <= o.[ExpiresAtUtc]
                            AND d.[Id] IS NOT NULL
                            AND (d.[Quantity] - i.[Quantity]) <= o.[AuthorizedQuantity]
                            AND (d.[AvailableQuantity] - i.[AvailableQuantity]) <= o.[AuthorizedQuantity]
                      )
                )
                    THROW 51068, 'INV_NEGATIVE_STOCK_BIN_SQL_SCOPE_INVALID: DEC-010 override does not cover the exact tenant warehouse item bin and decrement.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_InventoryBalances_ExactBinNegativeStockGuard]
            ON [dbo].[InventoryBalances]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF NOT EXISTS (
                    SELECT 1 FROM inserted
                    WHERE [LocationId] IS NOT NULL
                      AND ([QuantityOnHand] < 0 OR [QuantityAvailable] < 0 OR [QuantityAllocated] < 0))
                    RETURN;

                IF EXISTS (SELECT 1 FROM inserted WHERE [LocationId] IS NOT NULL AND [QuantityAllocated] < 0)
                    THROW 51069, 'INV_NEGATIVE_STOCK_BALANCE_ALLOCATION_PROHIBITED: allocated location balance cannot be negative.', 1;

                DECLARE @overrideId uniqueidentifier = TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0610_OVERRIDE_ID'));
                DECLARE @contextTransactionId bigint = TRY_CONVERT(bigint, SESSION_CONTEXT(N'TDC0610_TRANSACTION_ID'));
                DECLARE @currentTransactionId bigint = CONVERT(bigint, CURRENT_TRANSACTION_ID());

                IF @overrideId IS NULL OR @contextTransactionId IS NULL OR @contextTransactionId <> @currentTransactionId
                    THROW 51070, 'INV_NEGATIVE_STOCK_BALANCE_SQL_PROHIBITED: transaction-bound DEC-010 override context is required.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE i.[LocationId] IS NOT NULL
                      AND (i.[QuantityOnHand] < 0 OR i.[QuantityAvailable] < 0)
                      AND NOT EXISTS (
                          SELECT 1
                          FROM [dbo].[InventoryNegativeStockOverrides] o
                          WHERE o.[Id] = @overrideId
                            AND o.[TenantId] = i.[TenantId]
                            AND o.[InventoryItemId] = i.[InventoryItemId]
                            AND o.[WarehouseId] = i.[WarehouseId]
                            AND o.[LocationId] = i.[LocationId]
                            AND o.[IsDeleted] = 0
                            AND o.[ConsumedAtUtc] IS NOT NULL
                            AND o.[ConsumedByReferenceId] = o.[ReferenceId]
                            AND o.[ConsumptionTransactionId] = @currentTransactionId
                            AND o.[ConsumedAtUtc] <= o.[ExpiresAtUtc]
                            AND d.[Id] IS NOT NULL
                            AND (d.[QuantityOnHand] - i.[QuantityOnHand]) <= o.[AuthorizedQuantity]
                            AND (d.[QuantityAvailable] - i.[QuantityAvailable]) <= o.[AuthorizedQuantity]
                      )
                )
                    THROW 51071, 'INV_NEGATIVE_STOCK_BALANCE_SQL_SCOPE_INVALID: DEC-010 override does not cover the exact tenant warehouse item location balance and decrement.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryBalances_ExactBinNegativeStockGuard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryLocations_NegativeStockGuard];");
    }
}
