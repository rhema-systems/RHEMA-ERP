using System.Text.Json;
using ErpSystem.Api.Services.QuantitySurvey;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyArchitectureValuationTests
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    public async Task Internal_valuation_does_not_require_unused_portal_configuration(
        bool requireContractor, bool requireConsultant, bool externalEnabled, bool allowed)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenant);
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        var projects = new Mock<IProjectService>();
        projects.Setup(x => x.HasProjectAccessAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        var profile = new QuantitySurveyConfigurationProfile
        { TenantId = tenant, Name = "Core QS", LifecycleStatus = QuantitySurveyConfigurationProfileStatus.Published,
          PublishedAt = DateTime.UtcNow.AddDays(-1), EffectiveFrom = DateTime.UtcNow.AddDays(-1) };
        var template = new ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate
        { TenantId = tenant, TemplateCode = "QS-VALUATION", SourceLabel = "Valuation", IsActive = true, PublishedAt = DateTime.UtcNow };
        var entityType = new WorkflowEntityType { TenantId = tenant, Code = QuantitySurveyWorkflowBindingRegistry.Valuation, Name = "QS valuation", IsActive = true };
        var definition = new WorkflowDefinition
        { TenantId = tenant, Name = "QS approval", EntityTypeId = entityType.Id, EntityType = entityType,
          LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published, IsActive = true };
        definition.Steps.Add(new WorkflowStep { TenantId = tenant, WorkflowDefinitionId = definition.Id,
            Name = "Independent approval", IsRequired = true, StepType = WorkflowStepType.Approval });
        var decision = new QuantitySurveyConfigurationDecision
        { TenantId = tenant, ProfileId = profile.Id, DecisionKey = "QS-DEC-008", OwnerGroup = "QS",
          Status = QuantitySurveyConfigurationDecisionStatus.Approved,
          ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Approved,
          EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Verified,
          ValueJson = JsonSerializer.Serialize(new QsValuationCertificateValue {
              ValuationWorkflowDefinitionId = definition.Id, ValuationEvidenceMetadataTemplateId = template.Id,
              RequireContractorSubmission = requireContractor, RequireConsultantEndorsement = requireConsultant }) };
        db.AddRange(profile, template, entityType, definition, decision);
        // Placeholder optional decision is deliberately unapproved, like a freshly seeded profile.
        var externalDecision = new QuantitySurveyConfigurationDecision { TenantId = tenant, ProfileId = profile.Id,
            DecisionKey = "QS-DEC-013", OwnerGroup = "QS", ValueJson = "{}" };
        db.Add(externalDecision);
        await db.SaveChangesAsync();
        var service = new QuantitySurveyValuationWorksheetService(db, user.Object, projects.Object,
            Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IControlledFileUploadService>(), Mock.Of<ICentralDocumentRepositoryFileService>(),
            NullLogger<QuantitySurveyValuationWorksheetService>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["QuantitySurvey:OptionalFeatures"] = externalEnabled ? "external-submissions" : ""
            }).Build());
        if (allowed)
            (await service.GetLookupsAsync(Guid.NewGuid())).Should().NotBeNull();
        else
            await service.Invoking(x => x.GetLookupsAsync(Guid.NewGuid())).Should()
                .ThrowAsync<QuantitySurveyValuationWorksheetValidationException>().WithMessage("*Configure external submissions*");

        // A copied, configured extension must not turn on new intake, but a saved external
        // worksheet must retain that policy even after the deployment flag is turned off.
        externalDecision.Status = QuantitySurveyConfigurationDecisionStatus.Approved;
        externalDecision.ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Approved;
        externalDecision.EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Verified;
        externalDecision.ValueJson = JsonSerializer.Serialize(new QsExternalSubmissionValue {
            Channels = [Enum.GetValues<QuantitySurveyExternalSubmissionChannel>()[0]],
            AllowedFileExtensions = [".pdf"], RequirePortalIdentity = true, RequireEvidence = true, RequireSignature = true
        });
        await db.SaveChangesAsync();
        var resolver = typeof(QuantitySurveyValuationWorksheetService).GetMethod("ResolveValuationPolicyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        async Task<object> Resolve(QuantitySurveyValuationWorksheet? existing)
        {
            var pending = (Task)resolver.Invoke(service, [DateTime.UtcNow, CancellationToken.None, existing])!;
            await pending;
            return pending.GetType().GetProperty("Result")!.GetValue(pending)!;
        }
        void AssertExternal(object policy, bool enabled)
        {
            var valuation = (QsValuationCertificateValue)policy.GetType().GetProperty("Valuation")!.GetValue(policy)!;
            var external = (QsExternalSubmissionValue)policy.GetType().GetProperty("External")!.GetValue(policy)!;
            valuation.RequireContractorSubmission.Should().Be(enabled && requireContractor);
            valuation.RequireConsultantEndorsement.Should().Be(enabled && requireConsultant);
            external.RequirePortalIdentity.Should().Be(enabled);
            external.RequireSignature.Should().Be(enabled);
            external.RequireEvidence.Should().Be(enabled);
            // Internal measurement evidence is a separate mandatory policy, never disabled with the portal.
            valuation.RequireSupportingEvidence.Should().BeTrue();
        }
        AssertExternal(await Resolve(null), externalEnabled);
        AssertExternal(await Resolve(new QuantitySurveyValuationWorksheet { ConfigurationProfileId = profile.Id,
            ExternalSubmissionDecisionId = externalDecision.Id }), true);
        AssertExternal(await Resolve(new QuantitySurveyValuationWorksheet { ConfigurationProfileId = profile.Id }), false);

        user.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        await service.Invoking(x => x.GetLookupsAsync(Guid.NewGuid())).Should()
            .ThrowAsync<QuantitySurveyValuationWorksheetValidationException>().WithMessage("*No Published*");
    }
}
