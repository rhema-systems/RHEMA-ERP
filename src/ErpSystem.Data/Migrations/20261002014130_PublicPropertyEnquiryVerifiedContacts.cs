using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class PublicPropertyEnquiryVerifiedContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicPropertyEnquiryContactId",
                table: "EhcTickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EhcPublicPropertyEnquiryContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NormalizedContact = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastVerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEnquiryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_EhcPublicPropertyEnquiryContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcPublicPropertyEnquiryContacts_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EhcPublicPropertyEnquiryContacts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcPublicPropertyEnquiryVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ContactHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerificationAttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerificationTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_EhcPublicPropertyEnquiryVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcPublicPropertyEnquiryVerifications_EhcPublicPropertyEnquiryContacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "EhcPublicPropertyEnquiryContacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcPublicPropertyEnquiryVerifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EhcTickets_PublicPropertyEnquiryContactId",
                table: "EhcTickets",
                column: "PublicPropertyEnquiryContactId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcTickets_TenantId_PublicPropertyEnquiryContactId",
                table: "EhcTickets",
                columns: new[] { "TenantId", "PublicPropertyEnquiryContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcPublicPropertyEnquiryContacts_BusinessPartnerId",
                table: "EhcPublicPropertyEnquiryContacts",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPublicPropertyEnquiryContacts_TenantId_BusinessPartnerId",
                table: "EhcPublicPropertyEnquiryContacts",
                columns: new[] { "TenantId", "BusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcPublicPropertyEnquiryContacts_TenantId_Channel_NormalizedContact",
                table: "EhcPublicPropertyEnquiryContacts",
                columns: new[] { "TenantId", "Channel", "NormalizedContact" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPublicPropertyEnquiryVerifications_ContactId",
                table: "EhcPublicPropertyEnquiryVerifications",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcPublicPropertyEnquiryVerifications_TenantId_ListingId_Channel_ContactHash_RequestedAtUtc",
                table: "EhcPublicPropertyEnquiryVerifications",
                columns: new[] { "TenantId", "ListingId", "Channel", "ContactHash", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcPublicPropertyEnquiryVerifications_VerificationTokenHash",
                table: "EhcPublicPropertyEnquiryVerifications",
                column: "VerificationTokenHash",
                unique: true,
                filter: "[VerificationTokenHash] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_EhcTickets_EhcPublicPropertyEnquiryContacts_PublicPropertyEnquiryContactId",
                table: "EhcTickets",
                column: "PublicPropertyEnquiryContactId",
                principalTable: "EhcPublicPropertyEnquiryContacts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EhcTickets_EhcPublicPropertyEnquiryContacts_PublicPropertyEnquiryContactId",
                table: "EhcTickets");

            migrationBuilder.DropTable(
                name: "EhcPublicPropertyEnquiryVerifications");

            migrationBuilder.DropTable(
                name: "EhcPublicPropertyEnquiryContacts");

            migrationBuilder.DropIndex(
                name: "IX_EhcTickets_PublicPropertyEnquiryContactId",
                table: "EhcTickets");

            migrationBuilder.DropIndex(
                name: "IX_EhcTickets_TenantId_PublicPropertyEnquiryContactId",
                table: "EhcTickets");

            migrationBuilder.DropColumn(
                name: "PublicPropertyEnquiryContactId",
                table: "EhcTickets");
        }
    }
}
