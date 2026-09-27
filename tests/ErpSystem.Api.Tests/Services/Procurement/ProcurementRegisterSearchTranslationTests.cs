using System.Data.Common;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Procurement;

public sealed class ProcurementRegisterSearchTranslationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegisterSearchTranslatesOnSqlServerBeforeOpeningAConnection(bool purchaseOrder)
    {
        // EF compiles the real repository query before opening its connection. Stop there:
        // no server or database is contacted, while unsupported LINQ raises a different
        // exception before this interceptor can run. This exercises the actual predicate,
        // including its navigation fields, rather than copying it into a test query.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused.invalid;Database=translation_only;Integrated Security=True;TrustServerCertificate=True")
            .AddInterceptors(new StopBeforeConnection())
            .Options;
        await using var db = new ApplicationDbContext(options, Guid.NewGuid());
        var settings = Mock.Of<IProcurementSettingsRepository>();

        if (purchaseOrder)
        {
            var repository = new PurchaseOrderRepository(db, settings);
            await Assert.ThrowsAsync<SqlTranslationCompletedException>(() =>
                repository.GetPurchaseOrdersAsync(1, 5, "mIxEd"));
        }
        else
        {
            var repository = new PurchaseRequisitionRepository(db, settings, Mock.Of<ITenantContext>());
            await Assert.ThrowsAsync<SqlTranslationCompletedException>(() =>
                repository.GetRequisitionsAsync(1, 5, "mIxEd"));
        }
    }

    private sealed class SqlTranslationCompletedException : Exception { }

    private sealed class StopBeforeConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) => throw new SqlTranslationCompletedException();
    }
}
