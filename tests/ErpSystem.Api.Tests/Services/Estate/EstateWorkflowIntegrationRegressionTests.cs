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

        procedureService.Should().Contain("plotSizeAcres * ratePerAcre");
        procedureService.Should().Contain("asset.GroundRentRatePerAcre = ratePerAcre;");
        procedureService.Should().Contain("asset.GroundRentComputed = computed;");
        procedureService.Should().Contain(
            "asset.GroundRentPayable = decimal.Ceiling(rawGroundRent);");

        leaseSetup.Should().Contain("Approved Ground Rent Assessment");
        leaseSetup.Should().Contain("selectedAsset.groundRentRatePerAcre");
        leaseSetup.Should().Contain("/estate/EstateLandsPartiallyServiced");
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
