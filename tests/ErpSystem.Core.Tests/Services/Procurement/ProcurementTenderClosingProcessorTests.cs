using System.Data;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementTenderClosingProcessorTests
{
    [Fact]
    public async Task DisabledSettingPreservesPublishedTenders()
    {
        using var f = new Fixture(false);
        var due = f.AddTender(f.Now.AddMinutes(-1));
        await f.Context.SaveChangesAsync();
        (await f.Processor.ProcessTenantAsync(f.TenantId, f.Now)).Should().Be(0);
        due.Status.Should().Be("Published");
        f.Events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnabledSettingClosesOnlyDueTenantTendersAndDoesNotRepeatOrOpenBids()
    {
        using var f = new Fixture(true);
        var due = f.AddTender(f.Now); // Deadline itself is closed, not one minute later.
        var later = f.AddTender(f.Now.AddSeconds(1));
        var otherTenant = f.AddTender(f.Now.AddMinutes(-1), tenantId: Guid.NewGuid());
        var awarded = f.AddTender(f.Now.AddMinutes(-1), status: "Awarded");
        var noDeadline = f.AddTender(null);
        var bid = new TenderBid { Id = Guid.NewGuid(), TenantId = f.TenantId, TenderId = due.Id, BidNumber = "BID-1", BusinessPartnerId = Guid.NewGuid(), Status = "Submitted" };
        f.Context.Add(bid);
        await f.Context.SaveChangesAsync();
        (await f.Processor.ProcessTenantAsync(f.TenantId, f.Now)).Should().Be(1);
        due.Status.Should().Be("Closed");
        later.Status.Should().Be("Published");
        otherTenant.Status.Should().Be("Published");
        awarded.Status.Should().Be("Awarded");
        noDeadline.Status.Should().Be("Published");
        bid.Status.Should().Be("Submitted");
        bid.OpenedDate.Should().BeNull();
        (await f.Processor.ProcessTenantAsync(f.TenantId, f.Now.AddMilliseconds(1))).Should().Be(0);
        f.Events.Verify(events => events.RecordSystemAsync(f.TenantId, It.IsAny<string>(),
            It.Is<ProcurementControlEventWriteRequest>(request => request.SourceId == due.Id && request.EventType == "TenderAutomaticClosure"),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Unit.Verify(unit => unit.BeginTransactionAsync(IsolationLevel.Serializable, It.IsAny<CancellationToken>()), Times.Exactly(2));
        f.Unit.Verify(unit => unit.AcquireTransactionLockAsync($"procurement-tender-close:{f.TenantId:N}", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AuditFailureRollsBackWithoutPersistingClosure()
    {
        using var f = new Fixture(true);
        var due = f.AddTender(f.Now.AddMinutes(-1));
        await f.Context.SaveChangesAsync();
        f.Events.Setup(events => events.RecordSystemAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Audit unavailable"));
        Func<Task> action = () => f.Processor.ProcessTenantAsync(f.TenantId, f.Now);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Audit unavailable");
        f.Unit.Verify(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        f.Unit.Verify(unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.Context.Set<Tender>().AsNoTracking().Single(tender => tender.Id == due.Id).Status.Should().Be("Published");
    }

    [Fact]
    public async Task SchedulerRejectsMissingTenantAndNonUtcClock()
    {
        using var f = new Fixture(true);
        Func<Task> missingTenant = () => f.Processor.ProcessTenantAsync(Guid.Empty, f.Now);
        Func<Task> localClock = () => f.Processor.ProcessTenantAsync(f.TenantId, DateTime.SpecifyKind(f.Now, DateTimeKind.Unspecified));
        await missingTenant.Should().ThrowAsync<ArgumentException>();
        await localClock.Should().ThrowAsync<ArgumentException>();
        f.Unit.Verify(unit => unit.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public DateTime Now { get; } = DateTime.UtcNow;
        public ApplicationDbContext Context { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Mock<IUnitOfWork> Unit { get; } = new();
        public Mock<IProcurementControlEventService> Events { get; } = new();
        public ProcurementTenderClosingProcessor Processor { get; }
        private bool _transaction;

        public Fixture(bool enabled)
        {
            Context.Add(new ProcurementSettings { Id = Guid.NewGuid(), TenantId = TenantId, AutoCloseTenders = enabled });
            Context.SaveChanges();
            Bind<Tender>(); Bind<ProcurementSettings>();
            Unit.SetupGet(unit => unit.HasActiveTransaction).Returns(() => _transaction);
            Unit.Setup(unit => unit.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
                .Callback(() => _transaction = true).Returns(Task.CompletedTask);
            Unit.Setup(unit => unit.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => _transaction = false).Returns(Task.CompletedTask);
            Unit.Setup(unit => unit.RollbackAsync(It.IsAny<CancellationToken>())).Callback(() => _transaction = false).Returns(Task.CompletedTask);
            Unit.Setup(unit => unit.ClearTrackedChanges()).Callback(() => Context.ChangeTracker.Clear());
            Unit.Setup(unit => unit.ExecuteInStrategyAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task<int>> action, CancellationToken _) => action());
            Unit.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) => Context.SaveChangesAsync(token));
            Processor = new ProcurementTenderClosingProcessor(Unit.Object, Events.Object);
        }

        private void Bind<T>() where T : BaseEntity
        {
            var repository = new Mock<IGenericRepository<T>>();
            repository.Setup(value => value.GetQueryable()).Returns(() => Context.Set<T>());
            Unit.Setup(unit => unit.Repository<T>()).Returns(repository.Object);
        }

        public Tender AddTender(DateTime? deadline, string status = "Published", Guid? tenantId = null)
        {
            var tender = new Tender { Id = Guid.NewGuid(), TenantId = tenantId ?? TenantId, TenderNumber = $"TND-{Guid.NewGuid():N}", Title = "Supply", Status = status, SubmissionDeadline = deadline };
            Context.Add(tender);
            return tender;
        }
        public void Dispose() => Context.Dispose();
    }
}
