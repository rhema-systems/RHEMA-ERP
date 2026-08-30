using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public sealed record CivilEngineeringProjectEngineerCandidate(
    Guid UserId,
    string RoleName,
    bool IsActiveProjectMember,
    bool IsActiveInternalUser,
    bool IsConfiguredCandidate);

public static class CivilEngineeringProjectEngineerAssignmentPolicy
{
    public static readonly IReadOnlySet<string> SourceCivilRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CivilEngineeringAccessControlRegistry.SupervisingEngineerRole,
        CivilEngineeringAccessControlRegistry.CivilEngineerRole
    };

    public static IReadOnlyList<CivilEngineeringProjectEngineerAuthority> Authorities { get; } =
    [
        CivilEngineeringProjectEngineerAuthority.SiteSupervision,
        CivilEngineeringProjectEngineerAuthority.SiteSupervisionAndInstructions,
        CivilEngineeringProjectEngineerAuthority.FullProjectEngineer
    ];

    public static IReadOnlyList<string> ValidateCandidate(CivilEngineeringProjectEngineerCandidate candidate)
    {
        var errors = new List<string>();
        if (candidate.UserId == Guid.Empty)
            errors.Add("Select a Civil Engineer or Supervising Civil Engineer.");
        if (!candidate.IsActiveInternalUser)
            errors.Add("The selected Project Engineer must be an active internal user in the current tenant.");
        if (!candidate.IsActiveProjectMember)
            errors.Add("The selected Project Engineer must be an active member of this project.");
        if (!SourceCivilRoles.Contains(candidate.RoleName))
            errors.Add("Only a Civil Engineer or Supervising Civil Engineer may be appointed as Project Engineer.");
        if (!candidate.IsConfiguredCandidate)
            errors.Add("The effective Civil Engineering supervision policy does not permit the selected engineering role as Project Engineer.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateAssignment(
        DateTime effectiveFrom,
        CivilEngineeringProjectEngineerAuthority authority,
        DateTime? existingEffectiveFrom = null)
    {
        var errors = new List<string>();
        if (effectiveFrom == default)
            errors.Add("Effective from is required.");
        if (!Authorities.Contains(authority))
            errors.Add("Select a supported Project Engineer authority level.");
        if (existingEffectiveFrom.HasValue && effectiveFrom.Date <= existingEffectiveFrom.Value.Date)
            errors.Add("A replacement Project Engineer must have an effective date after the current appointment start date.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateEnd(DateTime effectiveFrom, DateTime effectiveTo, string? reason)
    {
        var errors = new List<string>();
        if (effectiveTo == default)
            errors.Add("Effective to is required.");
        if (effectiveTo.Date < effectiveFrom.Date)
            errors.Add("Effective to cannot be before the appointment effective-from date.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            errors.Add("Provide a reason of at least 5 characters when ending a Project Engineer appointment.");
        return errors;
    }
}
