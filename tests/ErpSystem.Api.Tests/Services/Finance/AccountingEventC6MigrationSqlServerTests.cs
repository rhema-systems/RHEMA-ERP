using System.Text.RegularExpressions;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Api.Services.Finance.GL;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingEventC6MigrationSqlServerTests
{
    [SqlServerFact]
    public async Task C7C8OwnerEffectLock_SerializesTwoGenuinelyOpenTransactions()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.CreatePredecessorAsync(); await database.ApplyAsync(up: true);
        await database.ApplyC7Async(up: true); await database.ApplyC8Async(up: true);
        var tenant = Guid.NewGuid(); var fingerprint = new string('A', 64);
        await using var first = database.Context(); await using var second = database.Context();
        await using var firstTransaction = await first.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await FinanceProducerOwnerEffectAuthority.AcquireAsync(first, tenant, "INVENTORY.DISPOSAL.V1", fingerprint, CancellationToken.None);
        await using var secondTransaction = await second.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var waiting = FinanceProducerOwnerEffectAuthority.AcquireAsync(second, tenant, "INVENTORY.DISPOSAL.V1", fingerprint, CancellationToken.None);
        await Task.Delay(200);
        waiting.IsCompleted.Should().BeFalse("the shared C7/C8 identity lock must serialize both open owner transactions");
        await firstTransaction.CommitAsync();
        await waiting;
        await secondTransaction.RollbackAsync();

        var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        await database.ExecuteAsync($"INSERT Tenants(Id) VALUES('{tenant}');");
        async Task<(Guid Group, Guid Individual)> SeedRaceAsync(string suffix, char effect)
        {
            var group = Guid.NewGuid(); var member1 = Guid.NewGuid(); var member2 = Guid.NewGuid(); var individual = Guid.NewGuid();
            await database.ExecuteAsync(ProducerEventInsert(tenant, member1, maker, $"RACE-{suffix}-1")
                + ProducerEventInsert(tenant, member2, maker, $"RACE-{suffix}-2")
                + ProducerEventInsert(tenant, individual, maker, $"RACE-C7-{suffix}") + $@"
INSERT ProducerIntentGroups(Id,IdempotencyKey,GroupKind,Version,RootProducerIntentGroupId,Status,MemberCount,
 ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,ExpectedOwnerEffectFingerprint,RequestSnapshotJson,
 RequestSnapshotHash,GroupFingerprint,PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
VALUES('{group}',N'RACE:GROUP:{suffix}',N'Original',1,'{group}',N'PendingApproval',2,N'INVENTORY.DISPOSAL.V1',
 N'INVENTORYDISPOSAL','{Guid.NewGuid()}',N'DISPOSE',REPLICATE('{effect}',64),N'{{}}',REPLICATE('D',64),REPLICATE('{effect}',64),
 '{maker}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');
INSERT ProducerIntentGroupMembers(Id,ProducerIntentGroupId,AccountingEventId,MemberOrder,MemberFingerprint,CreatedAt,IsDeleted,TenantId)
VALUES(NEWID(),'{group}','{member1}',1,REPLICATE('A',64),SYSUTCDATETIME(),0,'{tenant}'),
      (NEWID(),'{group}','{member2}',2,REPLICATE('A',64),SYSUTCDATETIME(),0,'{tenant}');
UPDATE ProducerIntentGroups SET Status=N'Approved',DecidedByUserId='{checker}',DecidedAtUtc=SYSUTCDATETIME(),DecisionReason=N'complete review' WHERE Id='{group}';
UPDATE AccountingEvents SET ProducerDecisionStatus=N'Approved',ProducerDecidedByUserId='{checker}',ProducerDecidedAtUtc=SYSUTCDATETIME(),ProducerDecisionReason=N'individual review' WHERE Id='{individual}';");
            return (group, individual);
        }

        async Task RunRaceAsync(bool groupWins, string suffix, char effect)
        {
            var seeded = await SeedRaceAsync(suffix, effect);
            await using var groupContext = database.Context(); await using var eventContext = database.Context();
            await using var groupTx = await groupContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await using var eventTx = await eventContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var winnerContext = groupWins ? groupContext : eventContext;
            await FinanceProducerOwnerEffectAuthority.AcquireAsync(winnerContext, tenant, "INVENTORY.DISPOSAL.V1", new string(effect, 64), CancellationToken.None);
            var groupInsert = $@"INSERT ProducerIntentGroupReceipts(Id,ProducerIntentGroupId,ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,
 EffectFingerprint,GroupFingerprint,RecordedAtUtc,RecordedByUserId,CreatedAt,IsDeleted,TenantId)
SELECT NEWID(),Id,ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,ExpectedOwnerEffectFingerprint,GroupFingerprint,SYSUTCDATETIME(),'{checker}',SYSUTCDATETIME(),0,TenantId
FROM ProducerIntentGroups WHERE Id='{seeded.Group}';";
            var eventInsert = $@"INSERT AccountingEventProducerReceipts(Id,AccountingEventId,ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,
 EffectFingerprint,RequestFingerprint,RecordedAtUtc,RecordedByUserId,CreatedAt,IsDeleted,TenantId)
VALUES(NEWID(),'{seeded.Individual}',N'INVENTORY.DISPOSAL.V1',N'INVENTORYDISPOSAL','{Guid.NewGuid()}',N'DISPOSE',REPLICATE('{effect}',64),REPLICATE('A',64),SYSUTCDATETIME(),'{checker}',SYSUTCDATETIME(),0,'{tenant}');";
            var winner = groupWins
                ? groupContext.Database.ExecuteSqlRawAsync(groupInsert)
                : eventContext.Database.ExecuteSqlRawAsync(eventInsert);
            await winner;
            var loser = groupWins
                ? eventContext.Database.ExecuteSqlRawAsync(eventInsert)
                : groupContext.Database.ExecuteSqlRawAsync(groupInsert);
            await Task.Delay(200);
            loser.IsCompleted.Should().BeFalse("the reciprocal trigger must wait on the winner's open transaction");
            if (groupWins) await groupTx.CommitAsync(); else await eventTx.CommitAsync();
            await FluentActions.Awaiting(() => loser).Should().ThrowAsync<SqlException>();
            if (groupWins) await eventTx.RollbackAsync(); else await groupTx.RollbackAsync();
        }

        await RunRaceAsync(groupWins: true, "GROUP-WINS", 'B');
        await RunRaceAsync(groupWins: false, "EVENT-WINS", 'C');
    }

    [SqlServerFact]
    public async Task C8FailureWorkflowAndReciprocalC7C8ReceiptAuthority_AreExecutable()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync(); await db.ApplyAsync(up: true); await db.ApplyC7Async(up: true); await db.ApplyC8Async(up: true);
        var tenant = Guid.NewGuid(); var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        await db.ExecuteAsync($"INSERT Tenants(Id) VALUES('{tenant}');");

        string GroupInsert(string suffix, string key, string participant, string entity, string action)
        {
            var id = Guid.NewGuid();
            return $@"
INSERT ProducerIntentGroups(Id,IdempotencyKey,GroupKind,Version,RootProducerIntentGroupId,Status,MemberCount,
 ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,ExpectedOwnerEffectFingerprint,RequestSnapshotJson,
 RequestSnapshotHash,GroupFingerprint,PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
VALUES('{id}',N'{key}',N'Original',1,'{id}',N'PendingApproval',2,N'{participant}',N'{entity}',
 '{Guid.NewGuid()}',N'{action}',REPLICATE('9',64),N'{{""case"":""{suffix}""}}',REPLICATE('D',64),REPLICATE('E',64),
 '{maker}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');";
        }
        foreach (var invalidSql in new[]
        {
            GroupInsert("TRAILING", "BAD-TRAILING ", "INVENTORY.DISPOSAL.V1", "INVENTORYDISPOSAL", "DISPOSE"),
            GroupInsert("PUNCTUATION", "BAD!KEY", "INVENTORY.DISPOSAL.V1", "INVENTORYDISPOSAL", "DISPOSE"),
            GroupInsert("LEADING", ":BAD", "INVENTORY.DISPOSAL.V1", "INVENTORYDISPOSAL", "DISPOSE"),
            GroupInsert("NONASCII", "BÁD", "INVENTORY.DISPOSAL.V1", "INVENTORYDISPOSAL", "DISPOSE"),
            GroupInsert("PSEUDO", "BAD-PSEUDO", "ALL_ACTIVE_BOOKS", "INVENTORYDISPOSAL", "DISPOSE"),
            GroupInsert("PARTICIPANT-TRAILING", "BAD-PARTICIPANT-TRAILING", "INVENTORY.DISPOSAL.V1 ", "INVENTORYDISPOSAL", "DISPOSE"),
            GroupInsert("PARTICIPANT-NONASCII", "BAD-PARTICIPANT-NONASCII", "INVENTÓRY", "INVENTORYDISPOSAL", "DISPOSE"),
            GroupInsert("ENTITY-LEADING", "BAD-ENTITY", "INVENTORY.DISPOSAL.V1", "1INVENTORY", "DISPOSE"),
            GroupInsert("OWNER-PUNCT", "BAD-OWNER", "INVENTORY.DISPOSAL.V1", "INVENTORYDISPOSAL", "DISPOSE:NOW"),
            GroupInsert("OWNER-PSEUDO", "BAD-OWNER-PSEUDO", "INVENTORY.DISPOSAL.V1", "INVENTORYDISPOSAL", "ALL")
        })
            await FluentActions.Awaiting(() => db.ExecuteAsync(invalidSql)).Should().ThrowAsync<SqlException>();

        async Task<(Guid GroupId, Guid IndividualEventId)> SeedAsync(string suffix, char effect)
        {
            var group = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid(); var individual = Guid.NewGuid();
            await db.ExecuteAsync(ProducerEventInsert(tenant, first, maker, $"C8-{suffix}-1")
                + ProducerEventInsert(tenant, second, maker, $"C8-{suffix}-2")
                + ProducerEventInsert(tenant, individual, maker, $"C7-{suffix}") + $@"
INSERT ProducerIntentGroups(Id,IdempotencyKey,GroupKind,Version,RootProducerIntentGroupId,Status,MemberCount,
 ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,ExpectedOwnerEffectFingerprint,RequestSnapshotJson,
 RequestSnapshotHash,GroupFingerprint,PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
VALUES('{group}',N'GROUP:{suffix}',N'Original',1,'{group}',N'PendingApproval',2,N'INVENTORY.DISPOSAL.V1',
 N'INVENTORYDISPOSAL','{Guid.NewGuid()}',N'DISPOSE',REPLICATE('{effect}',64),N'{{}}',REPLICATE('D',64),REPLICATE('{effect}',64),
 '{maker}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');
INSERT ProducerIntentGroupMembers(Id,ProducerIntentGroupId,AccountingEventId,MemberOrder,MemberFingerprint,CreatedAt,IsDeleted,TenantId)
VALUES(NEWID(),'{group}','{first}',1,REPLICATE('A',64),SYSUTCDATETIME(),0,'{tenant}'),
      (NEWID(),'{group}','{second}',2,REPLICATE('A',64),SYSUTCDATETIME(),0,'{tenant}');
UPDATE ProducerIntentGroups SET Status=N'Approved',DecidedByUserId='{checker}',DecidedAtUtc=SYSUTCDATETIME(),DecisionReason=N'complete review' WHERE Id='{group}';
UPDATE AccountingEvents SET ProducerDecisionStatus=N'Approved',ProducerDecidedByUserId='{checker}',ProducerDecidedAtUtc=SYSUTCDATETIME(),ProducerDecisionReason=N'individual review' WHERE Id='{individual}';");
            return (group, individual);
        }

        var failure = await SeedAsync("FAILURE", 'F');
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE ProducerIntentGroups SET Status=N'Failed',CompletedAtUtc=SYSUTCDATETIME(),FailureMessage=N'member two failed' WHERE Id='{failure.GroupId}';"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C8_GROUP_ATTEMPT_REQUIRED*");
        await db.ExecuteAsync($@"INSERT ProducerIntentGroupAttempts(Id,ProducerIntentGroupId,AttemptNumber,GroupFingerprint,Status,StartedAtUtc,CompletedAtUtc,
 FailedMemberOrder,FailedAccountingEventId,FailureMessage,CreatedAt,IsDeleted,TenantId)
SELECT NEWID(),g.Id,1,g.GroupFingerprint,N'Failed',SYSUTCDATETIME(),SYSUTCDATETIME(),2,m.AccountingEventId,N'member two failed',SYSUTCDATETIME(),0,g.TenantId
FROM ProducerIntentGroups g JOIN ProducerIntentGroupMembers m ON m.TenantId=g.TenantId AND m.ProducerIntentGroupId=g.Id AND m.MemberOrder=2
WHERE g.Id='{failure.GroupId}';");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM ProducerIntentGroups g JOIN ProducerIntentGroupAttempts a ON a.TenantId=g.TenantId AND a.ProducerIntentGroupId=g.Id WHERE g.Id='{failure.GroupId}' AND g.Status=N'Failed' AND a.Status=N'Failed' AND a.FailureMessage=g.FailureMessage"))
            .Should().Be(1);
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE ProducerIntentGroups SET Status=N'Posted',CompletedAtUtc=DATEADD(second,1,CompletedAtUtc),FailureMessage=NULL WHERE Id='{failure.GroupId}';"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C8_GROUP_ATTEMPT_REQUIRED*");
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE ProducerIntentGroups SET FailureMessage=N'rewritten' WHERE Id='{failure.GroupId}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C8_GROUP_OUTCOME_IMMUTABLE*");
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE ProducerIntentGroups SET Status=N'Approved',CompletedAtUtc=NULL,FailureMessage=NULL WHERE Id='{failure.GroupId}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C8_GROUP_WORKFLOW*");

        var groupLevel = await SeedAsync("GROUP-LEVEL-FAILURE", 'A');
        await FluentActions.Awaiting(() => db.ExecuteAsync($@"INSERT ProducerIntentGroupAttempts(Id,ProducerIntentGroupId,AttemptNumber,GroupFingerprint,Status,StartedAtUtc,CompletedAtUtc,
 FailedMemberOrder,FailedAccountingEventId,FailureMessage,CreatedAt,IsDeleted,TenantId)
SELECT NEWID(),g.Id,1,g.GroupFingerprint,N'Failed',SYSUTCDATETIME(),SYSUTCDATETIME(),1,NULL,N'invalid half-bound failure',SYSUTCDATETIME(),0,g.TenantId
FROM ProducerIntentGroups g WHERE g.Id='{groupLevel.GroupId}';"))
            .Should().ThrowAsync<SqlException>();
        await db.ExecuteAsync($@"INSERT ProducerIntentGroupAttempts(Id,ProducerIntentGroupId,AttemptNumber,GroupFingerprint,Status,StartedAtUtc,CompletedAtUtc,
 FailedMemberOrder,FailedAccountingEventId,FailureMessage,CreatedAt,IsDeleted,TenantId)
SELECT NEWID(),g.Id,1,g.GroupFingerprint,N'Failed',SYSUTCDATETIME(),SYSUTCDATETIME(),NULL,NULL,N'owner staging failed before member execution',SYSUTCDATETIME(),0,g.TenantId
FROM ProducerIntentGroups g WHERE g.Id='{groupLevel.GroupId}';");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM ProducerIntentGroups g JOIN ProducerIntentGroupAttempts a ON a.TenantId=g.TenantId AND a.ProducerIntentGroupId=g.Id WHERE g.Id='{groupLevel.GroupId}' AND g.Status=N'Failed' AND a.Status=N'Failed' AND a.FailedMemberOrder IS NULL AND a.FailedAccountingEventId IS NULL"))
            .Should().Be(1);

        var individualFirst = await SeedAsync("INDIVIDUAL-FIRST", 'B');
        await db.ExecuteAsync($@"INSERT AccountingEventProducerReceipts(Id,AccountingEventId,ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,
 EffectFingerprint,RequestFingerprint,RecordedAtUtc,RecordedByUserId,CreatedAt,IsDeleted,TenantId)
