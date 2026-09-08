using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20260908120000_AddProducerIntentGroupsC8")]
public partial class AddProducerIntentGroupsC8 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'[AccountingEvents]', N'U') IS NULL
 OR OBJECT_ID(N'[AccountingEventProducerReceipts]', N'U') IS NULL
 OR OBJECT_ID(N'[TR_AccountingEvents_C7ProducerDecision]', N'TR') IS NULL
 OR OBJECT_ID(N'[TR_AccountingEventProducerReceipts_C7Immutable]', N'TR') IS NULL
    THROW 51000, 'C8_PREFLIGHT: reviewed C7 producer authority is required.', 1;
IF OBJECT_ID(N'[ProducerIntentGroups]', N'U') IS NOT NULL
 OR OBJECT_ID(N'[ProducerIntentGroupMembers]', N'U') IS NOT NULL
 OR OBJECT_ID(N'[ProducerIntentGroupReceipts]', N'U') IS NOT NULL
 OR OBJECT_ID(N'[ProducerIntentGroupAttempts]', N'U') IS NOT NULL
    THROW 51000, 'C8_PREFLIGHT: partial producer-group authority already exists.', 1;

CREATE TABLE [ProducerIntentGroups] (
 [Id] uniqueidentifier NOT NULL, [IdempotencyKey] nvarchar(100) NOT NULL,
 [GroupKind] nvarchar(20) NOT NULL, [Version] int NOT NULL,
 [RootProducerIntentGroupId] uniqueidentifier NOT NULL, [SupersedesProducerIntentGroupId] uniqueidentifier NULL,
 [CorrectsProducerIntentGroupId] uniqueidentifier NULL, [ReversesProducerIntentGroupId] uniqueidentifier NULL,
 [Status] nvarchar(20) NOT NULL, [MemberCount] int NOT NULL,
 [ParticipantCode] nvarchar(100) NOT NULL, [OwnerEntityType] nvarchar(100) NOT NULL,
 [OwnerEntityId] uniqueidentifier NOT NULL, [OwnerAction] nvarchar(60) NOT NULL,
 [ExpectedOwnerEffectFingerprint] nvarchar(64) NOT NULL,
 [RequestSnapshotJson] nvarchar(max) NOT NULL, [RequestSnapshotHash] nvarchar(64) NOT NULL,
 [GroupFingerprint] nvarchar(64) NOT NULL, [PreparedByUserId] uniqueidentifier NOT NULL,
 [PreparedAtUtc] datetime2 NOT NULL, [DecidedByUserId] uniqueidentifier NULL,
 [DecidedAtUtc] datetime2 NULL, [DecisionReason] nvarchar(500) NULL,
 [CompletedAtUtc] datetime2 NULL, [FailureMessage] nvarchar(1000) NULL,
 [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL,
 [UpdatedBy] nvarchar(max) NULL, [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
 [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL, [TenantId] uniqueidentifier NOT NULL,
 CONSTRAINT [PK_ProducerIntentGroups] PRIMARY KEY ([Id]),
 CONSTRAINT [AK_ProducerIntentGroups_TenantId_Id] UNIQUE ([TenantId],[Id]),
 CONSTRAINT [CK_ProducerIntentGroups_NoDelete] CHECK ([IsDeleted]=0),
 CONSTRAINT [CK_ProducerIntentGroups_Status] CHECK ([Status] IN ('PendingApproval','Approved','Rejected','Posted','Failed')),
 CONSTRAINT [CK_ProducerIntentGroups_MemberCount] CHECK ([MemberCount] BETWEEN 2 AND 20),
 CONSTRAINT [CK_ProducerIntentGroups_Kind] CHECK ([GroupKind] IN ('Original','Correction','Reversal')),
 CONSTRAINT [CK_ProducerIntentGroups_Lineage] CHECK (([GroupKind]='Original' AND [Version]=1 AND [RootProducerIntentGroupId]=[Id] AND [SupersedesProducerIntentGroupId] IS NULL AND [CorrectsProducerIntentGroupId] IS NULL AND [ReversesProducerIntentGroupId] IS NULL) OR ([GroupKind]='Correction' AND [Version]>1 AND [SupersedesProducerIntentGroupId]=[CorrectsProducerIntentGroupId] AND [CorrectsProducerIntentGroupId] IS NOT NULL AND [ReversesProducerIntentGroupId] IS NULL) OR ([GroupKind]='Reversal' AND [Version]>1 AND [SupersedesProducerIntentGroupId]=[ReversesProducerIntentGroupId] AND [ReversesProducerIntentGroupId] IS NOT NULL AND [CorrectsProducerIntentGroupId] IS NULL)),
 CONSTRAINT [CK_ProducerIntentGroups_Decision] CHECK (([Status]='PendingApproval' AND [DecidedByUserId] IS NULL AND [DecidedAtUtc] IS NULL AND [DecisionReason] IS NULL AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] IN ('Approved','Rejected') AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL AND [DecisionReason] IS NOT NULL AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status]='Posted' AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL AND [DecisionReason] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NULL) OR ([Status]='Failed' AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL AND [DecisionReason] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NOT NULL)),
 CONSTRAINT [CK_ProducerIntentGroups_MakerChecker] CHECK ([DecidedByUserId] IS NULL OR [DecidedByUserId]<>[PreparedByUserId]),
 CONSTRAINT [CK_ProducerIntentGroups_GroupFingerprint] CHECK (LEN([GroupFingerprint])=64 AND [GroupFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
 CONSTRAINT [CK_ProducerIntentGroups_SnapshotHash] CHECK (LEN([RequestSnapshotHash])=64 AND [RequestSnapshotHash] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
 CONSTRAINT [CK_ProducerIntentGroups_EffectFingerprint] CHECK (LEN([ExpectedOwnerEffectFingerprint])=64 AND [ExpectedOwnerEffectFingerprint]<>REPLICATE('0',64) AND [ExpectedOwnerEffectFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
 CONSTRAINT [FK_ProducerIntentGroups_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
 CONSTRAINT [FK_ProducerIntentGroups_Root] FOREIGN KEY ([TenantId],[RootProducerIntentGroupId]) REFERENCES [ProducerIntentGroups]([TenantId],[Id]),
 CONSTRAINT [FK_ProducerIntentGroups_Supersedes] FOREIGN KEY ([TenantId],[SupersedesProducerIntentGroupId]) REFERENCES [ProducerIntentGroups]([TenantId],[Id]),
 CONSTRAINT [FK_ProducerIntentGroups_Corrects] FOREIGN KEY ([TenantId],[CorrectsProducerIntentGroupId]) REFERENCES [ProducerIntentGroups]([TenantId],[Id]),
 CONSTRAINT [FK_ProducerIntentGroups_Reverses] FOREIGN KEY ([TenantId],[ReversesProducerIntentGroupId]) REFERENCES [ProducerIntentGroups]([TenantId],[Id])
);
CREATE UNIQUE INDEX [IX_ProducerIntentGroups_TenantId_IdempotencyKey] ON [ProducerIntentGroups]([TenantId],[IdempotencyKey]);
CREATE UNIQUE INDEX [IX_ProducerIntentGroups_TenantId_GroupFingerprint] ON [ProducerIntentGroups]([TenantId],[GroupFingerprint]);
CREATE INDEX [IX_ProducerIntentGroups_TenantId_CorrectsProducerIntentGroupId] ON [ProducerIntentGroups]([TenantId],[CorrectsProducerIntentGroupId]);
CREATE INDEX [IX_ProducerIntentGroups_TenantId_ReversesProducerIntentGroupId] ON [ProducerIntentGroups]([TenantId],[ReversesProducerIntentGroupId]);
CREATE UNIQUE INDEX [IX_ProducerIntentGroups_TenantId_RootProducerIntentGroupId_Version] ON [ProducerIntentGroups]([TenantId],[RootProducerIntentGroupId],[Version]);
CREATE UNIQUE INDEX [IX_ProducerIntentGroups_TenantId_SupersedesProducerIntentGroupId] ON [ProducerIntentGroups]([TenantId],[SupersedesProducerIntentGroupId]) WHERE [SupersedesProducerIntentGroupId] IS NOT NULL;

CREATE TABLE [ProducerIntentGroupMembers] (
 [Id] uniqueidentifier NOT NULL, [ProducerIntentGroupId] uniqueidentifier NOT NULL, [AccountingEventId] uniqueidentifier NOT NULL,
 [MemberOrder] int NOT NULL, [MemberFingerprint] nvarchar(64) NOT NULL,
 [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
 [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL, [IsDeleted] bit NOT NULL,
 [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL, [TenantId] uniqueidentifier NOT NULL,
 CONSTRAINT [PK_ProducerIntentGroupMembers] PRIMARY KEY ([Id]),
 CONSTRAINT [CK_ProducerIntentGroupMembers_NoDelete] CHECK ([IsDeleted]=0),
 CONSTRAINT [CK_ProducerIntentGroupMembers_Order] CHECK ([MemberOrder]>0),
 CONSTRAINT [CK_ProducerIntentGroupMembers_Fingerprint] CHECK (LEN([MemberFingerprint])=64 AND [MemberFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
 CONSTRAINT [FK_ProducerIntentGroupMembers_Groups] FOREIGN KEY ([TenantId],[ProducerIntentGroupId]) REFERENCES [ProducerIntentGroups]([TenantId],[Id]),
 CONSTRAINT [FK_ProducerIntentGroupMembers_Events] FOREIGN KEY ([TenantId],[AccountingEventId]) REFERENCES [AccountingEvents]([TenantId],[Id]),
 CONSTRAINT [FK_ProducerIntentGroupMembers_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id])
);
CREATE UNIQUE INDEX [IX_ProducerIntentGroupMembers_TenantId_ProducerIntentGroupId_MemberOrder] ON [ProducerIntentGroupMembers]([TenantId],[ProducerIntentGroupId],[MemberOrder]);
CREATE UNIQUE INDEX [IX_ProducerIntentGroupMembers_TenantId_AccountingEventId] ON [ProducerIntentGroupMembers]([TenantId],[AccountingEventId]);

CREATE TABLE [ProducerIntentGroupReceipts] (
 [Id] uniqueidentifier NOT NULL, [ProducerIntentGroupId] uniqueidentifier NOT NULL,
 [ParticipantCode] nvarchar(100) NOT NULL, [OwnerEntityType] nvarchar(100) NOT NULL, [OwnerEntityId] uniqueidentifier NOT NULL,
 [OwnerAction] nvarchar(60) NOT NULL, [EffectFingerprint] nvarchar(64) NOT NULL, [GroupFingerprint] nvarchar(64) NOT NULL,
 [RecordedAtUtc] datetime2 NOT NULL, [RecordedByUserId] uniqueidentifier NOT NULL,
 [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
 [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL, [IsDeleted] bit NOT NULL,
 [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL, [TenantId] uniqueidentifier NOT NULL,
 CONSTRAINT [PK_ProducerIntentGroupReceipts] PRIMARY KEY ([Id]),
 CONSTRAINT [CK_ProducerIntentGroupReceipts_NoDelete] CHECK ([IsDeleted]=0),
 CONSTRAINT [CK_ProducerIntentGroupReceipts_EffectFingerprint] CHECK (LEN([EffectFingerprint])=64 AND [EffectFingerprint]<>REPLICATE('0',64) AND [EffectFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
 CONSTRAINT [CK_ProducerIntentGroupReceipts_GroupFingerprint] CHECK (LEN([GroupFingerprint])=64 AND [GroupFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
 CONSTRAINT [FK_ProducerIntentGroupReceipts_Groups] FOREIGN KEY ([TenantId],[ProducerIntentGroupId]) REFERENCES [ProducerIntentGroups]([TenantId],[Id]),
 CONSTRAINT [FK_ProducerIntentGroupReceipts_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id])
);
CREATE UNIQUE INDEX [IX_ProducerIntentGroupReceipts_TenantId_ProducerIntentGroupId] ON [ProducerIntentGroupReceipts]([TenantId],[ProducerIntentGroupId]);
CREATE UNIQUE INDEX [IX_ProducerIntentGroupReceipts_TenantId_ParticipantCode_EffectFingerprint] ON [ProducerIntentGroupReceipts]([TenantId],[ParticipantCode],[EffectFingerprint]);

CREATE TABLE [ProducerIntentGroupAttempts] (
 [Id] uniqueidentifier NOT NULL, [ProducerIntentGroupId] uniqueidentifier NOT NULL, [AttemptNumber] int NOT NULL,
 [GroupFingerprint] nvarchar(64) NOT NULL, [Status] nvarchar(20) NOT NULL, [StartedAtUtc] datetime2 NOT NULL,
 [CompletedAtUtc] datetime2 NULL, [FailedMemberOrder] int NULL, [FailedAccountingEventId] uniqueidentifier NULL,
 [FailureMessage] nvarchar(1000) NULL, [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL,
 [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL, [CreatedById] uniqueidentifier NULL,
 [LastModifiedById] uniqueidentifier NULL, [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL,
 [DeletedBy] nvarchar(max) NULL, [TenantId] uniqueidentifier NOT NULL,
 CONSTRAINT [PK_ProducerIntentGroupAttempts] PRIMARY KEY ([Id]),
 CONSTRAINT [CK_ProducerIntentGroupAttempts_NoDelete] CHECK ([IsDeleted]=0),
 CONSTRAINT [CK_ProducerIntentGroupAttempts_Number] CHECK ([AttemptNumber]>0),
 CONSTRAINT [CK_ProducerIntentGroupAttempts_Status] CHECK ([Status] IN ('Pending','Posted','Failed')),
 CONSTRAINT [CK_ProducerIntentGroupAttempts_Result] CHECK (([Status]='Pending' AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL AND [FailedMemberOrder] IS NULL AND [FailedAccountingEventId] IS NULL) OR ([Status]='Posted' AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NULL AND [FailedMemberOrder] IS NULL AND [FailedAccountingEventId] IS NULL) OR ([Status]='Failed' AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NOT NULL)),
 CONSTRAINT [CK_ProducerIntentGroupAttempts_Fingerprint] CHECK (LEN([GroupFingerprint])=64 AND [GroupFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
 CONSTRAINT [FK_ProducerIntentGroupAttempts_Groups] FOREIGN KEY ([TenantId],[ProducerIntentGroupId]) REFERENCES [ProducerIntentGroups]([TenantId],[Id]),
 CONSTRAINT [FK_ProducerIntentGroupAttempts_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id])
);
CREATE UNIQUE INDEX [IX_ProducerIntentGroupAttempts_TenantId_ProducerIntentGroupId_AttemptNumber] ON [ProducerIntentGroupAttempts]([TenantId],[ProducerIntentGroupId],[AttemptNumber]);

CREATE TRIGGER [TR_ProducerIntentGroupMembers_C8Immutable] ON [ProducerIntentGroupMembers] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51000, 'C8_MEMBER_IMMUTABLE: group membership is append-only.', 1;
 IF EXISTS(SELECT 1 FROM inserted m JOIN [ProducerIntentGroups] g ON g.[TenantId]=m.[TenantId] AND g.[Id]=m.[ProducerIntentGroupId]
   LEFT JOIN [AccountingEvents] e ON e.[TenantId]=m.[TenantId] AND e.[Id]=m.[AccountingEventId]
   WHERE g.[Status]<>N'PendingApproval' OR m.[MemberOrder]>g.[MemberCount] OR e.[Id] IS NULL
      OR e.[Status]<>N'PendingApproval' OR e.[ProducerDecisionStatus]<>N'Pending'
      OR e.[EventKind]<>g.[GroupKind] OR e.[Version]<>g.[Version]
      OR e.[IdempotencyKey] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(e.[IdempotencyKey]))) COLLATE Latin1_General_100_BIN2
      OR DATALENGTH(e.[IdempotencyKey])<>DATALENGTH(UPPER(LTRIM(RTRIM(e.[IdempotencyKey]))))
      OR LEFT(e.[IdempotencyKey],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z0-9]'
      OR e.[IdempotencyKey] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.:-]%'
      OR e.[RequestFingerprint]<>m.[MemberFingerprint] OR e.[ProducerParticipantIdentity]<>g.[ParticipantCode])
  THROW 51000, 'C8_MEMBER_AUTHORITY: ordered members must bind pending C7 events and the group participant.', 1;
 IF EXISTS(SELECT 1 FROM inserted m JOIN [ProducerIntentGroups] g ON g.[TenantId]=m.[TenantId] AND g.[Id]=m.[ProducerIntentGroupId]
   LEFT JOIN [ProducerIntentGroupMembers] pm ON pm.[TenantId]=g.[TenantId] AND pm.[ProducerIntentGroupId]=g.[SupersedesProducerIntentGroupId] AND pm.[MemberOrder]=m.[MemberOrder]
   LEFT JOIN [AccountingEvents] e ON e.[TenantId]=m.[TenantId] AND e.[Id]=m.[AccountingEventId]
   LEFT JOIN [AccountingEvents] pe ON pe.[TenantId]=pm.[TenantId] AND pe.[Id]=pm.[AccountingEventId]
   WHERE g.[GroupKind]<>N'Original' AND (pm.[Id] IS NULL OR pe.[Status]<>N'Posted'
     OR e.[SupersedesAccountingEventId]<>pe.[Id] OR e.[RootAccountingEventId]<>pe.[RootAccountingEventId]
     OR e.[Version]<>pe.[Version]+1
     OR e.[OriginatingModuleCode]<>pe.[OriginatingModuleCode] OR e.[SourceDocumentType]<>pe.[SourceDocumentType]
     OR e.[SourceDocumentId]<>pe.[SourceDocumentId] OR e.[PostingAction]<>pe.[PostingAction]
     OR (g.[GroupKind]=N'Correction' AND (e.[CorrectsAccountingEventId]<>pe.[Id] OR e.[ReversesAccountingEventId] IS NOT NULL))
     OR (g.[GroupKind]=N'Reversal' AND (e.[ReversesAccountingEventId]<>pe.[Id] OR e.[CorrectsAccountingEventId] IS NOT NULL))))
  THROW 51000, 'C8_MEMBER_LINEAGE: successor members must bind exact same-order posted predecessor roots.', 1;
END;

CREATE TRIGGER [TR_ProducerIntentGroupReceipts_C8Immutable] ON [ProducerIntentGroupReceipts] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51000, 'C8_RECEIPT_IMMUTABLE: group receipt evidence is append-only.', 1;
  IF EXISTS(SELECT 1 FROM inserted r WHERE
   r.[ParticipantCode] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[ParticipantCode]))) COLLATE Latin1_General_100_BIN2
   OR DATALENGTH(r.[ParticipantCode])<>DATALENGTH(UPPER(LTRIM(RTRIM(r.[ParticipantCode]))))
   OR LEFT(r.[ParticipantCode],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR r.[ParticipantCode] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR r.[OwnerEntityType] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[OwnerEntityType]))) COLLATE Latin1_General_100_BIN2
   OR LEFT(r.[OwnerEntityType],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR r.[OwnerEntityType] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR r.[OwnerAction] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[OwnerAction]))) COLLATE Latin1_General_100_BIN2
   OR LEFT(r.[OwnerAction],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR r.[OwnerAction] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%')
   THROW 51000, 'C8_RECEIPT_IDENTITY: receipt identities must use the reviewed canonical grammar.', 1;
  DECLARE @ownerTenant uniqueidentifier, @ownerParticipant nvarchar(100), @ownerEffect char(64), @ownerResource nvarchar(255), @ownerResult int;
  DECLARE owner_effects CURSOR LOCAL FAST_FORWARD FOR
   SELECT DISTINCT [TenantId],[ParticipantCode],[EffectFingerprint] FROM inserted ORDER BY [TenantId],[ParticipantCode],[EffectFingerprint];
  OPEN owner_effects; FETCH NEXT FROM owner_effects INTO @ownerTenant,@ownerParticipant,@ownerEffect;
  WHILE @@FETCH_STATUS=0 BEGIN
   SET @ownerResource=CONCAT(N'FIN:C7C8:',CONVERT(nvarchar(36),@ownerTenant),N'|',@ownerParticipant,N'|',@ownerEffect);
   EXEC @ownerResult=sp_getapplock @Resource=@ownerResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
   IF @ownerResult<0 BEGIN CLOSE owner_effects; DEALLOCATE owner_effects; THROW 51000, 'C8_RECEIPT_LOCK_FAILED: owner-effect identity could not be serialized.', 1; END;
   FETCH NEXT FROM owner_effects INTO @ownerTenant,@ownerParticipant,@ownerEffect;
  END; CLOSE owner_effects; DEALLOCATE owner_effects;
  IF EXISTS(SELECT 1 FROM inserted r JOIN [ProducerIntentGroups] g ON g.[TenantId]=r.[TenantId] AND g.[Id]=r.[ProducerIntentGroupId]
   WHERE g.[Status] NOT IN (N'Approved',N'Failed') OR g.[GroupFingerprint]<>r.[GroupFingerprint]
    OR g.[ParticipantCode]<>r.[ParticipantCode] OR g.[OwnerEntityType]<>r.[OwnerEntityType]
    OR g.[OwnerEntityId]<>r.[OwnerEntityId] OR g.[OwnerAction]<>r.[OwnerAction]
    OR g.[ExpectedOwnerEffectFingerprint]<>r.[EffectFingerprint])
  THROW 51000, 'C8_RECEIPT_AUTHORITY: one approved immutable group must own the exact receipt.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN [AccountingEventProducerReceipts] r ON r.[TenantId]=i.[TenantId]
   AND r.[ParticipantCode]=i.[ParticipantCode] AND r.[EffectFingerprint]=i.[EffectFingerprint])
  THROW 51000, 'C8_RECEIPT_REUSED: owner-effect evidence already belongs to a single event.', 1;
END;

CREATE TRIGGER [TR_ProducerIntentGroupAttempts_C8Immutable] ON [ProducerIntentGroupAttempts] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51000, 'C8_ATTEMPT_IMMUTABLE: durable group attempts are append-only.', 1;
 IF EXISTS(SELECT 1 FROM inserted a JOIN [ProducerIntentGroups] g ON g.[TenantId]=a.[TenantId] AND g.[Id]=a.[ProducerIntentGroupId]
  WHERE g.[GroupFingerprint]<>a.[GroupFingerprint] OR (a.[Status]=N'Pending' AND g.[Status] NOT IN (N'Approved',N'Failed'))
   OR (a.[Status]=N'Posted' AND g.[Status]<>N'Posted') OR (a.[Status]=N'Failed' AND g.[Status]<>N'Failed'))
  THROW 51000, 'C8_ATTEMPT_AUTHORITY: attempt outcome must match the immutable group lifecycle.', 1;
END;

CREATE TRIGGER [TR_ProducerIntentGroups_C8Authority] ON [ProducerIntentGroups] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id]=d.[Id] WHERE i.[Id] IS NULL)
  THROW 51000, 'C8_GROUP_IMMUTABLE: producer groups cannot be deleted.', 1;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id]=i.[Id] WHERE d.[Id] IS NULL AND i.[Status]<>N'PendingApproval')
  THROW 51000, 'C8_GROUP_INSERT_STATE: decision or execution cannot be preseeded.', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.[ParticipantCode] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[ParticipantCode]))) COLLATE Latin1_General_100_BIN2
   OR DATALENGTH(i.[ParticipantCode])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[ParticipantCode]))))
   OR LEFT(i.[ParticipantCode],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR i.[ParticipantCode] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR i.[IdempotencyKey] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[IdempotencyKey]))) COLLATE Latin1_General_100_BIN2
   OR DATALENGTH(i.[IdempotencyKey])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[IdempotencyKey]))))
   OR LEFT(i.[IdempotencyKey],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z0-9]'
   OR i.[IdempotencyKey] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.:-]%'
   OR i.[OwnerEntityType] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[OwnerEntityType]))) COLLATE Latin1_General_100_BIN2
   OR LEFT(i.[OwnerEntityType],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR i.[OwnerEntityType] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR i.[OwnerAction] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[OwnerAction]))) COLLATE Latin1_General_100_BIN2
   OR LEFT(i.[OwnerAction],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
   OR i.[OwnerAction] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
   OR i.[OwnerEntityId]='00000000-0000-0000-0000-000000000000')
  THROW 51000, 'C8_GROUP_IDENTITY: canonical participant and owner identity are required.', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.[Status] IN (N'Approved',N'Rejected',N'Posted',N'Failed')
   AND (i.[DecidedByUserId]=i.[PreparedByUserId] OR LEN(LTRIM(RTRIM(i.[DecisionReason])))=0))
  THROW 51000, 'C8_GROUP_MAKER_CHECKER: a distinct checker and governed reason are required.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE
   d.[TenantId]<>i.[TenantId] OR d.[IdempotencyKey]<>i.[IdempotencyKey] OR d.[GroupKind]<>i.[GroupKind]
   OR d.[Version]<>i.[Version] OR d.[RootProducerIntentGroupId]<>i.[RootProducerIntentGroupId]
   OR ISNULL(d.[SupersedesProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[SupersedesProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')
   OR ISNULL(d.[CorrectsProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[CorrectsProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')
   OR ISNULL(d.[ReversesProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')<>ISNULL(i.[ReversesProducerIntentGroupId],'00000000-0000-0000-0000-000000000000')
   OR d.[MemberCount]<>i.[MemberCount] OR d.[ParticipantCode]<>i.[ParticipantCode] OR d.[OwnerEntityType]<>i.[OwnerEntityType]
   OR d.[OwnerEntityId]<>i.[OwnerEntityId] OR d.[OwnerAction]<>i.[OwnerAction]
   OR d.[ExpectedOwnerEffectFingerprint]<>i.[ExpectedOwnerEffectFingerprint] OR d.[RequestSnapshotJson]<>i.[RequestSnapshotJson]
   OR d.[RequestSnapshotHash]<>i.[RequestSnapshotHash] OR d.[GroupFingerprint]<>i.[GroupFingerprint]
   OR d.[PreparedByUserId]<>i.[PreparedByUserId] OR d.[PreparedAtUtc]<>i.[PreparedAtUtc]
   OR (d.[DecidedByUserId] IS NOT NULL AND (i.[DecidedByUserId]<>d.[DecidedByUserId] OR i.[DecidedAtUtc]<>d.[DecidedAtUtc] OR i.[DecisionReason]<>d.[DecisionReason])))
  THROW 51000, 'C8_GROUP_EVIDENCE_IMMUTABLE: identity, lineage, membership, snapshot and decision cannot be rewritten.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
   WHERE (d.[Status]=N'PendingApproval' AND i.[Status] NOT IN (N'PendingApproval',N'Approved',N'Rejected'))
      OR (d.[Status]=N'Approved' AND i.[Status] NOT IN (N'Approved',N'Posted',N'Failed'))
      OR (d.[Status]=N'Failed' AND i.[Status] NOT IN (N'Failed',N'Posted'))
      OR (d.[Status] IN (N'Rejected',N'Posted') AND d.[Status]<>i.[Status]))
  THROW 51000, 'C8_GROUP_WORKFLOW: combined decision/execution and invalid transitions are forbidden.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
   WHERE d.[Status] IN (N'Rejected',N'Posted',N'Failed') AND i.[Status]=d.[Status]
    AND (ISNULL(i.[CompletedAtUtc],'0001-01-01')<>ISNULL(d.[CompletedAtUtc],'0001-01-01')
      OR ISNULL(i.[FailureMessage],N'')<>ISNULL(d.[FailureMessage],N'')))
  THROW 51000, 'C8_GROUP_OUTCOME_IMMUTABLE: terminal outcome evidence cannot be rewritten.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
   WHERE d.[Status]=N'Failed' AND i.[Status]=N'Posted'
    AND (i.[CompletedAtUtc]<=d.[CompletedAtUtc] OR i.[FailureMessage] IS NOT NULL))
  THROW 51000, 'C8_GROUP_RECOVERY: recovery must record a later posted completion and retain failed-attempt evidence.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
   WHERE d.[Status]=N'PendingApproval' AND i.[Status] IN (N'Approved',N'Rejected')
    AND ((SELECT COUNT(*) FROM [ProducerIntentGroupMembers] m WHERE m.[TenantId]=i.[TenantId] AND m.[ProducerIntentGroupId]=i.[Id])<>i.[MemberCount]
      OR EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers] m JOIN [AccountingEvents] e ON e.[TenantId]=m.[TenantId] AND e.[Id]=m.[AccountingEventId]
          WHERE m.[TenantId]=i.[TenantId] AND m.[ProducerIntentGroupId]=i.[Id] AND (e.[Status]<>N'PendingApproval' OR e.[ProducerDecisionStatus]<>N'Pending'))))
  THROW 51000, 'C8_GROUP_COMPLETE: the checker decides one complete pending immutable member set.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE i.[Status]=N'Posted' AND d.[Status]<>N'Posted'
   AND (NOT EXISTS(SELECT 1 FROM [ProducerIntentGroupReceipts] r WHERE r.[TenantId]=i.[TenantId] AND r.[ProducerIntentGroupId]=i.[Id])
    OR EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers] m JOIN [AccountingEvents] e ON e.[TenantId]=m.[TenantId] AND e.[Id]=m.[AccountingEventId]
       WHERE m.[TenantId]=i.[TenantId] AND m.[ProducerIntentGroupId]=i.[Id] AND e.[Status]<>N'Posted')))
  THROW 51000, 'C8_GROUP_OUTCOME: every member and the exact receipt must post before the group.', 1;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN [ProducerIntentGroups] p ON p.[TenantId]=i.[TenantId] AND p.[Id]=i.[SupersedesProducerIntentGroupId]
   WHERE i.[GroupKind]<>N'Original' AND (p.[Id] IS NULL OR p.[Status]<>N'Posted'
     OR i.[Version]<>p.[Version]+1 OR i.[RootProducerIntentGroupId]<>p.[RootProducerIntentGroupId]
     OR (i.[GroupKind]=N'Correction' AND i.[CorrectsProducerIntentGroupId]<>p.[Id])
     OR (i.[GroupKind]=N'Reversal' AND i.[ReversesProducerIntentGroupId]<>p.[Id])))
  THROW 51000, 'C8_GROUP_LINEAGE: successor requires the exact posted predecessor version and root.', 1;
