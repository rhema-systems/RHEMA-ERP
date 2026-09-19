using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementControlEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementControlEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    RuleCode = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleVersion = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DecisionKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InputValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    CausationId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementControlEvents", x => x.Id);
                    table.CheckConstraint("CK_ProcurementControlEvents_Result", "[Result] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_ProcurementControlEvents_SchemaVersion", "[SchemaVersion] >= 1");
                    table.ForeignKey(
                        name: "FK_ProcurementControlEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementControlEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementControlEventEvidenceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControlEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceKind = table.Column<int>(type: "int", nullable: false),
                    WorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RequirementKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementControlEventEvidenceLinks", x => x.Id);
                    table.CheckConstraint("CK_ProcurementControlEventEvidenceLinks_Kind", "[ReferenceKind] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_ProcurementControlEventEvidenceLinks_ReferenceTarget", "([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL) OR ([ReferenceKind] = 2 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementControlEventEvidenceLinks_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementControlEventEvidenceLinks_ProcurementControlEvents_ControlEventId",
                        column: x => x.ControlEventId,
                        principalTable: "ProcurementControlEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementControlEventEvidenceLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementControlEventEvidenceLinks_WorkflowEvidenceDocuments_WorkflowEvidenceDocumentId",
                        column: x => x.WorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEventEvidenceLinks_ControlEventId",
                table: "ProcurementControlEventEvidenceLinks",
                column: "ControlEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEventEvidenceLinks_FileUploadRecordId",
                table: "ProcurementControlEventEvidenceLinks",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEventEvidenceLinks_TenantId_ControlEventId_ReferenceKind_Reference",
                table: "ProcurementControlEventEvidenceLinks",
                columns: new[] { "TenantId", "ControlEventId", "ReferenceKind", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEventEvidenceLinks_TenantId_FileUploadRecordId",
                table: "ProcurementControlEventEvidenceLinks",
                columns: new[] { "TenantId", "FileUploadRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEventEvidenceLinks_TenantId_WorkflowEvidenceDocumentId",
                table: "ProcurementControlEventEvidenceLinks",
                columns: new[] { "TenantId", "WorkflowEvidenceDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEventEvidenceLinks_WorkflowEvidenceDocumentId",
                table: "ProcurementControlEventEvidenceLinks",
                column: "WorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEvents_ActorUserId",
                table: "ProcurementControlEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEvents_TenantId_CorrelationId_OccurredAtUtc",
                table: "ProcurementControlEvents",
                columns: new[] { "TenantId", "CorrelationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEvents_TenantId_EventKey",
                table: "ProcurementControlEvents",
                columns: new[] { "TenantId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEvents_TenantId_EventType_Result_OccurredAtUtc",
                table: "ProcurementControlEvents",
                columns: new[] { "TenantId", "EventType", "Result", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEvents_TenantId_OccurredAtUtc",
                table: "ProcurementControlEvents",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEvents_TenantId_RuleCode_OccurredAtUtc",
                table: "ProcurementControlEvents",
                columns: new[] { "TenantId", "RuleCode", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementControlEvents_TenantId_SourceType_SourceReference",
                table: "ProcurementControlEvents",
                columns: new[] { "TenantId", "SourceType", "SourceReference" });

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementControlEvents_AppendOnly]
                ON [dbo].[ProcurementControlEvents]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Procurement control events are append-only and cannot be updated or deleted.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementControlEventEvidenceLinks_AppendOnly]
                ON [dbo].[ProcurementControlEventEvidenceLinks]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51001, 'Procurement control-event evidence links are append-only and cannot be updated or deleted.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementControlEventEvidenceLinks_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementControlEvents_AppendOnly];");

            migrationBuilder.DropTable(
                name: "ProcurementControlEventEvidenceLinks");

            migrationBuilder.DropTable(
                name: "ProcurementControlEvents");
        }
    }
}
