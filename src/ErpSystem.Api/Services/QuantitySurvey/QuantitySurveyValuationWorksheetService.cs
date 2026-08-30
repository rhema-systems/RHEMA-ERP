using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed partial class QuantitySurveyValuationWorksheetService : IQuantitySurveyValuationWorksheetService
{
    private readonly ApplicationDbContext db;
    private readonly ICurrentUserService currentUser;
    private readonly IProjectService projectService;
    private readonly IWorkflowIntegrationService workflow;
    private readonly IWorkflowStatusAdapterRegistry workflowAdapters;
    private readonly IControlledFileUploadService controlledFiles;
    private readonly ICentralDocumentRepositoryFileService centralDocuments;
    private readonly ILogger<QuantitySurveyValuationWorksheetService> logger;

    public QuantitySurveyValuationWorksheetService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IProjectService projectService,
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry workflowAdapters,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        ILogger<QuantitySurveyValuationWorksheetService> logger)
    {
        this.db = db;
        this.currentUser = currentUser;
        this.projectService = projectService;
        this.workflow = workflow;
        this.workflowAdapters = workflowAdapters;
        this.controlledFiles = controlledFiles;
        this.centralDocuments = centralDocuments;
        this.logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyValuationLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        if (projectId == Guid.Empty) throw Validation("Select a project.");
        await RequireProjectAccessAsync(projectId);
        return await BuildValuationLookupsAsync(projectId, null, token);
    }

    public async Task<QuantitySurveyValuationWorksheetDto> GetAsync(
        Guid interimValuationId, Guid? projectBoqVersionId, CancellationToken token = default)
    {
        var existing = await Query().FirstOrDefaultAsync(value => value.ProjectInterimValuationId == interimValuationId, token);
        if (existing is not null)
        {
            await RequireProjectAccessAsync(existing.ProjectId);
            return Map(existing);
        }
        var valuation = await RequiredValuationAsync(interimValuationId, token);
        await RequireProjectAccessAsync(valuation.ProjectId);
        var source = await BuildSourceAsync(valuation, projectBoqVersionId, token);
        var lines = source.Lines.Select(value => QuantitySurveyValuationWorksheetRules.Calculate(
            value, value.MeasuredToDateQuantity, value.MeasuredToDateQuantity,
            valuation.RetentionPercentage, null)).ToList();
        return MapPreview(valuation, source.Version, valuation.RetentionPercentage, lines);
    }

    public async Task<QuantitySurveyValuationWorksheetDto> SaveAsync(
        Guid interimValuationId, SaveQuantitySurveyValuationWorksheetRequest request,
        string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (request.ProjectBoqVersionId == Guid.Empty) throw Validation("Select an approved published BoQ version.");
        var valuation = await RequiredValuationAsync(interimValuationId, token);
        await RequireProjectAccessAsync(valuation.ProjectId);
        if (valuation.Status is not (ProjectInterimValuationStatuses.Draft or
            ProjectInterimValuationStatuses.Submitted or ProjectInterimValuationStatuses.UnderReview))
            throw Conflict("Only a Draft, Submitted, or UnderReview interim valuation can have its governed worksheet amended.");

        var source = await BuildSourceAsync(valuation, request.ProjectBoqVersionId, token);
        var inputs = request.Lines.ToDictionary(value => value.ProjectBoqVersionLineId);
        if (inputs.Count != request.Lines.Count || inputs.Count != source.Lines.Count ||
            source.Lines.Any(value => !inputs.ContainsKey(value.ProjectBoqVersionLineId)))
            throw Validation("Submit exactly one controlled worksheet row for every item in the selected approved BoQ.");
        var retention = decimal.Round(request.RetentionPercentage, 4, MidpointRounding.AwayFromZero);
        var calculated = source.Lines.Select(value =>
        {
            var input = inputs[value.ProjectBoqVersionLineId];
            return QuantitySurveyValuationWorksheetRules.Calculate(value, input.CurrentClaimedQuantity,
                input.CurrentCertifiedQuantity, retention, input.ReviewNote);
        }).ToList();
        var totals = QuantitySurveyValuationWorksheetRules.Total(calculated);
        var policy = await ResolveValuationPolicyAsync(DateTime.UtcNow, token);
        var lookups = await BuildValuationLookupsAsync(valuation.ProjectId, null, token);
        if (policy.Valuation.RequireContractorSubmission &&
            (!request.ContractorBusinessPartnerId.HasValue ||
             !lookups.Contractors.Any(value => value.Id == request.ContractorBusinessPartnerId.Value)))
            throw Validation("Select an active project contractor with governed portal access.");
        if (policy.Valuation.RequireConsultantEndorsement &&
            (!request.ConsultantBusinessPartnerId.HasValue ||
             !lookups.Consultants.Any(value => value.Id == request.ConsultantBusinessPartnerId.Value)))
            throw Validation("Select an active project consultant with governed portal access.");
        if (request.ContractorBusinessPartnerId.HasValue && request.ConsultantBusinessPartnerId.HasValue &&
            request.ContractorBusinessPartnerId == request.ConsultantBusinessPartnerId)
            throw Validation("Contractor and consultant must be different business partners.");
        var requestHash = Hash(new
        {
            interimValuationId, request.ProjectBoqVersionId, RetentionPercentage = retention,
            request.ContractorBusinessPartnerId, request.ConsultantBusinessPartnerId,
            Lines = calculated.Select(value => new
            {
                value.Source.ProjectBoqVersionLineId,
                value.CurrentClaimedQuantity,
                value.CurrentCertifiedQuantity,
                value.ReviewNote
            })
        });

        var resultId = Guid.Empty;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var entity = await Query(true).FirstOrDefaultAsync(value => value.ProjectInterimValuationId == interimValuationId, token);
            var action = entity is null ? QuantitySurveyAuditEventMap.CreateValuationWorksheet : QuantitySurveyAuditEventMap.UpdateValuationWorksheet;
            if (entity is not null && entity.ClientRequestId == request.ClientRequestId)
            {
                if (!FixedEquals(entity.RequestHash, requestHash)) throw RetryConflict();
                resultId = entity.Id; await transaction.CommitAsync(token); return;
            }
            if (entity is not null && entity.LastMutationClientRequestId == request.ClientRequestId)
            {
                if (!FixedEquals(entity.LastMutationRequestHash, requestHash)) throw RetryConflict();
                resultId = entity.Id; await transaction.CommitAsync(token); return;
            }
            object? before = null;
            if (entity is null)
            {
                var duplicate = await db.QuantitySurveyValuationWorksheets.AsNoTracking()
                    .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
                if (duplicate is not null)
                {
                    if (duplicate.ProjectInterimValuationId != interimValuationId || !FixedEquals(duplicate.RequestHash, requestHash))
                        throw RetryConflict();
                    resultId = duplicate.Id; await transaction.CommitAsync(token); return;
                }
                entity = new QuantitySurveyValuationWorksheet
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = valuation.ProjectId,
                    ProjectInterimValuationId = valuation.Id, ProjectBoqVersionId = source.Version.Id,
                    ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                    Status = QuantitySurveyValuationWorkflowStatuses.Draft, ApprovalStatus = "Draft",
                    ContractorBusinessPartnerId = request.ContractorBusinessPartnerId,
                    ConsultantBusinessPartnerId = request.ConsultantBusinessPartnerId,
                    ConfigurationProfileId = policy.Profile.Id, ValuationDecisionId = policy.ValuationDecision.Id,
                    ExternalSubmissionDecisionId = policy.ExternalDecision.Id,
                    ApprovalWorkflowDefinitionId = policy.Valuation.ValuationWorkflowDefinitionId,
                    EvidenceMetadataTemplateId = policy.Template.Id,
                    EvidenceMetadataTemplateCodeSnapshot = policy.Template.TemplateCode,
                    PolicyHash = policy.PolicyHash,
                    ContractorSubmissionRequired = policy.Valuation.RequireContractorSubmission,
                    ConsultantEndorsementRequired = policy.Valuation.RequireConsultantEndorsement,
                    SupportingEvidenceRequired = policy.Valuation.RequireSupportingEvidence || policy.External.RequireEvidence,
                    PortalIdentityRequired = policy.External.RequirePortalIdentity,
                    ExternalSignatureRequired = policy.External.RequireSignature,
                    PreparedById = UserId, PreparedByName = UserName, PreparedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
                };
                db.QuantitySurveyValuationWorksheets.Add(entity);
            }
            else
            {
                if (entity.ProjectBoqVersionId != request.ProjectBoqVersionId)
                    throw Conflict("The approved BoQ lineage of an existing valuation worksheet cannot be replaced.");
                before = Snapshot(entity);
                if (IsLegacyDraftValuationPolicy(entity)) FreezeValuationPolicy(entity, policy);
                else ValidateFrozenValuationPolicy(entity, policy);
                if (entity.Status is not (QuantitySurveyValuationWorkflowStatuses.Draft or
                    QuantitySurveyValuationWorkflowStatuses.ContractorSubmitted or
                    QuantitySurveyValuationWorkflowStatuses.UnderQsReview))
                    throw Conflict("The valuation worksheet cannot be amended after QS vetting.");
                if (entity.Status != QuantitySurveyValuationWorkflowStatuses.Draft &&
                    (entity.ContractorBusinessPartnerId != request.ContractorBusinessPartnerId ||
                     entity.ConsultantBusinessPartnerId != request.ConsultantBusinessPartnerId))
                    throw Conflict("Assigned contractor and consultant cannot change after contractor submission.");
                if (entity.Status != QuantitySurveyValuationWorkflowStatuses.Draft &&
                    calculated.Any(value => entity.Lines.Single(line => line.ProjectBoqVersionLineId ==
                        value.Source.ProjectBoqVersionLineId).CurrentClaimedQuantity != value.CurrentClaimedQuantity))
                    throw Conflict("The QS reviewer cannot alter contractor-claimed quantities after submission.");
                ApplyRowVersion(entity, request.RowVersion);
                entity.ContractorBusinessPartnerId = request.ContractorBusinessPartnerId;
                entity.ConsultantBusinessPartnerId = request.ConsultantBusinessPartnerId;
                if (entity.Status == QuantitySurveyValuationWorkflowStatuses.ContractorSubmitted)
                {
                    QuantitySurveyValuationWorksheetRules.RequireTransition(
                        entity.Status, QuantitySurveyValuationWorkflowStatuses.UnderQsReview);
                    entity.Status = QuantitySurveyValuationWorkflowStatuses.UnderQsReview;
                    SyncParentValuation(entity, ProjectInterimValuationStatuses.UnderReview);
                }
                entity.LastMutationClientRequestId = request.ClientRequestId;
                entity.LastMutationRequestHash = requestHash;
                entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
            }
            Apply(entity, retention, totals, calculated, action, correlationId);
            var after = Snapshot(entity);
            db.QuantitySurveyValuationWorksheetRevisions.Add(new QuantitySurveyValuationWorksheetRevision
            {
                Id = Guid.NewGuid(), TenantId = TenantId, WorksheetId = entity.Id, Action = action,
                ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles,
                CorrelationId = Correlation(correlationId), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
                AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName, CreatedById = UserId
            });
            db.AuditLogs.Add(new AuditLog
            {
                TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
                Resource = nameof(QuantitySurveyValuationWorksheet), ResourceId = entity.Id.ToString(),
                OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
                NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
                IpAddress = "api", UserAgent = "QS-0501", Timestamp = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
            });
            await SaveChangesAsync(token); resultId = entity.Id; await transaction.CommitAsync(token);
        });
        var result = await Query().SingleAsync(value => value.Id == resultId, token);
        return Map(result);
    }

    public async Task<IReadOnlyList<QuantitySurveyValuationWorksheetRevisionDto>> HistoryAsync(
        Guid interimValuationId, CancellationToken token = default)
    {
        var worksheet = await Query().FirstOrDefaultAsync(value => value.ProjectInterimValuationId == interimValuationId, token)
            ?? throw new QuantitySurveyValuationWorksheetNotFoundException("The valuation worksheet has not been saved.");
        await RequireProjectAccessAsync(worksheet.ProjectId);
        return await db.QuantitySurveyValuationWorksheetRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.WorksheetId == worksheet.Id && !value.IsDeleted)
            .OrderBy(value => value.CreatedAt).ThenBy(value => value.Id)
            .Select(value => new QuantitySurveyValuationWorksheetRevisionDto
            {
                Id = value.Id, Action = value.Action, ActorName = value.ActorName, ActorRoles = value.ActorRoles,
                CorrelationId = value.CorrelationId, BeforeJson = value.BeforeJson, AfterJson = value.AfterJson,
                CreatedAt = value.CreatedAt
            }).ToListAsync(token);
    }

    private IQueryable<QuantitySurveyValuationWorksheet> Query(bool tracking = false)
    {
        var query = tracking ? db.QuantitySurveyValuationWorksheets.AsTracking() : db.QuantitySurveyValuationWorksheets.AsNoTracking();
        return query.Include(value => value.ProjectInterimValuation).Include(value => value.ProjectBoqVersion)
            .Include(value => value.ContractorBusinessPartner).Include(value => value.ConsultantBusinessPartner)
            .Include(value => value.ConfigurationProfile).Include(value => value.ValuationDecision)
            .Include(value => value.ExternalSubmissionDecision).Include(value => value.EvidenceMetadataTemplate)
            .Include(value => value.Lines)
            .Include(value => value.Evidence).ThenInclude(value => value.CentralDocumentRecord)
            .Include(value => value.Evidence).ThenInclude(value => value.CentralDocumentVersion)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
    }

    private async Task<ProjectInterimValuation> RequiredValuationAsync(Guid id, CancellationToken token)
        => await db.Set<ProjectInterimValuation>().AsNoTracking()
               .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, token)
           ?? throw new QuantitySurveyValuationWorksheetNotFoundException("The interim valuation was not found.");

    private async Task<ValuationSource> BuildSourceAsync(
        ProjectInterimValuation valuation, Guid? boqVersionId, CancellationToken token)
    {
        var versions = db.ProjectBoqVersions.AsNoTracking().Where(value => value.TenantId == TenantId &&
            value.ProjectId == valuation.ProjectId && !value.IsDeleted && value.Status == ProjectBoqVersionStatuses.Approved &&
            value.PublishedAt != null);
        var version = boqVersionId.HasValue
            ? await versions.FirstOrDefaultAsync(value => value.Id == boqVersionId.Value, token)
            : await versions.OrderByDescending(value => value.VersionNumber).FirstOrDefaultAsync(token);
        if (version is null) throw Validation("Select an approved published BoQ version for this project.");
        var boqLines = await db.ProjectBoqVersionLines.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == valuation.ProjectId &&
                            value.ProjectBoqVersionId == version.Id && !value.IsDeleted && value.ItemType == ProjectBoqItemTypes.Item)
            .OrderBy(value => value.SortOrder).ThenBy(value => value.Id).ToListAsync(token);
        if (boqLines.Count == 0) throw Validation("The selected approved BoQ contains no item lines.");
        if (boqLines.Any(value => !value.UnitRate.HasValue || value.UnitRate.Value < 0))
            throw Validation("Every item in the selected approved BoQ requires a controlled unit rate before valuation.");
        if (boqLines.Any(value => !string.Equals(value.Currency, valuation.Currency, StringComparison.OrdinalIgnoreCase)))
            throw Validation("The selected approved BoQ currency must match the interim valuation currency.");

        var keys = boqLines.Select(value => value.LineKey).ToList();
        var measurements = await db.QuantitySurveyMeasurementSheets.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == valuation.ProjectId && !value.IsDeleted &&
                            value.Status == "Recorded" && keys.Contains(value.BoqLineKey))
            .GroupBy(value => value.BoqLineKey)
            .Select(group => new { BoqLineKey = group.Key, Quantity = group.Sum(value => value.TotalMeasuredQuantity) })
            .ToDictionaryAsync(value => value.BoqLineKey, value => decimal.Round(value.Quantity, 4), token);
        var prior = await db.QuantitySurveyValuationWorksheets.AsNoTracking()
            .Include(value => value.ProjectInterimValuation).Include(value => value.Lines)
            .Where(value => value.TenantId == TenantId && value.ProjectId == valuation.ProjectId && !value.IsDeleted &&
                            value.ProjectInterimValuationId != valuation.Id &&
                            (value.ProjectInterimValuation.Status == ProjectInterimValuationStatuses.Certified ||
                             value.ProjectInterimValuation.Status == ProjectInterimValuationStatuses.Paid) &&
                            value.ProjectInterimValuation.ValuationDate <= valuation.ValuationDate)
            .OrderByDescending(value => value.ProjectInterimValuation.ValuationDate)
            .ThenByDescending(value => value.CreatedAt).ToListAsync(token);
        var previousByKey = prior.SelectMany(value => value.Lines.OrderBy(line => line.Sequence)
                .Select(line => new { value.ProjectInterimValuation.ValuationDate, WorksheetCreatedAt = value.CreatedAt, Line = line }))
            .Where(value => keys.Contains(value.Line.BoqLineKey))
            .GroupBy(value => value.Line.BoqLineKey)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(value => value.ValuationDate)
                .ThenByDescending(value => value.WorksheetCreatedAt).First().Line);
        var result = boqLines.Select((line, index) =>
        {
            measurements.TryGetValue(line.LineKey, out var measured);
            previousByKey.TryGetValue(line.LineKey, out var previous);
            if (previous is not null && previous.CurrentCertifiedQuantity > measured)
                throw Conflict($"Recorded measurements for {LineLabel(line)} are less than its previous certification. Reconcile measurement lineage before continuing.");
            return new QuantitySurveyValuationSourceLine(
                line.Id, line.LineKey, index + 1, LineLabel(line), line.Description, line.UnitOfMeasure,
                line.Currency, line.Quantity, line.UnitRate!.Value, measured,
                previous?.CurrentCertifiedQuantity ?? 0m, previous?.CurrentCertifiedValue ?? 0m,
                previous?.RetentionToDateValue ?? 0m);
        }).ToList();
        return new(version, result);
    }

    private void Apply(QuantitySurveyValuationWorksheet entity, decimal retention, QuantitySurveyValuationTotals totals,
        IReadOnlyList<QuantitySurveyValuationLineCalculation> lines, string action, string correlationId)
    {
        entity.RetentionPercentage = retention; entity.MeasuredToDateValue = totals.MeasuredToDateValue;
        entity.PreviouslyCertifiedValue = totals.PreviouslyCertifiedValue; entity.CurrentClaimedValue = totals.CurrentClaimedValue;
        entity.CurrentCertifiedValue = totals.CurrentCertifiedValue; entity.CurrentPeriodCertifiedValue = totals.CurrentPeriodCertifiedValue;
        entity.DisputedValue = totals.DisputedValue; entity.RetentionToDateValue = totals.RetentionToDateValue;
        entity.CurrentRetentionValue = totals.CurrentRetentionValue; entity.NetCurrentValue = totals.NetCurrentValue;
        entity.LineCount = lines.Count; entity.AuditAction = action; entity.CorrelationId = Correlation(correlationId); entity.ActorRoles = ActorRoles;
        foreach (var value in lines)
        {
            var line = entity.Lines.SingleOrDefault(item => item.ProjectBoqVersionLineId == value.Source.ProjectBoqVersionLineId);
            var isExistingLine = line is not null;
            if (line is null)
            {
                line = new QuantitySurveyValuationWorksheetLine
                {
                Id = Guid.NewGuid(), TenantId = TenantId, WorksheetId = entity.Id,
                ProjectBoqVersionLineId = value.Source.ProjectBoqVersionLineId,
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
                };
                entity.Lines.Add(line);
            }
            line.BoqLineKey = value.Source.BoqLineKey;
            line.Sequence = value.Source.Sequence; line.LineNumberSnapshot = value.Source.Label.Split(" · ")[0];
            line.DescriptionSnapshot = value.Source.Description; line.UnitOfMeasureSnapshot = value.Source.UnitOfMeasure;
            line.CurrencySnapshot = value.Source.Currency; line.BoqQuantitySnapshot = value.Source.BoqQuantity;
            line.UnitRateSnapshot = value.Source.UnitRate; line.MeasuredToDateQuantity = value.Source.MeasuredToDateQuantity;
            line.PreviouslyCertifiedQuantity = value.Source.PreviouslyCertifiedQuantity;
            line.CurrentClaimedQuantity = value.CurrentClaimedQuantity; line.CurrentCertifiedQuantity = value.CurrentCertifiedQuantity;
            line.DisputedQuantity = value.DisputedQuantity; line.MeasuredToDateValue = value.MeasuredToDateValue;
            line.PreviouslyCertifiedValue = value.Source.PreviouslyCertifiedValue; line.CurrentClaimedValue = value.CurrentClaimedValue;
            line.CurrentCertifiedValue = value.CurrentCertifiedValue; line.CurrentPeriodCertifiedValue = value.CurrentPeriodCertifiedValue;
            line.DisputedValue = value.DisputedValue; line.PreviousRetentionValue = value.Source.PreviousRetentionValue;
            line.RetentionToDateValue = value.RetentionToDateValue; line.CurrentRetentionValue = value.CurrentRetentionValue;
            line.NetCurrentValue = value.NetCurrentValue; line.ReviewNote = value.ReviewNote;
            if (isExistingLine)
            {
                line.UpdatedAt = DateTime.UtcNow; line.UpdatedBy = UserName; line.LastModifiedById = UserId;
            }
        }
    }

    private static QuantitySurveyValuationWorksheetDto Map(QuantitySurveyValuationWorksheet value) => new()
    {
        Id = value.Id, ProjectId = value.ProjectId, ProjectInterimValuationId = value.ProjectInterimValuationId,
        InterimValuationLabel = ValuationLabel(value.ProjectInterimValuation), ProjectBoqVersionId = value.ProjectBoqVersionId,
        BoqVersionNumber = value.ProjectBoqVersion.VersionNumber, Status = value.Status,
        ApprovalStatus = value.ApprovalStatus,
        ContractorBusinessPartnerId = value.ContractorBusinessPartnerId,
        ContractorName = value.ContractorBusinessPartner?.PartnerName,
        ConsultantBusinessPartnerId = value.ConsultantBusinessPartnerId,
        ConsultantName = value.ConsultantBusinessPartner?.PartnerName,
        WorkflowInstanceId = value.WorkflowInstanceId,
        ContractorSubmissionRequired = value.ContractorSubmissionRequired,
        ConsultantEndorsementRequired = value.ConsultantEndorsementRequired,
        SupportingEvidenceRequired = value.SupportingEvidenceRequired,
        PortalIdentityRequired = value.PortalIdentityRequired,
        ExternalSignatureRequired = value.ExternalSignatureRequired,
        ContractorAttestationText = ContractorAttestation,
        ConsultantAttestationText = ConsultantAttestation,
        ContractorSubmittedAt = value.ContractorSubmittedAt, QsVettedAt = value.QsVettedAt,
        QsReviewNote = value.QsReviewNote, ConsultantEndorsedAt = value.ConsultantEndorsedAt,
        ApprovedAt = value.ApprovedAt, RejectionReason = value.RejectionReason,
        CertificateReady = value.CertificateReady, CertificateReadyAt = value.CertificateReadyAt,
        RetentionPercentage = value.RetentionPercentage, MeasuredToDateValue = value.MeasuredToDateValue,
        PreviouslyCertifiedValue = value.PreviouslyCertifiedValue, CurrentClaimedValue = value.CurrentClaimedValue,
        CurrentCertifiedValue = value.CurrentCertifiedValue, CurrentPeriodCertifiedValue = value.CurrentPeriodCertifiedValue,
        DisputedValue = value.DisputedValue, RetentionToDateValue = value.RetentionToDateValue,
        CurrentRetentionValue = value.CurrentRetentionValue, NetCurrentValue = value.NetCurrentValue,
        RowVersion = Convert.ToBase64String(value.RowVersion), Lines = value.Lines.Where(line => !line.IsDeleted)
            .OrderBy(line => line.Sequence).Select(MapLine).ToList(),
        Evidence = value.Evidence.Where(item => !item.IsDeleted).OrderBy(item => item.UploadedAt)
            .Select(MapValuationEvidence).ToList()
    };

    private static QuantitySurveyValuationWorksheetDto MapPreview(ProjectInterimValuation valuation, ProjectBoqVersion version,
        decimal retention, IReadOnlyList<QuantitySurveyValuationLineCalculation> lines)
    {
        var totals = QuantitySurveyValuationWorksheetRules.Total(lines);
        return new()
        {
            ProjectId = valuation.ProjectId, ProjectInterimValuationId = valuation.Id, InterimValuationLabel = ValuationLabel(valuation),
            ProjectBoqVersionId = version.Id, BoqVersionNumber = version.VersionNumber,
            Status = QuantitySurveyValuationWorkflowStatuses.Draft, ApprovalStatus = "Draft",
            RetentionPercentage = retention, MeasuredToDateValue = totals.MeasuredToDateValue,
            PreviouslyCertifiedValue = totals.PreviouslyCertifiedValue, CurrentClaimedValue = totals.CurrentClaimedValue,
            CurrentCertifiedValue = totals.CurrentCertifiedValue, CurrentPeriodCertifiedValue = totals.CurrentPeriodCertifiedValue,
            DisputedValue = totals.DisputedValue, RetentionToDateValue = totals.RetentionToDateValue,
            CurrentRetentionValue = totals.CurrentRetentionValue, NetCurrentValue = totals.NetCurrentValue,
            Lines = lines.Select(MapLine).ToList()
        };
    }

    private static QuantitySurveyValuationWorksheetLineDto MapLine(QuantitySurveyValuationWorksheetLine value) => new()
    {
        Id = value.Id, ProjectBoqVersionLineId = value.ProjectBoqVersionLineId, BoqLineKey = value.BoqLineKey,
        Sequence = value.Sequence, Label = value.LineNumberSnapshot ?? value.ItemCodeSnapshot ?? $"Line {value.Sequence}",
        Description = value.DescriptionSnapshot, UnitOfMeasure = value.UnitOfMeasureSnapshot, Currency = value.CurrencySnapshot,
        BoqQuantity = value.BoqQuantitySnapshot, UnitRate = value.UnitRateSnapshot,
        MeasuredToDateQuantity = value.MeasuredToDateQuantity, PreviouslyCertifiedQuantity = value.PreviouslyCertifiedQuantity,
        CurrentClaimedQuantity = value.CurrentClaimedQuantity, CurrentCertifiedQuantity = value.CurrentCertifiedQuantity,
        DisputedQuantity = value.DisputedQuantity, MeasuredToDateValue = value.MeasuredToDateValue,
        PreviouslyCertifiedValue = value.PreviouslyCertifiedValue, CurrentClaimedValue = value.CurrentClaimedValue,
        CurrentCertifiedValue = value.CurrentCertifiedValue, CurrentPeriodCertifiedValue = value.CurrentPeriodCertifiedValue,
        DisputedValue = value.DisputedValue, PreviousRetentionValue = value.PreviousRetentionValue,
        RetentionToDateValue = value.RetentionToDateValue, CurrentRetentionValue = value.CurrentRetentionValue,
        NetCurrentValue = value.NetCurrentValue, ReviewNote = value.ReviewNote
    };

    private static QuantitySurveyValuationWorksheetLineDto MapLine(QuantitySurveyValuationLineCalculation value) => new()
    {
        ProjectBoqVersionLineId = value.Source.ProjectBoqVersionLineId, BoqLineKey = value.Source.BoqLineKey,
        Sequence = value.Source.Sequence, Label = value.Source.Label, Description = value.Source.Description,
        UnitOfMeasure = value.Source.UnitOfMeasure, Currency = value.Source.Currency, BoqQuantity = value.Source.BoqQuantity,
        UnitRate = value.Source.UnitRate, MeasuredToDateQuantity = value.Source.MeasuredToDateQuantity,
        PreviouslyCertifiedQuantity = value.Source.PreviouslyCertifiedQuantity,
        CurrentClaimedQuantity = value.CurrentClaimedQuantity, CurrentCertifiedQuantity = value.CurrentCertifiedQuantity,
        DisputedQuantity = value.DisputedQuantity, MeasuredToDateValue = value.MeasuredToDateValue,
        PreviouslyCertifiedValue = value.Source.PreviouslyCertifiedValue, CurrentClaimedValue = value.CurrentClaimedValue,
        CurrentCertifiedValue = value.CurrentCertifiedValue, CurrentPeriodCertifiedValue = value.CurrentPeriodCertifiedValue,
        DisputedValue = value.DisputedValue, PreviousRetentionValue = value.Source.PreviousRetentionValue,
        RetentionToDateValue = value.RetentionToDateValue, CurrentRetentionValue = value.CurrentRetentionValue,
        NetCurrentValue = value.NetCurrentValue, ReviewNote = value.ReviewNote
    };

    private static object Snapshot(QuantitySurveyValuationWorksheet value) => new
    {
        value.Id, value.ProjectId, value.ProjectInterimValuationId, value.ProjectBoqVersionId,
        value.Status, value.ApprovalStatus, value.ContractorBusinessPartnerId, value.ConsultantBusinessPartnerId,
        value.ConfigurationProfileId, value.ValuationDecisionId, value.ExternalSubmissionDecisionId,
        value.ApprovalWorkflowDefinitionId, value.WorkflowInstanceId, value.EvidenceMetadataTemplateId,
        value.PolicyHash, value.ContractorSubmittedById, value.ContractorSubmittedAt, value.ContractorSignatureHash,
        value.QsVettedById, value.QsVettedAt, value.QsReviewNote, value.ConsultantEndorsedById,
        value.ConsultantEndorsedAt, value.ConsultantSignatureHash, value.ApprovedById, value.ApprovedAt,
        value.RejectionReason, value.CertificateReady, value.CertificateReadyAt,
        value.RetentionPercentage, value.MeasuredToDateValue, value.PreviouslyCertifiedValue,
        value.CurrentClaimedValue, value.CurrentCertifiedValue, value.CurrentPeriodCertifiedValue,
        value.DisputedValue, value.RetentionToDateValue, value.CurrentRetentionValue, value.NetCurrentValue,
        Lines = value.Lines.OrderBy(line => line.Sequence).Select(line => new
        {
            line.ProjectBoqVersionLineId, line.BoqLineKey, line.Sequence, line.MeasuredToDateQuantity,
            line.PreviouslyCertifiedQuantity, line.CurrentClaimedQuantity, line.CurrentCertifiedQuantity,
            line.DisputedQuantity, line.PreviouslyCertifiedValue, line.CurrentPeriodCertifiedValue,
            line.CurrentRetentionValue, line.NetCurrentValue, line.ReviewNote
        }),
        Evidence = value.Evidence.Where(item => !item.IsDeleted).OrderBy(item => item.UploadedAt).Select(item => new
        {
            item.Id, item.EvidenceType, item.ChecksumSha256, item.CentralDocumentRecordId,
            item.CentralDocumentVersionId, item.UploadedAt
        })
    };

    private async Task RequireProjectAccessAsync(Guid projectId)
    {
        if (!await projectService.HasProjectAccessAsync(projectId))
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }
    private void ApplyRowVersion(QuantitySurveyValuationWorksheet entity, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Validation("The row version is required. Refresh and retry.");
        try { db.Entry(entity).Property(item => item.RowVersion).OriginalValue = Convert.FromBase64String(value); }
        catch (FormatException) { throw Validation("The row version is invalid. Refresh and retry."); }
    }
    private async Task SaveChangesAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException exception)
        {
            var entries = string.Join(", ", exception.Entries.Select(entry => entry.Metadata.ClrType.Name));
            logger.LogWarning(exception,
                "QS valuation persistence reported a concurrency conflict for entries: {Entries}.", entries);
            throw Conflict("The valuation worksheet changed after it was loaded. Refresh and retry.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql)
        {
            throw Conflict(sql.Number switch
            {
                51120 => "The valuation worksheet violates its tenant, project, interim-valuation, approved-BoQ, or line lineage controls.",
                51121 => "The valuation worksheet totals do not reconcile with its controlled line calculations.",
                51122 => "The valuation worksheet audit history is append-only.",
                >= 51960 and <= 51969 => "The interim valuation lifecycle, segregation-of-duties, workflow, evidence, or frozen-lineage control rejected this change.",
                2601 or 2627 => "An equivalent valuation worksheet operation already exists. Refresh and retry.",
                _ => "The valuation worksheet could not be saved because a database control rejected it."
            });
        }
    }
    private static string LineLabel(ProjectBoqVersionLine value) => value.LineNumber ?? value.ItemCode ?? $"Line {value.SortOrder}";
    private static string ValuationLabel(ProjectInterimValuation value) => string.IsNullOrWhiteSpace(value.ValuationNumber)
        ? value.Title : $"{value.ValuationNumber} · {value.Title}";
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right) => left is not null && right is not null && left.Length == right.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static string Correlation(string? value) => Truncate(string.IsNullOrWhiteSpace(value) ? "qs-0501" : value.Trim(), 100);
    private static string Truncate(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
    private static QuantitySurveyValuationWorksheetValidationException Validation(string message) => new(message);
    private static QuantitySurveyValuationWorksheetConflictException Conflict(string message) => new(message);
    private static QuantitySurveyValuationWorksheetConflictException RetryConflict() => Conflict("The client request identifier was already used for different valuation data. Refresh and retry with a new request.");
    private sealed record ValuationSource(ProjectBoqVersion Version, IReadOnlyList<QuantitySurveyValuationSourceLine> Lines);
}
