'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BadgeAlert, Loader2, Play } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
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
import { identificationExpiryService } from '@/services/hr/lookup.service';
import { useToast } from '@/hooks/use-toast';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

const KIND_LABELS: Record<string, string> = {
  IdentificationExpiring: 'Expiring',
  IdentificationExpired: 'Expired',
};

/** Negative days read as "N days over", which is what an expired document actually means. */
function Countdown({ days }: { days: number }) {
  if (days < 0) {
    return (
      <span className="font-medium text-red-600 dark:text-red-500">
        {Math.abs(days)} {Math.abs(days) === 1 ? 'day' : 'days'} over
      </span>
    );
  }
  if (days === 0) {
    return <span className="font-medium text-amber-600 dark:text-amber-500">today</span>;
  }
  return (
    <span>
      in {days} {days === 1 ? 'day' : 'days'}
    </span>
  );
}

/**
 * Whose job the card is now.
 *
 * ⚠ An ownership stamp, NOT a visibility filter. HR sees every row at both tiers; this column says
 * who is expected to act, not who was allowed to look. Reading it as "sent to" would suggest HR is
 * blind to tier 1, which it is not.
 */
function Owner({ tier }: { tier: number }) {
  return tier === 1
    ? <Badge variant="outline">The holder</Badge>
    : <Badge variant="destructive">HR</Badge>;
}

/**
 * The identification-expiry sweep — lane 3b.
 *
 * Two tiers that move ownership rather than volume: inside the lead window the holder is asked to
 * renew their own document; once the date has passed it becomes HR's compliance gap. One row per
 * card per deadline either way — a second row addressed to HR at tier 1 would double the log and
 * make "how many cards are expiring" ambiguous.
 *
 * ⚠ **Lead days live on the identification TYPE, not the card.** A type with none raises nothing,
 * which is the correct reading for an ID that does not expire — so a preview showing far fewer rows
 * than the card register holds is usually the lead-day column being unset, not the sweep failing.
 * The Lead column is on the table for exactly that reason: it shows why a row surfaced when it did.
 *
 * ⚠ Leavers are included by design. An unreturned company ID belonging to somebody who has left is
 * the case this screen exists to surface.
 */
