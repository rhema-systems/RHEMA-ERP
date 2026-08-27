using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 25 slice 12d, decision D7. Three tables: <c>HrPolicyDocuments</c> — the staff policy
    /// library — <c>HrPolicyAudiences</c>, who each policy applies to, and
    /// <c>HrPolicyAcknowledgements</c>, one row per person who actually answered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>There is no "pending acknowledgement" row, and that is the central decision.</b>
    /// Seeding one per targeted employee at publication would mean ~7,900 rows for a
    /// tenant-wide policy, would freeze the audience at publish — contradicting the read-time
    /// membership the shared resolver is built on — and would silently miss anybody who joins
    /// afterwards. So rows exist only where somebody signed or declined, "outstanding" is the
    /// ABSENCE of a row, and the compliance roster is a left join computed at request time:
    /// resolve who it applies to today, attach whatever each of them has done. A joiner shows as
    /// outstanding automatically; a leaver is simply not on the list.
    /// </para>
    /// <para>
    /// <b>The unique index <c>(TenantId, PolicyId, EmployeeId)</c> is load-bearing.</b> It is
    /// what makes a double-submitted signature a database error rather than two contradictory
    /// records of what one person agreed to.
    /// </para>
    /// <para>
    /// <b>Acknowledgements CASCADE from their policy, and that is safe only because the service
    /// refuses to delete a published one.</b> An acknowledgement of a policy that no longer
    /// exists is evidence of nothing — but a published policy is archived, never deleted, so the
    /// cascade only ever reaches the acknowledgements of an unsigned draft. The two halves have
    /// to agree; this is the half that would quietly destroy evidence if they stopped agreeing.
    /// </para>
    /// <para>
    /// <b><c>SupersedesPolicyId</c> is RESTRICT, not cascade.</b> Deleting a superseded version
    /// must not take its replacement — or that replacement's signatures — with it. A new version
    /// is a new row precisely so the old signatures keep pointing at the text they were given
    /// for.
    /// </para>
    /// <para>
    /// <b><c>HrPolicyAudiences.TargetId</c> carries no foreign key</b>, for the same reason as
    /// the announcement audience in slice 12c: it points at one of five tables depending on
    /// <c>TargetType</c>, and a target that no longer exists simply matches nobody.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — three <c>CreateTable</c> calls, eleven indexes and
    /// ten foreign keys, with nothing inferred and nothing renamed. (⚠ Worth confirming rather
    /// than assuming: EF inferred a wrong <c>RENAME</c> in the assets area at slice 3.)
    /// Rewritten as guarded SQL only to match the surrounding HR migrations, so it is safe to
    /// re-run against a database at either state — including one built from the EF model rather
    /// than from the chain, which is how this repo's <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddHrPolicyLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrPolicyDocuments', 'U') IS NULL
