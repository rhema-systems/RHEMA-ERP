using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The employee document file, its shared vocabulary, and the position requirements that make it
/// answerable (Employee Master feedback: "no employee document attachments", "no mandatory
/// documents against a position").
/// </summary>
/// <remarks>
/// <para><b>A document is created ONLY by the upload gate.</b> There is no JSON create on this
/// service — <see cref="AttachAsync"/> takes the gate's facts as parameters and is called from the
/// controller's multipart endpoint after the file has been scanned and registered. That is the
/// difference between this table and the six that had to be retro-fitted: a caller can describe
/// what a file IS and can never say where it lives.</para>
///
/// <para><b>The uploader is a parameter, never a DTO field</b> — the D-05 shape, met five times
/// before this.</para>
/// </remarks>
public class EmployeeDocumentService : IEmployeeDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    // For the tenant's default currency, which is what an unstated guarantor amount is stated in.
    private readonly ICompanyHrPolicyProvider _policyProvider;
    private readonly ILogger<EmployeeDocumentService> _logger;

    public EmployeeDocumentService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ICompanyHrPolicyProvider policyProvider,
        ILogger<EmployeeDocumentService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _policyProvider = policyProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DOCUMENT TYPES
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<EmployeeDocumentTypeDto>> GetTypesAsync(
        bool includeInactive = false, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        // The count is what makes the delete affordance honest: a type in use cannot be removed,
        // and the screen should say so before the button is pressed rather than after.
        return await _unitOfWork.Repository<EmployeeDocumentType>().GetQueryable()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && (includeInactive || t.IsActive))
            .OrderBy(t => t.Name)
            .Select(t => new EmployeeDocumentTypeDto
            {
                Id = t.Id,
                Name = t.Name,
                Code = t.Code,
                Description = t.Description,
                HasExpiry = t.HasExpiry,
                ExpiryReminderLeadDays = t.ExpiryReminderLeadDays,
                IsActive = t.IsActive,
                DocumentCount = t.Documents.Count(d => !d.IsDeleted),
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Seeds a starting vocabulary, skipping any name the tenant already has.
    /// </summary>
    /// <remarks>
    /// <para>An endpoint rather than <c>HasData</c>, the same call the separation clearance
    /// templates made: this is a tenant's editable list, not platform data, and a migration seed
    /// would re-assert it on a tenant that had deliberately pruned it.</para>
    ///
    /// <para>⚠ These are a defensible STARTING list, not TDC's answer. The Employee Master feedback
    /// asks for mandatory documents per position without saying which documents exist, so HR renames
    /// and extends this from the screen. Skipping existing names makes it safe to run twice.</para>
    /// </remarks>
    public async Task<int> SeedDefaultTypesAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var defaults = new (string Name, string Code, bool HasExpiry, string Description)[]
        {
            ("Signed employment contract", "CONTRACT", false, "The executed contract of employment."),
            ("National identification", "NATIONAL-ID", true, "Ghana Card or equivalent national ID."),
            ("Passport", "PASSPORT", true, "Passport biodata page."),
            ("Academic certificate", "ACADEMIC", false, "Degree, diploma or school certificate."),
            ("Professional certificate", "PROFESSIONAL", true, "Membership or practising certificate."),
            ("Driving licence", "DRIVING", true, "Required of anyone who drives on duty."),
            ("Work or residence permit", "PERMIT", true, "Required of expatriate staff."),
            ("Medical fitness certificate", "MEDICAL", true, "Pre-employment or periodic fitness."),
            ("Curriculum vitae", "CV", false, "The CV held on file."),
            ("Bank account confirmation", "BANK", false, "Evidence of the account salary is paid into."),
        };

        var existing = await _unitOfWork.Repository<EmployeeDocumentType>().GetQueryable()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => t.Name)
            .ToListAsync(ct);

        var added = 0;
        foreach (var (name, code, hasExpiry, description) in defaults)
        {
            if (existing.Any(e => e == name)) continue;

            await _unitOfWork.Repository<EmployeeDocumentType>().AddAsync(new EmployeeDocumentType
            {
                TenantId = tenantId,
                Name = name,
                Code = code,
                Description = description,
                HasExpiry = hasExpiry,
                IsActive = true,
            });
            added++;
        }

        if (added > 0) await _unitOfWork.SaveChangesAsync(ct);
        return added;
    }

    public async Task<EmployeeDocumentTypeDto> CreateTypeAsync(
        CreateEmployeeDocumentTypeDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await EnsureTypeNameIsFreeAsync(tenantId, dto.Name, null, ct);

        var entity = new EmployeeDocumentType
        {
            TenantId = tenantId,
            Name = dto.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim().ToUpperInvariant(),
            Description = dto.Description,
            HasExpiry = dto.HasExpiry,
            ExpiryReminderLeadDays = dto.ExpiryReminderLeadDays,
            IsActive = dto.IsActive,
        };

        await _unitOfWork.Repository<EmployeeDocumentType>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await GetTypesAsync(true, ct)).First(t => t.Id == entity.Id);
    }

    public async Task<EmployeeDocumentTypeDto> UpdateTypeAsync(
        Guid id, UpdateEmployeeDocumentTypeDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireTypeAsync(tenantId, id, ct);
        await EnsureTypeNameIsFreeAsync(tenantId, dto.Name, id, ct);

        entity.Name = dto.Name.Trim();
        entity.Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim().ToUpperInvariant();
        entity.Description = dto.Description;
        entity.HasExpiry = dto.HasExpiry;
        entity.ExpiryReminderLeadDays = dto.ExpiryReminderLeadDays;
        entity.IsActive = dto.IsActive;

        await _unitOfWork.SaveChangesAsync(ct);
        return (await GetTypesAsync(true, ct)).First(t => t.Id == id);
    }

    /// <summary>
    /// Retires a type. Refused while documents or requirements still reference it.
    /// </summary>
    /// <remarks>
    /// ⚠ Refusing rather than cascading is the point. A soft delete here would leave live documents
    /// pointing at a type the vocabulary no longer lists, and the compliance read would then report
    /// a requirement it cannot name. Deactivating is the supported way to take a type out of use
    /// without rewriting history, and the refusal says so.
    /// </remarks>
    public async Task DeleteTypeAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireTypeAsync(tenantId, id, ct);

        var documents = await _unitOfWork.Repository<EmployeeDocument>().GetQueryable()
            .CountAsync(d => d.TenantId == tenantId && !d.IsDeleted && d.DocumentTypeId == id, ct);
        var requirements = await _unitOfWork.Repository<PositionDocumentRequirement>().GetQueryable()
            .CountAsync(r => r.TenantId == tenantId && !r.IsDeleted && r.DocumentTypeId == id, ct);

        if (documents > 0 || requirements > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' is still in use — {documents} document(s) and {requirements} position "
                + "requirement(s) reference it. Mark it inactive instead, which stops it being offered "
                + "on new uploads without rewriting what is already on file.");

        entity.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DOCUMENTS
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<EmployeeDocumentDto>> GetForEmployeeAsync(
        Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await QueryDocuments(tenantId)
            .Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(d => Map(d, today)).ToList();
    }

    public async Task<EmployeeDocumentDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var row = await QueryDocuments(tenantId).FirstOrDefaultAsync(d => d.Id == id, ct);
        return row is null ? null : Map(row, today);
    }

    /// <summary>The raw row, for the download path's entitlement check.</summary>
    public async Task<EmployeeDocument?> GetEntityAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<EmployeeDocument>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted, ct);
    }

    /// <summary>
    /// Records a document AFTER the gate has scanned and registered its file.
    /// </summary>
    /// <param name="uploadedByEmployeeId">
    /// The authenticated employee. A parameter rather than a DTO field, so it cannot be asserted by
    /// the caller — the D-05 shape.
    /// </param>
    public async Task<EmployeeDocumentDto> AttachAsync(
        Guid employeeId,
        Guid documentTypeId,
        string? title,
        string? description,
        DateOnly? issuedOn,
        DateOnly? expiresOn,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId,
        string? fileName,
        string? mimeType,
        long? fileSizeBytes,
        Guid? uploadedByEmployeeId,
        CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var employeeExists = await _unitOfWork.Repository<Employee>().GetQueryable()
            .AnyAsync(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted, ct);
        if (!employeeExists)
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        // Validating the type here rather than letting the FK fail: SQL 547 surfaces as an
        // unexplained 500 rather than "that document type was not found".
        await RequireTypeAsync(tenantId, documentTypeId, ct);

        var entity = new EmployeeDocument
        {
            TenantId = tenantId,
            EmployeeId = employeeId,
            DocumentTypeId = documentTypeId,
            Title = title,
            Description = description,
            IssuedOn = issuedOn,
            ExpiresOn = expiresOn,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
            FileName = fileName,
            MimeType = mimeType,
            FileSizeBytes = fileSizeBytes,
            UploadedById = uploadedByEmployeeId,
        };

        await _unitOfWork.Repository<EmployeeDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        // ⚠ Re-read rather than mapping the tracked entity. A just-added row has no navigations
        // loaded, so the create response would carry a blank documentTypeName and uploadedByName
        // while the list read beside it resolves both — the stale-navigation-on-a-write-response
        // shape, found four times before by a harness assertion and never by reading the code.
        var saved = await QueryDocuments(tenantId).FirstAsync(d => d.Id == entity.Id, ct);
        return Map(saved, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<EmployeeDocumentDto> UpdateAsync(
        Guid id, UpdateEmployeeDocumentDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<EmployeeDocument>().GetQueryable()
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted, ct)
            ?? throw new ArgumentException($"Employee document with ID '{id}' not found.");

        await RequireTypeAsync(tenantId, dto.DocumentTypeId, ct);

        entity.DocumentTypeId = dto.DocumentTypeId;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.IssuedOn = dto.IssuedOn;
        entity.ExpiresOn = dto.ExpiresOn;

        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await QueryDocuments(tenantId).FirstAsync(d => d.Id == id, ct);
        return Map(saved, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<EmployeeDocument>().GetQueryable()
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted, ct)
            ?? throw new ArgumentException($"Employee document with ID '{id}' not found.");

        entity.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FILES THAT BELONG TO A ROW, NOT TO THE FILE (lane 3a)
    // ═══════════════════════════════════════════════════════════════════════
    //
    // ⚠ A guarantor's photograph and a referee's letter hang off THEIR row, not off the employee's
    // general document file — "which guarantor is this a photo of" is not a question a shared
    // documents list can answer. So they follow the oath-of-secrecy shape: one file per row, its
    // three gate identifiers on the row itself, and no path anywhere.
    //
    // ⚠ `EmployeeGuarantor.GuarantorFormPath` is a pre-existing caller-supplied path of exactly the
    // kind D-10/D-14/D-39 removed. It is untouched here because it holds legacy values and gating
    // it is its own piece of work — but nothing new uses that shape.

    public async Task<Employee> AttachEmployeePhotoAsync(
        Guid employeeId, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<Employee>().GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted, ct)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        entity.PhotoFileUploadRecordId = fileUploadRecordId;
        entity.PhotoDocumentRecordId = documentRecordId;
        entity.PhotoDocumentVersionId = documentVersionId;
        entity.PhotoFileName = fileName;
        entity.PhotoMimeType = mimeType;
        entity.PhotoFileSizeBytes = fileSizeBytes;

        // ⚠ PicturePath is deliberately NOT cleared. It is the ported location and may still be the
        // only copy of an older image; the download prefers the gated ids and falls back to it, so
        // clearing it here would destroy the fallback for anyone who has not re-uploaded.
        await _unitOfWork.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Employee?> GetEmployeeForPhotoAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<Employee>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId && !e.IsDeleted, ct);
    }

    public async Task<EmployeeDependent> AttachDependentPhotoAsync(
        Guid dependentId, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<EmployeeDependent>().GetQueryable()
            .FirstOrDefaultAsync(d => d.Id == dependentId && d.TenantId == tenantId && !d.IsDeleted, ct)
            ?? throw new ArgumentException($"Dependant with ID '{dependentId}' not found.");

        entity.PhotoFileUploadRecordId = fileUploadRecordId;
        entity.PhotoDocumentRecordId = documentRecordId;
        entity.PhotoDocumentVersionId = documentVersionId;
        entity.PhotoFileName = fileName;
        entity.PhotoMimeType = mimeType;
        entity.PhotoFileSizeBytes = fileSizeBytes;

        await _unitOfWork.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<EmployeeDependent?> GetDependentForPhotoAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<EmployeeDependent>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted, ct);
    }

    public async Task<EmployeeGuarantor> AttachGuarantorPhotoAsync(
        Guid guarantorId, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<EmployeeGuarantor>().GetQueryable()
            .FirstOrDefaultAsync(g => g.Id == guarantorId && g.TenantId == tenantId && !g.IsDeleted, ct)
            ?? throw new ArgumentException($"Guarantor with ID '{guarantorId}' not found.");

        entity.PhotoFileUploadRecordId = fileUploadRecordId;
        entity.PhotoDocumentRecordId = documentRecordId;
        entity.PhotoDocumentVersionId = documentVersionId;
        entity.PhotoFileName = fileName;
        entity.PhotoMimeType = mimeType;
        entity.PhotoFileSizeBytes = fileSizeBytes;

        await _unitOfWork.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<EmployeeGuarantor?> GetGuarantorAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<EmployeeGuarantor>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tenantId && !g.IsDeleted, ct);
    }

    public async Task<EmployeeReferee> AttachRefereeLetterAsync(
        Guid refereeId, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<EmployeeReferee>().GetQueryable()
            .FirstOrDefaultAsync(r => r.Id == refereeId && r.TenantId == tenantId && !r.IsDeleted, ct)
            ?? throw new ArgumentException($"Referee with ID '{refereeId}' not found.");

        entity.LetterFileUploadRecordId = fileUploadRecordId;
        entity.LetterDocumentRecordId = documentRecordId;
        entity.LetterDocumentVersionId = documentVersionId;
        entity.LetterFileName = fileName;
        entity.LetterMimeType = mimeType;
        entity.LetterFileSizeBytes = fileSizeBytes;

        // Receiving the written reference IS being contacted. Leaving IsContacted false while a
        // letter sits on the row would let the referee report contradict the file.
        if (!entity.IsContacted)
        {
            entity.IsContacted = true;
            entity.ContactedDate ??= DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<EmployeeReferee?> GetRefereeAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<EmployeeReferee>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DOCUMENTS THAT PERTAIN TO A GUARANTOR (round 2, lane A-6)
    // ═══════════════════════════════════════════════════════════════════════
    //
    // The signed form, an ID scan, a payslip — many per guarantor, each with a kind. The photograph
    // stays as six columns on the row (one face); these are the papers. They speak the employee
    // document vocabulary rather than minting a second one.

    private IQueryable<EmployeeGuarantorDocument> QueryGuarantorDocuments(Guid tenantId) =>
        _unitOfWork.Repository<EmployeeGuarantorDocument>().GetQueryable()
            .AsNoTracking()
            .Include(d => d.DocumentType)
            .Include(d => d.UploadedBy)
            .Where(d => d.TenantId == tenantId && !d.IsDeleted);

    private static EmployeeGuarantorDocumentDto MapGuarantorDocument(EmployeeGuarantorDocument d, DateOnly today) => new()
    {
        Id = d.Id,
        GuarantorId = d.GuarantorId,
        DocumentTypeId = d.DocumentTypeId,
        DocumentTypeName = d.DocumentType?.Name,
        Title = d.Title,
        Description = d.Description,
        IssuedOn = d.IssuedOn,
        ExpiresOn = d.ExpiresOn,
        IsExpired = d.ExpiresOn is DateOnly e && e < today,
        FileName = d.FileName,
        MimeType = d.MimeType,
        FileSizeBytes = d.FileSizeBytes,
        UploadedById = d.UploadedById,
        UploadedByName = d.UploadedBy is null ? null : $"{d.UploadedBy.FirstName} {d.UploadedBy.LastName}".Trim(),
        CreatedAt = d.CreatedAt,
    };

    public async Task<IEnumerable<EmployeeGuarantorDocumentDto>> GetGuarantorDocumentsAsync(
        Guid guarantorId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await QueryGuarantorDocuments(tenantId)
            .Where(d => d.GuarantorId == guarantorId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(d => MapGuarantorDocument(d, today)).ToList();
    }

    public async Task<EmployeeGuarantorDocument?> GetGuarantorDocumentEntityAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<EmployeeGuarantorDocument>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted, ct);
    }

    public async Task<EmployeeGuarantorDocumentDto> AttachGuarantorDocumentAsync(
        Guid guarantorId, Guid documentTypeId, string? title, string? description,
        DateOnly? issuedOn, DateOnly? expiresOn,
        Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes,
        Guid? uploadedByEmployeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var guarantorExists = await _unitOfWork.Repository<EmployeeGuarantor>().GetQueryable()
            .AnyAsync(g => g.Id == guarantorId && g.TenantId == tenantId && !g.IsDeleted, ct);
        if (!guarantorExists)
            throw new ArgumentException($"Guarantor with ID '{guarantorId}' not found.");

        await RequireTypeAsync(tenantId, documentTypeId, ct);

        var entity = new EmployeeGuarantorDocument
        {
            TenantId = tenantId,
            GuarantorId = guarantorId,
            DocumentTypeId = documentTypeId,
            Title = title,
            Description = description,
            IssuedOn = issuedOn,
            ExpiresOn = expiresOn,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
            FileName = fileName,
            MimeType = mimeType,
            FileSizeBytes = fileSizeBytes,
            UploadedById = uploadedByEmployeeId,
        };

        await _unitOfWork.Repository<EmployeeGuarantorDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await QueryGuarantorDocuments(tenantId).FirstAsync(d => d.Id == entity.Id, ct);
        return MapGuarantorDocument(saved, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task DeleteGuarantorDocumentAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<EmployeeGuarantorDocument>().GetQueryable()
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted, ct)
            ?? throw new ArgumentException($"Guarantor document with ID '{id}' not found.");

        entity.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  POSITION REQUIREMENTS
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<PositionDocumentRequirementDto>> GetRequirementsAsync(
        Guid positionId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<PositionDocumentRequirement>().GetQueryable()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.PositionId == positionId)
            .OrderBy(r => r.DocumentType.Name)
            .Select(r => new PositionDocumentRequirementDto
            {
                Id = r.Id,
                PositionId = r.PositionId,
                PositionTitle = r.Position.Title,
                DocumentTypeId = r.DocumentTypeId,
                DocumentTypeName = r.DocumentType.Name,
                IsMandatory = r.IsMandatory,
                Notes = r.Notes,
            })
            .ToListAsync(ct);
    }

    public async Task<PositionDocumentRequirementDto> AddRequirementAsync(
        CreatePositionDocumentRequirementDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await RequireTypeAsync(tenantId, dto.DocumentTypeId, ct);

        var positionExists = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .AnyAsync(p => p.Id == dto.PositionId && p.TenantId == tenantId && !p.IsDeleted, ct);
        if (!positionExists)
            throw new ArgumentException($"Position with ID '{dto.PositionId}' not found.");

        // ⚠ Revive rather than insert a second row — and it must be `GetQueryableIncludingDeleted`.
        // The first version used `GetQueryable()`, which FILTERS soft-deleted rows, so the revive
        // branch could never find anything and every re-add inserted a duplicate. It did not throw,
        // because the unique index is filtered on IsDeleted = 0 and the tombstone therefore does not
        // occupy the slot — so the bug was invisible except as an id that changed. Caught by the
        // harness asserting the returned id EQUALS the original, which is the assertion that shape
        // needs: "it worked" and "it revived" look identical without it.
        // ⚠ ORDERED, because there can be MANY tombstones for one key. The unique index is filtered
        // on IsDeleted = 0, so add-remove-add-remove leaves a tombstone each time and "the" previous
        // row is ambiguous. Taking the most recent is the only answer that stays stable as the
        // history grows; an unordered FirstOrDefault revives an arbitrary one, which reads as
        // working until somebody notices the notes came back from three versions ago.
        var existing = await _unitOfWork.Repository<PositionDocumentRequirement>()
            .GetQueryableIncludingDeleted(r => r.TenantId == tenantId
                                            && r.PositionId == dto.PositionId
                                            && r.DocumentTypeId == dto.DocumentTypeId)
            .OrderByDescending(r => r.IsDeleted ? 0 : 1)   // a live row wins outright
            .ThenByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            if (!existing.IsDeleted)
                throw new InvalidOperationException("That document is already required for this position.");

            existing.IsDeleted = false;
            existing.IsMandatory = dto.IsMandatory;
            existing.Notes = dto.Notes;
            await _unitOfWork.SaveChangesAsync(ct);
            return (await GetRequirementsAsync(dto.PositionId, ct)).First(r => r.Id == existing.Id);
        }

        var entity = new PositionDocumentRequirement
        {
            TenantId = tenantId,
            PositionId = dto.PositionId,
            DocumentTypeId = dto.DocumentTypeId,
            IsMandatory = dto.IsMandatory,
            Notes = dto.Notes,
        };

        await _unitOfWork.Repository<PositionDocumentRequirement>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await GetRequirementsAsync(dto.PositionId, ct)).First(r => r.Id == entity.Id);
    }

    public async Task<PositionDocumentRequirementDto> UpdateRequirementAsync(
        Guid id, UpdatePositionDocumentRequirementDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<PositionDocumentRequirement>().GetQueryable()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted, ct)
            ?? throw new ArgumentException($"Requirement with ID '{id}' not found.");

        entity.IsMandatory = dto.IsMandatory;
        entity.Notes = dto.Notes;
        await _unitOfWork.SaveChangesAsync(ct);

        return (await GetRequirementsAsync(entity.PositionId, ct)).First(r => r.Id == id);
    }

    public async Task DeleteRequirementAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<PositionDocumentRequirement>().GetQueryable()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted, ct)
            ?? throw new ArgumentException($"Requirement with ID '{id}' not found.");

        entity.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  COMPLIANCE — what the requirements are FOR
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// What this employee's position requires, and whether they hold it.
    /// </summary>
    /// <remarks>
    /// ⚠ An employee with no position has no requirements and is reported COMPLIANT, with a null
    /// position. That is deliberate and is the honest answer: nothing is required of them. Reporting
    /// non-compliance would put every unplaced employee on a chase list nobody can clear — the
    /// `ExpectedHeadcount` shape that already cost this module one mid-slice redesign.
    /// </remarks>
    public async Task<EmployeeDocumentComplianceDto> GetComplianceAsync(
        Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .AsNoTracking()
            .Where(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted)
            .Select(e => new
            {
                e.Id,
                Name = e.FirstName + " " + e.LastName,
                e.PositionId,
                PositionTitle = e.Position != null ? e.Position.Title : null,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        var result = new EmployeeDocumentComplianceDto
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            // ⚠ `Employee.PositionId` is a NON-NULLABLE Guid, so "no position" is Guid.Empty rather
            // than null. The DTO keeps it nullable and translates, because an all-zeros GUID on the
            // wire is a value every client would have to know to special-case.
            PositionId = employee.PositionId == Guid.Empty ? null : employee.PositionId,
            PositionTitle = employee.PositionTitle,
            IsCompliant = true,
        };

        // ⚠ Defensive, not load-bearing: measured 2026-09-01, 0 of 1,476 live employees carry an
        // empty position. Kept because an import can produce one and the alternative is a query
        // that silently matches every requirement row whose PositionId is also empty.
        if (employee.PositionId == Guid.Empty)
        {
            result.IsGuarantorSatisfied = true;
            result.IsFullyCompliant = result.IsCompliant;
            return result;
        }

        var positionId = employee.PositionId;

        var requirements = await _unitOfWork.Repository<PositionDocumentRequirement>().GetQueryable()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.PositionId == positionId)
            .Select(r => new { r.DocumentTypeId, TypeName = r.DocumentType.Name, r.IsMandatory })
            .ToListAsync(ct);

        if (requirements.Count == 0)
        {
            // ⚠ NOT a shortcut out of the whole method. A position can require a guarantor and no
            // documents at all, and returning here reported an unguaranteed cashier as compliant.
            await ApplyGuarantorComplianceAsync(result, tenantId, positionId, employeeId, ct);
            return result;
        }

        var held = await _unitOfWork.Repository<EmployeeDocument>().GetQueryable()
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.EmployeeId == employeeId)
            .Select(d => new { d.Id, d.DocumentTypeId, d.ExpiresOn })
            .ToListAsync(ct);

        foreach (var requirement in requirements.OrderBy(r => r.TypeName))
        {
            var candidates = held.Where(h => h.DocumentTypeId == requirement.DocumentTypeId).ToList();

            // A document expiring TODAY is still valid; one that expired yesterday is not.
            var live = candidates
                .Where(c => c.ExpiresOn == null || c.ExpiresOn >= today)
                .OrderByDescending(c => c.ExpiresOn ?? DateOnly.MaxValue)
                .FirstOrDefault();

            var line = new EmployeeDocumentComplianceLineDto
            {
                DocumentTypeId = requirement.DocumentTypeId,
                DocumentTypeName = requirement.TypeName,
                IsMandatory = requirement.IsMandatory,
                IsSatisfied = live is not null,
                DocumentId = live?.Id,
                ExpiresOn = live?.ExpiresOn,
                DaysUntilExpiry = live?.ExpiresOn is DateOnly due
                    ? due.DayNumber - today.DayNumber
                    : null,
                // Held once, but expired — a different conversation from never held.
                IsExpiredOnly = live is null && candidates.Count > 0,
            };

            result.Lines.Add(line);
        }

        result.MandatoryCount = result.Lines.Count(l => l.IsMandatory);
        result.MandatorySatisfiedCount = result.Lines.Count(l => l.IsMandatory && l.IsSatisfied);
        result.IsCompliant = result.MandatoryCount == result.MandatorySatisfiedCount;

        await ApplyGuarantorComplianceAsync(result, tenantId, positionId, employeeId, ct);
        return result;
    }

    /// <summary>
    /// Whether the employee's guarantors meet what their post requires.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Amounts in another currency are counted separately and never converted.</b>
    /// Not because conversion is unavailable — <c>HrCurrencyBridge</c> can do it and Finance's
    /// rate inversion (cross-module defect #2) was resolved by PR #99 and re-verified at merge #8.
    /// Because a converted verdict moves with the rate, so an employee would drift in and out of
    /// compliance with nobody having acted; and because "which date's rate" — signature date or
    /// today — is a policy choice TDC has not made. The foreign amounts are counted and reported,
    /// so the gap is visible and the choice stays open.</para>
    ///
    /// <para>⚠ <b>Sum, not maximum.</b> Two guarantors at half the amount each satisfy a surety
    /// between them — that is what co-signing means. If TDC wants a single guarantor to cover the
    /// whole sum, that is a rule to add, not an assumption to bake in silently.</para>
    /// </remarks>
    private async Task ApplyGuarantorComplianceAsync(
        EmployeeDocumentComplianceDto result, Guid tenantId, Guid positionId, Guid employeeId,
        CancellationToken ct)
    {
        var position = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .AsNoTracking()
            .Where(p => p.Id == positionId && p.TenantId == tenantId)
            .Select(p => new
            {
                p.RequiresGuarantor,
                p.RequiredGuarantorAmount,
                p.RequiredGuarantorCurrencyCode,
            })
            .FirstOrDefaultAsync(ct);

        if (position is null || !position.RequiresGuarantor)
        {
            // Nothing required: satisfied, and IsFullyCompliant is just the document answer.
            result.IsGuarantorSatisfied = true;
            result.IsFullyCompliant = result.IsCompliant;
            return;
        }

        var settings = await _policyProvider.GetAsync(ct);
        var requiredCurrency =
            (position.RequiredGuarantorCurrencyCode ?? settings.DefaultCurrencyCode ?? string.Empty)
            .Trim().ToUpperInvariant();

        var guarantors = await _unitOfWork.Repository<EmployeeGuarantor>().GetQueryable()
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted && g.EmployeeId == employeeId && g.IsActive)
            .Select(g => new { g.AmountGuaranteed, g.AmountGuaranteedCurrencyCode })
            .ToListAsync(ct);

        result.RequiresGuarantor = true;
        result.RequiredGuarantorAmount = position.RequiredGuarantorAmount;
        result.RequiredGuarantorCurrencyCode = requiredCurrency;
        result.GuarantorCount = guarantors.Count;

        // A guarantor with no stated currency is stated in the tenant default, which is what the
        // requirement falls back to as well — so they compare.
        static string Code(string? c, string fallback) =>
            string.IsNullOrWhiteSpace(c) ? fallback : c.Trim().ToUpperInvariant();

        result.GuaranteedTotal = guarantors
            .Where(g => g.AmountGuaranteed.HasValue
                     && Code(g.AmountGuaranteedCurrencyCode, requiredCurrency) == requiredCurrency)
            .Sum(g => g.AmountGuaranteed!.Value);

        result.GuarantorsInOtherCurrencies = guarantors
            .Count(g => g.AmountGuaranteed.HasValue
                     && Code(g.AmountGuaranteedCurrencyCode, requiredCurrency) != requiredCurrency);

        if (position.RequiredGuarantorAmount is not decimal required || required <= 0m)
        {
            // "A guarantor is required, no amount specified" — a legitimate state. Having one at
            // all is the whole test; judging a sum nobody set would be inventing the rule.
            result.IsGuarantorSatisfied = result.GuarantorCount > 0;
        }
        else
        {
            result.IsGuarantorSatisfied = result.GuaranteedTotal >= required;
            if (!result.IsGuarantorSatisfied)
                result.GuarantorShortfall = required - result.GuaranteedTotal;
        }

        result.IsFullyCompliant = result.IsCompliant && result.IsGuarantorSatisfied;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private IQueryable<EmployeeDocument> QueryDocuments(Guid tenantId) =>
        _unitOfWork.Repository<EmployeeDocument>().GetQueryable()
            .AsNoTracking()
            .Include(d => d.DocumentType)
            .Include(d => d.Employee)
            .Include(d => d.UploadedBy)
            .Where(d => d.TenantId == tenantId && !d.IsDeleted);

    private static EmployeeDocumentDto Map(EmployeeDocument d, DateOnly today) => new()
    {
        Id = d.Id,
        EmployeeId = d.EmployeeId,
        EmployeeName = d.Employee is null ? null : $"{d.Employee.FirstName} {d.Employee.LastName}".Trim(),
        EmployeeNumber = d.Employee?.EmployeeNumber,
        DocumentTypeId = d.DocumentTypeId,
        DocumentTypeName = d.DocumentType?.Name,
        Title = d.Title,
        Description = d.Description,
        IssuedOn = d.IssuedOn,
        ExpiresOn = d.ExpiresOn,
        IsExpired = d.ExpiresOn is DateOnly e && e < today,
        DaysUntilExpiry = d.ExpiresOn is DateOnly due ? due.DayNumber - today.DayNumber : null,
        FileName = d.FileName,
        MimeType = d.MimeType,
        FileSizeBytes = d.FileSizeBytes,
        DocumentRecordId = d.DocumentRecordId,
        UploadedById = d.UploadedById,
        UploadedByName = d.UploadedBy is null
            ? null
            : $"{d.UploadedBy.FirstName} {d.UploadedBy.LastName}".Trim(),
        CreatedAt = d.CreatedAt,
    };

    private async Task<EmployeeDocumentType> RequireTypeAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        await _unitOfWork.Repository<EmployeeDocumentType>().GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, ct)
        ?? throw new ArgumentException($"Document type with ID '{id}' not found.");

    private async Task EnsureTypeNameIsFreeAsync(Guid tenantId, string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        var clash = await _unitOfWork.Repository<EmployeeDocumentType>().GetQueryable()
            .AnyAsync(t => t.TenantId == tenantId
                        && !t.IsDeleted
                        && t.Name == trimmed
                        && (exceptId == null || t.Id != exceptId), ct);

        if (clash)
            throw new InvalidOperationException($"A document type named '{trimmed}' already exists.");
    }
}