END;

ALTER TRIGGER [TR_AccountingEventProducerReceipts_C7Immutable] ON [AccountingEventProducerReceipts] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51000, 'C7_RECEIPT_IMMUTABLE: producer receipt evidence is append-only.', 1;
  IF EXISTS(SELECT 1 FROM inserted r LEFT JOIN [AccountingEvents] e ON e.[TenantId]=r.[TenantId] AND e.[Id]=r.[AccountingEventId]
   WHERE e.[Id] IS NULL OR e.[ProducerDecisionStatus]<>N'Approved' OR e.[Status] NOT IN (N'Pending',N'Posted')
    OR e.[ProducerParticipantIdentity]<>r.[ParticipantCode] OR e.[RequestFingerprint]<>r.[RequestFingerprint]
    OR r.[OwnerEntityId]='00000000-0000-0000-0000-000000000000'
    OR r.[ParticipantCode] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[ParticipantCode]))) COLLATE Latin1_General_100_BIN2
    OR LEFT(r.[ParticipantCode],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
    OR r.[ParticipantCode] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
    OR r.[OwnerEntityType] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[OwnerEntityType]))) COLLATE Latin1_General_100_BIN2
    OR LEFT(r.[OwnerEntityType],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
    OR r.[OwnerEntityType] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'
    OR r.[OwnerAction] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(r.[OwnerAction]))) COLLATE Latin1_General_100_BIN2
    OR LEFT(r.[OwnerAction],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'
     OR r.[OwnerAction] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%')
   THROW 51000, 'C7_RECEIPT_AUTHORITY: receipt must match one approved producer event in execution.', 1;
  DECLARE @ownerTenant uniqueidentifier, @ownerParticipant nvarchar(100), @ownerEffect char(64), @ownerResource nvarchar(255), @ownerResult int;
  DECLARE owner_effects CURSOR LOCAL FAST_FORWARD FOR
   SELECT DISTINCT [TenantId],[ParticipantCode],[EffectFingerprint] FROM inserted ORDER BY [TenantId],[ParticipantCode],[EffectFingerprint];
  OPEN owner_effects; FETCH NEXT FROM owner_effects INTO @ownerTenant,@ownerParticipant,@ownerEffect;
  WHILE @@FETCH_STATUS=0 BEGIN
   SET @ownerResource=CONCAT(N'FIN:C7C8:',CONVERT(nvarchar(36),@ownerTenant),N'|',@ownerParticipant,N'|',@ownerEffect);
   EXEC @ownerResult=sp_getapplock @Resource=@ownerResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
   IF @ownerResult<0 BEGIN CLOSE owner_effects; DEALLOCATE owner_effects; THROW 51000, 'C7_RECEIPT_LOCK_FAILED: owner-effect identity could not be serialized.', 1; END;
   FETCH NEXT FROM owner_effects INTO @ownerTenant,@ownerParticipant,@ownerEffect;
  END; CLOSE owner_effects; DEALLOCATE owner_effects;
  IF EXISTS(SELECT 1 FROM inserted i JOIN [ProducerIntentGroupReceipts] r ON r.[TenantId]=i.[TenantId]
   AND r.[ParticipantCode]=i.[ParticipantCode] AND r.[EffectFingerprint]=i.[EffectFingerprint])
  THROW 51000, 'C7_RECEIPT_REUSED: owner-effect evidence already belongs to a producer group.', 1;
END;

ALTER TRIGGER [TR_AccountingEvents_C7ProducerDecision] ON [AccountingEvents] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id]=i.[Id] WHERE d.[Id] IS NULL AND i.[ProducerDecisionStatus] NOT IN (N'NotRequired',N'Pending'))
  THROW 51000, 'C7_INSERT_STATE: producer decisions cannot be fabricated during prepare.', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.[ProducerDecisionStatus]<>N'NotRequired' AND (i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[ProducerParticipantIdentity]))) COLLATE Latin1_General_100_BIN2 OR DATALENGTH(i.[ProducerParticipantIdentity])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[ProducerParticipantIdentity])))) OR LEFT(i.[ProducerParticipantIdentity],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]' OR i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'))
  THROW 51000, 'C7_PARTICIPANT_IDENTITY: canonical stable participant identity is required.', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.[ProducerDecisionStatus] IN (N'Approved',N'Rejected') AND (i.[ProducerDecidedByUserId]=i.[PreparedByUserId] OR LEN(LTRIM(RTRIM(i.[ProducerDecisionReason])))=0))
  THROW 51000, 'C7_MAKER_CHECKER: a distinct checker and governed reason are required.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE ISNULL(d.[ProducerParticipantIdentity],N'')<>ISNULL(i.[ProducerParticipantIdentity],N'') OR ISNULL(d.[ProducerIntentSnapshotHash],N'')<>ISNULL(i.[ProducerIntentSnapshotHash],N'') OR ISNULL(d.[ProducerIntentSnapshotJson],N'')<>ISNULL(i.[ProducerIntentSnapshotJson],N'') OR (d.[ProducerDecisionStatus]<>i.[ProducerDecisionStatus] AND EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers] m WHERE m.[TenantId]=i.[TenantId] AND m.[AccountingEventId]=i.[Id])) OR (d.[ProducerDecisionStatus]=N'Pending' AND i.[ProducerDecisionStatus] NOT IN (N'Pending',N'Approved',N'Rejected')) OR (d.[ProducerDecisionStatus] IN (N'NotRequired',N'Approved',N'Rejected') AND d.[ProducerDecisionStatus]<>i.[ProducerDecisionStatus]) OR (d.[ProducerDecidedByUserId] IS NOT NULL AND (i.[ProducerDecidedByUserId] IS NULL OR d.[ProducerDecidedByUserId]<>i.[ProducerDecidedByUserId] OR d.[ProducerDecidedAtUtc]<>i.[ProducerDecidedAtUtc] OR d.[ProducerDecisionReason]<>i.[ProducerDecisionReason])))
  THROW 51000, 'C7_DECISION_IMMUTABLE: participant and maker-checker evidence cannot be rewritten.', 1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE d.[ProducerDecisionStatus]=N'Pending' AND i.[ProducerDecisionStatus] IN (N'Approved',N'Rejected') AND (d.[Status]<>N'PendingApproval' OR i.[Status]<>N'PendingApproval'))
  THROW 51000, 'C7_DECISION_ONLY: approval cannot execute effects in the same statement.', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE (i.[ProducerDecisionStatus]=N'Pending' AND i.[Status]<>N'PendingApproval'
   AND NOT EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers] m JOIN [ProducerIntentGroups] g ON g.[TenantId]=m.[TenantId] AND g.[Id]=m.[ProducerIntentGroupId] JOIN [ProducerIntentGroupReceipts] r ON r.[TenantId]=g.[TenantId] AND r.[ProducerIntentGroupId]=g.[Id] WHERE m.[TenantId]=i.[TenantId] AND m.[AccountingEventId]=i.[Id] AND g.[Status] IN (N'Approved',N'Failed')))
   OR (i.[ProducerDecisionStatus]=N'Rejected' AND i.[Status]<>N'PendingApproval') OR (i.[ProducerDecisionStatus]=N'Approved' AND i.[Status] NOT IN (N'PendingApproval',N'Pending',N'Posted',N'Failed')))
  THROW 51000, 'C7_EXECUTION_GATE: only individual approval or complete approved group authority may enter C6.', 1;
