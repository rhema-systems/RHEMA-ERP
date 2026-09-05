using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 25 slice 12b, decision D7. One table: <c>HrLetterRequests</c> — a letter an employee
    /// asked HR for, and the letter HR issued for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two fulfilment routes on one row, deliberately.</b> <c>IssuedDocumentHtml</c> holds a
    /// letter GENERATED from the HR-editable template; the five gate columns
    /// (<c>FileUploadRecordId</c> / <c>DocumentRecordId</c> / <c>DocumentVersionId</c> /
    /// <c>FilePath</c> / <c>FileName</c>) hold a signed scan uploaded instead, for the letters
    /// that need a wet signature or somebody else's form. Splitting these into two tables would
    /// have bought nothing: a request is fulfilled once, by one route or the other, and the
    /// employee downloads it the same way either way.
    /// </para>
    /// <para>
    /// <b><c>IssuedDocumentHtml</c> is <c>nvarchar(max)</c> and the letter is FROZEN into it</b>
    /// rather than re-rendered on each read. This is the opposite of the asset responsibility
    /// letter, which is generated on demand precisely because it describes a live assignment. A
    /// letter of employment is a statement made on a date to a third party who may still be
    /// holding it a year later: it has to say tomorrow exactly what it said when it was handed
    /// over, even after a promotion changes the position it names. Same reasoning as the payroll
    /// payslip snapshot (slice 10). A <c>MaxLength</c> here would silently truncate the document
    /// somebody is about to give to a bank.
    /// </para>
    /// <para>
    /// <b>Two number series, one table.</b> <c>RequestNumber</c> (HLR-) identifies the ASK and is
    /// uniquely indexed; <c>LetterNumber</c> (HRL-) is the reference printed on the ISSUED letter
    /// and exists only once one has been issued. They are separate because a refused or withdrawn
    /// request must not consume a letter reference — an external party quoting HRL-2026-00042
    /// must always be quoting a letter that was actually issued.
    /// </para>
    /// <para>
    /// Every delete is <c>NO ACTION</c>: the employee and the issuing officer must both survive a
    /// letter that named them, because the record of what was stated about somebody, by whom, is
    /// the whole point of keeping it.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — one <c>CreateTable</c>, five indexes and three
    /// foreign keys, with nothing inferred and nothing renamed. (⚠ Worth confirming rather than
    /// assuming: EF inferred a wrong <c>RENAME</c> in the assets area at slice 3.) Rewritten as
    /// guarded SQL only to match the surrounding HR migrations, so it is safe to re-run against a
    /// database at either state — including one built from the EF model rather than from the
    /// chain, which is how this repo's <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddHrLetterRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrLetterRequests', 'U') IS NULL
CREATE TABLE [dbo].[HrLetterRequests] (
    [Id]                    uniqueidentifier NOT NULL,
    [RequestNumber]         nvarchar(50)     NOT NULL,
    [EmployeeId]            uniqueidentifier NOT NULL,
    [LetterType]            int              NOT NULL,
    [Purpose]               nvarchar(1000)   NOT NULL,
    [AddressedTo]           nvarchar(300)    NULL,
    [Status]                int              NOT NULL,
    [RequestedAt]           datetime2        NOT NULL,
    [IssuedById]            uniqueidentifier NULL,
    [IssuedAt]              datetime2        NULL,
    [LetterNumber]          nvarchar(50)     NULL,
    [IssuedDocumentHtml]    nvarchar(max)    NULL,
    [DecisionComments]      nvarchar(1000)   NULL,
    [RejectedAt]            datetime2        NULL,
    [CancelledAt]           datetime2        NULL,
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
    CONSTRAINT [PK_HrLetterRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HrLetterRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_HrLetterRequests_Employees_IssuedById] FOREIGN KEY ([IssuedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_HrLetterRequests_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Indexes are created separately from the table so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the table exists and these may not.
            foreach (var (name, column) in new[]
            {
                ("IX_HrLetterRequests_EmployeeId", "EmployeeId"),
                ("IX_HrLetterRequests_Status", "Status"),
                ("IX_HrLetterRequests_LetterType", "LetterType"),
                ("IX_HrLetterRequests_IssuedById", "IssuedById"),
            })
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.HrLetterRequests', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.HrLetterRequests'))
    CREATE INDEX [{name}] ON [dbo].[HrLetterRequests] ([{column}]);");
            }

            // The request number series. Unique and unfiltered, matching StaffGrievances and the
            // other HR series — safe only because the generator counts issued numbers INCLUDING
            // soft-deleted rows and so never re-issues one. It also serves as the tenant index,
            // TenantId being its leading column.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrLetterRequests', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_HrLetterRequests_TenantId_RequestNumber'
                     AND object_id = OBJECT_ID('dbo.HrLetterRequests'))
    CREATE UNIQUE INDEX [IX_HrLetterRequests_TenantId_RequestNumber]
        ON [dbo].[HrLetterRequests] ([TenantId], [RequestNumber]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ This discards every letter the organisation has issued to its own staff — the
            // frozen text of each one, who issued it and on what date. Those letters are held by
            // banks, embassies and landlords who may ask the company to confirm them; nothing
            // else in the schema records that they were ever written.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.HrLetterRequests', 'U') IS NOT NULL
    DROP TABLE [dbo].[HrLetterRequests];");
        }
    }
}
