'use client';

import { Suspense, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'next/navigation';
import { CheckCircle2, Loader2, MailWarning } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { publicCareersService } from '@/services/hr/careers.service';
import type { OfferResponseChoice } from '@/types/hr/careers';

/**
 * The anonymous offer-response page that offer emails link to
 * (`{PortalUrl}/careers/portal/offer-response?token=…` — the path is fixed by the emails
 * already in inboxes, which is why it lives under /careers/portal/). The GUID token is the
 * whole authority: no login, single use, marked consumed on a successful response. A candidate
 * with an account sees the same offer in their portal; this page is for the one who has not
 * registered.
 */
function OfferResponseInner() {
  const params = useSearchParams();
  const token = params?.get('token') ?? '';
  const { toast } = useToast();

  const [response, setResponse] = useState<OfferResponseChoice>('Accepted');
  const [notes, setNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  const offer = useQuery({
    queryKey: ['careers', 'offer-token', token],
    queryFn: () => publicCareersService.validateOfferToken(token),
    enabled: !!token,
    retry: false,
  });

  const submit = async () => {
    setSubmitting(true);
    try {
      await publicCareersService.respondWithOfferToken(
        token,
        response,
        notes.trim() || null,
        response === 'Declined' ? notes.trim() || null : null,
      );
      setDone(true);
    } catch (e: any) {
      toast({ title: 'Could not record your response', description: e?.message, variant: 'destructive' });
    } finally {
      setSubmitting(false);
    }
  };

  if (!token) {
    return (
      <p className="py-16 text-center text-muted-foreground">
        This link is missing its response token — use the link from your offer email.
      </p>
    );
  }

  if (offer.isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const o = offer.data;
  if (offer.isError || !o || o.tokenExpired || o.tokenAlreadyUsed) {
    return (
      <div className="mx-auto max-w-md">
        <Card>
          <CardContent className="space-y-2 py-10 text-center">
            <MailWarning className="mx-auto h-8 w-8 text-amber-600" />
            <p className="font-medium">
              {o?.tokenAlreadyUsed ? 'This offer has already been responded to.' : 'This link has expired.'}
            </p>
            <p className="text-sm text-muted-foreground">
              If you believe that is wrong, reply to the offer email and the recruitment team can
              send a fresh link.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (done) {
    return (
      <div className="mx-auto max-w-md">
        <Card>
          <CardContent className="space-y-2 py-10 text-center">
            <CheckCircle2 className="mx-auto h-8 w-8 text-green-600" />
            <p className="font-medium">Your response has been recorded.</p>
            <p className="text-sm text-muted-foreground">
              The recruitment team will be in touch about the next steps.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-lg space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-xl">Offer — {o.positionTitle}</CardTitle>
          <CardDescription>
            {o.offerNumber}
            {o.expiryDate ? ` · respond by ${o.expiryDate}` : ''}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm">
            <div>
              <div className="text-xs text-muted-foreground">Base salary</div>
              {o.baseSalary ?? '—'}
            </div>
            <div>
              <div className="text-xs text-muted-foreground">Proposed start</div>
              {o.startDate ?? '—'}
            </div>
          </div>
          {o.isConditional && (
            <Badge variant="secondary">This offer is conditional — see your offer letter.</Badge>
          )}
          {o.additionalTerms && (
            <div className="text-sm">
              <div className="text-xs text-muted-foreground">Additional terms</div>
              <p className="whitespace-pre-wrap">{o.additionalTerms}</p>
            </div>
          )}

          <div className="space-y-3 rounded-lg border p-4">
            <div className="space-y-2">
              <Label>Your response</Label>
              <Select value={response} onValueChange={(v) => setResponse(v as OfferResponseChoice)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Accepted">Accept the offer</SelectItem>
                  <SelectItem value="Negotiating">Discuss the terms</SelectItem>
                  <SelectItem value="Declined">Decline</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="offer-token-notes">
                {response === 'Declined' ? 'Why are you declining?' : 'Notes (optional)'}
              </Label>
              <Textarea
                id="offer-token-notes"
                value={notes}
                maxLength={4000}
                onChange={(e) => setNotes(e.target.value)}
              />
            </div>
            <Button disabled={submitting} onClick={submit}>
              {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Send response
            </Button>
            <p className="text-xs text-muted-foreground">
              This link works once — after you send a response it cannot be reused.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

export default function OfferResponsePage() {
  return (
    <Suspense
      fallback={
        <div className="flex items-center justify-center py-24">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      }
    >
      <OfferResponseInner />
    </Suspense>
  );
}
