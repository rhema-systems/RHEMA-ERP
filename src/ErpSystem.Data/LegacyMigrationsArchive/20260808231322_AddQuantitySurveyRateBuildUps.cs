using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260808231322_AddQuantitySurveyRateBuildUps")]
    /// <inheritdoc />
    public partial class AddQuantitySurveyRateBuildUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_QsRateLibraryRates_Source",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.AddColumn<Guid>(
                name: "RateBuildUpId",
                table: "QuantitySurveyRateLibraryRates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitRate",
                table: "QuantitySurveyRateLibraryRates",
                type: "decimal(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PreviousUnitRate",
                table: "QuantitySurveyRateLibraryRates",
                type: "decimal(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "QuantitySurveyRateBuildUps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateLibraryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildUpNumber = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    SourceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCodeSnapshot = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationProfileVersion = table.Column<int>(type: "int", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    MaterialSubtotal = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    DirectCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    AddOnCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    UnitRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    CalculationHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuditAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyRateBuildUps", x => x.Id);
                    table.CheckConstraint("CK_QsRateBuildUps_Amounts", "[MaterialSubtotal] >= 0 AND [DirectCost] > 0 AND [AddOnCost] >= 0 AND [UnitRate] > 0 AND [MaterialSubtotal] <= [DirectCost]");
                    table.CheckConstraint("CK_QsRateBuildUps_DecimalPlaces", "[DecimalPlaces] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_QsRateBuildUps_EvidencePair", "([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)");
                    table.CheckConstraint("CK_QsRateBuildUps_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUps_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUps_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUps_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUps_QuantitySurveyConfigurationDecisions_ConfigurationDecisionId",
                        column: x => x.ConfigurationDecisionId,
                        principalTable: "QuantitySurveyConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUps_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                        column: x => x.ConfigurationProfileId,
                        principalTable: "QuantitySurveyConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUps_QuantitySurveyRateLibraryItems_RateLibraryItemId",
                        column: x => x.RateLibraryItemId,
                        principalTable: "QuantitySurveyRateLibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUps_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyRateBuildUpLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateBuildUpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Component = table.Column<int>(type: "int", nullable: false),
                    CalculationMethod = table.Column<int>(type: "int", nullable: false),
                    PercentageBasis = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    SourceRateLibraryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceItemCodeSnapshot = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    SourceItemNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceUnitOfMeasureSnapshot = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    SourceRateVersion = table.Column<int>(type: "int", nullable: true),
                    SourceUnitRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    InputQuantity = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    InputPercentage = table.Column<decimal>(type: "decimal(9,4)", nullable: true),
                    InputFixedAmount = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    BasisAmount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    CalculatedAmount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyRateBuildUpLines", x => x.Id);
                    table.CheckConstraint("CK_QsRateBuildUpLines_Amounts", "[BasisAmount] >= 0 AND [CalculatedAmount] > 0");
                    table.CheckConstraint("CK_QsRateBuildUpLines_Component", "[Component] BETWEEN 0 AND 11");
                    table.CheckConstraint("CK_QsRateBuildUpLines_InputShape", "([CalculationMethod] = 0 AND [SourceRateLibraryItemId] IS NOT NULL AND [SourceRateId] IS NOT NULL AND [SourceItemCodeSnapshot] IS NOT NULL AND [SourceItemNameSnapshot] IS NOT NULL AND [SourceUnitOfMeasureSnapshot] IS NOT NULL AND [SourceRateVersion] > 0 AND [SourceUnitRate] > 0 AND [InputQuantity] > 0 AND [InputPercentage] IS NULL AND [InputFixedAmount] IS NULL AND [PercentageBasis] IS NULL) OR ([CalculationMethod] = 1 AND [SourceRateLibraryItemId] IS NULL AND [SourceRateId] IS NULL AND [SourceItemCodeSnapshot] IS NULL AND [SourceItemNameSnapshot] IS NULL AND [SourceUnitOfMeasureSnapshot] IS NULL AND [SourceRateVersion] IS NULL AND [SourceUnitRate] IS NULL AND [InputQuantity] IS NULL AND [InputPercentage] > 0 AND [InputFixedAmount] IS NULL AND [PercentageBasis] IS NOT NULL) OR ([CalculationMethod] = 2 AND [SourceRateLibraryItemId] IS NULL AND [SourceRateId] IS NULL AND [SourceItemCodeSnapshot] IS NULL AND [SourceItemNameSnapshot] IS NULL AND [SourceUnitOfMeasureSnapshot] IS NULL AND [SourceRateVersion] IS NULL AND [SourceUnitRate] IS NULL AND [InputQuantity] IS NULL AND [InputPercentage] IS NULL AND [InputFixedAmount] > 0 AND [PercentageBasis] IS NULL)");
                    table.CheckConstraint("CK_QsRateBuildUpLines_Method", "[CalculationMethod] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_QsRateBuildUpLines_PercentageBasis", "[PercentageBasis] IS NULL OR [PercentageBasis] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_QsRateBuildUpLines_Sequence", "[Sequence] > 0");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUpLines_QuantitySurveyRateBuildUps_RateBuildUpId",
                        column: x => x.RateBuildUpId,
                        principalTable: "QuantitySurveyRateBuildUps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUpLines_QuantitySurveyRateLibraryItems_SourceRateLibraryItemId",
                        column: x => x.SourceRateLibraryItemId,
                        principalTable: "QuantitySurveyRateLibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUpLines_QuantitySurveyRateLibraryRates_SourceRateId",
                        column: x => x.SourceRateId,
                        principalTable: "QuantitySurveyRateLibraryRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateBuildUpLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_RateBuildUpId",
                table: "QuantitySurveyRateLibraryRates",
                column: "RateBuildUpId",
                unique: true,
                filter: "[RateBuildUpId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsRateLibraryRates_BuildUpLineage",
                table: "QuantitySurveyRateLibraryRates",
                sql: "([SourceType] <> 12 AND [RateBuildUpId] IS NULL) OR ([SourceType] = 12 AND [RateBuildUpId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsRateLibraryRates_Source",
                table: "QuantitySurveyRateLibraryRates",
                sql: "[SourceType] IN (0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUpLines_RateBuildUpId",
                table: "QuantitySurveyRateBuildUpLines",
                column: "RateBuildUpId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUpLines_SourceRateId",
                table: "QuantitySurveyRateBuildUpLines",
                column: "SourceRateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUpLines_SourceRateLibraryItemId",
                table: "QuantitySurveyRateBuildUpLines",
                column: "SourceRateLibraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUpLines_TenantId_RateBuildUpId_Sequence",
                table: "QuantitySurveyRateBuildUpLines",
                columns: new[] { "TenantId", "RateBuildUpId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUpLines_TenantId_SourceRateId",
                table: "QuantitySurveyRateBuildUpLines",
                columns: new[] { "TenantId", "SourceRateId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUps_CentralDocumentRecordId",
                table: "QuantitySurveyRateBuildUps",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUps_CentralDocumentVersionId",
                table: "QuantitySurveyRateBuildUps",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUps_ConfigurationDecisionId",
                table: "QuantitySurveyRateBuildUps",
                column: "ConfigurationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUps_ConfigurationProfileId",
                table: "QuantitySurveyRateBuildUps",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUps_CurrencyId",
                table: "QuantitySurveyRateBuildUps",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUps_RateLibraryItemId",
                table: "QuantitySurveyRateBuildUps",
                column: "RateLibraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUps_TenantId_CalculationHash",
                table: "QuantitySurveyRateBuildUps",
                columns: new[] { "TenantId", "CalculationHash" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUps_TenantId_ClientRequestId",
                table: "QuantitySurveyRateBuildUps",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUps_TenantId_ConfigurationDecisionId_PreparedAt",
                table: "QuantitySurveyRateBuildUps",
                columns: new[] { "TenantId", "ConfigurationDecisionId", "PreparedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateBuildUps_TenantId_RateLibraryItemId_Version",
                table: "QuantitySurveyRateBuildUps",
                columns: new[] { "TenantId", "RateLibraryItemId", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyRateLibraryRates_QuantitySurveyRateBuildUps_RateBuildUpId",
                table: "QuantitySurveyRateLibraryRates",
                column: "RateBuildUpId",
                principalTable: "QuantitySurveyRateBuildUps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_QsRateBuildUps_Immutable]
                ON [dbo].[QuantitySurveyRateBuildUps]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Quantity Survey rate build-up snapshots are immutable.', 1;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_QsRateBuildUpLines_Immutable]
                ON [dbo].[QuantitySurveyRateBuildUpLines]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Quantity Survey rate build-up line snapshots are immutable.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsRateBuildUpLines_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsRateBuildUps_Immutable];");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyRateLibraryRates_QuantitySurveyRateBuildUps_RateBuildUpId",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropTable(
                name: "QuantitySurveyRateBuildUpLines");

            migrationBuilder.DropTable(
                name: "QuantitySurveyRateBuildUps");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyRateLibraryRates_RateBuildUpId",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsRateLibraryRates_BuildUpLineage",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsRateLibraryRates_Source",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropColumn(
                name: "RateBuildUpId",
                table: "QuantitySurveyRateLibraryRates");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitRate",
                table: "QuantitySurveyRateLibraryRates",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PreviousUnitRate",
                table: "QuantitySurveyRateLibraryRates",
                type: "decimal(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsRateLibraryRates_Source",
                table: "QuantitySurveyRateLibraryRates",
                sql: "[SourceType] IN (0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11)");
        }
    }
}
