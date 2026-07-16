using System.Globalization;
using System.Text.Json;
using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/land-acquisitions")]
[Authorize]
public class LandAcquisitionsController : ControllerBase
{
    private const string WorkflowEntityType = "LandAcquisition";
    private static readonly IReadOnlyDictionary<int, string[]> RequiredStageInputs = new Dictionary<int, string[]>
    {
        [0] = ["projectReference", "parcelLocation", "estimatedSize", "coordinates", "vendorId", "vendorName", "acquisitionType", "intendedUse", "openingNotes"],
        [1] = ["inspectionDate", "inspectionOfficer", "soilType", "topography", "hasAccessRoad", "hasUtilities", "siteAccessRoute", "drainageCondition", "existingDevelopment", "zoningClassification", "planningSchemeReference", "isFloodProne", "planningCompatible", "accessConfirmed", "environmentalClearance", "utilityAvailability", "encumbranceObserved", "assessmentRecommendation", "approvalNotes"],
        [2] = ["cadastreDescription", "regionId", "districtId", "townId", "totalArea", "areaUnit", "beacon1Index", "beacon1NorthingFeet", "beacon1EastingFeet", "beacon1Bearing", "beacon1DistanceFeet", "beacon2Index", "beacon2NorthingFeet", "beacon2EastingFeet", "beacon2Bearing", "beacon2DistanceFeet", "beacon3Index", "beacon3NorthingFeet", "beacon3EastingFeet", "beacon3Bearing", "beacon3DistanceFeet", "beacon4Index", "beacon4NorthingFeet", "beacon4EastingFeet", "beacon4Bearing", "beacon4DistanceFeet", "boundaryCoordinates", "surveyorName", "licensedSurveyor", "surveyDate", "surveyorSignedDate", "surveyPlanNumber", "mapSheetNumber", "surveyStatus", "isCertified", "beaconCount", "regionalSurveyorName", "regionalSurveyorSignedDate", "mainPortion", "coordinateReference", "surveyNotes"],
        [3] = ["cadastralMatch", "cadastralMatchVerified", "overlapCleared", "boundaryConfirmed", "verificationReference", "verificationOfficer", "verificationDate", "verificationNotes"],
        [4] = ["ownershipType", "ownerName", "contactNumber", "address", "acquisitionMethod", "tenureType", "ownershipStartDate", "percentage", "isCurrentOwner", "identificationType", "identificationNumber", "interestHeld", "classificationRisk", "dateGapReason", "ownerRegionId", "ownerDistrictId", "ownerTownId", "ownerBeacon1NorthingFeet", "ownerBeacon1EastingFeet", "ownerBeacon2NorthingFeet", "ownerBeacon2EastingFeet", "ownerBeacon3NorthingFeet", "ownerBeacon3EastingFeet", "ownerBeacon4NorthingFeet", "ownerBeacon4EastingFeet", "witnessName1", "witnessContact1", "witnessRelation1", "witnessAddress1", "witnessSwornOath1", "witnessOathSwornBefore1", "witnessOathSwornDate1", "witnessName2", "witnessContact2", "witnessRelation2", "witnessAddress2", "witnessSwornOath2", "witnessOathSwornBefore2", "witnessOathSwornDate2", "classificationNotes"],
        [5] = ["dueDiligenceStatus", "titleSearchCompleted", "ownerIdentityVerified", "authorityToSellVerified", "encumbrancesFound", "litigationFound", "landsCommissionSearchReference", "searchReference", "ownershipVerified", "verificationNotes"],
        [6] = ["sellerQuote", "offerAmount", "counterOffer", "negotiatedValue", "paymentType", "offerTerms", "agreementDay", "agreementMonth", "agreementYear", "isAccepted", "agreementGenerated", "negotiationNotes"],
        [7] = ["agreementDate", "isFamilyLand", "isStoolLand", "rootOfTitle", "specialConditions", "grantorName", "grantorAddress", "grantorPhone", "granteeName", "granteeAddress", "granteePhone", "agreementPaymentType", "agreementPaymentAmount", "agreementPaymentDueDate", "agreementPaymentMethod", "agreementWitness1Name", "agreementWitness1Address", "agreementWitness2Name", "agreementWitness2Address", "legalReviewComplete", "financeReviewComplete", "boardApprovalReference", "approvalConditions"],
        [8] = ["instrumentType", "instrumentNumber", "documentName", "documentType", "executionDate", "executedBy", "counterpartySignatory", "isExecuted", "witnessDetails", "executionNotes"],
        [9] = ["consentAuthority", "consentDate", "applicationNumber", "submissionDate", "documentName", "documentType", "isApproved", "consentNotes"],
        [10] = ["approvalReference", "approvalDate", "consentConditions", "approvalNotes"],
        [11] = ["propertyValue", "stampDutyAmount", "assessmentAuthority", "assessmentReference", "assessmentDate", "isApproved", "assessmentNotes"],
        [12] = ["financeApprovalReference", "approvedDutyAmount", "approverName", "approvalNotes"],
        [13] = ["documentName", "receiptNumber", "paymentReference", "paymentDate", "amountPaid", "paymentMethod", "isPaid", "paymentNotes"],
        [14] = ["registryOffice", "registrationNumber", "volume", "folio", "registrationDate", "isRegistered", "documentName", "registrationNotes"],
        [15] = ["assetCode", "assetNumber", "parcelIdentifier", "registrationNumber", "ownerName", "assetLocation", "assetCategory", "size", "sizeUnit", "assetStatus", "purpose", "zoningClassification", "ownershipVerification", "capitalizationValue", "glAccount", "custodian", "assetNotes"]
    };
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowService _workflowService;
    private readonly IEstateManagedAssetService _managedAssetService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICentralDocumentRenditionService _renditionService;
    private readonly ILogger<LandAcquisitionsController> _logger;

