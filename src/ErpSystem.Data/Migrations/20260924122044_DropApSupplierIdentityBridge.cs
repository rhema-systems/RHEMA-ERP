using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Removes the transitional AP supplier-to-Business-Partner identity bridge after
    /// AP invoices, payments, debit notes and WHT records became canonical consumers
    /// of BusinessPartnerId and governed Business Partner finance profiles.
    /// </summary>
    public partial class DropApSupplierIdentityBridge : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ApSupplierIdentityLinks");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApSupplierIdentityLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MappingSource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApSupplierIdentityLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApSupplierIdentityLinks_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApSupplierIdentityLinks_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApSupplierIdentityLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApSupplierIdentityLinks_BusinessPartnerId",
                table: "ApSupplierIdentityLinks",
                column: "BusinessPartnerId");
            migrationBuilder.CreateIndex(
                name: "IX_ApSupplierIdentityLinks_SupplierId",
                table: "ApSupplierIdentityLinks",
                column: "SupplierId");
            migrationBuilder.CreateIndex(
                name: "UX_ApSupplierIdentityLinks_Tenant_BusinessPartner",
                table: "ApSupplierIdentityLinks",
                columns: new[] { "TenantId", "BusinessPartnerId" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "UX_ApSupplierIdentityLinks_Tenant_Supplier",
                table: "ApSupplierIdentityLinks",
                columns: new[] { "TenantId", "SupplierId" },
                unique: true);
        }
    }
}
