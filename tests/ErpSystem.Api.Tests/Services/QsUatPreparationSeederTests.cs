using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using ErpSystem.Data.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class QsUatPreparationSeederTests
{
    [Theory]
    [InlineData(false, "RhemaERP_VpsTest_UAT", "RhemaERP_VpsTest_UAT")]
    [InlineData(true, "RhemaERP_VpsTest_UAT", "OtherDatabase")]
    [InlineData(true, "", "RhemaERP_VpsTest_UAT")]
    [InlineData(true, "master", "master")]
    [InlineData(true, "RhemaERP_Production", "RhemaERP_Production")]
    public void TargetGuard_RejectsDisabledWrongOrSystemTarget(bool enabled, string expected, string actual)
    {
        var action = () => QsUatPreparationSeeder.ValidateTarget(expected, actual, enabled);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TargetGuard_AcceptsExactExplicitTestTarget() =>
        QsUatPreparationSeeder.ValidateTarget("RhemaERP_VpsTest_UAT", "RhemaERP_VpsTest_UAT", true);

    public static IEnumerable<object[]> DecisionKeys => QsUatDecisionRecipes.Values.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(DecisionKeys))]
    public void EveryProposedDecision_PassesExistingOwnerSchema(string key)
    {
        var references = Regex.Matches(QsUatDecisionRecipes.Values[key], @"\{\{(\w+)\}\}")
            .Select(m => m.Groups[1].Value).Distinct().ToDictionary(name => name, _ => Guid.NewGuid().ToString());
        references["From"] = "2026-09-28";
        references["ReportIds"] = JsonSerializer.Serialize(new[] { Guid.NewGuid() });
        var resolved = QsUatDecisionRecipes.Resolve(key, references);
        resolved.Missing.Should().BeEmpty();
        resolved.Value.Should().NotBeNull();
        var validation = QuantitySurveyConfigurationDecisionRegistry.Validate(key, 1, resolved.Value!.Value);
        validation.Errors.Should().BeEmpty();
        validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingSelectors_AreReportedWithoutInventedIds()
    {
        var resolved = QsUatDecisionRecipes.Resolve("QS-DEC-008", new Dictionary<string, string> { ["From"] = "2026-09-28" });
        resolved.Value.Should().BeNull();
        resolved.Missing.Should().Contain("ExpenseAccount").And.Contain("TaxGroup").And.Contain("CertificateWorkflow");
    }

    [Fact]
    public void Routes_CoverEveryConfiguredBinding_WithIndependentExplicitPerformers()
    {
        var actors = QsUatPreparationSeeder.RequiredActors.ToDictionary(name => name, _ => Guid.NewGuid());
        foreach (var entity in QuantitySurveyWorkflowBindingRegistry.EntityTypes)
        {
            var route = QsUatPreparationSeeder.CreateWorkflowRecipe(entity.Code, entity.Name, actors);
            route.Transitions.Should().HaveCount(route.Steps.Count - 1);
            var performerIds = new List<Guid>();
            foreach (var step in route.Steps)
            {
                var config = step.Configuration!.ApprovalConfig!;
                config.PreventInitiatorApproval.Should().BeTrue();
                config.RequireDistinctApprovers.Should().BeTrue();
                config.AutoApprovalCondition.Should().BeNull();
                config.ApproverRules.Should().ContainSingle();
                var rule = config.ApproverRules.Single();
                rule.AssignmentType.Should().Be(WorkflowAssignmentType.User);
                performerIds.Add(rule.UserId!.Value);
                if (step.Order > 1) config.ConflictRules.Should().ContainSingle(r => r.ActorSource == WorkflowApprovalActorSource.AnyPreviousApprover);
            }
            performerIds.Should().OnlyHaveUniqueItems().And.NotContain(actors["uat.qs.preparer"]);
            if (entity.Code is "QS_VALUATION" or "QS_PAYMENT_CERTIFICATE") route.Steps.Should().HaveCount(4);
            if (entity.Code == "QS_VARIATION") route.Steps.Should().HaveCount(5);
        }
    }

    [Fact]
    public void Routes_RejectRepeatedOrMissingActors()
    {
        var actors = QsUatPreparationSeeder.RequiredActors.ToDictionary(name => name, _ => Guid.NewGuid());
        actors["uat.qs.approver"] = actors["uat.qs.reviewer"];
        ((Action)(() => QsUatPreparationSeeder.ValidateActors(actors))).Should().Throw<InvalidOperationException>();
        actors.Remove("uat.qs.engineer");
        ((Action)(() => QsUatPreparationSeeder.ValidateActors(actors))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DraftPreparation_PreservesUserValuesAndApprovalStates()
    {
        static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();
        QsUatPreparationSeeder.CanPrepareDecision(new QuantitySurveyDecisionDto
        { Status = QuantitySurveyConfigurationDecisionStatus.Draft, ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Pending, Value = Json("{}") }).Should().BeTrue();
        QsUatPreparationSeeder.CanPrepareDecision(new QuantitySurveyDecisionDto
        { Status = QuantitySurveyConfigurationDecisionStatus.Draft, ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Pending, Value = Json("{\"maximumRetentionPercent\":7}") }).Should().BeFalse();
        QsUatPreparationSeeder.CanPrepareDecision(new QuantitySurveyDecisionDto
        { Status = QuantitySurveyConfigurationDecisionStatus.Approved, ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Approved, Value = Json("{}") }).Should().BeFalse();
    }

    [Fact]
    public void FinanceSelectors_DoNotChooseAnAmbiguousAccount()
    {
        var lookup = new Dictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>>
        {
            ["postingExpenseAccounts"] = [new() { Value = Guid.NewGuid().ToString(), Label = "5100 - Expenses one" },
                new() { Value = Guid.NewGuid().ToString(), Label = "5200 - Expenses two" }]
        };
        var refs = QsUatPreparationSeeder.ResolveReferences(lookup, new Dictionary<string, Guid>(),
            new Dictionary<string, Guid>(), new Dictionary<string, Guid>(), new DateTime(2026, 9, 28));
        refs.Should().NotContainKey("ExpenseAccount");
    }

    [Fact]
    public void FinanceSelectors_ResolveSeededNaturalAccountNumbers()
    {
        var expense = Guid.NewGuid();
        var payable = Guid.NewGuid();
        var lookup = new Dictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>>
        {
            ["postingExpenseAccounts"] = [
                new() { Value = expense.ToString(), Label = "5000 - Cost of Goods Sold" },
                new() { Value = Guid.NewGuid().ToString(), Label = "5100 - Administrative Expenses" }],
            ["accountsPayableAccounts"] = [
                new() { Value = payable.ToString(), Label = "2000 - Accounts Payable" },
                new() { Value = Guid.NewGuid().ToString(), Label = "2100 - Accrued Liabilities" }]
        };

        var refs = QsUatPreparationSeeder.ResolveReferences(lookup, new Dictionary<string, Guid>(),
            new Dictionary<string, Guid>(), new Dictionary<string, Guid>(), new DateTime(2026, 9, 28));

        refs["ExpenseAccount"].Should().Be(expense.ToString());
        refs["ApAccount"].Should().Be(payable.ToString());
    }

    [Fact]
    public void FinanceSelectors_ResolveCanonicalPurchaseTaxGroup()
    {
        var taxGroup = Guid.NewGuid();
        var lookup = new Dictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>>
        {
            ["supplierTaxGroups"] = [
                new() { Value = taxGroup.ToString(), Label = "VAT-STD-PURCHASES - Purchase VAT Standard Scheme" },
                new() { Value = Guid.NewGuid().ToString(), Label = "WHT-SERVICES - Withholding services" }]
        };

        var refs = QsUatPreparationSeeder.ResolveReferences(lookup, new Dictionary<string, Guid>(),
            new Dictionary<string, Guid>(), new Dictionary<string, Guid>(), new DateTime(2026, 9, 28));

        refs["TaxGroup"].Should().Be(taxGroup.ToString());
    }

    [Fact]
    public void SeedReconciliation_RequiresUntouchedBootstrapOwnership()
    {
        static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();
        var decision = new QuantitySurveyDecisionDto
        {
            DecisionKey = "QS-DEC-007", SourceLineage = QsUatPreparationSeeder.SeedMarker,
            Status = QuantitySurveyConfigurationDecisionStatus.Draft,
            ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Pending,
            EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Missing,
            Value = Json("{\"effectiveFrom\":\"2026-09-28\"}")
        };

        QsUatPreparationSeeder.CanReconcileSeedDecision(decision, true).Should().BeTrue();
        QsUatPreparationSeeder.CanReconcileSeedDecision(decision, false).Should().BeFalse();
        QsUatPreparationSeeder.CanReconcileSeedDecision(new QuantitySurveyDecisionDto
        {
            SourceLineage = "Manual edit", Status = decision.Status, ApprovalStatus = decision.ApprovalStatus,
            EvidenceStatus = decision.EvidenceStatus, Value = decision.Value
        }, true).Should().BeFalse();
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("user-owned")]
    [InlineData("different-actor")]
    [InlineData("rejected")]
    [InlineData("reviewed")]
    [InlineData("stale-selection")]
    [InlineData("invalid-value")]
    public void AutoApproval_RequiresAllUntouchedSeededDecisionsAndCurrentSelections(string scenario)
    {
        var actorId = Guid.NewGuid();
        var decisions = new List<QuantitySurveyConfigurationDecision>();
        var lookups = new Dictionary<string, List<QuantitySurveyLookupOptionDto>>();
        foreach (var recipe in QsUatDecisionRecipes.Values)
        {
            var references = Regex.Matches(recipe.Value, @"\{\{(\w+)\}\}")
                .Select(m => m.Groups[1].Value).Distinct().ToDictionary(name => name, _ => Guid.NewGuid().ToString());
            references["From"] = "2026-09-28";
            references["ReportIds"] = JsonSerializer.Serialize(new[] { Guid.NewGuid() });
            var value = QsUatDecisionRecipes.Resolve(recipe.Key, references).Value!.Value;
            decisions.Add(new() { DecisionKey = recipe.Key, ValueJson = value.GetRawText(),
                SourceLineage = QsUatPreparationSeeder.SeedMarker, LastModifiedById = actorId });
            foreach (var field in QuantitySurveyConfigurationDecisionRegistry.GetRequired(recipe.Key).Fields.Where(f => f.LookupSource != null))
            {
                if (!value.TryGetProperty(field.Name, out var selected)) continue;
                if (!lookups.TryGetValue(field.LookupSource!, out var options)) lookups[field.LookupSource!] = options = [];
                var values = selected.ValueKind == JsonValueKind.Array ? selected.EnumerateArray().Select(v => v.GetString()!) : [selected.GetString()!];
                options.AddRange(values.Select(v => new QuantitySurveyLookupOptionDto { Value = v, Group = field.LookupGroup }));
            }
        }
        switch (scenario)
        {
            case "missing": decisions.RemoveAt(0); break;
            case "duplicate": decisions[0].DecisionKey = decisions[1].DecisionKey; break;
            case "user-owned": decisions[0].SourceLineage = "Manual edit"; break;
            case "different-actor": decisions[0].LastModifiedById = Guid.NewGuid(); break;
            case "rejected": decisions[0].Status = QuantitySurveyConfigurationDecisionStatus.Rejected; break;
            case "reviewed": decisions[0].EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Attached; break;
            case "stale-selection": lookups.Clear(); break;
            case "invalid-value": decisions[0].ValueJson = "{}"; break;
        }
        Action validate = () => QsUatPreparationSeeder.ValidateAutoApprovalDecisions(decisions, actorId,
            lookups.ToDictionary(x => x.Key, x => (IReadOnlyList<QuantitySurveyLookupOptionDto>)x.Value));
        if (scenario == "valid") validate.Should().NotThrow();
        else validate.Should().Throw<InvalidOperationException>();
    }

    [PreparedSqlFact]
    public async Task AutoApproval_SqlRollbackPublicationAndRetry_PreserveAtomicEvidence()
    {
        // Opt-in only: a previously prepared, disposable QS verification clone.
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("RHEMA_QS_APPROVAL_TEST_CONNECTION"));
        connection.InitialCatalog.Should().StartWith("RhemaERP_QsUatVerify_");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString, sql => sql.EnableRetryOnFailure()).Options;
        await using var db = new ApplicationDbContext(options);
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Code == "DEFAULT" && !t.IsDeleted);
        var userId = await db.Users.IgnoreQueryFilters().Where(u => u.TenantId == tenant.Id && u.UserName == "qs.uat.bootstrap")
            .Select(u => u.Id).SingleAsync();
        var actor = new QsUatSeedContext(); actor.Initialize(tenant, userId);
        var profileId = await db.QuantitySurveyConfigurationProfiles.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenant.Id && p.ProfileCode == "TDC-QUANTITY-SURVEY" && !p.IsDeleted)
            .Select(p => p.Id).SingleAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["QsUat:Enabled"] = "true", ["QsUat:AutoApprove"] = "true", ["QsUat:ExpectedDatabase"] = connection.InitialCatalog
        }).Build();
        var owner = new QuantitySurveyConfigurationService(db, actor, NullLogger<QuantitySurveyConfigurationService>.Instance);
        var failingOwner = new Mock<IQuantitySurveyConfigurationService>();
        failingOwner.Setup(s => s.GetLookupsAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => owner.GetLookupsAsync(ct));
        failingOwner.Setup(s => s.PublishProfileAsync(profileId, It.IsAny<QuantitySurveyLifecycleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("injected publication failure"));
        QsUatPreparationSeeder Seeder(IQuantitySurveyConfigurationService service) =>
            new(db, actor, null!, service, null!, null!, null!, configuration);
        async Task<int[]> Counts() => [await db.CentralDocumentRecords.CountAsync(), await db.CentralDocumentVersions.CountAsync(),
            await db.QuantitySurveyConfigurationEvidenceLinks.CountAsync(), await db.QuantitySurveyConfigurationRevisions.CountAsync()];
        var before = await Counts();
        var failed = () => Seeder(failingOwner.Object).AutoApproveAndPublishAsync(profileId, tenant.Id, userId, default);
        await failed.Should().ThrowAsync<InvalidOperationException>().WithMessage("injected publication failure");
        db.ChangeTracker.Clear();
        (await Counts()).Should().Equal(before, "failed publication must also roll back its DMS evidence and decision approvals");
        (await db.QuantitySurveyConfigurationDecisions.Where(d => d.ProfileId == profileId).Select(d => d.Status).ToListAsync())
            .Should().HaveCount(17).And.OnlyContain(s => s == QuantitySurveyConfigurationDecisionStatus.Draft);

        var seeder = Seeder(owner);
        await seeder.AutoApproveAndPublishAsync(profileId, tenant.Id, userId, default);
        db.ChangeTracker.Clear();
        var published = await owner.GetProfileAsync(profileId);
        published.LifecycleStatus.Should().Be(QuantitySurveyConfigurationProfileStatus.Published);
        published.Validation.IsValid.Should().BeTrue();
        published.Decisions.Should().HaveCount(17).And.OnlyContain(d => d.IsComplete && d.ApprovalReference == QsUatPreparationSeeder.AutoApprovalMarker);
        var after = await Counts();
        (after[0] - before[0]).Should().Be(1);
        (after[1] - before[1]).Should().Be(1);
        (after[2] - before[2]).Should().Be(17);
        (after[3] - before[3]).Should().Be(35);
        var memorandum = await db.CentralDocumentVersions.SingleAsync(v => v.DocumentRecord.SourceRecordId == profileId &&
            v.DocumentRecord.SourceLabel == QsUatPreparationSeeder.AutoApprovalMarker);
        memorandum.RepositoryPath.Should().BeNull("the memorandum is a metadata record, not a fictitious uploaded file");
        await seeder.AutoApproveAndPublishAsync(profileId, tenant.Id, userId, default);
        (await Counts()).Should().Equal(after, "retry must add no documents, evidence links or revisions");
    }

    private sealed class PreparedSqlFactAttribute : FactAttribute
    {
        public PreparedSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_QS_APPROVAL_TEST_CONNECTION")))
                Skip = "Set RHEMA_QS_APPROVAL_TEST_CONNECTION to an isolated, prepared QS verification clone.";
        }
    }
}
