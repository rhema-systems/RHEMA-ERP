'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Loader2, PackageCheck, Send, Undo2, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  );
}

type Action = 'submit' | 'recall' | 'approve' | 'reject' | 'complete';

const PROMPTS: Record<Action, { title: string; blurb: string; label: string; required: boolean }> = {
  submit: {
    title: 'Send for approval',
    blurb: 'It leaves your hands and becomes the approver’s. Recall it to edit it again.',
    label: 'Notes', required: false,
  },
  recall: {
    title: 'Pull it back',
    blurb: 'The approval is withdrawn and the transfer returns to draft.',
    label: 'Why', required: true,
  },
  approve: {
    title: 'Approve the transfer',
    blurb: 'Approving grants it. The asset does not move until the transfer is completed.',
    label: 'Comments', required: false,
  },
  reject: {
    title: 'Reject the transfer',
    blurb: 'Say why — the initiator sees this.',
    label: 'Reason', required: true,
  },
  complete: {
    title: 'Complete the transfer',
    blurb: 'This is what actually moves the asset in the register, and closes the old custody.',
    label: 'Notes', required: false,
  },
};

const DONE_TITLES: Record<Action, string> = {
  submit: 'Sent for approval',
  recall: 'Pulled back to draft',
  approve: 'Transfer approved',
  reject: 'Transfer rejected',
  complete: 'The asset has moved',
};

/**
 * One transfer, and the four decisions along it.
 *
 * ⚠ The step people skip is the last one. **Approval grants the move; completion performs it** —
 * it is completion that changes the asset's holder, unit or location, closes the outgoing custody
 * as `Transferred` rather than `Returned`, and opens the new one. An approved-but-uncompleted
 * transfer is not a bug: the register is correctly saying nothing has moved yet.
 */
export default function AssetTransferDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [action, setAction] = useState<Action | null>(null);
  const [text, setText] = useState('');

  const { data: t, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'transfer', id],
    queryFn: () => assetRegisterService.getTransfer(id),
    enabled: Boolean(id),
  });

  const run = useMutation({
    mutationFn: () => {
      switch (action) {
        case 'submit': return assetRegisterService.submitTransfer(id);
        case 'recall': return assetRegisterService.recallTransfer(id, text);
        case 'approve': return assetRegisterService.approveTransfer(id, text || undefined);
        case 'reject': return assetRegisterService.rejectTransfer(id, text);
        case 'complete': return assetRegisterService.completeTransfer(id, text || undefined);
        default: throw new Error('No action chosen');
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });
      toast({ title: DONE_TITLES[action ?? 'submit'] });
      setAction(null);
      setText('');
    },
    onError: (e: Error) =>
      toast({ title: 'The action was refused', description: e.message, variant: 'destructive' }),
  });

  if (isLoading || !t) {
    return (
      <div className="flex justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const prompt = action ? PROMPTS[action] : null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={t.transferNumber}
        description={`${t.assetName} · ${t.typeName}`}
        backHref="/hr/assets/transfers"
        actions={
          <div className="flex flex-wrap gap-2">
            {t.status === 'Draft' && (
              <Button onClick={() => setAction('submit')}>
                <Send className="mr-2 h-4 w-4" /> Send for approval
              </Button>
            )}
            {t.status === 'Pending' && (
              <>
                <Button onClick={() => setAction('approve')}>
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
                </Button>
                <Button variant="outline" onClick={() => setAction('reject')}>
                  <XCircle className="mr-2 h-4 w-4" /> Reject
                </Button>
                <Button variant="outline" onClick={() => setAction('recall')}>
                  <Undo2 className="mr-2 h-4 w-4" /> Pull it back
                </Button>
              </>
            )}
            {(t.status === 'Approved' || t.status === 'InTransit') && (
              <Button onClick={() => setAction('complete')}>
                <PackageCheck className="mr-2 h-4 w-4" /> Complete the move
              </Button>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={t.statusName} />
        {t.status === 'Approved' && (
          <span className="text-sm text-amber-600 dark:text-amber-500">
            Approved, but the asset has not moved yet — complete it to update the register.
          </span>
        )}
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">The move</CardTitle></CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-3">
            <Field label="Asset">
              <Link href={`/hr/assets/register/${t.assetId}`} className="hover:underline">
                {t.assetName} ({t.assetNumber})
              </Link>
            </Field>
            <Field label="Kind">{t.typeName}</Field>
            <Field label="Dated">{fmtDate(t.transferDate)}</Field>

            {/* Where it is coming FROM is taken from the register, never supplied by the caller —
                that is what keeps a transfer honest about the asset's actual position. */}
            <Field label="From (holder)">{t.fromEmployeeName ?? '—'}</Field>
            <Field label="From (unit)">{t.fromUnitName ?? '—'}</Field>
            <Field label="From (location)">{t.fromLocationName ?? '—'}</Field>

            <Field label="To (holder)">{t.toEmployeeName ?? '—'}</Field>
            <Field label="To (unit)">{t.toUnitName ?? '—'}</Field>
            <Field label="To (location)">{t.toLocationName ?? '—'}</Field>

            <Field label="Raised by">{t.initiatedByName ?? '—'}</Field>
            <Field label="Approved by">{t.approvedByName ?? '—'}</Field>
            <Field label="Completed">{fmtDate(t.completionDate)}</Field>

            {t.transferReason && (
              <div className="sm:col-span-3"><Field label="Reason">{t.transferReason}</Field></div>
            )}
            {t.notes && (
              <div className="sm:col-span-3"><Field label="Notes">{t.notes}</Field></div>
            )}
          </dl>
        </CardContent>
      </Card>

      <Dialog open={action !== null} onOpenChange={(o) => { if (!o) { setAction(null); setText(''); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{prompt?.title}</DialogTitle>
            <DialogDescription>{prompt?.blurb}</DialogDescription>
          </DialogHeader>
          {action !== 'submit' && (
            <div className="space-y-2">
              <Label>{prompt?.label}{prompt?.required ? ' *' : ''}</Label>
              <Textarea rows={3} value={text} onChange={(e) => setText(e.target.value)} />
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => { setAction(null); setText(''); }}>Cancel</Button>
            <Button
              onClick={() => run.mutate()}
              disabled={run.isPending || (prompt?.required === true && !text.trim())}
            >
              {run.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
