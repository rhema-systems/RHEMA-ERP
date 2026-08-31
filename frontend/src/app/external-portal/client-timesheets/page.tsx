'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { ClipboardList, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatDate } from '@/lib/hr/attendance-format';
import { clientPortalService } from '@/services/hr/client-portal.service';

/**
 * The consultant-client contact's home: every timesheet awaiting their confirmation, one
 * section per client organisation they serve (a contact invited by several clients holds one
 * contact row — and one section — per client).
 */
export default function ClientTimesheetsHomePage() {
  const dashboard = useQuery({
    queryKey: ['client-portal', 'dashboard'],
    queryFn: () => clientPortalService.getDashboard(),
    retry: false,
  });

  if (dashboard.isLoading) {
    return (
      <div className="flex justify-center py-16">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (dashboard.isError) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Client portal</CardTitle>
          <CardDescription>
            {(dashboard.error as Error | undefined)?.message ??
              'Your client portal data could not be loaded.'}
          </CardDescription>
        </CardHeader>
      </Card>
    );
  }

  const data = dashboard.data;
  if (!data) return null;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Timesheet confirmations</h1>
          <p className="text-muted-foreground">
            {data.contactName || data.email} ·{' '}
            {data.pendingConfirmationCount === 0
              ? 'nothing awaiting your confirmation'
              : `${data.pendingConfirmationCount} timesheet${data.pendingConfirmationCount === 1 ? '' : 's'} awaiting your confirmation`}
          </p>
        </div>
        <ClipboardList className="h-8 w-8 text-muted-foreground" />
      </div>

      {data.clients.map((client) => (
        <Card key={client.consultantClientId}>
          <CardHeader>
            <div className="flex items-start justify-between gap-4">
              <div>
                <CardTitle>{client.clientName}</CardTitle>
                <CardDescription>
                  {client.clientCode}
                  {client.contactRole ? ` · ${client.contactRole}` : ''}
                </CardDescription>
              </div>
              <Badge variant={client.pendingConfirmationCount > 0 ? 'default' : 'secondary'}>
                {client.pendingConfirmationCount} pending
              </Badge>
            </div>
          </CardHeader>
          <CardContent>
            {client.pendingTimesheets.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No timesheets are awaiting confirmation for this client.
              </p>
            ) : (
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Timesheet</TableHead>
                      <TableHead>Consultant</TableHead>
                      <TableHead>Period</TableHead>
                      <TableHead className="text-right">Hours</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {client.pendingTimesheets.map((ts) => (
                      <TableRow key={ts.id}>
                        <TableCell className="font-medium">{ts.timesheetNumber}</TableCell>
                        <TableCell>{ts.consultantName}</TableCell>
                        <TableCell>
                          {formatDate(ts.periodStartDate)} – {formatDate(ts.periodEndDate)}
                        </TableCell>
                        <TableCell className="text-right">{ts.totalHours}</TableCell>
                        <TableCell className="text-right">
                          <Button asChild size="sm">
                            <Link href={`/external-portal/client-timesheets/${ts.id}`}>Review</Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
