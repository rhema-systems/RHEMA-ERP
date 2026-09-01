using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDocumentRulesTests
{
    [Theory]
    [InlineData(".dwg", CivilEngineeringFileCategory.AutoCad)]
    [InlineData(".pro", CivilEngineeringFileCategory.ProtaStructure)]
    [InlineData(".rvt", CivilEngineeringFileCategory.Revit)]
    [InlineData(".std", CivilEngineeringFileCategory.StaadPro)]
    [InlineData(".pdf", CivilEngineeringFileCategory.Pdf)]
    [InlineData(".docx", CivilEngineeringFileCategory.Office)]
    public void FileCategory_maps_governed_engineering_formats(string extension, CivilEngineeringFileCategory expected) =>
        CivilEngineeringDocumentRules.FileCategory(extension).Should().Be(expected);

    [Fact]
    public void ExpectedReference_uses_controlled_discipline_policy() =>
        CivilEngineeringDocumentRules.ExpectedReference(
                "TDC 001", CivilEngineeringDesignDiscipline.Structural, null, 7, 2,
                CivilEngineeringDocumentNamingPolicy.ProjectDisciplineSequenceRevision)
            .Should().Be("TDC-001-STR-007-R02");

    [Fact]
    public void ExpectedReference_requires_package_for_package_policy() =>
        FluentActions.Invoking(() => CivilEngineeringDocumentRules.ExpectedReference(
                "TDC-001", CivilEngineeringDesignDiscipline.Civil, null, 1, 0,
                CivilEngineeringDocumentNamingPolicy.ProjectWorkPackageSequenceRevision))
            .Should().Throw<InvalidOperationException>().WithMessage("*work package*");

    [Theory]
    [InlineData(CivilEngineeringDocumentStatus.Draft)]
    [InlineData(CivilEngineeringDocumentStatus.Returned)]
    public void Submit_allows_only_editable_states(CivilEngineeringDocumentStatus status) =>
        CivilEngineeringDocumentRules.EnsureCanSubmit(status);

    [Theory]
    [InlineData(CivilEngineeringDocumentStatus.ForReview, true)]
    [InlineData(CivilEngineeringDocumentStatus.Approved, false)]
    public void Review_is_limited_to_for_review(CivilEngineeringDocumentStatus status, bool allowed)
    {
        var action = () => CivilEngineeringDocumentRules.EnsureCanReview(status);
        if (allowed) action.Should().NotThrow();
        else action.Should().Throw<InvalidOperationException>();
    }
}
