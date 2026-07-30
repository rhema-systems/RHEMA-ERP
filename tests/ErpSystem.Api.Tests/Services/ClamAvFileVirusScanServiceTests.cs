using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ErpSystem.Api.Extensions;
using ErpSystem.Api.Services;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class ClamAvFileVirusScanServiceTests
{
    [Fact]
    public async Task ScanAsyncStreamsContentAndReturnsClean()
    {
        var content = Encoding.UTF8.GetBytes("controlled supplier evidence");
        using var listener = StartListener(out var port);
        var daemon = ReceiveStreamAsync(listener, "stream: OK\0");
        var service = CreateService(port);

        var result = await service.ScanAsync(Request(content));

        result.Status.Should().Be(FileVirusScanStatus.Clean);
        (await daemon).Should().Equal(content);
    }

    [Fact]
    public async Task ScanAsyncReturnsInfectedForFoundResponse()
    {
        var content = Encoding.UTF8.GetBytes("EICAR test fixture");
        using var listener = StartListener(out var port);
        var daemon = ReceiveStreamAsync(
            listener,
            "stream: Win.Test.EICAR_HDB-1 FOUND\0");
        var service = CreateService(port);

        var result = await service.ScanAsync(Request(content));

        result.Status.Should().Be(FileVirusScanStatus.Infected);
        result.Message.Should().NotContain("EICAR");
        (await daemon).Should().Equal(content);
    }

    [Fact]
    public async Task HealthProbeRequiresClamdPong()
    {
        using var listener = StartListener(out var port);
        var daemon = ReceiveCommandAsync(listener, "zPING\0", "PONG\0");
        var service = CreateService(port);

        (await service.IsHealthyAsync()).Should().BeTrue();
        await daemon;
    }

    [Fact]
    public void FileUploadCompositionRegistersClamAvInsteadOfNoOp()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileVirusScan:ClamAv:Host"] = "127.0.0.1",
                ["FileVirusScan:ClamAv:Port"] = "3310"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddErpSystemFileUpload(configuration);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IFileVirusScanService>()
            .Should().BeOfType<ClamAvFileVirusScanService>();
    }

    private static ClamAvFileVirusScanService CreateService(int port) =>
        new(
            Options.Create(new ClamAvVirusScanOptions
            {
                Host = "127.0.0.1",
                Port = port,
                ConnectTimeoutSeconds = 2,
                ScanTimeoutSeconds = 5
            }),
            NullLogger<ClamAvFileVirusScanService>.Instance);

    private static FileVirusScanRequest Request(byte[] content) => new()
    {
        TenantId = Guid.NewGuid(),
        Category = ControlledFileUploadCategories.SupplierRegistrationEvidence,
        FileName = "supplier-evidence.pdf",
        ContentType = "application/pdf",
        FileSize = content.Length,
        Content = new MemoryStream(content)
    };

    private static TcpListener StartListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static async Task<byte[]> ReceiveStreamAsync(
        TcpListener listener,
        string response)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        var command = new byte[10];
        await stream.ReadExactlyAsync(command);
        Encoding.ASCII.GetString(command).Should().Be("zINSTREAM\0");

        using var content = new MemoryStream();
        var lengthBuffer = new byte[sizeof(uint)];
        while (true)
        {
            await stream.ReadExactlyAsync(lengthBuffer);
            var length = BinaryPrimitives.ReadUInt32BigEndian(lengthBuffer);
            if (length == 0)
                break;

            var chunk = new byte[checked((int)length)];
            await stream.ReadExactlyAsync(chunk);
            await content.WriteAsync(chunk);
        }

        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        await stream.FlushAsync();
        return content.ToArray();
    }

    private static async Task ReceiveCommandAsync(
        TcpListener listener,
        string expected,
        string response)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        var command = new byte[Encoding.ASCII.GetByteCount(expected)];
        await stream.ReadExactlyAsync(command);
        Encoding.ASCII.GetString(command).Should().Be(expected);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        await stream.FlushAsync();
    }
}
