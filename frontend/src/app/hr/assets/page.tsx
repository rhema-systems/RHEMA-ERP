'use client';

import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowRightLeft,
  BellRing,
  Boxes,
  Banknote,
  ClipboardList,
  FileText,
  Package,
  Receipt,
  ShieldCheck,
  Undo2,
  Wrench,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { MetricTiles, type MetricTile } from '@/components/hr/common/MetricTiles';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';
import { assetRegisterService } from '@/services/hr/asset-register.service';

/**
 * The company-asset register hub — area 16.
 *
 * The counters come from one call, `reports/register` unfiltered, rather than from six list reads
 * counted client-side. That matters beyond speed: the report's watchlist counts are **disjoint**
 * (due-soon excludes overdue) while the watchlist reads are **inclusive**, so a tile counting rows
 * off a list and a tile reading the report would print two different numbers for the same words.
 * Every figure on this page is the report's.
 */
export default function AssetsHubPage() {
  const { data: report } = useQuery({
    queryKey: ['hr', 'assets', 'report', 'hub'],
    queryFn: () => assetRegisterService.getRegisterReport(),
  });

  const tiles: MetricTile[] = [
    {
      label: 'Assets on the register',
      value: report?.assetCount ?? '—',
      hint: `${report?.assignedCount ?? 0} in someone's hands`,
      icon: Boxes,
      href: '/hr/assets/register',
    },
    {
      label: 'Service overdue',
      value: report?.maintenanceOverdueCount ?? '—',
      hint: `${report?.maintenanceDueSoonCount ?? 0} due soon, ${report?.maintenanceUnscheduledCount ?? 0} never scheduled`,
      icon: Wrench,
      tone: (report?.maintenanceOverdueCount ?? 0) > 0 ? 'danger' : 'default',
      href: '/hr/assets/maintenance',
    },
    {
      label: 'Cover lapsed',
      value: report?.insuranceExpiredCount ?? '—',
      hint: `${report?.insuranceExpiringSoonCount ?? 0} lapsing soon, ${report?.insuranceUndatedCount ?? 0} never dated`,
      icon: ShieldCheck,
      tone: (report?.insuranceExpiredCount ?? 0) > 0 ? 'danger' : 'default',
      href: '/hr/assets/insurance',
    },
    {
      label: 'Returns overdue',
      value: report?.returnsOverdueCount ?? '—',
      hint: 'Custodies past their expected return date',
      icon: Undo2,
      tone: (report?.returnsOverdueCount ?? 0) > 0 ? 'warning' : 'default',
      href: '/hr/assets/returns',
    },
  ];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Company assets"
        description="What the organisation owns, who is holding it, and what needs attention."
      />

      <MetricTiles tiles={tiles} />

      <NavCardGrid
        items={[
          {
            title: 'Register',
            description: 'Every asset, with its type, location, unit and current holder.',
            href: '/hr/assets/register',
            icon: Boxes,
          },
          {
            title: 'Assignments',
            description: 'Who signed for what, and when it is due back.',
            href: '/hr/assets/assignments',
            icon: Package,
          },
          {
            title: 'Requisitions',
            description: 'Requests for equipment, and the approvals waiting on you.',
            href: '/hr/assets/requisitions',
            icon: ClipboardList,
          },
          {
            title: 'Transfers',
            description: 'Assets moving between people, locations and units.',
            href: '/hr/assets/transfers',
            icon: ArrowRightLeft,
          },
          {
            title: 'Surcharges',
            description: 'Charges raised for damage or loss, and what payroll may recover.',
            href: '/hr/assets/surcharges',
            icon: Receipt,
          },
          {
            title: 'Maintenance',
            description: 'The service log, and the three lists of what has slipped.',
            href: '/hr/assets/maintenance',
            icon: Wrench,
          },
          {
            title: 'Insurance',
            description: 'Cover lapsing, cover lapsed, and policies nobody ever dated.',
            href: '/hr/assets/insurance',
            icon: ShieldCheck,
          },
          {
            title: 'Returns',
            description: 'What is coming back this fortnight, and what is already late.',
            href: '/hr/assets/returns',
            icon: AlertTriangle,
          },
          {
            title: 'Payroll deductions',
            description: 'What HR has declared for asset rental and surcharge recovery.',
            href: '/hr/assets/payroll',
            icon: Banknote,
          },
          {
            title: 'Register report',
            description: 'The estate counted and totalled on one printable page.',
            href: '/hr/assets/report',
            icon: FileText,
          },
          {
            title: 'Reminders',
            description: 'What the nightly sweep would chase, and what it has chased.',
            href: '/hr/assets/reminders',
            icon: BellRing,
          },
        ]}
      />
    </div>
  );
}
