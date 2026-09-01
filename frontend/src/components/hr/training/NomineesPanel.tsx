'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Plus, MoreHorizontal, Eye, Send, Users, UserX, CalendarSearch, Loader2, UsersRound, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import type { NomineeConflict } from '@/types/hr/training-delivery';
import { NOMINATION_TYPE_OPTIONS, NOMINATION_STATUS_OPTIONS } from '@/types/hr/training-delivery';
import type { NominationType, TrainingNominationSummary, BulkNominationResult } from '@/types/hr/training-delivery';

const statusLabel = (v: string) =>
  NOMINATION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const typeLabel = (v: string) => NOMINATION_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

interface Props {
  scheduleId: string;
  readOnly?: boolean;
}

/** Who is booked onto this schedule. Nomination approval runs on the generic workflow engine. */
export function NomineesPanel({ scheduleId, readOnly }: Props) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [addOpen, setAddOpen] = useState(false);
  const [withdrawTarget, setWithdrawTarget] = useState<TrainingNominationSummary | null>(null);
  const [bulkResult, setBulkResult] = useState<BulkNominationResult | null>(null);
  const [busy, setBusy] = useState(false);
  /**
   * Bulk nomination. Finish-plan lane 4 (2026-09-01): the endpoint, the client method and the
   * RESULT DISPLAY below (created count, every skipped row with its reason) all existed, and
   * nothing could start a batch — `setBulkResult` had no caller. This dialog is the missing action:
   * a list of employees, one nomination type and one justification, submitted as one request.
   */
  const [bulkOpen, setBulkOpen] = useState(false);
  const [bulkPeople, setBulkPeople] = useState<{ id: string; name: string }[]>([]);
  const [bulkType, setBulkType] = useState<NominationType>('HR');
  const [bulkJustification, setBulkJustification] = useState('');
  const [bulkConflicts, setBulkConflicts] = useState<NomineeConflict[] | null>(null);
  const [bulkChecking, setBulkChecking] = useState(false);
  /**
   * ⚠ The availability check was built and shown to nobody, so a nominee could be booked onto a
   * course while already on leave, travelling, or booked on another one over the same dates — a
   * clash the server would have reported if anyone had asked it. TDC raised this in the demo
   * feedback as well.
   *
   * `null` means not checked; an empty array means checked and clear. The screen says which.
   */
  const [conflicts, setConflicts] = useState<NomineeConflict[] | null>(null);
  const [checking, setChecking] = useState(false);

  const queryKey = ['hr', 'training', 'schedules', scheduleId, 'nominees'];
  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => trainingNominationService.getBySchedule(scheduleId),
  });

  const form = useForm<{ employeeId: string; type: NominationType; justification: string }>({
    defaultValues: { employeeId: '', type: 'HR', justification: '' },
  });

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey }),
      // The confirmed-seat count on the schedule header moves with this list.
      queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'schedules', scheduleId] }),
    ]);

  const checkAvailability = async (employeeId: string) => {
    if (!employeeId) return;
    setChecking(true);
    try {
      setConflicts(await trainingNominationService.checkAvailability(scheduleId, [employeeId]));
    } catch (error: any) {
      toast({
        title: 'The check could not run',
        description: error?.body?.message ?? error?.message,
        variant: 'destructive',
      });
      setConflicts(null);
    } finally {
      setChecking(false);
    }
  };

  const handleAdd = form.handleSubmit(async (values) => {
    if (!values.employeeId) return;
    setBusy(true);
    try {
      await trainingNominationService.create({
        scheduleId,
        employeeId: values.employeeId,
        type: values.type,
        nominationDate: new Date().toISOString(),
        justification: values.justification || null,
      });
      await refresh();
      toast({ title: 'Nominated' });
      form.reset({ employeeId: '', type: 'HR', justification: '' });
      setConflicts(null);
      setAddOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to nominate.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  const addBulkPerson = (id: string | null, label: string | null) => {
    if (!id || bulkPeople.some((p) => p.id === id)) return;
    setBulkPeople([...bulkPeople, { id, name: label ?? 'Employee' }]);
    setBulkConflicts(null);
  };

  const checkBulkAvailability = async () => {
    if (bulkPeople.length === 0) return;
    setBulkChecking(true);
    try {
      setBulkConflicts(
        await trainingNominationService.checkAvailability(scheduleId, bulkPeople.map((p) => p.id)),
      );
    } catch (error: any) {
      toast({
        title: 'The check could not run',
        description: error?.body?.message ?? error?.message,
        variant: 'destructive',
      });
      setBulkConflicts(null);
    } finally {
      setBulkChecking(false);
    }
  };

  const handleBulk = async () => {
    if (bulkPeople.length === 0) return;
    setBusy(true);
    try {
      const result = await trainingNominationService.bulkCreate({
        scheduleId,
        employeeIds: bulkPeople.map((p) => p.id),
        type: bulkType,
        justification: bulkJustification || null,
      });
      setBulkResult(result);
      await refresh();
      toast({
        title: `${result.createdCount} of ${result.requestedCount} nominated`,
        description:
          result.skipped.length > 0
            ? `${result.skipped.length} skipped — the reasons are listed above the table.`
            : undefined,
      });
      setBulkPeople([]);
      setBulkJustification('');
      setBulkConflicts(null);
      setBulkOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.body?.message ?? error?.message ?? 'Failed to nominate.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const rows = data ?? [];

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-start justify-between gap-4">
            <div>
              <CardTitle>Nominees</CardTitle>
              <CardDescription>
                Who is booked onto this run. Approval follows the published nomination workflow.
              </CardDescription>
            </div>
            {!readOnly && (
              <div className="flex gap-2">
                <Button size="sm" variant="outline" onClick={() => setBulkOpen(true)}>
                  <UsersRound className="mr-2 h-4 w-4" /> Nominate several
                </Button>
                <Button size="sm" onClick={() => setAddOpen(true)}>
                  <Plus className="mr-2 h-4 w-4" /> Nominate
                </Button>
              </div>
            )}
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {bulkResult && (
            <Alert>
              <UserX className="h-4 w-4" />
              <AlertTitle>
                {bulkResult.createdCount} of {bulkResult.requestedCount} nominated
              </AlertTitle>
              <AlertDescription>
                {bulkResult.skipped.length === 0 ? (
                  <span>Everyone in the batch was nominated.</span>
                ) : (
                  <ul className="ml-4 list-disc">
                    {bulkResult.skipped.map((s) => (
                      <li key={s.employeeId}>{s.reason}</li>
                    ))}
                  </ul>
                )}
                <button
                  type="button"
                  className="mt-2 text-xs underline"
                  onClick={() => setBulkResult(null)}
                >
                  Dismiss
                </button>
              </AlertDescription>
            </Alert>
          )}

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Nomination</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Nominated</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(6)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={Users}
                        title="No nominees yet"
                        description="Nominate an employee onto this schedule."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((n) => (
                    <TableRow
                      key={n.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/training/nominations/${n.id}`)}
                    >
                      <TableCell className="font-mono text-xs">{n.nominationNumber}</TableCell>
                      <TableCell className="font-medium">
                        {n.employeeName}
                        <div className="text-xs text-muted-foreground">{n.employeeNumber}</div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{typeLabel(n.type)}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {new Date(n.nominationDate).toLocaleDateString()}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={statusLabel(n.status)} />
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              className="h-8 w-8 p-0"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <span className="sr-only">Open menu</span>
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                router.push(`/hr/training/nominations/${n.id}`);
                              }}
                            >
                              <Eye className="mr-2 h-4 w-4" /> View details
                            </DropdownMenuItem>
                            {n.status === 'Draft' && (
                              <DropdownMenuItem
                                onClick={async (e) => {
                                  e.stopPropagation();
                                  try {
                                    await trainingNominationService.submit(n.id);
                                    await refresh();
                                    toast({ title: 'Submitted for approval' });
                                  } catch (error: any) {
                                    toast({
                                      title: 'Error',
                                      description: error?.message || 'Failed to submit.',
                                      variant: 'destructive',
                                    });
                                  }
                                }}
                              >
                                <Send className="mr-2 h-4 w-4" /> Submit
                              </DropdownMenuItem>
                            )}
                            {n.status !== 'Withdrawn' && n.status !== 'Rejected' && (
                              <>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-destructive focus:text-destructive"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setWithdrawTarget(n);
                                  }}
                                >
                                  <UserX className="mr-2 h-4 w-4" /> Withdraw
                                </DropdownMenuItem>
                              </>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Nominate onto this schedule</DialogTitle>
            <DialogDescription>
              If the schedule is full, add the employee to the waitlist instead.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <EmployeePickerField form={form} name="employeeId" label="Employee" required />

            {/* ⚠ Advisory, not a gate. Someone may legitimately be nominated over a clash — leave
                gets cancelled, travel moves — so this informs the decision rather than blocking it.
                What it must never do is stay silent. */}
            <div className="flex items-center gap-2">
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={!form.watch('employeeId') || checking}
                onClick={() => checkAvailability(form.watch('employeeId'))}
              >
                {checking ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <CalendarSearch className="mr-2 h-4 w-4" />
                )}
                Check availability
              </Button>
              {conflicts !== null && conflicts.length === 0 && (
                <span className="text-sm text-emerald-700 dark:text-emerald-400">
                  Nothing in their diary over these dates.
                </span>
              )}
            </div>

            {conflicts !== null && conflicts.length > 0 && (
              <Alert>
                <AlertTitle>
                  {conflicts.length} clash{conflicts.length === 1 ? '' : 'es'} over these dates
                </AlertTitle>
                <AlertDescription>
                  <ul className="mt-1 space-y-1 text-sm">
                    {conflicts.map((c, i) => (
                      <li key={`${c.source}-${i}`}>
                        <span className="font-medium">{c.source}:</span> {c.description}{' '}
                        <span className="text-muted-foreground">
                          ({new Date(c.fromDate).toLocaleDateString()} –{' '}
                          {new Date(c.toDate).toLocaleDateString()})
                        </span>
                      </li>
                    ))}
                  </ul>
                  <p className="mt-2 text-xs text-muted-foreground">
                    You can still nominate them — this is a warning, not a rule.
                  </p>
                </AlertDescription>
              </Alert>
            )}

            <SelectField form={form} name="type" label="Nomination type" required options={NOMINATION_TYPE_OPTIONS} />
            <TextareaField form={form} name="justification" label="Justification" rows={3} />
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setConflicts(null);
                setAddOpen(false);
              }}
              disabled={busy}
            >
              Cancel
            </Button>
            <Button onClick={handleAdd} disabled={busy}>
              Nominate
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={bulkOpen}
        onOpenChange={(o) => {
          if (!o) setBulkConflicts(null);
          setBulkOpen(o);
        }}
      >
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>Nominate several employees</DialogTitle>
            <DialogDescription>
              One nomination per person, all with the same type and justification. Anyone already
              nominated is skipped and the reason is shown. Seats are taken at approval, so a full
              schedule refuses the approval, not the nomination — use the waitlist for the overflow.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label>Employees</Label>
              <EmployeePicker
                value={null}
                initialLabel={null}
                placeholder="Search employees to add…"
                onChange={addBulkPerson}
              />
              {bulkPeople.length > 0 && (
                <div className="flex flex-wrap gap-2 pt-1">
                  {bulkPeople.map((p) => (
                    <Badge key={p.id} variant="secondary" className="gap-1.5 py-1 pl-2.5 pr-1">
                      {p.name}
                      <button
                        type="button"
                        aria-label={`Remove ${p.name}`}
                        onClick={() => {
                          setBulkPeople(bulkPeople.filter((x) => x.id !== p.id));
                          setBulkConflicts(null);
                        }}
                        className="rounded-sm hover:bg-muted"
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    </Badge>
                  ))}
                </div>
              )}
              <p className="text-xs text-muted-foreground">
                {bulkPeople.length} selected
              </p>
            </div>

            {/* Advisory, like the single-nominee check: it informs, it does not block. */}
            <div className="flex items-center gap-2">
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={bulkPeople.length === 0 || bulkChecking}
                onClick={checkBulkAvailability}
              >
                {bulkChecking ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <CalendarSearch className="mr-2 h-4 w-4" />
                )}
                Check availability
              </Button>
              {bulkConflicts !== null && bulkConflicts.length === 0 && (
                <span className="text-sm text-emerald-700 dark:text-emerald-400">
                  Nothing in their diaries over these dates.
                </span>
              )}
            </div>
            {bulkConflicts !== null && bulkConflicts.length > 0 && (
              <Alert>
                <AlertTitle>
                  {bulkConflicts.length} clash{bulkConflicts.length === 1 ? '' : 'es'} over these dates
                </AlertTitle>
                <AlertDescription>
                  <ul className="mt-1 space-y-1 text-sm">
                    {bulkConflicts.map((c, i) => (
                      <li key={`${c.source}-${i}`}>
                        <span className="font-medium">{c.source}:</span> {c.description}{' '}
                        <span className="text-muted-foreground">
                          ({new Date(c.fromDate).toLocaleDateString()} –{' '}
                          {new Date(c.toDate).toLocaleDateString()})
                        </span>
                      </li>
                    ))}
                  </ul>
                  <p className="mt-2 text-xs text-muted-foreground">
                    You can still nominate them — this is a warning, not a rule.
                  </p>
                </AlertDescription>
              </Alert>
            )}

            <div className="space-y-2">
              <Label>Nomination type</Label>
              <Select value={bulkType} onValueChange={(v) => setBulkType(v as NominationType)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {NOMINATION_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="bulkJustification">Justification</Label>
              <Textarea
                id="bulkJustification"
                rows={3}
                value={bulkJustification}
                onChange={(e) => setBulkJustification(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setBulkConflicts(null);
                setBulkOpen(false);
              }}
              disabled={busy}
            >
              Cancel
            </Button>
            <Button onClick={handleBulk} disabled={busy || bulkPeople.length === 0}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Nominate {bulkPeople.length > 0 ? bulkPeople.length : ''}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={withdrawTarget !== null}
        onOpenChange={(o) => !o && setWithdrawTarget(null)}
        title="Withdraw nomination"
        description={
          withdrawTarget
            ? `Withdraw ${withdrawTarget.employeeName} from this schedule? This frees their seat.`
            : ''
        }
        confirmText="Withdraw"
        variant="destructive"
        onConfirm={async () => {
          if (!withdrawTarget) return false;
          try {
            await trainingNominationService.withdraw(withdrawTarget.id);
            await refresh();
            toast({ title: 'Withdrawn' });
            setWithdrawTarget(null);
            return true;
          } catch (error: any) {
            toast({
              title: 'Error',
              description: error?.message || 'Failed to withdraw.',
              variant: 'destructive',
            });
            return false;
          }
        }}
      />
    </>
  );
}
