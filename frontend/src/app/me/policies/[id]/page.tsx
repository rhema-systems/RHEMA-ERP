'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Download, FileText, Loader2, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { useToast } from '@/hooks/use-toast';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import { myPoliciesService, POLICY_CATEGORY_LABEL } from '@/services/hr/policies.service';

/**
 * Read a policy, and sign it (area 25 slice 12d).
 *
 * The signing control is deliberately awkward in one specific way: the tick box is disabled
 * until the document has actually been opened. Acknowledgement is evidence that somebody read
 * something, and a one-click "I agree" next to an unopened PDF makes that evidence worthless.
 * It is a low bar — it proves a download, not comprehension — but it is the difference between
 * a record that means something and one that does not.
 *
 * The declaration displayed here is echoed back to the server on signing, which refuses a
 * mismatch. So a wording change while this page sat open cannot capture a signature against
 * text the employee never saw.
 */

const fmtDate = (v?: string | null) =>
  v ? new Date(v).toLocaleDateString(undefined, { day: 'numeric', month: 'long', year: 'numeric' }) : '—';

export default function MyPolicyPage() {
  const params = useParams();
  const id = String(params?.id ?? '');
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [opened, setOpened] = useState(false);
  const [agreed, setAgreed] = useState(false);
  const [declining, setDeclining] = useState(false);
  const [reason, setReason] = useState('');

  const { data: policy, isLoading, isError } = useQuery({
    queryKey: ['me', 'policies', id],
    queryFn: () => myPoliciesService.getById(id),
    enabled: id !== '',
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['me', 'policies'] });
    queryClient.invalidateQueries({ queryKey: ['me', 'inbox'] });
    queryClient.invalidateQueries({ queryKey: ['me', 'inbox-counts'] });
  };

  const download = async () => {
    if (!policy) return;
    try {
      await hrDocumentService.download(
        myPoliciesService.documentEndpoint(policy.id),
        policy.fileName ?? `${policy.policyNumber}.pdf`,
      );
      setOpened(true);
    } catch {
      toast({
        title: 'Could not open the policy',
        description: 'Please try again in a moment.',
        variant: 'destructive',
      });
    }
  };

  const acknowledge = useMutation({
    mutationFn: () => myPoliciesService.acknowledge(id, policy?.acknowledgementText ?? ''),
    onSuccess: () => {
      refresh();
      toast({ title: 'Acknowledged', description: 'Your confirmation has been recorded.' });
    },
    onError: (e: any) =>
      toast({
        title: 'Not recorded',
        description: e?.message || 'Your acknowledgement could not be recorded.',
        variant: 'destructive',
      }),
  });

  const decline = useMutation({
    mutationFn: () => myPoliciesService.decline(id, reason),
    onSuccess: () => {
      refresh();
      setDeclining(false);
      setReason('');
      toast({
        title: 'Recorded',
        description: 'HR can see your reason. The policy still shows as outstanding for you.',
      });
    },
    onError: (e: any) =>
      toast({ title: 'Not recorded', description: e?.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-56" />
      </div>
    );
  }

  if (isError || !policy) {
    return (
      <div className="space-y-6">
        <PageHeader title="Policy" backHref="/me/policies" />
        <p className="text-sm text-muted-foreground">
          This policy is not available to you. It may have been withdrawn, or replaced by a newer
          version — your policy list shows what currently applies.
        </p>
      </div>
    );
  }

  const alreadySigned = policy.myOutcome === 'Signed';

  return (
    <div className="space-y-6">
      <PageHeader
        title={policy.title}
        description={policy.summary ?? undefined}
        backHref="/me/policies"
      />

      <Card>
        <CardContent className="space-y-4 p-4">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="outline">
              {POLICY_CATEGORY_LABEL[policy.category] ?? policy.categoryName}
            </Badge>
            <span className="font-mono text-xs text-muted-foreground">{policy.policyNumber}</span>
            {policy.versionLabel && (
              <Badge variant="secondary" className="font-mono">{policy.versionLabel}</Badge>
            )}
            <span className="ml-auto text-xs text-muted-foreground">
              effective {fmtDate(policy.effectiveFrom ?? policy.publishedAt)}
            </span>
          </div>

          {policy.hasDocument ? (
            <Button variant="outline" onClick={download}>
              <Download className="mr-2 h-4 w-4" />
              {policy.fileName ?? 'Open the policy document'}
            </Button>
          ) : (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <FileText className="h-4 w-4" /> No document is attached to this policy.
            </p>
          )}
        </CardContent>
      </Card>

      {/* ── Acknowledgement ──────────────────────────────────────────── */}
      {policy.requiresAcknowledgement && (
        <Card>
          <CardContent className="space-y-4 p-4">
            {alreadySigned ? (
              <div className="flex items-start gap-3">
                <CheckCircle2 className="mt-0.5 h-5 w-5 text-emerald-600" />
                <div>
                  <p className="font-medium">You acknowledged this policy</p>
                  <p className="text-sm text-muted-foreground">
                    on {fmtDate(policy.mySignedAt)}
                    {policy.myDeclineReason
                      ? ' — after initially declining, which remains on the record.'
                      : '.'}
                  </p>
                  <p className="mt-2 rounded-md bg-muted/40 px-3 py-2 text-sm italic">
                    “{policy.acknowledgementText}”
                  </p>
                </div>
              </div>
            ) : (
              <>
                {policy.myOutcome === 'Declined' && (
                  <div className="flex items-start gap-3 rounded-md border border-red-300/60 bg-red-50 p-3 dark:border-red-500/40 dark:bg-red-950/30">
                    <XCircle className="mt-0.5 h-4 w-4 text-red-600" />
                    <div className="text-sm">
                      <p className="font-medium text-red-900 dark:text-red-200">
                        You declined this on {fmtDate(policy.myDeclinedAt)}
                      </p>
                      <p className="mt-0.5 text-red-900/80 dark:text-red-200/80">
                        “{policy.myDeclineReason}”
                      </p>
                      <p className="mt-1 text-muted-foreground">
                        It still shows as outstanding. You can acknowledge it below if that changes.
                      </p>
                    </div>
                  </div>
                )}

                <div className="rounded-md bg-muted/40 px-3 py-2 text-sm">
                  {policy.acknowledgementText}
                </div>

                <div className="flex items-start gap-2">
                  <Checkbox
                    id="agree"
                    checked={agreed}
                    disabled={policy.hasDocument && !opened}
                    onCheckedChange={(c) => setAgreed(c === true)}
                  />
                  <Label htmlFor="agree" className="font-normal leading-snug">
                    I confirm the statement above.
                    {policy.hasDocument && !opened && (
                      <span className="mt-0.5 block text-xs text-muted-foreground">
                        Open the policy document first — an acknowledgement is a record that you
                        read it.
                      </span>
                    )}
                  </Label>
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button
                    onClick={() => acknowledge.mutate()}
                    disabled={!agreed || acknowledge.isPending}
                  >
                    {acknowledge.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Acknowledge
                  </Button>
                  <Button variant="outline" onClick={() => setDeclining(true)}>
                    I cannot agree to this
                  </Button>
                </div>
              </>
            )}
          </CardContent>
        </Card>
      )}

      <Dialog open={declining} onOpenChange={(o) => !o && setDeclining(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Tell HR why</DialogTitle>
            <DialogDescription>
              Declining is recorded and HR will see your reason. The policy will still show as
              outstanding for you, and you can acknowledge it later if that changes.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label htmlFor="decline-reason">Reason</Label>
            <Textarea
              id="decline-reason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g. I would like to discuss clause 4 with my manager first."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeclining(false)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => decline.mutate()}
              disabled={decline.isPending || reason.trim() === ''}
            >
              {decline.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record my reason
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
