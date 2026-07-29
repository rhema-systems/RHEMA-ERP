using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCentralDocumentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CentralDocumentAccessRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessProfile = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RoleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PermissionKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    CanUpload = table.Column<bool>(type: "bit", nullable: false),
                    CanAnnotate = table.Column<bool>(type: "bit", nullable: false),
                    CanApprove = table.Column<bool>(type: "bit", nullable: false),
                    CanArchive = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_CentralDocumentAccessRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentralDocumentAccessRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CentralDocumentMetadataTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequiredFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelationshipsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetentionRule = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AccessProfile = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_CentralDocumentMetadataTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentralDocumentMetadataTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CentralDocumentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentReference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SourceRecordReference = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    SourceRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MetadataTemplateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RepositoryStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RepositoryPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalDocumentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrentVersion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    VersionStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AnnotationStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CommentStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AccessProfile = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RetentionStatus = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CentralDocumentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentralDocumentRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CentralDocumentRetentionPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RetentionDays = table.Column<int>(type: "int", nullable: false),
                    RequiresLegalHoldReview = table.Column<bool>(type: "bit", nullable: false),
                    AllowArchive = table.Column<bool>(type: "bit", nullable: false),
                    AllowDestruction = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CentralDocumentRetentionPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentralDocumentRetentionPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CentralDocumentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RepositoryPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RenditionPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CentralDocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentralDocumentVersions_CentralDocumentRecords_DocumentRecordId",
                        column: x => x.DocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CentralDocumentVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CentralDocumentAnnotationReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SyncfusionAnnotationStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AssignedReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AnnotationStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_CentralDocumentAnnotationReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentralDocumentAnnotationReviews_CentralDocumentRecords_DocumentRecordId",
                        column: x => x.DocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CentralDocumentAnnotationReviews_CentralDocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CentralDocumentAnnotationReviews_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentAccessRules_TenantId_AccessProfile_Module_RoleName",
                table: "CentralDocumentAccessRules",
                columns: new[] { "TenantId", "AccessProfile", "Module", "RoleName" });

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentAnnotationReviews_DocumentRecordId",
                table: "CentralDocumentAnnotationReviews",
                column: "DocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentAnnotationReviews_DocumentVersionId",
                table: "CentralDocumentAnnotationReviews",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentAnnotationReviews_TenantId_DocumentRecordId_Status",
                table: "CentralDocumentAnnotationReviews",
                columns: new[] { "TenantId", "DocumentRecordId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentMetadataTemplates_TenantId_Module_DocumentType",
                table: "CentralDocumentMetadataTemplates",
                columns: new[] { "TenantId", "Module", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentMetadataTemplates_TenantId_TemplateCode",
                table: "CentralDocumentMetadataTemplates",
                columns: new[] { "TenantId", "TemplateCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentRecords_TenantId_DocumentReference",
                table: "CentralDocumentRecords",
                columns: new[] { "TenantId", "DocumentReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentRecords_TenantId_SourceModule_SourceRecordReference",
                table: "CentralDocumentRecords",
                columns: new[] { "TenantId", "SourceModule", "SourceRecordReference" });

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentRetentionPolicies_TenantId_Module_DocumentType",
                table: "CentralDocumentRetentionPolicies",
                columns: new[] { "TenantId", "Module", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentRetentionPolicies_TenantId_PolicyCode",
                table: "CentralDocumentRetentionPolicies",
                columns: new[] { "TenantId", "PolicyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentVersions_DocumentRecordId",
                table: "CentralDocumentVersions",
                column: "DocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentVersions_TenantId_DocumentRecordId_VersionNumber",
                table: "CentralDocumentVersions",
                columns: new[] { "TenantId", "DocumentRecordId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CentralDocumentAccessRules");

            migrationBuilder.DropTable(
                name: "CentralDocumentAnnotationReviews");

            migrationBuilder.DropTable(
                name: "CentralDocumentMetadataTemplates");

            migrationBuilder.DropTable(
                name: "CentralDocumentRetentionPolicies");

            migrationBuilder.DropTable(
                name: "CentralDocumentVersions");

            migrationBuilder.DropTable(
                name: "CentralDocumentRecords");
        }
    }
}
