'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Eye, Loader2, Mail, Printer, Upload, X } from 'lucide-react';
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
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { useToast } from '@/hooks/use-toast';
import {
  hrLetterQueueService,
  LETTER_TYPE_LABEL,
  type HrLetterDocument,
  type HrLetterRequest,
  type HrLetterRequestStatus,
} from '@/services/hr/my-letters.service';
import { cn } from '@/lib/utils';

/**
 * Employee letter requests — the HR desk queue (area 25 slice 12b, decision D7).
 *
 * Two ways to fulfil one, and the screen leads with the cheaper: **preview then issue** renders
 * the letter from the HR-editable template and freezes it. **Upload** attaches a signed scan for
 * the letters that need a wet signature or somebody else's form.
 *
 * Preview is not decoration. Issuing freezes the document and hands it to a bank; reading it
 * first is the step that catches a wrong salary or a stale position before it leaves.
 */

const fmtDateTime = (v?: string | null) =>
  v ? new Date(v).toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—';

const STATUS_STYLE: Record<HrLetterRequestStatus, string> = {
  Pending: 'bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-200',
  Issued: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-200',
  Rejected: 'bg-red-100 text-red-900 dark:bg-red-950/50 dark:text-red-200',
  Cancelled: 'bg-muted text-muted-foreground',
};

