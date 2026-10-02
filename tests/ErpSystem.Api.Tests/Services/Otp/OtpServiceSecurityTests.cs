using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Otp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Otp;

public sealed class OtpServiceSecurityTests
{
    [Fact]
    public async Task SuccessfulVerificationConsumesCodeAndPreventsReplay()
    {
        var service = Service();
        var tenantId = Guid.NewGuid();
        var code = await service.CreateOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Email,
            "prospect@example.test",
            TimeSpan.FromMinutes(5),
            maxAttempts: 3,
            CancellationToken.None);

        var first = await service.VerifyOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Email,
            "PROSPECT@example.test ",
            code,
            consumeOnSuccess: true,
            CancellationToken.None);
        var replay = await service.VerifyOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Email,
            "prospect@example.test",
            code,
            consumeOnSuccess: true,
            CancellationToken.None);

        Assert.True(first.Success);
        Assert.False(replay.Success);
        Assert.Equal("Code expired or not found", replay.FailureReason);
    }

    [Fact]
    public async Task CodeIsBoundToTenantChannelAndExactNormalizedIdentity()
    {
        var service = Service();
        var tenantId = Guid.NewGuid();
        var code = await service.CreateOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Sms,
            "+233 (20) 123-4567",
            TimeSpan.FromMinutes(5),
            maxAttempts: 3,
            CancellationToken.None);

        var wrongTenant = await service.VerifyOtpAsync(
            Guid.NewGuid(),
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Sms,
            "+233201234567",
            code,
            consumeOnSuccess: true,
            CancellationToken.None);
        var wrongChannel = await service.VerifyOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Email,
            "+233201234567",
            code,
            consumeOnSuccess: true,
            CancellationToken.None);
        var wrongIdentity = await service.VerifyOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Sms,
            "+233201234568",
            code,
            consumeOnSuccess: true,
            CancellationToken.None);
        var normalizedIdentity = await service.VerifyOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Sms,
            "+233 20 123 4567",
            code,
            consumeOnSuccess: true,
            CancellationToken.None);

        Assert.False(wrongTenant.Success);
        Assert.False(wrongChannel.Success);
        Assert.False(wrongIdentity.Success);
        Assert.True(normalizedIdentity.Success);
    }

    [Fact]
    public async Task ExpiredCodeIsRejectedAndRemoved()
    {
        var cache = new MemoryRedisService();
        var service = Service(cache);
        var tenantId = Guid.NewGuid();
        var code = await service.CreateOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Email,
            "expired@example.test",
            TimeSpan.Zero,
            maxAttempts: 3,
            CancellationToken.None);

        var expired = await service.VerifyOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Email,
            "expired@example.test",
            code,
            consumeOnSuccess: true,
            CancellationToken.None);

        Assert.False(expired.Success);
        Assert.Equal("Code expired", expired.FailureReason);
        Assert.Empty(cache.Values);
    }

    [Fact]
    public async Task InvalidCodeDoesNotConsumeCorrectCodeBeforeAttemptLimit()
    {
        var service = Service();
        var tenantId = Guid.NewGuid();
        var code = await service.CreateOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Email,
            "retry@example.test",
            TimeSpan.FromMinutes(5),
            maxAttempts: 2,
            CancellationToken.None);

        var invalid = await service.VerifyOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Email,
            "retry@example.test",
            code == "000000" ? "111111" : "000000",
            consumeOnSuccess: true,
            CancellationToken.None);
        var valid = await service.VerifyOtpAsync(
            tenantId,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Email,
            "retry@example.test",
            code,
            consumeOnSuccess: true,
            CancellationToken.None);

        Assert.False(invalid.Success);
        Assert.Equal("Invalid code", invalid.FailureReason);
        Assert.True(valid.Success);
    }

    private static OtpService Service(MemoryRedisService? cache = null) =>
        new(cache ?? new MemoryRedisService(), NullLogger<OtpService>.Instance);

    private sealed class MemoryRedisService : IRedisService
    {
        public Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Values.TryGetValue(key, out var value) ? (T?)value : default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Values[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Values.Remove(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Values.ContainsKey(key));

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Dictionary<string, string>> GetInfoAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, string>());
    }
}
