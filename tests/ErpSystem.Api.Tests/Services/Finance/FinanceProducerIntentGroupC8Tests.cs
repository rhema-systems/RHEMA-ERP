using System.Reflection;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceProducerIntentGroupC8Tests
{
    [Fact]
    public void PublicSurface_IsGovernedDataOnly_AndHasNoExecutionEndpointOrBookSelection()
    {
        typeof(ProducerIntentGroupsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Should().NotContain(method => method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
        typeof(ProducerIntentGroupRequestDto).GetProperties().Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Book", StringComparison.OrdinalIgnoreCase));
        typeof(ProducerIntentGroupsController).GetMethod(nameof(ProducerIntentGroupsController.Prepare))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.PrepareAccountingEvents);
        var execute = typeof(IFinanceProducerIntentGroupApprovedExecution).GetMethod("ExecuteInAmbientTransactionAsync")!;
        execute.GetParameters().Should().ContainSingle(parameter => parameter.ParameterType == typeof(ProducerOwnerEffectReceiptDto));
        execute.GetParameters().Should().NotContain(parameter => typeof(Delegate).IsAssignableFrom(parameter.ParameterType)
            || parameter.ParameterType == typeof(ApplicationDbContext));
    }

    [Fact]
    public async Task DisabledByDefault_DeniesBeforeC5OrC6()
    {
        var harness = Harness(groupEnabled: false);

        var action = () => harness.Service.PrepareAsync(Group());

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("FINANCE_PRODUCER_INTENT_GROUP_DISABLED*");
        harness.Applicability.VerifyNoOtherCalls();
        harness.Executor.Prepared.Should().BeEmpty();
    }

    [Fact]
    public async Task Prepare_BindsCompleteOrderedMembers_AndExactRetryRejectsOrderCollision()
    {
        var harness = Harness();
        var request = Group();

        var prepared = await harness.Service.PrepareAsync(request);
        var exact = await harness.Service.PrepareAsync(request);

        prepared.Id.Should().Be(exact.Id);
        prepared.Status.Should().Be(ProducerIntentGroupStatuses.PendingApproval);
        prepared.Members.Select(member => member.MemberOrder).Should().Equal(1, 2);
        prepared.Members.Select(member => member.AccountingEvent.Id).Should().Equal(request.Members.Select(member => member.AccountingEventId!.Value));
        harness.Executor.Prepared.Should().HaveCount(2, "one neutral C6 event is prepared for each genuine source identity");

        var reversed = Clone(request);
        reversed.Members = reversed.Members.Reverse().ToList();
        var collision = () => harness.Service.PrepareAsync(reversed);
        await collision.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PRODUCER_INTENT_GROUP_MEMBER_ORDER_CONFLICT*");

        var payloadTamper = Clone(request);
        payloadTamper.Members[0].PostingRequest.Lines[0].DebitAmount++;
        payloadTamper.Members[0].PostingRequest.Lines[1].CreditAmount++;
        await FluentActions.Awaiting(() => harness.Service.PrepareAsync(payloadTamper)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_IDEMPOTENCY_CONFLICT*");
    }

    [Fact]
    public async Task Decision_IsWholeGroupMakerChecker_AndExactRetryBindsCheckerAndReason()
    {
        var harness = Harness();
        var request = Group();
        var prepared = await harness.Service.PrepareAsync(request);

        var sameMaker = () => harness.Service.ApproveAsync(prepared.Id,
            new DecideProducerIntentGroupRequestDto { Group = request, Reason = "Reviewed complete economics" });
        await sameMaker.Should().ThrowAsync<InvalidOperationException>().WithMessage("*checker must differ*");

        harness.Actor = Guid.NewGuid();
        var decision = new DecideProducerIntentGroupRequestDto { Group = request, Reason = "Reviewed complete economics" };
        (await harness.Service.ApproveAsync(prepared.Id, decision)).Status.Should().Be(ProducerIntentGroupStatuses.Approved);
        (await harness.Service.ApproveAsync(prepared.Id, decision)).Status.Should().Be(ProducerIntentGroupStatuses.Approved);

        harness.Actor = Guid.NewGuid();
        var conflict = () => harness.Service.ApproveAsync(prepared.Id, decision);
        await conflict.Should().ThrowAsync<InvalidOperationException>().WithMessage("PRODUCER_INTENT_GROUP_DECISION_CONFLICT*");
        harness.Executor.Executed.Should().BeEmpty("approval never executes a member");

        var rejectedHarness = Harness();
        var rejectedRequest = Group();
        var pending = await rejectedHarness.Service.PrepareAsync(rejectedRequest);
        rejectedHarness.Actor = Guid.NewGuid();
        (await rejectedHarness.Service.RejectAsync(pending.Id,
            new DecideProducerIntentGroupRequestDto { Group = rejectedRequest, Reason = "Rejected complete group" }))
            .Status.Should().Be(ProducerIntentGroupStatuses.Rejected);
        rejectedHarness.Executor.Executed.Should().BeEmpty();
    }

    [Fact]
    public async Task ApprovedExecution_RequiresOwnerSerializableAmbientTransactionBeforeAnyFinanceMutation()
    {
        var harness = Harness();
        var request = Group();
        var prepared = await harness.Service.PrepareAsync(request);
        harness.Actor = Guid.NewGuid();
        await harness.Service.ApproveAsync(prepared.Id,
            new DecideProducerIntentGroupRequestDto { Group = request, Reason = "Approved all members" });

        var action = () => ((IFinanceProducerIntentGroupApprovedExecution)harness.Service)
            .ExecuteInAmbientTransactionAsync(prepared.Id, request, Receipt(harness.TenantId, request));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PRODUCER_INTENT_GROUP_AMBIENT_TRANSACTION_REQUIRED*");
        harness.Db.ProducerIntentGroupReceipts.Should().BeEmpty();
        harness.Db.ProducerIntentGroupAttempts.Should().BeEmpty();
        harness.Executor.Executed.Should().BeEmpty();
    }

    [Fact]
    public async Task AmbientGroupExecution_SecondMemberRollbackThenRecovery_IsZeroOrAllAndExactlyRetryable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var harness = await RelationalHarnessAsync(connection);
        await using var db = harness.Db;
        var request = Group();
        var prepared = await harness.Service.PrepareAsync(request);
        harness.Actor = Guid.NewGuid();
        await harness.Service.ApproveAsync(prepared.Id,
            new DecideProducerIntentGroupRequestDto { Group = request, Reason = "Approved complete group" });
        var receipt = Receipt(harness.TenantId, request);

        await using (var mismatchTransaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var mismatch = Receipt(harness.TenantId, request); mismatch.EffectFingerprint = Hash('E');
            await FluentActions.Awaiting(() => ((IFinanceProducerIntentGroupApprovedExecution)harness.Service)
                .ExecuteInAmbientTransactionAsync(prepared.Id, request, mismatch)).Should()
                .ThrowAsync<InvalidOperationException>().WithMessage("PRODUCER_INTENT_GROUP_OWNER_EFFECT_CONFLICT*");
            await mismatchTransaction.RollbackAsync();
        }

        var ownerId = request.ExpectedOwnerEffect.OwnerEntityId;
        harness.Executor.PersistPostedStatus = true;
        harness.Executor.FailOnExecutionNumber = 2;
        Exception failure;
        await using (var ownerTransaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            db.Tenants.Add(new Tenant { Id = ownerId, Name = "Rolled back owner", Code = $"O-{ownerId:N}"[..20] });
            failure = await Assert.ThrowsAsync<ProducerIntentGroupMemberExecutionException>(() =>
                ((IFinanceProducerIntentGroupApprovedExecution)harness.Service)
                    .ExecuteInAmbientTransactionAsync(prepared.Id, request, receipt));
            await ownerTransaction.RollbackAsync();
        }

        // The reviewed after-rollback boundary must clear the tracked owner, member and receipt graph itself.
        await ((IFinanceProducerIntentGroupApprovedExecution)harness.Service)
            .RecordFailureAfterRollbackAsync(prepared.Id, request, receipt, failure);
        (await db.Tenants.AsNoTracking().AnyAsync(item => item.Id == ownerId)).Should().BeFalse();
        (await db.AccountingEvents.AsNoTracking().ToListAsync()).Should()
            .OnlyContain(item => item.Status == AccountingEventStatuses.PendingApproval);
        (await db.ProducerIntentGroupReceipts.AsNoTracking().CountAsync()).Should().Be(0);
        (await db.ProducerIntentGroupAttempts.AsNoTracking().Select(item => item.Status).ToListAsync())
            .Should().Equal(AccountingEventStatuses.Failed);

        harness.Executor.FailOnExecutionNumber = null;
        harness.Executor.Executed.Clear();
        await using (var recoveryTransaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            db.Tenants.Add(new Tenant { Id = ownerId, Name = "Committed owner", Code = $"O-{ownerId:N}"[..20] });
            var recovered = await ((IFinanceProducerIntentGroupApprovedExecution)harness.Service)
                .ExecuteInAmbientTransactionAsync(prepared.Id, request, receipt);
            recovered.Status.Should().Be(ProducerIntentGroupStatuses.Posted);
            await recoveryTransaction.CommitAsync();
        }

        (await db.Tenants.AsNoTracking().AnyAsync(item => item.Id == ownerId)).Should().BeTrue();
        (await db.AccountingEvents.AsNoTracking().ToListAsync()).Should()
            .OnlyContain(item => item.Status == AccountingEventStatuses.Posted);
        (await db.ProducerIntentGroupReceipts.AsNoTracking().CountAsync()).Should().Be(1);
        (await db.ProducerIntentGroupAttempts.AsNoTracking().OrderBy(item => item.AttemptNumber)
            .Select(item => item.Status).ToListAsync()).Should()
            .Equal(AccountingEventStatuses.Failed, AccountingEventStatuses.Posted);
        harness.Executor.Executed.Should().HaveCount(2);

        await using var exactRetryTransaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var exact = await ((IFinanceProducerIntentGroupApprovedExecution)harness.Service)
            .ExecuteInAmbientTransactionAsync(prepared.Id, request, receipt);
        exact.Status.Should().Be(ProducerIntentGroupStatuses.Posted);
        harness.Executor.Executed.Should().HaveCount(2, "a posted exact retry cannot execute either member again");
        await exactRetryTransaction.RollbackAsync();
    }

    [Fact]
    public async Task FailureAfterRollback_ClearsTrackedOwnerMutation_AndPersistsOnlyDurableGroupFailure()
    {
        var harness = Harness();
        var request = Group();
        var prepared = await harness.Service.PrepareAsync(request);
        harness.Actor = Guid.NewGuid();
        await harness.Service.ApproveAsync(prepared.Id,
            new DecideProducerIntentGroupRequestDto { Group = request, Reason = "Approved all members" });
        harness.Db.Tenants.Add(new Tenant { Id = Guid.NewGuid() });

        await ((IFinanceProducerIntentGroupApprovedExecution)harness.Service).RecordFailureAfterRollbackAsync(
            prepared.Id, request, Receipt(harness.TenantId, request), new InvalidOperationException("second member failed"));

        harness.Db.Tenants.Should().BeEmpty("rolled-back tracked owner state must never be replayed by failure persistence");
        harness.Db.ProducerIntentGroupReceipts.Should().BeEmpty();
        harness.Db.AccountingEventPostings.Should().BeEmpty();
        var durable = await harness.Db.ProducerIntentGroups.Include(group => group.Attempts).SingleAsync();
        durable.Status.Should().Be(ProducerIntentGroupStatuses.Failed);
        durable.Attempts.Should().ContainSingle(attempt => attempt.Status == AccountingEventStatuses.Failed);
        (await harness.Db.AccountingEvents.ToListAsync()).Should().OnlyContain(item => item.Status == AccountingEventStatuses.PendingApproval);
    }

    [Fact]
    public async Task Prepare_RejectsMissingDuplicateAndCrossTenantMembersBeforeC6()
    {
        var harness = Harness();
        var missing = Group(); missing.Members = [missing.Members[0]];
        await FluentActions.Awaiting(() => harness.Service.PrepareAsync(missing)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("PRODUCER_INTENT_GROUP_CARDINALITY*");

        var duplicate = Group(); duplicate.Members = [duplicate.Members[0], CloneIntent(duplicate.Members[0])];
        await FluentActions.Awaiting(() => harness.Service.PrepareAsync(duplicate)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("PRODUCER_INTENT_GROUP_DUPLICATE_MEMBER*");

        var crossTenant = Group(); crossTenant.Members[0].PostingRequest.SourceDocumentTenantId = Guid.NewGuid();
        await FluentActions.Awaiting(() => harness.Service.PrepareAsync(crossTenant)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("PRODUCER_INTENT_GROUP_TENANT_CONFLICT*");
        harness.Executor.Prepared.Should().BeEmpty();
    }

    [Fact]
    public void Migration_IsSingleFailClosedSqlBoundary_WithPreflightWorkflowAndLossRefusingDown()
    {
        typeof(ErpSystem.Data.Migrations.AddProducerIntentGroupsC8).GetCustomAttributes(false)
            .Select(attribute => attribute.GetType().Name).Should().Contain(["DbContextAttribute", "MigrationAttribute"]);
        var migration = new ExposedMigration();
        var up = string.Join("\n", migration.UpOperations().OfType<SqlOperation>().Select(operation => operation.Sql));
        var down = string.Join("\n", migration.DownOperations().OfType<SqlOperation>().Select(operation => operation.Sql));

        up.Should().Contain("C8_PREFLIGHT").And.Contain("C8_GROUP_COMPLETE").And.Contain("C8_GROUP_OUTCOME")
            .And.Contain("C8_RECEIPT_REUSED").And.Contain("TR_AccountingEvents_C7ProducerDecision")
            .And.Contain("[MemberCount] BETWEEN 2 AND 20");
        down.Should().Contain("C8_DOWN_REFUSED").And.Contain("ALTER TRIGGER [TR_AccountingEvents_C7ProducerDecision]");
        down.IndexOf("C8_DOWN_REFUSED", StringComparison.Ordinal).Should()
            .BeLessThan(down.IndexOf("DROP TABLE [ProducerIntentGroupAttempts]", StringComparison.Ordinal));
    }

    private static HarnessState Harness(bool groupEnabled = true)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        return ConfigureHarness(db, groupEnabled);
    }

    private static async Task<HarnessState> RelationalHarnessAsync(SqliteConnection connection)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await CreateMinimalRelationalSchemaAsync(db);
        var state = ConfigureHarness(db, groupEnabled: true);
        db.Tenants.Add(new Tenant { Id = state.TenantId, Name = "C8 tenant", Code = $"T-{state.TenantId:N}"[..20] });
        await db.SaveChangesAsync();
        return state;
    }

    private static async Task CreateMinimalRelationalSchemaAsync(ApplicationDbContext db)
    {
        // The production model carries SQL Server-only check expressions, so this focused SQLite
        // transaction test creates only the aggregate tables it exercises and leaves those checks
        // to the guarded migrated-SQL suite.
        Type[] entities = [typeof(Tenant), typeof(AccountingEvent), typeof(AccountingEventPosting),
            typeof(AccountingEventAttempt), typeof(AccountingEventProducerReceipt), typeof(ProducerIntentGroup), typeof(ProducerIntentGroupMember),
            typeof(ProducerIntentGroupReceipt), typeof(ProducerIntentGroupAttempt)];
        foreach (var clrType in entities)
        {
            var entity = db.Model.FindEntityType(clrType)!;
            var table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var columns = entity.GetProperties().Select(property => new
            {
                Property = property,
                Name = property.GetColumnName(store)!,
                SqlType = SqliteType(property.ClrType)
            }).Where(column => column.Name is not null).GroupBy(column => column.Name).Select(group => group.First()).ToList();
            var keyColumns = entity.FindPrimaryKey()!.Properties.Select(property => property.GetColumnName(store)!).ToList();
            var definitions = columns.Select(column => $"\"{column.Name}\" {column.SqlType}").ToList();
            definitions.Add($"PRIMARY KEY ({string.Join(", ", keyColumns.Select(column => $"\"{column}\""))})");
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"CREATE TABLE \"{table}\" ({string.Join(", ", definitions)});";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string SqliteType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(byte[])) return "BLOB";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(bool) || type.IsEnum) return "INTEGER";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "REAL";
        return "TEXT";
    }

    private static HarnessState ConfigureHarness(ApplicationDbContext db, bool groupEnabled)
    {
        var state = new HarnessState { TenantId = Guid.NewGuid(), Actor = Guid.NewGuid(), Db = db };
        state.Applicability.Setup(service => service.ResolveAsync(It.IsAny<ResolveAccountingBookApplicabilityDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingBookSelectionDto { CalculationInputHash = Hash('A'), SelectionFingerprint = Hash('B'),
                Books = [new AccountingBookSelectionBookDto { AccountingBookId = Guid.NewGuid(), AccountingBookCode = "IFRS", SelectionOrder = 1, AuthorityFingerprint = Hash('C') }] });
        state.User.SetupGet(user => user.TenantId).Returns(() => state.TenantId);
        state.User.SetupGet(user => user.UserId).Returns(() => state.Actor.ToString());
        state.User.SetupGet(user => user.UserName).Returns("c8-test");
        state.Audit.Setup(audit => audit.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        state.Executor = new StubExecutor(state.Db, state.TenantId, () => state.Actor);
        var eventApi = new Mock<IAccountingEventService>();
        eventApi.Setup(api => api.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, CancellationToken ct) => AccountingEventService.Map(
                await state.Db.AccountingEvents.AsNoTracking().SingleAsync(item => item.Id == id, ct)));
        var producer = new FinanceProducerIntentService(state.Applicability.Object, eventApi.Object, state.Executor,
            state.Db, state.User.Object, Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
        state.Service = new FinanceProducerIntentGroupService(state.Db, producer, state.Executor, state.User.Object,
            state.Audit.Object, Options.Create(new FinanceProducerIntentGroupOptions { Enabled = groupEnabled }),
            Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
        return state;
    }

    private static ProducerIntentGroupRequestDto Group()
    {
        var ownerId = Guid.NewGuid();
        var owner = new ProducerOwnerEffectIdentityDto { ParticipantCode = "inventory.disposal.v1",
            OwnerEntityType = "InventoryDisposal", OwnerEntityId = ownerId, OwnerAction = "Dispose", EffectFingerprint = Hash('F') };
        return new ProducerIntentGroupRequestDto
        {
            IdempotencyKey = "inventory-disposal-group-1", ParticipantIdentity = "inventory.disposal.v1",
            ExpectedOwnerEffect = owner,
            Members = [Intent("INVENTORY_DISPOSAL", "RECOVER", 25m, owner), Intent("STOCK_ADJUSTMENT", "DISPOSE_VALUE", 40m, owner)]
        };
    }

    private static ProducerAccountingIntentDto Intent(string type, string action, decimal amount,
        ProducerOwnerEffectIdentityDto owner) => new()
    {
        IdempotencyKey = $"{type}-{action}-1", ParticipantIdentity = "inventory.disposal.v1",
        ExpectedOwnerEffect = new ProducerOwnerEffectIdentityDto { ParticipantCode = owner.ParticipantCode,
            OwnerEntityType = owner.OwnerEntityType, OwnerEntityId = owner.OwnerEntityId,
            OwnerAction = owner.OwnerAction, EffectFingerprint = owner.EffectFingerprint },
        PostingRequest = new ProducerFinancePostingRequestDto { SourceModule = "INV", OriginModuleCode = "INV",
            SourceDocumentType = type, SourceDocumentId = Guid.NewGuid(), PostingAction = action, PostingDate = new DateTime(2026, 9, 8),
            Lines = [new FinancePostingLineDto { AccountId = Guid.NewGuid(), DebitAmount = amount },
                new FinancePostingLineDto { AccountId = Guid.NewGuid(), CreditAmount = amount }] }
    };

    private static ProducerIntentGroupRequestDto Clone(ProducerIntentGroupRequestDto request) =>
        JsonSerializer.Deserialize<ProducerIntentGroupRequestDto>(JsonSerializer.Serialize(request))!;

    private static ProducerAccountingIntentDto CloneIntent(ProducerAccountingIntentDto request) =>
        JsonSerializer.Deserialize<ProducerAccountingIntentDto>(JsonSerializer.Serialize(request))!;

    private static ProducerOwnerEffectReceiptDto Receipt(Guid tenantId, ProducerIntentGroupRequestDto request) => new()
    {
        TenantId = tenantId, ParticipantCode = request.ExpectedOwnerEffect.ParticipantCode,
        OwnerEntityType = request.ExpectedOwnerEffect.OwnerEntityType, OwnerEntityId = request.ExpectedOwnerEffect.OwnerEntityId,
        OwnerAction = request.ExpectedOwnerEffect.OwnerAction, EffectFingerprint = request.ExpectedOwnerEffect.EffectFingerprint
    };

    private static string Hash(char value) => new(value, 64);

    private sealed class HarnessState
    {
        public Guid TenantId { get; set; }
        public Guid Actor { get; set; }
        public ApplicationDbContext Db { get; set; } = null!;
        public Mock<IAccountingBookApplicabilityService> Applicability { get; } = new();
        public Mock<ICurrentUserService> User { get; } = new();
        public Mock<IFinanceAuditService> Audit { get; } = new();
        public StubExecutor Executor { get; set; } = null!;
        public FinanceProducerIntentGroupService Service { get; set; } = null!;
    }

    private sealed class StubExecutor(ApplicationDbContext db, Guid tenantId, Func<Guid> actor) : ITrustedAccountingEventExecutor
    {
        public List<CreateAccountingEventDto> Prepared { get; } = [];
        public List<Guid> Executed { get; } = [];
        public int? FailOnExecutionNumber { get; set; }
        public bool PersistPostedStatus { get; set; }

        public Task<AccountingEventDto> PrepareGroupMemberInAmbientTransactionAsync(CreateAccountingEventDto request,
            CancellationToken cancellationToken = default)
        {
            Prepared.Add(request);
            var id = request.AccountingEventId!.Value;
            var fingerprint = (string)typeof(AccountingEventService).GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [request, request.SelectionIdempotencyKey.Trim().ToUpperInvariant(), 1, id])!;
            var snapshot = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var item = new AccountingEvent { Id = id, TenantId = tenantId, RootAccountingEventId = id, Version = 1,
                EventKind = request.EventKind, IdempotencyKey = request.SelectionIdempotencyKey.Trim().ToUpperInvariant(),
                OriginatingModuleCode = request.PostingRequest.OriginModuleCode ?? request.PostingRequest.SourceModule,
                SourceDocumentType = request.PostingRequest.SourceDocumentType,
                SourceDocumentId = request.PostingRequest.SourceDocumentId, PostingAction = request.PostingRequest.PostingAction,
                Status = AccountingEventStatuses.PendingApproval, ProducerDecisionStatus = ProducerIntentDecisionStatuses.Pending,
                ProducerParticipantIdentity = request.ProducerParticipantIdentity, ProducerIntentSnapshotJson = snapshot,
                ProducerIntentSnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))), SelectionFingerprint = request.ExpectedSelectionFingerprint,
                RequestFingerprint = fingerprint, PreparedByUserId = actor(), PreparedAtUtc = DateTime.UtcNow,
                RequestedAtUtc = DateTime.UtcNow, EventDate = request.PostingRequest.PostingDate, CreatedAt = DateTime.UtcNow };
            db.AccountingEvents.Add(item);
            return Task.FromResult(AccountingEventService.Map(item));
        }

        public async Task<AccountingEventDto> ValidatePreparedGroupMemberAsync(Guid accountingEventId,
            CreateAccountingEventDto request, CancellationToken cancellationToken = default)
        {
            var item = await db.AccountingEvents.AsNoTracking().SingleAsync(entity => entity.Id == accountingEventId, cancellationToken);
            request.AccountingEventId = accountingEventId;
            var fingerprint = (string)typeof(AccountingEventService).GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [request, request.SelectionIdempotencyKey.Trim().ToUpperInvariant(), item.Version, item.RootAccountingEventId])!;
            if (fingerprint != item.RequestFingerprint)
                throw new InvalidOperationException("ACCOUNTING_EVENT_IDEMPOTENCY_CONFLICT: immutable member evidence changed.");
            return AccountingEventService.Map(item);
        }

        public async Task<AccountingEventDto> ExecuteApprovedGroupMemberInAmbientTransactionAsync(Guid accountingEventId,
            ReleaseAccountingEventDto request, Guid producerIntentGroupId, Guid approvedCheckerId,
            Guid expectedAmbientTransactionId, CancellationToken cancellationToken = default)
        {
            Executed.Add(accountingEventId);
            if (FailOnExecutionNumber == Executed.Count)
                throw new InvalidOperationException("injected member failure");
            var validated = await ValidatePreparedGroupMemberAsync(accountingEventId, request.Request, cancellationToken);
            if (PersistPostedStatus)
            {
                var item = await db.AccountingEvents.SingleAsync(entity => entity.Id == accountingEventId, cancellationToken);
                item.Status = AccountingEventStatuses.Posted;
                item.CompletedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                validated = AccountingEventService.Map(item);
            }
            return validated;
        }

        public Task<AccountingEventDto> ExecuteApprovedInAmbientTransactionAsync(Guid accountingEventId,
            ReleaseAccountingEventDto request, ProducerOwnerEffectReceiptDto receipt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecordApprovedFailureAfterRollbackAsync(Guid accountingEventId, ReleaseAccountingEventDto request,
            ProducerOwnerEffectReceiptDto receipt, Exception failure, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ExposedMigration : ErpSystem.Data.Migrations.AddProducerIntentGroupsC8
    {
        public IReadOnlyList<MigrationOperation> UpOperations() { var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Up(builder); return builder.Operations; }
        public IReadOnlyList<MigrationOperation> DownOperations() { var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Down(builder); return builder.Operations; }
    }
}
