using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the code-owned route certification registry, immutable line-specific dimension evidence,
/// and effective-dated account-rule versions used by Finance source-document adapters.
/// </summary>
// Normal Debug builds omit generated migration designers. Inline discovery metadata follows the
// repository's recent Finance migration pattern and keeps startup/database-update discovery safe.
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830193635_AddFinanceDimensionSourceInfrastructure")]
public partial class AddFinanceDimensionSourceInfrastructure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FinanceDimensionAccountRules_TenantId_AccountId_FinanceDimensionDefinitionId_SourceModule_SourceDocumentType_PostingAction",
            table: "FinanceDimensionAccountRules");

        migrationBuilder.AddColumn<string>(
            name: "ContractVersion",
            table: "FinanceDimensionAccountRules",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "IsEvidenceLocked",
            table: "FinanceDimensionAccountRules",
            type: "bit",
            nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<int>(
            name: "RouteId",
            table: "FinanceDimensionAccountRules",
            type: "int",
            nullable: true);
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "FinanceDimensionAccountRules",
            type: "rowversion",
            rowVersion: true,
            nullable: false);
        migrationBuilder.AddColumn<Guid>(
            name: "RuleFamilyId",
            table: "FinanceDimensionAccountRules",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty);
        migrationBuilder.AddColumn<int>(
            name: "RuleVersion",
            table: "FinanceDimensionAccountRules",
            type: "int",
            nullable: false,
            defaultValue: 1);
        migrationBuilder.AddColumn<string>(
            name: "SourceRoute",
            table: "FinanceDimensionAccountRules",
            type: "nvarchar(150)",
            maxLength: 150,
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "SupersedesRuleId",
            table: "FinanceDimensionAccountRules",
            type: "uniqueidentifier",
            nullable: true);

        // Add nullable snapshot metadata first so every legacy row can be populated explicitly.
        // The subsequent AlterColumn calls enforce the final non-null model without inventing a
        // historical name where current master data is unavailable.
        migrationBuilder.AddColumn<string>(
            name: "DimensionNameSnapshot",
            table: "FinanceDimensionSetItems",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "HistoricalNameReconstructed",
            table: "FinanceDimensionSetItems",
            type: "bit",
            nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<DateTime>(
            name: "SnapshotCapturedAt",
            table: "FinanceDimensionSetItems",
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "SnapshotQuality",
            table: "FinanceDimensionSetItems",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "SnapshotSource",
            table: "FinanceDimensionSetItems",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "FinanceDimensionRouteCertifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RouteId = table.Column<int>(type: "int", nullable: false),
                ProducerModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SourceRoute = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ContractVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                State = table.Column<int>(type: "int", nullable: false),
                EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                table.PrimaryKey("PK_FinanceDimensionRouteCertifications", x => x.Id);
                table.ForeignKey(
                    name: "FK_FinanceDimensionRouteCertifications_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint(
                    name: "CK_FinanceDimensionRouteCertifications_State",
                    sql: "[State] IN (0,1,2)");
            });

        migrationBuilder.CreateTable(
            name: "FinanceDimensionReadinessAssessments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RouteId = table.Column<int>(type: "int", nullable: false),
                ProducerModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SourceRoute = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ContractVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CurrentState = table.Column<int>(type: "int", nullable: false),
                TargetState = table.Column<int>(type: "int", nullable: false),
                BlockerCount = table.Column<int>(type: "int", nullable: false),
                BlockerResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                DataVersionWatermark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                AssessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                AssessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                table.PrimaryKey("PK_FinanceDimensionReadinessAssessments", x => x.Id);
                table.ForeignKey(
                    name: "FK_FinanceDimensionReadinessAssessments_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint(
                    name: "CK_FinanceDimensionReadinessAssessments_States",
                    sql: "[CurrentState] IN (0,1,2) AND [TargetState] IN (0,1,2)");
            });

        migrationBuilder.CreateTable(
            name: "FinanceDimensionSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CombinationHashSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DisplayValueSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                SnapshotSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SnapshotCapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                SnapshotQuality = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                HistoricalNameReconstructed = table.Column<bool>(type: "bit", nullable: false),
                RuleEvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                ProducerModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                SourceRoute = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ContractVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
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
                table.PrimaryKey("PK_FinanceDimensionSnapshots", x => x.Id);
                table.ForeignKey(
                    name: "FK_FinanceDimensionSnapshots_FinanceDimensionSets_FinanceDimensionSetId",
                    column: x => x.FinanceDimensionSetId,
                    principalTable: "FinanceDimensionSets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceDimensionSnapshots_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FinanceDimensionCertificationTransitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionRouteCertificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PreviousState = table.Column<int>(type: "int", nullable: false),
                NewState = table.Column<int>(type: "int", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TransitionedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReadinessAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReadinessEvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                IsEmergencyRollback = table.Column<bool>(type: "bit", nullable: false),
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
                table.PrimaryKey("PK_FinanceDimensionCertificationTransitions", x => x.Id);
                table.ForeignKey(
                    name: "FK_FinanceDimensionCertificationTransitions_FinanceDimensionReadinessAssessments_ReadinessAssessmentId",
                    column: x => x.ReadinessAssessmentId,
                    principalTable: "FinanceDimensionReadinessAssessments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceDimensionCertificationTransitions_FinanceDimensionRouteCertifications_FinanceDimensionRouteCertificationId",
                    column: x => x.FinanceDimensionRouteCertificationId,
                    principalTable: "FinanceDimensionRouteCertifications",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceDimensionCertificationTransitions_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FinanceDimensionSnapshotItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DimensionCodeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                DimensionNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DimensionValueCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                DimensionValueNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                FinanceDimensionAccountRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RuleFamilyIdSnapshot = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RuleVersionSnapshot = table.Column<int>(type: "int", nullable: true),
                RuleTypeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                RuleEffectiveDateSnapshot = table.Column<DateTime>(type: "datetime2", nullable: true),
                RuleExpiryDateSnapshot = table.Column<DateTime>(type: "datetime2", nullable: true),
                SnapshotSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SnapshotCapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                SnapshotQuality = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                HistoricalNameReconstructed = table.Column<bool>(type: "bit", nullable: false),
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
                table.PrimaryKey("PK_FinanceDimensionSnapshotItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_FinanceDimensionSnapshotItems_FinanceDimensionAccountRules_FinanceDimensionAccountRuleId",
                    column: x => x.FinanceDimensionAccountRuleId,
                    principalTable: "FinanceDimensionAccountRules",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceDimensionSnapshotItems_FinanceDimensionDefinitions_FinanceDimensionDefinitionId",
                    column: x => x.FinanceDimensionDefinitionId,
                    principalTable: "FinanceDimensionDefinitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceDimensionSnapshotItems_FinanceDimensionSnapshots_FinanceDimensionSnapshotId",
                    column: x => x.FinanceDimensionSnapshotId,
                    principalTable: "FinanceDimensionSnapshots",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceDimensionSnapshotItems_FinanceDimensionValues_FinanceDimensionValueId",
                    column: x => x.FinanceDimensionValueId,
                    principalTable: "FinanceDimensionValues",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceDimensionSnapshotItems_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FinanceSourceDimensionAssignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RouteId = table.Column<int>(type: "int", nullable: false),
                ProducerModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SourceRoute = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ContractVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FinanceDimensionSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                table.PrimaryKey("PK_FinanceSourceDimensionAssignments", x => x.Id);
                table.ForeignKey(
                    name: "FK_FinanceSourceDimensionAssignments_FinanceDimensionSets_FinanceDimensionSetId",
                    column: x => x.FinanceDimensionSetId,
                    principalTable: "FinanceDimensionSets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceSourceDimensionAssignments_FinanceDimensionSnapshots_FinanceDimensionSnapshotId",
                    column: x => x.FinanceDimensionSnapshotId,
                    principalTable: "FinanceDimensionSnapshots",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceSourceDimensionAssignments_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "FinanceDimensionSnapshotId",
            table: "AccountTransactions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql(
            """
            DECLARE @CapturedAt datetime2 = SYSUTCDATETIME();

            UPDATE item
            SET DimensionNameSnapshot = COALESCE(NULLIF(definition.Name, ''), CONCAT('[Unavailable definition ', CONVERT(nvarchar(36), item.FinanceDimensionDefinitionId), ']')),
                DimensionValueNameSnapshot = COALESCE(NULLIF(value.Name, ''), CONCAT('[Unavailable value ', CONVERT(nvarchar(36), item.FinanceDimensionValueId), ']')),
                SnapshotSource = 'MigrationCurrentMasterData',
                SnapshotCapturedAt = @CapturedAt,
                SnapshotQuality = 'Reconstructed',
                HistoricalNameReconstructed = 1
            FROM FinanceDimensionSetItems item
            LEFT JOIN FinanceDimensionDefinitions definition
                ON definition.Id = item.FinanceDimensionDefinitionId
               AND definition.TenantId = item.TenantId
            LEFT JOIN FinanceDimensionValues value
                ON value.Id = item.FinanceDimensionValueId
               AND value.TenantId = item.TenantId;

            UPDATE FinanceDimensionAccountRules
            SET RuleFamilyId = Id,
                RuleVersion = 1
            WHERE RuleFamilyId = '00000000-0000-0000-0000-000000000000';

            UPDATE accountRule
            SET IsEvidenceLocked = 1
            FROM FinanceDimensionAccountRules accountRule
            WHERE EXISTS
            (
                SELECT 1
                FROM AccountTransactions transactionLine
                INNER JOIN FinanceDimensionSetItems setItem
                    ON setItem.FinanceDimensionSetId = transactionLine.FinanceDimensionSetId
                   AND setItem.FinanceDimensionDefinitionId = accountRule.FinanceDimensionDefinitionId
                   AND setItem.TenantId = accountRule.TenantId
                WHERE transactionLine.TenantId = accountRule.TenantId
                  AND transactionLine.AccountId = accountRule.AccountId
                  AND transactionLine.TransactionDate >= accountRule.EffectiveDate
                  AND (accountRule.ExpiryDate IS NULL OR transactionLine.TransactionDate < DATEADD(day, 1, CONVERT(date, accountRule.ExpiryDate)))
                  AND (accountRule.SourceModule IS NULL OR accountRule.SourceModule = transactionLine.SourceModule)
                  AND (accountRule.SourceDocumentType IS NULL OR accountRule.SourceDocumentType = transactionLine.SourceDocumentType)
                  AND transactionLine.IsDeleted = 0
            );

            DECLARE @SnapshotMap TABLE
            (
                TransactionId uniqueidentifier NOT NULL PRIMARY KEY,
                SnapshotId uniqueidentifier NOT NULL
            );

            MERGE FinanceDimensionSnapshots AS target
            USING
            (
                SELECT transactionLine.Id AS TransactionId,
                       NEWID() AS SnapshotId,
                       transactionLine.TenantId,
                       transactionLine.FinanceDimensionSetId,
                       dimensionSet.CombinationHash,
                       dimensionSet.DisplayValue,
                       transactionLine.SourceDocumentType
                FROM AccountTransactions transactionLine
                INNER JOIN FinanceDimensionSets dimensionSet
                    ON dimensionSet.Id = transactionLine.FinanceDimensionSetId
                   AND dimensionSet.TenantId = transactionLine.TenantId
                WHERE transactionLine.FinanceDimensionSetId IS NOT NULL
                  AND transactionLine.FinanceDimensionSnapshotId IS NULL
                  AND transactionLine.IsDeleted = 0
            ) AS source
            ON 1 = 0
            WHEN NOT MATCHED THEN
                INSERT
                (
                    Id, TenantId, FinanceDimensionSetId, CombinationHashSnapshot,
                    DisplayValueSnapshot, SnapshotSource, SnapshotCapturedAt,
                    SnapshotQuality, HistoricalNameReconstructed, RuleEvidenceHash,
                    ProducerModule, SourceRoute, SourceDocumentType, ContractVersion,
                    CreatedAt, CreatedBy, IsDeleted
                )
                VALUES
                (
                    source.SnapshotId, source.TenantId, source.FinanceDimensionSetId,
                    source.CombinationHash, source.DisplayValue, 'MigrationCurrentMasterData',
                    @CapturedAt, 'Reconstructed', 1, NULL, NULL, NULL,
                    source.SourceDocumentType, NULL, @CapturedAt,
                    'MigrationCurrentMasterData', 0
                )
            OUTPUT source.TransactionId, inserted.Id
                INTO @SnapshotMap(TransactionId, SnapshotId);

            UPDATE transactionLine
            SET FinanceDimensionSnapshotId = map.SnapshotId
            FROM AccountTransactions transactionLine
            INNER JOIN @SnapshotMap map ON map.TransactionId = transactionLine.Id;

            INSERT FinanceDimensionSnapshotItems
            (
                Id, TenantId, FinanceDimensionSnapshotId,
                FinanceDimensionDefinitionId, FinanceDimensionValueId,
                DimensionCodeSnapshot, DimensionNameSnapshot,
                DimensionValueCodeSnapshot, DimensionValueNameSnapshot,
                FinanceDimensionAccountRuleId, RuleFamilyIdSnapshot,
                RuleVersionSnapshot, RuleTypeSnapshot, RuleEffectiveDateSnapshot,
                RuleExpiryDateSnapshot, SnapshotSource, SnapshotCapturedAt,
                SnapshotQuality, HistoricalNameReconstructed,
                CreatedAt, CreatedBy, IsDeleted
            )
            SELECT NEWID(), transactionLine.TenantId, map.SnapshotId,
                   setItem.FinanceDimensionDefinitionId, setItem.FinanceDimensionValueId,
                   setItem.DimensionCodeSnapshot, setItem.DimensionNameSnapshot,
                   setItem.DimensionValueCodeSnapshot, setItem.DimensionValueNameSnapshot,
                   NULL, NULL, NULL, NULL, NULL, NULL,
                   'MigrationCurrentMasterData', @CapturedAt, 'Reconstructed', 1,
                   @CapturedAt, 'MigrationCurrentMasterData', 0
            FROM @SnapshotMap map
            INNER JOIN AccountTransactions transactionLine ON transactionLine.Id = map.TransactionId
            INNER JOIN FinanceDimensionSetItems setItem
                ON setItem.FinanceDimensionSetId = transactionLine.FinanceDimensionSetId
               AND setItem.TenantId = transactionLine.TenantId;

            INSERT FinanceDimensionRouteCertifications
            (
                Id, TenantId, RouteId, ProducerModule, SourceRoute, DocumentType,
                ContractVersion, State, EffectiveDate, CreatedAt, CreatedBy, IsDeleted
            )
            SELECT NEWID(), tenant.Id, route.RouteId, route.ProducerModule, route.SourceRoute,
                   route.DocumentType, route.ContractVersion, route.State,
                   @CapturedAt, @CapturedAt, 'DimensionInfrastructureMigration', 0
            FROM Tenants tenant
            CROSS JOIN
            (
                VALUES
                    (1,  'Finance', 'finance.gl.manual-journals',                'ManualJournalEntry', '1.1', 2),
                    (10, 'Finance', 'finance.ap.vendor-invoices.manual',         'VendorInvoice',      '1.0', 1),
                    (11, 'Finance', 'finance.ap.supplier-debit-notes.manual',    'SupplierDebitNote',  '1.0', 1),
                    (20, 'Finance', 'finance.ar.customer-invoices.manual',       'CustomerInvoice',    '1.0', 1),
                    (30, 'Sales',   'sales.credit-notes',                        'SalesCreditNote',     '1.1', 1)
            ) route(RouteId, ProducerModule, SourceRoute, DocumentType, ContractVersion, State)
            WHERE tenant.IsDeleted = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM FinanceDimensionRouteCertifications existing
                  WHERE existing.TenantId = tenant.Id
                    AND existing.RouteId = route.RouteId
              );
            """);

        // Upgrade existing databases in the same deployment that exposes the guarded readiness
        // endpoints. Route definitions remain compiled; only approved governance roles receive
        // the state-transition permission.
        migrationBuilder.Sql("""
            DECLARE @PermissionId uniqueidentifier =
                (SELECT TOP (1) [Id]
                 FROM [Permissions]
                 WHERE [Name] = N'Finance.Dimensions.Certification.Manage');

            IF @PermissionId IS NULL
            BEGIN
                SET @PermissionId = NEWID();
                INSERT INTO [Permissions]
                    ([Id], [Name], [DisplayName], [Description], [Category], [IsSystemPermission],
                     [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@PermissionId, N'Finance.Dimensions.Certification.Manage',
                     N'Manage Dimension Certification',
                     N'Assess and promote recognized Finance dimension routes using governed readiness evidence.',
                     N'Finance - General Ledger', 1, SYSUTCDATETIME(),
                     N'Finance dimension infrastructure migration', 0);
            END
            ELSE
            BEGIN
                UPDATE [Permissions]
                SET [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [DeletedBy] = NULL
                WHERE [Id] = @PermissionId;
            END;

            INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [GrantedAt], [GrantedBy])
            SELECT [role].[Id], @PermissionId, SYSUTCDATETIME(),
                   N'Finance dimension infrastructure migration'
            FROM [AspNetRoles] AS [role]
            WHERE [role].[Name] IN (N'Financial Controller', N'TenantAdmin', N'SuperAdmin')
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [RolePermissions] AS [existing]
                  WHERE [existing].[RoleId] = [role].[Id]
                    AND [existing].[PermissionId] = @PermissionId
              );
            """);

        migrationBuilder.AlterColumn<string>(
            name: "DimensionNameSnapshot",
            table: "FinanceDimensionSetItems",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100,
            oldNullable: true);
        migrationBuilder.AlterColumn<DateTime>(
            name: "SnapshotCapturedAt",
            table: "FinanceDimensionSetItems",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);
        migrationBuilder.AlterColumn<string>(
            name: "SnapshotQuality",
            table: "FinanceDimensionSetItems",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(30)",
            oldMaxLength: 30,
            oldNullable: true);
        migrationBuilder.AlterColumn<string>(
            name: "SnapshotSource",
            table: "FinanceDimensionSetItems",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AccountTransactions_FinanceDimensionSnapshotId",
            table: "AccountTransactions",
            column: "FinanceDimensionSnapshotId",
            unique: true,
            filter: "[FinanceDimensionSnapshotId] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionAccountRules_SupersedesRuleId",
            table: "FinanceDimensionAccountRules",
            column: "SupersedesRuleId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionAccountRules_TenantId_RuleFamilyId_RuleVersion",
            table: "FinanceDimensionAccountRules",
            columns: new[] { "TenantId", "RuleFamilyId", "RuleVersion" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionAccountRules_RouteScope",
            table: "FinanceDimensionAccountRules",
            columns: new[]
            {
                "TenantId", "AccountId", "FinanceDimensionDefinitionId", "RouteId",
                "SourceRoute", "ContractVersion", "SourceModule", "SourceDocumentType", "PostingAction"
            });
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionCertificationTransitions_FinanceDimensionRouteCertificationId",
            table: "FinanceDimensionCertificationTransitions",
            column: "FinanceDimensionRouteCertificationId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionCertificationTransitions_ReadinessAssessmentId",
            table: "FinanceDimensionCertificationTransitions",
            column: "ReadinessAssessmentId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionCertificationTransitions_TenantId_Certification_TransitionedAt",
            table: "FinanceDimensionCertificationTransitions",
            columns: new[] { "TenantId", "FinanceDimensionRouteCertificationId", "TransitionedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionReadinessAssessments_TenantId_RouteId_AssessedAt",
            table: "FinanceDimensionReadinessAssessments",
            columns: new[] { "TenantId", "RouteId", "AssessedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionRouteCertifications_TenantId_RouteId",
            table: "FinanceDimensionRouteCertifications",
            columns: new[] { "TenantId", "RouteId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionSnapshots_FinanceDimensionSetId",
            table: "FinanceDimensionSnapshots",
            column: "FinanceDimensionSetId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionSnapshots_TenantId_FinanceDimensionSetId",
            table: "FinanceDimensionSnapshots",
            columns: new[] { "TenantId", "FinanceDimensionSetId" });
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionSnapshotItems_FinanceDimensionAccountRuleId",
            table: "FinanceDimensionSnapshotItems",
            column: "FinanceDimensionAccountRuleId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionSnapshotItems_FinanceDimensionDefinitionId",
            table: "FinanceDimensionSnapshotItems",
            column: "FinanceDimensionDefinitionId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionSnapshotItems_FinanceDimensionSnapshotId",
            table: "FinanceDimensionSnapshotItems",
            column: "FinanceDimensionSnapshotId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionSnapshotItems_FinanceDimensionValueId",
            table: "FinanceDimensionSnapshotItems",
            column: "FinanceDimensionValueId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionSnapshotItems_TenantId_SnapshotId_DefinitionId",
            table: "FinanceDimensionSnapshotItems",
            columns: new[] { "TenantId", "FinanceDimensionSnapshotId", "FinanceDimensionDefinitionId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_FinanceSourceDimensionAssignments_FinanceDimensionSetId",
            table: "FinanceSourceDimensionAssignments",
            column: "FinanceDimensionSetId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceSourceDimensionAssignments_FinanceDimensionSnapshotId",
            table: "FinanceSourceDimensionAssignments",
            column: "FinanceDimensionSnapshotId",
            unique: true,
            filter: "[FinanceDimensionSnapshotId] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceSourceDimensionAssignments_SourceKey",
            table: "FinanceSourceDimensionAssignments",
            columns: new[] { "TenantId", "SourceDocumentType", "SourceDocumentId", "SourceLineId" },
            unique: true,
            filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceSourceDimensionAssignments_TenantId_RouteId_SourceDocumentId",
            table: "FinanceSourceDimensionAssignments",
            columns: new[] { "TenantId", "RouteId", "SourceDocumentId" });

        migrationBuilder.AddForeignKey(
            name: "FK_AccountTransactions_FinanceDimensionSnapshots_FinanceDimensionSnapshotId",
            table: "AccountTransactions",
            column: "FinanceDimensionSnapshotId",
            principalTable: "FinanceDimensionSnapshots",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_FinanceDimensionAccountRules_FinanceDimensionAccountRules_SupersedesRuleId",
            table: "FinanceDimensionAccountRules",
            column: "SupersedesRuleId",
            principalTable: "FinanceDimensionAccountRules",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE @PermissionId uniqueidentifier =
                (SELECT TOP (1) [Id]
                 FROM [Permissions]
                 WHERE [Name] = N'Finance.Dimensions.Certification.Manage');
            IF @PermissionId IS NOT NULL
            BEGIN
                DELETE FROM [RolePermissions]
                WHERE [PermissionId] = @PermissionId
                  AND [GrantedBy] = N'Finance dimension infrastructure migration';

                DELETE FROM [Permissions]
                WHERE [Id] = @PermissionId
                  AND [CreatedBy] = N'Finance dimension infrastructure migration'
                  AND NOT EXISTS
                      (SELECT 1 FROM [RolePermissions] WHERE [PermissionId] = @PermissionId);
            END;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_AccountTransactions_FinanceDimensionSnapshots_FinanceDimensionSnapshotId",
            table: "AccountTransactions");
        migrationBuilder.DropForeignKey(
            name: "FK_FinanceDimensionAccountRules_FinanceDimensionAccountRules_SupersedesRuleId",
            table: "FinanceDimensionAccountRules");

        migrationBuilder.DropTable(name: "FinanceDimensionCertificationTransitions");
        migrationBuilder.DropTable(name: "FinanceDimensionSnapshotItems");
        migrationBuilder.DropTable(name: "FinanceSourceDimensionAssignments");
        migrationBuilder.DropTable(name: "FinanceDimensionReadinessAssessments");
        migrationBuilder.DropTable(name: "FinanceDimensionRouteCertifications");

        migrationBuilder.DropIndex(
            name: "IX_AccountTransactions_FinanceDimensionSnapshotId",
            table: "AccountTransactions");
        migrationBuilder.DropColumn(
            name: "FinanceDimensionSnapshotId",
            table: "AccountTransactions");

        migrationBuilder.DropTable(name: "FinanceDimensionSnapshots");

        migrationBuilder.DropIndex(
            name: "IX_FinanceDimensionAccountRules_SupersedesRuleId",
            table: "FinanceDimensionAccountRules");
        migrationBuilder.DropIndex(
            name: "IX_FinanceDimensionAccountRules_TenantId_RuleFamilyId_RuleVersion",
            table: "FinanceDimensionAccountRules");
        migrationBuilder.DropIndex(
            name: "IX_FinanceDimensionAccountRules_RouteScope",
            table: "FinanceDimensionAccountRules");

        migrationBuilder.DropColumn(name: "ContractVersion", table: "FinanceDimensionAccountRules");
        migrationBuilder.DropColumn(name: "IsEvidenceLocked", table: "FinanceDimensionAccountRules");
        migrationBuilder.DropColumn(name: "RouteId", table: "FinanceDimensionAccountRules");
        migrationBuilder.DropColumn(name: "RowVersion", table: "FinanceDimensionAccountRules");
        migrationBuilder.DropColumn(name: "RuleFamilyId", table: "FinanceDimensionAccountRules");
        migrationBuilder.DropColumn(name: "RuleVersion", table: "FinanceDimensionAccountRules");
        migrationBuilder.DropColumn(name: "SourceRoute", table: "FinanceDimensionAccountRules");
        migrationBuilder.DropColumn(name: "SupersedesRuleId", table: "FinanceDimensionAccountRules");

        migrationBuilder.DropColumn(name: "DimensionNameSnapshot", table: "FinanceDimensionSetItems");
        migrationBuilder.DropColumn(name: "HistoricalNameReconstructed", table: "FinanceDimensionSetItems");
        migrationBuilder.DropColumn(name: "SnapshotCapturedAt", table: "FinanceDimensionSetItems");
        migrationBuilder.DropColumn(name: "SnapshotQuality", table: "FinanceDimensionSetItems");
        migrationBuilder.DropColumn(name: "SnapshotSource", table: "FinanceDimensionSetItems");

        migrationBuilder.CreateIndex(
            name: "IX_FinanceDimensionAccountRules_TenantId_AccountId_FinanceDimensionDefinitionId_SourceModule_SourceDocumentType_PostingAction",
            table: "FinanceDimensionAccountRules",
            columns: new[]
            {
                "TenantId", "AccountId", "FinanceDimensionDefinitionId",
                "SourceModule", "SourceDocumentType", "PostingAction"
            },
            unique: true,
            filter: "[SourceModule] IS NOT NULL AND [SourceDocumentType] IS NOT NULL AND [PostingAction] IS NOT NULL");
    }
}
