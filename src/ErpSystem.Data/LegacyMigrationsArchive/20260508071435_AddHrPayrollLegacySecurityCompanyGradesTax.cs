using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollLegacySecurityCompanyGradesTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollTaxBands_TenantId_TaxType_SerialNo_EffectiveFrom",
                table: "PayrollTaxBands");

            migrationBuilder.AddColumn<decimal>(
                name: "CumulativeSalary",
                table: "PayrollTaxBands",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CumulativeTax",
                table: "PayrollTaxBands",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PayrollTaxBands",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnnual",
                table: "PayrollTaxBands",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegacyCompanyCode",
                table: "PayrollTaxBands",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayPeriod",
                table: "PayrollTaxBands",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayPeriodFrom",
                table: "PayrollTaxBands",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayPeriodTo",
                table: "PayrollTaxBands",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PerMonthAmount",
                table: "PayrollTaxBands",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableIncome",
                table: "PayrollTaxBands",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PayrollCompanyProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanySystemName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    MultipleBusinessUnits = table.Column<bool>(type: "bit", nullable: false),
                    BusinessNumber = table.Column<int>(type: "int", nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CityOrTown = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    RegionOrState = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    LicenceType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    LicenceNo = table.Column<int>(type: "int", nullable: true),
                    SocialSecurityNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TaxpayerIdNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PictureName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LogoName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SplitLicenceOnBusinessUnits = table.Column<bool>(type: "bit", nullable: false),
                    LicencePercentOrNumber = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    MakeCompanyABusinessUnit = table.Column<bool>(type: "bit", nullable: false),
                    MultiplePayBasis = table.Column<bool>(type: "bit", nullable: false),
                    CompanyPayBasis = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    MultipleEmployeePaymentMethods = table.Column<bool>(type: "bit", nullable: false),
                    DefaultEmployeePaymentMethod = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollCompanyProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollCompanyProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollGrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    GradeType = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    GradeName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SystemGradeName = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    MinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MidPoint = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    StartPoint = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    IncrementStep = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Ceiling = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BusinessUnitId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CompanyId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    OrderField = table.Column<int>(type: "int", nullable: true),
                    ReportingName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnforceNotchConsistency = table.Column<bool>(type: "bit", nullable: false),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollGrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollGrades_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLegacyMenuSecurity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FormName = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Allowed = table.Column<bool>(type: "bit", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    TopMenu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubMenu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PayrollLegacyMenuSecurity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLegacyMenuSecurity_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollBusinessUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollCompanyProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessUnitId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    BusinessUnitCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    BusinessUnitName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DistributeLicence = table.Column<bool>(type: "bit", nullable: false),
                    LicencePercentOrNumber = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    LicenceValue = table.Column<int>(type: "int", nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegionOrState = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CompanyId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    AccountsCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    MultiplePayBasis = table.Column<bool>(type: "bit", nullable: false),
                    CompanyPayBasis = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    MultipleEmployeePaymentMethods = table.Column<bool>(type: "bit", nullable: false),
                    DefaultEmployeePaymentMethod = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
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
                    table.PrimaryKey("PK_PayrollBusinessUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBusinessUnits_PayrollCompanyProfiles_PayrollCompanyProfileId",
                        column: x => x.PayrollCompanyProfileId,
                        principalTable: "PayrollCompanyProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollBusinessUnits_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollCompanyBankers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollCompanyProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BankCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    BankBranch = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Address1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address3 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountNumber1 = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccountNumber2 = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccountNumber3 = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CompanyId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Signatory1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Signatory2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Signatory3 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SignatoryPosition1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SignatoryPosition2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SignatoryPosition3 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankRegion = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                    table.PrimaryKey("PK_PayrollCompanyBankers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollCompanyBankers_PayrollCompanyProfiles_PayrollCompanyProfileId",
                        column: x => x.PayrollCompanyProfileId,
                        principalTable: "PayrollCompanyProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollCompanyBankers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollGradeNotches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    SystemGradeName = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Notch = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrderField = table.Column<int>(type: "int", nullable: true),
                    BusinessUnitId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CompanyId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    AnnualisedValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    GradeName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ReportingName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LegacyCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
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
                    table.PrimaryKey("PK_PayrollGradeNotches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollGradeNotches_PayrollGrades_PayrollGradeId",
                        column: x => x.PayrollGradeId,
                        principalTable: "PayrollGrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollGradeNotches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTaxBands_TenantId_PayPeriod_LegacyCompanyCode",
                table: "PayrollTaxBands",
                columns: new[] { "TenantId", "PayPeriod", "LegacyCompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTaxBands_TenantId_TaxType_SerialNo_EffectiveFrom_IsAnnual_LegacyCompanyCode",
                table: "PayrollTaxBands",
                columns: new[] { "TenantId", "TaxType", "SerialNo", "EffectiveFrom", "IsAnnual", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBusinessUnits_PayrollCompanyProfileId",
                table: "PayrollBusinessUnits",
                column: "PayrollCompanyProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBusinessUnits_TenantId_BusinessUnitId_LegacyCompanyCode",
                table: "PayrollBusinessUnits",
                columns: new[] { "TenantId", "BusinessUnitId", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBusinessUnits_TenantId_CompanyId_CompanyCode",
                table: "PayrollBusinessUnits",
                columns: new[] { "TenantId", "CompanyId", "CompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCompanyBankers_PayrollCompanyProfileId",
                table: "PayrollCompanyBankers",
                column: "PayrollCompanyProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCompanyBankers_TenantId_CompanyId_CompanyCode_BankCode_BankBranch",
                table: "PayrollCompanyBankers",
                columns: new[] { "TenantId", "CompanyId", "CompanyCode", "BankCode", "BankBranch" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCompanyBankers_TenantId_LegacyCompanyCode",
                table: "PayrollCompanyBankers",
                columns: new[] { "TenantId", "LegacyCompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCompanyProfiles_TenantId_CompanyCode_LegacyCompanyCode",
                table: "PayrollCompanyProfiles",
                columns: new[] { "TenantId", "CompanyCode", "LegacyCompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCompanyProfiles_TenantId_CompanyId",
                table: "PayrollCompanyProfiles",
                columns: new[] { "TenantId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollGradeNotches_PayrollGradeId",
                table: "PayrollGradeNotches",
                column: "PayrollGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollGradeNotches_TenantId_GradeName_Notch_LegacyCompanyCode",
                table: "PayrollGradeNotches",
                columns: new[] { "TenantId", "GradeName", "Notch", "LegacyCompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollGradeNotches_TenantId_PayrollGradeId_Notch_CurrencyCode_LegacyCompanyCode",
                table: "PayrollGradeNotches",
                columns: new[] { "TenantId", "PayrollGradeId", "Notch", "CurrencyCode", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollGrades_TenantId_GradeId_LegacyCompanyCode",
                table: "PayrollGrades",
                columns: new[] { "TenantId", "GradeId", "LegacyCompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollGrades_TenantId_GradeName_CurrencyCode_LegacyCompanyCode",
                table: "PayrollGrades",
                columns: new[] { "TenantId", "GradeName", "CurrencyCode", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLegacyMenuSecurity_TenantId_FormCode_UserName_GroupName_LegacyCompanyCode",
                table: "PayrollLegacyMenuSecurity",
                columns: new[] { "TenantId", "FormCode", "UserName", "GroupName", "LegacyCompanyCode" },
                unique: true,
                filter: "[LegacyCompanyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLegacyMenuSecurity_TenantId_GroupName_Allowed",
                table: "PayrollLegacyMenuSecurity",
                columns: new[] { "TenantId", "GroupName", "Allowed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollBusinessUnits");

            migrationBuilder.DropTable(
                name: "PayrollCompanyBankers");

            migrationBuilder.DropTable(
                name: "PayrollGradeNotches");

            migrationBuilder.DropTable(
                name: "PayrollLegacyMenuSecurity");

            migrationBuilder.DropTable(
                name: "PayrollCompanyProfiles");

            migrationBuilder.DropTable(
                name: "PayrollGrades");

            migrationBuilder.DropIndex(
                name: "IX_PayrollTaxBands_TenantId_PayPeriod_LegacyCompanyCode",
                table: "PayrollTaxBands");

            migrationBuilder.DropIndex(
                name: "IX_PayrollTaxBands_TenantId_TaxType_SerialNo_EffectiveFrom_IsAnnual_LegacyCompanyCode",
                table: "PayrollTaxBands");

            migrationBuilder.DropColumn(
                name: "CumulativeSalary",
                table: "PayrollTaxBands");

            migrationBuilder.DropColumn(
                name: "CumulativeTax",
                table: "PayrollTaxBands");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PayrollTaxBands");

            migrationBuilder.DropColumn(
                name: "IsAnnual",
                table: "PayrollTaxBands");

            migrationBuilder.DropColumn(
                name: "LegacyCompanyCode",
                table: "PayrollTaxBands");

            migrationBuilder.DropColumn(
                name: "PayPeriod",
                table: "PayrollTaxBands");

            migrationBuilder.DropColumn(
                name: "PayPeriodFrom",
                table: "PayrollTaxBands");

            migrationBuilder.DropColumn(
                name: "PayPeriodTo",
                table: "PayrollTaxBands");

            migrationBuilder.DropColumn(
                name: "PerMonthAmount",
                table: "PayrollTaxBands");

            migrationBuilder.DropColumn(
                name: "TaxableIncome",
                table: "PayrollTaxBands");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTaxBands_TenantId_TaxType_SerialNo_EffectiveFrom",
                table: "PayrollTaxBands",
                columns: new[] { "TenantId", "TaxType", "SerialNo", "EffectiveFrom" },
                unique: true);
        }
    }
}
