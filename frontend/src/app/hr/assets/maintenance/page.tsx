'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { CalendarClock, Loader2, Wrench } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import type { AssetMaintenanceDueItem } from '@/types/hr/assets';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtNum = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 2 });

/** Signed, and server-computed. Negative is late. */
function DaysCell({ days }: { days: number | null }) {
  if (days === null) return <span className="text-muted-foreground">—</span>;
  if (days < 0) {
    return (
      <span className="font-medium text-red-600 dark:text-red-500">
        {Math.abs(days)} days overdue
      </span>
    );
  }
  return <span className={days <= 7 ? 'text-amber-600 dark:text-amber-500' : ''}>in {days} days</span>;
}

function WatchlistTable({
  rows, showDays = true, emptyTitle, emptyBody,
}: {
  rows: AssetMaintenanceDueItem[];
  showDays?: boolean;
  emptyTitle: string;
  emptyBody: string;
}) {
  if (rows.length === 0) {
    return <EmptyState icon={Wrench} title={emptyTitle} description={emptyBody} />;
  }
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Asset</TableHead>
          <TableHead>Type</TableHead>
          <TableHead>Held by</TableHead>
          <TableHead>Unit</TableHead>
          <TableHead>Interval</TableHead>
          <TableHead>Last serviced</TableHead>
          <TableHead>Next due</TableHead>
          {showDays && <TableHead>When</TableHead>}
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((r) => (
          <TableRow key={r.id}>
            <TableCell>
              <Link href={`/hr/assets/register/${r.id}`} className="hover:underline">
                <div className="font-medium">{r.assetName}</div>
                <div className="text-xs text-muted-foreground">{r.assetNumber}</div>
              </Link>
            </TableCell>
            <TableCell>{r.assetTypeName}</TableCell>
            <TableCell>
              {r.currentAssignedToName ?? <span className="text-muted-foreground">In store</span>}
            </TableCell>
            <TableCell>{r.unitName ?? <span className="text-muted-foreground">Not set</span>}</TableCell>
            <TableCell>{r.maintenanceIntervalDays ? `${r.maintenanceIntervalDays} days` : '—'}</TableCell>
            <TableCell>{fmtDate(r.lastMaintenanceDate)}</TableCell>
            <TableCell>
              {r.isScheduled
                ? fmtDate(r.nextMaintenanceDate)
                : <span className="text-amber-600 dark:text-amber-500">Never scheduled</span>}
            </TableCell>
            {showDays && <TableCell><DaysCell days={r.daysRemaining} /></TableCell>}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * Servicing — the log, and the three lists of what has slipped — AST-1.
 *
 * ⚠ **"Due" is INCLUSIVE of "overdue", deliberately.** A list opened by the question "what should I
 * book this month?" must not hide the rows that have already lapsed. The overdue tab is that same
 * population cut out, because "what have we let slip" is a different job for a different person,
 * and eleven overdue rows inside two hundred due ones are lost rows. The counts on the register
 * report are the other way round — disjoint — for the opposite reason: three numbers in a row get
 * added up. Both are right; neither is a rounding of the other.
 *
 * The third list is the one a monitoring feature must not lose: every other maintenance read
 * requires a next-service date, so an asset flagged as needing servicing that nobody ever dated
 * appeared nowhere at all and could never become due.
 */
export default function AssetMaintenancePage() {
  const [daysAhead, setDaysAhead] = useState(30);

  const { data: due = [], isLoading: loadingDue } = useQuery({
    queryKey: ['hr', 'assets', 'maintenance', 'due', daysAhead],
    queryFn: () => assetRegisterService.getDueMaintenance(daysAhead),
  });
  const { data: overdue = [], isLoading: loadingOverdue } = useQuery({
    queryKey: ['hr', 'assets', 'maintenance', 'overdue'],
    queryFn: () => assetRegisterService.getOverdueMaintenance(),
  });
  const { data: unscheduled = [], isLoading: loadingUnscheduled } = useQuery({
    queryKey: ['hr', 'assets', 'maintenance', 'unscheduled'],
    queryFn: () => assetRegisterService.getUnscheduledMaintenance(),
  });
  const { data: log, isLoading: loadingLog } = useQuery({
    queryKey: ['hr', 'assets', 'maintenance', 'log'],
    queryFn: () => assetRegisterService.getMaintenancePaged({ pageSize: 50 }),
  });

  const asOf = due[0]?.asOf ?? overdue[0]?.asOf ?? null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Maintenance"
        description="The service log, and the three lists of what has slipped."
        backHref="/hr/assets"
      />

      {asOf && (
        <p className="text-sm text-muted-foreground">
          Answered by the server as at {fmtDate(asOf)}.
        </p>
      )}

      <Tabs defaultValue="due">
        <TabsList>
          <TabsTrigger value="due">Due ({due.length})</TabsTrigger>
          <TabsTrigger value="overdue">Overdue ({overdue.length})</TabsTrigger>
          <TabsTrigger value="unscheduled">Never scheduled ({unscheduled.length})</TabsTrigger>
          <TabsTrigger value="log">Service log</TabsTrigger>
        </TabsList>

        <TabsContent value="due" className="space-y-4 pt-4">
          <Card>
            <CardContent className="flex flex-wrap items-end gap-4 p-4">
              <div className="space-y-2">
                <Label>Looking ahead</Label>
                <Input
                  type="number"
                  min={1}
                  className="w-28"
                  value={daysAhead}
                  onChange={(e) => setDaysAhead(Math.max(1, Number(e.target.value) || 1))}
                />
              </div>
              <p className="pb-2 text-sm text-muted-foreground">
                days. This list <span className="font-medium">includes</span> assets already overdue —
                a plan that hid them would be planning around a fiction.
              </p>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-0">
              {loadingDue ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (
                <WatchlistTable
                  rows={due}
                  emptyTitle="Nothing due"
                  emptyBody={`No asset needs servicing inside the next ${daysAhead} days.`}
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="overdue" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {loadingOverdue ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (
                <WatchlistTable
                  rows={overdue}
                  emptyTitle="Nothing overdue"
                  emptyBody="Every scheduled service is still ahead of its date."
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="unscheduled" className="space-y-4 pt-4">
          <Card>
            <CardContent className="p-4 text-sm text-muted-foreground">
              These assets are flagged as needing regular servicing and have never been given a date.
              Every other maintenance list requires one, so until somebody sets it they appear
              nowhere and can never become due.
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-0">
              {loadingUnscheduled ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (
                <WatchlistTable
                  rows={unscheduled}
                  showDays={false}
                  emptyTitle="Everything is scheduled"
                  emptyBody="Every asset that needs regular servicing has a date."
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="log" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {loadingLog ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (log?.items ?? []).length === 0 ? (
                <EmptyState icon={CalendarClock} title="Nothing logged"
                  description="No service has been recorded against any asset." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Job</TableHead>
                      <TableHead>Asset</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Cost</TableHead>
                      <TableHead>Workshop</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(log?.items ?? []).map((m) => (
                      <TableRow key={m.id}>
                        <TableCell>{m.maintenanceNumber}</TableCell>
                        <TableCell>
                          <Link href={`/hr/assets/register/${m.assetId}`} className="hover:underline">
                            {m.assetName}
                          </Link>
                          <div className="text-xs text-muted-foreground">{m.assetNumber}</div>
                        </TableCell>
                        <TableCell>{fmtDate(m.maintenanceDate)}</TableCell>
                        <TableCell>{m.typeName}</TableCell>
                        <TableCell><StatusBadge status={m.statusName} /></TableCell>
                        <TableCell className="text-right">{fmtNum(m.cost)}</TableCell>
                        <TableCell>
                          {m.isAtWorkshop
                            ? m.maintenanceAdmissionNumber ?? 'At the workshop'
                            : <span className="text-muted-foreground">—</span>}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
