'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, CalendarClock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
import { safetyAuditService } from '@/services/hr/safety-audit.service';
import {
  SHE_AUDIT_STATUS_OPTIONS,
  type SheAuditStatus,
  type SheAuditSummary,
} from '@/types/hr/safety-audits';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

function StatusBadge({ status }: { status: SheAuditStatus }) {
  switch (status) {
    case 'Closed':
      return <Badge variant="secondary">Closed</Badge>;
    case 'Cancelled':
      return <Badge variant="outline">Cancelled</Badge>;
    case 'InProgress':
      return <Badge>In progress</Badge>;
    case 'ReportIssued':
      return <Badge variant="destructive">Report issued — findings open</Badge>;
    default:
      return <Badge variant="outline">Planned</Badge>;
  }
}

/**
 * The SHE audit register (FRD §12): planning → execution → findings → CAPA →
 * verification → closure. An audit whose report is issued stays prominent until
 * every finding is verified and closed.
 */
export default function SheAuditsPage() {
  const [status, setStatus] = useState<SheAuditStatus | 'all'>('all');

  const { data: audits = [], isLoading } = useQuery({
    queryKey: ['hr', 'safety-audits', 'list', status],
    queryFn: () => safetyAuditService.getAll(status === 'all' ? undefined : status),
  });

  const { data: upcoming = [] } = useQuery({
    queryKey: ['hr', 'safety-audits', 'upcoming'],
    queryFn: () => safetyAuditService.getUpcoming(30),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="SHE Audits"
        description="Management-system audits against their standards — planning, execution, findings and verified close-out. Distinct from workplace inspections."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/audits/new">
              <Plus className="mr-2 h-4 w-4" />
              Plan audit
            </Link>
          </Button>
        }
      />

      {upcoming.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-muted-foreground flex items-center gap-2 text-sm font-medium">
              <CalendarClock className="h-4 w-4" />
              Starting within 30 days
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {upcoming.map((a) => (
              <Link key={a.id} href={`/hr/safety/audits/${a.id}`}>
                <Badge variant="outline" className="hover:bg-accent cursor-pointer">
                  {a.auditNumber} · {fmtDate(a.plannedStartDate)}
                </Badge>
              </Link>
            ))}
          </CardContent>
        </Card>
      )}

      <div className="flex items-center gap-3">
        <Select value={status} onValueChange={(v) => setStatus(v as SheAuditStatus | 'all')}>
          <SelectTrigger className="w-52">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {SHE_AUDIT_STATUS_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : audits.length === 0 ? (
            <EmptyState
              title="No audits"
              description="Plan the first SHE audit to start the programme."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Standard</TableHead>
                  <TableHead>Lead auditor</TableHead>
                  <TableHead>Planned start</TableHead>
                  <TableHead className="text-right">Findings (open)</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {audits.map((a: SheAuditSummary) => (
                  <TableRow key={a.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/safety/audits/${a.id}`} className="hover:underline">
                        <span className="font-mono">{a.auditNumber}</span>
                      </Link>
                    </TableCell>
                    <TableCell className="max-w-xs">
                      <span className="line-clamp-1">{a.title}</span>
                    </TableCell>
                    <TableCell>{a.typeName}</TableCell>
                    <TableCell>{a.standard ?? '—'}</TableCell>
                    <TableCell>{a.leadAuditorName}</TableCell>
                    <TableCell className="tabular-nums">{fmtDate(a.plannedStartDate)}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {a.findingCount} ({a.openFindingCount})
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={a.status} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
