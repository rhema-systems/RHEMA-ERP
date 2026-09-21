'use client';

/**
 * Area 25 slice 4 — one of my leave requests.
 *
 * Every read/write here rides the request-owner arm (CanActOnRequestAsync): detail,
 * submit, cancel, and the attachment set — which is where medical certificates live, so
 * uploads go through the controlled gate and downloads only through the authorized
 * endpoint. Actions the backend would refuse for this status are simply not offered.
 */

import { use, useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
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
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import {
  ArrowLeft,
  Ban,
  Download,
  FileText,
  Loader2,
  Paperclip,
  CalendarClock,
  CircleCheck,
  CornerUpLeft,
  Pencil,
  Send,
  Trash2,
  Upload,
} from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { leaveService } from '@/services/hr/leave.service';
import {
  LEAVE_EVIDENCE_KIND_LABEL,
  type LeaveEvidenceKind,
} from '@/types/hr/leave-request';
import { LEAVE_STATUS_BADGE } from '@/components/me/leave/leave-status';
import {
  RespondToSuggestionDialog,
  RescheduleDialog,
  SuggestedDatesPanel,
  RescheduleTrailPanel,
  RecallPanel,
} from '@/components/hr/leave/LeaveDateChangeDialogs';

const fmtDate = (d: string) =>
  new Date(d).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
const fmtDateTime = (d: string) =>
  new Date(d).toLocaleString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
const fmtSize = (bytes?: number | null) =>
  bytes == null ? '' : bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  if (value === null || value === undefined || value === '') return null;
  return (
    <div className="grid grid-cols-3 gap-2 py-1.5 text-sm">
      <div className="text-muted-foreground">{label}</div>
      <div className="col-span-2">{value}</div>
    </div>
  );
}

