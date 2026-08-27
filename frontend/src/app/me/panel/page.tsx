'use client';

import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarCheck, CheckCircle2, Loader2, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobInterviewService } from '@/services/hr/interviews.service';

/**
 * A panelist's own diary (moved into the portal by area 25 slice 13b).
 *
 * ⚠ This is the *only* interview list a non-HR employee can read. The schedule at
 * `/hr/recruitment/interviews` is HR's and answers 403 for a panelist — measured: "Only HR can list
 * interviews by status" and "Only HR can list the interview schedule" — so their route into a
 * session has to start here. It is backed by `me/panelist-slots`, which takes the employee from the
 * token; the id-bearing twin would mean the client fetching its own employee id and passing it
 * back, which is the shape that produced this module's authorization holes.
 *
 * ⚠ The session and scorecard screens it links to stay on the desk at `/hr/recruitment/interviews/
 * {id}`. That is deliberate and is NOT a D3 violation: interview read access is enforced per
 * RECORD, not per role — `JobInterviewService.EnsureCanReadInterviewAsync` admits "HR, or a
 * panelist on THIS interview" — so a panelist following the link is authorised by the same rule
 * that put them on the panel. Duplicating the session view into the portal would mean two
 * scorecard forms against one upsert endpoint, which is how a scorecard gets silently replaced.
 *
 * An employee on no panels sees an empty list, not an error.
 */
export default function MyInterviewPanelPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const slots = useQuery({
    queryKey: ['hr', 'my-panel-slots'],
    queryFn: () => jobInterviewService.getMyPanelSlots(),
  });

  const confirm = useMutation({
    mutationFn: (panelistId: string) => jobInterviewService.confirmPanelist(panelistId),
    onSuccess: () => {
      toast({ title: 'Assignment confirmed' });
      queryClient.invalidateQueries({ queryKey: ['hr', 'my-panel-slots'] });
    },
    onError: (error: any) =>
      toast({ title: 'Could not confirm', description: error?.message, variant: 'destructive' }),
  });

  const rows = slots.data ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="My interview panel"
        description="Sessions you are sitting on, and the scorecards you owe."
        backHref="/me"
      />

      <Card>
        <CardContent className="p-0">
          {slots.isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <div className="py-10">
              <EmptyState
                icon={Users}
                title="No panel assignments"
                description="You are not on any interview panels at the moment."
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Interview</TableHead>
                  <TableHead className="w-[160px]">Your role</TableHead>
                  <TableHead className="w-[200px]">Confirmed</TableHead>
                  <TableHead className="w-[140px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((slot) => (
                  <TableRow key={slot.id}>
                    <TableCell>
                      <Link
                        href={`/hr/recruitment/interviews/${slot.jobInterviewId}`}
                        className="font-medium text-primary hover:underline"
                      >
                        {slot.interviewNumber}
                      </Link>
                      {slot.isRequired && (
                        <span className="ml-2 text-xs text-muted-foreground">attendance required</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={slot.role} />
                    </TableCell>
                    <TableCell>
                      {slot.isConfirmed ? (
                        <span className="flex items-center gap-1.5 text-sm">
                          <CheckCircle2 className="h-4 w-4 text-green-600" />
                          {formatDateTime(slot.confirmationDate)}
                        </span>
                      ) : (
                        <span className="text-sm text-muted-foreground">
                          {slot.invitationSentDate ? 'Not yet confirmed' : 'Not yet invited'}
                        </span>
                      )}
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-2">
                        {!slot.isConfirmed && (
                          <Button
                            variant="outline"
                            size="sm"
                            disabled={confirm.isPending}
                            onClick={() => confirm.mutate(slot.id)}
                          >
                            <CalendarCheck className="mr-1.5 h-4 w-4" />
                            Confirm
                          </Button>
                        )}
                        <Button variant="ghost" size="sm" asChild>
                          <Link href={`/hr/recruitment/interviews/${slot.jobInterviewId}`}>Open</Link>
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
