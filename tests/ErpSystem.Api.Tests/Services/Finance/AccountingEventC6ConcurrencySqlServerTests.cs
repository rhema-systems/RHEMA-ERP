using System.Data.Common;
using System.Text.RegularExpressions;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ErpSystem.Shared;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingEventC6ConcurrencySqlServerTests
{
    [SqlServerFact]
    public async Task ApprovedProducerReceipt_JoinsOwnerTransaction_RollsBackPersistsFailureAndRecoversExactlyOnce()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        Seeded seeded;
        await using (var setup = database.Context())
        {
            seeded = await SeedAsync(setup, database);
        }
        var request = Request(database, "C7-OWNER-ATOMIC");
        request.ProducerParticipantIdentity = "INVENTORY.DISPOSAL.V1";
        request.ExpectedOwnerEffect = new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = "INVENTORY.DISPOSAL.V1", OwnerEntityType = "INVENTORY_DISPOSAL",
            OwnerEntityId = request.PostingRequest.SourceDocumentId, OwnerAction = "DISPOSE", EffectFingerprint = new string('F', 64)
        };
        var receipt = new ProducerOwnerEffectReceiptDto
        {
            TenantId = database.TenantId, ParticipantCode = "INVENTORY.DISPOSAL.V1", OwnerEntityType = "INVENTORY_DISPOSAL",
            OwnerEntityId = request.PostingRequest.SourceDocumentId, OwnerAction = "DISPOSE", EffectFingerprint = new string('F', 64)
        };
        Guid eventId;
        await using (var prepare = database.Context())
            eventId = (await Service(prepare, database.TenantId, database.MakerId).CreateAsync(request)).Id;
        await using (var approve = database.Context())
            await Service(approve, database.TenantId, database.CheckerId).ApproveAsync(eventId,
                new ReleaseAccountingEventDto { Reason = "independent producer approval", Request = request });

        await using (var noAmbient = database.Context())
            await FluentActions.Awaiting(() => ((ITrustedAccountingEventExecutor)Service(noAmbient, database.TenantId, database.MakerId))
                .ExecuteApprovedInAmbientTransactionAsync(eventId,
                    new ReleaseAccountingEventDto { Reason = "independent producer approval", Request = request }, receipt))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_AMBIENT_TRANSACTION_REQUIRED:*");

        await using (var replaced = database.Context())
        {
            var service = Service(replaced, database.TenantId, database.MakerId);
            await using var original = await replaced.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var originalId = original.TransactionId;
            await original.RollbackAsync();
            await original.DisposeAsync();
            await using var replacement = await replaced.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            Action changed = () => typeof(AccountingEventService).GetMethod("RequireTransactionIdentity",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(service, [originalId]);
            changed.Should().Throw<System.Reflection.TargetInvocationException>().Which.InnerException.Should()
                .BeOfType<InvalidOperationException>().Which.Message.Should().StartWith("ACCOUNTING_EVENT_AMBIENT_TRANSACTION_CHANGED:");
            await replacement.RollbackAsync();
        }

        await using (var fail = database.Context())
        {
            await using var tx = await fail.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var ownerCase = await fail.InventoryDisposalCases.SingleAsync(item => item.Id == seeded.DisposalCaseId);
            ownerCase.Status = InventoryDisposalStatus.AuditVerified;
            ownerCase.AuditVerifiedById = database.MakerId;
            ownerCase.AuditVerifiedAtUtc = DateTime.UtcNow;
            ownerCase.AuditFindings = "tracked owner effect inside the shared transaction";
            var executor = (ITrustedAccountingEventExecutor)Service(fail, database.TenantId, database.MakerId, persistAudit: true);
            var failure = await FluentActions.Awaiting(() => executor.ExecuteApprovedInAmbientTransactionAsync(eventId,
                new ReleaseAccountingEventDto { Reason = "independent producer approval", Request = request }, receipt))
                .Should().ThrowAsync<InvalidOperationException>();
            await tx.RollbackAsync();
            await tx.DisposeAsync();
            fail.ChangeTracker.Entries<InventoryDisposalCase>().Should().ContainSingle(
                "the caller deliberately retains rolled-back owner tracking state");
            await executor.RecordApprovedFailureAfterRollbackAsync(eventId,
                new ReleaseAccountingEventDto { Reason = "independent producer approval", Request = request }, receipt, failure.Which);
        }
        await using (var verifyFailure = database.Context())
        {
            var ownerCase = await verifyFailure.InventoryDisposalCases.SingleAsync(item => item.Id == seeded.DisposalCaseId);
            ownerCase.Status.Should().Be(InventoryDisposalStatus.Identified);
            ownerCase.AuditVerifiedById.Should().BeNull();
            ownerCase.AuditVerifiedAtUtc.Should().BeNull();
            ownerCase.AuditFindings.Should().BeNull();
            var failed = await verifyFailure.AccountingEvents.Include(item => item.Attempts).Include(item => item.Postings)
                .SingleAsync(item => item.Id == eventId);
            failed.Status.Should().Be(AccountingEventStatuses.Failed);
            failed.ProducerDecisionStatus.Should().Be(ProducerIntentDecisionStatuses.Approved);
            failed.Attempts.Should().ContainSingle(item => item.Status == AccountingEventStatuses.Failed);
            failed.Postings.Should().BeEmpty();
            (await verifyFailure.AccountingBookSelectionEvidence.CountAsync()).Should().Be(0);
            (await verifyFailure.AccountingEventProducerReceipts.CountAsync(item => item.AccountingEventId == eventId)).Should().Be(0);
            (await verifyFailure.FinancePostingEvents.CountAsync()).Should().Be(0);
            (await verifyFailure.JournalEntries.CountAsync()).Should().Be(0);
            (await verifyFailure.AuditLogs.CountAsync(item => item.Action == FinanceAuditEvents.AccountingEventFailed
                && item.Resource == "Finance.AccountingEvent" && item.ResourceId == eventId.ToString())).Should().Be(1);
        }

        await using (var retry = database.Context())
        {
            await RepairSecondBookAsync(database, seeded);
            await using var tx = await retry.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var ownerCase = await retry.InventoryDisposalCases.SingleAsync(item => item.Id == seeded.DisposalCaseId);
            ownerCase.Status = InventoryDisposalStatus.AuditVerified;
            ownerCase.AuditVerifiedById = database.MakerId;
            ownerCase.AuditVerifiedAtUtc = DateTime.UtcNow;
            ownerCase.AuditFindings = "tracked owner effect committed on exact recovery";
            (await ((ITrustedAccountingEventExecutor)Service(retry, database.TenantId, database.MakerId)).ExecuteApprovedInAmbientTransactionAsync(eventId,
                new ReleaseAccountingEventDto { Reason = "independent producer approval", Request = request }, receipt))
                .Status.Should().Be(AccountingEventStatuses.Posted);
            await tx.CommitAsync();
        }
        await using (var exactRetry = database.Context())
        {
            await using var tx = await exactRetry.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            (await ((ITrustedAccountingEventExecutor)Service(exactRetry, database.TenantId, database.MakerId)).ExecuteApprovedInAmbientTransactionAsync(eventId,
                new ReleaseAccountingEventDto { Reason = "independent producer approval", Request = request }, receipt))
                .Status.Should().Be(AccountingEventStatuses.Posted);
            await tx.CommitAsync();
            (await exactRetry.InventoryDisposalCases.SingleAsync(item => item.Id == seeded.DisposalCaseId)).Status
                .Should().Be(InventoryDisposalStatus.AuditVerified);
            (await exactRetry.AccountingEventProducerReceipts.CountAsync(item => item.AccountingEventId == eventId)).Should().Be(1);
            await FluentActions.Awaiting(() => exactRetry.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [AccountingEventProducerReceipts] SET [OwnerAction]={"TAMPER"} WHERE [AccountingEventId]={eventId}"))
                .Should().ThrowAsync<SqlException>().WithMessage("*C7_RECEIPT_IMMUTABLE*");
        }

        var reused = Request(database, "C7-OWNER-REUSED", Guid.NewGuid());
        reused.ProducerParticipantIdentity = request.ProducerParticipantIdentity;
        reused.ExpectedOwnerEffect = request.ExpectedOwnerEffect;
        Guid reusedId;
        await using (var prepareReuse = database.Context())
            reusedId = (await Service(prepareReuse, database.TenantId, database.MakerId).CreateAsync(reused)).Id;
        await using (var approveReuse = database.Context())
            await Service(approveReuse, database.TenantId, database.CheckerId).ApproveAsync(reusedId,
                new ReleaseAccountingEventDto { Reason = "independent producer approval", Request = reused });
        await using (var rejectReuse = database.Context())
        {
            await using var tx = await rejectReuse.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await FluentActions.Awaiting(() => ((ITrustedAccountingEventExecutor)Service(rejectReuse, database.TenantId, database.MakerId))
                    .ExecuteApprovedInAmbientTransactionAsync(reusedId,
                        new ReleaseAccountingEventDto { Reason = "independent producer approval", Request = reused }, receipt))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_OWNER_EFFECT_REUSED:*");
            await tx.RollbackAsync();
        }
    }

    [SqlServerFact]
    public async Task TwoContextsSerializeIdenticalAndConflictingSubmissions()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await using (var setup = database.Context())
        {
            setup.Tenants.Add(Tenant(database.TenantId));
            await setup.SaveChangesAsync();
        }
        var request = Request(database);
        async Task<(AccountingEventDto? Result, Exception? Error)> Prepare(CreateAccountingEventDto candidate, DbCommandInterceptor interceptor)
        {
            await using var db = database.Context(interceptor);
            try { return (await Service(db, database.TenantId, database.MakerId).CreateAsync(candidate), null); }
            catch (Exception error) { return (null, error); }
        }
        var identicalRace = new EventLockCompetition();
        var identical = await Task.WhenAll(Prepare(request, identicalRace.Owner), Prepare(Request(database), identicalRace.Contender));
        identicalRace.AssertCompeted();
        identical.Should().OnlyContain(item => item.Error == null);
        identical.Select(item => item.Result!.Id).Distinct().Should().ContainSingle();
        var raceSource = Guid.NewGuid();
        var conflictA = Request(database, "EVENT-CONFLICT-RACE", raceSource); conflictA.PostingRequest.Description = "winner A";
        var conflictB = Request(database, "EVENT-CONFLICT-RACE", raceSource); conflictB.PostingRequest.Description = "winner B";
        var conflictRace = new EventLockCompetition();
        var conflicting = await Task.WhenAll(Prepare(conflictA, conflictRace.Owner), Prepare(conflictB, conflictRace.Contender));
        conflictRace.AssertCompeted();
        conflicting.Count(item => item.Result is not null && item.Error is null).Should().Be(1);
        conflicting.Count(item => item.Result is null && item.Error is InvalidOperationException error
            && error.Message.StartsWith("ACCOUNTING_EVENT_IDEMPOTENCY_CONFLICT:", StringComparison.Ordinal)).Should().Be(1);
        await using var verify = database.Context();
        (await verify.AccountingEvents.CountAsync()).Should().Be(2);
        (await verify.AccountingEvents.CountAsync(x => x.IdempotencyKey == "EVENT-CONFLICT-RACE")).Should().Be(1);
    }

    [SqlServerFact]
    public async Task ReleaseRacesSerializeSameAuthorityAndRejectDifferentCheckerOrReason()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        Seeded seeded;
        await using (var setup = database.Context()) seeded = await SeedAsync(setup, database);
        await RepairSecondBookAsync(database, seeded);

        async Task<Guid> Prepare(CreateAccountingEventDto request)
        {
            await using var context = database.Context();
            return (await Service(context, database.TenantId, database.MakerId).CreateAsync(request)).Id;
        }
        async Task<(AccountingEventDto? Result, Exception? Error)> Release(Guid id, CreateAccountingEventDto request,
            Guid checker, string reason, DbCommandInterceptor interceptor)
        {
            await using var context = database.Context(interceptor);
            try { return (await Service(context, database.TenantId, checker).ReleaseAsync(id,
                new ReleaseAccountingEventDto { Reason = reason, Request = request }), null); }
            catch (Exception error) { return (null, error); }
        }

        var sameRequest = Request(database, "RELEASE-SAME", Guid.NewGuid()); var sameId = await Prepare(sameRequest);
        var sameRace = new EventLockCompetition();
        var same = await Task.WhenAll(
            Release(sameId, sameRequest, database.CheckerId, "same governed release", sameRace.Owner),
            Release(sameId, Request(database, "RELEASE-SAME", sameRequest.PostingRequest.SourceDocumentId), database.CheckerId, "same governed release", sameRace.Contender));
        sameRace.AssertCompeted();
        same.Count(item => item.Error == null && item.Result != null && item.Result.Status == AccountingEventStatuses.Posted).Should().Be(2);
        await using (var wrongPostedActor = database.Context())
        {
            var before = await wrongPostedActor.AccountingEventAttempts.CountAsync(item => item.AccountingEventId == sameId);
            await FluentActions.Awaiting(() => Service(wrongPostedActor, database.TenantId, database.OtherCheckerId).ReleaseAsync(sameId,
                new ReleaseAccountingEventDto { Reason = "same governed release", Request = sameRequest }))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_RELEASE_CHECKER_CONFLICT:*");
            (await wrongPostedActor.AccountingEventAttempts.CountAsync(item => item.AccountingEventId == sameId)).Should().Be(before);
        }
        var correction = Request(database, "RELEASE-SAME-CORRECTION", sameRequest.PostingRequest.SourceDocumentId);
        correction.EventKind = AccountingEventKinds.Correction; correction.CorrectsAccountingEventId = sameId;
        correction.SupersedesAccountingEventId = sameId; correction.PostingRequest.PostingDate = database.EventDate.AddDays(1);
        var correctionId = await Prepare(correction);
        await using (var correctionContext = database.Context())
            (await Service(correctionContext, database.TenantId, database.CheckerId).ReleaseAsync(correctionId,
                new ReleaseAccountingEventDto { Reason = "exact correction", Request = correction })).Status.Should().Be(AccountingEventStatuses.Posted);
        await using (var correctionVerify = database.Context())
        {
            var leaves = await correctionVerify.AccountingEventPostings.Where(x => x.AccountingEventId == correctionId)
                .Select(x => x.FinancePostingEvent!).ToListAsync();
            leaves.Should().HaveCount(2).And.OnlyContain(x => x.SourceModule == "GL" && x.OriginModuleCode == "FIN"
                && x.SourceDocumentType == "AccountingEventCorrection" && x.SourceDocumentId == correctionId && x.PostingAction == "POST");
        }

        var checkerRequest = Request(database, "RELEASE-CHECKER", Guid.NewGuid()); var checkerId = await Prepare(checkerRequest);
        var checkerRace = new EventLockCompetition();
        var checkerOutcomes = await Task.WhenAll(
            Release(checkerId, checkerRequest, database.CheckerId, "fixed release", checkerRace.Owner),
            Release(checkerId, Request(database, "RELEASE-CHECKER", checkerRequest.PostingRequest.SourceDocumentId), database.OtherCheckerId, "fixed release", checkerRace.Contender));
        checkerRace.AssertCompeted();
        checkerOutcomes.Count(item => item.Result != null && item.Result.Status == AccountingEventStatuses.Posted && item.Error == null).Should().Be(1);
        checkerOutcomes.Count(item => item.Error is InvalidOperationException error
            && error.Message.StartsWith("ACCOUNTING_EVENT_RELEASE_CHECKER_CONFLICT:", StringComparison.Ordinal)).Should().Be(1);

        var reasonRequest = Request(database, "RELEASE-REASON", Guid.NewGuid()); var reasonId = await Prepare(reasonRequest);
        var reasonRace = new EventLockCompetition();
        var reasonOutcomes = await Task.WhenAll(
            Release(reasonId, reasonRequest, database.CheckerId, "fixed release", reasonRace.Owner),
            Release(reasonId, Request(database, "RELEASE-REASON", reasonRequest.PostingRequest.SourceDocumentId), database.CheckerId, "changed release", reasonRace.Contender));
        reasonRace.AssertCompeted();
        reasonOutcomes.Count(item => item.Result != null && item.Result.Status == AccountingEventStatuses.Posted && item.Error == null).Should().Be(1);
        reasonOutcomes.Count(item => item.Error is InvalidOperationException error
            && error.Message.StartsWith("ACCOUNTING_EVENT_RELEASE_REASON_CONFLICT:", StringComparison.Ordinal)).Should().Be(1);
    }

    [SqlServerFact]
    public async Task SecondBookFailureRollsBackEveryEconomicWrite_PersistsFailure_Retries_AndReversesExactGroup()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        Seeded seeded;
        await using (var setup = database.Context()) seeded = await SeedAsync(setup, database);
        var request = Request(database);
        Guid eventId;
        await using (var prepare = database.Context())
            eventId = (await Service(prepare, database.TenantId, database.MakerId).CreateAsync(request)).Id;

        await using (var release = database.Context())
        {
            await FluentActions.Awaiting(() => Service(release, database.TenantId, database.CheckerId)
                .ReleaseAsync(eventId, new ReleaseAccountingEventDto { Reason = "independent release", Request = Request(database) }))
                .Should().ThrowAsync<InvalidOperationException>();
        }
        await using (var failed = database.Context())
        {
            var item = await failed.AccountingEvents.Include(x => x.Attempts).Include(x => x.Postings).SingleAsync(x => x.Id == eventId);
            item.Status.Should().Be(AccountingEventStatuses.Failed); item.Attempts.Should().ContainSingle(x => x.Status == AccountingEventStatuses.Failed);
            item.Postings.Should().BeEmpty();
            (await failed.JournalEntries.CountAsync()).Should().Be(0);
            (await failed.FinancePostingEvents.CountAsync()).Should().Be(0);
            (await failed.AccountBalances.CountAsync()).Should().Be(0);
            (await failed.AccountingBookSelectionEvidence.CountAsync()).Should().Be(0);
            (await failed.Accounts.Where(x => x.Id == seeded.DebitId || x.Id == seeded.CreditId).SumAsync(x => x.Balance)).Should().Be(0m);
        }
        await using (var wrongFailedActor = database.Context())
        {
            await FluentActions.Awaiting(() => Service(wrongFailedActor, database.TenantId, database.OtherCheckerId)
                .ReleaseAsync(eventId, new ReleaseAccountingEventDto { Reason = "independent release", Request = Request(database) }))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_RELEASE_CHECKER_CONFLICT:*");
            (await wrongFailedActor.AccountingEventAttempts.CountAsync(item => item.AccountingEventId == eventId)).Should().Be(1);
        }

        await using (var repair = database.Context())
        {
            foreach (var account in new[] { seeded.DebitId, seeded.CreditId })
                if (!await repair.AccountAccountingBooks.AnyAsync(x => x.AccountId == account && x.AccountingBookId == seeded.SecondBookId))
                    repair.AccountAccountingBooks.Add(new AccountAccountingBook { TenantId = database.TenantId, AccountId = account,
                        AccountingBookId = seeded.SecondBookId, AccountClassificationId = account == seeded.DebitId ? seeded.SecondAssetClassId : seeded.SecondLiabilityClassId,
                        IsEnabled = true });
            await repair.SaveChangesAsync();
        }
        await using (var retry = database.Context())
        {
            var posted = await Service(retry, database.TenantId, database.CheckerId)
                .ReleaseAsync(eventId, new ReleaseAccountingEventDto { Reason = "independent release", Request = Request(database) });
            posted.Status.Should().Be(AccountingEventStatuses.Posted);
            posted.Postings.Should().HaveCount(2).And.OnlyContain(x => x.Status == AccountingEventStatuses.Posted && x.EventVersion == 1);
            posted.Attempts.Should().HaveCount(2);
            (await retry.AccountingBookSelectionEvidence.CountAsync()).Should().Be(1);
        }

        var reversalRequest = Request(database, "EVENT-REVERSAL"); reversalRequest.EventKind = AccountingEventKinds.Reversal;
        reversalRequest.PostingRequest.PostingDate = database.EventDate.AddDays(1);
        reversalRequest.ReversesAccountingEventId = eventId; reversalRequest.SupersedesAccountingEventId = eventId;
        Guid reversalId;
        await using (var reversePrepare = database.Context())
            reversalId = (await Service(reversePrepare, database.TenantId, database.MakerId).CreateAsync(reversalRequest)).Id;
        await using (var reverseRelease = database.Context())
        {
            var reversed = await Service(reverseRelease, database.TenantId, database.CheckerId)
                .ReleaseAsync(reversalId, new ReleaseAccountingEventDto { Reason = "exact group reversal", Request = reversalRequest });
            reversed.Version.Should().Be(2); reversed.RootAccountingEventId.Should().Be(eventId);
            reversed.EventDate.Should().Be(database.EventDate.AddDays(1));
            reversed.Postings.Select(x => (x.AccountingBookId, x.EventVersion)).Should().BeEquivalentTo(
                [(seeded.PrimaryBookId, 2), (seeded.SecondBookId, 2)]);
        }
        await using var final = database.Context();
        (await final.JournalEntries.CountAsync()).Should().Be(4);
        (await final.FinancePostingEvents.CountAsync()).Should().Be(4);
        (await final.AccountingEventPostings.CountAsync()).Should().Be(4);
        var originalLeaves = await final.AccountingEventPostings.Where(x => x.AccountingEventId == eventId)
            .ToDictionaryAsync(x => x.AccountingBookId, x => x.FinancePostingEventId!.Value);
        var reversalLeaves = await final.AccountingEventPostings.Include(x => x.FinancePostingEvent)
            .Where(x => x.AccountingEventId == reversalId).ToListAsync();
        reversalLeaves.Should().OnlyContain(x => x.FinancePostingEvent != null && x.FinancePostingEvent.SourceModule == "GL"
            && x.FinancePostingEvent.OriginModuleCode == "FIN" && x.FinancePostingEvent.SourceDocumentType == "FinancePostingEventReversal"
            && x.FinancePostingEvent.PostingAction == "Reverse" && x.FinancePostingEvent.SourceDocumentId == originalLeaves[x.AccountingBookId]);
        (await final.Accounts.Where(x => x.Id == seeded.DebitId || x.Id == seeded.CreditId)
            .Select(x => x.Balance).ToListAsync()).Should().HaveCount(2).And.OnlyContain(value => value == 0m);
        var balances = await final.AccountBalances.Where(x => x.AccountId == seeded.DebitId || x.AccountId == seeded.CreditId).ToListAsync();
        balances.Select(x => (x.AccountId, x.AccountingBookId)).Should().BeEquivalentTo(new[]
        {
            (seeded.DebitId, seeded.PrimaryBookId), (seeded.CreditId, seeded.PrimaryBookId),
            (seeded.DebitId, seeded.SecondBookId), (seeded.CreditId, seeded.SecondBookId)
        });
        balances.Should().OnlyContain(x => x.ClosingBalance == 0m && x.PeriodNetMovement == 0m && x.YearToDateNetMovement == 0m);
        (await final.AccountCurrencyExposures.CountAsync(x => x.AccountId == seeded.DebitId || x.AccountId == seeded.CreditId)).Should().Be(0);
    }

    [SqlServerFact]
    public async Task ProducerGroup_UsesRealC5AndC6_RollsBackSecondMemberAndRecoversExactlyOnce()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        Seeded seeded;
        await using (var setup = database.Context()) seeded = await SeedAsync(setup, database);
        ProducerIntentGroupRequestDto request;
        await using (var resolve = database.Context())
        {
            var applicability = Applicability(resolve, database.TenantId, database.MakerId);
            var recovery = await applicability.ResolveAsync(new ResolveAccountingBookApplicabilityDto
            {
                EffectiveDate = database.EventDate, OriginatingModuleCode = "INV",
                SourceDocumentType = "INVENTORY_DISPOSAL", PostingAction = "RECOVER"
            });
            var valuation = await applicability.ResolveAsync(new ResolveAccountingBookApplicabilityDto
            {
                EffectiveDate = database.EventDate, OriginatingModuleCode = "INV",
                SourceDocumentType = "STOCK_ADJUSTMENT", PostingAction = "DISPOSE_VALUE"
            });
            recovery.Books.Should().ContainSingle();
            valuation.Books.Should().HaveCount(2);
            var owner = new ProducerOwnerEffectIdentityDto
            {
                ParticipantCode = "INVENTORY.DISPOSAL.V1", OwnerEntityType = "INVENTORY_DISPOSAL",
                OwnerEntityId = seeded.DisposalCaseId, OwnerAction = "DISPOSE", EffectFingerprint = new string('8', 64)
            };
            ProducerAccountingIntentDto Member(string key, string document, string action, Guid source, decimal amount) => new()
            {
                IdempotencyKey = key, ParticipantIdentity = owner.ParticipantCode, ExpectedOwnerEffect = owner,
                PostingRequest = new ProducerFinancePostingRequestDto
                {
                    SourceModule = "INV", OriginModuleCode = "INV", SourceDocumentType = document,
                    SourceDocumentId = source, SourceDocumentTenantId = database.TenantId, PostingAction = action,
                    PostingDate = database.EventDate, FunctionalCurrencyCode = "GHS", Description = key,
                    Lines = [new FinancePostingLineDto { AccountId = seeded.DebitId, DebitAmount = amount, LineNumber = 1 },
                        new FinancePostingLineDto { AccountId = seeded.CreditId, CreditAmount = amount, LineNumber = 2 }]
                }
            };
            request = new ProducerIntentGroupRequestDto
            {
                IdempotencyKey = "C8:REAL:GROUP:1", ParticipantIdentity = owner.ParticipantCode, ExpectedOwnerEffect = owner,
                Members = [Member("C8:REAL:RECOVERY:1", "INVENTORY_DISPOSAL", "RECOVER", Guid.NewGuid(), 25m),
                    Member("C8:REAL:VALUATION:1", "STOCK_ADJUSTMENT", "DISPOSE_VALUE", Guid.NewGuid(), 100m)]
            };
        }

        ProducerIntentGroupDto prepared;
        await using (var prepare = database.Context()) prepared = await GroupService(prepare, database.TenantId, database.MakerId).PrepareAsync(request);
        await using (var approve = database.Context()) await GroupService(approve, database.TenantId, database.CheckerId).ApproveAsync(prepared.Id,
            new DecideProducerIntentGroupRequestDto { Group = request, Reason = "independent complete-group approval" });
        var receipt = new ProducerOwnerEffectReceiptDto
        {
            TenantId = database.TenantId, ParticipantCode = request.ExpectedOwnerEffect.ParticipantCode,
            OwnerEntityType = request.ExpectedOwnerEffect.OwnerEntityType, OwnerEntityId = request.ExpectedOwnerEffect.OwnerEntityId,
            OwnerAction = request.ExpectedOwnerEffect.OwnerAction, EffectFingerprint = request.ExpectedOwnerEffect.EffectFingerprint
        };

        Exception failure;
        await using (var execute = database.Context())
        {
            var service = GroupService(execute, database.TenantId, database.MakerId);
            await using var tx = await execute.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var owner = await execute.InventoryDisposalCases.SingleAsync(item => item.Id == seeded.DisposalCaseId);
            owner.Status = InventoryDisposalStatus.AuditVerified; owner.AuditVerifiedById = database.MakerId;
            owner.AuditVerifiedAtUtc = DateTime.UtcNow; owner.AuditFindings = "C8 tracked owner effect";
            failure = await Assert.ThrowsAsync<ProducerIntentGroupMemberExecutionException>(() =>
                ((IFinanceProducerIntentGroupApprovedExecution)service).ExecuteInAmbientTransactionAsync(prepared.Id, request, receipt));
            ((ProducerIntentGroupMemberExecutionException)failure).MemberOrder.Should().Be(2);
            await tx.RollbackAsync();
            await tx.DisposeAsync();
            execute.ChangeTracker.Entries<InventoryDisposalCase>().Should().ContainSingle();
            await ((IFinanceProducerIntentGroupApprovedExecution)service)
                .RecordFailureAfterRollbackAsync(prepared.Id, request, receipt, failure);
        }
        await using (var failed = database.Context())
        {
            (await failed.InventoryDisposalCases.SingleAsync(item => item.Id == seeded.DisposalCaseId)).Status
                .Should().Be(InventoryDisposalStatus.Identified);
            var group = await failed.ProducerIntentGroups.Include(item => item.Attempts).SingleAsync(item => item.Id == prepared.Id);
            group.Status.Should().Be(ProducerIntentGroupStatuses.Failed);
            group.Attempts.Should().ContainSingle(item => item.Status == AccountingEventStatuses.Failed && item.FailedMemberOrder == 2);
            (await failed.ProducerIntentGroupReceipts.CountAsync()).Should().Be(0);
            (await failed.AccountingBookSelectionEvidence.CountAsync()).Should().Be(0);
            (await failed.AccountingEventPostings.CountAsync()).Should().Be(0);
            (await failed.FinancePostingEvents.CountAsync()).Should().Be(0);
            (await failed.JournalEntries.CountAsync()).Should().Be(0);
            (await failed.AccountBalances.CountAsync()).Should().Be(0);
            (await failed.AccountCurrencyExposures.CountAsync()).Should().Be(0);
        }

        await RepairSecondBookAsync(database, seeded);
        await using (var recover = database.Context())
        {
            var service = GroupService(recover, database.TenantId, database.MakerId);
            await using var tx = await recover.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var owner = await recover.InventoryDisposalCases.SingleAsync(item => item.Id == seeded.DisposalCaseId);
            owner.Status = InventoryDisposalStatus.AuditVerified; owner.AuditVerifiedById = database.MakerId;
            owner.AuditVerifiedAtUtc = DateTime.UtcNow; owner.AuditFindings = "C8 committed owner effect";
            (await ((IFinanceProducerIntentGroupApprovedExecution)service)
                .ExecuteInAmbientTransactionAsync(prepared.Id, request, receipt)).Status.Should().Be(ProducerIntentGroupStatuses.Posted);
            await tx.CommitAsync();
        }
        await using (var retry = database.Context())
        {
            var before = (Events: await retry.FinancePostingEvents.CountAsync(), Journals: await retry.JournalEntries.CountAsync(),
                Postings: await retry.AccountingEventPostings.CountAsync(), Attempts: await retry.ProducerIntentGroupAttempts.CountAsync());
            await using var tx = await retry.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            (await ((IFinanceProducerIntentGroupApprovedExecution)GroupService(retry, database.TenantId, database.MakerId))
                .ExecuteInAmbientTransactionAsync(prepared.Id, request, receipt)).Status.Should().Be(ProducerIntentGroupStatuses.Posted);
            await tx.CommitAsync();
            (await retry.FinancePostingEvents.CountAsync()).Should().Be(before.Events);
            (await retry.JournalEntries.CountAsync()).Should().Be(before.Journals);
            (await retry.AccountingEventPostings.CountAsync()).Should().Be(before.Postings);
            (await retry.ProducerIntentGroupAttempts.CountAsync()).Should().Be(before.Attempts);
            (await retry.AccountingBookSelectionEvidence.CountAsync()).Should().Be(2);
            (await retry.AccountingEventPostings.CountAsync()).Should().Be(3);
            (await retry.ProducerIntentGroupReceipts.CountAsync()).Should().Be(1);
        }
    }

    private static AccountingEventService Service(ApplicationDbContext db, Guid tenant, Guid actor, bool persistAudit = false)
    {
        var user = User(tenant, actor);
        IFinanceAuditService audit;
        if (persistAudit)
            audit = new FinanceAuditService(db, user.Object, new HttpContextAccessor());
        else
        {
            var auditMock = new Mock<IFinanceAuditService>();
            auditMock.Setup(x => x.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog());
            audit = auditMock.Object;
        }
        var leaf = new FinancePostingEngine(db, user.Object, NullLogger<FinancePostingEngine>.Instance, audit);
        var applicability = Applicability(db, tenant, actor, audit: audit);
        return new AccountingEventService(db, user.Object, applicability, leaf, audit,
            Options.Create(new AccountingEventOptions { Enabled = true }));
    }

    private static FinanceProducerIntentGroupService GroupService(ApplicationDbContext db, Guid tenant, Guid actor)
    {
        var user = User(tenant, actor); var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog());
        var events = Service(db, tenant, actor);
        var producer = new FinanceProducerIntentService(Applicability(db, tenant, actor), events, events, db, user.Object,
            Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
        return new FinanceProducerIntentGroupService(db, producer, events, user.Object, audit.Object,
            Options.Create(new FinanceProducerIntentGroupOptions { Enabled = true }),
            Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
    }

    private static AccountingBookApplicabilityService Applicability(ApplicationDbContext db, Guid tenant, Guid actor,
        bool governedWorkflow = false, IFinanceAuditService? audit = null)
    {
        var user = User(tenant, actor); var workflow = new Mock<IWorkflowService>();
        if (governedWorkflow)
        {
            workflow.Setup(x => x.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(true);
            workflow.Setup(x => x.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid() });
            workflow.Setup(x => x.CanUserApproveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(true);
            workflow.Setup(x => x.ProcessApprovalStepAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        }
        if (audit is null)
        {
            var auditMock = new Mock<IFinanceAuditService>();
            auditMock.Setup(x => x.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog());
            audit = auditMock.Object;
        }
        var initialization = new Mock<IAccountingBookInitializationService>();
        initialization.Setup(x => x.ValidateCurrentApprovedEvidenceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingBookInitializationEvidenceValidationDto { IsValid = true, InitializationId = Guid.NewGuid(), Version = 1,
                EvidenceFingerprint = new string('A', 64), ReconciliationFingerprint = new string('B', 64) });
        return new AccountingBookApplicabilityService(db, user.Object, workflow.Object, audit, initialization.Object);
    }

    private static Mock<ICurrentUserService> User(Guid tenant, Guid actor)
    {
        var user = new Mock<ICurrentUserService>(); user.SetupGet(x => x.TenantId).Returns(tenant);
        user.SetupGet(x => x.UserId).Returns(actor.ToString()); user.SetupGet(x => x.UserName).Returns("c6.sql");
        user.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>()); return user;
    }

    private static async Task<Seeded> SeedAsync(ApplicationDbContext db, DisposableDatabase database)
    {
        var tenant = database.TenantId;
        var primary = Book(tenant, "IFRS", true, AccountingBookType.PrimaryFull); var second = Book(tenant, "LOCAL", false, AccountingBookType.ParallelFull);
        var year = new FiscalYear { TenantId = tenant, FiscalYearName = "FY26", FiscalYearCode = "2026", Year = 2026, FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), TotalDays = 365, NumberOfPeriods = 12, Status = "Open", IsActive = true };
        var period = new FiscalPeriod { TenantId = tenant, FiscalYearId = year.Id, PeriodName = "September", PeriodCode = "2026-09", PeriodNumber = 9,
            StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30), PeriodDays = 30, PeriodStatus = "Open", IsOpen = true };
        var debit = Account(tenant, "1000", AccountType.Asset); var credit = Account(tenant, "2000", AccountType.Liability);
        database.DebitId = debit.Id; database.CreditId = credit.Id;
        var pa = Classification(tenant, primary.Id, "ASSET", AccountType.Asset); var pl = Classification(tenant, primary.Id, "LIABILITY", AccountType.Liability);
        var sa = Classification(tenant, second.Id, "ASSET", AccountType.Asset); var sl = Classification(tenant, second.Id, "LIABILITY", AccountType.Liability);
        db.AddRange(Tenant(tenant), new FinanceSettings { TenantId = tenant, BaseCurrency = "GHS", CoaType = "Segmented", AccountSeparator = "-" }, primary, second,
            year, period, new ModuleDefinition { TenantId = tenant, ModuleCode = "INV", ModuleName = "Inventory", IsActive = true },
            new AccountingBookPeriod { TenantId = tenant, AccountingBookId = primary.Id, FiscalPeriodId = period.Id, PeriodStatus = AccountingBookPeriodStatus.Open },
            new AccountingBookPeriod { TenantId = tenant, AccountingBookId = second.Id, FiscalPeriodId = period.Id, PeriodStatus = AccountingBookPeriodStatus.Open },
            debit, credit, pa, pl, sa, sl,
            Mapping(tenant, primary.Id, debit.Id, pa.Id), Mapping(tenant, primary.Id, credit.Id, pl.Id), Mapping(tenant, second.Id, debit.Id, sa.Id));
        await db.SaveChangesAsync();
        var warehouse = new Warehouse { TenantId = tenant, Code = "C7-OWNER", Name = "C7 owner transaction", IsActive = true };
        var owner = new ApplicationUser
        {
            Id = database.MakerId, TenantId = tenant, FirstName = "C7", LastName = "Owner", UserName = $"c7.owner.{database.MakerId:N}",
            NormalizedUserName = $"C7.OWNER.{database.MakerId:N}", Email = $"{database.MakerId:N}@example.test",
            NormalizedEmail = $"{database.MakerId:N}@EXAMPLE.TEST", IsActive = true
        };
        db.AddRange(warehouse, owner);
        await db.SaveChangesAsync();
        var disposal = new InventoryDisposalCase
        {
            TenantId = tenant, DisposalNumber = "C7-OWNER-001", WarehouseId = warehouse.Id,
            Status = InventoryDisposalStatus.Identified, Method = InventoryDisposalMethod.WriteOff,
            Reason = "C7 atomic rollback test", IdentificationDetails = "tracked owner effect sentinel",
            RequestedById = owner.Id, RequestedAtUtc = DateTime.UtcNow, TotalQuantity = 1m, TotalValue = 100m,
            IdempotencyKey = "C7-OWNER-001", PayloadHash = new string('A', 64), CorrelationId = "C7-OWNER-001",
            IntegrityHash = new string('B', 64)
        };
        db.InventoryDisposalCases.Add(disposal);
        await db.SaveChangesAsync();
        var maker = Applicability(db, tenant, database.MakerId, governedWorkflow: true);
        var draft = await maker.CreateDraftAsync(new SaveAccountingBookApplicabilityPolicyDto { PolicyCode = "C6_SQL", Name = "C6 SQL policy",
            EffectiveFrom = new DateTime(2026, 1, 1), Reason = "C6 migrated SQL authority", Rules = [new SaveAccountingBookApplicabilityRuleDto {
                RuleCode = "INV_POST", Priority = 100, OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST",
                AccountingBookIds = [primary.Id, second.Id] }, new SaveAccountingBookApplicabilityRuleDto {
                RuleCode = "INV_DISPOSAL_RECOVERY", Priority = 100, OriginatingModuleCode = "INV", SourceDocumentType = "INVENTORY_DISPOSAL", PostingAction = "RECOVER",
                AccountingBookIds = [primary.Id] }, new SaveAccountingBookApplicabilityRuleDto {
                RuleCode = "INV_STOCK_DISPOSE", Priority = 100, OriginatingModuleCode = "INV", SourceDocumentType = "STOCK_ADJUSTMENT", PostingAction = "DISPOSE_VALUE",
                AccountingBookIds = [primary.Id, second.Id] }] });
        var submitted = await maker.SubmitAsync(draft.Id, new DecideAccountingBookApplicabilityPolicyDto { Reason = "submit", RowVersion = draft.RowVersion });
        var checker = Applicability(db, tenant, database.CheckerId, governedWorkflow: true);
        await checker.ApproveAsync(draft.Id, new DecideAccountingBookApplicabilityPolicyDto { Reason = "approve", RowVersion = submitted.RowVersion });
        var selection = await maker.ResolveAsync(new ResolveAccountingBookApplicabilityDto { EffectiveDate = database.EventDate,
            OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST" });
        database.CalculationInputHash = selection.CalculationInputHash; database.SelectionFingerprint = selection.SelectionFingerprint;
        return new Seeded(primary.Id, second.Id, debit.Id, credit.Id, sa.Id, sl.Id, disposal.Id, selection);
    }

    private static async Task RepairSecondBookAsync(DisposableDatabase database, Seeded seeded)
    {
        await using var repair = database.Context();
        foreach (var account in new[] { seeded.DebitId, seeded.CreditId })
            if (!await repair.AccountAccountingBooks.AnyAsync(x => x.AccountId == account && x.AccountingBookId == seeded.SecondBookId))
                repair.AccountAccountingBooks.Add(new AccountAccountingBook { TenantId = database.TenantId, AccountId = account,
                    AccountingBookId = seeded.SecondBookId, AccountClassificationId = account == seeded.DebitId
                        ? seeded.SecondAssetClassId : seeded.SecondLiabilityClassId, IsEnabled = true });
        await repair.SaveChangesAsync();
    }

    private static CreateAccountingEventDto Request(DisposableDatabase database, string idempotencyKey = "EVENT-1", Guid? sourceDocumentId = null) => new() { SelectionIdempotencyKey = idempotencyKey,
        ExpectedCalculationInputHash = database.CalculationInputHash, ExpectedSelectionFingerprint = database.SelectionFingerprint, PostingRequest = new FinancePostingRequestV2Dto {
            SourceModule = "INV", OriginModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT", SourceDocumentId = sourceDocumentId ?? database.SourceId,
            SourceDocumentTenantId = database.TenantId, PostingAction = "POST", PostingDate = database.EventDate, FunctionalCurrencyCode = "GHS",
            Description = "C6 SQL event", Lines = [new FinancePostingLineDto { AccountId = database.DebitId, DebitAmount = 100m, LineNumber = 1 },
                new FinancePostingLineDto { AccountId = database.CreditId, CreditAmount = 100m, LineNumber = 2 }] } };

    private static Tenant Tenant(Guid id) => new() { Id = id, Code = $"C6{id:N}"[..12].ToUpperInvariant(), Name = "C6 SQL", Status = TenantStatus.Active, BaseCurrency = "GHS" };
    private static AccountingBook Book(Guid tenant, string code, bool primary, AccountingBookType type) => new() { TenantId = tenant, Code = code, Name = code,
        Purpose = "Reporting", BookType = type, LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
        IsDefault = primary, IsActive = true, AllowsPosting = true };
    private static Account Account(Guid tenant, string code, AccountType type) => new() { TenantId = tenant, AccountCode = code, AccountNumber = code,
        AccountName = code, AccountType = type, CurrencyCode = "GHS", Status = AccountStatus.Active, AllowDirectPosting = true };
    private static AccountClassification Classification(Guid tenant, Guid book, string code, AccountType type) => new() { TenantId = tenant, AccountingBookId = book,
        Code = code, Name = code, CoreAccountType = type, Status = AccountClassificationStatus.Active, IsPostingClassification = true };
    private static AccountAccountingBook Mapping(Guid tenant, Guid book, Guid account, Guid classification) => new() { TenantId = tenant, AccountingBookId = book,
        AccountId = account, AccountClassificationId = classification, IsEnabled = true };
    private sealed record Seeded(Guid PrimaryBookId, Guid SecondBookId, Guid DebitId, Guid CreditId, Guid SecondAssetClassId,
        Guid SecondLiabilityClassId, Guid DisposalCaseId, AccountingBookSelectionDto Selection);

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER"))) Skip = "Set RHEMA_TEST_SQLSERVER to run exact-prefix disposable C6 service gates."; }
    }

    private sealed class EventLockCompetition
    {
        private readonly TaskCompletionSource _ownerAcquired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _contenderAtLock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _ownerObservations; private int _contenderObservations;
        public DbCommandInterceptor Owner => new EventLockInterceptor(this, owner: true);
        public DbCommandInterceptor Contender => new EventLockInterceptor(this, owner: false);
        public void AssertCompeted()
        {
            Volatile.Read(ref _ownerObservations).Should().Be(1, "the owner must pass the event application-lock command inside its transaction");
            Volatile.Read(ref _contenderObservations).Should().Be(1, "the contender must reach the same lock while the owner transaction is still held");
        }

        private sealed class EventLockInterceptor(EventLockCompetition race, bool owner) : DbCommandInterceptor
        {
            public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
                InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (!owner && IsEventLock(command))
                {
                    await race._ownerAcquired.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                    Interlocked.Increment(ref race._contenderObservations);
                    race._contenderAtLock.TrySetResult();
                }
                return result;
            }

            public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
                CancellationToken cancellationToken = default)
            {
                if (owner && IsEventLock(command))
                {
                    Interlocked.Increment(ref race._ownerObservations);
                    race._ownerAcquired.TrySetResult();
                    await race._contenderAtLock.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                }
                return result;
            }

            private static bool IsEventLock(DbCommand command) => command.CommandText.Contains("sp_getapplock", StringComparison.OrdinalIgnoreCase)
                && command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value?.ToString()?.StartsWith("FIN:C6:EVENT:", StringComparison.Ordinal) == true);
        }
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private static readonly Regex SafeName = new("^RHEMAERP_GL_REHEARSAL_C6_[A-Z0-9_]{1,64}$", RegexOptions.CultureInvariant);
        private readonly string _name; private readonly string _master; private readonly string _connection;
        public Guid TenantId { get; } = Guid.NewGuid(); public Guid MakerId { get; } = Guid.NewGuid(); public Guid CheckerId { get; } = Guid.NewGuid();
        public Guid OtherCheckerId { get; } = Guid.NewGuid();
        public Guid SourceId { get; } = Guid.NewGuid(); public Guid DebitId { get; set; } public Guid CreditId { get; set; }
        public string CalculationInputHash { get; set; } = new('A', 64); public string SelectionFingerprint { get; set; } = new('B', 64);
        public DateTime EventDate { get; } = new(2026, 9, 7);
        private DisposableDatabase(string name, string master, string connection) => (_name, _master, _connection) = (name, master, connection);
        public static async Task<DisposableDatabase> CreateAsync() { var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")!;
            var name = $"RHEMAERP_GL_REHEARSAL_C6_{Guid.NewGuid():N}".ToUpperInvariant(); if (!SafeName.IsMatch(name)) throw new InvalidOperationException("C6 prefix refusal.");
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var db = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString); await db.MasterAsync($"CREATE DATABASE [{name}]");
            await using var migrated = db.Context(); migrated.Database.SetCommandTimeout(TimeSpan.FromMinutes(3)); await migrated.Database.MigrateAsync();
            return db; }
        public ApplicationDbContext Context(DbCommandInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection,
                options => options.EnableRetryOnFailure().CommandTimeout(180));
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            return new ApplicationDbContext(builder.Options);
        }
        private async Task MasterAsync(string sql) { await using var c = new SqlConnection(_master); await c.OpenAsync(); await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 }; await cmd.ExecuteNonQueryAsync(); }
        public async ValueTask DisposeAsync() { if (SafeName.IsMatch(_name)) await MasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END"); }
    }
}
