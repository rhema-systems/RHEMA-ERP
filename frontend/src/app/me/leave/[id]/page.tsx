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
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import {
  ArrowLeft,
  Ban,
  Download,
  FileText,
  Loader2,
  Paperclip,
  Pencil,
  Send,
  Trash2,
  Upload,
} from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { leaveService } from '@/services/hr/leave.service';
import { LEAVE_STATUS_BADGE } from '@/components/me/leave/leave-status';

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
  const [cancelReason, setCancelReason] = useState('');

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

  const uploadMutation = useMutation({
    mutationFn: (file: File) => leaveService.uploadAttachment(id, file),
    onSuccess: async () => {
      toast({ title: 'Attached', description: 'The document was uploaded.' });
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
    isMine && ['Draft', 'Pending', 'Approved'].includes(request.status);
  const canAttach = isMine && !['Cancelled', 'Completed'].includes(request.status);

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
        {canCancel && (
          <Button variant="outline" onClick={() => setCancelOpen(true)}>
            <Ban className="mr-2 h-4 w-4" /> Cancel request
          </Button>
        )}
      </div>

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
        <CardHeader className="flex flex-row items-center justify-between space-y-0">
          <CardTitle className="flex items-center gap-2 text-base">
            <Paperclip className="h-4 w-4" /> Attachments
          </CardTitle>
          {canAttach && (
            <>
              <input
                ref={fileInputRef}
                type="file"
                className="hidden"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) uploadMutation.mutate(file);
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
            </>
          )}
        </CardHeader>
        <CardContent>
          {attachments?.length ? (
            <ul className="space-y-2">
              {attachments.map((a) => (
                <li
                  key={a.id}
                  className="flex items-center justify-between gap-2 rounded-md border px-3 py-2 text-sm"
                >
                  <span className="flex min-w-0 items-center gap-2">
                    <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
                    <span className="truncate font-medium">{a.fileName}</span>
                    <span className="shrink-0 text-xs text-muted-foreground">
                      {fmtSize(a.fileSizeBytes)}
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
    </div>
  );
}
