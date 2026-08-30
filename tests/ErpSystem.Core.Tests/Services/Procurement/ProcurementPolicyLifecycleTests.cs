using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPolicyLifecyclePolicyTests
{
    [Fact]
    public void LifecycleRulesKeepOnlyDraftEditableAndSelectPublishedEffectiveVersions()
    {
        var policy = new ProcurementPolicySet
        {
            LifecycleStatus = ProcurementPolicyLifecycleStatus.Draft,
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc)
        };

        ProcurementPolicyLifecyclePolicy.IsEditable(policy).Should().BeTrue();
        ProcurementPolicyLifecyclePolicy.GetNextVersion(new[]
        {
            new ProcurementPolicySet { Version = 1 },
            new ProcurementPolicySet { Version = 3 }
        }).Should().Be(4);

        policy.LifecycleStatus = ProcurementPolicyLifecycleStatus.Published;
        ProcurementPolicyLifecyclePolicy.IsRuntimeEligible(policy,
            new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
        ProcurementPolicyLifecyclePolicy.IsRuntimeEligible(policy,
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
        Action publishedEdit = () => ProcurementPolicyLifecyclePolicy.EnsureEditable(policy);
        publishedEdit.Should().Throw<ProcurementPolicyConflictException>().WithMessage("*immutable*");

        policy.LifecycleStatus = ProcurementPolicyLifecycleStatus.Retired;
        Action retiredEdit = () => ProcurementPolicyLifecyclePolicy.EnsureEditable(policy);
        retiredEdit.Should().Throw<ProcurementPolicyConflictException>().WithMessage("*immutable*");
    }
}

public sealed class ProcurementPolicyServiceTests
{
    private static readonly DateTime EffectiveFrom = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EffectiveTo = new(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public async Task CreateRequiresImmutableApprovedConfigurationAndMaterializesNormalizedRuleFamilies()
    {
        await using var fixture = new ServiceFixture("TenantAdmin");
        var draftSource = await fixture.AddSourceConfigurationAsync(ProcurementConfigurationProfileStatus.Draft, published: false);

        await fixture.Service.Invoking(service => service.CreatePolicySetAsync(NewPolicy(draftSource.Id), "create-invalid"))
            .Should().ThrowAsync<ProcurementPolicyValidationException>();

        var source = await fixture.AddSourceConfigurationAsync(ProcurementConfigurationProfileStatus.Retired, published: true);
        var created = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-valid");

        created.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Draft);
        created.SourceConfigurationProfileId.Should().Be(source.Id);
        created.Rules.Select(item => item.Kind).Should().Contain(new[]
        {
            ProcurementPolicyRuleKind.Category,
            ProcurementPolicyRuleKind.Method,
            ProcurementPolicyRuleKind.Threshold,
            ProcurementPolicyRuleKind.Authority,
            ProcurementPolicyRuleKind.Evidence,
            ProcurementPolicyRuleKind.Exception
        });
        created.Rules.Should().OnlyContain(item => item.SourceDecisionKey.StartsWith("DEC-"));
        var pettyEvidence = created.Rules.Single(item =>
            item.Kind == ProcurementPolicyRuleKind.Evidence && item.SourceDecisionKey == "DEC-005");
        JsonSerializer.Deserialize<SaveProcurementPolicyEvidenceRuleValue>(pettyEvidence.Value.GetRawText(), JsonOptions)!
            .Method.Should().Be(ProcurementMethodType.PettyPurchase);
        var exceptionEvidence = created.Rules.Single(item =>
            item.Kind == ProcurementPolicyRuleKind.Evidence && item.SourceDecisionKey == "DEC-006");
        JsonSerializer.Deserialize<SaveProcurementPolicyEvidenceRuleValue>(exceptionEvidence.Value.GetRawText(), JsonOptions)!
            .Method.Should().Be(ProcurementMethodType.SingleSource);
        created.Validation.IsValid.Should().BeFalse();
        created.Validation.Errors.Should().NotContain(item =>
            item.Code == "RULE_FAMILY_MISSING" &&
            (item.RuleKind == ProcurementPolicyRuleKind.Authority ||
             item.RuleKind == ProcurementPolicyRuleKind.Evidence ||
             item.RuleKind == ProcurementPolicyRuleKind.Exception ||
             item.RuleKind == ProcurementPolicyRuleKind.SegregationOfDuties));
        created.Validation.Errors.Should().Contain(item => item.Code == "RFQ_COMPETITION_REQUIRED");
        created.Validation.Errors.Should().NotContain(item => item.Code == "RFQ_WORKFLOW_REQUIRED");
        (await fixture.Context.ProcurementConfigurationProfiles.SingleAsync(item => item.Id == source.Id))
            .LifecycleStatus.Should().Be(ProcurementConfigurationProfileStatus.Retired);
    }

    [Fact]
    public async Task CoreSourcingPolicyPublishesWithoutOptionalAuthorityEvidenceExceptionOrSodFamilies()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var draft = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id, "CORE-SOURCING"), "create-core");
        await fixture.ConfigureRfqRuleAsync(draft.Id);

        draft = await fixture.Service.GetPolicySetAsync(draft.Id);
        draft.Validation.IsValid.Should().BeTrue();
        draft.IsComplete.Should().BeTrue();
        draft.Validation.Errors.Should().NotContain(item =>
            item.Code == "RULE_FAMILY_MISSING" || item.Code == "SOD_REQUIRED_CONTROL_MISSING");

        var published = await fixture.Service.PublishPolicySetAsync(draft.Id,
            new ProcurementPolicyLifecycleRequest
            {
                RowVersion = draft.RowVersion,
                Reason = "Publish document-aligned core sourcing policy"
            }, "publish-core");

        published.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Published);
    }

    [Fact]
    public async Task PermissionAuthorizedPolicyActorCanPublishWithoutLegacyRoleRejection()
    {
        await using var fixture = new ServiceFixture(ProcurementAccessControlRegistry.IctAdministratorRole);
        var source = await fixture.AddSourceConfigurationAsync();
        var created = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-auth");
        await fixture.AddValidSodRuleAsync(created.Id);
        created = await fixture.Service.GetPolicySetAsync(created.Id);

        var published = await fixture.Service.PublishPolicySetAsync(created.Id,
            new ProcurementPolicyLifecycleRequest { RowVersion = created.RowVersion, Reason = "Approved configuration publication" },
            "publish-permission-authorized");

        published.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Published);
        (await fixture.Context.ProcurementPolicyRevisions.SingleAsync(item =>
            item.PolicySetId == created.Id && item.CorrelationId == "publish-permission-authorized"))
            .Should().Match<ProcurementPolicyRevision>(item => item.Action == "Publish" && item.Result == "Succeeded");
    }

    [Fact]
    public async Task CompletePolicyPublishesBecomesImmutableClonesWithLineageAndRetiresPriorAtomically()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var draft = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-lifecycle");
        await fixture.AddValidSodRuleAsync(draft.Id);
        draft = await fixture.Service.GetPolicySetAsync(draft.Id);

        draft.Validation.IsValid.Should().BeTrue();
        var published = await fixture.Service.PublishPolicySetAsync(draft.Id,
            new ProcurementPolicyLifecycleRequest { RowVersion = draft.RowVersion, Reason = "Approved executable policy" },
            "publish-lifecycle");
        published.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Published);
        (await fixture.Service.GetEffectivePolicySetAsync(published.Code,
            new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc)))!.Id.Should().Be(published.Id);

        fixture.SetRoles(ProcurementAccessControlRegistry.IctAdministratorRole);
        var retired = await fixture.Service.RetirePolicySetAsync(published.Id,
            new ProcurementPolicyLifecycleRequest { RowVersion = published.RowVersion, Reason = "Retire superseded policy" },
            "retire-permission-authorized");
        retired.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Retired);
        (await fixture.Context.ProcurementPolicyRevisions.AnyAsync(item =>
            item.PolicySetId == published.Id && item.CorrelationId == "retire-permission-authorized" && item.Result == "Succeeded")).Should().BeTrue();

        await fixture.Service.Invoking(service => service.UpdatePolicySetAsync(retired.Id,
                UpdateFrom(retired), "immutable-update"))
            .Should().ThrowAsync<ProcurementPolicyConflictException>().WithMessage("*immutable*");

        var clone = await fixture.Service.CloneDraftAsync(retired.Id,
            new CloneProcurementPolicySetRequest { ChangeSummary = "Annual refresh" }, "clone-lifecycle");
        clone.Version.Should().Be(2);
        clone.PolicyKey.Should().Be(retired.PolicyKey);
        clone.SupersedesPolicySetId.Should().Be(retired.Id);
        clone.Rules.Should().HaveCount(retired.Rules.Count);
        clone.Rules.Should().OnlyContain(rule => rule.SourceRuleId.HasValue);

        var storedInheritedThreshold = await fixture.Context.ProcurementPolicyThresholdRules.SingleAsync(item =>
            item.PolicySetId == clone.Id);
        storedInheritedThreshold.RowVersion = new byte[] { 1, 2, 3, 4 };
        await fixture.Context.SaveChangesAsync();
        clone = await fixture.Service.GetPolicySetAsync(clone.Id);
        var inheritedThreshold = clone.Rules.Single(item => item.Kind == ProcurementPolicyRuleKind.Threshold);
        var inheritedThresholdValue = JsonSerializer.Deserialize<SaveProcurementPolicyThresholdRuleValue>(
            inheritedThreshold.Value.GetRawText(), JsonOptions)!;
        inheritedThresholdValue.Name = "Annual refresh threshold";
        var revisedThreshold = await fixture.Service.SaveRuleAsync(clone.Id, inheritedThreshold.Id,
            new SaveProcurementPolicyRuleRequest
            {
                Kind = ProcurementPolicyRuleKind.Threshold,
                Threshold = inheritedThresholdValue,
                RowVersion = inheritedThreshold.RowVersion
            }, "edit-cloned-baseline-rule");
        revisedThreshold.Name.Should().Be("Annual refresh threshold");
        revisedThreshold.SourceRuleId.Should().Be(inheritedThreshold.SourceRuleId);

        clone = await fixture.Service.GetPolicySetAsync(clone.Id);
        var publishedClone = await fixture.Service.PublishPolicySetAsync(clone.Id,
            new ProcurementPolicyLifecycleRequest { RowVersion = clone.RowVersion }, "publish-clone");
        publishedClone.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Published);
        (await fixture.Service.GetPolicySetAsync(published.Id)).LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Retired);
        (await fixture.Context.ProcurementPolicySets.CountAsync(item =>
            item.PolicyKey == published.PolicyKey && item.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published)).Should().Be(1);
        (await fixture.Service.GetHistoryAsync(clone.Id)).Should().Contain(item =>
            item.Action == "Publish" && item.Result == "Succeeded" && item.CorrelationId == "publish-clone");
    }

    [Fact]
    public async Task FutureDatedReplacementKeepsCurrentPolicyEffectiveUntilCutover()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var now = DateTime.UtcNow;
        var currentFrom = now.AddDays(-30);
        var replacementFrom = now.AddDays(30);
        var effectiveTo = now.AddDays(90);
        var source = await fixture.AddSourceConfigurationAsync();
        var draft = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id, "FUTURE-CUTOVER"), "create-current-cutover");
        await fixture.AddValidSodRuleAsync(draft.Id);
        draft = await fixture.Service.GetPolicySetAsync(draft.Id);
        var currentUpdate = UpdateFrom(draft);
        currentUpdate.EffectiveFrom = currentFrom;
        currentUpdate.EffectiveTo = effectiveTo;
        draft = await fixture.Service.UpdatePolicySetAsync(draft.Id, currentUpdate, "date-current-cutover");
        await SetRulePeriodsAsync(fixture.Context, draft.Id, currentFrom, effectiveTo);
        draft = await fixture.Service.GetPolicySetAsync(draft.Id);
        var publishedCurrent = await fixture.Service.PublishPolicySetAsync(draft.Id,
            new ProcurementPolicyLifecycleRequest { RowVersion = draft.RowVersion, Reason = "Publish current policy" },
            "publish-current-cutover");

        var replacement = await fixture.Service.CloneDraftAsync(publishedCurrent.Id,
            new CloneProcurementPolicySetRequest { ChangeSummary = "Future replacement" }, "clone-future-cutover");
        var replacementUpdate = UpdateFrom(replacement);
        replacementUpdate.EffectiveFrom = replacementFrom;
        replacementUpdate.EffectiveTo = effectiveTo;
        replacement = await fixture.Service.UpdatePolicySetAsync(replacement.Id, replacementUpdate, "date-future-cutover");
        await SetRulePeriodsAsync(fixture.Context, replacement.Id, replacementFrom, effectiveTo);
        replacement = await fixture.Service.GetPolicySetAsync(replacement.Id);
        var publishedReplacement = await fixture.Service.PublishPolicySetAsync(replacement.Id,
            new ProcurementPolicyLifecycleRequest { RowVersion = replacement.RowVersion, Reason = "Schedule future replacement" },
            "publish-future-cutover");

        var currentAfterPublication = await fixture.Service.GetPolicySetAsync(publishedCurrent.Id);
        currentAfterPublication.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Published);
        currentAfterPublication.EffectiveTo.Should().Be(replacementFrom.AddTicks(-1));
        publishedReplacement.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Published);
        (await fixture.Service.GetEffectivePolicySetAsync(publishedCurrent.Code, now))?.Id.Should().Be(publishedCurrent.Id);
        (await fixture.Service.GetEffectivePolicySetAsync(publishedCurrent.Code, replacementFrom.AddTicks(1)))?.Id
            .Should().Be(publishedReplacement.Id);
        (await fixture.Service.GetHistoryAsync(publishedCurrent.Id)).Should().Contain(item =>
            item.Action == "ScheduleSupersession" && item.CorrelationId == "publish-future-cutover");
    }

    [Fact]
    public async Task CloneDraftAdvancesPastSoftDeletedVersionsWithoutTreatingThemAsActiveDrafts()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var first = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id, "SOFT-DELETE-VERSION"), "create-v1");
        await fixture.AddValidSodRuleAsync(first.Id);
        first = await fixture.Service.GetPolicySetAsync(first.Id);
        var published = await fixture.Service.PublishPolicySetAsync(first.Id,
            new ProcurementPolicyLifecycleRequest { RowVersion = first.RowVersion }, "publish-v1");
        var deletedDraft = await fixture.Service.CloneDraftAsync(published.Id,
            new CloneProcurementPolicySetRequest { ChangeSummary = "Discarded version" }, "clone-v2");
        await fixture.Service.DeleteDraftAsync(deletedDraft.Id,
            new ProcurementPolicyLifecycleRequest { RowVersion = deletedDraft.RowVersion, Reason = "Discard" }, "delete-v2");

        var nextDraft = await fixture.Service.CloneDraftAsync(published.Id,
            new CloneProcurementPolicySetRequest { ChangeSummary = "Replacement version" }, "clone-v3");

        nextDraft.Version.Should().Be(3);
        nextDraft.SupersedesPolicySetId.Should().Be(published.Id);
    }

    [Fact]
    public async Task TenantIsolationAppliesToListDetailUpdateCloneAndPublish()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var created = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-tenant");
        var otherTenant = Guid.NewGuid();
        fixture.SwitchTenant(otherTenant);

        (await fixture.Service.GetPolicySetsAsync(new ProcurementPolicySetListRequest())).Items.Should().BeEmpty();
        await fixture.Service.Invoking(service => service.GetPolicySetAsync(created.Id))
            .Should().ThrowAsync<ProcurementPolicyNotFoundException>();
        await fixture.Service.Invoking(service => service.UpdatePolicySetAsync(created.Id, UpdateFrom(created), "update-tenant"))
            .Should().ThrowAsync<ProcurementPolicyNotFoundException>();
        await fixture.Service.Invoking(service => service.CloneDraftAsync(created.Id, new CloneProcurementPolicySetRequest(), "clone-tenant"))
            .Should().ThrowAsync<ProcurementPolicyNotFoundException>();
        await fixture.Service.Invoking(service => service.PublishPolicySetAsync(created.Id,
                new ProcurementPolicyLifecycleRequest { RowVersion = created.RowVersion }, "publish-tenant"))
            .Should().ThrowAsync<ProcurementPolicyNotFoundException>();
    }

    [Fact]
    public async Task StalePolicyAndRuleRowVersionsAreRejectedBeforeMutation()
    {
        await using var fixture = new ServiceFixture("TenantAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var created = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-concurrency");

        await fixture.Service.Invoking(service => service.UpdatePolicySetAsync(created.Id,
                new UpdateProcurementPolicySetRequest
                {
                    Name = "Stale edit", DefaultCurrencyCode = "GHS", EffectiveFrom = EffectiveFrom,
                    EffectiveTo = EffectiveTo, RowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9 })
                }, "policy-stale"))
            .Should().ThrowAsync<ProcurementPolicyConflictException>();

        var threshold = created.Rules.Single(item => item.Kind == ProcurementPolicyRuleKind.Threshold);
        var thresholdValue = JsonSerializer.Deserialize<SaveProcurementPolicyThresholdRuleValue>(threshold.Value.GetRawText(), JsonOptions)!;
        await fixture.Service.Invoking(service => service.SaveRuleAsync(created.Id, threshold.Id,
                new SaveProcurementPolicyRuleRequest
                {
                    Kind = ProcurementPolicyRuleKind.Threshold,
                    Threshold = thresholdValue,
                    RowVersion = Convert.ToBase64String(new byte[] { 8, 8, 8 })
                }, "rule-stale"))
            .Should().ThrowAsync<ProcurementPolicyConflictException>();
        (await fixture.Service.GetPolicySetAsync(created.Id)).Name.Should().Be(created.Name);
    }

    [Fact]
    public async Task TypedRuleValidationRejectsAmbiguousPayloadsInvalidSodAndOverlappingThresholds()
    {
        await using var fixture = new ServiceFixture("TenantAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var created = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-validation");

        await fixture.Service.Invoking(service => service.SaveRuleAsync(created.Id, null,
                new SaveProcurementPolicyRuleRequest
                {
                    Kind = ProcurementPolicyRuleKind.SegregationOfDuties,
                    SegregationOfDuties = SodValue("ProcurementOfficer", "ProcurementOfficer"),
                    Evidence = new SaveProcurementPolicyEvidenceRuleValue()
                }, "ambiguous"))
            .Should().ThrowAsync<ProcurementPolicyValidationException>();

        await fixture.Service.Invoking(service => service.SaveRuleAsync(created.Id, null,
                new SaveProcurementPolicyRuleRequest
                {
                    Kind = ProcurementPolicyRuleKind.SegregationOfDuties,
                    SegregationOfDuties = SodValue("ProcurementOfficer", "ProcurementOfficer")
                }, "invalid-sod"))
            .Should().ThrowAsync<ProcurementPolicyValidationException>();

        await fixture.Service.SaveRuleAsync(created.Id, null, new SaveProcurementPolicyRuleRequest
        {
            Kind = ProcurementPolicyRuleKind.Threshold,
            Threshold = new SaveProcurementPolicyThresholdRuleValue
            {
                RuleCode = "THRESHOLD-OVERLAP", Name = "Overlapping band", Category = ProcurementCategoryClass.Goods,
                ServiceClass = "General goods", Method = ProcurementMethodType.RequestForQuotation,
                CurrencyCode = "GHS", LowerBound = 50000, UpperBound = 120000, LowerInclusive = true,
                UpperInclusive = true, StatutoryReference = "Public Procurement Act", SourceDecisionKey = "DEC-001",
                EffectiveFrom = EffectiveFrom, EffectiveTo = EffectiveTo
            }
        }, "overlap");

        var validation = await fixture.Service.ValidatePolicySetAsync(created.Id, "validate-overlap");
        validation.Errors.Should().Contain(item => item.Code == "THRESHOLD_OVERLAP");
    }

    [Fact]
    public async Task EvidenceRuleGeneratesStableSharedRequirementKeyFromRuleCode()
    {
        await using var fixture = new ServiceFixture("TenantAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var created = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-evidence-key");

        var saved = await fixture.Service.SaveRuleAsync(created.Id, null,
            new SaveProcurementPolicyRuleRequest
            {
                Kind = ProcurementPolicyRuleKind.Evidence,
                Evidence = new SaveProcurementPolicyEvidenceRuleValue
                {
                    RuleCode = "EVID-WAYBILL",
                    EvidenceName = "Clean waybill",
                    Stage = ProcurementEvidenceStage.Receipt,
                    Category = ProcurementCategoryClass.Goods,
                    Method = ProcurementMethodType.RequestForQuotation,
                    IsMandatory = true,
                    RequiresVerification = true,
                    SourceDecisionKey = "DEC-006",
                    EffectiveFrom = EffectiveFrom,
                    EffectiveTo = EffectiveTo
                }
            }, "create-generated-evidence-key");

        var savedValue = JsonSerializer.Deserialize<SaveProcurementPolicyEvidenceRuleValue>(
            saved.Value.GetRawText(), JsonOptions)!;
        savedValue.SharedRequirementKey.Should().Be("EVID-WAYBILL");
        (await fixture.Context.ProcurementPolicyEvidenceRules.SingleAsync(item => item.Id == saved.Id))
            .SharedRequirementKey.Should().Be("EVID-WAYBILL");
    }

    [Fact]
    public async Task WorkflowReferenceMustReuseAnActivePublishedDefinitionFromTheCurrentTenant()
    {
        await using var fixture = new ServiceFixture("TenantAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var created = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-workflow-reference");

        await fixture.Service.Invoking(service => service.SaveRuleAsync(created.Id, null,
                new SaveProcurementPolicyRuleRequest
                {
                    Kind = ProcurementPolicyRuleKind.Method,
                    Method = new SaveProcurementPolicyMethodRuleValue
                    {
                        RuleCode = "METHOD-WORKFLOW-REFERENCE", Name = "Workflow governed method",
                        Category = ProcurementCategoryClass.Goods, ServiceClass = "General goods",
                        Method = ProcurementMethodType.RequestForQuotation, IsAllowed = true,
                        RequiresCompetition = true, MinimumQuotationCount = 3, WorkflowDefinitionId = Guid.NewGuid(),
                        SourceDecisionKey = "DEC-003", EffectiveFrom = EffectiveFrom, EffectiveTo = EffectiveTo
                    }
                }, "invalid-workflow-reference"))
            .Should().ThrowAsync<ProcurementPolicyValidationException>().WithMessage("*active Published workflow definition*");
    }

    [Fact]
    public async Task AuthorityRuleRejectsARoleThatIsNotAssignedWithinTheCurrentTenant()
    {
        await using var fixture = new ServiceFixture("TenantAdmin");
        fixture.SetRoleExists(false);
        var source = await fixture.AddSourceConfigurationAsync();
        var created = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-role-reference");

        var action = fixture.Service.Invoking(service => service.SaveRuleAsync(created.Id, null,
            new SaveProcurementPolicyRuleRequest
            {
                Kind = ProcurementPolicyRuleKind.Authority,
                Authority = new SaveProcurementPolicyAuthorityRuleValue
                {
                    RuleCode = "AUTH-GOODS-GHS",
                    Category = ProcurementCategoryClass.Goods,
                    CurrencyCode = "GHS",
                    LowerBound = 0,
                    LowerInclusive = true,
                    AuthorityName = "Head of Procurement",
                    AuthorityRole = "InventedRole",
                    SourceDecisionKey = "DEC-002",
                    EffectiveFrom = EffectiveFrom,
                    EffectiveTo = EffectiveTo
                }
            }, "invalid-role-reference"));

        await action.Should().ThrowAsync<ProcurementPolicyValidationException>()
            .WithMessage("*assigned to an active user in the current tenant*");
    }

    [Fact]
    public async Task RfqPolicyRequiresPositiveCompetitionButWorkflowMetadataIsOptional()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var created = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-rfq-operational-validation");

        var validation = await fixture.Service.ValidatePolicySetAsync(created.Id, "validate-rfq-operational");

        validation.Errors.Should().Contain(item => item.Code == "RFQ_COMPETITION_REQUIRED");
        validation.Errors.Should().NotContain(item => item.Code == "RFQ_WORKFLOW_REQUIRED");
    }

    [Fact]
    public async Task TenantOverrideRequiresImmutableBaseAndSourceRuleForReplaceOrDisable()
    {
        await using var fixture = new ServiceFixture("SuperAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var baseDraft = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id, "BASE-POLICY"), "create-base");

        await fixture.Service.Invoking(service => service.CreatePolicySetAsync(
                NewPolicy(source.Id, "OVERRIDE-DRAFT-BASE", ProcurementPolicyScopeType.TenantOverride, baseDraft.Id), "override-draft"))
            .Should().ThrowAsync<ProcurementPolicyValidationException>();

        await fixture.AddValidSodRuleAsync(baseDraft.Id);
        baseDraft = await fixture.Service.GetPolicySetAsync(baseDraft.Id);
        var publishedBase = await fixture.Service.PublishPolicySetAsync(baseDraft.Id,
            new ProcurementPolicyLifecycleRequest { RowVersion = baseDraft.RowVersion }, "publish-base");
        var tenantOverride = await fixture.Service.CreatePolicySetAsync(
            NewPolicy(source.Id, "TENANT-OVERRIDE", ProcurementPolicyScopeType.TenantOverride, publishedBase.Id), "create-override");

        await fixture.Service.Invoking(service => service.SaveRuleAsync(tenantOverride.Id, null,
                new SaveProcurementPolicyRuleRequest
                {
                    Kind = ProcurementPolicyRuleKind.SegregationOfDuties,
                    SegregationOfDuties = SodValue("Requester", "Approver", ProcurementPolicyOverrideAction.Replace)
                }, "replace-without-source"))
            .Should().ThrowAsync<ProcurementPolicyValidationException>();

        var baseSod = publishedBase.Rules.Single(item =>
            item.Kind == ProcurementPolicyRuleKind.SegregationOfDuties &&
            item.RuleCode == ProcurementSodRequiredControlRegistry.Definitions[0].Code);
        var replacement = SodValue("Requester", "Approver", ProcurementPolicyOverrideAction.Replace);
        replacement.RuleCode = "SOD-OVERRIDE";
        replacement.SourceRuleId = baseSod.Id;
        var saved = await fixture.Service.SaveRuleAsync(tenantOverride.Id, null,
            new SaveProcurementPolicyRuleRequest
            {
                Kind = ProcurementPolicyRuleKind.SegregationOfDuties,
                SegregationOfDuties = replacement
            }, "replace-valid");
        saved.SourceRuleId.Should().Be(baseSod.Id);
        saved.OverrideAction.Should().Be(ProcurementPolicyOverrideAction.Replace);
    }

    [Fact]
    public async Task DraftDeleteSoftDeletesRulesAndWritesAppendOnlyAudit()
    {
        await using var fixture = new ServiceFixture("TenantAdmin");
        var source = await fixture.AddSourceConfigurationAsync();
        var created = await fixture.Service.CreatePolicySetAsync(NewPolicy(source.Id), "create-delete");
        await fixture.Service.DeleteDraftAsync(created.Id,
            new ProcurementPolicyLifecycleRequest { RowVersion = created.RowVersion, Reason = "Discard duplicate" }, "delete-draft");

        await fixture.Service.Invoking(service => service.GetPolicySetAsync(created.Id))
            .Should().ThrowAsync<ProcurementPolicyNotFoundException>();
        (await fixture.Context.ProcurementPolicySets.IgnoreQueryFilters().SingleAsync(item => item.Id == created.Id))
            .IsDeleted.Should().BeTrue();
        (await fixture.Context.ProcurementPolicyRevisions.SingleAsync(item => item.CorrelationId == "delete-draft"))
            .Should().Match<ProcurementPolicyRevision>(item => item.Action == "DeleteDraft" && item.Result == "Succeeded");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
    };

    private static CreateProcurementPolicySetRequest NewPolicy(
        Guid sourceId,
        string code = "TDC-EXECUTABLE",
        ProcurementPolicyScopeType scope = ProcurementPolicyScopeType.TenantBaseline,
        Guid? basePolicyId = null) => new()
    {
        SourceConfigurationProfileId = sourceId,
        Code = code,
        Name = code.Replace('-', ' '),
        Description = "Normalized executable procurement policy test fixture.",
        ScopeType = scope,
        BasePolicySetId = basePolicyId,
        DefaultCurrencyCode = "GHS",
        EffectiveFrom = EffectiveFrom,
        EffectiveTo = EffectiveTo,
        ChangeSummary = "Materialize approved decisions",
        IsDefault = true
    };

    private static UpdateProcurementPolicySetRequest UpdateFrom(ProcurementPolicySetDto policy) => new()
    {
        Name = policy.Name,
        Description = policy.Description,
        DefaultCurrencyCode = policy.DefaultCurrencyCode,
        EffectiveFrom = policy.EffectiveFrom,
        EffectiveTo = policy.EffectiveTo,
        ChangeSummary = policy.ChangeSummary,
        IsDefault = policy.IsDefault,
        RowVersion = policy.RowVersion
    };

    private static async Task SetRulePeriodsAsync(
        ApplicationDbContext context,
        Guid policySetId,
        DateTime effectiveFrom,
        DateTime effectiveTo)
    {
        foreach (var item in await context.ProcurementPolicyCategoryRules.Where(item => item.PolicySetId == policySetId).ToListAsync())
        { item.EffectiveFrom = effectiveFrom; item.EffectiveTo = effectiveTo; }
        foreach (var item in await context.ProcurementPolicyMethodRules.Where(item => item.PolicySetId == policySetId).ToListAsync())
        { item.EffectiveFrom = effectiveFrom; item.EffectiveTo = effectiveTo; }
        foreach (var item in await context.ProcurementPolicyThresholdRules.Where(item => item.PolicySetId == policySetId).ToListAsync())
        { item.EffectiveFrom = effectiveFrom; item.EffectiveTo = effectiveTo; }
        foreach (var item in await context.ProcurementPolicyAuthorityRules.Where(item => item.PolicySetId == policySetId).ToListAsync())
        { item.EffectiveFrom = effectiveFrom; item.EffectiveTo = effectiveTo; }
        foreach (var item in await context.ProcurementPolicyEvidenceRules.Where(item => item.PolicySetId == policySetId).ToListAsync())
        { item.EffectiveFrom = effectiveFrom; item.EffectiveTo = effectiveTo; }
        foreach (var item in await context.ProcurementPolicyExceptionRules.Where(item => item.PolicySetId == policySetId).ToListAsync())
        { item.EffectiveFrom = effectiveFrom; item.EffectiveTo = effectiveTo; }
        foreach (var item in await context.ProcurementPolicySodRules.Where(item => item.PolicySetId == policySetId).ToListAsync())
        { item.EffectiveFrom = effectiveFrom; item.EffectiveTo = effectiveTo; }
        await context.SaveChangesAsync();
    }

    private static SaveProcurementPolicySodRuleValue SodValue(
        string initiator,
        string conflicting,
        ProcurementPolicyOverrideAction overrideAction = ProcurementPolicyOverrideAction.Add) => new()
    {
        RuleCode = "SOD-REQUEST-APPROVE",
        Name = "Requester cannot approve own procurement",
        InitiatorRole = initiator,
        ConflictingRole = conflicting,
        EntityType = "ProcurementDocument",
        Action = "Approve",
        Enforcement = ProcurementSodEnforcement.HardStop,
        Explanation = "Normalized policy declaration only; runtime enforcement belongs to a later task.",
        SourceDecisionKey = "DEC-004",
        EffectiveFrom = EffectiveFrom,
        EffectiveTo = EffectiveTo,
        OverrideAction = overrideAction
    };

    private sealed class ServiceFixture : IAsyncDisposable
    {
        private Guid _tenantId = Guid.NewGuid();
        private readonly HashSet<string> _roles;
        private readonly IReadOnlyList<ApplicationRole> _tenantRoles;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly Mock<IRoleService> _roleService = new();
        private readonly UnitOfWork _unitOfWork;

        public ServiceFixture(params string[] roles)
        {
            _roles = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _tenantRoles = new[]
                {
                    "Entity Tender Committee", "Central Tender Review Committee",
                    "Finance Manager", "PPA", "ProcurementOfficer", "Requester", "Approver"
                }
                .Concat(ProcurementSodRequiredControlRegistry.Definitions.SelectMany(item =>
                    new[] { item.InitiatorRole, item.ConflictingRole }))
                .Concat(roles)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    NormalizedName = name.ToUpperInvariant(),
                    Description = $"Test role {name}"
                })
                .ToList();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            _currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
            _currentUser.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            _currentUser.SetupGet(item => item.Username).Returns("policy.test@tdc.local");
            _currentUser.SetupGet(item => item.FullName).Returns("Policy Test User");
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Roles).Returns(() => _roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));
            _roleService.Setup(item => item.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
            _roleService.Setup(item => item.GetRolesForTenantAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_tenantRoles);
            _unitOfWork = new UnitOfWork(Context);
            Service = new ProcurementPolicyService(_unitOfWork, _currentUser.Object, _roleService.Object, NullLogger<ProcurementPolicyService>.Instance);
        }

        public ApplicationDbContext Context { get; }
        public ProcurementPolicyService Service { get; }
        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;
        public void SetRoles(params string[] roles)
        {
            _roles.Clear();
            foreach (var role in roles) _roles.Add(role);
        }
        public void SetRoleExists(bool exists) =>
            _roleService.Setup(item => item.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(exists);

        public async Task<ProcurementConfigurationProfile> AddSourceConfigurationAsync(
            ProcurementConfigurationProfileStatus status = ProcurementConfigurationProfileStatus.Retired,
            bool published = true)
        {
            var profile = new ProcurementConfigurationProfile
            {
                TenantId = _tenantId,
                ProfileKey = Guid.NewGuid(),
                ProfileCode = $"CONFIG-{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
                Name = "Approved procurement decisions",
                Version = 1,
                LifecycleStatus = status,
                EffectiveFrom = EffectiveFrom,
                EffectiveTo = EffectiveTo,
                PublishedAt = published ? DateTime.UtcNow.AddMinutes(-10) : null,
                PublishedById = published ? _currentUser.Object.UserId : null,
                CreatedBy = "Tests"
            };
            Context.ProcurementConfigurationProfiles.Add(profile);
            foreach (var definition in ProcurementConfigurationDecisionRegistry.Definitions)
            {
                var value = ValidDecisionValue(definition.DecisionKey);
                Context.ProcurementConfigurationDecisions.Add(new ProcurementConfigurationDecision
                {
                    TenantId = _tenantId,
                    ProfileId = profile.Id,
                    DecisionKey = definition.DecisionKey,
                    SchemaVersion = definition.SchemaVersion,
                    OwnerGroup = definition.OwnerGroup,
                    Status = ProcurementConfigurationDecisionStatus.Approved,
                    ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved,
                    EvidenceStatus = ProcurementConfigurationEvidenceStatus.Verified,
                    ValueJson = value.GetRawText(),
                    DecisionDate = EffectiveFrom,
                    EffectiveFrom = EffectiveFrom,
                    EffectiveTo = EffectiveTo,
                    ApprovedById = _currentUser.Object.UserId,
                    ApprovedAt = DateTime.UtcNow.AddMinutes(-10),
                    ApprovalReference = $"MINUTE-{definition.DecisionKey}",
                    CreatedBy = "Tests"
                });
            }
            await Context.SaveChangesAsync();
            return profile;
        }

        public async Task AddValidSodRuleAsync(Guid policyId)
        {
            foreach (var definition in ProcurementSodRequiredControlRegistry.Definitions)
            {
                await Service.SaveRuleAsync(policyId, null, new SaveProcurementPolicyRuleRequest
                {
                    Kind = ProcurementPolicyRuleKind.SegregationOfDuties,
                    SegregationOfDuties = new SaveProcurementPolicySodRuleValue
                    {
                        RuleCode = definition.Code,
                        Name = definition.Name,
                        InitiatorRole = definition.InitiatorRole,
                        ConflictingRole = definition.ConflictingRole,
                        EntityType = definition.EntityType,
                        Action = definition.Action,
                        Enforcement = ProcurementSodEnforcement.HardStop,
                        Explanation = definition.Explanation,
                        SourceDecisionKey = definition.SourceDecisionKey,
                        EffectiveFrom = EffectiveFrom,
                        EffectiveTo = EffectiveTo,
                        OverrideAction = ProcurementPolicyOverrideAction.Add
                    }
                }, "add-required-sod");
            }

            await ConfigureRfqRuleAsync(policyId);
        }

        public async Task ConfigureRfqRuleAsync(Guid policyId)
        {
            var method = await Context.ProcurementPolicyMethodRules.SingleAsync(item =>
                item.PolicySetId == policyId && item.Method == ProcurementMethodType.RequestForQuotation);
            var entityType = new WorkflowEntityType
            {
                TenantId = _tenantId,
                Code = "TENDER_EVALUATION",
                Name = "Tender Evaluation",
                IsActive = true
            };
            var workflow = new WorkflowDefinition
            {
                TenantId = _tenantId,
                DefinitionKey = Guid.NewGuid(),
                Name = "TDC Tender Evaluation Approval",
                EntityTypeId = entityType.Id,
                EntityType = entityType,
                Version = 1,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                IsActive = true,
                PublishedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            Context.AddRange(entityType, workflow);
            method.RequiresCompetition = true;
            method.MinimumQuotationCount = 3;
            method.WorkflowDefinitionId = workflow.Id;
            await Context.SaveChangesAsync();
        }

        private static JsonElement ValidDecisionValue(string key)
        {
            EffectiveDatedDecisionValueDto value = key switch
            {
                "DEC-001" => new ProcurementMethodThresholdDecisionValueDto { Category = ProcurementCategoryClass.Goods, ServiceClass = "General goods", Method = ProcurementMethodType.RequestForQuotation, CurrencyCode = "GHS", LowerBound = 0, UpperBound = 100000, LowerInclusive = true, UpperInclusive = true, StatutoryReference = "Public Procurement Act" },
                "DEC-002" => new ProcurementAuthorityDecisionValueDto { AuthorityLevel = "Entity Tender Committee", CurrencyCode = "GHS", LowerBound = 0, UpperBound = 100000, LowerInclusive = true, UpperInclusive = true, EscalationAuthority = "Central Tender Review Committee", ApplicableCategories = new() { ProcurementCategoryClass.Goods } },
                "DEC-003" => new ProcurementWorkflowSelectionDecisionValueDto { TransactionEntityType = "PurchaseRequisition", PolicySelector = "TDC default", WorkflowDefinitionId = Guid.NewGuid(), ApplicabilityConditions = "All governed requisitions" },
                "DEC-004" => new ProcurementAuthorityStageDecisionValueDto { AuthorityOrCommittee = "Entity Tender Committee", RoleType = "Committee", Quorum = 3, EvidenceRequirements = new() { "Signed minutes" }, MinimumAmount = 0, MaximumAmount = 100000, ApplicableCategories = new() { ProcurementCategoryClass.Goods }, Sequence = 1, StageGroup = "Approval", EscalationAuthority = "Managing Director" },
                "DEC-005" => new ProcurementPettyPurchaseDecisionValueDto { PettyThreshold = 5000, CurrencyCode = "GHS", WaiverEligible = false, JustificationRequired = true, EvidenceRequirements = new() { "Receipt" }, ApproverRole = "Finance Manager", ExpiryDate = EffectiveTo },
                "DEC-006" => new ProcurementExceptionPrerequisiteDecisionValueDto { Method = ProcurementMethodType.SingleSource, Prerequisites = new() { "Statutory justification" }, ApprovalAuthority = "PPA", MandatoryEvidenceChecklist = new() { "Approval letter" }, FilingReference = "PPA filing", ExpiryDate = EffectiveTo },
                "DEC-007" => new ProcurementSupplierFeeDecisionValueDto { FeeType = "Registration", Amount = 100, CurrencyCode = "GHS", TaxPercent = 0, PaymentChannels = new() { "Bank" }, ReceiptNumberFormat = "FEE-{YYYY}-{####}", ExemptionRule = "Written approval", RefundRule = "No refund after review", RenewalRule = "Annual renewal" },
                "DEC-008" => new ProcurementSignatureDecisionValueDto { DocumentType = "PurchaseOrder", SignatureMode = ProcurementSignatureMode.ElectronicOrManualEvidence, SignatoryRoles = new() { "Managing Director" }, SigningOrder = 1, VerificationRule = "Validate shared signature evidence", EvidenceRequirements = new() { "Signed document" } },
                "DEC-009" => new ProcurementGhanepsDecisionValueDto { ProfileCode = "TDC-GHANEPS", FileTemplateMappings = new() { "Plan=APP" }, Frequency = "Daily", Owner = "Procurement ICT", AcknowledgementRule = "Record acknowledgement", ReconciliationRule = "Daily exception reconciliation" },
                "DEC-010" => new ProcurementNegativeStockDecisionValueDto { DefaultPolicy = ProcurementNegativeStockPolicy.Prohibited, EmergencyOverrideEligible = false, OverridePermission = "Inventory.EmergencyOverride", EvidenceRequirements = new() { "Emergency authority" }, OverrideDurationHours = 1, AuditRequired = true },
                "DEC-011" => new ProcurementSupplierRiskDecisionValueDto { ReviewFrequencyMonths = 12, ExposureWindowMonths = 12, RiskDimensions = new() { "FinancialStability=50", "Compliance=50" }, RiskBands = new() { "High=0-60", "Low=60-100" }, ConcentrationLimitPercent = 40, MinimumScore = 60, EligibilityAction = ProcurementSupplierRiskEligibilityAction.AwardHardStop, PerformanceWindowMonths = 12, PerformanceDimensions = new() { "DeliveryTimeliness=15", "GrnQuality=15", "RejectionRate=15", "PriceCompetitiveness=15", "Responsiveness=10", "ComplaintResolution=10", "ContractCompletion=20" }, PerformanceBands = new() { "Unsatisfactory=0-50", "ImprovementRequired=50-75", "Satisfactory=75-100" }, MinimumPerformanceDataCoveragePercent = 60, ResponseTargetHours = 48, PerformanceEligibilityAction = ProcurementSupplierRiskEligibilityAction.AwardHardStop },
                "DEC-012" => new ProcurementCutoverDecisionValueDto { CutoverDate = EffectiveFrom, DualRunPeriodDays = 14, DataOwner = "Procurement Director", AcceptanceSignatories = new() { "Steering Committee" }, ReleaseStatus = "Approved", EvidenceRequirements = new() { "Signed acceptance" } },
                "DEC-013" => new ProcurementReceiptDocumentDecisionValueDto { DocumentType = ProcurementReceiptDocumentType.GrnAndMrn, ApplicabilityRule = "GRN for goods; MRN for materials", CoexistenceRule = ProcurementReceiptCoexistenceRule.BothFromSingleReceipt, NumberFormat = "{TYPE}-{YYYY}-{####}", TemplateReference = "TDC-{TYPE}", SignatureRequirements = new() { "Stores", "Internal Audit" }, EvidenceRequirements = new() { "Delivery note" } },
                "DEC-014" => new ProcurementNonFunctionalDecisionValueDto { WorkloadScenario = "500 concurrent users", AvailabilityTargetPercent = 99.5m, ResponseTargetMilliseconds = 3000, BackupFrequencyHours = 24, RpoMinutes = 60, RtoMinutes = 240, AuthenticationTarget = "MFA for privileged access", MonitoringTarget = "Central telemetry", UsabilityTarget = "Task completion testing", AccessibilityTarget = "WCAG 2.1 AA", AcceptanceMethod = "Signed performance and security report" },
                _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown decision key")
            };
            value.EffectiveFrom = EffectiveFrom;
            value.EffectiveTo = EffectiveTo;
            return JsonSerializer.SerializeToElement(value, value.GetType(), JsonOptions);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
