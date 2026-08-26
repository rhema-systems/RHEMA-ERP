'use client';

import Link from 'next/link';
import { AlertTriangle, FileWarning, Leaf, OctagonX } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';

/**
 * Report a concern — the unified reporting surface (area 25 slice 8, spec #36).
 *
 * The census found four separate open reporting screens and no front door; this is the front
 * door. All four forms are open to every employee (FR-SHE-100 / FR-SHE-200 / FR-ENV-025) and
 * always record the token's employee as the reporter.
 */
const CHOICES = [
  {
    href: '/me/safety/report/incident',
    icon: FileWarning,
    title: 'Report an incident',
    text: 'An accident, near miss, dangerous occurrence or illness — something already happened. Near misses matter as much as injuries.',
  },
  {
    href: '/me/safety/report/hazard',
    icon: AlertTriangle,
    title: 'Report a hazard',
    text: 'Something that could hurt someone but has not yet — an unguarded edge, a trailing cable, a blocked exit.',
  },
  {
    href: '/me/safety/report/stop-work',
    icon: OctagonX,
    title: 'Stop dangerous work',
    text: 'Work under way that looks imminently dangerous. You have the authority to stop it — stop first, record here.',
    urgent: true,
  },
  {
    href: '/me/safety/report/environmental',
    icon: Leaf,
    title: 'Report an environmental incident',
    text: 'A spill, uncontrolled dumping, contamination or an emission exceedance.',
  },
] as const;

export default function ReportConcernPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Report a Concern"
        description="Anyone can report — no role needed, and you are always recorded as the reporter. If anyone is hurt or in danger right now, deal with that and tell your supervisor first; these forms do not replace that."
        backHref="/me/safety"
      />

      <div className="grid gap-4 md:grid-cols-2">
        {CHOICES.map(({ href, icon: Icon, title, text, ...rest }) => {
          const urgent = 'urgent' in rest && rest.urgent;
          return (
            <Link key={href} href={href}>
              <Card
                className={`h-full transition-colors ${
                  urgent
                    ? 'border-destructive/50 hover:bg-destructive/5'
                    : 'hover:bg-muted/50'
                }`}
              >
                <CardContent className="flex items-start gap-3 p-4">
                  <Icon
                    className={`mt-0.5 h-6 w-6 shrink-0 ${
                      urgent ? 'text-destructive' : 'text-muted-foreground'
                    }`}
                  />
                  <div>
                    <p className="font-medium">{title}</p>
                    <p className="text-muted-foreground text-sm">{text}</p>
                  </div>
                </CardContent>
              </Card>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
