using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260213_AddEhcTicketLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EhcTicketLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelatedTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkType = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_EhcTicketLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcTicketLinks_EhcTickets_RelatedTicketId",
                        column: x => x.RelatedTicketId,
                        principalTable: "EhcTickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcTicketLinks_EhcTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "EhcTickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcTicketLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EhcTicketLinks_RelatedTicketId",
                table: "EhcTicketLinks",
                column: "RelatedTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcTicketLinks_TenantId_RelatedTicketId",
                table: "EhcTicketLinks",
                columns: new[] { "TenantId", "RelatedTicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcTicketLinks_TenantId_TicketId",
                table: "EhcTicketLinks",
                columns: new[] { "TenantId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcTicketLinks_TenantId_TicketId_RelatedTicketId_LinkType",
                table: "EhcTicketLinks",
                columns: new[] { "TenantId", "TicketId", "RelatedTicketId", "LinkType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcTicketLinks_TicketId",
                table: "EhcTicketLinks",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EhcTicketLinks");
        }
    }
}
