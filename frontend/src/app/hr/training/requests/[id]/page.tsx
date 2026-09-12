'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Loader2, Send, CheckCircle2, XCircle } from 'lucide-react';
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
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';
import { trainingRequestService } from '@/services/hr/training-request.service';
import { trainingProgramService } from '@/services/hr/training-program.service';
import { TRAINING_REQUEST_STATUS_OPTIONS } from '@/types/hr/training-delivery';

const statusLabel = (v: string) =>
  TRAINING_REQUEST_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function TrainingRequestDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [submitOpen, setSubmitOpen] = useState(false);
  const [approveOpen, setApproveOpen] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const queryKey = ['hr', 'training', 'requests', id];
  const { data: request, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => trainingRequestService.getById(id),
    enabled: !!id,
  });

  const { data: programs } = useQuery({
    queryKey: ['hr', 'training', 'programs', 'active'],
    queryFn: () => trainingProgramService.getActive(),
  });
  const programOptions = (programs ?? []).map((p) => ({
    value: p.id,
    label: `${p.programCode} — ${p.programName}`,
  }));

  const approveForm = useForm<{ linkedProgramId: string }>({ defaultValues: { linkedProgramId: '' } });
  const rejectForm = useForm<{ rejectionReason: string }>({ defaultValues: { rejectionReason: '' } });

  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'requests'] }),
    ]);

  const act = async (label: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await invalidate();
      toast({ title: label });
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || `${label} failed.`, variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !request) {
    return (
      <div className="p-6">
        <EmptyState title="Request not found" description="It may have been removed." />
      </div>
    );
  }

  const isDraft = request.status === 'Draft';
  const isSubmitted = request.status === 'Submitted';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={request.requestedTrainingTitle}
        description={`${request.requestNumber} · ${request.employeeName} (${request.employeeNumber})`}
        backHref="/hr/training/requests"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={statusLabel(request.status)} />
            {isDraft && (
              <Button size="sm" onClick={() => setSubmitOpen(true)}>
                <Send className="mr-2 h-4 w-4" /> Submit
              </Button>
            )}
            {isSubmitted && (
              <>
                <Button
                  size="sm"
                  onClick={() => {
                    approveForm.reset({ linkedProgramId: request.linkedProgramId ?? '' });
                    setApproveOpen(true);
                  }}
                >
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    rejectForm.reset({ rejectionReason: '' });
                    setRejectOpen(true);
                  }}
                >
                  <XCircle className="mr-2 h-4 w-4" /> Reject
                </Button>
              </>
            )}
          </div>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Request</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 py-2 sm:grid-cols-2">
          <Detail label="Raised on" value={fmt(request.requestDate)} />
          <Detail label="Linked programme" value={request.linkedProgramName ?? 'Not in the catalog'} />
          <Detail label="Approved by" value={request.approvedByName ?? '—'} />
          <Detail label="Approved on" value={fmt(request.approvalDate)} />
          <div className="sm:col-span-2">
            <p className="text-sm text-muted-foreground">Description</p>
            <p className="font-medium">{request.description || '—'}</p>
          </div>
          <div className="sm:col-span-2">
            <p className="text-sm text-muted-foreground">Justification</p>
            <p className="font-medium">{request.justification || '—'}</p>
          </div>
          {request.rejectionReason && (
            <div className="sm:col-span-2">
              <p className="text-sm text-muted-foreground">Rejection reason</p>
              <p className="font-medium text-destructive">{request.rejectionReason}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={submitOpen}
        onOpenChange={setSubmitOpen}
        title="Submit for approval?"
        description="It moves out of draft and appears on the approvals queue."
        confirmText="Submit"
        isLoading={busy}
        onConfirm={async () => {
          const ok = await act('Submitted', () => trainingRequestService.submit(id));
          if (ok) setSubmitOpen(false);
          return ok;
        }}
      />

      <Dialog open={approveOpen} onOpenChange={setApproveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Approve request</DialogTitle>
            <DialogDescription>
              Linking it to a catalog programme is what makes it schedulable.
            </DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <SelectField
              form={approveForm}
              name="linkedProgramId"
              label="Link to programme"
              options={programOptions}
              allowEmpty
              emptyLabel="Do not link"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproveOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                const ok = await act('Approved', () =>
                  trainingRequestService.approve(id, {
                    linkedProgramId: approveForm.getValues('linkedProgramId') || null,
                  }),
                );
                if (ok) setApproveOpen(false);
              }}
            >
              Approve
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={rejectOpen} onOpenChange={setRejectOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject request</DialogTitle>
            <DialogDescription>The reason is shown to whoever raised it.</DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <TextareaField form={rejectForm} name="rejectionReason" label="Reason" rows={3} required />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={busy}
              onClick={async () => {
                const reason = rejectForm.getValues('rejectionReason').trim();
                if (!reason) {
                  toast({ title: 'A reason is required', variant: 'destructive' });
                  return;
                }
                const ok = await act('Rejected', () =>
                  trainingRequestService.reject(id, { rejectionReason: reason }),
                );
                if (ok) setRejectOpen(false);
              }}
            >
              Reject
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="font-medium">{value}</p>
    </div>
  );
}
