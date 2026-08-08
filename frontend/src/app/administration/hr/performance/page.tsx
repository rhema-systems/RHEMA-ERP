'use client';

import {
  ClipboardCheck,
  Gauge,
  Library,
  ListChecks,
  Medal,
  SlidersHorizontal,
  Target,
  TriangleAlert,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';

/**
 * Setup for Performance — everything an appraisal is built out of before one can run.
 *
 * The order below is roughly the order it is set up in: the policy profile first, then the
 * vocabulary an appraisal form scores in (criteria, grades, KPIs), then the forms themselves,
 * then the goal-cascade pieces. Cycles are operational rather than setup and live under
 * `/hr/performance/cycles`.
 */
const items: NavCardItem[] = [
  {
    title: 'Appraisal Settings',
    description:
      'Named policy profiles a cycle runs under: who evaluates, how their scores are weighted, and what must happen before a result is final.',
    href: '/administration/hr/performance/settings',
    icon: SlidersHorizontal,
  },
  {
    title: 'Appraisal Templates',
    description:
      'The forms appraisals are scored on — weighted sections and items, scoped so each employee gets the right one.',
    href: '/administration/hr/performance/templates',
    icon: ClipboardCheck,
  },
  {
    title: 'Appraisal Criteria',
    description:
      'The behaviours and competencies a form can score, alongside its KPIs.',
    href: '/administration/hr/performance/criteria',
    icon: ListChecks,
  },
  {
    title: 'Grade Definitions',
    description:
      'The grades items are scored into, and the bands that map an overall result onto a rating.',
    href: '/administration/hr/performance/grade-definitions',
    icon: Medal,
  },
  {
    title: 'Strategic Goals',
    description:
      'Multi-year company intent. A cycle’s company goals link back to one, which is how each year inherits the strategy.',
    href: '/administration/hr/performance/strategic-goals',
    icon: Target,
  },
  {
    title: 'Goal Library',
    description:
      'Reusable goal templates, optionally scoped to an org level, unit or position. Copied into employee goals.',
    href: '/administration/hr/performance/goal-library',
    icon: Library,
  },
  {
    title: 'KPI Definitions',
    description:
      'How a goal is measured — the unit, the measurement type and the tolerance a result may miss by.',
    href: '/administration/hr/performance/kpi-definitions',
    icon: Gauge,
  },
  {
    title: 'Goal Risk Thresholds',
    description:
      'What counts as at risk. Drives the manager workspace’s at-risk tab and the org-wide report.',
    href: '/administration/hr/performance/goal-risk-settings',
    icon: TriangleAlert,
  },
];

export default function PerformanceSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Performance Setup"
        description="The policy, forms, measures and thresholds that appraisals and the goal cascade are built on."
        backHref="/administration/hr"
      />
      <NavCardGrid items={items} />
    </div>
  );
}
