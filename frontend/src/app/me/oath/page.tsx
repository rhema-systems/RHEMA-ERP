'use client';

/**
 * Area 25 slice 7 — My Oath of Secrecy (FR-HR-030): the affirmation surface, re-homed from
 * the desk oaths page's "Mine" tab (D3). The desk register (who is outstanding, paper
 * oaths, scans) stays at /hr/probation/oaths.
 *
 * Affirm takes NO id by design — it creates the caller's own oath, the server stamps the
 * date and records the IP. HR cannot affirm on anyone's behalf anywhere, and this page
 * offers no way to imply otherwise. Re-affirmation is allowed by the API (slice-0 note);
 * the page treats the newest oath as the standing one and shows the history beneath it.
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, ScrollText, ShieldCheck } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { oathOfSecrecyService } from '@/services/hr/probation.service';
import { toast } from 'sonner';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyOathPage() {
  const qc = useQueryClient();

  const { data: mine, isLoading } = useQuery({
    queryKey: ['me', 'oath', 'mine'],
    queryFn: () => oathOfSecrecyService.getMine(),
  });

  const affirm = useMutation({
    mutationFn: () => oathOfSecrecyService.affirm({}),
    onSuccess: () => {
      toast.success('Oath affirmed');
      void qc.invalidateQueries({ queryKey: ['me', 'oath'] });
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  const oaths = mine ?? [];
  const myOath = oaths[0];

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Oath of Secrecy"
        description="The confidentiality undertaking every employee affirms, and your record of it."
        backHref="/me"
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Your oath</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {isLoading ? (
            <div className="flex justify-center p-8">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : myOath ? (
            <>
              <div className="flex flex-wrap items-center gap-3">
                <Badge variant="secondary">{myOath.methodName}</Badge>
                <span className="text-sm text-muted-foreground">
                  Sworn {fmtDate(myOath.swornOn)}
                  {myOath.witnessedByName ? ` before ${myOath.witnessedByName}` : ''}
                </span>
                {myOath.hasSignature && (
                  <Badge variant="outline">
                    <ShieldCheck className="mr-1 h-3 w-3" />
                    Signed in the system
                  </Badge>
                )}
              </div>
              <p className="whitespace-pre-wrap rounded border bg-muted/40 p-3 text-sm">
                {myOath.oathText}
              </p>
            </>
          ) : (
            <>
              <Alert>
                <ScrollText className="h-4 w-4" />
                <AlertDescription>
                  You have not sworn an oath of secrecy. Affirm it if you agree; the date and
                  your identity are recorded by the system.
                </AlertDescription>
              </Alert>
              <Button disabled={affirm.isPending} onClick={() => affirm.mutate()}>
                {affirm.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Affirm my oath
              </Button>
            </>
          )}
        </CardContent>
      </Card>

      {oaths.length > 1 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Earlier affirmations</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {oaths.slice(1).map((o) => (
              <div key={o.id} className="flex flex-wrap items-center gap-3 rounded-md border p-3 text-sm">
                <Badge variant="outline">{o.methodName}</Badge>
                <span className="text-muted-foreground">
                  Sworn {fmtDate(o.swornOn)}
                  {o.witnessedByName ? ` before ${o.witnessedByName}` : ''}
                </span>
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
