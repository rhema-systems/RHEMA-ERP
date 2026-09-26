using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalWhtBusinessPartnerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [WithholdingTaxCertificates])
                    THROW 51943, 'Canonical WHT cutover requires WithholdingTaxCertificates to be empty. Run the verified Finance reset before applying this migration.', 1;
                IF EXISTS (SELECT 1 FROM [WithholdingTaxRemittanceLines])
                    THROW 51944, 'Canonical WHT cutover requires WithholdingTaxRemittanceLines to be empty. Run the verified Finance reset before applying this migration.', 1;
                """);

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "WithholdingTaxRemittanceLines",
                newName: "BusinessPartnerId");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "WithholdingTaxCertificates",
                newName: "BusinessPartnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BusinessPartnerId",
                table: "WithholdingTaxRemittanceLines",
                newName: "SupplierId");

            migrationBuilder.RenameColumn(
                name: "BusinessPartnerId",
                table: "WithholdingTaxCertificates",
                newName: "SupplierId");
        }
    }
}
