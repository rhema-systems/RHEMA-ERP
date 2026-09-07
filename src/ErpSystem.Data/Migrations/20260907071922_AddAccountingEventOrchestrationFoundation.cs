using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext)), Migration("20260907071922_AddAccountingEventOrchestrationFoundation")]
    public partial class AddAccountingEventOrchestrationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // C6 has no truthful historical event graph to backfill. Validate the complete C1-C5
            // authority before the first schema mutation and refuse partially installed C6 objects.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[Tenants]', N'U') IS NULL
   OR OBJECT_ID(N'[AccountingBooks]', N'U') IS NULL
   OR OBJECT_ID(N'[AccountingBookSelectionEvidence]', N'U') IS NULL
   OR OBJECT_ID(N'[AccountingBookSelectionEvidenceBooks]', N'U') IS NULL
   OR OBJECT_ID(N'[FinancePostingEvents]', N'U') IS NULL
   OR OBJECT_ID(N'[JournalEntries]', N'U') IS NULL
   OR COL_LENGTH(N'AccountingBookSelectionEvidence', N'TenantId') IS NULL
   OR COL_LENGTH(N'AccountingBookSelectionEvidence', N'SelectionFingerprint') IS NULL
   OR COL_LENGTH(N'AccountingBookSelectionEvidenceBooks', N'AuthorityFingerprint') IS NULL
   OR COL_LENGTH(N'FinancePostingEvents', N'AccountingBookId') IS NULL
   OR COL_LENGTH(N'FinancePostingEvents', N'OriginModuleCode') IS NULL
   OR COL_LENGTH(N'FinancePostingEvents', N'SourceModule') IS NULL
   OR COL_LENGTH(N'FinancePostingEvents', N'SourceDocumentType') IS NULL
   OR COL_LENGTH(N'FinancePostingEvents', N'SourceDocumentId') IS NULL
   OR COL_LENGTH(N'FinancePostingEvents', N'PostingAction') IS NULL
   OR COL_LENGTH(N'JournalEntries', N'AccountingBookId') IS NULL
    THROW 51000, 'C6_SCHEMA_PREFLIGHT: complete C1-C5 selection and leaf-posting authority is required.', 1;

IF OBJECT_ID(N'[AccountingEvents]', N'U') IS NOT NULL
   OR OBJECT_ID(N'[AccountingEventPostings]', N'U') IS NOT NULL
   OR OBJECT_ID(N'[AccountingEventAttempts]', N'U') IS NOT NULL
    THROW 51000, 'C6_SCHEMA_PREFLIGHT: conflicting C6 tables already exist; no mutation was performed.', 1;

IF EXISTS (
    SELECT 1 FROM [AccountingBookSelectionEvidence] e
    WHERE e.[IsDeleted] = 0 AND (e.[SelectionFingerprint] = N'' OR NOT EXISTS (
        SELECT 1 FROM [AccountingBookSelectionEvidenceBooks] eb
        WHERE eb.[TenantId] = e.[TenantId]
          AND eb.[AccountingBookSelectionEvidenceId] = e.[Id] AND eb.[IsDeleted] = 0))
)
    THROW 51000, 'C6_SELECTION_PREFLIGHT: frozen selection evidence is blank or has no exact-book authority.', 1;
