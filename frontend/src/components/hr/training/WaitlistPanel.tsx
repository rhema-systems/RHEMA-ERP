'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, MoreHorizontal, Trash2, Send, ThumbsUp, ThumbsDown, UserPlus, ListOrdered } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
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
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { DateField } from '@/components/hr/employee/tabs/fields';
import { useForm } from 'react-hook-form';
import { trainingWaitlistService } from '@/services/hr/training-waitlist.service';
import { TRAINING_WAITLIST_STATUS_OPTIONS } from '@/types/hr/training-delivery';
import type { TrainingWaitlistEntry } from '@/types/hr/training-delivery';

const statusLabel = (v: string) =>
  TRAINING_WAITLIST_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

const today = () => new Date().toISOString().slice(0, 10);
const plusDays = (n: number) => new Date(Date.now() + n * 86400000).toISOString().slice(0, 10);

interface Props {
  scheduleId: string;
  /** Disables the actions that change the queue once the schedule is finished. */
  readOnly?: boolean;
}

/**
 * The queue for a full schedule.
 *
 * The lifecycle is deliberately three steps — HR **offers** a freed seat, the employee **responds**,
 * HR then **enrols** them. Acceptance alone does not create a nomination: enrolment commits a seat
 * and budget, so it needs a named HR actor and a fresh capacity check. The Enrol action is what
 * produces the nomination and fills the "Nomination" column.
 */
