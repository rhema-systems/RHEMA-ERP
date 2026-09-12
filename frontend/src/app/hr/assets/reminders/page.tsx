'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BellRing, Loader2, Play } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

const KIND_LABELS: Record<string, string> = {
  MaintenanceDueSoon: 'Service due',
  MaintenanceOverdue: 'Service overdue',
  MaintenanceUnscheduled: 'Never scheduled',
  InsuranceExpiringSoon: 'Cover lapsing',
  InsuranceExpired: 'Cover lapsed',
  InsuranceUndated: 'Cover never dated',
  ReturnDueSoon: 'Return due',
  ReturnOverdue: 'Return overdue',
};

/**
 * The reminder sweep — AST-1, extended in slice 11 to insurance and returns.
 *
 * Eight rungs over three subjects. A daily background service runs the same sweep; running it here
 * exists so HR can force one after a bulk edit instead of waiting for tomorrow, and so the engine is
 * provable end to end. Repeating it is safe — dispatch is deduped per item.
 *
 * ⚠ **The engine has horizons the watchlist screens do not**: a 30-day due window and a 90-day
 * backlog floor, both measured back from the run date. So a preview at a far-future date queues
 * *nothing*, and a list screen can legitimately show an asset the sweep is not chasing. That is the
 * engine working, not failing, and three separate assertions read as "the engine is not chasing it"
 * before that was understood.
 *
 * ⚠ The return rungs key on the **assignment**, not the asset — a replacement laptop would
 * otherwise share a dedupe key with the one it replaced.
 */
export default function AssetRemindersPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [asOf, setAsOf] = useState('');

  const { data: preview = [], isLoading: loadingPreview } = useQuery({
    queryKey: ['hr', 'assets', 'reminders', 'preview', asOf],
    queryFn: () => assetRegisterService.previewReminders(asOf || undefined),
  });
  const { data: runs = [], isLoading: loadingRuns } = useQuery({
    queryKey: ['hr', 'assets', 'reminders', 'runs'],
    queryFn: () => assetRegisterService.getReminderRuns(20),
  });
  const { data: log = [], isLoading: loadingLog } = useQuery({
    queryKey: ['hr', 'assets', 'reminders', 'log'],
    queryFn: () => assetRegisterService.getReminderLog(14),
  });

  const run = useMutation({
    mutationFn: () => assetRegisterService.runReminderSweep(),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets', 'reminders'] });
      toast({
        title: 'Sweep finished',
        description: `${result.remindersQueued} queued, ${result.skippedAsAlreadySent} already sent.`,
      });
    },
    onError: (e: Error) =>
      toast({ title: 'The sweep failed', description: e.message, variant: 'destructive' }),
  });

  const fresh = preview.filter((p) => !p.alreadySent).length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Asset reminders"
        description="What the sweep would chase, and what it has chased."
        backHref="/hr/assets"
        actions={
          <Button onClick={() => run.mutate()} disabled={run.isPending}>
            {run.isPending
              ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              : <Play className="mr-2 h-4 w-4" />}
            Run a sweep now
          </Button>
        }
      />

      <Tabs defaultValue="preview">
        <TabsList>
          <TabsTrigger value="preview">Would fire ({preview.length})</TabsTrigger>
          <TabsTrigger value="runs">Runs ({runs.length})</TabsTrigger>
          <TabsTrigger value="log">Sent ({log.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="preview" className="space-y-4 pt-4">
          <Card>
            <CardContent className="flex flex-wrap items-end gap-4 p-4">
              <div className="space-y-2">
                <Label>Preview as at</Label>
                <Input type="date" className="w-48" value={asOf}
                  onChange={(e) => setAsOf(e.target.value)} />
              </div>
              <p className="pb-2 flex-1 text-sm text-muted-foreground">
                Changes nothing and claims no dedupe key. The sweep looks 30 days ahead and no more
                than 90 days back from this date, so a far-future date correctly returns nothing —
                the engine has horizons the watchlist screens do not.
                {' '}<span className="font-medium">{fresh}</span> of these {preview.length} would be
                newly sent; the rest were already claimed by an earlier run.
              </p>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-0">
              {loadingPreview ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : preview.length === 0 ? (
                <EmptyState icon={BellRing} title="Nothing to chase"
                  description="No asset or custody falls inside the sweep's horizons at that date." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>What</TableHead>
                      <TableHead>Subject</TableHead>
                      <TableHead>Reference</TableHead>
                      <TableHead>Due</TableHead>
                      <TableHead>When</TableHead>
                      <TableHead>Escalation</TableHead>
                      <TableHead>New?</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {preview.map((p) => (
                      <TableRow key={p.dedupeKey}>
                        <TableCell>{KIND_LABELS[p.kind] ?? p.kind}</TableCell>
                        <TableCell>{p.itemType}</TableCell>
                        <TableCell className="max-w-xs truncate">{p.reference}</TableCell>
                        <TableCell>{fmtDate(p.dueDate)}</TableCell>
                        <TableCell>
                          {p.daysRemaining === null ? '—'
                            : p.daysRemaining < 0
                              ? <span className="text-red-600 dark:text-red-500">
                                  {Math.abs(p.daysRemaining)} days over
                                </span>
                              : `in ${p.daysRemaining} days`}
                        </TableCell>
                        <TableCell>{p.escalationTier === 0 ? 'First notice' : `Tier ${p.escalationTier}`}</TableCell>
                        <TableCell>
                          {p.alreadySent
                            ? <span className="text-muted-foreground">Already sent</span>
                            : 'Yes'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="runs" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {loadingRuns ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : runs.length === 0 ? (
                <EmptyState icon={BellRing} title="No sweeps yet"
                  description="Neither the daily service nor anybody here has run one." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Started</TableHead>
                      <TableHead>Finished</TableHead>
                      <TableHead>Trigger</TableHead>
                      <TableHead className="text-right">Queued</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {runs.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell>{fmtDateTime(r.startedAt)}</TableCell>
                        <TableCell>{fmtDateTime(r.completedAt)}</TableCell>
                        <TableCell>{r.trigger}</TableCell>
                        <TableCell className="text-right">{r.remindersQueued}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
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
              ) : log.length === 0 ? (
                <EmptyState icon={BellRing} title="Nothing sent"
                  description="No reminder has gone out in the last fortnight." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Sent</TableHead>
                      <TableHead>What</TableHead>
                      <TableHead>Subject</TableHead>
                      <TableHead>Reference</TableHead>
                      <TableHead>Due</TableHead>
                      <TableHead>Escalation</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {log.map((e) => (
                      <TableRow key={e.id}>
                        <TableCell>{fmtDateTime(e.createdAt)}</TableCell>
                        <TableCell>{KIND_LABELS[e.kind] ?? e.kind}</TableCell>
                        <TableCell>{e.itemType}</TableCell>
                        <TableCell className="max-w-xs truncate">{e.reference}</TableCell>
                        <TableCell>{fmtDate(e.dueDate)}</TableCell>
                        <TableCell>
                          {e.escalationTier === 0 ? 'First notice' : `Tier ${e.escalationTier}`}
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
