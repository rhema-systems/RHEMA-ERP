'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import { CheckCircle2, Gavel, Plus, Scale, TriangleAlert, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { OrganizationScopeFields } from '@/components/hr/performance/OrganizationScopeFields';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { calibrationSessionService } from '@/services/hr/calibration.service';
import type { CalibrationSession, CalibrationStatus } from '@/types/hr/calibration';

/**
 * Calibration sessions for a cycle.
 *
 * A session is the panel that reconciles managers' ratings across a unit before HR signs the
 * appraisals off. When the cycle's settings profile has calibration switched on, this is not
 * optional: an appraisal cannot reach HR review until a session covering it has been committed.
 *
 * **Scope is the thing to get right when creating one.** A session covers its cycle, narrowed to
 * an organization unit (and everything beneath it) or an organization level. Leave both blank
 * and it covers the whole cycle — which is what you want for a small organisation and almost
 * never what you want otherwise.
 */
type Filter = 'open' | 'completed' | 'all';

const OPEN_STATUSES: CalibrationStatus[] = ['Pending', 'InProgress'];

export default function CalibrationSessionsPage() {
  const [cycleId, setCycleId] = useState('');
  const [filter, setFilter] = useState<Filter>('open');
  const [createOpen, setCreateOpen] = useState(false);
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [form, setForm] = useState({
    sessionName: '',
    organizationLevelId: '',
    organizationUnitId: '',
    scheduledDate: '',
    agenda: '',
  });

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'calibration-sessions', cycleId],
    queryFn: () => calibrationSessionService.getByCycle(cycleId),
    enabled: !!cycleId,
  });

  const rows = useMemo(() => data ?? [], [data]);

  const stats = useMemo(
    () => ({
      pending: rows.filter((r) => r.status === 'Pending').length,
      running: rows.filter((r) => r.status === 'InProgress').length,
      completed: rows.filter((r) => r.status === 'Completed').length,
    }),
    [rows],
  );

  const filtered = useMemo(() => {
    switch (filter) {
      case 'open':
        return rows.filter((r) => OPEN_STATUSES.includes(r.status));
      case 'completed':
        return rows.filter((r) => r.status === 'Completed');
      default:
        return rows;
    }
  }, [rows, filter]);

  const create = useMutation({
    mutationFn: () =>
      calibrationSessionService.create({
        appraisalCycleId: cycleId,
        sessionName: form.sessionName.trim(),
        organizationLevelId: form.organizationLevelId || null,
        organizationUnitId: form.organizationUnitId || null,
        scheduledDate: form.scheduledDate || null,
        agenda: form.agenda.trim() || null,
      }),
    onSuccess: (session) => {
      toast({
        title: 'Session created',
        description: `${session.sessionName} is ready. Add the panel, then open it.`,
      });
      setCreateOpen(false);
      setForm({
        sessionName: '',
        organizationLevelId: '',
        organizationUnitId: '',
        scheduledDate: '',
        agenda: '',
      });
      queryClient.invalidateQueries({ queryKey: ['hr', 'calibration-sessions', cycleId] });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not create the session', description: e.message, variant: 'destructive' }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Calibration"
        description="Panels that reconcile managers' ratings across a unit before HR signs the appraisals off."
        backHref="/hr/performance"
        actions={
          <Button onClick={() => setCreateOpen(true)} disabled={!cycleId}>
            <Plus className="mr-2 h-4 w-4" />
            New session
          </Button>
        }
      />

      <CycleSelect value={cycleId} onChange={setCycleId} />

      <MetricTiles
        tiles={[
          { label: 'Not yet opened', value: stats.pending, icon: Scale },
          {
            label: 'In progress',
            value: stats.running,
            icon: Gavel,
            tone: stats.running > 0 ? 'warning' : 'default',
          },
          { label: 'Completed', value: stats.completed, icon: CheckCircle2, tone: 'success' },
          { label: 'Total', value: rows.length },
        ]}
      />

      <Tabs value={filter} onValueChange={(v) => setFilter(v as Filter)}>
        <TabsList>
          <TabsTrigger value="open">Open ({stats.pending + stats.running})</TabsTrigger>
          <TabsTrigger value="completed">Completed ({stats.completed})</TabsTrigger>
          <TabsTrigger value="all">All ({rows.length})</TabsTrigger>
        </TabsList>
      </Tabs>

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load calibration sessions"
              description={(error as Error)?.message ?? 'Try again in a moment.'}
            />
          ) : !cycleId ? (
            <EmptyState
              icon={Scale}
              title="Pick a cycle"
              description="Calibration sessions belong to one appraisal cycle."
            />
          ) : isLoading ? (
            <div className="space-y-2 p-4">
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={Scale}
              title="No sessions"
              description={
                filter === 'all'
                  ? 'This cycle has no calibration sessions yet.'
                  : 'Nothing matches this filter.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Session</TableHead>
                  <TableHead>Scope</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Scheduled</TableHead>
                  <TableHead>Facilitator</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((session) => (
                  <TableRow key={session.id}>
                    <TableCell>
                      <div className="font-medium">{session.sessionName}</div>
                      {session.cycleCode && (
                        <div className="text-xs text-muted-foreground">{session.cycleCode}</div>
                      )}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {describeScope(session)}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={session.status} />
                      {session.completedDate && (
                        <div className="mt-1 text-xs text-muted-foreground">
                          {formatDate(session.completedDate)}
                          {session.completedByName ? ` · ${session.completedByName}` : ''}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDate(session.scheduledDate) || '—'}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {session.facilitatedByName ?? '—'}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/hr/performance/calibration/${session.id}`}>Open</Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>New calibration session</DialogTitle>
            <DialogDescription>
              Scope decides who is on the grid. A unit covers that unit and everything beneath it;
              leave both blank and the session covers the whole cycle.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="sessionName">Session name</Label>
              <Input
                id="sessionName"
                value={form.sessionName}
                onChange={(e) => setForm({ ...form, sessionName: e.target.value })}
                placeholder="Finance — FY2026 calibration"
              />
            </div>

            <OrganizationScopeFields
              levelId={form.organizationLevelId}
              unitId={form.organizationUnitId}
              onLevelChange={(v) => setForm({ ...form, organizationLevelId: v })}
              onUnitChange={(v) => setForm({ ...form, organizationUnitId: v })}
            />

            <div className="space-y-2">
              <Label htmlFor="scheduledDate">Scheduled for</Label>
              <Input
                id="scheduledDate"
                type="date"
                value={form.scheduledDate}
                onChange={(e) => setForm({ ...form, scheduledDate: e.target.value })}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="agenda">Agenda</Label>
              <Textarea
                id="agenda"
                rows={3}
                value={form.agenda}
                onChange={(e) => setForm({ ...form, agenda: e.target.value })}
                placeholder="What the panel will work through."
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => create.mutate()}
              disabled={!form.sessionName.trim() || create.isPending}
            >
              {create.isPending ? 'Creating…' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function describeScope(session: CalibrationSession): string {
  if (session.organizationUnitName) return `${session.organizationUnitName} (and below)`;
  if (session.organizationLevelName) return `All of ${session.organizationLevelName}`;
  return 'Whole cycle';
}
