'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Loader2, UserPlus } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Textarea } from '@/components/ui/textarea';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobHireService } from '@/services/hr/offers.service';
import { HIRE_TRANSITIONS, type JobHireStatus } from '@/types/hr/offers';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

/**
 * A single hire — the record that turns an accepted offer into an employee.
 *
 * ⚠ **`confirm-start` is the consequential action on this whole slice.** It creates the `Employee`
 * plus their contract, probation period, salary assignment, position history and the candidate's
 * carried-over qualifications, work history, referees and skills — and burns an employee number.
 * It is idempotent (refused once the hire is linked to an employee) so a retry is safe, but there is
 * no undo once it succeeds, which is why it gets its own confirmation copy rather than the shared
 * one-liner every other dialog uses.
 */
export default function JobHireDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [statusDialogOpen, setStatusDialogOpen] = useState(false);
  const [newStatus, setNewStatus] = useState<JobHireStatus | ''>('');
  const [statusNotes, setStatusNotes] = useState('');

  const [confirmOpen, setConfirmOpen] = useState(false);
  const [actualStartDate, setActualStartDate] = useState('');
  const [isInternal, setIsInternal] = useState(false);
  const [linkedEmployeeId, setLinkedEmployeeId] = useState<string | null>(null);
  const [linkedEmployeeLabel, setLinkedEmployeeLabel] = useState<string | null>(null);

  const { data: hire, isLoading, isError } = useQuery({
    queryKey: ['hr', 'hires', id],
    queryFn: () => jobHireService.getById(id),
    enabled: !!id,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'hires', id] });

  const updateStatus = useMutation({
    mutationFn: () => {
      if (!newStatus) throw new Error('Choose a status');
      return jobHireService.updateStatus(id, {
        newStatus,
        notes: statusNotes.trim() || null,
      });
    },
    onSuccess: async () => {
      await refresh();
      setStatusDialogOpen(false);
      setNewStatus('');
      setStatusNotes('');
      toast({ title: 'Status updated' });
    },
    onError: (e: any) => toast({ title: 'Refused', description: e?.message, variant: 'destructive' }),
  });

  const confirmStart = useMutation({
    mutationFn: () =>
      jobHireService.confirmStart(id, {
        actualStartDate,
        linkedEmployeeId: isInternal ? linkedEmployeeId : null,
      }),
    onSuccess: async () => {
      await refresh();
      setConfirmOpen(false);
      toast({
        title: 'Start confirmed',
        description: isInternal
          ? 'The existing employee record has been linked.'
          : 'A new employee record has been created.',
      });
    },
    onError: (e: any) =>
      toast({ title: 'Could not confirm start', description: e?.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !hire) {
    return (
      <div className="p-6">
        <EmptyState title="Hire not found" description="It may have been removed." />
      </div>
    );
  }

  const isActive = hire.status === 'Active';
  const availableTransitions = HIRE_TRANSITIONS[hire.status] ?? [];
  const canChangeStatus = availableTransitions.length > 0;
  // ⚠ G-12.2 (2026-09-15): `!isActive && !hire.employeeId` let a **Cancelled** hire through, and so
  // did the server. Pressing Confirm start on one created the employee, the contract, the probation
  // period and the position history, and burned an employee number — all irreversible, on a hire
  // somebody had explicitly called off. Every other terminal state in this module is guarded.
  const canConfirmStart = !isActive && !hire.employeeId && hire.status !== 'Cancelled';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={hire.hireNumber}
        description={`${hire.candidateName} · ${hire.applicationNumber}`}
        backHref="/hr/recruitment/hires"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={hire.status} />
            {canChangeStatus && (
              <Button variant="outline" onClick={() => setStatusDialogOpen(true)}>
                Update status
              </Button>
            )}
            {canConfirmStart && (
              <Button onClick={() => setConfirmOpen(true)}>
                <UserPlus className="mr-2 h-4 w-4" /> Confirm start
              </Button>
            )}
          </div>
        }
      />

      {isActive && hire.employeeId && (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Employee record created</AlertTitle>
          <AlertDescription>
            {hire.employeeName} ({hire.employeeNumber}) — confirmed {formatDateTime(hire.confirmedDate)} by{' '}
            {hire.confirmedByName ?? 'HR'}.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Overview</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
          <InfoRow label="Candidate" value={hire.candidateName} />
          <InfoRow
            label="Offer"
            value={
              <Link href={`/hr/recruitment/offers/${hire.offerId}`} className="text-primary hover:underline">
                {hire.offerNumber}
              </Link>
            }
          />
          <InfoRow label="Application" value={hire.applicationNumber} />
          <InfoRow label="Expected start" value={formatDate(hire.expectedStartDate)} />
          <InfoRow label="Actual start" value={formatDate(hire.actualStartDate)} />
          <InfoRow
            label="Employee"
            value={
              hire.employeeId ? (
                <Link href={`/hr/employees/${hire.employeeId}`} className="text-primary hover:underline">
                  {hire.employeeName} ({hire.employeeNumber})
                </Link>
              ) : (
                'Not yet created'
              )
            }
          />
          <InfoRow label="Confirmed by" value={hire.confirmedByName} />
          <InfoRow label="Confirmed" value={formatDateTime(hire.confirmedDate)} />
        </CardContent>
      </Card>

      {hire.notes && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Notes</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm">{hire.notes}</p>
          </CardContent>
        </Card>
      )}

      {/* ── status ────────────────────────────────────────────────────────── */}
      <Dialog open={statusDialogOpen} onOpenChange={setStatusDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Update onboarding status</DialogTitle>
            <DialogDescription>
              {hire.status} can move to {availableTransitions.map((s) => humanizeEnum(s)).join(' or ')}.
              Active is not offered here — confirm the start date instead.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label>New status</Label>
              <Select value={newStatus} onValueChange={(v) => setNewStatus(v as JobHireStatus)}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a status" />
                </SelectTrigger>
                <SelectContent>
                  {availableTransitions.map((s) => (
                    <SelectItem key={s} value={s}>
                      {humanizeEnum(s)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="statusNotes">Notes</Label>
              <Textarea
                id="statusNotes"
                rows={3}
                value={statusNotes}
                onChange={(e) => setStatusNotes(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStatusDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => updateStatus.mutate()} disabled={!newStatus || updateStatus.isPending}>
              {updateStatus.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── confirm start ─────────────────────────────────────────────────── */}
      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Confirm start date</DialogTitle>
          </DialogHeader>
          <Alert variant="destructive">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>This creates an employee record</AlertTitle>
            <AlertDescription>
              Confirming burns an employee number and creates the employee, their contract, probation
              period, salary assignment and position history. It cannot be undone — a retry is safe
              (it refuses once linked), but there is no way to detach the employee afterwards.
              {/* G-12.3 (2026-09-15): the warning was admirably honest about being irreversible and
                  said nothing about what to do if it happened anyway, which is the question somebody
                  asks at exactly the moment they most need an answer. */}
              <span className="mt-2 block">
                If you confirm one in error: terminate the employee through Separations and cancel
                this hire record. The employee number stays spent, and the departure opens a
                position vacancy — both are permanent, so check the name and the start date first.
              </span>
            </AlertDescription>
          </Alert>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="actualStartDate">Actual start date</Label>
              <Input
                id="actualStartDate"
                type="date"
                value={actualStartDate}
                onChange={(e) => setActualStartDate(e.target.value)}
              />
            </div>

            <div className="flex items-center gap-2">
              <Checkbox
                id="isInternal"
                checked={isInternal}
                onCheckedChange={(c) => setIsInternal(c === true)}
              />
              <Label htmlFor="isInternal" className="font-normal">
                Internal hire — link to an existing employee record
              </Label>
            </div>

            {isInternal && (
              <div className="space-y-1.5">
                <Label>Employee to link</Label>
                <EmployeePicker
                  value={linkedEmployeeId}
                  initialLabel={linkedEmployeeLabel}
                  onChange={(id2, label) => {
                    setLinkedEmployeeId(id2);
                    setLinkedEmployeeLabel(label);
                  }}
                />
              </div>
            )}
            {!isInternal && (
              <p className="text-xs text-muted-foreground">
                A new employee record will be created from the candidate&apos;s profile — their
                qualifications, work history, referees and skills carry over.
              </p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => confirmStart.mutate()}
              disabled={!actualStartDate || (isInternal && !linkedEmployeeId) || confirmStart.isPending}
            >
              {confirmStart.isPending ? 'Confirming…' : 'Confirm and create employee'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
