'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { Handshake, TriangleAlert, UserCog } from 'lucide-react';
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
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { employmentActionProposalService } from '@/services/hr/outcomes.service';
import { EMPLOYMENT_ACTION_DESTINATIONS } from '@/types/hr/outcomes';

/**
 * One employment-action proposal: a promotion, demotion, contract renewal, termination or
 * recognition that an appraisal called for.
 *
 * **This record is deliberately thin.** It captures the intent without a target position, a
 * salary, an award type or an effective date, because demanding those at approval time would
 * mean an appraisal outcome could not be recorded until someone had done the destination
 * module's work. The real record is created there; marking this Actioned is the receipt.
 *
 * Approval routing comes from the published `EmploymentActionProposal` workflow definition —
 * which is how a recognition and a termination can go to different approvers.
 */
export default function EmploymentActionProposalPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [actionOpen, setActionOpen] = useState(false);
  const [actionNotes, setActionNotes] = useState('');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'employment-action-proposal', id],
    queryFn: () => employmentActionProposalService.getById(id),
    enabled: !!id,
    retry: false,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'employment-action-proposal', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'employment-action-proposals'] });
  };

  const markActioned = useMutation({
    mutationFn: () =>
      employmentActionProposalService.markActioned(id, { notes: actionNotes.trim() || null }),
    onSuccess: () => {
      toast({
        title: 'Marked actioned',
        description: 'The proposal is closed; the owning module holds the real record.',
      });
      setActionOpen(false);
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not mark actioned', description: e.message, variant: 'destructive' }),
  });

  const workflow = useWorkflowRecord({
    recallPrompt: 'reason',
    entityType: 'EmploymentActionProposal',
    entityId: id,
    entityLabel: 'Employment Action Proposal',
    entityNumber: data?.appraisalNumber ?? undefined,
    status: data?.status ?? 'Proposed',
    canSubmit: data?.status === 'Proposed',
    canApproveReject: data?.status === 'PendingApproval',
    enabled: !!data,
    commands: {
      submit: () => employmentActionProposalService.submit(id),
      approve: () => employmentActionProposalService.approve(id),
      reject: (ctx) => employmentActionProposalService.reject(id, { notes: ctx.comments || null }),
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
        <PageHeader title="Employment action proposal" backHref="/hr/performance/proposals" />
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

  const destination = EMPLOYMENT_ACTION_DESTINATIONS[data.actionType];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${humanizeEnum(data.actionType)} — ${data.employeeName ?? 'Employee'}`}
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
              <Button onClick={() => setActionOpen(true)}>
                <Handshake className="mr-2 h-4 w-4" />
                Mark actioned
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

      {data.status === 'Approved' && (
        <Alert>
          <Handshake className="h-4 w-4" />
          <AlertTitle>Approved — the change has not been made</AlertTitle>
          <AlertDescription>
            Create the record in <strong>{destination}</strong>, then mark this actioned. Nothing
            happens to the employee&apos;s position, contract or record from here.
          </AlertDescription>
        </Alert>
      )}

      <Tabs defaultValue="details">
        <TabsList>
          <TabsTrigger value="details">Details</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="details" className="space-y-4 pt-4">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <UserCog className="h-4 w-4" />
                The proposal
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4 text-sm">
              <Row label="Employee" value={data.employeeName ?? '—'} />
              <Row label="Action" value={humanizeEnum(data.actionType)} />
              <Row label="Source appraisal" value={data.appraisalNumber ?? '—'} />
              <Row label="Actioned in" value={destination} />
              <div>
                <div className="text-muted-foreground">Notes</div>
                <div className="mt-1 whitespace-pre-wrap">
                  {data.notes || 'No notes were carried across from the recommendation.'}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="EmploymentActionProposal"
          entityId={id}
          entityLabel="Employment Action Proposal"
          entityNumber={data.appraisalNumber ?? undefined}
          status={data.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>

      <Dialog open={actionOpen} onOpenChange={setActionOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Mark actioned</DialogTitle>
            <DialogDescription>
              Confirms that the {humanizeEnum(data.actionType).toLowerCase()} has been created in{' '}
              {destination}. This closes the proposal.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="actionNotes">Notes</Label>
            <Textarea
              id="actionNotes"
              rows={3}
              value={actionNotes}
              onChange={(e) => setActionNotes(e.target.value)}
              placeholder="Reference to the record you created, effective date, anything worth keeping."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setActionOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => markActioned.mutate()} disabled={markActioned.isPending}>
              {markActioned.isPending ? 'Recording…' : 'Mark actioned'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4 border-b pb-2 last:border-0">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium">{value}</span>
    </div>
  );
}
