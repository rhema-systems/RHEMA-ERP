using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenderLotSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("026e0d59-8617-4a65-9c31-7786e5a52a5f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0f76bcfe-d331-424d-8427-b7717ad7a20e"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4903c903-2ad5-45b4-8ae6-4afaf674f15b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6ec7a0fd-0e17-4afe-9576-9135387c51e5"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7eedf35e-27ea-494f-b428-ec1f9506263f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8e2b46a2-f68b-4ffc-a20a-e70eb7c07c3b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f243bdc0-8fe6-4c17-b407-0d488b493325"));

            migrationBuilder.AddColumn<Guid>(
                name: "LotId",
                table: "TenderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BidLotId",
                table: "TenderBidItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BidLotId",
                table: "TenderAwards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LotId",
                table: "TenderAwards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenderLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotNumber = table.Column<int>(type: "int", nullable: false),
                    LotCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiredDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveryLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Specifications = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TenderLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderLots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderLots_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenderBidLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalLotAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DeliveryDays = table.Column<int>(type: "int", nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WarrantyTerms = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TechnicalProposal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommercialProposal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PriceScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QualityScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    DeliveryScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TotalScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    EvaluationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_TenderBidLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderBidLots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderBidLots_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenderBidLots_TenderLots_LotId",
                        column: x => x.LotId,
                        principalTable: "TenderLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(824));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(898));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(901));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(903));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1128));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1140));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1148));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1170));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1181));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1188));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1195));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1200));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1211));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1221));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1228));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1235));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1245));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1270));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1277));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1283));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1342));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1344));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1345));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1346));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1347));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1349));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1350));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1351));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1352));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1353));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1354));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1355));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1356));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1356));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1357));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1358));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1415));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1417));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1418));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1419));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1419));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1420));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1421));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1422));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1423));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1439));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1441));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1517));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1518));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1520));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1521));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1522));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1522));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1523));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1524));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1525));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1525));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1538));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1540));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1540));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("35a6ce17-8326-42de-959d-f3fa08600e69"), null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1009), null, null, null, null, null, null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1008), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("79a4bed4-93fa-4ae4-9b1a-6745c4a5cf29"), null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1025), null, null, null, null, null, null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1024), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8153cbb4-3507-439a-93fa-61356ca44edf"), null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(984), null, null, null, null, null, null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(984), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("9640b56b-c46c-4a7a-a3c8-526fff571e30"), null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(972), null, null, null, null, null, null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(966), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("dc723816-6369-43e7-ae7c-5d77fad719c1"), null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(993), null, null, null, null, null, null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(993), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("dd4838dc-f934-4ee7-b128-fa066aa07b3f"), null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1001), null, null, null, null, null, null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1000), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ef472f6d-6ce0-4325-b9ea-eed0d9b88ab4"), null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1018), null, null, null, null, null, null, new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(1018), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 0, 14, 39, 923, DateTimeKind.Utc).AddTicks(477));

            migrationBuilder.CreateIndex(
                name: "IX_TenderItems_LotId",
                table: "TenderItems",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidItems_BidLotId",
                table: "TenderBidItems",
                column: "BidLotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_BidLotId",
                table: "TenderAwards",
                column: "BidLotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_LotId",
                table: "TenderAwards",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_LotId",
                table: "TenderBidLots",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_Status",
                table: "TenderBidLots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_TenantId",
                table: "TenderBidLots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_TenderBidId",
                table: "TenderBidLots",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidLots_TenderBidId_LotId",
                table: "TenderBidLots",
                columns: new[] { "TenderBidId", "LotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderLots_Status",
                table: "TenderLots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenderLots_TenantId",
                table: "TenderLots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderLots_TenderId",
                table: "TenderLots",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderLots_TenderId_LotCode",
                table: "TenderLots",
                columns: new[] { "TenderId", "LotCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_TenderBidLots_BidLotId",
                table: "TenderAwards",
                column: "BidLotId",
                principalTable: "TenderBidLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwards_TenderLots_LotId",
                table: "TenderAwards",
                column: "LotId",
                principalTable: "TenderLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidItems_TenderBidLots_BidLotId",
                table: "TenderBidItems",
                column: "BidLotId",
                principalTable: "TenderBidLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderItems_TenderLots_LotId",
                table: "TenderItems",
                column: "LotId",
                principalTable: "TenderLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_TenderBidLots_BidLotId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwards_TenderLots_LotId",
                table: "TenderAwards");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidItems_TenderBidLots_BidLotId",
                table: "TenderBidItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderItems_TenderLots_LotId",
                table: "TenderItems");

            migrationBuilder.DropTable(
                name: "TenderBidLots");

            migrationBuilder.DropTable(
                name: "TenderLots");

            migrationBuilder.DropIndex(
                name: "IX_TenderItems_LotId",
                table: "TenderItems");

            migrationBuilder.DropIndex(
                name: "IX_TenderBidItems_BidLotId",
                table: "TenderBidItems");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwards_BidLotId",
                table: "TenderAwards");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwards_LotId",
                table: "TenderAwards");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("35a6ce17-8326-42de-959d-f3fa08600e69"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("79a4bed4-93fa-4ae4-9b1a-6745c4a5cf29"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8153cbb4-3507-439a-93fa-61356ca44edf"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("9640b56b-c46c-4a7a-a3c8-526fff571e30"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("dc723816-6369-43e7-ae7c-5d77fad719c1"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("dd4838dc-f934-4ee7-b128-fa066aa07b3f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ef472f6d-6ce0-4325-b9ea-eed0d9b88ab4"));

            migrationBuilder.DropColumn(
                name: "LotId",
                table: "TenderItems");

            migrationBuilder.DropColumn(
                name: "BidLotId",
                table: "TenderBidItems");

            migrationBuilder.DropColumn(
                name: "BidLotId",
                table: "TenderAwards");

            migrationBuilder.DropColumn(
                name: "LotId",
                table: "TenderAwards");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(578));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(685));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(692));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(696));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2571));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2623));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2641));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2652));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2679));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2702));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2711));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2720));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2735));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2799));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2816));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2836));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2855));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2879));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2898));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2906));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3041));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3046));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3048));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3050));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3052));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3055));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3057));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3059));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3061));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3082));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3084));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3085));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3087));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3089));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3091));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3093));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3177));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3181));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3183));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3184));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3186));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3188));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3190));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3192));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3193));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3195));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3197));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3199));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3201));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3202));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3204));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3324));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3326));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3329));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3331));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3333));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3334));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3336));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3338));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3340));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3342));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3367));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3369));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(3371));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("026e0d59-8617-4a65-9c31-7786e5a52a5f"), null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(836), null, null, null, null, null, null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(829), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("0f76bcfe-d331-424d-8427-b7717ad7a20e"), null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2177), null, null, null, null, null, null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2176), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4903c903-2ad5-45b4-8ae6-4afaf674f15b"), null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(876), null, null, null, null, null, null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(875), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6ec7a0fd-0e17-4afe-9576-9135387c51e5"), null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2272), null, null, null, null, null, null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2271), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7eedf35e-27ea-494f-b428-ec1f9506263f"), null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2303), null, null, null, null, null, null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2302), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8e2b46a2-f68b-4ffc-a20a-e70eb7c07c3b"), null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2237), null, null, null, null, null, null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(2236), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f243bdc0-8fe6-4c17-b407-0d488b493325"), null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(906), null, null, null, null, null, null, new DateTime(2025, 12, 28, 3, 1, 57, 614, DateTimeKind.Utc).AddTicks(905), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 3, 1, 57, 613, DateTimeKind.Utc).AddTicks(9938));
        }
    }
}
