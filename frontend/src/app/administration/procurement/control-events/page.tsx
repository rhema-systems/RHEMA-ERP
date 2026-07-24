'use client';

import { FormEvent, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Activity,
  CheckCircle2,
  FileCheck2,
  Fingerprint,
  RefreshCw,
  Search,
  ShieldAlert,
  ShieldCheck,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Pagination } from '@/components/ui/pagination';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  compactProcurementControlEventSearch,
  formatProcurementControlJson,
  procurementControlEventResults,
  procurementControlEventResultTone,
  procurementControlLineage,
} from '@/lib/procurement-control-event';
import { procurementControlEventService } from '@/services/procurement-control-event.service';
import type {
  ProcurementControlEvent,
  ProcurementControlEventResult,
  ProcurementControlEventSearch,
} from '@/types/procurement-control-event';

const dateTime = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';

const shortHash = (value: string) =>
  value.length > 18 ? `${value.slice(0, 18)}…` : value;

const EmptyState = () => (
  <div className="flex min-h-52 flex-col items-center justify-center px-6 text-center">
    <Activity className="mb-3 h-9 w-9 text-muted-foreground" />
    <p className="font-medium">No control events match this view</p>
    <p className="mt-1 max-w-lg text-sm text-muted-foreground">
      Events appear only when an adopted procurement control is enforced. No
      legacy transactions are guessed or backfilled.
    </p>
  </div>
);

const JsonPanel = ({ label, value }: { label: string; value?: string }) => (
  <div className="space-y-1.5">
    <Label>{label}</Label>
    <pre className="max-h-52 overflow-auto rounded-md border bg-muted/40 p-3 text-xs leading-5">
      {formatProcurementControlJson(value)}
    </pre>
  </div>
);

