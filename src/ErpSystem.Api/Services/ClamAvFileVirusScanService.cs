using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services;

public sealed class ClamAvVirusScanOptions
{
    public const string SectionName = "FileVirusScan:ClamAv";

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 3310;
    public int ConnectTimeoutSeconds { get; set; } = 5;
    public int ScanTimeoutSeconds { get; set; } = 60;
    public int ChunkSizeBytes { get; set; } = 64 * 1024;
    public int MaximumResponseBytes { get; set; } = 8 * 1024;
}

/// <summary>
/// Streams untrusted content to clamd over its INSTREAM protocol. The clamd
/// socket must stay on a trusted private network because the protocol has no
/// transport authentication.
/// </summary>
public sealed class ClamAvFileVirusScanService : IFileVirusScanService
{
    private static readonly byte[] InStreamCommand =
        Encoding.ASCII.GetBytes("zINSTREAM\0");
    private static readonly byte[] PingCommand =
        Encoding.ASCII.GetBytes("zPING\0");

    private readonly ClamAvVirusScanOptions _options;
    private readonly ILogger<ClamAvFileVirusScanService> _logger;

    public ClamAvFileVirusScanService(
        IOptions<ClamAvVirusScanOptions> options,
        ILogger<ClamAvFileVirusScanService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FileVirusScanResult> ScanAsync(
        FileVirusScanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Content.CanRead)
        {
            return Error("The malware scanner could not read the upload stream.");
        }

        try
        {
            var response = await ExecuteAsync(
                async (stream, token) =>
                {
                    await stream.WriteAsync(InStreamCommand, token);
                    var buffer = new byte[_options.ChunkSizeBytes];
                    var lengthBuffer = new byte[sizeof(uint)];
                    while (true)
                    {
                        var read = await request.Content.ReadAsync(buffer, token);
                        if (read == 0)
                            break;

                        BinaryPrimitives.WriteUInt32BigEndian(
                            lengthBuffer,
                            checked((uint)read));
                        await stream.WriteAsync(lengthBuffer, token);
                        await stream.WriteAsync(buffer.AsMemory(0, read), token);
                    }

                    lengthBuffer.AsSpan().Clear();
                    await stream.WriteAsync(lengthBuffer, token);
                    await stream.FlushAsync(token);
                },
                _options.ScanTimeoutSeconds,
                cancellationToken);

            if (response.EndsWith("OK", StringComparison.OrdinalIgnoreCase))
            {
                return new FileVirusScanResult
                {
                    Status = FileVirusScanStatus.Clean,
                    Message = "ClamAV scan completed."
                };
            }

            if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return new FileVirusScanResult
                {
                    Status = FileVirusScanStatus.Infected,
                    Message = "Malware detected by ClamAV."
                };
            }

            _logger.LogWarning(
                "ClamAV returned a non-final scan response for tenant {TenantId}, category {Category}.",
                request.TenantId,
                request.Category);
            return Error("ClamAV did not return a final clean scan result.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "ClamAV scan timed out for tenant {TenantId}, category {Category}.",
                request.TenantId,
                request.Category);
            return Error("ClamAV scanning timed out.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "ClamAV scanning was unavailable for tenant {TenantId}, category {Category}.",
                request.TenantId,
                request.Category);
            return Error("ClamAV scanning was unavailable.");
        }
    }

    public async Task<bool> IsHealthyAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await ExecuteAsync(
                async (stream, token) =>
                {
                    await stream.WriteAsync(PingCommand, token);
                    await stream.FlushAsync(token);
                },
                _options.ConnectTimeoutSeconds,
                cancellationToken);
            return string.Equals(response, "PONG", StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> ExecuteAsync(
        Func<NetworkStream, CancellationToken, Task> writeRequest,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var client = new TcpClient();
        await client.ConnectAsync(_options.Host, _options.Port, timeout.Token);
        await using var stream = client.GetStream();
        await writeRequest(stream, timeout.Token);
        return await ReadResponseAsync(stream, timeout.Token);
    }

    private async Task<string> ReadResponseAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        using var response = new MemoryStream();
        var buffer = new byte[1024];
        while (response.Length < _options.MaximumResponseBytes)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            var terminator = Array.IndexOf(buffer, (byte)0, 0, read);
            var count = terminator >= 0 ? terminator : read;
            await response.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            if (terminator >= 0)
                break;
        }

        if (response.Length == 0 ||
            response.Length >= _options.MaximumResponseBytes)
        {
            throw new InvalidOperationException(
                "ClamAV returned an empty or oversized response.");
        }

        return Encoding.UTF8.GetString(response.ToArray()).Trim();
    }

    private static FileVirusScanResult Error(string message) => new()
    {
        Status = FileVirusScanStatus.Error,
        Message = message
    };
}

public sealed class FileVirusScanHealthCheck(
    IFileVirusScanService virusScanService) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (virusScanService is ClamAvFileVirusScanService clamAv)
        {
            return await clamAv.IsHealthyAsync(cancellationToken)
                ? HealthCheckResult.Healthy("ClamAV is accepting scan requests.")
                : HealthCheckResult.Unhealthy(
                    "ClamAV is unavailable; clean-scan-required uploads are blocked.");
        }

        if (virusScanService is NoOpFileVirusScanService)
        {
            return HealthCheckResult.Unhealthy(
                "The no-op malware scanner cannot serve clean-scan-required uploads.");
        }

        return HealthCheckResult.Healthy(
            "A host-provided malware scanner is registered.");
    }
}
