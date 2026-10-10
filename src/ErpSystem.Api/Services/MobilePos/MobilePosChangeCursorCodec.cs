using System.Text;
using System.Text.Json;

namespace ErpSystem.Api.Services.MobilePos;

internal sealed record MobilePosChangeCursor(
    Guid TenantId,
    Guid ScopeId,
    DateTime SinceUtc,
    DateTime SnapshotAtUtc,
    int Offset);

internal static class MobilePosChangeCursorCodec
{
    public static MobilePosChangeCursor Create(Guid tenantId, Guid scopeId, DateTime? sinceUtc)
    {
        var since = sinceUtc?.ToUniversalTime() ?? DateTime.UnixEpoch;
        var snapshot = DateTime.UtcNow;
        if (since > snapshot)
            throw Invalid("The change watermark cannot be in the future.");
        return new MobilePosChangeCursor(tenantId, scopeId, since, snapshot, 0);
    }

    public static string Encode(MobilePosChangeCursor cursor)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor));
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static MobilePosChangeCursor Decode(string value, Guid tenantId, Guid scopeId)
    {
        try
        {
            var base64 = value.Trim().Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            var cursor = JsonSerializer.Deserialize<MobilePosChangeCursor>(Convert.FromBase64String(base64));
            if (cursor == null
                || cursor.TenantId != tenantId
                || cursor.ScopeId != scopeId
                || cursor.Offset < 0
                || cursor.Offset > 1_000_000
                || cursor.SinceUtc.Kind != DateTimeKind.Utc
                || cursor.SnapshotAtUtc.Kind != DateTimeKind.Utc
                || cursor.SinceUtc > cursor.SnapshotAtUtc
                || cursor.SnapshotAtUtc > DateTime.UtcNow.AddMinutes(1))
            {
                throw new InvalidOperationException();
            }
            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            throw Invalid("The change cursor is invalid, belongs to another assignment, or has expired.");
        }
    }

    private static MobilePosCommandRejectedException Invalid(string detail) =>
        new("MOBILE_POS_CHANGE_CURSOR_INVALID", detail);
}
