using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernedPrimaryBookReplacementAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PrimaryReplacementEffectiveDate",
                table: "AccountingBooks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryReplacementFromBookId",
                table: "AccountingBooks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryReplacementReason",
                table: "AccountingBooks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrimaryReplacementRequestedAtUtc",
                table: "AccountingBooks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryReplacementRequestedByUserId",
                table: "AccountingBooks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryReplacementWorkflowInstanceId",
                table: "AccountingBooks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountingBookPrimaryDesignations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousPrimaryBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewPrimaryBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AccountingBookPrimaryDesignations", x => x.Id);
                    table.CheckConstraint("CK_AccountingBookPrimaryDesignations_DifferentBooks", "[PreviousPrimaryBookId] <> [NewPrimaryBookId]");
                    table.CheckConstraint("CK_AccountingBookPrimaryDesignations_NoDelete", "[IsDeleted] = 0");
                    table.ForeignKey(
                        name: "FK_AccountingBookPrimaryDesignations_AccountingBooks_TenantId_NewPrimaryBookId",
                        columns: x => new { x.TenantId, x.NewPrimaryBookId },
                        principalTable: "AccountingBooks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookPrimaryDesignations_AccountingBooks_TenantId_PreviousPrimaryBookId",
                        columns: x => new { x.TenantId, x.PreviousPrimaryBookId },
                        principalTable: "AccountingBooks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookPrimaryDesignations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookPrimaryDesignations_TenantId_EffectiveFrom",
                table: "AccountingBookPrimaryDesignations",
                columns: new[] { "TenantId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookPrimaryDesignations_TenantId_NewPrimaryBookId",
                table: "AccountingBookPrimaryDesignations",
                columns: new[] { "TenantId", "NewPrimaryBookId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookPrimaryDesignations_TenantId_PreviousPrimaryBookId",
                table: "AccountingBookPrimaryDesignations",
                columns: new[] { "TenantId", "PreviousPrimaryBookId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingBookPrimaryDesignations");

            migrationBuilder.DropColumn(
                name: "PrimaryReplacementEffectiveDate",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "PrimaryReplacementFromBookId",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "PrimaryReplacementReason",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "PrimaryReplacementRequestedAtUtc",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "PrimaryReplacementRequestedByUserId",
                table: "AccountingBooks");

            migrationBuilder.DropColumn(
                name: "PrimaryReplacementWorkflowInstanceId",
                table: "AccountingBooks");
        }
    }
}
