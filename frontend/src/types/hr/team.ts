/**
 * Teams — working groups and who is in them.
 *
 * Transcribed from `TeamDTOs.cs` and confirmed against a live `SLICE=4b node probe-ui-payloads.mjs`.
 * Enums are **string** unions: the API registers `JsonStringEnumConverter`, so a numeric union
 * would compile and match nothing. Every member below is read off the C# enum rather than guessed
 * from the field name — slice 1 invented three legal forms that did not exist and `tsc` had
 * nothing to say about any of them.
 */

import type { AuditFields } from './common';

/** `TeamType` (HREnums.cs:447). Note `Other = 99`, not 7. */
export type TeamType =
  | 'Permanent'
  | 'Project'
  | 'TaskForce'
  | 'CrossFunctional'
  | 'Shift'
  | 'Committee'
  | 'Other';

/** `TeamStatus` (HREnums.cs:461). */
export type TeamStatus = 'Draft' | 'Active' | 'Inactive' | 'Dissolved';

/** `TeamMemberRole` (HREnums.cs:472). */
export type TeamMemberRole = 'Member' | 'DeputyLead' | 'TeamLead' | 'Coordinator' | 'Secretary';

export const TEAM_TYPES: { value: TeamType; label: string; hint: string }[] = [
  { value: 'Permanent', label: 'Permanent', hint: 'A standing group with no planned end date.' },
  { value: 'Project', label: 'Project', hint: 'Formed for a project; give it a project code and an end date.' },
  { value: 'TaskForce', label: 'Task force', hint: 'Short-lived, formed to deal with one thing.' },
  { value: 'CrossFunctional', label: 'Cross-functional', hint: 'Draws people from several units at once.' },
  { value: 'Shift', label: 'Shift', hint: 'A shift crew; attach the shift it works.' },
  { value: 'Committee', label: 'Committee', hint: 'A standing or ad-hoc committee.' },
  { value: 'Other', label: 'Other', hint: 'Anything the categories above do not describe.' },
];

export const TEAM_STATUSES: { value: TeamStatus; label: string; hint: string }[] = [
  { value: 'Draft', label: 'Draft', hint: 'Being set up. Not yet operating.' },
  { value: 'Active', label: 'Active', hint: 'Operating. Only active teams show as live on the organogram.' },
  { value: 'Inactive', label: 'Inactive', hint: 'Paused, but expected to resume.' },
  { value: 'Dissolved', label: 'Dissolved', hint: 'Wound up. Kept for the record.' },
];

export const TEAM_MEMBER_ROLES: { value: TeamMemberRole; label: string }[] = [
  { value: 'TeamLead', label: 'Team lead' },
  { value: 'DeputyLead', label: 'Deputy lead' },
  { value: 'Coordinator', label: 'Coordinator' },
  { value: 'Secretary', label: 'Secretary' },
  { value: 'Member', label: 'Member' },
];

export interface Team extends AuditFields {
  tenantId: string;
  name: string;
  code: string;
  description?: string | null;

  teamType: TeamType;
  status: TeamStatus;

  organizationUnitId?: string | null;
  organizationUnitName?: string | null;

  teamLeadId?: string | null;
  teamLeadName?: string | null;

  parentTeamId?: string | null;
  parentTeamName?: string | null;

  locationId?: string | null;
  locationName?: string | null;

  shiftId?: string | null;
  /** From `ShiftDefinition.ShiftName` — the entity has no `Name`. */
  shiftName?: string | null;

  costCenterCode?: string | null;
  /** Round 2, lane B2: the chart-of-accounts row; costCenterCode above is its snapshot. */
  financeAccountId?: string | null;
  projectCode?: string | null;
  teamEmail?: string | null;

  /** `DateOnly` — serialises as `YYYY-MM-DD`, not an ISO instant. */
  effectiveFrom: string;
  effectiveTo?: string | null;

  maxMembers?: number | null;
  sequence: number;
  isActive: boolean;
  notes?: string | null;

  /** Live memberships of people still on strength. Always sent. */
  memberCount: number;
  childTeamCount: number;
  /** `effectiveTo` is in the past — the team has run its course. */
  hasLapsed: boolean;
}

export interface TeamDetail extends Team {
  /** Current members and former ones alike; filter on `isCurrent`. */
  members: TeamMember[];
}

export interface TeamSummary {
  id: string;
  name: string;
  code: string;
  teamType: TeamType;
  status: TeamStatus;
  memberCount: number;
  isActive: boolean;
}

export interface TeamMember extends AuditFields {
  tenantId: string;
  teamId: string;
  teamName?: string | null;
  employeeId: string;
  employeeName?: string | null;
  employeeNumber?: string | null;
  positionTitle?: string | null;
  role: TeamMemberRole;
  /** Share of this person's time on this team. Matters when they are on several. */
  allocationPercent: number;
  isPrimary: boolean;
  joinDate: string;
  leaveDate?: string | null;
  isActive: boolean;
  notes?: string | null;
  /**
   * The server's single definition of "on the team right now": active, not deleted, and not past
   * its leaving date. Use this rather than re-deriving it — the roster, the member count and the
   * organogram all read the same flag so they cannot disagree.
   */
  isCurrent: boolean;
}

export interface TeamMemberHistoryEntry extends AuditFields {
  tenantId: string;
  teamId: string;
  teamName?: string | null;
  employeeId: string;
  employeeName?: string | null;
  previousRole: TeamMemberRole;
  newRole: TeamMemberRole;
  effectiveFrom: string;
  effectiveTo?: string | null;
  changeReason?: string | null;
}

export interface CreateTeamRequest {
  name: string;
  code: string;
  description?: string | null;
  teamType: TeamType;
  status: TeamStatus;
  organizationUnitId?: string | null;
  teamLeadId?: string | null;
  parentTeamId?: string | null;
  locationId?: string | null;
  shiftId?: string | null;
  costCenterCode?: string | null;
  /** Round 2, lane B2: the chart-of-accounts row; costCenterCode above is its snapshot. */
  financeAccountId?: string | null;
  projectCode?: string | null;
  teamEmail?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  maxMembers?: number | null;
  sequence: number;
  isActive: boolean;
  notes?: string | null;
}

export interface UpdateTeamRequest extends CreateTeamRequest {
  id: string;
}

export interface AddTeamMemberRequest {
  employeeId: string;
  role: TeamMemberRole;
  allocationPercent: number;
  isPrimary: boolean;
  joinDate: string;
  leaveDate?: string | null;
  notes?: string | null;
}

export interface UpdateTeamMemberRequest {
  role: TeamMemberRole;
  allocationPercent: number;
  isPrimary: boolean;
  joinDate: string;
  leaveDate?: string | null;
  isActive: boolean;
  notes?: string | null;
  /** Recorded on the membership history — but only when the role actually moves. */
  changeReason?: string | null;
}

export interface RemoveTeamMemberRequest {
  leaveDate?: string | null;
  reason?: string | null;
}

export function teamStatusLabel(status: TeamStatus): string {
  return TEAM_STATUSES.find((s) => s.value === status)?.label ?? status;
}

export function teamTypeLabel(type: TeamType): string {
  return TEAM_TYPES.find((t) => t.value === type)?.label ?? type;
}

export function teamMemberRoleLabel(role: TeamMemberRole): string {
  return TEAM_MEMBER_ROLES.find((r) => r.value === role)?.label ?? role;
}
