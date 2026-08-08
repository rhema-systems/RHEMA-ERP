'use client';

import { Coins, Briefcase, ShieldPlus } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';

/**
 * Setup for Compensation & Benefits. The operational side — an employee's resolved package and
 * their benefit enrolments — lives under `/hr`.
 *
 * Salary grades are deliberately absent: payroll owns the salary structure and HR only mirrors
 * it, so there is no HR grade editor to link to.
 */
const items: NavCardItem[] = [
  {
    title: 'Pay Components',
    description:
      'Allowances and deductions, mirrored from Payroll. Pension, tax treatment and effective dating are set here.',
    href: '/administration/hr/compensation/pay-components',
    icon: Coins,
  },
  {
    title: 'Position Emoluments',
    description: 'Components every holder of a position inherits, with per-position amounts.',
    href: '/administration/hr/compensation/position-emoluments',
    icon: Briefcase,
  },
  {
    title: 'Benefit Policies',
    description: 'Benefit schemes, their eligibility rules and per-grade values.',
    href: '/administration/hr/compensation/benefit-policies',
    icon: ShieldPlus,
  },
];

export default function CompensationSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Compensation & Benefits Setup"
        description="Pay components, position emoluments and the benefit policies employees enrol in."
        backHref="/administration/hr"
      />
      <NavCardGrid items={items} />
    </div>
  );
}
