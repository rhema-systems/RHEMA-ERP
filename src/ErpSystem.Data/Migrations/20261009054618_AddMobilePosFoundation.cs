using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePosFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobilePosOfflinePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AuthorizationWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    MaximumTransactionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaximumAggregateAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaximumTransactionCount = table.Column<int>(type: "int", nullable: true),
                    MaximumOfflineAgeMinutes = table.Column<int>(type: "int", nullable: false),
                    AllowCashSale = table.Column<bool>(type: "bit", nullable: false),
                    AllowCashReceipt = table.Column<bool>(type: "bit", nullable: false),
                    AllowPartialPayment = table.Column<bool>(type: "bit", nullable: false),
                    AllowReturns = table.Column<bool>(type: "bit", nullable: false),
                    AllowReversals = table.Column<bool>(type: "bit", nullable: false),
                    AllowProvisionalReceipt = table.Column<bool>(type: "bit", nullable: false),
                    AllowDayEndSubmissionWithPendingSync = table.Column<bool>(type: "bit", nullable: false),
                    RequireExternalReferenceForElectronicTender = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_MobilePosOfflinePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePosOfflinePolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosStores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompanyProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultWalkInBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultWalkInBusinessPartnerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfflinePolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_MobilePosStores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePosStores_BusinessPartnerRoles_DefaultWalkInBusinessPartnerRoleId",
                        column: x => x.DefaultWalkInBusinessPartnerRoleId,
                        principalTable: "BusinessPartnerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosStores_BusinessPartners_DefaultWalkInBusinessPartnerId",
                        column: x => x.DefaultWalkInBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosStores_CompanyProfiles_CompanyProfileId",
                        column: x => x.CompanyProfileId,
                        principalTable: "CompanyProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosStores_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosStores_MobilePosOfflinePolicies_OfflinePolicyId",
                        column: x => x.OfflinePolicyId,
                        principalTable: "MobilePosOfflinePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosStores_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosStores_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosStoreDimensionDefaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosStoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceDimensionDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceDimensionValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_MobilePosStoreDimensionDefaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePosStoreDimensionDefaults_FinanceDimensionDefinitions_FinanceDimensionDefinitionId",
                        column: x => x.FinanceDimensionDefinitionId,
                        principalTable: "FinanceDimensionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosStoreDimensionDefaults_FinanceDimensionValues_FinanceDimensionValueId",
                        column: x => x.FinanceDimensionValueId,
                        principalTable: "FinanceDimensionValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosStoreDimensionDefaults_MobilePosStores_MobilePosStoreId",
                        column: x => x.MobilePosStoreId,
                        principalTable: "MobilePosStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MobilePosStoreDimensionDefaults_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosTills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosStoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TillNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LiquidityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastHeartbeatAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_MobilePosTills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePosTills_LiquidityAccounts_LiquidityAccountId",
                        column: x => x.LiquidityAccountId,
                        principalTable: "LiquidityAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTills_MobilePosStores_MobilePosStoreId",
                        column: x => x.MobilePosStoreId,
                        principalTable: "MobilePosStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTills_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosUserStoreAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosStoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_MobilePosUserStoreAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePosUserStoreAssignments_MobilePosStores_MobilePosStoreId",
                        column: x => x.MobilePosStoreId,
                        principalTable: "MobilePosStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosUserStoreAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosUserStoreAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallationIdHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PublicKeyThumbprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DeviceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OperatingSystemVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AppVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PrinterAdapterKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ScannerAdapterKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MobilePosStoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MobilePosTillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocationEpoch = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_MobilePosDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePosDevices_MobilePosStores_MobilePosStoreId",
                        column: x => x.MobilePosStoreId,
                        principalTable: "MobilePosStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosDevices_MobilePosTills_MobilePosTillId",
                        column: x => x.MobilePosTillId,
                        principalTable: "MobilePosTills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosDevices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosDevices_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosDevices_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosDevices_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosTillPaymentMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosTillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowOnline = table.Column<bool>(type: "bit", nullable: false),
                    AllowOffline = table.Column<bool>(type: "bit", nullable: false),
                    RequireExternalAuthorizationReference = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MobilePosTillPaymentMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePosTillPaymentMethods_MobilePosTills_MobilePosTillId",
                        column: x => x.MobilePosTillId,
                        principalTable: "MobilePosTills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MobilePosTillPaymentMethods_PaymentMethod_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTillPaymentMethods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosDeviceAssignmentHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousTillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewStoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewTillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
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
                    table.PrimaryKey("PK_MobilePosDeviceAssignmentHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePosDeviceAssignmentHistories_MobilePosDevices_MobilePosDeviceId",
                        column: x => x.MobilePosDeviceId,
                        principalTable: "MobilePosDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MobilePosDeviceAssignmentHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosOfflineGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosStoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosTillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashierTillSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosOfflinePolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RevocationEpoch = table.Column<long>(type: "bigint", nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicySnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_MobilePosOfflineGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePosOfflineGrants_MobilePosDevices_MobilePosDeviceId",
                        column: x => x.MobilePosDeviceId,
                        principalTable: "MobilePosDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosOfflineGrants_MobilePosOfflinePolicies_MobilePosOfflinePolicyId",
                        column: x => x.MobilePosOfflinePolicyId,
                        principalTable: "MobilePosOfflinePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosOfflineGrants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosDeviceAssignmentHistories_MobilePosDeviceId",
                table: "MobilePosDeviceAssignmentHistories",
                column: "MobilePosDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosDeviceAssignmentHistories_TenantId_MobilePosDeviceId_ChangedAtUtc",
                table: "MobilePosDeviceAssignmentHistories",
                columns: new[] { "TenantId", "MobilePosDeviceId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosDevices_ApprovedByUserId",
                table: "MobilePosDevices",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosDevices_MobilePosStoreId",
                table: "MobilePosDevices",
                column: "MobilePosStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosDevices_MobilePosTillId",
                table: "MobilePosDevices",
                column: "MobilePosTillId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosDevices_RequestedByUserId",
                table: "MobilePosDevices",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosDevices_RevokedByUserId",
                table: "MobilePosDevices",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosDevices_TenantId_InstallationIdHash",
                table: "MobilePosDevices",
                columns: new[] { "TenantId", "InstallationIdHash" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosDevices_TenantId_MobilePosTillId",
                table: "MobilePosDevices",
                columns: new[] { "TenantId", "MobilePosTillId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [MobilePosTillId] IS NOT NULL AND [Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosDevices_TenantId_Status_LastSeenAtUtc",
                table: "MobilePosDevices",
                columns: new[] { "TenantId", "Status", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosOfflineGrants_MobilePosDeviceId",
                table: "MobilePosOfflineGrants",
                column: "MobilePosDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosOfflineGrants_MobilePosOfflinePolicyId",
                table: "MobilePosOfflineGrants",
                column: "MobilePosOfflinePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosOfflineGrants_TenantId_CashierTillSessionId_Status",
                table: "MobilePosOfflineGrants",
                columns: new[] { "TenantId", "CashierTillSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosOfflineGrants_TenantId_MobilePosDeviceId_Status_ExpiresAtUtc",
                table: "MobilePosOfflineGrants",
                columns: new[] { "TenantId", "MobilePosDeviceId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosOfflinePolicies_TenantId_IsActive",
                table: "MobilePosOfflinePolicies",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosOfflinePolicies_TenantId_Name",
                table: "MobilePosOfflinePolicies",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStoreDimensionDefaults_FinanceDimensionDefinitionId",
                table: "MobilePosStoreDimensionDefaults",
                column: "FinanceDimensionDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStoreDimensionDefaults_FinanceDimensionValueId",
                table: "MobilePosStoreDimensionDefaults",
                column: "FinanceDimensionValueId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStoreDimensionDefaults_MobilePosStoreId",
                table: "MobilePosStoreDimensionDefaults",
                column: "MobilePosStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStoreDimensionDefaults_TenantId_MobilePosStoreId_FinanceDimensionDefinitionId",
                table: "MobilePosStoreDimensionDefaults",
                columns: new[] { "TenantId", "MobilePosStoreId", "FinanceDimensionDefinitionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStores_CompanyProfileId",
                table: "MobilePosStores",
                column: "CompanyProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStores_DefaultWalkInBusinessPartnerId",
                table: "MobilePosStores",
                column: "DefaultWalkInBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStores_DefaultWalkInBusinessPartnerRoleId",
                table: "MobilePosStores",
                column: "DefaultWalkInBusinessPartnerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStores_LocationId",
                table: "MobilePosStores",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStores_OfflinePolicyId",
                table: "MobilePosStores",
                column: "OfflinePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStores_TenantId_Code",
                table: "MobilePosStores",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStores_TenantId_DefaultWalkInBusinessPartnerId",
                table: "MobilePosStores",
                columns: new[] { "TenantId", "DefaultWalkInBusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStores_TenantId_Status",
                table: "MobilePosStores",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosStores_WarehouseId",
                table: "MobilePosStores",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillPaymentMethods_MobilePosTillId",
                table: "MobilePosTillPaymentMethods",
                column: "MobilePosTillId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillPaymentMethods_PaymentMethodId",
                table: "MobilePosTillPaymentMethods",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTillPaymentMethods_TenantId_MobilePosTillId_PaymentMethodId",
                table: "MobilePosTillPaymentMethods",
                columns: new[] { "TenantId", "MobilePosTillId", "PaymentMethodId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTills_LiquidityAccountId",
                table: "MobilePosTills",
                column: "LiquidityAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTills_MobilePosStoreId",
                table: "MobilePosTills",
                column: "MobilePosStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTills_TenantId_LiquidityAccountId",
                table: "MobilePosTills",
                columns: new[] { "TenantId", "LiquidityAccountId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTills_TenantId_TillNumber",
                table: "MobilePosTills",
                columns: new[] { "TenantId", "TillNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosUserStoreAssignments_MobilePosStoreId",
                table: "MobilePosUserStoreAssignments",
                column: "MobilePosStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosUserStoreAssignments_TenantId_MobilePosStoreId_IsActive",
                table: "MobilePosUserStoreAssignments",
                columns: new[] { "TenantId", "MobilePosStoreId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosUserStoreAssignments_TenantId_UserId",
                table: "MobilePosUserStoreAssignments",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosUserStoreAssignments_UserId",
                table: "MobilePosUserStoreAssignments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobilePosDeviceAssignmentHistories");

            migrationBuilder.DropTable(
                name: "MobilePosOfflineGrants");

            migrationBuilder.DropTable(
                name: "MobilePosStoreDimensionDefaults");

            migrationBuilder.DropTable(
                name: "MobilePosTillPaymentMethods");

            migrationBuilder.DropTable(
                name: "MobilePosUserStoreAssignments");

            migrationBuilder.DropTable(
                name: "MobilePosDevices");

            migrationBuilder.DropTable(
                name: "MobilePosTills");

            migrationBuilder.DropTable(
                name: "MobilePosStores");

            migrationBuilder.DropTable(
                name: "MobilePosOfflinePolicies");
        }
    }
}
