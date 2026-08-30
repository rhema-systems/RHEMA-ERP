using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Projects;
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

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringConfigurationDecisionRegistryTests
{
    [Fact]
    public void RegistryMapsAllTrackerConfigurationKeys()
    {
        var definitions = CivilEngineeringConfigurationDecisionRegistry.Definitions;

        definitions.Should().HaveCount(14);
        definitions.Select(item => item.ConfigurationKey)
            .Should().Equal(Enumerable.Range(1, 14).Select(index => $"CIV-CFG-{index:000}"));
        definitions.Should().OnlyContain(item => item.Fields.Any(field => field.Name == "effectiveFrom"));
    }

    [Fact]
    public void SchemaUsesOnlyControlledInputsAndBoundLookups()
    {
        var allowed = new[] { "boolean", "number", "date", "select", "multiselect", "lookup", "multilookup" };
        var fields = CivilEngineeringConfigurationDecisionRegistry.ToDtos().SelectMany(item => item.Fields).ToList();

        fields.Should().OnlyContain(field => allowed.Contains(field.Control));
        fields.Where(field => field.Control is "select" or "multiselect")
            .Should().OnlyContain(field => field.Options.Count > 0);
        fields.Where(field => field.Control is "lookup" or "multilookup")
            .Should().OnlyContain(field => !string.IsNullOrWhiteSpace(field.LookupSource));
    }

    [Fact]
    public void EngineeringDocumentPolicyOffersEveryRequiredEngineeringFileFamily()
    {
        var policy = CivilEngineeringConfigurationDecisionRegistry.ToDtos()
            .Single(item => item.ConfigurationKey == "CIV-CFG-004");
        var extensions = policy.Fields.Single(item => item.Name == "allowedFileExtensions").Options
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        extensions.Should().Contain([".dwg", ".pro", ".rvt", ".std", ".pdf", ".docx", ".xlsx"]);
    }

    [Fact]
    public void EverySchemaFieldMapsToItsTypedValueAndEveryValuePropertyIsExposed()
    {
        foreach (var definition in CivilEngineeringConfigurationDecisionRegistry.Definitions)
        {
            var schemaNames = definition.Fields.Select(field => field.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var propertyNames = definition.ValueType.GetProperties().Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            schemaNames.SetEquals(propertyNames).Should().BeTrue(
                because: $"{definition.ConfigurationKey} must not persist hidden or unbound fields");
        }
    }

    [Fact]
    public void ValidationAcceptsTypedRoleAuthorityAndCanonicalizesItsValue()
    {
        using var value = JsonDocument.Parse(RolesAuthorityJson);

        var result = CivilEngineeringConfigurationDecisionRegistry.Validate(" civ-cfg-001 ", 1, value.RootElement);

        result.IsValid.Should().BeTrue();
        result.EffectiveFrom.Should().Be(new DateTime(2026, 8, 1));
        result.CanonicalJson.Should().Contain("operationalRoleIds");
    }

    [Fact]
    public void ValidationRejectsUnknownFieldsMissingSelectionsAndSchemaDrift()
    {
        using var unknown = JsonDocument.Parse(RolesAuthorityJson.Replace("}", ",\"uncontrolledText\":\"x\"}"));
        CivilEngineeringConfigurationDecisionRegistry.Validate("CIV-CFG-001", 1, unknown.RootElement)
            .IsValid.Should().BeFalse();

        using var missing = JsonDocument.Parse(RolesAuthorityJson.Replace("\"operationalRoleIds\":[\"11111111-1111-1111-1111-111111111111\"],", string.Empty));
        CivilEngineeringConfigurationDecisionRegistry.Validate("CIV-CFG-001", 1, missing.RootElement)
            .Errors.Should().Contain(message => message.Contains("Operational roles is required"));

        using var valid = JsonDocument.Parse(RolesAuthorityJson);
        CivilEngineeringConfigurationDecisionRegistry.Validate("CIV-CFG-001", 2, valid.RootElement)
            .Errors.Should().ContainSingle(message => message.Contains("schema version 1"));
    }

    [Fact]
    public void ValidationRejectsEmptyLookupAndInvalidEffectivePeriod()
    {
        using var emptyRole = JsonDocument.Parse(RolesAuthorityJson.Replace("11111111-1111-1111-1111-111111111111", "00000000-0000-0000-0000-000000000000"));
        CivilEngineeringConfigurationDecisionRegistry.Validate("CIV-CFG-001", 1, emptyRole.RootElement)
            .Errors.Should().Contain(message => message.Contains("invalid selection"));

        using var invalidDate = JsonDocument.Parse(RolesAuthorityJson.Replace("\"effectiveTo\":null", "\"effectiveTo\":\"2026-07-31\""));
        CivilEngineeringConfigurationDecisionRegistry.Validate("CIV-CFG-001", 1, invalidDate.RootElement)
            .Errors.Should().Contain(message => message.Contains("cannot be before"));
    }

    [Fact]
    public void WorkClassificationRequiresDefaultToBeAllowed()
    {
        using var value = JsonDocument.Parse("""
            {
              "effectiveFrom":"2026-08-01",
              "effectiveTo":null,
              "projectTypeIds":["aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"],
              "allowedClassifications":["constructionSupervision"],
              "defaultClassification":"newProjectDesign",
              "requireProjectReference":true,
              "requirePropertyReference":false,
              "requireLocationReference":true,
              "requirePlanningGisValidation":true
            }
            """);

        CivilEngineeringConfigurationDecisionRegistry.Validate("CIV-CFG-002", 1, value.RootElement)
            .Errors.Should().ContainSingle(message => message.Contains("Default classification"));
    }

    [Fact]
    public void VersionSequenceIncludesSoftDeletedRevisionsAndPublishedProfilesAreImmutable()
    {
        CivilEngineeringConfigurationLifecyclePolicy.NextVersion(
        [
            new CivilEngineeringConfigurationProfile { Version = 1 },
            new CivilEngineeringConfigurationProfile { Version = 2, IsDeleted = true }
        ]).Should().Be(3);

        var published = new CivilEngineeringConfigurationProfile
        {
            LifecycleStatus = CivilEngineeringConfigurationProfileStatus.Published
        };

        var edit = () => CivilEngineeringConfigurationLifecyclePolicy.EnsureEditable(published);
        edit.Should().Throw<CivilEngineeringConfigurationConflictException>().WithMessage("*immutable*");
    }

    [Fact]
    public async Task ServiceCreatesAllRegisteredDraftsAuditsAndEnforcesTenantIsolation()
    {
        await using var fixture = new ServiceFixture();
        var created = await fixture.Service.CreateProfileAsync(new CreateCivilEngineeringProfileRequest
        {
            Name = "Tenant A civil controls",
            EffectiveFrom = new DateTime(2026, 8, 1),
            IsDefault = true
        }, "civil-create");

        created.Decisions.Should().HaveCount(CivilEngineeringConfigurationDecisionRegistry.Definitions.Count);
        created.Decisions.Should().OnlyContain(item =>
            item.Status == CivilEngineeringConfigurationDecisionStatus.Draft &&
            item.ApprovalStatus == CivilEngineeringConfigurationApprovalStatus.Pending);
        var audit = (await fixture.Service.GetHistoryAsync(created.Id)).Should().ContainSingle().Subject;
        audit.Action.Should().Be(CivilEngineeringAuditEventMap.CreateProfile);
        audit.Operation.Should().Be(AuditOperationKind.Create);
        audit.SourceType.Should().Be("CivilEngineeringConfigurationProfile");
        audit.SourceId.Should().Be(created.Id);
        audit.ActorUserId.Should().Be(fixture.UserId);
        audit.CorrelationId.Should().Be("civil-create");

        fixture.TenantId = Guid.NewGuid();
        (await fixture.Service.GetProfilesAsync(new CivilEngineeringProfileListRequest())).Items.Should().BeEmpty();
        await fixture.Service.Invoking(service => service.GetProfileAsync(created.Id))
            .Should().ThrowAsync<CivilEngineeringConfigurationNotFoundException>();
    }

    [Fact]
    public async Task RecreatingAfterDeletedDraftContinuesHistoricalVersionFamily()
    {
        await using var fixture = new ServiceFixture();
        var first = await fixture.Service.CreateProfileAsync(new CreateCivilEngineeringProfileRequest
        {
            Name = "Initial civil policy",
            EffectiveFrom = new DateTime(2026, 8, 1),
            IsDefault = true
        }, "create-v1");
        await fixture.Service.DeleteDraftAsync(first.Id, new CivilEngineeringLifecycleRequest
        {
            RowVersion = first.RowVersion,
            Reason = "Discard incomplete draft"
        }, "delete-v1");

        var replacement = await fixture.Service.CreateProfileAsync(new CreateCivilEngineeringProfileRequest
        {
            Name = "Replacement civil policy",
            EffectiveFrom = new DateTime(2026, 9, 1),
            IsDefault = true
        }, "create-v2");

        replacement.Version.Should().Be(2);
        replacement.ProfileKey.Should().Be(first.ProfileKey);
        replacement.SupersedesProfileId.Should().Be(first.Id);
    }

    [Fact]
    public async Task SamePreparerCannotApproveButIndependentApproverCanWithCurrentDmsEvidence()
    {
        await using var fixture = new ServiceFixture();
        var profile = await fixture.Service.CreateProfileAsync(new CreateCivilEngineeringProfileRequest
        {
            Name = "Civil maker checker",
            EffectiveFrom = new DateTime(2026, 8, 1),
            IsDefault = true
        }, "maker-create");
        var decision = await fixture.Context.CivilEngineeringConfigurationDecisions
            .SingleAsync(item => item.ProfileId == profile.Id && item.ConfigurationKey == "CIV-CFG-001");
        decision.ValueJson = RolesAuthorityJson;
        decision.EffectiveFrom = new DateTime(2026, 8, 1);
        decision.Status = CivilEngineeringConfigurationDecisionStatus.Proposed;
        decision.ApprovalStatus = CivilEngineeringConfigurationApprovalStatus.Pending;
        decision.LastModifiedById = fixture.UserId;

        var record = new CentralDocumentRecord
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            DocumentReference = "CIV-EVID-001",
            Title = "Approved civil policy evidence",
            SourceModule = "DocumentManagement",
            SourceLabel = "Civil policy evidence",
            LifecycleStatus = "Active",
            VersionStatus = "Published",
            CurrentVersion = "v1.0",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        var version = new CentralDocumentVersion
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            DocumentRecordId = record.Id,
            DocumentRecord = record,
            VersionNumber = "v1.0",
            Status = "Published",
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        fixture.Context.CentralDocumentRecords.Add(record);
        fixture.Context.CentralDocumentVersions.Add(version);
        fixture.Context.CivilEngineeringConfigurationEvidenceLinks.Add(new CivilEngineeringConfigurationEvidenceLink
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            ProfileId = profile.Id,
            DecisionId = decision.Id,
            CentralDocumentRecordId = record.Id,
            CentralDocumentVersionId = version.Id,
            EvidenceType = "Policy",
            LinkedAt = DateTime.UtcNow,
            LinkedById = fixture.UserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });
        await fixture.Context.SaveChangesAsync();

        var request = new DecideCivilEngineeringDecisionRequest
        {
            RowVersion = string.Empty,
            ApprovalReference = "CIV-MINUTE-001"
        };
        await fixture.Service.Invoking(service => service.ApproveDecisionAsync(
                profile.Id, decision.ConfigurationKey, request, "same-user"))
            .Should().ThrowAsync<CivilEngineeringConfigurationConflictException>().WithMessage("*cannot approve*");

        fixture.UserId = Guid.NewGuid();
        var approved = await fixture.Service.ApproveDecisionAsync(
            profile.Id, decision.ConfigurationKey, request, "independent-user");
        approved.Status.Should().Be(CivilEngineeringConfigurationDecisionStatus.Approved);
        approved.EvidenceStatus.Should().Be(CivilEngineeringConfigurationEvidenceStatus.Verified);
    }

    [Fact]
    public async Task TenantSeederIsIdempotentAndCreatesOnlyUnapprovedDrafts()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenants = new[] { Guid.NewGuid(), Guid.NewGuid() };
        context.Tenants.AddRange(tenants.Select((id, index) => new Tenant
        {
            Id = id,
            Name = $"Tenant {index}",
            Code = $"CIV{index}",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        }));
        await context.SaveChangesAsync();
        var seeder = new CivilEngineeringConfigurationProfileSeeder(
            context,
            NullLogger<CivilEngineeringConfigurationProfileSeeder>.Instance);

        (await seeder.SeedAsync()).Should().Be(2);
        (await seeder.SeedAsync()).Should().Be(0);
        (await context.CivilEngineeringConfigurationProfiles.CountAsync()).Should().Be(2);
        (await context.CivilEngineeringConfigurationDecisions.CountAsync()).Should().Be(
            tenants.Length * CivilEngineeringConfigurationDecisionRegistry.Definitions.Count);
        (await context.CivilEngineeringConfigurationDecisions.ToListAsync()).Should().OnlyContain(item =>
            item.Status == CivilEngineeringConfigurationDecisionStatus.Draft &&
            item.ApprovalStatus == CivilEngineeringConfigurationApprovalStatus.Pending &&
            item.ValueJson == "{}");
    }

    [Fact]
    public async Task TenantSeederReconcilesNewDecisionKeysWithoutResettingTheExistingDraft()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenantId = Guid.NewGuid();
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Catalogue reconciliation tenant",
            Code = "CIVREC",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });
        await context.SaveChangesAsync();
        var seeder = new CivilEngineeringConfigurationProfileSeeder(
            context,
            NullLogger<CivilEngineeringConfigurationProfileSeeder>.Instance);

        (await seeder.SeedTenantAsync(tenantId)).Should().BeTrue();
        var profile = await context.CivilEngineeringConfigurationProfiles.SingleAsync();
        var preserved = await context.CivilEngineeringConfigurationDecisions.SingleAsync(item =>
            item.ProfileId == profile.Id && item.ConfigurationKey == "CIV-CFG-001");
        preserved.ValueJson = "{\"existing\":true}";
        var newlyRegistered = await context.CivilEngineeringConfigurationDecisions.SingleAsync(item =>
            item.ProfileId == profile.Id && item.ConfigurationKey == "CIV-CFG-014");
        context.CivilEngineeringConfigurationDecisions.Remove(newlyRegistered);
        await context.SaveChangesAsync();

        (await seeder.SeedTenantAsync(tenantId)).Should().BeTrue();
        (await seeder.SeedTenantAsync(tenantId)).Should().BeFalse();
        (await context.CivilEngineeringConfigurationProfiles.CountAsync()).Should().Be(1);
        (await context.CivilEngineeringConfigurationDecisions.CountAsync()).Should().Be(
            CivilEngineeringConfigurationDecisionRegistry.Definitions.Count);
        (await context.CivilEngineeringConfigurationDecisions.SingleAsync(item =>
            item.ProfileId == profile.Id && item.ConfigurationKey == "CIV-CFG-001")).ValueJson.Should().Be("{\"existing\":true}");
        var reconciled = await context.CivilEngineeringConfigurationDecisions.SingleAsync(item =>
            item.ProfileId == profile.Id && item.ConfigurationKey == "CIV-CFG-014");
        reconciled.Status.Should().Be(CivilEngineeringConfigurationDecisionStatus.Draft);
        reconciled.ApprovalStatus.Should().Be(CivilEngineeringConfigurationApprovalStatus.Pending);
        reconciled.EvidenceStatus.Should().Be(CivilEngineeringConfigurationEvidenceStatus.Missing);
        reconciled.ValueJson.Should().Be("{}");
        (await context.CivilEngineeringConfigurationRevisions.CountAsync(item =>
            item.ProfileId == profile.Id && item.Action == CivilEngineeringAuditEventMap.SeedDraft)).Should().Be(2);
    }

    [Fact]
    public async Task TenantSeederCreatesAnUnapprovedSuccessorInsteadOfMutatingAnImmutableProfile()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenantId = Guid.NewGuid();
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Immutable catalogue tenant",
            Code = "CIVIMM",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });
        await context.SaveChangesAsync();
        var seeder = new CivilEngineeringConfigurationProfileSeeder(
            context,
            NullLogger<CivilEngineeringConfigurationProfileSeeder>.Instance);
        (await seeder.SeedTenantAsync(tenantId)).Should().BeTrue();
        var published = await context.CivilEngineeringConfigurationProfiles.SingleAsync();
        published.LifecycleStatus = CivilEngineeringConfigurationProfileStatus.Published;
        var removed = await context.CivilEngineeringConfigurationDecisions.SingleAsync(item =>
            item.ProfileId == published.Id && item.ConfigurationKey == "CIV-CFG-014");
        context.CivilEngineeringConfigurationDecisions.Remove(removed);
        await context.SaveChangesAsync();

        (await seeder.SeedTenantAsync(tenantId)).Should().BeTrue();

        var profiles = await context.CivilEngineeringConfigurationProfiles.OrderBy(item => item.Version).ToListAsync();
        profiles.Should().HaveCount(2);
        profiles[0].LifecycleStatus.Should().Be(CivilEngineeringConfigurationProfileStatus.Published);
        profiles[0].Version.Should().Be(1);
        profiles[1].LifecycleStatus.Should().Be(CivilEngineeringConfigurationProfileStatus.Draft);
        profiles[1].Version.Should().Be(2);
        profiles[1].SupersedesProfileId.Should().Be(published.Id);
        var successorDecisions = await context.CivilEngineeringConfigurationDecisions
            .Where(item => item.ProfileId == profiles[1].Id)
            .ToListAsync();
        successorDecisions.Should().HaveCount(CivilEngineeringConfigurationDecisionRegistry.Definitions.Count);
        successorDecisions.Should().OnlyContain(item =>
            item.Status == CivilEngineeringConfigurationDecisionStatus.Draft &&
            item.ApprovalStatus == CivilEngineeringConfigurationApprovalStatus.Pending &&
            item.EvidenceStatus == CivilEngineeringConfigurationEvidenceStatus.Missing);
    }

    private sealed class ServiceFixture : IAsyncDisposable
    {
        private readonly Mock<ICurrentUserProvider> _currentUser = new();

        public ServiceFixture()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            _currentUser.SetupGet(item => item.UserId).Returns(() => UserId);
            _currentUser.SetupGet(item => item.TenantId).Returns(() => TenantId);
            _currentUser.SetupGet(item => item.Username).Returns("civil.test@tdc.local");
            _currentUser.SetupGet(item => item.FullName).Returns("Civil Test User");
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Roles).Returns(new[] { "TDC_CIVIL_ENGINEER" });
            Service = new CivilEngineeringConfigurationService(
                Context,
                _currentUser.Object,
                NullLogger<CivilEngineeringConfigurationService>.Instance);
        }

        public ApplicationDbContext Context { get; }
        public CivilEngineeringConfigurationService Service { get; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private const string RolesAuthorityJson = """
        {
          "effectiveFrom":"2026-08-01",
          "effectiveTo":null,
          "operationalRoleIds":["11111111-1111-1111-1111-111111111111"],
          "approvalRoleIds":["22222222-2222-2222-2222-222222222222"],
          "oversightRoleIds":["33333333-3333-3333-3333-333333333333"],
          "externalContributorRoleIds":["44444444-4444-4444-4444-444444444444"],
          "currencyCode":"GHS",
          "operationalAuthorityLimit":10000,
          "seniorAuthorityLimit":50000,
          "executiveAuthorityLimit":100000,
          "enforceProjectScope":true,
          "enforcePropertyScope":true,
          "enforceDepartmentScope":true
        }
        """;
}
