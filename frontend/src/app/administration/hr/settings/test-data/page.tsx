'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Circle, Loader2, RefreshCw, XCircle } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/hooks/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  hrTestDataService,
  type HrTestDataRun,
  type HrTestDataStatus,
  type HrTestDataTier,
} from '@/services/hr/hr-test-data.service';

/**
 * Developer Test Data. The HR seed that used to need `seed-hr-all` / `seed-hr-demo` on the
 * command line, as three buttons, so developers on other modules can give their database an
 * organisation, people and logins. Every step is idempotent: pressing a button twice adds nothing.
 */

const TIERS: { tier: HrTestDataTier; key: string; action: string; confirm?: string }[] = [
  { tier: 'Foundation', key: 'foundation', action: 'Seed foundation' },
  {
    tier: 'Workforce',
    key: 'workforce',
    action: 'Seed foundation + workforce',
    confirm:
      'This adds about a hundred SYNTHETIC staff to the DEFAULT tenant, on the TDC establishment, with their '
      + 'line managers, plus leave types, the Ghana holiday calendar and the 2026 salary scale. Anything already '
      + 'present is left as it is.',
  },
  {
    tier: 'Logins',
    key: 'logins',
    action: 'Seed everything, with logins',
    confirm:
      'This also creates the demo persona logins (hr.head, head.dev, staff, she.officer, …), each linked to a '
      + 'synthetic employee, all with the same published password. Use it only on a development or test database.',
  },
];

