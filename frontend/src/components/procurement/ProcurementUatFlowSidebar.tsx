import React from 'react';
import Link from 'next/link';
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  ChevronDown,
  Circle,
  CircleAlert,
  CircleHelp,
  Clock3,
  ExternalLink,
  MinusCircle,
  PlayCircle,
  XCircle,
  type LucideIcon,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import {
  buildProcurementUatFlow,
  getProcurementUatStageContext,
  procurementUatProgress,
  type ProcurementUatFlowStage,
  type ProcurementUatStageId,
  type ProcurementUatStageState,
  type ProcurementUatStageStates,
  type ProcurementUatStageStatus,
} from '@/lib/procurement-uat-flow';

export interface ProcurementUatFlowSidebarProps {
  currentStage: ProcurementUatStageId;
  /** Supply statuses from the page's real record/readiness data. Missing stages remain Unknown. */
  stageStates: ProcurementUatStageStates;
  recordReference?: string;
  title?: string;
  className?: string;
}

const statusPresentation: Record<
  ProcurementUatStageStatus,
  {
    label: string;
    icon: LucideIcon;
    badgeClassName: string;
    iconClassName: string;
  }
> = {
  unknown: {
    label: 'Unknown',
    icon: CircleHelp,
    badgeClassName: 'border-slate-200 bg-slate-50 text-slate-700',
    iconClassName: 'text-slate-500',
  },
  'not-started': {
    label: 'Not started',
    icon: Circle,
    badgeClassName: 'border-slate-200 bg-slate-50 text-slate-700',
    iconClassName: 'text-slate-400',
  },
  ready: {
    label: 'Ready',
    icon: PlayCircle,
    badgeClassName: 'border-cyan-200 bg-cyan-50 text-cyan-800',
    iconClassName: 'text-cyan-600',
  },
  'in-progress': {
    label: 'In progress',
    icon: Clock3,
    badgeClassName: 'border-blue-200 bg-blue-50 text-blue-800',
    iconClassName: 'text-blue-600',
  },
  blocked: {
    label: 'Blocked',
    icon: CircleAlert,
    badgeClassName: 'border-amber-200 bg-amber-50 text-amber-900',
    iconClassName: 'text-amber-600',
  },
  complete: {
    label: 'Complete',
    icon: CheckCircle2,
    badgeClassName: 'border-emerald-200 bg-emerald-50 text-emerald-800',
    iconClassName: 'text-emerald-600',
  },
  skipped: {
    label: 'Not applicable',
    icon: MinusCircle,
    badgeClassName: 'border-slate-200 bg-slate-50 text-slate-700',
    iconClassName: 'text-slate-500',
  },
  failed: {
    label: 'Failed',
    icon: XCircle,
    badgeClassName: 'border-red-200 bg-red-50 text-red-800',
    iconClassName: 'text-red-600',
  },
};

export function ProcurementUatFlowSidebar({
  currentStage,
  stageStates,
  recordReference,
  title = 'Procurement process',
  className,
}: ProcurementUatFlowSidebarProps) {
  const titleId = React.useId();
  const flow = buildProcurementUatFlow(currentStage, stageStates);
  const context = getProcurementUatStageContext(flow, currentStage);
  const progress = procurementUatProgress(flow);
  const currentBlockers = (context.current.state.blockers ?? []).slice(0, 1);
  const primaryActionStage =
    context.current.state.status === 'complete' ||
    context.current.state.status === 'skipped'
      ? context.next
      : context.current;

  return (
    <aside
      className={cn('w-full xl:sticky xl:top-4', className)}
      aria-labelledby={titleId}
      data-testid="procurement-uat-flow-sidebar"
    >
      <Card className="overflow-hidden hover:shadow-sm">
        <CardHeader className="space-y-2 p-4 pb-3">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <CardTitle id={titleId} className="text-base">
                {title}
              </CardTitle>
              {recordReference && (
                <p className="mt-1 truncate text-xs text-muted-foreground">
                  {recordReference}
                </p>
              )}
            </div>
            <Badge variant="outline" className="shrink-0 font-normal">
              {progress.completed}/{progress.total} complete
            </Badge>
          </div>
        </CardHeader>

        <CardContent className="space-y-3 p-4 pt-0">
          <div className="space-y-1.5" aria-label="Immediate process context">
            {context.previous ? (
              <CompactStageRow label="Prerequisite" stage={context.previous} />
            ) : (
              <div className="rounded-md border border-dashed px-3 py-2 text-xs text-muted-foreground">
                This is the first procurement stage.
              </div>
            )}
            <div
              className="flex justify-center text-muted-foreground"
              aria-hidden="true"
            >
              <ArrowRight className="h-3.5 w-3.5 rotate-90" />
            </div>
            <CompactStageRow label="Current" stage={context.current} current />
            {context.next && (
              <>
                <div
                  className="flex justify-center text-muted-foreground"
                  aria-hidden="true"
                >
                  <ArrowRight className="h-3.5 w-3.5 rotate-90" />
                </div>
                <CompactStageRow label="Next" stage={context.next} />
              </>
            )}
          </div>

          {currentBlockers.length > 0 && (
            <div
              className="rounded-md border border-amber-200 bg-amber-50 p-3 text-amber-950"
              role="status"
              aria-label="Current stage blockers"
            >
              <div className="flex items-start gap-2">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
                <div className="min-w-0">
                  <p className="text-xs font-semibold">
                    Resolve before continuing
                  </p>
                  <ul className="mt-1 space-y-1 text-xs">
                    {currentBlockers.map((blocker) => (
                      <li key={blocker}>{blocker}</li>
                    ))}
                  </ul>
                </div>
              </div>
            </div>
          )}

          {primaryActionStage?.state.href && (
            <Link
              href={primaryActionStage.state.href}
              aria-label={`${primaryActionStage.isCurrent ? 'Current' : 'Next'} action: ${
                primaryActionStage.state.actionLabel ??
                `Open ${primaryActionStage.label}`
              }`}
              className={cn(buttonVariants({ size: 'sm' }), 'w-full')}
            >
              {primaryActionStage.state.actionLabel ??
                `Open ${primaryActionStage.label}`}
              <ExternalLink className="h-3.5 w-3.5" />
            </Link>
          )}

          <details className="group rounded-md border">
            <summary className="flex cursor-pointer list-none items-center justify-between gap-2 px-3 py-2 text-sm font-medium [&::-webkit-details-marker]:hidden">
              <span>View full process</span>
              <ChevronDown className="h-4 w-4 transition-transform group-open:rotate-180" />
            </summary>
            <ol
              className="border-t px-3 py-2"
              aria-label="Full procurement process"
            >
              {flow.map((stage) => (
                <FullStageRow key={stage.id} stage={stage} />
              ))}
            </ol>
          </details>
        </CardContent>
      </Card>
    </aside>
  );
}

