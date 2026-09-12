/**
 * Appraisal conversations — the scheduled meetings that punctuate a cycle: kick-off, the
 * quarterly and mid-year reviews, the final review, a PIP discussion, or an ad-hoc meeting.
 *
 * **Not check-ins.** A check-in (`types/hr/appraisal-run.ts`) is an ad-hoc one-to-one that belongs
 * to a cycle and can carry goal updates. A conversation hangs off a specific appraisal and is the
 * formal meeting for a phase of it, with an agenda before and notes and takeaways after.
 *
 * **Completing is its own action.** `complete` stamps the held date and notifies the employee;
 * an ordinary update cannot close a conversation, and a completed one can no longer be edited.
 *
 * Route: `api/AppraisalConversations`.
 */
import type { AuditFields } from './common';

export type ConversationType =
  | 'KickOff'
  | 'QuarterlyQ1'
  | 'MidYear'
  | 'QuarterlyQ3'
  | 'QuarterlyQ4'
  | 'FinalReview'
  | 'PIPDiscussion'
  | 'AdHocMeeting';

export const CONVERSATION_TYPE_OPTIONS: { value: ConversationType; label: string }[] = [
  { value: 'KickOff', label: 'Kick-off' },
  { value: 'QuarterlyQ1', label: 'Quarterly — Q1' },
  { value: 'MidYear', label: 'Mid-year' },
  { value: 'QuarterlyQ3', label: 'Quarterly — Q3' },
  { value: 'QuarterlyQ4', label: 'Quarterly — Q4' },
  { value: 'FinalReview', label: 'Final review' },
  { value: 'PIPDiscussion', label: 'PIP discussion' },
  { value: 'AdHocMeeting', label: 'Ad-hoc meeting' },
];

export interface AppraisalConversation extends AuditFields {
  tenantId: string;
  appraisalId: string;
  appraisalNumber?: string | null;
  scheduledById?: string | null;
  scheduledByName?: string | null;
  conductedById?: string | null;
  conductedByName?: string | null;
  type: ConversationType;
  scheduledDate?: string | null;
  heldDate?: string | null;
  agenda?: string | null;
  postMeetingNotes?: string | null;
  keyTakeaways?: string | null;
  isCompleted: boolean;
  reviewEventId?: string | null;
}

export interface CreateAppraisalConversation {
  appraisalId: string;
  reviewEventId?: string | null;
  /** Left null to let the server stamp the signed-in employee, which is the normal case. */
  scheduledById?: string | null;
  conductedById?: string | null;
  type: ConversationType;
  scheduledDate?: string | null;
  agenda?: string | null;
}

export interface UpdateAppraisalConversation {
  id: string;
  appraisalId: string;
  scheduledById?: string | null;
  conductedById?: string | null;
  type: ConversationType;
  scheduledDate?: string | null;
  /**
   * Ignored by the server on this path: held date and completion are stamped by `complete`, so
   * an edit cannot quietly close a meeting that was never held.
   */
  heldDate?: string | null;
  agenda?: string | null;
  postMeetingNotes?: string | null;
  keyTakeaways?: string | null;
  isCompleted: boolean;
  reviewEventId?: string | null;
}

export interface CompleteConversation {
  postMeetingNotes?: string | null;
  keyTakeaways?: string | null;
}
