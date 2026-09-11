'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { BadgeDollarSign, Handshake, Info, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { useToast } from '@/hooks/use-toast';
import { formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import { salaryReviewProposalService } from '@/services/hr/outcomes.service';

/**
 * One pay-for-performance proposal.
 *
 * **Three stages, owned by three different things.** The figure is HR's to set and is editable
 * only while the proposal is Proposed. Approval belongs to the workflow engine — whoever the
 * published `SalaryReviewProposal` definition routes it to. Applying it belongs to payroll, and
 * is recorded here rather than approved: marking it Applied says the change has been made, not
 * that someone agreed to it.
 *
 * ⚠ Submitting is refused until the figure is set — a merit increase with no percentage is
 * nothing an approver can weigh. The form says so up front rather than after the 422.
 */
export default function SalaryReviewProposalPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [percent, setPercent] = useState('');
  const [amount, setAmount] = useState('');
  const [notes, setNotes] = useState('');
  const [applyOpen, setApplyOpen] = useState(false);
  const [applyNotes, setApplyNotes] = useState('');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'salary-review-proposal', id],
    queryFn: () => salaryReviewProposalService.getById(id),
    enabled: !!id,
    retry: false,
  });

  // Seed the editable fields once the proposal arrives, and again whenever it is refetched
  // after a change — otherwise a save would show the old figure until a reload.
  useEffect(() => {
    if (!data) return;
    setPercent(data.proposedPercent != null ? String(data.proposedPercent) : '');
    setAmount(data.proposedAmount != null ? String(data.proposedAmount) : '');
    setNotes(data.notes ?? '');
  }, [data]);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'salary-review-proposal', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'salary-review-proposals'] });
  };

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e.message, variant: 'destructive' });

  const isBonus = data?.proposalType === 'Bonus';
  const hasFigure = isBonus ? Number(amount) > 0 : Number(percent) > 0;
  const editable = data?.status === 'Proposed';

  const save = useMutation({
    mutationFn: () =>
      salaryReviewProposalService.update(id, {
        proposedPercent: percent.trim() ? Number(percent) : null,
        proposedAmount: amount.trim() ? Number(amount) : null,
        notes: notes.trim() || null,
      }),
    onSuccess: () => {
      toast({ title: 'Proposal updated' });
      refresh();
    },
    onError: fail('Could not save'),
  });

  const markApplied = useMutation({
    mutationFn: () =>
      salaryReviewProposalService.markApplied(id, { notes: applyNotes.trim() || null }),
    onSuccess: () => {
      toast({
        title: 'Marked applied',
        description: 'The proposal is closed; payroll owns the change from here.',
      });
      setApplyOpen(false);
      refresh();
    },
    onError: fail('Could not mark applied'),
  });

  /**
   * Submit / approve / reject / recall all come from the engine. This page never sets a status:
   * the service drives the workflow and `SalaryReviewProposalWorkflowStatusAdapter` maps the
   * outcome onto the record, so we refetch and let it decide.
   */
  const workflow = useWorkflowRecord({
    entityType: 'SalaryReviewProposal',
    entityId: id,
    entityLabel: 'Salary Review Proposal',
    entityNumber: data?.appraisalNumber ?? undefined,
    status: data?.status ?? 'Proposed',
    canSubmit: data?.status === 'Proposed' && hasFigure,
    canApproveReject: data?.status === 'PendingApproval',
    enabled: !!data,
    commands: {
      submit: () => salaryReviewProposalService.submit(id),
      approve: () => salaryReviewProposalService.approve(id),
      reject: (ctx) => salaryReviewProposalService.reject(id, { notes: ctx.comments || null }),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Salary review proposal" backHref="/hr/performance/proposals" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not load this proposal"
              description={(error as Error)?.message ?? 'It may have been removed.'}
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${humanizeEnum(data.proposalType)} — ${data.employeeName ?? 'Employee'}`}
        description={
          data.appraisalNumber
            ? `Raised from appraisal ${data.appraisalNumber}`
            : 'Raised from an appraisal outcome'
        }
        backHref="/hr/performance/proposals"
        actions={
          <div className="flex items-center gap-2">
            <WorkflowApprovalActions {...workflow.actionProps} />
            {data.status === 'Approved' && (
              // Round 3, lane S: the proposal is a recommendation; the pay change itself is a
              // salary change request on the employee's Salary tab, applied when approved there.
              <Button variant="outline" onClick={() => router.push(`/hr/employees/${data.employeeId}?tab=salary&fromProposal=${data.id}`)}>
                Raise the salary change
              </Button>
            )}
            {data.status === 'Approved' && (
              <Button onClick={() => setApplyOpen(true)}>
                <Handshake className="mr-2 h-4 w-4" />
                Mark applied
              </Button>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={data.status} />
        {data.sourceAppraisalId && (
          <Button variant="link" size="sm" className="h-auto p-0" asChild>
            <Link href={`/hr/performance/hr-review/${data.sourceAppraisalId}`}>
              Open the appraisal
            </Link>
          </Button>
        )}
      </div>

      {data.status === 'Proposed' && !hasFigure && (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertTitle>Needs a figure before it can go for approval</AlertTitle>
          <AlertDescription>
            The recommendation that raised this only knew an appraisal called for
            {isBonus ? ' a bonus' : ' an increase'} — not how much. Set{' '}
            {isBonus ? 'an amount' : 'a percentage'} and save, then submit.
          </AlertDescription>
        </Alert>
      )}

      {data.status === 'Approved' && (
        <Alert>
          <Handshake className="h-4 w-4" />
          <AlertTitle>Approved — nothing has changed yet</AlertTitle>
          <AlertDescription>
            This is an instruction to payroll, not the change itself. Mark it applied once the pay
            change has actually been made.
          </AlertDescription>
        </Alert>
      )}

      <Tabs defaultValue="details">
        <TabsList>
          <TabsTrigger value="details">Details</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="details" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <BadgeDollarSign className="h-4 w-4" />
                The proposal
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="proposedPercent">
                    Increase percentage
                    {!isBonus && <span className="ml-0.5 text-red-500">*</span>}
                  </Label>
                  <Input
                    id="proposedPercent"
                    type="number"
                    min={0}
                    max={100}
                    step="0.01"
                    value={percent}
                    onChange={(e) => setPercent(e.target.value)}
                    disabled={!editable || isBonus}
                    placeholder={isBonus ? 'Not used for a bonus' : 'e.g. 4'}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="proposedAmount">
                    Bonus amount
                    {isBonus && <span className="ml-0.5 text-red-500">*</span>}
                  </Label>
                  <Input
                    id="proposedAmount"
                    type="number"
                    min={0}
                    step="0.01"
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    disabled={!editable || !isBonus}
                    placeholder={isBonus ? 'e.g. 5000' : 'Not used for a merit increase'}
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="proposalNotes">Notes</Label>
                <Textarea
                  id="proposalNotes"
                  rows={4}
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  disabled={!editable}
                  placeholder="What the appraisal said, and anything an approver needs to know."
                />
              </div>

              {editable ? (
                <div className="flex justify-end">
                  <Button onClick={() => save.mutate()} disabled={save.isPending}>
                    {save.isPending ? 'Saving…' : 'Save'}
                  </Button>
                </div>
              ) : (
                <p className="text-xs text-muted-foreground">
                  Locked: a proposal can only be amended while it is still Proposed.
                  {data.status !== 'Rejected' &&
                    ' Recall it from the workflow to make changes.'}
                </p>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="SalaryReviewProposal"
          entityId={id}
          entityLabel="Salary Review Proposal"
          entityNumber={data.appraisalNumber ?? undefined}
          status={data.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>

      <Dialog open={applyOpen} onOpenChange={setApplyOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Mark applied</DialogTitle>
            <DialogDescription>
              Records that payroll has made the change — {isBonus
                ? formatMoney(data.proposedAmount)
                : `${data.proposedPercent}%`}{' '}
              for {data.employeeName}. This closes the proposal.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="applyNotes">Notes</Label>
            <Textarea
              id="applyNotes"
              rows={3}
              value={applyNotes}
              onChange={(e) => setApplyNotes(e.target.value)}
              placeholder="Effective date, payroll reference, anything worth keeping."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApplyOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => markApplied.mutate()} disabled={markApplied.isPending}>
              {markApplied.isPending ? 'Recording…' : 'Mark applied'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
