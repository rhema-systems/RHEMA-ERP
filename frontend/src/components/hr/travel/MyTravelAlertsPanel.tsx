'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BellRing, Check, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelAlertNotification } from '@/types/hr/travel-compliance';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * Destination alerts sent to the signed-in traveller, and the only place they can be acknowledged.
 *
 * ⚠ **The acknowledgement had no reachable caller at all.** The desk route sits on
 * `HR.Travel.Write`, which `HrStaffGrants` gives to HR staff and never to the `Employee` role,
 * while `AcknowledgeNotificationAsync` refuses anyone but the employee the alert was addressed to.
 * Those two rules do not overlap, so no traveller could pass the gate and no HR officer could pass
 * the service check — the same shape as the discipline authority gate, where a rule nobody can
 * reach is not a rule. This panel goes through the token-scoped `/me` route instead.
 *
 * ⚠ **Nothing here can acknowledge on someone's behalf, by construction**: no employee id is sent,
 * and the server takes the acknowledger from the token. An acknowledgement anyone can record for
 * you records nothing — this is a confirmation that a specific person read a security briefing
 * about where they are going.
 */
export function MyTravelAlertsPanel() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: alerts, isLoading } = useQuery({
    queryKey: ['my-travel-alerts'],
    queryFn: () => travelService.getMyAlerts(),
  });

  const acknowledge = useMutation({
    mutationFn: (n: StaffTravelAlertNotification) => travelService.acknowledgeMyAlert(n.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['my-travel-alerts'] });
      toast({ title: 'Thank you — that is recorded against your trip' });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Could not record that',
        description: e?.response?.data?.message ?? e?.message,
      }),
  });

  const rows = alerts ?? [];
  const outstanding = rows.filter((a) => !a.isAcknowledged);

  // Nothing to say when there is nothing to say — this sits above a trip list that is the point
  // of the page, so an empty "no alerts" card would only push it down.
  if (!isLoading && rows.length === 0) return null;

  return (
    <Card className={outstanding.length > 0 ? 'border-amber-300 dark:border-amber-900' : undefined}>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <BellRing className="h-4 w-4" />
          Alerts about your destinations
          {outstanding.length > 0 && (
            <Badge variant="destructive">{outstanding.length} to read</Badge>
          )}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading ? (
          <div className="flex justify-center py-6">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : (
          rows.map((a) => (
            <div
              key={a.id}
              className={`rounded-md border p-3 ${
                a.isAcknowledged ? '' : 'border-amber-300 bg-amber-50 dark:border-amber-900 dark:bg-amber-950/30'
              }`}
            >
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="text-sm font-medium">{a.alertTitle ?? 'A destination alert'}</p>
                  <p className="text-xs text-muted-foreground">
                    {a.requestNumber ? `Trip ${a.requestNumber}` : 'One of your trips'}
                    {a.notificationSentAt && ` · sent ${fmtDate(a.notificationSentAt)}`}
                  </p>
                </div>
                {a.isAcknowledged ? (
                  <Badge variant="secondary" className="gap-1">
                    <Check className="h-3 w-3" />
                    Read {fmtDate(a.acknowledgedAt)}
                  </Badge>
                ) : (
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={acknowledge.isPending}
                    onClick={() => acknowledge.mutate(a)}
                  >
                    {acknowledge.isPending && <Loader2 className="mr-2 h-3 w-3 animate-spin" />}
                    I have read this
                  </Button>
                )}
              </div>
            </div>
          ))
        )}
        <p className="text-xs text-muted-foreground">
          The full alert — what is happening and what to do about it — is on the trip itself, under
          its compliance tab.
        </p>
      </CardContent>
    </Card>
  );
}
