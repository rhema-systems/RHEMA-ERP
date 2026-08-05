'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Building2, Handshake, FileClock, Receipt } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';
import {
  consultantClientService,
  clientEngagementService,
  consultantTimesheetService,
  timesheetInvoiceService,
} from '@/services/hr/consultant.service';

/**
 * Consulting landing page.
 *
 * The tiles track the four places work gets stuck: timesheets awaiting internal approval,
 * timesheets waiting on the client, invoices overdue, and engagements about to end.
 */
const navItems: NavCardItem[] = [
  {
    title: 'Clients',
    description: 'Client organisations, their engagements and portal contacts.',
    href: '/hr/consulting/clients',
    icon: Building2,
  },
  {
    title: 'Engagements',
    description: 'Placement contracts, their billing terms and lifecycle.',
    href: '/hr/consulting/engagements',
    icon: Handshake,
  },
  {
    title: 'Timesheets',
    description: 'Billing-period timesheets, their entries, approval and client confirmation.',
    href: '/hr/consulting/timesheets',
    icon: FileClock,
  },
  {
    title: 'Invoices',
    description: 'Invoices raised from client-confirmed timesheets.',
    href: '/hr/consulting/invoices',
    icon: Receipt,
  },
];

function StatTile({
  label,
  value,
  href,
  loading,
  tone,
}: {
  label: string;
  value?: number;
  href: string;
  loading: boolean;
  tone?: 'danger';
}) {
  return (
    <Link href={href}>
      <Card className="h-full transition-colors hover:bg-muted/50">
        <CardContent className="p-4">
          <p className="text-xs text-muted-foreground">{label}</p>
          {loading ? (
            <Skeleton className="mt-2 h-7 w-12" />
          ) : (
            <p
              className={`mt-1 text-2xl font-semibold ${tone === 'danger' ? 'text-red-600' : ''}`}
            >
              {value ?? 0}
            </p>
          )}
        </CardContent>
      </Card>
    </Link>
  );
}

export default function ConsultingHomePage() {
  const { data: clients, isLoading: loadingClients } = useQuery({
    queryKey: ['hr', 'consultant-clients', 'active'],
    queryFn: () => consultantClientService.getActive(),
  });

  const { data: pendingApproval, isLoading: loadingApproval } = useQuery({
    queryKey: ['hr', 'consultant-timesheets', 'pending-approval'],
    queryFn: () => consultantTimesheetService.getPendingApproval(),
  });

  const { data: pendingClient, isLoading: loadingClient } = useQuery({
    queryKey: ['hr', 'consultant-timesheets', 'pending-client'],
    queryFn: () => consultantTimesheetService.getPendingClientConfirmation(),
  });

  const { data: overdue, isLoading: loadingOverdue } = useQuery({
    queryKey: ['hr', 'timesheet-invoices', 'overdue'],
    queryFn: () => timesheetInvoiceService.getOverdue(),
  });

  const { data: endingSoon, isLoading: loadingEnding } = useQuery({
    queryKey: ['hr', 'client-engagements', 'ending-within', 30],
    queryFn: () => clientEngagementService.getEndingWithin(30),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Consulting"
        description="Client engagements, consultant timesheets and the invoices raised from them."
      />

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <StatTile
          label="Active clients"
          value={clients?.length}
          href="/hr/consulting/clients"
          loading={loadingClients}
        />
        <StatTile
          label="Timesheets awaiting approval"
          value={pendingApproval?.length}
          href="/hr/consulting/timesheets"
          loading={loadingApproval}
        />
        <StatTile
          label="Awaiting client confirmation"
          value={pendingClient?.length}
          href="/hr/consulting/timesheets"
          loading={loadingClient}
        />
        <StatTile
          label="Overdue invoices"
          value={overdue?.length}
          href="/hr/consulting/invoices"
          loading={loadingOverdue}
          tone="danger"
        />
        <StatTile
          label="Engagements ending in 30 days"
          value={endingSoon?.length}
          href="/hr/consulting/engagements"
          loading={loadingEnding}
        />
      </div>

      <NavCardGrid items={navItems} />
    </div>
  );
}
