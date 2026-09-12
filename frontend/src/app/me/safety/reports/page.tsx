'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { FileWarning, Leaf, OctagonX } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { safetyIncidentService } from '@/services/hr/safety-incident.service';
import { safetyStopWorkService } from '@/services/hr/safety-stop-work.service';
import { safetyEnvironmentalService } from '@/services/hr/safety-environmental.service';
import type { SheStopWorkStatus } from '@/types/hr/safety-audits';

/**
 * My Reports (area 25 slice 8) — everything the caller has raised, in one place: safety
 * incidents (reported or involved in — the new `incidents/mine` self arm), stop-work orders and
 * environmental incidents (their existing mine reads). Follow-up happens desk-side; this page
 * answers "what happened to what I reported?".
 *
 * Hazards are absent by design: the hazard entity records no reporter (recorded residual), so a
 * "hazards I reported" list cannot exist yet.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const STOP_WORK_LABEL: Record<SheStopWorkStatus, string> = {
  Raised: 'Raised — work stopped',
  UnderReview: 'Under review — work stopped',
  Resolved: 'Resolved — awaiting clearance',
  Cleared: 'Cleared — work resumed',
  Cancelled: 'Cancelled',
};

export default function MySafetyReportsPage() {
  const { data: incidents = [], isLoading: incidentsLoading } = useQuery({
    queryKey: ['me', 'safety', 'incidents'],
    queryFn: () => safetyIncidentService.getMine(),
  });
  const { data: stopWork = [], isLoading: stopWorkLoading } = useQuery({
    queryKey: ['me', 'safety', 'stop-work'],
    queryFn: () => safetyStopWorkService.getMine(),
  });
  const { data: environmental = [], isLoading: envLoading } = useQuery({
    queryKey: ['me', 'safety', 'environmental'],
    queryFn: () => safetyEnvironmentalService.getMyIncidents(),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Reports"
        description="The concerns you have raised and where each one stands. The SHE team investigates and closes them out."
        backHref="/me/safety"
        actions={
          <Button asChild>
            <Link href="/me/safety/report">Report a concern</Link>
          </Button>
        }
      />

      <Tabs defaultValue="incidents">
        <TabsList>
          <TabsTrigger value="incidents">Incidents ({incidents.length})</TabsTrigger>
          <TabsTrigger value="stop-work">Stop-work ({stopWork.length})</TabsTrigger>
          <TabsTrigger value="environmental">Environmental ({environmental.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="incidents" className="mt-4">
          {incidentsLoading ? null : incidents.length === 0 ? (
            <EmptyState
              icon={FileWarning}
              title="No incident reports"
              description="Incidents you report — or are recorded as involved in — appear here."
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Number</TableHead>
                      <TableHead>Category</TableHead>
                      <TableHead>Severity</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {incidents.map((i) => (
                      <TableRow key={i.id}>
                        <TableCell className="font-mono text-sm">{i.incidentNumber}</TableCell>
                        <TableCell>{i.categoryName}</TableCell>
                        <TableCell>{i.severityName}</TableCell>
                        <TableCell>{fmtDate(i.incidentDate)}</TableCell>
                        <TableCell>
                          {i.status === 'Closed' ? (
                            <Badge variant="outline" className="text-muted-foreground">
                              Closed
                            </Badge>
                          ) : (
                            <Badge variant="secondary">{i.statusName}</Badge>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="stop-work" className="mt-4">
          {stopWorkLoading ? null : stopWork.length === 0 ? (
            <EmptyState
              icon={OctagonX}
              title="No stop-work orders"
              description="Stop-work orders you raise appear here with their resolution."
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Number</TableHead>
                      <TableHead className="max-w-sm">Work stopped</TableHead>
                      <TableHead>Raised</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Resolution</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {stopWork.map((o) => (
                      <TableRow key={o.id}>
                        <TableCell className="font-mono text-sm">{o.orderNumber}</TableCell>
                        <TableCell className="max-w-sm">
                          <span className="line-clamp-1">{o.workDescription}</span>
                        </TableCell>
                        <TableCell>{fmtDate(o.raisedDate)}</TableCell>
                        <TableCell>
                          <Badge
                            variant={
                              o.status === 'Cleared'
                                ? 'secondary'
                                : o.status === 'Cancelled'
                                  ? 'outline'
                                  : 'destructive'
                            }
                          >
                            {STOP_WORK_LABEL[o.status]}
                          </Badge>
                        </TableCell>
                        <TableCell className="max-w-xs">
                          <span className="text-muted-foreground line-clamp-1 text-sm">
                            {o.resolutionDescription ?? '—'}
                          </span>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="environmental" className="mt-4">
          {envLoading ? null : environmental.length === 0 ? (
            <EmptyState
              icon={Leaf}
              title="No environmental reports"
              description="Environmental incidents you report appear here."
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Number</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Severity</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {environmental.map((i) => (
                      <TableRow key={i.id}>
                        <TableCell className="font-mono text-sm">{i.incidentNumber}</TableCell>
                        <TableCell>{i.typeName}</TableCell>
                        <TableCell>{i.severityName}</TableCell>
                        <TableCell>{fmtDate(i.incidentDate)}</TableCell>
                        <TableCell>
                          {i.status === 'Closed' ? (
                            <Badge variant="outline" className="text-muted-foreground">
                              Closed
                            </Badge>
                          ) : (
                            <Badge variant="secondary">{i.statusName}</Badge>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
