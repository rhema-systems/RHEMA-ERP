'use client';

/**
 * Area 25 slice 7 — my acting appointments: spec destination #21, built fresh (census
 * verdict E; /hr/movements/acting is the HR register and stays). Read-only — acting
 * appointments are raised and ended by the desk; this is the subject's own view of them.
 */

import { useQuery } from '@tanstack/react-query';
import { UserCog } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { mePortalService } from '@/services/hr/me-portal.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyActingAppointmentsPage() {
  const { data: rows, isLoading } = useQuery({
    queryKey: ['me', 'movements', 'acting'],
    queryFn: () => mePortalService.getActingAppointments(),
  });

  const list = rows ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Acting Appointments"
        description="Positions you hold, or have held, in an acting capacity."
        backHref="/me/movements"
      />

      <Card>
        <CardHeader>
          <CardTitle>Appointments</CardTitle>
          <CardDescription>
            An acting role is temporary by design — its end is extended, made permanent, or
            processed as a return by HR.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Acting as</TableHead>
                  <TableHead>Covering for</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead>Allowance</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(6)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : list.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={UserCog}
                        title="No acting appointments"
                        description="You have not been appointed to act in another position."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  list.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell className="font-mono text-xs">{a.appointmentNumber}</TableCell>
                      <TableCell className="font-medium">{a.actingPositionTitle}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {a.actingForEmployeeName ?? '—'}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {fmtDate(a.startDate)} – {a.endDate ? fmtDate(a.endDate) : 'open'}
                      </TableCell>
                      <TableCell>
                        {a.receivesActingAllowance ? (
                          <Badge variant="secondary">
                            {typeof a.actingAllowance === 'number'
                              ? a.actingAllowance.toLocaleString()
                              : 'Yes'}
                          </Badge>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={a.statusName ?? a.status} />
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
