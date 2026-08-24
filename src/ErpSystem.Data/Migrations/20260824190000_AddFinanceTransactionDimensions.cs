using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the Finance-owned transaction-dimension foundation without changing structural chart-of-
/// account segments or requiring existing posting producers to supply dimensions.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260824190000_AddFinanceTransactionDimensions")]
public class AddFinanceTransactionDimensions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinanceDimensionDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Classification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ValueSourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                SourceEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_FinanceDimensionDefinitions", x => x.Id);
                table.ForeignKey("FK_FinanceDimensionDefinitions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_FinanceDimensionDefinitions_Classification", "[Classification] IN ('Analytical','Balancing','Derived')");
                table.CheckConstraint("CK_FinanceDimensionDefinitions_ValueSourceType", "[ValueSourceType] IN ('Lookup','EntityBacked')");
            });

        migrationBuilder.CreateTable(
            name: "FinanceDimensionSets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CombinationHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DisplayValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_FinanceDimensionSets", x => x.Id);
                table.ForeignKey("FK_FinanceDimensionSets_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FinanceDimensionValues",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ParentValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SourceEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_FinanceDimensionValues", x => x.Id);
                table.ForeignKey("FK_FinanceDimensionValues_FinanceDimensionDefinitions_FinanceDimensionDefinitionId", x => x.FinanceDimensionDefinitionId, "FinanceDimensionDefinitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceDimensionValues_FinanceDimensionValues_ParentValueId", x => x.ParentValueId, "FinanceDimensionValues", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceDimensionValues_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FinanceDimensionSetItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DimensionCodeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                DimensionValueCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                DimensionValueNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_FinanceDimensionSetItems", x => x.Id);
                table.ForeignKey("FK_FinanceDimensionSetItems_FinanceDimensionDefinitions_FinanceDimensionDefinitionId", x => x.FinanceDimensionDefinitionId, "FinanceDimensionDefinitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceDimensionSetItems_FinanceDimensionSets_FinanceDimensionSetId", x => x.FinanceDimensionSetId, "FinanceDimensionSets", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceDimensionSetItems_FinanceDimensionValues_FinanceDimensionValueId", x => x.FinanceDimensionValueId, "FinanceDimensionValues", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceDimensionSetItems_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FinanceDimensionAccountRules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceDimensionDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RuleType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                DefaultDimensionValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_FinanceDimensionAccountRules", x => x.Id);
                table.ForeignKey("FK_FinanceDimensionAccountRules_Accounts_AccountId", x => x.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceDimensionAccountRules_FinanceDimensionDefinitions_FinanceDimensionDefinitionId", x => x.FinanceDimensionDefinitionId, "FinanceDimensionDefinitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceDimensionAccountRules_FinanceDimensionValues_DefaultDimensionValueId", x => x.DefaultDimensionValueId, "FinanceDimensionValues", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FinanceDimensionAccountRules_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_FinanceDimensionAccountRules_RuleType", "[RuleType] IN ('Required','Optional','Prohibited','Fixed')");
            });

        migrationBuilder.AddColumn<Guid>(
            name: "FinanceDimensionSetId",
            table: "AccountTransactions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex("IX_FinanceDimensionDefinitions_TenantId_Code", "FinanceDimensionDefinitions", new[] { "TenantId", "Code" }, unique: true);
        migrationBuilder.CreateIndex("IX_FinanceDimensionDefinitions_TenantId", "FinanceDimensionDefinitions", "TenantId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionValues_FinanceDimensionDefinitionId", "FinanceDimensionValues", "FinanceDimensionDefinitionId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionValues_ParentValueId", "FinanceDimensionValues", "ParentValueId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionValues_TenantId_FinanceDimensionDefinitionId_Code", "FinanceDimensionValues", new[] { "TenantId", "FinanceDimensionDefinitionId", "Code" }, unique: true);
        migrationBuilder.CreateIndex("IX_FinanceDimensionValues_TenantId_FinanceDimensionDefinitionId_SourceEntityType_SourceEntityId", "FinanceDimensionValues", new[] { "TenantId", "FinanceDimensionDefinitionId", "SourceEntityType", "SourceEntityId" }, unique: true, filter: "[SourceEntityId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_FinanceDimensionValues_TenantId", "FinanceDimensionValues", "TenantId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionSets_TenantId_CombinationHash", "FinanceDimensionSets", new[] { "TenantId", "CombinationHash" }, unique: true);
        migrationBuilder.CreateIndex("IX_FinanceDimensionSets_TenantId", "FinanceDimensionSets", "TenantId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionSetItems_FinanceDimensionDefinitionId", "FinanceDimensionSetItems", "FinanceDimensionDefinitionId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionSetItems_FinanceDimensionSetId", "FinanceDimensionSetItems", "FinanceDimensionSetId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionSetItems_FinanceDimensionValueId", "FinanceDimensionSetItems", "FinanceDimensionValueId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionSetItems_TenantId_FinanceDimensionSetId_FinanceDimensionDefinitionId", "FinanceDimensionSetItems", new[] { "TenantId", "FinanceDimensionSetId", "FinanceDimensionDefinitionId" }, unique: true);
        migrationBuilder.CreateIndex("IX_FinanceDimensionSetItems_TenantId", "FinanceDimensionSetItems", "TenantId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionAccountRules_AccountId", "FinanceDimensionAccountRules", "AccountId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionAccountRules_DefaultDimensionValueId", "FinanceDimensionAccountRules", "DefaultDimensionValueId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionAccountRules_FinanceDimensionDefinitionId", "FinanceDimensionAccountRules", "FinanceDimensionDefinitionId");
        migrationBuilder.CreateIndex("IX_FinanceDimensionAccountRules_TenantId_AccountId_FinanceDimensionDefinitionId", "FinanceDimensionAccountRules", new[] { "TenantId", "AccountId", "FinanceDimensionDefinitionId" }, unique: true);
        migrationBuilder.CreateIndex("IX_FinanceDimensionAccountRules_TenantId", "FinanceDimensionAccountRules", "TenantId");
        migrationBuilder.CreateIndex("IX_AccountTransactions_FinanceDimensionSetId", "AccountTransactions", "FinanceDimensionSetId");
        migrationBuilder.CreateIndex("IX_AccountTransactions_TenantId_FinanceDimensionSetId_TransactionDate_AccountId", "AccountTransactions", new[] { "TenantId", "FinanceDimensionSetId", "TransactionDate", "AccountId" });
        migrationBuilder.AddForeignKey("FK_AccountTransactions_FinanceDimensionSets_FinanceDimensionSetId", "AccountTransactions", "FinanceDimensionSetId", "FinanceDimensionSets", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_AccountTransactions_FinanceDimensionSets_FinanceDimensionSetId", "AccountTransactions");
        migrationBuilder.DropIndex("IX_AccountTransactions_FinanceDimensionSetId", "AccountTransactions");
        migrationBuilder.DropIndex("IX_AccountTransactions_TenantId_FinanceDimensionSetId_TransactionDate_AccountId", "AccountTransactions");
        migrationBuilder.DropColumn("FinanceDimensionSetId", "AccountTransactions");
        migrationBuilder.DropTable("FinanceDimensionAccountRules");
        migrationBuilder.DropTable("FinanceDimensionSetItems");
        migrationBuilder.DropTable("FinanceDimensionSets");
        migrationBuilder.DropTable("FinanceDimensionValues");
        migrationBuilder.DropTable("FinanceDimensionDefinitions");
    }
}
