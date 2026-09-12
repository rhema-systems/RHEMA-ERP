// Types for HR Area 10 (SHE) slice 13: the SHE reminder engine.
// Mirrors ErpSystem.Core.DTOs.HR.SafetyReminderDTOs. Backend route: api/safety/reminders.
//
// The engine is an hourly background sweep (per tenant) that:
//   • auto-expires permits past their planned end and risk assessments past ValidUntil;
//   • fires due-soon reminder ladders (the statutory 180/90/60/30/14/7 sequence on
//     regulatory reviews; narrower ladders elsewhere) and escalates overdue items by
//     tier (1 ≤7d, 2 ≤30d, 3 beyond — tiers 2–3 also notify SuperAdmin);
//   • delivers through the notification-topic pipeline (admins can retarget/mute each
//     "SafetyCompliance.*" topic under notification settings);
//   • dedupes per item + due date + rung, so re-running is always safe.
// The run-now endpoint shares the exact sweep implementation with the hourly job.

export interface SheReminderRun {
  id: string;
  startedAt: string;
  completedAt?: string | null;
  trigger: 'Scheduled' | 'Manual' | string;
  remindersQueued: number;
  permitsExpired: number;
  riskAssessmentsExpired: number;
  /** Environmental permits the sweep flipped to Expired (slice 17, FR-ENV-019). */
  environmentalPermitsExpired: number;
}

export interface SheReminderRunResult extends SheReminderRun {
  /** Reminders queued this run, by kind (e.g. "PermitExpiringSoon" → 2). */
  queuedByKind: Record<string, number>;
}

export interface SheReminderLogEntry {
  id: string;
  runId: string;
  kind: string;
  itemType: string;
  entityId: string;
  reference: string;
  dueDate?: string | null;
  /** Days remaining at dispatch time; negative = overdue. */
  daysRemaining: number;
  /** 0 = due-soon rung; 1–3 = overdue escalation tier. */
  escalationTier: number;
  dispatchedAt: string;
}

/** Human labels for the engine's reminder kinds (log/run breakdown display). */
export const SHE_REMINDER_KIND_LABELS: Record<string, string> = {
  PermitExpiringSoon: 'Permit expiring soon',
  PermitExpired: 'Permit auto-expired',
  RiskAssessmentExpired: 'Risk assessment auto-expired',
  RiskAssessmentReviewDue: 'Risk assessment review due',
  InspectionDue: 'Inspection due',
  InspectionFollowUpDue: 'Follow-up inspection due',
  EquipmentInspectionDue: 'Equipment inspection due',
  EquipmentMaintenanceDue: 'Equipment maintenance due',
  EquipmentCertificationExpiring: 'Equipment certification expiring',
  SignInspectionDue: 'Sign inspection due',
  TrainingCertificateExpiring: 'Training certificate expiring',
  ContractorDocumentExpiring: 'Contractor document expiring',
  ContractorInductionExpiring: 'Contractor induction expiring',
  ContractorPreQualificationExpiring: 'Contractor pre-qualification expiring',
  RegulatoryReviewDue: 'Regulatory review due',
  EmergencyPlanReviewDue: 'Emergency plan review due',
  ResponseTeamCertificateExpiring: 'Response-team certificate expiring',
  DrillDue: 'Emergency drill due',
  HealthSurveillanceDue: 'Health surveillance recall due',
  FirstAidInspectionDue: 'First-aid station inspection due',
  PpeIssuanceExpiring: 'Issued PPE expiring',
  PpeStockLow: 'PPE stock at reorder level',
  // Slices 14–16 (labels backfilled in slice 17).
  CorrectiveActionDue: 'Corrective action due',
  AuditDue: 'SHE audit due',
  StopWorkOpen: 'Stop-work order open',
  StatutorySubmissionPending: 'Statutory submission pending',
  DocumentReviewDue: 'Controlled document review due',
  // Slice 17 — Part D environmental core.
  EnvironmentalPermitRenewal: 'Environmental permit renewal due',
  EnvironmentalPermitExpired: 'Environmental permit expired',
  MonitoringDue: 'Environmental monitoring due',
  RegulatoryUpdateDeadline: 'Regulatory update deadline',
  ProjectReviewDue: 'Project environmental review due',
  MonthlyEnvironmentalReport: 'Monthly environmental report generated',
};

export const sheReminderKindLabel = (kind: string): string =>
  SHE_REMINDER_KIND_LABELS[kind] ?? kind;
