using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The company seal and signature: which image is current, which were used before, and when.
/// </summary>
/// <remarks>
/// <para><b>Why this is not two columns on the company profile.</b> A seal is an instrument of
/// authority — whoever holds it can make a document look authentic. Overwriting one destroys the
/// answer to the question that matters after a compromise: <i>which documents carry the seal that
/// leaked?</i> Every image is kept with who uploaded it and the window it was current for.</para>
///
/// <para><b>Close-then-insert, and currency is derived.</b> Replacing a seal retires the open row
/// and inserts a new one; "current" means <c>RetiredOn is null</c>. Same idiom as
/// <c>EmployeeSalaryAssignment</c>, and deliberately no <c>IsCurrent</c> flag — the module already
/// pays for that shape on <c>EmployeeContractDetail</c>, where the flag is set true at creation and
/// updated by nothing.</para>
/// </remarks>
public interface ICompanySealAssetService
{
    /// <summary>The image in use for a kind, or <c>null</c> when none has ever been uploaded.</summary>
    Task<CompanySealAssetDto?> GetCurrentAsync(CompanySealAssetKind kind, CancellationToken ct = default);

    /// <summary>Every image of every kind, newest first — the audit trail.</summary>
    Task<IEnumerable<CompanySealAssetDto>> GetHistoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Makes an uploaded image the current one for its kind, retiring whatever it replaces.
    /// </summary>
    Task<CompanySealAssetDto> ReplaceAsync(
        CompanySealAssetKind kind, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Withdraws the current image of a kind without replacing it.
    /// </summary>
    /// <remarks>
    /// ⚠ A real operation, not a delete: a compromised seal must be able to stop being used
    /// immediately, before a replacement has been produced. Letters then fall back to the legacy
    /// URL if the tenant still has one, and otherwise render without a seal — which is the correct
    /// outcome, because the alternative is stamping documents with an image known to be bad.
    /// </remarks>
    Task<bool> RetireCurrentAsync(CompanySealAssetKind kind, string? reason, CancellationToken ct = default);

    /// <summary>The current image's bytes as a data URI, for embedding in a rendered letter.</summary>
    /// <remarks>
    /// ⚠ There is deliberately NO public URL for a seal. A letter is returned as rendered HTML, so
    /// the image has to be embedded rather than linked — which also means the seal is not reachable
    /// by anyone who merely guesses an address.
    /// </remarks>
    Task<string?> GetCurrentAsDataUriAsync(CompanySealAssetKind kind, CancellationToken ct = default);

    /// <summary>
    /// <see cref="GetCurrentAsDataUriAsync(CompanySealAssetKind, CancellationToken)"/> for a named tenant — for a letter or
    /// email rendered with nobody signed in to say whose it is (company-schedule final closure lane 4c: the logo).
    /// </summary>
    Task<string?> GetCurrentAsDataUriForTenantAsync(Guid tenantId, CompanySealAssetKind kind, CancellationToken ct = default);
}

/// <summary>
/// What a logo, seal or signature image must be (company-schedule final closure lane 4c, the user's rulings): a PNG or a
/// JPEG — by its name, its declared type AND its first bytes, so a renamed file is refused — of at most 2 MB, because
/// every letter carries it embedded. Checked before a byte is stored; the upload gate's own type list is a tenant
/// setting and admits documents.
/// </summary>
public static class CompanySealAssetRules
{
    /// <summary>2 MB: the user's ruling (2026-10-06).</summary>
    public const long MaxBytes = 2 * 1024 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    /// <summary>How many leading bytes <see cref="RefuseImage"/> needs to see.</summary>
    public const int HeaderLength = 8;

