using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260213_AddEhcProblemManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EhcProblems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProblemNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubcategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedFromTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RootCauseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RootCauseDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolutionSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_EhcProblems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcProblems_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcProblems_EhcRootCauseCodes_RootCauseId",
                        column: x => x.RootCauseId,
                        principalTable: "EhcRootCauseCodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcProblems_EhcTicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "EhcTicketCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcProblems_EhcTicketCategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "EhcTicketCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcProblems_EhcTickets_CreatedFromTicketId",
                        column: x => x.CreatedFromTicketId,
                        principalTable: "EhcTickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcProblems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcProblems_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EhcCapaTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProblemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_EhcCapaTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcCapaTasks_Departments_AssignedDepartmentId",
                        column: x => x.AssignedDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcCapaTasks_EhcProblems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "EhcProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcCapaTasks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcCapaTasks_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EhcProblemAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProblemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_EhcProblemAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcProblemAuditEvents_EhcProblems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "EhcProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcProblemAuditEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EhcProblemAuditEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EhcProblemTicketLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProblemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_EhcProblemTicketLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcProblemTicketLinks_EhcProblems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "EhcProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EhcProblemTicketLinks_EhcTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "EhcTickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EhcProblemTicketLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EhcCapaTasks_AssignedDepartmentId",
                table: "EhcCapaTasks",
                column: "AssignedDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcCapaTasks_AssignedToUserId",
                table: "EhcCapaTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcCapaTasks_ProblemId",
                table: "EhcCapaTasks",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcCapaTasks_TenantId_AssignedDepartmentId",
                table: "EhcCapaTasks",
                columns: new[] { "TenantId", "AssignedDepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcCapaTasks_TenantId_AssignedToUserId",
                table: "EhcCapaTasks",
                columns: new[] { "TenantId", "AssignedToUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcCapaTasks_TenantId_DueAt",
                table: "EhcCapaTasks",
                columns: new[] { "TenantId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcCapaTasks_TenantId_ProblemId",
                table: "EhcCapaTasks",
                columns: new[] { "TenantId", "ProblemId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcCapaTasks_TenantId_Status",
                table: "EhcCapaTasks",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblemAuditEvents_ActorUserId",
                table: "EhcProblemAuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblemAuditEvents_ProblemId",
                table: "EhcProblemAuditEvents",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblemAuditEvents_TenantId_ActorUserId",
                table: "EhcProblemAuditEvents",
                columns: new[] { "TenantId", "ActorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblemAuditEvents_TenantId_EventType",
                table: "EhcProblemAuditEvents",
                columns: new[] { "TenantId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblemAuditEvents_TenantId_ProblemId",
                table: "EhcProblemAuditEvents",
                columns: new[] { "TenantId", "ProblemId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_CategoryId",
                table: "EhcProblems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_CreatedFromTicketId",
                table: "EhcProblems",
                column: "CreatedFromTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_DepartmentId",
                table: "EhcProblems",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_OwnerUserId",
                table: "EhcProblems",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_RootCauseId",
                table: "EhcProblems",
                column: "RootCauseId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_SubcategoryId",
                table: "EhcProblems",
                column: "SubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_TenantId_DepartmentId",
                table: "EhcProblems",
                columns: new[] { "TenantId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_TenantId_OwnerUserId",
                table: "EhcProblems",
                columns: new[] { "TenantId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_TenantId_Priority",
                table: "EhcProblems",
                columns: new[] { "TenantId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_TenantId_ProblemNumber",
                table: "EhcProblems",
                columns: new[] { "TenantId", "ProblemNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblems_TenantId_Status",
                table: "EhcProblems",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblemTicketLinks_ProblemId",
                table: "EhcProblemTicketLinks",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblemTicketLinks_TenantId_ProblemId",
                table: "EhcProblemTicketLinks",
                columns: new[] { "TenantId", "ProblemId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblemTicketLinks_TenantId_ProblemId_TicketId",
                table: "EhcProblemTicketLinks",
                columns: new[] { "TenantId", "ProblemId", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblemTicketLinks_TenantId_TicketId",
                table: "EhcProblemTicketLinks",
                columns: new[] { "TenantId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcProblemTicketLinks_TicketId",
                table: "EhcProblemTicketLinks",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EhcCapaTasks");

            migrationBuilder.DropTable(
                name: "EhcProblemAuditEvents");

            migrationBuilder.DropTable(
                name: "EhcProblemTicketLinks");

            migrationBuilder.DropTable(
                name: "EhcProblems");
        }
    }
}
