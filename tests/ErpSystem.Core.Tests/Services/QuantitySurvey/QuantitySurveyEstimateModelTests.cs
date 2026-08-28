using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyEstimateModelTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    [Fact]
    public void Estimate_header_is_tenant_versioned_idempotent_and_single_current_approved()
    {
        var entity = Model.FindEntityType(typeof(QuantitySurveyEstimateVersion))!;

        entity.FindProperty(nameof(QuantitySurveyEstimateVersion.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        entity.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "ClientRequestId" }));
        entity.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "ProjectId", "EstimateType", "VersionNumber" }));
        entity.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "ProjectId", "EstimateType" }) &&
            index.GetFilter()!.Contains("Status] = 'Approved'", StringComparison.Ordinal));
        entity.GetCheckConstraints().Select(value => value.Name).Should().Contain(new[]
        {
            "CK_QsEstimateVersions_Status",
            "CK_QsEstimateVersions_Totals",
            "CK_QsEstimateVersions_EvidencePair"
        });
    }

    [Fact]
    public void Estimate_snapshots_preserve_governed_precision_and_unique_lineage()
    {
        var line = Model.FindEntityType(typeof(QuantitySurveyEstimateLine))!;
        line.FindProperty(nameof(QuantitySurveyEstimateLine.UnitRate))!
            .FindAnnotation(RelationalAnnotationNames.ColumnType)!.Value.Should().Be("decimal(18,6)");
        line.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "EstimateVersionId", "ProjectBoqVersionLineId" }));

        var markup = Model.FindEntityType(typeof(QuantitySurveyEstimateMarkup))!;
        markup.FindProperty(nameof(QuantitySurveyEstimateMarkup.Percentage))!
            .FindAnnotation(RelationalAnnotationNames.ColumnType)!.Value.Should().Be("decimal(9,4)");
        markup.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "EstimateVersionId", "Component" }));

        Model.FindEntityType(typeof(QuantitySurveyEstimateRevision))!
            .GetIndexes().Should().Contain(index =>
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "CorrelationId" }));
    }

    private IModel Model => _context.GetService<IDesignTimeModel>().Model;

    public void Dispose() => _context.Dispose();
}
