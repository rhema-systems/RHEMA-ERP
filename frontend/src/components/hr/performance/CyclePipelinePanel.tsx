'use client';

import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';
import type { CyclePipelineProgress, HRCycleDashboard } from '@/types/hr/analytics';

/**
 * How far the cycle's appraisals have got, stage by stage.
 *
 * Deliberately not a funnel chart. The counts do not nest the way a funnel implies — an
 * appraisal sitting at HR review is *past* self-evaluation, so it is counted as complete there
 * and as pending here, and the two figures for a stage never sum to the cycle total. A row per
 * stage with its own bar says exactly that; a funnel would imply a flow that is losing people.
 *
 * Stages the cycle's settings switch off are dropped rather than shown at zero — a cycle with no
 * peer reviews has no peer stage, and a row reading "0 of 40" would read as everyone being late.
 */
interface StageRow {
  name: string;
  pending: number;
  completed: number;
  href?: string;
}

function buildStages(p: CyclePipelineProgress, d: HRCycleDashboard): StageRow[] {
  const stages: StageRow[] = [
    { name: 'Goal setting', pending: p.goalSettingCount, completed: p.totalAppraisals - p.goalSettingCount },
    { name: 'Self-evaluation', pending: p.selfEvalPendingCount, completed: p.selfEvalCompletedCount },
  ];

  if (d.hasPeerReviews) {
    stages.splice(1, 0, {
      name: 'Peer nomination',
      pending: p.peerNominationCount,
      completed: p.totalAppraisals - p.peerNominationCount,
    });
    stages.push({
      name: 'Peer evaluation',
      pending: p.peerEvalPendingCount,
      completed: p.peerEvalCompletedCount,
    });
  }

  stages.push({
    name: 'Manager evaluation',
    pending: p.managerEvalPendingCount,
    completed: p.managerEvalCompletedCount,
  });

  if (d.hasCalibration) {
    stages.push({
      name: 'Calibration',
      pending: p.calibrationPendingCount,
      completed: p.calibrationCompletedCount,
    });
  }

  if (d.hasHRReview) {
    stages.push({
      name: 'HR review',
      pending: p.hrReviewPendingCount,
      completed: p.hrReviewCompletedCount,
    });
  }

  stages.push({
    name: 'Acknowledgment',
    pending: p.acknowledgmentPendingCount,
    completed: p.acknowledgmentCompletedCount,
  });

  return stages;
}

export function CyclePipelinePanel({ dashboard }: { dashboard: HRCycleDashboard }) {
  const p = dashboard.pipelineProgress;
  const total = p.totalAppraisals;

  if (total === 0) {
    return (
      <p className="py-6 text-center text-sm text-muted-foreground">
        No appraisals have been generated for this cycle yet.
      </p>
    );
  }

  const stages = buildStages(p, dashboard);

  return (
    <div className="space-y-4">
      {stages.map((stage) => {
        const percent = Math.round((stage.completed / total) * 100);
        return (
          <div key={stage.name} className="space-y-1.5">
            <div className="flex items-baseline justify-between gap-4 text-sm">
              <span className="font-medium">{stage.name}</span>
              <span className="tabular-nums text-muted-foreground">
                {stage.completed} of {total} past it
                {stage.pending > 0 && (
                  <span className={cn('ml-2', stage.pending > 0 && 'text-amber-600 dark:text-amber-500')}>
                    · {stage.pending} waiting here
                  </span>
                )}
              </span>
            </div>
            <Progress value={percent} className="h-2" />
          </div>
        );
      })}

      {p.appealCount > 0 && (
        <p className="pt-2 text-sm text-muted-foreground">
          <span className="font-medium text-foreground">{p.appealCount}</span> appraisal
          {p.appealCount === 1 ? ' is' : 's are'} out of the pipeline under appeal.
        </p>
      )}
    </div>
  );
}
