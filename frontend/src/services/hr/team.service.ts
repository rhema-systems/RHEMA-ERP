import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  AddTeamMemberRequest,
  CreateTeamRequest,
  RemoveTeamMemberRequest,
  Team,
  TeamDetail,
  TeamMember,
  TeamMemberHistoryEntry,
  TeamSummary,
  UpdateTeamMemberRequest,
  UpdateTeamRequest,
} from '@/types/hr/team';

/**
 * The teams register. Backend route: `api/hr/teams`.
 *
 * A team is who actually works together, as distinct from the organization unit a person is
 * formally posted into — which is why membership carries an allocation percentage and a
 * primary-team flag: in a matrix organisation a person can be on several teams at once.
 *
 * ⚠ Reads are open to any authenticated user; **writes are SuperAdmin / TenantAdmin / HR**. A team
 * defines who reports to whom and how someone's time is split, so it is not self-service.
 *
 * ⚠ `remove` does not delete. It stamps a leaving date and deactivates the membership, so the
 * roster keeps the record of who was on the team and when they left.
 */
class TeamService {
  private readonly baseUrl = '/hr/teams';

  getAll(includeInactive = false): Promise<Team[]> {
    return apiService.get<Team[]>(this.baseUrl, { includeInactive });
  }

  getSummary(): Promise<TeamSummary[]> {
    return apiService.get<TeamSummary[]>(`${this.baseUrl}/summary`);
  }

  getPaged(pageNumber = 1, pageSize = 20, search?: string): Promise<PagedResult<Team>> {
    return apiService.get<PagedResult<Team>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
      ...(search ? { search } : {}),
    });
  }

  getById(id: string): Promise<Team> {
    return apiService.get<Team>(`${this.baseUrl}/${id}`);
  }

  /** The team plus its full roster — current members and former ones alike. */
  getDetail(id: string): Promise<TeamDetail> {
    return apiService.get<TeamDetail>(`${this.baseUrl}/${id}/detail`);
  }

  create(data: CreateTeamRequest): Promise<Team> {
    return apiService.post<Team>(this.baseUrl, data);
  }

  update(id: string, data: UpdateTeamRequest): Promise<Team> {
    return apiService.put<Team>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<{ deleted: boolean }> {
    return apiService.delete<{ deleted: boolean }>(`${this.baseUrl}/${id}`);
  }

  // ── membership ──────────────────────────────────────────────────────────────

  getMembers(teamId: string, currentOnly = false): Promise<TeamMember[]> {
    return apiService.get<TeamMember[]>(`${this.baseUrl}/${teamId}/members`, { currentOnly });
  }

  addMember(teamId: string, data: AddTeamMemberRequest): Promise<TeamMember> {
    return apiService.post<TeamMember>(`${this.baseUrl}/${teamId}/members`, data);
  }

  updateMember(teamId: string, memberId: string, data: UpdateTeamMemberRequest): Promise<TeamMember> {
    return apiService.put<TeamMember>(`${this.baseUrl}/${teamId}/members/${memberId}`, data);
  }

  /** Ends the membership. The row survives, carrying its leaving date. */
  removeMember(
    teamId: string,
    memberId: string,
    data: RemoveTeamMemberRequest = {},
  ): Promise<{ removed: boolean }> {
    return apiService.delete<{ removed: boolean }>(
      `${this.baseUrl}/${teamId}/members/${memberId}`,
      data,
    );
  }

  getMemberHistory(teamId: string): Promise<TeamMemberHistoryEntry[]> {
    return apiService.get<TeamMemberHistoryEntry[]>(`${this.baseUrl}/${teamId}/member-history`);
  }

  /** Every team an employee belongs to. Routed off the employee, not the team. */
  getForEmployee(employeeId: string, currentOnly = true): Promise<TeamMember[]> {
    return apiService.get<TeamMember[]>(`/hr/employees/${employeeId}/teams`, { currentOnly });
  }
}

export const teamService = new TeamService();

/** Strips the read-only keys the update command does not model. */
export function toUpdateRequest(team: Team): UpdateTeamRequest {
  return {
    id: team.id,
    name: team.name,
    code: team.code,
    description: team.description ?? null,
    teamType: team.teamType,
    status: team.status,
    organizationUnitId: team.organizationUnitId ?? null,
    teamLeadId: team.teamLeadId ?? null,
    parentTeamId: team.parentTeamId ?? null,
    locationId: team.locationId ?? null,
    shiftId: team.shiftId ?? null,
    costCenterCode: team.costCenterCode ?? null,
    projectCode: team.projectCode ?? null,
    teamEmail: team.teamEmail ?? null,
    effectiveFrom: team.effectiveFrom,
    effectiveTo: team.effectiveTo ?? null,
    maxMembers: team.maxMembers ?? null,
    sequence: team.sequence,
    isActive: team.isActive,
    notes: team.notes ?? null,
  };
}
