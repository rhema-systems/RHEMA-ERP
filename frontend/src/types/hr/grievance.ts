/**
 * @deprecated Superseded by `types/hr/employee-relations.ts` in area 9c slice 10.
 *
 * This file is now a re-export. It exists so the four `/me/grievances` portal screens keep
 * compiling unchanged while slice 11 rewrites them; slice 12 deletes it once nothing imports it.
 *
 * ⚠ **The types it used to declare were a subset, and the gap was not cosmetic.** They described
 * area 9 slice 7's grievance — a case with a statement and a ladder. Since then the same endpoint
 * has grown parties, an HR interpretation, an investigation, a resolution decision, documents,
 * conferences and cross-links, none of which these types knew about. Anything still importing from
 * here was reading a case file through a nine-slice-old window, and the missing fields were
 * invisible rather than broken: TypeScript cannot warn about a field you never declared.
 *
 * Import from `@/types/hr/employee-relations` instead.
 */
export type {
  GrievanceEscalationLevel,
  GrievanceStatus,
  GrievanceStepOutcome,
  GrievanceStep,
  GrievanceSummary,
  Grievance,
  FileGrievanceRequest,
  RespondToGrievanceRequest,
  AssignGrievanceStepRequest,
  EscalateGrievanceRequest,
  WithdrawGrievanceRequest,
} from './employee-relations';

export {
  GRIEVANCE_LADDER,
  GRIEVANCE_STATUS_OPTIONS,
  SETTLED_GRIEVANCE_STATUSES,
} from './employee-relations';