");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_JournalEntries_TenantId_Id",
                table: "JournalEntries",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_FinancePostingEvents_TenantId_Id",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "AccountingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginatingModuleCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostingAction = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RootAccountingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupersedesAccountingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectsAccountingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversesAccountingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountingBookSelectionEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectionFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EventDate = table.Column<DateTime>(type: "date", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleaseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEvents", x => x.Id);
                    table.UniqueConstraint("AK_AccountingEvents_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_AccountingEvents_TenantId_Id_Version", x => new { x.TenantId, x.Id, x.Version });
                    table.CheckConstraint("CK_AccountingEvents_Kind", "[EventKind] IN ('Original','Correction','Reversal')");
                    table.CheckConstraint("CK_AccountingEvents_Lineage", "([EventKind] = 'Original' AND [Version] = 1 AND [RootAccountingEventId] = [Id] AND [SupersedesAccountingEventId] IS NULL AND [CorrectsAccountingEventId] IS NULL AND [ReversesAccountingEventId] IS NULL) OR ([EventKind] = 'Correction' AND [Version] > 1 AND [SupersedesAccountingEventId] = [CorrectsAccountingEventId] AND [CorrectsAccountingEventId] IS NOT NULL AND [ReversesAccountingEventId] IS NULL) OR ([EventKind] = 'Reversal' AND [Version] > 1 AND [SupersedesAccountingEventId] = [ReversesAccountingEventId] AND [ReversesAccountingEventId] IS NOT NULL AND [CorrectsAccountingEventId] IS NULL)");
                    table.CheckConstraint("CK_AccountingEvents_MakerChecker", "[ReleasedByUserId] IS NULL OR [ReleasedByUserId] <> [PreparedByUserId]");
                    table.CheckConstraint("CK_AccountingEvents_NoDelete", "[IsDeleted] = 0");
                    table.CheckConstraint("CK_AccountingEvents_RequestFingerprint", "LEN([RequestFingerprint]) = 64 AND [RequestFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AccountingEvents_ResultShape", "([Status] = 'PendingApproval' AND [AccountingBookSelectionEvidenceId] IS NULL AND [ReleasedByUserId] IS NULL AND [ReleasedAtUtc] IS NULL AND [ReleaseReason] IS NULL AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Pending' AND [ReleasedByUserId] IS NOT NULL AND [ReleasedAtUtc] IS NOT NULL AND [ReleaseReason] IS NOT NULL AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Posted' AND [AccountingBookSelectionEvidenceId] IS NOT NULL AND LEN([SelectionFingerprint]) = 64 AND [ReleasedByUserId] IS NOT NULL AND [ReleasedAtUtc] IS NOT NULL AND [ReleaseReason] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Failed' AND [ReleasedByUserId] IS NOT NULL AND [ReleasedAtUtc] IS NOT NULL AND [ReleaseReason] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NOT NULL)");
                    table.CheckConstraint("CK_AccountingEvents_SelectionFingerprint", "[SelectionFingerprint] = '' OR (LEN([SelectionFingerprint]) = 64 AND [SelectionFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%')");
                    table.CheckConstraint("CK_AccountingEvents_Status", "[Status] IN ('PendingApproval','Pending','Posted','Failed')");
                    table.CheckConstraint("CK_AccountingEvents_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_AccountingEvents_AccountingBookSelectionEvidence_TenantId_AccountingBookSelectionEvidenceId",
                        columns: x => new { x.TenantId, x.AccountingBookSelectionEvidenceId },
                        principalTable: "AccountingBookSelectionEvidence",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEvents_AccountingEvents_TenantId_CorrectsAccountingEventId",
                        columns: x => new { x.TenantId, x.CorrectsAccountingEventId },
                        principalTable: "AccountingEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEvents_AccountingEvents_TenantId_ReversesAccountingEventId",
                        columns: x => new { x.TenantId, x.ReversesAccountingEventId },
                        principalTable: "AccountingEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEvents_AccountingEvents_TenantId_RootAccountingEventId",
                        columns: x => new { x.TenantId, x.RootAccountingEventId },
                        principalTable: "AccountingEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEvents_AccountingEvents_TenantId_SupersedesAccountingEventId",
                        columns: x => new { x.TenantId, x.SupersedesAccountingEventId },
                        principalTable: "AccountingEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingEventAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEventAttempts", x => x.Id);
                    table.CheckConstraint("CK_AccountingEventAttempts_NoDelete", "[IsDeleted] = 0");
                    table.CheckConstraint("CK_AccountingEventAttempts_Number", "[AttemptNumber] > 0");
                    table.CheckConstraint("CK_AccountingEventAttempts_RequestFingerprint", "LEN([RequestFingerprint]) = 64 AND [RequestFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AccountingEventAttempts_ResultShape", "([Status] = 'Pending' AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Posted' AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Failed' AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NOT NULL)");
                    table.CheckConstraint("CK_AccountingEventAttempts_Status", "[Status] IN ('Pending','Posted','Failed')");
                    table.ForeignKey(
                        name: "FK_AccountingEventAttempts_AccountingEvents_TenantId_AccountingEventId",
                        columns: x => new { x.TenantId, x.AccountingEventId },
                        principalTable: "AccountingEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEventAttempts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingEventPostings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventVersion = table.Column<int>(type: "int", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectionOrder = table.Column<int>(type: "int", nullable: false),
                    AccountingBookCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AuthorityFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FinancePostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEventPostings", x => x.Id);
                    table.CheckConstraint("CK_AccountingEventPostings_AuthorityFingerprint", "LEN([AuthorityFingerprint]) = 64 AND [AuthorityFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AccountingEventPostings_EventVersion", "[EventVersion] > 0");
                    table.CheckConstraint("CK_AccountingEventPostings_NoDelete", "[IsDeleted] = 0");
                    table.CheckConstraint("CK_AccountingEventPostings_ResultShape", "([Status] = 'Pending' AND [FinancePostingEventId] IS NULL AND [JournalEntryId] IS NULL AND [PostedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Posted' AND [FinancePostingEventId] IS NOT NULL AND [JournalEntryId] IS NOT NULL AND [PostedAtUtc] IS NOT NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Failed' AND [FailureMessage] IS NOT NULL)");
                    table.CheckConstraint("CK_AccountingEventPostings_Status", "[Status] IN ('Pending','Posted','Failed')");
                    table.ForeignKey(
                        name: "FK_AccountingEventPostings_AccountingBooks_TenantId_AccountingBookId",
                        columns: x => new { x.TenantId, x.AccountingBookId },
                        principalTable: "AccountingBooks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEventPostings_AccountingEvents_TenantId_AccountingEventId_EventVersion",
                        columns: x => new { x.TenantId, x.AccountingEventId, x.EventVersion },
                        principalTable: "AccountingEvents",
                        principalColumns: new[] { "TenantId", "Id", "Version" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEventPostings_FinancePostingEvents_TenantId_FinancePostingEventId",
                        columns: x => new { x.TenantId, x.FinancePostingEventId },
                        principalTable: "FinancePostingEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEventPostings_JournalEntries_TenantId_JournalEntryId",
                        columns: x => new { x.TenantId, x.JournalEntryId },
                        principalTable: "JournalEntries",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEventPostings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEventAttempts_TenantId_AccountingEventId_AttemptNumber",
                table: "AccountingEventAttempts",
                columns: new[] { "TenantId", "AccountingEventId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEventPostings_TenantId_AccountingBookId",
                table: "AccountingEventPostings",
                columns: new[] { "TenantId", "AccountingBookId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEventPostings_TenantId_AccountingEventId_EventVersion_AccountingBookId",
                table: "AccountingEventPostings",
                columns: new[] { "TenantId", "AccountingEventId", "EventVersion", "AccountingBookId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEventPostings_TenantId_AccountingEventId_EventVersion_SelectionOrder",
                table: "AccountingEventPostings",
                columns: new[] { "TenantId", "AccountingEventId", "EventVersion", "SelectionOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEventPostings_TenantId_FinancePostingEventId",
                table: "AccountingEventPostings",
                columns: new[] { "TenantId", "FinancePostingEventId" },
                unique: true,
                filter: "[FinancePostingEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEventPostings_TenantId_JournalEntryId",
                table: "AccountingEventPostings",
                columns: new[] { "TenantId", "JournalEntryId" },
                unique: true,
                filter: "[JournalEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEvents_TenantId_AccountingBookSelectionEvidenceId",
                table: "AccountingEvents",
                columns: new[] { "TenantId", "AccountingBookSelectionEvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEvents_TenantId_CorrectsAccountingEventId",
                table: "AccountingEvents",
                columns: new[] { "TenantId", "CorrectsAccountingEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEvents_TenantId_IdempotencyKey",
                table: "AccountingEvents",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEvents_TenantId_OriginatingModuleCode_SourceDocumentType_SourceDocumentId_PostingAction_Version",
                table: "AccountingEvents",
                columns: new[] { "TenantId", "OriginatingModuleCode", "SourceDocumentType", "SourceDocumentId", "PostingAction", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEvents_TenantId_ReversesAccountingEventId",
                table: "AccountingEvents",
                columns: new[] { "TenantId", "ReversesAccountingEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEvents_TenantId_RootAccountingEventId_Version",
                table: "AccountingEvents",
                columns: new[] { "TenantId", "RootAccountingEventId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEvents_TenantId_SupersedesAccountingEventId",
                table: "AccountingEvents",
                columns: new[] { "TenantId", "SupersedesAccountingEventId" });

            // Foreign keys establish tenant lineage. These guards additionally bind each successor
            // version and each per-book result to the exact immutable C5 selection it represents.
            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_AccountingEvents_C6Authority]
ON [AccountingEvents] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id]=d.[Id] WHERE i.[Id] IS NULL)
        THROW 51000, 'C6_EVENT_IMMUTABLE: AccountingEvents cannot be deleted.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE i.[TenantId]='00000000-0000-0000-0000-000000000000'
       OR i.[SourceDocumentId]='00000000-0000-0000-0000-000000000000'
       OR i.[PreparedByUserId]='00000000-0000-0000-0000-000000000000'
       OR LEN(i.[OriginatingModuleCode])=0 OR LEN(i.[SourceDocumentType])=0 OR LEN(i.[PostingAction])=0 OR LEN(i.[IdempotencyKey])=0
       OR i.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[OriginatingModuleCode]))) COLLATE Latin1_General_100_BIN2
       OR i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[SourceDocumentType]))) COLLATE Latin1_General_100_BIN2
       OR i.[PostingAction] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[PostingAction]))) COLLATE Latin1_General_100_BIN2
       OR i.[IdempotencyKey] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[IdempotencyKey]))) COLLATE Latin1_General_100_BIN2
       OR DATALENGTH(i.[OriginatingModuleCode])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[OriginatingModuleCode]))))
       OR DATALENGTH(i.[SourceDocumentType])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[SourceDocumentType]))))
       OR DATALENGTH(i.[PostingAction])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[PostingAction]))))
       OR DATALENGTH(i.[IdempotencyKey])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[IdempotencyKey])))))
        THROW 51000, 'C6_EVENT_IDENTITY: canonical nonblank tenant, source, action, actor and idempotency identity is required.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
       WHERE d.[TenantId]<>i.[TenantId] OR d.[OriginatingModuleCode]<>i.[OriginatingModuleCode]
          OR d.[SourceDocumentType]<>i.[SourceDocumentType] OR d.[SourceDocumentId]<>i.[SourceDocumentId]
          OR d.[PostingAction]<>i.[PostingAction] OR d.[IdempotencyKey]<>i.[IdempotencyKey]
          OR d.[EventKind]<>i.[EventKind] OR d.[Version]<>i.[Version] OR d.[RootAccountingEventId]<>i.[RootAccountingEventId]
          OR ISNULL(d.[SupersedesAccountingEventId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[SupersedesAccountingEventId],'00000000-0000-0000-0000-000000000000')
          OR ISNULL(d.[CorrectsAccountingEventId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[CorrectsAccountingEventId],'00000000-0000-0000-0000-000000000000')
          OR ISNULL(d.[ReversesAccountingEventId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[ReversesAccountingEventId],'00000000-0000-0000-0000-000000000000')
          OR d.[RequestFingerprint]<>i.[RequestFingerprint] OR d.[EventDate]<>i.[EventDate]
          OR d.[RequestedAtUtc]<>i.[RequestedAtUtc] OR d.[RequestedByUserId]<>i.[RequestedByUserId]
          OR d.[PreparedByUserId]<>i.[PreparedByUserId] OR d.[PreparedAtUtc]<>i.[PreparedAtUtc]
          OR (d.[AccountingBookSelectionEvidenceId] IS NOT NULL AND (i.[AccountingBookSelectionEvidenceId] IS NULL OR d.[AccountingBookSelectionEvidenceId]<>i.[AccountingBookSelectionEvidenceId] OR d.[SelectionFingerprint]<>i.[SelectionFingerprint]))
          OR (d.[ReleasedByUserId] IS NOT NULL AND (i.[ReleasedByUserId] IS NULL OR i.[ReleasedAtUtc] IS NULL OR i.[ReleaseReason] IS NULL OR d.[ReleasedByUserId]<>i.[ReleasedByUserId] OR d.[ReleasedAtUtc]<>i.[ReleasedAtUtc] OR d.[ReleaseReason]<>i.[ReleaseReason])))
        THROW 51000, 'C6_EVENT_IMMUTABLE: economic, version, reversal and preparer identity cannot be rewritten.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
       WHERE (d.[Status]=N'Posted' AND i.[Status]<>N'Posted')
          OR (d.[Status]=N'PendingApproval' AND i.[Status] NOT IN (N'PendingApproval',N'Pending',N'Failed'))
          OR (d.[Status]=N'Pending' AND i.[Status] NOT IN (N'Pending',N'Posted',N'Failed'))
          OR (d.[Status]=N'Failed' AND i.[Status] NOT IN (N'Failed',N'Pending')))
        THROW 51000, 'C6_EVENT_STATUS: only governed preparation, release, failure and retry transitions are permitted.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
       WHERE d.[Status]=i.[Status] AND d.[Status] IN (N'Posted',N'Failed') AND
         (ISNULL(d.[CompletedAtUtc],CONVERT(datetime2,'1900-01-01'))<>ISNULL(i.[CompletedAtUtc],CONVERT(datetime2,'1900-01-01'))
          OR ISNULL(d.[FailureMessage],N'')<>ISNULL(i.[FailureMessage],N'')))
        THROW 51000, 'C6_EVENT_OUTCOME_IMMUTABLE: persisted Posted and Failed outcome evidence cannot be rewritten.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN [AccountingEvents] p WITH (UPDLOCK,HOLDLOCK)
       ON p.[TenantId]=i.[TenantId] AND p.[Id]=i.[SupersedesAccountingEventId]
       WHERE i.[EventKind] IN (N'Correction',N'Reversal') AND (p.[Status]<>N'Posted'
          OR i.[RootAccountingEventId]<>p.[RootAccountingEventId] OR i.[Version]<>p.[Version]+1
          OR i.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2<>p.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(i.[OriginatingModuleCode])<>DATALENGTH(p.[OriginatingModuleCode])
          OR i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>p.[SourceDocumentType] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(i.[SourceDocumentType])<>DATALENGTH(p.[SourceDocumentType])
          OR i.[SourceDocumentId]<>p.[SourceDocumentId]
          OR i.[PostingAction] COLLATE Latin1_General_100_BIN2<>p.[PostingAction] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(i.[PostingAction])<>DATALENGTH(p.[PostingAction])
          OR i.[AccountingBookSelectionEvidenceId]<>p.[AccountingBookSelectionEvidenceId]
          OR i.[SelectionFingerprint]<>p.[SelectionFingerprint]))
        THROW 51000, 'C6_EVENT_LINEAGE: successor must bind the posted predecessor canonical source, root, next version and frozen selection.', 1;

    IF EXISTS (SELECT 1 FROM [AccountingEvents] a WITH (UPDLOCK,HOLDLOCK)
       JOIN [AccountingEvents] b WITH (UPDLOCK,HOLDLOCK) ON b.[TenantId]=a.[TenantId]
        AND b.[SupersedesAccountingEventId]=a.[SupersedesAccountingEventId] AND b.[Id]<>a.[Id]
       WHERE a.[SupersedesAccountingEventId] IS NOT NULL)
        THROW 51000, 'C6_EVENT_LINEAGE: a predecessor may have only one canonical successor.', 1;

    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN [AccountingBookSelectionEvidence] e WITH (UPDLOCK,HOLDLOCK)
       ON e.[TenantId]=i.[TenantId] AND e.[Id]=i.[AccountingBookSelectionEvidenceId]
       WHERE (i.[Status] IN (N'Pending',N'Posted') OR i.[AccountingBookSelectionEvidenceId] IS NOT NULL) AND (e.[Id] IS NULL
          OR e.[SelectionFingerprint]<>i.[SelectionFingerprint]
          OR e.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2<>i.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(e.[OriginatingModuleCode])<>DATALENGTH(i.[OriginatingModuleCode])
          OR e.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(e.[SourceDocumentType])<>DATALENGTH(i.[SourceDocumentType])
          OR e.[PostingAction] COLLATE Latin1_General_100_BIN2<>i.[PostingAction] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(e.[PostingAction])<>DATALENGTH(i.[PostingAction])
          OR (i.[EventKind]=N'Original' AND CONVERT(date,e.[EffectiveDate])<>i.[EventDate])))
        THROW 51000, 'C6_EVENT_SELECTION: released event identity must match its immutable C5 selection.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE i.[Status]=N'Posted' AND (
       NOT EXISTS (SELECT 1 FROM [AccountingEventPostings] p WHERE p.[TenantId]=i.[TenantId] AND p.[AccountingEventId]=i.[Id])
       OR EXISTS (SELECT eb.[AccountingBookId],eb.[SelectionOrder],eb.[AccountingBookCodeSnapshot],eb.[AuthorityFingerprint]
          FROM [AccountingBookSelectionEvidenceBooks] eb WHERE eb.[TenantId]=i.[TenantId] AND eb.[AccountingBookSelectionEvidenceId]=i.[AccountingBookSelectionEvidenceId]
          EXCEPT SELECT p.[AccountingBookId],p.[SelectionOrder],p.[AccountingBookCodeSnapshot],p.[AuthorityFingerprint]
          FROM [AccountingEventPostings] p WHERE p.[TenantId]=i.[TenantId] AND p.[AccountingEventId]=i.[Id] AND p.[Status]=N'Posted')
       OR EXISTS (SELECT p.[AccountingBookId],p.[SelectionOrder],p.[AccountingBookCodeSnapshot],p.[AuthorityFingerprint]
          FROM [AccountingEventPostings] p WHERE p.[TenantId]=i.[TenantId] AND p.[AccountingEventId]=i.[Id] AND p.[Status]=N'Posted'
          EXCEPT SELECT eb.[AccountingBookId],eb.[SelectionOrder],eb.[AccountingBookCodeSnapshot],eb.[AuthorityFingerprint]
          FROM [AccountingBookSelectionEvidenceBooks] eb WHERE eb.[TenantId]=i.[TenantId] AND eb.[AccountingBookSelectionEvidenceId]=i.[AccountingBookSelectionEvidenceId])))
        THROW 51000, 'C6_EVENT_RELEASE: every and only frozen selected-book representation must be posted.', 1;
