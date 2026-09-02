'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  Clock3,
  ExternalLink,
  FileCheck2,
  FileOutput,
  History,
  Plus,
  RefreshCw,
  RotateCcw,
  Send,
  XCircle,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Pagination } from '@/components/ui/pagination';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import {
  procurementAppSubmissionActions,
  procurementAppSubmissionStatusTone,
  readProcurementAppExportFile,
  validateProcurementAppExport,
} from '@/lib/procurement-app-submission';
import { procurementAppSubmissionService } from '@/services/procurement-app-submission.service';
import type {
  ProcurementAppEvidenceReference,
  ProcurementAppSubmission,
  ProcurementAppSubmissionSearch,
  ProcurementAppSubmissionStatus,
  RecordProcurementAppExport,
} from '@/types/procurement-app-submission';

type LifecycleAction = 'submit' | 'acknowledge' | 'reject' | 'resubmit';

interface LifecycleForm {
  reference: string;
  reason: string;
  eventAt: string;
  exportFileName: string;
  exportFormat: string;
  exportTemplateVersion: string;
  exportChecksumSha256: string;
  notes: string;
}

const statusOptions: ProcurementAppSubmissionStatus[] = [
  'Exported',
  'Submitted',
  'Acknowledged',
  'Rejected',
];

const nowForInput = () => {
  const value = new Date();
  value.setMinutes(value.getMinutes() - value.getTimezoneOffset());
  return value.toISOString().slice(0, 16);
};

const newExport = (): RecordProcurementAppExport => ({
  procurementPlanId: '',
  exportFileName: '',
  exportFormat: 'XLSX',
  exportTemplateVersion: '',
  exportChecksumSha256: '',
  notes: '',
  evidence: [],
});

const newLifecycle = (): LifecycleForm => ({
  reference: '',
  reason: '',
  eventAt: nowForInput(),
  exportFileName: '',
  exportFormat: 'XLSX',
  exportTemplateVersion: '',
  exportChecksumSha256: '',
  notes: '',
});

const dateTime = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';

const shortHash = (value: string) =>
  value.length > 18 ? `${value.slice(0, 10)}…${value.slice(-8)}` : value;

