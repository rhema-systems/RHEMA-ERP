using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementAppSubmissionRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementAppSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TimelineCorrelationId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SupersedesSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExportFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ExportFormat = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ExportTemplateVersion = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    ExportChecksumSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ExportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExportedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExportedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ExternalSubmissionReference = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AcknowledgementReference = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcknowledgedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RejectionReference = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementAppSubmissions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementAppSubmissions_Attempt", "[AttemptNumber] >= 1");
                    table.CheckConstraint("CK_ProcurementAppSubmissions_Checksum", "LEN([ExportChecksumSha256]) = 64");
                    table.CheckConstraint("CK_ProcurementAppSubmissions_Outcome", "([Status] = 2 AND [AcknowledgementReference] IS NOT NULL AND [AcknowledgedAtUtc] IS NOT NULL AND [RejectionReference] IS NULL AND [RejectedAtUtc] IS NULL) OR ([Status] = 3 AND [RejectionReference] IS NOT NULL AND [RejectionReason] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL AND [AcknowledgementReference] IS NULL AND [AcknowledgedAtUtc] IS NULL) OR ([Status] IN (0, 1) AND [AcknowledgementReference] IS NULL AND [AcknowledgedAtUtc] IS NULL AND [RejectionReference] IS NULL AND [RejectedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_ProcurementAppSubmissions_Status", "[Status] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_ProcurementAppSubmissions_Submitted", "([Status] = 0 AND [ExternalSubmissionReference] IS NULL AND [SubmittedAtUtc] IS NULL) OR ([Status] IN (1, 2, 3) AND [ExternalSubmissionReference] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementAppSubmissions_ProcurementAppSubmissions_SupersedesSubmissionId",
                        column: x => x.SupersedesSubmissionId,
                        principalTable: "ProcurementAppSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementAppSubmissions_ProcurementPlans_ProcurementPlanId",
                        column: x => x.ProcurementPlanId,
                        principalTable: "ProcurementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementAppSubmissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAppSubmissions_ProcurementPlanId",
                table: "ProcurementAppSubmissions",
                column: "ProcurementPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAppSubmissions_SupersedesSubmissionId",
                table: "ProcurementAppSubmissions",
                column: "SupersedesSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAppSubmissions_TenantId_ProcurementPlanId",
                table: "ProcurementAppSubmissions",
                columns: new[] { "TenantId", "ProcurementPlanId" },
                unique: true,
                filter: "[AttemptNumber] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAppSubmissions_TenantId_ProcurementPlanId_AttemptNumber",
                table: "ProcurementAppSubmissions",
                columns: new[] { "TenantId", "ProcurementPlanId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAppSubmissions_TenantId_Status_UpdatedAt",
                table: "ProcurementAppSubmissions",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAppSubmissions_TenantId_SubmissionNumber_AttemptNumber",
                table: "ProcurementAppSubmissions",
                columns: new[] { "TenantId", "SubmissionNumber", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAppSubmissions_TenantId_TimelineCorrelationId_AttemptNumber",
                table: "ProcurementAppSubmissions",
                columns: new[] { "TenantId", "TimelineCorrelationId", "AttemptNumber" });

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementAppSubmissions_NoDelete]
                ON [dbo].[ProcurementAppSubmissions]
                INSTEAD OF DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'APP submission attempts are retained as auditable records and cannot be deleted.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementAppSubmissions_LifecycleGuard]
                ON [dbo].[ProcurementAppSubmissions]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        INNER JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[TenantId] <> d.[TenantId]
                           OR i.[ProcurementPlanId] <> d.[ProcurementPlanId]
                           OR i.[SubmissionNumber] <> d.[SubmissionNumber]
                           OR i.[AttemptNumber] <> d.[AttemptNumber]
                           OR i.[TimelineCorrelationId] <> d.[TimelineCorrelationId]
                           OR ISNULL(i.[SupersedesSubmissionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[SupersedesSubmissionId], '00000000-0000-0000-0000-000000000000')
                           OR i.[ExportFileName] <> d.[ExportFileName]
                           OR i.[ExportFormat] <> d.[ExportFormat]
                           OR i.[ExportTemplateVersion] <> d.[ExportTemplateVersion]
                           OR i.[ExportChecksumSha256] <> d.[ExportChecksumSha256]
                           OR i.[ExportedAtUtc] <> d.[ExportedAtUtc]
                           OR i.[ExportedById] <> d.[ExportedById]
                           OR i.[ExportedByName] <> d.[ExportedByName]
                           OR i.[CreatedAt] <> d.[CreatedAt]
                           OR ISNULL(i.[CreatedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[CreatedById], '00000000-0000-0000-0000-000000000000')
                           OR i.[IsDeleted] <> d.[IsDeleted]
                           OR NOT ((d.[Status] = 0 AND i.[Status] = 1) OR (d.[Status] = 1 AND i.[Status] IN (2, 3)))
                    )
                    BEGIN
                        THROW 51020, 'APP submission identity, export metadata, retention, or lifecycle transition is immutable.', 1;
                    END
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementAppSubmissions_NoDelete]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementAppSubmissions_LifecycleGuard]");

            migrationBuilder.DropTable(
                name: "ProcurementAppSubmissions");
        }
    }
}
