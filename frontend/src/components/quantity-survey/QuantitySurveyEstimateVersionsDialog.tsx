'use client';

import { useCallback, useMemo, useState } from 'react';
import { Calculator, Check, History, Plus, Send, X } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import {
  projectService,
  type CreateQuantitySurveyEstimateRequest,
  type ProjectBoqVersionWorkspaceDto,
  type QuantitySurveyEstimateAssumptionRequest,
  type QuantitySurveyEstimateType,
  type QuantitySurveyEstimateVersionDto,
  type QuantitySurveyEstimateWorkspaceDto,
} from '@/services/projectService';

type Props = { projectId: string };
const TYPES: Array<{ value: QuantitySurveyEstimateType; label: string }> = [
  { value: 'CostPlan', label: 'Cost plan' },
  { value: 'TenderEstimate', label: 'Tender estimate' },
  { value: 'BudgetEstimate', label: 'Budget estimate' },
];
const MARKUPS = [
  { component: 5, label: 'Overhead' },
  { component: 6, label: 'Profit' },
  { component: 8, label: 'Contingency' },
  { component: 9, label: 'Wastage' },
];
const formatMoney = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currency || 'GHS',
  }).format(value || 0);
const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';
const statusTone = (status: string) =>
  status === 'Approved'
    ? 'default'
    : status === 'Rejected'
      ? 'destructive'
      : 'secondary';

