using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ErpSystem.Api.Configuration;

/// <summary>
/// Parses the bounded command timeout accepted only by the migration and full-seed CLI entry points.
/// Ordinary web requests retain the provider's normal command timeout.
/// </summary>
internal sealed record MigrationCommandOptions(int CommandTimeoutSeconds)
{
    internal const string TimeoutArgument = "--migration-command-timeout-seconds";
    internal const int DefaultTimeoutSeconds = 600;
    internal const int MinimumTimeoutSeconds = 30;
    internal const int MaximumTimeoutSeconds = 900;

    internal static MigrationCommandOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 ||
            (!string.Equals(args[0], "apply-migrations", StringComparison.Ordinal) &&
             !string.Equals(args[0], "seed-db", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Migration command options require the apply-migrations or seed-db entry point.");
        }

        var timeoutSeconds = DefaultTimeoutSeconds;
        var timeoutSupplied = false;
        for (var index = 1; index < args.Length; index++)
        {
            if (!string.Equals(args[index], TimeoutArgument, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported migration/seed command argument '{args[index]}'.");
            }

            if (timeoutSupplied || index + 1 >= args.Length)
            {
                throw new InvalidOperationException($"{TimeoutArgument} must be supplied exactly once with one value.");
            }

            var value = args[++index];
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
                parsed < MinimumTimeoutSeconds || parsed > MaximumTimeoutSeconds)
            {
                throw new InvalidOperationException(
                    $"{TimeoutArgument} must be a whole number from {MinimumTimeoutSeconds} through {MaximumTimeoutSeconds}.");
            }

            timeoutSeconds = parsed;
            timeoutSupplied = true;
        }

        return new MigrationCommandOptions(timeoutSeconds);
    }

    internal void ApplyAndAssertTo(DatabaseFacade database)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (database.CreateExecutionStrategy().RetriesOnFailure)
        {
            throw new InvalidOperationException("Migration-only commands forbid automatic execution-strategy retries.");
        }

        database.SetCommandTimeout(TimeSpan.FromSeconds(CommandTimeoutSeconds));
        if (database.GetCommandTimeout() != CommandTimeoutSeconds)
        {
            throw new InvalidOperationException("The migration command timeout was not applied exactly.");
        }
    }
}
