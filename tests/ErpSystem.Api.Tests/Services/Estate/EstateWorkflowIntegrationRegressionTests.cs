using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class EstateWorkflowIntegrationRegressionTests
{
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
            "private async Task<Guid> ResolveStampDutyDebitAccountIdAsync");

        method.Should().Contain("payment.AccountsPayableSupplierId = invoice.SupplierId;");
        method.Should().Contain("[\"accountsPayableSupplierId\"] = invoice.SupplierId");
        method.Should().NotContain("payment.AccountsPayableSupplierId = payee.Id;");
        method.Should().NotContain("[\"accountsPayableSupplierId\"] = payee.Id");
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
        coreMethod.Should().Contain("asset.ExternalListingType != \"None\"");
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
    public void AcquisitionPublication_DoesNotDependOnRemovedSurveyVerificationCheckboxes()
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
        handoff.Should().Contain("BoundaryVerified = acquisition.StageOrder > (int)AcquisitionProcedure.SurveyVerification");
        handoff.Should().NotContain("CadastralMatch: true");
        handoff.Should().NotContain("OverlapCleared: true");
        handoff.Should().NotContain("BoundaryConfirmed: true");
        handoff.Should().NotContain("BoundaryVerified = true");
    }

    [Fact]
    public void AcquisitionDocumentMatching_PreservesSlashesInConfiguredDocumentNames()
    {
        var controller = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "LandAcquisitionsController.cs");
        var matcher = Slice(
            controller,
            "private static bool MatchesWorkflowDocumentRequirement",
            "private static string NormalizeDocumentValue");

        matcher.Should().Contain("RemoveLogicalDocumentExtension(document.FileName)");
        matcher.Should().Contain("Path.GetExtension(fileName)");
        matcher.Should().Contain("fileName[..^extension.Length]");
        matcher.Should().NotContain("Path.GetFileNameWithoutExtension(document.FileName)");
    }

    [Fact]
    public void AcquisitionWorkspaceDocuments_SatisfyLegacyDocumentChecklistItems()
    {
        var controller = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "LandAcquisitionsController.cs");
        var attachmentBuilder = Slice(
            controller,
            "private List<WorkflowTaskAttachmentDto> BuildStageTaskAttachments",
            "private static IReadOnlyList<WorkflowDocumentRequirementDto> ReadWorkflowStageDocumentRequirements");

        attachmentBuilder.Should().Contain("check.RequiresDocument");
        attachmentBuilder.Should().Contain("ChecklistItemId = string.IsNullOrWhiteSpace(checklistItem?.Id)");
        attachmentBuilder.Should().Contain("checklistItem.Id.Trim()");
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

        listingAction.Should().Contain("<Button");
        listingAction.Should().Contain("disabled");
        listingAction.Should().Contain("sendLandToPortalListings(selected.asset)");
        listingAction.IndexOf("disabled", StringComparison.Ordinal)
            .Should().BeLessThan(
                listingAction.IndexOf("onClick", StringComparison.Ordinal));
        listingAction.Should().NotContain("<Link");
        listingAction.Should().NotContain("asChild");
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
    public void ProjectLandClaims_RejectExternallyPublishedAssets()
    {
        var source = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Projects",
            "ProjectService.Construction.cs");
        var resolver = Slice(
            source,
            "private async Task<ReadyProjectLandSelection> RequireReadyProjectLandDemarcationAsync",
            "private async Task<(int ActiveCount, bool HasUnusedPortion, bool AllPortionsVerified)> GetProjectLandAvailabilityAsync");

        resolver.Should().Contain("&& !asset.IsPublishedToExternalPortal");
        resolver.Should().Contain(
            "&& asset.Status == EstateManagedAssetStatus.LandBank");
        resolver.Should().Contain("&& asset.IsReadyForProjectManagement");
    }

    [Fact]
    public void ClosedProjects_CannotSynchronizeDevelopmentProfileLand()
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
        var updateMethod = Slice(
            projectService,
            "private async Task<ProjectDetailDto> UpdateProjectCoreAsync",
            "private static void EnsureProjectIsMutable");
        var mutabilityGuard = Slice(
            projectService,
            "private static void EnsureProjectIsMutable",
            "public async Task SubmitProjectForApprovalAsync");
        var developmentProfileUpsert = Slice(
            constructionService,
            "private async Task<ProjectDevelopmentProfile> UpsertDevelopmentProfileEntityAsync",
            "private async Task<string?> ResolveDevelopmentProfileLandReferenceAsync");

        updateMethod.Should().Contain("EnsureProjectIsMutable(project);");
        developmentProfileUpsert.Should().Contain(
            "EnsureProjectIsMutable(project);");
        mutabilityGuard.Should().Contain("ProjectStatuses.Closed");
        mutabilityGuard.Should().Contain("ProjectStatuses.Archived");
        mutabilityGuard.Should().Contain(
            "Closed or archived projects are read-only");
    }

    [Fact]
    public void ListingDeepLinks_DoNotFallbackToAnUnrelatedAsset()
    {
        var page = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "listings",
            "page.tsx");
        var selection = Slice(
            page,
            "const selected = React.useMemo",
            "const loadDocuments = React.useCallback");

        selection.Should().Contain(
            "() => assets.find((asset) => asset.id === selectedId)");
        selection.Should().NotContain("|| assets[0]");
        selection.Should().Contain("if (requestedAssetId)");
        selection.Should().Contain("setRequestedAssetUnavailable(!requestedAsset)");
        selection.Should().Contain("setSelectedId(requestedAsset?.id ?? null)");
        page.Should().Contain(
            "The requested managed asset could not be loaded.");
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
        page.Should().Contain("selected.asset.externalListingType !== 'None' ||");
        page.Should().Contain(
            "Remove the land from Portal Listings before marking it ready for a project.");
    }

    [Fact]
    public void PortalListings_StageOnlyEligibleLandAndProjectHandoffProperty()
    {
        var managedAssets = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var externalController = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateExternalDocumentsController.cs");
        var listingsPage = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "listings",
            "page.tsx");
        var landPage = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "land-management",
            "page.tsx");
        var externalPage = ReadSource(
            "frontend",
            "src",
            "app",
            "external-portal",
            "property-listings",
            "page.tsx");
        var propertyRegister = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "PropertyUnitRegister.tsx");

        managedAssets.Should().Contain("query.PortalListingCandidates != true");
        managedAssets.Should().Contain("item.ExternalListingType != \"None\"");
        managedAssets.Should().Contain("!asset.IsPublishedFromProject");
        managedAssets.Should().Contain("asset.IsReadyForProjectManagement = false;");
        managedAssets.Should().Contain("Enter the monthly rent before publishing a rental listing.");
        managedAssets.Should().Contain("ExternalLeaseTermMonths");

        externalController.Should().Contain("asset.Demarcations.Any(item => !item.IsDeleted)");
        externalController.Should().Contain("asset.SourceType == EstateManagedAssetSourceType.ProjectUnit");
        externalController.Should().Contain("asset.ExternalMonthlyRent > 0");
        externalController.Should().Contain("asset.ExternalLeaseTermMonths > 0");

        listingsPage.Should().Contain("portalListingCandidates: true");
        listingsPage.Should().Contain("Rent per month");
        listingsPage.Should().Contain("Rental duration");
        listingsPage.Should().Contain("Remove from Portal Listings");
        landPage.Should().Contain("sendLandToPortalListings");
        landPage.Should().Contain("Send to Portal Listings");
        propertyRegister.Should().Contain("sendProjectPropertyToPortalListings");
        propertyRegister.Should().Contain("asset.isPublishedFromProject");
        propertyRegister.Should().Contain("'Send to portal'");
        externalPage.Should().Contain("Rent per month");
        externalPage.Should().Contain("formatLeaseTerm");
    }

    [Fact]
    public void GroundRentAssessment_FollowsEstateSopAndIsInheritedByLeaseManagement()
    {
        var procedureService = ReadSource(
            "src",
            "ErpSystem.Api",
            "Services",
            "ProcedureCaseService.cs");
        var leaseSetup = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "LeaseSetupWorkspace.tsx");
        var propertyManagementPage = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "page.tsx");
        var occupancyWorkspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "OccupancyAvailabilityWorkspace.tsx");
        var tenantOccupantWorkspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "TenantOccupantWorkspace.tsx");
        var billingWorkspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "BillingServiceChargeWorkspace.tsx");
        var moveInHandoverWorkspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "MoveInHandoverWorkspace.tsx");
        var recordsIndexWorkspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "RecordsIndexWorkspace.tsx");
        var estateLandManagementFrontendService = ReadSource(
            "frontend",
            "src",
            "services",
            "estate-land-management.service.ts");
        var managedAssetsController = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateManagedAssetsController.cs");
        var groundRentService = ReadSource(
            "src",
            "ErpSystem.Api",
            "Services",
            "Estate",
            "GroundRentAdministrationService.cs");
        var groundRentWorkspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "GroundRentAdministrationWorkspace.tsx");
        var managedAssetService = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateManagedAssetService.cs");
        var externalListingsController = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateExternalDocumentsController.cs");
        var listingApplicationWorkspace = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "property-management",
            "[entityType]",
            "ListingApplicationWorkspace.tsx");
        var externalRequestsPage = ReadSource(
            "frontend",
            "src",
            "app",
            "external-portal",
            "my-property-requests",
            "page.tsx");
        var externalEstateServices = ReadSource(
            "frontend",
            "src",
            "services",
            "external-estate-services.service.ts");
        var procedureWorkspace = ReadSource(
            "frontend",
            "src",
            "components",
            "procedures",
            "ProcedureCaseWorkspace.tsx");

        procedureService.Should().Contain("plotSizeAcres * ratePerAcre");
        procedureService.Should().Contain("asset.GroundRentRatePerAcre = ratePerAcre;");
        procedureService.Should().Contain("asset.GroundRentComputed = computed;");
        procedureService.Should().Contain(
            "asset.GroundRentPayable = decimal.Ceiling(rawGroundRent);");

        leaseSetup.Should().Contain("Land Ground Rent Assessment");
        leaseSetup.Should().Contain("selectedAsset.groundRentRatePerAcre");
        leaseSetup.Should().Contain("/estate/EstateLandsPartiallyServiced");
        leaseSetup.Should().Contain("/estate/EstateLeaseRenewal");
        leaseSetup.Should().Contain("/legal/LegalTerminationRecognition");
        leaseSetup.Should().Contain("/estate/property-management/EstatePropertyManagementOccupancyAvailability");
        leaseSetup.Should().Contain("/estate/property-management/EstatePropertyManagementMoveInMoveOutHandover");
        leaseSetup.Should().Contain("/estate/property-management/EstatePropertyManagementBillingServiceCharge");
        leaseSetup.Should().Contain("/estate/property-management/EstatePropertyManagementDocumentRecordIndex");
        leaseSetup.Should().Contain("buildProcedurePrefillHref");
        leaseSetup.Should().Contain("field_propertyReference");
        leaseSetup.Should().Contain("Reserved pending lease");
        leaseSetup.Should().Contain("Billing hold until signed agreement and move-in");
        leaseSetup.Should().Contain("Start billing from move-in / agreement start date");
        leaseSetup.Should().Contain("Blocked - signed agreement pending");
        leaseSetup.Should().Contain("Signed agreement / property file reference");
        propertyManagementPage.Should().Contain("OccupancyAvailabilityWorkspace");
        propertyManagementPage.Should().Contain("TenantOccupantWorkspace");
        propertyManagementPage.Should().Contain("BillingServiceChargeWorkspace");
        propertyManagementPage.Should().Contain("MoveInHandoverWorkspace");
        propertyManagementPage.Should().Contain("RecordsIndexWorkspace");
        propertyManagementPage.Should().Contain("EstatePropertyManagementOccupancyAvailability");
        propertyManagementPage.Should().Contain("EstatePropertyManagementTenantOccupant");
        propertyManagementPage.Should().Contain("EstatePropertyManagementBillingServiceCharge");
        propertyManagementPage.Should().Contain("EstatePropertyManagementMoveInMoveOutHandover");
        propertyManagementPage.Should().Contain("EstatePropertyManagementDocumentRecordIndex");
        propertyManagementPage.Should().NotContain("ProcedureCaseWorkspace");
        occupancyWorkspace.Should().Contain("Occupancy and Availability Board");
        occupancyWorkspace.Should().Contain("updateOccupancy");
        occupancyWorkspace.Should().Contain("Reserved, occupied, sold, blocked, maintenance, and retired");
        occupancyWorkspace.Should().Contain("buildHandoverHref");
        occupancyWorkspace.Should().Contain("buildBillingHref");
        tenantOccupantWorkspace.Should().Contain("Tenant / Occupant Register");
        tenantOccupantWorkspace.Should().Contain("Business Partner registration remains owned by the existing Business Partner module");
        billingWorkspace.Should().Contain("Billing / Service Charge Operations");
        billingWorkspace.Should().Contain("This screen does not duplicate Finance AR ledgers");
        billingWorkspace.Should().Contain("EstatePropertyManagementGroundRent");
        moveInHandoverWorkspace.Should().Contain("Move-in / Move-out / Handover Board");
        moveInHandoverWorkspace.Should().Contain("Save handover update");
        moveInHandoverWorkspace.Should().Contain("EstatePropertyManagementDocumentRecordIndex");
        recordsIndexWorkspace.Should().Contain("Property Documents / Records Index");
        recordsIndexWorkspace.Should().Contain("Central DMS remains the file/version owner");
        recordsIndexWorkspace.Should().Contain("getDocuments");
        estateLandManagementFrontendService.Should().Contain("updateOccupancy");
        estateLandManagementFrontendService.Should().Contain("UnderMaintenance");
        estateLandManagementFrontendService.Should().Contain("Blocked");
        managedAssetsController.Should().Contain("[HttpPatch(\"{id:guid}/occupancy\")]");
        managedAssetService.Should().Contain("asset.AssetType == EstateManagedAssetType.Land");
        managedAssetService.Should().Contain("Assess and approve the annual ground rent before publishing a land rental listing.");
        managedAssetService.Should().Contain("UpdateOccupancyAsync");
        managedAssetService.Should().Contain("Sold assets cannot be reopened from Occupancy / Availability.");
        managedAssetService.Should().Contain("Reserve, lease, or occupy the asset only after a customer or occupant is linked in Lease Management.");
        managedAssetService.Should().Contain("asset.Status = EstateManagedAssetStatus.Reserved;");
        managedAssetService.Should().Contain("asset.IsAvailableForLease = false;");
        managedAssetService.Should().Contain("asset.IsAvailableForSale = false;");
        managedAssetService.Should().Contain("asset.IsPublishedToExternalPortal = false;");
        managedAssetService.Should().Contain("asset.ExternalListingStatus = \"Withdrawn\";");
        externalListingsController.Should().Contain("asset.GroundRentPayable.HasValue");
        externalListingsController.Should().Contain("customer-decision");
        externalListingsController.Should().Contain("signed-agreement");
        externalListingsController.Should().Contain("Customer acceptance is available only after Property Management approves the request.");
        externalListingsController.Should().Contain("Signed lease / tenancy agreement");
        listingApplicationWorkspace.Should().Contain("Agreement generation");
        listingApplicationWorkspace.Should().Contain("generateDocumentFromTemplate");
        listingApplicationWorkspace.Should().Contain("generatedAgreementReference");
        externalRequestsPage.Should().Contain("submitPropertyRequestDecision");
        externalRequestsPage.Should().Contain("uploadSignedAgreement");
        externalRequestsPage.Should().Contain("Approved - customer response required");
        externalEstateServices.Should().Contain("customer-decision");
        externalEstateServices.Should().Contain("signed-agreement");
        procedureWorkspace.Should().Contain("prefilledFieldValues");
        groundRentService.Should().Contain("ResolveBillingStart(account.EstateManagedAsset)");
        groundRentService.Should().Contain("lease agreement start date or right-of-entry / move-in date");
        groundRentService.Should().Contain("signed lease or tenancy agreement reference");
        groundRentService.Should().Contain("periodStart = latestCharge?.PeriodEnd.Date.AddDays(1) ?? billingStart.Value.Date");
        groundRentWorkspace.Should().Contain("account.canGenerateInvoice");
        groundRentWorkspace.Should().Contain("account.invoiceHoldReason");
        leaseSetup.Should().NotContain("AnnualFlat");
        leaseSetup.Should().NotContain("PerSquareMeterAnnual");
        leaseSetup.Should().NotContain("groundRentBillingFrequency");
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
    public void ExternalPropertyRequests_EnterListingApplicationsBeforeOccupancyChanges()
    {
        var controller = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "EstateExternalDocumentsController.cs");
        var requestMethod = Slice(
            controller,
            "public async Task<IActionResult> CreateListingRequest",
            "private HashSet<string> BuildIdentityTerms");
        var catalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "PropertyManagementProcedureCatalogService.cs");
        var procedureCases = ReadSource(
            "src",
            "ErpSystem.Api",
            "Services",
            "ProcedureCaseService.cs");
        var stageResolution = Slice(
            procedureCases,
            "private async Task<List<StageSeed>> BuildStageSeedsAsync",
            "private (string Title, IReadOnlyList<FieldSeed> Fields");
        var seeding = ReadSource(
            "src",
            "ErpSystem.Api",
            "Services",
            "DatabaseSeedingService.cs");
        var propertyPermissions = ReadSource(
            "src",
            "ErpSystem.Shared",
            "PropertyManagementPermissions.cs");

        requestMethod.Should().Contain("EstatePropertyManagementListingApplication");
        requestMethod.Should().NotContain("EstatePropertyManagementOccupancyAvailability");
        requestMethod.Should().Contain("[\"reservationStatus\"] = \"Not reserved\"");
        requestMethod.Should().NotContain("[\"requestedStatus\"] = \"Reserved\"");
        catalog.Should().Contain("EstatePropertyManagementListingApplication");
        catalog.Should().NotContain("Stage(");
        catalog.Should().NotContain("Doc(");
        catalog.Should().NotContain("Handoff(");
        catalog.Should().Contain("new FacilitiesProcedureWorkspace(procedure, [], [], [], [], [])");
        stageResolution.Should().Contain("requires an active published workflow");
        stageResolution.Should().Contain("BuildConfiguredWorkflowChecklist(step)");
        stageResolution.Should().Contain("configuration?.QualityConfig?.QualityChecks");
        stageResolution.Should().NotContain("[step.Description ?? $\"Complete {step.Name}.\"]");
        seeding.Should().NotContain("EnsureEstatePropertyRequestWorkflowSeededAsync");
        seeding.Should().NotContain("PropertyRequestWorkflowCatalog.Stages");
        seeding.Should().NotContain("EnsureEstateLandAcquisitionWorkflowSeededAsync");
        seeding.Should().NotContain("LandAcquisitionWorkflowCatalog.Stages");
        File.Exists(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ErpSystem.Api",
            "Services",
            "Estate",
            "PropertyRequestWorkflowCatalog.cs")).Should().BeFalse();
        File.Exists(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ErpSystem.Api",
            "Services",
            "Estate",
            "LandAcquisitionWorkflowCatalog.cs")).Should().BeFalse();
        seeding.Should().Contain("PropertyManagementPermissions.All");
        seeding.Should().Contain("EnsurePropertyManagementTestRoleAssignmentsAsync");
        seeding.Should().Contain("PropertyManagementRoles.Supervisor");
        propertyPermissions.Should().Contain("property-management.case.read");
        propertyPermissions.Should().Contain("property-management.case.update");
        propertyPermissions.Should().Contain("public static readonly string[] OfficerNames");
        propertyPermissions.Should().Contain("public static readonly string[] SupervisorNames");
        propertyPermissions.Should().Contain("public static readonly string[] ManagerNames");
        procedureCases.Should().Contain("GetCurrentStageFieldKeysAsync");
        procedureCases.Should().Contain("configuration?.FormFields");
        procedureCases.Should().Contain("new JsonStringEnumConverter()");
        requestMethod.Should().Contain("EstatePropertyManagementListingApplication");
        procedureCases.Should().Contain("_db.ChangeTracker.Clear()");
        procedureCases.Should().Contain("item.WorkflowInstanceId, workflowInstance.Id");
    }

    [Fact]
    public void FacilitiesManagement_UsesDedicatedOperationalWorkspaces()
    {
        var facilitiesPage = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "facilities",
            "[entityType]",
            "page.tsx");
        var sidebar = ReadSource(
            "frontend",
            "src",
            "components",
            "layout",
            "sidebar.tsx");
        var facilitiesService = ReadSource(
            "frontend",
            "src",
            "services",
            "estate-facilities.service.ts");
        var dutyRosterController = ReadSource(
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Estate",
            "FacilitiesDutyRosterController.cs");
        var dutyRosterEntity = ReadSource(
            "src",
            "ErpSystem.Core",
            "Entities",
            "Estate",
            "EstateFacilityDutyRoster.cs");
        var dbContext = ReadSource(
            "src",
            "ErpSystem.Data",
            "ApplicationDbContext.cs");

        facilitiesPage.Should().Contain("FacilitiesWorkflowOverview");
        facilitiesPage.Should().Contain("Dedicated Facilities workspace");
        facilitiesPage.Should().Contain("No generic case view");
        facilitiesPage.Should().Contain("Operational stage board");
        facilitiesPage.Should().Contain("Documents required");
        facilitiesPage.Should().Contain("Module handoffs");
        facilitiesPage.Should().Contain("Maintenance Intake");
        facilitiesPage.Should().Contain("Complaint Management");
        facilitiesPage.Should().Contain("Service Provider Operations");
        facilitiesPage.Should().Contain("Staff & Cleaner Duties");
        facilitiesPage.Should().Contain("Facilities Asset Operating View");
        facilitiesPage.Should().Contain("Facilities Billing / Service Charge");
        facilitiesPage.Should().Contain("Facilities Document Index & DMS Readiness");
        facilitiesPage.Should().Contain("Cleaner Timetable / Duty Roster");
        facilitiesPage.Should().Contain("createDutyRosterItem");
        facilitiesPage.Should().Contain("markDutyCompleted");
        facilitiesPage.Should().Contain("Apartment / unit");
        facilitiesPage.Should().Contain("Maintenance ref.");
        facilitiesPage.Should().Contain("Complaint ref.");
        facilitiesPage.Should().NotContain("ProcedureCaseWorkspace");
        facilitiesService.Should().Contain("getDutyRoster");
        facilitiesService.Should().Contain("createDutyRosterItem");
        facilitiesService.Should().Contain("updateDutyAttendance");
        dutyRosterController.Should().Contain("api/estate/facilities/duty-roster");
        dutyRosterController.Should().Contain("LinkedMaintenanceReference");
        dutyRosterController.Should().Contain("LinkedComplaintReference");
        dutyRosterEntity.Should().Contain("EmployeeProfileId");
        dutyRosterEntity.Should().Contain("PropertyUnit");
        dutyRosterEntity.Should().Contain("Frequency");
        dbContext.Should().Contain("DbSet<EstateFacilityDutyRoster>");
        dbContext.Should().Contain("EstateFacilityDutyRosters");

        sidebar.Should().Contain("EstateFacilityMaintenance");
        sidebar.Should().Contain("EstateFacilityComplaint");
        sidebar.Should().Contain("EstateFacilityServiceProvider");
        sidebar.Should().Contain("EstateFacilityStaffCleaner");
        sidebar.Should().Contain("EstateFacilityAssetRegister");
        sidebar.Should().Contain("EstateFacilityBillingServiceCharge");
        sidebar.Should().Contain("EstateFacilityDocument");
        sidebar.Should().Contain("EstateFacilityPropertySite");
        sidebar.Should().Contain("EstateFacilityLease");
    }

    [Fact]
    public void LegalManagement_UsesDedicatedOperationalWorkspaces()
    {
        var legalPage = ReadSource(
            "frontend",
            "src",
            "app",
            "legal",
            "[entityType]",
            "page.tsx");
        var sidebar = ReadSource(
            "frontend",
            "src",
            "components",
            "layout",
            "sidebar.tsx");

        legalPage.Should().Contain("LegalMatterOperations");
        legalPage.Should().Contain("LegalWorkflowOverview");
        legalPage.Should().Contain("Dedicated Legal workspace");
        legalPage.Should().Contain("No generic case view");
        legalPage.Should().Contain("Legal stage board");
        legalPage.Should().Contain("Documents required");
        legalPage.Should().Contain("Legal handoffs");
        legalPage.Should().Contain("Workflow not configured");
        legalPage.Should().Contain("Matter control");
        legalPage.Should().Contain("Document control");
        legalPage.Should().Contain("Approval control");
        legalPage.Should().Contain("Closeout control");
        legalPage.Should().NotContain("ProcedureCaseWorkspace");

        sidebar.Should().Contain("LegalProcedure");
        sidebar.Should().Contain("LegalMortgage");
        sidebar.Should().Contain("LegalMortgageInPrinciple");
        sidebar.Should().Contain("LegalCourtProcess");
        sidebar.Should().Contain("LegalOtherCourtProcess");
        sidebar.Should().Contain("LegalTerminationRecognition");
        sidebar.Should().Contain("LegalAssignmentSubleaseVesting");
        sidebar.Should().Contain("LegalLeaseVariationRenewalSublease");
        sidebar.Should().Contain("LegalTransfer");
        sidebar.Should().Contain("LegalOpinionAdvisory");
        sidebar.Should().Contain("LegalExternalCounsel");
    }

    [Fact]
    public void SopCompletionWorkspaces_AreWorkflowConfigurableAndVisible()
    {
        var sidebar = ReadSource(
            "frontend",
            "src",
            "components",
            "layout",
            "sidebar.tsx");
        var workflowEntityCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Workflow",
            "WorkflowEntityTypeCatalogService.cs");
        var workflowStatusAdapters = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Workflow",
            "WorkflowStatusAdapters.cs");
        var frontendEntityMapping = ReadSource(
            "frontend",
            "src",
            "components",
            "workflow",
            "entityTypeMapping.ts");
        var dmsPage = ReadSource(
            "frontend",
            "src",
            "app",
            "document-management",
            "page.tsx");
        var estateProcedurePage = ReadSource(
            "frontend",
            "src",
            "app",
            "estate",
            "[entityType]",
            "page.tsx");
        var legalCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Legal",
            "LegalProcedureCatalogService.cs");
        var planningCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Planning",
            "PlanningProcedureCatalogService.cs");
        var facilitiesCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "FacilitiesProcedureCatalogService.cs");

        foreach (var entityType in new[]
                 {
                     "EstatePropertyManagementGroundRent",
                     "EstateFacilityPropertySite",
                     "EstateFacilityLease",
                     "LegalOpinionAdvisory",
                     "LegalExternalCounsel",
                     "CentralDocumentMetadataTemplate",
                     "CentralDocumentGovernance"
                 })
        {
            workflowEntityCatalog.Should().Contain(entityType);
            frontendEntityMapping.Should().Contain(entityType);
        }

        foreach (var entityType in new[]
                 {
                     "EstatePropertyManagementGroundRent",
                     "EstateFacilityPropertySite",
                     "EstateFacilityLease",
                     "LegalOpinionAdvisory",
                     "LegalExternalCounsel"
                 })
        {
            workflowStatusAdapters.Should().Contain(entityType);
        }

        sidebar.Should().Contain("/document-management/CentralDocumentMetadataTemplate");
        sidebar.Should().Contain("/document-management/CentralDocumentGovernance");
        sidebar.Should().Contain("/estate/facilities/EstateFacilityPropertySite");
        sidebar.Should().Contain("/estate/facilities/EstateFacilityLease");
        sidebar.Should().Contain("/legal/LegalOpinionAdvisory");
        sidebar.Should().Contain("/legal/LegalExternalCounsel");
        dmsPage.Should().Contain("'CentralDocumentMetadataTemplate'");
        dmsPage.Should().Contain("'CentralDocumentGovernance'");
        estateProcedurePage.Should().Contain("EstateSopOperations");
        estateProcedurePage.Should().Contain("Estate SOP Operations");
        estateProcedurePage.Should().Contain("Workflow configured in setup");
        estateProcedurePage.Should().Contain("/reports?module=estate");
        estateProcedurePage.Should().Contain("Finance / Revenue Check");
        estateProcedurePage.Should().Contain("Central DMS");
        legalCatalog.Should().Contain("Legal opinion requests");
        legalCatalog.Should().Contain("External counsel instructions");
        legalCatalog.Should().NotContain("Stage(");
        planningCatalog.Should().Contain("through configured workflows");
        planningCatalog.Should().NotContain("Stage(");
        facilitiesCatalog.Should().Contain("Facilities site, building, floor, unit, common-area");
        facilitiesCatalog.Should().Contain("Facilities lease, occupancy, viewing, billing trigger");
        facilitiesCatalog.Should().NotContain("Stage(");
    }

    [Fact]
    public void EstateLegalFacilitiesPropertyManagementAndDms_DoNotPredefineWorkflowStages()
    {
        var estateCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateProcedureCatalogService.cs");
        var propertyManagementCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "PropertyManagementProcedureCatalogService.cs");
        var facilitiesCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "FacilitiesProcedureCatalogService.cs");
        var legalCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Legal",
            "LegalProcedureCatalogService.cs");
        var planningCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Planning",
            "PlanningProcedureCatalogService.cs");
        var dmsCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "DocumentManagement",
            "CentralDocumentManagementService.cs");
        var dmsPage = ReadSource(
            "frontend",
            "src",
            "app",
            "document-management",
            "[entityType]",
            "page.tsx");
        var procedureCases = ReadSource(
            "src",
            "ErpSystem.Api",
            "Services",
            "ProcedureCaseService.cs");

        foreach (var catalog in new[]
                 {
                     estateCatalog,
                     propertyManagementCatalog,
                     facilitiesCatalog,
                     legalCatalog,
                     planningCatalog
                 })
        {
            catalog.Should().NotContain("Stage(");
            catalog.Should().NotContain("Doc(");
            catalog.Should().NotContain("Handoff(");
            catalog.Should().NotContain("StageCount");
            catalog.Should().Contain(", 0,");
        }

        propertyManagementCatalog.Should().Contain("new FacilitiesProcedureWorkspace(procedure, [], [], [], [], [])");
        facilitiesCatalog.Should().Contain("new FacilitiesProcedureWorkspace(procedure, [], [], [], [], [])");
        legalCatalog.Should().Contain("new LegalProcedureWorkspace(procedure, [], [], [], [], [])");
        planningCatalog.Should().Contain("new PlanningProcedureWorkspace(procedure, [], [], [], [], [])");
        dmsCatalog.Should().NotContain("Stage(");
        dmsCatalog.Should().NotContain("Field(");
        dmsCatalog.Should().NotContain("Outputs(");
        dmsCatalog.Should().NotContain("Handoffs(");
        dmsCatalog.Should().Contain(", 0,");
        dmsCatalog.Should().Contain("item,\n            [],\n            [],\n            [],\n            []");
        dmsPage.Should().Contain("Workflow not configured");
        dmsPage.Should().Contain("No DMS stages, checklists, fields, outputs, or handoffs are");
        procedureCases.Should().Contain("\"Estate\" => []");
        procedureCases.Should().Contain("procedure.Title,\n            [],\n            []");
        procedureCases.Should().NotContain("EstateStageNames(procedure.EntityType)");
    }

    [Fact]
    public void EstatePropertyFacilitiesLegalPlanningAndDmsReports_RouteThroughCentralReports()
    {
        var sidebar = ReadSource(
            "frontend",
            "src",
            "components",
            "layout",
            "sidebar.tsx");
        var userReportsPage = ReadSource(
            "frontend",
            "src",
            "app",
            "reports",
            "page.tsx");
        var adminReportsPage = ReadSource(
            "frontend",
            "src",
            "app",
            "administration",
            "reports",
            "page.tsx");
        var estateCatalog = ReadSource(
            "src",
            "ErpSystem.Core",
            "Services",
            "Estate",
            "EstateProcedureCatalogService.cs");
        var planningPage = ReadSource(
            "frontend",
            "src",
            "app",
            "development",
            "planning",
            "page.tsx");
        var planningWorkspacePage = ReadSource(
            "frontend",
            "src",
            "app",
            "development",
            "planning",
            "[entityType]",
            "page.tsx");

        foreach (var module in new[]
                 {
                     "planning",
                     "estate",
                     "property-management",
                     "facilities",
                     "legal",
                     "dms"
                 })
        {
            sidebar.Should().Contain($"/reports?module={module}");
            userReportsPage.Should().Contain($"id: '{module}'");
            adminReportsPage.Should().Contain($"id: '{module}'");
        }

        sidebar.Should().NotContain("/estate/EstateReportingControls");
        estateCatalog.Should().NotContain("Reporting and Controls");
        estateCatalog.Should().NotContain("EstateReportingControls");
        sidebar.Should().Contain("/reports?module=planning");
        planningPage.Should().Contain("/reports?module=planning");
        planningPage.Should().Contain("Configured in Workflow Setup");
        planningPage.Should().NotContain("procedure.stageCount} stages");
        planningWorkspacePage.Should().Contain("PlanningSopOperations");
        planningWorkspacePage.Should().Contain("Workflow configured in setup");
        planningWorkspacePage.Should().Contain("/reports?module=planning");
        userReportsPage.Should().Contain("new URLSearchParams(window.location.search).get('module')");
        userReportsPage.Should().Contain("normalizeReportModule(report.type) === selectedModule");
    }

    private static string ReadSource(params string[] path)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. path]));

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
