'use client';

import { Badge, type BadgeProps } from '@/components/ui/badge';

type BadgeVariant = BadgeProps['variant'];

// Maps common HR status strings to Badge variants. Extend as more areas are built.
// Keys are the status text with spaces, underscores and hyphens stripped, lower-cased.
const STATUS_VARIANTS: Record<string, BadgeVariant> = {
  active: 'default',
  inactive: 'secondary',
  pending: 'secondary',
  submitted: 'secondary',
  approved: 'default',
  rejected: 'destructive',
  cancelled: 'destructive',
  terminated: 'destructive',
  onhold: 'destructive',
  draft: 'outline',

  // Attendance & Time. "Absent" and "Late" are the exceptions worth spotting in a list,
  // so they carry the destructive/secondary weight rather than reading as neutral.
  present: 'default',
  absent: 'destructive',
  late: 'secondary',
  halfday: 'secondary',
  onleave: 'secondary',
  publicholiday: 'outline',
  weekend: 'outline',
  offday: 'outline',
  remotework: 'secondary',
  onduty: 'default',
  applied: 'default',
  completed: 'default',
  open: 'default',
  pendingclose: 'secondary',
  closed: 'secondary',
  exportedtopayroll: 'default',
  processing: 'secondary',
  inprogress: 'secondary',
  failed: 'destructive',
  partialsuccess: 'secondary',
  acknowledged: 'secondary',
  resolved: 'default',
  dismissed: 'outline',
  critical: 'destructive',
  warning: 'secondary',
  info: 'outline',

  // Consultant timesheets and invoicing.
  senttoclient: 'secondary',
  clientconfirmed: 'default',
  clientrejected: 'destructive',
  billed: 'default',
  void: 'outline',
  voided: 'outline',
  sent: 'secondary',
  viewed: 'secondary',
  confirmed: 'default',
  partiallypaid: 'secondary',
  paid: 'default',
  overdue: 'destructive',
  expired: 'destructive',
  resent: 'secondary',
  suspended: 'secondary',

  // Goal cascade. GoalStatus mixes the approval lifecycle with execution states, so both are
  // here: "at risk" is the one worth spotting in a list, and "locked" reads as settled rather
  // than as a problem.
  pendingapproval: 'secondary',
  locked: 'default',
  atrisk: 'destructive',
  ontrack: 'default',
  notstarted: 'outline',

  // TeamGovernanceStatus — a verdict on a whole goal set rather than one goal.
  awaitingapproval: 'secondary',
  invalidweight: 'destructive',
  structurallycomplete: 'default',

  // Appraisal cycles and templates. `humanizeEnum` lower-cases everything but the first
  // letter, so "InProgress" arrives as "In progress" and normalises to `inprogress`, which
  // is already mapped above. The rest are new here.
  covered: 'default',
  notemplate: 'destructive',
  conflict: 'destructive',
  excluded: 'outline',
  // Coverage statuses arrive humanised too — "No template" strips to `notemplate`.

  // Appeals. "Remanded" is a process state, not a verdict — it means the manager owes a
  // re-evaluation — so it reads as an outstanding action rather than as a decision.
  underreview: 'secondary',
  remanded: 'destructive',
  upheld: 'default',

  // Appraisal outcome recommendations. "Approved" here is NOT a finished state: it means the
  // dispatch to the owning module failed and the item still needs a retry, so it carries the
  // same weight as anything else awaiting work. "Actioned" is the finished one.
  proposed: 'secondary',
  actioned: 'default',

  // Calibration sessions. Committing is a separate step from completing, so a Completed session
  // may still be waiting on someone.
  // (pending / inprogress / completed / cancelled are all mapped above.)

  // Improvement plans. "Unsuccessful" is a verdict on the plan, not on the person, but it is the
  // one that has consequences attached, so it carries destructive weight. Draft and
  // PendingApproval mean nothing has been served on the employee yet.
  unsuccessful: 'destructive',
  performanceimproved: 'default',
  extended: 'secondary',
  demotion: 'destructive',
  termination: 'destructive',
  transferred: 'secondary',

  // Development plans and their objectives, and PIP review meeting attendance.
  attended: 'default',
  generalcomment: 'outline',
  progressupdate: 'secondary',
  riskflag: 'destructive',
  reviewnote: 'outline',

  // Appraisal conversations. "Held" carries its date, so only the two process states are here.
  scheduled: 'secondary',
  held: 'default',

  // Cycle-dashboard deadline states. "Passed" is not "Overdue": it means the phase is behind
  // the cycle and no longer anyone's problem, so it reads as settled.
  safe: 'default',
  approaching: 'secondary',
  imminent: 'destructive',
  today: 'destructive',
  passed: 'outline',
  none: 'outline',

  // Interim reviews. "EmployeeSubmitted" is the one that needs someone to act — it is the
  // manager's queue — so it carries weight rather than reading as settled.
  employeesubmitted: 'secondary',

  // Training service bonds.
  pendingacceptance: 'secondary',
  fulfilled: 'default',
  breached: 'destructive',
  settled: 'default',
  waived: 'outline',

  // Recruitment. The live, work-in-hand states carry weight; the ones that mean "nothing is
  // happening here any more" (Filled, Expired, Withdrawn) read as neutral rather than as a
  // problem — a filled vacancy is a success, not an alert.
  partiallyfulfilled: 'secondary',
  published: 'default',
  closedforapplications: 'secondary',
  shortlisting: 'secondary',
  interviewing: 'secondary',
  offerstage: 'secondary',
  filled: 'default',
  withdrawn: 'outline',

  // Position vacancies (establishment gaps), before a requisition exists.
  anticipated: 'outline',
  requisitionraised: 'default',

  // Applications. ApplicationStatus arrives humanised on the summary DTOs ("Under Review",
  // "Pre-Employment Check"), so the keys here are the stripped forms. "Shortlisted" and "Hired" are
  // the outcomes worth spotting; the mid-pipeline states read as work in hand. "Waitlisted" is not a
  // rejection — it is a hold — so it stays neutral rather than destructive.
  new: 'secondary',
  shortlisted: 'default',
  interviewscheduled: 'secondary',
  interviewcompleted: 'secondary',
  assessmentpending: 'secondary',
  preemploymentcheck: 'secondary',
  offerextended: 'secondary',
  offeraccepted: 'default',
  offerdeclined: 'destructive',
  hired: 'default',
  waitlisted: 'outline',

  // Pipeline stage types, shown on board columns and stage-history rows.
  applicationreview: 'outline',
  screening: 'outline',
  hiringmanagerreview: 'outline',
  assessment: 'outline',
  interview: 'outline',
  offer: 'outline',
  inbox: 'outline',

  // Shortlist decisions and their audit log.
  unshortlisted: 'secondary',
  autoshortlisted: 'default',
  notsubmitted: 'outline',
  progressed: 'default',
  merged: 'outline',

  // Talent pool.
  passive: 'secondary',
  dormant: 'outline',
  converted: 'default',

  // Interviews. "Rescheduled" is not a problem state — it is a session that moved and whose
  // candidates have been re-invited — so it reads as work in hand rather than a warning. Only a
  // cancellation and a no-show are destructive.
  rescheduled: 'secondary',
  noshow: 'destructive',

  // Interview panel roles, shown against each member of the panel.
  chair: 'default',
  member: 'outline',
  technicalassessor: 'secondary',
  observer: 'outline',

  // A panel's verdict on a candidate at one session (JobInterviewOutcome).
  highlyrecommended: 'default',
  recommended: 'default',
  acceptable: 'secondary',
  notrecommended: 'destructive',
  proceedtonextround: 'default',
  // ⚠ "onhold" is already mapped above (destructive) for the case where holding something is the
  // problem. An interview outcome of "On Hold" is a neutral parking decision rather than a
  // rejection, but one key cannot carry two meanings — the earlier mapping wins, and this is left
  // here as a note rather than a duplicate.

  // A single panelist's scorecard recommendation (JobInterviewRecommendation). Deliberately shares
  // no key with the outcome above — they are different scales answering different questions.
  stronghire: 'default',
  hire: 'default',
  neutral: 'secondary',
  nohire: 'destructive',
  strongnohire: 'destructive',

  // Interview formats.
  oneonone: 'outline',
  panel: 'outline',
  technical: 'outline',
  competencybased: 'outline',
  casestudy: 'outline',
  presentation: 'outline',
  inperson: 'outline',
  phone: 'outline',
  video: 'outline',
  hybrid: 'outline',
};

interface StatusBadgeProps {
  /** A status string (case-insensitive) mapped to a Badge variant. */
  status?: string;
  /** Convenience for boolean active/inactive flags; ignored when `status` is set. */
  active?: boolean;
  className?: string;
}

/**
 * Renders an HR status as a colored Badge. Pass either a `status` string or an
 * `active` boolean. Unknown statuses fall back to the `outline` variant.
 */
export function StatusBadge({ status, active, className }: StatusBadgeProps) {
  const label = status ?? (active ? 'Active' : 'Inactive');
  const key = label.toLowerCase().replace(/[\s_-]/g, '');
  const variant = STATUS_VARIANTS[key] ?? 'outline';
  return (
    <Badge variant={variant} className={className}>
      {label}
    </Badge>
  );
}
