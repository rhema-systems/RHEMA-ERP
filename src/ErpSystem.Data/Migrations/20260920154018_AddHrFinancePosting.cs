using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR finish plan lane 8, slice 1: HR → Finance posting. The account-role mappings, the
    /// per-event rules and the posting register — the HR side of FIN-INT-001. Design in
    /// <c>docs/HR/integration/HR-FINANCE-POSTING-DESIGN.md</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has these objects and a
    /// bare <c>CreateTable</c> stops the chain. The column list below is the scaffold's, verbatim.</para>
    ///
    /// <para><b>Three tables, one FK into Finance.</b> <c>HrFinanceAccountMappings.AccountId</c>
    /// is a real <c>Restrict</c> FK to <c>Accounts</c> — Finance cannot delete an account HR posts
    /// to without HR re-mapping the role, the same protection an organisation unit's cost code has.
    /// The register's <c>PostingEventId</c> / <c>JournalEntryId</c> and their reversal twins are
    /// deliberately <b>bare columns</b>: they are HR's back-references for drill-through, and an HR
    /// table must not constrain Finance's own reversal or archival, nor vanish with whatever
    /// Finance does next.</para>
    ///
    /// <para><b>No shadow FK was minted</b> — none of the three entities carries a navigation, so
    /// the scaffold produced exactly the declared columns. Checked against the
    /// <c>ef-unpaired-navigation-shadow-fks</c> greps before this body was written.</para>
    ///
    /// <para>The filtered unique indexes are the model's: one live mapping per role, one live rule
    /// per event, one live register row per (event, source document). A retry UPDATES the row.</para>
    /// </remarks>
    public partial class AddHrFinancePosting : Migration
    {
        private const string Mappings = "HrFinanceAccountMappings";
        private const string Rules = "HrFinancePostingRules";
        private const string Records = "HrFinancePostingRecords";

        private static string CreateIndex(string table, string index, string columns, bool unique = false, string filter = null) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE {(unique ? "UNIQUE " : string.Empty)}INDEX [{index}] ON [dbo].[{table}] ({columns}){(filter is null ? string.Empty : $" WHERE {filter}")};";

        private static string DropTable(string table) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{table}];";

        private const string AuditColumns = @"
    [CreatedAt]                  datetime2        NOT NULL,
    [UpdatedAt]                  datetime2        NULL,
    [CreatedBy]                  nvarchar(max)    NULL,
    [UpdatedBy]                  nvarchar(max)    NULL,
    [CreatedById]                uniqueidentifier NULL,
    [LastModifiedById]           uniqueidentifier NULL,
    [IsDeleted]                  bit              NOT NULL,
    [DeletedAt]                  datetime2        NULL,
    [DeletedBy]                  nvarchar(max)    NULL,
    [TenantId]                   uniqueidentifier NOT NULL,";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Account roles → Finance accounts ────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Mappings}', 'U') IS NULL
CREATE TABLE [dbo].[{Mappings}] (
    [Id]                         uniqueidentifier NOT NULL,
    [Role]                       int              NOT NULL,
    [AccountId]                  uniqueidentifier NOT NULL,
    [AccountCodeSnapshot]        nvarchar(50)     NOT NULL,
    [AccountNameSnapshot]        nvarchar(200)    NOT NULL,
    [Notes]                      nvarchar(500)    NULL,{AuditColumns}
    CONSTRAINT [PK_{Mappings}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Mappings}_Accounts_AccountId] FOREIGN KEY ([AccountId])
        REFERENCES [dbo].[Accounts] ([Id]),
    CONSTRAINT [FK_{Mappings}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex(Mappings, $"IX_{Mappings}_AccountId", "[AccountId]"));
            migrationBuilder.Sql(CreateIndex(Mappings, $"UX_{Mappings}_Tenant_Role", "[TenantId], [Role]",
                unique: true, filter: "[IsDeleted] = 0"));

            // ── Which events post ───────────────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Rules}', 'U') IS NULL
CREATE TABLE [dbo].[{Rules}] (
    [Id]                         uniqueidentifier NOT NULL,
    [EventCode]                  nvarchar(60)     NOT NULL,
    [IsEnabled]                  bit              NOT NULL,
    [PostOnActionDate]           bit              NOT NULL,
    [Notes]                      nvarchar(500)    NULL,{AuditColumns}
    CONSTRAINT [PK_{Rules}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Rules}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex(Rules, $"UX_{Rules}_Tenant_Event", "[TenantId], [EventCode]",
                unique: true, filter: "[IsDeleted] = 0"));

            // ── The register ────────────────────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Records}', 'U') IS NULL
CREATE TABLE [dbo].[{Records}] (
    [Id]                         uniqueidentifier NOT NULL,
    [EventCode]                  nvarchar(60)     NOT NULL,
    [SourceDocumentType]         nvarchar(100)    NOT NULL,
    [SourceDocumentId]           uniqueidentifier NOT NULL,
    [SourceReference]            nvarchar(100)    NOT NULL,
    [EmployeeId]                 uniqueidentifier NULL,
    [Description]                nvarchar(500)    NOT NULL,
    [Amount]                     decimal(18,2)    NOT NULL,
    [CurrencyCode]               char(3)          NOT NULL,
    [TransactionCurrencyCode]    char(3)          NULL,
    [TransactionAmount]          decimal(18,2)    NULL,
    [PostingDate]                datetime2        NOT NULL,
    [Status]                     int              NOT NULL,
    [AccountingBookCode]         nvarchar(20)     NULL,
    [IdempotencyKey]             nvarchar(200)    NOT NULL,
    [PostingAction]              nvarchar(50)     NOT NULL,
    [Generation]                 int              NOT NULL,
    [PostingEventId]             uniqueidentifier NULL,
    [JournalEntryId]             uniqueidentifier NULL,
    [JournalEntryNumber]         nvarchar(50)     NULL,
    [PostedAt]                   datetime2        NULL,
    [StatusReason]               nvarchar(2000)   NULL,
    [AttemptCount]               int              NOT NULL,
    [LastAttemptAt]              datetime2        NULL,
    [LinesSnapshot]              nvarchar(max)    NULL,
    [ReversalPostingEventId]     uniqueidentifier NULL,
    [ReversalJournalEntryId]     uniqueidentifier NULL,
    [ReversalJournalEntryNumber] nvarchar(50)     NULL,
    [ReversedAt]                 datetime2        NULL,
    [ReversalReason]             nvarchar(500)    NULL,
    [LastActedByUserId]          uniqueidentifier NULL,{AuditColumns}
    CONSTRAINT [PK_{Records}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{Records}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");
            migrationBuilder.Sql(CreateIndex(Records, $"IX_{Records}_JournalEntryId", "[JournalEntryId]"));
            migrationBuilder.Sql(CreateIndex(Records, $"IX_{Records}_Tenant_Source", "[TenantId], [SourceDocumentId]"));
            migrationBuilder.Sql(CreateIndex(Records, $"IX_{Records}_Tenant_Status_Date", "[TenantId], [Status], [PostingDate]"));
            migrationBuilder.Sql(CreateIndex(Records, $"UX_{Records}_Tenant_Event_Source", "[TenantId], [EventCode], [SourceDocumentId]",
                unique: true, filter: "[IsDeleted] = 0"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropTable(Records));
            migrationBuilder.Sql(DropTable(Rules));
            migrationBuilder.Sql(DropTable(Mappings));
        }
    }
}
