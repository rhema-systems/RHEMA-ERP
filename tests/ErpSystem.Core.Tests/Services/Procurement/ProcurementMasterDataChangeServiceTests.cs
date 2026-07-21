using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
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

public sealed class ProcurementMasterDataChangeServiceTests
{
    [Fact]
    public async Task RegistryCoversNineProtectedFamiliesAndExcludesInventoryTransactionFields()
    {
        await using var fixture = new Fixture();
        fixture.Switch(fixture.MakerUserId, "TDC_PROCUREMENT_OFFICER");

        var registry = await fixture.Service.GetRegistryAsync();

        registry.Should().HaveCount(9).And.OnlyHaveUniqueItems(item => item.ResourceType);
        registry.Single(item => item.ResourceType == ProcurementMasterDataResourceType.InventoryItem)
            .AllowedFields.Should().NotContain(new[] { "CurrentStock", "AvailableStock", "AllocatedStock", "OnOrderStock", "AverageCost", "LastPurchaseCost" });
        registry.Single(item => item.ResourceType == ProcurementMasterDataResourceType.WarehouseLocation)
            .AllowedFields.Should().NotContain(new[] { "CurrentWeight", "CurrentVolume", "CurrentItemCount" });
    }

    [Fact]
    public async Task PolicyRejectsOverlappingMakerAndCheckerRolesBeforeWriting()
    {
        await using var fixture = new Fixture();
        fixture.Switch(fixture.MakerUserId, "SuperAdmin");
        var request = PolicyRequest();
        request.CheckerRoles = new() { "TDC_PROCUREMENT_OFFICER" };

        var action = () => fixture.Service.SavePolicyAsync(null, request, "trace-role-overlap");

        (await action.Should().ThrowAsync<ProcurementMasterDataChangeValidationException>()).Which.Code.Should().Be("ROLE_SEPARATION_REQUIRED");
        (await fixture.Context.ProcurementMasterDataControlPolicies.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ActivePolicyBlocksDirectMutationAndAppendsDeniedControlEvent()
    {
        await using var fixture = new Fixture();
        await fixture.AddActivePolicyAsync();
        fixture.Switch(fixture.MakerUserId, "TDC_PROCUREMENT_OFFICER");

        var decision = await fixture.Service.CheckDirectMutationAsync(
            new[] { ProcurementMasterDataResourceType.SupplierProfile }, fixture.PartnerId,
            "BusinessPartner.Update", "trace-direct-denied");

        decision.Allowed.Should().BeFalse();
        decision.Code.Should().Be("STAGED_CHANGE_REQUIRED");
        var audit = await fixture.Context.ProcurementControlEvents.SingleAsync();
        audit.Result.Should().Be(ProcurementControlEventResult.Denied);
        audit.Action.Should().Be("DirectMutationDenied");
    }

    [Fact]
    public async Task CompleteMakerCheckerLifecycleAppliesEffectiveSupplierPatchWithImmutableAudit()
    {
        await using var fixture = new Fixture();
        await fixture.AddActivePolicyAsync();
        fixture.Switch(fixture.MakerUserId, "TDC_PROCUREMENT_OFFICER");
        var draft = await fixture.Service.SaveDraftAsync(null, ChangeRequest(fixture.PartnerId), "trace-create");
        await fixture.SetRequestRowVersionAsync(draft.Id);
        var submitted = await fixture.Service.SubmitAsync(draft.Id, new ProcurementMasterDataChangeLifecycleRequest
        {
            RowVersion = fixture.RowVersion,
            Comment = "Submit independently"
        }, "trace-submit");

        fixture.Switch(fixture.CheckerUserId, "TDC_HEAD_OF_PROCUREMENT");
        var approved = await fixture.Service.ApproveAsync(submitted.Id, new ProcurementMasterDataChangeDecisionRequest
        {
            RowVersion = fixture.RowVersion,
            Comment = "Eligibility revalidated and approved"
        }, "trace-approve");
        var applied = await fixture.Service.ApplyAsync(approved.Id, new ProcurementMasterDataChangeLifecycleRequest
        {
            RowVersion = fixture.RowVersion,
            Comment = "Effective now"
        }, "trace-apply");

        applied.Status.Should().Be(ProcurementMasterDataChangeStatus.Applied);
        applied.BeforeJson.Should().Contain("Original Supplier");
        applied.AppliedAfterJson.Should().Contain("Controlled Supplier");
        applied.BeforeHash.Should().MatchRegex("^[0-9A-F]{64}$");
        applied.AppliedAfterHash.Should().MatchRegex("^[0-9A-F]{64}$");
        (await fixture.Context.BusinessPartners.SingleAsync(item => item.Id == fixture.PartnerId)).PartnerName.Should().Be("Controlled Supplier");
        (await fixture.Context.ProcurementControlEvents.CountAsync(item => item.EventType == "MasterDataChange")).Should().Be(4);
    }

    [Fact]
    public async Task ApprovalFailsRevalidationWhenTargetChangedAfterSnapshot()
    {
        await using var fixture = new Fixture();
        await fixture.AddActivePolicyAsync();
        fixture.Switch(fixture.MakerUserId, "TDC_PROCUREMENT_OFFICER");
        var draft = await fixture.Service.SaveDraftAsync(null, ChangeRequest(fixture.PartnerId), "trace-stale-create");
        await fixture.SetRequestRowVersionAsync(draft.Id);
        var submitted = await fixture.Service.SubmitAsync(draft.Id, new ProcurementMasterDataChangeLifecycleRequest
        {
            RowVersion = fixture.RowVersion
        }, "trace-stale-submit");
        var partner = await fixture.Context.BusinessPartners.SingleAsync(item => item.Id == fixture.PartnerId);
        partner.PartnerName = "Concurrent change";
        await fixture.Context.SaveChangesAsync();
        fixture.Switch(fixture.CheckerUserId, "TDC_HEAD_OF_PROCUREMENT");

        var result = await fixture.Service.ApproveAsync(submitted.Id, new ProcurementMasterDataChangeDecisionRequest
        {
            RowVersion = fixture.RowVersion,
            Comment = "Check current state"
        }, "trace-stale-approve");

        result.Status.Should().Be(ProcurementMasterDataChangeStatus.RevalidationFailed);
        result.RevalidationMessage.Should().Contain("changed after");
        partner.PartnerName.Should().Be("Concurrent change");
    }

    [Fact]
    public async Task MakerCannotApproveOwnSubmittedRequestEvenWhenHoldingCheckerRole()
    {
        await using var fixture = new Fixture();
        await fixture.AddActivePolicyAsync();
        fixture.Switch(fixture.MakerUserId, "TDC_PROCUREMENT_OFFICER");
        var draft = await fixture.Service.SaveDraftAsync(null, ChangeRequest(fixture.PartnerId), "trace-self-create");
        await fixture.SetRequestRowVersionAsync(draft.Id);
        var submitted = await fixture.Service.SubmitAsync(draft.Id, new ProcurementMasterDataChangeLifecycleRequest
        {
            RowVersion = fixture.RowVersion
        }, "trace-self-submit");
        fixture.Switch(fixture.MakerUserId, "TDC_HEAD_OF_PROCUREMENT");

        var action = () => fixture.Service.ApproveAsync(submitted.Id, new ProcurementMasterDataChangeDecisionRequest
        {
            RowVersion = fixture.RowVersion,
            Comment = "Self approval attempt"
        }, "trace-self-approve");

        await action.Should().ThrowAsync<ProcurementMasterDataChangeAuthorizationException>();
        (await fixture.Context.ProcurementMasterDataChangeRequests.SingleAsync()).Status.Should().Be(ProcurementMasterDataChangeStatus.PendingApproval);
    }

    [Fact]
    public async Task SearchAndDetailNeverCrossTenantBoundary()
    {
        await using var fixture = new Fixture();
        await fixture.AddActivePolicyAsync();
        fixture.Switch(fixture.MakerUserId, "TDC_PROCUREMENT_OFFICER");
        var draft = await fixture.Service.SaveDraftAsync(null, ChangeRequest(fixture.PartnerId), "trace-tenant");
        fixture.SwitchTenant(fixture.ForeignTenantId);

        (await fixture.Service.SearchAsync(new ProcurementMasterDataChangeSearchRequest())).TotalCount.Should().Be(0);
        await fixture.Service.Invoking(service => service.GetAsync(draft.Id))
            .Should().ThrowAsync<ProcurementMasterDataChangeNotFoundException>();
    }

    private static SaveProcurementMasterDataPolicyRequest PolicyRequest() => new()
    {
        ResourceType = ProcurementMasterDataResourceType.SupplierProfile,
        Name = "Controlled supplier profiles",
        MakerRoles = new() { "TDC_PROCUREMENT_OFFICER" },
        CheckerRoles = new() { "TDC_HEAD_OF_PROCUREMENT" },
        RequireIndependentApproval = true,
        RequireRevalidation = true,
        EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-5)
    };

    private static SaveProcurementMasterDataChangeRequest ChangeRequest(Guid partnerId) => new()
    {
        ResourceType = ProcurementMasterDataResourceType.SupplierProfile,
        TargetId = partnerId,
        ProposedChangesJson = "{\"PartnerName\":\"Controlled Supplier\"}",
        Reason = "Approved supplier legal-name correction",
        EffectiveAtUtc = DateTime.UtcNow.AddMinutes(-1)
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _tenantId;
        private Guid _userId;
        private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            MakerUserId = Guid.NewGuid();
            CheckerUserId = Guid.NewGuid();
            PartnerId = Guid.NewGuid();
            _tenantId = TenantId;
            _userId = MakerUserId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.AddRange(
                new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active },
                new Tenant { Id = ForeignTenantId, Code = "OTHER", Name = "Other", Status = TenantStatus.Active });
            Context.Users.AddRange(User(MakerUserId, "maker@tdc.test"), User(CheckerUserId, "checker@tdc.test"));
            Context.BusinessPartners.Add(new BusinessPartner
            {
                Id = PartnerId,
                TenantId = TenantId,
                PartnerCode = "SUP-001",
                PartnerName = "Original Supplier",
                PartnerType = "Supplier",
                RegistrationStatus = "Approved",
                IsActive = true
            });
            Context.SaveChanges();
            _currentUser.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            _currentUser.SetupGet(item => item.UserId).Returns(() => _userId);
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Username).Returns(() => _userId == MakerUserId ? "maker@tdc.test" : "checker@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns(() => _userId == MakerUserId ? "Maker User" : "Checker User");
            _currentUser.SetupGet(item => item.Roles).Returns(() => _roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));
            _unitOfWork = new UnitOfWork(Context);
            var events = new ProcurementControlEventService(_unitOfWork, _currentUser.Object, NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementMasterDataChangeService(_unitOfWork, _currentUser.Object, events,
                new Mock<IWorkflowInstanceService>().Object, NullLogger<ProcurementMasterDataChangeService>.Instance);
        }

