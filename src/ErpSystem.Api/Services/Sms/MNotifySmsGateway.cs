using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErpSystem.Api.Services.Sms;

internal static class MNotifySmsGateway
{
    internal const string DefaultEndpoint = "https://api.mnotify.com/api/sms/quick";
    internal const string BalanceEndpoint = "https://api.mnotify.com/api/balance/sms";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal static async Task SendAsync(
        HttpClient client,
        GhanaGatewaySmsOptions options,
        string toPhoneNumber,
        string message,
        bool isOtp,
        CancellationToken cancellationToken)
    {
        if (options.Enabled != true)
            throw new InvalidOperationException("mNotify SMS is not enabled.");

        var endpoint = string.IsNullOrWhiteSpace(options.UrlTemplate)
            ? DefaultEndpoint
            : options.UrlTemplate.Trim();
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            !string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(endpointUri.Host, "api.mnotify.com", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(endpointUri.AbsolutePath.TrimEnd('/'), "/api/sms/quick", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(endpointUri.Query))
        {
            throw new InvalidOperationException($"mNotify endpoint must be {DefaultEndpoint} without query parameters.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new InvalidOperationException("mNotify SMS is enabled but the API key is not configured.");
        if (string.IsNullOrWhiteSpace(options.SenderId))
            throw new InvalidOperationException("mNotify SMS is enabled but the sender ID is not configured.");
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("SMS message is required.", nameof(message));

        var requestUri = AppendApiKey(endpointUri, options.ApiKey);
        var payload = new MNotifyQuickSmsRequest
        {
            Recipient = new[] { NormalizeRecipient(toPhoneNumber) },
            Sender = options.SenderId.Trim(),
            Message = message,
            IsSchedule = false,
            ScheduleDate = string.Empty,
            SmsType = isOtp ? "otp" : null
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = DeserializeResponse(responseBody);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"mNotify SMS failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase})." +
                FormatProviderFailure(result));
        }

        if (result is null ||
            !string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(result.Code, "2000", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("mNotify SMS was not accepted." + FormatProviderFailure(result));
        }
    }

    internal static string NormalizeRecipient(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

        var trimmed = phoneNumber.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("233", StringComparison.Ordinal))
            return $"0{digits[3..]}";
        if (digits.Length == 10 && digits[0] == '0')
            return digits;
        if (trimmed.StartsWith('+') && digits.Length is >= 8 and <= 15)
            return digits;

        throw new ArgumentException("Phone number must be a valid local Ghana or international number.", nameof(phoneNumber));
    }

    internal static async Task<MNotifySmsBalance> GetBalanceAsync(
        HttpClient client,
        string apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("mNotify API key is not configured.");

        var requestUri = AppendApiKey(new Uri(BalanceEndpoint), apiKey);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = DeserializeBalanceResponse(responseBody);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"mNotify balance check failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase})." +
                FormatBalanceFailure(result));
        }

        if (result is null || !string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("mNotify balance check was not accepted." + FormatBalanceFailure(result));

        return new MNotifySmsBalance(result.Balance, result.Bonus);
    }

    private static Uri AppendApiKey(Uri endpoint, string apiKey)
    {
        var builder = new UriBuilder(endpoint);
        var query = builder.Query.TrimStart('?');
        var keyPair = $"key={WebUtility.UrlEncode(apiKey.Trim())}";
        builder.Query = string.IsNullOrWhiteSpace(query) ? keyPair : $"{query}&{keyPair}";
        return builder.Uri;
    }

    private static MNotifyQuickSmsResponse? DeserializeResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            return JsonSerializer.Deserialize<MNotifyQuickSmsResponse>(responseBody, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MNotifyBalanceResponse? DeserializeBalanceResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            return JsonSerializer.Deserialize<MNotifyBalanceResponse>(responseBody, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FormatProviderFailure(MNotifyQuickSmsResponse? result)
    {
        if (result is null)
            return " The provider returned an unreadable response.";

        var code = string.IsNullOrWhiteSpace(result.Code) ? "unknown" : result.Code;
        var message = string.IsNullOrWhiteSpace(result.Message) ? "No provider message was supplied." : result.Message;
        return $" Provider code: {code}. {message}";
    }

    private static string FormatBalanceFailure(MNotifyBalanceResponse? result)
    {
        if (result is null)
            return " The provider returned an unreadable response.";
        return string.IsNullOrWhiteSpace(result.Message) ? string.Empty : $" {result.Message}";
    }

    private sealed class MNotifyQuickSmsRequest
    {
        [JsonPropertyName("recipient")]
        public required string[] Recipient { get; init; }

        [JsonPropertyName("sender")]
        public required string Sender { get; init; }

        [JsonPropertyName("message")]
        public required string Message { get; init; }

        [JsonPropertyName("is_schedule")]
        public bool IsSchedule { get; init; }

        [JsonPropertyName("schedule_date")]
        public string ScheduleDate { get; init; } = string.Empty;

        [JsonPropertyName("sms_type")]
        public string? SmsType { get; init; }
    }

    private sealed class MNotifyQuickSmsResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }

    private sealed class MNotifyBalanceResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("balance")]
        public decimal Balance { get; init; }

        [JsonPropertyName("bonus")]
        public decimal Bonus { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