END;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF EXISTS(SELECT 1 FROM [ProducerIntentGroups]) OR EXISTS(SELECT 1 FROM [ProducerIntentGroupMembers])
 OR EXISTS(SELECT 1 FROM [ProducerIntentGroupReceipts]) OR EXISTS(SELECT 1 FROM [ProducerIntentGroupAttempts])
 THROW 51000, 'C8_DOWN_REFUSED: producer group evidence exists.', 1;
DROP TRIGGER IF EXISTS [TR_ProducerIntentGroups_C8Authority];
DROP TRIGGER IF EXISTS [TR_ProducerIntentGroupMembers_C8Immutable];
DROP TRIGGER IF EXISTS [TR_ProducerIntentGroupReceipts_C8Immutable];
DROP TRIGGER IF EXISTS [TR_ProducerIntentGroupAttempts_C8Immutable];
DROP TABLE [ProducerIntentGroupAttempts]; DROP TABLE [ProducerIntentGroupReceipts]; DROP TABLE [ProducerIntentGroupMembers]; DROP TABLE [ProducerIntentGroups];
ALTER TRIGGER [TR_AccountingEventProducerReceipts_C7Immutable] ON [AccountingEventProducerReceipts] AFTER INSERT, UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51000, 'C7_RECEIPT_IMMUTABLE: producer receipt evidence is append-only.', 1;
 IF EXISTS(SELECT 1 FROM inserted r LEFT JOIN [AccountingEvents] e ON e.[TenantId]=r.[TenantId] AND e.[Id]=r.[AccountingEventId]
  WHERE e.[Id] IS NULL OR e.[ProducerDecisionStatus]<>N'Approved' OR e.[Status] NOT IN (N'Pending',N'Posted')
   OR e.[ProducerParticipantIdentity]<>r.[ParticipantCode] OR e.[RequestFingerprint]<>r.[RequestFingerprint]
   OR r.[OwnerEntityId]='00000000-0000-0000-0000-000000000000'
   OR LEN(LTRIM(RTRIM(r.[OwnerEntityType])))=0 OR LEN(LTRIM(RTRIM(r.[OwnerAction])))=0)
  THROW 51000, 'C7_RECEIPT_AUTHORITY: receipt must match one approved producer event in execution.', 1;
