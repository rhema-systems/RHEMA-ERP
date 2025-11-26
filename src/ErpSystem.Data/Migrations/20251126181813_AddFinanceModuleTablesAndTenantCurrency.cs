using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceModuleTablesAndTenantCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("01424a6f-cd1a-417d-a470-d94111460dc7"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("40bf4967-2216-4121-9e5b-d518c4c13f65"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("47ffa269-7878-4916-a25a-2fbfb327c395"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("816673e8-7979-45a4-854f-c8c008795a8f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a5d7828a-429f-412e-a135-e0c15a224e2f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("b2b5e343-1da3-470b-a9d6-881a0bd3f611"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d9d20505-1ff1-4048-8856-cb9066c16588"));

            migrationBuilder.AddColumn<string>(
                name: "BaseCurrency",
                table: "Tenants",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BaseCurrencyName",
                table: "Tenants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyDecimalPlaces",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrencySymbol",
                table: "Tenants",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "BusinessPartnerRegistrationStatusHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BusinessPartnerRegistrationStatusHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "BusinessPartnerRegistrationStatusHistories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "BusinessPartnerRegistrationStatusHistories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "BusinessPartnerRegistrationStatusHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "BusinessPartnerRegistrationStatusHistories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "BusinessPartnerRegistrationStatusHistories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "BusinessPartnerRegistrationStatusHistories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "BusinessPartnerRegistrationStatusHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    AccountCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountSubCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ParentAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsSegmented = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsMultiCurrency = table.Column<bool>(type: "bit", nullable: false),
                    IsIFRSClassified = table.Column<bool>(type: "bit", nullable: false),
                    IsBaseClassified = table.Column<bool>(type: "bit", nullable: false),
                    IsLocalClassified = table.Column<bool>(type: "bit", nullable: false),
                    IFRSLineItem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BaseLineItem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LocalLineItem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AllowDirectPosting = table.Column<bool>(type: "bit", nullable: false),
                    IsControlAccount = table.Column<bool>(type: "bit", nullable: false),
                    RequireDepartmentCode = table.Column<bool>(type: "bit", nullable: false),
                    RequireProjectCode = table.Column<bool>(type: "bit", nullable: false),
                    BudgetTrackingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DebitBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreditBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LastTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstateModuleLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayrollModuleLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcurementModuleLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxReportingCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CashFlowClassification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsSystemAccount = table.Column<bool>(type: "bit", nullable: false),
                    InactivatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InactivationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Accounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Accounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountSegmentStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SegmentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SegmentPosition = table.Column<int>(type: "int", nullable: false),
                    SegmentLength = table.Column<int>(type: "int", nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SeparatorCharacter = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    LookupTableRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsReportingDimension = table.Column<bool>(type: "bit", nullable: false),
                    IsNaturalAccount = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AccountSegmentStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountSegmentStructures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TargetCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    InverseRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RateType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    RateSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsManualEntry = table.Column<bool>(type: "bit", nullable: false),
                    APIEndpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    APIResponseMetadata = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HasBeenUsedInTransactions = table.Column<bool>(type: "bit", nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    FirstUsedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RateChangePercentage = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RateChangeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PreviousRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExceedsVarianceThreshold = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeRates_ExchangeRates_PreviousRateId",
                        column: x => x.PreviousRateId,
                        principalTable: "ExchangeRates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountCurrencyLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    RevaluationRequired = table.Column<bool>(type: "bit", nullable: false),
                    RevaluationFrequency = table.Column<int>(type: "int", nullable: false),
                    TransactionRateType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RevaluationRateType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InactivationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasTransactionHistory = table.Column<bool>(type: "bit", nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    FirstTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ForeignCurrencyBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseCurrencyEquivalent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CurrentExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RateEffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRevaluationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRevaluationAdjustment = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CumulativeRevaluationAdjustment = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCurrencyLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountCurrencyLinks_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountCurrencyLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SegmentLookupValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentStructureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentValue = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SegmentLookupValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SegmentLookupValues_AccountSegmentStructures_SegmentStructureId",
                        column: x => x.SegmentStructureId,
                        principalTable: "AccountSegmentStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SegmentLookupValues_SegmentLookupValues_ParentValueId",
                        column: x => x.ParentValueId,
                        principalTable: "SegmentLookupValues",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SegmentLookupValues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountSegmentValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentStructureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentValue = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SegmentLookupValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SegmentValueDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SegmentPosition = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AccountSegmentValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountSegmentValues_AccountSegmentStructures_SegmentStructureId",
                        column: x => x.SegmentStructureId,
                        principalTable: "AccountSegmentStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountSegmentValues_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountSegmentValues_SegmentLookupValues_SegmentLookupValueId",
                        column: x => x.SegmentLookupValueId,
                        principalTable: "SegmentLookupValues",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountSegmentValues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookClassification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OpeningBalanceType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PeriodDebits = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PeriodCredits = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PeriodNetMovement = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ClosingBalanceType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    YearToDateDebits = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    YearToDateCredits = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    YearToDateNetMovement = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SegmentString = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DepartmentSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CostCenterSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocationSegment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BaseCurrencyEquivalent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UnrealizedGainLoss = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    LastTransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTransactionUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsReconciled = table.Column<bool>(type: "bit", nullable: false),
                    LastReconciledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconciliationDiscrepancy = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HasActivity = table.Column<bool>(type: "bit", nullable: false),
                    IsZeroBalance = table.Column<bool>(type: "bit", nullable: false),
                    IsNegativeBalance = table.Column<bool>(type: "bit", nullable: false),
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
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountBalances_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountBalances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ForeignCurrencyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ExchangeRateSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExchangeRateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BookClassification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SegmentString = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsRevaluationEntry = table.Column<bool>(type: "bit", nullable: false),
                    RevaluationBatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RevaluationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TransactionTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_AccountTransactions_OriginalTransactionId",
                        column: x => x.OriginalTransactionId,
                        principalTable: "AccountTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountTransactions_AccountTransactions_ReversalTransactionId",
                        column: x => x.ReversalTransactionId,
                        principalTable: "AccountTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FiscalPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PeriodCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PeriodNumber = table.Column<int>(type: "int", nullable: false),
                    PeriodType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodDays = table.Column<int>(type: "int", nullable: false),
                    PeriodStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsCloseInitiated = table.Column<bool>(type: "bit", nullable: false),
                    CloseInitiatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CloseInitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrialBalanceValidated = table.Column<bool>(type: "bit", nullable: false),
                    TrialBalanceValidatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BankReconciliationComplete = table.Column<bool>(type: "bit", nullable: false),
                    BankReconciliationCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrencyRevaluationComplete = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyRevaluationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepreciationComplete = table.Column<bool>(type: "bit", nullable: false),
                    DepreciationCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InventoryValuationComplete = table.Column<bool>(type: "bit", nullable: false),
                    InventoryValuationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccrualsComplete = table.Column<bool>(type: "bit", nullable: false),
                    AccrualsCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasBeenReopened = table.Column<bool>(type: "bit", nullable: false),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    LastReopenedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReopenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsYearEnd = table.Column<bool>(type: "bit", nullable: false),
                    YearEndCloseComplete = table.Column<bool>(type: "bit", nullable: false),
                    YearEndCloseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YearEndClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalJournalEntries = table.Column<int>(type: "int", nullable: false),
                    TotalTransactionLines = table.Column<int>(type: "int", nullable: false),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceDifference = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ClosingNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AllowBackdating = table.Column<bool>(type: "bit", nullable: false),
                    AllowFutureDating = table.Column<bool>(type: "bit", nullable: false),
                    MaxTransactionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalPeriods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    JournalType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalDebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceDifference = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsBalanced = table.Column<bool>(type: "bit", nullable: false),
                    IsMultiCurrency = table.Column<bool>(type: "bit", nullable: false),
                    PrimaryCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    BookClassification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApprovalWorkflowId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    RecurringTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecurrenceFrequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NextRecurrenceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRevaluationEntry = table.Column<bool>(type: "bit", nullable: false),
                    RevaluationBatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RevaluationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsAutoReversalEntry = table.Column<bool>(type: "bit", nullable: false),
                    IsImported = table.Column<bool>(type: "bit", nullable: false),
                    ImportBatchReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EntryTag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    HasAttachments = table.Column<bool>(type: "bit", nullable: false),
                    AttachmentCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntries_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalEntries_JournalEntries_OriginalJournalEntryId",
                        column: x => x.OriginalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JournalEntries_JournalEntries_ReversalJournalEntryId",
                        column: x => x.ReversalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JournalEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FiscalYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FiscalYearCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    FiscalYearType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalDays = table.Column<int>(type: "int", nullable: false),
                    NumberOfPeriods = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsCloseInitiated = table.Column<bool>(type: "bit", nullable: false),
                    CloseInitiatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CloseInitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllPeriodsClosedValidated = table.Column<bool>(type: "bit", nullable: false),
                    AllPeriodsClosedValidatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalDepreciationComplete = table.Column<bool>(type: "bit", nullable: false),
                    FinalDepreciationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YearEndRevaluationComplete = table.Column<bool>(type: "bit", nullable: false),
                    YearEndRevaluationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YearEndInventoryComplete = table.Column<bool>(type: "bit", nullable: false),
                    YearEndInventoryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YearEndAccrualsComplete = table.Column<bool>(type: "bit", nullable: false),
                    YearEndAccrualsDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YearEndTrialBalanceValidated = table.Column<bool>(type: "bit", nullable: false),
                    YearEndTrialBalanceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetainedEarningsTransferComplete = table.Column<bool>(type: "bit", nullable: false),
                    RetainedEarningsTransferDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosingJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NetIncomeTransferred = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    OpeningBalancesGenerated = table.Column<bool>(type: "bit", nullable: false),
                    OpeningBalancesGeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextFiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpeningBalanceJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HasBeenReopened = table.Column<bool>(type: "bit", nullable: false),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    LastReopenedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReopenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReportingFramework = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TotalJournalEntries = table.Column<int>(type: "int", nullable: false),
                    TotalTransactionLines = table.Column<int>(type: "int", nullable: false),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceDifference = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetIncome = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsBudgetApproved = table.Column<bool>(type: "bit", nullable: false),
                    BudgetApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BudgetApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BudgetedRevenue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BudgetedExpenses = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BudgetedNetIncome = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsAuditComplete = table.Column<bool>(type: "bit", nullable: false),
                    AuditCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditFirm = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuditOpinion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuditReportReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    YearEndClosingNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalYears_FiscalYears_NextFiscalYearId",
                        column: x => x.NextFiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FiscalYears_JournalEntries_ClosingJournalEntryId",
                        column: x => x.ClosingJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FiscalYears_JournalEntries_OpeningBalanceJournalEntryId",
                        column: x => x.OpeningBalanceJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FiscalYears_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9745));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9798));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9801));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9803));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(82));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(100));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(108));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(114));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(128));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(136));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(143));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(151));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(162));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(171));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(183));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(199));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(217));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(224));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(235));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(243));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(303));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(305));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(306));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(307));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(308));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(310));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(311));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(312));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(312));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(314));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(315));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(316));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(317));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(317));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(318));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(319));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(370));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(373));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(374));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(375));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(375));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(376));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(377));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(378));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(379));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(380));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(380));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(381));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(382));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(383));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(384));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(452));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(454));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(456));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(457));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(458));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(459));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(459));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(460));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(461));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(462));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(477));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(479));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 26, 18, 18, 11, 751, DateTimeKind.Utc).AddTicks(480));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("0f2f7c4c-fb83-44c5-8106-d5256939db62"), null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9979), null, null, null, null, null, null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9978), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("1921fba9-bfce-48a5-801d-879278410adc"), null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9962), null, null, null, null, null, null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9961), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("2401dea8-721f-4d08-ab67-cf274389c668"), null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9947), null, null, null, null, null, null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9946), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8330a062-f0c2-40a8-8afb-00dc59a41052"), null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9900), null, null, null, null, null, null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9900), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("be877047-515b-4c98-b25b-aedf5a007b18"), null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9932), null, null, null, null, null, null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9931), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c08bbe23-8102-4b0c-bd7e-1a3269330df0"), null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9880), null, null, null, null, null, null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9876), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e20e0b14-93fd-43aa-862d-db16f58adce2"), null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9916), null, null, null, null, null, null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9916), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "BaseCurrency", "BaseCurrencyName", "CreatedAt", "CurrencyDecimalPlaces", "CurrencySymbol" },
                values: new object[] { "GHS", null, new DateTime(2025, 11, 26, 18, 18, 11, 750, DateTimeKind.Utc).AddTicks(9484), 2, null });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_AccountId",
                table: "AccountBalances",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_FiscalPeriodId",
                table: "AccountBalances",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalances_TenantId",
                table: "AccountBalances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCurrencyLinks_AccountId",
                table: "AccountCurrencyLinks",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCurrencyLinks_TenantId",
                table: "AccountCurrencyLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_ParentAccountId",
                table: "Accounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TenantId",
                table: "Accounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentStructures_TenantId",
                table: "AccountSegmentStructures",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_AccountId",
                table: "AccountSegmentValues",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_SegmentLookupValueId",
                table: "AccountSegmentValues",
                column: "SegmentLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_SegmentStructureId",
                table: "AccountSegmentValues",
                column: "SegmentStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSegmentValues_TenantId",
                table: "AccountSegmentValues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_AccountId",
                table: "AccountTransactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_FiscalPeriodId",
                table: "AccountTransactions",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_JournalEntryId",
                table: "AccountTransactions",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_OriginalTransactionId",
                table: "AccountTransactions",
                column: "OriginalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_ReversalTransactionId",
                table: "AccountTransactions",
                column: "ReversalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_TenantId",
                table: "AccountTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_PreviousRateId",
                table: "ExchangeRates",
                column: "PreviousRateId",
                unique: true,
                filter: "[PreviousRateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_TenantId",
                table: "ExchangeRates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_FiscalYearId",
                table: "FiscalPeriods",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_TenantId",
                table: "FiscalPeriods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_ClosingJournalEntryId",
                table: "FiscalYears",
                column: "ClosingJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_NextFiscalYearId",
                table: "FiscalYears",
                column: "NextFiscalYearId",
                unique: true,
                filter: "[NextFiscalYearId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_OpeningBalanceJournalEntryId",
                table: "FiscalYears",
                column: "OpeningBalanceJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_TenantId",
                table: "FiscalYears",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_FiscalPeriodId",
                table: "JournalEntries",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_OriginalJournalEntryId",
                table: "JournalEntries",
                column: "OriginalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversalJournalEntryId",
                table: "JournalEntries",
                column: "ReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId",
                table: "JournalEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentLookupValues_ParentValueId",
                table: "SegmentLookupValues",
                column: "ParentValueId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentLookupValues_SegmentStructureId",
                table: "SegmentLookupValues",
                column: "SegmentStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentLookupValues_TenantId",
                table: "SegmentLookupValues",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountBalances_FiscalPeriods_FiscalPeriodId",
                table: "AccountBalances",
                column: "FiscalPeriodId",
                principalTable: "FiscalPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountTransactions_FiscalPeriods_FiscalPeriodId",
                table: "AccountTransactions",
                column: "FiscalPeriodId",
                principalTable: "FiscalPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountTransactions_JournalEntries_JournalEntryId",
                table: "AccountTransactions",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FiscalPeriods_FiscalYears_FiscalYearId",
                table: "FiscalPeriods",
                column: "FiscalYearId",
                principalTable: "FiscalYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_FiscalPeriods_FiscalPeriodId",
                table: "JournalEntries");

            migrationBuilder.DropTable(
                name: "AccountBalances");

            migrationBuilder.DropTable(
                name: "AccountCurrencyLinks");

            migrationBuilder.DropTable(
                name: "AccountSegmentValues");

            migrationBuilder.DropTable(
                name: "AccountTransactions");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "SegmentLookupValues");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "AccountSegmentStructures");

            migrationBuilder.DropTable(
                name: "FiscalPeriods");

            migrationBuilder.DropTable(
                name: "FiscalYears");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0f2f7c4c-fb83-44c5-8106-d5256939db62"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("1921fba9-bfce-48a5-801d-879278410adc"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("2401dea8-721f-4d08-ab67-cf274389c668"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8330a062-f0c2-40a8-8afb-00dc59a41052"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("be877047-515b-4c98-b25b-aedf5a007b18"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c08bbe23-8102-4b0c-bd7e-1a3269330df0"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e20e0b14-93fd-43aa-862d-db16f58adce2"));

            migrationBuilder.DropColumn(
                name: "BaseCurrency",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BaseCurrencyName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CurrencyDecimalPlaces",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CurrencySymbol",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BusinessPartnerRegistrationStatusHistories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BusinessPartnerRegistrationStatusHistories");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "BusinessPartnerRegistrationStatusHistories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "BusinessPartnerRegistrationStatusHistories");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "BusinessPartnerRegistrationStatusHistories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "BusinessPartnerRegistrationStatusHistories");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "BusinessPartnerRegistrationStatusHistories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BusinessPartnerRegistrationStatusHistories");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "BusinessPartnerRegistrationStatusHistories");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(8807));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(8897));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(8900));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(8903));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9279));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9312));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9319));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9325));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9336));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9343));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9350));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9356));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9369));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9378));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9384));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9390));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9408));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9436));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9449));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9455));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9535));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9537));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9538));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9539));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9540));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9542));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9543));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9544));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9544));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9546));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9547));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9547));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9548));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9549));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9550));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9551));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9615));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9618));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9619));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9620));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9621));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9622));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9623));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9624));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9625));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9625));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9626));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9627));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9628));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9629));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9630));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9730));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9731));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9733));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9734));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9735));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9736));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9737));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9738));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9739));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9739));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9753));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9755));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9755));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("01424a6f-cd1a-417d-a470-d94111460dc7"), null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9059), null, null, null, null, null, null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9059), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("40bf4967-2216-4121-9e5b-d518c4c13f65"), null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9045), null, null, null, null, null, null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9044), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("47ffa269-7878-4916-a25a-2fbfb327c395"), null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9024), null, null, null, null, null, null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9017), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("816673e8-7979-45a4-854f-c8c008795a8f"), null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9072), null, null, null, null, null, null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9072), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a5d7828a-429f-412e-a135-e0c15a224e2f"), null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9112), null, null, null, null, null, null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9111), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("b2b5e343-1da3-470b-a9d6-881a0bd3f611"), null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9100), null, null, null, null, null, null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9099), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d9d20505-1ff1-4048-8856-cb9066c16588"), null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9086), null, null, null, null, null, null, new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(9085), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 0, 56, 27, 419, DateTimeKind.Utc).AddTicks(8478));
        }
    }
}
