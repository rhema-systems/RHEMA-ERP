using FluentAssertions;
using ErpSystem.Core.Services.Estate;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class EstateWorkflowIntegrationRegressionTests
{
    [Fact]
    public void PublishedWorkflowDocuments_ReplaceCatalogRequirements_AndCatalogStagesRemainFallback()
    {
        var service = ReadSource("src", "ErpSystem.Api", "Services", "ProcedureCaseService.cs");
        var workspace = Slice(service,
            "private async Task<WorkspaceSeed> BuildWorkspaceSeedAsync",
            "private static IReadOnlyList<string> BuildConfiguredWorkflowChecklist");

        workspace.Should().Contain("documents = await BuildWorkflowDocumentSeedsAsync(workflowDefinitionId);");
        workspace.Should().Contain("\"Legal\" => _legalCatalog.GetProcedureWorkspace(entityType)?.Stages");
        workspace.Should().Contain("\"PropertyManagement\" => _propertyManagementCatalog.GetProcedureWorkspace(entityType)?.Stages");
        workspace.Should().Contain("\"Facilities\" => _facilitiesCatalog.GetProcedureWorkspace(entityType)?.Stages");
        workspace.Should().Contain("\"Planning\" => _planningCatalog.GetProcedureWorkspace(entityType)?.Stages");
        service.Should().Contain("if (request.HasIntakeAttachment)");
        service.Should().Contain("CanManageOwnIntakeAttachment(procedureCase, document)");
    }

    [Fact]
    public void PropertyListingRequests_PersistPortalMetadataAndExposeApprovalFields()
    {
        var workspace = new PropertyManagementProcedureCatalogService()
            .GetProcedureWorkspace("EstatePropertyManagementListingApplication");
        var fieldKeys = workspace!.IntakeFields.Select(field => field.Key).ToList();
        var frontend = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "ListingApplicationWorkspace.tsx");
        var migration = ReadSource(
            "src",
            "ErpSystem.Data",
            "LegacyMigrationsArchive",
            "20260822120000_BackfillPropertyListingApplicationFields.cs");

        fieldKeys.Should().Contain([
            "applicationReference",
            "listingReference",
            "requestType",
            "decisionStatus",
            "agreementTemplateReference",
            "moveInDate"
        ]);
        frontend.Should().Contain("keys.add('decisionStatus');");
        frontend.Should().Contain("keys.add('moveInDate');");
        migration.Should().Contain("EstateManagedAssets");
        migration.Should().Contain("WHERE NOT EXISTS");
    }

    [Fact]
    public void CompletedPropertyRequest_IsReadOnlyForEveryRole()
    {
        var procedureService = ReadSource(
            "src",
            "ErpSystem.Api",
            "Services",
            "ProcedureCaseService.cs");
        var canEdit = Slice(
            procedureService,
            "private bool CanEdit(ProcedureCase procedureCase)",
            "private bool CanCreateLegalProcedureCase");
        var workspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "ListingApplicationWorkspace.tsx");

        canEdit.Should().Contain("if (IsCompleted(procedureCase))");
        canEdit.Should().Contain("return false;");
        workspace.Should().Contain("caseIsCompleted ||");
        workspace.Should().Contain("? 'Case completed'");
    }

    [Fact]
    public void ListingApproval_OwnsRentalMoveInDateBeforeCustomerAcceptance()
    {
        var procedureService = ReadSource(
            "src",
            "ErpSystem.Api",
            "Services",
            "ProcedureCaseService.cs");
        var approvalGuard = Slice(
            procedureService,
            "private static void EnsureExternalListingApprovalIsReady",
            "private static string? NormalizeProcedureField");
        var externalController = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateExternalDocumentsController.cs");
        var customerDecision = Slice(
            externalController,
            "public async Task<IActionResult> SubmitCustomerDecision",
            "public async Task<IActionResult> DownloadGeneratedAgreement");
        var signedUpload = Slice(
            externalController,
            "public async Task<IActionResult> UploadSignedAgreement",
            "public async Task<IActionResult> CreateRequest");
        var frontend = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "ListingApplicationWorkspace.tsx");
        var portal = ReadSource(
            "frontend",
            "src",
            "app",
            "external-portal",
            "my-property-requests",
            "page.tsx");

        procedureService.Should().Contain("EnsureExternalListingApprovalIsReady(procedureCase);");
        procedureService.Should().Contain("Sales - Estate Enquiry");
        approvalGuard.Should().Contain("IsPropertyListingApplication(procedureCase)");
        approvalGuard.Should().Contain("generatedAgreementReference");
        approvalGuard.Should().Contain("legalAgreementReviewStatus");
        approvalGuard.Should().Contain("Legal must approve the generated agreement before completing final approval.");
        approvalGuard.Should().Contain("moveInDate");
        approvalGuard.Should().Contain("IsRentalListingApplication(procedureCase)");
        frontend.Should().Contain("missingLegalAgreementReview");
        frontend.Should().Contain("Legal must approve the generated agreement before the customer can sign.");
        customerDecision.Should().Contain("The approved agreement must be generated before you can accept this request.");
        customerDecision.Should().Contain("Property Management must set the approved move-in date");
        signedUpload.Should().NotContain("[FromForm] string? moveInDate");
        signedUpload.Should().Contain("approvedMoveInDate = FieldValue(fields, \"moveInDate\")");
        portal.Should().NotContain("moveInDates");
    }

    [Fact]
    public void ExternalCustomerDecision_ExplicitlyAddsItsActivityAsANewRow()
    {
        var externalController = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateExternalDocumentsController.cs");
        var addActivity = Slice(
            externalController,
            "private void AddExternalCaseActivity",
            "private IQueryable<BusinessPartner> PortalCustomers");

        addActivity.Should().Contain("_db.ProcedureCaseActivities.Add(new ProcedureCaseActivity");
        addActivity.Should().NotContain("procedureCase.Activities.Add(new ProcedureCaseActivity");
    }

    [Fact]
    public void SignedAgreementUpload_ExplicitlyInsertsNewRowsAndCleansUpFailedStorage()
    {
        var externalController = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateExternalDocumentsController.cs");
        var signedUpload = Slice(
            externalController,
            "public async Task<IActionResult> UploadSignedAgreement",
            "public async Task<IActionResult> CreateRequest");
        var upsertField = Slice(
            externalController,
            "private void UpsertField",
            "private void AddExternalCaseActivity");

        signedUpload.Should().Contain("_db.ProcedureCaseDocuments.Add(document);");
        signedUpload.Should().Contain("await _fileStorageService.DeleteFileAsync(upload.FilePath);");
        signedUpload.Should().NotContain("procedureCase.Documents.Add(document);");
        upsertField.Should().Contain("_db.ProcedureCaseFields.Add(created);");
        upsertField.Should().NotContain("procedureCase.Fields.Add(created);");
    }

    [Fact]
    public void CustomerAgreement_RequiresInternalApprovalAndSignatureBeforeMoveInBecomesEffective()
    {
        var externalController = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateExternalDocumentsController.cs");
        var signedUpload = Slice(
            externalController,
            "public async Task<IActionResult> UploadSignedAgreement",
            "public async Task<IActionResult> CreateRequest");
        var documentController = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "DocumentManagement",
            "DocumentManagementController.cs");
        var agreementWorkflow = Slice(
            documentController,
            "private async Task SynchronizePropertyAgreementWorkflowAsync",
            "private void UpsertPropertyAgreementField");
        var internalWorkspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "ListingApplicationWorkspace.tsx");
        var customerPortal = ReadSource(
            "frontend",
            "src",
            "app",
            "external-portal",
            "my-property-requests",
            "page.tsx");

        signedUpload.Should().Contain("Blocked - internal approval and signature pending");
        signedUpload.Should().NotContain("Ready for billing from move-in date");
        documentController.Should().Contain("EnsureCustomerSignedAgreementVersionAsync");
        documentController.Should().Contain("CanUseSourceModuleForDms(record.SourceModule)");
        documentController.Should().Contain("IsPropertyListingAgreementAsync(record");
        documentController.Should().Contain("HasAnyRole(\"Property Manager\", \"Estate Manager\", \"Head of Estate\")");
        documentController.Should().Contain("HasAnyRole(\"Executive Approver\", \"Authorised Signatory\", \"Managing Director\")");
        documentController.Should().Contain("Final signed agreement ready");
        documentController.Should().Contain("/external-portal/my-property-requests");
        agreementWorkflow.Should().Contain("Internally approved - digital signature pending");
        agreementWorkflow.Should().Contain("Fully executed");
        agreementWorkflow.Should().Contain("billingStartDate");
        agreementWorkflow.Should().Contain("Effective - final agreement signed");
        agreementWorkflow.Should().Contain("SynchronizeExecutedRentalLeaseAsync");
        agreementWorkflow.Should().Contain("EstateManagedAssetStatus.Leased");
        agreementWorkflow.Should().Contain("asset.CustomerBusinessPartnerId = customer.Id");
        agreementWorkflow.Should().Contain("asset.PropertyFileReference = record.DocumentReference");
        agreementWorkflow.Should().Contain("asset.IsPublishedToExternalPortal = false");
        agreementWorkflow.Should().Contain("Agreement returned for correction");
        agreementWorkflow.Should().Contain("\"signedAgreementReference\"");
        var caseVisibility = Slice(
            ReadSource(
                "src",
                "ErpSystem.Api",
                "Services",
                "ProcedureCaseService.cs"),
            "private bool CanView(ProcedureCase procedureCase)",
            "private bool UserOwnsCase");
        caseVisibility.Should().Contain("CanOverseePropertyListingApplication(procedureCase)");
        caseVisibility.Should().Contain("\"Property Manager\"");
        caseVisibility.Should().Contain("\"Estate Manager\"");
        internalWorkspace.Should().Contain("Customer-submitted documents");
        internalWorkspace.Should().Contain("hasActiveCustomerSignedAgreement");
        internalWorkspace.Should().Contain("Submit for approval");
        internalWorkspace.Should().Contain("Approve agreement");
        internalWorkspace.Should().Contain("Digitally sign");
        internalWorkspace.Should().Contain("agreementAlreadyGenerated");
        internalWorkspace.Should().Contain("selectedCase.currentStageIndex >= 2 || agreementAlreadyGenerated");
        internalWorkspace.Should().Contain("'Agreement generated'");
        customerPortal.Should().Contain("Final signed agreement");
        customerPortal.Should().Contain("Download final PDF");
        externalController.Should().Contain("Customer signed agreement received");
        externalController.Should().Contain("Customer accepted property request");
        documentController.Should().Contain("estate.property.agreement-correction-required");
        var notificationDispatcher = ReadSource(
            "src",
            "ErpSystem.Api",
            "Services",
            "Notifications",
            "RoleNotificationDispatcher.cs");
        notificationDispatcher.Should().Contain("CreateInAppNotificationAsync");
    }

    [Fact]
    public void ProcedureDocumentMetadataEndpoint_CannotBindAnArbitraryPrivatePath()
    {
        var source = ReadSource("src", "ErpSystem.Api", "Services", "ProcedureCaseService.cs");
        var attachMethod = Slice(
            source,
            "public async Task<ProcedureCaseDetailDto?> AttachDocumentAsync",
            "private async Task<ProcedureCaseDetailDto> UpdateDocumentAttachmentAsync");
        var uploadMethod = Slice(
            source,
            "public async Task<ProcedureCaseDetailDto?> UploadDocumentAsync",
            "public async Task<ProcedureCaseDocumentContentDto?> GetDocumentContentAsync");

        attachMethod.Should().Contain("!string.IsNullOrWhiteSpace(request.FileUrl)");
        attachMethod.Should().Contain("Upload procedure case documents through the secure procedure document upload endpoint.");
        attachMethod.Should().NotContain("IsPrivateProcedureDocumentPath");
        uploadMethod.Should().Contain("UpdateDocumentAttachmentAsync(");
        source.Should().Contain("&& string.IsNullOrWhiteSpace(item.FileUrl)");
        source.Should().NotContain("&& !item.UploadedAt.HasValue");
    }

    [Fact]
    public void ExternalCustomerIntakeUpload_UsesPortalRouteAndClosesAfterFirstInternalStageRoutesForward()
    {
        var frontendService = ReadSource(
            "frontend",
            "src",
            "services",
            "external-estate-services.service.ts");
        var frontendListingsService = ReadSource(
            "frontend",
            "src",
            "services",
            "external-estate-listings.service.ts");
        var portalPage = ReadSource(
            "frontend",
            "src",
            "app",
            "external-portal",
            "my-property-requests",
            "page.tsx");
        var externalController = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateExternalDocumentsController.cs");
        var procedureService = ReadSource("src", "ErpSystem.Api", "Services", "ProcedureCaseService.cs");

        frontendService.Should().Contain("/estate/external/requests/${requestId}/customer-intake-documents/${documentId}/upload");
        frontendService.Should().NotContain("/procedure-cases/${requestId}/customer-intake-documents/${documentId}/upload");
        frontendListingsService.Should().Contain("/estate/external/requests/${requestId}/customer-intake-documents/${documentId}/upload");
        frontendListingsService.Should().NotContain("/procedure-cases/${requestId}/customer-intake-documents/${documentId}/upload");
        portalPage.Should().Contain("function canUploadIntakeDocuments");
        portalPage.Should().Contain("request.customerIntakeUploadClosed || request.currentStageIndex > 0");
        externalController.Should().Contain("[HttpPost(\"/api/estate/external/requests/{requestId:guid}/customer-intake-documents/{documentId:guid}/upload\")]");
        externalController.Should().Contain("ToExternalRequestDto(procedureCase)");
        externalController.Should().Contain("CustomerIntakeUploadClosed = HasFirstInternalStageBeenRoutedForward(item)");
        externalController.Should().Contain("CustomerIntakeUploadClosed = HasFirstInternalStageBeenRoutedForward(procedureCase)");
        procedureService.Should().Contain("HasFirstInternalStageBeenRoutedForward(procedureCase)");
        procedureService.Should().Contain("Customer intake documents can only be uploaded until the first internal stage is routed forward.");
    }

    [Fact]
    public void StampDutyPayable_StoresTheProcurementSupplierIdentifier()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "LandAcquisitionsController.cs");
        var method = Slice(
            source,
            "private async Task EnsureStampDutyPayableAsync",
            "private async Task EnsureOtherAcquisitionCostsPayableAsync");

        method.Should().Contain("payment.AccountsPayableSupplierId = invoice.SupplierId;");
        method.Should().Contain("[\"accountsPayableSupplierId\"] = invoice.SupplierId");
        method.Should().NotContain("payment.AccountsPayableSupplierId = payee.Id;");
        method.Should().NotContain("[\"accountsPayableSupplierId\"] = payee.Id");
    }

    [Fact]
    public void AcquisitionPayableSubmission_UsesFinanceApprovalWorkflow()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "LandAcquisitionsController.cs");
        var method = Slice(
            source,
            "private async Task EnsureAcquisitionPayableSubmittedForApprovalAsync",
            "private async Task<Guid> ResolveLandAcquisitionDebitAccountIdAsync");

        method.Should().Contain("if (invoice.Status == VendorInvoiceStatus.Draft)");
        method.Should().Contain("await _vendorInvoiceService.SubmitForApprovalAsync(invoice.Id, cancellationToken);");
        method.Should().Contain("if (invoice.Status == VendorInvoiceStatus.Rejected)");
        method.Should().Contain("was rejected");
    }

    [Fact]
    public void ManagedAssetReads_EnrichDetachedResultsWithoutPersistingRepairs()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var getMethod = Slice(
            source,
            "public async Task<IReadOnlyList<EstateManagedAssetDto>> GetManagedAssetsAsync",
            "public async Task<EstateManagedAssetDto> PublishLandAcquisitionAsync");
        var enrichmentMethod = Slice(
            source,
            "private async Task EnrichLandAcquisitionAssetsForReadAsync",
            "private static bool SetIfBlank");

        getMethod.Should().Contain(".AsNoTracking()");
        getMethod.Should().Contain("EnrichLandAcquisitionAssetsForReadAsync(assets)");
        enrichmentMethod.Should().NotContain("SaveChangesAsync");
        enrichmentMethod.Should().NotContain("UpdateAsync");
        enrichmentMethod.Should().NotContain("BoundaryVerified = true");
        source.Should().NotContain("RepairLandAcquisitionAssetsAsync");
        source.Should().NotContain("SaveRepairAsync");
    }

    [Fact]
    public void DemarcationMutations_RejectLandWithAnActiveExternalListing()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var createMethod = Slice(
            source,
            "private async Task<EstateLandDemarcationDto> CreateLandDemarcationCoreAsync",
            "public Task<EstateLandDemarcationDto> UpdateLandDemarcationAsync");
        var updateMethod = Slice(
            source,
            "private async Task<EstateLandDemarcationDto> UpdateLandDemarcationCoreAsync",
            "public async Task DeleteLandDemarcationAsync");
        var deleteMethod = Slice(
            source,
            "private async Task DeleteLandDemarcationCoreAsync",
            "public async Task<EstateManagedAssetDto> MarkReadyForProjectManagementAsync");
        var guard = Slice(
            source,
            "private static void EnsureDemarcationsCanBeChanged",
            "private async Task<HashSet<string>> GetAssignedProjectLandReferencesAsync");

        createMethod.Should().Contain("EnsureDemarcationsCanBeChanged(asset);");
        updateMethod.Should().Contain("EnsureDemarcationsCanBeChanged(asset);");
        deleteMethod.Should().Contain("EnsureDemarcationsCanBeChanged(asset);");
        guard.Should().Contain("asset.IsPublishedToExternalPortal");
        guard.Should().Contain(
            "Withdraw the active external land listing before changing its demarcations.");
    }

    [Fact]
    public void ReadinessApproval_SharesTheSerializableDemarcationMutationBoundary()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var publicMethod = Slice(
            source,
            "public async Task<EstateManagedAssetDto> MarkReadyForProjectManagementAsync",
            "private async Task<EstateManagedAssetDto> MarkReadyForProjectManagementCoreAsync");
        var coreMethod = Slice(
            source,
            "private async Task<EstateManagedAssetDto> MarkReadyForProjectManagementCoreAsync",
            "public Task<EstateManagedAssetDto> UpdateExternalListingAsync");

        publicMethod.Should().Contain("ExecuteSerializableMutationAsync(");
        publicMethod.Should().Contain("MarkReadyForProjectManagementCoreAsync(assetId)");
        coreMethod.Should().Contain("demarcations.Any(item => !item.BoundaryVerified)");
        coreMethod.Should().Contain("asset.IsReadyForProjectManagement = true;");
    }

    [Fact]
    public void ProjectLandSynchronization_CannotRestoreReadinessAfterADemarcationReset()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Projects",
            "ProjectService.Construction.cs");
        var availabilityMethod = Slice(
            source,
            "private async Task<(int ActiveCount, bool HasUnusedPortion, bool AllPortionsVerified)> GetProjectLandAvailabilityAsync",
            "private async Task<ReadyProjectLandSelection?> FindProjectLandSelectionAsync");
        var synchronizationMethod = Slice(
            source,
            "private async Task SynchronizeProjectLandAssetsAsync",
            "private sealed record ReadyProjectLandSelection");

        availabilityMethod.Should().NotContain("&& item.BoundaryVerified");
        availabilityMethod.Should().Contain("demarcations.All(item => item.BoundaryVerified)");
        synchronizationMethod.Should().Contain(
            "asset.IsReadyForProjectManagement = asset.IsReadyForProjectManagement");
        synchronizationMethod.Should().Contain("&& availability.AllPortionsVerified");
        synchronizationMethod.Should().NotContain(
            "asset.IsReadyForProjectManagement = !asset.IsPublishedToExternalPortal;");
    }

    [Fact]
    public void ProjectLandSelectors_UseOneBatchEndpointForCreateAndEdit()
    {
        var controller = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateManagedAssetsController.cs");
        var managedAssetService = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var batchMethod = Slice(
            managedAssetService,
            "public async Task<IReadOnlyList<ProjectReadyLandDemarcationDto>> GetProjectReadyLandDemarcationsAsync",
            "public Task<EstateLandDemarcationDto> CreateLandDemarcationAsync");
        var clientService = ReadSource(
            "frontend",
            "src",
            "services",
            "estate-land-management.service.ts");
        var selector = ReadSource(
            "frontend",
            "src",
            "components",
            "projects",
            "ReadyLandPortionSelect.tsx");
        var createPage = ReadSource(
            "frontend",
            "src",
            "app",
            "development",
            "projects",
            "new",
            "page.tsx");
        var editTab = ReadSource(
            "frontend",
            "src",
            "app",
            "development",
            "projects",
            "[id]",
            "components",
            "ProjectOverviewTab.tsx");

        controller.Should().Contain("[HttpGet(\"project-ready-demarcations\")]");
        batchMethod.Should().Contain("item.ProjectId != projectId.Value");
        batchMethod.Should().Contain("IsCurrentProjectSelection = isCurrentSelection");
        batchMethod.Should().Contain(
            "!item.EstateManagedAsset.IsPublishedToExternalPortal");
        clientService.Should().Contain(
            "'/estate/managed-assets/project-ready-demarcations'");
        selector.Should().Contain("getProjectReadyLandDemarcations(");
        createPage.Should().Contain("<ReadyLandPortionSelect");
        createPage.Should().NotContain("getLandDemarcations(asset.id)");
        editTab.Should().Contain("projectId={project.id}");
        editTab.Should().Contain("<ReadyLandPortionSelect");
        editTab.Should().NotContain("<Label>Land Reference</Label><Input");
    }

    [Fact]
    public void AcquisitionRepublishing_CannotInvalidateOrResetAssignedLand()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var publication = Slice(
            source,
            "public async Task<EstateManagedAssetDto> PublishLandAcquisitionAsync",
            "public async Task<EstateManagedAssetDto> PublishProjectUnitAsync");
        var assignmentGuard = Slice(
            source,
            "private async Task EnsureAcquisitionCanBeRepublishedAsync",
            "public async Task<EstateManagedAssetDto> PublishProjectUnitAsync");

        publication.Should().Contain("ExecuteSerializableMutationAsync(");
        publication.Should().Contain("await EnsureAcquisitionCanBeRepublishedAsync(existing);");
        publication.Should().Contain("if (isNew || string.IsNullOrWhiteSpace(asset.AssetCode))");
        publication.IndexOf("await EnsureAcquisitionCanBeRepublishedAsync(existing);", StringComparison.Ordinal)
            .Should().BeLessThan(
                publication.IndexOf(
                    "asset.Status = EstateManagedAssetStatus.LandBank;",
                    StringComparison.Ordinal));
        assignmentGuard.Should().Contain("asset.ProjectId.HasValue");
        assignmentGuard.Should().Contain("IsDemarcationAssignedToProject(");
        assignmentGuard.Should().Contain(
            "Land with a demarcation assigned to a project cannot be published to the land bank again.");
        assignmentGuard.Should().Contain("asset.IsPublishedToExternalPortal");
        assignmentGuard.Should().Contain(
            "Withdraw the active external land listing before publishing the acquisition to the land bank again.");
    }

    [Fact]
    public void AcquisitionPublication_PreservesTheSurveyVerificationOutcome()
    {
        var service = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var publication = Slice(
            service,
            "private async Task<EstateManagedAssetDto> PublishLandAcquisitionCoreAsync",
            "private async Task EnsureAcquisitionCanBeRepublishedAsync");
        var controller = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "LandAcquisitionsController.cs");
        var handoff = Slice(
            controller,
            "var managedAsset = await _managedAssetService.PublishLandAcquisitionAsync",
            "// Keep acquisition evidence available");

        publication.Should().Contain("handoff.BoundaryVerified");
        publication.Should().Contain("boundaryCoordinates != null");
        publication.Should().NotContain(
            "asset.BoundaryVerified || boundaryCoordinates != null");
        handoff.Should().Contain("CadastralMatch: true");
        handoff.Should().Contain("OverlapCleared: true");
        handoff.Should().Contain("BoundaryConfirmed: true");
        handoff.Should().NotContain("BoundaryVerified = true");
    }

    [Fact]
    public void DemarcationMutations_ReturnCanonicalLandReferences()
    {
        var service = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var createMethod = Slice(
            service,
            "private async Task<EstateLandDemarcationDto> CreateLandDemarcationCoreAsync",
            "public Task<EstateLandDemarcationDto> UpdateLandDemarcationAsync");
        var updateMethod = Slice(
            service,
            "private async Task<EstateLandDemarcationDto> UpdateLandDemarcationCoreAsync",
            "public async Task DeleteLandDemarcationAsync");
        var mapper = Slice(
            service,
            "private static EstateLandDemarcationDto MapDemarcationToDto",
            "private static EstateManagedAssetDto MapToDto");

        createMethod.Should().Contain(
            "MapDemarcationToDto(demarcation, asset.AssetCode)");
        updateMethod.Should().Contain(
            "MapDemarcationToDto(demarcation, asset.AssetCode)");
        mapper.Should().Contain(
            "LandReference = EstateLandDemarcationReference.Build(");
    }

    [Fact]
    public void DisabledSaleListingAction_DoesNotRenderANavigableLink()
    {
        var page = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "land-management",
            "page.tsx");
        var listingAction = Slice(
            page,
            "{saleListingDisabledReason ? (",
            "{selected.asset.isReadyForProjectManagement ? (");

        var enabledBranch = listingAction.IndexOf(") : (", StringComparison.Ordinal);
        enabledBranch.Should().BePositive();
        var disabledAction = listingAction[..enabledBranch];
        disabledAction.Should().Contain("<Button").And.Contain("disabled")
            .And.NotContain("<Link").And.NotContain("onClick=").And.NotContain("asChild");
        // Eligible listings now open the demarcation editor instead of navigating away.
        listingAction[enabledBranch..].Should()
            .Contain("onClick={() => setDemarcationAsset(selected.asset)}");
    }

    [Fact]
    public void QueuedDemarcations_CannotDiscardAnIncompleteEditorDraft()
    {
        var dialog = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "land-management",
            "DemarcateLandDialog.tsx");
        var saveMethod = Slice(
            dialog,
            "const save = async () =>",
            "const remove = async");
        var saveButton = Slice(
            dialog,
            "<Button\n            onClick={() => void save()}",
            "</DialogFooter>");

        saveMethod.Should().Contain("if (hasEditorValues && !currentPayload)");
        saveMethod.Should().Contain(
            "Complete or clear the current demarcation draft before saving.");
        saveButton.Should().Contain("hasEditorValues && isIncomplete");
        saveButton.Should().Contain(
            "pendingDemarcations.length === 0 && !hasEditorValues");
    }

    [Fact]
    public void SaleListingAction_DisablesWhenAnyDemarcationIsAssigned()
    {
        var page = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "land-management",
            "page.tsx");
        var disabledReason = Slice(
            page,
            "const saleListingDisabledReason",
            "const saleListingLabel");

        disabledReason.Should().Contain(
            "selectedDemarcations.some((item) => item.isAssignedToProject)");
        disabledReason.Should().Contain(
            "Land with an assigned demarcation cannot be listed for sale.");
    }

    [Fact]
    public void LegacyWholeParcelAssignments_CannotBeInvalidatedBySubdivision()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var createMethod = Slice(
            source,
            "private async Task<EstateLandDemarcationDto> CreateLandDemarcationCoreAsync",
            "public Task<EstateLandDemarcationDto> UpdateLandDemarcationAsync");
        var assignmentHelpers = Slice(
            source,
            "private static bool IsDemarcationAssignedToProject",
            "private static EstateLandDemarcationDto MapDemarcationToDto");

        createMethod.Should().Contain("existingBoundaries.Count > 0");
        createMethod.Should().Contain(
            "HasLegacyWholeParcelAssignment(asset, assignedLandReferences)");
        createMethod.Should().Contain(
            "Land assigned to a project by its whole-parcel reference cannot be subdivided.");
        assignmentHelpers.Should().Contain(
            "HasLegacyWholeParcelAssignment(asset, assignedLandReferences)");
    }

    [Fact]
    public void ReadyLandSelectorQuery_FiltersAndProjectsBeforeMaterialization()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var batchMethod = Slice(
            source,
            "public async Task<IReadOnlyList<ProjectReadyLandDemarcationDto>> GetProjectReadyLandDemarcationsAsync",
            "public Task<EstateLandDemarcationDto> CreateLandDemarcationAsync");
        var candidateQuery = Slice(
            batchMethod,
            "var candidates =",
            "var candidatesByAsset");

        batchMethod.Should().Contain(
            "item.EstateManagedAsset.IsReadyForProjectManagement");
        batchMethod.Should().Contain("normalizedCurrentReference != null");
        candidateQuery.Should().Contain(".Select(item => new ReadyLandCandidate");
        candidateQuery.IndexOf(".Select(item => new ReadyLandCandidate", StringComparison.Ordinal)
            .Should().BeLessThan(
                candidateQuery.IndexOf(".ToListAsync()", StringComparison.Ordinal));
        candidateQuery.Should().NotContain(".Select(item => item.BoundaryCoordinates)");
    }

    [Fact]
    public void OutboundDemarcationCosting_ReconcilesLeafTotalsAndParentPools()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var costingGuard = Slice(
            source,
            "private static void EnsureDemarcationCostingReconcilesForOutbound(",
            "private async Task<HashSet<string>> GetAssignedProjectLandReferencesAsync");

        costingGuard.Should().Contain("GetLeafDemarcations(demarcations)");
        costingGuard.Should().Contain("GetLeafDescendants(demarcations, parent.Id)");
        costingGuard.Should().Contain("leafDemarcations.Sum(item => item.AllocatedCost!.Value)");
        costingGuard.Should().Contain("Demarcation costs must equal the parent land value.");
        costingGuard.Should().Contain("must equal its parent pool");
    }

    [Fact]
    public void LandCreationFixedAsset_IsCreatedOnlyAfterSuccessfulWorkflowCompletion()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "LandAcquisitionsController.cs");
        var saveWorkspace = Slice(
            source,
            "public async Task<ActionResult<LandAcquisitionWorkspaceResponse>> SaveWorkspace(",
            "[HttpGet(\"{id:guid}/workspace/{procedureId:int}\")]");
        var workflowAction = Slice(
            source,
            "[HttpPost(\"workflow-action\")]",
            "[HttpPost(\"{id:guid}/workflow-task-completion\")]");
        var completionHelper = Slice(
            source,
            "private async Task EnsureFinanceFixedAssetForCompletedLandCreationAsync(",
            "private async Task EnsureFinanceFixedAssetForLandAssetAsync(");

        saveWorkspace.Should().NotContain("EnsureFinanceFixedAssetForLandAssetAsync");
        workflowAction.Should().Contain("EnsureFinanceFixedAssetForCompletedLandCreationAsync");
        workflowAction.IndexOf("var missingInputs", StringComparison.Ordinal)
            .Should().BeLessThan(
                workflowAction.IndexOf(
                    "EnsureFinanceFixedAssetForCompletedLandCreationAsync",
                    StringComparison.Ordinal));
        completionHelper.Should().Contain("workflowOutcome != WorkflowOutcome.Approved");
        completionHelper.Should().Contain("SnapshotToObjectDictionary(assetCreationSnapshot)");
    }

    [Fact]
    public void DemarcationEditors_ConfirmBeforeReplacingUnsavedDrafts()
    {
        var dialog = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "land-management",
            "DemarcateLandDialog.tsx");
        var selector = ReadSource(
            "frontend",
            "src",
            "components",
            "projects",
            "ReadyLandPortionSelect.tsx");

        dialog.Should().Contain("confirmEditorReplacement");
        dialog.Should().Contain(
            "confirmEditorReplacement('edit this saved demarcation')");
        dialog.Should().Contain(
            "confirmEditorReplacement('edit this pending demarcation')");
        dialog.Should().Contain("onClick={beginNewDemarcation}");
        selector.Should().Contain("portion.isCurrentProjectSelection");
        selector.Should().Contain("onValueChange(normalizedValue)");
    }

    [Fact]
    public void CanonicalLandReferenceMigration_ExcludesTheCurrentProjectFromOccupancy()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Projects",
            "ProjectService.Construction.cs");
        var resolution = Slice(
            source,
            "private async Task<string?> ResolveDevelopmentProfileLandReferenceAsync",
            "private async Task<(int ActiveCount, bool HasUnusedPortion, bool AllPortionsVerified)> GetProjectLandAvailabilityAsync");

        resolution.Should().Contain("Guid projectId");
        resolution.Should().Contain("excludedProjectId");
        resolution.Should().Contain(
            "!excludedProjectId.HasValue || item.ProjectId != excludedProjectId.Value");
        resolution.Should().Contain("ResolveReadyProjectLandReferenceAsync(");
        resolution.Should().Contain("projectId);");
    }

    [Fact]
    public void ProjectReadyLandEndpoint_AuthorizesProjectSpecificSelections()
    {
        var controller = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateManagedAssetsController.cs");
        var endpoint = Slice(
            controller,
            "public async Task<IActionResult> GetProjectReadyLandDemarcations",
            "[HttpPost(\"{id:guid}/demarcations\")]");

        endpoint.Should().Contain(
            "await _projectService.GetDevelopmentProfileAsync(projectId.Value);");
        endpoint.Should().Contain("catch (UnauthorizedAccessException)");
        endpoint.IndexOf(
                "_projectService.GetDevelopmentProfileAsync",
                StringComparison.Ordinal)
            .Should().BeLessThan(
                endpoint.IndexOf(
                    "_managedAssetService.GetProjectReadyLandDemarcationsAsync",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void DemarcationDialogAndReadinessAction_PreserveFrontendGuards()
    {
        var dialog = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "land-management",
            "DemarcateLandDialog.tsx");
        var page = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "land-management",
            "page.tsx");

        dialog.Should().Contain("const handleOpenChange = (next: boolean)");
        dialog.Should().Contain(
            "pendingDemarcations.length > 0 || hasDraftValues");
        dialog.Should().Contain(
            "Discard all unsaved demarcation drafts and close this dialog?");
        dialog.Should().Contain(
            "<Dialog open={open} onOpenChange={handleOpenChange}>");
        page.Should().Contain("selected.asset.isPublishedToExternalPortal ||");
        page.Should().Contain(
            "Withdraw the active external land listing before marking this land ready for a project.");
    }

    [Fact]
    public void HandoverMoveOut_ExplicitlyReleasesOccupantAndPreservesHistory()
    {
        var service = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var workspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "MoveInHandoverWorkspace.tsx");
        var updateOccupancy = Slice(
            service,
            "private async Task<EstateManagedAssetDto> UpdateOccupancyCoreAsync",
            "public Task<EstateManagedAssetDto> UpdateExternalListingAsync");

        workspace.Should().Contain("releaseOccupant: action === 'move-out'");
        updateOccupancy.Should().Contain("request.ReleaseOccupant == true");
        updateOccupancy.Should().Contain("Record the actual move-out date before releasing the occupant.");
        updateOccupancy.Should().Contain("Occupancy released on");
        updateOccupancy.Should().Contain("asset.CustomerBusinessPartnerId = null;");
        updateOccupancy.Should().Contain("asset.PropertyFileReference = null;");
        updateOccupancy.Should().Contain("groundRentAccount.Status = \"Closed\";");
        updateOccupancy.Should().Contain("Closed after occupancy release on");
    }

    [Fact]
    public void RentBillingActivation_CreatesAnIdempotentFinanceArDraftFromLeaseTerms()
    {
        var controller = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "PropertyManagementArBillingController.cs");
        var workspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "BillingServiceChargeWorkspace.tsx");
        var activation = Slice(
            controller,
            "public async Task<ActionResult<EstateRentBillingActivationResult>> ActivateRentBilling",
            "[HttpPost(\"invoices\")]");

        activation.Should().Contain("asset.RentBillingActivatedAt.HasValue");
        activation.Should().Contain("Full-term leases use the one-time Estate balance invoice");
        activation.Should().Contain("asset.ExternalMonthlyRent ?? asset.ExternalListingPrice");
        activation.Should().Contain("asset.CustomerBusinessPartnerId.Value");
        activation.Should().Contain("BuildRentInvoiceReference(asset.AssetCode, billingStart)");
        activation.Should().Contain("AccountCode == \"4110\"");
        workspace.Should().Contain("Activate billing");
        workspace.Should().Contain("asset.lastRentInvoiceId");
        workspace.Should().Contain("<ConfirmationDialog");
        workspace.Should().Contain("Monthly rent");
        workspace.Should().NotContain("window.confirm");
    }

    [Fact]
    public void PropertyAgreementRelease_RequiresLegalApprovalAcrossPortalAndDms()
    {
        var portalController = ReadSource(
            "src", "ErpSystem.Api", "Controllers", "Estate", "EstateExternalDocumentsController.cs");
        var dmsController = ReadSource(
            "src", "ErpSystem.Api", "Controllers", "DocumentManagement", "DocumentManagementController.cs");
        var legalCatalog = ReadSource(
            "src", "ErpSystem.Core", "Services", "Legal", "LegalProcedureCatalogService.cs");

        portalController.Should().Contain("IsLegalAgreementReleaseApproved(fields)");
        portalController.Should().Contain("Legal must approve the draft agreement before it can be accepted.");
        dmsController.Should().Contain("IsPropertyAgreementLegalReviewApprovedAsync");
        dmsController.Should().Contain("before internal approval and digital signature can continue");
        legalCatalog.Should().Contain("LegalPropertyAgreementReview");
        legalCatalog.Should().Contain("Head of Legal Signature");
    }

    [Fact]
    public void ExternalListings_OnlyTreatActiveCustomerRequestsAsDuplicates()
    {
        var controller = ReadSource(
            "src", "ErpSystem.Api", "Controllers", "Estate", "EstateExternalDocumentsController.cs");
        var listings = Slice(
            controller,
            "public async Task<IActionResult> GetListings",
            "[HttpGet(\"/api/estate/external/listings/{listingId:guid}/images/{documentId:guid}\")]");
        var submission = Slice(
            controller,
            "public async Task<IActionResult> CreateListingRequest",
            "private async Task NotifyListingRequestAsync");

        foreach (var flow in new[] { listings, submission })
        {
            flow.Should().Contain("procedureCase.Status != \"Completed\"");
            flow.Should().Contain("procedureCase.Status != \"Archived\"");
            flow.Should().Contain("field.Value == \"Sale completed\"");
            flow.Should().Contain("field.Value == \"Agreement fully executed\"");
        }
    }

    [Fact]
    public void PropertySaleCompletion_UsesSalesHandoffBalanceBeforeRequiringEstatePayment()
    {
        var controller = ReadSource(
            "src", "ErpSystem.Api", "Controllers", "Estate", "PropertyManagementArBillingController.cs");
        var completion = Slice(
            controller,
            "public async Task<ActionResult<EstateSaleCompletionResult>> CompleteSaleOwnership",
            "[HttpPost(\"invoices\")]");

        completion.Should().Contain("var payableAmount = salePayable.EstateBalance;");
        completion.Should().Contain("if (payableAmount > 0m)");
        completion.Should().Contain("Create and complete the Finance AR sale invoice for the Estate balance first.");
        completion.Should().Contain("invoice.BalanceAmount <= 0m");
        completion.Should().Contain("invoice.Status, \"Paid\"");
        completion.Should().Contain("legalConveyanceStatus");
        completion.Should().Contain("\"Completed by Legal\"");
        completion.Should().Contain("item.EntityType == \"LegalTransfer\"");
        completion.Should().Contain("item.Status == \"Completed\"");
        completion.Should().Contain("asset.Status = EstateManagedAssetStatus.Sold;");
        completion.Should().Contain("asset.OwnershipHistoryJson = JsonSerializer.Serialize(ownerHistory);");
        completion.Should().Contain("This property is already sold to another customer.");
        completion.Should().Contain("asset.IsPublishedToExternalPortal = false;");
        completion.Should().Contain("estate.property.sale-completed");

        var billing = Slice(
            controller,
            "public async Task<ActionResult<EstateSaleInvoiceResult>> CreateSaleInvoice",
            "private static string BuildSaleInvoiceReference");
        billing.Should().Contain("Sales has already recorded the full agreed amount. No Estate balance remains to invoice.");
        billing.Should().Contain("UnitPrice = salePayable.EstateBalance");

        var frontend = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "ListingApplicationWorkspace.tsx");
        frontend.Should().Contain("saleFullyPaidInSales");
        frontend.Should().Contain("No Estate invoice required");
    }

    [Fact]
    public void SerializableLandClaimRetries_ClearTrackedStateAfterRollback()
    {
        var projectService = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Projects",
            "ProjectServices.cs");
        var constructionService = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Projects",
            "ProjectService.Construction.cs");
        var estateService = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var createWrapper = Slice(
            projectService,
            "public async Task<ProjectDetailDto> CreateProjectAsync",
            "private async Task<ProjectDetailDto> CreateProjectCoreAsync");
        var updateWrapper = Slice(
            projectService,
            "public async Task<ProjectDetailDto> UpdateProjectAsync",
            "private async Task<ProjectDetailDto> UpdateProjectCoreAsync");
        var profileWrapper = Slice(
            constructionService,
            "public async Task<ProjectDevelopmentProfileDto> UpsertDevelopmentProfileAsync",
            "public async Task<IEnumerable<ProjectPhaseDto>> GetProjectPhasesAsync");
        var estateWrapper = Slice(
            estateService,
            "private async Task<T> ExecuteSerializableMutationAsync",
            "private static bool IsDemarcationAssignedToProject");

        foreach (var wrapper in new[]
                 {
                     createWrapper,
                     updateWrapper,
                     profileWrapper,
                     estateWrapper
                 })
        {
            wrapper.Should().Contain("_unitOfWork.HasActiveTransaction");
            wrapper.Should().Contain("_unitOfWork.ClearTrackedChanges();");
            wrapper.IndexOf("_unitOfWork.RollbackAsync()", StringComparison.Ordinal)
                .Should().BeLessThan(
                    wrapper.IndexOf("_unitOfWork.ClearTrackedChanges();", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ListingAgreementAndConveyance_RequireFinanceConfirmedPayments()
    {
        var service = ReadSource("src", "ErpSystem.Api", "Services", "ProcedureCaseService.cs");
        var handoff = Slice(service,
            "public async Task<ProcedureCaseDetailDto> CreateLinkedLegalMatterAsync",
            "var sourceReference = FirstNonBlank(sourceCase.ReferenceNumber");
        handoff.Should().Contain("matter.Purpose == \"AgreementReview\"");
        handoff.Should().Contain("await EnsurePremiumFinancePaymentAsync(sourceCase)");
        handoff.Should().Contain("matter.Purpose == \"ConveyanceRegistration\"");
        handoff.Should().Contain("await EnsureFullAmountFinancePaymentAsync(sourceCase)");

        var premium = Slice(service,
            "private async Task EnsurePremiumFinancePaymentAsync",
            "private async Task EnsureFullAmountFinancePaymentAsync");
        premium.Should().Contain("premiumChargeInvoiceId");
        premium.Should().Contain("_invoiceService.GetByIdAsync(invoiceId)");
        premium.Should().Contain("invoice.BalanceAmount > 0m");
        premium.Should().Contain("invoice.Status, \"Paid\"");

        var balance = Slice(service,
            "private async Task EnsureFullAmountFinancePaymentAsync",
            "private static void EnsurePropertyListingPremiumChargeGateReady");
        balance.Should().Contain("agreementExecutionStatus");
        balance.Should().Contain("saleInvoiceId");
        balance.Should().Contain("_invoiceService.GetByIdAsync(invoiceId)");
        balance.Should().Contain("invoice.BalanceAmount > 0m");
        balance.Should().Contain("invoice.Status, \"Paid\"");
        var classification = Slice(service,
            "private static bool IsRentalListingApplication",
            "private static bool IsLeaseListingApplication");
        classification.IndexOf("requestType.Contains(\"lease\"", StringComparison.Ordinal)
            .Should().BeLessThan(classification.IndexOf("var listingType", StringComparison.Ordinal));

        var controller = ReadSource("src", "ErpSystem.Api", "Controllers", "Estate", "PropertyManagementArBillingController.cs");
        var saleInvoice = Slice(controller,
            "public async Task<ActionResult<EstateSaleInvoiceResult>> CreateSaleInvoice",
            "public async Task<ActionResult<EstateSaleCompletionResult>> CompleteSaleOwnership");
        saleInvoice.Should().Contain("allowLease: true");
        var ownership = Slice(controller,
            "public async Task<ActionResult<EstateSaleCompletionResult>> CompleteSaleOwnership",
            "[HttpPost(\"premium/{procedureCaseId:guid}/invoice\")]");
        ownership.Should().NotContain("allowLease: true");
        var customerNotice = Slice(controller,
            "private async Task NotifyCustomerEstateInvoiceAsync",
            "private static SalePayableSnapshot ResolveSalePayable");
        customerNotice.Should().Contain("_db.BusinessPartnerUsers");
        customerNotice.Should().Contain("recipients.ExceptWith(notifiedRecipients)");
        customerNotice.Should().Contain("/external-portal/my-property-requests/");
    }

    [Fact]
    public void PortalPropertyBilling_ShowsIssuedFinanceInvoicesAndAllocatedReceipts()
    {
        var portal = ReadSource("src", "ErpSystem.Api", "Controllers", "Estate", "EstateExternalDocumentsController.cs");
        var requests = Slice(portal,
            "public async Task<IActionResult> GetMyRequests",
            "public async Task<IActionResult> GetMyRequest");
        requests.Should().Contain("item.SourceDepartment == \"Sales - Estate Enquiry\"");
        requests.Should().Contain("portalCustomerReferences.Contains(field.Value)");
        var portfolio = Slice(portal,
            "public async Task<IActionResult> GetMyProperties",
            "public async Task<IActionResult> DownloadLegalTransferDraft");
        portfolio.Should().Contain("customerIds.Contains(invoice.BusinessPartnerId)");
        portfolio.Should().Contain("invoice.Status == InvoiceStatus.Sent");
        portfolio.Should().Contain("invoice.Status == InvoiceStatus.Paid");
        portfolio.Should().Contain("invoice.Notes.Contains(\"Estate / Property Management\")");
        portfolio.Should().Contain("_db.Set<PaymentAllocation>()");
        portfolio.Should().Contain("Receipts = receipts ?? new List<ExternalInvoiceReceiptDto>()");
        portfolio.Should().Contain("FullTermLeaseAmount");
        var legacyLease = Slice(portal,
            "private static decimal? ResolveDemarcationLeaseAmount",
            "private static object ToExternalListingDto(EstateLandDemarcation");
        legacyLease.Should().Contain("demarcation.ExternalListingPrice == demarcation.ExternalMonthlyRent");
        legacyLease.Should().Contain("demarcation.TargetSalePrice");
    }

    [Fact]
    public void EstateAgreementTemplates_SeparateSaleLeaseAndRentMetadataAndAmounts()
    {
        var dms = ReadSource("src", "ErpSystem.Api", "Controllers", "DocumentManagement", "DocumentManagementController.cs");
        var defaults = Slice(dms,
            "private static readonly GeneratedDocumentTemplateDefinition[] GeneratedDocumentTemplates",
            "private static GeneratedDocumentTemplateDefinition Template(");
        defaults.Should().Contain("\"EST-RENT-AGREEMENT\"");
        defaults.Should().Contain("{{FullTermLeaseAmount}}");
        defaults.Should().Contain("{{MonthlyRent}}");
        defaults.Should().Contain("{{PurchasePrice}}");

        var metadata = Slice(dms,
            "private async Task EnsureEstateAgreementMetadataTemplatesAsync",
            "private async Task<bool> DefaultGenerationTemplatesExistAsync");
        metadata.Should().Contain("FullTermLeaseAmount");
        metadata.Should().Contain("MonthlyRent");
        metadata.Should().Contain("PurchasePrice");

        var merge = Slice(dms,
            "private async Task EnrichEstateAgreementMergeValuesAsync",
            "private static void SetAuthoritativeMergeValue");
        merge.Should().Contain("demarcation?.ExternalListingPrice");
        merge.Should().Contain("\"FullTermLeaseAmount\"");
        merge.Should().Contain("\"MonthlyRent\"");
        merge.Should().Contain("\"AnnualGroundRent\"");
        merge.Should().Contain("\"PremiumCharge\"");

        var workspace = ReadSource("frontend", "src", "app", "estate", "property-management", "[entityType]", "ListingApplicationWorkspace.tsx");
        workspace.Should().Contain("const lease = isLeaseApplication(selectedCase)");
        workspace.Should().Contain("FullTermLeaseAmount: leaseApplication ? paymentAmount : ''");
        workspace.Should().Contain("MonthlyRent: monthlyRental ? paymentAmount : ''");
    }

    private static string ReadSource(params string[] path)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. path]))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);

        start.Should().BeGreaterThanOrEqualTo(0, $"source should contain {startMarker}");
        end.Should().BeGreaterThan(start, $"source should contain {endMarker} after {startMarker}");
        return source[start..end];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
