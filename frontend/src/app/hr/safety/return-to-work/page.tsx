'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { HeartPulse, Plus } from 'lucide-react';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { safetyReturnToWorkService } from '@/services/hr/safety-return-to-work.service';
import { SHE_RETURN_TO_WORK_STATUS_OPTIONS } from '@/types/hr/safety-health';
import type { SheReturnToWorkPlanSummary } from '@/types/hr/safety-health';

/**
 * Return-to-work register. Access rides the HR.Medical.* policies — plans carry medical
 * restrictions and clearance notes. "Active" here means not yet Completed or Discontinued.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const statusLabel = (v: string) =>
  SHE_RETURN_TO_WORK_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

function PlanTable({ items, emptyText }: { items: SheReturnToWorkPlanSummary[]; emptyText: string }) {
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={emptyText} icon={HeartPulse} />;
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Plan</TableHead>
              <TableHead>Employee</TableHead>
              <TableHead>Incident</TableHead>
              <TableHead>Plan date</TableHead>
              <TableHead>Planned return</TableHead>
              <TableHead>Actual return</TableHead>
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((p) => (
              <TableRow key={p.id}>
                <TableCell>
                  <Link
                    href={`/hr/safety/return-to-work/${p.id}`}
                    className="font-mono text-primary hover:underline"
                  >
                    {p.planNumber}
                  </Link>
                </TableCell>
                <TableCell className="font-medium">{p.employeeName}</TableCell>
                <TableCell>
                  {p.safetyIncidentNumber ? (
                    <span className="font-mono text-sm">{p.safetyIncidentNumber}</span>
                  ) : (
                    '—'
                  )}
                </TableCell>
                <TableCell>{fmtDate(p.planDate)}</TableCell>
                <TableCell>{fmtDate(p.plannedReturnDate)}</TableCell>
                <TableCell>{fmtDate(p.actualReturnDate)}</TableCell>
                <TableCell>
                  {p.status === 'Completed' && !p.successfullyCompleted ? (
                    <Badge variant="destructive">Completed — unsuccessful</Badge>
                  ) : (
                    <StatusBadge status={statusLabel(p.status)} />
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function ReturnToWorkPage() {
  const [tab, setTab] = useState('active');

  const { data: all = [] } = useQuery({
    queryKey: ['hr', 'safety-rtw'],
    queryFn: () => safetyReturnToWorkService.getAll(),
  });
  const { data: active = [] } = useQuery({
    queryKey: ['hr', 'safety-rtw', 'active'],
    queryFn: () => safetyReturnToWorkService.getActive(),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Return to Work"
        description="Phased return plans after injury or illness, with medical clearance, workplace modifications and periodic reviews. Access requires medical permissions."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/return-to-work/new">
              <Plus className="mr-2 h-4 w-4" /> New plan
            </Link>
          </Button>
        }
      />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="active">Active ({active.length})</TabsTrigger>
          <TabsTrigger value="all">All plans ({all.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="active" className="mt-4">
          <PlanTable
            items={active}
            emptyText="No open return-to-work plans — nobody is currently on a phased return."
          />
        </TabsContent>
        <TabsContent value="all" className="mt-4">
          <PlanTable items={all} emptyText="No return-to-work plans have been recorded yet." />
        </TabsContent>
      </Tabs>
    </div>
  );
}
