using ErpSystem.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class UnitOfWorkTransactionLockTests
{
    [Fact]
    public async Task InMemory_requires_unit_of_work_transaction_before_provider_local_no_op()
    {
        await using var db = InMemoryContext();
        using var unitOfWork = new UnitOfWork(db);

        var withoutTransaction = async () =>
            await unitOfWork.AcquireTransactionLockAsync("test:in-memory");

        await withoutTransaction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A database transaction is required*");

        await unitOfWork.BeginTransactionAsync();
        await unitOfWork.AcquireTransactionLockAsync("test:in-memory");
        await unitOfWork.RollbackAsync();
    }

    [Fact]
    public async Task Sqlite_without_database_transaction_fails_closed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = SqliteContext(connection);
        using var unitOfWork = new UnitOfWork(db);

        var action = async () =>
            await unitOfWork.AcquireTransactionLockAsync("test:sqlite:no-transaction");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A database transaction is required*");
    }

    [Fact]
    public async Task Sqlite_active_database_transaction_supplies_locking_without_sql_server_command()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var commands = new CommandRecorder();
        await using var db = SqliteContext(connection, commands);
        using var unitOfWork = new UnitOfWork(db);

        await unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await unitOfWork.AcquireTransactionLockAsync("test:sqlite:active");

        commands.CommandTexts.Should().NotContain(text =>
            text.Contains("sp_getapplock", StringComparison.OrdinalIgnoreCase));
        await unitOfWork.RollbackAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_resource_is_rejected_after_active_transaction_guard(string resource)
    {
        await using var db = InMemoryContext();
        using var unitOfWork = new UnitOfWork(db);
        await unitOfWork.BeginTransactionAsync();

        var action = async () => await unitOfWork.AcquireTransactionLockAsync(resource);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("resource");
        await unitOfWork.RollbackAsync();
    }

    [Fact]
    public async Task Overlong_resource_is_rejected_after_active_transaction_guard()
    {
        await using var db = InMemoryContext();
        using var unitOfWork = new UnitOfWork(db);
        await unitOfWork.BeginTransactionAsync();

        var action = async () =>
            await unitOfWork.AcquireTransactionLockAsync(new string('x', 256));

        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("resource");
        await unitOfWork.RollbackAsync();
    }

    [Fact]
    public async Task Unknown_relational_provider_fails_before_dialect_specific_sql()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var commands = new CommandRecorder();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IDatabaseProvider, UnsupportedDatabaseProvider>()
            .AddInterceptors(commands)
            .Options;
        await using var db = new ApplicationDbContext(options);
        using var unitOfWork = new UnitOfWork(db);
        await unitOfWork.BeginTransactionAsync();

        var action = async () =>
            await unitOfWork.AcquireTransactionLockAsync("test:unsupported");

        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*UnsupportedDatabaseProvider*");
        commands.CommandTexts.Should().NotContain(text =>
            text.Contains("sp_getapplock", StringComparison.OrdinalIgnoreCase));
        await unitOfWork.RollbackAsync();
    }

    [Fact]
    public void SqlServer_path_retains_checked_transaction_owned_application_lock()
    {
        var source = File.ReadAllText(SourcePath("src", "ErpSystem.Data", "UnitOfWork.cs"));

        source.Should().Contain("Microsoft.EntityFrameworkCore.SqlServer")
            .And.Contain("EXEC @result = sys.sp_getapplock")
            .And.Contain("@LockOwner = 'Transaction'")
            .And.Contain("SELECT @result;")
            .And.Contain("await command.ExecuteScalarAsync(cancellationToken)")
            .And.Contain("if (result < 0)")
            .And.Contain("throw new TimeoutException");
    }

    private static ApplicationDbContext InMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ApplicationDbContext SqliteContext(
        SqliteConnection connection,
        params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection);
        if (interceptors.Length > 0)
            builder.AddInterceptors(interceptors);
        return new ApplicationDbContext(builder.Options);
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;
        var root = directory?.FullName
            ?? throw new InvalidOperationException("Repository root was not found.");
        return Path.Combine([root, .. parts]);
    }

    private sealed class UnsupportedDatabaseProvider : IDatabaseProvider
    {
        public string Name => nameof(UnsupportedDatabaseProvider);

        public bool IsConfigured(IDbContextOptions options) => true;
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> CommandTexts { get; } = [];

        public override InterceptionResult<object> ScalarExecuting(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            CommandTexts.Add(command.CommandText);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            CommandTexts.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
