using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrOraclePayrollMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollBonusPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Taxable = table.Column<bool>(type: "bit", nullable: false),
                    SeparateTax = table.Column<bool>(type: "bit", nullable: false),
                    TaxFreeCeiling = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinimumToTax = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Prorate = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PayrollBonusPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBonusPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ComponentType = table.Column<int>(type: "int", nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Taxable = table.Column<bool>(type: "bit", nullable: false),
                    EmployerTaxable = table.Column<bool>(type: "bit", nullable: false),
                    SeparateTax = table.Column<bool>(type: "bit", nullable: false),
                    IncludeInGross = table.Column<bool>(type: "bit", nullable: false),
                    GrossUp = table.Column<bool>(type: "bit", nullable: false),
                    Prorate = table.Column<bool>(type: "bit", nullable: false),
                    AppliesByDefault = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
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
                    table.PrimaryKey("PK_PayrollComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollComponents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ImportType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    MatchedRows = table.Column<int>(type: "int", nullable: false),
                    MissingRows = table.Column<int>(type: "int", nullable: false),
                    DuplicateRows = table.Column<int>(type: "int", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PayrollImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollImportBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollJournalMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ComponentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DebitCredit = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_PayrollJournalMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollJournalMappings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLoanPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MaxLoanAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InterestRatePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MaxPaybackPeriods = table.Column<int>(type: "int", nullable: true),
                    MaxDebitRatioPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    InterestType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_PayrollLoanPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLoanPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollOvertimePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalWorkingHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WeekdayRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    HolidayRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SpecialDutyRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Taxable = table.Column<bool>(type: "bit", nullable: false),
                    TaxCeiling = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    SeparateOvertimeTax = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PayrollOvertimePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollOvertimePolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollParameterSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CurrentPayPeriod = table.Column<int>(type: "int", nullable: false),
                    CurrentPeriodFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentPeriodTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MonthDays = table.Column<int>(type: "int", nullable: false),
                    PayFrequencyMonths = table.Column<int>(type: "int", nullable: false),
                    EmployeeSsfRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EmployerSsfRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SsfLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BonusTaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MinimumBonusToTax = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WithholdingTaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SeparateBonusTax = table.Column<bool>(type: "bit", nullable: false),
                    MultiCurrencyEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TimesheetEnabled = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PayrollParameterSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollParameterSets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPensionSchemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EmployeeRatePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EmployerRatePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ContributionLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PayrollPensionSchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPensionSchemes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PayPeriod = table.Column<int>(type: "int", nullable: false),
                    PayPeriodFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayPeriodTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RunDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EmployeeCount = table.Column<int>(type: "int", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployeeContributionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployerContributionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalculatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollTaxBands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SerialNo = table.Column<int>(type: "int", nullable: false),
                    LowerBound = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UpperBound = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RatePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FixedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_PayrollTaxBands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollTaxBands_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollTaxReliefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AppliesByDefault = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PayrollTaxReliefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollTaxReliefs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    LegacyEmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LegacyFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    MatchedEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedEmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_PayrollImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollImportRows_Employees_MatchedEmployeeId",
                        column: x => x.MatchedEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollImportRows_PayrollImportBatches_PayrollImportBatchId",
                        column: x => x.PayrollImportBatchId,
                        principalTable: "PayrollImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollImportRows_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollJournalLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DebitCredit = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Posted = table.Column<bool>(type: "bit", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PayrollJournalLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollJournalLines_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollJournalLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollReportSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<int>(type: "int", nullable: false),
                    ReportName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SnapshotNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_PayrollReportSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollReportSnapshots_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollReportSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRunEmployees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxableAllowances = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NonTaxableAllowances = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxableDeductions = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NonTaxableDeductions = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EmployeeContribution = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EmployerContribution = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxRelief = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxableIncome = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IncomeTax = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    GrossIncome = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NetIncome = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
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
                    table.PrimaryKey("PK_PayrollRunEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRunEmployees_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRunEmployees_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRunEmployees_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPayslipSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayslipNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GrossIncome = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NetIncome = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployeeContribution = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_PayrollPayslipSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPayslipSnapshots_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPayslipSnapshots_PayrollRunEmployees_PayrollRunEmployeeId",
                        column: x => x.PayrollRunEmployeeId,
                        principalTable: "PayrollRunEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPayslipSnapshots_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPayslipSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PayrollComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ComponentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployerAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Taxable = table.Column<bool>(type: "bit", nullable: false),
                    EmployerTaxable = table.Column<bool>(type: "bit", nullable: false),
                    SeparateTax = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
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
                    table.PrimaryKey("PK_PayrollTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollTransactions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollTransactions_PayrollComponents_PayrollComponentId",
                        column: x => x.PayrollComponentId,
                        principalTable: "PayrollComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollTransactions_PayrollRunEmployees_PayrollRunEmployeeId",
                        column: x => x.PayrollRunEmployeeId,
                        principalTable: "PayrollRunEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollTransactions_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEmployeeComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculationTypeOverride = table.Column<int>(type: "int", nullable: true),
                    AmountOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RateOverride = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Applicable = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_PayrollEmployeeComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeComponents_PayrollComponents_PayrollComponentId",
                        column: x => x.PayrollComponentId,
                        principalTable: "PayrollComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeComponents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEmployeeProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LegacyEmployeeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LegacyEmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PayrollActive = table.Column<bool>(type: "bit", nullable: false),
                    PayTax = table.Column<bool>(type: "bit", nullable: false),
                    SsfApplicable = table.Column<bool>(type: "bit", nullable: false),
                    GrossUp = table.Column<bool>(type: "bit", nullable: false),
                    Tier2Only = table.Column<bool>(type: "bit", nullable: false),
                    OvertimeEligible = table.Column<bool>(type: "bit", nullable: false),
                    SsfNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TinNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    DefaultPaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PayrollEmployeeProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeProfiles_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLoans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DateGranted = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AmountGranted = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MonthlyRepaymentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    InterestRatePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PaymentStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NumberOfRepayments = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_PayrollLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLoans_PayrollEmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "PayrollEmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollLoans_PayrollLoanPolicies_LoanPolicyId",
                        column: x => x.LoanPolicyId,
                        principalTable: "PayrollLoanPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollLoans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPaymentMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PaymentPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BankCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BankBranchCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_PayrollPaymentMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPaymentMethods_PayrollEmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "PayrollEmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPaymentMethods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollSalaryBases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MonthlyBasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AnnualBasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_PayrollSalaryBases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollSalaryBases_PayrollEmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "PayrollEmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollSalaryBases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollTimesheetSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayPeriod = table.Column<int>(type: "int", nullable: false),
                    PayPeriodFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayPeriodTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NormalHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WeekdayHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    HolidayHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AbsentHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NightShiftCount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AttendanceCount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OvertimeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_PayrollTimesheetSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollTimesheetSummaries_PayrollEmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "PayrollEmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollTimesheetSummaries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLoanSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollLoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    RepaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InterestAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Posted = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PayrollLoanSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLoanSchedules_PayrollLoans_PayrollLoanId",
                        column: x => x.PayrollLoanId,
                        principalTable: "PayrollLoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollLoanSchedules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBonusPolicies_TenantId_Code",
                table: "PayrollBonusPolicies",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollComponents_TenantId_Code",
                table: "PayrollComponents",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollComponents_TenantId_ComponentType_IsActive",
                table: "PayrollComponents",
                columns: new[] { "TenantId", "ComponentType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeComponents_EmployeeProfileId",
                table: "PayrollEmployeeComponents",
                column: "EmployeeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeComponents_PayrollComponentId",
                table: "PayrollEmployeeComponents",
                column: "PayrollComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeComponents_TenantId_EmployeeProfileId_PayrollComponentId",
                table: "PayrollEmployeeComponents",
                columns: new[] { "TenantId", "EmployeeProfileId", "PayrollComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeProfiles_DefaultPaymentMethodId",
                table: "PayrollEmployeeProfiles",
                column: "DefaultPaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeProfiles_EmployeeId",
                table: "PayrollEmployeeProfiles",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeProfiles_TenantId_EmployeeId",
                table: "PayrollEmployeeProfiles",
                columns: new[] { "TenantId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeProfiles_TenantId_EmployeeNumber",
                table: "PayrollEmployeeProfiles",
                columns: new[] { "TenantId", "EmployeeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeProfiles_TenantId_LegacyEmployeeNumber",
                table: "PayrollEmployeeProfiles",
                columns: new[] { "TenantId", "LegacyEmployeeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeProfiles_TenantId_PayrollActive",
                table: "PayrollEmployeeProfiles",
                columns: new[] { "TenantId", "PayrollActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollImportBatches_TenantId_ImportType_ImportedAt",
                table: "PayrollImportBatches",
                columns: new[] { "TenantId", "ImportType", "ImportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollImportRows_MatchedEmployeeId",
                table: "PayrollImportRows",
                column: "MatchedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollImportRows_PayrollImportBatchId",
                table: "PayrollImportRows",
                column: "PayrollImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollImportRows_TenantId_LegacyEmployeeNumber",
                table: "PayrollImportRows",
                columns: new[] { "TenantId", "LegacyEmployeeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollImportRows_TenantId_PayrollImportBatchId_RowNumber",
                table: "PayrollImportRows",
                columns: new[] { "TenantId", "PayrollImportBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalLines_PayrollRunId",
                table: "PayrollJournalLines",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalLines_TenantId_AccountCode",
                table: "PayrollJournalLines",
                columns: new[] { "TenantId", "AccountCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalLines_TenantId_PayrollRunId_SequenceNo",
                table: "PayrollJournalLines",
                columns: new[] { "TenantId", "PayrollRunId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalMappings_TenantId_AccountCode",
                table: "PayrollJournalMappings",
                columns: new[] { "TenantId", "AccountCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalMappings_TenantId_TransactionType_ComponentCode",
                table: "PayrollJournalMappings",
                columns: new[] { "TenantId", "TransactionType", "ComponentCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLoanPolicies_TenantId_Code",
                table: "PayrollLoanPolicies",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLoans_EmployeeProfileId",
                table: "PayrollLoans",
                column: "EmployeeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLoans_LoanPolicyId",
                table: "PayrollLoans",
                column: "LoanPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLoans_TenantId_EmployeeProfileId_FacilityNumber",
                table: "PayrollLoans",
                columns: new[] { "TenantId", "EmployeeProfileId", "FacilityNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLoans_TenantId_IsActive_PaymentStartDate",
                table: "PayrollLoans",
                columns: new[] { "TenantId", "IsActive", "PaymentStartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLoanSchedules_PayrollLoanId",
                table: "PayrollLoanSchedules",
                column: "PayrollLoanId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLoanSchedules_TenantId_PayrollLoanId_SequenceNo",
                table: "PayrollLoanSchedules",
                columns: new[] { "TenantId", "PayrollLoanId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollOvertimePolicies_TenantId_Code",
                table: "PayrollOvertimePolicies",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollOvertimePolicies_TenantId_IsDefault_IsActive",
                table: "PayrollOvertimePolicies",
                columns: new[] { "TenantId", "IsDefault", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollParameterSets_TenantId_Code",
                table: "PayrollParameterSets",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollParameterSets_TenantId_IsActive",
                table: "PayrollParameterSets",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPaymentMethods_EmployeeProfileId",
                table: "PayrollPaymentMethods",
                column: "EmployeeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPaymentMethods_TenantId_EmployeeProfileId_SequenceNo",
                table: "PayrollPaymentMethods",
                columns: new[] { "TenantId", "EmployeeProfileId", "SequenceNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPaymentMethods_TenantId_PaymentType_IsActive",
                table: "PayrollPaymentMethods",
                columns: new[] { "TenantId", "PaymentType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayslipSnapshots_EmployeeId",
                table: "PayrollPayslipSnapshots",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayslipSnapshots_PayrollRunEmployeeId",
                table: "PayrollPayslipSnapshots",
                column: "PayrollRunEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayslipSnapshots_PayrollRunId",
                table: "PayrollPayslipSnapshots",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayslipSnapshots_TenantId_PayrollRunId_EmployeeId",
                table: "PayrollPayslipSnapshots",
                columns: new[] { "TenantId", "PayrollRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayslipSnapshots_TenantId_PayslipNumber",
                table: "PayrollPayslipSnapshots",
                columns: new[] { "TenantId", "PayslipNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPensionSchemes_TenantId_Code",
                table: "PayrollPensionSchemes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPensionSchemes_TenantId_IsDefault_IsActive",
                table: "PayrollPensionSchemes",
                columns: new[] { "TenantId", "IsDefault", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollReportSnapshots_PayrollRunId",
                table: "PayrollReportSnapshots",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollReportSnapshots_TenantId_PayrollRunId_ReportType_GeneratedAt",
                table: "PayrollReportSnapshots",
                columns: new[] { "TenantId", "PayrollRunId", "ReportType", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollReportSnapshots_TenantId_SnapshotNumber",
                table: "PayrollReportSnapshots",
                columns: new[] { "TenantId", "SnapshotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunEmployees_EmployeeId",
                table: "PayrollRunEmployees",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunEmployees_PayrollRunId",
                table: "PayrollRunEmployees",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunEmployees_TenantId_EmployeeNumber",
                table: "PayrollRunEmployees",
                columns: new[] { "TenantId", "EmployeeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunEmployees_TenantId_PayrollRunId_EmployeeId",
                table: "PayrollRunEmployees",
                columns: new[] { "TenantId", "PayrollRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_PayPeriod_Status",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "PayPeriod", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_RunNumber",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "RunNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryBases_EmployeeProfileId",
                table: "PayrollSalaryBases",
                column: "EmployeeProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryBases_TenantId_EffectiveFrom_EffectiveTo",
                table: "PayrollSalaryBases",
                columns: new[] { "TenantId", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryBases_TenantId_EmployeeProfileId",
                table: "PayrollSalaryBases",
                columns: new[] { "TenantId", "EmployeeProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTaxBands_TenantId_TaxType_IsActive",
                table: "PayrollTaxBands",
                columns: new[] { "TenantId", "TaxType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTaxBands_TenantId_TaxType_SerialNo_EffectiveFrom",
                table: "PayrollTaxBands",
                columns: new[] { "TenantId", "TaxType", "SerialNo", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTaxReliefs_TenantId_AppliesByDefault_IsActive",
                table: "PayrollTaxReliefs",
                columns: new[] { "TenantId", "AppliesByDefault", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTaxReliefs_TenantId_Code",
                table: "PayrollTaxReliefs",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTimesheetSummaries_EmployeeProfileId",
                table: "PayrollTimesheetSummaries",
                column: "EmployeeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTimesheetSummaries_TenantId_EmployeeProfileId_PayPeriod",
                table: "PayrollTimesheetSummaries",
                columns: new[] { "TenantId", "EmployeeProfileId", "PayPeriod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTransactions_EmployeeId",
                table: "PayrollTransactions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTransactions_PayrollComponentId",
                table: "PayrollTransactions",
                column: "PayrollComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTransactions_PayrollRunEmployeeId",
                table: "PayrollTransactions",
                column: "PayrollRunEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTransactions_PayrollRunId",
                table: "PayrollTransactions",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTransactions_TenantId_PayrollRunEmployeeId",
                table: "PayrollTransactions",
                columns: new[] { "TenantId", "PayrollRunEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTransactions_TenantId_PayrollRunId_TransactionType",
                table: "PayrollTransactions",
                columns: new[] { "TenantId", "PayrollRunId", "TransactionType" });

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollEmployeeComponents_PayrollEmployeeProfiles_EmployeeProfileId",
                table: "PayrollEmployeeComponents",
                column: "EmployeeProfileId",
                principalTable: "PayrollEmployeeProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollEmployeeProfiles_PayrollPaymentMethods_DefaultPaymentMethodId",
                table: "PayrollEmployeeProfiles",
                column: "DefaultPaymentMethodId",
                principalTable: "PayrollPaymentMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollPaymentMethods_PayrollEmployeeProfiles_EmployeeProfileId",
                table: "PayrollPaymentMethods");

            migrationBuilder.DropTable(
                name: "PayrollBonusPolicies");

            migrationBuilder.DropTable(
                name: "PayrollEmployeeComponents");

            migrationBuilder.DropTable(
                name: "PayrollImportRows");

            migrationBuilder.DropTable(
                name: "PayrollJournalLines");

            migrationBuilder.DropTable(
                name: "PayrollJournalMappings");

            migrationBuilder.DropTable(
                name: "PayrollLoanSchedules");

            migrationBuilder.DropTable(
                name: "PayrollOvertimePolicies");

            migrationBuilder.DropTable(
                name: "PayrollParameterSets");

            migrationBuilder.DropTable(
                name: "PayrollPayslipSnapshots");

            migrationBuilder.DropTable(
                name: "PayrollPensionSchemes");

            migrationBuilder.DropTable(
                name: "PayrollReportSnapshots");

            migrationBuilder.DropTable(
                name: "PayrollSalaryBases");

            migrationBuilder.DropTable(
                name: "PayrollTaxBands");

            migrationBuilder.DropTable(
                name: "PayrollTaxReliefs");

            migrationBuilder.DropTable(
                name: "PayrollTimesheetSummaries");

            migrationBuilder.DropTable(
                name: "PayrollTransactions");

            migrationBuilder.DropTable(
                name: "PayrollImportBatches");

            migrationBuilder.DropTable(
                name: "PayrollLoans");

            migrationBuilder.DropTable(
                name: "PayrollComponents");

            migrationBuilder.DropTable(
                name: "PayrollRunEmployees");

            migrationBuilder.DropTable(
                name: "PayrollLoanPolicies");

            migrationBuilder.DropTable(
                name: "PayrollRuns");

            migrationBuilder.DropTable(
                name: "PayrollEmployeeProfiles");

            migrationBuilder.DropTable(
                name: "PayrollPaymentMethods");
        }
    }
}
