'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { CalendarClock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { medicalMeService } from '@/services/hr/medical-me.service';

/**
 * My Appointments (area 25 slice 8, spec #29) — built from nothing on the new
 * `api/medical/me/appointments` self arm; the census found no self-shaped appointment read.
 *
 * Read-only by design: appointments are booked by the clinic, so there is no create here —
 * corrections go through the medical desk. A row opens the full detail (purpose, physician,
 * outcome) fetched by id; somebody else's id simply does not resolve.
 */
const fmtDateTime = (v: string) => new Date(v).toLocaleString();
const fmtTime = (v?: string | null) => (v ? new Date(v).toLocaleTimeString() : '—');

function StatusBadge({ status, statusName }: { status: string; statusName: string }) {
  if (status === 'Cancelled' || status === 'NoShow')
    return <Badge variant="destructive">{statusName}</Badge>;
  if (status === 'Completed') return <Badge variant="outline">{statusName}</Badge>;
  return <Badge variant="secondary">{statusName}</Badge>;
}

export default function MyAppointmentsPage() {
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const { data: appointments = [], isLoading } = useQuery({
    queryKey: ['me', 'medical', 'appointments'],
    queryFn: () => medicalMeService.getAppointments(),
  });

  const { data: detail } = useQuery({
    queryKey: ['me', 'medical', 'appointments', selectedId],
    queryFn: () => medicalMeService.getAppointment(selectedId as string),
    enabled: !!selectedId,
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Appointments"
        description="Clinic appointments booked for you. To change or cancel one, contact the clinic or HR."
        backHref="/me/medical"
      />

      {isLoading ? null : appointments.length === 0 ? (
        <EmptyState
          icon={CalendarClock}
          title="No appointments"
          description="Appointments the clinic books for you will appear here."
        />
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>When</TableHead>
                  <TableHead>Facility</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {appointments.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell className="font-mono text-sm">{a.appointmentNumber}</TableCell>
                    <TableCell>{fmtDateTime(a.appointmentDateTime)}</TableCell>
                    <TableCell>{a.facilityName}</TableCell>
                    <TableCell>
                      <StatusBadge status={a.status} statusName={a.statusName} />
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" onClick={() => setSelectedId(a.id)}>
                        Details
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      <Dialog open={!!selectedId} onOpenChange={(open) => !open && setSelectedId(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              Appointment{' '}
              <span className="font-mono text-base">{detail?.appointmentNumber ?? ''}</span>
            </DialogTitle>
          </DialogHeader>
          {detail && (
            <div className="space-y-3 text-sm">
              <div className="grid gap-x-6 gap-y-2 sm:grid-cols-2">
                <div>
                  <p className="text-muted-foreground">When</p>
                  <p>{fmtDateTime(detail.appointmentDateTime)}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Status</p>
                  <StatusBadge status={detail.status} statusName={detail.statusName} />
                </div>
                <div>
                  <p className="text-muted-foreground">Facility</p>
                  <p>{detail.facilityName || '—'}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Physician</p>
                  <p>{detail.physicianName || 'Not assigned'}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Service</p>
                  <p>{detail.serviceTypeName}</p>
                </div>
                {detail.isForDependent && (
                  <div>
                    <p className="text-muted-foreground">For dependant</p>
                    <p>{detail.dependentName ?? '—'}</p>
                  </div>
                )}
                {detail.durationMinutes != null && (
                  <div>
                    <p className="text-muted-foreground">Duration</p>
                    <p>{detail.durationMinutes} min</p>
                  </div>
                )}
                {(detail.checkInTime || detail.checkOutTime) && (
                  <div>
                    <p className="text-muted-foreground">Checked in / out</p>
                    <p>
                      {fmtTime(detail.checkInTime)} / {fmtTime(detail.checkOutTime)}
                    </p>
                  </div>
                )}
              </div>
              <div>
                <p className="text-muted-foreground">Purpose</p>
                <p>{detail.purpose}</p>
              </div>
              {detail.outcomeSummary && (
                <div>
                  <p className="text-muted-foreground">Outcome</p>
                  <p>{detail.outcomeSummary}</p>
                </div>
              )}
              {detail.cancellationReason && (
                <div>
                  <p className="text-muted-foreground">Cancellation reason</p>
                  <p>{detail.cancellationReason}</p>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