export default function HrLetterQueuePage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [tab, setTab] = useState<'Pending' | 'all'>('Pending');
  const [previewing, setPreviewing] = useState<{ request: HrLetterRequest; document: HrLetterDocument } | null>(null);
  const [uploading, setUploading] = useState<HrLetterRequest | null>(null);
  const [rejecting, setRejecting] = useState<HrLetterRequest | null>(null);
  const [comments, setComments] = useState('');

  const { data: requests = [], isLoading, isError } = useQuery({
    queryKey: ['hr', 'letter-requests', tab],
    queryFn: () => hrLetterQueueService.getQueue(tab === 'all' ? undefined : 'Pending'),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'letter-requests'] });

  const preview = useMutation({
    mutationFn: async (request: HrLetterRequest) => ({
      request,
      document: await (request.status === 'Issued'
        ? hrLetterQueueService.getDocument(request.id)
        : hrLetterQueueService.preview(request.id)),
    }),
    onSuccess: setPreviewing,
    onError: (e: any) =>
      toast({
        title: 'Could not render the letter',
        description: e?.message || 'The template could not be resolved.',
        variant: 'destructive',
      }),
  });

  const issue = useMutation({
    mutationFn: (id: string) => hrLetterQueueService.issue(id),
    onSuccess: (updated) => {
      refresh();
      setPreviewing(null);
      toast({
        title: `Letter ${updated.letterNumber} issued`,
        description: `${updated.employeeName} can collect it from their portal now.`,
      });
    },
    onError: (e: any) =>
      toast({
        title: 'Not issued',
        description: e?.message || 'The letter could not be issued.',
        variant: 'destructive',
      }),
  });

  const reject = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      hrLetterQueueService.reject(id, reason),
    onSuccess: (updated) => {
      refresh();
      setRejecting(null);
      setComments('');
      toast({
        title: `${updated.requestNumber} refused`,
        description: 'The employee can read your reason on their portal.',
      });
    },
    onError: (e: any) =>
      toast({
        title: 'Not refused',
        description: e?.message || 'The request could not be refused.',
        variant: 'destructive',
      }),
  });

  const printPreview = () => {
    const done = () => {
      window.removeEventListener('afterprint', done);
      document.body.classList.remove('printing-hr-letter');
    };
    window.addEventListener('afterprint', done);
    document.body.classList.add('printing-hr-letter');
    window.print();
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employee Letter Requests"
        description="Letters staff have asked for. Read one before issuing it — issuing freezes the document and the employee can hand it to a bank."
        backHref="/hr/employees"
      />

      <Tabs value={tab} onValueChange={(v) => setTab(v as 'Pending' | 'all')}>
        <TabsList>
          <TabsTrigger value="Pending">Waiting on HR</TabsTrigger>
          <TabsTrigger value="all">All</TabsTrigger>
        </TabsList>
      </Tabs>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-36" />
          <Skeleton className="h-36" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          The queue could not be loaded right now. Try again in a moment.
        </p>
      ) : requests.length === 0 ? (
        <EmptyState
          icon={Mail}
          title={tab === 'Pending' ? 'Nothing waiting' : 'No letter requests'}
          description={
            tab === 'Pending'
              ? 'No employee is waiting on a letter.'
              : 'Nobody has asked for a letter yet.'
          }
        />
      ) : (
        <div className="space-y-3">
          {requests.map((r) => (
            <Card key={r.id}>
              <CardContent className="space-y-3 p-4">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{r.employeeName}</span>
                  <span className="font-mono text-xs text-muted-foreground">{r.employeeNumber}</span>
                  <Badge variant="outline">
                    {LETTER_TYPE_LABEL[r.letterType] ?? r.letterTypeName}
                  </Badge>
                  <Badge className={cn('border-0', STATUS_STYLE[r.status])}>{r.statusName}</Badge>
                  {r.letterNumber && (
                    <Badge variant="outline" className="font-mono">{r.letterNumber}</Badge>
                  )}
                  <span className="ml-auto font-mono text-xs text-muted-foreground">
                    {r.requestNumber}
                  </span>
                </div>

                <div className="text-sm">
                  <span className="text-muted-foreground">For: </span>
                  {r.purpose}
                  {r.addressedTo && (
                    <>
                      <span className="text-muted-foreground"> · addressed to: </span>
                      {r.addressedTo}
                    </>
                  )}
                </div>

                {r.decisionComments && (
                  <div className="rounded-md border-l-2 border-primary/50 bg-muted/30 px-3 py-2 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">
                      {r.issuedByName ?? 'HR'}
                    </div>
                    <p className="mt-0.5">{r.decisionComments}</p>
                  </div>
                )}

                <div className="flex flex-wrap items-center gap-2">
                  {(r.status === 'Pending' || r.fulfilment === 'Generated') && (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => preview.mutate(r)}
                      disabled={preview.isPending}
                    >
                      {preview.isPending ? (
                        <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" />
                      ) : (
                        <Eye className="mr-1 h-3.5 w-3.5" />
                      )}
                      {r.status === 'Issued' ? 'View letter' : 'Preview'}
                    </Button>
                  )}
                  {r.status === 'Pending' && (
                    <>
                      <Button variant="outline" size="sm" onClick={() => setUploading(r)}>
                        <Upload className="mr-1 h-3.5 w-3.5" /> Upload signed
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => { setRejecting(r); setComments(''); }}>
                        <X className="mr-1 h-3.5 w-3.5" /> Refuse
                      </Button>
                    </>
                  )}
                  <span className="ml-auto text-xs text-muted-foreground">
                    asked {fmtDateTime(r.requestedAt)}
                    {r.issuedAt ? ` · issued ${fmtDateTime(r.issuedAt)}` : ''}
                  </span>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* ── Preview / issue ───────────────────────────────────────────── */}
      <Dialog open={!!previewing} onOpenChange={(o) => !o && setPreviewing(null)}>
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader className="hr-letter-no-print">
            <DialogTitle>
              {previewing?.document.isIssued ? 'Issued letter' : 'Preview'} —{' '}
              {previewing ? LETTER_TYPE_LABEL[previewing.request.letterType] : ''}
            </DialogTitle>
            <DialogDescription>
              {previewing?.document.isIssued
                ? 'This is the copy frozen when the letter was issued.'
                : 'Read it before issuing. Issuing freezes this document and the employee can collect it immediately.'}
            </DialogDescription>
          </DialogHeader>

          <div className="hr-letter-print-root rounded-md border bg-white p-2 dark:bg-white">
            <div
              className="text-black"
              dangerouslySetInnerHTML={{ __html: previewing?.document.html ?? '' }}
            />
          </div>

          <DialogFooter className="hr-letter-no-print">
            <Button variant="outline" onClick={printPreview}>
              <Printer className="mr-1 h-4 w-4" /> Print
            </Button>
            <Button variant="outline" onClick={() => setPreviewing(null)}>
              Close
            </Button>
            {previewing?.request.status === 'Pending' && (
              <Button
                onClick={() => previewing && issue.mutate(previewing.request.id)}
                disabled={issue.isPending}
              >
                {issue.isPending ? (
                  <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                ) : (
                  <Check className="mr-1 h-4 w-4" />
                )}
                Issue this letter
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Upload a signed scan ──────────────────────────────────────── */}
      <Dialog open={!!uploading} onOpenChange={(o) => !o && setUploading(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Upload a signed letter</DialogTitle>
            <DialogDescription>
              For a letter that needs a wet signature, a stamp, or another organisation&apos;s own
              form. Uploading fulfils the request — {uploading?.employeeName} can download it
              straight away.
            </DialogDescription>
          </DialogHeader>
          {uploading && (
            <DocumentUploadField
              label="Signed letter"
              endpoint={hrLetterQueueService.uploadEndpoint(uploading.id)}
              helpText="The scanned, signed letter on company letterhead."
              onUploaded={() => {
                setUploading(null);
                refresh();
                toast({
                  title: 'Letter issued',
                  description: `${uploading.employeeName} can download it from their portal now.`,
                });
              }}
            />
          )}
        </DialogContent>
      </Dialog>

      {/* ── Refuse ───────────────────────────────────────────────────── */}
      <Dialog open={!!rejecting} onOpenChange={(o) => !o && setRejecting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Refuse {rejecting?.requestNumber}</DialogTitle>
            <DialogDescription>
              {rejecting?.employeeName} reads this on their portal, so say what is needed — a
              refusal without a reason leaves them with nowhere to go.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label htmlFor="reject-comments">Reason</Label>
            <Textarea
              id="reject-comments"
              rows={3}
              value={comments}
              onChange={(e) => setComments(e.target.value)}
              placeholder="e.g. Salary letters are issued after confirmation — please ask again in March."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejecting(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => rejecting && reject.mutate({ id: rejecting.id, reason: comments })}
              disabled={reject.isPending || comments.trim() === ''}
            >
              {reject.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Refuse request
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
