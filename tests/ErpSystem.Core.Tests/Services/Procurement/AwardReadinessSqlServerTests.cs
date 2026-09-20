using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

/// <summary>Runs the actual readiness migration and trigger in a new disposable SQL Server database.</summary>
public sealed class AwardReadinessSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task PettyAuthorityMigrationAdmitsExactNullableRouteAndRetainsSourceGuards()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.ApplyAsync(new AlignLegacyAwardReadinessWithLockedCurrentEvaluations());
        var source = await SourceFixture.SeedAsync(database);
        var methodRule = Guid.NewGuid();
        var definition = Guid.NewGuid();
        var workflow = Guid.NewGuid();
        await database.ExecuteAsync($"""
            UPDATE dbo.Tenders SET Status='Approved' WHERE Id='{source.TenderId}';
            UPDATE dbo.TenderBids SET Status='Submitted' WHERE Id='{source.BidId}';
            INSERT dbo.WorkflowInstances VALUES('{workflow}','{definition}','{source.TenderId}','{source.TenantId}',0,2);
            INSERT dbo.ProcurementExceptionalSourcingControls
            VALUES(NEWID(),'{source.TenderId}','{source.TenantId}',0,4,5,'{source.BidId}','{methodRule}',
                'METHOD-PETTY',NULL,'','{definition}','{workflow}');
            """);
        async Task Append(int method = 5, string prefix = "", Guid? route = null)
        {
            var authority = JsonSerializer.Serialize(new
            {
                methodRuleId = methodRule, methodRuleCode = "METHOD-PETTY", authorityRouteId = route,
                authorityRouteReference = "", workflowDefinitionId = definition, workflowInstanceId = workflow,
                workflowStatus = "Completed", approvalReference = "workflow-test",
                approvedByUserId = source.ApproverId, approvalActorUserIds = new[] { source.ApproverId }
            });
            var snapshot = JsonSerializer.Serialize(new { subjectType = "TenderBid", subjectIds = new[] { source.BidId }, businessPartnerIds = new[] { source.SupplierId } });
            var prerequisites = JsonSerializer.Serialize(Enumerable.Range(0, 9).Select(group => new { group, status = 0, items = new[] { new { status = 0 } } }));
            await database.ExecuteAsync(prefix + """
                INSERT dbo.ProcurementAwardReadinessDecisions
                (Id,TenantId,SourceType,SourceId,SourceReference,Method,DecisionSequence,Status,RecommendationSubjectType,
                 RecommendedSubjectIdsJson,RecommendedBusinessPartnerIdsJson,RecommendationSnapshotJson,EvaluationLineageJson,
                 SupplierLineageJson,PrequalificationLineageJson,VerificationLineageJson,AuthorityLineageJson,EvidenceLineageJson,
                 PrerequisiteSnapshotJson,TimelineSnapshotJson,BlockedReasonsJson,SourceIntegrityHash,IntegrityHash,IdempotencyKey,
                 CorrelationId,EvaluatedAtUtc,EvaluatedByUserId,EvaluatedByName,CreatedAt,CreatedById,IsDeleted,
                 MethodRuleId,MethodRuleCode,AuthorityRouteId,AuthorityRouteReference,WorkflowDefinitionId,WorkflowInstanceId)
                VALUES(NEWID(),@tenant,2,@source,'TND-SQL-READY',@method,
                 (SELECT ISNULL(MAX(DecisionSequence),0)+1 FROM dbo.ProcurementAwardReadinessDecisions),1,'TenderBid',
                 @subjects,@suppliers,@snapshot,'[]','[]','[]','[]',@authority,'[]',@prerequisites,'[]','[]',
                 REPLICATE('A',64),REPLICATE('B',64),CONVERT(varchar(36),NEWID()),'petty-sql',SYSUTCDATETIME(),
                 @approver,'Independent test approver',SYSUTCDATETIME(),@approver,0,@rule,'METHOD-PETTY',@route,'',@definition,@workflow);
                IF @@TRANCOUNT > 0 ROLLBACK;
                """, new SqlParameter("@tenant", source.TenantId), new SqlParameter("@source", source.TenderId),
                new SqlParameter("@method", method), new SqlParameter("@subjects", JsonSerializer.Serialize(new[] { source.BidId })),
                new SqlParameter("@suppliers", JsonSerializer.Serialize(new[] { source.SupplierId })),
                new SqlParameter("@snapshot", snapshot), new SqlParameter("@authority", authority),
                new SqlParameter("@prerequisites", prerequisites), new SqlParameter("@approver", source.ApproverId),
                new SqlParameter("@rule", methodRule), new SqlParameter("@route", System.Data.SqlDbType.UniqueIdentifier) { Value = (object?)route ?? DBNull.Value },
                new SqlParameter("@definition", definition), new SqlParameter("@workflow", workflow));
        }
        (await Assert.ThrowsAsync<SqlException>(() => Append())).Number.Should().Be(51407);
        await database.ApplyAsync(new AlignPettyPurchaseAwardReadinessAuthority());
        var guard = await database.ScalarAsync<string>(ReadTriggerSql);
        await database.ApplyAsync(new AlignPettyPurchaseAwardReadinessAuthority());
        (await database.ScalarAsync<string>(ReadTriggerSql)).Should().Be(guard);
        await Append();
        foreach (var mutation in new[]
        {
            "UPDATE dbo.ProcurementExceptionalSourcingControls SET Status=2;",
            "UPDATE dbo.ProcurementExceptionalSourcingControls SET RecommendedBidId=NEWID();",
            "UPDATE dbo.ProcurementExceptionalSourcingControls SET TenantId=NEWID();",
            "UPDATE dbo.ProcurementExceptionalSourcingControls SET MethodRuleId=NEWID();",
            "UPDATE dbo.TenderBids SET BusinessPartnerId=NEWID();",
            "UPDATE dbo.WorkflowInstances SET Status=1;",
            "UPDATE dbo.WorkflowInstances SET EntityId=NEWID();"
        })
            await Assert.ThrowsAsync<SqlException>(() => Append(prefix: "BEGIN TRAN; " + mutation));
        await Assert.ThrowsAsync<SqlException>(() => Append(method: 4, prefix: "BEGIN TRAN; UPDATE dbo.ProcurementExceptionalSourcingControls SET Method=4;"));
        await Assert.ThrowsAsync<SqlException>(() => Append(route: Guid.NewGuid()));
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ProcurementAwardReadinessDecisions;")).Should().Be(1);
        (await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync("UPDATE dbo.ProcurementAwardReadinessDecisions SET Status=0;"))).Number.Should().Be(51400);
    }

    [Fact]
    public void SqlGeneratorLineEndingsAreNormalizedInsideEveryComparedFragment()
    {
        var operations = new AlignLegacyAwardReadinessWithLockedCurrentEvaluations().UpOperations;
        var source = operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Single().Sql;
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=SqlGenerationOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        var generated = string.Join(string.Empty,
            context.GetService<IMigrationsSqlGenerator>().Generate(operations).Select(command => command.CommandText));

        foreach (var name in new[] { "original", "replacement", "prefix", "preparation" })
        {
            var pattern = $@"DECLARE @{name} nvarchar\(max\) = N'(?<fragment>(?:''|[^'])*)';";
            var originalMatch = Regex.Match(source, pattern, RegexOptions.Singleline);
            var generatedMatch = Regex.Match(generated, pattern, RegexOptions.Singleline);
            originalMatch.Success.Should().BeTrue(name);
            generatedMatch.Success.Should().BeTrue(name);
            var originalFragment = originalMatch.Groups["fragment"].Value;
            var generatedFragment = generatedMatch.Groups["fragment"].Value;
            originalFragment.Should().NotContain("\r", "C# Quote normalizes fragments before SQL generation");
            if (originalFragment.Contains('\n'))
                generatedFragment.Should().Contain(Environment.NewLine,
                    "the real SQL Server generator rewrites literal line endings, including CRLF on Windows");
            // EF also removes empty separator lines while generating SQL batches.
            // This is a source-versus-generator assertion only: production compares
            // the complete actual generated fragments without relaxing their content.
            static string ContentLines(string value) => string.Join("\n", value
                .Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line)));
            ContentLines(generatedFragment).Should().Be(ContentLines(originalFragment));
            generated.Should().Contain($"SET @{name} = REPLACE(@{name}, CHAR(13) + CHAR(10), CHAR(10));",
                "fragment normalization must execute after the SQL generator has rewritten literals");
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task MigrationAdmitsThreeLockedCurrentVotersAndRetainsRecalledHistoricalAttempt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var source = await SourceFixture.SeedAsync(database);
        var before = await database.ScalarAsync<string>(SourceHistorySql);

        var oldFailure = await Assert.ThrowsAsync<SqlException>(() => source.AppendReadyAsync(database));
        oldFailure.Number.Should().Be(51407, "the original production trigger reproduces the UAT failure");

        await database.ApplyAsync(new AlignLegacyAwardReadinessWithLockedCurrentEvaluations());
        var correctedGuard = await database.ScalarAsync<string>(ReadTriggerSql);
        await database.ApplyAsync(new AlignLegacyAwardReadinessWithLockedCurrentEvaluations());
        (await database.ScalarAsync<string>(ReadTriggerSql)).Should().Be(correctedGuard,
            "reapplying the exact correction is idempotent and must not duplicate or alter guards");
        await source.AppendReadyAsync(database);

        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ProcurementAwardReadinessDecisions;")).Should().Be(1);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.TenderEvaluations;")).Should().Be(4);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ProcurementEvaluationScoreSheets;")).Should().Be(4);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ProcurementEvaluationScoreRecalls WHERE Status=1;")).Should().Be(1);
        (await database.ScalarAsync<string>(SourceHistorySql)).Should().Be(before,
            "migration and readiness INSERT must not rewrite old evaluations, locks, or recall evidence");

        var immutable = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
            "UPDATE dbo.ProcurementAwardReadinessDecisions SET Status=0;"));
        immutable.Number.Should().Be(51400);

        var refusedDown = await Assert.ThrowsAsync<SqlException>(() => database.ApplyAsync(
            new AlignLegacyAwardReadinessWithLockedCurrentEvaluations(), down: true));
        refusedDown.Number.Should().Be(51419);
        (await database.ScalarAsync<string>(ReadTriggerSql)).Should().Be(correctedGuard);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ProcurementAwardReadinessDecisions;")).Should().Be(1);
        (await database.ScalarAsync<string>(SourceHistorySql)).Should().Be(before);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task MigrationRefusesToOverwriteDriftedBaselineTrigger()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = await database.ScalarAsync<string>(ReadTriggerSql);
        const string expected = "AND e.IsRecommended = 1 AND e.Status = 'Approved') <> 1";
        original.Should().Contain(expected);
        var drifted = "ALTER " + original[original.IndexOf("TRIGGER", StringComparison.OrdinalIgnoreCase)..]
            .Replace(expected, "AND e.IsRecommended = 1 AND e.Status = 'Approved') <> 2", StringComparison.Ordinal);
        await database.ExecuteAsync(drifted);
        var retainedDrift = await database.ScalarAsync<string>(ReadTriggerSql);

        var failure = await Assert.ThrowsAsync<SqlException>(() => database.ApplyAsync(
            new AlignLegacyAwardReadinessWithLockedCurrentEvaluations()));

        failure.Number.Should().Be(51419);
        (await database.ScalarAsync<string>(ReadTriggerSql)).Should().Be(retainedDrift,
            "baseline drift must be reviewed, not silently replaced");
    }

    [SqlServerTheory]
    [Trait("Category", "SqlServerIntegration")]
    [InlineData("same-length-fragment")]
    [InlineData("duplicate-anchor")]
    public async Task ReapplicationRefusesAlteredInstalledGuardWithoutOverwritingRetainedDecision(string scenario)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.ApplyAsync(new AlignLegacyAwardReadinessWithLockedCurrentEvaluations());
        var source = await SourceFixture.SeedAsync(database);
        await source.AppendReadyAsync(database);
        var corrected = await database.ScalarAsync<string>(ReadTriggerSql);
        var drifted = "ALTER " + corrected[corrected.IndexOf("TRIGGER", StringComparison.OrdinalIgnoreCase)..];
        if (scenario == "same-length-fragment")
        {
            const string expected = "e.Status NOT IN ('Submitted', 'Approved')";
            corrected.Should().Contain(expected);
            drifted = drifted.Replace(expected, "e.Status NOT IN ('Submitted', 'Rejected')", StringComparison.Ordinal);
            drifted.Length.Should().Be(("ALTER " + corrected[corrected.IndexOf("TRIGGER", StringComparison.OrdinalIgnoreCase)..]).Length);
        }
        else
        {
            const string marker = "OR (i.SourceType = 1 AND NOT EXISTS (";
            var anchorStart = corrected.IndexOf(marker, StringComparison.Ordinal);
            anchorStart.Should().BeGreaterThanOrEqualTo(0);
            drifted += "\n/* Duplicate installed-guard anchor: " + corrected.Substring(anchorStart, 64) + " */";
        }
        await database.ExecuteAsync(drifted);
        var retainedDrift = await database.ScalarAsync<string>(ReadTriggerSql);

        var failure = await Assert.ThrowsAsync<SqlException>(() => database.ApplyAsync(
            new AlignLegacyAwardReadinessWithLockedCurrentEvaluations()));

        failure.Number.Should().Be(51419, scenario);
        (await database.ScalarAsync<string>(ReadTriggerSql)).Should().Be(retainedDrift);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ProcurementAwardReadinessDecisions;")).Should().Be(1);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task EqualTimestampGuidTieSelectsTheSameCurrentProjectionAsDotNet()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.ApplyAsync(new AlignLegacyAwardReadinessWithLockedCurrentEvaluations());
        var source = await SourceFixture.SeedAsync(database);
        await source.ConfigureGuidTieAsync(database);

        var nativeSqlWinner = await database.ScalarAsync<Guid>(
            $"SELECT TOP(1) Id FROM dbo.TenderEvaluations WHERE TenderEvaluatorId='{source.EvaluatorIds[0]}' ORDER BY Id DESC;");
        nativeSqlWinner.Should().NotBe(source.CurrentEvaluationIds[0],
            "this fixture must expose SQL uniqueidentifier ordering versus .NET Guid ordering");
        var sameTimestamp = new DateTime(2026, 9, 6, 2, 1, 0, DateTimeKind.Utc);
        var expected = ProcurementTenderEvaluationProjectionPolicy.SelectCurrent(new[]
        {
            new TenderEvaluation { Id = source.PriorEvaluationId, TenderBidId = source.BidId,
                TenderEvaluatorId = source.EvaluatorIds[0], SubmittedDate = sameTimestamp, CreatedAt = sameTimestamp },
            new TenderEvaluation { Id = source.CurrentEvaluationIds[0], TenderBidId = source.BidId,
                TenderEvaluatorId = source.EvaluatorIds[0], SubmittedDate = sameTimestamp, CreatedAt = sameTimestamp }
        });
        expected.Should().ContainSingle().Which.Id.Should().Be(source.CurrentEvaluationIds[0]);

        await source.AppendReadyAsync(database);

        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ProcurementAwardReadinessDecisions;")).Should().Be(1);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.TenderEvaluations;")).Should().Be(4);
    }

    [SqlServerTheory]
    [Trait("Category", "SqlServerIntegration")]
    [InlineData("newer-draft", 51407)]
    [InlineData("wrong-recommended-bid", 51407)]
    [InlineData("foreign-evaluation-tenant", 51407)]
    [InlineData("foreign-evaluator-tenant", 51407)]
    [InlineData("wrong-source", 51404)]
    [InlineData("missing-lock", 51407)]
    [InlineData("tampered-lock-score", 51407)]
    [InlineData("fractional-tampered-lock-score", 51407)]
    [InlineData("tampered-lock-evaluation", 51407)]
    [InlineData("pending-current-recall", 51407)]
    [InlineData("approved-current-recall", 51407)]
    [InlineData("unapproved-prior-recall", 51407)]
    [InlineData("wrong-prior-authorized-attempt", 51407)]
    [InlineData("wrong-authority", 51409)]
    public async Task SqlGuardRejectsInvalidCurrentLineageOrIndependentAuthority(string scenario, int expectedError)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.ApplyAsync(new AlignLegacyAwardReadinessWithLockedCurrentEvaluations());
        var source = await SourceFixture.SeedAsync(database);
        await source.CorruptScenarioAsync(database, scenario);

        var failure = await Assert.ThrowsAsync<SqlException>(() => source.AppendReadyAsync(database));

        failure.Number.Should().Be(expectedError, scenario);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ProcurementAwardReadinessDecisions;")).Should().Be(0,
            "a rejected INSERT must not retain a Ready decision");
    }

    private sealed class SourceFixture
    {
        private static readonly DateTime SubmittedAt = new(2026, 9, 6, 2, 0, 0, DateTimeKind.Utc);
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid TenderId { get; } = Guid.NewGuid();
        public Guid BidId { get; } = Guid.NewGuid();
        public Guid SupplierId { get; } = Guid.NewGuid();
        public Guid ApproverId { get; } = Guid.NewGuid();
        public Guid CommitteeId { get; } = Guid.NewGuid();
        public Guid MeetingId { get; } = Guid.NewGuid();
        public Guid PriorSheetId { get; private set; }
        public Guid PriorEvaluationId { get; private set; }
        public Guid RecallId { get; } = Guid.NewGuid();
        public Guid[] EvaluatorIds { get; } = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        public Guid[] UserIds { get; } = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        public Guid[] AppointmentIds { get; } = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        public Guid[] CurrentEvaluationIds { get; } = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        public Guid[] CurrentSheetIds { get; } = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        private Guid? _recommendedBidOverride;
        private Guid? _sourceOverride;
        private Guid? _authorityOverride;

        public static async Task<SourceFixture> SeedAsync(TestDatabase database)
        {
            var fixture = new SourceFixture();
            await database.ExecuteAsync(
                """
                INSERT dbo.Tenants(Id) VALUES(@tenant);
                INSERT dbo.Users(Id,TenantId,IsActive) VALUES(@approver,@tenant,1);
                INSERT dbo.Tenders(Id,TenantId,IsDeleted,TenderNumber,Status)
                VALUES(@tender,@tenant,0,'TND-SQL-READY','Evaluated');
                INSERT dbo.TenderBids(Id,TenantId,IsDeleted,TenderId,BusinessPartnerId,Status)
                VALUES(@bid,@tenant,0,@tender,@supplier,'Evaluated');
                INSERT dbo.ProcurementEvaluationCommitteeControls(Id,TenantId,IsDeleted,SourceType,SourceId,Version,Status)
                VALUES(@committee,@tenant,0,0,@tender,1,1);
                INSERT dbo.ProcurementEvaluationMeetings(Id,TenantId,IsDeleted,CommitteeControlId,Status)
                VALUES(@meeting,@tenant,0,@committee,1);
                """, fixture.Parameters());
            for (var index = 0; index < 3; index++)
            {
                await database.ExecuteAsync(
                    """
                    INSERT dbo.Users(Id,TenantId,IsActive) VALUES(@user,@tenant,1);
                    INSERT dbo.TenderEvaluators(Id,TenantId,IsDeleted,TenderId,UserId)
                    VALUES(@evaluator,@tenant,0,@tender,@user);
                    INSERT dbo.ProcurementEvaluationCommitteeAppointments(Id,TenantId,IsDeleted,CommitteeControlId,UserId,Status)
                    VALUES(@appointment,@tenant,0,@committee,@user,1);
                    """, fixture.Parameters(
                        new SqlParameter("@user", fixture.UserIds[index]),
                        new SqlParameter("@evaluator", fixture.EvaluatorIds[index]),
                        new SqlParameter("@appointment", fixture.AppointmentIds[index])));
                if (index == 0)
                {
                    fixture.PriorSheetId = Guid.NewGuid();
                    fixture.PriorEvaluationId = Guid.NewGuid();
                    await fixture.SeedEvaluationAsync(database, index, fixture.PriorEvaluationId, fixture.PriorSheetId, 1, 1, SubmittedAt);
                }
                await fixture.SeedEvaluationAsync(database, index, fixture.CurrentEvaluationIds[index],
                    fixture.CurrentSheetIds[index], index == 0 ? 2 : 1, 0, SubmittedAt.AddMinutes(1));
            }
            await database.ExecuteAsync(
                """
                INSERT dbo.ProcurementEvaluationScoreRecalls
                    (Id,TenantId,IsDeleted,ScoreSheetId,Status,AuthorizedNewAttempt,RequestedByUserId,DecidedByUserId,DecidedAtUtc)
                VALUES(@recall,@tenant,0,@prior,1,2,@requester,@approver,@time);
                """, fixture.Parameters(new SqlParameter("@recall", fixture.RecallId),
                    new SqlParameter("@prior", fixture.PriorSheetId),
                    new SqlParameter("@requester", fixture.UserIds[0]), new SqlParameter("@time", SubmittedAt.AddSeconds(30))));
            return fixture;
        }

        public async Task ConfigureGuidTieAsync(TestDatabase database)
        {
            var currentId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
            var priorId = Guid.Parse("00000000-0000-0000-0000-ffffffffffff");
            await database.ExecuteAsync(
                """
                UPDATE dbo.TenderEvaluations SET Id=@priorNew,SubmittedDate=@time,EvaluationDate=@time,CreatedAt=@time WHERE Id=@priorOld;
                UPDATE dbo.TenderEvaluations SET Id=@currentNew WHERE Id=@currentOld;
                UPDATE dbo.ProcurementEvaluationScoreSheets SET SubmittedAtUtc=@time,
                    ScoreSnapshotJson=JSON_MODIFY(JSON_MODIFY(ScoreSnapshotJson,'$.evaluationId',LOWER(CONVERT(varchar(36),@priorNew))),
                        '$.submittedAtUtc','2026-09-06T02:01:00Z') WHERE Id=@priorSheet;
                UPDATE dbo.ProcurementEvaluationScoreSheets SET ScoreSnapshotJson=JSON_MODIFY(ScoreSnapshotJson,
                    '$.evaluationId',LOWER(CONVERT(varchar(36),@currentNew))) WHERE Id=@currentSheet;
                UPDATE dbo.ProcurementEvaluationScoreRecalls SET DecidedAtUtc=@time WHERE Id=@recall;
                """, Parameters(new SqlParameter("@priorNew", priorId), new SqlParameter("@priorOld", PriorEvaluationId),
                    new SqlParameter("@currentNew", currentId), new SqlParameter("@currentOld", CurrentEvaluationIds[0]),
                    new SqlParameter("@priorSheet", PriorSheetId), new SqlParameter("@currentSheet", CurrentSheetIds[0]),
                    new SqlParameter("@time", SubmittedAt.AddMinutes(1)), new SqlParameter("@recall", RecallId)));
            PriorEvaluationId = priorId;
            CurrentEvaluationIds[0] = currentId;
        }

        private async Task SeedEvaluationAsync(TestDatabase database, int voter, Guid evaluationId, Guid sheetId,
            int attempt, int sheetStatus, DateTime timestamp)
        {
            var evaluation = new TenderEvaluation
            {
                Id = evaluationId, TenantId = TenantId, TenderBidId = BidId,
                TenderEvaluatorId = EvaluatorIds[voter], Status = "Submitted", TotalScore = 90m,
                IsRecommended = true, EvaluationDate = timestamp, SubmittedDate = timestamp,
                CreatedAt = timestamp, TenderEvaluator = new TenderEvaluator { UserId = UserIds[voter] }
            };
            var snapshot = TenderEvaluationService.BuildLegacyScoreSnapshot(evaluation, timestamp);
            await database.ExecuteAsync(
                """
                INSERT dbo.TenderEvaluations(Id,TenantId,IsDeleted,TenderBidId,TenderEvaluatorId,Status,IsRecommended,
                    TotalScore,EvaluationDate,SubmittedDate,CreatedAt)
                VALUES(@evaluation,@tenant,0,@bid,@evaluator,'Submitted',1,90,@time,@time,@time);
                INSERT dbo.ProcurementEvaluationScoreSheets(Id,TenantId,IsDeleted,CommitteeControlId,MeetingId,
                    AppointmentId,Phase,ScoreSubjectType,ScoreSubjectId,Attempt,Status,SubmittedAtUtc,SubmittedByUserId,
                    ScoreSnapshotJson,SignatureReference,EvidenceReference,IntegrityHash)
                VALUES(@sheet,@tenant,0,@committee,@meeting,@appointment,2,'TenderEvaluation',@bid,@attempt,
                    @status,@time,@user,@snapshot,'test-signature','test-evidence',REPLICATE('A',64));
                """, Parameters(new SqlParameter("@evaluation", evaluationId), new SqlParameter("@evaluator", EvaluatorIds[voter]),
                    new SqlParameter("@time", timestamp), new SqlParameter("@sheet", sheetId),
                    new SqlParameter("@appointment", AppointmentIds[voter]), new SqlParameter("@attempt", attempt),
                    new SqlParameter("@status", sheetStatus), new SqlParameter("@user", UserIds[voter]),
                    new SqlParameter("@snapshot", snapshot)));
        }

        public async Task CorruptScenarioAsync(TestDatabase database, string scenario)
        {
            if (scenario == "wrong-source") { _sourceOverride = Guid.NewGuid(); return; }
            if (scenario == "wrong-authority") { _authorityOverride = UserIds[0]; return; }
            if (scenario == "wrong-recommended-bid")
            {
                _recommendedBidOverride = Guid.NewGuid();
                await database.ExecuteAsync(
                    "INSERT dbo.TenderBids(Id,TenantId,IsDeleted,TenderId,BusinessPartnerId,Status) VALUES(@other,@tenant,0,@tender,@supplier,'Evaluated');",
                    Parameters(new SqlParameter("@other", _recommendedBidOverride.Value)));
                return;
            }
            var sql = scenario switch
            {
                "newer-draft" => "INSERT dbo.TenderEvaluations(Id,TenantId,IsDeleted,TenderBidId,TenderEvaluatorId,Status,IsRecommended,TotalScore,EvaluationDate,CreatedAt) VALUES(NEWID(),@tenant,0,@bid,@evaluator,'Draft',0,0,'2026-09-06T02:05:00','2026-09-06T02:05:00');",
                "foreign-evaluation-tenant" => "UPDATE dbo.TenderEvaluations SET TenantId=NEWID() WHERE Id=@evaluation;",
                "foreign-evaluator-tenant" => "UPDATE dbo.TenderEvaluators SET TenantId=NEWID() WHERE Id=@evaluator;",
                "missing-lock" => "DELETE dbo.ProcurementEvaluationScoreSheets WHERE Id=@sheet;",
                "tampered-lock-score" => "UPDATE dbo.ProcurementEvaluationScoreSheets SET ScoreSnapshotJson=JSON_MODIFY(ScoreSnapshotJson,'$.TotalScore',10) WHERE Id=@sheet;",
                "fractional-tampered-lock-score" => "UPDATE dbo.ProcurementEvaluationScoreSheets SET ScoreSnapshotJson=JSON_MODIFY(ScoreSnapshotJson,'$.TotalScore',CAST(90.00000000001 AS decimal(38,11))) WHERE Id=@sheet;",
                "tampered-lock-evaluation" => "UPDATE dbo.ProcurementEvaluationScoreSheets SET ScoreSnapshotJson=JSON_MODIFY(ScoreSnapshotJson,'$.evaluationId',CONVERT(varchar(36),NEWID())) WHERE Id=@sheet;",
                "pending-current-recall" => "INSERT dbo.ProcurementEvaluationScoreRecalls(Id,TenantId,IsDeleted,ScoreSheetId,Status) VALUES(NEWID(),@tenant,0,@sheet,0);",
                "approved-current-recall" => "INSERT dbo.ProcurementEvaluationScoreRecalls(Id,TenantId,IsDeleted,ScoreSheetId,Status,AuthorizedNewAttempt) VALUES(NEWID(),@tenant,0,@sheet,1,3);",
                "unapproved-prior-recall" => "UPDATE dbo.ProcurementEvaluationScoreRecalls SET Status=0 WHERE Id=@recall;",
                "wrong-prior-authorized-attempt" => "UPDATE dbo.ProcurementEvaluationScoreRecalls SET AuthorizedNewAttempt=3 WHERE Id=@recall;",
                _ => throw new ArgumentOutOfRangeException(nameof(scenario))
            };
            await database.ExecuteAsync(sql, Parameters(new SqlParameter("@evaluation", CurrentEvaluationIds[0]),
                new SqlParameter("@evaluator", EvaluatorIds[0]), new SqlParameter("@sheet", CurrentSheetIds[0]),
                new SqlParameter("@recall", RecallId)));
        }

        public Task AppendReadyAsync(TestDatabase database)
        {
            var bidId = _recommendedBidOverride ?? BidId;
            var authority = JsonSerializer.Serialize(new
            {
                approvalReference = "award-readiness-decision", approvedByUserId = _authorityOverride ?? ApproverId,
                approvalActorUserIds = new[] { _authorityOverride ?? ApproverId }
            });
            var snapshot = JsonSerializer.Serialize(new { subjectType = "TenderBid", subjectIds = new[] { bidId }, businessPartnerIds = new[] { SupplierId } });
            var prerequisites = JsonSerializer.Serialize(Enumerable.Range(0, 9).Select(group => new
            {
                group, status = 0, items = new[] { new { status = 0 } }
            }));
            var lineage = JsonSerializer.Serialize(Enumerable.Range(0, 3).Select(index => new
            {
                evaluationType = "TenderEvaluation", evaluationId = CurrentEvaluationIds[index], phase = 2, status = "Submitted",
                scoreAttempts = new[] { new { scoreSheetId = CurrentSheetIds[index], committeeControlId = CommitteeId,
                    appointmentId = AppointmentIds[index], meetingId = MeetingId, phase = 2, scoreSubjectType = "TenderEvaluation",
                    scoreSubjectId = BidId, attempt = index == 0 ? 2 : 1, status = 0, submittedByUserId = UserIds[index],
                    submittedAtUtc = SubmittedAt.AddMinutes(1), integrityHash = new string('A', 64) } }
            }));
            return database.ExecuteAsync(
                """
                INSERT dbo.ProcurementAwardReadinessDecisions
                (Id,TenantId,SourceType,SourceId,SourceReference,Method,DecisionSequence,Status,RecommendationSubjectType,
                 RecommendedSubjectIdsJson,RecommendedBusinessPartnerIdsJson,RecommendationSnapshotJson,EvaluationLineageJson,
                 SupplierLineageJson,PrequalificationLineageJson,VerificationLineageJson,AuthorityLineageJson,EvidenceLineageJson,
                 PrerequisiteSnapshotJson,TimelineSnapshotJson,BlockedReasonsJson,SourceIntegrityHash,IntegrityHash,IdempotencyKey,
                 CorrelationId,EvaluatedAtUtc,EvaluatedByUserId,EvaluatedByName,CreatedAt,CreatedById,IsDeleted)
                VALUES(NEWID(),@tenant,1,@source,'TND-SQL-READY',1,1,1,'TenderBid',@subjects,@suppliers,@snapshot,@lineage,
                 '[]','[]','[]',@authority,'[]',@prerequisites,'[]','[]',REPLICATE('A',64),REPLICATE('B',64),CONVERT(varchar(36),NEWID()),
                 'sql-award-readiness-regression',@time,@approver,'Independent test approver',@time,@approver,0);
                """, Parameters(new SqlParameter("@source", _sourceOverride ?? TenderId),
                    new SqlParameter("@subjects", JsonSerializer.Serialize(new[] { bidId })),
                    new SqlParameter("@suppliers", JsonSerializer.Serialize(new[] { SupplierId })),
                    new SqlParameter("@snapshot", snapshot), new SqlParameter("@lineage", lineage),
                    new SqlParameter("@authority", authority), new SqlParameter("@prerequisites", prerequisites),
                    new SqlParameter("@time", SubmittedAt.AddMinutes(10))));
        }

        private SqlParameter[] Parameters(params SqlParameter[] additional) =>
        [new("@tenant", TenantId), new("@tender", TenderId), new("@bid", BidId), new("@supplier", SupplierId),
         new("@approver", ApproverId), new("@committee", CommitteeId), new("@meeting", MeetingId), .. additional];
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable award-readiness SQL Server regression.";
        }
    }

    private sealed class SqlServerTheoryAttribute : TheoryAttribute
    {
        public SqlServerTheoryAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable award-readiness SQL Server regression.";
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private const string Prefix = "RhemaERP_AwardReadinessTest_";
        private readonly string _databaseName = Prefix + Guid.NewGuid().ToString("N");
        private readonly string _masterConnection;
        private readonly string _connection;
        private bool _created;

        private TestDatabase(string baseConnection)
        {
            _masterConnection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master", TrustServerCertificate = true }.ConnectionString;
            _connection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = _databaseName, TrustServerCertificate = true }.ConnectionString;
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var result = new TestDatabase(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required."));
            await ExecuteOnAsync(result._masterConnection, $"CREATE DATABASE [{result._databaseName}];");
            result._created = true;
            try
            {
                await result.ExecuteAsync(DependencySchemaSql);
                await result.ApplyAsync(new AddProcurementAwardReadinessControls());
                return result;
            }
            catch { await result.DisposeAsync(); throw; }
        }

        public async Task ApplyAsync(Migration migration, bool down = false)
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            migration.GetType().GetMethod(down ? "Down" : "Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
            await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options);
            var commands = context.GetService<IMigrationsSqlGenerator>().Generate(builder.Operations);
            foreach (var command in commands) await ExecuteAsync(command.CommandText);
        }

        public Task ExecuteAsync(string sql, params SqlParameter[] parameters) => ExecuteOnAsync(_connection, sql, parameters);

        private static async Task ExecuteOnAsync(string connectionString, string sql, params SqlParameter[] parameters)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;
            command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new SqlConnection(_connection);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (T)(await command.ExecuteScalarAsync())!;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_created) return;
            if (!_databaseName.StartsWith(Prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(_databaseName[Prefix.Length..], "N", out _) ||
                new SqlConnectionStringBuilder(_connection).InitialCatalog != _databaseName)
                throw new InvalidOperationException("Refusing to drop a database outside the generated disposable test name.");
            SqlConnection.ClearAllPools();
            await ExecuteOnAsync(_masterConnection,
                $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END;");
            _created = false;
        }
    }

    private const string ReadTriggerSql = "SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_ProcurementAwardReadinessDecisions_Immutable'));";

    private const string SourceHistorySql = """
        SELECT (SELECT (SELECT * FROM dbo.TenderEvaluations ORDER BY Id FOR JSON PATH) AS evaluations,
               (SELECT * FROM dbo.ProcurementEvaluationScoreSheets ORDER BY Id FOR JSON PATH) AS sheets,
               (SELECT * FROM dbo.ProcurementEvaluationScoreRecalls ORDER BY Id FOR JSON PATH) AS recalls
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
        """;

    private const string DependencySchemaSql = """
        CREATE TABLE dbo.Tenants(Id uniqueidentifier NOT NULL PRIMARY KEY);
        CREATE TABLE dbo.Users(Id uniqueidentifier NOT NULL PRIMARY KEY,TenantId uniqueidentifier,IsActive bit);
        CREATE TABLE dbo.UserTenants(UserId uniqueidentifier,TenantId uniqueidentifier,Status int,IsDeleted bit,ExpiresAt datetime2);
        CREATE TABLE dbo.Tenders(Id uniqueidentifier NOT NULL PRIMARY KEY,TenantId uniqueidentifier,IsDeleted bit,TenderNumber nvarchar(100),Status nvarchar(50),AwardDate datetime2);
        CREATE TABLE dbo.TenderBids(Id uniqueidentifier NOT NULL PRIMARY KEY,TenantId uniqueidentifier,IsDeleted bit,TenderId uniqueidentifier,BusinessPartnerId uniqueidentifier,Status nvarchar(50));
        CREATE TABLE dbo.TenderEvaluators(Id uniqueidentifier NOT NULL PRIMARY KEY,TenantId uniqueidentifier,IsDeleted bit,TenderId uniqueidentifier,UserId uniqueidentifier);
        CREATE TABLE dbo.TenderEvaluations(Id uniqueidentifier NOT NULL PRIMARY KEY,TenantId uniqueidentifier,IsDeleted bit,TenderBidId uniqueidentifier,
            TenderEvaluatorId uniqueidentifier,Status nvarchar(50),IsRecommended bit,TotalScore decimal(5,2),EvaluationDate datetime2,SubmittedDate datetime2,
            UpdatedAt datetime2,CreatedAt datetime2,PriceScore decimal(5,2),QualityScore decimal(5,2),DeliveryScore decimal(5,2),
            ExperienceScore decimal(5,2),TechnicalScore decimal(5,2),ComplianceScore decimal(5,2),EvaluationCriteriaJson nvarchar(max),
            TechnicalComments nvarchar(max),CommercialComments nvarchar(max),OverallComments nvarchar(max),Recommendation nvarchar(max));
        CREATE TABLE dbo.RequestForQuotations(Id uniqueidentifier,TenantId uniqueidentifier,IsDeleted bit,RfqNumber nvarchar(100));
        CREATE TABLE dbo.RequestForQuotationQuotes(Id uniqueidentifier,RfqId uniqueidentifier,TenantId uniqueidentifier,BusinessPartnerId uniqueidentifier,IsDeleted bit);
        CREATE TABLE dbo.ProcurementRfqEvaluations(Id uniqueidentifier,RfqId uniqueidentifier,TenantId uniqueidentifier,IsDeleted bit,Status int,
            MethodRuleId uniqueidentifier,MethodRuleCode nvarchar(100),WorkflowDefinitionId uniqueidentifier,WorkflowInstanceId uniqueidentifier);
        CREATE TABLE dbo.ProcurementRfqEvaluationLines(EvaluationId uniqueidentifier,TenantId uniqueidentifier,IsDeleted bit,QuoteId uniqueidentifier);
        CREATE TABLE dbo.ProcurementTenderControls(Id uniqueidentifier,TenderId uniqueidentifier,TenantId uniqueidentifier,IsDeleted bit,Status int,
            Method int,RecommendedBidId uniqueidentifier,MethodRuleId uniqueidentifier,MethodRuleCode nvarchar(100),AuthorityRouteId uniqueidentifier,
            AuthorityRouteReference nvarchar(100),WorkflowDefinitionId uniqueidentifier,WorkflowInstanceId uniqueidentifier);
        CREATE TABLE dbo.ProcurementExceptionalSourcingControls(Id uniqueidentifier,TenderId uniqueidentifier,TenantId uniqueidentifier,IsDeleted bit,Status int,
            Method int,RecommendedBidId uniqueidentifier,MethodRuleId uniqueidentifier,MethodRuleCode nvarchar(100),AuthorityRouteId uniqueidentifier,
            AuthorityRouteReference nvarchar(100),WorkflowDefinitionId uniqueidentifier,WorkflowInstanceId uniqueidentifier);
        CREATE TABLE dbo.WorkflowInstances(Id uniqueidentifier,WorkflowDefinitionId uniqueidentifier,EntityId uniqueidentifier,TenantId uniqueidentifier,IsDeleted bit,Status int);
        CREATE TABLE dbo.ProcurementEvaluationCommitteeControls(Id uniqueidentifier NOT NULL PRIMARY KEY,TenantId uniqueidentifier,IsDeleted bit,
            SourceType int,SourceId uniqueidentifier,Version int,Status int);
        CREATE TABLE dbo.ProcurementEvaluationCommitteeAppointments(Id uniqueidentifier NOT NULL PRIMARY KEY,TenantId uniqueidentifier,IsDeleted bit,
            CommitteeControlId uniqueidentifier,UserId uniqueidentifier,Status int);
        CREATE TABLE dbo.ProcurementEvaluationMeetings(Id uniqueidentifier NOT NULL PRIMARY KEY,TenantId uniqueidentifier,IsDeleted bit,
            CommitteeControlId uniqueidentifier,Status int);
        CREATE TABLE dbo.ProcurementEvaluationScoreSheets(Id uniqueidentifier NOT NULL PRIMARY KEY,TenantId uniqueidentifier,IsDeleted bit,
            CommitteeControlId uniqueidentifier,MeetingId uniqueidentifier,AppointmentId uniqueidentifier,Phase int,ScoreSubjectType nvarchar(100),
            ScoreSubjectId uniqueidentifier,Attempt int,Status int,SubmittedAtUtc datetime2,SubmittedByUserId uniqueidentifier,ScoreSnapshotJson nvarchar(max),
            SignatureReference nvarchar(500),EvidenceReference nvarchar(500),IntegrityHash nvarchar(64));
        CREATE TABLE dbo.ProcurementEvaluationScoreRecalls(Id uniqueidentifier NOT NULL PRIMARY KEY,TenantId uniqueidentifier,IsDeleted bit,
            ScoreSheetId uniqueidentifier,Status int,AuthorizedNewAttempt int,RequestedByUserId uniqueidentifier,DecidedByUserId uniqueidentifier,DecidedAtUtc datetime2);
        """;
}