END;
ALTER TRIGGER [TR_AccountingEvents_C7ProducerDecision] ON [AccountingEvents] AFTER INSERT, UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id]=i.[Id] WHERE d.[Id] IS NULL AND i.[ProducerDecisionStatus] NOT IN (N'NotRequired',N'Pending')) THROW 51000, 'C7_INSERT_STATE: producer decisions cannot be fabricated during prepare.', 1;
 IF EXISTS (SELECT 1 FROM inserted i WHERE i.[ProducerDecisionStatus]<>N'NotRequired' AND (i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2<>UPPER(LTRIM(RTRIM(i.[ProducerParticipantIdentity]))) COLLATE Latin1_General_100_BIN2 OR DATALENGTH(i.[ProducerParticipantIdentity])<>DATALENGTH(UPPER(LTRIM(RTRIM(i.[ProducerParticipantIdentity])))) OR LEFT(i.[ProducerParticipantIdentity],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]' OR i.[ProducerParticipantIdentity] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%')) THROW 51000, 'C7_PARTICIPANT_IDENTITY: canonical stable participant identity is required.', 1;
 IF EXISTS (SELECT 1 FROM inserted i WHERE i.[ProducerDecisionStatus] IN (N'Approved',N'Rejected') AND (i.[ProducerDecidedByUserId]=i.[PreparedByUserId] OR LEN(LTRIM(RTRIM(i.[ProducerDecisionReason])))=0)) THROW 51000, 'C7_MAKER_CHECKER: a distinct checker and governed reason are required.', 1;
 IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE ISNULL(d.[ProducerParticipantIdentity],N'')<>ISNULL(i.[ProducerParticipantIdentity],N'') OR ISNULL(d.[ProducerIntentSnapshotHash],N'')<>ISNULL(i.[ProducerIntentSnapshotHash],N'') OR ISNULL(d.[ProducerIntentSnapshotJson],N'')<>ISNULL(i.[ProducerIntentSnapshotJson],N'') OR (d.[ProducerDecisionStatus]=N'Pending' AND i.[ProducerDecisionStatus] NOT IN (N'Pending',N'Approved',N'Rejected')) OR (d.[ProducerDecisionStatus] IN (N'NotRequired',N'Approved',N'Rejected') AND d.[ProducerDecisionStatus]<>i.[ProducerDecisionStatus]) OR (d.[ProducerDecidedByUserId] IS NOT NULL AND (i.[ProducerDecidedByUserId] IS NULL OR d.[ProducerDecidedByUserId]<>i.[ProducerDecidedByUserId] OR d.[ProducerDecidedAtUtc]<>i.[ProducerDecidedAtUtc] OR d.[ProducerDecisionReason]<>i.[ProducerDecisionReason]))) THROW 51000, 'C7_DECISION_IMMUTABLE: participant and maker-checker decision evidence cannot be rewritten.', 1;
 IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE d.[ProducerDecisionStatus]=N'Pending' AND i.[ProducerDecisionStatus] IN (N'Approved',N'Rejected') AND (d.[Status]<>N'PendingApproval' OR i.[Status]<>N'PendingApproval')) THROW 51000, 'C7_DECISION_ONLY: approval or rejection cannot execute C6 effects in the same statement.', 1;
 IF EXISTS (SELECT 1 FROM inserted i WHERE (i.[ProducerDecisionStatus]=N'Pending' AND i.[Status]<>N'PendingApproval') OR (i.[ProducerDecisionStatus]=N'Rejected' AND i.[Status]<>N'PendingApproval') OR (i.[ProducerDecisionStatus]=N'Approved' AND i.[Status] NOT IN (N'PendingApproval',N'Pending',N'Posted',N'Failed'))) THROW 51000, 'C7_EXECUTION_GATE: only an approved producer intent may enter C6 execution.', 1;
END;");
    }
}
