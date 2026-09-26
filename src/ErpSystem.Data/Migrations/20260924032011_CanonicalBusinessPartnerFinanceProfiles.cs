using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalBusinessPartnerFinanceProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessPartnerRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InactiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_BusinessPartnerRoles", x => x.Id);
                    table.CheckConstraint("CK_BusinessPartnerRoles_InactiveEvidence", "[Status] <> 2 OR ([InactiveFromUtc] IS NOT NULL AND NULLIF(LTRIM(RTRIM([StatusReason])), '') IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_BusinessPartnerRoles_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerRoles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerArProfileVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentTermId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 2, nullable: true),
                    IsWithholdingAgent = table.Column<bool>(type: "bit", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_BusinessPartnerArProfileVersions", x => x.Id);
                    table.CheckConstraint("CK_BusinessPartnerArProfiles_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.ForeignKey(
                        name: "FK_BusinessPartnerArProfileVersions_BusinessPartnerRoles_BusinessPartnerRoleId",
                        column: x => x.BusinessPartnerRoleId,
                        principalTable: "BusinessPartnerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerArProfileVersions_PaymentTerms_PaymentTermId",
                        column: x => x.PaymentTermId,
                        principalTable: "PaymentTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerArProfileVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerApProfileVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentTermId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultTaxGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultExpenseAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectToWithholding = table.Column<bool>(type: "bit", nullable: false),
                    DefaultWithholdingLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_BusinessPartnerApProfileVersions", x => x.Id);
                    table.CheckConstraint("CK_BusinessPartnerApProfiles_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.ForeignKey(
                        name: "FK_BusinessPartnerApProfileVersions_Accounts_DefaultExpenseAccountId",
                        column: x => x.DefaultExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerApProfileVersions_BusinessPartnerRoles_BusinessPartnerRoleId",
                        column: x => x.BusinessPartnerRoleId,
                        principalTable: "BusinessPartnerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerApProfileVersions_PaymentTerms_PaymentTermId",
                        column: x => x.PaymentTermId,
                        principalTable: "PaymentTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerApProfileVersions_TaxGroups_DefaultTaxGroupId",
                        column: x => x.DefaultTaxGroupId,
                        principalTable: "TaxGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerApProfileVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartnerApWhtDefaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApProfileVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WithholdingTaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefaultForAp = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_BusinessPartnerApWhtDefaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerApWhtDefaults_BusinessPartnerApProfileVersions_ApProfileVersionId",
                        column: x => x.ApProfileVersionId,
                        principalTable: "BusinessPartnerApProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerApWhtDefaults_Taxes_WithholdingTaxId",
                        column: x => x.WithholdingTaxId,
                        principalTable: "Taxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerApWhtDefaults_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApProfileVersions_BusinessPartnerRoleId",
                table: "BusinessPartnerApProfileVersions",
                column: "BusinessPartnerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApProfileVersions_DefaultExpenseAccountId",
                table: "BusinessPartnerApProfileVersions",
                column: "DefaultExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApProfileVersions_DefaultTaxGroupId",
                table: "BusinessPartnerApProfileVersions",
                column: "DefaultTaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApProfileVersions_DefaultWithholdingLineId",
                table: "BusinessPartnerApProfileVersions",
                column: "DefaultWithholdingLineId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApProfileVersions_PaymentTermId",
                table: "BusinessPartnerApProfileVersions",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApProfileVersions_TenantId_BusinessPartnerRoleId_Status_EffectiveFrom",
                table: "BusinessPartnerApProfileVersions",
                columns: new[] { "TenantId", "BusinessPartnerRoleId", "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApProfileVersions_TenantId_BusinessPartnerRoleId_VersionNumber",
                table: "BusinessPartnerApProfileVersions",
                columns: new[] { "TenantId", "BusinessPartnerRoleId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApWhtDefaults_ApProfileVersionId",
                table: "BusinessPartnerApWhtDefaults",
                column: "ApProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApWhtDefaults_OneDefault",
                table: "BusinessPartnerApWhtDefaults",
                columns: new[] { "TenantId", "ApProfileVersionId", "IsDefaultForAp" },
                unique: true,
                filter: "[IsDefaultForAp] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApWhtDefaults_TenantId_ApProfileVersionId_CategoryCode",
                table: "BusinessPartnerApWhtDefaults",
                columns: new[] { "TenantId", "ApProfileVersionId", "CategoryCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerApWhtDefaults_WithholdingTaxId",
                table: "BusinessPartnerApWhtDefaults",
                column: "WithholdingTaxId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerArProfileVersions_BusinessPartnerRoleId",
                table: "BusinessPartnerArProfileVersions",
                column: "BusinessPartnerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerArProfileVersions_PaymentTermId",
                table: "BusinessPartnerArProfileVersions",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerArProfileVersions_TenantId_BusinessPartnerRoleId_Status_EffectiveFrom",
                table: "BusinessPartnerArProfileVersions",
                columns: new[] { "TenantId", "BusinessPartnerRoleId", "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerArProfileVersions_TenantId_BusinessPartnerRoleId_VersionNumber",
                table: "BusinessPartnerArProfileVersions",
                columns: new[] { "TenantId", "BusinessPartnerRoleId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRoles_BusinessPartnerId",
                table: "BusinessPartnerRoles",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRoles_TenantId_BusinessPartnerId_RoleType",
                table: "BusinessPartnerRoles",
                columns: new[] { "TenantId", "BusinessPartnerId", "RoleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerRoles_TenantId_RoleType_Status",
                table: "BusinessPartnerRoles",
                columns: new[] { "TenantId", "RoleType", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartnerApProfileVersions_BusinessPartnerApWhtDefaults_DefaultWithholdingLineId",
                table: "BusinessPartnerApProfileVersions",
                column: "DefaultWithholdingLineId",
                principalTable: "BusinessPartnerApWhtDefaults",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartnerApProfileVersions_BusinessPartnerApWhtDefaults_DefaultWithholdingLineId",
                table: "BusinessPartnerApProfileVersions");

            migrationBuilder.DropTable(
                name: "BusinessPartnerArProfileVersions");

            migrationBuilder.DropTable(
                name: "BusinessPartnerApWhtDefaults");

            migrationBuilder.DropTable(
                name: "BusinessPartnerApProfileVersions");

            migrationBuilder.DropTable(
                name: "BusinessPartnerRoles");
        }
    }
}
