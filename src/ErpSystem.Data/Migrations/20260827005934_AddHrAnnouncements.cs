using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 25 slice 12c, decision D7. Two tables: <c>HrAnnouncements</c> — something the
    /// organisation is telling its staff — and <c>HrAnnouncementAudiences</c>, the rules saying
    /// who each one is for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>Body</c> is <c>nvarchar(max)</c> and holds PLAIN TEXT.</b> An announcement reaches
    /// everybody, so storing HR-authored HTML and rendering it would be a stored-XSS vector
    /// aimed at the entire staff; the portal renders this as text with line breaks preserved,
    /// never as markup. (The HR letters of slice 12b are HTML for the opposite reason: they are
    /// composed by the server from a template, and no user-supplied string reaches them as
    /// markup.) It is unbounded because a staff notice is prose and a length cap would truncate
    /// the middle of something everyone is about to read.
    /// </para>
    /// <para>
    /// <b><c>HrAnnouncementAudiences.TargetId</c> carries NO foreign key, deliberately.</b> It
    /// points at one of five different tables depending on <c>TargetType</c> — an organisation
    /// unit, an organisation level, a position, a location or an employee — so there is no
    /// single relationship to declare. The service resolves it; a target that no longer exists
    /// simply matches nobody, which is the safe direction for a broadcast to fail. The
    /// <c>(TargetType, TargetId)</c> index is what makes that resolution cheap.
    /// </para>
    /// <para>
    /// <b>There is no per-employee "read" table, and that is a decision rather than an
    /// omission.</b> A seen-row per person per announcement is a large table answering a
    /// question nobody asked: a notice board is not an obligation. Where the organisation
    /// genuinely has to prove somebody was told, that is a policy acknowledgement — a different
    /// thing, with a signature, a frozen text and a declinable outcome.
    /// </para>
    /// <para>
    /// <b>Audience membership is evaluated at READ time, never frozen at publish</b>, so there
    /// is deliberately no recipient table either: an employee who transfers into a unit on
    /// Monday should see the notice that unit was sent on Friday, and a leaver should stop
    /// seeing it. A frozen recipient list gets both of those wrong.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — two <c>CreateTable</c> calls, eight indexes and
    /// five foreign keys, with nothing inferred and nothing renamed. (⚠ Worth confirming rather
    /// than assuming: EF inferred a wrong <c>RENAME</c> in the assets area at slice 3.)
    /// Rewritten as guarded SQL only to match the surrounding HR migrations, so it is safe to
    /// re-run against a database at either state — including one built from the EF model rather
    /// than from the chain, which is how this repo's <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddHrAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrAnnouncements', 'U') IS NULL
CREATE TABLE [dbo].[HrAnnouncements] (
    [Id]                    uniqueidentifier NOT NULL,
    [Title]                 nvarchar(300)    NOT NULL,
    [Summary]               nvarchar(500)    NULL,
    [Body]                  nvarchar(max)    NOT NULL,
    [Category]              int              NOT NULL,
    [Status]                int              NOT NULL,
    [IsPinned]              bit              NOT NULL,
    [PublishedAt]           datetime2        NULL,
    [PublishedById]         uniqueidentifier NULL,
    [EffectiveFrom]         datetime2        NULL,
    [ExpiresOn]             datetime2        NULL,
    [ArchivedAt]            datetime2        NULL,
    [ArchivedById]          uniqueidentifier NULL,
    [FileUploadRecordId]    uniqueidentifier NULL,
    [DocumentRecordId]      uniqueidentifier NULL,
    [DocumentVersionId]     uniqueidentifier NULL,
    [FilePath]              nvarchar(500)    NULL,
    [FileName]              nvarchar(255)    NULL,
    [ContentType]           nvarchar(100)    NULL,
    [FileSize]              bigint           NULL,
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
    CONSTRAINT [PK_HrAnnouncements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HrAnnouncements_Employees_PublishedById] FOREIGN KEY ([PublishedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_HrAnnouncements_Employees_ArchivedById] FOREIGN KEY ([ArchivedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_HrAnnouncements_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrAnnouncementAudiences', 'U') IS NULL
CREATE TABLE [dbo].[HrAnnouncementAudiences] (
    [Id]                uniqueidentifier NOT NULL,
    [AnnouncementId]    uniqueidentifier NOT NULL,
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
    CONSTRAINT [PK_HrAnnouncementAudiences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HrAnnouncementAudiences_HrAnnouncements_AnnouncementId] FOREIGN KEY ([AnnouncementId])
        REFERENCES [dbo].[HrAnnouncements] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_HrAnnouncementAudiences_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Indexes are created separately from the tables so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the tables exist and these may not.
            foreach (var (name, table, columns) in new[]
            {
                ("IX_HrAnnouncements_TenantId", "HrAnnouncements", "[TenantId]"),
                ("IX_HrAnnouncements_Status", "HrAnnouncements", "[Status]"),
                ("IX_HrAnnouncements_Category", "HrAnnouncements", "[Category]"),
                ("IX_HrAnnouncements_PublishedById", "HrAnnouncements", "[PublishedById]"),
                ("IX_HrAnnouncements_ArchivedById", "HrAnnouncements", "[ArchivedById]"),
                // The portal's read filters on status and the date window together.
                ("IX_HrAnnouncements_TenantId_Status_ExpiresOn", "HrAnnouncements",
                 "[TenantId], [Status], [ExpiresOn]"),
                ("IX_HrAnnouncementAudiences_AnnouncementId", "HrAnnouncementAudiences", "[AnnouncementId]"),
                ("IX_HrAnnouncementAudiences_TenantId", "HrAnnouncementAudiences", "[TenantId]"),
                // What makes resolving a polymorphic target cheap — see the remarks.
                ("IX_HrAnnouncementAudiences_TargetType_TargetId", "HrAnnouncementAudiences",
                 "[TargetType], [TargetId]"),
            })
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{name}] ON [dbo].[{table}] ({columns});");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropped child-first: an audience rule references the announcement it belongs to.
            //
            // ⚠ This discards every notice the organisation has published to its staff, and who
            // each was addressed to. What was announced, to whom, and when is a record — an
            // announcement withdrawn after it caused a decision still needs to be findable, and
            // nothing else in the schema keeps it.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrAnnouncementAudiences', 'U') IS NOT NULL
    DROP TABLE [dbo].[HrAnnouncementAudiences];");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrAnnouncements', 'U') IS NOT NULL
    DROP TABLE [dbo].[HrAnnouncements];");
        }
    }
}