export default function MyLeaveRequestDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [dateDialog, setDateDialog] = useState<null | 'respond' | 'reschedule'>(null);
  const [cancelReason, setCancelReason] = useState('');
  /*
   * ⚠ What the document IS, chosen before the file is picked. The evidence gate refuses a
   * submission until a document of the required kind is attached, and it cannot read a file name
   * to decide — `scan.pdf` is a medical certificate or a holiday photograph with equal
   * probability. Without this control an employee told on the request form that she needs excuse
   * duty could attach it here and still be refused at Submit, because everything this screen
   * uploaded was filed as a plain supporting document.
   *
   * Defaults to `Other`, like the desk's panel: guessing the kind from the leave type would put
   * the gate back where it started.
   */
  const [evidenceKind, setEvidenceKind] = useState<LeaveEvidenceKind>('Other');

  const { data: request, isLoading } = useQuery({
    queryKey: ['me', 'leave-request', id],
    queryFn: () => leaveService.getById(id),
  });

  const { data: attachments } = useQuery({
    queryKey: ['me', 'leave-request', id, 'attachments'],
    queryFn: () => leaveService.getAttachments(id),
    enabled: !!request,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['me', 'leave-request', id] });
    await queryClient.invalidateQueries({ queryKey: ['me', 'leave-history'] });
    await queryClient.invalidateQueries({ queryKey: ['me', 'leave-balances'] });
  };

  const submitMutation = useMutation({
    mutationFn: () => leaveService.submit(id),
    onSuccess: async () => {
      toast({ title: 'Submitted', description: 'Your request is on its way for approval.' });
      await refresh();
    },
    onError: (e: any) =>
      toast({
        title: 'Could not submit',
        description: e?.message || 'The request could not be submitted for approval.',
        variant: 'destructive',
      }),
  });

  const cancelMutation = useMutation({
    mutationFn: () => leaveService.cancel(id, cancelReason.trim()),
    onSuccess: async () => {
      setCancelOpen(false);
      toast({ title: 'Cancelled', description: 'The leave request was cancelled.' });
      await refresh();
    },
    onError: (e: any) =>
      toast({
        title: 'Could not cancel',
        description: e?.message || 'The request could not be cancelled.',
        variant: 'destructive',
      }),
  });

  /**
   * Answering the approver's suggested dates, and moving an approved request.
   *
   * ⚠ Both re-enter approval — answering re-submits on the settled dates, and a move re-opens the
   * approval because an approval is an approval OF DATES (decision D-5). Neither is a quiet edit,
   * and the dialogs say so before you confirm.
   */
  const respondMutation = useMutation({
    mutationFn: (values: {
      accept: boolean;
      startDate: string | null;
      endDate: string | null;
      notes: string | null;
    }) => leaveService.respondToSuggestion(id, values),
    onSuccess: async () => {
      setDateDialog(null);
      toast({
        title: 'Answered',
        description: 'Your request is back with your approver on the settled dates.',
      });
      await refresh();
    },
    onError: (e: any) =>
      toast({
        title: 'Could not answer',
        description: e?.message || 'The suggested dates could not be answered.',
        variant: 'destructive',
      }),
  });

  const rescheduleMutation = useMutation({
    mutationFn: (values: { startDate: string; endDate: string; reason: string }) =>
      leaveService.reschedule(id, values),
    onSuccess: async () => {
      setDateDialog(null);
      toast({
        title: 'Moved',
        description: 'Your leave has moved and is back with your approver.',
      });
      await refresh();
    },
    onError: (e: any) =>
      toast({
        title: 'Could not move it',
        description: e?.message || 'The leave could not be moved.',
        variant: 'destructive',
      }),
  });

  const confirmMutation = useMutation({
    mutationFn: () => leaveService.confirmObservance(id),
    onSuccess: async () => {
      toast({ title: 'Thanks', description: 'We have recorded that you are still going.' });
      await refresh();
    },
    onError: (e: any) =>
      toast({
        title: 'Could not confirm',
        description: e?.message || 'The confirmation did not save.',
        variant: 'destructive',
      }),
  });

  const uploadMutation = useMutation({
    mutationFn: ({ file, kind }: { file: File; kind: LeaveEvidenceKind }) =>
      leaveService.uploadAttachment(id, file, kind),
    onSuccess: async (_data, { kind }) => {
      toast({
        title: 'Attached',
        description: `Uploaded as ${LEAVE_EVIDENCE_KIND_LABEL[kind].toLowerCase()}.`,
      });
      await queryClient.invalidateQueries({ queryKey: ['me', 'leave-request', id, 'attachments'] });
    },
    onError: (e: any) =>
      toast({
        title: 'Upload refused',
        description: e?.message || 'The document could not be uploaded.',
        variant: 'destructive',
      }),
  });

  const deleteAttachmentMutation = useMutation({
    mutationFn: (attachmentId: string) => leaveService.removeAttachment(attachmentId),
    onSuccess: async () => {
      toast({ title: 'Removed', description: 'The attachment was deleted.' });
      await queryClient.invalidateQueries({ queryKey: ['me', 'leave-request', id, 'attachments'] });
    },
    onError: (e: any) =>
      toast({
        title: 'Could not remove',
        description: e?.message || 'The attachment could not be deleted.',
        variant: 'destructive',
      }),
  });

  if (isLoading || !request) {
    return (
      <div className="mx-auto max-w-3xl space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-72" />
      </div>
    );
  }

  // Whose request this is is enforced server-side; this only trims the actions offered.
  const isMine = user?.employeeId && request.employeeId === user.employeeId;
  const canEdit = isMine && request.status === 'Draft';
  const canSubmit = isMine && (request.status === 'Draft' || request.status === 'Pending');
  const canCancel =
    isMine && ['Draft', 'Pending', 'Approved', 'ChangesSuggested'].includes(request.status);
  const canAttach = isMine && !['Cancelled', 'Completed'].includes(request.status);

  // Your approver sent it back with dates of their own; accept them or counter (closure plan R-3).
  const canRespond = isMine && request.status === 'ChangesSuggested';
  // Move approved leave without cancelling and re-keying it (R-8), and say it is still going (R-7).
  const canReschedule = isMine && request.status === 'Approved' && !request.closureDate;
  const canConfirm = canReschedule && !request.observanceConfirmedDate;

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="-ml-2 mb-2">
          <Link href="/me/leave">
            <ArrowLeft className="mr-1 h-4 w-4" /> My Leave
          </Link>
        </Button>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold tracking-tight">
              {request.leaveTypeName}
              {request.leaveSubTypeName ? ` · ${request.leaveSubTypeName}` : ''}
            </h1>
            <p className="text-sm text-muted-foreground">
              {request.requestNumber} · requested {fmtDateTime(request.requestDate)}
            </p>
          </div>
          <Badge className={LEAVE_STATUS_BADGE[request.status] ?? ''} variant="outline">
            {request.status}
          </Badge>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {canEdit && (
          <Button variant="outline" onClick={() => router.push(`/me/leave/${id}/edit`)}>
            <Pencil className="mr-2 h-4 w-4" /> Edit draft
          </Button>
        )}
        {canSubmit && (
          <Button onClick={() => submitMutation.mutate()} disabled={submitMutation.isPending}>
            {submitMutation.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Send className="mr-2 h-4 w-4" />
            )}
            Submit for approval
          </Button>
        )}
        {canRespond && (
          <Button onClick={() => setDateDialog('respond')}>
            <CornerUpLeft className="mr-2 h-4 w-4" /> Answer the suggested dates
          </Button>
        )}
        {canReschedule && (
          <Button variant="outline" onClick={() => setDateDialog('reschedule')}>
            <CalendarClock className="mr-2 h-4 w-4" /> Move these dates
          </Button>
        )}
        {canConfirm && (
          <Button
            variant="outline"
            disabled={confirmMutation.isPending}
            onClick={() => confirmMutation.mutate()}
          >
            {confirmMutation.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <CircleCheck className="mr-2 h-4 w-4" />
            )}
            Yes, still going
          </Button>
        )}
        {canCancel && (
          <Button variant="outline" onClick={() => setCancelOpen(true)}>
            <Ban className="mr-2 h-4 w-4" /> Cancel request
          </Button>
        )}
      </div>

      <SuggestedDatesPanel request={request} />
      <RescheduleTrailPanel request={request} />
      {/* Read-only here. Recall is the employer's act, but the employee is the person it happens
          to, so their own copy of the record has to say it happened and why. */}
      <RecallPanel request={request} />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Details</CardTitle>
        </CardHeader>
        <CardContent className="divide-y">
          <Row
            label="Dates"
            value={`${fmtDate(request.startDate)} – ${fmtDate(request.endDate)} (${request.totalDays} day${request.totalDays === 1 ? '' : 's'})`}
          />
          <Row label="Paid" value={request.isPaidLeave ? 'Yes' : 'No'} />
          <Row label="Reason" value={request.reason} />
          <Row label="Reliever" value={request.relieverEmployeeName} />
          <Row label="Second reliever" value={request.secondRelieverEmployeeName} />
          <Row label="Reliever notes" value={request.relieverNotes} />
          <Row label="Handover notes" value={request.handoverNotes} />
          {request.status === 'Approved' && (
            <Row
              label="Still going ahead"
              value={
                request.observanceConfirmedDate
                  ? `Confirmed ${fmtDate(request.observanceConfirmedDate)}`
                  : 'Not yet confirmed'
              }
            />
          )}
          {request.status === 'Cancelled' && (
            <>
              <Row
                label="Cancelled"
                value={request.cancellationDate ? fmtDateTime(request.cancellationDate) : '—'}
              />
              <Row label="Cancellation reason" value={request.cancellationReason} />
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Paperclip className="h-4 w-4" /> Attachments
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {canAttach && (
            <div className="space-y-2 rounded-md border bg-muted/30 p-3">
              <Label htmlFor="evidence-kind">What is this document?</Label>
              <Select
                value={evidenceKind}
                onValueChange={(v) => setEvidenceKind(v as LeaveEvidenceKind)}
              >
                <SelectTrigger id="evidence-kind" className="sm:max-w-sm">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(Object.keys(LEAVE_EVIDENCE_KIND_LABEL) as LeaveEvidenceKind[]).map((k) => (
                    <SelectItem key={k} value={k}>
                      {LEAVE_EVIDENCE_KIND_LABEL[k]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Choose this before you pick the file. If your leave needs excuse duty — a medical
                certificate — it has to be attached <strong>as that kind</strong> before the request
                can be submitted. Anything else is a supporting document.
              </p>

              <input
                ref={fileInputRef}
                type="file"
                className="hidden"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) uploadMutation.mutate({ file, kind: evidenceKind });
                  e.target.value = '';
                }}
              />
              <Button
                variant="outline"
                size="sm"
                disabled={uploadMutation.isPending}
                onClick={() => fileInputRef.current?.click()}
              >
                {uploadMutation.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Upload className="mr-2 h-4 w-4" />
                )}
                Attach a document
              </Button>
            </div>
          )}

          {attachments?.length ? (
            <ul className="space-y-2">
              {attachments.map((a) => (
                <li
                  key={a.id}
                  className="flex items-center justify-between gap-2 rounded-md border px-3 py-2 text-sm"
                >
                  <span className="flex min-w-0 flex-col gap-0.5">
                    <span className="flex min-w-0 items-center gap-2">
                      <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
                      <span className="truncate font-medium">{a.fileName}</span>
                      <span className="shrink-0 text-xs text-muted-foreground">
                        {fmtSize(a.fileSizeBytes)}
                      </span>
                    </span>
                    {/*
                      ⚠ Shown because it is what the evidence gate READS. Without it an employee
                      refused at Submit for missing a certificate cannot tell that the file she
                      attached was filed as something else.
                    */}
                    <span className="pl-6 text-xs text-muted-foreground">
                      {LEAVE_EVIDENCE_KIND_LABEL[a.evidenceKind] ?? a.evidenceKind}
                    </span>
                  </span>
                  <span className="flex shrink-0 items-center gap-1">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => leaveService.downloadAttachment(a.id, a.fileName)}
                      title="Download"
                    >
                      <Download className="h-4 w-4" />
                    </Button>
                    {canAttach && (
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={deleteAttachmentMutation.isPending}
                        onClick={() => deleteAttachmentMutation.mutate(a.id)}
                        title="Delete"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    )}
                  </span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-sm text-muted-foreground">
              Nothing attached. Supporting documents (e.g. a medical certificate) are stored
              privately and only people entitled to this request can open them.
            </p>
          )}
        </CardContent>
      </Card>

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel this leave request?</DialogTitle>
            <DialogDescription>
              {request.status === 'Approved'
                ? 'This request is already approved — cancelling gives the days back to your balance.'
                : 'The request will be closed and will not be considered for approval.'}
            </DialogDescription>
          </DialogHeader>
          <Textarea
            placeholder="Why are you cancelling? (required)"
            value={cancelReason}
            onChange={(e) => setCancelReason(e.target.value)}
            rows={3}
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>
              Keep it
            </Button>
            <Button
              variant="destructive"
              disabled={!cancelReason.trim() || cancelMutation.isPending}
              onClick={() => cancelMutation.mutate()}
            >
              {cancelMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Cancel request
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <RespondToSuggestionDialog
        request={request}
        open={dateDialog === 'respond'}
        onOpenChange={(open) => setDateDialog(open ? 'respond' : null)}
        busy={respondMutation.isPending}
        onConfirm={(values) => respondMutation.mutate(values)}
      />

      <RescheduleDialog
        request={request}
        open={dateDialog === 'reschedule'}
        onOpenChange={(open) => setDateDialog(open ? 'reschedule' : null)}
        busy={rescheduleMutation.isPending}
        onConfirm={(values) => rescheduleMutation.mutate(values)}
      />

    </div>
  );
}
