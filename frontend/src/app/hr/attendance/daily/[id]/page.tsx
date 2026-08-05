'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, BadgeCheck, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { dailyAttendanceService } from '@/services/hr/attendance.service';
import {
  formatDate,
  formatDateTime,
  formatTime,
  formatHours,
  humanizeEnum,
} from '@/lib/hr/attendance-format';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

function InfoCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">{children}</CardContent>
    </Card>
  );
}

/**
 * One day's attendance for one employee, with the raw punches and any regularizations
 * raised against it.
 *
 * Verification and exception approval are plain supervisor actions on this record, not
 * workflow steps — the workflow in this area sits on the *regularization* that asks for a
 * change, not on the day itself.
 */
export default function DailyAttendanceDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [action, setAction] = useState<null | 'verify' | 'exception'>(null);
  const [busy, setBusy] = useState(false);
  const [notes, setNotes] = useState('');

  const { data: r, isLoading, isError } = useQuery({
    queryKey: ['hr', 'daily-attendance', id],
    queryFn: () => dailyAttendanceService.getById(id),
    enabled: !!id,
  });

  const runAction = async () => {
    if (!action) return false;
    setBusy(true);
    try {
      if (action === 'verify') {
        await dailyAttendanceService.verify(id, notes.trim() || null);
      } else {
        await dailyAttendanceService.approveException(id, notes.trim() || null);
      }
      await queryClient.invalidateQueries({ queryKey: ['hr', 'daily-attendance'] });
      toast({
        title: 'Done',
        description: action === 'verify' ? 'Attendance verified.' : 'Exception approved.',
      });
      setAction(null);
      setNotes('');
      return true;
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Action failed.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !r) {
    return (
      <div className="p-6">
        <EmptyState title="Attendance record not found" description="It may have been removed." />
      </div>
    );
  }

  const canVerify = r.requiresVerification && !r.isVerified;
  const canApproveException = r.hasException && !r.exceptionApproved;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${r.employeeName} · ${formatDate(r.attendanceDate)}`}
        description={`${r.dayOfWeek}${r.workScheduleName ? ` · ${r.workScheduleName}` : ''}`}
        backHref="/hr/attendance/daily"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={r.status} />
            {canApproveException && (
              <Button variant="outline" onClick={() => setAction('exception')}>
                <ShieldCheck className="mr-2 h-4 w-4" /> Approve exception
              </Button>
            )}
            {canVerify && (
              <Button onClick={() => setAction('verify')}>
                <BadgeCheck className="mr-2 h-4 w-4" /> Verify
              </Button>
            )}
          </div>
        }
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="punches">Punches ({r.attendanceLogs.length})</TabsTrigger>
          <TabsTrigger value="regularizations">
            Regularizations ({r.regularizations.length})
          </TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <InfoCard title="Hours">
            <InfoRow label="Scheduled start" value={formatTime(r.scheduledStartTime)} />
            <InfoRow label="Scheduled end" value={formatTime(r.scheduledEndTime)} />
            <InfoRow label="Scheduled hours" value={formatHours(r.scheduledWorkHours)} />
            <InfoRow label="Actual in" value={formatTime(r.actualCheckInTime)} />
            <InfoRow label="Actual out" value={formatTime(r.actualCheckOutTime)} />
            <InfoRow label="Worked" value={formatHours(r.actualWorkHours)} />
            <InfoRow label="Break" value={r.totalBreakMinutes ? `${r.totalBreakMinutes} min` : '—'} />
            <InfoRow
              label="Late"
              value={r.isLate ? `Yes${r.lateMinutes ? ` (${r.lateMinutes} min)` : ''}` : 'No'}
            />
            <InfoRow
              label="Early departure"
              value={
                r.isEarlyDeparture
                  ? `Yes${r.earlyDepartureMinutes ? ` (${r.earlyDepartureMinutes} min)` : ''}`
                  : 'No'
              }
            />
          </InfoCard>

          <InfoCard title="Overtime">
            <InfoRow label="Overtime" value={r.isOvertime ? 'Yes' : 'No'} />
            <InfoRow label="Overtime hours" value={formatHours(r.overtimeHours)} />
            <InfoRow label="Approved" value={r.overtimeApproved ? 'Yes' : 'No'} />
            <InfoRow label="Approved by" value={r.overtimeApprovedByName} />
          </InfoCard>

          <InfoCard title="Location & device">
            <InfoRow label="Location" value={r.locationName} />
            <InfoRow label="Check-in place" value={r.checkInLocation} />
            <InfoRow label="Check-out place" value={r.checkOutLocation} />
            <InfoRow
              label="Check-in verification"
              value={humanizeEnum(r.checkInLocationStatus)}
            />
            <InfoRow
              label="Check-out verification"
              value={humanizeEnum(r.checkOutLocationStatus)}
            />
            <InfoRow label="Geofence zone" value={r.checkInGeofenceZoneName} />
            <InfoRow label="Check-in device" value={r.checkInDevice} />
            <InfoRow label="Check-out device" value={r.checkOutDevice} />
            <InfoRow
              label="Remote work"
              value={r.isRemoteWork ? r.remoteWorkLocation || 'Yes' : 'No'}
            />
          </InfoCard>

          <InfoCard title="Period & verification">
            <InfoRow label="Pay period" value={r.payPeriodName} />
            <InfoRow label="Public holiday" value={r.publicHolidayName} />
            <InfoRow label="Requires verification" value={r.requiresVerification ? 'Yes' : 'No'} />
            <InfoRow label="Verified" value={r.isVerified ? 'Yes' : 'No'} />
            <InfoRow label="Verified by" value={r.verifiedByName} />
            <InfoRow label="Verified on" value={formatDateTime(r.verifiedDate)} />
          </InfoCard>

          {(r.hasException || r.statusReason || r.notes || r.verificationNotes) && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Notes & exceptions</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-sm">
                {r.hasException && (
                  <div>
                    <p className="text-xs text-muted-foreground">
                      Exception {r.exceptionApproved ? '(approved)' : '(open)'}
                    </p>
                    <p className="whitespace-pre-wrap">{r.exceptionReason || '—'}</p>
                    {r.exceptionApprovedByName && (
                      <p className="text-xs text-muted-foreground">
                        Approved by {r.exceptionApprovedByName}
                      </p>
                    )}
                  </div>
                )}
                <div>
                  <p className="text-xs text-muted-foreground">Status reason</p>
                  <p className="whitespace-pre-wrap">{r.statusReason || '—'}</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Verification notes</p>
                  <p className="whitespace-pre-wrap">{r.verificationNotes || '—'}</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Notes</p>
                  <p className="whitespace-pre-wrap">{r.notes || '—'}</p>
                </div>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="punches" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {r.attendanceLogs.length === 0 ? (
                <EmptyState
                  title="No punches"
                  description="This day was recorded without any device or self-service punches."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Time</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Device</TableHead>
                      <TableHead>Processed</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {r.attendanceLogs.map((log) => (
                      <TableRow key={log.id}>
                        <TableCell>{formatDateTime(log.logDateTime)}</TableCell>
                        <TableCell>{humanizeEnum(log.logType)}</TableCell>
                        <TableCell className="text-muted-foreground">
                          {log.deviceSerialNumber || '—'}
                        </TableCell>
                        <TableCell>
                          {log.isProcessed ? (
                            <Badge variant="outline">Processed</Badge>
                          ) : (
                            <Badge variant="secondary">Pending</Badge>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="regularizations" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {r.regularizations.length === 0 ? (
                <EmptyState
                  title="No regularizations"
                  description="Nobody has asked for this day to be corrected."
                  action={
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() =>
                        router.push(`/hr/attendance/regularizations/new?attendanceId=${id}`)
                      }
                    >
                      Raise a regularization
                    </Button>
                  }
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Number</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Raised</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Applied</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {r.regularizations.map((reg) => (
                      <TableRow
                        key={reg.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => router.push(`/hr/attendance/regularizations/${reg.id}`)}
                      >
                        <TableCell className="font-medium">{reg.regularizationNumber}</TableCell>
                        <TableCell>{humanizeEnum(reg.type)}</TableCell>
                        <TableCell>{formatDateTime(reg.requestDate)}</TableCell>
                        <TableCell>
                          <StatusBadge status={reg.status} />
                        </TableCell>
                        <TableCell>{reg.isApplied ? 'Yes' : 'No'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={action !== null}
        onOpenChange={(open) => !open && setAction(null)}
        title={action === 'verify' ? 'Verify this attendance?' : 'Approve the exception?'}
        description={
          action === 'verify'
            ? 'Confirms the recorded hours are correct so the day can roll into the monthly summary.'
            : 'Accepts the exception on this day so it stops blocking the summary.'
        }
        confirmText={action === 'verify' ? 'Verify' : 'Approve'}
        isLoading={busy}
        onConfirm={runAction}
      >
        <div className="space-y-2">
          <Label htmlFor="actionNotes">Notes</Label>
          <Textarea
            id="actionNotes"
            rows={3}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="Optional"
          />
        </div>
      </ConfirmationDialog>
    </div>
  );
}
