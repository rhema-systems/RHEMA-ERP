using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 25 slice 12a, decision D6. Two tables: <c>EmployeeProfileChangeRequests</c> — an
    /// employee's request to correct their own personal data, and HR's answer to it — and
    /// <c>EmployeeProfileChangeItems</c>, one row per field with what it was, what was asked
    /// for, and what actually landed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a request table and not a self-service write.</b> The portal lets an employee edit
    /// their own contact details directly, because they are the only authority on their own
    /// phone number. A bank account, a date of birth, an SSNIT number or a name is a different
    /// class of fact: a wrong bank account is a payroll fraud vector, and a wrong date of birth
    /// moves a retirement date. Those arrive here for a named HR officer to approve, and the
    /// approval is what applies them.
    /// </para>
    /// <para>
    /// <b>The item rows are the audit trail, which is the point.</b> <c>OldValue</c> is
    /// snapshotted when the request is FILED, so months later the record still says what the
    /// value was before anyone touched it — the question an auditor actually asks about a
    /// bank-account change, and the one HR cannot answer today. <c>AppliedValue</c> is stamped
    /// by the applier, never sent by a client, so "what was requested" and "what landed" stay
    /// separately visible even if they always agree.
    /// </para>
    /// <para>
    /// <b>The unique index is <c>(TenantId, RequestNumber)</c> and is deliberately NOT filtered
    /// on soft-delete</b>, matching <c>StaffGrievances</c> and the other HR number series. That
    /// is safe only because the generator counts issued numbers INCLUDING soft-deleted rows and
    /// so never re-issues one — the two halves have to agree, and this is the half that would
    /// fail loudly if they ever stopped agreeing.
    /// </para>
    /// <para>
    /// <b>Deletes.</b> Items cascade from their request: an item has no meaning apart from the
    /// request it belongs to. Everything else is <c>NO ACTION</c> — the employee, the reviewing
    /// officer and the targeted bank account must all survive the request that referenced them,
    /// because the audit question is what was asked for at the time.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — two <c>CreateTable</c> calls, eight indexes and
    /// six foreign keys, with nothing inferred and nothing renamed. (⚠ Worth confirming rather
    /// than assuming: EF inferred a wrong <c>RENAME</c> in the assets area at slice 3.)
    /// Rewritten as guarded SQL only to match the surrounding HR migrations, so it is safe to
    /// re-run against a database at either state — including one built from the EF model rather
    /// than from the chain, which is how this repo's <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeProfileChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeProfileChangeRequests', 'U') IS NULL
