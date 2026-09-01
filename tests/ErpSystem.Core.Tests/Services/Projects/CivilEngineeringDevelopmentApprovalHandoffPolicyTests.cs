using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDevelopmentApprovalHandoffPolicyTests
{
    [Fact]
    public void Handoff_requires_another_section_and_configured_recipient()
    {
        var errors = CivilEngineeringDevelopmentApprovalHandoffPolicy.ValidateCreate(new CreateCivilEngineeringDevelopmentApprovalHandoffRequest
        {
            ClientRequestId = Guid.Empty,
            ToSection = CivilEngineeringPermittingSection.BuildingInspectorate,
            RecipientRoleId = Guid.Empty,
            RecipientUserId = Guid.Empty,
            DueDate = DateTime.UtcNow.Date.AddDays(1)
        }, CivilEngineeringPermittingSection.BuildingInspectorate, DateTime.UtcNow);

        errors.Should().Contain(error => error.Contains("client request", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("recipient role", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("recipient user", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("different destination", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Handoff_allows_optional_supporting_evidence_but_rejects_an_unknown_section()
    {
        var errors = CivilEngineeringDevelopmentApprovalHandoffPolicy.ValidateCreate(new CreateCivilEngineeringDevelopmentApprovalHandoffRequest
        {
            ClientRequestId = Guid.NewGuid(), ToSection = (CivilEngineeringPermittingSection)99,
            RecipientRoleId = Guid.NewGuid(), RecipientUserId = Guid.NewGuid(), DueDate = DateTime.UtcNow.Date.AddDays(1)
        }, CivilEngineeringPermittingSection.Architecture, DateTime.UtcNow);

        errors.Should().ContainSingle(error => error.Contains("supported destination", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Handoff_rejects_a_past_recipient_due_date()
    {
        var errors = CivilEngineeringDevelopmentApprovalHandoffPolicy.ValidateCreate(new CreateCivilEngineeringDevelopmentApprovalHandoffRequest
        {
            ClientRequestId = Guid.NewGuid(), ToSection = CivilEngineeringPermittingSection.Architecture,
            RecipientRoleId = Guid.NewGuid(), RecipientUserId = Guid.NewGuid(), DueDate = DateTime.UtcNow.Date.AddDays(-1)
        }, CivilEngineeringPermittingSection.BuildingInspectorate, DateTime.UtcNow);

        errors.Should().ContainSingle(error => error.Contains("due date cannot be in the past", StringComparison.OrdinalIgnoreCase));
    }
}