export default function ProcurementAppSubmissionsPage() {
  const { hasRole } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const canManage = [
    'SuperAdmin',
    'TenantAdmin',
    'TDC_PROCUREMENT_OFFICER',
    'TDC_SENIOR_PROCUREMENT_OFFICER',
    'TDC_HEAD_OF_PROCUREMENT',
  ].some(hasRole);
  const [filters, setFilters] = useState<ProcurementAppSubmissionSearch>({
    page: 1,
    pageSize: 25,
  });
  const [selectedId, setSelectedId] = useState<string>();
  const [exportOpen, setExportOpen] = useState(false);
  const [exportForm, setExportForm] =
    useState<RecordProcurementAppExport>(newExport);
  const [action, setAction] = useState<LifecycleAction>();
  const [actionTarget, setActionTarget] = useState<ProcurementAppSubmission>();
  const [lifecycleForm, setLifecycleForm] =
    useState<LifecycleForm>(newLifecycle);
  const [evidenceValue, setEvidenceValue] = useState('');

  const summary = useQuery({
    queryKey: ['procurement-app-submission-summary'],
    queryFn: procurementAppSubmissionService.summary,
  });
  const plans = useQuery({
    queryKey: ['procurement-app-submission-plans'],
    queryFn: procurementAppSubmissionService.publishedPlans,
  });
  const register = useQuery({
    queryKey: ['procurement-app-submissions', filters],
    queryFn: () => procurementAppSubmissionService.search(filters),
  });
  const detail = useQuery({
    queryKey: ['procurement-app-submission', selectedId],
    queryFn: () =>
      selectedId
        ? procurementAppSubmissionService.get(selectedId)
        : Promise.reject(new Error('No APP submission selected.')),
    enabled: Boolean(selectedId),
  });

  const availablePlans = useMemo(
    () => (plans.data ?? []).filter((plan) => !plan.hasSubmissionRegister),
    [plans.data]
  );
  const exportError = validateProcurementAppExport(exportForm);

  const evidence = (
    requirementKey: string
  ): ProcurementAppEvidenceReference[] => {
    if (!evidenceValue.trim()) return [];
    return [
      {
        referenceKind: 'ExternalReference',
        reference: evidenceValue.trim(),
        label: 'APP lifecycle evidence',
        requirementKey,
      },
    ];
  };

  const selectExportPackage = async (
    file: File | undefined,
    target: 'export' | 'resubmit'
  ) => {
    if (!file) return;
    try {
      const metadata = await readProcurementAppExportFile(file);
      if (target === 'export') {
        setExportForm((current) => ({ ...current, ...metadata }));
      } else {
        setLifecycleForm((current) => ({ ...current, ...metadata }));
      }
    } catch (error) {
      console.error('Unable to calculate APP export checksum:', error);
      toast({
        title: 'Unable to read export package',
        description: 'Select the generated APP export file and try again.',
        variant: 'destructive',
      });
    }
  };

  const invalidate = async (id?: string) => {
    if (id) setSelectedId(id);
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['procurement-app-submission-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-app-submission-plans'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-app-submissions'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['procurement-app-submission'],
      }),
    ]);
  };

  const recordExport = useMutation({
    mutationFn: () =>
      procurementAppSubmissionService.recordExport({
        ...exportForm,
        evidence: evidence('APP_EXPORT'),
      }),
    onSuccess: async (value) => {
      setExportOpen(false);
      setExportForm(newExport());
      setEvidenceValue('');
      await invalidate(value.id);
      toast({
        title: `${value.submissionNumber} registered`,
        description:
          'The published plan export and checksum are now in the immutable APP timeline.',
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to record APP export',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const runLifecycle = useMutation({
    mutationFn: async () => {
      if (!action || !actionTarget)
        throw new Error('Select an APP lifecycle action.');
      const eventAt = new Date(lifecycleForm.eventAt).toISOString();
      const shared = evidence(`APP_${action.toUpperCase()}`);
      switch (action) {
        case 'submit':
          return procurementAppSubmissionService.submit(actionTarget.id, {
            externalSubmissionReference: lifecycleForm.reference,
            submittedAtUtc: eventAt,
            notes: lifecycleForm.notes || undefined,
            rowVersion: actionTarget.rowVersion,
            evidence: shared,
          });
        case 'acknowledge':
          return procurementAppSubmissionService.acknowledge(actionTarget.id, {
            acknowledgementReference: lifecycleForm.reference,
            acknowledgedAtUtc: eventAt,
            notes: lifecycleForm.notes || undefined,
            rowVersion: actionTarget.rowVersion,
            evidence: shared,
          });
        case 'reject':
          return procurementAppSubmissionService.reject(actionTarget.id, {
            rejectionReference: lifecycleForm.reference,
            rejectionReason: lifecycleForm.reason,
            rejectedAtUtc: eventAt,
            notes: lifecycleForm.notes || undefined,
            rowVersion: actionTarget.rowVersion,
            evidence: shared,
          });
        case 'resubmit':
          return procurementAppSubmissionService.resubmit(actionTarget.id, {
            exportFileName: lifecycleForm.exportFileName,
            exportFormat: lifecycleForm.exportFormat,
            exportTemplateVersion: lifecycleForm.exportTemplateVersion,
            exportChecksumSha256: lifecycleForm.exportChecksumSha256,
            notes: lifecycleForm.notes || undefined,
            rowVersion: actionTarget.rowVersion,
            evidence: shared,
          });
      }
    },
    onSuccess: async (value) => {
      setAction(undefined);
      setActionTarget(undefined);
      setLifecycleForm(newLifecycle());
      setEvidenceValue('');
      await invalidate(value.id);
      toast({
        title: `${value.submissionNumber} is ${value.status}`,
        description: `Attempt ${value.attemptNumber} and its evidence are recorded in the APP timeline.`,
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'APP lifecycle action failed',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const openAction = (
    nextAction: LifecycleAction,
    item: ProcurementAppSubmission
  ) => {
    setAction(nextAction);
    setActionTarget(item);
    setLifecycleForm(newLifecycle());
    setEvidenceValue('');
  };

  const actionValid = (() => {
    if (!action) return false;
    if (action === 'resubmit')
      return !validateProcurementAppExport({
        procurementPlanId: actionTarget?.procurementPlanId ?? '',
        exportFileName: lifecycleForm.exportFileName,
        exportFormat: lifecycleForm.exportFormat,
        exportTemplateVersion: lifecycleForm.exportTemplateVersion,
        exportChecksumSha256: lifecycleForm.exportChecksumSha256,
      });
    if (!lifecycleForm.reference.trim() || !lifecycleForm.eventAt) return false;
    return action !== 'reject' || Boolean(lifecycleForm.reason.trim());
  })();

  const selected = detail.data;
  const selectedActions = selected
    ? procurementAppSubmissionActions(selected)
    : undefined;
  const loadError = summary.isError || plans.isError || register.isError;

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <h1 className="text-3xl font-bold">APP submission register</h1>
          <p className="mt-1 max-w-4xl text-muted-foreground">
            Control published procurement-plan exports, GHANEPS/PPA references,
            acknowledgements, rejections, resubmissions, and shared evidence
            without changing the approved planning workflow.
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            onClick={() =>
              void Promise.all([
                summary.refetch(),
                plans.refetch(),
                register.refetch(),
              ])
            }
          >
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
          {canManage && (
            <Button
              onClick={() => {
                setExportForm(newExport());
                setEvidenceValue('');
                setExportOpen(true);
              }}
            >
              <Plus className="mr-2 h-4 w-4" /> Record export
            </Button>
          )}
        </div>
      </div>

      {loadError && (
        <Alert variant="destructive">
          <XCircle className="h-4 w-4" />
          <AlertTitle>APP register could not be loaded</AlertTitle>
          <AlertDescription>
            Check the tenant session and procurement role, then retry.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {[
          {
            label: 'Published plans',
            value: summary.data?.publishedPlanCount ?? 0,
            icon: FileCheck2,
          },
          {
            label: 'Registered plans',
            value: summary.data?.registeredPlanCount ?? 0,
            icon: FileOutput,
          },
          {
            label: 'Awaiting response',
            value: summary.data?.submittedCount ?? 0,
            icon: Clock3,
          },
          {
            label: 'Acknowledged',
            value: summary.data?.acknowledgedCount ?? 0,
            icon: CheckCircle2,
          },
          {
            label: 'Rejected',
            value: summary.data?.rejectedCount ?? 0,
            icon: XCircle,
          },
        ].map(({ label, value, icon: Icon }) => (
          <Card key={label}>
            <CardContent className="flex items-center justify-between p-5">
              <div>
                <p className="text-sm text-muted-foreground">{label}</p>
                <p className="mt-1 text-2xl font-semibold">{value}</p>
              </div>
              <Icon className="h-5 w-5 text-muted-foreground" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Submission attempts</CardTitle>
          <CardDescription>
            Each rejected submission remains immutable; resubmission creates a
            linked attempt in the same timeline.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_190px_150px]">
            <Input
              aria-label="Search APP register"
              placeholder="Search register, plan, or external reference"
              value={filters.search ?? ''}
              onChange={(event) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  search: event.target.value || undefined,
                }))
              }
            />
            <Select
              value={filters.status ?? 'all'}
              onValueChange={(value) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  status:
                    value === 'all'
                      ? undefined
                      : (value as ProcurementAppSubmissionStatus),
                }))
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="All statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {statusOptions.map((status) => (
                  <SelectItem key={status} value={status}>
                    {status}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Input
              aria-label="Fiscal year"
              type="number"
              min={2000}
              max={2200}
              placeholder="Fiscal year"
              value={filters.fiscalYear ?? ''}
              onChange={(event) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  fiscalYear: event.target.value
                    ? Number(event.target.value)
                    : undefined,
                }))
              }
            />
          </div>

          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Register / attempt</TableHead>
                  <TableHead>Published plan</TableHead>
                  <TableHead>Export package</TableHead>
                  <TableHead>External reference</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(register.data?.items ?? []).map((item) => (
                  <TableRow key={item.id}>
                    <TableCell>
                      <button
                        className="text-left font-medium hover:underline"
                        onClick={() => setSelectedId(item.id)}
                      >
                        {item.submissionNumber}
                      </button>
                      <div className="text-xs text-muted-foreground">
                        Attempt {item.attemptNumber} ·{' '}
                        {dateTime(item.exportedAtUtc)}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>
                        {item.planNumber} · rev {item.planRevisionNumber}
                      </div>
                      <div className="max-w-64 truncate text-xs text-muted-foreground">
                        {item.planTitle} · FY {item.fiscalYear}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{item.exportFileName}</div>
                      <div className="text-xs text-muted-foreground">
                        {item.exportFormat} · template{' '}
                        {item.exportTemplateVersion}
                      </div>
                    </TableCell>
                    <TableCell>
                      {item.acknowledgementReference ??
                        item.rejectionReference ??
                        item.externalSubmissionReference ??
                        '—'}
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={procurementAppSubmissionStatusTone(
                          item.status
                        )}
                      >
                        {item.status}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setSelectedId(item.id)}
                      >
                        View
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
                {!register.isLoading && !register.data?.items.length && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="py-10 text-center text-muted-foreground"
                    >
                      No APP submission attempts match the current filters.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
          <Pagination
            currentPage={register.data?.page ?? 1}
            totalPages={Math.max(
              1,
              Math.ceil(
                (register.data?.totalCount ?? 0) /
                  (register.data?.pageSize ?? 25)
              )
            )}
            totalItems={register.data?.totalCount ?? 0}
            pageSize={register.data?.pageSize ?? 25}
            onPageChange={(page) =>
              setFilters((current) => ({ ...current, page }))
            }
            onPageSizeChange={(pageSize) =>
              setFilters((current) => ({ ...current, page: 1, pageSize }))
            }
          />
        </CardContent>
      </Card>

      {selected && (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
          <Card>
            <CardHeader>
              <div className="flex items-start justify-between gap-3">
                <div>
                  <CardTitle>
                    {selected.submissionNumber} · attempt{' '}
                    {selected.attemptNumber}
                  </CardTitle>
                  <CardDescription>
                    {selected.planNumber} · revision{' '}
                    {selected.planRevisionNumber}
                  </CardDescription>
                </div>
                <Badge
                  variant="outline"
                  className={procurementAppSubmissionStatusTone(
                    selected.status
                  )}
                >
                  {selected.status}
                </Badge>
              </div>
            </CardHeader>
            <CardContent className="space-y-5">
              <dl className="grid gap-4 text-sm sm:grid-cols-2">
                <div>
                  <dt className="text-muted-foreground">Export file</dt>
                  <dd className="mt-1 font-medium">
                    {selected.exportFileName}
                  </dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">Format / template</dt>
                  <dd className="mt-1 font-medium">
                    {selected.exportFormat} / {selected.exportTemplateVersion}
                  </dd>
                </div>
                <div className="sm:col-span-2">
                  <dt className="text-muted-foreground">SHA-256</dt>
                  <dd className="mt-1 break-all font-mono text-xs">
                    {selected.exportChecksumSha256}
                  </dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">Submitted</dt>
                  <dd className="mt-1">{dateTime(selected.submittedAtUtc)}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">
                    Submission reference
                  </dt>
                  <dd className="mt-1">
                    {selected.externalSubmissionReference ?? '—'}
                  </dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">Acknowledgement</dt>
                  <dd className="mt-1">
                    {selected.acknowledgementReference ?? '—'}
                  </dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">Rejection</dt>
                  <dd className="mt-1">{selected.rejectionReference ?? '—'}</dd>
                </div>
              </dl>
              {selected.rejectionReason && (
                <Alert variant="destructive">
                  <XCircle className="h-4 w-4" />
                  <AlertTitle>External rejection reason</AlertTitle>
                  <AlertDescription>
                    {selected.rejectionReason}
                  </AlertDescription>
                </Alert>
              )}
              {canManage && selectedActions && (
                <div className="flex flex-wrap gap-2 border-t pt-4">
                  {selectedActions.canSubmit && (
                    <Button
                      size="sm"
                      onClick={() => openAction('submit', selected)}
                    >
                      <Send className="mr-2 h-4 w-4" /> Submit
                    </Button>
                  )}
                  {selectedActions.canAcknowledge && (
                    <Button
                      size="sm"
                      onClick={() => openAction('acknowledge', selected)}
                    >
                      <CheckCircle2 className="mr-2 h-4 w-4" /> Acknowledge
                    </Button>
                  )}
                  {selectedActions.canReject && (
                    <Button
                      size="sm"
                      variant="destructive"
                      onClick={() => openAction('reject', selected)}
                    >
                      <XCircle className="mr-2 h-4 w-4" /> Reject
                    </Button>
                  )}
                  {selectedActions.canResubmit && (
                    <Button
                      size="sm"
                      onClick={() => openAction('resubmit', selected)}
                    >
                      <RotateCcw className="mr-2 h-4 w-4" /> Record resubmission
                    </Button>
                  )}
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <History className="h-5 w-5" /> Immutable timeline
              </CardTitle>
              <CardDescription>
                One correlation series across every attempt, backed by the
                shared procurement control-event ledger.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {selected.timeline.map((event, index) => (
                <div key={event.id} className="relative rounded-md border p-4">
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div>
                      <p className="font-medium">
                        {index + 1}. {event.action}
                      </p>
                      <p className="text-sm text-muted-foreground">
                        {event.actorName} · {dateTime(event.occurredAtUtc)}
                      </p>
                    </div>
                    <Badge variant="outline">{event.result}</Badge>
                  </div>
                  {event.reason && (
                    <p className="mt-3 text-sm">{event.reason}</p>
                  )}
                  <div className="mt-3 space-y-2">
                    {event.evidence.map((item) => (
                      <div
                        key={`${item.referenceKind}-${item.reference}`}
                        className="flex items-start gap-2 rounded bg-muted/50 p-2 text-xs"
                      >
                        <ExternalLink className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                        <div>
                          <p className="font-medium">
                            {item.label ?? item.referenceKind}
                          </p>
                          <p className="break-all text-muted-foreground">
                            {item.fileName ?? item.reference}
                          </p>
                        </div>
                      </div>
                    ))}
                  </div>
                  <p className="mt-3 font-mono text-[11px] text-muted-foreground">
                    Integrity {shortHash(event.integrityHash)}
                  </p>
                </div>
              ))}
            </CardContent>
          </Card>
        </div>
      )}

      <Dialog open={exportOpen} onOpenChange={setExportOpen}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Record published-plan APP export</DialogTitle>
            <DialogDescription>
              Select the generated export package. Its file name, format and
              integrity checksum are calculated automatically; the package
              content is not uploaded by this screen.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label>Published plan version</Label>
              <Select
                value={exportForm.procurementPlanId}
                onValueChange={(value) =>
                  setExportForm((current) => ({
                    ...current,
                    procurementPlanId: value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select an unregistered published plan" />
                </SelectTrigger>
                <SelectContent>
                  {availablePlans.map((plan) => (
                    <SelectItem key={plan.id} value={plan.id}>
                      {plan.planNumber} · rev {plan.revisionNumber} · FY{' '}
                      {plan.fiscalYear}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {!availablePlans.length && (
                <p className="text-xs text-muted-foreground">
                  Every eligible published plan already has a register, or no
                  published plan has items.
                </p>
              )}
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="app-export-package">Export package</Label>
              <Input
                id="app-export-package"
                type="file"
                accept=".xlsx,.xls,.csv,.xml,.json"
                onChange={(event) =>
                  void selectExportPackage(event.target.files?.[0], 'export')
                }
              />
              {exportForm.exportFileName && (
                <p className="text-xs text-muted-foreground">
                  {exportForm.exportFileName} · {exportForm.exportFormat} ·
                  integrity verified
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label>Template version</Label>
              <Input
                value={exportForm.exportTemplateVersion}
                onChange={(event) =>
                  setExportForm((current) => ({
                    ...current,
                    exportTemplateVersion: event.target.value,
                  }))
                }
                placeholder="PPA-APP-v1"
              />
            </div>
            <EvidenceFields
              value={evidenceValue}
              onValue={setEvidenceValue}
            />
            <div className="space-y-2 sm:col-span-2">
              <Label>Notes</Label>
              <Textarea
                value={exportForm.notes ?? ''}
                onChange={(event) =>
                  setExportForm((current) => ({
                    ...current,
                    notes: event.target.value,
                  }))
                }
              />
            </div>
          </div>
          {exportError && (
            <p className="text-sm text-destructive">{exportError}</p>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setExportOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={Boolean(exportError) || recordExport.isPending}
              onClick={() => recordExport.mutate()}
            >
              {recordExport.isPending ? 'Recording…' : 'Record export'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(action)}
        onOpenChange={(open) => !open && setAction(undefined)}
      >
        <DialogContent className="max-h-[90vh] max-w-xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {action ? action[0].toUpperCase() + action.slice(1) : ''} APP
              attempt
            </DialogTitle>
            <DialogDescription>
              {actionTarget?.submissionNumber} · attempt{' '}
              {actionTarget?.attemptNumber}. The action and evidence are
              appended to the immutable timeline.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            {action === 'resubmit' ? (
              <>
                <div className="space-y-2 sm:col-span-2">
                  <Label htmlFor="app-replacement-package">
                    Replacement export package
                  </Label>
                  <Input
                    id="app-replacement-package"
                    type="file"
                    accept=".xlsx,.xls,.csv,.xml,.json"
                    onChange={(event) =>
                      void selectExportPackage(
                        event.target.files?.[0],
                        'resubmit'
                      )
                    }
                  />
                  {lifecycleForm.exportFileName && (
                    <p className="text-xs text-muted-foreground">
                      {lifecycleForm.exportFileName} ·{' '}
                      {lifecycleForm.exportFormat} · integrity verified
                    </p>
                  )}
                </div>
                <div className="space-y-2">
                  <Label>Template version</Label>
                  <Input
                    value={lifecycleForm.exportTemplateVersion}
                    onChange={(event) =>
                      setLifecycleForm((current) => ({
                        ...current,
                        exportTemplateVersion: event.target.value,
                      }))
                    }
                  />
                </div>
              </>
            ) : (
              <>
                <div className="space-y-2 sm:col-span-2">
                  <Label>
                    {action === 'submit'
                      ? 'GHANEPS/PPA submission reference'
                      : action === 'acknowledge'
                        ? 'Acknowledgement reference'
                        : 'Rejection notice reference'}
                  </Label>
                  <Input
                    value={lifecycleForm.reference}
                    onChange={(event) =>
                      setLifecycleForm((current) => ({
                        ...current,
                        reference: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="space-y-2 sm:col-span-2">
                  <Label>External event date and time</Label>
                  <Input
                    type="datetime-local"
                    value={lifecycleForm.eventAt}
                    onChange={(event) =>
                      setLifecycleForm((current) => ({
                        ...current,
                        eventAt: event.target.value,
                      }))
                    }
                  />
                </div>
                {action === 'reject' && (
                  <div className="space-y-2 sm:col-span-2">
                    <Label>Rejection reason</Label>
                    <Textarea
                      value={lifecycleForm.reason}
                      onChange={(event) =>
                        setLifecycleForm((current) => ({
                          ...current,
                          reason: event.target.value,
                        }))
                      }
                    />
                  </div>
                )}
              </>
            )}
            <EvidenceFields
              value={evidenceValue}
              onValue={setEvidenceValue}
            />
            <div className="space-y-2 sm:col-span-2">
              <Label>Notes</Label>
              <Textarea
                value={lifecycleForm.notes}
                onChange={(event) =>
                  setLifecycleForm((current) => ({
                    ...current,
                    notes: event.target.value,
                  }))
                }
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAction(undefined)}>
              Cancel
            </Button>
            <Button
              variant={action === 'reject' ? 'destructive' : 'default'}
              disabled={!actionValid || runLifecycle.isPending}
              onClick={() => runLifecycle.mutate()}
            >
              {runLifecycle.isPending ? 'Recording…' : 'Confirm action'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function EvidenceFields({
  value,
  onValue,
}: {
  value: string;
  onValue: (value: string) => void;
}) {
  return (
    <div className="space-y-2 sm:col-span-2">
      <Label>External submission or acknowledgement reference (optional)</Label>
      <Input
        value={value}
        onChange={(event) => onValue(event.target.value)}
        placeholder="Official GHANEPS/PPA reference, if available"
      />
    </div>
  );
}