    /// <summary>
    /// Why this file cannot be a company image, or null when it can. <paramref name="header"/> is the file's first
    /// <see cref="HeaderLength"/> bytes (fewer when the file is shorter).
    /// </summary>
    public static string? RefuseImage(
        CompanySealAssetKind kind, string? fileName, string? contentType, long length, ReadOnlySpan<byte> header)
    {
        var what = Describe(kind);
        if (length > MaxBytes)
            return $"The {what} must be at most 2 MB; this file is {length / (1024.0 * 1024.0):0.#} MB. Every letter carries it.";

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        var png = extension == ".png";
        var jpeg = extension is ".jpg" or ".jpeg";
        var type = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        var typeOk = png ? type is "image/png" or "" : jpeg && type is "image/jpeg" or "image/jpg" or "image/pjpeg" or "";
        var bytesOk = png ? header.StartsWith(PngSignature) : jpeg && header.StartsWith(JpegSignature);
        return (png || jpeg) && typeOk && bytesOk
            ? null
            : $"The {what} must be a PNG or JPEG image.";
    }

    /// <summary>The kind as a person would say it.</summary>
    public static string Describe(CompanySealAssetKind kind) => kind switch
    {
        CompanySealAssetKind.Seal => "company seal",
        CompanySealAssetKind.Signature => "signature",
        CompanySealAssetKind.Logo => "logo",
        _ => "image",
    };
}

public class CompanySealAssetService : ICompanySealAssetService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<CompanySealAssetService> _logger;

