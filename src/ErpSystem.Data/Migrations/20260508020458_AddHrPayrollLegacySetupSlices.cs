using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollLegacySetupSlices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowEmployeePaymentMethod",
                table: "PayrollParameterSets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DateFormat",
                table: "PayrollParameterSets",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DebitRatio",
                table: "PayrollParameterSets",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultEmployeePaymentMethod",
                table: "PayrollParameterSets",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultPayBasis",
                table: "PayrollParameterSets",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "PayrollParameterSets",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyCompanyCode",
                table: "PayrollParameterSets",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxLoginCount",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumHireAge",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPasswordLength",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPasswordLowercase",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPasswordNumbers",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPasswordSpecialCharacters",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPasswordUppercase",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumTaxableIncome",
                table: "PayrollParameterSets",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MultiLoginEnabled",
                table: "PayrollParameterSets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MultiplePayBasisEnabled",
                table: "PayrollParameterSets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "NightAllowanceAmount",
                table: "PayrollParameterSets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NightAllowancePercent",
                table: "PayrollParameterSets",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PasswordExpirationDays",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PasswordReuseCount",
                table: "PayrollParameterSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayMode",
                table: "PayrollParameterSets",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportingCurrency",
                table: "PayrollParameterSets",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "PayrollParameterSets",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PayrollCodeTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Blocked = table.Column<bool>(type: "bit", nullable: false),
                    Dependent = table.Column<bool>(type: "bit", nullable: false),
                    DependentOnCodeType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollCodeTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollCodeTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PayPeriod = table.Column<int>(type: "int", nullable: false),
                    PayPeriodFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayPeriodTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollExchangeRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollExchangeRates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HolidayDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollHolidays_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollNonWorkingDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OvertimeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollNonWorkingDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollNonWorkingDays_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollCodeValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollCodeTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ActualCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AdditionalDescription = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccountCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Blocked = table.Column<bool>(type: "bit", nullable: false),
                    DependentCodeType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    DependentActualCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollCodeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollCodeValues_PayrollCodeTypes_PayrollCodeTypeId",
                        column: x => x.PayrollCodeTypeId,
                        principalTable: "PayrollCodeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollCodeValues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCodeTypes_TenantId_Blocked",
                table: "PayrollCodeTypes",
                columns: new[] { "TenantId", "Blocked" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCodeTypes_TenantId_CodeType_LegacyCompanyCode",
                table: "PayrollCodeTypes",
                columns: new[] { "TenantId", "CodeType", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCodeValues_PayrollCodeTypeId",
                table: "PayrollCodeValues",
                column: "PayrollCodeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCodeValues_TenantId_CodeType_ActualCode_LegacyCompanyCode",
                table: "PayrollCodeValues",
                columns: new[] { "TenantId", "CodeType", "ActualCode", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCodeValues_TenantId_CodeType_Blocked",
                table: "PayrollCodeValues",
                columns: new[] { "TenantId", "CodeType", "Blocked" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollExchangeRates_TenantId_CurrencyCode_PayPeriod_LegacyCompanyCode",
                table: "PayrollExchangeRates",
                columns: new[] { "TenantId", "CurrencyCode", "PayPeriod", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollExchangeRates_TenantId_PayPeriodFrom_PayPeriodTo",
                table: "PayrollExchangeRates",
                columns: new[] { "TenantId", "PayPeriodFrom", "PayPeriodTo" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollHolidays_TenantId_HolidayDate_LegacyCompanyCode",
                table: "PayrollHolidays",
                columns: new[] { "TenantId", "HolidayDate", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollNonWorkingDays_TenantId_DayCode_LegacyCompanyCode",
                table: "PayrollNonWorkingDays",
                columns: new[] { "TenantId", "DayCode", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollCodeValues");

            migrationBuilder.DropTable(
                name: "PayrollExchangeRates");

            migrationBuilder.DropTable(
                name: "PayrollHolidays");

            migrationBuilder.DropTable(
                name: "PayrollNonWorkingDays");

            migrationBuilder.DropTable(
                name: "PayrollCodeTypes");

            migrationBuilder.DropColumn(
                name: "AllowEmployeePaymentMethod",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "DateFormat",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "DebitRatio",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "DefaultEmployeePaymentMethod",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "DefaultPayBasis",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "LegacyCompanyCode",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MaxLoginCount",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MinimumHireAge",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MinimumPasswordLength",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MinimumPasswordLowercase",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MinimumPasswordNumbers",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MinimumPasswordSpecialCharacters",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MinimumPasswordUppercase",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MinimumTaxableIncome",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MultiLoginEnabled",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "MultiplePayBasisEnabled",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "NightAllowanceAmount",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "NightAllowancePercent",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "PasswordExpirationDays",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "PasswordReuseCount",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "PayMode",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "ReportingCurrency",
                table: "PayrollParameterSets");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "PayrollParameterSets");
        }
    }
}
