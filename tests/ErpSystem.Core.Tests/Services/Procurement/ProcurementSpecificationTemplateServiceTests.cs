using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSpecificationTemplateServiceTests
{
    [Fact]
    public async Task CompleteGoodsTemplateTraversesDraftSubmissionPublicationAndImmutableTimeline()
    {
        await using var fixture = new Fixture();
        var created = await fixture.Service.CreateAsync(fixture.ValidRequest("GOODS-STD"), "trace-create");

        created.Status.Should().Be(ProcurementSpecificationTemplateStatus.Draft);
        created.ValidationIssues.Should().BeEmpty();

        var submitted = await fixture.Service.SubmitAsync(created.Id, fixture.Lifecycle(created.RowVersion), "trace-submit");
        fixture.SwitchActor(Guid.NewGuid(), "Specification Approver");
        var published = await fixture.Service.PublishAsync(created.Id, fixture.Lifecycle(submitted.RowVersion), "trace-publish");

        published.Status.Should().Be(ProcurementSpecificationTemplateStatus.Published);
        published.Timeline.Select(item => item.Action).Should().Equal("Created", "Submitted", "Published");
        published.Timeline.Should().OnlyContain(item => item.IntegrityHash.Length == 64);
        published.Timeline.Single(item => item.Action == "Submitted").Evidence.Should().ContainSingle();
    }

    [Fact]
    public async Task SubmissionRequiresEveryStandardSectionAndEvidence()
    {
        await using var fixture = new Fixture();
        var request = fixture.ValidRequest("SERVICES-TOR");
        request.AcceptanceCriteria = "";
        var created = await fixture.Service.CreateAsync(request, "trace-create");

        var validation = await fixture.Service.ValidateAsync(created.Id);
        validation.IsValid.Should().BeFalse();
        validation.Issues.Should().ContainSingle(item => item.Code == "ACCEPTANCE_CRITERIA_REQUIRED");
        await fixture.Service.Invoking(service => service.SubmitAsync(created.Id,
                new ProcurementSpecificationTemplateLifecycleRequest { RowVersion = created.RowVersion }, "trace-submit"))
            .Should().ThrowAsync<ProcurementSpecificationTemplateValidationException>();
    }

    [Fact]
    public async Task DeletedCloneVersionIsNotReused()
    {
        await using var fixture = new Fixture();
        var published = await fixture.CreatePublishedAsync("WORKS-STD");
        fixture.SwitchActor(Guid.NewGuid(), "Template Manager");
        var firstClone = await fixture.Service.CloneAsync(published.Id, fixture.Clone(published.RowVersion, 30), "trace-clone-2");
        await fixture.Service.DeleteDraftAsync(firstClone.Id, fixture.Lifecycle(firstClone.RowVersion), "trace-delete-2");

        var secondClone = await fixture.Service.CloneAsync(published.Id, fixture.Clone(published.RowVersion, 60), "trace-clone-3");

        secondClone.Version.Should().Be(3);
        (await fixture.Context.ProcurementSpecificationTemplates.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == firstClone.Id)).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task FutureReplacementKeepsCurrentPublishedTemplateEffectiveUntilItsStartDate()
    {
        await using var fixture = new Fixture();
        var current = await fixture.CreatePublishedAsync("GOODS-FUTURE");
        fixture.SwitchActor(Guid.NewGuid(), "Template Manager");
        var replacement = await fixture.Service.CloneAsync(current.Id, fixture.Clone(current.RowVersion, 14), "trace-clone");
        var submitted = await fixture.Service.SubmitAsync(replacement.Id, fixture.Lifecycle(replacement.RowVersion), "trace-submit-2");
        fixture.SwitchActor(Guid.NewGuid(), "Replacement Approver");
        await fixture.Service.PublishAsync(replacement.Id, fixture.Lifecycle(submitted.RowVersion), "trace-publish-2");

        var effective = await fixture.Service.GetEffectiveAsync("GOODS-FUTURE", DateTime.UtcNow);
        var currentRow = await fixture.Context.ProcurementSpecificationTemplates.SingleAsync(item => item.Id == current.Id);

        effective!.Id.Should().Be(current.Id);
        currentRow.Status.Should().Be(ProcurementSpecificationTemplateStatus.Published);
        currentRow.EffectiveToUtc.Should().BeCloseTo(replacement.EffectiveFromUtc.AddTicks(-1), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SearchAndDetailNeverCrossTenantBoundary()
    {
        await using var fixture = new Fixture();
        var created = await fixture.Service.CreateAsync(fixture.ValidRequest("GOODS-TENANT"), "trace-create");
        fixture.SwitchTenant(fixture.ForeignTenantId);

        (await fixture.Service.SearchAsync(new ProcurementSpecificationTemplateSearchRequest())).TotalCount.Should().Be(0);
        await fixture.Service.Invoking(service => service.GetAsync(created.Id))
            .Should().ThrowAsync<ProcurementSpecificationTemplateNotFoundException>();
    }

    [Fact]
    public async Task StaleRowVersionIsRejectedBeforeMutation()
    {
        await using var fixture = new Fixture();
        var created = await fixture.Service.CreateAsync(fixture.ValidRequest("GOODS-CONCURRENCY"), "trace-create");

        var request = fixture.ValidRequest("GOODS-CONCURRENCY");
        request.Name = "Stale update";
        request.RowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9 });

        await fixture.Service.Invoking(service => service.UpdateAsync(created.Id, request, "trace-stale"))
            .Should().ThrowAsync<ProcurementSpecificationTemplateConflictException>()
            .WithMessage("*changed by another user*");

        (await fixture.Service.GetAsync(created.Id)).Name.Should().Be(created.Name);
    }

    [Fact]
    public async Task NonAdministratorMutationRequiresPlanManagerCapability()
    {
        await using var fixture = new Fixture();
        fixture.SwitchRoles("TDC_PROCUREMENT_OFFICER");
        fixture.Access.Setup(service => service.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = false,
                Code = "ACCESS_PERMISSION_DENIED",
                Message = "No active planning responsibility assignment."
            });

        var action = () => fixture.Service.CreateAsync(fixture.ValidRequest("GOODS-DENIED"), "trace-denied");

        await action.Should().ThrowAsync<ProcurementSpecificationTemplateAuthorizationException>();
        (await fixture.Context.ProcurementSpecificationTemplates.CountAsync()).Should().Be(0);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _tenantId;
        private Guid _userId;
        private string _fullName = "Template Manager";
        private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase) { "TenantAdmin" };
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            _tenantId = TenantId;
            _userId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.AddRange(
                new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active },
                new Tenant { Id = ForeignTenantId, Code = "OTHER", Name = "Other", Status = TenantStatus.Active });
            Context.SaveChanges();

            var currentUser = new Mock<ICurrentUserProvider>();
            currentUser.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            currentUser.SetupGet(item => item.UserId).Returns(() => _userId);
            currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            currentUser.SetupGet(item => item.Username).Returns(() => $"{_userId:N}@tdc.test");
            currentUser.SetupGet(item => item.FullName).Returns(() => _fullName);
            currentUser.SetupGet(item => item.Roles).Returns(() => _roles);
            currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));

            _unitOfWork = new UnitOfWork(Context);
            var events = new ProcurementControlEventService(_unitOfWork, currentUser.Object,
                NullLogger<ProcurementControlEventService>.Instance);
            Access = new Mock<IProcurementAccessControlService>();
            Access.Setup(item => item.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true, Code = "ACCESS_ALLOWED", Message = "Allowed" });
            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.EnforceAsync(It.IsAny<ProcurementSodGuardRequest>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto { Allowed = true, Code = "SOD_ALLOWED", Message = "Independent actor." });
            var workflows = new Mock<IWorkflowInstanceService>();
            Service = new ProcurementSpecificationTemplateService(_unitOfWork, currentUser.Object, Access.Object,
                sod.Object, events, workflows.Object, NullLogger<ProcurementSpecificationTemplateService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public ApplicationDbContext Context { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public ProcurementSpecificationTemplateService Service { get; }

        public SaveProcurementSpecificationTemplateRequest ValidRequest(string code) => new()
        {
            TemplateCode = code,
            Name = $"{code} Standard Template",
            Kind = code.Contains("WORKS") ? ProcurementSpecificationTemplateKind.Works :
                code.Contains("SERVICES") ? ProcurementSpecificationTemplateKind.Services : ProcurementSpecificationTemplateKind.Goods,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
            Purpose = "Define the procurement need and intended outcome.",
            FunctionalAndPerformanceRequirements = "State measurable capacity, availability, and performance requirements.",
            ProcessAndMaterialsRequirements = "State required processes and materials, or a justified not-applicable statement.",
            DimensionsAndMarkingRequirements = "State dimensions, packaging, identification, and marking requirements.",
            TestingAndInspectionRequirements = "State inspection points, test methods, samples, and records.",
            ApplicableStandards = "List applicable Ghanaian, international, industry, safety, and environmental standards.",
            Deliverables = "List goods, works, services, reports, manuals, training, and completion records.",
            AcceptanceCriteria = "Define objective acceptance measures, evidence, responsible reviewer, and sign-off conditions."
        };

        public ProcurementSpecificationTemplateLifecycleRequest Lifecycle(string rowVersion) => new()
        {
            RowVersion = rowVersion,
            Comment = "Lifecycle review completed.",
            Evidence =
            [
                new ProcurementControlEventEvidenceReference
                {
                    ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                    Reference = "TDC-SPEC-EVIDENCE-001",
                    Label = "Template review evidence"
                }
            ]
        };

        public CloneProcurementSpecificationTemplateRequest Clone(string rowVersion, int days) => new()
        {
            RowVersion = rowVersion,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(days),
            ChangeSummary = $"Scheduled replacement after {days} days."
        };

        public async Task<ProcurementSpecificationTemplateDto> CreatePublishedAsync(string code)
        {
            var created = await Service.CreateAsync(ValidRequest(code), $"trace-{code}-create");
            var submitted = await Service.SubmitAsync(created.Id, Lifecycle(created.RowVersion), $"trace-{code}-submit");
            SwitchActor(Guid.NewGuid(), "Template Approver");
            return await Service.PublishAsync(created.Id, Lifecycle(submitted.RowVersion), $"trace-{code}-publish");
        }

        public void SwitchActor(Guid userId, string fullName)
        {
            _userId = userId;
            _fullName = fullName;
        }

        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;

        public void SwitchRoles(params string[] roles)
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
