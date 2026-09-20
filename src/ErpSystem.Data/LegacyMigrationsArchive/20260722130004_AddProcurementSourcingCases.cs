using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSourcingCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourcingCaseId",
                table: "Tenders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcingCaseId",
                table: "RequestForQuotations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcurementSourcingCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcingReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseSequence = table.Column<int>(type: "int", nullable: false),
                    CaseNumber = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SourcePlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePlanItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    SelectedMethod = table.Column<int>(type: "int", nullable: false),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    MethodRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodRuleCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    ThresholdRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThresholdRuleCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    AuthorityRouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityRouteReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovedExceptionRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExceptionApprovalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExceptionEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceControlFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    CaseFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSourcingCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCases_ProcurementPlanItems_SourcePlanItemId",
                        column: x => x.SourcePlanItemId,
                        principalTable: "ProcurementPlanItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCases_ProcurementPlans_SourcePlanId",
                        column: x => x.SourcePlanId,
                        principalTable: "ProcurementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCases_ProcurementPolicyExceptionRules_ApprovedExceptionRuleId",
                        column: x => x.ApprovedExceptionRuleId,
                        principalTable: "ProcurementPolicyExceptionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCases_ProcurementPolicyMethodRules_MethodRuleId",
                        column: x => x.MethodRuleId,
                        principalTable: "ProcurementPolicyMethodRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCases_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCases_ProcurementPolicyThresholdRules_ThresholdRuleId",
                        column: x => x.ThresholdRuleId,
                        principalTable: "ProcurementPolicyThresholdRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCases_ProcurementRequisitionAuthorityRoutes_AuthorityRouteId",
                        column: x => x.AuthorityRouteId,
                        principalTable: "ProcurementRequisitionAuthorityRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCases_ProcurementRequisitionSourcingReleases_SourcingReleaseId",
                        column: x => x.SourcingReleaseId,
                        principalTable: "ProcurementRequisitionSourcingReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCases_PurchaseRequisitions_PurchaseRequisitionId",
                        column: x => x.PurchaseRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSourcingCaseLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcingCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotNumber = table.Column<int>(type: "int", nullable: false),
                    LotCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSourcingCaseLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCaseLots_ProcurementSourcingCases_SourcingCaseId",
                        column: x => x.SourcingCaseId,
                        principalTable: "ProcurementSourcingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCaseLots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSourcingCaseSourceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcingCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestSequence = table.Column<int>(type: "int", nullable: false),
                    RequestReference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SourceType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LotIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceEntityReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegisteredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegisteredByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementSourcingCaseSourceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCaseSourceRequests_ProcurementSourcingCases_SourcingCaseId",
                        column: x => x.SourcingCaseId,
                        principalTable: "ProcurementSourcingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCaseSourceRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSourcingCaseLotItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcingCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequisitionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSourcingCaseLotItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCaseLotItems_ProcurementSourcingCaseLots_LotId",
                        column: x => x.LotId,
                        principalTable: "ProcurementSourcingCaseLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCaseLotItems_ProcurementSourcingCases_SourcingCaseId",
                        column: x => x.SourcingCaseId,
                        principalTable: "ProcurementSourcingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCaseLotItems_PurchaseRequisitionItems_PurchaseRequisitionItemId",
                        column: x => x.PurchaseRequisitionItemId,
                        principalTable: "PurchaseRequisitionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSourcingCaseLotItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_SourcingCaseId",
                table: "Tenders",
                column: "SourcingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotations_SourcingCaseId",
                table: "RequestForQuotations",
                column: "SourcingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseLotItems_LotId",
                table: "ProcurementSourcingCaseLotItems",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseLotItems_PurchaseRequisitionItemId",
                table: "ProcurementSourcingCaseLotItems",
                column: "PurchaseRequisitionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseLotItems_SourcingCaseId",
                table: "ProcurementSourcingCaseLotItems",
                column: "SourcingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseLotItems_TenantId_LotId_PurchaseRequisitionItemId",
                table: "ProcurementSourcingCaseLotItems",
                columns: new[] { "TenantId", "LotId", "PurchaseRequisitionItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseLotItems_TenantId_SourcingCaseId_PurchaseRequisitionItemId",
                table: "ProcurementSourcingCaseLotItems",
                columns: new[] { "TenantId", "SourcingCaseId", "PurchaseRequisitionItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseLots_SourcingCaseId",
                table: "ProcurementSourcingCaseLots",
                column: "SourcingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseLots_TenantId_SourcingCaseId_LotCode",
                table: "ProcurementSourcingCaseLots",
                columns: new[] { "TenantId", "SourcingCaseId", "LotCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseLots_TenantId_SourcingCaseId_LotNumber",
                table: "ProcurementSourcingCaseLots",
                columns: new[] { "TenantId", "SourcingCaseId", "LotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_ApprovedExceptionRuleId",
                table: "ProcurementSourcingCases",
                column: "ApprovedExceptionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_AuthorityRouteId",
                table: "ProcurementSourcingCases",
                column: "AuthorityRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_MethodRuleId",
                table: "ProcurementSourcingCases",
                column: "MethodRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_PolicySetId",
                table: "ProcurementSourcingCases",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_PurchaseRequisitionId",
                table: "ProcurementSourcingCases",
                column: "PurchaseRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_SourcePlanId",
                table: "ProcurementSourcingCases",
                column: "SourcePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_SourcePlanItemId",
                table: "ProcurementSourcingCases",
                column: "SourcePlanItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_SourcingReleaseId",
                table: "ProcurementSourcingCases",
                column: "SourcingReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_TenantId_CaseNumber",
                table: "ProcurementSourcingCases",
                columns: new[] { "TenantId", "CaseNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_TenantId_PurchaseRequisitionId_CaseSequence",
                table: "ProcurementSourcingCases",
                columns: new[] { "TenantId", "PurchaseRequisitionId", "CaseSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_TenantId_SourcingReleaseId",
                table: "ProcurementSourcingCases",
                columns: new[] { "TenantId", "SourcingReleaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_TenantId_Status",
                table: "ProcurementSourcingCases",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_ThresholdRuleId",
                table: "ProcurementSourcingCases",
                column: "ThresholdRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseSourceRequests_SourcingCaseId",
                table: "ProcurementSourcingCaseSourceRequests",
                column: "SourcingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseSourceRequests_TenantId_RequestReference",
                table: "ProcurementSourcingCaseSourceRequests",
                columns: new[] { "TenantId", "RequestReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseSourceRequests_TenantId_SourceType_SourceEntityId",
                table: "ProcurementSourcingCaseSourceRequests",
                columns: new[] { "TenantId", "SourceType", "SourceEntityId" },
                unique: true,
                filter: "[SourceEntityId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCaseSourceRequests_TenantId_SourcingCaseId_RequestSequence",
                table: "ProcurementSourcingCaseSourceRequests",
                columns: new[] { "TenantId", "SourcingCaseId", "RequestSequence" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestForQuotations_ProcurementSourcingCases_SourcingCaseId",
                table: "RequestForQuotations",
                column: "SourcingCaseId",
                principalTable: "ProcurementSourcingCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenders_ProcurementSourcingCases_SourcingCaseId",
                table: "Tenders",
                column: "SourcingCaseId",
                principalTable: "ProcurementSourcingCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("ALTER TABLE [ProcurementSourcingCases] ADD CONSTRAINT [CK_ProcurementSourcingCases_State] CHECK ([Status] BETWEEN 0 AND 3 AND [CaseSequence] > 0 AND [EstimatedValue] >= 0 AND LEN([SourceControlFingerprint]) = 64 AND LEN([CaseFingerprint]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1);");
            migrationBuilder.Sql("ALTER TABLE [ProcurementSourcingCaseLots] ADD CONSTRAINT [CK_ProcurementSourcingCaseLots_Value] CHECK ([LotNumber] > 0 AND [EstimatedValue] >= 0);");
            migrationBuilder.Sql("ALTER TABLE [ProcurementSourcingCaseSourceRequests] ADD CONSTRAINT [CK_ProcurementSourcingCaseSourceRequests_State] CHECK ([RequestSequence] > 0 AND [Status] BETWEEN 0 AND 2 AND ISJSON([LotIdsJson]) = 1);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX [UX_ProcurementSourcingCases_ActiveRequisition] ON [ProcurementSourcingCases] ([TenantId], [PurchaseRequisitionId]) WHERE [IsDeleted] = 0 AND [Status] IN (0, 1);");

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSourcingCases_Lifecycle]
                ON [dbo].[ProcurementSourcingCases]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                        THROW 51060, 'Procurement sourcing cases cannot be deleted.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[PurchaseRequisitions] pr ON pr.[Id] = i.[PurchaseRequisitionId]
                        LEFT JOIN [dbo].[ProcurementRequisitionSourcingReleases] sr ON sr.[Id] = i.[SourcingReleaseId]
                        LEFT JOIN [dbo].[ProcurementPlans] pp ON pp.[Id] = i.[SourcePlanId]
                        LEFT JOIN [dbo].[ProcurementPlanItems] pi ON pi.[Id] = i.[SourcePlanItemId]
                        LEFT JOIN [dbo].[ProcurementPolicySets] ps ON ps.[Id] = i.[PolicySetId]
                        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] mr ON mr.[Id] = i.[MethodRuleId]
                        LEFT JOIN [dbo].[ProcurementPolicyThresholdRules] tr ON tr.[Id] = i.[ThresholdRuleId]
                        LEFT JOIN [dbo].[ProcurementRequisitionAuthorityRoutes] ar ON ar.[Id] = i.[AuthorityRouteId]
                        LEFT JOIN [dbo].[ProcurementPolicyExceptionRules] er ON er.[Id] = i.[ApprovedExceptionRuleId]
                        WHERE i.[IsDeleted] = 1
                           OR pr.[Id] IS NULL OR pr.[TenantId] <> i.[TenantId] OR pr.[IsDeleted] = 1
                           OR pr.[Status] <> 'Approved' OR pr.[SourcePlanId] <> i.[SourcePlanId] OR pr.[SourcePlanItemId] <> i.[SourcePlanItemId]
                           OR pr.[ProcurementCategory] <> i.[Category] OR pr.[TotalAmount] <> i.[EstimatedValue]
                           OR UPPER(LTRIM(RTRIM(pr.[Currency]))) <> i.[CurrencyCode]
                           OR sr.[Id] IS NULL OR sr.[TenantId] <> i.[TenantId] OR sr.[IsDeleted] = 1
                           OR sr.[PurchaseRequisitionId] <> i.[PurchaseRequisitionId]
                           OR sr.[SourcePlanId] <> i.[SourcePlanId] OR sr.[SourcePlanItemId] <> i.[SourcePlanItemId]
                           OR sr.[AuthorityRouteId] <> i.[AuthorityRouteId] OR sr.[AuthorityRouteReference] <> i.[AuthorityRouteReference]
                           OR sr.[ControlFingerprint] <> i.[SourceControlFingerprint]
                           OR ISNULL(sr.[ApprovedExceptionRuleId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[ApprovedExceptionRuleId], '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(sr.[ExceptionApprovalReference], '') <> ISNULL(i.[ExceptionApprovalReference], '')
                           OR pp.[Id] IS NULL OR pp.[TenantId] <> i.[TenantId] OR pp.[IsDeleted] = 1
                           OR pi.[Id] IS NULL OR pi.[TenantId] <> i.[TenantId] OR pi.[IsDeleted] = 1 OR pi.[ProcurementPlanId] <> i.[SourcePlanId]
                           OR ps.[Id] IS NULL OR ps.[TenantId] <> i.[TenantId] OR ps.[IsDeleted] = 1
                           OR ps.[Code] <> i.[PolicyCode] OR ps.[Version] <> i.[PolicyVersion]
                           OR ps.[LifecycleStatus] <> 1 OR ps.[PublishedAt] IS NULL OR ps.[EffectiveFrom] > i.[CreatedAt]
                           OR (ps.[EffectiveTo] IS NOT NULL AND ps.[EffectiveTo] < i.[CreatedAt])
                           OR mr.[Id] IS NULL OR mr.[TenantId] <> i.[TenantId] OR mr.[IsDeleted] = 1
                           OR mr.[PolicySetId] <> i.[PolicySetId] OR mr.[RuleCode] <> i.[MethodRuleCode]
                           OR mr.[Method] <> i.[SelectedMethod] OR mr.[Category] <> i.[Category] OR mr.[IsAllowed] = 0 OR mr.[IsEnabled] = 0
                           OR mr.[EffectiveFrom] > i.[CreatedAt] OR (mr.[EffectiveTo] IS NOT NULL AND mr.[EffectiveTo] < i.[CreatedAt])
                           OR tr.[Id] IS NULL OR tr.[TenantId] <> i.[TenantId] OR tr.[IsDeleted] = 1
                           OR tr.[PolicySetId] <> i.[PolicySetId] OR tr.[RuleCode] <> i.[ThresholdRuleCode]
                           OR tr.[Method] <> i.[SelectedMethod] OR tr.[Category] <> i.[Category] OR tr.[CurrencyCode] <> i.[CurrencyCode] OR tr.[IsEnabled] = 0
                           OR tr.[EffectiveFrom] > i.[CreatedAt] OR (tr.[EffectiveTo] IS NOT NULL AND tr.[EffectiveTo] < i.[CreatedAt])
                           OR i.[EstimatedValue] < tr.[LowerBound] OR (i.[EstimatedValue] = tr.[LowerBound] AND tr.[LowerInclusive] = 0)
                           OR (tr.[UpperBound] IS NOT NULL AND (i.[EstimatedValue] > tr.[UpperBound] OR (i.[EstimatedValue] = tr.[UpperBound] AND tr.[UpperInclusive] = 0)))
                           OR ar.[Id] IS NULL OR ar.[TenantId] <> i.[TenantId] OR ar.[IsDeleted] = 1
                           OR ar.[PurchaseRequisitionId] <> i.[PurchaseRequisitionId] OR ar.[PolicySetId] <> i.[PolicySetId]
                           OR ar.[RouteReference] <> i.[AuthorityRouteReference] OR ar.[Amount] <> i.[EstimatedValue]
                           OR ar.[Category] <> i.[Category] OR ar.[CurrencyCode] <> i.[CurrencyCode]
                           OR (i.[ApprovedExceptionRuleId] IS NOT NULL AND
                               (er.[Id] IS NULL OR er.[TenantId] <> i.[TenantId] OR er.[IsDeleted] = 1
                                OR er.[PolicySetId] <> i.[PolicySetId]
                                OR ISNULL(pr.[ExceptionEvidenceReference], '') <> ISNULL(i.[ExceptionEvidenceReference], '')))
                    )
                        THROW 51061, 'Procurement sourcing-case tenant, release, policy, rule, threshold, or authority lineage is invalid.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE (d.[Id] IS NULL AND
                               (i.[Status] <> 0 OR i.[StartedAtUtc] IS NOT NULL OR i.[ClosedAtUtc] IS NOT NULL OR i.[ClosureReason] IS NOT NULL))
                           OR (d.[Id] IS NOT NULL AND
                               (d.[TenantId] <> i.[TenantId]
                                OR d.[PurchaseRequisitionId] <> i.[PurchaseRequisitionId]
                                OR d.[SourcingReleaseId] <> i.[SourcingReleaseId]
                                OR d.[CaseSequence] <> i.[CaseSequence] OR d.[CaseNumber] <> i.[CaseNumber]
                                OR d.[SourcePlanId] <> i.[SourcePlanId] OR d.[SourcePlanItemId] <> i.[SourcePlanItemId]
                                OR d.[Category] <> i.[Category] OR d.[SelectedMethod] <> i.[SelectedMethod]
                                OR d.[EstimatedValue] <> i.[EstimatedValue] OR d.[CurrencyCode] <> i.[CurrencyCode]
                                OR d.[PolicySetId] <> i.[PolicySetId] OR d.[PolicyCode] <> i.[PolicyCode] OR d.[PolicyVersion] <> i.[PolicyVersion]
                                OR d.[MethodRuleId] <> i.[MethodRuleId] OR d.[MethodRuleCode] <> i.[MethodRuleCode]
                                OR d.[ThresholdRuleId] <> i.[ThresholdRuleId] OR d.[ThresholdRuleCode] <> i.[ThresholdRuleCode]
                                OR d.[AuthorityRouteId] <> i.[AuthorityRouteId] OR d.[AuthorityRouteReference] <> i.[AuthorityRouteReference]
                                OR ISNULL(d.[ApprovedExceptionRuleId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[ApprovedExceptionRuleId], '00000000-0000-0000-0000-000000000000')
                                OR ISNULL(d.[ExceptionApprovalReference], '') <> ISNULL(i.[ExceptionApprovalReference], '')
                                OR ISNULL(d.[ExceptionEvidenceReference], '') <> ISNULL(i.[ExceptionEvidenceReference], '')
                                OR d.[Justification] <> i.[Justification] OR d.[CreatedByName] <> i.[CreatedByName]
                                OR d.[SourceControlFingerprint] <> i.[SourceControlFingerprint] OR d.[CaseFingerprint] <> i.[CaseFingerprint]
                                OR d.[SnapshotJson] <> i.[SnapshotJson] OR d.[IntegrityHash] <> i.[IntegrityHash]
                                OR d.[CreatedAt] <> i.[CreatedAt] OR ISNULL(d.[CreatedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[CreatedById], '00000000-0000-0000-0000-000000000000')
                                OR i.[IsDeleted] <> 0
                                OR NOT ((d.[Status] = 0 AND i.[Status] IN (1, 3)) OR (d.[Status] = 1 AND i.[Status] IN (2, 3)))
                                OR (i.[Status] = 1 AND (i.[StartedAtUtc] IS NULL OR i.[StartedByName] IS NULL OR i.[ClosedAtUtc] IS NOT NULL))
                                OR (i.[Status] IN (2, 3) AND (i.[ClosedAtUtc] IS NULL OR i.[ClosedByName] IS NULL OR LEN(LTRIM(RTRIM(i.[ClosureReason]))) < 5))))
                    )
                        THROW 51062, 'Procurement sourcing-case immutable fields or lifecycle transition are invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSourcingCaseLots_Immutable]
                ON [dbo].[ProcurementSourcingCaseLots]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51063, 'Procurement sourcing-case lots are immutable.', 1;
                    IF EXISTS
                    (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[ProcurementSourcingCases] sc ON sc.[Id] = i.[SourcingCaseId]
                        WHERE i.[IsDeleted] = 1 OR sc.[Id] IS NULL OR sc.[TenantId] <> i.[TenantId]
                           OR sc.[IsDeleted] = 1 OR sc.[Status] <> 0 OR sc.[CurrencyCode] <> i.[CurrencyCode]
                           OR i.[LotNumber] <= 0 OR i.[EstimatedValue] < 0 OR LEN(LTRIM(RTRIM(i.[Title]))) = 0
                    )
                        THROW 51064, 'Procurement sourcing-case lot tenant, state, currency, or value is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSourcingCaseLotItems_Immutable]
                ON [dbo].[ProcurementSourcingCaseLotItems]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51065, 'Procurement sourcing-case lot items are immutable.', 1;
                    IF EXISTS
                    (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[ProcurementSourcingCases] sc ON sc.[Id] = i.[SourcingCaseId]
                        LEFT JOIN [dbo].[ProcurementSourcingCaseLots] lot ON lot.[Id] = i.[LotId]
                        LEFT JOIN [dbo].[PurchaseRequisitionItems] pri ON pri.[Id] = i.[PurchaseRequisitionItemId]
                        WHERE i.[IsDeleted] = 1 OR sc.[Id] IS NULL OR sc.[TenantId] <> i.[TenantId] OR sc.[Status] <> 0
                           OR lot.[Id] IS NULL OR lot.[TenantId] <> i.[TenantId] OR lot.[SourcingCaseId] <> i.[SourcingCaseId] OR lot.[IsDeleted] = 1
                           OR pri.[Id] IS NULL OR pri.[TenantId] <> i.[TenantId] OR pri.[RequisitionId] <> sc.[PurchaseRequisitionId] OR pri.[IsDeleted] = 1
                    )
                        THROW 51066, 'Procurement sourcing-case lot-item tenant or requisition lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSourcingCaseSourceRequests_Lifecycle]
                ON [dbo].[ProcurementSourcingCaseSourceRequests]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                        THROW 51067, 'Procurement sourcing-case source requests cannot be deleted.', 1;
                    IF EXISTS
                    (
                        SELECT 1 FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        LEFT JOIN [dbo].[ProcurementSourcingCases] sc ON sc.[Id] = i.[SourcingCaseId]
                        WHERE i.[IsDeleted] = 1 OR sc.[Id] IS NULL OR sc.[TenantId] <> i.[TenantId] OR sc.[IsDeleted] = 1
                           OR i.[Method] <> sc.[SelectedMethod]
                           OR (i.[Method] = 0 AND i.[SourceType] <> 'RequestForQuotation')
                           OR (i.[Method] <> 0 AND i.[SourceType] <> 'Tender')
                           OR (d.[Id] IS NULL AND
                               NOT ((i.[Status] = 0 AND i.[SourceEntityId] IS NULL AND i.[RegisteredAtUtc] IS NULL)
                                    OR (i.[Status] = 1 AND i.[SourceEntityId] IS NOT NULL AND i.[SourceEntityReference] IS NOT NULL AND i.[RegisteredAtUtc] IS NOT NULL)))
                           OR (d.[Id] IS NOT NULL AND
                               (d.[TenantId] <> i.[TenantId] OR d.[SourcingCaseId] <> i.[SourcingCaseId]
                                OR d.[RequestSequence] <> i.[RequestSequence] OR d.[RequestReference] <> i.[RequestReference]
                                OR d.[SourceType] <> i.[SourceType] OR d.[Method] <> i.[Method]
                                OR d.[LotIdsJson] <> i.[LotIdsJson] OR d.[PlannedAtUtc] <> i.[PlannedAtUtc]
                                OR d.[Status] <> 0 OR i.[Status] NOT IN (1, 2)
                                OR (i.[Status] = 1 AND (i.[SourceEntityId] IS NULL OR i.[SourceEntityReference] IS NULL OR i.[RegisteredAtUtc] IS NULL))
                                OR (i.[Status] = 2 AND (i.[SourceEntityId] IS NOT NULL OR i.[RegisteredAtUtc] IS NOT NULL))))
                    )
                        THROW 51068, 'Procurement sourcing-case source-request tenant, method, or lifecycle is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_RequestForQuotations_SourcingReleaseGuard]
                ON [dbo].[RequestForQuotations]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        LEFT JOIN [dbo].[ProcurementRequisitionSourcingReleases] sr ON sr.[Id] = i.[SourcingReleaseId]
                        LEFT JOIN [dbo].[ProcurementSourcingCases] sc ON sc.[Id] = i.[SourcingCaseId]
                        LEFT JOIN [dbo].[PurchaseRequisitions] pr ON pr.[Id] = i.[SourcePurchaseRequisitionId]
                        LEFT JOIN [dbo].[ProcurementPolicySets] ps ON ps.[Id] = sc.[PolicySetId]
                        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] mr ON mr.[Id] = sc.[MethodRuleId]
                        LEFT JOIN [dbo].[ProcurementPolicyThresholdRules] tr ON tr.[Id] = sc.[ThresholdRuleId]
                        WHERE (d.[Id] IS NULL AND (i.[SourcePurchaseRequisitionId] IS NULL OR i.[SourcingReleaseId] IS NULL OR i.[SourcingCaseId] IS NULL))
                           OR ((i.[SourcePurchaseRequisitionId] IS NULL OR i.[SourcingReleaseId] IS NULL OR i.[SourcingCaseId] IS NULL)
                               AND NOT (d.[Id] IS NOT NULL AND d.[SourcePurchaseRequisitionId] IS NULL AND d.[SourcingReleaseId] IS NULL AND d.[SourcingCaseId] IS NULL))
                           OR (i.[SourcingCaseId] IS NOT NULL AND
                               (sr.[Id] IS NULL OR sr.[TenantId] <> i.[TenantId] OR sr.[IsDeleted] = 1 OR sr.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]
                                OR sc.[Id] IS NULL OR sc.[TenantId] <> i.[TenantId] OR sc.[IsDeleted] = 1 OR sc.[Status] NOT IN (0, 1)
                                OR sc.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId] OR sc.[SourcingReleaseId] <> i.[SourcingReleaseId]
                                OR sc.[SelectedMethod] <> 0 OR sc.[EstimatedValue] <> i.[EstimatedValue] OR sc.[CurrencyCode] <> UPPER(LTRIM(RTRIM(i.[Currency])))
                                OR sc.[SourceControlFingerprint] <> sr.[ControlFingerprint]
                                OR pr.[Id] IS NULL OR pr.[TenantId] <> i.[TenantId] OR pr.[IsDeleted] = 1 OR pr.[Status] <> 'Approved'
                                OR pr.[TotalAmount] <> sc.[EstimatedValue] OR pr.[ProcurementCategory] <> sc.[Category]
                                OR ps.[Id] IS NULL OR ps.[TenantId] <> i.[TenantId] OR ps.[IsDeleted] = 1 OR ps.[LifecycleStatus] <> 1
                                OR ps.[EffectiveFrom] > SYSUTCDATETIME() OR (ps.[EffectiveTo] IS NOT NULL AND ps.[EffectiveTo] < SYSUTCDATETIME())
                                OR mr.[Id] IS NULL OR mr.[IsDeleted] = 1 OR mr.[IsEnabled] = 0 OR mr.[Method] <> 0
                                OR tr.[Id] IS NULL OR tr.[IsDeleted] = 1 OR tr.[IsEnabled] = 0 OR tr.[Method] <> 0
                                OR NOT EXISTS (SELECT 1 FROM [dbo].[ProcurementSourcingCaseSourceRequests] x WHERE x.[SourcingCaseId] = sc.[Id] AND x.[TenantId] = i.[TenantId] AND x.[SourceType] = 'RequestForQuotation' AND x.[IsDeleted] = 0 AND ((x.[Status] = 0 AND x.[SourceEntityId] IS NULL) OR (x.[Status] = 1 AND x.[SourceEntityId] = i.[Id])))
                                OR EXISTS (SELECT 1 FROM [dbo].[PurchaseRequisitionItems] pri WHERE pri.[RequisitionId] = sc.[PurchaseRequisitionId] AND pri.[TenantId] = i.[TenantId] AND pri.[IsDeleted] = 0 AND NOT EXISTS (SELECT 1 FROM [dbo].[ProcurementSourcingCaseLotItems] li WHERE li.[SourcingCaseId] = sc.[Id] AND li.[PurchaseRequisitionItemId] = pri.[Id] AND li.[TenantId] = i.[TenantId] AND li.[IsDeleted] = 0))
                                OR EXISTS (SELECT 1 FROM [dbo].[ProcurementSourcingCaseLotItems] li LEFT JOIN [dbo].[PurchaseRequisitionItems] pri ON pri.[Id] = li.[PurchaseRequisitionItemId] WHERE li.[SourcingCaseId] = sc.[Id] AND li.[IsDeleted] = 0 AND (pri.[Id] IS NULL OR pri.[RequisitionId] <> sc.[PurchaseRequisitionId] OR pri.[IsDeleted] = 1))
                                OR EXISTS (SELECT 1 FROM [dbo].[ProcurementSourcingCaseLots] lot WHERE lot.[SourcingCaseId] = sc.[Id] AND lot.[IsDeleted] = 0 AND lot.[EstimatedValue] <> (SELECT COALESCE(SUM(pri.[LineTotal]), 0) FROM [dbo].[ProcurementSourcingCaseLotItems] li JOIN [dbo].[PurchaseRequisitionItems] pri ON pri.[Id] = li.[PurchaseRequisitionItemId] WHERE li.[LotId] = lot.[Id] AND li.[IsDeleted] = 0 AND pri.[IsDeleted] = 0))))
                           OR (d.[Id] IS NOT NULL AND d.[SourcingCaseId] IS NOT NULL AND
                               (d.[SourcePurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId] OR d.[SourcingReleaseId] <> i.[SourcingReleaseId] OR d.[SourcingCaseId] <> i.[SourcingCaseId]
                                OR d.[EstimatedValue] <> i.[EstimatedValue] OR d.[Currency] <> i.[Currency]))
                    )
                        THROW 51069, 'RFQ creation requires one current, policy-compliant, fully lotted sourcing case.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_Tenders_SourcingReleaseGuard]
                ON [dbo].[Tenders]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        LEFT JOIN [dbo].[ProcurementRequisitionSourcingReleases] sr ON sr.[Id] = i.[SourcingReleaseId]
                        LEFT JOIN [dbo].[ProcurementSourcingCases] sc ON sc.[Id] = i.[SourcingCaseId]
                        LEFT JOIN [dbo].[PurchaseRequisitions] pr ON pr.[Id] = i.[SourcePurchaseRequisitionId]
                        LEFT JOIN [dbo].[ProcurementPolicySets] ps ON ps.[Id] = sc.[PolicySetId]
                        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] mr ON mr.[Id] = sc.[MethodRuleId]
                        LEFT JOIN [dbo].[ProcurementPolicyThresholdRules] tr ON tr.[Id] = sc.[ThresholdRuleId]
                        WHERE (d.[Id] IS NULL AND (i.[SourcePurchaseRequisitionId] IS NULL OR i.[SourcingReleaseId] IS NULL OR i.[SourcingCaseId] IS NULL))
                           OR ((i.[SourcePurchaseRequisitionId] IS NULL OR i.[SourcingReleaseId] IS NULL OR i.[SourcingCaseId] IS NULL)
                               AND NOT (d.[Id] IS NOT NULL AND d.[SourcePurchaseRequisitionId] IS NULL AND d.[SourcingReleaseId] IS NULL AND d.[SourcingCaseId] IS NULL))
                           OR (i.[SourcingCaseId] IS NOT NULL AND
                               (sr.[Id] IS NULL OR sr.[TenantId] <> i.[TenantId] OR sr.[IsDeleted] = 1 OR sr.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]
                                OR sc.[Id] IS NULL OR sc.[TenantId] <> i.[TenantId] OR sc.[IsDeleted] = 1 OR sc.[Status] NOT IN (0, 1)
                                OR sc.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId] OR sc.[SourcingReleaseId] <> i.[SourcingReleaseId]
                                OR ((UPPER(LTRIM(RTRIM(i.[TenderType]))) = 'RFQ' AND sc.[SelectedMethod] <> 0) OR (UPPER(LTRIM(RTRIM(i.[TenderType]))) <> 'RFQ' AND sc.[SelectedMethod] = 0))
                                OR sc.[EstimatedValue] <> i.[EstimatedValue] OR sc.[CurrencyCode] <> UPPER(LTRIM(RTRIM(i.[Currency])))
                                OR sc.[SourceControlFingerprint] <> sr.[ControlFingerprint]
                                OR pr.[Id] IS NULL OR pr.[TenantId] <> i.[TenantId] OR pr.[IsDeleted] = 1 OR pr.[Status] <> 'Approved'
                                OR pr.[TotalAmount] <> sc.[EstimatedValue] OR pr.[ProcurementCategory] <> sc.[Category]
                                OR ps.[Id] IS NULL OR ps.[TenantId] <> i.[TenantId] OR ps.[IsDeleted] = 1 OR ps.[LifecycleStatus] <> 1
                                OR ps.[EffectiveFrom] > SYSUTCDATETIME() OR (ps.[EffectiveTo] IS NOT NULL AND ps.[EffectiveTo] < SYSUTCDATETIME())
                                OR mr.[Id] IS NULL OR mr.[IsDeleted] = 1 OR mr.[IsEnabled] = 0 OR mr.[Method] <> sc.[SelectedMethod]
                                OR tr.[Id] IS NULL OR tr.[IsDeleted] = 1 OR tr.[IsEnabled] = 0 OR tr.[Method] <> sc.[SelectedMethod]
                                OR NOT EXISTS (SELECT 1 FROM [dbo].[ProcurementSourcingCaseSourceRequests] x WHERE x.[SourcingCaseId] = sc.[Id] AND x.[TenantId] = i.[TenantId] AND x.[SourceType] = 'Tender' AND x.[IsDeleted] = 0 AND ((x.[Status] = 0 AND x.[SourceEntityId] IS NULL) OR (x.[Status] = 1 AND x.[SourceEntityId] = i.[Id])))
                                OR EXISTS (SELECT 1 FROM [dbo].[PurchaseRequisitionItems] pri WHERE pri.[RequisitionId] = sc.[PurchaseRequisitionId] AND pri.[TenantId] = i.[TenantId] AND pri.[IsDeleted] = 0 AND NOT EXISTS (SELECT 1 FROM [dbo].[ProcurementSourcingCaseLotItems] li WHERE li.[SourcingCaseId] = sc.[Id] AND li.[PurchaseRequisitionItemId] = pri.[Id] AND li.[TenantId] = i.[TenantId] AND li.[IsDeleted] = 0))
                                OR EXISTS (SELECT 1 FROM [dbo].[ProcurementSourcingCaseLotItems] li LEFT JOIN [dbo].[PurchaseRequisitionItems] pri ON pri.[Id] = li.[PurchaseRequisitionItemId] WHERE li.[SourcingCaseId] = sc.[Id] AND li.[IsDeleted] = 0 AND (pri.[Id] IS NULL OR pri.[RequisitionId] <> sc.[PurchaseRequisitionId] OR pri.[IsDeleted] = 1))
                                OR EXISTS (SELECT 1 FROM [dbo].[ProcurementSourcingCaseLots] lot WHERE lot.[SourcingCaseId] = sc.[Id] AND lot.[IsDeleted] = 0 AND lot.[EstimatedValue] <> (SELECT COALESCE(SUM(pri.[LineTotal]), 0) FROM [dbo].[ProcurementSourcingCaseLotItems] li JOIN [dbo].[PurchaseRequisitionItems] pri ON pri.[Id] = li.[PurchaseRequisitionItemId] WHERE li.[LotId] = lot.[Id] AND li.[IsDeleted] = 0 AND pri.[IsDeleted] = 0))))
                           OR (d.[Id] IS NOT NULL AND d.[SourcingCaseId] IS NOT NULL AND
                               (d.[SourcePurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId] OR d.[SourcingReleaseId] <> i.[SourcingReleaseId] OR d.[SourcingCaseId] <> i.[SourcingCaseId]
                                OR ISNULL(d.[EstimatedValue], -1) <> ISNULL(i.[EstimatedValue], -1) OR ISNULL(d.[Currency], '') <> ISNULL(i.[Currency], '') OR d.[TenderType] <> i.[TenderType]))
                    )
                        THROW 51070, 'Tender creation requires one current, policy-compliant, fully lotted sourcing case.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_RequestForQuotations_SourcingReleaseGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_Tenders_SourcingReleaseGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSourcingCases_Lifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSourcingCaseLots_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSourcingCaseLotItems_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSourcingCaseSourceRequests_Lifecycle];");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestForQuotations_ProcurementSourcingCases_SourcingCaseId",
                table: "RequestForQuotations");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenders_ProcurementSourcingCases_SourcingCaseId",
                table: "Tenders");

            migrationBuilder.DropTable(
                name: "ProcurementSourcingCaseLotItems");

            migrationBuilder.DropTable(
                name: "ProcurementSourcingCaseSourceRequests");

            migrationBuilder.DropTable(
                name: "ProcurementSourcingCaseLots");

            migrationBuilder.DropTable(
                name: "ProcurementSourcingCases");

            migrationBuilder.DropIndex(
                name: "IX_Tenders_SourcingCaseId",
                table: "Tenders");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotations_SourcingCaseId",
                table: "RequestForQuotations");

            migrationBuilder.DropColumn(
                name: "SourcingCaseId",
                table: "Tenders");

            migrationBuilder.DropColumn(
                name: "SourcingCaseId",
                table: "RequestForQuotations");

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_RequestForQuotations_SourcingReleaseGuard]
                ON [dbo].[RequestForQuotations]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1 FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        LEFT JOIN [dbo].[ProcurementRequisitionSourcingReleases] sr ON sr.[Id] = i.[SourcingReleaseId]
                        WHERE (i.[SourcePurchaseRequisitionId] IS NULL AND i.[SourcingReleaseId] IS NOT NULL)
                           OR (i.[SourcePurchaseRequisitionId] IS NOT NULL AND i.[SourcingReleaseId] IS NULL)
                           OR (i.[SourcingReleaseId] IS NOT NULL AND (sr.[Id] IS NULL OR sr.[TenantId] <> i.[TenantId] OR sr.[IsDeleted] = 1 OR sr.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]))
                           OR (d.[Id] IS NOT NULL AND (d.[SourcePurchaseRequisitionId] IS NOT NULL OR d.[SourcingReleaseId] IS NOT NULL) AND
                               (ISNULL(d.[SourcePurchaseRequisitionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcePurchaseRequisitionId], '00000000-0000-0000-0000-000000000000')
                                OR ISNULL(d.[SourcingReleaseId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcingReleaseId], '00000000-0000-0000-0000-000000000000')))
                    ) THROW 51042, 'RFQ sourcing requires one immutable same-tenant requisition release.', 1;
                END
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_Tenders_SourcingReleaseGuard]
                ON [dbo].[Tenders]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1 FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        LEFT JOIN [dbo].[ProcurementRequisitionSourcingReleases] sr ON sr.[Id] = i.[SourcingReleaseId]
                        WHERE (i.[SourcePurchaseRequisitionId] IS NULL AND i.[SourcingReleaseId] IS NOT NULL)
                           OR (i.[SourcePurchaseRequisitionId] IS NOT NULL AND i.[SourcingReleaseId] IS NULL)
                           OR (i.[SourcingReleaseId] IS NOT NULL AND (sr.[Id] IS NULL OR sr.[TenantId] <> i.[TenantId] OR sr.[IsDeleted] = 1 OR sr.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]))
                           OR (d.[Id] IS NOT NULL AND (d.[SourcePurchaseRequisitionId] IS NOT NULL OR d.[SourcingReleaseId] IS NOT NULL) AND
                               (ISNULL(d.[SourcePurchaseRequisitionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcePurchaseRequisitionId], '00000000-0000-0000-0000-000000000000')
                                OR ISNULL(d.[SourcingReleaseId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcingReleaseId], '00000000-0000-0000-0000-000000000000')))
                    ) THROW 51042, 'Tender sourcing requires one immutable same-tenant requisition release.', 1;
                END
                """);
        }
    }
}
