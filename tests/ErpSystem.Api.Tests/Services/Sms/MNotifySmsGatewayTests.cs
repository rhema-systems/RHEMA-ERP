using System.Net;
using System.Text.Json;
using ErpSystem.Api.Services.Sms;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Sms;

public sealed class MNotifySmsGatewayTests
{
    [Fact]
    public async Task SendAsync_PostsQuickSmsContractWithoutOtpTypeForOrdinarySms()
    {
        var handler = new RecordingHandler(SuccessResponse());
        using var client = new HttpClient(handler);

        await MNotifySmsGateway.SendAsync(
            client,
            Options(),
            "+233241234567",
            "A normal notification",
            isOtp: false,
            CancellationToken.None);

        handler.Method.Should().Be(HttpMethod.Post);
        handler.RequestUri.Should().Be("https://api.mnotify.com/api/sms/quick?key=test%2Bkey");
        handler.ContentType.Should().StartWith("application/json");
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("recipient")[0].GetString().Should().Be("0241234567");
        body.RootElement.GetProperty("sender").GetString().Should().Be("RHEMA");
        body.RootElement.GetProperty("message").GetString().Should().Be("A normal notification");
        body.RootElement.GetProperty("is_schedule").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("schedule_date").GetString().Should().BeEmpty();
        body.RootElement.TryGetProperty("sms_type", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_IncludesOtpTypeOnlyForOtpSms()
    {
        var handler = new RecordingHandler(SuccessResponse());
        using var client = new HttpClient(handler);

        await MNotifySmsGateway.SendAsync(
            client,
            Options(),
            "0241234567",
            "Your code is 123456",
            isOtp: true,
            CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("sms_type").GetString().Should().Be("otp");
    }

    [Fact]
    public async Task SendAsync_RejectsProviderFailureEvenWhenHttpStatusIsSuccessful()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":"error","code":"4001","message":"invalid sender"}""")
        });
        using var client = new HttpClient(handler);

        var action = () => MNotifySmsGateway.SendAsync(
            client,
            Options(),
            "0241234567",
            "Message",
            isOtp: false,
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*4001*invalid sender*");
    }

    [Fact]
    public async Task GetBalanceAsync_UsesOfficialEndpointAndReturnsBalanceAndBonus()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":"success","balance":1483.5,"bonus":25}""")
        });
        using var client = new HttpClient(handler);

        var result = await MNotifySmsGateway.GetBalanceAsync(client, "test+key", CancellationToken.None);

        handler.Method.Should().Be(HttpMethod.Get);
        handler.RequestUri.Should().Be("https://api.mnotify.com/api/balance/sms?key=test%2Bkey");
        result.Balance.Should().Be(1483.5m);
        result.Bonus.Should().Be(25m);
    }

    [Fact]
    public async Task GetBalanceAsync_RejectsProviderFailureEvenWhenHttpStatusIsSuccessful()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":"error","message":"invalid api key"}""")
        });
        using var client = new HttpClient(handler);

        var action = () => MNotifySmsGateway.GetBalanceAsync(client, "bad-key", CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not accepted*invalid api key*");
    }

    [Theory]
    [InlineData("+233 24 123 4567", "0241234567")]
    [InlineData("233241234567", "0241234567")]
    [InlineData("0241234567", "0241234567")]
    [InlineData("+447700900123", "447700900123")]
    public void NormalizeRecipient_ProducesMNotifyRecipient(string input, string expected)
    {
        MNotifySmsGateway.NormalizeRecipient(input).Should().Be(expected);
    }

    private static GhanaGatewaySmsOptions Options() => new()
    {
        Enabled = true,
        UrlTemplate = MNotifySmsGateway.DefaultEndpoint,
        ApiKey = "test+key",
        SenderId = "RHEMA",
        TimeoutSeconds = 10
    };

    private static HttpResponseMessage SuccessResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("""{"status":"success","code":"2000","message":"messages sent successfully","summary":{"total_sent":1}}""")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public RecordingHandler(HttpResponseMessage response) => _response = response;

        public HttpMethod? Method { get; private set; }
        public string? RequestUri { get; private set; }
        public string? ContentType { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri?.AbsoluteUri;
            ContentType = request.Content?.Headers.ContentType?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _response;
        }
    }
}
