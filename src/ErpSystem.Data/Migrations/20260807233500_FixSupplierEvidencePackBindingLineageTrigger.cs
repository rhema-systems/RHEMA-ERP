using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260807233500_FixSupplierEvidencePackBindingLineageTrigger")]
public partial class FixSupplierEvidencePackBindingLineageTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSupplierRegistrationEvidencePackBindings_Immutable]
            ON [dbo].[ProcurementSupplierRegistrationEvidencePackBindings]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51712, 'Supplier registration evidence-pack bindings are immutable.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN [dbo].[BusinessPartnerRegistrations] r
                      ON r.[Id] = i.[RegistrationId]
                    JOIN [dbo].[ProcurementSupplierEvidencePackVersions] p
                      ON p.[Id] = i.[PackVersionId]
                    WHERE r.[TenantId] <> i.[TenantId]
                       OR p.[TenantId] <> i.[TenantId]
                       OR r.[RegistrationCategory] <> i.[RegistrationCategory]
                       OR p.[Category] <> i.[RegistrationCategory]
                       OR p.[Status] <> 2
                       OR p.[PackCode] <> i.[PackCode]
                       OR p.[Version] <> i.[PackVersion]
                       OR TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.[PackSnapshotJson], '$.id')) <> p.[Id]
                       OR TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.[PackSnapshotJson], '$.tenantId')) <> i.[TenantId]
                       OR JSON_VALUE(i.[PackSnapshotJson], '$.packCode') <> p.[PackCode]
                       OR TRY_CONVERT(int, JSON_VALUE(i.[PackSnapshotJson], '$.category')) <> i.[RegistrationCategory]
                       OR TRY_CONVERT(int, JSON_VALUE(i.[PackSnapshotJson], '$.version')) <> p.[Version]
                       OR TRY_CONVERT(int, JSON_VALUE(i.[PackSnapshotJson], '$.status')) <> 2
                       OR r.[Status] NOT IN ('Draft', 'MoreInfoRequired'))
                    THROW 51713, 'Supplier registration evidence-pack binding lineage mismatch.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSupplierRegistrationEvidencePackBindings_Immutable]
            ON [dbo].[ProcurementSupplierRegistrationEvidencePackBindings]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51712, 'Supplier registration evidence-pack bindings are immutable.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN [dbo].[BusinessPartnerRegistrations] r
                      ON r.[Id] = i.[RegistrationId]
                    JOIN [dbo].[ProcurementSupplierEvidencePackVersions] p
                      ON p.[Id] = i.[PackVersionId]
                    WHERE r.[TenantId] <> i.[TenantId]
                       OR p.[TenantId] <> i.[TenantId]
                       OR r.[RegistrationCategory] <> i.[RegistrationCategory]
                       OR p.[Category] <> i.[RegistrationCategory]
                       OR p.[Status] <> 2
                       OR p.[IntegrityHash] <> i.[PackSnapshotHash]
                       OR r.[Status] NOT IN ('Draft', 'MoreInfoRequired'))
                    THROW 51713, 'Supplier registration evidence-pack binding lineage mismatch.', 1;
            END
            """);
    }
}
