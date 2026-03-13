using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260213_AddEhcKnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EhcKnowledgeBaseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_EhcKnowledgeBaseCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcKnowledgeBaseCategories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EhcTicketWatchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_EhcTicketWatchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcTicketWatchers_EhcTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "EhcTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcTicketWatchers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcTicketWatchers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EhcKnowledgeBaseArticles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TagsCsv = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsInternalOnly = table.Column<bool>(type: "bit", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EhcKnowledgeBaseArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcKnowledgeBaseArticles_EhcKnowledgeBaseCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "EhcKnowledgeBaseCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcKnowledgeBaseArticles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EhcKnowledgeBaseArticles_CategoryId",
                table: "EhcKnowledgeBaseArticles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcKnowledgeBaseArticles_TenantId_CategoryId",
                table: "EhcKnowledgeBaseArticles",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcKnowledgeBaseArticles_TenantId_Code",
                table: "EhcKnowledgeBaseArticles",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcKnowledgeBaseArticles_TenantId_IsInternalOnly",
                table: "EhcKnowledgeBaseArticles",
                columns: new[] { "TenantId", "IsInternalOnly" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcKnowledgeBaseArticles_TenantId_IsPublished",
                table: "EhcKnowledgeBaseArticles",
                columns: new[] { "TenantId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcKnowledgeBaseCategories_TenantId_Code",
                table: "EhcKnowledgeBaseCategories",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcKnowledgeBaseCategories_TenantId_IsActive",
                table: "EhcKnowledgeBaseCategories",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcTicketWatchers_TenantId_TicketId",
                table: "EhcTicketWatchers",
                columns: new[] { "TenantId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcTicketWatchers_TenantId_TicketId_UserId",
                table: "EhcTicketWatchers",
                columns: new[] { "TenantId", "TicketId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcTicketWatchers_TicketId",
                table: "EhcTicketWatchers",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcTicketWatchers_UserId",
                table: "EhcTicketWatchers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EhcKnowledgeBaseArticles");

            migrationBuilder.DropTable(
                name: "EhcTicketWatchers");

            migrationBuilder.DropTable(
                name: "EhcKnowledgeBaseCategories");
        }
    }
}