export function QuantitySurveyEstimateVersionsDialog({ projectId }: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const canManage = hasPermission('quantity-survey.estimates.manage');
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [workspace, setWorkspace] =
    useState<QuantitySurveyEstimateWorkspaceDto | null>(null);
  const [boqWorkspace, setBoqWorkspace] =
    useState<ProjectBoqVersionWorkspaceDto | null>(null);
  const [selectedId, setSelectedId] = useState<string>();
  const [estimateType, setEstimateType] =
    useState<QuantitySurveyEstimateType>('CostPlan');
  const [boqVersionId, setBoqVersionId] = useState('');
  const [name, setName] = useState('');
  const [changeReason, setChangeReason] = useState('');
  const [assumptions, setAssumptions] = useState<
    QuantitySurveyEstimateAssumptionRequest[]
  >([]);
  const [markups, setMarkups] = useState<Record<number, string>>({});
  const [workflowNote, setWorkflowNote] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [estimates, boqs] = await Promise.all([
        projectService.getQuantitySurveyEstimateWorkspace(projectId),
        projectService.getProjectBoqVersionWorkspace(projectId),
      ]);
      setWorkspace(estimates);
      setBoqWorkspace(boqs);
      setBoqVersionId(
        (current) => current || estimates.approvedBoqVersionIds[0] || ''
      );
      setSelectedId((current) => current || estimates.versions[0]?.id);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Failed to load QS estimates'
      );
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  const selected = workspace?.versions.find((value) => value.id === selectedId);
  const currentSource = useMemo(
    () =>
      workspace?.versions
        .filter(
          (value) =>
            value.estimateType === estimateType && value.status === 'Approved'
        )
        .sort((left, right) => right.versionNumber - left.versionNumber)[0],
    [estimateType, workspace]
  );
  const approvedBoqs = useMemo(
    () =>
      (boqWorkspace?.versions ?? []).filter((value) =>
        workspace?.approvedBoqVersionIds.includes(value.id)
      ),
    [boqWorkspace, workspace]
  );

  const create = async () => {
    if (!boqVersionId || !name.trim() || changeReason.trim().length < 5) {
      toast.error(
        'Select an approved BoQ and enter a name and meaningful reason.'
      );
      return;
    }
    const invalidAssumption = assumptions.some(
      (value) =>
        !value.code.trim() || !value.description.trim() || !value.value.trim()
    );
    if (invalidAssumption) {
      toast.error('Complete every assumption or remove the empty row.');
      return;
    }
    setLoading(true);
    try {
      const request: CreateQuantitySurveyEstimateRequest = {
        clientRequestId: crypto.randomUUID(),
        projectBoqVersionId: boqVersionId,
        sourceEstimateVersionId: currentSource?.id,
        estimateType,
        name: name.trim(),
        changeReason: changeReason.trim(),
        estimateDate: new Date().toISOString(),
        assumptions,
        markups: MARKUPS.map((value) => ({
          component: value.component,
          percentage: Number(markups[value.component] || 0),
        })).filter((value) => value.percentage > 0),
      };
      const created = await projectService.createQuantitySurveyEstimate(
        projectId,
        request
      );
      toast.success(
        `${TYPES.find((value) => value.value === estimateType)?.label} v${created.versionNumber} created`
      );
      setName('');
      setChangeReason('');
      setAssumptions([]);
      setMarkups({});
      await load();
      setSelectedId(created.id);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Failed to create QS estimate'
      );
    } finally {
      setLoading(false);
    }
  };

  const runAction = async (
    version: QuantitySurveyEstimateVersionDto,
    action: 'submit' | 'approve' | 'reject'
  ) => {
    if (action === 'reject' && workflowNote.trim().length < 5) {
      toast.error('Enter a rejection reason of at least 5 characters.');
      return;
    }
    setLoading(true);
    try {
      await projectService.runQuantitySurveyEstimateAction(
        projectId,
        version.id,
        action,
        action === 'reject'
          ? { reason: workflowNote.trim() }
          : action === 'approve'
            ? { comments: workflowNote.trim() || undefined }
            : undefined
      );
      toast.success(
        `Estimate ${action === 'submit' ? 'submitted' : action === 'approve' ? 'approved' : 'rejected'}`
      );
      setWorkflowNote('');
      await load();
      setSelectedId(version.id);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : `Failed to ${action} estimate`
      );
    } finally {
      setLoading(false);
    }
  };

  if (!canRead) return null;

  return (
    <Dialog
      open={open}
      onOpenChange={(value) => {
        setOpen(value);
        if (value) void load();
      }}
    >
      <DialogTrigger asChild>
        <Button variant="outline" className="gap-2">
          <Calculator className="h-4 w-4" />
          QS Estimates
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[94vh] max-w-[96vw] overflow-y-auto xl:max-w-7xl">
        <DialogHeader>
          <DialogTitle>
            Cost plans, tender estimates & budget estimates
          </DialogTitle>
        </DialogHeader>
        <div className="grid gap-5 xl:grid-cols-[minmax(0,1.1fr)_minmax(380px,0.9fr)]">
          <div className="space-y-4">
            <div className="overflow-hidden rounded-lg border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Version</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Source BoQ</TableHead>
                    <TableHead className="text-right">Total</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {workspace?.versions.length ? (
                    workspace.versions.map((version) => (
                      <TableRow
                        key={version.id}
                        className={
                          selectedId === version.id
                            ? 'bg-muted/70'
                            : 'cursor-pointer'
                        }
                        onClick={() => setSelectedId(version.id)}
                      >
                        <TableCell>
                          <div className="font-medium">{version.name}</div>
                          <div className="text-xs text-muted-foreground">
                            {
                              TYPES.find(
                                (value) => value.value === version.estimateType
                              )?.label
                            }{' '}
                            · v{version.versionNumber}
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant={statusTone(version.status)}>
                            {version.status}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          {boqWorkspace?.versions.find(
                            (value) => value.id === version.projectBoqVersionId
                          )?.versionNumber
                            ? `v${boqWorkspace.versions.find((value) => value.id === version.projectBoqVersionId)?.versionNumber}`
                            : 'Approved publication'}
                        </TableCell>
                        <TableCell className="text-right font-medium">
                          {formatMoney(
                            version.totalAmount,
                            version.currencyCode
                          )}
                        </TableCell>
                      </TableRow>
                    ))
                  ) : (
                    <TableRow>
                      <TableCell
                        colSpan={4}
                        className="h-20 text-center text-muted-foreground"
                      >
                        {loading ? 'Loading…' : 'No estimate versions yet.'}
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </div>

            {selected ? (
              <div className="space-y-3 rounded-lg border p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h3 className="font-semibold">
                      {selected.name} · v{selected.versionNumber}
                    </h3>
                    <p className="text-sm text-muted-foreground">
                      Prepared {formatDate(selected.estimateDate)} from QS
                      configuration v{selected.configurationProfileVersion}
                    </p>
                  </div>
                  <div className="flex gap-2">
                    {canManage &&
                      (selected.status === 'Draft' ||
                        selected.status === 'Rejected') && (
                        <Button
                          size="sm"
                          onClick={() => void runAction(selected, 'submit')}
                          disabled={loading}
                        >
                          <Send className="mr-2 h-4 w-4" />
                          Submit
                        </Button>
                      )}
                    {canApprove && selected.status === 'PendingApproval' && (
                      <>
                        <Button
                          size="sm"
                          onClick={() => void runAction(selected, 'approve')}
                          disabled={loading}
                        >
                          <Check className="mr-2 h-4 w-4" />
                          Approve
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() => void runAction(selected, 'reject')}
                          disabled={loading}
                        >
                          <X className="mr-2 h-4 w-4" />
                          Reject
                        </Button>
                      </>
                    )}
                  </div>
                </div>
                {canApprove && selected.status === 'PendingApproval' && (
                  <Textarea
                    value={workflowNote}
                    onChange={(event) => setWorkflowNote(event.target.value)}
                    placeholder="Approval comment or rejection reason"
                  />
                )}
                <div className="grid grid-cols-3 gap-3 text-sm">
                  <div>
                    <span className="text-muted-foreground">Direct cost</span>
                    <div className="font-semibold">
                      {formatMoney(selected.directCost, selected.currencyCode)}
                    </div>
                  </div>
                  <div>
                    <span className="text-muted-foreground">Markups</span>
                    <div className="font-semibold">
                      {formatMoney(selected.markupTotal, selected.currencyCode)}
                    </div>
                  </div>
                  <div>
                    <span className="text-muted-foreground">Total</span>
                    <div className="font-semibold">
                      {formatMoney(selected.totalAmount, selected.currencyCode)}
                    </div>
                  </div>
                </div>
                <div className="max-h-64 overflow-auto rounded border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>BoQ line</TableHead>
                        <TableHead>Rate source</TableHead>
                        <TableHead className="text-right">Qty</TableHead>
                        <TableHead className="text-right">Rate</TableHead>
                        <TableHead className="text-right">Amount</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {selected.lines.map((line) => (
                        <TableRow key={line.id}>
                          <TableCell>
                            <div className="font-medium">
                              {line.lineNumber} ·{' '}
                              {line.itemCode || 'No item code'}
                            </div>
                            <div className="max-w-72 truncate text-xs text-muted-foreground">
                              {line.description}
                            </div>
                          </TableCell>
                          <TableCell>
                            {line.rateSource}
                            {line.sourceRateVersion
                              ? ` v${line.sourceRateVersion}`
                              : ''}
                          </TableCell>
                          <TableCell className="text-right">
                            {line.quantity}
                          </TableCell>
                          <TableCell className="text-right">
                            {line.unitRate.toLocaleString()}
                          </TableCell>
                          <TableCell className="text-right">
                            {line.lineAmount.toLocaleString()}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
                <div>
                  <h4 className="mb-2 flex items-center gap-2 text-sm font-semibold">
                    <History className="h-4 w-4" />
                    Approval history
                  </h4>
                  <div className="space-y-1 text-sm">
                    {selected.approvalHistory.map((item) => (
                      <div
                        key={item.id}
                        className="flex justify-between gap-4 rounded bg-muted/50 px-3 py-2"
                      >
                        <span>
                          {item.action} · {item.actorName}
                          {item.reason ? ` — ${item.reason}` : ''}
                        </span>
                        <span className="shrink-0 text-muted-foreground">
                          {formatDate(item.createdAt)}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            ) : null}
          </div>

          {canManage ? (
            <div className="space-y-4 rounded-lg border p-4">
              <div>
                <h3 className="font-semibold">
                  Create controlled estimate version
                </h3>
                <p className="text-sm text-muted-foreground">
                  Lines and effective published rates are snapshotted from the
                  selected approved BoQ.
                </p>
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label>Estimate type</Label>
                  <Select
                    value={estimateType}
                    onValueChange={(value) =>
                      setEstimateType(value as QuantitySurveyEstimateType)
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {TYPES.map((value) => (
                        <SelectItem key={value.value} value={value.value}>
                          {value.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1">
                  <Label>Approved BoQ</Label>
                  <Select value={boqVersionId} onValueChange={setBoqVersionId}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select approved BoQ" />
                    </SelectTrigger>
                    <SelectContent>
                      {approvedBoqs.map((value) => (
                        <SelectItem key={value.id} value={value.id}>
                          v{value.versionNumber} · {value.versionType} ·{' '}
                          {value.lineCount} lines
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              {currentSource && (
                <p className="rounded bg-muted px-3 py-2 text-sm">
                  Revision source: {currentSource.name} v
                  {currentSource.versionNumber}
                </p>
              )}
              <div className="space-y-1">
                <Label>Name</Label>
                <Input
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  placeholder="e.g. Pre-tender cost plan"
                />
              </div>
              <div className="space-y-1">
                <Label>Change reason</Label>
                <Textarea
                  value={changeReason}
                  onChange={(event) => setChangeReason(event.target.value)}
                  placeholder="Purpose and basis of this estimate version"
                />
              </div>
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <Label>Assumptions</Label>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() =>
                      setAssumptions((values) => [
                        ...values,
                        { code: '', description: '', value: '' },
                      ])
                    }
                  >
                    <Plus className="mr-1 h-3.5 w-3.5" />
                    Add
                  </Button>
                </div>
                {assumptions.map((assumption, index) => (
                  <div
                    key={index}
                    className="grid grid-cols-[0.7fr_1.3fr_1fr_auto] gap-2"
                  >
                    <Input
                      value={assumption.code}
                      onChange={(event) =>
                        setAssumptions((values) =>
                          values.map((value, itemIndex) =>
                            itemIndex === index
                              ? { ...value, code: event.target.value }
                              : value
                          )
                        )
                      }
                      placeholder="Code"
                    />
                    <Input
                      value={assumption.description}
                      onChange={(event) =>
                        setAssumptions((values) =>
                          values.map((value, itemIndex) =>
                            itemIndex === index
                              ? { ...value, description: event.target.value }
                              : value
                          )
                        )
                      }
                      placeholder="Description"
                    />
                    <Input
                      value={assumption.value}
                      onChange={(event) =>
                        setAssumptions((values) =>
                          values.map((value, itemIndex) =>
                            itemIndex === index
                              ? { ...value, value: event.target.value }
                              : value
                          )
                        )
                      }
                      placeholder="Value"
                    />
                    <Button
                      size="icon"
                      variant="ghost"
                      onClick={() =>
                        setAssumptions((values) =>
                          values.filter((_, itemIndex) => itemIndex !== index)
                        )
                      }
                    >
                      <X className="h-4 w-4" />
                    </Button>
                  </div>
                ))}
              </div>
              <div className="space-y-2">
                <Label>Controlled markups (%)</Label>
                <div className="grid gap-3 sm:grid-cols-2">
                  {MARKUPS.map((markup) => (
                    <div key={markup.component} className="space-y-1">
                      <Label className="text-xs text-muted-foreground">
                        {markup.label}
                      </Label>
                      <Input
                        type="number"
                        min="0"
                        step="0.01"
                        value={markups[markup.component] || ''}
                        onChange={(event) =>
                          setMarkups((values) => ({
                            ...values,
                            [markup.component]: event.target.value,
                          }))
                        }
                      />
                    </div>
                  ))}
                </div>
                <p className="text-xs text-muted-foreground">
                  Server validation enforces the approved QS-DEC-004 maximum for
                  each component.
                </p>
              </div>
              <Button
                className="w-full"
                onClick={() => void create()}
                disabled={loading || approvedBoqs.length === 0}
              >
                {loading ? 'Working…' : 'Create estimate snapshot'}
              </Button>
            </div>
          ) : (
            <div className="rounded-lg border p-4 text-sm text-muted-foreground">
              You can review estimate versions, but your role cannot prepare or
              submit them.
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
