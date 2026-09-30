'use client';

import { Ban, Check, Circle, Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  APPRAISAL_PHASE_LABELS,
  APPRAISAL_PHASE_ORDER,
  type AppraisalPhase,
} from '@/types/hr/appraisal-run';
import type { AppraisalSettings } from '@/types/hr/appraisal';
import type { AppraisalSubStatus } from '@/types/hr/outcomes';

/**
 * Where an appraisal is, as a rail.
 *
 * The phase comes from `api/AppraisalWorkflow/{id}/phase` and is computed from live state each
 * time — it is not stored, so it is always the truth about what has actually been submitted.
 *
 * Steps a cycle does not require are dropped rather than shown greyed: a cycle with no peer
 * reviews never reports `PeerEvaluation`, so leaving it on the rail would imply the appraisal
 * is stuck at a step it will never reach. Pass `settings` to prune — and to put HR review ahead
 * of calibration when the cycle runs it first; without them every step is shown in the default
 * order, which is the honest fallback when the settings have not loaded.
 */
export function AppraisalPhaseRail({
  phase,
  subStatus,
  settings,
  className,
}: {
  phase?: AppraisalPhase | null;
  /**
   * The step within the phase. A withdrawn appraisal (performance closure E-d1) reports the
   * `Closed` phase, which would draw every step before it as done; it is shown as withdrawn instead.
   */
  subStatus?: AppraisalSubStatus | null;
  settings?: AppraisalSettings | null;
  className?: string;
}) {
  if (subStatus === 'Withdrawn') {
    return (
      <p className={cn('flex items-center gap-1.5 text-sm text-muted-foreground', className)}>
        <Ban className="h-4 w-4" />
        Withdrawn from the cycle — it went no further, and takes no more steps.
      </p>
    );
  }

  const steps = orderFor(settings).filter((p) => appliesTo(p, settings));
  const currentIndex = phase ? steps.indexOf(phase) : -1;

  return (
    <ol className={cn('flex flex-wrap items-center gap-x-1 gap-y-2', className)}>
      {steps.map((step, index) => {
        const state =
          currentIndex < 0
            ? 'pending'
            : index < currentIndex
              ? 'done'
              : index === currentIndex
                ? 'current'
                : 'pending';

        return (
          <li key={step} className="flex items-center gap-1">
            <span
              className={cn(
                'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs',
                state === 'done' && 'border-emerald-600/30 bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400',
                state === 'current' && 'border-primary bg-primary/10 font-medium text-foreground',
                state === 'pending' && 'text-muted-foreground',
              )}
            >
              {state === 'done' ? (
                <Check className="h-3 w-3" />
              ) : state === 'current' ? (
                <Loader2 className="h-3 w-3" />
              ) : (
                <Circle className="h-3 w-3" />
              )}
              {APPRAISAL_PHASE_LABELS[step]}
            </span>
            {index < steps.length - 1 && (
              <span aria-hidden className="text-muted-foreground/40">
                ›
              </span>
            )}
          </li>
        );
      })}
    </ol>
  );
}

/** The display order, with HR review ahead of calibration when the cycle runs it first. */
function orderFor(settings?: AppraisalSettings | null): AppraisalPhase[] {
  if (settings?.hrReviewTiming !== 'BeforeCalibration') return APPRAISAL_PHASE_ORDER;
  return APPRAISAL_PHASE_ORDER.map((p) =>
    p === 'Calibration' ? 'HRReview' : p === 'HRReview' ? 'Calibration' : p,
  );
}

/**
 * Mirrors the server's pipeline (`AppraisalGates.Pipeline`). Kept in step with it: a phase the
 * server can never report should not be on the rail.
 */
function appliesTo(phase: AppraisalPhase, settings?: AppraisalSettings | null): boolean {
  if (!settings) return true;

  switch (phase) {
    case 'GoalSetting':
      // The kick-off conversation is held at the goal-setting step. The mid-year holds the
      // manager's submission instead (closure B2), so it does not make a goal-setting step.
      return settings.requireGoalSetting || settings.requireKickOffConversation;
    case 'PeerNomination':
    case 'PeerEvaluation':
      return settings.requirePeerReviews && settings.minPeerEvaluators > 0;
    case 'SelfEvaluation':
      return settings.requireSelfEvaluation;
    case 'ManagerEvaluation':
      return settings.requireManagerEvaluation;
    case 'Calibration':
      return settings.requireCalibration;
    case 'HRReview':
      return settings.requireHRReview;
    case 'EmployeeReview':
      // The final conversation and the acknowledgment. The conversation is a step unless the
      // acknowledgment may go ahead without it.
      return (
        settings.requireEmployeeAcknowledgment ||
        (settings.requireFinalConversation &&
          !(settings.requireEmployeeAcknowledgment && settings.allowAcknowledgmentWithoutConversation))
      );
    case 'Closed':
      return true;
    default:
      return true;
  }
}
