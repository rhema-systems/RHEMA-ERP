using System.Text.Json;
using System.Text.Json.Nodes;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Data.Services;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyConfigurationRegistryTests
{
    [Fact]
    public void RegisterMapsAllSeventeenDecisionsToConfigurationKeys()
    {
        var definitions = QuantitySurveyConfigurationDecisionRegistry.Definitions;

        definitions.Should().HaveCount(17);
        definitions.Select(item => item.DecisionKey)
            .Should().Equal(Enumerable.Range(1, 17).Select(index => $"QS-DEC-{index:000}"));
        definitions.Select(item => item.ConfigurationKey)
            .Should().Equal(Enumerable.Range(1, 17).Select(index => $"QS-CFG-{index:000}"));
        definitions.Should().OnlyContain(item => item.Fields.Any(field => field.Name == "effectiveFrom"));
    }

    [Fact]
    public void SchemasExposeOnlyControlledInputs()
    {
        var allowed = new[] { "boolean", "number", "date", "select", "multiselect", "lookup", "multilookup" };

        QuantitySurveyConfigurationDecisionRegistry.ToDtos()
            .SelectMany(item => item.Fields)
            .Should().OnlyContain(field => allowed.Contains(field.Control));
    }

    [Fact]
    public void EnumSchemaOptionsMatchCanonicalStoredJsonValues()
    {
        var standard = QuantitySurveyConfigurationDecisionRegistry.ToDtos()
            .Single(item => item.DecisionKey == "QS-DEC-002")
            .Fields.Single(field => field.Name == "defaultStandard");

        standard.Options.Should().Contain("smm7").And.Contain("tdcLocal");
        standard.Options.Should().NotContain("Smm7").And.NotContain("TdcLocal");
    }

    [Fact]
    public void ValidationRejectsUnknownFieldsAndSchemaDrift()
    {
        using var unknown = JsonDocument.Parse(ValidRateBuildUpJson.Replace("}", ",\"unsupportedFreeText\":\"x\"}"));
        QuantitySurveyConfigurationDecisionRegistry.Validate("QS-DEC-004", 1, unknown.RootElement)
            .IsValid.Should().BeFalse();

        using var valid = JsonDocument.Parse(ValidRateBuildUpJson);
        QuantitySurveyConfigurationDecisionRegistry.Validate("QS-DEC-004", 2, valid.RootElement)
            .Errors.Should().ContainSingle(message => message.Contains("schema version 1"));
    }

    [Fact]
    public void ValidationAcceptsTypedRateBuildUpAndRejectsMissingRequiredSelections()
    {
        using var valid = JsonDocument.Parse(ValidRateBuildUpJson);
        var result = QuantitySurveyConfigurationDecisionRegistry.Validate("qs-dec-004", 1, valid.RootElement);
        result.IsValid.Should().BeTrue();
        result.EffectiveFrom.Should().Be(new DateTime(2026, 8, 1));

        var incompleteNode = JsonNode.Parse(ValidRateBuildUpJson)!.AsObject();
        incompleteNode.Remove("decimalPlaces");
        using var incomplete = JsonDocument.Parse(incompleteNode.ToJsonString());
        QuantitySurveyConfigurationDecisionRegistry.Validate("QS-DEC-004", 1, incomplete.RootElement)
            .Errors.Should().Contain(message => message.Contains("Decimal places is required"));
    }

    [Fact]
    public void RetentionPolicyRejectsStagePercentagesAboveOneHundredPercent()
    {
        var value = JsonSerializer.SerializeToElement(new
        {
            effectiveFrom = new DateTime(2026, 8, 1),
            effectiveTo = (DateTime?)null,
            maximumRetentionPercent = 10m,
            practicalCompletionReleasePercent = 50m,
            sectionalTakeoverReleasePercent = 30m,
            defectsReleasePercent = 30m,
            defectsLiabilityDays = 365,
            approvalWorkflowDefinitionId = Guid.NewGuid(),
            allowRetentionBond = false
        });

        QuantitySurveyConfigurationDecisionRegistry.Validate("QS-DEC-009", 1, value)
            .Errors.Should().ContainSingle(message => message.Contains("cannot exceed 100%"));
    }

    [Fact]
    public void VersionSequenceIncludesSoftDeletedRevisionsSuppliedByCaller()
    {
        var versions = new[]
        {
            new QuantitySurveyConfigurationProfile { Version = 1 },
            new QuantitySurveyConfigurationProfile { Version = 2, IsDeleted = true }
        };

        QuantitySurveyConfigurationLifecyclePolicy.NextVersion(versions).Should().Be(3);
    }

    [Fact]
    public void PublishedAndRetiredProfilesAreImmutable()
    {
        foreach (var status in new[] { QuantitySurveyConfigurationProfileStatus.Published, QuantitySurveyConfigurationProfileStatus.Retired })
        {
            var profile = new QuantitySurveyConfigurationProfile { LifecycleStatus = status };
            var action = () => QuantitySurveyConfigurationLifecyclePolicy.EnsureEditable(profile);
            action.Should().Throw<QuantitySurveyConfigurationConflictException>().WithMessage("*immutable*");
        }
    }

    [Fact]
    public async Task ServiceIsTenantScopedAndSeedsAllDecisionRows()
    {
        await using var fixture = new ServiceFixture();
        var created = await fixture.Service.CreateProfileAsync(new CreateQuantitySurveyProfileRequest
        {
            Name = "Tenant A QS policy", EffectiveFrom = new DateTime(2026, 8, 1), IsDefault = true
        }, "tenant-a-create");

        created.Decisions.Should().HaveCount(17);
        created.Decisions.Should().OnlyContain(item => item.Status == QuantitySurveyConfigurationDecisionStatus.Draft);
        var audit = (await fixture.Service.GetHistoryAsync(created.Id)).Should().ContainSingle().Subject;
        audit.Action.Should().Be(QuantitySurveyAuditEventMap.CreateProfile);
        audit.Operation.Should().Be(AuditOperationKind.Create);
        audit.SourceType.Should().Be("QuantitySurveyConfigurationProfile");
        audit.SourceId.Should().Be(created.Id);
        audit.ActorUserId.Should().Be(fixture.UserId);
        audit.ActorRoles.Should().Contain("TDC_QUANTITY_SURVEYOR");
        audit.CorrelationId.Should().Be("tenant-a-create");
        audit.After.Should().NotBeNull();

        fixture.TenantId = Guid.NewGuid();
        (await fixture.Service.GetProfilesAsync(new QuantitySurveyProfileListRequest())).Items.Should().BeEmpty();
        await fixture.Service.Invoking(service => service.GetProfileAsync(created.Id))
            .Should().ThrowAsync<QuantitySurveyConfigurationNotFoundException>();
    }

    [Fact]
    public async Task RecreatingAfterDeletedOnlyDraftContinuesTheHistoricalVersionFamily()
    {
        await using var fixture = new ServiceFixture();
        var first = await fixture.Service.CreateProfileAsync(new CreateQuantitySurveyProfileRequest
        {
            Name = "Initial QS policy", EffectiveFrom = new DateTime(2026, 8, 1), IsDefault = true
        }, "create-v1");
        await fixture.Service.DeleteDraftAsync(first.Id, new QuantitySurveyLifecycleRequest
        {
            RowVersion = first.RowVersion,
            Reason = "Discard incomplete draft"
        }, "delete-v1");

        var replacement = await fixture.Service.CreateProfileAsync(new CreateQuantitySurveyProfileRequest
        {
            Name = "Replacement QS policy", EffectiveFrom = new DateTime(2026, 9, 1), IsDefault = true
        }, "create-v2");

        replacement.Version.Should().Be(2);
        replacement.ProfileKey.Should().Be(first.ProfileKey);
        replacement.SupersedesProfileId.Should().Be(first.Id);
    }

    [Fact]
    public async Task CloneCreatesDraftDecisionsAndRejectsAnInvalidEffectivePeriod()
    {
        await using var fixture = new ServiceFixture();
        var source = await fixture.Service.CreateProfileAsync(new CreateQuantitySurveyProfileRequest
        {
            Name = "Published source", EffectiveFrom = new DateTime(2026, 8, 1),
            EffectiveTo = new DateTime(2026, 12, 31), IsDefault = true
        }, "create-source");
        var sourceEntity = await fixture.Context.QuantitySurveyConfigurationProfiles
            .SingleAsync(item => item.Id == source.Id);
        sourceEntity.LifecycleStatus = QuantitySurveyConfigurationProfileStatus.Published;
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.Invoking(service => service.CloneDraftAsync(
                source.Id,
                new CloneQuantitySurveyProfileRequest
                {
                    EffectiveFrom = new DateTime(2027, 1, 1),
                    ChangeSummary = "Invalid future clone"
                },
                "invalid-clone"))
            .Should().ThrowAsync<QuantitySurveyConfigurationValidationException>();

        var clone = await fixture.Service.CloneDraftAsync(
            source.Id,
            new CloneQuantitySurveyProfileRequest
            {
                EffectiveFrom = new DateTime(2026, 9, 1),
                ChangeSummary = "Prepare replacement"
            },
            "valid-clone");

        clone.Version.Should().Be(2);
        clone.Decisions.Should().OnlyContain(item =>
            item.Status == QuantitySurveyConfigurationDecisionStatus.Draft &&
            item.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Pending &&
            item.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Missing);
    }

    [Fact]
    public async Task SamePreparerCannotApproveButIndependentApproverCanWithCurrentDmsEvidence()
    {
        await using var fixture = new ServiceFixture();
        var created = await fixture.Service.CreateProfileAsync(new CreateQuantitySurveyProfileRequest
        {
            Name = "Maker checker", EffectiveFrom = new DateTime(2026, 8, 1), IsDefault = true
        }, "maker-create");
        var decision = await fixture.Context.QuantitySurveyConfigurationDecisions
            .SingleAsync(item => item.ProfileId == created.Id && item.DecisionKey == "QS-DEC-004");
        decision.ValueJson = ValidRateBuildUpJson;
        decision.EffectiveFrom = new DateTime(2026, 8, 1);
        decision.Status = QuantitySurveyConfigurationDecisionStatus.Proposed;
        decision.ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Pending;
        decision.LastModifiedById = fixture.UserId;

        var record = new CentralDocumentRecord
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, DocumentReference = "QS-EVID-001", Title = "Approved QS policy",
            SourceModule = "DocumentManagement", SourceLabel = "QS policy evidence", LifecycleStatus = "Active",
            VersionStatus = "Published", CurrentVersion = "v1.0", CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        };
        var version = new CentralDocumentVersion
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, DocumentRecordId = record.Id, DocumentRecord = record,
            VersionNumber = "v1.0", Status = "Published", PublishedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        };
        fixture.Context.CentralDocumentRecords.Add(record);
        fixture.Context.CentralDocumentVersions.Add(version);
        fixture.Context.QuantitySurveyConfigurationEvidenceLinks.Add(new QuantitySurveyConfigurationEvidenceLink
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, ProfileId = created.Id, DecisionId = decision.Id,
            CentralDocumentRecordId = record.Id, CentralDocumentVersionId = version.Id, EvidenceType = "Policy",
            LinkedAt = DateTime.UtcNow, LinkedById = fixture.UserId, CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        });
        await fixture.Context.SaveChangesAsync();

        var request = new DecideQuantitySurveyDecisionRequest { RowVersion = string.Empty, ApprovalReference = "QS-MINUTE-001" };
        await fixture.Service.Invoking(service => service.ApproveDecisionAsync(created.Id, decision.DecisionKey, request, "same-user"))
            .Should().ThrowAsync<QuantitySurveyConfigurationConflictException>().WithMessage("*cannot approve*");

        fixture.UserId = Guid.NewGuid();
        var approved = await fixture.Service.ApproveDecisionAsync(created.Id, decision.DecisionKey, request, "independent-user");
        approved.Status.Should().Be(QuantitySurveyConfigurationDecisionStatus.Approved);
        approved.EvidenceStatus.Should().Be(QuantitySurveyConfigurationEvidenceStatus.Verified);

        // One approved process is publishable; the sixteen unused questionnaire decisions
        // remain present for future configuration without blocking this profile.
        (await fixture.Service.ValidateProfileAsync(created.Id)).IsValid.Should().BeTrue();
        var published = await fixture.Service.PublishProfileAsync(created.Id,
            new QuantitySurveyLifecycleRequest { RowVersion = string.Empty, Reason = "Enable rate build-ups only" },
            "publish-configured-process");
        published.LifecycleStatus.Should().Be(QuantitySurveyConfigurationProfileStatus.Published);
        published.TotalDecisionCount.Should().Be(1);
        published.CompleteDecisionCount.Should().Be(1);
        published.IsComplete.Should().BeTrue();
        published.Decisions.Should().HaveCount(17);
    }

    [Fact]
    public async Task EmptyProfileCannotPublishAndConfiguredUnapprovedDecisionsRemainBlocking()
    {
        await using var fixture = new ServiceFixture();
        var profile = await fixture.Service.CreateProfileAsync(new CreateQuantitySurveyProfileRequest
        { Name = "Core QS", EffectiveFrom = new DateTime(2026, 8, 1) }, "create-core");
        profile.TotalDecisionCount.Should().Be(0);
        profile.IsComplete.Should().BeFalse();
        profile.Validation.Errors.Should().ContainSingle(error => error.Code == "EMPTY_PROFILE");

        var decision = await fixture.Context.QuantitySurveyConfigurationDecisions
            .SingleAsync(d => d.ProfileId == profile.Id && d.DecisionKey == "QS-DEC-004");
        decision.ValueJson = ValidRateBuildUpJson;
        decision.EffectiveFrom = profile.EffectiveFrom;
        await fixture.Context.SaveChangesAsync();
        var validation = await fixture.Service.ValidateProfileAsync(profile.Id);
        validation.Errors.Should().Contain(error => error.Code == "NOT_APPROVED");
        validation.Errors.Should().OnlyContain(error => error.DecisionKey == "QS-DEC-004");

        decision.ValueJson = "{invalid-json";
        await fixture.Context.SaveChangesAsync();
        (await fixture.Service.ValidateProfileAsync(profile.Id)).Errors
            .Should().Contain(error => error.Code == "INVALID_VALUE");
    }

    [Fact]
    public async Task BoqPolicyRejectsWrongEntityWorkflowAndAcceptsMatchingPublishedDefinitions()
    {
        await using var fixture = new ServiceFixture();
        var boqEntityType = new WorkflowEntityType
        {
            TenantId = fixture.TenantId,
            Code = QuantitySurveyWorkflowBindingRegistry.Boq,
            Name = "Quantity survey BoQ",
            IsActive = true,
            CreatedBy = "Tests"
        };
        var estimateEntityType = new WorkflowEntityType
        {
            TenantId = fixture.TenantId,
            Code = QuantitySurveyWorkflowBindingRegistry.Estimate,
            Name = "Quantity survey estimate",
            IsActive = true,
            CreatedBy = "Tests"
        };
        var boqWorkflow = PublishedWorkflow(fixture.TenantId, boqEntityType, "BoQ approval");
        var estimateWorkflow = PublishedWorkflow(fixture.TenantId, estimateEntityType, "Estimate approval");
        fixture.Context.WorkflowEntityTypes.AddRange(boqEntityType, estimateEntityType);
        fixture.Context.WorkflowDefinitions.AddRange(boqWorkflow, estimateWorkflow);
        await fixture.Context.SaveChangesAsync();

        var profile = await fixture.Service.CreateProfileAsync(new CreateQuantitySurveyProfileRequest
        {
            Name = "Workflow constrained QS policy",
            EffectiveFrom = new DateTime(2026, 8, 1),
            IsDefault = true
        }, "workflow-profile-create");
        var decision = profile.Decisions.Single(item => item.DecisionKey == "QS-DEC-003");

        var wrongRequest = BoqPolicyRequest(
            decision.RowVersion,
            boqWorkflowDefinitionId: estimateWorkflow.Id,
            estimateWorkflowDefinitionId: estimateWorkflow.Id);
        var failure = await fixture.Service.Invoking(service => service.SaveDecisionAsync(
                profile.Id,
                decision.DecisionKey,
                wrongRequest,
                "wrong-workflow-entity"))
            .Should().ThrowAsync<QuantitySurveyConfigurationValidationException>();
        failure.Which.Validation.Errors.Should().ContainSingle(issue =>
            issue.DecisionKey == decision.DecisionKey &&
            issue.Message.Contains("wrong-entity", StringComparison.OrdinalIgnoreCase));

        var saved = await fixture.Service.SaveDecisionAsync(
            profile.Id,
            decision.DecisionKey,
            BoqPolicyRequest(decision.RowVersion, boqWorkflow.Id, estimateWorkflow.Id),
            "matching-workflow-entities");

        saved.Value.GetProperty("boqWorkflowDefinitionId").GetGuid().Should().Be(boqWorkflow.Id);
        saved.Value.GetProperty("estimateWorkflowDefinitionId").GetGuid().Should().Be(estimateWorkflow.Id);
    }

    [Fact]
    public async Task TenantSeederIsIdempotentAndCreatesOnlyUnapprovedDrafts()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var context = new ApplicationDbContext(options);
        var tenants = new[] { Guid.NewGuid(), Guid.NewGuid() };
        context.Tenants.AddRange(tenants.Select((id, index) => new Tenant { Id = id, Name = $"Tenant {index}", Code = $"Q{index}", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "Tests" }));
        await context.SaveChangesAsync();
        var seeder = new QuantitySurveyConfigurationProfileSeeder(context, NullLogger<QuantitySurveyConfigurationProfileSeeder>.Instance);

        (await seeder.SeedAsync()).Should().Be(2);
        (await seeder.SeedAsync()).Should().Be(0);
        (await context.QuantitySurveyConfigurationProfiles.CountAsync()).Should().Be(2);
        (await context.QuantitySurveyConfigurationDecisions.CountAsync()).Should().Be(34);
        (await context.QuantitySurveyConfigurationDecisions.ToListAsync()).Should().OnlyContain(item =>
            item.Status == QuantitySurveyConfigurationDecisionStatus.Draft &&
            item.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Pending &&
            item.ValueJson == "{}");
    }

    private sealed class ServiceFixture : IAsyncDisposable
    {
        private readonly Mock<ICurrentUserProvider> _currentUser = new();

        public ServiceFixture()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Throw(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            TenantId = Guid.NewGuid(); UserId = Guid.NewGuid();
            _currentUser.SetupGet(item => item.UserId).Returns(() => UserId);
            _currentUser.SetupGet(item => item.TenantId).Returns(() => TenantId);
            _currentUser.SetupGet(item => item.Username).Returns("qs.test@tdc.local");
            _currentUser.SetupGet(item => item.FullName).Returns("QS Test User");
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Roles).Returns(new[] { "TDC_QUANTITY_SURVEYOR" });
            Service = new QuantitySurveyConfigurationService(Context, _currentUser.Object, NullLogger<QuantitySurveyConfigurationService>.Instance);
        }

        public ApplicationDbContext Context { get; }
        public QuantitySurveyConfigurationService Service { get; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private static WorkflowDefinition PublishedWorkflow(
        Guid tenantId,
        WorkflowEntityType entityType,
        string name) => new()
        {
            TenantId = tenantId,
            DefinitionKey = Guid.NewGuid(),
            Name = name,
            EntityTypeId = entityType.Id,
            EntityType = entityType,
            Version = 1,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
            IsActive = true,
            PublishedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };

    private static SaveQuantitySurveyDecisionRequest BoqPolicyRequest(
        string rowVersion,
        Guid boqWorkflowDefinitionId,
        Guid estimateWorkflowDefinitionId)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            effectiveFrom = "2026-08-01",
            effectiveTo = (string?)null,
            requiredVersionTypes = new[] { "original", "approved" },
            boqWorkflowDefinitionId,
            estimateWorkflowDefinitionId,
            approvedVersionsImmutable = true,
            requireWorkflowBeforeUse = true,
            requireLineLevelComparison = true
        }));
        return new SaveQuantitySurveyDecisionRequest
        {
            SchemaVersion = 1,
            Value = document.RootElement.Clone(),
            RowVersion = rowVersion,
            Reason = "Configure version approval workflows"
        };
    }

    private const string ValidRateBuildUpJson = """
        {
          "effectiveFrom":"2026-08-01",
          "effectiveTo":null,
          "components":["material","labour","plant","subcontract","overhead","profit"],
          "maximumOverheadPercent":15,
          "maximumProfitPercent":10,
          "maximumContingencyPercent":5,
          "maximumWastagePercent":3,
          "decimalPlaces":2
        }
        """;
}
