'use client';

import { useState } from 'react';
import { CheckCircle2, Clock, Loader2, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { formatDate } from '@/lib/hr/attendance-format';
import type { ClientTimesheetConfirmationPublic } from '@/types/hr/consultant';

/** "09:30:00" → "09:30" — TimeOnly serialises with seconds nobody billed by. */
const shortTime = (t: string) => (t?.length >= 5 ? t.slice(0, 5) : t);

const statusVariant = (status: string): 'default' | 'secondary' | 'destructive' | 'outline' =>
  status === 'Confirmed' ? 'default' : status === 'Rejected' ? 'destructive' : 'secondary';

/**
 * The timesheet-review body shared by the two client doors: the logged-in portal detail page
 * and the anonymous emailed-link page. The data shape is the same public DTO either way; only
 * the submit transport differs, so the parents own the mutations.
 */
export function TimesheetConfirmationCard({
  data,
  submitting,
  done,
  onConfirm,
  onReject,
}: {
  data: ClientTimesheetConfirmationPublic;
  submitting: boolean;
  /** Set after a successful response so the card locks and reports the outcome. */
  done: 'Confirmed' | 'Rejected' | null;
  onConfirm: (notes: string | null) => void;
  onReject: (notes: string) => void;
}) {
  const [notes, setNotes] = useState('');
  const [rejectError, setRejectError] = useState<string | null>(null);

  const effectiveStatus = done ?? data.status;
  const canRespond = !done && data.canRespond;

  return (
    <Card>
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div>
            <CardTitle>Timesheet {data.timesheetNumber}</CardTitle>
            <CardDescription>
              {data.consultantName} · {data.clientName}
            </CardDescription>
          </div>
          <Badge variant={statusVariant(effectiveStatus)}>{effectiveStatus}</Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
          <div>
            <p className="text-muted-foreground">Period</p>
            <p className="font-medium">
              {formatDate(data.periodStartDate)} – {formatDate(data.periodEndDate)}
            </p>
          </div>
          <div>
            <p className="text-muted-foreground">Total hours</p>
            <p className="font-medium">{data.totalHours}</p>
          </div>
          <div>
            <p className="text-muted-foreground">Respond by</p>
            <p className="font-medium">{formatDate(data.tokenExpiryDate)}</p>
          </div>
        </div>

        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>Start</TableHead>
                <TableHead>End</TableHead>
                <TableHead>Break</TableHead>
                <TableHead className="text-right">Hours</TableHead>
                <TableHead>Activity</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.entries.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-muted-foreground">
                    No entries recorded on this timesheet.
                  </TableCell>
                </TableRow>
              ) : (
                data.entries.map((entry) => (
                  <TableRow key={entry.id}>
                    <TableCell>{formatDate(entry.workDate)}</TableCell>
                    <TableCell>{shortTime(entry.startTime)}</TableCell>
                    <TableCell>{shortTime(entry.endTime)}</TableCell>
                    <TableCell>{entry.breakMinutes} min</TableCell>
                    <TableCell className="text-right">{entry.totalHours}</TableCell>
                    <TableCell className="max-w-[280px]">
                      <span className="line-clamp-2">{entry.activitySummary}</span>
                      {entry.location ? (
                        <span className="block text-xs text-muted-foreground">{entry.location}</span>
                      ) : null}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>

        {done ? (
          <div className="flex items-center gap-2 rounded-md border p-3 text-sm">
            {done === 'Confirmed' ? (
              <CheckCircle2 className="h-4 w-4 text-green-600" />
            ) : (
              <XCircle className="h-4 w-4 text-red-600" />
            )}
            <span>
              {done === 'Confirmed'
                ? 'Thank you — this timesheet has been confirmed.'
                : 'This timesheet has been rejected and sent back to the consultant firm.'}
            </span>
          </div>
        ) : data.isExpired ? (
          <div className="flex items-center gap-2 rounded-md border p-3 text-sm text-muted-foreground">
            <Clock className="h-4 w-4" />
            <span>
              This confirmation request has expired. Please ask your consultant firm contact to
              resend it.
            </span>
          </div>
        ) : !data.canRespond ? (
          <div className="flex items-center gap-2 rounded-md border p-3 text-sm text-muted-foreground">
            <CheckCircle2 className="h-4 w-4" />
            <span>This timesheet has already been responded to.</span>
          </div>
        ) : null}

        {canRespond ? (
          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label htmlFor="client-notes">Notes {`(required when rejecting)`}</Label>
              <Textarea
                id="client-notes"
                value={notes}
                onChange={(e) => {
                  setNotes(e.target.value);
                  if (rejectError) setRejectError(null);
                }}
                maxLength={2000}
                placeholder="Any comments on the recorded hours…"
                rows={3}
              />
              {rejectError ? <p className="text-sm text-destructive">{rejectError}</p> : null}
            </div>
            <div className="flex gap-2">
              <Button disabled={submitting} onClick={() => onConfirm(notes.trim() || null)}>
                {submitting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Confirm timesheet
              </Button>
              <Button
                variant="destructive"
                disabled={submitting}
                onClick={() => {
                  if (!notes.trim()) {
                    setRejectError('Please explain what is wrong before rejecting.');
                    return;
                  }
                  onReject(notes.trim());
                }}
              >
                Reject
              </Button>
            </div>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}
