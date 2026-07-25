using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementPlanningCyclePublishBudgetLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddColumnIfMissing(migrationBuilder, "ProcurementPlans", "PreviousVersionId", "uniqueidentifier NULL");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlans", "PlanningCycle", "nvarchar(20) NOT NULL CONSTRAINT [DF_ProcurementPlans_PlanningCycle] DEFAULT N'Annual'");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlans", "PlanningQuarter", "nvarchar(10) NULL");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlans", "PublishComments", "nvarchar(2000) NULL");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlans", "PublishedById", "uniqueidentifier NULL");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlans", "PublishedDate", "datetime2 NULL");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlans", "RevisionNumber", "int NOT NULL CONSTRAINT [DF_ProcurementPlans_RevisionNumber] DEFAULT 1");

            AddColumnIfMissing(migrationBuilder, "ProcurementPlanItems", "ApprovedBudgetAmount", "decimal(18,2) NULL");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlanItems", "BudgetCategoryName", "nvarchar(100) NULL");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlanItems", "BudgetLineCode", "nvarchar(50) NULL");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlanItems", "BudgetNotes", "nvarchar(500) NULL");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlanItems", "ProcurementBudgetAllocationId", "uniqueidentifier NULL");
            AddColumnIfMissing(migrationBuilder, "ProcurementPlanItems", "ProcurementBudgetId", "uniqueidentifier NULL");

            CreateIndexIfMissing(migrationBuilder, "ProcurementPlans", "IX_ProcurementPlans_PlanningCycle", "PlanningCycle");
            CreateIndexIfMissing(migrationBuilder, "ProcurementPlans", "IX_ProcurementPlans_PlanningQuarter", "PlanningQuarter");
            CreateIndexIfMissing(migrationBuilder, "ProcurementPlans", "IX_ProcurementPlans_PreviousVersionId", "PreviousVersionId");
            CreateIndexIfMissing(migrationBuilder, "ProcurementPlans", "IX_ProcurementPlans_PublishedById", "PublishedById");
            CreateIndexIfMissing(migrationBuilder, "ProcurementPlans", "IX_ProcurementPlans_PublishedDate", "PublishedDate");
            CreateIndexIfMissing(migrationBuilder, "ProcurementPlanItems", "IX_ProcurementPlanItems_BudgetLineCode", "BudgetLineCode");
            CreateIndexIfMissing(migrationBuilder, "ProcurementPlanItems", "IX_ProcurementPlanItems_ProcurementBudgetAllocationId", "ProcurementBudgetAllocationId");
            CreateIndexIfMissing(migrationBuilder, "ProcurementPlanItems", "IX_ProcurementPlanItems_ProcurementBudgetId", "ProcurementBudgetId");

            AddForeignKeyIfMissing(
                migrationBuilder,
                "ProcurementPlanItems",
                "FK_ProcurementPlanItems_ProcurementBudgetAllocations_ProcurementBudgetAllocationId",
                "ProcurementBudgetAllocationId",
                "ProcurementBudgetAllocations",
                "Id");

            AddForeignKeyIfMissing(
                migrationBuilder,
                "ProcurementPlanItems",
                "FK_ProcurementPlanItems_ProcurementBudgets_ProcurementBudgetId",
                "ProcurementBudgetId",
                "ProcurementBudgets",
                "Id");

            AddForeignKeyIfMissing(
                migrationBuilder,
                "ProcurementPlans",
                "FK_ProcurementPlans_ProcurementPlans_PreviousVersionId",
                "PreviousVersionId",
                "ProcurementPlans",
                "Id");

            AddForeignKeyIfMissing(
                migrationBuilder,
                "ProcurementPlans",
                "FK_ProcurementPlans_Users_PublishedById",
                "PublishedById",
                "Users",
                "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropForeignKeyIfExists(migrationBuilder, "ProcurementPlanItems", "FK_ProcurementPlanItems_ProcurementBudgetAllocations_ProcurementBudgetAllocationId");
            DropForeignKeyIfExists(migrationBuilder, "ProcurementPlanItems", "FK_ProcurementPlanItems_ProcurementBudgets_ProcurementBudgetId");
            DropForeignKeyIfExists(migrationBuilder, "ProcurementPlans", "FK_ProcurementPlans_ProcurementPlans_PreviousVersionId");
            DropForeignKeyIfExists(migrationBuilder, "ProcurementPlans", "FK_ProcurementPlans_Users_PublishedById");

            DropIndexIfExists(migrationBuilder, "ProcurementPlans", "IX_ProcurementPlans_PlanningCycle");
            DropIndexIfExists(migrationBuilder, "ProcurementPlans", "IX_ProcurementPlans_PlanningQuarter");
            DropIndexIfExists(migrationBuilder, "ProcurementPlans", "IX_ProcurementPlans_PreviousVersionId");
            DropIndexIfExists(migrationBuilder, "ProcurementPlans", "IX_ProcurementPlans_PublishedById");
            DropIndexIfExists(migrationBuilder, "ProcurementPlans", "IX_ProcurementPlans_PublishedDate");
            DropIndexIfExists(migrationBuilder, "ProcurementPlanItems", "IX_ProcurementPlanItems_BudgetLineCode");
            DropIndexIfExists(migrationBuilder, "ProcurementPlanItems", "IX_ProcurementPlanItems_ProcurementBudgetAllocationId");
            DropIndexIfExists(migrationBuilder, "ProcurementPlanItems", "IX_ProcurementPlanItems_ProcurementBudgetId");

            DropColumnIfExists(migrationBuilder, "ProcurementPlans", "PreviousVersionId");
            DropColumnIfExists(migrationBuilder, "ProcurementPlans", "PlanningCycle");
            DropColumnIfExists(migrationBuilder, "ProcurementPlans", "PlanningQuarter");
            DropColumnIfExists(migrationBuilder, "ProcurementPlans", "PublishComments");
            DropColumnIfExists(migrationBuilder, "ProcurementPlans", "PublishedById");
            DropColumnIfExists(migrationBuilder, "ProcurementPlans", "PublishedDate");
            DropColumnIfExists(migrationBuilder, "ProcurementPlans", "RevisionNumber");

            DropColumnIfExists(migrationBuilder, "ProcurementPlanItems", "ApprovedBudgetAmount");
            DropColumnIfExists(migrationBuilder, "ProcurementPlanItems", "BudgetCategoryName");
            DropColumnIfExists(migrationBuilder, "ProcurementPlanItems", "BudgetLineCode");
            DropColumnIfExists(migrationBuilder, "ProcurementPlanItems", "BudgetNotes");
            DropColumnIfExists(migrationBuilder, "ProcurementPlanItems", "ProcurementBudgetAllocationId");
            DropColumnIfExists(migrationBuilder, "ProcurementPlanItems", "ProcurementBudgetId");
        }

        private static void AddColumnIfMissing(MigrationBuilder migrationBuilder, string table, string column, string definition)
        {
            migrationBuilder.Sql($@"
IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL
BEGIN
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};
END");
        }

        private static void CreateIndexIfMissing(MigrationBuilder migrationBuilder, string table, string indexName, string column)
        {
            migrationBuilder.Sql($@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'{indexName}'
      AND object_id = OBJECT_ID(N'dbo.{table}')
)
BEGIN
    CREATE INDEX [{indexName}] ON [dbo].[{table}] ([{column}]);
END");
        }

        private static void AddForeignKeyIfMissing(
            MigrationBuilder migrationBuilder,
            string table,
            string foreignKeyName,
            string column,
            string principalTable,
            string principalColumn)
        {
            migrationBuilder.Sql($@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'{foreignKeyName}'
      AND parent_object_id = OBJECT_ID(N'dbo.{table}')
)
BEGIN
    ALTER TABLE [dbo].[{table}] WITH CHECK
    ADD CONSTRAINT [{foreignKeyName}]
    FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([{principalColumn}]);

    ALTER TABLE [dbo].[{table}] CHECK CONSTRAINT [{foreignKeyName}];
END");
        }

        private static void DropForeignKeyIfExists(MigrationBuilder migrationBuilder, string table, string foreignKeyName)
        {
            migrationBuilder.Sql($@"
IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'{foreignKeyName}'
      AND parent_object_id = OBJECT_ID(N'dbo.{table}')
)
BEGIN
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{foreignKeyName}];
END");
        }

        private static void DropIndexIfExists(MigrationBuilder migrationBuilder, string table, string indexName)
        {
            migrationBuilder.Sql($@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'{indexName}'
      AND object_id = OBJECT_ID(N'dbo.{table}')
)
BEGIN
    DROP INDEX [{indexName}] ON [dbo].[{table}];
END");
        }

        private static void DropColumnIfExists(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($@"
IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NOT NULL
BEGIN
    DECLARE @constraintName sysname;

    SELECT @constraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.{table}')
      AND c.name = N'{column}';

    IF @constraintName IS NOT NULL
    BEGIN
        DECLARE @dropConstraintSql nvarchar(max) =
            N'ALTER TABLE [dbo].[{table}] DROP CONSTRAINT ' + QUOTENAME(@constraintName);
        EXEC sp_executesql @dropConstraintSql;
    END

    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END");
        }
    }
}
