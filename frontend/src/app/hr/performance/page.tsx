'use client';

import {
  BellRing,
  Building2,
  CalendarCheck,
  CalendarRange,
  ChartColumn,
  ClipboardCheck,
  ClipboardList,
  ClipboardPen,
  FastForward,
  Gavel,
  GraduationCap,
  Handshake,
  Layers,
  Lightbulb,
  MessagesSquare,
  NotebookPen,
  Scale,
  Target,
  TriangleAlert,
  UserCheck,
  Users,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';
import { AppraisalNotificationsPanel } from '@/components/hr/performance/AppraisalNotificationsPanel';

/**
 * Performance — the cycle, the appraisal run inside it, and the goal cascade underneath.
 *
 * Cycles come first because everything else is scoped to one: the cycle decides who is being
 * appraised, on which form, and against which deadlines. Then the run itself, ordered by who
 * acts — the employee's own appraisal and the peer feedback they owe, the manager's
 * evaluations and check-ins, then HR's sign-off. Below that the goal cascade, in the order
 * goals are actually set: the organisation states its objectives, units take them up,
 * employees align to a unit or company goal, and managers approve what lands with them.
 *
 * Then what happens around and after the sign-off, in the order it happens: calibration
 * reconciles ratings before HR signs anything, appeals contest what HR decided, and
 * recommendations turn a finished appraisal into an actual pay change or employment action.
 * Deadline enforcement is last because it is the exception path — the override for when a step
 * has stalled and nobody is going to complete it.
 */
const items: NavCardItem[] = [
  {
    title: 'Analytics',
    description:
      'For HR: where a cycle has actually got to — pipeline, deadlines, score spread, per-unit progress, what needs chasing, and what the outcomes became.',
    href: '/hr/performance/analytics',
    icon: ChartColumn,
  },
  {
    title: 'Appraisal Cycles',
    description:
      'Each run of the appraisal process — its dates, who it covers, which forms it uses, and how far along it is.',
    href: '/hr/performance/cycles',
    icon: CalendarRange,
  },
  {
    title: 'My Appraisals',
    description:
      'Your own appraisals: the self-evaluation, where each one has got to, and the result once HR signs it off.',
    href: '/hr/performance/appraisals',
    icon: ClipboardCheck,
  },
  {
    title: 'Peer Reviews',
    description:
      'Feedback colleagues have asked you for, and the forms still waiting on you.',
    href: '/hr/performance/peer-reviews',
    icon: MessagesSquare,
  },
  {
    title: 'Team Appraisals',
    description:
      'For managers: your reports in a cycle, what they have submitted, and your evaluation of each.',
    href: '/hr/performance/team-appraisals',
    icon: UserCheck,
  },
  {
    title: 'Check-ins',
    description:
      'One-to-ones held during a cycle, and the goal updates that come out of them.',
    href: '/hr/performance/check-ins',
    icon: CalendarCheck,
  },
  {
    title: 'Conversations',
    description:
      'Kick-off, quarterly, mid-year and final review meetings held against an appraisal — the agenda before, the notes after.',
    href: '/hr/performance/conversations',
    icon: MessagesSquare,
  },
  {
    title: 'Journal',
    description:
      'Evidence noted as it happens. Private to you unless you share it — a private entry is readable by nobody else, not your manager and not HR.',
    href: '/hr/performance/journal',
    icon: NotebookPen,
  },
  {
    title: 'Development Plans',
    description:
      'What people are working on becoming good at: objectives, progress against them, and the manager feedback recorded alongside.',
    href: '/hr/performance/development-plans',
    icon: GraduationCap,
  },
  {
    title: 'Improvement Plans',
    description:
      'Formal plans where performance has fallen short, their review meetings, and the outcome each one closed with.',
    href: '/hr/performance/pip',
    icon: ClipboardPen,
  },
  {
    title: 'HR Review',
    description:
      'For HR: appraisals ready for sign-off, what is outstanding on the rest, and the final score.',
    href: '/hr/performance/hr-review',
    icon: ClipboardList,
  },
  {
    title: 'Calibration',
    description:
      'Panels that reconcile managers’ ratings across a unit. On cycles that require it, nothing reaches HR review until a session is committed.',
    href: '/hr/performance/calibration',
    icon: Scale,
  },
  {
    title: 'Appeals',
    description:
      'Employees contesting a finalised appraisal — what is waiting on HR, and what is back with a manager.',
    href: '/hr/performance/appeals',
    icon: Gavel,
  },
  {
    title: 'Recommendations',
    description:
      'What appraisals say should happen next, and whether the pay change, PIP or promotion it called for actually got created.',
    href: '/hr/performance/recommendations',
    icon: Lightbulb,
  },
  {
    title: 'Proposals',
    description:
      'Pay changes and employment actions raised from appraisal outcomes, on their way to payroll and the modules that own them.',
    href: '/hr/performance/proposals',
    icon: Handshake,
  },
  {
    title: 'Company Goals',
    description:
      'A cycle’s organisation-wide objectives, and how far each has cascaded into units and people.',
    href: '/hr/performance/company-goals',
    icon: Building2,
  },
  {
    title: 'Unit Goals',
    description:
      'What each org unit is accountable for, and whether it rolls up to a company goal or stands alone.',
    href: '/hr/performance/unit-goals',
    icon: Layers,
  },
  {
    title: 'Employee Goals',
    description:
      'An employee’s goals for a cycle: weights, alignment, progress entries and the approval workflow.',
    href: '/hr/performance/employee-goals',
    icon: Target,
  },
  {
    title: 'Team Goals',
    description:
      'For managers: goals awaiting your approval, whose sets are incomplete, and what is slipping.',
    href: '/hr/performance/team-goals',
    icon: Users,
  },
  {
    title: 'Goals At Risk',
    description:
      'For HR: every at-risk goal across the organisation, most severe first, filterable by unit and level.',
    href: '/hr/performance/at-risk',
    icon: TriangleAlert,
  },
  {
    title: 'Notifications',
    description:
      'Your appraisal queue: cycle openings and the deadline reminders HR sends from a cycle.',
    href: '/hr/performance/notifications',
    icon: BellRing,
  },
  {
    title: 'Deadline Enforcement',
    description:
      'For HR: push a stalled appraisal past a step nobody is going to complete. Audited against you.',
    href: '/hr/performance/deadline-enforcement',
    icon: FastForward,
  },
];

export default function PerformanceLandingPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Performance"
        description="Appraisal cycles, and the goal cascade that runs inside them — from company objectives down to each employee."
        backHref="/hr"
      />
      <NavCardGrid items={items} />
      <AppraisalNotificationsPanel limit={5} />
    </div>
  );
}
