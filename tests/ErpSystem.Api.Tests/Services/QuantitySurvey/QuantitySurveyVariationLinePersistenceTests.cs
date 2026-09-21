using ErpSystem.Api.Services.QuantitySurvey;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyVariationLinePersistenceTests
{
    [Fact]
    public void Applied_status_requires_the_linked_tenant_project_Boq_to_be_approved()
    {
        var variation = new ProjectVariationOrder { TenantId = Guid.NewGuid(), ProjectId = Guid.NewGuid(),
            DownstreamApplicationStatus = ProjectVariationApplicationStatuses.AppliedPendingBoqApproval };
        QuantitySurveyVariationService.ResolveApplicationStatus(variation).Should().Be(ProjectVariationApplicationStatuses.AppliedPendingBoqApproval);
        variation.RevisedBoqVersion = new ProjectBoqVersion { TenantId = variation.TenantId, ProjectId = variation.ProjectId,
            Status = ProjectBoqVersionStatuses.Draft };
        QuantitySurveyVariationService.ResolveApplicationStatus(variation).Should().Be(ProjectVariationApplicationStatuses.AppliedPendingBoqApproval);
        variation.RevisedBoqVersion.Status = ProjectBoqVersionStatuses.Approved;
        QuantitySurveyVariationService.ResolveApplicationStatus(variation).Should().Be(ProjectVariationApplicationStatuses.Applied);
        variation.RevisedBoqVersion.TenantId = Guid.NewGuid();
        QuantitySurveyVariationService.ResolveApplicationStatus(variation).Should().Be(ProjectVariationApplicationStatuses.AppliedPendingBoqApproval);
    }

    [Fact]
    public void First_forecast_adds_variation_once_to_budget_before_the_budget_is_revised()
    {
        var result = QuantitySurveyVariationService.CalculateForecastVariation(null, 10000m, 1000m);
        result.ForecastCost.Should().Be(11000m);
        result.EstimateAtCompletion.Should().Be(11000m);
        var existing = QuantitySurveyVariationService.CalculateForecastVariation(
            new ProjectForecastVersion { ForecastCost = 12000m, EstimateAtCompletion = 13000m }, 10000m, 1000m);
        existing.ForecastCost.Should().Be(13000m);
        existing.EstimateAtCompletion.Should().Be(14000m);
    }

    [Fact]
    public async Task Edit_remove_and_restore_preserve_source_line_identity_and_track_new_rows_as_added()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var variation = new ProjectVariationOrder { TenantId = tenant, RowVersion = [1] };
        QuantitySurveyVariationValuationLine Line(Guid source, decimal quantity) => new()
        {
            TenantId = tenant, VariationOrderId = variation.Id, ProjectBoqVersionLineId = source,
            QuantityChange = quantity, UnitRate = 1000, Amount = quantity * 1000,
            LineReferenceSnapshot = "1.01", DescriptionSnapshot = "UAT excavation",
            ValuationReason = "UAT revised quantity", SourceHash = "source-hash"
        };
        var first = Line(Guid.NewGuid(), 1); var second = Line(Guid.NewGuid(), 2);
        variation.ValuationLines.Add(first); variation.ValuationLines.Add(second);
        db.Add(variation); await db.SaveChangesAsync();
        var added = Line(Guid.NewGuid(), 3);
        QuantitySurveyVariationService.ReconcileValuationLines(db, variation,
            [Line(first.ProjectBoqVersionLineId, 4), added], actor, "UAT preparer");
        db.ChangeTracker.DetectChanges();
        db.Entry(first).State.Should().Be(EntityState.Modified);
        db.Entry(added).State.Should().Be(EntityState.Added);
        first.QuantityChange.Should().Be(4);
        second.IsDeleted.Should().BeTrue();
        variation.ValuationLines.Count.Should().Be(3);
        await db.SaveChangesAsync();

        QuantitySurveyVariationService.ReconcileValuationLines(db, variation,
            [Line(first.ProjectBoqVersionLineId, 5), Line(second.ProjectBoqVersionLineId, 6)], actor, "UAT preparer");
        second.IsDeleted.Should().BeFalse();
        second.DeletedAt.Should().BeNull();
        second.QuantityChange.Should().Be(6);
        added.IsDeleted.Should().BeTrue();
        db.ChangeTracker.DetectChanges();
        db.ChangeTracker.Entries<QuantitySurveyVariationValuationLine>().Should().NotContain(x => x.State == EntityState.Added);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.QuantitySurveyVariationValuationLines.IgnoreQueryFilters().ToListAsync();
        stored.Should().HaveCount(3);
        stored.Single(x => x.ProjectBoqVersionLineId == first.ProjectBoqVersionLineId).Id.Should().Be(first.Id);
        stored.Single(x => x.ProjectBoqVersionLineId == second.ProjectBoqVersionLineId).Id.Should().Be(second.Id);
    }
}
