using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Consolidates the legacy BUSINESS_PARTNER/BusinessPartner alias into the
/// canonical BusinessPartner/Business Partner workflow entity type. Existing
/// definitions and instances retain their audit history under the canonical row.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261009143000_ConsolidateBusinessPartnerWorkflowEntityType")]
public sealed class ConsolidateBusinessPartnerWorkflowEntityType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Mappings TABLE
            (
                [TenantId] uniqueidentifier NOT NULL,
                [CanonicalId] uniqueidentifier NOT NULL,
                [LegacyId] uniqueidentifier NOT NULL,
                PRIMARY KEY ([LegacyId])
            );

            INSERT INTO @Mappings ([TenantId], [CanonicalId], [LegacyId])
            SELECT canonical.[TenantId], canonical.[Id], legacy.[Id]
            FROM [WorkflowEntityTypes] canonical
            INNER JOIN [WorkflowEntityTypes] legacy
                ON legacy.[TenantId] = canonical.[TenantId]
               AND legacy.[Id] <> canonical.[Id]
            WHERE canonical.[Code] COLLATE Latin1_General_100_BIN2 = N'BusinessPartner'
              AND canonical.[Name] COLLATE Latin1_General_100_BIN2 = N'Business Partner'
              AND canonical.[IsDeleted] = 0
              AND legacy.[Code] COLLATE Latin1_General_100_BIN2 = N'BUSINESS_PARTNER'
              AND legacy.[Name] COLLATE Latin1_General_100_BIN2 = N'BusinessPartner'
              AND legacy.[IsDeleted] = 0;

            UPDATE definitions
            SET definitions.[EntityTypeId] = mapping.[CanonicalId],
                definitions.[UpdatedAt] = SYSUTCDATETIME(),
                definitions.[UpdatedBy] = N'Business Partner workflow entity consolidation'
            FROM [WorkflowDefinitions] definitions
            INNER JOIN @Mappings mapping ON mapping.[LegacyId] = definitions.[EntityTypeId];

            UPDATE instances
            SET instances.[EntityTypeId] = mapping.[CanonicalId],
                instances.[UpdatedAt] = SYSUTCDATETIME(),
                instances.[UpdatedBy] = N'Business Partner workflow entity consolidation'
            FROM [WorkflowInstances] instances
            INNER JOIN @Mappings mapping ON mapping.[LegacyId] = instances.[EntityTypeId];

            UPDATE legacy
            SET legacy.[Code] = CONCAT(N'RETIRED_BP_', REPLACE(CONVERT(nvarchar(36), legacy.[Id]), N'-', N'')),
                legacy.[Name] = CONCAT(N'Retired BusinessPartner alias ', CONVERT(nvarchar(36), legacy.[Id])),
                legacy.[IsActive] = 0,
                legacy.[IsDeleted] = 1,
                legacy.[DeletedAt] = SYSUTCDATETIME(),
                legacy.[DeletedBy] = N'Business Partner workflow entity consolidation',
                legacy.[UpdatedAt] = SYSUTCDATETIME(),
                legacy.[UpdatedBy] = N'Business Partner workflow entity consolidation'
            FROM [WorkflowEntityTypes] legacy
            INNER JOIN @Mappings mapping ON mapping.[LegacyId] = legacy.[Id];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally irreversible: restoring a duplicate entity type could split
        // retained workflow definitions and instances away from their canonical owner.
    }
}