    public LandAcquisitionsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowService workflowService,
        IEstateManagedAssetService managedAssetService,
        IFileStorageService fileStorageService,
        ICentralDocumentRenditionService renditionService,
        ILogger<LandAcquisitionsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowService = workflowService;
        _managedAssetService = managedAssetService;
        _fileStorageService = fileStorageService;
        _renditionService = renditionService;
        _logger = logger;
    }

    [HttpGet("workflow-board")]
    public async Task<ActionResult<LandAcquisitionBoardDto>> GetWorkflowBoard(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var acquisitions = await BaseQuery(tenantId)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ToListAsync(cancellationToken);

        var isAdministrator = IsWorkflowAdministrator();
        var visibleByStage = new Dictionary<int, List<LandAcquisition>>();
        foreach (var acquisition in acquisitions)
        {
            if (await CanAccessStageAsync(acquisition, acquisition.StageOrder, userId: GetUserId(), isAdministrator))
            {
                if (!visibleByStage.TryGetValue(acquisition.StageOrder, out var visible))
                {
                    visible = [];
                    visibleByStage[acquisition.StageOrder] = visible;
                }
                visible.Add(acquisition);
            }
        }

        var stages = StageDefinitions
            .Where(stage => isAdministrator || HasRole(stage.RequiredRole) || visibleByStage.ContainsKey(stage.Order))
            .Select(stage =>
            {
                var items = visibleByStage.TryGetValue(stage.Order, out var visible)
                    ? visible.Select(ToItemDto).ToList()
                    : [];
                return new LandAcquisitionStageDto(
                stage.Id,
                stage.Order,
                stage.Title,
                stage.Description,
                stage.WorkspaceKind,
                stage.DemoUiRoute,
                stage.Method,
                stage.RequiredRole,
                items.Count,
                stage.PrimaryAction,
                stage.RejectAction,
                items);
            }).ToList();

        return Ok(new LandAcquisitionBoardDto(stages));
    }

    [HttpPost("workspace")]
    public async Task<ActionResult<LandAcquisitionWorkspaceResponse>> SaveWorkspace(
        [FromBody] LandAcquisitionWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProcedureId < 0 || request.ProcedureId > 15)
        {
            return BadRequest("Invalid acquisition procedure.");
        }

        var tenantId = GetTenantId();
        var userId = GetUserId();
        var acquisition = request.AcquisitionId.HasValue
            ? await BaseQuery(tenantId).FirstOrDefaultAsync(item => item.Id == request.AcquisitionId.Value, cancellationToken)
            : null;

        if (acquisition == null && !IsWorkflowAdministrator() && !HasRole(StageDefinitions[0].RequiredRole))
        {
            return Forbid();
        }

        if (acquisition != null &&
            (request.ProcedureId != acquisition.StageOrder ||
             !await CanAccessStageAsync(acquisition, request.ProcedureId, userId, IsWorkflowAdministrator())))
        {
            return Forbid();
        }

        if (acquisition == null)
        {
            acquisition = new LandAcquisition
            {
                TenantId = tenantId,
                CreatedById = userId,
                StageOrder = 0,
                CurrentStage = AcquisitionProcedure.LandIdentification,
                Status = LandAcquisitionStatus.PendingIdentification,
                ProjectReference = await GenerateProjectReferenceAsync(tenantId, cancellationToken),
                Location = "Unspecified"
            };
            _context.LandAcquisitions.Add(acquisition);
        }

        ApplyWorkspace(acquisition, request);
        SaveWorkspaceSnapshot(acquisition, request.ProcedureId, request.Values);
        acquisition.LastModifiedById = userId;
        acquisition.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var missingInputs = GetMissingStageInputs(acquisition, request.ProcedureId);
        return Ok(new LandAcquisitionWorkspaceResponse
        {
            Success = true,
            Message = "Land acquisition workspace saved.",
            AcquisitionId = acquisition.Id,
            Item = ToItemDto(acquisition),
            StageInputsComplete = missingInputs.Count == 0,
            MissingInputs = missingInputs
        });
    }

    [HttpGet("{id:guid}/workspace/{procedureId:int}")]
    public async Task<ActionResult<LandAcquisitionWorkspaceDataResponse>> GetWorkspace(
        Guid id,
        int procedureId,
        CancellationToken cancellationToken)
    {
        if (!RequiredStageInputs.ContainsKey(procedureId))
        {
            return BadRequest("Invalid acquisition procedure.");
        }

        var acquisition = await BaseQuery(GetTenantId())
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (acquisition == null)
        {
            return NotFound("Land acquisition was not found.");
        }

        if (procedureId != acquisition.StageOrder ||
            !await CanAccessStageAsync(acquisition, procedureId, GetUserId(), IsWorkflowAdministrator()))
        {
            return Forbid();
        }

        var snapshots = ReadWorkspaceSnapshots(acquisition);
        snapshots.TryGetValue(procedureId, out var values);
        var responseValues = values?.ToDictionary(
            pair => pair.Key,
            pair => (object?)pair.Value,
            StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (procedureId == (int)AcquisitionProcedure.OwnershipClassification &&
            snapshots.TryGetValue((int)AcquisitionProcedure.LandIdentification, out var identification))
        {
            CopySnapshotValue(identification, responseValues, "vendorId");
            CopySnapshotValue(identification, responseValues, "vendorName");
        }

        var missingInputs = GetMissingStageInputs(acquisition, procedureId);
        return Ok(new LandAcquisitionWorkspaceDataResponse
        {
            AcquisitionId = id,
            ProcedureId = procedureId,
            Values = responseValues,
            StageInputsComplete = missingInputs.Count == 0,
            MissingInputs = missingInputs
        });
    }

    [HttpGet("{id:guid}/summary")]
    public async Task<ActionResult<LandAcquisitionSummaryResponse>> GetSummary(
        Guid id,
        CancellationToken cancellationToken)
    {
        var acquisition = await BaseQuery(GetTenantId())
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (acquisition == null)
        {
            return NotFound("Land acquisition was not found.");
        }

        if (!await CanAccessStageAsync(acquisition, acquisition.StageOrder, GetUserId(), IsWorkflowAdministrator()))
        {
            return Forbid();
        }

        var snapshots = ReadWorkspaceSnapshots(acquisition);
        var stages = snapshots
            .Where(pair => pair.Key <= acquisition.StageOrder)
            .OrderBy(pair => pair.Key)
            .Select(pair => new LandAcquisitionStageSummaryDto
            {
                ProcedureId = pair.Key,
                Title = StageDefinitions.FirstOrDefault(stage => stage.Order == pair.Key)?.Title ?? $"Stage {pair.Key + 1}",
                Values = pair.Value.ToDictionary(
                    value => value.Key,
                    value => (object?)value.Value,
                    StringComparer.OrdinalIgnoreCase)
            })
            .ToList();

        return Ok(new LandAcquisitionSummaryResponse
        {
            AcquisitionId = acquisition.Id,
            Stages = stages
        });
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<IReadOnlyList<LandAcquisitionDocumentDto>>> GetDocuments(
        Guid id,
        CancellationToken cancellationToken)
    {
        var acquisition = await BaseQuery(GetTenantId())
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (acquisition == null)
        {
            return NotFound("Land acquisition was not found.");
        }

        if (!await CanAccessStageAsync(acquisition, acquisition.StageOrder, GetUserId(), IsWorkflowAdministrator()))
        {
            return Forbid();
        }

        var documents = acquisition.Documents
            .Where(document => !document.IsDeleted)
            .OrderByDescending(document => document.CreatedAt)
            .Select(ToDocumentDto)
            .ToList();

        return Ok(new { success = true, data = documents });
    }

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<LandAcquisitionDocumentDto>> UploadDocument(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] int procedureId,
        [FromForm] string documentType,
        [FromForm] string? documentName,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "Select a document to upload." });
        }

        if (!RequiredStageInputs.ContainsKey(procedureId))
        {
            return BadRequest(new { success = false, message = "Invalid acquisition procedure." });
        }

        var tenantId = GetTenantId();
        var acquisition = await BaseQuery(tenantId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (acquisition == null)
        {
            return NotFound("Land acquisition was not found.");
        }

        if (procedureId != acquisition.StageOrder ||
            !await CanAccessStageAsync(acquisition, procedureId, GetUserId(), IsWorkflowAdministrator()))
        {
            return Forbid();
        }

        var folder = $"estate/land-acquisitions/{id:N}/{procedureId}";
        await using var stream = file.OpenReadStream();
        var filePath = await _fileStorageService.UploadFileAsync(stream, file.FileName, folder);
        var document = new LandAcquisitionDocument
        {
            TenantId = tenantId,
            LandAcquisitionId = acquisition.Id,
            FileName = NormalizeDocumentFileName(file.FileName, documentName),
            FilePath = filePath,
            DocumentType = string.IsNullOrWhiteSpace(documentType) ? "Other" : documentType.Trim(),
            Procedure = (AcquisitionProcedure)procedureId,
            CreatedById = GetUserId(),
            CreatedBy = _currentUserService.UserName,
            CreatedAt = DateTime.UtcNow
        };

        acquisition.Documents.Add(document);
        acquisition.UpdatedAt = DateTime.UtcNow;
        acquisition.LastModifiedById = GetUserId();
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, data = ToDocumentDto(document), message = "Land acquisition document uploaded." });
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var acquisition = await BaseQuery(GetTenantId())
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (acquisition == null)
        {
            return NotFound("Land acquisition was not found.");
        }

        if (!await CanAccessStageAsync(acquisition, acquisition.StageOrder, GetUserId(), IsWorkflowAdministrator()))
        {
            return Forbid();
        }

        var document = acquisition.Documents.FirstOrDefault(item => item.Id == documentId && !item.IsDeleted);
        if (document == null)
        {
            return NotFound(new { success = false, message = "Land acquisition document was not found." });
        }

        var stream = await _fileStorageService.DownloadFileAsync(document.FilePath, document.Id);
        return File(stream, ContentTypeFor(document.FileName), document.FileName);
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/viewer-pdf")]
    public async Task<IActionResult> ViewDocumentPdf(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var acquisition = await BaseQuery(GetTenantId())
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (acquisition == null)
        {
            return NotFound("Land acquisition was not found.");
        }

        if (!await CanAccessStageAsync(acquisition, acquisition.StageOrder, GetUserId(), IsWorkflowAdministrator()))
        {
            return Forbid();
        }

        var document = acquisition.Documents.FirstOrDefault(item => item.Id == documentId && !item.IsDeleted);
        if (document == null)
        {
            return NotFound(new { success = false, message = "Land acquisition document was not found." });
        }

        var contentType = ContentTypeFor(document.FileName);
        var sourceStream = await _fileStorageService.DownloadFileAsync(document.FilePath, document.Id);
        if (IsPdfFile(document.FileName, contentType))
        {
            return File(sourceStream, "application/pdf", enableRangeProcessing: true);
        }

        await using (sourceStream)
        {
            var preview = await _renditionService.CreatePdfPreviewAsync(
                new CentralDocumentPdfPreviewRequest(
                    sourceStream,
                    document.FileName,
                    contentType),
                cancellationToken);

            if (!preview.Success || preview.PdfStream is null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = preview.ErrorMessage ?? "This document cannot be previewed in the PDF viewer."
                });
            }

            return File(preview.PdfStream, "application/pdf", preview.FileName ?? $"{Path.GetFileNameWithoutExtension(document.FileName)}.pdf", enableRangeProcessing: true);
        }
    }

    [HttpPost("workflow-action")]
    public async Task<ActionResult<LandAcquisitionWorkflowActionResponse>> RunWorkflowAction(
        [FromBody] LandAcquisitionWorkflowActionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var acquisition = await BaseQuery(tenantId)
            .FirstOrDefaultAsync(item => item.Id == request.AcquisitionId, cancellationToken);

        if (acquisition == null)
        {
            return NotFound("Land acquisition was not found.");
        }

        if (IsInitialSubmission(acquisition, request) &&
            !IsWorkflowAdministrator() &&
            !HasRole(StageDefinitions[0].RequiredRole))
        {
            return Forbid();
        }

        if (!IsReject(request.ActionType))
        {
            var missingInputs = GetMissingStageInputs(acquisition, acquisition.StageOrder);
            if (missingInputs.Count > 0)
            {
                return BadRequest(new
                {
                    message = "Complete every required stage input before submitting or approving this acquisition.",
                    missingInputs
                });
            }
        }

        try
        {
            WorkflowIntegrationResult result;
            if (IsInitialSubmission(acquisition, request))
            {
                result = await _workflowIntegrationService.SubmitAsync(WorkflowEntityType, acquisition.Id);
                if (!result.ExecutionResult.Success)
                {
                    return BadRequest(result.ExecutionResult.Message ?? "Workflow submission failed.");
                }

                acquisition.SubmittedAt = DateTime.UtcNow;
                acquisition.SubmittedById = userId;
                acquisition.Status = LandAcquisitionStatus.PendingApproval;
            }
            else
            {
                var action = IsReject(request.ActionType) ? "Reject" : "Approve";
                var canApprove = await _workflowIntegrationService.CanUserApproveAsync(WorkflowEntityType, acquisition.Id, userId);
                if (!canApprove)
                {
                    return Forbid();
                }

                result = await _workflowIntegrationService.ProcessApprovalAsync(
                    WorkflowEntityType,
                    acquisition.Id,
                    userId,
                    action,
                    request.Comments);

                if (!result.ExecutionResult.Success)
                {
                    return BadRequest(result.ExecutionResult.Message ?? "Workflow approval failed.");
                }
            }

            ApplyWorkflowOutcome(acquisition, result.Outcome, request, userId);
            await SyncStageFromWorkflowAsync(acquisition, result.Outcome, cancellationToken);

            acquisition.LastModifiedById = userId;
            acquisition.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new LandAcquisitionWorkflowActionResponse
            {
                Success = true,
                Message = result.Outcome == WorkflowOutcome.Rejected
                    ? "Land acquisition was rejected by workflow."
                    : "Land acquisition workflow action completed.",
                WorkflowOutcome = result.Outcome.ToString(),
                Item = ToItemDto(acquisition)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run land acquisition workflow action for {AcquisitionId}", request.AcquisitionId);
            return StatusCode(500, "Unable to complete land acquisition workflow action.");
        }
    }

    [HttpPost("{id:guid}/ready-for-project-management")]
    public async Task<ActionResult<EstateManagedAssetDto>> MarkReadyForProjectManagement(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var acquisition = await BaseQuery(tenantId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (acquisition == null)
        {
            return NotFound("Land acquisition was not found.");
        }

        var survey = acquisition.CadastralSurveys.FirstOrDefault();
        if (survey == null || string.IsNullOrWhiteSpace(survey.BoundaryCoordinates))
        {
            return BadRequest("Demarcate the land boundary before marking it ready for project management.");
        }

        var asset = acquisition.LandAssets.FirstOrDefault();
        var managedAsset = await _managedAssetService.PublishLandAcquisitionAsync(new LandAcquisitionEstateHandoffDto
        {
            LandAcquisitionId = acquisition.Id,
            ProjectReference = acquisition.ProjectReference,
            AssetCode = asset?.AssetCode,
            AssetNumber = asset?.AssetNumber,
            ParcelIdentifier = asset?.ParcelIdentifier,
            Name = asset?.ParcelIdentifier ?? $"{acquisition.ProjectReference} land",
            Description = asset?.Notes,
            Location = asset?.Location ?? acquisition.Location,
            Purpose = asset?.Purpose ?? acquisition.IntendedUse,
            AreaSquareMeters = ToSquareMeters(survey.AreaSize, survey.AreaUnit, acquisition.EstimatedSize),
            ValuationAmount = asset is { CapitalizationValue: > 0 } ? asset.CapitalizationValue : null,
            BoundaryCoordinates = survey.BoundaryCoordinates,
            SurveyPlanNumber = survey.PlanNumber,
            MapSheetNumber = survey.MapSheetNumber,
            ZoningClassification = asset?.ZoningClassification ?? acquisition.PhysicalAssessment?.ZoningClassification,
            PlanningComplianceStatus = acquisition.SuitableForDueDiligence ? "Compliant" : "Pending",
            GisLayerReference = survey.MapSheetNumber ?? survey.PlanNumber,
            BoundaryVerified = true,
            Notes = "Demarcated land published from land acquisition for project management pull."
        });

        acquisition.InternalApproved = true;
        acquisition.UpdatedAt = DateTime.UtcNow;
        acquisition.LastModifiedById = GetUserId();
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = managedAsset,
            message = "Demarcated land is ready for project management."
        });
    }

    private IQueryable<LandAcquisition> BaseQuery(Guid tenantId)
        => _context.LandAcquisitions
            .Include(item => item.Documents)
            .Include(item => item.Notes)
            .Include(item => item.ChecklistResponses)
            .Include(item => item.CadastralSurveys)
            .Include(item => item.OwnershipHistories)
            .Include(item => item.NegotiationOffers)
            .Include(item => item.Registrations)
            .Include(item => item.LandAssets)
            .Include(item => item.PhysicalAssessment)
            .Include(item => item.Agreement)
            .Include(item => item.LandInstrument)
            .Include(item => item.StatutoryConsent)
            .Include(item => item.StampDutyAssessment)
            .Include(item => item.StampDutyPayment)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted);

    private void ApplyWorkspace(LandAcquisition acquisition, LandAcquisitionWorkspaceRequest request)
    {
        var values = request.Values;
        switch ((AcquisitionProcedure)request.ProcedureId)
        {
            case AcquisitionProcedure.LandIdentification:
                acquisition.ProjectReference = Text(values, "projectReference") ?? acquisition.ProjectReference;
                acquisition.Location = Text(values, "parcelLocation") ?? Text(values, "location") ?? acquisition.Location;
                acquisition.IntendedUse = Text(values, "intendedUse") ?? acquisition.IntendedUse;
                acquisition.EstimatedSize = Decimal(values, "estimatedSize") ?? acquisition.EstimatedSize;
                acquisition.Coordinates = Text(values, "coordinates") ?? acquisition.Coordinates;
                var openingNotes = Text(values, "openingNotes");
                if (!string.IsNullOrWhiteSpace(openingNotes) && acquisition.Notes.All(note => note.Note != openingNotes))
                {
                    acquisition.Notes.Add(new LandAcquisitionNote
                    {
                        TenantId = acquisition.TenantId,
                        Note = openingNotes,
                        Stage = AcquisitionProcedure.LandIdentification,
                        StageOrder = 0,
                        InputType = "Opening"
                    });
                }
                break;

            case AcquisitionProcedure.SuitabilityApproval:
                acquisition.PhysicalAssessment ??= Child(new LandPhysicalAssessment(), acquisition);
                acquisition.PhysicalAssessment.PlanningCompatible = Bool(values, "planningCompatible");
                acquisition.PhysicalAssessment.AccessConfirmed = Bool(values, "accessConfirmed");
                acquisition.PhysicalAssessment.EnvironmentalClearance = Bool(values, "environmentalClearance");
                acquisition.PhysicalAssessment.UtilityAvailability = Bool(values, "utilityAvailability");
                acquisition.PhysicalAssessment.IsFloodProne = Bool(values, "isFloodProne") || Bool(values, "floodProne");
                acquisition.PhysicalAssessment.SoilType = Text(values, "soilType") ?? acquisition.PhysicalAssessment.SoilType;
                acquisition.PhysicalAssessment.Topography = Text(values, "topography") ?? acquisition.PhysicalAssessment.Topography;
                acquisition.PhysicalAssessment.ZoningClassification =
                    Text(values, "zoningClassification") ?? Text(values, "classification") ?? acquisition.PhysicalAssessment.ZoningClassification;
                acquisition.PhysicalAssessment.Notes = Text(values, "approvalNotes") ?? Text(values, "assessmentNotes") ?? Text(values, "notes");
                acquisition.PlanningUploaded = acquisition.PhysicalAssessment.PlanningCompatible;
                acquisition.SuitableForDueDiligence = acquisition.PhysicalAssessment.PlanningCompatible && acquisition.PhysicalAssessment.AccessConfirmed;
                break;

            case AcquisitionProcedure.CadastralSurvey:
                var survey = acquisition.CadastralSurveys.FirstOrDefault() ?? Child(new CadastralSurvey(), acquisition);
                survey.Description = Text(values, "cadastreDescription") ?? Text(values, "description") ?? survey.Description;
                survey.AreaSize = Decimal(values, "totalArea") ?? Decimal(values, "areaSize") ?? survey.AreaSize;
                survey.AreaUnit = Text(values, "areaUnit") ?? survey.AreaUnit;
                survey.Latitude = Decimal(values, "latitude") ?? survey.Latitude;
                survey.Longitude = Decimal(values, "longitude") ?? survey.Longitude;
                survey.NorthEastLat = Decimal(values, "northEastLat") ?? survey.NorthEastLat;
                survey.NorthEastLng = Decimal(values, "northEastLng") ?? survey.NorthEastLng;
                survey.NorthWestLat = Decimal(values, "northWestLat") ?? survey.NorthWestLat;
                survey.NorthWestLng = Decimal(values, "northWestLng") ?? survey.NorthWestLng;
                survey.SouthEastLat = Decimal(values, "southEastLat") ?? survey.SouthEastLat;
                survey.SouthEastLng = Decimal(values, "southEastLng") ?? survey.SouthEastLng;
                survey.SouthWestLat = Decimal(values, "southWestLat") ?? survey.SouthWestLat;
                survey.SouthWestLng = Decimal(values, "southWestLng") ?? survey.SouthWestLng;
                survey.BoundaryCoordinates = Text(values, "boundaryCoordinates") ?? survey.BoundaryCoordinates;
                survey.SurveyorName = Text(values, "surveyorName") ?? survey.SurveyorName;
                survey.SurveyDate = Date(values, "surveyDate") ?? survey.SurveyDate;
                survey.SurveyorSignedDate = Date(values, "surveyorSignedDate") ?? survey.SurveyorSignedDate;
                survey.PlanNumber = Text(values, "planNumber") ?? Text(values, "surveyPlanNumber") ?? survey.PlanNumber;
                survey.MapSheetNumber = Text(values, "mapSheetNumber") ?? survey.MapSheetNumber;
                survey.BeaconCount = Text(values, "beaconCount") ?? survey.BeaconCount;
                survey.RegionalSurveyorName = Text(values, "regionalSurveyorName") ?? survey.RegionalSurveyorName;
                survey.RegionalSurveyorSignedDate = Date(values, "regionalSurveyorSignedDate") ?? survey.RegionalSurveyorSignedDate;
                survey.MainPortion = Bool(values, "mainPortion");
                survey.CoordinateReference = Text(values, "coordinateReference") ?? survey.CoordinateReference;
                survey.Notes = Text(values, "surveyNotes") ?? survey.Notes;
                break;

            case AcquisitionProcedure.SurveyVerification:
                var verification = acquisition.CadastralSurveys.FirstOrDefault() ?? Child(new CadastralSurvey(), acquisition);
                verification.CadastralMatch = Bool(values, "cadastralMatch");
                verification.OverlapCleared = Bool(values, "overlapCleared");
                verification.BoundaryConfirmed = Bool(values, "boundaryConfirmed");
                verification.VerificationReference = Text(values, "verificationReference") ?? verification.VerificationReference;
                verification.Notes = Text(values, "verificationNotes") ?? verification.Notes;
                break;

            case AcquisitionProcedure.OwnershipClassification:
            case AcquisitionProcedure.OwnershipVerification:
                var owner = acquisition.OwnershipHistories.FirstOrDefault() ?? Child(new OwnershipHistory(), acquisition);
                var isCurrentOwner = Bool(values, "isCurrentOwner");
                owner.BusinessPartnerId = isCurrentOwner && Guid.TryParse(Text(values, "vendorId"), out var businessPartnerId)
                    ? businessPartnerId
                    : null;
                owner.OwnerName = Text(values, "beneficialOwner") ?? Text(values, "ownerName") ?? Text(values, "vendorName") ?? owner.OwnerName;
                owner.ContactNumber = Text(values, "contactNumber") ?? owner.ContactNumber;
                owner.Address = Text(values, "address") ?? owner.Address;
                owner.InterestHeld = Text(values, "interestHeld") ?? owner.InterestHeld;
                owner.RiskLevel = Text(values, "classificationRisk") ?? owner.RiskLevel;
                owner.SearchReference = Text(values, "searchReference") ?? owner.SearchReference;
                owner.TitleSearchCompleted = Bool(values, "titleSearchCompleted");
                owner.OwnerIdentityVerified = Bool(values, "ownerIdentityVerified");
                owner.AuthorityToSellVerified = Bool(values, "authorityToSellVerified");
                owner.TenureType = Text(values, "tenureType") ?? owner.TenureType;
                owner.OwnershipStartDate = Date(values, "ownershipStartDate") ?? owner.OwnershipStartDate;
                owner.OwnershipEndDate = Date(values, "ownershipEndDate") ?? owner.OwnershipEndDate;
                owner.OwnershipPercentage = Decimal(values, "percentage") ?? Decimal(values, "ownershipPercentage") ?? owner.OwnershipPercentage;
                owner.IsCurrentOwner = isCurrentOwner;
                owner.IdentificationType = Text(values, "identificationType") ?? owner.IdentificationType;
                owner.IdentificationNumber = Text(values, "identificationNumber") ?? owner.IdentificationNumber;
                owner.DateGapReason = Text(values, "dateGapReason") ?? owner.DateGapReason;
                owner.WitnessName1 = Text(values, "witnessName1") ?? owner.WitnessName1;
                owner.WitnessContact1 = Text(values, "witnessContact1") ?? owner.WitnessContact1;
                owner.WitnessRelationship1 = Text(values, "witnessRelation1") ?? Text(values, "witnessRelationship1") ?? owner.WitnessRelationship1;
                owner.WitnessAddress1 = Text(values, "witnessAddress1") ?? owner.WitnessAddress1;
                owner.WitnessSwornOath1 = Bool(values, "witnessSwornOath1") || Bool(values, "oathSworn1");
                owner.WitnessOathSwornBefore1 = Text(values, "swornBefore") ?? Text(values, "witnessOathSwornBefore1") ?? owner.WitnessOathSwornBefore1;
                owner.WitnessOathSwornDate1 = Date(values, "swornDate") ?? Date(values, "witnessOathSwornDate1") ?? owner.WitnessOathSwornDate1;
                owner.WitnessName2 = Text(values, "witnessName2") ?? owner.WitnessName2;
                owner.WitnessContact2 = Text(values, "witnessContact2") ?? owner.WitnessContact2;
                owner.WitnessRelationship2 = Text(values, "witnessRelation2") ?? Text(values, "witnessRelationship2") ?? owner.WitnessRelationship2;
                owner.WitnessAddress2 = Text(values, "witnessAddress2") ?? owner.WitnessAddress2;
                owner.WitnessSwornOath2 = Bool(values, "witnessSwornOath2") || Bool(values, "oathSworn2");
                owner.WitnessOathSwornBefore2 = Text(values, "swornBefore2") ?? Text(values, "witnessOathSwornBefore2") ?? owner.WitnessOathSwornBefore2;
                owner.WitnessOathSwornDate2 = Date(values, "swornDate2") ?? Date(values, "witnessOathSwornDate2") ?? owner.WitnessOathSwornDate2;
                owner.Notes = Text(values, "verificationNotes") ?? Text(values, "classificationNotes") ?? owner.Notes;
                acquisition.OwnershipType = ParseOwnership(Text(values, "ownershipType")) ?? acquisition.OwnershipType;
                owner.OwnershipType = acquisition.OwnershipType;
                owner.AcquisitionMethod = ParseAcquisitionMethod(Text(values, "acquisitionMethod") ?? Text(values, "acquisitionType")) ?? owner.AcquisitionMethod;
                break;

            case AcquisitionProcedure.AgreementNegotiation:
                var offer = acquisition.NegotiationOffers.FirstOrDefault() ?? Child(new NegotiationOffer(), acquisition);
                offer.OpeningOffer = Decimal(values, "openingOffer") ?? Decimal(values, "offerAmount") ?? Decimal(values, "amount") ?? offer.OpeningOffer;
                offer.CounterOffer = Decimal(values, "counterOffer") ?? offer.CounterOffer;
                offer.NegotiatedValue = Decimal(values, "negotiatedValue") ?? Decimal(values, "agreedAmount") ?? offer.NegotiatedValue;
                offer.SellerQuote = Decimal(values, "sellerQuote") ?? offer.SellerQuote;
                offer.PaymentTerms = Text(values, "paymentTerms") ?? Text(values, "offerTerms") ?? Text(values, "terms") ?? offer.PaymentTerms;
                offer.PaymentType = Text(values, "paymentType") ?? offer.PaymentType;
                offer.AgreementDay = Text(values, "agreementDay") ?? offer.AgreementDay;
                offer.AgreementMonth = Text(values, "agreementMonth") ?? offer.AgreementMonth;
                offer.AgreementYear = Text(values, "agreementYear") ?? offer.AgreementYear;
                offer.AgreementGenerated = Bool(values, "agreementGenerated");
                offer.IsAccepted = Bool(values, "isAccepted") || offer.IsAccepted;
                offer.Notes = Text(values, "negotiationNotes") ?? offer.Notes;
                break;

            case AcquisitionProcedure.AgreementApproval:
                acquisition.Agreement ??= Child(new LandAgreement(), acquisition);
                acquisition.Agreement.LegalReviewComplete = Bool(values, "legalReviewComplete");
                acquisition.Agreement.FinanceReviewComplete = Bool(values, "financeReviewComplete");
                acquisition.Agreement.BoardApprovalReference = Text(values, "boardApprovalReference") ?? acquisition.Agreement.BoardApprovalReference;
                acquisition.Agreement.ApprovalConditions = Text(values, "approvalConditions") ?? acquisition.Agreement.ApprovalConditions;
                acquisition.Agreement.AgreementDate = Date(values, "agreementDate") ?? acquisition.Agreement.AgreementDate ?? DateTime.UtcNow;
                acquisition.Agreement.IsFamilyLand = Bool(values, "isFamilyLand");
                acquisition.Agreement.IsStoolLand = Bool(values, "isStoolLand");
                acquisition.Agreement.RootOfTitle = Text(values, "rootOfTitle") ?? acquisition.Agreement.RootOfTitle;
                acquisition.Agreement.SpecialConditions = Text(values, "specialConditions") ?? acquisition.Agreement.SpecialConditions;
                acquisition.Agreement.PartyDetails = Text(values, "partyDetails") ?? BuildAgreementPartyDetails(values) ?? acquisition.Agreement.PartyDetails;
                acquisition.Agreement.PaymentSchedule = Text(values, "paymentSchedule") ?? BuildAgreementPaymentDetails(values) ?? acquisition.Agreement.PaymentSchedule;
                acquisition.Agreement.WitnessDetails = Text(values, "agreementWitnessDetails") ?? BuildAgreementWitnessDetails(values) ?? acquisition.Agreement.WitnessDetails;
                break;

            case AcquisitionProcedure.LandInstrumentExecution:
                acquisition.LandInstrument ??= Child(new LandInstrument(), acquisition);
                acquisition.LandInstrument.InstrumentType = Text(values, "instrumentType") ?? acquisition.LandInstrument.InstrumentType;
                acquisition.LandInstrument.InstrumentNumber = Text(values, "instrumentNumber") ?? acquisition.LandInstrument.InstrumentNumber;
                acquisition.LandInstrument.ExecutionDate = Date(values, "executionDate") ?? acquisition.LandInstrument.ExecutionDate;
                acquisition.LandInstrument.ExecutedBy = Text(values, "executedBy") ?? acquisition.LandInstrument.ExecutedBy;
                acquisition.LandInstrument.CounterpartySignatory = Text(values, "counterpartySignatory") ?? acquisition.LandInstrument.CounterpartySignatory;
                acquisition.LandInstrument.WitnessDetails = Text(values, "witnessDetails") ?? acquisition.LandInstrument.WitnessDetails;
                acquisition.LandInstrument.Notes = Text(values, "executionNotes") ?? acquisition.LandInstrument.Notes;
                acquisition.LandInstrument.IsExecuted = Bool(values, "isExecuted") || acquisition.LandInstrument.ExecutionDate.HasValue;
                break;

            case AcquisitionProcedure.StatutoryConsent:
            case AcquisitionProcedure.StatutoryConsentApproval:
                acquisition.StatutoryConsent ??= Child(new StatutoryConsent(), acquisition);
                acquisition.StatutoryConsent.ConsentAuthority = Text(values, "consentAuthority") ?? acquisition.StatutoryConsent.ConsentAuthority;
                acquisition.StatutoryConsent.ApplicationNumber = Text(values, "applicationNumber") ?? acquisition.StatutoryConsent.ApplicationNumber;
                acquisition.StatutoryConsent.ConsentDate = Date(values, "consentDate") ?? acquisition.StatutoryConsent.ConsentDate;
                acquisition.StatutoryConsent.SubmissionDate = Date(values, "submissionDate") ?? acquisition.StatutoryConsent.SubmissionDate;
                acquisition.StatutoryConsent.ApprovalReference = Text(values, "approvalReference") ?? acquisition.StatutoryConsent.ApprovalReference;
                acquisition.StatutoryConsent.ApprovalDate = Date(values, "approvalDate") ?? acquisition.StatutoryConsent.ApprovalDate;
                acquisition.StatutoryConsent.Conditions = Text(values, "consentConditions") ?? Text(values, "consentNotes");
                acquisition.StatutoryConsent.IsApproved = Bool(values, "isApproved") || acquisition.StatutoryConsent.ApprovalDate.HasValue;
                break;

            case AcquisitionProcedure.StampDutyAssessment:
            case AcquisitionProcedure.StampDutyAssessmentApproval:
                acquisition.StampDutyAssessment ??= Child(new StampDutyAssessment(), acquisition);
                acquisition.StampDutyAssessment.AssessedValue = Decimal(values, "assessedValue") ?? Decimal(values, "propertyValue") ?? acquisition.StampDutyAssessment.AssessedValue;
                acquisition.StampDutyAssessment.DutyAmount = Decimal(values, "dutyAmount") ?? Decimal(values, "stampDutyAmount") ?? Decimal(values, "approvedDutyAmount") ?? acquisition.StampDutyAssessment.DutyAmount;
                acquisition.StampDutyAssessment.AssessmentReference = Text(values, "assessmentReference") ?? acquisition.StampDutyAssessment.AssessmentReference;
                acquisition.StampDutyAssessment.AssessmentDate = Date(values, "assessmentDate") ?? acquisition.StampDutyAssessment.AssessmentDate;
                acquisition.StampDutyAssessment.AssessmentAuthority = Text(values, "assessmentAuthority") ?? acquisition.StampDutyAssessment.AssessmentAuthority;
                acquisition.StampDutyAssessment.FinanceApprovalReference = Text(values, "financeApprovalReference") ?? acquisition.StampDutyAssessment.FinanceApprovalReference;
                acquisition.StampDutyAssessment.ApproverName = Text(values, "approverName") ?? acquisition.StampDutyAssessment.ApproverName;
                acquisition.StampDutyAssessment.Notes = Text(values, "assessmentNotes") ?? Text(values, "approvalNotes");
                acquisition.StampDutyAssessment.IsApproved = Bool(values, "isApproved") || !string.IsNullOrWhiteSpace(acquisition.StampDutyAssessment.FinanceApprovalReference);
                break;

            case AcquisitionProcedure.StampDutyPayment:
                acquisition.StampDutyPayment ??= Child(new StampDutyPayment(), acquisition);
                acquisition.StampDutyPayment.ReceiptNumber = Text(values, "receiptNumber") ?? Text(values, "paymentReference") ?? acquisition.StampDutyPayment.ReceiptNumber;
                acquisition.StampDutyPayment.PaymentReference = Text(values, "paymentReference") ?? acquisition.StampDutyPayment.PaymentReference;
                acquisition.StampDutyPayment.PaymentDate = Date(values, "paymentDate") ?? acquisition.StampDutyPayment.PaymentDate;
                acquisition.StampDutyPayment.AmountPaid = Decimal(values, "amountPaid") ?? acquisition.StampDutyPayment.AmountPaid;
                acquisition.StampDutyPayment.PaymentMethod = Text(values, "paymentMethod") ?? acquisition.StampDutyPayment.PaymentMethod;
                acquisition.StampDutyPayment.Notes = Text(values, "paymentNotes") ?? acquisition.StampDutyPayment.Notes;
                acquisition.StampDutyPayment.IsPaid = acquisition.StampDutyPayment.AmountPaid > 0;
                break;

            case AcquisitionProcedure.LandsCommissionRegistration:
                var registration = acquisition.Registrations.FirstOrDefault() ?? Child(new LandRegistration(), acquisition);
                registration.RegistryOffice = Text(values, "registryOffice");
                registration.RegistrationNumber = Text(values, "registrationNumber");
                registration.Volume = Text(values, "volume");
                registration.Folio = Text(values, "folio");
                registration.RegistrationDate = Date(values, "registrationDate");
                registration.Notes = Text(values, "registrationNotes");
                registration.IsRegistered = !string.IsNullOrWhiteSpace(registration.RegistrationNumber);
                break;

            case AcquisitionProcedure.LandAssetCreation:
                var asset = acquisition.LandAssets.FirstOrDefault() ?? Child(new LandAsset(), acquisition);
                asset.AssetCode = Text(values, "assetCode") ?? Text(values, "assetNumber") ?? asset.AssetCode;
                asset.AssetNumber = Text(values, "assetNumber") ?? asset.AssetNumber;
                asset.ParcelIdentifier = Text(values, "parcelIdentifier") ?? asset.ParcelIdentifier;
                asset.RegistrationNumber = Text(values, "registrationNumber") ?? asset.RegistrationNumber;
                asset.OwnerName = Text(values, "ownerName") ?? asset.OwnerName;
                asset.Location = Text(values, "assetLocation") ?? Text(values, "location") ?? asset.Location;
                asset.AssetCategory = Text(values, "assetCategory") ?? Text(values, "assetType") ?? asset.AssetCategory;
                asset.Size = Decimal(values, "size") ?? asset.Size;
                asset.SizeUnit = Text(values, "sizeUnit") ?? asset.SizeUnit;
                asset.Status = Text(values, "assetStatus") ?? asset.Status;
                asset.Purpose = Text(values, "purpose") ?? asset.Purpose;
                asset.ZoningClassification = Text(values, "zoningClassification") ?? asset.ZoningClassification;
                asset.OwnershipVerification = Text(values, "ownershipVerification") ?? asset.OwnershipVerification;
                asset.CapitalizationValue = Decimal(values, "capitalizationValue") ?? asset.CapitalizationValue;
                asset.GlAccount = Text(values, "glAccount") ?? asset.GlAccount;
                asset.Custodian = Text(values, "custodian") ?? asset.Custodian;
                asset.Notes = Text(values, "assetNotes") ?? asset.Notes;
                break;
        }
    }

    private static Dictionary<int, Dictionary<string, JsonElement>> ReadWorkspaceSnapshots(LandAcquisition acquisition)
    {
        if (string.IsNullOrWhiteSpace(acquisition.WorkspaceDataJson))
        {
            return new Dictionary<int, Dictionary<string, JsonElement>>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, Dictionary<string, JsonElement>>>(acquisition.WorkspaceDataJson)
                ?? new Dictionary<int, Dictionary<string, JsonElement>>();
        }
        catch (JsonException)
        {
            return new Dictionary<int, Dictionary<string, JsonElement>>();
        }
    }

    private static void SaveWorkspaceSnapshot(
        LandAcquisition acquisition,
        int procedureId,
        Dictionary<string, object?> values)
    {
        var snapshots = ReadWorkspaceSnapshots(acquisition);
        snapshots[procedureId] = values.ToDictionary(
            pair => pair.Key,
            pair => JsonSerializer.SerializeToElement(pair.Value),
            StringComparer.OrdinalIgnoreCase);
        acquisition.WorkspaceDataJson = JsonSerializer.Serialize(snapshots);
    }

    private static IReadOnlyList<string> GetMissingStageInputs(LandAcquisition acquisition, int procedureId)
    {
        if (!RequiredStageInputs.TryGetValue(procedureId, out var requiredInputs))
        {
            return ["Unknown acquisition stage"];
        }

        var snapshots = ReadWorkspaceSnapshots(acquisition);
        if (!snapshots.TryGetValue(procedureId, out var values))
        {
            return requiredInputs;
        }

        var missing = requiredInputs
            .Where(key => !values.TryGetValue(key, out var value) || !HasInputValue(value))
            .ToList();

        if (procedureId == (int)AcquisitionProcedure.OwnershipClassification &&
            values.TryGetValue("isCurrentOwner", out var currentOwnerValue) &&
            currentOwnerValue.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            if (currentOwnerValue.GetBoolean())
            {
                if (!values.TryGetValue("vendorId", out var vendorId) || !HasInputValue(vendorId))
                {
                    missing.Add("vendorId");
                }
            }
            else if (!values.TryGetValue("ownershipEndDate", out var endDate) || !HasInputValue(endDate))
            {
                missing.Add("ownershipEndDate");
            }
        }

        return missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void CopySnapshotValue(
        IReadOnlyDictionary<string, JsonElement> source,
        IDictionary<string, object?> destination,
        string key)
    {
        if (!destination.ContainsKey(key) && source.TryGetValue(key, out var value))
        {
            destination[key] = value;
        }
    }

    private static bool HasInputValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => true
        };

    private bool IsWorkflowAdministrator()
        => (_currentUserService.Roles ?? Array.Empty<string>()).Any(role =>
            role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("SystemAdmin", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("WorkflowAdmin", StringComparison.OrdinalIgnoreCase));

    private bool HasRole(string role)
        => (_currentUserService.Roles ?? Array.Empty<string>()).Any(current =>
            current.Equals(role, StringComparison.OrdinalIgnoreCase));

    private async Task<bool> CanAccessStageAsync(
        LandAcquisition acquisition,
        int stageOrder,
        Guid userId,
        bool isAdministrator)
    {
        if (isAdministrator) return true;
        var stage = StageDefinitions.FirstOrDefault(candidate => candidate.Order == stageOrder);
        if (stage == null) return false;
        if (stageOrder == 0) return HasRole(stage.RequiredRole);
        if (userId == Guid.Empty) return false;
        return await _workflowIntegrationService.CanUserApproveAsync(WorkflowEntityType, acquisition.Id, userId);
    }

    private T Child<T>(T child, LandAcquisition acquisition) where T : TenantEntity
    {
        child.TenantId = acquisition.TenantId;
        if (child is LandPhysicalAssessment physical) physical.LandAcquisition = acquisition;
        if (child is CadastralSurvey survey) acquisition.CadastralSurveys.Add(survey);
        if (child is OwnershipHistory owner) acquisition.OwnershipHistories.Add(owner);
        if (child is NegotiationOffer offer) acquisition.NegotiationOffers.Add(offer);
        if (child is LandAgreement agreement) agreement.LandAcquisition = acquisition;
        if (child is LandInstrument instrument) instrument.LandAcquisition = acquisition;
        if (child is StatutoryConsent consent) consent.LandAcquisition = acquisition;
        if (child is StampDutyAssessment assessment) assessment.LandAcquisition = acquisition;
        if (child is StampDutyPayment payment) payment.LandAcquisition = acquisition;
        if (child is LandRegistration registration) acquisition.Registrations.Add(registration);
        if (child is LandAsset asset) acquisition.LandAssets.Add(asset);
        return child;
    }

    private static bool IsInitialSubmission(LandAcquisition acquisition, LandAcquisitionWorkflowActionRequest request)
        => !IsReject(request.ActionType) &&
           acquisition.StageOrder == 0 &&
           (acquisition.Status == LandAcquisitionStatus.PendingIdentification || acquisition.Status == LandAcquisitionStatus.Rejected);

    private static bool IsReject(string? actionType)
        => string.Equals(actionType, "reject", StringComparison.OrdinalIgnoreCase);

    private static void ApplyWorkflowOutcome(
        LandAcquisition acquisition,
        WorkflowOutcome outcome,
        LandAcquisitionWorkflowActionRequest request,
        Guid userId)
    {
        if (outcome == WorkflowOutcome.Rejected || IsReject(request.ActionType))
        {
            acquisition.Status = LandAcquisitionStatus.Rejected;
            acquisition.RejectionReason = request.Comments;
            return;
        }

        acquisition.RejectionReason = null;
        if (outcome == WorkflowOutcome.Approved && acquisition.StageOrder >= 15)
        {
            acquisition.Status = LandAcquisitionStatus.AssetCreated;
            acquisition.ApprovedAt = DateTime.UtcNow;
            acquisition.ApprovedById = userId;
            return;
        }

        acquisition.Status = LandAcquisitionStatus.PendingApproval;
    }

    private async Task SyncStageFromWorkflowAsync(
        LandAcquisition acquisition,
        WorkflowOutcome outcome,
        CancellationToken cancellationToken)
    {
        if (outcome == WorkflowOutcome.Rejected)
        {
            return;
        }

        var current = await _workflowService.GetCurrentWorkflowStepAsync(WorkflowEntityType, acquisition.Id);
        var matched = current == null
            ? null
            : StageDefinitions.FirstOrDefault(stage =>
                string.Equals(stage.Title, current.StepName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage.WorkflowStepName, current.StepName, StringComparison.OrdinalIgnoreCase));

        if (matched != null)
        {
            acquisition.StageOrder = matched.Order;
            acquisition.CurrentStage = (AcquisitionProcedure)matched.Order;
            return;
        }

        if (outcome == WorkflowOutcome.Pending)
        {
            var next = Math.Min(15, acquisition.StageOrder + 1);
            acquisition.StageOrder = next;
            acquisition.CurrentStage = (AcquisitionProcedure)next;
            return;
        }

        if (outcome == WorkflowOutcome.Approved)
        {
            acquisition.StageOrder = 15;
            acquisition.CurrentStage = AcquisitionProcedure.LandAssetCreation;
        }

        await Task.CompletedTask;
    }

    private LandAcquisitionItemDto ToItemDto(LandAcquisition item)
    {
        var owner = item.OwnershipHistories.FirstOrDefault();
        var snapshots = ReadWorkspaceSnapshots(item);
        snapshots.TryGetValue(0, out var identification);
        var identifiedVendorName = identification != null &&
                                   identification.TryGetValue("vendorName", out var vendorNameValue) &&
                                   vendorNameValue.ValueKind == JsonValueKind.String
            ? vendorNameValue.GetString()
            : null;
        var identifiedAcquisitionType = identification != null &&
                                        identification.TryGetValue("acquisitionType", out var acquisitionTypeValue) &&
                                        acquisitionTypeValue.ValueKind == JsonValueKind.String
            ? acquisitionTypeValue.GetString()
            : null;
        var missingInputs = GetMissingStageInputs(item, item.StageOrder);
        return new LandAcquisitionItemDto(
            item.Id,
            item.ProjectReference,
            item.Location,
            StatusLabel(item.Status),
            item.StageOrder,
            StageDefinitions.FirstOrDefault(stage => stage.Order == item.StageOrder)?.Title ?? item.CurrentStage.ToString(),
            item.IntendedUse,
            item.EstimatedSize > 0 ? $"{item.EstimatedSize:N2} acres" : "Not recorded",
            owner?.OwnerName ?? identifiedVendorName,
            owner?.AcquisitionMethod.ToString() ?? identifiedAcquisitionType,
            item.Documents.Count(document => !document.IsDeleted),
            item.Notes.Count(note => !note.IsDeleted),
            (item.UpdatedAt ?? item.CreatedAt).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            item.NegotiationOffers.FirstOrDefault()?.NegotiatedValue?.ToString("C", CultureInfo.GetCultureInfo("en-GH")),
            owner?.RiskLevel,
            missingInputs.Count == 0,
            missingInputs);
    }

    private static LandAcquisitionDocumentDto ToDocumentDto(LandAcquisitionDocument document)
        => new()
        {
            Id = document.Id,
            LandAcquisitionId = document.LandAcquisitionId,
            FileName = document.FileName,
            DocumentType = document.DocumentType,
            DocumentName = document.FileName,
            ProcedureId = (int)document.Procedure,
            Procedure = document.Procedure.ToString(),
            UploadedAt = document.CreatedAt,
            UploadedBy = document.CreatedBy
        };

    private async Task<string> GenerateProjectReferenceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var count = await _context.LandAcquisitions
            .Where(item => item.TenantId == tenantId)
            .CountAsync(cancellationToken);
        return $"ACQ-{DateTime.UtcNow:yyyy}-{count + 1:0000}";
    }

    private Guid GetTenantId()
        => _currentUserService.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    private Guid GetUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : Guid.Empty;

    private static string? Text(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is JsonElement json)
        {
            return json.ValueKind == JsonValueKind.String ? json.GetString() : json.ToString();
        }

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool Bool(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value == null)
        {
            return false;
        }

        if (value is JsonElement json)
        {
            return json.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(json.GetString(), out var parsed) && parsed,
                _ => false
            };
        }

        return value is bool boolean ? boolean : bool.TryParse(value.ToString(), out var parsedBoolean) && parsedBoolean;
    }

    private static decimal? Decimal(Dictionary<string, object?> values, string key)
    {
        var text = Text(values, key);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var clean = new string(text.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static DateTime? Date(Dictionary<string, object?> values, string key)
    {
        var text = Text(values, key);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value) ? value : null;
    }

    private static string? BuildAgreementPartyDetails(Dictionary<string, object?> values)
        => JoinDetails(
            ("Grantor / Seller", Text(values, "grantorName")),
            ("Grantor Address", Text(values, "grantorAddress")),
            ("Grantor Phone", Text(values, "grantorPhone")),
            ("Grantee / Buyer", Text(values, "granteeName")),
            ("Grantee Address", Text(values, "granteeAddress")),
            ("Grantee Phone", Text(values, "granteePhone")));

    private static string? BuildAgreementPaymentDetails(Dictionary<string, object?> values)
        => JoinDetails(
            ("Payment Type", Text(values, "agreementPaymentType")),
            ("Amount", Text(values, "agreementPaymentAmount")),
            ("Due Date", Text(values, "agreementPaymentDueDate")),
            ("Method", Text(values, "agreementPaymentMethod")));

    private static string? BuildAgreementWitnessDetails(Dictionary<string, object?> values)
        => JoinDetails(
            ("Witness 1", Text(values, "agreementWitness1Name")),
            ("Witness 1 Address", Text(values, "agreementWitness1Address")),
            ("Witness 2", Text(values, "agreementWitness2Name")),
            ("Witness 2 Address", Text(values, "agreementWitness2Address")));

    private static string? JoinDetails(params (string Label, string? Value)[] items)
    {
        var lines = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{item.Label}: {item.Value!.Trim()}")
            .ToArray();

        return lines.Length == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static string ContentTypeFor(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };

    private static bool IsPdfFile(string fileName, string? contentType)
        => (!string.IsNullOrWhiteSpace(contentType)
                && contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            || fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDocumentFileName(string fileName, string? documentName)
    {
        if (string.IsNullOrWhiteSpace(documentName))
        {
            return fileName;
        }

        var cleanName = documentName.Trim();
        return string.IsNullOrWhiteSpace(Path.GetExtension(cleanName))
            ? $"{cleanName}{Path.GetExtension(fileName)}"
            : cleanName;
    }

    private static decimal? ToSquareMeters(decimal? areaSize, string? areaUnit, decimal estimatedSize)
    {
        if (areaSize.HasValue && areaSize.Value > 0)
        {
            var unit = (areaUnit ?? "sqm").Trim().ToLowerInvariant();
            return unit switch
            {
                "acre" or "acres" => areaSize.Value * 4046.8564224m,
                "hectare" or "hectares" or "ha" => areaSize.Value * 10000m,
                _ => areaSize.Value
            };
        }

        return estimatedSize > 0 ? estimatedSize * 4046.8564224m : null;
    }

    private static LandOwnershipType? ParseOwnership(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized switch
        {
            "stool" or "skin" or "stoolorskin" => LandOwnershipType.StoolOrSkin,
            "family" => LandOwnershipType.Family,
            "private" or "privateindividual" => LandOwnershipType.PrivateIndividual,
            "state" or "stateorvested" => LandOwnershipType.StateOrVested,
            "mixedinterest" => LandOwnershipType.MixedInterest,
            _ => null
        };
    }

    private static LandAcquisitionMethod? ParseAcquisitionMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized switch
        {
            "purchase" or "directpurchase" => LandAcquisitionMethod.Purchase,
            "gift" => LandAcquisitionMethod.Gift,
            "inheritance" => LandAcquisitionMethod.Inheritance,
            "courtorder" => LandAcquisitionMethod.CourtOrder,
            "leasehold" or "lease" => LandAcquisitionMethod.Leasehold,
            "assignment" => LandAcquisitionMethod.Assignment,
            "conveyance" => LandAcquisitionMethod.Conveyance,
            _ => null
        };
    }

    private static string StatusLabel(LandAcquisitionStatus status)
        => status switch
        {
            LandAcquisitionStatus.PendingIdentification => "Draft",
            LandAcquisitionStatus.Submitted => "Submitted",
            LandAcquisitionStatus.PendingApproval => "Pending Approval",
            LandAcquisitionStatus.Rejected => "Rejected",
            LandAcquisitionStatus.Completed => "Approved",
            LandAcquisitionStatus.AssetCreated => "Approved",
            _ => status.ToString()
        };

    private sealed record StageDefinition(
        int Id,
        int Order,
        string Title,
        string WorkflowStepName,
        string Description,
        string WorkspaceKind,
        string DemoUiRoute,
        string Method,
        string RequiredRole,
        string PrimaryAction,
        string? RejectAction);

    private static readonly IReadOnlyList<StageDefinition> StageDefinitions = new List<StageDefinition>
    {
        new(0, 0, "Parcel Identification", "Parcel Identification", "Identify the land parcel, intended use, location, size, and opening notes.", "parcel-identification", "/LandParcel/ParcelIdentificationView", "GET", "Estate Officer", "Submit for Suitability Approval", "Return Identification"),
        new(1, 1, "Suitability Approval", "Suitability Approval", "Review planning fit, access, environmental constraints, and acquisition suitability.", "suitability-approval", "/LandParcel/SuitabilityApprovalView", "GET", "Estate Manager", "Approve Suitability", "Reject Suitability"),
        new(2, 2, "Cadastral Survey", "Cadastral Survey", "Capture cadastral survey plan, coordinates, demarcation details, and survey documents.", "cadastral-survey", "/LandParcel/CadastralSurvey", "GET", "Survey Officer", "Submit for Survey Verification", "Return Survey"),
        new(3, 3, "Cadastral Survey Verification", "Cadastral Survey Verification", "Verify cadastral match, boundary consistency, encumbrances, and survey overlap checks.", "cadastral-verification", "/LandParcel/CadastralSurveyVerifcation", "GET", "Senior Surveyor", "Approve Survey Verification", "Reject Survey Verification"),
        new(4, 4, "Ownership Classification", "Ownership Classification", "Classify ownership as stool, family, private, state, allodial, or mixed interest.", "ownership-classification", "/LandParcel/OwnershipClassification", "GET", "Legal Officer", "Submit Ownership Classification", "Return Classification"),
        new(5, 5, "Ownership Verification", "Ownership Verification", "Verify title documents, identity, searches, authority to sell, and ownership history.", "ownership-verification", "/LandParcel/OwnershipVerification", "POST", "Legal Manager", "Approve Ownership Verification", "Reject Ownership Verification"),
        new(6, 6, "Agreement Negotiation", "Agreement Negotiation", "Record offers, counteroffers, negotiated value, conditions, and negotiation notes.", "agreement-negotiation", "/LandParcel/AgreementNegotiation", "POST", "Acquisition Committee", "Approve Negotiation", "Return Negotiation"),
        new(7, 7, "Agreement Approval", "Agreement Approval", "Approve negotiated agreement terms before land instrument execution.", "agreement-approval", "/LandParcel/AgreementApproval", "POST", "Executive Approver", "Approve Agreement", "Reject Agreement"),
        new(8, 8, "Land Instrument Execution", "Land Instrument Execution", "Capture execution details for the conveyance, assignment, lease, or acquisition instrument.", "execution", "/LandParcel/Execution", "GET", "Legal Officer", "Submit Executed Instrument", "Return Execution"),
        new(9, 9, "Statutory Consent", "Statutory Consent", "Prepare and submit statutory consent application to the appropriate authority.", "statutory-consent", "/LandParcel/StatutoryConsent", "GET", "Lands Commission Liaison", "Submit Statutory Consent", "Return Consent Application"),
        new(10, 10, "Statutory Consent Approval", "Statutory Consent Approval", "Review statutory consent approval reference, conditions, approval date, and documents.", "statutory-consent-approval", "/LandParcel/StatutoryConsentApproval", "GET", "Legal Manager", "Approve Statutory Consent", "Reject Statutory Consent"),
        new(11, 11, "Stamp Duty Assessment", "Stamp Duty Assessment", "Record valuation, assessed value, stamp duty amount, and assessment reference.", "stamp-duty-assessment", "/LandParcel/StampDutyAssessment", "GET", "Finance Officer", "Submit Stamp Duty Assessment", "Return Assessment"),
        new(12, 12, "Stamp Duty Approval", "Stamp Duty Approval", "Approve the stamp duty assessment before payment is processed.", "stamp-duty-approval", "/LandParcel/StampDutyApproval", "GET", "Finance Manager", "Approve Stamp Duty Assessment", "Reject Stamp Duty Assessment"),
        new(13, 13, "Stamp Duty Payment", "Stamp Duty Payment", "Capture payment receipt, payment date, amount paid, and payment evidence.", "stamp-duty-payment", "/LandParcel/StampDutyPaymentPage", "GET", "Accounts Payable", "Submit Stamp Duty Payment", "Return Payment"),
        new(14, 14, "Registration", "Registration", "Capture registry, registration number, volume, folio, instrument date, and archive details.", "registration", "/LandParcel/RegistrationStage", "GET", "Land Registry Officer", "Submit Registration", "Return Registration"),
        new(15, 15, "Asset Creation", "Asset Creation", "Create the estate asset, assign asset code, GL account, capitalization value, and custodian.", "asset-creation", "/LandParcel/AssetCreation", "GET", "Fixed Asset Officer", "Create Estate Asset", "Return Asset Creation"),
    };
}
