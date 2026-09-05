using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementDownstreamFrozenLineageMigrationTests
{
    [Fact]
    public void Award_readiness_uses_the_frozen_source_evaluation_not_current_master_configuration()
    {
        var sql = TriggerSql(
            new AddProcurementAwardReadinessControls(),
            "TR_ProcurementAwardReadinessDecisions_Immutable");

        sql.Should().Contain("ProcurementTenderControls")
            .And.Contain("TenderEvaluations")
            .And.Contain("WorkflowInstances")
            .And.Contain("THROW 51407")
            .And.Contain("THROW 51408")
            .And.NotContain("ProcurementPolicySets")
            .And.NotContain("ProcurementConfigurationProfiles")
            .And.NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void Contract_activation_preserves_frozen_award_policy_and_configuration_references()
    {
        var sql = TriggerSql(
            new TDC0407ContractActivationGate(),
            "TR_ProcurementContractActivations_TDC0407Protected");

        sql.Should().Contain("i.ConfigurationProfileId <> d.ConfigurationProfileId")
            .And.Contain("i.PolicySetId <> d.PolicySetId")
            .And.Contain("i.AwardReadinessDecisionId <> d.AwardReadinessDecisionId")
            .And.Contain("i.AwardReadinessIntegrityHash <> d.AwardReadinessIntegrityHash")
            .And.NotContain("JOIN ProcurementPolicySets")
            .And.NotContain("JOIN ProcurementConfigurationProfiles")
            .And.NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void Purchase_order_source_transition_uses_ready_award_lineage_not_current_policy_masters()
    {
        var sql = TriggerSql(
            new AlignPurchaseOrderSourceTriggerWithSupportedRoutes(),
            "TR_PurchaseOrders_ApprovedSourceProtected");

        sql.Should().Contain("readiness.Status = 1")
            .And.Contain("sourcing.Status = 3")
            .And.Contain("THROW 51205")
            .And.NotContain("ProcurementPolicySets")
            .And.NotContain("ProcurementConfigurationProfiles")
            .And.NotContain("DISABLE TRIGGER");
    }

    private static string TriggerSql(Migration migration, string triggerName)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return string.Join(
            Environment.NewLine,
            builder.Operations
                .OfType<SqlOperation>()
                .Select(operation => operation.Sql)
                .Where(sql => sql.Contains(triggerName, StringComparison.Ordinal)));
    }
}
