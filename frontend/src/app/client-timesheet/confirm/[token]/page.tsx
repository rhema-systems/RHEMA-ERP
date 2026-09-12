'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Loader2, MailWarning } from 'lucide-react';
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/hooks/use-toast';
import { TimesheetConfirmationCard } from '@/components/hr/client-portal/timesheet-confirmation-card';
import { publicTimesheetConfirmationService } from '@/services/hr/client-portal.service';

/**
 * The anonymous timesheet-confirmation page that confirmation emails link to
 * (`{PortalUrl}/client-timesheet/confirm/{token}` — the path is fixed by the email template in
 * ConsultantServices, so this page lives exactly there). The GUID token is the whole
 * authority: no login, single response, regenerated on every resend. A contact with a portal
 * account sees the same timesheet inside the portal; this page is for the one who has not
 * completed an invite — the door consultant billing cannot do without.
 */
export default function PublicTimesheetConfirmationPage() {
  const params = useParams<{ token: string }>();
  const token = params?.token ?? '';
  const { toast } = useToast();
  const [done, setDone] = useState<'Confirmed' | 'Rejected' | null>(null);

  const validation = useQuery({
    queryKey: ['client-timesheet-confirmation', token],
    queryFn: () => publicTimesheetConfirmationService.validate(token),
    enabled: !!token,
    retry: false,
  });

  const confirm = useMutation({
    mutationFn: (notes: string | null) => publicTimesheetConfirmationService.confirm(token, notes),
    onSuccess: () => setDone('Confirmed'),
    onError: (e: any) =>
      toast({ title: 'Could not confirm', description: e?.message, variant: 'destructive' }),
  });

  const reject = useMutation({
    mutationFn: (notes: string) => publicTimesheetConfirmationService.reject(token, notes),
    onSuccess: () => setDone('Rejected'),
    onError: (e: any) =>
      toast({ title: 'Could not reject', description: e?.message, variant: 'destructive' }),
  });

  return (
    <div className="min-h-screen bg-gray-50 px-4 py-10">
      <div className="mx-auto max-w-4xl space-y-4">
        <div>
          <h1 className="text-2xl font-semibold">Timesheet confirmation</h1>
          <p className="text-muted-foreground">
            Review the consultant&apos;s recorded hours and confirm or reject them.
          </p>
        </div>

        {!token || validation.isError ? (
          <Card>
            <CardHeader>
              <div className="flex items-center gap-2">
                <MailWarning className="h-5 w-5 text-muted-foreground" />
                <CardTitle>This link is not valid</CardTitle>
              </div>
              <CardDescription>
                {(validation.error as Error | undefined)?.message ??
                  'The confirmation link is invalid or has expired. Please ask your consultant firm contact to resend it.'}
              </CardDescription>
            </CardHeader>
          </Card>
        ) : validation.isLoading ? (
          <div className="flex justify-center py-16">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        ) : !validation.data ? null : (
          <TimesheetConfirmationCard
            data={validation.data}
            submitting={confirm.isPending || reject.isPending}
            done={done}
            onConfirm={(notes) => confirm.mutate(notes)}
            onReject={(notes) => reject.mutate(notes)}
          />
        )}
      </div>
    </div>
  );
}