CREATE TABLE [dbo].[EmployeeProfileChangeRequests] (
    [Id]                          uniqueidentifier NOT NULL,
    [RequestNumber]               nvarchar(50)     NOT NULL,
    [EmployeeId]                  uniqueidentifier NOT NULL,
    [Status]                      int              NOT NULL,
    [SubmittedAt]                 datetime2        NOT NULL,
    [Reason]                      nvarchar(1000)   NOT NULL,
    [BankDetailId]                uniqueidentifier NULL,
    [ReviewedById]                uniqueidentifier NULL,
    [ReviewedAt]                  datetime2        NULL,
    [ReviewComments]              nvarchar(1000)   NULL,
    [AppliedAt]                   datetime2        NULL,
    [CancelledAt]                 datetime2        NULL,
    [EvidenceFileUploadRecordId]  uniqueidentifier NULL,
    [EvidenceDocumentRecordId]    uniqueidentifier NULL,
    [EvidenceDocumentVersionId]   uniqueidentifier NULL,
    [EvidenceFilePath]            nvarchar(500)    NULL,
    [EvidenceFileName]            nvarchar(255)    NULL,
    [EvidenceContentType]         nvarchar(100)    NULL,
    [EvidenceFileSize]            bigint           NULL,
    [CreatedAt]                   datetime2        NOT NULL,
    [UpdatedAt]                   datetime2        NULL,
    [CreatedBy]                   nvarchar(max)    NULL,
    [UpdatedBy]                   nvarchar(max)    NULL,
    [CreatedById]                 uniqueidentifier NULL,
    [LastModifiedById]            uniqueidentifier NULL,
    [IsDeleted]                   bit              NOT NULL,
    [DeletedAt]                   datetime2        NULL,
    [DeletedBy]                   nvarchar(max)    NULL,
    [TenantId]                    uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeProfileChangeRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeProfileChangeRequests_EmployeeBankDetails_BankDetailId] FOREIGN KEY ([BankDetailId])
        REFERENCES [dbo].[EmployeeBankDetails] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeProfileChangeRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeProfileChangeRequests_Employees_ReviewedById] FOREIGN KEY ([ReviewedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeProfileChangeRequests_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeProfileChangeItems', 'U') IS NULL
CREATE TABLE [dbo].[EmployeeProfileChangeItems] (
    [Id]               uniqueidentifier NOT NULL,
    [RequestId]        uniqueidentifier NOT NULL,
    [Field]            int              NOT NULL,
    [OldValue]         nvarchar(500)    NULL,
    [NewValue]         nvarchar(500)    NOT NULL,
    [AppliedValue]     nvarchar(500)    NULL,
    [CreatedAt]        datetime2        NOT NULL,
    [UpdatedAt]        datetime2        NULL,
    [CreatedBy]        nvarchar(max)    NULL,
    [UpdatedBy]        nvarchar(max)    NULL,
    [CreatedById]      uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted]        bit              NOT NULL,
    [DeletedAt]        datetime2        NULL,
    [DeletedBy]        nvarchar(max)    NULL,
    [TenantId]         uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeProfileChangeItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeProfileChangeItems_EmployeeProfileChangeRequests_RequestId] FOREIGN KEY ([RequestId])
        REFERENCES [dbo].[EmployeeProfileChangeRequests] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeProfileChangeItems_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Indexes are created separately from the tables so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the tables exist and these may not.
            foreach (var (name, table, column) in new[]
            {
                ("IX_EmployeeProfileChangeRequests_EmployeeId", "EmployeeProfileChangeRequests", "EmployeeId"),
                ("IX_EmployeeProfileChangeRequests_Status", "EmployeeProfileChangeRequests", "Status"),
                ("IX_EmployeeProfileChangeRequests_BankDetailId", "EmployeeProfileChangeRequests", "BankDetailId"),
                ("IX_EmployeeProfileChangeRequests_ReviewedById", "EmployeeProfileChangeRequests", "ReviewedById"),
                ("IX_EmployeeProfileChangeItems_RequestId", "EmployeeProfileChangeItems", "RequestId"),
                ("IX_EmployeeProfileChangeItems_Field", "EmployeeProfileChangeItems", "Field"),
                ("IX_EmployeeProfileChangeItems_TenantId", "EmployeeProfileChangeItems", "TenantId"),
            })
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{name}] ON [dbo].[{table}] ([{column}]);");
            }

            // The number series. Unique and unfiltered — see the remark on soft-delete above.
            // It also serves as the tenant index, TenantId being its leading column.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeProfileChangeRequests', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_EmployeeProfileChangeRequests_TenantId_RequestNumber'
                     AND object_id = OBJECT_ID('dbo.EmployeeProfileChangeRequests'))
    CREATE UNIQUE INDEX [IX_EmployeeProfileChangeRequests_TenantId_RequestNumber]
        ON [dbo].[EmployeeProfileChangeRequests] ([TenantId], [RequestNumber]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropped child-first: the items reference the request they belong to.
            //
            // ⚠ This discards every personal-data change an employee ever asked for and every
            // answer HR gave — including the before/after values behind a bank-account change.
            // Nothing else in the schema carries it: the employee record keeps only the CURRENT
            // values, which is precisely what these rows exist to give provenance to.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeProfileChangeItems', 'U') IS NOT NULL
    DROP TABLE [dbo].[EmployeeProfileChangeItems];");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeProfileChangeRequests', 'U') IS NOT NULL
    DROP TABLE [dbo].[EmployeeProfileChangeRequests];");
        }
    }
}
