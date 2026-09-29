using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class PhysicalCountPostingEligibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_approval_count_exposes_post_to_the_scoped_posting_operator_without_human_approvers(bool lookupByNumber)
    {
        var fixture = new Fixture { Roles = [] };
        fixture.Count.ApprovalRequired = false;
        fixture.Count.FinanceApprovedById = null;
        fixture.Count.StoresApprovedById = null;
        fixture.Count.AuditAttestedById = null;
        fixture.Count.ApprovedById = null;
        fixture.Count.CountedById = Guid.Parse(fixture.Actor!);
        var detail = lookupByNumber
            ? await fixture.Service.GetByCountNumberAsync(fixture.Count.CountNumber)
            : await fixture.Service.GetByIdAsync(fixture.Count.Id);
        detail!.ApprovalRequired.Should().BeFalse();
        detail.CanPost.Should().BeTrue();
        fixture.Requests.Should().Contain(request => request.PermissionCode == "procurement.inventory.adjust.approve");
    }

    [Fact]
    public async Task No_approval_snapshot_does_not_allow_post_when_human_approval_fields_are_populated()
    {
        var fixture = new Fixture();
        fixture.Count.ApprovalRequired = false;
        (await fixture.Service.GetByIdAsync(fixture.Count.Id))!.CanPost.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_detail_routes_allow_only_the_scoped_finance_approver(bool lookupByNumber)
    {
        var fixture = new Fixture();
        var detail = lookupByNumber
            ? await fixture.Service.GetByCountNumberAsync(fixture.Count.CountNumber)
            : await fixture.Service.GetByIdAsync(fixture.Count.Id);

        detail.Should().NotBeNull();
        detail!.CanPost.Should().BeTrue();
        detail.CanDecide.Should().BeFalse("posting is distinct from deciding another approval stage");
        fixture.Requests.Should().ContainSingle(request => request.PermissionCode == "procurement.inventory.adjust.approve" &&
            request.WarehouseId == fixture.Count.WarehouseId && request.LocationId == fixture.Count.LocationId &&
            request.RequireLocationScope && request.SourceType == "PhysicalCount" && request.SourceReference == fixture.Count.CountNumber);
    }

    [Theory]
    [InlineData("TDC_INTERNAL_AUDIT")]
    [InlineData("TDC_STORES_MANAGER")]
    [InlineData("SuperAdmin")]
    [InlineData("")]
    public async Task Other_roles_do_not_receive_post_even_when_the_record_names_them_as_finance_approver(string role)
    {
        var fixture = new Fixture { Roles = [role] };
        (await fixture.Service.GetByIdAsync(fixture.Count.Id))!.CanPost.Should().BeFalse();
        fixture.Requests.Should().NotContain(request => request.PermissionCode == "procurement.inventory.adjust.approve");
    }

    [Fact]
    public async Task Finance_role_comparison_matches_the_case_insensitive_post_guard()
    {
        var fixture = new Fixture { Roles = ["tdc_finance_reviewer"] };
        (await fixture.Service.GetByIdAsync(fixture.Count.Id))!.CanPost.Should().BeTrue();
    }

    [Fact]
    public async Task Another_finance_reviewer_cannot_post_the_recorded_approvers_count()
    {
        var fixture = new Fixture { Actor = Guid.NewGuid().ToString() };
        (await fixture.Service.GetByIdAsync(fixture.Count.Id))!.CanPost.Should().BeFalse();
    }

    [Theory]
    [InlineData("initiator")]
    [InlineData("counter")]
    [InlineData("stores")]
    [InlineData("audit")]
    public async Task Finance_approver_with_a_conflicting_recorded_duty_cannot_post(string duty)
    {
        var fixture = new Fixture();
        var actor = Guid.Parse(fixture.Actor!);
        switch (duty)
        {
            case "initiator": fixture.Count.InitiatedById = actor; break;
            case "counter": fixture.Count.CountedById = actor; break;
            case "stores": fixture.Count.StoresApprovedById = actor; break;
            case "audit": fixture.Count.AuditAttestedById = actor; break;
        }
        (await fixture.Service.GetByIdAsync(fixture.Count.Id))!.CanPost.Should().BeFalse();
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("InProgress")]
    [InlineData("UnderReview")]
    [InlineData("PendingStoresApproval")]
    [InlineData("PendingFinanceApproval")]
    [InlineData("PendingAuditAttestation")]
    [InlineData("Approved")]
    [InlineData("Posted")]
    [InlineData("Cancelled")]
    public async Task Only_ready_to_post_exposes_the_controlled_post_action(string status)
    {
        var fixture = new Fixture();
        fixture.Count.Status = status;
        (await fixture.Service.GetByIdAsync(fixture.Count.Id))!.CanPost.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public async Task Cutoff_is_checked_using_the_same_UTC_boundary_as_posting(int dayOffset, bool expected)
    {
        var fixture = new Fixture();
        fixture.Count.CutoffAtUtc = DateTime.UtcNow.AddDays(dayOffset);
        (await fixture.Service.GetByIdAsync(fixture.Count.Id))!.CanPost.Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_access_does_not_imply_posting_access_at_warehouse_or_location_scope(bool warehouseWide)
    {
        var fixture = new Fixture { ApproveScopeAllowed = false };
        if (warehouseWide) fixture.Count.LocationId = null;
        var detail = await fixture.Service.GetByIdAsync(fixture.Count.Id);
        detail.Should().NotBeNull();
        detail!.CanPost.Should().BeFalse();
        fixture.Requests.Should().ContainSingle(request => request.PermissionCode == "procurement.inventory.adjust.approve" &&
            request.WarehouseId == fixture.Count.WarehouseId && request.LocationId == fixture.Count.LocationId && request.RequireLocationScope);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scope_authorization_and_validation_denials_hide_post_without_breaking_detail(bool validationFailure)
    {
        var fixture = new Fixture
        {
            ScopeException = validationFailure
                ? new ProcurementAccessValidationException("SCOPE_REQUIRED", "A current scope is required.")
                : new ProcurementAccessAuthorizationException("The reviewer has no current scope.")
        };
        (await fixture.Service.GetByIdAsync(fixture.Count.Id))!.CanPost.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Missing_authenticated_actor_never_exposes_post(string? actor)
    {
        var fixture = new Fixture { Actor = actor };
        (await fixture.Service.GetByIdAsync(fixture.Count.Id))!.CanPost.Should().BeFalse();
    }

    [Theory]
    [InlineData("other-tenant")]
    [InlineData("empty-tenant")]
    [InlineData("deleted")]
    public async Task Records_outside_the_controlled_post_owner_scope_never_expose_post(string invalidScope)
    {
        var fixture = new Fixture();
        if (invalidScope == "other-tenant") fixture.Count.TenantId = Guid.NewGuid();
        else if (invalidScope == "empty-tenant") fixture.Count.TenantId = Guid.Empty;
        else fixture.Count.IsDeleted = true;
        (await fixture.Service.GetByIdAsync(fixture.Count.Id))!.CanPost.Should().BeFalse();
    }

    private sealed class Fixture
    {
        public string? Actor { get; set; } = Guid.NewGuid().ToString();
        public string[] Roles { get; set; } = ["TDC_FINANCE_REVIEWER"];
        public bool ApproveScopeAllowed { get; set; } = true;
        public Exception? ScopeException { get; set; }
        public List<ProcurementAccessCapabilityRequest> Requests { get; } = [];
        public PhysicalCount Count { get; }
        public PhysicalCountService Service { get; }

        public Fixture()
        {
            var tenant = Guid.NewGuid();
            Count = new PhysicalCount
            {
                Id = Guid.NewGuid(), TenantId = tenant, CountNumber = "PC-CAN-POST", Status = "ReadyToPost",
                WarehouseId = Guid.NewGuid(), LocationId = Guid.NewGuid(), FinanceApprovedById = Guid.Parse(Actor!),
                InitiatedById = Guid.NewGuid(), CountedById = Guid.NewGuid(), StoresApprovedById = Guid.NewGuid(),
                AuditAttestedById = Guid.NewGuid(), RowVersion = [1, 2, 3]
            };
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(user => user.UserId).Returns(() => Actor);
            currentUser.SetupGet(user => user.TenantId).Returns(tenant);
            currentUser.SetupGet(user => user.Roles).Returns(() => Roles);
            var repository = new Mock<IPhysicalCountRepository>();
            repository.Setup(owner => owner.GetWithItemsAsync(Count.Id)).ReturnsAsync(Count);
            repository.Setup(owner => owner.GetByCountNumberAsync(Count.CountNumber)).ReturnsAsync(Count);
            repository.Setup(owner => owner.GetQueryable(It.IsAny<Expression<Func<PhysicalCount, bool>>>()))
                .Returns((Expression<Func<PhysicalCount, bool>> predicate) => new AsyncQuery<PhysicalCount>(new[] { Count }.Where(predicate.Compile())));

            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(owner => owner.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken _) =>
                {
                    Requests.Add(request);
                    if (request.PermissionCode == "procurement.inventory.adjust.approve" && ScopeException != null) throw ScopeException;
                    return new ProcurementAccessCapabilityDecisionDto
                    {
                        Allowed = request.PermissionCode != "procurement.inventory.adjust.approve" || ApproveScopeAllowed
                    };
                });
            Service = new PhysicalCountService(repository.Object, Mock.Of<IPhysicalCountItemRepository>(),
                Mock.Of<IInventoryItemRepository>(), Mock.Of<IWarehouseRepository>(), Mock.Of<IWarehouseQuantityRepository>(),
                Mock.Of<IUnitOfWork>(), currentUser.Object, access.Object, Mock.Of<IStockAdjustmentService>(),
                Mock.Of<IProcurementControlEventService>(), NullLogger<PhysicalCountService>.Instance,
                Mock.Of<IWarehouseDefaultLocationService>());
        }
    }
    private sealed class AsyncQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public AsyncQuery(IEnumerable<T> values) : base(values) { }
        public AsyncQuery(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new AsyncProvider(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new AsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }
    private sealed class AsyncEnumerator<T>(IEnumerator<T> enumerator) : IAsyncEnumerator<T>
    {
        public T Current => enumerator.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(enumerator.MoveNext());
        public ValueTask DisposeAsync() { enumerator.Dispose(); return ValueTask.CompletedTask; }
    }
    private sealed class AsyncProvider(IQueryProvider provider) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => provider.CreateQuery(expression);
        public IQueryable<T> CreateQuery<T>(Expression expression) => new AsyncQuery<T>(expression);
        public object? Execute(Expression expression) => provider.Execute(expression);
        public T Execute<T>(Expression expression) => provider.Execute<T>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var result = typeof(IQueryProvider).GetMethod(nameof(Execute), 1, new[] { typeof(Expression) })!.MakeGenericMethod(resultType).Invoke(provider, new object[] { expression });
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, new[] { result })!;
        }
    }
}
