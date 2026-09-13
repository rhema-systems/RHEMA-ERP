using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUnitizationAndVariations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BlockName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FloorLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AreaSquareMeters = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ValuationRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    HandoverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUnits_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectUnits_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCustomerVariations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    VariationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Timing = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TargetCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QuotedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BilledAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequiresScheduleAdjustment = table.Column<bool>(type: "bit", nullable: false),
                    ScheduleImpactDays = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectCustomerVariations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCustomerVariations_ProjectUnits_ProjectUnitId",
                        column: x => x.ProjectUnitId,
                        principalTable: "ProjectUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ProjectCustomerVariations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectCustomerVariations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_CustomerBusinessPartnerId",
                table: "ProjectCustomerVariations",
                columns: new[] { "ProjectId", "CustomerBusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_ProjectUnitId",
                table: "ProjectCustomerVariations",
                columns: new[] { "ProjectId", "ProjectUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_RequestDate",
                table: "ProjectCustomerVariations",
                columns: new[] { "ProjectId", "RequestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_ProjectId_Status",
                table: "ProjectCustomerVariations",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_ProjectUnitId",
                table: "ProjectCustomerVariations",
                column: "ProjectUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCustomerVariations_TenantId",
                table: "ProjectCustomerVariations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_Code",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_CustomerBusinessPartnerId",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "CustomerBusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_SortOrder",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_Status",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_TenantId",
                table: "ProjectUnits",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectCustomerVariations");

            migrationBuilder.DropTable(
                name: "ProjectUnits");
        }
    }
}
