using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261004031726_ConfigureCrmOpportunityStagesAndHistory")]
public sealed class ConfigureCrmOpportunityStagesAndHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OpportunityStageDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                IsClosed = table.Column<bool>(type: "bit", nullable: false),
                IsWon = table.Column<bool>(type: "bit", nullable: false),
                IsLost = table.Column<bool>(type: "bit", nullable: false),
                DefaultProbability = table.Column<int>(type: "int", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OpportunityStageDefinitions", x => x.Id);
                table.CheckConstraint(
                    "CK_OpportunityStageDefinition_Outcome",
                    "NOT ([IsWon] = 1 AND [IsLost] = 1) AND ([IsWon] = 0 OR [IsClosed] = 1) AND ([IsLost] = 0 OR [IsClosed] = 1)");
                table.ForeignKey(
                    name: "FK_OpportunityStageDefinitions_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "StageDefinitionId",
            table: "Opportunities",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "OpportunityStageHistories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StageDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EnteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                AmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CurrencySnapshot = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                ProbabilitySnapshot = table.Column<int>(type: "int", nullable: false),
                IsLegacySnapshot = table.Column<bool>(type: "bit", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OpportunityStageHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_OpportunityStageHistories_Opportunities_OpportunityId",
                    column: x => x.OpportunityId,
                    principalTable: "Opportunities",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OpportunityStageHistories_OpportunityStageDefinitions_StageDefinitionId",
                    column: x => x.StageDefinitionId,
                    principalTable: "OpportunityStageDefinitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OpportunityStageHistories_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OpportunityStageDefinitions_TenantId_Code",
            table: "OpportunityStageDefinitions",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OpportunityStageDefinitions_TenantId_SortOrder",
            table: "OpportunityStageDefinitions",
            columns: new[] { "TenantId", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_OpportunityStageHistories_OpportunityId",
            table: "OpportunityStageHistories",
            column: "OpportunityId");

        migrationBuilder.CreateIndex(
            name: "IX_OpportunityStageHistories_StageDefinitionId",
            table: "OpportunityStageHistories",
            column: "StageDefinitionId");

        migrationBuilder.CreateIndex(
            name: "IX_OpportunityStageHistories_TenantId_EnteredAt_StageDefinitionId",
            table: "OpportunityStageHistories",
            columns: new[] { "TenantId", "EnteredAt", "StageDefinitionId" });

        migrationBuilder.CreateIndex(
            name: "IX_OpportunityStageHistories_TenantId_OpportunityId_EnteredAt",
            table: "OpportunityStageHistories",
            columns: new[] { "TenantId", "OpportunityId", "EnteredAt" });

        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Opportunities]') AND name = N'IX_Opportunities_TenantId')
                DROP INDEX [IX_Opportunities_TenantId] ON [dbo].[Opportunities];
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Opportunities_StageDefinitionId",
            table: "Opportunities",
            column: "StageDefinitionId");

        migrationBuilder.CreateIndex(
            name: "IX_Opportunities_TenantId_ExpectedCloseDate_IsDeleted",
            table: "Opportunities",
            columns: new[] { "TenantId", "ExpectedCloseDate", "IsDeleted" });

        migrationBuilder.CreateIndex(
            name: "IX_Opportunities_TenantId_StageDefinitionId_IsDeleted",
            table: "Opportunities",
            columns: new[] { "TenantId", "StageDefinitionId", "IsDeleted" });

        migrationBuilder.AddForeignKey(
            name: "FK_Opportunities_OpportunityStageDefinitions_StageDefinitionId",
            table: "Opportunities",
            column: "StageDefinitionId",
            principalTable: "OpportunityStageDefinitions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        // This is initial tenant configuration, not runtime reporting logic. Tenants
        // can rename, reorder and deactivate these stages through the CRM API.
        migrationBuilder.Sql("""
            INSERT INTO dbo.OpportunityStageDefinitions
                (Id, TenantId, Code, Name, SortOrder, IsActive, IsClosed, IsWon, IsLost, DefaultProbability,
                 CreatedAt, CreatedBy, IsDeleted)
            SELECT NEWID(), tenant.Id, seed.Code, seed.Name, seed.SortOrder, 1, seed.IsClosed, seed.IsWon, seed.IsLost,
                   seed.DefaultProbability, SYSUTCDATETIME(), 'crm-stage-migration', 0
            FROM dbo.Tenants tenant
            CROSS JOIN (VALUES
                ('ENQUIRY',     'Enquiry',      10, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), 10),
                ('QUALIFIED',   'Qualified',    20, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), 25),
                ('PROPOSAL',    'Proposal',     30, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), 50),
                ('NEGOTIATION', 'Negotiation',  40, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), 75),
                ('WON',         'Won',          50, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), 100),
                ('LOST',        'Lost',         60, CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), 0)
            ) seed(Code, Name, SortOrder, IsClosed, IsWon, IsLost, DefaultProbability)
            WHERE NOT EXISTS (
                SELECT 1
                FROM dbo.OpportunityStageDefinitions existing
                WHERE existing.TenantId = tenant.Id AND existing.Code = seed.Code
            );

            ;WITH UnknownStages AS (
                SELECT opportunity.TenantId,
                       LTRIM(RTRIM(opportunity.Stage)) AS StageName,
                       ROW_NUMBER() OVER (
                           PARTITION BY opportunity.TenantId
                           ORDER BY LTRIM(RTRIM(opportunity.Stage))) AS StageOrder
                FROM dbo.Opportunities opportunity
                WHERE opportunity.IsDeleted = 0
                  AND NULLIF(LTRIM(RTRIM(opportunity.Stage)), '') IS NOT NULL
                  AND UPPER(LTRIM(RTRIM(opportunity.Stage))) NOT IN
                      ('PROSPECTING', 'ENQUIRY', 'QUALIFICATION', 'QUALIFIED', 'PROPOSAL',
                       'NEGOTIATION', 'CLOSED WON', 'WON', 'CLOSED LOST', 'LOST')
                GROUP BY opportunity.TenantId, LTRIM(RTRIM(opportunity.Stage))
            )
            INSERT INTO dbo.OpportunityStageDefinitions
                (Id, TenantId, Code, Name, SortOrder, IsActive, IsClosed, IsWon, IsLost, DefaultProbability,
                 CreatedAt, CreatedBy, IsDeleted)
            SELECT NEWID(), unknownStage.TenantId,
                   'LEGACY_' + LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', unknownStage.StageName), 2), 40),
                   LEFT(unknownStage.StageName, 100), 1000 + unknownStage.StageOrder,
                   1, 0, 0, 0, NULL, SYSUTCDATETIME(), 'crm-stage-migration', 0
            FROM UnknownStages unknownStage
            WHERE NOT EXISTS (
                SELECT 1
                FROM dbo.OpportunityStageDefinitions existing
                WHERE existing.TenantId = unknownStage.TenantId
                  AND existing.Name = unknownStage.StageName
            );

            UPDATE opportunity
            SET opportunity.StageDefinitionId = stage.Id,
                opportunity.Stage = stage.Name,
                opportunity.ActualCloseDate = CASE
                    WHEN stage.IsClosed = 1
                        THEN COALESCE(opportunity.ActualCloseDate, opportunity.UpdatedAt, opportunity.CreatedAt, SYSUTCDATETIME())
                    ELSE opportunity.ActualCloseDate
                END
            FROM dbo.Opportunities opportunity
            INNER JOIN dbo.OpportunityStageDefinitions stage
                ON stage.TenantId = opportunity.TenantId
               AND stage.Code = CASE UPPER(LTRIM(RTRIM(opportunity.Stage)))
                    WHEN 'PROSPECTING' THEN 'ENQUIRY'
                    WHEN 'ENQUIRY' THEN 'ENQUIRY'
                    WHEN 'QUALIFICATION' THEN 'QUALIFIED'
                    WHEN 'QUALIFIED' THEN 'QUALIFIED'
                    WHEN 'PROPOSAL' THEN 'PROPOSAL'
                    WHEN 'NEGOTIATION' THEN 'NEGOTIATION'
                    WHEN 'CLOSED WON' THEN 'WON'
                    WHEN 'WON' THEN 'WON'
                    WHEN 'CLOSED LOST' THEN 'LOST'
                    WHEN 'LOST' THEN 'LOST'
                    ELSE 'LEGACY_' + LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', LTRIM(RTRIM(opportunity.Stage))), 2), 40)
               END
            WHERE opportunity.StageDefinitionId IS NULL;

            INSERT INTO dbo.OpportunityStageHistories
                (Id, TenantId, OpportunityId, StageDefinitionId, EnteredAt, AmountSnapshot, CurrencySnapshot,
                 ProbabilitySnapshot, IsLegacySnapshot, CreatedAt, CreatedBy, IsDeleted)
            SELECT NEWID(), opportunity.TenantId, opportunity.Id, opportunity.StageDefinitionId,
                   opportunity.CreatedAt, opportunity.Amount, NULLIF(UPPER(LTRIM(RTRIM(opportunity.Currency))), ''),
                   CASE WHEN opportunity.Probability < 0 THEN 0
                        WHEN opportunity.Probability > 100 THEN 100
                        ELSE opportunity.Probability END,
                   1, SYSUTCDATETIME(), 'crm-stage-migration', 0
            FROM dbo.Opportunities opportunity
            WHERE opportunity.StageDefinitionId IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.OpportunityStageHistories history
                  WHERE history.TenantId = opportunity.TenantId
                    AND history.OpportunityId = opportunity.Id
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OpportunityStageHistories");

        migrationBuilder.DropForeignKey(
            name: "FK_Opportunities_OpportunityStageDefinitions_StageDefinitionId",
            table: "Opportunities");

        migrationBuilder.DropIndex(name: "IX_Opportunities_StageDefinitionId", table: "Opportunities");
        migrationBuilder.DropIndex(name: "IX_Opportunities_TenantId_ExpectedCloseDate_IsDeleted", table: "Opportunities");
        migrationBuilder.DropIndex(name: "IX_Opportunities_TenantId_StageDefinitionId_IsDeleted", table: "Opportunities");

        migrationBuilder.CreateIndex(
            name: "IX_Opportunities_TenantId",
            table: "Opportunities",
            column: "TenantId");

        migrationBuilder.DropColumn(name: "StageDefinitionId", table: "Opportunities");
        migrationBuilder.DropTable(name: "OpportunityStageDefinitions");
    }
}
