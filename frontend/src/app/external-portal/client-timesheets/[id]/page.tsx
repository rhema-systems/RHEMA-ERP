'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/hooks/use-toast';
import { TimesheetConfirmationCard } from '@/components/hr/client-portal/timesheet-confirmation-card';
import { clientPortalService } from '@/services/hr/client-portal.service';

/** One timesheet, reviewed and answered from inside the logged-in client portal. */
export default function ClientTimesheetDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [done, setDone] = useState<'Confirmed' | 'Rejected' | null>(null);

  const timesheet = useQuery({
    queryKey: ['client-portal', 'timesheet', id],
    queryFn: () => clientPortalService.getTimesheet(id),
    enabled: !!id,
    retry: false,
  });

  const finish = (outcome: 'Confirmed' | 'Rejected') => {
    setDone(outcome);
    queryClient.invalidateQueries({ queryKey: ['client-portal', 'dashboard'] });
    queryClient.invalidateQueries({ queryKey: ['client-portal', 'timesheet', id] });
  };

  const confirm = useMutation({
    mutationFn: (notes: string | null) => clientPortalService.confirmTimesheet(id, notes),
    onSuccess: () => finish('Confirmed'),
    onError: (e: any) =>
      toast({ title: 'Could not confirm', description: e?.message, variant: 'destructive' }),
  });

  const reject = useMutation({
    mutationFn: (notes: string) => clientPortalService.rejectTimesheet(id, notes),
    onSuccess: () => finish('Rejected'),
    onError: (e: any) =>
      toast({ title: 'Could not reject', description: e?.message, variant: 'destructive' }),
  });

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <Button asChild variant="ghost" size="sm">
        <Link href="/external-portal/client-timesheets">
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back to timesheets
        </Link>
      </Button>

      {timesheet.isLoading ? (
        <div className="flex justify-center py-16">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      ) : timesheet.isError ? (
        <Card>
          <CardHeader>
            <CardTitle>Timesheet unavailable</CardTitle>
            <CardDescription>
              {(timesheet.error as Error | undefined)?.message ??
                'This timesheet could not be loaded.'}
            </CardDescription>
          </CardHeader>
        </Card>
      ) : !timesheet.data ? null : (
        <TimesheetConfirmationCard
          data={timesheet.data}
          submitting={confirm.isPending || reject.isPending}
          done={done}
          onConfirm={(notes) => confirm.mutate(notes)}
          onReject={(notes) => reject.mutate(notes)}
        />
      )}
    </div>
  );
}
