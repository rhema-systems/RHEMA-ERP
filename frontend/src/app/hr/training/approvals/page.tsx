'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { CheckCircle2, XCircle, Eye, UserCheck } from 'lucide-react';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { TextareaField } from '@/components/hr/employee/tabs/fields';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import { NOMINATION_STATUS_OPTIONS } from '@/types/hr/training-delivery';
import type { TrainingNominationSummary } from '@/types/hr/training-delivery';

const statusLabel = (v: string) =>
  NOMINATION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

type Stage = 'Supervisor' | 'HR';

/**
 * The two nomination approval queues. They are separate endpoints because they are separate
 * decisions — a supervisor releases their report, HR confirms the seat and the spend — and the same
 * nomination passes through both in order.
 */
export default function NominationApprovalsPage() {
  const [stage, setStage] = useState<Stage>('Supervisor');

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Nomination Approvals"
        description="Nominations waiting on a decision. Supervisor first, then HR."
        backHref="/hr/training"
      />
      <Tabs value={stage} onValueChange={(v) => setStage(v as Stage)}>
        <TabsList>
          <TabsTrigger value="Supervisor">Awaiting supervisor</TabsTrigger>
          <TabsTrigger value="HR">Awaiting HR</TabsTrigger>
        </TabsList>
        <TabsContent value="Supervisor" className="pt-4">
          <ApprovalQueue stage="Supervisor" />
        </TabsContent>
        <TabsContent value="HR" className="pt-4">
          <ApprovalQueue stage="HR" />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function ApprovalQueue({ stage }: { stage: Stage }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [approveTarget, setApproveTarget] = useState<TrainingNominationSummary | null>(null);
  const [rejectTarget, setRejectTarget] = useState<TrainingNominationSummary | null>(null);
  const [busy, setBusy] = useState(false);

  const queryKey = ['hr', 'training', 'nominations', 'pending', stage];
  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () =>
      stage === 'Supervisor'
        ? trainingNominationService.getPendingSupervisor()
        : trainingNominationService.getPendingHr(),
  });

  const approveForm = useForm<{ comments: string }>({ defaultValues: { comments: '' } });
  const rejectForm = useForm<{ rejectionReason: string }>({ defaultValues: { rejectionReason: '' } });

  const act = async (label: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'nominations'] });
      toast({ title: label });
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || `${label} failed.`, variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const rows = data ?? [];

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>{stage === 'Supervisor' ? 'Supervisor queue' : 'HR queue'}</CardTitle>
          <CardDescription>
            {stage === 'Supervisor'
              ? 'Releasing a report to attend — the first gate.'
              : 'Confirming the seat and the spend — the final gate.'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Nomination</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Programme</TableHead>
                  <TableHead>Starts</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[170px]">Decision</TableHead>
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
                        icon={UserCheck}
                        title="Nothing waiting"
                        description={`No nominations are sitting with ${stage === 'Supervisor' ? 'a supervisor' : 'HR'}.`}
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((n) => (
                    <TableRow key={n.id}>
                      <TableCell className="font-mono text-xs">{n.nominationNumber}</TableCell>
                      <TableCell className="font-medium">
                        {n.employeeName}
                        <div className="text-xs text-muted-foreground">{n.employeeNumber}</div>
                      </TableCell>
                      <TableCell>{n.programName}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {new Date(n.trainingStartDate).toLocaleDateString()}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={statusLabel(n.status)} />
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => {
                              approveForm.reset({ comments: '' });
                              setApproveTarget(n);
                            }}
                          >
                            <CheckCircle2 className="h-4 w-4 text-green-600" />
                            <span className="sr-only">Approve</span>
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => {
                              rejectForm.reset({ rejectionReason: '' });
                              setRejectTarget(n);
                            }}
                          >
                            <XCircle className="h-4 w-4 text-destructive" />
                            <span className="sr-only">Reject</span>
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => router.push(`/hr/training/nominations/${n.id}`)}
                          >
                            <Eye className="h-4 w-4" />
                            <span className="sr-only">View</span>
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={approveTarget !== null} onOpenChange={(o) => !o && setApproveTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Approve as {stage}</DialogTitle>
            <DialogDescription>
              {approveTarget
                ? `${approveTarget.employeeName} — ${approveTarget.programName}.${
                    stage === 'Supervisor' ? ' It then goes to HR.' : ''
                  }`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <TextareaField form={approveForm} name="comments" label="Comments" rows={3} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproveTarget(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                if (!approveTarget) return;
                const ok = await act('Approved', () =>
                  trainingNominationService.approve(approveTarget.id, {
                    approverRole: stage,
                    comments: approveForm.getValues('comments') || null,
                  }),
                );
                if (ok) setApproveTarget(null);
              }}
            >
              Approve
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={rejectTarget !== null} onOpenChange={(o) => !o && setRejectTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject nomination</DialogTitle>
            <DialogDescription>The reason is recorded against the nomination.</DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <TextareaField form={rejectForm} name="rejectionReason" label="Reason" rows={3} required />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectTarget(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={busy}
              onClick={async () => {
                if (!rejectTarget) return;
                const reason = rejectForm.getValues('rejectionReason').trim();
                if (!reason) {
                  toast({ title: 'A reason is required', variant: 'destructive' });
                  return;
                }
                const ok = await act('Rejected', () =>
                  trainingNominationService.reject(rejectTarget.id, { rejectionReason: reason }),
                );
                if (ok) setRejectTarget(null);
              }}
            >
              Reject
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
