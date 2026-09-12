import { apiService } from '../api.service';
import type {
  CheckInObjectiveLink,
  DevelopmentSkillSuggestion,
  GoalRequiredSkill,
  SetGoalRequiredSkill,
} from '@/types/hr/performance-links';

/**
 * api/PerformanceLinks — goal → required competencies, and check-in → yearly objectives.
 *
 * Entitlement follows the parent: a goal's skills are HR / the owner / their manager; a check-in's
 * objectives are HR / the employee / the conductor. Nothing here takes an actor id.
 *
 * ⚠ Both setters are **replace-set**: the argument is the whole list, and `[]` clears it.
 */
class PerformanceLinkService {
  private readonly baseUrl = '/PerformanceLinks';

  getGoalRequiredSkills(goalId: string): Promise<GoalRequiredSkill[]> {
    return apiService.get<GoalRequiredSkill[]>(`${this.baseUrl}/goals/${goalId}/required-skills`);
  }

  /** Replace-set — pass every skill the goal should end up with. */
  setGoalRequiredSkills(goalId: string, skills: SetGoalRequiredSkill[]): Promise<GoalRequiredSkill[]> {
    return apiService.put<GoalRequiredSkill[]>(
      `${this.baseUrl}/goals/${goalId}/required-skills`,
      skills,
    );
  }

  /** Competencies this employee's goals say they need to develop, for a cycle. */
  getSkillSuggestions(employeeId: string, cycleId: string): Promise<DevelopmentSkillSuggestion[]> {
    return apiService.get<DevelopmentSkillSuggestion[]>(
      `${this.baseUrl}/employees/${employeeId}/cycles/${cycleId}/skill-suggestions`,
    );
  }

  /** The signed-in employee's own suggestions — no employee id needed. */
  getMySkillSuggestions(cycleId: string): Promise<DevelopmentSkillSuggestion[]> {
    return apiService.get<DevelopmentSkillSuggestion[]>(
      `${this.baseUrl}/employees/me/cycles/${cycleId}/skill-suggestions`,
    );
  }

  getCheckInObjectives(checkInId: string): Promise<CheckInObjectiveLink[]> {
    return apiService.get<CheckInObjectiveLink[]>(
      `${this.baseUrl}/check-ins/${checkInId}/objectives`,
    );
  }

  /** Replace-set — pass every company-goal id the check-in should end up linked to. */
  setCheckInObjectives(checkInId: string, companyGoalIds: string[]): Promise<CheckInObjectiveLink[]> {
    return apiService.put<CheckInObjectiveLink[]>(
      `${this.baseUrl}/check-ins/${checkInId}/objectives`,
      companyGoalIds,
    );
  }
}

export const performanceLinkService = new PerformanceLinkService();
