'use client';

import { useQuery } from '@tanstack/react-query';
import { Briefcase, Building2, CalendarClock, ClipboardList, FileText, Megaphone, UserCheck, Users } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
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
const ITEMS: NavCardItem[] = [
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
    title: 'My panel',
    description: 'Interviews you are sitting on, and the scorecards you owe.',
    href: '/hr/recruitment/my-panel',
    icon: UserCheck,
  },
];

export default function RecruitmentLandingPage() {
  const requisitions = useQuery({
    queryKey: ['hr', 'requisitions', 'summary'],
    queryFn: () => staffRequisitionService.getStatusSummary(),
  });

  const gaps = useQuery({
    queryKey: ['hr', 'position-vacancy-stats'],
    queryFn: () => positionVacancyService.getStats(),
  });

  const adverts = useQuery({
    queryKey: ['hr', 'postings', 'active'],
    queryFn: () => jobPostingService.getActive(),
  });

  const r = requisitions.data;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Recruitment"
        description="From an establishment gap to an advert on a job board."
        backHref="/hr"
      />

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
        ]}
      />

      <NavCardGrid items={ITEMS} />
    </div>
  );
}
