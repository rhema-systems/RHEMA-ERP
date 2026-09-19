using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyEscalationFormulaRulesTests
{
    private static readonly QsEscalationValue Policy = new()
    {
        MaterialCoefficient = 45m,
        LabourCoefficient = 25m,
        PlantCoefficient = 20m,
        OtherCoefficient = 10m
    };

    [Fact]
    public void Exact_policy_components_are_accepted()
    {
        QuantitySurveyEscalationFormulaRules.ValidatePolicyComponents(
                Values((QuantitySurveyEscalationComponentType.Other, 10m)), Policy)
            .Should().BeNull();
    }

    [Fact]
    public void Missing_or_duplicate_component_is_rejected()
    {
        var values = Values((QuantitySurveyEscalationComponentType.Other, 10m));
        values[3] = (QuantitySurveyEscalationComponentType.Material, 10m);

        QuantitySurveyEscalationFormulaRules.ValidatePolicyComponents(values, Policy)
            .Should().Contain("exactly once");
    }

    [Fact]
    public void Client_coefficient_override_is_rejected_even_when_total_is_one_hundred()
    {
        QuantitySurveyEscalationFormulaRules.ValidatePolicyComponents(
                Values(
                    (QuantitySurveyEscalationComponentType.Material, 44m),
                    (QuantitySurveyEscalationComponentType.Other, 11m)),
                Policy)
            .Should().Contain("approved QS-DEC-006 value");
    }

    [Fact]
    public void Migration_is_qs_only_and_contains_database_hard_stops()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Data",
            "LegacyMigrationsArchive",
            "20260809152905_AddQuantitySurveyEscalationFormulaRegister.cs"));

        source.Should().NotContain("EstateManagedAssets");
        source.Should().NotContain("EstateGroundRent");
        source.Should().Contain("TR_QsEscalationFormulas_TenantAndLineageGuard");
        source.Should().Contain("TR_QsEscalationFormulas_ApprovedImmutable");
        source.Should().Contain("TR_QsEscalationFormulaComponents_Guard");
        source.Should().Contain("TR_QsPriceIndexFamilies_InUseGuard");
        source.Should().Contain("TR_QsEscalationFormulaRevisions_AppendOnly");
        source.Should().Contain("QS-DEC-001");
        source.Should().Contain("QS-DEC-006");
        source.Should().Contain("QS_ESCALATION");
        source.Should().Contain("Pending or Approved formulas require exactly four policy-matching controlled index components");
        source.Should().Contain("AND s.[ProjectId] = i.[ProjectId]");
        source.Should().Contain("AND s.[ContractId] = i.[ContractId]");
        source.Should().Contain("AND s.[Code] = i.[Code]");
    }

    [Fact]
    public void Migration_is_discoverable_in_fast_builds_and_covered_by_vps_preflight()
    {
        var root = FindRepositoryRoot();
        var migrationId = "20260809152905_AddQuantitySurveyEscalationFormulaRegister";
        var metadata = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "FastBuildMigrationMetadata.cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        metadata.Should().Contain($"Migration(\"{migrationId}\")");
        preflight.Should().Contain($"GUARD_COVERAGE|{migrationId}");
    }

    [Fact]
    public void Service_preserves_revision_lineage_and_translates_database_guards()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Data",
            "Services",
            "QuantitySurveyEscalationFormulaService.cs"));

        service.Should().Contain("RequireStableRevisionLineage(source, request.ProjectId, request.ContractId, normalized.Code)");
        service.Should().Contain("sqlException.Number is >= 51020 and <= 51029");
        service.Should().Contain("An index family with this code was created concurrently");
        service.Should().Contain("if (entity.Status is \"PendingApproval\" or \"Approved\") return MapFormula(entity)");
        service.Should().Contain("if (entity.Status == \"Rejected\") return MapFormula(entity)");
        service.Should().Contain("if (entity.Status == \"Retired\") return MapFormula(entity)");
    }

    private static List<(QuantitySurveyEscalationComponentType Component, decimal Coefficient)> Values(
        params (QuantitySurveyEscalationComponentType Component, decimal Coefficient)[] overrides)
    {
        var values = new List<(QuantitySurveyEscalationComponentType, decimal)>
        {
            (QuantitySurveyEscalationComponentType.Material, 45m),
            (QuantitySurveyEscalationComponentType.Labour, 25m),
            (QuantitySurveyEscalationComponentType.Plant, 20m),
            (QuantitySurveyEscalationComponentType.Other, 10m)
        };
        foreach (var replacement in overrides)
        {
            var index = values.FindIndex(value => value.Item1 == replacement.Component);
            values[index] = replacement;
        }
        return values;
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
