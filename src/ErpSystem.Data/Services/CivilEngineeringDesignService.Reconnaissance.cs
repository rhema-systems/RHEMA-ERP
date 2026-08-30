using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed partial class CivilEngineeringDesignService
{
    public async Task<IReadOnlyList<CivilEngineeringReconnaissanceReportDto>> ListReconnaissanceAsync(
        Guid designCaseId,
        CancellationToken token = default)
    {
        var designCase = await RequiredAsync(designCaseId, false, token);
        await RequireProjectAsync(designCase.ProjectId);
        return (await ReconnaissanceQuery(false)
                .Where(value => value.DesignCaseId == designCaseId)
                .OrderByDescending(value => value.VisitDate)
                .ThenByDescending(value => value.CreatedAt)
                .ToListAsync(token))
            .Select(MapReconnaissance)
            .ToList();
    }

    public async Task<CivilEngineeringReconnaissanceReportDto> CreateReconnaissanceAsync(
        Guid designCaseId,
        CreateCivilEngineeringReconnaissanceRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var designCase = await RequiredAsync(designCaseId, false, token);
        await RequireProjectAsync(designCase.ProjectId);
        await RequireTransitionActorAsync(designCase, CivilEngineeringDesignActor.SupervisingCivilEngineer, token);
        var normalized = NormalizeReconnaissance(request);
        var requestHash = Hash(new
        {
            designCaseId,
            normalized.VisitDate,
            normalized.SiteLocation,
            normalized.Summary,
            normalized.SiteConditions,
            Items = normalized.Items
        });
        var retry = await ReconnaissanceQuery(false).SingleOrDefaultAsync(value =>
            value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
            if (retry.DesignCaseId != designCaseId)
                throw Conflict("The client request identifier belongs to another Civil design case.");
            await transaction.CommitAsync(token);
            return MapReconnaissance(retry);
        }
        EnsureReconnaissanceStage(designCase.Stage);
        var policy = await ResolveReconnaissancePolicyAsync(designCase.ConfigurationDecisionId, token);
        await ValidateReconnaissanceItemsAsync(
            normalized.Items,
            policy.SiteTemplate,
            policy.CrossSectionTemplate,
            false,
            token);

        var now = DateTime.UtcNow;
        var value = new ProjectCivilReconnaissanceReport
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            DesignCaseId = designCaseId,
            ClientRequestId = request.ClientRequestId,
            RequestHash = requestHash,
            ReportNumber = $"CIV-REC-{now:yyMMdd}-{request.ClientRequestId:N}"[..25].ToUpperInvariant(),
            VisitDate = normalized.VisitDate,
            SiteLocation = normalized.SiteLocation,
            Summary = normalized.Summary,
            SiteConditions = normalized.SiteConditions,
            SiteReconnaissanceTemplateId = policy.SiteTemplate.Id,
            CrossSectionTemplateId = policy.CrossSectionTemplate.Id,
            PolicyHash = policy.PolicyHash,
            PreparedByUserId = UserId,
            CorrelationId = NormalizeCorrelation(correlationId),
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        AddReconnaissanceItems(value, normalized.Items);
        AddReconnaissanceRevision(
            value,
            CivilEngineeringAuditEventMap.CreateSiteReconnaissance,
            null,
            ReconnaissanceSnapshot(value),
            null,
            correlationId);
        AddReconnaissanceAudit(
            value,
            CivilEngineeringAuditEventMap.CreateSiteReconnaissance,
            null,
            ReconnaissanceSnapshot(value),
            correlationId);
        db.ProjectCivilReconnaissanceReports.Add(value);
        try
        {
            await SaveReconnaissanceAsync(token, true);
            await transaction.CommitAsync(token);
        }
        catch (DbUpdateException exception) when (IsUniqueReconnaissanceConflict(exception))
        {
            await transaction.RollbackAsync(token);
            db.ChangeTracker.Clear();
            var concurrent = await ReconnaissanceQuery(false).SingleOrDefaultAsync(candidate =>
                candidate.ClientRequestId == request.ClientRequestId, token);
            if (concurrent is null) throw Conflict("The reconnaissance create request conflicted. Retry safely.");
            if (concurrent.DesignCaseId != designCaseId || !FixedEquals(concurrent.RequestHash, requestHash))
                throw RetryConflict();
            return MapReconnaissance(concurrent);
        }
        return MapReconnaissance(await RequiredReconnaissanceAsync(designCaseId, value.Id, false, token));
    }

    public async Task<CivilEngineeringReconnaissanceReportDto> UpdateReconnaissanceAsync(
        Guid designCaseId,
        Guid reportId,
        UpdateCivilEngineeringReconnaissanceRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "Reason");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var value = await RequiredReconnaissanceAsync(designCaseId, reportId, true, token);
        await RequireProjectAsync(value.DesignCase.ProjectId);
        await RequireTransitionActorAsync(value.DesignCase, CivilEngineeringDesignActor.SupervisingCivilEngineer, token);
        EnsureReconnaissanceStage(value.DesignCase.Stage);
        if (value.Status != CivilEngineeringReconnaissanceStatuses.Draft)
            throw Conflict("Completed reconnaissance is immutable; create a new visit report for later findings.");
        var normalized = NormalizeReconnaissance(request);
        var mutationHash = Hash(new
        {
            reportId,
            normalized.VisitDate,
            normalized.SiteLocation,
            normalized.Summary,
            normalized.SiteConditions,
            Items = normalized.Items,
            Reason = reason
        });
        if (IsReconnaissanceRetry(value, request.ClientRequestId, mutationHash))
        {
            await transaction.RollbackAsync(token);
            return MapReconnaissance(value);
        }
        CheckReconnaissanceVersion(value.RowVersion, request.RowVersion);
        var siteTemplate = await RequiredTemplateAsync(value.SiteReconnaissanceTemplateId, token);
        var crossSectionTemplate = await RequiredTemplateAsync(value.CrossSectionTemplateId, token);
        await ValidateReconnaissanceItemsAsync(
            normalized.Items,
            siteTemplate,
            crossSectionTemplate,
            false,
            token);

        var before = ReconnaissanceSnapshot(value);
        db.ProjectCivilReconnaissanceItems.RemoveRange(value.Items);
        value.Items.Clear();
        value.VisitDate = normalized.VisitDate;
        value.SiteLocation = normalized.SiteLocation;
        value.Summary = normalized.Summary;
        value.SiteConditions = normalized.SiteConditions;
        value.LastMutationClientRequestId = request.ClientRequestId;
        value.LastMutationRequestHash = mutationHash;
        value.CorrelationId = NormalizeCorrelation(correlationId);
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = UserName;
        value.LastModifiedById = UserId;
        AddReconnaissanceItems(value, normalized.Items);
        AddReconnaissanceRevision(
            value,
            CivilEngineeringAuditEventMap.UpdateSiteReconnaissance,
            before,
            ReconnaissanceSnapshot(value),
            reason,
            correlationId);
        AddReconnaissanceAudit(
            value,
            CivilEngineeringAuditEventMap.UpdateSiteReconnaissance,
            before,
            ReconnaissanceSnapshot(value),
            correlationId);
        await SaveReconnaissanceAsync(token);
        await transaction.CommitAsync(token);
        return MapReconnaissance(await RequiredReconnaissanceAsync(designCaseId, reportId, false, token));
    }

    public async Task<CivilEngineeringReconnaissanceReportDto> CompleteReconnaissanceAsync(
        Guid designCaseId,
        Guid reportId,
        CompleteCivilEngineeringReconnaissanceRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "Completion reason");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var value = await RequiredReconnaissanceAsync(designCaseId, reportId, true, token);
        await RequireProjectAsync(value.DesignCase.ProjectId);
        await RequireTransitionActorAsync(value.DesignCase, CivilEngineeringDesignActor.SupervisingCivilEngineer, token);
        var mutationHash = Hash(new { reportId, Action = "Complete", Reason = reason });
        if (IsReconnaissanceRetry(value, request.ClientRequestId, mutationHash))
        {
            await transaction.RollbackAsync(token);
            return MapReconnaissance(value);
        }
        EnsureReconnaissanceStage(value.DesignCase.Stage);
        CheckReconnaissanceVersion(value.RowVersion, request.RowVersion);
        if (value.Status != CivilEngineeringReconnaissanceStatuses.Draft)
            throw Conflict("The reconnaissance report is already completed.");

        var siteTemplate = await RequiredTemplateAsync(value.SiteReconnaissanceTemplateId, token);
        var crossSectionTemplate = await RequiredTemplateAsync(value.CrossSectionTemplateId, token);
        var currentItems = value.Items.OrderBy(item => item.DisplayOrder).Select(ToRequest).ToList();
        await ValidateReconnaissanceItemsAsync(
            currentItems,
            siteTemplate,
            crossSectionTemplate,
            true,
            token);
        var before = ReconnaissanceSnapshot(value);
        value.Status = CivilEngineeringReconnaissanceStatuses.Completed;
        value.CompletedByUserId = UserId;
        value.CompletedAt = DateTime.UtcNow;
        value.LastMutationClientRequestId = request.ClientRequestId;
        value.LastMutationRequestHash = mutationHash;
        value.CorrelationId = NormalizeCorrelation(correlationId);
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = UserName;
        value.LastModifiedById = UserId;
        AddReconnaissanceRevision(
            value,
            CivilEngineeringAuditEventMap.UpdateSiteReconnaissance,
            before,
            ReconnaissanceSnapshot(value),
            reason,
            correlationId);
        AddReconnaissanceAudit(
            value,
            CivilEngineeringAuditEventMap.UpdateSiteReconnaissance,
            before,
            ReconnaissanceSnapshot(value),
            correlationId);
        await SaveReconnaissanceAsync(token);
        await transaction.CommitAsync(token);
        return MapReconnaissance(await RequiredReconnaissanceAsync(designCaseId, reportId, false, token));
    }

    private IQueryable<ProjectCivilReconnaissanceReport> ReconnaissanceQuery(bool tracked)
    {
        var query = db.ProjectCivilReconnaissanceReports
            .Include(value => value.DesignCase)
            .Include(value => value.Items.Where(item => !item.IsDeleted))
                .ThenInclude(item => item.InformationSourceSection)
                    .ThenInclude(section => section!.Department)
            .Include(value => value.Items.Where(item => !item.IsDeleted))
                .ThenInclude(item => item.CentralDocumentRecord)
            .Include(value => value.Items.Where(item => !item.IsDeleted))
                .ThenInclude(item => item.CentralDocumentVersion)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<ProjectCivilReconnaissanceReport> RequiredReconnaissanceAsync(
        Guid designCaseId,
        Guid reportId,
        bool tracked,
        CancellationToken token) =>
        await ReconnaissanceQuery(tracked).SingleOrDefaultAsync(value =>
            value.Id == reportId && value.DesignCaseId == designCaseId, token)
        ?? throw new CivilEngineeringDesignNotFoundException("The site reconnaissance report was not found.");

    private static void EnsureReconnaissanceStage(string stage)
    {
        try { CivilEngineeringReconnaissanceRules.EnsureMutableStage(stage); }
        catch (InvalidOperationException exception) { throw Validation(exception.Message); }
    }

    private static ReconnaissanceInput NormalizeReconnaissance(
        CreateCivilEngineeringReconnaissanceRequest request)
    {
        var visitDate = request.VisitDate.Kind == DateTimeKind.Utc
            ? request.VisitDate
            : request.VisitDate.ToUniversalTime();
        if (request.VisitDate == default || visitDate > DateTime.UtcNow.AddDays(1))
            throw Validation("Select a valid site visit date that is not in the future.");
        var items = request.Items.OrderBy(item => item.DisplayOrder).Select((item, index) =>
            new CivilEngineeringReconnaissanceItemRequest
            {
                Kind = item.Kind,
                Description = RequiredText(item.Description, 3, 2000, "Item description"),
                InformationSourceSectionId = item.InformationSourceSectionId,
                CentralDocumentRecordId = item.CentralDocumentRecordId,
                CentralDocumentVersionId = item.CentralDocumentVersionId,
                ConstraintCategory = item.ConstraintCategory,
                Severity = item.Severity,
                ResolutionStatus = item.ResolutionStatus,
                BlocksDesign = item.BlocksDesign,
                DisplayOrder = index
            }).ToList();
        return new ReconnaissanceInput(
            visitDate,
            RequiredText(request.SiteLocation, 3, 500, "Site location"),
            RequiredText(request.Summary, 10, 4000, "Summary"),
            RequiredText(request.SiteConditions, 5, 2000, "Site conditions"),
            items);
    }

    private async Task<ReconnaissancePolicy> ResolveReconnaissancePolicyAsync(
        Guid decisionId,
        CancellationToken token)
    {
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == decisionId
                                           && value.ConfigurationKey == "CIV-CFG-003"
                                           && !value.IsDeleted, token)
            ?? throw Validation("The frozen CIV-CFG-003 design policy is not available.");
        CivilEngineeringDesignReviewValue configured;
        try
        {
            configured = JsonSerializer.Deserialize<CivilEngineeringDesignReviewValue>(decision.ValueJson, JsonOptions)
                         ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw Conflict("The frozen CIV-CFG-003 policy contains invalid reconnaissance configuration.");
        }
        var siteTemplate = await RequiredTemplateAsync(configured.SiteReconnaissanceTemplateId, token);
        var crossSectionTemplate = await RequiredTemplateAsync(configured.CrossSectionTemplateId, token);
        return new ReconnaissancePolicy(
            siteTemplate,
            crossSectionTemplate,
            Hash(new
            {
                Decision = decision.Id,
                decision.ValueJson,
                SiteTemplate = siteTemplate.Id,
                CrossSectionTemplate = crossSectionTemplate.Id
            }));
    }

    private async Task<CentralDocumentMetadataTemplate> RequiredTemplateAsync(
        Guid id,
        CancellationToken token) =>
        await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == id && value.IsActive
            && value.PublishedAt.HasValue && !value.IsDeleted, token)
        ?? throw Validation("The configured reconnaissance DMS template is not active and Published.");

    private async Task ValidateReconnaissanceItemsAsync(
        IReadOnlyList<CivilEngineeringReconnaissanceItemRequest> input,
        CentralDocumentMetadataTemplate siteTemplate,
        CentralDocumentMetadataTemplate crossSectionTemplate,
        bool completing,
        CancellationToken token)
    {
        var facts = input.Select(ToFacts).ToList();
        try { CivilEngineeringReconnaissanceRules.EnsureValidItems(facts, completing); }
        catch (InvalidOperationException exception) { throw Validation(exception.Message); }
        if (input.GroupBy(item => new
            {
                item.Kind,
                item.InformationSourceSectionId,
                item.CentralDocumentVersionId,
                Description = item.Description.Trim().ToUpperInvariant()
            }).Any(group => group.Count() > 1))
            throw Validation("Duplicate reconnaissance items are not allowed.");

        var sectionIds = input.Where(item => item.Kind == CivilEngineeringReconnaissanceItemKind.InformationSource)
            .Select(item => item.InformationSourceSectionId!.Value).Distinct().ToList();
        var validSectionCount = await db.Sections.AsNoTracking().CountAsync(value =>
            sectionIds.Contains(value.Id) && value.TenantId == TenantId && value.IsActive && !value.IsDeleted
            && value.Department.TenantId == TenantId && value.Department.IsActive && !value.Department.IsDeleted, token);
        if (validSectionCount != sectionIds.Count)
            throw Validation("One or more information-source sections are inactive or outside the active tenant.");

        var versionIds = input.Where(item => item.CentralDocumentVersionId.HasValue)
            .Select(item => item.CentralDocumentVersionId!.Value).Distinct().ToList();
        var versions = await db.CentralDocumentVersions.AsNoTracking()
            .Include(value => value.DocumentRecord)
            .Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId && versionIds.Contains(value.Id))
            .ToListAsync(token);
        if (versions.Count != versionIds.Count)
            throw Validation("One or more reconnaissance documents are not current, Published, or tenant-owned.");
        foreach (var item in input.Where(value => value.CentralDocumentVersionId.HasValue))
        {
            var version = versions.Single(value => value.Id == item.CentralDocumentVersionId);
            if (version.DocumentRecordId != item.CentralDocumentRecordId)
                throw Validation("A reconnaissance document version does not belong to the selected DMS record.");
            var expectedTemplate = item.Kind == CivilEngineeringReconnaissanceItemKind.Photo
                ? siteTemplate.TemplateCode
                : crossSectionTemplate.TemplateCode;
            if (!string.Equals(version.DocumentRecord.MetadataTemplateCode, expectedTemplate, StringComparison.OrdinalIgnoreCase))
                throw Validation($"The selected {item.Kind} document must use DMS template {expectedTemplate}.");
        }
    }

    private void AddReconnaissanceItems(
        ProjectCivilReconnaissanceReport report,
        IReadOnlyList<CivilEngineeringReconnaissanceItemRequest> input)
    {
        foreach (var item in input)
        {
            report.Items.Add(new ProjectCivilReconnaissanceItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Kind = item.Kind,
                Description = item.Description,
                InformationSourceSectionId = item.InformationSourceSectionId,
                CentralDocumentRecordId = item.CentralDocumentRecordId,
                CentralDocumentVersionId = item.CentralDocumentVersionId,
                ConstraintCategory = item.ConstraintCategory,
                Severity = item.Severity,
                ResolutionStatus = item.ResolutionStatus,
                BlocksDesign = item.BlocksDesign,
                DisplayOrder = item.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = UserId
            });
        }
    }

    private void AddReconnaissanceRevision(
        ProjectCivilReconnaissanceReport report,
        string action,
        object? before,
        object after,
        string? reason,
        string correlationId) => report.Revisions.Add(new ProjectCivilReconnaissanceRevision
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        Action = action,
        ActorUserId = UserId,
        ActorName = UserName,
        ActorRoles = ActorRoles,
        CorrelationId = NormalizeCorrelation(correlationId),
        Reason = reason,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        AfterJson = JsonSerializer.Serialize(after, JsonOptions),
        CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName,
        CreatedById = UserId
    });

    private void AddReconnaissanceAudit(
        ProjectCivilReconnaissanceReport report,
        string action,
        object? before,
        object after,
        string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId,
        UserId = UserId,
        Username = UserName,
        Action = action,
        Resource = nameof(ProjectCivilReconnaissanceReport),
        ResourceId = report.Id.ToString(),
        OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        NewValues = JsonSerializer.Serialize(new
        {
            correlationId = NormalizeCorrelation(correlationId),
            value = after
        }, JsonOptions),
        IpAddress = currentUser.IpAddress,
        UserAgent = currentUser.UserAgent,
        Timestamp = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName,
        CreatedById = UserId
    });

    private static object ReconnaissanceSnapshot(ProjectCivilReconnaissanceReport value) => new
    {
        value.Id,
        value.DesignCaseId,
        value.ReportNumber,
        value.VisitDate,
        value.SiteLocation,
        value.Summary,
        value.SiteConditions,
        value.Status,
        value.SiteReconnaissanceTemplateId,
        value.CrossSectionTemplateId,
        value.PolicyHash,
        value.PreparedByUserId,
        value.CompletedByUserId,
        value.CompletedAt,
        Items = value.Items.Where(item => !item.IsDeleted).OrderBy(item => item.DisplayOrder).Select(item => new
        {
            item.Kind,
            item.Description,
            item.InformationSourceSectionId,
            item.CentralDocumentRecordId,
            item.CentralDocumentVersionId,
            item.ConstraintCategory,
            item.Severity,
            item.ResolutionStatus,
            item.BlocksDesign,
            item.DisplayOrder
        })
    };

    private static CivilEngineeringReconnaissanceReportDto MapReconnaissance(
        ProjectCivilReconnaissanceReport value) => new()
    {
        Id = value.Id,
        DesignCaseId = value.DesignCaseId,
        ReportNumber = value.ReportNumber,
        VisitDate = value.VisitDate,
        SiteLocation = value.SiteLocation,
        Summary = value.Summary,
        SiteConditions = value.SiteConditions,
        Status = value.Status,
        SiteReconnaissanceTemplateId = value.SiteReconnaissanceTemplateId,
        CrossSectionTemplateId = value.CrossSectionTemplateId,
        PreparedByUserId = value.PreparedByUserId,
        CompletedByUserId = value.CompletedByUserId,
        CompletedAt = value.CompletedAt,
        RowVersion = Convert.ToBase64String(value.RowVersion),
        Items = value.Items.Where(item => !item.IsDeleted).OrderBy(item => item.DisplayOrder).Select(item =>
            new CivilEngineeringReconnaissanceItemDto
            {
                Id = item.Id,
                Kind = item.Kind,
                Description = item.Description,
                InformationSourceSectionId = item.InformationSourceSectionId,
                InformationSourceSectionLabel = item.InformationSourceSection is null
                    ? null
                    : item.InformationSourceSection.Department.Name + " / " + item.InformationSourceSection.Name,
                CentralDocumentRecordId = item.CentralDocumentRecordId,
                CentralDocumentVersionId = item.CentralDocumentVersionId,
                DocumentReference = item.CentralDocumentRecord?.DocumentReference,
                DocumentTitle = item.CentralDocumentRecord?.Title,
                VersionNumber = item.CentralDocumentVersion?.VersionNumber,
                ConstraintCategory = item.ConstraintCategory,
                Severity = item.Severity,
                ResolutionStatus = item.ResolutionStatus,
                BlocksDesign = item.BlocksDesign,
                DisplayOrder = item.DisplayOrder
            }).ToList()
    };

    private static CivilEngineeringReconnaissanceItemFacts ToFacts(
        CivilEngineeringReconnaissanceItemRequest item) => new(
        item.Kind,
        item.InformationSourceSectionId,
        item.CentralDocumentRecordId,
        item.CentralDocumentVersionId,
        item.ConstraintCategory,
        item.Severity,
        item.ResolutionStatus,
        item.BlocksDesign);

    private static CivilEngineeringReconnaissanceItemRequest ToRequest(
        ProjectCivilReconnaissanceItem item) => new()
    {
        Kind = item.Kind,
        Description = item.Description,
        InformationSourceSectionId = item.InformationSourceSectionId,
        CentralDocumentRecordId = item.CentralDocumentRecordId,
        CentralDocumentVersionId = item.CentralDocumentVersionId,
        ConstraintCategory = item.ConstraintCategory,
        Severity = item.Severity,
        ResolutionStatus = item.ResolutionStatus,
        BlocksDesign = item.BlocksDesign,
        DisplayOrder = item.DisplayOrder
    };

    private static bool IsReconnaissanceRetry(
        ProjectCivilReconnaissanceReport value,
        Guid clientRequestId,
        string requestHash)
    {
        if (value.LastMutationClientRequestId != clientRequestId) return false;
        if (!FixedEquals(value.LastMutationRequestHash, requestHash)) throw RetryConflict();
        return true;
    }

    private static void CheckReconnaissanceVersion(byte[] current, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Conflict("The reconnaissance row version is invalid. Refresh and retry."); }
        if (!CryptographicOperations.FixedTimeEquals(current, expected))
            throw Conflict("The reconnaissance report changed. Refresh and retry.");
    }

    private async Task SaveReconnaissanceAsync(CancellationToken token, bool rethrowUnique = false)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException)
        { throw Conflict("The reconnaissance report changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sqlException
                                                  && sqlException.Number is >= 51910 and <= 51929)
        { throw Conflict(sqlException.Message); }
        catch (DbUpdateException exception) when (rethrowUnique && IsUniqueReconnaissanceConflict(exception))
        { throw; }
        catch (DbUpdateException exception) when (IsUniqueReconnaissanceConflict(exception))
        { throw Conflict("A reconnaissance report already uses this request or reference."); }
    }

    private static bool IsUniqueReconnaissanceConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 }
        || exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
        || exception.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;

    private sealed record ReconnaissanceInput(
        DateTime VisitDate,
        string SiteLocation,
        string Summary,
        string SiteConditions,
        IReadOnlyList<CivilEngineeringReconnaissanceItemRequest> Items);

    private sealed record ReconnaissancePolicy(
        CentralDocumentMetadataTemplate SiteTemplate,
        CentralDocumentMetadataTemplate CrossSectionTemplate,
        string PolicyHash);
}
