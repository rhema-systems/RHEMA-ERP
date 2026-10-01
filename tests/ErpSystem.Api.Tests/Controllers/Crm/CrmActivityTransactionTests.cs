using System.Data.Common;
using System.Security.Claims;
using ErpSystem.Api.Controllers.Crm;
using ErpSystem.Core.DTOs.Crm;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Crm;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Crm;

public sealed class CrmActivityTransactionTests
{
    [Theory]
    [InlineData("contact")]
    [InlineData("link")]
    public async Task Transient_failure_retries_contact_activity_and_link_as_one_atomic_unit(string failure)
    {
        await using var f = await Fixture.Create(failure);
        var result = await f.CreateActivity();
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var activity = Assert.IsType<CrmActivityDetailDto>(created.Value);
        Assert.Equal(f.Ticket.Id, activity.PropertyEnquiryTicketId);
        Assert.Equal(2, f.ContactCalls);
        Assert.Equal(2, f.ActivityCalls);
        Assert.Equal(1, await f.Db.CrmActivities.CountAsync());
        Assert.Equal(1, await f.Db.EhcCrmEngagementLinks.CountAsync());
        Assert.Equal(1, await f.Db.EhcTicketAuditEvents.CountAsync());
        Assert.Equal("Contacted", (await f.Db.EhcTickets.AsNoTracking().SingleAsync()).Description);
        Assert.True(f.Transactions.Started >= 2);
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_returns_committed_activity_without_duplicate_contact_or_links()
    {
        await using var f = await Fixture.Create("commit");
        var result = await f.CreateActivity();
        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(1, f.ContactCalls);
        Assert.Equal(1, f.ActivityCalls);
        Assert.Equal(1, await f.Db.CrmActivities.CountAsync());
        Assert.Equal(1, await f.Db.EhcCrmEngagementLinks.CountAsync());
        Assert.Equal(1, await f.Db.EhcTicketAuditEvents.CountAsync());
    }

    [Fact]
    public async Task Permanent_failure_rolls_back_contact_and_activity_and_keeps_existing_error_response()
    {
        await using var f = await Fixture.Create("permanent");
        var result = await f.CreateActivity();
        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, problem.StatusCode);
        Assert.Empty(await f.Db.CrmActivities.ToListAsync());
        Assert.Empty(await f.Db.EhcCrmEngagementLinks.ToListAsync());
        Assert.Empty(await f.Db.EhcTicketAuditEvents.ToListAsync());
        Assert.Equal("Original", (await f.Db.EhcTickets.AsNoTracking().SingleAsync()).Description);
    }

    [Fact]
    public async Task Lead_automatically_links_enquiry_and_planned_activity_does_not_record_contact()
    {
        await using var f = await Fixture.Create();
        var result = await f.CreateActivity(completed: false);
        var activity = Assert.IsType<CrmActivityDetailDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(f.Ticket.Id, activity.PropertyEnquiryTicketId);
        Assert.Equal(0, f.ContactCalls);
        Assert.Equal(1, f.ActivityCalls);
        Assert.Equal("Original", (await f.Db.EhcTickets.AsNoTracking().SingleAsync()).Description);
    }

