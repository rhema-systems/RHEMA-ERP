'use client';

import {
  Tag,
  Layers,
  Building2,
  GraduationCap,
  BookOpen,
  ClipboardPen,
  CalendarRange,
  Coins,
  ShieldAlert,
  Route,
  Handshake,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

export default function TrainingSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training & Learning"
        description="Catalog and providers behind the training lifecycle — planning, scheduling and delivery live under HR."
      />

      <NavCardGrid
        items={[
          {
            title: 'Categories',
            description: 'Classification used to group training programs.',
            href: '/administration/hr/training/categories',
            icon: Tag,
          },
          {
            title: 'Program Groups',
            description: 'Optional curriculum clusters above the individual program.',
            href: '/administration/hr/training/program-groups',
            icon: Layers,
          },
          {
            title: 'Vendors',
            description: 'External training firms, consultants and institutions.',
            href: '/administration/hr/training/vendors',
            icon: Building2,
          },
          {
            title: 'Trainers',
            description: 'Internal and external trainers, their skills and availability.',
            href: '/administration/hr/training/trainers',
            icon: GraduationCap,
          },
          {
            title: 'Programs',
            description: 'The training program catalog — materials, competencies and skills.',
            href: '/administration/hr/training/programs',
            icon: BookOpen,
          },
          {
            title: 'Needs Assessments',
            description: 'Training gaps identified for individual employees, with recommendations.',
            href: '/administration/hr/training/needs-assessments',
            icon: ClipboardPen,
          },
          {
            title: 'Training Plans',
            description: 'Annual or quarterly plans by organization scope, with items and budget lines.',
            href: '/administration/hr/training/plans',
            icon: CalendarRange,
          },
          {
            title: 'Training Budgets',
            description: 'Allocated training spend, approvals and spend transactions.',
            href: '/administration/hr/training/budgets',
            icon: Coins,
          },
          {
            title: 'Compliance Requirements',
            description: 'Training a population must hold, and how often it renews.',
            href: '/administration/hr/training/compliance',
            icon: ShieldAlert,
          },
          {
            title: 'Learning Paths',
            description: 'Ordered curricula, their target skills, and who is enrolled on them.',
            href: '/administration/hr/training/learning-paths',
            icon: Route,
          },
          {
            title: 'Mentoring Programmes',
            description: 'Mentoring schemes and the mentor/mentee pairs inside them.',
            href: '/administration/hr/training/mentoring',
            icon: Handshake,
          },
        ]}
      />
    </div>
  );
}
