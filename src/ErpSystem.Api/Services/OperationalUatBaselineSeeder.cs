using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using ErpSystem.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>
/// Reconciles the reusable actors and master data used by the Procurement,
/// Inventory, Finance and connected QS UAT walkthroughs. The operation is
/// intentionally create-only for users and business master records: existing
/// passwords and tenant-owned values are never replaced.
/// </summary>
public sealed class OperationalUatBaselineSeeder(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IDatabaseSeedingService databaseSeedingService,
    ProcurementAccessControlSeeder procurementAccessControlSeeder,
    QuantitySurveyAccessControlSeeder quantitySurveyAccessControlSeeder,
    FinanceDataSeeder financeDataSeeder,
    IConfiguration configuration,
    ILogger<OperationalUatBaselineSeeder> logger)
{
    private const string SeedActor = "Operational UAT baseline";
    private const string SharedPasswordKey = "UatBootstrap:SharedPassword";

    private static readonly UatActor[] DedicatedActors =
    [
        new("procurementofficer", "Procurement", "Officer", ["TDC_PROCUREMENT_OFFICER"]),
        new("procurementapprover", "Procurement", "Approver",
            ["Procurement User", "TDC_HEAD_OF_PROCUREMENT", "TDC_STORES_MANAGER"]),
        new("procurementevaluator", "Procurement", "Evaluator", ["TDC_EVALUATOR"]),
        new("tdc0102-checker-201531", "Procurement", "Committee Checker", ["TDC_EVALUATOR"]),
        new("financereviewer", "Finance", "Reviewer", ["TDC_FINANCE_REVIEWER", "TDC_LEGAL_REVIEWER"]),
        new("financeapprover", "Finance", "Approver", ["Finance User", "TDC_ETC_MEMBER"]),
        new("storesofficer", "Stores", "Officer", ["Inventory User", "TDC_STORES_OFFICER"]),
        new("storesmanager", "Stores", "Manager", ["Inventory User", "TDC_STORES_MANAGER"]),
        new("uat.qs.preparer", "UAT QS", "Preparer", [Constants.Roles.Employee, "TDC_QUANTITY_SURVEYOR"]),
        new("uat.qs.reviewer", "UAT QS", "Reviewer",
            [Constants.Roles.Employee, "TDC_QUANTITY_SURVEYOR", "TDC_SUPERVISING_QUANTITY_SURVEYOR"]),
        new("uat.qs.approver", "UAT QS", "Approver",
            [Constants.Roles.Manager, "TDC_QUANTITY_SURVEYOR", "TDC_SUPERVISING_QUANTITY_SURVEYOR"])
    ];

    private static readonly UatRoleAugmentation[] ExistingActorRoles =
    [
        new("admin", ["TDC_SUPERVISING_QUANTITY_SURVEYOR"]),
        new("manager", ["TDC_USER_DEPARTMENT_HEAD", "TDC_STORES_OFFICER", "TDC_STORES_MANAGER"]),
        new("employee", ["TDC_MANAGING_DIRECTOR", "TDC_INTERNAL_AUDIT"]),
        new("ap.officer", ["TDC_HEAD_OF_PROCUREMENT", "TDC_STORES_MANAGER"]),
        new("finance.manager", ["TDC_EVALUATOR"])
    ];

    private static readonly UomSeed[] UnitsOfMeasure =
    [
        new("EA", "Each", "Quantity", "each", true, 0),
        new("EACH", "Each", "Quantity", "each", true, 1),
        new("KG", "Kilogram", "Weight", "kg", false, 2),
        new("L", "Litre", "Volume", "L", false, 3),
        new("PACK", "Pack", "Quantity", "pack", false, 4)
    ];

    private static readonly InventoryCategorySeed[] InventoryCategories =
    [
        new("FILT", "Filters", "EA"),
        new("FLD", "Fluids", "L"),
        new("IGN", "Ignition", "EA"),
        new("LUB", "Lubricants", "L"),
        new("PROJECT-DEMO", "Project Demo Materials", "EA"),
        new("TOOLS", "Maintenance Tools", "EA")
    ];

    private static readonly InventoryItemSeed[] InventoryItems =
    [
        new("FILTER-AIR-001", "Air Filter - Heavy Duty", "FILT", "EA", 24.50m),
        new("FILTER-OIL-001", "Oil Filter - Heavy Duty", "FILT", "EA", 17.50m),
        new("FLUID-BRAKE-DOT4", "Brake Fluid DOT 4 (1 Liter)", "FLD", "L", 10.50m),
        new("OIL-5W30-5L", "Engine Oil 5W-30 (5 Liters)", "LUB", "L", 31.50m),
        new("PM-BARCODE-DEVICE", "Barcode Device Kit", "PROJECT-DEMO", "EA", 750m),
        new("SKU-001", "PVC Pipe 50mm", "PROJECT-DEMO", "EACH", 2000m),
        new("SPARK-PLUG-001", "Spark Plug - Iridium", "IGN", "EA", 8.40m),
        new("TOOL-SCAN-001", "Heavy Duty Diagnostic Scanner", "TOOLS", "EA", 595m),
        new("UAT-PO-2026-0001-WIRELESS-KEYBOARD", "Wireless Keyboard", "FILT", "EA", 399m)
    ];

    public async Task<OperationalUatSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pending.Length > 0)
        {
            throw new InvalidOperationException(
                "Operational UAT seeding requires an already migrated database. Pending migrations: " +
                string.Join(", ", pending));
        }

        var missingDedicatedActors = new List<string>();
        foreach (var actor in DedicatedActors)
        {
            if (await userManager.FindByNameAsync(actor.UserName) is null)
                missingDedicatedActors.Add(actor.UserName);
        }

        var sharedPassword = configuration[SharedPasswordKey];
        if (missingDedicatedActors.Count > 0 && string.IsNullOrWhiteSpace(sharedPassword))
        {
            throw new InvalidOperationException(
                "Set UatBootstrap__SharedPassword as a process-scoped secret before creating missing UAT actors: " +
                string.Join(", ", missingDedicatedActors));
        }

        await databaseSeedingService.SeedTestUsersAsync();
        await procurementAccessControlSeeder.SeedAsync(cancellationToken, preserveExistingWorkflows: true);
        await quantitySurveyAccessControlSeeder.SeedAsync(cancellationToken);

        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(
            value => value.Code == "DEFAULT" && !value.IsDeleted,
            cancellationToken);

        var createdUsers = 0;
        var addedRoles = 0;
        foreach (var actor in DedicatedActors)
        {
            var outcome = await EnsureActorAsync(tenant.Id, actor, sharedPassword!, cancellationToken);
            createdUsers += outcome.Created ? 1 : 0;
            addedRoles += outcome.AddedRoles;
        }

        foreach (var augmentation in ExistingActorRoles)
            addedRoles += await EnsureExistingActorRolesAsync(augmentation, cancellationToken);

        var policySeedActor = await userManager.FindByNameAsync("procurementapprover")
            ?? throw new InvalidOperationException("Required UAT actor is missing: procurementapprover");
        var policySeedResult = await new ProcurementUatPolicySetSeeder(db).SeedTenantAsync(
            tenant.Id,
            policySeedActor.Id,
            cancellationToken);

        // Identity and Procurement reconciliation materialize a broad graph in this scoped
        // context. Finance seeding updates deterministic system accounts and must begin with a
        // clean tracker; otherwise a stale tracked account can produce a false concurrency
        // failure after the Finance manifest performs its guarded reconciliation.
        ResetTrackingAtSeederBoundary();
        try
        {
            await financeDataSeeder.SeedAsync();
        }
        catch (InvalidOperationException exception) when (IsPreservableFinanceGovernanceBlocker(exception))
        {
            // A user-owned legacy mapping is Finance evidence. Do not silently classify, disable,
            // or replace it merely to provision the cross-module UAT fixture. Finance can resolve
            // the reported mapping through its governed workspace while the other UAT masters
            // continue to reconcile.
            logger.LogWarning(
                "Operational UAT Finance reconciliation retained a user-owned mapping for Finance review: {FinanceBlocker}",
                exception.Message);
        }
        ResetTrackingAtSeederBoundary();
        var masterCounts = await EnsureInventoryMasterDataAsync(tenant.Id, cancellationToken);
        var createdResponsibilities = await EnsureResponsibilityScopesAsync(tenant.Id, cancellationToken);

        var result = new OperationalUatSeedResult(
            createdUsers,
            addedRoles,
            masterCounts.UnitsOfMeasure,
            masterCounts.Categories,
            masterCounts.Warehouses,
            masterCounts.Locations,
            masterCounts.Items,
            masterCounts.Suppliers,
            createdResponsibilities);

        logger.LogInformation(
            "Operational UAT baseline reconciled. Created users={CreatedUsers}, role links={AddedRoles}, procurement policies={ProcurementPolicies}, procurement workflows={ProcurementWorkflows}, UOM={Units}, categories={Categories}, warehouses={Warehouses}, locations={Locations}, items={Items}, suppliers={Suppliers}, responsibility scopes={Responsibilities}.",
            result.CreatedUsers,
            result.AddedRoleAssignments,
            policySeedResult.CreatedPolicies,
            policySeedResult.CreatedWorkflows,
            result.CreatedUnitsOfMeasure,
            result.CreatedCategories,
            result.CreatedWarehouses,
            result.CreatedLocations,
            result.CreatedItems,
            result.CreatedSuppliers,
            result.CreatedResponsibilityAssignments);

        return result;
    }

    internal void ResetTrackingAtSeederBoundary() => db.ChangeTracker.Clear();

    internal static bool IsPreservableFinanceGovernanceBlocker(InvalidOperationException exception) =>
        exception.Message.StartsWith(
            "FINANCE_CLASSIFICATION_ENABLED_MAPPING_LINEAGE_INVALID:",
            StringComparison.Ordinal);

    internal async Task<InventoryMasterSeedCounts> EnsureInventoryMasterDataAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var createdUnits = 0;
        foreach (var seed in UnitsOfMeasure)
        {
            var exists = await db.UnitsOfMeasure.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.Code == seed.Code,
                cancellationToken);
            if (exists) continue;
            db.UnitsOfMeasure.Add(new UnitOfMeasure
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = seed.Code, Name = seed.Name,
                Category = seed.Category, Symbol = seed.Symbol, IsBaseUnit = seed.IsBaseUnit,
                IsActive = true, SortOrder = seed.SortOrder, CreatedAt = now, CreatedBy = SeedActor
            });
            createdUnits++;
        }
        await db.SaveChangesAsync(cancellationToken);

        var unitByCode = await db.UnitsOfMeasure.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted)
            .ToDictionaryAsync(value => value.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var categoryByCode = await db.InventoryCategories.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted)
            .ToDictionaryAsync(value => value.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var createdCategories = 0;
        foreach (var seed in InventoryCategories)
        {
            if (categoryByCode.ContainsKey(seed.Code)) continue;
            var category = new InventoryCategory
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = seed.Code, Name = seed.Name,
                Description = "Reusable UAT inventory master data.", DefaultUnitOfMeasure = seed.DefaultUom,
                IsActive = true, CreatedAt = now, CreatedBy = SeedActor
            };
            db.InventoryCategories.Add(category);
            categoryByCode[seed.Code] = category;
            createdCategories++;
        }

        var createdWarehouses = 0;
        var warehouseByCode = await db.Warehouses.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted)
            .ToDictionaryAsync(value => value.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var seed in new[]
                 {
                     new WarehouseSeed("DEMO-PM", "Project Demo Warehouse", false),
                     new WarehouseSeed("WH-02", "HQ", true)
                 })
        {
            if (warehouseByCode.ContainsKey(seed.Code)) continue;
            var warehouse = new Warehouse
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = seed.Code, Name = seed.Name,
                Description = "Reusable UAT warehouse master data.", IsActive = true,
                IsDefault = seed.IsDefault, WarehouseType = "Standard", CreatedAt = now, CreatedBy = SeedActor
            };
            db.Warehouses.Add(warehouse);
            warehouseByCode[seed.Code] = warehouse;
            createdWarehouses++;
        }
        await db.SaveChangesAsync(cancellationToken);

        var createdLocations = 0;
        foreach (var seed in new[]
                 {
                     new WarehouseLocationSeed("DEMO-PM", "DEFAULT", "Default bin", true),
                     new WarehouseLocationSeed("DEMO-PM", "LOC-001", "Main", false),
                     new WarehouseLocationSeed("WH-02", "DEFAULT", "Default bin", true)
                 })
        {
            var warehouse = warehouseByCode[seed.WarehouseCode];
            var exists = await db.WarehouseLocations.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.WarehouseId == warehouse.Id &&
                value.LocationCode == seed.Code,
                cancellationToken);
            if (exists) continue;
            db.WarehouseLocations.Add(new WarehouseLocation
            {
                Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id,
                LocationCode = seed.Code, Name = seed.Name, LocationType = "Bin",
                IsDefault = seed.IsDefault, IsActive = true, IsPickingLocation = true,
                IsReceivingLocation = true, CreatedAt = now, CreatedBy = SeedActor
            });
            createdLocations++;
        }

        var createdItems = 0;
        foreach (var seed in InventoryItems)
        {
            var exists = await db.InventoryItems.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.ItemCode == seed.Code,
                cancellationToken);
            if (exists) continue;
            var item = new InventoryItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ItemCode = seed.Code, Name = seed.Name,
                Description = "Reusable UAT procurement and inventory item.",
                CategoryId = categoryByCode[seed.CategoryCode].Id, UnitOfMeasure = seed.UnitOfMeasure,
                StandardCost = seed.StandardCost, AverageCost = seed.StandardCost,
                LastPurchaseCost = seed.StandardCost, Status = ItemStatus.Active,
                CreatedAt = now, CreatedBy = SeedActor
            };
            db.InventoryItems.Add(item);
            // Receipt and purchase conversion use the item/UOM identity, not only
            // the display code. Create the base lineage with the new item, leaving
            // existing tenant-owned item definitions and conversions unchanged.
            db.ItemUnitsOfMeasure.Add(new ItemUnitOfMeasure
            {
                Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = item.Id,
                UnitOfMeasureId = unitByCode[seed.UnitOfMeasure].Id, ConversionToBase = 1m,
                IsBaseUnit = true, IsStockingUnit = true, IsPurchaseUnit = true,
                IsActive = true, CreatedAt = now, CreatedBy = SeedActor
            });
            createdItems++;
        }
        await db.SaveChangesAsync(cancellationToken);

        var createdSuppliers = 0;
        var paymentTermId = await db.PaymentTerms.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.Code == "NET30")
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        foreach (var seed in new[]
                 {
                     new SupplierSeed("CONT-GH-ADOM-BUILD", "Adom Construction Ltd", "Accra", "+233-302-880-440"),
                     new SupplierSeed("SUP260001", "Harbourline Goods Supply Ltd", "Tema", "+233-000-000-001")
                 })
        {
            var partner = await db.BusinessPartners.IgnoreQueryFilters().SingleOrDefaultAsync(value =>
                value.TenantId == tenantId && value.PartnerCode == seed.Code,
                cancellationToken);
            if (partner is not null)
            {
                // The Projects baseline already owns this approved fictional Adom
                // contractor. Reuse that identity without changing its details.
                // Only the missing initial supplier capability may be introduced;
                // any existing role/profile remains governed, including drafts.
                var knownSeedIdentity =
                    (seed.Code == "CONT-GH-ADOM-BUILD" && partner.CreatedBy == "System" && partner.PartnerType == "Contractor") ||
                    (partner.CreatedBy == SeedActor && partner.PartnerType == "Supplier");
                if (!knownSeedIdentity || partner.IsDeleted || !partner.IsActive ||
                    partner.RegistrationStatus != "Approved" || partner.ApprovalStatus != "Approved") continue;
                var existingRoles = await db.BusinessPartnerRoles.IgnoreQueryFilters()
                    .Where(value => value.TenantId == tenantId && value.BusinessPartnerId == partner.Id)
                    .Select(value => new { value.Id, value.RoleType }).ToListAsync(cancellationToken);
                var roleIds = existingRoles.Select(value => value.Id).ToArray();
                if (existingRoles.Any(value => value.RoleType == BusinessPartnerRoleType.Supplier) ||
                    (roleIds.Length > 0 && await db.BusinessPartnerApProfileVersions.IgnoreQueryFilters().AnyAsync(value =>
                        value.TenantId == tenantId && roleIds.Contains(value.BusinessPartnerRoleId), cancellationToken))) continue;
            }
            else
            {
                partner = new BusinessPartner
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, PartnerCode = seed.Code, PartnerName = seed.Name,
                    LegalName = seed.Name, PartnerType = "Supplier", RegistrationStatus = "Approved",
                    ApprovalStatus = "Approved", IsActive = true, Currency = "GHS",
                    PhysicalCity = seed.City, PhysicalCountry = "Ghana", PrimaryPhone = seed.Phone,
                    PrimaryEmail = seed.Code.ToLowerInvariant() + "@uat.invalid", PaymentTerms = "Net 30",
                    PaymentTermId = paymentTermId, SubjectToWithholdingDeduction = false,
                    Notes = "Reusable fictional supplier for Procurement and Inventory UAT.",
                    CreatedAt = now, CreatedBy = SeedActor
                };
                db.BusinessPartners.Add(partner);
                createdSuppliers++;
            }
            var role = new BusinessPartnerRole
            {
                Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = partner.Id,
                RoleType = BusinessPartnerRoleType.Supplier, Status = BusinessPartnerRoleStatus.Active,
                ActiveFromUtc = now, CreatedAt = now, CreatedBy = SeedActor
            };
            db.BusinessPartnerRoles.Add(role);
            db.BusinessPartnerApProfileVersions.Add(new BusinessPartnerApProfileVersion
            {
                Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerRoleId = role.Id,
                VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
                EffectiveFrom = now, ApReferenceNumber = seed.Code, PaymentTermId = paymentTermId,
                SubjectToWithholding = false, ApprovedAtUtc = now,
                DecisionReason = "Initial fictional Operational UAT supplier profile.",
                CreatedAt = now, CreatedBy = SeedActor
            });
        }
        await db.SaveChangesAsync(cancellationToken);

        return new InventoryMasterSeedCounts(
            createdUnits, createdCategories, createdWarehouses, createdLocations, createdItems, createdSuppliers);
    }

    private async Task<ActorSeedOutcome> EnsureActorAsync(
        Guid tenantId,
        UatActor actor,
        string sharedPassword,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(actor.UserName);
        var created = false;
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserName = actor.UserName,
                Email = actor.UserName.Replace('.', '-') + "@uat.invalid",
                EmailConfirmed = true, FirstName = actor.FirstName, LastName = actor.LastName,
                AuthenticationProvider = AuthenticationProvider.Local, IsActive = true,
                MustChangePassword = false, CreatedAt = DateTime.UtcNow, CreatedBy = SeedActor
            };
            var createResult = await userManager.CreateAsync(user, sharedPassword);
            if (!createResult.Succeeded)
                throw IdentityFailure($"create UAT actor {actor.UserName}", createResult);
            created = true;
        }

        await EnsureTenantAccessAsync(user.Id, tenantId, cancellationToken);
        var addedRoles = await EnsureRolesAsync(user, actor.Roles);
        return new ActorSeedOutcome(created, addedRoles);
    }

    private async Task<int> EnsureExistingActorRolesAsync(
        UatRoleAugmentation augmentation,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(augmentation.UserName)
            ?? throw new InvalidOperationException(
                $"Baseline user {augmentation.UserName} was not created by the standard test-user seeder.");
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(
            value => value.Code == "DEFAULT" && !value.IsDeleted,
            cancellationToken);
        await EnsureTenantAccessAsync(user.Id, tenant.Id, cancellationToken);
        return await EnsureRolesAsync(user, augmentation.Roles);
    }

    private async Task<int> EnsureRolesAsync(ApplicationUser user, IReadOnlyCollection<string> roleNames)
    {
        var additions = 0;
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new ApplicationRole(roleName)
                {
                    Id = Guid.NewGuid(), NormalizedName = roleManager.NormalizeKey(roleName),
                    IsSystemRole = true, CreatedAt = DateTime.UtcNow, CreatedBy = SeedActor
                });
                if (!roleResult.Succeeded)
                    throw IdentityFailure($"create role {roleName}", roleResult);
            }

            if (await userManager.IsInRoleAsync(user, roleName)) continue;
            var addResult = await userManager.AddToRoleAsync(user, roleName);
            if (!addResult.Succeeded)
                throw IdentityFailure($"assign role {roleName} to {user.UserName}", addResult);
            additions++;
        }
        return additions;
    }

    private async Task EnsureTenantAccessAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await db.UserTenants.IgnoreQueryFilters().AnyAsync(value =>
            value.UserId == userId && value.TenantId == tenantId && !value.IsDeleted,
            cancellationToken);
        if (existing) return;
        db.UserTenants.Add(new UserTenant
        {
            Id = Guid.NewGuid(), UserId = userId, TenantId = tenantId,
            AccessLevel = UserTenantAccessLevel.Standard, Status = UserTenantStatus.Active,
            IsDefault = true, GrantedAt = DateTime.UtcNow, GrantedBy = SeedActor,
            Notes = "Created by the operational UAT baseline seeder.", CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedActor
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> EnsureResponsibilityScopesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new ResponsibilitySeed("manager", "TDC_STORES_OFFICER", ProcurementWarehouseScopeMode.Restricted, ["DEMO-PM"]),
            new ResponsibilitySeed("manager", "TDC_STORES_MANAGER", ProcurementWarehouseScopeMode.All, []),
            new ResponsibilitySeed("procurementapprover", "TDC_STORES_MANAGER", ProcurementWarehouseScopeMode.Restricted, ["DEMO-PM"]),
            new ResponsibilitySeed("ap.officer", "TDC_STORES_MANAGER", ProcurementWarehouseScopeMode.Restricted, ["DEMO-PM"]),
            new ResponsibilitySeed("financereviewer", "TDC_FINANCE_REVIEWER", ProcurementWarehouseScopeMode.Restricted, ["DEMO-PM"]),
            new ResponsibilitySeed("employee", "TDC_INTERNAL_AUDIT", ProcurementWarehouseScopeMode.Restricted, ["DEMO-PM"]),
            new ResponsibilitySeed("storesofficer", "TDC_STORES_OFFICER", ProcurementWarehouseScopeMode.Restricted, ["DEMO-PM", "WH-02"]),
            new ResponsibilitySeed("storesmanager", "TDC_STORES_MANAGER", ProcurementWarehouseScopeMode.Restricted, ["DEMO-PM", "WH-02"])
        };
        var created = 0;
        foreach (var seed in definitions)
        {
            var user = await userManager.FindByNameAsync(seed.UserName)
                ?? throw new InvalidOperationException($"Required UAT actor is missing: {seed.UserName}");
            var role = await roleManager.FindByNameAsync(seed.RoleName)
                ?? throw new InvalidOperationException($"Required UAT role is missing: {seed.RoleName}");
            var exists = await db.ProcurementResponsibilityAssignments.IgnoreQueryFilters().AnyAsync(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.UserId == user.Id &&
                value.RoleName == seed.RoleName && value.IsActive && value.EffectiveTo == null,
                cancellationToken);
            if (exists) continue;

            var assignment = new ProcurementResponsibilityAssignment
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id, RoleId = role.Id,
                RoleName = seed.RoleName, WarehouseScopeMode = seed.WarehouseScope,
                LocationScopeMode = ProcurementLocationScopeMode.All,
                EffectiveFrom = DateTime.UtcNow.Date, IsActive = true,
                Reason = "Reusable scope for Procurement and Inventory UAT.",
                CreatedAt = DateTime.UtcNow, CreatedBy = SeedActor
            };
            db.ProcurementResponsibilityAssignments.Add(assignment);

            foreach (var warehouseCode in seed.WarehouseCodes)
            {
                var warehouseId = await db.Warehouses.IgnoreQueryFilters()
                    .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.Code == warehouseCode)
                    .Select(value => value.Id)
                    .SingleAsync(cancellationToken);
                db.ProcurementResponsibilityWarehouses.Add(new ProcurementResponsibilityWarehouse
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AssignmentId = assignment.Id,
                    WarehouseId = warehouseId, CreatedAt = DateTime.UtcNow, CreatedBy = SeedActor
                });
            }
            created++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    private static InvalidOperationException IdentityFailure(string operation, IdentityResult result) =>
        new($"Could not {operation}: " + string.Join(", ", result.Errors.Select(value => value.Description)));

    private sealed record UatActor(string UserName, string FirstName, string LastName, string[] Roles);
    private sealed record UatRoleAugmentation(string UserName, string[] Roles);
    private sealed record UomSeed(string Code, string Name, string Category, string Symbol, bool IsBaseUnit, int SortOrder);
    private sealed record InventoryCategorySeed(string Code, string Name, string DefaultUom);
    private sealed record InventoryItemSeed(string Code, string Name, string CategoryCode, string UnitOfMeasure, decimal StandardCost);
    private sealed record WarehouseSeed(string Code, string Name, bool IsDefault);
    private sealed record WarehouseLocationSeed(string WarehouseCode, string Code, string Name, bool IsDefault);
    private sealed record SupplierSeed(string Code, string Name, string City, string Phone);
    private sealed record ResponsibilitySeed(
        string UserName,
        string RoleName,
        ProcurementWarehouseScopeMode WarehouseScope,
        string[] WarehouseCodes);
    private sealed record ActorSeedOutcome(bool Created, int AddedRoles);
}

public sealed record OperationalUatSeedResult(
    int CreatedUsers,
    int AddedRoleAssignments,
    int CreatedUnitsOfMeasure,
    int CreatedCategories,
    int CreatedWarehouses,
    int CreatedLocations,
    int CreatedItems,
    int CreatedSuppliers,
    int CreatedResponsibilityAssignments);

internal sealed record InventoryMasterSeedCounts(
    int UnitsOfMeasure,
    int Categories,
    int Warehouses,
    int Locations,
    int Items,
    int Suppliers);
