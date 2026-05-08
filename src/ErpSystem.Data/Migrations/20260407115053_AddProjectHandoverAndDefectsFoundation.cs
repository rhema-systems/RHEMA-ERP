using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectHandoverAndDefectsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectCommissioningItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SystemArea = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequiresRegulatoryInspection = table.Column<bool>(type: "bit", nullable: false),
                    PlannedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CertificateReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponsibleParty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ProjectCommissioningItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCommissioningItems_ProjectUnits_ProjectUnitId",
                        column: x => x.ProjectUnitId,
                        principalTable: "ProjectUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ProjectCommissioningItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectCommissioningItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDefectLiabilityCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReportedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TargetResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsWarrantyRelated = table.Column<bool>(type: "bit", nullable: false),
                    WarrantyExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RectificationCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ChargeableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
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
                    table.PrimaryKey("PK_ProjectDefectLiabilityCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDefectLiabilityCases_ProjectUnits_ProjectUnitId",
                        column: x => x.ProjectUnitId,
                        principalTable: "ProjectUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ProjectDefectLiabilityCases_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectDefectLiabilityCases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectHandoverItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandoverType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResponsibleParty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProjectHandoverItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectHandoverItems_ProjectUnits_ProjectUnitId",
                        column: x => x.ProjectUnitId,
                        principalTable: "ProjectUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ProjectHandoverItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectHandoverItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSnagItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReportedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TargetClosureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RaisedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponsibleParty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ProjectSnagItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSnagItems_ProjectUnits_ProjectUnitId",
                        column: x => x.ProjectUnitId,
                        principalTable: "ProjectUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ProjectSnagItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectSnagItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCommissioningItems_ProjectId_ProjectUnitId",
                table: "ProjectCommissioningItems",
                columns: new[] { "ProjectId", "ProjectUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCommissioningItems_ProjectId_SortOrder",
                table: "ProjectCommissioningItems",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCommissioningItems_ProjectId_Status",
                table: "ProjectCommissioningItems",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCommissioningItems_ProjectUnitId",
                table: "ProjectCommissioningItems",
                column: "ProjectUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCommissioningItems_TenantId",
                table: "ProjectCommissioningItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_CustomerBusinessPartnerId",
                table: "ProjectDefectLiabilityCases",
                columns: new[] { "ProjectId", "CustomerBusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_ProjectUnitId",
                table: "ProjectDefectLiabilityCases",
                columns: new[] { "ProjectId", "ProjectUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_Status",
                table: "ProjectDefectLiabilityCases",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectId_TargetResolutionDate",
                table: "ProjectDefectLiabilityCases",
                columns: new[] { "ProjectId", "TargetResolutionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_ProjectUnitId",
                table: "ProjectDefectLiabilityCases",
                column: "ProjectUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDefectLiabilityCases_TenantId",
                table: "ProjectDefectLiabilityCases",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverItems_ProjectId_HandoverType",
                table: "ProjectHandoverItems",
                columns: new[] { "ProjectId", "HandoverType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverItems_ProjectId_ProjectUnitId",
                table: "ProjectHandoverItems",
                columns: new[] { "ProjectId", "ProjectUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverItems_ProjectId_SortOrder",
                table: "ProjectHandoverItems",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverItems_ProjectId_Status",
                table: "ProjectHandoverItems",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverItems_ProjectUnitId",
                table: "ProjectHandoverItems",
                column: "ProjectUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverItems_TenantId",
                table: "ProjectHandoverItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnagItems_ProjectId_ProjectUnitId",
                table: "ProjectSnagItems",
                columns: new[] { "ProjectId", "ProjectUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnagItems_ProjectId_Severity",
                table: "ProjectSnagItems",
                columns: new[] { "ProjectId", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnagItems_ProjectId_Status",
                table: "ProjectSnagItems",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnagItems_ProjectId_TargetClosureDate",
                table: "ProjectSnagItems",
                columns: new[] { "ProjectId", "TargetClosureDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnagItems_ProjectUnitId",
                table: "ProjectSnagItems",
                column: "ProjectUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnagItems_TenantId",
                table: "ProjectSnagItems",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectCommissioningItems");

            migrationBuilder.DropTable(
                name: "ProjectDefectLiabilityCases");

            migrationBuilder.DropTable(
                name: "ProjectHandoverItems");

            migrationBuilder.DropTable(
                name: "ProjectSnagItems");
        }
    }
}
