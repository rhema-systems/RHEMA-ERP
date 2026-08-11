using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyDayworkService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IControlledFileUploadService controlledFiles,
    ICentralDocumentRepositoryFileService centralDocuments) : IQuantitySurveyDayworkService
{
    private const int MaximumEvidenceBytes = 50 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyDayworkWorkspaceDto> GetWorkspaceAsync(Guid projectId, bool external, CancellationToken token = default)
    {
        ExternalActor? actor = external ? await RequireExternalActorAsync(projectId, false, null, token) : null;
        if (!external) await RequireProjectAsync(projectId);

        var variationsQuery = db.ProjectVariationOrders.AsNoTracking().Include(value => value.Contract).ThenInclude(value => value!.BusinessPartner)
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && value.IsQuantitySurveyGoverned && !value.IsDeleted &&
                value.ContractId.HasValue && value.ContractorBusinessPartnerId.HasValue &&
                (value.VariationType == ProjectVariationOrderTypes.Daywork || value.VariationType == ProjectVariationOrderTypes.AdditionalWork));
        if (actor.HasValue) variationsQuery = variationsQuery.Where(value => value.ContractorBusinessPartnerId == actor.Value.BusinessPartnerId);
        var variations = await variationsQuery.OrderByDescending(value => value.RequestedDate).Select(value =>
            new QuantitySurveyDayworkVariationLookupDto(value.Id, value.ReferenceNumber ?? value.Id.ToString(), value.Title,
                value.VariationType, value.ContractId!.Value, value.Contract!.ContractNumber,
                value.ContractorBusinessPartnerId!.Value, value.Contract.BusinessPartner.PartnerName, value.Currency,
                value.EstimatedAmount ?? 0m, value.Status)).ToListAsync(token);

        var now = DateTime.UtcNow;
        var currencies = variations.Select(value => value.Currency).Distinct().ToList();
        var rates = await db.QuantitySurveyRateLibraryRates.AsNoTracking().Include(value => value.RateLibraryItem).ThenInclude(value => value.UnitOfMeasure)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published &&
                value.PublishedAt != null && value.EffectiveFrom <= now && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now) &&
                value.RateLibraryItem.IsActive && !value.RateLibraryItem.IsDeleted && currencies.Contains(value.CurrencyCodeSnapshot) &&
                (value.RateLibraryItem.Category == QuantitySurveyRateItemCategory.Labour ||
                 value.RateLibraryItem.Category == QuantitySurveyRateItemCategory.Material ||
                 value.RateLibraryItem.Category == QuantitySurveyRateItemCategory.Plant ||
                 value.RateLibraryItem.Category == QuantitySurveyRateItemCategory.Equipment))
            .OrderBy(value => value.RateLibraryItem.Category).ThenBy(value => value.RateLibraryItem.Code)
            .Select(value => new { value.Id, value.RateLibraryItemId, value.RateLibraryItem.Code, value.RateLibraryItem.Name,
                value.RateLibraryItem.Category, value.RateLibraryItem.UnitOfMeasureId, Unit = value.RateLibraryItem.UnitOfMeasure.Code,
                value.UnitRate, Currency = value.CurrencyCodeSnapshot, value.EffectiveFrom, value.EffectiveTo }).ToListAsync(token);

        var sheetsQuery = Query().Where(value => value.ProjectId == projectId);
        if (actor.HasValue) sheetsQuery = sheetsQuery.Where(value => value.ContractorBusinessPartnerId == actor.Value.BusinessPartnerId);
        var sheets = await sheetsQuery.OrderByDescending(value => value.WorkDate).ThenByDescending(value => value.CreatedAt).ToListAsync(token);
        return new QuantitySurveyDayworkWorkspaceDto
        {
            Variations = variations,
            Rates = rates.Select(value => new QuantitySurveyDayworkRateLookupDto(value.Id, value.RateLibraryItemId, value.Code, value.Name,
                QuantitySurveyDayworkRules.MapRateCategory(value.Category)!.Value, value.UnitOfMeasureId, value.Unit, value.UnitRate,
                value.Currency, value.EffectiveFrom, value.EffectiveTo)).ToList(),
            Sheets = sheets.Select(Map).ToList()
        };
    }

    public async Task<QuantitySurveyDayworkSheetDto> SaveExternalAsync(Guid projectId, SaveQuantitySurveyDayworkRequest request,
        string correlationId, CancellationToken token = default)
    {
        var actor = await RequireExternalActorAsync(projectId, true, request.Id, token);
        if (request.ClientRequestId == Guid.Empty || request.VariationOrderId == Guid.Empty || request.Lines.Count == 0)
            throw Validation("Select a Daywork or Additional Work record and at least one controlled rate line.");
        var workDate = request.WorkDate.ToUniversalTime().Date;
        if (workDate > DateTime.UtcNow.Date || workDate < DateTime.UtcNow.Date.AddYears(-2))
            throw Validation("The work date must be today or within the previous two years.");
        var location = RequiredText(request.WorkLocation, 2, 200, "Work location");
        var description = RequiredText(request.Description, 10, 2000, "Work description");
        var variation = await RequiredVariationAsync(projectId, request.VariationOrderId, actor.BusinessPartnerId, true, token);
        var policy = await ResolvePolicyAsync(variation, token);
        var rateIds = request.Lines.Select(value => value.RateLibraryRateId).Distinct().ToList();
        if (rateIds.Count != request.Lines.Count) throw Validation("Each controlled rate may appear only once per sheet.");
        var rates = await RequiredRatesAsync(rateIds, variation.Currency, workDate, token);
        var lines = request.Lines.Select((input, index) =>
        {
            if (!rates.TryGetValue(input.RateLibraryRateId, out var rate)) throw Validation("A selected rate is unavailable for the work date and contract currency.");
            var type = QuantitySurveyDayworkRules.MapRateCategory(rate.Item.Category)
                ?? throw Validation("Only Labour, Material, Plant or Equipment rates are permitted.");
            var quantity = decimal.Round(input.Quantity, 4, MidpointRounding.AwayFromZero);
            var amount = QuantitySurveyDayworkRules.LineAmount(quantity, rate.Rate.UnitRate);
            return new LineSnapshot(index + 1, type, rate.Rate.Id, rate.Item.Id, rate.Item.Code, rate.Item.Name,
                rate.Item.UnitOfMeasureId, rate.Item.UnitOfMeasure.Code, quantity, rate.Rate.UnitRate, amount,
                TrimOptional(input.Note, 1000), Hash(new { RateId = rate.Rate.Id, ItemId = rate.Item.Id, rate.Rate.Version, rate.Rate.UnitRate,
                    rate.Rate.CurrencyCodeSnapshot, rate.Rate.EffectiveFrom, rate.Rate.EffectiveTo, rate.Rate.SourceType,
                    rate.Rate.SourceReference, rate.Rate.CorrelationId }));
        }).ToList();
        var total = lines.Sum(value => value.Amount);
        if (total <= 0) throw Validation("The daywork sheet total must be greater than zero.");
        var requestHash = Hash(new { projectId, request.VariationOrderId, workDate, location, description, lines, total,
            actor.BusinessPartnerId, policy.ProfileId, policy.DecisionId, policy.EvidenceTemplateId, policy.PolicyHash });

        var strategy = db.Database.CreateExecutionStrategy();
        Guid id = Guid.Empty;
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var retry = await db.QuantitySurveyDayworkRevisions.AsNoTracking().FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
            if (retry is not null)
            {
                if ((request.Id.HasValue && retry.DayworkSheetId != request.Id.Value) || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
                id = retry.DayworkSheetId; await transaction.CommitAsync(token); return;
            }
            QuantitySurveyDayworkSheet entity;
            object? before = null;
            var action = QuantitySurveyAuditEventMap.CreateDayworkSheet;
            if (request.Id.HasValue)
            {
                entity = await RequiredAsync(request.Id.Value, true, token);
                if (entity.ProjectId != projectId || entity.ContractorBusinessPartnerId != actor.BusinessPartnerId)
                    throw new UnauthorizedAccessException("The sheet is not assigned to the linked contractor.");
                if (!QuantitySurveyDayworkRules.CanEdit(entity.Status)) throw Conflict("Only a Draft or Rejected sheet can be amended.");
                ApplyRowVersion(entity, request.RowVersion); before = Snapshot(entity); action = QuantitySurveyAuditEventMap.UpdateDayworkSheet;
                entity.LastMutationClientRequestId = request.ClientRequestId; entity.LastMutationRequestHash = requestHash;
                db.QuantitySurveyDayworkLines.RemoveRange(entity.Lines);
            }
            else
            {
                entity = new QuantitySurveyDayworkSheet
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ClientRequestId = request.ClientRequestId,
                    RequestHash = requestHash, SheetNumber = await NextNumberAsync(token), CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName, CreatedById = UserId
                };
                db.QuantitySurveyDayworkSheets.Add(entity);
            }
            entity.VariationOrderId = variation.Id; entity.ContractId = variation.ContractId!.Value;
            entity.ContractorBusinessPartnerId = actor.BusinessPartnerId; entity.WorkDate = workDate;
            entity.WorkLocation = location; entity.Description = description; entity.Status = QuantitySurveyDayworkSheetStatus.Draft;
            entity.Currency = variation.Currency.ToUpperInvariant(); entity.TotalAmount = total;
            entity.ConfigurationProfileId = policy.ProfileId; entity.VariationDecisionId = policy.DecisionId;
            entity.EvidenceMetadataTemplateId = policy.EvidenceTemplateId; entity.PolicyHash = policy.PolicyHash;
            entity.ContractorSignedById = null; entity.ContractorSignedAt = null; entity.ContractorSignatureHash = null;
            entity.VerifiedById = null; entity.VerifiedAt = null; entity.VerifierSignatureHash = null;
            entity.VerificationNote = null; entity.RejectionReason = null; entity.CorrelationId = Correlation(correlationId);
            entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
            entity.Lines = lines.Select(value => new QuantitySurveyDayworkLine
            {
                Id = Guid.NewGuid(), TenantId = TenantId, DayworkSheetId = entity.Id, Sequence = value.Sequence,
                LineType = value.Type, RateLibraryRateId = value.RateId, RateLibraryItemId = value.ItemId,
                ItemCodeSnapshot = value.Code, ItemNameSnapshot = value.Name, UnitOfMeasureId = value.UnitId,
                UnitOfMeasureSnapshot = value.Unit, Quantity = value.Quantity, UnitRate = value.UnitRate, Amount = value.Amount,
                Note = value.Note, SourceHash = value.SourceHash, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
            }).ToList();
            AddRevision(entity, request.ClientRequestId, requestHash, action, description, before, Snapshot(entity), correlationId, actor.BusinessPartnerId);
            AddAudit(entity, action, before, Snapshot(entity), correlationId);
            await SaveChangesAsync(token); id = entity.Id; await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear(); return Map(await RequiredAsync(id, false, token));
    }

    public async Task<QuantitySurveyDayworkEvidenceDto> UploadEvidenceAsync(Guid id, Guid clientRequestId, string title,
        string fileName, string contentType, long fileSize, Func<Stream> openRead, bool external, string correlationId,
        CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        Guid? actorPartnerId = null;
        if (external)
        {
            var actor = await RequireExternalActorAsync(entity.ProjectId, true, entity.Id, token);
            if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId) throw new UnauthorizedAccessException("The sheet is not assigned to the linked contractor.");
            actorPartnerId = actor.BusinessPartnerId;
        }
        else await RequireProjectAsync(entity.ProjectId);
        if (!QuantitySurveyDayworkRules.CanEdit(entity.Status)) throw Conflict("Evidence can be added only to a Draft or Rejected sheet.");
        if (clientRequestId == Guid.Empty || fileSize <= 0 || fileSize > MaximumEvidenceBytes) throw Validation("Select an evidence file up to 50 MB.");
        var safeTitle = RequiredText(title, 3, 200, "Evidence title"); var safeName = Path.GetFileName(fileName);
        await using var source = openRead(); using var memory = new MemoryStream(); await source.CopyToAsync(memory, token);
        var bytes = memory.ToArray(); if (bytes.LongLength != fileSize) throw Validation("The evidence file size changed during upload.");
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var requestHash = Hash(new { id, safeTitle, safeName, contentType, fileSize, checksum });
        var retry = await db.QuantitySurveyDayworkEvidence.AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == clientRequestId, token);
        if (retry is not null) { if (retry.DayworkSheetId != id || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict(); return MapEvidence(retry); }
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId &&
            value.Id == entity.EvidenceMetadataTemplateId && !value.IsDeleted && value.IsActive && value.PublishedAt != null, token)
            ?? throw Conflict("The frozen daywork-evidence DMS template is no longer active and Published.");
        var upload = await controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
            Category = ControlledFileUploadCategories.QuantitySurveyDayworkEvidence, FileName = safeName,
            ContentType = contentType, FileSize = bytes.LongLength, OpenReadStream = () => new MemoryStream(bytes, false)
        }, token);
        if (upload.Record.VirusScanStatus != FileVirusScanStatus.Clean || !FixedEquals(upload.ChecksumSha256, checksum))
        { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw Conflict("The centrally scanned evidence did not pass its integrity check."); }
        var evidenceId = Guid.NewGuid(); CentralDocumentRepositoryLink document;
        try
        {
            document = await centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId, ActorUserId = UserId, ActorName = UserName, FileUploadRecordId = upload.Record.Id,
                SourceModule = "QuantitySurvey", SourceLabel = "Quantity Survey daywork evidence",
                SourceEntityType = nameof(QuantitySurveyDayworkEvidence), SourceRecordId = evidenceId,
                SourceRecordReference = entity.SheetNumber, Title = safeTitle, DocumentType = "DayworkEvidence",
                MetadataTemplateCode = template.TemplateCode, AccessProfile = template.AccessProfile, VersionStatus = "Submitted",
                ChangeSummary = "Clean scanned daywork evidence retained in the central DMS.", RequirePublishedGovernance = true,
                MetadataValues = [new("dayworkSheetId", "Daywork sheet ID", entity.Id.ToString(), "guid"),
                    new("projectId", "Project ID", entity.ProjectId.ToString(), "guid"), new("checksumSha256", "Checksum SHA-256", checksum)]
            }, token);
        }
        catch { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw; }
        var evidence = new QuantitySurveyDayworkEvidence
        {
            Id = evidenceId, TenantId = TenantId, DayworkSheetId = id, ClientRequestId = clientRequestId, RequestHash = requestHash,
            Title = safeTitle, OriginalFileName = upload.Record.OriginalFileName, ContentType = contentType, FileSize = bytes.LongLength,
            ChecksumSha256 = checksum, FileUploadRecordId = document.FileUploadRecordId,
            CentralDocumentRecordId = document.DocumentRecordId, CentralDocumentVersionId = document.DocumentVersionId,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        };
        db.QuantitySurveyDayworkEvidence.Add(evidence);
        AddAudit(entity, QuantitySurveyAuditEventMap.AttachDayworkEvidence, null,
            new { evidence.Id, evidence.Title, evidence.ChecksumSha256, actorPartnerId }, correlationId);
        try { await SaveChangesAsync(token); }
        catch { await centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, token); throw; }
        return MapEvidence(evidence);
    }

    public async Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(Guid id, Guid evidenceId, bool external, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        if (external)
        {
            var actor = await RequireExternalActorAsync(entity.ProjectId, false, entity.Id, token);
            if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId) throw new UnauthorizedAccessException("The sheet is not assigned to the linked contractor.");
        }
        else await RequireProjectAsync(entity.ProjectId);
        var evidence = entity.Evidence.SingleOrDefault(value => value.Id == evidenceId && !value.IsDeleted)
            ?? throw new QuantitySurveyDayworkNotFoundException("The daywork evidence was not found.");
        return await centralDocuments.OpenAsync(TenantId, evidence.CentralDocumentRecordId, evidence.CentralDocumentVersionId, token)
               ?? throw Conflict("The central-DMS evidence content is unavailable.");
    }

    public async Task<QuantitySurveyDayworkSheetDto> SignExternalAsync(Guid id, QuantitySurveyDayworkActionRequest request,
        string correlationId, CancellationToken token = default)
    {
        var current = await RequiredAsync(id, false, token);
        var actor = await RequireExternalActorAsync(current.ProjectId, true, current.Id, token);
        if (actor.BusinessPartnerId != current.ContractorBusinessPartnerId) throw new UnauthorizedAccessException("The sheet is not assigned to the linked contractor.");
        return await MutateAsync(id, request, QuantitySurveyAuditEventMap.SignDayworkSheet, correlationId, actor.BusinessPartnerId, token, async value =>
        {
            if (!QuantitySurveyDayworkRules.CanEdit(value.Status)) throw Conflict("Only a Draft or Rejected sheet can be signed.");
            if (value.Lines.Count == 0 || value.Evidence.All(item => item.IsDeleted)) throw Conflict("Add controlled rate lines and at least one clean DMS evidence file before signing.");
            await ValidateFrozenAsync(value, token);
            var signedAt = DateTime.UtcNow;
            value.Status = QuantitySurveyDayworkSheetStatus.ContractorSigned; value.ContractorSignedById = UserId; value.ContractorSignedAt = signedAt;
            value.ContractorSignatureHash = Hash(new { value.Id, value.SheetNumber, value.WorkDate, value.TotalAmount, value.PolicyHash,
                Lines = value.Lines.OrderBy(line => line.Sequence).Select(line => new { line.SourceHash, line.Quantity, line.UnitRate, line.Amount }),
                Evidence = value.Evidence.Where(item => !item.IsDeleted).OrderBy(item => item.Id).Select(item => item.ChecksumSha256),
                ActorUserId = UserId, actor.BusinessPartnerId, SignedAt = signedAt });
            value.VerifiedById = null; value.VerifiedAt = null; value.VerifierSignatureHash = null;
            value.VerificationNote = null; value.RejectionReason = null;
        });
    }

    public Task<QuantitySurveyDayworkSheetDto> VerifyAsync(Guid id, QuantitySurveyDayworkActionRequest request, bool accept,
        string correlationId, CancellationToken token = default) => MutateAsync(id, request,
        accept ? QuantitySurveyAuditEventMap.VerifyDayworkSheet : QuantitySurveyAuditEventMap.RejectDayworkSheet,
        correlationId, null, token, async value =>
        {
            await RequireProjectAsync(value.ProjectId);
            if (value.Status != QuantitySurveyDayworkSheetStatus.ContractorSigned) throw Conflict("Only a contractor-signed sheet can be verified or rejected.");
            if (value.ContractorSignedById == UserId) throw Conflict("The contractor signer cannot verify the same sheet.");
            await ValidateFrozenAsync(value, token);
            var verifiedAt = DateTime.UtcNow;
            value.VerifiedById = UserId; value.VerifiedAt = verifiedAt; value.VerificationNote = RequiredText(request.Reason, 5, 2000, "Verification note");
            value.VerifierSignatureHash = Hash(new { value.Id, value.ContractorSignatureHash, value.TotalAmount,
                Decision = accept ? "Verified" : "Rejected", ActorUserId = UserId, VerifiedAt = verifiedAt, value.VerificationNote });
            value.Status = accept ? QuantitySurveyDayworkSheetStatus.Verified : QuantitySurveyDayworkSheetStatus.Rejected;
            value.RejectionReason = accept ? null : value.VerificationNote;
        });

    private async Task<QuantitySurveyDayworkSheetDto> MutateAsync(Guid id, QuantitySurveyDayworkActionRequest request, string action,
        string correlationId, Guid? actorPartnerId, CancellationToken token, Func<QuantitySurveyDayworkSheet, Task> mutation)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "Action reason");
        var requestHash = Hash(new { id, action, reason, request.RowVersion });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var value = await RequiredAsync(id, true, token);
            if (value.LastMutationClientRequestId == request.ClientRequestId)
            { if (!FixedEquals(value.LastMutationRequestHash, requestHash)) throw RetryConflict(); await transaction.CommitAsync(token); return; }
            if (await db.QuantitySurveyDayworkRevisions.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.ClientRequestId == request.ClientRequestId, token)) throw RetryConflict();
            ApplyRowVersion(value, request.RowVersion); var before = Snapshot(value); await mutation(value);
            value.LastMutationClientRequestId = request.ClientRequestId; value.LastMutationRequestHash = requestHash;
            value.CorrelationId = Correlation(correlationId); value.UpdatedAt = DateTime.UtcNow; value.UpdatedBy = UserName; value.LastModifiedById = UserId;
            AddRevision(value, request.ClientRequestId, requestHash, action, reason, before, Snapshot(value), correlationId, actorPartnerId);
            AddAudit(value, action, before, Snapshot(value), correlationId); await SaveChangesAsync(token); await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear(); return Map(await RequiredAsync(id, false, token));
    }

    private async Task ValidateFrozenAsync(QuantitySurveyDayworkSheet value, CancellationToken token)
    {
        var variation = await RequiredVariationAsync(value.ProjectId, value.VariationOrderId, value.ContractorBusinessPartnerId, true, token);
        if (variation.ContractId != value.ContractId || !string.Equals(variation.Currency, value.Currency, StringComparison.OrdinalIgnoreCase))
            throw Conflict("The parent variation contract or currency changed.");
        var policy = await ResolvePolicyAsync(variation, token);
        if (policy.ProfileId != value.ConfigurationProfileId || policy.DecisionId != value.VariationDecisionId ||
            policy.EvidenceTemplateId != value.EvidenceMetadataTemplateId || !FixedEquals(policy.PolicyHash, value.PolicyHash))
            throw Conflict("The effective daywork policy changed. Amend and re-sign the sheet.");
        var rates = await RequiredRatesAsync(value.Lines.Select(line => line.RateLibraryRateId).ToList(), value.Currency, value.WorkDate, token,
            requireCurrentlyPublished: false);
        foreach (var line in value.Lines)
        {
            if (!rates.TryGetValue(line.RateLibraryRateId, out var rate) ||
                !FixedEquals(line.SourceHash, Hash(new { RateId = rate.Rate.Id, ItemId = rate.Item.Id, rate.Rate.Version, rate.Rate.UnitRate,
                    rate.Rate.CurrencyCodeSnapshot, rate.Rate.EffectiveFrom, rate.Rate.EffectiveTo, rate.Rate.SourceType,
                    rate.Rate.SourceReference, rate.Rate.CorrelationId }))) throw Conflict("A frozen daywork rate source changed.");
        }
    }

    private async Task<Policy> ResolvePolicyAsync(ProjectVariationOrder variation, CancellationToken token)
    {
        if (!variation.ConfigurationProfileId.HasValue || !variation.VariationDecisionId.HasValue || !variation.EvidenceMetadataTemplateId.HasValue ||
            string.IsNullOrWhiteSpace(variation.PolicyHash)) throw Conflict("The parent variation has no frozen QS-DEC-011 governance.");
        var now = DateTime.UtcNow;
        var valid = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().AnyAsync(value => value.TenantId == TenantId &&
            value.Id == variation.ConfigurationProfileId && !value.IsDeleted && value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published &&
            value.PublishedAt != null && value.EffectiveFrom <= now && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now), token);
        var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId &&
            value.Id == variation.VariationDecisionId && value.ProfileId == variation.ConfigurationProfileId && value.DecisionKey == "QS-DEC-011" &&
            !value.IsDeleted && value.Status == QuantitySurveyConfigurationDecisionStatus.Approved &&
            value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
            value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified, token);
        if (!valid || decision is null) throw Conflict("The parent variation's QS-DEC-011 policy is no longer effective.");
        try
        {
            using var json = JsonDocument.Parse(decision.ValueJson);
            if (!json.RootElement.TryGetProperty("allowedTypes", out var allowed) || allowed.ValueKind != JsonValueKind.Array ||
                !allowed.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String &&
                    string.Equals(item.GetString(), variation.VariationType == ProjectVariationOrderTypes.Daywork
                        ? "Daywork" : "Additional Work", StringComparison.OrdinalIgnoreCase)))
                throw Conflict("QS-DEC-011 does not allow this daywork/additional-work family.");
        }
        catch (JsonException)
        {
            throw Conflict("The effective QS-DEC-011 configuration is invalid. Correct and republish the policy before continuing.");
        }
        var templateValid = await db.CentralDocumentMetadataTemplates.AsNoTracking().AnyAsync(value => value.TenantId == TenantId &&
            value.Id == variation.EvidenceMetadataTemplateId && !value.IsDeleted && value.IsActive && value.PublishedAt != null, token);
        if (!templateValid) throw Conflict("The frozen variation evidence DMS template is no longer active and Published.");
        return new(variation.ConfigurationProfileId.Value, variation.VariationDecisionId.Value,
            variation.EvidenceMetadataTemplateId.Value, variation.PolicyHash);
    }

    private async Task<ProjectVariationOrder> RequiredVariationAsync(Guid projectId, Guid variationId, Guid contractorId,
        bool requireEditable, CancellationToken token)
    {
        var value = await db.ProjectVariationOrders.AsNoTracking().Include(item => item.Contract).ThenInclude(item => item!.Tender)
            .ThenInclude(item => item.SourcePurchaseRequisition).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == variationId &&
                item.ProjectId == projectId && item.IsQuantitySurveyGoverned && !item.IsDeleted && item.ContractId.HasValue &&
                item.ContractorBusinessPartnerId == contractorId && item.Contract != null && item.Contract.ContractType == "Works" &&
                item.Contract.Status == "Active" && item.Contract.Tender.SourcePurchaseRequisition != null &&
                item.Contract.Tender.SourcePurchaseRequisition.ProjectId == projectId &&
                (item.VariationType == ProjectVariationOrderTypes.Daywork || item.VariationType == ProjectVariationOrderTypes.AdditionalWork), token)
            ?? throw Validation("Select a governed Daywork or Additional Work record assigned to this contractor and project.");
        if (requireEditable && value.Status is not (ProjectVariationOrderStatuses.Draft or ProjectVariationOrderStatuses.Rejected))
            throw Conflict("Daywork sheets can be prepared only while the parent variation is Draft or Rejected.");
        return value;
    }

    private async Task<Dictionary<Guid, RateSource>> RequiredRatesAsync(IReadOnlyCollection<Guid> ids, string currency,
        DateTime workDate, CancellationToken token, bool requireCurrentlyPublished = true)
    {
        var query = db.QuantitySurveyRateLibraryRates.AsNoTracking().Include(value => value.RateLibraryItem).ThenInclude(value => value.UnitOfMeasure)
            .Where(value => value.TenantId == TenantId && ids.Contains(value.Id) && !value.IsDeleted && value.PublishedAt != null &&
                value.EffectiveFrom.Date <= workDate.Date && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value.Date >= workDate.Date) &&
                value.CurrencyCodeSnapshot == currency && value.RateLibraryItem.IsActive && !value.RateLibraryItem.IsDeleted);
        query = requireCurrentlyPublished
            ? query.Where(value => value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published)
            : query.Where(value => value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published ||
                value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Retired);
        var values = await query.ToListAsync(token);
        if (values.Count != ids.Distinct().Count())
            throw Validation(requireCurrentlyPublished
                ? "Every selected rate must be Published, effective on the work date, active, and in the contract currency."
                : "Every frozen rate must remain traceable, effective on the work date, active, and in the contract currency.");
        return values.ToDictionary(value => value.Id, value => new RateSource(value, value.RateLibraryItem));
    }

    private async Task<ExternalActor> RequireExternalActorAsync(Guid projectId, bool requireUpload, Guid? sheetId, CancellationToken token)
    {
        var link = await db.BusinessPartnerUsers.IgnoreQueryFilters().AsNoTracking().Include(value => value.BusinessPartner).Include(value => value.User)
            .FirstOrDefaultAsync(value => value.UserId == UserId && value.IsActive && !value.IsDeleted && !value.BusinessPartner.IsDeleted &&
                value.BusinessPartner.TenantId == TenantId && value.User.TenantId == TenantId && value.User.IsActive, token)
            ?? throw new UnauthorizedAccessException("No active business-partner identity is linked to this portal user.");
        if (!link.BusinessPartner.IsActive || !BusinessPartnerLifecyclePolicy.IsOperationalRegistration(link.BusinessPartner.RegistrationStatus))
            throw new UnauthorizedAccessException("The linked business partner is not active.");
        _ = await projectService.GetExternalProjectByIdAsync(projectId, UserId)
            ?? throw new UnauthorizedAccessException("You do not have access to the selected project.");
        var project = await db.Projects.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == projectId, token);
        if (!project.ExternalPortalAccessEnabled || !project.ExternalCollaborationEnabled)
            throw new UnauthorizedAccessException("External collaboration is not enabled for this project.");
        if (project.BusinessPartnerId != link.BusinessPartnerId)
        {
            var allowed = await db.ProjectExternalAccessPolicies.AsNoTracking().AnyAsync(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && value.BusinessPartnerId == link.BusinessPartnerId && !value.IsDeleted &&
                (!requireUpload || value.CanUpload) && (value.ArtifactType == "Project" ||
                 (sheetId.HasValue && value.ArtifactType == "DayworkSheet" && value.ArtifactId == sheetId)), token);
            if (!allowed) throw new UnauthorizedAccessException("The project external-access policy does not permit this daywork action.");
        }
        return new(link.BusinessPartnerId);
    }

    private IQueryable<QuantitySurveyDayworkSheet> Query(bool tracked = false)
    {
        var query = db.QuantitySurveyDayworkSheets.Include(value => value.VariationOrder).Include(value => value.ContractorBusinessPartner)
            .Include(value => value.Lines).Include(value => value.Evidence).Where(value => value.TenantId == TenantId && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }
    private async Task<QuantitySurveyDayworkSheet> RequiredAsync(Guid id, bool tracked, CancellationToken token) =>
        await Query(tracked).SingleOrDefaultAsync(value => value.Id == id, token)
        ?? throw new QuantitySurveyDayworkNotFoundException("The governed daywork sheet was not found.");
    private async Task RequireProjectAsync(Guid projectId)
    { if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null) throw new UnauthorizedAccessException("You are not permitted to access the selected project."); }
    private async Task<string> NextNumberAsync(CancellationToken token)
    { var year = DateTime.UtcNow.Year; var count = await db.QuantitySurveyDayworkSheets.IgnoreQueryFilters().CountAsync(value => value.TenantId == TenantId && value.CreatedAt.Year == year, token); return $"DW-{year}-{count + 1:00000}"; }

    private void AddRevision(QuantitySurveyDayworkSheet value, Guid clientRequestId, string requestHash, string action,
        string reason, object? before, object after, string correlationId, Guid? actorPartnerId) => db.QuantitySurveyDayworkRevisions.Add(new()
        { Id = Guid.NewGuid(), TenantId = TenantId, DayworkSheetId = value.Id, ClientRequestId = clientRequestId, RequestHash = requestHash,
            Action = action, ActorUserId = UserId, ActorBusinessPartnerId = actorPartnerId, ActorName = UserName, ActorRoles = ActorRoles,
            CorrelationId = Correlation(correlationId), Reason = reason, BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private void AddAudit(QuantitySurveyDayworkSheet value, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = nameof(QuantitySurveyDayworkSheet), ResourceId = value.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QuantitySurvey", Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private async Task SaveChangesAsync(CancellationToken token)
    { try { await db.SaveChangesAsync(token); } catch (DbUpdateConcurrencyException) { throw Conflict("The daywork sheet changed. Refresh and retry."); }
      catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("IX_", StringComparison.OrdinalIgnoreCase) == true ||
          exception.InnerException?.Message.Contains("5200", StringComparison.OrdinalIgnoreCase) == true)
      { throw Conflict("The daywork sheet conflicts with an existing governed source or lifecycle rule."); } }

    private static QuantitySurveyDayworkSheetDto Map(QuantitySurveyDayworkSheet value) => new()
    {
        Id = value.Id, ProjectId = value.ProjectId, VariationOrderId = value.VariationOrderId,
        VariationReference = value.VariationOrder.ReferenceNumber ?? value.VariationOrderId.ToString(), VariationType = value.VariationOrder.VariationType,
        SheetNumber = value.SheetNumber, WorkDate = value.WorkDate, WorkLocation = value.WorkLocation, Description = value.Description,
        Status = value.Status, Currency = value.Currency, TotalAmount = value.TotalAmount,
        Contractor = value.ContractorBusinessPartner.PartnerName, ContractorSignedAt = value.ContractorSignedAt,
        VerifiedAt = value.VerifiedAt, VerificationNote = value.VerificationNote, RejectionReason = value.RejectionReason,
        CertificateEligible = value.Status == QuantitySurveyDayworkSheetStatus.Verified && value.VariationOrder.Status == ProjectVariationOrderStatuses.Approved,
        RowVersion = Convert.ToBase64String(value.RowVersion),
        Lines = value.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.Sequence).Select(line => new QuantitySurveyDayworkLineDto
        { Id = line.Id, LineType = line.LineType, RateLibraryRateId = line.RateLibraryRateId, Code = line.ItemCodeSnapshot,
            Name = line.ItemNameSnapshot, Unit = line.UnitOfMeasureSnapshot, Quantity = line.Quantity, UnitRate = line.UnitRate,
            Amount = line.Amount, Note = line.Note }).ToList(),
        Evidence = value.Evidence.Where(item => !item.IsDeleted).OrderBy(item => item.CreatedAt).Select(MapEvidence).ToList()
    };
    private static QuantitySurveyDayworkEvidenceDto MapEvidence(QuantitySurveyDayworkEvidence value) => new()
    { Id = value.Id, Title = value.Title, FileName = value.OriginalFileName, FileSize = value.FileSize, ChecksumSha256 = value.ChecksumSha256 };
    private static object Snapshot(QuantitySurveyDayworkSheet value) => new
    { value.Id, value.ProjectId, value.VariationOrderId, value.ContractId, value.ContractorBusinessPartnerId, value.SheetNumber,
        value.WorkDate, value.WorkLocation, value.Description, value.Status, value.Currency, value.TotalAmount,
        value.ConfigurationProfileId, value.VariationDecisionId, value.EvidenceMetadataTemplateId, value.PolicyHash,
        value.ContractorSignedById, value.ContractorSignedAt, value.ContractorSignatureHash, value.VerifiedById, value.VerifiedAt,
        value.VerifierSignatureHash, value.VerificationNote, value.RejectionReason,
        Lines = value.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.Sequence).Select(line =>
            new { line.LineType, line.RateLibraryRateId, line.ItemCodeSnapshot, line.Quantity, line.UnitRate, line.Amount, line.SourceHash }) };
    private static void ApplyRowVersion(QuantitySurveyDayworkSheet value, string? encoded)
    { if (string.IsNullOrWhiteSpace(encoded)) throw Validation("A daywork row version is required. Refresh and retry."); byte[] expected;
      try { expected = Convert.FromBase64String(encoded); } catch (FormatException) { throw Validation("The daywork row version is invalid. Refresh and retry."); }
      if (expected.Length != value.RowVersion.Length || !CryptographicOperations.FixedTimeEquals(expected, value.RowVersion)) throw Conflict("The daywork sheet changed. Refresh and retry."); }
    private static string RequiredText(string? value, int min, int max, string label) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length < min || value.Trim().Length > max
            ? throw Validation($"{label} must contain {min} to {max} characters.") : value.Trim();
    private static string? TrimOptional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(max, value.Trim().Length)];
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
    { if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false; var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right); return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
    private static QuantitySurveyDayworkValidationException Validation(string message) => new(message);
    private static QuantitySurveyDayworkConflictException Conflict(string message) => new(message);
    private static QuantitySurveyDayworkConflictException RetryConflict() => Conflict("This client request identifier is already bound to different daywork inputs.");
    private readonly record struct ExternalActor(Guid BusinessPartnerId);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid EvidenceTemplateId, string PolicyHash);
    private sealed record RateSource(QuantitySurveyRateLibraryRate Rate, QuantitySurveyRateLibraryItem Item);
    private sealed record LineSnapshot(int Sequence, QuantitySurveyDayworkLineType Type, Guid RateId, Guid ItemId,
        string Code, string Name, Guid UnitId, string Unit, decimal Quantity, decimal UnitRate, decimal Amount, string? Note, string SourceHash);
}
