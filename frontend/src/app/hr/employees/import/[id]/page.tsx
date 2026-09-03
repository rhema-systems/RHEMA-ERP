'use client';

/**
 * One employee import session — steps 3, 4 and 5 of the wizard on a single page, driven by the
 * session's status: review the checked rows, confirm the commit, watch it run, collect the result.
 *
 * ⚠ The rows shown here are what the CHECKER decided; the register is only touched by the commit,
 * which runs in the background and is polled. A row with an error never commits. A row with a
 * warning does, and the person lands on the follow-up list — that list is the output of this screen
 * that outlives it, which is why it downloads as a workbook.
 *
 * ⚠ "Fix in Excel" is the intended loop for errors: download the checked copy (every problem cell
 * is coloured and commented), correct it, upload it again as a new import. Skipping a row is for
 * the person who should not be loaded at all, not for the one whose department is misspelt.
 */

import { useEffect, useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Download,
  ExternalLink,
  Loader2,
  Play,
  Search,
  Upload,
  XCircle,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { WizardSteps } from '@/components/hr/employee-import/WizardSteps';
import { RowOutcomeBadge, SessionStatusBadge } from '@/components/hr/employee-import/SessionStatusBadge';
import { employeeImportService } from '@/services/hr/employee-import.service';
import { formatDateTime } from '@/lib/hr/attendance-format';
import type {
  EmployeeImportCommitPolicy,
  EmployeeImportFinding,
  EmployeeImportRow,
  EmployeeImportRowOutcome,
  EmployeeImportSessionSummary,
} from '@/types/hr/employee-import';

const PAGE_SIZE = 50;
type OutcomeFilter = EmployeeImportRowOutcome | 'All';

function stepFor(status: EmployeeImportSessionSummary['status']): number {
  switch (status) {
    case 'Validated': return 3;
    case 'CommitRequested':
    case 'Committing': return 4;
    default: return 5;
  }
}

function FindingLine({ finding }: { finding: EmployeeImportFinding }) {
  const isError = finding.severity === 'Error';
  return (
    <li className="flex items-start gap-2 text-sm">
      {isError
        ? <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-600" />
        : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />}
      <span>
        {finding.cell && <code className="mr-1 rounded bg-muted px-1 text-xs">{finding.cell.replace(/^Employees!/, '')}</code>}
        {finding.message}
        {finding.suggestions.length > 0 && (
          <span className="text-muted-foreground"> Did you mean: {finding.suggestions.join(', ')}?</span>
        )}
      </span>
    </li>
  );
}

function Tile({ label, value, tone }: { label: string; value: number; tone?: 'green' | 'amber' | 'red' | 'muted' }) {
  const color =
    tone === 'green' ? 'text-green-700' : tone === 'amber' ? 'text-amber-700' : tone === 'red' ? 'text-red-700' : 'text-foreground';
  return (
    <Card>
      <CardContent className="p-4">
        <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
        <div className={`text-2xl font-semibold ${color}`}>{value}</div>
      </CardContent>
    </Card>
  );
}

export default function EmployeeImportSessionPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [outcome, setOutcome] = useState<OutcomeFilter>('All');
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [page, setPage] = useState(1);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [policy, setPolicy] = useState<EmployeeImportCommitPolicy>('ValidRowsOnly');
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [downloading, setDownloading] = useState<'report' | 'followup' | null>(null);

  const sessionKey = ['hr', 'employee-import', 'session', id] as const;
  const { data: session, isLoading } = useQuery({
    queryKey: sessionKey,
    queryFn: () => employeeImportService.get(id),
    refetchInterval: (query) => {
      const s = query.state.data?.status;
      return s === 'CommitRequested' || s === 'Committing' ? 2000 : false;
    },
  });

  const isRunning = session?.status === 'CommitRequested' || session?.status === 'Committing';
  const isDone = session?.status === 'Committed' || session?.status === 'CommittedWithErrors' || session?.status === 'Failed';

  const { data: progress } = useQuery({
    queryKey: ['hr', 'employee-import', 'progress', id],
    queryFn: () => employeeImportService.progress(id),
    enabled: !!session && isRunning,
    refetchInterval: 2000,
  });

  useEffect(() => {
    if (progress && !isRunning) queryClient.invalidateQueries({ queryKey: ['hr', 'employee-import', 'rows', id] });
  }, [progress, isRunning, queryClient, id]);

  const rowsKey = ['hr', 'employee-import', 'rows', id, outcome, search, page, session?.status] as const;
  const { data: rows, isLoading: rowsLoading } = useQuery({
    queryKey: rowsKey,
    queryFn: () => employeeImportService.getRows(id, { outcome: outcome === 'All' ? null : outcome, search, page, pageSize: PAGE_SIZE }),
    enabled: !!session,
  });

  const { data: followUp } = useQuery({
    queryKey: ['hr', 'employee-import', 'follow-up', id],
    queryFn: () => employeeImportService.followUp(id),
    enabled: !!session && isDone,
  });

  const pageCount = rows ? Math.max(1, Math.ceil(rows.total / PAGE_SIZE)) : 1;

  const filters = useMemo<{ key: OutcomeFilter; label: string; count?: number }[]>(() => {
    if (!session) return [];
    const done = isDone;
    return done
      ? [
          { key: 'All', label: 'All', count: session.totalRows },
          { key: 'Committed', label: 'Created', count: session.committedCount },
          { key: 'CommittedWithIssues', label: 'Created with issues' },
          { key: 'Failed', label: 'Failed', count: session.failedCount },
          { key: 'Error', label: 'Not imported (errors)', count: session.errorCount },
          { key: 'Skipped', label: 'Skipped', count: session.skippedCount },
        ]
      : [
          { key: 'All', label: 'All', count: session.totalRows },
          { key: 'Ready', label: 'Ready', count: session.readyCount },
          { key: 'Warning', label: 'Warnings', count: session.warningCount },
          { key: 'Error', label: 'Errors', count: session.errorCount },
        ];
  }, [session, isDone]);

  const toggleSkip = async (row: EmployeeImportRow) => {
    try {
      await employeeImportService.setSkip(id, row.id, !row.skip);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: sessionKey }),
        queryClient.invalidateQueries({ queryKey: ['hr', 'employee-import', 'rows', id] }),
      ]);
    } catch (error: any) {
      toast({ title: 'Row not changed', description: error?.message ?? 'Please try again.', variant: 'destructive' });
    }
  };

  const downloadReport = async () => {
    if (!session) return;
    setDownloading('report');
    try {
      await employeeImportService.downloadCheckedCopy(id, session.reference);
    } catch (error: any) {
      toast({ title: 'Download failed', description: error?.message ?? 'Please try again.', variant: 'destructive' });
    } finally {
      setDownloading(null);
    }
  };

  const downloadFollowUp = async () => {
    if (!session) return;
    setDownloading('followup');
    try {
      await employeeImportService.downloadFollowUp(id, session.reference);
    } catch (error: any) {
      toast({ title: 'Download failed', description: error?.message ?? 'Please try again.', variant: 'destructive' });
    } finally {
      setDownloading(null);
    }
  };

  const commit = async () => {
    setCommitting(true);
    try {
      await employeeImportService.commit(id, policy);
      setConfirmOpen(false);
      toast({ title: 'Commit started', description: 'Rows are being written in the background. This page follows the progress.' });
      await queryClient.invalidateQueries({ queryKey: sessionKey });
    } catch (error: any) {
      toast({ title: 'Commit refused', description: error?.message ?? 'Please try again.', variant: 'destructive' });
    } finally {
      setCommitting(false);
    }
  };

  const cancel = async () => {
    setCancelling(true);
    try {
      await employeeImportService.cancel(id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'employee-import'] });
      toast({ title: 'Import cancelled', description: 'Nothing was written. The checked rows are kept for the record.' });
      setCancelOpen(false);
      return true;
    } catch (error: any) {
      toast({ title: 'Not cancelled', description: error?.message ?? 'Please try again.', variant: 'destructive' });
      return false;
    } finally {
      setCancelling(false);
    }
  };

  if (isLoading || !session) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  const liveErrors = session.errorCount;
  const toCommit = session.totalRows - session.errorCount - session.skippedCount;
  const pct = progress && progress.toCommit > 0
    ? Math.round(((progress.committedCount + progress.failedCount) / progress.toCommit) * 100)
    : 0;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={session.reference}
        description={`${session.fileName} · uploaded ${formatDateTime(session.uploadedOn)} by ${session.uploadedByName ?? '—'}`}
        backHref="/hr/employees/import"
        actions={
          <div className="flex items-center gap-2">
            <SessionStatusBadge status={session.status} />
            <Button variant="outline" onClick={downloadReport} disabled={downloading === 'report'}>
              {downloading === 'report' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
              Checked file
            </Button>
          </div>
        }
      />
      <WizardSteps current={stepFor(session.status)} />

      {session.fileFindings.length > 0 && (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>About the file</AlertTitle>
          <AlertDescription>
            <ul className="list-disc space-y-1 pl-5">
              {session.fileFindings.map((f, i) => <li key={i}>{f.message}</li>)}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      {session.status === 'Failed' && (
        <Alert variant="destructive">
          <XCircle className="h-4 w-4" />
          <AlertTitle>The commit stopped</AlertTitle>
          <AlertDescription>{session.failureMessage ?? 'An unexpected error stopped the commit. Rows already created are kept; the rest were not written.'}</AlertDescription>
        </Alert>
      )}

      {/* ── Tiles ── */}
      {isDone ? (
        <div className="grid grid-cols-2 gap-4 md:grid-cols-5">
          <Tile label="Rows" value={session.totalRows} />
          <Tile label="Created" value={session.committedCount} tone="green" />
          <Tile label="Failed at write" value={session.failedCount} tone={session.failedCount ? 'red' : 'muted'} />
          <Tile label="Not imported (errors)" value={session.errorCount} tone={session.errorCount ? 'red' : 'muted'} />
          <Tile label="Skipped" value={session.skippedCount} tone="muted" />
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-4 md:grid-cols-5">
          <Tile label="Rows" value={session.totalRows} />
          <Tile label="Ready" value={session.readyCount} tone="green" />
          <Tile label="Warnings" value={session.warningCount} tone={session.warningCount ? 'amber' : 'muted'} />
          <Tile label="Errors" value={session.errorCount} tone={session.errorCount ? 'red' : 'muted'} />
          <Tile label="Skipped" value={session.skippedCount} tone="muted" />
        </div>
      )}

      {/* ── Step 4: progress ── */}
      {isRunning && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2"><Loader2 className="h-5 w-5 animate-spin" /> Writing rows to the register</CardTitle>
            <CardDescription>
              Each employee goes through the same checks as the create form. You can leave this page; the import continues.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <Progress value={pct} />
            <div className="text-sm text-muted-foreground">
              {progress
                ? `${progress.committedCount + progress.failedCount} of ${progress.toCommit} written · ${progress.committedCount} created · ${progress.failedCount} failed`
                : 'Waiting for the committer to pick this up…'}
            </div>
          </CardContent>
        </Card>
      )}

      {/* ── Step 5: result ── */}
      {isDone && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              {session.status === 'Committed'
                ? <CheckCircle2 className="h-5 w-5 text-green-600" />
                : <AlertTriangle className="h-5 w-5 text-amber-600" />}
              {session.status === 'Committed' ? 'Every row that was due to import did' : 'Finished, with some rows needing attention'}
            </CardTitle>
            <CardDescription>
              Completed {session.commitCompletedOn ? formatDateTime(session.commitCompletedOn) : '—'}.
              {session.counterReconciliation.length > 0 && (
                <> Staff-number counters: {session.counterReconciliation.map((c) => `${c.register} ${c.counterBefore} → ${c.counterAfter}`).join('; ')}.</>
              )}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <Button variant="outline" onClick={downloadFollowUp} disabled={downloading === 'followup' || !followUp || followUp.length === 0}>
                {downloading === 'followup' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
                Profiles to complete ({followUp?.length ?? 0})
              </Button>
              {(session.errorCount > 0 || session.failedCount > 0) && (
                <Button variant="outline" onClick={() => router.push('/hr/employees/import/new')}>
                  <Upload className="mr-2 h-4 w-4" /> Fix the checked file and upload again
                </Button>
              )}
              <Button variant="ghost" onClick={() => router.push('/hr/employees')}>Open the employee register</Button>
            </div>
            {followUp && followUp.length > 0 && (
              <div className="rounded border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-16">Row</TableHead>
                      <TableHead>Employee</TableHead>
                      <TableHead>What to complete</TableHead>
                      <TableHead className="w-12" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {followUp.slice(0, 200).map((f) => (
                      <TableRow key={f.employeeId}>
                        <TableCell>{f.rowNumber}</TableCell>
                        <TableCell>
                          <div className="font-medium">{f.displayName}</div>
                          <div className="text-xs text-muted-foreground">{f.staffNumber}</div>
                        </TableCell>
                        <TableCell>
                          <ul className="list-disc space-y-0.5 pl-4 text-sm">
                            {f.items.map((item, i) => <li key={i}>{item}</li>)}
                          </ul>
                        </TableCell>
                        <TableCell>
                          <Button variant="ghost" size="icon" title="Open profile" onClick={() => router.push(`/hr/employees/${f.employeeId}`)}>
                            <ExternalLink className="h-4 w-4" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
                {followUp.length > 200 && (
                  <div className="p-2 text-xs text-muted-foreground">Showing 200 of {followUp.length}; download the workbook for the full list.</div>
                )}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* ── Step 3: review + commit controls ── */}
      {session.status === 'Validated' && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2"><Play className="h-5 w-5" /> Commit</CardTitle>
            <CardDescription>
              {toCommit} {toCommit === 1 ? 'row' : 'rows'} will be written{liveErrors > 0 ? `; ${liveErrors} with errors will be left behind` : ''}
              {session.skippedCount > 0 ? `; ${session.skippedCount} skipped` : ''}. Rows with warnings import and are listed for follow-up.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <RadioGroup value={policy} onValueChange={(v) => setPolicy(v as EmployeeImportCommitPolicy)} className="space-y-2">
              <div className="flex items-start gap-2">
                <RadioGroupItem value="ValidRowsOnly" id="policy-valid" />
                <Label htmlFor="policy-valid" className="font-normal">
                  <span className="font-medium">Commit valid rows only.</span> Rows with errors stay here; fix them in the checked file and upload again.
                </Label>
              </div>
              <div className="flex items-start gap-2">
                <RadioGroupItem value="AllOrNothing" id="policy-all" disabled={liveErrors > 0} />
                <Label htmlFor="policy-all" className={`font-normal ${liveErrors > 0 ? 'text-muted-foreground' : ''}`}>
                  <span className="font-medium">All or nothing.</span> Refuse to start while any row has an error{liveErrors > 0 ? ` (${liveErrors} do)` : ''}.
                </Label>
              </div>
            </RadioGroup>
            <div className="flex flex-wrap gap-2">
              <Button onClick={() => setConfirmOpen(true)} disabled={toCommit <= 0}>
                <Play className="mr-2 h-4 w-4" /> Commit {toCommit} {toCommit === 1 ? 'row' : 'rows'}
              </Button>
              <Button variant="outline" onClick={() => router.push('/hr/employees/import/new')}>
                <Upload className="mr-2 h-4 w-4" /> Upload a corrected file instead
              </Button>
              <Button variant="ghost" onClick={() => setCancelOpen(true)}>Cancel this import</Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* ── Rows ── */}
      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle>Rows</CardTitle>
            <div className="flex flex-wrap items-center gap-2">
              {filters.map((f) => (
                <Button
                  key={f.key}
                  size="sm"
                  variant={outcome === f.key ? 'default' : 'outline'}
                  onClick={() => { setOutcome(f.key); setPage(1); }}
                >
                  {f.label}{f.count !== undefined ? ` (${f.count})` : ''}
                </Button>
              ))}
              <form
                className="flex items-center gap-1"
                onSubmit={(e) => { e.preventDefault(); setSearch(searchInput.trim()); setPage(1); }}
              >
                <Input
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  placeholder="Staff number or name"
                  className="h-8 w-48"
                />
                <Button type="submit" size="sm" variant="outline"><Search className="h-4 w-4" /></Button>
              </form>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {rowsLoading || !rows ? (
            <div className="space-y-2">{[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-10 w-full" />)}</div>
          ) : rows.items.length === 0 ? (
            <EmptyState title="No rows match" description="Change the filter or the search." />
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-14">Row</TableHead>
                    <TableHead>Staff No.</TableHead>
                    <TableHead>Name</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Department</TableHead>
                    <TableHead>Position</TableHead>
                    <TableHead>Result</TableHead>
                    <TableHead className="text-right">Findings</TableHead>
                    {session.status === 'Validated' && <TableHead className="w-20">Skip</TableHead>}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.items.map((row) => {
                    const open = expanded === row.id;
                    const findingsCount = row.findings.length + (row.commitMessage ? 1 : 0);
                    return (
                      <RowGroup key={row.id}>
                        <TableRow className="cursor-pointer" onClick={() => setExpanded(open ? null : row.id)}>
                          <TableCell className="text-muted-foreground">{row.rowNumber}</TableCell>
                          <TableCell className="font-medium">{row.staffNumber ?? '—'}</TableCell>
                          <TableCell>{row.displayName ?? '—'}</TableCell>
                          <TableCell>{row.employmentType ?? '—'}</TableCell>
                          <TableCell className="max-w-[180px] truncate">{row.values.Department ?? '—'}</TableCell>
                          <TableCell className="max-w-[200px] truncate">{row.values.Position ?? '—'}</TableCell>
                          <TableCell><RowOutcomeBadge outcome={row.outcome} skip={row.skip} /></TableCell>
                          <TableCell className="text-right">
                            {row.errorCount > 0 && <Badge variant="destructive" className="mr-1">{row.errorCount} error{row.errorCount === 1 ? '' : 's'}</Badge>}
                            {row.warningCount > 0 && <Badge variant="outline" className="border-amber-300 text-amber-800">{row.warningCount} warning{row.warningCount === 1 ? '' : 's'}</Badge>}
                            {findingsCount === 0 && <span className="text-muted-foreground">—</span>}
                          </TableCell>
                          {session.status === 'Validated' && (
                            <TableCell onClick={(e) => e.stopPropagation()}>
                              <Switch checked={row.skip} onCheckedChange={() => toggleSkip(row)} aria-label="Skip this row" />
                            </TableCell>
                          )}
                        </TableRow>
                        {open && (
                          <TableRow className="bg-muted/30 hover:bg-muted/30">
                            <TableCell colSpan={session.status === 'Validated' ? 9 : 8}>
                              <div className="grid gap-4 md:grid-cols-2">
                                <div>
                                  <div className="mb-1 text-xs font-medium uppercase text-muted-foreground">Findings</div>
                                  {row.findings.length === 0 && !row.commitMessage
                                    ? <div className="text-sm text-muted-foreground">Nothing to report.</div>
                                    : (
                                      <ul className="space-y-1">
                                        {row.findings.map((f, i) => <FindingLine key={i} finding={f} />)}
                                        {row.commitMessage && (
                                          <li className="flex items-start gap-2 text-sm">
                                            <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-600" />
                                            <span>At commit: {row.commitMessage}</span>
                                          </li>
                                        )}
                                      </ul>
                                    )}
                                  {row.createdEmployeeId && (
                                    <Button variant="link" className="mt-2 h-auto p-0" onClick={() => router.push(`/hr/employees/${row.createdEmployeeId}`)}>
                                      Open the created profile <ExternalLink className="ml-1 h-3 w-3" />
                                    </Button>
                                  )}
                                </div>
                                <div>
                                  <div className="mb-1 text-xs font-medium uppercase text-muted-foreground">As read from the sheet</div>
                                  <dl className="grid grid-cols-[minmax(120px,auto)_1fr] gap-x-3 gap-y-0.5 text-sm">
                                    {Object.entries(row.values).filter(([, v]) => v).map(([k, v]) => (
                                      <div key={k} className="contents">
                                        <dt className="text-muted-foreground">{k.replace(/^Id:[0-9a-f]+:/i, 'ID ')}</dt>
                                        <dd className="truncate" title={v ?? ''}>{v}</dd>
                                      </div>
                                    ))}
                                  </dl>
                                </div>
                              </div>
                            </TableCell>
                          </TableRow>
                        )}
                      </RowGroup>
                    );
                  })}
                </TableBody>
              </Table>
              <div className="mt-3 flex items-center justify-between text-sm text-muted-foreground">
                <span>{rows.total} {rows.total === 1 ? 'row' : 'rows'}</span>
                <div className="flex items-center gap-2">
                  <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                    <ChevronLeft className="h-4 w-4" />
                  </Button>
                  <span>Page {page} of {pageCount}</span>
                  <Button variant="outline" size="sm" disabled={page >= pageCount} onClick={() => setPage((p) => p + 1)}>
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Commit {toCommit} {toCommit === 1 ? 'employee' : 'employees'} to the register?</DialogTitle>
            <DialogDescription>
              {policy === 'AllOrNothing'
                ? 'Every row is written. If any row is refused at write time it is reported and the rest continue; nothing is undone.'
                : `${toCommit} rows are written; ${liveErrors} with errors and ${session.skippedCount} skipped are left behind.`}
              {' '}Staff numbers are accepted exactly as given and the register's counter is advanced past them. This cannot be reversed from here.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)} disabled={committing}>Back</Button>
            <Button onClick={commit} disabled={committing}>
              {committing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Play className="mr-2 h-4 w-4" />}
              Commit
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={cancelOpen}
        onOpenChange={setCancelOpen}
        title="Cancel this import?"
        description="Nothing has been written. The checked rows stay on record; upload a corrected file to start again."
        confirmText="Cancel import"
        onConfirm={cancel}
        isLoading={cancelling}
        variant="destructive"
      />
    </div>
  );
}

/** A fragment with a key — table bodies cannot hold React fragments with keys inline. */
function RowGroup({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
