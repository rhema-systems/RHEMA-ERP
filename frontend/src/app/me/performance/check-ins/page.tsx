'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import { CalendarPlus, MessagesSquare, TriangleAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { checkInService } from '@/services/hr/appraisal-run.service';
import { employeeService } from '@/services/hr/employee.service';
import { CHECK_IN_TYPE_OPTIONS, type CheckInType } from '@/types/hr/appraisal-run';

/**
 * Check-ins — the one-to-ones and interim conversations held during a cycle.
 *
 * Two lists rather than one, because "check-ins about me" and "check-ins I run" are different
 * jobs: the first is a record of conversations I have had, the second is a queue of ones I
 * still need to hold. Both come from `/me` routes, so neither needs an employee id.
 *
 * Area 25 slice 5: re-homed into the portal. The schedule dialog picks from the caller's
 * OWN direct reports (`manager/me/direct-reports`, self-armed) — the desk-wide employee
 * search needs a permission a plain manager may not hold, and a check-in you run is with
 * one of your own people anyway.
 *
 * ⚠ Creating one is refused with 422 when the cycle's settings profile has check-ins switched
 * off — that is a policy decision on the cycle, not a permission problem.
 */
/** Tells the server "resolve this from my token" on the fields it fills in for us. */
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

export default function CheckInsPage() {
  const [scope, setScope] = useState<'mine' | 'conducting'>('conducting');
  const [cycleId, setCycleId] = useState('');
  const [createOpen, setCreateOpen] = useState(false);

  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [form, setForm] = useState({
    employeeId: null as string | null,
    checkInType: 'OneOnOne' as CheckInType,
    title: '',
    scheduledDate: '',
    agenda: '',
  });

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'check-ins', scope, cycleId],
    queryFn: () =>
      scope === 'mine'
        ? checkInService.getMine(cycleId || undefined)
        : checkInService.getMineAsConductor(cycleId || undefined),
  });

  // Only while scheduling: the people a check-in of mine can be with.
  const { data: reports } = useQuery({
    queryKey: ['me', 'direct-reports'],
    queryFn: () => employeeService.getMyDirectReports(),
    enabled: createOpen,
  });

  const create = useMutation({
    mutationFn: () =>
      checkInService.create({
        appraisalCycleId: cycleId,
        employeeId: form.employeeId ?? '',
        // Left empty on purpose: the server fills it from the token, since whoever schedules a
        // check-in from this screen is the one holding it and the client has no employee id.
        conductedById: EMPTY_GUID,
        checkInType: form.checkInType,
        title: form.title.trim(),
        scheduledDate: new Date(form.scheduledDate).toISOString(),
        agenda: form.agenda.trim() || null,
      }),
    onSuccess: () => {
      toast({ title: 'Check-in scheduled' });
      setCreateOpen(false);
      setForm({
        employeeId: null,
        checkInType: 'OneOnOne',
        title: '',
        scheduledDate: '',
        agenda: '',
      });
      queryClient.invalidateQueries({ queryKey: ['hr', 'check-ins'] });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not schedule', description: e.message, variant: 'destructive' }),
  });

  const rows = data ?? [];
  const canCreate = Boolean(cycleId && form.employeeId && form.title.trim() && form.scheduledDate);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Check-ins"
        description="One-to-ones and interim conversations held during a cycle, and the goal updates that come out of them."
        backHref="/me"
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <CalendarPlus className="mr-2 h-4 w-4" />
            Schedule
          </Button>
        }
      />

      <CycleSelect value={cycleId} onChange={setCycleId} allowAll />

      <Tabs value={scope} onValueChange={(v) => setScope(v as typeof scope)}>
        <TabsList>
          <TabsTrigger value="conducting">Check-ins I run</TabsTrigger>
          <TabsTrigger value="mine">Check-ins about me</TabsTrigger>
        </TabsList>
      </Tabs>

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load check-ins"
              description={
                (error as Error)?.message ??
                'Your account may not be linked to an employee record.'
              }
            />
          ) : isLoading ? (
            <div className="space-y-2 p-4">
              {[0, 1].map((i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={MessagesSquare}
              title="No check-ins"
              description={
                scope === 'conducting'
                  ? 'Schedule a one-to-one to record what was discussed and update goals from it.'
                  : 'Check-ins your manager holds with you appear here.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Check-in</TableHead>
                  <TableHead>{scope === 'conducting' ? 'With' : 'Held by'}</TableHead>
                  <TableHead>Scheduled</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.id}>
                    <TableCell>
                      <div className="font-medium">{row.title}</div>
                      <div className="text-xs text-muted-foreground">
                        {humanizeEnum(row.checkInType)}
                        {row.cycleCode ? ` · ${row.cycleCode}` : ''}
                      </div>
                    </TableCell>
                    <TableCell className="text-sm">
                      {scope === 'conducting' ? row.employeeName : row.conductedByName}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDateTime(row.scheduledDate)}
                    </TableCell>
                    <TableCell>
                      {row.conductedDate ? (
                        <Badge variant="default">Held {formatDate(row.conductedDate)}</Badge>
                      ) : (
                        <Badge variant="secondary">Scheduled</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/me/performance/check-ins/${row.id}`}>Open</Link>
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
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Schedule a check-in</DialogTitle>
            <DialogDescription>
              Check-ins belong to a cycle, so goal updates recorded in one land against that
              cycle&apos;s goals.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            {!cycleId && (
              <p className="text-sm text-amber-600 dark:text-amber-500">
                Choose an appraisal cycle above first.
              </p>
            )}
            <div className="space-y-2">
              <Label>Employee</Label>
              <Select
                value={form.employeeId ?? ''}
                onValueChange={(id) => setForm((p) => ({ ...p, employeeId: id || null }))}
              >
                <SelectTrigger>
                  <SelectValue
                    placeholder={
                      reports?.length ? 'Choose one of your reports' : 'No direct reports found'
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {(reports ?? []).map((r) => (
                    <SelectItem key={r.id} value={r.id}>
                      {r.firstName} {r.lastName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {reports && reports.length === 0 && (
                <p className="text-xs text-muted-foreground">
                  Nobody reports to you on the HR record, so there is nobody to schedule with.
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="ci-title">Title</Label>
              <Input
                id="ci-title"
                maxLength={300}
                value={form.title}
                onChange={(e) => setForm((p) => ({ ...p, title: e.target.value }))}
                placeholder="Q2 one-to-one"
              />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="ci-type">Type</Label>
                <Select
                  value={form.checkInType}
                  onValueChange={(v) => setForm((p) => ({ ...p, checkInType: v as CheckInType }))}
                >
                  <SelectTrigger id="ci-type">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CHECK_IN_TYPE_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="ci-date">Scheduled for</Label>
                <Input
                  id="ci-date"
                  type="datetime-local"
                  value={form.scheduledDate}
                  onChange={(e) => setForm((p) => ({ ...p, scheduledDate: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="ci-agenda">Agenda</Label>
              <Textarea
                id="ci-agenda"
                rows={3}
                maxLength={2000}
                value={form.agenda}
                onChange={(e) => setForm((p) => ({ ...p, agenda: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => create.mutate()} disabled={!canCreate || create.isPending}>
              Schedule
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
