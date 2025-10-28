using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCardAdmissionCompletionAndCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("28e4e204-5bdf-4daa-b984-b834afb0b670"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("34ccaf3d-1e5a-4337-a0b7-d763a9883e2a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("3af1ba05-dd45-425e-8e14-f60d4baf506c"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4b167478-e799-4d5b-ba45-b4e6276b43b6"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("548eb3d0-2941-4974-952b-64cf083cd787"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5f221f20-71de-42f5-a1d5-6ab30851d872"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("79685966-f621-497a-93fd-45da712973cf"));

            migrationBuilder.AddColumn<string>(
                name: "AcceptanceNotes",
                table: "JobCard",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedById",
                table: "JobCard",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedDate",
                table: "JobCard",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdmissionNotes",
                table: "JobCard",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssetConditionOnAdmission",
                table: "JobCard",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssetConditionOnCompletion",
                table: "JobCard",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BayOrStation",
                table: "JobCard",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CertificateGenerated",
                table: "JobCard",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CertificateGeneratedDate",
                table: "JobCard",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedDate",
                table: "JobCard",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionNotes",
                table: "JobCard",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CustomerAcceptance",
                table: "JobCard",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpDate",
                table: "JobCard",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpInstructions",
                table: "JobCard",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FuelLevelOnAdmission",
                table: "JobCard",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FuelLevelOnCompletion",
                table: "JobCard",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HoursReadingOnAdmission",
                table: "JobCard",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HoursReadingOnCompletion",
                table: "JobCard",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MileageReadingOnAdmission",
                table: "JobCard",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MileageReadingOnCompletion",
                table: "JobCard",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QualityCheckDate",
                table: "JobCard",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityCheckNotes",
                table: "JobCard",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QualityCheckPassed",
                table: "JobCard",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "QualityCheckedById",
                table: "JobCard",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemainingIssues",
                table: "JobCard",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresFollowUp",
                table: "JobCard",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyDays",
                table: "JobCard",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarrantyExpiration",
                table: "JobCard",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyTerms",
                table: "JobCard",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkCompletedSummary",
                table: "JobCard",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobCardCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_JobCardCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobCardCertificates_Employees_IssuedById",
                        column: x => x.IssuedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobCardCertificates_JobCard_JobCardId",
                        column: x => x.JobCardId,
                        principalTable: "JobCard",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobCardCertificates_MaintenanceAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobCardCertificates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(5980));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6018));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6020));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6022));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6272));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6281));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6287));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6293));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6312));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6321));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6327));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6331));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6339));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6348));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6353));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6358));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6365));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6371));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6375));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6379));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6429));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6431));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6432));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6432));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6433));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6434));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6435));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6436));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6437));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6438));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6439));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6441));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6564));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6565));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6566));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6567));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6567));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6568));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6569));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6569));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6570));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6571));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6572));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6572));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6573));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6574));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6580));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6644));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6645));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6647));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6647));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6648));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6649));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6649));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6650));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6650));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6651));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6663));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6664));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6665));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("0d85b5d0-cde9-44cd-ae5b-6b31c04a051c"), null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6152), null, null, null, null, null, null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6152), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("39385bb2-28a7-4149-a389-047fdd24337b"), null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6118), null, null, null, null, null, null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6118), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4f178edf-88f4-406d-b6dd-b5db1a4ae757"), null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6135), null, null, null, null, null, null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6135), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("694b6884-d254-42ed-809f-6d5b1ee54023"), null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6189), null, null, null, null, null, null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6189), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8c6cd849-96e0-4d71-a108-e27d97f9f22e"), null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6097), null, null, null, null, null, null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6094), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c1c289a3-9e01-4c43-ab55-fa29b00c5974"), null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6172), null, null, null, null, null, null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6171), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("fe954e9a-6fc2-44fe-9a56-ce9b650d3ad9"), null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6205), null, null, null, null, null, null, new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(6205), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 11, 41, 23, 263, DateTimeKind.Utc).AddTicks(5716));

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_AcceptedById",
                table: "JobCard",
                column: "AcceptedById");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_QualityCheckedById",
                table: "JobCard",
                column: "QualityCheckedById");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardCertificates_AssetId",
                table: "JobCardCertificates",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardCertificates_IssuedById",
                table: "JobCardCertificates",
                column: "IssuedById");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardCertificates_JobCardId",
                table: "JobCardCertificates",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardCertificates_TenantId",
                table: "JobCardCertificates",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_Employees_AcceptedById",
                table: "JobCard",
                column: "AcceptedById",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_Employees_QualityCheckedById",
                table: "JobCard",
                column: "QualityCheckedById",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_Employees_AcceptedById",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_Employees_QualityCheckedById",
                table: "JobCard");

            migrationBuilder.DropTable(
                name: "JobCardCertificates");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_AcceptedById",
                table: "JobCard");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_QualityCheckedById",
                table: "JobCard");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0d85b5d0-cde9-44cd-ae5b-6b31c04a051c"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("39385bb2-28a7-4149-a389-047fdd24337b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4f178edf-88f4-406d-b6dd-b5db1a4ae757"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("694b6884-d254-42ed-809f-6d5b1ee54023"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8c6cd849-96e0-4d71-a108-e27d97f9f22e"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c1c289a3-9e01-4c43-ab55-fa29b00c5974"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("fe954e9a-6fc2-44fe-9a56-ce9b650d3ad9"));

            migrationBuilder.DropColumn(
                name: "AcceptanceNotes",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "AcceptedById",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "AcceptedDate",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "AdmissionNotes",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "AssetConditionOnAdmission",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "AssetConditionOnCompletion",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "BayOrStation",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "CertificateGenerated",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "CertificateGeneratedDate",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "CompletedDate",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "CompletionNotes",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "CustomerAcceptance",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "FollowUpDate",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "FollowUpInstructions",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "FuelLevelOnAdmission",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "FuelLevelOnCompletion",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "HoursReadingOnAdmission",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "HoursReadingOnCompletion",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "MileageReadingOnAdmission",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "MileageReadingOnCompletion",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "QualityCheckDate",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "QualityCheckNotes",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "QualityCheckPassed",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "QualityCheckedById",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "RemainingIssues",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "RequiresFollowUp",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "WarrantyDays",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "WarrantyExpiration",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "WarrantyTerms",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "WorkCompletedSummary",
                table: "JobCard");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(384));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(443));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(446));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(448));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(768));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(792));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(799));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(806));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(824));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(832));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(839));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(847));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(856));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(878));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(885));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(892));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(901));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(915));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(925));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(933));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(981));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(989));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(991));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(992));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(993));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(995));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(995));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(997));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(997));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(999));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1000));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1000));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1001));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1002));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1003));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1004));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1112));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1114));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1115));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1116));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1117));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1118));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1119));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1120));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1120));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1121));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1122));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1123));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1124));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1125));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1125));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1202));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1203));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1205));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1206));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1207));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1208));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1209));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1210));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1211));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1211));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1226));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1227));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(1228));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("28e4e204-5bdf-4daa-b984-b834afb0b670"), null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(589), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(588), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("34ccaf3d-1e5a-4337-a0b7-d763a9883e2a"), null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(559), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(558), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("3af1ba05-dd45-425e-8e14-f60d4baf506c"), null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(654), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(653), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4b167478-e799-4d5b-ba45-b4e6276b43b6"), null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(632), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(631), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("548eb3d0-2941-4974-952b-64cf083cd787"), null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(611), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(610), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5f221f20-71de-42f5-a1d5-6ab30851d872"), null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(676), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(676), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("79685966-f621-497a-93fd-45da712973cf"), null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(532), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(528), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 52, 1, 124, DateTimeKind.Utc).AddTicks(159));
        }
    }
}
