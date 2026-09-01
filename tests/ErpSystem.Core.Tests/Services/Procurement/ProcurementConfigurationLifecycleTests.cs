using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementConfigurationLifecyclePolicyTests
{
    [Fact]
    public void Draft_IsEditable_WhilePublishedAndRetiredAreImmutable()
    {
        var profile = new ProcurementConfigurationProfile { LifecycleStatus = ProcurementConfigurationProfileStatus.Draft };
        ProcurementConfigurationLifecyclePolicy.IsEditable(profile).Should().BeTrue();

        profile.LifecycleStatus = ProcurementConfigurationProfileStatus.Published;
        var publishedEdit = () => ProcurementConfigurationLifecyclePolicy.EnsureEditable(profile);
        publishedEdit.Should().Throw<ProcurementConfigurationConflictException>().WithMessage("*immutable*");

        profile.LifecycleStatus = ProcurementConfigurationProfileStatus.Retired;
        var retiredEdit = () => ProcurementConfigurationLifecyclePolicy.EnsureEditable(profile);
        retiredEdit.Should().Throw<ProcurementConfigurationConflictException>().WithMessage("*immutable*");
    }

    [Fact]
    public void VersionAndEffectiveSelectionRulesAreDeterministic()
    {
        var profiles = new[]
        {
            new ProcurementConfigurationProfile { Version = 1 },
            new ProcurementConfigurationProfile { Version = 4 }
        };
        ProcurementConfigurationLifecyclePolicy.GetNextVersion(profiles).Should().Be(5);

        var published = new ProcurementConfigurationProfile
        {
            LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc)
        };
        ProcurementConfigurationLifecyclePolicy.IsRuntimeEligible(published, new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
        ProcurementConfigurationLifecyclePolicy.IsRuntimeEligible(published, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
    }
}

public sealed class ProcurementConfigurationDecisionRegistryTests
{
    [Fact]
    public void RegistryContainsExactlyTheGovernedDecisionSet()
    {
        ProcurementConfigurationDecisionRegistry.Definitions.Select(item => item.DecisionKey)
            .Should().Equal(Enumerable.Range(1, 14).Select(index => $"DEC-{index:000}"));
        ProcurementConfigurationDecisionRegistry.Definitions.Should().OnlyContain(item =>
            item.SchemaVersion == 1 && item.RequiresApproval && item.RequiresEvidence && item.RequiresRenewedApproval);
    }

    [Fact]
    public void TypedValidationRejectsUnknownFieldsAndSchemaDrift()
    {
        using var wrongSchemaDocument = JsonDocument.Parse(ValidDec001Json);
        ProcurementConfigurationDecisionRegistry.Validate("DEC-001", 2, wrongSchemaDocument.RootElement)
            .Errors.Should().ContainSingle(message => message.Contains("schema version 1"));

        using var unknownFieldDocument = JsonDocument.Parse(ValidDec001Json.Replace("}", ",\"unexpected\":true}"));
        ProcurementConfigurationDecisionRegistry.Validate("DEC-001", 1, unknownFieldDocument.RootElement)
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void TypedValidationCanonicalizesAValidEffectiveDatedValue()
    {
        using var document = JsonDocument.Parse(ValidDec001Json);
        var result = ProcurementConfigurationDecisionRegistry.Validate("dec-001", 1, document.RootElement);
        result.IsValid.Should().BeTrue();
        result.EffectiveFrom.Should().Be(new DateTime(2026, 1, 1));
        result.CanonicalJson.Should().Contain("\"requestForQuotation\"");
    }

    [Fact]
    public void TypedValidationRejectsAnInvalidEffectivePeriod()
    {
        using var document = JsonDocument.Parse(ValidDec001Json
            .Replace("2026-01-01T00:00:00Z", "2027-01-01T00:00:00Z"));
        ProcurementConfigurationDecisionRegistry.Validate("DEC-001", 1, document.RootElement)
            .Errors.Should().Contain(message => message.Contains("cannot be before effective from"));
    }

    internal const string ValidDec001Json = """
        {
          "category":"goods",
          "serviceClass":"General goods",
          "method":"requestForQuotation",
          "currencyCode":"GHS",
          "lowerBound":0,
          "upperBound":100000,
          "lowerInclusive":true,
          "upperInclusive":true,
          "statutoryReference":"Public Procurement Act",
          "effectiveFrom":"2026-01-01T00:00:00Z",
          "effectiveTo":"2026-12-31T23:59:59Z"
        }
        """;
}

public sealed class ProcurementConfigurationServiceTests
{
    [Fact]
    public async Task CreateSeedsFourteenDraftDecisionsAndIsTenantScoped()
    {
        await using var fixture = new ServiceFixture("TenantAdmin");
        var created = await fixture.Service.CreateProfileAsync(NewProfileRequest(), "create-1");
        created.Decisions.Should().HaveCount(14);
        created.Decisions.Should().OnlyContain(item => item.Status == ProcurementConfigurationDecisionStatus.Draft);

        fixture.SwitchTenant(Guid.NewGuid());
        var otherTenant = await fixture.Service.GetProfilesAsync(new ProcurementConfigurationProfileListRequest());
        otherTenant.Items.Should().BeEmpty();
        await fixture.Service.Invoking(service => service.GetProfileAsync(created.Id))
            .Should().ThrowAsync<ProcurementConfigurationNotFoundException>();
        await fixture.Service.Invoking(service => service.UpdateProfileAsync(created.Id,
                new UpdateProcurementConfigurationProfileRequest
                {
                    Name = created.Name, EffectiveFrom = created.EffectiveFrom,
                    EffectiveTo = created.EffectiveTo, IsDefault = created.IsDefault, RowVersion = created.RowVersion
                }, "cross-tenant-update"))
            .Should().ThrowAsync<ProcurementConfigurationNotFoundException>();
        await fixture.Service.Invoking(service => service.CloneDraftAsync(created.Id,
                new CloneProcurementConfigurationProfileRequest(), "cross-tenant-clone"))
            .Should().ThrowAsync<ProcurementConfigurationNotFoundException>();
        await fixture.Service.Invoking(service => service.PublishProfileAsync(created.Id,
                new ProcurementConfigurationLifecycleRequest { RowVersion = created.RowVersion }, "cross-tenant-publish"))
            .Should().ThrowAsync<ProcurementConfigurationNotFoundException>();
    }

    [Fact]
    public async Task IncompleteProfileCannotPublishAndRejectedAttemptIsAudited()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var created = await fixture.Service.CreateProfileAsync(NewProfileRequest(), "create-2");

        await fixture.Service.Invoking(service => service.PublishProfileAsync(created.Id,
                new ProcurementConfigurationLifecycleRequest { RowVersion = created.RowVersion, Reason = "Premature publish" }, "publish-2"))
            .Should().ThrowAsync<ProcurementConfigurationValidationException>();

        var revisions = await fixture.Context.ProcurementConfigurationRevisions.Where(item => item.ProfileId == created.Id).ToListAsync();
        revisions.Should().Contain(item => item.Action == "Publish" && item.Result == "Rejected");
    }

    [Fact]
    public async Task PermissionAuthorizedConfigurationActorReachesPublicationValidationWithoutLegacyRoleRejection()
    {
        await using var fixture = new ServiceFixture(ProcurementAccessControlRegistry.IctAdministratorRole);
        var created = await fixture.Service.CreateProfileAsync(NewProfileRequest(), "create-3");

        await fixture.Service.Invoking(service => service.PublishProfileAsync(created.Id,
                new ProcurementConfigurationLifecycleRequest { RowVersion = created.RowVersion }, "publish-3"))
            .Should().ThrowAsync<ProcurementConfigurationValidationException>();

        var rejected = await fixture.Context.ProcurementConfigurationRevisions
            .Where(item => item.ProfileId == created.Id && item.Result == "Rejected")
            .Select(item => item.Action)
            .ToListAsync();
        rejected.Should().Contain("Publish");
    }

    [Fact]
    public async Task StaleProfileRowVersionIsRejectedBeforeMutation()
    {
        await using var fixture = new ServiceFixture("TenantAdmin");
        var created = await fixture.Service.CreateProfileAsync(NewProfileRequest(), "create-4");
        var tracked = await fixture.Context.ProcurementConfigurationProfiles.SingleAsync(item => item.Id == created.Id);
        tracked.RowVersion = new byte[] { 1, 2, 3 };
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.Invoking(service => service.UpdateProfileAsync(created.Id,
                new UpdateProcurementConfigurationProfileRequest
                {
                    Name = "Stale update",
                    EffectiveFrom = tracked.EffectiveFrom,
                    IsDefault = tracked.IsDefault,
                    RowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9 })
                }, "update-4"))
            .Should().ThrowAsync<ProcurementConfigurationConflictException>().WithMessage("*changed by another user*");
    }

    [Fact]
    public async Task CompleteProfilePublishes_CloneRetiresPriorAtomically_AndHistoryIsAuditable()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var first = await fixture.Service.CreateProfileAsync(NewProfileRequest(), "create-publish");
        await PrepareForPublicationAsync(fixture.Service, first.Id,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc));

        (await fixture.Service.ValidateProfileAsync(first.Id, "validate-first")).IsValid.Should().BeTrue();
        first = await fixture.Service.GetProfileAsync(first.Id);
        var publishedFirst = await fixture.Service.PublishProfileAsync(first.Id,
            new ProcurementConfigurationLifecycleRequest { RowVersion = first.RowVersion, Reason = "Approve first policy baseline" }, "publish-first");
        publishedFirst.LifecycleStatus.Should().Be(ProcurementConfigurationProfileStatus.Published);

        var clone = await fixture.Service.CloneDraftAsync(first.Id,
            new CloneProcurementConfigurationProfileRequest { ChangeSummary = "Second governed period" }, "clone-second");
        clone.Version.Should().Be(2);
        clone.Decisions.Should().OnlyContain(item => item.ApprovalStatus == ProcurementConfigurationApprovalStatus.Pending && item.Evidence.Count == 0);
        clone = await fixture.Service.UpdateProfileAsync(clone.Id,
            new UpdateProcurementConfigurationProfileRequest
            {
                Name = clone.Name,
                EffectiveFrom = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                IsDefault = clone.IsDefault,
                ChangeSummary = clone.ChangeSummary,
                RowVersion = clone.RowVersion,
                Reason = "Set second policy period"
            }, "update-clone");
        await PrepareForPublicationAsync(fixture.Service, clone.Id,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc));

        clone = await fixture.Service.GetProfileAsync(clone.Id);
        var publishedClone = await fixture.Service.PublishProfileAsync(clone.Id,
            new ProcurementConfigurationLifecycleRequest { RowVersion = clone.RowVersion, Reason = "Supersede first period" }, "publish-second");
        publishedClone.LifecycleStatus.Should().Be(ProcurementConfigurationProfileStatus.Published);
        (await fixture.Service.GetProfileAsync(first.Id)).LifecycleStatus.Should().Be(ProcurementConfigurationProfileStatus.Retired);
        (await fixture.Service.GetEffectiveProfileAsync("TDC-PROCUREMENT", new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc)))
            ?.Id.Should().Be(clone.Id);

        await fixture.Service.Invoking(service => service.PublishProfileAsync(clone.Id,
                new ProcurementConfigurationLifecycleRequest { RowVersion = publishedClone.RowVersion }, "republish-second"))
            .Should().ThrowAsync<ProcurementConfigurationConflictException>();

        await fixture.Service.RetireProfileAsync(clone.Id,
            new ProcurementConfigurationLifecycleRequest { RowVersion = publishedClone.RowVersion, Reason = "Close governed test period" }, "retire-second");
        var actions = (await fixture.Service.GetHistoryAsync(clone.Id)).Select(item => item.Action).ToHashSet();
        actions.Should().Contain(new[] { "CloneDraft", "UpdateProfile", "LinkEvidence", "UpdateDecision", "Publish", "Retire" });
    }

    [Fact]
    public async Task FutureDatedReplacementKeepsCurrentProfileEffectiveUntilCutover()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var now = DateTime.UtcNow;
        var currentFrom = now.AddDays(-30);
        var replacementFrom = now.AddDays(30);
        var effectiveTo = now.AddDays(90);
        var first = await fixture.Service.CreateProfileAsync(new CreateProcurementConfigurationProfileRequest
        {
            ProfileCode = "FUTURE-CONFIG",
            Name = "Future configuration cutover",
            EffectiveFrom = currentFrom,
            EffectiveTo = effectiveTo,
            IsDefault = true
        }, "create-current-config-cutover");
        await PrepareForPublicationAsync(fixture.Service, first.Id, currentFrom, effectiveTo);
        first = await fixture.Service.GetProfileAsync(first.Id);
        var publishedCurrent = await fixture.Service.PublishProfileAsync(first.Id,
            new ProcurementConfigurationLifecycleRequest { RowVersion = first.RowVersion, Reason = "Publish current configuration" },
            "publish-current-config-cutover");

        var replacement = await fixture.Service.CloneDraftAsync(publishedCurrent.Id,
            new CloneProcurementConfigurationProfileRequest { ChangeSummary = "Future configuration replacement" },
            "clone-future-config-cutover");
        replacement = await fixture.Service.UpdateProfileAsync(replacement.Id,
            new UpdateProcurementConfigurationProfileRequest
            {
                Name = replacement.Name,
                EffectiveFrom = replacementFrom,
                EffectiveTo = effectiveTo,
                IsDefault = replacement.IsDefault,
                ChangeSummary = replacement.ChangeSummary,
                RowVersion = replacement.RowVersion,
                Reason = "Set future configuration period"
            }, "date-future-config-cutover");
        await PrepareForPublicationAsync(fixture.Service, replacement.Id, replacementFrom, effectiveTo);
        replacement = await fixture.Service.GetProfileAsync(replacement.Id);
        var publishedReplacement = await fixture.Service.PublishProfileAsync(replacement.Id,
            new ProcurementConfigurationLifecycleRequest { RowVersion = replacement.RowVersion, Reason = "Schedule future configuration" },
            "publish-future-config-cutover");

        var currentAfterPublication = await fixture.Service.GetProfileAsync(publishedCurrent.Id);
        currentAfterPublication.LifecycleStatus.Should().Be(ProcurementConfigurationProfileStatus.Published);
        currentAfterPublication.EffectiveTo.Should().Be(replacementFrom.AddTicks(-1));
        publishedReplacement.LifecycleStatus.Should().Be(ProcurementConfigurationProfileStatus.Published);
        (await fixture.Service.GetEffectiveProfileAsync(publishedCurrent.ProfileCode, now))?.Id.Should().Be(publishedCurrent.Id);
        (await fixture.Service.GetEffectiveProfileAsync(publishedCurrent.ProfileCode, replacementFrom.AddTicks(1)))?.Id
            .Should().Be(publishedReplacement.Id);
        (await fixture.Service.GetHistoryAsync(publishedCurrent.Id)).Should().Contain(item =>
            item.Action == "ScheduleSupersession" && item.CorrelationId == "publish-future-config-cutover");
    }

    [Fact]
    public async Task CloneDraftAdvancesPastSoftDeletedVersionsWithoutTreatingThemAsActiveDrafts()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var first = await fixture.Service.CreateProfileAsync(NewProfileRequest(), "create-soft-delete-version");
        await PrepareForPublicationAsync(fixture.Service, first.Id,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc));
        first = await fixture.Service.GetProfileAsync(first.Id);
        var published = await fixture.Service.PublishProfileAsync(first.Id,
            new ProcurementConfigurationLifecycleRequest { RowVersion = first.RowVersion, Reason = "Publish clone source" },
            "publish-soft-delete-version");
        var deletedDraft = await fixture.Service.CloneDraftAsync(published.Id,
            new CloneProcurementConfigurationProfileRequest { ChangeSummary = "Discarded version" },
            "clone-soft-delete-version-2");
        await fixture.Service.DeleteDraftAsync(deletedDraft.Id,
            new ProcurementConfigurationLifecycleRequest { RowVersion = deletedDraft.RowVersion, Reason = "Discard draft" },
            "delete-soft-delete-version-2");

        var nextDraft = await fixture.Service.CloneDraftAsync(published.Id,
            new CloneProcurementConfigurationProfileRequest { ChangeSummary = "Replacement version" },
            "clone-soft-delete-version-3");

        nextDraft.Version.Should().Be(3);
        nextDraft.SupersedesProfileId.Should().Be(published.Id);
        (await fixture.Context.ProcurementConfigurationProfiles.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == deletedDraft.Id)).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DraftDeletionIsBlockedByEvidence_AndEligibleDeletionIsAudited()
    {
        await using var fixture = new ServiceFixture("TenantAdmin");
        var protectedDraft = await fixture.Service.CreateProfileAsync(NewProfileRequest(), "create-protected");
        var decision = protectedDraft.Decisions.Single(item => item.DecisionKey == "DEC-001");
        await fixture.Service.LinkEvidenceAsync(protectedDraft.Id, decision.DecisionKey,
            new LinkProcurementConfigurationEvidenceRequest
            {
                EvidenceType = "ExternalReference",
                ExternalReference = "TEST-EVIDENCE",
                DecisionRowVersion = decision.RowVersion
            }, "evidence-protected");
        protectedDraft = await fixture.Service.GetProfileAsync(protectedDraft.Id);
        await fixture.Service.Invoking(service => service.DeleteDraftAsync(protectedDraft.Id,
                new ProcurementConfigurationLifecycleRequest { RowVersion = protectedDraft.RowVersion }, "delete-protected"))
            .Should().ThrowAsync<ProcurementConfigurationConflictException>().WithMessage("*evidence or workflow dependencies*");

        fixture.SwitchTenant(Guid.NewGuid());
        var deletable = await fixture.Service.CreateProfileAsync(new CreateProcurementConfigurationProfileRequest
        {
            ProfileCode = "DELETE-TEST", Name = "Deletable draft", EffectiveFrom = DateTime.UtcNow.Date
        }, "create-delete");
        await fixture.Service.DeleteDraftAsync(deletable.Id,
            new ProcurementConfigurationLifecycleRequest { RowVersion = deletable.RowVersion, Reason = "Test eligible deletion" }, "delete-eligible");
        (await fixture.Context.ProcurementConfigurationRevisions.IgnoreQueryFilters()
            .AnyAsync(item => item.ProfileId == deletable.Id && item.Action == "DeleteDraft" && item.Result == "Succeeded"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task PermissionAuthorizedConfigurationActorCanApproveAndReturnDecisionForGovernedRework()
    {
        await using var fixture = new ServiceFixture(ProcurementAccessControlRegistry.IctAdministratorRole);
        var profile = await fixture.Service.CreateProfileAsync(NewProfileRequest(), "create-rework");
        var decision = profile.Decisions.Single(item => item.DecisionKey == "DEC-001");
        await fixture.Service.LinkEvidenceAsync(profile.Id, decision.DecisionKey,
            new LinkProcurementConfigurationEvidenceRequest
            {
                EvidenceType = "ExternalReference",
                ExternalReference = "TEST-REWORK-EVIDENCE",
                DecisionRowVersion = decision.RowVersion
            }, "evidence-rework");
        decision = (await fixture.Service.GetProfileAsync(profile.Id)).Decisions.Single(item => item.DecisionKey == "DEC-001");
        using var document = JsonDocument.Parse(ProcurementConfigurationDecisionRegistryTests.ValidDec001Json);
        var approved = await fixture.Service.SaveDecisionAsync(profile.Id, decision.DecisionKey,
            new SaveProcurementConfigurationDecisionRequest
            {
                SchemaVersion = 1,
                OwnerGroup = decision.OwnerGroup,
                Status = ProcurementConfigurationDecisionStatus.Approved,
                ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved,
                Value = document.RootElement.Clone(),
                DecisionDate = DateTime.UtcNow,
                ApprovalReference = "MINUTE-REWORK",
                RowVersion = decision.RowVersion
            }, "approve-rework");

        approved.EvidenceStatus.Should().Be(ProcurementConfigurationEvidenceStatus.Verified);

        approved = (await fixture.Service.GetProfileAsync(profile.Id)).Decisions.Single(item => item.DecisionKey == "DEC-001");
        var returned = await fixture.Service.SaveDecisionAsync(profile.Id, decision.DecisionKey,
            new SaveProcurementConfigurationDecisionRequest
            {
                SchemaVersion = approved.SchemaVersion,
                OwnerGroup = approved.OwnerGroup,
                Status = ProcurementConfigurationDecisionStatus.Proposed,
                ApprovalStatus = ProcurementConfigurationApprovalStatus.Pending,
                Value = approved.Value,
                DecisionDate = approved.DecisionDate,
                ApprovalReference = approved.ApprovalReference,
                SourceLineage = approved.SourceLineage,
                Notes = approved.Notes,
                RowVersion = approved.RowVersion,
                Reason = "Governed rework required"
            }, "return-rework");

        returned.Status.Should().Be(ProcurementConfigurationDecisionStatus.Proposed);
        returned.ApprovalStatus.Should().Be(ProcurementConfigurationApprovalStatus.Pending);
        returned.EvidenceStatus.Should().Be(ProcurementConfigurationEvidenceStatus.Attached);
        returned.ApprovedById.Should().BeNull();
        (await fixture.Context.ProcurementConfigurationRevisions
            .AnyAsync(item => item.ProfileId == profile.Id &&
                              item.Action == "ReturnDecisionToProposed" &&
                              item.Result == "Succeeded"))
            .Should().BeTrue();
    }

    private static readonly JsonSerializerOptions DecisionJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
    };

    private static async Task PrepareForPublicationAsync(
        ProcurementConfigurationService service,
        Guid profileId,
        DateTime effectiveFrom,
        DateTime effectiveTo)
    {
        foreach (var definition in ProcurementConfigurationDecisionRegistry.Definitions)
        {
            var current = (await service.GetProfileAsync(profileId)).Decisions.Single(item => item.DecisionKey == definition.DecisionKey);
            await service.LinkEvidenceAsync(profileId, definition.DecisionKey,
                new LinkProcurementConfigurationEvidenceRequest
                {
                    EvidenceType = "ExternalReference",
                    ExternalReference = $"TEST-{definition.DecisionKey}",
                    DecisionRowVersion = current.RowVersion,
                    Reason = "Attach governed test evidence"
                }, $"evidence-{definition.DecisionKey}");
            current = (await service.GetProfileAsync(profileId)).Decisions.Single(item => item.DecisionKey == definition.DecisionKey);
            var request = new SaveProcurementConfigurationDecisionRequest
                {
                    SchemaVersion = definition.SchemaVersion,
                    OwnerGroup = definition.OwnerGroup,
                    Status = ProcurementConfigurationDecisionStatus.Approved,
                    ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved,
                    Value = CreateValidDecisionValue(definition.DecisionKey, effectiveFrom, effectiveTo),
                    DecisionDate = effectiveFrom,
                    ApprovalReference = $"MINUTE-{definition.DecisionKey}",
                    SourceLineage = "TDC-0001 automated publication fixture",
                    RowVersion = current.RowVersion,
                    Reason = "Approve governed test decision"
                };
            try
            {
                await service.SaveDecisionAsync(profileId, definition.DecisionKey,
                    request, $"approve-{definition.DecisionKey}");
            }
            catch (ProcurementConfigurationValidationException exception)
            {
                var details = string.Join("; ", exception.Validation.Errors.Select(error => error.Message));
                throw new InvalidOperationException(
                    $"Fixture value for {definition.DecisionKey} is invalid: {details}", exception);
            }
        }
    }

    private static JsonElement CreateValidDecisionValue(string decisionKey, DateTime from, DateTime to)
    {
        EffectiveDatedDecisionValueDto value = decisionKey switch
        {
            "DEC-001" => new ProcurementMethodThresholdDecisionValueDto { Category = ProcurementCategoryClass.Goods, ServiceClass = "General goods", Method = ProcurementMethodType.RequestForQuotation, CurrencyCode = "GHS", LowerBound = 0, UpperBound = 100000, LowerInclusive = true, UpperInclusive = true, StatutoryReference = "Public Procurement Act" },
            "DEC-002" => new ProcurementAuthorityDecisionValueDto { AuthorityLevel = "Entity Tender Committee", CurrencyCode = "GHS", LowerBound = 0, UpperBound = 100000, LowerInclusive = true, UpperInclusive = true, EscalationAuthority = "Central Tender Review Committee", ApplicableCategories = new() { ProcurementCategoryClass.Goods } },
            "DEC-003" => new ProcurementWorkflowSelectionDecisionValueDto { TransactionEntityType = "PurchaseRequisition", PolicySelector = "TDC default", WorkflowDefinitionId = Guid.NewGuid(), ApplicabilityConditions = "All governed requisitions" },
            "DEC-004" => new ProcurementAuthorityStageDecisionValueDto { AuthorityOrCommittee = "Entity Tender Committee", RoleType = "Committee", Quorum = 3, EvidenceRequirements = new() { "Signed minutes" }, MinimumAmount = 0, MaximumAmount = 100000, ApplicableCategories = new() { ProcurementCategoryClass.Goods }, Sequence = 1, StageGroup = "Approval", EscalationAuthority = "Managing Director" },
            "DEC-005" => new ProcurementPettyPurchaseDecisionValueDto { PettyThreshold = 5000, CurrencyCode = "GHS", WaiverEligible = false, JustificationRequired = true, EvidenceRequirements = new() { "Receipt" }, ApproverRole = "Finance Manager", ExpiryDate = to },
            "DEC-006" => new ProcurementExceptionPrerequisiteDecisionValueDto { Method = ProcurementMethodType.SingleSource, Prerequisites = new() { "Statutory justification" }, ApprovalAuthority = "PPA", MandatoryEvidenceChecklist = new() { "Approval letter" }, FilingReference = "PPA filing", ExpiryDate = to },
            "DEC-007" => new ProcurementSupplierFeeDecisionValueDto { FeeType = "Registration", Amount = 100, CurrencyCode = "GHS", TaxPercent = 0, PaymentChannels = new() { "Bank" }, RevenueAccountId = Guid.NewGuid(), ReceiptNumberFormat = "FEE-{YYYY}-{####}", ExemptionRule = "Written approval", RefundRule = "No refund after review", RenewalRule = "Annual renewal" },
            "DEC-008" => new ProcurementSignatureDecisionValueDto { DocumentType = "PurchaseOrder", SignatureMode = ProcurementSignatureMode.ElectronicOrManualEvidence, SignatoryRoles = new() { "Managing Director" }, SigningOrder = 1, VerificationRule = "Validate shared signature evidence", EvidenceRequirements = new() { "Signed document" } },
            "DEC-009" => new ProcurementGhanepsDecisionValueDto { ProfileCode = "TDC-GHANEPS", FileTemplateMappings = new() { "Plan=APP" }, Frequency = "Daily", Owner = "Procurement ICT", AcknowledgementRule = "Record acknowledgement", ReconciliationRule = "Daily exception reconciliation" },
            "DEC-010" => new ProcurementNegativeStockDecisionValueDto { DefaultPolicy = ProcurementNegativeStockPolicy.Prohibited, EmergencyOverrideEligible = false, OverridePermission = "Inventory.EmergencyOverride", EvidenceRequirements = new() { "Emergency authority" }, OverrideDurationHours = 1, AuditRequired = true },
            "DEC-011" => new ProcurementSupplierRiskDecisionValueDto { ReviewFrequencyMonths = 12, ExposureWindowMonths = 12, RiskDimensions = new() { "FinancialStability=50", "Compliance=50" }, RiskBands = new() { "High=0-60", "Low=60-100" }, ConcentrationLimitPercent = 40, MinimumScore = 60, EligibilityAction = ProcurementSupplierRiskEligibilityAction.AwardHardStop, PerformanceWindowMonths = 12, PerformanceDimensions = new() { "DeliveryTimeliness=15", "GrnQuality=15", "RejectionRate=15", "PriceCompetitiveness=15", "Responsiveness=10", "ComplaintResolution=10", "ContractCompletion=20" }, PerformanceBands = new() { "Unsatisfactory=0-50", "ImprovementRequired=50-75", "Satisfactory=75-100" }, MinimumPerformanceDataCoveragePercent = 60, ResponseTargetHours = 48, PerformanceEligibilityAction = ProcurementSupplierRiskEligibilityAction.AwardHardStop },
            "DEC-012" => new ProcurementCutoverDecisionValueDto { CutoverDate = from, DualRunPeriodDays = 14, DataOwner = "Procurement Director", AcceptanceSignatories = new() { "Steering Committee" }, ReleaseStatus = "Approved", EvidenceRequirements = new() { "Signed acceptance" } },
            "DEC-013" => new ProcurementReceiptDocumentDecisionValueDto { DocumentType = ProcurementReceiptDocumentType.GrnAndMrn, ApplicabilityRule = "GRN for goods; MRN for materials", CoexistenceRule = ProcurementReceiptCoexistenceRule.BothFromSingleReceipt, NumberFormat = "{TYPE}-{YYYY}-{####}", TemplateReference = "TDC-{TYPE}", SignatureRequirements = new() { "Stores", "Internal Audit" }, EvidenceRequirements = new() { "Delivery note" } },
            "DEC-014" => new ProcurementNonFunctionalDecisionValueDto { WorkloadScenario = "500 concurrent users", AvailabilityTargetPercent = 99.5m, ResponseTargetMilliseconds = 3000, BackupFrequencyHours = 24, RpoMinutes = 60, RtoMinutes = 240, AuthenticationTarget = "MFA for privileged access", MonitoringTarget = "Central telemetry", UsabilityTarget = "Task completion testing", AccessibilityTarget = "WCAG 2.1 AA", AcceptanceMethod = "Signed performance and security report" },
            _ => throw new ArgumentOutOfRangeException(nameof(decisionKey), decisionKey, "Unknown decision key")
        };
        value.EffectiveFrom = from;
        value.EffectiveTo = to;
        return JsonSerializer.SerializeToElement(value, value.GetType(), DecisionJsonOptions);
    }

    private static CreateProcurementConfigurationProfileRequest NewProfileRequest() => new()
    {
        ProfileCode = "TDC-PROCUREMENT",
        Name = "TDC Procurement Policy",
        EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EffectiveTo = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
        IsDefault = true
    };

    private sealed class ServiceFixture : IAsyncDisposable
    {
        private Guid _tenantId = Guid.NewGuid();
        private readonly HashSet<string> _roles;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly UnitOfWork _unitOfWork;

        public ServiceFixture(params string[] roles)
        {
            _roles = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            _currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
            _currentUser.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            _currentUser.SetupGet(item => item.Username).Returns("procurement.test@tdc.local");
            _currentUser.SetupGet(item => item.FullName).Returns("Procurement Test User");
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Roles).Returns(() => _roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));
            _unitOfWork = new UnitOfWork(Context);
            Service = new ProcurementConfigurationService(_unitOfWork, _currentUser.Object, NullLogger<ProcurementConfigurationService>.Instance);
        }

        public ApplicationDbContext Context { get; }
        public ProcurementConfigurationService Service { get; }
        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;
        public void SetRoles(params string[] roles)
        {
            _roles.Clear();
            foreach (var role in roles) _roles.Add(role);
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}

public sealed class ProcurementConfigurationProfileSeederTests
{
    [Fact]
    public async Task SeederIsIdempotentAndDoesNotMutateExistingRuntimeSettings()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenantId = Guid.NewGuid();
        context.Tenants.Add(new Tenant { Id = tenantId, Name = "TDC", Code = "TDC", Status = TenantStatus.Active, CreatedBy = "Tests" });
        context.ProcurementSettings.Add(new ProcurementSettings
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AutoCreateInventoryItems = true,
            RequireApprovalForPO = false, Notes = "runtime-baseline", CreatedBy = "Tests"
        });
        await context.SaveChangesAsync();

        var seeder = new ProcurementConfigurationProfileSeeder(context, NullLogger<ProcurementConfigurationProfileSeeder>.Instance);
        (await seeder.SeedAsync()).Should().Be(1);
        (await seeder.SeedAsync()).Should().Be(0);

        (await context.ProcurementConfigurationProfiles.CountAsync(item => item.TenantId == tenantId)).Should().Be(1);
        (await context.ProcurementConfigurationDecisions.CountAsync(item => item.TenantId == tenantId)).Should().Be(14);
        var settings = await context.ProcurementSettings.SingleAsync(item => item.TenantId == tenantId);
        settings.AutoCreateInventoryItems.Should().BeTrue();
        settings.RequireApprovalForPO.Should().BeFalse();
        settings.Notes.Should().Be("runtime-baseline");
    }
}
