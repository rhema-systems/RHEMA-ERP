using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class StockAdjustmentTrackedStateTests
{
    [Theory]
    [InlineData("PendingApproval")]
    [InlineData("ReadyToPost")]
    [InlineData("Posted")]
    [InlineData("Reversed")]
    public async Task Actual_repository_update_of_a_tracked_lifecycle_root_preserves_immutable_children(string nextStatus)
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var adjustment = new StockAdjustment { TenantId = Guid.NewGuid(), Status = "Draft", AdjustmentNumber = "TRACKED-ROOT" };
        var line = new StockAdjustmentItem { TenantId = adjustment.TenantId, AdjustmentId = adjustment.Id, AdjustmentQuantity = -1, UnitCost = 10, AdjustmentValue = -10 };
        var action = new StockAdjustmentAction { TenantId = adjustment.TenantId, StockAdjustmentId = adjustment.Id, ActionType = "Created", Sequence = 1 };
        var evidence = new StockAdjustmentEvidence { TenantId = adjustment.TenantId, StockAdjustmentId = adjustment.Id };
        adjustment.Items.Add(line);
        adjustment.Actions.Add(action);
        adjustment.Evidence.Add(evidence);
        context.Add(adjustment);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var tracked = await context.Set<StockAdjustment>().Include(value => value.Items)
            .Include(value => value.Actions).Include(value => value.Evidence).SingleAsync();
        var originalLineTimestamp = tracked.Items.Single().UpdatedAt;
        var originalActionTimestamp = tracked.Actions.Single().UpdatedAt;
        var originalEvidenceTimestamp = tracked.Evidence.Single().UpdatedAt;
        tracked.Status = nextStatus;

        await new StockAdjustmentRepository(context).UpdateAsync(tracked);
        context.ChangeTracker.DetectChanges();

        context.Entry(tracked).State.Should().Be(EntityState.Modified);
        context.Entry(tracked.Items.Single()).State.Should().Be(EntityState.Unchanged);
        context.Entry(tracked.Actions.Single()).State.Should().Be(EntityState.Unchanged);
        context.Entry(tracked.Evidence.Single()).State.Should().Be(EntityState.Unchanged);
        await context.SaveChangesAsync();
        tracked.Items.Single().UpdatedAt.Should().Be(originalLineTimestamp);
        tracked.Actions.Single().UpdatedAt.Should().Be(originalActionTimestamp);
        tracked.Evidence.Single().UpdatedAt.Should().Be(originalEvidenceTimestamp);
    }
}
