using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class SubledgerSettlementReadModelTranslationTests
{
    [Fact]
    public void Ar_receipt_rebuild_filter_translates_for_sql_server()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused.invalid;Database=translation_only;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        using var context = new ApplicationDbContext(options, Guid.NewGuid());

        var query = SubledgerSettlementReadModelService.BuildArReceiptRebuildQuery(
            context.Set<CustomerPayment>().AsNoTracking(),
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid() },
            DateTime.UtcNow);

        var sql = query.ToQueryString();

        sql.Should().Contain("Cancelled");
        sql.Should().Contain("Bounced");
    }
}
