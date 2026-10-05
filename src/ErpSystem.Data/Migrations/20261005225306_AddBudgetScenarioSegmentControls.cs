using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetScenarioSegmentControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetReturns_TenantId_BudgetScenarioId_DistributionDimensionValueId",
                table: "BudgetReturns");

            migrationBuilder.DropIndex(
                name: "IX_BudgetReturns_TenantId_BudgetScenarioId_SegmentValueId",
                table: "BudgetReturns");

            migrationBuilder.CreateTable(
                name: "BudgetScenarioControlSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountSegmentStructureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetScenarioControlSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetScenarioControlSegments_AccountSegmentStructures_AccountSegmentStructureId",
                        column: x => x.AccountSegmentStructureId,
                        principalTable: "AccountSegmentStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetScenarioControlSegments_BudgetScenarios_BudgetScenarioId",
                        column: x => x.BudgetScenarioId,
                        principalTable: "BudgetScenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetScenarioControlSegments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [BudgetScenarioControlSegments]
                    ([Id], [BudgetScenarioId], [AccountSegmentStructureId], [DisplayOrder],
                     [CreatedAt], [IsDeleted], [TenantId])
                SELECT NEWID(), existing_scope.[BudgetScenarioId], existing_scope.[SegmentStructureId],
                       0, SYSUTCDATETIME(), 0, existing_scope.[TenantId]
                FROM
                (
                    SELECT DISTINCT budget_return.[TenantId], budget_return.[BudgetScenarioId],
                           segment_value.[SegmentStructureId]
                    FROM [BudgetReturns] AS budget_return
                    INNER JOIN [SegmentLookupValues] AS segment_value
                        ON segment_value.[Id] = budget_return.[SegmentValueId]
                        AND segment_value.[TenantId] = budget_return.[TenantId]
                        AND segment_value.[IsDeleted] = 0
                    WHERE budget_return.[IsDeleted] = 0
                        AND budget_return.[SegmentValueId] IS NOT NULL
                ) AS existing_scope;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReturns_TenantId_BudgetScenarioId_SegmentValueId_DistributionDimensionValueId",
                table: "BudgetReturns",
                columns: new[] { "TenantId", "BudgetScenarioId", "SegmentValueId", "DistributionDimensionValueId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND ([SegmentValueId] IS NOT NULL OR [DistributionDimensionValueId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarioControlSegments_AccountSegmentStructureId",
                table: "BudgetScenarioControlSegments",
                column: "AccountSegmentStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarioControlSegments_BudgetScenarioId",
                table: "BudgetScenarioControlSegments",
                column: "BudgetScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarioControlSegments_TenantId_BudgetScenarioId_AccountSegmentStructureId",
                table: "BudgetScenarioControlSegments",
                columns: new[] { "TenantId", "BudgetScenarioId", "AccountSegmentStructureId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetScenarioControlSegments");

            migrationBuilder.DropIndex(
                name: "IX_BudgetReturns_TenantId_BudgetScenarioId_SegmentValueId_DistributionDimensionValueId",
                table: "BudgetReturns");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReturns_TenantId_BudgetScenarioId_DistributionDimensionValueId",
                table: "BudgetReturns",
                columns: new[] { "TenantId", "BudgetScenarioId", "DistributionDimensionValueId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [DistributionDimensionValueId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReturns_TenantId_BudgetScenarioId_SegmentValueId",
                table: "BudgetReturns",
                columns: new[] { "TenantId", "BudgetScenarioId", "SegmentValueId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [SegmentValueId] IS NOT NULL");
        }
    }
}