END;

");
            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_AccountingEventPostings_C6Authority]
ON [AccountingEventPostings] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id]=d.[Id] WHERE i.[Id] IS NULL)
        THROW 51000, 'C6_POSTING_IMMUTABLE: exact-book evidence cannot be deleted.', 1;
    IF EXISTS (SELECT 1 FROM deleted d JOIN inserted i ON i.[Id]=d.[Id]
       WHERE d.[TenantId]<>i.[TenantId] OR d.[AccountingEventId]<>i.[AccountingEventId]
          OR d.[EventVersion]<>i.[EventVersion] OR d.[AccountingBookId]<>i.[AccountingBookId]
          OR d.[SelectionOrder]<>i.[SelectionOrder] OR d.[AccountingBookCodeSnapshot]<>i.[AccountingBookCodeSnapshot]
          OR d.[AuthorityFingerprint]<>i.[AuthorityFingerprint] OR d.[Status]<>N'Pending' OR i.[Status] NOT IN (N'Posted',N'Failed'))
        THROW 51000, 'C6_POSTING_IMMUTABLE: only Pending-to-final result completion is permitted.', 1;
    IF EXISTS (SELECT 1 FROM inserted p JOIN [AccountingEvents] e WITH (UPDLOCK,HOLDLOCK)
       ON e.[TenantId]=p.[TenantId] AND e.[Id]=p.[AccountingEventId]
       LEFT JOIN [AccountingBookSelectionEvidenceBooks] eb WITH (UPDLOCK,HOLDLOCK)
       ON eb.[TenantId]=p.[TenantId] AND eb.[AccountingBookSelectionEvidenceId]=e.[AccountingBookSelectionEvidenceId]
        AND eb.[AccountingBookId]=p.[AccountingBookId] AND eb.[SelectionOrder]=p.[SelectionOrder]
       WHERE e.[Version]<>p.[EventVersion] OR e.[AccountingBookSelectionEvidenceId] IS NULL OR eb.[Id] IS NULL
        OR eb.[AccountingBookCodeSnapshot]<>p.[AccountingBookCodeSnapshot] OR eb.[AuthorityFingerprint]<>p.[AuthorityFingerprint])
        THROW 51000, 'C6_POSTING_SELECTION: per-book evidence must exactly match the frozen C5 coordinate.', 1;
    IF EXISTS (SELECT 1 FROM inserted p
       JOIN [AccountingEvents] e ON e.[TenantId]=p.[TenantId] AND e.[Id]=p.[AccountingEventId] AND e.[Version]=p.[EventVersion]
       LEFT JOIN [AccountingEventPostings] predecessor ON predecessor.[TenantId]=p.[TenantId]
        AND predecessor.[AccountingEventId]=e.[ReversesAccountingEventId] AND predecessor.[AccountingBookId]=p.[AccountingBookId]
        AND predecessor.[EventVersion]=e.[Version]-1 AND predecessor.[Status]=N'Posted'
       LEFT JOIN [FinancePostingEvents] f ON f.[TenantId]=p.[TenantId] AND f.[Id]=p.[FinancePostingEventId]
       LEFT JOIN [JournalEntries] j ON j.[TenantId]=p.[TenantId] AND j.[Id]=p.[JournalEntryId]
       WHERE p.[Status]=N'Posted' AND (f.[Id] IS NULL OR j.[Id] IS NULL OR f.[AccountingBookId]<>p.[AccountingBookId]
        OR j.[AccountingBookId]<>p.[AccountingBookId] OR f.[JournalEntryId] IS NULL OR f.[JournalEntryId]<>p.[JournalEntryId]
        OR f.[PostingStatus]<>N'Posted' OR j.[PostingStatus]<>N'Posted'
        OR (e.[EventKind]=N'Original' AND (
             ISNULL(f.[OriginModuleCode],f.[SourceModule]) COLLATE Latin1_General_100_BIN2<>e.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(ISNULL(f.[OriginModuleCode],f.[SourceModule]))<>DATALENGTH(e.[OriginatingModuleCode])
          OR f.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>e.[SourceDocumentType] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(f.[SourceDocumentType])<>DATALENGTH(e.[SourceDocumentType]) OR f.[SourceDocumentId]<>e.[SourceDocumentId]
          OR f.[PostingAction] COLLATE Latin1_General_100_BIN2<>e.[PostingAction] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(f.[PostingAction])<>DATALENGTH(e.[PostingAction])))
        OR (e.[EventKind]=N'Correction' AND (
             f.[SourceModule] COLLATE Latin1_General_100_BIN2<>N'GL' OR DATALENGTH(f.[SourceModule])<>DATALENGTH(N'GL')
          OR f.[OriginModuleCode] COLLATE Latin1_General_100_BIN2<>N'FIN' OR DATALENGTH(f.[OriginModuleCode])<>DATALENGTH(N'FIN')
          OR f.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>N'AccountingEventCorrection' OR DATALENGTH(f.[SourceDocumentType])<>DATALENGTH(N'AccountingEventCorrection')
          OR f.[SourceDocumentId]<>e.[Id]
          OR f.[PostingAction] COLLATE Latin1_General_100_BIN2<>e.[PostingAction] COLLATE Latin1_General_100_BIN2
          OR DATALENGTH(f.[PostingAction])<>DATALENGTH(e.[PostingAction])))
        OR (e.[EventKind]=N'Reversal' AND (
             predecessor.[Id] IS NULL OR predecessor.[FinancePostingEventId] IS NULL OR f.[SourceDocumentId]<>predecessor.[FinancePostingEventId]
          OR f.[SourceModule] COLLATE Latin1_General_100_BIN2<>N'GL' OR DATALENGTH(f.[SourceModule])<>DATALENGTH(N'GL')
          OR f.[OriginModuleCode] COLLATE Latin1_General_100_BIN2<>N'FIN' OR DATALENGTH(f.[OriginModuleCode])<>DATALENGTH(N'FIN')
          OR f.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>N'FinancePostingEventReversal' OR DATALENGTH(f.[SourceDocumentType])<>DATALENGTH(N'FinancePostingEventReversal')
          OR f.[PostingAction] COLLATE Latin1_General_100_BIN2<>N'Reverse' OR DATALENGTH(f.[PostingAction])<>DATALENGTH(N'Reverse')))))
        THROW 51000, 'C6_POSTING_RESULT: leaf journal and posting event must be Posted in the exact selected book.', 1;
