using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public sealed class QsUatActorSeeder(ApplicationDbContext db, UserManager<ApplicationUser> users,
    RoleManager<ApplicationRole> roles, QsUatSeedContext context,
    IBusinessPartnerUserService partnerUsers, IConfiguration configuration)
{
    private const string Marker = "QS UAT bootstrap";

    public static void ValidateTarget(string actualDatabase, string? expectedDatabase, string environment, bool enabled)
    {
        if (!enabled || environment is not ("Test" or "Testing" or "Development") ||
            string.IsNullOrWhiteSpace(expectedDatabase) || actualDatabase != expectedDatabase ||
            !(actualDatabase.StartsWith("RhemaERP_VpsTest_", StringComparison.Ordinal) ||
              actualDatabase.StartsWith("RhemaERP_QsUatVerify_", StringComparison.Ordinal)))
            throw new InvalidOperationException("QS_UAT_TARGET_REQUIRED: explicit test mode and exact approved test database are required.");
    }

    public async Task PrepareAsync(CancellationToken token = default)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Code == "DEFAULT" && !t.IsDeleted, token);
        var bootstrap = await users.FindByNameAsync("qs.uat.bootstrap");
        if (bootstrap is null)
        {
            bootstrap = NewUser(tenant.Id, "qs.uat.bootstrap", "QS UAT", "Bootstrap");
            bootstrap.IsActive = false;
            Require(await users.CreateAsync(bootstrap), "create non-login seed identity");
        }
        else if (bootstrap.TenantId != tenant.Id || bootstrap.IsActive || bootstrap.PasswordHash is not null ||
                 bootstrap.CreatedBy != Marker || (await users.GetRolesAsync(bootstrap)).Count != 0)
            throw new InvalidOperationException("QS_UAT_BOOTSTRAP_CONFLICT: reserved seed identity is not an inactive non-login system identity.");
        context.Initialize(tenant, bootstrap.Id);

        await EnsureActorAsync(tenant.Id, "uat.qs.engineer", "Engineer", ["TDC_CIVIL_ENGINEER"], token);
        foreach (var actor in new[] { ("uat.qs.contractor", "QS-UAT-CONT-001", "Contractor"),
                                      ("uat.qs.consultant", "QS-UAT-CONS-001", "Consultant") })
        {
            var user = await EnsureActorAsync(tenant.Id, actor.Item1, actor.Item3, ["ExternalUser"], token);
            var partner = await db.BusinessPartners.IgnoreQueryFilters().SingleOrDefaultAsync(
                p => p.TenantId == tenant.Id && p.PartnerCode == actor.Item2, token);
            if (partner is null)
            {
                // These are visibly fictional UAT master defaults, not a supplier
                // registration or transaction represented as approved by a person.
                var termId = await db.PaymentTerms.IgnoreQueryFilters().Where(t => t.TenantId == tenant.Id &&
                    !t.IsDeleted && t.IsActive && t.Code == "NET30").Select(t => (Guid?)t.Id).SingleOrDefaultAsync(token);
                partner = new BusinessPartner
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, PartnerCode = actor.Item2,
                    PartnerName = "Fictional QS UAT " + actor.Item3, PartnerType = "Contractor",
                    RegistrationStatus = "Approved", ApprovalStatus = "Approved", IsActive = true,
                    Currency = "GHS", PrimaryEmail = actor.Item1 + "@example.invalid", PaymentTermId = termId,
                    Notes = "Fictional test-only QS prerequisite. No human registration decision or business transaction is implied.",
                    CreatedBy = Marker, CreatedById = bootstrap.Id, CreatedAt = DateTime.UtcNow
                };
                db.BusinessPartners.Add(partner);
                var role = new BusinessPartnerRole
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, BusinessPartnerId = partner.Id,
                    RoleType = BusinessPartnerRoleType.Contractor, Status = BusinessPartnerRoleStatus.Active,
                    StatusReason = "Fictional QS UAT master", CreatedBy = Marker, CreatedById = bootstrap.Id
                };
                db.BusinessPartnerRoles.Add(role);
                db.BusinessPartnerApProfileVersions.Add(new BusinessPartnerApProfileVersion
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, BusinessPartnerRoleId = role.Id,
                    VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
                    EffectiveFrom = DateTime.UtcNow.Date, ApReferenceNumber = actor.Item2, PaymentTermId = termId,
                    ApprovedAtUtc = DateTime.UtcNow,
                    DecisionReason = "Initial fictional QS UAT default; no human approval asserted.",
                    CreatedBy = Marker, CreatedById = bootstrap.Id
                });
                await db.SaveChangesAsync(token);
            }
            else if (partner.IsDeleted || !partner.IsActive || partner.CreatedBy != Marker ||
                     partner.RegistrationStatus != "Approved")
                throw new InvalidOperationException("QS_UAT_PARTNER_CONFLICT: existing partner requires review; no values replaced.");
            await partnerUsers.LinkExistingExternalUserAsync(partner.Id, user.Id);
        }
    }

    private async Task<ApplicationUser> EnsureActorAsync(Guid tenantId, string name, string lastName,
        string[] requiredRoles, CancellationToken token)
    {
        foreach (var role in requiredRoles)
            if (!await roles.RoleExistsAsync(role))
                throw new InvalidOperationException($"QS_UAT_ROLE_MISSING: {role}. Run standard access-control seeding first.");
        var user = await users.FindByNameAsync(name);
        if (user is null)
        {
            var password = configuration["UatBootstrap:SharedPassword"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("QS_UAT_PASSWORD_REQUIRED: supply the protected shared UAT password for missing actors.");
            user = NewUser(tenantId, name, "UAT QS", lastName);
            Require(await users.CreateAsync(user, password), "create UAT actor");
            Require(await users.AddToRolesAsync(user, requiredRoles), "assign UAT actor roles");
        }
        if (user.TenantId != tenantId || !user.IsActive)
            throw new InvalidOperationException($"QS_UAT_ACTOR_CONFLICT: {name} is not active in DEFAULT.");
        var currentRoles = await users.GetRolesAsync(user);
        if (requiredRoles.Contains("ExternalUser") && (currentRoles.Count != 1 || currentRoles[0] != "ExternalUser"))
            throw new InvalidOperationException("QS_UAT_EXTERNAL_ROLE_CONFLICT: existing external account must have only ExternalUser.");
        foreach (var role in requiredRoles)
            if (!currentRoles.Contains(role)) Require(await users.AddToRoleAsync(user, role), "assign missing UAT role");
        var membership = await db.UserTenants.IgnoreQueryFilters().SingleOrDefaultAsync(
            m => m.TenantId == tenantId && m.UserId == user.Id, token);
        if (membership is null)
        {
            db.UserTenants.Add(new UserTenant
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id, IsDefault = true,
                AccessLevel = UserTenantAccessLevel.Standard, Status = UserTenantStatus.Active,
                GrantedAt = DateTime.UtcNow, GrantedBy = Marker, CreatedBy = Marker
            });
            await db.SaveChangesAsync(token);
        }
        else if (membership.IsDeleted || membership.Status != UserTenantStatus.Active ||
                 membership.ExpiresAt.HasValue && membership.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("QS_UAT_MEMBERSHIP_CONFLICT: existing membership retained for review.");
        return user;
    }

    private static ApplicationUser NewUser(Guid tenantId, string name, string firstName, string lastName) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, UserName = name, Email = name + "@example.invalid",
        EmailConfirmed = true, FirstName = firstName, LastName = lastName, IsActive = true,
        AuthenticationProvider = AuthenticationProvider.Local, MustChangePassword = false,
        CreatedAt = DateTime.UtcNow, CreatedBy = Marker
    };

    private static void Require(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"QS_UAT_IDENTITY_FAILED: could not {action}: " +
                string.Join(", ", result.Errors.Select(e => e.Code)));
    }
}
