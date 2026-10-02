using ErpSystem.Shared;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The two stages of a staff travel request's approval (travel final closure, lane 2, decision D-7):
/// the traveller's line authority, then HR — the contract between the seeded definition, the workflow
/// engine's context and the travel service.
/// </summary>
/// <remarks>
/// <para><b>Stage 1 is addressed by name, not by role.</b> The engine's context for a travel request
/// carries the logins of the traveller's two nearest line authorities (<see cref="HrLineAuthority"/>),
/// and the stage's approver rules read them, so the task, the notification and the inbox row reach the
/// traveller's own supervisor or head of unit — not every holder of the Manager role, as a role-based
/// stage would — and a supervisor who holds no Manager role can still decide. When no line authority
/// can sign in, neither key resolves and the engine falls back to the step's required role, HR: the
/// travel desk decides the stage, and the travel service records why on the request.</para>
///
/// <para><b>The step names are what the travel service keys on</b> to apply the line rule. A tenant
/// that renames the stage in the workflow designer turns that check off (the engine's routing still
/// applies); a request decided on a superseded definition — the one-step route before lane 2 — is
/// decided as that route said.</para>
/// </remarks>
public static class StaffTravelApprovalLadder
{
    public const string DefinitionName = "Staff Travel Approval";

    /// <summary>Stage 1: the traveller's line authority, or the travel desk when none can sign in.</summary>
    public const string LineStageName = "Line manager approval";

    /// <summary>Stage 2: HR, which also sets the approved budget.</summary>
    public const string DeskStageName = "HR approval";

    /// <summary>The engine context key holding the nearest line authority's login.</summary>
    public const string LineApproverKey = "lineApproverUserId";

    /// <summary>The engine context key holding the next line authority's login, as the first one's backup.</summary>
    public const string SecondLineApproverKey = "lineApproverUserId2";

    /// <summary>How many line authorities stage 1 is addressed to.</summary>
    public const int LineApproverCount = 2;

    /// <summary>Who decides stage 1 when no line authority can sign in.</summary>
    public const string LineStageFallbackRole = Constants.Roles.Hr;

    public static readonly string[] DeskStageRoles = [Constants.Roles.Hr, Constants.Roles.TenantAdmin];
}
