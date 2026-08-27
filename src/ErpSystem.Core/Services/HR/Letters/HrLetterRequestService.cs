using System.Globalization;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Letters;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ICurrencyService = ErpSystem.Core.Interfaces.Finance.ICurrencyService;

namespace ErpSystem.Core.Services.HR.Letters;

/// <summary>
/// D7's letter requests. See <see cref="IHrLetterRequestService"/> for the two fulfilment
/// routes and why the generated letter is frozen rather than re-rendered.
/// </summary>
public class HrLetterRequestService : IHrLetterRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly ICurrencyService _currencies;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<HrLetterRequestService> _logger;

    public HrLetterRequestService(
        IUnitOfWork unitOfWork,
        ITemplatedEmailService templatedEmail,
        ICompanyProfileProvider companyProfile,
        ICurrencyService currencies,
        ICurrentUserProvider currentUserProvider,
        ILogger<HrLetterRequestService> logger)
    {
        _unitOfWork = unitOfWork;
        _templatedEmail = templatedEmail;
        _companyProfile = companyProfile;
        _currencies = currencies;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>Which template renders which letter.</summary>
    private static string EventKeyFor(HrLetterType type) => type switch
    {
        HrLetterType.EmploymentConfirmation => HrLettersEmailCatalog.Events.EmploymentConfirmation,
        HrLetterType.IntroductionLetter => HrLettersEmailCatalog.Events.IntroductionLetter,
        HrLetterType.ServiceCertificate => HrLettersEmailCatalog.Events.ServiceCertificate,
        HrLetterType.SalaryConfirmation => HrLettersEmailCatalog.Events.SalaryConfirmation,
        _ => throw new InvalidOperationException($"No letter template is defined for '{type}'."),
    };

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private IQueryable<HrLetterRequest> Scoped(Guid tenantId) =>
        Bare(tenantId)
            .Include(r => r.Employee)
            .Include(r => r.IssuedBy);

    private IQueryable<HrLetterRequest> Bare(Guid tenantId) =>
        _unitOfWork.Repository<HrLetterRequest>()
            .GetQueryable()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

    // ── The employee's half ───────────────────────────────────────────────────

    public async Task<HrLetterRequestDto> CreateAsync(
        Guid employeeId, CreateHrLetterRequestDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        if (!Enum.IsDefined(dto.LetterType))
            throw new InvalidOperationException("That is not a letter this system issues.");
        if (string.IsNullOrWhiteSpace(dto.Purpose))
            throw new InvalidOperationException("Say what you need the letter for.");

        // One open request per letter type: a second is almost always an anxious re-click, and
        // two identical letters issued days apart is a real problem for whoever receives them.
        var alreadyOpen = await Bare(tenantId).AnyAsync(
            r => r.EmployeeId == employeeId
              && r.LetterType == dto.LetterType
              && r.Status == HrLetterRequestStatus.Pending,
            cancellationToken);
        if (alreadyOpen)
            throw new InvalidOperationException(
                "You already have a request for this letter waiting with HR.");

        var request = new HrLetterRequest
        {
            TenantId = tenantId,
            RequestNumber = await NextNumberAsync(tenantId, "HLR", r => r.RequestNumber, cancellationToken),
            EmployeeId = employeeId,
            LetterType = dto.LetterType,
            Purpose = dto.Purpose.Trim(),
            AddressedTo = string.IsNullOrWhiteSpace(dto.AddressedTo) ? null : dto.AddressedTo.Trim(),
            Status = HrLetterRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow,
        };

        await _unitOfWork.Repository<HrLetterRequest>().AddAsync(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Letter request {Number} ({Type}) filed by employee {EmployeeId}.",
            request.RequestNumber, request.LetterType, employeeId);

        return await RequireDtoAsync(request.Id, tenantId, cancellationToken);
    }

    public async Task<IEnumerable<HrLetterRequestDto>> GetMineAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var rows = await Scoped(tenantId)
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<HrLetterRequestDto?> GetByIdAsync(
        Guid id, Guid requestingEmployeeId, bool isHrDesk, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(id, requestingEmployeeId, isHrDesk, cancellationToken);
        return request is null ? null : Map(request);
    }

    public async Task<HrLetterRequestDto> CancelAsync(
        Guid id, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Caller-scoped in the query: a foreign request is a lookup miss, never a 403 that
        // would confirm it exists (the law slices 8–10 established).
        var request = await Scoped(tenantId)
                .FirstOrDefaultAsync(r => r.Id == id && r.EmployeeId == employeeId, cancellationToken)
            ?? throw new KeyNotFoundException("Letter request not found.");

        RequirePending(request);

        request.Status = HrLetterRequestStatus.Cancelled;
        request.CancelledAt = DateTime.UtcNow;
        await _unitOfWork.Repository<HrLetterRequest>().UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(request);
    }

    // ── The HR desk ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<HrLetterRequestDto>> GetQueueAsync(
        HrLetterRequestStatus? status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = Scoped(tenantId);
        if (status is { } wanted) query = query.Where(r => r.Status == wanted);

        var rows = await query
            .OrderBy(r => r.Status == HrLetterRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<HrLetterDocumentDto> PreviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var request = await LoadForRenderAsync(tenantId, id, cancellationToken);

        var rendered = await RenderAsync(request, letterNumber: null, cancellationToken);
        return new HrLetterDocumentDto
        {
            RequestId = request.Id,
            RequestNumber = request.RequestNumber,
            LetterType = request.LetterType,
            Subject = rendered.Subject,
            Html = rendered.HtmlBody,
            IsIssued = false,
        };
    }

    public async Task<HrLetterRequestDto> IssueGeneratedAsync(
        Guid id, Guid issuerEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var request = await LoadForRenderAsync(tenantId, id, cancellationToken);
        RequirePending(request);

        var letterNumber = await NextNumberAsync(tenantId, "HRL", r => r.LetterNumber!, cancellationToken);
        var rendered = await RenderAsync(request, letterNumber, cancellationToken);

        // Frozen, not re-rendered on read: the letter must say tomorrow what it said today,
        // even after a promotion changes the position it names.
        request.IssuedDocumentHtml = rendered.HtmlBody;
        request.LetterNumber = letterNumber;
        request.Status = HrLetterRequestStatus.Issued;
        request.IssuedById = issuerEmployeeId;
        request.IssuedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<HrLetterRequest>().UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Letter {LetterNumber} issued for request {Number} by {IssuerId}.",
            letterNumber, request.RequestNumber, issuerEmployeeId);

        return await RequireDtoAsync(request.Id, tenantId, cancellationToken);
    }

    public async Task AttachIssuedFileAsync(
        Guid id, Guid issuerEmployeeId, Guid fileUploadRecordId, Guid? documentRecordId,
        Guid? documentVersionId, string filePath, string fileName, string contentType, long fileSize,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var request = await Scoped(tenantId).FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Letter request not found.");
        RequirePending(request);

        request.FileUploadRecordId = fileUploadRecordId;
        request.DocumentRecordId = documentRecordId;
        request.DocumentVersionId = documentVersionId;
        request.FilePath = filePath;
        request.FileName = fileName;
        request.ContentType = contentType;
        request.FileSize = fileSize;

        request.LetterNumber ??= await NextNumberAsync(tenantId, "HRL", r => r.LetterNumber!, cancellationToken);
        request.Status = HrLetterRequestStatus.Issued;
        request.IssuedById = issuerEmployeeId;
        request.IssuedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<HrLetterRequest>().UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<HrLetterRequestDto> RejectAsync(
        Guid id, Guid issuerEmployeeId, RejectHrLetterRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var request = await Scoped(tenantId).FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Letter request not found.");
        RequirePending(request);

        if (string.IsNullOrWhiteSpace(dto.Comments))
            throw new InvalidOperationException("Say why the letter is not being issued.");

        request.Status = HrLetterRequestStatus.Rejected;
        request.DecisionComments = dto.Comments.Trim();
        request.RejectedAt = DateTime.UtcNow;
        request.IssuedById = issuerEmployeeId;

        await _unitOfWork.Repository<HrLetterRequest>().UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(request);
    }

    public async Task<HrLetterDocumentDto?> GetIssuedDocumentAsync(
        Guid id, Guid requestingEmployeeId, bool isHrDesk, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(id, requestingEmployeeId, isHrDesk, cancellationToken);
        if (request is null
            || request.Status != HrLetterRequestStatus.Issued
            || string.IsNullOrWhiteSpace(request.IssuedDocumentHtml))
        {
            return null;
        }

        return new HrLetterDocumentDto
        {
            RequestId = request.Id,
            RequestNumber = request.RequestNumber,
            LetterNumber = request.LetterNumber,
            LetterType = request.LetterType,
            Subject = $"{request.LetterType}",
            Html = request.IssuedDocumentHtml,
            IsIssued = true,
            IssuedAt = request.IssuedAt,
            IssuedByName = request.IssuedBy?.FullName,
        };
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static void RequirePending(HrLetterRequest request)
    {
        if (request.Status != HrLetterRequestStatus.Pending)
            throw new InvalidOperationException(
                $"This request has already been {request.Status.ToString().ToLowerInvariant()}.");
    }

    private async Task<HrLetterRequest?> LoadAsync(
        Guid id, Guid requestingEmployeeId, bool isHrDesk, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var request = await Scoped(tenantId).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return null;
        if (!isHrDesk && request.EmployeeId != requestingEmployeeId) return null;
        return request;
    }

    /// <summary>Loads the request with everything the letter's tokens read.</summary>
    private async Task<HrLetterRequest> LoadForRenderAsync(Guid tenantId, Guid id, CancellationToken ct)
        => await Bare(tenantId)
               .Include(r => r.IssuedBy)
               .Include(r => r.Employee).ThenInclude(e => e.Position)
               .Include(r => r.Employee).ThenInclude(e => e.Department)
               .FirstOrDefaultAsync(r => r.Id == id, ct)
           ?? throw new KeyNotFoundException("Letter request not found.");

    private async Task<RenderedEmail> RenderAsync(
        HrLetterRequest request, string? letterNumber, CancellationToken ct)
    {
        var company = await _companyProfile.GetAsync(ct);

        // The salary letter states an amount to a bank. `Employee.Salary` is a bare decimal with
        // no currency column anywhere on the record, so the unit comes from Finance's BASE
        // currency — the sanctioned read (`hr-finance-integration-split`), and the one HR assets
        // already uses for surcharges. A figure printed without its currency on a letter to a
        // lender is not a cosmetic problem.
        string? currencyCode = null;
        if (request.LetterType == HrLetterType.SalaryConfirmation)
        {
            try { currencyCode = (await _currencies.GetBaseCurrencyAsync(ct))?.CurrencyCode; }
            catch (Exception ex)
            {
                // Finance is another module; a failure there must not take out the letter. The
                // template omits the whole salary clause when the amount cannot be stated
                // properly, which is the right failure: no figure beats an unlabelled one.
                _logger.LogWarning(ex, "Could not resolve the base currency for a salary letter.");
            }
        }

        var tokens = BuildTokens(request, company, letterNumber, currencyCode);

        var rendered = await _templatedEmail.RenderAsync(
            HrLettersEmailCatalog.Module, EventKeyFor(request.LetterType), tokens, ct);

        // Only reachable if the catalog registration were dropped, since the built-in default is
        // the fallback — but a blank letter is exactly the failure nobody notices until it has
        // been handed to a bank, so it refuses in words instead.
        if (rendered is null)
            throw new InvalidOperationException(
                "No template is configured for this letter, and no built-in default could be "
                + "resolved. Check the HR Letters email templates.");

        return rendered;
    }

    private static Dictionary<string, string?> BuildTokens(
        HrLetterRequest r, CompanyProfile company, string? letterNumber, string? currencyCode)
    {
        var e = r.Employee;

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            // Company identity comes from the per-tenant profile, never from configuration — the
            // provider already falls back to the Tenant record when no profile row exists yet.
            ["CompanyName"] = string.IsNullOrWhiteSpace(company.LegalName) ? null : company.LegalName,
            ["CompanyAddress"] = ComposeAddress(company),
            ["CompanyLogoUrl"] = company.LogoUrl,
            ["CompanyFooter"] = company.DocumentFooterText,
            ["SignatoryName"] = company.DefaultSignatoryName,
            ["SignatoryTitle"] = company.DefaultSignatoryTitle,

            ["LetterDate"] = DateTime.UtcNow.ToString("d MMMM yyyy", CultureInfo.InvariantCulture),
            ["LetterNumber"] = letterNumber,
            ["AddressedTo"] = string.IsNullOrWhiteSpace(r.AddressedTo)
                ? "To whom it may concern"
                : r.AddressedTo,

            ["EmployeeName"] = e?.FullName,
            ["EmployeeNumber"] = e?.EmployeeNumber,
            ["PositionTitle"] = e?.Position?.Title,
            ["DepartmentName"] = e?.Department?.Name,
            ["EmploymentType"] = e?.EmploymentType.ToString(),
            ["DateEmployed"] = e?.DateEmployed?.ToString("d MMMM yyyy", CultureInfo.InvariantCulture),
            ["YearsOfService"] = e?.YearsOfService?.ToString(CultureInfo.InvariantCulture),

            ["Purpose"] = r.Purpose,

            // Only the salary letter's template names this token; the others never merge it, so
            // an employment confirmation cannot accidentally disclose pay (proven by the probe's
            // disclosure check across all four letters).
            //
            // ⚠ It is null unless BOTH the amount and its currency are known — the template then
            // drops the whole clause. "96,000.00" on a letter to a lender, with no unit, is worse
            // than saying nothing: the reader supplies a currency of their own choosing.
            ["AnnualSalary"] = e?.Salary is { } salary && !string.IsNullOrWhiteSpace(currencyCode)
                ? $"{currencyCode} {salary.ToString("N2", CultureInfo.InvariantCulture)}"
                : null,
        };
    }

    private static string? ComposeAddress(CompanyProfile c)
    {
        var parts = new[] { c.RegisteredAddress, c.City, c.Region, c.DigitalAddress }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    /// <remarks>
    /// Counts issued numbers over rows INCLUDING soft-deleted ones — counting live rows
    /// re-issues a number the moment anything is deleted (the shape fixed across the SHE,
    /// movement, grievance, disciplinary and profile-change generators).
    /// </remarks>
    private async Task<string> NextNumberAsync(
        Guid tenantId, string prefixCode,
        Func<HrLetterRequest, string> selector, CancellationToken ct)
    {
        var prefix = $"{prefixCode}-{DateTime.UtcNow.Year}-";

        var issued = await _unitOfWork.Repository<HrLetterRequest>()
            .GetQueryableIncludingDeleted(r => r.TenantId == tenantId)
            .ToListAsync(ct);

        var highest = issued
            .Select(selector)
            .Where(n => !string.IsNullOrEmpty(n) && n.StartsWith(prefix, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D5}";
    }

    private async Task<HrLetterRequestDto> RequireDtoAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var row = await Scoped(tenantId).AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Letter request not found.");
        return Map(row);
    }

    private static HrLetterRequestDto Map(HrLetterRequest r) => new()
    {
        Id = r.Id,
        RequestNumber = r.RequestNumber,
        EmployeeId = r.EmployeeId,
        EmployeeName = r.Employee?.FullName ?? string.Empty,
        EmployeeNumber = r.Employee?.EmployeeNumber ?? string.Empty,
        LetterType = r.LetterType,
        Purpose = r.Purpose,
        AddressedTo = r.AddressedTo,
        Status = r.Status,
        RequestedAt = r.RequestedAt,
        IssuedById = r.IssuedById,
        IssuedByName = r.IssuedBy?.FullName,
        IssuedAt = r.IssuedAt,
        LetterNumber = r.LetterNumber,
        DecisionComments = r.DecisionComments,
        RejectedAt = r.RejectedAt,
        CancelledAt = r.CancelledAt,
        HasDocument = r.HasDocument,
        Fulfilment = !string.IsNullOrWhiteSpace(r.IssuedDocumentHtml)
            ? "Generated"
            : r.FileUploadRecordId.HasValue ? "Uploaded" : null,
        FileName = r.FileName,
    };
}
