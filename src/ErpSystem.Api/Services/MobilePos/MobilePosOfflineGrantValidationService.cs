using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public sealed record MobilePosOfflineGrantValidationRequest(
    string Token,
    Guid OfflineGrantId,
    Guid DeviceId,
    Guid StoreId,
    Guid TillId,
    Guid TillSessionId,
    string CommandType,
    int SchemaVersion,
    DateTime OccurredAtUtc,
    decimal TransactionAmount,
    IReadOnlyCollection<MobilePosTenderInputDto> Tenders);

public sealed record MobilePosOfflineGrantAuthorization(
    MobilePosOfflineGrant Grant,
    MobilePosOfflineGrantPolicySnapshotDto Policy);

public interface IMobilePosOfflineGrantValidationService
{
    Task<MobilePosOfflineGrantAuthorization> AuthorizeAsync(
        MobilePosOfflineGrantValidationRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Revalidates every offline command against both the signed authorization and the persisted grant.
/// The grant may have reached its normal expiry after the command was recorded, but the command time
/// must be inside the signed window and a revoked grant or device is never accepted.
/// </summary>
public sealed class MobilePosOfflineGrantValidationService : IMobilePosOfflineGrantValidationService
{
    private const string CashSaleCommand = "CashSale";
    private const int CashSaleSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMobilePosOfflineGrantTokenService _tokens;

    public MobilePosOfflineGrantValidationService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IMobilePosOfflineGrantTokenService tokens)
    {
        _db = db;
        _currentUser = currentUser;
        _tokens = tokens;
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A current tenant is required for Mobile POS offline synchronization.");

    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated user is required for Mobile POS offline synchronization.");

    public async Task<MobilePosOfflineGrantAuthorization> AuthorizeAsync(
        MobilePosOfflineGrantValidationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var occurredAtUtc = request.OccurredAtUtc.Kind == DateTimeKind.Utc
            ? request.OccurredAtUtc
            : request.OccurredAtUtc.ToUniversalTime();
        var serverNow = DateTime.UtcNow;
        if (occurredAtUtc > serverNow.AddMinutes(5))
            throw Reject("MOBILE_POS_OFFLINE_DEVICE_CLOCK_AHEAD", "The offline command time is too far ahead of the server clock.");
        if (request.TransactionAmount <= 0m)
            throw Reject("MOBILE_POS_OFFLINE_AMOUNT_INVALID", "The offline transaction amount must be greater than zero.");

        MobilePosOfflineGrantTokenPayload token;
        try
        {
            token = _tokens.Validate(request.Token, occurredAtUtc);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            throw Reject("MOBILE_POS_OFFLINE_GRANT_TOKEN_INVALID", exception.Message);
        }

        var tenantId = TenantId;
        var userId = UserId;
        var grant = await _db.MobilePosOfflineGrants
            .Include(item => item.MobilePosDevice)
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.OfflineGrantId,
                cancellationToken)
            ?? throw Reject("MOBILE_POS_OFFLINE_GRANT_NOT_FOUND", "The offline authorization no longer exists in this tenant.");
        if (grant.Status == MobilePosOfflineGrantStatus.Revoked)
            throw Reject("MOBILE_POS_OFFLINE_GRANT_REVOKED", "The offline authorization was revoked and cannot be synchronized.");
        if (grant.Status is not (MobilePosOfflineGrantStatus.Active or MobilePosOfflineGrantStatus.Expired))
            throw Reject("MOBILE_POS_OFFLINE_GRANT_STATUS_INVALID", "The offline authorization is not in a synchronizable state.");

        if (token.GrantId != grant.Id || token.TenantId != tenantId || token.UserId != userId
            || token.DeviceId != request.DeviceId || token.StoreId != request.StoreId
            || token.TillId != request.TillId || token.CashierTillSessionId != request.TillSessionId
            || token.OfflinePolicyId != grant.MobilePosOfflinePolicyId)
        {
            throw Reject("MOBILE_POS_OFFLINE_GRANT_CONTEXT_MISMATCH", "The signed authorization does not match the command, user, tenant, device, store, till, or session.");
        }
        if (grant.UserId != token.UserId || grant.MobilePosDeviceId != token.DeviceId
            || grant.MobilePosStoreId != token.StoreId || grant.MobilePosTillId != token.TillId
            || grant.CashierTillSessionId != token.CashierTillSessionId
            || grant.RevocationEpoch != token.RevocationEpoch
            || grant.IssuedAtUtc.ToUniversalTime() != token.IssuedAtUtc.ToUniversalTime()
            || grant.ExpiresAtUtc.ToUniversalTime() != token.ExpiresAtUtc.ToUniversalTime())
        {
            throw Reject("MOBILE_POS_OFFLINE_GRANT_RECORD_MISMATCH", "The persisted offline authorization no longer matches its signed token.");
        }
        if (occurredAtUtc < grant.IssuedAtUtc.ToUniversalTime()
            || occurredAtUtc > grant.ExpiresAtUtc.ToUniversalTime())
        {
            throw Reject("MOBILE_POS_OFFLINE_COMMAND_OUTSIDE_WINDOW", "The offline command was recorded outside its authorized time window.");
        }

        var device = grant.MobilePosDevice;
        if (device.Status != MobilePosDeviceStatus.Active || device.RevocationEpoch != token.RevocationEpoch)
            throw Reject("MOBILE_POS_OFFLINE_DEVICE_REVOKED", "The device is no longer active at the authorization epoch.");
        if (device.MobilePosStoreId != token.StoreId || device.MobilePosTillId != token.TillId)
            throw Reject("MOBILE_POS_OFFLINE_DEVICE_ASSIGNMENT_CHANGED", "The device store or till assignment changed after offline authorization.");

        var policyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(grant.PolicySnapshotJson)));
        if (!FixedTimeHexEquals(policyHash, grant.PolicySnapshotHash)
            || !FixedTimeHexEquals(policyHash, token.PolicySnapshotHash))
        {
            throw Reject("MOBILE_POS_OFFLINE_POLICY_SNAPSHOT_INVALID", "The stored offline policy snapshot does not match the signed authorization.");
        }
        MobilePosOfflineGrantPolicySnapshotDto policy;
        try
        {
            policy = JsonSerializer.Deserialize<MobilePosOfflineGrantPolicySnapshotDto>(grant.PolicySnapshotJson, JsonOptions)
                ?? throw new JsonException("The snapshot was empty.");
        }
        catch (JsonException)
        {
            throw Reject("MOBILE_POS_OFFLINE_POLICY_SNAPSHOT_INVALID", "The stored offline policy snapshot could not be read.");
        }
        if (policy.PolicyId != grant.MobilePosOfflinePolicyId)
            throw Reject("MOBILE_POS_OFFLINE_POLICY_SNAPSHOT_INVALID", "The stored offline policy snapshot belongs to another policy.");