function CompactStageRow({
  label,
  stage,
  current = false,
}: {
  label: string;
  stage: ProcurementUatFlowStage;
  current?: boolean;
}) {
  const presentation = statusPresentation[stage.state.status];
  const Icon = presentation.icon;

  return (
    <div
      className={cn(
        'rounded-md border px-3 py-2',
        current && 'border-blue-200 bg-blue-50/50'
      )}
      aria-current={current ? 'step' : undefined}
    >
      <div className="flex items-start gap-2">
        <Icon
          className={cn('mt-0.5 h-4 w-4 shrink-0', presentation.iconClassName)}
          aria-hidden="true"
        />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center justify-between gap-1">
            <span className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
              {label}
            </span>
            <StatusBadge status={stage.state.status} />
          </div>
          <p className="mt-0.5 text-sm font-medium leading-tight">
            {stage.label}
          </p>
          {stage.state.responsibleRole && (
            <p className="mt-1 text-xs text-muted-foreground">
              Responsible: {stage.state.responsibleRole}
            </p>
          )}
          {stage.state.context && (
            <p className="mt-1 text-xs text-muted-foreground">
              {stage.state.context}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}

function FullStageRow({ stage }: { stage: ProcurementUatFlowStage }) {
  const presentation = statusPresentation[stage.state.status];
  const Icon = presentation.icon;
  const blockers = stage.state.blockers ?? [];

  return (
    <li
      className={cn(
        'relative flex gap-2 border-l pb-4 pl-4 last:border-l-transparent last:pb-1',
        stage.isCurrent && 'border-l-blue-400'
      )}
      aria-current={stage.isCurrent ? 'step' : undefined}
    >
      <span className="absolute -left-2 top-0 bg-card">
        <Icon
          className={cn('h-4 w-4', presentation.iconClassName)}
          aria-hidden="true"
        />
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-start justify-between gap-1">
          <p className="text-xs font-medium">
            {stage.index + 1}. {stage.label}
          </p>
          <StatusBadge status={stage.state.status} />
        </div>
        <p className="mt-1 text-[11px] leading-relaxed text-muted-foreground">
          {stage.state.context ?? stage.description}
        </p>
        {stage.state.responsibleRole && (
          <p className="mt-1 text-[11px] text-muted-foreground">
            Responsible: {stage.state.responsibleRole}
          </p>
        )}
        {blockers.length > 0 && (
          <ul className="mt-1 space-y-0.5 text-[11px] text-amber-800">
            {blockers.map((blocker) => (
              <li key={blocker}>Blocked: {blocker}</li>
            ))}
          </ul>
        )}
        {stage.state.href && (
          <Link
            href={stage.state.href}
            className="mt-1 inline-flex items-center gap-1 text-[11px] font-medium text-primary hover:underline"
          >
            {stage.state.actionLabel ?? `Open ${stage.label}`}
            <ExternalLink className="h-3 w-3" />
          </Link>
        )}
      </div>
    </li>
  );
}

function StatusBadge({ status }: { status: ProcurementUatStageStatus }) {
  const presentation = statusPresentation[status];

  return (
    <Badge
      variant="outline"
      className={cn(
        'px-1.5 py-0 text-[10px] font-medium',
        presentation.badgeClassName
      )}
    >
      {presentation.label}
    </Badge>
  );
}

export type { ProcurementUatStageId, ProcurementUatStageState };
