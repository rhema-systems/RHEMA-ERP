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
    href: '/hr/recruitment/adverts',
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

export default function RecruitmentLandingPage() {
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(['SuperAdmin', 'HR']);

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
            { label: 'Open establishment gaps', value: gaps.data?.totalOpen ?? '—' },
            {
              label: 'Awaiting approval',
              value: r ? r.submitted + r.underReview : '—',
              hint: 'Requisitions with an approver',
            },
            { label: 'Approved requisitions', value: r?.approved ?? '—' },
            { label: 'Live adverts', value: adverts.data?.length ?? '—' },
            {
              label: 'Offers expiring soon',
              value: expiringOffers.data?.length ?? '—',
              hint: 'Within 7 days',
              tone: (expiringOffers.data?.length ?? 0) > 0 ? 'warning' : 'default',
            },
          ]}
        />
      )}

      <NavCardGrid items={isHr ? [...HR_ITEMS, ...EVERYONE_ITEMS] : EVERYONE_ITEMS} />
    </div>
  );
}
