using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementMasterDataChangeControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementMasterDataControlPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceType = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MakerRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheckerRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequireIndependentApproval = table.Column<bool>(type: "bit", nullable: false),
                    RequireRevalidation = table.Column<bool>(type: "bit", nullable: false),
                    RequireEvidence = table.Column<bool>(type: "bit", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersedesPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementMasterDataControlPolicies", x => x.Id);
                    table.CheckConstraint("CK_ProcurementMasterDataControlPolicies_EffectivePeriod", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc]");
                    table.CheckConstraint("CK_ProcurementMasterDataControlPolicies_RequiredControls", "[RequireIndependentApproval] = 1 AND [RequireRevalidation] = 1");
                    table.CheckConstraint("CK_ProcurementMasterDataControlPolicies_ResourceType", "[ResourceType] BETWEEN 0 AND 8");
                    table.CheckConstraint("CK_ProcurementMasterDataControlPolicies_Status", "[Status] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_ProcurementMasterDataControlPolicies_Version", "[Version] >= 1");
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataControlPolicies_ProcurementMasterDataControlPolicies_SupersedesPolicyId",
                        column: x => x.SupersedesPolicyId,
                        principalTable: "ProcurementMasterDataControlPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataControlPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataControlPolicies_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementMasterDataChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    ResourceType = table.Column<int>(type: "int", nullable: false),
                    TargetKind = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ProposedChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProposedChangesHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    AppliedAfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppliedAfterHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EffectiveAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MakerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckerComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RevalidatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevalidatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevalidationPassed = table.Column<bool>(type: "bit", nullable: true),
                    RevalidationMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RevalidatedSnapshotHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    AppliedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementMasterDataChangeRequests", x => x.Id);
                    table.CheckConstraint("CK_ProcurementMasterDataChangeRequests_ApprovalActors", "[CheckerUserId] IS NULL OR [CheckerUserId] <> [MakerUserId]");
                    table.CheckConstraint("CK_ProcurementMasterDataChangeRequests_Hashes", "LEN([BeforeHash]) = 64 AND LEN([ProposedChangesHash]) = 64 AND ([AppliedAfterHash] IS NULL OR LEN([AppliedAfterHash]) = 64) AND ([RevalidatedSnapshotHash] IS NULL OR LEN([RevalidatedSnapshotHash]) = 64)");
                    table.CheckConstraint("CK_ProcurementMasterDataChangeRequests_ResourceType", "[ResourceType] BETWEEN 0 AND 8");
                    table.CheckConstraint("CK_ProcurementMasterDataChangeRequests_Status", "[Status] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_ProcurementMasterDataChangeRequests_TargetKind", "[TargetKind] BETWEEN 0 AND 7");
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataChangeRequests_ProcurementMasterDataControlPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "ProcurementMasterDataControlPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataChangeRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataChangeRequests_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataChangeRequests_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementMasterDataChangeEvidenceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementMasterDataChangeEvidenceLinks", x => x.Id);
                    table.CheckConstraint("CK_ProcurementMasterDataChangeEvidenceLinks_Kind", "[ReferenceKind] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_ProcurementMasterDataChangeEvidenceLinks_ReferenceTarget", "([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL) OR ([ReferenceKind] = 2 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataChangeEvidenceLinks_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataChangeEvidenceLinks_ProcurementMasterDataChangeRequests_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalTable: "ProcurementMasterDataChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataChangeEvidenceLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementMasterDataChangeEvidenceLinks_WorkflowEvidenceDocuments_WorkflowEvidenceDocumentId",
                        column: x => x.WorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeEvidenceLinks_ChangeRequestId",
                table: "ProcurementMasterDataChangeEvidenceLinks",
                column: "ChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeEvidenceLinks_FileUploadRecordId",
                table: "ProcurementMasterDataChangeEvidenceLinks",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeEvidenceLinks_TenantId_ChangeRequestId_ReferenceKind_Reference",
                table: "ProcurementMasterDataChangeEvidenceLinks",
                columns: new[] { "TenantId", "ChangeRequestId", "ReferenceKind", "Reference" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeEvidenceLinks_TenantId_FileUploadRecordId",
                table: "ProcurementMasterDataChangeEvidenceLinks",
                columns: new[] { "TenantId", "FileUploadRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeEvidenceLinks_TenantId_WorkflowEvidenceDocumentId",
                table: "ProcurementMasterDataChangeEvidenceLinks",
                columns: new[] { "TenantId", "WorkflowEvidenceDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeEvidenceLinks_WorkflowEvidenceDocumentId",
                table: "ProcurementMasterDataChangeEvidenceLinks",
                column: "WorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeRequests_PolicyId",
                table: "ProcurementMasterDataChangeRequests",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeRequests_TenantId_CorrelationId",
                table: "ProcurementMasterDataChangeRequests",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeRequests_TenantId_PolicyId",
                table: "ProcurementMasterDataChangeRequests",
                columns: new[] { "TenantId", "PolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeRequests_TenantId_RequestNumber",
                table: "ProcurementMasterDataChangeRequests",
                columns: new[] { "TenantId", "RequestNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeRequests_TenantId_ResourceType_TargetId_CreatedAt",
                table: "ProcurementMasterDataChangeRequests",
                columns: new[] { "TenantId", "ResourceType", "TargetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeRequests_TenantId_Status_EffectiveAtUtc",
                table: "ProcurementMasterDataChangeRequests",
                columns: new[] { "TenantId", "Status", "EffectiveAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeRequests_WorkflowDefinitionId",
                table: "ProcurementMasterDataChangeRequests",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataChangeRequests_WorkflowInstanceId",
                table: "ProcurementMasterDataChangeRequests",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataControlPolicies_SupersedesPolicyId",
                table: "ProcurementMasterDataControlPolicies",
                column: "SupersedesPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataControlPolicies_TenantId_ResourceType_Status",
                table: "ProcurementMasterDataControlPolicies",
                columns: new[] { "TenantId", "ResourceType", "Status" },
                unique: true,
                filter: "[Status] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataControlPolicies_TenantId_ResourceType_Version",
                table: "ProcurementMasterDataControlPolicies",
                columns: new[] { "TenantId", "ResourceType", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataControlPolicies_TenantId_Status_EffectiveFromUtc",
                table: "ProcurementMasterDataControlPolicies",
                columns: new[] { "TenantId", "Status", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementMasterDataControlPolicies_WorkflowDefinitionId",
                table: "ProcurementMasterDataControlPolicies",
                column: "WorkflowDefinitionId");

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementMasterDataControlPolicies_LifecycleGuard]
                ON [dbo].[ProcurementMasterDataControlPolicies]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51010, 'Procurement master-data control policy versions cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM deleted d JOIN inserted i ON i.Id = d.Id
                        WHERE d.Status <> 0 AND (
                            i.PolicyKey <> d.PolicyKey OR i.ResourceType <> d.ResourceType OR i.Version <> d.Version OR
                            i.Name <> d.Name OR ISNULL(i.Description, '') <> ISNULL(d.Description, '') OR
                            i.MakerRolesJson <> d.MakerRolesJson OR i.CheckerRolesJson <> d.CheckerRolesJson OR
                            i.RequireIndependentApproval <> d.RequireIndependentApproval OR i.RequireRevalidation <> d.RequireRevalidation OR
                            i.RequireEvidence <> d.RequireEvidence OR ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') OR
                            i.EffectiveFromUtc <> d.EffectiveFromUtc OR ISNULL(i.SupersedesPolicyId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.SupersedesPolicyId, '00000000-0000-0000-0000-000000000000') OR
                            i.TenantId <> d.TenantId))
                        THROW 51011, 'Active and retired procurement master-data policy content is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM deleted d JOIN inserted i ON i.Id = d.Id
                        WHERE i.Status <> d.Status AND NOT ((d.Status = 0 AND i.Status = 1) OR (d.Status = 1 AND i.Status = 2)))
                        THROW 51012, 'Invalid procurement master-data control policy lifecycle transition.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementMasterDataChangeRequests_NoDelete]
                ON [dbo].[ProcurementMasterDataChangeRequests]
                INSTEAD OF DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51013, 'Procurement master-data change requests cannot be deleted.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementMasterDataChangeRequests_SnapshotGuard]
                ON [dbo].[ProcurementMasterDataChangeRequests]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM deleted d JOIN inserted i ON i.Id = d.Id
                        WHERE d.Status <> 0 AND (
                            i.PolicyId <> d.PolicyId OR i.PolicyVersion <> d.PolicyVersion OR i.ResourceType <> d.ResourceType OR
                            i.TargetKind <> d.TargetKind OR i.TargetId <> d.TargetId OR i.TargetReference <> d.TargetReference OR
                            i.BeforeJson <> d.BeforeJson OR i.BeforeHash <> d.BeforeHash OR
                            i.ProposedChangesJson <> d.ProposedChangesJson OR i.ProposedChangesHash <> d.ProposedChangesHash OR
                            i.Reason <> d.Reason OR i.EffectiveAtUtc <> d.EffectiveAtUtc OR i.MakerUserId <> d.MakerUserId OR
                            ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') OR
                            i.CorrelationId <> d.CorrelationId OR i.TenantId <> d.TenantId))
                        THROW 51014, 'Submitted procurement master-data before/proposed snapshots and control lineage are immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM deleted d JOIN inserted i ON i.Id = d.Id
                        WHERE i.Status <> d.Status AND NOT (
                            (d.Status = 0 AND i.Status IN (1, 5, 6)) OR
                            (d.Status = 1 AND i.Status IN (2, 3, 5, 6)) OR
                            (d.Status = 2 AND i.Status IN (4, 6))))
                        THROW 51015, 'Invalid procurement master-data change-request lifecycle transition.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementMasterDataChangeEvidenceLinks_SubmittedGuard]
                ON [dbo].[ProcurementMasterDataChangeEvidenceLinks]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM (
                            SELECT ChangeRequestId FROM inserted
                            UNION
                            SELECT ChangeRequestId FROM deleted
                        ) changed
                        JOIN [dbo].[ProcurementMasterDataChangeRequests] request ON request.Id = changed.ChangeRequestId
                        WHERE request.Status <> 0)
                        THROW 51016, 'Evidence references are frozen when a procurement master-data change request leaves Draft.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementMasterDataChangeEvidenceLinks_SubmittedGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementMasterDataChangeRequests_SnapshotGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementMasterDataChangeRequests_NoDelete];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementMasterDataControlPolicies_LifecycleGuard];");

            migrationBuilder.DropTable(
                name: "ProcurementMasterDataChangeEvidenceLinks");

            migrationBuilder.DropTable(
                name: "ProcurementMasterDataChangeRequests");

            migrationBuilder.DropTable(
                name: "ProcurementMasterDataControlPolicies");
        }
    }
}
