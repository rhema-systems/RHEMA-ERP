using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSpecificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementSpecificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FunctionalAndPerformanceRequirements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessAndMaterialsRequirements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DimensionsAndMarkingRequirements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TestingAndInspectionRequirements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicableStandards = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Deliverables = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AcceptanceCriteria = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersedesTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSpecificationTemplates", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSpecificationTemplates_EffectivePeriod", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc]");
                    table.CheckConstraint("CK_ProcurementSpecificationTemplates_Kind", "[Kind] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_ProcurementSpecificationTemplates_Revision", "[RevisionNumber] >= 1");
                    table.CheckConstraint("CK_ProcurementSpecificationTemplates_Status", "[Status] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_ProcurementSpecificationTemplates_Version", "[Version] >= 1");
                    table.ForeignKey(
                        name: "FK_ProcurementSpecificationTemplates_ProcurementSpecificationTemplates_SupersedesTemplateId",
                        column: x => x.SupersedesTemplateId,
                        principalTable: "ProcurementSpecificationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSpecificationTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSpecificationTemplates_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSpecificationTemplates_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSpecificationTemplates_SupersedesTemplateId",
                table: "ProcurementSpecificationTemplates",
                column: "SupersedesTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSpecificationTemplates_TenantId_Kind_Status",
                table: "ProcurementSpecificationTemplates",
                columns: new[] { "TenantId", "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSpecificationTemplates_TenantId_Status_EffectiveFromUtc",
                table: "ProcurementSpecificationTemplates",
                columns: new[] { "TenantId", "Status", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSpecificationTemplates_TenantId_TemplateCode_Version",
                table: "ProcurementSpecificationTemplates",
                columns: new[] { "TenantId", "TemplateCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSpecificationTemplates_TenantId_TemplateKey_Status",
                table: "ProcurementSpecificationTemplates",
                columns: new[] { "TenantId", "TemplateKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSpecificationTemplates_TenantId_TemplateKey_Version",
                table: "ProcurementSpecificationTemplates",
                columns: new[] { "TenantId", "TemplateKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSpecificationTemplates_WorkflowDefinitionId",
                table: "ProcurementSpecificationTemplates",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSpecificationTemplates_WorkflowInstanceId",
                table: "ProcurementSpecificationTemplates",
                column: "WorkflowInstanceId");

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSpecificationTemplates_NoDelete]
                ON [dbo].[ProcurementSpecificationTemplates]
                INSTEAD OF DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Specification-template rows are audit records and cannot be physically deleted.', 1;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSpecificationTemplates_LifecycleGuard]
                ON [dbo].[ProcurementSpecificationTemplates]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[Status] <> d.[Status]
                          AND NOT (
                              (d.[Status] = 0 AND i.[Status] = 1) OR
                              (d.[Status] = 1 AND i.[Status] IN (0, 2)) OR
                              (d.[Status] = 2 AND i.[Status] = 3)
                          )
                    )
                        THROW 51001, 'Invalid specification-template lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[TemplateKey] <> d.[TemplateKey] OR i.[Version] <> d.[Version]
                    )
                        THROW 51002, 'Specification-template family and version identifiers are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[Status] <> 0 AND (
                            i.[TemplateCode] <> d.[TemplateCode] OR
                            i.[Name] <> d.[Name] OR
                            ISNULL(i.[Description], N'') <> ISNULL(d.[Description], N'') OR
                            i.[Kind] <> d.[Kind] OR
                            i.[IsDefault] <> d.[IsDefault] OR
                            i.[EffectiveFromUtc] <> d.[EffectiveFromUtc] OR
                            ISNULL(i.[ChangeSummary], N'') <> ISNULL(d.[ChangeSummary], N'') OR
                            i.[Purpose] <> d.[Purpose] OR
                            i.[FunctionalAndPerformanceRequirements] <> d.[FunctionalAndPerformanceRequirements] OR
                            i.[ProcessAndMaterialsRequirements] <> d.[ProcessAndMaterialsRequirements] OR
                            i.[DimensionsAndMarkingRequirements] <> d.[DimensionsAndMarkingRequirements] OR
                            i.[TestingAndInspectionRequirements] <> d.[TestingAndInspectionRequirements] OR
                            i.[ApplicableStandards] <> d.[ApplicableStandards] OR
                            i.[Deliverables] <> d.[Deliverables] OR
                            i.[AcceptanceCriteria] <> d.[AcceptanceCriteria] OR
                            ISNULL(i.[WorkflowDefinitionId], '00000000-0000-0000-0000-000000000000') <>
                                ISNULL(d.[WorkflowDefinitionId], '00000000-0000-0000-0000-000000000000') OR
                            ISNULL(i.[SupersedesTemplateId], '00000000-0000-0000-0000-000000000000') <>
                                ISNULL(d.[SupersedesTemplateId], '00000000-0000-0000-0000-000000000000')
                        )
                    )
                        THROW 51003, 'Specification-template content can be changed only while the revision is Draft.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE (i.[IsDeleted] <> d.[IsDeleted] AND d.[Status] <> 0) OR
                              (d.[IsDeleted] = 1 AND i.[IsDeleted] = 0)
                    )
                        THROW 51004, 'Only Draft specification templates can be soft-deleted and deletion cannot be reversed.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementSpecificationTemplates");
        }
    }
}
