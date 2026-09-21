using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveySqlTriggerModelGuardTests
{
    [Theory]
    [InlineData("ProjectBoqVersions")]
    [InlineData("ProjectBoqVersionLines")]
    [InlineData("ProjectBoqRemeasurementRevisions")]
    [InlineData("ProjectBoqRemeasurementLines")]
    [InlineData("ProjectBoqRemeasurementSources")]
    [InlineData("QuantitySurveyRateBuildUps")]
    [InlineData("QuantitySurveyRateBuildUpLines")]
    [InlineData("QuantitySurveyEstimateVersions")]
    [InlineData("QuantitySurveyEstimateLines")]
    [InlineData("QuantitySurveyEstimateAssumptions")]
    [InlineData("QuantitySurveyEstimateMarkups")]
    [InlineData("QuantitySurveyEstimateRevisions")]
    [InlineData("QuantitySurveyMeasurementSheets")]
    [InlineData("QuantitySurveyMeasurementLines")]
    [InlineData("QuantitySurveyMeasurementAttachments")]
    [InlineData("QuantitySurveyMeasurementRevisions")]
    [InlineData("QuantitySurveyValuationWorksheets")]
    [InlineData("QuantitySurveyValuationWorksheetLines")]
    [InlineData("QuantitySurveyValuationWorksheetEvidence")]
    [InlineData("QuantitySurveyValuationWorksheetRevisions")]
    [InlineData("ProjectPaymentCertificates")]
    [InlineData("QuantitySurveyPaymentCertificateRevisions")]
    public void Governed_lifecycle_tables_disable_sql_output_for_enabled_triggers(string tableName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=QsTriggerModelOnly;Trusted_Connection=True")
            .Options;

        using var context = new ApplicationDbContext(options);
        var entityType = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == tableName);

        entityType.FindAnnotation("SqlServer:UseSqlOutputClause")
            .Should().NotBeNull($"{tableName} has an enabled SQL Server trigger");
        entityType.FindAnnotation("SqlServer:UseSqlOutputClause")!.Value
            .Should().Be(false, $"EF OUTPUT without INTO is invalid for triggered table {tableName}");
    }
}
