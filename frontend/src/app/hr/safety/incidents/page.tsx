'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, ChevronLeft, ChevronRight, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { safetyIncidentService } from '@/services/hr/safety-incident.service';
import {
  SHE_INCIDENT_STATUS_OPTIONS,
  type SheIncidentStatus,
} from '@/types/hr/safety-incidents';

const severityTone = (severity: string) =>
  severity === 'Catastrophic' || severity === 'Major'
    ? 'destructive'
    : severity === 'Moderate'
      ? 'secondary'
      : 'outline';

/**
 * The incident register (HR view). Employees do not come here — they report through the open
 * form and the SHE team works the register. Severity-based approval routing is deliberately
 * absent for now (FR-SHE-224 is a later build); triage is manual through the detail page.
 */
export default function IncidentRegisterPage() {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<SheIncidentStatus | 'all'>('all');
  const pageSize = 20;

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'safety-incidents', page, status],
    queryFn: () =>
      safetyIncidentService.getPaged(page, pageSize, status === 'all' ? undefined : status),
  });

  const items = data?.items ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Incident Register"
        description="Every reported incident, newest first. Investigation, corrective actions and closure run from the incident itself."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/incidents/new">
              <Plus className="mr-2 h-4 w-4" />
              Record incident
            </Link>
          </Button>
        }
      />

      <div className="flex items-center gap-3">
        <Select
          value={status}
          onValueChange={(v) => {
            setStatus(v as SheIncidentStatus | 'all');
            setPage(1);
          }}
        >
          <SelectTrigger className="w-64">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {SHE_INCIDENT_STATUS_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {data && (
          <span className="text-muted-foreground text-sm">
            {data.totalCount} incident{data.totalCount === 1 ? '' : 's'}
          </span>
        )}
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              title="No incidents"
              description={
                status === 'all'
                  ? 'Nothing reported yet — a quiet register is the goal.'
                  : 'Nothing in this status.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Severity</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead>Reported by</TableHead>
                  <TableHead className="text-right">People</TableHead>
                  <TableHead className="text-right">Open CAs</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((i) => (
                  <TableRow key={i.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/safety/incidents/${i.id}`} className="hover:underline">
                        <span className="font-mono">{i.incidentNumber}</span>
                      </Link>
                      {i.isLostTimeInjury && (
                        <Badge variant="destructive" className="ml-2">
                          LTI · {i.totalLostDays}d
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell>{new Date(i.incidentDate).toLocaleDateString()}</TableCell>
                    <TableCell>{i.incidentTypeName ?? i.categoryName}</TableCell>
                    <TableCell>
                      <Badge variant={severityTone(i.severity)}>{i.severityName}</Badge>
                    </TableCell>
                    <TableCell>{i.locationName ?? '—'}</TableCell>
                    <TableCell>{i.reportedByName || '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {i.involvedPersonCount}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {i.openCorrectiveActionCount > 0 ? (
                        <span className="flex items-center justify-end gap-1">
                          <AlertTriangle className="h-3.5 w-3.5 text-amber-600" />
                          {i.openCorrectiveActionCount}
                        </span>
                      ) : (
                        0
                      )}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={i.statusName} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="outline"
            size="icon"
            disabled={!data.hasPrevious}
            onClick={() => setPage((p) => p - 1)}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="text-sm tabular-nums">
            {data.page} / {data.totalPages}
          </span>
          <Button
            variant="outline"
            size="icon"
            disabled={!data.hasNext}
            onClick={() => setPage((p) => p + 1)}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      )}
    </div>
  );
}
