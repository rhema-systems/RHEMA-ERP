using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Exit clearance — the tenant's clearance form and one filled-in copy per separation
    /// (area 9b slice 4, FR-HR-091 and FR-HR-183).
    /// </summary>
    /// <remarks>
    /// <para>FR-HR-183 names what a clearance runs across: outstanding loans, salary advances,
    /// company property, office equipment, duty-post keys, documents and payroll recoveries. Those
    /// are the <c>ClearanceItemKind</c> members. FR-HR-091 makes the completed form a <b>gate</b> —
    /// entitlements are computed only after it, which is why the item carries an
    /// <c>OutstandingAmount</c> the settlement will read.</para>
    ///
    /// <para><b>Items snapshot their template.</b> Name, kind, owning unit and the mandatory flag
    /// are copied onto the item rather than read through <c>TemplateId</c>, and there is
    /// deliberately <b>no foreign key</b> from item to template: a clearance form is evidence about
    /// one person's exit, and editing or retiring a catalogue line afterwards must not rewrite what
    /// somebody signed, nor orphan it.</para>
    ///
    /// <para><b>The owning unit is <c>OrganizationUnit</c>, not <c>Department</c></b> — the HR
    /// module's placement entity, and the one the data supports: 3,810 of 3,834 employees carry an
    /// organisation unit against 3,320 with a department, and this tenant's seven departments
    /// belong to the estate and procurement modules. It routes and labels only: all 41 organisation
    /// units have a null <c>HeadEmployeeId</c>, so deriving a signatory from the unit head would
    /// resolve to nobody on every line of every form.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL, listed in
    /// <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddSeparationClearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationClearanceTemplates'))
BEGIN
    CREATE TABLE [dbo].[SeparationClearanceTemplates] (
        [Id]                       uniqueidentifier NOT NULL,
        [Name]                     nvarchar(200)    NOT NULL,
        [Kind]                     int              NOT NULL,
        [Description]              nvarchar(1000)   NULL,
        [OwningOrganizationUnitId] uniqueidentifier NULL,
        [IsMandatory]              bit              NOT NULL CONSTRAINT [DF_SeparationClearanceTemplates_IsMandatory] DEFAULT (1),
        [IsActive]                 bit              NOT NULL CONSTRAINT [DF_SeparationClearanceTemplates_IsActive] DEFAULT (1),
        [SortOrder]                int              NOT NULL CONSTRAINT [DF_SeparationClearanceTemplates_SortOrder] DEFAULT (0),
        [CreatedAt]                datetime2        NOT NULL,
        [UpdatedAt]                datetime2        NULL,
        [CreatedBy]                nvarchar(max)    NULL,
        [UpdatedBy]                nvarchar(max)    NULL,
        [CreatedById]              uniqueidentifier NULL,
        [LastModifiedById]         uniqueidentifier NULL,
        [IsDeleted]                bit              NOT NULL CONSTRAINT [DF_SeparationClearanceTemplates_IsDeleted] DEFAULT (0),
        [DeletedAt]                datetime2        NULL,
        [DeletedBy]                nvarchar(max)    NULL,
        [TenantId]                 uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SeparationClearanceTemplates] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationClearanceTemplates_OrganizationUnits_OwningOrganizationUnitId')
    ALTER TABLE [dbo].[SeparationClearanceTemplates] ADD CONSTRAINT [FK_SeparationClearanceTemplates_OrganizationUnits_OwningOrganizationUnitId]
        FOREIGN KEY ([OwningOrganizationUnitId]) REFERENCES [dbo].[OrganizationUnits] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationClearanceTemplates_Tenants_TenantId')
    ALTER TABLE [dbo].[SeparationClearanceTemplates] ADD CONSTRAINT [FK_SeparationClearanceTemplates_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
BEGIN
    CREATE TABLE [dbo].[SeparationClearanceItems] (
        [Id]                       uniqueidentifier NOT NULL,
        [SeparationId]             uniqueidentifier NOT NULL,
        [TemplateId]               uniqueidentifier NULL,
        [Name]                     nvarchar(200)    NOT NULL,
        [Kind]                     int              NOT NULL,
        [OwningOrganizationUnitId] uniqueidentifier NULL,
        [IsMandatory]              bit              NOT NULL CONSTRAINT [DF_SeparationClearanceItems_IsMandatory] DEFAULT (1),
        [SortOrder]                int              NOT NULL CONSTRAINT [DF_SeparationClearanceItems_SortOrder] DEFAULT (0),
        [Status]                   int              NOT NULL CONSTRAINT [DF_SeparationClearanceItems_Status] DEFAULT (1),
        [OutstandingAmount]        decimal(18,2)    NULL,
        [Notes]                    nvarchar(2000)   NULL,
        [SignedOffBy]              nvarchar(200)    NULL,
        [RecordedById]             uniqueidentifier NULL,
        [RecordedOn]               datetime2        NULL,
        [CreatedAt]                datetime2        NOT NULL,
        [UpdatedAt]                datetime2        NULL,
        [CreatedBy]                nvarchar(max)    NULL,
        [UpdatedBy]                nvarchar(max)    NULL,
        [CreatedById]              uniqueidentifier NULL,
        [LastModifiedById]         uniqueidentifier NULL,
        [IsDeleted]                bit              NOT NULL CONSTRAINT [DF_SeparationClearanceItems_IsDeleted] DEFAULT (0),
        [DeletedAt]                datetime2        NULL,
        [DeletedBy]                nvarchar(max)    NULL,
        [TenantId]                 uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SeparationClearanceItems] PRIMARY KEY ([Id])
    );