export function WaitlistPanel({ scheduleId, readOnly }: Props) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [addOpen, setAddOpen] = useState(false);
  const [offerTarget, setOfferTarget] = useState<TrainingWaitlistEntry | null>(null);
  const [removeTarget, setRemoveTarget] = useState<TrainingWaitlistEntry | null>(null);
  const [promoteTarget, setPromoteTarget] = useState<TrainingWaitlistEntry | null>(null);
  const [busy, setBusy] = useState(false);

  const queryKey = ['hr', 'training', 'schedules', scheduleId, 'waitlist'];
  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => trainingWaitlistService.getBySchedule(scheduleId),
  });

  const addForm = useForm<{ employeeId: string }>({ defaultValues: { employeeId: '' } });
  const offerForm = useForm<{ offerDate: string; offerExpiryDate: string }>({
    defaultValues: { offerDate: today(), offerExpiryDate: plusDays(3) },
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey });

  const run = async (label: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await refresh();
      toast({ title: label });
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || `${label} failed.`, variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const handleAdd = addForm.handleSubmit(async (values) => {
    if (!values.employeeId) return;
    const okDone = await run('Added to waitlist', () =>
      trainingWaitlistService.add({ scheduleId, employeeId: values.employeeId }),
    );
    if (okDone) {
      addForm.reset({ employeeId: '' });
      setAddOpen(false);
    }
  });

  const handleOffer = offerForm.handleSubmit(async (values) => {
    if (!offerTarget) return;
    const okDone = await run('Slot offered', () =>
      trainingWaitlistService.offerSlot(offerTarget.id, {
        offerDate: new Date(values.offerDate).toISOString(),
        offerExpiryDate: new Date(values.offerExpiryDate).toISOString(),
      }),
    );
    if (okDone) setOfferTarget(null);
  });

  const rows = data ?? [];

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-start justify-between gap-4">
            <div>
              <CardTitle>Waitlist</CardTitle>
              <CardDescription>
                Offer a freed seat, then enrol whoever accepts. Accepting does not enrol on its own.
              </CardDescription>
            </div>
            {!readOnly && (
              <Button size="sm" onClick={() => setAddOpen(true)}>
                <Plus className="mr-2 h-4 w-4" /> Add to waitlist
              </Button>
            )}
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[70px]">Position</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Added</TableHead>
                  <TableHead>Offer</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Nomination</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={ListOrdered}
                        title="Nobody is waiting"
                        description="Add an employee here when the schedule is full."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((w) => {
                    const canOffer = w.status === 'Active';
                    const canRespond = w.status === 'Offered';
                    const canEnrol = w.status === 'Accepted' && !w.createdNominationId;
                    return (
                      <TableRow key={w.id}>
                        <TableCell className="font-mono text-xs">#{w.position}</TableCell>
                        <TableCell className="font-medium">
                          {w.employeeName}
                          <div className="text-xs text-muted-foreground">{w.employeeNumber}</div>
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {new Date(w.addedDate).toLocaleDateString()}
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {w.offerDate ? (
                            <>
                              {new Date(w.offerDate).toLocaleDateString()}
                              {w.offerExpiryDate && (
                                <div className="text-xs">
                                  expires {new Date(w.offerExpiryDate).toLocaleDateString()}
                                </div>
                              )}
                            </>
                          ) : (
                            '—'
                          )}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={statusLabel(w.status)} />
                        </TableCell>
                        <TableCell>
                          {w.createdNominationNumber ? (
                            <Badge variant="secondary" className="font-mono text-[10px]">
                              {w.createdNominationNumber}
                            </Badge>
                          ) : w.status === 'Accepted' ? (
                            <span className="text-xs text-amber-600">Awaiting enrolment</span>
                          ) : (
                            <span className="text-muted-foreground">—</span>
                          )}
                        </TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" className="h-8 w-8 p-0" disabled={readOnly}>
                                <span className="sr-only">Open menu</span>
                                <MoreHorizontal className="h-4 w-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuLabel>Actions</DropdownMenuLabel>
                              {canOffer && (
                                <DropdownMenuItem onClick={() => setOfferTarget(w)}>
                                  <Send className="mr-2 h-4 w-4" /> Offer slot
                                </DropdownMenuItem>
                              )}
                              {canRespond && (
                                <>
                                  <DropdownMenuItem
                                    onClick={() =>
                                      run('Offer accepted', () =>
                                        trainingWaitlistService.respond(w.id, {
                                          offerAccepted: true,
                                          responseDate: new Date().toISOString(),
                                        }),
                                      )
                                    }
                                  >
                                    <ThumbsUp className="mr-2 h-4 w-4" /> Record acceptance
                                  </DropdownMenuItem>
                                  <DropdownMenuItem
                                    onClick={() =>
                                      run('Offer declined', () =>
                                        trainingWaitlistService.respond(w.id, {
                                          offerAccepted: false,
                                          responseDate: new Date().toISOString(),
                                        }),
                                      )
                                    }
                                  >
                                    <ThumbsDown className="mr-2 h-4 w-4" /> Record decline
                                  </DropdownMenuItem>
                                </>
                              )}
                              {canEnrol && (
                                <DropdownMenuItem onClick={() => setPromoteTarget(w)}>
                                  <UserPlus className="mr-2 h-4 w-4" /> Enrol (create nomination)
                                </DropdownMenuItem>
                              )}
                              <DropdownMenuSeparator />
                              <DropdownMenuItem
                                className="text-destructive focus:text-destructive"
                                onClick={() => setRemoveTarget(w)}
                              >
                                <Trash2 className="mr-2 h-4 w-4" /> Remove
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add to waitlist</DialogTitle>
            <DialogDescription>
              They join at the end of the queue. Position is assigned automatically.
            </DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <EmployeePickerField form={addForm} name="employeeId" label="Employee" required />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={handleAdd} disabled={busy}>
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={offerTarget !== null} onOpenChange={(o) => !o && setOfferTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Offer a slot</DialogTitle>
            <DialogDescription>
              {offerTarget
                ? `${offerTarget.employeeName} will be offered the freed seat. They still need to accept, and you then enrol them.`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            <DateField form={offerForm} name="offerDate" label="Offer date" required />
            <DateField form={offerForm} name="offerExpiryDate" label="Expires" required />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOfferTarget(null)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={handleOffer} disabled={busy}>
              Send offer
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={promoteTarget !== null}
        onOpenChange={(o) => !o && setPromoteTarget(null)}
        title="Enrol from the waitlist?"
        description={
          promoteTarget
            ? `A nomination will be created for ${promoteTarget.employeeName}. The seat is re-checked now — if the schedule filled since the offer, this will be refused.`
            : ''
        }
        confirmText="Enrol"
        isLoading={busy}
        onConfirm={async () => {
          if (!promoteTarget) return false;
          const okDone = await run('Enrolled', () => trainingWaitlistService.promote(promoteTarget.id));
          if (okDone) setPromoteTarget(null);
          return okDone;
        }}
      />

      <ConfirmationDialog
        open={removeTarget !== null}
        onOpenChange={(o) => !o && setRemoveTarget(null)}
        title="Remove from waitlist"
        description={removeTarget ? `Remove ${removeTarget.employeeName} from the queue?` : ''}
        confirmText="Remove"
        variant="destructive"
        isLoading={busy}
        onConfirm={async () => {
          if (!removeTarget) return false;
          const okDone = await run('Removed', () => trainingWaitlistService.remove(removeTarget.id));
          if (okDone) setRemoveTarget(null);
          return okDone;
        }}
      />
    </>
  );
}
