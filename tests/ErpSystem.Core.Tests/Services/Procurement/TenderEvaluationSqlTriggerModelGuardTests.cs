using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderEvaluationSqlTriggerModelGuardTests
{
    [Fact]
    public void Tender_evaluation_disables_sql_output_for_committee_projection_trigger()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=TenderEvaluationTriggerModelOnly;Trusted_Connection=True")
            .Options;

        using var context = new ApplicationDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(TenderEvaluation));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("TenderEvaluations");
        entityType.FindAnnotation("SqlServer:UseSqlOutputClause")
            .Should().NotBeNull("TenderEvaluations has an enabled SQL Server projection trigger");
        entityType.FindAnnotation("SqlServer:UseSqlOutputClause")!.Value
            .Should().Be(false,
                "EF OUTPUT without INTO is invalid for the triggered TenderEvaluations table");
    }
}
