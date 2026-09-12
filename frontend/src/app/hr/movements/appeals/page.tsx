'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Gavel, Clock } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
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
import { movementSubtypeService } from '@/services/hr/movement-subtype.service';
import type { StaffDemotionDetail } from '@/types/hr/movement-subtypes';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const daysLeft = (deadline?: string | null) => {
  if (!deadline) return null;
  const ms = new Date(deadline).getTime() - Date.now();
  return Math.ceil(ms / 86_400_000);
};

type View = 'filed' | 'awaiting';

/**
 * Demotion appeals — what employees have said, and who has yet to say anything.
 *
 * ⚠ **Two lists, not one filtered list.** `pending-appeals` selects demotions with NO employee
 * response, so answering removes a demotion from it. A filed appeal therefore appeared in no list
 * at all until `filed-appeals` was added (ledger D-37), and HR could only find one by opening that
 * movement. The two queues are disjoint by construction: every demotion granting a right of appeal
 * sits in exactly one of them.
 *
 * Nothing is decided here. An appeal is answered on the movement, where the decision belongs with
 * the record it changes; this screen exists so nobody has to know an appeal was filed in order to
 * find it.
 */
export default function DemotionAppealsPage() {
  const [view, setView] = useState<View>('filed');

  const { data: filed = [], isLoading: loadingFiled } = useQuery({
    queryKey: ['hr', 'demotions', 'filed-appeals'],
    queryFn: () => movementSubtypeService.getFiledAppeals(),
  });

  const { data: awaiting = [], isLoading: loadingAwaiting } = useQuery({
    queryKey: ['hr', 'demotions', 'pending-appeals'],
    queryFn: () => movementSubtypeService.getPendingAppeals(),
  });

  const isLoading = view === 'filed' ? loadingFiled : loadingAwaiting;
  const rows: StaffDemotionDetail[] = view === 'filed' ? filed : awaiting;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Demotion appeals"
        description="What employees have said about a demotion notice, and who has yet to answer."
        backHref="/hr/movements"
      />

      <div className="flex flex-wrap gap-2">
        <Button
          variant={view === 'filed' ? 'default' : 'outline'}
          onClick={() => setView('filed')}
        >
          <Gavel className="mr-2 h-4 w-4" />
          Answered ({filed.length})
        </Button>
        <Button
          variant={view === 'awaiting' ? 'default' : 'outline'}
          onClick={() => setView('awaiting')}
        >
          <Clock className="mr-2 h-4 w-4" />
          Awaiting a response ({awaiting.length})
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>
            {view === 'filed' ? 'Answered' : 'Awaiting a response'}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex justify-center py-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              title={view === 'filed' ? 'No appeals or acceptances yet' : 'Nobody is being waited on'}
              description={
                view === 'filed'
                  ? 'Nothing has been answered. Employees answer a demotion notice from their own movements page.'
                  : 'Every demotion carrying a right of appeal has been answered.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Movement</TableHead>
                  <TableHead>Reason</TableHead>
                  <TableHead>Notified</TableHead>
                  <TableHead>{view === 'filed' ? 'Answered' : 'Deadline'}</TableHead>
                  <TableHead>{view === 'filed' ? 'What they said' : 'Time left'}</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((d) => {
                  const left = daysLeft(d.appealDeadline);
                  return (
                    <TableRow key={d.id}>
                      <TableCell>
                        <div className="font-medium">{d.employeeName || '—'}</div>
                        {d.employeeNumber && (
                          <div className="text-xs text-muted-foreground">{d.employeeNumber}</div>
                        )}
                      </TableCell>
                      <TableCell className="font-medium">{d.movementNumber || '—'}</TableCell>
                      <TableCell>
                        <div>{d.reasonName}</div>
                        {d.isDisciplinaryAction && (
                          <Badge variant="outline" className="mt-1">
                            Disciplinary
                          </Badge>
                        )}
                        {d.isPerformanceRelated && (
                          <Badge variant="outline" className="mt-1">
                            Performance
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell>{fmtDate(d.notificationDate)}</TableCell>
                      <TableCell>
                        {view === 'filed' ? fmtDate(d.employeeResponseDate) : fmtDate(d.appealDeadline)}
                      </TableCell>
                      <TableCell className="max-w-md">
                        {view === 'filed' ? (
                          <span className="text-sm">{d.employeeResponse ?? '—'}</span>
                        ) : left === null ? (
                          <span className="text-sm text-muted-foreground">No deadline set</span>
                        ) : (
                          <Badge variant={left <= 3 ? 'destructive' : 'outline'}>
                            {left} day{left === 1 ? '' : 's'}
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button asChild variant="ghost" size="sm">
                          <Link href={`/hr/movements/${d.movementId}`}>Open movement</Link>
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