END;

");
            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_AccountingEventAttempts_C6AppendOnly]
ON [AccountingEventAttempts] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51000, 'C6_ATTEMPT_IMMUTABLE: final attempt outcomes are append-only.', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE [Status]=N'Pending')
        THROW 51000, 'C6_ATTEMPT_FINAL_REQUIRED: attempt evidence must be inserted once with its final outcome.', 1;
    IF EXISTS (SELECT 1 FROM inserted a JOIN [AccountingEvents] e
       ON e.[TenantId]=a.[TenantId] AND e.[Id]=a.[AccountingEventId]
       WHERE a.[RequestFingerprint]<>e.[RequestFingerprint])
        THROW 51000, 'C6_ATTEMPT_IDENTITY: attempt fingerprint must match the immutable event request.', 1;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The predecessor cannot represent neutral events, attempts, or per-book release evidence.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [AccountingEventAttempts]) OR EXISTS (SELECT 1 FROM [AccountingEventPostings])
   OR EXISTS (SELECT 1 FROM [AccountingEvents])
    THROW 51000, 'C6_DOWN_BLOCKED: AccountingEvent evidence exists and cannot be represented by the predecessor schema.', 1;
DROP TRIGGER IF EXISTS [TR_AccountingEventAttempts_C6AppendOnly];
DROP TRIGGER IF EXISTS [TR_AccountingEventPostings_C6Authority];
DROP TRIGGER IF EXISTS [TR_AccountingEvents_C6Authority];
");

            migrationBuilder.DropTable(
                name: "AccountingEventAttempts");

            migrationBuilder.DropTable(
                name: "AccountingEventPostings");

            migrationBuilder.DropTable(
                name: "AccountingEvents");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_JournalEntries_TenantId_Id",
                table: "JournalEntries");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FinancePostingEvents_TenantId_Id",
                table: "FinancePostingEvents");
        }
    }
}
