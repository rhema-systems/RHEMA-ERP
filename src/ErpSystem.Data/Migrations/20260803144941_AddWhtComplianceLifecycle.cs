using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Keep discovery metadata on the executable migration because routine Debug builds omit EF's
    // very large generated designer history. This prevents startup migration and command-line
    // deployment from silently overlooking the Finance WHT compliance schema.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803144941_AddWhtComplianceLifecycle")]
    /// <inheritdoc />
    public partial class AddWhtComplianceLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingTaxBaseAmount",
                table: "VendorPayment",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "WithholdingTaxCalculationNote",
                table: "VendorPayment",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingTaxCumulativeBefore",
                table: "VendorPayment",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingTaxThresholdAmount",
                table: "VendorPayment",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WithholdingTaxThresholdApplied",
                table: "VendorPayment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "WithholdingTaxCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "date", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuedByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupersedesCertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersededByCertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LifecycleReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CancelledByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PaymentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SupplierTin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TaxName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxableBase = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WithholdingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_WithholdingTaxCertificates", x => x.Id);
                    table.CheckConstraint("CK_WithholdingTaxCertificates_Amounts", "[TaxableBase] >= 0 AND [WithholdingAmount] > 0 AND [NetPaidAmount] >= 0");
                    table.CheckConstraint("CK_WithholdingTaxCertificates_Version", "[VersionNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_WithholdingTaxCertificates_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithholdingTaxCertificates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithholdingTaxCertificates_VendorPayment_VendorPaymentId",
                        column: x => x.VendorPaymentId,
                        principalTable: "VendorPayment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithholdingTaxCertificates_WithholdingTaxCertificates_SupersededByCertificateId",
                        column: x => x.SupersededByCertificateId,
                        principalTable: "WithholdingTaxCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithholdingTaxCertificates_WithholdingTaxCertificates_SupersedesCertificateId",
                        column: x => x.SupersedesCertificateId,
                        principalTable: "WithholdingTaxCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WithholdingTaxRemittances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemittanceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateTime>(type: "date", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalWithholdingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubmissionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "date", nullable: true),
                    AuthorityReceiptReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PaidByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CancelledByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_WithholdingTaxRemittances", x => x.Id);
                    table.CheckConstraint("CK_WithholdingTaxRemittances_Period", "[PeriodTo] >= [PeriodFrom] AND [DueDate] >= [PeriodTo]");
                    table.CheckConstraint("CK_WithholdingTaxRemittances_Total", "[TotalWithholdingAmount] > 0");
                    table.ForeignKey(
                        name: "FK_WithholdingTaxRemittances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WithholdingTaxRemittanceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemittanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SupplierTin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "date", nullable: false),
                    TaxableBase = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WithholdingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_WithholdingTaxRemittanceLines", x => x.Id);
                    table.CheckConstraint("CK_WithholdingTaxRemittanceLines_Amounts", "[TaxableBase] >= 0 AND [WithholdingAmount] > 0");
                    table.ForeignKey(
                        name: "FK_WithholdingTaxRemittanceLines_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithholdingTaxRemittanceLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithholdingTaxRemittanceLines_VendorPayment_VendorPaymentId",
                        column: x => x.VendorPaymentId,
                        principalTable: "VendorPayment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithholdingTaxRemittanceLines_WithholdingTaxCertificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "WithholdingTaxCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithholdingTaxRemittanceLines_WithholdingTaxRemittances_RemittanceId",
                        column: x => x.RemittanceId,
                        principalTable: "WithholdingTaxRemittances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxCertificates_JournalEntryId",
                table: "WithholdingTaxCertificates",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxCertificates_SupersededByCertificateId",
                table: "WithholdingTaxCertificates",
                column: "SupersededByCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxCertificates_SupersedesCertificateId",
                table: "WithholdingTaxCertificates",
                column: "SupersedesCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxCertificates_TenantId_CertificateNumber",
                table: "WithholdingTaxCertificates",
                columns: new[] { "TenantId", "CertificateNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxCertificates_TenantId_VendorPaymentId",
                table: "WithholdingTaxCertificates",
                columns: new[] { "TenantId", "VendorPaymentId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxCertificates_TenantId_VendorPaymentId_VersionNumber",
                table: "WithholdingTaxCertificates",
                columns: new[] { "TenantId", "VendorPaymentId", "VersionNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxCertificates_VendorPaymentId",
                table: "WithholdingTaxCertificates",
                column: "VendorPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxRemittanceLines_CertificateId",
                table: "WithholdingTaxRemittanceLines",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxRemittanceLines_JournalEntryId",
                table: "WithholdingTaxRemittanceLines",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxRemittanceLines_RemittanceId",
                table: "WithholdingTaxRemittanceLines",
                column: "RemittanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxRemittanceLines_TenantId_RemittanceId_PaymentNumber",
                table: "WithholdingTaxRemittanceLines",
                columns: new[] { "TenantId", "RemittanceId", "PaymentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxRemittanceLines_TenantId_VendorPaymentId",
                table: "WithholdingTaxRemittanceLines",
                columns: new[] { "TenantId", "VendorPaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxRemittanceLines_VendorPaymentId",
                table: "WithholdingTaxRemittanceLines",
                column: "VendorPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxRemittances_TenantId_RemittanceNumber",
                table: "WithholdingTaxRemittances",
                columns: new[] { "TenantId", "RemittanceNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WithholdingTaxRemittances_TenantId_Status_PeriodTo",
                table: "WithholdingTaxRemittances",
                columns: new[] { "TenantId", "Status", "PeriodTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WithholdingTaxRemittanceLines");

            migrationBuilder.DropTable(
                name: "WithholdingTaxCertificates");

            migrationBuilder.DropTable(
                name: "WithholdingTaxRemittances");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxBaseAmount",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxCalculationNote",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxCumulativeBefore",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxThresholdAmount",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxThresholdApplied",
                table: "VendorPayment");
        }
    }
}
