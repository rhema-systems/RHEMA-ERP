using System.Collections;
using System.Linq.Expressions;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public class StockMovementCurrencyTests
{
    [Theory]
    [InlineData("GHS", "GHS")]
    [InlineData(" eur ", "EUR")]
    [InlineData(null, null)]
    [InlineData("invalid", null)]
    public async Task ReturnsOnlyCurrentTenantValuationCurrencyForBothMovementSources(string? configured, string? expected)
    {
        var tenant = Guid.NewGuid();
        var stock = new Mock<IStockMovementRepository>();
        var valuation = new Mock<IInventoryMovementRepository>();
        var settings = new Mock<IGenericRepository<FinanceSettings>>();
        var unit = new Mock<IUnitOfWork>();
        var user = new Mock<ICurrentUserProvider>();
        user.SetupGet(u => u.TenantId).Returns(tenant);
        var rows = new[] {
            new FinanceSettings { TenantId = Guid.NewGuid(), BaseCurrency = "USD" },
            new FinanceSettings { TenantId = tenant, BaseCurrency = "USD", IsDeleted = true },
            new FinanceSettings { TenantId = tenant, BaseCurrency = configured! },
        };
        settings.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<FinanceSettings, bool>>>()))
            .ReturnsAsync((Expression<Func<FinanceSettings, bool>> predicate) => rows.SingleOrDefault(predicate.Compile()));
        unit.Setup(u => u.Repository<FinanceSettings>()).Returns(settings.Object);
        stock.Setup(r => r.GetQueryable(It.IsAny<Expression<Func<StockMovement, bool>>>()))
            .Returns((Expression<Func<StockMovement, bool>> predicate) => new AsyncQuery<StockMovement>(new[] {
                new StockMovement { Id = Guid.NewGuid(), TenantId = tenant, MovementType = "Receipt", MovementDate = DateTime.UtcNow.AddMinutes(-1) }
            }.Where(predicate.Compile())));
        valuation.Setup(r => r.GetQueryable(It.IsAny<Expression<Func<InventoryMovement, bool>>>()))
            .Returns((Expression<Func<InventoryMovement, bool>> predicate) => new AsyncQuery<InventoryMovement>(new[] {
                new InventoryMovement { Id = Guid.NewGuid(), TenantId = tenant, MovementType = InventoryMovementType.PurchaseReceipt,
                    Direction = MovementDirection.In, IsPosted = true, MovementDate = DateTime.UtcNow.AddMinutes(-1) }
            }.Where(predicate.Compile())));
        var controller = new StockMovementsController(stock.Object, valuation.Object, user.Object,
            Mock.Of<ILogger<StockMovementsController>>(), unit.Object);
        var result = await controller.GetStockMovements();
        var values = Assert.IsAssignableFrom<IEnumerable<StockMovementDto>>(Assert.IsType<OkObjectResult>(result.Result).Value).ToList();
        Assert.Equal(2, values.Count);
        Assert.All(values, value => Assert.Equal(expected, value.CurrencyCode));
        unit.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // Lightweight async LINQ source: tests controller mapping without rebuilding the ERP model.
    private sealed class AsyncQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public AsyncQuery(IEnumerable<T> values) : base(values) { }
        public AsyncQuery(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new AsyncProvider<T>(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token = default) => new AsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }
    private sealed class AsyncProvider<T>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => new AsyncQuery<T>(expression);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new AsyncQuery<TElement>(expression);
        public object? Execute(Expression expression) => inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken token = default) => throw new NotSupportedException();
    }
    private sealed class AsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    }
}
