using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.MobilePos;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosSyncService
{
    Task<MobilePosSyncPushResultDto> PushAsync(
        MobilePosSyncPushRequestDto request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Accepts one durable device command at a time. Business rejections and idempotency conflicts are
/// returned as terminal command states so the client can move its outbox without guessing from an
/// HTTP transport failure. Unexpected failures remain failed HTTP requests and are never labelled
/// as a completed command.
/// </summary>
public sealed class MobilePosSyncService : IMobilePosSyncService
{
    private const string CashSaleCommand = "CashSale";
    private const string CashReceiptCommand = "CashReceipt";
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMobilePosOfflineGrantValidationService _grants;
    private readonly IMobilePosSaleService _sales;
    private readonly IMobilePosCollectionService _collections;

    public MobilePosSyncService(
        IMobilePosOfflineGrantValidationService grants,
        IMobilePosSaleService sales,
        IMobilePosCollectionService collections)
    {
        _grants = grants;
        _sales = sales;
        _collections = collections;
    }

    public async Task<MobilePosSyncPushResultDto> PushAsync(
        MobilePosSyncPushRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var mutationId = Required(request.ClientMutationId, 100, "client mutation ID");
        try
        {
            var commandType = request.CommandType?.Trim() ?? string.Empty;
            if ((!string.Equals(commandType, CashSaleCommand, StringComparison.Ordinal)
                 && !string.Equals(commandType, CashReceiptCommand, StringComparison.Ordinal))
                || request.SchemaVersion != CurrentSchemaVersion)
            {
                throw Reject(
                    "MOBILE_POS_SYNC_COMMAND_NOT_SUPPORTED",
                    "This Mobile POS sync command type or schema version is not supported.");
            }
            if (request.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                throw Reject("MOBILE_POS_SYNC_PAYLOAD_REQUIRED", "The Mobile POS sync payload is required.");

            var canonicalPayload = Canonicalize(request.Payload);
            var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)))
                .ToLowerInvariant();
            if (!FixedTimeHexEquals(payloadHash, request.PayloadHash))
                throw Reject("MOBILE_POS_SYNC_PAYLOAD_HASH_MISMATCH", "The queued command payload no longer matches its recorded hash.");

            return string.Equals(commandType, CashSaleCommand, StringComparison.Ordinal)
                ? await PushSaleAsync(request, mutationId, cancellationToken)
                : await PushCollectionAsync(request, mutationId, cancellationToken);
        }
        catch (MobilePosMutationConflictException exception)
        {
            return Failure("Conflict", mutationId, "MOBILE_POS_SYNC_IDEMPOTENCY_CONFLICT", exception.Message);
        }
        catch (MobilePosCommandRejectedException exception)
        {
            return Failure("Rejected", mutationId, exception.Code, exception.Message);
        }
    }

    private async Task<MobilePosSyncPushResultDto> PushSaleAsync(
        MobilePosSyncPushRequestDto request,
        string mutationId,
        CancellationToken cancellationToken)
    {
        MobilePosCompleteSaleRequestDto command;
        try
        {
            command = request.Payload.Deserialize<MobilePosCompleteSaleRequestDto>(JsonOptions)
                ?? throw new JsonException("The payload was empty.");
        }
        catch (JsonException)
        {
            throw Reject("MOBILE_POS_SYNC_PAYLOAD_INVALID", "The queued cash sale payload could not be read.");
        }
        ValidateIdentity(request, mutationId, command.ClientMutationId, command.LocalReference);
        if (!command.OccurredAtUtc.HasValue)
            throw Reject("MOBILE_POS_OFFLINE_OCCURRED_AT_REQUIRED", "The queued sale does not retain its device occurrence time.");

        var authorization = await _grants.AuthorizeAsync(
            CreateGrantRequest(request, CashSaleCommand, command.OccurredAtUtc.Value,
                command.ExpectedTotalAmount, command.Tenders), cancellationToken);
        var sale = await _sales.CompleteOfflineAsync(command, authorization, cancellationToken);
        return new MobilePosSyncPushResultDto
        {
            State = "Synced",
            ClientMutationId = mutationId,
            Sale = sale
        };
    }

    private async Task<MobilePosSyncPushResultDto> PushCollectionAsync(
        MobilePosSyncPushRequestDto request,
        string mutationId,
        CancellationToken cancellationToken)
    {
        MobilePosCompleteCollectionRequestDto command;
        try
        {
            command = request.Payload.Deserialize<MobilePosCompleteCollectionRequestDto>(JsonOptions)
                ?? throw new JsonException("The payload was empty.");
        }
        catch (JsonException)
        {
            throw Reject("MOBILE_POS_SYNC_PAYLOAD_INVALID", "The queued customer collection payload could not be read.");
        }
        ValidateIdentity(request, mutationId, command.ClientMutationId, command.LocalReference);
        if (!command.OccurredAtUtc.HasValue)
            throw Reject("MOBILE_POS_OFFLINE_OCCURRED_AT_REQUIRED", "The queued collection does not retain its device occurrence time.");

        var authorization = await _grants.AuthorizeAsync(
            CreateGrantRequest(request, CashReceiptCommand, command.OccurredAtUtc.Value,
                command.Allocations.Sum(item => item.Amount), command.Tenders), cancellationToken);
        var collection = await _collections.CompleteOfflineAsync(command, authorization, cancellationToken);
        return new MobilePosSyncPushResultDto
        {
            State = "Synced",
            ClientMutationId = mutationId,
            Collection = collection
        };
    }

    private static MobilePosOfflineGrantValidationRequest CreateGrantRequest(
        MobilePosSyncPushRequestDto request,
        string commandType,
        DateTime occurredAtUtc,
        decimal transactionAmount,
        IReadOnlyCollection<MobilePosTenderInputDto> tenders) => new(
            Required(request.OfflineGrantToken, 8_000, "offline grant token"),
            Required(request.OfflineGrantId, "offline grant ID"),
            Required(request.DeviceId, "device ID"),
            Required(request.StoreId, "store ID"),
            Required(request.TillId, "till ID"),
            Required(request.TillSessionId, "till session ID"),
            commandType,
            CurrentSchemaVersion,
            occurredAtUtc,
            transactionAmount,
            tenders);

    private static void ValidateIdentity(
        MobilePosSyncPushRequestDto request,
        string mutationId,
        string? payloadMutationId,
        string? payloadLocalReference)
    {
        if (!string.Equals(payloadMutationId?.Trim(), mutationId, StringComparison.Ordinal)
            || !string.Equals(payloadLocalReference?.Trim(),
                Required(request.LocalReference, 100, "local reference"), StringComparison.Ordinal))
        {
            throw Reject(
                "MOBILE_POS_SYNC_ENVELOPE_MISMATCH",
                "The queued command identity does not match its synchronization envelope.");
        }
    }

    private static MobilePosSyncPushResultDto Failure(
        string state,
        string mutationId,
        string code,
        string detail) => new()
        {
            State = state,
            ClientMutationId = mutationId,
            ErrorCode = code,
            ErrorDetail = detail
        };

    private static string Canonicalize(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{JsonSerializer.Serialize(property.Name)}:{Canonicalize(property.Value)}")) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonicalize)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
        JsonValueKind.Number => CanonicalNumber(element),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => throw Reject("MOBILE_POS_SYNC_PAYLOAD_INVALID", "The queued command contains an unsupported JSON value.")
    };

    private static string CanonicalNumber(JsonElement element)
    {
        var raw = element.GetRawText();
        if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
            return decimalValue.ToString("G29", CultureInfo.InvariantCulture);
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue)
            && double.IsFinite(doubleValue))
            return doubleValue.ToString("R", CultureInfo.InvariantCulture).ToLowerInvariant();
        throw Reject("MOBILE_POS_SYNC_PAYLOAD_INVALID", "The queued command contains an invalid number.");
    }

    private static bool FixedTimeHexEquals(string left, string? right)
    {
        try
        {
            var leftBytes = Convert.FromHexString(left);
            var rightBytes = Convert.FromHexString(right?.Trim() ?? string.Empty);
            return leftBytes.Length == rightBytes.Length
                && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static Guid Required(Guid value, string label) => value != Guid.Empty
        ? value
        : throw Reject("MOBILE_POS_SYNC_ENVELOPE_INVALID", $"The {label} is required.");

    private static string Required(string? value, int maximumLength, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
            throw Reject("MOBILE_POS_SYNC_ENVELOPE_INVALID", $"The {label} must contain 1 to {maximumLength} characters.");
        return normalized;
    }

    private static MobilePosCommandRejectedException Reject(string code, string detail) => new(code, detail);
}
