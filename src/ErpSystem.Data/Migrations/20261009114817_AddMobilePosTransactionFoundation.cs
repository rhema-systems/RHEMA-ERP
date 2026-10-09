using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePosTransactionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobilePosSales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosStoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosTillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashierTillSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientMutationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsedStoreDefaultCustomer = table.Column<bool>(type: "bit", nullable: false),
                    BusinessDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MobilePosOfflineGrantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OfflinePolicySnapshotHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SynchronizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_MobilePosSales", x => x.Id);
                    table.CheckConstraint("CK_MobilePosSales_Amounts", "[SubTotal] >= 0 AND [TaxAmount] >= 0 AND [DiscountAmount] >= 0 AND [TotalAmount] >= 0");
                    table.CheckConstraint("CK_MobilePosSales_PolicyHash", "[OfflinePolicySnapshotHash] IS NULL OR LEN([OfflinePolicySnapshotHash]) = 64");
                    table.ForeignKey(
                        name: "FK_MobilePosSales_BusinessPartnerRoles_BusinessPartnerRoleId",
                        column: x => x.BusinessPartnerRoleId,
                        principalTable: "BusinessPartnerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSales_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSales_CashierTillSessions_CashierTillSessionId",
                        column: x => x.CashierTillSessionId,
                        principalTable: "CashierTillSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSales_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSales_MobilePosDevices_MobilePosDeviceId",
                        column: x => x.MobilePosDeviceId,
                        principalTable: "MobilePosDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSales_MobilePosOfflineGrants_MobilePosOfflineGrantId",
                        column: x => x.MobilePosOfflineGrantId,
                        principalTable: "MobilePosOfflineGrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSales_MobilePosStores_MobilePosStoreId",
                        column: x => x.MobilePosStoreId,
                        principalTable: "MobilePosStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSales_MobilePosTills_MobilePosTillId",
                        column: x => x.MobilePosTillId,
                        principalTable: "MobilePosTills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSales_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSales_Users_OperatorUserId",
                        column: x => x.OperatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobileMutationReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientMutationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CommandType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReplayCount = table.Column<int>(type: "int", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorDetail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MobilePosSaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CanonicalInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CanonicalCustomerPaymentIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MobileMutationReceipts", x => x.Id);
                    table.CheckConstraint("CK_MobileMutationReceipts_Hashes", "LEN([RequestHash]) = 64 AND ([ResultHash] IS NULL OR LEN([ResultHash]) = 64)");
                    table.CheckConstraint("CK_MobileMutationReceipts_SchemaVersion", "[SchemaVersion] > 0");
                    table.ForeignKey(
                        name: "FK_MobileMutationReceipts_Invoices_CanonicalInvoiceId",
                        column: x => x.CanonicalInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobileMutationReceipts_MobilePosDevices_MobilePosDeviceId",
                        column: x => x.MobilePosDeviceId,
                        principalTable: "MobilePosDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobileMutationReceipts_MobilePosSales_MobilePosSaleId",
                        column: x => x.MobilePosSaleId,
                        principalTable: "MobilePosSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobileMutationReceipts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosSaleLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosSaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_MobilePosSaleLines", x => x.Id);
                    table.CheckConstraint("CK_MobilePosSaleLines_Values", "[Sequence] > 0 AND [Quantity] > 0 AND [UnitPrice] >= 0 AND [DiscountAmount] >= 0 AND [TaxAmount] >= 0 AND [LineTotal] >= 0");
                    table.ForeignKey(
                        name: "FK_MobilePosSaleLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSaleLines_MobilePosSales_MobilePosSaleId",
                        column: x => x.MobilePosSaleId,
                        principalTable: "MobilePosSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MobilePosSaleLines_TaxGroups_TaxGroupId",
                        column: x => x.TaxGroupId,
                        principalTable: "TaxGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosSaleLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosTenders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosSaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LiquidityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProviderStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    WasRecordedOffline = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MobilePosTenders", x => x.Id);
                    table.CheckConstraint("CK_MobilePosTenders_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_MobilePosTenders_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTenders_CustomerPayment_CustomerPaymentId",
                        column: x => x.CustomerPaymentId,
                        principalTable: "CustomerPayment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTenders_LiquidityAccounts_LiquidityAccountId",
                        column: x => x.LiquidityAccountId,
                        principalTable: "LiquidityAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTenders_MobilePosSales_MobilePosSaleId",
                        column: x => x.MobilePosSaleId,
                        principalTable: "MobilePosSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MobilePosTenders_PaymentMethod_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosTenders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileMutationReceipts_CanonicalInvoiceId",
                table: "MobileMutationReceipts",
                column: "CanonicalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_MobileMutationReceipts_MobilePosDeviceId",
                table: "MobileMutationReceipts",
                column: "MobilePosDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MobileMutationReceipts_MobilePosSaleId",
                table: "MobileMutationReceipts",
                column: "MobilePosSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_MobileMutationReceipts_TenantId_MobilePosDeviceId_ClientMutationId",
                table: "MobileMutationReceipts",
                columns: new[] { "TenantId", "MobilePosDeviceId", "ClientMutationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobileMutationReceipts_TenantId_Status_LastAttemptAtUtc",
                table: "MobileMutationReceipts",
                columns: new[] { "TenantId", "Status", "LastAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSaleLines_InventoryItemId",
                table: "MobilePosSaleLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSaleLines_MobilePosSaleId",
                table: "MobilePosSaleLines",
                column: "MobilePosSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSaleLines_TaxGroupId",
                table: "MobilePosSaleLines",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSaleLines_TenantId_MobilePosSaleId_ClientLineId",
                table: "MobilePosSaleLines",
                columns: new[] { "TenantId", "MobilePosSaleId", "ClientLineId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSaleLines_TenantId_MobilePosSaleId_Sequence",
                table: "MobilePosSaleLines",
                columns: new[] { "TenantId", "MobilePosSaleId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_BusinessPartnerId",
                table: "MobilePosSales",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_BusinessPartnerRoleId",
                table: "MobilePosSales",
                column: "BusinessPartnerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_CashierTillSessionId",
                table: "MobilePosSales",
                column: "CashierTillSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_InvoiceId",
                table: "MobilePosSales",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_MobilePosDeviceId",
                table: "MobilePosSales",
                column: "MobilePosDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_MobilePosOfflineGrantId",
                table: "MobilePosSales",
                column: "MobilePosOfflineGrantId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_MobilePosStoreId",
                table: "MobilePosSales",
                column: "MobilePosStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_MobilePosTillId",
                table: "MobilePosSales",
                column: "MobilePosTillId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_OperatorUserId",
                table: "MobilePosSales",
                column: "OperatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_TenantId_InvoiceId",
                table: "MobilePosSales",
                columns: new[] { "TenantId", "InvoiceId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [InvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_TenantId_MobilePosDeviceId_ClientMutationId",
                table: "MobilePosSales",
                columns: new[] { "TenantId", "MobilePosDeviceId", "ClientMutationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_TenantId_MobilePosDeviceId_LocalReference",
                table: "MobilePosSales",
                columns: new[] { "TenantId", "MobilePosDeviceId", "LocalReference" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosSales_TenantId_MobilePosStoreId_BusinessDate_Status",
                table: "MobilePosSales",
                columns: new[] { "TenantId", "MobilePosStoreId", "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTenders_BankAccountId",
                table: "MobilePosTenders",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTenders_CustomerPaymentId",
                table: "MobilePosTenders",
                column: "CustomerPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTenders_LiquidityAccountId",
                table: "MobilePosTenders",
                column: "LiquidityAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTenders_MobilePosSaleId",
                table: "MobilePosTenders",
                column: "MobilePosSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTenders_PaymentMethodId",
                table: "MobilePosTenders",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTenders_TenantId_CustomerPaymentId",
                table: "MobilePosTenders",
                columns: new[] { "TenantId", "CustomerPaymentId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [CustomerPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosTenders_TenantId_MobilePosSaleId_Sequence",
                table: "MobilePosTenders",
                columns: new[] { "TenantId", "MobilePosSaleId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileMutationReceipts");

            migrationBuilder.DropTable(
                name: "MobilePosSaleLines");

            migrationBuilder.DropTable(
                name: "MobilePosTenders");

            migrationBuilder.DropTable(
                name: "MobilePosSales");
        }
    }
}