        var commandType = request.CommandType?.Trim() ?? string.Empty;
        if (!string.Equals(commandType, CashSaleCommand, StringComparison.Ordinal)
            || request.SchemaVersion != CashSaleSchemaVersion
            || !policy.AllowedCommandTypes.Contains(CashSaleCommand, StringComparer.Ordinal))
        {
            throw Reject("MOBILE_POS_OFFLINE_COMMAND_NOT_ALLOWED", "This offline command type or schema version was not authorized by the signed policy.");
        }
        ValidateTenders(request.Tenders, policy);
        if (policy.MaximumTransactionAmount.HasValue
            && request.TransactionAmount > policy.MaximumTransactionAmount.Value)
        {
            throw Reject("MOBILE_POS_OFFLINE_TRANSACTION_LIMIT_EXCEEDED", "The offline transaction exceeds the signed per-transaction limit.");
        }

        var priorSales = await _db.MobilePosSales.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.MobilePosOfflineGrantId == grant.Id
                && item.Status == MobilePosSaleStatus.Completed && !item.IsDeleted)
            .Select(item => item.TotalAmount)
            .ToListAsync(cancellationToken);
        if (policy.MaximumTransactionCount.HasValue
            && priorSales.Count + 1 > policy.MaximumTransactionCount.Value)
        {
            throw Reject("MOBILE_POS_OFFLINE_TRANSACTION_COUNT_EXCEEDED", "The signed offline transaction-count limit has been reached.");
        }
        if (policy.MaximumAggregateAmount.HasValue
            && priorSales.Sum() + request.TransactionAmount > policy.MaximumAggregateAmount.Value)
        {
            throw Reject("MOBILE_POS_OFFLINE_AGGREGATE_LIMIT_EXCEEDED", "The signed offline aggregate amount limit would be exceeded.");
        }

        var sessionMatches = await _db.CashierTillSessions.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && item.Id == token.CashierTillSessionId
            && item.CashierUserId == userId && !item.IsDeleted,
            cancellationToken);
        if (!sessionMatches)
            throw Reject("MOBILE_POS_OFFLINE_TILL_SESSION_MISMATCH", "The signed cashier till session does not exist for this operator.");

        return new MobilePosOfflineGrantAuthorization(grant, policy);
    }

    private static void ValidateTenders(
        IReadOnlyCollection<MobilePosTenderInputDto> tenders,
        MobilePosOfflineGrantPolicySnapshotDto policy)
    {
        if (tenders.Count is < 1 or > 10)
            throw Reject("MOBILE_POS_OFFLINE_TENDERS_INVALID", "An offline sale must contain between one and ten tenders.");
        var allowed = policy.AllowedPaymentMethods.ToDictionary(item => item.PaymentMethodId);
        foreach (var tender in tenders)
        {
            if (!allowed.TryGetValue(tender.PaymentMethodId, out var method))
                throw Reject("MOBILE_POS_OFFLINE_TENDER_NOT_ALLOWED", "A tender was not included in the signed offline policy.");
            var reference = string.IsNullOrWhiteSpace(tender.ExternalReference) ? null : tender.ExternalReference.Trim();
            if (method.RequiresReference && reference is null)
                throw Reject("MOBILE_POS_OFFLINE_TENDER_REFERENCE_REQUIRED", $"{method.Name} requires its external authorization reference.");
            if (!string.Equals(method.Type, PaymentMethodType.Cash.ToString(), StringComparison.OrdinalIgnoreCase)
                && method.RequireExternalAuthorizationReference && reference is null)
            {
                throw Reject("MOBILE_POS_OFFLINE_ELECTRONIC_TENDER_UNAUTHORIZED", $"{method.Name} must be completed externally before it is recorded offline.");
            }
        }
    }

    private static bool FixedTimeHexEquals(string left, string right)
    {
        try
        {
            var leftBytes = Convert.FromHexString(left);
            var rightBytes = Convert.FromHexString(right);
            return leftBytes.Length == rightBytes.Length
                && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static MobilePosCommandRejectedException Reject(string code, string detail) => new(code, detail);
}
