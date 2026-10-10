using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public sealed record MobilePosMutationCompletion<TResult>(
    TResult Result,
    Guid? MobilePosSaleId = null,
    Guid? CanonicalInvoiceId = null,
    IReadOnlyCollection<Guid>? CanonicalCustomerPaymentIds = null);

public sealed record MobilePosMutationExecution<TResult>(
    Guid ReceiptId,
    bool IsReplay,
    TResult Result);

public interface IMobilePosMutationExecutionService
{
    Task<MobilePosMutationExecution<TResult>> ExecuteAsync<TCommand, TResult>(
        Guid mobilePosDeviceId,
        string clientMutationId,
        string commandType,
        int schemaVersion,
        TCommand command,
        Func<CancellationToken, Task<MobilePosMutationCompletion<TResult>>> handler,
        CancellationToken cancellationToken);
}

public sealed class MobilePosMutationConflictException : InvalidOperationException
{
    public MobilePosMutationConflictException(string message) : base(message)
    {
    }
}

public sealed class MobilePosCommandRejectedException : InvalidOperationException
{
    public MobilePosCommandRejectedException(string code, string detail, bool isReplay = false)
        : base(detail)
    {
        Code = string.IsNullOrWhiteSpace(code) ? "MOBILE_POS_COMMAND_REJECTED" : code.Trim();
        IsReplay = isReplay;
    }

    public string Code { get; }
    public bool IsReplay { get; }
}

