using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringReconnaissanceRulesTests
{
    [Fact]
    public void Amendments_are_limited_to_information_gathering()
    {
        var action = () => CivilEngineeringReconnaissanceRules.EnsureMutableStage(
            CivilEngineeringDesignStages.CivilEngineerDesign);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*gathering information*");
    }

    [Fact]
    public void Completion_requires_a_controlled_source_and_photo()
    {
        var items = new[]
        {
            Constraint(CivilEngineeringConstraintResolutionStatus.Mitigated)
        };

        var action = () => CivilEngineeringReconnaissanceRules.EnsureValidItems(items, true);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*photo*");
    }

    [Fact]
    public void Completion_rejects_open_design_blockers()
    {
        var items = new[]
        {
            Source(),
            Photo(),
            Constraint(CivilEngineeringConstraintResolutionStatus.Open, true)
        };

        var action = () => CivilEngineeringReconnaissanceRules.EnsureValidItems(items, true);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*design-blocking*");
    }

    [Fact]
    public void Completion_accepts_resolved_relational_inputs()
    {
        var items = new[]
        {
            Source(),
            Photo(),
            Constraint(CivilEngineeringConstraintResolutionStatus.Mitigated, true)
        };

        var action = () => CivilEngineeringReconnaissanceRules.EnsureValidItems(items, true);

        action.Should().NotThrow();
    }

    [Fact]
    public void Information_source_requires_section_and_dms_lineage()
    {
        var items = new[]
        {
            new CivilEngineeringReconnaissanceItemFacts(
                CivilEngineeringReconnaissanceItemKind.InformationSource,
                null,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                null,
                null,
                false)
        };

        var action = () => CivilEngineeringReconnaissanceRules.EnsureValidItems(items, false);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*active section*");
    }

    private static CivilEngineeringReconnaissanceItemFacts Source() => new(
        CivilEngineeringReconnaissanceItemKind.InformationSource,
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        null,
        null,
        null,
        false);

    private static CivilEngineeringReconnaissanceItemFacts Photo() => new(
        CivilEngineeringReconnaissanceItemKind.Photo,
        null,
        Guid.NewGuid(),
        Guid.NewGuid(),
        null,
        null,
        null,
        false);

    private static CivilEngineeringReconnaissanceItemFacts Constraint(
        CivilEngineeringConstraintResolutionStatus status,
        bool blocks = false) => new(
        CivilEngineeringReconnaissanceItemKind.Constraint,
        null,
        null,
        null,
        CivilEngineeringConstraintCategory.Topography,
        CivilEngineeringConstraintSeverity.High,
        status,
        blocks);
}
