'use client';

import Link from 'next/link';
import { ArrowRight } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';
import { hrOperationalTrainingLinks, hrSetupGroup } from '@/config/hr-setup-nav';

/**
 * The training catalogue: what can be delivered, by whom, and what it must renew.
 *
 * ⚠ Needs Assessments, Training Plans and Training Budgets are no longer cards here. They are
 * per-employee, per-cycle casework — the front of the same chain whose every later step
 * (Requests, Nomination Approvals, Enrollments, Completions) already lived under `/hr/training`
 * — so both the screens and their routes moved there. The pointers below are for anyone who
 * still comes to this page looking for them.
 *
 * Cards come from `config/hr-setup-nav.ts` — see the note on the HR hub page.
 */
const group = hrSetupGroup('Training & Learning');

export default function TrainingSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training & Learning"
        description={group.description}
        backHref="/administration/hr"
      />

      <NavCardGrid items={group.links} />

      <div className="rounded-lg border border-dashed p-4">
        <h2 className="text-sm font-medium">Moved to HR → Training &amp; Development</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          These are run per cycle rather than configured once, so they sit with the rest of the
          training desk.
        </p>
        <ul className="mt-3 space-y-1">
          {hrOperationalTrainingLinks.map(link => (
            <li key={link.href}>
              <Link
                href={link.href}
                className="inline-flex items-center gap-1.5 text-sm text-primary hover:underline"
              >
                {link.title}
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