const DetailDialog = ({
  selectedId,
  onOpenChange,
}: {
  selectedId?: string;
  onOpenChange: (open: boolean) => void;
}) => {
  const detail = useQuery({
    queryKey: ['procurement-control-event', selectedId],
    queryFn: () =>
      selectedId
        ? procurementControlEventService.get(selectedId)
        : Promise.reject(new Error('No control event was selected.')),
    enabled: Boolean(selectedId),
  });
  const event = detail.data;

  return (
    <Dialog open={Boolean(selectedId)} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[92vh] max-w-5xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Control-event detail</DialogTitle>
          <DialogDescription>
            Immutable decision context and references to evidence owned by the
            shared workflow and upload controls.
          </DialogDescription>
        </DialogHeader>

        {detail.isLoading && (
          <p className="py-12 text-center text-sm text-muted-foreground">
            Loading event detail…
          </p>
        )}
        {detail.isError && (
          <Alert variant="destructive">
            <ShieldAlert className="h-4 w-4" />
            <AlertTitle>Unable to load event</AlertTitle>
            <AlertDescription>{detail.error.message}</AlertDescription>
          </Alert>
        )}
        {event && (
          <div className="space-y-5">
            <div className="flex flex-wrap items-center gap-2">
              <Badge
                variant="outline"
                className={procurementControlEventResultTone(event.result)}
              >
                {event.result}
              </Badge>
              <Badge variant="outline">Schema v{event.schemaVersion}</Badge>
              <Badge
                variant="outline"
                className={
                  event.integrityValid ? 'text-emerald-700' : 'text-destructive'
                }
              >
                {event.integrityValid
                  ? 'Integrity valid'
                  : 'Integrity mismatch'}
              </Badge>
            </div>

            <dl className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
              <div>
                <dt className="text-muted-foreground">Event / action</dt>
                <dd className="font-medium">
                  {event.eventType} · {event.action}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Actor</dt>
                <dd className="font-medium">{event.actorName}</dd>
                <dd className="text-xs text-muted-foreground">
                  {event.actorRoles.join(', ') || 'No role claim'}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Occurred</dt>
                <dd className="font-medium">{dateTime(event.occurredAtUtc)}</dd>
                <dd className="text-xs text-muted-foreground">
                  Recorded {dateTime(event.recordedAtUtc)}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Rule / decisions</dt>
                <dd className="font-medium">
                  {procurementControlLineage(
                    event.ruleCode,
                    event.ruleVersion,
                    event.decisionKeys
                  )}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Source</dt>
                <dd className="font-medium">{event.sourceType}</dd>
                <dd className="break-all text-xs text-muted-foreground">
                  {event.sourceReference}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">
                  Correlation / causation
                </dt>
                <dd className="break-all font-mono text-xs">
                  {event.correlationId}
                </dd>
                <dd className="break-all font-mono text-xs text-muted-foreground">
                  {event.causationId || 'No causation ID'}
                </dd>
              </div>
            </dl>

            {event.reason && (
              <div className="rounded-md border bg-muted/30 p-3 text-sm">
                <span className="font-medium">Reason: </span>
                {event.reason}
              </div>
            )}

            <div className="grid gap-4 lg:grid-cols-2">
              <JsonPanel label="Input values" value={event.inputValuesJson} />
              <JsonPanel label="Result values" value={event.resultValuesJson} />
              <JsonPanel label="Before state" value={event.beforeJson} />
              <JsonPanel label="After state" value={event.afterJson} />
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between gap-3">
                <Label>Shared evidence references</Label>
                <Badge variant="secondary">{event.evidence.length}</Badge>
              </div>
              {event.evidence.length === 0 ? (
                <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                  No evidence reference was required for this decision.
                </p>
              ) : (
                event.evidence.map((item) => (
                  <div
                    key={item.id}
                    className="grid gap-2 rounded-md border p-3 text-sm sm:grid-cols-[1fr_auto]"
                  >
                    <div>
                      <p className="font-medium">
                        {item.label || item.fileName || item.reference}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {item.referenceKind} ·{' '}
                        {item.requirementKey || 'No requirement key'}
                      </p>
                      {item.sha256 && (
                        <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
                          SHA-256 {item.sha256}
                        </p>
                      )}
                    </div>
                    <Badge
                      variant="outline"
                      className={
                        item.referenceAvailable
                          ? 'text-emerald-700'
                          : 'text-destructive'
                      }
                    >
                      {item.referenceAvailable
                        ? item.verificationStatus || 'Available'
                        : 'Unavailable'}
                    </Badge>
                  </div>
                ))
              )}
            </div>

            <div className="space-y-1 rounded-md border bg-slate-950 p-3 font-mono text-xs text-slate-100">
              <p className="break-all">Event key: {event.eventKey}</p>
              <p className="break-all">SHA-256: {event.integrityHash}</p>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
};

export default function ProcurementControlEventsPage() {
  const [draftSearch, setDraftSearch] = useState('');
  const [draftEventType, setDraftEventType] = useState('');
  const [draftResult, setDraftResult] = useState('');
  const [draftSourceType, setDraftSourceType] = useState('');
  const [filters, setFilters] = useState<ProcurementControlEventSearch>({
    page: 1,
    pageSize: 25,
  });
  const [selectedId, setSelectedId] = useState<string>();

  const summary = useQuery({
    queryKey: ['procurement-control-event-summary'],
    queryFn: procurementControlEventService.summary,
  });
  const events = useQuery({
    queryKey: ['procurement-control-events', filters],
    queryFn: () =>
      procurementControlEventService.search(
        compactProcurementControlEventSearch(filters)
      ),
  });
  const integrity = useMutation({
    mutationFn: () => procurementControlEventService.verifyIntegrity(1000),
  });

  const applyFilters = (formEvent: FormEvent) => {
    formEvent.preventDefault();
    setFilters((current) => ({
      ...current,
      page: 1,
      search: draftSearch.trim() || undefined,
      eventType: draftEventType.trim() || undefined,
      result: (draftResult || undefined) as
        | ProcurementControlEventResult
        | undefined,
      sourceType: draftSourceType.trim() || undefined,
    }));
  };

  const resetFilters = () => {
    setDraftSearch('');
    setDraftEventType('');
    setDraftResult('');
    setDraftSourceType('');
    setFilters({ page: 1, pageSize: 25 });
  };

  const refresh = () => Promise.all([summary.refetch(), events.refetch()]);
  const page = events.data;
  const totalPages = Math.max(
    1,
    Math.ceil((page?.totalCount ?? 0) / (page?.pageSize ?? 25))
  );
  const summaryCards = [
    {
      label: 'Recorded events',
      value: summary.data?.totalCount ?? 0,
      icon: Activity,
    },
    {
      label: 'Allowed / succeeded',
      value: summary.data?.allowedCount ?? 0,
      icon: CheckCircle2,
    },
    {
      label: 'Denied / rejected',
      value: summary.data?.deniedOrRejectedCount ?? 0,
      icon: ShieldAlert,
    },
    {
      label: 'Evidence linked',
      value: summary.data?.evidenceLinkedCount ?? 0,
      icon: FileCheck2,
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <h1 className="text-3xl font-bold">Procurement control events</h1>
          <p className="mt-1 max-w-4xl text-muted-foreground">
            Inspect tenant-scoped control decisions, rule and DEC lineage,
            actors, outcomes, state snapshots, correlations, and shared evidence
            references.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={refresh}
          disabled={summary.isFetching || events.isFetching}
        >
          <RefreshCw
            className={`mr-2 h-4 w-4 ${summary.isFetching || events.isFetching ? 'animate-spin' : ''}`}
          />
          Refresh
        </Button>
      </div>

      <Alert>
        <ShieldCheck className="h-4 w-4" />
        <AlertTitle>Append-only control record</AlertTitle>
        <AlertDescription>
          This surface cannot create, edit, or delete events. Evidence remains
          owned by the shared workflow/upload controls; this register stores
          verified references only.
        </AlertDescription>
      </Alert>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {summaryCards.map((item) => (
          <Card key={item.label}>
            <CardContent className="flex items-center justify-between p-5">
              <div>
                <p className="text-sm text-muted-foreground">{item.label}</p>
                <p className="mt-1 text-2xl font-semibold">
                  {summary.isLoading ? '—' : item.value}
                </p>
              </div>
              <item.icon className="h-6 w-6 text-muted-foreground" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Find control events</CardTitle>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={applyFilters}
            className="grid gap-3 md:grid-cols-2 xl:grid-cols-[2fr_1fr_1fr_1fr_auto]"
          >
            <div className="space-y-1.5">
              <Label htmlFor="event-search">Search</Label>
              <Input
                id="event-search"
                value={draftSearch}
                onChange={(event) => setDraftSearch(event.target.value)}
                placeholder="Actor, rule, source, correlation…"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="event-type">Event type</Label>
              <Input
                id="event-type"
                value={draftEventType}
                onChange={(event) => setDraftEventType(event.target.value)}
                placeholder="AccessDecision"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="event-result">Result</Label>
              <select
                id="event-result"
                value={draftResult}
                onChange={(event) => setDraftResult(event.target.value)}
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              >
                <option value="">All results</option>
                {procurementControlEventResults.map((result) => (
                  <option key={result} value={result}>
                    {result}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="source-type">Source type</Label>
              <Input
                id="source-type"
                value={draftSourceType}
                onChange={(event) => setDraftSourceType(event.target.value)}
                placeholder="PurchaseRequisition"
              />
            </div>
            <div className="flex items-end gap-2">
              <Button type="submit">
                <Search className="mr-2 h-4 w-4" />
                Apply
              </Button>
              <Button type="button" variant="ghost" onClick={resetFilters}>
                Reset
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0 pb-3">
          <div>
            <CardTitle className="text-base">Event register</CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">
              Newest occurrence first · latest{' '}
              {dateTime(summary.data?.latestOccurredAtUtc)}
            </p>
          </div>
          <Button
            variant="outline"
            onClick={() => integrity.mutate()}
            disabled={integrity.isPending}
          >
            <Fingerprint className="mr-2 h-4 w-4" />
            {integrity.isPending ? 'Verifying…' : 'Verify integrity'}
          </Button>
        </CardHeader>
        <CardContent className="p-0">
          {integrity.isError && (
            <div className="px-6 pb-4">
              <Alert variant="destructive">
                <ShieldAlert className="h-4 w-4" />
                <AlertTitle>Integrity verification failed</AlertTitle>
                <AlertDescription>{integrity.error.message}</AlertDescription>
              </Alert>
            </div>
          )}
          {integrity.data && (
            <div className="px-6 pb-4">
              <Alert
                variant={integrity.data.isValid ? 'default' : 'destructive'}
              >
                {integrity.data.isValid ? (
                  <ShieldCheck className="h-4 w-4" />
                ) : (
                  <ShieldAlert className="h-4 w-4" />
                )}
                <AlertTitle>
                  {integrity.data.isValid
                    ? 'Integrity verified'
                    : 'Integrity issue detected'}
                </AlertTitle>
                <AlertDescription>
                  Checked {integrity.data.checkedCount};{' '}
                  {integrity.data.validCount} valid;{' '}
                  {integrity.data.invalidCount} invalid at{' '}
                  {dateTime(integrity.data.verifiedAtUtc)}.
                  {integrity.data.issues.length > 0 &&
                    ` Affected keys: ${integrity.data.issues.map((item) => item.eventKey).join(', ')}.`}
                </AlertDescription>
              </Alert>
            </div>
          )}
          {events.isError && (
            <div className="px-6 pb-6">
              <Alert variant="destructive">
                <ShieldAlert className="h-4 w-4" />
                <AlertTitle>Unable to load the register</AlertTitle>
                <AlertDescription>{events.error.message}</AlertDescription>
              </Alert>
            </div>
          )}
          {events.isLoading ? (
            <p className="py-16 text-center text-sm text-muted-foreground">
              Loading control events…
            </p>
          ) : !page?.items.length ? (
            <EmptyState />
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Occurred</TableHead>
                    <TableHead>Result</TableHead>
                    <TableHead>Event / action</TableHead>
                    <TableHead>Actor</TableHead>
                    <TableHead>Rule / DEC</TableHead>
                    <TableHead>Source</TableHead>
                    <TableHead>Correlation</TableHead>
                    <TableHead className="text-right">Evidence</TableHead>
                    <TableHead />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {page.items.map((event: ProcurementControlEvent) => (
                    <TableRow key={event.id}>
                      <TableCell className="whitespace-nowrap">
                        {dateTime(event.occurredAtUtc)}
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant="outline"
                          className={procurementControlEventResultTone(
                            event.result
                          )}
                        >
                          {event.result}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <p className="font-medium">{event.eventType}</p>
                        <p className="text-xs text-muted-foreground">
                          {event.action}
                        </p>
                      </TableCell>
                      <TableCell>
                        <p className="font-medium">{event.actorName}</p>
                        <p className="max-w-40 truncate text-xs text-muted-foreground">
                          {event.actorRoles.join(', ') || 'No role claim'}
                        </p>
                      </TableCell>
                      <TableCell className="max-w-64">
                        <p
                          className="truncate"
                          title={procurementControlLineage(
                            event.ruleCode,
                            event.ruleVersion,
                            event.decisionKeys
                          )}
                        >
                          {procurementControlLineage(
                            event.ruleCode,
                            event.ruleVersion,
                            event.decisionKeys
                          )}
                        </p>
                      </TableCell>
                      <TableCell>
                        <p>{event.sourceType}</p>
                        <p
                          className="max-w-48 truncate text-xs text-muted-foreground"
                          title={event.sourceReference}
                        >
                          {event.sourceReference}
                        </p>
                      </TableCell>
                      <TableCell>
                        <button
                          type="button"
                          className="max-w-36 truncate font-mono text-xs text-primary hover:underline"
                          title={event.correlationId}
                          onClick={() => {
                            setDraftSearch(event.correlationId);
                            setFilters((current) => ({
                              ...current,
                              page: 1,
                              correlationId: event.correlationId,
                              search: undefined,
                            }));
                          }}
                        >
                          {shortHash(event.correlationId)}
                        </button>
                      </TableCell>
                      <TableCell className="text-right">
                        {event.evidence.length}
                      </TableCell>
                      <TableCell>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setSelectedId(event.id)}
                        >
                          View
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <Pagination
                currentPage={page.page}
                totalPages={totalPages}
                totalItems={page.totalCount}
                pageSize={page.pageSize}
                onPageChange={(next) =>
                  setFilters((current) => ({ ...current, page: next }))
                }
                onPageSizeChange={(next) =>
                  setFilters((current) => ({
                    ...current,
                    page: 1,
                    pageSize: next,
                  }))
                }
              />
            </>
          )}
        </CardContent>
      </Card>

      <DetailDialog
        selectedId={selectedId}
        onOpenChange={(open) => {
          if (!open) setSelectedId(undefined);
        }}
      />
    </div>
  );
}
