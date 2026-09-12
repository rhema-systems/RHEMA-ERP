/**
 * The two linkages that tie the performance area together.
 *
 * **Goal → required competencies.** Which skills a goal actually needs, and which of those the
 * employee still has to develop. The `developmentNeeded` ones become suggestions on the
 * development plan, which is the whole point: the plan is built from what this year's goals
 * demand rather than from a blank page.
 *
 * **Check-in → yearly objectives.** Which company objectives a one-to-one was actually about, so
 * a year of conversations can be read back against the strategy they were meant to serve.
 *
 * ⚠ **Both writes are replace-set** — the body is the complete list, and an empty array clears
 * everything. Send the full set you want to end up with, never just the additions. See
 * [[replace-set-payload-convention]] in the project notes; this is the same trap that wiped
 * position entitlements on every save.
 *
 * Route: `api/PerformanceLinks`.
 */
import type { AuditFields } from './common';

export interface GoalRequiredSkill extends AuditFields {
  employeeGoalId: string;
  competencyId: string;
  competencyName?: string | null;
  /** Flags this competency as a gap — it is what feeds the development-plan suggestions. */
  developmentNeeded: boolean;
  note?: string | null;
}

export interface SetGoalRequiredSkill {
  competencyId: string;
  developmentNeeded: boolean;
  note?: string | null;
}

export interface DevelopmentSkillSuggestion {
  competencyId: string;
  competencyName?: string | null;
  /** The goals that asked for this competency — the reason the suggestion exists. */
  fromGoals: string[];
}

export interface CheckInObjectiveLink extends AuditFields {
  checkInId: string;
  companyGoalId: string;
  objectiveTitle?: string | null;
}
