using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitorEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Competitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Strengths = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Weaknesses = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KeyProducts = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PricingStrategy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstimatedMarketShare = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ThreatLevel = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompetitorDeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompetitorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpportunityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ThreatLevel = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    DealValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CompetitorProposal = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OurDifferentiator = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LessonsLearned = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReportedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitorDeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitorDeals_Competitors_CompetitorId",
                        column: x => x.CompetitorId,
                        principalTable: "Competitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitorDeals_CompetitorId",
                table: "CompetitorDeals",
                column: "CompetitorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitorDeals");

            migrationBuilder.DropTable(
                name: "Competitors");
        }
    }
}
