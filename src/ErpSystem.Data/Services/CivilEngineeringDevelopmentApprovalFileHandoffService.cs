using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// Append-only CIV-0402 routing overlay. It reuses the governed approval-file service for the
/// active tenant/role/configuration gate. Routing never makes a permitting recommendation or
/// advances the shared workflow; those CIV-0403/CIV-0404 actions remain its sole owner.
/// </summary>
public sealed class CivilEngineeringDevelopmentApprovalFileHandoffService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    ICivilEngineeringDevelopmentApprovalFileService approvalFiles) : ICivilEngineeringDevelopmentApprovalFileHandoffService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringDevelopmentApprovalHandoffLookupsDto> GetLookupsAsync(Guid fileId, CancellationToken token = default)
    {
        var baseLookups = await approvalFiles.GetLookupsAsync(token); // centralized CIV-0401 active-role/configuration/DMS gate
        var file = await RequiredFileAsync(fileId, false, token);
        var roles = await ConfiguredRolesAsync(file, token);
        return new CivilEngineeringDevelopmentApprovalHandoffLookupsDto
        {
            Sections = Enum.GetValues<CivilEngineeringPermittingSection>(),
            RecipientRoles = await RecipientRolesAsync(roles, token),
            Documents = baseLookups.Documents
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringDevelopmentApprovalHandoffDto>> ListAsync(Guid fileId, CancellationToken token = default)
    {
        await approvalFiles.GetLookupsAsync(token);
        await RequiredFileAsync(fileId, false, token);
        return await MapAsync(await Handoffs(false).Where(value => value.DevelopmentApprovalFileId == fileId).OrderBy(value => value.SequenceNumber).ToListAsync(token), token);
    }

    public async Task<CivilEngineeringDevelopmentApprovalHandoffDto> CreateAsync(Guid fileId, CreateCivilEngineeringDevelopmentApprovalHandoffRequest request, string correlationId, CancellationToken token = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var file = await RequiredFileAsync(fileId, true, token);
        await approvalFiles.GetLookupsAsync(token);
        if (file.Status != CivilEngineeringDevelopmentApprovalFileStatus.SiteInspectionCompleted)
            throw Validation("Record the governed site inspection before moving the development approval file between sections.");
        var previous = await Handoffs(true).Where(value => value.DevelopmentApprovalFileId == fileId).OrderByDescending(value => value.SequenceNumber).FirstOrDefaultAsync(token);
        var fromSection = previous?.ToSection ?? CivilEngineeringPermittingSection.BuildingInspectorate;
        var requestedEvidence = request.Evidence ?? [];
        var requestHash = Hash(new { fileId, request.ToSection, request.RecipientRoleId, request.RecipientUserId, coverNote = request.CoverNote?.Trim(), dueDate = request.DueDate.Date, evidence = requestedEvidence.OrderBy(item => item.CentralDocumentVersionId).Select(item => new { item.CentralDocumentRecordId, item.CentralDocumentVersionId }) });
        var retry = await Handoffs(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (retry.DevelopmentApprovalFileId != fileId || retry.FromUserId != UserId)
                throw Conflict("This client request identifier was already used by a different development-file handoff.");
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different handoff values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([retry], token)).Single();
        }

        var now = DateTime.UtcNow;
        var errors = CivilEngineeringDevelopmentApprovalHandoffPolicy.ValidateCreate(request, fromSection, now);
        if (errors.Count > 0) throw Validation(errors);

        var configuredRoleIds = await ConfiguredRolesAsync(file, token);
        if (!await db.UserRoles.AsNoTracking().AnyAsync(value => value.UserId == UserId && configuredRoleIds.Contains(value.RoleId), token))
            throw new UnauthorizedAccessException("The current user is not assigned to a frozen CIV-CFG-009 permitting handoff role for this file.");
        if (previous is not null && previous.RecipientUserId != UserId)
            throw new UnauthorizedAccessException("Only the active recipient of the latest development-file handoff can route it onward.");
        if (!configuredRoleIds.Contains(request.RecipientRoleId)) throw Validation("Select a recipient role configured by the frozen CIV-CFG-009 permitting policy.");
        if (request.RecipientUserId == UserId) throw Validation("A development approval file cannot be handed off to the current sender.");
        if (request.DueDate.Date > file.DueDate.Date) throw Validation("The recipient handoff due date cannot be later than the development-file due date.");
        if (!await db.UserRoles.AsNoTracking().Include(value => value.User).AnyAsync(value => value.UserId == request.RecipientUserId && value.RoleId == request.RecipientRoleId && value.User.TenantId == TenantId && value.User.IsActive, token))
            throw Validation("Select an active current-tenant recipient who holds the selected configured recipient role.");
        var evidence = await RequireEvidenceAsync(file, requestedEvidence, token);
        var handoff = new ProjectCivilDevelopmentApprovalFileHandoff
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DevelopmentApprovalFileId = file.Id,
            SequenceNumber = (previous?.SequenceNumber ?? 0) + 1, ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
            FromSection = fromSection, ToSection = request.ToSection, FromUserId = UserId, RecipientRoleId = request.RecipientRoleId, RecipientUserId = request.RecipientUserId,
            CoverNote = TextOrNull(request.CoverNote, 2000), DueDate = request.DueDate.Date, CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        foreach (var document in evidence)
            handoff.Evidence.Add(new ProjectCivilDevelopmentApprovalHandoffEvidence { Id = Guid.NewGuid(), TenantId = TenantId, HandoffId = handoff.Id, CentralDocumentRecordId = document.DocumentRecordId, CentralDocumentVersionId = document.Id, CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId });
        db.ProjectCivilDevelopmentApprovalFileHandoffs.Add(handoff);
        db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = CivilEngineeringAuditEventMap.UpdateDevelopmentFileHandoff, Resource = nameof(ProjectCivilDevelopmentApprovalFileHandoff), ResourceId = handoff.Id.ToString(), OldValues = previous is null ? null : JsonSerializer.Serialize(new { previous.Id, previous.SequenceNumber, previous.ToSection, previous.RecipientRoleId, previous.RecipientUserId, previous.DueDate }, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), fileId, handoff.Id, handoff.SequenceNumber, handoff.FromSection, handoff.ToSection, handoff.RecipientRoleId, handoff.RecipientUserId, handoff.CoverNote, handoff.DueDate, evidenceCount = evidence.Count }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = now, CreatedAt = now, CreatedBy = UserName, CreatedById = UserId });
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([handoff], token)).Single();
    }

    private IQueryable<ProjectCivilDevelopmentApprovalFileHandoff> Handoffs(bool tracked) =>
        (tracked ? db.ProjectCivilDevelopmentApprovalFileHandoffs : db.ProjectCivilDevelopmentApprovalFileHandoffs.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<ProjectCivilDevelopmentApprovalFile> RequiredFileAsync(Guid id, bool tracked, CancellationToken token) =>
        await (tracked ? db.ProjectCivilDevelopmentApprovalFiles : db.ProjectCivilDevelopmentApprovalFiles.AsNoTracking()).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, token)
        ?? throw new CivilEngineeringDevelopmentApprovalFileHandoffNotFoundException("The development approval file was not found.");

    private async Task<HashSet<Guid>> ConfiguredRolesAsync(ProjectCivilDevelopmentApprovalFile file, CancellationToken token)
    {
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == file.PermittingConfigurationDecisionId && value.ProfileId == file.ConfigurationProfileId && value.ConfigurationKey == "CIV-CFG-009" && !value.IsDeleted, token)
            ?? throw Validation("The development approval file has no valid frozen CIV-CFG-009 permitting policy.");
        CivilEngineeringPermittingReviewValue policy;
        try { policy = JsonSerializer.Deserialize<CivilEngineeringPermittingReviewValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("The frozen CIV-CFG-009 permitting policy cannot be read."); }
        var roles = policy.HandoffRoleIds.Where(value => value != Guid.Empty).ToHashSet();
        if (roles.Count == 0) throw Validation("The frozen CIV-CFG-009 permitting policy has no handoff roles.");
        if (await db.Roles.AsNoTracking().CountAsync(value => roles.Contains(value.Id), token) != roles.Count) throw Validation("One or more frozen CIV-CFG-009 handoff roles no longer exist.");
        return roles;
    }

    private async Task<IReadOnlyList<CivilEngineeringPermittingRoleLookupDto>> RecipientRolesAsync(HashSet<Guid> roleIds, CancellationToken token)
    {
        var roles = await db.Roles.AsNoTracking().Where(value => roleIds.Contains(value.Id) && value.Name != null).OrderBy(value => value.Name).Select(value => new { value.Id, Name = value.Name! }).ToListAsync(token);
        var userRoles = await db.UserRoles.AsNoTracking().Include(value => value.User).Where(value => roleIds.Contains(value.RoleId) && value.User.TenantId == TenantId && value.User.IsActive)
            .Select(value => new { value.RoleId, value.UserId, value.User.FirstName, value.User.LastName, value.User.UserName }).ToListAsync(token);
        return roles.Select(role => new CivilEngineeringPermittingRoleLookupDto
        {
            RoleId = role.Id, RoleName = role.Name,
            Recipients = userRoles.Where(value => value.RoleId == role.Id).Select(value => new CivilEngineeringDevelopmentApprovalLookupOptionDto { Id = value.UserId, Label = Name(value.FirstName, value.LastName, value.UserName) }).DistinctBy(value => value.Id).OrderBy(value => value.Label).ToList()
        }).ToList();
    }

    private async Task<List<CentralDocumentVersion>> RequireEvidenceAsync(ProjectCivilDevelopmentApprovalFile file, IEnumerable<CivilEngineeringDevelopmentApprovalEvidenceRequest> supplied, CancellationToken token)
    {
        var values = new List<CentralDocumentVersion>();
        var documentDecision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == file.DocumentConfigurationDecisionId && value.ProfileId == file.ConfigurationProfileId && value.ConfigurationKey == "CIV-CFG-004" && !value.IsDeleted, token)
            ?? throw Validation("The development approval file has no valid frozen CIV-CFG-004 DMS policy.");
        CivilEngineeringDocumentPolicyValue policy;
        try { policy = JsonSerializer.Deserialize<CivilEngineeringDocumentPolicyValue>(documentDecision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("The frozen CIV-CFG-004 DMS policy cannot be read."); }
        if (policy.MetadataTemplateId != file.MetadataTemplateId || !policy.RequireVersioning) throw Conflict("The development approval file’s DMS-policy lineage is inconsistent.");
        var allowed = policy.AllowedFileExtensions.Select(NormalizeExtension).Where(value => value.Length > 1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in supplied)
        {
            var version = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == item.CentralDocumentVersionId, token)
                ?? throw Validation("Select a current Published central-DMS handoff document.");
            if (version.DocumentRecordId != item.CentralDocumentRecordId || !string.Equals(version.DocumentRecord.MetadataTemplateCode, file.MetadataTemplateCodeSnapshot, StringComparison.OrdinalIgnoreCase)) throw Validation("The selected handoff evidence does not belong to the file’s frozen Civil DMS template.");
            var extension = NormalizeExtension(Path.GetExtension(version.FileName ?? string.Empty));
            if (!allowed.Contains(extension) || version.FileSize is null or <= 0 || version.FileSize > checked((long)policy.MaximumFileSizeMb * 1024L * 1024L)) throw Validation("The selected handoff document is not allowed by the frozen CIV-CFG-004 file policy.");
            values.Add(version);
        }
        return values;
    }

    private async Task<IReadOnlyList<CivilEngineeringDevelopmentApprovalHandoffDto>> MapAsync(IReadOnlyCollection<ProjectCivilDevelopmentApprovalFileHandoff> values, CancellationToken token)
    {
        var ids = values.Select(value => value.Id).ToList();
        var userIds = values.SelectMany(value => new[] { value.FromUserId, value.RecipientUserId }).Distinct().ToList();
        var roleIds = values.Select(value => value.RecipientRoleId).Distinct().ToList();
        var evidence = await db.ProjectCivilDevelopmentApprovalHandoffEvidence.AsNoTracking().Where(value => value.TenantId == TenantId && ids.Contains(value.HandoffId) && !value.IsDeleted).ToListAsync(token);
        var recordIds = evidence.Select(value => value.CentralDocumentRecordId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => Name(value.FirstName, value.LastName, value.UserName), token);
        var roles = await db.Roles.AsNoTracking().Where(value => roleIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Name ?? "Unavailable role", token);
        var records = await db.CentralDocumentRecords.AsNoTracking().Where(value => value.TenantId == TenantId && recordIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.DocumentReference, token);
        return values.Select(value => new CivilEngineeringDevelopmentApprovalHandoffDto
        {
            Id = value.Id, SequenceNumber = value.SequenceNumber, FromSection = value.FromSection, ToSection = value.ToSection, FromUserName = users.GetValueOrDefault(value.FromUserId, "Unavailable sender"),
            RecipientRoleId = value.RecipientRoleId, RecipientRoleName = roles.GetValueOrDefault(value.RecipientRoleId, "Unavailable role"), RecipientUserId = value.RecipientUserId, RecipientUserName = users.GetValueOrDefault(value.RecipientUserId, "Unavailable recipient"), CoverNote = value.CoverNote, DueDate = value.DueDate, CreatedAt = value.CreatedAt,
            Evidence = evidence.Where(item => item.HandoffId == value.Id).Select(item => new CivilEngineeringDevelopmentApprovalEvidenceDto { Kind = CivilEngineeringDevelopmentApprovalEvidenceKind.ApplicationPackage, CentralDocumentRecordId = item.CentralDocumentRecordId, CentralDocumentVersionId = item.CentralDocumentVersionId, DocumentReference = records.GetValueOrDefault(item.CentralDocumentRecordId) }).ToList()
        }).ToList();
    }

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The development-file routing changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52240 and <= 52269) { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting development-file handoff was detected. Refresh and retry."); }
    }

    private static string? TextOrNull(string? value, int maximum) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) return null; if (normalized.Length > maximum) throw Validation("The handoff cover note cannot exceed 2,000 characters."); return normalized; }
    private static string Name(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string NormalizeExtension(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : (value.StartsWith('.') ? value : "." + value).Trim().ToLowerInvariant();
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left ?? string.Empty), Encoding.UTF8.GetBytes(right ?? string.Empty));
    private static CivilEngineeringDevelopmentApprovalFileHandoffValidationException Validation(string message) => new(message);
    private static CivilEngineeringDevelopmentApprovalFileHandoffValidationException Validation(IEnumerable<string> messages) => new(string.Join(" ", messages));
    private static CivilEngineeringDevelopmentApprovalFileHandoffConflictException Conflict(string message) => new(message);
}
