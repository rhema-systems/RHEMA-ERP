using System.Data;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed partial class CivilEngineeringDesignService
{
    public async Task<CivilEngineeringPlanningGisLookupsDto> GetPlanningGisLookupsAsync(
        Guid designCaseId,
        CancellationToken token = default)
    {
        var designCase = await RequiredAsync(designCaseId, false, token);
        await RequireProjectAsync(designCase.ProjectId);
        await RequirePlanningGisProjectMembershipAsync(designCase.ProjectId, token);
        if (!designCase.EstateManagedAssetId.HasValue)
            throw Validation("The Engineering Case has no controlled property or site for Planning/GIS validation.");

        var site = await RequirePlanningGisEstateAssetAsync(designCase.EstateManagedAssetId.Value, token);
        var files = await db.ProjectCivilDevelopmentApprovalFiles.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.ProjectId == designCase.ProjectId
                           && item.EstateManagedAssetId == site.Id && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new CivilEngineeringPlanningGisLookupOptionDto
            {
                Id = item.Id,
                Label = item.FileNumber + " - " + item.ApplicationReference
            })
            .ToListAsync(token);

        var approvalFileIds = files.Select(item => item.Id).ToList();
        var evidence = await db.ProjectCivilDevelopmentApprovalEvidence.AsNoTracking()
            .Where(item => item.TenantId == TenantId && approvalFileIds.Contains(item.DevelopmentApprovalFileId) && !item.IsDeleted)
            .Join(db.CentralDocumentVersions.AsNoTracking()
                    .Include(item => item.DocumentRecord)
                    .Where(CentralDocumentEvidenceRules.CurrentPublished()),
                approvalEvidence => approvalEvidence.CentralDocumentVersionId,
                version => version.Id,
                (approvalEvidence, version) => new { approvalEvidence, version })
            .Where(item => item.approvalEvidence.CentralDocumentRecordId == item.version.DocumentRecordId
                           && item.version.TenantId == TenantId
                           && item.version.DocumentRecord.MetadataTemplateCode == "TDC-CIV-ENGINEERING-FILE")
            .OrderByDescending(item => item.version.PublishedAt ?? item.version.CreatedAt)
            .Take(250)
            .Select(item => new CivilEngineeringPlanningGisDocumentLookupDto
            {
                DevelopmentApprovalFileId = item.approvalEvidence.DevelopmentApprovalFileId,
                CentralDocumentRecordId = item.version.DocumentRecordId,
                CentralDocumentVersionId = item.version.Id,
                Label = item.version.DocumentRecord.DocumentReference + " - " + item.version.DocumentRecord.Title
            })
            .ToListAsync(token);

        return new CivilEngineeringPlanningGisLookupsDto
        {
            EstateManagedAssetId = site.Id,
            EstateManagedAssetLabel = site.AssetCode + " - " + site.Name,
            DevelopmentApprovalFiles = files,
            PlanningConditions = await PlanningGisCatalogAsync(ProjectCatalogDefaults.CivilPlanningConditions, token),
            DevelopmentConstraints = await PlanningGisCatalogAsync(ProjectCatalogDefaults.CivilDevelopmentConstraints, token),
            LandUseImpacts = await PlanningGisCatalogAsync(ProjectCatalogDefaults.CivilLandUseImpacts, token),
            EvidenceDocuments = evidence,
            LayoutConformities = Enum.GetValues<CivilEngineeringLayoutConformity>()
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringPlanningGisValidationDto>> ListPlanningGisValidationsAsync(
        Guid designCaseId,
        CancellationToken token = default)
    {
        var designCase = await RequiredAsync(designCaseId, false, token);
        await RequireProjectAsync(designCase.ProjectId);
        await RequirePlanningGisProjectMembershipAsync(designCase.ProjectId, token);
        return (await PlanningGisValidations(false)
                .Where(item => item.DesignCaseId == designCaseId)
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(token))
            .Select(MapPlanningGisValidation)
            .ToList();
    }

    public async Task<CivilEngineeringPlanningGisValidationDto> CreatePlanningGisValidationAsync(
        Guid designCaseId,
        CreateCivilEngineeringPlanningGisValidationRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");
        if (request.EstateManagedAssetId == Guid.Empty || request.DevelopmentApprovalFileId == Guid.Empty
            || request.PlanningConditionId == Guid.Empty || request.DevelopmentConstraintId == Guid.Empty
            || request.LandUseImpactId == Guid.Empty || request.CentralDocumentRecordId == Guid.Empty
            || request.CentralDocumentVersionId == Guid.Empty)
            throw Validation("Select the governed site, development file, planning condition, constraint, land-use impact and current DMS evidence.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var designCase = await RequiredAsync(designCaseId, true, token);
        await RequireProjectAsync(designCase.ProjectId);
        await RequirePlanningGisProjectMembershipAsync(designCase.ProjectId, token);
        if (designCase.EstateManagedAssetId != request.EstateManagedAssetId)
            throw Validation("Select the controlled property or site already linked to this Engineering Case.");

        var requestHash = Hash(new
        {
            designCaseId,
            request.EstateManagedAssetId,
            request.DevelopmentApprovalFileId,
            request.PlanningConditionId,
            request.DevelopmentConstraintId,
            request.LandUseImpactId,
            request.LayoutConformity,
            request.CentralDocumentRecordId,
            request.CentralDocumentVersionId
        });
        var retry = await PlanningGisValidations(true).SingleOrDefaultAsync(item => item.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (retry.DesignCaseId != designCaseId || retry.PreparedByUserId != UserId || !FixedEquals(retry.RequestHash, requestHash))
                throw RetryConflict();
            await transaction.CommitAsync(token);
            return MapPlanningGisValidation(retry);
        }
        if (await PlanningGisValidations(false).AnyAsync(item => item.DesignCaseId == designCaseId
                                                               && (item.Status == CivilEngineeringPlanningGisValidationStatus.Draft
                                                                   || item.Status == CivilEngineeringPlanningGisValidationStatus.Submitted), token))
            throw Conflict("This Engineering Case already has an open Planning/GIS validation.");

        var site = await RequirePlanningGisEstateAssetAsync(request.EstateManagedAssetId, token);
        var approvalFile = await db.ProjectCivilDevelopmentApprovalFiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == request.DevelopmentApprovalFileId
                                          && item.ProjectId == designCase.ProjectId
                                          && item.EstateManagedAssetId == site.Id && !item.IsDeleted, token)
            ?? throw Validation("Select a current development-approval file for the Engineering Case project and property/site.");
        await RequireApprovalFileEvidenceAsync(approvalFile.Id, token);
        await RequirePlanningGisCatalogEntryAsync(request.PlanningConditionId, ProjectCatalogDefaults.CivilPlanningConditions, token);
        await RequirePlanningGisCatalogEntryAsync(request.DevelopmentConstraintId, ProjectCatalogDefaults.CivilDevelopmentConstraints, token);
        await RequirePlanningGisCatalogEntryAsync(request.LandUseImpactId, ProjectCatalogDefaults.CivilLandUseImpacts, token);
        var evidence = await RequirePlanningGisEvidenceAsync(
            approvalFile.Id,
            request.CentralDocumentRecordId,
            request.CentralDocumentVersionId,
            token);
        var now = DateTime.UtcNow;
        var value = new ProjectCivilPlanningGisValidation
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            DesignCaseId = designCase.Id,
            EstateManagedAssetId = site.Id,
            DevelopmentApprovalFileId = approvalFile.Id,
            ClientRequestId = request.ClientRequestId,
            RequestHash = requestHash,
            PlanningConditionId = request.PlanningConditionId,
            DevelopmentConstraintId = request.DevelopmentConstraintId,
            LandUseImpactId = request.LandUseImpactId,
            LayoutConformity = request.LayoutConformity,
            SpatialReferenceSnapshot = PlanningGisSpatialReference(site),
            GisProviderSnapshot = TrimOrNull(site.GisProvider, 200),
            GisFeatureIdSnapshot = TrimOrNull(site.GisFeatureId, 200),
            GisSourceCrsSnapshot = TrimOrNull(site.GisSourceCrs, 120),
            BoundaryCoordinatesSnapshot = TrimOrNull(site.BoundaryCoordinates, 4000),
            ZoningClassificationSnapshot = TrimOrNull(site.ZoningClassification, 120),
            PlanningComplianceSnapshot = TrimOrNull(site.PlanningComplianceStatus, 120),
            CentralDocumentRecordId = evidence.DocumentRecordId,
            CentralDocumentVersionId = evidence.Id,
            Status = CivilEngineeringPlanningGisValidationStatus.Draft,
            PreparedByUserId = UserId,
            CorrelationId = NormalizeCorrelation(correlationId),
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        db.ProjectCivilPlanningGisValidations.Add(value);
        AddPlanningGisRevision(value, CivilEngineeringAuditEventMap.CreatePlanningGisValidation, null, PlanningGisSnapshot(value), correlationId);
        AddPlanningGisAudit(value, CivilEngineeringAuditEventMap.CreatePlanningGisValidation, null, PlanningGisSnapshot(value), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return MapPlanningGisValidation(await RequiredPlanningGisValidationAsync(value.Id, false, token));
    }

    public async Task<CivilEngineeringPlanningGisValidationDto> SubmitPlanningGisValidationAsync(
        Guid validationId,
        CivilEngineeringPlanningGisSubmitRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var value = await RequiredPlanningGisValidationAsync(validationId, true, token);
        await RequireProjectAsync(value.DesignCase.ProjectId);
        await RequirePlanningGisProjectMembershipAsync(value.DesignCase.ProjectId, token);
        var mutationHash = Hash(new { Action = "Submit", validationId });
        if (IsPlanningGisMutationRetry(value, request.ClientRequestId, mutationHash))
        {
            await transaction.RollbackAsync(token);
            return MapPlanningGisValidation(value);
        }
        CheckVersion(value.RowVersion, request.RowVersion);
        if (value.PreparedByUserId != UserId)
            throw new UnauthorizedAccessException("Only the Planning/GIS preparer can submit this validation for independent review.");
        if (value.Status != CivilEngineeringPlanningGisValidationStatus.Draft)
            throw Conflict("Only a draft Planning/GIS validation can be submitted.");
        var before = PlanningGisSnapshot(value);
        value.Status = CivilEngineeringPlanningGisValidationStatus.Submitted;
        value.SubmittedAt = DateTime.UtcNow;
        value.SubmittedByUserId = UserId;
        value.LastMutationClientRequestId = request.ClientRequestId;
        value.LastMutationRequestHash = mutationHash;
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = UserName;
        value.LastModifiedById = UserId;
        AddPlanningGisRevision(value, CivilEngineeringAuditEventMap.SubmitPlanningGisValidation, before, PlanningGisSnapshot(value), correlationId);
        AddPlanningGisAudit(value, CivilEngineeringAuditEventMap.SubmitPlanningGisValidation, before, PlanningGisSnapshot(value), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return MapPlanningGisValidation(await RequiredPlanningGisValidationAsync(value.Id, false, token));
    }

    public async Task<CivilEngineeringPlanningGisValidationDto> DecidePlanningGisValidationAsync(
        Guid validationId,
        CivilEngineeringPlanningGisDecisionRequest request,
        string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");
        if (request.Outcome is not (CivilEngineeringPlanningGisValidationStatus.Approved or CivilEngineeringPlanningGisValidationStatus.Rejected))
            throw Validation("Select Approved or Rejected as the independent Planning/GIS review outcome.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var value = await RequiredPlanningGisValidationAsync(validationId, true, token);
        await RequireProjectAsync(value.DesignCase.ProjectId);
        await RequirePlanningGisProjectMembershipAsync(value.DesignCase.ProjectId, token);
        var mutationHash = Hash(new { Action = "Decide", validationId, request.Outcome });
        if (IsPlanningGisMutationRetry(value, request.ClientRequestId, mutationHash))
        {
            await transaction.RollbackAsync(token);
            return MapPlanningGisValidation(value);
        }
        CheckVersion(value.RowVersion, request.RowVersion);
        if (value.PreparedByUserId == UserId)
            throw new UnauthorizedAccessException("The Planning/GIS preparer cannot approve or reject the same validation.");
        if (value.Status != CivilEngineeringPlanningGisValidationStatus.Submitted)
            throw Conflict("Only a submitted Planning/GIS validation can be independently decided.");
        var before = PlanningGisSnapshot(value);
        value.Status = request.Outcome;
        value.ReviewedAt = DateTime.UtcNow;
        value.ReviewedByUserId = UserId;
        value.LastMutationClientRequestId = request.ClientRequestId;
        value.LastMutationRequestHash = mutationHash;
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = UserName;
        value.LastModifiedById = UserId;
        var action = request.Outcome == CivilEngineeringPlanningGisValidationStatus.Approved
            ? CivilEngineeringAuditEventMap.ApprovePlanningGisValidation
            : CivilEngineeringAuditEventMap.RejectPlanningGisValidation;
        AddPlanningGisRevision(value, action, before, PlanningGisSnapshot(value), correlationId);
        AddPlanningGisAudit(value, action, before, PlanningGisSnapshot(value), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return MapPlanningGisValidation(await RequiredPlanningGisValidationAsync(value.Id, false, token));
    }

    public async Task<IReadOnlyList<CivilEngineeringPlanningGisValidationRevisionDto>> PlanningGisHistoryAsync(
        Guid validationId,
        CancellationToken token = default)
    {
        var value = await RequiredPlanningGisValidationAsync(validationId, false, token);
        await RequireProjectAsync(value.DesignCase.ProjectId);
        await RequirePlanningGisProjectMembershipAsync(value.DesignCase.ProjectId, token);
        return await db.ProjectCivilPlanningGisValidationRevisions.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.PlanningGisValidationId == validationId && !item.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new CivilEngineeringPlanningGisValidationRevisionDto
            {
                Id = item.Id,
                Action = item.Action,
                ActorUserId = item.ActorUserId,
                ActorName = item.ActorName,
                ActorRoles = item.ActorRoles,
                CorrelationId = item.CorrelationId,
                Timestamp = item.CreatedAt
            })
            .ToListAsync(token);
    }

    private IQueryable<ProjectCivilPlanningGisValidation> PlanningGisValidations(bool tracked)
    {
        var query = (tracked ? db.ProjectCivilPlanningGisValidations : db.ProjectCivilPlanningGisValidations.AsNoTracking())
            .Include(item => item.DesignCase)
            .Include(item => item.EstateManagedAsset)
            .Include(item => item.DevelopmentApprovalFile)
            .Include(item => item.PlanningCondition)
            .Include(item => item.DevelopmentConstraint)
            .Include(item => item.LandUseImpact)
            .Include(item => item.CentralDocumentRecord)
            .Where(item => item.TenantId == TenantId && !item.IsDeleted);
        return query;
    }

    private async Task<ProjectCivilPlanningGisValidation> RequiredPlanningGisValidationAsync(Guid id, bool tracked, CancellationToken token) =>
        await PlanningGisValidations(tracked).SingleOrDefaultAsync(item => item.Id == id, token)
        ?? throw new CivilEngineeringDesignNotFoundException("The Planning/GIS validation was not found.");

    private async Task RequirePlanningGisProjectMembershipAsync(Guid projectId, CancellationToken token)
    {
        if (!await db.ProjectMembers.AsNoTracking().AnyAsync(item => item.TenantId == TenantId
                                                                  && item.ProjectId == projectId
                                                                  && item.UserId == UserId
                                                                  && item.IsActive && !item.IsDeleted, token))
            throw new UnauthorizedAccessException("You are not an active member of the Engineering Case project.");
    }

    private async Task<EstateManagedAsset> RequirePlanningGisEstateAssetAsync(Guid id, CancellationToken token)
    {
        var value = await db.EstateManagedAssets.AsNoTracking().SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == id && !item.IsDeleted,
            token) ?? throw Validation("The selected property or site is not available in this tenant.");
        if (!value.BoundaryVerified || string.IsNullOrWhiteSpace(PlanningGisSpatialReferenceOrNull(value)))
            throw Validation("The selected property or site must have a verified governed GIS, geometry, or boundary reference before Planning/GIS review.");
        return value;
    }

    private async Task RequireApprovalFileEvidenceAsync(Guid fileId, CancellationToken token)
    {
        var currentEvidence = await db.ProjectCivilDevelopmentApprovalEvidence.AsNoTracking()
            .Join(db.CentralDocumentVersions.AsNoTracking().Include(item => item.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()),
                evidence => evidence.CentralDocumentVersionId,
                version => version.Id,
                (evidence, _) => evidence)
            .AnyAsync(item => item.TenantId == TenantId && item.DevelopmentApprovalFileId == fileId && !item.IsDeleted, token);
        if (!currentEvidence)
            throw Validation("The selected development-approval file has no current Published central-DMS evidence.");
    }

    private async Task RequirePlanningGisCatalogEntryAsync(Guid id, string catalogType, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        if (!await db.ProjectCatalogEntries.AsNoTracking().AnyAsync(item => item.TenantId == TenantId
                && item.Id == id && item.CatalogType == catalogType && item.IsActive && !item.IsDeleted
                && (!item.EffectiveFrom.HasValue || item.EffectiveFrom <= now)
                && (!item.EffectiveTo.HasValue || item.EffectiveTo >= now), token))
            throw Validation("Select an active value from the governed Planning/GIS catalogue.");
    }

    private async Task<CentralDocumentVersion> RequirePlanningGisEvidenceAsync(
        Guid approvalFileId,
        Guid recordId,
        Guid versionId,
        CancellationToken token)
    {
        var selected = await db.ProjectCivilDevelopmentApprovalEvidence.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.DevelopmentApprovalFileId == approvalFileId
                           && item.CentralDocumentRecordId == recordId && item.CentralDocumentVersionId == versionId
                           && !item.IsDeleted)
            .Join(db.CentralDocumentVersions.AsNoTracking()
                    .Include(item => item.DocumentRecord)
                    .Where(CentralDocumentEvidenceRules.CurrentPublished()),
                approvalEvidence => approvalEvidence.CentralDocumentVersionId,
                version => version.Id,
                (_, version) => version)
            .SingleOrDefaultAsync(item => item.TenantId == TenantId, token);
        var value = selected
            ?? throw Validation("Select current Published Planning/GIS evidence from central DMS.");
        if (value.DocumentRecordId != recordId
            || !string.Equals(value.DocumentRecord.MetadataTemplateCode, "TDC-CIV-ENGINEERING-FILE", StringComparison.OrdinalIgnoreCase))
            throw Validation("The selected evidence must be a current Published Civil Engineering DMS document.");
        return value;
    }

    private async Task<IReadOnlyList<CivilEngineeringPlanningGisLookupOptionDto>> PlanningGisCatalogAsync(string catalogType, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        return await db.ProjectCatalogEntries.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.CatalogType == catalogType && item.IsActive && !item.IsDeleted
                           && (!item.EffectiveFrom.HasValue || item.EffectiveFrom <= now)
                           && (!item.EffectiveTo.HasValue || item.EffectiveTo >= now))
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .Select(item => new CivilEngineeringPlanningGisLookupOptionDto { Id = item.Id, Label = item.Code + " - " + item.Name })
            .ToListAsync(token);
    }

    private static CivilEngineeringPlanningGisValidationDto MapPlanningGisValidation(ProjectCivilPlanningGisValidation value) => new()
    {
        Id = value.Id,
        DesignCaseId = value.DesignCaseId,
        EstateManagedAssetId = value.EstateManagedAssetId,
        EstateManagedAssetLabel = value.EstateManagedAsset.AssetCode + " - " + value.EstateManagedAsset.Name,
        DevelopmentApprovalFileId = value.DevelopmentApprovalFileId,
        DevelopmentApprovalFileLabel = value.DevelopmentApprovalFile.FileNumber + " - " + value.DevelopmentApprovalFile.ApplicationReference,
        PlanningConditionId = value.PlanningConditionId,
        PlanningConditionLabel = value.PlanningCondition.Code + " - " + value.PlanningCondition.Name,
        DevelopmentConstraintId = value.DevelopmentConstraintId,
        DevelopmentConstraintLabel = value.DevelopmentConstraint.Code + " - " + value.DevelopmentConstraint.Name,
        LandUseImpactId = value.LandUseImpactId,
        LandUseImpactLabel = value.LandUseImpact.Code + " - " + value.LandUseImpact.Name,
        LayoutConformity = value.LayoutConformity,
        SpatialReference = value.SpatialReferenceSnapshot,
        BoundaryCoordinates = value.BoundaryCoordinatesSnapshot,
        CentralDocumentRecordId = value.CentralDocumentRecordId,
        CentralDocumentVersionId = value.CentralDocumentVersionId,
        EvidenceReference = value.CentralDocumentRecord.DocumentReference,
        Status = value.Status,
        PreparedByUserId = value.PreparedByUserId,
        SubmittedAt = value.SubmittedAt,
        ReviewedByUserId = value.ReviewedByUserId,
        ReviewedAt = value.ReviewedAt,
        RowVersion = Convert.ToBase64String(value.RowVersion)
    };

    private static bool IsPlanningGisMutationRetry(ProjectCivilPlanningGisValidation value, Guid requestId, string requestHash)
    {
        if (value.LastMutationClientRequestId != requestId) return false;
        if (!FixedEquals(value.LastMutationRequestHash, requestHash)) throw RetryConflict();
        return true;
    }

    private void AddPlanningGisRevision(ProjectCivilPlanningGisValidation value, string action, object? before, object after, string correlationId) =>
        value.Revisions.Add(new ProjectCivilPlanningGisValidationRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, PlanningGisValidationId = value.Id,
            Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles,
            CorrelationId = NormalizeCorrelation(correlationId),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions),
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private void AddPlanningGisAudit(ProjectCivilPlanningGisValidation value, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = nameof(ProjectCivilPlanningGisValidation), ResourceId = value.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(after, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty,
            UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        });

    private static object PlanningGisSnapshot(ProjectCivilPlanningGisValidation value) => new
    {
        value.Id, value.DesignCaseId, value.EstateManagedAssetId, value.DevelopmentApprovalFileId,
        value.PlanningConditionId, value.DevelopmentConstraintId, value.LandUseImpactId, value.LayoutConformity,
        value.SpatialReferenceSnapshot, value.GisProviderSnapshot, value.GisFeatureIdSnapshot,
        value.GisSourceCrsSnapshot, value.BoundaryCoordinatesSnapshot, value.ZoningClassificationSnapshot,
        value.PlanningComplianceSnapshot, value.CentralDocumentRecordId, value.CentralDocumentVersionId,
        value.Status, value.PreparedByUserId, value.SubmittedAt, value.SubmittedByUserId, value.ReviewedAt,
        value.ReviewedByUserId
    };

    private static string PlanningGisSpatialReference(EstateManagedAsset value) =>
        PlanningGisSpatialReferenceOrNull(value)
        ?? throw Validation("The selected property or site has no governed spatial reference.");

    private static string? PlanningGisSpatialReferenceOrNull(EstateManagedAsset value)
    {
        var values = new[]
        {
            value.GisLayerReference,
            value.GisFeatureId,
            value.BoundaryCoordinates,
            value.CadastreDescription,
            value.SurveyPlanNumber,
            value.MapSheetNumber
        }.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!.Trim()).ToList();
        return values.Count == 0 ? null : string.Join(" | ", values).Length <= 512
            ? string.Join(" | ", values)
            : string.Join(" | ", values)[..512];
    }

    private static string? TrimOrNull(string? value, int max) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}