VALUES(NEWID(),'{individualFirst.IndividualEventId}',N'INVENTORY.DISPOSAL.V1',N'INVENTORYDISPOSAL','{Guid.NewGuid()}',N'DISPOSE',REPLICATE('B',64),REPLICATE('A',64),SYSUTCDATETIME(),'{checker}',SYSUTCDATETIME(),0,'{tenant}');");
        await FluentActions.Awaiting(() => db.ExecuteAsync($@"INSERT ProducerIntentGroupReceipts(Id,ProducerIntentGroupId,ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,
 EffectFingerprint,GroupFingerprint,RecordedAtUtc,RecordedByUserId,CreatedAt,IsDeleted,TenantId)
SELECT NEWID(),Id,ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,ExpectedOwnerEffectFingerprint,GroupFingerprint,SYSUTCDATETIME(),'{checker}',SYSUTCDATETIME(),0,TenantId
FROM ProducerIntentGroups WHERE Id='{individualFirst.GroupId}';"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C8_RECEIPT_REUSED*");

        var groupFirst = await SeedAsync("GROUP-FIRST", 'C');
        await db.ExecuteAsync($@"INSERT ProducerIntentGroupReceipts(Id,ProducerIntentGroupId,ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,
 EffectFingerprint,GroupFingerprint,RecordedAtUtc,RecordedByUserId,CreatedAt,IsDeleted,TenantId)