CREATE TABLE [dbo].[HrPolicyDocuments] (
    [Id]                       uniqueidentifier NOT NULL,
    [PolicyNumber]             nvarchar(50)     NOT NULL,
    [Title]                    nvarchar(300)    NOT NULL,
    [Summary]                  nvarchar(1000)   NULL,
    [Category]                 int              NOT NULL,
    [VersionLabel]             nvarchar(30)     NULL,
    [Status]                   int              NOT NULL,
    [EffectiveFrom]            datetime2        NULL,
    [ReviewOn]                 datetime2        NULL,
    [SupersedesPolicyId]       uniqueidentifier NULL,
    [RequiresAcknowledgement]  bit              NOT NULL,
    [AcknowledgementText]      nvarchar(4000)   NULL,
    [AcknowledgementDueDays]   int              NULL,
    [PublishedAt]              datetime2        NULL,
    [PublishedById]            uniqueidentifier NULL,
    [ArchivedAt]               datetime2        NULL,
    [ArchivedById]             uniqueidentifier NULL,
    [FileUploadRecordId]       uniqueidentifier NULL,
    [DocumentRecordId]         uniqueidentifier NULL,
    [DocumentVersionId]        uniqueidentifier NULL,
    [FilePath]                 nvarchar(500)    NULL,
    [FileName]                 nvarchar(255)    NULL,
    [ContentType]              nvarchar(100)    NULL,
    [FileSize]                 bigint           NULL,
    [CreatedAt]                datetime2        NOT NULL,
    [UpdatedAt]                datetime2        NULL,
    [CreatedBy]                nvarchar(max)    NULL,
    [UpdatedBy]                nvarchar(max)    NULL,
    [CreatedById]              uniqueidentifier NULL,
    [LastModifiedById]         uniqueidentifier NULL,
    [IsDeleted]                bit              NOT NULL,
    [DeletedAt]                datetime2        NULL,
    [DeletedBy]                nvarchar(max)    NULL,
    [TenantId]                 uniqueidentifier NOT NULL,
    CONSTRAINT [PK_HrPolicyDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HrPolicyDocuments_Employees_PublishedById] FOREIGN KEY ([PublishedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_HrPolicyDocuments_Employees_ArchivedById] FOREIGN KEY ([ArchivedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_HrPolicyDocuments_HrPolicyDocuments_SupersedesPolicyId] FOREIGN KEY ([SupersedesPolicyId])
        REFERENCES [dbo].[HrPolicyDocuments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_HrPolicyDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrPolicyAudiences', 'U') IS NULL
CREATE TABLE [dbo].[HrPolicyAudiences] (
    [Id]                uniqueidentifier NOT NULL,
    [PolicyId]          uniqueidentifier NOT NULL,
    [TargetType]        int              NOT NULL,
    [TargetId]          uniqueidentifier NULL,
    [IsExclusion]       bit              NOT NULL,
    [CreatedAt]         datetime2        NOT NULL,
    [UpdatedAt]         datetime2        NULL,
    [CreatedBy]         nvarchar(max)    NULL,
    [UpdatedBy]         nvarchar(max)    NULL,
    [CreatedById]       uniqueidentifier NULL,
    [LastModifiedById]  uniqueidentifier NULL,
    [IsDeleted]         bit              NOT NULL,
    [DeletedAt]         datetime2        NULL,
    [DeletedBy]         nvarchar(max)    NULL,
    [TenantId]          uniqueidentifier NOT NULL,
    CONSTRAINT [PK_HrPolicyAudiences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HrPolicyAudiences_HrPolicyDocuments_PolicyId] FOREIGN KEY ([PolicyId])
        REFERENCES [dbo].[HrPolicyDocuments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_HrPolicyAudiences_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrPolicyAcknowledgements', 'U') IS NULL
CREATE TABLE [dbo].[HrPolicyAcknowledgements] (
    [Id]                    uniqueidentifier NOT NULL,
    [PolicyId]              uniqueidentifier NOT NULL,
    [EmployeeId]            uniqueidentifier NOT NULL,
    [Outcome]               int              NOT NULL,
    [AcknowledgementText]   nvarchar(4000)   NULL,
    [SignedAt]              datetime2        NULL,
    [DeclinedAt]            datetime2        NULL,
    [DeclineReason]         nvarchar(1000)   NULL,
    [SignatureIpAddress]    nvarchar(50)     NULL,
    [SignatureHash]         nvarchar(128)    NULL,
    [CreatedAt]             datetime2        NOT NULL,
    [UpdatedAt]             datetime2        NULL,
    [CreatedBy]             nvarchar(max)    NULL,
    [UpdatedBy]             nvarchar(max)    NULL,
    [CreatedById]           uniqueidentifier NULL,
    [LastModifiedById]      uniqueidentifier NULL,
    [IsDeleted]             bit              NOT NULL,
    [DeletedAt]             datetime2        NULL,
    [DeletedBy]             nvarchar(max)    NULL,
    [TenantId]              uniqueidentifier NOT NULL,
    CONSTRAINT [PK_HrPolicyAcknowledgements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HrPolicyAcknowledgements_HrPolicyDocuments_PolicyId] FOREIGN KEY ([PolicyId])
        REFERENCES [dbo].[HrPolicyDocuments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_HrPolicyAcknowledgements_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_HrPolicyAcknowledgements_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Indexes are created separately from the tables so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the tables exist and these may not.
            foreach (var (name, table, columns) in new[]
            {
                ("IX_HrPolicyDocuments_Status", "HrPolicyDocuments", "[Status]"),
                ("IX_HrPolicyDocuments_Category", "HrPolicyDocuments", "[Category]"),
                ("IX_HrPolicyDocuments_PublishedById", "HrPolicyDocuments", "[PublishedById]"),
                ("IX_HrPolicyDocuments_ArchivedById", "HrPolicyDocuments", "[ArchivedById]"),
                ("IX_HrPolicyDocuments_SupersedesPolicyId", "HrPolicyDocuments", "[SupersedesPolicyId]"),
                // The portal's read filters on status and the effective date together.
                ("IX_HrPolicyDocuments_TenantId_Status_EffectiveFrom", "HrPolicyDocuments",
                 "[TenantId], [Status], [EffectiveFrom]"),
                ("IX_HrPolicyAudiences_PolicyId", "HrPolicyAudiences", "[PolicyId]"),
                ("IX_HrPolicyAudiences_TenantId", "HrPolicyAudiences", "[TenantId]"),
                ("IX_HrPolicyAudiences_TargetType_TargetId", "HrPolicyAudiences", "[TargetType], [TargetId]"),
                ("IX_HrPolicyAcknowledgements_PolicyId", "HrPolicyAcknowledgements", "[PolicyId]"),
                ("IX_HrPolicyAcknowledgements_EmployeeId", "HrPolicyAcknowledgements", "[EmployeeId]"),
                ("IX_HrPolicyAcknowledgements_Outcome", "HrPolicyAcknowledgements", "[Outcome]"),
            })
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{name}] ON [dbo].[{table}] ({columns});");
            }

            // The policy number series.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrPolicyDocuments', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_HrPolicyDocuments_TenantId_PolicyNumber'
                     AND object_id = OBJECT_ID('dbo.HrPolicyDocuments'))
    CREATE UNIQUE INDEX [IX_HrPolicyDocuments_TenantId_PolicyNumber]
        ON [dbo].[HrPolicyDocuments] ([TenantId], [PolicyNumber]);");

            // ⚠ One answer per person per policy. This is not a performance index: it is the
            // guard that turns a double-submitted signature into a database error rather than
            // two contradictory records of what one person agreed to.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrPolicyAcknowledgements', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_HrPolicyAcknowledgements_TenantId_PolicyId_EmployeeId'
                     AND object_id = OBJECT_ID('dbo.HrPolicyAcknowledgements'))
    CREATE UNIQUE INDEX [IX_HrPolicyAcknowledgements_TenantId_PolicyId_EmployeeId]
        ON [dbo].[HrPolicyAcknowledgements] ([TenantId], [PolicyId], [EmployeeId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropped child-first.
            //
            // ⚠ This discards every signature the organisation has collected against a policy —
            // who agreed to what, on what date, from where, and the frozen text they agreed to.
            // That is the evidence an employer relies on when a code of conduct is disputed, and
            // nothing else in the schema holds it.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrPolicyAcknowledgements', 'U') IS NOT NULL
    DROP TABLE [dbo].[HrPolicyAcknowledgements];");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrPolicyAudiences', 'U') IS NOT NULL
    DROP TABLE [dbo].[HrPolicyAudiences];");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrPolicyDocuments', 'U') IS NOT NULL
    DROP TABLE [dbo].[HrPolicyDocuments];");
        }
    }
}