END");

            // ⚠ No foreign key on TemplateId, deliberately — see the remarks on this class.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationClearanceItems_EmployeeSeparations_SeparationId')
    ALTER TABLE [dbo].[SeparationClearanceItems] ADD CONSTRAINT [FK_SeparationClearanceItems_EmployeeSeparations_SeparationId]
        FOREIGN KEY ([SeparationId]) REFERENCES [dbo].[EmployeeSeparations] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationClearanceItems_Employees_RecordedById')
    ALTER TABLE [dbo].[SeparationClearanceItems] ADD CONSTRAINT [FK_SeparationClearanceItems_Employees_RecordedById]
        FOREIGN KEY ([RecordedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationClearanceItems_OrganizationUnits_OwningOrganizationUnitId')
    ALTER TABLE [dbo].[SeparationClearanceItems] ADD CONSTRAINT [FK_SeparationClearanceItems_OrganizationUnits_OwningOrganizationUnitId]
        FOREIGN KEY ([OwningOrganizationUnitId]) REFERENCES [dbo].[OrganizationUnits] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationClearanceItems_Tenants_TenantId')
    ALTER TABLE [dbo].[SeparationClearanceItems] ADD CONSTRAINT [FK_SeparationClearanceItems_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SeparationClearanceTemplate_Tenant_Name' AND object_id = OBJECT_ID('dbo.SeparationClearanceTemplates'))
    CREATE UNIQUE INDEX [UX_SeparationClearanceTemplate_Tenant_Name] ON [dbo].[SeparationClearanceTemplates] ([TenantId], [Name]) WHERE [IsDeleted] = 0;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationClearanceTemplate_Kind' AND object_id = OBJECT_ID('dbo.SeparationClearanceTemplates'))
    CREATE INDEX [IX_SeparationClearanceTemplate_Kind] ON [dbo].[SeparationClearanceTemplates] ([Kind]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationClearanceTemplate_IsActive' AND object_id = OBJECT_ID('dbo.SeparationClearanceTemplates'))
    CREATE INDEX [IX_SeparationClearanceTemplate_IsActive] ON [dbo].[SeparationClearanceTemplates] ([IsActive]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationClearanceTemplates_OwningOrganizationUnitId' AND object_id = OBJECT_ID('dbo.SeparationClearanceTemplates'))
    CREATE INDEX [IX_SeparationClearanceTemplates_OwningOrganizationUnitId] ON [dbo].[SeparationClearanceTemplates] ([OwningOrganizationUnitId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationClearanceTemplates_TenantId' AND object_id = OBJECT_ID('dbo.SeparationClearanceTemplates'))
    CREATE INDEX [IX_SeparationClearanceTemplates_TenantId] ON [dbo].[SeparationClearanceTemplates] ([TenantId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationClearanceItem_SeparationId' AND object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    CREATE INDEX [IX_SeparationClearanceItem_SeparationId] ON [dbo].[SeparationClearanceItems] ([SeparationId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationClearanceItem_Status' AND object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    CREATE INDEX [IX_SeparationClearanceItem_Status] ON [dbo].[SeparationClearanceItems] ([Status]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationClearanceItem_TemplateId' AND object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    CREATE INDEX [IX_SeparationClearanceItem_TemplateId] ON [dbo].[SeparationClearanceItems] ([TemplateId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationClearanceItems_OwningOrganizationUnitId' AND object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    CREATE INDEX [IX_SeparationClearanceItems_OwningOrganizationUnitId] ON [dbo].[SeparationClearanceItems] ([OwningOrganizationUnitId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationClearanceItems_RecordedById' AND object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    CREATE INDEX [IX_SeparationClearanceItems_RecordedById] ON [dbo].[SeparationClearanceItems] ([RecordedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationClearanceItems_TenantId' AND object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    CREATE INDEX [IX_SeparationClearanceItems_TenantId] ON [dbo].[SeparationClearanceItems] ([TenantId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    DROP TABLE [dbo].[SeparationClearanceItems];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationClearanceTemplates'))
    DROP TABLE [dbo].[SeparationClearanceTemplates];");
        }
    }
}
