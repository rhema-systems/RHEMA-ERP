using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260903120000_AddBusinessPartnerBankAccounts")]
public sealed class AddBusinessPartnerBankAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BusinessPartnerBankAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                BranchName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                SwiftCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Iban = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Currency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                IsPrimary = table.Column<bool>(type: "bit", nullable: false),
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
                table.PrimaryKey("PK_BusinessPartnerBankAccounts", x => x.Id);
                table.ForeignKey(
                    name: "FK_BusinessPartnerBankAccounts_BusinessPartners_BusinessPartnerId",
                    column: x => x.BusinessPartnerId,
                    principalTable: "BusinessPartners",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_BusinessPartnerBankAccounts_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BusinessPartnerBankAccounts_BusinessPartnerId",
            table: "BusinessPartnerBankAccounts",
            column: "BusinessPartnerId");

        migrationBuilder.CreateIndex(
            name: "IX_BusinessPartnerBankAccounts_TenantId",
            table: "BusinessPartnerBankAccounts",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_BusinessPartnerBankAccounts_TenantId_BusinessPartnerId",
            table: "BusinessPartnerBankAccounts",
            columns: new[] { "TenantId", "BusinessPartnerId" });

        migrationBuilder.CreateIndex(
            name: "IX_BusinessPartnerBankAccounts_TenantId_BusinessPartnerId_IsPrimary",
            table: "BusinessPartnerBankAccounts",
            columns: new[] { "TenantId", "BusinessPartnerId", "IsPrimary" },
            unique: true,
            filter: "[IsPrimary] = 1 AND [IsDeleted] = 0");

        migrationBuilder.Sql(
            """
            INSERT INTO [BusinessPartnerBankAccounts]
                ([Id], [BusinessPartnerId], [BankName], [BranchName], [AccountName], [AccountNumber],
                 [SwiftCode], [Iban], [Currency], [IsPrimary], [IsActive], [TenantId], [CreatedAt], [IsDeleted])
            SELECT NEWID(), [Id], [BankName], [BankBranch], [BankAccountName], [BankAccountNumber],
                   [BankSwiftCode], [BankIBAN], [Currency], 1, 1, [TenantId], SYSUTCDATETIME(), 0
            FROM [BusinessPartners]
            WHERE [IsDeleted] = 0
              AND (NULLIF(LTRIM(RTRIM([BankName])), '') IS NOT NULL
                OR NULLIF(LTRIM(RTRIM([BankBranch])), '') IS NOT NULL
                OR NULLIF(LTRIM(RTRIM([BankAccountName])), '') IS NOT NULL
                OR NULLIF(LTRIM(RTRIM([BankAccountNumber])), '') IS NOT NULL
                OR NULLIF(LTRIM(RTRIM([BankSwiftCode])), '') IS NOT NULL
                OR NULLIF(LTRIM(RTRIM([BankIBAN])), '') IS NOT NULL);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "BusinessPartnerBankAccounts");
}