        public string RowVersion => Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public Guid MakerUserId { get; }
        public Guid CheckerUserId { get; }
        public Guid PartnerId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementMasterDataChangeService Service { get; }

        public void Switch(Guid userId, params string[] roles)
        {
            _userId = userId;
            _roles.Clear();
            foreach (var role in roles) _roles.Add(role);
        }
        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;

        public async Task AddActivePolicyAsync()
        {
            Context.ProcurementMasterDataControlPolicies.Add(new ProcurementMasterDataControlPolicy
            {
                TenantId = TenantId,
                ResourceType = ProcurementMasterDataResourceType.SupplierProfile,
                Name = "Controlled supplier profiles",
                Version = 1,
                Status = ProcurementMasterDataPolicyStatus.Active,
                MakerRolesJson = "[\"TDC_PROCUREMENT_OFFICER\"]",
                CheckerRolesJson = "[\"TDC_HEAD_OF_PROCUREMENT\"]",
                RequireIndependentApproval = true,
                RequireRevalidation = true,
                EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-10),
                RowVersion = new byte[] { 1, 2, 3, 4 }
            });
            await Context.SaveChangesAsync();
        }

        public async Task SetRequestRowVersionAsync(Guid id)
        {
            var request = await Context.ProcurementMasterDataChangeRequests.SingleAsync(item => item.Id == id);
            request.RowVersion = new byte[] { 1, 2, 3, 4 };
            await Context.SaveChangesAsync();
        }

        private ApplicationUser User(Guid id, string username) => new()
        {
            Id = id,
            TenantId = TenantId,
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            FirstName = username.StartsWith("maker") ? "Maker" : "Checker",
            LastName = "User",
            IsActive = true
        };

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