SELECT NEWID(),Id,ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,ExpectedOwnerEffectFingerprint,GroupFingerprint,SYSUTCDATETIME(),'{checker}',SYSUTCDATETIME(),0,TenantId
FROM ProducerIntentGroups WHERE Id='{groupFirst.GroupId}';");
        await FluentActions.Awaiting(() => db.ExecuteAsync($@"INSERT AccountingEventProducerReceipts(Id,AccountingEventId,ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,
 EffectFingerprint,RequestFingerprint,RecordedAtUtc,RecordedByUserId,CreatedAt,IsDeleted,TenantId)
VALUES(NEWID(),'{groupFirst.IndividualEventId}',N'INVENTORY.DISPOSAL.V1',N'INVENTORYDISPOSAL','{Guid.NewGuid()}',N'DISPOSE',REPLICATE('C',64),REPLICATE('A',64),SYSUTCDATETIME(),'{checker}',SYSUTCDATETIME(),0,'{tenant}');"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C7_RECEIPT_REUSED*");
    }

    [SqlServerFact]
    public async Task C8PreflightEmptyDownAndEvidenceRefusal_AreExecutableAndRestoreC7()
    {
        await using var preflight = await DisposableDatabase.CreateAsync();
        await preflight.CreatePredecessorAsync();
        await preflight.ApplyAsync(up: true);
        await FluentActions.Awaiting(() => preflight.ApplyC8Async(up: true))
            .Should().ThrowAsync<SqlException>().WithMessage("*C8_PREFLIGHT*");
        (await preflight.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'ProducerIntentGroup%'")).Should().Be(0);

        await using var empty = await DisposableDatabase.CreateAsync();
        await empty.CreatePredecessorAsync(); await empty.ApplyAsync(up: true); await empty.ApplyC7Async(up: true);
        await empty.ApplyC8Async(up: true);
        (await empty.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'ProducerIntentGroup%'")).Should().Be(4);
        await empty.ApplyC8Async(up: false);
        (await empty.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'ProducerIntentGroup%'")).Should().Be(0);
        (await empty.ScalarAsync<int>("SELECT COUNT(*) FROM sys.triggers WHERE name='TR_AccountingEvents_C7ProducerDecision'")).Should().Be(1);

        await using var evidence = await DisposableDatabase.CreateAsync();
        await evidence.CreatePredecessorAsync(); await evidence.ApplyAsync(up: true); await evidence.ApplyC7Async(up: true);
        await evidence.ApplyC8Async(up: true);
        var tenant = Guid.NewGuid(); var maker = Guid.NewGuid(); var group = Guid.NewGuid();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await evidence.ExecuteAsync($"INSERT Tenants(Id) VALUES('{tenant}');");
        await evidence.ExecuteAsync(ProducerEventInsert(tenant, first, maker, "C8-MEMBER-1"));
        await evidence.ExecuteAsync(ProducerEventInsert(tenant, second, maker, "C8-MEMBER-2"));
        await evidence.ExecuteAsync($@"
INSERT ProducerIntentGroups(Id,IdempotencyKey,GroupKind,Version,RootProducerIntentGroupId,Status,MemberCount,
 ParticipantCode,OwnerEntityType,OwnerEntityId,OwnerAction,ExpectedOwnerEffectFingerprint,RequestSnapshotJson,
 RequestSnapshotHash,GroupFingerprint,PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
VALUES('{group}',N'C8-GROUP-1',N'Original',1,'{group}',N'PendingApproval',2,N'INVENTORY.DISPOSAL.V1',
 N'INVENTORYDISPOSAL','{Guid.NewGuid()}',N'DISPOSE',REPLICATE('F',64),N'{{}}',REPLICATE('D',64),REPLICATE('C',64),
 '{maker}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');
INSERT ProducerIntentGroupMembers(Id,ProducerIntentGroupId,AccountingEventId,MemberOrder,MemberFingerprint,CreatedAt,IsDeleted,TenantId)
VALUES(NEWID(),'{group}','{first}',1,REPLICATE('A',64),SYSUTCDATETIME(),0,'{tenant}'),
      (NEWID(),'{group}','{second}',2,REPLICATE('A',64),SYSUTCDATETIME(),0,'{tenant}');");
        await FluentActions.Awaiting(() => evidence.ApplyC8Async(up: false))
            .Should().ThrowAsync<SqlException>().WithMessage("*C8_DOWN_REFUSED*");
    }

    [SqlServerFact]
    public async Task C7PreflightAndEmptyDown_AreExecutableAndScoped()
    {
        await using var preflight = await DisposableDatabase.CreateAsync();
        await preflight.CreatePredecessorAsync();
        await FluentActions.Awaiting(() => preflight.ApplyC7Async(up: true))
            .Should().ThrowAsync<SqlException>().WithMessage("*C7_PREFLIGHT*");
        (await preflight.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('AccountingEvents') AND name LIKE 'Producer%'")).Should().Be(0);

        await using var empty = await DisposableDatabase.CreateAsync();
        await empty.CreatePredecessorAsync();
        await empty.ApplyAsync(up: true);
        await empty.ApplyC7Async(up: true);
        (await empty.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name='AccountingEventProducerReceipts'")).Should().Be(1);
        (await empty.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('AccountingEvents') AND name LIKE 'Producer%'")).Should().Be(7);
        await empty.ApplyC7Async(up: false);
        (await empty.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name='AccountingEventProducerReceipts'")).Should().Be(0);
        (await empty.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('AccountingEvents') AND name LIKE 'Producer%'")).Should().Be(0);
    }

    [SqlServerFact]
    public async Task C7SqlAuthority_AllowsExactDecisionOnly_AndRejectsPreseedCombinedMutationAndLossyDown()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        await db.ApplyC7Async(up: true);
        var tenant = Guid.NewGuid(); var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        await db.ExecuteAsync($"INSERT Tenants(Id) VALUES('{tenant}');");
        var approvedId = Guid.NewGuid();
        await db.ExecuteAsync(ProducerEventInsert(tenant, approvedId, maker, "C7-APPROVE"));
        await db.ExecuteAsync($"UPDATE AccountingEvents SET ProducerDecisionStatus=N'Approved',ProducerDecidedByUserId='{checker}',ProducerDecidedAtUtc=SYSUTCDATETIME(),ProducerDecisionReason=N'reviewed' WHERE Id='{approvedId}';");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE Id='{approvedId}' AND Status='PendingApproval' AND ProducerDecisionStatus='Approved'")).Should().Be(1);
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE AccountingEvents SET ProducerDecisionReason=N'changed' WHERE Id='{approvedId}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C7_DECISION_IMMUTABLE*");
        var rejectedId = Guid.NewGuid();
        await db.ExecuteAsync(ProducerEventInsert(tenant, rejectedId, maker, "C7-REJECT"));
        await db.ExecuteAsync($"UPDATE AccountingEvents SET ProducerDecisionStatus=N'Rejected',ProducerDecidedByUserId='{checker}',ProducerDecidedAtUtc=SYSUTCDATETIME(),ProducerDecisionReason=N'invalid evidence' WHERE Id='{rejectedId}';");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE Id='{rejectedId}' AND Status='PendingApproval' AND ProducerDecisionStatus='Rejected'")).Should().Be(1);

        var concurrentId = Guid.NewGuid();
        await db.ExecuteAsync(ProducerEventInsert(tenant, concurrentId, maker, "C7-CONCURRENT"));
        await using (var winner = await db.OpenAsync())
        await using (var loser = await db.OpenAsync())
        await using (var winnerTx = (SqlTransaction)await winner.BeginTransactionAsync())
        {
            await using var winnerCommand = new SqlCommand($"UPDATE AccountingEvents SET ProducerDecisionStatus=N'Approved',ProducerDecidedByUserId='{checker}',ProducerDecidedAtUtc=SYSUTCDATETIME(),ProducerDecisionReason=N'concurrent approval' WHERE Id='{concurrentId}';", winner, winnerTx);
            await winnerCommand.ExecuteNonQueryAsync();
            await using var loserCommand = new SqlCommand($"UPDATE AccountingEvents SET ProducerDecisionStatus=N'Rejected',ProducerDecidedByUserId='{Guid.NewGuid()}',ProducerDecidedAtUtc=SYSUTCDATETIME(),ProducerDecisionReason=N'conflicting rejection' WHERE Id='{concurrentId}';", loser) { CommandTimeout = 30 };
            var loserAttempt = loserCommand.ExecuteNonQueryAsync();
            await Task.Delay(200);
            loserAttempt.IsCompleted.Should().BeFalse("the row lock must serialize competing producer decisions");
            await winnerTx.CommitAsync();
            await FluentActions.Awaiting(async () => await loserAttempt).Should().ThrowAsync<SqlException>()
                .WithMessage("*C7_DECISION_IMMUTABLE*");
        }
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE Id='{concurrentId}' AND ProducerDecisionStatus='Approved'")).Should().Be(1);

        var combinedId = Guid.NewGuid();
        await db.ExecuteAsync(ProducerEventInsert(tenant, combinedId, maker, "C7-COMBINED"));
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE AccountingEvents SET ProducerDecisionStatus=N'Approved',ProducerDecidedByUserId='{checker}',ProducerDecidedAtUtc=SYSUTCDATETIME(),ProducerDecisionReason=N'reviewed',Status=N'Pending',ReleasedByUserId='{checker}',ReleasedAtUtc=SYSUTCDATETIME(),ReleaseReason=N'reviewed' WHERE Id='{combinedId}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C7_DECISION_ONLY*");

        var preseed = ProducerEventInsert(tenant, Guid.NewGuid(), maker, "C7-PRESEED")
            .Replace("N'Pending',N'INVENTORY.DISPOSAL.V1'", $"N'Approved',N'INVENTORY.DISPOSAL.V1'")
            .Replace("NULL,NULL,NULL", $"'{checker}',SYSUTCDATETIME(),N'fabricated'");
        await FluentActions.Awaiting(() => db.ExecuteAsync(preseed)).Should().ThrowAsync<SqlException>().WithMessage("*C7_INSERT_STATE*");
        await FluentActions.Awaiting(() => db.ApplyC7Async(up: false)).Should().ThrowAsync<SqlException>().WithMessage("*C7_DOWN_REFUSED*");
    }

    [SqlServerFact]
    public async Task UpAndEmptyDownCreateAndRemoveOnlyC6Authority()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN ('AccountingEvents','AccountingEventPostings','AccountingEventAttempts')")).Should().Be(3);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.triggers WHERE name LIKE 'TR_AccountingEvent%_C6%' OR name='TR_AccountingEventAttempts_C6AppendOnly'")).Should().Be(3);
        await db.ApplyAsync(up: false);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'AccountingEvent%'")).Should().Be(0);
    }

    [SqlServerFact]
    public async Task PreflightFailureOccursBeforeUniqueConstraintsOrC6Tables()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ExecuteAsync("CREATE TABLE AccountingEvents(Id uniqueidentifier NOT NULL PRIMARY KEY);");
        await FluentActions.Awaiting(() => db.ApplyAsync(up: true)).Should().ThrowAsync<SqlException>().WithMessage("*C6_SCHEMA_PREFLIGHT*");
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.key_constraints WHERE name IN ('AK_JournalEntries_TenantId_Id','AK_FinancePostingEvents_TenantId_Id')")).Should().Be(0);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN ('AccountingEventPostings','AccountingEventAttempts')")).Should().Be(0);
    }

    [SqlServerFact]
    public async Task FailedEventAndAttemptPersistButBoundedDownRefusesTheirLoss()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        var tenant = Guid.NewGuid(); var eventId = Guid.NewGuid(); var actor = Guid.NewGuid(); var attemptId = Guid.NewGuid();
        await db.ExecuteAsync($"INSERT Tenants(Id) VALUES('{tenant}');" + EventInsert(tenant, eventId, actor, "FAILURE-KEY", "FAILED", status: "Failed") +
            $"INSERT AccountingEventAttempts(Id,AccountingEventId,AttemptNumber,RequestFingerprint,Status,StartedAtUtc,CompletedAtUtc,FailureMessage,CreatedAt,IsDeleted,TenantId) VALUES('{attemptId}','{eventId}',1,REPLICATE('A',64),'Failed',SYSUTCDATETIME(),SYSUTCDATETIME(),N'leaf failed',SYSUTCDATETIME(),0,'{tenant}');");
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM AccountingEvents WHERE Status='Failed'")).Should().Be(1);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM AccountingEventAttempts WHERE Status='Failed'")).Should().Be(1);
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE AccountingEvents SET CompletedAtUtc=DATEADD(second,1,CompletedAtUtc),FailureMessage=N'rewritten' WHERE Id='{eventId}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C6_EVENT_OUTCOME_IMMUTABLE*");
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE AccountingEvents SET RequestFingerprint=REPLICATE('B',64) WHERE Id='{eventId}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C6_EVENT_IMMUTABLE*");
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE AccountingEventAttempts SET FailureMessage=N'rewritten' WHERE Id='{attemptId}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C6_ATTEMPT_IMMUTABLE*");
        await FluentActions.Awaiting(() => db.ExecuteAsync($"DELETE AccountingEventAttempts WHERE Id='{attemptId}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C6_ATTEMPT_IMMUTABLE*");
        var book = Guid.NewGuid(); var evidence = Guid.NewGuid();
        await db.ExecuteAsync($@"INSERT AccountingBooks(Id,TenantId,Code) VALUES('{book}','{tenant}',N'PRIMARY');
INSERT AccountingBookSelectionEvidence(Id,TenantId,OriginatingModuleCode,SourceDocumentType,PostingAction,EffectiveDate,IdempotencyKey,SelectionFingerprint,IsDeleted)
 VALUES('{evidence}','{tenant}',N'FIN',N'JOURNAL',N'FAILED',CONVERT(date,'2026-09-07'),N'FAILURE-SELECTION',REPLICATE('B',64),0);
INSERT AccountingBookSelectionEvidenceBooks(Id,TenantId,AccountingBookSelectionEvidenceId,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,AuthorityFingerprint,IsDeleted)
 VALUES('{Guid.NewGuid()}','{tenant}','{evidence}','{book}',0,N'PRIMARY',REPLICATE('C',64),0);");
        await FluentActions.Awaiting(() => db.ExecuteAsync($@"UPDATE AccountingEvents SET Status=N'Posted',AccountingBookSelectionEvidenceId='{evidence}',
 SelectionFingerprint=REPLICATE('B',64),FailureMessage=NULL WHERE Id='{eventId}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C6_EVENT_STATUS*");
        await db.ExecuteAsync($@"UPDATE AccountingEvents SET Status=N'Pending',AccountingBookSelectionEvidenceId='{evidence}',SelectionFingerprint=REPLICATE('B',64),
 CompletedAtUtc=NULL,FailureMessage=NULL WHERE Id='{eventId}'");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE Id='{eventId}' AND Status='Pending' AND CompletedAtUtc IS NULL AND FailureMessage IS NULL"))
            .Should().Be(1);
        await FluentActions.Awaiting(() => db.ApplyAsync(up: false)).Should().ThrowAsync<SqlException>().WithMessage("*C6_DOWN_BLOCKED*");
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name='AccountingEvents'")).Should().Be(1);
    }

    [SqlServerFact]
    public async Task LaterDatedSuccessorReusesExactPostedPredecessorSelectionEvidence()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        var tenant = Guid.NewGuid(); var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        var book = Guid.NewGuid(); var evidence = Guid.NewGuid(); var source = Guid.NewGuid();
        var original = Guid.NewGuid(); var successor = Guid.NewGuid(); var journal = Guid.NewGuid(); var leaf = Guid.NewGuid();
        var originalPosting = Guid.NewGuid();
        var successorPosting = Guid.NewGuid(); var badJournal = Guid.NewGuid(); var badLeaf = Guid.NewGuid();
        await db.ExecuteAsync($@"
INSERT Tenants(Id) VALUES('{tenant}');
INSERT AccountingBooks(Id,TenantId,Code) VALUES('{book}','{tenant}',N'PRIMARY');
INSERT AccountingBookSelectionEvidence(Id,TenantId,OriginatingModuleCode,SourceDocumentType,PostingAction,EffectiveDate,IdempotencyKey,SelectionFingerprint,IsDeleted)
 VALUES('{evidence}','{tenant}',N'FIN',N'JOURNAL',N'POST',CONVERT(date,'2026-09-07'),N'SELECTION-1',REPLICATE('B',64),0);
INSERT AccountingBookSelectionEvidenceBooks(Id,TenantId,AccountingBookSelectionEvidenceId,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,AuthorityFingerprint,IsDeleted)
 VALUES('{Guid.NewGuid()}','{tenant}','{evidence}','{book}',0,N'PRIMARY',REPLICATE('C',64),0);
INSERT AccountingEvents(Id,OriginatingModuleCode,SourceDocumentType,SourceDocumentId,PostingAction,IdempotencyKey,EventKind,Version,RootAccountingEventId,
 SelectionFingerprint,RequestFingerprint,Status,EventDate,RequestedAtUtc,RequestedByUserId,PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
 VALUES('{original}',N'FIN',N'JOURNAL','{source}',N'POST',N'ORIGINAL-1',N'Original',1,'{original}',REPLICATE('B',64),REPLICATE('A',64),N'PendingApproval',
 CONVERT(date,'2026-09-07'),SYSUTCDATETIME(),'{maker}','{maker}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');
UPDATE AccountingEvents SET AccountingBookSelectionEvidenceId='{evidence}',Status=N'Pending',ReleasedByUserId='{checker}',ReleasedAtUtc=SYSUTCDATETIME(),ReleaseReason=N'release'
 WHERE Id='{original}';
INSERT JournalEntries(Id,TenantId,AccountingBookId,PostingStatus) VALUES('{journal}','{tenant}','{book}',N'Posted');
INSERT FinancePostingEvents(Id,TenantId,AccountingBookId,JournalEntryId,PostingStatus,SourceModule,OriginModuleCode,SourceDocumentType,SourceDocumentId,PostingAction)
 VALUES('{leaf}','{tenant}','{book}','{journal}',N'Posted',N'FIN',N'FIN',N'JOURNAL','{source}',N'POST');
INSERT AccountingEventPostings(Id,AccountingEventId,EventVersion,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,AuthorityFingerprint,Status,
 FinancePostingEventId,JournalEntryId,PostedAtUtc,CreatedAt,IsDeleted,TenantId)
 VALUES('{originalPosting}','{original}',1,'{book}',0,N'PRIMARY',REPLICATE('C',64),N'Posted','{leaf}','{journal}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');
UPDATE AccountingEvents SET Status=N'Posted',CompletedAtUtc=SYSUTCDATETIME() WHERE Id='{original}';");

        var successorMutations = new[]
        {
            (Name: "module", Module: "INV", Document: "JOURNAL", SourceId: source, Action: "POST"),
            (Name: "document type", Module: "FIN", Document: "UNRELATED", SourceId: source, Action: "POST"),
            (Name: "document id", Module: "FIN", Document: "JOURNAL", SourceId: Guid.NewGuid(), Action: "POST"),
            (Name: "posting action", Module: "FIN", Document: "JOURNAL", SourceId: source, Action: "ADJUST")
        };
        foreach (var mutation in successorMutations)
        {
            var rejectedSuccessor = Guid.NewGuid();
            await FluentActions.Awaiting(() => db.ExecuteAsync($@"
INSERT AccountingEvents(Id,OriginatingModuleCode,SourceDocumentType,SourceDocumentId,PostingAction,IdempotencyKey,EventKind,Version,RootAccountingEventId,
 SupersedesAccountingEventId,ReversesAccountingEventId,SelectionFingerprint,RequestFingerprint,Status,EventDate,RequestedAtUtc,RequestedByUserId,
 PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
 VALUES('{rejectedSuccessor}',N'{mutation.Module}',N'{mutation.Document}','{mutation.SourceId}',N'{mutation.Action}',N'REJECT-{rejectedSuccessor:N}',N'Reversal',2,
 '{original}','{original}','{original}',REPLICATE('B',64),REPLICATE('D',64),N'PendingApproval',CONVERT(date,'2026-09-08'),SYSUTCDATETIME(),
 '{maker}','{maker}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');"))
                .Should().ThrowAsync<SqlException>().WithMessage("*C6_EVENT_LINEAGE*", $"a successor cannot mutate its predecessor {mutation.Name}");
            (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE TenantId='{tenant}' AND Version=2"))
                .Should().Be(0, "each rejected successor statement must leave zero C6 mutation");
        }

        await db.ExecuteAsync($@"
INSERT AccountingEvents(Id,OriginatingModuleCode,SourceDocumentType,SourceDocumentId,PostingAction,IdempotencyKey,EventKind,Version,RootAccountingEventId,
 SupersedesAccountingEventId,ReversesAccountingEventId,SelectionFingerprint,RequestFingerprint,Status,EventDate,RequestedAtUtc,RequestedByUserId,
 PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
 VALUES('{successor}',N'FIN',N'JOURNAL','{source}',N'POST',N'REVERSAL-1',N'Reversal',2,'{original}','{original}','{original}',REPLICATE('B',64),REPLICATE('D',64),
 N'PendingApproval',CONVERT(date,'2026-09-08'),SYSUTCDATETIME(),'{maker}','{maker}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');
UPDATE AccountingEvents SET AccountingBookSelectionEvidenceId='{evidence}',Status=N'Pending',ReleasedByUserId='{checker}',ReleasedAtUtc=SYSUTCDATETIME(),ReleaseReason=N'reversal'
 WHERE Id='{successor}';");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE Id='{successor}' AND Status='Pending' AND EventDate=CONVERT(date,'2026-09-08') AND AccountingBookSelectionEvidenceId='{evidence}'"))
            .Should().Be(1);
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE AccountingEvents SET CompletedAtUtc=DATEADD(second,1,CompletedAtUtc) WHERE Id='{original}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C6_EVENT_OUTCOME_IMMUTABLE*");
        await FluentActions.Awaiting(() => db.ExecuteAsync($@"INSERT AccountingEventAttempts(Id,AccountingEventId,AttemptNumber,RequestFingerprint,Status,StartedAtUtc,CreatedAt,IsDeleted,TenantId)
 VALUES('{Guid.NewGuid()}','{successor}',1,REPLICATE('D',64),N'Pending',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}')"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C6_ATTEMPT_FINAL_REQUIRED*");
        await db.ExecuteAsync($@"
INSERT JournalEntries(Id,TenantId,AccountingBookId,PostingStatus) VALUES('{badJournal}','{tenant}','{book}',N'Posted');
INSERT FinancePostingEvents(Id,TenantId,AccountingBookId,JournalEntryId,PostingStatus,SourceModule,OriginModuleCode,SourceDocumentType,SourceDocumentId,PostingAction)
 VALUES('{badLeaf}','{tenant}','{book}','{badJournal}',N'Posted',N'GL',N'FIN',N'FinancePostingEventReversal','{Guid.NewGuid()}',N'Reverse');
INSERT AccountingEventPostings(Id,AccountingEventId,EventVersion,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,AuthorityFingerprint,Status,CreatedAt,IsDeleted,TenantId)
 VALUES('{successorPosting}','{successor}',2,'{book}',0,N'PRIMARY',REPLICATE('C',64),N'Pending',SYSUTCDATETIME(),0,'{tenant}');");
        await FluentActions.Awaiting(() => db.ExecuteAsync($@"UPDATE AccountingEventPostings SET Status=N'Posted',FinancePostingEventId='{badLeaf}',JournalEntryId='{badJournal}',PostedAtUtc=SYSUTCDATETIME()
 WHERE Id='{successorPosting}'" )).Should().ThrowAsync<SqlException>().WithMessage("*C6_POSTING_RESULT*");
        var unrelated = new[]
        {
            (SourceModule: "XX", Origin: "FIN", Document: "FinancePostingEventReversal", SourceId: leaf, Action: "Reverse"),
            (SourceModule: "GL", Origin: "XXX", Document: "FinancePostingEventReversal", SourceId: leaf, Action: "Reverse"),
            (SourceModule: "GL", Origin: "FIN", Document: "UnrelatedReversal", SourceId: leaf, Action: "Reverse"),
            (SourceModule: "GL", Origin: "FIN", Document: "FinancePostingEventReversal", SourceId: leaf, Action: "Unrelated")
        };
        foreach (var evidenceMismatch in unrelated)
        {
            var mismatchJournal = Guid.NewGuid(); var mismatchLeaf = Guid.NewGuid();
            await db.ExecuteAsync($@"INSERT JournalEntries(Id,TenantId,AccountingBookId,PostingStatus) VALUES('{mismatchJournal}','{tenant}','{book}',N'Posted');
INSERT FinancePostingEvents(Id,TenantId,AccountingBookId,JournalEntryId,PostingStatus,SourceModule,OriginModuleCode,SourceDocumentType,SourceDocumentId,PostingAction)
 VALUES('{mismatchLeaf}','{tenant}','{book}','{mismatchJournal}',N'Posted',N'{evidenceMismatch.SourceModule}',N'{evidenceMismatch.Origin}',N'{evidenceMismatch.Document}','{evidenceMismatch.SourceId}',N'{evidenceMismatch.Action}');");
            await FluentActions.Awaiting(() => db.ExecuteAsync($@"UPDATE AccountingEventPostings SET Status=N'Posted',FinancePostingEventId='{mismatchLeaf}',JournalEntryId='{mismatchJournal}',PostedAtUtc=SYSUTCDATETIME()
 WHERE Id='{successorPosting}'")).Should().ThrowAsync<SqlException>().WithMessage("*C6_POSTING_RESULT*");
        }
        await FluentActions.Awaiting(() => db.ExecuteAsync($@"UPDATE AccountingEventPostings SET Status=N'Posted',FinancePostingEventId='{leaf}',JournalEntryId='{journal}',PostedAtUtc=SYSUTCDATETIME()
 WHERE Id='{successorPosting}'" )).Should().ThrowAsync<SqlException>();
        var validJournal = Guid.NewGuid(); var validLeaf = Guid.NewGuid();
        await db.ExecuteAsync($@"INSERT JournalEntries(Id,TenantId,AccountingBookId,PostingStatus) VALUES('{validJournal}','{tenant}','{book}',N'Posted');
INSERT FinancePostingEvents(Id,TenantId,AccountingBookId,JournalEntryId,PostingStatus,SourceModule,OriginModuleCode,SourceDocumentType,SourceDocumentId,PostingAction)
 VALUES('{validLeaf}','{tenant}','{book}','{validJournal}',N'Posted',N'GL',N'FIN',N'FinancePostingEventReversal','{leaf}',N'Reverse');
UPDATE AccountingEventPostings SET Status=N'Posted',FinancePostingEventId='{validLeaf}',JournalEntryId='{validJournal}',PostedAtUtc=SYSUTCDATETIME()
 WHERE Id='{successorPosting}';
UPDATE AccountingEvents SET Status=N'Posted',CompletedAtUtc=SYSUTCDATETIME() WHERE Id='{successor}';");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEventPostings WHERE Id='{successorPosting}' AND Status='Posted' AND FinancePostingEventId='{validLeaf}'"))
            .Should().Be(1);
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE AccountingEventPostings SET AuthorityFingerprint=REPLICATE('E',64) WHERE Id='{originalPosting}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C6_POSTING_IMMUTABLE*");
        await FluentActions.Awaiting(() => db.ExecuteAsync($"UPDATE AccountingEvents SET SelectionFingerprint=REPLICATE('E',64) WHERE Id='{original}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*C6_EVENT_IMMUTABLE*");
    }

    [SqlServerFact]
    public async Task CorrectionAcceptsOnlyExactSelectedBookSiblingsAndRejectsUnrelatedRepresentationReuse()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        var tenant = Guid.NewGuid(); var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        var firstBook = Guid.NewGuid(); var secondBook = Guid.NewGuid(); var unselectedBook = Guid.NewGuid();
        var evidence = Guid.NewGuid(); var source = Guid.NewGuid(); var original = Guid.NewGuid(); var correction = Guid.NewGuid();
        var originalFirstLeaf = Guid.NewGuid(); var originalSecondLeaf = Guid.NewGuid();
        var originalFirstJournal = Guid.NewGuid(); var originalSecondJournal = Guid.NewGuid();
        var correctionFirstPosting = Guid.NewGuid(); var correctionSecondPosting = Guid.NewGuid();
        await db.ExecuteAsync($@"
INSERT Tenants(Id) VALUES('{tenant}');
INSERT AccountingBooks(Id,TenantId,Code) VALUES('{firstBook}','{tenant}',N'PRIMARY'),('{secondBook}','{tenant}',N'STAT'),('{unselectedBook}','{tenant}',N'UNSELECTED');
INSERT AccountingBookSelectionEvidence(Id,TenantId,OriginatingModuleCode,SourceDocumentType,PostingAction,EffectiveDate,IdempotencyKey,SelectionFingerprint,IsDeleted)
 VALUES('{evidence}','{tenant}',N'FIN',N'JOURNAL-ENTRY.V2',N'POST.EXACT_V2',CONVERT(date,'2026-09-07'),N'CORRECTION-SELECTION',REPLICATE('B',64),0);
INSERT AccountingBookSelectionEvidenceBooks(Id,TenantId,AccountingBookSelectionEvidenceId,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,AuthorityFingerprint,IsDeleted)
 VALUES('{Guid.NewGuid()}','{tenant}','{evidence}','{firstBook}',0,N'PRIMARY',REPLICATE('C',64),0),
       ('{Guid.NewGuid()}','{tenant}','{evidence}','{secondBook}',1,N'STAT',REPLICATE('D',64),0);
INSERT AccountingEvents(Id,OriginatingModuleCode,SourceDocumentType,SourceDocumentId,PostingAction,IdempotencyKey,EventKind,Version,RootAccountingEventId,
 AccountingBookSelectionEvidenceId,SelectionFingerprint,RequestFingerprint,Status,EventDate,RequestedAtUtc,RequestedByUserId,PreparedByUserId,PreparedAtUtc,
 ReleasedByUserId,ReleasedAtUtc,ReleaseReason,CreatedAt,IsDeleted,TenantId)
 VALUES('{original}',N'FIN',N'JOURNAL-ENTRY.V2','{source}',N'POST.EXACT_V2',N'CORRECTION-ORIGINAL',N'Original',1,'{original}','{evidence}',REPLICATE('B',64),
 REPLICATE('A',64),N'Pending',CONVERT(date,'2026-09-07'),SYSUTCDATETIME(),'{maker}','{maker}',SYSUTCDATETIME(),'{checker}',SYSUTCDATETIME(),N'release',SYSUTCDATETIME(),0,'{tenant}');
INSERT JournalEntries(Id,TenantId,AccountingBookId,PostingStatus) VALUES('{originalFirstJournal}','{tenant}','{firstBook}',N'Posted'),('{originalSecondJournal}','{tenant}','{secondBook}',N'Posted');
INSERT FinancePostingEvents(Id,TenantId,AccountingBookId,JournalEntryId,PostingStatus,SourceModule,OriginModuleCode,SourceDocumentType,SourceDocumentId,PostingAction)
 VALUES('{originalFirstLeaf}','{tenant}','{firstBook}','{originalFirstJournal}',N'Posted',N'FIN',N'FIN',N'JOURNAL-ENTRY.V2','{source}',N'POST.EXACT_V2'),
       ('{originalSecondLeaf}','{tenant}','{secondBook}','{originalSecondJournal}',N'Posted',N'FIN',N'FIN',N'JOURNAL-ENTRY.V2','{source}',N'POST.EXACT_V2');
INSERT AccountingEventPostings(Id,AccountingEventId,EventVersion,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,AuthorityFingerprint,Status,
 FinancePostingEventId,JournalEntryId,PostedAtUtc,CreatedAt,IsDeleted,TenantId)
 VALUES('{Guid.NewGuid()}','{original}',1,'{firstBook}',0,N'PRIMARY',REPLICATE('C',64),N'Posted','{originalFirstLeaf}','{originalFirstJournal}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}'),
       ('{Guid.NewGuid()}','{original}',1,'{secondBook}',1,N'STAT',REPLICATE('D',64),N'Posted','{originalSecondLeaf}','{originalSecondJournal}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');
UPDATE AccountingEvents SET Status=N'Posted',CompletedAtUtc=SYSUTCDATETIME() WHERE Id='{original}';
INSERT AccountingEvents(Id,OriginatingModuleCode,SourceDocumentType,SourceDocumentId,PostingAction,IdempotencyKey,EventKind,Version,RootAccountingEventId,
 SupersedesAccountingEventId,CorrectsAccountingEventId,AccountingBookSelectionEvidenceId,SelectionFingerprint,RequestFingerprint,Status,EventDate,RequestedAtUtc,
 RequestedByUserId,PreparedByUserId,PreparedAtUtc,ReleasedByUserId,ReleasedAtUtc,ReleaseReason,CreatedAt,IsDeleted,TenantId)
 VALUES('{correction}',N'FIN',N'JOURNAL-ENTRY.V2','{source}',N'POST.EXACT_V2',N'CORRECTION-EXACT',N'Correction',2,'{original}','{original}','{original}',
 '{evidence}',REPLICATE('B',64),REPLICATE('E',64),N'Pending',CONVERT(date,'2026-09-09'),SYSUTCDATETIME(),'{maker}','{maker}',SYSUTCDATETIME(),
 '{checker}',SYSUTCDATETIME(),N'correct',SYSUTCDATETIME(),0,'{tenant}');
INSERT AccountingEventPostings(Id,AccountingEventId,EventVersion,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,AuthorityFingerprint,Status,CreatedAt,IsDeleted,TenantId)
 VALUES('{correctionFirstPosting}','{correction}',2,'{firstBook}',0,N'PRIMARY',REPLICATE('C',64),N'Pending',SYSUTCDATETIME(),0,'{tenant}'),
       ('{correctionSecondPosting}','{correction}',2,'{secondBook}',1,N'STAT',REPLICATE('D',64),N'Pending',SYSUTCDATETIME(),0,'{tenant}');");

        var unrelatedSelectedJournal = Guid.NewGuid(); var unrelatedSelectedLeaf = Guid.NewGuid();
        var unrelatedUnselectedJournal = Guid.NewGuid(); var unrelatedUnselectedLeaf = Guid.NewGuid();
        await db.ExecuteAsync($@"
INSERT JournalEntries(Id,TenantId,AccountingBookId,PostingStatus) VALUES('{unrelatedSelectedJournal}','{tenant}','{firstBook}',N'Posted'),('{unrelatedUnselectedJournal}','{tenant}','{unselectedBook}',N'Posted');
INSERT FinancePostingEvents(Id,TenantId,AccountingBookId,JournalEntryId,PostingStatus,SourceModule,OriginModuleCode,SourceDocumentType,SourceDocumentId,PostingAction)
 VALUES('{unrelatedSelectedLeaf}','{tenant}','{firstBook}','{unrelatedSelectedJournal}',N'Posted',N'GL',N'FIN',N'AccountingEventCorrection','{Guid.NewGuid()}',N'POST.EXACT_V2'),
       ('{unrelatedUnselectedLeaf}','{tenant}','{unselectedBook}','{unrelatedUnselectedJournal}',N'Posted',N'GL',N'FIN',N'AccountingEventCorrection','{correction}',N'POST.EXACT_V2');");
        await FluentActions.Awaiting(() => db.ExecuteAsync($@"UPDATE AccountingEventPostings SET Status=N'Failed',FinancePostingEventId='{unrelatedSelectedLeaf}',
 JournalEntryId='{unrelatedSelectedJournal}',PostedAtUtc=SYSUTCDATETIME(),FailureMessage=N'failed after leaf' WHERE Id='{correctionFirstPosting}'"))
            .Should().ThrowAsync<SqlException>().WithMessage("*CK_AccountingEventPostings_ResultShape*");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEventPostings WHERE Id='{correctionFirstPosting}' AND Status='Pending' AND FinancePostingEventId IS NULL AND JournalEntryId IS NULL AND PostedAtUtc IS NULL AND FailureMessage IS NULL"))
            .Should().Be(1, "a Failed row cannot retain or own leaf result evidence");
        foreach (var unrelated in new[]
                 {
                     (Leaf: unrelatedSelectedLeaf, Journal: unrelatedSelectedJournal),
                     (Leaf: unrelatedUnselectedLeaf, Journal: unrelatedUnselectedJournal)
                 })
        {
            await FluentActions.Awaiting(() => db.ExecuteAsync($@"UPDATE AccountingEventPostings SET Status=N'Posted',FinancePostingEventId='{unrelated.Leaf}',
 JournalEntryId='{unrelated.Journal}',PostedAtUtc=SYSUTCDATETIME() WHERE Id='{correctionFirstPosting}'"))
                .Should().ThrowAsync<SqlException>().WithMessage("*C6_POSTING_RESULT*");
            (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEventPostings WHERE AccountingEventId='{correction}' AND (Status<>'Pending' OR FinancePostingEventId IS NOT NULL OR JournalEntryId IS NOT NULL)"))
                .Should().Be(0, "unrelated direct representations must leave zero C6 result mutation");
            (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE TenantId='{tenant}' AND Status='Pending' AND Id='{correction}'"))
                .Should().Be(1, "the canonical correction aggregate must remain unchanged");
            (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEventAttempts WHERE TenantId='{tenant}'"))
                .Should().Be(0, "rejected direct evidence cannot manufacture C6 attempt history");
        }

        var firstJournal = Guid.NewGuid(); var firstLeaf = Guid.NewGuid();
        var secondJournal = Guid.NewGuid(); var secondLeaf = Guid.NewGuid();
        await db.ExecuteAsync($@"
INSERT JournalEntries(Id,TenantId,AccountingBookId,PostingStatus) VALUES('{firstJournal}','{tenant}','{firstBook}',N'Posted'),('{secondJournal}','{tenant}','{secondBook}',N'Posted');
INSERT FinancePostingEvents(Id,TenantId,AccountingBookId,JournalEntryId,PostingStatus,SourceModule,OriginModuleCode,SourceDocumentType,SourceDocumentId,PostingAction)
 VALUES('{firstLeaf}','{tenant}','{firstBook}','{firstJournal}',N'Posted',N'GL',N'FIN',N'AccountingEventCorrection','{correction}',N'POST.EXACT_V2'),
       ('{secondLeaf}','{tenant}','{secondBook}','{secondJournal}',N'Posted',N'GL',N'FIN',N'AccountingEventCorrection','{correction}',N'POST.EXACT_V2');
UPDATE AccountingEventPostings SET Status=N'Posted',FinancePostingEventId='{firstLeaf}',JournalEntryId='{firstJournal}',PostedAtUtc=SYSUTCDATETIME()
 WHERE Id='{correctionFirstPosting}';");
        await FluentActions.Awaiting(() => db.ExecuteAsync($@"UPDATE AccountingEventPostings SET Status=N'Posted',FinancePostingEventId='{firstLeaf}',
 JournalEntryId='{firstJournal}',PostedAtUtc=SYSUTCDATETIME() WHERE Id='{correctionSecondPosting}'"))
            .Should().ThrowAsync<SqlException>();
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEventPostings WHERE Id='{correctionSecondPosting}' AND Status='Pending' AND FinancePostingEventId IS NULL AND JournalEntryId IS NULL"))
            .Should().Be(1, "leaf reuse denial must roll back without mutating its sibling");
        await db.ExecuteAsync($@"UPDATE AccountingEventPostings SET Status=N'Posted',FinancePostingEventId='{secondLeaf}',JournalEntryId='{secondJournal}',PostedAtUtc=SYSUTCDATETIME()
 WHERE Id='{correctionSecondPosting}';
UPDATE AccountingEvents SET Status=N'Posted',CompletedAtUtc=SYSUTCDATETIME() WHERE Id='{correction}';");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEventPostings WHERE AccountingEventId='{correction}' AND Status='Posted'"))
            .Should().Be(2, "both legitimate exact-book correction siblings are durable");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE Id='{correction}' AND Status='Posted'"))
            .Should().Be(1);
    }

    [SqlServerFact]
    public async Task ConcurrentSameTenantIdempotencyPersistsExactlyOneEvent()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        await db.ExecuteAsync($"INSERT Tenants(Id) VALUES('{tenant}');");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var arrivals = 0;
        async Task<Exception?> InsertAsync(string action)
        {
            await using var connection = await db.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            try
            {
                await using var command = connection.CreateCommand(); command.Transaction = transaction;
                command.CommandText = EventInsert(tenant, Guid.NewGuid(), actor, "RACE-KEY", action, "PendingApproval");
                await command.ExecuteNonQueryAsync(); await transaction.CommitAsync(); return null;
            }
            catch (Exception ex) { try { await transaction.RollbackAsync(); } catch { } return ex; }
        }
        var outcomes = await Task.WhenAll(Task.Run(() => InsertAsync("POST-A")), Task.Run(() => InsertAsync("POST-B")));
        outcomes.Count(item => item is null).Should().Be(1);
        outcomes.Count(item => item is SqlException).Should().Be(1);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM AccountingEvents WHERE TenantId='" + tenant + "' AND IdempotencyKey='RACE-KEY'")).Should().Be(1);
    }

    [SqlServerFact]
    public async Task CanonicalEventSourceRejectsUnsupportedPseudoAndNonAsciiIdentityBeforePersistence()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        await db.ExecuteAsync($"INSERT Tenants(Id) VALUES('{tenant}');");
        var invalid = new[]
        {
            (Key: "MODULE-LOWER", Module: "fin", Document: "JOURNAL", Action: "POST"),
            (Key: "MODULE-UNKNOWN", Module: "UNKNOWN", Document: "JOURNAL", Action: "POST"),
            (Key: "MODULE-PSEUDO", Module: "ALL", Document: "JOURNAL", Action: "POST"),
            (Key: "DOCUMENT-PSEUDO-ALL", Module: "FIN", Document: "ALL", Action: "POST"),
            (Key: "DOCUMENT-PSEUDO", Module: "FIN", Document: "ALL_ACTIVE_BOOKS", Action: "POST"),
            (Key: "DOCUMENT-PSEUDO-CLASSIFIED", Module: "FIN", Document: "ALL_CLASSIFIED_BOOKS", Action: "POST"),
            (Key: "DOCUMENT-PSEUDO-COMPACT", Module: "FIN", Document: "ALLCLASSIFIEDBOOKS", Action: "POST"),
            (Key: "DOCUMENT-DIGIT", Module: "FIN", Document: "1JOURNAL", Action: "POST"),
            (Key: "DOCUMENT-PUNCTUATION", Module: "FIN", Document: "JOURNAL$", Action: "POST"),
            (Key: "DOCUMENT-SPACE", Module: "FIN", Document: "JOUR NAL", Action: "POST"),
            (Key: "DOCUMENT-NONASCII", Module: "FIN", Document: "JOURNÁL", Action: "POST"),
            (Key: "ACTION-PSEUDO", Module: "FIN", Document: "JOURNAL", Action: "ALL"),
            (Key: "ACTION-PSEUDO-ACTIVE", Module: "FIN", Document: "JOURNAL", Action: "ALL_ACTIVE_BOOKS"),
            (Key: "ACTION-PSEUDO-CLASSIFIED", Module: "FIN", Document: "JOURNAL", Action: "ALL_CLASSIFIED_BOOKS"),
            (Key: "ACTION-PSEUDO-COMPACT", Module: "FIN", Document: "JOURNAL", Action: "ALLCLASSIFIEDBOOKS"),
            (Key: "ACTION-DIGIT", Module: "FIN", Document: "JOURNAL", Action: "1POST"),
            (Key: "ACTION-PUNCTUATION", Module: "FIN", Document: "JOURNAL", Action: "POST$"),
            (Key: "ACTION-SPACE", Module: "FIN", Document: "JOURNAL", Action: "POST NOW"),
            (Key: "ACTION-NONASCII", Module: "FIN", Document: "JOURNAL", Action: "PÓST"),
            (Key: "ACTION-TRAILING", Module: "FIN", Document: "JOURNAL", Action: "POST ")
        };
        foreach (var candidate in invalid)
        {
            await FluentActions.Awaiting(() => db.ExecuteAsync(EventInsert(tenant, Guid.NewGuid(), actor,
                    candidate.Key, candidate.Action, "PendingApproval", candidate.Module, candidate.Document)))
                .Should().ThrowAsync<SqlException>().WithMessage("*C6_EVENT_IDENTITY*");
            (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE TenantId='{tenant}'"))
                .Should().Be(0, $"{candidate.Key} must fail atomically before a C6 event is inserted");
        }

        var canonical = Guid.NewGuid();
        await db.ExecuteAsync(EventInsert(tenant, canonical, actor, "CANONICAL-KEY", "POST.EXACT_V2", "PendingApproval", "FIN", "JOURNAL-ENTRY.V2"));
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE Id='{canonical}' AND OriginatingModuleCode='FIN' AND SourceDocumentType='JOURNAL-ENTRY.V2' AND PostingAction='POST.EXACT_V2'"))
            .Should().Be(1);
    }

    private static string EventInsert(Guid tenant, Guid id, Guid actor, string key, string action, string status,
        string module = "FIN", string documentType = "JOURNAL") => $@"
INSERT AccountingEvents(Id,OriginatingModuleCode,SourceDocumentType,SourceDocumentId,PostingAction,IdempotencyKey,EventKind,Version,RootAccountingEventId,
 SelectionFingerprint,RequestFingerprint,Status,EventDate,RequestedAtUtc,RequestedByUserId,PreparedByUserId,PreparedAtUtc,
 ReleasedByUserId,ReleasedAtUtc,ReleaseReason,CompletedAtUtc,FailureMessage,CreatedAt,IsDeleted,TenantId)
VALUES('{id}',N'{module}',N'{documentType}','{Guid.NewGuid()}',N'{action}',N'{key}',N'Original',1,'{id}',N'',REPLICATE('A',64),N'{status}',CONVERT(date,'2026-09-07'),
 SYSUTCDATETIME(),'{actor}','{actor}',SYSUTCDATETIME(),{(status == "PendingApproval" ? "NULL,NULL,NULL,NULL,NULL" : $"'{Guid.NewGuid()}',SYSUTCDATETIME(),N'release',SYSUTCDATETIME(),N'leaf failed'")},SYSUTCDATETIME(),0,'{tenant}');";

    private static string ProducerEventInsert(Guid tenant, Guid id, Guid maker, string key) => $@"
INSERT AccountingEvents(Id,OriginatingModuleCode,SourceDocumentType,SourceDocumentId,PostingAction,IdempotencyKey,EventKind,Version,RootAccountingEventId,
 SelectionFingerprint,RequestFingerprint,Status,EventDate,RequestedAtUtc,RequestedByUserId,PreparedByUserId,PreparedAtUtc,
 ProducerDecisionStatus,ProducerParticipantIdentity,ProducerIntentSnapshotJson,ProducerIntentSnapshotHash,
 ProducerDecidedByUserId,ProducerDecidedAtUtc,ProducerDecisionReason,CreatedAt,IsDeleted,TenantId)
VALUES('{id}',N'INV',N'INVENTORY_DISPOSAL','{Guid.NewGuid()}',N'DISPOSE',N'{key}',N'Original',1,'{id}',N'',REPLICATE('A',64),N'PendingApproval',CONVERT(date,'2026-09-07'),
 SYSUTCDATETIME(),'{maker}','{maker}',SYSUTCDATETIME(),N'Pending',N'INVENTORY.DISPOSAL.V1',N'{{}}',REPLICATE('B',64),NULL,NULL,NULL,SYSUTCDATETIME(),0,'{tenant}');";

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run exact-prefix disposable C6 migration gates.";
        }
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private static readonly Regex SafeName = new("^RHEMAERP_GL_REHEARSAL_C6_[A-Z0-9_]{1,64}$", RegexOptions.CultureInvariant);
        private readonly string _name; private readonly string _master; private readonly string _connection;
        private DisposableDatabase(string name, string master, string connection) => (_name, _master, _connection) = (name, master, connection);

        public static async Task<DisposableDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER") ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var name = $"RHEMAERP_GL_REHEARSAL_C6_{Guid.NewGuid():N}".ToUpperInvariant();
            if (!SafeName.IsMatch(name)) throw new InvalidOperationException("Disposable C6 database prefix assertion failed.");
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var result = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await result.MasterAsync($"CREATE DATABASE [{name}]"); return result;
        }

        public Task CreatePredecessorAsync() => ExecuteAsync(@"
CREATE TABLE Tenants(Id uniqueidentifier NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY);
CREATE TABLE AccountingBooks(Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBooks PRIMARY KEY,TenantId uniqueidentifier NOT NULL,Code nvarchar(20) NOT NULL,
 CONSTRAINT AK_AccountingBooks_TenantId_Id UNIQUE(TenantId,Id));
CREATE TABLE AccountingBookSelectionEvidence(Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBookSelectionEvidence PRIMARY KEY,TenantId uniqueidentifier NOT NULL,
 OriginatingModuleCode nvarchar(40) NOT NULL,SourceDocumentType nvarchar(80) NOT NULL,PostingAction nvarchar(60) NOT NULL,EffectiveDate datetime2 NOT NULL,
 IdempotencyKey nvarchar(100) NOT NULL,SelectionFingerprint nvarchar(64) NOT NULL,IsDeleted bit NOT NULL,
 CONSTRAINT AK_AccountingBookSelectionEvidence_TenantId_Id UNIQUE(TenantId,Id));
CREATE TABLE AccountingBookSelectionEvidenceBooks(Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBookSelectionEvidenceBooks PRIMARY KEY,TenantId uniqueidentifier NOT NULL,
 AccountingBookSelectionEvidenceId uniqueidentifier NOT NULL,AccountingBookId uniqueidentifier NOT NULL,SelectionOrder int NOT NULL,
 AccountingBookCodeSnapshot nvarchar(20) NOT NULL,AuthorityFingerprint nvarchar(64) NOT NULL,IsDeleted bit NOT NULL);
CREATE TABLE JournalEntries(Id uniqueidentifier NOT NULL CONSTRAINT PK_JournalEntries PRIMARY KEY,TenantId uniqueidentifier NOT NULL,AccountingBookId uniqueidentifier NOT NULL,PostingStatus nvarchar(20) NOT NULL);
CREATE TABLE FinancePostingEvents(Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancePostingEvents PRIMARY KEY,TenantId uniqueidentifier NOT NULL,AccountingBookId uniqueidentifier NOT NULL,
 JournalEntryId uniqueidentifier NULL,PostingStatus nvarchar(20) NOT NULL,SourceModule nvarchar(50) NOT NULL,OriginModuleCode nvarchar(10) NULL,
 SourceDocumentType nvarchar(100) NOT NULL,SourceDocumentId uniqueidentifier NOT NULL,PostingAction nvarchar(50) NOT NULL);");

        public async Task ApplyAsync(bool up)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options;
            await using var context = new ApplicationDbContext(options);
            var generator = context.GetService<IMigrationsSqlGenerator>(); var migration = new ExposedMigration();
            foreach (var command in generator.Generate(up ? migration.UpOperations() : migration.DownOperations())) await ExecuteAsync(command.CommandText);
        }
        public async Task ApplyC7Async(bool up)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options;
            await using var context = new ApplicationDbContext(options);
            var generator = context.GetService<IMigrationsSqlGenerator>(); var migration = new ExposedC7Migration();
            foreach (var command in generator.Generate(up ? migration.UpOperations() : migration.DownOperations())) await ExecuteAsync(command.CommandText);
        }
        public async Task ApplyC8Async(bool up)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options;
            await using var context = new ApplicationDbContext(options);
            var generator = context.GetService<IMigrationsSqlGenerator>(); var migration = new ExposedC8Migration();
            foreach (var command in generator.Generate(up ? migration.UpOperations() : migration.DownOperations())) await ExecuteAsync(command.CommandText);
        }
        public async Task<SqlConnection> OpenAsync() { var connection = new SqlConnection(_connection); await connection.OpenAsync(); return connection; }
        public ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options);
        public async Task ExecuteAsync(string sql) { await using var connection = await OpenAsync(); await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 }; await command.ExecuteNonQueryAsync(); }
        public async Task<T> ScalarAsync<T>(string sql) { await using var connection = await OpenAsync(); await using var command = new SqlCommand(sql, connection); return (T)Convert.ChangeType(await command.ExecuteScalarAsync(), typeof(T)); }
        private async Task MasterAsync(string sql) { await using var connection = new SqlConnection(_master); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 }; await command.ExecuteNonQueryAsync(); }
        public async ValueTask DisposeAsync() { if (SafeName.IsMatch(_name)) await MasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END"); }

        private sealed class ExposedMigration : AddAccountingEventOrchestrationFoundation
        {
            public IReadOnlyList<MigrationOperation> UpOperations() { var b = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Up(b); return b.Operations; }
            public IReadOnlyList<MigrationOperation> DownOperations() { var b = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Down(b); return b.Operations; }
        }
        private sealed class ExposedC7Migration : AddProducerIntentStagingC7
        {
            public IReadOnlyList<MigrationOperation> UpOperations() { var b = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Up(b); return b.Operations; }
            public IReadOnlyList<MigrationOperation> DownOperations() { var b = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Down(b); return b.Operations; }
        }
        private sealed class ExposedC8Migration : AddProducerIntentGroupsC8
        {
            public IReadOnlyList<MigrationOperation> UpOperations() { var b = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Up(b); return b.Operations; }
            public IReadOnlyList<MigrationOperation> DownOperations() { var b = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Down(b); return b.Operations; }
        }
    }
}