export default function IdentificationExpiryPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data: preview = [], isLoading: loadingPreview } = useQuery({
    queryKey: ['hr', 'identification-expiry', 'preview'],
    queryFn: () => identificationExpiryService.preview(),
  });
  const { data: runs = [], isLoading: loadingRuns } = useQuery({
    queryKey: ['hr', 'identification-expiry', 'runs'],
    queryFn: () => identificationExpiryService.getRuns(20),
  });
  const { data: log = [], isLoading: loadingLog } = useQuery({
    queryKey: ['hr', 'identification-expiry', 'log'],
    queryFn: () => identificationExpiryService.getLog(30),
  });

  const run = useMutation({
    mutationFn: () => identificationExpiryService.run(),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'identification-expiry'] });
      toast({
        title: 'Sweep finished',
        description:
          `${result.cardsConsidered} card(s) due, ${result.remindersQueued} new reminder(s), `
          + `${result.alreadyRaised} already raised.`,
      });
    },
    onError: (e: Error) =>
      toast({ title: 'The sweep failed', description: e.message, variant: 'destructive' }),
  });

  const { expired, expiring, fresh } = useMemo(() => ({
    expired: preview.filter((p) => p.escalationTier === 2).length,
    expiring: preview.filter((p) => p.escalationTier === 1).length,
    fresh: preview.filter((p) => !p.alreadyRaised).length,
  }), [preview]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Identification expiry"
        description="Company IDs, passports and permits coming up for renewal — and those already lapsed."
        backHref="/hr/employees"
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
          <TabsTrigger value="preview">Due ({preview.length})</TabsTrigger>
          <TabsTrigger value="runs">Sweeps ({runs.length})</TabsTrigger>
          <TabsTrigger value="log">Raised ({log.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="preview" className="space-y-4 pt-4">
          <Card>
            <CardContent className="p-4 text-sm text-muted-foreground">
              <span className="font-medium text-foreground">{expiring}</span> approaching renewal and{' '}
              <span className="font-medium text-foreground">{expired}</span> already lapsed.{' '}
              <span className="font-medium text-foreground">{fresh}</span> would be newly raised by a
              sweep now; the rest were claimed by an earlier one and will not be repeated.
              <br />
              Warning times are set per identification type, on{' '}
              <Link href="/administration/hr/identification-types" className="underline">
                identification types
              </Link>
              . A type with no warning time raises nothing — the correct reading for an ID that does
              not expire.
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-0">
              {loadingPreview ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : preview.length === 0 ? (
                <EmptyState
                  icon={BadgeAlert}
                  title="Nothing expiring"
                  description="No card falls inside its type's warning window. If that looks wrong, check that the identification types carry a warning time — without one they raise nothing."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Document</TableHead>
                      <TableHead>Number</TableHead>
                      <TableHead>Expires</TableHead>
                      <TableHead>When</TableHead>
                      <TableHead className="text-right">Lead</TableHead>
                      <TableHead>Now with</TableHead>
                      <TableHead>State</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {preview.map((p) => (
                      <TableRow key={`${p.employeeIdentificationCardId}-${p.escalationTier}-${p.dueDate}`}>
                        <TableCell>
                          <Link
                            href={`/hr/employees/${p.employeeId}`}
                            className="font-medium hover:underline"
                          >
                            {p.employeeName ?? 'Unknown employee'}
                          </Link>
                          {p.employeeNumber && (
                            <div className="text-xs text-muted-foreground">{p.employeeNumber}</div>
                          )}
                        </TableCell>
                        <TableCell>{p.identificationTypeName}</TableCell>
                        <TableCell className="font-mono text-xs">{p.documentNumber ?? '—'}</TableCell>
                        <TableCell>{fmtDate(p.dueDate)}</TableCell>
                        <TableCell><Countdown days={p.daysRemaining} /></TableCell>
                        <TableCell className="text-right text-muted-foreground">{p.leadDays}d</TableCell>
                        <TableCell><Owner tier={p.escalationTier} /></TableCell>
                        <TableCell>
                          {p.alreadyRaised
                            ? <span className="text-muted-foreground">Already raised</span>
                            : <span>{KIND_LABELS[p.kind] ?? p.kind}</span>}
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
                <EmptyState
                  icon={BadgeAlert}
                  title="No sweep has ever run"
                  description="Neither the nightly service nor anybody here has run one. A sweep that finds nothing still records a row, so an empty list here means it genuinely has not run."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Started</TableHead>
                      <TableHead>Finished</TableHead>
                      <TableHead>Trigger</TableHead>
                      <TableHead className="text-right">Raised</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {runs.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell>{fmtDateTime(r.startedAt)}</TableCell>
                        <TableCell>{fmtDateTime(r.completedAt)}</TableCell>
                        <TableCell>
                          <Badge variant={r.trigger === 'Scheduled' ? 'secondary' : 'outline'}>
                            {r.trigger === 'Scheduled' ? 'Nightly' : r.trigger}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-right">{r.remindersQueued}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="log" className="space-y-4 pt-4">
          <Card>
            <CardContent className="p-4 text-sm text-muted-foreground">
              What actually went out over the last 30 days. These are the reminders as they were
              raised — renewing a document does not rewrite a row here, it raises a fresh reminder
              against the new deadline.
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-0">
              {loadingLog ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : log.length === 0 ? (
                <EmptyState
                  icon={BadgeAlert}
                  title="Nothing raised"
                  description="No reminder has gone out in the last 30 days."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Raised</TableHead>
                      <TableHead>Employee</TableHead>
                      <TableHead>Document</TableHead>
                      <TableHead>Expired</TableHead>
                      <TableHead>Was</TableHead>
                      <TableHead>Owner</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {log.map((e) => (
                      <TableRow key={e.id}>
                        <TableCell>{fmtDateTime(e.raisedAt)}</TableCell>
                        <TableCell>
                          <Link
                            href={`/hr/employees/${e.employeeId}`}
                            className="font-medium hover:underline"
                          >
                            {e.employeeName ?? 'Unknown employee'}
                          </Link>
                          {e.employeeNumber && (
                            <div className="text-xs text-muted-foreground">{e.employeeNumber}</div>
                          )}
                        </TableCell>
                        <TableCell>{e.identificationTypeName ?? e.reference ?? '—'}</TableCell>
                        <TableCell>{fmtDate(e.dueDate)}</TableCell>
                        <TableCell><Countdown days={e.daysRemaining} /></TableCell>
                        <TableCell><Owner tier={e.escalationTier} /></TableCell>
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
