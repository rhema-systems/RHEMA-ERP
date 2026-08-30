using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringProjectEngineerAssignmentPolicyTests
{
    [Fact]
    public void Allows_an_active_configured_civil_engineer_project_member()
    {
        var errors = CivilEngineeringProjectEngineerAssignmentPolicy.ValidateCandidate(new(
            Guid.NewGuid(),
            CivilEngineeringAccessControlRegistry.CivilEngineerRole,
            true,
            true,
            true));

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Rejects_a_non_engineering_or_unconfigured_candidate()
    {
        var errors = CivilEngineeringProjectEngineerAssignmentPolicy.ValidateCandidate(new(
            Guid.NewGuid(),
            CivilEngineeringAccessControlRegistry.DraftsmanRole,
            true,
            true,
            false));

        errors.Should().Contain(error => error.Contains("Civil Engineer", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("supervision policy", StringComparison.Ordinal));
    }

    [Fact]
    public void Requires_a_later_effective_date_for_reassignment()
    {
        var errors = CivilEngineeringProjectEngineerAssignmentPolicy.ValidateAssignment(
            new DateTime(2026, 8, 15),
            CivilEngineeringProjectEngineerAuthority.FullProjectEngineer,
            new DateTime(2026, 8, 15));

        errors.Should().ContainSingle(error => error.Contains("after", StringComparison.Ordinal));
    }

    [Fact]
    public void Requires_reasoned_end_and_valid_period()
    {
        var errors = CivilEngineeringProjectEngineerAssignmentPolicy.ValidateEnd(
            new DateTime(2026, 8, 15),
            new DateTime(2026, 8, 14),
            "no");

        errors.Should().HaveCount(2);
    }
}