export default function DeveloperTestDataPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [pending, setPending] = useState<(typeof TIERS)[number] | null>(null);

  const { data: status, isLoading } = useQuery({
    queryKey: ['hr', 'test-data', 'status'],
    queryFn: () => hrTestDataService.getStatus(),
    // Poll only while a run is in flight; otherwise the probes are read once per visit.
    refetchInterval: (query) => ((query.state.data as HrTestDataStatus | undefined)?.currentRun ? 2000 : false),
  });

  const run = useMutation({
    mutationFn: (tier: HrTestDataTier) => hrTestDataService.run(tier),
    onSuccess: (res) => {
      toast({ title: 'Seeding started', description: res.message });
      queryClient.invalidateQueries({ queryKey: ['hr', 'test-data', 'status'] });
    },
    onError: (err: Error) => {
      toast({ title: 'Could not start seeding', description: err.message, variant: 'destructive' });
      queryClient.invalidateQueries({ queryKey: ['hr', 'test-data', 'status'] });
    },
  });

  const running = !!status?.currentRun;
  const start = (t: (typeof TIERS)[number]) => (t.confirm ? setPending(t) : run.mutate(t.tier));

  return (
    <div className="space-y-6">
      <PageHeader
        title="Developer Test Data"
        description="Seed the HR organisation, a synthetic workforce and demo logins, so other modules have people to work with. Each button includes the ones before it, and running one twice adds nothing."
        backHref="/administration/hr/settings"
      />

      {isLoading || !status ? (
        <div className="grid gap-4 md:grid-cols-3">
          {[0, 1, 2].map((i) => <Skeleton key={i} className="h-64" />)}
        </div>
      ) : (
        <>
          <p className="text-sm text-muted-foreground">
            Seeds the <b>{status.tenantCode}</b> tenant · API environment: <b>{status.environment}</b>
          </p>

          {!status.enabled && (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Not available here</AlertTitle>
              <AlertDescription>{status.disabledReason}</AlertDescription>
            </Alert>
          )}
          {status.stateError && (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Could not read what is seeded</AlertTitle>
              <AlertDescription>{status.stateError}</AlertDescription>
            </Alert>
          )}
          {status.syntheticStaffBlockedReason && (
            <Alert>
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Real staff are present — workforce and logins are unavailable</AlertTitle>
              <AlertDescription>{status.syntheticStaffBlockedReason}</AlertDescription>
            </Alert>
          )}

          <div className="grid gap-4 md:grid-cols-3">
            {TIERS.map((t) => {
              const info = status.tiers.find((x) => x.key === t.key);
              const blocked = t.tier !== 'Foundation' && !!status.syntheticStaffBlockedReason;
              return (
                <Card key={t.key} className="flex flex-col">
                  <CardHeader>
                    <div className="flex items-center justify-between gap-2">
                      <CardTitle className="text-base">{info?.title ?? t.tier}</CardTitle>
                      {info && (
                        <Badge variant={info.complete ? 'default' : 'secondary'}>
                          {info.complete ? 'Seeded' : 'Not seeded'}
                        </Badge>
                      )}
                    </div>
                    <CardDescription>{info?.description}</CardDescription>
                  </CardHeader>
                  <CardContent className="flex-1">
                    <ul className="space-y-1.5 text-sm">
                      {info?.steps.map((s) => (
                        <li key={s.name} className="flex items-start gap-2">
                          {s.alwaysRuns ? (
                            <RefreshCw className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" aria-label="Runs every time" />
                          ) : s.present ? (
                            <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" aria-label="Present" />
                          ) : (
                            <Circle className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" aria-label="Not yet" />
                          )}
                          <span className={s.present || s.alwaysRuns ? '' : 'text-muted-foreground'}>{s.name}</span>
                        </li>
                      ))}
                    </ul>
                  </CardContent>
                  <CardFooter>
                    <Button
                      className="w-full"
                      disabled={!status.enabled || running || blocked || run.isPending || !!status.stateError}
                      onClick={() => start(t)}
                    >
                      {running && status.currentRun?.tier === t.tier && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                      {t.action}
                    </Button>
                  </CardFooter>
                </Card>
              );
            })}
          </div>

          <RunPanel run={status.currentRun ?? status.lastRun ?? null} />

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Demo logins</CardTitle>
              <CardDescription>
                Every persona signs in with the password <code className="rounded bg-muted px-1">{status.personaPassword}</code>.
                Each is linked to the employee holding the post, so approvals and self-service act as a real person.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Username</TableHead>
                    <TableHead>Post</TableHead>
                    <TableHead>Linked employee</TableHead>
                    <TableHead>Roles</TableHead>
                    <TableHead>For</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {status.personas.map((p) => (
                    <TableRow key={p.username}>
                      <TableCell className="font-mono text-xs">
                        {p.username}
                        {!p.exists && <Badge variant="outline" className="ml-2">not created</Badge>}
                      </TableCell>
                      <TableCell className="text-sm">{p.positionTitle}</TableCell>
                      <TableCell className="text-sm">{p.linkedEmployee ?? '—'}</TableCell>
                      <TableCell className="text-xs text-muted-foreground">{p.roles.join(', ')}</TableCell>
                      <TableCell className="text-xs text-muted-foreground">{p.purpose}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </>
      )}

      <AlertDialog open={!!pending} onOpenChange={(open) => !open && setPending(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{pending?.action}?</AlertDialogTitle>
            <AlertDialogDescription>{pending?.confirm}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (pending) run.mutate(pending.tier);
                setPending(null);
              }}
            >
              Seed
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function RunPanel({ run }: { run: HrTestDataRun | null }) {
  if (!run) return null;
  const done = run.steps.filter((s) => s.result !== 'Failed').length;
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between gap-2">
          <CardTitle className="text-base">
            {run.state === 'Running' ? 'Seeding' : 'Last run'}: {run.tier}
          </CardTitle>
          <Badge variant={run.state === 'Failed' ? 'destructive' : run.state === 'Running' ? 'secondary' : 'default'}>
            {run.state === 'Running' && <Loader2 className="mr-1 h-3 w-3 animate-spin" />}
            {run.state}
          </Badge>
        </div>
        <CardDescription>
          Started by {run.requestedBy} at {new Date(run.startedAt).toLocaleString()}
          {run.finishedAt ? ` · finished ${new Date(run.finishedAt).toLocaleString()}` : ` · ${done} step(s) so far`}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-2">
        {run.error && (
          <Alert variant="destructive">
            <AlertTriangle className="h-4 w-4" />
            <AlertDescription>{run.error}</AlertDescription>
          </Alert>
        )}
        <ul className="space-y-1 text-sm">
          {run.steps.map((s, i) => (
            <li key={`${s.name}-${i}`} className="flex items-start gap-2">
              {s.result === 'Failed' ? (
                <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
              ) : (
                <CheckCircle2 className={`mt-0.5 h-4 w-4 shrink-0 ${s.result === 'Ran' ? 'text-emerald-600' : 'text-muted-foreground'}`} />
              )}
              <span>
                {s.name}
                <span className="ml-2 text-xs text-muted-foreground">
                  {s.result === 'Ran' ? 'seeded' : s.result === 'Skipped' ? 'already present' : 'failed'}
                  {s.error ? ` — ${s.error}` : ''}
                </span>
              </span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
