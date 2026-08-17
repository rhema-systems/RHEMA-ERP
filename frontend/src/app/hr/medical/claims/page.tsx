'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { FileText, Flag } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { medicalClaimService } from '@/services/hr/medical-claims.service';
import { MEDICAL_EXPENSE_TYPE_OPTIONS } from '@/types/hr/medical';
import type { MedicalExpenseClaimSummary } from '@/types/hr/medical';

/**
 * The medical claims caseload.
 *
 * Three queues rather than one filtered list, because they are different jobs: claims awaiting a
 * decision, claims someone has flagged for a closer look, and the full register for lookup.
 */
const money = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const typeLabel = (v: string) =>
  MEDICAL_EXPENSE_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

function StatusBadgeFor({ status }: { status: string }) {
  if (status === 'Paid') return <Badge variant="secondary">Paid</Badge>;
  if (status === 'Approved') return <Badge variant="secondary">Approved</Badge>;
  if (status === 'Rejected' || status === 'Cancelled')
    return <Badge variant="destructive">{status}</Badge>;
  return <Badge variant="outline">{status}</Badge>;
}

function ClaimTable({
  items,
  empty,
}: {
  items: MedicalExpenseClaimSummary[];
  empty: string;
}) {
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={empty} icon={FileText} />;
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Claim</TableHead>
              <TableHead>Employee</TableHead>
              <TableHead>Service date</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Facility</TableHead>
              <TableHead className="text-right">Requested</TableHead>
              <TableHead className="text-right">Approved</TableHead>
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((c) => (
              <TableRow key={c.id}>
                <TableCell>
                  <Link
                    href={`/hr/medical/claims/${c.id}`}
                    className="font-mono text-sm text-primary hover:underline"
                  >
                    {c.claimNumber}
                  </Link>
                  {c.isFlaggedForReview && (
                    <Flag className="ml-2 inline h-3 w-3 text-red-600" aria-label="Flagged" />
                  )}
                </TableCell>
                <TableCell className="font-medium">
                  {c.employeeName}
                  {c.isForDependent && (
                    <span className="ml-1 text-muted-foreground">
                      · for {c.dependentName || 'dependant'}
                    </span>
                  )}
                </TableCell>
                <TableCell>{fmtDate(c.serviceDate)}</TableCell>
                <TableCell>{typeLabel(c.expenseType)}</TableCell>
                <TableCell>{c.facilityName}</TableCell>
                <TableCell className="text-right tabular-nums">
                  {money(c.amountRequested)}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {money(c.amountApproved)}
                </TableCell>
                <TableCell>
                  <StatusBadgeFor status={c.status} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function MedicalClaimsPage() {
  const [tab, setTab] = useState('pending');
  const [page, setPage] = useState(1);

  const { data: pending = [] } = useQuery({
    queryKey: ['hr', 'medical-claims', 'pending'],
    queryFn: () => medicalClaimService.getPending(),
  });
  const { data: flagged = [] } = useQuery({
    queryKey: ['hr', 'medical-claims', 'flagged'],
    queryFn: () => medicalClaimService.getFlagged(),
  });
  const { data: paged } = useQuery({
    queryKey: ['hr', 'medical-claims', 'paged', page],
    queryFn: () => medicalClaimService.getPaged(page, 20),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Medical Claims"
        description="Reimbursement claims filed by employees, and the decisions taken on them. Employees file their own through My Medical Claims."
        backHref="/hr/medical"
      />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="pending">Awaiting decision ({pending.length})</TabsTrigger>
          <TabsTrigger value="flagged">Flagged ({flagged.length})</TabsTrigger>
          <TabsTrigger value="all">All claims</TabsTrigger>
        </TabsList>

        <TabsContent value="pending" className="mt-4">
          <ClaimTable
            items={pending}
            empty="Nothing is waiting on a decision."
          />
        </TabsContent>

        <TabsContent value="flagged" className="mt-4">
          <p className="mb-3 text-sm text-muted-foreground">
            Claims marked for a closer look. A claimant is never told their claim has been flagged.
          </p>
          <ClaimTable items={flagged} empty="No claims are currently flagged." />
        </TabsContent>

        <TabsContent value="all" className="mt-4 space-y-4">
          <ClaimTable
            items={paged?.items ?? []}
            empty="No claims have been filed yet."
          />
          {paged && paged.totalPages > 1 && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-muted-foreground">
                Page {paged.page} of {paged.totalPages} · {paged.totalCount} claims
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!paged.hasPrevious}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!paged.hasNext}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
