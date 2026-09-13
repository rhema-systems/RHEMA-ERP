using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class UnionService : IUnionService
{
    private readonly IUnionRepository _unionRepository;
    private readonly ICollectiveBargainingAgreementRepository _agreementRepository;
    private readonly IGenericRepository<UnionContact> _contactRepository;
    private readonly IGenericRepository<UnionDocument> _documentRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnionService> _logger;

    public UnionService(
        IUnionRepository unionRepository,
        ICollectiveBargainingAgreementRepository agreementRepository,
        IGenericRepository<UnionContact> contactRepository,
        IGenericRepository<UnionDocument> documentRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<UnionService> logger)
    {
        _unionRepository = unionRepository;
        _agreementRepository = agreementRepository;
        _contactRepository = contactRepository;
        _documentRepository = documentRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private async Task<Union> GetOwnedUnionAsync(Guid id)
    {
        var entity = await _unionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Union with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// The union with its agreements loaded — what every path that MAPS a union must use.
    /// </summary>
    /// <remarks>
    /// ⚠ This is D-6, measured rather than argued. <c>UpdateAsync</c> mapped the entity returned by
    /// <c>GetOwnedUnionAsync</c>, which fetches without the agreements, so the PUT response carried
    /// <c>agreementCount: 0</c> and an empty list for a union the GET reported two agreements for. A
    /// screen that re-renders from its own save response therefore emptied the agreements table in
    /// front of the user, and a refresh brought them back. The plain <c>GetOwnedUnionAsync</c> stays
    /// for the paths that only need to prove ownership.
    /// </remarks>
    private async Task<Union> LoadOwnedUnionWithAgreementsAsync(Guid id)
    {
        var entity = await _unionRepository.GetByIdWithAgreementsAsync(id, GetTenantId());
        if (entity == null)
            throw new ArgumentException($"Union with ID '{id}' not found.");
        return entity;
    }

    /// <summary>The agreement with its union loaded, so the mapped DTO can name it.</summary>
    /// <remarks>
    /// ⚠ The same shape as D-6, one level down. <c>AddAgreementAsync</c> gets away with the plain
    /// fetch only by accident: it loads the union first to check ownership, so EF's navigation fixup
    /// fills <c>entity.Union</c> for free. <c>UpdateAgreementAsync</c> loads no union, so its
    /// response came back with <c>unionName: null</c> while the GET beside it carried the name.
    /// </remarks>
    private async Task<CollectiveBargainingAgreement> GetOwnedAgreementAsync(Guid id)
    {
        var entity = await _agreementRepository.GetByIdWithUnionAsync(id, GetTenantId());
        if (entity == null)
            throw new ArgumentException($"Collective bargaining agreement with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// An agreement cannot expire before it takes effect.
    /// </summary>
    /// <remarks>
    /// Nothing checked this, and the register's whole job is to say which agreement is in force —
    /// a backwards pair makes that question unanswerable rather than merely wrong.
    /// </remarks>
    private static void ValidateAgreementDates(DateTime effectiveDate, DateTime? expiryDate)
    {
        if (expiryDate.HasValue && expiryDate.Value.Date < effectiveDate.Date)
            throw new InvalidOperationException(
                "The agreement's expiry date cannot be earlier than its effective date.");
    }

    // ⚠ The tenant predicate is now inside the query rather than applied to the result. All three
    // reads used to fetch every tenant's unions WITH their whole agreement graphs and discard most of
    // them in memory. Never a leak — the filter did run — but the wrong place for it.
    public async Task<IEnumerable<UnionDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _unionRepository.GetAllWithCountsAsync(GetTenantId())).ToDtoList();

    public async Task<IEnumerable<UnionDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        => (await _unionRepository.GetActiveAsync(GetTenantId())).ToDtoList();

    public async Task<UnionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => (await LoadOwnedUnionWithAgreementsAsync(id)).ToDto();

    public async Task<UnionDto> CreateAsync(CreateUnionDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        if (!string.IsNullOrWhiteSpace(createDto.Code))
        {
            // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
            var duplicate = await _unionRepository.GetQueryable()
                .AnyAsync(u => u.TenantId == tenantId && u.Code == createDto.Code, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"A union with code '{createDto.Code}' already exists.");
        }

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        // ⚠ Nothing stamps the author automatically — see AuditStampExtensions. Slice 11's audit
        // found every union row carrying a blank CreatedBy while the register next door had one on
        // every row.
        entity.StampCreated(_currentUserProvider);
        await _unionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union created: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<UnionDto> UpdateAsync(UpdateUnionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedUnionAsync(updateDto.Id);

        if (!string.IsNullOrWhiteSpace(updateDto.Code) && updateDto.Code != entity.Code)
        {
            var duplicate = await _unionRepository.GetQueryable()
                .AnyAsync(u => u.TenantId == entity.TenantId && u.Code == updateDto.Code && u.Id != updateDto.Id, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"A union with code '{updateDto.Code}' already exists.");
        }

        updateDto.UpdateEntity(entity);
        // Round 3, lane U (D-8): once a primary contact exists the trio is its mirror, whatever a
        // stale form sent.
        await MirrorPrimaryContactAsync(entity, cancellationToken);
        entity.StampUpdated(_currentUserProvider);
        await _unionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union updated: {Name}", entity.Name);

        // Re-read with the agreements so the write response says what the read says. See the remarks
        // on LoadOwnedUnionWithAgreementsAsync.
        return (await LoadOwnedUnionWithAgreementsAsync(entity.Id)).ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await LoadOwnedUnionWithAgreementsAsync(id);

        // ⚠ Deleting a union is a soft delete, and the cascade configured on the relationship only
        // fires on a hard one. So this used to leave the agreements alive and unreachable: every read
        // of them goes through the union, which no longer resolves. Refusing is the same answer
        // OrganizationUnitService gives for a unit with children, and it names the number so the
        // caller knows what to clear first.
        var agreementCount = entity.Agreements?.Count ?? 0;
        if (agreementCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete this union because it has {agreementCount} collective bargaining agreement(s). Remove them first.");
        // Round 3, lane U: the same reasoning for its files — a soft-deleted union would leave them
        // alive and unreachable.
        var documentCount = entity.Documents?.Count(d => !d.IsDeleted) ?? 0;
        if (documentCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete this union because it has {documentCount} document(s) on file. Remove them first.");

        await _unionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union deleted: {Id}", id);
        return true;
    }

    public async Task<CollectiveBargainingAgreementDto> AddAgreementAsync(CreateCollectiveBargainingAgreementDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedUnionAsync(createDto.UnionId);
        ValidateAgreementDates(createDto.EffectiveDate, createDto.ExpiryDate);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.StampCreated(_currentUserProvider);
        await _agreementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA added to union: {UnionId}", createDto.UnionId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<CollectiveBargainingAgreementDto>> GetAgreementsAsync(Guid unionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedUnionAsync(unionId);
        return (await _agreementRepository.GetByUnionIdAsync(unionId, tenantId)).ToDtoList();
    }

    public async Task<CollectiveBargainingAgreementDto> UpdateAgreementAsync(UpdateCollectiveBargainingAgreementDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAgreementAsync(updateDto.Id);
        ValidateAgreementDates(updateDto.EffectiveDate, updateDto.ExpiryDate);
        updateDto.UpdateEntity(entity);
        entity.StampUpdated(_currentUserProvider);
        await _agreementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAgreementAsync(Guid agreementId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAgreementAsync(agreementId);
        // Round 3, lane U: the signed copy is a UnionDocument naming this agreement. The delete is
        // soft, so the FK never fires; without this the file would point at an agreement no read
        // can reach.
        var documentCount = await _documentRepository.GetQueryable().AsNoTracking()
            .CountAsync(d => d.AgreementId == agreementId && !d.IsDeleted, cancellationToken);
        if (documentCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete this agreement because it has {documentCount} document(s) filed against it. Remove them first.");
        await _agreementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CBA deleted: {Id}", agreementId);
        return true;
    }

    // ── Contacts (round 3, lane U; register row U-1; decision D-8) ─────────────

    public async Task<IEnumerable<UnionContactDto>> GetContactsAsync(Guid unionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedUnionAsync(unionId);
        var rows = await _contactRepository.GetQueryable().AsNoTracking()
            .Include(c => c.Employee)
            .Where(c => c.UnionId == unionId && c.TenantId == GetTenantId() && !c.IsDeleted)
            .OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Role)
            .ToListAsync(cancellationToken);
        return rows.Select(c => c.ToContactDto());
    }

    public async Task<UnionContactDto> AddContactAsync(Guid unionId, CreateUnionContactDto dto, CancellationToken cancellationToken = default)
    {
        var union = await GetOwnedUnionAsync(unionId);
        var tenantId = GetTenantId();
        var employee = await ResolveContactPersonAsync(dto, tenantId, cancellationToken);

        var existing = await LiveContactsAsync(unionId, tenantId, cancellationToken);
        // The first contact is the primary whatever the box said; a new primary demotes the old one.
        var isPrimary = dto.IsPrimary || existing.Count == 0;
        if (isPrimary)
            foreach (var other in existing.Where(c => c.IsPrimary)) { other.IsPrimary = false; await _contactRepository.UpdateAsync(other); }

        var entity = new UnionContact
        {
            TenantId = tenantId, UnionId = unionId,
            EmployeeId = employee?.Id, ExternalName = employee is null ? dto.ExternalName!.Trim() : null,
            Email = Clean(dto.Email), Phone = Clean(dto.Phone), Role = dto.Role.Trim(), IsPrimary = isPrimary, Notes = Clean(dto.Notes),
        };
        entity.StampCreated(_currentUserProvider);
        await _contactRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity.Employee = employee;
        await MirrorPrimaryContactAsync(union, cancellationToken);
        await _unionRepository.UpdateAsync(union);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Union contact added: union {UnionId}, role {Role}", unionId, entity.Role);
        return entity.ToContactDto();
    }

    public async Task<UnionContactDto> UpdateContactAsync(UpdateUnionContactDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _contactRepository.GetQueryable()
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.Id == dto.Id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Union contact with ID '{dto.Id}' not found.");
        var union = await GetOwnedUnionAsync(entity.UnionId);
        var employee = await ResolveContactPersonAsync(dto, tenantId, cancellationToken);

        var existing = await LiveContactsAsync(entity.UnionId, tenantId, cancellationToken);
        var othersPrimary = existing.Where(c => c.Id != entity.Id && c.IsPrimary).ToList();
        if (dto.IsPrimary)
            foreach (var other in othersPrimary) { other.IsPrimary = false; await _contactRepository.UpdateAsync(other); }
        // Demoting the only primary leaves the union without one — refuse in words rather than
        // let the mirror go blank.
        if (!dto.IsPrimary && entity.IsPrimary && othersPrimary.Count == 0 && existing.Count > 1)
            throw new InvalidOperationException("Make another contact the primary first; a union with contacts always has one.");

        entity.EmployeeId = employee?.Id;
        entity.Employee = employee;
        entity.ExternalName = employee is null ? dto.ExternalName!.Trim() : null;
        entity.Email = Clean(dto.Email); entity.Phone = Clean(dto.Phone); entity.Role = dto.Role.Trim();
        entity.IsPrimary = dto.IsPrimary || existing.Count == 1;
        entity.Notes = Clean(dto.Notes);
        entity.StampUpdated(_currentUserProvider);
        await _contactRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await MirrorPrimaryContactAsync(union, cancellationToken);
        await _unionRepository.UpdateAsync(union);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToContactDto();
    }

    public async Task<bool> DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _contactRepository.GetQueryable()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.TenantId == tenantId && !c.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Union contact with ID '{contactId}' not found.");
        var union = await GetOwnedUnionAsync(entity.UnionId);
        await _contactRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The primary went: promote the next one so the mirror never goes blank while contacts remain.
        if (entity.IsPrimary)
        {
            var next = (await LiveContactsAsync(entity.UnionId, tenantId, cancellationToken)).OrderBy(c => c.CreatedAt).FirstOrDefault();
            if (next is not null) { next.IsPrimary = true; await _contactRepository.UpdateAsync(next); await _unitOfWork.SaveChangesAsync(cancellationToken); }
        }
        await MirrorPrimaryContactAsync(union, cancellationToken);
        await _unionRepository.UpdateAsync(union);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>An employee of ours (must be the tenant's, live) OR an external name; neither is refused.</summary>
    private async Task<Employee?> ResolveContactPersonAsync(CreateUnionContactDto dto, Guid tenantId, CancellationToken cancellationToken)
    {
        if (dto.EmployeeId is { } id)
        {
            var employee = await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId && !e.IsDeleted, cancellationToken);
            if (employee is null)
                throw new InvalidOperationException("That employee is not on this organisation's register.");
            return employee;
        }
        if (string.IsNullOrWhiteSpace(dto.ExternalName))
            throw new InvalidOperationException("A union contact is one of our employees or an external person — pick an employee or give the person's name.");
        return null;
    }

    private async Task<List<UnionContact>> LiveContactsAsync(Guid unionId, Guid tenantId, CancellationToken cancellationToken)
        => await _contactRepository.GetQueryable().Include(c => c.Employee)
            .Where(c => c.UnionId == unionId && c.TenantId == tenantId && !c.IsDeleted)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// D-8: the union's legacy contact trio mirrors the primary contact row while any contact
    /// exists — an employee's name, or the external name; the row's email and phone. With no
    /// contact rows the typed values stand.
    /// </summary>
    private async Task MirrorPrimaryContactAsync(Union union, CancellationToken cancellationToken)
    {
        var primary = await _contactRepository.GetQueryable().AsNoTracking().Include(c => c.Employee)
            .Where(c => c.UnionId == union.Id && c.TenantId == union.TenantId && !c.IsDeleted)
            .OrderByDescending(c => c.IsPrimary).ThenBy(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (primary is null) return;
        union.ContactPerson = primary.Employee?.FullName ?? primary.ExternalName;
        union.ContactEmail = primary.Email;
        union.ContactPhone = primary.Phone;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ── Documents (round 3, lane U; register row U-2) ─────────────────────────

    public async Task<IEnumerable<UnionDocumentDto>> GetDocumentsAsync(Guid unionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedUnionAsync(unionId);
        var rows = await _documentRepository.GetQueryable().AsNoTracking()
            .Include(d => d.UploadedBy).Include(d => d.Agreement)
            .Where(d => d.UnionId == unionId && d.TenantId == GetTenantId() && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync(cancellationToken);
        return rows.Select(d => d.ToDocumentDto());
    }

    public async Task<UnionDocumentDto> AddDocumentAsync(
        Guid unionId, Guid? agreementId, UnionDocumentKind kind, Guid uploadedById, string fileName, long? fileSize,
        string? description, CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null)
    {
        var tenantId = GetTenantId();
        await GetOwnedUnionAsync(unionId);
        if (!Enum.IsDefined(typeof(UnionDocumentKind), kind))
            throw new InvalidOperationException($"'{(int)kind}' is not a union document kind.");

        CollectiveBargainingAgreement? agreement = null;
        if (agreementId is { } aid)
        {
            agreement = await _agreementRepository.GetQueryable().AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == aid && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
            if (agreement is null || agreement.UnionId != unionId)
                throw new InvalidOperationException("That agreement does not belong to this union.");
            // A file on an agreement IS the agreement's copy, whatever kind was ticked.
            kind = UnionDocumentKind.CollectiveAgreement;
        }
        else if (kind == UnionDocumentKind.CollectiveAgreement)
        {
            throw new InvalidOperationException("A collective agreement file must name the agreement it is the copy of.");
        }

        var entity = new UnionDocument
        {
            TenantId = tenantId, UnionId = unionId, AgreementId = agreement?.Id, Kind = kind,
            FileName = fileName, FileSize = fileSize, Description = Clean(description), UploadDate = DateTime.UtcNow,
            UploadedById = uploadedById, FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId, DocumentVersionId = documentVersionId,
        };
        entity.StampCreated(_currentUserProvider);
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reread = await _documentRepository.GetQueryable().AsNoTracking()
            .Include(d => d.UploadedBy).Include(d => d.Agreement)
            .FirstAsync(d => d.Id == entity.Id, cancellationToken);
        return reread.ToDocumentDto();
    }

    public async Task<UnionDocument?> GetDocumentForDownloadAsync(Guid unionId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await _documentRepository.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UnionId == unionId && d.TenantId == tenantId && !d.IsDeleted, cancellationToken);
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _documentRepository.GetQueryable()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId && !d.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Union document with ID '{documentId}' not found.");
        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Logo (round 3, lane U) ────────────────────────────────────────────────

    public async Task<Union?> GetUnionForLogoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await _unionRepository.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);
    }

    public async Task<UnionDto> AttachLogoAsync(
        Guid id, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedUnionAsync(id);
        entity.LogoFileUploadRecordId = fileUploadRecordId;
        entity.LogoDocumentRecordId = documentRecordId;
        entity.LogoDocumentVersionId = documentVersionId;
        entity.LogoFileName = fileName;
        entity.LogoMimeType = mimeType;
        entity.LogoFileSizeBytes = fileSizeBytes;
        entity.StampUpdated(_currentUserProvider);
        await _unionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await LoadOwnedUnionWithAgreementsAsync(id)).ToDto();
    }
}
