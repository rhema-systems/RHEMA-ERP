using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Makes orientation audience rules fire (round 4, lane I3), and says why they did or did not
/// (lane I5).
/// </summary>
/// <remarks>
/// <para>Before this, <c>OrientationEnrollmentTrigger</c> was written by the seeder and read by
/// nothing. See <c>OrientationEnrollmentTriggerService</c> for the semantics.</para>
///
/// <para><b>The event hooks are best-effort by contract.</b> Hire, movement and publish call in
/// AFTER their own work has committed; each hook logs and swallows its own failure, because a
/// hire that stood should not be reported as failed because an orientation could not be created.
/// The nightly sweep evaluates the same dated triggers again, so a hook that failed is caught up
/// within a day.</para>
/// </remarks>
public interface IOrientationEnrollmentTriggerService
{
    /// <summary>A person has been hired or created: run the programmes' <c>OnHire</c> rules for them.</summary>
    Task<OrientationTriggerRunResultDto> OnEmployeeHiredAsync(
        Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>A movement has been implemented: run the transfer or promotion rules it maps to.</summary>
    Task<OrientationTriggerRunResultDto> OnMovementImplementedAsync(
        Guid movementId, CancellationToken cancellationToken = default);

    /// <summary>A programme has just become Active: run its <c>OnProgramPublish</c> rules.</summary>
    Task<OrientationTriggerRunResultDto> OnProgramPublishedAsync(
        Guid programId, Guid? actingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// HR's "enrol the audience now" on one programme: its Manual, publish and scheduled rules —
    /// every rule without a date to count from. <paramref name="preview"/> writes nothing.
    /// </summary>
    Task<OrientationTriggerRunResultDto> EnrolAudienceNowAsync(
        Guid programId, Guid actingUserId, bool preview, CancellationToken cancellationToken = default);

    /// <summary>
    /// The nightly sweep for one tenant: the scheduled rules, plus the dated rules whose day has
    /// come (a hire with a delay, a movement whose effective date has arrived, a hook that failed).
    /// </summary>
    Task<OrientationTriggerRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, bool preview = false,
        CancellationToken cancellationToken = default);

    /// <summary>How many people a rule's target reaches, before it is saved (lane I2).</summary>
    Task<OrientationAudienceReachDto> CountReachAsync(
        OrientationAudienceReachRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Reach for each of a set of saved rules, for the rules list.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountRuleReachAsync(
        Guid tenantId, IEnumerable<OrientationAudienceRule> rules, CancellationToken cancellationToken = default);

    /// <summary>
    /// The display name of each rule's target — the unit, level, position, location or employee
    /// — for <c>OrientationAudienceRuleDto.TargetEntityName</c> (§ 3 defect 15).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> ResolveTargetNamesAsync(
        Guid tenantId, IEnumerable<(HrAudienceTargetType Type, Guid? Id)> targets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refuses a target that cannot be evaluated: a missing id where one is needed, an id on
    /// AllEmployees, or an id that does not name a record of that kind in this tenant.
    /// </summary>
    Task ValidateTargetAsync(
        Guid tenantId, HrAudienceTargetType targetType, Guid? targetEntityId, bool allowEmployee,
        CancellationToken cancellationToken = default);

    /// <summary>Which rules would fire for this employee, and why (lane I5).</summary>
    Task<OrientationTriggerDiagnosisDto> DiagnoseAsync(
        Guid employeeId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Chooses the onboarding plan template for a hire, and says why (round 4, lane I4).
/// </summary>
public interface IOnboardingTemplateApplicabilityService
{
    /// <summary>
    /// The template for a placement. Every argument is optional — an offer may not know the
    /// location yet — and a missing one simply cannot match.
    /// </summary>
    Task<OnboardingTemplateApplicabilityDto> FindApplicableAsync(
        Guid tenantId, Guid? positionId, Guid? organizationUnitId, Guid? organizationLevelId, Guid? locationId,
        CancellationToken cancellationToken = default);

    /// <summary>The template for an existing employee's current placement.</summary>
    Task<OnboardingTemplateApplicabilityDto> FindApplicableForEmployeeAsync(
        Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OnboardingPlanTemplateAudienceDto>> GetAudiencesAsync(
        Guid templateId, CancellationToken cancellationToken = default);

    Task<OnboardingPlanTemplateAudienceDto> AddAudienceAsync(
        Guid templateId, CreateOnboardingPlanTemplateAudienceDto dto, Guid createdByUserId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAudienceAsync(
        Guid templateId, Guid audienceId, Guid deletedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// After a hire's start is confirmed: create the employee's onboarding plan from the applicable
    /// template, unless they already have one or no template applies. Best-effort — logs and
    /// returns null rather than throwing.
    /// </summary>
    Task<Guid?> CreatePlanOnHireAsync(
        Guid tenantId, Guid employeeId, DateOnly startDate, Guid actingUserId,
        CancellationToken cancellationToken = default);
}
