'use client';

import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CalendarClock,
  CheckCircle2,
  CircleAlert,
  Clock3,
  History,
  Loader2,
  Play,
  Plus,
  RefreshCw,
  Settings2,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
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
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  procurementCalendarOccurrenceActions,
  procurementCalendarOpenStatuses,
  procurementCalendarProfileActions,
  validateProcurementCalendarProfile,
} from '@/lib/procurement-calendar';
import { procurementCalendarService } from '@/services/procurement-calendar.service';
import type {
  ProcurementCalendarEventType,
  ProcurementCalendarOccurrence,
  ProcurementCalendarProfile,
  ProcurementCalendarRuleRequest,
  SaveProcurementCalendarProfile,
} from '@/types/procurement-calendar';

const events: Array<[ProcurementCalendarEventType, string, number, number]> = [
  ['AppPreparation', 'APP preparation', 9, 1],
  ['AppSubmission', 'APP submission', 11, 30],
  ['MidYearReview', 'Mid-year procurement review', 7, 15],
  ['CycleCount', 'Inventory cycle count', 3, 31],
  ['YearEndClose', 'Procurement year-end close', 12, 31],
  ['Renewal', 'Annual renewal review', 1, 31],
  ['GhanepsDeadline', 'GHANEPS filing deadline', 12, 15],
];
const ownerRoles = [
  'TDC_PROCUREMENT_OFFICER',
  'TDC_SENIOR_PROCUREMENT_OFFICER',
  'TDC_HEAD_OF_PROCUREMENT',
  'TDC_STORES_MANAGER',
  'TDC_FINANCE_REVIEWER',
  'TDC_MANAGING_DIRECTOR',
];
const formatDate = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';
const dateInput = (value?: string) => (value ? value.slice(0, 10) : '');
const isoDate = (value: string) =>
  new Date(`${value}T00:00:00.000Z`).toISOString();
const initialRules = (): ProcurementCalendarRuleRequest[] =>
  events.map(([eventType, title, dueMonth, dueDay]) => ({
    eventType,
    title,
    dueMonth,
    dueDay,
    dueLocalTime: '09:00:00',
    reminderLeadDays: 14,
    escalationAfterDays: 1,
    ownerRoleName: 'TDC_PROCUREMENT_OFFICER',
    escalationRoleName: 'TDC_HEAD_OF_PROCUREMENT',
    statutoryReference: '',
    isEnabled: true,
  }));

type LifecycleAction =
  | {
      kind: 'publish' | 'retire' | 'delete';
      profile: ProcurementCalendarProfile;
    }
  | { kind: 'clone'; profile: ProcurementCalendarProfile }
  | {
      kind: 'acknowledge' | 'complete' | 'cancel';
      occurrence: ProcurementCalendarOccurrence;
    }
  | { kind: 'run' };

export default function ProcurementAnnualCalendarPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [taskStatus, setTaskStatus] = useState('open');
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<ProcurementCalendarProfile>();
  const [saving, setSaving] = useState(false);
  const [action, setAction] = useState<LifecycleAction>();
  const [actionReason, setActionReason] = useState('');
  const [approvalReference, setApprovalReference] = useState('');
  const [effectiveFrom, setEffectiveFrom] = useState('');
  const [horizonDays, setHorizonDays] = useState('365');
  const [form, setForm] = useState<SaveProcurementCalendarProfile>({
    profileCode: 'TDC-ANNUAL-CALENDAR',
    name: 'TDC annual procurement calendar',
    description:
      'Controlled annual APP, review, inventory, renewal, year-end, and GHANEPS obligations.',
    timeZoneId: 'Greenwich Standard Time',
    generationHorizonDays: 365,
    catchUpDays: 30,
    effectiveFromUtc: isoDate(new Date().toISOString().slice(0, 10)),
    changeSummary: '',
    rules: initialRules(),
  });
  const canManage = hasPermission('procurement.calendar.manage');
  const canRun = hasPermission('procurement.calendar.run');

  const summary = useQuery({
    queryKey: ['procurement-calendar-summary'],
    queryFn: procurementCalendarService.summary,
  });
  const profiles = useQuery({
    queryKey: ['procurement-calendar-profiles'],
    queryFn: procurementCalendarService.profiles,
  });
  const timeZones = useQuery({
    queryKey: ['procurement-calendar-time-zones'],
    queryFn: procurementCalendarService.timeZones,
  });
  const occurrences = useQuery({
    queryKey: ['procurement-calendar-occurrences', taskStatus],
    queryFn: () =>
      procurementCalendarService.occurrences({
        page: 1,
        pageSize: 100,
        status:
          taskStatus === 'open' || taskStatus === 'all'
            ? undefined
            : taskStatus,
      }),
  });
  const runs = useQuery({
    queryKey: ['procurement-calendar-runs'],
    queryFn: procurementCalendarService.runs,
  });
  const visibleTasks = useMemo(
    () =>
      (occurrences.data?.items ?? []).filter((item) =>
        taskStatus === 'open'
          ? procurementCalendarOpenStatuses.has(item.status)
          : true
      ),
    [occurrences.data?.items, taskStatus]
  );
  const loading =
    summary.isLoading ||
    profiles.isLoading ||
    occurrences.isLoading ||
    runs.isLoading;
  const loadError =
    summary.isError || profiles.isError || occurrences.isError || runs.isError;

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['procurement-calendar-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-calendar-profiles'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-calendar-occurrences'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-calendar-runs'],
      }),
    ]);
  };

  const openNew = () => {
    setEditing(undefined);
    setForm({
      profileCode: 'TDC-ANNUAL-CALENDAR',
      name: 'TDC annual procurement calendar',
      description:
        'Controlled annual APP, review, inventory, renewal, year-end, and GHANEPS obligations.',
      timeZoneId: timeZones.data?.includes('Greenwich Standard Time')
        ? 'Greenwich Standard Time'
        : (timeZones.data?.[0] ?? 'UTC'),
      generationHorizonDays: 365,
      catchUpDays: 30,
      effectiveFromUtc: isoDate(new Date().toISOString().slice(0, 10)),
      changeSummary: '',
      rules: initialRules(),
    });
    setEditorOpen(true);
  };

  const openEdit = (profile: ProcurementCalendarProfile) => {
    setEditing(profile);
    setForm({
      profileCode: profile.profileCode,
      name: profile.name,
      description: profile.description,
      timeZoneId: profile.timeZoneId,
      generationHorizonDays: profile.generationHorizonDays,
      catchUpDays: profile.catchUpDays,
      effectiveFromUtc: profile.effectiveFromUtc,
      effectiveToUtc: profile.effectiveToUtc,
      changeSummary: profile.changeSummary ?? '',
      rowVersion: profile.rowVersion,
      rules: profile.rules.map((rule) => ({ ...rule })),
    });
    setEditorOpen(true);
  };

  const saveProfile = async () => {
    const validation = validateProcurementCalendarProfile(form);
    if (validation) {
      toast.error(validation);
      return;
    }
    setSaving(true);
    try {
      if (editing)
        await procurementCalendarService.updateProfile(editing.id, form);
      else await procurementCalendarService.createProfile(form);
      toast.success(
        editing ? 'Calendar Draft updated.' : 'Calendar Draft created.'
      );
      setEditorOpen(false);
      await refresh();
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Calendar Draft could not be saved.'
      );
    } finally {
      setSaving(false);
    }
  };

  const openAction = (next: LifecycleAction) => {
    setAction(next);
    setActionReason('');
    setApprovalReference('');
    setEffectiveFrom(new Date().toISOString().slice(0, 10));
    setHorizonDays('365');
  };

  const executeAction = async () => {
    if (!action || !actionReason.trim()) return;
    setSaving(true);
    try {
      if (action.kind === 'publish') {
        if (!approvalReference.trim())
          throw new Error('Approval or evidence reference is required.');
        await procurementCalendarService.publishProfile(action.profile.id, {
          rowVersion: action.profile.rowVersion,
          reason: actionReason,
          approvalReference,
        });
      } else if (action.kind === 'retire') {
        await procurementCalendarService.retireProfile(action.profile.id, {
          rowVersion: action.profile.rowVersion,
          reason: actionReason,
        });
      } else if (action.kind === 'delete') {
        await procurementCalendarService.deleteDraft(action.profile.id, {
          rowVersion: action.profile.rowVersion,
          reason: actionReason,
        });
      } else if (action.kind === 'clone') {
        await procurementCalendarService.cloneProfile(action.profile.id, {
          changeSummary: actionReason,
          effectiveFromUtc: isoDate(effectiveFrom),
        });
      } else if (action.kind === 'acknowledge') {
        await procurementCalendarService.acknowledge(
          action.occurrence.id,
          action.occurrence.rowVersion,
          actionReason
        );
      } else if (action.kind === 'complete') {
        await procurementCalendarService.complete(
          action.occurrence.id,
          action.occurrence.rowVersion,
          actionReason
        );
      } else if (action.kind === 'cancel') {
        await procurementCalendarService.cancel(
          action.occurrence.id,
          action.occurrence.rowVersion,
          actionReason
        );
      } else {
        await procurementCalendarService.run(actionReason, Number(horizonDays));
      }
      toast.success('Procurement calendar action completed.');
      setAction(undefined);
      await refresh();
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Calendar action failed.'
      );
    } finally {
      setSaving(false);
    }
  };

  const updateRule = (
    index: number,
    patch: Partial<ProcurementCalendarRuleRequest>
  ) =>
    setForm((current) => ({
      ...current,
      rules: current.rules.map((rule, ruleIndex) =>
        ruleIndex === index ? { ...rule, ...patch } : rule
      ),
    }));

  const cards = [
    ['Open tasks', summary.data?.openTasks ?? 0, CalendarClock],
    ['Due', summary.data?.dueTasks ?? 0, Clock3],
    ['Escalated', summary.data?.escalatedTasks ?? 0, AlertTriangle],
    ['Completed', summary.data?.completedTasks ?? 0, CheckCircle2],
    ['Published profiles', summary.data?.publishedProfiles ?? 0, Settings2],
  ] as const;

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <h1 className="text-3xl font-bold">Annual procurement calendar</h1>
          <p className="mt-1 max-w-4xl text-muted-foreground">
            Owned APP, mid-year review, cycle-count, year-end, renewal, and
            GHANEPS tasks with effective dates, reminders, acknowledgements,
            completion, and escalation history.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            variant="outline"
            onClick={() => void refresh()}
            disabled={loading}
          >
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
          {(canManage || canRun) && (
            <>
              {canRun && (
                <Button
                  variant="outline"
                  onClick={() => openAction({ kind: 'run' })}
                >
                  <Play className="mr-2 h-4 w-4" /> Run now
                </Button>
              )}
              {canManage && (
                <Button onClick={openNew}>
                  <Plus className="mr-2 h-4 w-4" /> New Draft
                </Button>
              )}
            </>
          )}
        </div>
      </div>

      {loadError && (
        <Alert variant="destructive">
          <CircleAlert className="h-4 w-4" />
          <AlertTitle>Calendar could not be loaded</AlertTitle>
          <AlertDescription>
            Check the tenant session and calendar permission, then retry.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {cards.map(([label, value, Icon]) => (
          <Card key={label}>
            <CardContent className="flex items-center justify-between p-5">
              <div>
                <p className="text-sm text-muted-foreground">{label}</p>
                <p className="mt-1 text-2xl font-semibold">{value}</p>
              </div>
              <Icon className="h-5 w-5 text-muted-foreground" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Tabs defaultValue="tasks" className="space-y-4">
        <TabsList>
          <TabsTrigger value="tasks">
            <CalendarClock className="mr-2 h-4 w-4" /> Tasks
          </TabsTrigger>
          <TabsTrigger value="profiles">
            <Settings2 className="mr-2 h-4 w-4" /> Profiles
          </TabsTrigger>
          <TabsTrigger value="runs">
            <History className="mr-2 h-4 w-4" /> Run history
          </TabsTrigger>
        </TabsList>

        <TabsContent value="tasks" className="space-y-3">
          <div className="flex items-center justify-between gap-3">
            <p className="text-sm text-muted-foreground">
              Operational tasks are shown first; configuration remains in the
              Profiles tab.
            </p>
            <Select value={taskStatus} onValueChange={setTaskStatus}>
              <SelectTrigger className="w-44">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="open">Open tasks</SelectItem>
                <SelectItem value="all">All tasks</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
                <SelectItem value="Escalated">Escalated</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Task</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead>Owner</TableHead>
                  <TableHead>Escalation</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {visibleTasks.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell>
                      <div className="font-medium">{item.title}</div>
                      <div className="max-w-80 truncate text-xs text-muted-foreground">
                        {item.ruleCode} · {item.statutoryReference}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{formatDate(item.dueAtUtc)}</div>
                      <div className="text-xs text-muted-foreground">
                        {item.timeZoneId}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{item.ownerName}</div>
                      <div className="text-xs text-muted-foreground">
                        {item.ownerRoleName}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{item.escalationOwnerName}</div>
                      <div className="text-xs text-muted-foreground">
                        {item.escalationRoleName}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={
                          item.status === 'Escalated'
                            ? 'destructive'
                            : item.status === 'Completed'
                              ? 'default'
                              : 'outline'
                        }
                      >
                        {item.status}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      {procurementCalendarOccurrenceActions(item, canManage)
                        .isOpen && (
                        <div className="flex justify-end gap-1">
                          {procurementCalendarOccurrenceActions(item, canManage)
                            .canAcknowledge && (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() =>
                                openAction({
                                  kind: 'acknowledge',
                                  occurrence: item,
                                })
                              }
                            >
                              Acknowledge
                            </Button>
                          )}
                          {procurementCalendarOccurrenceActions(item, canManage)
                            .canComplete && (
                            <Button
                              size="sm"
                              onClick={() =>
                                openAction({
                                  kind: 'complete',
                                  occurrence: item,
                                })
                              }
                            >
                              Complete
                            </Button>
                          )}
                          {procurementCalendarOccurrenceActions(item, canManage)
                            .canCancel && (
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() =>
                                openAction({ kind: 'cancel', occurrence: item })
                              }
                            >
                              Cancel
                            </Button>
                          )}
                        </div>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
                {!occurrences.isLoading && visibleTasks.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="h-28 text-center text-muted-foreground"
                    >
                      No calendar tasks match this view. An approved Published
                      profile is required before generation.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </TabsContent>

        <TabsContent value="profiles">
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Profile</TableHead>
                  <TableHead>Effective period</TableHead>
                  <TableHead>Time zone / horizon</TableHead>
                  <TableHead>Coverage</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(profiles.data ?? []).map((profile) => (
                  <TableRow key={profile.id}>
                    <TableCell>
                      <div className="font-medium">{profile.name}</div>
                      <div className="text-xs text-muted-foreground">
                        {profile.profileCode} · v{profile.version}
                      </div>
                    </TableCell>
                    <TableCell>
                      {formatDate(profile.effectiveFromUtc)}
                      <div className="text-xs text-muted-foreground">
                        to {formatDate(profile.effectiveToUtc)}
                      </div>
                    </TableCell>
                    <TableCell>
                      {profile.timeZoneId}
                      <div className="text-xs text-muted-foreground">
                        {profile.catchUpDays}d catch-up ·{' '}
                        {profile.generationHorizonDays}d horizon
                      </div>
                    </TableCell>
                    <TableCell>
                      {profile.rules.filter((rule) => rule.isEnabled).length}/7
                      obligations
                      <div className="text-xs text-muted-foreground">
                        {profile.approvalReference ?? 'Approval not recorded'}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={
                          profile.status === 'Published' ? 'default' : 'outline'
                        }
                      >
                        {profile.status}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        {canManage &&
                          procurementCalendarProfileActions(profile)
                            .canEdit && (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => openEdit(profile)}
                            >
                              Edit
                            </Button>
                          )}
                        {canManage &&
                          procurementCalendarProfileActions(profile)
                            .canPublish && (
                            <Button
                              size="sm"
                              onClick={() =>
                                openAction({ kind: 'publish', profile })
                              }
                            >
                              Publish
                            </Button>
                          )}
                        {canManage &&
                          procurementCalendarProfileActions(profile)
                            .canDelete && (
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() =>
                                openAction({ kind: 'delete', profile })
                              }
                            >
                              Delete
                            </Button>
                          )}
                        {canManage &&
                          procurementCalendarProfileActions(profile)
                            .canClone && (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() =>
                                openAction({ kind: 'clone', profile })
                              }
                            >
                              Clone Draft
                            </Button>
                          )}
                        {canManage &&
                          procurementCalendarProfileActions(profile)
                            .canRetire && (
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() =>
                                openAction({ kind: 'retire', profile })
                              }
                            >
                              Retire
                            </Button>
                          )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {!profiles.isLoading && (profiles.data?.length ?? 0) === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="h-28 text-center text-muted-foreground"
                    >
                      No calendar profile exists. Create a Draft; no statutory
                      date is active until an independent authorized user
                      publishes it.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </TabsContent>

        <TabsContent value="runs">
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Run</TableHead>
                  <TableHead>Window</TableHead>
                  <TableHead>Generation</TableHead>
                  <TableHead>Delivery</TableHead>
                  <TableHead>Result</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(runs.data ?? []).map((run) => (
                  <TableRow key={run.id}>
                    <TableCell>
                      <div className="font-medium">
                        {run.trigger} · attempt {run.attemptCount}
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {formatDate(run.startedAtUtc)} · {run.requestedByName}
                      </div>
                    </TableCell>
                    <TableCell>
                      {formatDate(run.windowStartUtc)}
                      <div className="text-xs text-muted-foreground">
                        to {formatDate(run.windowEndUtc)}
                      </div>
                    </TableCell>
                    <TableCell>
                      {run.createdCount} created · {run.rescheduledCount}{' '}
                      rescheduled
                      <div className="text-xs text-muted-foreground">
                        {run.profilesEvaluated} profiles · {run.rulesEvaluated}{' '}
                        rules
                      </div>
                    </TableCell>
                    <TableCell>
                      {run.reminderCount} reminders · {run.dueCount} due
                      <div className="text-xs text-muted-foreground">
                        {run.escalationCount} escalations
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={
                          run.status === 'Failed' ? 'destructive' : 'outline'
                        }
                      >
                        {run.status}
                      </Badge>
                      {run.errorSummary && (
                        <div className="mt-1 max-w-72 text-xs text-destructive">
                          {run.errorSummary}
                        </div>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
                {!runs.isLoading && (runs.data?.length ?? 0) === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={5}
                      className="h-28 text-center text-muted-foreground"
                    >
                      No generation run has been recorded.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </TabsContent>
      </Tabs>

      <Dialog open={editorOpen} onOpenChange={setEditorOpen}>
        <DialogContent className="max-h-[92vh] max-w-7xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {editing
                ? `Edit ${editing.profileCode} v${editing.version}`
                : 'Create calendar Draft'}
            </DialogTitle>
            <DialogDescription>
              Dates remain inactive until an independent authorized publisher
              supplies approved lineage. All times are interpreted in the
              selected tenant calendar time zone.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <div className="space-y-2">
              <Label>Profile code</Label>
              <Input
                value={form.profileCode}
                disabled={Boolean(editing)}
                onChange={(event) =>
                  setForm({ ...form, profileCode: event.target.value })
                }
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Name</Label>
              <Input
                value={form.name}
                onChange={(event) =>
                  setForm({ ...form, name: event.target.value })
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Time zone</Label>
              <Select
                value={form.timeZoneId}
                onValueChange={(timeZoneId) => setForm({ ...form, timeZoneId })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(timeZones.data ?? [form.timeZoneId]).map((zone) => (
                    <SelectItem key={zone} value={zone}>
                      {zone}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Effective from</Label>
              <Input
                type="date"
                value={dateInput(form.effectiveFromUtc)}
                onChange={(event) =>
                  setForm({
                    ...form,
                    effectiveFromUtc: isoDate(event.target.value),
                  })
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Effective to (optional)</Label>
              <Input
                type="date"
                value={dateInput(form.effectiveToUtc)}
                onChange={(event) =>
                  setForm({
                    ...form,
                    effectiveToUtc: event.target.value
                      ? isoDate(event.target.value)
                      : undefined,
                  })
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Catch-up days</Label>
              <Input
                type="number"
                min={0}
                max={365}
                value={form.catchUpDays}
                onChange={(event) =>
                  setForm({ ...form, catchUpDays: Number(event.target.value) })
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Generation horizon</Label>
              <Input
                type="number"
                min={1}
                max={730}
                value={form.generationHorizonDays}
                onChange={(event) =>
                  setForm({
                    ...form,
                    generationHorizonDays: Number(event.target.value),
                  })
                }
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Description</Label>
              <Textarea
                value={form.description ?? ''}
                onChange={(event) =>
                  setForm({ ...form, description: event.target.value })
                }
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Change summary</Label>
              <Textarea
                value={form.changeSummary}
                onChange={(event) =>
                  setForm({ ...form, changeSummary: event.target.value })
                }
              />
            </div>
          </div>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Obligation</TableHead>
                  <TableHead>Date / time</TableHead>
                  <TableHead>Reminder / escalation</TableHead>
                  <TableHead>Owner</TableHead>
                  <TableHead>Escalates to</TableHead>
                  <TableHead>Approved source/reference</TableHead>
                  <TableHead>Enabled</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {form.rules.map((rule, index) => (
                  <TableRow key={rule.eventType}>
                    <TableCell>
                      <div className="font-medium">{rule.title}</div>
                      <div className="text-xs text-muted-foreground">
                        {rule.eventType}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="flex min-w-48 gap-1">
                        <Input
                          aria-label={`${rule.eventType} month`}
                          type="number"
                          min={1}
                          max={12}
                          value={rule.dueMonth}
                          onChange={(event) =>
                            updateRule(index, {
                              dueMonth: Number(event.target.value),
                            })
                          }
                        />
                        <Input
                          aria-label={`${rule.eventType} day`}
                          type="number"
                          min={1}
                          max={31}
                          value={rule.dueDay}
                          onChange={(event) =>
                            updateRule(index, {
                              dueDay: Number(event.target.value),
                            })
                          }
                        />
                        <Input
                          aria-label={`${rule.eventType} time`}
                          type="time"
                          value={rule.dueLocalTime.slice(0, 5)}
                          onChange={(event) =>
                            updateRule(index, {
                              dueLocalTime: `${event.target.value}:00`,
                            })
                          }
                        />
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="flex min-w-36 gap-1">
                        <Input
                          aria-label={`${rule.eventType} reminder days`}
                          type="number"
                          min={0}
                          max={365}
                          value={rule.reminderLeadDays}
                          onChange={(event) =>
                            updateRule(index, {
                              reminderLeadDays: Number(event.target.value),
                            })
                          }
                        />
                        <Input
                          aria-label={`${rule.eventType} escalation days`}
                          type="number"
                          min={0}
                          max={365}
                          value={rule.escalationAfterDays}
                          onChange={(event) =>
                            updateRule(index, {
                              escalationAfterDays: Number(event.target.value),
                            })
                          }
                        />
                      </div>
                    </TableCell>
                    <TableCell>
                      <Select
                        value={rule.ownerRoleName}
                        onValueChange={(ownerRoleName) =>
                          updateRule(index, { ownerRoleName })
                        }
                      >
                        <SelectTrigger className="min-w-56">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {ownerRoles.map((role) => (
                            <SelectItem key={role} value={role}>
                              {role}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </TableCell>
                    <TableCell>
                      <Select
                        value={rule.escalationRoleName}
                        onValueChange={(escalationRoleName) =>
                          updateRule(index, { escalationRoleName })
                        }
                      >
                        <SelectTrigger className="min-w-56">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {ownerRoles.map((role) => (
                            <SelectItem key={role} value={role}>
                              {role}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </TableCell>
                    <TableCell>
                      <Input
                        className="min-w-64"
                        value={rule.statutoryReference}
                        placeholder="Decision, PPA, GHANEPS, or approved TDC reference"
                        onChange={(event) =>
                          updateRule(index, {
                            statutoryReference: event.target.value,
                          })
                        }
                      />
                    </TableCell>
                    <TableCell>
                      <Switch
                        checked={rule.isEnabled}
                        onCheckedChange={(isEnabled) =>
                          updateRule(index, { isEnabled })
                        }
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditorOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => void saveProfile()} disabled={saving}>
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save
              Draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(action)}
        onOpenChange={(open) => !open && setAction(undefined)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {action
                ? action.kind[0].toUpperCase() + action.kind.slice(1)
                : 'Calendar action'}
            </DialogTitle>
            <DialogDescription>
              Every lifecycle and task action is tenant-scoped, concurrency
              checked, and appended to the immutable procurement control-event
              history.
            </DialogDescription>
          </DialogHeader>
          {action?.kind === 'publish' && (
            <div className="space-y-2">
              <Label>Approval / evidence reference</Label>
              <Input
                value={approvalReference}
                onChange={(event) => setApprovalReference(event.target.value)}
                placeholder="Approved workflow, decision, or evidence reference"
              />
            </div>
          )}
          {action?.kind === 'clone' && (
            <div className="space-y-2">
              <Label>Replacement effective from</Label>
              <Input
                type="date"
                value={effectiveFrom}
                onChange={(event) => setEffectiveFrom(event.target.value)}
              />
            </div>
          )}
          {action?.kind === 'run' && (
            <div className="space-y-2">
              <Label>Generation horizon days</Label>
              <Input
                type="number"
                min={1}
                max={730}
                value={horizonDays}
                onChange={(event) => setHorizonDays(event.target.value)}
              />
            </div>
          )}
          <div className="space-y-2">
            <Label>Reason</Label>
            <Textarea
              value={actionReason}
              onChange={(event) => setActionReason(event.target.value)}
              placeholder="Required audited reason"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAction(undefined)}>
              Cancel
            </Button>
            <Button
              onClick={() => void executeAction()}
              disabled={saving || !actionReason.trim()}
            >
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
