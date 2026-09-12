'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Loader2, Save, Send, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { awardsService } from '@/services/hr/awards.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * One nomination the caller raised.
 *
 * ⚠ **A draft and a submitted nomination are different objects to the user.** A draft can be edited
 * and withdrawn; a submitted one can be neither — the API refuses both with a 409. The screen offers
 * the buttons only in the state that accepts them, rather than showing them and letting the refusal
 * explain itself afterwards.
 *
 * ⚠ **Somebody else's nomination is a 404 here, not a 403.** A 403 would confirm the id exists and
 * turn this route into a way of enumerating nomination ids.
 *
 * Area 25 slice 9: re-homed from /hr/awards/me/nominations/[id] (D3). The Withdraw button works
 * for the first time — the service used to POST a route that never existed (the backend is
 * DELETE nominations/{id}).
 */
export default function MyNominationPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [justification, setJustification] = useState('');

  const { data: nomination, isLoading, isError } = useQuery({
    queryKey: ['me', 'awards', 'nominations', id],
    queryFn: () => awardsService.getMyNomination(id),
    retry: false,
  });

  useEffect(() => {
    if (nomination) setJustification(nomination.justification ?? '');
  }, [nomination]);

  const isDraft = nomination?.status === 'Draft';
  const dirty = nomination ? justification.trim() !== (nomination.justification ?? '').trim() : false;

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['me', 'awards', 'nominations'] });
  };

  const save = useMutation({
    mutationFn: () =>
      awardsService.updateNomination(id, {
        id,
        justification: justification.trim(),
        proposedMonetaryAmount: nomination?.proposedMonetaryAmount ?? null,
        proposedLeaveDays: nomination?.proposedLeaveDays ?? null,
      }),
    onSuccess: () => { toast.success('Saved.'); invalidate(); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The change was refused.'),
  });

  const submit = useMutation({
    mutationFn: () => awardsService.submitNomination(id),
    onSuccess: () => { toast.success('Submitted. It can no longer be changed.'); invalidate(); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The submission was refused.'),
  });

  const withdraw = useMutation({
    mutationFn: () => awardsService.withdrawNomination(id),
    onSuccess: () => { toast.success('Withdrawn.'); invalidate(); router.push('/me/awards'); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The withdrawal was refused.'),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !nomination) {
    return (
      <div className="space-y-6">
        <PageHeader title="Nomination" backHref="/me/awards" />
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            This nomination does not exist, or it is not one of yours.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={nomination.nominationNumber}
        description={`${nomination.awardTypeName}${nomination.awardCycleName ? ` — ${nomination.awardCycleName}` : ''}`}
        backHref="/me/awards"
        actions={<Badge variant="secondary">{nomination.statusName}</Badge>}
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Who you put forward</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div>
            <p className="text-xs text-muted-foreground">Nominee</p>
            <p className="text-sm">
              {nomination.nomineeName || nomination.teamName || '—'}
              {nomination.nomineeEmployeeNumber && (
                <span className="ml-2 text-xs text-muted-foreground">
                  {nomination.nomineeEmployeeNumber}
                </span>
              )}
            </p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Raised</p>
            <p className="text-sm">{fmtDate(nomination.nominationDate)}</p>
          </div>
          {/* The outcome, once there is one. A nomination that produced an award names it. */}
          {nomination.awardNumber && (
            <div>
              <p className="text-xs text-muted-foreground">Outcome</p>
              <p className="text-sm">Award {nomination.awardNumber}</p>
            </div>
          )}
          {nomination.outcomeReason && (
            <div>
              <p className="text-xs text-muted-foreground">Reason given</p>
              <p className="text-sm">{nomination.outcomeReason}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Justification</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {isDraft ? (
            <>
              <div className="space-y-2">
                <Label htmlFor="justification">Why they deserve it</Label>
                <Textarea
                  id="justification"
                  rows={6}
                  value={justification}
                  onChange={(e) => setJustification(e.target.value)}
                />
              </div>
              <Alert>
                <AlertTriangle className="h-4 w-4" />
                <AlertDescription>
                  This is still a draft, so nobody has seen it. Once submitted it cannot be changed
                  or withdrawn.
                </AlertDescription>
              </Alert>
              <div className="flex justify-end gap-2">
                <Button
                  variant="outline"
                  disabled={withdraw.isPending}
                  onClick={() => withdraw.mutate()}
                >
                  {withdraw.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Trash2 className="mr-2 h-4 w-4" />
                  )}
                  Withdraw
                </Button>
                <Button
                  variant="outline"
                  disabled={!dirty || justification.trim().length === 0 || save.isPending}
                  onClick={() => save.mutate()}
                >
                  {save.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Save className="mr-2 h-4 w-4" />
                  )}
                  Save
                </Button>
                <Button
                  disabled={dirty || submit.isPending}
                  title={dirty ? 'Save your changes first' : undefined}
                  onClick={() => submit.mutate()}
                >
                  {submit.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Send className="mr-2 h-4 w-4" />
                  )}
                  Submit
                </Button>
              </div>
            </>
          ) : (
            <p className="whitespace-pre-wrap text-sm">{nomination.justification}</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
