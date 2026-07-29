using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSupplierEvidencePacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RegistrationCategory",
                table: "BusinessPartnerRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChecksumSha256",
                table: "BusinessPartnerRegistrationDocuments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassificationCode",
                table: "BusinessPartnerRegistrationDocuments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceRequirementCode",
                table: "BusinessPartnerRegistrationDocuments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "BusinessPartnerRegistrationDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedAtUtc",
                table: "BusinessPartnerRegistrationDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierEvidencePackVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceConfigurationProfileCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceConfigurationProfileVersion = table.Column<int>(type: "int", nullable: false),
                    CreationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastOperationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersedesVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LifecycleSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierEvidencePackVersions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierEvidencePackVersions_State", "[Category] BETWEEN 0 AND 2 AND [Version] >= 1 AND [Status] BETWEEN 0 AND 3 AND [SourceConfigurationProfileVersion] >= 1 AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]) AND LEN([CreationCorrelationId]) > 0 AND LEN([LastOperationCorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([LifecycleSnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierEvidencePackVersions_ProcurementConfigurationProfiles_SourceConfigurationProfileId",
                        column: x => x.SourceConfigurationProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierEvidencePackVersions_ProcurementSupplierEvidencePackVersions_SupersedesVersionId",
                        column: x => x.SupersedesVersionId,
                        principalTable: "ProcurementSupplierEvidencePackVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierEvidencePackVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierEvidencePackVersions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierEvidencePackVersions_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierEvidenceRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    ClassificationScheme = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AllowedClassificationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidityMode = table.Column<int>(type: "int", nullable: false),
                    MinimumRemainingDays = table.Column<int>(type: "int", nullable: true),
                    ApprovalStepOrder = table.Column<int>(type: "int", nullable: false),
                    ApprovalStepName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaxFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AllowedMimeTypesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierEvidenceRequirements", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierEvidenceRequirements_Kind", "([Kind] = 0 AND [DocumentType] IS NOT NULL) OR ([Kind] = 1 AND [ClassificationScheme] IS NOT NULL AND [AllowedClassificationsJson] IS NOT NULL) OR ([Kind] = 2 AND [DocumentType] IS NOT NULL AND [ClassificationScheme] IS NOT NULL AND [AllowedClassificationsJson] IS NOT NULL)");
                    table.CheckConstraint("CK_ProcurementSupplierEvidenceRequirements_State", "[Kind] BETWEEN 0 AND 2 AND [ValidityMode] BETWEEN 0 AND 2 AND [ApprovalStepOrder] >= 1 AND [MaxFileSizeBytes] > 0 AND ([AllowedClassificationsJson] IS NULL OR ISJSON([AllowedClassificationsJson]) = 1) AND ISJSON([AllowedMimeTypesJson]) = 1 AND LEN([IntegrityHash]) = 64 AND (([ValidityMode] = 2 AND [MinimumRemainingDays] > 0) OR ([ValidityMode] <> 2 AND [MinimumRemainingDays] IS NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierEvidenceRequirements_ProcurementSupplierEvidencePackVersions_PackVersionId",
                        column: x => x.PackVersionId,
                        principalTable: "ProcurementSupplierEvidencePackVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierEvidenceRequirements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierRegistrationEvidencePackBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationCategory = table.Column<int>(type: "int", nullable: false),
                    PackCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PackVersion = table.Column<int>(type: "int", nullable: false),
                    BoundAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BoundById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PackSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierRegistrationEvidencePackBindings", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierRegistrationEvidencePackBindings_State", "[RegistrationCategory] BETWEEN 0 AND 2 AND [PackVersion] >= 1 AND LEN([PackSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([PackSnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRegistrationEvidencePackBindings_BusinessPartnerRegistrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "BusinessPartnerRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRegistrationEvidencePackBindings_ProcurementSupplierEvidencePackVersions_PackVersionId",
                        column: x => x.PackVersionId,
                        principalTable: "ProcurementSupplierEvidencePackVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRegistrationEvidencePackBindings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_SourceConfigurationProfileId",
                table: "ProcurementSupplierEvidencePackVersions",
                column: "SourceConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_SupersedesVersionId",
                table: "ProcurementSupplierEvidencePackVersions",
                column: "SupersedesVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_TenantId_Category_Status_EffectiveFromUtc",
                table: "ProcurementSupplierEvidencePackVersions",
                columns: new[] { "TenantId", "Category", "Status", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_TenantId_CreationCorrelationId",
                table: "ProcurementSupplierEvidencePackVersions",
                columns: new[] { "TenantId", "CreationCorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_TenantId_PackCode_Version",
                table: "ProcurementSupplierEvidencePackVersions",
                columns: new[] { "TenantId", "PackCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_TenantId_PackKey",
                table: "ProcurementSupplierEvidencePackVersions",
                columns: new[] { "TenantId", "PackKey" },
                unique: true,
                filter: "[Status] IN (0, 1) AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_TenantId_PackKey_Version",
                table: "ProcurementSupplierEvidencePackVersions",
                columns: new[] { "TenantId", "PackKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_TenantId_SourceConfigurationProfileId",
                table: "ProcurementSupplierEvidencePackVersions",
                columns: new[] { "TenantId", "SourceConfigurationProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_TenantId_WorkflowDefinitionId",
                table: "ProcurementSupplierEvidencePackVersions",
                columns: new[] { "TenantId", "WorkflowDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_WorkflowDefinitionId",
                table: "ProcurementSupplierEvidencePackVersions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidencePackVersions_WorkflowInstanceId",
                table: "ProcurementSupplierEvidencePackVersions",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidenceRequirements_PackVersionId",
                table: "ProcurementSupplierEvidenceRequirements",
                column: "PackVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidenceRequirements_TenantId_PackVersionId_ApprovalStepOrder",
                table: "ProcurementSupplierEvidenceRequirements",
                columns: new[] { "TenantId", "PackVersionId", "ApprovalStepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierEvidenceRequirements_TenantId_PackVersionId_RequirementCode",
                table: "ProcurementSupplierEvidenceRequirements",
                columns: new[] { "TenantId", "PackVersionId", "RequirementCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRegistrationEvidencePackBindings_PackVersionId",
                table: "ProcurementSupplierRegistrationEvidencePackBindings",
                column: "PackVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRegistrationEvidencePackBindings_RegistrationId",
                table: "ProcurementSupplierRegistrationEvidencePackBindings",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRegistrationEvidencePackBindings_TenantId_PackVersionId",
                table: "ProcurementSupplierRegistrationEvidencePackBindings",
                columns: new[] { "TenantId", "PackVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRegistrationEvidencePackBindings_TenantId_RegistrationId",
                table: "ProcurementSupplierRegistrationEvidencePackBindings",
                columns: new[] { "TenantId", "RegistrationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRegistrations_TenantId_RegistrationCategory_Status",
                table: "BusinessPartnerRegistrations",
                columns: new[] { "TenantId", "RegistrationCategory", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRegistrationDocuments_TenantId_RegistrationId_EvidenceRequirementCode",
                table: "BusinessPartnerRegistrationDocuments",
                columns: new[] { "TenantId", "RegistrationId", "EvidenceRequirementCode" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessPartnerRegistrations_RegistrationCategory",
                table: "BusinessPartnerRegistrations",
                sql: "[RegistrationCategory] IS NULL OR [RegistrationCategory] BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessPartnerRegistrationDocuments_EvidenceValidity",
                table: "BusinessPartnerRegistrationDocuments",
                sql: "([ChecksumSha256] IS NULL OR LEN([ChecksumSha256]) = 64) " +
                     "AND ([ExpiresAtUtc] IS NULL OR [IssuedAtUtc] IS NULL OR [ExpiresAtUtc] > [IssuedAtUtc])");

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSupplierEvidencePackVersions_Lifecycle]
                ON [dbo].[ProcurementSupplierEvidencePackVersions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
                        THROW 51700, 'Supplier evidence-pack versions cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementConfigurationProfiles] p
                          ON p.[Id] = i.[SourceConfigurationProfileId]
                        WHERE p.[TenantId] <> i.[TenantId])
                        THROW 51701, 'Supplier evidence-pack configuration-profile tenant mismatch.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[WorkflowDefinitions] w
                          ON w.[Id] = i.[WorkflowDefinitionId]
                        WHERE w.[TenantId] <> i.[TenantId])
                        THROW 51702, 'Supplier evidence-pack workflow tenant mismatch.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[WorkflowInstances] w
                          ON w.[Id] = i.[WorkflowInstanceId]
                        WHERE i.[WorkflowInstanceId] IS NOT NULL
                          AND (w.[TenantId] <> i.[TenantId]
                               OR w.[WorkflowDefinitionId] <> i.[WorkflowDefinitionId]
                               OR w.[EntityId] <> i.[Id]))
                        THROW 51703, 'Supplier evidence-pack workflow-instance binding mismatch.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementSupplierEvidencePackVersions] s
                          ON s.[Id] = i.[SupersedesVersionId]
                        WHERE i.[SupersedesVersionId] IS NOT NULL
                          AND (s.[TenantId] <> i.[TenantId]
                               OR s.[PackKey] <> i.[PackKey]
                               OR s.[Version] >= i.[Version]))
                        THROW 51704, 'Supplier evidence-pack supersession lineage mismatch.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE NOT (
                            d.[Status] = i.[Status]
                            OR (d.[Status] = 0 AND i.[Status] = 1)
                            OR (d.[Status] = 1 AND i.[Status] IN (0, 2))
                            OR (d.[Status] = 2 AND i.[Status] = 3)))
                        THROW 51705, 'Invalid supplier evidence-pack lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[Status] IN (2, 3)
                          AND (
                            i.[TenantId] <> d.[TenantId]
                            OR i.[PackKey] <> d.[PackKey]
                            OR i.[PackCode] <> d.[PackCode]
                            OR i.[Name] <> d.[Name]
                            OR ISNULL(i.[Description], '') <> ISNULL(d.[Description], '')
                            OR i.[Category] <> d.[Category]
                            OR i.[Version] <> d.[Version]
                            OR i.[EffectiveFromUtc] <> d.[EffectiveFromUtc]
                            OR ISNULL(i.[EffectiveToUtc], '9999-12-31') <> ISNULL(d.[EffectiveToUtc], '9999-12-31')
                            OR i.[SourceConfigurationProfileId] <> d.[SourceConfigurationProfileId]
                            OR i.[WorkflowDefinitionId] <> d.[WorkflowDefinitionId]
                            OR ISNULL(i.[SupersedesVersionId], '00000000-0000-0000-0000-000000000000')
                               <> ISNULL(d.[SupersedesVersionId], '00000000-0000-0000-0000-000000000000')))
                        THROW 51706, 'Published or retired supplier evidence-pack content is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted
                        WHERE [Status] = 2
                          AND ([SubmittedById] IS NULL OR [SubmittedAtUtc] IS NULL
                               OR [PublishedById] IS NULL OR [PublishedAtUtc] IS NULL
                               OR [WorkflowInstanceId] IS NULL))
                        THROW 51707, 'Published supplier evidence packs require submission, workflow, and publication lineage.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted
                        WHERE [Status] = 3
                          AND ([RetiredById] IS NULL OR [RetiredAtUtc] IS NULL))
                        THROW 51708, 'Retired supplier evidence packs require retirement lineage.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSupplierEvidenceRequirements_Protected]
                ON [dbo].[ProcurementSupplierEvidenceRequirements]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
                        THROW 51709, 'Supplier evidence requirements cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementSupplierEvidencePackVersions] p
                          ON p.[Id] = i.[PackVersionId]
                        WHERE p.[TenantId] <> i.[TenantId])
                        THROW 51710, 'Supplier evidence requirement tenant mismatch.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementSupplierEvidencePackVersions] p
                          ON p.[Id] = i.[PackVersionId]
                        WHERE p.[Status] <> 0)
                        THROW 51711, 'Only Draft supplier evidence-pack requirements may change.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSupplierRegistrationEvidencePackBindings_Immutable]
                ON [dbo].[ProcurementSupplierRegistrationEvidencePackBindings]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51712, 'Supplier registration evidence-pack bindings are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[BusinessPartnerRegistrations] r
                          ON r.[Id] = i.[RegistrationId]
                        JOIN [dbo].[ProcurementSupplierEvidencePackVersions] p
                          ON p.[Id] = i.[PackVersionId]
                        WHERE r.[TenantId] <> i.[TenantId]
                           OR p.[TenantId] <> i.[TenantId]
                           OR r.[RegistrationCategory] <> i.[RegistrationCategory]
                           OR p.[Category] <> i.[RegistrationCategory]
                           OR p.[Status] <> 2
                           OR p.[IntegrityHash] <> i.[PackSnapshotHash]
                           OR r.[Status] NOT IN ('Draft', 'MoreInfoRequired'))
                        THROW 51713, 'Supplier registration evidence-pack binding lineage mismatch.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSupplierRegistrationEvidencePackBindings_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSupplierEvidenceRequirements_Protected];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSupplierEvidencePackVersions_Lifecycle];");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessPartnerRegistrationDocuments_EvidenceValidity",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessPartnerRegistrations_RegistrationCategory",
                table: "BusinessPartnerRegistrations");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierEvidenceRequirements");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierRegistrationEvidencePackBindings");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierEvidencePackVersions");

            migrationBuilder.DropColumn(
                name: "RegistrationCategory",
                table: "BusinessPartnerRegistrations");

            migrationBuilder.DropColumn(
                name: "ChecksumSha256",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropColumn(
                name: "ClassificationCode",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropColumn(
                name: "EvidenceRequirementCode",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "BusinessPartnerRegistrationDocuments");

            migrationBuilder.DropColumn(
                name: "IssuedAtUtc",
                table: "BusinessPartnerRegistrationDocuments");
        }
    }
}
