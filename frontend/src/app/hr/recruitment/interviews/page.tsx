'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { CalendarDays, Loader2, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { dateOffset, formatDate, formatTime, humanizeEnum, today } from '@/lib/hr/attendance-format';
import { jobInterviewService } from '@/services/hr/interviews.service';
import { INTERVIEW_STATUSES, type JobInterviewStatus } from '@/types/hr/interviews';

type Mode = 'range' | 'status';

/**
 * HR's interview schedule.
 *
 * ⚠ There is no general paged search on this controller — only purpose-built reads (by date range,
 * by status, by vacancy, by round). The date range is the default because a schedule is a diary,
 * and `from`/`to` are required by the API rather than optional.
 *
 * A panelist gets a 403 from every read on this page; their sessions are at `/hr/recruitment/my-panel`.
 */
export default function InterviewsPage() {
  const [mode, setMode] = useState<Mode>('range');
  const [from, setFrom] = useState(dateOffset(-7));
  const [to, setTo] = useState(dateOffset(30));
  const [status, setStatus] = useState<JobInterviewStatus>('Scheduled');

  const range = useQuery({
    queryKey: ['hr', 'interviews', 'range', from, to],
    queryFn: () => jobInterviewService.getByDateRange(from, to),
    enabled: mode === 'range' && !!from && !!to,
  });

  const byStatus = useQuery({
    queryKey: ['hr', 'interviews', 'status', status],
    queryFn: () => jobInterviewService.getByStatus(status),
    enabled: mode === 'status',
  });

  const active = mode === 'range' ? range : byStatus;
  const rows = active.data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Interviews"
        description="Scheduled sessions, their panels and the candidates booked into them."
        backHref="/hr/recruitment"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" asChild>
              <Link href="/hr/recruitment/my-panel">
                <Users className="mr-1.5 h-4 w-4" />
                My panel
              </Link>
            </Button>
            <Button asChild>
              <Link href="/hr/recruitment/interviews/new">Schedule an interview</Link>
            </Button>
          </div>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-3 p-4">
          <div className="space-y-1.5">
            <Label className="text-xs text-muted-foreground">View</Label>
            <Select value={mode} onValueChange={(v) => setMode(v as Mode)}>
              <SelectTrigger className="w-[180px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="range">By date</SelectItem>
                <SelectItem value="status">By status</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {mode === 'range' ? (
            <>
              <div className="space-y-1.5">
                <Label htmlFor="from" className="text-xs text-muted-foreground">
                  From
                </Label>
                <Input
                  id="from"
                  type="date"
                  value={from}
                  max={to}
                  onChange={(e) => setFrom(e.target.value)}
                  className="w-[170px]"
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="to" className="text-xs text-muted-foreground">
                  To
                </Label>
                <Input
                  id="to"
                  type="date"
                  value={to}
                  min={from}
                  onChange={(e) => setTo(e.target.value)}
                  className="w-[170px]"
                />
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setFrom(today());
                  setTo(dateOffset(14));
                }}
              >
                Next two weeks
              </Button>
            </>
          ) : (
            <div className="space-y-1.5">
              <Label className="text-xs text-muted-foreground">Status</Label>
              <Select value={status} onValueChange={(v) => setStatus(v as JobInterviewStatus)}>
                <SelectTrigger className="w-[200px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {INTERVIEW_STATUSES.map((st) => (
                    <SelectItem key={st} value={st}>
                      {humanizeEnum(st)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {active.isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <div className="py-10">
              <EmptyState
                icon={CalendarDays}
                title="No interviews"
                description={
                  mode === 'range'
                    ? 'Nothing scheduled in this window.'
                    : `No interviews are ${humanizeEnum(status).toLowerCase()}.`
                }
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Reference</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>When</TableHead>
                  <TableHead>Candidates</TableHead>
                  <TableHead>Panel</TableHead>
                  <TableHead className="w-[130px]">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((interview) => (
                  <TableRow key={interview.id} className="cursor-pointer">
                    <TableCell>
                      <Link
                        href={`/hr/recruitment/interviews/${interview.id}`}
                        className="font-medium text-primary hover:underline"
                      >
                        {interview.interviewNumber}
                      </Link>
                      <div className="text-xs text-muted-foreground">
                        Round {interview.round} · {humanizeEnum(interview.type)}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{interview.jobTitle || '—'}</div>
                      <div className="text-xs text-muted-foreground">{interview.vacancyNumber}</div>
                    </TableCell>
                    <TableCell>
                      <div>{formatDate(interview.scheduledDate)}</div>
                      <div className="text-xs text-muted-foreground">
                        {formatTime(interview.startTime)} – {formatTime(interview.endTime)}
                      </div>
                    </TableCell>
                    <TableCell className="max-w-[220px]">
                      {interview.candidateNames.length === 0 ? (
                        <span className="text-muted-foreground">None booked</span>
                      ) : (
                        <span className="text-sm">{interview.candidateNames.join(', ')}</span>
                      )}
                    </TableCell>
                    <TableCell className="max-w-[220px]">
                      {interview.panelistNames.length === 0 ? (
                        <span className="text-muted-foreground">No panel</span>
                      ) : (
                        <span className="text-sm">{interview.panelistNames.join(', ')}</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={interview.status} />
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
