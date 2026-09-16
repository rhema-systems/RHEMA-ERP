'use client';

import { useQuery } from '@tanstack/react-query';
import {
  Briefcase,
  Building2,
  CalendarClock,
  ClipboardList,
  FileText,
  Gauge,
  HandCoins,
  Megaphone,
  ShieldCheck,
  TrendingUp,
  UserCheck,
  Users,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { useAuth } from '@/hooks/use-auth';
import { jobOfferService } from '@/services/hr/offers.service';
import {
  jobPostingService,
  positionVacancyService,
  staffRequisitionService,
} from '@/services/hr/recruitment.service';

/**
 * Recruitment — ordered the way a hire actually happens.
 *
 * A gap in the establishment becomes a requisition for headcount; an approved requisition becomes
 * a vacancy; publishing the vacancy raises the adverts, which bring in candidates and applications.
 * Each screen is a step in that chain rather than a separate register, which is why establishment
 * comes first and applications last.
 *
 * Screening and the pipeline board are deliberately absent here: both are scoped to one vacancy and
 * are reached from that vacancy, not from a register of their own.
 */
const HR_ITEMS: NavCardItem[] = [
  {
    title: 'Dashboard',
    description: 'Pipeline shape, SLA breaches, what starts soon and what expires soon.',
    href: '/hr/recruitment/dashboard',
    icon: Gauge,
  },
  {
    title: 'Establishment',
    description: 'Where headcount sits against the establishment, and the gaps that follow.',
    href: '/hr/recruitment/establishment',
    icon: Building2,
  },
  {
    title: 'Requisitions',
    description: 'Requests for headcount, from draft through approval to fulfilment.',
    href: '/hr/recruitment/requisitions',
    icon: ClipboardList,
  },
  {
    title: 'Vacancies',
    description: 'Approved requisitions turned into advertised roles.',
    href: '/hr/recruitment/vacancies',
    icon: Briefcase,
  },
  {
    title: 'Live adverts',
    description: 'Every posting currently open for applications, across all channels.',
    href: '/hr/recruitment/postings',
    icon: Megaphone,
  },
  {
    title: 'Candidates',
    description: 'The people behind the applications, plus the talent pool kept for future roles.',
    href: '/hr/recruitment/candidates',
    icon: Users,
  },
  {
    title: 'Applications',
    description: 'Every application across all vacancies. Work one vacancy from its pipeline board.',
    href: '/hr/recruitment/applications',
    icon: FileText,
  },
  {
    title: 'Interviews',
    description: 'Scheduled sessions, their panels, question plans and the panel’s scorecards.',
    href: '/hr/recruitment/interviews',
    icon: CalendarClock,
  },
  {
    title: 'Offers',
    description: 'Terms raised against an application, through approval to the candidate’s response.',
    href: '/hr/recruitment/offers',
    icon: HandCoins,
  },
  {
    title: 'Hires',
    description: 'The handover from recruitment to employment — start dates and confirming an employee.',
    href: '/hr/recruitment/hires',
    icon: UserCheck,
  },
  {
    title: 'Pre-employment checks',
    description: 'Medical, police clearance, background and reference checks, across every offer.',
    href: '/hr/recruitment/pre-employment-checks',
    icon: ShieldCheck,
  },
  {
    title: 'Analytics',
    description: 'Time to fill, cost per hire, funnel, source effectiveness and recruiter load.',
    href: '/hr/recruitment/analytics',
    icon: TrendingUp,
  },
];

/**
 * Self-service surfaces, open to any authenticated employee — HR or not.
 *
 * Area 25 slice 13b moved both into the portal at `/me/*`. They are still linked from here
 * because a recruiter is also an employee and this is the page they are already on; the desk
 * keeps no copy of either screen.
 */
const EVERYONE_ITEMS: NavCardItem[] = [
  {
    title: 'Internal job board',
    description: 'Open roles you can apply for, and the applications you have already made.',
    href: '/me/jobs',
    icon: Megaphone,
  },
  {
    title: 'My panel',
    description: 'Interviews you are sitting on, and the scorecards you owe.',
    href: '/me/panel',
    icon: UserCheck,
  },
];

/**
 * What a tile shows while it does not have a number (G-2.1).
 *
 * ⚠ Every tile used to render `data?.field ?? '—'`. There was no error branch and no loading state,
 * so an endpoint returning 500, an expired token, and a request still in flight all displayed the
 * same em dash — a user could not tell *"we are still counting"* from *"we could not count"*.
 * Misleading rather than wrong, but on five of the module's front-page numbers.
 *
 * Three states, three appearances: `…` while loading, `—` when there is genuinely no figure, and —
 * for a failure — `—` **plus** the banner below the tiles, because a wrong-looking number with no
 * explanation is what caused the confusion in the first place.
 *
 * `0` is deliberately still `0`, not a dash: a genuine zero is an answer.
 */
function tileValue(
  query: { isLoading: boolean; isError: boolean },
  value: number | undefined,
): string | number {
  if (query.isLoading) return '…';
  if (query.isError) return '—';
  return value ?? '—';
}

export default function RecruitmentLandingPage() {
  const { hasAnyPermission } = useAuth();
  // G-2.3 (2026-09-15): this was hasAnyRole(['SuperAdmin', 'HR']) while every recruitment endpoint
  // behind it authorises on HR.Recruitment.*, which TenantAdmin, Admin and the legacy HR User role
  // also hold. Those three were served by the whole API and shown nothing here but the two
  // self-service cards — they could reach /hr/recruitment/requisitions by typing the URL and it
  // worked. The page now asks the question the API asks.
  const isHr = hasAnyPermission([
    'HR.Recruitment.Read',
    'HR.Recruitment.Write',
    'HR.Recruitment.Admin',
  ]);

  const requisitions = useQuery({
    queryKey: ['hr', 'requisitions', 'summary'],
    queryFn: () => staffRequisitionService.getStatusSummary(),
    enabled: isHr,
  });

  const gaps = useQuery({
    queryKey: ['hr', 'position-vacancy-stats'],
    queryFn: () => positionVacancyService.getStats(),
    enabled: isHr,
  });

  const adverts = useQuery({
    queryKey: ['hr', 'postings', 'active'],
    queryFn: () => jobPostingService.getActive(),
    enabled: isHr,
  });

  const expiringOffers = useQuery({
    queryKey: ['hr', 'offers', 'expiring'],
    queryFn: () => jobOfferService.getExpiring(7),
    enabled: isHr,
  });

  const r = requisitions.data;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Recruitment"
        description="From an establishment gap to an advert on a job board."
        backHref="/hr"
      />

      {isHr && (
        <MetricTiles
          tiles={[
            {
              label: 'Open establishment gaps',
              value: tileValue(gaps, gaps.data?.totalOpen),
              href: '/hr/recruitment/establishment',
            },
            {
              label: 'Awaiting approval',
              value: tileValue(requisitions, r ? r.submitted + r.underReview : undefined),
              hint: 'Requisitions with an approver',
              href: '/hr/recruitment/requisitions',
            },
            {
              label: 'Approved requisitions',
              value: tileValue(requisitions, r?.approved),
              href: '/hr/recruitment/requisitions',
            },
            {
              label: 'Live adverts',
              value: tileValue(adverts, adverts.data?.length),
              href: '/hr/recruitment/postings',
            },
            {
              label: 'Offers expiring soon',
              value: tileValue(expiringOffers, expiringOffers.data?.length),
              hint: 'Sent, unanswered, and running out within 7 days',
              tone: (expiringOffers.data?.length ?? 0) > 0 ? 'warning' : 'default',
              href: '/hr/recruitment/offers',
            },
          ]}
        />
      )}

      {/* G-2.1: the difference between "nothing to count" and "could not count". Without this a
          failed query is an em dash, which reads as a real and reassuring zero-ish answer. */}
      {isHr && [requisitions, gaps, adverts, expiringOffers].some((q) => q.isError) && (
        <p className="text-sm text-destructive">
          Some of these counters could not be loaded, so the figures above are incomplete. Refresh
          to try again.
        </p>
      )}

      <NavCardGrid items={isHr ? [...HR_ITEMS, ...EVERYONE_ITEMS] : EVERYONE_ITEMS} />
    </div>
  );
}
