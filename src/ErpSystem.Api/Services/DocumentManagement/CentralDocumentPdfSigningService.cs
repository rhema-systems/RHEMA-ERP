using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Bouncycastleconnector;
using iText.Commons.Bouncycastle.Cert;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Signatures;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services.DocumentManagement;

public sealed class DocumentSigningOptions
{
    public const string SectionName = "DocumentSigning";

    public string? CertificateThumbprint { get; set; }
    public StoreName StoreName { get; set; } = StoreName.My;
    public StoreLocation StoreLocation { get; set; } = StoreLocation.CurrentUser;
    public bool CreateDevelopmentCertificate { get; set; }
    public string OrganizationName { get; set; } = "RHEMA ERP";
    public string Location { get; set; } = "ERP Document Management";
}

public sealed record CentralDocumentPdfSigningRequest(
    string DocumentReference,
    string SignerName,
    string SignerRole,
    string? Notes,
    DateTime SignedAtUtc);

public sealed record CentralDocumentPdfSigningResult(
    byte[] PdfBytes,
    string SignatureFieldName,
    string CertificateThumbprint,
    string CertificateSubject,
    string CertificateSerialNumber,
    DateTime CertificateValidFromUtc,
    DateTime CertificateValidToUtc,
    string DigestAlgorithm,
    string DocumentSha256,
    bool SignatureIntegrityValid,
    bool SignatureCoversWholeDocument);

public interface ICentralDocumentPdfSigningService
{
    Task<CentralDocumentPdfSigningResult> SignAsync(
        Stream sourcePdf,
        CentralDocumentPdfSigningRequest request,
        CancellationToken cancellationToken);
}

public sealed class CentralDocumentPdfSigningService : ICentralDocumentPdfSigningService
{
    private const string DigestAlgorithm = "SHA-256";
    private readonly DocumentSigningOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CentralDocumentPdfSigningService> _logger;

    public CentralDocumentPdfSigningService(
        IOptions<DocumentSigningOptions> options,
        IHostEnvironment environment,
        ILogger<CentralDocumentPdfSigningService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<CentralDocumentPdfSigningResult> SignAsync(
        Stream sourcePdf,
        CentralDocumentPdfSigningRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourcePdf);
        cancellationToken.ThrowIfCancellationRequested();

        using var certificate = ResolveCertificate();
        ValidateCertificate(certificate, request.SignedAtUtc);

        await using var source = new MemoryStream();
        await sourcePdf.CopyToAsync(source, cancellationToken);
        source.Position = 0;

        var preparedPdf = AddExecutionPage(source.ToArray(), request);
        var signatureFieldName = $"RhemaOrganizationalSignature_{Guid.NewGuid():N}";
        var signedPdf = SignPreparedPdf(preparedPdf, certificate, request, signatureFieldName);
        var verification = VerifySignature(signedPdf, signatureFieldName);
        if (!verification.IntegrityValid || !verification.CoversWholeDocument)
        {
            throw new InvalidOperationException("The generated PDF signature did not pass its integrity verification.");
        }

        return new CentralDocumentPdfSigningResult(
            signedPdf,
            signatureFieldName,
            certificate.Thumbprint,
            certificate.Subject,
            certificate.SerialNumber,
            certificate.NotBefore.ToUniversalTime(),
            certificate.NotAfter.ToUniversalTime(),
            DigestAlgorithm,
            Convert.ToHexString(SHA256.HashData(signedPdf)),
            verification.IntegrityValid,
            verification.CoversWholeDocument);
    }

    private byte[] AddExecutionPage(byte[] sourcePdf, CentralDocumentPdfSigningRequest request)
    {
        using var input = new MemoryStream(sourcePdf, writable: false);
        using var output = new MemoryStream();
        using (var pdf = new PdfDocument(new PdfReader(input), new PdfWriter(output)))
        {
            var page = pdf.AddNewPage(PageSize.A4);
            var font = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
            var bold = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
            var pageSize = page.GetPageSize();
            using var canvas = new Canvas(page, new Rectangle(
                64,
                64,
                pageSize.GetWidth() - 128,
                pageSize.GetHeight() - 128));
            canvas.Add(new Paragraph("ORGANIZATIONAL DIGITAL SIGNATURE")
                .SetFont(bold)
                .SetFontSize(15)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(24));
            canvas.Add(new Paragraph("This execution page is part of the agreement and is protected by the embedded PDF signature.")
                .SetFont(font)
                .SetFontSize(10)
                .SetMarginBottom(20));
            AddSigningLine(canvas, bold, font, "Document reference", request.DocumentReference);
            AddSigningLine(canvas, bold, font, "Signed by", request.SignerName);
            AddSigningLine(canvas, bold, font, "Authority / role", request.SignerRole);
            AddSigningLine(canvas, bold, font, "Signed at (UTC)", request.SignedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
            AddSigningLine(canvas, bold, font, "Organization", _options.OrganizationName);
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                AddSigningLine(canvas, bold, font, "Signing notes", request.Notes.Trim());
            }

            canvas.Add(new Paragraph("The signature panel below is generated from the organization's protected X.509 certificate. Any later change to the signed PDF invalidates its integrity check.")
                .SetFont(font)
                .SetFontSize(9)
                .SetMarginTop(24));
        }

        return output.ToArray();
    }

    private static void AddSigningLine(Canvas canvas, PdfFont bold, PdfFont regular, string label, string value)
    {
        canvas.Add(new Paragraph()
            .SetFontSize(10)
            .SetMarginBottom(7)
            .Add(new Text($"{label}: ").SetFont(bold))
            .Add(new Text(value).SetFont(regular)));
    }

