using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementRequisitionAuthorityRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementRequisitionAuthorityRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    RouteReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PolicyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    PolicyScopeType = table.Column<int>(type: "int", nullable: false),
                    SourceConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BasePolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    PolicyDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkflowVersion = table.Column<int>(type: "int", nullable: false),
                    WorkflowEntityTypeCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CapturedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementRequisitionAuthorityRoutes", x => x.Id);
                    table.CheckConstraint("CK_ProcurementRequisitionAuthorityRoutes_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_ProcurementRequisitionAuthorityRoutes_Attempt", "[AttemptNumber] > 0");
                    table.CheckConstraint("CK_ProcurementRequisitionAuthorityRoutes_Currency", "LEN([CurrencyCode]) = 3");
                    table.CheckConstraint("CK_ProcurementRequisitionAuthorityRoutes_Hash", "LEN([IntegrityHash]) = 64");
                    table.CheckConstraint("CK_ProcurementRequisitionAuthorityRoutes_Versions", "[PolicyVersion] > 0 AND [WorkflowVersion] > 0");
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRoutes_ProcurementConfigurationProfiles_SourceConfigurationProfileId",
                        column: x => x.SourceConfigurationProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRoutes_ProcurementPolicySets_BasePolicySetId",
                        column: x => x.BasePolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRoutes_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRoutes_PurchaseRequisitions_PurchaseRequisitionId",
                        column: x => x.PurchaseRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRoutes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRoutes_Users_CapturedById",
                        column: x => x.CapturedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRoutes_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementRequisitionAuthorityRouteSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityRouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    AuthorityRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RulePolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RulePolicyCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    RulePolicyVersion = table.Column<int>(type: "int", nullable: false),
                    RuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    SourceDecisionKey = table.Column<string>(type: "varchar(7)", unicode: false, maxLength: 7, nullable: false),
                    SourceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthorityRole = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CurrencyCode = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    LowerBound = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UpperBound = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    LowerInclusive = table.Column<bool>(type: "bit", nullable: false),
                    UpperInclusive = table.Column<bool>(type: "bit", nullable: false),
                    Quorum = table.Column<int>(type: "int", nullable: false),
                    IsObserver = table.Column<bool>(type: "bit", nullable: false),
                    EscalationAuthority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowStepName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkflowStepOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementRequisitionAuthorityRouteSteps", x => x.Id);
                    table.CheckConstraint("CK_ProcurementRequisitionAuthorityRouteSteps_Bounds", "[LowerBound] >= 0 AND ([UpperBound] IS NULL OR [UpperBound] >= [LowerBound])");
                    table.CheckConstraint("CK_ProcurementRequisitionAuthorityRouteSteps_Currency", "LEN([CurrencyCode]) = 3");
                    table.CheckConstraint("CK_ProcurementRequisitionAuthorityRouteSteps_Decision", "[SourceDecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_ProcurementRequisitionAuthorityRouteSteps_Quorum", "[Quorum] BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_ProcurementRequisitionAuthorityRouteSteps_Sequence", "[Sequence] BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRouteSteps_ProcurementPolicyAuthorityRules_AuthorityRuleId",
                        column: x => x.AuthorityRuleId,
                        principalTable: "ProcurementPolicyAuthorityRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRouteSteps_ProcurementPolicySets_RulePolicySetId",
                        column: x => x.RulePolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRouteSteps_ProcurementRequisitionAuthorityRoutes_AuthorityRouteId",
                        column: x => x.AuthorityRouteId,
                        principalTable: "ProcurementRequisitionAuthorityRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRouteSteps_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRouteSteps_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequisitionAuthorityRouteSteps_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_BasePolicySetId",
                table: "ProcurementRequisitionAuthorityRoutes",
                column: "BasePolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_CapturedById",
                table: "ProcurementRequisitionAuthorityRoutes",
                column: "CapturedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_PolicySetId",
                table: "ProcurementRequisitionAuthorityRoutes",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_PurchaseRequisitionId",
                table: "ProcurementRequisitionAuthorityRoutes",
                column: "PurchaseRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_SourceConfigurationProfileId",
                table: "ProcurementRequisitionAuthorityRoutes",
                column: "SourceConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_TenantId_CapturedAtUtc",
                table: "ProcurementRequisitionAuthorityRoutes",
                columns: new[] { "TenantId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_TenantId_PolicySetId_PolicyVersion",
                table: "ProcurementRequisitionAuthorityRoutes",
                columns: new[] { "TenantId", "PolicySetId", "PolicyVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_TenantId_PurchaseRequisitionId_AttemptNumber",
                table: "ProcurementRequisitionAuthorityRoutes",
                columns: new[] { "TenantId", "PurchaseRequisitionId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_TenantId_RouteReference",
                table: "ProcurementRequisitionAuthorityRoutes",
                columns: new[] { "TenantId", "RouteReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_TenantId_WorkflowDefinitionId_WorkflowVersion",
                table: "ProcurementRequisitionAuthorityRoutes",
                columns: new[] { "TenantId", "WorkflowDefinitionId", "WorkflowVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRoutes_WorkflowDefinitionId",
                table: "ProcurementRequisitionAuthorityRoutes",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRouteSteps_AuthorityRouteId",
                table: "ProcurementRequisitionAuthorityRouteSteps",
                column: "AuthorityRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRouteSteps_AuthorityRuleId",
                table: "ProcurementRequisitionAuthorityRouteSteps",
                column: "AuthorityRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRouteSteps_RulePolicySetId",
                table: "ProcurementRequisitionAuthorityRouteSteps",
                column: "RulePolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRouteSteps_TenantId_AuthorityRouteId_Sequence",
                table: "ProcurementRequisitionAuthorityRouteSteps",
                columns: new[] { "TenantId", "AuthorityRouteId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRouteSteps_TenantId_AuthorityRuleId",
                table: "ProcurementRequisitionAuthorityRouteSteps",
                columns: new[] { "TenantId", "AuthorityRuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRouteSteps_TenantId_WorkflowStepId",
                table: "ProcurementRequisitionAuthorityRouteSteps",
                columns: new[] { "TenantId", "WorkflowStepId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRouteSteps_WorkflowDefinitionId",
                table: "ProcurementRequisitionAuthorityRouteSteps",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequisitionAuthorityRouteSteps_WorkflowStepId",
                table: "ProcurementRequisitionAuthorityRouteSteps",
                column: "WorkflowStepId");

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementRequisitionAuthorityRoutes_NoMutation]
                ON [dbo].[ProcurementRequisitionAuthorityRoutes]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51030, 'Procurement requisition authority routes are immutable.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementRequisitionAuthorityRouteSteps_NoMutation]
                ON [dbo].[ProcurementRequisitionAuthorityRouteSteps]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51030, 'Procurement requisition authority route steps are immutable.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementRequisitionAuthorityRoutes_TenantGuard]
                ON [dbo].[ProcurementRequisitionAuthorityRoutes]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[PurchaseRequisitions] pr ON pr.[Id] = i.[PurchaseRequisitionId]
                        LEFT JOIN [dbo].[ProcurementPolicySets] ps ON ps.[Id] = i.[PolicySetId]
                        LEFT JOIN [dbo].[ProcurementConfigurationProfiles] cp ON cp.[Id] = i.[SourceConfigurationProfileId]
                        LEFT JOIN [dbo].[WorkflowDefinitions] wd ON wd.[Id] = i.[WorkflowDefinitionId]
                        LEFT JOIN [dbo].[ProcurementPolicySets] bp ON bp.[Id] = i.[BasePolicySetId]
                        WHERE pr.[Id] IS NULL OR pr.[TenantId] <> i.[TenantId]
                           OR ps.[Id] IS NULL OR ps.[TenantId] <> i.[TenantId]
                           OR ps.[PolicyKey] <> i.[PolicyKey]
                           OR ps.[Code] <> i.[PolicyCode]
                           OR ps.[Version] <> i.[PolicyVersion]
                           OR ps.[SourceConfigurationProfileId] <> i.[SourceConfigurationProfileId]
                           OR cp.[Id] IS NULL OR cp.[TenantId] <> i.[TenantId]
                           OR wd.[Id] IS NULL OR wd.[TenantId] <> i.[TenantId]
                           OR wd.[DefinitionKey] <> i.[WorkflowDefinitionKey]
                           OR wd.[Name] <> i.[WorkflowName]
                           OR wd.[Version] <> i.[WorkflowVersion]
                           OR (i.[BasePolicySetId] IS NOT NULL AND (bp.[Id] IS NULL OR bp.[TenantId] <> i.[TenantId]))
                    )
                    BEGIN
                        THROW 51031, 'Authority route tenant or source lineage is invalid.', 1;
                    END
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementRequisitionAuthorityRouteSteps_TenantGuard]
                ON [dbo].[ProcurementRequisitionAuthorityRouteSteps]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProcurementRequisitionAuthorityRoutes] r ON r.[Id] = i.[AuthorityRouteId]
                        LEFT JOIN [dbo].[ProcurementPolicyAuthorityRules] ar ON ar.[Id] = i.[AuthorityRuleId]
                        LEFT JOIN [dbo].[ProcurementPolicySets] ps ON ps.[Id] = i.[RulePolicySetId]
                        LEFT JOIN [dbo].[WorkflowDefinitions] wd ON wd.[Id] = i.[WorkflowDefinitionId]
                        LEFT JOIN [dbo].[WorkflowSteps] ws ON ws.[Id] = i.[WorkflowStepId]
                        WHERE r.[Id] IS NULL OR r.[TenantId] <> i.[TenantId]
                           OR r.[WorkflowDefinitionId] <> i.[WorkflowDefinitionId]
                           OR ar.[Id] IS NULL OR ar.[TenantId] <> i.[TenantId]
                           OR ar.[PolicySetId] <> i.[RulePolicySetId]
                           OR ar.[RuleCode] <> i.[RuleCode]
                           OR ar.[SourceDecisionKey] <> i.[SourceDecisionKey]
                           OR ISNULL(ar.[SourceRuleId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourceRuleId], '00000000-0000-0000-0000-000000000000')
                           OR ar.[AuthorityName] <> i.[AuthorityName]
                           OR ar.[AuthorityRole] <> i.[AuthorityRole]
                           OR ar.[CurrencyCode] <> i.[CurrencyCode]
                           OR ar.[Sequence] <> i.[Sequence]
                           OR ar.[LowerBound] <> i.[LowerBound]
                           OR ISNULL(ar.[UpperBound], -1) <> ISNULL(i.[UpperBound], -1)
                           OR ar.[LowerInclusive] <> i.[LowerInclusive]
                           OR ar.[UpperInclusive] <> i.[UpperInclusive]
                           OR ar.[Quorum] <> i.[Quorum]
                           OR ar.[IsObserver] <> i.[IsObserver]
                           OR ISNULL(ar.[EscalationAuthority], '') <> ISNULL(i.[EscalationAuthority], '')
                           OR ps.[Id] IS NULL OR ps.[TenantId] <> i.[TenantId]
                           OR ps.[Code] <> i.[RulePolicyCode]
                           OR ps.[Version] <> i.[RulePolicyVersion]
                           OR wd.[Id] IS NULL OR wd.[TenantId] <> i.[TenantId]
                           OR ws.[Id] IS NULL OR ws.[TenantId] <> i.[TenantId]
                           OR ws.[WorkflowDefinitionId] <> i.[WorkflowDefinitionId]
                           OR ws.[Name] <> i.[WorkflowStepName]
                           OR ws.[Order] <> i.[WorkflowStepOrder]
                    )
                    BEGIN
                        THROW 51031, 'Authority route step tenant or source lineage is invalid.', 1;
                    END
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementRequisitionAuthorityRouteSteps");

            migrationBuilder.DropTable(
                name: "ProcurementRequisitionAuthorityRoutes");
        }
    }
}
