using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
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
            "CK_QsEstimateVersions_EvidencePair",
            "CK_QsEstimateVersions_SourceSnapshotSchema"
        });
        entity.FindProperty(nameof(QuantitySurveyEstimateVersion.FundingSourceSnapshot))!
            .GetMaxLength().Should().Be(500);
        entity.FindProperty(nameof(QuantitySurveyEstimateVersion.PropertyReferenceSnapshot))!
            .GetMaxLength().Should().Be(2000);
    }

    [Fact]
    public void Estimate_source_snapshot_is_server_derived_tenant_safe_and_deterministic()
    {
        var tenantId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FundingSource = "  Government of Ghana capital programme  "
        };
        var snapshots = QuantitySurveyEstimateSourceSnapshotBuilder.Capture(project,
        [
            Asset(project, tenantId, "PROP-002", "FILE-B"),
            Asset(project, tenantId, "PROP-001", "FILE-A"),
            Asset(project, tenantId, "PROP-003", null),
            Asset(project, Guid.NewGuid(), "FOREIGN", "FOREIGN-FILE"),
            new EstateManagedAsset
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = Guid.NewGuid(),
                AssetCode = "UNLINKED", PropertyFileReference = "UNLINKED-FILE"
            },
            new EstateManagedAsset
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, IsDeleted = true,
                AssetCode = "DELETED", PropertyFileReference = "DELETED-FILE"
            }
        ]);

        snapshots.FundingSource.Should().Be("Government of Ghana capital programme");
        snapshots.PropertyReference.Should().Be("FILE-A; FILE-B; PROP-003");
    }

    [Fact]
    public void Estimate_source_snapshot_does_not_require_browser_supplied_source_fields()
    {
        var request = typeof(ErpSystem.Core.DTOs.QuantitySurvey.CreateQuantitySurveyEstimateRequest);
        request.GetProperty("FundingSource").Should().BeNull();
        request.GetProperty("FundingSourceId").Should().BeNull();
        request.GetProperty("PropertyReference").Should().BeNull();
        request.GetProperty("PropertyId").Should().BeNull();
    }

    [Fact]
    public void Current_estimate_hash_and_revision_snapshot_cover_authoritative_source_snapshots()
    {
        var estimate = new QuantitySurveyEstimateVersion
        {
            ProjectId = Guid.NewGuid(), ProjectBoqVersionId = Guid.NewGuid(), VersionNumber = 1,
            EstimateType = QuantitySurveyEstimateType.CostPlan, Name = "Source snapshot estimate",
            EstimateDate = new DateTime(2026, 8, 29), CurrencyId = Guid.NewGuid(),
            CurrencyCodeSnapshot = "GHS", FundingSourceSnapshot = "Government funding",
            PropertyReferenceSnapshot = "PROP-001", SourceSnapshotSchemaVersion = 1,
            ConfigurationProfileId = Guid.NewGuid(), ConfigurationDecisionId = Guid.NewGuid(),
            ConfigurationProfileVersion = 1
        };
        var hashMethod = typeof(ErpSystem.Core.Services.Projects.ProjectService).GetMethod(
            "ComputeEstimateHash", BindingFlags.Static | BindingFlags.NonPublic)!;
        string Hash() => (string)hashMethod.Invoke(null,
        [
            estimate,
            Array.Empty<QuantitySurveyEstimateLine>(),
            Array.Empty<QuantitySurveyEstimateAssumption>(),
            Array.Empty<QuantitySurveyEstimateMarkup>()
        ])!;

        var original = Hash();
        estimate.FundingSourceSnapshot = "Internally generated funds";
        Hash().Should().NotBe(original);
        estimate.FundingSourceSnapshot = "Government funding";
        estimate.PropertyReferenceSnapshot = "PROP-002";
        Hash().Should().NotBe(original);

        var serializeMethod = typeof(ErpSystem.Core.Services.Projects.ProjectService).GetMethod(
            "SerializeEstimate", BindingFlags.Static | BindingFlags.NonPublic)!;
        var historySnapshot = (string)serializeMethod.Invoke(null, [estimate])!;
        historySnapshot.Should().Contain("Government funding").And.Contain("PROP-002");
    }

    private static EstateManagedAsset Asset(Project project, Guid tenantId, string code, string? reference) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id,
        AssetCode = code, PropertyFileReference = reference
    };

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