    public CompanySealAssetService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IFileStorageService fileStorage,
        ILogger<CompanySealAssetService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private IQueryable<CompanySealAsset> Owned(Guid tenantId) =>
        _unitOfWork.Repository<CompanySealAsset>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted);

    public async Task<CompanySealAssetDto?> GetCurrentAsync(
        CompanySealAssetKind kind, CancellationToken ct = default)
    {
        var entity = await CurrentEntityAsync(GetTenantId(), kind, ct);
        return entity is null ? null : Map(entity);
    }

    private Task<CompanySealAsset?> CurrentEntityAsync(
        Guid tenantId, CompanySealAssetKind kind, CancellationToken ct) =>
        Owned(tenantId)
            .Where(a => a.Kind == kind && a.RetiredOn == null)
            // ⚠ Ordered rather than assumed unique. The service keeps one open row per kind, but a
            // read that silently depended on that would become a 500 the day anything else did not.
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    public async Task<IEnumerable<CompanySealAssetDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        var rows = await Owned(GetTenantId())
            .OrderByDescending(a => a.EffectiveFrom)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<CompanySealAssetDto> ReplaceAsync(
        CompanySealAssetKind kind, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, string? reason, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;

        // Close before inserting, so there is never a moment with two open rows for one kind. Not a
        // unique index: the delete here is soft, and a unique index that counts soft-deleted rows is
        // the defect this module has met nine times.
        var superseded = await Owned(tenantId)
            .Where(a => a.Kind == kind && a.RetiredOn == null)
            .ToListAsync(ct);

        foreach (var old in superseded)
        {
            old.RetiredOn = now;
            old.RetiredReason = string.IsNullOrWhiteSpace(reason) ? "Replaced." : reason.Trim();
            old.UpdatedBy = DescribeActor();
            await _unitOfWork.Repository<CompanySealAsset>().UpdateAsync(old);
        }

        var entity = new CompanySealAsset
        {
            TenantId = tenantId,
            Kind = kind,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
            FileName = fileName,
            MimeType = mimeType,
            FileSizeBytes = fileSizeBytes,
            EffectiveFrom = now,
            // ⚠ Stamped EXPLICITLY. CreatedBy is not filled in for us — the probe that uploaded the
            // first seal read back `uploadedBy: null`, which would have made the audit trail answer
            // "when" and not "who". The whole reason this is a history table is to answer both.
            CreatedBy = DescribeActor(),
        };

        await _unitOfWork.Repository<CompanySealAsset>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        // ⚠ Logged at Information on purpose. Replacing a seal is a privileged act on an instrument
        // of authority, and "who changed it and when" must be answerable from the log as well as
        // from the table, in case the table is what somebody tampered with.
        _logger.LogInformation(
            "Company {Kind} replaced for tenant {TenantId}: upload {UploadId}, superseding {Count} row(s).",
            kind, tenantId, fileUploadRecordId, superseded.Count);

        return Map(entity);
    }

    public async Task<bool> RetireCurrentAsync(
        CompanySealAssetKind kind, string? reason, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var open = await Owned(tenantId)
            .Where(a => a.Kind == kind && a.RetiredOn == null)
            .ToListAsync(ct);

        if (open.Count == 0) return false;

        foreach (var row in open)
        {
            row.RetiredOn = DateTime.UtcNow;
            row.RetiredReason = string.IsNullOrWhiteSpace(reason) ? "Withdrawn." : reason.Trim();
            row.UpdatedBy = DescribeActor();
            await _unitOfWork.Repository<CompanySealAsset>().UpdateAsync(row);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogWarning(
            "Company {Kind} withdrawn for tenant {TenantId} with no replacement. Reason: {Reason}",
            kind, tenantId, reason ?? "(none given)");
        return true;
    }

    public Task<string?> GetCurrentAsDataUriAsync(
        CompanySealAssetKind kind, CancellationToken ct = default)
        => GetCurrentAsDataUriForTenantAsync(GetTenantId(), kind, ct);

    public async Task<string?> GetCurrentAsDataUriForTenantAsync(
        Guid tenantId, CompanySealAssetKind kind, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return null;
        var current = await CurrentEntityAsync(tenantId, kind, ct);
        if (current is null) return null;

        try
        {
            var upload = await _unitOfWork.Repository<Entities.FileUploadRecord>().GetQueryable()
                .FirstOrDefaultAsync(u => u.Id == current.FileUploadRecordId && !u.IsDeleted, ct);

            // ⚠ Never embed bytes we did not get a clean scan verdict on. The gate scans on the way
            // in, but a verdict can be Pending or Skipped, and an unscanned image embedded in every
            // outgoing offer letter is precisely what the category registration exists to prevent.
            if (upload is not { VirusScanStatus: Enums.FileVirusScanStatus.Clean }) return null;

            await using var stream = await _fileStorage.DownloadFileAsync(upload.FilePath, upload.Id);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);

            var mime = string.IsNullOrWhiteSpace(current.MimeType)
                ? (string.IsNullOrWhiteSpace(upload.ContentType) ? "image/png" : upload.ContentType)
                : current.MimeType;
            return $"data:{mime};base64,{Convert.ToBase64String(buffer.ToArray())}";
        }
        catch (Exception ex)
        {
            // ⚠ A letter must still render. A missing seal image is a blank stamp, which somebody
            // will notice and fix; an exception here would take down offer-letter generation
            // entirely, which is a far worse failure for the same cause.
            _logger.LogError(ex,
                "Could not read the current {Kind} image for embedding; the letter will render without it.", kind);
            return null;
        }
    }

    /// <summary>Who is doing this, for the audit trail. Name where we have one, id otherwise.</summary>
    private string DescribeActor()
    {
        var name = _currentUserProvider.FullName;
        if (string.IsNullOrWhiteSpace(name)) name = _currentUserProvider.Username;
        return string.IsNullOrWhiteSpace(name)
            ? _currentUserProvider.UserId.ToString()
            : name.Trim();
    }

    private static CompanySealAssetDto Map(CompanySealAsset e) => new()
    {
        Id = e.Id,
        Kind = e.Kind,
        FileName = e.FileName,
        MimeType = e.MimeType,
        FileSizeBytes = e.FileSizeBytes,
        EffectiveFrom = e.EffectiveFrom,
        RetiredOn = e.RetiredOn,
        RetiredReason = e.RetiredReason,
        IsCurrent = e.RetiredOn == null,
        UploadedBy = e.CreatedBy,
        UploadedAt = e.CreatedAt,
    };
}
