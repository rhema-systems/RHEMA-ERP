using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class QsUatPreparationSeederTests
{
    [Theory]
    [InlineData(false, "RhemaERP_VpsTest_UAT", "RhemaERP_VpsTest_UAT")]
    [InlineData(true, "RhemaERP_VpsTest_UAT", "OtherDatabase")]
    [InlineData(true, "", "RhemaERP_VpsTest_UAT")]
    [InlineData(true, "master", "master")]
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
}