/// <summary>
/// Executes a Mobile POS mutation and its Finance writes inside one transaction. The transaction-
/// scoped lock serializes retries for a tenant/device/client key; completed and business-rejected
/// outcomes are replayed only when the server-computed request fingerprint is identical.
/// </summary>
public sealed class MobilePosMutationExecutionService : IMobilePosMutationExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public MobilePosMutationExecutionService(
        ApplicationDbContext db,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A current tenant is required for Mobile POS.");

    public async Task<MobilePosMutationExecution<TResult>> ExecuteAsync<TCommand, TResult>(
        Guid mobilePosDeviceId,
        string clientMutationId,
        string commandType,
        int schemaVersion,
        TCommand command,
        Func<CancellationToken, Task<MobilePosMutationCompletion<TResult>>> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(handler);
        if (mobilePosDeviceId == Guid.Empty)
            throw new ArgumentException("A Mobile POS device is required.", nameof(mobilePosDeviceId));

        var normalizedMutationId = NormalizeRequired(clientMutationId, 100, "client mutation ID");
        var normalizedCommandType = NormalizeRequired(commandType, 100, "command type");
        if (schemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "The schema version must be greater than zero.");

        var tenantId = TenantId;
        var commandJson = JsonSerializer.Serialize(command, JsonOptions);
        var requestHash = ComputeHash($"{normalizedCommandType}\n{schemaVersion}\n{commandJson}");
        if (!await _db.MobilePosDevices.AsNoTracking().AnyAsync(item =>
                item.TenantId == tenantId && item.Id == mobilePosDeviceId && !item.IsDeleted,
                cancellationToken))
        {
            throw new MobilePosCommandRejectedException(
                "MOBILE_POS_DEVICE_NOT_FOUND",
                "The Mobile POS device is not registered in the current tenant.");
        }

        try
        {
            if (IsInMemoryProvider())
            {
                return await ExecuteCoreAsync(
                    tenantId,
                    mobilePosDeviceId,
                    normalizedMutationId,
                    normalizedCommandType,
                    schemaVersion,
                    requestHash,
                    handler,
                    cancellationToken);
            }

            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync(
                    LockResource(tenantId, mobilePosDeviceId, normalizedMutationId),
                    cancellationToken);

                var result = await ExecuteCoreAsync(
                    tenantId,
                    mobilePosDeviceId,
                    normalizedMutationId,
                    normalizedCommandType,
                    schemaVersion,
                    requestHash,
                    handler,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }
        catch (MobilePosCommandRejectedException exception) when (!exception.IsReplay)
        {
            _db.ChangeTracker.Clear();
            await PersistRejectedOutcomeAsync(
                tenantId,
                mobilePosDeviceId,
                normalizedMutationId,
                normalizedCommandType,
                schemaVersion,
                requestHash,
                exception,
                cancellationToken);
            throw;
        }
        catch
        {
            // The relational transaction has already rolled back while unwinding the
            // execution-strategy delegate. Remove entries whose in-memory state still
            // reflects rolled-back SaveChanges calls so an exact retry starts from the
            // durable database state instead of a phantom owner or receipt row.
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<MobilePosMutationExecution<TResult>> ExecuteCoreAsync<TResult>(
        Guid tenantId,
        Guid mobilePosDeviceId,
        string clientMutationId,
        string commandType,
        int schemaVersion,
        string requestHash,
        Func<CancellationToken, Task<MobilePosMutationCompletion<TResult>>> handler,
        CancellationToken cancellationToken)
    {
        var existing = await _db.MobileMutationReceipts.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId
            && item.MobilePosDeviceId == mobilePosDeviceId
            && item.ClientMutationId == clientMutationId
            && !item.IsDeleted,
            cancellationToken);
        if (existing is not null)
            return await ReplayAsync<TResult>(existing, commandType, schemaVersion, requestHash, cancellationToken);

        var now = DateTime.UtcNow;
        var receipt = new MobileMutationReceipt
        {
            TenantId = tenantId,
            MobilePosDeviceId = mobilePosDeviceId,
            ClientMutationId = clientMutationId,
            CommandType = commandType,
            SchemaVersion = schemaVersion,
            RequestHash = requestHash,
            Status = MobileMutationReceiptStatus.Processing,
            StartedAtUtc = now,
            LastAttemptAtUtc = now
        };
        _db.MobileMutationReceipts.Add(receipt);
        await _db.SaveChangesAsync(cancellationToken);

        var completion = await handler(cancellationToken);
        var resultJson = JsonSerializer.Serialize(completion.Result, JsonOptions);
        receipt.Status = MobileMutationReceiptStatus.Completed;
        receipt.ResultJson = resultJson;
        receipt.ResultHash = ComputeHash(resultJson);
        receipt.MobilePosSaleId = completion.MobilePosSaleId;
        receipt.CanonicalInvoiceId = completion.CanonicalInvoiceId;
        receipt.CanonicalCustomerPaymentIdsJson = completion.CanonicalCustomerPaymentIds is null
            ? null
            : JsonSerializer.Serialize(
                completion.CanonicalCustomerPaymentIds.Where(id => id != Guid.Empty).Distinct().ToArray(),
                JsonOptions);
        receipt.CompletedAtUtc = DateTime.UtcNow;
        receipt.LastAttemptAtUtc = receipt.CompletedAtUtc.Value;
        await _db.SaveChangesAsync(cancellationToken);

        return new MobilePosMutationExecution<TResult>(receipt.Id, false, completion.Result);
    }

    private async Task<MobilePosMutationExecution<TResult>> ReplayAsync<TResult>(
        MobileMutationReceipt receipt,
        string commandType,
        int schemaVersion,
        string requestHash,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(receipt.CommandType, commandType, StringComparison.Ordinal)
            || receipt.SchemaVersion != schemaVersion
            || !string.Equals(receipt.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new MobilePosMutationConflictException(
                "The client mutation ID has already been used for a different Mobile POS request.");
        }

        if (receipt.Status == MobileMutationReceiptStatus.Rejected)
        {
            throw new MobilePosCommandRejectedException(
                receipt.ErrorCode ?? "MOBILE_POS_COMMAND_REJECTED",
                receipt.ErrorDetail ?? "The Mobile POS command was rejected.",
                isReplay: true);
        }

        if (receipt.Status != MobileMutationReceiptStatus.Completed || string.IsNullOrWhiteSpace(receipt.ResultJson))
        {
            throw new MobilePosMutationConflictException(
                "The Mobile POS request is already being processed. Retry after the current attempt finishes.");
        }

        var result = JsonSerializer.Deserialize<TResult>(receipt.ResultJson, JsonOptions)
            ?? throw new InvalidOperationException("The stored Mobile POS mutation result could not be restored.");
        receipt.ReplayCount++;
        receipt.LastAttemptAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new MobilePosMutationExecution<TResult>(receipt.Id, true, result);
    }

    private async Task PersistRejectedOutcomeAsync(
        Guid tenantId,
        Guid mobilePosDeviceId,
        string clientMutationId,
        string commandType,
        int schemaVersion,
        string requestHash,
        MobilePosCommandRejectedException exception,
        CancellationToken cancellationToken)
    {
        if (IsInMemoryProvider())
        {
            await PersistRejectedOutcomeCoreAsync(
                tenantId, mobilePosDeviceId, clientMutationId, commandType, schemaVersion,
                requestHash, exception, cancellationToken);
            return;
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            await _unitOfWork.AcquireTransactionLockAsync(
                LockResource(tenantId, mobilePosDeviceId, clientMutationId),
                cancellationToken);
            await PersistRejectedOutcomeCoreAsync(
                tenantId, mobilePosDeviceId, clientMutationId, commandType, schemaVersion,
                requestHash, exception, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task PersistRejectedOutcomeCoreAsync(
        Guid tenantId,
        Guid mobilePosDeviceId,
        string clientMutationId,
        string commandType,
        int schemaVersion,
        string requestHash,
        MobilePosCommandRejectedException exception,
        CancellationToken cancellationToken)
    {
        var receipt = await _db.MobileMutationReceipts.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId
            && item.MobilePosDeviceId == mobilePosDeviceId
            && item.ClientMutationId == clientMutationId
            && !item.IsDeleted,
            cancellationToken);
        if (receipt is not null && receipt.Status == MobileMutationReceiptStatus.Completed)
            return;
        if (receipt is not null &&
            (!string.Equals(receipt.CommandType, commandType, StringComparison.Ordinal)
             || receipt.SchemaVersion != schemaVersion
             || !string.Equals(receipt.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var now = DateTime.UtcNow;
        receipt ??= new MobileMutationReceipt
        {
            TenantId = tenantId,
            MobilePosDeviceId = mobilePosDeviceId,
            ClientMutationId = clientMutationId,
            CommandType = commandType,
            SchemaVersion = schemaVersion,
            RequestHash = requestHash,
            StartedAtUtc = now
        };
        if (receipt.Id == Guid.Empty)
            receipt.Id = Guid.NewGuid();
        if (_db.Entry(receipt).State == EntityState.Detached)
            _db.MobileMutationReceipts.Add(receipt);

        receipt.Status = MobileMutationReceiptStatus.Rejected;
        receipt.ErrorCode = Truncate(exception.Code, 100);
        receipt.ErrorDetail = Truncate(exception.Message, 1000);
        receipt.CompletedAtUtc = now;
        receipt.LastAttemptAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private bool IsInMemoryProvider() => string.Equals(
        _db.Database.ProviderName,
        "Microsoft.EntityFrameworkCore.InMemory",
        StringComparison.Ordinal);

    private static string NormalizeRequired(string? value, int maximumLength, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
            throw new ArgumentException($"The {label} must contain 1 to {maximumLength} characters.");
        return normalized;
    }

    private static string LockResource(Guid tenantId, Guid deviceId, string clientMutationId)
    {
        var keyHash = ComputeHash(clientMutationId)[..24];
        return $"mobile-pos-mutation:{tenantId:N}:{deviceId:N}:{keyHash}";
    }

    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
