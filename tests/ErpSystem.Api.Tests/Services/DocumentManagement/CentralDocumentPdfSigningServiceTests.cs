using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ErpSystem.Api.Services.DocumentManagement;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Signatures;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.DocumentManagement;

public sealed class CentralDocumentPdfSigningServiceTests
{
    [Fact]
    public async Task SignAsync_EmbedsWholeDocumentSignatureAndExecutionPage()
    {
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest(
            "CN=RHEMA ERP Signing Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using var createdCertificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        using var certificate = new X509Certificate2(
            createdCertificate.Export(X509ContentType.Pfx),
            string.Empty,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(certificate);

        try
        {
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(item => item.EnvironmentName).Returns(Environments.Production);
            var service = new CentralDocumentPdfSigningService(
                Options.Create(new DocumentSigningOptions
                {
                    CertificateThumbprint = certificate.Thumbprint,
                    StoreName = StoreName.My,
                    StoreLocation = StoreLocation.CurrentUser,
                    OrganizationName = "RHEMA ERP Test"
                }),
                environment.Object,
                NullLogger<CentralDocumentPdfSigningService>.Instance);

            await using var sourcePdf = CreatePdf();
            var result = await service.SignAsync(
                sourcePdf,
                new CentralDocumentPdfSigningRequest(
                    "DMS-TEST-001",
                    "Executive Approver",
                    "Authorised Signatory",
                    "Approved in test.",
                    DateTime.UtcNow),
                CancellationToken.None);

            result.SignatureIntegrityValid.Should().BeTrue();
            result.SignatureCoversWholeDocument.Should().BeTrue();
            result.CertificateThumbprint.Should().Be(certificate.Thumbprint);
            result.DocumentSha256.Should().HaveLength(64);

            using var signedStream = new MemoryStream(result.PdfBytes, writable: false);
            using var pdf = new PdfDocument(new PdfReader(signedStream));
            pdf.GetNumberOfPages().Should().Be(2);
            var signatures = new SignatureUtil(pdf);
            signatures.GetSignatureNames().Should().ContainSingle(result.SignatureFieldName);
            signatures.ReadSignatureData(result.SignatureFieldName)
                .VerifySignatureIntegrityAndAuthenticity()
                .Should().BeTrue();
        }
        finally
        {
            store.Remove(certificate);
        }
    }

    private static MemoryStream CreatePdf()
    {
        var stream = new MemoryStream();
        var writer = new PdfWriter(stream);
        writer.SetCloseStream(false);
        using (var pdf = new PdfDocument(writer))
        {
            pdf.AddNewPage();
        }

        stream.Position = 0;
        return stream;
    }
}
