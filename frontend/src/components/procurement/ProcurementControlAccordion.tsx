'use client';

import React, { useId, useState, type ReactNode } from 'react';
import { ChevronDown, ShieldCheck } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface ProcurementControlAccordionProps {
  title: string;
  summary?: ReactNode;
  status?: ReactNode;
  notice?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  contentClassName?: string;
  'data-testid'?: string;
}

/** Progressive disclosure only: children stay mounted so readiness checks and form state are retained. */
export function ProcurementControlAccordion({
  title, summary, status, notice, actions, children, className, contentClassName,
  'data-testid': testId,
}: ProcurementControlAccordionProps) {
  const id = useId();
  const [open, setOpen] = useState(false);
  return (
    <section className={cn('rounded-xl border bg-card text-card-foreground shadow-sm', className)} data-testid={testId}>
      <div className="flex items-start gap-2 p-2">
        <h3 className="min-w-0 flex-1">
          <button
            type="button"
            id={`${id}-trigger`}
            aria-controls={`${id}-details`}
            aria-expanded={open}
            onClick={() => setOpen(value => !value)}
            className="flex w-full items-center gap-3 rounded-lg p-3 text-left transition-colors hover:bg-muted/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          >
            <ShieldCheck aria-hidden="true" className="h-4 w-4 shrink-0 text-muted-foreground" />
            <span className="min-w-0 flex-1">
              <span className="block text-sm font-semibold">{title}</span>
              {summary && <span className="mt-1 block text-xs font-normal text-muted-foreground">{summary}</span>}
            </span>
            <span className="shrink-0 text-xs font-medium text-primary max-sm:sr-only">{open ? 'Hide details' : 'Show details'}</span>
            <ChevronDown aria-hidden="true" className={cn('h-4 w-4 shrink-0 transition-transform', open && 'rotate-180')} />
          </button>
        </h3>
        {(status || actions) && <div className="flex max-w-[40%] shrink-0 flex-wrap items-center justify-end gap-2 py-3 pr-2">{status}{actions}</div>}
      </div>
      {notice && <div className="mx-5 mb-4 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-950 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100">{notice}</div>}
      <div id={`${id}-details`} role="region" aria-labelledby={`${id}-trigger`} hidden={!open} className={cn('border-t px-5 py-4', contentClassName)}>
        {children}
      </div>
    </section>
  );
}
