'use client';

import { Workflow, HelpCircle, LayoutList, ClipboardCheck } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

/**
 * Setup landing for recruitment.
 *
 * ⚠ Built in slice 10 because two screens already pointed here. `question-bank` and
 * `question-presets` both carried `backHref="/administration/hr/recruitment"` and the route did
 * not exist, so their Back button was a 404 — the group was the only one of its five neighbours
 * (performance, training, orientation, safety, attendance) without an index page, and the
 * sidebar's group parent had been aimed at a child to work around it.
 */
export default function RecruitmentAdministrationPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Recruitment Setup"
        description="The reusable parts of hiring: the stages an application moves through, the questions asked at interview, and the checks run before an offer. Requisitions, vacancies and candidates live under HR."
      />

      <NavCardGrid
        items={[
          {
            title: 'Pipelines',
            description:
              'The stages an application moves through, and what each stage requires before it can advance.',
            href: '/administration/hr/recruitment/pipelines',
            icon: Workflow,
          },
          {
            title: 'Question Bank',
            description:
              'Interview questions, with the competency each is meant to test and how it is scored.',
            href: '/administration/hr/recruitment/question-bank',
            icon: HelpCircle,
          },
          {
            title: 'Interview Presets',
            description:
              'Named sets of questions, so a panel starts from a prepared scorecard rather than a blank one.',
            href: '/administration/hr/recruitment/question-presets',
            icon: LayoutList,
          },
          {
            title: 'Check Templates',
            description:
              'The pre-employment checks — references, medicals, qualifications — and which are mandatory.',
            href: '/administration/hr/recruitment/check-templates',
            icon: ClipboardCheck,
          },
        ]}
      />
    </div>
  );
}
