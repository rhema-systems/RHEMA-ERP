using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20260907190000_AddProducerIntentStagingC7")]
public partial class AddProducerIntentStagingC7 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // C7 extends only the reviewed C6 aggregate. Refuse a partial or out-of-order installation.
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[AccountingEvents]', N'U') IS NULL OR OBJECT_ID(N'[TR_AccountingEvents_C6Authority]', N'TR') IS NULL
    THROW 51000, 'C7_PREFLIGHT: reviewed C6 AccountingEvent authority is required.', 1;
IF COL_LENGTH(N'AccountingEvents', N'ProducerDecisionStatus') IS NOT NULL
 OR COL_LENGTH(N'AccountingEvents', N'ProducerParticipantIdentity') IS NOT NULL
 OR COL_LENGTH(N'AccountingEvents', N'ProducerIntentSnapshotJson') IS NOT NULL
 OR COL_LENGTH(N'AccountingEvents', N'ProducerIntentSnapshotHash') IS NOT NULL
 OR COL_LENGTH(N'AccountingEvents', N'ProducerDecidedByUserId') IS NOT NULL
 OR COL_LENGTH(N'AccountingEvents', N'ProducerDecidedAtUtc') IS NOT NULL
 OR COL_LENGTH(N'AccountingEvents', N'ProducerDecisionReason') IS NOT NULL
    THROW 51000, 'C7_PREFLIGHT: partial producer-intent staging columns already exist.', 1;");

        migrationBuilder.AddColumn<string>(name: "ProducerDecisionStatus", table: "AccountingEvents",
            type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "NotRequired");
        migrationBuilder.AddColumn<string>(name: "ProducerParticipantIdentity", table: "AccountingEvents",
            type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ProducerIntentSnapshotJson", table: "AccountingEvents",
            type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ProducerIntentSnapshotHash", table: "AccountingEvents",
            type: "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ProducerDecidedByUserId", table: "AccountingEvents",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "ProducerDecidedAtUtc", table: "AccountingEvents",
            type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ProducerDecisionReason", table: "AccountingEvents",
            type: "nvarchar(500)", maxLength: 500, nullable: true);

        migrationBuilder.AddCheckConstraint(name: "CK_AccountingEvents_ProducerDecision", table: "AccountingEvents", sql:
            "([ProducerDecisionStatus] = 'NotRequired' AND [ProducerParticipantIdentity] IS NULL AND [ProducerIntentSnapshotJson] IS NULL AND [ProducerIntentSnapshotHash] IS NULL AND [ProducerDecidedByUserId] IS NULL AND [ProducerDecidedAtUtc] IS NULL AND [ProducerDecisionReason] IS NULL) OR ([ProducerDecisionStatus] = 'Pending' AND [ProducerParticipantIdentity] IS NOT NULL AND [ProducerIntentSnapshotJson] IS NOT NULL AND LEN([ProducerIntentSnapshotHash]) = 64 AND [ProducerDecidedByUserId] IS NULL AND [ProducerDecidedAtUtc] IS NULL AND [ProducerDecisionReason] IS NULL) OR ([ProducerDecisionStatus] IN ('Approved','Rejected') AND [ProducerParticipantIdentity] IS NOT NULL AND [ProducerIntentSnapshotJson] IS NOT NULL AND LEN([ProducerIntentSnapshotHash]) = 64 AND [ProducerDecidedByUserId] IS NOT NULL AND [ProducerDecidedAtUtc] IS NOT NULL AND [ProducerDecisionReason] IS NOT NULL)");

        migrationBuilder.Sql(@"
CREATE TRIGGER [TR_AccountingEvents_C7ProducerDecision]
ON [AccountingEvents] AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id]=i.[Id]
       WHERE d.[Id] IS NULL AND i.[ProducerDecisionStatus] NOT IN (N'NotRequired',N'Pending'))
        THROW 51000, 'C7_INSERT_STATE: producer decisions cannot be fabricated during prepare.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE i.[ProducerDecisionStatus] <> N'NotRequired' AND
      (i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2 <> UPPER(LTRIM(RTRIM(i.[ProducerParticipantIdentity]))) COLLATE Latin1_General_100_BIN2
       OR DATALENGTH(i.[ProducerParticipantIdentity]) <> DATALENGTH(UPPER(LTRIM(RTRIM(i.[ProducerParticipantIdentity]))))
       OR LEFT(i.[ProducerParticipantIdentity],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
       OR i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'))
        THROW 51000, 'C7_PARTICIPANT_IDENTITY: canonical stable participant identity is required.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE i.[ProducerDecisionStatus] IN (N'Approved',N'Rejected')
       AND (i.[ProducerDecidedByUserId] = i.[PreparedByUserId] OR LEN(LTRIM(RTRIM(i.[ProducerDecisionReason]))) = 0))
        THROW 51000, 'C7_MAKER_CHECKER: a distinct checker and governed reason are required.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE
       ISNULL(d.[ProducerParticipantIdentity],N'') <> ISNULL(i.[ProducerParticipantIdentity],N'')
       OR ISNULL(d.[ProducerIntentSnapshotHash],N'') <> ISNULL(i.[ProducerIntentSnapshotHash],N'')
       OR ISNULL(d.[ProducerIntentSnapshotJson],N'') <> ISNULL(i.[ProducerIntentSnapshotJson],N'')
       OR (d.[ProducerDecisionStatus] = N'Pending' AND i.[ProducerDecisionStatus] NOT IN (N'Pending',N'Approved',N'Rejected'))
       OR (d.[ProducerDecisionStatus] IN (N'NotRequired',N'Approved',N'Rejected') AND d.[ProducerDecisionStatus] <> i.[ProducerDecisionStatus])
       OR (d.[ProducerDecidedByUserId] IS NOT NULL AND (i.[ProducerDecidedByUserId] IS NULL OR d.[ProducerDecidedByUserId] <> i.[ProducerDecidedByUserId]
          OR d.[ProducerDecidedAtUtc] <> i.[ProducerDecidedAtUtc] OR d.[ProducerDecisionReason] <> i.[ProducerDecisionReason])))
        THROW 51000, 'C7_DECISION_IMMUTABLE: participant and maker-checker decision evidence cannot be rewritten.', 1;

    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE
       d.[ProducerDecisionStatus] = N'Pending' AND i.[ProducerDecisionStatus] IN (N'Approved',N'Rejected')
       AND (d.[Status] <> N'PendingApproval' OR i.[Status] <> N'PendingApproval'))
        THROW 51000, 'C7_DECISION_ONLY: approval or rejection cannot execute C6 effects in the same statement.', 1;

    IF EXISTS (SELECT 1 FROM inserted i WHERE
       (i.[ProducerDecisionStatus] = N'Pending' AND i.[Status] <> N'PendingApproval')
       OR (i.[ProducerDecisionStatus] = N'Rejected' AND i.[Status] <> N'PendingApproval')
       OR (i.[ProducerDecisionStatus] = N'Approved' AND i.[Status] NOT IN (N'PendingApproval',N'Pending',N'Posted',N'Failed')))
        THROW 51000, 'C7_EXECUTION_GATE: only an approved producer intent may enter C6 execution.', 1;
END;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [AccountingEvents] WHERE [ProducerDecisionStatus] <> N'NotRequired')
    THROW 51000, 'C7_DOWN_REFUSED: producer decision evidence exists.', 1;
DROP TRIGGER IF EXISTS [TR_AccountingEvents_C7ProducerDecision];");
        migrationBuilder.DropCheckConstraint(name: "CK_AccountingEvents_ProducerDecision", table: "AccountingEvents");
        migrationBuilder.DropColumn(name: "ProducerDecisionReason", table: "AccountingEvents");
        migrationBuilder.DropColumn(name: "ProducerDecidedAtUtc", table: "AccountingEvents");
        migrationBuilder.DropColumn(name: "ProducerDecidedByUserId", table: "AccountingEvents");
        migrationBuilder.DropColumn(name: "ProducerParticipantIdentity", table: "AccountingEvents");
        migrationBuilder.DropColumn(name: "ProducerIntentSnapshotHash", table: "AccountingEvents");
        migrationBuilder.DropColumn(name: "ProducerIntentSnapshotJson", table: "AccountingEvents");
        migrationBuilder.DropColumn(name: "ProducerDecisionStatus", table: "AccountingEvents");
    }
}