    private byte[] SignPreparedPdf(
        byte[] preparedPdf,
        X509Certificate2 certificate,
        CentralDocumentPdfSigningRequest request,
        string signatureFieldName)
    {
        using var input = new MemoryStream(preparedPdf, writable: false);
        using var output = new MemoryStream();
        var signer = new PdfSigner(new PdfReader(input), output, new StampingProperties().UseAppendMode());
        var pageNumber = signer.GetDocument().GetNumberOfPages();
        var appearanceText = $"Digitally signed by {_options.OrganizationName}\n" +
            $"Authorized signatory: {request.SignerName}\n" +
            $"Role: {request.SignerRole}\n" +
            $"Date: {request.SignedAtUtc:yyyy-MM-dd HH:mm:ss} UTC\n" +
            $"Reference: {request.DocumentReference}";

        signer.SetFieldName(signatureFieldName);
        signer.SetSignDate(request.SignedAtUtc);
        signer.SetCertificationLevel(PdfSigner.CERTIFIED_NO_CHANGES_ALLOWED);
#pragma warning disable CS0612, CS0618 // iText 8 retains this API for custom visible signature text.
        signer.GetSignatureAppearance()
            .SetReason($"Final approval of {request.DocumentReference}")
            .SetLocation(_options.Location)
            .SetPageNumber(pageNumber)
            .SetPageRect(new Rectangle(72, 82, 451, 112))
            .SetLayer2Text(appearanceText);
#pragma warning restore CS0612, CS0618

        var factory = BouncyCastleFactoryCreator.GetFactory();
        using var certificateStream = new MemoryStream(certificate.RawData, writable: false);
        IX509Certificate signingCertificate = factory.CreateX509Certificate(certificateStream);
        var externalSignature = new WindowsCertificateExternalSignature(certificate);
        signer.SignDetached(
            externalSignature,
            new[] { signingCertificate },
            null,
            null,
            null,
            0,
            PdfSigner.CryptoStandard.CADES);

        return output.ToArray();
    }

    private static (bool IntegrityValid, bool CoversWholeDocument) VerifySignature(byte[] signedPdf, string signatureFieldName)
    {
        using var input = new MemoryStream(signedPdf, writable: false);
        using var pdf = new PdfDocument(new PdfReader(input));
        var signatures = new SignatureUtil(pdf);
        var pkcs7 = signatures.ReadSignatureData(signatureFieldName);
        return (
            pkcs7.VerifySignatureIntegrityAndAuthenticity(),
            signatures.SignatureCoversWholeDocument(signatureFieldName));
    }

    private X509Certificate2 ResolveCertificate()
    {
        var thumbprint = NormalizeThumbprint(_options.CertificateThumbprint);
        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            using var store = new X509Store(_options.StoreName, _options.StoreLocation);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            var match = store.Certificates
                .Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
                .OfType<X509Certificate2>()
                .FirstOrDefault();
            return match is null
                ? throw new InvalidOperationException($"The configured document-signing certificate '{thumbprint}' was not found in {_options.StoreLocation}/{_options.StoreName}.")
                : new X509Certificate2(match);
        }

        if (_environment.IsDevelopment() && _options.CreateDevelopmentCertificate)
        {
            return ResolveOrCreateDevelopmentCertificate();
        }

        throw new InvalidOperationException("Document signing is not configured. Set DocumentSigning:CertificateThumbprint to a certificate with a private key.");
    }

    private X509Certificate2 ResolveOrCreateDevelopmentCertificate()
    {
        const string subject = "CN=RHEMA ERP Development Document Signing";
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        var existing = store.Certificates
            .Find(X509FindType.FindBySubjectDistinguishedName, subject, validOnly: false)
            .OfType<X509Certificate2>()
            .Where(item => item.HasPrivateKey && item.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(30))
            .OrderByDescending(item => item.NotAfter)
            .FirstOrDefault();
        if (existing is not null)
        {
            return new X509Certificate2(existing);
        }

        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));
        var persisted = new X509Certificate2(
            created.Export(X509ContentType.Pfx),
            string.Empty,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
        store.Add(persisted);
        _logger.LogWarning(
            "Created development-only document signing certificate {Thumbprint} in CurrentUser/My. Configure an organizational certificate before production use.",
            persisted.Thumbprint);
        return persisted;
    }

    private static void ValidateCertificate(X509Certificate2 certificate, DateTime signingTimeUtc)
    {
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("The document-signing certificate does not have an accessible private key.");
        }

        var utc = signingTimeUtc.ToUniversalTime();
        if (utc < certificate.NotBefore.ToUniversalTime() || utc > certificate.NotAfter.ToUniversalTime())
        {
            throw new InvalidOperationException("The document-signing certificate is not valid at the current time.");
        }

        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa is null)
        {
            throw new InvalidOperationException("The configured document-signing certificate must have an RSA private key.");
        }
    }

    private static string? NormalizeThumbprint(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private sealed class WindowsCertificateExternalSignature : IExternalSignature
    {
        private readonly X509Certificate2 _certificate;

        public WindowsCertificateExternalSignature(X509Certificate2 certificate)
        {
            _certificate = certificate;
        }

        public string GetDigestAlgorithmName() => DigestAlgorithm;

        public string GetSignatureAlgorithmName() => "RSA";

        public ISignatureMechanismParams? GetSignatureMechanismParameters() => null;

        public byte[] Sign(byte[] message)
        {
            using var rsa = _certificate.GetRSAPrivateKey()
                ?? throw new InvalidOperationException("The document-signing private key is unavailable.");
            return rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }
}
