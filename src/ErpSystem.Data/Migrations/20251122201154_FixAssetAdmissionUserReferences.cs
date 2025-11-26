using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixAssetAdmissionUserReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("2f48d96c-f03f-4656-84a5-6b925cae4cdb"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4cd77c3e-d16a-4806-84fd-b28559ede347"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5493ae24-69eb-4084-a655-f8149dd9339b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8aa776b1-2759-43de-9fda-4e283740c275"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("9c9b954b-e74c-46a3-9334-5290c3b82676"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ba70063d-f1a4-43ce-bedb-fb898140c74e"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("cf7ff630-3ca4-42ce-997a-85a3de2fb82a"));

            migrationBuilder.CreateTable(
                name: "AssetAdmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmissionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmissionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssetConditionOnAdmission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AdmissionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ObservedProblems = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MileageReading = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    HoursReading = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    FuelLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AdmissionChecklist = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhotoPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdmissionLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BayOrStation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EstimatedCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedDischargeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DischargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AssetAdmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetAdmissions_JobCard_JobCardId",
                        column: x => x.JobCardId,
                        principalTable: "JobCard",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetAdmissions_MaintenanceAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetAdmissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetAdmissions_Users_AdmittedById",
                        column: x => x.AdmittedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetAdmissions_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssetDischarges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DischargeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DischargeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DischargedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetConditionOnDischarge = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DischargeNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WorkCompleted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RemainingIssues = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MileageReading = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    HoursReading = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    FuelLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    QualityCheckPassed = table.Column<bool>(type: "bit", nullable: false),
                    QualityCheckedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QualityCheckDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QualityCheckNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DischargeChecklist = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhotoPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertificateGenerated = table.Column<bool>(type: "bit", nullable: false),
                    CertificateGeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CertificatePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CustomerAcceptance = table.Column<bool>(type: "bit", nullable: false),
                    AcceptedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcceptedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptanceNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequiresFollowUp = table.Column<bool>(type: "bit", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FollowUpInstructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WarrantyDays = table.Column<int>(type: "int", nullable: false),
                    WarrantyExpiration = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WarrantyTerms = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AssetDischarges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDischarges_AssetAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "AssetAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDischarges_JobCard_JobCardId",
                        column: x => x.JobCardId,
                        principalTable: "JobCard",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetDischarges_MaintenanceAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDischarges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDischarges_Users_AcceptedById",
                        column: x => x.AcceptedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetDischarges_Users_DischargedById",
                        column: x => x.DischargedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetDischarges_Users_QualityCheckedById",
                        column: x => x.QualityCheckedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetDischarges_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssetMaintenanceDowntimes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DischargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DowntimeStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DowntimeEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DowntimeMinutes = table.Column<int>(type: "int", nullable: false),
                    DowntimeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EstimatedCostImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ActualCostImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_AssetMaintenanceDowntimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetMaintenanceDowntimes_AssetAdmissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "AssetAdmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetMaintenanceDowntimes_AssetDischarges_DischargeId",
                        column: x => x.DischargeId,
                        principalTable: "AssetDischarges",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetMaintenanceDowntimes_JobCard_JobCardId",
                        column: x => x.JobCardId,
                        principalTable: "JobCard",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetMaintenanceDowntimes_MaintenanceAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetMaintenanceDowntimes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetMaintenanceDowntimes_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceCertificate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DischargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileFormat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CertificateData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssetDischargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_MaintenanceCertificate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceCertificate_AssetDischarges_AssetDischargeId",
                        column: x => x.AssetDischargeId,
                        principalTable: "AssetDischarges",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceCertificate_AssetDischarges_DischargeId",
                        column: x => x.DischargeId,
                        principalTable: "AssetDischarges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceCertificate_MaintenanceAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceCertificate_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceCertificate_Users_IssuedById",
                        column: x => x.IssuedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(8939));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9002));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9004));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9007));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9225));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9239));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9246));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9252));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9264));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9282));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9288));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9294));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9305));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9315));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9322));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9328));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9338));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9365));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9371));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9377));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9435));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9437));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9438));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9441));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9445));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9446));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9447));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9448));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9448));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9449));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9450));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9451));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9452));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9500));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9502));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9503));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9504));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9504));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9505));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9506));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9507));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9508));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9509));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9509));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9510));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9511));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9512));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9512));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9584));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9585));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9587));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9588));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9589));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9590));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9591));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9591));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9592));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9593));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9607));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9608));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9609));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("4daf9344-6f4d-445e-9e54-5702cd64f3ff"), null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9098), null, null, null, null, null, null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9098), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("50ae4a38-d28b-4841-92ec-92247ceba8f0"), null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9121), null, null, null, null, null, null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9121), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("62383107-04db-447f-9fff-f7deae730bc2"), null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9106), null, null, null, null, null, null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9105), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8d938884-9741-4161-92fd-d1d4f042eaae"), null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9128), null, null, null, null, null, null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9128), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ceb9101a-c9ab-47a6-b9d1-1070803b5589"), null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9113), null, null, null, null, null, null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9112), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f9f3be22-da64-49f4-acd6-1743df087401"), null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9090), null, null, null, null, null, null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9090), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("fea568a2-de30-4763-a70f-c544594cad75"), null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9076), null, null, null, null, null, null, new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(9072), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 22, 20, 11, 51, 431, DateTimeKind.Utc).AddTicks(8628));

            migrationBuilder.CreateIndex(
                name: "IX_AssetAdmissions_AdmissionDate",
                table: "AssetAdmissions",
                column: "AdmissionDate");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAdmissions_AdmissionNumber",
                table: "AssetAdmissions",
                column: "AdmissionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetAdmissions_AdmissionType",
                table: "AssetAdmissions",
                column: "AdmissionType");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAdmissions_AdmittedById",
                table: "AssetAdmissions",
                column: "AdmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAdmissions_AssetId",
                table: "AssetAdmissions",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAdmissions_JobCardId",
                table: "AssetAdmissions",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAdmissions_Status",
                table: "AssetAdmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAdmissions_TenantId",
                table: "AssetAdmissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAdmissions_WorkOrderId",
                table: "AssetAdmissions",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_AcceptedById",
                table: "AssetDischarges",
                column: "AcceptedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_AdmissionId",
                table: "AssetDischarges",
                column: "AdmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_AssetId",
                table: "AssetDischarges",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_CustomerAcceptance",
                table: "AssetDischarges",
                column: "CustomerAcceptance");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_DischargeDate",
                table: "AssetDischarges",
                column: "DischargeDate");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_DischargedById",
                table: "AssetDischarges",
                column: "DischargedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_DischargeNumber",
                table: "AssetDischarges",
                column: "DischargeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_JobCardId",
                table: "AssetDischarges",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_QualityCheckedById",
                table: "AssetDischarges",
                column: "QualityCheckedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_TenantId",
                table: "AssetDischarges",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDischarges_WorkOrderId",
                table: "AssetDischarges",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaintenanceDowntimes_AdmissionId",
                table: "AssetMaintenanceDowntimes",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaintenanceDowntimes_AssetId",
                table: "AssetMaintenanceDowntimes",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaintenanceDowntimes_DischargeId",
                table: "AssetMaintenanceDowntimes",
                column: "DischargeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaintenanceDowntimes_DowntimeStart",
                table: "AssetMaintenanceDowntimes",
                column: "DowntimeStart");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaintenanceDowntimes_JobCardId",
                table: "AssetMaintenanceDowntimes",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaintenanceDowntimes_Status",
                table: "AssetMaintenanceDowntimes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaintenanceDowntimes_TenantId",
                table: "AssetMaintenanceDowntimes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaintenanceDowntimes_WorkOrderId",
                table: "AssetMaintenanceDowntimes",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCertificate_AssetDischargeId",
                table: "MaintenanceCertificate",
                column: "AssetDischargeId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCertificate_AssetId",
                table: "MaintenanceCertificate",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCertificate_CertificateNumber",
                table: "MaintenanceCertificate",
                column: "CertificateNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCertificate_DischargeId",
                table: "MaintenanceCertificate",
                column: "DischargeId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCertificate_IssuedById",
                table: "MaintenanceCertificate",
                column: "IssuedById");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCertificate_IssuedDate",
                table: "MaintenanceCertificate",
                column: "IssuedDate");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCertificate_TenantId",
                table: "MaintenanceCertificate",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderQualityChecks_WorkOrders_WorkOrderId",
                table: "WorkOrderQualityChecks",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderQualityChecks_WorkOrders_WorkOrderId",
                table: "WorkOrderQualityChecks");

            migrationBuilder.DropTable(
                name: "AssetMaintenanceDowntimes");

            migrationBuilder.DropTable(
                name: "MaintenanceCertificate");

            migrationBuilder.DropTable(
                name: "AssetDischarges");

            migrationBuilder.DropTable(
                name: "AssetAdmissions");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4daf9344-6f4d-445e-9e54-5702cd64f3ff"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("50ae4a38-d28b-4841-92ec-92247ceba8f0"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("62383107-04db-447f-9fff-f7deae730bc2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8d938884-9741-4161-92fd-d1d4f042eaae"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ceb9101a-c9ab-47a6-b9d1-1070803b5589"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f9f3be22-da64-49f4-acd6-1743df087401"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("fea568a2-de30-4763-a70f-c544594cad75"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1497));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1551));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1563));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1566));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1822));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1834));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1841));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1847));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1858));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1866));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1873));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1880));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1890));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1909));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1917));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1923));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1932));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1951));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1957));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1963));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2015));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2017));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2018));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2020));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2021));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2022));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2023));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2024));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2025));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2027));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2027));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2028));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2029));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2030));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2031));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2032));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2079));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2081));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2082));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2083));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2084));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2084));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2085));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2086));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2087));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2087));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2088));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2089));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2090));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2090));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2091));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2154));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2155));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2157));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2157));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2158));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2159));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2160));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2161));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2161));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2170));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2182));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2183));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(2184));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("2f48d96c-f03f-4656-84a5-6b925cae4cdb"), null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1722), null, null, null, null, null, null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1721), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4cd77c3e-d16a-4806-84fd-b28559ede347"), null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1639), null, null, null, null, null, null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1636), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5493ae24-69eb-4084-a655-f8149dd9339b"), null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1706), null, null, null, null, null, null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1706), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8aa776b1-2759-43de-9fda-4e283740c275"), null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1674), null, null, null, null, null, null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1674), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("9c9b954b-e74c-46a3-9334-5290c3b82676"), null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1691), null, null, null, null, null, null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1691), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ba70063d-f1a4-43ce-bedb-fb898140c74e"), null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1737), null, null, null, null, null, null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1737), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("cf7ff630-3ca4-42ce-997a-85a3de2fb82a"), null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1658), null, null, null, null, null, null, new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1657), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 8, 24, 21, 66, DateTimeKind.Utc).AddTicks(1223));
        }
    }
}
