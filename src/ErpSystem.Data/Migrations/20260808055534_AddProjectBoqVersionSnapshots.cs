using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808055534_AddProjectBoqVersionSnapshots")]
public partial class AddProjectBoqVersionSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "VersionLineKey",
            table: "ProjectBoqItems",
            type: "uniqueidentifier",
            nullable: false,
            defaultValueSql: "NEWID()");

        // Existing BoQ rows pre-date versioning. Their primary keys are stable and unique,
        // so use them as the initial lineage keys instead of generating opaque replacements.
        migrationBuilder.Sql(
            "UPDATE [ProjectBoqItems] SET [VersionLineKey] = [Id];",
            suppressTransaction: false);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqItems_TenantId_ProjectId_VersionLineKey",
            table: "ProjectBoqItems",
            columns: new[] { "TenantId", "ProjectId", "VersionLineKey" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "ProjectBoqVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                VersionType = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ChangeSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                AuditAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SnapshotHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                LineCount = table.Column<int>(type: "int", nullable: false),
                SnapshotAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ActorRoles = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                table.PrimaryKey("PK_ProjectBoqVersions", x => x.Id);
                table.CheckConstraint("CK_ProjectBoqVersions_ApprovalStatus", "[ApprovalStatus] IN ('Draft', 'PendingApproval', 'Approved', 'Rejected')");
                table.CheckConstraint("CK_ProjectBoqVersions_LineCount", "[LineCount] >= 0");
                table.CheckConstraint("CK_ProjectBoqVersions_Status", "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'Rejected', 'Retired')");
                table.CheckConstraint("CK_ProjectBoqVersions_VersionNumber", "[VersionNumber] > 0");
                table.CheckConstraint("CK_ProjectBoqVersions_VersionType", "[VersionType] BETWEEN 0 AND 6");
                table.ForeignKey(
                    name: "FK_ProjectBoqVersions_ProjectBoqVersions_SourceVersionId",
                    column: x => x.SourceVersionId,
                    principalTable: "ProjectBoqVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ProjectBoqVersions_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ProjectBoqVersions_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProjectBoqVersionLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectBoqVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LineKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceBoqItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProjectPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PackageCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                PackageName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                SectionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                SectionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                TradeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                TradeName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                CostCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                CostCodeName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                MeasurementStandard = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                MeasurementCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                MeasurementRule = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                LineNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                ItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                ItemType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                UnitRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                LineAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
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
                table.PrimaryKey("PK_ProjectBoqVersionLines", x => x.Id);
                table.CheckConstraint("CK_ProjectBoqVersionLines_LineAmount", "[LineAmount] IS NULL OR [LineAmount] >= 0");
                table.CheckConstraint("CK_ProjectBoqVersionLines_Quantity", "[Quantity] >= 0");
                table.CheckConstraint("CK_ProjectBoqVersionLines_UnitRate", "[UnitRate] IS NULL OR [UnitRate] >= 0");
                table.ForeignKey(
                    name: "FK_ProjectBoqVersionLines_ProjectBoqVersions_ProjectBoqVersionId",
                    column: x => x.ProjectBoqVersionId,
                    principalTable: "ProjectBoqVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectBoqVersionLines_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqVersions_ProjectId",
            table: "ProjectBoqVersions",
            column: "ProjectId");
        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqVersions_SourceVersionId",
            table: "ProjectBoqVersions",
            column: "SourceVersionId");
        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqVersions_TenantId_ProjectId_Status",
            table: "ProjectBoqVersions",
            columns: new[] { "TenantId", "ProjectId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqVersions_TenantId_ProjectId_VersionNumber",
            table: "ProjectBoqVersions",
            columns: new[] { "TenantId", "ProjectId", "VersionNumber" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqVersions_TenantId_ProjectId_VersionType_SnapshotAt",
            table: "ProjectBoqVersions",
            columns: new[] { "TenantId", "ProjectId", "VersionType", "SnapshotAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqVersionLines_ProjectBoqVersionId",
            table: "ProjectBoqVersionLines",
            column: "ProjectBoqVersionId");
        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqVersionLines_TenantId_ProjectBoqVersionId_LineKey",
            table: "ProjectBoqVersionLines",
            columns: new[] { "TenantId", "ProjectBoqVersionId", "LineKey" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqVersionLines_TenantId_ProjectBoqVersionId_SortOrder",
            table: "ProjectBoqVersionLines",
            columns: new[] { "TenantId", "ProjectBoqVersionId", "SortOrder" });
        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqVersionLines_TenantId_ProjectId_LineKey",
            table: "ProjectBoqVersionLines",
            columns: new[] { "TenantId", "ProjectId", "LineKey" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProjectBoqVersionLines");
        migrationBuilder.DropTable(name: "ProjectBoqVersions");
        migrationBuilder.DropIndex(
            name: "IX_ProjectBoqItems_TenantId_ProjectId_VersionLineKey",
            table: "ProjectBoqItems");
        migrationBuilder.DropColumn(name: "VersionLineKey", table: "ProjectBoqItems");
    }
}
