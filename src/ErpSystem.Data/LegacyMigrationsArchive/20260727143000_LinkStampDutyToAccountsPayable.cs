using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260727143000_LinkStampDutyToAccountsPayable")]
    public partial class LinkStampDutyToAccountsPayable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Suppliers', 'IsWithholdingTaxApplicable') IS NULL
                    ALTER TABLE dbo.Suppliers ADD IsWithholdingTaxApplicable bit NOT NULL CONSTRAINT DF_Suppliers_IsWithholdingTaxApplicable_LandAcquisition DEFAULT 0;
                IF COL_LENGTH('dbo.Suppliers', 'TaxTreatment') IS NULL
                    ALTER TABLE dbo.Suppliers ADD TaxTreatment int NOT NULL CONSTRAINT DF_Suppliers_TaxTreatment_LandAcquisition DEFAULT 1;

                IF COL_LENGTH('dbo.VendorInvoice', 'IsOpeningBalance') IS NULL
                    ALTER TABLE dbo.VendorInvoice ADD IsOpeningBalance bit NOT NULL CONSTRAINT DF_VendorInvoice_IsOpeningBalance_LandAcquisition DEFAULT 0;
                IF COL_LENGTH('dbo.VendorInvoice', 'WithholdingTaxId') IS NULL
                    ALTER TABLE dbo.VendorInvoice ADD WithholdingTaxId uniqueidentifier NULL;
                IF COL_LENGTH('dbo.VendorInvoice', 'WithholdingTaxAccountId') IS NULL
                    ALTER TABLE dbo.VendorInvoice ADD WithholdingTaxAccountId uniqueidentifier NULL;
                IF COL_LENGTH('dbo.VendorInvoice', 'WithholdingCertificateNumber') IS NULL
                    ALTER TABLE dbo.VendorInvoice ADD WithholdingCertificateNumber nvarchar(100) NULL;
                IF COL_LENGTH('dbo.VendorInvoice', 'WithholdingCertificateDate') IS NULL
                    ALTER TABLE dbo.VendorInvoice ADD WithholdingCertificateDate datetime2 NULL;

                IF COL_LENGTH('dbo.VendorInvoiceLineItem', 'FixedAssetId') IS NULL
                    ALTER TABLE dbo.VendorInvoiceLineItem ADD FixedAssetId uniqueidentifier NULL;
                IF COL_LENGTH('dbo.VendorInvoiceLineItem', 'CapitalizationJournalEntryId') IS NULL
                    ALTER TABLE dbo.VendorInvoiceLineItem ADD CapitalizationJournalEntryId uniqueidentifier NULL;
                IF COL_LENGTH('dbo.VendorInvoiceLineItem', 'CapitalizationPostingEventId') IS NULL
                    ALTER TABLE dbo.VendorInvoiceLineItem ADD CapitalizationPostingEventId uniqueidentifier NULL;
                IF COL_LENGTH('dbo.VendorInvoiceLineItem', 'CapitalizedAt') IS NULL
                    ALTER TABLE dbo.VendorInvoiceLineItem ADD CapitalizedAt datetime2 NULL;
                IF COL_LENGTH('dbo.VendorInvoiceLineItem', 'TaxGroupId') IS NULL
                    ALTER TABLE dbo.VendorInvoiceLineItem ADD TaxGroupId uniqueidentifier NULL;
                IF COL_LENGTH('dbo.VendorInvoiceLineItem', 'TaxTreatment') IS NULL
                    ALTER TABLE dbo.VendorInvoiceLineItem ADD TaxTreatment int NOT NULL CONSTRAINT DF_VendorInvoiceLineItem_TaxTreatment_LandAcquisition DEFAULT 1;

                IF COL_LENGTH('dbo.VendorPayment', 'IsSupplierAdvance') IS NULL
                    ALTER TABLE dbo.VendorPayment ADD IsSupplierAdvance bit NOT NULL CONSTRAINT DF_VendorPayment_IsSupplierAdvance_LandAcquisition DEFAULT 0;
                IF COL_LENGTH('dbo.VendorPayment', 'PaymentMethodId') IS NULL
                    ALTER TABLE dbo.VendorPayment ADD PaymentMethodId uniqueidentifier NULL;
                IF COL_LENGTH('dbo.VendorPayment', 'WithholdingTaxId') IS NULL
                    ALTER TABLE dbo.VendorPayment ADD WithholdingTaxId uniqueidentifier NULL;
                IF COL_LENGTH('dbo.VendorPayment', 'WithholdingTaxAccountId') IS NULL
                    ALTER TABLE dbo.VendorPayment ADD WithholdingTaxAccountId uniqueidentifier NULL;
                IF COL_LENGTH('dbo.VendorPayment', 'WithholdingCertificateNumber') IS NULL
                    ALTER TABLE dbo.VendorPayment ADD WithholdingCertificateNumber nvarchar(100) NULL;
                IF COL_LENGTH('dbo.VendorPayment', 'WithholdingCertificateDate') IS NULL
                    ALTER TABLE dbo.VendorPayment ADD WithholdingCertificateDate datetime2 NULL;

                IF COL_LENGTH('dbo.VendorPaymentAllocation', 'ApplicationJournalEntryId') IS NULL
                    ALTER TABLE dbo.VendorPaymentAllocation ADD ApplicationJournalEntryId uniqueidentifier NULL;
                IF COL_LENGTH('dbo.VendorPaymentAllocation', 'ApplicationPostingEventId') IS NULL
                    ALTER TABLE dbo.VendorPaymentAllocation ADD ApplicationPostingEventId uniqueidentifier NULL;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountsPayableInvoiceId",
                table: "StampDutyPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountsPayablePaymentId",
                table: "StampDutyPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountsPayableSupplierId",
                table: "StampDutyPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampDutyPayments_TenantId_AccountsPayableInvoiceId",
                table: "StampDutyPayments",
                columns: new[] { "TenantId", "AccountsPayableInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_StampDutyPayments_TenantId_AccountsPayablePaymentId",
                table: "StampDutyPayments",
                columns: new[] { "TenantId", "AccountsPayablePaymentId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StampDutyPayments_TenantId_AccountsPayableInvoiceId",
                table: "StampDutyPayments");

            migrationBuilder.DropIndex(
                name: "IX_StampDutyPayments_TenantId_AccountsPayablePaymentId",
                table: "StampDutyPayments");

            migrationBuilder.DropColumn(
                name: "AccountsPayableInvoiceId",
                table: "StampDutyPayments");

            migrationBuilder.DropColumn(
                name: "AccountsPayablePaymentId",
                table: "StampDutyPayments");

            migrationBuilder.DropColumn(
                name: "AccountsPayableSupplierId",
                table: "StampDutyPayments");
        }
    }
}