    [Fact]
    public async Task Caller_owned_transaction_is_not_committed_by_controller()
    {
        await using var f = await Fixture.Create();
        await f.Db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await f.Db.Database.BeginTransactionAsync();
            Assert.IsType<CreatedAtActionResult>((await f.CreateActivity()).Result);
            Assert.Same(transaction, f.Db.Database.CurrentTransaction);
            await transaction.RollbackAsync();
        });
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.CrmActivities.ToListAsync());
        Assert.Empty(await f.Db.EhcCrmEngagementLinks.ToListAsync());
        Assert.Equal("Original", (await f.Db.EhcTickets.AsNoTracking().SingleAsync()).Description);
    }

    private sealed class RetryFailure : Exception { }

    private sealed class RetryFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new TestRetryStrategy(dependencies);
    }

    private sealed class TestRetryStrategy(ExecutionStrategyDependencies dependencies)
        : SqlServerRetryingExecutionStrategy(dependencies, 1, TimeSpan.Zero, null)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is RetryFailure;
    }

    private sealed class TransactionProbe(string? failure) : DbTransactionInterceptor
    {
        public bool Enabled { get; set; }
        public int Started { get; private set; }
        private bool failed;
        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            if (Enabled)
            {
                Assert.NotNull(ExecutionStrategy.Current);
                Started++;
            }
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Enabled && failure == "commit" && !failed) { failed = true; throw new RetryFailure(); }
            return Task.CompletedTask;
        }
    }

    private sealed class LinkFailure(string? failure) : SaveChangesInterceptor
    {
        private bool failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!failed && eventData.Context!.ChangeTracker.Entries<EhcCrmEngagementLink>()
                    .Any(entry => entry.State == EntityState.Added))
            {
                if (failure == "link") { failed = true; throw new RetryFailure(); }
                if (failure == "permanent") throw new InvalidOperationException("Link could not be saved.");
            }
            return ValueTask.FromResult(result);
        }
    }

    // A relational slice using the real entity types and controller queries, without loading the
    // ERP's unrelated SQL Server schema. Transactions and retries are real; service effects share it.
    private sealed class ActivityDbContext(DbContextOptions<ActivityDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes().ToArray()) modelBuilder.Ignore(entity.ClrType);
            foreach (var type in new[] { typeof(EhcTicket), typeof(OrganizationUnit), typeof(Activity),
                         typeof(EhcCrmEngagementLink), typeof(EhcTicketAuditEvent) })
            {
                var entity = modelBuilder.Entity(type);
                foreach (var property in type.GetProperties())
                    if ((!property.PropertyType.IsValueType && property.PropertyType != typeof(string) && property.PropertyType != typeof(byte[]))
                        || property.Name == "RowVersion") entity.Ignore(property.Name);
                entity.HasKey("Id");
                foreach (var property in entity.Metadata.GetProperties()) property.SetColumnType(null);
            }
            modelBuilder.Entity<EhcTicket>().HasOne(ticket => ticket.AssignedOrganizationUnit).WithMany()
                .HasForeignKey(ticket => ticket.AssignedOrganizationUnitId);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ActivityDbContext Db { get; }
        public TransactionProbe Transactions { get; }
        public EhcTicket Ticket { get; private set; } = null!;
        private readonly Guid tenantId = Guid.NewGuid();
        private readonly Guid leadId = Guid.NewGuid();
        private readonly Mock<ICrmService> crm = new();
        private readonly Mock<IPropertyEnquiryProspectService> prospects = new();
        private readonly Mock<ICurrentUserService> current = new();
        private CrmController controller = null!;
        public int ContactCalls { get; private set; }
        public int ActivityCalls { get; private set; }

        private Fixture(SqliteConnection connection, string? failure)
        {
            this.connection = connection;
            Transactions = new(failure);
            Db = new(new DbContextOptionsBuilder<ActivityDbContext>().UseSqlite(connection)
                .ReplaceService<IExecutionStrategyFactory, RetryFactory>()
                .AddInterceptors(Transactions, new LinkFailure(failure)).Options);
        }

        public static async Task<Fixture> Create(string? failure = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var f = new Fixture(connection, failure);
            await f.Db.Database.EnsureCreatedAsync();
            var sales = new OrganizationUnit { TenantId = f.tenantId, Code = "DEPT-SALES", Name = "Sales", Path = "/SALES", IsActive = true };
            f.Ticket = new EhcTicket { TenantId = f.tenantId, TicketNumber = "PE-RETRY", TicketType = EhcTicketType.Enquiry,
                Status = EhcTicketStatus.InProgress, Subject = "Property enquiry", Description = "Original", PropertyListingContextJson = "{}",
                AssignedOrganizationUnitId = sales.Id, AssignedOrganizationUnit = sales, CrmLeadId = f.leadId };
            f.Db.AddRange(sales, f.Ticket);
            await f.Db.SaveChangesAsync();
            f.Db.ChangeTracker.Clear();
            f.current.SetupGet(user => user.TenantId).Returns(f.tenantId);
            f.current.SetupGet(user => user.UserId).Returns(Guid.NewGuid().ToString());
            f.current.SetupGet(user => user.UserName).Returns("sales.officer");
            f.prospects.Setup(service => service.GetAsync(f.Ticket.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PropertyEnquiryProspectDto?)null);
            f.prospects.Setup(service => service.RecordContactAsync(f.Ticket.Id,
                    It.IsAny<RecordPropertyEnquiryContactRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    f.ContactCalls++;
                    Assert.NotNull(f.Db.Database.CurrentTransaction);
                    var ticket = await f.Db.EhcTickets.SingleAsync();
                    Assert.Equal("Original", ticket.Description);
                    ticket.Description = "Contacted";
                    await f.Db.SaveChangesAsync();
                    return new PropertyEnquiryProspectDto(f.Ticket.Id, f.leadId, null, null, null, null, null,
                        EhcPropertyProspectStatuses.Contacted, 0, "GHS", ProspectDepositRequirementTypes.Full,
                        0, 0, true, null, null);
                });
            f.crm.Setup(service => service.CreateActivityAsync(It.IsAny<CreateCrmActivityDto>()))
                .Returns(async (CreateCrmActivityDto dto) =>
                {
                    f.ActivityCalls++;
                    Assert.NotNull(f.Db.Database.CurrentTransaction);
                    if (failure == "contact" && f.ActivityCalls == 1) throw new RetryFailure();
                    var activity = new Activity { TenantId = f.tenantId, LeadId = dto.LeadId, Subject = dto.Subject,
                        ActivityType = dto.ActivityType, ActivityStatus = dto.ActivityStatus };
                    f.Db.CrmActivities.Add(activity);
                    await f.Db.SaveChangesAsync();
                    return new CrmActivityDetailDto { ActivityId = activity.Id, Subject = activity.Subject, LeadId = activity.LeadId };
                });
            f.controller = new CrmController(f.crm.Object) { ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Role, "Sales Officer")], "Test")) }
            } };
            f.Transactions.Enabled = true;
            return f;
        }

        public Task<ActionResult<CrmActivityDetailDto>> CreateActivity(bool completed = true) => controller.CreateActivity(
            new CreateCrmActivityDto { LeadId = leadId, Subject = "Follow up", ActivityType = "Call",
                ActivityStatus = completed ? "Completed" : "Planned", ActivityDate = DateTime.UtcNow },
            Db, current.Object, prospects.Object, CancellationToken.None);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
