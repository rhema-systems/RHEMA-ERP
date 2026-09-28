using System.Data;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>Opt-in, fictional Estate input for QS UAT. Estate remains the sole writer of land and readiness.</summary>
public sealed class EstateUatLandSeeder(
    ApplicationDbContext db,
    IEstateManagedAssetService estate,
    IUnitOfWork unitOfWork,
    ICurrentUserProvider currentUser,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    public const string SeedName = "QS-UAT-LAND-001";
    public const string SeedMarker = "[RHEMA:QS-UAT-LAND:v1]";
    public const string SyntheticBoundary = "[[0,0],[0,100],[100,100],[100,0]]";

    public async Task<EstateUatLandSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        ValidateWriteTarget(environment.EnvironmentName, configuration["QsUat:Enabled"],
            configuration["QsUat:ExpectedDatabase"], db.Database.GetDbConnection().Database);
        var tenantId = await RequireDefaultTenantAsync(cancellationToken);
        if (unitOfWork.HasActiveTransaction || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("QS UAT land seeding requires its own clean service scope.");

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Estate's unit of work joins this caller-owned transaction; its individual commits
            // cannot leave a half-created UAT parcel if a later readiness check fails.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await unitOfWork.AcquireTransactionLockAsync($"qs-uat-land:{tenantId:N}:{SeedName}", cancellationToken);
                var existing = await FindSeedAsync(tenantId, cancellationToken);
                if (existing is not null)
                {
                    var retained = await DescribeAsync(existing, false, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return retained;
                }

                var created = await estate.CreateManualExistingLandAsync(CreateRequest());
                await estate.CreateLandDemarcationAsync(created.Id, new SaveEstateLandDemarcationDto
                {
                    Description = "QS UAT synthetic full-parcel demarcation; not a legal survey",
                    BoundaryCoordinates = SyntheticBoundary,
                    BeaconCount = 4,
                    BoundaryVerified = true
                });
                await estate.MarkReadyForProjectManagementAsync(created.Id);
                var persisted = await FindSeedAsync(tenantId, cancellationToken)
                    ?? throw new InvalidOperationException("The Estate owner did not retain the QS UAT parcel identity.");
                var result = await DescribeAsync(persisted, true, cancellationToken);
                if (!result.ReadyForProjectSelection)
                    throw new InvalidOperationException("The new QS UAT demarcation did not pass the Estate project-selector contract.");
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    /// <summary>Reads the same Estate selector used by Projects; never repairs or rewrites a parcel.</summary>
    public async Task<EstateUatLandSeedResult?> VerifyAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = await RequireDefaultTenantAsync(cancellationToken);
        var existing = await FindSeedAsync(tenantId, cancellationToken);
        return existing is null ? null : await DescribeAsync(existing, false, cancellationToken);
    }

    internal static void ValidateWriteTarget(string environmentName, string? enabled, string? expectedDatabase, string actualDatabase)
    {
        if (!(environmentName.Equals("Test", StringComparison.OrdinalIgnoreCase) ||
              environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase) ||
              environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase)) ||
            !bool.TryParse(enabled, out var isEnabled) || !isEnabled)
            throw new InvalidOperationException("QS UAT seeding requires QsUat:Enabled=true in Test, Testing or Development.");
        if (string.IsNullOrWhiteSpace(expectedDatabase) ||
            !string.Equals(expectedDatabase, actualDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("QS UAT seeding requires an exact QsUat:ExpectedDatabase match.");
    }

    private async Task<Guid> RequireDefaultTenantAsync(CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId == Guid.Empty || currentUser.UserId == Guid.Empty ||
            !await db.Tenants.IgnoreQueryFilters().AsNoTracking().AnyAsync(value =>
                value.Id == tenantId && value.Code == "DEFAULT" && !value.IsDeleted, cancellationToken))
            throw new InvalidOperationException("QS UAT land seeding requires an identified bootstrap actor in the DEFAULT tenant.");
        return tenantId;
    }

    private async Task<EstateManagedAsset?> FindSeedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var matches = await db.EstateManagedAssets.IgnoreQueryFilters().AsNoTracking()
            .Where(value => value.TenantId == tenantId &&
                (value.Name == SeedName || value.SurveyPlanNumber == SeedName ||
                 (value.Notes != null && value.Notes.Contains(SeedMarker))))
            .ToListAsync(cancellationToken);
        if (matches.Count > 1)
            throw new InvalidOperationException("Multiple records match the QS UAT parcel; existing data was preserved for review.");
        var existing = matches.SingleOrDefault();
        if (existing is not null && (existing.IsDeleted || existing.Notes?.Contains(SeedMarker, StringComparison.Ordinal) != true))
            throw new InvalidOperationException("The QS UAT parcel identifier is already retained by another or deleted record; no data was changed.");
        return existing;
    }

    private async Task<EstateUatLandSeedResult> DescribeAsync(EstateManagedAsset asset, bool created, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var demarcations = await estate.GetLandDemarcationsAsync(asset.Id);
        var ready = (await estate.GetProjectReadyLandDemarcationsAsync())
            .Where(value => value.AssetId == asset.Id).ToArray();
        var selected = ready.FirstOrDefault();
        var single = demarcations.Count == 1 ? demarcations[0] : null;
        var assigned = demarcations.Any(value => value.IsAssignedToProject);
        return new EstateUatLandSeedResult(asset.Id, asset.AssetCode, asset.Name,
            selected?.DemarcationId ?? single?.Id, selected?.LandReference ?? single?.LandReference,
            created, ready.Length > 0, assigned,
            created ? "Created fictional land through Estate validation and verified its project selector." :
            assigned ? "Existing UAT land is assigned to a project; existing data was preserved." :
            ready.Length > 0 ? "Existing UAT land is selectable; existing data was preserved." :
            "Existing UAT land is not currently selectable; review its Estate state. Existing data was preserved.");
    }

    internal static CreateManualExistingLandDto CreateRequest() => new()
    {
        Name = SeedName,
        Description = "FICTIONAL QS UAT land. Synthetic geometry and ownership; not a real parcel or legal instrument.",
        Location = "Synthetic QS UAT training site",
        Purpose = "QS and Project Management acceptance testing only",
        ZoningClassification = "UAT synthetic training zone",
        PlanningComplianceStatus = "UAT fixture only",
        GisLayerReference = "UAT-SYNTHETIC-LOCAL-FEET",
        CadastreDescription = "Synthetic local grid in feet; no geographic or cadastral authority",
        Region = "UAT Region", District = "UAT District", Town = "UAT Training Site",
        AreaValue = 10000m, AreaUnit = "sq ft", AreaSquareMeters = 929.0304m,
        SurveyorName = "Fictional UAT Surveyor (not licensed survey evidence)",
        SurveyDate = new DateTime(2026, 1, 1), SurveyPlanNumber = SeedName, MapSheetNumber = "UAT-MAP-001",
        BeaconCount = 4, BoundaryCoordinates = SyntheticBoundary, BoundaryVerified = true,
        ValuationAmount = 100000m, OwnerConsiderationCost = 100000m, TotalCapitalizedCost = 100000m,
        Currency = "GHS", Notes = SeedMarker + " Fictional bootstrap data only; do not use for legal, survey or financial reporting.",
        OwnershipHistory = [new ExistingLandOwnerDto
        {
            OwnerName = "Fictional QS UAT Owner", OwnershipType = "UAT synthetic ownership",
            InterestHeld = "Training only", IdentificationType = "Synthetic UAT identifier",
            IdentificationNumber = "UAT-NOT-A-LEGAL-ID", ContactNumber = "0000000000",
            Address = "Fictional QS UAT training site", OwnershipStartDate = new DateTime(2026, 1, 1),
            OwnershipPercentage = 100m, IsCurrentOwner = true
        }]
    };
}

public sealed record EstateUatLandSeedResult(Guid AssetId, string AssetCode, string Name,
    Guid? DemarcationId, string? LandReference, bool Created, bool ReadyForProjectSelection,
    bool AssignedToProject, string Message);
