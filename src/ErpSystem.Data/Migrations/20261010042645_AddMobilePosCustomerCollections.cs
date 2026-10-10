using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePosCustomerCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MobilePosCollectionId",
                table: "MobileMutationReceipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MobilePosCollections",
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
                    BusinessDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MobilePosCollections", x => x.Id);
                    table.CheckConstraint("CK_MobilePosCollections_TotalAmount", "[TotalAmount] > 0");
                    table.ForeignKey(
                        name: "FK_MobilePosCollections_BusinessPartnerRoles_BusinessPartnerRoleId",
                        column: x => x.BusinessPartnerRoleId,
                        principalTable: "BusinessPartnerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollections_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollections_CashierTillSessions_CashierTillSessionId",
                        column: x => x.CashierTillSessionId,
                        principalTable: "CashierTillSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollections_MobilePosDevices_MobilePosDeviceId",
                        column: x => x.MobilePosDeviceId,
                        principalTable: "MobilePosDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollections_MobilePosStores_MobilePosStoreId",
                        column: x => x.MobilePosStoreId,
                        principalTable: "MobilePosStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollections_MobilePosTills_MobilePosTillId",
                        column: x => x.MobilePosTillId,
                        principalTable: "MobilePosTills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollections_Users_OperatorUserId",
                        column: x => x.OperatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosCollectionAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosCollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_MobilePosCollectionAllocations", x => x.Id);
                    table.CheckConstraint("CK_MobilePosCollectionAllocations_Values", "[Sequence] > 0 AND [Amount] > 0");
                    table.ForeignKey(
                        name: "FK_MobilePosCollectionAllocations_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollectionAllocations_MobilePosCollections_MobilePosCollectionId",
                        column: x => x.MobilePosCollectionId,
                        principalTable: "MobilePosCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MobilePosCollectionAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePosCollectionTenders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MobilePosCollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LiquidityAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_MobilePosCollectionTenders", x => x.Id);
                    table.CheckConstraint("CK_MobilePosCollectionTenders_Values", "[Sequence] > 0 AND [Amount] > 0");
                    table.ForeignKey(
                        name: "FK_MobilePosCollectionTenders_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollectionTenders_CustomerPayment_CustomerPaymentId",
                        column: x => x.CustomerPaymentId,
                        principalTable: "CustomerPayment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollectionTenders_LiquidityAccounts_LiquidityAccountId",
                        column: x => x.LiquidityAccountId,
                        principalTable: "LiquidityAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollectionTenders_MobilePosCollections_MobilePosCollectionId",
                        column: x => x.MobilePosCollectionId,
                        principalTable: "MobilePosCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MobilePosCollectionTenders_PaymentMethod_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilePosCollectionTenders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileMutationReceipts_MobilePosCollectionId",
                table: "MobileMutationReceipts",
                column: "MobilePosCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionAllocations_InvoiceId",
                table: "MobilePosCollectionAllocations",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionAllocations_MobilePosCollectionId",
                table: "MobilePosCollectionAllocations",
                column: "MobilePosCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionAllocations_TenantId_MobilePosCollectionId_InvoiceId",
                table: "MobilePosCollectionAllocations",
                columns: new[] { "TenantId", "MobilePosCollectionId", "InvoiceId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionAllocations_TenantId_MobilePosCollectionId_Sequence",
                table: "MobilePosCollectionAllocations",
                columns: new[] { "TenantId", "MobilePosCollectionId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_BusinessPartnerId",
                table: "MobilePosCollections",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_BusinessPartnerRoleId",
                table: "MobilePosCollections",
                column: "BusinessPartnerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_CashierTillSessionId",
                table: "MobilePosCollections",
                column: "CashierTillSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_MobilePosDeviceId",
                table: "MobilePosCollections",
                column: "MobilePosDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_MobilePosStoreId",
                table: "MobilePosCollections",
                column: "MobilePosStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_MobilePosTillId",
                table: "MobilePosCollections",
                column: "MobilePosTillId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_OperatorUserId",
                table: "MobilePosCollections",
                column: "OperatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_TenantId_MobilePosDeviceId_ClientMutationId",
                table: "MobilePosCollections",
                columns: new[] { "TenantId", "MobilePosDeviceId", "ClientMutationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_TenantId_MobilePosDeviceId_LocalReference",
                table: "MobilePosCollections",
                columns: new[] { "TenantId", "MobilePosDeviceId", "LocalReference" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_TenantId_MobilePosStoreId_BusinessDate_Status",
                table: "MobilePosCollections",
                columns: new[] { "TenantId", "MobilePosStoreId", "BusinessDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionTenders_BankAccountId",
                table: "MobilePosCollectionTenders",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionTenders_CustomerPaymentId",
                table: "MobilePosCollectionTenders",
                column: "CustomerPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionTenders_LiquidityAccountId",
                table: "MobilePosCollectionTenders",
                column: "LiquidityAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionTenders_MobilePosCollectionId",
                table: "MobilePosCollectionTenders",
                column: "MobilePosCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionTenders_PaymentMethodId",
                table: "MobilePosCollectionTenders",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionTenders_TenantId_CustomerPaymentId",
                table: "MobilePosCollectionTenders",
                columns: new[] { "TenantId", "CustomerPaymentId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollectionTenders_TenantId_MobilePosCollectionId_Sequence",
                table: "MobilePosCollectionTenders",
                columns: new[] { "TenantId", "MobilePosCollectionId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_MobileMutationReceipts_MobilePosCollections_MobilePosCollectionId",
                table: "MobileMutationReceipts",
                column: "MobilePosCollectionId",
                principalTable: "MobilePosCollections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MobileMutationReceipts_MobilePosCollections_MobilePosCollectionId",
                table: "MobileMutationReceipts");

            migrationBuilder.DropTable(
                name: "MobilePosCollectionAllocations");

            migrationBuilder.DropTable(
                name: "MobilePosCollectionTenders");

            migrationBuilder.DropTable(
                name: "MobilePosCollections");

            migrationBuilder.DropIndex(
                name: "IX_MobileMutationReceipts_MobilePosCollectionId",
                table: "MobileMutationReceipts");

            migrationBuilder.DropColumn(
                name: "MobilePosCollectionId",
                table: "MobileMutationReceipts");
        }
    }
}
