using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Workflow;

public sealed record WorkflowCertificateInspection(string Thumbprint, string Subject,
    DateTime NotBefore, DateTime NotAfter, bool ChainValid);

public static class WorkflowSignatureValidator
{
    public static IReadOnlyList<string> Validate(WorkflowSignaturePolicyDto? policy,
        IEnumerable<string> actorRoles, object? resultData, DateTime nowUtc)
    {
        if (policy?.IsRequired != true) return [];
        var errors = new List<string>();
        var submission = ReadSubmission(resultData);
        if (submission == null) return ["An electronic signature is required for this approval."];
        if (submission.Method != policy.Method) errors.Add("The submitted signature method does not match the approval policy.");
        if (!string.IsNullOrWhiteSpace(policy.RequiredSigningRole) &&
            !actorRoles.Contains(policy.RequiredSigningRole, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Signing authority requires role '{policy.RequiredSigningRole}'.");
        if (string.IsNullOrWhiteSpace(submission.Attestation) ||
            !string.Equals(submission.Attestation.Trim(), policy.AttestationText.Trim(), StringComparison.Ordinal))
            errors.Add("The required signing attestation was not accepted.");
        if (submission.SignedAt > nowUtc.AddMinutes(5) || submission.SignedAt < nowUtc.AddHours(-24))
            errors.Add("The signature timestamp is outside the accepted signing window.");

        if (policy.Method == WorkflowSignatureMethod.DigitalCertificate)
        {
            var certificate = InspectCertificate(submission.CertificateBase64, nowUtc);
            if (certificate == null) errors.Add("A readable signing certificate is required.");
            else
            {
                if (nowUtc < certificate.NotBefore.ToUniversalTime() || nowUtc > certificate.NotAfter.ToUniversalTime())
                    errors.Add("The signing certificate is not currently valid.");
                if (policy.RequireValidCertificateChain && !certificate.ChainValid)
                    errors.Add("The signing certificate chain could not be validated.");
            }
        }
        if (policy.Method == WorkflowSignatureMethod.ExternalProvider && string.IsNullOrWhiteSpace(submission.ExternalReference))
            errors.Add("An external signature reference is required.");
        return errors;
    }

    public static WorkflowSignatureSubmissionDto? ReadSubmission(object? resultData)
    {
        if (resultData == null) return null;
        try
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(resultData));
            if (!TryGet(document.RootElement, "signature", out var signature)) return null;
            return JsonSerializer.Deserialize<WorkflowSignatureSubmissionDto>(signature.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException) { return null; }
    }

    public static WorkflowCertificateInspection? InspectCertificate(string? certificateBase64, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(certificateBase64)) return null;
        try
        {
            using var certificate = new X509Certificate2(Convert.FromBase64String(certificateBase64));
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationTime = nowUtc;
            return new WorkflowCertificateInspection(certificate.Thumbprint, certificate.Subject,
                certificate.NotBefore, certificate.NotAfter, chain.Build(certificate));
        }
        catch (CryptographicException) { return null; }
        catch (FormatException) { return null; }
    }

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = property.Value; return true; }
        value = default; return false;
    }
}
