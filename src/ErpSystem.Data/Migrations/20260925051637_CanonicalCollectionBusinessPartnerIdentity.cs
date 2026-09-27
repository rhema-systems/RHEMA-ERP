using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalCollectionBusinessPartnerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Customer GUIDs in these legacy tables refer to the retired Sales Customer master.
            // They must not be reinterpreted as canonical Business Partner GUIDs. The approved
            // development cutover resets Finance/collection transactions before migration.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [CollectionActivities])
                    THROW 51951, 'Canonical collection cutover requires CollectionActivities to be empty. Run the verified Finance reset before applying this migration.', 1;
                IF EXISTS (SELECT 1 FROM [PaymentPlans])
                    THROW 51952, 'Canonical collection cutover requires PaymentPlans to be empty. Run the verified Finance reset before applying this migration.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionActivities_Customer_CustomerId",
                table: "CollectionActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentPlans_Customer_CustomerId",
                table: "PaymentPlans");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "PaymentPlans",
                newName: "BusinessPartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_PaymentPlans_CustomerId",
                table: "PaymentPlans",
                newName: "IX_PaymentPlans_BusinessPartnerId");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "CollectionActivities",
                newName: "BusinessPartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_CollectionActivities_CustomerId",
                table: "CollectionActivities",
                newName: "IX_CollectionActivities_BusinessPartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionActivities_BusinessPartners_BusinessPartnerId",
                table: "CollectionActivities",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentPlans_BusinessPartners_BusinessPartnerId",
                table: "PaymentPlans",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollectionActivities_BusinessPartners_BusinessPartnerId",
                table: "CollectionActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentPlans_BusinessPartners_BusinessPartnerId",
                table: "PaymentPlans");

            migrationBuilder.RenameColumn(
                name: "BusinessPartnerId",
                table: "PaymentPlans",
                newName: "CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_PaymentPlans_BusinessPartnerId",
                table: "PaymentPlans",
                newName: "IX_PaymentPlans_CustomerId");

            migrationBuilder.RenameColumn(
                name: "BusinessPartnerId",
                table: "CollectionActivities",
                newName: "CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_CollectionActivities_BusinessPartnerId",
                table: "CollectionActivities",
                newName: "IX_CollectionActivities_CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionActivities_Customer_CustomerId",
                table: "CollectionActivities",
                column: "CustomerId",
                principalTable: "Customer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentPlans_Customer_CustomerId",
                table: "PaymentPlans",
                column: "CustomerId",
                principalTable: "Customer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
