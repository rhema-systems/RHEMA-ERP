using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 12 slice 3 — retires the duplicate travel vendor master onto Procurement's Supplier,
    /// and links a company-vehicle leg to the fleet trip that reserves the vehicle.
    ///
    /// <para><b>Vendors.</b> StaffTravelVendor was a second supplier master — VendorCode,
    /// VendorName, contact, account number, contract dates, IsPreferred, Rating and PaymentTerms as
    /// free text — all of which Supplier already models, with SupplierPerformanceMetric behind the
    /// rating and PaymentTerm as a real entity rather than a string. An airline paid through travel
    /// and the same airline paid through procurement must not be two records that can disagree. The
    /// six VendorId columns (flights, hotels, ground transport, car rentals, visa applications,
    /// insurance) are repointed at Suppliers; the columns themselves are unchanged.</para>
    ///
    /// <para>⚠ <b>Any existing VendorId is set to NULL before the new constraint is added.</b> Those
    /// ids reference StaffTravelVendors rows and are meaningless against Suppliers, so leaving them
    /// would make the FK creation fail. This is deliberate data loss, and it is safe here because it
    /// was measured: on the reference database 0 of the six tables had a single non-null VendorId,
    /// and the 12 StaffTravelVendors rows were all test fixtures. <b>Re-measure before running this
    /// anywhere that has real bookings</b> — if any exist, their vendors must be onboarded as
    /// Suppliers and the ids remapped first, which this migration deliberately does not attempt.</para>
    ///
    /// <para><b>Fleet.</b> StaffTravelGroundTransport gains a nullable FleetTripId. Fleet already
    /// models a trip properly — vehicle, driver (an HR Employee FK, so the seam was half-built),
    /// origin, destination, planned window, expected mileage and cost. Recording a company-vehicle
    /// journey as free text meant two people could be promised the same vehicle and neither system
    /// would know.</para>
    ///
    /// <para>The scaffolded operations are replaced with guarded SQL (repo convention): local dev
    /// DBs are built from the EF model by rebuild-db, so a database can already carry this shape
    /// without the migration being stamped. The generated Designer and the regenerated snapshot are
    /// kept as scaffolded.</para>
    /// </summary>
    public partial class AddStaffTravelSupplierAndFleetTripLinks : Migration
    {
        /// <summary>The six travel tables whose VendorId is being repointed.</summary>
        private static readonly string[] VendorTables =
        {
            "StaffTravelFlightBookings",
            "StaffTravelHotelBookings",
            "StaffTravelGroundTransports",
            "StaffTravelCarRentalBookings",
            "StaffTravelVisaApplications",
            "StaffTravelInsurancePolicies",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in VendorTables)
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = N'FK_{table}_StaffTravelVendors_VendorId'
             AND parent_object_id = OBJECT_ID(N'[{table}]'))
    ALTER TABLE [{table}] DROP CONSTRAINT [FK_{table}_StaffTravelVendors_VendorId];
");

                // See the class remarks: these ids point at StaffTravelVendors and cannot satisfy
                // the Suppliers constraint. Measured as zero rows before this was written.
                migrationBuilder.Sql($@"
UPDATE [{table}] SET [VendorId] = NULL WHERE [VendorId] IS NOT NULL;
");

                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = N'FK_{table}_Suppliers_VendorId'
                 AND parent_object_id = OBJECT_ID(N'[{table}]'))
    ALTER TABLE [{table}] ADD CONSTRAINT [FK_{table}_Suppliers_VendorId]
        FOREIGN KEY ([VendorId]) REFERENCES [Suppliers] ([Id]);
");
            }

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffTravelGroundTransports]', N'FleetTripId') IS NULL
    ALTER TABLE [StaffTravelGroundTransports] ADD [FleetTripId] uniqueidentifier NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffTravelGroundTransports]', N'FleetTripId') IS NOT NULL
    ALTER TABLE [StaffTravelGroundTransports] DROP COLUMN [FleetTripId];
");

            foreach (var table in VendorTables)
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = N'FK_{table}_Suppliers_VendorId'
             AND parent_object_id = OBJECT_ID(N'[{table}]'))
    ALTER TABLE [{table}] DROP CONSTRAINT [FK_{table}_Suppliers_VendorId];
");

                // Down cannot restore the ids Up nulled — they were not preserved, because the
                // measured row count was zero. Stated rather than silently implied.
                migrationBuilder.Sql($@"
UPDATE [{table}] SET [VendorId] = NULL WHERE [VendorId] IS NOT NULL;
");

                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = N'FK_{table}_StaffTravelVendors_VendorId'
                 AND parent_object_id = OBJECT_ID(N'[{table}]'))
    ALTER TABLE [{table}] ADD CONSTRAINT [FK_{table}_StaffTravelVendors_VendorId]
        FOREIGN KEY ([VendorId]) REFERENCES [StaffTravelVendors] ([Id]);
");
            }
        }
    }
}
